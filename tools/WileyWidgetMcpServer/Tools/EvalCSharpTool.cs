using ModelContextProtocol.Server;
using System.ComponentModel;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System.Text;

namespace WileyWidget.McpServer.Tools;

/// <summary>
/// MCP tool for dynamic evaluation of C# code snippets.
/// Provides rapid prototyping, debugging, and UI validation capabilities without recompilation.
/// </summary>
[McpServerToolType]
public static class EvalCSharpTool
{
    [McpServerTool]
    [Description("Evaluates C# code dynamically without compilation. Perfect for rapid UI/control validation, exploratory testing, theme checks, and mock-driven debugging of WileyWidget forms and Syncfusion controls.")]
    public static async Task<string> EvalCSharp(
        [Description("C# code to execute. Can instantiate forms, inspect Syncfusion controls, verify properties, and run assertions.")]
        string csx,
        [Description("Optional: Full path to a .csx file to execute instead of inline code")]
        string? csxFile = null,
        [Description("Maximum execution time in seconds (default: 30)")]
        int timeoutSeconds = 30)
    {
        var startTime = DateTime.UtcNow;
        var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            string codeToExecute;

            // Load from file if specified, otherwise use inline code
            if (!string.IsNullOrEmpty(csxFile))
            {
                if (!File.Exists(csxFile))
                {
                    return $"❌ Error: File not found: {csxFile}";
                }

                codeToExecute = await File.ReadAllTextAsync(csxFile, cancellationTokenSource.Token);
            }
            else if (!string.IsNullOrEmpty(csx))
            {
                codeToExecute = csx;
            }
            else
            {
                return "❌ Error: Either 'csx' or 'csxFile' parameter must be provided.";
            }

            // Configure script options with WileyWidget references
            var scriptOptions = ScriptOptions.Default
                .WithReferences(typeof(System.Windows.Forms.Form).Assembly)
                .WithReferences(typeof(Syncfusion.WinForms.Controls.SfForm).Assembly)
                .WithReferences(typeof(Syncfusion.WinForms.DataGrid.SfDataGrid).Assembly)
                .WithReferences(typeof(Syncfusion.WinForms.Themes.Office2019Theme).Assembly) // Theme support
                .WithReferences(typeof(Syncfusion.WinForms.Controls.SfSkinManager).Assembly) // SfSkinManager
                .WithReferences(typeof(WileyWidget.WinForms.Forms.MainForm).Assembly)
                .WithReferences(typeof(WileyWidget.McpServer.Helpers.SyncfusionTestHelper).Assembly)
                .WithReferences(typeof(Moq.Mock).Assembly)
                .WithImports(
                    "System",
                    "System.Collections.Generic",
                    "System.Linq",
                    "System.Windows.Forms",
                    "System.Drawing",
                    "Syncfusion.WinForms.Controls",
                    "Syncfusion.WinForms.DataGrid",
                    "Syncfusion.WinForms.Themes",
                    "WileyWidget.WinForms.Forms",
                    "WileyWidget.WinForms.Themes",
                    "WileyWidget.McpServer.Helpers",
                    "Moq");

            // Capture stdout/stderr
            var output = new StringBuilder();
            var originalOut = Console.Out;
            var originalError = Console.Error;

            try
            {
                var stringWriter = new StringWriter(output);
                Console.SetOut(stringWriter);
                Console.SetError(stringWriter);

                // Execute script
                var result = await CSharpScript.EvaluateAsync(
                    codeToExecute,
                    scriptOptions,
                    cancellationToken: cancellationTokenSource.Token);

                var duration = DateTime.UtcNow - startTime;

                // Build result
                var resultText = new StringBuilder();
                resultText.AppendLine("✅ Execution Successful");
                resultText.AppendLine($"Duration: {duration.TotalMilliseconds:F2}ms");
                resultText.AppendLine();

                if (output.Length > 0)
                {
                    resultText.AppendLine("Output:");
                    resultText.AppendLine(output.ToString());
                    resultText.AppendLine();
                }

                if (result != null)
                {
                    resultText.AppendLine("Return Value:");
                    resultText.AppendLine($"  Type: {result.GetType().Name}");
                    resultText.AppendLine($"  Value: {result}");
                }

                return resultText.ToString();
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalError);
            }
        }
        catch (CompilationErrorException compilationEx)
        {
            var duration = DateTime.UtcNow - startTime;
            var errorText = new StringBuilder();
            errorText.AppendLine("❌ Compilation Error");
            errorText.AppendLine($"Duration: {duration.TotalMilliseconds:F2}ms");
            errorText.AppendLine();
            errorText.AppendLine("Errors:");

            foreach (var diagnostic in compilationEx.Diagnostics)
            {
                errorText.AppendLine($"  Line {diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1}: {diagnostic.GetMessage()}");
            }

            return errorText.ToString();
        }
        catch (OperationCanceledException)
        {
            return $"❌ Execution Timeout: Code exceeded {timeoutSeconds} second limit.";
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            var errorText = new StringBuilder();
            errorText.AppendLine("❌ Runtime Error");
            errorText.AppendLine($"Duration: {duration.TotalMilliseconds:F2}ms");
            errorText.AppendLine();
            errorText.AppendLine($"Error: {ex.Message}");
            errorText.AppendLine();
            errorText.AppendLine("Stack Trace:");
            errorText.AppendLine(ex.StackTrace);

            return errorText.ToString();
        }
        finally
        {
            cancellationTokenSource.Dispose();
        }
    }
}