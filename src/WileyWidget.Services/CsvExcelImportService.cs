using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using ExcelDataReader;
using Microsoft.Extensions.Logging;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services
{
    /// <summary>
    /// Service for importing budget and account data from CSV and Excel files
    /// Uses CsvHelper for CSV parsing and ExcelDataReader for Excel files
    /// </summary>
    public class CsvExcelImportService
    {
        private readonly ILogger<CsvExcelImportService> _logger;

        public CsvExcelImportService(ILogger<CsvExcelImportService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        static CsvExcelImportService()
        {
            // Register ExcelDataReader encoding provider for legacy Excel formats
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        /// <summary>
        /// Imports transaction data from CSV or Excel file
        /// </summary>
        public async Task<ImportResult> ImportTransactionsAsync(string filePath)
        {
            try
            {
                var extension = Path.GetExtension(filePath).ToLowerInvariant();

                return extension switch
                {
                    ".csv" => await ImportTransactionsFromCsvAsync(filePath),
                    ".xlsx" or ".xls" => await ImportTransactionsFromExcelAsync(filePath),
                    _ => new ImportResult { Success = false, ErrorMessage = $"Unsupported file extension: {extension}" }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import transactions from {FilePath}", filePath);
                return new ImportResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        /// <summary>
        /// Imports budget entries from CSV or Excel file
        /// </summary>
        public async Task<ImportResult> ImportBudgetEntriesAsync(string filePath)
        {
            try
            {
                var extension = Path.GetExtension(filePath).ToLowerInvariant();

                return extension switch
                {
                    ".csv" => await ImportBudgetEntriesFromCsvAsync(filePath),
                    ".xlsx" or ".xls" => await ImportBudgetEntriesFromExcelAsync(filePath),
                    _ => new ImportResult { Success = false, ErrorMessage = $"Unsupported file extension: {extension}" }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import budget entries from {FilePath}", filePath);
                return new ImportResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        private async Task<ImportResult> ImportTransactionsFromCsvAsync(string filePath)
        {
            var transactions = new List<Transaction>();
            var errors = new List<string>();

            await Task.Run(() =>
            {
                using var reader = new StreamReader(filePath);
                using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true,
                    MissingFieldFound = null,
                    BadDataFound = context => errors.Add($"Line {context.RawRecord}: Bad data")
                });

                csv.Context.RegisterClassMap<TransactionCsvMap>();

                try
                {
                    var records = csv.GetRecords<TransactionImportRow>().ToList();

                    foreach (var record in records)
                    {
                        try
                        {
                            transactions.Add(new Transaction
                            {
                                Description = record.Description ?? "Imported Transaction",
                                Amount = record.Amount,
                                TransactionDate = record.Date ?? DateTime.Now,
                                Type = record.Type?.ToLower() == "credit" ? "Credit" : "Debit",
                                BudgetEntryId = record.BudgetEntryId > 0 ? record.BudgetEntryId : 0,
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow
                            });
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"Line {record.RowNumber}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"CSV parsing error: {ex.Message}");
                }
            });

            _logger.LogInformation("Imported {Count} transactions from CSV with {ErrorCount} errors",
                transactions.Count, errors.Count);

            return new ImportResult
            {
                Success = errors.Count == 0,
                AccountsImported = transactions.Count,
                ErrorMessage = errors.Any() ? string.Join("; ", errors.Take(10)) : null,
                ValidationErrors = errors.Any() ? errors : null
            };
        }

        private async Task<ImportResult> ImportTransactionsFromExcelAsync(string filePath)
        {
            var transactions = new List<Transaction>();
            var errors = new List<string>();

            await Task.Run(() =>
            {
                using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = ExcelReaderFactory.CreateReader(stream);

                var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
                {
                    ConfigureDataTable = _ => new ExcelDataTableConfiguration
                    {
                        UseHeaderRow = true
                    }
                });

                var table = dataSet.Tables[0];

                for (int i = 0; i < table.Rows.Count; i++)
                {
                    try
                    {
                        var row = table.Rows[i];

                        var description = row["Description"]?.ToString() ?? row["Memo"]?.ToString() ?? "Imported Transaction";
                        var amountStr = row["Amount"]?.ToString() ?? row["Value"]?.ToString() ?? "0";
                        var dateStr = row["Date"]?.ToString() ?? row["TransactionDate"]?.ToString();
                        var typeStr = row["Type"]?.ToString() ?? row["TransactionType"]?.ToString() ?? "Debit";
                        var budgetEntryIdStr = row["BudgetEntryId"]?.ToString() ?? "0";

                        if (decimal.TryParse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount) &&
                            int.TryParse(budgetEntryIdStr, out var budgetEntryId))
                        {
                            DateTime transactionDate = DateTime.Now;
                            if (!string.IsNullOrEmpty(dateStr))
                            {
                                DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out transactionDate);
                            }

                            transactions.Add(new Transaction
                            {
                                Description = description,
                                Amount = amount,
                                TransactionDate = transactionDate,
                                Type = typeStr.ToLower().Contains("credit") ? "Credit" : "Debit",
                                BudgetEntryId = budgetEntryId,
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow
                            });
                        }
                        else
                        {
                            errors.Add($"Row {i + 2}: Invalid amount or budget entry ID");
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Row {i + 2}: {ex.Message}");
                    }
                }
            });

            _logger.LogInformation("Imported {Count} transactions from Excel with {ErrorCount} errors",
                transactions.Count, errors.Count);

            return new ImportResult
            {
                Success = errors.Count == 0,
                AccountsImported = transactions.Count,
                ErrorMessage = errors.Any() ? string.Join("; ", errors.Take(10)) : null,
                ValidationErrors = errors.Any() ? errors : null
            };
        }

        private async Task<ImportResult> ImportBudgetEntriesFromCsvAsync(string filePath)
        {
            var budgetEntries = new List<BudgetEntry>();
            var errors = new List<string>();

            await Task.Run(() =>
            {
                using var reader = new StreamReader(filePath);
                using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true,
                    MissingFieldFound = null,
                    BadDataFound = context => errors.Add($"Line {context.RawRecord}: Bad data")
                });

                csv.Context.RegisterClassMap<BudgetEntryCsvMap>();

                try
                {
                    var records = csv.GetRecords<BudgetEntryImportRow>().ToList();

                    foreach (var record in records)
                    {
                        try
                        {
                            budgetEntries.Add(new BudgetEntry
                            {
                                AccountNumber = record.AccountNumber ?? "000",
                                Description = record.Description ?? "Imported Budget Entry",
                                BudgetedAmount = record.BudgetedAmount,
                                ActualAmount = record.ActualAmount,
                                FiscalYear = record.FiscalYear > 0 ? record.FiscalYear : DateTime.Now.Year,
                                DepartmentId = record.DepartmentId > 0 ? record.DepartmentId : 1,
                                FundType = Enum.TryParse<FundType>(record.FundType, true, out var fundType) ? fundType : FundType.GeneralFund,
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow
                            });
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"Line {record.RowNumber}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"CSV parsing error: {ex.Message}");
                }
            });

            _logger.LogInformation("Imported {Count} budget entries from CSV with {ErrorCount} errors",
                budgetEntries.Count, errors.Count);

            return new ImportResult
            {
                Success = errors.Count == 0,
                AccountsImported = budgetEntries.Count,
                ErrorMessage = errors.Any() ? string.Join("; ", errors.Take(10)) : null,
                ValidationErrors = errors.Any() ? errors : null
            };
        }

        private async Task<ImportResult> ImportBudgetEntriesFromExcelAsync(string filePath)
        {
            var budgetEntries = new List<BudgetEntry>();
            var errors = new List<string>();

            await Task.Run(() =>
            {
                using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = ExcelReaderFactory.CreateReader(stream);

                var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
                {
                    ConfigureDataTable = _ => new ExcelDataTableConfiguration
                    {
                        UseHeaderRow = true
                    }
                });

                var table = dataSet.Tables[0];

                for (int i = 0; i < table.Rows.Count; i++)
                {
                    try
                    {
                        var row = table.Rows[i];

                        var accountNumber = row["AccountNumber"]?.ToString() ?? row["Account"]?.ToString() ?? "000";
                        var description = row["Description"]?.ToString() ?? "Imported Budget Entry";
                        var budgetedStr = row["BudgetedAmount"]?.ToString() ?? row["Budget"]?.ToString() ?? "0";
                        var actualStr = row["ActualAmount"]?.ToString() ?? row["Actual"]?.ToString() ?? "0";
                        var fiscalYearStr = row["FiscalYear"]?.ToString() ?? row["Year"]?.ToString();
                        var departmentIdStr = row["DepartmentId"]?.ToString() ?? row["Department"]?.ToString() ?? "1";
                        var fundTypeStr = row["FundType"]?.ToString() ?? row["Fund"]?.ToString() ?? "GeneralFund";

                        if (decimal.TryParse(budgetedStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var budgeted) &&
                            decimal.TryParse(actualStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var actual) &&
                            int.TryParse(departmentIdStr, out var departmentId))
                        {
                            int fiscalYear = DateTime.Now.Year;
                            if (!string.IsNullOrEmpty(fiscalYearStr))
                            {
                                int.TryParse(fiscalYearStr, out fiscalYear);
                            }

                            budgetEntries.Add(new BudgetEntry
                            {
                                AccountNumber = accountNumber,
                                Description = description,
                                BudgetedAmount = budgeted,
                                ActualAmount = actual,
                                FiscalYear = fiscalYear,
                                DepartmentId = departmentId,
                                FundType = Enum.TryParse<FundType>(fundTypeStr, true, out var fundType) ? fundType : FundType.GeneralFund,
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow
                            });
                        }
                        else
                        {
                            errors.Add($"Row {i + 2}: Invalid amounts or department ID");
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Row {i + 2}: {ex.Message}");
                    }
                }
            });

            _logger.LogInformation("Imported {Count} budget entries from Excel with {ErrorCount} errors",
                budgetEntries.Count, errors.Count);

            return new ImportResult
            {
                Success = errors.Count == 0,
                AccountsImported = budgetEntries.Count,
                ErrorMessage = errors.Any() ? string.Join("; ", errors.Take(10)) : null,
                ValidationErrors = errors.Any() ? errors : null
            };
        }

        // Import DTOs
        private class TransactionImportRow
        {
            public int RowNumber { get; set; }
            public string? Description { get; set; }
            public decimal Amount { get; set; }
            public DateTime? Date { get; set; }
            public string? Type { get; set; }
            public int BudgetEntryId { get; set; }
        }

        private class BudgetEntryImportRow
        {
            public int RowNumber { get; set; }
            public string? AccountNumber { get; set; }
            public string? Description { get; set; }
            public decimal BudgetedAmount { get; set; }
            public decimal ActualAmount { get; set; }
            public int FiscalYear { get; set; }
            public int DepartmentId { get; set; }
            public string? FundType { get; set; }
        }

        // CsvHelper mappings
        private sealed class TransactionCsvMap : ClassMap<TransactionImportRow>
        {
            public TransactionCsvMap()
            {
                Map(m => m.Description).Name("Description", "Memo");
                Map(m => m.Amount).Name("Amount", "Value");
                Map(m => m.Date).Name("Date", "TransactionDate");
                Map(m => m.Type).Name("Type", "TransactionType");
                Map(m => m.BudgetEntryId).Name("BudgetEntryId", "BudgetId");
            }
        }

        private sealed class BudgetEntryCsvMap : ClassMap<BudgetEntryImportRow>
        {
            public BudgetEntryCsvMap()
            {
                Map(m => m.AccountNumber).Name("AccountNumber", "Account");
                Map(m => m.Description).Name("Description");
                Map(m => m.BudgetedAmount).Name("BudgetedAmount", "Budget");
                Map(m => m.ActualAmount).Name("ActualAmount", "Actual");
                Map(m => m.FiscalYear).Name("FiscalYear", "Year");
                Map(m => m.DepartmentId).Name("DepartmentId", "Department");
                Map(m => m.FundType).Name("FundType", "Fund");
            }
        }
    }
}
