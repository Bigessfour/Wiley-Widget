using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.Drawing;
using SfButton = Syncfusion.WinForms.Controls.SfButton;
using SfComboBox = Syncfusion.WinForms.ListView.SfComboBox;
using SfDataGrid = Syncfusion.WinForms.DataGrid.SfDataGrid;
using ChartControl = Syncfusion.Windows.Forms.Chart.ChartControl;
using TextBoxExt = Syncfusion.Windows.Forms.Tools.TextBoxExt;
using TabControlAdv = Syncfusion.Windows.Forms.Tools.TabControlAdv;
using TabPageAdv = Syncfusion.Windows.Forms.Tools.TabPageAdv;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Supporting;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Helpers;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Controls.Analytics;

/// <summary>
/// Analytics Hub panel providing a unified, tabbed interface for budget analytics,
/// forecasting, scenario modeling, and variance analysis.
/// Consolidates functionality from AnalyticsPanel, BudgetAnalyticsPanel, and BudgetOverviewPanel.
/// </summary>
[SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
public partial class AnalyticsHubPanel : ScopedPanelBase<AnalyticsHubViewModel>
{
    // UI Controls
    private PanelHeader? _panelHeader;
    private TabControlAdv? _tabControl;
    private LoadingOverlay? _loadingOverlay;
    private NoDataOverlay? _noDataOverlay;
    private ErrorProvider? _errorProvider;

    // Global filter controls
    private SfComboBox? _fiscalYearComboBox;
    private TextBoxExt? _searchTextBox;
    private SfButton? _globalRefreshButton;
    private SfButton? _globalExportButton;
    private LegacyGradientPanel? _filtersPanel;

    // Tab-specific controls (lazy-loaded)
    private OverviewTabControl? _overviewTab;
    private TrendsTabControl? _trendsTab;
    private ScenariosTabControl? _scenariosTab;
    private VariancesTabControl? _variancesTab;

    // Status
    private StatusStrip? _statusStrip;
    private ToolStripStatusLabel? _statusLabel;
    private ToolStripStatusLabel? _recordCountLabel;
    private ToolStripStatusLabel? _lastRefreshLabel;
    private DateTimeOffset? _lastRefreshAt;

    // Event handlers for cleanup
    private EventHandler? _panelHeaderRefreshHandler;
    private EventHandler? _panelHeaderCloseHandler;
    private EventHandler? _fiscalYearSelectedIndexChangedHandler;
    private EventHandler? _searchTextChangedHandler;
    private EventHandler? _globalRefreshClickHandler;
    private EventHandler? _globalExportClickHandler;
    private EventHandler? _tabControlSelectedIndexChangedHandler;
    private PropertyChangedEventHandler? _viewModelPropertyChangedHandler;
    private bool _isInitialized;

    /// <summary>
    /// Gets or sets the strongly-typed ViewModel.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new AnalyticsHubViewModel? ViewModel
    {
        get => (AnalyticsHubViewModel?)base.ViewModel;
        set => base.ViewModel = value;
    }

    /// <summary>
    /// Initializes a new instance of the AnalyticsHubPanel class.
    /// </summary>
    public AnalyticsHubPanel(
        IServiceScopeFactory scopeFactory,
        ILogger<ScopedPanelBase<AnalyticsHubViewModel>> logger)
        : base(scopeFactory, logger)
    {
        // NOTE: InitializeControls() moved to OnViewModelResolved()
    }

    protected override void OnViewModelResolved(object? viewModel)
    {
        base.OnViewModelResolved(viewModel);
        if (viewModel is AnalyticsHubViewModel && !_isInitialized && !DesignMode)
        {
            try
            {
                InitializeControls();
                SubscribeToViewModelEvents();
                BindInitialData();
                _isInitialized = true;
                _logger?.LogDebug("AnalyticsHubPanel controls initialized successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to initialize AnalyticsHubPanel controls");
                UpdateStatus("Initialization failed");
            }
        }
    }

    #region ICompletablePanel Implementation

    /// <summary>
    /// Loads the panel asynchronously and initializes analytics data.
    /// </summary>
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
            {
                await ViewModel.RefreshAllCommand.ExecuteAsync(null);
            }

            ct.ThrowIfCancellationRequested();

            // Load the initially selected tab
            await LoadCurrentTabAsync(ct);

            _lastRefreshAt = DateTimeOffset.Now;
            UpdateLastRefreshLabel();
            UpdateRecordCount();
            UpdateNoDataOverlayVisibility();

            _logger?.LogDebug("AnalyticsHubPanel loaded successfully");
            UpdateStatus("Ready");
            _isLoaded = true;
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

    /// <summary>
    /// Saves the panel asynchronously. Analytics hub is read-only, so this is a no-op.
    /// </summary>
    public override async Task SaveAsync(CancellationToken ct)
    {
        await Task.CompletedTask;
        _logger?.LogDebug("AnalyticsHubPanel save completed");
    }

    /// <summary>
    /// Validates the panel asynchronously.
    /// </summary>
    public override async Task<ValidationResult> ValidateAsync(CancellationToken ct)
    {
        var errors = new List<ValidationItem>();

        if (ViewModel == null)
        {
            errors.Add(new ValidationItem("ViewModel", "ViewModel not initialized", ValidationSeverity.Error));
        }
        else
        {
            // Validate fiscal year selection
            if (ViewModel.SelectedFiscalYear == 0)
            {
                errors.Add(new ValidationItem("FiscalYear", "Please select a fiscal year", ValidationSeverity.Warning));
            }

            // Validate tab-specific data availability
            if (_tabControl != null)
            {
                switch (_tabControl.SelectedIndex)
                {
                    case 0: // Overview
                        if (ViewModel.Overview == null)
                        {
                            errors.Add(new ValidationItem("Overview", "Overview data not loaded", ValidationSeverity.Warning));
                        }
                        break;
                    case 1: // Trends
                        if (ViewModel.Trends == null)
                        {
                            errors.Add(new ValidationItem("Trends", "Trends data not loaded", ValidationSeverity.Warning));
                        }
                        break;
                    case 2: // Scenarios
                        if (ViewModel.Scenarios == null)
                        {
                            errors.Add(new ValidationItem("Scenarios", "Scenarios data not loaded", ValidationSeverity.Warning));
                        }
                        break;
                    case 3: // Variances
                        if (ViewModel.Variances == null)
                        {
                            errors.Add(new ValidationItem("Variances", "Variances data not loaded", ValidationSeverity.Warning));
                        }
                        break;
                }
            }
        }

        return new ValidationResult(errors.Count == 0, errors);
    }

    #endregion

    private void InitializeControls()
    {
        // Suspend layout during initialization
        this.SuspendLayout();

        // Apply Syncfusion theme
        SfSkinManager.SetVisualStyle(this, SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme);

        // Set up form properties
        Text = "Analytics Hub";
        Dock = DockStyle.Fill;
        Size = new Size(1400, 900);
        MinimumSize = new Size(
            (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(1200.0f),
            (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(700.0f)
        );

        // Panel header with global actions
        _panelHeader = new PanelHeader
        {
            Dock = DockStyle.Top,
            Title = "Analytics Hub"
        };
        _panelHeaderRefreshHandler = async (s, e) => await ViewModel?.RefreshAllCommand.ExecuteAsync(null);
        _panelHeader.RefreshClicked += _panelHeaderRefreshHandler;
        _panelHeaderCloseHandler = (s, e) => ClosePanel();
        _panelHeader.CloseClicked += _panelHeaderCloseHandler;
        Controls.Add(_panelHeader);

        // Global filters panel
        InitializeGlobalFilters();

        // Main tab control
        InitializeTabControl();

        // Status strip
        InitializeStatusStrip();

        // Overlays
        InitializeOverlays();

        // Error provider
        _errorProvider = new ErrorProvider
        {
            BlinkStyle = ErrorBlinkStyle.NeverBlink,
            BlinkRate = 0
        };

        // Set tab order
        SetTabOrder();

        // Apply theme to child controls
        ApplyThemeToControls(SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme);

        // Resume layout
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    private void InitializeGlobalFilters()
    {
        var themeName = SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme;
        _filtersPanel = new LegacyGradientPanel
        {
            Dock = DockStyle.Top,
            Height = 50,
            Padding = new Padding(10),
            BorderStyle = BorderStyle.None
        };
        SfSkinManager.SetVisualStyle(_filtersPanel, themeName);
        _filtersPanel.ThemeName = themeName;

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 1
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120)); // Fiscal Year label
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100)); // Fiscal Year combo
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));  // Search label
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));  // Search box
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200)); // Buttons

        // Fiscal Year
        var fyLabel = new Label { Text = "Fiscal Year:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill };
        _fiscalYearComboBox = new SfComboBox
        {
            Dock = DockStyle.Fill,
            DisplayMember = "Value",
            ValueMember = "Value"
        };
        _fiscalYearSelectedIndexChangedHandler = (s, e) =>
        {
            if (ViewModel != null && _fiscalYearComboBox?.SelectedItem is int year)
            {
                ViewModel.SelectedFiscalYear = year;
            }
        };
        _fiscalYearComboBox.SelectedIndexChanged += _fiscalYearSelectedIndexChangedHandler;

        // Search
        var searchLabel = new Label { Text = "Search:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill };
        _searchTextBox = new TextBoxExt { Dock = DockStyle.Fill };
        _searchTextChangedHandler = (s, e) =>
        {
            if (ViewModel != null)
            {
                ViewModel.SearchText = _searchTextBox?.Text ?? string.Empty;
            }
        };
        _searchTextBox.TextChanged += _searchTextChangedHandler;

        // Buttons
        var buttonsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        _globalRefreshButton = new SfButton { Text = "Refresh All", Width = 90 };
        _globalRefreshClickHandler = async (s, e) => await ViewModel?.RefreshAllCommand.ExecuteAsync(null);
        _globalRefreshButton.Click += _globalRefreshClickHandler;

        _globalExportButton = new SfButton { Text = "Export Hub", Width = 90 };
        _globalExportClickHandler = (s, e) => ExportHub();
        _globalExportButton.Click += _globalExportClickHandler;

        buttonsPanel.Controls.AddRange(new Control[] { _globalRefreshButton, _globalExportButton });

        table.Controls.Add(fyLabel, 0, 0);
        table.Controls.Add(_fiscalYearComboBox, 1, 0);
        table.Controls.Add(searchLabel, 2, 0);
        table.Controls.Add(_searchTextBox, 3, 0);
        table.Controls.Add(buttonsPanel, 4, 0);

        _filtersPanel.Controls.Add(table);
        Controls.Add(_filtersPanel);
    }

    private void InitializeTabControl()
    {
        _tabControl = new TabControlAdv
        {
            Dock = DockStyle.Fill,
            Multiline = true
        };
        SfSkinManager.SetVisualStyle(_tabControl, SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme);

        // Create tabs
        var overviewTabPage = new TabPageAdv { Text = "Overview" };
        _overviewTab = new OverviewTabControl(ViewModel?.Overview, ViewModel);
        overviewTabPage.Controls.Add(_overviewTab);

        var trendsTabPage = new TabPageAdv { Text = "Trends & Forecasts" };
        _trendsTab = new TrendsTabControl(ViewModel?.Trends);
        trendsTabPage.Controls.Add(_trendsTab);

        var scenariosTabPage = new TabPageAdv { Text = "Scenarios" };
        _scenariosTab = new ScenariosTabControl(ViewModel?.Scenarios);
        scenariosTabPage.Controls.Add(_scenariosTab);

        var variancesTabPage = new TabPageAdv { Text = "Variances" };
        _variancesTab = new VariancesTabControl(ViewModel?.Variances);
        variancesTabPage.Controls.Add(_variancesTab);

        _tabControl.TabPages.AddRange(new TabPageAdv[] {
            overviewTabPage, trendsTabPage, scenariosTabPage, variancesTabPage
        });

        _tabControlSelectedIndexChangedHandler = async (s, e) => await LoadCurrentTabAsync(CancellationToken.None);
        _tabControl.SelectedIndexChanged += _tabControlSelectedIndexChangedHandler;

        Controls.Add(_tabControl);
    }

    private void InitializeStatusStrip()
    {
        _statusStrip = new StatusStrip { Dock = DockStyle.Bottom };
        _statusLabel = new ToolStripStatusLabel { Text = "Ready", Spring = true };
        _recordCountLabel = new ToolStripStatusLabel { Text = "Records: 0" };
        _lastRefreshLabel = new ToolStripStatusLabel { Text = "Last refresh: Never" };

        _statusStrip.Items.AddRange(new ToolStripItem[] {
            _statusLabel, _recordCountLabel, _lastRefreshLabel
        });

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

    private void SetTabOrder()
    {
        // Set tab order for accessibility
        if (_fiscalYearComboBox != null) _fiscalYearComboBox.TabIndex = 0;
        if (_searchTextBox != null) _searchTextBox.TabIndex = 1;
        if (_globalRefreshButton != null) _globalRefreshButton.TabIndex = 2;
        if (_globalExportButton != null) _globalExportButton.TabIndex = 3;
        if (_tabControl != null) _tabControl.TabIndex = 4;
    }

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
                case 0: // Overview
                    if (_overviewTab != null && !_overviewTab.IsLoaded)
                    {
                        await _overviewTab.LoadAsync();
                    }
                    break;
                case 1: // Trends
                    if (_trendsTab != null && !_trendsTab.IsLoaded)
                    {
                        await _trendsTab.LoadAsync();
                    }
                    break;
                case 2: // Scenarios
                    if (_scenariosTab != null && !_scenariosTab.IsLoaded)
                    {
                        await _scenariosTab.LoadAsync();
                    }
                    break;
                case 3: // Variances
                    if (_variancesTab != null && !_variancesTab.IsLoaded)
                    {
                        await _variancesTab.LoadAsync();
                    }
                    break;
            }

            UpdateRecordCount();
            UpdateNoDataOverlayVisibility();
            UpdateStatus("Ready");
        }
        catch (OperationCanceledException)
        {
            UpdateStatus("Load cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load current tab");
            UpdateStatus("Error loading tab");
            ShowErrorOverlay("Failed to load analytics tab. Check logs for details.");
        }
        finally
        {
            if (overlayShown)
            {
                HideLoadingOverlay();
            }
        }
    }

    private void UpdateStatus(string message)
    {
        if (_statusLabel != null)
        {
            if (InvokeRequired)
                Invoke(() => _statusLabel.Text = message);
            else
                _statusLabel.Text = message;
        }
    }

    private void ExportHub()
    {
        if (IsBusy || ViewModel == null || _tabControl == null)
        {
            return;
        }

        _ = ExportHubAsync(CancellationToken.None);
    }

    private async Task ExportHubAsync(CancellationToken ct)
    {
        if (_tabControl == null || ViewModel == null)
        {
            ShowErrorOverlay("Analytics data is not ready for export.");
            return;
        }

        var tabName = GetCurrentTabName();
        var defaultFileName = $"Analytics_{tabName}_{DateTime.Now:yyyyMMdd_HHmm}.csv";

        using var saveDialog = new SaveFileDialog
        {
            Filter = "CSV Files|*.csv",
            Title = $"Export {tabName}",
            FileName = defaultFileName
        };

        if (saveDialog.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        try
        {
            ct.ThrowIfCancellationRequested();

            IsBusy = true;
            ShowLoadingOverlay($"Exporting {tabName}...");
            UpdateStatus($"Exporting {tabName}...");

            await ExportCurrentTabToCsvAsync(saveDialog.FileName, ct);

            UpdateStatus("Export completed successfully");
            MessageBox.Show(
                $"Export completed successfully:\n{saveDialog.FileName}",
                "Export Successful",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            UpdateStatus("Export cancelled");
        }
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

    protected override void OnThemeChanged(string themeName)
    {
        base.OnThemeChanged(themeName);

        try
        {
            SfSkinManager.SetVisualStyle(this, themeName);
            ApplyThemeToControls(themeName);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to apply theme '{Theme}' to AnalyticsHubPanel", themeName);
        }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        if (!IsLoaded && !DesignMode)
        {
            _ = LoadAsync(CancellationToken.None);
        }
    }

    private void SubscribeToViewModelEvents()
    {
        if (ViewModel == null) return;

        if (_viewModelPropertyChangedHandler != null)
        {
            ViewModel.PropertyChanged -= _viewModelPropertyChangedHandler;
        }

        _viewModelPropertyChangedHandler = (s, e) =>
        {
            if (e.PropertyName == nameof(ViewModel.FiscalYears))
            {
                BindFiscalYears();
            }
            else if (e.PropertyName == nameof(ViewModel.SelectedFiscalYear))
            {
                UpdateSelectedFiscalYear();
            }
        };

        ViewModel.PropertyChanged += _viewModelPropertyChangedHandler;
    }

    private void BindInitialData()
    {
        BindFiscalYears();
        UpdateSelectedFiscalYear();
        UpdateRecordCount();
        UpdateLastRefreshLabel();
    }

    private void BindFiscalYears()
    {
        if (_fiscalYearComboBox != null && ViewModel?.FiscalYears != null)
        {
            _fiscalYearComboBox.DataSource = ViewModel.FiscalYears;
        }
    }

    private void UpdateSelectedFiscalYear()
    {
        if (_fiscalYearComboBox != null && ViewModel != null)
        {
            _fiscalYearComboBox.SelectedItem = ViewModel.SelectedFiscalYear;
        }
    }

    private void UpdateRecordCount()
    {
        if (_recordCountLabel == null || ViewModel == null) return;

        var metricsCount = ViewModel.Overview?.Metrics?.Count ?? 0;
        var kpiCount = ViewModel.Overview?.Kpis?.Count ?? 0;
        var total = metricsCount + kpiCount;

        if (InvokeRequired)
        {
            Invoke(() => _recordCountLabel.Text = $"Records: {total}");
        }
        else
        {
            _recordCountLabel.Text = $"Records: {total}";
        }
    }

    private void UpdateLastRefreshLabel()
    {
        if (_lastRefreshLabel == null) return;

        var labelText = _lastRefreshAt.HasValue
            ? $"Last refresh: {_lastRefreshAt.Value:yyyy-MM-dd HH:mm}"
            : "Last refresh: Never";

        if (InvokeRequired)
        {
            Invoke(() => _lastRefreshLabel.Text = labelText);
        }
        else
        {
            _lastRefreshLabel.Text = labelText;
        }
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

        if (InvokeRequired)
        {
            Invoke(() => _loadingOverlay.Visible = false);
        }
        else
        {
            _loadingOverlay.Visible = false;
        }
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

    private void HideNoDataOverlay()
    {
        if (_noDataOverlay == null) return;

        if (InvokeRequired)
        {
            Invoke(() => _noDataOverlay.Visible = false);
        }
        else
        {
            _noDataOverlay.Visible = false;
        }
    }

    private void UpdateNoDataOverlayVisibility()
    {
        if (ViewModel == null || _tabControl == null)
        {
            ShowNoDataOverlay("No analytics data available\r\nPerform budget operations to generate insights");
            return;
        }

        bool hasData = _tabControl.SelectedIndex switch
        {
            0 => (ViewModel.Overview?.Metrics?.Count ?? 0) > 0 || (ViewModel.Overview?.Kpis?.Count ?? 0) > 0,
            1 => (ViewModel.Trends?.TrendData?.Count ?? 0) > 0 || (ViewModel.Trends?.ForecastData?.Count ?? 0) > 0,
            2 => (ViewModel.Scenarios?.ScenarioResults?.Count ?? 0) > 0,
            3 => (ViewModel.Variances?.Variances?.Count ?? 0) > 0,
            _ => false
        };

        if (hasData)
        {
            HideNoDataOverlay();
        }
        else
        {
            var tabName = GetCurrentTabName();
            ShowNoDataOverlay($"No {tabName.ToLower(CultureInfo.InvariantCulture)} data available\r\nPerform budget operations to generate insights");
        }
    }

    private void ShowErrorOverlay(string message)
    {
        _logger?.LogError("AnalyticsHubPanel error: {Message}", message);
        MessageBox.Show(this, message, "Analytics Hub", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private void ApplyThemeToControls(string themeName)
    {
        if (_filtersPanel != null)
        {
            SfSkinManager.SetVisualStyle(_filtersPanel, themeName);
            _filtersPanel.ThemeName = themeName;
        }

        if (_fiscalYearComboBox != null)
        {
            SfSkinManager.SetVisualStyle(_fiscalYearComboBox, themeName);
            _fiscalYearComboBox.ThemeName = themeName;
        }

        if (_searchTextBox != null)
        {
            SfSkinManager.SetVisualStyle(_searchTextBox, themeName);
            _searchTextBox.ThemeName = themeName;
        }

        if (_globalRefreshButton != null)
        {
            SfSkinManager.SetVisualStyle(_globalRefreshButton, themeName);
            _globalRefreshButton.ThemeName = themeName;
        }

        if (_globalExportButton != null)
        {
            SfSkinManager.SetVisualStyle(_globalExportButton, themeName);
            _globalExportButton.ThemeName = themeName;
        }

        if (_tabControl != null)
        {
            SfSkinManager.SetVisualStyle(_tabControl, themeName);
            foreach (TabPageAdv page in _tabControl.TabPages)
            {
                SfSkinManager.SetVisualStyle(page, themeName);
            }
        }

        if (_panelHeader != null)
        {
            SfSkinManager.SetVisualStyle(_panelHeader, themeName);
        }

        if (_loadingOverlay != null)
        {
            SfSkinManager.SetVisualStyle(_loadingOverlay, themeName);
            _loadingOverlay.ThemeName = themeName;
        }

        if (_noDataOverlay != null)
        {
            SfSkinManager.SetVisualStyle(_noDataOverlay, themeName);
            _noDataOverlay.ThemeName = themeName;
        }

        if (_overviewTab != null)
        {
            SfSkinManager.SetVisualStyle(_overviewTab, themeName);
        }

        if (_trendsTab != null)
        {
            SfSkinManager.SetVisualStyle(_trendsTab, themeName);
        }

        if (_scenariosTab != null)
        {
            SfSkinManager.SetVisualStyle(_scenariosTab, themeName);
        }

        if (_variancesTab != null)
        {
            SfSkinManager.SetVisualStyle(_variancesTab, themeName);
        }
    }

    private string GetCurrentTabName()
    {
        if (_tabControl == null)
        {
            return "Analytics";
        }

        return _tabControl.SelectedIndex switch
        {
            0 => "Overview",
            1 => "Trends",
            2 => "Scenarios",
            3 => "Variances",
            _ => "Analytics"
        };
    }

    private async Task ExportCurrentTabToCsvAsync(string filePath, CancellationToken ct)
    {
        if (ViewModel == null)
        {
            throw new InvalidOperationException("Analytics data is not available.");
        }

        await using var writer = new StreamWriter(filePath, false, Encoding.UTF8);

        switch (_tabControl?.SelectedIndex)
        {
            case 0:
                await WriteOverviewCsvAsync(writer, ct);
                break;
            case 1:
                await WriteTrendsCsvAsync(writer, ct);
                break;
            case 2:
                await WriteScenariosCsvAsync(writer, ct);
                break;
            case 3:
                await WriteVariancesCsvAsync(writer, ct);
                break;
            default:
                await WriteOverviewCsvAsync(writer, ct);
                break;
        }
    }

    private async Task WriteOverviewCsvAsync(StreamWriter writer, CancellationToken ct)
    {
        if (ViewModel?.Overview == null) return;

        await writer.WriteLineAsync("Section,Name,Value,Department,BudgetedAmount,Amount,Variance,VariancePercent,IsOverBudget,Format,IsPositive");

        var overview = ViewModel.Overview;
        var summaryItems = new (string Name, decimal Value)[]
        {
            ("Total Budget", overview.TotalBudget),
            ("Total Actual", overview.TotalActual),
            ("Total Variance", overview.TotalVariance),
            ("Over Budget Count", overview.OverBudgetCount),
            ("Under Budget Count", overview.UnderBudgetCount)
        };

        foreach (var item in summaryItems)
        {
            ct.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(string.Join(",", new[]
            {
                "Summary",
                EscapeCsvField(item.Name),
                item.Value.ToString(CultureInfo.InvariantCulture),
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty
            }));
        }

        if (overview.Kpis == null) return;

        foreach (var kpi in overview.Kpis)
        {
            ct.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(string.Join(",", new[]
            {
                "KPI",
                EscapeCsvField(kpi.Title),
                kpi.Value.ToString(CultureInfo.InvariantCulture),
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                EscapeCsvField(kpi.Format),
                kpi.IsPositive.ToString(CultureInfo.InvariantCulture)
            }));
        }

        if (overview.Metrics == null) return;

        foreach (var metric in overview.Metrics)
        {
            ct.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(string.Join(",", new[]
            {
                "Metric",
                EscapeCsvField(metric.Name),
                metric.Value.ToString(CultureInfo.InvariantCulture),
                EscapeCsvField(metric.DepartmentName),
                metric.BudgetedAmount.ToString(CultureInfo.InvariantCulture),
                metric.Amount.ToString(CultureInfo.InvariantCulture),
                metric.Variance.ToString(CultureInfo.InvariantCulture),
                metric.VariancePercent.ToString(CultureInfo.InvariantCulture),
                metric.IsOverBudget.ToString(CultureInfo.InvariantCulture),
                string.Empty,
                string.Empty
            }));
        }
    }

    private async Task WriteTrendsCsvAsync(StreamWriter writer, CancellationToken ct)
    {
        if (ViewModel?.Trends == null) return;

        await writer.WriteLineAsync("Section,Series,Date,Value,PredictedReserves,ConfidenceInterval,Department,AverageVariancePercent,TotalBudgeted,TotalActual,Count");

        if (ViewModel.Trends.TrendData == null) return;

        foreach (var series in ViewModel.Trends.TrendData)
        {
            foreach (var point in series.Points)
            {
                ct.ThrowIfCancellationRequested();
                await writer.WriteLineAsync(string.Join(",", new[]
                {
                    "Trend",
                    EscapeCsvField(series.Name),
                    point.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    point.Value.ToString(CultureInfo.InvariantCulture),
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty
                }));
            }
        }

        foreach (var forecast in ViewModel.Trends.ForecastData)
        {
            ct.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(string.Join(",", new[]
            {
                "Forecast",
                string.Empty,
                forecast.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                string.Empty,
                forecast.PredictedReserves.ToString(CultureInfo.InvariantCulture),
                forecast.ConfidenceInterval.ToString(CultureInfo.InvariantCulture),
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty
            }));
        }

        foreach (var dept in ViewModel.Trends.DepartmentVariances)
        {
            ct.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(string.Join(",", new[]
            {
                "Department",
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                EscapeCsvField(dept.Department),
                dept.AverageVariancePercent.ToString(CultureInfo.InvariantCulture),
                dept.TotalBudgeted.ToString(CultureInfo.InvariantCulture),
                dept.TotalActual.ToString(CultureInfo.InvariantCulture),
                dept.Count.ToString(CultureInfo.InvariantCulture)
            }));
        }
    }

    private async Task WriteScenariosCsvAsync(StreamWriter writer, CancellationToken ct)
    {
        if (ViewModel?.Scenarios == null) return;

        await writer.WriteLineAsync("Description,ProjectedValue,Variance");

        if (ViewModel.Scenarios.ScenarioResults == null) return;

        foreach (var result in ViewModel.Scenarios.ScenarioResults)
        {
            ct.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(string.Join(",", new[]
            {
                EscapeCsvField(result.Description),
                result.ProjectedValue.ToString(CultureInfo.InvariantCulture),
                result.Variance.ToString(CultureInfo.InvariantCulture)
            }));
        }
    }

    private async Task WriteVariancesCsvAsync(StreamWriter writer, CancellationToken ct)
    {
        if (ViewModel?.Variances == null) return;

        await writer.WriteLineAsync("Department,Account,Budget,Actual,Variance,VariancePercent");

        if (ViewModel.Variances.Variances == null) return;

        foreach (var variance in ViewModel.Variances.Variances)
        {
            ct.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(string.Join(",", new[]
            {
                EscapeCsvField(variance.Department),
                EscapeCsvField(variance.Account),
                variance.Budget.ToString(CultureInfo.InvariantCulture),
                variance.Actual.ToString(CultureInfo.InvariantCulture),
                variance.Variance.ToString(CultureInfo.InvariantCulture),
                variance.VariancePercent.ToString(CultureInfo.InvariantCulture)
            }));
        }
    }

    private static string EscapeCsvField(string? field)
    {
        if (string.IsNullOrEmpty(field))
        {
            return string.Empty;
        }

        if (field.Contains(",", StringComparison.Ordinal) ||
            field.Contains("\"", StringComparison.Ordinal) ||
            field.Contains("\n", StringComparison.Ordinal) ||
            field.Contains("\r", StringComparison.Ordinal))
        {
            return "\"" + field.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }

        return field;
    }

    private void ClosePanel()
    {
        var parent = Parent;
        parent?.Controls.Remove(this);
        Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Clean up event handlers
            if (_panelHeader != null)
            {
                _panelHeader.RefreshClicked -= _panelHeaderRefreshHandler;
                _panelHeader.CloseClicked -= _panelHeaderCloseHandler;
            }

            if (_fiscalYearComboBox != null)
                _fiscalYearComboBox.SelectedIndexChanged -= _fiscalYearSelectedIndexChangedHandler;

            if (_searchTextBox != null)
                _searchTextBox.TextChanged -= _searchTextChangedHandler;

            if (_globalRefreshButton != null)
                _globalRefreshButton.Click -= _globalRefreshClickHandler;

            if (_globalExportButton != null)
                _globalExportButton.Click -= _globalExportClickHandler;

            if (_tabControl != null)
                _tabControl.SelectedIndexChanged -= _tabControlSelectedIndexChangedHandler;

            if (ViewModel != null)
                ViewModel.PropertyChanged -= _viewModelPropertyChangedHandler;
        }

        base.Dispose(disposing);
    }
}



