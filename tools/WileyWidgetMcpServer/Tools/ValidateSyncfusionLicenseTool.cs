using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Windows.Forms;
using Syncfusion.Licensing;
using Syncfusion.WinForms.DataGrid;

namespace WileyWidget.McpServer.Tools;

/// <summary>
/// MCP tool to validate that a Syncfusion license is present and that a simple Syncfusion control
/// can initialize without triggering license popups (e.g., evaluation/invalid dialogs or watermarks).
/// </summary>
public static class ValidateSyncfusionLicenseTool
{
    [McpServerTool]
    [Description("Validates Syncfusion license by registering the license from environment and performing a small UI load check.")]
    public static string ValidateSyncfusionLicense(
        [Description("Output format: 'text' or 'json' (default: 'text')")] string outputFormat = "text")
    {
        var result = new LicenseValidationResult();

        try
        {
            // Find license key in environment variables (common names)
            var key = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY")
                      ?? Environment.GetEnvironmentVariable("Syncfusion:LicenseKey")
                      ?? Environment.GetEnvironmentVariable("Syncfusion_LicenseKey");

            if (string.IsNullOrWhiteSpace(key))
            {
                return FormatError("No Syncfusion license key found in environment.", outputFormat);
            }

            result.Present = true;
            result.KeyHash = ShortHash(key);

            // Try registering the license (safe - RegisterLicense is idempotent)
            try
            {
                SyncfusionLicenseProvider.RegisterLicense(key);
                result.Registered = true;
                result.RegisteredMessage = "Registered via environment key.";
            }
            catch (Exception ex)
            {
                result.Registered = false;
                result.RegisteredMessage = ex.Message;
            }

            // Run a small UI check on an STA thread to try to instantiate an SfDataGrid and detect dialogs
            var uiCheck = new UiCheckResult();
            var t = new Thread(() =>
            {
                try
                {
                    using var f = new Form { StartPosition = FormStartPosition.Manual, ShowInTaskbar = false, Width = 280, Height = 160, Opacity = 0 };
                    var grid = new SfDataGrid { Dock = DockStyle.Fill, Name = "_LicenseCheckGrid" };
                    f.Controls.Add(grid);

                    // Show form briefly to allow components to initialize
                    f.Show();

                    var sw = Stopwatch.StartNew();
                    while (sw.ElapsedMilliseconds < 500)
                    {
                        Application.DoEvents();
                    }

                    // Collect any modal/dialog windows that could indicate license popups
                    var dialogs = new List<string>();
                    foreach (Form open in Application.OpenForms)
                    {
                        if (open == f) continue;
                        var title = (open.Text ?? string.Empty).Trim();
                        if (string.IsNullOrEmpty(title)) continue;
                        // Heuristic checks for license-related dialogs
                        var lower = title.ToLowerInvariant();
                        if (lower.Contains("license") || lower.Contains("evaluation") || lower.Contains("trial") || lower.Contains("invalid") || lower.Contains("watermark") || lower.Contains("syncfusion"))
                        {
                            dialogs.Add(title);
                        }
                    }

                    uiCheck.Loaded = f.IsHandleCreated;
                    uiCheck.Dialogs = dialogs.ToArray();

                    f.Hide();
                }
                catch (Exception ex)
                {
                    uiCheck.Exception = ex.Message;
                }
            });

            t.SetApartmentState(ApartmentState.STA);
            t.IsBackground = true;
            t.Start();
            var finished = t.Join(3000);
            if (!finished)
            {
                uiCheck.TimedOut = true;
            }

            result.UiLoaded = uiCheck.Loaded;
            result.UiDialogs = uiCheck.Dialogs ?? Array.Empty<string>();
            result.UiException = uiCheck.Exception;
            result.UiTimedOut = uiCheck.TimedOut;

            // Build output
            return outputFormat.ToLowerInvariant() == "json" ? FormatJson(result) : FormatText(result);
        }
        catch (Exception ex)
        {
            return FormatError($"Validation error: {ex.Message}\n\n{ex.StackTrace}", outputFormat);
        }
    }

    private static string FormatText(LicenseValidationResult r)
    {
        var lines = new List<string>
        {
            $"License presence: {(r.Present ? "present" : "absent")}",
            $"Registered: {(r.Registered ? "yes" : "no")}",
            $"Key hash: {r.KeyHash}",
            $"UI control loaded: {(r.UiLoaded ? "yes" : "no")}",
            $"Dialogs detected: {(r.UiDialogs?.Length ?? 0)}"
        };

        if (r.UiDialogs?.Length > 0)
        {
            lines.Add("Dialogs:");
            lines.AddRange(r.UiDialogs.Select(d => $"  - {d}"));
        }

        if (!string.IsNullOrEmpty(r.RegisteredMessage))
            lines.Add($"Registered message: {r.RegisteredMessage}");
        if (!string.IsNullOrEmpty(r.UiException))
            lines.Add($"UI exception: {r.UiException}");
        if (r.UiTimedOut)
            lines.Add("UI check: timed out");

        return string.Join("\n", lines);
    }

    private static string FormatJson(LicenseValidationResult r)
    {
        return JsonSerializer.Serialize(r, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string FormatError(string msg, string outputFormat)
    {
        if (outputFormat.ToLowerInvariant() == "json")
        {
            var e = new { success = false, error = msg };
            return JsonSerializer.Serialize(e, new JsonSerializerOptions { WriteIndented = true });
        }

        return $"❌ Error: {msg}";
    }

    private static string ShortHash(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret)) return string.Empty;
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(secret);
        var hash = sha.ComputeHash(bytes);
        var hex = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        return hex.Substring(0, Math.Min(8, hex.Length));
    }

    private class LicenseValidationResult
    {
        public bool Present { get; set; }
        public bool Registered { get; set; }
        public string? RegisteredMessage { get; set; }
        public string? KeyHash { get; set; }
        public bool UiLoaded { get; set; }
        public string[] UiDialogs { get; set; } = Array.Empty<string>();
        public string? UiException { get; set; }
        public bool UiTimedOut { get; set; }
    }

    private class UiCheckResult
    {
        public bool Loaded { get; set; }
        public string[]? Dialogs { get; set; }
        public string? Exception { get; set; }
        public bool TimedOut { get; set; }
    }
}
