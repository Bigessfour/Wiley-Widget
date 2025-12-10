using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Serilog;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services
{
    /// <summary>
    /// Service for interacting with Bold Reports ReportViewer control via reflection.
    /// This allows the service layer to work with Bold Reports without direct references.
    /// </summary>
    public class BoldReportService : IBoldReportService
    {
        private readonly ILogger<BoldReportService> _logger;

        public BoldReportService(ILogger<BoldReportService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Loads a report into the viewer.
        /// </summary>
        public async Task LoadReportAsync(
            object reportViewer,
            string reportPath,
            Dictionary<string, object>? dataSources = null,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (reportViewer == null) throw new ArgumentNullException(nameof(reportViewer));
            if (string.IsNullOrWhiteSpace(reportPath)) throw new ArgumentNullException(nameof(reportPath));

            try
            {
                await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var viewerType = reportViewer.GetType();

                    progress?.Report(0.1);
                    if (!TrySetProperty(viewerType, reportViewer, "ReportPath", reportPath))
                        throw new InvalidOperationException("ReportViewer control does not expose ReportPath. Ensure Bold Reports package is referenced.");

                    progress?.Report(0.35);
                    ApplyDataSources(viewerType, reportViewer, dataSources);

                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(0.7);
                    RefreshViewer(viewerType, reportViewer);

                    progress?.Report(1.0);
                }, cancellationToken);

                _logger.LogInformation("Bold Reports loaded successfully: {ReportPath}", reportPath);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Bold Reports load canceled for {ReportPath}", reportPath);
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load Bold Report: {ReportPath}", reportPath);
                throw;
            }
        }

        /// <summary>
        /// Exports the current report to PDF.
        /// </summary>
        public async Task ExportToPdfAsync(object reportViewer, string filePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            if (reportViewer == null) throw new ArgumentNullException(nameof(reportViewer));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));

            try
            {
                await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var viewerType = reportViewer.GetType();
                    progress?.Report(0.1);

                    var exportMethod = viewerType.GetMethod("ExportToPdf", new[] { typeof(string) })
                                    ?? viewerType.GetMethod("ExportToPdf", BindingFlags.Public | BindingFlags.Instance);

                    if (exportMethod == null)
                        throw new InvalidOperationException("ReportViewer control does not have ExportToPdf method.");

                    exportMethod.Invoke(reportViewer, new object[] { filePath });
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

        /// <summary>
        /// Exports the current report to Excel.
        /// </summary>
        public async Task ExportToExcelAsync(object reportViewer, string filePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            if (reportViewer == null) throw new ArgumentNullException(nameof(reportViewer));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));

            try
            {
                await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var viewerType = reportViewer.GetType();
                    progress?.Report(0.1);

                    var exportMethod = viewerType.GetMethod("ExportToExcel", new[] { typeof(string) })
                                    ?? viewerType.GetMethod("ExportToExcel", BindingFlags.Public | BindingFlags.Instance);

                    if (exportMethod == null)
                        throw new InvalidOperationException("ReportViewer control does not have ExportToExcel method.");

                    exportMethod.Invoke(reportViewer, new object[] { filePath });
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

            try
            {
                await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var viewerType = reportViewer.GetType();
                    RefreshViewer(viewerType, reportViewer);
                }, cancellationToken);

                Log.Debug("Bold Report refreshed");
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Refresh canceled");
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to refresh Bold Report");
                throw;
            }
        }

        /// <summary>
        /// Sets report parameters.
        /// </summary>
        public async Task SetReportParametersAsync(object reportViewer, Dictionary<string, object> parameters, CancellationToken cancellationToken = default)
        {
            if (reportViewer == null) throw new ArgumentNullException(nameof(reportViewer));
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));

            try
            {
                await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var viewerType = reportViewer.GetType();
                    var parametersProp = viewerType.GetProperty("Parameters", BindingFlags.Public | BindingFlags.Instance);

                    if (parametersProp == null)
                        throw new InvalidOperationException("ReportViewer control does not have Parameters property.");

                    var currentParams = parametersProp.GetValue(reportViewer) as IDictionary;
                    if (currentParams == null)
                        throw new InvalidOperationException("ReportViewer.Parameters is not an IDictionary.");

                    currentParams.Clear();
                    foreach (var kv in parameters)
                        currentParams[kv.Key] = kv.Value;

                    RefreshViewer(viewerType, reportViewer);
                }, cancellationToken);

                Log.Information("Report parameters set successfully");
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Setting report parameters canceled");
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to set Bold Report parameters");
                throw;
            }
        }

        /// <summary>
        /// Sends a print command to the viewer.
        /// </summary>
        public async Task PrintAsync(object reportViewer, CancellationToken cancellationToken = default)
        {
            if (reportViewer == null) throw new ArgumentNullException(nameof(reportViewer));

            try
            {
                await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var viewerType = reportViewer.GetType();

                    if (TryInvoke(viewerType, reportViewer, "Print")) return;
                    if (TryInvoke(viewerType, reportViewer, "PrintReport")) return;

                    throw new InvalidOperationException("ReportViewer control does not expose Print/PrintReport method.");
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

        /// <summary>
        /// Applies optional viewer configuration (zoom, search text, toggles, etc.).
        /// </summary>
        public async Task ConfigureViewerAsync(object reportViewer, Dictionary<string, object> options, CancellationToken cancellationToken = default)
        {
            if (reportViewer == null) throw new ArgumentNullException(nameof(reportViewer));
            if (options == null) throw new ArgumentNullException(nameof(options));

            try
            {
                await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var viewerType = reportViewer.GetType();

                    foreach (var option in options)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        // First try to map directly to a property
                        if (TrySetProperty(viewerType, reportViewer, option.Key, option.Value))
                        {
                            continue;
                        }

                        // Try a SetOption(string, object) pattern if available
                        var setOption = viewerType.GetMethod("SetOption", BindingFlags.Public | BindingFlags.Instance);
                        if (setOption != null)
                        {
                            try
                            {
                                setOption.Invoke(reportViewer, new[] { option.Key, option.Value });
                                continue;
                            }
                            catch (TargetParameterCountException)
                            {
                                // fall through
                            }
                        }

                        // Last attempt: invoke a Configure method if it exists
                        if (TryInvoke(viewerType, reportViewer, "Configure", option.Value))
                        {
                            continue;
                        }

                        _logger.LogDebug("Viewer option '{OptionName}' could not be applied", option.Key);
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
                Log.Error(ex, "Failed to configure Bold Report viewer");
                throw;
            }
        }

        private static void RefreshViewer(Type viewerType, object reportViewer)
        {
            if (TryInvoke(viewerType, reportViewer, "RefreshReport")) return;
            if (TryInvoke(viewerType, reportViewer, "Refresh")) return;
            throw new InvalidOperationException("ReportViewer control does not expose RefreshReport or Refresh method.");
        }

        private static void ApplyDataSources(Type viewerType, object reportViewer, Dictionary<string, object>? dataSources)
        {
            if (dataSources == null || dataSources.Count == 0) return;

            var dataSourcesProp = viewerType.GetProperty("DataSources", BindingFlags.Public | BindingFlags.Instance);
            if (dataSourcesProp == null) return;

            var currentDataSources = dataSourcesProp.GetValue(reportViewer) as IDictionary;
            if (currentDataSources == null) return;

            currentDataSources.Clear();
            foreach (var kv in dataSources)
                currentDataSources[kv.Key] = kv.Value;
        }

        private static bool TrySetProperty(Type type, object instance, string propertyName, object value)
        {
            var prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (prop == null || !prop.CanWrite) return false;

            try
            {
                prop.SetValue(instance, value);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryInvoke(Type type, object instance, string methodName, params object[] parameters)
        {
            var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
            if (method == null) return false;

            try
            {
                method.Invoke(instance, parameters);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
