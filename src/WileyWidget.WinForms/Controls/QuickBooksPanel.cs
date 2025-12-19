using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.WinForms.Controls;

/// <summary>
/// QuickBooks integration panel that provides access to QuickBooks Online data and operations.
/// Allows users to connect to QuickBooks, sync data, and manage QuickBooks integration.
/// </summary>
[SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
public sealed class QuickBooksPanel : UserControl
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<QuickBooksPanel> _logger;
    private readonly IQuickBooksService _quickBooksService;

    // UI Controls
    private Label? _statusLabel;
    private Button? _connectButton;
    private Button? _disconnectButton;
    private Button? _syncButton;
    private Button? _importAccountsButton;
    private TextBox? _statusTextBox;
    private ProgressBar? _progressBar;
    private GroupBox? _connectionGroup;
    private GroupBox? _operationsGroup;
    private bool _isConnected;

    /// <summary>
    /// Constructor accepting service provider for DI resolution.
    /// </summary>
    public QuickBooksPanel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

        _logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<QuickBooksPanel>>(serviceProvider);
        _quickBooksService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IQuickBooksService>(serviceProvider);

        InitializeComponent();
        _logger.LogInformation("QuickBooksPanel created");
    }

    private void InitializeComponent()
    {
        // Panel properties - theme inherited from parent via SkinManager cascade
        Name = "QuickBooksPanel";
        Dock = DockStyle.Fill;
        AutoScroll = true;
        Padding = new Padding(20);

        // Connection Status Group
        _connectionGroup = new GroupBox
        {
            Text = "Connection Status",
            Location = new Point(20, 20),
            Size = new Size(540, 120),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            TabIndex = 0,
            AccessibleName = "Connection Status Group"
        };

        _statusLabel = new Label
        {
            Text = "Checking connection...",
            Location = new Point(20, 30),
            Size = new Size(200, 20),
            AutoSize = false,
            TabIndex = 1,
            AccessibleName = "Connection Status Label"
        };

        _connectButton = new Button
        {
            Text = "Connect",
            Location = new Point(20, 60),
            Size = new Size(100, 35),
            Enabled = false,
            TabIndex = 2,
            AccessibleName = "Connect to QuickBooks Button",
            AccessibleDescription = "Establish connection to QuickBooks Online"
        };
        _connectButton.Click += async (s, e) => await ConnectAsync();

        _disconnectButton = new Button
        {
            Text = "Disconnect",
            Location = new Point(130, 60),
            Size = new Size(100, 35),
            Enabled = false,
            TabIndex = 3,
            AccessibleName = "Disconnect from QuickBooks Button",
            AccessibleDescription = "Disconnect from QuickBooks Online"
        };
        _disconnectButton.Click += async (s, e) => await DisconnectAsync();

        _connectionGroup.Controls.AddRange(new Control[] { _statusLabel, _connectButton, _disconnectButton });

        // Operations Group
        _operationsGroup = new GroupBox
        {
            Text = "Operations",
            Location = new Point(20, 150),
            Size = new Size(540, 120),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            TabIndex = 4,
            AccessibleName = "QuickBooks Operations Group"
        };

        _syncButton = new Button
        {
            Text = "Sync Data",
            Location = new Point(20, 30),
            Size = new Size(120, 35),
            Enabled = false,
            TabIndex = 5,
            AccessibleName = "Sync QuickBooks Data Button",
            AccessibleDescription = "Synchronize data from QuickBooks Online"
        };
        _syncButton.Click += async (s, e) => await SyncDataAsync();

        _importAccountsButton = new Button
        {
            Text = "Import Chart of Accounts",
            Location = new Point(150, 30),
            Size = new Size(160, 35),
            Enabled = false,
            TabIndex = 6,
            AccessibleName = "Import Chart of Accounts Button",
            AccessibleDescription = "Import account structure from QuickBooks"
        };
        _importAccountsButton.Click += async (s, e) => await ImportAccountsAsync();

        _operationsGroup.Controls.AddRange(new Control[] { _syncButton, _importAccountsButton });

        // Status Text Box
        _statusTextBox = new TextBox
        {
            Location = new Point(20, 280),
            Size = new Size(540, 150),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            TabIndex = 7,
            AccessibleName = "Status Log Text Box",
            AccessibleDescription = "Operation status and log messages"
        };

        // Progress Bar
        _progressBar = new ProgressBar
        {
            Location = new Point(20, 440),
            Size = new Size(540, 25),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            Visible = false,
            TabIndex = 8,
            AccessibleName = "Operation Progress Bar"
        };

        // Add controls to panel
        Controls.AddRange(new Control[] { _connectionGroup, _operationsGroup, _statusTextBox, _progressBar });

        _logger.LogInformation("QuickBooksPanel UI initialized");
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        // Check initial connection status
        await UpdateConnectionStatusAsync();
    }

    private async Task UpdateConnectionStatusAsync()
    {
        try
        {
            SetProgressVisible(true);
            UpdateStatus("Checking QuickBooks connection...");

            var connectionStatus = await _quickBooksService.GetConnectionStatusAsync();

            _isConnected = connectionStatus.IsConnected;

            if (connectionStatus.IsConnected)
            {
                _statusLabel!.Text = $"Connected to: {connectionStatus.CompanyName ?? "Unknown Company"}";
                _statusLabel!.ForeColor = Color.Green; // Semantic status color
                _connectButton!.Enabled = false;
                _disconnectButton!.Enabled = true;
                _syncButton!.Enabled = true;
                _importAccountsButton!.Enabled = true;
                UpdateStatus($"Connected to QuickBooks. Last sync: {connectionStatus.LastSyncTime ?? "Never"}");
            }
            else
            {
                _statusLabel!.Text = "Not Connected";
                _statusLabel!.ForeColor = Color.Red; // Semantic status color
                _connectButton!.Enabled = true;
                _disconnectButton!.Enabled = false;
                _syncButton!.Enabled = false;
                _importAccountsButton!.Enabled = false;
                UpdateStatus("Not connected to QuickBooks. Click 'Connect' to establish connection.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking connection status");
            _statusLabel!.Text = "Error checking status";
            _statusLabel!.ForeColor = Color.Red; // Semantic status color
            UpdateStatus($"Error checking connection: {ex.Message}");
        }
        finally
        {
            SetProgressVisible(false);
        }
    }

    private async Task ConnectAsync()
    {
        try
        {
            SetProgressVisible(true);
            UpdateStatus("Connecting to QuickBooks...");
            SetButtonsEnabled(false);

            var success = await _quickBooksService.ConnectAsync();
            if (success)
            {
                UpdateStatus("Successfully connected to QuickBooks!");
                await UpdateConnectionStatusAsync();
            }
            else
            {
                UpdateStatus("Failed to connect to QuickBooks. Check your configuration.");
                MessageBox.Show(
                    "Failed to connect to QuickBooks. Please check your API credentials and try again.",
                    "Connection Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting to QuickBooks");
            UpdateStatus($"Connection error: {ex.Message}");
            MessageBox.Show(
                $"Error connecting to QuickBooks: {ex.Message}",
                "Connection Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            SetProgressVisible(false);
            SetButtonsEnabled(true);
        }
    }

    private async Task DisconnectAsync()
    {
        try
        {
            SetProgressVisible(true);
            UpdateStatus("Disconnecting from QuickBooks...");
            SetButtonsEnabled(false);

            await _quickBooksService.DisconnectAsync();
            UpdateStatus("Disconnected from QuickBooks.");
            await UpdateConnectionStatusAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting from QuickBooks");
            UpdateStatus($"Disconnect error: {ex.Message}");
        }
        finally
        {
            SetProgressVisible(false);
            SetButtonsEnabled(true);
        }
    }

    private async Task SyncDataAsync()
    {
        try
        {
            SetProgressVisible(true);
            UpdateStatus("Syncing data from QuickBooks...");
            SetButtonsEnabled(false);

            var result = await _quickBooksService.SyncDataAsync();
            if (result.Success)
            {
                UpdateStatus($"Successfully synced {result.RecordsSynced} records from QuickBooks in {result.Duration.TotalSeconds:F1} seconds.");
                MessageBox.Show(
                    $"Successfully synced {result.RecordsSynced} records from QuickBooks.",
                    "Sync Complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                UpdateStatus($"Sync failed: {result.ErrorMessage}");
                MessageBox.Show(
                    $"Sync failed: {result.ErrorMessage}",
                    "Sync Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing data from QuickBooks");
            UpdateStatus($"Sync error: {ex.Message}");
            MessageBox.Show(
                $"Error syncing data: {ex.Message}",
                "Sync Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            SetProgressVisible(false);
            SetButtonsEnabled(true);
        }
    }

    private async Task ImportAccountsAsync()
    {
        try
        {
            SetProgressVisible(true);
            UpdateStatus("Importing chart of accounts from QuickBooks...");
            SetButtonsEnabled(false);

            var result = await _quickBooksService.ImportChartOfAccountsAsync();
            if (result.Success)
            {
                UpdateStatus($"Successfully imported {result.AccountsImported} accounts, updated {result.AccountsUpdated}, skipped {result.AccountsSkipped} in {result.Duration.TotalSeconds:F1} seconds.");
                MessageBox.Show(
                    $"Import complete:\n\nImported: {result.AccountsImported}\nUpdated: {result.AccountsUpdated}\nSkipped: {result.AccountsSkipped}",
                    "Import Complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                UpdateStatus($"Import failed: {result.ErrorMessage}");
                MessageBox.Show(
                    $"Import failed: {result.ErrorMessage}",
                    "Import Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing chart of accounts");
            UpdateStatus($"Import error: {ex.Message}");
            MessageBox.Show(
                $"Error importing accounts: {ex.Message}",
                "Import Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            SetProgressVisible(false);
            SetButtonsEnabled(true);
        }
    }

    private void UpdateStatus(string message)
    {
        if (_statusTextBox != null && !_statusTextBox.IsDisposed && !IsDisposed)
        {
            if (InvokeRequired)
            {
                Invoke(() => _statusTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\r\n"));
            }
            else
            {
                _statusTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\r\n");
            }
        }
    }

    private void SetProgressVisible(bool visible)
    {
        if (_progressBar != null && !_progressBar.IsDisposed && !IsDisposed)
        {
            if (InvokeRequired)
            {
                Invoke(() => _progressBar.Visible = visible);
            }
            else
            {
                _progressBar.Visible = visible;
            }
        }
    }

    private void SetButtonsEnabled(bool enabled)
    {
        if (IsDisposed) return;

        if (InvokeRequired)
        {
            Invoke(() =>
            {
                if (!IsDisposed)
                {
                    _connectButton!.Enabled = enabled && !_isConnected;
                    _disconnectButton!.Enabled = enabled && _isConnected;
                    _syncButton!.Enabled = enabled && _isConnected;
                    _importAccountsButton!.Enabled = enabled && _isConnected;
                }
            });
        }
        else
        {
            _connectButton!.Enabled = enabled && !_isConnected;
            _disconnectButton!.Enabled = enabled && _isConnected;
            _syncButton!.Enabled = enabled && _isConnected;
            _importAccountsButton!.Enabled = enabled && _isConnected;
        }
    }

    /// <summary>
    /// Cleanup panel resources.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _statusLabel?.Dispose();
            _connectButton?.Dispose();
            _disconnectButton?.Dispose();
            _syncButton?.Dispose();
            _importAccountsButton?.Dispose();
            _statusTextBox?.Dispose();
            _progressBar?.Dispose();
            _connectionGroup?.Dispose();
            _operationsGroup?.Dispose();
        }

        base.Dispose(disposing);
    }
}
