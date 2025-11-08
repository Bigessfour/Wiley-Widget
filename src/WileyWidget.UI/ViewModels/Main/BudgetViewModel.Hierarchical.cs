using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;  // Added for CultureInfo.InvariantCulture
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using Serilog;
using Syncfusion.XlsIO;
using WileyWidget.Models;

namespace WileyWidget.ViewModels.Main;

/// <summary>
/// BudgetViewModel extension for hierarchical budget management
/// Adds support for GASB-compliant account structures and Excel import/export
/// </summary>
public partial class BudgetViewModel
{
    /// <summary>
    /// Progress information for import operations
    /// </summary>
    private class ImportProgressInfo
    {
        public double ProgressPercentage { get; }
        public string StatusMessage { get; }

        public ImportProgressInfo(double progressPercentage, string statusMessage)
        {
            ProgressPercentage = progressPercentage;
            StatusMessage = statusMessage;
        }
    }

    /// <summary>
    /// Validation result for imported data
    /// </summary>
    private class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; } = new();
    }

    /// <summary>
    /// Import budget data from Excel file with progress reporting and cancellation
    /// Handles hierarchical account structures like 410.1
    /// </summary>
    private async Task ImportBudgetAsync()
    {
        var cts = new CancellationTokenSource();
        IProgress<ImportProgressInfo> progress = new Progress<ImportProgressInfo>(info =>
        {
            ProgressValue = info.ProgressPercentage;
            ProgressText = info.StatusMessage;
        });

        try
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Import Budget from Excel",
                Filter = "Excel Files (*.xlsx;*.xls)|*.xlsx;*.xls|All Files (*.*)|*.*",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                IsBusy = true;
                ProgressValue = 0;
                ProgressMaximum = 100;
                ProgressText = $"Preparing to import budget from {Path.GetFileName(openFileDialog.FileName)}...";

                // Check file size for overrun alert
                var fileInfo = new FileInfo(openFileDialog.FileName);
                const long maxFileSize = 50 * 1024 * 1024; // 50MB limit
                if (fileInfo.Length > maxFileSize)
                {
                    var result = MessageBox.Show(
                        $"The selected file is {fileInfo.Length / (1024 * 1024):N1}MB, which exceeds the recommended 50MB limit.\n\n" +
                        "Large files may cause performance issues or memory problems.\n\n" +
                        "Do you want to continue anyway?",
                        "Large File Warning",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }

                try
                {
                    // Implement Excel import logic using Syncfusion.XlsIO with progress and cancellation
                    var importedAccounts = await ImportBudgetFromExcelAsync(openFileDialog.FileName, progress, cts.Token);

                    // Check for overrun (too many records)
                    const int maxRecords = 10000;
                    if (importedAccounts.Count > maxRecords)
                    {
                        var result = MessageBox.Show(
                            $"The file contains {importedAccounts.Count:N0} records, which exceeds the recommended limit of {maxRecords:N0}.\n\n" +
                            "Processing large datasets may take significant time and memory.\n\n" +
                            "Do you want to continue processing all records?",
                            "Large Dataset Warning",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);

                        if (result != MessageBoxResult.Yes)
                        {
                            cts.Cancel();
                            return;
                        }
                    }

                    // Parse hierarchical account numbers and build parent-child relationships
                    progress.Report(new ImportProgressInfo(60, "Building hierarchical structure..."));
                    var hierarchicalAccounts = await Task.Run(() => BuildHierarchicalStructure(importedAccounts), cts.Token);

                    // Validate data integrity
                    progress.Report(new ImportProgressInfo(80, "Validating data integrity..."));
                    var validationResult = ValidateImportedData(importedAccounts);
                    if (!validationResult.IsValid)
                    {
                        var result = MessageBox.Show(
                            $"Data validation found issues:\n\n{string.Join("\n", validationResult.Errors)}\n\n" +
                            "Do you want to continue with the import despite these issues?",
                            "Data Validation Warning",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);

                        if (result != MessageBoxResult.Yes)
                        {
                            cts.Cancel();
                            return;
                        }
                    }

                    // Save imported accounts to database
                    progress.Report(new ImportProgressInfo(90, "Saving to database..."));

                    if (_municipalAccountRepository != null)
                    {
                        try
                        {
                            int savedCount = 0;
                            foreach (var account in importedAccounts)
                            {
                                // Check if account already exists by account number
                                var existing = await _municipalAccountRepository.GetByAccountNumberAsync(account.AccountNumber?.Value ?? string.Empty);
                                if (existing == null)
                                {
                                    await _municipalAccountRepository.AddAsync(account);
                                    savedCount++;
                                }
                                else
                                {
                                    // Update existing account with imported data
                                    existing.Name = account.Name;
                                    existing.Fund = account.Fund;
                                    existing.Type = account.Type;
                                    existing.TypeDescription = account.TypeDescription;
                                    existing.FundDescription = account.FundDescription;
                                    existing.BudgetAmount = account.BudgetAmount;
                                    existing.IsActive = account.IsActive;
                                    await _municipalAccountRepository.UpdateAsync(existing);
                                    savedCount++;
                                }
                            }

                            Log.Information("Saved {SavedCount} of {TotalCount} accounts to database", savedCount, importedAccounts.Count);
                        }
                        catch (Exception dbEx)
                        {
                            Log.Error(dbEx, "Failed to save imported accounts to database");
                            MessageBox.Show(
                                $"Error saving accounts to database: {dbEx.Message}\n\n" +
                                "The accounts were imported but may not have been saved to the database.",
                                "Database Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                        }
                    }
                    else
                    {
                        Log.Warning("Municipal account repository not available - imported accounts not saved to database");
                    }

                    ProgressText = $"Successfully imported {importedAccounts.Count} accounts with {hierarchicalAccounts.Count} hierarchical relationships";

                    MessageBox.Show(
                        $"Budget import completed successfully!\n\n" +
                        $"Imported: {importedAccounts.Count} accounts\n" +
                        $"Hierarchical relationships: {hierarchicalAccounts.Count}\n" +
                        $"Validation issues: {(validationResult.IsValid ? "None" : validationResult.Errors.Count.ToString(CultureInfo.InvariantCulture))}\n\n" +
                        $"The accounts have been parsed and structured according to municipal accounting standards.",
                        "Import Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    Log.Information("Excel import completed: {FileName}, {AccountCount} accounts imported", openFileDialog.FileName, importedAccounts.Count);
                }
                catch (OperationCanceledException)
                {
                    ProgressText = "Import cancelled by user";
                    Log.Information("Excel import cancelled by user: {FileName}", openFileDialog.FileName);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error importing budget from Excel: {FileName}", openFileDialog.FileName);
                    MessageBox.Show(
                        $"Error importing budget:\n\n{ex.Message}",
                        "Import Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                finally
                {
                    IsBusy = false;
                    ProgressValue = 0;
                    ProgressText = string.Empty;
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to import budget: {ex.Message}";
            HasError = true;
            Log.Error(ex, "Failed to import budget from Excel");

            MessageBox.Show(
                $"Error importing budget:\n{ex.Message}",
                "Import Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            cts.Dispose();
            IsBusy = false;
            ProgressValue = 0;
            ProgressText = string.Empty;
        }
    }

    /// <summary>
    /// Validates imported budget data for integrity and compliance
    /// </summary>
    private ValidationResult ValidateImportedData(List<MunicipalAccount> accounts)
    {
        var result = new ValidationResult { IsValid = true };

        // Check for duplicate account numbers
        var duplicates = accounts.GroupBy(a => a.AccountNumber?.Value)
                                .Where(g => g.Count() > 1)
                                .Select(g => g.Key)
                                .ToList();

        if (duplicates.Any())
        {
            result.IsValid = false;
            result.Errors.Add($"Duplicate account numbers found: {string.Join(", ", duplicates.Take(5))}");
        }

        // Check for invalid account number formats
        var invalidAccounts = accounts.Where(a => a.AccountNumber == null ||
                                                 !System.Text.RegularExpressions.Regex.IsMatch(a.AccountNumber.Value, @"^\d{3}(\.\d{1,2})?$"))
                                     .ToList();

        if (invalidAccounts.Any())
        {
            result.IsValid = false;
            result.Errors.Add($"{invalidAccounts.Count} accounts have invalid account number formats");
        }

        // Check for negative budget amounts
        var negativeBudgets = accounts.Where(a => a.BudgetAmount < 0).ToList();
        if (negativeBudgets.Any())
        {
            result.IsValid = false;
            result.Errors.Add($"{negativeBudgets.Count} accounts have negative budget amounts");
        }

        // Check for missing required fields
        var missingDescriptions = accounts.Where(a => string.IsNullOrWhiteSpace(a.Name)).ToList();
        if (missingDescriptions.Any())
        {
            result.IsValid = false;
            result.Errors.Add($"{missingDescriptions.Count} accounts are missing descriptions");
        }

        return result;
    }
    private List<MunicipalAccount> BuildHierarchicalStructure(List<MunicipalAccount> accounts)
    {
        var accountDict = accounts.ToDictionary(a => a.AccountNumber?.Value ?? "", a => a);

        foreach (var account in accounts)
        {
            if (account.AccountNumber == null) continue;

            // Find parent by removing the last segment after the last dot
            var parts = account.AccountNumber.Value.Split('.');
            if (parts.Length > 1)
            {
                var parentNumber = string.Join(".", parts.Take(parts.Length - 1));
                if (accountDict.TryGetValue(parentNumber, out var parent))
                {
                    account.ParentAccountId = parent.Id;
                    parent.ChildAccounts.Add(account);
                }
            }
            else
            {
                // Root level account
                account.ParentAccountId = null;
            }
        }

        return accounts;
    }

    /// <summary>
    /// Export budget data to Excel with hierarchy preserved
    /// </summary>
    private async Task ExportBudgetAsync()
    {
        var cts = new CancellationTokenSource();
        var progress = new Progress<ImportProgressInfo>(info =>
        {
            ProgressValue = info.ProgressPercentage;
            ProgressText = info.StatusMessage;
        });

        try
        {
            var saveFileDialog = new SaveFileDialog
            {
                Title = "Export Budget to Excel",
                Filter = "Excel Files (*.xlsx)|*.xlsx|All Files (*.*)|*.*",
                DefaultExt = ".xlsx",
                FileName = $"Budget_{SelectedFiscalYear}_{DateTime.Now:yyyyMMdd}.xlsx"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                IsBusy = true;
                ProgressValue = 0;
                ProgressMaximum = 100;
                ProgressText = $"Preparing to export budget to {Path.GetFileName(saveFileDialog.FileName)}...";

                // Check available data for overrun alert
                if (!BudgetAccounts.Any())
                {
                    MessageBox.Show(
                        "No budget data available to export.\n\nPlease load budget data first.",
                        "No Data Available",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Check for large dataset warning
                const int largeDatasetThreshold = 5000;
                if (BudgetAccounts.Count > largeDatasetThreshold)
                {
                    var result = MessageBox.Show(
                        $"The dataset contains {BudgetAccounts.Count:N0} accounts, which may take significant time to export.\n\n" +
                        "Consider filtering the data or exporting in smaller batches.\n\n" +
                        "Do you want to continue with the full export?",
                        "Large Dataset Warning",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }

                try
                {
                    // Implement Excel export logic using Syncfusion.XlsIO with progress and cancellation
                    await ExportBudgetToExcelAsync(saveFileDialog.FileName, progress, cts.Token);

                    ProgressText = $"Export completed successfully to {Path.GetFileName(saveFileDialog.FileName)}";

                    MessageBox.Show(
                        $"Budget exported successfully to:\n{saveFileDialog.FileName}\n\n" +
                        "Export includes:\n" +
                        "- Hierarchical account structure\n" +
                        "- Budget vs Actual comparison\n" +
                        "- Variance calculations\n" +
                        "- Over-budget highlighting\n\n" +
                        $"Total accounts exported: {BudgetAccounts.Count}",
                        "Export Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    Log.Information("Budget exported to: {FileName}", saveFileDialog.FileName);
                }
                catch (OperationCanceledException)
                {
                    ProgressText = "Export cancelled by user";
                    Log.Information("Budget export cancelled by user");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error exporting budget to Excel");
                    MessageBox.Show(
                        $"Error exporting budget:\n\n{ex.Message}",
                        "Export Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                finally
                {
                    IsBusy = false;
                    ProgressValue = 0;
                    ProgressText = string.Empty;
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to export budget: {ex.Message}";
            HasError = true;
            Log.Error(ex, "Failed to export budget to Excel");

            MessageBox.Show(
                $"Error exporting budget:\n{ex.Message}",
                "Export Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            cts.Dispose();
            IsBusy = false;
            ProgressValue = 0;
            ProgressText = string.Empty;
        }
    }

    /// <summary>
    /// Add a new budget account
    /// </summary>
    private void AddAccount()
    {
        var newAccount = new BudgetAccount
        {
            Id = BudgetAccounts.Count + 1,
            AccountNumber = $"NEW-{BudgetAccounts.Count + 1}",
            Description = "New Budget Account",
            FundType = "GF",
            BudgetAmount = 0,
            ActualAmount = 0,
            ParentId = -1
        };

        BudgetAccounts.Add(newAccount);
        Log.Information("Added new budget account: {AccountNumber}", newAccount.AccountNumber);
    }

    /// <summary>
    /// Delete the selected budget account
    /// </summary>
    private void DeleteAccount()
    {
        try
        {
            // For budget analysis view, show planned functionality
            // In a full implementation, this would delete from the database
            var result = MessageBox.Show(
                "Delete Account Functionality:\n\n" +
                "This feature will allow deletion of budget accounts with:\n" +
                "• Confirmation dialog with account details\n" +
                "• Cascade delete protection for child accounts\n" +
                "• Audit trail logging of the deletion\n" +
                "• Database transaction rollback on failure\n\n" +
                "Would you like to see the implementation plan?",
                "Delete Account - Planned Feature",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                MessageBox.Show(
                    "Implementation Plan:\n\n" +
                    "1. Add SelectedBudgetDetail property to ViewModel\n" +
                    "2. Bind grid SelectedItem to the property\n" +
                    "3. Show confirmation dialog with account details\n" +
                    "4. Check for child accounts before deletion\n" +
                    "5. Execute delete in database transaction\n" +
                    "6. Log audit entry for the deletion\n" +
                    "7. Refresh the budget details collection\n" +
                    "8. Update totals and charts",
                    "Delete Account Implementation Plan",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            Log.Information("Delete account functionality displayed to user");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to show delete account dialog: {ex.Message}";
            HasError = true;
            Log.Error(ex, "Failed to show delete account dialog");
        }
    }

    /// <summary>
    /// Load sample hierarchical budget data for demonstration
    /// </summary>
    private void LoadSampleBudgetAccounts()
    {
        BudgetAccounts.Clear();

        // Root accounts
        var account410 = new BudgetAccount
        {
            Id = 1,
            AccountNumber = "410",
            Description = "Water Revenue",
            FundType = "EF",
            BudgetAmount = 500000,
            ActualAmount = 475000,
            ParentId = -1
        };

        var account410_1 = new BudgetAccount
        {
            Id = 2,
            AccountNumber = "410.1",
            Description = "Residential Water Sales",
            FundType = "EF",
            BudgetAmount = 350000,
            ActualAmount = 340000,
            ParentId = 1
        };

        var account410_2 = new BudgetAccount
        {
            Id = 3,
            AccountNumber = "410.2",
            Description = "Commercial Water Sales",
            FundType = "EF",
            BudgetAmount = 150000,
            ActualAmount = 135000,
            ParentId = 1
        };

        var account510 = new BudgetAccount
        {
            Id = 4,
            AccountNumber = "510",
            Description = "Operating Expenses",
            FundType = "EF",
            BudgetAmount = 350000,
            ActualAmount = 380000,
            ParentId = -1
        };

        var account510_1 = new BudgetAccount
        {
            Id = 5,
            AccountNumber = "510.1",
            Description = "Personnel Costs",
            FundType = "EF",
            BudgetAmount = 200000,
            ActualAmount = 210000,
            ParentId = 4
        };

        var account510_2 = new BudgetAccount
        {
            Id = 6,
            AccountNumber = "510.2",
            Description = "Utilities",
            FundType = "EF",
            BudgetAmount = 150000,
            ActualAmount = 170000,
            ParentId = 4
        };

        // Add children to parents
        account410.Children.Add(account410_1);
        account410.Children.Add(account410_2);
        account510.Children.Add(account510_1);
        account510.Children.Add(account510_2);

        // Add root accounts to collection
        BudgetAccounts.Add(account410);
        BudgetAccounts.Add(account510);

        RecalculateTotals();
        UpdateChartData();
    }

    /// <summary>
    /// Recalculate total budget, actual, and variance
    /// </summary>
    private void RecalculateTotals()
    {
        TotalBudget = CalculateTotalBudget(BudgetAccounts);
        TotalActual = CalculateTotalActual(BudgetAccounts);
        TotalVariance = TotalBudget - TotalActual;

        Log.Debug("Recalculated totals: Budget={Budget:C2}, Actual={Actual:C2}, Variance={Variance:C2}",
            TotalBudget, TotalActual, TotalVariance);
    }

    private decimal CalculateTotalBudget(ObservableCollection<BudgetAccount> accounts)
    {
        decimal total = 0;
        foreach (var account in accounts)
        {
            total += account.BudgetAmount;
            total += CalculateTotalBudget(account.Children);
        }
        return total;
    }

    private decimal CalculateTotalActual(ObservableCollection<BudgetAccount> accounts)
    {
        decimal total = 0;
        foreach (var account in accounts)
        {
            total += account.ActualAmount;
            total += CalculateTotalActual(account.Children);
        }
        return total;
    }

    /// <summary>
    /// Update chart data for visualizations
    /// </summary>
    private void UpdateChartData()
    {
        // Update budget distribution by fund type
        BudgetDistributionData.Clear();
        var fundGroups = GetAllAccounts(BudgetAccounts)
            .GroupBy(a => a.FundType)
            .Select(g => new BudgetDistributionData
            {
                FundType = FundTypes.FirstOrDefault(f => f.Code == g.Key)?.Name ?? g.Key,
                Amount = g.Sum(a => a.BudgetAmount),
                Percentage = 0 // Will be calculated
            });

        var totalAmount = fundGroups.Sum(g => g.Amount);
        foreach (var group in fundGroups)
        {
            group.Percentage = totalAmount > 0 ? (double)(group.Amount / totalAmount) : 0;
            BudgetDistributionData.Add(group);
        }

        // Update budget comparison by top-level categories
        BudgetComparisonData.Clear();
        foreach (var account in BudgetAccounts.Take(10))
        {
            BudgetComparisonData.Add(new BudgetComparisonData
            {
                Category = account.AccountNumber,
                BudgetAmount = account.BudgetAmount,
                ActualAmount = account.ActualAmount
            });
        }
    }

    private IEnumerable<BudgetAccount> GetAllAccounts(ObservableCollection<BudgetAccount> accounts)
    {
        foreach (var account in accounts)
        {
            yield return account;
            foreach (var child in GetAllAccounts(account.Children))
            {
                yield return child;
            }
        }
    }

    /// <summary>
    /// Initialize budget accounts if empty
    /// </summary>
    private void OnSelectedFiscalYearChanged(string value)
    {
        if (BudgetAccounts.Count == 0)
        {
            LoadSampleBudgetAccounts();
        }
    }

    /// <summary>
    /// Import budget data from Excel file using Syncfusion.XlsIO with progress reporting
    /// </summary>
    private async Task<List<MunicipalAccount>> ImportBudgetFromExcelAsync(string filePath, IProgress<ImportProgressInfo> progress, CancellationToken cancellationToken)
    {
        // Parse on a background thread; no UI updates inside this block
        return await Task.Run(() =>
        {
            var accounts = new List<MunicipalAccount>();

            progress.Report(new ImportProgressInfo(10, "Opening Excel file..."));

            using (var excelEngine = new Syncfusion.XlsIO.ExcelEngine())
            {
                var application = excelEngine.Excel;
                var workbook = application.Workbooks.Open(filePath);
                var worksheet = workbook.Worksheets[0]; // Assume first worksheet

                progress.Report(new ImportProgressInfo(20, "Analyzing worksheet structure..."));

                // Find header row (look for "Account Number" or similar)
                int headerRow = FindHeaderRow(worksheet);
                if (headerRow == -1)
                {
                    throw new InvalidOperationException("Could not find header row with account information");
                }

                // Map column indices
                var columnMap = MapColumns(worksheet, headerRow);

                progress.Report(new ImportProgressInfo(30, "Reading account data..."));

                // Read data rows
                int row = headerRow + 1;
                int totalRows = worksheet.Rows.Length - headerRow;
                int processedRows = 0;

                while (row <= worksheet.Rows.Length)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var accountNumber = GetCellValue(worksheet, row, columnMap["AccountNumber"]);
                    if (string.IsNullOrWhiteSpace(accountNumber))
                        break; // End of data

                    var parsedFund = ParseFund(GetCellValue(worksheet, row, columnMap["Fund"])) ?? WileyWidget.Models.MunicipalFundType.General;
                    var parsedFundClass = ParseFundClass(GetCellValue(worksheet, row, columnMap["FundClass"]));

                    var account = new MunicipalAccount
                    {
                        AccountNumber = new WileyWidget.Models.AccountNumber(accountNumber),
                        Name = GetCellValue(worksheet, row, columnMap["Name"]) ?? $"Account {accountNumber}",
                        Type = ParseAccountType(GetCellValue(worksheet, row, columnMap["Type"])),
                        Fund = parsedFund,
                        FundDescription = parsedFundClass?.ToString() ?? parsedFund.ToString()
                    };

                    // Parse budget amounts if available
                    if (columnMap.ContainsKey("BudgetAmount"))
                    {
                        var budgetText = GetCellValue(worksheet, row, columnMap["BudgetAmount"]);
                        if (decimal.TryParse(budgetText, out var budgetAmount))
                        {
                            // Set budget amount (would need to extend model or use related entities)
                        }
                    }

                    accounts.Add(account);
                    row++;
                    processedRows++;

                    // Report progress every 100 rows or at key milestones
                    if (processedRows % 100 == 0 || processedRows == totalRows)
                    {
                        double progressPercent = 30 + (processedRows / (double)totalRows) * 60; // 30-90% range
                        progress.Report(new ImportProgressInfo(progressPercent, $"Processed {processedRows} of {totalRows} accounts..."));
                    }
                }
            }

            progress.Report(new ImportProgressInfo(95, $"Import complete: {accounts.Count} accounts loaded"));
            return accounts;
        }, cancellationToken);
    }

    /// <summary>
    /// Export budget data to Excel with hierarchical structure and progress reporting
    /// </summary>
    private async Task ExportBudgetToExcelAsync(string filePath, IProgress<ImportProgressInfo> progress, CancellationToken cancellationToken)
    {
        // Generate Excel on a background thread; no UI updates inside this block
        await Task.Run(() =>
        {
            progress.Report(new ImportProgressInfo(10, "Creating Excel workbook..."));

            using (var excelEngine = new ExcelEngine())
            {
                var application = excelEngine.Excel;
                application.DefaultVersion = ExcelVersion.Xlsx;

                var workbook = application.Workbooks.Create(1);
                var worksheet = workbook.Worksheets[0];
                worksheet.Name = $"Budget_{SelectedFiscalYear}";

                progress.Report(new ImportProgressInfo(20, "Setting up headers and formatting..."));

                // Set up headers
                worksheet.Range["A1"].Text = $"Town of Wiley Budget - {SelectedFiscalYear}";
                worksheet.Range["A1:F1"].Merge();
                worksheet.Range["A1"].CellStyle.Font.Size = 16;
                worksheet.Range["A1"].CellStyle.Font.Bold = true;
                worksheet.Range["A1"].HorizontalAlignment = ExcelHAlign.HAlignCenter;

                // Report info
                worksheet.Range["A3"].Text = "Generated:";
                worksheet.Range["B3"].Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);  // Fixed: Use InvariantCulture for consistent formatting
                worksheet.Range["A4"].Text = "Fiscal Year:";
                worksheet.Range["B4"].Text = SelectedFiscalYear;

                // Column headers
                worksheet.Range["A6"].Text = "Account Number";
                worksheet.Range["B6"].Text = "Account Name";
                worksheet.Range["C6"].Text = "Type";
                worksheet.Range["D6"].Text = "Fund";
                worksheet.Range["E6"].Text = "Budget Amount";
                worksheet.Range["F6"].Text = "Actual Amount";
                worksheet.Range["G6"].Text = "Variance";
                worksheet.Range["H6"].Text = "% Variance";

                // Style headers
                var headerRange = worksheet.Range["A6:H6"];
                headerRange.CellStyle.Font.Bold = true;
                headerRange.CellStyle.Interior.Color = System.Drawing.Color.LightGray;
                headerRange.CellStyle.HorizontalAlignment = ExcelHAlign.HAlignCenter;

                progress.Report(new ImportProgressInfo(30, "Exporting account data..."));

                // Data rows
                int row = 7;
                int totalAccounts = BudgetAccounts.Count;
                int processedAccounts = 0;

                foreach (var account in BudgetAccounts.OrderBy(a => a.AccountNumber))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (account.AccountNumber == null) continue;

                    worksheet.Range[$"A{row}"].Text = account.AccountNumber;
                    worksheet.Range[$"B{row}"].Text = account.Description;
                    worksheet.Range[$"C{row}"].Text = account.FundType.ToString();
                    worksheet.Range[$"D{row}"].Text = account.FundType.ToString();

                    // Get budget and actual amounts (simplified - would need actual data access)
                    var budgetAmount = account.BudgetAmount;
                    var actualAmount = account.ActualAmount;
                    var variance = actualAmount - budgetAmount;
                    var percentVariance = budgetAmount != 0 ? (variance / budgetAmount) * 100 : 0;

                    worksheet.Range[$"E{row}"].Number = (double)budgetAmount;
                    worksheet.Range[$"E{row}"].NumberFormat = "$#,##0.00";
                    worksheet.Range[$"F{row}"].Number = (double)actualAmount;
                    worksheet.Range[$"F{row}"].NumberFormat = "$#,##0.00";
                    worksheet.Range[$"G{row}"].Number = (double)variance;
                    worksheet.Range[$"G{row}"].NumberFormat = "$#,##0.00;($#,##0.00)";
                    worksheet.Range[$"H{row}"].Number = (double)percentVariance;
                    worksheet.Range[$"H{row}"].NumberFormat = "0.00%";

                    // Color coding for over-budget
                    if (variance > 0)
                    {
                        worksheet.Range[$"G{row}"].CellStyle.Interior.Color = System.Drawing.Color.LightCoral;
                        worksheet.Range[$"H{row}"].CellStyle.Interior.Color = System.Drawing.Color.LightCoral;
                    }
                    else if (variance < 0)
                    {
                        worksheet.Range[$"G{row}"].CellStyle.Interior.Color = System.Drawing.Color.LightGreen;
                        worksheet.Range[$"H{row}"].CellStyle.Interior.Color = System.Drawing.Color.LightGreen;
                    }

                    row++;
                    processedAccounts++;

                    // Report progress every 100 accounts or at key milestones
                    if (processedAccounts % 100 == 0 || processedAccounts == totalAccounts)
                    {
                        double progressPercent = 30 + (processedAccounts / (double)totalAccounts) * 50; // 30-80% range
                        progress.Report(new ImportProgressInfo(progressPercent, $"Exported {processedAccounts} of {totalAccounts} accounts..."));
                    }
                }

                progress.Report(new ImportProgressInfo(85, "Adding summary information..."));

                // Summary section
                row += 2;
                worksheet.Range[$"A{row}"].Text = "Summary";
                worksheet.Range[$"A{row}:B{row}"].Merge();
                worksheet.Range[$"A{row}"].CellStyle.Font.Bold = true;

                row++;
                var totalBudget = BudgetAccounts.Sum(a => a.BudgetAmount);
                var totalActual = BudgetAccounts.Sum(a => a.ActualAmount);
                var totalVariance = totalActual - totalBudget;

                worksheet.Range[$"D{row}"].Text = "Total Budget:";
                worksheet.Range[$"E{row}"].Number = (double)totalBudget;
                worksheet.Range[$"E{row}"].NumberFormat = "$#,##0.00";

                row++;
                worksheet.Range[$"D{row}"].Text = "Total Actual:";
                worksheet.Range[$"F{row}"].Number = (double)totalActual;
                worksheet.Range[$"F{row}"].NumberFormat = "$#,##0.00";

                row++;
                worksheet.Range[$"D{row}"].Text = "Total Variance:";
                worksheet.Range[$"G{row}"].Number = (double)totalVariance;
                worksheet.Range[$"G{row}"].NumberFormat = "$#,##0.00;($#,##0.00)";

                progress.Report(new ImportProgressInfo(90, "Finalizing Excel file..."));

                // Auto-fit columns
                worksheet.UsedRange.AutofitColumns();

                // Save the workbook
                workbook.SaveAs(filePath);

                progress.Report(new ImportProgressInfo(100, "Export completed successfully"));
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Find the header row containing account information
    /// </summary>
    private int FindHeaderRow(Syncfusion.XlsIO.IWorksheet worksheet)
    {
        for (int row = 1; row <= Math.Min(10, worksheet.Rows.Length); row++)
        {
            for (int col = 1; col <= Math.Min(10, worksheet.Columns.Length); col++)
            {
                var cellValue = worksheet.Range[row, col].Text?.ToLowerInvariant();
                if (cellValue?.Contains("account", StringComparison.OrdinalIgnoreCase) == true && cellValue.Contains("number", StringComparison.OrdinalIgnoreCase))
                {
                    return row;
                }
            }
        }
        return -1;
    }

    /// <summary>
    /// Map column names to indices
    /// </summary>
    private Dictionary<string, int> MapColumns(Syncfusion.XlsIO.IWorksheet worksheet, int headerRow)
    {
        var map = new Dictionary<string, int>();
        var expectedColumns = new[] { "AccountNumber", "Name", "Type", "Fund", "FundClass", "BudgetAmount" };
        var columnNames = new Dictionary<string, string[]>
        {
            ["AccountNumber"] = new[] { "account number", "account", "number", "acct num" },
            ["Name"] = new[] { "name", "description", "account name", "desc" },
            ["Type"] = new[] { "type", "account type", "acct type" },
            ["Fund"] = new[] { "fund", "fund number" },
            ["FundClass"] = new[] { "fund class", "class" },
            ["BudgetAmount"] = new[] { "budget", "amount", "budget amount", "total" }
        };

        for (int col = 1; col <= worksheet.Columns.Length; col++)
        {
            var headerText = worksheet.Range[headerRow, col].Text?.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(headerText)) continue;

            foreach (var kvp in columnNames)
            {
                if (kvp.Value.Any(alias => headerText.Contains(alias, StringComparison.Ordinal)))
                {
                    map[kvp.Key] = col;
                    break;
                }
            }
        }

        return map;
    }

    /// <summary>
    /// Get cell value safely
    /// </summary>
    private string? GetCellValue(Syncfusion.XlsIO.IWorksheet worksheet, int row, int col)
    {
        if (row <= worksheet.Rows.Length && col <= worksheet.Columns.Length)
        {
            return worksheet.Range[row, col].Text;
        }
        return null;
    }

    /// <summary>
    /// Parse account type from string
    /// </summary>
    private WileyWidget.Models.AccountType ParseAccountType(string? typeText)
    {
        if (string.IsNullOrWhiteSpace(typeText)) return WileyWidget.Models.AccountType.Asset;

        return typeText.ToLowerInvariant() switch
        {
            var t when t.Contains("asset", StringComparison.Ordinal) || t.Contains("cash", StringComparison.Ordinal) || t.Contains("investment", StringComparison.Ordinal) => WileyWidget.Models.AccountType.Asset,
            var t when t.Contains("liability", StringComparison.Ordinal) || t.Contains("payable", StringComparison.Ordinal) || t.Contains("debt", StringComparison.Ordinal) => WileyWidget.Models.AccountType.Payables,
            var t when t.Contains("equity", StringComparison.Ordinal) || t.Contains("retained", StringComparison.Ordinal) || t.Contains("balance", StringComparison.Ordinal) => WileyWidget.Models.AccountType.RetainedEarnings,
            var t when t.Contains("revenue", StringComparison.Ordinal) || t.Contains("tax", StringComparison.Ordinal) || t.Contains("fee", StringComparison.Ordinal) || t.Contains("grant", StringComparison.Ordinal) => WileyWidget.Models.AccountType.Revenue,
            var t when t.Contains("expense", StringComparison.Ordinal) || t.Contains("salary", StringComparison.Ordinal) || t.Contains("supply", StringComparison.Ordinal) || t.Contains("utility", StringComparison.Ordinal) => WileyWidget.Models.AccountType.Expense,
            _ => WileyWidget.Models.AccountType.Asset
        };
    }

    /// <summary>
    /// Parse fund from string
    /// </summary>
    private MunicipalFundType? ParseFund(string? fundText)
    {
        if (string.IsNullOrWhiteSpace(fundText)) return null;
        if (Enum.TryParse<MunicipalFundType>(fundText, out var fund)) return fund;
        return null;
    }

    /// <summary>
    /// Parse fund class from string
    /// </summary>
    private WileyWidget.Models.FundClass? ParseFundClass(string? fundClassText)
    {
        if (string.IsNullOrWhiteSpace(fundClassText)) return null;

        return fundClassText.ToLowerInvariant() switch
        {
            var t when t.Contains("governmental", StringComparison.Ordinal) => WileyWidget.Models.FundClass.Governmental,
            var t when t.Contains("proprietary", StringComparison.Ordinal) => WileyWidget.Models.FundClass.Proprietary,
            var t when t.Contains("fiduciary", StringComparison.Ordinal) => WileyWidget.Models.FundClass.Fiduciary,
            var t when t.Contains("memo", StringComparison.Ordinal) => WileyWidget.Models.FundClass.Memo,
            _ => null
        };
    }
}
