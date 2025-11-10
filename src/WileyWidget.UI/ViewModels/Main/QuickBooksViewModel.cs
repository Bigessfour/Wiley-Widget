using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using WileyWidget.Services;
using WileyWidget.Services.Threading;
using WileyWidget.ViewModels.Base;

namespace WileyWidget.ViewModels.Main {
    public class QuickBooksViewModel : AsyncViewModelBase, INavigationAware, IDisposable
    {
        private readonly IQuickBooksService _quickBooksService;
        private readonly ISettingsService _settings;
        private CancellationTokenSource _cancellationTokenSource = new();

        private bool _isConnected;
        private string _connectionStatus = "Not Connected";
        private string _companyName;
        private DateTime? _lastSyncTime;
        private bool _isSyncing;
        private string _syncStatus;
        private ObservableCollection<string> _syncHistory = new();

        // Validation properties
        private string _connectionValidationMessage;
        private string _syncValidationMessage;

        public QuickBooksViewModel(IQuickBooksService quickBooksService, ISettingsService settings, IDispatcherHelper dispatcherHelper, ILogger<QuickBooksViewModel> logger)
            : base(dispatcherHelper, logger)
        {
            _quickBooksService = quickBooksService ?? throw new ArgumentNullException(nameof(quickBooksService));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));

            ConnectCommand = new DelegateCommand(async () => await ConnectAsync(), () => !IsConnected);
            DisconnectCommand = new DelegateCommand(async () => await DisconnectAsync(), () => IsConnected);
            SyncNowCommand = new DelegateCommand(async () => await SyncNowAsync(), () => IsConnected && !IsSyncing);
            CancelCommand = new DelegateCommand(() => CancelOperations(), () => IsSyncing);
            RefreshStatusCommand = new DelegateCommand(async () => await RefreshStatusAsync());

            // Initialize status
            _ = RefreshStatusAsync();
        }

        public DelegateCommand ConnectCommand { get; }
        public DelegateCommand DisconnectCommand { get; }
        public DelegateCommand SyncNowCommand { get; }
        public DelegateCommand CancelCommand { get; }
        public DelegateCommand RefreshStatusCommand { get; }

        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                if (SetProperty(ref _isConnected, value))
                {
                    ConnectCommand.RaiseCanExecuteChanged();
                    DisconnectCommand.RaiseCanExecuteChanged();
                    SyncNowCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string ConnectionStatus
        {
            get => _connectionStatus;
            set => SetProperty(ref _connectionStatus, value);
        }

        public string CompanyName
        {
            get => _companyName;
            set => SetProperty(ref _companyName, value);
        }

        public DateTime? LastSyncTime
        {
            get => _lastSyncTime;
            set => SetProperty(ref _lastSyncTime, value);
        }

        public bool IsSyncing
        {
            get => _isSyncing;
            set
            {
                if (SetProperty(ref _isSyncing, value))
                {
                    SyncNowCommand.RaiseCanExecuteChanged();
                    CancelCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string SyncStatus
        {
            get => _syncStatus;
            set => SetProperty(ref _syncStatus, value);
        }

        public ObservableCollection<string> SyncHistory
        {
            get => _syncHistory;
            set => SetProperty(ref _syncHistory, value);
        }

        public string ConnectionValidationMessage
        {
            get => _connectionValidationMessage;
            set => SetProperty(ref _connectionValidationMessage, value);
        }

        public string SyncValidationMessage
        {
            get => _syncValidationMessage;
            set => SetProperty(ref _syncValidationMessage, value);
        }

        private async Task ConnectAsync()
        {
            try
            {
                // Validate connection prerequisites
                ConnectionValidationMessage = ValidateConnectionPrerequisites();
                if (!string.IsNullOrEmpty(ConnectionValidationMessage))
                {
                    MessageBox.Show(ConnectionValidationMessage, "Connection Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                IsBusy = true;
                BusyMessage = "Connecting to QuickBooks...";

                Logger.LogInformation("Initiating QuickBooks OAuth connection");

                // This will open the browser for OAuth flow
                var result = await _quickBooksService.ConnectAsync(_cancellationTokenSource.Token);

                // Refresh status after connection attempt
                await RefreshStatusAsync();

                Logger.LogInformation("QuickBooks connection process completed. Result: {Result}", result);
            }
            catch (OperationCanceledException)
            {
                Logger.LogInformation("QuickBooks connection was cancelled");
                ConnectionValidationMessage = "Connection cancelled by user";
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to connect to QuickBooks");
                ConnectionValidationMessage = $"Connection failed: {ex.Message}";
                MessageBox.Show($"Failed to connect to QuickBooks: {ex.Message}",
                    "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task DisconnectAsync()
        {
            try
            {
                IsBusy = true;
                BusyMessage = "Disconnecting from QuickBooks...";

                Logger.LogInformation("Disconnecting from QuickBooks");

                await _quickBooksService.DisconnectAsync(_cancellationTokenSource.Token);

                // Reset status
                IsConnected = false;
                ConnectionStatus = "Not Connected";
                CompanyName = null;
                LastSyncTime = null;

                Logger.LogInformation("QuickBooks disconnection completed");
            }
            catch (OperationCanceledException)
            {
                Logger.LogInformation("QuickBooks disconnection was cancelled");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to disconnect from QuickBooks");
                MessageBox.Show($"Failed to disconnect from QuickBooks: {ex.Message}",
                    "Disconnection Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task SyncNowAsync()
        {
            try
            {
                // Validate sync prerequisites
                SyncValidationMessage = ValidateSyncPrerequisites();
                if (!string.IsNullOrEmpty(SyncValidationMessage))
                {
                    MessageBox.Show(SyncValidationMessage, "Sync Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                IsSyncing = true;
                SyncStatus = "Synchronizing...";

                Logger.LogInformation("Starting manual QuickBooks sync");

                var syncResult = await _quickBooksService.SyncDataAsync(_cancellationTokenSource.Token);

                LastSyncTime = DateTime.Now;
                SyncStatus = $"Sync completed: {syncResult.RecordsSynced} records synced in {syncResult.Duration.TotalSeconds:F1}s";

                // Add to history
                var historyEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - Synced {syncResult.RecordsSynced} records ({syncResult.Duration.TotalSeconds:F1}s)";
                SyncHistory.Insert(0, historyEntry);

                // Keep only last 10 entries
                while (SyncHistory.Count > 10)
                {
                    SyncHistory.RemoveAt(SyncHistory.Count - 1);
                }

                Logger.LogInformation("QuickBooks sync completed: {RecordsSynced} records in {Duration}",
                    syncResult.RecordsSynced, syncResult.Duration);

                MessageBox.Show($"QuickBooks synchronization completed!\n\n{syncResult.RecordsSynced} records synced in {syncResult.Duration.TotalSeconds:F1} seconds.",
                    "Sync Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (OperationCanceledException)
            {
                Logger.LogInformation("QuickBooks sync was cancelled");
                SyncStatus = "Sync cancelled";
                SyncValidationMessage = "Sync operation was cancelled";
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to sync with QuickBooks");
                SyncStatus = $"Sync failed: {ex.Message}";
                SyncValidationMessage = $"Sync failed: {ex.Message}";
                MessageBox.Show($"Failed to sync with QuickBooks: {ex.Message}",
                    "Sync Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsSyncing = false;
            }
        }

        private async Task RefreshStatusAsync()
        {
            try
            {
                Logger.LogInformation("Refreshing QuickBooks connection status");

                var status = await _quickBooksService.GetConnectionStatusAsync(_cancellationTokenSource.Token);

                IsConnected = status.IsConnected;
                ConnectionStatus = status.IsConnected ? "Connected" : "Not Connected";
                CompanyName = status.CompanyName;

                if (status.IsConnected && !string.IsNullOrEmpty(status.CompanyName))
                {
                    ConnectionStatus = $"Connected to {status.CompanyName}";
                }

                Logger.LogInformation("QuickBooks status refreshed: Connected={IsConnected}, Company={CompanyName}",
                    status.IsConnected, status.CompanyName);
            }
            catch (OperationCanceledException)
            {
                Logger.LogInformation("QuickBooks status refresh was cancelled");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to refresh QuickBooks status");
                ConnectionStatus = "Status check failed";
            }
        }

        private void CancelOperations()
        {
            Logger.LogInformation("Cancelling QuickBooks operations");
            _cancellationTokenSource.Cancel();

            // Create a new token source for future operations
            _cancellationTokenSource = new CancellationTokenSource();

            SyncStatus = "Operations cancelled";
            SyncValidationMessage = "Operations were cancelled by user";
        }

        private string ValidateConnectionPrerequisites()
        {
            // Check if already connected
            if (IsConnected)
            {
                return "Already connected to QuickBooks. Please disconnect first if you want to reconnect.";
            }

            // Check if settings are configured
            var s = _settings.Current;
            if (string.IsNullOrEmpty(s.QboClientId) || string.IsNullOrEmpty(s.QboClientSecret))
            {
                return "QuickBooks client credentials are not configured. Please check your settings.";
            }

            // Check URL ACL
            // This would be an async check, but for now we'll assume it's configured
            // In a real implementation, we might want to check this here

            return null; // No validation errors
        }

        private string ValidateSyncPrerequisites()
        {
            // Check if connected
            if (!IsConnected)
            {
                return "Not connected to QuickBooks. Please connect first before syncing.";
            }

            // Check if already syncing
            if (IsSyncing)
            {
                return "A sync operation is already in progress. Please wait for it to complete.";
            }

            // Check if recently synced (optional - prevent too frequent syncs)
            if (LastSyncTime.HasValue && (DateTime.Now - LastSyncTime.Value).TotalMinutes < 1)
            {
                return "Please wait at least 1 minute between sync operations.";
            }

            return null; // No validation errors
        }

        #region INavigationAware Implementation

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            Logger.LogInformation("Navigated to QuickBooks view");

            // Refresh status when the view becomes active
            _ = RefreshStatusAsync();
        }

        public bool IsNavigationTarget(NavigationContext navigationContext)
        {
            // Allow navigation to this view; caller can request a fresh instance via parameters if needed
            return true;
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            Logger.LogInformation("Navigated away from QuickBooks view");

            // Cancel any ongoing operations and reset sync state as appropriate
            try
            {
                _cancellationTokenSource?.Cancel();
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Error cancelling QuickBooks operations on navigation away");
            }
            finally
            {
                // Create a fresh token source for future operations
                _cancellationTokenSource = new CancellationTokenSource();
            }
        }

        #endregion

        #region IDisposable Implementation

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
