using System;
using Microsoft.UI.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.Licensing;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using WileyWidget.Data;
using WileyWidget.Business.Interfaces;
using WileyWidget.Services;

namespace WileyWidget
{
    public partial class App : Application
    {
        public static IServiceProvider? Services { get; private set; }
        private Window? m_window;

        public App()
        {
            // Attempt to register Syncfusion license before initializing components
            TryRegisterSyncfusionLicense();

            this.InitializeComponent();

            var services = new ServiceCollection();
            
            // Add logging
            services.AddLogging(builder =>
            {
                builder.AddDebug();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            // Add configuration (appsettings.json optional + environment)
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory ?? Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables()
                .Build();

            services.AddSingleton<IConfiguration>(configuration);

            // Register UI pages and viewmodels for DI
            services.AddTransient<Views.BudgetOverviewPage>();
            services.AddTransient<ViewModels.BudgetOverviewViewModel>();

            // Register memory cache and cache service
            services.AddWileyMemoryCache();

            // Try to wire backend data services. Use explicit registrations when project assemblies are available.
            try
            {
                // AppDbContextFactory has constructors that accept IConfiguration
                var dbFactory = new AppDbContextFactory(configuration);
                services.AddSingleton<IDbContextFactory<AppDbContext>>(dbFactory);

                // Register BudgetRepository
                services.AddScoped<IBudgetRepository, BudgetRepository>();

                // Register other Data services as needed (Transaction repository, etc.) if present
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Backend registration failed: {ex.Message}");
            }

            Services = services.BuildServiceProvider();
        }

        private void TryRegisterSyncfusionLicense()
        {
            try
            {
                // 1) Environment variable (preferred)
                string? licenseKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
                if (!string.IsNullOrWhiteSpace(licenseKey))
                {
                    SyncfusionLicenseProvider.RegisterLicense(licenseKey.Trim());
                    System.Diagnostics.Debug.WriteLine("Syncfusion license registered from environment variable.");
                    return;
                }

                // 2) license.key file next to executable
                var baseDir = AppContext.BaseDirectory ?? Directory.GetCurrentDirectory();
                var licenseFile = Path.Combine(baseDir, "license.key");
                if (File.Exists(licenseFile))
                {
                    var fileKey = File.ReadAllText(licenseFile).Trim();
                    if (!string.IsNullOrWhiteSpace(fileKey))
                    {
                        SyncfusionLicenseProvider.RegisterLicense(fileKey);
                        System.Diagnostics.Debug.WriteLine($"Syncfusion license registered from file: {licenseFile}");
                        return;
                    }
                }

                // No license found — leave to environment or external registration
                System.Diagnostics.Debug.WriteLine("Syncfusion license NOT registered (no env var or license.key found).");
            }
            catch (Exception ex)
            {
                // Avoid throwing during app construction; log to Debug only
                System.Diagnostics.Debug.WriteLine($"Syncfusion license registration failed: {ex}");
            }
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            m_window = new MainWindow();
            m_window.Activate();
        }
    }
}
