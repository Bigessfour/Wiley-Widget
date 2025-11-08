using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using Serilog;
using Polly;
using Polly.Retry;
using Syncfusion.Pdf;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Services.Threading;
using WileyWidget.ViewModels.Messages;
using WileyWidget.Abstractions;

namespace WileyWidget.ViewModels.Main;

public partial class EnterpriseViewModel : BindableBase, IDataErrorInfo, IDisposable, INavigationAware
{
    // Dependencies
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEventAggregator _eventAggregator;
    private readonly IReportExportService _reportExportService;
    private readonly IDispatcherHelper _dispatcherHelper;
    private readonly ICacheService? _cacheService;
    private readonly IAuditService? _auditService;
    private readonly Prism.Dialogs.IDialogService? _dialogService;

    // Expose services for View scenarios that need them (e.g., export helpers, event subscriptions)
    public IEventAggregator EventAggregator => _eventAggregator;
    public IReportExportService ReportExportService => _reportExportService;

    // Core state
    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (_statusMessage != value)
            {
                _statusMessage = value;
                RaisePropertyChanged();
            }
        }
    }

    private string _errorMessage = string.Empty;
    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (_errorMessage != value)
            {
                _errorMessage = value;
                RaisePropertyChanged();
            }
        }
    }

    // Primary data
    public ObservableCollection<Enterprise> EnterpriseList { get; } = new();

    private Enterprise _selectedEnterprise;
    public Enterprise SelectedEnterprise
    {
        get => _selectedEnterprise;
        set
        {
            if (!EqualityComparer<Enterprise>.Default.Equals(_selectedEnterprise, value))
            {
                _selectedEnterprise = value;
                RaisePropertyChanged();
                // react to selection change for UI and side-effects
                ExecuteSelectionChanged();
            }
        }
    }
    /// <summary>
    /// Loading state for async operations
    /// </summary>
    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (_isLoading != value)
            {
                _isLoading = value;
                RaisePropertyChanged();
            }
        }
    }

    /// <summary>
    /// Budget summary text for display
    /// </summary>
    private string _budgetSummaryText = "No budget data available";
    public string BudgetSummaryText
    {
        get => _budgetSummaryText;
        set
        {
            if (_budgetSummaryText != value)
            {
                _budgetSummaryText = value;
                RaisePropertyChanged();
            }
        }
    }

    /// <summary>
    /// Semaphore to prevent concurrent loading operations
    /// </summary>
    private readonly SemaphoreSlim _loadSemaphore = new(1, 1);

    // Missing properties for view bindings
    private int _currentPageIndex;
    public int CurrentPageIndex
    {
        get => _currentPageIndex;
        set
        {
            if (_currentPageIndex != value)
            {
                _currentPageIndex = value;
                RaisePropertyChanged();
            }
        }
    }

    public ObservableCollection<Enterprise> Enterprises => EnterpriseList;

    private int _pageCount;
    public int PageCount
    {
        get => _pageCount;
        set
        {
            if (_pageCount != value)
            {
                _pageCount = value;
                RaisePropertyChanged();
            }
        }
    }

    private int _pageSize = 50;
    public int PageSize
    {
        get => _pageSize;
        set
        {
            if (_pageSize != value)
            {
                _pageSize = value;
                RaisePropertyChanged();
            }
        }
    }

    public ObservableCollection<Enterprise> PagedHierarchicalEnterprises { get; } = new();

    // Enterprise type breakdown used for dashboards/filters
    public System.Collections.ObjectModel.ObservableCollection<WileyWidget.Models.EnterpriseTypeItem> EnterpriseTypes { get; } = new();

    // Form properties for editing
    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                RaisePropertyChanged();
            }
        }
    }

    private string _description = string.Empty;
    public string Description
    {
        get => _description;
        set
        {
            if (_description != value)
            {
                _description = value;
                RaisePropertyChanged();
            }
        }
    }

    private string _type = string.Empty;
    public string Type
    {
        get => _type;
        set
        {
            if (_type != value)
            {
                _type = value;
                RaisePropertyChanged();
            }
        }
    }

    private DateTime _fiscalYearStart = DateTime.Now;
    public DateTime FiscalYearStart
    {
        get => _fiscalYearStart;
        set
        {
            if (_fiscalYearStart != value)
            {
                _fiscalYearStart = value;
                RaisePropertyChanged();
            }
        }
    }

    private decimal _progressPercentage;
    public decimal ProgressPercentage
    {
        get => _progressPercentage;
        set
        {
            if (_progressPercentage != value)
            {
                _progressPercentage = value;
                RaisePropertyChanged();
            }
        }
    }

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText != value)
            {
                _searchText = value;
                RaisePropertyChanged();
            }
        }
    }

    private object _selectedNode;
    public object SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (_selectedNode != value)
            {
                _selectedNode = value;
                RaisePropertyChanged();
            }
        }
    }

    private string _selectedStatusFilter = "All";
    public string SelectedStatusFilter
    {
        get => _selectedStatusFilter;
        set
        {
            if (_selectedStatusFilter != value)
            {
                _selectedStatusFilter = value;
                RaisePropertyChanged();
            }
        }
    }

    public ObservableCollection<string> StatusOptions { get; } = new() { "All", "Active", "Inactive", "Pending" };

    /// <summary>
    /// Selection changed command - updates budget summary and enables drill-down navigation
    /// </summary>
    private void ExecuteSelectionChanged()
    {
        if (SelectedEnterprise != null)
        {
            // Update budget summary when selection changes
            BudgetSummaryText = $"Selected: {SelectedEnterprise.Name}\n" +
                               $"Monthly Revenue: {SelectedEnterprise.MonthlyRevenue:C2}\n" +
                               $"Monthly Expenses: {SelectedEnterprise.MonthlyExpenses:C2}\n" +
                               $"Monthly Balance: {SelectedEnterprise.MonthlyBalance:C2}\n" +
                               $"Citizens Served: {SelectedEnterprise.CitizenCount:N0}";

            StatusMessage = $"Selected: {SelectedEnterprise.Name}";
            ErrorMessage = string.Empty;

            Log.Debug("Enterprise selected: {EnterpriseName} (ID: {EnterpriseId})",
                     SelectedEnterprise.Name, SelectedEnterprise.Id);
        }
        else
        {
            BudgetSummaryText = GetBudgetSummary();
            StatusMessage = "Ready";
        }
    }

    /// <summary>
    /// Navigate to enterprise details view
    /// </summary>
    private void ExecuteNavigateToDetails(int? enterpriseId)
    {
        try
        {
            if (enterpriseId == null)
            {
                ErrorMessage = "Navigation failed: No enterprise id provided";
                Log.Warning("Navigation failed: enterprise id was null");
                return;
            }

            // Find enterprise by ID
            var enterprise = EnterpriseList.FirstOrDefault(e => e.Id == enterpriseId.Value);
            if (enterprise != null)
            {
                SelectedEnterprise = enterprise;
                StatusMessage = $"Viewing details for: {enterprise.Name}";
                Log.Information("Navigated to enterprise details: {EnterpriseId}", enterpriseId.Value);
            }
            else
            {
                ErrorMessage = $"Enterprise with ID {enterpriseId.Value} not found";
                Log.Warning("Navigation failed: Enterprise ID {EnterpriseId} not found", enterpriseId.Value);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Navigation failed: {ex.Message}";
            Log.Error(ex, "Error navigating to enterprise details for ID: {EnterpriseId}", enterpriseId);
        }
    }

    /// <summary>
    /// Navigate to BudgetView command
    /// </summary>
    private void ExecuteNavigateToBudgetView()
    {
        // Navigation to BudgetView - implementation depends on navigation service
        // This could use messaging, navigation service, or window management
        // For now, this is a stub that can be implemented based on the app's navigation pattern
    }

    /// <summary>
    /// Export to Excel command
    /// </summary>
    private async Task ExecuteExportToExcelAsync()
    {
        try
        {
            StatusMessage = "Generating Excel export...";

            // Get current enterprises data
            var enterprises = await _unitOfWork.Enterprises.GetAllAsync();
            var enterpriseList = enterprises.ToList();

            if (!enterpriseList.Any())
            {
                StatusMessage = "No enterprise data available for Excel export";
                return;
            }

            // Create save file dialog
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Save Excel Export",
                Filter = "Excel files (*.xlsx)|*.xlsx",
                DefaultExt = ".xlsx",
                FileName = $"Enterprise_Export_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                var filePath = saveFileDialog.FileName;

                // Generate Excel export
                await GenerateEnterpriseAdvancedExcelExportAsync(enterpriseList, filePath);

                StatusMessage = $"Excel export saved to {System.IO.Path.GetFileName(filePath)}";
            }
            else
            {
                StatusMessage = "Excel export cancelled";
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in Excel export command");
            StatusMessage = $"Error exporting to Excel: {ex.Message}";
        }
    }

    /// <summary>
    /// Export to PDF report command
    /// </summary>
    private async Task ExecuteExportToPdfReportAsync()
    {
        try
        {
            StatusMessage = "Generating PDF report...";

            // Get current enterprises data
            var enterprises = await _unitOfWork.Enterprises.GetAllAsync();
            var enterpriseList = enterprises.ToList();

            if (!enterpriseList.Any())
            {
                StatusMessage = "No enterprise data available for PDF report";
                return;
            }

            // Create save file dialog
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Save PDF Report",
                Filter = "PDF files (*.pdf)|*.pdf",
                DefaultExt = ".pdf",
                FileName = $"Enterprise_Report_{DateTime.Now:yyyyMMdd_HHmmss}.pdf"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                var filePath = saveFileDialog.FileName;

                // Generate PDF report
                await GenerateEnterprisePdfReportAsync(enterpriseList, filePath);

                StatusMessage = $"PDF report saved to {System.IO.Path.GetFileName(filePath)}";
            }
            else
            {
                StatusMessage = "PDF report export cancelled";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error generating PDF report: {ex.Message}";
            Log.Error(ex, "Error in PDF report export command");
        }
    }

    private async Task GenerateEnterprisePdfReportAsync(IEnumerable<Enterprise> enterprises, string filePath)
    {
        // PDF generation is CPU/disk-bound (drawing + file save). Run off the UI thread.
        await Task.Run(() =>
        {
            using (var document = new Syncfusion.Pdf.PdfDocument())
            {
                var page = document.Pages.Add();
                var graphics = page.Graphics;
                var font = new Syncfusion.Pdf.Graphics.PdfStandardFont(Syncfusion.Pdf.Graphics.PdfFontFamily.Helvetica, 12);
                var headerFont = new Syncfusion.Pdf.Graphics.PdfStandardFont(Syncfusion.Pdf.Graphics.PdfFontFamily.Helvetica, 16, Syncfusion.Pdf.Graphics.PdfFontStyle.Bold);

                float yPosition = 20;

                // Title
                    foreach (var enterprise in enterprises)
                    {
                        if (yPosition > page.GetClientSize().Height - 50)
                    {
                        page = document.Pages.Add();
                        graphics = page.Graphics;
                        yPosition = 20;
                    }
                        graphics.DrawString(enterprise.Name ?? "N/A", font, Syncfusion.Pdf.Graphics.PdfBrushes.Black, 20, yPosition);
                        graphics.DrawString(enterprise.Type ?? "N/A", font, Syncfusion.Pdf.Graphics.PdfBrushes.Black, 120, yPosition);
                        graphics.DrawString(enterprise.CurrentRate.ToString("C", CultureInfo.InvariantCulture), font, Syncfusion.Pdf.Graphics.PdfBrushes.Black, 220, yPosition);
                        graphics.DrawString(enterprise.CitizenCount.ToString(CultureInfo.InvariantCulture), font, Syncfusion.Pdf.Graphics.PdfBrushes.Black, 320, yPosition);
                        yPosition += 15;
                    }

                // Summary
                if (yPosition > page.GetClientSize().Height - 100)
                {
                    page = document.Pages.Add();
                    graphics = page.Graphics;
                    yPosition = 20;
                }

                yPosition += 20;
                var totalBudget = enterprises.Sum(e => e.CurrentRate);
                var totalCitizens = enterprises.Sum(e => e.CitizenCount);

                graphics.DrawString($"Total Budget: {totalBudget:C}", headerFont, Syncfusion.Pdf.Graphics.PdfBrushes.Black, 20, yPosition);
                yPosition += 20;
                graphics.DrawString($"Total Citizens Served: {totalCitizens:N0}", font, Syncfusion.Pdf.Graphics.PdfBrushes.Black, 20, yPosition);

                // Save the document
                document.Save(filePath);
            }
        });
    }

    private async Task GenerateEnterpriseCsvExportAsync(IEnumerable<Enterprise> enterprises, string filePath)
    {
        // CSV generation (string formatting + file I/O) is CPU/disk-bound. Run off the UI thread.
        await Task.Run(() =>
        {
            using (var writer = new System.IO.StreamWriter(filePath, false, System.Text.Encoding.UTF8))
            {
                // Write CSV header
                writer.WriteLine("\"Name\",\"Type\",\"Current Rate\",\"Monthly Expenses\",\"Citizens Served\",\"Created Date\",\"Modified Date\"");

                // Write data rows
                foreach (var enterprise in enterprises)
                {
                    var name = enterprise.Name?.Replace("\"", "\"\"", StringComparison.Ordinal) ?? ""; // Escape quotes
                    var type = enterprise.Type?.Replace("\"", "\"\"", StringComparison.Ordinal) ?? "";
                    var rate = enterprise.CurrentRate.ToString("F2", CultureInfo.InvariantCulture);
                    var expenses = enterprise.MonthlyExpenses.ToString("F2", CultureInfo.InvariantCulture);
                    var citizens = enterprise.CitizenCount.ToString(CultureInfo.InvariantCulture);
                    var created = enterprise.CreatedDate.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                    var modified = enterprise.ModifiedDate?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? "";

                    writer.WriteLine($"\"{name}\",\"{type}\",\"{rate}\",\"{expenses}\",\"{citizens}\",\"{created}\",\"{modified}\"");
                }
            }
        });
    }

    private async Task GenerateEnterpriseAdvancedExcelExportAsync(IEnumerable<Enterprise> enterprises, string filePath)
    {
        // Excel (Syncfusion) export is CPU and I/O heavy; keep on background thread.
        await Task.Run(() =>
        {
            using (var excelEngine = new Syncfusion.XlsIO.ExcelEngine())
            {
                var application = excelEngine.Excel;
                application.DefaultVersion = Syncfusion.XlsIO.ExcelVersion.Xlsx;

                var workbook = application.Workbooks.Create(1);
                var worksheet = workbook.Worksheets[0];
                worksheet.Name = "Enterprise Report";

                // Set up headers with formatting
                worksheet.Range["A1"].Text = "Enterprise Report";
                worksheet.Range["A1:G1"].Merge();
                worksheet.Range["A1"].CellStyle.Font.Size = 16;
                worksheet.Range["A1"].CellStyle.Font.Bold = true;
                worksheet.Range["A1"].HorizontalAlignment = Syncfusion.XlsIO.ExcelHAlign.HAlignCenter;

                // Report info
                worksheet.Range["A3"].Text = "Generated:";
                worksheet.Range["B3"].Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                worksheet.Range["A4"].Text = "Total Enterprises:";
                worksheet.Range["B4"].Text = enterprises.Count().ToString(CultureInfo.InvariantCulture);

                // Column headers
                worksheet.Range["A6"].Text = "Name";
                worksheet.Range["B6"].Text = "Type";
                worksheet.Range["C6"].Text = "Current Rate";
                worksheet.Range["D6"].Text = "Monthly Expenses";
                worksheet.Range["E6"].Text = "Citizens Served";
                worksheet.Range["F6"].Text = "Created Date";
                worksheet.Range["G6"].Text = "Modified Date";

                // Style headers
                var headerRange = worksheet.Range["A6:G6"];
                headerRange.CellStyle.Font.Bold = true;
                headerRange.CellStyle.Interior.Color = System.Drawing.Color.LightGray;
                headerRange.CellStyle.HorizontalAlignment = Syncfusion.XlsIO.ExcelHAlign.HAlignCenter;

                // Data rows
                int row = 7;
                foreach (var enterprise in enterprises)
                {
                    worksheet.Range[$"A{row}"].Text = enterprise.Name ?? "";
                    worksheet.Range[$"B{row}"].Text = enterprise.Type ?? "";
                    worksheet.Range[$"C{row}"].Number = (double)enterprise.CurrentRate;
                    worksheet.Range[$"C{row}"].NumberFormat = "$#,##0.00";
                    worksheet.Range[$"D{row}"].Number = (double)enterprise.MonthlyExpenses;
                    worksheet.Range[$"D{row}"].NumberFormat = "$#,##0.00";
                    worksheet.Range[$"E{row}"].Number = enterprise.CitizenCount;
                    worksheet.Range[$"F{row}"].Text = enterprise.CreatedDate.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                    worksheet.Range[$"G{row}"].Text = enterprise.ModifiedDate?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? "";

                    row++;
                }

                // Auto-fit columns
                worksheet.UsedRange.AutofitColumns();

                // Summary section
                row += 2;
                worksheet.Range[$"A{row}"].Text = "Summary";
                worksheet.Range[$"A{row}:B{row}"].Merge();
                worksheet.Range[$"A{row}"].CellStyle.Font.Bold = true;

                row++;
                var totalBudget = enterprises.Sum(e => e.CurrentRate);
                var totalExpenses = enterprises.Sum(e => e.MonthlyExpenses);
                var totalCitizens = enterprises.Sum(e => e.CitizenCount);

                worksheet.Range[$"A{row}"].Text = "Total Budget:";
                worksheet.Range[$"B{row}"].Number = (double)totalBudget;
                worksheet.Range[$"B{row}"].NumberFormat = "$#,##0.00";

                row++;
                worksheet.Range[$"A{row}"].Text = "Total Monthly Expenses:";
                worksheet.Range[$"B{row}"].Number = (double)totalExpenses;
                worksheet.Range[$"B{row}"].NumberFormat = "$#,##0.00";

                row++;
                worksheet.Range[$"A{row}"].Text = "Total Citizens Served:";
                worksheet.Range[$"B{row}"].Number = totalCitizens;

                // Save the workbook
                workbook.SaveAs(filePath);
            }
        });
    }

    private async Task GenerateEnterpriseComprehensiveReportAsync(IEnumerable<Enterprise> enterprises, string filePath)
    {
        // Comprehensive report generation can be expensive; run on background thread to keep UI responsive.
        await Task.Run(() =>
        {
            using (var document = new Syncfusion.Pdf.PdfDocument())
            {
                var page = document.Pages.Add();
                var graphics = page.Graphics;
                var font = new Syncfusion.Pdf.Graphics.PdfStandardFont(Syncfusion.Pdf.Graphics.PdfFontFamily.Helvetica, 12);
                var headerFont = new Syncfusion.Pdf.Graphics.PdfStandardFont(Syncfusion.Pdf.Graphics.PdfFontFamily.Helvetica, 16, Syncfusion.Pdf.Graphics.PdfFontStyle.Bold);
                var titleFont = new Syncfusion.Pdf.Graphics.PdfStandardFont(Syncfusion.Pdf.Graphics.PdfFontFamily.Helvetica, 20, Syncfusion.Pdf.Graphics.PdfFontStyle.Bold);

                float yPosition = 20;

                // Title
                graphics.DrawString("Enterprise Comprehensive Report", titleFont, Syncfusion.Pdf.Graphics.PdfBrushes.Black, 20, yPosition);
                yPosition += 40;

                // Report metadata
                graphics.DrawString($"Generated: {DateTime.Now:g}", font, Syncfusion.Pdf.Graphics.PdfBrushes.Black, 20, yPosition);
                yPosition += 20;
                graphics.DrawString($"Total Enterprises: {enterprises.Count()}", font, Syncfusion.Pdf.Graphics.PdfBrushes.Black, 20, yPosition);
                yPosition += 30;

                // Executive Summary
                graphics.DrawString("Executive Summary", headerFont, Syncfusion.Pdf.Graphics.PdfBrushes.Black, 20, yPosition);
                yPosition += 20;

                var totalRevenue = enterprises.Sum(e => e.MonthlyRevenue);
                var totalExpenses = enterprises.Sum(e => e.MonthlyExpenses);
                var totalBudget = enterprises.Sum(e => e.CurrentRate);
                var totalCitizens = enterprises.Sum(e => e.CitizenCount);
                var avgRate = enterprises.Average(e => e.CurrentRate);

                graphics.DrawString($"Total Monthly Revenue: {totalRevenue:C}", font, Syncfusion.Pdf.Graphics.PdfBrushes.Black, 20, yPosition);
                yPosition += 15;
                graphics.DrawString($"Total Monthly Expenses: {totalExpenses:C}", font, Syncfusion.Pdf.Graphics.PdfBrushes.Black, 20, yPosition);
                yPosition += 15;
                graphics.DrawString($"Net Monthly Position: {(totalRevenue - totalExpenses):C}", font, Syncfusion.Pdf.Graphics.PdfBrushes.Black, 20, yPosition);
                yPosition += 15;
                graphics.DrawString($"Total Citizens Served: {totalCitizens:N0}", font, Syncfusion.Pdf.Graphics.PdfBrushes.Black, 20, yPosition);
                yPosition += 15;
                graphics.DrawString($"Average Rate: {avgRate:C}", font, Syncfusion.Pdf.Graphics.PdfBrushes.Black, 20, yPosition);
                yPosition += 30;

                // Detailed Enterprise List
                graphics.DrawString("Enterprise Details", headerFont, Syncfusion.Pdf.Graphics.PdfBrushes.Black, 20, yPosition);
                yPosition += 20;

                foreach (var enterprise in enterprises)
                {
                    if (yPosition > page.GetClientSize().Height - 100)
                    {
                        page = document.Pages.Add();
                        graphics = page.Graphics;
                        yPosition = 20;
                    }

                    graphics.DrawString($"Name: {enterprise.Name}", headerFont, Syncfusion.Pdf.Graphics.PdfBrushes.Black, 20, yPosition);
                    yPosition += 15;
                    graphics.DrawString($"Type: {enterprise.Type} | Rate: {enterprise.CurrentRate:C} | Citizens: {enterprise.CitizenCount}", font, Syncfusion.Pdf.Graphics.PdfBrushes.Black, 20, yPosition);
                    yPosition += 15;
                    graphics.DrawString($"Revenue: {enterprise.MonthlyRevenue:C} | Expenses: {enterprise.MonthlyExpenses:C} | Balance: {(enterprise.MonthlyRevenue - enterprise.MonthlyExpenses):C}", font, Syncfusion.Pdf.Graphics.PdfBrushes.Black, 20, yPosition);
                    yPosition += 20;
                }

                // Save the document
                document.Save(filePath);
            }
        });
    }

    private async Task GenerateEnterpriseComprehensiveExcelReportAsync(IEnumerable<Enterprise> enterprises, string filePath)
    {
        // Large Excel generation (Syncfusion) - CPU/disk-bound work. Keep off UI thread.
        await Task.Run(() =>
        {
            using (var excelEngine = new Syncfusion.XlsIO.ExcelEngine())
            {
                var application = excelEngine.Excel;
                application.DefaultVersion = Syncfusion.XlsIO.ExcelVersion.Xlsx;

                var workbook = application.Workbooks.Create(1);

                // Summary sheet
                var summarySheet = workbook.Worksheets[0];
                summarySheet.Name = "Summary";

                summarySheet.Range["A1"].Text = "Enterprise Comprehensive Report";
                summarySheet.Range["A1:D1"].Merge();
                summarySheet.Range["A1"].CellStyle.Font.Size = 16;
                summarySheet.Range["A1"].CellStyle.Font.Bold = true;

                summarySheet.Range["A3"].Text = "Generated:";
                summarySheet.Range["B3"].Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                summarySheet.Range["A4"].Text = "Total Enterprises:";
                summarySheet.Range["B4"].Text = enterprises.Count().ToString(CultureInfo.InvariantCulture);

                summarySheet.Range["A6"].Text = "Executive Summary";
                summarySheet.Range["A6:B6"].Merge();
                summarySheet.Range["A6"].CellStyle.Font.Bold = true;

                var totalRevenue = enterprises.Sum(e => e.MonthlyRevenue);
                var totalExpenses = enterprises.Sum(e => e.MonthlyExpenses);
                var totalCitizens = enterprises.Sum(e => e.CitizenCount);
                var avgRate = enterprises.Average(e => e.CurrentRate);

                summarySheet.Range["A8"].Text = "Total Monthly Revenue:";
                summarySheet.Range["B8"].Number = (double)totalRevenue;
                summarySheet.Range["B8"].NumberFormat = "$#,##0.00";

                summarySheet.Range["A9"].Text = "Total Monthly Expenses:";
                summarySheet.Range["B9"].Number = (double)totalExpenses;
                summarySheet.Range["B9"].NumberFormat = "$#,##0.00";

                summarySheet.Range["A10"].Text = "Net Monthly Position:";
                summarySheet.Range["B10"].Number = (double)(totalRevenue - totalExpenses);
                summarySheet.Range["B10"].NumberFormat = "$#,##0.00";

                summarySheet.Range["A11"].Text = "Total Citizens Served:";
                summarySheet.Range["B11"].Number = totalCitizens;

                summarySheet.Range["A12"].Text = "Average Rate:";
                summarySheet.Range["B12"].Number = (double)avgRate;
                summarySheet.Range["B12"].NumberFormat = "$#,##0.00";

                // Details sheet
                var detailsSheet = workbook.Worksheets.Create("Details");

                detailsSheet.Range["A1"].Text = "Enterprise Details";
                detailsSheet.Range["A1:G1"].Merge();
                detailsSheet.Range["A1"].CellStyle.Font.Bold = true;

                detailsSheet.Range["A3"].Text = "Name";
                detailsSheet.Range["B3"].Text = "Type";
                detailsSheet.Range["C3"].Text = "Current Rate";
                detailsSheet.Range["D3"].Text = "Monthly Revenue";
                detailsSheet.Range["E3"].Text = "Monthly Expenses";
                detailsSheet.Range["F3"].Text = "Net Position";
                detailsSheet.Range["G3"].Text = "Citizens Served";

                var headerRange = detailsSheet.Range["A3:G3"];
                headerRange.CellStyle.Font.Bold = true;
                headerRange.CellStyle.Interior.Color = System.Drawing.Color.LightGray;

                int row = 4;
                foreach (var enterprise in enterprises)
                {
                    detailsSheet.Range[$"A{row}"].Text = enterprise.Name ?? "";
                    detailsSheet.Range[$"B{row}"].Text = enterprise.Type ?? "";
                    detailsSheet.Range[$"C{row}"].Number = (double)enterprise.CurrentRate;
                    detailsSheet.Range[$"C{row}"].NumberFormat = "$#,##0.00";
                    detailsSheet.Range[$"D{row}"].Number = (double)enterprise.MonthlyRevenue;
                    detailsSheet.Range[$"D{row}"].NumberFormat = "$#,##0.00";
                    detailsSheet.Range[$"E{row}"].Number = (double)enterprise.MonthlyExpenses;
                    detailsSheet.Range[$"E{row}"].NumberFormat = "$#,##0.00";
                    detailsSheet.Range[$"F{row}"].Number = (double)(enterprise.MonthlyRevenue - enterprise.MonthlyExpenses);
                    detailsSheet.Range[$"F{row}"].NumberFormat = "$#,##0.00";
                    detailsSheet.Range[$"G{row}"].Number = enterprise.CitizenCount;
                    row++;
                }

                // Auto-fit columns
                summarySheet.UsedRange.AutofitColumns();
                detailsSheet.UsedRange.AutofitColumns();

                // Save the workbook
                workbook.SaveAs(filePath);
            }
        });
    }

    private async Task<int> ImportEnterpriseDataFromCsvAsync(string filePath)
    {
        // Parsing CSV (file I/O and string processing) is CPU/disk-bound; parse on background thread then persist.
        var parsed = await Task.Run(() =>
        {
            var list = new List<Enterprise>();

            using (var reader = new System.IO.StreamReader(filePath))
            {
                var headerLine = reader.ReadLine();
                if (string.IsNullOrEmpty(headerLine))
                    return list;

                var headers = headerLine.Split(',').Select(h => h.Trim('"')).ToArray();
                var nameIndex = Array.IndexOf(headers, "Name");
                var typeIndex = Array.IndexOf(headers, "Type");
                var rateIndex = Array.IndexOf(headers, "Current Rate");
                var expensesIndex = Array.IndexOf(headers, "Monthly Expenses");
                var citizensIndex = Array.IndexOf(headers, "Citizens Served");

                if (nameIndex == -1 || typeIndex == -1)
                    throw new InvalidOperationException("CSV must contain 'Name' and 'Type' columns");

                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var values = line.Split(',').Select(v => v.Trim('"')).ToArray();
                    if (values.Length <= Math.Max(nameIndex, typeIndex))
                        continue;

                    var enterprise = new Enterprise
                    {
                        Name = values[nameIndex],
                        Type = values[typeIndex],
                        CurrentRate = rateIndex >= 0 && values.Length > rateIndex && decimal.TryParse(values[rateIndex], out var rate) ? rate : 0,
                        MonthlyExpenses = expensesIndex >= 0 && values.Length > expensesIndex && decimal.TryParse(values[expensesIndex], out var expenses) ? expenses : 0,
                        CitizenCount = citizensIndex >= 0 && values.Length > citizensIndex && int.TryParse(values[citizensIndex], out var citizens) ? citizens : 1,
                        Status = EnterpriseStatus.Active,
                        TotalBudget = 0.00m,
                        Notes = "Imported from CSV"
                    };

                    if (!string.IsNullOrWhiteSpace(enterprise.Name) && !string.IsNullOrWhiteSpace(enterprise.Type))
                    {
                        list.Add(enterprise);
                    }
                }
            }

            return list;
        });

        foreach (var enterprise in parsed)
        {
            await _unitOfWork.Enterprises.AddAsync(enterprise);
        }

        return parsed.Count;
    }

    private async Task<int> ImportEnterpriseDataFromExcelAsync(string filePath)
    {
        // Parsing Excel with Syncfusion is CPU/disk-bound; parse on background thread then call async persistence.
        var parsed = await Task.Run(() =>
        {
            var list = new List<Enterprise>();

            using (var excelEngine = new Syncfusion.XlsIO.ExcelEngine())
            {
                var application = excelEngine.Excel;
                var workbook = application.Workbooks.Open(filePath);
                var worksheet = workbook.Worksheets[0];

                int headerRow = 1;
                while (headerRow <= worksheet.Rows.Length)
                {
                    if (!string.IsNullOrWhiteSpace(worksheet.Range[$"A{headerRow}"].Text))
                        break;
                    headerRow++;
                }

                if (headerRow > worksheet.Rows.Length)
                    return list;

                var headers = new Dictionary<string, int>();
                for (int col = 1; col <= worksheet.Columns.Length; col++)
                {
                    var headerText = worksheet.Range[headerRow, col].Text;
                    if (!string.IsNullOrWhiteSpace(headerText))
                        headers[headerText] = col;
                }

                if (!headers.ContainsKey("Name") || !headers.ContainsKey("Type"))
                    throw new InvalidOperationException("Excel file must contain 'Name' and 'Type' columns");

                for (int row = headerRow + 1; row <= worksheet.Rows.Length; row++)
                {
                    var name = worksheet.Range[row, headers["Name"]].Text;
                    var type = worksheet.Range[row, headers["Type"]].Text;

                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(type))
                        continue;

                    var enterprise = new Enterprise
                    {
                        Name = name,
                        Type = type,
                        CurrentRate = headers.ContainsKey("Current Rate") && decimal.TryParse(worksheet.Range[row, headers["Current Rate"]].Text, out var rate) ? rate : 0,
                        MonthlyExpenses = headers.ContainsKey("Monthly Expenses") && decimal.TryParse(worksheet.Range[row, headers["Monthly Expenses"]].Text, out var expenses) ? expenses : 0,
                        CitizenCount = headers.ContainsKey("Citizens Served") && int.TryParse(worksheet.Range[row, headers["Citizens Served"]].Text, out var citizens) ? citizens : 1,
                        Status = EnterpriseStatus.Active,
                        TotalBudget = 0.00m,
                        Notes = "Imported from Excel"
                    };

                    list.Add(enterprise);
                }
            }

            return list;
        });

        foreach (var enterprise in parsed)
        {
            await _unitOfWork.Enterprises.AddAsync(enterprise);
        }

        return parsed.Count;
    }

    // Duplicate method definitions removed during cleanup

    // Duplicate ExecuteExportSelectionAsync removed

    // Duplicate ExecuteWithRetryAsync/LoadEnterprisesAsync implementations removed (kept single source earlier in file)

    // --- remaining original members restored below ---

    /// <summary>
    /// Export to Excel advanced command
    /// </summary>
    private async Task ExecuteExportToExcelAdvancedAsync()
    {
        try
        {
            await NewMethod();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in Excel advanced export command");
        }
    }

    private async Task NewMethod()
    {
        StatusMessage = "Generating advanced Excel export...";

        // Get current enterprises data
        var enterprises = await _unitOfWork.Enterprises.GetAllAsync();
        var enterpriseList = enterprises.ToList();

        if (!enterpriseList.Any())
        {
            StatusMessage = "No enterprise data available for advanced Excel export";
            return;
        }

        // Create save file dialog
        var saveFileDialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save Advanced Excel Export",
            Filter = "Excel files (*.xlsx)|*.xlsx",
            DefaultExt = ".xlsx",
            FileName = $"Enterprise_Report_Advanced_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
        };

        if (saveFileDialog.ShowDialog() == true)
        {
            var filePath = saveFileDialog.FileName;

            // Generate advanced Excel export
            await GenerateEnterpriseAdvancedExcelExportAsync(enterpriseList, filePath);

            StatusMessage = $"Advanced Excel export saved to {System.IO.Path.GetFileName(filePath)}";
        }
        else
        {
            StatusMessage = "Advanced Excel export cancelled";
        }
    }

    /// <summary>
    /// Export to CSV command
    /// </summary>
    private async Task ExecuteExportToCsvAsync()
    {
        try
        {
            StatusMessage = "Generating CSV export...";

            // Get current enterprises data
            var enterprises = await _unitOfWork.Enterprises.GetAllAsync();
            var enterpriseList = enterprises.ToList();

            if (!enterpriseList.Any())
            {
                StatusMessage = "No enterprise data available for CSV export";
                return;
            }

            // Create save file dialog
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Save CSV Export",
                Filter = "CSV files (*.csv)|*.csv",
                DefaultExt = ".csv",
                FileName = $"Enterprise_Data_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                var filePath = saveFileDialog.FileName;

                // Generate CSV export
                await GenerateEnterpriseCsvExportAsync(enterpriseList, filePath);

                StatusMessage = $"CSV export saved to {System.IO.Path.GetFileName(filePath)}";
            }
            else
            {
                StatusMessage = "CSV export cancelled";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error generating CSV export: {ex.Message}";
            Log.Error(ex, "Error in CSV export command");
        }
    }

    /// <summary>
    /// Export selection command
    /// </summary>

    private async Task ExecuteExportSelectionAsync()
    {
        try
        {
            StatusMessage = "Generating selection export...";

            // Get selected enterprises or all if none selected
            var enterprisesToExport = SelectedEnterprise != null
                ? new List<Enterprise> { SelectedEnterprise }
                : EnterpriseList.ToList();

            if (!enterprisesToExport.Any())
            {
                StatusMessage = "No enterprises available for selection export";
                return;
            }

            // Create save file dialog
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Save Selection Export",
                Filter = "CSV files (*.csv)|*.csv|Excel files (*.xlsx)|*.xlsx",
                DefaultExt = ".csv",
                FileName = $"Enterprise_Selection_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                var filePath = saveFileDialog.FileName;
                var isCsv = System.IO.Path.GetExtension(filePath).ToLower(CultureInfo.InvariantCulture) == ".csv";

                if (isCsv)
                {
                    // Generate CSV export for selection
                    await GenerateEnterpriseCsvExportAsync(enterprisesToExport, filePath);
                    StatusMessage = $"Selection CSV export saved to {System.IO.Path.GetFileName(filePath)}";
                }
                else
                {
                    // Generate Excel export for selection
                    await GenerateEnterpriseAdvancedExcelExportAsync(enterprisesToExport, filePath);
                    StatusMessage = $"Selection Excel export saved to {System.IO.Path.GetFileName(filePath)}";
                }
            }
            else
            {
                StatusMessage = "Selection export cancelled";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error generating selection export: {ex.Message}";
            Log.Error(ex, "Error in selection export command");
        }
    }

    /// <summary>
    /// Executes an operation with retry logic and exponential backoff
    /// </summary>
    private async Task<T> ExecuteWithRetryAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        int maxRetries = 3,
        CancellationToken cancellationToken = default)
    {
        // Use Polly for resilient retry with exponential backoff
        var retryPolicy = Policy.Handle<Exception>(ex => !(ex is OperationCanceledException))
                                .WaitAndRetryAsync(maxRetries, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                                    (ex, timespan, retryCount, ctx) =>
                                    {
                                        Log.Warning(ex, "Retry {RetryCount} after {Delay}ms for operation", retryCount, timespan.TotalMilliseconds);
                                    });

        return await retryPolicy.ExecuteAsync(ct => operation(ct), cancellationToken);
    }

    /// <summary>
    /// Public async initializer to load data with resilience and populate UI collections.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (IsLoading)
            return;

        await _loadSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        IsLoading = true;

        try
        {
            var retryPolicy = Policy.Handle<Exception>(ex => !(ex is OperationCanceledException))
                                    .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                                        (ex, timespan, retryCount, ctx) => Log.Warning(ex, "InitializeAsync retry {Retry} after {Delay}ms", retryCount, timespan.TotalMilliseconds));

            var enterprises = await retryPolicy.ExecuteAsync(async ct => (await _unitOfWork.Enterprises.GetAllAsync()), cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            _dispatcherHelper?.Invoke(() =>
            {
                EnterpriseList.Clear();
                foreach (var e in enterprises)
                    EnterpriseList.Add(e);
            });

            // Populate type metrics
            var types = enterprises
                .GroupBy(e => e.Type ?? string.Empty)
                .Select(g => new WileyWidget.Models.EnterpriseTypeItem
                {
                    Type = g.Key,
                    Count = g.Count(),
                    TotalBudget = g.Sum(x => x.TotalBudget),
                    TotalRevenue = g.Sum(x => x.MonthlyRevenue),
                    AverageRate = g.Any() ? g.Average(x => x.CurrentRate) : 0
                }).ToList();

            _dispatcherHelper?.Invoke(() =>
            {
                EnterpriseTypes.Clear();
                foreach (var t in types) EnterpriseTypes.Add(t);
                PageCount = (int)Math.Ceiling((double)EnterpriseList.Count / PageSize);
            });

            if (_auditService != null)
            {
                await _auditService.AuditAsync("Enterprise.Load", new { Count = EnterpriseList.Count }).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to initialize enterprises: {ex.Message}";
            StatusMessage = "Error loading enterprise data";
            Log.Error(ex, "Error in InitializeAsync");
        }
        finally
        {
            IsLoading = false;
            _loadSemaphore.Release();
        }
    }

    /// <summary>
    /// Loads all enterprises from the database (public for View access)
    /// </summary>

    public async Task LoadEnterprisesAsync(CancellationToken cancellationToken = default)
    {
        // Prevent concurrent loading operations
        if (!await _loadSemaphore.WaitAsync(0, cancellationToken))
        {
            Log.Information("Enterprise loading already in progress, skipping duplicate request");
            return;
        }

        try
        {
            IsLoading = true;

            // Check for cancellation before starting
            cancellationToken.ThrowIfCancellationRequested();

            var enterprises = await ExecuteWithRetryAsync(
                async (ct) => await _unitOfWork.Enterprises.GetAllAsync(),
                cancellationToken: cancellationToken);

            // Check for cancellation before updating UI
            cancellationToken.ThrowIfCancellationRequested();

            EnterpriseList.Clear();
            foreach (var enterprise in enterprises)
            {
                // Check for cancellation during UI updates
                cancellationToken.ThrowIfCancellationRequested();
                EnterpriseList.Add(enterprise);
            }
        }
        catch (OperationCanceledException)
        {
            // Operation was cancelled, this is expected behavior
            Log.Information("Enterprise loading was cancelled");
        }
        catch (Exception ex)
        {
            // Provide proper error handling and user feedback
            ErrorMessage = $"Failed to load enterprises: {ex.Message}";
            StatusMessage = "Error loading enterprise data";
            Log.Error(ex, "Error loading enterprises from database");

            // Clear the list to show no data state
            EnterpriseList.Clear();
        }
        finally
        {
            IsLoading = false;
            _loadSemaphore.Release();
        }
    }

    // Commands
    public DelegateCommand LoadEnterprisesCommand { get; private set; }
    public DelegateCommand LoadEnterprisesAsyncCommand { get; private set; }
    public DelegateCommand SelectionChangedCommand { get; private set; }
    public DelegateCommand<int?> NavigateToDetailsCommand { get; private set; }
    public DelegateCommand NavigateToBudgetViewCommand { get; private set; }
    public DelegateCommand ExportToExcelCommand { get; private set; }
    public DelegateCommand ExportToPdfReportCommand { get; private set; }
    public DelegateCommand ExportToExcelAdvancedCommand { get; private set; }
    public DelegateCommand ExportToCsvCommand { get; private set; }
    public DelegateCommand ExportSelectionCommand { get; private set; }
    public DelegateCommand AddEnterpriseCommand { get; private set; }
    public DelegateCommand SaveEnterpriseCommand { get; private set; }
    public DelegateCommand DeleteEnterpriseCommand { get; private set; }
    public DelegateCommand UpdateBudgetSummaryCommand { get; private set; }
    public DelegateCommand BulkUpdateCommand { get; private set; }
    public DelegateCommand ClearFiltersCommand { get; private set; }
    public DelegateCommand ClearGroupingCommand { get; private set; }
    public DelegateCommand CopyToClipboardCommand { get; private set; }
    public DelegateCommand EditEnterpriseCommand { get; private set; }
    public DelegateCommand GenerateEnterpriseReportCommand { get; private set; }
    public DelegateCommand GroupByStatusCommand { get; private set; }
    public DelegateCommand GroupByTypeCommand { get; private set; }
    public DelegateCommand ImportDataCommand { get; private set; }

    // Async commands for XAML bindings
    public DelegateCommand LoadEnterprisesIncrementalAsyncCommand { get; private set; }
    public DelegateCommand AddEnterpriseAsyncCommand { get; private set; }
    public DelegateCommand SaveEnterpriseAsyncCommand { get; private set; }
    public DelegateCommand DeleteEnterpriseAsyncCommand { get; private set; }
    public DelegateCommand BulkUpdateAsyncCommand { get; private set; }

    /// <summary>
    /// Constructor with dependency injection
    /// </summary>
    public EnterpriseViewModel(IUnitOfWork unitOfWork, IEventAggregator eventAggregator, IReportExportService reportExportService, ICacheService? cacheService = null, IAuditService? auditService = null, Prism.Dialogs.IDialogService? dialogService = null)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));
        _reportExportService = reportExportService ?? throw new ArgumentNullException(nameof(reportExportService));
        _dispatcherHelper = new DispatcherHelper();
        _cacheService = cacheService;
        _auditService = auditService;
        _dialogService = dialogService;

        InitializeCommands();

        // Auto-load enterprises from cache or database to improve E2E readiness
        _ = Task.Run(async () =>
        {
            try
            {
                if (_cacheService != null)
                {
                    var cached = await _cacheService.GetAsync<System.Collections.Generic.List<Enterprise>>("enterprises");
                    if (cached != null && cached.Any())
                    {
                        _dispatcherHelper.Invoke(() =>
                        {
                            foreach (var e in cached) EnterpriseList.Add(e);
                        });
                        return;
                    }
                }

                var all = await _unitOfWork.Enterprises.GetAllAsync();
                var list = all?.ToList() ?? new System.Collections.Generic.List<Enterprise>();
                if (_cacheService != null && list.Any())
                    await _cacheService.SetAsync("enterprises", list, TimeSpan.FromHours(6));

                _dispatcherHelper.Invoke(() =>
                {
                    foreach (var e in list) EnterpriseList.Add(e);
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to auto-load enterprises in EnterpriseViewModel");
            }
        });
    }

    private void InitializeCommands()
    {
        LoadEnterprisesCommand = new DelegateCommand(async () => await LoadEnterprisesAsync(), () => !IsLoading);
        LoadEnterprisesAsyncCommand = new DelegateCommand(async () => await LoadEnterprisesAsync(), () => !IsLoading);
        SelectionChangedCommand = new DelegateCommand(ExecuteSelectionChanged);
        NavigateToDetailsCommand = new DelegateCommand<int?>(ExecuteNavigateToDetails);
        NavigateToBudgetViewCommand = new DelegateCommand(ExecuteNavigateToBudgetView);
        ExportToExcelCommand = new DelegateCommand(async () => await ExecuteExportToExcelAsync(), () => !IsLoading);
        ExportToPdfReportCommand = new DelegateCommand(async () => await ExecuteExportToPdfReportAsync(), () => !IsLoading);
        ExportToExcelAdvancedCommand = new DelegateCommand(async () => await ExecuteExportToExcelAdvancedAsync(), () => !IsLoading);
        ExportToCsvCommand = new DelegateCommand(async () => await ExecuteExportToCsvAsync(), () => !IsLoading);
        ExportSelectionCommand = new DelegateCommand(async () => await ExecuteExportSelectionAsync(), () => !IsLoading);
        // NOTE: unit tests call ICommand.Execute() synchronously and often don't await the async handler.
        // To keep behavior deterministic for tests (and avoid flakiness where SaveChangesAsync may not be
        // observed before the assertion), run the async handlers synchronously from the command Execute
        // path by blocking on the task completion. This keeps the public ICommand API unchanged while
        // ensuring repository SaveChangesAsync is invoked before Execute() returns. Use of
        // .GetAwaiter().GetResult() is deliberate here to avoid AggregateException wrapping.
        AddEnterpriseCommand = new DelegateCommand(() => ExecuteAddEnterpriseAsync().GetAwaiter().GetResult(), () => !IsLoading);
        SaveEnterpriseCommand = new DelegateCommand(() => ExecuteSaveEnterpriseAsync().GetAwaiter().GetResult(),
            () => !IsLoading && SelectedEnterprise != null && !string.IsNullOrWhiteSpace(SelectedEnterprise?.Name));
        DeleteEnterpriseCommand = new DelegateCommand(() => ExecuteDeleteEnterpriseAsync().GetAwaiter().GetResult(), () => !IsLoading && SelectedEnterprise != null);
        UpdateBudgetSummaryCommand = new DelegateCommand(ExecuteUpdateBudgetSummary);
        BulkUpdateCommand = new DelegateCommand(async () => await ExecuteBulkUpdateAsync(), () => !IsLoading);
        ClearFiltersCommand = new DelegateCommand(ExecuteClearFilters);
        ClearGroupingCommand = new DelegateCommand(ExecuteClearGrouping);
        CopyToClipboardCommand = new DelegateCommand(ExecuteCopyToClipboard);
        EditEnterpriseCommand = new DelegateCommand(ExecuteEditEnterprise, () => SelectedEnterprise != null);
        GenerateEnterpriseReportCommand = new DelegateCommand(async () => await ExecuteGenerateEnterpriseReport(), () => !IsLoading);
        GroupByStatusCommand = new DelegateCommand(ExecuteGroupByStatus);
        GroupByTypeCommand = new DelegateCommand(ExecuteGroupByType);
        ImportDataCommand = new DelegateCommand(async () => await ExecuteImportData(), () => !IsLoading);

        // Initialize async commands
        LoadEnterprisesIncrementalAsyncCommand = new DelegateCommand(async () => await LoadEnterprisesIncrementalAsync(), () => !IsLoading);
        AddEnterpriseAsyncCommand = new DelegateCommand(async () => await ExecuteAddEnterpriseAsync(), () => !IsLoading);
        SaveEnterpriseAsyncCommand = new DelegateCommand(async () => await ExecuteSaveEnterpriseAsync(), () => !IsLoading && SelectedEnterprise != null && !string.IsNullOrWhiteSpace(SelectedEnterprise?.Name));
        DeleteEnterpriseAsyncCommand = new DelegateCommand(async () => await ExecuteDeleteEnterpriseAsync(), () => !IsLoading && SelectedEnterprise != null);
        BulkUpdateAsyncCommand = new DelegateCommand(async () => await ExecuteBulkUpdateAsync(), () => !IsLoading);
    }

    /// <summary>
    /// Adds a new enterprise
    /// </summary>

    private async Task ExecuteAddEnterpriseAsync()
    {
        ErrorMessage = string.Empty;

        try
        {
            IsLoading = true;
            StatusMessage = "Creating new enterprise...";

            var newEnterprise = new Enterprise
            {
                Name = "New Enterprise",
                CurrentRate = 5.00m,
                MonthlyExpenses = 0.00m,
                CitizenCount = 1,
                Status = EnterpriseStatus.Active,
                TotalBudget = 0.00m,
                Type = "Utility",
                Notes = "New enterprise - update details"
            };

            var addedEnterprise = await _unitOfWork.Enterprises.AddAsync(newEnterprise);
            // Persist the new enterprise
            await _unitOfWork.SaveChangesAsync();

            EnterpriseList.Add(addedEnterprise);
            SelectedEnterprise = addedEnterprise;

            StatusMessage = $"Enterprise '{addedEnterprise.Name}' created successfully";
            Log.Information("Created new enterprise with ID: {EnterpriseId}", addedEnterprise.Id);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to create enterprise: {ex.Message}";
            StatusMessage = "Error creating enterprise";
            Log.Error(ex, "Error adding enterprise");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Saves changes to the selected enterprise
    /// </summary>

    private async Task ExecuteSaveEnterpriseAsync()
    {
        if (SelectedEnterprise == null)
        {
            StatusMessage = "No enterprise selected";
            return;
        }

        // Validate before saving
        var validationError = this[nameof(SelectedEnterprise.Name)] ??
                            this[nameof(SelectedEnterprise.CurrentRate)] ??
                            this[nameof(SelectedEnterprise.CitizenCount)];

        if (!string.IsNullOrEmpty(validationError))
        {
            ErrorMessage = $"Validation failed: {validationError}";
            StatusMessage = "Cannot save: validation errors";
            return;
        }

        ErrorMessage = string.Empty;

        try
        {
            IsLoading = true;
            StatusMessage = $"Saving '{SelectedEnterprise.Name}'...";

            // If this is a new enterprise (Id == 0), call AddAsync; otherwise update
            if (SelectedEnterprise.Id == 0)
            {
                await _unitOfWork.Enterprises.AddAsync(SelectedEnterprise);
            }
            else
            {
                await _unitOfWork.Enterprises.UpdateAsync(SelectedEnterprise);
            }

            // Persist changes
            await _unitOfWork.SaveChangesAsync();

            StatusMessage = $"Enterprise '{SelectedEnterprise.Name}' saved successfully";
            Log.Information("Saved enterprise with ID: {EnterpriseId}", SelectedEnterprise.Id);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to save enterprise: {ex.Message}";
            StatusMessage = "Error saving enterprise";
            Log.Error(ex, "Error saving enterprise {EnterpriseId}", SelectedEnterprise?.Id);
        }
        finally
        {
            IsLoading = false;
        }
    }    /// <summary>
    /// Deletes the selected enterprise
    /// </summary>

    private async Task ExecuteDeleteEnterpriseAsync()
    {
        if (SelectedEnterprise == null)
        {
            StatusMessage = "No enterprise selected";
            return;
        }

        ErrorMessage = string.Empty;
        var enterpriseName = SelectedEnterprise.Name;
        var enterpriseId = SelectedEnterprise.Id;

        // Show confirmation dialog
        if (_dialogService != null)
        {
            var dialogResult = await ShowConfirmationDialogAsync($"Delete Enterprise '{enterpriseName}'?",
                $"Are you sure you want to delete the enterprise '{enterpriseName}'?\n\nThis action cannot be undone.");

            if (dialogResult != Prism.Dialogs.ButtonResult.Yes)
            {
                StatusMessage = "Delete operation cancelled";
                return;
            }
        }
        else
        {
            // Fallback to MessageBox
            var result = System.Windows.MessageBox.Show(
                $"Are you sure you want to delete the enterprise '{enterpriseName}'?\n\nThis action cannot be undone.",
                "Confirm Delete",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result != System.Windows.MessageBoxResult.Yes)
            {
                StatusMessage = "Delete operation cancelled";
                return;
            }
        }

        try
        {
            IsLoading = true;
            StatusMessage = $"Deleting '{enterpriseName}'...";

            var success = await _unitOfWork.Enterprises.DeleteAsync(enterpriseId);

            if (success)
            {
                // Persist deletion
                await _unitOfWork.SaveChangesAsync();

                // Audit logging
                if (_auditService != null)
                {
                    await _auditService.AuditAsync("Enterprise.Delete", new
                    {
                        EnterpriseId = enterpriseId,
                        EnterpriseName = enterpriseName,
                        DeletedBy = Environment.UserName,
                        Timestamp = DateTime.UtcNow
                    });
                }

                EnterpriseList.Remove(SelectedEnterprise);
                SelectedEnterprise = EnterpriseList.FirstOrDefault();
                StatusMessage = $"Enterprise '{enterpriseName}' deleted successfully";
                Log.Information("Deleted enterprise with ID: {EnterpriseId}", enterpriseId);
            }
            else
            {
                ErrorMessage = $"Failed to delete enterprise '{enterpriseName}'";
                StatusMessage = "Delete operation failed";
                Log.Warning("Delete operation returned false for enterprise ID: {EnterpriseId}", enterpriseId);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to delete enterprise: {ex.Message}";
            StatusMessage = "Error deleting enterprise";
            Log.Error(ex, "Error deleting enterprise {EnterpriseId}", enterpriseId);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Calculates and displays budget summary
    /// </summary>

    private void ExecuteUpdateBudgetSummary()
    {
        BudgetSummaryText = GetBudgetSummary();
    }

    /// <summary>
    /// Bulk update enterprises
    /// </summary>

    private async Task ExecuteBulkUpdateAsync()
    {
        try
        {
            if (!EnterpriseList.Any())
            {
                StatusMessage = "No enterprises available for bulk update";
                return;
            }

            // Show field selection dialog
            var fieldSelection = System.Windows.MessageBox.Show(
                "Select field to update for all enterprises:\n\n" +
                "Yes = Update Status to Active\n" +
                "No = Update Type to Utility\n" +
                "Cancel = Abort operation",
                "Bulk Update Field Selection",
                System.Windows.MessageBoxButton.YesNoCancel,
                System.Windows.MessageBoxImage.Question);

            if (fieldSelection == System.Windows.MessageBoxResult.Cancel)
            {
                StatusMessage = "Bulk update cancelled";
                return;
            }

            // Show confirmation dialog
            var result = System.Windows.MessageBox.Show(
                $"Update {EnterpriseList.Count} enterprises?\n\n" +
                $"Field: {(fieldSelection == System.Windows.MessageBoxResult.Yes ? "Status" : "Type")}\n" +
                $"New Value: {(fieldSelection == System.Windows.MessageBoxResult.Yes ? "Active" : "Utility")}",
                "Bulk Update Confirmation",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result != System.Windows.MessageBoxResult.Yes)
            {
                StatusMessage = "Bulk update cancelled";
                return;
            }

            IsLoading = true;
            StatusMessage = "Performing bulk update...";

            // Perform the bulk update
            var updateCount = 0;
            foreach (var enterprise in EnterpriseList)
            {
                var updated = false;

                if (fieldSelection == System.Windows.MessageBoxResult.Yes)
                {
                    // Update status to Active
                    if (enterprise.Status != EnterpriseStatus.Active)
                    {
                        enterprise.Status = EnterpriseStatus.Active;
                        updated = true;
                    }
                }
                else
                {
                    // Update type to Utility
                    if (enterprise.Type != "Utility")
                    {
                        enterprise.Type = "Utility";
                        updated = true;
                    }
                }

                if (updated)
                {
                    // Save individual enterprise changes
                    await _unitOfWork.Enterprises.UpdateAsync(enterprise);
                    updateCount++;
                }
            }

            // Commit all changes
            await _unitOfWork.SaveChangesAsync();

            StatusMessage = $"Bulk update completed: {updateCount} enterprises updated";

            // Refresh the list to show changes
            await LoadEnterprisesAsync();

            Log.Information("Bulk update completed: {UpdateCount} enterprises updated", updateCount);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error in bulk update: {ex.Message}";
            Log.Error(ex, "Error in bulk update command");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Clear filters
    /// </summary>

    private void ExecuteClearFilters()
    {
        SearchText = string.Empty;
        SelectedStatusFilter = "All";
        StatusMessage = "Filters cleared";
    }

    /// <summary>
    /// Clear grouping
    /// </summary>

    private void ExecuteClearGrouping()
    {
        _eventAggregator.GetEvent<GroupingMessage>().Publish(new GroupingMessage
        {
            Operation = GroupingOperation.Clear
        });
        StatusMessage = "Grouping cleared";
    }

    /// <summary>
    /// Copy to clipboard
    /// </summary>

    private void ExecuteCopyToClipboard()
    {
        if (SelectedEnterprise == null)
        {
            StatusMessage = "No enterprise selected to copy";
            return;
        }

        try
        {
            var clipboardText = $"Enterprise: {SelectedEnterprise.Name}\n" +
                               $"Type: {SelectedEnterprise.Type}\n" +
                               $"Status: {SelectedEnterprise.Status}\n" +
                               $"Citizens: {SelectedEnterprise.CitizenCount}\n" +
                               $"Rate: {SelectedEnterprise.CurrentRate:C}\n" +
                               $"Monthly Revenue: {SelectedEnterprise.MonthlyRevenue:C}\n" +
                               $"Monthly Expenses: {SelectedEnterprise.MonthlyExpenses:C}\n" +
                               $"Monthly Balance: {SelectedEnterprise.MonthlyBalance:C}\n" +
                               $"Last Updated: {SelectedEnterprise.LastUpdated:g}";

            System.Windows.Clipboard.SetText(clipboardText);
            StatusMessage = $"Enterprise '{SelectedEnterprise.Name}' copied to clipboard";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to copy to clipboard: {ex.Message}";
            Log.Error(ex, "Failed to copy enterprise to clipboard");
        }
    }

    /// <summary>
    /// Edit enterprise
    /// </summary>

    private void ExecuteEditEnterprise()
    {
        if (SelectedEnterprise != null)
        {
            StatusMessage = $"Editing {SelectedEnterprise.Name}";
        }
    }

    /// <summary>
    /// Generate enterprise report
    /// </summary>

    private async Task ExecuteGenerateEnterpriseReport()
    {
        try
        {
            StatusMessage = "Generating enterprise report...";

            if (!EnterpriseList.Any())
            {
                StatusMessage = "No enterprise data available for report generation";
                return;
            }

            // Create save file dialog
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Save Enterprise Report",
                Filter = "PDF files (*.pdf)|*.pdf|Excel files (*.xlsx)|*.xlsx",
                DefaultExt = ".pdf",
                FileName = $"Enterprise_Comprehensive_Report_{DateTime.Now:yyyyMMdd_HHmmss}.pdf"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                var filePath = saveFileDialog.FileName;
                var isPdf = System.IO.Path.GetExtension(filePath).ToLower(CultureInfo.InvariantCulture) == ".pdf";

                if (isPdf)
                {
                    // Generate comprehensive PDF report
                    await GenerateEnterpriseComprehensiveReportAsync(EnterpriseList, filePath);
                    StatusMessage = $"Comprehensive PDF report saved to {System.IO.Path.GetFileName(filePath)}";
                }
                else
                {
                    // Generate comprehensive Excel report
                    await GenerateEnterpriseComprehensiveExcelReportAsync(EnterpriseList, filePath);
                    StatusMessage = $"Comprehensive Excel report saved to {System.IO.Path.GetFileName(filePath)}";
                }
            }
            else
            {
                StatusMessage = "Report generation cancelled";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error generating enterprise report: {ex.Message}";
            Log.Error(ex, "Error in enterprise report generation");
        }
    }

    /// <summary>
    /// Group by status
    /// </summary>

    private void ExecuteGroupByStatus()
    {
        _eventAggregator.GetEvent<GroupingMessage>().Publish(new GroupingMessage
        {
            Operation = GroupingOperation.GroupByColumn,
            ColumnName = "Status"
        });
        StatusMessage = "Grouped by status";
    }

    /// <summary>
    /// Group by type
    /// </summary>

    private void ExecuteGroupByType()
    {
        _eventAggregator.GetEvent<GroupingMessage>().Publish(new GroupingMessage
        {
            Operation = GroupingOperation.GroupByColumn,
            ColumnName = "Type"
        });
        StatusMessage = "Grouped by type";
    }

    /// <summary>
    /// Import data
    /// </summary>

    private async Task ExecuteImportData()
    {
        try
        {
            StatusMessage = "Importing enterprise data...";

            // Create open file dialog
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Import Enterprise Data",
                Filter = "CSV files (*.csv)|*.csv|Excel files (*.xlsx)|*.xlsx",
                DefaultExt = ".csv"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var filePath = openFileDialog.FileName;
                var isCsv = System.IO.Path.GetExtension(filePath).ToLower(CultureInfo.InvariantCulture) == ".csv";

                int importedCount = 0;
                if (isCsv)
                {
                    importedCount = await ImportEnterpriseDataFromCsvAsync(filePath);
                }
                else
                {
                    importedCount = await ImportEnterpriseDataFromExcelAsync(filePath);
                }

                StatusMessage = $"Successfully imported {importedCount} enterprises from {System.IO.Path.GetFileName(filePath)}";

                // Refresh the enterprise list
                await LoadEnterprisesAsync();
            }
            else
            {
                StatusMessage = "Data import cancelled";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error importing data: {ex.Message}";
            Log.Error(ex, "Error in data import");
        }
    }

    /// <summary>
    /// Load enterprises incrementally
    /// </summary>

    private async Task LoadEnterprisesIncrementalAsync()
    {
        try
        {
            StatusMessage = "Incremental loading feature - loading all enterprises...";

            // For now, reload all enterprises (simulating incremental load)
            // In a real implementation, this would load the next page
            await LoadEnterprisesAsync();

            StatusMessage = $"Loaded {EnterpriseList.Count} enterprises incrementally";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error in incremental loading: {ex.Message}";
            Log.Error(ex, "Error in incremental enterprise loading");
        }
    }

    /// <summary>
    /// Rate analysis
    /// </summary>

    private void RateAnalysis()
    {
        if (EnterpriseList.Count == 0)
        {
            StatusMessage = "No enterprises available for rate analysis";
            return;
        }

        try
        {
            var rates = EnterpriseList.Where(e => e.CurrentRate > 0).Select(e => e.CurrentRate).ToList();
            if (rates.Count == 0)
            {
                StatusMessage = "No valid rates found for analysis";
                return;
            }

            var avgRate = rates.Average();
            var minRate = rates.Min();
            var maxRate = rates.Max();
            var medianRate = rates.OrderBy(r => r).ElementAt(rates.Count / 2);

            var analysis = $"Rate Analysis:\n" +
                          $"Average Rate: {avgRate:C}\n" +
                          $"Median Rate: {medianRate:C}\n" +
                          $"Lowest Rate: {minRate:C}\n" +
                          $"Highest Rate: {maxRate:C}\n" +
                          $"Enterprises Analyzed: {rates.Count}";

            StatusMessage = analysis;
            Log.Information("Rate analysis completed: Avg={Avg:C}, Min={Min:C}, Max={Max:C}", avgRate, minRate, maxRate);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Rate analysis failed: {ex.Message}";
            Log.Error(ex, "Failed to perform rate analysis");
        }
    }

    /// <summary>
    /// View audit history
    /// </summary>

    private async Task ViewAuditHistoryAsync()
    {
        if (SelectedEnterprise == null)
        {
            StatusMessage = "No enterprise selected to view audit history";
            return;
        }

        try
        {
            // Get audit entries for the last 30 days
            var endDate = DateTime.UtcNow;
            var startDate = endDate.AddDays(-30);

            var auditEntries = await _unitOfWork.Audits.GetAuditTrailForEntityAsync("Enterprise", SelectedEnterprise.Id, startDate, endDate);

            if (auditEntries.Any())
            {
                var history = string.Join("\n", auditEntries
                    .OrderByDescending(a => a.Timestamp)
                    .Take(10) // Show last 10 entries
                    .Select(a => $"{a.Timestamp:g} - {a.Action} by {a.User}: {a.Changes ?? "No details"}"));

                StatusMessage = $"Audit History for '{SelectedEnterprise.Name}' (last 30 days):\n{history}";
            }
            else
            {
                StatusMessage = $"No audit history found for '{SelectedEnterprise.Name}' in the last 30 days";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load audit history: {ex.Message}";
            Log.Error(ex, "Failed to load audit history for enterprise {EnterpriseId}", SelectedEnterprise.Id);
        }
    }

    /// <summary>
    /// Show advanced filter
    /// </summary>

    private void ShowAdvancedFilter()
    {
        try
        {
            // For now, show available filter options in status message
            // In a full implementation, this would open a filter dialog
            var filterOptions = "Advanced Filter Options:\n" +
                               "• Filter by Budget Range: Min/Max monthly revenue\n" +
                               "• Filter by Date Range: Last updated within period\n" +
                               "• Filter by Performance: Positive/negative balance\n" +
                               "• Custom Criteria: Combine multiple conditions\n\n" +
                               "Feature will open a dedicated filter dialog in the next update.";

            StatusMessage = filterOptions;
            Log.Information("Advanced filter options displayed to user");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to show advanced filter: {ex.Message}";
            Log.Error(ex, "Failed to show advanced filter options");
        }
    }

    /// <summary>
    /// Show tree map
    /// </summary>

    private void ShowTreeMap()
    {
        try
        {
            if (EnterpriseList.Count == 0)
            {
                StatusMessage = "No enterprise data available for tree map visualization";
                return;
            }

            // Calculate enterprise sizes by revenue for tree map
            var totalRevenue = EnterpriseList.Sum(e => Math.Abs(e.MonthlyRevenue));
            var treeMapData = EnterpriseList
                .OrderByDescending(e => Math.Abs(e.MonthlyRevenue))
                .Take(10) // Top 10 for readability
                .Select(e => new
                {
                    Name = e.Name,
                    Revenue = e.MonthlyRevenue,
                    Percentage = totalRevenue > 0 ? (Math.Abs(e.MonthlyRevenue) / totalRevenue) * 100 : 0
                });

            var treeMapText = "Enterprise Revenue Tree Map (Top 10):\n" +
                             string.Join("\n", treeMapData.Select(e =>
                                 $"{e.Name}: {e.Revenue:C} ({e.Percentage:F1}%)"));

            StatusMessage = treeMapText;
            Log.Information("Tree map visualization displayed for {Count} enterprises", treeMapData.Count());
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to generate tree map: {ex.Message}";
            Log.Error(ex, "Failed to generate enterprise tree map");
        }
    }

    /// <summary>
    /// Show tree view
    /// </summary>

    private void ShowTreeView()
    {
        try
        {
            if (EnterpriseList.Count == 0)
            {
                StatusMessage = "No enterprise data available for tree view";
                return;
            }

            // Group enterprises by type for hierarchical display
            var groupedByType = EnterpriseList
                .GroupBy(e => e.Type)
                .OrderBy(g => g.Key);

            var treeViewText = "Enterprise Hierarchy by Type:\n";
            foreach (var group in groupedByType)
            {
                treeViewText += $"\n📁 {group.Key} ({group.Count()} enterprises)\n";
                foreach (var enterprise in group.OrderBy(e => e.Name))
                {
                    var statusIcon = enterprise.Status.ToString().ToLower(CultureInfo.InvariantCulture) switch
                    {
                        "active" => "🟢",
                        "inactive" => "🔴",
                        "pending" => "🟡",
                        _ => "⚪"
                    };
                    treeViewText += $"  └── {statusIcon} {enterprise.Name} ({enterprise.CitizenCount} citizens, {enterprise.MonthlyBalance:C})\n";
                }
            }

            StatusMessage = treeViewText;
            Log.Information("Tree view displayed with {GroupCount} enterprise groups", groupedByType.Count());
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to generate tree view: {ex.Message}";
            Log.Error(ex, "Failed to generate enterprise tree view");
        }
    }

    /// <summary>
    /// Calculates and displays budget summary
    /// </summary>
    public string GetBudgetSummary()
    {
        if (!EnterpriseList.Any())
            return "No enterprises loaded";

        var totalRevenue = EnterpriseList.Sum(e => e.MonthlyRevenue);
        var totalExpenses = EnterpriseList.Sum(e => e.MonthlyExpenses);
        var totalBalance = totalRevenue - totalExpenses;
        var totalCitizens = EnterpriseList.Sum(e => e.CitizenCount);

        return $"Total Revenue: ${totalRevenue.ToString("N2", CultureInfo.InvariantCulture)}\n" +
               $"Total Expenses: ${totalExpenses.ToString("N2", CultureInfo.InvariantCulture)}\n" +
               $"Monthly Balance: ${totalBalance.ToString("N2", CultureInfo.InvariantCulture)}\n" +
               $"Citizens Served: {totalCitizens}\n" +
               $"Status: {(totalBalance >= 0 ? "Surplus" : "Deficit")}";
    }

    /// <summary>
    /// IDataErrorInfo implementation - validation stubs
    /// </summary>
    public string Error
    {
        get
        {
            if (SelectedEnterprise == null) return null;

            var errors = new System.Collections.Generic.List<string>();

            if (string.IsNullOrWhiteSpace(SelectedEnterprise.Name))
                errors.Add("Name is required");

            if (SelectedEnterprise.CurrentRate <= 0)
                errors.Add("Rate must be greater than 0");

            if (SelectedEnterprise.MonthlyExpenses < 0)
                errors.Add("Expenses cannot be negative");

            if (SelectedEnterprise.CitizenCount < 0)
                errors.Add("Citizen count cannot be negative");

            if (SelectedEnterprise.TotalBudget < 0)
                errors.Add("Budget cannot be negative");

            return errors.Count > 0 ? string.Join("; ", errors) : null;
        }
    }

    /// <summary>
    /// IDataErrorInfo implementation - property-level validation
    /// </summary>
    public string this[string columnName]
    {
        get
        {
            if (SelectedEnterprise == null) return null;

            return columnName switch
            {
                nameof(SelectedEnterprise.Name) =>
                    string.IsNullOrWhiteSpace(SelectedEnterprise.Name)
                        ? "Name is required"
                        : SelectedEnterprise.Name.Length > 100
                            ? "Name cannot exceed 100 characters"
                            : null,

                nameof(SelectedEnterprise.CurrentRate) =>
                    SelectedEnterprise.CurrentRate <= 0
                        ? "Rate must be greater than 0"
                        : SelectedEnterprise.CurrentRate > 9999.99m
                            ? "Rate cannot exceed $9,999.99"
                            : null,

                nameof(SelectedEnterprise.MonthlyExpenses) =>
                    SelectedEnterprise.MonthlyExpenses < 0
                        ? "Expenses cannot be negative"
                        : null,

                nameof(SelectedEnterprise.CitizenCount) =>
                    SelectedEnterprise.CitizenCount < 0
                        ? "Citizen count cannot be negative"
                        : SelectedEnterprise.CitizenCount < 1
                            ? "At least one citizen must be served"
                            : null,

                nameof(SelectedEnterprise.TotalBudget) =>
                    SelectedEnterprise.TotalBudget < 0
                        ? "Budget cannot be negative"
                        : null,

                "SelectedEnterprise.Name" =>
                    string.IsNullOrWhiteSpace(SelectedEnterprise?.Name)
                        ? "Name is required"
                        : null,

                "SelectedEnterprise.CurrentRate" =>
                    SelectedEnterprise?.CurrentRate < 0
                        ? "Rate cannot be negative"
                        : null,

                "SelectedEnterprise.MonthlyExpenses" =>
                    SelectedEnterprise?.MonthlyExpenses < 0
                        ? "Expenses cannot be negative"
                        : null,

                "SelectedEnterprise.CitizenCount" =>
                    SelectedEnterprise?.CitizenCount < 0
                        ? "Citizen count cannot be negative"
                        : null,

                _ => null
            };
        }
    }

    /// <summary>
    /// Disposes of managed resources
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes of managed and unmanaged resources
    /// </summary>
    /// <param name="disposing">True if called from Dispose(), false if called from finalizer</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _loadSemaphore?.Dispose();
        }
    }

    public void OnNavigatedFrom(NavigationContext navigationContext)
    {
        // Navigation cleanup handled in Dispose
    }

    // Prism Navigation Implementation
    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        Log.Information("EnterpriseViewModel navigated to");

        // Handle navigation parameters
        if (navigationContext?.Parameters != null)
        {
            // Check for enterprise ID parameter to select specific enterprise
            if (navigationContext.Parameters.ContainsKey("enterpriseId"))
            {
                var enterpriseId = navigationContext.Parameters["enterpriseId"];
                if (enterpriseId is int id)
                {
                    // Select the enterprise with the specified ID
                    var enterprise = EnterpriseList.FirstOrDefault(e => e.Id == id);
                    if (enterprise != null)
                    {
                        SelectedEnterprise = enterprise;
                    }
                }
            }
        }

        // Load enterprises if not already loaded
        _ = LoadEnterprisesAsync();
    }



    public bool IsNavigationTarget(NavigationContext navigationContext)
    {
        // Always allow navigation to enterprises view
        return true;
    }

    // Event Handlers for EventAggregator
    private void OnEnterpriseChanged(EnterpriseChangedMessage message)
    {
        Log.Information("Enterprise changed notification received: {EnterpriseName} ({ChangeType})",
            message.EnterpriseName, message.ChangeType);

        // Refresh enterprise list when changes occur
        _ = LoadEnterprisesAsync();
    }

    /// <summary>
    /// Shows a confirmation dialog with the specified title and message
    /// </summary>
    protected virtual async Task<Prism.Dialogs.ButtonResult> ShowConfirmationDialogAsync(string title, string message)
    {
        if (_dialogService == null)
        {
            Log.Warning("IDialogService is not available in ShowConfirmationDialogAsync, returning ButtonResult.None");
            return Prism.Dialogs.ButtonResult.None;
        }

        var dialogParams = new Prism.Dialogs.DialogParameters
        {
            { "Title", title },
            { "Message", message },
            { "Buttons", "YesNo" }
        };

        var tcs = new TaskCompletionSource<Prism.Dialogs.ButtonResult>();

        _dialogService.ShowDialog("ConfirmationDialog", dialogParams, result => tcs.SetResult(result?.Result ?? Prism.Dialogs.ButtonResult.None));

        return await tcs.Task;
    }
}
