using System;
using System.Collections.Generic;
using System.Threading;
using System.ComponentModel;
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
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.WinForms.DataGrid.Events;
using Syncfusion.WinForms.DataGrid.Styles;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Utils;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Controls.Analytics;
using WileyWidget.WinForms.Helpers;

namespace WileyWidget.WinForms.Controls;

/// <summary>
/// QuickBooks integration panel with full MVVM pattern, connection management, and sync history tracking.
/// Inherits from ScopedPanelBase for proper DI lifecycle management.
/// Uses Syncfusion API properly: Dock layout and SfSkinManager theming per Syncfusion documentation.
/// </summary>
[SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
public partial class QuickBooksPanel : ScopedPanelBase
{
    // Strongly-typed ViewModel (this is what you use in your code)
    public new QuickBooksViewModel? ViewModel
    {
        get => (QuickBooksViewModel?)base.ViewModel;
        set => base.ViewModel = value;
    }

    #region UI Controls

    private PanelHeader? _panelHeader;
    private LoadingOverlay? _loadingOverlay;
    private NoDataOverlay? _noDataOverlay;
    private StatusStrip? _statusStrip;
    private ToolStripStatusLabel? _statusLabel;
    private ToolTip? _sharedTooltip;

    // Layout state management
    private bool _inResize = false; // Prevents resize recursion when adjusting panel heights
    private int _layoutNestingDepth = 0; // Hard limit on nesting depth — prevents runaway cascades

    // Main layout containers (organized using SplitContainerAdv for professional layout)
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
    private Syncfusion.Windows.Forms.Tools.Events.SplitterMoveEventHandler? _splitterMovingHandler;

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

    /// <summary>
    /// Calculates the minimum height needed for summary panel based on its content structure.
    /// Returns DPI-aware height: optimized for internal card TableLayout distribution.
    /// </summary>
    private static int CalculateSummaryPanelMinHeight()
    {
        var headerHeight = DpiHeight(32f); // Slightly increased for visibility
        var cardRowHeight = DpiHeight(85f); // Optimized for TableLayout distribution
        var panelPadding = DpiHeight(16f); // Balanced padding (8 top + 8 bottom)
        var baseHeight = headerHeight + (2 * cardRowHeight) + panelPadding;
        return (int)(baseHeight * 1.10f); // 10% buffer is sufficient with SplitContainerAdv
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
        if (_syncHistoryGrid != null)
        {
            _syncHistoryGrid.Focus();
        }
    }

    #endregion

    /// <summary>
    /// Initializes a new instance of the <see cref="QuickBooksPanel"/> class.
    /// </summary>
    public QuickBooksPanel(
        IServiceScopeFactory scopeFactory,
        ILogger<ScopedPanelBase> logger)
        : base(scopeFactory, logger)
    {
        InitializeComponent();
    }

    /// <summary>
    /// Called after the ViewModel has been resolved. Initializes UI and bindings.
    /// Sets initial splitter distances to ensure proper panel layout and prevent button blocking.
    /// </summary>
    protected override void OnViewModelResolved(object? viewModel)
    {
        base.OnViewModelResolved(viewModel);
        if (viewModel is not QuickBooksViewModel)
        {
            return;
        }

        CreateConnectionPanel();
        CreateOperationsPanel();
        CreateSummaryPanel();
        CreateHistoryPanel();

        BindViewModel();
        ApplySyncfusionTheme();

        // === NEW: Safe SplitContainerAdv configuration using SafeSplitterDistanceHelper ===
        ConfigureSplitContainersSafely();

        // Attach a single shared SplitterMoving handler for clamping (prevents user drag exceptions)
        _splitterMovingHandler = OnSplitterMoving;
        _splitContainerMain!.SplitterMoving += _splitterMovingHandler;
        _splitContainerTop!.SplitterMoving += _splitterMovingHandler;
        _splitContainerBottom!.SplitterMoving += _splitterMovingHandler;

        Logger.LogDebug("QuickBooksPanel: ViewModel resolved and UI initialized with safe splitter configuration");
    }

    /// <summary>
    /// Loads the panel and initializes the ViewModel.
    /// </summary>
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        if (ViewModel != null && !DesignMode)
        {
            // Queue async initialization on the UI thread
            BeginInvoke(new Func<Task>(async () =>
            {
                try
                {
                    await ViewModel.InitializeAsync();
                    UpdateLoadingState();
                    UpdateNoDataOverlay();

                    // Force immediate UI refresh and minimum sizing after data is retrieved
                    EnforceMinimumContentHeight();
                    _mainPanel?.PerformLayout();
                    _splitContainerMain?.PerformLayout();
                    _splitContainerTop?.PerformLayout();
                    _splitContainerBottom?.PerformLayout();
                    _syncHistoryGrid?.Refresh();

                    // Defer sizing validation - QuickBooks panel has nested SplitContainers and grids
                    DeferSizeValidation();
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to initialize QuickBooksPanel");
                }
            }));
        }
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

            // DYNAMICALLY CLAMP MIN SIZES: Syncfusion SplitContainerAdv constraint:
            // Panel1MinSize + Panel2MinSize + SplitterWidth must not exceed container dimension
            // When container is very narrow (< 300px), reduce min sizes to prevent InvalidOperationException
            ClampMinSizesIfNeeded(_splitContainerMain);
            ClampMinSizesIfNeeded(_splitContainerTop);
            ClampMinSizesIfNeeded(_splitContainerBottom);

            // Adjust min sizes responsively based on current width (wide/medium/narrow)
            AdjustMinSizesForCurrentWidth();

            // Refresh all split containers to ensure they respect minimum sizes
            if (_splitContainerMain != null)
            {
                _splitContainerMain.Height = Height - (_panelHeader?.Height ?? 0) - (_statusStrip?.Height ?? 0) - Padding.Vertical;

                // Safely adjust splitter distance within bounds
                if (!SafeSplitterDistanceHelper.TrySetSplitterDistance(_splitContainerMain, _splitContainerMain.SplitterDistance))
                {
                    // If TrySet fails, calculate a safe fallback distance
                    int max = _splitContainerMain.Height - _splitContainerMain.Panel2MinSize - _splitContainerMain.SplitterWidth;
                    if (max > _splitContainerMain.Panel1MinSize)
                    {
                        _splitContainerMain.SplitterDistance = max;
                    }
                }
                _splitContainerMain.PerformLayout();
            }

            if (_splitContainerTop != null)
            {
                // Safely adjust splitter distance within bounds
                if (!SafeSplitterDistanceHelper.TrySetSplitterDistance(_splitContainerTop, _splitContainerTop.SplitterDistance))
                {
                    // If TrySet fails, calculate a safe fallback distance
                    int max = _splitContainerTop.Width - _splitContainerTop.Panel2MinSize - _splitContainerTop.SplitterWidth;
                    if (max > _splitContainerTop.Panel1MinSize)
                    {
                        _splitContainerTop.SplitterDistance = max;
                    }
                }
                _splitContainerTop.PerformLayout();
            }

            if (_splitContainerBottom != null)
            {
                // Safely adjust splitter distance within bounds
                var summaryMin = CalculateSummaryPanelMinHeight();
                if (!SafeSplitterDistanceHelper.TrySetSplitterDistance(_splitContainerBottom, _splitContainerBottom.SplitterDistance))
                {
                    // If TrySet fails, calculate a safe fallback distance
                    int max = _splitContainerBottom.Height - _splitContainerBottom.Panel2MinSize - _splitContainerBottom.SplitterWidth;
                    if (max > summaryMin)
                    {
                        _splitContainerBottom.SplitterDistance = max;
                    }
                    else if (_splitContainerBottom.SplitterDistance < summaryMin)
                    {
                        _splitContainerBottom.SplitterDistance = summaryMin;
                    }
                }
                _splitContainerBottom.PerformLayout();
            }

            // Refresh SfDataGrid on window resize for responsive column layout
            if (_syncHistoryGrid != null && IsHandleCreated)
            {
                try
                {
                    // Suspend grid layout to prevent cascade during column sizing
                    _syncHistoryGrid.SuspendLayout();

                    // SfDataGrid specific - helps columns adapt
                    _syncHistoryGrid.AutoSizeColumnsMode = Syncfusion.WinForms.DataGrid.Enums.AutoSizeColumnsMode.AllCellsWithLastColumnFill;

                    // Refresh grid layout and columns for responsive sizing
                    _syncHistoryGrid.PerformLayout();

                    // Resume grid layout — layout committed once
                    _syncHistoryGrid.ResumeLayout(false);

                    // Mark grid as dirty to trigger final column recalculation
                    _syncHistoryGrid.Invalidate(true);
                }
                catch (Exception gridEx)
                {
                    Logger.LogWarning(gridEx, "Failed to refresh SfDataGrid layout on resize");
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
                    Logger.LogWarning(panelEx, "Failed to perform layout on main panel");
                }
            }


            // Enforce minimum dimensions based on content
            EnforceMinimumContentHeight();

            // Add safety clamp for all splitters
            if (_splitContainerMain != null) ClampSplitterSafely(_splitContainerMain!);
            if (_splitContainerTop != null) ClampSplitterSafely(_splitContainerTop!);
            if (_splitContainerBottom != null) ClampSplitterSafely(_splitContainerBottom!);

            // Log resize in Debug mode only to reduce log spam
            Logger.LogDebug("QuickBooksPanel resized to {Width}x{Height}, nesting depth: {Depth}", Width, Height, _layoutNestingDepth);
        }
        finally
        {
            // Always resume layout and reset flags in correct order
            ResumeLayout(true);
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
        if (_mainPanel == null || !IsHandleCreated) return;

        // Calculate absolute minimum needed for visual integrity (Summary + History Buffer)
        // Includes Connection/Operations height as well if they were visible
        int summaryHeight = CalculateSummaryPanelMinHeight();
        int historyHeight = DpiHeight(320f); // lowered
        int headerHeight = _panelHeader?.Height ?? DpiHeight(50f);
        int footerHeight = _statusStrip?.Height ?? DpiHeight(25f);
        int connectionHeight = DpiHeight(160f);

        int minNeeded = headerHeight + connectionHeight + summaryHeight + historyHeight + footerHeight + Padding.Vertical * 4;

        if (MinimumSize.Height < minNeeded)
        {
            MinimumSize = new Size(MinimumSize.Width, minNeeded);
        }

        // No Height = ... line here!

        // SfDataGrid row height safety
        if (_syncHistoryGrid != null)
        {
            _syncHistoryGrid.RowHeight = DpiHeight(28f);
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
        int availableDimension = splitter.Orientation == Orientation.Horizontal
            ? splitter.Height
            : splitter.Width;

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

        int available = splitter.Orientation == Orientation.Horizontal
            ? splitter.Height - splitter.SplitterWidth
            : splitter.Width - splitter.SplitterWidth;

        int min1 = splitter.Panel1MinSize;
        int min2 = splitter.Panel2MinSize;

        if (available < min1 + min2)
        {
            // Emergency fallback — reduce min sizes temporarily
            splitter.Panel1MinSize = Math.Max(80, min1 / 2);
            splitter.Panel2MinSize = Math.Max(80, min2 / 2);
            available = splitter.Orientation == Orientation.Horizontal
                ? splitter.Height - splitter.SplitterWidth
                : splitter.Width - splitter.SplitterWidth;
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
        const int standardMinSize = 100;         // Base: main splitter top/bottom panels
        const int connectionMinSize = 110;       // Connection panel: shows status + 3 buttons
        const int operationMinSize = 110;        // Operations panel: shows sync buttons + progress
        const int summaryMinSize = 100;          // Summary panel: shows KPI cards (min height enforced separately)
        const int historyMinSize = 100;          // History panel: shows grid (min height enforced separately)

        // Calculate adjusted sizes based on width
        int topMin1, topMin2, bottomMin1, bottomMin2, mainMin1, mainMin2;

        if (currentWidth >= wideThreshold)
        {
            // WIDE: Use standard sizes
            topMin1 = connectionMinSize;
            topMin2 = operationMinSize;
            bottomMin1 = summaryMinSize;
            bottomMin2 = historyMinSize;
            mainMin1 = standardMinSize;
            mainMin2 = standardMinSize;
        }
        else if (currentWidth >= mediumThreshold)
        {
            // MEDIUM: 75% of standard
            topMin1 = Math.Max(50, (int)(connectionMinSize * 0.75f));
            topMin2 = Math.Max(50, (int)(operationMinSize * 0.75f));
            bottomMin1 = Math.Max(50, (int)(summaryMinSize * 0.75f));
            bottomMin2 = Math.Max(50, (int)(historyMinSize * 0.75f));
            mainMin1 = Math.Max(50, (int)(standardMinSize * 0.75f));
            mainMin2 = Math.Max(50, (int)(standardMinSize * 0.75f));
        }
        else
        {
            // NARROW: 50% of standard, minimum 50px
            topMin1 = Math.Max(50, (int)(connectionMinSize * 0.5f));
            topMin2 = Math.Max(50, (int)(operationMinSize * 0.5f));
            bottomMin1 = Math.Max(50, (int)(summaryMinSize * 0.5f));
            bottomMin2 = Math.Max(50, (int)(historyMinSize * 0.5f));
            mainMin1 = Math.Max(50, (int)(standardMinSize * 0.5f));
            mainMin2 = Math.Max(50, (int)(standardMinSize * 0.5f));
        }

        // Apply adjusted min sizes with constraint checking
        try
        {
            // Helper: Safely set both Panel1MinSize and Panel2MinSize while respecting constraints
            ApplySplitterMinSizesWithConstraintCheck(_splitContainerTop, topMin1, topMin2, "Top");
            ApplySplitterMinSizesWithConstraintCheck(_splitContainerBottom, bottomMin1, bottomMin2, "Bottom");
            ApplySplitterMinSizesWithConstraintCheck(_splitContainerMain, mainMin1, mainMin2, "Main");

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
        if (splitter == null || !splitter.IsHandleCreated)
            return;

        int containerDim = splitter.Orientation == Orientation.Horizontal ? splitter.Height : splitter.Width;
        int totalRequired = requestedMin1 + requestedMin2 + splitter.SplitterWidth;

        // If constraint violated, scale both down proportionally
        int actualMin1 = requestedMin1;
        int actualMin2 = requestedMin2;

        if (totalRequired > containerDim)
        {
            // Calculate reduction ratio
            float scale = (float)containerDim / totalRequired;
            actualMin1 = Math.Max(30, (int)(requestedMin1 * scale * 0.9f)); // 0.9f adds safety margin
            actualMin2 = Math.Max(30, (int)(requestedMin2 * scale * 0.9f));

            Logger.LogDebug(
                "ApplySplitterMinSizesWithConstraintCheck: {Name} splitter constraint violated. " +
                "Requested=[{Req1}, {Req2}], Container={Container}, Adjusted=[{Act1}, {Act2}]",
                splitterName, requestedMin1, requestedMin2, containerDim, actualMin1, actualMin2);
        }

        // Set Panel2MinSize first so Panel1MinSize has valid context
        if (splitter.Panel2MinSize != actualMin2)
            splitter.Panel2MinSize = actualMin2;
        if (splitter.Panel1MinSize != actualMin1)
            splitter.Panel1MinSize = actualMin1;
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
        Size = new Size(1400, 900);
        MinimumSize = new Size((int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(640f), (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(480f));
        Padding = new Padding(8);
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
        Controls.Add(_panelHeader);

        // Main scrollable panel
        _mainPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            BorderStyle = BorderStyle.None,
            AutoScroll = true,
            AutoScrollMinSize = new Size(1000, 800),
            AutoSize = false
        };

        // Create the four main content panels
        CreateConnectionPanel();
        CreateOperationsPanel();
        CreateSummaryPanel();
        CreateHistoryPanel();

        // Hierarchical SplitContainerAdv Layout:
        // Main (Horizontal Splitter) -> Top (Vertical Splitter: Conn vs Ops) + Bottom (Horizontal Splitter: Summary vs History)

        _splitContainerTop = new SplitContainerAdv
        {
            Name = "SplitContainerTop",
            Dock = DockStyle.Fill,
            Orientation = System.Windows.Forms.Orientation.Vertical,
            IsSplitterFixed = false,
            SplitterWidth = 6,
            BorderStyle = BorderStyle.FixedSingle
        };
        _splitContainerTop.Panel1.Controls.Add(_connectionPanel!);
        _splitContainerTop.Panel2.Controls.Add(_operationsPanel!);

        _splitContainerBottom = new SplitContainerAdv
        {
            Name = "SplitContainerBottom",
            Dock = DockStyle.Fill,
            Orientation = System.Windows.Forms.Orientation.Horizontal, // Changed to Horizontal: Summary Top, History Bottom
            IsSplitterFixed = false,
            SplitterWidth = 6,
            BorderStyle = BorderStyle.FixedSingle
        };
        _splitContainerBottom.Panel1.Controls.Add(_summaryPanel!);
        _splitContainerBottom.Panel2.Controls.Add(_historyPanel!);

        _splitContainerMain = new SplitContainerAdv
        {
            Name = "SplitContainerMain",
            Dock = DockStyle.Fill,
            Orientation = System.Windows.Forms.Orientation.Horizontal,
            IsSplitterFixed = false,
            SplitterWidth = 6,
            BorderStyle = BorderStyle.None
        };
        _splitContainerMain.Panel1.Controls.Add(_splitContainerTop);
        _splitContainerMain.Panel2.Controls.Add(_splitContainerBottom);

        _mainPanel.Controls.Add(_splitContainerMain);
        Controls.Add(_mainPanel);

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

        // Use SafeSplitterDistanceHelper to defer SplitterDistance setting until controls are properly sized
        // Initialize inner splitters first (bottom-up for nesting)
        // MODEST MIN SIZES: Professional appearance with responsive scaling via AdjustMinSizesForCurrentWidth()
        // Syncfusion constraint: Panel1MinSize + Panel2MinSize + SplitterWidth ≤ container dimension
        // These are modest starting sizes; responsive scaling kicks in during OnResize for narrow containers
        SafeSplitterDistanceHelper.ConfigureSafeSplitContainer(
            _splitContainerTop, panel1MinSize: 110, panel2MinSize: 110, desiredDistance: DpiHeight(400f));

        SafeSplitterDistanceHelper.ConfigureSafeSplitContainer(
            _splitContainerBottom, panel1MinSize: 100, panel2MinSize: 100, desiredDistance: CalculateSummaryPanelMinHeight());

        // Outer main splitter with modest min sizes for top/bottom balance
        SafeSplitterDistanceHelper.ConfigureSafeSplitContainer(
            _splitContainerMain, panel1MinSize: 100, panel2MinSize: 100, desiredDistance: DpiHeight(350f));

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

        // Status strip for feedback
        _statusStrip = new StatusStrip
        {
            Dock = DockStyle.Bottom,
            Height = DpiHeight(25f),
            Name = "StatusStrip",
            AccessibleName = "Status Bar",
            AccessibleDescription = "Displays current status and feedback"
        };
        _statusLabel = new ToolStripStatusLabel
        {
            Text = "Ready",
            Name = "StatusLabel"
        };
        _statusStrip.Items.Add(_statusLabel);
        Controls.Add(_statusStrip);

        // Finalize layout
        EnforceMinimumContentHeight();

        ResumeLayout(false);

        // Add diagnostics logging for splitter validation
        var mainDiag = SafeSplitterDistanceHelper.GetDiagnostics(_splitContainerMain, "MainSplitter");
        Logger.LogInformation(mainDiag);

        var topDiag = SafeSplitterDistanceHelper.GetDiagnostics(_splitContainerTop, "TopSplitter");
        Logger.LogInformation(topDiag);

        var bottomDiag = SafeSplitterDistanceHelper.GetDiagnostics(_splitContainerBottom, "BottomSplitter");
        Logger.LogInformation(bottomDiag);

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

        _summaryPanel.Padding = new Padding(12, 8, 12, 8);
        _summaryPanel.BorderStyle = BorderStyle.FixedSingle;
        _summaryPanel.AutoSize = false; // Explicit false: allows parent to set height

        // Summary header with improved typography
        var summaryHeader = new Label
        {
            Text = "QuickBooks Summary",
            Dock = DockStyle.Top,
            AutoSize = false, // CRITICAL: Explicit false + Height prevents measurement loops
            Height = DpiHeight(28f), // Explicit height for header
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            AccessibleName = "Summary Header",
            AccessibleDescription = "Header for QuickBooks summary metrics"
        };
        _summaryPanel.Controls.Add(summaryHeader);

        // TableLayoutPanel for KPI cards with responsive layout
        var tableLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = false, // Explicit false: height controlled by summary panel container
            ColumnCount = 3,
            RowCount = 2,
            Padding = new Padding(0, 5, 0, 0)
        };

        // Column styles: Percent for equal distribution (responsive)
        tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));

        // Row styles: Percent for flexible distribution (proportional to total available height)
        tableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
        tableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));

        // KPI cards: Receives proportional height from TableLayoutPanel
        var cardHeight = DpiHeight(80f);

        _totalSyncsLabel = new KpiCardControl
        {
            Title = "Total Syncs",
            Value = "0",
            Dock = DockStyle.Fill,
            MinimumSize = new Size(DpiHeight(100f), cardHeight),
            Margin = new Padding(DpiHeight(6f))
        };
        _sharedTooltip?.SetToolTip(_totalSyncsLabel, "Total number of synchronizations performed (all time)");
        tableLayout.Controls.Add(_totalSyncsLabel, 0, 0);

        _successfulSyncsLabel = new KpiCardControl
        {
            Title = "Successful",
            Value = "0",
            Dock = DockStyle.Fill,
            MinimumSize = new Size(DpiHeight(100f), cardHeight),
            Margin = new Padding(DpiHeight(6f))
        };
        _sharedTooltip?.SetToolTip(_successfulSyncsLabel, "Number of successful sync operations");
        tableLayout.Controls.Add(_successfulSyncsLabel, 1, 0);

        _failedSyncsLabel = new KpiCardControl
        {
            Title = "Failed",
            Value = "0",
            Dock = DockStyle.Fill,
            MinimumSize = new Size(DpiHeight(100f), cardHeight),
            Margin = new Padding(DpiHeight(6f))
        };
        _sharedTooltip?.SetToolTip(_failedSyncsLabel, "Number of failed sync operations (needs attention)");
        tableLayout.Controls.Add(_failedSyncsLabel, 2, 0);

        _totalRecordsLabel = new KpiCardControl
        {
            Title = "Records Synced",
            Value = "0",
            Dock = DockStyle.Fill,
            MinimumSize = new Size(DpiHeight(100f), cardHeight),
            Margin = new Padding(DpiHeight(6f))
        };
        _sharedTooltip?.SetToolTip(_totalRecordsLabel, "Total records processed during syncs");
        tableLayout.Controls.Add(_totalRecordsLabel, 0, 1);

        _accountsImportedLabel = new KpiCardControl
        {
            Title = "Accounts Imported",
            Value = "0",
            Dock = DockStyle.Fill,
            MinimumSize = new Size(DpiHeight(100f), cardHeight),
            Margin = new Padding(DpiHeight(6f))
        };
        _sharedTooltip?.SetToolTip(_accountsImportedLabel, "Number of accounts imported from QuickBooks");
        tableLayout.Controls.Add(_accountsImportedLabel, 1, 1);

        _avgDurationLabel = new KpiCardControl
        {
            Title = "Avg Duration",
            Value = "0s",
            Dock = DockStyle.Fill,
            MinimumSize = new Size(DpiHeight(100f), cardHeight),
            Margin = new Padding(DpiHeight(6f))
        };
        _sharedTooltip?.SetToolTip(_avgDurationLabel, "Average duration of sync operations (seconds)");
        tableLayout.Controls.Add(_avgDurationLabel, 2, 1);

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
            Margin = new Padding(4),
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(DpiHeight(8f)),
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
            Font = new Font("Segoe UI", 8f, FontStyle.Regular),
            Visible = false // Only show if we start using it
        };
        cardLayout.Controls.Add(topSmallLabel, 0, 0);

        // Title: centered portion of card
        var titleLabel = new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
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
            Font = new Font("Segoe UI", 16f, FontStyle.Bold),
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
            Padding = new Padding(10)
        };
        SfSkinManager.SetVisualStyle(topPanel, SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);

        // Vertical split: left = connection, right = operations (safe deferred sizing)
        var splitTop = new SplitContainerAdv
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            FixedPanel = Syncfusion.Windows.Forms.Tools.Enums.FixedPanel.None
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

        _connectionPanel.Padding = new Padding(12, 8, 12, 8);
        _connectionPanel.BorderStyle = BorderStyle.FixedSingle;
        _connectionPanel.AutoSize = false; // Explicit false: parent sets height

        // Connection header with improved typography and spacing
        var connectionHeader = new Label
        {
            Text = "Connection Status",
            Dock = DockStyle.Top,
            AutoSize = false, // CRITICAL: Explicit false + Height prevents measurement loops
            Height = DpiHeight(28f), // Explicit height for header
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 0, 0, 5), // Space below header
            AccessibleName = "Connection Status Header",
            AccessibleDescription = "Header for connection status section"
        };
        _connectionPanel.Controls.Add(connectionHeader);

        // TableLayoutPanel for organized content layout
        var tableLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = false, // CRITICAL: Explicit false prevents undersizing
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(0, 5, 0, 0)
        };

        // Row styles: Absolute for fixed heights (prevents undersizing and ensures alignment)
        tableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiHeight(24f))); // Status label
        tableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiHeight(20f))); // Company label
        tableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiHeight(20f))); // Last sync label
        tableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiHeight(36f))); // Button row

        // Connection info labels with semantic status coloring (exception to theme rule)
        _connectionStatusLabel = new Label
        {
            Text = "Status: Checking...",
            Dock = DockStyle.Fill,
            AutoSize = false, // CRITICAL: Prevent WinForms RightToLeft recursion bug during TableLayout measurement
            Font = new Font("Segoe UI", 10f, FontStyle.Bold), // Bolder for status importance
            TextAlign = ContentAlignment.MiddleLeft,
            AccessibleName = "Connection Status",
            AccessibleDescription = "Current QuickBooks connection status"
        };
        _sharedTooltip?.SetToolTip(_connectionStatusLabel, "Shows the current connection status to QuickBooks");
        tableLayout.Controls.Add(_connectionStatusLabel, 0, 0);

        _companyNameLabel = new Label
        {
            Text = "Company: -",
            Dock = DockStyle.Fill,
            AutoSize = false, // CRITICAL: Prevent WinForms RightToLeft recursion bug during TableLayout measurement
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleLeft,
            AccessibleName = "Company Name",
            AccessibleDescription = "Name of the connected QuickBooks company"
        };
        _sharedTooltip?.SetToolTip(_companyNameLabel, "Name of the QuickBooks company currently connected");
        tableLayout.Controls.Add(_companyNameLabel, 0, 1);

        _lastSyncLabel = new Label
        {
            Text = "Last Sync: -",
            Dock = DockStyle.Fill,
            AutoSize = false, // CRITICAL: Prevent WinForms RightToLeft recursion bug during TableLayout measurement
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleLeft,
            AccessibleName = "Last Sync Time",
            AccessibleDescription = "Timestamp of the last successful sync"
        };
        _sharedTooltip?.SetToolTip(_lastSyncLabel, "When the last sync with QuickBooks occurred");
        tableLayout.Controls.Add(_lastSyncLabel, 0, 2);

        // Buttons in a FlowLayoutPanel for responsive button layout
        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = false, // Explicit false: TableLayoutPanel row height controls button row
            Height = DpiHeight(36f),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 2, 0, 0)
        };

        _connectButton = new SfButton
        {
            Text = "Connect",
            Size = new Size(DpiHeight(85f), DpiHeight(36f)),
            Anchor = AnchorStyles.Left | AnchorStyles.Top,
            AccessibleName = "Connect to QuickBooks",
            AccessibleDescription = "Establishes connection to QuickBooks Online via OAuth",
            TabIndex = 1,
            TabStop = true
        };
        _sharedTooltip?.SetToolTip(_connectButton, "Click to authorize and connect to QuickBooks Online");
        _connectButtonClickHandler = async (s, e) => await InitiateQuickBooksOAuthFlowAsync();
        _connectButton.Click += _connectButtonClickHandler;
        buttonPanel.Controls.Add(_connectButton);

        _disconnectButton = new SfButton
        {
            Text = "Disconnect",
            Size = new Size(DpiHeight(100f), DpiHeight(36f)),
            Anchor = AnchorStyles.Left | AnchorStyles.Top,
            AccessibleName = "Disconnect from QuickBooks",
            AccessibleDescription = "Terminates current QuickBooks Online connection",
            TabIndex = 2,
            TabStop = true
        };
        _sharedTooltip?.SetToolTip(_disconnectButton, "Click to disconnect from QuickBooks");
        _disconnectButtonClickHandler = async (s, e) =>
        {
            if (await ShowDisconnectConfirmationAsync())
            {
                await ExecuteCommandAsync(ViewModel?.DisconnectCommand);
            }
        };
        _disconnectButton.Click += _disconnectButtonClickHandler;
        buttonPanel.Controls.Add(_disconnectButton);

        _testConnectionButton = new SfButton
        {
            Text = "Test Connection",
            Size = new Size(DpiHeight(120f), DpiHeight(36f)),
            Anchor = AnchorStyles.Left | AnchorStyles.Top,
            AccessibleName = "Test QuickBooks Connection",
            AccessibleDescription = "Verifies QuickBooks Online connection status",
            TabIndex = 3,
            TabStop = true
        };
        _sharedTooltip?.SetToolTip(_testConnectionButton, "Click to test the current QuickBooks connection");
        _testConnectionButtonClickHandler = async (s, e) => await ExecuteCommandAsync(ViewModel?.TestConnectionCommand);
        _testConnectionButton.Click += _testConnectionButtonClickHandler;
        buttonPanel.Controls.Add(_testConnectionButton);

        tableLayout.Controls.Add(buttonPanel, 0, 3);
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
            _operationsPanel.Controls.Clear();
        }

        _operationsPanel.Padding = new Padding(12, 8, 12, 8);
        _operationsPanel.BorderStyle = BorderStyle.FixedSingle;
        _operationsPanel.AutoSize = false; // Explicit false: parent sets height
        _operationsPanel.Margin = new Padding(0, 5, 0, 5);  // Added margin to prevent top clipping

        // Operations header with improved typography
        var operationsHeader = new Label
        {
            Text = "QuickBooks Operations",
            Dock = DockStyle.Top,
            AutoSize = false, // CRITICAL: Explicit false + Height prevents measurement loops
            Height = DpiHeight(28f), // Explicit height for header
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 0, 0, 5), // Space below header
            AccessibleName = "Operations Header",
            AccessibleDescription = "Header for QuickBooks operations section"
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
        tableLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize, 0)); // Button row - auto-size to fit wrapped buttons
        tableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiHeight(25f))); // Progress bar

        // Operations buttons in FlowLayoutPanel for responsive layout
        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true, // Allow panel to grow when buttons wrap
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(0, 0, 0, 0),
            Margin = new Padding(0, 10, 0, 0)  // Added top margin for button spacing
        };

        _syncDataButton = new SfButton
        {
            Text = "🔄 Sync Data",
            Size = new Size(DpiHeight(120f), DpiHeight(36f)),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Anchor = AnchorStyles.Left | AnchorStyles.Top,
            AccessibleName = "Sync Data with QuickBooks",
            AccessibleDescription = "Synchronizes financial data between Wiley Widget and QuickBooks Online",
            TabIndex = 4,
            TabStop = true
        };
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

        _importAccountsButton = new SfButton
        {
            Text = "📥 Import Chart of Accounts",
            Size = new Size(DpiHeight(170f), DpiHeight(36f)),
            Anchor = AnchorStyles.Left | AnchorStyles.Top,
            AccessibleName = "Import Chart of Accounts",
            AccessibleDescription = "Imports complete chart of accounts from QuickBooks Online",
            TabIndex = 5,
            TabStop = true
        };
        _sharedTooltip?.SetToolTip(_importAccountsButton, "Click to import the chart of accounts from QuickBooks");
        _importAccountsButtonClickHandler = async (s, e) => await ExecuteCommandAsync(ViewModel?.ImportAccountsCommand);
        _importAccountsButton.Click += _importAccountsButtonClickHandler;
        buttonPanel.Controls.Add(_importAccountsButton);

        tableLayout.Controls.Add(buttonPanel, 0, 0);

        // Sync progress bar with professional styling
        _syncProgressBar = new ProgressBarAdv
        {
            Dock = DockStyle.Fill,
            Height = DpiHeight(25f),
            MinimumSize = new Size(0, DpiHeight(25f)),
            Visible = false,
            ProgressStyle = ProgressBarStyles.WaitingGradient
        };
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
            _historyPanel.Controls.Clear();
        }

        _historyPanel.BorderStyle = BorderStyle.FixedSingle;
        _historyPanel.Padding = new Padding(12, 8, 12, 8);
        _historyPanel.AutoSize = false; // Explicit false: Dock.Fill with MinimumSize prevents collapse
        _historyPanel.MinimumSize = new Size(0, DpiHeight(350f)); // Minimum height for grid visibility

        // History header with professional typography
        var titleLabel = new Label
        {
            Text = "Sync History",
            Dock = DockStyle.Top,
            AutoSize = false, // CRITICAL: Explicit false + Height prevents measurement loops
            Height = DpiHeight(28f), // Match other section headers for visual consistency
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 0, 0, 5), // Space below header
            AccessibleName = "Sync History Header",
            AccessibleDescription = "Header for sync history section"
        };
        _historyPanel.Controls.Add(titleLabel);

        // Toolbar with filter and buttons using FlowLayoutPanel
        var toolbarPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = DpiHeight(40f),
            AutoSize = false, // Explicit false: explicit height controls layout
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 5, 0, 5)
        };

        // Filter label
        var filterLabel = new Label
        {
            Text = "Filter:",
            AutoSize = false, // Explicit false: FlowLayoutPanel manages layout
            Size = new Size(DpiHeight(45f), DpiHeight(28f)), // Fixed size for toolbar consistency
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 0, 5, 0), // Space to the right
            AccessibleName = "Filter Label",
            AccessibleDescription = "Label for filter input"
        };
        toolbarPanel.Controls.Add(filterLabel);

        // Filter text box with professional sizing
        _filterTextBox = new TextBoxExt
        {
            Size = new Size(DpiHeight(220f), DpiHeight(28f)),
            Anchor = AnchorStyles.Left | AnchorStyles.Top,
            AccessibleName = "Filter Sync History",
            AccessibleDescription = "Enter text to filter sync history records",
            TabIndex = 6,
            TabStop = true
        };
        _sharedTooltip?.SetToolTip(_filterTextBox, "Type to filter the sync history by any field (Timestamp, Operation, Status, etc.)");
        _filterTextBoxTextChangedHandler = (s, e) =>
        {
            if (ViewModel != null)
                ViewModel.FilterText = _filterTextBox.Text;
        };
        _filterTextBox.TextChanged += _filterTextBoxTextChangedHandler;
        toolbarPanel.Controls.Add(_filterTextBox);

        // History toolbar buttons with professional sizing
        _refreshHistoryButton = new SfButton
        {
            Text = "🔄 Refresh",
            Size = new Size(DpiHeight(95f), DpiHeight(32f)),
            Anchor = AnchorStyles.Left | AnchorStyles.Top,
            AccessibleName = "Refresh Sync History",
            AccessibleDescription = "Reloads sync history from database",
            TabIndex = 7,
            TabStop = true
        };
        _sharedTooltip?.SetToolTip(_refreshHistoryButton, "Click to reload sync history from the database");
        _refreshHistoryButtonClickHandler = async (s, e) => await ExecuteCommandAsync(ViewModel?.RefreshHistoryCommand);
        _refreshHistoryButton.Click += _refreshHistoryButtonClickHandler;
        toolbarPanel.Controls.Add(_refreshHistoryButton);

        _clearHistoryButton = new SfButton
        {
            Text = "🗑 Clear",
            Size = new Size(DpiHeight(75f), DpiHeight(32f)),
            Anchor = AnchorStyles.Left | AnchorStyles.Top,
            AccessibleName = "Clear Sync History",
            AccessibleDescription = "Removes all sync history records from the display",
            TabIndex = 8,
            TabStop = true
        };
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

        _exportHistoryButton = new SfButton
        {
            Text = "📤 Export CSV",
            Size = new Size(DpiHeight(105f), DpiHeight(32f)),
            Anchor = AnchorStyles.Left | AnchorStyles.Top,
            AccessibleName = "Export History to CSV",
            AccessibleDescription = "Exports sync history data to CSV file",
            TabIndex = 9,
            TabStop = true
        };
        _sharedTooltip?.SetToolTip(_exportHistoryButton, "Click to export sync history as a CSV file");
        _exportHistoryButtonClickHandler = async (s, e) =>
        {
            var filePath = ShowExportFilePickerDialog();
            if (filePath != null && ViewModel != null)
            {
                try
                {
                    IsBusy = true;
                    UpdateStatus("Exporting sync history...");

                    using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
                    {
                        await writer.WriteLineAsync("Timestamp,Operation,Status,Records Processed,Duration,Message");

                        foreach (var record in ViewModel.FilteredSyncHistory)
                        {
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

                    UpdateStatus($"Exported {ViewModel.FilteredSyncHistory.Count} records to {Path.GetFileName(filePath)}");
                    MessageBox.Show($"Exported {ViewModel.FilteredSyncHistory.Count} records to {Path.GetFileName(filePath)}", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to export sync history");
                    MessageBox.Show($"Export failed: {ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    IsBusy = false;
                }
            }
        };
        _exportHistoryButton.Click += _exportHistoryButtonClickHandler;
        toolbarPanel.Controls.Add(_exportHistoryButton);

        _historyPanel.Controls.Add(toolbarPanel);

        // Grid fills remaining space
        CreateSyncHistoryGrid();
        _syncHistoryGrid!.Dock = DockStyle.Fill;
        _syncHistoryGrid!.AutoSizeColumnsMode = AutoSizeColumnsMode.Fill;
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
                    _statusLabel.ForeColor = isError ? Color.Red : SystemColors.ControlText;
                    try { _statusLabel.Invalidate(); } catch { }
                }
            }
            catch { }
        });
    }

    /// <summary>
    /// Creates the sync history data grid with proper column configuration.
    /// Implements Syncfusion SfDataGrid best practices: explicit column sizing, sorting, resizing.
    /// Call ColumnSizer.Refresh() in OnResize for responsive column layout (per Syncfusion docs).
    /// </summary>
    private void CreateSyncHistoryGrid()
    {
        _syncHistoryGrid = new SfDataGrid
        {
            AutoGenerateColumns = false,
            AllowResizingColumns = true, // Allow user to resize columns
            AllowSorting = true, // Enable column sorting for better UX
            AllowFiltering = false,
            SelectionMode = GridSelectionMode.Single,
            NavigationMode = Syncfusion.WinForms.DataGrid.Enums.NavigationMode.Row,
            RowHeight = 30,
            HeaderRowHeight = 36,
            AccessibleName = "Sync History Grid",
            AccessibleDescription = "Grid displaying QuickBooks sync history records"
        };

        // Define columns with explicit widths for professional appearance
        _syncHistoryGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(QuickBooksSyncHistoryRecord.FormattedTimestamp),
            HeaderText = "Timestamp",
            Width = 150,
            AllowSorting = true,
            AllowResizing = true
        });

        _syncHistoryGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(QuickBooksSyncHistoryRecord.Operation),
            HeaderText = "Operation",
            Width = 130,
            AllowSorting = true,
            AllowResizing = true
        });

        _syncHistoryGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(QuickBooksSyncHistoryRecord.Status),
            HeaderText = "Status",
            Width = 80,
            AllowSorting = true,
            AllowResizing = true
        });

        _syncHistoryGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = nameof(QuickBooksSyncHistoryRecord.RecordsProcessed),
            HeaderText = "Records",
            Width = 75,
            Format = "N0",
            AllowSorting = true,
            AllowResizing = true
        });

        _syncHistoryGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(QuickBooksSyncHistoryRecord.FormattedDuration),
            HeaderText = "Duration",
            Width = 75,
            AllowSorting = true,
            AllowResizing = true
        });

        _syncHistoryGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(QuickBooksSyncHistoryRecord.Message),
            HeaderText = "Message",
            Width = 300,
            AllowSorting = false,
            AllowResizing = true
        });

        // Selection change handler for record details
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

        // Cell styling for status indicators
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
            _syncHistoryGrid.SafeInvoke(() => _syncHistoryGrid.View.Refresh());
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
    /// Uses SfSkinManager cascade - no manual color assignments.
    /// </summary>
    private void ApplySyncfusionTheme()
    {
        try
        {
            // Apply Office2019Colorful theme to entire panel for vibrant, professional appearance
            // Per Syncfusion documentation: https://help.syncfusion.com/windowsforms/themes/office-2019-theme
            SfSkinManager.SetVisualStyle(this, "Office2019Colorful");

            if (_syncHistoryGrid != null)
            {
                // Grid styling with Office2019Colorful palette colors
                _syncHistoryGrid.Style.HeaderStyle.Font.Bold = true;
                _syncHistoryGrid.Style.HeaderStyle.Font.Size = 9.5f;
                _syncHistoryGrid.Style.CellStyle.Font.Size = 9f;

                // Office2019Colorful header: Blue accent (0, 122, 204) with white text
                _syncHistoryGrid.Style.HeaderStyle.BackColor = Color.FromArgb(0, 122, 204);
                _syncHistoryGrid.Style.HeaderStyle.TextColor = Color.White;

                // Cell styling with white background and light blue alternating rows
                _syncHistoryGrid.Style.CellStyle.BackColor = Color.White;
                _syncHistoryGrid.Style.CellStyle.TextColor = Color.Black;

                Logger.LogDebug("Office2019Colorful theme applied successfully to QuickBooksPanel");
            }

            // Apply theme-aware styling to buttons (will be colored per Office2019Colorful)
            ApplyButtonStyles();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to apply Syncfusion theme to QuickBooksPanel");
        }
    }

    /// <summary>
    /// Applies Office2019Colorful-compatible styles to action buttons.
    /// Uses theme-aware colors and consistent styling across all buttons.
    /// </summary>
    private void ApplyButtonStyles()
    {
        // Office2019Colorful palette colors
        const int blueAccent = 0x007ACC;     // RGB(0, 122, 204) - Primary accent
        const int greenAccent = 0x107C10;    // RGB(16, 124, 16) - Success green
        const int redAccent = 0xD13438;      // RGB(209, 52, 56) - Error red
        const int grayAccent = 0x737373;     // RGB(115, 115, 115) - Neutral gray

        try
        {
            // Connection panel buttons
            if (_connectButton != null)
            {
                _connectButton.ForeColor = Color.White;
                _connectButton.Style.BackColor = Color.FromArgb(greenAccent);
            }

            if (_disconnectButton != null)
            {
                _disconnectButton.ForeColor = Color.White;
                _disconnectButton.Style.BackColor = Color.FromArgb(redAccent);
            }

            if (_testConnectionButton != null)
            {
                _testConnectionButton.ForeColor = Color.White;
                _testConnectionButton.Style.BackColor = Color.FromArgb(blueAccent);
            }

            // Operations panel buttons
            if (_syncDataButton != null)
            {
                _syncDataButton.ForeColor = Color.White;
                _syncDataButton.Style.BackColor = Color.FromArgb(blueAccent);
            }

            if (_importAccountsButton != null)
            {
                _importAccountsButton.ForeColor = Color.White;
                _importAccountsButton.Style.BackColor = Color.FromArgb(blueAccent);
            }

            if (_refreshHistoryButton != null)
            {
                _refreshHistoryButton.ForeColor = Color.White;
                _refreshHistoryButton.Style.BackColor = Color.FromArgb(grayAccent);
            }

            if (_clearHistoryButton != null)
            {
                _clearHistoryButton.ForeColor = Color.White;
                _clearHistoryButton.Style.BackColor = Color.FromArgb(redAccent);
            }

            if (_exportHistoryButton != null)
            {
                _exportHistoryButton.ForeColor = Color.White;
                _exportHistoryButton.Style.BackColor = Color.FromArgb(greenAccent);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to apply button styles");
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
    /// Initiates the QuickBooks OAuth 2.0 authorization flow.
    /// Starts the callback handler, generates the authorization URL, and opens the browser.
    /// </summary>
    private async Task InitiateQuickBooksOAuthFlowAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("Starting QuickBooks OAuth flow");

            // Resolve services from the scoped service provider
            var serviceProvider = this.ServiceProvider
                ?? throw new InvalidOperationException("Service provider not available");

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

    #region SplitContainerAdv Configuration Methods

    /// <summary>
    /// Configures all SplitContainerAdv instances using SafeSplitterDistanceHelper.
    /// Sets reasonable min sizes, preferred initial distances, and proportional resizing where appropriate.
    /// </summary>
    private void ConfigureSplitContainersSafely()
    {
        if (_splitContainerMain == null || _splitContainerTop == null || _splitContainerBottom == null)
            return;

        // ------------------------------------------------------------------
        // Main horizontal splitter (top section vs bottom section)
        // Preferred top height ~260 DPI-aware pixels (connection + operations)
        // Not proportional – extra height goes to the history grid
        // ------------------------------------------------------------------
        int mainTopPreferred = DpiHeight(260f);
        SafeSplitterDistanceHelper.ConfigureSafeSplitContainerAdvanced(
            _splitContainerMain,
            panel1MinSize: DpiHeight(180f),     // Minimum top section height
            panel2MinSize: DpiHeight(280f),     // Minimum bottom section (grid needs space)
            desiredDistance: mainTopPreferred,
            splitterWidth: 6);

        // Force the preferred distance even if the control is already sized
        SafeSplitterDistanceHelper.SetSplitterDistanceDeferred(_splitContainerMain, mainTopPreferred);

        // ------------------------------------------------------------------
        // Top vertical splitter (connection panel | operations panel)
        // Proportional – maintains ~45% / 55% split when width changes
        // ------------------------------------------------------------------
        SafeSplitterDistanceHelper.ConfigureSafeSplitContainerAdvanced(
            _splitContainerTop,
            panel1MinSize: DpiHeight(240f),
            panel2MinSize: DpiHeight(320f),  // Increased from 280f to ensure operations buttons fit with padding
            desiredDistance: 0,                  // Ignored – proportion handler will set it
            splitterWidth: 6);

        SafeSplitterDistanceHelper.SetupProportionalResizing(_splitContainerTop, 0.45);

        // Initial proportion (in case SizeChanged hasn't fired yet)
        int topInitial = SafeSplitterDistanceHelper.CalculateSafeDistance(_splitContainerTop, 0.45);
        SafeSplitterDistanceHelper.SetSplitterDistanceDeferred(_splitContainerTop, topInitial);

        // ------------------------------------------------------------------
        // Bottom horizontal splitter (summary panel | history panel)
        // Preferred summary height based on KPI cards + small buffer
        // Not proportional – extra height goes to the history grid
        // ------------------------------------------------------------------
        int summaryPreferred = CalculateSummaryPanelMinHeight() + DpiHeight(20f);
        SafeSplitterDistanceHelper.ConfigureSafeSplitContainerAdvanced(
            _splitContainerBottom,
            panel1MinSize: summaryPreferred - DpiHeight(40f), // Slightly flexible min
            panel2MinSize: DpiHeight(180f),
            desiredDistance: summaryPreferred,
            splitterWidth: 6);

        SafeSplitterDistanceHelper.SetSplitterDistanceDeferred(_splitContainerBottom, summaryPreferred);
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
                int containerDim = sc.Orientation == Orientation.Horizontal ? sc.Height : sc.Width;
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

    /// <summary>
    /// Applies Office2016Colorful style to all splitters for modern appearance.
    /// Synchronizes with SfSkinManager theme when available.
    /// Per Syncfusion documentation: Style property controls visual appearance.
    /// </summary>
    public void ApplySplitterStyleToAllContainers()
    {
        try
        {
            if (_splitContainerMain != null)
            {
                _splitContainerMain.Style = Syncfusion.Windows.Forms.Tools.Enums.Style.Office2016Colorful;
            }
            if (_splitContainerTop != null)
            {
                _splitContainerTop.Style = Syncfusion.Windows.Forms.Tools.Enums.Style.Office2016Colorful;
            }
            if (_splitContainerBottom != null)
            {
                _splitContainerBottom.Style = Syncfusion.Windows.Forms.Tools.Enums.Style.Office2016Colorful;
            }

            Logger.LogDebug("Office2016Colorful style applied to all splitters");
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to apply splitter style");
        }
    }

    /// <summary>
    /// Customizes the splitter grip and arrow appearance with hover colors.
    /// Enhances visual feedback when mouse hovers over splitter.
    /// Per Syncfusion documentation: HotGripDark, HotGripLight, HotExpandFill, HotExpandLine.
    /// </summary>
    /// <param name="splitter">The SplitContainerAdv control to customize</param>
    public void CustomizeSplitterGripAppearance(SplitContainerAdv? splitter)
    {
        if (splitter == null) return;

        try
        {
            // Office2019Colorful palette
            const int blueAccent = 0x007ACC;      // RGB(0, 122, 204)
            const int lightBlue = 0xE8F4F8;       // Light background

            // Normal grip colors (subtle)
            splitter.GripDark = new BrushInfo(Color.FromArgb(117, 117, 117));
            splitter.GripLight = new BrushInfo(Color.FromArgb(200, 200, 200));

            // Expand arrow colors (normal)
            splitter.ExpandFill = new BrushInfo(Color.FromArgb(blueAccent));
            splitter.ExpandLine = Color.White;

            // Hover colors (more pronounced)
            splitter.HotGripDark = new BrushInfo(Color.FromArgb(blueAccent));
            splitter.HotGripLight = new BrushInfo(Color.FromArgb(lightBlue));
            splitter.HotExpandFill = new BrushInfo(Color.FromArgb(0, 122, 204)); // Blue highlight
            splitter.HotExpandLine = Color.White;

            Logger.LogDebug("Splitter grip appearance customized with Office2019 colors");
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to customize splitter grip appearance");
        }
    }

    /// <summary>
    /// Applies professional 3D border styling to all splitters.
    /// Provides visual depth and separation between panels.
    /// Per Syncfusion documentation: BorderStyle can be FixedSingle or Fixed3D.
    /// </summary>
    public void ApplyBorderStyleToAllSplitters()
    {
        try
        {
            if (_splitContainerMain != null)
            {
                _splitContainerMain.BorderStyle = BorderStyle.Fixed3D;
            }
            if (_splitContainerTop != null)
            {
                _splitContainerTop.BorderStyle = BorderStyle.Fixed3D;
            }
            if (_splitContainerBottom != null)
            {
                _splitContainerBottom.BorderStyle = BorderStyle.Fixed3D;
            }

            Logger.LogDebug("Fixed3D border style applied to all splitters");
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to apply border style to splitters");
        }
    }

    /// <summary>
    /// Enables proportional resizing on a splitter.
    /// When user resizes, both panels maintain their proportion relative to container.
    /// Useful for balanced layouts that scale gracefully.
    /// </summary>
    /// <param name="splitter">The SplitContainerAdv control</param>
    /// <param name="proportionForPanel1">Target proportion for Panel1 (0.0-1.0, typically 0.5 for 50/50)</param>
    public void EnableProportionalResizing(SplitContainerAdv? splitter, float proportionForPanel1 = 0.5f)
    {
        if (splitter == null || !splitter.IsHandleCreated) return;

        try
        {
            // Calculate desired distance based on proportion
            int containerSize = splitter.Orientation == Orientation.Horizontal
                ? splitter.Height
                : splitter.Width;

            int desiredDistance = (int)(containerSize * proportionForPanel1);

            // Clamp to valid range
            desiredDistance = Math.Max(splitter.Panel1MinSize,
                Math.Min(desiredDistance, containerSize - splitter.Panel2MinSize - splitter.SplitterWidth));

            splitter.SplitterDistance = desiredDistance;
            Logger.LogDebug("Proportional resizing enabled: Panel1={Proportion:P0}", proportionForPanel1);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to enable proportional resizing");
        }
    }

    /// <summary>
    /// Resets a splitter to its default configuration.
    /// Useful for resetting layout after user customization or theme change.
    /// </summary>
    /// <param name="splitter">The SplitContainerAdv control to reset</param>
    public void ResetSplitterToDefaults(SplitContainerAdv? splitter)
    {
        if (splitter == null) return;

        try
        {
            // Reset to Syncfusion defaults
            splitter.Panel1MinSize = 25;  // Syncfusion default
            splitter.Panel2MinSize = 25;  // Syncfusion default
            splitter.FixedPanel = Syncfusion.Windows.Forms.Tools.Enums.FixedPanel.None;  // Both panels resizable
            splitter.IsSplitterFixed = false;  // Allow splitter movement
            splitter.Panel1Collapsed = false;  // Both panels visible
            splitter.Panel2Collapsed = false;

            Logger.LogDebug("Splitter reset to defaults");
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to reset splitter to defaults");
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

        if (saveDialog.ShowDialog() == DialogResult.OK)
        {
            return saveDialog.FileName;
        }

        return null;
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
