using System;
using System.Linq;
using System.Windows.Forms;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Tests.Integration;
using WileyWidget.WinForms.Tests.Infrastructure;
using Xunit;

namespace WileyWidget.WinForms.Tests.Integration.Forms;

[Trait("Category", "Integration")]
[Collection("SyncfusionTheme")]
public sealed class DockingIntegrationTests
{
    [StaFact]
    public void DockingHostFactory_CreatesAllPanels()
    {
        // Force headless mode to prevent UI hangs
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");

        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var form = IntegrationTestServices.CreateMainForm(provider);
        var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
        var panelNavigator = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IPanelNavigationService>(provider);

        var (dockingManager, leftPanel, rightPanel, centralPanel, activityLogPanel, activityRefreshTimer, layoutManager) =
            DockingHostFactory.CreateDockingHost(form, provider, panelNavigator, form, logger);

        // Validate all components are created
        dockingManager.Should().NotBeNull();
        leftPanel.Should().NotBeNull();
        rightPanel.Should().NotBeNull();
        centralPanel.Should().NotBeNull();
        activityLogPanel.Should().BeNull(); // By design, reserved for future
        activityRefreshTimer.Should().NotBeNull();
        layoutManager.Should().NotBeNull();

        // Validate docking manager is attached to form
        dockingManager.HostForm.Should().Be(form);
        dockingManager.HostControl.Should().Be(form);

        // Validate panels are properly docked
        leftPanel!.Dock.Should().Be(DockStyle.Left);
        rightPanel!.Dock.Should().Be(DockStyle.Right);
        centralPanel!.Dock.Should().Be(DockStyle.Fill);
    }

    [StaFact]
    public async Task DockingLayoutManager_SavesAndLoadsLayout()
    {
        // Force headless mode to prevent UI hangs
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");

        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var form = IntegrationTestServices.CreateMainForm(provider);
        var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
        var panelNavigator = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IPanelNavigationService>(provider);

        var (dockingManager, leftPanel, rightPanel, centralPanel, _, _, layoutManager) =
            DockingHostFactory.CreateDockingHost(form, provider, panelNavigator, form, logger);

        ArgumentNullException.ThrowIfNull(layoutManager);

        // Modify panel sizes to test persistence
        leftPanel!.Width = 250;
        rightPanel!.Width = 350;
        centralPanel!.Height = 600;

        // Save layout
        layoutManager.SaveDockingLayout(dockingManager);

        // Modify again
        leftPanel!.Width = 300;
        rightPanel!.Width = 400;

        // Load layout
        await layoutManager.LoadDockingLayoutAsync(dockingManager);

        // Verify layout was restored (sizes should be approximately restored)
        leftPanel!.Width.Should().BeInRange(240, 260); // Allow some tolerance
        rightPanel!.Width.Should().BeInRange(340, 360);
    }

    [StaFact]
    public void DockingHostFactory_AppliesThemesCorrectly()
    {
        // Force headless mode to prevent UI hangs
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");

        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var form = IntegrationTestServices.CreateMainForm(provider);
        var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
        var panelNavigator = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IPanelNavigationService>(provider);

        var (dockingManager, leftPanel, rightPanel, centralPanel, _, _, _) =
            DockingHostFactory.CreateDockingHost(form, provider, panelNavigator, form, logger);

        // Verify theme application
        leftPanel!.ThemeName.Should().Be("Office2019Colorful");
        rightPanel!.ThemeName.Should().Be("Office2019Colorful");
        centralPanel!.ThemeName.Should().Be("Office2019Colorful");
    }

    [StaFact]
    public async Task DockingLayoutManager_HandlesPanelStateChanges()
    {
        // Force headless mode to prevent UI hangs
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");

        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var form = IntegrationTestServices.CreateMainForm(provider);
        var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
        var panelNavigator = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IPanelNavigationService>(provider);

        var (dockingManager, leftPanel, _, _, _, _, layoutManager) =
            DockingHostFactory.CreateDockingHost(form, provider, panelNavigator, form, logger);

        ArgumentNullException.ThrowIfNull(layoutManager);

        // Test panel visibility changes
        leftPanel!.Visible = false;
        layoutManager.SaveDockingLayout(dockingManager);

        leftPanel.Visible = true;
        await layoutManager.LoadDockingLayoutAsync(dockingManager);

        // Panel should be restored to previous state
        leftPanel.Visible.Should().BeFalse();
    }
}
