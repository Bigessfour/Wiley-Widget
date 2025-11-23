using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WileyWidget.Models;
using ClosedXML.Excel;
using WileyWidget.Models.Entities;

namespace WileyWidget.Services.Excel;

/// <summary>
/// Service for reading Excel files and extracting municipal budget data
/// </summary>
public class ExcelReaderService : IExcelReaderService
{
    private readonly ILogger<ExcelReaderService> _logger;

    public ExcelReaderService(ILogger<ExcelReaderService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<BudgetEntry>> ReadBudgetDataAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("Excel file not found", filePath);

        try
        {
            // Parse Excel off the UI thread (I/O + CPU-bound)
            return await Task.Run(() =>
            {
                var budgetEntries = new List<BudgetEntry>();

                using (var workbook = new XLWorkbook(filePath))
                {
                    var worksheet = workbook.Worksheet(1); // Assume first worksheet

                    // Find header row
                    int headerRow = FindHeaderRow(worksheet);
                    if (headerRow == -1)
                    {
                        throw new InvalidOperationException("Could not find header row with budget information");
                    }

                    // Map column indices
                    var columnMap = MapColumns(worksheet, headerRow);

                    // Read data rows
                    int row = headerRow + 1;
                    while (row <= worksheet.Rows().Count())
                    {
                        var accountNumber = GetCellValue(worksheet, row, columnMap["AccountNumber"]);
                        if (string.IsNullOrWhiteSpace(accountNumber))
                            break; // End of data

                        var budgetEntry = new BudgetEntry
                        {
                            AccountNumber = accountNumber,
                            Description = GetCellValue(worksheet, row, columnMap["Description"]) ?? $"Account {accountNumber}",
                            BudgetedAmount = ParseDecimal(GetCellValue(worksheet, row, columnMap["BudgetedAmount"])),
                            ActualAmount = ParseDecimal(GetCellValue(worksheet, row, columnMap["ActualAmount"])),
                            FiscalYear = ParseInt(GetCellValue(worksheet, row, columnMap["FiscalYear"])) ?? DateTime.Now.Year,
                            SourceFilePath = filePath,
                            SourceRowNumber = row
                        };

                        // Optional fields
                        if (columnMap.ContainsKey("FundType"))
                        {
                            var fundTypeStr = GetCellValue(worksheet, row, columnMap["FundType"]);
                            if (Enum.TryParse<FundType>(fundTypeStr, true, out var fundType))
                            {
                                budgetEntry.FundType = fundType;
                            }
                        }

                        if (columnMap.ContainsKey("DepartmentId"))
                        {
                            budgetEntry.DepartmentId = ParseInt(GetCellValue(worksheet, row, columnMap["DepartmentId"])) ?? 1;
                        }

                        if (columnMap.ContainsKey("StartPeriod"))
                        {
                            var startPeriodStr = GetCellValue(worksheet, row, columnMap["StartPeriod"]);
                            if (DateTime.TryParse(startPeriodStr, out var startPeriod))
                            {
                                budgetEntry.StartPeriod = startPeriod;
                            }
                        }

                        if (columnMap.ContainsKey("EndPeriod"))
                        {
                            var endPeriodStr = GetCellValue(worksheet, row, columnMap["EndPeriod"]);
                            if (DateTime.TryParse(endPeriodStr, out var endPeriod))
                            {
                                budgetEntry.EndPeriod = endPeriod;
                            }
                        }

                        budgetEntries.Add(budgetEntry);
                        row++;
                    }
                }

                return budgetEntries;
            });
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error reading budget data from Excel file: {filePath}", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> ValidateExcelStructureAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        if (!File.Exists(filePath))
            return false;

        try
        {
            // Validate structure off the UI thread; no UI mutations here
            return await Task.Run(() =>
            {
                using (var workbook = new XLWorkbook(filePath))
                {
                    var worksheet = workbook.Worksheet(1);

                    // Check if we can find required headers
                    int headerRow = FindHeaderRow(worksheet);
                    if (headerRow == -1)
                        return false;

                    var columnMap = MapColumns(worksheet, headerRow);

                    // Check for required columns
                    return columnMap.ContainsKey("AccountNumber") && columnMap.ContainsKey("Description");
                }
            });
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Find the header row by looking for common budget column names
    /// </summary>
    private int FindHeaderRow(IXLWorksheet worksheet)
    {
        for (int row = 1; row <= Math.Min(10, worksheet.Rows().Count()); row++)
        {
            for (int col = 1; col <= Math.Min(10, worksheet.Columns().Count()); col++)
            {
                var cellValue = GetCellValue(worksheet, row, col)?.ToLowerInvariant();
                if (cellValue != null && (cellValue.Contains("account", StringComparison.OrdinalIgnoreCase) || cellValue.Contains("number", StringComparison.OrdinalIgnoreCase) || cellValue.Contains("description", StringComparison.OrdinalIgnoreCase)))
                {
                    return row;
                }
            }
        }
        return -1;
    }

    /// <summary>
    /// Map column names to their indices
    /// </summary>
    private Dictionary<string, int> MapColumns(IXLWorksheet worksheet, int headerRow)
    {
        var columnMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int col = 1; col <= worksheet.Columns().Count(); col++)
        {
            var headerValue = GetCellValue(worksheet, headerRow, col)?.Trim();
            if (!string.IsNullOrWhiteSpace(headerValue))
            {
                // Map common variations to standard names
                var standardName = headerValue.ToLowerInvariant() switch
                {
                    var h when h.Contains("account", StringComparison.OrdinalIgnoreCase) && h.Contains("number", StringComparison.OrdinalIgnoreCase) => "AccountNumber",
                    var h when h.Contains("account", StringComparison.OrdinalIgnoreCase) => "AccountNumber",
                    var h when h.Contains("description", StringComparison.OrdinalIgnoreCase) => "Description",
                    var h when h.Contains("budget", StringComparison.OrdinalIgnoreCase) => "BudgetedAmount",
                    var h when h.Contains("actual", StringComparison.OrdinalIgnoreCase) => "ActualAmount",
                    var h when h.Contains("fiscal", StringComparison.OrdinalIgnoreCase) && h.Contains("year", StringComparison.OrdinalIgnoreCase) => "FiscalYear",
                    var h when h.Contains("fund", StringComparison.OrdinalIgnoreCase) && h.Contains("type", StringComparison.OrdinalIgnoreCase) => "FundType",
                    var h when h.Contains("department", StringComparison.OrdinalIgnoreCase) => "DepartmentId",
                    var h when h.Contains("start", StringComparison.OrdinalIgnoreCase) && h.Contains("period", StringComparison.OrdinalIgnoreCase) => "StartPeriod",
                    var h when h.Contains("end", StringComparison.OrdinalIgnoreCase) && h.Contains("period", StringComparison.OrdinalIgnoreCase) => "EndPeriod",
                    _ => headerValue
                };

                columnMap[standardName] = col;
            }
        }

        return columnMap;
    }

    /// <summary>
    /// Get cell value safely
    /// </summary>
    private string? GetCellValue(IXLWorksheet worksheet, int row, int col)
    {
        try
        {
            var cell = worksheet.Cell(row, col);
            try
            {
                // ClosedXML provides GetString() which safely returns the cell string
                var s = cell.GetString();
                return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
            }
            catch
            {
                // Fall back to raw ToString
                var v = cell.Value;
                var s2 = v.ToString();
                return string.IsNullOrWhiteSpace(s2) ? null : s2.Trim();
            }
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parse decimal value safely
    /// </summary>
    private decimal ParseDecimal(string? value)
    {
        if (decimal.TryParse(value, out var result))
            return result;
        return 0;
    }

    /// <summary>
    /// Parse integer value safely
    /// </summary>
    private int? ParseInt(string? value)
    {
        if (int.TryParse(value, out var result))
            return result;
        return null;
    }
}
