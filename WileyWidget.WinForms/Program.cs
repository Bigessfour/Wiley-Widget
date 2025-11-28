using Microsoft.Extensions.DependencyInjection;
using Serilog;
using WileyWidget.WinForms.Configuration;  // You'll create this in a sec
using WileyWidget.WinForms.Forms;
// using Syncfusion.Licensing; // Uncomment when you have a license key

namespace WileyWidget.WinForms
{
    internal static class Program
    {
        public static IServiceProvider Services { get; private set; } = null!;

        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            // Configure DI first (we may fetch license from secret vault)
            Services = DependencyInjection.ConfigureServices();

            // Register Syncfusion license KEY per official guidance BEFORE any Syncfusion control is created
            // Source: https://help.syncfusion.com/windowsforms/licensing/how-to-register-in-an-application
            var loggerFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<Microsoft.Extensions.Logging.ILoggerFactory>(Services);
            // Use a non-generic logger name — Program is a static class, static types cannot be used as generic type arguments.
            var logger = loggerFactory?.CreateLogger("Program");

            string? licenseKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
            if (string.IsNullOrWhiteSpace(licenseKey))
            {
                try
                {
                    var vault = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.Services.ISecretVaultService>(Services);
                    licenseKey = vault?.GetSecret("SyncfusionLicenseKey");
                }
                catch (Exception ex)
                {
                    // Fallback: ignore detailed debug if generic logger
                    if (logger != null) Microsoft.Extensions.Logging.LoggerExtensions.LogDebug(logger as Microsoft.Extensions.Logging.ILogger, ex, "Secret vault lookup for Syncfusion license key failed (non-fatal)." );
                }
            }

            if (!string.IsNullOrWhiteSpace(licenseKey))
            {
                try
                {
                    Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(licenseKey);
                    if (logger != null) Microsoft.Extensions.Logging.LoggerExtensions.LogInformation(logger as Microsoft.Extensions.Logging.ILogger, "Syncfusion license registered successfully.");
                }
                catch (Exception ex)
                {
                    if (logger != null) Microsoft.Extensions.Logging.LoggerExtensions.LogWarning(logger as Microsoft.Extensions.Logging.ILogger, ex, "Failed to register Syncfusion license key (controls may throw licensing warnings).");
                }
            }
            else
            {
                if (logger != null) Microsoft.Extensions.Logging.LoggerExtensions.LogInformation(logger as Microsoft.Extensions.Logging.ILogger, "No Syncfusion license key found (env: SYNCFUSION_LICENSE_KEY or secret 'SyncfusionLicenseKey'). Running without registration.");
            }

            // Launch main form
            Application.Run(Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<MainForm>(Services));
        }
    }
}
