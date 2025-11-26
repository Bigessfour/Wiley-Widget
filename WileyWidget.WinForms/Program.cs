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

            // === Your existing Serilog + DI bootstrap (100% unchanged) ===
            Services = DependencyInjection.ConfigureServices();

            // === Register Syncfusion License ===
            // TODO: Replace with your actual Syncfusion license key
            // Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("YOUR_SYNCFUSION_LICENSE_KEY_HERE");

            // === Launch your main form ===
            Application.Run(Services.GetRequiredService<MainForm>());
        }
    }
}
