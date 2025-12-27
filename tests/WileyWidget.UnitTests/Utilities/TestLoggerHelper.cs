using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Xunit.Abstractions;

namespace WileyWidget.WinForms.Tests.Utilities;

/// <summary>
/// Helper class for configuring Serilog logging in xUnit tests.
/// Forwards Serilog output to xUnit's ITestOutputHelper for test diagnostics.
/// </summary>
public static class TestLoggerHelper
{
    /// <summary>
    /// Creates a Serilog logger configured to output to xUnit test output.
    /// </summary>
    /// <param name="testOutputHelper">xUnit test output helper</param>
    /// <param name="minimumLevel">Minimum log level (default: Debug)</param>
    /// <returns>Configured Serilog ILogger</returns>
    public static Serilog.ILogger CreateTestLogger(
        ITestOutputHelper testOutputHelper,
        LogEventLevel minimumLevel = LogEventLevel.Debug)
    {
        return new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .WriteTo.TestOutput(testOutputHelper, formatProvider: System.Globalization.CultureInfo.InvariantCulture)
            .CreateLogger();
    }

    /// <summary>
    /// Creates a Microsoft.Extensions.Logging.ILoggerFactory configured for xUnit tests.
    /// Use this to create ILogger&lt;T&gt; instances for dependency injection in tests.
    /// </summary>
    /// <param name="testOutputHelper">xUnit test output helper</param>
    /// <param name="minimumLevel">Minimum log level (default: Debug)</param>
    /// <returns>Configured ILoggerFactory</returns>
    public static ILoggerFactory CreateTestLoggerFactory(
        ITestOutputHelper testOutputHelper,
        Microsoft.Extensions.Logging.LogLevel minimumLevel = Microsoft.Extensions.Logging.LogLevel.Debug)
    {
        var serilogLogger = CreateTestLogger(testOutputHelper, ConvertLogLevel(minimumLevel));

        return LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(minimumLevel);
            builder.AddSerilog(serilogLogger, dispose: true);
        });
    }

    /// <summary>
    /// Creates a strongly-typed ILogger&lt;T&gt; for use in tests.
    /// </summary>
    /// <typeparam name="T">Type to associate with the logger</typeparam>
    /// <param name="testOutputHelper">xUnit test output helper</param>
    /// <param name="minimumLevel">Minimum log level (default: Debug)</param>
    /// <returns>Configured ILogger&lt;T&gt;</returns>
    public static ILogger<T> CreateTestLogger<T>(
        ITestOutputHelper testOutputHelper,
        Microsoft.Extensions.Logging.LogLevel minimumLevel = Microsoft.Extensions.Logging.LogLevel.Debug)
    {
#pragma warning disable CA2000 // ILoggerFactory should be disposed, but loggers created from it are used throughout test lifetime
        var factory = CreateTestLoggerFactory(testOutputHelper, minimumLevel);
#pragma warning restore CA2000
        return factory.CreateLogger<T>();
    }

    /// <summary>
    /// Configures the global Serilog.Log.Logger for test scenarios.
    /// Call this in test constructor or setup method.
    /// IMPORTANT: Call Serilog.Log.CloseAndFlush() in test disposal to avoid resource leaks.
    /// </summary>
    /// <param name="testOutputHelper">xUnit test output helper</param>
    /// <param name="minimumLevel">Minimum log level (default: Debug)</param>
    public static void ConfigureGlobalTestLogger(
        ITestOutputHelper testOutputHelper,
        LogEventLevel minimumLevel = LogEventLevel.Debug)
    {
        Log.Logger = CreateTestLogger(testOutputHelper, minimumLevel);
    }

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
}

/// <summary>
/// Example usage class demonstrating test logging patterns.
/// </summary>
/// <example>
/// <code>
/// public class MyServiceTests
/// {
///     private readonly ITestOutputHelper _output;
///     private readonly ILogger&lt;MyService&gt; _logger;
///
///     public MyServiceTests(ITestOutputHelper output)
///     {
///         _output = output;
///         _logger = TestLoggerHelper.CreateTestLogger&lt;MyService&gt;(output);
///     }
///
///     [Fact]
///     public async Task TestMethod()
///     {
///         // Logger output will appear in xUnit test output
///         var service = new MyService(_logger);
///         await service.DoWorkAsync();
///     }
/// }
/// </code>
/// </example>
internal static class TestLoggerExamples
{
    // Example patterns documented in XML doc above
}
