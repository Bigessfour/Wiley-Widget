using Microsoft.Extensions.DependencyInjection;
using Serilog;
using WileyWidget.WinForms.Configuration;  // You'll create this in a sec
using WileyWidget.WinForms.Forms;
using Syncfusion.Licensing; // Uncomment when you have a license key
using Syncfusion.Windows.Forms;

namespace WileyWidget.WinForms
{
    internal static class Program
    {
        public static IServiceProvider Services { get; private set; } = null!;

        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            // === Your existing Serilog + DI bootstrap (100% unchanged) ===
            Services = DependencyInjection.ConfigureServices();

            // === Register Syncfusion License ===
            string licenseKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", EnvironmentVariableTarget.Machine);
            if (!string.IsNullOrEmpty(licenseKey))
            {
                Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(licenseKey);
            }

            // === Apply global Syncfusion theming ===
            // Note: skip applying Syncfusion skins at design-time to avoid missing types in some environments.
            // If you have Syncfusion skins available, uncomment and ensure the correct skin type exists.
            // SfSkinManager.SetSkin(new MaterialLightSkin());

            // === Launch your main form ===
            Application.Run(Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<MainForm>(Services));
        }
    }
}
