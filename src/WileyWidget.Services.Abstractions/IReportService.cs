using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WileyWidget.Services.Abstractions
{
    /// <summary>
    /// Abstraction for FastReport Open Source integration used by the UI layer.
    /// Provides full programmatic control over report generation, export, and viewer operations.
    /// </summary>
    public interface IReportService
    {
        /// <summary>
        /// Loads a report asynchronously.
        /// </summary>
        Task LoadReportAsync(
            object reportViewer,
            string reportPath,
            Dictionary<string, object>? dataSources = null,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Refreshes the report data.
        /// </summary>
        Task RefreshReportAsync(object reportViewer, CancellationToken cancellationToken = default);

        /// <summary>
        /// Exports the current report to PDF format.
        /// </summary>
        Task ExportToPdfAsync(
            object reportViewer,
            string filePath,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Exports the current report to Excel format.
        /// </summary>
        Task ExportToExcelAsync(
            object reportViewer,
            string filePath,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets report parameters.
        /// </summary>
        Task SetReportParametersAsync(
            object reportViewer,
            Dictionary<string, object> parameters,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Prints the current report.
        /// </summary>
        Task PrintAsync(
            object reportViewer,
            CancellationToken cancellationToken = default);
    }
}
