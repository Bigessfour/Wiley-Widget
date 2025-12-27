using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.WinForms.ViewModels;

public partial class QuickBooksViewModel : ObservableObject
{
    private readonly ILogger<QuickBooksViewModel> _logger;
    private readonly IQuickBooksService _quickBooksService;
    private SynchronizationContext? _uiContext;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private int _progress;

    [ObservableProperty]
    private string? _companyName;

    [ObservableProperty]
    private string? _lastSyncTime;

    [ObservableProperty]
    private string _connectionLabel = "Checking connection...";

    public ObservableCollection<string> Logs { get; } = new();

    public IAsyncRelayCommand ConnectCommand { get; }
    public IAsyncRelayCommand DisconnectCommand { get; }
    public IAsyncRelayCommand SyncCommand { get; }
    public IAsyncRelayCommand ImportAccountsCommand { get; }

    public QuickBooksViewModel(ILogger<QuickBooksViewModel> logger, IQuickBooksService quickBooksService, SynchronizationContext? uiContext = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _quickBooksService = quickBooksService ?? throw new ArgumentNullException(nameof(quickBooksService));
        _uiContext = uiContext;

        ConnectCommand = new AsyncRelayCommand(ConnectAsync, () => CanConnect);
        DisconnectCommand = new AsyncRelayCommand(DisconnectAsync, () => CanDisconnect);
        SyncCommand = new AsyncRelayCommand(SyncDataAsync, () => CanSync);
        ImportAccountsCommand = new AsyncRelayCommand(ImportAccountsAsync, () => CanImportAccounts);
    }

    public bool CanConnect => !IsConnected && !IsLoading;
    public bool CanDisconnect => IsConnected && !IsLoading;
    public bool CanSync => IsConnected && !IsLoading;
    public bool CanImportAccounts => IsConnected && !IsLoading;

    partial void OnIsConnectedChanged(bool value)
    {
        RefreshCommandsCanExecute();
    }

    partial void OnIsLoadingChanged(bool value)
    {
        RefreshCommandsCanExecute();
    }

    private void RefreshCommandsCanExecute()
    {
        try
        {
            (ConnectCommand as IAsyncRelayCommand)?.NotifyCanExecuteChanged();
            (DisconnectCommand as IAsyncRelayCommand)?.NotifyCanExecuteChanged();
            (SyncCommand as IAsyncRelayCommand)?.NotifyCanExecuteChanged();
            (ImportAccountsCommand as IAsyncRelayCommand)?.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(CanConnect));
            OnPropertyChanged(nameof(CanDisconnect));
            OnPropertyChanged(nameof(CanSync));
            OnPropertyChanged(nameof(CanImportAccounts));
        }
        catch
        {
            // best-effort
        }
    }

    public void SetUiContext(SynchronizationContext context)
    {
        _uiContext = context ?? throw new ArgumentNullException(nameof(context));
    }

    private void AddLog(string message)
    {
        var entry = $"[{DateTime.Now:HH:mm:ss}] {message}";
        if (_uiContext != null)
        {
            _uiContext.Post(_ =>
            {
                Logs.Add(entry);
                StatusText += entry + Environment.NewLine;
            }, null);
        }
        else
        {
            Logs.Add(entry);
            StatusText += entry + Environment.NewLine;
        }
    }

    public async Task RefreshConnectionStatusAsync()
    {
        try
        {
            IsLoading = true;
            StatusText = "Checking QuickBooks connection...";
            var connectionStatus = await _quickBooksService.GetConnectionStatusAsync();
            IsConnected = connectionStatus.IsConnected;
            CompanyName = connectionStatus.CompanyName;
            LastSyncTime = connectionStatus.LastSyncTime;
            ConnectionLabel = connectionStatus.IsConnected
                ? $"Connected to: {connectionStatus.CompanyName ?? "Unknown Company"}"
                : "Not Connected";
            AddLog(ConnectionLabel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking QuickBooks connection");
            AddLog($"Error checking connection: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ConnectAsync()
    {
        try
        {
            IsLoading = true;
            AddLog("Connecting to QuickBooks...");
            var success = await _quickBooksService.ConnectAsync();
            if (success)
            {
                AddLog("Successfully connected to QuickBooks!");
                await RefreshConnectionStatusAsync();
            }
            else
            {
                AddLog("Failed to connect to QuickBooks.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting to QuickBooks");
            AddLog($"Connection error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task DisconnectAsync()
    {
        try
        {
            IsLoading = true;
            AddLog("Disconnecting from QuickBooks...");
            await _quickBooksService.DisconnectAsync();
            AddLog("Disconnected from QuickBooks.");
            await RefreshConnectionStatusAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting from QuickBooks");
            AddLog($"Disconnect error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SyncDataAsync()
    {
        try
        {
            IsLoading = true;
            AddLog("Syncing data from QuickBooks...");
            var result = await _quickBooksService.SyncDataAsync();
            if (result.Success)
            {
                AddLog($"Successfully synced {result.RecordsSynced} records from QuickBooks in {result.Duration.TotalSeconds:F1} seconds.");
            }
            else
            {
                AddLog($"Sync failed: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing data from QuickBooks");
            AddLog($"Sync error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ImportAccountsAsync()
    {
        try
        {
            IsLoading = true;
            AddLog("Importing chart of accounts from QuickBooks...");
            var result = await _quickBooksService.ImportChartOfAccountsAsync();
            if (result.Success)
            {
                AddLog($"Successfully imported {result.AccountsImported} accounts, updated {result.AccountsUpdated}, skipped {result.AccountsSkipped} in {result.Duration.TotalSeconds:F1} seconds.");
            }
            else
            {
                AddLog($"Import failed: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing chart of accounts");
            AddLog($"Import error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }
}
