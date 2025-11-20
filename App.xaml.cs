using System;
using Microsoft.UI.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.Licensing;
using System.IO;

namespace WileyWidget
{
    public partial class App : Application
    {
        public static IServiceProvider Services { get; private set; }
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
