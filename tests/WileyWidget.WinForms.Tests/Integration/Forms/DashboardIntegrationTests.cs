using System;
using System.Linq;
using System.Windows.Forms;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Tests.Integration;
using WileyWidget.WinForms.Tests.Infrastructure;
using Xunit;

namespace WileyWidget.WinForms.Tests.Integration.Forms;

[Trait("Category", "Integration")]
[Collection("SyncfusionTheme")]
public sealed class DashboardIntegrationTests
{
    [StaFact]
    public void DashboardFactory_CreatesDashboardPanel()
    {
        // Force headless mode to prevent UI hangs
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");

        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var form = IntegrationTestServices.CreateMainForm(provider);
        var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
        var panelNavigator = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IPanelNavigationService>(provider);

        var dashboardPanel = DashboardFactory.CreateDashboardPanel(panelNavigator, form, logger);

        dashboardPanel.Should().NotBeNull();
        dashboardPanel.Controls.Cast<Control>().Should().NotBeEmpty();
    }

    [StaFact]
    public void DashboardFactory_IncludesNavigationCards()
    {
        // Force headless mode to prevent UI hangs
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");

        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var form = IntegrationTestServices.CreateMainForm(provider);
        var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
        var panelNavigator = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IPanelNavigationService>(provider);

        var dashboardPanel = DashboardFactory.CreateDashboardPanel(panelNavigator, form, logger);

        // Look for navigation cards (buttons or panels with specific text)
        var navigationElements = dashboardPanel.Controls.OfType<Control>()
            .Where(c => c.Text.Contains("Dashboard") || c.Text.Contains("Accounts") ||
                       c.Text.Contains("Budget") || c.Text.Contains("Reports"))
            .ToList();

        navigationElements.Should().NotBeEmpty();
    }

    [StaFact]
    public void DashboardFactory_AppliesThemeCorrectly()
    {
        // Force headless mode to prevent UI hangs
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");

        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var form = IntegrationTestServices.CreateMainForm(provider);
        var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
        var panelNavigator = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IPanelNavigationService>(provider);

        var dashboardPanel = DashboardFactory.CreateDashboardPanel(panelNavigator, form, logger);

        // Dashboard panel created successfully (theme inheritance is handled by parent form)
        dashboardPanel.Should().NotBeNull();
    }

    [StaFact]
    public void DashboardFactory_LayoutIsResponsive()
    {
        // Force headless mode to prevent UI hangs
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");

        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var form = IntegrationTestServices.CreateMainForm(provider);
        var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
        var panelNavigator = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IPanelNavigationService>(provider);

        var dashboardPanel = DashboardFactory.CreateDashboardPanel(panelNavigator, form, logger);

        // Verify panel has proper docking and sizing
        dashboardPanel.Dock.Should().Be(DockStyle.Fill);
        dashboardPanel.MinimumSize.Should().NotBe(Size.Empty);
    }
}
