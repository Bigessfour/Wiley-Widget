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
        string? formTypeName = null)
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

            // Configure script options
            var scriptOptions = ScriptOptions.Default
                .WithReferences(typeof(System.Windows.Forms.Form).Assembly)
                .WithReferences(typeof(Syncfusion.WinForms.Controls.SfForm).Assembly)
                .WithReferences(typeof(WileyWidget.WinForms.Forms.MainForm).Assembly)
                .WithReferences(typeof(WileyWidget.McpServer.Helpers.SyncfusionTestHelper).Assembly)
                .WithImports(
                    "System",
                    "System.Windows.Forms",
                    "System.Linq",
                    "Syncfusion.WinForms.Controls",
                    "Syncfusion.WinForms.DataGrid",
                    "WileyWidget.WinForms.Forms",
                    "WileyWidget.McpServer.Helpers");

            // Execute script
            var startTime = DateTime.UtcNow;
            try
            {
                var result = await CSharpScript.EvaluateAsync(codeToExecute, scriptOptions);
                var duration = DateTime.UtcNow - startTime;

                var resultText = $"✅ Test PASSED: {testDescription}\n\n";
                resultText += $"Duration: {duration.TotalMilliseconds:F2}ms\n";

                if (result != null)
                {
                    resultText += $"\nResult: {result}";
                }

                return resultText;
            }
            catch (CompilationErrorException compilationEx)
            {
                var duration = DateTime.UtcNow - startTime;
                var errorText = $"❌ Test FAILED (Compilation): {testDescription}\n\n";
                errorText += $"Duration: {duration.TotalMilliseconds:F2}ms\n\n";
                errorText += "Compilation Errors:\n";
                foreach (var diagnostic in compilationEx.Diagnostics)
                {
                    errorText += $"  {diagnostic}\n";
                }

                return errorText;
            }
            catch (Exception testEx)
            {
                var duration = DateTime.UtcNow - startTime;
                var errorText = $"❌ Test FAILED (Runtime): {testDescription}\n\n";
                errorText += $"Duration: {duration.TotalMilliseconds:F2}ms\n\n";
                errorText += $"Error: {testEx.Message}\n\n";
                errorText += $"Stack Trace:\n{testEx.StackTrace}";

                return errorText;
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