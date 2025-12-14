using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Reporting.WinForms;
using BoldReports.WPF;
using Serilog;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services
{
    /// <summary>
    /// Service for interacting with Microsoft Reporting Services ReportViewer control.
    /// This allows the service layer to work with Microsoft Reporting Services without direct references.
    /// </summary>
    public class BoldReportService : IBoldReportService
    {
        private readonly ILogger<BoldReportService> _logger;

        public BoldReportService(ILogger<BoldReportService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task LoadReportAsync(
            object reportViewer,
            string reportPath,
            Dictionary<string, object>? dataSources = null,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (reportViewer == null) throw new ArgumentNullException(nameof(reportViewer));
            if (!(reportViewer is ReportViewer rv)) throw new ArgumentException("ReportViewer must be of type Microsoft.Reporting.WinForms.ReportViewer", nameof(reportViewer));
            if (string.IsNullOrWhiteSpace(reportPath)) throw new ArgumentNullException(nameof(reportPath));

            try
            {
                await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    progress?.Report(0.1);
                    rv.LocalReport.ReportPath = reportPath;

                    progress?.Report(0.35);
                    ApplyDataSources(rv, dataSources);

                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(0.7);
                    rv.RefreshReport();

                    progress?.Report(1.0);
                }, cancellationToken);

                _logger.LogInformation("Report loaded successfully: {ReportPath}", reportPath);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Report load canceled for {ReportPath}", reportPath);
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load report: {ReportPath}", reportPath);
                throw;
            }
        }

        public async Task ExportToPdfAsync(object reportViewer, string filePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            if (reportViewer == null) throw new ArgumentNullException(nameof(reportViewer));
            if (!(reportViewer is ReportViewer rv)) throw new ArgumentException("ReportViewer must be of type Microsoft.Reporting.WinForms.ReportViewer", nameof(reportViewer));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));

            try
            {
                await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(0.1);

                    // Export using ReportViewer's LocalReport.Render method
                    string mimeType, encoding, extension;
                    string[] streams;
                    Warning[] warnings;

                    byte[] pdfBytes = rv.LocalReport.Render(
                        "PDF", null, out mimeType, out encoding, out extension, out streams, out warnings);

                    System.IO.File.WriteAllBytes(filePath, pdfBytes);
                    progress?.Report(1.0);
                }, cancellationToken);

                _logger.LogInformation("Report exported to PDF: {FilePath}", filePath);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("PDF export canceled: {FilePath}", filePath);
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to export report to PDF: {FilePath}", filePath);
                throw;
            }
        }

        public async Task ExportToExcelAsync(object reportViewer, string filePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            if (reportViewer == null) throw new ArgumentNullException(nameof(reportViewer));
            if (!(reportViewer is ReportViewer rv)) throw new ArgumentException("ReportViewer must be of type Microsoft.Reporting.WinForms.ReportViewer", nameof(reportViewer));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));

            try
            {
                await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(0.1);

                    // Export using ReportViewer's LocalReport.Render method
                    string mimeType, encoding, extension;
                    string[] streams;
                    Warning[] warnings;

                    byte[] excelBytes = rv.LocalReport.Render(
                        "EXCELOPENXML", null, out mimeType, out encoding, out extension, out streams, out warnings);

                    System.IO.File.WriteAllBytes(filePath, excelBytes);
                    progress?.Report(1.0);
                }, cancellationToken);

                _logger.LogInformation("Report exported to Excel: {FilePath}", filePath);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Excel export canceled: {FilePath}", filePath);
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to export report to Excel: {FilePath}", filePath);
                throw;
            }
        }

        /// <summary>
        /// Refreshes the report data.
        /// </summary>
        public async Task RefreshReportAsync(object reportViewer, CancellationToken cancellationToken = default)
        {
            if (reportViewer == null) throw new ArgumentNullException(nameof(reportViewer));
            if (!(reportViewer is ReportViewer rv)) throw new ArgumentException("ReportViewer must be of type Microsoft.Reporting.WinForms.ReportViewer", nameof(reportViewer));

            try
            {
                await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    rv.RefreshReport();
                }, cancellationToken);

                _logger.LogDebug("Report refreshed");
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Refresh canceled");
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to refresh report");
                throw;
            }
        }

        /// <summary>
        /// Sets report parameters.
        /// </summary>
        public async Task SetReportParametersAsync(object reportViewer, Dictionary<string, object> parameters, CancellationToken cancellationToken = default)
        {
            if (reportViewer == null) throw new ArgumentNullException(nameof(reportViewer));
            if (!(reportViewer is ReportViewer rv)) throw new ArgumentException("ReportViewer must be of type Microsoft.Reporting.WinForms.ReportViewer", nameof(reportViewer));
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));

            try
            {
                await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var reportParameters = new List<ReportParameter>();
                    foreach (var kv in parameters)
                    {
                        reportParameters.Add(new ReportParameter(kv.Key, kv.Value?.ToString()));
                    }

                    rv.LocalReport.SetParameters(reportParameters);
                    rv.RefreshReport();
                }, cancellationToken);

                _logger.LogInformation("Report parameters set successfully");
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Setting report parameters canceled");
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to set report parameters");
                throw;
            }
        }

        /// <summary>
        /// Sends a print command to the viewer.
        /// </summary>
        public async Task PrintAsync(object reportViewer, CancellationToken cancellationToken = default)
        {
            if (reportViewer == null) throw new ArgumentNullException(nameof(reportViewer));
            if (!(reportViewer is ReportViewer rv)) throw new ArgumentException("ReportViewer must be of type Microsoft.Reporting.WinForms.ReportViewer", nameof(reportViewer));

            try
            {
                await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    rv.PrintDialog();
                }, cancellationToken);

                _logger.LogInformation("Print command dispatched to report viewer");
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Print canceled");
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to print report");
                throw;
            }
        }

        public async Task ConfigureViewerAsync(object reportViewer, Dictionary<string, object> options, CancellationToken cancellationToken = default)
        {
            if (reportViewer == null) throw new ArgumentNullException(nameof(reportViewer));
            if (!(reportViewer is ReportViewer rv)) throw new ArgumentException("ReportViewer must be of type Microsoft.Reporting.WinForms.ReportViewer", nameof(reportViewer));
            if (options == null) throw new ArgumentNullException(nameof(options));

            try
            {
                await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    foreach (var option in options)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        switch (option.Key)
                        {
                            case "ZoomPercent":
                                if (option.Value is int zoomPercent)
                                {
                                    rv.ZoomMode = ZoomMode.Percent;
                                    rv.ZoomPercent = zoomPercent;
                                }
                                break;
                            case "SearchText":
                                // ReportViewer doesn't have built-in search, this might need custom implementation
                                _logger.LogWarning("SearchText configuration not supported by ReportViewer");
                                break;
                            case "ShowParametersPanel":
                                // ReportViewer shows parameters automatically if they exist
                                _logger.LogWarning("ShowParametersPanel configuration not directly supported by ReportViewer");
                                break;
                            default:
                                _logger.LogWarning("Unknown configuration option: {Option}", option.Key);
                                break;
                        }
                    }
                }, cancellationToken);

                _logger.LogInformation("Applied {Count} viewer options", options.Count);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Configure viewer canceled");
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to configure ReportViewer");
                throw;
            }
        }

        private static void ApplyDataSources(dynamic reportViewer, Dictionary<string, object>? dataSources)
        {
            if (dataSources == null || dataSources.Count == 0) return;

            reportViewer.LocalReport.DataSources.Clear();
            foreach (var kv in dataSources)
            {
                reportViewer.LocalReport.DataSources.Add(new ReportDataSource(kv.Key, kv.Value));
            }
        }
    }
}
