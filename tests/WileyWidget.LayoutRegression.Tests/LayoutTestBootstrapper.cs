using System;
using System.Windows.Forms;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using Syncfusion.Licensing;
using Syncfusion.WinForms.Themes;
using WileyWidget.WinForms;

// No namespace: a [SetUpFixture] in the global namespace runs OneTimeSetUp/TearDown
// for the entire test assembly, covering MainFormLayoutTests (which also has no namespace).

/// <summary>
/// Assembly-wide setup: registers the Syncfusion license and loads Office2019 theme before
/// any layout regression test runs, preventing trial popups from blocking test execution.
/// </summary>
[SetUpFixture]
public class LayoutTestBootstrapper
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        // ── 1. Suppress modal dialogs from WinForms / Syncfusion ──
        try { Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException); } catch { }
        Application.ThreadException += (_, e) => { TestContext.WriteLine($"[layout-test] ThreadException: {e.Exception.Message}"); };

        // ── 2. Register Syncfusion license to prevent the trial popup ──
        try
        {
            var licenseKey = ResolveLicenseKey();
            if (!string.IsNullOrWhiteSpace(licenseKey))
            {
                SyncfusionLicenseProvider.RegisterLicense(licenseKey);
                TestContext.WriteLine("[layout-test] Syncfusion license registered.");
            }
            else
            {
                TestContext.WriteLine("[layout-test] Syncfusion license key not found; trial popup suppressed via WinForms exception mode.");
            }
        }
        catch (Exception ex)
        {
            TestContext.WriteLine($"[layout-test] License registration failed (non-fatal): {ex.Message}");
        }

        // ── 3. Load Office2019 theme so tab renderers don't throw ──
        try
        {
            Syncfusion.Windows.Forms.SkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);
            Syncfusion.WinForms.Controls.SfSkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);
            if (string.IsNullOrWhiteSpace(Syncfusion.WinForms.Controls.SfSkinManager.ApplicationVisualTheme))
                Syncfusion.WinForms.Controls.SfSkinManager.ApplicationVisualTheme = "Office2019Colorful";
        }
        catch { /* best-effort */ }

        // ── 4. WinForms visual styles ──
        try { Application.EnableVisualStyles(); } catch { }
    }

    [OneTimeTearDown]
    public void OneTimeTearDown() { }

    private static string? ResolveLicenseKey()
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
            Environment.GetEnvironmentVariable("Syncfusion:LicenseKey", EnvironmentVariableTarget.Machine),
            Environment.GetEnvironmentVariable("SyncfusionLicenseKey", EnvironmentVariableTarget.Process)
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
                .AddUserSecrets<Program>(optional: true)
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
}
