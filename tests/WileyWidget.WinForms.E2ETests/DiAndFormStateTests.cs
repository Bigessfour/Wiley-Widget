using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WileyWidget.Business.Interfaces;
using WileyWidget.Data;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Configuration;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.ViewModels;
using Xunit;
using WinFormsWindowState = System.Windows.Forms.FormWindowState;
using SpServices = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions;

namespace WileyWidget.WinForms.E2ETests
{
    internal static class WinFormsTestThread
    {
        public static void RunInSta(Action action)
        {
            Exception? captured = null;

            var thread = new Thread(() =>
            {
                try
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    _ = Application.OleRequired();
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
    }

    public static class DiTestProvider
    {
        public static ServiceProvider BuildProvider(string databaseName)
        {
            var services = DependencyInjection.CreateServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UI:UseMdiMode"] = "false",
                ["UI:UseTabbedMdi"] = "false",
                ["UI:UseDockingManager"] = "true"
            }).Build());
            services.AddSingleton<WileyWidget.Models.HealthCheckConfiguration>();
            services.AddDbContextFactory<AppDbContext>(o => o.UseInMemoryDatabase(databaseName), ServiceLifetime.Scoped);
            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(databaseName));

            // Ensure MainForm is registered as Scoped in tests so it can resolve scoped services correctly
            // Remove production singleton and re-register as scoped for test execution
            var existingMainFormDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(MainForm));
            if (existingMainFormDescriptor != null)
            {
                services.Remove(existingMainFormDescriptor);
            }
            services.AddScoped<MainForm>();

            return services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true,
                ValidateOnBuild = true
            });
        }
    }

    public class DependencyInjectionTests
    {
        [Fact]
        [Trait("Category", "UiSmokeTests")]
        public void Resolves_all_winforms_services_and_views()
        {
            WinFormsTestThread.RunInSta(() =>
            {
                using var provider = DiTestProvider.BuildProvider($"di-tests-{Guid.NewGuid():N}");
                using var scope = provider.CreateScope();

                var types = new[]
                {
                typeof(MainForm),
                typeof(ChartForm),
                typeof(SettingsForm),
                typeof(AccountsForm),
                typeof(DashboardForm),
                typeof(BudgetOverviewForm),
                typeof(ReportsForm),
                typeof(CustomersForm),
                typeof(ChartViewModel),
                typeof(SettingsViewModel),
                typeof(AccountsViewModel),
                typeof(DashboardViewModel),
                typeof(BudgetViewModel),
                typeof(CustomersViewModel),
                typeof(MainViewModel),
                typeof(ReportsViewModel),
                typeof(IBudgetRepository),
                typeof(IMunicipalAccountRepository),
                typeof(IUtilityCustomerRepository),
                typeof(IBudgetCategoryService),
                typeof(IDashboardService)
            };

                foreach (var type in types)
                {
                    var instance = SpServices.GetRequiredService(scope.ServiceProvider, type);
                    Assert.NotNull(instance);
                }
            });
        }

        [Fact]
        public async Task AppDbContext_is_scoped_and_factory_creates_instances()
        {
            using var provider = DiTestProvider.BuildProvider($"di-scope-tests-{Guid.NewGuid():N}");
            using var scope1 = provider.CreateScope();
            using var scope2 = provider.CreateScope();

            var ctx1 = SpServices.GetRequiredService<AppDbContext>(scope1.ServiceProvider);
            var ctx2 = SpServices.GetRequiredService<AppDbContext>(scope2.ServiceProvider);
            Assert.NotSame(ctx1, ctx2);

            var factory = SpServices.GetRequiredService<IDbContextFactory<AppDbContext>>(scope1.ServiceProvider);
            await using var ctx3 = await factory.CreateDbContextAsync();
            Assert.NotNull(ctx3);
        }
    }

    public class FormStateManagerTests
    {
        [Fact]
        public void Persists_and_applies_form_state()
        {
            var logger = NullLogger.Instance;
            var manager = new FormStateManager(logger);
            var formName = $"TestForm_{Guid.NewGuid():N}";
            var expectedPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WileyWidget",
                $"{formName}.state.json");

            using var form = new Form
            {
                Location = new Point(10, 20),
                Size = new Size(400, 300),
                WindowState = WinFormsWindowState.Normal
            };

            manager.SaveFormState(form, formName, mainSplitterDistance: 120, leftSplitterDistance: 60);

            try
            {
                Assert.True(File.Exists(expectedPath));
                var state = manager.LoadFormState(formName);
                Assert.NotNull(state);

                using var targetForm = new Form();
                manager.ApplyFormState(targetForm, state!);

                Assert.Equal(new Size(400, 300), targetForm.Size);
                Assert.Equal(WinFormsWindowState.Normal, targetForm.WindowState);
            }
            finally
            {
                if (File.Exists(expectedPath))
                {
                    File.Delete(expectedPath);
                }
            }
        }
    }
}
