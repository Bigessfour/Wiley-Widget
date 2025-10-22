using System;
using System.Threading;
using System.Windows;
using System.Runtime.InteropServices;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using WileyWidget;

namespace WileyWidget;

/// <summary>
/// Application entry point with proper STA threading model for WPF applications.
/// This class provides the proper threading model and initialization sequence
/// for hosting WPF within the .NET Generic Host pattern.
/// 
/// Based on Microsoft Documentation:
/// - https://learn.microsoft.com/en-us/dotnet/desktop/wpf/app-development/application-management-overview
/// - https://learn.microsoft.com/en-us/dotnet/api/system.stathreadattribute
/// </summary>
public static class Program
{
    /// <summary>
    /// Application entry point. The STAThreadAttribute is required for WPF applications
    /// as per Microsoft documentation: "WPF uses the single-threaded apartment (STA) threading model."
    /// </summary>
    /// <param name="args">Command-line arguments</param>
    /// <returns>Exit code (0 for success)</returns>
    [STAThread]
    public static int Main(string[] args)
    {
        var startupStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var startupId = Guid.NewGuid().ToString("N")[..8];

        try
        {
            Log.Information("=== WileyWidget Application Startup - Session: {StartupId} ===", startupId);
            Log.Information("Command line arguments: {Args}", string.Join(" ", args ?? Array.Empty<string>()));
            Log.Information("Process ID: {ProcessId}, CLR Version: {ClrVersion}", Environment.ProcessId, Environment.Version);
            Log.Information("OS: {OSVersion}, Architecture: {Architecture}", Environment.OSVersion, RuntimeInformation.OSArchitecture);

            // Create bootstrap logger for early startup diagnostics
            // Microsoft pattern: Bootstrap logger first, then full configuration after host builder
            var bootstrapLogger = new LoggerConfiguration()
                .MinimumLevel.Debug()  // Capture all levels from the start
                .Enrich.WithMachineName()
                .Enrich.WithProcessId()
                .Enrich.WithThreadId()
                .Enrich.WithEnvironmentName()
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss.fff}] [{Level:u3}] {ProcessId}:{ThreadId} {SourceContext} {Message:lj}{NewLine}{Exception}")
                .WriteTo.File("logs/startup-.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {MachineName} {ProcessId}:{ThreadId} {SourceContext} {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            Log.Logger = bootstrapLogger;

            Log.Debug("Bootstrap logger created successfully");
            Log.Information("Starting bootstrap test - Session: {StartupId}", startupId);

            // Enhanced bootstrap test with timing and diagnostics
            var bootstrapTestStopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                // Test basic system capabilities
                var testThread = Thread.CurrentThread;
                Log.Debug("Bootstrap test - Thread diagnostics: ID={ThreadId}, Name={ThreadName}, Priority={Priority}",
                    testThread.ManagedThreadId, testThread.Name ?? "unnamed", testThread.Priority);

                // Test apartment state
                var apartmentState = testThread.GetApartmentState();
                Log.Debug("Bootstrap test - Apartment state: {ApartmentState}", apartmentState);

                // Test memory and GC
                GC.Collect();
                Log.Debug("Bootstrap test - GC collection completed, Memory: {MemoryMB}MB",
                    GC.GetTotalMemory(false) / 1024 / 1024);

                bootstrapTestStopwatch.Stop();
                Log.Information("Bootstrap test PASSED in {ElapsedMs}ms - Session: {StartupId}",
                    bootstrapTestStopwatch.ElapsedMilliseconds, startupId);
            }
            catch (Exception ex)
            {
                bootstrapTestStopwatch.Stop();
                Log.Error(ex, "Bootstrap test FAILED after {ElapsedMs}ms - Session: {StartupId}",
                    bootstrapTestStopwatch.ElapsedMilliseconds, startupId);
                throw; // Re-throw to fail startup
            }

            Log.Information("Starting WileyWidget application with proper STA threading model - Session: {StartupId}", startupId);
            Log.Information("Thread ID: {ThreadId}, IsBackground: {IsBackground}, ApartmentState: {ApartmentState}",
                Environment.CurrentManagedThreadId,
                Thread.CurrentThread.IsBackground,
                Thread.CurrentThread.GetApartmentState());

            // Additional thread diagnostics
            Log.Debug("Thread diagnostics - ManagedThreadId: {ThreadId}, Name: {ThreadName}, Priority: {Priority}, IsPool: {IsPool}, IsAlive: {IsAlive}",
                Thread.CurrentThread.ManagedThreadId,
                Thread.CurrentThread.Name ?? "unnamed",
                Thread.CurrentThread.Priority,
                Thread.CurrentThread.IsThreadPoolThread,
                Thread.CurrentThread.IsAlive);

            // Log thread pool status
            int workerThreads, completionPortThreads;
            ThreadPool.GetAvailableThreads(out workerThreads, out completionPortThreads);
            int maxWorkerThreads, maxCompletionPortThreads;
            ThreadPool.GetMaxThreads(out maxWorkerThreads, out maxCompletionPortThreads);

            Log.Debug("Thread pool status - Available Workers: {AvailableWorkers}/{MaxWorkers}, Available IO: {AvailableIO}/{MaxIO}",
                workerThreads, maxWorkerThreads, completionPortThreads, maxCompletionPortThreads);

            // Ensure we're on STA thread (this should already be the case due to STAThread attribute)
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            {
                Log.Warning("Current thread is not STA - this may cause WPF threading issues. Expected STA, got {ActualApartmentState}",
                    Thread.CurrentThread.GetApartmentState());
                Log.Warning("Thread details - ManagedThreadId: {ThreadId}, Name: {ThreadName}",
                    Thread.CurrentThread.ManagedThreadId, Thread.CurrentThread.Name ?? "unnamed");
            }
            else
            {
                Log.Debug("STA thread verification passed - ApartmentState: {ApartmentState}", ApartmentState.STA);
            }

            Log.Information("Creating WPF Application instance - Session: {StartupId}", startupId);
            var appCreationStopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Create and run the WPF application with proper STA threading
            // Prism applications don't need InitializeComponent() when using code-only setup
            var app = new App();
            appCreationStopwatch.Stop();
            Log.Information("WPF Application instance created successfully in {ElapsedMs}ms - Session: {StartupId}",
                appCreationStopwatch.ElapsedMilliseconds, startupId);

            // Start the application - Prism will handle initialization
            Log.Information("Starting WPF application run loop - Session: {StartupId}", startupId);
            startupStopwatch.Stop();
            Log.Information("Total startup preparation completed in {TotalElapsedMs}ms - Session: {StartupId}",
                startupStopwatch.ElapsedMilliseconds, startupId);

            var exitCode = app.Run();

            Log.Information("Application exited with code: {ExitCode} - Session: {StartupId}", exitCode, startupId);
            Log.Information("=== Application Shutdown Complete - Session: {StartupId} ===", startupId);
            return exitCode;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application startup failed - Session: {StartupId}. Exception Type: {ExceptionType}, Message: {Message}",
                startupId, ex.GetType().Name, ex.Message);
            Log.Fatal("Startup failure details - Elapsed time before failure: {ElapsedMs}ms", startupStopwatch.ElapsedMilliseconds);
            Log.Fatal("System state at failure - Thread: {ThreadId}, Memory: {MemoryMB}MB",
                Environment.CurrentManagedThreadId, GC.GetTotalMemory(false) / 1024 / 1024);

            // Try to flush logs before exit
            try
            {
                Log.CloseAndFlush();
            }
            catch (Exception flushEx)
            {
                // Last resort logging to console
                Console.Error.WriteLine($"CRITICAL: Failed to flush logs during startup failure: {flushEx.Message}");
            }

            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
