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
/// Dashboard panel view for embedding in docking layout.
/// Provides enterprise-level dashboard visualization with KPIs, charts, alerts, and activity feeds.
/// Implements MVVM pattern with ViewModel auto-wiring via Prism.
/// Accessibility: Supports keyboard navigation, screen readers, and high contrast themes.
/// </summary>
public partial class DashboardPanelView : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardPanelView"/> class.
    /// ViewModel is automatically wired via Prism's ViewModelLocator (AutoWireViewModel="True" in XAML).
    /// </summary>
    public DashboardPanelView()
    {
        InitializeComponent();

        // ViewModel is auto-wired via Prism's ViewModelLocator (see XAML AutoWireViewModel="True")
        // Load/unload hooks for diagnostics and resource management
        Loaded += DashboardPanelView_Loaded;
        Unloaded += DashboardPanelView_Unloaded;
    }

    /// <summary>
    /// Handles the Loaded event. Logs view loading for diagnostics.
    /// Data loading is managed by the ViewModel constructor and OnNavigatedTo.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The event data.</param>
    private void DashboardPanelView_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        // Data loading is handled by ViewModel constructor - no need to load again
        // This ensures proper separation of concerns between View and ViewModel
        Log.Debug("DashboardPanelView loaded successfully");
    }

    /// <summary>
    /// Handles the Unloaded event. Logs view unloading for diagnostics.
    /// Auto-refresh cleanup and resource disposal is handled by ViewModel's IDisposable implementation.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The event data.</param>
    private void DashboardPanelView_Unloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        // Auto-refresh cleanup handled by ViewModel's IDisposable implementation
        // No local timer to clean up - all lifecycle management is ViewModel responsibility
        Log.Debug("DashboardPanelView unloaded");
    }
}
