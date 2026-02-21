using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Microsoft.SemanticKernel;

namespace WileyWidget.Services.Plugins.System
{
    /// <summary>
    /// Provides tools for the AI agent to introspect its own codebase, specifically focusing on AI and Semantic Kernel components.
    /// This allows the agent to understand its own capabilities and architecture.
    /// </summary>
    public class CodebaseInsightPlugin
    {
        private const int MaxFileLengthChars = 20000;

        /// <summary>
        /// Locates the source root by walking up from the base directory.
        /// </summary>
        private string? GetSourceRoot()
        {
            var current = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(current))
            {
                if (Directory.Exists(Path.Combine(current, "src")) && File.Exists(Path.Combine(current, "WileyWidget.sln")))
                {
                    return Path.Combine(current, "src");
                }
                var parent = Directory.GetParent(current);
                if (parent == null) break;
                current = parent.FullName;
            }
            return null;
        }

        [KernelFunction]
        [Description("Lists all AI-related source files in the Wiley Widget solution (WinForms and Services).")]
        public string ListAiArchitectureFiles()
        {
            var srcRoot = GetSourceRoot();
            if (srcRoot == null) return "Error: Could not locate 'src' folder. Ensure the application is running in a development environment.";

            var relevantPaths = new[]
            {
                global::System.IO.Path.Combine(srcRoot, "WileyWidget.Services", "Plugins"),
                global::System.IO.Path.Combine(srcRoot, "WileyWidget.Services", "AI"),
                global::System.IO.Path.Combine(srcRoot, "WileyWidget.WinForms", "Services", "AI")
            };

            var sb = new global::System.Text.StringBuilder();
            sb.AppendLine("Wiley Widget AI Architecture Files:");

            foreach (var path in relevantPaths)
            {
                if (global::System.IO.Directory.Exists(path))
                {
                    var files = global::System.IO.Directory.GetFiles(path, "*.cs", global::System.IO.SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        var relativePath = global::System.IO.Path.GetRelativePath(srcRoot, file);
                        sb.AppendLine($"- {relativePath}");
                    }
                }
            }

            if (sb.Length == 0) return "No AI-related files found.";
            return sb.ToString();
        }

        [KernelFunction]
        [Description("Reads the content of a specific AI-related source file.")]
        public string ReadAiSourceFile(
            [Description("The relative path of the file (e.g. 'WileyWidget.Services/Plugins/System/TimePlugin.cs').")] string relativePath)
        {
            var srcRoot = GetSourceRoot();
            if (srcRoot == null) return "Error: Source root not found.";

            // Normalize separators
            relativePath = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

            // Security check: ensure path is within srcRoot and only allows reading .cs files
            var fullPath = Path.GetFullPath(Path.Combine(srcRoot, relativePath));
            if (!fullPath.StartsWith(srcRoot, StringComparison.OrdinalIgnoreCase))
                return "Error: Access denied. Path is outside source root.";

            if (!fullPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                return "Error: Access denied. Only .cs source files can be read.";

            if (!File.Exists(fullPath))
                return $"Error: File not found at {relativePath}";

            try
            {
                var content = File.ReadAllText(fullPath);
                if (content.Length > MaxFileLengthChars)
                {
                    return $"File is too large ({content.Length} chars). Truncated content:\n" + content.Substring(0, MaxFileLengthChars) + "\n...[truncated]";
                }
                return content;
            }
            catch (Exception ex)
            {
                return $"Error reading file: {ex.Message}";
            }
        }
    }
}
