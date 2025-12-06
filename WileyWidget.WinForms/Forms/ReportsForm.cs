using System.Diagnostics.CodeAnalysis;
using System.Windows.Forms.Integration;
using BoldReports.UI.Xaml;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Input;
using Syncfusion.WinForms.Core;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Forms;

/// <summary>
/// Reports form displaying BoldReports ReportViewer with WinForms integration via ElementHost.
/// Supports loading RDL/RDLC reports, exporting to PDF/Excel, and parameter binding.
///
/// Architecture:
/// - ReportViewer: WPF control hosted via ElementHost (WPF interop)
/// - ViewModel: ReportsViewModel (MVVM via CommunityToolkit.Mvvm)
/// - Service: IBoldReportService (reflection-based for WPF/Services layer isolation)
/// </summary>
[SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "WinForms UI")]
public partial class ReportsForm : Form
{
    private readonly ReportsViewModel _viewModel;
    private readonly ILogger<ReportsForm> _logger;

    private ElementHost? _elementHost;
    private ReportViewer? _reportViewer;
    private ComboBox? _reportTypeCombo;
    private DateTimePicker? _fromDatePicker;
    private DateTimePicker? _toDatePicker;
    private Button? _generateButton;
    private Button? _exportPdfButton;
    private Button? _exportExcelButton;
    private Label? _statusLabel;

    public ReportsForm(ReportsViewModel viewModel, ILogger<ReportsForm> logger)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        SetupUI();
        BindViewModel();

        _logger.LogInformation("ReportsForm initialized");
    }

    /// <summary>
    /// Initialize UI components programmatically.
    /// </summary>
    private void SetupUI()
    {
        try
        {
            // === FORM SETTINGS ===
            Text = "Reports - Wiley Widget";
            Size = new Size(1400, 900);
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(1000, 600);
            FormBorderStyle = FormBorderStyle.Sizable;
            DoubleBuffered = true;

            // === TOOLBAR PANEL (Top) ===
            var toolbarPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Color.FromArgb(240, 240, 240),
                Padding = new Padding(10, 8, 10, 8)
            };

            // Report Type Combo
            var typeLabel = new Label
            {
                Text = "Report Type:",
                AutoSize = true,
                Location = new Point(10, 12)
            };
            _reportTypeCombo = new ComboBox
            {
                Location = new Point(90, 10),
                Size = new Size(200, 24),
                DataSource = ReportsViewModel.AvailableReportTypes.ToList(),
                SelectedItem = _viewModel.SelectedReportType,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _reportTypeCombo.SelectedIndexChanged += (s, e) =>
            {
                _viewModel.SelectedReportType = _reportTypeCombo.SelectedItem?.ToString() ?? "Budget Summary";
            };

            // From Date Picker
            var fromLabel = new Label
            {
                Text = "From:",
                AutoSize = true,
                Location = new Point(310, 12)
            };
            _fromDatePicker = new DateTimePicker
            {
                Location = new Point(345, 10),
                Size = new Size(120, 24),
                Value = _viewModel.FromDate,
                Format = DateTimePickerFormat.Short
            };
            _fromDatePicker.ValueChanged += (s, e) =>
            {
                _viewModel.FromDate = _fromDatePicker.Value;
            };

            // To Date Picker
            var toLabel = new Label
            {
                Text = "To:",
                AutoSize = true,
                Location = new Point(475, 12)
            };
            _toDatePicker = new DateTimePicker
            {
                Location = new Point(495, 10),
                Size = new Size(120, 24),
                Value = _viewModel.ToDate,
                Format = DateTimePickerFormat.Short
            };
            _toDatePicker.ValueChanged += (s, e) =>
            {
                _viewModel.ToDate = _toDatePicker.Value;
            };

            // Generate Button
            _generateButton = new Button
            {
                Text = "Generate",
                Location = new Point(630, 10),
                Size = new Size(90, 32),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _generateButton.Click += async (s, e) =>
            {
                _logger.LogInformation("Generate report button clicked");
                await _viewModel.GenerateReportCommand.ExecuteAsync(null);
            };

            // Export PDF Button
            _exportPdfButton = new Button
            {
                Text = "Export PDF",
                Location = new Point(730, 10),
                Size = new Size(90, 32),
                BackColor = Color.FromArgb(230, 126, 34),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _exportPdfButton.Click += async (s, e) =>
            {
                _logger.LogInformation("Export PDF button clicked");
                await _viewModel.ExportToPdfCommand.ExecuteAsync(null);
            };

            // Export Excel Button
            _exportExcelButton = new Button
            {
                Text = "Export Excel",
                Location = new Point(830, 10),
                Size = new Size(100, 32),
                BackColor = Color.FromArgb(46, 204, 113),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _exportExcelButton.Click += async (s, e) =>
            {
                _logger.LogInformation("Export Excel button clicked");
                await _viewModel.ExportToExcelCommand.ExecuteAsync(null);
            };

            // Status Label
            _statusLabel = new Label
            {
                Text = "Ready",
                AutoSize = true,
                Location = new Point(10, 45),
                ForeColor = Color.Green
            };

            toolbarPanel.Controls.AddRange(new Control[]
            {
                typeLabel, _reportTypeCombo,
                fromLabel, _fromDatePicker,
                toLabel, _toDatePicker,
                _generateButton, _exportPdfButton, _exportExcelButton,
                _statusLabel
            });

            Controls.Add(toolbarPanel);

            // === REPORT VIEWER PANEL (Main) ===
            var reportPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(5)
            };

            // Create WPF ReportViewer
            _reportViewer = new ReportViewer();

            // Host WPF control in WinForms via ElementHost
            _elementHost = new ElementHost
            {
                Dock = DockStyle.Fill,
                Child = _reportViewer
            };

            reportPanel.Controls.Add(_elementHost);
            Controls.Add(reportPanel);

            // Store reference to ReportViewer in ViewModel
            _viewModel.ReportViewer = _reportViewer;

            _logger.LogDebug("UI components initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize UI components");
            MessageBox.Show($"Failed to initialize reports form: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            throw;
        }
    }

    /// <summary>
    /// Bind ViewModel properties to UI using INotifyPropertyChanged.
    /// </summary>
    private void BindViewModel()
    {
        // Bind IsBusy to button states
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ReportsViewModel.IsBusy))
            {
                if (_generateButton != null) _generateButton.Enabled = !_viewModel.IsBusy;
                if (_exportPdfButton != null) _exportPdfButton.Enabled = !_viewModel.IsBusy;
                if (_exportExcelButton != null) _exportExcelButton.Enabled = !_viewModel.IsBusy;
            }
            else if (e.PropertyName == nameof(ReportsViewModel.ErrorMessage))
            {
                if (_statusLabel != null)
                {
                    if (!string.IsNullOrEmpty(_viewModel.ErrorMessage))
                    {
                        _statusLabel.Text = $"Error: {_viewModel.ErrorMessage}";
                        _statusLabel.ForeColor = Color.Red;
                    }
                }
            }
            else if (e.PropertyName == nameof(ReportsViewModel.StatusMessage))
            {
                if (_statusLabel != null)
                {
                    _statusLabel.Text = _viewModel.StatusMessage ?? "Ready";
                    _statusLabel.ForeColor = string.IsNullOrEmpty(_viewModel.ErrorMessage) ? Color.Green : Color.Red;
                }
            }
        };

        _logger.LogDebug("ViewModel binding established");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _elementHost?.Dispose();
            _reportTypeCombo?.Dispose();
            _fromDatePicker?.Dispose();
            _toDatePicker?.Dispose();
            _generateButton?.Dispose();
            _exportPdfButton?.Dispose();
            _exportExcelButton?.Dispose();
            _statusLabel?.Dispose();
        }

        base.Dispose(disposing);
    }
}
