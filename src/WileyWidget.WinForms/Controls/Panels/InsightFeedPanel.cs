#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using System.Drawing;
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Events;
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.WinForms.Controls;
using Syncfusion.Drawing;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Supporting;
using AppThemeColors = WileyWidget.WinForms.Themes.ThemeColors;

using LegacyGradientPanel = WileyWidget.WinForms.Controls.Base.LegacyGradientPanel;

namespace WileyWidget.WinForms.Controls.Panels
{
    /// <summary>
    /// Panel for displaying proactive AI insights in a grid with priority highlighting.
    /// Uses Syncfusion SfDataGrid with SfSkinManager theme styling.
    /// Cards display priority badges with color coding and "Ask JARVIS" action buttons.
    /// </summary>
    public partial class InsightFeedPanel : ScopedPanelBase<InsightFeedViewModel>
    {
        /// <summary>Public typed ViewModel for external access (e.g. ProactiveInsightsPanel container).</summary>
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public new InsightFeedViewModel? ViewModel => base.ViewModel;

        private LegacyGradientPanel _topPanel = null!;
        private PanelHeader? _panelHeader;
        private LoadingOverlay? _loadingOverlay;
        private Label _lblStatus = null!;
        private SfDataGrid _insightsGrid = null!;
        private ToolStrip _toolStrip = null!;
        private ToolStripButton _btnRefresh = null!;
        private ToolTip? _sharedTooltip;

        private EventHandler? _refreshButtonClickHandler;
        private PropertyChangedEventHandler ViewModelPropertyChangedHandler;
        private SelectionChangingEventHandler? _insightsGridSelectionChangingHandler;
        private FilterChangingEventHandler? _insightsGridFilterChangingHandler;
        private EventHandler? _panelHeaderRefreshHandler;
        private EventHandler? _panelHeaderCloseHandler;
        private EventHandler? _panelHeaderHelpHandler;
        private EventHandler? _panelHeaderPinToggledHandler;

        /// <summary>
        /// Constructor using DI scope factory for proper lifecycle management.
        /// </summary>
        public InsightFeedPanel(IServiceScopeFactory? scopeFactory = null, ILogger<ScopedPanelBase<InsightFeedViewModel>>? logger = null)
            : base(scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory)), (ILogger?)logger ?? NullLogger.Instance)
        {
            InitializeComponent();


            _logger?.LogInformation("InsightFeedPanel initializing");

            InitializeUI();
            BindViewModel();
            ApplyTheme();

            this.PerformLayout();
            this.Refresh();

            _logger?.LogDebug("[PANEL] {PanelName} content anchored and refreshed", this.Name);

            _logger?.LogInformation("InsightFeedPanel initialized successfully");
        }

        public override async Task LoadAsync(CancellationToken ct = default)
        {
            if (ViewModel != null)
            {
                await ViewModel.RefreshAsync(ct);
            }
        }

        public override async Task<ValidationResult> ValidateAsync(CancellationToken ct = default)
        {
            // Insights feed is read-only, minimal validation
            return await Task.FromResult(ValidationResult.Success);
        }

        public override Task SaveAsync(CancellationToken ct = default)
        {
            // Insights feed is read-only, no save needed
            return Task.CompletedTask;
        }

        public override void FocusFirstError()
        {
            _insightsGrid?.Focus();
        }

        /// <summary>
        /// Initializes the UI controls and ensures all child controls are properly created and configured.
        /// </summary>
        private void InitializeUI()
        {
            try
            {
                // Provide breathing room and protect from collapsing
                this.Padding = new Padding(8);
                this.MinimumSize = new Size(320, 240);
                this.AccessibleName = "Insight Feed Panel";
                this.AccessibleDescription = "Displays proactive insights and the data grid";

                // Top panel with header and toolbar
                _topPanel = new LegacyGradientPanel
                {
                    // Removed fixed Height to allow growth; MinimumSize ensures header won't collapse below 60px
                    MinimumSize = new Size(0, 60), // ensures header won't collapse below 60px
                    Dock = DockStyle.Top,
                    Padding = new Padding(12, 8, 12, 8),
                    Name = "InsightFeedTopPanel",
                    AccessibleName = "Insight Feed Top Panel",
                    AccessibleDescription = "Header area for the insight feed",
                    AccessibleRole = AccessibleRole.Pane
                };
                Controls.Add(_topPanel);

                // Status and toolbar (both docked right) are added before the header (Fill) so toolbar appears at the rightmost edge
                // Add status first so toolbar sits at the far right
                // Status label - Theme colors applied via SfSkinManager
                _lblStatus = new Label
                {
                    Text = "Loading insights...",
                    Dock = DockStyle.Right,
                    TextAlign = ContentAlignment.MiddleRight,
                    Margin = new Padding(8),
                    AutoSize = true,
                    MaximumSize = new Size(300, int.MaxValue),
                    AutoEllipsis = true,
                    Name = "StatusLabel",
                    AccessibleName = "Status Label",
                    AccessibleDescription = "Displays current status of the insights feed"
                };
                _topPanel.Controls.Add(_lblStatus);

                _toolStrip = new ToolStrip
                {
                    Height = 32,
                    Dock = DockStyle.Right,
                    AutoSize = true,
                    GripStyle = ToolStripGripStyle.Hidden,
                    Margin = new Padding(0),
                    Padding = new Padding(4, 2, 4, 2),
                    Name = "InsightFeedToolStrip",
                    AccessibleName = "Insight Feed Toolbar",
                    AccessibleDescription = "Toolbar for insight feed actions",
                    AccessibleRole = AccessibleRole.ToolBar
                };
                _topPanel.Controls.Add(_toolStrip);

                _btnRefresh = new ToolStripButton("🔄 Refresh")
                {
                    ToolTipText = "Manually refresh insights",
                    Name = "RefreshButton",
                    AccessibleName = "Refresh Insights",
                    AccessibleDescription = "Click to manually refresh the insights feed",
                    Margin = new Padding(4, 0, 4, 0)
                };
                _toolStrip.Items.Add(_btnRefresh);

                // Panel header - add last so DockStyle.Fill sizes correctly after other docked controls
                _panelHeader = new PanelHeader
                {
                    Dock = DockStyle.Fill,
                    AccessibleName = "Insights Header",
                    AccessibleDescription = "Title area for the insights feed"
                };
                _topPanel.Controls.Add(_panelHeader);

                // Wire PanelHeader events
                _panelHeaderRefreshHandler = RefreshButton_Click;
                _panelHeaderCloseHandler = (s, e) => ClosePanel();
                _panelHeaderHelpHandler = PanelHeader_HelpClicked;
                _panelHeaderPinToggledHandler = PanelHeader_PinToggled;
                _panelHeader.RefreshClicked += _panelHeaderRefreshHandler;
                _panelHeader.CloseClicked += _panelHeaderCloseHandler;
                _panelHeader.HelpClicked += _panelHeaderHelpHandler;
                _panelHeader.PinToggled += _panelHeaderPinToggledHandler;

                // Insights grid - configured for Office2019Colorful theme with full Syncfusion support
                _insightsGrid = new SfDataGrid
                {
                    Dock = DockStyle.Fill,
                    AllowSelectionOnMouseDown = true,
                    AllowDraggingColumns = false,
                    AllowResizingColumns = true,
                    AllowSorting = true,
                    AllowGrouping = false,
                    AllowFiltering = true,
                    AllowEditing = false,
                    ShowRowHeader = false,
                    ShowGroupDropArea = false,
                    RowHeight = 40,
                    AutoSizeColumnsMode = AutoSizeColumnsMode.Fill,
                    SelectionMode = GridSelectionMode.Extended,
                    Name = "InsightsDataGrid",
                    AccessibleName = "Insights Data Grid",
                    AccessibleDescription = "Displays proactive AI insights with priority levels and actions",
                    AutoGenerateColumns = false
                };
                Controls.Add(_insightsGrid);

                // Loading overlay (added last so it can overlay the grid when visible)
                _loadingOverlay = new LoadingOverlay
                {
                    Dock = DockStyle.Fill,
                    Name = "InsightLoadingOverlay",
                    AccessibleName = "Loading Indicator"
                };
                Controls.Add(_loadingOverlay);

                // Configure grid columns
                ConfigureGridColumns();

                _sharedTooltip = new ToolTip();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error initializing InsightFeedPanel UI");
                throw;
            }
        }

        /// <summary>
        /// Configures the SfDataGrid columns for displaying insights.
        /// Column layout: Priority | Category | Details | Generated (timestamp)
        /// All columns support sorting and filtering as per specification.
        /// </summary>
        private void ConfigureGridColumns()
        {
            try
            {
                // Priority column with color coding (High/Medium/Low)
                var priorityColumn = new GridTextColumn
                {
                    MappingName = nameof(InsightCardModel.Priority),
                    HeaderText = "Priority",
                    Width = 80,
                    AllowSorting = true,
                    AllowFiltering = true
                };

                // Category column (Budget, Revenue, Expenditure, etc.)
                var categoryColumn = new GridTextColumn
                {
                    MappingName = nameof(InsightCardModel.Category),
                    HeaderText = "Category",
                    Width = 150,
                    AllowSorting = true,
                    AllowFiltering = true
                };

                // Explanation/Response column (the actual insight text)
                var explanationColumn = new GridTextColumn
                {
                    MappingName = nameof(InsightCardModel.Explanation),
                    HeaderText = "Details",
                    Width = 400,
                    AllowSorting = true,
                    AllowFiltering = true
                };

                // Timestamp column (when the insight was generated)
                var timestampColumn = new GridTextColumn
                {
                    MappingName = nameof(InsightCardModel.Timestamp),
                    HeaderText = "Generated",
                    Width = 150,
                    AllowSorting = true,
                    AllowFiltering = true,
                    Format = "g"
                };

                _insightsGrid.Columns.Add(priorityColumn);
                _insightsGrid.Columns.Add(categoryColumn);
                _insightsGrid.Columns.Add(explanationColumn);
                _insightsGrid.Columns.Add(timestampColumn);

                _logger?.LogDebug("Grid columns configured successfully with {ColumnCount} columns", _insightsGrid.Columns.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error configuring grid columns");
                throw;
            }
        }

        /// <summary>
        /// Binds the ViewModel to the UI controls.
        /// Establishes bindings for data source, status messages, loading state, and commands.
        /// </summary>
        private void BindViewModel()
        {
            if (ViewModel == null)
            {
                _logger?.LogWarning("ViewModel is null - cannot bind");
                return;
            }

            try
            {
                // Bind insights collection to grid (ObservableCollection handles updates automatically)
                _insightsGrid.DataSource = ViewModel.InsightCards;
                _logger?.LogDebug("ViewModel InsightCards collection bound to SfDataGrid");

                // Subscribe to ViewModel property changes for UI updates
                ViewModelPropertyChangedHandler = new System.ComponentModel.PropertyChangedEventHandler(ViewModel_PropertyChanged);
                ViewModel.PropertyChanged += ViewModelPropertyChangedHandler;

                // Wire up refresh command (manual refresh button in toolbar)
                _refreshButtonClickHandler = RefreshButton_Click;
                _btnRefresh.Click += _refreshButtonClickHandler;

                // Handle row selection for Ask Jarvis action (single-click to ask about insight)
                _insightsGridSelectionChangingHandler = InsightsGrid_SelectionChanging;
                _insightsGrid.SelectionChanging += _insightsGridSelectionChangingHandler;

                // Prevent invalid relational filters on string columns
                _insightsGridFilterChangingHandler = InsightsGrid_FilterChanging;
                _insightsGrid.FilterChanging += _insightsGridFilterChangingHandler;

                _logger?.LogInformation("ViewModel bound successfully to InsightFeedPanel");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error binding ViewModel to UI");
                throw;
            }
        }

        /// <summary>
        /// Handles ViewModel property changes and updates UI accordingly.
        /// </summary>
        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            try
            {
                switch (e?.PropertyName)
                {
                    case nameof(InsightFeedViewModel.StatusMessage):
                        if (ViewModel?.StatusMessage != null)
                        {
                            _lblStatus.Text = ViewModel.StatusMessage;
                            _lblStatus.Refresh();
                            _logger?.LogDebug("Status updated: {Status}", ViewModel.StatusMessage);
                        }
                        break;

                    case nameof(InsightFeedViewModel.IsLoading):
                        if (_loadingOverlay != null)
                        {
                            _loadingOverlay.Visible = ViewModel?.IsLoading ?? false;
                            _logger?.LogDebug("Loading state changed: {IsLoading}", ViewModel?.IsLoading);
                        }
                        break;

                    case nameof(InsightFeedViewModel.HighPriorityCount):
                    case nameof(InsightFeedViewModel.MediumPriorityCount):
                    case nameof(InsightFeedViewModel.LowPriorityCount):
                        _logger?.LogDebug(
                            "Priority counts updated: High={High}, Medium={Medium}, Low={Low}",
                            ViewModel?.HighPriorityCount,
                            ViewModel?.MediumPriorityCount,
                            ViewModel?.LowPriorityCount);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling ViewModel property change: {PropertyName}", e?.PropertyName);
            }
        }

        /// <summary>
        /// Handles refresh button click to manually trigger insights refresh.
        /// </summary>
        private void RefreshButton_Click(object? sender, EventArgs e)
        {
            try
            {
                _logger?.LogInformation("User requested manual refresh of insights");
                if (ViewModel is InsightFeedViewModel concreteVm)
                {
                    concreteVm.RefreshInsightsCommand.Execute(null);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error executing refresh command");
            }
        }

        private void PanelHeader_HelpClicked(object? sender, EventArgs e)
        {
            MessageBox.Show(
                "Insight Feed Help: Real-time AI insights for your data.",
                "Help",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void PanelHeader_PinToggled(object? sender, EventArgs e)
        {
            // Pin behavior is managed by host docking infrastructure.
        }

        /// <summary>
        /// Handles row selection in the grid and executes Ask Jarvis command.
        /// The selected insight is passed to the chat ViewModel for context.
        /// </summary>
        private void InsightsGrid_SelectionChanging(object? sender, SelectionChangingEventArgs? e)
        {
            try
            {
                if (e?.AddedItems?.Count > 0 && e.AddedItems[0] is InsightCardModel card)
                {
                    _logger?.LogInformation(
                        "User selected insight: Category={Category}, Priority={Priority}",
                        card.Category,
                        card.Priority);
                    if (ViewModel is InsightFeedViewModel concreteVm)
                    {
                        concreteVm.AskJarvisCommand.Execute(card);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling grid selection");
            }
        }

        /// <summary>
        /// Prevents invalid relational filters on string columns to fix System.InvalidOperationException:
        /// "The binary operator GreaterThan is not defined for the types 'System.String' and 'System.String'"
        /// </summary>
        private void InsightsGrid_FilterChanging(object? sender, FilterChangingEventArgs e)
        {
            if (e?.Column?.MappingName == null)
            {
                return;
            }

            // String columns that should not allow relational comparison operators
            var isStringColumn =
                e.Column.MappingName == nameof(InsightCardModel.Priority) ||
                e.Column.MappingName == nameof(InsightCardModel.Category) ||
                e.Column.MappingName == nameof(InsightCardModel.Explanation);

            if (!isStringColumn)
            {
                return;
            }

            // Check if any predicate uses relational operators (GreaterThan, LessThan, etc.)
            var hasRelationalPredicate = e.FilterPredicates.Any(p =>
                p.FilterType == Syncfusion.Data.FilterType.GreaterThan ||
                p.FilterType == Syncfusion.Data.FilterType.GreaterThanOrEqual ||
                p.FilterType == Syncfusion.Data.FilterType.LessThan ||
                p.FilterType == Syncfusion.Data.FilterType.LessThanOrEqual);

            if (!hasRelationalPredicate)
            {
                return;
            }

            // Cancel the filter and log the prevention
            e.Cancel = true;
            _logger?.LogDebug("InsightFeedPanel: Cancelled invalid relational filter on string column {Column}", e.Column.MappingName);
        }

        /// <summary>
        /// Applies Office2019Colorful theme to the panel and all child controls using SfSkinManager.
        /// SfSkinManager is the single authoritative source for all theming.
        /// </summary>
        private void ApplyTheme()
        {
            try
            {
                // Apply current application theme via SfSkinManager (authoritative theme source)
                // This cascades to all child controls automatically
                var currentTheme = SfSkinManager.ApplicationVisualTheme ?? AppThemeColors.DefaultTheme;
                SfSkinManager.SetVisualStyle(this, currentTheme);

                // Apply theme to key child controls explicitly for cascade assurance
                SfSkinManager.SetVisualStyle(_topPanel, currentTheme);
                SfSkinManager.SetVisualStyle(_insightsGrid, currentTheme);

                // Note: Manual BackColor/ForeColor assignments removed
                // All colors now come from SfSkinManager theme cascade
                // This ensures proper theme switching and consistency

                _logger?.LogDebug(
                    "Theme applied successfully to InsightFeedPanel using {Theme}",
                    currentTheme);

                if (_loadingOverlay != null)
                {
                    SfSkinManager.SetVisualStyle(_loadingOverlay, currentTheme);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to apply theme to InsightFeedPanel");
                // Graceful degradation - don't crash if theming fails
            }
        }

        // Constructor-time resolution via Program.Services has been removed in favor of constructor injection.
        // Provide explicit parameters to the constructor in DI scenarios (ViewModel, IThemeService, ILogger).

        /// <summary>
        /// Initializes the designer component. Auto-generated code.
        /// </summary>
        private void InitializeComponent()
        {
            try { this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi; } catch { }
            this.SuspendLayout();
            this.Name = "InsightFeedPanel";
            this.Size = new System.Drawing.Size(800, 600);
            this.ResumeLayout(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    // Unsubscribe from events to prevent leaks
                    if (ViewModel != null && ViewModelPropertyChangedHandler != null)
                    {
                        ViewModel.PropertyChanged -= ViewModelPropertyChangedHandler;
                    }

                    if (_btnRefresh != null && _refreshButtonClickHandler != null)
                    {
                        _btnRefresh.Click -= _refreshButtonClickHandler;
                    }

                    if (_panelHeader != null)
                    {
                        if (_panelHeaderRefreshHandler != null)
                        {
                            _panelHeader.RefreshClicked -= _panelHeaderRefreshHandler;
                        }
                        if (_panelHeaderCloseHandler != null)
                        {
                            _panelHeader.CloseClicked -= _panelHeaderCloseHandler;
                        }
                        if (_panelHeaderHelpHandler != null)
                        {
                            _panelHeader.HelpClicked -= _panelHeaderHelpHandler;
                        }
                        if (_panelHeaderPinToggledHandler != null)
                        {
                            _panelHeader.PinToggled -= _panelHeaderPinToggledHandler;
                        }
                    }

                    if (_insightsGrid != null && _insightsGridSelectionChangingHandler != null)
                    {
                        _insightsGrid.SelectionChanging -= _insightsGridSelectionChangingHandler;
                    }

                    if (_insightsGrid != null && _insightsGridFilterChangingHandler != null)
                    {
                        _insightsGrid.FilterChanging -= _insightsGridFilterChangingHandler;
                    }

                    _sharedTooltip?.Dispose();
                    _topPanel?.Dispose();
                    _panelHeader?.Dispose();
                    _loadingOverlay?.Dispose();
                    _lblStatus?.Dispose();
                    _insightsGrid?.Dispose();
                    _toolStrip?.Dispose();
                    _btnRefresh?.Dispose();

                    _logger?.LogDebug("InsightFeedPanel disposed successfully");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error during InsightFeedPanel disposal");
                }
            }

            base.Dispose(disposing);
        }
    }
}





