using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms.Gauge;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.Controls;
using SPSE = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions;
using WileyWidget.Models;
using WileyWidget.WinForms.Controls.Panels;
using WileyWidget.WinForms.Controls.Supporting;
using WileyWidget.WinForms.Factories;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Tests.Infrastructure;
using WileyWidget.WinForms.Utilities;
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
        DisplayCategory = "Utility rate study",
        Revenue = profitable ? 1_200_000m : 800_000m,
        Expenses = 1_000_000m,
        PriorYearRevenue = 1_050_000m,
        PriorYearExpenses = 980_000m,
        CurrentYearEstimatedRevenue = profitable ? 1_200_000m : 800_000m,
        CurrentYearEstimatedExpenses = 1_000_000m,
        BudgetYearRevenue = 1_260_000m,
        BudgetYearExpenses = 1_040_000m,
        CurrentRate = 47.50m,
        RecommendedRate = 51.25m,
        ReserveCoverageMonths = 5.2m
    };

    private static EnterpriseSnapshot SewerSnap() => new()
    {
        Name = "Sewer",
        Revenue = 950_000m,
        Expenses = 1_100_000m,   // underwater — cross-subsidy case
        PriorYearRevenue = 910_000m,
        PriorYearExpenses = 1_020_000m,
        CurrentYearEstimatedRevenue = 950_000m,
        CurrentYearEstimatedExpenses = 1_100_000m,
        BudgetYearRevenue = 1_000_000m,
        BudgetYearExpenses = 1_140_000m,
        CurrentRate = 39.00m,
        RecommendedRate = 46.00m,
        ReserveCoverageMonths = 2.8m
    };

    private static EnterpriseSnapshot TrashSnap() => new()
    {
        Name = "Trash",
        Revenue = 600_000m,
        Expenses = 560_000m,
        PriorYearRevenue = 570_000m,
        PriorYearExpenses = 545_000m,
        CurrentYearEstimatedRevenue = 600_000m,
        CurrentYearEstimatedExpenses = 560_000m,
        BudgetYearRevenue = 615_000m,
        BudgetYearExpenses = 575_000m,
        CurrentRate = 22.00m,
        RecommendedRate = 23.50m,
        ReserveCoverageMonths = 6.4m
    };

    private static EnterpriseSnapshot ApartmentsSnap() => new()
    {
        Name = "Apartments",
        DisplayCategory = "Operations / income support",
        Revenue = 420_000m,
        Expenses = 380_000m,
        PriorYearRevenue = 405_000m,
        PriorYearExpenses = 372_000m,
        CurrentYearEstimatedRevenue = 420_000m,
        CurrentYearEstimatedExpenses = 380_000m,
        BudgetYearRevenue = 430_000m,
        BudgetYearExpenses = 388_000m,
        ReserveCoverageMonths = 4.1m,
        InsightSummary = "Apartment operations are shown separately so they do not mask utility deficits."
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
    public void EnterpriseTabs_PopulateOneTabPer_Snapshot()
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

        var tabControl = FindDescendantControlByAccessibleName<TabControlAdv>(panel, "Enterprise tabs");
        tabControl.Should().NotBeNull("enterprise TabControlAdv must exist");
        tabControl!.TabPages.Count.Should().Be(vm.EnterpriseSnapshots.Count, "one enterprise tab per snapshot");
    }

    [StaFact]
    public void HeaderActionButtons_AreFullyVisible_WithComfortableTopSpacing()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var scope = provider.CreateScope();
        using var hostForm = new Form
        {
            Width = 1440,
            Height = 900,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-2000, -2000),
        };
        using var panel = CreatePanel(scope.ServiceProvider);

        hostForm.Controls.Add(panel);
        hostForm.CreateControl();
        panel.CreateControl();
        panel.PerformLayout();
        PumpUi();

        var header = FindDescendantControlByAccessibleName<PanelHeader>(panel, "Enterprise vital signs header");

        header.Should().NotBeNull();
        header!.Height.Should().BeGreaterThanOrEqualTo(LayoutTokens.GetScaled(LayoutTokens.HeaderMinimumHeight));

        var refreshButton = FindDescendantControlByAccessibleName<SfButton>(header, "Refresh");
        var closeButton = FindDescendantControlByAccessibleName<SfButton>(header, "Close");

        refreshButton.Should().NotBeNull();
        closeButton.Should().NotBeNull();

        foreach (var button in new[] { refreshButton!, closeButton! })
        {
            var boundsInHeader = header.RectangleToClient(button.RectangleToScreen(button.ClientRectangle));
            boundsInHeader.Top.Should().BeGreaterThanOrEqualTo(4, $"{button.AccessibleName} should not ride the top edge of the header");
            boundsInHeader.Bottom.Should().BeLessThanOrEqualTo(header.ClientSize.Height - 4, $"{button.AccessibleName} should fit cleanly inside the header");
        }
    }

    [StaFact]
    public void EnterpriseTabs_ContainWaterTab_WhenSnapshotsLoaded()
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

        var tabControl = FindDescendantControlByAccessibleName<TabControlAdv>(panel, "Enterprise tabs");
        tabControl.Should().NotBeNull("enterprise TabControlAdv must exist");
        tabControl!.TabPages.Cast<TabPageAdv>().Select(page => page.Text).Should().Contain("Water");
    }

    [StaFact]
    public void EnterpriseTabs_RenderBelowHeader_WithoutTopClipping()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var scope = provider.CreateScope();
        using var hostForm = new Form
        {
            Width = 1440,
            Height = 900,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-2000, -2000),
        };
        using var panel = CreatePanel(scope.ServiceProvider);
        var vm = BuildViewModel(panel);

        vm.EnterpriseSnapshots.Add(WaterSnap());
        vm.EnterpriseSnapshots.Add(SewerSnap());
        vm.EnterpriseSnapshots.Add(TrashSnap());
        vm.EnterpriseSnapshots.Add(ApartmentsSnap());

        hostForm.Controls.Add(panel);
        hostForm.CreateControl();
        panel.CreateControl();
        panel.PerformLayout();
        PumpUi();

        var header = panel.Controls.OfType<TableLayoutPanel>()
            .SelectMany(layout => layout.Controls.OfType<PanelHeader>())
            .Single();
        var tabControl = FindDescendantControlByAccessibleName<TabControlAdv>(panel, "Enterprise tabs");

        tabControl.Should().NotBeNull();

        var tabBoundsInPanel = panel.RectangleToClient(tabControl!.Parent!.RectangleToScreen(tabControl.Bounds));

        tabBoundsInPanel.Top.Should().BeGreaterThanOrEqualTo(header.Bottom + 4,
            "the enterprise tab strip must start below the header so the top edge stays visible and clickable");
        tabBoundsInPanel.Bottom.Should().BeLessThanOrEqualTo(panel.ClientSize.Height - 24,
            "the tab host should remain inside the panel body without clipping the footer");
    }

    [StaFact]
    public void EnterpriseTabs_Clear_WhenSnapshotsReplaced()
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

        var tabControl = FindDescendantControlByAccessibleName<TabControlAdv>(panel, "Enterprise tabs")!;
        tabControl.TabPages.Count.Should().Be(1, "stale enterprise tabs must be cleared on refresh");
    }

    [StaFact]
    public void Panel_ShowsUniversityOfTennesseeRateStudyFootnote()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var scope = provider.CreateScope();
        using var panel = CreatePanel(scope.ServiceProvider);

        var footnote = FindDescendantControlByAccessibleName<Label>(panel, "Enterprise vital signs study footnote");

        footnote.Should().NotBeNull("the panel should disclose the study source used to configure the rate-study view");
        footnote!.Text.Should().Contain("University of Tennessee");
        footnote.Text.Should().Contain("https://trace.tennessee.edu/utk_mtaspubs/164");
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
    public void CreateEnterpriseChart_HasThreeSeries_IncomeExpensesBreakEven()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        var snap = WaterSnap();
        using var scope = CreateScope();
        var factory = SPSE.GetRequiredService<SyncfusionControlFactory>(scope.ServiceProvider);

        using var chart = factory.CreateEnterpriseChart(snap);

        chart.Series.Count.Should().BeGreaterOrEqualTo(3, "chart should expose Income/Expenses/Break Even series");
        var names = chart.Series.Cast<object>()
            .Select(s => s.GetType().GetProperty("Name")?.GetValue(s)?.ToString())
            .ToList();
        names.Should().Contain("Income").And.Contain("Expenses").And.Contain("Break Even");

        foreach (var series in chart.Series.Cast<object>())
        {
            var points = series.GetType().GetProperty("Points")?.GetValue(series) as System.Collections.IList;
            points.Should().NotBeNull();
            points!.Count.Should().BeGreaterOrEqualTo(3, "each series should show prior actual, current estimate, and budget goal");
        }
    }

    [StaFact]
    public void CreateEnterpriseFinancialCard_ShowsMetricFooter()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        var snap = WaterSnap();
        using var scope = CreateScope();
        var factory = SPSE.GetRequiredService<SyncfusionControlFactory>(scope.ServiceProvider);

        using var card = factory.CreateEnterpriseFinancialCard(snap);

        var metricsTable = FindDescendantControlByAccessibleName<TableLayoutPanel>(card, "Water enterprise metrics");
        metricsTable.Should().NotBeNull("financial card should include the metric footer below the chart");
    }

    [StaFact]
    public void CreateEnterpriseFinancialCard_ShowsGaugeBand()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        var snap = WaterSnap();
        using var scope = CreateScope();
        var factory = SPSE.GetRequiredService<SyncfusionControlFactory>(scope.ServiceProvider);

        using var card = factory.CreateEnterpriseFinancialCard(snap);

        FindDescendantControlByAccessibleName<TableLayoutPanel>(card, "Water enterprise gauges")
            .Should().NotBeNull("financial card should include rate-study gauges for recovery, reserves, and rate adequacy");

        FindDescendantControlByAccessibleName<LinearGauge>(card, "Water cost recovery gauge")
            .Should().NotBeNull();
        FindDescendantControlByAccessibleName<LinearGauge>(card, "Water reserve gauge")
            .Should().NotBeNull();
        FindDescendantControlByAccessibleName<LinearGauge>(card, "Water rate adequacy gauge")
            .Should().NotBeNull();
    }

    [StaFact]
    public void CreateEnterpriseFinancialCard_KeepsBottomMetricsAndSummaryVisible_WhenDocked()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        var snap = ApartmentsSnap();
        using var scope = CreateScope();
        var factory = SPSE.GetRequiredService<SyncfusionControlFactory>(scope.ServiceProvider);
        using var hostForm = new Form
        {
            Width = 1360,
            Height = 900,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-2000, -2000),
        };
        using var card = factory.CreateEnterpriseFinancialCard(snap);

        card.Dock = DockStyle.Fill;
        hostForm.Controls.Add(card);
        hostForm.CreateControl();
        card.CreateControl();
        card.PerformLayout();
        PumpUi();

        var metricsTable = FindDescendantControlByAccessibleName<TableLayoutPanel>(card, "Apartments enterprise metrics");
        var summaryLabel = FindDescendantControlByAccessibleName<Label>(card, "Apartments enterprise summary");
        var incomeValue = FindDescendantControlByAccessibleName<Label>(card, "Income value");

        metricsTable.Should().NotBeNull();
        summaryLabel.Should().NotBeNull();
        incomeValue.Should().NotBeNull();

        var metricsBounds = card.RectangleToClient(metricsTable!.Parent!.RectangleToScreen(metricsTable.Bounds));
        var summaryBounds = card.RectangleToClient(summaryLabel!.Parent!.RectangleToScreen(summaryLabel.Bounds));
        var incomeBounds = incomeValue!.Parent!.RectangleToClient(incomeValue.RectangleToScreen(incomeValue.ClientRectangle));

        metricsBounds.Bottom.Should().BeLessThanOrEqualTo(card.ClientSize.Height - LayoutTokens.GetScaled(8),
            "the bottom metrics band should remain fully visible inside the enterprise card");
        summaryBounds.Bottom.Should().BeLessThanOrEqualTo(card.ClientSize.Height,
            "the enterprise summary note should remain visible instead of being cut off at the bottom edge");
        incomeBounds.Bottom.Should().BeLessThanOrEqualTo(incomeValue.Parent!.ClientSize.Height - 2,
            "metric values should fit within their cells without the bottom half being clipped");
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
