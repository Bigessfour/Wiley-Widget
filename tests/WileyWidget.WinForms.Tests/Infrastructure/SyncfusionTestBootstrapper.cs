using System;
using System.Runtime.ExceptionServices;
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
        RegisterKnownWinFormsExceptionFilters();

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

    private static void RegisterKnownWinFormsExceptionFilters()
    {
        try
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException, false);
            Application.ThreadException += static (_, e) =>
            {
                if (e.Exception is NullReferenceException && IsKnownSyncfusionPaintException(e.Exception))
                {
                    Console.WriteLine($"[TEST-BOOTSTRAP] Ignored known Syncfusion paint exception: {e.Exception.Message}");
                    return;
                }

                ExceptionDispatchInfo.Capture(e.Exception).Throw();
            };
        }
        catch
        {
            // Best-effort safeguard for test-host WinForms paint exceptions.
        }
    }

    private static bool IsKnownSyncfusionPaintException(Exception exception)
    {
        var stackTrace = exception.StackTrace;
        if (string.IsNullOrWhiteSpace(stackTrace))
        {
            return false;
        }

        return stackTrace.Contains("Syncfusion.Windows.Forms.Tools.RibbonPanelThemeRenderer.DrawFrame", StringComparison.OrdinalIgnoreCase)
            || stackTrace.Contains("Syncfusion.Windows.Forms.Tools.RibbonPanel.OnNcPaint", StringComparison.OrdinalIgnoreCase)
            || stackTrace.Contains("Syncfusion.Windows.Forms.Tools.DockingManager.HostControl_Paint", StringComparison.OrdinalIgnoreCase);
    }
}
