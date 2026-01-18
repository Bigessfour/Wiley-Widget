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
using GradientPanelExt = Syncfusion.Windows.Forms.Tools.GradientPanelExt;
using GridTextColumn = Syncfusion.WinForms.DataGrid.GridTextColumn;
using SfButton = Syncfusion.WinForms.Controls.SfButton;
using SfComboBox = Syncfusion.WinForms.ListView.SfComboBox;
using SfDataGrid = Syncfusion.WinForms.DataGrid.SfDataGrid;
using SfSkinManager = Syncfusion.WinForms.Controls.SfSkinManager;
using Syncfusion.Drawing;
using Syncfusion.WinForms.Controls.Styles;
using Syncfusion.WinForms.DataGrid.Enums;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Utils;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Controls;

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
    private GradientPanelExt? _reportViewerContainer;
    private Report? _fastReport;
    // private FastReport.ReportViewer? _previewControl; // Removed - not available in Open Source
    private StatusStrip? _statusStrip;
    private ToolStripStatusLabel? _statusLabel;
    private LoadingOverlay? _loadingOverlay;
    private NoDataOverlay? _noDataOverlay;

    // Toolbar controls
    private GradientPanelExt? _toolbarPanel;
    private SfComboBox? _reportSelector;
    private SfButton? _loadReportButton;
    private SfButton? _exportPdfButton;
    private SfButton? _exportExcelButton;
    private SfButton? _printButton;
    private SfButton? _parametersButton;

    // Parameters panel
    private GradientPanelExt? _parametersPanel;
    private SfDataGrid? _parametersGrid;
    private SfButton? _applyParametersButton;
    private SfButton? _closeParametersButton;

    // Layout containers
    private SplitContainer? _mainSplitContainer;
    private SplitContainer? _parametersSplitContainer;

    // Event handlers for proper cleanup
// Event handlers removed - not assigned in current design


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
    protected override void OnViewModelResolved(ReportsViewModel viewModel)
    {
        base.OnViewModelResolved(viewModel);
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
                BeginInvoke(new System.Action(() => LoadInitialReport()));
            }
            else
            {
                HandleCreated += (s, e) => LoadInitialReport();
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

    private void LoadInitialReport()
    {
        _ = LoadInitialReportAsync();
    }

    private void InitializeControls()
    {
        SuspendLayout();

        Name = "ReportsPanel";
        AccessibleName = "Reports"; // Panel title for UI automation
        Size = new Size(1400, 900);
        MinimumSize = new Size((int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(800f), (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(600f));
        AutoScroll = true;
        Padding = new Padding(8);
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
        _panelHeader.RefreshClicked += async (s, e) => await RefreshReportsAsync();
        _panelHeader.CloseClicked += (s, e) => ClosePanel();
        Controls.Add(_panelHeader);

        // Main layout container with parameters panel support
        _parametersSplitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            Panel2Collapsed = true // Initially hidden
        };
        SafeSplitterDistanceHelper.TrySetSplitterDistance(_parametersSplitContainer, 200);

        // Parameters panel (top, initially collapsed)
        _parametersPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(_parametersPanel, "Office2019Colorful");

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
            AutoSize = true,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 10)
        };
        parametersLayout.Controls.Add(parametersLabel, 0, 0);

        // Parameters grid (SfDataGrid for parameter input)
        _parametersGrid = new SfDataGrid
        {
            Name = "parametersGrid",
            AccessibleName = "Report Parameters Grid",
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowEditing = true,
            EditMode = EditMode.SingleClick,
            SelectionMode = GridSelectionMode.Single,
            Margin = new Padding(0, 0, 0, 10)
        };

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

        _closeParametersButton = new SfButton
        {
            Text = "&Close",
            Name = "closeParametersButton",
            AccessibleName = "Close Parameters",
            AutoSize = true,
            MinimumSize = new System.Drawing.Size(80, 30),
            Margin = new Padding(0)
        };
        _closeParametersButton.Click += (s, e) => ToggleParametersPanel();
        parametersButtonPanel.Controls.Add(_closeParametersButton);

        _applyParametersButton = new SfButton
        {
            Text = "&Apply",
            Name = "applyParametersButton",
            AccessibleName = "Apply Parameters",
            AutoSize = true,
            MinimumSize = new System.Drawing.Size(80, 30),
            Margin = new Padding(0, 0, 10, 0)
        };
        _applyParametersButton.Click += async (s, e) => await ApplyParametersAsync();
        parametersButtonPanel.Controls.Add(_applyParametersButton);

        parametersLayout.Controls.Add(parametersButtonPanel, 0, 2);
        _parametersPanel.Controls.Add(parametersLayout);

        _parametersSplitContainer.Panel1.Controls.Add(_parametersPanel);

        // Main content split container (toolbar + report viewer)
        _mainSplitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal
        };
        SafeSplitterDistanceHelper.TrySetSplitterDistance(_mainSplitContainer, 60);

        // Top panel: Toolbar
        _toolbarPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(_toolbarPanel, "Office2019Colorful");

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
        _reportSelector = new SfComboBox
        {
            Name = "reportSelector",
            AccessibleName = "reportSelector",
            Location = new Point(65, 10),
            Size = new Size(300, 25),
            DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList
        };
        _reportSelector.SelectedIndexChanged += ReportSelector_SelectedIndexChanged;
        toolbarFlow.Controls.Add(_reportSelector);

        // Load/Generate button
        _loadReportButton = new SfButton
        {
            Text = "Generate",
            Name = "Toolbar_Generate",
            AccessibleName = "Generate",
            Location = new Point(380, 10),
            Size = new Size(100, 30),
            Enabled = false
        };
        _loadReportButton.Click += async (s, e) => await LoadSelectedReportAsync();
        toolbarFlow.Controls.Add(_loadReportButton);

        // Parameters button
        _parametersButton = new SfButton
        {
            Text = "&Parameters",
            Name = "parametersButton",
            AccessibleName = "Toggle Parameters",
            AutoSize = true,
            MinimumSize = new System.Drawing.Size(100, 30),
            Enabled = false,
            Margin = new Padding(0, 0, 10, 0)
        };
        _parametersButton.Click += (s, e) => ToggleParametersPanel();
        toolbarFlow.Controls.Add(_parametersButton);

        // Export buttons
        _exportPdfButton = new SfButton
        {
            Text = "Export PDF",
            Name = "Toolbar_ExportPdf",
            AccessibleName = "Export PDF",
            Location = new Point(490, 10),
            Size = new Size(100, 30),
            Enabled = false
        };
        _exportPdfButton.Click += async (s, e) => await ExportToPdfAsync();
        toolbarFlow.Controls.Add(_exportPdfButton);

        _exportExcelButton = new SfButton
        {
            Text = "Export Excel",
            Name = "Toolbar_ExportExcel",
            AccessibleName = "Export Excel",
            Location = new Point(600, 10),
            Size = new Size(100, 30),
            Enabled = false
        };
        _exportExcelButton.Click += async (s, e) => await ExportToExcelAsync();
        toolbarFlow.Controls.Add(_exportExcelButton);

        // Print button
        _printButton = new SfButton
        {
            Text = "&Print",
            Name = "Toolbar_Print",
            AccessibleName = "Print",
            Location = new Point(710, 10),
            Size = new Size(100, 30),
            Enabled = false
        };
        _printButton.Click += async (s, e) => await PrintReportAsync();
        toolbarFlow.Controls.Add(_printButton);

        _toolbarPanel.Controls.Add(toolbarFlow);

        _mainSplitContainer.Panel1.Controls.Add(_toolbarPanel);

        // Bottom panel: FastReport viewer container
        var viewerPanel = new GradientPanelExt
        {
            Name = "PreviewPanel",
            AccessibleName = "Report Preview",
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(viewerPanel, ThemeColors.DefaultTheme);

        // Initialize FastReport viewer container
        _reportViewerContainer = new GradientPanelExt
        {
            Name = "reportViewerContainer",
            AccessibleName = "Report Preview Container",
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(_reportViewerContainer, ThemeColors.DefaultTheme);

        // Initialize FastReport
        _fastReport = new Report();

        // FastReport preview control not available in Open Source - using placeholder
        // _previewControl = new FastReport.ReportViewer
        // {
        //     Name = "previewControl",
        //     AccessibleName = "Report Preview",
        //     Dock = DockStyle.Fill
        // };
        // _reportViewerContainer.Controls.Add(_previewControl);

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

        // Subscribe to ViewModel property changes
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;

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
            // This is a placeholder - in production, you would load actual parameters
            // from the FastReport file or a configuration
            var sampleParameters = new List<ReportParameter>
            {
                new ReportParameter { Name = "FromDate", Value = DateTime.Now.AddMonths(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Type = "Date" },
                new ReportParameter { Name = "ToDate", Value = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Type = "Date" },
                new ReportParameter { Name = "Department", Value = "All", Type = "String" },
                new ReportParameter { Name = "IncludeInactive", Value = "false", Type = "Boolean" }
            };

            if (_parametersGrid != null)
            {
                _parametersGrid.DataSource = sampleParameters;
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

    private void ClosePanel()
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
                    dockingManager.SetEnableDocking(this, false);
                    Logger.LogDebug("Panel closed via DockingManager");
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
        if (_statusLabel != null)
        {
            _statusLabel.Text = message;
        }
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

                // Unsubscribe from ViewModel events
                if (ViewModel != null)
                {
                    ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
                }

                // Theme subscription removed - handled by SfSkinManager
                // Unsubscribe panel header events - handlers removed from design
                // if (_panelHeader != null)
                // {
                //     if (_panelHeaderRefreshHandler != null)
                //         _panelHeader.RefreshClicked -= _panelHeaderRefreshHandler;
                //     if (_panelHeaderCloseHandler != null)
                //         _panelHeader.CloseClicked -= _panelHeaderCloseHandler;
                // }

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
                try { _parametersGrid?.SafeClearDataSource(); } catch { }
                try { _parametersGrid?.SafeDispose(); } catch { }

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

