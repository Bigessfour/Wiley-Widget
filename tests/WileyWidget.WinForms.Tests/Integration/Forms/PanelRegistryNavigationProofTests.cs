using System;
using System.Collections.Generic;
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

        if (panelType == typeof(AuditLogPanel))
        {
            using var auditPanel = new AuditLogPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<AuditLogViewModel>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<SyncfusionControlFactory>(scopedProvider));

            Assert.NotNull(FindDescendantByAccessibleName(auditPanel, "Audit log entries grid"));
            Assert.NotNull(FindDescendantByAccessibleName(auditPanel, "Audit log filters"));
            Assert.NotNull(FindDescendantByAccessibleName(auditPanel, "Audit events chart"));
            return;
        }

        if (panelType == typeof(PaymentsPanel))
        {
            using var paymentsPanel = new PaymentsPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PaymentsPanel>>(scopedProvider));

            Assert.NotNull(FindDescendantByAccessibleName(paymentsPanel, "Payments Grid"));
            Assert.NotNull(FindDescendantByAccessibleName(paymentsPanel, "Add Payment"));
            return;
        }

        if (panelType == typeof(CustomersPanel))
        {
            using var customersPanel = new CustomersPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ScopedPanelBase<CustomersViewModel>>>(scopedProvider));

            Assert.NotNull(customersPanel.ViewModel);
            Assert.False(customersPanel.IsDisposed, "CustomersPanel should remain alive after proof construction.");
            return;
        }

        if (panelType == typeof(AnalyticsHubPanel))
        {
            using var analyticsHubPanel = new AnalyticsHubPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ScopedPanelBase<AnalyticsHubViewModel>>>(scopedProvider));

            Assert.NotNull(FindDescendantByAccessibleName(analyticsHubPanel, "Analytics fiscal year selector"));
            Assert.NotNull(FindDescendantByAccessibleName(analyticsHubPanel, "Analytics search"));
            Assert.NotNull(FindDescendantByAccessibleName(analyticsHubPanel, "Analytics sections"));
            return;
        }

        if (panelType == typeof(SettingsPanel))
        {
            using var settingsPanel = new SettingsPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<SettingsViewModel>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<SyncfusionControlFactory>(scopedProvider));

            Assert.NotNull(FindDescendantByAccessibleName(settingsPanel, "Application Title"));
            Assert.NotNull(FindDescendantByAccessibleName(settingsPanel, "Open edit forms docked"));
            Assert.NotNull(FindDescendantByAccessibleName(settingsPanel, "Export path"));
            return;
        }

        if (panelType == typeof(WileyWidget.WinForms.Controls.Supporting.CsvMappingWizardPanel))
        {
            using var csvMappingWizardPanel = new WileyWidget.WinForms.Controls.Supporting.CsvMappingWizardPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.Extensions.Logging.ILogger<WileyWidget.WinForms.Controls.Supporting.CsvMappingWizardPanel>>(scopedProvider));

            Assert.NotNull(FindDescendantByAccessibleName(csvMappingWizardPanel, "CSV Preview"));
            Assert.NotNull(FindDescendantByAccessibleName(csvMappingWizardPanel, "Has Header"));
            return;
        }

        if (panelType == typeof(BudgetPanel))
        {
            using var budgetPanel = new BudgetPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ScopedPanelBase<BudgetViewModel>>>(scopedProvider));

            Assert.True(budgetPanel.Controls.Count > 0, "BudgetPanel should initialize its root control tree during proof construction.");
            Assert.False(budgetPanel.IsDisposed, "BudgetPanel should remain alive after proof construction.");
            return;
        }

        if (panelType == typeof(AccountEditPanel))
        {
            using var accountEditPanel = new AccountEditPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<AccountsViewModel>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<SyncfusionControlFactory>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<DpiAwareImageService>(scopedProvider));

            Assert.NotNull(accountEditPanel.ViewModel);
            Assert.NotNull(FindDescendantByAccessibleName(accountEditPanel, "Account Number"));
            Assert.NotNull(FindDescendantByAccessibleName(accountEditPanel, "Account Name"));
            Assert.NotNull(FindDescendantByAccessibleName(accountEditPanel, "Cancel"));
            return;
        }

        if (panelType == typeof(EnterpriseVitalSignsPanel))
        {
            using var enterpriseVitalSignsPanel = new EnterpriseVitalSignsPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<EnterpriseVitalSignsViewModel>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<SyncfusionControlFactory>(scopedProvider));

            Assert.NotNull(FindDescendantByAccessibleName(enterpriseVitalSignsPanel, "Enterprise vital signs header"));
            Assert.NotNull(FindDescendantByAccessibleName(enterpriseVitalSignsPanel, "Enterprise gauges"));
            Assert.NotNull(FindDescendantByAccessibleName(enterpriseVitalSignsPanel, "Enterprise vital signs status"));
            return;
        }

        if (panelType == typeof(PaymentEditPanel))
        {
            using var paymentEditPanel = new PaymentEditPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ScopedPanelBase<PaymentsViewModel>>>(scopedProvider));

            Assert.NotNull(FindDescendantByAccessibleName(paymentEditPanel, "Add New Vendor"));
            Assert.NotNull(FindDescendantByAccessibleName(paymentEditPanel, "Save Changes"));
            Assert.NotNull(FindDescendantByAccessibleName(paymentEditPanel, "Cancel Edit"));
            return;
        }

        if (panelType == typeof(DepartmentSummaryPanel))
        {
            using var departmentSummaryPanel = new DepartmentSummaryPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ScopedPanelBase<DepartmentSummaryViewModel>>>(scopedProvider));

            Assert.NotNull(FindDescendantByAccessibleName(departmentSummaryPanel, "Department Summary header"));
            Assert.NotNull(FindDescendantByAccessibleName(departmentSummaryPanel, "Summary cards"));
            Assert.NotNull(FindDescendantByAccessibleName(departmentSummaryPanel, "Department metrics grid"));
            return;
        }

        if (panelType == typeof(UtilityBillPanel))
        {
            using var utilityBillPanel = new UtilityBillPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<UtilityBillViewModel>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<SyncfusionControlFactory>(scopedProvider));

            Assert.False(utilityBillPanel.IsDisposed, "UtilityBillPanel should remain alive after proof construction.");
            return;
        }

        if (panelType == typeof(RecommendedMonthlyChargePanel))
        {
            using var recommendedMonthlyChargePanel = new RecommendedMonthlyChargePanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ScopedPanelBase<RecommendedMonthlyChargeViewModel>>>(scopedProvider));

            Assert.True(recommendedMonthlyChargePanel.Controls.Count > 0, $"Panel '{displayName}' should create its root control tree during proof construction.");
            Assert.NotNull(FindDescendantByAccessibleName(recommendedMonthlyChargePanel, "Refresh Data"));
            Assert.NotNull(FindDescendantByAccessibleName(recommendedMonthlyChargePanel, "Department Rates Grid"));
            Assert.NotNull(FindDescendantByAccessibleName(recommendedMonthlyChargePanel, "Benchmarks Grid"));
            return;
        }

        if (panelType == typeof(QuickBooksPanel))
        {
            using var quickBooksPanel = new QuickBooksPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<QuickBooksViewModel>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<SyncfusionControlFactory>(scopedProvider));

            Assert.True(quickBooksPanel.Controls.Count > 0, $"Panel '{displayName}' should create its root control tree during proof construction.");
            Assert.NotNull(FindDescendantByAccessibleName(quickBooksPanel, "QuickBooks Panel Header"));
            Assert.NotNull(FindDescendantByAccessibleName(quickBooksPanel, "Connect to QuickBooks"));
            Assert.NotNull(FindDescendantByAccessibleName(quickBooksPanel, "Sync History Grid"));
            return;
        }

        if (panelType == typeof(RevenueTrendsPanel))
        {
            using var revenueTrendsPanel = new RevenueTrendsPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ScopedPanelBase<RevenueTrendsViewModel>>>(scopedProvider));

            Assert.True(revenueTrendsPanel.Controls.Count > 0, $"Panel '{displayName}' should create its root control tree during proof construction.");
            Assert.NotNull(FindDescendantByAccessibleName(revenueTrendsPanel, "Revenue Trends panel header"));
            Assert.NotNull(FindDescendantByAccessibleName(revenueTrendsPanel, "Revenue trends line chart"));
            Assert.NotNull(FindDescendantByAccessibleName(revenueTrendsPanel, "Monthly revenue breakdown data grid"));
            return;
        }

        if (panelType == typeof(AccountsPanel))
        {
            using var accountsPanel = new AccountsPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<AccountsViewModel>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<SyncfusionControlFactory>(scopedProvider));

            Assert.True(accountsPanel.Controls.Count > 0, $"Panel '{displayName}' should create its root control tree during proof construction.");
            Assert.NotNull(FindDescendantByAccessibleName(accountsPanel, "Chart of Accounts Panel Header"));
            Assert.NotNull(FindDescendantByAccessibleName(accountsPanel, "New Account"));
            Assert.NotNull(FindDescendantByAccessibleName(accountsPanel, "Accounts Grid"));
            return;
        }

        if (panelType == typeof(ReportsPanel))
        {
            using var reportsPanel = new ReportsPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ReportsViewModel>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<SyncfusionControlFactory>(scopedProvider));

            Assert.True(reportsPanel.Controls.Count > 0, $"Panel '{displayName}' should create its root control tree during proof construction.");
            Assert.NotNull(FindDescendantByAccessibleName(reportsPanel, "Report Selector"));
            Assert.NotNull(FindDescendantByAccessibleName(reportsPanel, "Generate Report"));
            Assert.NotNull(FindDescendantByAccessibleName(reportsPanel, "Report Preview Container"));
            return;
        }

        if (panelType == typeof(InsightFeedPanel))
        {
            using var insightFeedPanel = new InsightFeedPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ScopedPanelBase<InsightFeedViewModel>>>(scopedProvider));

            Assert.True(insightFeedPanel.Controls.Count > 0, $"Panel '{displayName}' should create its root control tree during proof construction.");
            Assert.NotNull(FindDescendantByAccessibleName(insightFeedPanel, "Insights Header"));
            Assert.NotNull(FindDescendantByAccessibleName(insightFeedPanel, "Insight Feed Toolbar"));
            Assert.NotNull(FindDescendantByAccessibleName(insightFeedPanel, "Insights Data Grid"));
            return;
        }

        if (panelType == typeof(WarRoomPanel))
        {
            using var warRoomPanel = new WarRoomPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<WarRoomViewModel>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<SyncfusionControlFactory>(scopedProvider));

            Assert.True(warRoomPanel.Controls.Count > 0, $"Panel '{displayName}' should create its root control tree during proof construction.");
            Assert.NotNull(FindDescendantByAccessibleName(warRoomPanel, "Scenario Input"));
            Assert.NotNull(FindDescendantByAccessibleName(warRoomPanel, "Run Scenario"));
            Assert.NotNull(FindDescendantByAccessibleName(warRoomPanel, "War Room Status"));
            return;
        }

        if (panelType == typeof(ActivityLogPanel))
        {
            using var activityLogPanel = new ActivityLogPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ActivityLogViewModel>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<SyncfusionControlFactory>(scopedProvider));

            Assert.True(activityLogPanel.Controls.Count > 0, $"Panel '{displayName}' should create its root control tree during proof construction.");
            Assert.NotNull(FindDescendantByAccessibleName(activityLogPanel, "Activity Log Header"));
            Assert.NotNull(FindDescendantByAccessibleName(activityLogPanel, "Export Activity Log"));
            Assert.NotNull(FindDescendantByAccessibleName(activityLogPanel, "Clear Activity Log"));
            return;
        }

        if (panelType == typeof(ProactiveInsightsPanel))
        {
            using var proactiveInsightsPanel = new ProactiveInsightsPanel(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ProactiveInsightsPanel>>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(scopedProvider),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ScopedPanelBase<InsightFeedViewModel>>>(scopedProvider),
                scopedProvider);

            Assert.True(proactiveInsightsPanel.Controls.Count > 0, $"Panel '{displayName}' should create its root control tree during proof construction.");
            Assert.NotNull(FindDescendantByAccessibleName(proactiveInsightsPanel, "Proactive Insights Header"));
            Assert.NotNull(FindDescendantByAccessibleName(proactiveInsightsPanel, "Proactive Actions Toolbar"));
            Assert.NotNull(FindDescendantByAccessibleName(proactiveInsightsPanel, "Proactive insights status"));
            return;
        }

        if (panelType == typeof(FormHostPanel) && string.Equals(displayName, "Rates", StringComparison.OrdinalIgnoreCase))
        {
            using var ratesPage = ActivatorUtilities.CreateInstance<RatesPage>(scopedProvider);

            AssertRatesPageIsInitialized(ratesPage);
            return;
        }
        var control = ActivatorUtilities.CreateInstance(scopedProvider, panelType) as Control;
        Assert.NotNull(control);

        using var host = new Form
        {
            Size = new System.Drawing.Size(1400, 900),
            StartPosition = FormStartPosition.CenterScreen,
            ShowInTaskbar = false,
            Text = $"Proof Host - {displayName}"
        };

        control!.Dock = DockStyle.Fill;
        host.Controls.Add(control);
        _ = host.Handle;
        host.CreateControl();
        control.CreateControl();
        host.PerformLayout();
        control.PerformLayout();

        await PumpMessagesAsync(250);

        Assert.True(control.IsHandleCreated, $"Panel '{displayName}' should create a WinForms handle inside the proof host.");
        Assert.False(control.IsDisposed, $"Panel '{displayName}' should not be disposed immediately after rendering.");
        Assert.Same(host, control.FindForm());

        host.Close();
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
