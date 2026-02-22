using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using FluentAssertions;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Tests.Infrastructure;
using WileyWidget.WinForms.Tests.Integration;
using Xunit;

namespace WileyWidget.WinForms.Tests.Integration.Forms;

[Collection("IntegrationTests")]
public sealed class MainFormRibbonNavigationIntegrationTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [WinFormsFact]
    public void RibbonNavigationButtons_Click_ActivatesExpectedPanel_ForAllNavTargets()
    {
        TestThemeHelper.EnsureOffice2019Colorful();

        using var provider = IntegrationTestServices.BuildProvider(new Dictionary<string, string?>
        {
            ["UI:IsUiTestHarness"] = "true",
            ["UI:ShowRibbon"] = "true",
            ["UI:ShowStatusBar"] = "false",
            ["UI:AutoShowDashboard"] = "false",
            ["UI:UseSyncfusionDocking"] = "false",
            ["UI:MinimalMode"] = "false"
        });

        using var form = IntegrationTestServices.CreateMainForm(provider);
        _ = form.Handle;
        form.Show();
        form.InvokeInitializeChrome();
        PumpMessages(8);

        var ribbon = GetPrivateField<RibbonControlAdv>(form, "_ribbon");
        ribbon.Should().NotBeNull("Ribbon must be initialized before navigation button validation");

        EnsurePanelNavigatorInitialized(form);
        var navigator = form.PanelNavigator;
        navigator.Should().NotBeNull("PanelNavigator must be available for click-path navigation validation");

        var navButtons = GetRibbonNavigationButtons(form, ribbon!)
            .Where(button => button.Enabled)
            .ToList();

        navButtons.Should().NotBeEmpty("Expected ribbon navigation buttons with Nav:* tags");

        var registryTargets = PanelRegistry.Panels
            .Select(panel => panel.DisplayName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var clickFailures = new List<string>();

        foreach (var button in navButtons)
        {
            var target = GetNavigationTarget(button);
            target.Should().NotBeNullOrWhiteSpace($"Button '{button.Name}' should have a Nav:* target");

            if (!registryTargets.Contains(target!))
            {
                clickFailures.Add($"Button '{button.Name}' points to unknown target '{target}'.");
                continue;
            }

            var activationObserved = false;
            EventHandler<PanelActivatedEventArgs> handler = (_, args) =>
            {
                if (MatchesTarget(args.PanelName, target!))
                {
                    activationObserved = true;
                }
            };

            navigator!.PanelActivated += handler;
            try
            {
                button.PerformClick();
                PumpMessages(10);
            }
            finally
            {
                navigator.PanelActivated -= handler;
            }

            var activePanel = navigator.GetActivePanelName();
            if (!MatchesTarget(activePanel, target!))
            {
                clickFailures.Add($"Button '{button.Name}' target '{target}' did not activate expected panel (actual='{activePanel ?? "<null>"}').");
            }

            if (!activationObserved)
            {
                clickFailures.Add($"Button '{button.Name}' target '{target}' did not raise matching PanelActivated event.");
            }
        }

        clickFailures.Should().BeEmpty("every ribbon nav button should click through to panel activation");
    }

    [WinFormsFact]
    public void RibbonNavigationButtons_Clicks_UpdateActivePanelState()
    {
        TestThemeHelper.EnsureOffice2019Colorful();

        using var provider = IntegrationTestServices.BuildProvider(new Dictionary<string, string?>
        {
            ["UI:IsUiTestHarness"] = "true",
            ["UI:ShowRibbon"] = "true",
            ["UI:ShowStatusBar"] = "false",
            ["UI:AutoShowDashboard"] = "false",
            ["UI:UseSyncfusionDocking"] = "false"
        });

        using var form = IntegrationTestServices.CreateMainForm(provider);
        _ = form.Handle;
        form.Show();
        form.InvokeInitializeChrome();
        PumpMessages(8);

        var ribbon = GetPrivateField<RibbonControlAdv>(form, "_ribbon");
        ribbon.Should().NotBeNull();

        var distinctTargets = GetRibbonNavigationButtons(form, ribbon!)
            .Select(button => new { Button = button, Target = GetNavigationTarget(button) })
            .Where(item => !string.IsNullOrWhiteSpace(item.Target))
            .GroupBy(item => item.Target!, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Take(2)
            .ToList();

        distinctTargets.Count.Should().BeGreaterOrEqualTo(2, "Need at least two ribbon navigation targets to validate history state");

        distinctTargets[0].Button.PerformClick();
        PumpMessages(8);
        distinctTargets[1].Button.PerformClick();
        PumpMessages(8);

        var navigator = form.PanelNavigator;
        navigator.Should().NotBeNull();
        MatchesTarget(navigator!.GetActivePanelName(), distinctTargets[1].Target!).Should().BeTrue();
    }

    private static IEnumerable<ToolStripButton> GetRibbonNavigationButtons(MainForm form, RibbonControlAdv ribbon)
    {
        var findMethod = typeof(MainForm).GetMethod(
            "FindToolStripItems",
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(RibbonControlAdv), typeof(Func<ToolStripItem, bool>) },
            modifiers: null);

        findMethod.Should().NotBeNull("MainForm should expose internal ToolStrip search helper");

        var results = findMethod!.Invoke(form, new object[]
        {
            ribbon,
            new Func<ToolStripItem, bool>(item =>
                item.Tag is string tag &&
                tag.StartsWith("Nav:", StringComparison.OrdinalIgnoreCase))
        }) as IEnumerable<ToolStripItem>;

        return (results ?? Enumerable.Empty<ToolStripItem>())
            .OfType<ToolStripButton>()
            .GroupBy(button => button.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private static void EnsurePanelNavigatorInitialized(MainForm form)
    {
        var ensureMethod = typeof(MainForm).GetMethod("EnsurePanelNavigatorInitialized", BindingFlags.Instance | BindingFlags.NonPublic);
        ensureMethod.Should().NotBeNull();
        ensureMethod!.Invoke(form, null);
    }

    private static string? GetNavigationTarget(ToolStripItem item)
    {
        if (item.Tag is not string tag || !tag.StartsWith("Nav:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return tag[4..].Trim();
    }

    private static bool MatchesTarget(string? actualPanelName, string expectedTarget)
    {
        if (string.IsNullOrWhiteSpace(actualPanelName) || string.IsNullOrWhiteSpace(expectedTarget))
        {
            return false;
        }

        if (string.Equals(actualPanelName, expectedTarget, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var normalizedActual = actualPanelName.Replace(" ", string.Empty, StringComparison.Ordinal);
        var normalizedExpected = expectedTarget.Replace(" ", string.Empty, StringComparison.Ordinal);
        return string.Equals(normalizedActual, normalizedExpected, StringComparison.OrdinalIgnoreCase);
    }

    private static T? GetPrivateField<T>(object target, string fieldName) where T : class
    {
        return target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(target) as T;
    }

    private static void PumpMessages(int iterations)
    {
        for (var index = 0; index < iterations; index++)
        {
            Application.DoEvents();
        }
    }
}
