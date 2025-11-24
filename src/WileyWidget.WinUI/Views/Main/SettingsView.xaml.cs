using Microsoft.UI.Xaml.Controls;
using WileyWidget.WinUI.ViewModels.Main;

namespace WileyWidget.WinUI.Views.Main
{
    public sealed partial class SettingsView : UserControl
    {
        public SettingsViewModel? ViewModel { get; }

        public SettingsView(SettingsViewModel? viewModel = null)
        {
            ViewModel = viewModel;
            this.InitializeComponent();

            // Set DataContext for data binding
            this.DataContext = ViewModel;
        }
    }
}