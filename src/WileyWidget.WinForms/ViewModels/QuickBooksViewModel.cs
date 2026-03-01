using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.WinForms.ViewModels;

/// <summary>
/// ViewModel for QuickBooks integration panel.
/// Provides connection management, data synchronization, import operations, and sync history tracking.
/// Follows full MVVM pattern with async commands, observable collections, and comprehensive error handling.
/// </summary>
public sealed partial class QuickBooksViewModel : ObservableObject, IQuickBooksViewModel, IDisposable
{
    private readonly ILogger<QuickBooksViewModel> _logger;
    private readonly IQuickBooksService _quickBooksService;
    private readonly WileyWidget.Business.Interfaces.IQuickBooksBudgetSyncService? _quickBooksBudgetSyncService;
    private System.Threading.Timer? _connectionPollingTimer;
    private bool _disposed;
    private CancellationTokenSource? _cancellationTokenSource;

    #region Observable Properties

    /// <summary>Gets or sets whether the panel is currently loading data.</summary>
    [ObservableProperty]
    private bool isLoading;

    /// <summary>Gets or sets the status text displayed to the user.</summary>
    [ObservableProperty]
    private string statusText = "Ready";

    /// <summary>Gets or sets the current error message, if any.</summary>
    [ObservableProperty]
    private string? errorMessage;

    /// <summary>Gets or sets whether QuickBooks is currently connected.</summary>
    [ObservableProperty]
    private bool isConnected;

    /// <summary>Gets or sets the company name from QuickBooks.</summary>
    [ObservableProperty]
    private string? companyName;

    /// <summary>Gets or sets the last sync timestamp.</summary>
    [ObservableProperty]
    private string? lastSyncTime;

    /// <summary>Gets or sets the connection status message.</summary>
    [ObservableProperty]
    private string connectionStatus = "Checking connection...";

    /// <summary>Gets or sets the connection status message.</summary>
    [ObservableProperty]
    private string connectionStatusMessage = "Checking connection...";

    /// <summary>Gets or sets the sync progress percentage (0-100).</summary>
    [ObservableProperty]
    private int syncProgress;

    /// <summary>Gets or sets whether a sync operation is in progress.</summary>
    [ObservableProperty]
    private bool isSyncing;

    /// <summary>Gets the collection of sync history records.</summary>
    public ObservableCollection<QuickBooksSyncHistoryRecord> SyncHistory { get; } = new();

    /// <summary>Gets the filtered collection of sync history records.</summary>
    [ObservableProperty]
    private ObservableCollection<QuickBooksSyncHistoryRecord> filteredSyncHistory = new();

    /// <summary>Gets or sets the selected sync history record.</summary>
    [ObservableProperty]
    private QuickBooksSyncHistoryRecord? selectedSyncRecord;

    /// <summary>Gets or sets the search/filter text for sync history.</summary>
    [ObservableProperty]
    private string? filterText;

    // Summary Properties
    /// <summary>Gets or sets the total number of sync operations.</summary>
    [ObservableProperty]
    private int totalSyncs;

    /// <summary>Gets or sets the number of successful syncs.</summary>
    [ObservableProperty]
    private int successfulSyncs;

    /// <summary>Gets or sets the number of failed syncs.</summary>
    [ObservableProperty]
    private int failedSyncs;

    /// <summary>Gets or sets the total records synced across all operations.</summary>
    [ObservableProperty]
    private int totalRecordsSynced;

    /// <summary>Gets or sets the number of accounts imported.</summary>
    [ObservableProperty]
    private int accountsImported;

    /// <summary>Gets or sets the average sync duration in seconds.</summary>
    [ObservableProperty]
    private double averageSyncDuration;

    #endregion

    #region Commands

    /// <summary>Gets the command to check connection status.</summary>
    public IAsyncRelayCommand CheckConnectionCommand { get; }

    /// <summary>Gets the command to connect to QuickBooks.</summary>
    public IAsyncRelayCommand ConnectCommand { get; }

    /// <summary>Gets the command to disconnect from QuickBooks.</summary>
    public IAsyncRelayCommand DisconnectCommand { get; }

    /// <summary>Gets the command to test the connection.</summary>
    public IAsyncRelayCommand TestConnectionCommand { get; }

    /// <summary>Gets the command to synchronize data from QuickBooks.</summary>
    public IAsyncRelayCommand SyncDataCommand { get; }

    /// <summary>Gets the command to test sync accounts from QuickBooks.</summary>
    public IAsyncRelayCommand SyncAccountsCommand { get; }

    /// <summary>Gets the command to import chart of accounts.</summary>
    public IAsyncRelayCommand ImportAccountsCommand { get; }

    /// <summary>Gets the command to refresh sync history.</summary>
    public IAsyncRelayCommand RefreshHistoryCommand { get; }

    /// <summary>Gets the command to clear sync history.</summary>
    public IRelayCommand ClearHistoryCommand { get; }

    /// <summary>Gets the command to export sync history to CSV.</summary>
    public IAsyncRelayCommand ExportHistoryCommand { get; }

    #endregion

    /// <summary>
    /// Initializes a new instance of the <see cref="QuickBooksViewModel"/> class.
    /// </summary>
    /// <param name="logger">Logger instance for diagnostic logging.</param>
    /// <param name="quickBooksService">QuickBooks service for API operations.</param>
    public QuickBooksViewModel(
        ILogger<QuickBooksViewModel> logger,
        IQuickBooksService quickBooksService,
        WileyWidget.Business.Interfaces.IQuickBooksBudgetSyncService? quickBooksBudgetSyncService = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _quickBooksService = quickBooksService ?? throw new ArgumentNullException(nameof(quickBooksService));
        _quickBooksBudgetSyncService = quickBooksBudgetSyncService;

        // Initialize commands
        CheckConnectionCommand = new AsyncRelayCommand(CheckConnectionAsync);
        ConnectCommand = new AsyncRelayCommand(ConnectAsync, CanConnect);
        DisconnectCommand = new AsyncRelayCommand(DisconnectAsync, CanDisconnect);
        TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync);
        SyncDataCommand = new AsyncRelayCommand(SyncDataAsync, CanSyncData);
        SyncAccountsCommand = new AsyncRelayCommand(SyncAccountsTestAsync, CanSyncData);
        ImportAccountsCommand = new AsyncRelayCommand(ImportAccountsAsync, CanImportAccounts);
        RefreshHistoryCommand = new AsyncRelayCommand(RefreshHistoryAsync);
        ClearHistoryCommand = new RelayCommand(ClearHistory);
        ExportHistoryCommand = new AsyncRelayCommand(ExportHistoryAsync);

        // Wire up property changes for filtering
        PropertyChanged += OnPropertyChangedForFiltering;

        _logger.LogInformation("QuickBooksViewModel constructed");
    }

    #region Initialization

    /// <summary>
    /// Initializes the ViewModel by checking connection status and loading history.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Initializing QuickBooksViewModel");

            await CheckConnectionAsync(cancellationToken);
            ResetSyncHistoryState();

            // Start connection polling (every 30 seconds)
            StartConnectionPolling();

            _logger.LogInformation("QuickBooksViewModel initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize QuickBooksViewModel");
            ErrorMessage = $"Initialization failed: {ex.Message}";
            StatusText = "Initialization error";
        }
    }

    #endregion

    #region Connection Management

    /// <summary>
    /// Checks the current QuickBooks connection status.
    /// </summary>
    private async Task CheckConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IsLoading = true;
            StatusText = "Checking QuickBooks connection...";
            ErrorMessage = null;

            _logger.LogInformation("Checking QuickBooks connection status");

            var status = await _quickBooksService.GetConnectionStatusAsync(cancellationToken);

            IsConnected = status.IsConnected;
            CompanyName = status.CompanyName;
            LastSyncTime = status.LastSyncTime;

            if (status.IsConnected)
            {
                ConnectionStatus = $"Connected to {status.CompanyName ?? "QuickBooks"}";
                ConnectionStatusMessage = $"Connected to {status.CompanyName ?? "QuickBooks"}";
                StatusText = $"Connected. Last sync: {status.LastSyncTime ?? "Never"}";
                _logger.LogInformation("QuickBooks connected: {CompanyName}", status.CompanyName);
            }
            else
            {
                ConnectionStatus = "Not connected";
                ConnectionStatusMessage = "Not connected";
                StatusText = "Not connected. Click 'Connect' to establish connection.";
                _logger.LogInformation("QuickBooks not connected");
            }

            // Notify commands to update their CanExecute state
            ConnectCommand.NotifyCanExecuteChanged();
            DisconnectCommand.NotifyCanExecuteChanged();
            SyncDataCommand.NotifyCanExecuteChanged();
            ImportAccountsCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check connection status");
            ConnectionStatus = "Error checking status";
            ConnectionStatusMessage = "Error checking status";
            StatusText = "Connection check failed";
            ErrorMessage = $"Connection check failed: {ex.Message}";
            IsConnected = false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Connects to QuickBooks Online.
    /// </summary>
    private async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IsLoading = true;
            StatusText = "Connecting to QuickBooks...";
            ErrorMessage = null;

            _logger.LogInformation("Initiating QuickBooks connection");

            var success = await _quickBooksService.ConnectAsync(cancellationToken);

            if (success)
            {
                await CheckConnectionAsync(cancellationToken);
                StatusText = "Successfully connected to QuickBooks!";

                AddSyncHistoryRecord(new QuickBooksSyncHistoryRecord
                {
                    Timestamp = DateTime.Now,
                    Operation = "Connect",
                    Status = "Success",
                    RecordsProcessed = 0,
                    Duration = TimeSpan.Zero,
                    Message = "Connected to QuickBooks successfully"
                });

                _logger.LogInformation("QuickBooks connection successful");
            }
            else
            {
                StatusText = "Connection failed. Check credentials.";
                ErrorMessage = "Failed to connect to QuickBooks. Please verify your API credentials.";

                AddSyncHistoryRecord(new QuickBooksSyncHistoryRecord
                {
                    Timestamp = DateTime.Now,
                    Operation = "Connect",
                    Status = "Failed",
                    RecordsProcessed = 0,
                    Duration = TimeSpan.Zero,
                    Message = "Connection failed"
                });

                _logger.LogWarning("QuickBooks connection failed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during QuickBooks connection");
            StatusText = "Connection error";
            ErrorMessage = $"Connection error: {ex.Message}";

            AddSyncHistoryRecord(new QuickBooksSyncHistoryRecord
            {
                Timestamp = DateTime.Now,
                Operation = "Connect",
                Status = "Error",
                RecordsProcessed = 0,
                Duration = TimeSpan.Zero,
                Message = $"Error: {ex.Message}"
            });
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Disconnects from QuickBooks Online.
    /// </summary>
    private async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IsLoading = true;
            StatusText = "Disconnecting from QuickBooks...";
            ErrorMessage = null;

            _logger.LogInformation("Disconnecting from QuickBooks");

            await _quickBooksService.DisconnectAsync(cancellationToken);

            await CheckConnectionAsync(cancellationToken);
            StatusText = "Disconnected from QuickBooks";

            AddSyncHistoryRecord(new QuickBooksSyncHistoryRecord
            {
                Timestamp = DateTime.Now,
                Operation = "Disconnect",
                Status = "Success",
                RecordsProcessed = 0,
                Duration = TimeSpan.Zero,
                Message = "Disconnected successfully"
            });

            _logger.LogInformation("QuickBooks disconnected successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during QuickBooks disconnection");
            StatusText = "Disconnection error";
            ErrorMessage = $"Disconnection error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Tests the QuickBooks connection.
    /// </summary>
    private async Task TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IsLoading = true;
            StatusText = "Testing QuickBooks connection...";
            ErrorMessage = null;

            _logger.LogInformation("Testing QuickBooks connection");

            var success = await _quickBooksService.TestConnectionAsync();

            if (success)
            {
                StatusText = "Connection test successful!";
                AddSyncHistoryRecord(new QuickBooksSyncHistoryRecord
                {
                    Timestamp = DateTime.Now,
                    Operation = "Test Connection",
                    Status = "Success",
                    RecordsProcessed = 0,
                    Duration = TimeSpan.Zero,
                    Message = "Connection test passed"
                });
                _logger.LogInformation("QuickBooks connection test successful");
            }
            else
            {
                StatusText = "Connection test failed";
                ErrorMessage = "Connection test failed. Check your credentials.";
                AddSyncHistoryRecord(new QuickBooksSyncHistoryRecord
                {
                    Timestamp = DateTime.Now,
                    Operation = "Test Connection",
                    Status = "Failed",
                    RecordsProcessed = 0,
                    Duration = TimeSpan.Zero,
                    Message = "Connection test failed"
                });
                _logger.LogWarning("QuickBooks connection test failed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during connection test");
            StatusText = "Connection test error";
            ErrorMessage = $"Test error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanConnect() => !IsConnected && !IsLoading;
    private bool CanDisconnect() => IsConnected && !IsLoading;

    /// <inheritdoc />
    public async System.Threading.Tasks.Task<string> RunDiagnosticsAsync(System.Threading.CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var d = await _quickBooksService.RunDiagnosticsAsync(cancellationToken).ConfigureAwait(false);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("QuickBooks Sandbox Diagnostics");
            sb.AppendLine(new string('─', 40));
            sb.AppendLine($"Environment  : {d.Environment}");
            sb.AppendLine($"Redirect URI : {d.RedirectUri}");
            sb.AppendLine();
            sb.AppendLine("Credentials (presence only — values not shown)");
            sb.AppendLine($"  Client ID     : {(d.HasClientId ? "✓ present" : "✗ MISSING")}");
            sb.AppendLine($"  Client Secret : {(d.HasClientSecret ? "✓ present" : "✗ MISSING")}");
            sb.AppendLine($"  Realm ID      : {(d.HasRealmId ? "✓ present" : "✗ MISSING — set after first successful auth")}");
            sb.AppendLine();
            sb.AppendLine("URL ACL (HTTP listener registration)");
            sb.AppendLine($"  Registered : {(d.UrlAclRegistered ? "✓ YES" : "✗ NO — run: netsh http add urlacl url=" + d.UrlAclUrl + " user=%USERNAME%")}");
            sb.AppendLine($"  URL        : {d.UrlAclUrl}");
            sb.AppendLine();
            sb.AppendLine("OAuth Token");
            sb.AppendLine($"  Valid token : {(d.HasValidToken ? "✓ YES" : "✗ NO — click Connect to authorize")}");
            sb.AppendLine($"  Expires at  : {d.TokenExpiry}");
            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RunDiagnosticsAsync failed");
            ErrorMessage = $"Diagnostics error: {ex.Message}";
            return $"Diagnostics failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    #endregion

    #region Data Synchronization

    /// <summary>
    /// Synchronizes data from QuickBooks.
    /// </summary>
    private async Task SyncDataAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IsSyncing = true;
            IsLoading = true;
            SyncProgress = 0;
            StatusText = "Syncing data from QuickBooks...";
            ErrorMessage = null;

            _logger.LogInformation("Starting QuickBooks data sync");

            var startTime = DateTime.Now;

            // Simulate progress updates
            for (int i = 0; i <= 100; i += 20)
            {
                SyncProgress = i;
                await Task.Delay(200, cancellationToken); // Simulate work
            }

            var result = await _quickBooksService.SyncDataAsync(cancellationToken);

            var duration = DateTime.Now - startTime;

            if (result.Success)
            {
                int updatedCount = 0;

                // Refresh connection status first
                await CheckConnectionAsync(cancellationToken);

                // Attempt to sync budget actuals for current fiscal year (best-effort)
                try
                {
                    if (_quickBooksBudgetSyncService != null)
                    {
                        var currentFiscalYear = DateTime.Now.Month >= 7 ? DateTime.Now.Year + 1 : DateTime.Now.Year;
                        updatedCount = await _quickBooksBudgetSyncService.SyncFiscalYearActualsAsync(currentFiscalYear, cancellationToken);
                        if (updatedCount > 0)
                        {
                            AddSyncHistoryRecord(new QuickBooksSyncHistoryRecord
                            {
                                Timestamp = DateTime.Now,
                                Operation = "Budget Sync",
                                Status = "Success",
                                RecordsProcessed = updatedCount,
                                Duration = TimeSpan.Zero,
                                Message = $"Updated {updatedCount} budget entries for FY {currentFiscalYear}"
                            });
                        }
                    }
                }
                catch (Exception exBudget)
                {
                    _logger.LogWarning(exBudget, "Budget sync after QuickBooks sync failed");
                    AddSyncHistoryRecord(new QuickBooksSyncHistoryRecord
                    {
                        Timestamp = DateTime.Now,
                        Operation = "Budget Sync",
                        Status = "Failed",
                        RecordsProcessed = 0,
                        Duration = TimeSpan.Zero,
                        Message = exBudget.Message
                    });
                }

                StatusText = $"Sync complete: {result.RecordsSynced} records synced in {result.Duration.TotalSeconds:F1}s; Budget updates: {updatedCount} rows";

                AddSyncHistoryRecord(new QuickBooksSyncHistoryRecord
                {
                    Timestamp = DateTime.Now,
                    Operation = "Sync Data",
                    Status = "Success",
                    RecordsProcessed = result.RecordsSynced,
                    Duration = result.Duration,
                    Message = $"Synced {result.RecordsSynced} records; Budget updates: {updatedCount} rows"
                });

                _logger.LogInformation("QuickBooks sync completed: {RecordCount} records; budget updates {UpdatedCount}", result.RecordsSynced, updatedCount);
            }
            else
            {
                StatusText = $"Sync failed: {result.ErrorMessage}";
                ErrorMessage = result.ErrorMessage;

                AddSyncHistoryRecord(new QuickBooksSyncHistoryRecord
                {
                    Timestamp = DateTime.Now,
                    Operation = "Sync Data",
                    Status = "Failed",
                    RecordsProcessed = 0,
                    Duration = duration,
                    Message = result.ErrorMessage ?? "Sync failed"
                });

                _logger.LogWarning("QuickBooks sync failed: {Error}", result.ErrorMessage);
            }

            SyncProgress = 100;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during QuickBooks sync");
            StatusText = "Sync error";
            ErrorMessage = $"Sync error: {ex.Message}";

            AddSyncHistoryRecord(new QuickBooksSyncHistoryRecord
            {
                Timestamp = DateTime.Now,
                Operation = "Sync Data",
                Status = "Error",
                RecordsProcessed = 0,
                Duration = TimeSpan.Zero,
                Message = $"Error: {ex.Message}"
            });
        }
        finally
        {
            IsSyncing = false;
            IsLoading = false;
            SyncProgress = 0;
        }
    }

    /// <summary>
    /// Test sync of accounts from QuickBooks (pulls Chart of Accounts).
    /// </summary>
    private async Task SyncAccountsTestAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IsSyncing = true;
            IsLoading = true;
            SyncProgress = 0;
            StatusText = "Syncing accounts from QuickBooks...";
            ErrorMessage = null;

            _logger.LogInformation("Starting QuickBooks accounts test sync");

            var startTime = DateTime.Now;

            var result = await _quickBooksService.SyncAccountsAsync(cancellationToken);

            var duration = DateTime.Now - startTime;

            if (result.Success)
            {
                StatusText = result.RecordsSynced > 0
                    ? $"Account sync successful: {result.RecordsSynced} accounts synced in {result.Duration.TotalSeconds:F1}s"
                    : $"Account sync complete: No accounts found (check QuickBooks connection)";

                AddSyncHistoryRecord(new QuickBooksSyncHistoryRecord
                {
                    Timestamp = DateTime.Now,
                    Operation = "Sync Accounts (Test)",
                    Status = "Success",
                    RecordsProcessed = result.RecordsSynced,
                    Duration = result.Duration,
                    Message = result.ErrorMessage ?? $"Successfully synced {result.RecordsSynced} accounts"
                });

                _logger.LogInformation("Accounts test sync completed: {RecordCount} accounts", result.RecordsSynced);
            }
            else
            {
                StatusText = $"Account sync failed: {result.ErrorMessage}";
                ErrorMessage = result.ErrorMessage;

                AddSyncHistoryRecord(new QuickBooksSyncHistoryRecord
                {
                    Timestamp = DateTime.Now,
                    Operation = "Sync Accounts (Test)",
                    Status = "Failed",
                    RecordsProcessed = 0,
                    Duration = duration,
                    Message = result.ErrorMessage ?? "Account sync failed"
                });

                _logger.LogWarning("Accounts test sync failed: {Error}", result.ErrorMessage);
            }

            SyncProgress = 100;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Accounts sync was cancelled");
            StatusText = "Account sync cancelled";
            ErrorMessage = "Operation was cancelled";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during accounts test sync");
            StatusText = "Accounts sync error";
            ErrorMessage = $"Error: {ex.Message}";

            AddSyncHistoryRecord(new QuickBooksSyncHistoryRecord
            {
                Timestamp = DateTime.Now,
                Operation = "Sync Accounts (Test)",
                Status = "Error",
                RecordsProcessed = 0,
                Duration = TimeSpan.Zero,
                Message = $"Error: {ex.Message}"
            });
        }
        finally
        {
            IsSyncing = false;
            IsLoading = false;
            SyncProgress = 0;
        }
    }

    /// <summary>
    /// Imports the chart of accounts from QuickBooks.
    /// </summary>
    private async Task ImportAccountsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IsLoading = true;
            StatusText = "Importing chart of accounts...";
            ErrorMessage = null;

            _logger.LogInformation("Starting chart of accounts import");

            var result = await _quickBooksService.ImportChartOfAccountsAsync(cancellationToken);

            if (result.Success)
            {
                StatusText = $"Import complete: {result.AccountsImported} imported, {result.AccountsUpdated} updated, {result.AccountsSkipped} skipped";
                AccountsImported += result.AccountsImported;

                AddSyncHistoryRecord(new QuickBooksSyncHistoryRecord
                {
                    Timestamp = DateTime.Now,
                    Operation = "Import Accounts",
                    Status = "Success",
                    RecordsProcessed = result.AccountsImported + result.AccountsUpdated,
                    Duration = result.Duration,
                    Message = $"Imported: {result.AccountsImported}, Updated: {result.AccountsUpdated}, Skipped: {result.AccountsSkipped}"
                });

                _logger.LogInformation("Chart of accounts import completed: {Imported} imported, {Updated} updated",
                    result.AccountsImported, result.AccountsUpdated);
            }
            else
            {
                StatusText = $"Import failed: {result.ErrorMessage}";
                ErrorMessage = result.ErrorMessage;

                AddSyncHistoryRecord(new QuickBooksSyncHistoryRecord
                {
                    Timestamp = DateTime.Now,
                    Operation = "Import Accounts",
                    Status = "Failed",
                    RecordsProcessed = 0,
                    Duration = result.Duration,
                    Message = result.ErrorMessage ?? "Import failed"
                });

                _logger.LogWarning("Chart of accounts import failed: {Error}", result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during chart of accounts import");
            StatusText = "Import error";
            ErrorMessage = $"Import error: {ex.Message}";

            AddSyncHistoryRecord(new QuickBooksSyncHistoryRecord
            {
                Timestamp = DateTime.Now,
                Operation = "Import Accounts",
                Status = "Error",
                RecordsProcessed = 0,
                Duration = TimeSpan.Zero,
                Message = $"Error: {ex.Message}"
            });
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanSyncData() => IsConnected && !IsLoading && !IsSyncing;
    private bool CanImportAccounts() => IsConnected && !IsLoading;

    #endregion

    #region Sync History Management

    /// <summary>
    /// Adds a sync history record and updates summaries.
    /// </summary>
    private void AddSyncHistoryRecord(QuickBooksSyncHistoryRecord record)
    {
        SyncHistory.Insert(0, record); // Add to beginning for newest-first
        ApplyHistoryFilter();
        UpdateSummaries();
    }

    /// <summary>
    /// Refreshes the sync history from the service.
    /// </summary>
    private async Task RefreshHistoryAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IsLoading = true;
            StatusText = "Refreshing sync history...";

            await Task.Delay(500, cancellationToken); // Simulate refresh

            ApplyHistoryFilter();
            UpdateSummaries();
            StatusText = "Sync history refreshed";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh sync history");
            ErrorMessage = $"Refresh failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Clears the sync history.
    /// </summary>
    private void ClearHistory()
    {
        SyncHistory.Clear();
        FilteredSyncHistory.Clear();
        UpdateSummaries();
        StatusText = "Sync history cleared";
    }

    /// <summary>
    /// Exports sync history to CSV.
    /// </summary>
    private async Task ExportHistoryAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IsLoading = true;
            StatusText = "Exporting sync history...";

            string filePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                $"QuickBooksSyncHistory_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

            await using var writer = new StreamWriter(filePath, false, Encoding.UTF8);

            // Write CSV header
            await writer.WriteLineAsync("Timestamp,Operation,Status,Records Processed,Duration,Message");

            // Write data rows
            foreach (var record in FilteredSyncHistory)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var line = string.Join(",", new[]
                {
                    $"\"{record.FormattedTimestamp}\"",
                    $"\"{EscapeCsvField(record.Operation)}\"",
                    $"\"{EscapeCsvField(record.Status)}\"",
                    record.RecordsProcessed.ToString(CultureInfo.InvariantCulture),
                    $"\"{record.FormattedDuration}\"",
                    $"\"{EscapeCsvField(record.Message)}\""
                });

                await writer.WriteLineAsync(line);
            }

            StatusText = $"Exported {FilteredSyncHistory.Count} records to {Path.GetFileName(filePath)}";
            _logger.LogInformation("Exported {Count} sync history records to {File}", FilteredSyncHistory.Count, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export sync history");
            ErrorMessage = $"Export failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field))
            return string.Empty;

        // If field contains comma, quote, or newline, wrap in quotes and escape quotes
        if (field.Contains(",", StringComparison.Ordinal) ||
            field.Contains("\"", StringComparison.Ordinal) ||
            field.Contains("\n", StringComparison.Ordinal) ||
            field.Contains("\r", StringComparison.Ordinal))
        {
            return "\"" + field.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }

        return field;
    }

    /// <summary>
    /// Applies filtering to sync history based on filter text.
    /// </summary>
    private void ApplyHistoryFilter()
    {
        FilteredSyncHistory.Clear();

        var filtered = string.IsNullOrWhiteSpace(FilterText)
            ? SyncHistory
            : SyncHistory.Where(r =>
                (r.Operation?.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (r.Status?.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (r.Message?.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ?? false));

        foreach (var record in filtered)
        {
            FilteredSyncHistory.Add(record);
        }
    }

    /// <summary>
    /// Handles property changes for filtering.
    /// </summary>
    private void OnPropertyChangedForFiltering(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FilterText))
        {
            ApplyHistoryFilter();
        }
    }

    #endregion

    #region Summary Calculations

    /// <summary>
    /// Updates summary statistics from sync history.
    /// </summary>
    private void UpdateSummaries()
    {
        TotalSyncs = SyncHistory.Count;
        SuccessfulSyncs = SyncHistory.Count(r => r.Status == "Success");
        FailedSyncs = SyncHistory.Count(r => r.Status == "Failed" || r.Status == "Error");
        TotalRecordsSynced = SyncHistory.Sum(r => r.RecordsProcessed);

        var syncDurations = SyncHistory
            .Where(r => r.Duration.TotalSeconds > 0)
            .Select(r => r.Duration.TotalSeconds)
            .ToList();

        AverageSyncDuration = syncDurations.Any() ? syncDurations.Average() : 0;
    }

    #endregion

    #region Connection Polling

    /// <summary>
    /// Starts polling the connection status every 30 seconds with structured error handling.
    /// Uses background thread timer with SynchronizationContext marshaling for UI updates.
    /// CRITICAL: Captures UI context at startup and uses it to marshal observable property updates.
    /// </summary>
    private void StartConnectionPolling()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        var uiContext = SynchronizationContext.Current; // Capture UI context once

        _connectionPollingTimer = new System.Threading.Timer(
            async _ =>
            {
                if (_disposed || _cancellationTokenSource?.IsCancellationRequested == true)
                {
                    _logger.LogDebug("Connection polling callback skipped (disposed or cancelled)");
                    return;
                }

                try
                {
                    // Timer callback runs on background thread
                    // Marshal CheckConnectionAsync to UI context to update observable properties safely
                    if (uiContext != null)
                    {
                        uiContext.Post(async _ =>
                        {
                            try
                            {
                                await CheckConnectionAsync(_cancellationTokenSource.Token);
                            }
                            catch (ObjectDisposedException)
                            {
                                _logger.LogDebug("Connection polling skipped (ViewModel disposed)");
                            }
                            catch (OperationCanceledException)
                            {
                                _logger.LogDebug("Connection polling cancelled");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Connection polling failed: {ErrorMessage}", ex.Message);
                            }
                        }, null);
                    }
                    else
                    {
                        // Fallback: no UI context available (shouldn't happen in normal WinForms)
                        await CheckConnectionAsync(_cancellationTokenSource.Token);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Connection polling outer handler failed: {ErrorMessage}", ex.Message);
                }
            },
            null,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(30));

        _logger.LogDebug("Connection polling started (30s interval)");
    }

    /// <summary>
    /// Stops connection polling.
    /// </summary>
    private void StopConnectionPolling()
    {
        _cancellationTokenSource?.Cancel();
        _connectionPollingTimer?.Dispose();
        _connectionPollingTimer = null;
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        _logger.LogDebug("Connection polling stopped");
    }

    #endregion

    #region Empty-State Helpers

    /// <summary>
    /// Resets sync history to an empty state when no production history source is configured.
    /// </summary>
    private void ResetSyncHistoryState()
    {
        _logger.LogInformation("ResetSyncHistoryState called: no persisted QuickBooks history source is configured.");
        SyncHistory.Clear();
        ApplyHistoryFilter();
        UpdateSummaries();
        StatusText = "No QuickBooks history loaded yet";
    }

    #endregion

    #region Disposal

    /// <summary>
    /// Disposes resources used by the ViewModel.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Protected implementation of Dispose pattern.
    /// </summary>
    private void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            StopConnectionPolling();
        }

        _disposed = true;
        _logger.LogDebug("QuickBooksViewModel disposed");
    }

    #endregion
}

/// <summary>
/// Represents a single QuickBooks sync history record.
/// </summary>
public sealed class QuickBooksSyncHistoryRecord
{
    /// <summary>Gets or sets the timestamp of the operation.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Gets or sets the operation type (Connect, Sync Data, Import Accounts, etc.).</summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>Gets or sets the operation status (Success, Failed, Error).</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Gets or sets the number of records processed.</summary>
    public int RecordsProcessed { get; set; }

    /// <summary>Gets or sets the operation duration.</summary>
    public TimeSpan Duration { get; set; }

    /// <summary>Gets or sets the operation message or details.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Gets the formatted timestamp for display.</summary>
    public string FormattedTimestamp => Timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    /// <summary>Gets the formatted duration for display.</summary>
    public string FormattedDuration => Duration.TotalSeconds > 0
        ? $"{Duration.TotalSeconds:F1}s"
        : "-";
}
