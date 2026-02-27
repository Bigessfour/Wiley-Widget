using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SPSE = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions;
using WileyWidget.Models;
using WileyWidget.WinForms.Controls.Panels;
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

    private static EnterpriseSnapshot WaterSnap(bool profitable = true) => new()
    {
        Name = "Water",
        Revenue = profitable ? 1_200_000m : 800_000m,
        Expenses = 1_000_000m
    };

    private static EnterpriseSnapshot SewerSnap() => new()
    {
        Name = "Sewer",
        Revenue = 950_000m,
        Expenses = 1_100_000m   // underwater — cross-subsidy case
    };

    private static EnterpriseSnapshot TrashSnap() => new()
    {
        Name = "Trash",
        Revenue = 600_000m,
        Expenses = 560_000m
    };

    private static EnterpriseSnapshot ApartmentsSnap() => new()
    {
        Name = "Apartments",
        Revenue = 420_000m,
        Expenses = 380_000m
    };

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

        panel.CompletionStatus.Should().Contain("4");
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
        gauge.Ranges.Count.Should().BeGreaterOrEqualTo(2, "gauge should include configured threshold ranges");
    }

    [StaFact]
    public void CreateCircularGauge_HasCorrectValue_WhenBelow100()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        using var scope = CreateScope();
        var factory = SPSE.GetRequiredService<SyncfusionControlFactory>(scope.ServiceProvider);

        using var gauge = factory.CreateCircularGauge(82.3, "Sewer");

        gauge.Value.Should().Be(82.3f);
        gauge.Ranges.Count.Should().BeGreaterOrEqualTo(2, "gauge should include configured threshold ranges");
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
    public void CreateEnterpriseChart_HasThreeSeries_RevenueExpensesBreakEven()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        var snap = WaterSnap();
        using var scope = CreateScope();
        var factory = SPSE.GetRequiredService<SyncfusionControlFactory>(scope.ServiceProvider);

        using var chart = factory.CreateEnterpriseChart(snap);

        chart.Series.Count.Should().BeGreaterOrEqualTo(3, "chart should expose Revenue/Expenses/Break Even series");
        var names = chart.Series.Cast<object>()
            .Select(s => s.GetType().GetProperty("Name")?.GetValue(s)?.ToString())
            .ToList();
        names.Should().Contain("Revenue").And.Contain("Expenses").And.Contain("Break Even");
    }

    [StaFact]
    public void CreateEnterpriseChart_BreakEvenSeries_IsPresent()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        var snap = WaterSnap();
        using var scope = CreateScope();
        var factory = SPSE.GetRequiredService<SyncfusionControlFactory>(scope.ServiceProvider);

        using var chart = factory.CreateEnterpriseChart(snap);

        var breakEven = chart.Series.Cast<object>()
            .FirstOrDefault(s => s.GetType().GetProperty("Name")?.GetValue(s)?.ToString() == "Break Even");
        breakEven.Should().NotBeNull("a Break Even reference series must be present");
    }

    // Legacy dashboard factory tests removed; EnterpriseVitalSignsPanel is the supported path.
}
