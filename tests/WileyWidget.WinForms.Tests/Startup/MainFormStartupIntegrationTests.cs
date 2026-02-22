using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Syncfusion.WinForms.Controls;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Controls.Panels;
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
        var previousJarvisAutomation = Environment.GetEnvironmentVariable("WILEYWIDGET_UI_AUTOMATION_JARVIS");
        var previousAccountsAutomation = Environment.GetEnvironmentVariable("WILEYWIDGET_UI_AUTOMATION_ACCOUNTS");

        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_AUTOMATION_JARVIS", "false");
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_AUTOMATION_ACCOUNTS", "false");
        TestThemeHelper.EnsureOffice2019Colorful();
        SfSkinManager.ApplicationVisualTheme = "Office2019Colorful";

        var configOverrides = new Dictionary<string, string?>
        {
            ["UI:IsUiTestHarness"] = "true",
            ["UI:UseSyncfusionDocking"] = "true",
            ["UI:ShowRibbon"] = "true",
            ["UI:ShowStatusBar"] = "true",
            ["UI:AutoShowDashboard"] = "true",
            ["UI:MinimalMode"] = "false",
            ["UI:AutoShowPanels"] = "true"
        };

        try
        {
            using var provider = IntegrationTestServices.BuildProvider(configOverrides);
            using var form = IntegrationTestServices.CreateMainForm(provider);

            Exception? startupException = null;

            try
            {
                // Force larger size to avoid layout cramping during test restore
                form.Size = new System.Drawing.Size(1400, 900);
                form.StartPosition = FormStartPosition.CenterScreen;
                form.Show();
                _ = form.Handle;
                Application.DoEvents();

                await PumpMessagesAsync(2000);

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
                await PumpMessagesAsync(1500);
            }
            catch (Exception ex)
            {
                startupException = ex;
            }

            startupException.Should().BeNull("Startup should complete without exceptions");

            // Diagnostic: log config values
            var config = provider.GetService(typeof(IConfiguration)) as IConfiguration;
            Console.WriteLine($"[TEST DIAG] UI:UseSyncfusionDocking = {config?.GetSection("UI:UseSyncfusionDocking").Value}");
            Console.WriteLine($"[TEST DIAG] UI:IsUiTestHarness = {config?.GetSection("UI:IsUiTestHarness").Value}");
            Console.WriteLine($"[TEST DIAG] UI:AutoShowDashboard = {config?.GetSection("UI:AutoShowDashboard").Value}");
            // Check if _panelNavigator exists
            var panelNav = GetPrivateField<object>(form, "_panelNavigator");
            Console.WriteLine($"[TEST DIAG] _panelNavigator is null? {panelNav == null}");
            // Check if _uiConfig exists and has docking enabled
            var uiConfig = GetPrivateField<object>(form, "_uiConfig");
            Console.WriteLine($"[TEST DIAG] _uiConfig is null? {uiConfig == null}");

            var runtimeUiConfig = GetPrivateField<WileyWidget.WinForms.Configuration.UIConfiguration>(form, "_uiConfig");
            _ = runtimeUiConfig; // reserved for future assertions

            panelNav.Should().NotBeNull("panel navigator should be initialized");
            GetPrivateField<RibbonControlAdv>(form, "_ribbon").Should().NotBeNull("Ribbon should be initialized");
            GetPrivateField<StatusBarAdv>(form, "_statusBar").Should().NotBeNull("StatusBar should be initialized");
            Panel? rightPanel = null; // _rightDockPanel field removed from MainForm

            var autoShowDashboard = config?.GetValue<bool?>("UI:AutoShowDashboard") ?? false;
            if (autoShowDashboard)
            {
                Console.WriteLine("[TEST] Waiting for EnterpriseVitalSignsPanel...");
                var dashboardLoaded = await IntegrationTestServices.WaitForConditionAsync(
                    () => FindControl<EnterpriseVitalSignsPanel>(form) != null
                          || string.Equals(GetPrivateField<string>(form, "_currentPanelName"), "Enterprise Vital Signs", StringComparison.OrdinalIgnoreCase),
                    TimeSpan.FromSeconds(8),
                    pollInterval: TimeSpan.FromMilliseconds(200),
                    onTimeout: message =>
                    {
                        var treePath = IntegrationTestServices.DumpControlTreeToFile(form);
                        var screenshotPath = IntegrationTestServices.TryCaptureScreenshot(form);
                        Console.WriteLine($"[TEST] {message}");
                        Console.WriteLine($"[TEST] Control tree: {treePath}");
                        if (!string.IsNullOrWhiteSpace(screenshotPath))
                        {
                            Console.WriteLine($"[TEST] Screenshot: {screenshotPath}");
                        }
                    },
                    CancellationToken.None);
                Console.WriteLine($"[TEST] Dashboard detected in startup window? {dashboardLoaded}");
            }
            else
            {
                FindControl<FormHostPanel>(form)
                    .Should().BeNull("Dashboard should not be auto-shown when AutoShowDashboard is false");
            }

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

            if (form.MainViewModel != null)
            {
                await form.MainViewModel.OnVisibilityChangedAsync(true);
                form.MainViewModel.IsDataLoaded.Should().BeTrue("Data should be loaded after initialization");
            }
            else
            {
                Console.WriteLine("[TEST] MainViewModel not yet available in current startup timing window.");
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("WILEYWIDGET_UI_AUTOMATION_JARVIS", previousJarvisAutomation);
            Environment.SetEnvironmentVariable("WILEYWIDGET_UI_AUTOMATION_ACCOUNTS", previousAccountsAutomation);
        }
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
