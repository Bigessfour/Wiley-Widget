using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
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

            var licenseKey = ResolveSyncfusionLicenseKey();

            if (!string.IsNullOrWhiteSpace(licenseKey))
            {
                Environment.SetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", licenseKey, EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable("Syncfusion__LicenseKey", licenseKey, EnvironmentVariableTarget.Process);
                SyncfusionLicenseProvider.RegisterLicense(licenseKey);
            }
            else
            {
                Console.WriteLine("[TEST-BOOTSTRAP] Syncfusion license key not found in process/user/machine env or user-secrets; tests may show trial dialogs.");
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

    private static string? ResolveSyncfusionLicenseKey()
    {
        string?[] envCandidates =
        {
            Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", EnvironmentVariableTarget.Process),
            Environment.GetEnvironmentVariable("Syncfusion__LicenseKey", EnvironmentVariableTarget.Process),
            Environment.GetEnvironmentVariable("Syncfusion:LicenseKey", EnvironmentVariableTarget.Process),
            Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", EnvironmentVariableTarget.User),
            Environment.GetEnvironmentVariable("Syncfusion__LicenseKey", EnvironmentVariableTarget.User),
            Environment.GetEnvironmentVariable("Syncfusion:LicenseKey", EnvironmentVariableTarget.User),
            Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", EnvironmentVariableTarget.Machine),
            Environment.GetEnvironmentVariable("Syncfusion__LicenseKey", EnvironmentVariableTarget.Machine),
            Environment.GetEnvironmentVariable("Syncfusion:LicenseKey", EnvironmentVariableTarget.Machine)
        };

        foreach (var candidate in envCandidates)
        {
            var normalized = NormalizeSecret(candidate);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                return normalized;
            }
        }

        try
        {
            var configuration = new ConfigurationBuilder()
                .AddUserSecrets<WileyWidget.WinForms.Program>(optional: true)
                .Build();

            var fromUserSecrets = NormalizeSecret(configuration["Syncfusion:LicenseKey"])
                ?? NormalizeSecret(configuration["SYNCFUSION_LICENSE_KEY"])
                ?? NormalizeSecret(configuration["Syncfusion__LicenseKey"]);

            if (!string.IsNullOrWhiteSpace(fromUserSecrets))
            {
                return fromUserSecrets;
            }
        }
        catch
        {
            // Best effort for environments where user-secrets is unavailable.
        }

        return null;
    }

    private static string? NormalizeSecret(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var trimmed = rawValue.Trim();
        if ((trimmed.StartsWith("\"", StringComparison.Ordinal) && trimmed.EndsWith("\"", StringComparison.Ordinal))
            || (trimmed.StartsWith("'", StringComparison.Ordinal) && trimmed.EndsWith("'", StringComparison.Ordinal)))
        {
            trimmed = trimmed.Substring(1, trimmed.Length - 2).Trim();
        }

        if (trimmed.StartsWith("YOUR_SYNCFUSION_LICENSE_KEY", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("SYNCFUSION_LICENSE_KEY_HERE", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return trimmed;
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
