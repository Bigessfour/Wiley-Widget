using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using WileyWidget.ViewModels;
using Syncfusion.SfSkinManager;
using Syncfusion.Windows.Shared;
using WileyWidget.Services;
using WileyWidget.Services.Logging;
using Serilog;

#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618, CS8622, CS8625 // Suppress nullability warnings in WPF application

namespace WileyWidget
{
    /// <summary>
    /// Interaction logic for DashboardView.xaml
    /// </summary>
    public partial class DashboardView : UserControl
    {
        // private DispatcherTimer _refreshTimer; // Removed - timer is now managed by ViewModel

        public DashboardView()
        {
            Log.Debug("DashboardView: Constructor called");
            
            InitializeComponent();
            Log.Debug("DashboardView: InitializeComponent completed");

            // Note: ViewModel is now auto-wired by Prism ViewModelLocator
            // Theme is applied declaratively in XAML to prevent loading crashes

            // Removed: Auto-refresh timer setup - now handled by ViewModel only
            // if (DataContext is DashboardViewModel viewModel)
            // {
            //     Log.Debug("DashboardView: DataContext set to DashboardViewModel in constructor");
            //     SetupAutoRefreshTimer(viewModel);
            // }
            // else
            // {
            //     Log.Warning("DashboardView: DataContext is not DashboardViewModel in constructor");
            // }

            // Load dashboard data when control loads
            Loaded += DashboardView_Loaded;
            DataContextChanged += DashboardView_DataContextChanged;
            LayoutUpdated += DashboardView_LayoutUpdated;
            
            Log.Debug("DashboardView: Constructor completed");
        }
        
        private bool _layoutLoggedOnce = false;

        private void DashboardView_LayoutUpdated(object sender, EventArgs e)
        {
            if (!_layoutLoggedOnce)
            {
                _layoutLoggedOnce = true;
                Log.Debug("DashboardView: LayoutUpdated event fired - Layout pass completed");
                
                // Log visual tree after layout
                LogVisualTree();
            }
        }

        private void DashboardView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            Log.Debug($"DashboardView: DataContext changed from {e.OldValue?.GetType().Name ?? "null"} to {e.NewValue?.GetType().Name ?? "null"}");
            
            if (e.NewValue is DashboardViewModel viewModel)
            {
                Log.Information("DashboardView: DataContext successfully set to DashboardViewModel");
                // Removed: Auto-refresh timer setup - now handled by ViewModel only
                // SetupAutoRefreshTimer(viewModel);
            }
        }

        private void DashboardView_Loaded(object sender, RoutedEventArgs e)
        {
            using var loggingContext = LoggingContext.BeginOperation("DashboardView_Loaded");
            
            Log.Debug("DashboardView: Loaded event fired - {LogContext}", loggingContext);
            Log.Information($"DashboardView: Size - ActualWidth: {ActualWidth}, ActualHeight: {ActualHeight}");
            
            // Data loading is now handled by ViewModel constructor
            // No need to trigger LoadDataCommand here as it's redundant
            Log.Information("DashboardView: View loaded successfully - data loading handled by ViewModel - {LogContext}", loggingContext);
        }
        
        private void LogVisualTree()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("=== DashboardView Visual Tree ===");
                WalkVisualTree(this, 0, sb);
                Log.Information(sb.ToString());
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error logging visual tree");
            }
        }
        
        private void WalkVisualTree(DependencyObject element, int depth, StringBuilder sb)
        {
            if (element == null) return;
            
            string indent = new string(' ', depth * 2);
            string typeName = element.GetType().Name;
            
            // Get size information if it's a FrameworkElement
            string sizeInfo = "";
            if (element is FrameworkElement fe)
            {
                sizeInfo = $" [ActualSize: {fe.ActualWidth:F0}x{fe.ActualHeight:F0}, Visibility: {fe.Visibility}]";
            }
            
            sb.AppendLine($"{indent}{typeName}{sizeInfo}");
            
            // Recursively walk children (limit depth to avoid excessive logging)
            if (depth < 10)
            {
                int childCount = VisualTreeHelper.GetChildrenCount(element);
                for (int i = 0; i < childCount; i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(element, i);
                    WalkVisualTree(child, depth + 1, sb);
                }
            }
        }

        // Methods for UI test compatibility
        public void Show()
        {
            // UserControl doesn't have Show, but make it visible
            Visibility = Visibility.Visible;
        }

        public void Close()
        {
            // UserControl doesn't have Close, but hide it
            Visibility = Visibility.Collapsed;
        }

        public string Title => "Dashboard";
    }
}