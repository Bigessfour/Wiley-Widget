using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace WileyWidget.WinForms.Plugins
{
    /// <summary>
    /// Semantic Kernel plugin that evaluates C# code dynamically using Roslyn scripting.
    /// Provides JARVIS with the ability to write and execute C# code to inspect application state,
    /// perform calculations, and interact with the runtime environment.
    /// WILEY WIDGET SPECIFIC: Can access MainForm and live application state.
    /// </summary>
    public sealed class CSharpEvaluationPlugin
    {
        private readonly ILogger<CSharpEvaluationPlugin> _logger;
        private const int DefaultTimeoutSeconds = 30;
        private const int MaxCodeLength = 100_000; // 100KB max code

        /// <summary>
        /// Globals available to scripts for contextual execution.
        /// </summary>
        public class ScriptGlobals
        {
            /// <summary>
            /// Logger for script output and diagnostics.
            /// </summary>
            public ILogger? Logger { get; set; }

            /// <summary>
            /// Additional context data that can be injected by the caller.
            /// </summary>
            public Dictionary<string, object> Context { get; set; } = new();

            /// <summary>
            /// Wiley-specific: Access to running MainForm instance (use carefully!).
            /// Example: var title = MainForm?.Text; // "Wiley Widget"
            /// </summary>
            public Form? MainForm => Application.OpenForms
                .Cast<Form>()
                .FirstOrDefault(f => f.GetType().Name == "MainForm");
        }

        public CSharpEvaluationPlugin(ILogger<CSharpEvaluationPlugin> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [KernelFunction("evaluate_csharp")]
        [Description("Evaluate C# code dynamically and return JSON result. Use this to perform calculations, inspect types, access assemblies, or execute C# expressions in Wiley Widget's runtime. The code runs in a sandboxed Roslyn environment with common namespaces pre-imported. Returns structured JSON: {success, result?, error?, elapsedMs}.")]
        [return: Description("JSON object with execution result: {success: bool, result?: any, error?: string, details?: string, elapsedMs?: number}")]
        public async Task<string> EvaluateCSharpAsync(
            [Description("C# code to evaluate. Can be a single expression or multiple statements. The last expression will be returned as the result. Example: 'AppDomain.CurrentDomain.GetAssemblies().Length' or 'var x = 5; return x * x;'")]
            string code,
            [Description("Optional: Timeout in seconds (default: 30, max: 300). Script will be terminated if it exceeds this time.")]
            int timeoutSeconds = DefaultTimeoutSeconds,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("[CSharpEval] ▶ Starting evaluation - code length: {CodeLength} chars, timeout: {Timeout}s",
                code?.Length ?? 0, timeoutSeconds);

            // ===== INPUT VALIDATION =====
            if (string.IsNullOrWhiteSpace(code))
            {
                _logger.LogWarning("[CSharpEval] ❌ Rejected: empty code");
                return JsonSerializer.Serialize(new { success = false, error = "Code cannot be empty." });
            }

            if (code.Length > MaxCodeLength)
            {
                _logger.LogWarning("[CSharpEval] ❌ Rejected: code too large ({Length} > {Max})", code.Length, MaxCodeLength);
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Code exceeds max length of {MaxCodeLength} chars (provided: {code.Length})."
                });
            }

            // Clamp timeout to safe range
            timeoutSeconds = Math.Clamp(timeoutSeconds, 1, 300);

            // ===== SECURITY SCAN =====
            var securityIssues = CheckForSecurityRisks(code);
            if (securityIssues.Any())
            {
                var risksString = string.Join(", ", securityIssues);
                _logger.LogWarning("[CSharpEval] 🔒 Security risks detected: {Risks}", risksString);
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Security risks detected in code",
                    details = $"Forbidden patterns: {risksString}"
                });
            }

            _logger.LogDebug("[CSharpEval] ✅ Security scan passed");

            // ===== SCRIPT SETUP =====
            var scriptOptions = ScriptOptions.Default
                .WithImports(GetCommonNamespaces())
                .WithReferences(GetCommonAssemblies())
                .WithOptimizationLevel(Microsoft.CodeAnalysis.OptimizationLevel.Release);

            var globals = new ScriptGlobals
            {
                Logger = _logger
            };

            _logger.LogDebug("[CSharpEval] 🔧 Script options configured - {NamespaceCount} namespaces, {AssemblyCount} assemblies",
                GetCommonNamespaces().Length, GetCommonAssemblies().Length);

            // ===== EXECUTION WITH TIMEOUT =====
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            try
            {

                var stopwatch = Stopwatch.StartNew();
                _logger.LogDebug("[CSharpEval] ⏱️ Starting script execution...");

                // Use CSharpScript.EvaluateAsync for single-expression evaluation
                // This is more efficient than Create + RunAsync for most JARVIS queries
                var scriptTask = CSharpScript.EvaluateAsync(
                    code,
                    scriptOptions,
                    globals,
                    cancellationToken: cts.Token);

                // Proper async timeout pattern with Task.WhenAny
                var timeoutTask = Task.Delay(Timeout.Infinite, cts.Token);
                var completedTask = await Task.WhenAny(scriptTask, timeoutTask);

                if (completedTask == scriptTask)
                {
                    // Script completed before timeout
                    var result = await scriptTask; // Will not block - already completed
                    stopwatch.Stop();

                    _logger.LogInformation("[CSharpEval] ✅ Execution succeeded in {ElapsedMs}ms",
                        stopwatch.ElapsedMilliseconds);

                    // Return structured JSON with result
                    return JsonSerializer.Serialize(new
                    {
                        success = true,
                        result = FormatResultForJson(result),
                        elapsedMs = stopwatch.ElapsedMilliseconds,
                        resultType = result?.GetType().Name ?? "null"
                    });
                }
                else
                {
                    // Timeout occurred
                    stopwatch.Stop();
                    _logger.LogWarning("[CSharpEval] ⏱️ Timeout after {ElapsedMs}ms (limit: {TimeoutMs}ms)",
                        stopwatch.ElapsedMilliseconds, timeoutSeconds * 1000);

                    return JsonSerializer.Serialize(new
                    {
                        success = false,
                        error = $"Execution timed out after {timeoutSeconds}s",
                        elapsedMs = stopwatch.ElapsedMilliseconds
                    });
                }
            }
            catch (CompilationErrorException ex)
            {
                _logger.LogError(ex, "[CSharpEval] ❌ Compilation failed");
                var errorDetails = ex.Diagnostics
                    .Select(d => $"Line {d.Location.GetLineSpan().StartLinePosition.Line + 1}: {d.GetMessage()}")
                    .ToArray();

                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Compilation error",
                    details = errorDetails
                });
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("[CSharpEval] 🛑 Execution cancelled by user");
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Execution cancelled by user"
                });
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("[CSharpEval] ⏱️ Execution timed out (cancellation)");
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Execution timed out after {timeoutSeconds}s"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CSharpEval] ❌ Runtime error: {ExceptionType}", ex.GetType().Name);
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Runtime error",
                    details = $"{ex.GetType().Name}: {ex.Message}",
                    stackTrace = ex.StackTrace?.Split('\n').Take(5).ToArray() // First 5 lines only
                });
            }
        }

        /// <summary>
        /// Checks code for dangerous patterns that could harm the system.
        /// Wiley-specific: Blocks file deletion, process spawning, unsafe code.
        /// </summary>
        private static List<string> CheckForSecurityRisks(string code)
        {
            var risks = new List<string>();

            // Forbidden patterns that could cause harm in Wiley Widget context
            var forbiddenPatterns = new Dictionary<string, string>
            {
                { "Process.Start", "process execution" },
                { "File.Delete", "file deletion" },
                { "Directory.Delete", "directory deletion" },
                { "File.WriteAllText", "file writing" },
                { "File.WriteAllBytes", "file writing" },
                { "Assembly.LoadFile", "assembly loading" },
                { "Assembly.LoadFrom", "assembly loading" },
                { "Unsafe", "unsafe code" },
                { "System.Runtime.CompilerServices.Unsafe", "unsafe code" },
                { "System.Reflection.Emit", "dynamic code emission" },
                { "System.CodeDom.Compiler", "code compilation" },
                { "Registry.SetValue", "registry modification" },
                { "Environment.Exit", "application termination" },
                { "Application.Exit", "application termination" }
            };

            foreach (var (pattern, description) in forbiddenPatterns)
            {
                if (code.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    risks.Add($"{description} ({pattern})");
                }
            }

            return risks;
        }

        /// <summary>
        /// Format script result for JSON serialization.
        /// Handles common types and prevents serialization errors.
        /// </summary>
        private static object? FormatResultForJson(object? value)
        {
            if (value == null) return null;

            var type = value.GetType();

            // Handle primitives and strings directly
            if (type.IsPrimitive || value is string || value is decimal)
                return value;

            // Handle DateTime
            if (value is DateTime dt)
                return dt.ToString("O"); // ISO 8601

            // Handle collections (limit to 100 items to prevent huge payloads)
            if (value is System.Collections.IEnumerable enumerable and not string)
            {
                var items = enumerable.Cast<object>().Take(100).Select(FormatResultForJson).ToArray();
                return new { items, count = items.Length, truncated = enumerable.Cast<object>().Count() > 100 };
            }

            // For complex objects, return ToString() representation
            return value.ToString();
        }

        [KernelFunction("list_loaded_assemblies")]
        [Description("List all assemblies currently loaded in the application domain. Useful for understanding what types and namespaces are available for C# evaluation.")]
        [return: Description("Formatted text listing all loaded assemblies with versions, or JSON with assembly details.")]
        public string ListLoadedAssemblies()
        {
            _logger.LogDebug("[CSharpEval] Listing loaded assemblies...");

            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => !a.IsDynamic)
                    .OrderBy(a => a.GetName().Name)
                    .Select(a =>
                    {
                        var name = a.GetName();
                        var version = name.Version?.ToString() ?? "n/a";
                        return $"• {name.Name} (v{version})";
                    })
                    .ToList();

                var sb = new StringBuilder();
                sb.AppendLine($"📦 Loaded Assemblies ({assemblies.Count} total):");
                sb.AppendLine();
                foreach (var assembly in assemblies.Take(100)) // Limit to first 100 to avoid overwhelming output
                {
                    sb.AppendLine(assembly);
                }

                if (assemblies.Count > 100)
                {
                    sb.AppendLine($"... and {assemblies.Count - 100} more");
                }

                _logger.LogInformation("[CSharpEval] Listed {Count} assemblies", assemblies.Count);
                return sb.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CSharpEval] Error listing assemblies");
                return $"❌ Error: {ex.Message}";
            }
        }

        [KernelFunction("inspect_type")]
        [Description("Inspect a .NET type by name and return its members, properties, methods, and fields. Use this to understand the structure of a type before writing C# code.")]
        [return: Description("Formatted text with type details including interfaces, properties, methods, and fields.")]
        public string InspectType(
            [Description("Fully qualified type name (e.g., 'System.String', 'WileyWidget.WinForms.Forms.MainForm'). Can also use partial name for fuzzy matching.")]
            string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return "❌ Error: Type name cannot be empty.";
            }

            _logger.LogDebug("[CSharpEval] Inspecting type: {TypeName}", typeName);

            try
            {
                // Try to find the type in all loaded assemblies
                Type? type = null;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = assembly.GetType(typeName, throwOnError: false);
                    if (type != null) break;
                }

                if (type == null)
                {
                    // Try fuzzy match
                    _logger.LogDebug("[CSharpEval] Type not found, trying fuzzy match...");
                    var matches = AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(a => a.GetTypes())
                        .Where(t => t.FullName?.Contains(typeName, StringComparison.OrdinalIgnoreCase) ?? false)
                        .Take(10)
                        .ToList();

                    if (matches.Count > 0)
                    {
                        var suggestions = string.Join("\n", matches.Select(t => $"  • {t.FullName}"));
                        return $"❌ Type '{typeName}' not found. Did you mean:\n{suggestions}";
                    }

                    return $"❌ Type '{typeName}' not found in any loaded assembly.";
                }

                var sb = new StringBuilder();
                sb.AppendLine($"🔍 Type: {type.FullName}");
                sb.AppendLine($"   Assembly: {type.Assembly.GetName().Name}");
                sb.AppendLine($"   Namespace: {type.Namespace}");
                sb.AppendLine($"   Base Type: {type.BaseType?.Name ?? "None"}");
                sb.AppendLine();

                // Interfaces
                var interfaces = type.GetInterfaces();
                if (interfaces.Length > 0)
                {
                    sb.AppendLine($"📋 Interfaces ({interfaces.Length}):");
                    foreach (var iface in interfaces.Take(20))
                    {
                        sb.AppendLine($"   • {iface.Name}");
                    }
                    sb.AppendLine();
                }

                // Properties
                var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                if (properties.Length > 0)
                {
                    sb.AppendLine($"📝 Properties ({properties.Length}):");
                    foreach (var prop in properties.Take(30))
                    {
                        var accessibility = prop.GetMethod?.IsPublic == true ? "public" : "protected";
                        var staticMod = prop.GetMethod?.IsStatic == true ? "static " : "";
                        sb.AppendLine($"   • {accessibility} {staticMod}{prop.PropertyType.Name} {prop.Name}");
                    }
                    sb.AppendLine();
                }

                // Methods
                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Where(m => !m.IsSpecialName) // Exclude property getters/setters
                    .ToArray();

                if (methods.Length > 0)
                {
                    sb.AppendLine($"🔧 Methods ({methods.Length}):");
                    foreach (var method in methods.Take(30))
                    {
                        var accessibility = method.IsPublic ? "public" : "protected";
                        var staticMod = method.IsStatic ? "static " : "";
                        var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                        sb.AppendLine($"   • {accessibility} {staticMod}{method.ReturnType.Name} {method.Name}({parameters})");
                    }
                    sb.AppendLine();
                }

                // Fields
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                if (fields.Length > 0)
                {
                    sb.AppendLine($"📦 Fields ({fields.Length}):");
                    foreach (var field in fields.Take(20))
                    {
                        var accessibility = field.IsPublic ? "public" : "protected";
                        var staticMod = field.IsStatic ? "static " : "";
                        sb.AppendLine($"   • {accessibility} {staticMod}{field.FieldType.Name} {field.Name}");
                    }
                }

                _logger.LogInformation("[CSharpEval] Inspected type: {TypeName} ({MemberCount} members)",
                    type.FullName, properties.Length + methods.Length + fields.Length);

                return sb.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CSharpEval] Error inspecting type '{TypeName}'", typeName);
                return $"❌ Error: {ex.Message}";
            }
        }

        [KernelFunction("get_environment_info")]
        [Description("Get information about the current runtime environment (OS, .NET version, process info, memory usage, etc.). Useful for diagnostics and debugging in Wiley Widget.")]
        [return: Description("Formatted text with comprehensive environment and process information.")]
        public string GetEnvironmentInfo()
        {
            _logger.LogDebug("[CSharpEval] Getting environment info...");

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("🖥️ Runtime Environment:");
                sb.AppendLine();
                sb.AppendLine($"   OS: {Environment.OSVersion}");
                sb.AppendLine($"   .NET Version: {Environment.Version}");
                sb.AppendLine($"   64-bit OS: {Environment.Is64BitOperatingSystem}");
                sb.AppendLine($"   64-bit Process: {Environment.Is64BitProcess}");
                sb.AppendLine($"   Processor Count: {Environment.ProcessorCount}");
                sb.AppendLine($"   User: {Environment.UserName}");
                sb.AppendLine($"   Machine: {Environment.MachineName}");
                sb.AppendLine($"   Current Directory: {Environment.CurrentDirectory}");
                sb.AppendLine($"   System Directory: {Environment.SystemDirectory}");
                sb.AppendLine();

                var process = Process.GetCurrentProcess();
                sb.AppendLine($"📊 Process Info:");
                sb.AppendLine($"   Process Name: {process.ProcessName}");
                sb.AppendLine($"   Process ID: {process.Id}");
                sb.AppendLine($"   Working Set: {process.WorkingSet64 / 1024 / 1024:N0} MB");
                sb.AppendLine($"   Private Memory: {process.PrivateMemorySize64 / 1024 / 1024:N0} MB");
                sb.AppendLine($"   Threads: {process.Threads.Count}");
                sb.AppendLine($"   Started: {process.StartTime:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"   Uptime: {DateTime.Now - process.StartTime:hh\\:mm\\:ss}");

                _logger.LogInformation("[CSharpEval] Environment info retrieved - {ProcessName} PID {ProcessId}",
                    process.ProcessName, process.Id);

                return sb.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CSharpEval] Error getting environment info");
                return $"❌ Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Get common assemblies to reference in scripts.
        /// Includes .NET core assemblies + WileyWidget.WinForms for local type access.
        /// </summary>
        private static Assembly[] GetCommonAssemblies()
        {
            return new[]
            {
                typeof(object).Assembly,                    // System.Private.CoreLib
                typeof(Console).Assembly,                   // System.Console
                typeof(System.Linq.Enumerable).Assembly,    // System.Linq
                typeof(System.Collections.Generic.List<>).Assembly, // System.Collections
                typeof(System.Text.StringBuilder).Assembly, // System.Text
                typeof(System.IO.File).Assembly,            // System.IO
                typeof(System.Net.Http.HttpClient).Assembly, // System.Net.Http
                typeof(System.Threading.Tasks.Task).Assembly, // System.Threading
                typeof(Form).Assembly,                      // System.Windows.Forms
                Assembly.GetExecutingAssembly()             // Current assembly (WileyWidget.WinForms)
            };
        }

        /// <summary>
        /// Get common namespaces to import in scripts.
        /// Pre-imports frequently used namespaces so JARVIS doesn't need full qualification.
        /// </summary>
        private static string[] GetCommonNamespaces()
        {
            return new[]
            {
                "System",
                "System.Collections.Generic",
                "System.Linq",
                "System.Text",
                "System.Threading.Tasks",
                "System.IO",
                "System.Diagnostics",
                "System.Windows.Forms",
                "Microsoft.Extensions.Logging"
            };
        }
    }
}
