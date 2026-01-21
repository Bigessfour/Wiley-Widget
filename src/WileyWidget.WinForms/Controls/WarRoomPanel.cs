#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.Drawing;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.Windows.Forms.Chart;
using Syncfusion.Windows.Forms.Gauge;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Utils;
using WileyWidget.WinForms.ViewModels;

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
    public partial class WarRoomPanel : ScopedPanelBase<WarRoomViewModel>
    {
        /// <summary>
        /// Simple DataContext wrapper for host compatibility.
        /// </summary>
        public new object? DataContext { get; private set; }

        // Event handler storage for proper cleanup in Dispose
        private EventHandler? _btnRunScenarioClickHandler;
        private PropertyChangedEventHandler? _viewModelPropertyChangedHandler;
        private EventHandler? _scenarioInputTextChangedHandler;

        private GradientPanelExt _topPanel = null!;
        private PanelHeader? _panelHeader;
        private LoadingOverlay? _loadingOverlay;
        private Label _lblStatus = null!;

        // Input controls
        private TextBox _scenarioInput = null!;
        private Syncfusion.WinForms.Controls.SfButton _btnRunScenario = null!;
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
        private ErrorProvider? _errorProvider;

        // Obsolete parameterless constructor for designer compatibility
        [Obsolete("Use DI constructor with IServiceScopeFactory and ILogger", false)]
        public WarRoomPanel()
            : this(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(Program.Services!),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<ScopedPanelBase<WarRoomViewModel>>>(Program.Services!))
        {
        }

        // Primary DI constructor
        public WarRoomPanel(
            IServiceScopeFactory scopeFactory,
            ILogger<ScopedPanelBase<WarRoomViewModel>> logger)
            : base(scopeFactory, logger)
        {
            _logger?.LogInformation("WarRoomPanel initializing");

            InitializeComponent();

            // Apply theme via SfSkinManager (single source of truth)
            try
            {
                SfSkinManager.SetVisualStyle(this, SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme);
            }
            catch { }

            BindViewModel();

            // Defer sizing validation for complex WarRoom layouts with charts and grids
            DeferSizeValidation();

            _logger?.LogInformation("WarRoomPanel initialized successfully");
        }

        /// <summary>
        /// Initializes all UI controls with proper layout and databinding.
        /// </summary>
        private void InitializeComponent()
        {
            try
            {
                // Initialize ErrorProvider first (used in input validation)
                _errorProvider = new ErrorProvider
                {
                    BlinkStyle = ErrorBlinkStyle.NeverBlink,
                    BlinkRate = 0,
                    Icon = null!,
                    ContainerControl = this
                };

                BuildTopInputPanel();
                BuildContentArea();
                PerformLayout();
                Refresh();
                _logger?.LogDebug("[PANEL] {PanelName} content anchored and refreshed", this.Name);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error initializing WarRoomPanel UI");
                throw;
            }
        }

        private void DeferSizeValidation()
        {
            if (IsDisposed) return;

            if (IsHandleCreated)
            {
                try
                {
                    BeginInvoke(new Action(() => SafeControlSizeValidator.TryAdjustConstrainedSize(this, out _, out _)));
                }
                catch { }
                return;
            }

            EventHandler? handleCreatedHandler = null;
            handleCreatedHandler = (s, e) =>
            {
                HandleCreated -= handleCreatedHandler;
                if (IsDisposed) return;

                try
                {
                    BeginInvoke(new Action(() => SafeControlSizeValidator.TryAdjustConstrainedSize(this, out _, out _)));
                }
                catch { }
            };

            HandleCreated += handleCreatedHandler;
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

            _btnRunScenario = new Syncfusion.WinForms.Controls.SfButton
            {
                Text = "Run Scenario",
                AutoSize = false,
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
                AutoSize = false,
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
                Text = "💬 Or ask JARVIS aloud using voice input (if available in your installation)",
                AutoSize = false,
                Margin = new Padding(0, 4, 0, 0),
                Font = new Font("Segoe UI", 9F, FontStyle.Italic),
                Name = "VoiceHint",
                AccessibleName = "Voice Hint",
                AccessibleDescription = "Hint to use voice input with JARVIS assistant (Alt+V)"
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
                SplitterWidth = 6,
                Margin = new Padding(0, 0, 0, 12),
                Name = "ChartSplitContainer",
                AccessibleName = "Chart Splitter",
                AccessibleDescription = "Adjusts space between revenue and department charts"
            };

            SafeSplitterDistanceHelper.ConfigureSafeSplitContainerAdvanced(
                chartSplit,
                panel1MinSize: 220,
                panel2MinSize: 220,
                desiredDistance: 520,
                splitterWidth: 6);

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
                AccessibleName = "Grids Splitter",
                AccessibleDescription = "Adjusts space between projections and department grids"
            };
            SafeSplitterDistanceHelper.ConfigureSafeSplitContainerAdvanced(
                gridsSplit,
                panel1MinSize: 180,
                panel2MinSize: 180,
                desiredDistance: 220,
                splitterWidth: 6);
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

            this.PerformLayout();
            this.Refresh();

            _logger?.LogDebug("[PANEL] {PanelName} content anchored and refreshed", this.Name);
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
        /// Binds the ViewModel to UI controls with event handler storage for proper cleanup.
        /// </summary>
        private void BindViewModel()
        {
            if (ViewModel == null)
            {
                _logger?.LogWarning("ViewModel is null - cannot bind");
                return;
            }

            try
            {
                // Validate grids exist before binding
                if (_projectionsGrid != null && ViewModel.Projections != null)
                {
                    _projectionsGrid.DataSource = ViewModel.Projections;
                }
                else
                {
                    _logger?.LogWarning("ProjectionsGrid or Projections collection is null");
                }

                if (_departmentImpactGrid != null && ViewModel.DepartmentImpacts != null)
                {
                    _departmentImpactGrid.DataSource = ViewModel.DepartmentImpacts;
                }
                else
                {
                    _logger?.LogWarning("DepartmentImpactGrid or DepartmentImpacts collection is null");
                }

                // Subscribe to ViewModel property changes (with stored delegate for cleanup)
                _viewModelPropertyChangedHandler = ViewModel_PropertyChanged;
                ViewModel.PropertyChanged += _viewModelPropertyChangedHandler;

                // Wire up Run Scenario button with stored handler for cleanup
                _btnRunScenarioClickHandler = async (s, e) => await OnRunScenarioClickAsync();
                _btnRunScenario.Click += _btnRunScenarioClickHandler;

                // Wire up scenario input textbox with stored handler for cleanup
                _scenarioInputTextChangedHandler = (s, e) => OnScenarioInputTextChanged();
                _scenarioInput.TextChanged += _scenarioInputTextChangedHandler;

                _logger?.LogInformation("ViewModel bound successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error binding ViewModel");
            }
        }

        /// <summary>
        /// Handles Run Scenario button click with validation and busy state tracking.
        /// </summary>
        private async Task OnRunScenarioClickAsync()
        {
            try
            {
                _lblInputError.Visible = false;

                if (string.IsNullOrWhiteSpace(_scenarioInput.Text))
                {
                    _lblInputError.Text = "Please enter a scenario";
                    _lblInputError.Visible = true;
                    _scenarioInput.Focus();
                    return;
                }

                if (_scenarioInput.Text.Length < 10)
                {
                    _lblInputError.Text = "Scenario description too short (minimum 10 characters)";
                    _lblInputError.Visible = true;
                    _scenarioInput.Focus();
                    return;
                }

                if (ViewModel?.RunScenarioCommand == null)
                {
                    _logger?.LogError("RunScenarioCommand is null");
                    _lblInputError.Text = "Command not available";
                    _lblInputError.Visible = true;
                    return;
                }

                // Set busy state during scenario analysis
                var token = RegisterOperation();
                IsBusy = true;

                try
                {
                    await ViewModel.RunScenarioCommand.ExecuteAsync(token);
                    _logger?.LogInformation("Scenario analysis completed successfully");
                }
                finally
                {
                    IsBusy = false;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in Run Scenario handler");
                _lblInputError.Text = $"Error: {ex.Message}";
                _lblInputError.Visible = true;
                IsBusy = false;
                MessageBox.Show($"Failed to run scenario: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Handles scenario input text changes and marks panel as dirty.
        /// </summary>
        private void OnScenarioInputTextChanged()
        {
            if (ViewModel != null)
            {
                ViewModel.ScenarioInput = _scenarioInput.Text;
                SetHasUnsavedChanges(true); // Mark as dirty on user edit
                _lblInputError.Visible = false; // Clear error on input change
            }
        }

        /// <summary>
        /// Handles ViewModel property changes with proper null checking and UI updates.
        /// </summary>
        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            try
            {
                // Verify ViewModel still exists (may be disposed)
                if (ViewModel == null || IsDisposed)
                    return;

                switch (e?.PropertyName)
                {
                    case nameof(WarRoomViewModel.StatusMessage):
                        if (_lblStatus != null)
                        {
                            _lblStatus.Text = ViewModel.StatusMessage ?? "Ready";
                            _lblStatus.Refresh();
                        }
                        break;

                    case nameof(WarRoomViewModel.IsAnalyzing):
                        if (_loadingOverlay != null)
                        {
                            _loadingOverlay.Visible = ViewModel.IsAnalyzing;
                            if (_loadingOverlay.Visible)
                            {
                                _loadingOverlay.BringToFront();
                            }
                        }
                        break;

                    case nameof(WarRoomViewModel.HasResults):
                        bool hasResults = ViewModel.HasResults;
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
                            _lblRateIncreaseValue.Text = ViewModel.RequiredRateIncrease ?? "—";
                        }
                        break;

                    case nameof(WarRoomViewModel.RiskLevel):
                        if (_riskGauge != null)
                        {
                            _riskGauge.Value = (float)ViewModel.RiskLevel;
                        }
                        break;

                    case nameof(WarRoomViewModel.Projections):
                        // Validate before rendering
                        if (ViewModel.Projections != null && ViewModel.Projections.Count > 0)
                        {
                            RenderRevenueChart();
                        }
                        break;

                    case nameof(WarRoomViewModel.DepartmentImpacts):
                        // Validate before rendering
                        if (ViewModel.DepartmentImpacts != null && ViewModel.DepartmentImpacts.Count > 0)
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

                if (ViewModel?.Projections == null || ViewModel.Projections.Count == 0)
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

                foreach (var proj in ViewModel.Projections)
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

                foreach (var proj in ViewModel.Projections)
                {
                    expenseSeries.Points.Add(proj.Year, (double)proj.ProjectedExpenses);
                }

                _revenueChart.Series.Add(expenseSeries);

                // Refresh chart
                _revenueChart.Refresh();
                _logger?.LogDebug("Revenue chart rendered with {Count} projections", ViewModel.Projections.Count);
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

                if (ViewModel?.DepartmentImpacts == null || ViewModel.DepartmentImpacts.Count == 0)
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

                foreach (var impact in ViewModel.DepartmentImpacts)
                {
                    impactSeries.Points.Add(impact.DepartmentName, (double)impact.ImpactAmount);
                }

                _departmentChart.Series.Add(impactSeries);

                _departmentChart.Refresh();
                _logger?.LogDebug("Department chart rendered with {Count} departments", ViewModel.DepartmentImpacts.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error rendering department chart");
            }
        }

        /// <summary>
        /// Loads scenario data and initializes the panel.
        /// </summary>
        public override async Task LoadAsync(CancellationToken ct)
        {
            if (IsLoaded) return;

            try
            {
                IsBusy = true;

                // Pre-load any cached scenarios or default data
                if (ViewModel != null && !DesignMode)
                {
                    _logger?.LogInformation("Loading WarRoom scenario data");
                    // Initialize ViewModel with any default scenarios from database if available
                    await Task.CompletedTask;
                }

                SetHasUnsavedChanges(false);
                _logger?.LogInformation("WarRoomPanel loaded successfully");
            }
            catch (OperationCanceledException)
            {
                _logger?.LogInformation("WarRoomPanel load cancelled");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading WarRoom panel");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Saves any unsaved scenario changes (if applicable).
        /// </summary>
        public override async Task SaveAsync(CancellationToken ct)
        {
            try
            {
                if (!HasUnsavedChanges)
                {
                    _logger?.LogDebug("No unsaved changes in WarRoom panel");
                    return;
                }

                IsBusy = true;

                // Validate before saving
                var validationResult = await ValidateAsync(ct);
                if (!validationResult.IsValid)
                {
                    _logger?.LogWarning("Validation failed during save: {ErrorCount} errors", validationResult.Errors.Count);
                    FocusFirstError();
                    throw new InvalidOperationException($"Cannot save: {validationResult.Errors.Count} validation error(s)");
                }

                // Persist any scenario state if needed
                _logger?.LogInformation("Saving WarRoom scenario data");
                await Task.CompletedTask;

                SetHasUnsavedChanges(false);
                _logger?.LogInformation("WarRoomPanel saved successfully");
            }
            catch (OperationCanceledException)
            {
                _logger?.LogInformation("WarRoomPanel save cancelled");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error saving WarRoom panel");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Validates the scenario input before submission.
        /// </summary>
        public override async Task<ValidationResult> ValidateAsync(CancellationToken ct)
        {
            try
            {
                IsBusy = true;
                _errorProvider?.Clear();
                var errors = new List<ValidationItem>();

                // Validate scenario input
                if (string.IsNullOrEmpty(_scenarioInput.Text))
                {
                    var error = new ValidationItem(
                        FieldName: "ScenarioInput",
                        Message: "Scenario cannot be empty",
                        Severity: ValidationSeverity.Error
                    );
                    errors.Add(error);
                    _errorProvider?.SetError(_scenarioInput, "Required");
                }
                else if (_scenarioInput.Text.Length < 10)
                {
                    var error = new ValidationItem(
                        FieldName: "ScenarioInput",
                        Message: "Scenario must be at least 10 characters",
                        Severity: ValidationSeverity.Error
                    );
                    errors.Add(error);
                    _errorProvider?.SetError(_scenarioInput, "Too short");
                }
                else if (_scenarioInput.Text.Length > 500)
                {
                    var error = new ValidationItem(
                        FieldName: "ScenarioInput",
                        Message: "Scenario must be 500 characters or less",
                        Severity: ValidationSeverity.Error
                    );
                    errors.Add(error);
                    _errorProvider?.SetError(_scenarioInput, "Too long");
                }
                else
                {
                    // Clear error for valid input
                    _errorProvider?.SetError(_scenarioInput, "");
                }

                await Task.CompletedTask;

                return errors.Count == 0
                    ? ValidationResult.Success
                    : ValidationResult.Failed(errors.ToArray());
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error validating WarRoom panel");
                return ValidationResult.Failed(new ValidationItem(
                    FieldName: "Validation",
                    Message: ex.Message,
                    Severity: ValidationSeverity.Error
                ));
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Focuses the first validation error control.
        /// </summary>
        public override void FocusFirstError()
        {
            try
            {
                // If scenario input has error, focus it
                if (_scenarioInput != null && !string.IsNullOrEmpty(_errorProvider?.GetError(_scenarioInput)))
                {
                    _scenarioInput.Focus();
                    _scenarioInput.SelectAll();
                    return;
                }

                // Default: focus scenario input
                if (_scenarioInput != null)
                {
                    _scenarioInput.Focus();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error focusing first validation error");
            }
        }

        /// <summary>
        /// Cleans up all resources, event handlers, and child controls.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    // Unsubscribe from ViewModel events
                    if (ViewModel != null && _viewModelPropertyChangedHandler != null)
                    {
                        ViewModel.PropertyChanged -= _viewModelPropertyChangedHandler;
                    }

                    // Unsubscribe from button click handler
                    if (_btnRunScenario != null && _btnRunScenarioClickHandler != null)
                    {
                        _btnRunScenario.Click -= _btnRunScenarioClickHandler;
                    }

                    // Unsubscribe from text changed handler
                    if (_scenarioInput != null && _scenarioInputTextChangedHandler != null)
                    {
                        _scenarioInput.TextChanged -= _scenarioInputTextChangedHandler;
                    }

                    // Dispose Syncfusion controls
                    _projectionsGrid?.Dispose();
                    _departmentImpactGrid?.Dispose();
                    _revenueChart?.Dispose();
                    _departmentChart?.Dispose();
                    _riskGauge?.Dispose();

                    // Dispose containers and overlays
                    _loadingOverlay?.Dispose();
                    _topPanel?.Dispose();
                    _panelHeader?.Dispose();
                    _contentPanel?.Dispose();

                    // Dispose ErrorProvider
                    _errorProvider?.Dispose();
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error during Dispose cleanup");
                }
            }

            base.Dispose(disposing);
        }
    }
}
