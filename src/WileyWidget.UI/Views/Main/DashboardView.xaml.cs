using System;
using System.Windows;
using System.Windows.Controls;
using WileyWidget.ViewModels;

#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618, CS8622, CS8625 // Suppress nullability warnings in WPF application

namespace WileyWidget.Views.Main {
    /// <summary>
    /// Main dashboard view providing comprehensive enterprise financial analytics and visualization.
    /// Features: KPI gauges, interactive charts, budget trends, enterprise management, and real-time alerts.
    /// Implements MVVM pattern with ViewModel auto-wiring via Prism's ViewModelLocator.
    ///
    /// Accessibility Features:
    /// - Full keyboard navigation support via Tab/Shift+Tab
    /// - Screen reader compatible with AutomationProperties
    /// - High contrast theme support
    /// - Tooltips for all interactive elements
    /// - Focus management with FocusOnLoadBehavior
    ///
    /// Performance: Optimized with data virtualization, async loading, and caching strategies.
    /// Security: All data access through repository abstractions with comprehensive validation.
    /// </summary>
    public partial class DashboardView : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DashboardView"/> class.
        /// ViewModel is automatically wired by Prism's ViewModelLocator based on naming convention.
        /// </summary>
        public DashboardView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Shows the dashboard view by setting visibility to Visible.
        /// Provided for UI test compatibility.
        /// </summary>
        public void Show()
        {
            // UserControl doesn't have Show, but make it visible
            Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Hides the dashboard view by setting visibility to Collapsed.
        /// Provided for UI test compatibility.
        /// </summary>
        public void Close()
        {
            // UserControl doesn't have Close, but hide it
            Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Gets the title of the dashboard view.
        /// Used for window title bars and navigation breadcrumbs.
        /// </summary>
        public string Title => "Dashboard";
    }
}
