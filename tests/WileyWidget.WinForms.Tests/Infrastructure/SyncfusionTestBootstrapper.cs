using System;
using System.Runtime.CompilerServices;
using Syncfusion.Licensing;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;

namespace WileyWidget.WinForms.Tests.Infrastructure;

internal static class SyncfusionTestBootstrapper
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        Environment.SetEnvironmentVariable("WILEYWIDGET_TESTS", "true");

        try
        {
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
                SfSkinManager.ApplicationVisualTheme = "Default";
            }
        }
        catch
        {
            // Best-effort theme load for tests.
        }
    }
}
