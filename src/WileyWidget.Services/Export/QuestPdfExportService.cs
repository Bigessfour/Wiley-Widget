using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using WileyWidget.Models;

namespace WileyWidget.Services.Export
{
    /// <summary>
    /// Service for exporting data to PDF using QuestPDF (MIT license).
    /// Replaces Syncfusion.Pdf with open-source alternative.
    /// </summary>
    public class QuestPdfExportService
    {
        private readonly ILogger<QuestPdfExportService> _logger;

        public QuestPdfExportService(ILogger<QuestPdfExportService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Configure QuestPDF license
            QuestPDF.Settings.License = LicenseType.Community;
        }

        /// <summary>
        /// Exports data to PDF format
        /// </summary>
        public async Task ExportToPdfAsync(object data, string filePath)
        {
            await Task.Run(() =>
            {
                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(2, Unit.Centimetre);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(12));

                        page.Header()
                            .Text("Wiley Widget Report")
                            .SemiBold().FontSize(20).FontColor(Colors.Blue.Medium);

                        page.Content()
                            .PaddingVertical(1, Unit.Centimetre)
                            .Column(x =>
                            {
                                x.Spacing(20);

                                // Handle different data types
                                if (data is IEnumerable<object> enumerableData)
                                {
                                    var items = enumerableData.ToList();
                                    if (items.Any())
                                    {
                                        var firstItem = items.First();
                                        var properties = firstItem.GetType().GetProperties();

                                        // Create table
                                        x.Item().Table(table =>
                                        {
                                            // Define columns
                                            table.ColumnsDefinition(columns =>
                                            {
                                                foreach (var prop in properties)
                                                {
                                                    columns.RelativeColumn();
                                                }
                                            });

                                            // Add headers
                                            table.Header(header =>
                                            {
                                                foreach (var prop in properties)
                                                {
                                                    header.Cell().Element(CellStyle).Text(prop.Name).SemiBold();
                                                }

                                                static IContainer CellStyle(IContainer container)
                                                {
                                                    return container.DefaultTextStyle(x => x.SemiBold())
                                                        .PaddingVertical(5)
                                                        .BorderBottom(1)
                                                        .BorderColor(Colors.Black);
                                                }
                                            });

                                            // Add data rows
                                            foreach (var item in items)
                                            {
                                                foreach (var prop in properties)
                                                {
                                                    var value = prop.GetValue(item);
                                                    table.Cell().Element(CellStyle).Text(value?.ToString() ?? "");
                                                }

                                                static IContainer CellStyle(IContainer container)
                                                {
                                                    return container.BorderBottom(1)
                                                        .BorderColor(Colors.Grey.Lighten2)
                                                        .PaddingVertical(5);
                                                }
                                            }
                                        });
                                    }
                                }
                                else
                                {
                                    // Single object
                                    var properties = data.GetType().GetProperties();
                                    foreach (var prop in properties)
                                    {
                                        var value = prop.GetValue(data);
                                        x.Item().Row(row =>
                                        {
                                            row.RelativeItem().Text(prop.Name + ":").SemiBold();
                                            row.RelativeItem().Text(value?.ToString() ?? "");
                                        });
                                    }
                                }
                            });

                        page.Footer()
                            .AlignCenter()
                            .Text(x =>
                            {
                                x.Span("Page ");
                                x.CurrentPageNumber();
                                x.Span(" of ");
                                x.TotalPages();
                            });
                    });
                });

                document.GeneratePdf(filePath);
                _logger.LogInformation("PDF exported successfully to {FilePath}", filePath);
            });
        }

        /// <summary>
        /// Exports budget entries to PDF
        /// </summary>
        public async Task<string> ExportBudgetEntriesToPdfAsync(IEnumerable<BudgetEntry> entries, string filePath)
        {
            await Task.Run(() =>
            {
                var entryList = entries.ToList();

                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4.Landscape());
                        page.Margin(1, Unit.Centimetre);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(10));

                        page.Header()
                            .Text("Budget Entries Report")
                            .SemiBold().FontSize(16).FontColor(Colors.Blue.Medium);

                        page.Content()
                            .PaddingVertical(0.5f, Unit.Centimetre)
                            .Column(x =>
                            {
                                x.Item().Table(table =>
                                {
                                    // Define columns
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.ConstantColumn(50);  // ID
                                        columns.RelativeColumn(2);   // Account Code
                                        columns.RelativeColumn(3);   // Description
                                        columns.RelativeColumn(1);   // Amount
                                        columns.RelativeColumn(1);   // Date
                                        columns.RelativeColumn(1);   // Category
                                    });

                                    // Add headers
                                    table.Header(header =>
                                    {
                                        header.Cell().Element(HeaderStyle).Text("ID");
                                        header.Cell().Element(HeaderStyle).Text("Account Code");
                                        header.Cell().Element(HeaderStyle).Text("Description");
                                        header.Cell().Element(HeaderStyle).Text("Amount");
                                        header.Cell().Element(HeaderStyle).Text("Date");
                                        header.Cell().Element(HeaderStyle).Text("Category");

                                        static IContainer HeaderStyle(IContainer container)
                                        {
                                            return container.DefaultTextStyle(x => x.SemiBold())
                                                .PaddingVertical(5)
                                                .Background(Colors.Blue.Lighten3)
                                                .BorderBottom(1)
                                                .BorderColor(Colors.Black);
                                        }
                                    });

                                    // Add data rows
                                    foreach (var entry in entryList)
                                    {
                                        table.Cell().Element(CellStyle).Text(entry.Id.ToString());
                                        table.Cell().Element(CellStyle).Text(entry.AccountNumber ?? "");
                                        table.Cell().Element(CellStyle).Text(entry.Description ?? "");
                                        table.Cell().Element(CellStyle).Text($"${entry.BudgetedAmount:N2}");
                                        table.Cell().Element(CellStyle).Text(entry.StartPeriod.ToString("MM/dd/yyyy"));
                                        table.Cell().Element(CellStyle).Text(entry.FundType.ToString());

                                        static IContainer CellStyle(IContainer container)
                                        {
                                            return container.BorderBottom(1)
                                                .BorderColor(Colors.Grey.Lighten2)
                                                .PaddingVertical(3)
                                                .PaddingHorizontal(5);
                                        }
                                    }
                                });
                            });

                        page.Footer()
                            .AlignCenter()
                            .Text(x =>
                            {
                                x.Span("Page ");
                                x.CurrentPageNumber();
                                x.Span(" of ");
                                x.TotalPages();
                                x.Span(" | Generated: " + DateTime.Now.ToString("MM/dd/yyyy HH:mm"));
                            });
                    });
                });

                document.GeneratePdf(filePath);
                _logger.LogInformation("Exported {Count} budget entries to PDF: {FilePath}", entryList.Count, filePath);
            });

            return filePath;
        }

        /// <summary>
        /// Exports municipal accounts to PDF
        /// </summary>
        public async Task<string> ExportMunicipalAccountsToPdfAsync(IEnumerable<MunicipalAccount> accounts, string filePath)
        {
            await Task.Run(() =>
            {
                var accountList = accounts.ToList();

                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4.Landscape());
                        page.Margin(1, Unit.Centimetre);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(10));

                        page.Header()
                            .Text("Municipal Accounts Report")
                            .SemiBold().FontSize(16).FontColor(Colors.Blue.Medium);

                        page.Content()
                            .PaddingVertical(0.5f, Unit.Centimetre)
                            .Column(x =>
                            {
                                x.Item().Table(table =>
                                {
                                    // Define columns
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(2);   // Account Number
                                        columns.RelativeColumn(3);   // Description
                                        columns.RelativeColumn(1);   // Type
                                        columns.RelativeColumn(1);   // Balance
                                        columns.RelativeColumn(1);   // Budget
                                        columns.RelativeColumn(1);   // Variance
                                    });

                                    // Add headers
                                    table.Header(header =>
                                    {
                                        header.Cell().Element(HeaderStyle).Text("Account #");
                                        header.Cell().Element(HeaderStyle).Text("Description");
                                        header.Cell().Element(HeaderStyle).Text("Type");
                                        header.Cell().Element(HeaderStyle).Text("Balance");
                                        header.Cell().Element(HeaderStyle).Text("Budget");
                                        header.Cell().Element(HeaderStyle).Text("Variance");

                                        static IContainer HeaderStyle(IContainer container)
                                        {
                                            return container.DefaultTextStyle(x => x.SemiBold())
                                                .PaddingVertical(5)
                                                .Background(Colors.Blue.Lighten3)
                                                .BorderBottom(1)
                                                .BorderColor(Colors.Black);
                                        }
                                    });

                                    // Add data rows
                                    foreach (var account in accountList)
                                    {
                                        // Map to current model property names
                                        var variance = account.BudgetAmount - account.Balance;
                                        var varianceColor = variance < 0 ? Colors.Red.Medium : Colors.Green.Medium;

                                        table.Cell().Element(CellStyle).Text(account.AccountNumber?.ToString() ?? account.AccountNumber_Value ?? "");
                                        table.Cell().Element(CellStyle).Text(account.Name ?? "");
                                        table.Cell().Element(CellStyle).Text(account.TypeDescription ?? account.Type.ToString());
                                        table.Cell().Element(CellStyle).Text($"${account.Balance:N2}");
                                        table.Cell().Element(CellStyle).Text($"${account.BudgetAmount:N2}");
                                        table.Cell().Element(CellStyle).Text($"${variance:N2}").FontColor(varianceColor);

                                        static IContainer CellStyle(IContainer container)
                                        {
                                            return container.BorderBottom(1)
                                                .BorderColor(Colors.Grey.Lighten2)
                                                .PaddingVertical(3)
                                                .PaddingHorizontal(5);
                                        }
                                    }
                                });
                            });

                        page.Footer()
                            .AlignCenter()
                            .Text(x =>
                            {
                                x.Span("Page ");
                                x.CurrentPageNumber();
                                x.Span(" of ");
                                x.TotalPages();
                                x.Span(" | Generated: " + DateTime.Now.ToString("MM/dd/yyyy HH:mm"));
                            });
                    });
                });

                document.GeneratePdf(filePath);
                _logger.LogInformation("Exported {Count} municipal accounts to PDF: {FilePath}", accountList.Count, filePath);
            });

            return filePath;
        }
    }
}
