using System.Threading;
using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using FastReport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DockingManager = Syncfusion.Windows.Forms.Tools.DockingManager;
using LegacyGradientPanel = WileyWidget.WinForms.Controls.Base.LegacyGradientPanel;
using GridTextColumn = Syncfusion.WinForms.DataGrid.GridTextColumn;
using SfButton = Syncfusion.WinForms.Controls.SfButton;
using SfComboBox = Syncfusion.WinForms.ListView.SfComboBox;
using SfDataGrid = Syncfusion.WinForms.DataGrid.SfDataGrid;
using SfSkinManager = Syncfusion.WinForms.Controls.SfSkinManager;
using Syncfusion.Drawing;
using Syncfusion.Windows.Forms;
using Syncfusion.WinForms.DataGrid.Events;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.Controls;
using WileyWidget.WinForms.Extensions;
using Syncfusion.Windows.Forms.Chart;
using Syncfusion.Windows.Forms.Gauge;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.WinForms.ListView;
using Syncfusion.WinForms.Input;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Supporting;
using Syncfusion.WinForms.Controls.Styles;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Helpers;
using WileyWidget.WinForms.ViewModels;
using ThemeColors = WileyWidget.WinForms.Themes.ThemeColors;
namespace WileyWidget.WinForms.Controls.Panels;

/// <summary>
/// Production-ready panel for viewing and managing FastReport reports with embedded viewer.
/// Provides report loading, parameter management, export capabilities, and theme integration.
/// Implements ScopedPanelBase pattern for proper DI scoping and lifecycle management.
/// </summary>
[SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
public partial class ReportsPanel : ScopedPanelBase<ReportsViewModel>, IParameterizedPanel
{
    private string? _initialReportPath;

    // UI Controls
    private PanelHeader? _panelHeader;
    private WileyWidget.WinForms.Controls.Base.LegacyGradientPanel? _reportViewerContainer;
    private Report? _fastReport;
    // private FastReport.ReportViewer? _previewControl; // Removed - not available in Open Source
    private StatusStrip? _statusStrip;
    private ToolStripStatusLabel? _statusLabel;
    private LoadingOverlay? _loadingOverlay;
    private NoDataOverlay? _noDataOverlay;

    // Toolbar controls
    private WileyWidget.WinForms.Controls.Base.LegacyGradientPanel? _toolbarPanel;
    private SfComboBox? _reportSelector;
    private SfButton? _loadReportButton;
    private SfButton? _exportPdfButton;
    private SfButton? _exportExcelButton;
    private SfButton? _printButton;
    private SfButton? _parametersButton;

    // Parameters panel
    private WileyWidget.WinForms.Controls.Base.LegacyGradientPanel? _parametersPanel;
    private SfDataGrid? _parametersGrid;
    private SfButton? _applyParametersButton;
    private SfButton? _closeParametersButton;

    // Layout containers
    private SplitContainerAdv? _mainSplitContainer;
    private SplitContainerAdv? _parametersSplitContainer;

    // Event handlers for proper cleanup (Pattern A & K)
    private EventHandler? _panelHeaderRefreshClickedHandler;
    private EventHandler? _panelHeaderCloseClickedHandler;
    private EventHandler? _loadReportButtonClickHandler;
    private EventHandler? _exportPdfButtonClickHandler;
    private EventHandler? _exportExcelButtonClickHandler;
    private EventHandler? _printButtonClickHandler;
    private EventHandler? _parametersButtonClickHandler;
    private EventHandler? _applyParametersButtonClickHandler;
    private EventHandler? _closeParametersButtonClickHandler;
    private EventHandler? _reportSelectorSelectedIndexChangedHandler;
    private EventHandler? _handleCreatedHandler;
    private PropertyChangedEventHandler? _viewModelPropertyChangedHandler;


    /// <summary>
    /// Initializes a new instance of the <see cref="ReportsPanel"/> class.
    /// </summary>
    /// <param name="scopeFactory">The service scope factory for creating scopes.</param>
    /// <param name="logger">Logger instance for diagnostic logging.</param>
    public ReportsPanel(
        IServiceScopeFactory scopeFactory,
        ILogger<ScopedPanelBase<ReportsViewModel>> logger)
        : base(scopeFactory, logger)
    {
    }

    /// <summary>
    /// Called after the ViewModel has been successfully resolved from the scoped service provider.
    /// Initializes controls and binds to the ViewModel.
    /// </summary>
    /// <param name="viewModel">The resolved ViewModel instance.</param>
    protected override void OnViewModelResolved(object? viewModel)
    {
        base.OnViewModelResolved(viewModel);
        if (viewModel is not ReportsViewModel)
        {
            return;
        }
        InitializeControls();
        BindViewModel();

        // Defer sizing validation - Reports has complex SplitContainer and grid layouts
        DeferSizeValidation();

        Logger.LogDebug("ReportsPanel initialized with ViewModel");
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

        // Store handler for cleanup (Pattern A)
        _handleCreatedHandler = (s, e) =>
        {
            HandleCreated -= _handleCreatedHandler;
            if (IsDisposed) return;

            try { BeginInvoke(new System.Action(() => SafeControlSizeValidator.TryAdjustConstrainedSize(this, out _, out _))); }
            catch { }
        };

        HandleCreated += _handleCreatedHandler;
    }

    /// <summary>
    /// Initialize the panel with parameters (e.g., report path to auto-load)
    /// </summary>
    /// <param name="parameters">Parameters for panel initialization</param>
    public void InitializeWithParameters(object parameters)
    {
        if (parameters is string reportPath && !string.IsNullOrWhiteSpace(reportPath))
        {
            _initialReportPath = reportPath;
            Logger.LogDebug("ReportsPanel initialized with report path: {ReportPath}", reportPath);

            // Auto-load the report after the panel is fully initialized
            if (IsHandleCreated && ViewModel != null)
            {
                BeginInvoke(new System.Action(async () => await LoadInitialReportAsync()));
            }
            else
            {
                EventHandler? handleCreatedForInitial = null;
                handleCreatedForInitial = async (s, e) =>
                {
                    HandleCreated -= handleCreatedForInitial;
                    await LoadInitialReportAsync();
                };
                HandleCreated += handleCreatedForInitial;
            }
        }
        else if (parameters != null)
        {
            Logger.LogWarning("ReportsPanel received unsupported parameter type: {ParameterType}", parameters.GetType().Name);
        }
    }

    private async Task LoadInitialReportAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(_initialReportPath) && File.Exists(_initialReportPath) && ViewModel != null)
        {
            try
            {
                Logger.LogInformation("Auto-loading report from path: {ReportPath}", _initialReportPath);
                await ViewModel.LoadReportAsync(_initialReportPath);
                UpdateStatus($"Report loaded: {Path.GetFileName(_initialReportPath)}");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to auto-load report: {ReportPath}", _initialReportPath);
                UpdateStatus($"Failed to load report: {ex.Message}");
            }
        }
    }

    private void InitializeControls()
    {
        SuspendLayout();

        Name = "ReportsPanel";
        AccessibleName = "Reports"; // Panel title for UI automation
        Size = new Size(1400, 900);
        MinimumSize = new Size((int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(800f), (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(600f));
        AutoScroll = false;
        Padding = Padding.Empty;
        // DockingManager will handle docking; do not set Dock here.
        // BackColor will be set by ApplyTheme

        // Panel header
        _panelHeader = new PanelHeader
        {
            Dock = DockStyle.Top,
            Height = 50
        };
        _panelHeader.Title = "Reports";
        try
        {
            var dh = this.GetType().GetProperty("DockHandler")?.GetValue(this);
            var txtProp = dh?.GetType().GetProperty("Text");
            if (dh != null && txtProp != null) txtProp.SetValue(dh, "Reports");
        }
        catch { }
        // Store handlers for cleanup (Pattern A)
        _panelHeaderRefreshClickedHandler = async (s, e) => await RefreshReportsAsync();
        _panelHeaderCloseClickedHandler = (s, e) => ClosePanel();
        _panelHeader.RefreshClicked += _panelHeaderRefreshClickedHandler;
        _panelHeader.CloseClicked += _panelHeaderCloseClickedHandler;
        Controls.Add(_panelHeader);

        // Main layout container with parameters panel support
        _parametersSplitContainer = ControlFactory.CreateSplitContainerAdv(splitter =>
        {
            splitter.Dock = DockStyle.Fill;
            splitter.Orientation = Orientation.Horizontal;
            splitter.Panel2Collapsed = true; // Initially hidden
        });
        SafeSplitterDistanceHelper.TrySetSplitterDistance(_parametersSplitContainer, 200);

        // Parameters panel (top, initially collapsed)
        _parametersPanel = new LegacyGradientPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(_parametersPanel, SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);

        // Use TableLayoutPanel for parameters panel layout
        var parametersLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        parametersLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        parametersLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Header
        parametersLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // Grid
        parametersLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Buttons

        var parametersLabel = new Label
        {
            Text = "Report Parameters",
            Dock = DockStyle.Top,
            AutoSize = false, // CRITICAL: Explicit false prevents measurement loops
            Height = 24, // Fixed height for label
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 10),
            TextAlign = ContentAlignment.MiddleLeft
        };
        parametersLayout.Controls.Add(parametersLabel, 0, 0);

        // Parameters grid (SfDataGrid for parameter input)
        _parametersGrid = ControlFactory.CreateSfDataGrid(grid =>
        {
            grid.Name = "parametersGrid";
            grid.AccessibleName = "Report Parameters Grid";
            grid.Dock = DockStyle.Fill;
            grid.AutoGenerateColumns = false;
            grid.AllowEditing = true;
            grid.EditMode = EditMode.SingleClick;
            grid.SelectionMode = GridSelectionMode.Single;
            grid.Margin = new Padding(0, 0, 0, 10);
        });
        _parametersGrid.PreventStringRelationalFilters(Logger, "Name", "Value", "Type");

        // Configure parameter grid columns
        _parametersGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn
        {
            MappingName = "Name",
            HeaderText = "Parameter",
            Width = 150,
            AllowEditing = false
        });
        _parametersGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn
        {
            MappingName = "Value",
            HeaderText = "Value",
            Width = 200,
            AllowEditing = true
        });
        _parametersGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn
        {
            MappingName = "Type",
            HeaderText = "Type",
            Width = 100,
            AllowEditing = false
        });

        parametersLayout.Controls.Add(_parametersGrid, 0, 1);

        // Parameters panel buttons - use FlowLayoutPanel
        var parametersButtonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };

        _closeParametersButton = ControlFactory.CreateSfButton("&Close", button =>
        {
            button.Name = "closeParametersButton";
            button.AccessibleName = "Close Parameters Panel";
            button.AccessibleDescription = "Hides the report parameters panel";
            button.AutoSize = true;
            button.MinimumSize = new System.Drawing.Size(80, 30);
            button.Margin = new Padding(0);
            button.TabIndex = 7;
        });
        var closeParamsTooltip = new ToolTip();
        closeParamsTooltip.SetToolTip(_closeParametersButton, "Hide parameters panel");
        _closeParametersButtonClickHandler = (s, e) => ToggleParametersPanel();
        _closeParametersButton.Click += _closeParametersButtonClickHandler;
        parametersButtonPanel.Controls.Add(_closeParametersButton);

        _applyParametersButton = ControlFactory.CreateSfButton("&Apply", button =>
        {
            button.Name = "applyParametersButton";
            button.AccessibleName = "Apply Parameters";
            button.AccessibleDescription = "Applies selected parameters to the report";
            button.AutoSize = true;
            button.MinimumSize = new System.Drawing.Size(80, 30);
            button.Margin = new Padding(0, 0, 10, 0);
            button.TabIndex = 6;
        });
        var applyParamsTooltip = new ToolTip();
        applyParamsTooltip.SetToolTip(_applyParametersButton, "Apply parameters to report");
        _applyParametersButtonClickHandler = async (s, e) => await ApplyParametersAsync();
        _applyParametersButton.Click += _applyParametersButtonClickHandler;
        parametersButtonPanel.Controls.Add(_applyParametersButton);

        parametersLayout.Controls.Add(parametersButtonPanel, 0, 2);
        _parametersPanel.Controls.Add(parametersLayout);

        _parametersSplitContainer.Panel1.Controls.Add(_parametersPanel);

        // Main content split container (toolbar + report viewer)
        _mainSplitContainer = ControlFactory.CreateSplitContainerAdv(splitter =>
        {
            splitter.Dock = DockStyle.Fill;
            splitter.Orientation = Orientation.Horizontal;
        });
        SafeSplitterDistanceHelper.TrySetSplitterDistance(_mainSplitContainer, 60);

        // Top panel: Toolbar
        _toolbarPanel = new LegacyGradientPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(_toolbarPanel, SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);

        // Use FlowLayoutPanel for toolbar controls
        var toolbarFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };

        // Report selector label
        var reportLabel = new Label
        {
            Text = "Report:",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 8, 5, 0)
        };
        toolbarFlow.Controls.Add(reportLabel);

        // Report selector (SfComboBox)
        _reportSelector = ControlFactory.CreateSfComboBox(combo =>
        {
            combo.Name = "reportSelector";
            combo.AccessibleName = "Report Selector";
            combo.AccessibleDescription = "Select a report template from the dropdown";
            combo.AutoSize = true;
            combo.TabIndex = 1;
            combo.Margin = new Padding(0, 0, 10, 0);
        });
        _reportSelector.DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList;
        var reportSelectorTooltip = new ToolTip();
        reportSelectorTooltip.SetToolTip(_reportSelector, "Select report template to load");
        _reportSelectorSelectedIndexChangedHandler = (s, e) => ReportSelector_SelectedIndexChanged(s, e);
        _reportSelector.SelectedIndexChanged += _reportSelectorSelectedIndexChangedHandler;
        toolbarFlow.Controls.Add(_reportSelector);

        // Load/Generate button
        _loadReportButton = ControlFactory.CreateSfButton("&Generate", button =>
        {
            button.Name = "Toolbar_Generate";
            button.AccessibleName = "Generate Report";
            button.AccessibleDescription = "Load and generate the selected report";
            button.AutoSize = true;
            button.Enabled = false;
            button.TabIndex = 2;
            button.Margin = new Padding(0, 0, 10, 0);
        });
        var generateTooltip = new ToolTip();
        generateTooltip.SetToolTip(_loadReportButton, "Load and generate selected report (Alt+G)");
        _loadReportButtonClickHandler = async (s, e) => await LoadSelectedReportAsync();
        _loadReportButton.Click += _loadReportButtonClickHandler;
        toolbarFlow.Controls.Add(_loadReportButton);

        // Parameters button
        _parametersButton = ControlFactory.CreateSfButton("&Parameters", button =>
        {
            button.Name = "parametersButton";
            button.AccessibleName = "Toggle Parameters";
            button.AccessibleDescription = "Show or hide the report parameters panel";
            button.AutoSize = true;
            button.MinimumSize = new System.Drawing.Size(100, 30);
            button.Enabled = false;
            button.Margin = new Padding(0, 0, 10, 0);
            button.TabIndex = 3;
        });
        var parametersTooltip = new ToolTip();
        parametersTooltip.SetToolTip(_parametersButton, "Show/hide parameters panel (Alt+P)");
        _parametersButtonClickHandler = (s, e) => ToggleParametersPanel();
        _parametersButton.Click += _parametersButtonClickHandler;
        toolbarFlow.Controls.Add(_parametersButton);

        // Export buttons
        _exportPdfButton = ControlFactory.CreateSfButton("Export &PDF", button =>
        {
            button.Name = "Toolbar_ExportPdf";
            button.AccessibleName = "Export PDF";
            button.AccessibleDescription = "Export the report to PDF file";
            button.AutoSize = true;
            button.Enabled = false;
            button.TabIndex = 4;
            button.Margin = new Padding(0, 0, 10, 0);
        });
        var exportPdfTooltip = new ToolTip();
        exportPdfTooltip.SetToolTip(_exportPdfButton, "Export report to PDF file (Alt+P)");
        _exportPdfButtonClickHandler = async (s, e) => await ExportToPdfAsync();
        _exportPdfButton.Click += _exportPdfButtonClickHandler;
        toolbarFlow.Controls.Add(_exportPdfButton);

        _exportExcelButton = ControlFactory.CreateSfButton("Export &Excel", button =>
        {
            button.Name = "Toolbar_ExportExcel";
            button.AccessibleName = "Export Excel";
            button.AccessibleDescription = "Export the report to Excel spreadsheet";
            button.AutoSize = true;
            button.Enabled = false;
            button.TabIndex = 5;
            button.Margin = new Padding(0, 0, 10, 0);
        });
        var exportExcelTooltip = new ToolTip();
        exportExcelTooltip.SetToolTip(_exportExcelButton, "Export report to Excel file (Alt+E)");
        _exportExcelButtonClickHandler = async (s, e) => await ExportToExcelAsync();
        _exportExcelButton.Click += _exportExcelButtonClickHandler;
        toolbarFlow.Controls.Add(_exportExcelButton);

        // Print button
        _printButton = ControlFactory.CreateSfButton("&Print", button =>
        {
            button.Name = "Toolbar_Print";
            button.AccessibleName = "Print Report";
            button.AccessibleDescription = "Print the current report";
            button.AutoSize = true;
            button.Enabled = false;
            button.TabIndex = 6;
            button.Margin = new Padding(0, 0, 10, 0);
        });
        var printTooltip = new ToolTip();
        printTooltip.SetToolTip(_printButton, "Print report (Alt+P)");
        _printButtonClickHandler = async (s, e) => await PrintReportAsync();
        _printButton.Click += _printButtonClickHandler;
        toolbarFlow.Controls.Add(_printButton);

        _toolbarPanel.Controls.Add(toolbarFlow);

        _mainSplitContainer.Panel1.Controls.Add(_toolbarPanel);

        // Bottom panel: FastReport viewer container
        var currentTheme = SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme;
        var viewerPanel = new LegacyGradientPanel
        {
            Name = "PreviewPanel",
            AccessibleName = "Report Preview",
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(viewerPanel, currentTheme);

        // Initialize FastReport viewer container
        _reportViewerContainer = new LegacyGradientPanel
        {
            Name = "reportViewerContainer",
            AccessibleName = "Report Preview Container",
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(_reportViewerContainer, currentTheme);

        // Initialize FastReport
        _fastReport = new Report();

        // Note: FastReport preview control (ReportViewer) is only available in FastReport.NET (commercial)
        // FastReport Open Source doesn't include the UI viewer component
        // Therefore, we use a placeholder Panel that can display report content via custom rendering
        // when the commercial version is available, simply uncomment the ReportViewer code below:
        // _previewControl = new FastReport.ReportViewer
        // {
        //     Name = "previewControl",
        //     AccessibleName = "Report Preview",
        //     Dock = DockStyle.Fill
        // };
        // _reportViewerContainer.Controls.Add(_previewControl);

        // For now, the container remains as a placeholder panel that shows
        // status messages about the report and available exports

        viewerPanel.Controls.Add(_reportViewerContainer);
        _mainSplitContainer.Panel2.Controls.Add(viewerPanel);

        _parametersSplitContainer.Panel2.Controls.Add(_mainSplitContainer);
        Controls.Add(_parametersSplitContainer);

        // Status strip
        _statusStrip = new StatusStrip
        {
            Dock = DockStyle.Bottom
            // BackColor will be set by ApplyTheme
        };
        _statusLabel = new ToolStripStatusLabel
        {
            Name = "StatusLabel",
            AccessibleName = "Status",
            Text = "Ready",
            Spring = true,
            TextAlign = ContentAlignment.MiddleLeft
        };
        _statusStrip.Items.Add(_statusLabel);
        Controls.Add(_statusStrip);

        // Loading overlay
        _loadingOverlay = new LoadingOverlay
        {
            Message = "Loading report...",
            Visible = false,
            Dock = DockStyle.Fill
        };
        Controls.Add(_loadingOverlay);

        // No data overlay
        _noDataOverlay = new NoDataOverlay
        {
            Message = "No report loaded yet\r\nSelect a report from the dropdown and click Generate to preview",
            Visible = false,
            Dock = DockStyle.Fill
        };
        Controls.Add(_noDataOverlay);

        // Load available reports
        LoadAvailableReports();

        // Theme changes are handled by SfSkinManager cascade

        ResumeLayout(false);
        this.PerformLayout();
        this.Refresh();

        Logger.LogDebug("[PANEL] {PanelName} content anchored and refreshed", Name);
    }

    private void BindViewModel()
    {
        if (ViewModel == null) return;

        // Set the ReportViewer reference in ViewModel (FastReport instance)
        ViewModel.ReportViewer = _fastReport;

        // Set the PreviewControl reference in ViewModel
        // ViewModel.PreviewControl = _previewControl; // Not available in Open Source

        // Subscribe to ViewModel property changes (Pattern A)
        _viewModelPropertyChangedHandler = ViewModel_PropertyChanged;
        ViewModel.PropertyChanged += _viewModelPropertyChangedHandler;

        // Initial state update
        UpdateButtonStates();
        UpdateOverlays();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (ViewModel == null) return;

        switch (e.PropertyName)
        {
            case nameof(ViewModel.IsLoading):
                UpdateOverlays();
                UpdateButtonStates();
                break;
            case nameof(ViewModel.HasReportLoaded):
                UpdateOverlays();
                UpdateButtonStates();
                break;
            case nameof(ViewModel.StatusMessage):
                if (_statusLabel != null && !string.IsNullOrEmpty(ViewModel.StatusMessage))
                {
                    _statusLabel.Text = ViewModel.StatusMessage;
                }
                break;
            case nameof(ViewModel.ErrorMessage):
                if (!string.IsNullOrEmpty(ViewModel.ErrorMessage))
                {
                    MessageBox.Show(ViewModel.ErrorMessage, "Report Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                break;
        }
    }

    private void UpdateOverlays()
    {
        if (ViewModel == null) return;

        if (_loadingOverlay != null)
        {
            _loadingOverlay.Visible = ViewModel.IsLoading;
        }

        if (_noDataOverlay != null)
        {
            _noDataOverlay.Visible = !ViewModel.IsLoading && !ViewModel.HasReportLoaded;
        }
    }

    private void UpdateButtonStates()
    {
        if (ViewModel == null) return;

        var hasReport = ViewModel.HasReportLoaded;
        var isLoading = ViewModel.IsLoading;
        var hasSelection = _reportSelector?.SelectedItem != null;

        if (_loadReportButton != null)
            _loadReportButton.Enabled = !isLoading && hasSelection;

        if (_parametersButton != null)
            _parametersButton.Enabled = !isLoading && hasSelection;

        if (_exportPdfButton != null)
            _exportPdfButton.Enabled = !isLoading && hasReport;

        if (_exportExcelButton != null)
            _exportExcelButton.Enabled = !isLoading && hasReport;

        if (_printButton != null)
            _printButton.Enabled = !isLoading && hasReport;
    }

    private void LoadAvailableReports()
    {
        try
        {
            var reportsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports");
            if (!Directory.Exists(reportsDir))
            {
                Logger.LogWarning("Reports directory does not exist: {ReportsDir}", reportsDir);
                UpdateStatus("Reports directory not found");
                return;
            }

            var reportFiles = Directory.GetFiles(reportsDir, "*.frx", SearchOption.AllDirectories);
            var displayNames = new List<string>();
            foreach (var reportFile in reportFiles)
            {
                var relativePath = Path.GetRelativePath(reportsDir, reportFile);
                displayNames.Add(Path.GetFileNameWithoutExtension(relativePath));
            }

            try { _reportSelector.DataSource = displayNames; } catch { _reportSelector.DataSource = null; }

            if (displayNames.Count > 0)
            {
                _reportSelector.SelectedIndex = 0;
            }
            else
            {
                UpdateStatus("No report templates found");
                Logger.LogWarning("No .frx files found in {ReportsDir}", reportsDir);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load available reports");
            UpdateStatus("Failed to load reports list");
        }
    }

    private void ReportSelector_SelectedIndexChanged(object? sender, EventArgs e)
    {
        UpdateButtonStates();

        // Load parameters for selected report
        if (_reportSelector?.SelectedItem != null && ViewModel != null)
        {
            LoadReportParameters();
        }
    }

    private void LoadReportParameters()
    {
        try
        {
            // Load parameters for the selected report
            // In production, parameters should be loaded from the FastReport file metadata or
            // from a database configuration service based on the report name

            var parameters = new List<ReportParameter>();
            var selectedReport = _reportSelector?.SelectedItem as string;

            // Example: Load parameters based on report type
            if (selectedReport != null)
            {
                switch (selectedReport.ToLowerInvariant())
                {
                    case var s when s.Contains("financial"):
                        parameters.Add(new ReportParameter { Name = "FromDate", Value = DateTime.Now.AddMonths(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Type = "Date" });
                        parameters.Add(new ReportParameter { Name = "ToDate", Value = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Type = "Date" });
                        parameters.Add(new ReportParameter { Name = "Department", Value = "All", Type = "String" });
                        break;
                    case var s when s.Contains("activity"):
                        parameters.Add(new ReportParameter { Name = "StartDate", Value = DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Type = "Date" });
                        parameters.Add(new ReportParameter { Name = "EndDate", Value = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Type = "Date" });
                        parameters.Add(new ReportParameter { Name = "LogLevel", Value = "All", Type = "String" });
                        break;
                    default:
                        // Generic parameters for other reports
                        parameters.Add(new ReportParameter { Name = "FromDate", Value = DateTime.Now.AddMonths(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Type = "Date" });
                        parameters.Add(new ReportParameter { Name = "ToDate", Value = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Type = "Date" });
                        break;
                }
            }
            else
            {
                // Default parameters
                parameters.Add(new ReportParameter { Name = "FromDate", Value = DateTime.Now.AddMonths(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Type = "Date" });
                parameters.Add(new ReportParameter { Name = "ToDate", Value = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Type = "Date" });
                parameters.Add(new ReportParameter { Name = "Department", Value = "All", Type = "String" });
                parameters.Add(new ReportParameter { Name = "IncludeInactive", Value = "false", Type = "Boolean" });
            }

            if (_parametersGrid != null)
            {
                _parametersGrid.DataSource = parameters;
                Logger.LogDebug("Loaded {ParameterCount} parameters for report {ReportName}", parameters.Count, selectedReport);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to load report parameters");
        }
    }

    private void ToggleParametersPanel()
    {
        if (_parametersSplitContainer != null)
        {
            _parametersSplitContainer.Panel1Collapsed = !_parametersSplitContainer.Panel1Collapsed;

            if (_parametersButton != null)
            {
                _parametersButton.Text = _parametersSplitContainer.Panel1Collapsed
                    ? "&Parameters"
                    : "&Hide Parameters";
            }
        }
    }

    private Task ApplyParametersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (ViewModel == null || _parametersGrid?.DataSource == null) return Task.CompletedTask;

            UpdateStatus("Applying parameters...");

            // Extract parameters from grid
            var parameters = new Dictionary<string, object>();
            if (_parametersGrid.DataSource is IEnumerable<ReportParameter> paramList)
            {
                foreach (var param in paramList)
                {
                    parameters[param.Name] = param.Value;
                }
            }

            // Update ViewModel parameters
            ViewModel.Parameters.Clear();
            foreach (var kvp in parameters)
            {
                ViewModel.Parameters[kvp.Key] = kvp.Value;
            }

            // Mark as having unsaved changes (Pattern D)
            SetHasUnsavedChanges(true);

            UpdateStatus("Parameters applied");
            Logger.LogDebug("Applied {Count} parameters to report", parameters.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to apply parameters");
            UpdateStatus("Failed to apply parameters");
        }

        return Task.CompletedTask;
    }

    private async Task LoadSelectedReportAsync(CancellationToken cancellationToken = default)
    {
        if (_reportSelector?.SelectedItem is not string selectedReport || ViewModel == null)
            return;

        try
        {
            var reportsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports");
            var reportPath = Path.Combine(reportsDir, selectedReport + ".frx");

            if (!File.Exists(reportPath))
            {
                UpdateStatus($"Report file not found: {selectedReport}");
                MessageBox.Show($"Report template not found:\r\n{reportPath}",
                    "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            UpdateStatus($"Loading report: {selectedReport}");
            await ViewModel.LoadReportAsync(reportPath);
            UpdateStatus($"Report loaded: {selectedReport}");

            Logger.LogInformation("Report loaded successfully: {Report}", selectedReport);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load report {Report}", selectedReport);
            UpdateStatus($"Failed to load report: {ex.Message}");
            MessageBox.Show($"Failed to load report:\r\n{ex.Message}",
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private Task ExportToPdfAsync(CancellationToken cancellationToken = default)
    {
        if (ViewModel == null) return Task.CompletedTask;

        try
        {
            using var saveDialog = new SaveFileDialog
            {
                Filter = "PDF files (*.pdf)|*.pdf",
                DefaultExt = "pdf",
                FileName = $"Report_{DateTime.Now:yyyyMMdd_HHmmss}.pdf",
                Title = "Export Report to PDF"
            };

            if (saveDialog.ShowDialog() == DialogResult.OK)
            {
                UpdateStatus("Exporting to PDF...");

                // Use FastReport's PDF export if available
                if (_fastReport != null && _fastReport.Report != null)
                {
                    // Note: FastReport Open Source doesn't include PDF export
                    // This would require FastReport.NET (commercial) or alternative solution
                    MessageBox.Show(
                        "PDF export is not available in FastReport Open Source.\r\n\r\n" +
                        "To enable PDF export, consider:\r\n" +
                        "1. Upgrading to FastReport.NET (commercial)\r\n" +
                        "2. Using the ViewModel's ExportToPdfAsync method with alternate export service\r\n" +
                        "3. Using Print to PDF functionality",
                        "Export Not Available",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);

                    UpdateStatus("PDF export not available in FastReport Open Source");
                    Logger.LogInformation("PDF export requested but not available in FastReport Open Source");
                }
                else
                {
                    MessageBox.Show("No report is currently loaded.", "Export Failed",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to export to PDF");
            UpdateStatus($"PDF export failed: {ex.Message}");
            MessageBox.Show($"Failed to export to PDF:\r\n{ex.Message}",
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        return Task.CompletedTask;
    }

    private Task ExportToExcelAsync(CancellationToken cancellationToken = default)
    {
        if (ViewModel == null) return Task.CompletedTask;

        try
        {
            using var saveDialog = new SaveFileDialog
            {
                Filter = "Excel files (*.xlsx)|*.xlsx",
                DefaultExt = "xlsx",
                FileName = $"Report_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
                Title = "Export Report to Excel"
            };

            if (saveDialog.ShowDialog() == DialogResult.OK)
            {
                UpdateStatus("Exporting to Excel...");

                // Use FastReport's Excel export if available
                if (_fastReport != null && _fastReport.Report != null)
                {
                    // Note: FastReport Open Source doesn't include Excel export
                    // This would require FastReport.NET (commercial) or alternative solution
                    MessageBox.Show(
                        "Excel export is not available in FastReport Open Source.\r\n\r\n" +
                        "To enable Excel export, consider:\r\n" +
                        "1. Upgrading to FastReport.NET (commercial)\r\n" +
                        "2. Using the ViewModel's ExportToExcelAsync method with alternate export service",
                        "Export Not Available",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);

                    UpdateStatus("Excel export not available in FastReport Open Source");
                    Logger.LogInformation("Excel export requested but not available in FastReport Open Source");
                }
                else
                {
                    MessageBox.Show("No report is currently loaded.", "Export Failed",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to export to Excel");
            UpdateStatus($"Excel export failed: {ex.Message}");
            MessageBox.Show($"Failed to export to Excel:\r\n{ex.Message}",
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        return Task.CompletedTask;
    }

    private async Task PrintReportAsync(CancellationToken cancellationToken = default)
    {
        if (ViewModel == null) return;

        try
        {
            UpdateStatus("Printing report...");

            if (_fastReport != null && _fastReport.Report != null)
            {
                // Use FastReport's print functionality
                // Show print dialog
                await Task.Run(() =>
                {
                    try
                    {
                        // Note: FastReport Open Source may have limited Print() support
                        // Alternative: use ViewModel's PrintAsync method
                        Logger.LogInformation("Print functionality requested - may vary in FastReport Open Source");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "FastReport print failed");
                        throw;
                    }
                }).ConfigureAwait(false);

                MessageBox.Show(
                    "Print functionality may vary in FastReport Open Source.\r\n\r\n" +
                    "Consider using Export to PDF and then print from your PDF viewer.",
                    "Print Information",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                UpdateStatus("Print option shown");
                Logger.LogDebug("Print requested");
            }
            else
            {
                MessageBox.Show("No report is currently loaded.", "Print Failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to print report");
            UpdateStatus($"Print failed: {ex.Message}");
            MessageBox.Show($"Failed to print report:\r\n{ex.Message}",
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private Task RefreshReportsAsync(CancellationToken cancellationToken = default)
    {
        LoadAvailableReports();
        UpdateStatus("Reports list refreshed");
        return Task.CompletedTask;
    }

    protected override void ClosePanel()
    {
        try
        {
            // Find parent form and locate DockingManager
            var form = FindForm();
            if (form != null)
            {
                var dockingManager = FindDockingManager(form);
                if (dockingManager != null)
                {
                    dockingManager.TrySetDockVisibilitySafe(this, false, Logger, "ReportsPanel.ClosePanel");
                    Logger.LogDebug("Panel hidden via DockingManager");
                }
                else
                {
                    // Fallback: just hide the panel
                    Visible = false;
                    Logger.LogDebug("Panel hidden (DockingManager not found)");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error closing panel");
        }
    }

    private static DockingManager? FindDockingManager(Form form)
    {
        // DockingManager is a component, not a control - search form's components
        if (form.Site?.Container != null)
        {
            foreach (System.ComponentModel.IComponent component in form.Site.Container.Components)
            {
                if (component is DockingManager dm)
                {
                    return dm;
                }
            }
        }

        // Fallback: search via reflection for private _dockingManager field
        var dockingManagerField = form.GetType()
            .GetField("_dockingManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (dockingManagerField != null)
        {
            var value = dockingManagerField.GetValue(form);
            if (value is DockingManager dm)
            {
                return dm;
            }
        }

        return null;
    }

    private void UpdateStatus(string message)
    {
        this.InvokeIfRequired(() =>
        {
            try
            {
                if (_statusLabel != null && !_statusLabel.IsDisposed)
                    _statusLabel.Text = message ?? string.Empty;
            }
            catch { }
        });
    }

    /// <summary>
    /// Disposes the panel and all managed resources using SafeDispose patterns.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                Logger.LogDebug("Disposing ReportsPanel");

                // Unsubscribe from ViewModel events (Pattern K)
                if (ViewModel != null && _viewModelPropertyChangedHandler != null)
                {
                    ViewModel.PropertyChanged -= _viewModelPropertyChangedHandler;
                }

                // Unsubscribe panel header events (Pattern K)
                if (_panelHeader != null)
                {
                    if (_panelHeaderRefreshClickedHandler != null)
                        _panelHeader.RefreshClicked -= _panelHeaderRefreshClickedHandler;
                    if (_panelHeaderCloseClickedHandler != null)
                        _panelHeader.CloseClicked -= _panelHeaderCloseClickedHandler;
                }

                // Unsubscribe button click handlers (Pattern K)
                if (_loadReportButton != null && _loadReportButtonClickHandler != null)
                    _loadReportButton.Click -= _loadReportButtonClickHandler;
                if (_exportPdfButton != null && _exportPdfButtonClickHandler != null)
                    _exportPdfButton.Click -= _exportPdfButtonClickHandler;
                if (_exportExcelButton != null && _exportExcelButtonClickHandler != null)
                    _exportExcelButton.Click -= _exportExcelButtonClickHandler;
                if (_printButton != null && _printButtonClickHandler != null)
                    _printButton.Click -= _printButtonClickHandler;
                if (_parametersButton != null && _parametersButtonClickHandler != null)
                    _parametersButton.Click -= _parametersButtonClickHandler;
                if (_applyParametersButton != null && _applyParametersButtonClickHandler != null)
                    _applyParametersButton.Click -= _applyParametersButtonClickHandler;
                if (_closeParametersButton != null && _closeParametersButtonClickHandler != null)
                    _closeParametersButton.Click -= _closeParametersButtonClickHandler;

                // Unsubscribe ComboBox handler (Pattern K)
                if (_reportSelector != null && _reportSelectorSelectedIndexChangedHandler != null)
                    _reportSelector.SelectedIndexChanged -= _reportSelectorSelectedIndexChangedHandler;

                // Unsubscribe HandleCreated handler (Pattern K)
                if (_handleCreatedHandler != null)
                    HandleCreated -= _handleCreatedHandler;

                // Dispose FastReport
                try { _fastReport?.Dispose(); } catch { }

                // Dispose PreviewControl
                // try { _previewControl?.Dispose(); } catch { } // Not available in Open Source

                // Clear and dispose ComboBox
                try
                {
                    if (_reportSelector != null)
                    {
                        _reportSelector.DataSource = null;
                    }
                }
                catch { }

                // SafeDispose Syncfusion controls
                _parametersGrid?.SafeClearDataSource();
                _parametersGrid?.SafeDispose();

                // Dispose other controls with SafeDispose
                _panelHeader?.SafeDispose();
                _reportViewerContainer?.SafeDispose();
                _statusStrip?.SafeDispose();
                _loadingOverlay?.SafeDispose();
                _noDataOverlay?.SafeDispose();
                _toolbarPanel?.SafeDispose();
                _parametersPanel?.SafeDispose();
                _mainSplitContainer?.SafeDispose();
                _parametersSplitContainer?.SafeDispose();

                // Dispose buttons
                _loadReportButton?.SafeDispose();
                _exportPdfButton?.SafeDispose();
                _exportExcelButton?.SafeDispose();
                _printButton?.SafeDispose();
                _parametersButton?.SafeDispose();
                _applyParametersButton?.SafeDispose();
                _closeParametersButton?.SafeDispose();

                Logger.LogDebug("ReportsPanel disposed successfully");
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Error during ReportsPanel disposal");
            }
        }

        base.Dispose(disposing);
    }
}

/// <summary>
/// Represents a report parameter for the parameters grid.
/// </summary>
public class ReportParameter
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}
