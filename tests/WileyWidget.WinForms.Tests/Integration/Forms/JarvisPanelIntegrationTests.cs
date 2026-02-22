using System;
using System.Linq;
using System.Windows.Forms;
using FluentAssertions;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Tests.Integration;
using WileyWidget.WinForms.Tests.Infrastructure;
using Xunit;

namespace WileyWidget.WinForms.Tests.Integration.Forms;

[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
public sealed class JarvisPanelIntegrationTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    // NOTE: These tests are obsolete. RightDockPanelFactory was refactored to remove JARVIS chat.
    // JARVIS is managed through DockingManager panel navigation.
    // These tests need to be rewritten to test the new architecture.

    [StaFact(Skip = "Obsolete: JARVIS is now a separate fixed sidebar, not part of right dock panel")]
    public void RightDockPanel_ContainsJarvisChatControl()
    {
        // Force headless mode to prevent BlazorWebView initialization hangs
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");

        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var form = IntegrationTestServices.CreateMainForm(provider);
        var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);

        var (rightDockPanel, _) = RightDockPanelFactory.CreateRightDockPanel(form, provider, logger);

        var jarvisControl = FindControl<JARVISChatUserControl>(rightDockPanel);
        jarvisControl.Should().NotBeNull();
        if (IsHeadlessTestMode())
        {
            jarvisControl!.Controls.OfType<Label>().Any(lbl => lbl.Name == "JARVISChatPlaceholder").Should().BeTrue();
        }
        else
        {
            jarvisControl!.Controls.OfType<BlazorWebView>().Any(view => view.Name == "JARVISChatBlazorView").Should().BeTrue();
        }
    }

    [StaFact(Skip = "Obsolete: JARVIS is now a separate fixed sidebar, not part of right dock panel")]
    public void SwitchRightPanelContent_SelectsJarvisTab()
    {
        // Force headless mode to prevent BlazorWebView initialization hangs
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");

        using var provider = IntegrationTestServices.BuildProvider();
        using var form = IntegrationTestServices.CreateMainForm(provider);
        var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);

        var (rightDockPanel, _) = RightDockPanelFactory.CreateRightDockPanel(form, provider, logger);

        // RightDockPanelFactory.SwitchRightPanelContent(rightDockPanel, RightDockPanelFactory.RightPanelMode.JarvisChat, logger);

        var tabControl = rightDockPanel.Controls.OfType<TabControl>().First();
        tabControl.SelectedTab.Should().NotBeNull();
        tabControl.SelectedTab!.Name.Should().Be("JARVISChatTab");
    }

    [StaFact(Skip = "Obsolete: JARVIS is now a separate fixed sidebar, not part of right dock panel")]
    public void JarvisControl_AppliesThemeCorrectly()
    {
        // Force headless mode to prevent BlazorWebView initialization hangs
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");

        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var form = IntegrationTestServices.CreateMainForm(provider);
        var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);

        var (rightDockPanel, _) = RightDockPanelFactory.CreateRightDockPanel(form, provider, logger);

        var jarvisControl = FindControl<JARVISChatUserControl>(rightDockPanel);
        jarvisControl.Should().NotBeNull();

        // The SfSkinManager application theme should already be set by the test helper
        SfSkinManager.ApplicationVisualTheme.Should().Be("Office2019Colorful");
    }

    [StaFact(Skip = "Obsolete: JARVIS is now a separate fixed sidebar, not part of right dock panel")]
    public void RightDockPanel_HasExpectedTabs()
    {
        // Force headless mode to prevent BlazorWebView initialization hangs
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");

        using var provider = IntegrationTestServices.BuildProvider();
        using var form = IntegrationTestServices.CreateMainForm(provider);
        var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);

        var (rightDockPanel, _) = RightDockPanelFactory.CreateRightDockPanel(form, provider, logger);

        var tabControl = rightDockPanel.Controls.OfType<TabControl>().First();
        tabControl.TabPages.Count.Should().Be(2);
        tabControl.TabPages[0].Name.Should().Be("ActivityLogTab");
        tabControl.TabPages[1].Name.Should().Be("JARVISChatTab");
    }

    [StaFact(Skip = "Obsolete: JARVIS is now a separate fixed sidebar, not part of right dock panel")]
    public void SwitchToActivityLog_SelectsActivityTab()
    {
        // Force headless mode to prevent BlazorWebView initialization hangs
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");

        using var provider = IntegrationTestServices.BuildProvider();
        using var form = IntegrationTestServices.CreateMainForm(provider);
        var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);

        var (rightDockPanel, _) = RightDockPanelFactory.CreateRightDockPanel(form, provider, logger);

        // RightDockPanelFactory.SwitchRightPanelContent(rightDockPanel, RightDockPanelFactory.RightPanelMode.ActivityLog, logger);

        var tabControl = rightDockPanel.Controls.OfType<TabControl>().First();
        tabControl.SelectedTab.Should().NotBeNull();
        tabControl.SelectedTab!.Name.Should().Be("ActivityLogTab");
    }

    [StaFact(Skip = "Obsolete: JARVIS is now a separate fixed sidebar, not part of right dock panel")]
    public void JarvisControl_IsProperlyDocked()
    {
        // Force headless mode to prevent BlazorWebView initialization hangs
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");

        using var provider = IntegrationTestServices.BuildProvider();
        using var form = IntegrationTestServices.CreateMainForm(provider);
        var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);

        var (rightDockPanel, _) = RightDockPanelFactory.CreateRightDockPanel(form, provider, logger);

        var jarvisControl = FindControl<JARVISChatUserControl>(rightDockPanel);
        jarvisControl.Should().NotBeNull();
        jarvisControl!.Dock.Should().Be(DockStyle.Fill);
        jarvisControl.Name.Should().Be("JARVISChatUserControl");
    }

    private static TPanel? FindControl<TPanel>(Control root) where TPanel : Control
    {
        if (root is TPanel match)
        {
            return match;
        }

        foreach (Control child in root.Controls)
        {
            var found = FindControl<TPanel>(child);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static bool IsHeadlessTestMode()
    {
        return string.Equals(Environment.GetEnvironmentVariable("WILEYWIDGET_TESTS"), "true", StringComparison.OrdinalIgnoreCase)
               || string.Equals(Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS"), "true", StringComparison.OrdinalIgnoreCase)
               || !Environment.UserInteractive;
    }
}
