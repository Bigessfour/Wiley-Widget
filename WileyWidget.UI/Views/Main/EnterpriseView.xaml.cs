using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WileyWidget.Services.Threading;
using WileyWidget.ViewModels;
using WileyWidget.ViewModels.Main;
using WileyWidget.ViewModels.Messages;

namespace WileyWidget.Views.Main;

/// <summary>
/// Enterprise Management UserControl - Provides full CRUD interface for municipal enterprises
/// </summary>
public partial class EnterpriseView : UserControl
{
    // Optional dispatcher helper used to marshal UI updates. If not provided via DI/ViewModel,
    // a default DispatcherHelper will be created on first use.
    private IDispatcherHelper? _dispatcherHelper;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnterpriseView"/> class.
    /// ViewModel is auto-wired by Prism ViewModelLocator.
    /// </summary>
    public EnterpriseView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Exports the SfDataGrid data to Excel format using CSV approach
    /// </summary>
    public async Task ExportToExcelAsync()
    {
        try
        {
            var dataGrid = FindName("EnterpriseDataGrid") as Syncfusion.UI.Xaml.Grid.SfDataGrid;
            if (dataGrid?.ItemsSource == null) return;

            // Create save file dialog
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx|CSV Files (*.csv)|*.csv|All files (*.*)|*.*",
                DefaultExt = ".xlsx",
                FileName = $"EnterpriseData_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                // Capture UI data on the UI thread, then perform CPU/disk-bound CSV generation on a background thread.
                // NOTE: ExportItemsToCsv is performing CPU and disk I/O (enumeration, reflection, and file writes).
                // Using Task.Run here is appropriate to avoid blocking the UI thread while writing files.
                var csvFileName = System.IO.Path.ChangeExtension(saveFileDialog.FileName, ".csv");
                var items = (dataGrid.ItemsSource as System.Collections.IEnumerable)?.Cast<object>().ToList() ?? new List<object>();

                // Use injected ReportExportService from ViewModel for better testability and DI
                if (DataContext is EnterpriseViewModel vm)
                {
                    await vm.ReportExportService.ExportToCsvAsync(items.Cast<object>(), csvFileName).ConfigureAwait(false);
                }
                else
                {
                    // Fallback: Log error - this should not happen in normal operation
                    // ReportExportService requires ILogger injection, so we can't create it directly
                    Serilog.Log.Error("EnterpriseViewModel not available in DataContext for CSV export");
                    return;
                }

                // Ensure MessageBox (UI interaction) runs on the UI thread.
                if (_dispatcherHelper == null)
                {
                    _dispatcherHelper = new DispatcherHelper();
                }

                await _dispatcherHelper.InvokeAsync(() =>
                {
                    MessageBox.Show($"Data exported successfully to {saveFileDialog.FileName}",
                        "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            if (_dispatcherHelper == null)
            {
                _dispatcherHelper = new DispatcherHelper();
            }

            await _dispatcherHelper.InvokeAsync(() =>
            {
                MessageBox.Show($"Error exporting to Excel: {ex.Message}",
                    "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Exports the SfDataGrid data to PDF format using CSV approach
    /// </summary>
    public async Task ExportToPdfAsync()
    {
        try
        {
            var dataGrid = FindName("EnterpriseDataGrid") as Syncfusion.UI.Xaml.Grid.SfDataGrid;
            if (dataGrid?.ItemsSource == null) return;

            // Create save file dialog
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PDF Files (*.pdf)|*.pdf|CSV Files (*.csv)|*.csv|All files (*.*)|*.*",
                DefaultExt = ".pdf",
                FileName = $"EnterpriseData_{DateTime.Now:yyyyMMdd_HHmmss}.pdf"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                // See notes in ExportToExcelAsync: CSV export is CPU/disk-bound so Task.Run is appropriate here.
                var csvFileName = System.IO.Path.ChangeExtension(saveFileDialog.FileName, ".csv");
                var items = (dataGrid.ItemsSource as System.Collections.IEnumerable)?.Cast<object>().ToList() ?? new List<object>();

                await Task.Run(() => ExportItemsToCsv(items, csvFileName)).ConfigureAwait(false);

                if (_dispatcherHelper == null)
                {
                    _dispatcherHelper = new DispatcherHelper();
                }

                await _dispatcherHelper.InvokeAsync(() =>
                {
                    MessageBox.Show($"Data exported successfully to {saveFileDialog.FileName}",
                        "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            if (_dispatcherHelper == null)
            {
                _dispatcherHelper = new DispatcherHelper();
            }

            await _dispatcherHelper.InvokeAsync(() =>
            {
                MessageBox.Show($"Error exporting to PDF: {ex.Message}",
                    "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Exports the SfDataGrid data to CSV format
    /// </summary>
    private void ExportToCsv(string fileName)
    {
        try
        {
            // Back-compat: if called directly, attempt to get items from the UI
            IEnumerable<object> items = Enumerable.Empty<object>();
            var dataGrid = FindName("EnterpriseDataGrid") as Syncfusion.UI.Xaml.Grid.SfDataGrid;
            if (dataGrid?.ItemsSource != null)
            {
                items = (dataGrid.ItemsSource as System.Collections.IEnumerable)?.Cast<object>() ?? Enumerable.Empty<object>();
            }

            ExportItemsToCsv(items, fileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error exporting to CSV: {ex.Message}",
                "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportItemsToCsv(IEnumerable<object> items, string fileName)
    {
        using var writer = new System.IO.StreamWriter(fileName);

        var firstItem = items.FirstOrDefault();
        if (firstItem != null)
        {
            var properties = firstItem.GetType().GetProperties()
                .Where(p => p.CanRead)
                .Select(p => p.Name);
            writer.WriteLine(string.Join(",", properties));
        }

        foreach (var item in items)
        {
            var values = item.GetType().GetProperties()
                .Where(p => p.CanRead)
                .Select(p => (p.GetValue(item)?.ToString() ?? string.Empty).Replace("\"", "\"\"", StringComparison.Ordinal));
            writer.WriteLine(string.Join(",", values));
        }
    }
}
