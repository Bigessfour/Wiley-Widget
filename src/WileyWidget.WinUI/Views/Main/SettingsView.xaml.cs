using Microsoft.UI.Xaml.Controls;
using WileyWidget.WinUI.ViewModels.Main;

namespace WileyWidget.WinUI.Views.Main
{
    public sealed partial class SettingsView : UserControl
    {
        public SettingsViewModel ViewModel { get; }

        public SettingsView(SettingsViewModel viewModel)
        {
            ViewModel = viewModel;
            this.InitializeComponent();
        }

        // Parameterless constructor for design-time
        public SettingsView() : this(null!)
        {
        }
    }
}