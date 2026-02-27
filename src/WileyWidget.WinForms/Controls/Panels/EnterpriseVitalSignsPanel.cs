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
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Supporting;
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
        private LoadingOverlay _loader = null!;

        // ── Panel-specific fields ────────────────────────────────────────────────
        private FlowLayoutPanel _gaugeFlow = null!;
        private TableLayoutPanel _chartTable = null!;
        private Label _statusLabel = null!;
        private NoDataOverlay _noDataOverlay = null!;
        private ToolTip? _toolTip;
        private string? _lastErrorMessage;
        private bool _loadTriggered;
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

            SafeSuspendAndLayout(InitializeLayout);
            BindViewModel();
            Load += EnterpriseVitalSignsPanel_Load;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            MinimumSize = RecommendedDockedPanelMinimumLogicalSize;
            PerformLayout();
        }

        private void InitializeLayout()
        {
            // ── Sacred Panel Skeleton layout properties ──────────────────────
            Dock = DockStyle.Fill;
            MinimumSize = new Size(
                RecommendedDockedPanelMinimumLogicalWidth,
                RecommendedDockedPanelMinimumLogicalHeight);
            AutoScaleMode = AutoScaleMode.Dpi;
            Padding = new Padding(0);

            // ── Header ──────────────────────────────────────────────────────
            _header = new PanelHeader(_factory)
            {
                Dock = DockStyle.Top,
                Title = "Enterprise Vital Signs — FY 2026",
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
                RowCount = 3,
                ColumnCount = 1,
                Margin = Padding.Empty,
                Padding = new Padding(8)
            };
            _content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _content.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));  // header slot unused (header docked top)
            _content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _content.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));

            // ── Gauge flow ───────────────────────────────────────────────────
            _gaugeFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 280,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoScroll = true,
                Padding = new Padding(20),
                AccessibleName = "Enterprise gauges"
            };
            _toolTip.SetToolTip(_gaugeFlow, "Gauge cards for current enterprise health.");

            // ── Chart table ──────────────────────────────────────────────────
            _chartTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Padding = new Padding(15),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
                AccessibleName = "Enterprise chart table"
            };
            _toolTip.SetToolTip(_chartTable, "Comparative enterprise chart data.");
            for (int i = 0; i < 2; i++)
            {
                _chartTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
                _chartTable.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            }

            // ── Content host ─────────────────────────────────────────────────
            var contentHost = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
            contentHost.Controls.Add(_chartTable);
            contentHost.Controls.Add(_gaugeFlow);

            // ── Overlays (via factory) ───────────────────────────────────────
            _noDataOverlay = new NoDataOverlay
            {
                Message = "No enterprise vital signs available\r\nImport or sync budget data to populate.",
                Dock = DockStyle.Fill,
                Visible = false,
                AccessibleName = "No enterprise data overlay"
            };
            contentHost.Controls.Add(_noDataOverlay);
            _noDataOverlay.BringToFront();

            _loader = _factory.CreateLoadingOverlay();
            contentHost.Controls.Add(_loader);
            _loader.BringToFront();

            _content.Controls.Add(contentHost, 0, 1);

            // ── Status bar ───────────────────────────────────────────────────
            _statusLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(4, 0, 0, 0),
                Text = "Ready",
                AccessibleName = "Enterprise vital signs status"
            };
            _content.Controls.Add(_statusLabel, 0, 2);

            // ── Wire to panel ─────────────────────────────────────────────────
            Controls.Add(_content);
            Controls.Add(_header);
            _header.BringToFront();
        }

        // RefreshClicked and CloseClicked are now wired inline in InitializeLayout.

        private void BindViewModel()
        {
            _snapshotsChangedHandler = (_, _) =>
            {
                if (IsHandleCreated && InvokeRequired)
                    BeginInvoke((Action)RefreshAllVisuals);
                else
                    RefreshAllVisuals();
            };
            _vm.EnterpriseSnapshots.CollectionChanged += _snapshotsChangedHandler;

            _viewModelPropertyChangedHandler = OnViewModelPropertyChanged;
            _vm.PropertyChanged += _viewModelPropertyChangedHandler;

            RefreshAllVisuals();
            UpdateLoadingState();
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
            _gaugeFlow.Controls.Clear();
            _chartTable.Controls.Clear();

            if (_vm.EnterpriseSnapshots.Count == 0)
            {
                UpdateLoadingState();
                SetStatusMessage("No enterprise data available.");
                return;
            }

            int index = 0;
            foreach (var snapshot in _vm.EnterpriseSnapshots)
            {
                var gaugePanel = new Panel()
                {
                    Margin = new Padding(10),
                    Width = 260,
                    Height = 260
                };

                var gauge = _factory.CreateEnterpriseGauge(snapshot.BreakEvenRatio, snapshot.Name);
                gauge.Dock = DockStyle.Fill;
                gaugePanel.Controls.Add(gauge);
                _gaugeFlow.Controls.Add(gaugePanel);

                var chartPanel = new Panel()
                {
                    Dock = DockStyle.Fill,
                    BorderStyle = BorderStyle.FixedSingle,
                    Padding = new Padding(8)
                };

                var chart = _factory.CreateEnterpriseChart(snapshot);
                chart.Dock = DockStyle.Fill;
                chartPanel.Controls.Add(chart);

                int row = index / _chartTable.ColumnCount;
                int col = index % _chartTable.ColumnCount;

                while (row >= _chartTable.RowCount)
                {
                    _chartTable.RowCount++;
                    _chartTable.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
                }

                _chartTable.Controls.Add(chartPanel, col, row);
                index++;
            }

            UpdateLoadingState();

            Logger.LogDebug("[{Panel}] Refreshed visuals. Gauges: {GaugeCount}, Charts: {ChartCount}",
                GetType().Name, _gaugeFlow.Controls.Count, _chartTable.Controls.Count);

            SetStatusMessage(_vm.EnterpriseSnapshots.Count == 1
                ? "Showing 1 enterprise snapshot."
                : $"Showing {_vm.EnterpriseSnapshots.Count} enterprise snapshots.");
        }

        private void SetStatusMessage(string message)
        {
            if (_statusLabel != null)
            {
                _statusLabel.Text = message;
            }
        }

        public bool IsComplete => _vm.EnterpriseSnapshots.Count > 0;

        public string CompletionStatus => $"Loaded {_vm.EnterpriseSnapshots.Count} enterprises — ready for council";

        public override async Task LoadAsync(CancellationToken ct = default)
        {
            try
            {
                _loader.Visible = true;
                _loader.BringToFront();
                await _vm.OnVisibilityChangedAsync(true);
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
