using System.Threading;
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
        Task<string> ExportBudgetEntriesAsync(IEnumerable<BudgetEntry> entries, string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Exports municipal accounts to Excel format.
        /// </summary>
        Task<string> ExportMunicipalAccountsAsync(IEnumerable<MunicipalAccount> accounts, string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Exports generic enterprise data to Excel format.
        /// </summary>
        Task<string> ExportEnterpriseDataAsync<T>(IEnumerable<T> data, string filePath) where T : class;

        /// <summary>
        /// Exports generic data to Excel with custom columns.
        /// </summary>
        Task<string> ExportGenericDataAsync<T>(IEnumerable<T> data, string filePath, string worksheetName, Dictionary<string, Func<T, object>> columns);

        /// <summary>
        /// Exports a budget forecast result to Excel with multiple worksheets (Summary, Line Items, Historical Trends, Assumptions).
        /// </summary>
        Task<string> ExportBudgetForecastAsync(BudgetForecastResult forecast, string filePath, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Service for exporting data to Excel using Syncfusion.XlsIO.
    /// </summary>
    public class ExcelExportService : IExcelExportService
    {
        private readonly ILogger<ExcelExportService> _logger;

        public ExcelExportService(ILogger<ExcelExportService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<string> ExportBudgetEntriesAsync(IEnumerable<BudgetEntry> entries, string filePath, CancellationToken cancellationToken = default)
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
                        var cell = worksheet.Range[1, i + 1];
                        cell.Text = headers[i];
                        cell.CellStyle.Font.Bold = true;
                        cell.CellStyle.Color = System.Drawing.Color.SteelBlue;
                        cell.CellStyle.Font.Color = Syncfusion.XlsIO.ExcelKnownColors.White;
                    }

                    // Add data
                    var entryList = entries.ToList();
                    for (int i = 0; i < entryList.Count; i++)
                    {
                        var entry = entryList[i];
                        int row = i + 2;

                        worksheet.Range[row, 1].Number = entry.Id;
                        worksheet.Range[row, 2].Text = entry.AccountNumber ?? "";
                        worksheet.Range[row, 3].Text = entry.Description ?? "";
                        worksheet.Range[row, 4].Number = (double)entry.BudgetedAmount;
                        worksheet.Range[row, 4].NumberFormat = "\"$\"#,##0.00";
                        worksheet.Range[row, 5].DateTime = entry.StartPeriod;
                        worksheet.Range[row, 5].NumberFormat = "MM/dd/yyyy";
                        worksheet.Range[row, 6].Text = entry.FundType.ToString();
                        worksheet.Range[row, 7].Text = entry.Department?.Name ?? "";
                    }

                    // Auto-fit columns
                    worksheet.UsedRange.AutofitColumns();

                    // Add filters
                    worksheet.AutoFilters.FilterRange = worksheet.UsedRange;

                    // Save the workbook
                    using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                    {
                        workbook.SaveAs(stream);
                    }
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

        public async Task<string> ExportMunicipalAccountsAsync(IEnumerable<MunicipalAccount> accounts, string filePath, CancellationToken cancellationToken = default)
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
                        var cell = worksheet.Range[1, i + 1];
                        cell.Text = headers[i];
                        cell.CellStyle.Font.Bold = true;
                        cell.CellStyle.Color = System.Drawing.Color.SeaGreen;
                        cell.CellStyle.Font.Color = Syncfusion.XlsIO.ExcelKnownColors.White;
                    }

                    // Add data
                    var accountList = accounts.ToList();
                    for (int i = 0; i < accountList.Count; i++)
                    {
                        var account = accountList[i];
                        int row = i + 2;

                        worksheet.Range[row, 1].Number = account.Id;
                        worksheet.Range[row, 2].Text = account.AccountNumber?.Value ?? "";
                        worksheet.Range[row, 3].Text = account.Name ?? "";
                        worksheet.Range[row, 4].Text = account.Type.ToString();
                        worksheet.Range[row, 5].Number = (double)account.Balance;
                        worksheet.Range[row, 5].NumberFormat = "\"$\"#,##0.00";
                        worksheet.Range[row, 6].Text = account.IsActive ? "Active" : "Inactive";
                        worksheet.Range[row, 7].DateTime = DateTime.Now;
                        worksheet.Range[row, 7].NumberFormat = "MM/dd/yyyy HH:mm:ss";
                    }

                    // Auto-fit columns
                    worksheet.UsedRange.AutofitColumns();

                    // Add filters
                    worksheet.AutoFilters.FilterRange = worksheet.UsedRange;

                    // Save the workbook
                    using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                    {
                        workbook.SaveAs(stream);
                    }

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
                        var cell = worksheet.Range[1, i + 1];
                        cell.Text = columnNames[i];
                        cell.CellStyle.Font.Bold = true;
                        cell.CellStyle.Color = System.Drawing.Color.SteelBlue;
                        cell.CellStyle.Font.Color = Syncfusion.XlsIO.ExcelKnownColors.White;
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

                            var cell = worksheet.Range[row, col + 1];

                            if (value is DateTime dateTime)
                            {
                                cell.DateTime = dateTime;
                                cell.NumberFormat = "MM/dd/yyyy";
                            }
                            else if (value is decimal || value is double || value is float)
                            {
                                cell.Number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                                cell.NumberFormat = "#,##0.00";
                            }
                            else if (value is int || value is long)
                            {
                                cell.Number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                            }
                            else
                            {
                                cell.Text = value?.ToString() ?? "";
                            }
                        }
                    }

                    // Auto-fit columns
                    worksheet.UsedRange.AutofitColumns();

                    // Add filters
                    worksheet.AutoFilters.FilterRange = worksheet.UsedRange;

                    // Save the workbook
                    using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                    {
                        workbook.SaveAs(stream);
                    }

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

        public async Task<string> ExportBudgetForecastAsync(BudgetForecastResult forecast, string filePath, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var excelEngine = new ExcelEngine();
                    var application = excelEngine.Excel;
                    application.DefaultVersion = ExcelVersion.Xlsx;
                    var workbook = application.Workbooks.Create(4); // 4 worksheets

                    // ========== WORKSHEET 1: SUMMARY ==========
                    var summarySheet = workbook.Worksheets[0];
                    summarySheet.Name = "Summary";

                    // Title
                    summarySheet.Range["A1"].Text = "Budget Forecast Summary";
                    summarySheet.Range["A1"].CellStyle.Font.Bold = true;
                    summarySheet.Range["A1"].CellStyle.Font.Size = 16;
                    summarySheet.Range["A1"].CellStyle.Color = System.Drawing.Color.Navy;
                    summarySheet.Range["A1"].CellStyle.Font.Color = Syncfusion.XlsIO.ExcelKnownColors.White;

                    // Summary data
                    int row = 3;
                    summarySheet.Range[$"A{row}"].Text = "Enterprise:";
                    summarySheet.Range[$"B{row}"].Text = forecast.EnterpriseName;
                    summarySheet.Range[$"A{row}"].CellStyle.Font.Bold = true;
                    row++;

                    summarySheet.Range[$"A{row}"].Text = "Current Fiscal Year:";
                    summarySheet.Range[$"B{row}"].Number = forecast.CurrentFiscalYear;
                    summarySheet.Range[$"A{row}"].CellStyle.Font.Bold = true;
                    row++;

                    summarySheet.Range[$"A{row}"].Text = "Proposed Fiscal Year:";
                    summarySheet.Range[$"B{row}"].Number = forecast.ProposedFiscalYear;
                    summarySheet.Range[$"A{row}"].CellStyle.Font.Bold = true;
                    row++;

                    summarySheet.Range[$"A{row}"].Text = "Generated Date:";
                    summarySheet.Range[$"B{row}"].DateTime = forecast.GeneratedDate;
                    summarySheet.Range[$"B{row}"].NumberFormat = "MM/dd/yyyy HH:mm";
                    summarySheet.Range[$"A{row}"].CellStyle.Font.Bold = true;
                    row += 2;

                    summarySheet.Range[$"A{row}"].Text = "Total Current Budget:";
                    summarySheet.Range[$"B{row}"].Number = (double)forecast.TotalCurrentBudget;
                    summarySheet.Range[$"B{row}"].NumberFormat = "\"$\"#,##0.00";
                    summarySheet.Range[$"A{row}"].CellStyle.Font.Bold = true;
                    summarySheet.Range[$"B{row}"].CellStyle.Font.Bold = true;
                    row++;

                    summarySheet.Range[$"A{row}"].Text = "Total Proposed Budget:";
                    summarySheet.Range[$"B{row}"].Number = (double)forecast.TotalProposedBudget;
                    summarySheet.Range[$"B{row}"].NumberFormat = "\"$\"#,##0.00";
                    summarySheet.Range[$"A{row}"].CellStyle.Font.Bold = true;
                    summarySheet.Range[$"B{row}"].CellStyle.Font.Bold = true;
                    summarySheet.Range[$"B{row}"].CellStyle.Color = System.Drawing.Color.LightGreen;
                    row++;

                    summarySheet.Range[$"A{row}"].Text = "Total Increase:";
                    summarySheet.Range[$"B{row}"].Number = (double)forecast.TotalIncrease;
                    summarySheet.Range[$"B{row}"].NumberFormat = "\"$\"#,##0.00";
                    summarySheet.Range[$"A{row}"].CellStyle.Font.Bold = true;
                    summarySheet.Range[$"B{row}"].CellStyle.Font.Bold = true;
                    if (forecast.TotalIncrease > 0)
                        summarySheet.Range[$"B{row}"].CellStyle.Font.Color = Syncfusion.XlsIO.ExcelKnownColors.Red;
                    row++;

                    summarySheet.Range[$"A{row}"].Text = "Increase Percentage:";
                    summarySheet.Range[$"B{row}"].Number = (double)forecast.TotalIncreasePercent;
                    summarySheet.Range[$"B{row}"].NumberFormat = "0.00\"%\"";
                    summarySheet.Range[$"A{row}"].CellStyle.Font.Bold = true;
                    summarySheet.Range[$"B{row}"].CellStyle.Font.Bold = true;
                    row++;

                    summarySheet.Range[$"A{row}"].Text = "Inflation Rate:";
                    summarySheet.Range[$"B{row}"].Number = (double)forecast.InflationRate;
                    summarySheet.Range[$"B{row}"].NumberFormat = "0.00\"%\"";
                    summarySheet.Range[$"A{row}"].CellStyle.Font.Bold = true;
                    row += 2;

                    summarySheet.Range[$"A{row}"].Text = "Summary:";
                    summarySheet.Range[$"A{row}"].CellStyle.Font.Bold = true;
                    row++;
                    summarySheet.Range[$"A{row}:C{row}"].Merge();
                    summarySheet.Range[$"A{row}"].Text = forecast.Summary;
                    summarySheet.Range[$"A{row}"].WrapText = true;

                    summarySheet.UsedRange.AutofitColumns();

                    // ========== WORKSHEET 2: LINE ITEMS ==========
                    var lineItemsSheet = workbook.Worksheets[1];
                    lineItemsSheet.Name = "Line Items";

                    // Headers
                    var headers = new[] { "Category", "Description", "Current Budget", "Proposed Budget", "$ Change", "% Change", "Justification", "Goal-Driven" };
                    for (int i = 0; i < headers.Length; i++)
                    {
                        var cell = lineItemsSheet.Range[1, i + 1];
                        cell.Text = headers[i];
                        cell.CellStyle.Font.Bold = true;
                        cell.CellStyle.Color = System.Drawing.Color.SteelBlue;
                        cell.CellStyle.Font.Color = Syncfusion.XlsIO.ExcelKnownColors.White;
                    }

                    // Data rows
                    for (int i = 0; i < forecast.ProposedLineItems.Count; i++)
                    {
                        var item = forecast.ProposedLineItems[i];
                        int dataRow = i + 2;

                        lineItemsSheet.Range[dataRow, 1].Text = item.Category;
                        lineItemsSheet.Range[dataRow, 2].Text = item.Description;
                        lineItemsSheet.Range[dataRow, 3].Number = (double)item.CurrentAmount;
                        lineItemsSheet.Range[dataRow, 3].NumberFormat = "\"$\"#,##0.00";
                        lineItemsSheet.Range[dataRow, 4].Number = (double)item.ProposedAmount;
                        lineItemsSheet.Range[dataRow, 4].NumberFormat = "\"$\"#,##0.00";
                        lineItemsSheet.Range[dataRow, 5].Number = (double)item.Increase;
                        lineItemsSheet.Range[dataRow, 5].NumberFormat = "\"$\"#,##0.00";
                        lineItemsSheet.Range[dataRow, 6].Number = (double)item.IncreasePercent;
                        lineItemsSheet.Range[dataRow, 6].NumberFormat = "0.00\"%\"";
                        lineItemsSheet.Range[dataRow, 7].Text = item.Justification;
                        lineItemsSheet.Range[dataRow, 8].Text = item.IsGoalDriven ? "Yes" : "No";

                        // Conditional formatting for changes
                        if (item.Increase > 0)
                        {
                            lineItemsSheet.Range[dataRow, 5].CellStyle.Font.Color = Syncfusion.XlsIO.ExcelKnownColors.Red;
                            lineItemsSheet.Range[dataRow, 6].CellStyle.Font.Color = Syncfusion.XlsIO.ExcelKnownColors.Red;
                        }
                        else if (item.Increase < 0)
                        {
                            lineItemsSheet.Range[dataRow, 5].CellStyle.Font.Color = Syncfusion.XlsIO.ExcelKnownColors.Green;
                            lineItemsSheet.Range[dataRow, 6].CellStyle.Font.Color = Syncfusion.XlsIO.ExcelKnownColors.Green;
                        }

                        // Highlight goal-driven items
                        if (item.IsGoalDriven)
                        {
                            lineItemsSheet.Range[dataRow, 1, dataRow, 8].CellStyle.Color = System.Drawing.Color.LightYellow;
                        }
                    }

                    // Add totals row
                    int totalsRow = forecast.ProposedLineItems.Count + 2;
                    lineItemsSheet.Range[totalsRow, 1].Text = "TOTAL";
                    lineItemsSheet.Range[totalsRow, 1].CellStyle.Font.Bold = true;
                    lineItemsSheet.Range[totalsRow, 3].Formula = $"=SUM(C2:C{totalsRow - 1})";
                    lineItemsSheet.Range[totalsRow, 3].NumberFormat = "\"$\"#,##0.00";
                    lineItemsSheet.Range[totalsRow, 3].CellStyle.Font.Bold = true;
                    lineItemsSheet.Range[totalsRow, 4].Formula = $"=SUM(D2:D{totalsRow - 1})";
                    lineItemsSheet.Range[totalsRow, 4].NumberFormat = "\"$\"#,##0.00";
                    lineItemsSheet.Range[totalsRow, 4].CellStyle.Font.Bold = true;
                    lineItemsSheet.Range[totalsRow, 5].Formula = $"=SUM(E2:E{totalsRow - 1})";
                    lineItemsSheet.Range[totalsRow, 5].NumberFormat = "\"$\"#,##0.00";
                    lineItemsSheet.Range[totalsRow, 5].CellStyle.Font.Bold = true;

                    lineItemsSheet.UsedRange.AutofitColumns();
                    lineItemsSheet.AutoFilters.FilterRange = lineItemsSheet.Range[1, 1, totalsRow - 1, 8];
                    lineItemsSheet.Range["A1"].FreezePanes();

                    // ========== WORKSHEET 3: HISTORICAL TRENDS ==========
                    var historySheet = workbook.Worksheets[2];
                    historySheet.Name = "Historical Trends";

                    if (forecast.HistoricalTrends != null && forecast.HistoricalTrends.Any())
                    {
                        // Headers
                        var histHeaders = new[] { "Fiscal Year", "Total Budget", "YoY Change", "YoY %" };
                        for (int i = 0; i < histHeaders.Length; i++)
                        {
                            var cell = historySheet.Range[1, i + 1];
                            cell.Text = histHeaders[i];
                            cell.CellStyle.Font.Bold = true;
                            cell.CellStyle.Color = System.Drawing.Color.DarkGreen;
                            cell.CellStyle.Font.Color = Syncfusion.XlsIO.ExcelKnownColors.White;
                        }

                        // Data
                        for (int i = 0; i < forecast.HistoricalTrends.Count; i++)
                        {
                            var trend = forecast.HistoricalTrends[i];
                            int dataRow = i + 2;

                            historySheet.Range[dataRow, 1].Number = trend.FiscalYear;
                            historySheet.Range[dataRow, 2].Number = (double)trend.TotalBudget;
                            historySheet.Range[dataRow, 2].NumberFormat = "\"$\"#,##0.00";
                            historySheet.Range[dataRow, 3].Number = (double)trend.YearOverYearChange;
                            historySheet.Range[dataRow, 3].NumberFormat = "\"$\"#,##0.00";
                            historySheet.Range[dataRow, 4].Number = (double)trend.YearOverYearPercent;
                            historySheet.Range[dataRow, 4].NumberFormat = "0.00\"%\"";
                        }

                        historySheet.UsedRange.AutofitColumns();
                    }
                    else
                    {
                        historySheet.Range["A1"].Text = "No historical trend data available";
                    }

                    // ========== WORKSHEET 4: ASSUMPTIONS & METHODOLOGY ==========
                    var assumptionsSheet = workbook.Worksheets[3];
                    assumptionsSheet.Name = "Assumptions";

                    assumptionsSheet.Range["A1"].Text = "Assumptions & Methodology";
                    assumptionsSheet.Range["A1"].CellStyle.Font.Bold = true;
                    assumptionsSheet.Range["A1"].CellStyle.Font.Size = 14;
                    assumptionsSheet.Range["A1"].CellStyle.Color = System.Drawing.Color.DarkOrange;
                    assumptionsSheet.Range["A1"].CellStyle.Font.Color = Syncfusion.XlsIO.ExcelKnownColors.White;

                    int assumptionRow = 3;
                    assumptionsSheet.Range[$"A{assumptionRow}"].Text = "Assumptions:";
                    assumptionsSheet.Range[$"A{assumptionRow}"].CellStyle.Font.Bold = true;
                    assumptionRow++;

                    foreach (var assumption in forecast.Assumptions)
                    {
                        assumptionsSheet.Range[$"A{assumptionRow}"].Text = $"• {assumption}";
                        assumptionRow++;
                    }

                    assumptionRow += 2;
                    assumptionsSheet.Range[$"A{assumptionRow}"].Text = "Goals Considered:";
                    assumptionsSheet.Range[$"A{assumptionRow}"].CellStyle.Font.Bold = true;
                    assumptionRow++;

                    if (forecast.Goals.Any())
                    {
                        foreach (var goal in forecast.Goals)
                        {
                            assumptionsSheet.Range[$"A{assumptionRow}"].Text = $"• {goal}";
                            assumptionRow++;
                        }
                    }
                    else
                    {
                        assumptionsSheet.Range[$"A{assumptionRow}"].Text = "No specific goals specified";
                        assumptionRow++;
                    }

                    assumptionsSheet.Range["A:A"].ColumnWidth = 100;

                    // Save the workbook
                    using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                    {
                        workbook.SaveAs(stream);
                    }

                    _logger.LogInformation("Exported budget forecast for FY {FY} to {FilePath}", forecast.ProposedFiscalYear, filePath);
                    return filePath;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to export budget forecast to Excel");
                    throw;
                }
            }, cancellationToken);
        }
    }
}
