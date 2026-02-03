using System;
using System.Linq;
using System.Windows.Forms;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Tests.Integration;
using WileyWidget.WinForms.Tests.Infrastructure;
using Xunit;

namespace WileyWidget.WinForms.Tests.Integration.Forms;

[Trait("Category", "Integration")]
[Collection("SyncfusionTheme")]
public sealed class RibbonIntegrationTests
{
    [StaFact]
    public void RibbonFactory_CreatesRibbonWithTabs()
    {
        // Force headless mode to prevent UI hangs
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");

        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var form = IntegrationTestServices.CreateMainForm(provider);
        var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);

        var (ribbon, homeTab) = RibbonFactory.CreateRibbon(form, logger);

        ribbon.Should().NotBeNull();
        ribbon.Header.Should().NotBeNull();
        ribbon.Header.MainItems.OfType<ToolStripTabItem>().Should().Contain(homeTab);
        ribbon.Header.MainItems.Count.Should().BeGreaterThan(0);

        // Verify Home tab exists
        homeTab.Should().NotBeNull();
    }

    [StaFact]
    public void RibbonFactory_AppliesThemeCorrectly()
    {
        // Force headless mode to prevent UI hangs
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");

        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var form = IntegrationTestServices.CreateMainForm(provider);
        var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);

        var (ribbon, _) = RibbonFactory.CreateRibbon(form, logger);

        ribbon.ThemeName.Should().Be("Office2019Colorful");
    }

    [StaFact]
    public void RibbonFactory_IncludesNavigationButtons()
    {
        // Force headless mode to prevent UI hangs
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");

        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var form = IntegrationTestServices.CreateMainForm(provider);
        var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);

        var (ribbon, homeTab) = RibbonFactory.CreateRibbon(form, logger);

        // Find navigation buttons (Dashboard, Accounts, etc.)
        homeTab.Should().NotBeNull();
        var homeTabPanel = homeTab.Panel;
        RibbonPanel navigationPanel = homeTabPanel ?? throw new InvalidOperationException("Ribbon home tab panel was not created while building the ribbon.");

        // Check for navigation buttons in the tab
        var navigationButtons = navigationPanel.Controls
            .OfType<ToolStripEx>()
            .SelectMany(strip => strip.Items.OfType<ToolStripButton>())
            .Where(b =>
            {
                var buttonText = b.Text ?? string.Empty;
                return buttonText.Contains("Dashboard") || buttonText.Contains("Accounts") || buttonText.Contains("Budget");
            })
            .ToList();

        navigationButtons.Should().NotBeEmpty();
    }

    [StaFact]
    public void RibbonFactory_GlobalSearchIntegration()
    {
        // Force headless mode to prevent UI hangs
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");

        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var form = IntegrationTestServices.CreateMainForm(provider);
        var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);

        var (ribbon, homeTab) = RibbonFactory.CreateRibbon(form, logger);

        // Verify global search textbox is integrated
        var searchBox = FindControl<ToolStripTextBox>(ribbon);
        searchBox.Should().NotBeNull();
        searchBox!.ToolTipText.Should().Contain("Search");
        searchBox.Name.Should().Be("GlobalSearch");
        searchBox.AccessibleName.Should().Be("Global Search Box");
    }

    private static TControl? FindControl<TControl>(Control root) where TControl : class
    {
        if (root is TControl match)
        {
            return match;
        }

        foreach (Control child in root.Controls)
        {
            var found = FindControl<TControl>(child);
            if (found != null)
            {
                return found;
            }
        }

        if (root is ToolStrip toolStrip)
        {
            foreach (ToolStripItem item in toolStrip.Items)
            {
                if (item is TControl matchItem)
                {
                    return matchItem;
                }
            }
        }

        return null;
    }
}
