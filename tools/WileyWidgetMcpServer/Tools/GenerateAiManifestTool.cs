using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace WileyWidget.McpServer.Tools;

/// <summary>
/// MCP tool to generate the AI fetchable manifest by invoking the repository's Python generator script.
/// - Runs scripts/generate-ai-manifest.py using the available Python executable
/// - Returns the generated manifest contents or an informative error message
/// </summary>
[McpServerToolType]
public static class GenerateAiManifestTool
{
    [McpServerTool]
    [Description("Run the repository's AI manifest generator (scripts/generate-ai-manifest.py) and return the generated manifest content.")]
    public static async Task<string> GenerateAiManifest(
        [Description("Path to generator script relative to workspace root (default: scripts/generate-ai-manifest.py)")] string scriptPath = "scripts/generate-ai-manifest.py",
        [Description("Output manifest file path relative to workspace root (default: ai-fetchable-manifest.json)")] string outputPath = "ai-fetchable-manifest.json",
        [Description("Python executable to use (default: python). The tool will attempt common alternatives if this is not found.)")] string pythonExe = "python",
        [Description("Timeout in seconds for the generator process (default: 60)")] int timeoutSeconds = 60)
    {
        try
        {
            var workspaceRoot = FindWorkspaceRoot() ?? Directory.GetCurrentDirectory();
            var scriptFullPath = Path.Combine(workspaceRoot, scriptPath);
            if (!File.Exists(scriptFullPath))
            {
                return $"❌ Generator script not found: {scriptPath} (expected at {scriptFullPath})";
            }

            // Try to find a usable python binary
            var python = await FindPythonExecutableAsync(pythonExe);
            if (string.IsNullOrEmpty(python))
            {
                return "❌ Python executable not found on PATH. Please install Python 3.14+ or provide the path via the 'pythonExe' parameter.";
            }

            var psi = new ProcessStartInfo
            {
                FileName = python,
                Arguments = $"\"{scriptFullPath}\"",
                WorkingDirectory = workspaceRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var sbOut = new StringBuilder();
            var sbErr = new StringBuilder();

            using var process = new Process { StartInfo = psi };
            process.OutputDataReceived += (s, e) => { if (e.Data != null) sbOut.AppendLine(e.Data); };
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) sbErr.AppendLine(e.Data); };

            var started = process.Start();
            if (!started)
            {
                return "❌ Failed to start Python process.";
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var exited = process.WaitForExit(timeoutSeconds * 1000);
            if (!exited)
            {
                try { process.Kill(); } catch { }
                return $"❌ Generator timed out after {timeoutSeconds} seconds.\nStdout:\n{sbOut}\nStderr:\n{sbErr}";
            }

            var exitCode = process.ExitCode;

            if (exitCode != 0)
            {
                return $"❌ Generator failed (exit code {exitCode}).\nStdout:\n{sbOut}\nStderr:\n{sbErr}";
            }

            // Ensure manifest exists
            var outputFullPath = Path.Combine(workspaceRoot, outputPath);
            if (!File.Exists(outputFullPath))
            {
                return $"❌ Generator finished but manifest not found at {outputPath}.\nStdout:\n{sbOut}\nStderr:\n{sbErr}";
            }

            // Read and return manifest content
            var content = await File.ReadAllTextAsync(outputFullPath);
            return content;
        }
        catch (Exception ex)
        {
            return $"❌ Error running generator: {ex.Message}\n{ex.StackTrace}";
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

    private static async Task<string?> FindPythonExecutableAsync(string preferred)
    {
        // Try the preferred first
        if (await IsCommandAvailableAsync(preferred)) return preferred;

        // Common alternates
        var candidates = new[] { "python3", "py" };
        foreach (var candidate in candidates)
        {
            if (await IsCommandAvailableAsync(candidate)) return candidate;
        }

        return null;
    }

    private static async Task<bool> IsCommandAvailableAsync(string cmd)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = cmd,
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return false;
            var exited = process.WaitForExit(5000);
            if (!exited)
            {
                try { process.Kill(); } catch { }
                return false;
            }

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
