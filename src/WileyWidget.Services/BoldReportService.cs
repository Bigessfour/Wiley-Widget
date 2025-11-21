using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Serilog;

namespace WileyWidget.Services
{
    /// <summary>
    /// Implementation of Bold Reports integration service.
    /// Uses reflection so the project doesn't hard-depend on Bold Reports assemblies.
    /// </summary>
    public class BoldReportService : IBoldReportService
    {
        private readonly ILogger<BoldReportService> _logger;

        public BoldReportService(ILogger<BoldReportService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Loads an RDL report into the report viewer.
        /// </summary>
        public async Task LoadReportAsync(object reportViewer, string reportPath, Dictionary<string, object>? dataSources = null)
        {
            if (reportViewer == null) throw new ArgumentNullException(nameof(reportViewer));
            if (string.IsNullOrWhiteSpace(reportPath)) throw new ArgumentNullException(nameof(reportPath));
            if (!File.Exists(reportPath)) throw new FileNotFoundException($"Report file not found: {reportPath}", reportPath);

            try
            {
                await Task.Run(() =>
                {
                    var viewerType = reportViewer.GetType();

                    var reportPathProp = viewerType.GetProperty("ReportPath", BindingFlags.Public | BindingFlags.Instance);
                    var setDataSourceMethod = viewerType.GetMethod("SetDataSource", BindingFlags.Public | BindingFlags.Instance);
                    var refreshMethod = viewerType.GetMethod("RefreshReport", BindingFlags.Public | BindingFlags.Instance)
                                        ?? viewerType.GetMethod("Refresh", BindingFlags.Public | BindingFlags.Instance);

                    if (reportPathProp == null)
                        throw new InvalidOperationException("ReportViewer control does not have a ReportPath property. Ensure Bold Reports is available.");

                    reportPathProp.SetValue(reportViewer, reportPath);

                    if (dataSources != null && dataSources.Count > 0 && setDataSourceMethod != null)
                    {
                        foreach (var ds in dataSources)
                        {
                            // Assume signature SetDataSource(string name, object data)
                            try { setDataSourceMethod.Invoke(reportViewer, new object[] { ds.Key, ds.Value }); }
                            catch (TargetParameterCountException)
                            {
                                // Try single-argument overload
                                setDataSourceMethod.Invoke(reportViewer, new object[] { ds.Value });
                            }
                        }
                    }

                    if (refreshMethod != null)
                        refreshMethod.Invoke(reportViewer, null);
                });

                Log.Information("Bold Reports loaded successfully: {ReportPath}", reportPath);
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
        public async Task ExportToPdfAsync(object reportViewer, string filePath)
        {
            if (reportViewer == null) throw new ArgumentNullException(nameof(reportViewer));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));

            try
            {
                await Task.Run(() =>
                {
                    var viewerType = reportViewer.GetType();
                    var exportMethod = viewerType.GetMethod("ExportToPdf", new[] { typeof(string) })
                                    ?? viewerType.GetMethod("ExportToPdf", BindingFlags.Public | BindingFlags.Instance);

                    if (exportMethod == null)
                        throw new InvalidOperationException("ReportViewer control does not have ExportToPdf method.");

                    exportMethod.Invoke(reportViewer, new object[] { filePath });
                });

                Log.Information("Report exported to PDF: {FilePath}", filePath);
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
        public async Task ExportToExcelAsync(object reportViewer, string filePath)
        {
            if (reportViewer == null) throw new ArgumentNullException(nameof(reportViewer));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));

            try
            {
                await Task.Run(() =>
                {
                    var viewerType = reportViewer.GetType();
                    var exportMethod = viewerType.GetMethod("ExportToExcel", new[] { typeof(string) })
                                    ?? viewerType.GetMethod("ExportToExcel", BindingFlags.Public | BindingFlags.Instance);

                    if (exportMethod == null)
                        throw new InvalidOperationException("ReportViewer control does not have ExportToExcel method.");

                    exportMethod.Invoke(reportViewer, new object[] { filePath });
                });

                Log.Information("Report exported to Excel: {FilePath}", filePath);
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
        public async Task RefreshReportAsync(object reportViewer)
        {
            if (reportViewer == null) throw new ArgumentNullException(nameof(reportViewer));

            try
            {
                await Task.Run(() =>
                {
                    var viewerType = reportViewer.GetType();
                    var refreshMethod = viewerType.GetMethod("RefreshReport", BindingFlags.Public | BindingFlags.Instance)
                                        ?? viewerType.GetMethod("Refresh", BindingFlags.Public | BindingFlags.Instance);

                    if (refreshMethod == null)
                        throw new InvalidOperationException("ReportViewer control does not have RefreshReport method.");

                    refreshMethod.Invoke(reportViewer, null);
                });

                Log.Debug("Bold Report refreshed");
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
        public async Task SetReportParametersAsync(object reportViewer, Dictionary<string, object> parameters)
        {
            if (reportViewer == null) throw new ArgumentNullException(nameof(reportViewer));
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));

            try
            {
                await Task.Run(() =>
                {
                    var viewerType = reportViewer.GetType();
                    var parametersProp = viewerType.GetProperty("Parameters", BindingFlags.Public | BindingFlags.Instance);
                    var refreshMethod = viewerType.GetMethod("RefreshReport", BindingFlags.Public | BindingFlags.Instance)
                                        ?? viewerType.GetMethod("Refresh", BindingFlags.Public | BindingFlags.Instance);

                    if (parametersProp == null)
                        throw new InvalidOperationException("ReportViewer control does not have Parameters property.");

                    var currentParams = parametersProp.GetValue(reportViewer) as IDictionary;
                    if (currentParams == null)
                        throw new InvalidOperationException("ReportViewer.Parameters is not an IDictionary.");

                    currentParams.Clear();
                    foreach (var kv in parameters)
                        currentParams[kv.Key] = kv.Value;

                    if (refreshMethod != null)
                        refreshMethod.Invoke(reportViewer, null);
                });

                Log.Debug("Bold Report parameters set: {ParameterCount}", parameters.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to set Bold Report parameters");
                throw;
            }
        }
    }
}
