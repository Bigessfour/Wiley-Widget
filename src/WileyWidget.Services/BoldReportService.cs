using System.Reflection;
using Microsoft.Extensions.Logging;

namespace WileyWidget.Services;

/// <summary>
/// Service for BoldReports integration, providing report loading, export, and parameter management.
/// Uses reflection to interact with BoldReports.WPF ReportViewer control without requiring
/// WPF references in the Services assembly.
/// 
/// BoldReports package: BoldReports.WPF v12.1.13
/// Key features: RDL/RDLC rendering, PDF/Excel/Word export, SSRS compatibility
/// </summary>
public sealed class BoldReportService : IBoldReportService
{
    private readonly ILogger<BoldReportService> _logger;

    // Cached reflection types for performance
    private static Type? _reportViewerType;
    private static Type? _reportWriterType;
    private static Type? _reportDataSourceType;
    private static Type? _writingFormatType;
    private static bool _typesInitialized;
    private static readonly object _typeLock = new();

    public BoldReportService(ILogger<BoldReportService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        InitializeTypes();
    }

    /// <summary>
    /// Initialize BoldReports types via reflection (one-time setup).
    /// </summary>
    private static void InitializeTypes()
    {
        if (_typesInitialized) return;

        lock (_typeLock)
        {
            if (_typesInitialized) return;

            try
            {
                // Load BoldReports.WPF assembly
                var assembly = Assembly.Load("BoldReports.WPF");

                _reportViewerType = assembly.GetType("BoldReports.UI.Xaml.ReportViewer");
                _reportWriterType = assembly.GetType("BoldReports.Writer.ReportWriter");
                _reportDataSourceType = assembly.GetType("BoldReports.ReportDataSource");
                _writingFormatType = assembly.GetType("BoldReports.Writer.WriterFormat");

                _typesInitialized = true;
            }
            catch
            {
                // Types not available - will fail gracefully at runtime
                _typesInitialized = true;
            }
        }
    }

    /// <inheritdoc />
    public async Task LoadReportAsync(object reportViewer, string reportPath, Dictionary<string, object>? dataSources = null)
    {
        ArgumentNullException.ThrowIfNull(reportViewer);
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);

        await Task.Run(() =>
        {
            try
            {
                _logger.LogInformation("Loading report from path: {ReportPath}", reportPath);

                var viewerType = reportViewer.GetType();

                // Set ReportPath property
                var reportPathProperty = viewerType.GetProperty("ReportPath");
                if (reportPathProperty != null)
                {
                    reportPathProperty.SetValue(reportViewer, reportPath);
                }

                // Set ProcessingMode to Local for RDLC files
                if (reportPath.EndsWith(".rdlc", StringComparison.OrdinalIgnoreCase))
                {
                    var processingModeProperty = viewerType.GetProperty("ProcessingMode");
                    if (processingModeProperty != null)
                    {
                        // ProcessingMode.Local = 0
                        var processingModeType = processingModeProperty.PropertyType;
                        var localValue = Enum.Parse(processingModeType, "Local");
                        processingModeProperty.SetValue(reportViewer, localValue);
                    }
                }

                // Add data sources if provided
                if (dataSources != null && _reportDataSourceType != null)
                {
                    var dataSourcesProperty = viewerType.GetProperty("DataSources");
                    if (dataSourcesProperty != null)
                    {
                        var dataSourcesList = dataSourcesProperty.GetValue(reportViewer);
                        var addMethod = dataSourcesList?.GetType().GetMethod("Add");

                        foreach (var kvp in dataSources)
                        {
                            // Create ReportDataSource(name, value)
                            var dataSource = Activator.CreateInstance(_reportDataSourceType, kvp.Key, kvp.Value);
                            addMethod?.Invoke(dataSourcesList, new[] { dataSource });
                        }
                    }
                }

                // Refresh the report viewer to render
                var refreshMethod = viewerType.GetMethod("RefreshReport");
                refreshMethod?.Invoke(reportViewer, null);

                _logger.LogInformation("Report loaded successfully: {ReportPath}", reportPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load report: {ReportPath}", reportPath);
                throw;
            }
        });
    }

    /// <inheritdoc />
    public async Task ExportToPdfAsync(object reportViewer, string filePath)
    {
        ArgumentNullException.ThrowIfNull(reportViewer);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        await ExportToFormatAsync(reportViewer, filePath, "PDF");
    }

    /// <inheritdoc />
    public async Task ExportToExcelAsync(object reportViewer, string filePath)
    {
        ArgumentNullException.ThrowIfNull(reportViewer);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        await ExportToFormatAsync(reportViewer, filePath, "Excel");
    }

    /// <summary>
    /// Export report to specified format using ReportWriter.
    /// </summary>
    private async Task ExportToFormatAsync(object reportViewer, string filePath, string format)
    {
        await Task.Run(() =>
        {
            try
            {
                _logger.LogInformation("Exporting report to {Format}: {FilePath}", format, filePath);

                var viewerType = reportViewer.GetType();

                // Use the built-in Export method if available
                var exportMethod = viewerType.GetMethod("Export", new[] { typeof(string), typeof(string) });
                if (exportMethod != null)
                {
                    exportMethod.Invoke(reportViewer, new object[] { format, filePath });
                    _logger.LogInformation("Report exported successfully to {Format}: {FilePath}", format, filePath);
                    return;
                }

                // Fallback: Use ReportWriter for export
                if (_reportWriterType != null && _writingFormatType != null)
                {
                    // Get report path from viewer
                    var reportPathProperty = viewerType.GetProperty("ReportPath");
                    var reportPath = reportPathProperty?.GetValue(reportViewer) as string;

                    if (!string.IsNullOrEmpty(reportPath))
                    {
                        // Create ReportWriter instance
                        using var fileStream = new FileStream(reportPath, FileMode.Open, FileAccess.Read);
                        var writer = Activator.CreateInstance(_reportWriterType, fileStream);

                        if (writer != null)
                        {
                            // Get DataSources from viewer and apply to writer
                            var dataSourcesProperty = viewerType.GetProperty("DataSources");
                            var dataSources = dataSourcesProperty?.GetValue(reportViewer);
                            var writerDataSourcesProperty = _reportWriterType.GetProperty("DataSources");
                            if (dataSources != null && writerDataSourcesProperty != null)
                            {
                                // Copy data sources to writer
                                var writerDataSources = writerDataSourcesProperty.GetValue(writer);
                                var addRangeMethod = writerDataSources?.GetType().GetMethod("AddRange");
                                addRangeMethod?.Invoke(writerDataSources, new[] { dataSources });
                            }

                            // Parse WriterFormat enum
                            var writerFormat = Enum.Parse(_writingFormatType, format);

                            // Call Save method
                            var saveMethod = _reportWriterType.GetMethod("Save", new[] { typeof(string), _writingFormatType });
                            saveMethod?.Invoke(writer, new[] { filePath, writerFormat });

                            // Dispose writer if disposable
                            (writer as IDisposable)?.Dispose();
                        }
                    }
                }

                _logger.LogInformation("Report exported successfully to {Format}: {FilePath}", format, filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export report to {Format}: {FilePath}", format, filePath);
                throw;
            }
        });
    }

    /// <inheritdoc />
    public async Task RefreshReportAsync(object reportViewer)
    {
        ArgumentNullException.ThrowIfNull(reportViewer);

        await Task.Run(() =>
        {
            try
            {
                _logger.LogDebug("Refreshing report viewer");

                var viewerType = reportViewer.GetType();
                var refreshMethod = viewerType.GetMethod("RefreshReport");
                refreshMethod?.Invoke(reportViewer, null);

                _logger.LogDebug("Report viewer refreshed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh report viewer");
                throw;
            }
        });
    }

    /// <inheritdoc />
    public async Task SetReportParametersAsync(object reportViewer, Dictionary<string, object> parameters)
    {
        ArgumentNullException.ThrowIfNull(reportViewer);
        ArgumentNullException.ThrowIfNull(parameters);

        await Task.Run(() =>
        {
            try
            {
                _logger.LogInformation("Setting {Count} report parameters", parameters.Count);

                var viewerType = reportViewer.GetType();

                // Get Parameters collection
                var parametersProperty = viewerType.GetProperty("Parameters");
                if (parametersProperty != null)
                {
                    var parametersList = parametersProperty.GetValue(reportViewer);
                    var clearMethod = parametersList?.GetType().GetMethod("Clear");
                    var addMethod = parametersList?.GetType().GetMethod("Add");

                    // Clear existing parameters
                    clearMethod?.Invoke(parametersList, null);

                    // Get ReportParameter type
                    var reportParameterType = viewerType.Assembly.GetType("BoldReports.ReportParameter");
                    if (reportParameterType != null)
                    {
                        foreach (var kvp in parameters)
                        {
                            // Create ReportParameter(name, values)
                            var values = new List<string> { kvp.Value?.ToString() ?? string.Empty };
                            var param = Activator.CreateInstance(reportParameterType, kvp.Key, values);
                            addMethod?.Invoke(parametersList, new[] { param });
                        }
                    }
                }

                _logger.LogInformation("Report parameters set successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set report parameters");
                throw;
            }
        });
    }
}
