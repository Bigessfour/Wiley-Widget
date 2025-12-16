using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using WileyWidget.Data;
using WileyWidget.WinForms.Configuration;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Tests.Infrastructure;
using Xunit;
using SfTools = Syncfusion.Windows.Forms.Tools;

namespace WileyWidget.WinForms.Tests.Unit.Forms;

[Trait("Category", "Unit")]
[Trait("Category", "UiSmokeTests")]
[Collection(WinFormsUiCollection.CollectionName)]
public class MainFormUiSmokeTests
{
    private readonly WinFormsUiThreadFixture _ui;

    public MainFormUiSmokeTests(WinFormsUiThreadFixture ui)
    {
        _ui = ui;
    }

    private static IConfiguration BuildConfig(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    [Fact]
    public void MainForm_Construct_And_Toggle_MdiMode_DoesNotThrow()
    {
        _ui.Run(() =>
        {
            var config = BuildConfig(new Dictionary<string, string?>
            {
                ["UI:IsUiTestHarness"] = "true",
                ["UI:UseMdiMode"] = "true",
                ["UI:UseTabbedMdi"] = "false",
                ["UI:UseDockingManager"] = "false"
            });

            using var mainForm = new MainForm(new ServiceCollection().BuildServiceProvider(), config, NullLogger<MainForm>.Instance, WileyWidget.WinForms.Configuration.ReportViewerLaunchOptions.Disabled);

            Assert.True(mainForm.IsMdiContainer);

            mainForm.UseMdiMode = false;
            Assert.False(mainForm.IsMdiContainer);

            mainForm.UseMdiMode = true;
            Assert.True(mainForm.IsMdiContainer);
        });
    }

    [Fact]
    public void MainForm_ToggleMdiMode_Method_Updates_IsMdiContainer()
    {
        _ui.Run(() =>
        {
            var config = BuildConfig(new Dictionary<string, string?>
            {
                ["UI:IsUiTestHarness"] = "true",
                ["UI:UseMdiMode"] = "false",
                ["UI:UseTabbedMdi"] = "false",
                ["UI:UseDockingManager"] = "false"
            });

            using var mainForm = new MainForm(new ServiceCollection().BuildServiceProvider(), config, NullLogger<MainForm>.Instance, WileyWidget.WinForms.Configuration.ReportViewerLaunchOptions.Disabled);

            Assert.False(mainForm.IsMdiContainer);

            var toggle = typeof(MainForm).GetMethod("ToggleMdiMode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(toggle);

            // Invoke once - should enable MDI
            toggle!.Invoke(mainForm, null);
            Assert.True(mainForm.IsMdiContainer);

            // Invoke again - should disable MDI
            toggle.Invoke(mainForm, null);
            Assert.False(mainForm.IsMdiContainer);
        });
    }

    [Fact]
    public void MainForm_ShowPanel_IsSafeNoOp_WhenPanelNotAvailable()
    {
        _ui.Run(() =>
        {
            var config = BuildConfig(new Dictionary<string, string?>
            {
                ["UI:IsUiTestHarness"] = "true",
                ["UI:UseMdiMode"] = "true",
                ["UI:UseTabbedMdi"] = "false",
                ["UI:UseDockingManager"] = "true"
            });

            using var mainForm = new MainForm(new ServiceCollection().BuildServiceProvider(), config, NullLogger<MainForm>.Instance, WileyWidget.WinForms.Configuration.ReportViewerLaunchOptions.Disabled);

            // Uses reflection internally and intentionally swallows failures.
            mainForm.ShowPanel<UserControl>("DoesNotExist");
        });
    }

    [Fact]
    public void MainForm_AutoShows_Dashboard_WhenConfigured()
    {
        _ui.Run(() =>
        {
            var config = BuildConfig(new Dictionary<string, string?>
            {
                ["UI:IsUiTestHarness"] = "true",
                ["UI:UseMdiMode"] = "true",
                ["UI:UseTabbedMdi"] = "false",
                ["UI:UseDockingManager"] = "false",
                ["UI:AutoShowDashboard"] = "true"
            });

            using var mainForm = new MainForm(new ServiceCollection().BuildServiceProvider(), config, NullLogger<MainForm>.Instance, WileyWidget.WinForms.Configuration.ReportViewerLaunchOptions.Disabled);

            // Trigger OnLoad by showing the form
            mainForm.Show();

            // Verify the auto-show attempt occurred (recorded even if child creation failed)
            var dashField = typeof(MainForm).GetField("_dashboardAutoShown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(dashField);
            Assert.True((bool)dashField.GetValue(mainForm)!);

            // Cleanup
            mainForm.Close();
        });
    }

    [Fact]
    public void MainForm_DockPanel_WhenDockingNotInitialized_ThrowsInvalidOperationException()
    {
        _ui.Run(() =>
        {
            var config = BuildConfig(new Dictionary<string, string?>
            {
                ["UI:IsUiTestHarness"] = "true",
                ["UI:UseDockingManager"] = "false",
                ["UI:UseMdiMode"] = "false",
                ["UI:UseTabbedMdi"] = "false"
            });

            using var mainForm = new MainForm(new ServiceCollection().BuildServiceProvider(), config, NullLogger<MainForm>.Instance, WileyWidget.WinForms.Configuration.ReportViewerLaunchOptions.Disabled);

            Assert.Throws<InvalidOperationException>(() => mainForm.DockPanel<UserControl>("TestPanel", SfTools.DockingStyle.Left));
        });
    }

    [Fact]
    public void DiContainer_Resolves_Core_WinForms_Types_WithoutThrowing()
    {
        _ui.Run(() =>
        {
            var services = WileyWidget.WinForms.Configuration.DependencyInjection.CreateServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConfiguration>(BuildConfig(new Dictionary<string, string?>
            {
                ["UI:IsUiTestHarness"] = "true",
                ["UI:UseMdiMode"] = "false",
                ["UI:UseTabbedMdi"] = "false",
                ["UI:UseDockingManager"] = "true"
            }));
            services.AddSingleton<WileyWidget.Models.HealthCheckConfiguration>();

            var dbName = $"ui-smoke-{Guid.NewGuid():N}";
            services.AddDbContextFactory<AppDbContext>(o => o.UseInMemoryDatabase(dbName), ServiceLifetime.Scoped);
            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));

            // For tests, ensure MainForm is resolved with test scope and not built from the root provider
            var existingMainFormDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(MainForm));
            if (existingMainFormDescriptor != null)
            {
                services.Remove(existingMainFormDescriptor);
            }
            services.AddScoped<MainForm>();

            using var provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true,
                ValidateOnBuild = true
            });

            using var scope = provider.CreateScope();

            var types = new[]
            {
                typeof(MainForm),
                typeof(DashboardForm),
                typeof(AccountsForm),
                typeof(ChartForm),
                typeof(ReportsForm),
                typeof(SettingsForm)
            };

            var disposables = new List<IDisposable>();
            foreach (var type in types)
            {
                Console.WriteLine($"Resolving: {type.FullName}");
                var instance = scope.ServiceProvider.GetRequiredService(type);
                Console.WriteLine($"Resolved: {type.FullName} => {instance?.GetType().FullName}");
                if (instance is MainForm mf)
                {
                    Console.WriteLine($"MainForm.IsMdiContainer = {mf.IsMdiContainer}");
                }
                Assert.NotNull(instance);

                if (instance is IDisposable disposable)
                {
                    disposables.Add(disposable);
                }
            }

            // Dispose in reverse order to be polite for any parent-child relationships
            for (int i = disposables.Count - 1; i >= 0; i--)
            {
                disposables[i].Dispose();
            }
        });
    }
}
