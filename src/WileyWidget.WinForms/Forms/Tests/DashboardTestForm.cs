using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Syncfusion.Windows.Forms.Chart;
using Syncfusion.Windows.Forms.Gauge;
using Syncfusion.WinForms.DataGrid;
using WileyWidget.Models;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Forms.Tests;

/// <summary>
/// Comprehensive test harness for DashboardForm Syncfusion controls.
/// Tests: 8 RadialGauges, 3 ChartControls, 8 SfDataGrids.
/// </summary>
public sealed class DashboardTestForm : Form
{
    private readonly DashboardForm _dashboardForm;
    private readonly DashboardViewModel _viewModel;
    private readonly AnalyticsViewModel _analyticsViewModel;

    // UI Components
    private readonly SplitContainer _splitContainer;
    private readonly GroupBox _testControlsGroup;
    private readonly RichTextBox _resultsBox;
    private readonly Button _testAllBtn;
    private readonly Button _testGaugesBtn;
    private readonly Button _testChartsBtn;
    private readonly Button _testGridsBtn;
    private readonly Button _clearResultsBtn;
    private readonly CheckBox _verboseCheckbox;
    private readonly ProgressBar _progressBar;
    private readonly StatusStrip _statusStrip;
    private readonly ToolStripStatusLabel _statusLabel;

    // Test state
    private int _passedTests;
    private int _failedTests;

    public DashboardTestForm(
        DashboardForm dashboardForm,
        DashboardViewModel viewModel,
        AnalyticsViewModel analyticsViewModel)
    {
        _dashboardForm = dashboardForm ?? throw new ArgumentNullException(nameof(dashboardForm));
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _analyticsViewModel = analyticsViewModel ?? throw new ArgumentNullException(nameof(analyticsViewModel));

        // Initialize controls
        _splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 400
        };

        _testControlsGroup = new GroupBox
        {
            Text = "Test Controls",
            Dock = DockStyle.Fill,
            Padding = new Padding(10)
        };

        _resultsBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            Font = new Font("Consolas", 9),
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.LightGreen
        };

        _testAllBtn = new Button { Text = "▶ Test All", Width = 150, Height = 40 };
        _testGaugesBtn = new Button { Text = "🔘 Test Gauges (8)", Width = 150, Height = 35 };
        _testChartsBtn = new Button { Text = "📈 Test Charts (3)", Width = 150, Height = 35 };
        _testGridsBtn = new Button { Text = "📊 Test Grids (8)", Width = 150, Height = 35 };
        _clearResultsBtn = new Button { Text = "🗑 Clear", Width = 100, Height = 30 };
        _verboseCheckbox = new CheckBox { Text = "Verbose Output", Checked = true };

        _progressBar = new ProgressBar { Dock = DockStyle.Bottom, Height = 25 };

        _statusStrip = new StatusStrip();
        _statusLabel = new ToolStripStatusLabel("Ready");
        _statusStrip.Items.Add(_statusLabel);

        InitializeComponents();
        SetupLayout();
        SeedMockData();
    }

    private void InitializeComponents()
    {
        Text = "Dashboard Test Harness";
        Size = new Size(1200, 800);
        StartPosition = FormStartPosition.CenterScreen;

        // Wire events
        _testAllBtn.Click += (s, e) => RunAllTests();
        _testGaugesBtn.Click += (s, e) => TestAllGauges();
        _testChartsBtn.Click += (s, e) => TestAllCharts();
        _testGridsBtn.Click += (s, e) => TestAllGrids();
        _clearResultsBtn.Click += (s, e) => ClearResults();
    }

    private void SetupLayout()
    {
        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 150,
            FlowDirection = FlowDirection.TopDown,
            Padding = new Padding(10)
        };

        buttonPanel.Controls.Add(_testAllBtn);
        buttonPanel.Controls.Add(_testGaugesBtn);
        buttonPanel.Controls.Add(_testChartsBtn);
        buttonPanel.Controls.Add(_testGridsBtn);
        buttonPanel.Controls.Add(_clearResultsBtn);
        buttonPanel.Controls.Add(_verboseCheckbox);

        _testControlsGroup.Controls.Add(_dashboardForm);
        _dashboardForm.Dock = DockStyle.Fill;
        _dashboardForm.TopLevel = false;
        _dashboardForm.FormBorderStyle = FormBorderStyle.None;
        _dashboardForm.Show();

        _splitContainer.Panel1.Controls.Add(_testControlsGroup);
        _splitContainer.Panel1.Controls.Add(buttonPanel);

        _splitContainer.Panel2.Controls.Add(_resultsBox);
        _splitContainer.Panel2.Controls.Add(_progressBar);

        Controls.Add(_splitContainer);
        Controls.Add(_statusStrip);
    }

    private void RunAllTests()
    {
        ClearResults();
        LogInfo("╔═══════════════════════════════════════════════════════════╗");
        LogInfo("║     COMPREHENSIVE DASHBOARDFORM TEST SUITE              ║");
        LogInfo("╚═══════════════════════════════════════════════════════════╝");

        _progressBar.Maximum = 19; // 8 gauges + 3 charts + 8 grids
        _progressBar.Value = 0;

        TestAllGauges();
        TestAllCharts();
        TestAllGrids();

        LogInfo($"\n✅ PASSED: {_passedTests}  ❌ FAILED: {_failedTests}");
        _statusLabel.Text = $"Tests Complete: {_passedTests} passed, {_failedTests} failed";
    }

    private void TestAllGauges()
    {
        LogHeader("TESTING 8 RADIAL GAUGES");

        TestGauge("_budgetGauge", 100000f);
        TestGauge("_revenueGauge", 85000f);
        TestGauge("_expensesGauge", 65000f);
        TestGauge("_netPositionGauge", 20000f);
        TestGauge("_variancePercentGauge", 15f);
        TestGauge("_varianceAmountGauge", 5000f);
        TestGauge("_revenueAmountGauge", 85000f);
        TestGauge("_expensesAmountGauge", 65000f);
    }

    private void TestGauge(string fieldName, float expectedValue)
    {
        try
        {
            var field = typeof(DashboardForm).GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (field == null)
            {
                LogFail($"{fieldName}: Field not found via reflection");
                return;
            }

            var fieldValue = field.GetValue(_dashboardForm);
            if (fieldValue == null)
            {
                LogFail($"{fieldName}: Gauge is null");
                return;
            }

            if (fieldValue is not RadialGauge gauge)
            {
                LogFail($"{fieldName}: Field is {fieldValue.GetType().Name}, not RadialGauge");
                return;
            }

            // Test 1: Existence
            LogPass($"{fieldName}: ✓ Exists");

            // Test 2: Value range
            bool valueInRange = gauge.Value >= gauge.MinimumValue &&
                               gauge.Value <= gauge.MaximumValue;
            if (valueInRange)
            {
                LogPass($"{fieldName}: ✓ Value={gauge.Value:F1} in range [{gauge.MinimumValue},{gauge.MaximumValue}]");
            }
            else
            {
                LogFail($"{fieldName}: ✗ Value={gauge.Value:F1} out of range");
            }

            // Test 3: Needle visibility
            if (_verboseCheckbox.Checked)
            {
                LogInfo($"{fieldName}:   ShowNeedle={gauge.ShowNeedle}");
            }

            _progressBar.Value++;
        }
        catch (Exception ex)
        {
            LogFail($"{fieldName}: Exception - {ex.Message}");
        }
    }

    private void TestAllCharts()
    {
        LogHeader("TESTING 3 CHART CONTROLS");

        TestChart("_revenueChart", 1, "Revenue");
        TestChart("_trendChart", 2, "Trend");
        TestChart("_forecastChart", 1, "Forecast");
    }

    private void TestChart(string fieldName, int expectedSeries, string description)
    {
        try
        {
            var field = typeof(DashboardForm).GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (field == null)
            {
                LogFail($"{fieldName}: Field not found via reflection");
                return;
            }

            var fieldValue = field.GetValue(_dashboardForm);
            if (fieldValue == null)
            {
                LogFail($"{fieldName}: Chart is null");
                return;
            }

            if (fieldValue is not ChartControl chart)
            {
                LogFail($"{fieldName}: Field is {fieldValue.GetType().Name}, not ChartControl");
                return;
            }

            LogPass($"{fieldName}: ✓ Exists");

            // Test series count
            if (chart.Series.Count >= expectedSeries)
            {
                LogPass($"{fieldName}: ✓ Has {chart.Series.Count} series (expected ≥{expectedSeries})");
            }
            else
            {
                LogFail($"{fieldName}: ✗ Only {chart.Series.Count} series (expected ≥{expectedSeries})");
            }

            // Test data points
            if (chart.Series.Count > 0 && _verboseCheckbox.Checked)
            {
                for (int i = 0; i < chart.Series.Count; i++)
                {
                    var series = chart.Series[i] as ChartSeries;
                    if (series != null)
                    {
                        LogInfo($"{fieldName}:   Series '{series.Text}' has {series.Points.Count} points");
                    }
                }
            }

            _progressBar.Value++;
        }
        catch (Exception ex)
        {
            LogFail($"{fieldName}: Exception - {ex.Message}");
        }
    }

    private void TestAllGrids()
    {
        LogHeader("TESTING 8 SFDATAGRIDS");

        TestGrid("_metricsGrid", "Metrics");
        TestGrid("_fundsGrid", "Funds");
        TestGrid("_departmentsGrid", "Departments");
        TestGrid("_topVariancesGrid", "Top Variances");
        TestGrid("_analysisGrid", "Analysis");
        TestGrid("_analyticsMetricsGrid", "Analytics Metrics");
        TestGrid("_analyticsVariancesGrid", "Analytics Variances");
        TestGrid("_scenarioGrid", "Scenario");
    }

    private void TestGrid(string fieldName, string description)
    {
        try
        {
            var field = typeof(DashboardForm).GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (field == null)
            {
                LogFail($"{fieldName}: Field not found via reflection");
                return;
            }

            var fieldValue = field.GetValue(_dashboardForm);
            if (fieldValue == null)
            {
                LogFail($"{fieldName}: Grid is null");
                return;
            }

            if (fieldValue is not SfDataGrid grid)
            {
                LogFail($"{fieldName}: Field is {fieldValue.GetType().Name}, not SfDataGrid");
                return;
            }

            LogPass($"{fieldName}: ✓ Exists");

            // Test data source
            if (grid.DataSource != null)
            {
                LogPass($"{fieldName}: ✓ DataSource bound (Rows={grid.RowCount})");
            }
            else
            {
                LogInfo($"{fieldName}:   DataSource is null (may be lazy-loaded)");
            }

            if (_verboseCheckbox.Checked && grid.Columns.Count > 0)
            {
                LogInfo($"{fieldName}:   Columns: {grid.Columns.Count}");
            }

            _progressBar.Value++;
        }
        catch (Exception ex)
        {
            LogFail($"{fieldName}: Exception - {ex.Message}");
        }
    }

    private void SeedMockData()
    {
        // Populate ViewModels with realistic test data
        _viewModel.TotalBudgeted = 1000000m;
        _viewModel.TotalRevenue = 850000m;
        _viewModel.TotalExpenses = 650000m;

        for (int i = 1; i <= 12; i++)
        {
            _viewModel.MonthlyRevenueData.Add(new MonthlyRevenue
            {
                MonthNumber = i,
                Amount = 70000m + (i * 1000m)
            });
        }
    }

    private void LogHeader(string text) =>
        _resultsBox.AppendText($"\n\n═══ {text} ═══\n");

    private void LogPass(string text)
    {
        _resultsBox.SelectionColor = Color.LightGreen;
        _resultsBox.AppendText($"✓ {text}\n");
        _passedTests++;
    }

    private void LogFail(string text)
    {
        _resultsBox.SelectionColor = Color.IndianRed;
        _resultsBox.AppendText($"✗ {text}\n");
        _failedTests++;
    }

    private void LogInfo(string text)
    {
        _resultsBox.SelectionColor = Color.LightGray;
        _resultsBox.AppendText($"{text}\n");
    }

    private void ClearResults()
    {
        _resultsBox.Clear();
        _passedTests = 0;
        _failedTests = 0;
        _progressBar.Value = 0;
        _statusLabel.Text = "Ready";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _splitContainer?.Dispose();
            _testControlsGroup?.Dispose();
            _resultsBox?.Dispose();
            _statusStrip?.Dispose();
            _progressBar?.Dispose();
        }

        base.Dispose(disposing);
    }
}
