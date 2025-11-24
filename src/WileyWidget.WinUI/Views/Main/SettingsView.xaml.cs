using Microsoft.UI.Xaml.Controls;
using WileyWidget.WinUI.ViewModels.Main;

namespace WileyWidget.WinUI.Views.Main
{
    public sealed partial class SettingsView : UserControl
    {
        public SettingsViewModel? ViewModel { get; }

        public SettingsView(SettingsViewModel? viewModel = null)
        {
            // Attempt to resolve via DI if not provided
            ViewModel = viewModel ?? App.Services?.GetService<SettingsViewModel>();
            if (ViewModel == null)
            {
                try { App.Services?.GetService<ILogger<SettingsView>>()?.LogWarning("SettingsViewModel not available from DI; using fallback instance"); } catch { }
                ViewModel = new SettingsViewModel();
            }

            this.InitializeComponent();

            // Set DataContext for data binding
            this.DataContext = ViewModel;
        }
    }
}
