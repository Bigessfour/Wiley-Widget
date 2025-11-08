using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WileyWidget.Services.Threading;
using WileyWidget.ViewModels;
using WileyWidget.ViewModels.Messages;

namespace WileyWidget.Views.Main;

/// <summary>
/// Department Management UserControl - Provides full CRUD interface for municipal departments
/// </summary>
public partial class DepartmentView : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DepartmentView"/> class.
    /// ViewModel is auto-wired by Prism ViewModelLocator.
    /// </summary>
    public DepartmentView()
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
            var dataGrid = FindName("DepartmentGrid") as Syncfusion.UI.Xaml.Grid.SfDataGrid;
            if (dataGrid?.ItemsSource == null) return;

            // Create save file dialog
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx|CSV Files (*.csv)|*.csv|All files (*.*)|*.*",
                DefaultExt = ".xlsx",
                FileName = $"DepartmentData_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                // Capture UI data on the UI thread, then perform CPU/disk-bound CSV generation on a background thread.
                var data = dataGrid.ItemsSource as System.Collections.IEnumerable;
                if (data == null) return;

                // Convert to list for background processing
                var items = data.Cast<object>().ToList();

                await Task.Run(() =>
                {
                    try
                    {
                        // Generate CSV content
                        var csvContent = GenerateCsvContent(items);

                        // Save to file
                        System.IO.File.WriteAllText(saveFileDialog.FileName, csvContent);

                        // Show success message on UI thread
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show($"Data exported successfully to {saveFileDialog.FileName}",
                                          "Export Complete",
                                          MessageBoxButton.OK,
                                          MessageBoxImage.Information);
                        });
                    }
                    catch (Exception ex)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show($"Export failed: {ex.Message}",
                                          "Export Error",
                                          MessageBoxButton.OK,
                                          MessageBoxImage.Error);
                        });
                    }
                });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Export failed: {ex.Message}",
                          "Export Error",
                          MessageBoxButton.OK,
                          MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Generates CSV content from the data items
    /// </summary>
    private string GenerateCsvContent(System.Collections.Generic.List<object> items)
    {
        if (!items.Any()) return string.Empty;

        var csv = new System.Text.StringBuilder();

        // Add headers
        csv.AppendLine("ID,Name,DepartmentCode,ParentId");

        // Add data rows
        foreach (var item in items)
        {
            if (item is WileyWidget.Models.Department dept)
            {
                csv.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"{dept.Id},\"{dept.Name}\",\"{dept.DepartmentCode}\",{dept.ParentId}");
            }
        }

        return csv.ToString();
    }
}
