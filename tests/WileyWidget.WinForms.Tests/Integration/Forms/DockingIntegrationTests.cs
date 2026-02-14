using System;
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

        var (dockingManager, leftPanel, rightPanel, centralPanel, activityLogPanel, activityRefreshTimer, layoutManager) =
            DockingHostFactory.CreateDockingHost(form, provider, null, form, logger);

        // Validate all components are created
        dockingManager.Should().NotBeNull();
        leftPanel.Should().NotBeNull();
        rightPanel.Should().NotBeNull();
        centralPanel.Should().NotBeNull();
        activityLogPanel.Should().NotBeNull();
        activityRefreshTimer.Should().BeNull();
        layoutManager.Should().BeNull();

        // Validate docking manager is attached to form
        dockingManager.HostForm.Should().Be(form);
        dockingManager.HostControl.Should().Be(form);

        // Validate panels are registered for docking
        dockingManager.GetEnableDocking(leftPanel!).Should().BeTrue();
        dockingManager.GetEnableDocking(rightPanel!).Should().BeTrue();
        dockingManager.GetEnableDocking(centralPanel!).Should().BeTrue();
    }

    [StaFact]
    public void DockingHostFactory_DoesNotCreateLegacyLayoutManager()
    {
        // Force headless mode to prevent UI hangs
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");

        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var form = IntegrationTestServices.CreateMainForm(provider);
        var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);

        var (dockingManager, leftPanel, rightPanel, centralPanel, _, _, layoutManager) =
            DockingHostFactory.CreateDockingHost(form, provider, null, form, logger);

        dockingManager.Should().NotBeNull();
        leftPanel.Should().NotBeNull();
        rightPanel.Should().NotBeNull();
        centralPanel.Should().NotBeNull();
        layoutManager.Should().BeNull();
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

        var (dockingManager, leftPanel, rightPanel, centralPanel, _, _, _) =
            DockingHostFactory.CreateDockingHost(form, provider, null, form, logger);

        // Verify theme application at DockingManager level (child controls inherit via SfSkinManager cascade)
        dockingManager.ThemeName.Should().Be("Office2019Colorful");
        leftPanel.Should().NotBeNull();
        rightPanel.Should().NotBeNull();
        centralPanel.Should().NotBeNull();
    }

    [StaFact]
    public void DockingHostFactory_AllowsInteractiveDockingFeatures()
    {
        // Force headless mode to prevent UI hangs
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");

        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var form = IntegrationTestServices.CreateMainForm(provider);
        var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);

        var (dockingManager, leftPanel, _, _, _, _, layoutManager) =
            DockingHostFactory.CreateDockingHost(form, provider, null, form, logger);

        layoutManager.Should().BeNull();
        leftPanel.Should().NotBeNull();

        // interactive mode should keep docking enabled so users can dock/float/auto-hide via DockingManager UI
        dockingManager.GetEnableDocking(leftPanel!).Should().BeTrue();
    }
}
