using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FluentAssertions;
using Syncfusion.WinForms.Controls;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Tests.Infrastructure;
using WileyWidget.WinForms.Tests.Integration;
using Xunit;

namespace WileyWidget.WinForms.Tests.Startup;

[Collection("SyncfusionTheme")]
public sealed class MainFormStartupIntegrationTests
{
    [WinFormsFact]
    public async Task FullStartup_NormalConfig_SucceedsWithoutExceptions()
    {
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");
        TestThemeHelper.EnsureOffice2019Colorful();
        SfSkinManager.ApplicationVisualTheme = "Office2019Colorful";

        var configOverrides = new Dictionary<string, string?>
        {
            ["UI:IsUiTestHarness"] = "true",
            ["UI:UseSyncfusionDocking"] = "true",
            ["UI:ShowRibbon"] = "true",
            ["UI:ShowStatusBar"] = "true"
        };

        using var provider = IntegrationTestServices.BuildProvider(configOverrides);
        using var form = IntegrationTestServices.CreateMainForm(provider);

        Exception? startupException = null;

        try
        {
            _ = form.Handle;
            form.CreateControl();
            Application.DoEvents();

            InvokeOnLoad(form);
            Application.DoEvents();

            InvokeOnShown(form);
            Application.DoEvents();

            await PumpMessagesAsync(1500);

            var deferred = await WaitForDeferredInitializationAsync(form, TimeSpan.FromSeconds(5));
            if (deferred != null)
            {
                var completed = await Task.WhenAny(deferred, Task.Delay(TimeSpan.FromSeconds(5)));
                if (completed != deferred)
                {
                    throw new TimeoutException("Deferred initialization timed out");
                }
            }

            await form.InitializeAsync(CancellationToken.None);
            await PumpMessagesAsync(500);
        }
        catch (Exception ex)
        {
            startupException = ex;
        }

        startupException.Should().BeNull("Startup should complete without exceptions");

        GetPrivateField<DockingManager>(form, "_dockingManager").Should().NotBeNull("DockingManager should be initialized");
        GetPrivateField<RibbonControlAdv>(form, "_ribbon").Should().NotBeNull("Ribbon should be initialized");
        GetPrivateField<StatusBarAdv>(form, "_statusBar").Should().NotBeNull("StatusBar should be initialized");
        GetPrivateField<Panel>(form, "_centralDocumentPanel").Should().NotBeNull("Central document panel should be initialized");
        var rightPanel = GetPrivateField<Panel>(form, "_rightDockPanel");
        rightPanel.Should().NotBeNull("Right dock panel should be initialized");

        FindControl<DashboardPanel>(form).Should().NotBeNull("Dashboard should be present after initialization");

        var tabControlAdv = rightPanel != null ? FindControl<TabControlAdv>(rightPanel) : null;
        var tabControl = rightPanel != null ? FindControl<TabControl>(rightPanel) : null;
        if (tabControlAdv != null)
        {
            tabControlAdv.TabPages.Cast<TabPageAdv>().Any(tp => HasActivityOrJarvisTab(tp.Name))
                .Should().BeTrue("Right panel should include Activity Log or JARVIS tab when TabControlAdv is present");
        }
        else if (tabControl != null)
        {
            tabControl.TabPages.Cast<TabPage>().Any(tp => HasActivityOrJarvisTab(tp.Name))
                .Should().BeTrue("Right panel should include Activity Log or JARVIS tab when TabControl is present");
        }

        SfSkinManager.ApplicationVisualTheme.Should().Be("Office2019Colorful", "Default theme should be applied");

        form.MainViewModel.Should().NotBeNull("MainViewModel should be resolvable");
        if (form.MainViewModel != null)
        {
            await form.MainViewModel.OnVisibilityChangedAsync(true);
            form.MainViewModel.IsDataLoaded.Should().BeTrue("Data should be loaded after initialization");
        }
    }

    private static void InvokeOnLoad(MainForm form)
    {
        var method = typeof(MainForm).GetMethod("OnLoad", BindingFlags.Instance | BindingFlags.NonPublic);
        method?.Invoke(form, new object[] { EventArgs.Empty });
    }

    private static void InvokeOnShown(MainForm form)
    {
        var method = typeof(MainForm).GetMethod("OnShown", BindingFlags.Instance | BindingFlags.NonPublic);
        method?.Invoke(form, new object[] { EventArgs.Empty });
    }

    private static async Task<Task?> WaitForDeferredInitializationAsync(MainForm form, TimeSpan timeout)
    {
        var start = DateTime.UtcNow;
        Task? deferred = null;

        while (DateTime.UtcNow - start < timeout)
        {
            deferred = GetPrivateField<Task>(form, "_deferredInitializationTask");
            if (deferred != null)
            {
                break;
            }

            await Task.Delay(50);
        }

        return deferred;
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

    private static T? GetPrivateField<T>(object obj, string fieldName) where T : class
    {
        return obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(obj) as T;
    }

    private static T? FindControl<T>(Control root) where T : class
    {
        if (root is T match)
        {
            return match;
        }

        foreach (Control child in root.Controls)
        {
            var found = FindControl<T>(child);
            if (found != null)
            {
                return found;
            }
        }

        if (root is ToolStrip toolStrip)
        {
            foreach (ToolStripItem item in toolStrip.Items)
            {
                if (item is T matchItem)
                {
                    return matchItem;
                }
            }
        }

        return null;
    }

    private static bool HasActivityOrJarvisTab(string? tabName)
    {
        if (string.IsNullOrWhiteSpace(tabName))
        {
            return false;
        }

        return tabName.Contains("ActivityLog", StringComparison.OrdinalIgnoreCase)
            || tabName.Contains("Jarvis", StringComparison.OrdinalIgnoreCase);
    }
}
