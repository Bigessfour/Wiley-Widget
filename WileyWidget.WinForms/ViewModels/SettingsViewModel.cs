using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;

namespace WileyWidget.WinForms.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        [ObservableProperty]
        private string title = "Settings";

        public IRelayCommand SaveCommand { get; }

        public SettingsViewModel()
        {
            SaveCommand = new AsyncRelayCommand(SaveAsync);
        }

        private async Task SaveAsync()
        {
            await Task.Delay(100);
        }
    }
}
