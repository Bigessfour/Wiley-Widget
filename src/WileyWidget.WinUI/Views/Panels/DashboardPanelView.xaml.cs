using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.WinUI.ViewModels.Main;
using System;

namespace WileyWidget.WinUI.Views.Panels
{
    public sealed partial class DashboardPanelView : UserControl
    {
        public DashboardViewModel ViewModel { get; }

        public DashboardPanelView(DashboardViewModel? viewModel = null)
        {
            this.InitializeComponent();

            // Resolve ViewModel from DI if not provided
            ViewModel = viewModel ?? App.Services?.GetRequiredService<DashboardViewModel>()
                ?? throw new InvalidOperationException("DashboardViewModel not available from DI container");

            // Set DataContext for data binding
            this.DataContext = ViewModel;
        }
    }
}