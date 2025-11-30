using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Threading.Tasks;

namespace WileyWidget.ViewModels
{
    public record MyEvent(object? Payload);

    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private string title;

        public IRelayCommand LoadDataCommand { get; }
        private readonly IMessenger _messenger;

        public MainViewModel(IMessenger messenger)
        {
            _messenger = messenger;
            LoadDataCommand = new RelayCommand(async () => await LoadDataAsync());
            // Register for MyEvent messages; handler will receive the MyEvent instance
            _messenger.Register<MainViewModel, MyEvent>(this, (r, m) => r.HandleEvent(m.Payload));
        }

        private async Task LoadDataAsync()
        {
            // Example async I/O-bound work
            await Task.Delay(100); // placeholder for real async work
        }

        private void HandleEvent(object? data)
        {
            // handle event logic here
        }
    }
}

