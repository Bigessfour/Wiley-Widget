using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WileyWidget.Services.Abstractions
{
    /// <summary>
    /// Abstraction for Bold Reports integration used by the UI layer.
    /// Kept minimal so implementations can interact with a viewer object via reflection
    /// without requiring references in the Abstractions assembly.
    /// </summary>
    public interface IBoldReportService
    {
        Task ConfigureViewerAsync(object reportViewer, Dictionary<string, object> options, CancellationToken cancellationToken = default);

        Task LoadReportAsync(
            object reportViewer,
            string reportPath,
            Dictionary<string, object>? dataSources = null,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default);

        Task ExportToPdfAsync(object reportViewer, string filePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default);

        Task ExportToExcelAsync(object reportViewer, string filePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default);

        Task PrintAsync(object reportViewer, CancellationToken cancellationToken = default);

        Task RefreshReportAsync(object reportViewer, CancellationToken cancellationToken = default);

        Task SetReportParametersAsync(object reportViewer, Dictionary<string, object> parameters, CancellationToken cancellationToken = default);
    }
}
