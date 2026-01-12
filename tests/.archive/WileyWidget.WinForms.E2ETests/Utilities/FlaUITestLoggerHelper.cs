using System.Globalization;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using System;
using System.IO;

namespace WileyWidget.WinForms.E2ETests.Utilities;

/// <summary>
/// Helper class for configuring Serilog logging in FlaUI UI automation tests.
/// Writes logs to both test output and dedicated test log files for debugging.
/// </summary>
public static class FlaUITestLoggerHelper
{
    private static readonly string TestLogsPath = GetRepoRootLogsPath();

    /// <summary>
    /// Gets the repo root logs folder path.
    /// Walks up from test bin/Debug/net10.0/... to repo root.
    /// </summary>
    private static string GetRepoRootLogsPath()
    {
        var baseDir = Directory.GetCurrentDirectory();
        var repoRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
        var logsPath = Path.Combine(repoRoot, "logs");
        Directory.CreateDirectory(logsPath);
        return logsPath;
    }

    /// <summary>
    /// Creates a Serilog logger configured for FlaUI UI automation tests.
    /// Logs to both console and a test-specific log file.
    /// </summary>
    /// <param name="testName">Name of the test (used in log file name)</param>
    /// <param name="minimumLevel">Minimum log level (default: Debug)</param>
    /// <returns>Configured Serilog ILogger</returns>
    public static Serilog.ILogger CreateFlaUITestLogger(
        string testName,
        LogEventLevel minimumLevel = LogEventLevel.Debug)
    {
        if (testName == null) throw new ArgumentNullException(nameof(testName));

        Directory.CreateDirectory(TestLogsPath);

        var sanitizedTestName = SanitizeFileName(testName);
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var logFilePath = Path.Combine(TestLogsPath, $"{sanitizedTestName}_{timestamp}.log");

        return new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
            .WriteTo.File(
                logFilePath,
                formatProvider: CultureInfo.InvariantCulture,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .Enrich.FromLogContext()
            .Enrich.WithProperty("TestName", testName)
            .CreateLogger();
    }

    /// <summary>
    /// Creates a Microsoft.Extensions.Logging.ILoggerFactory configured for FlaUI tests.
    /// </summary>
    /// <param name="testName">Name of the test</param>
    /// <param name="minimumLevel">Minimum log level (default: Debug)</param>
    /// <returns>Configured ILoggerFactory</returns>
    public static ILoggerFactory CreateFlaUILoggerFactory(
        string testName,
        Microsoft.Extensions.Logging.LogLevel minimumLevel = Microsoft.Extensions.Logging.LogLevel.Debug)
    {
        var serilogLogger = CreateFlaUITestLogger(testName, ConvertLogLevel(minimumLevel));

        return LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(minimumLevel);
            builder.AddSerilog(serilogLogger, dispose: true);
        });
    }

    /// <summary>
    /// Creates a strongly-typed ILogger&lt;T&gt; for use in FlaUI tests.
    /// </summary>
    /// <typeparam name="T">Type to associate with the logger</typeparam>
    /// <param name="testName">Name of the test</param>
    /// <param name="minimumLevel">Minimum log level (default: Debug)</param>
    /// <returns>Configured ILogger&lt;T&gt;</returns>
    public static ILogger<T> CreateFlaUILogger<T>(
        string testName,
        Microsoft.Extensions.Logging.LogLevel minimumLevel = Microsoft.Extensions.Logging.LogLevel.Debug)
    {
        using var factory = CreateFlaUILoggerFactory(testName, minimumLevel);
        return factory.CreateLogger<T>();
    }

    /// <summary>
    /// Configures the global Serilog.Log.Logger for FlaUI test scenarios.
    /// Call this at the start of each test.
    /// IMPORTANT: Call Serilog.Log.CloseAndFlush() at test end to ensure all logs are written.
    /// </summary>
    /// <param name="testName">Name of the test</param>
    /// <param name="minimumLevel">Minimum log level (default: Debug)</param>
    public static void ConfigureGlobalFlaUILogger(
        string testName,
        LogEventLevel minimumLevel = LogEventLevel.Debug)
    {
        Log.Logger = CreateFlaUITestLogger(testName, minimumLevel);
    }

    /// <summary>
    /// Logs an automation action (button click, text entry, etc.) with timing.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="action">Action description</param>
    /// <param name="elementName">UI element name</param>
    public static void LogAutomationAction(Serilog.ILogger logger, string action, string elementName)
    {
        if (logger == null) throw new ArgumentNullException(nameof(logger));

        logger.Information("UI Automation: {Action} on element '{ElementName}'", action, elementName);
    }

    /// <summary>
    /// Logs an automation assertion with expected vs actual values.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="assertionType">Type of assertion (e.g., "Text matches", "Element visible")</param>
    /// <param name="expected">Expected value</param>
    /// <param name="actual">Actual value</param>
    /// <param name="success">Whether assertion passed</param>
    public static void LogAutomationAssertion(
        Serilog.ILogger logger,
        string assertionType,
        object expected,
        object actual,
        bool success)
    {
        if (logger == null) throw new ArgumentNullException(nameof(logger));

        if (success)
        {
            logger.Information("Assertion PASSED: {AssertionType} - Expected: {Expected}, Actual: {Actual}",
                assertionType, expected, actual);
        }
        else
        {
            logger.Error("Assertion FAILED: {AssertionType} - Expected: {Expected}, Actual: {Actual}",
                assertionType, expected, actual);
        }
    }

    /// <summary>
    /// Logs test timing information.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="phaseName">Test phase (e.g., "Setup", "Execution", "Teardown")</param>
    /// <param name="elapsedMs">Elapsed milliseconds</param>
    public static void LogTestTiming(Serilog.ILogger logger, string phaseName, long elapsedMs)
    {
        if (logger == null) throw new ArgumentNullException(nameof(logger));

        logger.Information("Test Phase '{Phase}' completed in {ElapsedMs}ms", phaseName, elapsedMs);
    }

    /// <summary>
    /// Gets the path to the test logs directory.
    /// Useful for attaching log files to test reports.
    /// </summary>
    /// <returns>Full path to test logs directory</returns>
    public static string GetTestLogsPath() => TestLogsPath;

    /// <summary>
    /// Converts Microsoft.Extensions.Logging.LogLevel to Serilog.Events.LogEventLevel.
    /// </summary>
    private static LogEventLevel ConvertLogLevel(Microsoft.Extensions.Logging.LogLevel logLevel)
    {
        return logLevel switch
        {
            Microsoft.Extensions.Logging.LogLevel.Trace => LogEventLevel.Verbose,
            Microsoft.Extensions.Logging.LogLevel.Debug => LogEventLevel.Debug,
            Microsoft.Extensions.Logging.LogLevel.Information => LogEventLevel.Information,
            Microsoft.Extensions.Logging.LogLevel.Warning => LogEventLevel.Warning,
            Microsoft.Extensions.Logging.LogLevel.Error => LogEventLevel.Error,
            Microsoft.Extensions.Logging.LogLevel.Critical => LogEventLevel.Fatal,
            _ => LogEventLevel.Information
        };
    }

    /// <summary>
    /// Sanitizes a test name for use in file names.
    /// </summary>
    private static string SanitizeFileName(string testName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", testName.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        return sanitized.Length > 100 ? sanitized.Substring(0, 100) : sanitized;
    }
}

/// <summary>
/// Example usage class demonstrating FlaUI test logging patterns.
/// </summary>
/// <example>
/// <code>
/// [TestFixture]
/// public class MainFormUITests
/// {
///     private Serilog.ILogger _logger;
///
///     [SetUp]
///     public void Setup()
///     {
///         var testName = TestContext.CurrentContext.Test.Name;
///         _logger = FlaUITestLoggerHelper.CreateFlaUITestLogger(testName);
///         _logger.Information("Starting test: {TestName}", testName);
///     }
///
///     [TearDown]
///     public void TearDown()
///     {
///         _logger.Information("Test completed");
///         Log.CloseAndFlush();
///     }
///
///     [Test]
///     public void Test_ClickButton()
///     {
///         var sw = Stopwatch.StartNew();
///
///         // Automation code
///         FlaUITestLoggerHelper.LogAutomationAction(_logger, "Click", "LoadDataButton");
///         button.Click();
///
///         // Assertion
///         FlaUITestLoggerHelper.LogAutomationAssertion(_logger, "Button enabled", true, button.IsEnabled, true);
///
///         sw.Stop();
///         FlaUITestLoggerHelper.LogTestTiming(_logger, "Button click test", sw.ElapsedMilliseconds);
///     }
/// }
/// </code>
/// </example>
internal static class FlaUITestLoggerExamples
{
    // Example patterns documented in XML doc above
}
