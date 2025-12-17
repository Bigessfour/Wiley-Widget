using Microsoft.Extensions.Logging;
using Xunit.Abstractions;
using Serilog;
using Serilog.Events;

namespace WileyWidget.WinForms.Tests.Utilities;

/// <summary>
/// Helper class for configuring Serilog logging in XUnit tests.
/// Forwards Serilog output to XUnit's ITestOutputHelper for test diagnostics.
/// </summary>
public static class XUnitTestLoggerHelper
{
    /// <summary>
    /// Creates a Serilog logger configured to output to XUnit test output.
    /// </summary>
    /// <param name="output">XUnit test output helper</param>
    /// <param name="minimumLevel">Minimum log level (default: Debug)</param>
    /// <returns>Configured Serilog ILogger</returns>
    public static Serilog.ILogger CreateXUnitTestLogger(ITestOutputHelper output, LogEventLevel minimumLevel = LogEventLevel.Debug)
    {
        return new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .WriteTo.TestOutput(output, outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}", formatProvider: System.Globalization.CultureInfo.InvariantCulture)
            .CreateLogger();
    }

    /// <summary>
    /// Creates a Microsoft.Extensions.Logging.ILoggerFactory configured for XUnit tests.
    /// Use this to create ILogger&lt;T&gt; instances for dependency injection in tests.
    /// </summary>
    /// <param name="output">XUnit test output helper</param>
    /// <param name="minimumLevel">Minimum log level (default: Debug)</param>
    /// <returns>Configured ILoggerFactory</returns>
    public static ILoggerFactory CreateXUnitLoggerFactory(
        ITestOutputHelper output,
        Microsoft.Extensions.Logging.LogLevel minimumLevel = Microsoft.Extensions.Logging.LogLevel.Debug)
    {
        var serilogLogger = CreateXUnitTestLogger(output, ConvertLogLevel(minimumLevel));

        return LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(minimumLevel);
            builder.AddSerilog(serilogLogger, dispose: true);
        });
    }

    /// <summary>
    /// Creates a strongly-typed ILogger&lt;T&gt; for use in XUnit tests.
    /// </summary>
    /// <typeparam name="T">Type to associate with the logger</typeparam>
    /// <param name="output">XUnit test output helper</param>
    /// <param name="minimumLevel">Minimum log level (default: Debug)</param>
    /// <returns>Configured ILogger&lt;T&gt;</returns>
    public static ILogger<T> CreateXUnitLogger<T>(
        ITestOutputHelper output,
        Microsoft.Extensions.Logging.LogLevel minimumLevel = Microsoft.Extensions.Logging.LogLevel.Debug)
    {
#pragma warning disable CA2000 // ILoggerFactory should be disposed, but loggers created from it are used throughout test lifetime
        var factory = CreateXUnitLoggerFactory(output, minimumLevel);
#pragma warning restore CA2000
        return factory.CreateLogger<T>();
    }

    /// <summary>
    /// Configures the global Serilog.Log.Logger for XUnit test scenarios.
    /// Call this in test constructor or [SetUp] method.
    /// IMPORTANT: Call Serilog.Log.CloseAndFlush() in test cleanup to avoid resource leaks.
    /// </summary>
    /// <param name="output">XUnit test output helper</param>
    /// <param name="minimumLevel">Minimum log level (default: Debug)</param>
    public static void ConfigureGlobalXUnitLogger(ITestOutputHelper output, LogEventLevel minimumLevel = LogEventLevel.Debug)
    {
        Log.Logger = CreateXUnitTestLogger(output, minimumLevel);
    }

    /// <summary>
    /// Writes a log message directly to XUnit TestOutput.
    /// Use this for quick diagnostic messages without full Serilog setup.
    /// </summary>
    /// <param name="output">XUnit test output helper</param>
    /// <param name="message">Message to write</param>
    public static void WriteToXUnit(ITestOutputHelper output, string message)
    {
        if (output is null)
        {
            throw new ArgumentNullException(nameof(output));
        }

        output.WriteLine(message);
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
/// Example usage class demonstrating XUnit test logging patterns.
/// </summary>
/// <example>
/// <code>
/// public class MyServiceTests
/// {
///     private readonly ITestOutputHelper _output;
///     private ILogger&lt;MyService&gt; _logger;
///
///     public MyServiceTests(ITestOutputHelper output)
///     {
///         _output = output;
///         _logger = XUnitTestLoggerHelper.CreateXUnitLogger&lt;MyService&gt;(_output);
///     }
///
///     [Fact]
///     public void TestMethod()
///     {
///         // Logger output will appear in XUnit test output
///         var service = new MyService(_logger);
///         // ... test logic
///     }
/// }
/// </code>
/// </example>
internal static class XUnitTestLoggerExamples
{
    // Example patterns documented in XML doc above
}
