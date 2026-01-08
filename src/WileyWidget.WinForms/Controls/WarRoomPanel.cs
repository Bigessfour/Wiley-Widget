#nullable enable

using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Syncfusion.Windows.Forms.Chart;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.Windows.Forms.Gauge;
using Syncfusion.Drawing;
using Syncfusion.WinForms.Controls;
using Microsoft.Extensions.Logging;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Theming;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Extensions;
using AppThemeColors = WileyWidget.WinForms.Themes.ThemeColors;

namespace WileyWidget.WinForms.Controls
{
    /// <summary>
    /// War Room panel for interactive what-if scenario analysis.
    /// Integrates GrokAgentService for AI-powered financial projections.
    /// Features:
    /// - Natural language scenario input with JARVIS voice hint
    /// - Revenue/Reserves trend chart (line chart)
    /// - Department impact analysis (column chart)
    /// - Risk level gauge (radial gauge)
    /// - Prominent "Required Rate Increase" display
    /// </summary>
    public partial class WarRoomPanel : UserControl
    {
        /// <summary>
        /// Simple DataContext wrapper for host compatibility.
        /// </summary>
        public new object? DataContext { get; private set; }

        private readonly WarRoomViewModel _vm;
        private readonly ILogger<WarRoomPanel>? _logger;
        private readonly Services.IThemeService? _themeService;

        private GradientPanelExt _topPanel = null!;
        private PanelHeader? _panelHeader;
        private LoadingOverlay? _loadingOverlay;
        private Label _lblStatus = null!;

        // Input controls
        private TextBox _scenarioInput = null!;
        private Button _btnRunScenario = null!;
        private Label _lblVoiceHint = null!;

        // Results panels
        private Panel _resultsPanel = null!;

        // Top-left: Big headline
        private Label _lblRateIncreaseHeadline = null!;
        private Label _lblRateIncreaseValue = null!;

        // Top-right: Risk gauge
        private RadialGauge? _riskGauge;

        // Middle: Charts
        private ChartControl _revenueChart = null!;
        private ChartControl _departmentChart = null!;

        // Bottom: Results grid
        private SfDataGrid _projectionsGrid = null!;
        private SfDataGrid _departmentImpactGrid = null!;

        private Panel _chartPanel = null!;
        private Panel _gridPanel = null!;

        private ToolTip? _sharedTooltip;

        public WarRoomPanel() : this(
            ResolveWarRoomViewModel(),
            ResolveThemeService(),
            ResolveLogger())
        {
        }

        public WarRoomPanel(
            WarRoomViewModel? viewModel = null,
            Services.IThemeService? themeService = null,
            ILogger<WarRoomPanel>? logger = null)
        {
            InitializeComponent();

            _logger = logger ?? ResolveLogger();
            _themeService = themeService;
            _vm = viewModel ?? ResolveWarRoomViewModel();

            DataContext = _vm;

            _logger?.LogInformation("WarRoomPanel initializing");

            InitializeUI();
            BindViewModel();
            ApplyTheme();

            _logger?.LogInformation("WarRoomPanel initialized successfully");
        }

        /// <summary>
        /// Initializes all UI controls with proper layout and theming.
        /// </summary>
        private void InitializeUI()
        {
            try
            {
                // Top panel with scenario input
                _topPanel = new GradientPanelExt
                {
                    Height = 120,
                    Dock = DockStyle.Top,
                    Padding = new Padding(8),
                    Name = "WarRoomTopPanel",
                    AccessibleName = "War Room Input Panel"
                };
                Controls.Add(_topPanel);

                // Panel header
                _panelHeader = new PanelHeader
                {
                    Dock = DockStyle.Top,
                    Parent = _topPanel,
                    Height = 40
                };

                // Status label
                _lblStatus = new Label
                {
                    Text = "Ready",
                    Dock = DockStyle.Right,
                    TextAlign = ContentAlignment.MiddleRight,
                    AutoSize = false,
                    Width = 200,
                    Height = 30,
                    Margin = new Padding(4),
                    Name = "StatusLabel"
                };
                _topPanel.Controls.Add(_lblStatus);

                // Scenario input layout
                var inputGroupBox = new GroupBox
                {
                    Text = "Scenario Input",
                    Dock = DockStyle.Fill,
                    Padding = new Padding(8),
                    // ForeColor removed - let SkinManager handle theming
                    Name = "ScenarioInputGroup"
                };
                _topPanel.Controls.Add(inputGroupBox);

                _scenarioInput = new TextBox
                {
                    Text = "Raise water rates 12% and inflation is 4% for 5 years",
                    Multiline = false,
                    Dock = DockStyle.Left,
                    AutoSize = false,
                    Height = 28,
                    Width = 500,
                    Margin = new Padding(4),
                    Name = "ScenarioInput",
                    AccessibleName = "Scenario Input",
                    AccessibleDescription = "Enter your scenario in natural language"
                };
                inputGroupBox.Controls.Add(_scenarioInput);

                _btnRunScenario = new Button
                {
                    Text = "Run Scenario",
                    Dock = DockStyle.Right,
                    AutoSize = true,
                    Height = 28,
                    Width = 120,
                    Margin = new Padding(4),
                    Name = "RunScenarioButton",
                    AccessibleName = "Run Scenario",
                    AccessibleDescription = "Click to analyze the scenario"
                };
                inputGroupBox.Controls.Add(_btnRunScenario);

                _lblVoiceHint = new Label
                {
                    Text = "💬 Ask JARVIS aloud",
                    Dock = DockStyle.Bottom,
                    TextAlign = ContentAlignment.MiddleLeft,
                    AutoSize = true,
                    Margin = new Padding(4),
                    Font = new Font("Segoe UI", 9, FontStyle.Italic),
                    // ForeColor removed - let SkinManager handle theming
                    Name = "VoiceHint"
                };
                inputGroupBox.Controls.Add(_lblVoiceHint);

                // Loading overlay
                _loadingOverlay = new LoadingOverlay
                {
                    Dock = DockStyle.Fill,
                    Name = "WarRoomLoadingOverlay"
                };
                Controls.Add(_loadingOverlay);

                // Results panel
                _resultsPanel = new Panel
                {
                    Dock = DockStyle.Fill,
                    AutoScroll = true,
                    Name = "ResultsPanel",
                    Visible = false
                };
                Controls.Add(_resultsPanel);

                // Headline + Gauge panel (top of results)
                var headlinePanel = new Panel
                {
                    Height = 150,
                    Dock = DockStyle.Top,
                    Padding = new Padding(8),
                    Name = "HeadlinePanel"
                };
                _resultsPanel.Controls.Add(headlinePanel);

                // Rate Increase headline (left side - big and bold)
                var headlineGroup = new GroupBox
                {
                    Text = "Scenario Result",
                    Dock = DockStyle.Left,
                    Width = 300,
                    Padding = new Padding(8),
                    Name = "HeadlineGroup"
                };
                headlinePanel.Controls.Add(headlineGroup);

                _lblRateIncreaseHeadline = new Label
                {
                    Text = "Required Rate Increase:",
                    Dock = DockStyle.Top,
                    TextAlign = ContentAlignment.MiddleLeft,
                    AutoSize = false,
                    Height = 24,
                    Font = new Font("Segoe UI", 10, FontStyle.Bold),
                    Name = "RateIncreaseLabel"
                };
                headlineGroup.Controls.Add(_lblRateIncreaseHeadline);

                _lblRateIncreaseValue = new Label
                {
                    Text = "12.4%",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.TopLeft,
                    Font = new Font("Segoe UI", 48, FontStyle.Bold),
                    // Accent color removed - let SkinManager handle theming
                    Name = "RateIncreaseValue"
                };
                headlineGroup.Controls.Add(_lblRateIncreaseValue);

                // Risk gauge (right side)
                var gaugeGroup = new GroupBox
                {
                    Text = "Risk Level",
                    Dock = DockStyle.Right,
                    Width = 250,
                    Padding = new Padding(8),
                    Name = "RiskGaugeGroup"
                };
                headlinePanel.Controls.Add(gaugeGroup);

                _riskGauge = new RadialGauge
                {
                    Dock = DockStyle.Fill,
                    Name = "RiskGauge",
                    MinimumValue = 0,
                    MaximumValue = 100,
                    Value = 50
                };
                gaugeGroup.Controls.Add(_riskGauge);

                // Charts panel
                _chartPanel = new Panel
                {
                    Height = 300,
                    Dock = DockStyle.Top,
                    Padding = new Padding(4),
                    Name = "ChartPanel"
                };
                _resultsPanel.Controls.Add(_chartPanel);

                // Revenue chart (left)
                var revChartGroup = new GroupBox
                {
                    Text = "Revenue & Reserves Projection",
                    Dock = DockStyle.Left,
                    Width = 450,
                    Padding = new Padding(4),
                    Name = "RevenueChartGroup"
                };
                _chartPanel.Controls.Add(revChartGroup);

                _revenueChart = new ChartControl
                {
                    Dock = DockStyle.Fill,
                    Name = "RevenueChart",
                    Palette = ChartColorPalette.GrayScale
                };
                revChartGroup.Controls.Add(_revenueChart);

                // Department impact chart (right)
                var deptChartGroup = new GroupBox
                {
                    Text = "Department Budget Impact",
                    Dock = DockStyle.Right,
                    Width = 450,
                    Padding = new Padding(4),
                    Name = "DeptChartGroup"
                };
                _chartPanel.Controls.Add(deptChartGroup);

                _departmentChart = new ChartControl
                {
                    Dock = DockStyle.Fill,
                    Name = "DepartmentChart",
                    Palette = ChartColorPalette.GrayScale
                };
                deptChartGroup.Controls.Add(_departmentChart);

                // Grids panel
                _gridPanel = new Panel
                {
                    Dock = DockStyle.Fill,
                    Padding = new Padding(4),
                    Name = "GridPanel"
                };
                _resultsPanel.Controls.Add(_gridPanel);

                // Projections grid
                var projGridGroup = new GroupBox
                {
                    Text = "Year-by-Year Projections",
                    Dock = DockStyle.Top,
                    Height = 180,
                    Padding = new Padding(4),
                    Name = "ProjectionsGridGroup"
                };
                _gridPanel.Controls.Add(projGridGroup);

                _projectionsGrid = new SfDataGrid
                {
                    Dock = DockStyle.Fill,
                    AllowFiltering = true,
                    AllowSorting = true,
                    AllowEditing = false,
                    RowHeight = 24,
                    AutoSizeColumnsMode = AutoSizeColumnsMode.Fill,
                    ShowRowHeader = false,
                    Name = "ProjectionsGrid",
                    AutoGenerateColumns = false
                };
                projGridGroup.Controls.Add(_projectionsGrid);

                ConfigureProjectionsGrid();

                // Department Impact grid
                var deptGridGroup = new GroupBox
                {
                    Text = "Department Impact Analysis",
                    Dock = DockStyle.Fill,
                    Padding = new Padding(4),
                    Name = "DepartmentGridGroup"
                };
                _gridPanel.Controls.Add(deptGridGroup);

                _departmentImpactGrid = new SfDataGrid
                {
                    Dock = DockStyle.Fill,
                    AllowFiltering = true,
                    AllowSorting = true,
                    AllowEditing = false,
                    RowHeight = 24,
                    AutoSizeColumnsMode = AutoSizeColumnsMode.Fill,
                    ShowRowHeader = false,
                    Name = "DepartmentImpactGrid",
                    AutoGenerateColumns = false
                };
                deptGridGroup.Controls.Add(_departmentImpactGrid);

                ConfigureDepartmentImpactGrid();

                _sharedTooltip = new ToolTip();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error initializing WarRoomPanel UI");
                throw;
            }
        }

        /// <summary>
        /// Configures the projections grid columns.
        /// </summary>
        private void ConfigureProjectionsGrid()
        {
            try
            {
                _projectionsGrid.Columns.Add(new GridTextColumn
                {
                    MappingName = nameof(ScenarioProjection.Year),
                    HeaderText = "Year",
                    Width = 60,
                    Format = "0"
                });

                _projectionsGrid.Columns.Add(new GridTextColumn
                {
                    MappingName = nameof(ScenarioProjection.ProjectedRate),
                    HeaderText = "Rate ($/mo)",
                    Width = 100,
                    Format = "C2"
                });

                _projectionsGrid.Columns.Add(new GridTextColumn
                {
                    MappingName = nameof(ScenarioProjection.ProjectedRevenue),
                    HeaderText = "Revenue",
                    Width = 100,
                    Format = "C0"
                });

                _projectionsGrid.Columns.Add(new GridTextColumn
                {
                    MappingName = nameof(ScenarioProjection.ProjectedExpenses),
                    HeaderText = "Expenses",
                    Width = 100,
                    Format = "C0"
                });

                _projectionsGrid.Columns.Add(new GridTextColumn
                {
                    MappingName = nameof(ScenarioProjection.ProjectedBalance),
                    HeaderText = "Balance",
                    Width = 100,
                    Format = "C0"
                });

                _projectionsGrid.Columns.Add(new GridTextColumn
                {
                    MappingName = nameof(ScenarioProjection.ReserveLevel),
                    HeaderText = "Reserve (3mo target)",
                    Width = 120,
                    Format = "C0"
                });

                _logger?.LogDebug("Projections grid configured with {ColumnCount} columns", _projectionsGrid.Columns.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error configuring projections grid");
            }
        }

        /// <summary>
        /// Configures the department impact grid columns.
        /// </summary>
        private void ConfigureDepartmentImpactGrid()
        {
            try
            {
                _departmentImpactGrid.Columns.Add(new GridTextColumn
                {
                    MappingName = nameof(DepartmentImpact.DepartmentName),
                    HeaderText = "Department",
                    Width = 150
                });

                _departmentImpactGrid.Columns.Add(new GridTextColumn
                {
                    MappingName = nameof(DepartmentImpact.CurrentBudget),
                    HeaderText = "Current Budget",
                    Width = 120,
                    Format = "C0"
                });

                _departmentImpactGrid.Columns.Add(new GridTextColumn
                {
                    MappingName = nameof(DepartmentImpact.ProjectedBudget),
                    HeaderText = "Projected Budget",
                    Width = 130,
                    Format = "C0"
                });

                _departmentImpactGrid.Columns.Add(new GridTextColumn
                {
                    MappingName = nameof(DepartmentImpact.ImpactAmount),
                    HeaderText = "Impact Amount",
                    Width = 120,
                    Format = "C0"
                });

                _departmentImpactGrid.Columns.Add(new GridTextColumn
                {
                    MappingName = nameof(DepartmentImpact.ImpactPercentage),
                    HeaderText = "Impact %",
                    Width = 100,
                    Format = "0.00%"
                });

                _logger?.LogDebug("Department impact grid configured with {ColumnCount} columns", _departmentImpactGrid.Columns.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error configuring department impact grid");
            }
        }

        /// <summary>
        /// Binds the ViewModel to UI controls.
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
                // Bind data collections to grids
                _projectionsGrid.DataSource = _vm.Projections;
                _departmentImpactGrid.DataSource = _vm.DepartmentImpacts;

                // Subscribe to ViewModel property changes
                _vm.PropertyChanged += ViewModel_PropertyChanged;

                // Wire up command
                _btnRunScenario.Click += (s, e) => _vm.RunScenarioCommand.Execute(null);

                // Wire up input textbox
                _scenarioInput.TextChanged += (s, e) => _vm.ScenarioInput = _scenarioInput.Text;

                _logger?.LogInformation("ViewModel bound successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error binding ViewModel");
            }
        }

        /// <summary>
        /// Handles ViewModel property changes.
        /// </summary>
        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            try
            {
                switch (e?.PropertyName)
                {
                    case nameof(WarRoomViewModel.StatusMessage):
                        _lblStatus.Text = _vm?.StatusMessage ?? "Ready";
                        _lblStatus.Refresh();
                        break;

                    case nameof(WarRoomViewModel.IsAnalyzing):
                        if (_loadingOverlay != null)
                            _loadingOverlay.Visible = _vm?.IsAnalyzing ?? false;
                        break;

                    case nameof(WarRoomViewModel.HasResults):
                        if (_resultsPanel != null)
                            _resultsPanel.Visible = _vm?.HasResults ?? false;
                        break;

                    case nameof(WarRoomViewModel.RequiredRateIncrease):
                        _lblRateIncreaseValue.Text = _vm?.RequiredRateIncrease ?? "—";
                        break;

                    case nameof(WarRoomViewModel.RiskLevel):
                        if (_riskGauge != null)
                            _riskGauge.Value = (float)(_vm?.RiskLevel ?? 0);
                        break;

                    case nameof(WarRoomViewModel.Projections):
                        RenderRevenueChart();
                        break;

                    case nameof(WarRoomViewModel.DepartmentImpacts):
                        RenderDepartmentChart();
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling ViewModel property change: {PropertyName}", e?.PropertyName);
            }
        }

        /// <summary>
        /// Renders the revenue and reserves trend chart.
        /// </summary>
        private void RenderRevenueChart()
        {
            try
            {
                if (_vm?.Projections == null || _vm.Projections.Count == 0)
                    return;

                _revenueChart.Series.Clear();
                _revenueChart.PrimaryXAxis.Title = "Year";
                _revenueChart.PrimaryYAxis.Title = "Amount ($)";

                // Revenue series
                var revenueSeries = new ChartSeries
                {
                    Name = "Revenue",
                    Type = ChartSeriesType.Line
                };

                foreach (var proj in _vm.Projections)
                {
                    revenueSeries.Points.Add(proj.Year, (double)proj.ProjectedRevenue);
                }

                _revenueChart.Series.Add(revenueSeries);

                // Expenses series
                var expenseSeries = new ChartSeries
                {
                    Name = "Expenses",
                    Type = ChartSeriesType.Line
                };

                foreach (var proj in _vm.Projections)
                {
                    expenseSeries.Points.Add(proj.Year, (double)proj.ProjectedExpenses);
                }

                _revenueChart.Series.Add(expenseSeries);

                // Refresh chart
                _revenueChart.Refresh();
                _logger?.LogDebug("Revenue chart rendered with {Count} projections", _vm.Projections.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error rendering revenue chart");
            }
        }

        /// <summary>
        /// Renders the department impact column chart.
        /// </summary>
        private void RenderDepartmentChart()
        {
            try
            {
                if (_vm?.DepartmentImpacts == null || _vm.DepartmentImpacts.Count == 0)
                    return;

                _departmentChart.Series.Clear();
                _departmentChart.PrimaryXAxis.Title = "Department";
                _departmentChart.PrimaryYAxis.Title = "Budget Impact ($)";

                var impactSeries = new ChartSeries
                {
                    Name = "Impact Amount",
                    Type = ChartSeriesType.Column
                };

                foreach (var impact in _vm.DepartmentImpacts)
                {
                    impactSeries.Points.Add(impact.DepartmentName, (double)impact.ImpactAmount);
                }

                _departmentChart.Series.Add(impactSeries);

                _departmentChart.Refresh();
                _logger?.LogDebug("Department chart rendered with {Count} departments", _vm.DepartmentImpacts.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error rendering department chart");
            }
        }

        /// <summary>
        /// Applies theme via SfSkinManager.
        /// </summary>
        private void ApplyTheme()
        {
            try
            {
                SfSkinManager.SetVisualStyle(this, AppThemeColors.DefaultTheme);
                SfSkinManager.SetVisualStyle(_topPanel, AppThemeColors.DefaultTheme);
                SfSkinManager.SetVisualStyle(_projectionsGrid, AppThemeColors.DefaultTheme);
                SfSkinManager.SetVisualStyle(_departmentImpactGrid, AppThemeColors.DefaultTheme);
                SfSkinManager.SetVisualStyle(_revenueChart, AppThemeColors.DefaultTheme);
                SfSkinManager.SetVisualStyle(_departmentChart, AppThemeColors.DefaultTheme);

                _logger?.LogDebug("Theme applied to WarRoomPanel");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to apply theme");
            }
        }

        /// <summary>
        /// Resolves the WarRoomViewModel from DI.
        /// </summary>
        private static WarRoomViewModel ResolveWarRoomViewModel()
        {
            if (Program.Services == null)
            {
                Serilog.Log.Warning("WarRoomPanel: Program.Services is null - using fallback ViewModel");
                return new WarRoomViewModel();
            }

            try
            {
                var vm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                    .GetService<WarRoomViewModel>(Program.Services);

                if (vm != null)
                {
                    Serilog.Log.Debug("WarRoomPanel: ViewModel resolved from DI container");
                    return vm;
                }

                Serilog.Log.Warning("WarRoomPanel: ViewModel not registered - using fallback");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "WarRoomPanel: Failed to resolve ViewModel");
            }

            return new WarRoomViewModel();
        }

        private static Services.IThemeService? ResolveThemeService()
        {
            if (Program.Services == null) return null;

            try
            {
                return Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                    .GetService<Services.IThemeService>(Program.Services);
            }
            catch { return null; }
        }

        private static ILogger<WarRoomPanel>? ResolveLogger()
        {
            if (Program.Services == null) return null;

            try
            {
                return Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                    .GetService<ILogger<WarRoomPanel>>(Program.Services);
            }
            catch { return null; }
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Name = "WarRoomPanel";
            this.Size = new System.Drawing.Size(1000, 800);
            this.ResumeLayout(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    if (_vm != null)
                        _vm.PropertyChanged -= ViewModel_PropertyChanged;

                    _sharedTooltip?.Dispose();
                    _topPanel?.Dispose();
                    _panelHeader?.Dispose();
                    _loadingOverlay?.Dispose();
                    _scenarioInput?.Dispose();
                    _btnRunScenario?.Dispose();
                    _revenueChart?.Dispose();
                    _departmentChart?.Dispose();
                    _projectionsGrid?.Dispose();
                    _departmentImpactGrid?.Dispose();
                    _riskGauge?.Dispose();

                    _logger?.LogDebug("WarRoomPanel disposed successfully");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error during WarRoomPanel disposal");
                }
            }

            base.Dispose(disposing);
        }
    }
}
