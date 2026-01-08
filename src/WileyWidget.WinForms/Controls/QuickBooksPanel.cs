using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.WinForms.DataGrid.Events;
using Syncfusion.WinForms.DataGrid.Styles;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Extensions;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.Drawing;

namespace WileyWidget.WinForms.Controls;

/// <summary>
/// QuickBooks integration panel with full MVVM pattern, connection management, and sync history tracking.
/// Inherits from ScopedPanelBase for proper DI lifecycle management.
/// Provides connection status monitoring, data synchronization, and import operations.
/// </summary>
[SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
public sealed class QuickBooksPanel : ScopedPanelBase<QuickBooksViewModel>
{
    #region UI Controls

    private PanelHeader? _panelHeader;
    private LoadingOverlay? _loadingOverlay;
    private NoDataOverlay? _noDataOverlay;
    private StatusStrip? _statusStrip;
    private ToolStripStatusLabel? _statusLabel;
    private ToolStripProgressBar? _progressBar;

    private TableLayoutPanel? _mainLayout;
    private SplitContainer? _mainSplitContainer;

    // Connection Panel
    private GradientPanelExt? _connectionPanel;
    private Label? _connectionStatusLabel;
    private Label? _companyNameLabel;
    private Label? _lastSyncLabel;
    private SfButton? _connectButton;
    private SfButton? _disconnectButton;
    private SfButton? _testConnectionButton;

    // Operations Panel
    private GradientPanelExt? _operationsPanel;
    private SfButton? _syncDataButton;
    private SfButton? _importAccountsButton;
    private SfButton? _refreshHistoryButton;
    private SfButton? _clearHistoryButton;
    private SfButton? _exportHistoryButton;
    private Syncfusion.Windows.Forms.Tools.ProgressBarAdv? _syncProgressBar;

    // Sync History Grid
    private SfDataGrid? _syncHistoryGrid;
    private TextBoxExt? _filterTextBox;

    // Summary Panel
    private GradientPanelExt? _summaryPanel;
    private Label? _totalSyncsLabel;
    private Label? _successfulSyncsLabel;
    private Label? _failedSyncsLabel;
    private Label? _totalRecordsLabel;
    private Label? _accountsImportedLabel;
    private Label? _avgDurationLabel;

    #endregion

    /// <summary>
    /// Initializes a new instance of the <see cref="QuickBooksPanel"/> class.
    /// </summary>
    /// <param name="scopeFactory">Service scope factory for DI.</param>
    /// <param name="logger">Logger instance.</param>
    public QuickBooksPanel(
        IServiceScopeFactory scopeFactory,
        ILogger<ScopedPanelBase<QuickBooksViewModel>> logger)
        : base(scopeFactory, logger)
    {
    }


    /// <summary>
    /// Called after the ViewModel has been resolved. Initializes UI and bindings.
    /// </summary>
    protected override void OnViewModelResolved(QuickBooksViewModel viewModel)
    {
        base.OnViewModelResolved(viewModel);

        InitializeControls();
        BindViewModel();
        ApplySyncfusionTheme();

        Logger.LogDebug("QuickBooksPanel: ViewModel resolved and UI initialized");
    }

    /// <summary>
    /// Loads the panel and initializes the ViewModel.
    /// </summary>
    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        if (ViewModel != null && !DesignMode)
        {
            try
            {
                await ViewModel.InitializeAsync();
                UpdateLoadingState();
                UpdateNoDataOverlay();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to initialize QuickBooksPanel");
            }
        }
    }

    #region Control Initialization

    /// <summary>
    /// Initializes all UI controls and layout.
    /// </summary>
    private void InitializeControls()
    {
        SuspendLayout();

        Name = "QuickBooksPanel";
        Size = new Size(1400, 900);
        MinimumSize = new Size((int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(800f), (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(600f));
        AutoScroll = true;
        Padding = new Padding(8);
        // DockingManager will handle docking; do not set Dock here.

        // Panel header
        _panelHeader = new PanelHeader
        {
            Dock = DockStyle.Top,
            Height = 50
        };
        _panelHeader.Title = "QuickBooks Integration";
        _panelHeader.RefreshClicked += async (s, e) => await RefreshAsync();
        _panelHeader.CloseClicked += (s, e) => ClosePanel();
        Controls.Add(_panelHeader);

        // Main layout container
        _mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1
            // BackColor removed - let SkinManager handle theming
        };
        _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));   // Summary
        _mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // Split container
        _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));   // Status bar

        // Create summary panel
        CreateSummaryPanel();
        if (_summaryPanel == null) {
            _summaryPanel = new GradientPanelExt { Dock = DockStyle.Fill, Padding = new Padding(10,10,10,5), BorderStyle = BorderStyle.None, BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty) };
            SfSkinManager.SetVisualStyle(_summaryPanel, "Office2019Colorful");
        }
        _mainLayout.Controls.Add(_summaryPanel!, 0, 0);

        // Create main split container
        CreateMainContent();
        _mainLayout.Controls.Add(_mainSplitContainer!, 0, 1);

        // Create status strip
        CreateStatusStrip();
        _mainLayout.Controls.Add(_statusStrip!, 0, 2);

        Controls.Add(_mainLayout);

        // Create overlays
        _loadingOverlay = new LoadingOverlay
        {
            Message = "Loading QuickBooks data...",
            Visible = false
        };
        Controls.Add(_loadingOverlay);
        _loadingOverlay.BringToFront();

        _noDataOverlay = new NoDataOverlay
        {
            Message = "No sync history yet\r\nConnect and sync data with QuickBooks to get started",
            Visible = false
        };
        Controls.Add(_noDataOverlay);
        _noDataOverlay.BringToFront();

        ResumeLayout(false);
        PerformLayout();
    }

    /// <summary>
    /// Creates the summary panel with KPI metrics.
    /// </summary>
    private void CreateSummaryPanel()
    {
        _summaryPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            // BackColor removed - let SkinManager handle theming
            Padding = new Padding(10, 10, 10, 5),
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(_summaryPanel, "Office2019Colorful");

        var flowPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = true
        };

        // Create KPI cards
        _totalSyncsLabel = CreateMetricCard("Total Syncs", "0");
        _successfulSyncsLabel = CreateMetricCard("Successful", "0");
        _failedSyncsLabel = CreateMetricCard("Failed", "0");
        _totalRecordsLabel = CreateMetricCard("Records Synced", "0");
        _accountsImportedLabel = CreateMetricCard("Accounts Imported", "0");
        _avgDurationLabel = CreateMetricCard("Avg Duration", "0s");

        flowPanel.Controls.Add(_totalSyncsLabel.Parent!);
        flowPanel.Controls.Add(_successfulSyncsLabel.Parent!);
        flowPanel.Controls.Add(_failedSyncsLabel.Parent!);
        flowPanel.Controls.Add(_totalRecordsLabel.Parent!);
        flowPanel.Controls.Add(_accountsImportedLabel.Parent!);
        flowPanel.Controls.Add(_avgDurationLabel.Parent!);

        _summaryPanel.Controls.Add(flowPanel);
    }

    /// <summary>
    /// Creates a metric card for the summary panel.
    /// </summary>
    private Label CreateMetricCard(string title, string value)
    {
        var cardPanel = new GradientPanelExt
        {
            Size = new Size(200, 60),
            // BackColor removed - let SkinManager handle theming
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(5),
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(cardPanel, "Office2019Colorful");

        var titleLabel = new Label
        {
            Text = title,
            Location = new Point(10, 8),
            Size = new Size(180, 18),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular)
            // ForeColor removed - let SkinManager handle theming
        };

        var valueLabel = new Label
        {
            Text = value,
            Location = new Point(10, 28),
            Size = new Size(180, 24),
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            // ForeColor removed - let SkinManager handle theming
        };

        cardPanel.Controls.Add(titleLabel);
        cardPanel.Controls.Add(valueLabel);

        return valueLabel;
    }

    /// <summary>
    /// Creates the main split container with connection/operations and sync history.
    /// </summary>
    private void CreateMainContent()
    {
        _mainSplitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 350,
            FixedPanel = FixedPanel.Panel1
        };

        // Top panel: Connection and Operations
        CreateConnectionAndOperationsPanel();
        _mainSplitContainer.Panel1.Controls.Add(CreateTopPanel());

        // Bottom panel: Sync History Grid
        CreateSyncHistoryGrid();
        _mainSplitContainer.Panel2.Controls.Add(CreateHistoryPanel());

        _mainSplitContainer.Panel1.Padding = new Padding(10);
        _mainSplitContainer.Panel2.Padding = new Padding(10);
    }

    /// <summary>
    /// Creates the top panel with connection status and operations.
    /// </summary>
    private GradientPanelExt CreateTopPanel()
    {
        var topPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(topPanel, "Office2019Colorful");

        var splitTop = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 450
        };

        splitTop.Panel1.Controls.Add(_connectionPanel!);
        splitTop.Panel2.Controls.Add(_operationsPanel!);

        topPanel.Controls.Add(splitTop);
        return topPanel;
    }

    /// <summary>
    /// Creates the connection status panel.
    /// </summary>
    private void CreateConnectionAndOperationsPanel()
    {
        // Connection Panel
        _connectionPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            // BackColor removed - let SkinManager handle theming
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(_connectionPanel, "Office2019Colorful");

        var connectionPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10)
        };

        var connectionLabel = new Label
        {
            Text = "Connection Status",
            Location = new Point(5, 5),
            Size = new Size(200, 22),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };
        connectionPanel.Controls.Add(connectionLabel);

        int yPos = 35;

        _connectionStatusLabel = new Label
        {
            Text = "Status: Checking...",
            Location = new Point(15, yPos),
            Size = new Size(400, 22),
            Font = new Font("Segoe UI", 10f, FontStyle.Regular)
        };
        connectionPanel.Controls.Add(_connectionStatusLabel);
        yPos += 28;

        _companyNameLabel = new Label
        {
            Text = "Company: -",
            Location = new Point(15, yPos),
            Size = new Size(400, 20),
            Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            // ForeColor removed - let SkinManager handle theming
        };
        connectionPanel.Controls.Add(_companyNameLabel);
        yPos += 25;

        _lastSyncLabel = new Label
        {
            Text = "Last Sync: -",
            Location = new Point(15, yPos),
            Size = new Size(400, 20),
            Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            // ForeColor removed - let SkinManager handle theming
        };
        connectionPanel.Controls.Add(_lastSyncLabel);
        yPos += 35;

        // Connection buttons
        int xPos = 15;
        _connectButton = new SfButton
        {
            Text = "Connect",
            Location = new Point(xPos, yPos),
            Size = new Size(100, 35),
            // Style colors removed - let SkinManager handle theming
            AccessibleName = "Connect to QuickBooks",
            AccessibleDescription = "Establishes connection to QuickBooks Online"
        };
        _connectButton.Click += async (s, e) => await ExecuteCommandAsync(ViewModel?.ConnectCommand);
        connectionPanel.Controls.Add(_connectButton);
        xPos += 110;

        _disconnectButton = new SfButton
        {
            Text = "Disconnect",
            Location = new Point(xPos, yPos),
            Size = new Size(110, 35),
            AccessibleName = "Disconnect from QuickBooks",
            AccessibleDescription = "Terminates current QuickBooks Online connection"
        };
        _disconnectButton.Click += async (s, e) => await ExecuteCommandAsync(ViewModel?.DisconnectCommand);
        connectionPanel.Controls.Add(_disconnectButton);
        xPos += 120;

        _testConnectionButton = new SfButton
        {
            Text = "Test Connection",
            Location = new Point(xPos, yPos),
            Size = new Size(130, 35),
            AccessibleName = "Test QuickBooks Connection",
            AccessibleDescription = "Verifies QuickBooks Online connection status"
        };
        _testConnectionButton.Click += async (s, e) => await ExecuteCommandAsync(ViewModel?.TestConnectionCommand);
        connectionPanel.Controls.Add(_testConnectionButton);

        _connectionPanel.Controls.Add(connectionPanel);

        // Operations Panel
        _operationsPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            // BackColor removed - let SkinManager handle theming
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(_operationsPanel, "Office2019Colorful");

        var operationsPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10)
        };

        var operationsLabel = new Label
        {
            Text = "QuickBooks Operations",
            Location = new Point(5, 5),
            Size = new Size(200, 22),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };
        operationsPanel.Controls.Add(operationsLabel);

        yPos = 35;

        _syncDataButton = new SfButton
        {
            Text = "🔄 Sync Data",
            Location = new Point(15, yPos),
            Size = new Size(140, 40),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            // Style colors removed - let SkinManager handle theming
            AccessibleName = "Sync Data with QuickBooks",
            AccessibleDescription = "Synchronizes financial data between Wiley Widget and QuickBooks Online"
        };
        _syncDataButton.Click += async (s, e) => await ExecuteCommandAsync(ViewModel?.SyncDataCommand);
        operationsPanel.Controls.Add(_syncDataButton);
        yPos += 50;

        _importAccountsButton = new SfButton
        {
            Text = "📥 Import Chart of Accounts",
            Location = new Point(15, yPos),
            Size = new Size(220, 40),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
            AccessibleName = "Import Chart of Accounts",
            AccessibleDescription = "Imports complete chart of accounts from QuickBooks Online"
        };
        _importAccountsButton.Click += async (s, e) => await ExecuteCommandAsync(ViewModel?.ImportAccountsCommand);
        operationsPanel.Controls.Add(_importAccountsButton);
        yPos += 55;

        // Sync progress bar
        _syncProgressBar = new Syncfusion.Windows.Forms.Tools.ProgressBarAdv
        {
            Location = new Point(15, yPos),
            Size = new Size(400, 25),
            Visible = false,
            ProgressStyle = Syncfusion.Windows.Forms.Tools.ProgressBarStyles.WaitingGradient
        };
        operationsPanel.Controls.Add(_syncProgressBar);

        _operationsPanel.Controls.Add(operationsPanel);
    }

    /// <summary>
    /// Creates the sync history panel with grid and filter.
    /// </summary>
    private GradientPanelExt CreateHistoryPanel()
    {
        var historyPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(historyPanel, "Office2019Colorful");

        var headerPanel = new GradientPanelExt
        {
            Dock = DockStyle.Top,
            Height = 45,
            Padding = new Padding(0, 5, 0, 5),
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(headerPanel, "Office2019Colorful");

        var titleLabel = new Label
        {
            Text = "Sync History",
            Location = new Point(5, 12),
            Size = new Size(120, 22),
            Font = new Font("Segoe UI", 10f, FontStyle.Bold)
        };
        headerPanel.Controls.Add(titleLabel);

        int xPos = 130;
        var filterLabel = new Label
        {
            Text = "Filter:",
            Location = new Point(xPos, 14),
            Size = new Size(45, 20)
        };
        headerPanel.Controls.Add(filterLabel);
        xPos += 50;

        _filterTextBox = new TextBoxExt
        {
            Location = new Point(xPos, 11),
            Size = new Size(200, 25)
            // NullText removed: property does not exist on TextBoxExt
        };
        _filterTextBox.TextChanged += (s, e) =>
        {
            if (ViewModel != null)
                ViewModel.FilterText = _filterTextBox.Text;
        };
        headerPanel.Controls.Add(_filterTextBox);
        xPos += 210;

        _refreshHistoryButton = new SfButton
        {
            Text = "🔄 Refresh",
            Location = new Point(xPos, 9),
            Size = new Size(95, 28),
            AccessibleName = "Refresh Sync History",
            AccessibleDescription = "Reloads sync history from database"
        };
        _refreshHistoryButton.Click += async (s, e) => await ExecuteCommandAsync(ViewModel?.RefreshHistoryCommand);
        headerPanel.Controls.Add(_refreshHistoryButton);
        xPos += 105;

        _clearHistoryButton = new SfButton
        {
            Text = "🗑 Clear",
            Location = new Point(xPos, 9),
            Size = new Size(85, 28),
            AccessibleName = "Clear Sync History",
            AccessibleDescription = "Removes all sync history records from the display"
        };
        _clearHistoryButton.Click += (s, e) => ViewModel?.ClearHistoryCommand.Execute(null);
        headerPanel.Controls.Add(_clearHistoryButton);
        xPos += 95;

        _exportHistoryButton = new SfButton
        {
            Text = "📤 Export CSV",
            Location = new Point(xPos, 9),
            Size = new Size(110, 28),
            AccessibleName = "Export History to CSV",
            AccessibleDescription = "Exports sync history data to CSV file"
        };
        _exportHistoryButton.Click += async (s, e) => await ExecuteCommandAsync(ViewModel?.ExportHistoryCommand);
        headerPanel.Controls.Add(_exportHistoryButton);

        historyPanel.Controls.Add(_syncHistoryGrid!);
        historyPanel.Controls.Add(headerPanel);

        return historyPanel;
    }

    /// <summary>
    /// Creates the sync history data grid.
    /// </summary>
    private void CreateSyncHistoryGrid()
    {
        _syncHistoryGrid = new SfDataGrid
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowResizingColumns = true,
            AllowSorting = true,
            AllowFiltering = false,
            SelectionMode = GridSelectionMode.Single,
            NavigationMode = Syncfusion.WinForms.DataGrid.Enums.NavigationMode.Row,
            RowHeight = 32,
            HeaderRowHeight = 38
        };

        // Define columns
        _syncHistoryGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(QuickBooksSyncHistoryRecord.FormattedTimestamp),
            HeaderText = "Timestamp",
            Width = 180,
            AllowSorting = true
        });

        _syncHistoryGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(QuickBooksSyncHistoryRecord.Operation),
            HeaderText = "Operation",
            Width = 150,
            AllowSorting = true
        });

        _syncHistoryGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(QuickBooksSyncHistoryRecord.Status),
            HeaderText = "Status",
            Width = 100,
            AllowSorting = true
        });

        _syncHistoryGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = nameof(QuickBooksSyncHistoryRecord.RecordsProcessed),
            HeaderText = "Records",
            Width = 100,
            Format = "N0",
            AllowSorting = true
        });

        _syncHistoryGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(QuickBooksSyncHistoryRecord.FormattedDuration),
            HeaderText = "Duration",
            Width = 100,
            AllowSorting = true
        });

        _syncHistoryGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(QuickBooksSyncHistoryRecord.Message),
            HeaderText = "Message",
            Width = 400,
            AllowSorting = false
        });

        _syncHistoryGrid.SelectionChanged += (s, e) =>
        {
            if (ViewModel != null && _syncHistoryGrid.SelectedItem is QuickBooksSyncHistoryRecord record)
            {
                ViewModel.SelectedSyncRecord = record;
            }
        };
    }

    /// <summary>
    /// Creates the status strip at the bottom.
    /// </summary>
    private void CreateStatusStrip()
    {
        _statusStrip = new StatusStrip
        {
            Dock = DockStyle.Fill
            // BackColor removed - let SkinManager handle theming
        };

        _statusLabel = new ToolStripStatusLabel
        {
            Text = "Ready",
            Spring = true,
            TextAlign = ContentAlignment.MiddleLeft
        };

        _progressBar = new ToolStripProgressBar
        {
            Size = new Size(150, 18),
            Visible = false,
            Style = ProgressBarStyle.Marquee
        };

        _statusStrip.Items.Add(_statusLabel);
        _statusStrip.Items.Add(_progressBar);
    }

    #endregion

    #region ViewModel Binding

    /// <summary>
    /// Binds the ViewModel to UI controls.
    /// </summary>
    private void BindViewModel()
    {
        if (ViewModel == null) return;

        // Bind sync history grid
        _syncHistoryGrid!.DataSource = ViewModel.FilteredSyncHistory;

        // Subscribe to property changes
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;

        Logger.LogDebug("QuickBooksPanel: ViewModel bound to UI");
    }

    /// <summary>
    /// Handles ViewModel property changes.
    /// </summary>
    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (ViewModel == null || IsDisposed) return;

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(() => ViewModel_PropertyChanged(sender, e));
            }
            catch { /* Control may be disposed */ }
            return;
        }

        if (IsDisposed) return;

        switch (e.PropertyName)
        {
            case nameof(ViewModel.IsLoading):
                UpdateLoadingState();
                break;

            case nameof(ViewModel.StatusText):
                if (_statusLabel != null)
                    _statusLabel.Text = ViewModel.StatusText ?? "Ready";
                break;

            case nameof(ViewModel.ErrorMessage):
                if (!string.IsNullOrEmpty(ViewModel.ErrorMessage))
                    MessageBox.Show(ViewModel.ErrorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                break;

            case nameof(ViewModel.IsConnected):
                UpdateConnectionStatus();
                break;

            case nameof(ViewModel.CompanyName):
                if (_companyNameLabel != null)
                    _companyNameLabel.Text = $"Company: {ViewModel.CompanyName ?? "-"}";
                break;

            case nameof(ViewModel.LastSyncTime):
                if (_lastSyncLabel != null)
                    _lastSyncLabel.Text = $"Last Sync: {ViewModel.LastSyncTime ?? "-"}";
                break;

            case nameof(ViewModel.ConnectionStatusMessage):
                if (_connectionStatusLabel != null)
                    _connectionStatusLabel.Text = $"Status: {ViewModel.ConnectionStatusMessage}";
                break;

            case nameof(ViewModel.IsSyncing):
                UpdateSyncingState();
                break;

            case nameof(ViewModel.SyncProgress):
                if (_syncProgressBar != null)
                    _syncProgressBar.Value = ViewModel.SyncProgress;
                break;

            case nameof(ViewModel.TotalSyncs):
            case nameof(ViewModel.SuccessfulSyncs):
            case nameof(ViewModel.FailedSyncs):
            case nameof(ViewModel.TotalRecordsSynced):
            case nameof(ViewModel.AccountsImported):
            case nameof(ViewModel.AverageSyncDuration):
                UpdateSummaryPanel();
                break;

            case nameof(ViewModel.FilteredSyncHistory):
                RefreshSyncHistoryDisplay();
                UpdateNoDataOverlay();
                break;
        }
    }

    /// <summary>
    /// Updates the loading state and overlay visibility.
    /// </summary>
    private void UpdateLoadingState()
    {
        if (_loadingOverlay == null || ViewModel == null) return;

        _loadingOverlay.Visible = ViewModel.IsLoading;
        if (_progressBar != null)
            _progressBar.Visible = ViewModel.IsLoading;
    }

    /// <summary>
    /// Updates connection status UI.
    /// </summary>
    private void UpdateConnectionStatus()
    {
        if (ViewModel == null) return;

        var isConnected = ViewModel.IsConnected;

        if (_connectionStatusLabel != null)
        {
            _connectionStatusLabel.ForeColor = isConnected ? Color.Green : Color.Red; // Semantic status colors (allowed exception)
        }

        // Update button states
        if (_connectButton != null)
            _connectButton.Enabled = !isConnected && !ViewModel.IsLoading;

        if (_disconnectButton != null)
            _disconnectButton.Enabled = isConnected && !ViewModel.IsLoading;

        if (_testConnectionButton != null)
            _testConnectionButton.Enabled = !ViewModel.IsLoading;

        if (_syncDataButton != null)
            _syncDataButton.Enabled = isConnected && !ViewModel.IsLoading && !ViewModel.IsSyncing;

        if (_importAccountsButton != null)
            _importAccountsButton.Enabled = isConnected && !ViewModel.IsLoading;
    }

    /// <summary>
    /// Updates syncing state UI.
    /// </summary>
    private void UpdateSyncingState()
    {
        if (ViewModel == null || _syncProgressBar == null) return;

        _syncProgressBar.Visible = ViewModel.IsSyncing;
        if (!ViewModel.IsSyncing)
            _syncProgressBar.Value = 0;
    }

    /// <summary>
    /// Updates the summary panel with current metrics.
    /// </summary>
    private void UpdateSummaryPanel()
    {
        if (ViewModel == null) return;

        if (_totalSyncsLabel != null)
            _totalSyncsLabel.Text = ViewModel.TotalSyncs.ToString("N0", CultureInfo.CurrentCulture);

        if (_successfulSyncsLabel != null)
            _successfulSyncsLabel.Text = ViewModel.SuccessfulSyncs.ToString("N0", CultureInfo.CurrentCulture);

        if (_failedSyncsLabel != null)
            _failedSyncsLabel.Text = ViewModel.FailedSyncs.ToString("N0", CultureInfo.CurrentCulture);

        if (_totalRecordsLabel != null)
            _totalRecordsLabel.Text = ViewModel.TotalRecordsSynced.ToString("N0", CultureInfo.CurrentCulture);

        if (_accountsImportedLabel != null)
            _accountsImportedLabel.Text = ViewModel.AccountsImported.ToString("N0", CultureInfo.CurrentCulture);

        if (_avgDurationLabel != null)
            _avgDurationLabel.Text = $"{ViewModel.AverageSyncDuration:F1}s";
    }

    /// <summary>
    /// Refreshes the sync history grid display after collection changes.
    /// </summary>
    private void RefreshSyncHistoryDisplay()
    {
        if (_syncHistoryGrid?.DataSource != null)
        {
            _syncHistoryGrid.View?.Refresh();
        }
    }

    /// <summary>
    /// Updates the no data overlay visibility.
    /// </summary>
    private void UpdateNoDataOverlay()
    {
        if (_noDataOverlay == null || ViewModel == null) return;

        var hasData = ViewModel.FilteredSyncHistory.Count > 0;
        _noDataOverlay.Visible = !hasData && !ViewModel.IsLoading;
    }

    #endregion

    #region Syncfusion Theming

    /// <summary>
    /// Applies Syncfusion Office2019Colorful theme to all controls.
    /// Note: Currently uses manual Style property configuration for fine-grained control over colors.
    /// Alternative approach: Use ThemeName property (e.g., _syncHistoryGrid.ThemeName = "Office2019Colorful")
    /// for easier theme management, but this limits custom color overrides (success green, danger red).
    /// </summary>
    private void ApplySyncfusionTheme()
    {
        try
        {
            if (_syncHistoryGrid != null)
            {
                // Header styling
                _syncHistoryGrid.Style.HeaderStyle.Font.Bold = true;
                _syncHistoryGrid.Style.HeaderStyle.Font.Size = 9.5f;

                // Selection styling

                // Cell styling
                _syncHistoryGrid.Style.CellStyle.Font.Size = 9f;

                // Add alternate row coloring and status color coding via QueryCellStyle
                _syncHistoryGrid.QueryCellStyle += SyncHistoryGrid_QueryCellStyle;

                Logger.LogDebug("Syncfusion theme applied successfully to QuickBooksPanel");
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to apply Syncfusion theme to QuickBooksPanel");
        }
    }

    /// <summary>
    /// Handles cell styling for the sync history grid.
    /// </summary>
    private void SyncHistoryGrid_QueryCellStyle(object? sender, QueryCellStyleEventArgs e)
    {
        if (e.Column == null || e.DataRow == null) return;

        // Alternate row coloring
        if (e.DataRow.RowIndex % 2 == 0)
            // Alternating row styling removed - let SkinManager handle theming

            // Status column color coding
            if (e.Column.MappingName == nameof(QuickBooksSyncHistoryRecord.Status) && e.DataRow.RowData is QuickBooksSyncHistoryRecord record)
            {
                switch (record.Status)
                {
                    case "Success":
                        e.Style.TextColor = Color.Green; // Semantic success color
                        e.Style.Font.Bold = true;
                        break;
                    case "Failed":
                    case "Error":
                        e.Style.TextColor = Color.Red; // Semantic error color
                        e.Style.Font.Bold = true;
                        break;
                }
            }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Executes an async command safely.
    /// </summary>
    private async Task ExecuteCommandAsync(CommunityToolkit.Mvvm.Input.IAsyncRelayCommand? command)
    {
        if (command == null || !command.CanExecute(null)) return;

        try
        {
            await command.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Command execution failed");
            MessageBox.Show($"Operation failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Refreshes the panel data.
    /// </summary>
    private async Task RefreshAsync()
    {
        if (ViewModel != null)
        {
            await ViewModel.CheckConnectionCommand.ExecuteAsync(null);
            await ViewModel.RefreshHistoryCommand.ExecuteAsync(null);
        }
    }

    /// <summary>
    /// Closes the panel.
    /// </summary>
    private void ClosePanel()
    {
        var parent = Parent;
        parent?.Controls.Remove(this);
        Dispose();
    }

    #endregion

    #region Disposal

    /// <summary>
    /// Disposes panel resources.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Unsubscribe from events
            if (ViewModel != null)
            {
                ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }

            if (_syncHistoryGrid != null)
            {
                _syncHistoryGrid.QueryCellStyle -= SyncHistoryGrid_QueryCellStyle;
            }

            // Dispose Syncfusion controls using SafeDispose
            try { _syncHistoryGrid?.SafeClearDataSource(); } catch { }
            try { _syncHistoryGrid?.SafeDispose(); } catch { }
            try { _connectButton?.Dispose(); } catch { }
            try { _disconnectButton?.Dispose(); } catch { }
            try { _testConnectionButton?.Dispose(); } catch { }
            try { _syncDataButton?.Dispose(); } catch { }
            try { _importAccountsButton?.Dispose(); } catch { }
            try { _refreshHistoryButton?.Dispose(); } catch { }
            try { _clearHistoryButton?.Dispose(); } catch { }
            try { _exportHistoryButton?.Dispose(); } catch { }
            try { _syncProgressBar?.Dispose(); } catch { }

            // Dispose other controls
            try { _panelHeader?.Dispose(); } catch { }
            try { _loadingOverlay?.Dispose(); } catch { }
            try { _noDataOverlay?.Dispose(); } catch { }
            try { _statusStrip?.Dispose(); } catch { }
            try { _mainLayout?.Dispose(); } catch { }
            try { _mainSplitContainer?.Dispose(); } catch { }
            try { _connectionPanel?.Dispose(); } catch { }
            try { _operationsPanel?.Dispose(); } catch { }
            try { _summaryPanel?.Dispose(); } catch { }

            Logger.LogDebug("QuickBooksPanel disposed");
        }

        base.Dispose(disposing);
    }

    #endregion
}
