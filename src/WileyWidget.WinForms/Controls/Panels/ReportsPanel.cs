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
using WileyWidget.WinForms.Utilities;
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
using WileyWidget.WinForms.Factories;
using ThemeColors = WileyWidget.WinForms.Themes.ThemeColors;
namespace WileyWidget.WinForms.Controls.Panels;

/// <summary>
/// Production-ready panel for viewing and managing FastReport reports with PDF preview/export workflow.
/// Provides report loading, parameter management, PDF export, and theme integration.
/// Implements ScopedPanelBase pattern for proper DI scoping and lifecycle management.
/// </summary>
[SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
public partial class ReportsPanel : ScopedPanelBase<ReportsViewModel>, IParameterizedPanel
{
    private string? _initialReportPath;

    // UI Controls
    private PanelHeader? _panelHeader;
    private Panel? _reportViewerContainer;
    private Report? _fastReport;
    // private FastReport.ReportViewer? _previewControl; // Removed - not available in Open Source
    private StatusStrip? _statusStrip;
    private ToolStripStatusLabel? _statusLabel;
    private LoadingOverlay? _loadingOverlay;
    private NoDataOverlay? _noDataOverlay;

    // Toolbar controls
    private Panel? _toolbarPanel;
    private SfComboBox? _reportSelector;
    private SfButton? _loadReportButton;
    private SfButton? _exportPdfButton;
    private SfButton? _previewPdfButton;
    private SfButton? _printButton;
    private SfButton? _parametersButton;
    private bool _isPdfPreviewInProgress;

    // Parameters panel
    private Panel? _parametersPanel;
    private SfDataGrid? _parametersGrid;
    private SfButton? _applyParametersButton;
    private SfButton? _closeParametersButton;

    // Layout containers
    private SplitContainerAdv? _mainSplitContainer;
    private SplitContainerAdv? _parametersSplitContainer;

    // Canonical skeleton fields
    private readonly SyncfusionControlFactory? _factory;
    private TableLayoutPanel? _content;

    // Event handlers for proper cleanup (Pattern A & K)
    private EventHandler? _panelHeaderRefreshClickedHandler;
    private EventHandler? _panelHeaderCloseClickedHandler;
    private EventHandler? _loadReportButtonClickHandler;
    private EventHandler? _exportPdfButtonClickHandler;
    private EventHandler? _previewPdfButtonClickHandler;
    private EventHandler? _printButtonClickHandler;
    private EventHandler? _parametersButtonClickHandler;
    private EventHandler? _applyParametersButtonClickHandler;
    private EventHandler? _closeParametersButtonClickHandler;
    private EventHandler? _reportSelectorSelectedIndexChangedHandler;
    private EventHandler? _handleCreatedHandler;
    private PropertyChangedEventHandler? _viewModelPropertyChangedHandler;


    /// <summary>
    /// Initializes a new instance of the <see cref="ReportsPanel"/> class with direct dependencies.
    /// </summary>
    /// <param name="vm">The ViewModel instance.</param>
    /// <summary>
    /// Initializes a new instance of the <see cref="ReportsPanel"/> class.
    /// </summary>
    /// <param name="viewModel">The ViewModel instance.</param>
    /// <param name="controlFactory">The Syncfusion control factory.</param>
    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public ReportsPanel(ReportsViewModel viewModel, SyncfusionControlFactory controlFactory)
        : base(viewModel, controlFactory, ResolveLogger())
    {
        _factory = controlFactory ?? throw new ArgumentNullException(nameof(controlFactory));
        AutoScaleMode = AutoScaleMode.Dpi;
        CompleteDirectInitialization();
    }

    private static ILogger ResolveLogger()
    {
        return Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ILogger<ReportsPanel>>(Program.Services)
            ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ReportsPanel>.Instance;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        MinimumSize = new Size(1024, 720);
        PerformLayout();
        Invalidate(true);
    }

    /// <summary>
    /// Implements ICompletablePanel lifecycle: LoadAsync
    /// </summary>
    public override async Task LoadAsync(CancellationToken ct)
    {
        if (_loadingOverlay != null)
        {
            _loadingOverlay.Visible = true;
        }

        try
        {
            LoadAvailableReports();
            await Task.CompletedTask;
        }
        finally
        {
            if (_loadingOverlay != null)
            {
                _loadingOverlay.Visible = false;
            }
        }
    }

    /// <summary>
    /// Called after the ViewModel has been successfully resolved from the scoped service provider.
    /// Initializes controls and binds to the ViewModel.
    /// </summary>
    /// <param name="viewModel">The resolved ViewModel instance.</param>
    protected override void OnViewModelResolved(ReportsViewModel? viewModel)
    {
        if (viewModel == null)
        {
            Logger.LogWarning("ReportsPanel: ViewModel resolved as null — controls will not initialize.");
            return;
        }

        SafeSuspendAndLayout(InitializeControls);
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
                BeginInvoke(new System.Action(async () =>
                {
                    try { await LoadInitialReportAsync(); }
                    catch (Exception ex) { Logger.LogError(ex, "Failed to auto-load initial report on BeginInvoke"); }
                }));
            }
            else
            {
                EventHandler? handleCreatedForInitial = null;
                handleCreatedForInitial = async (s, e) =>
                {
                    HandleCreated -= handleCreatedForInitial;
                    try { await LoadInitialReportAsync(); }
                    catch (Exception ex) { Logger.LogError(ex, "Failed to auto-load initial report on HandleCreated"); }
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
        MinimumSize = new Size(1024, 720);
        AutoScroll = false;
        Padding = Padding.Empty;
        // Apply theme for cascade to all child controls
        SfSkinManager.SetVisualStyle(this, SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme);
        // Dock style is set by PanelNavigationService (TabbedMDIManager host); do not set Dock here.

        // Panel header
        _panelHeader = new PanelHeader
        {
            Dock = DockStyle.Top,
            Height = LayoutTokens.HeaderHeight
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

        // Canonical _content root
        _content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 1,
            Padding = Padding.Empty,
            Margin = Padding.Empty,
            AutoSize = false,
            Name = "ReportsPanelContent"
        };
        _content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _content.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        // Main layout container with parameters panel support
        _parametersSplitContainer = ControlFactory.CreateSplitContainerAdv(splitter =>
        {
            splitter.Dock = DockStyle.Fill;
            splitter.Orientation = Orientation.Horizontal;
            splitter.Panel1Collapsed = true; // Initially hidden
        });
        SafeSplitterDistanceHelper.TrySetSplitterDistance(_parametersSplitContainer, 200);

        // Parameters panel (top, initially collapsed)
        _parametersPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(LayoutTokens.PanelPadding),
            BorderStyle = BorderStyle.None,
        };

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
        _toolbarPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            BorderStyle = BorderStyle.None,
        };

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

        _previewPdfButton = ControlFactory.CreateSfButton("Pre&view PDF", button =>
        {
            button.Name = "Toolbar_PreviewPdf";
            button.AccessibleName = "Preview PDF";
            button.AccessibleDescription = "Preview the report in a PDF dialog";
            button.AutoSize = true;
            button.Enabled = false;
            button.TabIndex = 5;
            button.Margin = new Padding(0, 0, 10, 0);
        });
        var previewPdfTooltip = new ToolTip();
        previewPdfTooltip.SetToolTip(_previewPdfButton, "Generate a temporary PDF and preview it in-app (Alt+V)");
        _previewPdfButtonClickHandler = async (s, e) => await PreviewReportAsPdfAsync();
        _previewPdfButton.Click += _previewPdfButtonClickHandler;
        toolbarFlow.Controls.Add(_previewPdfButton);

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
        var viewerPanel = new Panel
        {
            Name = "PreviewPanel",
            AccessibleName = "Report Preview",
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
        };

        // Initialize FastReport viewer container
        _reportViewerContainer = new Panel
        {
            Name = "reportViewerContainer",
            AccessibleName = "Report Preview Container",
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
        };

        // Initialize FastReport
        _fastReport = new Report();

        // FastReport Open Source does not include the embedded viewer used by the original design.
        // The supported preview path for this panel is the PDF preview dialog.
        // If the product later adopts a commercial embedded viewer, wire it here deliberately.
        // _previewControl = new FastReport.ReportViewer
        // {
        //     Name = "previewControl",
        //     AccessibleName = "Report Preview",
        //     Dock = DockStyle.Fill
        // };
        // _reportViewerContainer.Controls.Add(_previewControl);

        // Keep the container as a stable host surface for report state messaging.

        viewerPanel.Controls.Add(_reportViewerContainer);
        _mainSplitContainer.Panel2.Controls.Add(viewerPanel);

        _parametersSplitContainer.Panel2.Controls.Add(_mainSplitContainer);
        _content!.Controls.Add(_parametersSplitContainer, 0, 0);
        Controls.Add(_content);

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
            Message = "No report loaded yet\r\nSelect a report, load it, then use Preview PDF or Export PDF",
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

        if (_previewPdfButton != null)
            _previewPdfButton.Enabled = !isLoading && hasReport && !_isPdfPreviewInProgress;

        if (_printButton != null)
            _printButton.Enabled = !isLoading && hasReport && !_isPdfPreviewInProgress;
    }

    private void LoadAvailableReports()
    {
        try
        {
            var displayNames = new List<string>();
            if (ViewModel?.ReportTemplateDisplayNames.Count > 0)
            {
                displayNames.AddRange(ViewModel.ReportTemplateDisplayNames);
            }

            var reportsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports");
            if (displayNames.Count == 0 && !Directory.Exists(reportsDir))
            {
                Logger.LogWarning("Reports directory does not exist: {ReportsDir}", reportsDir);
                UpdateStatus("Reports directory not found");
                return;
            }

            if (displayNames.Count == 0)
            {
                var reportFiles = Directory.GetFiles(reportsDir, "*.frx", SearchOption.AllDirectories);
                foreach (var reportFile in reportFiles)
                {
                    var relativePath = Path.GetRelativePath(reportsDir, reportFile);
                    displayNames.Add(Path.GetFileNameWithoutExtension(relativePath));
                }
            }

            try { _reportSelector!.DataSource = displayNames; }
            catch (Exception ex) { Logger.LogWarning(ex, "Failed to bind report selector DataSource"); }

            if (displayNames.Count > 0)
            {
                _reportSelector.SelectedIndex = 0;
                if (ViewModel != null && _reportSelector.SelectedItem is string reportName)
                {
                    ViewModel.SelectedReportType = reportName;
                }
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
        if (ViewModel != null && _reportSelector?.SelectedItem is string selectedReport)
        {
            ViewModel.SelectedReportType = selectedReport;
        }

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
            ViewModel.SelectedReportType = selectedReport;
            var reportPath = ViewModel.GetReportPathIfExists();

            if (string.IsNullOrWhiteSpace(reportPath))
            {
                UpdateStatus($"Report file not found: {selectedReport}");
                MessageBox.Show($"Report template not found for:\r\n{selectedReport}",
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

    private async Task ExportToPdfAsync(CancellationToken cancellationToken = default)
    {
        if (ViewModel == null)
        {
            return;
        }

        try
        {
            var result = await ExportWorkflowService.ExecuteWithSaveDialogAsync(
                owner: this,
                operationKey: $"{nameof(ReportsPanel)}.Pdf",
                dialogTitle: "Export Report to PDF",
                filter: "PDF files (*.pdf)|*.pdf",
                defaultExtension: "pdf",
                defaultFileName: $"Report_{DateTime.Now:yyyyMMdd_HHmmss}.pdf",
                exportAction: (filePath, ct) => ViewModel.ExportToPdfFileAsync(filePath, ct),
                statusCallback: UpdateStatus,
                logger: Logger,
                cancellationToken: cancellationToken);

            if (result.IsSkipped)
            {
                MessageBox.Show(result.ErrorMessage ?? "An export is already in progress.", "Export",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (result.IsCancelled)
            {
                UpdateStatus("PDF export cancelled.");
                return;
            }

            if (!result.IsSuccess)
            {
                UpdateStatus($"PDF export failed: {result.ErrorMessage}");
                MessageBox.Show($"Failed to export to PDF:\r\n{result.ErrorMessage}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            UpdateStatus($"PDF export completed: {result.FilePath}");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to export to PDF");
            UpdateStatus($"PDF export failed: {ex.Message}");
            MessageBox.Show($"Failed to export to PDF:\r\n{ex.Message}",
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

    }

    private async Task PreviewReportAsPdfAsync(CancellationToken cancellationToken = default)
    {
        if (ViewModel == null || _isPdfPreviewInProgress)
        {
            return;
        }

        string previewFilePath = string.Empty;

        try
        {
            _isPdfPreviewInProgress = true;
            UpdateButtonStates();
            UpdateStatus("Preparing PDF preview...");

            var previewDirectory = Path.Combine(Path.GetTempPath(), "WileyWidget", "PdfPreview");
            Directory.CreateDirectory(previewDirectory);

            previewFilePath = Path.Combine(
                previewDirectory,
                $"ReportPreview_{DateTime.Now:yyyyMMdd_HHmmss_fff}.pdf");

            await ViewModel.ExportToPdfFileAsync(previewFilePath, cancellationToken);

            if (!File.Exists(previewFilePath))
            {
                throw new FileNotFoundException("Generated preview file was not found.", previewFilePath);
            }

            ShowPdfPreviewDialog(previewFilePath);
            UpdateStatus("PDF preview closed");
        }
        catch (OperationCanceledException)
        {
            UpdateStatus("PDF preview cancelled.");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to preview PDF report");
            UpdateStatus($"PDF preview failed: {ex.Message}");
            MessageBox.Show($"Failed to preview PDF:\r\n{ex.Message}",
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _isPdfPreviewInProgress = false;
            UpdateButtonStates();

            if (!string.IsNullOrWhiteSpace(previewFilePath) && File.Exists(previewFilePath))
            {
                try
                {
                    File.Delete(previewFilePath);
                }
                catch (Exception ex)
                {
                    Logger.LogDebug(ex, "Failed to delete temporary preview file: {PreviewFilePath}", previewFilePath);
                }
            }
        }
    }

    private void ShowPdfPreviewDialog(string pdfFilePath)
    {
        if (string.IsNullOrWhiteSpace(pdfFilePath) || !File.Exists(pdfFilePath))
        {
            throw new FileNotFoundException("PDF preview file was not found.", pdfFilePath);
        }

        using var previewForm = new Form
        {
            Text = $"PDF Preview - {Path.GetFileName(pdfFilePath)}",
            StartPosition = FormStartPosition.CenterParent,
            Width = 1200,
            Height = 800,
            MinimumSize = new System.Drawing.Size(900, 600),
            ShowInTaskbar = false,
        };

        var currentTheme = SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme;
        SfSkinManager.SetVisualStyle(previewForm, currentTheme);

        var pdfViewerControl = ControlFactory.CreatePdfViewerControl(viewer =>
        {
            viewer.Name = "ReportsPdfPreviewViewer";
            viewer.AccessibleName = "Report PDF Preview Viewer";
            viewer.AccessibleDescription = "Embedded PDF preview for generated reports";
        });

        previewForm.Controls.Add(pdfViewerControl);
        pdfViewerControl.Load(pdfFilePath);
        previewForm.ShowDialog(this);
    }

    private async Task PrintReportAsync(CancellationToken cancellationToken = default)
    {
        if (ViewModel == null)
        {
            return;
        }

        if (!ViewModel.HasReportLoaded)
        {
            MessageBox.Show("No report is currently loaded.", "Print Failed",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            UpdateStatus("Opening print preview...");
            Logger.LogDebug("Opening embedded PDF preview for print workflow");
            await PreviewReportAsPdfAsync(cancellationToken);
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
                if (_previewPdfButton != null && _previewPdfButtonClickHandler != null)
                    _previewPdfButton.Click -= _previewPdfButtonClickHandler;
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
                _previewPdfButton?.SafeDispose();
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
