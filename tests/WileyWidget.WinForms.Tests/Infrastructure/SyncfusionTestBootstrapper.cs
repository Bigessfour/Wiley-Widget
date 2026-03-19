using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Syncfusion.Licensing;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;
using System.Windows.Forms;
using WileyWidget.Services;

namespace WileyWidget.WinForms.Tests.Infrastructure;

internal static class SyncfusionTestBootstrapper
{
    private static readonly string[] SyncfusionLicenseKeyAliases =
    [
        "WILEY_SYNC_LIC_KEY",
        "SYNCFUSION_LICENSE_KEY",
        "Syncfusion:LicenseKey",
        "Syncfusion__LicenseKey",
        "Syncfusion-LicenseKey",
        "SyncfusionLicenseKey",
        "syncfusion-license-key"
    ];

    [ModuleInitializer]
    internal static void Initialize()
    {
        Environment.SetEnvironmentVariable("WILEYWIDGET_TESTS", "true");
        Environment.SetEnvironmentVariable("DOTNET_TEST_MODE", "true");
        Environment.SetEnvironmentVariable("SYNCFUSION_SILENT_LICENSE_VALIDATION", "true");
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
                PromoteLicenseToProcessEnvironment(licenseKey);
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
            // Tests must fail fast without blocking modal UI. CatchException mode can surface
            // WinForms JIT dialogs that trap the testhost behind uncloseable popups.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException, false);

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
        foreach (var alias in SyncfusionLicenseKeyAliases)
        {
            foreach (var scope in new[] { EnvironmentVariableTarget.Process, EnvironmentVariableTarget.User, EnvironmentVariableTarget.Machine })
            {
                var normalized = NormalizeSecret(Environment.GetEnvironmentVariable(alias, scope));
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    return normalized;
                }
            }
        }

        try
        {
            var configuration = new ConfigurationBuilder()
                .AddUserSecrets<WileyWidget.WinForms.Program>(optional: true)
                .Build();

            foreach (var alias in SyncfusionLicenseKeyAliases)
            {
                var normalized = NormalizeSecret(configuration[alias]);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    return normalized;
                }
            }
        }
        catch
        {
            // Best effort for environments where user-secrets is unavailable.
        }

        try
        {
            using var vault = new EncryptedLocalSecretVaultService(NullLogger<EncryptedLocalSecretVaultService>.Instance);
            foreach (var alias in SyncfusionLicenseKeyAliases)
            {
                var normalized = NormalizeSecret(vault.GetSecret(alias));
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    return normalized;
                }
            }
        }
        catch
        {
            // Best effort for environments where the encrypted vault is unavailable.
        }

        return null;
    }

    private static void PromoteLicenseToProcessEnvironment(string licenseKey)
    {
        foreach (var alias in SyncfusionLicenseKeyAliases)
        {
            try
            {
                Environment.SetEnvironmentVariable(alias, licenseKey, EnvironmentVariableTarget.Process);
            }
            catch
            {
                // Best effort only. Some aliases may not be valid environment variable names on all hosts.
            }
        }
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
