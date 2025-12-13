using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Syncfusion.Windows.Forms.Chart;
using Syncfusion.Drawing;
using System.Drawing;
using System.Diagnostics.CodeAnalysis;
using Syncfusion.WinForms.Controls;
using Serilog;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Themes;
using Syncfusion.WinForms.Themes;

namespace WileyWidget.WinForms.Forms
{
    internal static class ChartFormResources
    {
        public const string FormTitle = "Budget Analytics";
    }

    [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
    public partial class ChartForm : Form
    {
        private readonly ChartViewModel _vm;
        private readonly MainForm _mainForm;
        private ChartControl? _cartesian;
        private ChartControl? _pie;
        private Label? _statusLabel;

        public ChartForm(ChartViewModel vm, MainForm mainForm)
        {
            _vm = vm ?? throw new ArgumentNullException(nameof(vm));
            _mainForm = mainForm ?? throw new ArgumentNullException(nameof(mainForm));
            InitializeComponent();
            MdiParent = _mainForm;
            Text = ChartFormResources.FormTitle;
            SfSkinManager.SetVisualStyle(this, "Office2019Colorful");
            ThemeColors.ApplyTheme(this);

            _vm.PropertyChanged += VmOnPropertyChanged;
            WireCollectionChanges();

            FormClosed += (_, _) =>
            {
                _vm.PropertyChanged -= VmOnPropertyChanged;
                UnwireCollectionChanges();
            };
        }

        private void InitializeComponent()
        {
            Name = "ChartForm";
            _cartesian = CreateCartesianChart();
            _cartesian.Dock = DockStyle.Fill;
            _pie = CreatePieChart();
            _pie.Dock = DockStyle.Fill;

            var split = new SplitContainer { Dock = DockStyle.Fill };
            split.Panel1.Controls.Add(_cartesian);
            split.Panel2.Controls.Add(_pie);

            _statusLabel = new Label
            {
                Dock = DockStyle.Bottom,
                Text = "Loading charts...",
                AutoSize = false,
                Height = 26,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 4, 4, 4)
            };

            Controls.Add(split);
            Controls.Add(_statusLabel);
            Size = new Size(1000, 700);
            StartPosition = FormStartPosition.CenterParent;

            Load += async (s, e) => await LoadChartDataAsync();
        }

        private ChartControl CreateCartesianChart()
        {
            var cartesian = new ChartControl
            {
                Dock = DockStyle.Fill,
                Name = "Chart_Cartesian",
                Text = "Budget Trend"
            };
            SfSkinManager.SetVisualStyle(cartesian, ThemeColors.DefaultTheme);

            cartesian.PrimaryXAxis.Title = "Month";
            cartesian.PrimaryYAxis.Title = "Amount ($)";

            return cartesian;
        }

        private ChartControl CreatePieChart()
        {
            var pie = new ChartControl
            {
                Dock = DockStyle.Fill,
                Name = "Chart_Pie",
                Text = "Budget Distribution"
            };
            SfSkinManager.SetVisualStyle(pie, ThemeColors.DefaultTheme);

            // Hide axis for pie chart by setting stroke to transparent
            pie.PrimaryXAxis.DrawGrid = false;
            pie.PrimaryYAxis.DrawGrid = false;

            return pie;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
        }

        private async Task LoadChartDataAsync()
        {
            try
            {
                await _vm.LoadChartDataAsync();

                if (IsDisposed) return;

                if (InvokeRequired)
                {
                    try
                    {
                        BeginInvoke(UpdateCharts);
                    }
                    catch
                    {
                        // BeginInvoke can fail if form is disposing - safe to ignore as operation is cosmetic
                    }
                }
                else
                {
                    UpdateCharts();
                }
            }
            catch (Exception ex)
            {
                try
                {
                    Log.Error(ex, "ChartForm: Load handler failed");
                    ShowStatus($"Error: {ex.Message}", isError: true);
                }
                catch
                {
                    // Logging failure during exception handling - cannot recover, safe to ignore
                }
            }
        }

        private void UpdateCharts()
        {
            try
            {
                if (_cartesian == null || _pie == null) return;

                UpdateCartesianSeries(_cartesian);
                UpdatePieSeries(_pie);
                ShowStatus("Charts updated", isError: false);
            }
            catch (Exception ex)
            {
                try
                {
                    Log.Warning(ex, "ChartForm: updateCharts failed");
                }
                catch
                {
                    // Logging failure during exception handling - cannot recover, safe to ignore
                }
            }
        }

        private void WireCollectionChanges()
        {
            _vm.MonthlyRevenueData.CollectionChanged += CollectionsChanged;
            _vm.PieChartData.CollectionChanged += CollectionsChanged;
            _vm.ChartData.CollectionChanged += CollectionsChanged;
            _vm.LineChartData.CollectionChanged += CollectionsChanged;
        }

        private void UnwireCollectionChanges()
        {
            _vm.MonthlyRevenueData.CollectionChanged -= CollectionsChanged;
            _vm.PieChartData.CollectionChanged -= CollectionsChanged;
            _vm.ChartData.CollectionChanged -= CollectionsChanged;
            _vm.LineChartData.CollectionChanged -= CollectionsChanged;
        }

        private void CollectionsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (IsDisposed || _cartesian == null || _pie == null) return;

            if (InvokeRequired)
            {
                BeginInvoke(UpdateCharts);
            }
            else
            {
                UpdateCharts();
            }
        }

        private void VmOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ChartViewModel.ErrorMessage))
            {
                var message = string.IsNullOrWhiteSpace(_vm.ErrorMessage) ? "Ready" : _vm.ErrorMessage;
                ShowStatus(message ?? "Ready", isError: !string.IsNullOrWhiteSpace(_vm.ErrorMessage));
            }
            else if (e.PropertyName == nameof(ChartViewModel.IsLoading))
            {
                ShowStatus(_vm.IsLoading ? "Loading charts..." : "Charts ready", isError: false);
            }
        }

        private void ShowStatus(string message, bool isError)
        {
            if (_statusLabel == null) return;

            if (InvokeRequired)
            {
                BeginInvoke(() => ShowStatus(message, isError));
                return;
            }

            _statusLabel.Text = message;
            _statusLabel.ForeColor = isError ? ThemeColors.Error : ThemeColors.Success;
        }

        private void UpdateCartesianSeries(ChartControl cartesian)
        {
            cartesian.Series.Clear();
            var series = new ChartSeries("Revenue", ChartSeriesType.Line);
            series.Style.Interior = new BrushInfo(ThemeColors.PrimaryAccent);

            foreach (var data in _vm.MonthlyRevenueData)
            {
                if (data == null) continue;
                series.Points.Add(data.MonthNumber, (double)data.Amount);
            }

            cartesian.Series.Add(series);
        }

        private void UpdatePieSeries(ChartControl pie)
        {
            pie.Series.Clear();
            var pieSeries = new ChartSeries("Distribution", ChartSeriesType.Pie);

            foreach (var p in _vm.PieChartData)
            {
                pieSeries.Points.Add(p.Category, (double)p.Value);
            }

            pie.Series.Add(pieSeries);
        }
    }
}
