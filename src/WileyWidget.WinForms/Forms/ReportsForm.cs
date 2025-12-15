using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using FastReport;
using Microsoft.Extensions.Logging;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Themes;

namespace WileyWidget.WinForms.Forms;

/// <summary>
/// Reports form for rendering and exporting reports.
/// This version keeps the viewer optional: if Microsoft Reporting Services is not available, a placeholder is shown.
/// </summary>
[SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "WinForms UI")]
public sealed class ReportsForm : Form
{
    private readonly ReportsViewModel _viewModel;
    private readonly ILogger<ReportsForm> _logger;
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    // UI elements
    private Control? _viewerHost;
    private Report? _reportViewer;
    private ComboBox? _reportTypeCombo;
    private DateTimePicker? _fromDatePicker;
    private DateTimePicker? _toDatePicker;
    private NumericUpDown? _pageSizeControl;
    private Button? _generateButton;
    private Button? _exportPdfButton;
    private Button? _exportExcelButton;
    private Button? _printButton;
    private Button? _toggleParamsButton;
    private Button? _findButton;
    private TextBox? _findTextBox;
    private ComboBox? _zoomCombo;
    private Label? _statusLabel;
    private Label? _pageInfoLabel;
    private DataGridView? _previewGrid;
    private bool _viewerAvailable;

    public ReportsForm(ReportsViewModel viewModel, ILogger<ReportsForm> logger, MainForm mainForm)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        if (mainForm == null)
        {
            throw new ArgumentNullException(nameof(mainForm));
        }

        // Only set MdiParent if MainForm is in MDI mode AND using MDI for child forms
        // In DockingManager mode, forms are shown as owned windows, not MDI children
        if (mainForm.IsMdiContainer && mainForm.UseMdiMode)
        {
            MdiParent = mainForm;
        }

        BuildLayout();
        ThemeColors.ApplyTheme(this);
        BindViewModel();
    }

    private void BuildLayout()
    {
        Name = "ReportsForm";
        Text = "Reports";
        MinimumSize = new System.Drawing.Size(900, 650);

        var toolbarFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(8),
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight
        };

        var typeLabel = new Label { Text = "Report", AutoSize = true, Margin = new Padding(0, 6, 4, 0) };
        _reportTypeCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
        _reportTypeCombo.Items.AddRange(ReportsViewModel.AvailableReportTypes);
        _reportTypeCombo.DataBindings.Add(new Binding("SelectedItem", _viewModel, nameof(ReportsViewModel.SelectedReportType), true, DataSourceUpdateMode.OnPropertyChanged));

        var fromLabel = new Label { Text = "From", AutoSize = true, Margin = new Padding(12, 6, 4, 0) };
        _fromDatePicker = new DateTimePicker { Width = 130, Format = DateTimePickerFormat.Short };
        _fromDatePicker.DataBindings.Add(new Binding("Value", _viewModel, nameof(ReportsViewModel.FromDate), true, DataSourceUpdateMode.OnPropertyChanged));

        var toLabel = new Label { Text = "To", AutoSize = true, Margin = new Padding(12, 6, 4, 0) };
        _toDatePicker = new DateTimePicker { Width = 130, Format = DateTimePickerFormat.Short };
        _toDatePicker.DataBindings.Add(new Binding("Value", _viewModel, nameof(ReportsViewModel.ToDate), true, DataSourceUpdateMode.OnPropertyChanged));

        _generateButton = new Button { Text = "Generate", AutoSize = true, Margin = new Padding(12, 3, 4, 3) };
        _exportPdfButton = new Button { Text = "Export PDF", AutoSize = true };
        _exportExcelButton = new Button { Text = "Export Excel", AutoSize = true };
        _printButton = new Button { Text = "Print", AutoSize = true };
        _toggleParamsButton = new Button { Text = "Params", AutoSize = true };
        _findButton = new Button { Text = "Find", AutoSize = true };
        _findTextBox = new TextBox { Width = 160 };
        _zoomCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
        _zoomCombo.Items.AddRange(new object[] { "50%", "75%", "100%", "125%", "150%", "200%" });
        _zoomCombo.SelectedItem = "100%";

        _statusLabel = new Label { Text = "Ready", AutoSize = true, Margin = new Padding(12, 6, 0, 0) };

        _generateButton.Click += async (s, e) =>
        {
            if (!EnsureViewer()) return;
            await _viewModel.GenerateReportAsync(_cts.Token);
        };
        _exportPdfButton.Click += async (s, e) =>
        {
            if (!EnsureViewer()) return;
            await _viewModel.ExportToPdfAsync(_cts.Token);
        };
        _exportExcelButton.Click += async (s, e) =>
        {
            if (!EnsureViewer()) return;
            await _viewModel.ExportToExcelAsync(_cts.Token);
        };
        _printButton.Click += async (s, e) =>
        {
            if (!EnsureViewer()) return;
            await _viewModel.PrintAsync(_cts.Token);
        };
        _toggleParamsButton.Click += async (s, e) =>
        {
            if (!EnsureViewer()) return;
            await _viewModel.ToggleParametersPanelAsync(_cts.Token);
        };
        _findButton.Click += async (s, e) =>
        {
            if (!EnsureViewer()) return;
            await _viewModel.FindAsync(_cts.Token);
        };
        _zoomCombo.SelectedIndexChanged += async (s, e) =>
        {
            if (_zoomCombo?.SelectedItem is string value && int.TryParse(value.TrimEnd('%'), out var pct))
            {
                await _viewModel.SetZoomAsync(pct, _cts.Token);
            }
        };

        toolbarFlow.Controls.AddRange(new Control[]
        {
            typeLabel, _reportTypeCombo,
            fromLabel, _fromDatePicker,
            toLabel, _toDatePicker,
            _generateButton, _exportPdfButton, _exportExcelButton, _printButton,
            _zoomCombo, _findTextBox, _findButton, _toggleParamsButton,
            _statusLabel
        });

        Controls.Add(toolbarFlow);

        // Viewer placeholder (Microsoft Reporting Services optional)
        _viewerHost = BuildViewerHost();
        _viewerHost.Dock = DockStyle.Fill;
        _viewModel.ReportViewer = _reportViewer;

        _previewGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AccessibleName = "Report Preview",
            AccessibleDescription = "Shows a preview of the report data"
        };

        _pageInfoLabel = new Label { AutoSize = true, Margin = new Padding(6, 8, 6, 0) };
        _pageSizeControl = new NumericUpDown
        {
            Minimum = 5,
            Maximum = 500,
            Value = _viewModel.PageSize,
            Width = 80
        };
        _pageSizeControl.ValueChanged += (s, e) =>
        {
            _viewModel.PageSize = (int)_pageSizeControl.Value;
            _viewModel.CurrentPage = 1;
            RefreshPreviewGrid();
        };

        var previewToolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(6)
        };
        previewToolbar.Controls.Add(new Label { Text = "Page Size", AutoSize = true, Margin = new Padding(0, 6, 4, 0) });
        previewToolbar.Controls.Add(_pageSizeControl);
        previewToolbar.Controls.Add(_pageInfoLabel);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 380,
            Panel1MinSize = 300,
            Panel2MinSize = 150
        };

        split.Panel1.Controls.Add(_viewerHost);
        split.Panel2.Controls.Add(_previewGrid);
        split.Panel2.Controls.Add(previewToolbar);

        Controls.Add(split);
    }

    private void BindViewModel()
    {
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        RefreshPreviewGrid();
        UpdateStatus();
        UpdateCommandAvailability();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ReportsViewModel.IsBusy):
                SetBusyState(!_viewModel.IsBusy);
                UpdateCommandAvailability();
                break;
            case nameof(ReportsViewModel.ErrorMessage):
            case nameof(ReportsViewModel.StatusMessage):
                UpdateStatus();
                break;
            case nameof(ReportsViewModel.PreviewData):
            case nameof(ReportsViewModel.CurrentPage):
            case nameof(ReportsViewModel.PageSize):
                RefreshPreviewGrid();
                break;
        }
    }

    private void SetBusyState(bool enabled)
    {
        var allowCommands = enabled && _viewerAvailable;
        _generateButton?.SetEnabledSafe(allowCommands);
        _exportPdfButton?.SetEnabledSafe(allowCommands);
        _exportExcelButton?.SetEnabledSafe(allowCommands);
        _printButton?.SetEnabledSafe(allowCommands);
    }

    private void UpdateStatus()
    {
        if (_statusLabel == null) return;
        var hasError = !string.IsNullOrEmpty(_viewModel.ErrorMessage);
        _statusLabel.Text = hasError ? $"Error: {_viewModel.ErrorMessage}" : _viewModel.StatusMessage ?? "Ready";
        _statusLabel.ForeColor = hasError ? ThemeColors.Error : ThemeColors.Success;
    }

    private void UpdateCommandAvailability()
    {
        if (_statusLabel == null)
        {
            return;
        }

        if (!_viewerAvailable)
        {
            _statusLabel.Text = "Report viewer not available. Install Microsoft Reporting Services to enable generation.";
            _statusLabel.ForeColor = ThemeColors.Warning;
        }
    }

    private void RefreshPreviewGrid()
    {
        try
        {
            if (_previewGrid == null) return;

            if (InvokeRequired)
            {
                Invoke(new Action(RefreshPreviewGrid));
                return;
            }

            var list = _viewModel.PreviewData?.ToList() ?? new System.Collections.Generic.List<ReportDataItem>();
            var binding = new BindingSource
            {
                DataSource = list.Select(p => new { p.Name, p.Value, p.Category }).ToList()
            };
            _previewGrid.DataSource = binding;

            if (_pageInfoLabel != null)
            {
                _pageInfoLabel.Text = $"Page: {_viewModel.CurrentPage} | Page Size: {_viewModel.PageSize} | Rows: {list.Count}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh preview grid");
        }
    }

    private static Control BuildPlaceholder(string text)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 10F),
            BackColor = ThemeColors.Background
        };
    }

    private Control BuildViewerHost()
    {
        try
        {
            // Create FastReport WinForms ReportViewer
            // Note: FastReport.OpenSource doesn't include a ReportViewer control
            // TODO: Implement report viewing using FastReport.Report directly or upgrade to FastReport.Net
            // _reportViewer = new ReportViewer { Dock = DockStyle.Fill };
            _reportViewer = null;

            _viewerAvailable = false;
            _statusLabel?.SetEnabledSafe(false);
            _logger.LogInformation("FastReport Open Source viewer not available - ReportViewer control requires FastReport.Net");
            return BuildPlaceholder("Report viewer requires FastReport.Net. FastReport.OpenSource only supports report generation, not viewing.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize ReportViewer");
        }

        _reportViewer = null;
        _viewerAvailable = false;
        return BuildPlaceholder("ReportViewer not available. Install FastReport.OpenSource to enable report rendering.");
    }

    private bool EnsureViewer()
    {
        if (_viewerAvailable && _reportViewer != null)
        {
            return true;
        }

        UpdateCommandAvailability();
        MessageBox.Show(this, "ReportViewer is not available. Install FastReport.OpenSource to generate or export reports.", "Viewer Not Available", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _cts.Cancel();
            _cts.Dispose();

            _viewerHost?.Dispose();
            _reportTypeCombo?.Dispose();
            _fromDatePicker?.Dispose();
            _toDatePicker?.Dispose();
            _pageSizeControl?.Dispose();
            _generateButton?.Dispose();
            _exportPdfButton?.Dispose();
            _exportExcelButton?.Dispose();
            _printButton?.Dispose();
            _toggleParamsButton?.Dispose();
            _findButton?.Dispose();
            _findTextBox?.Dispose();
            _zoomCombo?.Dispose();
            _statusLabel?.Dispose();
            _pageInfoLabel?.Dispose();
            _previewGrid?.Dispose();
        }

        _disposed = true;
        base.Dispose(disposing);
    }
}

internal static class ControlExtensions
{
    public static void SetEnabledSafe(this Control control, bool enabled)
    {
        if (control.InvokeRequired)
        {
            control.Invoke(new Action<Control, bool>(SetEnabledSafe), control, enabled);
        }
        else
        {
            control.Enabled = enabled;
        }
    }
}
