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
using WileyWidget.WinForms.Tests.Infrastructure;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Forms;

[Trait("Category", "Unit")]
[Trait("Category", "UiSmokeTests")]
[Collection(WinFormsUiCollection.CollectionName)]
public class DockingTabbedMdiIntegrationTests
{
    private readonly WinFormsUiThreadFixture _ui;

    public DockingTabbedMdiIntegrationTests(WinFormsUiThreadFixture ui)
    {
        _ui = ui;
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
        _ui.Run(() =>
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

            using var mainForm = new MainForm(serviceProvider, config, NullLogger<MainForm>.Instance, WileyWidget.WinForms.Configuration.ReportViewerLaunchOptions.Disabled);

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

                // Syncfusion v32.1.9: TabbedMDI disables document mode for proper tab integration
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
        _ui.Run(() =>
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

            using var mainForm = new MainForm(serviceProvider, config, NullLogger<MainForm>.Instance, WileyWidget.WinForms.Configuration.ReportViewerLaunchOptions.Disabled);

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
