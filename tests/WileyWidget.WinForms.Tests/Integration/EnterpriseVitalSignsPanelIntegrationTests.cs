using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Reflection;
using System.Windows.Forms;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms.Chart;
using SPSE = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions;
using WileyWidget.Models;
using WileyWidget.WinForms.Controls.Panels;
using WileyWidget.WinForms.Diagnostics;
using WileyWidget.WinForms.Factories;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Tests.Infrastructure;
using WileyWidget.WinForms.ViewModels;
using Xunit;

namespace WileyWidget.WinForms.Tests.Integration;

[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
public sealed class EnterpriseVitalSignsPanelIntegrationTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    // ── helpers ────────────────────────────────────────────────────────────────

    private static EnterpriseVitalSignsViewModel BuildViewModel(EnterpriseVitalSignsPanel panel)
        => (EnterpriseVitalSignsViewModel)panel.UntypedViewModel!;

    private static EnterpriseVitalSignsPanel CreatePanel(IServiceProvider services)
    {
        var vm = SPSE.GetRequiredService<EnterpriseVitalSignsViewModel>(services);
        var factory = SPSE.GetRequiredService<SyncfusionControlFactory>(services);
        return new EnterpriseVitalSignsPanel(vm, factory);
    }

    private static T? FindDescendantControl<T>(Control root) where T : Control
    {
        foreach (Control child in root.Controls)
        {
            if (child is T match)
            {
                return match;
            }

            var nested = FindDescendantControl<T>(child);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private static T? FindDescendantControlByAccessibleName<T>(Control root, string accessibleName) where T : Control
    {
        foreach (Control child in root.Controls)
        {
            if (child is T match && string.Equals(match.AccessibleName, accessibleName, StringComparison.Ordinal))
            {
                return match;
            }

            var nested = FindDescendantControlByAccessibleName<T>(child, accessibleName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private static void PumpUi(int milliseconds = 120)
    {
        var deadline = Environment.TickCount64 + milliseconds;
        while (Environment.TickCount64 < deadline)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
    }

    private static T? WaitForDescendantControlByAccessibleName<T>(Control root, string accessibleName, int timeoutMilliseconds = 2000)
        where T : Control
    {
        var deadline = Environment.TickCount64 + timeoutMilliseconds;

        do
        {
            var match = FindDescendantControlByAccessibleName<T>(root, accessibleName);
            if (match != null)
            {
                return match;
            }

            Application.DoEvents();
            Thread.Sleep(10);
        }
        while (Environment.TickCount64 < deadline);

        return null;
    }

    private static bool WaitForCondition(Func<bool> condition, int timeoutMilliseconds = 2000)
    {
        var deadline = Environment.TickCount64 + timeoutMilliseconds;

        do
        {
            if (condition())
            {
                return true;
            }

            Application.DoEvents();
            Thread.Sleep(10);
        }
        while (Environment.TickCount64 < deadline);

        return condition();
    }

    private static Control CreateEnterpriseSnapshotPanel(EnterpriseVitalSignsPanel panel, EnterpriseSnapshot snapshot)
    {
        var method = typeof(EnterpriseVitalSignsPanel).GetMethod(
            "CreateEnterpriseSnapshotPanel",
            BindingFlags.Instance | BindingFlags.NonPublic);

        method.Should().NotBeNull("the panel should render enterprise snapshots through a dedicated snapshot panel builder");

        return (Control)method!.Invoke(panel, new object[] { snapshot })!;
    }

    private static EnterpriseSnapshot WaterSnap(bool profitable = true) => new()
    {
        Name = "Water",
        Revenue = profitable ? 1_200_000m : 800_000m,
        Expenses = 1_000_000m,
        MonthlyTrend = CreateMonthlyTrend(profitable ? 1_200_000m : 800_000m, 1_000_000m),
        TrendNarrative = "Jul-Jan uses available actuals; Feb-Jun is projected from the current-year estimate."
    };

    private static EnterpriseSnapshot SewerSnap() => new()
    {
        Name = "Sewer",
        Revenue = 950_000m,
        Expenses = 1_100_000m,   // underwater — cross-subsidy case
        MonthlyTrend = CreateMonthlyTrend(950_000m, 1_100_000m),
        TrendNarrative = "Jul-Jan uses available actuals; Feb-Jun is projected from the current-year estimate."
    };

    private static EnterpriseSnapshot TrashSnap() => new()
    {
        Name = "Trash",
        Revenue = 600_000m,
        Expenses = 560_000m,
        MonthlyTrend = CreateMonthlyTrend(600_000m, 560_000m),
        TrendNarrative = "Jul-Jan uses available actuals; Feb-Jun is projected from the current-year estimate."
    };

    private static EnterpriseSnapshot ApartmentsSnap() => new()
    {
        Name = "Apartments",
        Revenue = 420_000m,
        Expenses = 380_000m,
        MonthlyTrend = CreateMonthlyTrend(420_000m, 380_000m),
        TrendNarrative = "Jul-Jan uses available actuals; Feb-Jun is projected from the current-year estimate."
    };

    private static List<EnterpriseMonthlyTrendPoint> CreateMonthlyTrend(decimal annualRevenue, decimal annualExpenses)
    {
        var revenueWeights = new[] { 0.075m, 0.078m, 0.08m, 0.082m, 0.084m, 0.086m, 0.083m, 0.081m, 0.079m, 0.084m, 0.093m, 0.095m };
        var expenseWeights = new[] { 0.079m, 0.08m, 0.081m, 0.081m, 0.082m, 0.083m, 0.083m, 0.083m, 0.083m, 0.083m, 0.08m, 0.082m };

        var monthlyRevenue = SpreadAnnualTotal(annualRevenue, revenueWeights);
        var monthlyExpenses = SpreadAnnualTotal(annualExpenses, expenseWeights);
        var fiscalStart = new DateTime(2025, 7, 1);

        return Enumerable.Range(0, 12)
            .Select(index => new EnterpriseMonthlyTrendPoint
            {
                MonthStart = fiscalStart.AddMonths(index),
                Revenue = monthlyRevenue[index],
                Expenses = monthlyExpenses[index]
            })
            .ToList();
    }

    private static decimal[] SpreadAnnualTotal(decimal annualTotal, IReadOnlyList<decimal> weights)
    {
        var monthlyTotals = new decimal[weights.Count];
        decimal assigned = 0m;

        for (int index = 0; index < weights.Count - 1; index++)
        {
            monthlyTotals[index] = decimal.Round(annualTotal * weights[index], 2, MidpointRounding.AwayFromZero);
            assigned += monthlyTotals[index];
        }

        monthlyTotals[^1] = annualTotal - assigned;
        return monthlyTotals;
    }

    // ── construction ───────────────────────────────────────────────────────────

    [StaFact]
    public void Panel_ConstructsWithoutException_WithValidDependencies()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var scope = provider.CreateScope();

        var act = () => { using var panel = CreatePanel(scope.ServiceProvider); };

        act.Should().NotThrow();
    }

    [StaFact]
    public void Panel_DocksToFill_OnConstruction()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var scope = provider.CreateScope();

        using var panel = CreatePanel(scope.ServiceProvider);

        panel.Dock.Should().Be(DockStyle.Fill);
    }

    // ── ICompletablePanel ─────────────────────────────────────────────────────

    [StaFact]
    public void IsComplete_IsFalse_WhenSnapshotsEmpty()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var scope = provider.CreateScope();

        using var panel = CreatePanel(scope.ServiceProvider);

        panel.IsComplete.Should().BeFalse();
    }

    [StaFact]
    public void IsComplete_BecomesTrue_AfterSnapshotsLoaded()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var scope = provider.CreateScope();
        using var panel = CreatePanel(scope.ServiceProvider);
        var vm = BuildViewModel(panel);

        // Adding to the collection fires CollectionChanged → RefreshAllVisuals
        vm.EnterpriseSnapshots.Add(WaterSnap());
        vm.EnterpriseSnapshots.Add(SewerSnap());
        vm.EnterpriseSnapshots.Add(TrashSnap());
        vm.EnterpriseSnapshots.Add(ApartmentsSnap());

        panel.IsComplete.Should().BeTrue();
    }

    [StaFact]
    public void CompletionStatus_ReportsCorrectCount()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var scope = provider.CreateScope();
        using var panel = CreatePanel(scope.ServiceProvider);
        var vm = BuildViewModel(panel);

        // Adding to the collection fires CollectionChanged → RefreshAllVisuals
        vm.EnterpriseSnapshots.Add(WaterSnap());
        vm.EnterpriseSnapshots.Add(SewerSnap());
        vm.EnterpriseSnapshots.Add(TrashSnap());
        vm.EnterpriseSnapshots.Add(ApartmentsSnap());
        PumpUi(250);

        panel.CompletionStatus.Should().Contain(vm.EnterpriseSnapshots.Count.ToString());
        panel.CompletionStatus.Should().Contain("ready for council");
    }

    // ── layout refresh ────────────────────────────────────────────────────────

    [StaFact]
    public void GaugeRow_PopulatesOneGaugePer_Snapshot()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var scope = provider.CreateScope();
        using var panel = CreatePanel(scope.ServiceProvider);
        var vm = BuildViewModel(panel);

        vm.EnterpriseSnapshots.Clear();

        // Adding to the collection fires CollectionChanged → RefreshAllVisuals
        vm.EnterpriseSnapshots.Add(WaterSnap());
        vm.EnterpriseSnapshots.Add(SewerSnap());
        vm.EnterpriseSnapshots.Add(TrashSnap());
        vm.EnterpriseSnapshots.Add(ApartmentsSnap());
        PumpUi();

        // The FlowLayoutPanel hosting gauges is docked Top
        var gaugeFlow = FindDescendantControlByAccessibleName<FlowLayoutPanel>(panel, "Enterprise gauges");
        gaugeFlow.Should().NotBeNull("gauge FlowLayoutPanel must exist");
        gaugeFlow!.Controls.Count.Should().Be(vm.EnterpriseSnapshots.Count, "one gauge per snapshot");
    }

    [StaFact]
    public void GaugeRow_IsCenteredWithinGaugeHost_WhenSnapshotsLoaded()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var scope = provider.CreateScope();
        using var panel = CreatePanel(scope.ServiceProvider);
        using var host = new Form
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-2000, -2000),
            Size = new Size(1430, 946),
            ShowInTaskbar = false
        };

        host.Controls.Add(panel);
        host.Show();
        panel.CreateControl();
        PumpUi(200);

        var vm = BuildViewModel(panel);
        vm.EnterpriseSnapshots.Clear();
        vm.EnterpriseSnapshots.Add(WaterSnap());
        vm.EnterpriseSnapshots.Add(SewerSnap());
        vm.EnterpriseSnapshots.Add(TrashSnap());
        vm.EnterpriseSnapshots.Add(ApartmentsSnap());

        PumpUi(250);
        panel.TriggerForceFullLayout();
        PumpUi(250);

        var gaugeHost = FindDescendantControlByAccessibleName<Panel>(panel, "Enterprise gauge host");
        var gaugeFlow = FindDescendantControlByAccessibleName<FlowLayoutPanel>(panel, "Enterprise gauges");

        gaugeHost.Should().NotBeNull();
        gaugeFlow.Should().NotBeNull();

        var expectedLeft = Math.Max(0, (gaugeHost!.ClientSize.Width - gaugeFlow!.PreferredSize.Width) / 2);
        gaugeFlow.Left.Should().BeInRange(expectedLeft - 8, expectedLeft + 8, "the gauge row should stay visually centered across the available width");
    }

    [StaFact]
    public void ChartTable_PopulatesOnePanelPer_Snapshot()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var scope = provider.CreateScope();
        using var panel = CreatePanel(scope.ServiceProvider);
        var vm = BuildViewModel(panel);

        vm.EnterpriseSnapshots.Clear();

        // Adding to the collection fires CollectionChanged → RefreshAllVisuals
        vm.EnterpriseSnapshots.Add(WaterSnap());
        vm.EnterpriseSnapshots.Add(SewerSnap());
        vm.EnterpriseSnapshots.Add(TrashSnap());
        vm.EnterpriseSnapshots.Add(ApartmentsSnap());
        PumpUi();

        // The TableLayoutPanel hosting charts is docked Fill
        var chartTable = FindDescendantControlByAccessibleName<TableLayoutPanel>(panel, "Enterprise chart table");
        chartTable.Should().NotBeNull("chart TableLayoutPanel must exist");
        chartTable!.Controls.Count.Should().Be(vm.EnterpriseSnapshots.Count, "one chart container per snapshot");
    }

    [StaFact]
    public void LayoutAudit_DoesNotReportClippedOrHorizontallyCrowdedControls_AfterSnapshotsLoaded()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var scope = provider.CreateScope();
        using var panel = CreatePanel(scope.ServiceProvider);
        using var host = new Form
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-2000, -2000),
            Size = new Size(1430, 946),
            ShowInTaskbar = false
        };

        host.Controls.Add(panel);
        host.Show();
        panel.CreateControl();
        PumpUi(200);

        try
        {
            var vm = BuildViewModel(panel);
            vm.EnterpriseSnapshots.Clear();
            vm.EnterpriseSnapshots.Add(WaterSnap());
            vm.EnterpriseSnapshots.Add(SewerSnap());
            vm.EnterpriseSnapshots.Add(TrashSnap());
            vm.EnterpriseSnapshots.Add(ApartmentsSnap());
            vm.OverallCityNet = vm.EnterpriseSnapshots.Sum(snapshot => snapshot.NetPosition);

            PumpUi(250);
            panel.TriggerForceFullLayout();
            PumpUi(250);

            var audit = PanelLayoutDiagnostics.Capture(panel);

            audit.ZeroSizedVisibleControls.Should().BeEmpty("EVS overlays and charts should not collapse after the final layout pass");
            audit.ClippedVisibleControls.Should().BeEmpty("EVS cards should fit their non-scrollable viewport at the standard hosted size");
            audit.HorizontalEdgeCrowdedVisibleControls.Should().BeEmpty("EVS should not press visible controls against the horizontal viewport edge after final layout");
        }
        finally
        {
            host.Close();
        }
    }

    [StaFact]
    public void Charts_RemainReadable_AtMediumViewport()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var scope = provider.CreateScope();
        using var panel = CreatePanel(scope.ServiceProvider);
        using var host = new Form
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-2000, -2000),
            Size = new Size(1084, 768),
            ShowInTaskbar = false
        };

        host.Controls.Add(panel);
        host.Show();
        panel.CreateControl();
        PumpUi(200);

        var vm = BuildViewModel(panel);
        vm.EnterpriseSnapshots.Clear();
        vm.EnterpriseSnapshots.Add(WaterSnap());
        vm.EnterpriseSnapshots.Add(SewerSnap());
        vm.EnterpriseSnapshots.Add(TrashSnap());
        vm.EnterpriseSnapshots.Add(ApartmentsSnap());
        vm.OverallCityNet = vm.EnterpriseSnapshots.Sum(snapshot => snapshot.NetPosition);

        PumpUi(250);
        panel.TriggerForceFullLayout();
        PumpUi(250);

        var audit = PanelLayoutDiagnostics.Capture(panel);
        var waterChart = FindDescendantControlByAccessibleName<ChartControl>(panel, "Water current fiscal year chart");
        var sewerChart = FindDescendantControlByAccessibleName<ChartControl>(panel, "Sewer current fiscal year chart");

        audit.ClippedVisibleControls.Should().BeEmpty("the EVS scroll surface should prevent non-scrollable clipping at the medium viewport");
        audit.HorizontalEdgeCrowdedVisibleControls.Should().BeEmpty("EVS cards should keep a usable horizontal gutter at the medium viewport");
        waterChart.Should().NotBeNull();
        sewerChart.Should().NotBeNull();
        waterChart!.Height.Should().BeGreaterThanOrEqualTo(190, "the chart plot area should remain tall enough to render labels and series legibly");
        sewerChart!.Height.Should().BeGreaterThanOrEqualTo(190, "the chart plot area should remain tall enough to render labels and series legibly");
    }

    [StaFact]
    public void SummaryPanel_DisplaysOverallCityNet_WhenSnapshotsLoaded()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var scope = provider.CreateScope();
        using var panel = CreatePanel(scope.ServiceProvider);
        using var host = new Form
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-2000, -2000),
            Size = new Size(1430, 946),
            ShowInTaskbar = false
        };
        var vm = BuildViewModel(panel);

        host.Controls.Add(panel);
        host.Show();
        panel.CreateControl();
        panel.TriggerForceFullLayout();

        vm.EnterpriseSnapshots.Clear();
        vm.EnterpriseSnapshots.Add(WaterSnap());
        vm.EnterpriseSnapshots.Add(SewerSnap());
        vm.EnterpriseSnapshots.Add(TrashSnap());
        vm.EnterpriseSnapshots.Add(ApartmentsSnap());
        vm.OverallCityNet = vm.EnterpriseSnapshots.Sum(snapshot => snapshot.NetPosition);
        WaitForCondition(() => FindDescendantControlByAccessibleName<Label>(panel, "Overall city net value") != null);

        var overallCityNetLabel = FindDescendantControlByAccessibleName<Label>(panel, "Overall city net value");
        overallCityNetLabel.Should().NotBeNull("the panel should surface the enterprise total net position");
        overallCityNetLabel!.Text.Should().Be(vm.OverallCityNet.ToString("C0"));
        var selfSustainingCount = vm.EnterpriseSnapshots.Count(snapshot => snapshot.IsSelfSustaining);
        FindDescendantControlByAccessibleName<Label>(panel, "Self-sustaining count value")!.Text.Should().Be($"{selfSustainingCount} of {vm.EnterpriseSnapshots.Count}");
        FindDescendantControlByAccessibleName<Label>(panel, "Largest gap value")!.Text.Should().Contain("Sewer");
    }

    [StaFact]
    public void SnapshotDetails_DisplayNetStatusAndCrossSubsidyNarrative()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var scope = provider.CreateScope();
        using var panel = CreatePanel(scope.ServiceProvider);
        var vm = BuildViewModel(panel);

        var sewerSnapshot = SewerSnap();
        sewerSnapshot.CrossSubsidyNote = "Supported by citywide enterprise transfers";

        var snapshotPanel = CreateEnterpriseSnapshotPanel(panel, sewerSnapshot);

        WaitForDescendantControlByAccessibleName<Control>(snapshotPanel, "Sewer snapshot details")
            .Should().NotBeNull("per-enterprise details should be visible under each chart");
        WaitForDescendantControlByAccessibleName<Label>(snapshotPanel, "Net Position value")!
            .Text.Should().Be(sewerSnapshot.NetPosition.ToString("C0"));
        WaitForDescendantControlByAccessibleName<Label>(snapshotPanel, "Self-Sustaining value")!
            .Text.Should().Be("No");
        WaitForDescendantControlByAccessibleName<Label>(snapshotPanel, "Cross-Subsidy value")!
            .Text.Should().Be(sewerSnapshot.CrossSubsidyNote);
        WaitForDescendantControlByAccessibleName<Label>(snapshotPanel, "Sewer monthly narrative")!
            .Text.Should().Contain("projected from the current-year estimate");
    }

    [StaFact]
    public void SnapshotCards_DisplayCenteredEnterpriseHeader()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var scope = provider.CreateScope();
        using var panel = CreatePanel(scope.ServiceProvider);

        var snapshotPanel = CreateEnterpriseSnapshotPanel(panel, WaterSnap());

        var titleLabel = WaitForDescendantControlByAccessibleName<Label>(snapshotPanel, "Water snapshot title");
        titleLabel.Should().NotBeNull("each chart card should show a centered enterprise header above the chart");
        titleLabel!.Text.Should().Be("Water");
        titleLabel.TextAlign.Should().Be(ContentAlignment.MiddleCenter);
    }

    [StaFact]
    public void GaugeRow_Clears_WhenSnapshotsReplaced()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var scope = provider.CreateScope();
        using var panel = CreatePanel(scope.ServiceProvider);
        var vm = BuildViewModel(panel);

        vm.EnterpriseSnapshots.Clear();

        // First load
        vm.EnterpriseSnapshots.Add(WaterSnap());
        vm.EnterpriseSnapshots.Add(SewerSnap());
        vm.EnterpriseSnapshots.Add(TrashSnap());
        vm.EnterpriseSnapshots.Add(ApartmentsSnap());
        PumpUi();

        // Replace with fewer snapshots
        vm.EnterpriseSnapshots.Clear();
        vm.EnterpriseSnapshots.Add(WaterSnap());
        PumpUi();

        var gaugeFlow = FindDescendantControlByAccessibleName<FlowLayoutPanel>(panel, "Enterprise gauges")!;
        gaugeFlow.Controls.Count.Should().Be(1, "stale gauges must be cleared on refresh");

        var chartTable = FindDescendantControlByAccessibleName<TableLayoutPanel>(panel, "Enterprise chart table")!;
        chartTable.Controls.Count.Should().Be(1, "stale charts must be cleared on refresh");
        chartTable.RowCount.Should().Be(1, "chart rows should shrink to match the remaining snapshots");
        chartTable.RowStyles.Count.Should().Be(1, "row styles should be rebuilt instead of accumulating across refreshes");
    }

    // ── break-even semantics ──────────────────────────────────────────────────

    [StaFact]
    public void BreakEvenRatio_IsOver100_WhenRevenue_ExceedsExpenses()
    {
        var snap = WaterSnap(profitable: true);  // Revenue 1.2M, Expenses 1.0M

        snap.BreakEvenRatio.Should().BeGreaterThan(100d);
        snap.IsSelfSustaining.Should().BeTrue();
    }

    [StaFact]
    public void BreakEvenRatio_IsUnder100_WhenExpenses_ExceedRevenue()
    {
        var snap = SewerSnap();  // Revenue 0.95M, Expenses 1.1M

        snap.BreakEvenRatio.Should().BeLessThan(100d);
        snap.IsSelfSustaining.Should().BeFalse();
    }

    // ── SyncfusionControlFactory ──────────────────────────────────────────────

    [StaFact]
    public void CreateCircularGauge_DoesNotThrow_WithValidInputs()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        using var scope = CreateScope();
        var factory = SPSE.GetRequiredService<SyncfusionControlFactory>(scope.ServiceProvider);

        var act = () =>
        {
            using var gauge = factory.CreateCircularGauge(87.5, "Water");
        };

        act.Should().NotThrow();
    }

    [StaFact]
    public void CreateCircularGauge_AppliesSyncfusionTheme()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        using var scope = CreateScope();
        var factory = SPSE.GetRequiredService<SyncfusionControlFactory>(scope.ServiceProvider);

        using var gauge = factory.CreateCircularGauge(120.0, "Sewer");

        gauge.ThemeName.Should().NotBeNullOrEmpty("gauge must have theme applied");
    }

    [StaFact]
    public void CreateCircularGauge_HasCorrectValue_WhenAbove100()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        using var scope = CreateScope();
        var factory = SPSE.GetRequiredService<SyncfusionControlFactory>(scope.ServiceProvider);

        using var gauge = factory.CreateCircularGauge(110.0, "Water");

        gauge.Value.Should().Be(110.0f);
        gauge.Ranges.Count.Should().BeGreaterThanOrEqualTo(2, "gauge should include configured threshold ranges");
    }

    [StaFact]
    public void CreateCircularGauge_HasCorrectValue_WhenBelow100()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        using var scope = CreateScope();
        var factory = SPSE.GetRequiredService<SyncfusionControlFactory>(scope.ServiceProvider);

        using var gauge = factory.CreateCircularGauge(82.3, "Sewer");

        gauge.Value.Should().Be(82.3f);
        gauge.Ranges.Count.Should().BeGreaterThanOrEqualTo(2, "gauge should include configured threshold ranges");
    }

    [StaFact]
    public void CreateEnterpriseChart_DoesNotThrow_WithValidSnapshot()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        var snap = WaterSnap();
        using var scope = CreateScope();
        var factory = SPSE.GetRequiredService<SyncfusionControlFactory>(scope.ServiceProvider);

        var act = () =>
        {
            using var chart = factory.CreateEnterpriseChart(snap);
        };

        act.Should().NotThrow();
    }

    [StaFact]
    public void CreateEnterpriseChart_HasTrendSeries_WhenMonthlyTrendPresent()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        var snap = WaterSnap();
        using var scope = CreateScope();
        var factory = SPSE.GetRequiredService<SyncfusionControlFactory>(scope.ServiceProvider);

        using var chart = factory.CreateEnterpriseChart(snap);

        chart.Series.Count.Should().BeGreaterThanOrEqualTo(3, "chart should expose Revenue, Expenses, and Net Position trend series");
        var names = chart.Series.Cast<object>()
            .Select(s => s.GetType().GetProperty("Name")?.GetValue(s)?.ToString())
            .ToList();
        names.Should().Contain("Revenue").And.Contain("Expenses").And.Contain("Net Position");
        chart.Title.Text.Should().Contain("12-Month Fiscal Trend");
    }

    [StaFact]
    public void CreateEnterpriseChart_UsesTwelvePoints_WhenMonthlyTrendPresent()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        var snap = WaterSnap();
        using var scope = CreateScope();
        var factory = SPSE.GetRequiredService<SyncfusionControlFactory>(scope.ServiceProvider);

        using var chart = factory.CreateEnterpriseChart(snap);

        var revenueSeries = chart.Series.Cast<object>()
            .First(s => s.GetType().GetProperty("Name")?.GetValue(s)?.ToString() == "Revenue");
        var points = revenueSeries.GetType().GetProperty("Points")?.GetValue(revenueSeries) as System.Collections.ICollection;
        points.Should().NotBeNull();
        points!.Count.Should().Be(12);
    }

    [StaFact]
    public void CreateEnterpriseChart_BreakEvenSeries_IsPresent_WhenMonthlyTrendUnavailable()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        var snap = new EnterpriseSnapshot
        {
            Name = "Fallback Water",
            Revenue = 1_200_000m,
            Expenses = 1_000_000m
        };
        using var scope = CreateScope();
        var factory = SPSE.GetRequiredService<SyncfusionControlFactory>(scope.ServiceProvider);

        using var chart = factory.CreateEnterpriseChart(snap);

        var breakEven = chart.Series.Cast<object>()
            .FirstOrDefault(s => s.GetType().GetProperty("Name")?.GetValue(s)?.ToString() == "Break Even");
        breakEven.Should().NotBeNull("a Break Even reference series must still be present for fallback annual snapshots");
    }

    // Legacy dashboard factory tests removed; EnterpriseVitalSignsPanel is the supported path.
}
