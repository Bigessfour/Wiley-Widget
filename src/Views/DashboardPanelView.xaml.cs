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
    private DispatcherTimer _refreshTimer;

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

            // Set up auto-refresh timer
            SetupAutoRefreshTimer();
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

    private async void DashboardPanelView_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_viewModel != null)
            await _viewModel.LoadDashboardDataAsync();
    }

    private void DashboardPanelView_Unloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        // Clean up timer
        if (_refreshTimer != null)
        {
            _refreshTimer.Stop();
            _refreshTimer = null;
        }
    }

    private void SetupAutoRefreshTimer()
    {
        _refreshTimer = new DispatcherTimer();
        _refreshTimer.Tick += async (s, e) =>
        {
            if (_viewModel.AutoRefreshEnabled)
            {
                await _viewModel.RefreshDashboardDataAsync();
            }
        };
        _refreshTimer.Interval = TimeSpan.FromMinutes(_viewModel.RefreshIntervalMinutes);
        _refreshTimer.Start();
    }
}