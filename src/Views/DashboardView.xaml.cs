using System;
using System.Windows;
using System.Windows.Controls;
using WileyWidget.ViewModels;

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
            InitializeComponent();
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
