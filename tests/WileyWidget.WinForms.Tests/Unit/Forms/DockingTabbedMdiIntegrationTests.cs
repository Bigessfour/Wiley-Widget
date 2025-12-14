using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SfTools = Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Forms;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Forms;

[Trait("Category", "Unit")]
[Trait("Category", "UiSmokeTests")]
public class DockingTabbedMdiIntegrationTests
{
    private static void RunInSta(System.Action action)
    {
        Exception? captured = null;

        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                captured = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (captured != null)
        {
            throw captured;
        }
    }

    private static void SetPrivateField(object target, string fieldName, object? value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(target, value);
    }

    [Fact]
    public void RegisterAsDockingMDIChild_WhenTabbedMdiEnabled_DisablesDockingDocumentMode()
    {
        RunInSta(() =>
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["UI:UseDockingManager"] = "true",
                    ["UI:UseMdiMode"] = "true",
                    ["UI:UseTabbedMdi"] = "true",
                    ["UI:IsUiTestHarness"] = "false"
                })
                .Build();

            var serviceProvider = new ServiceCollection().BuildServiceProvider();

            using var mainForm = new MainForm(serviceProvider, config, NullLogger<MainForm>.Instance);

            var components = new Container();
            var dockingManager = new SfTools.DockingManager(components)
            {
                HostControl = mainForm,
                EnableDocumentMode = true
            };

            try
            {
                SetPrivateField(mainForm, "_dockingManager", dockingManager);

                using var child = new System.Windows.Forms.Form();
                mainForm.RegisterAsDockingMDIChild(child, enabled: true);

                Assert.False(dockingManager.EnableDocumentMode);
            }
            finally
            {
                SetPrivateField(mainForm, "_dockingManager", null);
                dockingManager.Dispose();
                components.Dispose();
            }
        });
    }

    [Fact]
    public void RegisterAsDockingMDIChild_WhenTabbedMdiDisabled_DoesNotChangeDocumentMode()
    {
        RunInSta(() =>
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["UI:UseDockingManager"] = "true",
                    ["UI:UseMdiMode"] = "true",
                    ["UI:UseTabbedMdi"] = "false",
                    ["UI:IsUiTestHarness"] = "false"
                })
                .Build();

            var serviceProvider = new ServiceCollection().BuildServiceProvider();

            using var mainForm = new MainForm(serviceProvider, config, NullLogger<MainForm>.Instance);

            var components = new Container();
            var dockingManager = new SfTools.DockingManager(components)
            {
                HostControl = mainForm,
                EnableDocumentMode = true
            };

            try
            {
                SetPrivateField(mainForm, "_dockingManager", dockingManager);

                using var child = new System.Windows.Forms.Form();
                mainForm.RegisterAsDockingMDIChild(child, enabled: true);

                Assert.True(dockingManager.EnableDocumentMode);
            }
            finally
            {
                SetPrivateField(mainForm, "_dockingManager", null);
                dockingManager.Dispose();
                components.Dispose();
            }
        });
    }
}
