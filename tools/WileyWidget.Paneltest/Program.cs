using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using Serilog;
using WileyWidget.WinForms.Controls;
using WileyWidget.Paneltest.Fixtures;
using WileyWidget.Paneltest.TestCases;

namespace WileyWidget.Paneltest;

/// <summary>
/// Panel Test Harness - Standalone application for isolated panel testing and rendering.
///
/// Usage:
///   WileyWidget.Paneltest.exe [panel-name] [--show] [--theme THEME_NAME]
///
/// Examples:
///   WileyWidget.Paneltest.exe list
///   WileyWidget.Paneltest.exe warroom --show
/// </summary>
internal class Program
{
    private static readonly Dictionary<string, Type> RegisteredPanelTests = new()
    {
        ["warroom"] = typeof(WarRoomPanelTestCase),
    };

    [STAThread]
    private static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // Initialize Serilog
        var logsDirectory = "Logs";
        if (!Directory.Exists(logsDirectory))
        {
            Directory.CreateDirectory(logsDirectory);
        }

        var logFile = Path.Combine(logsDirectory, $"paneltest-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss.fff}] [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(logFile, outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        try
        {
            Log.Information("════════════════════════════════════════════════════════════════════");
            Log.Information("  WileyWidget.Paneltest Starting");
            Log.Information("  Log file: {LogFile}", logFile);
            Log.Information("════════════════════════════════════════════════════════════════════");

            if (args.Length == 0)
            {
                PrintUsage();
                return 0;
            }

            string command = args[0].ToLower(CultureInfo.InvariantCulture);
            Log.Debug("Command: {Command}", command);

            var result = command switch
            {
                "list" => CommandListPanels(),
                "warroom" or "wr" => CommandRunPanel("warroom", args),
                "--help" or "-h" or "help" => PrintUsageAndReturn(),
                _ => PrintUnknownPanelAndReturn(command)
            };

            Log.Information("════════════════════════════════════════════════════════════════════");
            Log.Information("  WileyWidget.Paneltest Completed (Exit Code: {ExitCode})", result);
            Log.Information("════════════════════════════════════════════════════════════════════");
            return result;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "FATAL ERROR");
            Console.Error.WriteLine($"FATAL: {ex.Message}");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static int CommandListPanels()
    {
        Log.Information("Listing available panels...");
        Console.WriteLine("Available panels:");
        foreach (var (name, type) in RegisteredPanelTests)
        {
            Console.WriteLine($"  {name,-20} ({type.Name})");
        }
        Console.WriteLine("\nUsage: WileyWidget.Paneltest.exe <panel> [--show]");
        return 0;
    }

    private static int CommandRunPanel(string panelName, string[] args)
    {
        Log.Information("Running panel: {PanelName}", panelName);

        if (!RegisteredPanelTests.TryGetValue(panelName, out var testType))
        {
            Log.Error("Panel not registered: {PanelName}", panelName);
            return 1;
        }

        bool showForm = args.Contains("--show");
        Log.Information("Configuration: ShowForm={ShowForm}", showForm);

        Console.WriteLine($"[PANELTEST] {panelName} (Show={showForm})");
        var result = RunTest(testType, showForm);
        return result ? 0 : 1;
    }

    private static bool RunTest(Type testType, bool showForm)
    {
        try
        {
            Log.Debug("Creating test instance: {TestType}", testType.Name);
            var test = (BasePanelTestCase)Activator.CreateInstance(testType)!;

            object? capturedViewModel = null;
            using (test)
            {
                Log.Debug("Rendering panel...");
                test.RenderPanel(showForm);

                // Capture ViewModel immediately after rendering, before any disposal
                capturedViewModel = test.GetViewModel();

                Log.Debug("Panel rendered successfully (ViewModel captured: {HasViewModel})", capturedViewModel != null);

                Log.Debug("Asserting panel initialized...");
                test.AssertPanelInitialized();  // Check ViewModel BEFORE panel disposal
                Log.Information("Test PASSED");

                return true;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Test FAILED");
            Console.WriteLine($"✗ ERROR: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private static int PrintUsageAndReturn()
    {
        PrintUsage();
        return 0;
    }

    private static int PrintUnknownPanelAndReturn(string command)
    {
        Console.WriteLine($"Unknown panel: {command}");
        return 1;
    }

    private static void PrintUsage()
    {
        Console.WriteLine(@"
╔════════════════════════════════════════════════════════════════════╗
║   WileyWidget.Paneltest - Isolated Panel Rendering Test Harness    ║
╚════════════════════════════════════════════════════════════════════╝

USAGE:
  WileyWidget.Paneltest.exe <command> [options]

COMMANDS:
  list              List available test panels
  warroom, wr       Test WarRoomPanel
  help              Show this message

OPTIONS:
  --show            Display form during test (interactive)

EXAMPLES:
  WileyWidget.Paneltest.exe list
  WileyWidget.Paneltest.exe warroom --show

OUTPUT:
  Results: Results/test-results.json
  Logs:    Logs/paneltest-YYYYMMDD-HHMMSS.log

FEATURES:
  ✓ Isolated panel testing (no full app launch)
  ✓ Mock services & sample data
  ✓ STA thread support for WinForms
  ✓ Comprehensive logging (console + file)
  ✓ JSON test report generation
");
    }
}
