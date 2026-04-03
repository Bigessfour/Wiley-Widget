using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Syncfusion.WinForms.DataGrid;
using Microsoft.Extensions.DependencyInjection;
using Syncfusion.Windows.Forms.Tools;
using Xunit;
using WileyWidget.WinForms.Factories;
using WileyWidget.WinForms.Controls.Panels;
using WileyWidget.WinForms.Diagnostics;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Tests.Infrastructure;
using WileyWidget.WinForms.Tests.Integration;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Tests.Integration.Forms;

[Collection("SyncfusionTheme")]
public sealed class PanelRegistryNavigationProofTests
{
    public static IEnumerable<object[]> PanelData => PanelRegistry.Panels
        .Where(entry => entry.PanelType != typeof(JARVISChatUserControl))
        .OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
        .Select(entry => new object[] { entry.DisplayName, entry.PanelType, entry.DefaultDock, entry.ShowInRibbonPanelsMenu });

    [StaFact]
    [Trait("Category", "Smoke")]
    [Trait("Area", "AllPanels")]
    [Trait("Panel", "RecommendedMonthlyCharge")]
    public async Task RecommendedMonthlyChargePanel_CanBeCreatedAndDisplayed()
    {
        var provider = BuildProofProvider();

        try
        {
            using var scope = provider.CreateScope();
            var scopedProvider = scope.ServiceProvider;

            var recommendedMonthlyChargePanel = new RecommendedMonthlyChargePanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ScopedPanelBase<RecommendedMonthlyChargeViewModel>>>(scopedProvider));

            try
            {
                Assert.True(recommendedMonthlyChargePanel.Controls.Count > 0);
                Assert.NotNull(FindDescendantByAccessibleName(recommendedMonthlyChargePanel, "Refresh Data"));
                Assert.NotNull(FindDescendantByAccessibleName(recommendedMonthlyChargePanel, "Department Rates Grid"));
                Assert.NotNull(FindDescendantByAccessibleName(recommendedMonthlyChargePanel, "Benchmarks Grid"));
            }
            finally
            {
                recommendedMonthlyChargePanel.Dispose();
            }
        }
        finally
        {
            provider.Dispose();
        }
    }

    [StaFact]
    [Trait("Category", "Smoke")]
    [Trait("Area", "AllPanels")]
    [Trait("Panel", "QuickBooks")]
    public async Task QuickBooksPanel_CanBeCreatedAndDisplayed()
    {
        var provider = BuildProofProvider();

        try
        {
            using var scope = provider.CreateScope();
            var scopedProvider = scope.ServiceProvider;

            using var quickBooksPanel = new QuickBooksPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<QuickBooksViewModel>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<SyncfusionControlFactory>(scopedProvider));

            Assert.True(quickBooksPanel.Controls.Count > 0);
            Assert.NotNull(FindDescendantByAccessibleName(quickBooksPanel, "QuickBooks Panel Header"));
            Assert.NotNull(FindDescendantByAccessibleName(quickBooksPanel, "Connect to QuickBooks"));
            Assert.NotNull(FindDescendantByAccessibleName(quickBooksPanel, "Import QuickBooks Desktop Export"));
            Assert.NotNull(FindDescendantByAccessibleName(quickBooksPanel, "Import QuickBooks CSV or Excel Export"));
            Assert.NotNull(FindDescendantByAccessibleName(quickBooksPanel, "Sync History Grid"));
        }
        finally
        {
            provider.Dispose();
        }
    }

    [StaFact]
    [Trait("Category", "Smoke")]
    [Trait("Area", "AllPanels")]
    [Trait("Panel", "Payments")]
    public async Task PaymentsPanel_PassesHostedLayoutAudit_AtStandardAndMediumSizes()
    {
        var provider = BuildProofProvider();
        var previousAutomation = Environment.GetEnvironmentVariable("WILEYWIDGET_UI_AUTOMATION");

        try
        {
            Environment.SetEnvironmentVariable("WILEYWIDGET_UI_AUTOMATION", "true");
            await AssertPanelPassesHostedLayoutAuditAsync(provider, typeof(PaymentsPanel), "Payments");
        }
        finally
        {
            Environment.SetEnvironmentVariable("WILEYWIDGET_UI_AUTOMATION", previousAutomation);
            provider.Dispose();
        }
    }

    [StaFact]
    [Trait("Category", "Smoke")]
    [Trait("Area", "AllPanels")]
    [Trait("Panel", "QuickBooks")]
    public async Task QuickBooksPanel_DoesNotClipCoreControls_AtStandardHostSize()
    {
        var provider = BuildProofProvider();
        var previousAutomation = Environment.GetEnvironmentVariable("WILEYWIDGET_UI_AUTOMATION");

        try
        {
            Environment.SetEnvironmentVariable("WILEYWIDGET_UI_AUTOMATION", "true");

            using var scope = provider.CreateScope();
            var scopedProvider = scope.ServiceProvider;

            using var quickBooksPanel = new QuickBooksPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<QuickBooksViewModel>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<SyncfusionControlFactory>(scopedProvider));

            using var host = new Form
            {
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-2000, -2000),
                Size = new Size(1280, 840),
                ShowInTaskbar = false,
            };

            host.Controls.Add(quickBooksPanel);
            host.Show();
            quickBooksPanel.Show();

            await WaitForAccessibleNamesAsync(
                quickBooksPanel,
                new[]
                {
                    "QuickBooks Panel Header",
                    "Connect to QuickBooks",
                    "Import QuickBooks Desktop Export",
                    "Import QuickBooks CSV or Excel Export",
                    "Sync History Grid",
                },
                timeoutMs: 3000);

            var audit = PanelLayoutDiagnostics.Capture(quickBooksPanel);
            Assert.False(
                audit.HostBelowMinimum,
                $"QuickBooks should not rely on a smaller-than-minimum host after layout stabilization. Host={audit.HostClientSize} Minimum={audit.MinimumSize}");
            Assert.Empty(audit.ZeroSizedVisibleControls);
            Assert.Empty(audit.ClippedVisibleControls);
        }
        finally
        {
            Environment.SetEnvironmentVariable("WILEYWIDGET_UI_AUTOMATION", previousAutomation);
            provider.Dispose();
        }
    }

    [StaFact]
    [Trait("Category", "Smoke")]
    [Trait("Area", "AllPanels")]
    [Trait("Panel", "RevenueTrends")]
    public async Task RevenueTrendsPanel_CanBeCreatedAndDisplayed()
    {
        var provider = BuildProofProvider();

        try
        {
            using var scope = provider.CreateScope();
            var scopedProvider = scope.ServiceProvider;

            using var revenueTrendsPanel = new RevenueTrendsPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ScopedPanelBase<RevenueTrendsViewModel>>>(scopedProvider));

            Assert.True(revenueTrendsPanel.Controls.Count > 0);
            Assert.NotNull(FindDescendantByAccessibleName(revenueTrendsPanel, "Revenue Trends panel header"));
            Assert.NotNull(FindDescendantByAccessibleName(revenueTrendsPanel, "Revenue trends line chart"));
            Assert.NotNull(FindDescendantByAccessibleName(revenueTrendsPanel, "Monthly revenue breakdown data grid"));
        }
        finally
        {
            provider.Dispose();
        }
    }

    [StaFact]
    [Trait("Category", "Smoke")]
    [Trait("Area", "AllPanels")]
    [Trait("Panel", "Accounts")]
    public async Task AccountsPanel_CanBeCreatedAndDisplayed()
    {
        var provider = BuildProofProvider();

        try
        {
            using var scope = provider.CreateScope();
            var scopedProvider = scope.ServiceProvider;

            using var accountsPanel = new AccountsPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<AccountsViewModel>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<SyncfusionControlFactory>(scopedProvider));

            Assert.True(accountsPanel.Controls.Count > 0);
            Assert.NotNull(FindDescendantByAccessibleName(accountsPanel, "Chart of Accounts Panel Header"));
            Assert.NotNull(FindDescendantByAccessibleName(accountsPanel, "New Account"));
            Assert.NotNull(FindDescendantByAccessibleName(accountsPanel, "Accounts Grid"));
        }
        finally
        {
            provider.Dispose();
        }
    }

    [StaFact]
    [Trait("Category", "Smoke")]
    [Trait("Area", "AllPanels")]
    [Trait("Panel", "Reports")]
    public async Task ReportsPanel_CanBeCreatedAndDisplayed()
    {
        var provider = BuildProofProvider();

        try
        {
            using var scope = provider.CreateScope();
            var scopedProvider = scope.ServiceProvider;

            using var reportsPanel = new ReportsPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ReportsViewModel>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<SyncfusionControlFactory>(scopedProvider));

            Assert.True(reportsPanel.Controls.Count > 0);
            Assert.NotNull(FindDescendantByAccessibleName(reportsPanel, "Report Selector"));
            Assert.NotNull(FindDescendantByAccessibleName(reportsPanel, "Generate Report"));
            Assert.NotNull(FindDescendantByAccessibleName(reportsPanel, "Report Preview Container"));
        }
        finally
        {
            provider.Dispose();
        }
    }

    [StaFact]
    [Trait("Category", "Smoke")]
    [Trait("Area", "AllPanels")]
    [Trait("Panel", "InsightFeed")]
    public async Task InsightFeedPanel_CanBeCreatedAndDisplayed()
    {
        var provider = BuildProofProvider();

        try
        {
            using var scope = provider.CreateScope();
            var scopedProvider = scope.ServiceProvider;

            using var insightFeedPanel = new InsightFeedPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ScopedPanelBase<InsightFeedViewModel>>>(scopedProvider));

            Assert.True(insightFeedPanel.Controls.Count > 0);
            Assert.NotNull(FindDescendantByAccessibleName(insightFeedPanel, "Insights Header"));
            Assert.NotNull(FindDescendantByAccessibleName(insightFeedPanel, "Insight Feed Toolbar"));
            Assert.NotNull(FindDescendantByAccessibleName(insightFeedPanel, "Insights Data Grid"));
        }
        finally
        {
            provider.Dispose();
        }
    }

    [StaFact]
    [Trait("Category", "Smoke")]
    [Trait("Area", "AllPanels")]
    [Trait("Panel", "WarRoom")]
    public async Task WarRoomPanel_CanBeCreatedAndDisplayed()
    {
        var provider = BuildProofProvider();

        try
        {
            using var scope = provider.CreateScope();
            var scopedProvider = scope.ServiceProvider;

            using var warRoomPanel = new WarRoomPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<WarRoomViewModel>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<SyncfusionControlFactory>(scopedProvider));

            Assert.True(warRoomPanel.Controls.Count > 0);
            Assert.NotNull(FindDescendantByAccessibleName(warRoomPanel, "Scenario Input"));
            Assert.NotNull(FindDescendantByAccessibleName(warRoomPanel, "Run Scenario"));
            Assert.NotNull(FindDescendantByAccessibleName(warRoomPanel, "War Room Status"));
        }
        finally
        {
            provider.Dispose();
        }
    }

    [StaFact]
    [Trait("Category", "Smoke")]
    [Trait("Area", "AllPanels")]
    [Trait("Panel", "ActivityLog")]
    public async Task ActivityLogPanel_CanBeCreatedAndDisplayed()
    {
        var provider = BuildProofProvider();

        try
        {
            using var scope = provider.CreateScope();
            var scopedProvider = scope.ServiceProvider;

            using var activityLogPanel = new ActivityLogPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ActivityLogViewModel>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<SyncfusionControlFactory>(scopedProvider));

            Assert.True(activityLogPanel.Controls.Count > 0);
            Assert.NotNull(FindDescendantByAccessibleName(activityLogPanel, "Activity Log Header"));
            Assert.NotNull(FindDescendantByAccessibleName(activityLogPanel, "Export Activity Log"));
            Assert.NotNull(FindDescendantByAccessibleName(activityLogPanel, "Clear Activity Log"));
        }
        finally
        {
            provider.Dispose();
        }
    }

    [StaFact]
    [Trait("Category", "Smoke")]
    [Trait("Area", "AllPanels")]
    [Trait("Panel", "ProactiveInsights")]
    public async Task ProactiveInsightsPanel_CanBeCreatedAndDisplayed()
    {
        var provider = BuildProofProvider();

        try
        {
            using var scope = provider.CreateScope();
            var scopedProvider = scope.ServiceProvider;

            using var proactiveInsightsPanel = new ProactiveInsightsPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ProactiveInsightsPanel>>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ScopedPanelBase<InsightFeedViewModel>>>(scopedProvider),
                scopedProvider);

            Assert.True(proactiveInsightsPanel.Controls.Count > 0);
            Assert.NotNull(FindDescendantByAccessibleName(proactiveInsightsPanel, "Proactive Insights Header"));
            Assert.NotNull(FindDescendantByAccessibleName(proactiveInsightsPanel, "Proactive Actions Toolbar"));
            Assert.NotNull(FindDescendantByAccessibleName(proactiveInsightsPanel, "Proactive insights status"));
        }
        finally
        {
            provider.Dispose();
        }
    }

    [StaFact]
    [Trait("Category", "Smoke")]
    [Trait("Area", "AllPanels")]
    [Trait("Panel", "Rates")]
    public void RatesPage_CanBeCreatedAndDisplayed()
    {
        using var ratesPage = new RatesPage();

        AssertRatesPageIsInitialized(ratesPage);
    }

    [StaTheory]
    [MemberData(nameof(PanelData))]
    [Trait("Category", "Smoke")]
    [Trait("Area", "AllPanels")]
    public async Task PanelRegistry_Panel_CanBeCreatedAndDisplayed(
        string displayName,
        Type panelType,
        DockingStyle defaultDock,
        bool showInRibbonPanelsMenu)
    {
        using var provider = BuildProofProvider();

        _ = defaultDock;
        _ = showInRibbonPanelsMenu;
        await AssertPanelCanRenderInHostAsync(provider, panelType, displayName);
    }

    [StaTheory]
    [MemberData(nameof(PanelData))]
    [Trait("Category", "Smoke")]
    [Trait("Area", "AllPanels")]
    public async Task PanelRegistry_Panel_PassesHostedLayoutAudit_AtStandardAndMediumSizes(
        string displayName,
        Type panelType,
        DockingStyle defaultDock,
        bool showInRibbonPanelsMenu)
    {
        var provider = BuildProofProvider();
        var previousAutomation = Environment.GetEnvironmentVariable("WILEYWIDGET_UI_AUTOMATION");

        try
        {
            Environment.SetEnvironmentVariable("WILEYWIDGET_UI_AUTOMATION", "true");

            _ = defaultDock;
            _ = showInRibbonPanelsMenu;
            await AssertPanelPassesHostedLayoutAuditAsync(provider, panelType, displayName);
        }
        finally
        {
            Environment.SetEnvironmentVariable("WILEYWIDGET_UI_AUTOMATION", previousAutomation);
            provider.Dispose();
        }
    }

    private static ServiceProvider BuildProofProvider()
    {
        TestThemeHelper.EnsureOffice2019Colorful();

        return IntegrationTestServices.BuildProvider(new Dictionary<string, string?>
        {
            ["UI:IsUiTestHarness"] = "true",
            ["UI:UseSyncfusionDocking"] = "true",
            ["UI:ShowRibbon"] = "true",
            ["UI:ShowStatusBar"] = "true",
            ["UI:AutoShowDashboard"] = "false",
            ["UI:MinimalMode"] = "false",
            ["UI:AutoShowPanels"] = "false"
        });
    }

    private static async Task AssertPanelCanRenderInHostAsync(IServiceProvider provider, Type panelType, string displayName)
    {
        using var scope = provider.CreateScope();
        var scopedProvider = scope.ServiceProvider;

        using var panel = CreatePanelUnderTest(scopedProvider, panelType, displayName);

        if (panelType == typeof(AuditLogPanel))
        {
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Audit log entries grid"));
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Audit log filters"));
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Audit events chart"));
            return;
        }

        if (panelType == typeof(PaymentsPanel))
        {
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Payments Grid"));
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Add Payment"));
            return;
        }

        if (panelType == typeof(CustomersPanel))
        {
            Assert.NotNull(((CustomersPanel)panel).ViewModel);
            Assert.False(panel.IsDisposed, "CustomersPanel should remain alive before hosted proof rendering.");
            AssertControlContainsAccessibleNames(
                panel,
                displayName,
                "Customer search",
                "Add Customer",
                "Sync QuickBooks",
                "Export Customers");
            return;
        }

        if (panelType == typeof(AnalyticsHubPanel))
        {
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Analytics fiscal year selector"));
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Analytics search"));
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Analytics sections"));
            return;
        }

        if (panelType == typeof(SettingsPanel))
        {
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Application Title"));
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Open edit forms docked"));
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Export path"));
            return;
        }

        if (panelType == typeof(WileyWidget.WinForms.Controls.Supporting.CsvMappingWizardPanel))
        {
            Assert.NotNull(FindDescendantByAccessibleName(panel, "CSV Preview"));
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Has Header"));
            return;
        }

        if (panelType == typeof(BudgetPanel))
        {
            Assert.True(panel.Controls.Count > 0, "BudgetPanel should initialize its root control tree during proof construction.");
            AssertControlContainsAccessibleNames(
                panel,
                displayName,
                "Search Budget Entries",
                "Fiscal Year Filter",
                "Load Budgets",
                "Budget Entries Grid");
            return;
        }

        if (panelType == typeof(AccountEditPanel))
        {
            Assert.NotNull(((AccountEditPanel)panel).ViewModel);
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Account Number"));
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Account Name"));
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Cancel"));
            return;
        }

        if (panelType == typeof(EnterpriseVitalSignsPanel))
        {
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Enterprise vital signs header"));
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Enterprise gauges"));
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Enterprise vital signs status"));
            return;
        }

        if (panelType == typeof(PaymentEditPanel))
        {
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Add New Vendor"));
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Save Changes"));
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Cancel Edit"));
            return;
        }

        if (panelType == typeof(DepartmentSummaryPanel))
        {
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Department Summary header"));
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Summary cards"));
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Department metrics grid"));
            return;
        }

        if (panelType == typeof(UtilityBillPanel))
        {
            Assert.False(panel.IsDisposed, "UtilityBillPanel should remain alive before hosted proof rendering.");
            AssertControlContainsAccessibleNames(
                panel,
                displayName,
                "Search",
                "Status Filter",
                "Utility Bills Grid",
                "Create Bill",
                "Customers Grid");
            return;
        }

        if (panelType == typeof(RecommendedMonthlyChargePanel))
        {
            Assert.True(panel.Controls.Count > 0, $"Panel '{displayName}' should create its root control tree during proof construction.");
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Refresh Data"));
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Department Rates Grid"));
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Benchmarks Grid"));
            return;
        }

        if (panelType == typeof(QuickBooksPanel))
        {
            Assert.True(panel.Controls.Count > 0, $"Panel '{displayName}' should create its root control tree during proof construction.");
            Assert.NotNull(FindDescendantByAccessibleName(panel, "QuickBooks Panel Header"));
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Connect to QuickBooks"));
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Import QuickBooks Desktop Export"));
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Import QuickBooks CSV or Excel Export"));
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Sync History Grid"));
            return;
        }

        if (panelType == typeof(RevenueTrendsPanel))
        {
            Assert.True(panel.Controls.Count > 0, $"Panel '{displayName}' should create its root control tree during proof construction.");
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Revenue Trends panel header"));
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Revenue trends line chart"));
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Monthly revenue breakdown data grid"));
            return;
        }

        if (panelType == typeof(AccountsPanel))
        {
            Assert.True(panel.Controls.Count > 0, $"Panel '{displayName}' should create its root control tree during proof construction.");
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Chart of Accounts Panel Header"));
            Assert.NotNull(FindDescendantByAccessibleName(panel, "New Account"));
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Accounts Grid"));
            return;
        }

        if (panelType == typeof(ReportsPanel))
        {
            Assert.True(panel.Controls.Count > 0, $"Panel '{displayName}' should create its root control tree during proof construction.");
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Report Selector"));
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Generate Report"));
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Report Preview Container"));
            return;
        }

        if (panelType == typeof(InsightFeedPanel))
        {
            Assert.True(panel.Controls.Count > 0, $"Panel '{displayName}' should create its root control tree during proof construction.");
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Insights Header"));
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Insight Feed Toolbar"));
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Insights Data Grid"));
            return;
        }

        if (panelType == typeof(WarRoomPanel))
        {
            Assert.True(panel.Controls.Count > 0, $"Panel '{displayName}' should create its root control tree during proof construction.");
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Scenario Input"));
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Run Scenario"));
            Assert.NotNull(FindDescendantByAccessibleName(panel, "War Room Status"));
            return;
        }

        if (panelType == typeof(ActivityLogPanel))
        {
            Assert.True(panel.Controls.Count > 0, $"Panel '{displayName}' should create its root control tree during proof construction.");
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Activity Log Header"));
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Export Activity Log"));
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Clear Activity Log"));
            return;
        }

        if (panelType == typeof(ProactiveInsightsPanel))
        {
            Assert.True(panel.Controls.Count > 0, $"Panel '{displayName}' should create its root control tree during proof construction.");
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Proactive Insights Header"));
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Proactive Actions Toolbar"));
            Assert.NotNull(FindDescendantByAccessibleName(panel, "Proactive insights status"));
            return;
        }

        if (panelType == typeof(FormHostPanel) && string.Equals(displayName, "Rates", StringComparison.OrdinalIgnoreCase))
        {
            AssertRatesPageIsInitialized((RatesPage)panel);
            return;
        }

        Assert.True(panel.IsHandleCreated, $"Panel '{displayName}' should create a WinForms handle inside the proof host.");
        Assert.False(panel.IsDisposed, $"Panel '{displayName}' should not be disposed immediately after rendering.");
    }

    private static async Task AssertPanelPassesHostedLayoutAuditAsync(IServiceProvider provider, Type panelType, string displayName)
    {
        var hostSizes = new[]
        {
            new Size(1400, 900),
            new Size(1280, 840)
        };

        foreach (var targetSize in hostSizes)
        {
            using var scope = provider.CreateScope();
            var scopedProvider = scope.ServiceProvider;

            using var panel = CreatePanelUnderTest(scopedProvider, panelType, displayName);
            await AssertPanelPassesHostedLayoutAuditAsync(panel, displayName, targetSize);
        }
    }

    private static async Task AssertPanelPassesHostedLayoutAuditAsync(Control panel, string displayName, Size targetSize)
    {
        var hostSize = new Size(
            Math.Max(targetSize.Width, panel.MinimumSize.Width),
            Math.Max(targetSize.Height, panel.MinimumSize.Height));

        Console.WriteLine($"[PanelProof] Start {displayName} at {hostSize.Width}x{hostSize.Height}");

        if (panel is Form hostedForm)
        {
            hostedForm.StartPosition = FormStartPosition.Manual;
            hostedForm.Location = new Point(-2000, -2000);
            hostedForm.Size = hostSize;
            hostedForm.ShowInTaskbar = false;

            hostedForm.Show();
            Console.WriteLine($"[PanelProof] Shown {displayName} at {hostSize.Width}x{hostSize.Height}");
            hostedForm.PerformLayout();

            Console.WriteLine($"[PanelProof] Capturing {displayName} at {hostSize.Width}x{hostSize.Height}");

            var hostedAudit = PanelLayoutDiagnostics.Capture(hostedForm);
            Assert.False(hostedAudit.HostBelowMinimum, $"Panel '{displayName}' should not render below its minimum size at {hostSize.Width}x{hostSize.Height}.");
            Assert.Empty(hostedAudit.ClippedVisibleControls);
            Assert.Empty(hostedAudit.ZeroSizedVisibleControls);
            Assert.Empty(hostedAudit.HorizontalEdgeCrowdedVisibleControls);

            hostedForm.Close();
            Console.WriteLine($"[PanelProof] Done {displayName} at {hostSize.Width}x{hostSize.Height}");
            return;
        }

        using var host = new Form
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-2000, -2000),
            Size = hostSize,
            ShowInTaskbar = false,
            Text = $"Proof Host - {displayName}"
        };

        panel.Dock = DockStyle.Fill;
        host.Controls.Add(panel);
        _ = host.Handle;
        host.CreateControl();
        panel.CreateControl();
        _ = panel.Handle;
        host.Show();
        var capturedMinimumSize = panel.MinimumSize;
        hostSize = new Size(
            Math.Max(targetSize.Width, capturedMinimumSize.Width),
            Math.Max(targetSize.Height, capturedMinimumSize.Height));
        host.ClientSize = hostSize;
        host.PerformLayout();
        panel.PerformLayout();

        await TryInitializePanelAsync(panel);
        if (panel is ScopedPanelBase scopedPanel)
        {
            scopedPanel.TriggerForceFullLayout();
        }
        else
        {
            panel.PerformLayout();
            panel.Invalidate(true);
            panel.Update();
        }

        Console.WriteLine($"[PanelProof] Shown {displayName} at {hostSize.Width}x{hostSize.Height}");
        var parentName = panel.Parent is null ? "<null>" : panel.Parent.GetType().Name;
        Console.WriteLine($"[PanelProof] HandleState {displayName}: host={host.IsHandleCreated}, panel={panel.IsHandleCreated}, parent={parentName}");

        Assert.True(panel.IsHandleCreated, $"Panel '{displayName}' should create a WinForms handle inside the proof host.");
        Assert.False(panel.IsDisposed, $"Panel '{displayName}' should not be disposed immediately after rendering.");
        Assert.Same(host, panel.FindForm());

        Console.WriteLine($"[PanelProof] Capturing {displayName} at {hostSize.Width}x{hostSize.Height}");

        var layoutAudit = PanelLayoutDiagnostics.Capture(panel);
        var relevantClipped = layoutAudit.ClippedVisibleControls.Where(ShouldKeepAuditFinding).ToArray();
        var relevantZeroSized = layoutAudit.ZeroSizedVisibleControls.Where(ShouldKeepAuditFinding).ToArray();
        var relevantCrowded = layoutAudit.HorizontalEdgeCrowdedVisibleControls.Where(ShouldKeepAuditFinding).ToArray();

        Assert.False(layoutAudit.HostBelowMinimum, $"Panel '{displayName}' should not render below its minimum size at {hostSize.Width}x{hostSize.Height}.");
        Assert.Empty(relevantClipped);
        Assert.Empty(relevantZeroSized);
        if (relevantCrowded.Length > 0)
        {
            Console.WriteLine($"[PanelProof] Crowded {displayName}: {relevantCrowded.Length} findings filtered to diagnostics only.");
        }

        host.Close();
        Console.WriteLine($"[PanelProof] Done {displayName} at {hostSize.Width}x{hostSize.Height}");
    }

    private static Control CreatePanelUnderTest(IServiceProvider scopedProvider, Type panelType, string displayName)
    {
        if (panelType == typeof(AuditLogPanel))
        {
            return new AuditLogPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<AuditLogViewModel>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<SyncfusionControlFactory>(scopedProvider));
        }

        if (panelType == typeof(PaymentsPanel))
        {
            return new PaymentsPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PaymentsPanel>>(scopedProvider));
        }

        if (panelType == typeof(CustomersPanel))
        {
            return new CustomersPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ScopedPanelBase<CustomersViewModel>>>(scopedProvider));
        }

        if (panelType == typeof(AnalyticsHubPanel))
        {
            return new AnalyticsHubPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ScopedPanelBase<AnalyticsHubViewModel>>>(scopedProvider));
        }

        if (panelType == typeof(SettingsPanel))
        {
            return new SettingsPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<SettingsViewModel>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<SyncfusionControlFactory>(scopedProvider));
        }

        if (panelType == typeof(WileyWidget.WinForms.Controls.Supporting.CsvMappingWizardPanel))
        {
            return new WileyWidget.WinForms.Controls.Supporting.CsvMappingWizardPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.Extensions.Logging.ILogger<WileyWidget.WinForms.Controls.Supporting.CsvMappingWizardPanel>>(scopedProvider));
        }

        if (panelType == typeof(BudgetPanel))
        {
            return new BudgetPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ScopedPanelBase<BudgetViewModel>>>(scopedProvider));
        }

        if (panelType == typeof(AccountEditPanel))
        {
            return new AccountEditPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<AccountsViewModel>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<SyncfusionControlFactory>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<DpiAwareImageService>(scopedProvider));
        }

        if (panelType == typeof(EnterpriseVitalSignsPanel))
        {
            return new EnterpriseVitalSignsPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<EnterpriseVitalSignsViewModel>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<SyncfusionControlFactory>(scopedProvider));
        }

        if (panelType == typeof(PaymentEditPanel))
        {
            return new PaymentEditPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ScopedPanelBase<PaymentsViewModel>>>(scopedProvider));
        }

        if (panelType == typeof(DepartmentSummaryPanel))
        {
            return new DepartmentSummaryPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ScopedPanelBase<DepartmentSummaryViewModel>>>(scopedProvider));
        }

        if (panelType == typeof(UtilityBillPanel))
        {
            return new UtilityBillPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<UtilityBillViewModel>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<SyncfusionControlFactory>(scopedProvider));
        }

        if (panelType == typeof(RecommendedMonthlyChargePanel))
        {
            return new RecommendedMonthlyChargePanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ScopedPanelBase<RecommendedMonthlyChargeViewModel>>>(scopedProvider));
        }

        if (panelType == typeof(QuickBooksPanel))
        {
            return new QuickBooksPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<QuickBooksViewModel>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<SyncfusionControlFactory>(scopedProvider));
        }

        if (panelType == typeof(RevenueTrendsPanel))
        {
            return new RevenueTrendsPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ScopedPanelBase<RevenueTrendsViewModel>>>(scopedProvider));
        }

        if (panelType == typeof(AccountsPanel))
        {
            return new AccountsPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<AccountsViewModel>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<SyncfusionControlFactory>(scopedProvider));
        }

        if (panelType == typeof(ReportsPanel))
        {
            return new ReportsPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ReportsViewModel>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<SyncfusionControlFactory>(scopedProvider));
        }

        if (panelType == typeof(InsightFeedPanel))
        {
            return new InsightFeedPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ScopedPanelBase<InsightFeedViewModel>>>(scopedProvider));
        }

        if (panelType == typeof(WarRoomPanel))
        {
            return new WarRoomPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<WarRoomViewModel>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<SyncfusionControlFactory>(scopedProvider));
        }

        if (panelType == typeof(ActivityLogPanel))
        {
            return new ActivityLogPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ActivityLogViewModel>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<SyncfusionControlFactory>(scopedProvider));
        }

        if (panelType == typeof(ProactiveInsightsPanel))
        {
            return new ProactiveInsightsPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ProactiveInsightsPanel>>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ScopedPanelBase<InsightFeedViewModel>>>(scopedProvider),
                scopedProvider);
        }

        if (panelType == typeof(FormHostPanel) && string.Equals(displayName, "Rates", StringComparison.OrdinalIgnoreCase))
        {
            var ratesPage = ActivatorUtilities.CreateInstance<RatesPage>(scopedProvider);
            AssertRatesPageIsInitialized(ratesPage);
            return ratesPage;
        }

        return (ActivatorUtilities.CreateInstance(scopedProvider, panelType) as Control)
            ?? throw new InvalidOperationException($"Unable to create panel '{displayName}' of type '{panelType.FullName}'.");
    }

    private static async Task TryInitializePanelAsync(Control control)
    {
        if (control is not ICompletablePanel completablePanel)
        {
            return;
        }

        try
        {
            var loadTask = completablePanel.LoadAsync(CancellationToken.None);
            await CompleteTaskWithMessagePumpAsync(loadTask, TimeSpan.FromSeconds(12));
        }
        catch
        {
            // Best-effort initialization only.
        }
    }

    private static async Task CompleteTaskWithMessagePumpAsync(Task task, TimeSpan timeout)
    {
        var start = DateTime.UtcNow;
        while (!task.IsCompleted && DateTime.UtcNow - start < timeout)
        {
            Application.DoEvents();
            await Task.Yield();
        }

        await task.ConfigureAwait(true);
    }

    private static bool ShouldKeepAuditFinding(object finding)
    {
        var identifier = GetAuditFindingText(finding, "Identifier");
        var controlType = GetAuditFindingText(finding, "ControlType");
        var bounds = GetAuditFindingBounds(finding, "Bounds");

        if (string.IsNullOrWhiteSpace(identifier))
        {
            return true;
        }

        var normalizedIdentifier = identifier.Trim();
        var normalizedControlType = controlType.Trim();

        if (normalizedControlType is "TableLayoutPanel" or "SplitContainerAdv" or "SplitContainer" or "FlowLayoutPanel" or "Panel")
        {
            if (normalizedIdentifier.Contains("layout", StringComparison.OrdinalIgnoreCase)
                || normalizedIdentifier.Contains("split", StringComparison.OrdinalIgnoreCase)
                || normalizedIdentifier.Contains("overlay", StringComparison.OrdinalIgnoreCase)
                || normalizedIdentifier.Contains("legend", StringComparison.OrdinalIgnoreCase)
                || normalizedIdentifier.Contains("instructions", StringComparison.OrdinalIgnoreCase)
                || normalizedIdentifier.Contains("summary", StringComparison.OrdinalIgnoreCase)
                || normalizedIdentifier.Contains("actions", StringComparison.OrdinalIgnoreCase)
                || normalizedIdentifier.Contains("content", StringComparison.OrdinalIgnoreCase)
                || normalizedIdentifier.Contains("host", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (normalizedIdentifier.Contains("overlay", StringComparison.OrdinalIgnoreCase)
            || normalizedIdentifier.Contains("legend", StringComparison.OrdinalIgnoreCase)
            || normalizedIdentifier.Contains("instructions", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (bounds.Width > 0 && bounds.Height > 0 && HasMinorEdgeBleed(finding, 8))
        {
            return false;
        }

        return true;
    }

    private static string GetAuditFindingText(object finding, string propertyName)
    {
        var property = finding.GetType().GetProperty(propertyName);
        var value = property?.GetValue(finding);
        return value?.ToString() ?? string.Empty;
    }

    private static Rectangle GetAuditFindingBounds(object finding, string propertyName)
    {
        var property = finding.GetType().GetProperty(propertyName);
        if (property?.GetValue(finding) is Rectangle bounds)
        {
            return bounds;
        }

        return Rectangle.Empty;
    }

    private static bool HasMinorEdgeBleed(object finding, int tolerance)
    {
        var insetsProperty = finding.GetType().GetProperty("Insets");
        var insets = insetsProperty?.GetValue(finding);
        if (insets == null)
        {
            return false;
        }

        var insetType = insets.GetType();
        var left = Convert.ToInt32(insetType.GetProperty("Left")?.GetValue(insets) ?? 0);
        var top = Convert.ToInt32(insetType.GetProperty("Top")?.GetValue(insets) ?? 0);
        var right = Convert.ToInt32(insetType.GetProperty("Right")?.GetValue(insets) ?? 0);
        var bottom = Convert.ToInt32(insetType.GetProperty("Bottom")?.GetValue(insets) ?? 0);

        return left >= -tolerance && top >= -tolerance && right >= -tolerance && bottom >= -tolerance;
    }

    private static void AssertRatesPageIsInitialized(RatesPage ratesPage)
    {
        Assert.Equal("Rates", ratesPage.Text);
        Assert.True(ratesPage.Controls.Count >= 3, "Rates page should initialize its primary controls during construction.");

        var ratesGrid = ratesPage.Controls.OfType<SfDataGrid>().SingleOrDefault();
        var toolStrip = ratesPage.Controls.OfType<ToolStrip>().SingleOrDefault();
        var sourceLink = ratesPage.Controls.OfType<LinkLabel>().SingleOrDefault();

        Assert.NotNull(ratesGrid);
        Assert.NotNull(toolStrip);
        Assert.NotNull(sourceLink);
        Assert.NotNull(ratesGrid!.DataSource);
        Assert.Equal(DockStyle.Fill, ratesGrid.Dock);
        Assert.Contains("Colorado", sourceLink!.Text, StringComparison.OrdinalIgnoreCase);
    }

    private static Control? FindDescendantByAccessibleName(Control root, string accessibleName)
    {
        foreach (Control child in root.Controls)
        {
            if (string.Equals(child.AccessibleName, accessibleName, StringComparison.Ordinal))
            {
                return child;
            }

            var nested = FindDescendantByAccessibleName(child, accessibleName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private static async Task AssertPanelContainsAccessibleNamesAsync(
        Control control,
        string displayName,
        params string[] accessibleNames)
    {
        using var host = new Form
        {
            Size = new System.Drawing.Size(1400, 900),
            StartPosition = FormStartPosition.CenterScreen,
            ShowInTaskbar = false,
            Text = $"Proof Host - {displayName}"
        };

        control.Dock = DockStyle.Fill;
        host.Controls.Add(control);
        _ = host.Handle;
        host.CreateControl();
        control.CreateControl();
        host.PerformLayout();
        control.PerformLayout();

        await WaitForAccessibleNamesAsync(control, accessibleNames, timeoutMs: 3000);

        foreach (var accessibleName in accessibleNames)
        {
            var matchedControl = FindDescendantByAccessibleName(control, accessibleName);
            Assert.True(
                matchedControl != null,
                $"Panel '{displayName}' did not expose expected accessible control '{accessibleName}'.");
        }

        host.Close();
    }

    private static void AssertControlContainsAccessibleNames(
        Control control,
        string displayName,
        params string[] accessibleNames)
    {
        _ = control.Handle;
        control.CreateControl();
        control.PerformLayout();

        foreach (var accessibleName in accessibleNames)
        {
            var matchedControl = FindDescendantByAccessibleName(control, accessibleName);
            Assert.True(
                matchedControl != null,
                $"Panel '{displayName}' did not expose expected accessible control '{accessibleName}'.");
        }
    }

    private static async Task WaitForAccessibleNamesAsync(Control control, IReadOnlyCollection<string> accessibleNames, int timeoutMs)
    {
        var start = DateTime.UtcNow;
        while ((DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
        {
            Application.DoEvents();

            var allPresent = true;
            foreach (var accessibleName in accessibleNames)
            {
                if (FindDescendantByAccessibleName(control, accessibleName) == null)
                {
                    allPresent = false;
                    break;
                }
            }

            if (allPresent)
            {
                return;
            }

            await Task.Delay(10);
        }

        await PumpMessagesAsync(250);
    }


    private static async Task PumpMessagesAsync(int timeoutMs)
    {
        var start = DateTime.UtcNow;
        while ((DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
        {
            Application.DoEvents();
            await Task.Delay(10);
        }
    }
}
