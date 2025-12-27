using ModelContextProtocol.Server;
using System.ComponentModel;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace WileyWidget.McpServer.Tools;

/// <summary>
/// MCP tool for running headless form tests via C# scripts (.csx files) or inline code.
/// Executes test scripts similar to the WileyWidget.UITests runner.
/// </summary>
[McpServerToolType]
public static class RunHeadlessFormTestTool
{
    [McpServerTool]
    [Description("Executes a headless UI test for a WinForms form. Can run predefined .csx test scripts or inline C# test code. Returns pass/fail status and any errors encountered.")]
    public static async Task<string> RunHeadlessFormTest(
        [Description("Optional: Path to .csx script file relative to workspace root (e.g., 'tests/WileyWidget.UITests/Scripts/AccountsFormTest.csx')")]
        string? scriptPath = null,
        [Description("Optional: Inline C# test code to execute. Used if scriptPath not provided.")]
        string? testCode = null,
        [Description("Optional: Specific form type to test (e.g., 'WileyWidget.WinForms.Forms.AccountsForm'). Used with inline code.")]
        string? formTypeName = null,
        [Description("Optional: Execution timeout in seconds (default: 60)")]
        int timeoutSeconds = 60)
    {
        try
        {
            if (string.IsNullOrEmpty(scriptPath) && string.IsNullOrEmpty(testCode))
            {
                return "❌ Either 'scriptPath' or 'testCode' must be provided.";
            }

            string codeToExecute;
            string testDescription;

            if (!string.IsNullOrEmpty(scriptPath))
            {
                // Load from file
                var fullPath = Path.Combine(Environment.CurrentDirectory, scriptPath);
                if (!File.Exists(fullPath))
                {
                    // Try from workspace root
                    var workspaceRoot = FindWorkspaceRoot();
                    if (workspaceRoot != null)
                    {
                        fullPath = Path.Combine(workspaceRoot, scriptPath);
                    }
                }

                if (!File.Exists(fullPath))
                {
                    return $"❌ Script file not found: {scriptPath}";
                }

                codeToExecute = await File.ReadAllTextAsync(fullPath);
                testDescription = Path.GetFileName(scriptPath);
            }
            else
            {
                // Use inline code
                codeToExecute = testCode!;
                testDescription = formTypeName ?? "Inline Test";
            }

            // Configure script options (include Syncfusion theme and data grid assemblies)
            var scriptOptions = ScriptOptions.Default
                .WithReferences(typeof(System.Windows.Forms.Form).Assembly)
                .WithReferences(typeof(Syncfusion.WinForms.Controls.SfForm).Assembly)
                .WithReferences(typeof(Syncfusion.WinForms.DataGrid.SfDataGrid).Assembly)
                .WithReferences(typeof(Syncfusion.WinForms.Themes.Office2019Theme).Assembly)
                .WithReferences(typeof(WileyWidget.WinForms.Forms.MainForm).Assembly)
                .WithReferences(typeof(WileyWidget.McpServer.Helpers.SyncfusionTestHelper).Assembly)
                .WithImports(
                    "System",
                    "System.Text",
                    "System.Windows.Forms",
                    "System.Linq",
                    "Syncfusion.WinForms.Controls",
                    "Syncfusion.WinForms.DataGrid",
                    "Syncfusion.WinForms.Themes",
                    "WileyWidget.WinForms.Forms",
                    "WileyWidget.McpServer.Helpers" );

            // Pre-load theme assemblies to ensure availability at runtime
            try
            {
                var _ = typeof(Syncfusion.WinForms.Themes.Office2019Theme);
            }
            catch { }

            // Execute script with stdout/stderr capture and timeout
            var startTime = DateTime.UtcNow;
            if (timeoutSeconds <= 0) timeoutSeconds = 60;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            var output = new System.Text.StringBuilder();
            var originalOut = Console.Out;
            var originalError = Console.Error;
            try
            {
                var writer = new System.IO.StringWriter(output);
                Console.SetOut(writer);
                Console.SetError(writer);
                writer.WriteLine($"Using timeout: {timeoutSeconds}s");

                // Attempt to register Syncfusion license from environment for this process to avoid popup dialogs
                try
                {
                    var licenseKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY") ?? Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE");
                    if (!string.IsNullOrWhiteSpace(licenseKey))
                    {
                        try
                        {
                            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(licenseKey);
                            writer.WriteLine("Syncfusion license registered from environment variable SYNCFUSION_LICENSE_KEY");
                        }
                        catch (Exception ex)
                        {
                            writer.WriteLine($"Warning: Syncfusion license registration failed: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    writer.WriteLine($"Warning: Failed to check Syncfusion license env var: {ex.Message}");
                }

                // Execute the script with an explicit timeout watcher. Some script calls (e.g., Task.Delay without observing
                // an external token) may not respond to cancellation immediately, so use Task.WhenAny to reliably detect timeouts.
                var evalTask = CSharpScript.EvaluateAsync(codeToExecute, scriptOptions, cancellationToken: cts.Token);
                var finished = await Task.WhenAny(evalTask, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds)));
                if (finished != evalTask)
                {
                    try { cts.Cancel(); } catch { }
                    var durationElapsed = DateTime.UtcNow - startTime;
                    var sbTimeout = new System.Text.StringBuilder();
                    sbTimeout.AppendLine($"❌ Test FAILED (Timeout after {timeoutSeconds}s): {testDescription}");
                    sbTimeout.AppendLine();
                    sbTimeout.AppendLine($"Duration: {durationElapsed.TotalMilliseconds:F2}ms");
                    sbTimeout.AppendLine();
                    if (output.Length > 0)
                    {
                        sbTimeout.AppendLine("Partial Output:");
                        sbTimeout.AppendLine(output.ToString());
                        sbTimeout.AppendLine();
                    }
                    return sbTimeout.ToString();
                }

                var result = await evalTask;
                var duration = DateTime.UtcNow - startTime;

                var resultSb = new System.Text.StringBuilder();
                resultSb.AppendLine($"✅ Test PASSED: {testDescription}");
                resultSb.AppendLine();
                resultSb.AppendLine($"Duration: {duration.TotalMilliseconds:F2}ms");
                resultSb.AppendLine();

                if (output.Length > 0)
                {
                    resultSb.AppendLine("Output:");
                    resultSb.AppendLine(output.ToString());
                    resultSb.AppendLine();
                }

                resultSb.AppendLine("Result:");
                if (result != null)
                {
                    resultSb.AppendLine($"  Type: {result.GetType().Name}");
                    resultSb.AppendLine($"  Value: {result}");
                }
                else
                {
                    resultSb.AppendLine("  (no return value)");
                }

                return resultSb.ToString();
            }
            catch (CompilationErrorException compilationEx)
            {
                var duration = DateTime.UtcNow - startTime;
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"❌ Test FAILED (Compilation): {testDescription}");
                sb.AppendLine();
                sb.AppendLine($"Duration: {duration.TotalMilliseconds:F2}ms");
                sb.AppendLine();
                sb.AppendLine("Compilation Errors:");
                foreach (var diagnostic in compilationEx.Diagnostics)
                {
                    sb.AppendLine($"  {diagnostic}");
                }
                if (output.Length > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("Partial Output:");
                    sb.AppendLine(output.ToString());
                }
                return sb.ToString();
            }
            catch (OperationCanceledException)
            {
                var duration = DateTime.UtcNow - startTime;
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"❌ Test FAILED (Timeout after {timeoutSeconds}s): {testDescription}");
                sb.AppendLine();
                sb.AppendLine($"Duration: {duration.TotalMilliseconds:F2}ms");
                if (output.Length > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("Partial Output:");
                    sb.AppendLine(output.ToString());
                }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - startTime;
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"❌ Test FAILED (Runtime): {testDescription}");
                sb.AppendLine();
                sb.AppendLine($"Duration: {duration.TotalMilliseconds:F2}ms");
                sb.AppendLine();
                sb.AppendLine($"Error: {ex.Message}");
                sb.AppendLine();
                sb.AppendLine("Stack Trace:");
                sb.AppendLine(ex.StackTrace ?? string.Empty);
                if (output.Length > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("Partial Output:");
                    sb.AppendLine(output.ToString());
                }
                return sb.ToString();
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalError);
            }
        }
        catch (Exception ex)
        {
            return $"❌ Error: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}";
        }
    }

    private static string? FindWorkspaceRoot()
    {
        var current = Directory.GetCurrentDirectory();
        while (current != null)
        {
            if (File.Exists(Path.Combine(current, "WileyWidget.sln")))
            {
                return current;
            }
            current = Directory.GetParent(current)?.FullName;
        }
        return null;
    }
}
