using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WileyWidget.Services;

namespace WileyWidget.ViewModels
{
    public record MyEvent(object? Payload);

    public partial class MainViewModel : ObservableObject
    {
        private readonly StartupOrchestrator _orchestrator;
        private readonly ILogger<MainViewModel> _logger;
        private readonly IMessenger _messenger;

        [ObservableProperty]
        private string title;

        [ObservableProperty]
        private bool isInitializing;

        public IRelayCommand LoadDataCommand { get; }

        public MainViewModel(
            IMessenger messenger,
            StartupOrchestrator orchestrator,
            ILogger<MainViewModel> logger)
        {
            _messenger = messenger;
            _orchestrator = orchestrator;
            _logger = logger;

            LoadDataCommand = new RelayCommand(async () => await LoadDataAsync());

            // Register for MyEvent messages; handler will receive the MyEvent instance
            _messenger.Register<MainViewModel, MyEvent>(this, (r, m) => r.HandleEvent(m.Payload));
        }

        /// <summary>
        /// Initialize the main view model with startup orchestration.
        /// Call this method during app initialization or main window load.
        /// </summary>
        public async Task InitializeAsync()
        {
            _logger.LogInformation("MainViewModel: Initiating startup orchestration");
            IsInitializing = true;

            try
            {
                // Await startup orchestrator completion (IHostedService runs automatically)
                await _orchestrator.CompletionTask;
                _logger.LogInformation("MainViewModel: Startup orchestration completed successfully");
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "MainViewModel: Startup orchestration failed");
                // Handle startup failure (show error dialog, etc.)
            }
            finally
            {
                IsInitializing = false;
            }
        }

        private async Task LoadDataAsync()
        {
            _logger.LogInformation("MainViewModel: Loading data");
            // Example async I/O-bound work
            await Task.Delay(100); // placeholder for real async work
        }

        private void HandleEvent(object? data)
        {
            // handle event logic here
            _logger.LogDebug("MainViewModel: Event received with data: {Data}", data);
        }
    }
}

