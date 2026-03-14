using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using FastReport;
using FastReport.Export;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services
{
    /// <summary>
    /// Service for interacting with FastReport Open Source report objects and prepared reports.
    /// Provides programmatic control over report generation, parameter application, export handoff, and printing.
    /// All methods must be called on the UI thread.
    /// </summary>
    public class FastReportService : IReportService
    {
        private readonly ILogger<FastReportService> _logger;

        private enum ReportLoadStage
        {
            ResolveTemplatePath,
            LoadTemplate,
            RegisterDataSources,
            PrepareReport,
        }

        public FastReportService(ILogger<FastReportService> logger)
        {
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
        }

        private static string DescribeDataSources(IReadOnlyDictionary<string, object>? dataSources)
        {
            if (dataSources == null || dataSources.Count == 0)
            {
                return "none";
            }

            return string.Join(", ",
                dataSources.Select(kvp => $"{kvp.Key}:{kvp.Value?.GetType().Name ?? "null"}"));
        }

        private static InvalidOperationException CreateStageFailure(
            ReportLoadStage stage,
            string reportPath,
            Exception innerException,
            string? detail = null)
        {
            var pathDisplay = Path.GetFileName(reportPath);
            var message = string.IsNullOrWhiteSpace(detail)
                ? $"FastReport load failed during {stage} for '{pathDisplay}'."
                : $"FastReport load failed during {stage} for '{pathDisplay}': {detail}";

            return new InvalidOperationException(message, innerException);
        }

        private static bool NormalizeLegacyContainerNodes(XContainer root)
        {
            var modified = false;
            modified |= UnwrapContainerNodes(root, "Parameters");
            modified |= UnwrapContainerNodes(root, "TableDataSources");
            modified |= UnwrapContainerNodes(root, "Columns");
            return modified;
        }

        private static Stream OpenTemplateStream(string reportPath, ILogger logger)
        {
            var document = XDocument.Load(reportPath, LoadOptions.PreserveWhitespace);
            var normalized = NormalizeLegacyContainerNodes(document);

            if (!normalized)
            {
                return File.OpenRead(reportPath);
            }

            logger.LogWarning(
                "[FASTREPORT] Normalized legacy wrapper nodes in template before load: {ReportPath}",
                reportPath);

            var stream = new MemoryStream();
            document.Save(stream);
            stream.Position = 0;
            return stream;
        }

        private static bool UnwrapContainerNodes(XContainer root, string containerName)
        {
            var wrappers = root
                .Descendants()
                .Where(element => string.Equals(element.Name.LocalName, containerName, StringComparison.Ordinal))
                .ToList();

            if (wrappers.Count == 0)
            {
                return false;
            }

            foreach (var wrapper in wrappers)
            {
                wrapper.ReplaceWith(wrapper.Nodes());
            }

            return true;
        }

        private static DataSet ConvertEnumerableToDataSet(IEnumerable items, string dataSetName)
        {
            var table = new DataTable(dataSetName);

            // Use enumerator to inspect first item and then iterate
            var enumerator = items.GetEnumerator();
            if (!enumerator.MoveNext())
            {
                // empty sequence -> single column placeholder
                table.Columns.Add("Value", typeof(object));
                var emptyDs = new DataSet(dataSetName);
                table.TableName = dataSetName;
                emptyDs.Tables.Add(table);
                return emptyDs;
            }

            var first = enumerator.Current;

            if (first == null)
            {
                table.Columns.Add("Value", typeof(object));
                table.Rows.Add(DBNull.Value);
            }
            else
            {
                var itemType = first.GetType();

                bool isPrimitiveLike = itemType.IsPrimitive || itemType == typeof(string) || itemType == typeof(decimal) || itemType == typeof(DateTime) || itemType.IsEnum || itemType == typeof(Guid);

                if (isPrimitiveLike)
                {
                    table.Columns.Add("Value", Nullable.GetUnderlyingType(itemType) ?? itemType);
                    table.Rows.Add(first);
                }
                else
                {
                    var props = itemType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    foreach (var p in props)
                    {
                        var colType = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
                        table.Columns.Add(p.Name, colType);
                    }

                    var values = props.Select(p => p.GetValue(first) ?? DBNull.Value).ToArray();
                    table.Rows.Add(values);
                }
            }

            // Add remaining rows
            while (enumerator.MoveNext())
            {
                var cur = enumerator.Current;
                if (cur == null)
                {
                    var vals = new object[table.Columns.Count];
                    for (int i = 0; i < vals.Length; i++) vals[i] = DBNull.Value;
                    table.Rows.Add(vals);
                    continue;
                }

                var curType = cur.GetType();
                bool curIsPrimitiveLike = curType.IsPrimitive || curType == typeof(string) || curType == typeof(decimal) || curType == typeof(DateTime) || curType.IsEnum || curType == typeof(Guid);

                if (curIsPrimitiveLike)
                {
                    table.Rows.Add(cur);
                }
                else
                {
                    var props = curType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    var vals = props.Select(p => p.GetValue(cur) ?? DBNull.Value).ToArray();
                    table.Rows.Add(vals);
                }
            }

            table.TableName = dataSetName;
            var ds = new DataSet(dataSetName);
            ds.Tables.Add(table);
            return ds;
        }

        private static void EnableDataSourceIfPresent(Report report, string? dataSourceName)
        {
            if (string.IsNullOrWhiteSpace(dataSourceName))
            {
                return;
            }

            var dataSource = report.GetDataSource(dataSourceName);
            if (dataSource != null)
            {
                dataSource.Enabled = true;
            }
        }

        private static void EnableRegisteredDataSources(Report report, string registrationName, object value)
        {
            EnableDataSourceIfPresent(report, registrationName);

            if (value is DataSet dataSet)
            {
                foreach (DataTable table in dataSet.Tables)
                {
                    EnableDataSourceIfPresent(report, table.TableName);
                }

                return;
            }

            if (value is DataTable tableValue)
            {
                EnableDataSourceIfPresent(report, tableValue.TableName);
            }
        }

        private static void PrepareReport(Report report)
        {
            if (!report.Prepare())
            {
                throw new InvalidOperationException("FastReport did not prepare the report successfully.");
            }
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

            var resolvedReportPath = Path.GetFullPath(reportPath);
            var currentStage = ReportLoadStage.ResolveTemplatePath;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                _logger.LogInformation(
                    "[FASTREPORT] Starting report load: Template={ReportPath}, DataSources={DataSources}",
                    resolvedReportPath,
                    DescribeDataSources(dataSources));

                if (!File.Exists(resolvedReportPath))
                {
                    throw new FileNotFoundException("FastReport template file was not found.", resolvedReportPath);
                }

                progress?.Report(0.1);

                // Load the report template (must be executed on the UI thread)
                currentStage = ReportLoadStage.LoadTemplate;
                _logger.LogInformation("[FASTREPORT] Loading template: {ReportPath}", resolvedReportPath);
                using (var templateStream = OpenTemplateStream(resolvedReportPath, _logger))
                {
                    report.Load(templateStream);
                }
                _logger.LogInformation("[FASTREPORT] Template loaded: {ReportPath}", resolvedReportPath);

                progress?.Report(0.3);

                // Register data sources after loading the template, then enable them so the template can bind.
                if (dataSources != null)
                {
                    currentStage = ReportLoadStage.RegisterDataSources;
                    foreach (var kvp in dataSources)
                    {
                        try
                        {
                            _logger.LogDebug(
                                "[FASTREPORT] Registering data source: {DataSourceName} ({DataSourceType})",
                                kvp.Key,
                                kvp.Value?.GetType().FullName ?? "null");

                            if (kvp.Value is DataSet ds)
                            {
                                report.RegisterData(ds, kvp.Key);
                            }
                            else if (kvp.Value is DataTable dt)
                            {
                                var newDs = new DataSet(kvp.Key);
                                var copy = dt.Copy();
                                copy.TableName = string.IsNullOrWhiteSpace(copy.TableName) ? kvp.Key : copy.TableName;
                                newDs.Tables.Add(copy);
                                report.RegisterData(newDs, kvp.Key);
                            }
                            else if (kvp.Value is IEnumerable enumerable)
                            {
                                var newDs = ConvertEnumerableToDataSet(enumerable, kvp.Key);
                                report.RegisterData(newDs, kvp.Key);
                            }
                            else
                            {
                                _logger.LogWarning("Data source {Name} of type {Type} is not supported for automatic registration - skipping.", kvp.Key, kvp.Value?.GetType().FullName ?? "null");
                            }

                            EnableRegisteredDataSources(report, kvp.Key, kvp.Value);
                            _logger.LogDebug("[FASTREPORT] Registered data source: {DataSourceName}", kvp.Key);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "[FASTREPORT] Failed to register data source {Name}", kvp.Key);
                            throw CreateStageFailure(
                                currentStage,
                                resolvedReportPath,
                                ex,
                                $"Data source '{kvp.Key}' could not be registered.");
                        }
                    }
                }

                progress?.Report(0.6);
                cancellationToken.ThrowIfCancellationRequested();

                // Prepare the report after all data and parameters are registered.
                currentStage = ReportLoadStage.PrepareReport;
                _logger.LogInformation("[FASTREPORT] Preparing report: {ReportPath}", resolvedReportPath);
                PrepareReport(report);
                _logger.LogInformation("[FASTREPORT] Report prepared: {ReportPath}", resolvedReportPath);

                progress?.Report(1.0);
                _logger.LogInformation("Report loaded successfully: {ReportPath}", resolvedReportPath);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("[FASTREPORT] Report load canceled during {Stage} for {ReportPath}", currentStage, resolvedReportPath);
                throw;
            }
            catch (InvalidOperationException ex) when (ex.InnerException != null && ex.Message.StartsWith("FastReport load failed during ", StringComparison.Ordinal))
            {
                _logger.LogError(ex, "[FASTREPORT] Report load failed during {Stage}: {ReportPath}", currentStage, resolvedReportPath);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[FASTREPORT] Report load failed during {Stage}: {ReportPath}", currentStage, resolvedReportPath);
                throw CreateStageFailure(currentStage, resolvedReportPath, ex, ex.Message);
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

                PrepareReport(report);

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

                // Re-prepare the report with the updated parameters.
                PrepareReport(report);

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

                PrepareReport(report);
                throw new NotSupportedException("The FastReport Open Source package in this solution does not expose a direct print API. Use the PDF preview/export workflow instead.");
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
