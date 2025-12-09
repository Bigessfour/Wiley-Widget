using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WileyWidget.Services
{
    /// <summary>
    /// Abstraction for Bold Reports integration used by the UI layer.
    /// Kept minimal so implementations can interact with a viewer object via reflection
    /// without requiring WPF references in the Abstractions assembly.
    /// </summary>
    public interface IBoldReportService
    {
        Task LoadReportAsync(object reportViewer, string reportPath, Dictionary<string, object>? dataSources = null, IProgress<double>? progress = null, CancellationToken cancellationToken = default);

        Task ExportToPdfAsync(object reportViewer, string filePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default);

        Task ExportToExcelAsync(object reportViewer, string filePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default);

        Task RefreshReportAsync(object reportViewer, IProgress<double>? progress = null, CancellationToken cancellationToken = default);

        Task SetReportParametersAsync(object reportViewer, Dictionary<string, object> parameters, CancellationToken cancellationToken = default);

        /// <summary>
        /// Configure viewer properties via a best-effort reflection-based approach.
        /// Options can include keys like "EnableCustomCode", "ValidateHyperlinks", "DisableExtensions", etc.
        /// Implementations should attempt to set matching properties on the viewer when available.
        /// </summary>
        Task ConfigureViewerAsync(object reportViewer, Dictionary<string, object> options, CancellationToken cancellationToken = default);

        /// <summary>
        /// Print the current report using viewer's print method if available.
        /// </summary>
        Task PrintAsync(object reportViewer, CancellationToken cancellationToken = default);
    }
}
