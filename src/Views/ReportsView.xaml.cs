using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Prism.Ioc;
using Microsoft.Extensions.DependencyInjection;
//using Syncfusion.Windows.Reports.Viewer;
using WileyWidget.ViewModels;

namespace WileyWidget;

/// <summary>
/// Interactive report surface powered by Syncfusion controls.
/// </summary>
public partial class ReportsView : UserControl
{
    private string? _cachedReportPath;

    /// <summary>
    /// Prism-aware constructor - prefer this so the container can inject dependencies.
    /// </summary>
    public ReportsView(IContainerProvider containerProvider)
    {
        InitializeComponent();

        // DataContext will be auto-wired by Prism ViewModelLocator
        if (DataContext is ReportsViewModel vm)
        {
            vm.DataLoaded += OnDataLoaded;
            vm.ExportCompleted += OnExportCompleted;
        }
    }

    /// <summary>
    /// Parameterless constructor remains for XAML designer compatibility and for
    /// any code paths that instantiate the view without DI. It attempts to use
    /// the application container as a fallback but does not throw if unavailable.
    /// </summary>
    public ReportsView()
    {
        InitializeComponent();

        // DataContext will be auto-wired by Prism ViewModelLocator
        if (DataContext is ReportsViewModel vm)
        {
            vm.DataLoaded += OnDataLoaded;
            vm.ExportCompleted += OnExportCompleted;
        }
    }

    //private void OnReportViewerLoaded(object sender, RoutedEventArgs e)
    //{
    //    RefreshReportViewer();
    //}

    private void ReportViewer_Loaded(object sender, RoutedEventArgs e)
    {
        // Currently, the Bold Reports WPF Report Viewer control isn't available.
        // Show the fallback message to guide setup until the correct package is integrated.
        ShowFallback();
    }

    private void ShowFallback()
    {
        Dispatcher.Invoke(() =>
        {
            // Resolve by name to avoid compile-time dependency on generated fields
            if (FindName("FallbackText") is FrameworkElement fallback)
            {
                fallback.Visibility = Visibility.Visible;
            }
        });
    }

    private void OnDataLoaded(object? sender, ReportsViewModel.ReportDataEventArgs e)
    {
        // No-op for now: report viewer not wired. Keep fallback visible.
        Dispatcher.Invoke(ShowFallback);
    }

    private void OnExportCompleted(object? sender, ReportsViewModel.ReportExportCompletedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            MessageBox.Show(
                $"Report exported to {e.FilePath}",
                $"Export ({e.Format.ToUpperInvariant()})",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        });
    }

    private void RefreshReportViewer()
    {
        // Placeholder: the Bold Reports viewer isn't integrated yet.
        // Keep the fallback visible so users know to install/configure Bold Reports.
        ShowFallback();
    }

    private string EnsureReportDefinition()
    {
        if (!string.IsNullOrEmpty(_cachedReportPath) && File.Exists(_cachedReportPath))
        {
            return _cachedReportPath;
        }

        var resourceUri = new Uri("pack://application:,,,/src/Reports/EnterpriseSummary.rdl", UriKind.Absolute);
        var resourceStream = Application.GetResourceStream(resourceUri)?.Stream;
        if (resourceStream is null)
        {
            throw new InvalidOperationException("Unable to locate EnterpriseSummary.rdl resource.");
        }

        var directory = Path.Combine(Path.GetTempPath(), "WileyWidget", "Reports");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "EnterpriseSummary.rdl");

        using (resourceStream)
        using (var fileStream = File.Create(path))
        {
            resourceStream.CopyTo(fileStream);
        }

        _cachedReportPath = path;
        return path;
    }
}
