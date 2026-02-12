#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FastReport;
using Microsoft.Extensions.Logging;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services
{
    /// <summary>
    /// Extension methods and helpers for using FastReport with budget forecasts.
    /// Provides convenience methods for loading budget-specific report templates and preparing data.
    /// </summary>
    public static class FastReportBudgetExtensions
    {
        /// <summary>
        /// Loads the Budget Forecast Summary report template and prepares it with forecast data.
        /// </summary>
        /// <param name="reportService">The FastReport service instance</param>
        /// <param name="report">The FastReport Report object</param>
        /// <param name="forecast">The budget forecast data</param>
        /// <param name="progress">Optional progress reporter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public static async Task LoadBudgetForecastSummaryAsync(
            this IReportService reportService,
            Report report,
            BudgetForecastResult forecast,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(reportService);
            ArgumentNullException.ThrowIfNull(report);
            ArgumentNullException.ThrowIfNull(forecast);

            var templatePath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Reports",
                "BudgetForecastSummary.frx");

            var dataSources = new Dictionary<string, object>
            {
                ["ForecastData"] = new[] { forecast }
            };

            await reportService.LoadReportAsync(
                report,
                templatePath,
                dataSources,
                progress,
                cancellationToken);
        }

        /// <summary>
        /// Loads the Budget Forecast Line Items report template and prepares it with line item data.
        /// </summary>
        /// <param name="reportService">The FastReport service instance</param>
        /// <param name="report">The FastReport Report object</param>
        /// <param name="forecast">The budget forecast data</param>
        /// <param name="progress">Optional progress reporter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public static async Task LoadBudgetForecastLineItemsAsync(
            this IReportService reportService,
            Report report,
            BudgetForecastResult forecast,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(reportService);
            ArgumentNullException.ThrowIfNull(report);
            ArgumentNullException.ThrowIfNull(forecast);

            var templatePath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Reports",
                "BudgetForecastLineItems.frx");

            var dataSources = new Dictionary<string, object>
            {
                ["LineItems"] = forecast.ProposedLineItems,
                ["ForecastHeader"] = new[] { new
                {
                    forecast.EnterpriseName,
                    forecast.CurrentFiscalYear,
                    forecast.ProposedFiscalYear
                }}
            };

            await reportService.LoadReportAsync(
                report,
                templatePath,
                dataSources,
                progress,
                cancellationToken);
        }

        /// <summary>
        /// Exports a budget forecast using FastReport for layout/preview and Syncfusion for high-quality exports.
        /// This hybrid approach leverages FastReport's superior report designer and Syncfusion's advanced export capabilities.
        /// </summary>
        /// <param name="excelService">The Excel export service (Syncfusion-based)</param>
        /// <param name="reportService">The FastReport service (for preview/design)</param>
        /// <param name="forecast">The budget forecast data</param>
        /// <param name="outputPath">Output file path (.xlsx)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Path to the exported file</returns>
        /// <remarks>
        /// This method uses Syncfusion XlsIO for export because:
        /// 1. FastReport Open Source lacks native Excel export
        /// 2. Syncfusion provides advanced Excel features (formulas, conditional formatting, pivot tables)
        /// 3. FastReport templates can still be used for preview/print via Report object
        /// 
        /// Use FastReport Report object with PreviewControl for interactive preview, then call this method for export.
        /// </remarks>
        public static async Task<string> ExportBudgetForecastHybridAsync(
            this Export.IExcelExportService excelService,
            IReportService reportService,
            BudgetForecastResult forecast,
            string outputPath,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(excelService);
            ArgumentNullException.ThrowIfNull(reportService);
            ArgumentNullException.ThrowIfNull(forecast);
            ArgumentNullException.ThrowIfNull(outputPath);

            // Use Syncfusion for high-quality Excel export
            return await excelService.ExportBudgetForecastAsync(forecast, outputPath, cancellationToken);
        }
    }
}
