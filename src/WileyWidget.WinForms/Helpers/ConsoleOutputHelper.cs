using System;

namespace WileyWidget.WinForms.Helpers;

/// <summary>
/// Helper to suppress Console.WriteLine in headless/MCP mode.
/// Checks environment variables to determine if we're in headless mode.
/// </summary>
public static class ConsoleOutputHelper
{
    private static readonly bool _isHeadless = IsHeadlessMode();

    /// <summary>
    /// Determines if we're running in headless mode (MCP server, unit tests, etc.)
    /// </summary>
    private static bool IsHeadlessMode()
    {
        // Check for MCP server environment variables
        if (Environment.GetEnvironmentVariable("SYNCFUSION_SILENT_LICENSE_VALIDATION") == "true")
            return true;

        // Check for headless environment variables
        var headlessVar = Environment.GetEnvironmentVariable("HEADLESS_MODE");
        if (headlessVar == "true" || headlessVar == "1")
            return true;

        // Check if running in unit test context
        var testVar = Environment.GetEnvironmentVariable("DOTNET_TEST_MODE");
        if (testVar == "true" || testVar == "1")
            return true;

        return false;
    }

    /// <summary>
    /// Safely writes to console only if not in headless mode.
    /// In headless mode, use the logger instead.
    /// </summary>
    public static void WriteLineSafe(string message)
    {
        if (!_isHeadless)
        {
            Console.WriteLine(message);
        }
    }

    /// <summary>
    /// Safely writes formatted message to console only if not in headless mode.
    /// </summary>
    public static void WriteLineSafe(string format, params object?[] args)
    {
        if (!_isHeadless)
        {
            Console.WriteLine(format, args);
        }
    }
}
