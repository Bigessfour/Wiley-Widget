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
            // Ensure InitializeComponent is available
            InitializeComponent();

            // Resolve ViewModel from DI if not provided; fallback to a new instance and log
            ViewModel = viewModel ?? App.Services?.GetService<DashboardViewModel>();
            if (ViewModel == null)
            {
                try { App.Services?.GetService<ILogger<DashboardPanelView>>()?.LogError("DashboardViewModel not available from DI container; using fallback instance"); } catch { }
                ViewModel = new DashboardViewModel();
            }

            // Set DataContext for data binding
            this.DataContext = ViewModel;
        }

    }
}
