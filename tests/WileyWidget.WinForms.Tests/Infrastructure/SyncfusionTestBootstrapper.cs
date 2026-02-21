using System;
using System.Runtime.CompilerServices;
using Syncfusion.Licensing;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;
using System.Windows.Forms;

namespace WileyWidget.WinForms.Tests.Infrastructure;

internal static class SyncfusionTestBootstrapper
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        Environment.SetEnvironmentVariable("WILEYWIDGET_TESTS", "true");

        try
        {
            // Ensure WinForms visual styles and DPI awareness are set for tests that exercise rendering.
            try
            {
                Application.SetHighDpiMode(HighDpiMode.SystemAware);
                Application.EnableVisualStyles();
            }
            catch { /* best-effort: some test hosts may reject these calls */ }

            var licenseKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY")
                             ?? Environment.GetEnvironmentVariable("Syncfusion__LicenseKey");

            if (!string.IsNullOrWhiteSpace(licenseKey))
            {
                SyncfusionLicenseProvider.RegisterLicense(licenseKey);
            }
        }
        catch
        {
            // Best-effort licensing for tests; failures will surface in Syncfusion controls if critical.
        }

        try
        {
            Syncfusion.Windows.Forms.SkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);
            SfSkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);
            if (string.IsNullOrWhiteSpace(SfSkinManager.ApplicationVisualTheme))
            {
                // Use a concrete Office2019 theme for tests so theme renderers have valid resources
                SfSkinManager.ApplicationVisualTheme = "Office2019Colorful";
            }
        }
        catch
        {
            // Best-effort theme load for tests.
        }
    }
}
