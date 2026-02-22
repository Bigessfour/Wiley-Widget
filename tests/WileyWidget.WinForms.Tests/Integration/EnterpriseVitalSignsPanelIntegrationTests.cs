using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
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

    private static EnterpriseVitalSignsViewModel BuildViewModel(IServiceProvider scope)
        => SPSE.GetRequiredService<EnterpriseVitalSignsViewModel>(scope);

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
        var scopeFactory = scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var logger = SPSE.GetRequiredService<ILogger<EnterpriseVitalSignsPanel>>(scope.ServiceProvider);

        var act = () => { using var panel = new EnterpriseVitalSignsPanel(scopeFactory, logger); };

        act.Should().NotThrow();
    }

    [StaFact]
    public void Panel_DocksToFill_OnConstruction()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var scope = provider.CreateScope();
        var scopeFactory = scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var logger = SPSE.GetRequiredService<ILogger<EnterpriseVitalSignsPanel>>(scope.ServiceProvider);

        using var panel = new EnterpriseVitalSignsPanel(scopeFactory, logger);

        panel.Dock.Should().Be(DockStyle.Fill);
    }

    // ── ICompletablePanel ─────────────────────────────────────────────────────

    [StaFact]
    public void IsComplete_IsFalse_WhenSnapshotsEmpty()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var scope = provider.CreateScope();
        var scopeFactory = scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var logger = SPSE.GetRequiredService<ILogger<EnterpriseVitalSignsPanel>>(scope.ServiceProvider);

        using var panel = new EnterpriseVitalSignsPanel(scopeFactory, logger);

        panel.IsComplete.Should().BeFalse();
    }

    [StaFact]
    public void IsComplete_BecomesTrue_AfterSnapshotsLoaded()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var scope = provider.CreateScope();
        var scopeFactory = scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var logger = SPSE.GetRequiredService<ILogger<EnterpriseVitalSignsPanel>>(scope.ServiceProvider);
        var vm = BuildViewModel(scope.ServiceProvider);
        using var panel = new EnterpriseVitalSignsPanel(scopeFactory, logger);

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
        var scopeFactory = scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var logger = SPSE.GetRequiredService<ILogger<EnterpriseVitalSignsPanel>>(scope.ServiceProvider);
        var vm = BuildViewModel(scope.ServiceProvider);
        using var panel = new EnterpriseVitalSignsPanel(scopeFactory, logger);

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
        var scopeFactory = scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var logger = SPSE.GetRequiredService<ILogger<EnterpriseVitalSignsPanel>>(scope.ServiceProvider);
        var vm = BuildViewModel(scope.ServiceProvider);
        using var panel = new EnterpriseVitalSignsPanel(scopeFactory, logger);

        // Adding to the collection fires CollectionChanged → RefreshAllVisuals
        vm.EnterpriseSnapshots.Add(WaterSnap());
        vm.EnterpriseSnapshots.Add(SewerSnap());
        vm.EnterpriseSnapshots.Add(TrashSnap());
        vm.EnterpriseSnapshots.Add(ApartmentsSnap());

        // The FlowLayoutPanel hosting gauges is docked Top
        var gaugeFlow = panel.Controls.OfType<FlowLayoutPanel>().FirstOrDefault();
        gaugeFlow.Should().NotBeNull("gauge FlowLayoutPanel must exist");
        gaugeFlow!.Controls.Count.Should().Be(4, "one gauge per snapshot");
    }

    [StaFact]
    public void ChartTable_PopulatesOnePanelPer_Snapshot()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var scope = provider.CreateScope();
        var scopeFactory = scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var logger = SPSE.GetRequiredService<ILogger<EnterpriseVitalSignsPanel>>(scope.ServiceProvider);
        var vm = BuildViewModel(scope.ServiceProvider);
        using var panel = new EnterpriseVitalSignsPanel(scopeFactory, logger);

        // Adding to the collection fires CollectionChanged → RefreshAllVisuals
        vm.EnterpriseSnapshots.Add(WaterSnap());
        vm.EnterpriseSnapshots.Add(SewerSnap());
        vm.EnterpriseSnapshots.Add(TrashSnap());
        vm.EnterpriseSnapshots.Add(ApartmentsSnap());

        // The TableLayoutPanel hosting charts is docked Fill
        var chartTable = panel.Controls.OfType<TableLayoutPanel>().FirstOrDefault();
        chartTable.Should().NotBeNull("chart TableLayoutPanel must exist");
        chartTable!.Controls.Count.Should().Be(4, "one chart container per snapshot");
    }

    [StaFact]
    public void GaugeRow_Clears_WhenSnapshotsReplaced()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var scope = provider.CreateScope();
        var vm = BuildViewModel(scope.ServiceProvider);
        using var panel = new EnterpriseVitalSignsPanel(vm);

        // First load
        vm.EnterpriseSnapshots = new ObservableCollection<EnterpriseSnapshot>
        {
            WaterSnap(), SewerSnap(), TrashSnap(), ApartmentsSnap()
        };

        // Replace with fewer snapshots
        vm.EnterpriseSnapshots = new ObservableCollection<EnterpriseSnapshot>
        {
            WaterSnap()
        };

        var gaugeFlow = panel.Controls.OfType<FlowLayoutPanel>().FirstOrDefault()!;
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
        using var scope = Fixture.Provider.CreateScope();
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
        using var scope = Fixture.Provider.CreateScope();
        var factory = SPSE.GetRequiredService<SyncfusionControlFactory>(scope.ServiceProvider);

        using var gauge = factory.CreateCircularGauge(120.0, "Sewer");

        gauge.ThemeName.Should().NotBeNullOrEmpty("gauge must have theme applied");
    }

    [StaFact]
    public void CreateCircularGauge_HasCorrectValue_WhenAbove100()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        using var scope = Fixture.Provider.CreateScope();
        var factory = SPSE.GetRequiredService<SyncfusionControlFactory>(scope.ServiceProvider);

        using var gauge = factory.CreateCircularGauge(110.0, "Water");

        gauge.Value.Should().Be(110.0f);
        gauge.Ranges.Should().HaveCount(3, "gauge should have red, yellow, and green ranges");
    }

    [StaFact]
    public void CreateCircularGauge_HasCorrectValue_WhenBelow100()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        using var scope = Fixture.Provider.CreateScope();
        var factory = SPSE.GetRequiredService<SyncfusionControlFactory>(scope.ServiceProvider);

        using var gauge = factory.CreateCircularGauge(82.3, "Sewer");

        gauge.Value.Should().Be(82.3f);
        gauge.Ranges.Should().HaveCount(3, "gauge should have red, yellow, and green ranges");
    }

    [StaFact]
    public void CreateEnterpriseChart_DoesNotThrow_WithValidSnapshot()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        var snap = WaterSnap();
        using var scope = Fixture.Provider.CreateScope();
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
        using var scope = Fixture.Provider.CreateScope();
        var factory = SPSE.GetRequiredService<SyncfusionControlFactory>(scope.ServiceProvider);

        using var chart = factory.CreateEnterpriseChart(snap);

        chart.Series.Count.Should().Be(3, "Revenue, Expenses, and Break Even series");
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
        using var scope = Fixture.Provider.CreateScope();
        var factory = SPSE.GetRequiredService<SyncfusionControlFactory>(scope.ServiceProvider);

        using var chart = factory.CreateEnterpriseChart(snap);

        var breakEven = chart.Series.Cast<object>()
            .FirstOrDefault(s => s.GetType().GetProperty("Name")?.GetValue(s)?.ToString() == "Break Even");
        breakEven.Should().NotBeNull("a Break Even reference series must be present");
    }

    // Legacy dashboard factory tests removed; EnterpriseVitalSignsPanel is the supported path.
}
