using System;
using System.Collections.Generic;
using System.Threading;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using GradientPanelExt = Syncfusion.Windows.Forms.Tools.GradientPanelExt;
using ProgressBarAdv = Syncfusion.Windows.Forms.Tools.ProgressBarAdv;
using ProgressBarStyles = Syncfusion.Windows.Forms.Tools.ProgressBarStyles;
using SfButton = Syncfusion.WinForms.Controls.SfButton;
using SfDataGrid = Syncfusion.WinForms.DataGrid.SfDataGrid;
using SfSkinManager = Syncfusion.WinForms.Controls.SfSkinManager;
using TextBoxExt = Syncfusion.Windows.Forms.Tools.TextBoxExt;
using GridTextColumn = Syncfusion.WinForms.DataGrid.GridTextColumn;
using GridNumericColumn = Syncfusion.WinForms.DataGrid.GridNumericColumn;
using Syncfusion.Drawing;
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.WinForms.DataGrid.Events;
using Syncfusion.WinForms.DataGrid.Styles;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Utils;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Controls;

/// <summary>
/// QuickBooks integration panel with full MVVM pattern, connection management, and sync history tracking.
/// Inherits from ScopedPanelBase for proper DI lifecycle management.
/// Uses Syncfusion API properly: Dock layout, GradientPanelExt per Syncfusion documentation.
/// </summary>
[SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
public partial class QuickBooksPanel : ScopedPanelBase<QuickBooksViewModel>
{
    #region UI Controls

    private PanelHeader? _panelHeader;
    private LoadingOverlay? _loadingOverlay;
    private NoDataOverlay? _noDataOverlay;

    // Main layout containers (use Dock, not absolute positioning)
    private SplitContainer? _mainSplitContainer;
    private GradientPanelExt? _connectionPanel;
    private GradientPanelExt? _operationsPanel;
    private GradientPanelExt? _summaryPanel;
    private GradientPanelExt? _historyPanel;

    // Connection Panel Controls (Dock-based layout)
    private Label? _connectionStatusLabel;
    private Label? _companyNameLabel;
    private Label? _lastSyncLabel;
    private SfButton? _connectButton;
    private SfButton? _disconnectButton;
    private SfButton? _testConnectionButton;

    // Operations Panel Controls (Dock-based layout)
    private SfButton? _syncDataButton;
    private SfButton? _importAccountsButton;
    private SfButton? _refreshHistoryButton;
    private SfButton? _clearHistoryButton;
    private SfButton? _exportHistoryButton;
    private ProgressBarAdv? _syncProgressBar;

    // Sync History Grid
    private SfDataGrid? _syncHistoryGrid;
    private TextBoxExt? _filterTextBox;

    // Summary Panel KPI Labels
    private Label? _totalSyncsLabel;
    private Label? _successfulSyncsLabel;
    private Label? _failedSyncsLabel;
    private Label? _totalRecordsLabel;
    private Label? _accountsImportedLabel;
    private Label? _avgDurationLabel;

    // Event handler storage (for proper cleanup in Dispose)
    private EventHandler? _panelHeaderRefreshClickedHandler;
    private EventHandler? _panelHeaderCloseClickedHandler;
    private EventHandler? _connectButtonClickHandler;
    private EventHandler? _disconnectButtonClickHandler;
    private EventHandler? _testConnectionButtonClickHandler;
    private EventHandler? _syncDataButtonClickHandler;
    private EventHandler? _importAccountsButtonClickHandler;
    private EventHandler? _refreshHistoryButtonClickHandler;
    private EventHandler? _clearHistoryButtonClickHandler;
    private EventHandler? _exportHistoryButtonClickHandler;
    private EventHandler? _filterTextBoxTextChangedHandler;
    private SelectionChangedEventHandler? _gridSelectionChangedHandler;
    private MouseEventHandler? _gridMouseDoubleClickHandler;
    private QueryCellStyleEventHandler? _gridQueryCellStyleHandler;

    #endregion

    #region ICompletablePanel Overrides

    /// <summary>
    /// Loads the panel asynchronously (ICompletablePanel implementation).
    /// Initializes ViewModel and loads sync history.
    /// </summary>
    public override async Task LoadAsync(CancellationToken ct)
    {
        if (IsLoaded) return;

        try
        {
            IsBusy = true;

            if (ViewModel != null && !DesignMode)
            {
                await ViewModel.InitializeAsync(ct);
                UpdateLoadingState();
                UpdateNoDataOverlay();
            }

            Logger.LogDebug("QuickBooksPanel loaded successfully");
        }
        catch (OperationCanceledException)
        {
            Logger.LogInformation("QuickBooksPanel load cancelled");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load QuickBooksPanel");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Saves the panel asynchronously (ICompletablePanel implementation).
    /// QuickBooks panel is read-only, so this is a no-op.
    /// </summary>
    public override async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            IsBusy = true;

            // QuickBooks panel is view-only; no persistence required.
            // If future changes allow edits, implement save logic here.

            await Task.CompletedTask;
            Logger.LogDebug("QuickBooksPanel save completed");
        }
        catch (OperationCanceledException)
        {
            Logger.LogInformation("QuickBooksPanel save cancelled");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to save QuickBooksPanel");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Validates the panel asynchronously (ICompletablePanel implementation).
    /// Ensures connection is active and syncs are not failing.
    /// </summary>
    public override async Task<ValidationResult> ValidateAsync(CancellationToken ct)
    {
        try
        {
            IsBusy = true;

            var errors = new List<ValidationItem>();

            if (ViewModel == null)
            {
                errors.Add(new ValidationItem("ViewModel", "ViewModel not initialized", ValidationSeverity.Error));
            }
            else if (!ViewModel.IsConnected)
            {
                errors.Add(new ValidationItem("Connection", "QuickBooks connection is not active", ValidationSeverity.Warning));
            }
            else if (ViewModel.IsSyncing)
            {
                errors.Add(new ValidationItem("Sync", "Sync operation in progress; please wait", ValidationSeverity.Info));
            }

            await Task.CompletedTask;

            return errors.Count == 0
                ? ValidationResult.Success
                : new ValidationResult(false, errors);
        }
        catch (OperationCanceledException)
        {
            Logger.LogInformation("QuickBooksPanel validation cancelled");
            return ValidationResult.Failed(new ValidationItem("Cancelled", "Validation was cancelled", ValidationSeverity.Info));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Validation error in QuickBooksPanel");
            return ValidationResult.Failed(new ValidationItem("Validation", ex.Message, ValidationSeverity.Error));
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Focuses the first validation error control (ICompletablePanel implementation).
    /// </summary>
    public override void FocusFirstError()
    {
        if (_connectionPanel != null)
        {
            _connectionPanel.Focus();
        }
    }

    #endregion

    /// <summary>
    /// Initializes a new instance of the <see cref="QuickBooksPanel"/> class.
    /// </summary>
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

                // Defer sizing validation - QuickBooks panel has nested SplitContainers and grids
                DeferSizeValidation();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to initialize QuickBooksPanel");
            }
        }
    }

    private void DeferSizeValidation()
    {
        if (IsDisposed) return;

        if (IsHandleCreated)
        {
            try { BeginInvoke(new System.Action(() => SafeControlSizeValidator.TryAdjustConstrainedSize(this, out _, out _))); }
            catch { }
            return;
        }

        EventHandler? handleCreatedHandler = null;
        handleCreatedHandler = (s, e) =>
        {
            HandleCreated -= handleCreatedHandler;
            if (IsDisposed) return;

            try { BeginInvoke(new System.Action(() => SafeControlSizeValidator.TryAdjustConstrainedSize(this, out _, out _))); }
            catch { }
        };

        HandleCreated += handleCreatedHandler;
    }

    #region Control Initialization

    /// <summary>
    /// Initializes all UI controls and layout using proper Syncfusion API (Dock-based, not absolute positioning).
    /// </summary>
    private void InitializeControls()
    {
        SuspendLayout();

        Name = "QuickBooksPanel";
        Size = new Size(1400, 900);
        MinimumSize = new Size((int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(1000f), (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(750f));
        Padding = new Padding(8);
        Dock = DockStyle.Fill;

        // Panel header
        _panelHeader = new PanelHeader
        {
            Dock = DockStyle.Top,
            Height = 50
        };
        _panelHeader.Title = "QuickBooks Integration";

        // Store handlers for cleanup
        _panelHeaderRefreshClickedHandler = async (s, e) => await RefreshAsync();
        _panelHeaderCloseClickedHandler = (s, e) => ClosePanel();

        _panelHeader.RefreshClicked += _panelHeaderRefreshClickedHandler;
        _panelHeader.CloseClicked += _panelHeaderCloseClickedHandler;
        Controls.Add(_panelHeader);

        // Summary panel (KPI metrics)
        CreateSummaryPanel();
        Controls.Add(_summaryPanel!);

        // Main split container: top = connection/operations, bottom = history
        _mainSplitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            FixedPanel = FixedPanel.None
        };

        // Defer min size and splitter distance assignment until control is properly sized
        // This prevents InvalidOperationException during initialization
        SafeSplitterDistanceHelper.ConfigureSafeSplitContainer(
            _mainSplitContainer,
            panel1MinSize: 250,
            panel2MinSize: 200,
            desiredDistance: 320);

        // Setup proportional resizing for responsive layout
        SafeSplitterDistanceHelper.SetupProportionalResizing(_mainSplitContainer, 0.42);  // favor history grid space

        // Top: Connection and Operations panels (side by side)
        CreateConnectionAndOperationsPanels();
        _mainSplitContainer.Panel1.Controls.Add(CreateTopPanel());

        // Bottom: Sync History
        CreateSyncHistoryGrid();
        CreateHistoryPanel();
        _mainSplitContainer.Panel2.Controls.Add(_historyPanel!);

        Controls.Add(_mainSplitContainer);

        // Create overlays
        _loadingOverlay = new LoadingOverlay
        {
            Message = "Loading QuickBooks data...",
            Dock = DockStyle.Fill,
            Visible = false
        };
        Controls.Add(_loadingOverlay);
        _loadingOverlay.BringToFront();

        _noDataOverlay = new NoDataOverlay
        {
            Message = "No sync history yet\r\nConnect and sync data with QuickBooks to get started",
            Dock = DockStyle.Fill,
            Visible = false
        };
        Controls.Add(_noDataOverlay);
        _noDataOverlay.BringToFront();

        ResumeLayout(false);
        // NOTE: Do NOT call PerformLayout() or Refresh() here - causes infinite loop with TableLayoutPanel
        // ResumeLayout(false) already schedules layout correctly without triggering GetPreferredSize recursion

        Logger.LogDebug("[PANEL] {PanelName} content anchored", this.Name);
    }

    /// <summary>
    /// Creates the summary panel with KPI metrics using GradientPanelExt per Syncfusion API.
    /// </summary>
    private void CreateSummaryPanel()
    {
        var currentTheme = SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;
        _summaryPanel = new GradientPanelExt
        {
            Dock = DockStyle.Top,
            Height = 125,
            Padding = new Padding(8, 6, 8, 6),
            BorderStyle = BorderStyle.None,
            CornerRadius = 4,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(_summaryPanel, currentTheme);

        var summaryTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 2,
            Padding = new Padding(2),
            Margin = Padding.Empty,
            AutoSize = false  // Prevent GetPreferredSize recursion
        };

        for (var i = 0; i < 3; i++)
        {
            summaryTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        }

        summaryTable.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
        summaryTable.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));

        _totalSyncsLabel = CreateMetricCard(summaryTable, 0, 0, "Total Syncs", "0");
        _successfulSyncsLabel = CreateMetricCard(summaryTable, 1, 0, "Successful", "0");
        _failedSyncsLabel = CreateMetricCard(summaryTable, 2, 0, "Failed", "0");
        _totalRecordsLabel = CreateMetricCard(summaryTable, 0, 1, "Records Synced", "0");
        _accountsImportedLabel = CreateMetricCard(summaryTable, 1, 1, "Accounts Imported", "0");
        _avgDurationLabel = CreateMetricCard(summaryTable, 2, 1, "Avg Duration", "0s");

        _summaryPanel.Controls.Add(summaryTable);
    }

    /// <summary>
    /// Creates a metric card for the summary panel using GradientPanelExt.
    /// Cards use theme-compliant styling via SfSkinManager.
    /// </summary>
    private Label CreateMetricCard(TableLayoutPanel parent, int column, int row, string title, string value)
    {
        var currentTheme = SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme;
        var cardPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(4),
            BorderStyle = BorderStyle.FixedSingle,
            CornerRadius = 4,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty),
            Padding = new Padding(10, 8, 10, 8),
            AutoSize = false  // Prevent GetPreferredSize recursion with TableLayoutPanel
        };
        SfSkinManager.SetVisualStyle(cardPanel, currentTheme);

        var titleLabel = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 18,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
            AutoSize = false
        };

        var valueLabel = new Label
        {
            Text = value,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            AutoSize = false
        };

        cardPanel.Controls.Add(valueLabel);
        cardPanel.Controls.Add(titleLabel);
        parent.Controls.Add(cardPanel, column, row);

        return valueLabel;
    }

    /// <summary>
    /// Creates the top panel with connection and operations panels side-by-side.
    /// Uses SafeSplitterDistanceHelper to avoid SplitterDistance out-of-bounds exceptions.
    /// </summary>
    private GradientPanelExt CreateTopPanel()
    {
        var topPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            CornerRadius = 0,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty),
            Padding = new Padding(10)
        };
        SfSkinManager.SetVisualStyle(topPanel, SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);

        // Vertical split: left = connection, right = operations (safe deferred sizing)
        var splitTop = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            FixedPanel = FixedPanel.None
        };

        // Defer min sizes and splitter distance until the control has real dimensions
        SafeSplitterDistanceHelper.ConfigureSafeSplitContainer(
            splitTop,
            panel1MinSize: 280,
            panel2MinSize: 280,
            desiredDistance: 400);

        // Maintain ~50% split during resize with helper-managed bounds
        SafeSplitterDistanceHelper.SetupProportionalResizing(splitTop, 0.5);

        splitTop.Panel1.Controls.Add(_connectionPanel!);
        splitTop.Panel2.Controls.Add(_operationsPanel!);

        topPanel.Controls.Add(splitTop);
        return topPanel;
    }

    /// <summary>
    /// Creates connection and operations panels with proper Dock layout (no absolute positioning).
    /// </summary>
    private void CreateConnectionAndOperationsPanels()
    {
        // Connection Panel
        _connectionPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            BorderStyle = BorderStyle.None,
            CornerRadius = 2,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(_connectionPanel, SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);

        // Connection header
        var connectionHeader = new Label
        {
            Text = "Connection Status",
            Dock = DockStyle.Top,
            Height = 28,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        _connectionPanel.Controls.Add(connectionHeader);

        // Connection info panel
        var infoPanel = new GradientPanelExt
        {
            Dock = DockStyle.Top,
            Height = 100,
            Padding = new Padding(5),
            BorderStyle = BorderStyle.None,
            CornerRadius = 0,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(infoPanel, SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);

        _connectionStatusLabel = new Label
        {
            Text = "Status: Checking...",
            Dock = DockStyle.Top,
            Height = 28,
            Font = new Font("Segoe UI", 10f, FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleLeft
        };
        infoPanel.Controls.Add(_connectionStatusLabel);

        _companyNameLabel = new Label
        {
            Text = "Company: -",
            Dock = DockStyle.Top,
            Height = 24,
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleLeft
        };
        infoPanel.Controls.Add(_companyNameLabel);

        _lastSyncLabel = new Label
        {
            Text = "Last Sync: -",
            Dock = DockStyle.Top,
            Height = 24,
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleLeft
        };
        infoPanel.Controls.Add(_lastSyncLabel);

        _connectionPanel.Controls.Add(infoPanel);

        // Buttons panel - use FlowLayoutPanel for proper wrapping
        var buttonsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 60,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(5),
            AutoSize = true
        };

        _connectButton = new SfButton
        {
            Text = "Connect",
            AutoSize = false,
            Size = new Size(85, 36),
            Margin = new Padding(3),
            AccessibleName = "Connect to QuickBooks",
            AccessibleDescription = "Establishes connection to QuickBooks Online",
            TabIndex = 1,
            TabStop = true
        };
        var connectTooltip = new ToolTip();
        connectTooltip.SetToolTip(_connectButton, "Click to authorize QuickBooks connection via OAuth");

        _connectButtonClickHandler = async (s, e) => await InitiateQuickBooksOAuthFlowAsync();
        _connectButton.Click += _connectButtonClickHandler;
        buttonsPanel.Controls.Add(_connectButton);

        _disconnectButton = new SfButton
        {
            Text = "Disconnect",
            AutoSize = false,
            Size = new Size(100, 36),
            Margin = new Padding(3),
            AccessibleName = "Disconnect from QuickBooks",
            AccessibleDescription = "Terminates current QuickBooks Online connection",
            TabIndex = 2,
            TabStop = true
        };
        var disconnectTooltip = new ToolTip();
        disconnectTooltip.SetToolTip(_disconnectButton, "Click to disconnect from QuickBooks");

        _disconnectButtonClickHandler = async (s, e) =>
        {
            if (await ShowDisconnectConfirmationAsync())
            {
                await ExecuteCommandAsync(ViewModel?.DisconnectCommand);
            }
        };
        _disconnectButton.Click += _disconnectButtonClickHandler;
        buttonsPanel.Controls.Add(_disconnectButton);

        _testConnectionButton = new SfButton
        {
            Text = "Test Connection",
            AutoSize = false,
            Size = new Size(120, 36),
            Margin = new Padding(3),
            AccessibleName = "Test QuickBooks Connection",
            AccessibleDescription = "Verifies QuickBooks Online connection status",
            TabIndex = 3,
            TabStop = true
        };
        var testTooltip = new ToolTip();
        testTooltip.SetToolTip(_testConnectionButton, "Click to test the current QuickBooks connection");

        _testConnectionButtonClickHandler = async (s, e) => await ExecuteCommandAsync(ViewModel?.TestConnectionCommand);
        _testConnectionButton.Click += _testConnectionButtonClickHandler;
        buttonsPanel.Controls.Add(_testConnectionButton);

        _connectionPanel.Controls.Add(buttonsPanel);

        // Operations Panel
        _operationsPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            BorderStyle = BorderStyle.None,
            CornerRadius = 2,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(_operationsPanel, SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);

        // Operations header
        var operationsHeader = new Label
        {
            Text = "QuickBooks Operations",
            Dock = DockStyle.Top,
            Height = 28,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        _operationsPanel.Controls.Add(operationsHeader);

        // Operations buttons - use FlowLayoutPanel
        var opsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 130,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(5),
            AutoSize = true
        };

        _syncDataButton = new SfButton
        {
            Text = "🔄 Sync Data",
            AutoSize = false,
            Size = new Size(130, 36),
            Margin = new Padding(3),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            AccessibleName = "Sync Data with QuickBooks",
            AccessibleDescription = "Synchronizes financial data between Wiley Widget and QuickBooks Online",
            TabIndex = 4,
            TabStop = true
        };
        var syncTooltip = new ToolTip();
        syncTooltip.SetToolTip(_syncDataButton, "Click to synchronize data with QuickBooks");

        _syncDataButtonClickHandler = async (s, e) =>
        {
            if (await ShowSyncConfirmationAsync("Sync Data"))
            {
                await ExecuteCommandAsync(ViewModel?.SyncDataCommand);
            }
        };
        _syncDataButton.Click += _syncDataButtonClickHandler;
        opsPanel.Controls.Add(_syncDataButton);

        _importAccountsButton = new SfButton
        {
            Text = "📥 Import Chart of Accounts",
            AutoSize = false,
            Size = new Size(180, 36),
            Margin = new Padding(3),
            AccessibleName = "Import Chart of Accounts",
            AccessibleDescription = "Imports complete chart of accounts from QuickBooks Online",
            TabIndex = 5,
            TabStop = true
        };
        var importTooltip = new ToolTip();
        importTooltip.SetToolTip(_importAccountsButton, "Click to import the chart of accounts from QuickBooks");

        _importAccountsButtonClickHandler = async (s, e) => await ExecuteCommandAsync(ViewModel?.ImportAccountsCommand);
        _importAccountsButton.Click += _importAccountsButtonClickHandler;
        opsPanel.Controls.Add(_importAccountsButton);

        _operationsPanel.Controls.Add(opsPanel);

        // Sync progress bar
        _syncProgressBar = new ProgressBarAdv
        {
            Dock = DockStyle.Bottom,
            Height = 25,
            Visible = false,
            ProgressStyle = ProgressBarStyles.WaitingGradient
        };
        _operationsPanel.Controls.Add(_syncProgressBar);
    }

    /// <summary>
    /// Creates the sync history panel with grid and filter bar.
    /// </summary>
    private void CreateHistoryPanel()
    {
        _historyPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            CornerRadius = 2,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty),
            Padding = new Padding(8)
        };
        SfSkinManager.SetVisualStyle(_historyPanel, SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);

        // History header with filter
        var headerPanel = new GradientPanelExt
        {
            Dock = DockStyle.Top,
            Height = 56,
            Padding = new Padding(6, 6, 6, 2),
            BorderStyle = BorderStyle.None,
            CornerRadius = 0,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(headerPanel, SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);

        var headerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };

        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var titleLabel = new Label
        {
            Text = "Sync History",
            Anchor = AnchorStyles.Left,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize = true,
            Margin = new Padding(2, 0, 12, 0)
        };
        headerLayout.Controls.Add(titleLabel, 0, 0);

        var filterLabel = new Label
        {
            Text = "Filter:",
            Anchor = AnchorStyles.Left,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize = true,
            Margin = new Padding(0, 0, 6, 0)
        };
        headerLayout.Controls.Add(filterLabel, 1, 0);

        _filterTextBox = new TextBoxExt
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 12, 0),
            MinimumSize = new Size(220, 28),
            AccessibleName = "Filter Sync History",
            AccessibleDescription = "Enter text to filter sync history records",
            TabIndex = 6,
            TabStop = true
        };
        var filterTooltip = new ToolTip();
        filterTooltip.SetToolTip(_filterTextBox, "Type to filter the sync history by any field");

        _filterTextBoxTextChangedHandler = (s, e) =>
        {
            if (ViewModel != null)
                ViewModel.FilterText = _filterTextBox.Text;
        };
        _filterTextBox.TextChanged += _filterTextBoxTextChangedHandler;
        headerLayout.Controls.Add(_filterTextBox, 2, 0);

        // Buttons - use FlowLayoutPanel for consistent spacing
        var buttonsFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        _exportHistoryButton = new SfButton
        {
            Text = "📤 Export CSV",
            AutoSize = false,
            Size = new Size(105, 32),
            Margin = new Padding(3),
            AccessibleName = "Export History to CSV",
            AccessibleDescription = "Exports sync history data to CSV file",
            TabIndex = 9,
            TabStop = true
        };
        var exportTooltip = new ToolTip();
        exportTooltip.SetToolTip(_exportHistoryButton, "Click to export sync history as CSV");

        _exportHistoryButtonClickHandler = async (s, e) =>
        {
            var filePath = ShowExportFilePickerDialog();
            if (filePath != null)
            {
                Logger.LogInformation("Export to: {FilePath}", filePath);
                MessageBox.Show($"Export functionality will be implemented.\nSelected: {filePath}", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };
        _exportHistoryButton.Click += _exportHistoryButtonClickHandler;
        buttonsFlow.Controls.Add(_exportHistoryButton);

        _clearHistoryButton = new SfButton
        {
            Text = "🗑 Clear",
            AutoSize = false,
            Size = new Size(75, 32),
            Margin = new Padding(3),
            AccessibleName = "Clear Sync History",
            AccessibleDescription = "Removes all sync history records from the display",
            TabIndex = 8,
            TabStop = true
        };
        var clearTooltip = new ToolTip();
        clearTooltip.SetToolTip(_clearHistoryButton, "Click to clear all sync history (cannot be undone)");

        _clearHistoryButtonClickHandler = async (s, e) =>
        {
            if (await ShowClearHistoryConfirmationAsync())
            {
                if (ViewModel?.ClearHistoryCommand.CanExecute(null) == true)
                {
                    ViewModel.ClearHistoryCommand.Execute(null);
                }
            }
        };
        _clearHistoryButton.Click += _clearHistoryButtonClickHandler;
        buttonsFlow.Controls.Add(_clearHistoryButton);

        _refreshHistoryButton = new SfButton
        {
            Text = "🔄 Refresh",
            AutoSize = false,
            Size = new Size(95, 32),
            Margin = new Padding(3),
            AccessibleName = "Refresh Sync History",
            AccessibleDescription = "Reloads sync history from database",
            TabIndex = 7,
            TabStop = true
        };
        var refreshTooltip = new ToolTip();
        refreshTooltip.SetToolTip(_refreshHistoryButton, "Click to reload sync history");

        _refreshHistoryButtonClickHandler = async (s, e) => await ExecuteCommandAsync(ViewModel?.RefreshHistoryCommand);
        _refreshHistoryButton.Click += _refreshHistoryButtonClickHandler;
        buttonsFlow.Controls.Add(_refreshHistoryButton);

        headerLayout.Controls.Add(buttonsFlow, 3, 0);
        headerPanel.Controls.Add(headerLayout);
        _historyPanel.Controls.Add(headerPanel);

        // Grid
        _historyPanel.Controls.Add(_syncHistoryGrid!);
    }

    /// <summary>
    /// Creates the sync history data grid with proper column configuration.
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
            RowHeight = 30,
            HeaderRowHeight = 36
        };

        // Define columns with widths optimized for responsive layout
        _syncHistoryGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(QuickBooksSyncHistoryRecord.FormattedTimestamp),
            HeaderText = "Timestamp",
            Width = 150,
            AllowSorting = true
        });

        _syncHistoryGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(QuickBooksSyncHistoryRecord.Operation),
            HeaderText = "Operation",
            Width = 130,
            AllowSorting = true
        });

        _syncHistoryGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(QuickBooksSyncHistoryRecord.Status),
            HeaderText = "Status",
            Width = 80,
            AllowSorting = true
        });

        _syncHistoryGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = nameof(QuickBooksSyncHistoryRecord.RecordsProcessed),
            HeaderText = "Records",
            Width = 75,
            Format = "N0",
            AllowSorting = true
        });

        _syncHistoryGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(QuickBooksSyncHistoryRecord.FormattedDuration),
            HeaderText = "Duration",
            Width = 75,
            AllowSorting = true
        });

        _syncHistoryGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(QuickBooksSyncHistoryRecord.Message),
            HeaderText = "Message",
            Width = 300,
            AllowSorting = false
        });

        _gridSelectionChangedHandler = (s, e) =>
        {
            if (ViewModel != null && _syncHistoryGrid.SelectedItem is QuickBooksSyncHistoryRecord record)
            {
                ViewModel.SelectedSyncRecord = record;
            }
        };
        _syncHistoryGrid.SelectionChanged += _gridSelectionChangedHandler;

        // Double-click to show record details
        _gridMouseDoubleClickHandler = (s, e) =>
        {
            if (_syncHistoryGrid.SelectedItem is QuickBooksSyncHistoryRecord record)
            {
                HandleSyncRecordDoubleClick(record);
            }
        };
        _syncHistoryGrid.MouseDoubleClick += _gridMouseDoubleClickHandler;

        // Right-click context menu for record actions
        var contextMenu = new ContextMenuStrip
        {
            AutoClose = true,
            ShowImageMargin = true
        };

        var viewDetailsItem = new ToolStripMenuItem("View Details", null, (s, e) =>
        {
            if (_syncHistoryGrid.SelectedItem is QuickBooksSyncHistoryRecord record)
            {
                ShowSyncRecordDetailsDialog(record);
            }
        });

        var retryItem = new ToolStripMenuItem("Retry", null, async (s, e) =>
        {
            if (_syncHistoryGrid.SelectedItem is QuickBooksSyncHistoryRecord record && record.Status == "Failed")
            {
                await RetryFailedSyncAsync(record);
            }
        });

        var deleteItem = new ToolStripMenuItem("Delete Record", null, (s, e) =>
        {
            if (_syncHistoryGrid.SelectedItem is QuickBooksSyncHistoryRecord record)
            {
                DeleteSyncRecord(record);
            }
        });

        contextMenu.Items.AddRange(new ToolStripItem[] { viewDetailsItem, retryItem, deleteItem });
        _syncHistoryGrid.ContextMenuStrip = contextMenu;

        _gridQueryCellStyleHandler = (object? sender, QueryCellStyleEventArgs e) => SyncHistoryGrid_QueryCellStyle(sender, e);
        _syncHistoryGrid.QueryCellStyle += _gridQueryCellStyleHandler;
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
            if (IsHandleCreated && !IsDisposed)
            {
                try
                {
                    BeginInvoke(() => ViewModel_PropertyChanged(sender, e));
                }
                catch { /* Control may be disposed */ }
            }
            return;
        }

        if (IsDisposed) return;

        switch (e.PropertyName)
        {
            case nameof(ViewModel.IsLoading):
                UpdateLoadingState();
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

            case nameof(ViewModel.FilterText):
                if (_filterTextBox != null && _filterTextBox.Text != (ViewModel.FilterText ?? ""))
                    _filterTextBox.Text = ViewModel.FilterText ?? "";
                break;
        }
    }

    private void UpdateLoadingState()
    {
        if (_loadingOverlay == null || ViewModel == null) return;
        _loadingOverlay.Visible = ViewModel.IsLoading;
    }

    private void UpdateConnectionStatus()
    {
        if (ViewModel == null) return;

        var isConnected = ViewModel.IsConnected;

        // Use semantic status colors only for connection indicator (exception to theme rule)
        if (_connectionStatusLabel != null)
        {
            _connectionStatusLabel.ForeColor = isConnected ? Color.Green : Color.Red;
        }

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

    private void UpdateSyncingState()
    {
        if (ViewModel == null || _syncProgressBar == null) return;

        _syncProgressBar.Visible = ViewModel.IsSyncing;
        if (!ViewModel.IsSyncing)
            _syncProgressBar.Value = 0;
    }

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

    private void RefreshSyncHistoryDisplay()
    {
        if (_syncHistoryGrid?.View != null)
        {
            _syncHistoryGrid.View.Refresh();
        }
    }

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
    /// Uses SfSkinManager cascade - no manual color assignments.
    /// </summary>
    private void ApplySyncfusionTheme()
    {
        try
        {
            if (_syncHistoryGrid != null)
            {
                _syncHistoryGrid.Style.HeaderStyle.Font.Bold = true;
                _syncHistoryGrid.Style.HeaderStyle.Font.Size = 9.5f;
                _syncHistoryGrid.Style.CellStyle.Font.Size = 9f;

                Logger.LogDebug("Syncfusion theme applied successfully to QuickBooksPanel");
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to apply Syncfusion theme to QuickBooksPanel");
        }
    }

    /// <summary>
    /// Handles cell styling for the sync history grid (status color coding).
    /// </summary>
    private void SyncHistoryGrid_QueryCellStyle(object? sender, QueryCellStyleEventArgs e)
    {
        if (e.Column == null || e.DataRow == null) return;

        // Apply semantic status indicator colors (exception to theme rule: status colors)
        if (e.Column.MappingName == nameof(QuickBooksSyncHistoryRecord.Status) && e.DataRow.RowData is QuickBooksSyncHistoryRecord record)
        {
            e.Style.Font.Bold = true;
            switch (record.Status)
            {
                case "Success":
                    e.Style.TextColor = Color.Green;
                    break;
                case "Failed":
                case "Error":
                    e.Style.TextColor = Color.Red;
                    break;
                default:
                    e.Style.TextColor = Color.Orange;
                    break;
            }
        }
    }

    #endregion

    #region Helper Methods

    private async Task ExecuteCommandAsync(CommunityToolkit.Mvvm.Input.IAsyncRelayCommand? command, CancellationToken cancellationToken = default)
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
    /// Initiates the QuickBooks OAuth 2.0 authorization flow.
    /// Starts the callback handler, generates the authorization URL, and opens the browser.
    /// </summary>
    private async Task InitiateQuickBooksOAuthFlowAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("Starting QuickBooks OAuth flow");

            // Resolve services from the parent form's service provider
            var serviceProvider = ((Form?)FindForm())?.GetType().GetProperty("ServiceProvider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(FindForm()) as IServiceProvider
                ?? throw new InvalidOperationException("Unable to resolve service provider from parent form");

            // Start the OAuth callback handler (listens on port 5000)
            var callbackHandler = serviceProvider.GetService(typeof(QuickBooksOAuthCallbackHandler)) as QuickBooksOAuthCallbackHandler
                ?? throw new InvalidOperationException("QuickBooksOAuthCallbackHandler is not registered in DI");

            await callbackHandler.StartListeningAsync(cancellationToken);
            Logger.LogInformation("OAuth callback handler started on http://localhost:5000/callback");

            // Get the auth service to generate the authorization URL
            var authService = serviceProvider.GetService(typeof(IQuickBooksAuthService)) as IQuickBooksAuthService
                ?? throw new InvalidOperationException("IQuickBooksAuthService is not registered in DI");

            var authUrl = authService.GenerateAuthorizationUrl();
            Logger.LogInformation("Generated OAuth authorization URL");

            // Open the browser to the authorization URL
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = authUrl,
                UseShellExecute = true
            });

            Logger.LogInformation("Opened browser to QuickBooks authorization URL");

            // Give user instructions
            MessageBox.Show(
                "A browser window will open for QuickBooks authorization.\n\n" +
                "1. Log in with your QuickBooks Online account\n" +
                "2. Review and authorize the app\n" +
                "3. The browser will close automatically after authorization\n\n" +
                "If the browser doesn't open, please visit:\n" + authUrl,
                "QuickBooks Authorization",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            // Wait a bit for OAuth callback to complete (5 seconds)
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);

            // Check if OAuth succeeded by verifying token is available
            var token = await authService.GetAccessTokenAsync(cancellationToken);
            if (token != null)
            {
                Logger.LogInformation("OAuth authentication successful");
                MessageBox.Show(
                    "Authorization successful! Now fetching company information and accounts...",
                    "Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                // Fetch company info
                var companyService = serviceProvider.GetService(typeof(IQuickBooksCompanyInfoService)) as IQuickBooksCompanyInfoService;
                if (companyService != null)
                {
                    var companyInfo = await companyService.GetCompanyInfoAsync(cancellationToken);
                    if (companyInfo != null)
                    {
                        Logger.LogInformation("Fetched company info: {CompanyName}", companyInfo.CompanyName);
                    }
                }

                // Fetch Chart of Accounts
                var accountService = serviceProvider.GetService(typeof(IQuickBooksChartOfAccountsService)) as IQuickBooksChartOfAccountsService;
                if (accountService != null)
                {
                    var accounts = await accountService.FetchAccountsAsync(cancellationToken);
                    Logger.LogInformation("Fetched {AccountCount} accounts", accounts.Count);

                    // If no accounts exist, seed the sandbox
                    if (accounts.Count == 0)
                    {
                        Logger.LogInformation("No accounts found; seeding sandbox with municipal finance accounts...");
                        var seederService = serviceProvider.GetService(typeof(IQuickBooksSandboxSeederService)) as IQuickBooksSandboxSeederService;
                        if (seederService != null)
                        {
                            var seedResult = await seederService.SeedSandboxAsync(cancellationToken);
                            if (seedResult.IsSuccess)
                            {
                                MessageBox.Show(
                                    $"Sandbox seeded successfully!\n\nCreated {seedResult.AccountsCreated} accounts:\n" +
                                    $"{string.Join("\n", seedResult.CreatedAccounts.Take(10))}" +
                                    (seedResult.CreatedAccounts.Count > 10 ? "\n..." : ""),
                                    "Seeding Complete",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Information);
                            }
                            else
                            {
                                MessageBox.Show(
                                    $"Sandbox seeding completed with issues:\n{seedResult.Message}\n\n" +
                                    $"Created {seedResult.AccountsCreated} of {seedResult.AccountsAttempted} accounts",
                                    "Seeding Partial",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Warning);
                            }
                        }
                    }

                    // Update UI to display the accounts
                    if (_syncHistoryGrid != null && accounts.Count > 0)
                    {
                        try
                        {
                            // Create display records from fetched accounts
                            var accountRecords = accounts.Select(account => new QuickBooksSyncHistoryRecord
                            {
                                Timestamp = DateTime.UtcNow,
                                Operation = "Account Import",
                                Status = "Success",
                                RecordsProcessed = 1,
                                Message = $"{account.Name} ({account.AccountNumber ?? "N/A"}) - {account.Type}/{account.SubType}",
                                Duration = TimeSpan.Zero
                            }).ToList();

                            // Display accounts in sync history grid
                            _syncHistoryGrid.DataSource = accountRecords;

                            // Update ViewModel metrics
                            if (ViewModel != null)
                            {
                                ViewModel.AccountsImported = accounts.Count;
                                ViewModel.TotalRecordsSynced += accounts.Count;
                            }

                            Logger.LogInformation("Displayed {AccountCount} accounts in sync history grid", accounts.Count);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(ex, "Failed to display accounts in grid");
                            MessageBox.Show(
                                $"Error displaying accounts: {ex.Message}",
                                "Display Error",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                        }
                    }
                }

                // Stop the callback handler once OAuth is complete
                await callbackHandler.StopListeningAsync(cancellationToken);
            }
            else
            {
                Logger.LogWarning("OAuth authorization failed: no token received");
                MessageBox.Show(
                    "Authorization was not completed. Please try again.",
                    "Authorization Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to initiate OAuth flow");
            MessageBox.Show(
                $"Failed to start authorization: {ex.Message}\n\n" +
                "Please ensure port 5000 is available and try again.",
                "Authorization Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    #endregion

    #region CRUD Operations & Dialogs

    /// <summary>
    /// Shows disconnect confirmation dialog.
    /// Prevents accidental disconnection from QuickBooks.
    /// </summary>
    public async Task<bool> ShowDisconnectConfirmationAsync(CancellationToken cancellationToken = default)
    {
        var result = MessageBox.Show(
            "Are you sure you want to disconnect from QuickBooks?\n\n" +
            "You will need to reconnect to resume data synchronization.",
            "Confirm Disconnect",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        return result == DialogResult.Yes;
    }

    /// <summary>
    /// Shows clear history confirmation dialog.
    /// Prevents accidental deletion of sync history.
    /// </summary>
    public async Task<bool> ShowClearHistoryConfirmationAsync(CancellationToken cancellationToken = default)
    {
        var result = MessageBox.Show(
            "Are you sure you want to clear all sync history?\n\n" +
            "This action cannot be undone. All synchronization records will be permanently deleted.",
            "Confirm Clear History",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        return result == DialogResult.Yes;
    }

    /// <summary>
    /// Shows sync confirmation dialog.
    /// Informs user about the sync operation and allows cancellation.
    /// </summary>
    public async Task<bool> ShowSyncConfirmationAsync(string operation = "Sync", CancellationToken cancellationToken = default)
    {
        var result = MessageBox.Show(
            $"Start {operation} with QuickBooks?\n\n" +
            "This operation may take several minutes depending on the amount of data.",
            $"Confirm {operation}",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        return result == DialogResult.Yes;
    }

    /// <summary>
    /// Shows file picker dialog for exporting sync history.
    /// Returns the selected file path, or null if cancelled.
    /// </summary>
    public string? ShowExportFilePickerDialog()
    {
        var saveDialog = new SaveFileDialog
        {
            Title = "Export QuickBooks Sync History",
            Filter = "CSV Files (*.csv)|*.csv|Excel Files (*.xlsx)|*.xlsx|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
            DefaultExt = "csv",
            FileName = $"QuickBooks_SyncHistory_{DateTime.Now:yyyy-MM-dd_HHmmss}.csv",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
        };

        return saveDialog.ShowDialog() == DialogResult.OK ? saveDialog.FileName : null;
    }

    /// <summary>
    /// Shows sync record detail dialog.
    /// Displays full details of a selected sync history record.
    /// </summary>
    public void ShowSyncRecordDetailsDialog(dynamic record)
    {
        if (record == null) return;

        try
        {
            var timestamp = record.Timestamp?.ToString("g") ?? "Unknown";
            var operation = record.Operation ?? "Unknown";
            var status = record.Status ?? "Unknown";
            var recordsProcessed = record.RecordsProcessed?.ToString("N0") ?? "0";
            var duration = record.Duration?.ToString() ?? "Unknown";
            var message = record.Message ?? "(No details)";

            var details = $"Sync Record Details\n\n" +
                $"Timestamp: {timestamp}\n" +
                $"Operation: {operation}\n" +
                $"Status: {status}\n" +
                $"Records Processed: {recordsProcessed}\n" +
                $"Duration: {duration}\n\n" +
                $"Message:\n{message}";

            MessageBox.Show(details, "Sync Record Details", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error displaying sync record details");
            MessageBox.Show("Unable to display record details.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Shows sync result summary dialog after sync operation completes.
    /// Displays statistics about the completed sync.
    /// </summary>
    public void ShowSyncResultSummaryDialog(int recordsSynced, int successCount, int failureCount, string duration)
    {
        var result = MessageBox.Show(
            $"Sync Operation Complete\n\n" +
            $"Total Records: {recordsSynced:N0}\n" +
            $"Successful: {successCount:N0}\n" +
            $"Failed: {failureCount:N0}\n" +
            $"Duration: {duration}\n\n" +
            "Would you like to view the detailed sync history?",
            "Sync Summary",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information);

        if (result == DialogResult.Yes)
        {
            _syncHistoryGrid?.Focus();
        }
    }

    /// <summary>
    /// Shows connection settings dialog for configuring QuickBooks API credentials.
    /// </summary>
    public bool ShowConnectionSettingsDialog()
    {
        var message = "QuickBooks Connection Settings\n\n" +
            $"Status: {(ViewModel?.IsConnected == true ? "Connected" : "Disconnected")}\n" +
            $"Company: {ViewModel?.CompanyName ?? "Not connected"}\n" +
            $"Last Sync: {ViewModel?.LastSyncTime ?? "Never"}\n\n" +
            "Click OK to reconfigure connection settings.";

        var result = MessageBox.Show(
            message,
            "Connection Settings",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Information);

        return result == DialogResult.OK;
    }

    /// <summary>
    /// Retries a failed sync operation.
    /// Allows user to retry specific failed sync records.
    /// </summary>
    public async Task<bool> ShowRetryFailedSyncConfirmationAsync(dynamic failedRecord, CancellationToken cancellationToken = default)
    {
        try
        {
            var operation = failedRecord?.Operation ?? "Unknown";
            var timestamp = failedRecord?.Timestamp?.ToString("g") ?? "Unknown";
            var message = failedRecord?.Message ?? "Unknown";

            var message_text = $"Retry Failed Sync?\n\n" +
                $"Operation: {operation}\n" +
                $"Last Attempt: {timestamp}\n" +
                $"Reason: {message}\n\n" +
                "Click Yes to retry this operation.";

            var result = MessageBox.Show(
                message_text,
                "Retry Failed Sync",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            return result == DialogResult.Yes;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error showing retry confirmation");
            return false;
        }
    }

    /// <summary>
    /// Shows delete record confirmation dialog.
    /// Allows user to delete individual sync history records.
    /// </summary>
    public bool ShowDeleteRecordConfirmationDialog(dynamic record)
    {
        try
        {
            var operation = record?.Operation ?? "Unknown";
            var timestamp = record?.Timestamp?.ToString("g") ?? "Unknown";

            var message = $"Delete Sync Record?\n\n" +
                $"Operation: {operation}\n" +
                $"Timestamp: {timestamp}\n\n" +
                "This action cannot be undone.";

            var result = MessageBox.Show(
                message,
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            return result == DialogResult.Yes;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error showing delete confirmation");
            return false;
        }
    }

    /// <summary>
    /// Handles grid double-click to show sync record details.
    /// </summary>
    private void HandleSyncRecordDoubleClick(dynamic record)
    {
        if (record != null)
        {
            ShowSyncRecordDetailsDialog(record);
        }
    }

    /// <summary>
    /// Handles retry action for failed sync records.
    /// </summary>
    private async Task RetryFailedSyncAsync(dynamic failedRecord, CancellationToken cancellationToken = default)
    {
        if (failedRecord == null) return;

        var status = failedRecord?.Status;
        if (status != "Failed") return;

        if (await ShowRetryFailedSyncConfirmationAsync(failedRecord))
        {
            var operation = failedRecord?.Operation ?? "";
            if (operation.Contains("Sync"))
            {
                await ExecuteCommandAsync(ViewModel?.SyncDataCommand);
            }
            else if (operation.Contains("Import"))
            {
                await ExecuteCommandAsync(ViewModel?.ImportAccountsCommand);
            }
        }
    }

    /// <summary>
    /// Handles delete action for individual sync records.
    /// </summary>
    private void DeleteSyncRecord(dynamic record)
    {
        if (record == null) return;

        if (ShowDeleteRecordConfirmationDialog(record))
        {
            try
            {
                ViewModel?.SyncHistory?.Remove(record);
                string? operation = record?.Operation != null ? record.Operation.ToString() : null;
                Logger.LogInformation("Deleted sync record: {Operation}", operation);
                MessageBox.Show("Sync record deleted successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to delete sync record");
                MessageBox.Show($"Failed to delete record: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    #endregion

    #region Helper Methods

    private async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (ViewModel != null)
        {
            await ViewModel.CheckConnectionCommand.ExecuteAsync(null);
            await ViewModel.RefreshHistoryCommand.ExecuteAsync(null);
        }
    }

    private void ClosePanel()
    {
        var parent = Parent;
        parent?.Controls.Remove(this);
        Dispose();
    }

    #endregion

    #region Disposal

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Unsubscribe from ViewModel
            if (ViewModel != null)
            {
                ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }

            // Unsubscribe panel header events
            if (_panelHeader != null && _panelHeaderRefreshClickedHandler != null)
                _panelHeader.RefreshClicked -= _panelHeaderRefreshClickedHandler;
            if (_panelHeader != null && _panelHeaderCloseClickedHandler != null)
                _panelHeader.CloseClicked -= _panelHeaderCloseClickedHandler;

            // Unsubscribe button click events
            if (_connectButton != null && _connectButtonClickHandler != null)
                _connectButton.Click -= _connectButtonClickHandler;
            if (_disconnectButton != null && _disconnectButtonClickHandler != null)
                _disconnectButton.Click -= _disconnectButtonClickHandler;
            if (_testConnectionButton != null && _testConnectionButtonClickHandler != null)
                _testConnectionButton.Click -= _testConnectionButtonClickHandler;
            if (_syncDataButton != null && _syncDataButtonClickHandler != null)
                _syncDataButton.Click -= _syncDataButtonClickHandler;
            if (_importAccountsButton != null && _importAccountsButtonClickHandler != null)
                _importAccountsButton.Click -= _importAccountsButtonClickHandler;
            if (_refreshHistoryButton != null && _refreshHistoryButtonClickHandler != null)
                _refreshHistoryButton.Click -= _refreshHistoryButtonClickHandler;
            if (_clearHistoryButton != null && _clearHistoryButtonClickHandler != null)
                _clearHistoryButton.Click -= _clearHistoryButtonClickHandler;
            if (_exportHistoryButton != null && _exportHistoryButtonClickHandler != null)
                _exportHistoryButton.Click -= _exportHistoryButtonClickHandler;

            // Unsubscribe filter text box
            if (_filterTextBox != null && _filterTextBoxTextChangedHandler != null)
                _filterTextBox.TextChanged -= _filterTextBoxTextChangedHandler;

            // Unsubscribe grid events
            if (_syncHistoryGrid != null)
            {
                if (_gridSelectionChangedHandler != null)
                    _syncHistoryGrid.SelectionChanged -= _gridSelectionChangedHandler;
                if (_gridMouseDoubleClickHandler != null)
                    _syncHistoryGrid.MouseDoubleClick -= _gridMouseDoubleClickHandler;
                if (_gridQueryCellStyleHandler != null)
                    _syncHistoryGrid.QueryCellStyle -= _gridQueryCellStyleHandler;
            }

            // Dispose controls
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
            try { _filterTextBox?.Dispose(); } catch { }
            try { _syncProgressBar?.Dispose(); } catch { }
            try { _panelHeader?.Dispose(); } catch { }
            try { _loadingOverlay?.Dispose(); } catch { }
            try { _noDataOverlay?.Dispose(); } catch { }
            try { _mainSplitContainer?.Dispose(); } catch { }
            try { _connectionPanel?.Dispose(); } catch { }
            try { _operationsPanel?.Dispose(); } catch { }
            try { _summaryPanel?.Dispose(); } catch { }
            try { _historyPanel?.Dispose(); } catch { }

            Logger.LogDebug("QuickBooksPanel disposed");
        }

        base.Dispose(disposing);
    }

    #endregion
}
