using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Serilog;
using Serilog.Events;

namespace WileyWidget.WinForms.Tests.Utilities;

/// <summary>
/// Helper class for configuring Serilog logging in NUnit tests.
/// Forwards Serilog output to NUnit's TestContext for test diagnostics.
/// </summary>
public static class NUnitTestLoggerHelper
{
    /// <summary>
    /// Creates a Serilog logger configured to output to NUnit test output.
    /// </summary>
    /// <param name="minimumLevel">Minimum log level (default: Debug)</param>
    /// <returns>Configured Serilog ILogger</returns>
    public static Serilog.ILogger CreateNUnitTestLogger(LogEventLevel minimumLevel = LogEventLevel.Debug)
    {
        return new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .WriteTo.NUnitOutput()
            .CreateLogger();
    }

    /// <summary>
    /// Creates a Microsoft.Extensions.Logging.ILoggerFactory configured for NUnit tests.
    /// Use this to create ILogger&lt;T&gt; instances for dependency injection in tests.
    /// </summary>
    /// <param name="minimumLevel">Minimum log level (default: Debug)</param>
    /// <returns>Configured ILoggerFactory</returns>
    public static ILoggerFactory CreateNUnitLoggerFactory(
        Microsoft.Extensions.Logging.LogLevel minimumLevel = Microsoft.Extensions.Logging.LogLevel.Debug)
    {
        var serilogLogger = CreateNUnitTestLogger(ConvertLogLevel(minimumLevel));

        return LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(minimumLevel);
            builder.AddSerilog(serilogLogger, dispose: true);
        });
    }

    /// <summary>
    /// Creates a strongly-typed ILogger&lt;T&gt; for use in NUnit tests.
    /// </summary>
    /// <typeparam name="T">Type to associate with the logger</typeparam>
    /// <param name="minimumLevel">Minimum log level (default: Debug)</param>
    /// <returns>Configured ILogger&lt;T&gt;</returns>
    public static ILogger<T> CreateNUnitLogger<T>(
        Microsoft.Extensions.Logging.LogLevel minimumLevel = Microsoft.Extensions.Logging.LogLevel.Debug)
    {
        var factory = CreateNUnitLoggerFactory(minimumLevel);
        return factory.CreateLogger<T>();
    }

    /// <summary>
    /// Configures the global Serilog.Log.Logger for NUnit test scenarios.
    /// Call this in [SetUp] method.
    /// IMPORTANT: Call Serilog.Log.CloseAndFlush() in [TearDown] to avoid resource leaks.
    /// </summary>
    /// <param name="minimumLevel">Minimum log level (default: Debug)</param>
    public static void ConfigureGlobalNUnitLogger(LogEventLevel minimumLevel = LogEventLevel.Debug)
    {
        Log.Logger = CreateNUnitTestLogger(minimumLevel);
    }

    /// <summary>
    /// Writes a log message directly to NUnit TestContext.
    /// Use this for quick diagnostic messages without full Serilog setup.
    /// </summary>
    /// <param name="message">Message to write</param>
    public static void WriteToNUnit(string message)
    {
        TestContext.WriteLine(message);
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
/// Example usage class demonstrating NUnit test logging patterns.
/// </summary>
/// <example>
/// <code>
/// [TestFixture]
/// public class MyServiceTests
/// {
///     private ILogger&lt;MyService&gt; _logger;
///
///     [SetUp]
///     public void Setup()
///     {
///         _logger = NUnitTestLoggerHelper.CreateNUnitLogger&lt;MyService&gt;();
///     }
///
///     [TearDown]
///     public void TearDown()
///     {
///         Log.CloseAndFlush();
///     }
///
///     [Test]
///     public async Task TestMethod()
///     {
///         // Logger output will appear in NUnit test output
///         var service = new MyService(_logger);
///         await service.DoWorkAsync();
///     }
/// }
/// </code>
/// </example>
internal static class NUnitTestLoggerExamples
{
    // Example patterns documented in XML doc above
}
