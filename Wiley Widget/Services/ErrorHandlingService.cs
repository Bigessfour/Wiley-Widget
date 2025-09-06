using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Serilog;
using System.Threading.Tasks;

namespace WileyWidget.Services;

/// <summary>
/// Service for handling application errors and providing graceful degradation.
/// Manages global exception handling and user-friendly error reporting.
/// </summary>
public class ErrorHandlingService
{
    /// <summary>
    /// Configures global exception handling for the application.
    /// </summary>
    public void ConfigureGlobalExceptionHandling()
    {
        // Configure unhandled exception handling
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            var exception = e.ExceptionObject as Exception;
            Log.Fatal(exception, "💥 CRITICAL: Unhandled exception in AppDomain");
        };

        // Configure dispatcher unhandled exception handling
        Application.Current.DispatcherUnhandledException += (sender, e) =>
        {
            Log.Error(e.Exception, "💥 CRITICAL: Unhandled exception in Dispatcher");
            e.Handled = true; // Prevent application crash
        };

        // Configure task unhandled exception handling
        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            Log.Warning(e.Exception, "⚠️ Unobserved task exception");
            e.SetObserved(); // Prevent exception escalation
        };

        Log.Information("✅ Global exception handling configured");
    }

    /// <summary>
    /// Handles startup failures with graceful degradation.
    /// </summary>
    /// <param name="ex">The exception that caused the startup failure.</param>
    public void HandleStartupFailure(Exception ex)
    {
        try
        {
            Log.Error(ex, "💥 STARTUP FAILURE: Attempting graceful degradation");

            // Try to show a basic error window instead of crashing
            var errorWindow = new Window
            {
                Title = "Startup Error - Wiley Widget",
                Width = 400,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            var errorText = new TextBlock
            {
                Text = $"Application failed to start properly.\n\nError: {ex.Message}\n\nPlease check the logs for more details.",
                Margin = new Thickness(20),
                TextWrapping = TextWrapping.Wrap
            };

            var closeButton = new Button
            {
                Content = "Close",
                Margin = new Thickness(20),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom
            };

            closeButton.Click += (s, e) => errorWindow.Close();

            var grid = new Grid();
            grid.Children.Add(errorText);
            grid.Children.Add(closeButton);

            errorWindow.Content = grid;
            errorWindow.Show();

            // Don't call Shutdown() here - let the error window handle it
        }
        catch (Exception fallbackEx)
        {
            Log.Fatal(fallbackEx, "💥 CRITICAL: Even graceful degradation failed");
            // Last resort - force shutdown
            Application.Current.Shutdown(1);
        }
    }

    /// <summary>
    /// Safely executes an initialization step with error handling and logging.
    /// </summary>
    /// <param name="stepName">Name of the initialization step for logging.</param>
    /// <param name="action">The action to execute.</param>
    public void SafeExecute(string stepName, Action action)
    {
        try
        {
            Log.Information("🔧 Executing startup step: {Step}", stepName);
            action();
            Log.Information("✅ Startup step completed: {Step}", stepName);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ Startup step failed: {Step} - {Message}", stepName, ex.Message);
            // Continue with other steps - don't crash the entire startup
        }
    }

    /// <summary>
    /// Safely executes an asynchronous initialization step with error handling and logging.
    /// </summary>
    /// <param name="stepName">Name of the initialization step for logging.</param>
    /// <param name="action">The asynchronous action to execute.</param>
    public async Task SafeExecuteAsync(string stepName, Func<Task> action)
    {
        try
        {
            Log.Information("🔧 Executing async startup step: {Step}", stepName);
            await action();
            Log.Information("✅ Async startup step completed: {Step}", stepName);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ Async startup step failed: {Step} - {Message}", stepName, ex.Message);
            // Continue with other steps - don't crash the entire startup
        }
    }
}
