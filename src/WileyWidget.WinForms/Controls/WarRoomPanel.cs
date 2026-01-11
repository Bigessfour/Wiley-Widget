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
using WileyWidget.WinForms.Utils;

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
    /// Production-Ready: Full validation, databinding, error handling, sizing, accessibility.
    /// </summary>
    public partial class WarRoomPanel : UserControl
    {
        /// <summary>
        /// Simple DataContext wrapper for host compatibility.
        /// </summary>
        public new object? DataContext { get; private set; }

        private readonly WarRoomViewModel _vm;
        private readonly ILogger<WarRoomPanel>? _logger;

        private GradientPanelExt _topPanel = null!;
        private PanelHeader? _panelHeader;
        private LoadingOverlay? _loadingOverlay;
        private Label _lblStatus = null!;

        // Input controls
        private TextBox _scenarioInput = null!;
        private Button _btnRunScenario = null!;
        private Label _lblVoiceHint = null!;
        private Label _lblInputError = null!;

        // Results panels
        private Panel _resultsPanel = null!;
        private Label _lblNoResults = null!;

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

        private Panel _contentPanel = null!;

        [Obsolete("Use DI constructor with WarRoomViewModel and ILogger parameters", false)]
        public WarRoomPanel() : this(
            Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WarRoomViewModel>(Program.Services!) ?? new WarRoomViewModel(),
            Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ILogger<WarRoomPanel>>(Program.Services!))
        {
        }

        public WarRoomPanel(WarRoomViewModel viewModel, ILogger<WarRoomPanel>? logger = null)
        {
            InitializeComponent();
            _logger = logger;
            _vm = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = _vm;
            _logger?.LogInformation("WarRoomPanel initializing");
            InitializeUI();
            BindViewModel();

            // Defer sizing validation for complex WarRoom layouts with charts and grids
            this.BeginInvoke(new System.Action(() => SafeControlSizeValidator.TryAdjustConstrainedSize(this, out _, out _)));

            _logger?.LogInformation("WarRoomPanel initialized successfully");
        }

        /// <summary>
        /// Initializes all UI controls with proper layout and databinding.
        /// Theme is applied globally via application startup.
        /// </summary>
        private void InitializeUI()
        {
            try
            {
                BuildTopInputPanel();
                BuildContentArea();
                _logger?.LogDebug("WarRoomPanel UI initialized successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error initializing WarRoomPanel UI");
                throw;
            }
        }

        private void BuildTopInputPanel()
        {
            _topPanel = new GradientPanelExt
            {
                MinimumSize = new Size(0, 160),
                Height = 160,
                Dock = DockStyle.Top,
                Padding = new Padding(12),
                Name = "WarRoomTopPanel",
                AccessibleName = "War Room Input Panel",
                AccessibleDescription = "Contains the scenario input field and run button",
                AccessibleRole = AccessibleRole.Pane
            };
            Controls.Add(_topPanel);

            _panelHeader = new PanelHeader
            {
                Dock = DockStyle.Top,
                Name = "WarRoomPanelHeader",
                AccessibleName = "War Room Header",
                AccessibleDescription = "Panel title and quick actions"
            };
            _panelHeader.Title = "War Room";
            _topPanel.Controls.Add(_panelHeader);

            var topBodyLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0),
                Name = "TopInputBodyLayout"
            };
            topBodyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F));
            topBodyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            _topPanel.Controls.Add(topBodyLayout);

            var inputGroupBox = new GroupBox
            {
                Text = "Scenario Input",
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                Margin = new Padding(0),
                Name = "ScenarioInputGroup",
                AccessibleName = "Scenario Input Group",
                AccessibleDescription = "Enter scenarios and run the analysis"
            };
            topBodyLayout.Controls.Add(inputGroupBox, 0, 0);

            var scenarioLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                AutoSize = true,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            scenarioLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            scenarioLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            scenarioLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            scenarioLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            scenarioLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            inputGroupBox.Controls.Add(scenarioLayout);

            _scenarioInput = new TextBox
            {
                Text = "Raise water rates 12% and inflation is 4% for 5 years",
                Multiline = false,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 8, 0),
                Name = "ScenarioInput",
                AccessibleName = "Scenario Input",
                AccessibleDescription = "Enter your scenario in natural language (e.g., 'Raise rates 10% for 3 years')",
                MaxLength = 500
            };
            scenarioLayout.Controls.Add(_scenarioInput, 0, 0);

            _btnRunScenario = new Button
            {
                Text = "Run Scenario",
                AutoSize = true,
                Anchor = AnchorStyles.Right,
                Margin = new Padding(0),
                Name = "RunScenarioButton",
                AccessibleName = "Run Scenario",
                AccessibleDescription = "Click to analyze the scenario with AI"
            };
            scenarioLayout.Controls.Add(_btnRunScenario, 1, 0);

            _lblInputError = new Label
            {
                Text = string.Empty,
                AutoSize = true,
                Margin = new Padding(0, 4, 0, 0),
                Name = "InputErrorLabel",
                AccessibleName = "Input Error",
                AccessibleDescription = "Displays validation errors for scenario input",
                ForeColor = Color.Red,
                Font = new Font("Segoe UI", 8F, FontStyle.Regular),
                Visible = false
            };
            scenarioLayout.Controls.Add(_lblInputError, 0, 1);
            scenarioLayout.SetColumnSpan(_lblInputError, 2);

            _lblVoiceHint = new Label
            {
                Text = "💬 Or ask JARVIS aloud",
                AutoSize = true,
                Margin = new Padding(0, 4, 0, 0),
                Font = new Font("Segoe UI", 9F, FontStyle.Italic),
                Name = "VoiceHint",
                AccessibleName = "Voice Hint",
                AccessibleDescription = "Hint to use voice input with JARVIS"
            };
            scenarioLayout.Controls.Add(_lblVoiceHint, 0, 2);
            scenarioLayout.SetColumnSpan(_lblVoiceHint, 2);

            var statusHostPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(8, 4, 8, 4),
                Margin = new Padding(12, 0, 0, 0),
                Name = "StatusHostPanel",
                AccessibleName = "War Room Status Panel",
                AccessibleDescription = "Shows whether the panel is ready or analyzing"
            };
            topBodyLayout.Controls.Add(statusHostPanel, 1, 0);

            _lblStatus = new Label
            {
                Text = "Ready",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                AutoSize = false,
                Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                Name = "StatusLabel",
                AccessibleName = "Status",
                AccessibleDescription = "Current processing status (Ready, Analyzing)"
            };
            statusHostPanel.Controls.Add(_lblStatus);
        }

        private void BuildContentArea()
        {
            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Name = "ContentPanel",
                Padding = new Padding(0),
                AccessibleName = "War Room Content",
                AccessibleDescription = "Hosts the results layout and placeholders"
            };
            Controls.Add(_contentPanel);

            _resultsPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Name = "ResultsPanel",
                Visible = false,
                AccessibleName = "Results Panel",
                AccessibleDescription = "Displays charts and grids after a scenario completes"
            };
            _contentPanel.Controls.Add(_resultsPanel);

            _lblNoResults = new Label
            {
                Text = "Run a scenario to see results...",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 12F, FontStyle.Italic),
                Name = "NoResultsLabel",
                AccessibleName = "No Results",
                AccessibleDescription = "Prompt displayed when there are no scenario results",
                Margin = new Padding(12)
            };
            _contentPanel.Controls.Add(_lblNoResults);
            _lblNoResults.BringToFront();

            _loadingOverlay = new LoadingOverlay
            {
                Dock = DockStyle.Fill,
                Visible = false,
                Name = "WarRoomLoadingOverlay",
                AccessibleName = "War Room Loading Overlay",
                AccessibleDescription = "Indicates that a scenario is currently being analyzed"
            };
            Controls.Add(_loadingOverlay);
            _loadingOverlay.BringToFront();

            BuildResultsLayout();
        }

        private void BuildResultsLayout()
        {
            var resultsLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(12),
                Margin = new Padding(0),
                Name = "ResultsLayout",
                AccessibleName = "Results Layout",
                AccessibleDescription = "Shows headline, charts, and grids for scenario results"
            };
            resultsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            resultsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 60F));
            resultsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));
            _resultsPanel.Controls.Add(resultsLayout);

            var headlineLayout = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = 1,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 12),
                Padding = new Padding(0)
            };
            headlineLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
            headlineLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            resultsLayout.Controls.Add(headlineLayout, 0, 0);

            var headlineGroup = new GroupBox
            {
                Text = "Scenario Result",
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                Margin = new Padding(0, 0, 8, 0),
                AccessibleName = "Rate Increase Summary",
                AccessibleDescription = "Displays the required rate increase headline"
            };
            headlineLayout.Controls.Add(headlineGroup, 0, 0);

            var headlineStack = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            headlineStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            headlineStack.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            headlineGroup.Controls.Add(headlineStack);

            _lblRateIncreaseHeadline = new Label
            {
                Text = "Required Rate Increase:",
                Dock = DockStyle.Top,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = true,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Name = "RateIncreaseLabel",
                AccessibleName = "Rate Increase Label",
                AccessibleDescription = "Intro text for the required rate increase value"
            };
            headlineStack.Controls.Add(_lblRateIncreaseHeadline, 0, 0);

            _lblRateIncreaseValue = new Label
            {
                Text = "—",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 48F, FontStyle.Bold),
                Name = "RateIncreaseValue",
                AccessibleName = "Required Rate Increase Value",
                AccessibleDescription = "Displays the required percentage increase for the scenario"
            };
            headlineStack.Controls.Add(_lblRateIncreaseValue, 0, 1);

            var gaugeGroup = new GroupBox
            {
                Text = "Risk Assessment",
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                AccessibleName = "Risk Assessment",
                AccessibleDescription = "Radial gauge showing the scenario risk level"
            };
            headlineLayout.Controls.Add(gaugeGroup, 1, 0);

            _riskGauge = new RadialGauge
            {
                Dock = DockStyle.Fill,
                Name = "RiskGauge",
                MinimumValue = 0,
                MaximumValue = 100,
                Value = 0,
                AccessibleName = "Risk Level Gauge",
                AccessibleDescription = "Risk level from 0 (safe) to 100 (critical)"
            };
            gaugeGroup.Controls.Add(_riskGauge);

            var chartSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                // NOTE: SplitterDistance removed from initializer - must be set AFTER control sizing
                // Per Microsoft docs (https://learn.microsoft.com/dotnet/api/system.windows.forms.splitcontainer):
                // "SplitterDistance must be between Panel1MinSize and Width - Panel2MinSize"
                // Setting before control has size causes InvalidOperationException
                SplitterWidth = 6,
                Panel1MinSize = 220,
                Panel2MinSize = 220,
                Margin = new Padding(0, 0, 0, 12),
                Name = "ChartSplitContainer",
                AccessibleName = "Chart Splitter",
                AccessibleDescription = "Adjusts space between revenue and department charts"
            };

            // Defer SplitterDistance until after handle is created and control is sized
            // Per Microsoft docs: "Use SplitterDistance to specify where the splitter starts on your form"
            // Constraint: SplitterDistance must be >= Panel1MinSize and <= (Width - Panel2MinSize)
            chartSplit.HandleCreated += (s, e) =>
            {
                if (chartSplit.Width > 0)
                {
                    int maxDistance = chartSplit.Width - chartSplit.Panel2MinSize;
                    int desiredDistance = Math.Max(chartSplit.Panel1MinSize, Math.Min(400, maxDistance));
                    chartSplit.SplitterDistance = desiredDistance;
                }
            };

            // Defer SplitterDistance until after handle is created and control is sized (Microsoft-documented pattern)
            chartSplit.HandleCreated += (s, e) =>
            {
                if (chartSplit.Width > 0)
                {
                    // Calculate valid distance respecting constraints: Panel1MinSize <= distance <= (Width - Panel2MinSize)
                    int maxDistance = chartSplit.Width - chartSplit.Panel2MinSize - chartSplit.SplitterWidth;
                    int desiredDistance = 520; // Original desired value
                    int safeDistance = Math.Max(chartSplit.Panel1MinSize, Math.Min(desiredDistance, maxDistance));
                    chartSplit.SplitterDistance = safeDistance;
                }
            };

            resultsLayout.Controls.Add(chartSplit, 0, 1);

            var revenueChartGroup = new GroupBox
            {
                Text = "Revenue & Expenses Projection",
                Dock = DockStyle.Fill,
                Padding = new Padding(8),
                Margin = new Padding(0, 0, 8, 0),
                AccessibleName = "Revenue chart",
                AccessibleDescription = "Displays projected revenue and expense trends"
            };
            revenueChartGroup.MinimumSize = new Size(260, 220);
            chartSplit.Panel1.Controls.Add(revenueChartGroup);

            _revenueChart = new ChartControl
            {
                Dock = DockStyle.Fill,
                Name = "RevenueChart",
                Palette = ChartColorPalette.GrayScale,
                AccessibleName = "Revenue & Expense Chart",
                AccessibleDescription = "Line chart showing revenue and expense projections"
            };
            revenueChartGroup.Controls.Add(_revenueChart);

            var departmentChartGroup = new GroupBox
            {
                Text = "Department Budget Impact",
                Dock = DockStyle.Fill,
                Padding = new Padding(8),
                AccessibleName = "Department chart",
                AccessibleDescription = "Shows the budget impact per department"
            };
            departmentChartGroup.MinimumSize = new Size(260, 220);
            chartSplit.Panel2.Controls.Add(departmentChartGroup);

            _departmentChart = new ChartControl
            {
                Dock = DockStyle.Fill,
                Name = "DepartmentChart",
                Palette = ChartColorPalette.GrayScale,
                AccessibleName = "Department Impact Chart",
                AccessibleDescription = "Column chart showing department impacts"
            };
            departmentChartGroup.Controls.Add(_departmentChart);

            var gridsSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterWidth = 6,
                Panel1MinSize = 180,
                Panel2MinSize = 180,
                AccessibleName = "Grids Splitter",
                AccessibleDescription = "Adjusts space between projections and department grids"
            };
            SafeSplitterDistanceHelper.TrySetSplitterDistance(gridsSplit, 220);
            resultsLayout.Controls.Add(gridsSplit, 0, 2);

            var projectionsGroup = new GroupBox
            {
                Text = "Year-by-Year Projections",
                Dock = DockStyle.Fill,
                Padding = new Padding(8),
                Margin = new Padding(0, 0, 0, 6),
                AccessibleName = "Projections Grid Group",
                AccessibleDescription = "Shows the year-by-year projection table"
            };
            projectionsGroup.MinimumSize = new Size(0, 180);
            gridsSplit.Panel1.Controls.Add(projectionsGroup);

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
                AutoGenerateColumns = false,
                SelectionMode = GridSelectionMode.Single,
                AccessibleName = "Projections Grid",
                AccessibleDescription = "Year-by-year financial projections table"
            };
            projectionsGroup.Controls.Add(_projectionsGrid);

            var departmentGroup = new GroupBox
            {
                Text = "Department Impact Analysis",
                Dock = DockStyle.Fill,
                Padding = new Padding(8),
                AccessibleName = "Department Impact Grid Group",
                AccessibleDescription = "Shows department impact values"
            };
            departmentGroup.MinimumSize = new Size(0, 180);
            gridsSplit.Panel2.Controls.Add(departmentGroup);

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
                AutoGenerateColumns = false,
                SelectionMode = GridSelectionMode.Single,
                AccessibleName = "Department Impact Grid",
                AccessibleDescription = "Impact analysis by department"
            };
            departmentGroup.Controls.Add(_departmentImpactGrid);

            ConfigureProjectionsGrid();
            ConfigureDepartmentImpactGrid();
        }

        /// <summary>
        /// Configures the projections grid columns with proper formatting and alignment.
        /// </summary>
        private void ConfigureProjectionsGrid()
        {
            try
            {
                if (_projectionsGrid == null)
                {
                    _logger?.LogWarning("ProjectionsGrid is null - cannot configure");
                    return;
                }

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
                    HeaderText = "Reserve (3mo)",
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
        /// Configures the department impact grid columns with proper formatting and alignment.
        /// </summary>
        private void ConfigureDepartmentImpactGrid()
        {
            try
            {
                if (_departmentImpactGrid == null)
                {
                    _logger?.LogWarning("DepartmentImpactGrid is null - cannot configure");
                    return;
                }

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
        /// Binds the ViewModel to UI controls with comprehensive error handling and validation.
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
                // Validate grids exist before binding
                if (_projectionsGrid != null && _vm.Projections != null)
                {
                    _projectionsGrid.DataSource = _vm.Projections;
                }
                else
                {
                    _logger?.LogWarning("ProjectionsGrid or Projections collection is null");
                }

                if (_departmentImpactGrid != null && _vm.DepartmentImpacts != null)
                {
                    _departmentImpactGrid.DataSource = _vm.DepartmentImpacts;
                }
                else
                {
                    _logger?.LogWarning("DepartmentImpactGrid or DepartmentImpacts collection is null");
                }

                // Subscribe to ViewModel property changes
                _vm.PropertyChanged += ViewModel_PropertyChanged;

                // Wire up Run Scenario command with error handling
                _btnRunScenario.Click += async (s, e) =>
                {
                    try
                    {
                        _lblInputError.Visible = false;

                        if (string.IsNullOrWhiteSpace(_scenarioInput.Text))
                        {
                            _lblInputError.Text = "Please enter a scenario";
                            _lblInputError.Visible = true;
                            return;
                        }

                        if (_scenarioInput.Text.Length < 10)
                        {
                            _lblInputError.Text = "Scenario description too short (minimum 10 characters)";
                            _lblInputError.Visible = true;
                            return;
                        }

                        await _vm.RunScenarioCommand.ExecuteAsync(null);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error in Run Scenario click handler");
                        _lblInputError.Text = $"Error: {ex.Message}";
                        _lblInputError.Visible = true;
                        MessageBox.Show($"Failed to run scenario: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                };

                // Wire up input textbox with validation
                _scenarioInput.TextChanged += (s, e) =>
                {
                    _vm.ScenarioInput = _scenarioInput.Text;
                    _lblInputError.Visible = false; // Clear error on input change
                };

                _logger?.LogInformation("ViewModel bound successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error binding ViewModel");
            }
        }

        /// <summary>
        /// Handles ViewModel property changes with proper null checking and UI updates.
        /// </summary>
        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            try
            {
                switch (e?.PropertyName)
                {
                    case nameof(WarRoomViewModel.StatusMessage):
                        if (_lblStatus != null)
                        {
                            _lblStatus.Text = _vm?.StatusMessage ?? "Ready";
                            _lblStatus.Refresh();
                        }
                        break;

                    case nameof(WarRoomViewModel.IsAnalyzing):
                        if (_loadingOverlay != null)
                        {
                            _loadingOverlay.Visible = _vm?.IsAnalyzing ?? false;
                            if (_loadingOverlay.Visible)
                            {
                                _loadingOverlay.BringToFront();
                            }
                        }
                        break;

                    case nameof(WarRoomViewModel.HasResults):
                        bool hasResults = _vm?.HasResults ?? false;
                        if (_resultsPanel != null)
                        {
                            _resultsPanel.Visible = hasResults;
                        }
                        if (_lblNoResults != null)
                        {
                            _lblNoResults.Visible = !hasResults;
                        }
                        break;

                    case nameof(WarRoomViewModel.RequiredRateIncrease):
                        if (_lblRateIncreaseValue != null)
                        {
                            _lblRateIncreaseValue.Text = _vm?.RequiredRateIncrease ?? "—";
                        }
                        break;

                    case nameof(WarRoomViewModel.RiskLevel):
                        if (_riskGauge != null)
                        {
                            _riskGauge.Value = (float)(_vm?.RiskLevel ?? 0);
                        }
                        break;

                    case nameof(WarRoomViewModel.Projections):
                        // Validate before rendering
                        if (_vm?.Projections != null && _vm.Projections.Count > 0)
                        {
                            RenderRevenueChart();
                        }
                        break;

                    case nameof(WarRoomViewModel.DepartmentImpacts):
                        // Validate before rendering
                        if (_vm?.DepartmentImpacts != null && _vm.DepartmentImpacts.Count > 0)
                        {
                            RenderDepartmentChart();
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling ViewModel property change: {PropertyName}", e?.PropertyName);
            }
        }

        /// <summary>
        /// Renders the revenue and expenses trend chart with validation.
        /// </summary>
        private void RenderRevenueChart()
        {
            try
            {
                // Validate all preconditions
                if (_revenueChart == null)
                {
                    _logger?.LogWarning("RevenueChart is null - cannot render");
                    return;
                }

                if (_vm?.Projections == null || _vm.Projections.Count == 0)
                {
                    _logger?.LogDebug("No projections data - skipping revenue chart render");
                    return;
                }

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
        /// Renders the department impact column chart with validation.
        /// </summary>
        private void RenderDepartmentChart()
        {
            try
            {
                // Validate all preconditions
                if (_departmentChart == null)
                {
                    _logger?.LogWarning("DepartmentChart is null - cannot render");
                    return;
                }

                if (_vm?.DepartmentImpacts == null || _vm.DepartmentImpacts.Count == 0)
                {
                    _logger?.LogDebug("No department impacts data - skipping department chart render");
                    return;
                }

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

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Name = "WarRoomPanel";
            this.Size = new System.Drawing.Size(1200, 900);
            this.MinimumSize = new System.Drawing.Size(1024, 720);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ResumeLayout(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    if (_vm != null)
                    {
                        _vm.PropertyChanged -= ViewModel_PropertyChanged;
                    }

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
