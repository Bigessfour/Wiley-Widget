using System;
using System.Windows.Controls;
using System.Windows.Threading;
using WileyWidget.ViewModels;
using Syncfusion.SfSkinManager;
using Syncfusion.Windows.Shared;
using WileyWidget.Services;
using Serilog;

#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618, CS8622, CS8625 // Suppress nullability warnings in WPF application

namespace WileyWidget.Views;

/// <summary>
/// Dashboard panel view for embedding in docking layout
/// </summary>
public partial class DashboardPanelView : UserControl
{
    private readonly DashboardViewModel _viewModel;
    // private DispatcherTimer _refreshTimer; // Removed - auto-refresh handled by ViewModel

    public DashboardPanelView()
    {
        InitializeComponent();

        // Get the ViewModel from the service provider
        DashboardViewModel? resolvedViewModel = null;
        try
        {
            var containerProvider = App.GetContainerProvider();
            resolvedViewModel = containerProvider.Resolve<DashboardViewModel>();
        }
        catch (InvalidOperationException)
        {
            resolvedViewModel = null;
        }

        if (resolvedViewModel != null)
        {
            _viewModel = resolvedViewModel;
            DataContext = _viewModel;

            // Auto-refresh is handled by ViewModel - no need for separate timer in panel view
            // SetupAutoRefreshTimer();
        }
        else
        {
            // For testing purposes, allow view to load without ViewModel
            _viewModel = null;
            DataContext = null;
        }

        // Load dashboard data when control loads
        Loaded += DashboardPanelView_Loaded;
        Unloaded += DashboardPanelView_Unloaded;
    }

    private void DashboardPanelView_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        // Data loading is handled by ViewModel constructor - no need to load again
        // if (_viewModel != null)
        //     await _viewModel.LoadDashboardDataAsync();
        
        Log.Debug("DashboardPanelView loaded successfully - using shared ViewModel data");
    }

    private void DashboardPanelView_Unloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        // Auto-refresh cleanup handled by ViewModel's IDisposable implementation
        // No local timer to clean up
        Log.Debug("DashboardPanelView unloaded");
    }
}