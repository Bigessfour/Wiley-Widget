using Syncfusion.Data;
#nullable enable

using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.Drawing;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.Windows.Forms.Chart;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Supporting;
using WileyWidget.WinForms.Factories;
using WileyWidget.WinForms.Helpers;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Utilities;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Controls.Panels
{
    /// <summary>
    /// War Room — interactive scenario analysis panel.
    /// </summary>
    public partial class WarRoomPanel : ScopedPanelBase<WarRoomViewModel>, ICompletablePanel
    {
        // Fields declared here and used by InitializeComponent in Designer.cs
        private Panel? _topPanel;
        private PanelHeader? _panelHeader;
        private TextBoxExt? _scenarioInput;
        private SfButton? _btnRunScenario;
        private SfButton? _btnExportForecast;
        private Panel? _contentPanel;
        private Panel? _resultsPanel;
        private ChartControl? _revenueChart;
        private SfDataGrid? _projectionsGrid;
        private SfDataGrid? _departmentGrid;
        private KpiCardControl? _requiredRateCard;
        private KpiCardControl? _riskCard;
        private KpiCardControl? _baselineRevenueCard;
        private KpiCardControl? _projectedRevenueCard;
        private KpiCardControl? _deltaCard;
        private NoDataOverlay? _noDataOverlay;
        private Label? _resultsSummaryLabel;
        private Label? _statusLabel;
        private ToolTip? _toolTip;
        private bool _isViewModelBound;
        private PropertyChangedEventHandler? _viewModelPropertyChangedHandler;
        private NotifyCollectionChangedEventHandler? _projectionsCollectionChangedHandler;
        private NotifyCollectionChangedEventHandler? _departmentCollectionChangedHandler;
        private bool _dataLoaded;

        // Canonical skeleton fields
        private readonly SyncfusionControlFactory? _factory;
        private TableLayoutPanel? _content;
        private LoadingOverlay? _loader;

        private static ILogger ResolveLogger()
        {
            return Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ILogger<WarRoomPanel>>(Program.Services)
                ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<WarRoomPanel>.Instance;
        }

        private void InitializeControls()
        {
            SuspendLayout();

            var actionRowHeight = LayoutTokens.GetScaled(LayoutTokens.StandardControlHeightLarge)
                + LayoutTokens.GetScaled(LayoutTokens.ToolbarPadding.Top + LayoutTokens.ToolbarPadding.Bottom);
            var compactPanelPadding = LayoutTokens.GetScaled(LayoutTokens.PanelPaddingCompact);
            var toolbarPadding = LayoutTokens.GetScaled(new Padding(
                LayoutTokens.PanelPaddingCompact.Left,
                LayoutTokens.ToolbarPadding.Top,
                LayoutTokens.PanelPaddingCompact.Right,
                LayoutTokens.ToolbarPadding.Bottom));

            Name = "WarRoomPanel";
            AccessibleName = "War Room"; // Panel title for UI automation
            Size = ScaleLogicalToDevice(new Size(1100, 760));
            MinimumSize = ScaleLogicalToDevice(new Size(1024, 720));
            AutoScaleMode = AutoScaleMode.Dpi;
            Dock = DockStyle.Fill;
            AutoScroll = false;
            Padding = Padding.Empty;

            // Apply theme for cascade to all child controls
            SfSkinManager.SetVisualStyle(this, SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme);

            // Panel header (single source of truth for header events/state)
            _panelHeader = _factory!.CreatePanelHeader(header =>
            {
                header.Dock = DockStyle.Fill;
                header.Title = "War Room";
                header.Height = LayoutTokens.HeaderHeight;
                header.ShowHelpButton = false;
                header.ShowPinButton = false;
            });

            // Canonical _content root
            _content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = Padding.Empty,
                Margin = Padding.Empty,
                AutoSize = false,
                Name = "WarRoomPanelContent"
            };
            _content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _content.RowStyles.Add(new RowStyle(SizeType.Absolute, LayoutTokens.HeaderHeight));
            _content.RowStyles.Add(new RowStyle(SizeType.Absolute, actionRowHeight));
            _content.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            _content.Controls.Add(_panelHeader, 0, 0);

            // Top panel
            _topPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Height = actionRowHeight,
                MinimumSize = new Size(0, actionRowHeight),
                Padding = toolbarPadding
            };

            var actionsRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                AutoSize = false,
                Height = LayoutTokens.GetScaled(LayoutTokens.StandardControlHeightLarge),
                Margin = Padding.Empty,
                Padding = Padding.Empty,
            };
            actionsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            actionsRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            actionsRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            actionsRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _scenarioInput = _factory!.CreateTextBoxExt(textBox =>
            {
                textBox.Dock = DockStyle.Fill;
                textBox.Height = LayoutTokens.GetScaled(LayoutTokens.StandardControlHeightLarge);
                textBox.MinimumSize = LayoutTokens.GetScaled(new Size(320, LayoutTokens.StandardControlHeightLarge));
                textBox.Margin = new Padding(0, 0, LayoutTokens.ContentMargin, 0);
                textBox.PlaceholderText = "Describe scenario assumptions...";
            });
            actionsRow.Controls.Add(_scenarioInput, 0, 0);

            _btnRunScenario = _factory!.CreateSfButton("Run Scenario", button =>
            {
                button.Dock = DockStyle.Fill;
                button.AutoSize = false;
                button.MinimumSize = LayoutTokens.GetScaled(new Size(136, LayoutTokens.StandardControlHeightLarge));
                button.Size = LayoutTokens.GetScaled(new Size(136, LayoutTokens.StandardControlHeightLarge));
                button.Margin = new Padding(0, 0, LayoutTokens.ContentMargin, 0);
            });
            actionsRow.Controls.Add(_btnRunScenario, 1, 0);

            _btnExportForecast = _factory!.CreateSfButton("Export Forecast", button =>
            {
                button.Dock = DockStyle.Fill;
                button.AutoSize = false;
                button.MinimumSize = LayoutTokens.GetScaled(new Size(148, LayoutTokens.StandardControlHeightLarge));
                button.Size = LayoutTokens.GetScaled(new Size(148, LayoutTokens.StandardControlHeightLarge));
                button.Margin = Padding.Empty;
            });
            actionsRow.Controls.Add(_btnExportForecast, 2, 0);

            _topPanel.Controls.Add(actionsRow);
            _content.Controls.Add(_topPanel, 0, 1);

            // Content panel
            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = compactPanelPadding
            };

            _resultsPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = Padding.Empty,
                AccessibleName = "War Room Results"
            };
            _contentPanel.Controls.Add(_resultsPanel);
            InitializeResultsSurface();

            _statusLabel = new Label
            {
                Dock = DockStyle.Bottom,
                Height = LayoutTokens.GetScaled(LayoutTokens.StatusBarHeight),
                Padding = LayoutTokens.GetScaled(new Padding(LayoutTokens.ToolbarPadding.Left, 0, 0, 0)),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "Ready",
                AccessibleName = "War Room Status"
            };
            _contentPanel.Controls.Add(_statusLabel);
            _statusLabel.BringToFront();

            _content.Controls.Add(_contentPanel, 0, 2);

            // Add content root to controls
            Controls.Add(_content);

            // Loading overlay
            _loader = _factory!.CreateLoadingOverlay(overlay =>
            {
                overlay.Dock = DockStyle.Fill;
                overlay.Visible = false;
            });
            Controls.Add(_loader);

            // Tooltips
            _toolTip = new ToolTip
            {
                AutoPopDelay = 8000,
                InitialDelay = 250,
                ReshowDelay = 100,
                ShowAlways = true
            };

            if (_scenarioInput != null)
            {
                _scenarioInput.AccessibleName = "Scenario Input";
                _toolTip.SetToolTip(_scenarioInput, "Describe assumptions for the scenario analysis.");
                _scenarioInput.TextChanged += OnScenarioInputTextChanged;
            }

            if (_btnRunScenario != null)
            {
                _btnRunScenario.AccessibleName = "Run Scenario";
                _toolTip.SetToolTip(_btnRunScenario, "Run what-if analysis for the current scenario.");
            }

            if (_btnExportForecast != null)
            {
                _btnExportForecast.AccessibleName = "Export Forecast";
                _toolTip.SetToolTip(_btnExportForecast, "Export forecast results for reporting.");
            }

            // Wire events
            if (_panelHeader != null)
            {
                _panelHeader.CloseClicked += (s, e) => ClosePanel();
                _panelHeader.RefreshClicked += async (s, e) => await LoadAsync();
            }

            if (_btnRunScenario != null)
                _btnRunScenario.Click += OnRunScenarioClicked;

            if (_btnExportForecast != null)
                _btnExportForecast.Click += OnExportForecastClicked;

            ResumeLayout(false);
        }

        private void InitializeResultsSurface()
        {
            if (_resultsPanel == null || _factory == null)
            {
                return;
            }

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                Name = "WarRoomResultsLayout"
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, LayoutTokens.Dp(LayoutTokens.SummaryPanelHeight + 24)));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 48F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 52F));

            _resultsSummaryLabel = _factory.CreateLabel(label =>
            {
                label.Dock = DockStyle.Fill;
                label.AutoSize = false;
                label.Height = LayoutTokens.Dp(30);
                label.Margin = new Padding(0, 0, 0, 2);
                label.Padding = new Padding(4, 0, 0, 0);
                label.TextAlign = ContentAlignment.MiddleLeft;
                label.Font = new Font(label.Font.FontFamily, label.Font.Size + 1F, FontStyle.Bold);
                label.AccessibleName = "War Room Summary";
                label.Text = "Run a scenario to populate projections, impacts, and trend charts.";
            });
            layout.Controls.Add(_resultsSummaryLabel, 0, 0);

            var kpiBorder = _factory.CreatePanel(panel =>
            {
                panel.Dock = DockStyle.Fill;
                panel.Padding = LayoutTokens.GetScaled(LayoutTokens.ToolbarPadding);
                panel.Margin = new Padding(0, 0, 0, 2);
                panel.AccessibleName = "War Room KPI Summary";
            });
            kpiBorder.Controls.Add(CreateKpiRow());
            layout.Controls.Add(kpiBorder, 0, 1);

            _revenueChart = _factory.CreateChartControl("Scenario Revenue and Expense Trend", chart =>
            {
                chart.Dock = DockStyle.Fill;
                chart.Margin = new Padding(0, 0, 0, LayoutTokens.ContentMargin);
                chart.MinimumSize = LayoutTokens.GetScaled(new Size(0, 280));
                chart.PrimaryXAxis.Title = "Year";
                chart.PrimaryYAxis.Title = "Amount ($)";
                chart.PrimaryYAxis.Format = "C0";
                chart.PrimaryXAxis.LabelRotate = true;
                chart.PrimaryXAxis.LabelRotateAngle = 45;
                chart.ShowLegend = true;
                chart.Legend.Visible = true;
                chart.Legend.Position = ChartDock.Top;
                chart.Legend.Alignment = ChartAlignment.Center;
                chart.LegendsPlacement = ChartPlacement.Outside;
                chart.ElementsSpacing = 8;
            });
            layout.Controls.Add(_revenueChart, 0, 2);

            var gridsLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                Name = "WarRoomGridsLayout"
            };
            gridsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            gridsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            gridsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _projectionsGrid = _factory.CreateSfDataGrid(grid =>
            {
                grid.Dock = DockStyle.Fill;
                grid.Name = "WarRoomProjectionsGrid";
                grid.AllowEditing = false;
                grid.AllowDeleting = false;
                grid.AllowGrouping = false;
                grid.ShowGroupDropArea = false;
                grid.AutoSizeColumnsMode = Syncfusion.WinForms.DataGrid.Enums.AutoSizeColumnsMode.Fill;
                DisableAddNewRowSurface(grid);
            });

            _departmentGrid = _factory.CreateSfDataGrid(grid =>
            {
                grid.Dock = DockStyle.Fill;
                grid.Name = "WarRoomDepartmentImpactGrid";
                grid.AllowEditing = false;
                grid.AllowDeleting = false;
                grid.AllowGrouping = false;
                grid.ShowGroupDropArea = false;
                grid.AutoSizeColumnsMode = Syncfusion.WinForms.DataGrid.Enums.AutoSizeColumnsMode.Fill;
                DisableAddNewRowSurface(grid);
            });

            gridsLayout.Controls.Add(CreateResultsSection("Projections", _projectionsGrid, isLast: false), 0, 0);
            gridsLayout.Controls.Add(CreateResultsSection("Department Impacts", _departmentGrid, isLast: true), 1, 0);
            layout.Controls.Add(gridsLayout, 0, 3);

            _resultsPanel.Controls.Add(layout);

            _noDataOverlay = _factory.CreateNoDataOverlay(overlay =>
            {
                overlay.Dock = DockStyle.Fill;
                overlay.Message = "No results yet. Type a scenario and click Run Scenario to generate projections.";
                overlay.Visible = true;
            });
            _noDataOverlay.ShowActionButton("Run Scenario", (s, e) => OnRunScenarioClicked(s, e));
            _resultsPanel.Controls.Add(_noDataOverlay);
            _noDataOverlay.BringToFront();
        }

        private Control CreateKpiRow()
        {
            var row = _factory!.CreateTableLayoutPanel(table =>
            {
                table.Dock = DockStyle.Fill;
                table.ColumnCount = 5;
                table.RowCount = 1;
                table.Margin = Padding.Empty;
                table.Padding = Padding.Empty;
            });

            for (int i = 0; i < 5; i++)
            {
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            }
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _requiredRateCard = CreateKpiCard("Required Increase", "n/a", "Target rate change", isLast: false);
            _riskCard = CreateKpiCard("Risk Score", "0%", "Higher means more risk", isLast: false);
            _baselineRevenueCard = CreateKpiCard("Baseline Revenue", "$0", "Current monthly", isLast: false);
            _projectedRevenueCard = CreateKpiCard("Projected Revenue", "$0", "Scenario monthly", isLast: false);
            _deltaCard = CreateKpiCard("Revenue Delta", "$0", "Projected - baseline", isLast: true);

            row.Controls.Add(_requiredRateCard, 0, 0);
            row.Controls.Add(_riskCard, 1, 0);
            row.Controls.Add(_baselineRevenueCard, 2, 0);
            row.Controls.Add(_projectedRevenueCard, 3, 0);
            row.Controls.Add(_deltaCard, 4, 0);
            return row;
        }

        private KpiCardControl CreateKpiCard(string title, string value, string subtitle, bool isLast)
        {
            return _factory!.CreateKpiCardControl(card =>
            {
                card.Dock = DockStyle.Fill;
                var metricCardSpacing = LayoutTokens.GetScaled(LayoutTokens.MetricCardMargin).Right;
                card.Margin = isLast ? Padding.Empty : new Padding(0, 0, metricCardSpacing, 0);
                card.Title = title;
                card.Value = value;
                card.Subtitle = subtitle;
            });
        }

        private Panel CreateResultsSection(string title, Control content, bool isLast)
        {
            var sectionPanel = _factory!.CreatePanel(panel =>
            {
                panel.Dock = DockStyle.Fill;
                var sectionSpacing = LayoutTokens.GetScaled(LayoutTokens.ContentInnerPadding).Right;
                panel.Margin = isLast ? Padding.Empty : new Padding(0, 0, sectionSpacing, 0);
                panel.Padding = Padding.Empty;
                panel.AccessibleName = title;
            });

            var headerLabel = _factory.CreateLabel(title, label =>
            {
                label.Dock = DockStyle.Top;
                label.AutoSize = false;
                label.Height = LayoutTokens.Dp(30);
                label.Padding = new Padding(2, 0, 0, 0);
                label.TextAlign = ContentAlignment.MiddleLeft;
            });

            var contentHost = _factory.CreatePanel(panel =>
            {
                panel.Dock = DockStyle.Fill;
                panel.Padding = Padding.Empty;
            });
            contentHost.Controls.Add(content);

            sectionPanel.Controls.Add(contentHost);
            sectionPanel.Controls.Add(headerLabel);
            return sectionPanel;
        }

        /// <summary>
        /// Constructor that accepts required dependencies from DI container.
        /// </summary>
        [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
        public WarRoomPanel(WarRoomViewModel viewModel, SyncfusionControlFactory controlFactory)
            : base(viewModel)
        {
            _factory = controlFactory ?? throw new ArgumentNullException(nameof(controlFactory));
            SafeSuspendAndLayout(InitializeControls);
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);

            if (Visible && !_dataLoaded)
            {
                _dataLoaded = true;
                LoadAsyncSafe();
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            MinimumSize = ScaleLogicalToDevice(new Size(1024, 720));
            PerformLayout();
            Invalidate(true);

            if (_panelHeader != null)
            {
                _panelHeader.Title = "War Room";
            }
        }

        public override async Task LoadAsync(CancellationToken ct = default)
        {
            if (IsLoaded) return;
            _loader!.Visible = true;
            IsBusy = true;
            Logger.LogDebug("[WarRoomPanel] LoadAsync starting");

            try
            {
                // WarRoomViewModel does not implement IAsyncInitializable — base handles gracefully
                await base.LoadAsync(ct);
                BindViewModel();
                Logger.LogDebug("[WarRoomPanel] LoadAsync completed");
            }
            finally
            {
                IsBusy = false;
                _loader!.Visible = false;
            }
        }

        private void BindViewModel()
        {
            if (ViewModel == null)
            {
                return;
            }

            if (_isViewModelBound)
            {
                return;
            }

            if (_scenarioInput != null)
            {
                _scenarioInput.Text = ViewModel.ScenarioInput;
            }

            if (_projectionsGrid != null)
            {
                _projectionsGrid.DataSource = ViewModel.Projections;
            }

            if (_departmentGrid != null)
            {
                _departmentGrid.DataSource = ViewModel.DepartmentImpacts;
            }

            ConfigureGrids();

            _viewModelPropertyChangedHandler = OnViewModelPropertyChanged;
            _projectionsCollectionChangedHandler = OnProjectionsCollectionChanged;
            _departmentCollectionChangedHandler = OnDepartmentCollectionChanged;

            ViewModel.PropertyChanged += _viewModelPropertyChangedHandler;
            ViewModel.Projections.CollectionChanged += _projectionsCollectionChangedHandler;
            ViewModel.DepartmentImpacts.CollectionChanged += _departmentCollectionChangedHandler;

            _isViewModelBound = true;

            SeedHarnessResultsIfNeeded();
            SetStatusMessage(ViewModel.StatusMessage);
            UpdateResultsFromViewModel();
            Logger.LogDebug("[WarRoomPanel] BindViewModel complete (ViewModel={VmPresent})", ViewModel is not null);
        }

        private void SeedHarnessResultsIfNeeded()
        {
            if (ViewModel == null)
            {
                return;
            }

            if (!IsUiTestHarness())
            {
                return;
            }

            if (ViewModel.HasResults || ViewModel.Projections.Any() || ViewModel.DepartmentImpacts.Any())
            {
                return;
            }

            ViewModel.RequiredRateIncrease = "11.8%";
            ViewModel.RiskLevel = 47.5m;
            ViewModel.BaselineMonthlyRevenue = 128000m;
            ViewModel.ProjectedMonthlyRevenue = 151500m;
            ViewModel.RevenueDifference = ViewModel.ProjectedMonthlyRevenue - ViewModel.BaselineMonthlyRevenue;

            ViewModel.Projections.Clear();
            ViewModel.Projections.Add(new ScenarioProjection
            {
                Year = 2026,
                ProjectedRate = 47.2m,
                ProjectedRevenue = 133000m,
                ProjectedExpenses = 121000m,
                ProjectedBalance = 12000m,
                ReserveLevel = 36000m
            });
            ViewModel.Projections.Add(new ScenarioProjection
            {
                Year = 2027,
                ProjectedRate = 49.1m,
                ProjectedRevenue = 139500m,
                ProjectedExpenses = 124200m,
                ProjectedBalance = 15300m,
                ReserveLevel = 51300m
            });
            ViewModel.Projections.Add(new ScenarioProjection
            {
                Year = 2028,
                ProjectedRate = 51.0m,
                ProjectedRevenue = 145000m,
                ProjectedExpenses = 127900m,
                ProjectedBalance = 17100m,
                ReserveLevel = 68400m
            });
            ViewModel.Projections.Add(new ScenarioProjection
            {
                Year = 2029,
                ProjectedRate = 52.4m,
                ProjectedRevenue = 151500m,
                ProjectedExpenses = 130800m,
                ProjectedBalance = 20700m,
                ReserveLevel = 89100m
            });

            ViewModel.DepartmentImpacts.Clear();
            ViewModel.DepartmentImpacts.Add(new DepartmentImpact
            {
                DepartmentName = "Water Operations",
                CurrentBudget = 54000m,
                ProjectedBudget = 60372m,
                ImpactAmount = 6372m,
                ImpactPercentage = 11.8m
            });
            ViewModel.DepartmentImpacts.Add(new DepartmentImpact
            {
                DepartmentName = "Wastewater",
                CurrentBudget = 47000m,
                ProjectedBudget = 52546m,
                ImpactAmount = 5546m,
                ImpactPercentage = 11.8m
            });
            ViewModel.DepartmentImpacts.Add(new DepartmentImpact
            {
                DepartmentName = "Storm Water",
                CurrentBudget = 26000m,
                ProjectedBudget = 29068m,
                ImpactAmount = 3068m,
                ImpactPercentage = 11.8m
            });
            ViewModel.DepartmentImpacts.Add(new DepartmentImpact
            {
                DepartmentName = "Administration",
                CurrentBudget = 18000m,
                ProjectedBudget = 20124m,
                ImpactAmount = 2124m,
                ImpactPercentage = 11.8m
            });

            ViewModel.HasResults = true;
            ViewModel.StatusMessage = "Sample scenario loaded for screenshot harness";
        }

        private static bool IsUiTestHarness()
        {
            try
            {
                var forceSeed = Environment.GetEnvironmentVariable("WILEYWIDGET_FORCE_WARROOM_SEEDED");
                if (string.Equals(forceSeed, "1", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(forceSeed, "true", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(forceSeed, "yes", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                var config = ServiceProviderServiceExtensions.GetService<IConfiguration>(Program.Services);
                return config?.GetValue<bool>("UI:IsUiTestHarness") ?? false;
            }
            catch
            {
                return false;
            }
        }

        private void UnbindViewModel()
        {
            if (!_isViewModelBound || ViewModel == null)
            {
                return;
            }

            if (_viewModelPropertyChangedHandler != null)
            {
                ViewModel.PropertyChanged -= _viewModelPropertyChangedHandler;
            }

            if (_projectionsCollectionChangedHandler != null)
            {
                ViewModel.Projections.CollectionChanged -= _projectionsCollectionChangedHandler;
            }

            if (_departmentCollectionChangedHandler != null)
            {
                ViewModel.DepartmentImpacts.CollectionChanged -= _departmentCollectionChangedHandler;
            }

            _isViewModelBound = false;
        }

        private void OnScenarioInputTextChanged(object? sender, EventArgs e)
        {
            if (ViewModel == null || _scenarioInput == null)
            {
                return;
            }

            if (!string.Equals(ViewModel.ScenarioInput, _scenarioInput.Text, StringComparison.Ordinal))
            {
                ViewModel.ScenarioInput = _scenarioInput.Text;
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (ViewModel == null)
            {
                return;
            }

            this.SafeInvoke(() =>
            {
                if (ViewModel == null)
                {
                    return;
                }

                switch (e.PropertyName)
                {
                    case nameof(WarRoomViewModel.StatusMessage):
                        SetStatusMessage(ViewModel.StatusMessage);
                        break;
                    case nameof(WarRoomViewModel.ScenarioInput):
                        if (_scenarioInput != null && !string.Equals(_scenarioInput.Text, ViewModel.ScenarioInput, StringComparison.Ordinal))
                        {
                            _scenarioInput.Text = ViewModel.ScenarioInput;
                        }
                        break;
                    case nameof(WarRoomViewModel.Projections):
                        if (_projectionsGrid != null)
                        {
                            _projectionsGrid.DataSource = ViewModel.Projections;
                        }
                        OnProjectionsCollectionChanged();
                        break;
                    case nameof(WarRoomViewModel.DepartmentImpacts):
                        if (_departmentGrid != null)
                        {
                            _departmentGrid.DataSource = ViewModel.DepartmentImpacts;
                        }
                        OnDepartmentCollectionChanged();
                        break;
                    case nameof(WarRoomViewModel.HasResults):
                    case nameof(WarRoomViewModel.RequiredRateIncrease):
                    case nameof(WarRoomViewModel.RiskLevel):
                    case nameof(WarRoomViewModel.BaselineMonthlyRevenue):
                    case nameof(WarRoomViewModel.ProjectedMonthlyRevenue):
                    case nameof(WarRoomViewModel.RevenueDifference):
                    case nameof(WarRoomViewModel.IsAnalyzing):
                        UpdateResultsFromViewModel();
                        break;
                }
            });
        }

        private void OnProjectionsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnProjectionsCollectionChanged();
        }

        private void OnProjectionsCollectionChanged()
        {
            UpdateResultsFromViewModel();
        }

        private void OnDepartmentCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnDepartmentCollectionChanged();
        }

        private void OnDepartmentCollectionChanged()
        {
            UpdateResultsFromViewModel();
        }

        private void UpdateResultsFromViewModel()
        {
            if (ViewModel == null || IsDisposed || Disposing)
            {
                return;
            }

            this.SafeInvoke(() =>
            {
                if (ViewModel == null || IsDisposed || Disposing)
                {
                    return;
                }

                var hasProjections = ViewModel.Projections.Any();
                var hasImpacts = ViewModel.DepartmentImpacts.Any();
                var hasResults = ViewModel.HasResults || hasProjections || hasImpacts;

                if (_resultsSummaryLabel != null)
                {
                    if (!hasResults)
                    {
                        _resultsSummaryLabel.Text = "Run a scenario to populate projections, impacts, and trend charts.";
                    }
                    else
                    {
                        var rateIncrease = string.IsNullOrWhiteSpace(ViewModel.RequiredRateIncrease)
                            ? "n/a"
                            : ViewModel.RequiredRateIncrease;
                        var risk = ViewModel.RiskLevel.ToString("0.##", CultureInfo.InvariantCulture);
                        var delta = ViewModel.RevenueDifference.ToString("C0", CultureInfo.CurrentCulture);
                        _resultsSummaryLabel.Text = $"5-year cumulative projection assuming {rateIncrease} phased rate increase + 4% annual inflation. Risk: {risk}% | Revenue Delta: {delta}";
                    }
                }

                if (_noDataOverlay != null)
                {
                    _noDataOverlay.Visible = !hasResults && !ViewModel.IsAnalyzing;
                    if (_noDataOverlay.Visible)
                    {
                        _noDataOverlay.BringToFront();
                    }
                }

                UpdateKpiCards();

                if (_projectionsGrid != null && _projectionsGrid.DataSource == null)
                {
                    _projectionsGrid.DataSource = ViewModel.Projections;
                }

                if (_departmentGrid != null && _departmentGrid.DataSource == null)
                {
                    _departmentGrid.DataSource = ViewModel.DepartmentImpacts;
                }

                ConfigureGrids();

                RenderRevenueChart();
                _projectionsGrid?.Refresh();
                _departmentGrid?.Refresh();
            });
        }

        private void ConfigureGrids()
        {
            ConfigureProjectionGrid();
            ConfigureDepartmentGrid();
        }

        private void ConfigureProjectionGrid()
        {
            if (_projectionsGrid == null)
            {
                return;
            }

            _projectionsGrid.AutoSizeColumnsMode = Syncfusion.WinForms.DataGrid.Enums.AutoSizeColumnsMode.Fill;
            DisableAddNewRowSurface(_projectionsGrid);
            _projectionsGrid.AllowEditing = false;
            _projectionsGrid.AllowFiltering = false;
            _projectionsGrid.FilterRowPosition = Syncfusion.WinForms.DataGrid.Enums.RowPosition.None;
            _projectionsGrid.AllowSorting = true;
            _projectionsGrid.RowHeight = LayoutTokens.GetScaled(LayoutTokens.GridRowHeightMedium);
            _projectionsGrid.HeaderRowHeight = LayoutTokens.GetScaled(LayoutTokens.GridHeaderRowHeightTall);

            ConfigureGridColumn(_projectionsGrid, nameof(ScenarioProjection.ProjectedRate), "Projected Rate", "0.##");
            ConfigureGridColumn(_projectionsGrid, nameof(ScenarioProjection.ProjectedRevenue), "Revenue", "C0");
            ConfigureGridColumn(_projectionsGrid, nameof(ScenarioProjection.ProjectedExpenses), "Expenses", "C0");
            ConfigureGridColumn(_projectionsGrid, nameof(ScenarioProjection.ProjectedBalance), "Balance", "C0");
            ConfigureGridColumn(_projectionsGrid, nameof(ScenarioProjection.ReserveLevel), "Reserve", "C0");
            RefreshGridVisualState(_projectionsGrid);
        }

        private void ConfigureDepartmentGrid()
        {
            if (_departmentGrid == null)
            {
                return;
            }

            _departmentGrid.AutoSizeColumnsMode = Syncfusion.WinForms.DataGrid.Enums.AutoSizeColumnsMode.Fill;
            DisableAddNewRowSurface(_departmentGrid);
            _departmentGrid.AllowEditing = false;
            _departmentGrid.AllowFiltering = false;
            _departmentGrid.FilterRowPosition = Syncfusion.WinForms.DataGrid.Enums.RowPosition.None;
            _departmentGrid.AllowSorting = true;
            _departmentGrid.RowHeight = LayoutTokens.GetScaled(LayoutTokens.GridRowHeightMedium);
            _departmentGrid.HeaderRowHeight = LayoutTokens.GetScaled(LayoutTokens.GridHeaderRowHeightTall);

            ConfigureGridColumn(_departmentGrid, nameof(DepartmentImpact.CurrentBudget), null, "C0");
            ConfigureGridColumn(_departmentGrid, nameof(DepartmentImpact.ProjectedBudget), null, "C0");
            ConfigureGridColumn(_departmentGrid, nameof(DepartmentImpact.ImpactAmount), null, "C0");
            ConfigureGridColumn(_departmentGrid, nameof(DepartmentImpact.ImpactPercentage), null, "0.##'%'");
            RefreshGridVisualState(_departmentGrid);
        }

        private static void ConfigureGridColumn(SfDataGrid grid, string mappingName, string? headerText, string? format)
        {
            var column = grid.Columns.FirstOrDefault(c => string.Equals(c.MappingName, mappingName, StringComparison.Ordinal));
            if (column == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(headerText))
            {
                column.HeaderText = headerText;
            }

            if (!string.IsNullOrWhiteSpace(format))
            {
                column.Format = format;
            }
        }

        private static void RefreshGridVisualState(SfDataGrid grid)
        {
            grid.Invalidate();
            grid.View?.Refresh();
        }

        private static void DisableAddNewRowSurface(SfDataGrid grid)
        {
            grid.AddNewRowPosition = Syncfusion.WinForms.DataGrid.Enums.RowPosition.None;
            grid.AddNewRowText = string.Empty;

            var placeholderProperty = grid.GetType().GetProperty("NewItemPlaceholderPosition");
            if (placeholderProperty?.CanWrite != true || !placeholderProperty.PropertyType.IsEnum)
            {
                return;
            }

            try
            {
                var noneValue = Enum.Parse(placeholderProperty.PropertyType, "None", ignoreCase: false);
                placeholderProperty.SetValue(grid, noneValue);
            }
            catch
            {
                // Older grid variants may not expose the placeholder enum without extra framework references.
            }
        }

        private void RenderRevenueChart()
        {
            if (_revenueChart == null || ViewModel == null)
            {
                return;
            }

            _revenueChart.Series.Clear();
            if (!ViewModel.Projections.Any())
            {
                if (ViewModel.BaselineMonthlyRevenue > 0 || ViewModel.ProjectedMonthlyRevenue > 0)
                {
                    var monthlySeries = new ChartSeries("Monthly Revenue", ChartSeriesType.Column);
                    monthlySeries.Points.Add("Baseline", (double)ViewModel.BaselineMonthlyRevenue);
                    monthlySeries.Points.Add("Projected", (double)ViewModel.ProjectedMonthlyRevenue);
                    monthlySeries.Style.Interior = new BrushInfo(ThemeColors.Success);
                    _revenueChart.Series.Add(monthlySeries);
                }
                _revenueChart.Refresh();
                return;
            }

            var revenueSeries = new ChartSeries("Projected Revenue", ChartSeriesType.Line);
            var expensesSeries = new ChartSeries("Projected Expenses", ChartSeriesType.Line);
            var balanceSeries = new ChartSeries("Projected Balance", ChartSeriesType.Column);

            revenueSeries.Style.Interior = new BrushInfo(ThemeColors.Success);
            expensesSeries.Style.Interior = new BrushInfo(ThemeColors.Warning);
            balanceSeries.Style.Interior = new BrushInfo(GradientStyle.Vertical, ThemeColors.Success, ThemeColors.Error);

            foreach (var projection in ViewModel.Projections.OrderBy(p => p.Year))
            {
                var year = projection.Year.ToString(CultureInfo.InvariantCulture);
                revenueSeries.Points.Add(year, (double)projection.ProjectedRevenue);
                expensesSeries.Points.Add(year, (double)projection.ProjectedExpenses);
                balanceSeries.Points.Add(year, (double)projection.ProjectedBalance);
            }

            _revenueChart.Series.Add(revenueSeries);
            _revenueChart.Series.Add(expensesSeries);
            _revenueChart.Series.Add(balanceSeries);
            _revenueChart.Refresh();
        }

        private void UpdateKpiCards()
        {
            if (ViewModel == null)
            {
                return;
            }

            if (_requiredRateCard != null)
            {
                _requiredRateCard.Value = string.IsNullOrWhiteSpace(ViewModel.RequiredRateIncrease) ? "n/a" : ViewModel.RequiredRateIncrease;
            }

            if (_riskCard != null)
            {
                _riskCard.Value = ViewModel.RiskLevel.ToString("0.##", CultureInfo.InvariantCulture) + "%";
            }

            if (_baselineRevenueCard != null)
            {
                _baselineRevenueCard.Value = ViewModel.BaselineMonthlyRevenue.ToString("C0", CultureInfo.CurrentCulture);
            }

            if (_projectedRevenueCard != null)
            {
                _projectedRevenueCard.Value = ViewModel.ProjectedMonthlyRevenue.ToString("C0", CultureInfo.CurrentCulture);
            }

            if (_deltaCard != null)
            {
                _deltaCard.Value = ViewModel.RevenueDifference.ToString("C0", CultureInfo.CurrentCulture);
            }
        }

        private async void OnRunScenarioClicked(object? sender, EventArgs e)
        {
            try
            {
                if (_panelHeader != null)
                {
                    _panelHeader.IsLoading = true;
                }
                SetStatusMessage("Running scenario analysis…");

                if (ViewModel?.RunScenarioCommand?.CanExecute(null) ?? false)
                {
                    await ViewModel.RunScenarioCommand.ExecuteAsync(null);
                    SetStatusMessage("Scenario analysis complete.");
                }
                else
                {
                    Logger.LogWarning("[WarRoomPanel] RunScenarioCommand unavailable");
                    SetStatusMessage("Scenario command is not available.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[WarRoomPanel] RunScenario click failed");
                SetStatusMessage($"Unable to run scenario: {ex.Message}");
            }
            finally
            {
                if (_panelHeader != null)
                {
                    _panelHeader.IsLoading = false;
                }
            }
        }

        private async void OnExportForecastClicked(object? sender, EventArgs e)
        {
            try
            {
                if (_panelHeader != null)
                {
                    _panelHeader.IsLoading = true;
                }
                SetStatusMessage("Exporting forecast…");

                if (ViewModel?.ExportForecastCommand?.CanExecute(null) ?? false)
                {
                    await ViewModel.ExportForecastCommand.ExecuteAsync(null);
                    SetStatusMessage("Forecast export complete.");
                }
                else
                {
                    Logger.LogWarning("[WarRoomPanel] ExportForecastCommand unavailable");
                    SetStatusMessage("Export command is not available.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[WarRoomPanel] ExportForecast click failed");
                SetStatusMessage($"Unable to export forecast: {ex.Message}");
            }
            finally
            {
                if (_panelHeader != null)
                {
                    _panelHeader.IsLoading = false;
                }
            }
        }

        private void SetStatusMessage(string message)
        {
            this.SafeInvoke(() =>
            {
                if (_statusLabel != null)
                {
                    _statusLabel.Text = message;
                }
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                UnbindViewModel();
                if (_scenarioInput != null)
                {
                    _scenarioInput.TextChanged -= OnScenarioInputTextChanged;
                }
                _noDataOverlay?.Dispose();
                _toolTip?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
