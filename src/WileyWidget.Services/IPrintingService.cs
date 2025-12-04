using System.Threading.Tasks;

namespace WileyWidget.Services
{
    /// <summary>
    /// Interface for printing and previewing reports and charts.
    /// Provides unified API for generating PDFs and displaying print previews.
    /// </summary>
    public interface IPrintingService
    {
        /// <summary>
        /// Generates a PDF from the provided model data.
        /// Returns the path to the generated PDF file.
        /// </summary>
        /// <param name="model">The data model to render into PDF</param>
        /// <returns>Path to the generated PDF file</returns>
        Task<string> GeneratePdfAsync(object model);

        /// <summary>
        /// Shows a print preview dialog for the specified PDF.
        /// </summary>
        /// <param name="pdfPath">Path to the PDF file to preview</param>
        Task PreviewAsync(string pdfPath);

        /// <summary>
        /// Prints the specified PDF directly to the default printer.
        /// </summary>
        /// <param name="pdfPath">Path to the PDF file to print</param>
        Task PrintAsync(string pdfPath);
    }
}