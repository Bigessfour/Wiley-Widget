using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FastReport;
using FastReport.Export;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services
{
    /// <summary>
    /// Service for interacting with FastReport Open Source WinForms ReportViewer control.
    /// Provides full programmatic control over report generation, export, and viewer operations.
    /// All methods must be called on the UI thread.
    /// </summary>
    public class FastReportService : IReportService
    {
        private readonly ILogger<FastReportService> _logger;

        public FastReportService(ILogger<FastReportService> logger)
        {
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
        }

        /// <summary>
        /// Loads a report asynchronously. Must be called on UI thread.
        /// </summary>
        /// <param name="reportViewer">FastReport Report object</param>
        /// <param name="reportPath">Path to .frx report file</param>
        /// <param name="dataSources">Optional dictionary of data source names and data objects</param>
        /// <param name="progress">Optional progress reporter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task LoadReportAsync(
            object reportViewer,
            string reportPath,
            Dictionary<string, object>? dataSources = null,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(reportViewer);
            ArgumentNullException.ThrowIfNull(reportPath);
            if (string.IsNullOrWhiteSpace(reportPath))
            {
                throw new ArgumentException("Report path cannot be empty or whitespace.", nameof(reportPath));
            }

            if (reportViewer is not Report report)
            {
                throw new ArgumentException("ReportViewer must be of type FastReport.Report", nameof(reportViewer));
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(0.1);

                // Load the report template
                await Task.Run(() => report.Load(reportPath), cancellationToken);

                progress?.Report(0.3);

                // Register data sources
                // Note: FastReport Open Source RegisterData requires DataSet
                if (dataSources != null)
                {
                    foreach (var kvp in dataSources)
                    {
                        // RegisterData in FastReport.OpenSource accepts object data and string name
                        if (kvp.Value is System.Data.DataSet ds)
                        {
                            report.RegisterData(ds, kvp.Key);
                        }
                        else
                        {
                            _logger.LogWarning("Data source {Name} is not a DataSet - skipping registration. FastReport Open Source requires DataSet objects.", kvp.Key);
                        }
                    }
                }

                progress?.Report(0.6);
                cancellationToken.ThrowIfCancellationRequested();

                // Prepare the report
                report.Prepare();

                progress?.Report(1.0);
                _logger.LogInformation("Report loaded successfully: {ReportPath}", reportPath);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Report load canceled for {ReportPath}", reportPath);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load report: {ReportPath}", reportPath);
                throw;
            }
        }

        /// <summary>
        /// Refreshes the report data. Must be called on UI thread.
        /// </summary>
        /// <param name="reportViewer">FastReport Report object</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task RefreshReportAsync(object reportViewer, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(reportViewer);

            if (reportViewer is not Report report)
            {
                throw new ArgumentException("ReportViewer must be of type FastReport.Report", nameof(reportViewer));
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();

                report.Prepare();

                _logger.LogDebug("Report refreshed");
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Refresh canceled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh report");
                throw;
            }
        }

        /// <summary>
        /// Exports the current report to PDF. Must be called on UI thread.
        /// </summary>
        /// <param name="reportViewer">FastReport Report object</param>
        /// <param name="filePath">Output PDF file path</param>
        /// <param name="progress">Optional progress reporter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public Task ExportToPdfAsync(
            object reportViewer,
            string filePath,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(reportViewer);
            ArgumentNullException.ThrowIfNull(filePath);

            if (reportViewer is not Report report)
            {
                throw new ArgumentException("ReportViewer must be of type FastReport.Report", nameof(reportViewer));
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(0.1);

                // Ensure directory exists
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                progress?.Report(0.3);

                // FastReport Open Source doesn't include export functionality
                throw new NotSupportedException("PDF export is not available in FastReport Open Source. Consider upgrading to FastReport.Net or use Syncfusion for exports.");
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("PDF export canceled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export report to PDF: {FilePath}", filePath);
                throw;
            }
        }

        /// <summary>
        /// Exports the current report to Excel. Must be called on UI thread.
        /// </summary>
        /// <param name="reportViewer">FastReport Report object</param>
        /// <param name="filePath">Output Excel file path</param>
        /// <param name="progress">Optional progress reporter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public Task ExportToExcelAsync(
            object reportViewer,
            string filePath,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(reportViewer);
            ArgumentNullException.ThrowIfNull(filePath);

            if (reportViewer is not Report report)
            {
                throw new ArgumentException("ReportViewer must be of type FastReport.Report", nameof(reportViewer));
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(0.1);

                // Ensure directory exists
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                progress?.Report(0.3);

                // FastReport Open Source doesn't include export functionality
                throw new NotSupportedException("Excel export is not available in FastReport Open Source. Consider upgrading to FastReport.Net or use Syncfusion for exports.");
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Excel export canceled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export report to Excel: {FilePath}", filePath);
                throw;
            }
        }

        /// <summary>
        /// Sets report parameters. Must be called on UI thread.
        /// </summary>
        /// <param name="reportViewer">FastReport Report object</param>
        /// <param name="parameters">Dictionary of parameter names and values</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task SetReportParametersAsync(
            object reportViewer,
            Dictionary<string, object> parameters,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(reportViewer);
            ArgumentNullException.ThrowIfNull(parameters);

            if (reportViewer is not Report report)
            {
                throw new ArgumentException("ReportViewer must be of type FastReport.Report", nameof(reportViewer));
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();

                foreach (var kvp in parameters)
                {
                    report.SetParameterValue(kvp.Key, kvp.Value);
                }

                // Re-prepare the report with new parameters
                report.Prepare();

                _logger.LogDebug("Report parameters set: {Count} parameters", parameters.Count);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Set parameters canceled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set report parameters");
                throw;
            }
        }

        /// <summary>
        /// Prints the current report. Must be called on UI thread.
        /// </summary>
        /// <param name="reportViewer">FastReport Report object</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task PrintAsync(object reportViewer, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(reportViewer);

            if (reportViewer is not Report report)
            {
                throw new ArgumentException("ReportViewer must be of type FastReport.Report", nameof(reportViewer));
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();

                // FastReport Open Source doesn't support direct printing
                // Export to PDF and let the user print from there
                throw new NotSupportedException("Direct printing is not supported in FastReport Open Source. Export to PDF and print from there.");
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Print canceled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to print report");
                throw;
            }
        }

    }
}
