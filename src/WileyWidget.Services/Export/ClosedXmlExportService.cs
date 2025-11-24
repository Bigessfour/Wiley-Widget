using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging;
using WileyWidget.Models;

namespace WileyWidget.Services.Export
{
    /// <summary>
    /// Service for exporting data to Excel using ClosedXML (MIT license).
    /// Replaces Syncfusion XlsIO with open-source alternative.
    /// </summary>
    public class ClosedXmlExportService : IExcelExportService
    {
        private readonly ILogger<ClosedXmlExportService> _logger;

        public ClosedXmlExportService(ILogger<ClosedXmlExportService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<string> ExportBudgetEntriesAsync(IEnumerable<BudgetEntry> entries, string filePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var workbook = new XLWorkbook();
                    var worksheet = workbook.Worksheets.Add("Budget Entries");

                    // Add headers
                    var headers = new[] { "ID", "Account Code", "Description", "Amount", "Date", "Category", "Status" };
                    for (int i = 0; i < headers.Length; i++)
                    {
                        var cell = worksheet.Cell(1, i + 1);
                        cell.Value = headers[i];
                        cell.Style.Font.Bold = true;
                        cell.Style.Fill.BackgroundColor = XLColor.FromArgb(0, 112, 192);
                        cell.Style.Font.FontColor = XLColor.White;
                    }

                    // Add data
                    var entryList = entries.ToList();
                    for (int i = 0; i < entryList.Count; i++)
                    {
                        var entry = entryList[i];
                        int row = i + 2;

                        worksheet.Cell(row, 1).Value = entry.Id;
                        worksheet.Cell(row, 2).Value = entry.AccountNumber ?? "";
                        worksheet.Cell(row, 3).Value = entry.Description ?? "";
                        worksheet.Cell(row, 4).Value = (double)entry.BudgetedAmount;
                        worksheet.Cell(row, 4).Style.NumberFormat.Format = "$#,##0.00";
                        worksheet.Cell(row, 5).Value = entry.StartPeriod;
                        worksheet.Cell(row, 5).Style.DateFormat.Format = "mm/dd/yyyy";
                        worksheet.Cell(row, 6).Value = entry.FundType.ToString();
                        worksheet.Cell(row, 7).Value = entry.Department?.Name ?? ""; 
                    }

                    // Auto-fit columns
                    worksheet.Columns().AdjustToContents();

                    // Add filters
                    var range = worksheet.Range(1, 1, entryList.Count + 1, headers.Length);
                    range.SetAutoFilter();

                    // Save the workbook
                    workbook.SaveAs(filePath);

                    _logger.LogInformation("Exported {Count} budget entries to {FilePath}", entryList.Count, filePath);
                    return filePath;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to export budget entries to Excel");
                    throw;
                }
            });
        }

        public async Task<string> ExportMunicipalAccountsAsync(IEnumerable<MunicipalAccount> accounts, string filePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var workbook = new XLWorkbook();
                    var worksheet = workbook.Worksheets.Add("Municipal Accounts");

                    // Add headers
                    var headers = new[] { "Account Number", "Description", "Type", "Balance", "Budget", "Variance" };
                    for (int i = 0; i < headers.Length; i++)
                    {
                        var cell = worksheet.Cell(1, i + 1);
                        cell.Value = headers[i];
                        cell.Style.Font.Bold = true;
                        cell.Style.Fill.BackgroundColor = XLColor.FromArgb(0, 112, 192);
                        cell.Style.Font.FontColor = XLColor.White;
                    }

                    // Add data
                    var accountList = accounts.ToList();
                    for (int i = 0; i < accountList.Count; i++)
                    {
                        var account = accountList[i];
                        int row = i + 2;

                        // AccountNumber may be an owned value object; prefer the string value if available
                        worksheet.Cell(row, 1).Value = account.AccountNumber?.ToString() ?? account.AccountNumber_Value ?? "";
                        // Use Name instead of Description (model uses Name)
                        worksheet.Cell(row, 2).Value = account.Name ?? "";
                        // Use TypeDescription for a human readable Type
                        worksheet.Cell(row, 3).Value = account.TypeDescription ?? account.Type.ToString();
                        worksheet.Cell(row, 4).Value = (double)account.Balance;
                        worksheet.Cell(row, 4).Style.NumberFormat.Format = "$#,##0.00";
                        worksheet.Cell(row, 5).Value = (double)account.BudgetAmount;
                        worksheet.Cell(row, 5).Style.NumberFormat.Format = "$#,##0.00";
                        
                        var variance = account.BudgetAmount - account.Balance;
                        worksheet.Cell(row, 6).Value = (double)variance;
                        worksheet.Cell(row, 6).Style.NumberFormat.Format = "$#,##0.00";
                        
                        // Color code the variance
                        if (variance < 0)
                        {
                            worksheet.Cell(row, 6).Style.Font.FontColor = XLColor.Red;
                        }
                        else if (variance > 0)
                        {
                            worksheet.Cell(row, 6).Style.Font.FontColor = XLColor.Green;
                        }
                    }

                    // Auto-fit columns
                    worksheet.Columns().AdjustToContents();

                    // Add filters
                    var range = worksheet.Range(1, 1, accountList.Count + 1, headers.Length);
                    range.SetAutoFilter();

                    // Save the workbook
                    workbook.SaveAs(filePath);

                    _logger.LogInformation("Exported {Count} municipal accounts to {FilePath}", accountList.Count, filePath);
                    return filePath;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to export municipal accounts to Excel");
                    throw;
                }
            });
        }

        public async Task<string> ExportEnterpriseDataAsync<T>(IEnumerable<T> data, string filePath) where T : class
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var workbook = new XLWorkbook();
                    var worksheet = workbook.Worksheets.Add("Data");

                    var dataList = data.ToList();
                    if (!dataList.Any())
                    {
                        _logger.LogWarning("No data to export");
                        return filePath;
                    }

                    // Get properties
                    var properties = typeof(T).GetProperties();

                    // Add headers
                    for (int i = 0; i < properties.Length; i++)
                    {
                        var cell = worksheet.Cell(1, i + 1);
                        cell.Value = properties[i].Name;
                        cell.Style.Font.Bold = true;
                        cell.Style.Fill.BackgroundColor = XLColor.FromArgb(0, 112, 192);
                        cell.Style.Font.FontColor = XLColor.White;
                    }

                    // Add data
                    for (int i = 0; i < dataList.Count; i++)
                    {
                        var item = dataList[i];
                        int row = i + 2;

                        for (int j = 0; j < properties.Length; j++)
                        {
                            var value = properties[j].GetValue(item);
                            var cell = worksheet.Cell(row, j + 1);
                            
                            if (value != null)
                            {
                                cell.Value = value.ToString();
                            }
                        }
                    }

                    // Auto-fit columns
                    worksheet.Columns().AdjustToContents();

                    // Add filters
                    var range = worksheet.Range(1, 1, dataList.Count + 1, properties.Length);
                    range.SetAutoFilter();

                    // Save the workbook
                    workbook.SaveAs(filePath);

                    _logger.LogInformation("Exported {Count} items to {FilePath}", dataList.Count, filePath);
                    return filePath;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to export enterprise data to Excel");
                    throw;
                }
            });
        }

        public async Task<string> ExportGenericDataAsync<T>(IEnumerable<T> data, string filePath, string worksheetName, Dictionary<string, Func<T, object>> columns)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var workbook = new XLWorkbook();
                    var worksheet = workbook.Worksheets.Add(worksheetName);

                    var dataList = data.ToList();
                    var columnList = columns.ToList();

                    // Add headers
                    for (int i = 0; i < columnList.Count; i++)
                    {
                        var cell = worksheet.Cell(1, i + 1);
                        cell.Value = columnList[i].Key;
                        cell.Style.Font.Bold = true;
                        cell.Style.Fill.BackgroundColor = XLColor.FromArgb(0, 112, 192);
                        cell.Style.Font.FontColor = XLColor.White;
                    }

                    // Add data
                    for (int i = 0; i < dataList.Count; i++)
                    {
                        var item = dataList[i];
                        int row = i + 2;

                        for (int j = 0; j < columnList.Count; j++)
                        {
                            var value = columnList[j].Value(item);
                            var cell = worksheet.Cell(row, j + 1);
                            
                            if (value != null)
                            {
                                // Handle different types
                                if (value is DateTime dateTime)
                                {
                                    cell.Value = dateTime;
                                    cell.Style.DateFormat.Format = "mm/dd/yyyy";
                                }
                                else if (value is decimal || value is double || value is float)
                                {
                                    cell.Value = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                                    cell.Style.NumberFormat.Format = "#,##0.00";
                                }
                                else if (value is int || value is long)
                                {
                                    cell.Value = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                                }
                                else
                                {
                                    cell.Value = value.ToString();
                                }
                            }
                        }
                    }

                    // Auto-fit columns
                    worksheet.Columns().AdjustToContents();

                    // Add filters
                    var range = worksheet.Range(1, 1, dataList.Count + 1, columnList.Count);
                    range.SetAutoFilter();

                    // Save the workbook
                    workbook.SaveAs(filePath);

                    _logger.LogInformation("Exported {Count} items to {FilePath} (worksheet: {WorksheetName})", dataList.Count, filePath, worksheetName);
                    return filePath;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to export generic data to Excel");
                    throw;
                }
            });
        }
    }
}
