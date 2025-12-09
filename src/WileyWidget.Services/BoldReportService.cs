using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
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
public sealed partial class BoldReportService : IBoldReportService
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
    /// Additional viewer configuration and print helpers.
    /// </summary>
    public async Task ConfigureViewerAsync(object reportViewer, Dictionary<string, object> options, CancellationToken cancellationToken = default)
    {
        if (reportViewer == null) throw new ArgumentNullException(nameof(reportViewer));
        if (options == null) return;

        await Task.Run(() =>
        {
            try
            {
                InvokeOnUI(reportViewer, () =>
                {
                    var t = reportViewer.GetType();
                    foreach (var kvp in options)
                    {
                        try
                        {
                            var prop = t.GetProperty(kvp.Key);
                            if (prop != null && prop.CanWrite)
                            {
                                try
                                {
                                    prop.SetValue(reportViewer, Convert.ChangeType(kvp.Value, prop.PropertyType));
                                }
                                catch
                                {
                                    // ignore conversion/set failures
                                }
                            }
                            else
                            {
                                // Try method-style setters like SetEnableCustomCode(true)
                                var method = t.GetMethod("Set" + kvp.Key, new[] { kvp.Value?.GetType() ?? typeof(object) });
                                method?.Invoke(reportViewer, new[] { kvp.Value });
                            }
                        }
                        catch
                        {
                            // best-effort
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ConfigureViewerAsync encountered an error while applying options");
            }
        }, cancellationToken);
    }

    public async Task PrintAsync(object reportViewer, CancellationToken cancellationToken = default)
    {
        if (reportViewer == null) throw new ArgumentNullException(nameof(reportViewer));

        await Task.Run(() =>
        {
            try
            {
                InvokeOnUI(reportViewer, () =>
                {
                    var t = reportViewer.GetType();
                    // Try common print methods
                    var printMethod = t.GetMethod("Print");
                    if (printMethod != null)
                    {
                        printMethod.Invoke(reportViewer, null);
                        return;
                    }

                    var showPrintDialog = t.GetMethod("ShowPrintDialog");
                    if (showPrintDialog != null)
                    {
                        showPrintDialog.Invoke(reportViewer, null);
                        return;
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PrintAsync failed to invoke print on the viewer");
                throw;
            }
        }, cancellationToken);
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

    /// <summary>
    /// Invoke the provided action on the UI thread for the supplied target if possible.
    /// This uses reflection to support WPF (`Dispatcher`) and WinForms (`InvokeRequired`/`Invoke`) hosts
    /// without taking a hard dependency on PresentationFramework or System.Windows.Forms.
    /// If no UI marshalling is available, the action is executed inline.
    /// </summary>
    private static void InvokeOnUI(object? target, Action action)
    {
        if (target is null)
        {
            action();
            return;
        }

        var t = target.GetType();

        // WPF Dispatcher pattern
        var dispatcherProp = t.GetProperty("Dispatcher");
        if (dispatcherProp != null)
        {
            var dispatcher = dispatcherProp.GetValue(target);
            if (dispatcher != null)
            {
                var invokeMethod = dispatcher.GetType().GetMethod("Invoke", new[] { typeof(Action) });
                if (invokeMethod != null)
                {
                    invokeMethod.Invoke(dispatcher, new object[] { action });
                    return;
                }

                var beginInvoke = dispatcher.GetType().GetMethod("BeginInvoke", new[] { typeof(Action) });
                if (beginInvoke != null)
                {
                    beginInvoke.Invoke(dispatcher, new object[] { action });
                    return;
                }
            }
        }

        // WinForms pattern: InvokeRequired / Invoke
        var invokeRequiredProp = t.GetProperty("InvokeRequired");
        if (invokeRequiredProp != null)
        {
            try
            {
                var invokeRequired = (bool)invokeRequiredProp.GetValue(target)!;
                if (invokeRequired)
                {
                    var invokeMethod = t.GetMethod("Invoke", new[] { typeof(Delegate), typeof(object[]) });
                    if (invokeMethod != null)
                    {
                        invokeMethod.Invoke(target, new object[] { action, null });
                        return;
                    }

                    var beginInvoke = t.GetMethod("BeginInvoke", new[] { typeof(Delegate), typeof(object[]) });
                    if (beginInvoke != null)
                    {
                        beginInvoke.Invoke(target, new object[] { action, null });
                        return;
                    }
                }
            }
            catch
            {
                // swallow reflection-related issues and fall back to direct invocation below
            }
        }

        // No UI marshalling available — run directly.
        action();
    }

    /// <inheritdoc />
    public async Task LoadReportAsync(object reportViewer, string reportPath, Dictionary<string, object>? dataSources = null, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reportViewer);
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);

        await Task.Run(() =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(0.0);
                _logger.LogInformation("Loading report from path: {ReportPath}", reportPath);

                // All access to the viewer must happen on its owning UI thread
                InvokeOnUI(reportViewer, () =>
                {
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
                                var dataSource = Activator.CreateInstance(_reportDataSourceType, kvp.Key, kvp.Value);
                                addMethod?.Invoke(dataSourcesList, new[] { dataSource });
                            }
                        }
                    }

                    // Refresh the report viewer to render
                    var refreshMethod = viewerType.GetMethod("RefreshReport");
                    progress?.Report(0.8);
                    cancellationToken.ThrowIfCancellationRequested();
                    refreshMethod?.Invoke(reportViewer, null);

                    progress?.Report(1.0);
                    _logger.LogInformation("Report loaded successfully: {ReportPath}", reportPath);
                });
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("LoadReportAsync was cancelled by token");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load report: {ReportPath}", reportPath);
                throw;
            }
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task ExportToPdfAsync(object reportViewer, string filePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reportViewer);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        await ExportToFormatAsync(reportViewer, filePath, "PDF", progress, cancellationToken);
    }

    /// <inheritdoc />
    public async Task ExportToExcelAsync(object reportViewer, string filePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reportViewer);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        await ExportToFormatAsync(reportViewer, filePath, "Excel", progress, cancellationToken);
    }

    /// <summary>
    /// Export report to specified format using ReportWriter.
    /// </summary>
    private async Task ExportToFormatAsync(object reportViewer, string filePath, string format, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(0.0);
                _logger.LogInformation("Exporting report to {Format}: {FilePath}", format, filePath);

                var viewerType = reportViewer.GetType();

                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(0.2);

                // Use the built-in Export method if available (must run on UI thread)
                var exportMethod = viewerType.GetMethod("Export", new[] { typeof(string), typeof(string) });
                if (exportMethod != null)
                {
                    InvokeOnUI(reportViewer, () => exportMethod.Invoke(reportViewer, new object[] { format, filePath }));
                    progress?.Report(1.0);
                    _logger.LogInformation("Report exported successfully to {Format}: {FilePath}", format, filePath);
                    return;
                }

                // Fallback: Use ReportWriter for export
                if (_reportWriterType != null && _writingFormatType != null)
                {
                    // Get report path and any viewer-side data from viewer on UI thread
                    string? reportPathFromViewer = null;
                    object? dataSourcesFromViewer = null;

                    InvokeOnUI(reportViewer, () =>
                    {
                        var reportPathProperty = viewerType.GetProperty("ReportPath");
                        reportPathFromViewer = reportPathProperty?.GetValue(reportViewer) as string;

                        var dataSourcesProperty = viewerType.GetProperty("DataSources");
                        dataSourcesFromViewer = dataSourcesProperty?.GetValue(reportViewer);
                    });

                    if (!string.IsNullOrEmpty(reportPathFromViewer))
                    {
                        // Create ReportWriter instance and perform export off the UI thread
                        using var fileStream = new FileStream(reportPathFromViewer, FileMode.Open, FileAccess.Read);
                        progress?.Report(0.5);
                        cancellationToken.ThrowIfCancellationRequested();

                        var writer = Activator.CreateInstance(_reportWriterType, fileStream);

                        if (writer != null)
                        {
                            // If possible, copy data sources to writer (best-effort)
                            var writerDataSourcesProperty = _reportWriterType.GetProperty("DataSources");
                            if (dataSourcesFromViewer != null && writerDataSourcesProperty != null)
                            {
                                try
                                {
                                    var writerDataSources = writerDataSourcesProperty.GetValue(writer);
                                    var addRangeMethod = writerDataSources?.GetType().GetMethod("AddRange");
                                    addRangeMethod?.Invoke(writerDataSources, new[] { dataSourcesFromViewer });
                                }
                                catch
                                {
                                    // ignore failures copying data sources across threads; export may still succeed
                                }
                            }

                            cancellationToken.ThrowIfCancellationRequested();
                            progress?.Report(0.9);

                            var writerFormat = Enum.Parse(_writingFormatType, format);
                            var saveMethod = _reportWriterType.GetMethod("Save", new[] { typeof(string), _writingFormatType });
                            saveMethod?.Invoke(writer, new[] { filePath, writerFormat });

                            progress?.Report(1.0);
                            (writer as IDisposable)?.Dispose();
                        }
                    }
                }

                _logger.LogInformation("Report exported successfully to {Format}: {FilePath}", format, filePath);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("ExportToFormatAsync was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export report to {Format}: {FilePath}", format, filePath);
                throw;
            }
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RefreshReportAsync(object reportViewer, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reportViewer);

        await Task.Run(() =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(0.0);
                _logger.LogDebug("Refreshing report viewer");

                progress?.Report(0.5);
                cancellationToken.ThrowIfCancellationRequested();

                var viewerType = reportViewer.GetType();
                var refreshMethod = viewerType.GetMethod("RefreshReport");
                InvokeOnUI(reportViewer, () => refreshMethod?.Invoke(reportViewer, null));

                _logger.LogDebug("Report viewer refreshed successfully");
                progress?.Report(1.0);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("RefreshReportAsync was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh report viewer");
                throw;
            }
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SetReportParametersAsync(object reportViewer, Dictionary<string, object> parameters, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reportViewer);
        ArgumentNullException.ThrowIfNull(parameters);

        await Task.Run(() =>
        {
            try
            {
                _logger.LogInformation("Setting {Count} report parameters", parameters.Count);

                // All parameter updates must happen on the viewer's UI thread
                InvokeOnUI(reportViewer, () =>
                {
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
                });

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
