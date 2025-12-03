using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;

namespace WileyWidget.WinForms.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private string title = "Wiley Widget — WinForms + .NET 9";

        public IRelayCommand LoadDataCommand { get; }

        public MainViewModel()
        {
            LoadDataCommand = new RelayCommand(async () => await LoadDataAsync());
        }

        private async Task LoadDataAsync()
        {
            await Task.Delay(100);
        }

        public async Task InitializeAsync()
        {
            await LoadDataAsync();
        }
    }
}
