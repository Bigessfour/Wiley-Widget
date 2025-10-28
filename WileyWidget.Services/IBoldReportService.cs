#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace WileyWidget.Services;

/// <summary>
/// Interface for Bold Reports integration service
/// </summary>
public interface IBoldReportService
{
    /// <summary>
    /// Loads an RDL report into the report viewer
    /// </summary>
    /// <param name="reportViewer">The Bold ReportViewer control</param>
    /// <param name="reportPath">Path to the RDL report file</param>
    /// <param name="dataSources">Data sources for the report</param>
    Task LoadReportAsync(Control reportViewer, string reportPath, Dictionary<string, object>? dataSources = null);

    /// <summary>
    /// Exports the current report to PDF
    /// </summary>
    /// <param name="reportViewer">The Bold ReportViewer control</param>
    /// <param name="filePath">Path to save the PDF file</param>
    Task ExportToPdfAsync(Control reportViewer, string filePath);

    /// <summary>
    /// Exports the current report to Excel
    /// </summary>
    /// <param name="reportViewer">The Bold ReportViewer control</param>
    /// <param name="filePath">Path to save the Excel file</param>
    Task ExportToExcelAsync(Control reportViewer, string filePath);

    /// <summary>
    /// Refreshes the report data
    /// </summary>
    /// <param name="reportViewer">The Bold ReportViewer control</param>
    Task RefreshReportAsync(Control reportViewer);

    /// <summary>
    /// Sets report parameters
    /// </summary>
    /// <param name="reportViewer">The Bold ReportViewer control</param>
    /// <param name="parameters">Report parameters</param>
    Task SetReportParametersAsync(Control reportViewer, Dictionary<string, object> parameters);
}
