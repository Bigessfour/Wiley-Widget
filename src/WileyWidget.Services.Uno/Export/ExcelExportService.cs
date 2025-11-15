using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Syncfusion.XlsIO;
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
    /// Service for exporting data to Excel using Syncfusion XlsIO.
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
                    using var excelEngine = new ExcelEngine();
                    var application = excelEngine.Excel;
                    application.DefaultVersion = ExcelVersion.Xlsx;

                    var workbook = application.Workbooks.Create(1);
                    var worksheet = workbook.Worksheets[0];
                    worksheet.Name = "Budget Entries";

                    // Add headers
                    var headers = new[] { "ID", "Account Code", "Description", "Amount", "Date", "Category", "Status" };
                    for (int i = 0; i < headers.Length; i++)
                    {
                        worksheet[1, i + 1].Text = headers[i];
                        worksheet[1, i + 1].CellStyle.Font.Bold = true;
                        worksheet[1, i + 1].CellStyle.Color = System.Drawing.Color.FromArgb(0, 112, 192);
                        worksheet[1, i + 1].CellStyle.Font.Color = ExcelKnownColors.White;
                    }

                    // Add data
                    var entryList = entries.ToList();
                    for (int i = 0; i < entryList.Count; i++)
                    {
                        var entry = entryList[i];
                        int row = i + 2;

                        worksheet[row, 1].Number = entry.Id;
                        worksheet[row, 2].Text = entry.AccountNumber ?? "";
                        worksheet[row, 3].Text = entry.Description ?? "";
                        worksheet[row, 4].Number = (double)entry.BudgetedAmount;
                        worksheet[row, 4].NumberFormat = "$#,##0.00";
                        worksheet[row, 5].DateTime = entry.StartPeriod;
                        worksheet[row, 5].NumberFormat = "mm/dd/yyyy";
                        worksheet[row, 6].Text = entry.FundType.ToString();
                        worksheet[row, 7].Text = entry.Department?.Name ?? "";
                    }

                    // Auto-fit columns
                    worksheet.UsedRange.AutofitColumns();

                    // Add filters
                    worksheet.AutoFilters.FilterRange = worksheet.UsedRange;

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
                    using var excelEngine = new ExcelEngine();
                    var application = excelEngine.Excel;
                    application.DefaultVersion = ExcelVersion.Xlsx;

                    var workbook = application.Workbooks.Create(1);
                    var worksheet = workbook.Worksheets[0];
                    worksheet.Name = "Municipal Accounts";

                    // Add headers
                    var headers = new[] { "ID", "Account Number", "Name", "Type", "Balance", "Status", "Last Updated" };
                    for (int i = 0; i < headers.Length; i++)
                    {
                        worksheet[1, i + 1].Text = headers[i];
                        worksheet[1, i + 1].CellStyle.Font.Bold = true;
                        worksheet[1, i + 1].CellStyle.Color = System.Drawing.Color.FromArgb(0, 176, 80);
                        worksheet[1, i + 1].CellStyle.Font.Color = ExcelKnownColors.White;
                    }

                    // Add data
                    var accountList = accounts.ToList();
                    for (int i = 0; i < accountList.Count; i++)
                    {
                        var account = accountList[i];
                        int row = i + 2;

                        worksheet[row, 1].Number = account.Id;
                        worksheet[row, 2].Text = account.AccountNumber?.Value ?? "";
                        worksheet[row, 3].Text = account.Name ?? "";
                        worksheet[row, 4].Text = account.Type.ToString();
                        worksheet[row, 5].Number = (double)account.Balance;
                        worksheet[row, 5].NumberFormat = "$#,##0.00";
                        worksheet[row, 6].Text = account.IsActive ? "Active" : "Inactive";
                        worksheet[row, 7].DateTime = DateTime.Now;
                        worksheet[row, 7].NumberFormat = "mm/dd/yyyy hh:mm:ss";
                    }

                    // Auto-fit columns
                    worksheet.UsedRange.AutofitColumns();

                    // Add filters
                    worksheet.AutoFilters.FilterRange = worksheet.UsedRange;

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
                    using var excelEngine = new ExcelEngine();
                    var application = excelEngine.Excel;
                    application.DefaultVersion = ExcelVersion.Xlsx;

                    var workbook = application.Workbooks.Create(1);
                    var worksheet = workbook.Worksheets[0];
                    worksheet.Name = worksheetName;

                    // Add headers
                    var columnNames = columns.Keys.ToArray();
                    for (int i = 0; i < columnNames.Length; i++)
                    {
                        worksheet[1, i + 1].Text = columnNames[i];
                        worksheet[1, i + 1].CellStyle.Font.Bold = true;
                        worksheet[1, i + 1].CellStyle.Color = System.Drawing.Color.FromArgb(68, 114, 196);
                        worksheet[1, i + 1].CellStyle.Font.Color = ExcelKnownColors.White;
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

                            if (value is DateTime dateTime)
                            {
                                worksheet[row, col + 1].DateTime = dateTime;
                                worksheet[row, col + 1].NumberFormat = "mm/dd/yyyy";
                            }
                            else if (value is decimal || value is double || value is float)
                            {
                                worksheet[row, col + 1].Number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                            }
                            else if (value is int || value is long)
                            {
                                worksheet[row, col + 1].Number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                            }
                            else
                            {
                                worksheet[row, col + 1].Text = value?.ToString() ?? "";
                            }
                        }
                    }

                    // Auto-fit columns
                    worksheet.UsedRange.AutofitColumns();

                    // Add filters
                    worksheet.AutoFilters.FilterRange = worksheet.UsedRange;

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
