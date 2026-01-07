using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol;
using WileyWidget.McpServer.Tools;

namespace WileyWidget.McpServer;

/// <summary>
/// MCP Server for WileyWidget UI Testing and Validation.
/// Exposes tools for headless form validation, Syncfusion control inspection, and theme compliance checks.
/// </summary>
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            // Pre-load WileyWidget and Syncfusion assemblies into AppDomain
            // This ensures they're available for Roslyn script compilation
            PreLoadAssemblies();

            // Allow a quick CLI helper to run the license check without starting the MCP server
            if (args != null && args.Length > 0 && args[0] == "--run-license-check")
            {
                var fmt = args.Length > 1 ? args[1] : "json";
                var output = WileyWidget.McpServer.Tools.ValidateSyncfusionLicenseTool.ValidateSyncfusionLicense(fmt);
                Console.WriteLine(output);
                return 0;
            }

            // Create empty application builder (no console output noise for STDIO transport)
            var builder = Host.CreateEmptyApplicationBuilder(settings: null);

            // Add MCP server with STDIO transport and tools from assembly
            builder.Services.AddMcpServer()
                .WithStdioServerTransport()
                .WithToolsFromAssembly();

            // Build and run the application
            var app = builder.Build();
            await app.RunAsync();

            return 0;
        }
        catch (Exception ex)
        {
            // Log to stderr (safe for STDIO transport)
            Console.Error.WriteLine($"MCP Server error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    /// <summary>
    /// Pre-load WileyWidget and Syncfusion assemblies into AppDomain.
    /// Essential for Roslyn script compilation to find types during evaluation.
    /// </summary>
    private static void PreLoadAssemblies()
    {
        try
        {
            var assemblyDir = Path.GetDirectoryName(typeof(Program).Assembly.Location) ?? AppContext.BaseDirectory;

            // Build a deterministic search list so AssemblyResolve can find dependencies
            var searchDirs = new[]
            {
                assemblyDir,
                AppContext.BaseDirectory,
                Directory.GetCurrentDirectory(),
                Path.Combine(Directory.GetCurrentDirectory(), "bin", "Debug", "net10.0-windows10.0.26100.0"),
                Path.Combine(Directory.GetCurrentDirectory(), "tools", "WileyWidgetMcpServer", "bin", "Debug", "net10.0-windows10.0.26100.0")
            }
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

            // Ensure AssemblyResolve can locate matching DLLs in the known search directories
            AppDomain.CurrentDomain.AssemblyResolve += (_, args) => ResolveAssembly(args, searchDirs);

            // Assembly names to pre-load
            var assemblyNames = new[]
            {
                "WileyWidget.WinForms.dll",
                "WileyWidget.Models.dll",
                "WileyWidget.Services.dll",
                "WileyWidget.Abstractions.dll",
                "WileyWidget.Business.dll",
                "WileyWidget.Data.dll",
                "Syncfusion.WinForms.Controls.dll",
                "Syncfusion.WinForms.DataGrid.dll",
                "Syncfusion.WinForms.Themes.dll",
                "Syncfusion.Windows.Forms.dll",
                "Syncfusion.Windows.Forms.Tools.dll",
                "Syncfusion.Drawing.dll",
                "Syncfusion.Core.dll",
                "Moq.dll"
            };

            foreach (var dllName in assemblyNames)
            {
                var assemblyPath = searchDirs
                    .Select(dir => Path.Combine(dir, dllName))
                    .FirstOrDefault(File.Exists);

                if (!string.IsNullOrEmpty(assemblyPath))
                {
                    try
                    {
                        System.Reflection.Assembly.LoadFrom(assemblyPath);
                    }
                    catch { /* Assembly may already be loaded */ }
                }
            }
        }
        catch
        {
            // Silently continue if pre-loading fails; some assemblies may not exist
        }

        static System.Reflection.Assembly? ResolveAssembly(ResolveEventArgs args, IReadOnlyCollection<string> searchDirs)
        {
            var requestedName = new System.Reflection.AssemblyName(args.Name).Name;
            if (string.IsNullOrWhiteSpace(requestedName))
            {
                return null;
            }

            var candidatePath = searchDirs
                .Select(dir => Path.Combine(dir, $"{requestedName}.dll"))
                .FirstOrDefault(File.Exists);

            return candidatePath != null
                ? System.Reflection.Assembly.LoadFrom(candidatePath)
                : null;
        }
    }
}
