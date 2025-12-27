using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol;
using WileyWidget.McpServer.Tools;
using Syncfusion.Licensing;

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
            // Command-line convenience: if a recognized tool is supplied, run it one-shot and exit.
            // Register Syncfusion license if provided via environment to avoid interactive dialogs when instantiating controls
            var sfLicense = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY") ?? Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE");
            if (!string.IsNullOrWhiteSpace(sfLicense))
            {
                try
                {
                    SyncfusionLicenseProvider.RegisterLicense(sfLicense);
                    Console.WriteLine("Syncfusion license registered from environment variable SYNCFUSION_LICENSE_KEY");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Warning: Failed to register Syncfusion license: {ex.Message}");
                }
            }

            if (args != null && args.Length > 0)
            {
                var toolName = args[0];
                if (string.Equals(toolName, "RunHeadlessFormTest", StringComparison.OrdinalIgnoreCase))
                {
                    string? scriptPath = null;
                    string? testCode = null;
                    string? formTypeName = null;
                    int timeoutSeconds = 60;
                    for (int i = 1; i < args.Length; i++)
                    {
                        if (args[i] == "--scriptPath" && i + 1 < args.Length) scriptPath = args[++i];
                        else if (args[i] == "--testCode" && i + 1 < args.Length) testCode = args[++i];
                        else if (args[i] == "--formTypeName" && i + 1 < args.Length) formTypeName = args[++i];
                        else if (args[i] == "--timeoutSeconds" && i + 1 < args.Length && int.TryParse(args[++i], out var parsed)) timeoutSeconds = parsed;
                    }

                    var result = await WileyWidget.McpServer.Tools.RunHeadlessFormTestTool.RunHeadlessFormTest(scriptPath, testCode, formTypeName, timeoutSeconds);
                    if (string.IsNullOrEmpty(result))
                    {
                        Console.WriteLine("(no output from tool)");
                    }
                    else
                    {
                        Console.WriteLine(result);
                    }

                    return 0;
                }

                // Other ad-hoc one-shot tools may be handled here in future
            }

            // Create empty application builder (no console output noise for STDIO transport)
            Console.WriteLine("Starting MCP server (stdio transport)");
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
}
