#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using System.Drawing;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Events;
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.WinForms.Controls;
using Syncfusion.Drawing;
using Syncfusion.Windows.Forms.Tools;
using Microsoft.Extensions.Logging;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Theming;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Extensions;
using AppThemeColors = WileyWidget.WinForms.Themes.ThemeColors;

namespace WileyWidget.WinForms.Controls
{
    /// <summary>
    /// User control for displaying proactive AI insights in a grid with priority highlighting.
    /// Uses Syncfusion SfDataGrid with Office2019Colorful theme styling.
    /// Cards display priority badges with color coding and "Ask JARVIS" action buttons.
    /// </summary>
    public partial class InsightFeedPanel : UserControl
    {
        /// <summary>
        /// Simple DataContext wrapper for host compatibility.
        /// </summary>
        public new object? DataContext { get; private set; }

        private readonly IInsightFeedViewModel? _vm;
        private readonly ILogger<InsightFeedPanel>? _logger;
        private readonly Services.IThemeService? _themeService;

        private GradientPanelExt _topPanel = null!;
        private PanelHeader? _panelHeader;
        private LoadingOverlay? _loadingOverlay;
        private Label _lblStatus = null!;
        private SfDataGrid _insightsGrid = null!;
        private ToolStrip _toolStrip = null!;
        private ToolStripButton _btnRefresh = null!;
        private ToolTip? _sharedTooltip;

        /// <summary>
        /// Creates a new instance of the InsightFeedPanel.
        /// </summary>
        public InsightFeedPanel() : this(null, null, null)
        {
        }

        /// <summary>
        /// Creates a new instance with explicit ViewModel and services.
        /// </summary>
        public InsightFeedPanel(
            IInsightFeedViewModel? viewModel = null,
            Services.IThemeService? themeService = null,
            ILogger<InsightFeedPanel>? logger = null)
        {
            InitializeComponent();

            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<InsightFeedPanel>.Instance;
            _themeService = themeService;
            _vm = viewModel ?? new InsightFeedViewModel();

            DataContext = _vm;

            _logger?.LogInformation("InsightFeedPanel initializing");

            InitializeUI();
            BindViewModel();
            ApplyTheme();

            _logger?.LogInformation("InsightFeedPanel initialized successfully");
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
                _topPanel = new GradientPanelExt
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
            if (_vm == null)
            {
                _logger?.LogWarning("ViewModel is null - cannot bind");
                return;
            }

            try
            {
                // Bind insights collection to grid (ObservableCollection handles updates automatically)
                _insightsGrid.DataSource = _vm.InsightCards;
                _logger?.LogDebug("ViewModel InsightCards collection bound to SfDataGrid");

                // Subscribe to ViewModel property changes for UI updates
                _vm.PropertyChanged += ViewModel_PropertyChanged;

                // Wire up refresh command (manual refresh button in toolbar)
                _btnRefresh.Click += RefreshButton_Click;

                // Handle row selection for Ask Jarvis action (single-click to ask about insight)
                _insightsGrid.SelectionChanging += InsightsGrid_SelectionChanging;

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
                        if (_vm?.StatusMessage != null)
                        {
                            _lblStatus.Text = _vm.StatusMessage;
                            _lblStatus.Refresh();
                            _logger?.LogDebug("Status updated: {Status}", _vm.StatusMessage);
                        }
                        break;

                    case nameof(InsightFeedViewModel.IsLoading):
                        if (_loadingOverlay != null)
                        {
                            _loadingOverlay.Visible = _vm?.IsLoading ?? false;
                            _logger?.LogDebug("Loading state changed: {IsLoading}", _vm?.IsLoading);
                        }
                        break;

                    case nameof(InsightFeedViewModel.HighPriorityCount):
                    case nameof(InsightFeedViewModel.MediumPriorityCount):
                    case nameof(InsightFeedViewModel.LowPriorityCount):
                        _logger?.LogDebug(
                            "Priority counts updated: High={High}, Medium={Medium}, Low={Low}",
                            _vm?.HighPriorityCount,
                            _vm?.MediumPriorityCount,
                            _vm?.LowPriorityCount);
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
                if (_vm is InsightFeedViewModel concreteVm)
                {
                    concreteVm.RefreshInsightsCommand.Execute(null);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error executing refresh command");
            }
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
                    if (_vm is InsightFeedViewModel concreteVm)
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
        /// Applies Office2019Colorful theme to the panel and all child controls using SfSkinManager.
        /// SfSkinManager is the single authoritative source for all theming.
        /// </summary>
        private void ApplyTheme()
        {
            try
            {
                // Apply theme via SfSkinManager (authoritative theme source)
                // This cascades to all child controls automatically
                SfSkinManager.SetVisualStyle(this, AppThemeColors.DefaultTheme);

                // Apply theme to key child controls explicitly for cascade assurance
                SfSkinManager.SetVisualStyle(_topPanel, AppThemeColors.DefaultTheme);
                SfSkinManager.SetVisualStyle(_insightsGrid, AppThemeColors.DefaultTheme);

                // Note: Manual BackColor/ForeColor assignments removed
                // All colors now come from SfSkinManager theme cascade
                // This ensures proper theme switching and consistency

                _logger?.LogDebug(
                    "Theme applied successfully to InsightFeedPanel using {Theme}",
                    AppThemeColors.DefaultTheme);

                if (_loadingOverlay != null)
                {
                    SfSkinManager.SetVisualStyle(_loadingOverlay, AppThemeColors.DefaultTheme);
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
                    if (_vm != null)
                    {
                        _vm.PropertyChanged -= ViewModel_PropertyChanged;
                    }

                    if (_btnRefresh != null)
                    {
                        _btnRefresh.Click -= RefreshButton_Click;
                    }

                    if (_insightsGrid != null)
                    {
                        _insightsGrid.SelectionChanging -= InsightsGrid_SelectionChanging;
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
