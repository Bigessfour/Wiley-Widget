using System.Threading;
using System.Threading.Tasks;

namespace WileyWidget.WinForms.Exporters
{
    /// <summary>
    /// Contract for exporting data to common business formats.
    /// </summary>
    public interface IDataExporter
    {
        Task ExportToCsvAsync(object data, string filePath, CancellationToken cancellationToken = default);
        Task ExportToExcelAsync(object data, string filePath, CancellationToken cancellationToken = default);
        Task ExportToPdfAsync(object data, string filePath, CancellationToken cancellationToken = default);
    }
}
