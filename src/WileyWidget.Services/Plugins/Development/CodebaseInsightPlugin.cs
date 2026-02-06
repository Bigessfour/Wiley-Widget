using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;

namespace WileyWidget.Services.Plugins.Development
{
    /// <summary>
    /// Semantic Kernel plugin that provides introspection capabilities into the Wiley Widget codebase.
    /// Allows the AI to read its own source code to answer architectural questions or perform code analysis.
    /// </summary>
    public class CodebaseInsightPlugin
    {
        // Define safe root paths relative to the execution directory
        // In a typical .NET run (bin/Debug/...), the src/ root is usually a few levels up.
        // We will try to resolve the solution root dynamically.
        private readonly Lazy<string?> _solutionRoot = new Lazy<string?>(ResolveSolutionRoot);

        [KernelFunction]
        [Description("Returns a list of key files related to the AI/Semantic Kernel architecture in the project.")]
        [return: Description("A list of file paths relative to the solution root.")]
        public string GetAiArchitectureFiles()
        {
            var files = new List<string>
            {
                "src/WileyWidget.WinForms/Services/AI/GrokAgentService.cs",
                "src/WileyWidget.WinForms/Services/AI/KernelPluginRegistrar.cs",
                "src/WileyWidget.Services/Plugins/AnomalyDetectionPlugin.cs",
                "src/WileyWidget.Services/Plugins/System/TimePlugin.cs",
                "src/WileyWidget.Services/Plugins/Finance/QuickBooksPlugin.cs",
                "src/WileyWidget.Services/Plugins/Data/DataReportingPlugin.cs",
                "src/WileyWidget.Services/Plugins/Development/CodebaseInsightPlugin.cs"
            };

            return string.Join(Environment.NewLine, files);
        }

        [KernelFunction]
        [Description("Reads the content of a specific source code file.")]
        [return: Description("The text content of the file, or an error message.")]
        public string ReadSourceFile(
            [Description("The relative path to the file from the solution root (e.g., 'src/WileyWidget.Services/Plugins/AnomalyDetectionPlugin.cs')")] string relativeFilePath)
        {
            var root = _solutionRoot.Value;
            if (string.IsNullOrEmpty(root))
            {
                return "Error: Could not locate the solution root directory.";
            }

            // Sanitize path to prevent directory traversal attacks
            if (relativeFilePath.Contains(".."))
            {
                return "Error: Directory traversal ('..') is not allowed.";
            }

            var fullPath = Path.Combine(root, relativeFilePath);
            if (!File.Exists(fullPath))
            {
                return $"Error: File not found at {fullPath}";
            }

            // Basic extension check for safety
            var ext = Path.GetExtension(fullPath).ToLowerInvariant();
            var allowedExtensions = new[] { ".cs", ".json", ".md", ".xml", ".txt", ".csproj" };
            if (!allowedExtensions.Contains(ext))
            {
                return $"Error: File type '{ext}' is not allowed for reading.";
            }

            try
            {
                // Read file content
                var content = File.ReadAllText(fullPath);

                // Truncate if extremely large (e.g., > 20KB) to save tokens, though typical source files are small.
                if (content.Length > 20000)
                {
                    return content.Substring(0, 20000) + "\n...[TRUNCATED due to length]...";
                }

                return content;
            }
            catch (Exception ex)
            {
                return $"Error reading file: {ex.Message}";
            }
        }

        [KernelFunction]
        [Description("Lists files in a specific directory within the codebase.")]
        [return: Description("A list of file paths found in the directory.")]
        public string ListProjectFiles(
            [Description("The relative path to the directory (e.g., 'src/WileyWidget.Services/Plugins')")] string relativeDirPath)
        {
            var root = _solutionRoot.Value;
            if (string.IsNullOrEmpty(root))
            {
                return "Error: Could not locate the solution root directory.";
            }

            if (relativeDirPath.Contains(".."))
            {
                return "Error: Directory traversal ('..') is not allowed.";
            }

            var fullDirPath = Path.Combine(root, relativeDirPath);
            if (!Directory.Exists(fullDirPath))
            {
                return $"Error: Directory not found at {fullDirPath}";
            }

            try
            {
                var files = Directory.GetFiles(fullDirPath);
                // Convert back to relative paths for the AI
                var relativeFiles = files.Select(f => Path.GetRelativePath(root, f)).ToList();
                return string.Join(Environment.NewLine, relativeFiles);
            }
            catch (Exception ex)
            {
                return $"Error listing directory: {ex.Message}";
            }
        }

        private static string? ResolveSolutionRoot()
        {
            // Start from current base directory and look up for .sln file
            var current = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(current))
            {
                if (Directory.GetFiles(current, "*.sln").Any())
                {
                    return current;
                }
                current = Directory.GetParent(current)?.FullName;
            }
            return null; // Should not happen in a valid dev environment run
        }
    }
}
