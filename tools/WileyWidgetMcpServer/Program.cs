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
}