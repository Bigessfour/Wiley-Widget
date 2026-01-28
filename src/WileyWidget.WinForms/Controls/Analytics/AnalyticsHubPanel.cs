using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Linq;
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
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Utils;
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

    // Event handlers for cleanup
    private EventHandler? _panelHeaderRefreshHandler;
    private EventHandler? _panelHeaderCloseHandler;
    private EventHandler? _fiscalYearSelectedIndexChangedHandler;
    private EventHandler? _searchTextChangedHandler;
    private EventHandler? _globalRefreshClickHandler;
    private EventHandler? _globalExportClickHandler;
    private EventHandler? _tabControlSelectedIndexChangedHandler;
    private PropertyChangedEventHandler? _viewModelPropertyChangedHandler;

    /// <summary>
    /// Initializes a new instance of the AnalyticsHubPanel class.
    /// </summary>
    public AnalyticsHubPanel(
        IServiceScopeFactory scopeFactory,
        ILogger<ScopedPanelBase<AnalyticsHubViewModel>> logger)
        : base(scopeFactory, logger)
    {
        InitializeControls();
    }

    #region ICompletablePanel Implementation

    /// <summary>
    /// Loads the panel asynchronously and initializes analytics data.
    /// </summary>
    public override async Task LoadAsync(CancellationToken ct)
    {
        if (IsLoaded) return;

        try
        {
            IsBusy = true;
            UpdateStatus("Loading Analytics Hub...");

            if (ViewModel != null && !DesignMode)
            {
                await ViewModel.RefreshAllCommand.ExecuteAsync(null);
            }

            // Load the initially selected tab
            await LoadCurrentTabAsync();

            _logger?.LogDebug("AnalyticsHubPanel loaded successfully");
            UpdateStatus("Ready");
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("AnalyticsHubPanel load cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load AnalyticsHubPanel");
            UpdateStatus("Error loading data");
        }
        finally
        {
            IsBusy = false;
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

        // Resume layout
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    private void InitializeGlobalFilters()
    {
        var filtersPanel = new GradientPanelExt
        {
            Dock = DockStyle.Top,
            Height = 50,
            Padding = new Padding(10),
            BorderStyle = BorderStyle.None
        };
        SfSkinManager.SetVisualStyle(filtersPanel, SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme);

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

        filtersPanel.Controls.Add(table);
        Controls.Add(filtersPanel);
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

        _tabControlSelectedIndexChangedHandler = async (s, e) => await LoadCurrentTabAsync();
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

    private async Task LoadCurrentTabAsync()
    {
        if (_tabControl == null || ViewModel == null) return;

        try
        {
            UpdateStatus("Loading tab data...");

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

            UpdateStatus("Ready");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load current tab");
            UpdateStatus("Error loading tab");
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
        // TODO: Implement global export functionality
        MessageBox.Show("Global export functionality will be implemented", "Analytics Hub",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        // Bind ViewModel properties
        if (ViewModel != null)
        {
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

            BindFiscalYears();
            UpdateSelectedFiscalYear();
        }
    }

    private void BindFiscalYears()
    {
        if (_fiscalYearComboBox != null && ViewModel != null)
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



