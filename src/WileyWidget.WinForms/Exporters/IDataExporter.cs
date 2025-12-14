using System.Threading.Tasks;

namespace WileyWidget.WinForms.Exporters
{
    /// <summary>
    /// Stub exporter service for data export functionality.
    /// </summary>
    public interface IDataExporter
    {
        Task ExportToCsvAsync(object data, string filePath);
        Task ExportToExcelAsync(object data, string filePath);
        Task ExportToPdfAsync(object data, string filePath);
    }
}
