using System.Collections.Generic;
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
        Task LoadReportAsync(object reportViewer, string reportPath, Dictionary<string, object>? dataSources = null);

        Task ExportToPdfAsync(object reportViewer, string filePath);

        Task ExportToExcelAsync(object reportViewer, string filePath);

        Task RefreshReportAsync(object reportViewer);

        Task SetReportParametersAsync(object reportViewer, Dictionary<string, object> parameters);
    }
}
