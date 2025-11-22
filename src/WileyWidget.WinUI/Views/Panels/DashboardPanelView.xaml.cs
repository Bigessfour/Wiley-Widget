using Microsoft.UI.Xaml.Controls;
using WileyWidget.WinUI.ViewModels.Main;

namespace WileyWidget.WinUI.Views.Panels
{
    public sealed partial class DashboardPanelView : UserControl
    {
        public DashboardViewModel ViewModel { get; }

        public DashboardPanelView(DashboardViewModel viewModel)
        {
            ViewModel = viewModel;
            this.InitializeComponent();
        }

        // Parameterless constructor for design-time
        public DashboardPanelView() : this(null!)
        {
        }
    }
}