// ──────────────────────────────────────────────────────────────────────────────────
// EnterpriseVitalSignsPanel — Sacred Panel Skeleton v2026
// Wiley Widget Municipal Finance · WileyWidgetUIStandards §1 compliant
// ──────────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using WileyWidget.Models;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Supporting;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Factories;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Controls.Panels
{
    /// <summary>
    /// Enterprise Vital Signs panel — displays enterprise financial snapshots with
    /// radial gauges and comparative charts.  Implements the Sacred Panel Skeleton
    /// (WileyWidgetUIStandards §1): constructor takes pre-built ViewModel + factory,
    /// all Syncfusion controls created via <see cref="SyncfusionControlFactory"/>.
    /// </summary>
    public partial class EnterpriseVitalSignsPanel : ScopedPanelBase<EnterpriseVitalSignsViewModel>, ICompletablePanel
    {
        // ── Sacred Panel Skeleton mandatory fields ───────────────────────────────
        private readonly EnterpriseVitalSignsViewModel _vm;
        private readonly SyncfusionControlFactory _factory;
        private PanelHeader _header = null!;
        private TableLayoutPanel _content = null!;
        private Panel _contentHost = null!;
        private LoadingOverlay _loader = null!;

        // ── Panel-specific fields ────────────────────────────────────────────────
        private TableLayoutPanel _dashboardLayout = null!;
        private TableLayoutPanel _dashboardBody = null!;
        private Panel _summaryPanel = null!;
        private Panel _gaugeHost = null!;
        private FlowLayoutPanel _gaugeFlow = null!;
        private TableLayoutPanel _chartTable = null!;
        private Label _overallCityNetValueLabel = null!;
        private Label _selfSustainingValueLabel = null!;
        private Label _largestGapValueLabel = null!;
        private Label _statusLabel = null!;
        private NoDataOverlay _noDataOverlay = null!;
        private ToolTip? _toolTip;
        private string? _lastErrorMessage;
        private bool _loadTriggered;
        private bool _refreshVisualsQueued;
        private NotifyCollectionChangedEventHandler? _snapshotsChangedHandler;
        private PropertyChangedEventHandler? _viewModelPropertyChangedHandler;

        /// <summary>
        /// Sacred Panel Skeleton constructor: ViewModel and factory are injected directly
        /// by the DI container — no <see cref="IServiceScopeFactory"/> required.
        /// </summary>
        public EnterpriseVitalSignsPanel(
            EnterpriseVitalSignsViewModel vm,
            SyncfusionControlFactory factory)
            : base(vm, Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ILogger<EnterpriseVitalSignsPanel>>(Program.Services))
        {
            _vm = vm ?? throw new ArgumentNullException(nameof(vm));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            MinimumSize = new Size(1024, 720);

            SafeSuspendAndLayout(InitializeLayout);
            BindViewModel();
            Load += EnterpriseVitalSignsPanel_Load;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            PerformLayout();
        }

        private void InitializeLayout()
        {
            using var layoutScope = LogContext.PushProperty("Panel", nameof(EnterpriseVitalSignsPanel));
            using var operationScope = LogContext.PushProperty("PanelOperation", "InitializeLayout");

            int gaugeRowHeight = GetGaugeRowHeight();

            // ── Sacred Panel Skeleton layout properties ──────────────────────
            Dock = DockStyle.Fill;
            AutoScaleMode = AutoScaleMode.Dpi;
            Padding = new Padding(0);

            // ── Header ──────────────────────────────────────────────────────
            _header = new PanelHeader(_factory)
            {
                Dock = DockStyle.Top,
                Title = "Enterprise Vital Signs",
                AccessibleName = "Enterprise vital signs header",
                ShowHelpButton = false,
                ShowPinButton = false,
                ShowCloseButton = true,
                ShowRefreshButton = true
            };
            _header.RefreshClicked += (s, e) => _vm.RefreshCommand.Execute(null);
            _header.CloseClicked += (s, e) => ClosePanel();
            _toolTip = new ToolTip();
            _toolTip.SetToolTip(_header, "Refresh enterprise metrics or close this panel.");

            // ── Root content (3-row: header slot / body / status) ────────────
            _content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                Margin = Padding.Empty,
                Padding = new Padding(8)
            };
            _content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _content.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));

            _summaryPanel = CreateSummaryPanel();

            // ── Gauge flow ───────────────────────────────────────────────────
            _gaugeHost = new Panel
            {
                Dock = DockStyle.Top,
                Height = gaugeRowHeight,
                MinimumSize = new Size(0, gaugeRowHeight),
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                AccessibleName = "Enterprise gauge host"
            };

            _gaugeFlow = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = false,
                MinimumSize = new Size(GetGaugeCardSize().Width, gaugeRowHeight),
                Padding = new Padding(0, 12, 0, 12),
                Margin = Padding.Empty,
                AccessibleName = "Enterprise gauges"
            };
            _gaugeHost.Controls.Add(_gaugeFlow);
            _gaugeHost.Resize += (_, _) => CenterGaugeRow();
            _toolTip.SetToolTip(_gaugeFlow, "Break-even ratios for the current fiscal year. Values at or above 100% are self-sustaining.");

            // ── Chart table ──────────────────────────────────────────────────
            _chartTable = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 2,
                Padding = new Padding(8),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
                AccessibleName = "Enterprise chart table"
            };
            _toolTip.SetToolTip(_chartTable, "Twelve-point fiscal trends derived from available actuals and current-year estimates.");
            for (int i = 0; i < 2; i++)
            {
                _chartTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
                _chartTable.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            }

            _dashboardBody = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                AccessibleName = "Enterprise dashboard body"
            };
            _dashboardBody.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _dashboardBody.RowStyles.Add(new RowStyle(SizeType.Absolute, gaugeRowHeight));
            _dashboardBody.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _dashboardBody.Controls.Add(_gaugeHost, 0, 0);
            _dashboardBody.Controls.Add(_chartTable, 0, 1);

            _dashboardLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                AccessibleName = "Enterprise dashboard layout"
            };
            _dashboardLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _dashboardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, _summaryPanel.Height));
            _dashboardLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _dashboardLayout.Controls.Add(_summaryPanel, 0, 0);
            _dashboardLayout.Controls.Add(_dashboardBody, 0, 1);

            // ── Content host ─────────────────────────────────────────────────
            _contentHost = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                AutoScroll = true,
                AccessibleName = "Enterprise content host"
            };
            _contentHost.Resize += (_, _) => QueueRefreshAllVisuals();
            _contentHost.Controls.Add(_dashboardLayout);

            // ── Overlays (via factory) ───────────────────────────────────────
            _noDataOverlay = new NoDataOverlay
            {
                Message = "No enterprise vital signs available\r\nImport or sync budget data to populate.",
                Dock = DockStyle.Fill,
                Visible = false,
                AccessibleName = "No enterprise data overlay"
            };
            _contentHost.Controls.Add(_noDataOverlay);
            _noDataOverlay.BringToFront();

            _loader = _factory.CreateLoadingOverlay();
            _loader.Visible = false;
            _contentHost.Controls.Add(_loader);
            _loader.BringToFront();

            _content.Controls.Add(_contentHost, 0, 0);

            // ── Status bar ───────────────────────────────────────────────────
            _statusLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(4, 0, 0, 0),
                Text = "Ready",
                AccessibleName = "Enterprise vital signs status"
            };
            _content.Controls.Add(_statusLabel, 0, 1);

            // ── Wire to panel ─────────────────────────────────────────────────
            Controls.Add(_content);
            Controls.Add(_header);
            _header.BringToFront();
        }

        private Panel CreateSummaryPanel()
        {
            int summaryHeight = GetSummaryPanelHeight();
            int summaryMetricRowHeight = GetSummaryMetricRowHeight();

            var summaryPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = summaryHeight,
                MinimumSize = new Size(0, summaryHeight),
                Margin = Padding.Empty,
                Padding = new Padding(12, 10, 12, 10),
                AccessibleName = "Enterprise summary panel"
            };
            summaryPanel.ApplySyncfusionTheme(Syncfusion.WinForms.Controls.SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme, Logger);

            var summaryLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 2,
                Margin = Padding.Empty,
                AccessibleName = "Enterprise summary layout"
            };
            summaryLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34F));
            summaryLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33F));
            summaryLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33F));
            summaryLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, summaryMetricRowHeight));
            summaryLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _overallCityNetValueLabel = CreateSummaryMetric(summaryLayout, 0, "Overall City Net Position", "Overall city net value", 16F,
                "Top-line enterprise surplus or deficit across the city.");
            _selfSustainingValueLabel = CreateSummaryMetric(summaryLayout, 1, "Self-Sustaining", "Self-sustaining count value", 12F,
                "Count of enterprises currently covering their own costs.");
            _largestGapValueLabel = CreateSummaryMetric(summaryLayout, 2, "Largest Gap", "Largest gap value", 12F,
                "Enterprise with the largest deficit exposure.");

            var trendHintLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Twelve-point fiscal trends use available actuals and current-year estimates because the Wiley source file does not store twelve raw monthly amounts.",
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                AccessibleName = "Enterprise dashboard trend hint"
            };

            summaryLayout.Controls.Add(trendHintLabel, 0, 1);
            summaryLayout.SetColumnSpan(trendHintLabel, 3);

            summaryPanel.Controls.Add(summaryLayout);
            _toolTip?.SetToolTip(summaryPanel, "Overall city net position and guidance for reading enterprise gauges and charts.");
            return summaryPanel;
        }

        private Label CreateSummaryMetric(
            TableLayoutPanel parent,
            int columnIndex,
            string title,
            string valueAccessibleName,
            float valueFontSize,
            string description)
        {
            var metricPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0, 0, 8, 0),
                AccessibleName = title
            };
            metricPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            metricPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var titleLabel = new Label
            {
                Text = title,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.BottomLeft,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                AccessibleName = $"{title} caption"
            };

            var valueLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft,
                Font = new Font("Segoe UI", valueFontSize, FontStyle.Bold),
                AccessibleName = valueAccessibleName
            };

            metricPanel.Controls.Add(titleLabel, 0, 0);
            metricPanel.Controls.Add(valueLabel, 0, 1);
            _toolTip?.SetToolTip(metricPanel, description);
            parent.Controls.Add(metricPanel, columnIndex, 0);
            return valueLabel;
        }

        // RefreshClicked and CloseClicked are now wired inline in InitializeLayout.

        private void BindViewModel()
        {
            _snapshotsChangedHandler = (_, _) =>
            {
                if (IsHandleCreated && InvokeRequired)
                    BeginInvoke((Action)QueueRefreshAllVisuals);
                else
                    QueueRefreshAllVisuals();
            };
            _vm.EnterpriseSnapshots.CollectionChanged += _snapshotsChangedHandler;

            _viewModelPropertyChangedHandler = OnViewModelPropertyChanged;
            _vm.PropertyChanged += _viewModelPropertyChangedHandler;

            RefreshAllVisuals();
            UpdateLoadingState();
        }

        private void QueueRefreshAllVisuals()
        {
            if (_refreshVisualsQueued || IsDisposed)
            {
                return;
            }

            _refreshVisualsQueued = true;

            if (!IsHandleCreated)
            {
                _refreshVisualsQueued = false;
                RefreshAllVisuals();
                return;
            }

            BeginInvoke((MethodInvoker)(() =>
            {
                _refreshVisualsQueued = false;

                if (IsDisposed)
                {
                    return;
                }

                RefreshAllVisuals();
            }));
        }

        private async void EnterpriseVitalSignsPanel_Load(object? sender, EventArgs e)
        {
            if (_loadTriggered) return;
            _loadTriggered = true;

            try
            {
                await LoadAsync();
                Logger.LogDebug("[{Panel}] Initial load completed. Snapshots: {Count}",
                    GetType().Name, _vm.EnterpriseSnapshots.Count);
            }
            catch (Exception ex)
            {
                _loadTriggered = false;
                Logger.LogError(ex, "[{Panel}] Initial load failed", GetType().Name);
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(EnterpriseVitalSignsViewModel.OverallCityNet))
            {
                if (IsHandleCreated && InvokeRequired)
                    BeginInvoke((Action)UpdateSummaryMetrics);
                else
                    UpdateSummaryMetrics();

                return;
            }

            if (e.PropertyName is nameof(EnterpriseVitalSignsViewModel.IsLoading)
                                or nameof(EnterpriseVitalSignsViewModel.ErrorMessage))
            {
                if (IsHandleCreated && InvokeRequired)
                    BeginInvoke((Action)UpdateLoadingState);
                else
                    UpdateLoadingState();
            }
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            if (Visible)
            {
                try
                {
                    UpdateLoadingState();

                    if (_vm.EnterpriseSnapshots.Count > 0)
                    {
                        RefreshAllVisuals();
                    }
                }
                catch
                {
                    // Layout stabilization continues in base.OnVisibleChanged
                }
            }

            base.OnVisibleChanged(e);

            if (Visible && !_loadTriggered && _vm.EnterpriseSnapshots.Count == 0)
            {
                _loadTriggered = true;
                _ = LoadAsync();
            }
        }

        private void UpdateLoadingState()
        {
            _header.IsLoading = _vm.IsLoading;
            _loader.Visible = _vm.IsLoading;
            if (_vm.IsLoading)
            {
                _loader.BringToFront();
                SetStatusMessage("Loading enterprise vital signs…");
            }

            var showNoData = !_vm.IsLoading && _vm.EnterpriseSnapshots.Count == 0;
            _noDataOverlay.Visible = showNoData;
            if (showNoData) _noDataOverlay.BringToFront();

            if (!string.IsNullOrWhiteSpace(_vm.ErrorMessage) &&
                !string.Equals(_lastErrorMessage, _vm.ErrorMessage, StringComparison.Ordinal))
            {
                _lastErrorMessage = _vm.ErrorMessage;
                SetStatusMessage($"Data update issue: {_vm.ErrorMessage}");
            }
        }

        private void RefreshAllVisuals()
        {
            using var refreshScope = LogContext.PushProperty("Panel", nameof(EnterpriseVitalSignsPanel));
            using var operationScope = LogContext.PushProperty("PanelOperation", "RefreshAllVisuals");

            SuspendLayout();
            _content.SuspendLayout();
            _contentHost.SuspendLayout();
            _dashboardLayout.SuspendLayout();
            _dashboardBody.SuspendLayout();
            _gaugeFlow.SuspendLayout();
            _chartTable.SuspendLayout();

            try
            {
                _gaugeFlow.Controls.Clear();
                _chartTable.Controls.Clear();
                _chartTable.RowStyles.Clear();
                UpdateSummaryMetrics();

                if (_vm.EnterpriseSnapshots.Count == 0)
                {
                    _logger?.LogInformation("EnterpriseVitalSignsPanel refresh: no snapshots available");
                    _chartTable.RowCount = 1;
                    _chartTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                    UpdateLoadingState();
                    SetStatusMessage("No enterprise data available.");
                    return;
                }

                int availableWidth = _contentHost.ClientSize.Width > 0 ? _contentHost.ClientSize.Width : ClientSize.Width;
                int columnCount = availableWidth >= 900 ? 2 : 1;
                int rowCount = (_vm.EnterpriseSnapshots.Count + columnCount - 1) / columnCount;
                int chartCardHeight = GetSnapshotCardHeight();
                Size gaugeCardSize = GetGaugeCardSize();

                _logger?.LogInformation(
                    "EnterpriseVitalSignsPanel refresh: Snapshots={SnapshotCount}, Columns={ColumnCount}, Rows={RowCount}, GaugeCards={GaugeCards}",
                    _vm.EnterpriseSnapshots.Count,
                    columnCount,
                    rowCount,
                    _gaugeFlow.Controls.Count);

                _chartTable.ColumnCount = columnCount;
                _chartTable.RowCount = rowCount;
                _chartTable.ColumnStyles.Clear();

                for (int columnIndex = 0; columnIndex < columnCount; columnIndex++)
                {
                    _chartTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columnCount));
                }

                for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
                {
                    _chartTable.RowStyles.Add(new RowStyle(SizeType.Absolute, chartCardHeight));
                }

                int index = 0;
                foreach (var snapshot in _vm.EnterpriseSnapshots)
                {
                    var gaugePanel = new Panel
                    {
                        Margin = new Padding(6, 8, 6, 8),
                        Width = gaugeCardSize.Width,
                        Height = gaugeCardSize.Height,
                        MinimumSize = gaugeCardSize,
                        AccessibleName = $"{snapshot.Name} gauge card"
                    };

                    var gauge = _factory.CreateEnterpriseGauge(snapshot.BreakEvenRatio, snapshot.Name);
                    gauge.Dock = DockStyle.Fill;
                    gaugePanel.Controls.Add(gauge);
                    _gaugeFlow.Controls.Add(gaugePanel);

                    int row = index / columnCount;
                    int col = index % columnCount;
                    _chartTable.Controls.Add(CreateEnterpriseSnapshotPanel(snapshot), col, row);
                    index++;
                }

                CenterGaugeRow();

                UpdateLoadingState();

                Logger.LogDebug("[{Panel}] Refreshed visuals. Gauges: {GaugeCount}, Charts: {ChartCount}",
                    GetType().Name, _gaugeFlow.Controls.Count, _chartTable.Controls.Count);

                SetStatusMessage(_vm.EnterpriseSnapshots.Count == 1
                    ? "Showing 1 enterprise snapshot."
                    : $"Showing {_vm.EnterpriseSnapshots.Count} enterprise snapshots.");
            }
            finally
            {
                _chartTable.ResumeLayout(true);
                _gaugeFlow.ResumeLayout(true);
                _dashboardBody.ResumeLayout(true);
                _dashboardLayout.ResumeLayout(true);
                _contentHost.ResumeLayout(true);
                _content.ResumeLayout(true);
                ResumeLayout(true);
                PerformLayout();
            }
        }

        private Control CreateEnterpriseSnapshotPanel(EnterpriseSnapshot snapshot)
        {
            int snapshotTitleHeight = GetSnapshotTitleHeight();
            int snapshotDetailsHeight = GetSnapshotDetailsHeight();
            int snapshotCardHeight = GetSnapshotCardHeight();

            var container = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Margin = new Padding(6),
                Padding = new Padding(8),
                MinimumSize = new Size(0, snapshotCardHeight),
                AccessibleName = $"{snapshot.Name} financial snapshot"
            };
            container.RowStyles.Add(new RowStyle(SizeType.Absolute, snapshotTitleHeight));
            container.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            container.RowStyles.Add(new RowStyle(SizeType.Absolute, snapshotDetailsHeight));

            container.SuspendLayout();
            try
            {
                var titleLabel = new Label
                {
                    Dock = DockStyle.Fill,
                    AutoSize = true,
                    Margin = new Padding(0, 0, 0, 6),
                    Text = snapshot.Name,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                    AccessibleName = $"{snapshot.Name} snapshot title"
                };

                var chart = _factory.CreateEnterpriseChart(snapshot);
                chart.Dock = DockStyle.Fill;
                chart.MinimumSize = new Size(0, GetSnapshotChartMinimumHeight());
                container.Controls.Add(titleLabel, 0, 0);
                container.Controls.Add(chart, 0, 1);
                container.Controls.Add(CreateSnapshotDetailsPanel(snapshot), 0, 2);
            }
            finally
            {
                container.ResumeLayout(true);
            }
            return container;
        }

        private Control CreateSnapshotDetailsPanel(EnterpriseSnapshot snapshot)
        {
            int detailsHeight = GetSnapshotDetailsHeight();

            var details = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 3,
                RowCount = 3,
                Height = detailsHeight,
                MinimumSize = new Size(0, detailsHeight),
                Margin = new Padding(0, 8, 0, 0),
                Padding = new Padding(4, 0, 4, 4),
                AccessibleName = $"{snapshot.Name} snapshot details"
            };

            details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));
            details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            details.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            details.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            details.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            AddSnapshotMetric(details, 0, "Net Position", snapshot.NetPosition.ToString("C0"), snapshot.NetPosition >= 0);
            AddSnapshotMetric(details, 1, "Self-Sustaining", snapshot.IsSelfSustaining ? "Yes" : "No", snapshot.IsSelfSustaining);
            AddSnapshotMetric(details, 2, "Cross-Subsidy", snapshot.CrossSubsidyNote, null);

            var monthlyNarrative = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 8, 0, 0),
                Text = snapshot.TrendNarrative,
                AccessibleName = $"{snapshot.Name} monthly narrative"
            };
            details.Controls.Add(monthlyNarrative, 0, 2);
            details.SetColumnSpan(monthlyNarrative, 3);

            return details;
        }

        private static void AddSnapshotMetric(TableLayoutPanel parent, int columnIndex, string label, string value, bool? positiveState)
        {
            var captionLabel = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = label,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                AccessibleName = $"{label} caption"
            };

            var valueLabel = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 2, 0, 0),
                Text = value,
                AccessibleName = $"{label} value"
            };

            if (positiveState.HasValue)
            {
                if (positiveState.Value)
                {
                    valueLabel.ForeColor = WileyWidget.WinForms.Themes.ThemeColors.Success;
                }
                else
                {
                    valueLabel.ForeColor = WileyWidget.WinForms.Themes.ThemeColors.Error;
                }
            }

            parent.Controls.Add(captionLabel, columnIndex, 0);
            parent.Controls.Add(valueLabel, columnIndex, 1);
        }

        private void UpdateSummaryMetrics()
        {
            if (_overallCityNetValueLabel == null)
            {
                return;
            }

            if (_vm.EnterpriseSnapshots.Count == 0)
            {
                _overallCityNetValueLabel.Text = "No data";
                _overallCityNetValueLabel.ResetForeColor();
                _selfSustainingValueLabel.Text = "0 of 0";
                _selfSustainingValueLabel.ResetForeColor();
                _largestGapValueLabel.Text = "No deficit";
                _largestGapValueLabel.ResetForeColor();
                return;
            }

            _overallCityNetValueLabel.Text = _vm.OverallCityNet.ToString("C0");
            if (_vm.OverallCityNet >= 0)
            {
                _overallCityNetValueLabel.ForeColor = WileyWidget.WinForms.Themes.ThemeColors.Success;
            }
            else
            {
                _overallCityNetValueLabel.ForeColor = WileyWidget.WinForms.Themes.ThemeColors.Error;
            }

            int selfSustainingCount = _vm.EnterpriseSnapshots.Count(snapshot => snapshot.IsSelfSustaining);
            _selfSustainingValueLabel.Text = $"{selfSustainingCount} of {_vm.EnterpriseSnapshots.Count}";
            if (selfSustainingCount == _vm.EnterpriseSnapshots.Count)
            {
                _selfSustainingValueLabel.ForeColor = WileyWidget.WinForms.Themes.ThemeColors.Success;
            }
            else
            {
                _selfSustainingValueLabel.ResetForeColor();
            }

            var largestGap = _vm.EnterpriseSnapshots.OrderBy(snapshot => snapshot.NetPosition).First();
            _largestGapValueLabel.Text = largestGap.NetPosition < 0
                ? $"{largestGap.Name} {largestGap.NetPosition:C0}"
                : "No deficit";
            if (largestGap.NetPosition < 0)
            {
                _largestGapValueLabel.ForeColor = WileyWidget.WinForms.Themes.ThemeColors.Error;
            }
            else
            {
                _largestGapValueLabel.ForeColor = WileyWidget.WinForms.Themes.ThemeColors.Success;
            }
        }

        private void SetStatusMessage(string message)
        {
            if (_statusLabel != null)
            {
                _statusLabel.Text = message;
            }
        }

        private void CenterGaugeRow()
        {
            if (_gaugeHost == null || _gaugeFlow == null || _gaugeHost.IsDisposed || _gaugeFlow.IsDisposed)
            {
                return;
            }

            var preferredSize = _gaugeFlow.PreferredSize;
            int x = Math.Max(0, (_gaugeHost.ClientSize.Width - preferredSize.Width) / 2);
            int y = Math.Max(0, (_gaugeHost.ClientSize.Height - preferredSize.Height) / 2);
            _gaugeFlow.Location = new Point(x, y);
        }

        private static int GetGaugeRowHeight()
            => (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(272.0f);

        private static Size GetGaugeCardSize()
            => new(
                (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(242.0f),
                (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(250.0f));

        private static int GetSummaryPanelHeight()
            => (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(156.0f);

        private static int GetSummaryMetricRowHeight()
            => (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(84.0f);

        private static int GetSnapshotTitleHeight()
            => (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(32.0f);

        private static int GetSnapshotDetailsHeight()
            => (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(82.0f);

        private static int GetSnapshotCardHeight()
            => (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(340.0f);

        private static int GetSnapshotChartMinimumHeight()
            => (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(205.0f);

        public bool IsComplete => _vm.EnterpriseSnapshots.Count > 0;

        public string CompletionStatus => $"Loaded {_vm.EnterpriseSnapshots.Count} enterprises — ready for council";

        public override async Task LoadAsync(CancellationToken ct = default)
        {
            using var loadScope = LogContext.PushProperty("Panel", nameof(EnterpriseVitalSignsPanel));
            using var operationScope = LogContext.PushProperty("PanelOperation", "Load");

            try
            {
                _loader.Visible = true;
                _loader.BringToFront();
                await _vm.OnVisibilityChangedAsync(true);
                _logger?.LogInformation(
                    "EnterpriseVitalSignsPanel loaded: SnapshotCount={SnapshotCount}, IsLoading={IsLoading}",
                    _vm.EnterpriseSnapshots.Count,
                    _vm.IsLoading);
            }
            finally
            {
                _loader.Visible = false;
            }
        }

        public override Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;

        public override Task<ValidationResult> ValidateAsync(CancellationToken ct = default)
            => Task.FromResult(ValidationResult.Success);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Load -= EnterpriseVitalSignsPanel_Load;

                if (_snapshotsChangedHandler != null)
                    _vm.EnterpriseSnapshots.CollectionChanged -= _snapshotsChangedHandler;

                if (_viewModelPropertyChangedHandler != null)
                    _vm.PropertyChanged -= _viewModelPropertyChangedHandler;

                _toolTip?.Dispose();
            }

            base.Dispose(disposing);
        }

        public override void FocusFirstError() { }
    }
}
