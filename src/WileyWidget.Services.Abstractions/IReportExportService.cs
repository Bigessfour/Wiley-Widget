using System.Collections.Generic;
using System.Threading.Tasks;
using WileyWidget.Models;

namespace WileyWidget.Services
{
    /// <summary>
    /// Interface for report export services
    /// </summary>
    public interface IReportExportService
    {
        /// <summary>
        /// Exports data to PDF format
        /// </summary>
        Task ExportToPdfAsync(object data, string filePath);

        /// <summary>
        /// Exports data to Excel format
        /// </summary>
        Task ExportToExcelAsync(object data, string filePath);

        /// <summary>
        /// Exports data to CSV format
        /// </summary>
        Task ExportToCsvAsync(IEnumerable<object> data, string filePath);

        /// <summary>
        /// Gets supported export formats
        /// </summary>
        IEnumerable<string> GetSupportedFormats();

        /// <summary>
        /// Exports a compliance report to PDF
        /// </summary>
        Task ExportComplianceReportToPdfAsync(ComplianceReport report, string filePath);

        /// <summary>
        /// Exports a compliance report to Excel
        /// </summary>
        Task ExportComplianceReportToExcelAsync(ComplianceReport report, string filePath);
    }
}
