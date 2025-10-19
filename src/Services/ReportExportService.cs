using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.XlsIO;

namespace WileyWidget.Services;

/// <summary>
/// Implementation of report export service
/// </summary>
public class ReportExportService : IReportExportService
{
    /// <summary>
    /// Exports data to PDF format
    /// </summary>
    public async Task ExportToPdfAsync(object data, string filePath)
    {
        // Offload PDF generation to a background thread (CPU/disk-bound)
        await Task.Run(() =>
        {
            using (var document = new PdfDocument())
            {
                var page = document.Pages.Add();
                var graphics = page.Graphics;

                // Set up font
                var font = new PdfStandardFont(PdfFontFamily.Helvetica, 12);

                float yPosition = 10;

                // Handle different data types
                if (data is IEnumerable<object> enumerableData)
                {
                    // Tabular data
                    var items = enumerableData.ToList();
                    if (items.Any())
                    {
                        // Get properties from first item
                        var properties = items.First().GetType().GetProperties()
                            .Where(p => p.CanRead)
                            .ToArray();

                        // Add headers
                        float xPosition = 10;
                        foreach (var prop in properties)
                        {
                            graphics.DrawString(prop.Name, font, PdfBrushes.Black, xPosition, yPosition);
                            xPosition += 100; // Fixed column width
                        }
                        yPosition += 20;

                        // Add data rows
                        foreach (var item in items)
                        {
                            xPosition = 10;
                            foreach (var prop in properties)
                            {
                                var value = prop.GetValue(item)?.ToString() ?? "";
                                graphics.DrawString(value, font, PdfBrushes.Black, xPosition, yPosition);
                                xPosition += 100;
                            }
                            yPosition += 15;

                            // Start new page if needed
                            if (yPosition > page.GetClientSize().Height - 50)
                            {
                                page = document.Pages.Add();
                                graphics = page.Graphics;
                                yPosition = 10;
                            }
                        }
                    }
                }
                else
                {
                    // Single object - display properties
                    var properties = data.GetType().GetProperties()
                        .Where(p => p.CanRead)
                        .ToArray();

                    foreach (var prop in properties)
                    {
                        var value = prop.GetValue(data)?.ToString() ?? "";
                        var text = $"{prop.Name}: {value}";
                        graphics.DrawString(text, font, PdfBrushes.Black, 10, yPosition);
                        yPosition += 15;
                    }
                }

                // Save the document
                document.Save(filePath);
            }
        });
    }

    /// <summary>
    /// Exports data to Excel format
    /// </summary>
    public async Task ExportToExcelAsync(object data, string filePath)
    {
        // Offload Excel generation to a background thread (CPU/disk-bound)
        await Task.Run(() =>
        {
            using (var excelEngine = new ExcelEngine())
            {
                var application = excelEngine.Excel;
                application.DefaultVersion = ExcelVersion.Excel2016;

                var workbook = application.Workbooks.Create(1);
                var worksheet = workbook.Worksheets[0];

                int rowIndex = 1;

                // Handle different data types
                if (data is IEnumerable<object> enumerableData)
                {
                    // Tabular data
                    var items = enumerableData.ToList();
                    if (items.Any())
                    {
                        // Get properties from first item
                        var properties = items.First().GetType().GetProperties()
                            .Where(p => p.CanRead)
                            .ToArray();

                        // Add headers
                        for (int i = 0; i < properties.Length; i++)
                        {
                            worksheet.Range[rowIndex, i + 1].Value = properties[i].Name;
                        }
                        rowIndex++;

                        // Add data rows
                        foreach (var item in items)
                        {
                            for (int i = 0; i < properties.Length; i++)
                            {
                                var value = properties[i].GetValue(item)?.ToString() ?? "";
                                worksheet.Range[rowIndex, i + 1].Value = value;
                            }
                            rowIndex++;
                        }
                    }
                }
                else
                {
                    // Single object - display properties
                    var properties = data.GetType().GetProperties()
                        .Where(p => p.CanRead)
                        .ToArray();

                    foreach (var prop in properties)
                    {
                        worksheet.Range[rowIndex, 1].Value = prop.Name;
                        worksheet.Range[rowIndex, 2].Value = prop.GetValue(data)?.ToString() ?? "";
                        rowIndex++;
                    }
                }

                // Auto-fit columns
                worksheet.UsedRange.AutofitColumns();

                // Save the workbook
                workbook.SaveAs(filePath);
            }
        });
    }

    /// <summary>
    /// Exports data to CSV format
    /// </summary>
    public async Task ExportToCsvAsync(IEnumerable<object> data, string filePath)
    {
        await Task.Run(() =>
        {
            var items = data.ToList();
            if (!items.Any()) return;

            using (var writer = new StreamWriter(filePath))
            {
                // Get properties from first item
                var properties = items.First().GetType().GetProperties()
                    .Where(p => p.CanRead)
                    .ToArray();

                // Write headers
                var headers = string.Join(",", properties.Select(p => EscapeCsvValue(p.Name)));
                writer.WriteLine(headers);

                // Write data rows
                foreach (var item in items)
                {
                    var values = properties.Select(p =>
                    {
                        var value = p.GetValue(item)?.ToString() ?? "";
                        return EscapeCsvValue(value);
                    });
                    var line = string.Join(",", values);
                    writer.WriteLine(line);
                }
            }
        });
    }

    /// <summary>
    /// Gets supported export formats
    /// </summary>
    public IEnumerable<string> GetSupportedFormats()
    {
        return new[] { "PDF", "Excel", "CSV" };
    }

    /// <summary>
    /// Escapes CSV values that contain commas, quotes, or newlines
    /// </summary>
    private static string EscapeCsvValue(string value)
    {
        if (value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r"))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        return value;
    }
}