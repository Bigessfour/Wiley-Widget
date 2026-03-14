using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Tests.Infrastructure;
using Xunit;

namespace WileyWidget.WinForms.Tests.Integration.Forms;

public sealed class MainFormPanelNavigationInternalProofTests
{
    [StaFact]
    public void CreatePanelNavigationCommand_ActivatesEveryTownReleasePanel_ThroughShowPanelTypePath()
    {
        TestThemeHelper.EnsureOffice2019Colorful();

        using var provider = IntegrationTestServices.BuildProvider(new Dictionary<string, string?>
        {
            ["UI:IsUiTestHarness"] = "true",
            ["UI:ShowRibbon"] = "true",
            ["UI:ShowStatusBar"] = "false",
            ["UI:AutoShowDashboard"] = "false",
            ["UI:AutoShowPanels"] = "false",
            ["UI:UseSyncfusionDocking"] = "false",
            ["UI:MinimalMode"] = "false"
        });

        var form = CreateOffscreenMainForm(provider, initializeTestChrome: true);

        try
        {
            var createCommandMethod = typeof(MainForm).GetMethod(
                "CreatePanelNavigationCommand",
                BindingFlags.Static | BindingFlags.NonPublic);

            createCommandMethod.Should().NotBeNull("MainForm ribbon navigation must keep exposing the internal command factory used by the ribbon surface");

            var failures = new List<string>();

            foreach (var entry in GetTownReleasePanelEntries())
            {
                try
                {
                    var command = createCommandMethod!.Invoke(null, new object?[] { form, entry, NullLogger.Instance }) as Delegate;
                    if (command == null)
                    {
                        failures.Add($"{entry.DisplayName}: CreatePanelNavigationCommand returned null.");
                        continue;
                    }

                    command.DynamicInvoke();
                    PumpMessages(6);
                }
                catch (Exception ex)
                {
                    var root = ex is TargetInvocationException tie && tie.InnerException != null
                        ? tie.InnerException
                        : ex;

                    failures.Add($"{entry.DisplayName}: CreatePanelNavigationCommand invocation threw {root.GetType().Name} - {root.Message}");
                    continue;
                }

                AssertActivePanel(form, entry, failures, "CreatePanelNavigationCommand");
                AssertPanelHost(form, entry, failures, "CreatePanelNavigationCommand");
            }

            failures.Should().BeEmpty("every town-release panel should remain reachable through the private ribbon command factory and the MainForm.ShowPanel(Type, ...) path it calls internally");
        }
        finally
        {
            DisposeFormAndOwnedScope(form);
        }
    }

    private static IReadOnlyList<PanelRegistry.PanelEntry> GetTownReleasePanelEntries() =>
        PanelRegistry.GetTownReleasePanels()
            .OrderBy(entry => entry.DisplayName, StringComparer.Ordinal)
            .ToList();

    private static void AssertActivePanel(MainForm form, PanelRegistry.PanelEntry entry, ICollection<string> failures, string source)
    {
        var activePanelName = form.PanelNavigator?.GetActivePanelName();
        if (!string.Equals(activePanelName, entry.DisplayName, StringComparison.OrdinalIgnoreCase))
        {
            failures.Add($"{entry.DisplayName}: {source} left active panel as '{activePanelName ?? "<null>"}'.");
        }
    }

    private static void AssertPanelHost(MainForm form, PanelRegistry.PanelEntry entry, ICollection<string> failures, string source)
    {
        if (entry.PanelType == typeof(FormHostPanel) && string.Equals(entry.DisplayName, "Rates", StringComparison.OrdinalIgnoreCase))
        {
            var hasRatesHost = form.MdiChildren.Any(child => child is RatesPage || child.GetType() == typeof(RatesPage));
            if (!hasRatesHost)
            {
                failures.Add($"{entry.DisplayName}: {source} did not produce a RatesPage host.");
            }

            return;
        }

        if (entry.PanelType == typeof(JARVISChatUserControl))
        {
            var rightDock = form.GetJarvisPanel();
            if (rightDock == null || !rightDock.Visible)
            {
                failures.Add($"{entry.DisplayName}: {source} did not surface the JARVIS right dock panel.");
            }

            return;
        }

        var hasMdiHost = form.MdiChildren.Any(child => string.Equals(child.Text, entry.DisplayName, StringComparison.OrdinalIgnoreCase));
        if (!hasMdiHost)
        {
            failures.Add($"{entry.DisplayName}: {source} did not create an MDI host with the requested panel name.");
        }
    }

    private static MainForm CreateOffscreenMainForm(IServiceProvider provider, bool initializeTestChrome)
    {
        var form = IntegrationTestServices.CreateMainForm(provider);
        form.ShowInTaskbar = false;
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-32000, -32000);
        form.Size = new Size(1400, 900);

        if (initializeTestChrome)
        {
            form.InitializeMinimalChromeForTests();
            form.CreateHandleForTests();
            form.InitializeForTests();
        }
        else
        {
            form.IsMdiContainer = true;
            form.CreateHandleForTests();
        }

        form.Show();
        form.Activate();
        Application.DoEvents();
        PumpMessages(4);
        return form;
    }

    private static void DisposeFormAndOwnedScope(MainForm form)
    {
        try
        {
            if (!form.IsDisposed)
            {
                if (form.IsHandleCreated)
                {
                    form.Close();
                }

                form.Dispose();
            }
        }
        finally
        {
            if (form.Tag is IDisposable ownedScope)
            {
                ownedScope.Dispose();
            }
        }
    }

    private static void PumpMessages(int cycles)
    {
        for (var index = 0; index < cycles; index++)
        {
            Application.DoEvents();
        }
    }
}