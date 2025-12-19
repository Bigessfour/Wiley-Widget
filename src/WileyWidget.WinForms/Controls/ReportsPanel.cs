using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using FastReport;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Services;

namespace WileyWidget.WinForms.Controls;

/// <summary>
/// Panel for viewing and managing FastReport reports with embedded viewer.
/// Provides report loading, parameter management, and export capabilities.
/// </summary>
[SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
public partial class ReportsPanel : UserControl, IParameterizedPanel
{
    private readonly ReportsViewModel _viewModel;
    private readonly ILogger<ReportsPanel> _logger;
    private string? _initialReportPath;

    // UI Controls
    private PanelHeader? _panelHeader;
    private Panel? _reportViewer;
    private StatusStrip? _statusStrip;
    private ToolStripStatusLabel? _statusLabel;
    private LoadingOverlay? _loadingOverlay;
    private NoDataOverlay? _noDataOverlay;
    private Button? _loadReportButton;
    private Button? _exportPdfButton;
    private Button? _exportExcelButton;
    private Button? _printButton;
    private ComboBox? _reportSelector;
    private Panel? _toolbarPanel;
    private SplitContainer? _mainSplitContainer;

    public ReportsPanel(
        ReportsViewModel viewModel,
        ILogger<ReportsPanel> logger)
    {
        // InitializeComponent(); // Not needed for UserControl

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        InitializeControls();
        BindViewModel();

        _logger.LogDebug("ReportsPanel initialized");
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
            _logger.LogDebug("ReportsPanel initialized with report path: {ReportPath}", reportPath);

            // Auto-load the report after the panel is fully initialized
            if (IsHandleCreated)
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
            _logger.LogWarning("ReportsPanel received unsupported parameter type: {ParameterType}", parameters.GetType().Name);
        }
    }

    private void LoadInitialReport()
    {
        if (!string.IsNullOrWhiteSpace(_initialReportPath) && File.Exists(_initialReportPath))
        {
            try
            {
                _logger.LogInformation("Auto-loading report from path: {ReportPath}", _initialReportPath);
                LoadReportFromPath(_initialReportPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-load report: {ReportPath}", _initialReportPath);
            }
        }
    }

    private async void LoadReportFromPath(string reportPath)
    {
        try
        {
            var reportName = Path.GetFileName(reportPath);
            UpdateStatus($"Loading report: {reportName}");
            await _viewModel.LoadReportAsync(reportPath);
            UpdateStatus($"Report loaded: {reportName}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load report from path {ReportPath}", reportPath);
            UpdateStatus($"Failed to load report: {ex.Message}");
        }
    }

    private void InitializeControls()
    {
        Name = "ReportsPanel";
        Size = new Size(1400, 900);
        Dock = DockStyle.Fill;

        // Panel header
        _panelHeader = new PanelHeader { Dock = DockStyle.Top };
        _panelHeader.Title = "Reports";
        _panelHeader.RefreshClicked += async (s, e) => await RefreshReportsAsync();
        _panelHeader.CloseClicked += (s, e) => ClosePanel();
        Controls.Add(_panelHeader);

        // Main layout container
        _mainSplitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 80
        };

        // Top panel: Toolbar
        _toolbarPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 80,
            Padding = new Padding(10)
        };

        // Report selector
        var reportLabel = new Label
        {
            Text = "Report:",
            Location = new Point(10, 15),
            Size = new Size(50, 25),
            TextAlign = ContentAlignment.MiddleLeft
        };
        _toolbarPanel.Controls.Add(reportLabel);

        _reportSelector = new ComboBox
        {
            Location = new Point(65, 10),
            Size = new Size(300, 25),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _reportSelector.SelectedIndexChanged += ReportSelector_SelectedIndexChanged;
        _toolbarPanel.Controls.Add(_reportSelector);

        // Load button
        _loadReportButton = new Button
        {
            Text = "&Load Report",
            Location = new Point(380, 10),
            Size = new Size(100, 30),
            Enabled = false
        };
        _loadReportButton.Click += async (s, e) => await LoadSelectedReportAsync();
        _toolbarPanel.Controls.Add(_loadReportButton);

        // Export buttons
        _exportPdfButton = new Button
        {
            Text = "Export &PDF",
            Location = new Point(490, 10),
            Size = new Size(100, 30),
            Enabled = false
        };
        _exportPdfButton.Click += async (s, e) => await ExportToPdfAsync();
        _toolbarPanel.Controls.Add(_exportPdfButton);

        _exportExcelButton = new Button
        {
            Text = "Export &Excel",
            Location = new Point(600, 10),
            Size = new Size(100, 30),
            Enabled = false
        };
        _exportExcelButton.Click += async (s, e) => await ExportToExcelAsync();
        _toolbarPanel.Controls.Add(_exportExcelButton);

        // Print button
        _printButton = new Button
        {
            Text = "&Print",
            Location = new Point(710, 10),
            Size = new Size(100, 30),
            Enabled = false
        };
        _printButton.Click += async (s, e) => await PrintReportAsync();
        _toolbarPanel.Controls.Add(_printButton);

        _mainSplitContainer.Panel1.Controls.Add(_toolbarPanel);

        // Bottom panel: Report viewer
        var viewerPanel = new Panel
        {
            Dock = DockStyle.Fill
        };

        // Initialize report viewer panel (placeholder for FastReport viewer)
        _reportViewer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = SystemColors.ControlLight,
            BorderStyle = BorderStyle.FixedSingle
        };

        // Add a label indicating that report viewer is not implemented
        var placeholderLabel = new Label
        {
            Text = "Report Viewer\r\n(Not implemented in FastReport OpenSource)\r\nUse Export buttons to view reports",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 12, FontStyle.Bold)
        };
        _reportViewer.Controls.Add(placeholderLabel);

        // Set the ReportViewer on the ViewModel (placeholder)
        _viewModel.ReportViewer = _reportViewer;

        viewerPanel.Controls.Add(_reportViewer);
        _mainSplitContainer.Panel2.Controls.Add(viewerPanel);

        Controls.Add(_mainSplitContainer);

        // Status strip
        _statusStrip = new StatusStrip { Dock = DockStyle.Bottom };
        _statusLabel = new ToolStripStatusLabel
        {
            Text = "Ready",
            Spring = true,
            TextAlign = ContentAlignment.MiddleLeft
        };
        _statusStrip.Items.Add(_statusLabel);
        Controls.Add(_statusStrip);

        // Loading overlay
        _loadingOverlay = new LoadingOverlay { Message = "Loading report..." };
        Controls.Add(_loadingOverlay);

        // No data overlay
        _noDataOverlay = new NoDataOverlay { Message = "No report loaded" };
        Controls.Add(_noDataOverlay);

        // Wire up ViewModel events
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;

        // Load available reports
        LoadAvailableReports();

        _logger.LogDebug("ReportsPanel controls initialized");
    }

    private void BindViewModel()
    {
        // ViewModel binding is handled through PropertyChanged events
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(_viewModel.IsLoading):
                if (_loadingOverlay != null) _loadingOverlay.Visible = _viewModel.IsLoading;
                if (_noDataOverlay != null) _noDataOverlay.Visible = !_viewModel.IsLoading && !_viewModel.HasReportLoaded;
                UpdateButtonStates();
                break;
            case nameof(_viewModel.HasReportLoaded):
                if (_noDataOverlay != null) _noDataOverlay.Visible = !_viewModel.HasReportLoaded && !_viewModel.IsLoading;
                UpdateButtonStates();
                break;
            case nameof(_viewModel.StatusMessage):
                if (_statusLabel != null) _statusLabel.Text = _viewModel.StatusMessage;
                break;
        }
    }

    private void UpdateButtonStates()
    {
        var hasReport = _viewModel.HasReportLoaded;
        var isLoading = _viewModel.IsLoading;

        if (_loadReportButton != null) _loadReportButton.Enabled = !isLoading && _reportSelector?.SelectedItem != null;
        if (_exportPdfButton != null) _exportPdfButton.Enabled = !isLoading && hasReport;
        if (_exportExcelButton != null) _exportExcelButton.Enabled = !isLoading && hasReport;
        if (_printButton != null) _printButton.Enabled = !isLoading && hasReport;
    }

    private void LoadAvailableReports()
    {
        try
        {
            var reportsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports");
            if (Directory.Exists(reportsDir))
            {
                var reportFiles = Directory.GetFiles(reportsDir, "*.frx", SearchOption.AllDirectories);
                _reportSelector?.Items.Clear();

                foreach (var reportFile in reportFiles)
                {
                    var relativePath = Path.GetRelativePath(reportsDir, reportFile);
                    _reportSelector?.Items.Add(relativePath);
                }

                if (_reportSelector?.Items.Count > 0)
                {
                    _reportSelector.SelectedIndex = 0;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load available reports");
            UpdateStatus("Failed to load reports list");
        }
    }

    private void ReportSelector_SelectedIndexChanged(object? sender, EventArgs e)
    {
        UpdateButtonStates();
    }

    private async Task LoadSelectedReportAsync()
    {
        if (_reportSelector?.SelectedItem is not string selectedReport)
            return;

        try
        {
            var reportsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports");
            var reportPath = Path.Combine(reportsDir, selectedReport);

            if (!File.Exists(reportPath))
            {
                UpdateStatus($"Report file not found: {selectedReport}");
                return;
            }

            UpdateStatus($"Loading report: {selectedReport}");
            await _viewModel.LoadReportAsync(reportPath);
            UpdateStatus($"Report loaded: {selectedReport}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load report {Report}", selectedReport);
            UpdateStatus($"Failed to load report: {ex.Message}");
        }
    }

    private async Task ExportToPdfAsync()
    {
        try
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "PDF files (*.pdf)|*.pdf",
                DefaultExt = "pdf",
                FileName = $"Report_{DateTime.Now:yyyyMMdd_HHmmss}.pdf"
            };

            if (saveDialog.ShowDialog() == DialogResult.OK)
            {
                UpdateStatus("Exporting to PDF...");
                await _viewModel.ExportToPdfAsync();
                UpdateStatus($"Exported to PDF: {Path.GetFileName(saveDialog.FileName)}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export to PDF");
            UpdateStatus($"PDF export failed: {ex.Message}");
        }
    }

    private async Task ExportToExcelAsync()
    {
        try
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "Excel files (*.xlsx)|*.xlsx",
                DefaultExt = "xlsx",
                FileName = $"Report_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
            };

            if (saveDialog.ShowDialog() == DialogResult.OK)
            {
                UpdateStatus("Exporting to Excel...");
                await _viewModel.ExportToExcelAsync();
                UpdateStatus($"Exported to Excel: {Path.GetFileName(saveDialog.FileName)}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export to Excel");
            UpdateStatus($"Excel export failed: {ex.Message}");
        }
    }

    private async Task PrintReportAsync()
    {
        try
        {
            UpdateStatus("Printing report...");
            await _viewModel.PrintReportAsync();
            UpdateStatus("Report sent to printer");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to print report");
            UpdateStatus($"Print failed: {ex.Message}");
        }
    }

    private async Task RefreshReportsAsync()
    {
        LoadAvailableReports();
        UpdateStatus("Reports list refreshed");
    }

    private void ClosePanel()
    {
        // Find parent form and locate DockingManager in its components
        var form = FindForm();
        if (form != null)
        {
            var dockingManager = FindDockingManager(form);
            if (dockingManager != null)
            {
                dockingManager.SetEnableDocking(this, false);
            }
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;

            _panelHeader?.Dispose();
            _reportViewer?.Dispose();
            _statusStrip?.Dispose();
            _loadingOverlay?.Dispose();
            _noDataOverlay?.Dispose();
            _toolbarPanel?.Dispose();
            _mainSplitContainer?.Dispose();
        }
        base.Dispose(disposing);
    }
}
