using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ClosedXML.Excel;
using WileyWidget.Models;

namespace WileyWidget.Services.Export
{
    /// <summary>
    /// Interface for Excel export functionality.
    /// </summary>
    public interface IExcelExportService
    {
        /// <summary>
        /// Exports budget entries to Excel format.
        /// </summary>
        Task<string> ExportBudgetEntriesAsync(IEnumerable<BudgetEntry> entries, string filePath);

        /// <summary>
        /// Exports municipal accounts to Excel format.
        /// </summary>
        Task<string> ExportMunicipalAccountsAsync(IEnumerable<MunicipalAccount> accounts, string filePath);

        /// <summary>
        /// Exports generic enterprise data to Excel format.
        /// </summary>
        Task<string> ExportEnterpriseDataAsync<T>(IEnumerable<T> data, string filePath) where T : class;

        /// <summary>
        /// Exports generic data to Excel with custom columns.
        /// </summary>
        Task<string> ExportGenericDataAsync<T>(IEnumerable<T> data, string filePath, string worksheetName, Dictionary<string, Func<T, object>> columns);
    }

    /// <summary>
    /// Service for exporting data to Excel using ClosedXML (open-source).
    /// Replaces Syncfusion.XlsIO with MIT-licensed ClosedXML library.
    /// </summary>
    public class ExcelExportService : IExcelExportService
    {
        private readonly ILogger<ExcelExportService> _logger;

        public ExcelExportService(ILogger<ExcelExportService> logger)
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
                        worksheet.Cell(row, 5).Style.DateFormat.Format = "MM/dd/yyyy";
                        worksheet.Cell(row, 6).Value = entry.FundType.ToString();
                        worksheet.Cell(row, 7).Value = entry.Department?.Name ?? "";
                    }

                    // Auto-fit columns
                    worksheet.Columns().AdjustToContents();

                    // Add filters
                    var dataRange = worksheet.Range(1, 1, entryList.Count + 1, headers.Length);
                    dataRange.SetAutoFilter();

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
                    var headers = new[] { "ID", "Account Number", "Name", "Type", "Balance", "Status", "Last Updated" };
                    for (int i = 0; i < headers.Length; i++)
                    {
                        var cell = worksheet.Cell(1, i + 1);
                        cell.Value = headers[i];
                        cell.Style.Font.Bold = true;
                        cell.Style.Fill.BackgroundColor = XLColor.FromArgb(0, 176, 80);
                        cell.Style.Font.FontColor = XLColor.White;
                    }

                    // Add data
                    var accountList = accounts.ToList();
                    for (int i = 0; i < accountList.Count; i++)
                    {
                        var account = accountList[i];
                        int row = i + 2;

                        worksheet.Cell(row, 1).Value = account.Id;
                        worksheet.Cell(row, 2).Value = account.AccountNumber?.Value ?? "";
                        worksheet.Cell(row, 3).Value = account.Name ?? "";
                        worksheet.Cell(row, 4).Value = account.Type.ToString();
                        worksheet.Cell(row, 5).Value = (double)account.Balance;
                        worksheet.Cell(row, 5).Style.NumberFormat.Format = "$#,##0.00";
                        worksheet.Cell(row, 6).Value = account.IsActive ? "Active" : "Inactive";
                        worksheet.Cell(row, 7).Value = DateTime.Now;
                        worksheet.Cell(row, 7).Style.DateFormat.Format = "MM/dd/yyyy HH:mm:ss";
                    }

                    // Auto-fit columns
                    worksheet.Columns().AdjustToContents();

                    // Add filters
                    var dataRange = worksheet.Range(1, 1, accountList.Count + 1, headers.Length);
                    dataRange.SetAutoFilter();

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
            // Use generic export with dynamic column mapping
            var columns = new Dictionary<string, Func<T, object>>
            {
                ["ID"] = item => item.GetType().GetProperty("Id")?.GetValue(item) ?? 0,
                ["Name"] = item => item.GetType().GetProperty("Name")?.GetValue(item)?.ToString() ?? "",
                ["Description"] = item => item.GetType().GetProperty("Description")?.GetValue(item)?.ToString() ?? "",
                ["Created"] = item => item.GetType().GetProperty("CreatedAt")?.GetValue(item) ?? DateTime.Now
            };

            return await ExportGenericDataAsync(data, filePath, "Enterprise Data", columns);
        }

        public async Task<string> ExportGenericDataAsync<T>(
            IEnumerable<T> data,
            string filePath,
            string worksheetName,
            Dictionary<string, Func<T, object>> columns)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var workbook = new XLWorkbook();
                    var worksheet = workbook.Worksheets.Add(worksheetName);

                    // Add headers
                    var columnNames = columns.Keys.ToArray();
                    for (int i = 0; i < columnNames.Length; i++)
                    {
                        var cell = worksheet.Cell(1, i + 1);
                        cell.Value = columnNames[i];
                        cell.Style.Font.Bold = true;
                        cell.Style.Fill.BackgroundColor = XLColor.FromArgb(68, 114, 196);
                        cell.Style.Font.FontColor = XLColor.White;
                    }

                    // Add data
                    var dataList = data.ToList();
                    for (int i = 0; i < dataList.Count; i++)
                    {
                        var item = dataList[i];
                        int row = i + 2;

                        for (int col = 0; col < columnNames.Length; col++)
                        {
                            var columnName = columnNames[col];
                            var value = columns[columnName](item);

                            var cell = worksheet.Cell(row, col + 1);

                            if (value is DateTime dateTime)
                            {
                                cell.Value = dateTime;
                                cell.Style.DateFormat.Format = "MM/dd/yyyy";
                            }
                            else if (value is decimal || value is double || value is float)
                            {
                                cell.Value = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                                cell.Style.NumberFormat.Format = "#,##0.00";
                            }
                            else if (value is int || value is long)
                            {
                                cell.Value = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                            }
                            else
                            {
                                cell.Value = value?.ToString() ?? "";
                            }
                        }
                    }

                    // Auto-fit columns
                    worksheet.Columns().AdjustToContents();

                    // Add filters
                    var dataRange = worksheet.Range(1, 1, dataList.Count + 1, columnNames.Length);
                    dataRange.SetAutoFilter();

                    // Save the workbook
                    workbook.SaveAs(filePath);

                    _logger.LogInformation("Exported {Count} records to {FilePath}", dataList.Count, filePath);
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
