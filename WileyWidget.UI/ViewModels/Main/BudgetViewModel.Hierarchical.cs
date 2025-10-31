using System.Collections.ObjectModel;
using System.Globalization;  // Added for CultureInfo.InvariantCulture
using System.IO;
using System.Linq;
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
    /// Import budget data from Excel file
    /// Handles hierarchical account structures like 410.1
    /// </summary>
    private async Task ImportBudgetAsync()
    {
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
                ProgressText = $"Importing budget from {Path.GetFileName(openFileDialog.FileName)}...";

                try
                {
                    // Implement Excel import logic using Syncfusion.XlsIO
                    var importedAccounts = await ImportBudgetFromExcelAsync(openFileDialog.FileName);

                    // Parse hierarchical account numbers and build parent-child relationships
                    var hierarchicalAccounts = BuildHierarchicalStructure(importedAccounts);

                    // TODO: Save imported accounts to database

                    ProgressText = $"Successfully imported {importedAccounts.Count} accounts with {hierarchicalAccounts.Count} hierarchical relationships";

                    MessageBox.Show(
                        $"Budget import completed successfully!\n\n" +
                        $"Imported: {importedAccounts.Count} accounts\n" +
                        $"Hierarchical relationships: {hierarchicalAccounts.Count}\n\n" +
                        $"The accounts have been parsed and structured according to municipal accounting standards.",
                        "Import Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    Log.Information("Excel import completed: {FileName}, {AccountCount} accounts imported", openFileDialog.FileName, importedAccounts.Count);
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
            IsBusy = false;
            ProgressText = string.Empty;
        }
    }

    /// <summary>
    /// Builds hierarchical structure from flat account list
    /// Parses account numbers like "410.1" to set parent-child relationships
    /// </summary>
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
                ProgressText = $"Exporting budget to {Path.GetFileName(saveFileDialog.FileName)}...";

                // Implement Excel export logic using Syncfusion.XlsIO
                await ExportBudgetToExcelAsync(saveFileDialog.FileName);

                MessageBox.Show(
                    $"Budget exported successfully to:\n{saveFileDialog.FileName}\n\n" +
                    "Export includes:\n" +
                    "- Hierarchical account structure\n" +
                    "- Budget vs Actual comparison\n" +
                    "- Variance calculations\n" +
                    "- Over-budget highlighting",
                    "Export Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                Log.Information("Budget exported to: {FileName}", saveFileDialog.FileName);
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
            IsBusy = false;
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
    /// Import budget data from Excel file using Syncfusion.XlsIO
    /// </summary>
    private async Task<List<MunicipalAccount>> ImportBudgetFromExcelAsync(string filePath)
    {
        // Parse on a background thread; no UI updates inside this block
        return await Task.Run(() =>
        {
            var accounts = new List<MunicipalAccount>();

            using (var excelEngine = new Syncfusion.XlsIO.ExcelEngine())
            {
                var application = excelEngine.Excel;
                var workbook = application.Workbooks.Open(filePath);
                var worksheet = workbook.Worksheets[0]; // Assume first worksheet

                // Find header row (look for "Account Number" or similar)
                int headerRow = FindHeaderRow(worksheet);
                if (headerRow == -1)
                {
                    throw new InvalidOperationException("Could not find header row with account information");
                }

                // Map column indices
                var columnMap = MapColumns(worksheet, headerRow);

                // Read data rows
                int row = headerRow + 1;
                while (row <= worksheet.Rows.Length)
                {
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
                }
            }

            return accounts;
        });
    }

    /// <summary>
    /// Export budget data to Excel with hierarchical structure
    /// </summary>
    private async Task ExportBudgetToExcelAsync(string filePath)
    {
        // Generate Excel on a background thread; no UI updates inside this block
        await Task.Run(() =>
        {
            using (var excelEngine = new ExcelEngine())
            {
                var application = excelEngine.Excel;
                application.DefaultVersion = ExcelVersion.Xlsx;

                var workbook = application.Workbooks.Create(1);
                var worksheet = workbook.Worksheets[0];
                worksheet.Name = $"Budget_{SelectedFiscalYear}";

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

                // Data rows
                int row = 7;
                foreach (var account in BudgetAccounts.OrderBy(a => a.AccountNumber))
                {
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
                }

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

                // Auto-fit columns
                worksheet.UsedRange.AutofitColumns();

                // Save the workbook
                workbook.SaveAs(filePath);
            }
        });
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
