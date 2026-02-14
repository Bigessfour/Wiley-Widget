using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
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
                LogUnhandledException("Application.ThreadException", e.Exception);

                if (e.Exception is NullReferenceException && IsKnownSyncfusionPaintException(e.Exception))
                {
                    Console.WriteLine($"[TEST-BOOTSTRAP] Ignored known Syncfusion paint exception: {e.Exception.Message}");
                    return;
                }

                ExceptionDispatchInfo.Capture(e.Exception).Throw();
            };

            AppDomain.CurrentDomain.UnhandledException += static (_, e) =>
            {
                var exception = e.ExceptionObject as Exception
                    ?? new InvalidOperationException($"Non-Exception unhandled object: {e.ExceptionObject}");

                LogUnhandledException("AppDomain.UnhandledException", exception);
            };

            TaskScheduler.UnobservedTaskException += static (_, e) =>
            {
                LogUnhandledException("TaskScheduler.UnobservedTaskException", e.Exception);
                e.SetObserved();
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

    private static void LogUnhandledException(string source, Exception exception)
    {
        try
        {
            var artifactsDir = Path.Combine(AppContext.BaseDirectory, "artifacts");
            Directory.CreateDirectory(artifactsDir);

            var logPath = Path.Combine(artifactsDir, "testhost-unhandled-exceptions.log");
            var entry = $"[{DateTime.UtcNow:O}] {source}{Environment.NewLine}{exception}{Environment.NewLine}{new string('-', 80)}{Environment.NewLine}";

            File.AppendAllText(logPath, entry);
            Console.WriteLine($"[TEST-BOOTSTRAP] Captured unhandled exception from {source}. Log: {logPath}");
        }
        catch
        {
            // Swallow logging failures to avoid recursive exception handling.
        }
    }
}
