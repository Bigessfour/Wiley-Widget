using System;
using System.Windows.Controls;
using System.Windows.Threading;
using Serilog;
using Syncfusion.SfSkinManager;
using Syncfusion.Windows.Shared;
using WileyWidget.Services;
using WileyWidget.ViewModels;

#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618, CS8622, CS8625 // Suppress nullability warnings in WPF application

namespace WileyWidget.Views.Panels;

/// <summary>
/// Dashboard panel view for embedding in docking layout
/// </summary>
public partial class DashboardPanelView : UserControl
{
    public DashboardPanelView()
    {
        InitializeComponent();

        // ViewModel is auto-wired via Prism's ViewModelLocator (see XAML AutoWireViewModel="True")
        // Load/unload hooks for diagnostics only
        Loaded += DashboardPanelView_Loaded;
        Unloaded += DashboardPanelView_Unloaded;
    }

    private void DashboardPanelView_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        // Data loading is handled by ViewModel constructor - no need to load again
        // if (_viewModel != null)
        //     await _viewModel.LoadDashboardDataAsync();

    Log.Debug("DashboardPanelView loaded successfully");
    }

    private void DashboardPanelView_Unloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        // Auto-refresh cleanup handled by ViewModel's IDisposable implementation
        // No local timer to clean up
    Log.Debug("DashboardPanelView unloaded");
    }
}
