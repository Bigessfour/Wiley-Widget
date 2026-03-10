using System;
using System.Collections.Generic;
using System.Threading;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProgressBarAdv = Syncfusion.Windows.Forms.Tools.ProgressBarAdv;
using ProgressBarStyles = Syncfusion.Windows.Forms.Tools.ProgressBarStyles;
using SfButton = Syncfusion.WinForms.Controls.SfButton;
using SfDataGrid = Syncfusion.WinForms.DataGrid.SfDataGrid;
using SfSkinManager = Syncfusion.WinForms.Controls.SfSkinManager;
using StatusBarAdv = Syncfusion.Windows.Forms.Tools.StatusBarAdv;
using SplitContainerAdv = Syncfusion.Windows.Forms.Tools.SplitContainerAdv;
using TextBoxExt = Syncfusion.Windows.Forms.Tools.TextBoxExt;
using GridTextColumn = Syncfusion.WinForms.DataGrid.GridTextColumn;
using GridNumericColumn = Syncfusion.WinForms.DataGrid.GridNumericColumn;
using Syncfusion.Drawing;
using Syncfusion.Windows.Forms;
using Syncfusion.WinForms.DataGrid.Events;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.Controls;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Themes;
using ThemeColors = WileyWidget.WinForms.Themes.ThemeColors;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using Syncfusion.Windows.Forms.Chart;
using Syncfusion.Windows.Forms.Gauge;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.ListView;
using Syncfusion.WinForms.Input;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Supporting;
using WileyWidget.WinForms.Factories;
using Syncfusion.WinForms.DataGrid.Enums;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Utilities;
// using WileyWidget.WinForms.Utils; // Consolidated
using WileyWidget.WinForms.ViewModels;

using WileyWidget.WinForms.Helpers;

namespace WileyWidget.WinForms.Controls.Panels;

/// <summary>
/// QuickBooks integration panel with full MVVM pattern, connection management, and sync history tracking.
/// Inherits from ScopedPanelBase for proper DI lifecycle management.
/// Uses Syncfusion API properly: Dock layout and SfSkinManager theming per Syncfusion documentation.
/// </summary>
[SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
public partial class QuickBooksPanel : ScopedPanelBase<QuickBooksViewModel>
{
    private const string QuickBooksOAuthPlaygroundRedirectUri = "https://developer.intuit.com/v2/OAuth2Playground/RedirectUrl";

    #region UI Controls

    private PanelHeader? _panelHeader;
    private LoadingOverlay? _loadingOverlay;
    private NoDataOverlay? _noDataOverlay;
    private StatusStrip? _statusStrip;
    private ToolStripStatusLabel? _statusLabel;
    private ToolStripStatusLabel? _statusConnectionBadge;  // Right-side connection indicator in status bar
    private ToolTip? _sharedTooltip;

    // Layout state management
    private bool _inResize = false; // Prevents resize recursion when adjusting panel heights
    private int _layoutNestingDepth = 0; // Hard limit on nesting depth — prevents runaway cascades
    private bool _splittersConfigured = false; // Ensures ConfigureSplitContainersSafely runs only once after layout
    private readonly HashSet<string> _splitterWidthRetryPending = new(StringComparer.Ordinal);

    // Main layout containers (organized using SplitContainerAdv for professional layout)
    private TableLayoutPanel? _content;
    private Panel? _mainPanel;
    private SplitContainerAdv? _splitContainerMain;
    private SplitContainerAdv? _splitContainerTop;
    private SplitContainerAdv? _splitContainerBottom;
    private Panel? _connectionPanel;
    private Panel? _operationsPanel;
    private Panel? _summaryPanel;
    private Panel? _historyPanel;

    // Connection Panel Controls (Dock-based layout)
    private Label? _connectionStatusLabel;
    private Label? _companyNameLabel;
    private Label? _lastSyncLabel;
    private SfButton? _connectButton;
    private SfButton? _manualConnectButton;
    private SfButton? _disconnectButton;
    private SfButton? _testConnectionButton;
    private SfButton? _diagnosticsButton;

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

    // Summary Panel KPI cards
    private KpiCardControl? _totalSyncsLabel;
    private KpiCardControl? _successfulSyncsLabel;
    private KpiCardControl? _failedSyncsLabel;
    private KpiCardControl? _totalRecordsLabel;
    private KpiCardControl? _accountsImportedLabel;
    private KpiCardControl? _avgDurationLabel;

    // Event handler storage (for proper cleanup in Dispose)
    private EventHandler? _panelHeaderRefreshClickedHandler;
    private EventHandler? _panelHeaderCloseClickedHandler;
    private EventHandler? _connectButtonClickHandler;
    private EventHandler? _manualConnectButtonClickHandler;
    private EventHandler? _disconnectButtonClickHandler;
    private EventHandler? _testConnectionButtonClickHandler;
    private EventHandler? _diagnosticsButtonClickHandler;
    private EventHandler? _syncDataButtonClickHandler;
    private EventHandler? _importAccountsButtonClickHandler;
    private EventHandler? _refreshHistoryButtonClickHandler;
    private EventHandler? _clearHistoryButtonClickHandler;
    private EventHandler? _exportHistoryButtonClickHandler;
    private EventHandler? _filterTextBoxTextChangedHandler;
    private SelectionChangedEventHandler? _gridSelectionChangedHandler;
    private MouseEventHandler? _gridMouseDoubleClickHandler;
    private QueryCellStyleEventHandler? _gridQueryCellStyleHandler;
    private Syncfusion.Windows.Forms.Tools.Events.SplitterMoveEventHandler? _splitterMovingHandler;
    private readonly SyncfusionControlFactory? _factory;

    #endregion

    #region DPI-Aware Sizing Helpers

    /// <summary>
    /// Converts a logical pixel height to DPI-aware device units.
    /// Used for consistent sizing across different monitor DPI settings.
    /// </summary>
    /// <param name="logicalPixels">Height in logical pixels (e.g., 180f)</param>
    /// <returns>DPI-scaled height in device units</returns>
    private static int DpiHeight(float logicalPixels) =>
        (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(logicalPixels);

    private static int GetSplitAxisLength(SplitContainerAdv splitter) =>
        splitter.Orientation == Orientation.Vertical
            ? splitter.Width
            : splitter.Height;

    /// <summary>
    /// Calculates the minimum height needed for summary panel based on its content structure.
    /// Returns DPI-aware height: optimized for internal card TableLayout distribution.
    /// </summary>
    private static int CalculateSummaryPanelMinHeight()
    {
        var headerHeight = DpiHeight(32f);
        var cardRowHeight = DpiHeight(72f);
        var interRowSpacing = DpiHeight(6f);
        var panelPadding = DpiHeight(12f);
        return headerHeight + (3 * cardRowHeight) + (2 * interRowSpacing) + panelPadding;
    }

    private static int CalculateConnectionFallbackMinHeight() => DpiHeight(196f);

    private static int CalculateOperationsFallbackMinHeight() => DpiHeight(176f);

    private static int ToolbarButtonHeightPx() => DpiHeight(LayoutTokens.ToolbarButtonHeight);

    private static int StandardLabelHeightPx() => DpiHeight(LayoutTokens.StandardControlHeight);

    private static int ComfortableLabelHeightPx() => DpiHeight(LayoutTokens.StandardControlHeightComfortable);

    private int GetPreferredPanelHeight(Control? panel, int fallbackHeight)
    {
        if (panel == null || panel.IsDisposed)
        {
            return fallbackHeight;
        }

        var width = panel.ClientSize.Width > 0 ? panel.ClientSize.Width : panel.Width;
        if (width <= 0)
        {
            return fallbackHeight;
        }

        try
        {
            panel.PerformLayout();
            var preferredHeight = panel.GetPreferredSize(new Size(width, 0)).Height;
            return Math.Max(fallbackHeight, preferredHeight);
        }
        catch
        {
            return fallbackHeight;
        }
    }

    private int CalculateTopSectionMinHeight()
    {
        var connectionMinHeight = GetPreferredPanelHeight(_connectionPanel, CalculateConnectionFallbackMinHeight());
        var operationsMinHeight = GetPreferredPanelHeight(_operationsPanel, CalculateOperationsFallbackMinHeight());
        return Math.Max(connectionMinHeight, operationsMinHeight);
    }

    private void UpdateTopSectionMinimumHeight()
    {
        if (_splitContainerMain == null || _splitContainerMain.IsDisposed || !_splitContainerMain.IsHandleCreated)
        {
            return;
        }

        var topSectionMinHeight = CalculateTopSectionMinHeight();
        ApplySplitterMinSizesWithConstraintCheck(
            _splitContainerMain,
            topSectionMinHeight,
            Math.Max(0, _splitContainerMain.Panel2MinSize),
            "MainTopSection");
        ClampSplitterSafely(_splitContainerMain);
    }

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
                var initializeStopwatch = Stopwatch.StartNew();
                await ViewModel.InitializeAsync(ct);
                initializeStopwatch.Stop();
                LogSlowUiOperation("LoadAsync.ViewModel.InitializeAsync", initializeStopwatch.ElapsedMilliseconds, warningThresholdMs: 1500);

                UpdateLoadingState();
                UpdateNoDataOverlay();

                // Check if QuickBooks is connected and prompt user if not
                if (!ViewModel.IsConnected)
                {
                    await ShowConnectionPromptAsync(ct);
                }

                IsLoaded = true;
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
                : new ValidationResult(false, errors.ToArray());
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
        if (_syncHistoryGrid != null)
        {
            _syncHistoryGrid.Focus();
        }
    }

    #endregion

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public QuickBooksPanel(QuickBooksViewModel viewModel, SyncfusionControlFactory controlFactory)
        : base(viewModel, ResolveLogger())
    {
        // IMPORTANT: base ctor calls OnViewModelResolved before this body runs.
        // _factory and split containers are not yet available at that point.
        // OnViewModelResolved is therefore a no-op; BuildPanelUI() is called here
        // after _factory and InitializeComponent have both completed.
        _factory = controlFactory ?? throw new ArgumentNullException(nameof(controlFactory));
        ThemeColors.EnsureThemeAssemblyLoaded(Logger);
        SafeSuspendAndLayout(InitializeComponent);
        BuildPanelUI();
    }

    private static ILogger ResolveLogger()
    {
        return Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ILogger<QuickBooksPanel>>(Program.Services)
            ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<QuickBooksPanel>.Instance;
    }

    private SyncfusionControlFactory Factory => _factory ?? ControlFactory;

    private IServiceProvider? ResolveServiceProvider()
    {
        return _scope?.ServiceProvider ?? Program.Services;
    }

    /// <summary>
    /// Enforces a DPI-aware minimum size once the Win32 handle is available and DeviceDpi
    /// is accurate.  Only raises the floor — never lowers it — so the Designer's
    /// static (720 × 520) value remains respected on standard-DPI machines.
    /// </summary>
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        var scaledMin = ScaleLogicalToDevice(LayoutTokens.StandardPanelMinimumSize);
        var newMin = new Size(
            Math.Max(MinimumSize.Width, scaledMin.Width),
            Math.Max(MinimumSize.Height, scaledMin.Height));

        if (newMin != MinimumSize)
        {
            MinimumSize = newMin;
            Logger.LogDebug(
                "QuickBooksPanel: MinimumSize raised to {W}×{H} after handle created (DeviceDpi={Dpi})",
                newMin.Width, newMin.Height, DeviceDpi);
        }
    }

    /// <summary>
    /// Called when the panel's visibility changes.
    /// Queues a final layout pass so splitters and content are sized correctly
    /// after the panel is shown.
    /// </summary>
    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);

        if (!Visible) return;

        // Use BeginInvoke so Syncfusion's docking resize messages have already been
        // processed before we enforce content minimums.
        BeginInvoke(new System.Action(() =>
        {
            if (Width > 500 && Height > 400)
            {
                EnforceMinimumContentHeight();

                // Layout the splitter containers themselves
                _splitContainerMain?.PerformLayout();
                _splitContainerTop?.PerformLayout();
                _splitContainerBottom?.PerformLayout();

                // Layout inner panels so their hosted controls render at correct positions
                _splitContainerMain?.Panel1?.PerformLayout();
                _splitContainerMain?.Panel2?.PerformLayout();
                _splitContainerTop?.Panel1?.PerformLayout();
                _splitContainerTop?.Panel2?.PerformLayout();
                _splitContainerBottom?.Panel1?.PerformLayout();
                _splitContainerBottom?.Panel2?.PerformLayout();

                // Re-apply splitter affordance once controls are realized.
                ConfigureSplitterVisualAffordance();

                // Re-apply content-aware splitter defaults after docking is stable.
                ConfigureSplitContainersSafely();
                if (_splitContainerMain != null) { ClampSplitterSafely(_splitContainerMain); }
                if (_splitContainerTop != null) { ClampSplitterSafely(_splitContainerTop); }
                if (_splitContainerBottom != null) { ClampSplitterSafely(_splitContainerBottom); }

                Logger.LogDebug("QuickBooksPanel.OnVisibleChanged: Final layout pass completed ({W}x{H})", Width, Height);
            }
        }));
    }

    /// <summary>
    /// Called after the ViewModel has been resolved. Initializes UI and bindings.
    /// Sets initial splitter distances to ensure proper panel layout and prevent button blocking.
    /// </summary>
    protected override void OnViewModelResolved(QuickBooksViewModel? viewModel)
    {
        base.OnViewModelResolved(viewModel);
        // UI building is deferred to BuildPanelUI(), called from the constructor body
        // after _factory and InitializeComponent have completed.
    }

    /// <summary>
    /// Creates all child controls and bindings. Called from the constructor body
    /// once _factory (injected) and split containers (from InitializeComponent) are ready.
    /// </summary>
    private void BuildPanelUI()
    {
        if (ViewModel is null) return;

        // Attach PanelHeader event handlers — the control is created by InitializeComponent
        // but InitializeControls() (dead code) was the only prior attachment site.
        if (_panelHeader != null)
        {
            _panelHeaderRefreshClickedHandler = async (s, e) => await RefreshAsync();
            _panelHeaderCloseClickedHandler = (s, e) => ClosePanel();
            _panelHeader.RefreshClicked += _panelHeaderRefreshClickedHandler;
            _panelHeader.CloseClicked += _panelHeaderCloseClickedHandler;
        }

        CreateConnectionPanel();
        CreateOperationsPanel();
        CreateSummaryPanel();
        CreateHistoryPanel();

        BindViewModel();
        ApplySyncfusionTheme();
        ConfigureSplitterVisualAffordance();

        // Attach a single shared SplitterMoving handler for clamping (prevents user drag exceptions)
        _splitterMovingHandler = OnSplitterMoving;
        _splitContainerMain!.SplitterMoving += _splitterMovingHandler;
        _splitContainerTop!.SplitterMoving += _splitterMovingHandler;
        _splitContainerBottom!.SplitterMoving += _splitterMovingHandler;

        Logger.LogDebug("QuickBooksPanel: ViewModel resolved and UI initialized");
    }

    private void ConfigureSplitterVisualAffordance()
    {
        if (_splitContainerMain == null || _splitContainerTop == null || _splitContainerBottom == null)
            return;

        // Syncfusion SplitContainerAdv samples use a 13px splitter width baseline.
        // Keep at or above that baseline (DPI-scaled) to avoid ArgumentOutOfRangeException.
        var minSplitterWidth = LayoutTokens.GetScaled(LayoutTokens.SplitterWidth);

        _splitContainerMain.BorderStyle = BorderStyle.FixedSingle;
        _splitContainerTop.BorderStyle = BorderStyle.FixedSingle;
        _splitContainerBottom.BorderStyle = BorderStyle.FixedSingle;

        TrySetSplitterWidth(_splitContainerMain, minSplitterWidth);
        TrySetSplitterWidth(_splitContainerTop, minSplitterWidth);
        TrySetSplitterWidth(_splitContainerBottom, minSplitterWidth);
    }

    private void TrySetSplitterWidth(SplitContainerAdv splitter, int preferredWidth)
    {
        var width = Math.Clamp(preferredWidth, 1, 30);

        // SplitContainerAdv can reject SplitterWidth early in initialization.
        // Defer and retry once after handles/layout are ready.
        if (!IsHandleCreated || !splitter.IsHandleCreated || splitter.Width <= 0 || splitter.Height <= 0)
        {
            Logger.LogDebug(
                "QuickBooksPanel: Deferring SplitterWidth assignment for {SplitterName} until handles/layout are ready",
                splitter.Name);
            QueueDeferredSplitterWidthAssignment(splitter, width);
            return;
        }

        if (TryAssignSplitterWidth(splitter, width))
        {
            return;
        }

        for (var fallbackWidth = width + 1; fallbackWidth <= 30; fallbackWidth++)
        {
            if (TryAssignSplitterWidth(splitter, fallbackWidth))
            {
                Logger.LogDebug(
                    "QuickBooksPanel: SplitterWidth fallback applied ({Preferred} -> {Fallback}) for {SplitterName}",
                    width,
                    fallbackWidth,
                    splitter.Name);
                return;
            }
        }

        for (var fallbackWidth = width - 1; fallbackWidth >= 1; fallbackWidth--)
        {
            if (TryAssignSplitterWidth(splitter, fallbackWidth))
            {
                Logger.LogDebug(
                    "QuickBooksPanel: SplitterWidth downgraded ({Preferred} -> {Fallback}) for {SplitterName}",
                    width,
                    fallbackWidth,
                    splitter.Name);
                return;
            }
        }

        var existing = GetSplitterWidthSafe(splitter);
        if (existing > 0)
        {
            Logger.LogDebug(
                "QuickBooksPanel: Retaining existing SplitterWidth {Existing} for {SplitterName}",
                existing,
                splitter.Name);
            return;
        }

        Logger.LogWarning(
            "QuickBooksPanel: Unable to determine a safe SplitterWidth for {SplitterName}",
            splitter.Name);
    }

    private void QueueDeferredSplitterWidthAssignment(SplitContainerAdv splitter, int preferredWidth)
    {
        if (IsDisposed || Disposing || splitter.IsDisposed)
        {
            return;
        }

        var retryKey = string.IsNullOrWhiteSpace(splitter.Name)
            ? $"SplitContainerAdv-{splitter.GetHashCode():X}"
            : splitter.Name;

        lock (_splitterWidthRetryPending)
        {
            if (!_splitterWidthRetryPending.Add(retryKey))
            {
                return;
            }
        }

        void ReleaseRetryKey()
        {
            lock (_splitterWidthRetryPending)
            {
                _splitterWidthRetryPending.Remove(retryKey);
            }
        }

        void RetryOnUiThread()
        {
            ReleaseRetryKey();

            try
            {
                if (!IsDisposed && !Disposing && !splitter.IsDisposed)
                {
                    TrySetSplitterWidth(splitter, preferredWidth);
                }
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "QuickBooksPanel: Deferred SplitterWidth retry failed for {SplitterName}", splitter.Name);
            }
        }

        void QueueRetry()
        {
            try
            {
                if (IsHandleCreated)
                {
                    BeginInvoke((MethodInvoker)RetryOnUiThread);
                }
                else
                {
                    RetryOnUiThread();
                }
            }
            catch (Exception ex)
            {
                ReleaseRetryKey();
                Logger.LogDebug(ex, "QuickBooksPanel: Failed to queue deferred SplitterWidth retry for {SplitterName}", splitter.Name);
            }
        }

        if (IsHandleCreated)
        {
            QueueRetry();
            return;
        }

        EventHandler? handleCreatedHandler = null;
        handleCreatedHandler = (_, _) =>
        {
            HandleCreated -= handleCreatedHandler;
            if (IsDisposed || Disposing || splitter.IsDisposed)
            {
                ReleaseRetryKey();
                return;
            }

            QueueRetry();
        };

        HandleCreated += handleCreatedHandler;
    }

    private static int GetSplitterWidthSafe(SplitContainerAdv splitter)
    {
        try
        {
            return splitter.SplitterWidth;
        }
        catch
        {
            return -1;
        }
    }

    private static bool TryAssignSplitterWidth(SplitContainerAdv splitter, int width)
    {
        try
        {
            splitter.SplitterWidth = width;
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }
    /// <summary>
    /// Called during layout to configure splitters once containers have valid dimensions.
    /// Defers configuration until handle is created and containers are large enough.
    /// </summary>
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);

        // Skip if already configured or if handle not yet created (InitializeComponent phase)
        if (_splittersConfigured || !IsHandleCreated)
            return;

        // Ensure containers exist and have reasonable dimensions (not tiny during early layout)
        if (_splitContainerMain == null || _splitContainerTop == null || _splitContainerBottom == null)
            return;

        // Only configure when main container AND nested containers have been laid out with valid dimensions.
        // _splitContainerTop (Dock=Fill in Panel1) must also be sized before we run configuration —
        // otherwise its Width reflects a stale value from a prior layout pass, causing false fallbacks.
        const int MinNonZeroHeight = 100;
        const int MinNonZeroWidth = 200;

        if (_splitContainerMain.Height >= MinNonZeroHeight &&
            _splitContainerMain.Width >= MinNonZeroWidth &&
            _splitContainerTop.Width >= MinNonZeroWidth)
        {
            ConfigureSplitContainersSafely();
            _splittersConfigured = true;
            Logger.LogDebug("QuickBooksPanel: Splitter configuration applied (Container: {Width}x{Height})",
                _splitContainerMain.Width, _splitContainerMain.Height);
        }
        else
        {
            Logger.LogTrace("QuickBooksPanel: Deferring splitter config - container too small ({Width}x{Height})",
                _splitContainerMain.Width, _splitContainerMain.Height);
        }
    }

    /// <summary>
    /// Loads the panel and initializes the ViewModel.
    /// </summary>
    protected override void OnPanelLoaded(EventArgs e)
    {
        base.OnPanelLoaded(e);
    }

    /// <summary>
    /// Handles resize events with responsive SfDataGrid layout and SplitContainerAdv adaptation.
    /// Includes recursion prevention and dynamic min-size clamping for narrow containers.
    /// Syncfusion constraint enforcement per: https://help.syncfusion.com/windowsforms/overview
    /// </summary>
    protected override void OnResize(EventArgs e)
    {
        // Hard limit on nesting depth — prevents runaway cascades if multiple panels fire OnResize
        if (_layoutNestingDepth > 3) return;

        // Primary guard: prevent re-entrant calls during layout
        if (_inResize) return;

        _inResize = true;
        _layoutNestingDepth++;

        try
        {
            // Suspend layout updates during entire operation to prevent intermediate redraws
            SuspendLayout();

            base.OnResize(e);

            // Only do heavy min-size clamping once the panel has finished expanding.
            // Panels first create controls at a tiny default size, then
            // grow them to the real size.  Running ClampMinSizesIfNeeded during that tiny
            // phase forces splitters to their absolute minimums and leaves controls crushed.
            if (Width > 500 && Height > 400)
            {
                // DYNAMICALLY CLAMP MIN SIZES: Syncfusion SplitContainerAdv constraint:
                // Panel1MinSize + Panel2MinSize + SplitterWidth must not exceed container dimension
                ClampMinSizesIfNeeded(_splitContainerMain);
                ClampMinSizesIfNeeded(_splitContainerTop);
                ClampMinSizesIfNeeded(_splitContainerBottom);

                // Adjust min sizes responsively based on current width (wide/medium/narrow)
                AdjustMinSizesForCurrentWidth();
            }
            else
            {
                Logger?.LogDebug("QuickBooksPanel: Deferring size clamp - container still too small ({W}x{H}), waiting for docking to finish sizing", Width, Height);
            }

            // Refresh all split containers to ensure they respect minimum sizes.
            // Height assignment and ClampSplitterSafely are always performed so basic layout
            // stays correct even during the initial small-size phase.
            if (_splitContainerMain != null)
            {
                _splitContainerMain.Height = Height - (_panelHeader?.Height ?? 0) - (_statusStrip?.Height ?? 0) - Padding.Vertical;

                // Clamp splitter distance within bounds
                ClampSplitterSafely(_splitContainerMain);
                _splitContainerMain.PerformLayout();
            }

            if (_splitContainerTop != null)
            {
                // Clamp splitter distance within bounds
                ClampSplitterSafely(_splitContainerTop);
                _splitContainerTop.PerformLayout();
            }

            if (_splitContainerBottom != null)
            {
                // Clamp splitter distance within bounds
                var summaryMin = CalculateSummaryPanelMinHeight();
                ClampSplitterSafely(_splitContainerBottom);
                _splitContainerBottom.PerformLayout();
            }

            // Refresh SfDataGrid on window resize for responsive column layout
            if (_syncHistoryGrid != null && IsHandleCreated)
            {
                try
                {
                    // SfDataGrid specific - refresh view for responsive column sizing
                    // AutoSizeColumnsMode already set in CreateSyncHistoryGrid, no need to set again
                    _syncHistoryGrid.Invalidate(true);
                }
                catch (Exception gridEx)
                {
                    Logger?.LogWarning(gridEx, "Failed to refresh SfDataGrid layout on resize");
                }
            }

            // Ensure main panel scrolls properly
            if (_mainPanel != null && IsHandleCreated)
            {
                try
                {
                    _mainPanel.SuspendLayout();
                    _mainPanel.PerformLayout();
                    _mainPanel.ResumeLayout(false);
                }
                catch (Exception panelEx)
                {
                    Logger?.LogWarning(panelEx, "Failed to perform layout on main panel");
                }
            }


            // Only enforce content-height minimums once the panel is at a real size
            if (Width > 500 && Height > 400)
            {
                EnforceMinimumContentHeight();
            }

            // Add safety clamp for all splitters
            if (_splitContainerMain != null) ClampSplitterSafely(_splitContainerMain!);
            if (_splitContainerTop != null) ClampSplitterSafely(_splitContainerTop!);
            if (_splitContainerBottom != null) ClampSplitterSafely(_splitContainerBottom!);

            // Log resize in Debug mode only to reduce log spam
            Logger?.LogDebug("QuickBooksPanel resized to {Width}x{Height}, nesting depth: {Depth}", Width, Height, _layoutNestingDepth);
        }
        finally
        {
            // Always resume layout and reset flags in correct order
            // ✅ PERF FIX: Use ResumeLayout(false) to prevent immediate layout thrashing
            ResumeLayout(false);
            _layoutNestingDepth--;
            _inResize = false;
        }
    }

    /// <summary>
    /// Enforces a minimum height for the panel calculated from its constituent parts.
    /// Ensures that even if the container is small, the panel maintains hit-testable areas.
    /// Triggers immediate resize and layout refresh to prevent clipping.
    /// </summary>
    private void EnforceMinimumContentHeight()
    {
        // _mainPanel is not used in the Designer-path layout; guard only on handle readiness.
        if (!IsHandleCreated) return;

        // Calculate absolute minimum needed for visual integrity (Summary + History Buffer)
        // Includes Connection/Operations height as well if they were visible
        int summaryHeight = CalculateSummaryPanelMinHeight();
        int historyHeight = DpiHeight(320f); // lowered
        int headerHeight = _panelHeader?.Height ?? DpiHeight(50f);
        int footerHeight = _statusStrip?.Height ?? DpiHeight(25f);
        int topSectionHeight = CalculateTopSectionMinHeight();

        int minNeeded = headerHeight + topSectionHeight + summaryHeight + historyHeight + footerHeight + Padding.Vertical * 4;

        if (MinimumSize.Height < minNeeded)
        {
            MinimumSize = new Size(MinimumSize.Width, minNeeded);
        }

        UpdateTopSectionMinimumHeight();

        // No Height = ... line here!

        // SfDataGrid row height safety
        if (_syncHistoryGrid != null)
        {
            _syncHistoryGrid.RowHeight = LayoutTokens.GetScaled(LayoutTokens.GridRowHeightMedium);
            // Ensure at least 5 rows + header are logically visible
            var gridMinHeight = (_syncHistoryGrid.RowHeight * 5) + DpiHeight(36f);
            _syncHistoryGrid.MinimumSize = new Size(0, gridMinHeight);
        }
    }

    /// <summary>
    /// Dynamically clamps Panel1MinSize and Panel2MinSize to prevent Syncfusion constraint violations.
    ///
    /// Syncfusion constraint: Panel1MinSize + Panel2MinSize + SplitterWidth ≤ container dimension
    ///
    /// When container is too narrow (< 300px for vertical, < 200px for horizontal),
    /// reduces min sizes to 50% of their current value to allow the splitter to remain functional.
    /// This prevents ArgumentOutOfRangeException during resize cycles.
    /// </summary>
    private void ClampMinSizesIfNeeded(SplitContainerAdv? splitter)
    {
        if (splitter == null || !splitter.IsHandleCreated)
            return;

        // Determine available dimension based on orientation
        int availableDimension = GetSplitAxisLength(splitter);

        int min1 = splitter.Panel1MinSize;
        int min2 = splitter.Panel2MinSize;
        int splitterWidth = splitter.SplitterWidth;

        // Threshold: if container is very narrow, reduce min sizes
        // Horizontal splitters need less width threshold; vertical can handle more
        int narrowThreshold = splitter.Orientation == Orientation.Horizontal ? 200 : 300;

        // Check if total minimum space exceeds available dimension
        int totalMinRequired = min1 + min2 + splitterWidth;

        if (availableDimension < narrowThreshold || availableDimension < totalMinRequired)
        {
            // Emergency: reduce min sizes to make splitter functional
            // New min size = 50% of original, with floor of 50px to keep panels hit-testable
            int reducedMin1 = Math.Max(50, min1 / 2);
            int reducedMin2 = Math.Max(50, min2 / 2);

            try
            {
                if (splitter.Panel1MinSize != reducedMin1)
                    splitter.Panel1MinSize = reducedMin1;
                if (splitter.Panel2MinSize != reducedMin2)
                    splitter.Panel2MinSize = reducedMin2;

                Logger.LogDebug(
                    "ClampMinSizesIfNeeded: {Orientation} splitter reduced min sizes. " +
                    "Old: Panel1Min={OldMin1}px, Panel2Min={OldMin2}px. " +
                    "New: Panel1Min={NewMin1}px, Panel2Min={NewMin2}px. " +
                    "Available={AvailableDim}px",
                    splitter.Orientation, min1, min2, reducedMin1, reducedMin2, availableDimension);
            }
            catch (ArgumentOutOfRangeException)
            {
                // Syncfusion threw due to constraint violation - log and continue
                // The next ClampSplitterSafely call will handle emergency fallback
                Logger.LogWarning(
                    "ClampMinSizesIfNeeded: Failed to reduce min sizes for {Orientation} splitter. " +
                    "Constraint violation detected. Will rely on ClampSplitterSafely fallback.",
                    splitter.Orientation);
            }
        }
    }

    private void ClampSplitterSafely(SplitContainerAdv? splitter)
    {
        if (splitter == null) return;

        int available = GetSplitAxisLength(splitter) - splitter.SplitterWidth;

        if (available <= 0)
        {
            return;
        }

        int min1 = splitter.Panel1MinSize;
        int min2 = splitter.Panel2MinSize;

        if (available < min1 + min2)
        {
            // Emergency fallback — tiny startup sizes can be smaller than any useful floor.
            // Reset both mins first to avoid Syncfusion validating against the old opposing min.
            int newMin1 = Math.Max(0, available / 2);
            int newMin2 = Math.Max(0, available - newMin1);

            splitter.Panel1MinSize = 0;
            splitter.Panel2MinSize = 0;

            if (newMin1 > 0)
            {
                splitter.Panel1MinSize = newMin1;
            }

            if (newMin2 > 0)
            {
                splitter.Panel2MinSize = newMin2;
            }

            available = GetSplitAxisLength(splitter) - splitter.SplitterWidth;
            min1 = splitter.Panel1MinSize;
            min2 = splitter.Panel2MinSize;
        }

        if (splitter.SplitterDistance < min1)
            splitter.SplitterDistance = min1;
        if (splitter.SplitterDistance > available - min2)
            splitter.SplitterDistance = available - min2;
    }

    /// <summary>
    /// Adjusts minimum sizes of all three splitters based on current panel width.
    /// Implements responsive behavior: wider container → larger min sizes; narrower → smaller min sizes.
    ///
    /// Thresholds (in logical pixels, DPI-aware):
    /// - Wide (≥800px):  Standard min sizes for professional appearance
    /// - Medium (500-799px): Reduced min sizes, 75% of standard
    /// - Narrow (<500px):   Minimal sizes, 50% of standard, but never below 50px
    ///
    /// Constraint-aware: Before setting Panel1MinSize, validates that the container can fit
    /// both Panel1MinSize + Panel2MinSize + SplitterWidth. If not, scales both down proportionally.
    /// This prevents ArgumentOutOfRangeException on extremely narrow containers.
    /// Called every OnResize to maintain responsive behavior during window resizing.
    /// </summary>
    private void AdjustMinSizesForCurrentWidth()
    {
        if (_splitContainerTop == null || _splitContainerBottom == null || _splitContainerMain == null)
            return;

        // Current container width (use Main width as reference)
        int currentWidth = _splitContainerMain.Width;

        // Define responsive thresholds (in DPI-aware logical pixels)
        // These values scale with DPI, so the responsive behavior is consistent across monitors
        int wideThreshold = DpiHeight(800f);     // ≥800px = wide
        int mediumThreshold = DpiHeight(500f);   // 500-799px = medium
        // Narrow is <500px

        // MODEST MIN SIZES for professional appearance (per Syncfusion SplitContainerAdv best practices)
        // These are starting sizes; responsive scaling (wide/medium/narrow) adjusts them as needed
        const int mainTopMinSize = 220;          // Main top split: connection + operations region
        const int mainBottomMinSize = 380;       // Main bottom split: summary + history region
        const int connectionMinSize = 560;       // Connection panel minimum width at 96 DPI
        const int operationMinSize = 240;        // Operations panel minimum width at 96 DPI
        const int summaryMinSize = 180;          // Summary panel minimum height at 96 DPI
        const int historyMinSize = 240;          // History panel minimum height at 96 DPI

        // Calculate adjusted sizes based on width
        int topMin1, topMin2, bottomMin1, bottomMin2, mainMin1, mainMin2;
        int topSectionMinHeight = CalculateTopSectionMinHeight();

        if (currentWidth >= wideThreshold)
        {
            // WIDE: Use standard sizes
            topMin1 = connectionMinSize;
            topMin2 = operationMinSize;
            bottomMin1 = summaryMinSize;
            bottomMin2 = historyMinSize;
            mainMin1 = Math.Max(mainTopMinSize, topSectionMinHeight);
            mainMin2 = mainBottomMinSize;
        }
        else if (currentWidth >= mediumThreshold)
        {
            // MEDIUM: 75% of standard
            topMin1 = Math.Max(50, (int)(connectionMinSize * 0.75f));
            topMin2 = Math.Max(50, (int)(operationMinSize * 0.75f));
            bottomMin1 = Math.Max(50, (int)(summaryMinSize * 0.75f));
            bottomMin2 = Math.Max(50, (int)(historyMinSize * 0.75f));
            mainMin1 = Math.Max(Math.Max(50, (int)(mainTopMinSize * 0.75f)), topSectionMinHeight);
            mainMin2 = Math.Max(50, (int)(mainBottomMinSize * 0.75f));
        }
        else
        {
            // NARROW: 50% of standard, minimum 50px
            topMin1 = Math.Max(50, (int)(connectionMinSize * 0.5f));
            topMin2 = Math.Max(50, (int)(operationMinSize * 0.5f));
            bottomMin1 = Math.Max(50, (int)(summaryMinSize * 0.5f));
            bottomMin2 = Math.Max(50, (int)(historyMinSize * 0.5f));
            mainMin1 = Math.Max(Math.Max(50, (int)(mainTopMinSize * 0.5f)), topSectionMinHeight);
            mainMin2 = Math.Max(50, (int)(mainBottomMinSize * 0.5f));
        }

        // Apply adjusted min sizes with constraint checking
        try
        {
            // Helper: Safely set both Panel1MinSize and Panel2MinSize while respecting constraints
            ApplySplitterMinSizesWithConstraintCheck(_splitContainerTop, topMin1, topMin2, "Top");
            ApplySplitterMinSizesWithConstraintCheck(_splitContainerBottom, bottomMin1, bottomMin2, "Bottom");
            ApplySplitterMinSizesWithConstraintCheck(_splitContainerMain, mainMin1, mainMin2, "Main");
            UpdateTopSectionMinimumHeight();

            // Log in debug mode for responsive behavior verification
            Logger.LogDebug(
                "AdjustMinSizesForCurrentWidth: Width={CurrentWidth}px. " +
                "TopSplitter=[{TopMin1}, {TopMin2}], BottomSplitter=[{BottomMin1}, {BottomMin2}], MainSplitter=[{MainMin1}, {MainMin2}]",
                currentWidth, topMin1, topMin2, bottomMin1, bottomMin2, mainMin1, mainMin2);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            // If constraint violation occurs, log and rely on ClampMinSizesIfNeeded to handle it
            Logger.LogWarning(ex,
                "AdjustMinSizesForCurrentWidth: Constraint violation detected at width {Width}px. " +
                "Falling back to emergency clamping. This typically indicates a very narrow container.",
                currentWidth);

            // Emergency fallback: clamp all splitters to safe values
            ClampSplitterSafely(_splitContainerTop);
            ClampSplitterSafely(_splitContainerBottom);
            ClampSplitterSafely(_splitContainerMain);
        }
    }

    /// <summary>
    /// Safely applies Panel1MinSize and Panel2MinSize to a splitter while respecting Syncfusion constraints.
    /// Constraint: Panel1MinSize + Panel2MinSize + SplitterWidth ≤ container dimension
    /// If the constraint would be violated, scales both sizes down proportionally.
    /// </summary>
    private void ApplySplitterMinSizesWithConstraintCheck(SplitContainerAdv splitter, int requestedMin1, int requestedMin2, string splitterName)
    {
        if (splitter == null || splitter.IsDisposed || !splitter.IsHandleCreated)
            return;

        int containerDim = GetSplitAxisLength(splitter);
        int splitterThickness = Math.Max(0, splitter.SplitterWidth);
        int availableDim = Math.Max(0, containerDim - splitterThickness);

        int safeRequestedMin1 = Math.Max(0, requestedMin1);
        int safeRequestedMin2 = Math.Max(0, requestedMin2);
        int actualMin1 = safeRequestedMin1;
        int actualMin2 = safeRequestedMin2;

        // Narrow-container scenario:
        // If requested minimum sizes cannot fit into the available panel space (container minus splitter),
        // reduce both values before touching Syncfusion setters so Panel1MinSize/Panel2MinSize never violate constraints.
        if (actualMin1 + actualMin2 > availableDim)
        {
            int totalRequested = Math.Max(1, actualMin1 + actualMin2);
            actualMin1 = (int)Math.Round((double)actualMin1 * availableDim / totalRequested);
            actualMin1 = Math.Clamp(actualMin1, 0, availableDim);
            actualMin2 = Math.Max(0, availableDim - actualMin1);

            // Emergency ratio fallback if proportional rounding still produces an unusable split.
            if (availableDim > 1 && (actualMin1 == 0 || actualMin2 == 0) && safeRequestedMin1 > 0 && safeRequestedMin2 > 0)
            {
                actualMin1 = Math.Clamp((int)Math.Round(availableDim * 0.30), 1, availableDim - 1);
                actualMin2 = Math.Max(1, availableDim - actualMin1);
            }

            Logger.LogDebug(
                "ApplySplitterMinSizesWithConstraintCheck: {Name} splitter constraint adjusted. " +
                "Requested=[{Req1}, {Req2}], Available={Available}, Adjusted=[{Act1}, {Act2}]",
                splitterName, requestedMin1, requestedMin2, availableDim, actualMin1, actualMin2);
        }

        actualMin1 = Math.Clamp(actualMin1, 0, availableDim);
        actualMin2 = Math.Clamp(actualMin2, 0, Math.Max(0, availableDim - actualMin1));

        int currentMin1 = Math.Max(0, splitter.Panel1MinSize);
        int currentMin2 = Math.Max(0, splitter.Panel2MinSize);

        bool canSetPanel1First = actualMin1 + currentMin2 <= availableDim;
        bool canSetPanel2First = currentMin1 + actualMin2 <= availableDim;

        try
        {
            if (canSetPanel1First)
            {
                if (splitter.Panel1MinSize != actualMin1)
                    splitter.Panel1MinSize = actualMin1;
                if (splitter.Panel2MinSize != actualMin2)
                    splitter.Panel2MinSize = actualMin2;
                return;
            }

            if (canSetPanel2First)
            {
                if (splitter.Panel2MinSize != actualMin2)
                    splitter.Panel2MinSize = actualMin2;
                if (splitter.Panel1MinSize != actualMin1)
                    splitter.Panel1MinSize = actualMin1;
                return;
            }

            int bridgePanel1 = Math.Max(0, availableDim - currentMin2);
            if (splitter.Panel1MinSize != bridgePanel1)
                splitter.Panel1MinSize = bridgePanel1;

            if (splitter.Panel2MinSize != actualMin2)
                splitter.Panel2MinSize = actualMin2;
            if (splitter.Panel1MinSize != actualMin1)
                splitter.Panel1MinSize = actualMin1;
        }
        catch (ArgumentOutOfRangeException ex)
        {
            Logger.LogWarning(ex,
                "ApplySplitterMinSizesWithConstraintCheck: {Name} splitter min-size constraint rejected. " +
                "Requested=[{Req1}, {Req2}], Available={Available}. Keeping safe fallback values.",
                splitterName, requestedMin1, requestedMin2, availableDim);

            try
            {
                int fallbackMin1 = Math.Clamp((int)Math.Round(availableDim * 0.30), 0, availableDim);
                int fallbackMin2 = Math.Max(0, availableDim - fallbackMin1);

                int maxPanel1WithCurrentPanel2 = Math.Max(0, availableDim - splitter.Panel2MinSize);
                if (splitter.Panel1MinSize > maxPanel1WithCurrentPanel2)
                    splitter.Panel1MinSize = maxPanel1WithCurrentPanel2;

                if (splitter.Panel2MinSize != fallbackMin2)
                    splitter.Panel2MinSize = fallbackMin2;
                if (splitter.Panel1MinSize != fallbackMin1)
                    splitter.Panel1MinSize = fallbackMin1;
            }
            catch (ArgumentOutOfRangeException fallbackEx)
            {
                Logger.LogWarning(fallbackEx,
                    "ApplySplitterMinSizesWithConstraintCheck: {Name} splitter emergency fallback could not be fully applied. " +
                    "Deferring to existing splitter clamping logic.",
                    splitterName);
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
    /// Initializes all UI controls and layout using professional panel-based design.
    /// Ensures NO layout recursion through explicit AutoSize = false on all docked Top panels.
    /// Uses TableLayoutPanel for consistent sizing and DPI-aware heights throughout.
    ///
    /// Panel Height Strategy (DPI-aware logical units):
    /// - _summaryPanel:      200 px (header 28 + 2×60 row cards + padding)
    /// - _connectionPanel:   180 px (header 28 + 3 status lines + button row 36 + padding)
    /// - _operationsPanel:   160 px (header 28 + button row 36 + progress bar 25 + padding)
    /// - _historyPanel:      Fill (MinimumSize 350 px)
    ///
    /// Visual Polish:
    /// - FixedSingle borders on all sections for visual separation
    /// - Increased vertical spacing (15-20 px) between sections
    /// - Consistent font sizes (title 10pt, content 9-10pt)
    /// - DPI-aware button sizing using DpiHeight() helper
    /// - Semantic status colors (green/red) for connection indicator only
    /// </summary>
    private void InitializeControls()
    {
        SuspendLayout();

        Name = "QuickBooksPanel";
        Size = ScaleLogicalToDevice(LayoutTokens.DefaultDashboardPanelSize);
        MinimumSize = ScaleLogicalToDevice(LayoutTokens.StandardPanelMinimumSize);
        Padding = Padding.Empty;
        Dock = DockStyle.Fill;

        // Shared tooltip for all controls
        _sharedTooltip = new ToolTip
        {
            AutoPopDelay = 5000,
            InitialDelay = 500,
            ReshowDelay = 100,
            ShowAlways = true
        };

        // Panel header
        _panelHeader = new PanelHeader
        {
            Dock = DockStyle.Top,
            Height = DpiHeight(50f),
            Title = "QuickBooks Integration",
            AccessibleName = "QuickBooks Panel Header",
            AccessibleDescription = "Header for QuickBooks integration panel"
        };

        // Store handlers for cleanup
        _panelHeaderRefreshClickedHandler = async (s, e) => await RefreshAsync();
        _panelHeaderCloseClickedHandler = (s, e) => ClosePanel();

        _panelHeader.RefreshClicked += _panelHeaderRefreshClickedHandler;
        _panelHeader.CloseClicked += _panelHeaderCloseClickedHandler;

        // Main panel — no AutoScroll; size is enforced via MinimumSize + DPI helper on handle created
        _mainPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = Padding.Empty,
            BorderStyle = BorderStyle.None,
            AutoScroll = false,
            AutoSize = false
        };

        // Create the four main content panels
        CreateConnectionPanel();
        CreateOperationsPanel();
        CreateSummaryPanel();
        CreateHistoryPanel();

        // Hierarchical SplitContainerAdv Layout:
        // Main (Horizontal Splitter) -> Top (Vertical Splitter: Conn vs Ops) + Bottom (Horizontal Splitter: Summary vs History)

        _splitContainerTop = Factory.CreateSplitContainerAdv(splitter =>
        {
            splitter.Name = "SplitContainerTop";
            splitter.Dock = DockStyle.Fill;
            splitter.Orientation = System.Windows.Forms.Orientation.Vertical;
            splitter.IsSplitterFixed = true;  // Locked: top strip is a fixed two-column layout, not user-draggable
            splitter.SplitterWidth = LayoutTokens.GetScaled(LayoutTokens.SplitterWidth);
            splitter.BorderStyle = BorderStyle.None;
        });
        _splitContainerTop.Panel1.Controls.Add(_connectionPanel!);
        _splitContainerTop.Panel2.Controls.Add(_operationsPanel!);

        _splitContainerBottom = Factory.CreateSplitContainerAdv(splitter =>
        {
            splitter.Name = "SplitContainerBottom";
            splitter.Dock = DockStyle.Fill;
            splitter.Orientation = System.Windows.Forms.Orientation.Horizontal;
            splitter.IsSplitterFixed = false;
            splitter.SplitterWidth = LayoutTokens.GetScaled(LayoutTokens.SplitterWidth);
            splitter.BorderStyle = BorderStyle.None;
        });
        _splitContainerBottom.Panel1.Controls.Add(_summaryPanel!);
        _splitContainerBottom.Panel2.Controls.Add(_historyPanel!);

        _splitContainerMain = Factory.CreateSplitContainerAdv(splitter =>
        {
            splitter.Name = "SplitContainerMain";
            splitter.Dock = DockStyle.Fill;
            splitter.Orientation = System.Windows.Forms.Orientation.Horizontal;
            splitter.IsSplitterFixed = false;
            splitter.SplitterWidth = LayoutTokens.GetScaled(LayoutTokens.SplitterWidth);
            splitter.BorderStyle = BorderStyle.None;
        });
        _splitContainerMain.Panel1.Controls.Add(_splitContainerTop);
        _splitContainerMain.Panel2.Controls.Add(_splitContainerBottom);

        _mainPanel.Controls.Add(_splitContainerMain);

        _content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = Padding.Empty,
            Margin = Padding.Empty,
            AutoSize = false,
            Name = "QuickBooksPanelContent"
        };
        _content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _content.RowStyles.Add(new RowStyle(SizeType.Absolute, _panelHeader.Height));
        _content.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _content.Controls.Add(_panelHeader, 0, 0);
        _content.Controls.Add(_mainPanel, 0, 1);

        Controls.Add(_content);

        // Force initial layout calculation before setting SplitterDistance
        this.SuspendLayout();
        _mainPanel.SuspendLayout();
        _splitContainerMain.SuspendLayout();
        _splitContainerTop.SuspendLayout();
        _splitContainerBottom.SuspendLayout();

        _splitContainerMain.PerformLayout();
        _splitContainerTop.PerformLayout();
        _splitContainerBottom.PerformLayout();
        _mainPanel.PerformLayout();
        this.PerformLayout();

        this.ResumeLayout(false);
        _mainPanel.ResumeLayout(false);
        _splitContainerMain.ResumeLayout(false);
        _splitContainerTop.ResumeLayout(false);
        _splitContainerBottom.ResumeLayout(false);

        // Configure splitter min sizes for responsive layout
        // MODEST MIN SIZES: Professional appearance with responsive scaling via AdjustMinSizesForCurrentWidth()
        // Syncfusion constraint: Panel1MinSize + Panel2MinSize + SplitterWidth ≤ container dimension
        // These are modest starting sizes; responsive scaling kicks in during OnResize for narrow containers
        _splitContainerTop!.Panel1MinSize = DpiHeight(560f);
        _splitContainerTop!.Panel2MinSize = DpiHeight(240f);
        _splitContainerTop!.SplitterDistance = DpiHeight(880f);

        _splitContainerBottom!.Panel1MinSize = Math.Max(DpiHeight(160f), CalculateSummaryPanelMinHeight() - DpiHeight(12f));
        _splitContainerBottom!.Panel2MinSize = DpiHeight(240f);
        _splitContainerBottom!.SplitterDistance = CalculateSummaryPanelMinHeight() + DpiHeight(8f);

        // Outer main splitter with modest min sizes for top/bottom balance
        _splitContainerMain!.Panel1MinSize = Math.Max(CalculateTopSectionMinHeight(), DpiHeight(220f));
        _splitContainerMain!.Panel2MinSize = DpiHeight(380f);
        _splitContainerMain!.SplitterDistance = Math.Max(CalculateTopSectionMinHeight() + DpiHeight(16f), DpiHeight(336f));

        // SizeChanged handlers are handled by SafeSplitterDistanceHelper for automatic clamping

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

        // Status strip — SizingGrip=false gives a clean bottom edge; spring keeps badge right-aligned
        _statusStrip = new StatusStrip
        {
            Dock = DockStyle.Bottom,
            Height = DpiHeight(26f),
            Name = "StatusStrip",
            SizingGrip = false,  // Remove the chunky resize triangle from bottom-right corner
            AccessibleName = "Status Bar",
            AccessibleDescription = "Displays current operation status and connection state"
        };
        _statusLabel = new ToolStripStatusLabel
        {
            Text = "Ready",
            Name = "StatusLabel",
            Spring = true,       // Stretches to fill available width, pushing the badge to the right
            TextAlign = ContentAlignment.MiddleLeft
        };
        // Right-aligned connection status badge
        _statusConnectionBadge = new ToolStripStatusLabel
        {
            Text = "\u25cf Not Connected",  // ● Not Connected
            Name = "ConnectionBadge",
            ForeColor = Color.OrangeRed,   // Semantic status color — exception to theme rule
            TextAlign = ContentAlignment.MiddleRight,
            AccessibleName = "Connection Badge",
            AccessibleDescription = "Shows current QuickBooks connection state"
        };
        _statusStrip.Items.Add(_statusLabel);
        _statusStrip.Items.Add(_statusConnectionBadge);
        Controls.Add(_statusStrip);

        // Finalize layout
        EnforceMinimumContentHeight();

        ResumeLayout(false);

        Logger.LogDebug("[PANEL] QuickBooksPanel initialized with hierarchical split layout");
    }

    /// <summary>
    /// Creates the summary panel with KPI metrics using TableLayoutPanel.
    /// Uses Absolute row sizing for consistent card heights and prevents layout collapse.
    /// Each card has a fixed preferred size for professional appearance and readability.
    /// </summary>
    private void CreateSummaryPanel()
    {
        if (_summaryPanel == null)
        {
            _summaryPanel = new Panel();
            if (_splitContainerBottom != null && !_splitContainerBottom.Panel1.Controls.Contains(_summaryPanel))
            {
                _splitContainerBottom.Panel1.Controls.Add(_summaryPanel);
            }
        }
        else
        {
            _summaryPanel.Controls.Clear();
        }

        _summaryPanel.Padding = LayoutTokens.GetScaled(LayoutTokens.SectionPanelPadding);
        _summaryPanel.BorderStyle = BorderStyle.FixedSingle;
        _summaryPanel.AutoSize = false;
        _summaryPanel.AutoScroll = false;

        // Bold section header with painted rule — clean visual hierarchy
        var summaryHeader = new Label
        {
            Text = "QuickBooks Summary",
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = DpiHeight(30f),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font(Font.FontFamily, 8.25f, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 4),
            AccessibleName = "Summary Header",
            AccessibleDescription = "Header for QuickBooks summary metrics"
        };
        summaryHeader.Paint += static (s, pe) =>
        {
            var lbl = (Label)s!;
            using var rule = new Pen(SystemColors.ControlDark, 1);
            pe.Graphics.DrawLine(rule, 0, lbl.Height - 2, lbl.Width, lbl.Height - 2);
        };
        _summaryPanel.Controls.Add(summaryHeader);

        var cardHeight = DpiHeight(52f);
        var cardMinimumWidth = DpiHeight(72f);
        var cardMargin = new Padding(DpiHeight(2f));
        var cardRowHeight = cardHeight + cardMargin.Top + cardMargin.Bottom;

        // Keep the KPI strip dense so the 3x2 layout fits its band instead of stretching vertically.
        var tableLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(0, DpiHeight(2f), 0, 0),
            Margin = Padding.Empty
        };

        tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        tableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, cardRowHeight));
        tableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, cardRowHeight));
        tableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, cardRowHeight));

        _totalSyncsLabel = new KpiCardControl
        {
            Title = "Total Syncs",
            Value = "0",
            Dock = DockStyle.Fill,
            MinimumSize = new Size(cardMinimumWidth, cardHeight),
            Margin = cardMargin
        };
        _sharedTooltip?.SetToolTip(_totalSyncsLabel, "Total number of synchronizations performed (all time)");
        tableLayout.Controls.Add(_totalSyncsLabel, 0, 0);

        _successfulSyncsLabel = new KpiCardControl
        {
            Title = "Successful",
            Value = "0",
            Dock = DockStyle.Fill,
            MinimumSize = new Size(cardMinimumWidth, cardHeight),
            Margin = cardMargin
        };
        _sharedTooltip?.SetToolTip(_successfulSyncsLabel, "Number of successful sync operations");
        tableLayout.Controls.Add(_successfulSyncsLabel, 1, 0);

        _failedSyncsLabel = new KpiCardControl
        {
            Title = "Failed",
            Value = "0",
            Dock = DockStyle.Fill,
            MinimumSize = new Size(cardMinimumWidth, cardHeight),
            Margin = cardMargin
        };
        _sharedTooltip?.SetToolTip(_failedSyncsLabel, "Number of failed sync operations (needs attention)");
        tableLayout.Controls.Add(_failedSyncsLabel, 0, 1);

        _totalRecordsLabel = new KpiCardControl
        {
            Title = "Records",
            Value = "0",
            Dock = DockStyle.Fill,
            MinimumSize = new Size(cardMinimumWidth, cardHeight),
            Margin = cardMargin
        };
        _sharedTooltip?.SetToolTip(_totalRecordsLabel, "Total records processed during syncs");
        tableLayout.Controls.Add(_totalRecordsLabel, 1, 1);

        _accountsImportedLabel = new KpiCardControl
        {
            Title = "Accounts",
            Value = "0",
            Dock = DockStyle.Fill,
            MinimumSize = new Size(cardMinimumWidth, cardHeight),
            Margin = cardMargin
        };
        _sharedTooltip?.SetToolTip(_accountsImportedLabel, "Number of accounts imported from QuickBooks");
        tableLayout.Controls.Add(_accountsImportedLabel, 0, 2);

        _avgDurationLabel = new KpiCardControl
        {
            Title = "Avg Duration",
            Value = "0s",
            Dock = DockStyle.Fill,
            MinimumSize = new Size(cardMinimumWidth, cardHeight),
            Margin = cardMargin
        };
        _sharedTooltip?.SetToolTip(_avgDurationLabel, "Average duration of sync operations (seconds)");
        tableLayout.Controls.Add(_avgDurationLabel, 1, 2);

        _summaryPanel.Controls.Add(tableLayout);
    }

    /// <summary>
    /// Creates a professional metric card for the summary panel with explicit sizing.
    /// Stores reference to value label for dynamic binding updates.
    /// </summary>
    /// <param name="title">Card title (e.g., "Total Syncs")</param>
    /// <param name="value">Initial card value (e.g., "0")</param>
    /// <param name="cardHeight">DPI-aware height for card (includes title and value space)</param>
    /// <returns>The cardPanel ready to add to TableLayoutPanel</returns>
    private Panel CreateMetricCard(string title, string value, int cardHeight)
    {
        var cardPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = LayoutTokens.GetScaled(LayoutTokens.CardMargin),
            BorderStyle = BorderStyle.FixedSingle,
            Padding = LayoutTokens.GetScaled(LayoutTokens.ContentInnerPadding),
            AutoSize = false, // Explicit false: let parent TableLayoutPanel control size
            Height = cardHeight,
            MinimumSize = new Size(DpiHeight(100f), cardHeight) // Ensure card doesn't undersize
        };

        // Use TableLayoutPanel for vertical stack to balance spacing per recommendation
        var cardLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(0),
            AutoSize = false
        };

        // Distribution: 10% Top Small, 20% Title, 40% Spacer, 30% Value
        // This eliminates excessive whitespace while keeping content legible on high DPI
        cardLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 10f)); // Top small number/metric
        cardLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 20f)); // Title label
        cardLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 40f)); // Flexible spacer (absorbs whitespace)
        cardLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 30f)); // Large value

        // Top small label (secondary info)
        var topSmallLabel = new Label
        {
            Text = "",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopCenter,
            Visible = false // Only show if we start using it
        };
        cardLayout.Controls.Add(topSmallLabel, 0, 0);

        // Title: centered portion of card
        var titleLabel = new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            AccessibleName = $"{title} Title"
        };
        cardLayout.Controls.Add(titleLabel, 0, 1);

        // Spacer panel to absorb vertical space
        var spacer = new Panel { Dock = DockStyle.Fill };
        cardLayout.Controls.Add(spacer, 0, 2);

        // Value: bottom portion, large font for impact
        var valueLabel = new Label
        {
            Text = value,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            AccessibleName = $"{title} Value"
        };
        _sharedTooltip?.SetToolTip(valueLabel, $"Displays the current {title.ToLower(System.Globalization.CultureInfo.InvariantCulture)} count");
        cardLayout.Controls.Add(valueLabel, 0, 3);

        cardPanel.Controls.Add(cardLayout);

        return cardPanel;
    }

    /// <summary>
    /// Creates the top panel with connection and operations panels side-by-side.
    /// Uses SafeSplitterDistanceHelper to avoid SplitterDistance out-of-bounds exceptions.
    /// </summary>
    private Panel CreateTopPanel()
    {
        var topPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            Padding = Padding.Empty
        };

        // Vertical split: left = connection, right = operations (safe deferred sizing)
        var splitTop = Factory.CreateSplitContainerAdv(splitter =>
        {
            splitter.Dock = DockStyle.Fill;
            splitter.Orientation = Orientation.Vertical;
            splitter.FixedPanel = Syncfusion.Windows.Forms.Tools.Enums.FixedPanel.None;
        });

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
    /// <summary>
    /// Creates the connection panel with status and buttons using TableLayoutPanel.
    /// Uses Absolute row sizing to ensure buttons and status labels remain visible and properly spaced.
    /// Provides professional visual separation with borders and consistent spacing.
    /// </summary>
    private void CreateConnectionPanel()
    {
        if (_connectionPanel == null)
        {
            _connectionPanel = new Panel();
            if (_splitContainerTop != null && !_splitContainerTop.Panel1.Controls.Contains(_connectionPanel))
            {
                _splitContainerTop.Panel1.Controls.Add(_connectionPanel);
            }
        }
        else
        {
            _connectionPanel.Controls.Clear();
        }

        _connectionPanel.Padding = LayoutTokens.GetScaled(LayoutTokens.SectionPanelPadding);
        _connectionPanel.BorderStyle = BorderStyle.FixedSingle;
        _connectionPanel.AutoSize = false;
        _connectionPanel.AutoScroll = true;
        _connectionPanel.MinimumSize = new Size(0, CalculateConnectionFallbackMinHeight());

        // Bold section header with a 1px painted rule beneath it — no border on the panel itself
        var connectionHeader = new Label
        {
            Text = "Connection Status",
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = DpiHeight(30f),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font(Font.FontFamily, 8.25f, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 4),
            AccessibleName = "Connection Status Header",
            AccessibleDescription = "Header for connection status section"
        };
        connectionHeader.Paint += static (s, pe) =>
        {
            var lbl = (Label)s!;
            using var rule = new Pen(SystemColors.ControlDark, 1);
            pe.Graphics.DrawLine(rule, 0, lbl.Height - 2, lbl.Width, lbl.Height - 2);
        };
        _connectionPanel.Controls.Add(connectionHeader);

        var connectionButtonRowHeight = ToolbarButtonHeightPx() + DpiHeight(10f);
        var connectionButtonPanelHeight = (connectionButtonRowHeight * 2) + DpiHeight(10f);

        // TableLayoutPanel for organized content layout
        var tableLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = false, // CRITICAL: Explicit false prevents undersizing
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(0, 5, 0, 0)
        };
        tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, DpiHeight(96f)));
        tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        // Row styles: Absolute for fixed heights (prevents undersizing and ensures alignment)
        tableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, ComfortableLabelHeightPx())); // Status label
        tableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, StandardLabelHeightPx())); // Company label
        tableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, StandardLabelHeightPx())); // Last sync label
        tableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, connectionButtonPanelHeight)); // Button rows stay dense without forcing the panel taller

        Label CreateStatusKeyLabel(string text, string accessibleName)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = Padding.Empty,
                AccessibleName = accessibleName
            };
        }

        // Connection info labels with semantic status coloring (exception to theme rule)
        tableLayout.Controls.Add(CreateStatusKeyLabel("Status", "Connection status label"), 0, 0);
        _connectionStatusLabel = new Label
        {
            Text = "Checking...",
            Dock = DockStyle.Fill,
            AutoSize = false, // CRITICAL: Prevent WinForms RightToLeft recursion bug during TableLayout measurement
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            Margin = Padding.Empty,
            AccessibleName = "Connection Status",
            AccessibleDescription = "Current QuickBooks connection status"
        };
        _sharedTooltip?.SetToolTip(_connectionStatusLabel, "Shows the current connection status to QuickBooks");
        tableLayout.Controls.Add(_connectionStatusLabel, 1, 0);

        tableLayout.Controls.Add(CreateStatusKeyLabel("Company", "Company name label"), 0, 1);
        _companyNameLabel = new Label
        {
            Text = "-",
            Dock = DockStyle.Fill,
            AutoSize = false, // CRITICAL: Prevent WinForms RightToLeft recursion bug during TableLayout measurement
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            Margin = Padding.Empty,
            AccessibleName = "Company Name",
            AccessibleDescription = "Name of the connected QuickBooks company"
        };
        _sharedTooltip?.SetToolTip(_companyNameLabel, "Name of the QuickBooks company currently connected");
        tableLayout.Controls.Add(_companyNameLabel, 1, 1);

        tableLayout.Controls.Add(CreateStatusKeyLabel("Last sync", "Last sync label"), 0, 2);
        _lastSyncLabel = new Label
        {
            Text = "-",
            Dock = DockStyle.Fill,
            AutoSize = false, // CRITICAL: Prevent WinForms RightToLeft recursion bug during TableLayout measurement
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            Margin = Padding.Empty,
            AccessibleName = "Last Sync Time",
            AccessibleDescription = "Timestamp of the last successful sync"
        };
        _sharedTooltip?.SetToolTip(_lastSyncLabel, "When the last sync with QuickBooks occurred");
        tableLayout.Controls.Add(_lastSyncLabel, 1, 2);

        // Buttons stay within two compact rows so the connection section does not demand extra height.
        var buttonPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            ColumnCount = 3,
            RowCount = 2,
            Padding = new Padding(0, 6, 0, 0),
            Margin = Padding.Empty
        };
        buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));
        buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        buttonPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        buttonPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

        _connectButton = Factory.CreateSfButton("Connect", button =>
        {
            button.Dock = DockStyle.Fill;
            button.MinimumSize = LayoutTokens.GetScaled(new Size(88, LayoutTokens.ToolbarButtonHeight));
            button.AccessibleName = "Connect to QuickBooks";
            button.AccessibleDescription = "Establishes connection to QuickBooks Online via OAuth";
            button.TabIndex = 1;
            button.TabStop = true;
        });
        _sharedTooltip?.SetToolTip(_connectButton, "Click to authorize and connect to QuickBooks Online");
        _connectButtonClickHandler = async (s, e) => await ExecuteCommandAsync(ViewModel?.ConnectCommand);
        _connectButton.Click += _connectButtonClickHandler;
        buttonPanel.Controls.Add(_connectButton, 0, 0);

        _manualConnectButton = Factory.CreateSfButton("Manual OAuth", button =>
        {
            button.Dock = DockStyle.Fill;
            button.MinimumSize = LayoutTokens.GetScaled(new Size(92, LayoutTokens.ToolbarButtonHeight));
            button.AccessibleName = "Manual QuickBooks OAuth";
            button.AccessibleDescription = "Fallback QuickBooks OAuth flow that uses the Intuit OAuth Playground redirect and asks you to paste the final redirect URL back into the app";
            button.TabIndex = 2;
            button.TabStop = true;
        });
        _sharedTooltip?.SetToolTip(_manualConnectButton, "Fallback when Intuit will not retain the localhost redirect URI; opens browser auth and asks you to paste the final redirect URL back into Wiley Widget");
        _manualConnectButtonClickHandler = async (s, e) => await InitiateQuickBooksOAuthFlowAsync(useOAuthPlaygroundRedirect: true);
        _manualConnectButton.Click += _manualConnectButtonClickHandler;
        buttonPanel.Controls.Add(_manualConnectButton, 1, 0);

        _disconnectButton = Factory.CreateSfButton("Disconnect", button =>
        {
            button.Dock = DockStyle.Fill;
            button.MinimumSize = LayoutTokens.GetScaled(new Size(88, LayoutTokens.ToolbarButtonHeight));
            button.AccessibleName = "Disconnect from QuickBooks";
            button.AccessibleDescription = "Terminates current QuickBooks Online connection";
            button.TabIndex = 3;
            button.TabStop = true;
        });
        _sharedTooltip?.SetToolTip(_disconnectButton, "Click to disconnect from QuickBooks");
        _disconnectButtonClickHandler = async (s, e) =>
        {
            if (await ShowDisconnectConfirmationAsync())
            {
                await ExecuteCommandAsync(ViewModel?.DisconnectCommand);
            }
        };
        _disconnectButton.Click += _disconnectButtonClickHandler;
        buttonPanel.Controls.Add(_disconnectButton, 2, 0);

        _testConnectionButton = Factory.CreateSfButton("Test Connection", button =>
        {
            button.Dock = DockStyle.Fill;
            button.MinimumSize = LayoutTokens.GetScaled(new Size(92, LayoutTokens.ToolbarButtonHeight));
            button.AccessibleName = "Test QuickBooks Connection";
            button.AccessibleDescription = "Verifies QuickBooks Online connection status";
            button.TabIndex = 4;
            button.TabStop = true;
        });
        _sharedTooltip?.SetToolTip(_testConnectionButton, "Click to test the current QuickBooks connection");
        _testConnectionButtonClickHandler = async (s, e) => await ExecuteCommandAsync(ViewModel?.TestConnectionCommand);
        _testConnectionButton.Click += _testConnectionButtonClickHandler;
        buttonPanel.Controls.Add(_testConnectionButton, 0, 1);

        _diagnosticsButton = Factory.CreateSfButton("Show Diagnostics", button =>
        {
            button.Dock = DockStyle.Fill;
            button.MinimumSize = LayoutTokens.GetScaled(new Size(92, LayoutTokens.ToolbarButtonHeight));
            button.AccessibleName = "QuickBooks Diagnostics";
            button.AccessibleDescription = "Displays sandbox connection diagnostics without exposing secret values";
            button.TabIndex = 5;
            button.TabStop = true;
        });
        _sharedTooltip?.SetToolTip(_diagnosticsButton, "Shows sandbox environment, credential presence, URL ACL, and token status");
        _diagnosticsButtonClickHandler = async (s, e) =>
        {
            if (ViewModel is null) return;
            var report = await ViewModel.RunDiagnosticsAsync();
            MessageBox.Show(report, "QuickBooks Sandbox Diagnostics", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        _diagnosticsButton.Click += _diagnosticsButtonClickHandler;
        buttonPanel.Controls.Add(_diagnosticsButton, 1, 1);
        buttonPanel.SetColumnSpan(_diagnosticsButton, 2);

        tableLayout.Controls.Add(buttonPanel, 0, 3);
        tableLayout.SetColumnSpan(buttonPanel, 2);
        _connectionPanel.Controls.Add(tableLayout);
    }

    /// <summary>
    /// Creates the operations panel with sync buttons and progress bar using TableLayoutPanel.
    /// Uses Absolute row sizing to ensure buttons and progress bar are always visible.
    /// Provides professional spacing and visual consistency with the connection panel.
    /// </summary>
    private void CreateOperationsPanel()
    {
        if (_operationsPanel == null)
        {
            _operationsPanel = new Panel();
            if (_splitContainerTop != null && !_splitContainerTop.Panel2.Controls.Contains(_operationsPanel))
            {
                _splitContainerTop.Panel2.Controls.Add(_operationsPanel);
            }
        }
        else
        {
            // Dispose the designer-created ProgressBarAdv before the field is reassigned below.
            var oldProgressBar = _syncProgressBar;
            _syncProgressBar = null;
            _operationsPanel.Controls.Clear();
            try { oldProgressBar?.Dispose(); } catch { }
        }

        _operationsPanel.Padding = LayoutTokens.GetScaled(LayoutTokens.SectionPanelPadding);
        _operationsPanel.BorderStyle = BorderStyle.FixedSingle;
        _operationsPanel.AutoSize = false;
        _operationsPanel.AutoScroll = true;
        _operationsPanel.MinimumSize = new Size(0, CalculateOperationsFallbackMinHeight());
        _operationsPanel.Margin = new Padding(0, 5, 0, 5);

        // Bold section header with painted rule — visual hierarchy without nested borders
        var operationsHeader = new Label
        {
            Text = "QuickBooks Operations",
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = DpiHeight(30f),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font(Font.FontFamily, 8.25f, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 4),
            AccessibleName = "Operations Header",
            AccessibleDescription = "Header for QuickBooks operations section"
        };
        operationsHeader.Paint += static (s, pe) =>
        {
            var lbl = (Label)s!;
            using var rule = new Pen(SystemColors.ControlDark, 1);
            pe.Graphics.DrawLine(rule, 0, lbl.Height - 2, lbl.Width, lbl.Height - 2);
        };
        _operationsPanel.Controls.Add(operationsHeader);

        // TableLayoutPanel for organized operations layout
        var tableLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = false, // CRITICAL: Explicit false prevents undersizing
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(0, 5, 0, 0)
        };

        // Row styles: Absolute for fixed heights (prevents undersizing)
        tableLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Button row
        tableLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Progress bar row — collapses to zero when hidden

        // Operations buttons in FlowLayoutPanel for responsive layout
        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true, // Allow panel to grow when buttons wrap
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            MinimumSize = new Size(0, CalculateOperationsFallbackMinHeight() - DpiHeight(44f)),
            Padding = new Padding(0, 0, 0, 0),
            Margin = new Padding(0, 10, 0, 0)  // Added top margin for button spacing
        };

        _syncDataButton = Factory.CreateSfButton("Sync Data", button =>
        {
            button.Size = LayoutTokens.GetScaled(new Size(120, LayoutTokens.ToolbarButtonHeight));
            button.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            button.AccessibleName = "Sync Data with QuickBooks";
            button.AccessibleDescription = "Synchronizes financial data between Wiley Widget and QuickBooks Online";
            button.TabIndex = 4;
            button.TabStop = true;
        });
        _sharedTooltip?.SetToolTip(_syncDataButton, "Click to synchronize data with QuickBooks");
        _syncDataButtonClickHandler = async (s, e) =>
        {
            if (await ShowSyncConfirmationAsync("Sync Data"))
            {
                await ExecuteCommandAsync(ViewModel?.SyncDataCommand);
            }
        };
        _syncDataButton.Click += _syncDataButtonClickHandler;
        buttonPanel.Controls.Add(_syncDataButton);

        _importAccountsButton = Factory.CreateSfButton("Import Accounts", button =>
        {
            button.Size = LayoutTokens.GetScaled(new Size(145, LayoutTokens.ToolbarButtonHeight));
            button.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            button.AccessibleName = "Import Chart of Accounts";
            button.AccessibleDescription = "Imports complete chart of accounts from QuickBooks Online";
            button.TabIndex = 5;
            button.TabStop = true;
        });
        _sharedTooltip?.SetToolTip(_importAccountsButton, "Click to import the chart of accounts from QuickBooks");
        _importAccountsButtonClickHandler = async (s, e) => await ExecuteCommandAsync(ViewModel?.ImportAccountsCommand);
        _importAccountsButton.Click += _importAccountsButtonClickHandler;
        buttonPanel.Controls.Add(_importAccountsButton);

        tableLayout.Controls.Add(buttonPanel, 0, 0);

        // Sync progress bar with professional styling
        _syncProgressBar = Factory.CreateProgressBarAdv(progress =>
        {
            progress.Dock = DockStyle.Fill;
            progress.Height = LayoutTokens.GetScaled(LayoutTokens.CompactControlHeight);
            progress.MinimumSize = Size.Empty; // Allow AutoSize row to fully collapse when hidden
            progress.Visible = false;
            progress.ProgressStyle = ProgressBarStyles.WaitingGradient;
        });
        tableLayout.Controls.Add(_syncProgressBar, 0, 1);

        _operationsPanel.Controls.Add(tableLayout);
    }

    /// <summary>
    /// Creates the sync history panel with grid and controls using professional layout.
    /// Sets explicit MinimumSize to prevent panel from collapsing when empty.
    /// Uses FlowLayoutPanel for responsive toolbar and SfDataGrid for data presentation.
    /// </summary>
    private void CreateHistoryPanel()
    {
        if (_historyPanel == null)
        {
            _historyPanel = new Panel();
            if (_splitContainerBottom != null && !_splitContainerBottom.Panel2.Controls.Contains(_historyPanel))
            {
                _splitContainerBottom.Panel2.Controls.Add(_historyPanel);
            }
        }
        else
        {
            // Dispose designer-created grid and filter before the fields are reassigned below.
            var oldGrid = _syncHistoryGrid;
            var oldFilter = _filterTextBox;
            _syncHistoryGrid = null;
            _filterTextBox = null;
            _historyPanel.Controls.Clear();
            try { oldGrid?.SafeDispose(); } catch { }
            try { oldFilter?.Dispose(); } catch { }
        }

        _historyPanel.BorderStyle = BorderStyle.FixedSingle;
        _historyPanel.Padding = LayoutTokens.GetScaled(LayoutTokens.SectionPanelPadding);
        _historyPanel.AutoSize = false;
        _historyPanel.AutoScroll = true;
        _historyPanel.MinimumSize = new Size(0, DpiHeight(240f));

        // Bold section header with painted rule
        var titleLabel = new Label
        {
            Text = "Sync History",
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = DpiHeight(30f),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font(Font.FontFamily, 8.25f, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 4),
            AccessibleName = "Sync History Header",
            AccessibleDescription = "Header for sync history section"
        };
        titleLabel.Paint += static (s, pe) =>
        {
            var lbl = (Label)s!;
            using var rule = new Pen(SystemColors.ControlDark, 1);
            pe.Graphics.DrawLine(rule, 0, lbl.Height - 2, lbl.Width, lbl.Height - 2);
        };
        _historyPanel.Controls.Add(titleLabel);

        // Toolbar with filter and buttons using FlowLayoutPanel
        var toolbarPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(0, ToolbarButtonHeightPx() + DpiHeight(8f)),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(0, 6, 0, 6)
        };

        // Filter label
        var filterLabel = new Label
        {
            Text = "Filter:",
            AutoSize = false, // Explicit false: FlowLayoutPanel manages layout
            Size = LayoutTokens.GetScaled(new Size(52, LayoutTokens.ToolbarButtonHeight)), // Fixed size for toolbar consistency
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 0, 5, 0), // Space to the right
            AccessibleName = "Filter Label",
            AccessibleDescription = "Label for filter input"
        };
        toolbarPanel.Controls.Add(filterLabel);

        // Filter text box — height matches action buttons (30px) for a flush toolbar
        _filterTextBox = Factory.CreateTextBoxExt(textBox =>
        {
            textBox.Size = LayoutTokens.GetScaled(new Size(220, LayoutTokens.ToolbarButtonHeight));
            textBox.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            textBox.AccessibleName = "Filter Sync History";
            textBox.AccessibleDescription = "Enter text to filter sync history records";
            textBox.TabIndex = 6;
            textBox.TabStop = true;
        });
        _sharedTooltip?.SetToolTip(_filterTextBox, "Type to filter the sync history by any field (Timestamp, Operation, Status, etc.)");
        _filterTextBoxTextChangedHandler = (s, e) =>
        {
            if (ViewModel != null)
                ViewModel.FilterText = _filterTextBox.Text;
        };
        _filterTextBox.TextChanged += _filterTextBoxTextChangedHandler;
        toolbarPanel.Controls.Add(_filterTextBox);

        // History toolbar buttons with professional sizing
        _refreshHistoryButton = Factory.CreateSfButton("Refresh", button =>
        {
            button.Size = LayoutTokens.GetScaled(new Size(100, LayoutTokens.ToolbarButtonHeight));
            button.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            button.AccessibleName = "Refresh Sync History";
            button.AccessibleDescription = "Reloads sync history from database";
            button.TabIndex = 7;
            button.TabStop = true;
        });
        _sharedTooltip?.SetToolTip(_refreshHistoryButton, "Click to reload sync history from the database");
        _refreshHistoryButtonClickHandler = async (s, e) => await ExecuteCommandAsync(ViewModel?.RefreshHistoryCommand);
        _refreshHistoryButton.Click += _refreshHistoryButtonClickHandler;
        toolbarPanel.Controls.Add(_refreshHistoryButton);

        _clearHistoryButton = Factory.CreateSfButton("Clear", button =>
        {
            button.Size = LayoutTokens.GetScaled(new Size(82, LayoutTokens.ToolbarButtonHeight));
            button.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            button.AccessibleName = "Clear Sync History";
            button.AccessibleDescription = "Removes all sync history records from the display";
            button.TabIndex = 8;
            button.TabStop = true;
        });
        _sharedTooltip?.SetToolTip(_clearHistoryButton, "Click to clear all sync history (cannot be undone)");
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
        toolbarPanel.Controls.Add(_clearHistoryButton);

        _exportHistoryButton = Factory.CreateSfButton("Export CSV", button =>
        {
            button.Size = LayoutTokens.GetScaled(new Size(120, LayoutTokens.ToolbarButtonHeight));
            button.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            button.AccessibleName = "Export History to CSV";
            button.AccessibleDescription = "Exports sync history data to CSV file";
            button.TabIndex = 9;
            button.TabStop = true;
        });
        _sharedTooltip?.SetToolTip(_exportHistoryButton, "Click to export sync history as a CSV file");
        _exportHistoryButtonClickHandler = async (s, e) =>
        {
            if (ViewModel == null)
            {
                MessageBox.Show("Export is unavailable because QuickBooks history is not initialized.",
                    "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var result = await ExportWorkflowService.ExecuteWithSaveDialogAsync(
                owner: this,
                operationKey: $"{nameof(QuickBooksPanel)}.SyncHistory.Csv",
                dialogTitle: "Export QuickBooks Sync History",
                filter: "CSV Files (*.csv)|*.csv|Excel Files (*.xlsx)|*.xlsx|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                defaultExtension: "csv",
                defaultFileName: $"QuickBooks_SyncHistory_{DateTime.Now:yyyy-MM-dd_HHmmss}.csv",
                exportAction: async (filePath, cancellationToken) =>
                {
                    IsBusy = true;
                    UpdateStatus("Exporting sync history...");

                    try
                    {
                        using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
                        await writer.WriteLineAsync("Timestamp,Operation,Status,Records Processed,Duration,Message");

                        foreach (var record in ViewModel.FilteredSyncHistory)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

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
                    }
                    finally
                    {
                        IsBusy = false;
                    }
                },
                statusCallback: message => UpdateStatus(message),
                logger: Logger,
                cancellationToken: CancellationToken.None);

            if (result.IsSkipped)
            {
                MessageBox.Show(result.ErrorMessage ?? "An export is already in progress.",
                    "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (result.IsCancelled)
            {
                UpdateStatus("Export cancelled.");
                return;
            }

            if (!result.IsSuccess)
            {
                try
                {
                    UpdateStatus("Export failed.");
                    MessageBox.Show(result.ErrorMessage ?? "Export failed.", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    IsBusy = false;
                }

                return;
            }

            UpdateStatus($"Exported {ViewModel.FilteredSyncHistory.Count} records to {Path.GetFileName(result.FilePath)}");
            MessageBox.Show($"Exported {ViewModel.FilteredSyncHistory.Count} records to {Path.GetFileName(result.FilePath)}", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        _exportHistoryButton.Click += _exportHistoryButtonClickHandler;
        toolbarPanel.Controls.Add(_exportHistoryButton);

        _historyPanel.Controls.Add(toolbarPanel);

        // Grid fills remaining space (AutoSizeColumnsMode set in CreateSyncHistoryGrid)
        CreateSyncHistoryGrid();
        _syncHistoryGrid!.Dock = DockStyle.Fill;
        _historyPanel.Controls.Add(_syncHistoryGrid);
    }

    /// <summary>
    /// Updates the status bar with the specified message.
    /// </summary>
    /// <param name="message">The status message to display.</param>
    /// <param name="isError">True if this is an error message.</param>
    private void UpdateStatus(string message, bool isError = false)
    {
        // Marshal status updates to UI thread if required (non-blocking)
        this.InvokeIfRequired(() =>
        {
            try
            {
                if (_statusLabel != null && !_statusLabel.IsDisposed)
                {
                    _statusLabel.Text = message ?? string.Empty;
                    _statusLabel.ForeColor = isError ? Color.Red : Color.Empty;
                    try { _statusLabel.Invalidate(); } catch { }
                }
            }
            catch { }
        });
    }

    /// <summary>
    /// Creates the sync history data grid with proper column configuration.
    /// Implements Syncfusion SfDataGrid best practices: explicit column sizing, sorting, resizing.
    /// Uses BeginUpdate/EndUpdate for performance and sets optimal column width modes.
    /// </summary>
    private void CreateSyncHistoryGrid()
    {
        _syncHistoryGrid = Factory.CreateSfDataGrid(grid =>
        {
            grid.AutoGenerateColumns = false;
            grid.AllowResizingColumns = true;
            grid.AllowSorting = true;
            grid.AllowFiltering = false;
            grid.ShowRowHeader = false;
            grid.SelectionMode = GridSelectionMode.Single;
            grid.NavigationMode = Syncfusion.WinForms.DataGrid.Enums.NavigationMode.Row;
            grid.RowHeight = LayoutTokens.GetScaled(LayoutTokens.GridRowHeightMedium);
            grid.HeaderRowHeight = LayoutTokens.GetScaled(LayoutTokens.GridHeaderRowHeightComfortable);
            grid.AutoSizeColumnsMode = AutoSizeColumnsMode.None;
            grid.EnableDataVirtualization = true;
            grid.AccessibleName = "Sync History Grid";
            grid.AccessibleDescription = "Grid displaying QuickBooks sync history records with sortable columns and status indicators";
            grid.TabIndex = 10;
            grid.TabStop = true;
        });

        _syncHistoryGrid.PreventStringRelationalFilters(
            _logger,
            nameof(QuickBooksSyncHistoryRecord.FormattedTimestamp),
            nameof(QuickBooksSyncHistoryRecord.Operation),
            nameof(QuickBooksSyncHistoryRecord.Status),
            nameof(QuickBooksSyncHistoryRecord.FormattedDuration),
            nameof(QuickBooksSyncHistoryRecord.Message)
        );

        _syncHistoryGrid.BeginUpdate();

        _syncHistoryGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(QuickBooksSyncHistoryRecord.FormattedTimestamp),
            HeaderText = "Time",
            Width = 112,
            MinimumWidth = 96,
            AllowSorting = true,
            AllowResizing = true
        });

        _syncHistoryGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(QuickBooksSyncHistoryRecord.Operation),
            HeaderText = "Operation",
            Width = 96,
            MinimumWidth = 84,
            AllowSorting = true,
            AllowResizing = true
        });

        _syncHistoryGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(QuickBooksSyncHistoryRecord.Status),
            HeaderText = "Status",
            Width = 72,
            MinimumWidth = 60,
            AllowSorting = true,
            AllowResizing = true
        });

        _syncHistoryGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = nameof(QuickBooksSyncHistoryRecord.RecordsProcessed),
            HeaderText = "Rows",
            Width = 64,
            MinimumWidth = 56,
            Format = "N0",
            AllowSorting = true,
            AllowResizing = true
        });

        _syncHistoryGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(QuickBooksSyncHistoryRecord.FormattedDuration),
            HeaderText = "Duration",
            Width = 96,
            MinimumWidth = 88,
            AllowSorting = true,
            AllowResizing = true
        });

        _syncHistoryGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(QuickBooksSyncHistoryRecord.Message),
            HeaderText = "Message",
            Width = 220,
            MinimumWidth = 180,
            AllowSorting = false,
            AllowResizing = true
        });

        _gridSelectionChangedHandler = (s, e) =>
        {
            if (ViewModel != null && _syncHistoryGrid.SelectedItem is QuickBooksSyncHistoryRecord record)
            {
                ViewModel.SelectedSyncRecord = record;
            }
        };
        _syncHistoryGrid.SelectionChanged += _gridSelectionChangedHandler;

        _gridMouseDoubleClickHandler = (s, e) =>
        {
            if (_syncHistoryGrid.SelectedItem is QuickBooksSyncHistoryRecord record)
            {
                HandleSyncRecordDoubleClick(record);
            }
        };
        _syncHistoryGrid.MouseDoubleClick += _gridMouseDoubleClickHandler;

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

        _syncHistoryGrid.EndUpdate();
    }

    #endregion

    #region ViewModel Binding

    /// <summary>
    /// Binds the ViewModel to UI controls.
    /// </summary>
    private void BindViewModel()
    {
        if (ViewModel == null) return;

        // Bind sync history grid with performance optimization
        _syncHistoryGrid!.BeginUpdate();
        try
        {
            _syncHistoryGrid.DataSource = ViewModel.FilteredSyncHistory;
        }
        finally
        {
            _syncHistoryGrid.EndUpdate();
        }

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
                    _companyNameLabel.Text = ViewModel.CompanyName ?? "-";
                // Keep status-bar badge label in sync with resolved company name
                if (_statusConnectionBadge != null && ViewModel.IsConnected && !string.IsNullOrWhiteSpace(ViewModel.CompanyName))
                    _statusConnectionBadge.Text = $"\u25cf {ViewModel.CompanyName}";
                break;

            case nameof(ViewModel.LastSyncTime):
                if (_lastSyncLabel != null)
                    _lastSyncLabel.Text = ViewModel.LastSyncTime ?? "-";
                break;

            case nameof(ViewModel.ConnectionStatusMessage):
                if (_connectionStatusLabel != null)
                    _connectionStatusLabel.Text = ViewModel.ConnectionStatusMessage;
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

        // Update right-side status bar badge
        if (_statusConnectionBadge != null)
        {
            if (isConnected)
            {
                var company = ViewModel.CompanyName;
                _statusConnectionBadge.Text = string.IsNullOrWhiteSpace(company)
                    ? "\u25cf Connected"
                    : $"\u25cf {company}";
                _statusConnectionBadge.ForeColor = Color.Green;    // Semantic: connected = green
            }
            else
            {
                _statusConnectionBadge.Text = "\u25cf Not Connected";
                _statusConnectionBadge.ForeColor = Color.OrangeRed; // Semantic: disconnected = warning
            }
        }

        if (_connectButton != null)
            _connectButton.Enabled = !isConnected && !ViewModel.IsLoading;

        if (_manualConnectButton != null)
            _manualConnectButton.Enabled = !isConnected && !ViewModel.IsLoading;

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
            _totalSyncsLabel.Value = ViewModel.TotalSyncs.ToString("N0", CultureInfo.CurrentCulture);

        if (_successfulSyncsLabel != null)
            _successfulSyncsLabel.Value = ViewModel.SuccessfulSyncs.ToString("N0", CultureInfo.CurrentCulture);

        if (_failedSyncsLabel != null)
            _failedSyncsLabel.Value = ViewModel.FailedSyncs.ToString("N0", CultureInfo.CurrentCulture);

        if (_totalRecordsLabel != null)
            _totalRecordsLabel.Value = ViewModel.TotalRecordsSynced.ToString("N0", CultureInfo.CurrentCulture);

        if (_accountsImportedLabel != null)
            _accountsImportedLabel.Value = ViewModel.AccountsImported.ToString("N0", CultureInfo.CurrentCulture);

        if (_avgDurationLabel != null)
            _avgDurationLabel.Value = $"{ViewModel.AverageSyncDuration:F1}s";
    }

    private void RefreshSyncHistoryDisplay()
    {
        if (_syncHistoryGrid?.View != null)
        {
            _syncHistoryGrid.SafeInvoke(() =>
            {
                // Wrap refresh in BeginUpdate/EndUpdate for performance
                try
                {
                    _syncHistoryGrid.BeginUpdate();
                    _syncHistoryGrid.View.Refresh();
                }
                finally
                {
                    _syncHistoryGrid.EndUpdate();
                }
            });
        }

        // Update NoDataOverlay visibility based on data presence
        if (_noDataOverlay != null && ViewModel != null)
        {
            var hasData = ViewModel.FilteredSyncHistory.Count > 0;
            if (!_noDataOverlay.IsDisposed)
                _noDataOverlay.SafeInvoke(() => _noDataOverlay.Visible = !hasData && !ViewModel.IsLoading);
        }
    }

    private void UpdateNoDataOverlay()
    {
        if (_noDataOverlay == null || ViewModel == null) return;

        var hasData = ViewModel.FilteredSyncHistory.Count > 0;
        if (!_noDataOverlay.IsDisposed)
            _noDataOverlay.SafeInvoke(() => _noDataOverlay.Visible = !hasData && !ViewModel.IsLoading);
    }

    #endregion

    #region Syncfusion Theming

    /// <summary>
    /// Applies Syncfusion Office2019Colorful theme to all controls.
    /// STRICT SfSkinManager compliance: NO manual color assignments.
    /// Theme cascade handles all styling automatically per Syncfusion best practices.
    /// </summary>
    private void ApplySyncfusionTheme()
    {
        try
        {
            var themeName = SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme;
            SfSkinManager.SetVisualStyle(this, themeName);

            // Grid font styling only (colors come from theme)
            if (_syncHistoryGrid != null)
            {
                _syncHistoryGrid.Style.HeaderStyle.Font.Bold = true;
                _syncHistoryGrid.Style.HeaderStyle.Font.Size = 9.5f;
                _syncHistoryGrid.Style.CellStyle.Font.Size = 9f;
            }

            Logger.LogDebug("{Theme} theme applied successfully to QuickBooksPanel", themeName);
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
    /// Initiates the QuickBooks OAuth 2.0 authorization code flow (out-of-band / paste-URL variant).
    /// Follows the Intuit OAuth 2.0 spec: generates authorization URL → opens browser → user
    /// authorizes → user copies redirect URL → app extracts code+realmId+state → exchanges code
    /// for tokens via Basic-Auth POST to the token endpoint.
    ///
    /// Per Intuit docs:
    /// - state must be validated to prevent CSRF (RFC 6749 §10.12)
    /// - realmId from the redirect URL identifies the connected QBO company
    /// - Authorization codes are single-use and expire quickly; exchange immediately
    /// - Token endpoint requires Authorization: Basic base64(clientId:clientSecret)
    /// </summary>
    private async Task InitiateQuickBooksOAuthFlowAsync(bool useOAuthPlaygroundRedirect = false, CancellationToken cancellationToken = default)
    {
        try
        {
            var redirectUriOverride = useOAuthPlaygroundRedirect ? QuickBooksOAuthPlaygroundRedirectUri : null;

            Logger.LogInformation(
                "Starting QuickBooks OAuth 2.0 authorization flow ({Mode})",
                useOAuthPlaygroundRedirect ? "manual-playground" : "manual-configured");

            var serviceProvider = ResolveServiceProvider()
                ?? throw new InvalidOperationException("Service provider not available");

            var authService = serviceProvider.GetService(typeof(IQuickBooksAuthService)) as IQuickBooksAuthService
                ?? throw new InvalidOperationException("IQuickBooksAuthService is not registered in DI");

            // Step 1: Generate authorization URL (state is embedded in the URL)
            var authUrl = await authService.GenerateAuthorizationUrlAsync(redirectUriOverride, cancellationToken);
            var generatedState = ExtractOAuthQueryParam(authUrl, "state");
            Logger.LogInformation("OAuth authorization URL generated (state: {State})", generatedState ?? "(none)");

            // Step 2: Open browser to the authorization page
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = authUrl,
                UseShellExecute = true
            });
            Logger.LogInformation("Browser opened to QuickBooks authorization URL");

            // Step 3: Out-of-band paste dialog — user copies either the redirect URL or the auth code
            string pastedUrlOrCode;
            string pastedRealmId;
            using (var pasteForm = new Form
            {
                Text = "QuickBooks Authorization — Paste Redirect URL Or Code",
                Width = 680,
                Height = 330,
                StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false,
                MaximizeBox = false,
                FormBorderStyle = FormBorderStyle.FixedDialog
            })
            {
                var lbl = new Label
                {
                    Text = "1. Complete the QuickBooks sign-in and consent flow in the browser.\r\n" +
                           "2. When Intuit finishes, copy the final browser result.\r\n" +
                           "3. If the browser shows a full redirect URL, copy that URL.\r\n" +
                           "4. If the browser only shows an Authorization Code and Realm ID, copy those values instead.\r\n" +
                           "5. Paste the URL or code below, optionally paste the Realm ID, then click OK.",
                    Left = 12,
                    Top = 12,
                    Width = 644,
                    Height = 108,
                    AutoSize = false
                };
                var txt = new TextBox
                {
                    Left = 12,
                    Top = 126,
                    Width = 644,
                    Height = 26,
                    PlaceholderText = useOAuthPlaygroundRedirect
                        ? "https://developer.intuit.com/v2/OAuth2Playground/RedirectUrl?code=…&state=…&realmId=…"
                        : "http://localhost:5000/callback/?code=…&state=…&realmId=…"
                };
                var codeLabel = new Label
                {
                    Text = "Authorization Code Or Full Redirect URL",
                    Left = 12,
                    Top = 106,
                    Width = 320,
                    Height = 16,
                    AutoSize = false
                };
                var realmLabel = new Label
                {
                    Text = "Realm ID (optional if included in the URL)",
                    Left = 12,
                    Top = 162,
                    Width = 320,
                    Height = 16,
                    AutoSize = false
                };
                var realmText = new TextBox
                {
                    Left = 12,
                    Top = 182,
                    Width = 644,
                    Height = 26,
                    PlaceholderText = "9341455168020461"
                };
                var btnOk = new Button { Text = "OK", Left = 484, Top = 228, Width = 80, DialogResult = DialogResult.OK };
                var btnCancel = new Button { Text = "Cancel", Left = 576, Top = 228, Width = 80, DialogResult = DialogResult.Cancel };
                pasteForm.Controls.AddRange(new Control[] { lbl, codeLabel, txt, realmLabel, realmText, btnOk, btnCancel });
                pasteForm.AcceptButton = btnOk;
                pasteForm.CancelButton = btnCancel;

                if (pasteForm.ShowDialog() != DialogResult.OK)
                {
                    Logger.LogInformation("User cancelled the OAuth paste dialog — flow aborted");
                    return; // Silent cancel; no failure dialog shown
                }

                pastedUrlOrCode = txt.Text.Trim();
                pastedRealmId = realmText.Text.Trim();
            }

            // Step 4: Parse code, realmId, state from the pasted redirect URL or raw auth-code page
            if (string.IsNullOrWhiteSpace(pastedUrlOrCode))
            {
                Logger.LogWarning("User submitted an empty URL/code value");
                MessageBox.Show(
                    "No URL or authorization code was entered.\n\nAfter authorizing in the browser, paste either the full redirect URL or the Authorization Code shown on the page.",
                    "Missing OAuth Value",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            Logger.LogDebug("Parsing pasted OAuth value (length: {Length})", pastedUrlOrCode.Length);

            var authCode = ExtractOAuthQueryParam(pastedUrlOrCode, "code");
            if (string.IsNullOrWhiteSpace(authCode) && !pastedUrlOrCode.Contains("?", StringComparison.Ordinal) && !pastedUrlOrCode.Contains("://", StringComparison.OrdinalIgnoreCase))
            {
                authCode = pastedUrlOrCode;
            }

            var returnedRealmId = ExtractOAuthQueryParam(pastedUrlOrCode, "realmId");
            if (string.IsNullOrWhiteSpace(returnedRealmId))
            {
                returnedRealmId = pastedRealmId;
            }

            var returnedState = ExtractOAuthQueryParam(pastedUrlOrCode, "state");

            Logger.LogDebug(
                "Parsed redirect params — code: {HasCode}, realmId: {RealmId}, state: {HasState}",
                authCode != null ? $"present ({authCode.Length} chars)" : "MISSING",
                returnedRealmId ?? "(not present)",
                returnedState ?? "(not present)");

            // Step 5: CSRF — validate state matches what we sent (per Intuit and RFC 6749 §10.12)
            if (!string.IsNullOrWhiteSpace(generatedState) && !string.IsNullOrWhiteSpace(returnedState))
            {
                if (!string.Equals(generatedState, returnedState, StringComparison.Ordinal))
                {
                    Logger.LogWarning(
                        "OAuth state mismatch (CSRF check failed). Expected: {Expected}, Got: {Got}",
                        generatedState, returnedState);
                    MessageBox.Show(
                        "Security validation failed: the response state did not match the request state.\n\n" +
                        "This may indicate a tampered response. Please try again.",
                        "Security Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }
                Logger.LogDebug("OAuth state validated successfully");
            }
            else if (!string.IsNullOrWhiteSpace(generatedState) && string.IsNullOrWhiteSpace(returnedState))
            {
                if (useOAuthPlaygroundRedirect)
                {
                    Logger.LogInformation("OAuth playground redirect did not include state in the pasted callback URL");
                }
                else
                {
                    Logger.LogWarning("State was sent in authorization request but not returned in redirect URL");
                }
            }

            // Step 6: Require the authorization code
            if (string.IsNullOrWhiteSpace(authCode))
            {
                var loggedValue = pastedUrlOrCode.Length > 80 ? pastedUrlOrCode[..80] + "…" : pastedUrlOrCode;
                var appearsToBeIntuitErrorPage = pastedUrlOrCode.Contains("/oauth2/error", StringComparison.OrdinalIgnoreCase);

                if (appearsToBeIntuitErrorPage)
                {
                    Logger.LogInformation("User pasted an Intuit OAuth error page instead of an authorization code: {ValuePrefix}", loggedValue);
                }
                else
                {
                    Logger.LogWarning("No authorization code could be parsed from pasted OAuth value: {ValuePrefix}", loggedValue);
                }

                MessageBox.Show(
                    appearsToBeIntuitErrorPage
                        ? "The value you pasted is Intuit's OAuth error page, not a successful callback.\n\n" +
                          "Authorize the app again in the browser, then paste the full redirect URL containing '?code=...' or the Authorization Code field itself."
                        : "Could not find the authorization code in the value you pasted.\n\n" +
                          "Paste either the full redirect URL containing '?code=...' or the Authorization Code value shown on the Intuit page.\n\n" +
                          "Make sure you:\n" +
                          "\u2022 Authorized the app (clicked Connect/Allow) in the browser\n" +
                          "\u2022 Copied the URL AFTER being redirected or copied the Authorization Code field itself\n" +
                          "\u2022 Included the Realm ID if the page showed it separately",
                    "Authorization Code Not Found",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            // Step 7: Exchange authorization code for tokens
            // Per Intuit: code is single-use and expires quickly — exchange immediately
            Logger.LogInformation(
                "Exchanging authorization code for tokens (realmId from redirect: {RealmId})",
                returnedRealmId ?? "not in URL — using configured value");

            var exchangeResult = await authService.ExchangeCodeForTokenAsync(authCode, redirectUriOverride, cancellationToken);

            if (!exchangeResult.IsSuccess)
            {
                Logger.LogWarning("Token exchange failed: {Error}", exchangeResult.ErrorMessage);
                MessageBox.Show(
                    $"Token exchange failed:\n{exchangeResult.ErrorMessage}\n\n" +
                    "Authorization codes expire quickly and are single-use.\n" +
                    "Please try again — authorize in the browser and paste the new URL immediately.",
                    "Authorization Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            Logger.LogInformation("OAuth token exchange successful — tokens stored. RealmId: {RealmId}", returnedRealmId ?? "(from config)");

            // If the redirect URL carried a realmId that differs from the configured value,
            // push it into the token store so downstream services use the correct company.
            if (!string.IsNullOrWhiteSpace(returnedRealmId))
            {
                await authService.SetRealmIdAsync(returnedRealmId, cancellationToken);
            }

            var quickBooksService = serviceProvider.GetService(typeof(IQuickBooksService)) as IQuickBooksService;
            var effectiveRealmId = authService.GetRealmId();
            if (string.IsNullOrWhiteSpace(effectiveRealmId))
            {
                try
                {
                    if (quickBooksService != null)
                    {
                        await quickBooksService.DisconnectAsync(cancellationToken);
                        Logger.LogInformation("Discarded incomplete QuickBooks authorization state after missing realmId");
                    }
                }
                catch (Exception cleanupEx)
                {
                    Logger.LogWarning(cleanupEx, "Failed to discard incomplete QuickBooks authorization state after missing realmId");
                }

                Logger.LogError("OAuth token exchange succeeded but no QuickBooks realmId is available for follow-up API calls");
                MessageBox.Show(
                    "Authorization completed, but Wiley Widget could not determine the QuickBooks company ID (realmId).\n\n" +
                    "The incomplete authorization was discarded. Paste the full redirect URL including 'realmId=...' or enter the realm ID manually and try again.",
                    "QuickBooks Company ID Missing",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(returnedRealmId))
            {
                Logger.LogWarning(
                    "OAuth token exchange succeeded without a realmId in the pasted callback. Reusing configured realmId {RealmId}",
                    effectiveRealmId);
            }

            Logger.LogInformation(
                "QuickBooks post-auth context resolved. CallbackRealmId: {CallbackRealmId}, EffectiveRealmId: {EffectiveRealmId}, Environment: {Environment}",
                returnedRealmId ?? "<missing>",
                effectiveRealmId,
                authService.GetEnvironment());

            if (quickBooksService != null)
            {
                var verified = await quickBooksService.TestConnectionAsync(cancellationToken);
                if (!verified)
                {
                    try
                    {
                        await quickBooksService.DisconnectAsync(cancellationToken);
                        Logger.LogInformation("Discarded unusable QuickBooks authorization state after failed post-OAuth verification");
                    }
                    catch (Exception cleanupEx)
                    {
                        Logger.LogWarning(cleanupEx, "Failed to discard unusable QuickBooks authorization state after verification failure");
                    }

                    Logger.LogWarning(
                        "OAuth token exchange succeeded, but QuickBooks API verification failed for realm {RealmId} in {Environment}",
                        effectiveRealmId,
                        authService.GetEnvironment());

                    MessageBox.Show(
                        "Authorization code exchange succeeded, but QuickBooks rejected the follow-up API verification.\n\n" +
                        "Verify that:\n" +
                        "- the authorized company matches the realm ID\n" +
                        "- the app credentials belong to the same Intuit app\n" +
                        "- sandbox vs production configuration is correct\n\n" +
                        "The unusable authorization was discarded so the app stays in a clean disconnected state.",
                        "QuickBooks Verification Failed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }
            }

            // Step 8: Post-authorization — fetch company info and accounts
            MessageBox.Show(
                "Authorization successful!\n\nFetching company information and accounts...",
                "Connected to QuickBooks",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            var companyService = serviceProvider.GetService(typeof(IQuickBooksCompanyInfoService)) as IQuickBooksCompanyInfoService;
            if (companyService != null)
            {
                try
                {
                    var companyInfo = await companyService.GetCompanyInfoAsync(cancellationToken);
                    if (companyInfo != null)
                        Logger.LogInformation("Fetched company info: {CompanyName}", companyInfo.CompanyName);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to fetch company info after token exchange");
                }
            }

            var accountService = serviceProvider.GetService(typeof(IQuickBooksChartOfAccountsService)) as IQuickBooksChartOfAccountsService;
            if (accountService != null)
            {
                try
                {
                    var accounts = await accountService.FetchAccountsAsync(cancellationToken);
                    Logger.LogInformation("Fetched {AccountCount} accounts from QuickBooks", accounts.Count);

                    if (accounts.Count == 0)
                    {
                        Logger.LogInformation("No accounts found — seeding sandbox with municipal finance accounts");
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

                    if (_syncHistoryGrid != null && ViewModel != null && accounts.Count > 0)
                    {
                        try
                        {
                            var accountRecords = accounts.Select(account => new QuickBooksSyncHistoryRecord
                            {
                                Timestamp = DateTime.UtcNow,
                                Operation = "Account Import",
                                Status = "Success",
                                RecordsProcessed = 1,
                                Message = $"{account.Name} ({WileyWidget.Models.AccountNumber.FormatDisplay(account.AccountNumber) ?? "N/A"}) - {account.Type}/{account.SubType}",
                                Duration = TimeSpan.Zero
                            }).ToList();

                            await _syncHistoryGrid.InvokeAsyncSafe(() =>
                            {
                                ViewModel.ReplaceSyncHistorySnapshot(accountRecords);
                                ViewModel.AccountsImported = accounts.Count;
                                ViewModel.StatusText = $"Loaded {accounts.Count} QuickBooks accounts into sync history.";
                            }, Logger);

                            Logger.LogInformation("Displayed {AccountCount} accounts in sync history grid", accounts.Count);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(ex, "Failed to display accounts in grid");
                            MessageBox.Show(
                                $"Accounts were imported but could not be displayed:\n{ex.Message}",
                                "Display Error",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to fetch/display accounts after token exchange");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unhandled exception in OAuth flow");
            MessageBox.Show(
                $"Failed to complete authorization: {ex.Message}",
                "Authorization Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Extracts a named query-string parameter from a URL or query string fragment.
    /// Returns null if the parameter is not present. Handles URL-encoded values.
    /// </summary>
    /// <param name="url">Full URL or query string (with or without leading '?').</param>
    /// <param name="paramName">Case-insensitive parameter name to find.</param>
    private static string? ExtractOAuthQueryParam(string url, string paramName)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var qs = url.Contains('?') ? url[(url.IndexOf('?') + 1)..] : url;
        foreach (var part in qs.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2 && kv[0].Equals(paramName, StringComparison.OrdinalIgnoreCase))
                return Uri.UnescapeDataString(kv[1]);
        }
        return null;
    }

    #endregion

    #region Connection Prompt Methods

    /// <summary>
    /// Shows a connection prompt dialog when QuickBooks is not connected.
    /// Provides user with option to connect immediately or dismiss the prompt.
    /// </summary>
    private async Task ShowConnectionPromptAsync(CancellationToken cancellationToken = default)
    {
        if (InvokeRequired)
        {
            // Use BeginInvoke + TaskCompletionSource to marshal to the UI thread without blocking it.
            // Calling .Wait() inside Invoke() would deadlock: Invoke blocks until the UI thread
            // finishes, but the async continuation also needs the UI thread to complete.
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            BeginInvoke((MethodInvoker)(() =>
            {
                _ = ShowConnectionPromptAsync(cancellationToken).ContinueWith(
                    promptTask =>
                    {
                        if (promptTask.IsCanceled)
                        {
                            tcs.TrySetCanceled(cancellationToken);
                            return;
                        }

                        if (promptTask.IsFaulted)
                        {
                            var exception = promptTask.Exception?.InnerException ?? promptTask.Exception;
                            tcs.TrySetException(exception ?? new InvalidOperationException("Connection prompt failed."));
                            return;
                        }

                        tcs.TrySetResult(true);
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default);
            }));
            await tcs.Task.ConfigureAwait(false);
            return;
        }

        try
        {
            var result = MessageBox.Show(
                "QuickBooks is not connected.\n\n" +
                "To sync data and access QuickBooks features, you need to authorize this application.\n\n" +
                "Would you like to connect to QuickBooks now?",
                "QuickBooks Connection Required",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                Logger.LogInformation("User chose to connect to QuickBooks from prompt");
                await ExecuteCommandAsync(ViewModel?.ConnectCommand);
            }
            else
            {
                Logger.LogInformation("User dismissed QuickBooks connection prompt");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to show QuickBooks connection prompt");
        }
    }

    #endregion

    #region SplitContainerAdv Configuration Methods

    /// <summary>
    /// Configures all SplitContainerAdv instances with reasonable min sizes and distances.
    /// Uses defensive programming with validation and fallbacks to prevent exceptions.
    /// </summary>
    private void ConfigureSplitContainersSafely()
    {
        if (_splitContainerMain == null || _splitContainerTop == null || _splitContainerBottom == null)
            return;

        try
        {
            // ------------------------------------------------------------------
            // Main horizontal splitter (top section vs bottom section)
            // ------------------------------------------------------------------
            int topSectionPreferred = CalculateTopSectionMinHeight() + DpiHeight(16f);
            int mainTopPreferred = Math.Max(topSectionPreferred, DpiHeight(336f));
            int mainPanel1Min = Math.Max(DpiHeight(220f), CalculateTopSectionMinHeight());
            int mainPanel2Min = DpiHeight(260f);

            // Validate container can hold minimum sizes
            int mainRequiredHeight = mainPanel1Min + mainPanel2Min + _splitContainerMain.SplitterWidth;
            if (_splitContainerMain.Height >= mainRequiredHeight)
            {
                _splitContainerMain.Panel1MinSize = mainPanel1Min;
                _splitContainerMain.Panel2MinSize = mainPanel2Min;

                // Set distance within validated bounds
                int safeDistance = Math.Max(mainPanel1Min,
                    Math.Min(mainTopPreferred, _splitContainerMain.Height - mainPanel2Min - _splitContainerMain.SplitterWidth));
                _splitContainerMain.SplitterDistance = safeDistance;
            }
            else
            {
                // Fallback: use proportional min sizes when container is smaller than ideal
                int fallbackMin1 = Math.Max(50, _splitContainerMain.Height / 3);
                int fallbackMin2 = Math.Max(50, _splitContainerMain.Height / 3);
                _splitContainerMain.Panel1MinSize = fallbackMin1;
                _splitContainerMain.Panel2MinSize = fallbackMin2;
                _splitContainerMain.SplitterDistance = fallbackMin1;
                Logger.LogDebug("QuickBooksPanel: Using fallback main splitter sizes - container height {Height} < required {Required}",
                    _splitContainerMain.Height, mainRequiredHeight);
            }
        }
        catch (ArgumentOutOfRangeException ex)
        {
            Logger.LogWarning(ex, "QuickBooksPanel: Failed to configure main splitter - will retry on next layout");
            _splittersConfigured = false; // Allow retry
            return;
        }

        try
        {
            // ------------------------------------------------------------------
            // Top vertical splitter (connection panel | operations panel)
            // ------------------------------------------------------------------
            int topPanel1Min = DpiHeight(560f);
            int topPanel2Min = DpiHeight(240f);

            int topRequiredWidth = topPanel1Min + topPanel2Min + _splitContainerTop.SplitterWidth;
            if (_splitContainerTop.Width >= topRequiredWidth)
            {
                _splitContainerTop.Panel1MinSize = topPanel1Min;
                _splitContainerTop.Panel2MinSize = topPanel2Min;

                int topDistance = Math.Max(topPanel1Min, (int)(_splitContainerTop.Width * 0.60f));
                topDistance = Math.Max(topPanel1Min,
                    Math.Min(topDistance, _splitContainerTop.Width - topPanel2Min - _splitContainerTop.SplitterWidth));
                _splitContainerTop.SplitterDistance = topDistance;
            }
            else
            {
                // Fallback proportional
                int fallbackMin1 = Math.Max(50, _splitContainerTop.Width / 2);
                int fallbackMin2 = Math.Max(50, _splitContainerTop.Width / 4);
                _splitContainerTop.Panel1MinSize = fallbackMin1;
                _splitContainerTop.Panel2MinSize = fallbackMin2;
                _splitContainerTop.SplitterDistance = fallbackMin1;
                Logger.LogDebug("QuickBooksPanel: Using fallback top splitter sizes - container width {Width} < required {Required}",
                    _splitContainerTop.Width, topRequiredWidth);
            }
        }
        catch (ArgumentOutOfRangeException ex)
        {
            Logger.LogWarning(ex, "QuickBooksPanel: Failed to configure top splitter - will retry on next layout");
            _splittersConfigured = false;
            return;
        }

        try
        {
            // ------------------------------------------------------------------
            // Bottom horizontal splitter (summary panel | history panel)
            // ------------------------------------------------------------------
            int summaryPreferred = CalculateSummaryPanelMinHeight() + DpiHeight(8f);
            int bottomPanel1Min = Math.Max(DpiHeight(140f), summaryPreferred - DpiHeight(16f));
            int bottomPanel2Min = DpiHeight(220f);

            int bottomRequiredHeight = bottomPanel1Min + bottomPanel2Min + _splitContainerBottom.SplitterWidth;
            if (_splitContainerBottom.Height >= bottomRequiredHeight)
            {
                _splitContainerBottom.Panel1MinSize = bottomPanel1Min;
                _splitContainerBottom.Panel2MinSize = bottomPanel2Min;

                int safeDistance = Math.Max(bottomPanel1Min,
                    Math.Min(summaryPreferred, _splitContainerBottom.Height - bottomPanel2Min - _splitContainerBottom.SplitterWidth));
                _splitContainerBottom.SplitterDistance = safeDistance;
            }
            else
            {
                // Fallback proportional
                int fallbackMin1 = Math.Max(50, _splitContainerBottom.Height / 3);
                int fallbackMin2 = Math.Max(50, _splitContainerBottom.Height / 3);
                _splitContainerBottom.Panel1MinSize = fallbackMin1;
                _splitContainerBottom.Panel2MinSize = fallbackMin2;
                _splitContainerBottom.SplitterDistance = fallbackMin1;
                Logger.LogDebug("QuickBooksPanel: Using fallback bottom splitter sizes - container height {Height} < required {Required}",
                    _splitContainerBottom.Height, bottomRequiredHeight);
            }
        }
        catch (ArgumentOutOfRangeException ex)
        {
            Logger.LogWarning(ex, "QuickBooksPanel: Failed to configure bottom splitter - will retry on next layout");
            _splittersConfigured = false;
            return;
        }

        Logger.LogDebug("QuickBooksPanel: All splitters configured successfully");
    }

    /// <summary>
    /// Shared SplitterMoving handler – clamps the distance to valid bounds during user drag.
    /// Prevents user from dragging the splitter beyond min/max constraints.
    /// </summary>
    private void OnSplitterMoving(object? sender, EventArgs e)
    {
        if (sender is Syncfusion.Windows.Forms.Tools.SplitContainerAdv sc)
        {
            try
            {
                // Recalculate bounds based on current container size and min sizes
                int containerDim = GetSplitAxisLength(sc);
                int min1 = sc.Panel1MinSize;
                int min2 = sc.Panel2MinSize;
                int minDistance = min1;
                int maxDistance = Math.Max(min1, containerDim - min2 - sc.SplitterWidth);

                // Validate the splitter state to prevent constraint violations
                if (sc.SplitterDistance < minDistance)
                    sc.SplitterDistance = minDistance;
                else if (sc.SplitterDistance > maxDistance)
                    sc.SplitterDistance = maxDistance;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Error in OnSplitterMoving handler");
            }
        }
    }

    /// <summary>
    /// Enables or disables double-click splitter toggle behavior on a SplitContainerAdv.
    /// Configures Panel1Collapsed property for collapse control.
    /// </summary>
    /// <param name="splitter">The SplitContainerAdv control to configure</param>
    /// <param name="toggleOnDoubleClick">Enable double-click to toggle (default true)</param>
    public void EnableSplitterToggleOnDoubleClick(SplitContainerAdv? splitter, bool toggleOnDoubleClick = true)
    {
        if (splitter == null) return;

        try
        {
            // Use Panel1Collapsed for collapse control
            if (!toggleOnDoubleClick)
            {
                splitter.Panel1Collapsed = false;
            }
            Logger.LogDebug("Splitter toggle: {State}", toggleOnDoubleClick ? "enabled" : "disabled");
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to enable splitter toggle");
        }
    }

    /// <summary>
    /// Manually collapses or expands a panel.
    /// Allows programmatic control of panel visibility.
    /// </summary>
    /// <param name="splitter">The SplitContainerAdv control</param>
    /// <param name="collapsePanel1">True to collapse Panel1, false to expand</param>
    public void SetPanelCollapsedState(SplitContainerAdv? splitter, bool collapsePanel1)
    {
        if (splitter == null) return;

        try
        {
            splitter.Panel1Collapsed = collapsePanel1;
            Logger.LogDebug("Panel1 {State}", collapsePanel1 ? "collapsed" : "expanded");
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to set panel collapsed state");
        }
    }

    /// <summary>
    /// Fixes one panel size while allowing the other to be resizable.
    /// Useful for history grid: fix history size while letting other panels adjust.
    /// Per Syncfusion documentation: FixedPanel prevents resizing one panel.
    /// </summary>
    /// <param name="splitter">The SplitContainerAdv control</param>
    /// <param name="fixedPanel">Which panel to keep fixed (Panel1 or Panel2)</param>
    public void SetFixedPanel(SplitContainerAdv? splitter, Syncfusion.Windows.Forms.Tools.Enums.FixedPanel fixedPanel)
    {
        if (splitter == null) return;

        try
        {
            splitter.FixedPanel = fixedPanel;
            var panelName = fixedPanel == Syncfusion.Windows.Forms.Tools.Enums.FixedPanel.Panel1 ? "Panel1" :
                           fixedPanel == Syncfusion.Windows.Forms.Tools.Enums.FixedPanel.Panel2 ? "Panel2" : "None";
            Logger.LogDebug("Fixed panel set: {Panel} size is locked", panelName);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to set fixed panel");
        }
    }

    /// <summary>
    /// Configures the splitter increment for smooth, granular resizing.
    /// Controls how many pixels the splitter moves per keyboard event.
    /// Default is 1 pixel; higher values allow coarser adjustment.
    /// </summary>
    /// <param name="splitter">The SplitContainerAdv control</param>
    /// <param name="incrementPixels">Number of pixels to move per increment (default 5)</param>
    public void SetSplitterIncrement(SplitContainerAdv? splitter, int incrementPixels = 5)
    {
        if (splitter == null) return;

        try
        {
            splitter.SplitterIncrement = incrementPixels;
            Logger.LogDebug("Splitter increment set to {Pixels} pixels", incrementPixels);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to set splitter increment");
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
            var checkConnectionStopwatch = Stopwatch.StartNew();
            await ViewModel.CheckConnectionCommand.ExecuteAsync(null);
            checkConnectionStopwatch.Stop();
            LogSlowUiOperation("Refresh.CheckConnectionCommand", checkConnectionStopwatch.ElapsedMilliseconds, warningThresholdMs: 1500);

            var refreshHistoryStopwatch = Stopwatch.StartNew();
            await ViewModel.RefreshHistoryCommand.ExecuteAsync(null);
            refreshHistoryStopwatch.Stop();
            LogSlowUiOperation("Refresh.RefreshHistoryCommand", refreshHistoryStopwatch.ElapsedMilliseconds);
        }
    }

    private void LogSlowUiOperation(string operationName, long elapsedMs, int warningThresholdMs = 400)
    {
        if (elapsedMs < warningThresholdMs)
        {
            return;
        }

        Logger.LogWarning(
            "QuickBooksPanel: Slow operation {OperationName} took {ElapsedMs}ms",
            operationName,
            elapsedMs);
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
            if (_manualConnectButton != null && _manualConnectButtonClickHandler != null)
                _manualConnectButton.Click -= _manualConnectButtonClickHandler;
            if (_disconnectButton != null && _disconnectButtonClickHandler != null)
                _disconnectButton.Click -= _disconnectButtonClickHandler;
            if (_testConnectionButton != null && _testConnectionButtonClickHandler != null)
                _testConnectionButton.Click -= _testConnectionButtonClickHandler;
            if (_diagnosticsButton != null && _diagnosticsButtonClickHandler != null)
                _diagnosticsButton.Click -= _diagnosticsButtonClickHandler;
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

            // Unsubscribe splitter events
            if (_splitterMovingHandler != null)
            {
                _splitContainerMain?.SplitterMoving -= _splitterMovingHandler;
                _splitContainerTop?.SplitterMoving -= _splitterMovingHandler;
                _splitContainerBottom?.SplitterMoving -= _splitterMovingHandler;
                _splitterMovingHandler = null;
            }

            // Dispose controls
            try { _syncHistoryGrid?.SafeClearDataSource(); } catch { }
            try { _syncHistoryGrid?.SafeDispose(); } catch { }
            try { _connectButton?.Dispose(); } catch { }
            try { _manualConnectButton?.Dispose(); } catch { }
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
            try { _mainPanel?.Dispose(); } catch { }
            try { _statusStrip?.Dispose(); } catch { }
            try { _sharedTooltip?.Dispose(); } catch { }
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
