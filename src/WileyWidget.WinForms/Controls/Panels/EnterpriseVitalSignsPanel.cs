using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Supporting;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Controls.Panels
{
    public partial class EnterpriseVitalSignsPanel : ScopedPanelBase<EnterpriseVitalSignsViewModel>, ICompletablePanel
    {
        private FlowLayoutPanel _gaugeFlow = null!;
        private TableLayoutPanel _chartTable = null!;
        private bool _dataLoaded;
        private LoadingOverlay? _loadingOverlay;
        private string? _lastErrorMessage;

        public EnterpriseVitalSignsPanel(IServiceScopeFactory scopeFactory, ILogger<EnterpriseVitalSignsPanel> logger)
            : base(scopeFactory, logger)
        {
            InitializeLayout();
            BindViewModel();
        }

        private void InitializeLayout()
        {
            Dock = DockStyle.Fill;

            _gaugeFlow = new FlowLayoutPanel()
            {
                Dock = DockStyle.Top,
                Height = 280,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoScroll = true,
                Padding = new Padding(20)
            };

            _chartTable = new TableLayoutPanel()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Padding = new Padding(15),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.Single
            };

            for (int i = 0; i < 2; i++)
            {
                _chartTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
                _chartTable.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            }

            Controls.Add(_gaugeFlow);
            Controls.Add(_chartTable);

            _loadingOverlay = new LoadingOverlay()
            {
                Dock = DockStyle.Fill,
                Visible = false
            };
            Controls.Add(_loadingOverlay);
            _loadingOverlay.BringToFront();
        }

        private void BindViewModel()
        {
            if (ViewModel == null)
            {
                return;
            }

            ViewModel.EnterpriseSnapshots.CollectionChanged += (s, e) =>
            {
                if (IsHandleCreated && InvokeRequired)
                {
                    BeginInvoke((Action)RefreshAllVisuals);
                }
                else
                {
                    RefreshAllVisuals();
                }
            };

            ViewModel.PropertyChanged += OnViewModelPropertyChanged;

            RefreshAllVisuals();
            UpdateLoadingState();
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(EnterpriseVitalSignsViewModel.IsLoading) ||
                e.PropertyName == nameof(EnterpriseVitalSignsViewModel.ErrorMessage))
            {
                if (IsHandleCreated && InvokeRequired)
                {
                    BeginInvoke((Action)UpdateLoadingState);
                }
                else
                {
                    UpdateLoadingState();
                }
            }
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);

            if (Visible && !_dataLoaded)
            {
                _dataLoaded = true;
                _ = LoadAsync();
            }
        }

        private void UpdateLoadingState()
        {
            if (_loadingOverlay == null || ViewModel == null)
            {
                return;
            }

            _loadingOverlay.Visible = ViewModel.IsLoading;
            if (ViewModel.IsLoading)
            {
                _loadingOverlay.BringToFront();
            }

            if (!string.IsNullOrWhiteSpace(ViewModel.ErrorMessage) &&
                !string.Equals(_lastErrorMessage, ViewModel.ErrorMessage, StringComparison.Ordinal))
            {
                _lastErrorMessage = ViewModel.ErrorMessage;
                MessageBox.Show(
                    ViewModel.ErrorMessage,
                    "Enterprise Vital Signs",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void RefreshAllVisuals()
        {
            if (ViewModel == null)
            {
                return;
            }

            _gaugeFlow.Controls.Clear();
            _chartTable.Controls.Clear();

            if (ViewModel.EnterpriseSnapshots.Count == 0)
            {
                return;
            }

            int index = 0;
            foreach (var snapshot in ViewModel.EnterpriseSnapshots)
            {
                var gaugePanel = new Panel()
                {
                    Margin = new Padding(10),
                    Width = 260,
                    Height = 260
                };

                var gauge = ControlFactory.CreateEnterpriseGauge(snapshot.BreakEvenRatio, snapshot.Name);
                gauge.Dock = DockStyle.Fill;
                gaugePanel.Controls.Add(gauge);
                _gaugeFlow.Controls.Add(gaugePanel);

                var chartPanel = new Panel()
                {
                    Dock = DockStyle.Fill,
                    BorderStyle = BorderStyle.FixedSingle,
                    Padding = new Padding(8)
                };

                var chart = ControlFactory.CreateEnterpriseChart(snapshot);
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
        }

        public bool IsComplete => (ViewModel?.EnterpriseSnapshots.Count ?? 0) > 0;

        public string CompletionStatus => $"Loaded {ViewModel?.EnterpriseSnapshots.Count ?? 0} enterprises — ready for council";

        public override Task LoadAsync(CancellationToken ct = default)
        {
            if (ViewModel?.LoadDataCommand.CanExecute(null) == true)
            {
                return ViewModel.LoadDataCommand.ExecuteAsync(null);
            }

            return Task.CompletedTask;
        }

        public override Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;

        public override Task<ValidationResult> ValidateAsync(CancellationToken ct = default)
            => Task.FromResult(ValidationResult.Success);

        public override void FocusFirstError() { }
    }
}
