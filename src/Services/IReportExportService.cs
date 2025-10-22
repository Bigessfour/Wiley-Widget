#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WileyWidget.Services;

/// <summary>
/// Interface for report export services
/// </summary>
public interface IReportExportService
{
    /// <summary>
    /// Exports data to PDF format
    /// </summary>
    /// <param name="data">The data to export</param>
    /// <param name="filePath">The file path for the export</param>
    /// <returns>A task representing the export operation</returns>
    Task ExportToPdfAsync(object data, string filePath);

    /// <summary>
    /// Exports data to Excel format
    /// </summary>
    /// <param name="data">The data to export</param>
    /// <param name="filePath">The file path for the export</param>
    /// <returns>A task representing the export operation</returns>
    Task ExportToExcelAsync(object data, string filePath);

    /// <summary>
    /// Exports data to CSV format
    /// </summary>
    /// <param name="data">The data to export</param>
    /// <param name="filePath">The file path for the export</param>
    /// <returns>A task representing the export operation</returns>
    Task ExportToCsvAsync(IEnumerable<object> data, string filePath);

    /// <summary>
    /// Exports a ComplianceReport to a well-formatted PDF using Syncfusion.Pdf
    /// </summary>
    /// <param name="report">The compliance report to export</param>
    /// <param name="filePath">The PDF file path to write</param>
    Task ExportComplianceReportToPdfAsync(WileyWidget.Models.ComplianceReport report, string filePath);

    /// <summary>
    /// Exports a ComplianceReport to a well-structured Excel workbook using Syncfusion.XlsIO
    /// </summary>
    /// <param name="report">The compliance report to export</param>
    /// <param name="filePath">The Excel file path to write</param>
    Task ExportComplianceReportToExcelAsync(WileyWidget.Models.ComplianceReport report, string filePath);

    /// <summary>
    /// Gets supported export formats
    /// </summary>
    /// <returns>Collection of supported formats</returns>
    IEnumerable<string> GetSupportedFormats();
}
