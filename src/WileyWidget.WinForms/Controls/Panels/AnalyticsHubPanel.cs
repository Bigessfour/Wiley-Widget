using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
using Syncfusion.Windows.Forms.Tools;
using SfButton = Syncfusion.WinForms.Controls.SfButton;
using SfComboBox = Syncfusion.WinForms.ListView.SfComboBox;
using TextBoxExt = Syncfusion.Windows.Forms.Tools.TextBoxExt;
using TabControlAdv = Syncfusion.Windows.Forms.Tools.TabControlAdv;
using TabPageAdv = Syncfusion.Windows.Forms.Tools.TabPageAdv;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Supporting;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Controls.Panels;

/// <summary>
/// Analytics Hub v2.1 - unified tabbed interface fully wired to AnalyticsHubViewModel.
/// Consolidates Overview, Trends & Forecasts, Scenarios, and Variances tabs.
/// </summary>
[SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
public partial class AnalyticsHubPanel : ScopedPanelBase<AnalyticsHubViewModel>
{
    // Chrome
    private PanelHeader? _panelHeader;
    private LegacyGradientPanel? _filtersPanel;
    private TabControlAdv? _tabControl;
    private StatusStrip? _statusStrip;
    private ToolStripStatusLabel? _statusLabel;
    private ToolStripStatusLabel? _recordCountLabel;
    private ToolStripStatusLabel? _lastRefreshLabel;
    private ErrorProvider? _errorProvider;

    // Global filter controls
    private SfComboBox? _fiscalYearComboBox;
    private TextBoxExt? _searchTextBox;
    private SfButton? _refreshButton;
    private SfButton? _exportButton;

    // Overlays
    private LoadingOverlay? _loadingOverlay;
    private NoDataOverlay? _noDataOverlay;

    // Lazy-loaded tab views
    private OverviewTabControl? _overviewTab;
    private TrendsTabControl? _trendsTab;
    private ScenariosTabControl? _scenariosTab;
    private VariancesTabControl? _variancesTab;

    // State
    private DateTimeOffset? _lastRefreshAt;

    // Stored event handlers (v2.1 naming) for clean unsubscription
    private EventHandler? _refreshClicked;
    private EventHandler? _closeClicked;
    private EventHandler? _fyChanged;
    private EventHandler? _searchChanged;
    private EventHandler? _tabChanged;
    private PropertyChangedEventHandler? _vmChanged;

    /// <summary>Initializes a new instance of the AnalyticsHubPanel class.</summary>
    public AnalyticsHubPanel(
        IServiceScopeFactory scopeFactory,
        ILogger<ScopedPanelBase<AnalyticsHubViewModel>> logger)
        : base(scopeFactory, logger)
    {
        Size = new Size(1400, 900);
        MinimumSize = new Size(1200, 700);
        Dock = DockStyle.Fill;
        InitializeControls();
    }

    // -------------------------------------------------------------------------
    // ViewModel resolution - typed override provided by ScopedPanelBase<T>
    // -------------------------------------------------------------------------
    // Control construction
    // -------------------------------------------------------------------------

    private void InitializeControls()
    {
        SuspendLayout();
        SfSkinManager.SetVisualStyle(this, SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme);

        _panelHeader = new PanelHeader { Title = "Analytics Hub" };
        _refreshClicked = async (s, e) => await (ViewModel?.RefreshAllCommand.ExecuteAsync(null) ?? Task.CompletedTask);
        _panelHeader.RefreshClicked += _refreshClicked;
        _closeClicked = (s, e) => ClosePanel();
        _panelHeader.CloseClicked += _closeClicked;
        Controls.Add(_panelHeader);

        InitializeGlobalFilters();
        InitializeTabControl();
        InitializeStatusStrip();
        InitializeOverlays();

        _errorProvider = new ErrorProvider { BlinkStyle = ErrorBlinkStyle.NeverBlink, BlinkRate = 0 };

        ApplyThemeToControls(SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme);

        ResumeLayout(false);
        PerformLayout();
    }

    private void InitializeGlobalFilters()
    {
        var themeName = SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme;

        _filtersPanel = new LegacyGradientPanel
        {
            Dock = DockStyle.Top,
            Height = 52,
            Padding = new Padding(12, 8, 12, 8),
            BorderStyle = BorderStyle.None
        };
        SfSkinManager.SetVisualStyle(_filtersPanel, themeName);
        _filtersPanel.ThemeName = themeName;

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 1,
            AutoSize = false
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));

        table.Controls.Add(
            new Label { Text = "Fiscal Year:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 0);

        _fiscalYearComboBox = ControlFactory.CreateSfComboBox(c =>
        {
            c.Dock = DockStyle.Fill;
            c.AllowNull = false;
        });
        _fyChanged = FiscalYear_SelectedIndexChanged;
        _fiscalYearComboBox.SelectedIndexChanged += _fyChanged;
        table.Controls.Add(_fiscalYearComboBox, 1, 0);

        table.Controls.Add(
            new Label { Text = "Search:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 2, 0);

        _searchTextBox = ControlFactory.CreateTextBoxExt(t => t.Dock = DockStyle.Fill);
        _searchChanged = (s, e) =>
        {
            if (ViewModel != null)
                ViewModel.SearchText = _searchTextBox?.Text ?? string.Empty;
        };
        _searchTextBox.TextChanged += _searchChanged;
        table.Controls.Add(_searchTextBox, 3, 0);

        var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, AutoSize = false };
        _refreshButton = ControlFactory.CreateSfButton("Refresh All", b => b.Width = 110);
        _refreshButton.Click += async (s, e) => await (ViewModel?.RefreshAllCommand.ExecuteAsync(null) ?? Task.CompletedTask);
        _exportButton = ControlFactory.CreateSfButton("Export", b => b.Width = 90);
        _exportButton.Click += (s, e) => ExportHub();
        btnPanel.Controls.AddRange(new Control[] { _refreshButton, _exportButton });
        table.Controls.Add(btnPanel, 4, 0);

        _filtersPanel.Controls.Add(table);
        Controls.Add(_filtersPanel);
    }

    private void InitializeTabControl()
    {
        _tabControl = ControlFactory.CreateTabControlAdv(t =>
        {
            t.Dock = DockStyle.Fill;
            t.Multiline = true;
        });

        _overviewTab  = new OverviewTabControl(ViewModel?.Overview, Logger) { Dock = DockStyle.Fill };
        _trendsTab    = new TrendsTabControl(ViewModel?.Trends, Logger)                { Dock = DockStyle.Fill };
        _scenariosTab = new ScenariosTabControl(ViewModel?.Scenarios, Logger)          { Dock = DockStyle.Fill };
        _variancesTab = new VariancesTabControl(ViewModel?.Variances, Logger)          { Dock = DockStyle.Fill };

        _tabControl.TabPages.AddRange(new[]
        {
            CreateTabPage("Overview",           _overviewTab),
            CreateTabPage("Trends & Forecasts", _trendsTab),
            CreateTabPage("Scenarios",          _scenariosTab),
            CreateTabPage("Variances",          _variancesTab)
        });

        _tabChanged = async (s, e) => await LoadCurrentTabAsync(CancellationToken.None);
        _tabControl.SelectedIndexChanged += _tabChanged;

        Controls.Add(_tabControl);
    }

    private static TabPageAdv CreateTabPage(string text, Control content)
    {
        var page = new TabPageAdv { Text = text };
        page.Controls.Add(content);
        return page;
    }

    private void InitializeStatusStrip()
    {
        _statusStrip      = new StatusStrip { Dock = DockStyle.Bottom };
        _statusLabel      = new ToolStripStatusLabel { Text = "Ready",             Spring = true };
        _recordCountLabel = new ToolStripStatusLabel { Text = "Records: 0" };
        _lastRefreshLabel = new ToolStripStatusLabel { Text = "Last refresh: Never" };
        _statusStrip.Items.AddRange(new ToolStripItem[] { _statusLabel, _recordCountLabel, _lastRefreshLabel });
        Controls.Add(_statusStrip);
    }

    private void InitializeOverlays()
    {
        _loadingOverlay = new LoadingOverlay
        {
            Message = "Loading analytics data...",
            Dock = DockStyle.Fill,
            Visible = false
        };
        Controls.Add(_loadingOverlay);
        _loadingOverlay.BringToFront();

        _noDataOverlay = new NoDataOverlay
        {
            Message = "No analytics data available\r\nPerform budget operations to generate insights",
            Dock = DockStyle.Fill,
            Visible = false
        };
        Controls.Add(_noDataOverlay);
        _noDataOverlay.BringToFront();
    }

    // -------------------------------------------------------------------------
    // ViewModel wiring (merged Subscribe + BindInitialData)
    // -------------------------------------------------------------------------

    private void WireViewModel()
    {
        if (ViewModel == null) return;

        _vmChanged = (s, e) =>
        {
            if (e.PropertyName == nameof(ViewModel.FiscalYears))
                InvokeOnUiThread(BindFiscalYears);
            else if (e.PropertyName == nameof(ViewModel.SelectedFiscalYear))
                InvokeOnUiThread(UpdateSelectedFiscalYear);
        };
        ViewModel.PropertyChanged += _vmChanged;

        BindFiscalYears();
        UpdateSelectedFiscalYear();
        UpdateRecordCount();
        UpdateLastRefreshLabel();
    }

    private void BindFiscalYears()
    {
        if (_fiscalYearComboBox == null || ViewModel?.FiscalYears == null) return;
        try
        {
            var years = ViewModel.FiscalYears.OrderBy(y => y).ToList();
            if (years.Count == 0) { _fiscalYearComboBox.DataSource = null; return; }
            _fiscalYearComboBox.DataSource = years;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "AnalyticsHubPanel: error binding fiscal years");
        }
    }

    private void UpdateSelectedFiscalYear()
    {
        if (_fiscalYearComboBox == null || ViewModel == null) return;
        try
        {
            if (_fiscalYearComboBox.DataSource is not List<int> years || years.Count == 0) return;
            var idx = years.IndexOf(ViewModel.SelectedFiscalYear);
            _fiscalYearComboBox.SelectedIndex = idx >= 0 ? idx : 0;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "AnalyticsHubPanel: error updating selected fiscal year");
        }
    }

    private void FiscalYear_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_fiscalYearComboBox == null || ViewModel == null) return;
        if (_fiscalYearComboBox.SelectedItem is int year && year != ViewModel.SelectedFiscalYear)
            ViewModel.SelectedFiscalYear = year;
    }

    // -------------------------------------------------------------------------
    // ICompletablePanel implementation
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public override async Task LoadAsync(CancellationToken ct)
    {
        if (IsLoaded) return;
        ct.ThrowIfCancellationRequested();

        try
        {
            IsBusy = true;
            ShowLoadingOverlay("Loading Analytics Hub...");
            HideNoDataOverlay();
            UpdateStatus("Loading Analytics Hub...");

            if (ViewModel != null && !DesignMode)
                await ViewModel.RefreshAllCommand.ExecuteAsync(null);

            ct.ThrowIfCancellationRequested();
            await LoadCurrentTabAsync(ct);

            _lastRefreshAt = DateTimeOffset.Now;
            UpdateLastRefreshLabel();
            UpdateRecordCount();
            UpdateNoDataOverlayVisibility();

            _logger?.LogDebug("AnalyticsHubPanel loaded successfully");
            UpdateStatus("Ready");
            IsLoaded = true;
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("AnalyticsHubPanel load cancelled");
            UpdateStatus("Load cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load AnalyticsHubPanel");
            UpdateStatus("Error loading data");
            ShowErrorOverlay("Failed to load analytics data. Check logs for details.");
        }
        finally
        {
            IsBusy = false;
            HideLoadingOverlay();
        }
    }

    /// <inheritdoc/>
    public override Task SaveAsync(CancellationToken ct)
    {
        _logger?.LogDebug("AnalyticsHubPanel SaveAsync (read-only panel)");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override async Task<ValidationResult> ValidateAsync(CancellationToken ct)
    {
        var errors = new List<ValidationItem>();

        if (ViewModel == null)
        {
            errors.Add(new ValidationItem("ViewModel", "ViewModel not initialized", ValidationSeverity.Error));
        }
        else
        {
            if (ViewModel.SelectedFiscalYear == 0)
                errors.Add(new ValidationItem("FiscalYear", "Please select a fiscal year", ValidationSeverity.Warning));

            if (_tabControl != null)
            {
                switch (_tabControl.SelectedIndex)
                {
                    case 0 when ViewModel.Overview == null:
                        errors.Add(new ValidationItem("Overview", "Overview data not loaded", ValidationSeverity.Warning)); break;
                    case 1 when ViewModel.Trends == null:
                        errors.Add(new ValidationItem("Trends", "Trends data not loaded", ValidationSeverity.Warning)); break;
                    case 2 when ViewModel.Scenarios == null:
                        errors.Add(new ValidationItem("Scenarios", "Scenarios data not loaded", ValidationSeverity.Warning)); break;
                    case 3 when ViewModel.Variances == null:
                        errors.Add(new ValidationItem("Variances", "Variances data not loaded", ValidationSeverity.Warning)); break;
                }
            }
        }

        return new ValidationResult(errors.Count == 0, errors.ToArray());
    }

    // -------------------------------------------------------------------------
    // Tab loading (lazy)
    // -------------------------------------------------------------------------

    /// <summary>Loads the currently visible tab if not already loaded.</summary>
    protected virtual async Task LoadCurrentTabAsync(CancellationToken ct)
    {
        if (_tabControl == null || ViewModel == null) return;

        var overlayShown = false;
        try
        {
            ct.ThrowIfCancellationRequested();
            UpdateStatus("Loading tab data...");

            if (_loadingOverlay != null && !_loadingOverlay.Visible)
            {
                ShowLoadingOverlay("Loading tab data...");
                overlayShown = true;
            }

            switch (_tabControl.SelectedIndex)
            {
                case 0 when _overviewTab != null && !_overviewTab.IsLoaded:
                    await _overviewTab.LoadAsync(); break;
                case 1 when _trendsTab != null && !_trendsTab.IsLoaded:
                    await _trendsTab.LoadAsync(); break;
                case 2 when _scenariosTab != null && !_scenariosTab.IsLoaded:
                    await _scenariosTab.LoadAsync(); break;
                case 3 when _variancesTab != null && !_variancesTab.IsLoaded:
                    await _variancesTab.LoadAsync(); break;
            }

            UpdateRecordCount();
            UpdateNoDataOverlayVisibility();
            UpdateStatus("Ready");
        }
        catch (OperationCanceledException) { UpdateStatus("Load cancelled"); }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load current tab");
            UpdateStatus("Error loading tab");
        }
        finally
        {
            if (overlayShown) HideLoadingOverlay();
        }
    }

    // -------------------------------------------------------------------------
    // Theme
    // -------------------------------------------------------------------------

    private void ApplyThemeToControls(string themeName)
    {
        void Apply(Control? c) { if (c != null) SfSkinManager.SetVisualStyle(c, themeName); }

        Apply(_filtersPanel);
        if (_filtersPanel != null) _filtersPanel.ThemeName = themeName;
        Apply(_fiscalYearComboBox);
        if (_fiscalYearComboBox != null) _fiscalYearComboBox.ThemeName = themeName;
        Apply(_searchTextBox);
        if (_searchTextBox != null) _searchTextBox.ThemeName = themeName;
        Apply(_refreshButton);
        if (_refreshButton != null) _refreshButton.ThemeName = themeName;
        Apply(_exportButton);
        if (_exportButton != null) _exportButton.ThemeName = themeName;
        Apply(_panelHeader);
        Apply(_tabControl);
        if (_tabControl != null)
            foreach (TabPageAdv page in _tabControl.TabPages)
                Apply(page);
        Apply(_loadingOverlay);
        if (_loadingOverlay != null) _loadingOverlay.ThemeName = themeName;
        Apply(_noDataOverlay);
        if (_noDataOverlay != null) _noDataOverlay.ThemeName = themeName;
        Apply(_overviewTab);
        Apply(_trendsTab);
        Apply(_scenariosTab);
        Apply(_variancesTab);
    }

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        if (!IsLoaded && !DesignMode)
            _ = LoadAsync(CancellationToken.None);
    }

    // -------------------------------------------------------------------------
    // Status and overlays (thread-safe)
    // -------------------------------------------------------------------------

    private void UpdateStatus(string message)
    {
        if (_statusLabel == null) return;
        if (InvokeRequired) Invoke(() => _statusLabel.Text = message);
        else _statusLabel.Text = message;
    }

    private void UpdateRecordCount()
    {
        if (_recordCountLabel == null || ViewModel == null) return;
        var total = (ViewModel.Overview?.Metrics?.Count ?? 0) + (ViewModel.Overview?.Kpis?.Count ?? 0);
        if (InvokeRequired) Invoke(() => _recordCountLabel.Text = $"Records: {total}");
        else _recordCountLabel.Text = $"Records: {total}";
    }

    private void UpdateLastRefreshLabel()
    {
        if (_lastRefreshLabel == null) return;
        var text = _lastRefreshAt.HasValue
            ? $"Last refresh: {_lastRefreshAt.Value:yyyy-MM-dd HH:mm}"
            : "Last refresh: Never";
        if (InvokeRequired) Invoke(() => _lastRefreshLabel.Text = text);
        else _lastRefreshLabel.Text = text;
    }

    private void ShowLoadingOverlay(string message)
    {
        if (_loadingOverlay == null) return;
        if (InvokeRequired)
        {
            Invoke(() =>
            {
                _loadingOverlay.Message = message;
                _loadingOverlay.Visible = true;
                _loadingOverlay.BringToFront();
            });
        }
        else
        {
            _loadingOverlay.Message = message;
            _loadingOverlay.Visible = true;
            _loadingOverlay.BringToFront();
        }
    }

    private void HideLoadingOverlay()
    {
        if (_loadingOverlay == null) return;
        if (InvokeRequired) Invoke(() => _loadingOverlay.Visible = false);
        else _loadingOverlay.Visible = false;
    }

    private void HideNoDataOverlay()
    {
        if (_noDataOverlay == null) return;
        if (InvokeRequired) Invoke(() => _noDataOverlay.Visible = false);
        else _noDataOverlay.Visible = false;
    }

    private void ShowNoDataOverlay(string message)
    {
        if (_noDataOverlay == null) return;
        if (InvokeRequired)
        {
            Invoke(() =>
            {
                _noDataOverlay.Message = message;
                _noDataOverlay.Visible = true;
                _noDataOverlay.BringToFront();
            });
        }
        else
        {
            _noDataOverlay.Message = message;
            _noDataOverlay.Visible = true;
            _noDataOverlay.BringToFront();
        }
    }

    private void UpdateNoDataOverlayVisibility()
    {
        if (ViewModel == null || _tabControl == null)
        {
            ShowNoDataOverlay("No analytics data available");
            return;
        }

        var hasData = _tabControl.SelectedIndex switch
        {
            0 => (ViewModel.Overview?.Metrics?.Count ?? 0) > 0 || (ViewModel.Overview?.Kpis?.Count ?? 0) > 0,
            1 => (ViewModel.Trends?.TrendData?.Count ?? 0) > 0 || (ViewModel.Trends?.ForecastData?.Count ?? 0) > 0,
            2 => (ViewModel.Scenarios?.ScenarioResults?.Count ?? 0) > 0,
            3 => (ViewModel.Variances?.Variances?.Count ?? 0) > 0,
            _ => false
        };

        if (hasData)
            HideNoDataOverlay();
        else
            ShowNoDataOverlay($"No {GetCurrentTabName().ToLower(CultureInfo.InvariantCulture)} data available");
    }

    private static void ShowErrorOverlay(string message) =>
        MessageBox.Show(message, "Analytics Hub", MessageBoxButtons.OK, MessageBoxIcon.Error);

    // -------------------------------------------------------------------------
    // Export (CSV implemented locally - no ViewModel dependency)
    // -------------------------------------------------------------------------

    private void ExportHub()
    {
        if (IsBusy || ViewModel == null || _tabControl == null) return;
        _ = ExportHubAsync(CancellationToken.None);
    }

    private async Task ExportHubAsync(CancellationToken ct)
    {
        var tabName = GetCurrentTabName();
        using var dlg = new SaveFileDialog
        {
            Filter = "CSV Files|*.csv",
            Title = $"Export {tabName}",
            FileName = $"Analytics_{tabName}_{DateTime.Now:yyyyMMdd_HHmm}.csv"
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        try
        {
            ct.ThrowIfCancellationRequested();
            IsBusy = true;
            ShowLoadingOverlay($"Exporting {tabName}...");
            UpdateStatus($"Exporting {tabName}...");

            await ExportCurrentTabToCsvAsync(dlg.FileName, ct);

            UpdateStatus("Export completed successfully");
            MessageBox.Show(
                $"Export completed:\n{dlg.FileName}",
                "Export Successful", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException) { UpdateStatus("Export cancelled"); }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to export analytics data");
            UpdateStatus("Export failed");
            ShowErrorOverlay($"Export failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            HideLoadingOverlay();
        }
    }

    private string GetCurrentTabName() => _tabControl?.SelectedIndex switch
    {
        0 => "Overview", 1 => "Trends", 2 => "Scenarios", 3 => "Variances", _ => "Analytics"
    };

    private async Task ExportCurrentTabToCsvAsync(string filePath, CancellationToken ct)
    {
        if (ViewModel == null) throw new InvalidOperationException("Analytics data not available.");
        await using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
        switch (_tabControl?.SelectedIndex)
        {
            case 0:  await WriteOverviewCsvAsync(writer, ct);  break;
            case 1:  await WriteTrendsCsvAsync(writer, ct);    break;
            case 2:  await WriteScenariosCsvAsync(writer, ct); break;
            case 3:  await WriteVariancesCsvAsync(writer, ct); break;
            default: await WriteOverviewCsvAsync(writer, ct);  break;
        }
    }

    private async Task WriteOverviewCsvAsync(StreamWriter w, CancellationToken ct)
    {
        if (ViewModel?.Overview == null) return;
        var ov = ViewModel.Overview;
        await w.WriteLineAsync("Section,Name,Value,Department,BudgetedAmount,Amount,Variance,VariancePercent,IsOverBudget,Format,IsPositive");

        foreach (var (name, value) in new[]
        {
            ("Total Budget",       ov.TotalBudget),
            ("Total Actual",       ov.TotalActual),
            ("Total Variance",     ov.TotalVariance),
            ("Over Budget Count",  (decimal)ov.OverBudgetCount),
            ("Under Budget Count", (decimal)ov.UnderBudgetCount)
        })
        {
            ct.ThrowIfCancellationRequested();
            await w.WriteLineAsync(CsvRow("Summary", name, value.ToString(CultureInfo.InvariantCulture), "", "", "", "", "", "", "", ""));
        }

        if (ov.Kpis != null)
            foreach (var k in ov.Kpis)
            {
                ct.ThrowIfCancellationRequested();
                await w.WriteLineAsync(CsvRow("KPI", k.Title, k.Value.ToString(CultureInfo.InvariantCulture),
                    "", "", "", "", "", "", k.Format, k.IsPositive.ToString(CultureInfo.InvariantCulture)));
            }

        if (ov.Metrics != null)
            foreach (var m in ov.Metrics)
            {
                ct.ThrowIfCancellationRequested();
                await w.WriteLineAsync(CsvRow("Metric", m.Name, m.Value.ToString(CultureInfo.InvariantCulture),
                    m.DepartmentName, m.BudgetedAmount.ToString(CultureInfo.InvariantCulture),
                    m.Amount.ToString(CultureInfo.InvariantCulture), m.Variance.ToString(CultureInfo.InvariantCulture),
                    m.VariancePercent.ToString(CultureInfo.InvariantCulture), m.IsOverBudget.ToString(CultureInfo.InvariantCulture), "", ""));
            }
    }

    private async Task WriteTrendsCsvAsync(StreamWriter w, CancellationToken ct)
    {
        if (ViewModel?.Trends == null) return;
        var t = ViewModel.Trends;
        await w.WriteLineAsync("Section,Series,Date,Value,PredictedReserves,ConfidenceInterval,Department,AvgVariancePct,TotalBudgeted,TotalActual,Count");

        if (t.TrendData != null)
            foreach (var series in t.TrendData)
                foreach (var pt in series.Points)
                {
                    ct.ThrowIfCancellationRequested();
                    await w.WriteLineAsync(CsvRow("Trend", series.Name,
                        pt.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        pt.Value.ToString(CultureInfo.InvariantCulture), "", "", "", "", "", "", ""));
                }

        if (t.ForecastData != null)
            foreach (var f in t.ForecastData)
            {
                ct.ThrowIfCancellationRequested();
                await w.WriteLineAsync(CsvRow("Forecast", "",
                    f.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), "",
                    f.PredictedReserves.ToString(CultureInfo.InvariantCulture),
                    f.ConfidenceInterval.ToString(CultureInfo.InvariantCulture), "", "", "", "", ""));
            }

        if (t.DepartmentVariances != null)
            foreach (var d in t.DepartmentVariances)
            {
                ct.ThrowIfCancellationRequested();
                await w.WriteLineAsync(CsvRow("Department", "", "", "", "", "", d.Department,
                    d.AverageVariancePercent.ToString(CultureInfo.InvariantCulture),
                    d.TotalBudgeted.ToString(CultureInfo.InvariantCulture),
                    d.TotalActual.ToString(CultureInfo.InvariantCulture),
                    d.Count.ToString(CultureInfo.InvariantCulture)));
            }
    }

    private async Task WriteScenariosCsvAsync(StreamWriter w, CancellationToken ct)
    {
        if (ViewModel?.Scenarios?.ScenarioResults == null) return;
        await w.WriteLineAsync("Description,ProjectedValue,Variance");
        foreach (var r in ViewModel.Scenarios.ScenarioResults)
        {
            ct.ThrowIfCancellationRequested();
            await w.WriteLineAsync(CsvRow(r.Description,
                r.ProjectedValue.ToString(CultureInfo.InvariantCulture),
                r.Variance.ToString(CultureInfo.InvariantCulture)));
        }
    }

    private async Task WriteVariancesCsvAsync(StreamWriter w, CancellationToken ct)
    {
        if (ViewModel?.Variances?.Variances == null) return;
        await w.WriteLineAsync("Department,Account,Budget,Actual,Variance,VariancePercent");
        foreach (var v in ViewModel.Variances.Variances)
        {
            ct.ThrowIfCancellationRequested();
            await w.WriteLineAsync(CsvRow(v.Department, v.Account,
                v.Budget.ToString(CultureInfo.InvariantCulture),
                v.Actual.ToString(CultureInfo.InvariantCulture),
                v.Variance.ToString(CultureInfo.InvariantCulture),
                v.VariancePercent.ToString(CultureInfo.InvariantCulture)));
        }
    }

    private static string CsvRow(params string?[] fields) =>
        string.Join(",", fields.Select(EscapeCsv));

    private static string EscapeCsv(string? field)
    {
        if (string.IsNullOrEmpty(field)) return string.Empty;
        if (field.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return field;
        return "\"" + field.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    // -------------------------------------------------------------------------
    // Disposal
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_panelHeader != null)
            {
                _panelHeader.RefreshClicked -= _refreshClicked;
                _panelHeader.CloseClicked -= _closeClicked;
            }

            _fiscalYearComboBox?.SelectedIndexChanged -= _fyChanged;
            _searchTextBox?.TextChanged -= _searchChanged;
            _tabControl?.SelectedIndexChanged -= _tabChanged;

            if (ViewModel != null && _vmChanged != null)
                ViewModel.PropertyChanged -= _vmChanged;

            _errorProvider?.Dispose();
        }

        base.Dispose(disposing);
    }
}
