using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using Xunit;
using WileyWidget.UI.Tests.Infrastructure;
using FlaUIApp = FlaUI.Core.Application;

namespace WileyWidget.UI.Tests;

/// <summary>
/// Collection definition for UI tests.
/// Ensures UI tests run sequentially to avoid conflicts with the single WinForms application instance.
/// </summary>
[CollectionDefinition("UITests", DisableParallelization = true)]
public class UITestsCollection : ICollectionFixture<UITestsFixture>
{
}

/// <summary>
/// Fixture for UI tests providing shared application instance management.
/// Launches the application once per test collection and provides shared access.
///
/// Optimized for:
/// - Single application launch per test collection (reduces ~15s startup overhead per test)
/// - Thread-safe access for future parallelization
/// - Proper cleanup with ForceCloseSharedApplication
/// </summary>
public class UITestsFixture : IDisposable
{
    private FlaUIApp? _application;
    private UIA3Automation? _automation;
    private Window? _mainWindow;
    private bool _disposed;
    private readonly object _lock = new();
    private int _activeTests;

    /// <summary>
    /// Default timeout for application startup.
    /// </summary>
    public TimeSpan StartupTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Retry interval for polling operations.
    /// </summary>
    public TimeSpan RetryInterval { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Gets whether the application is currently running.
    /// </summary>
    public bool IsApplicationRunning => _application != null && !_application.HasExited;

    /// <summary>
    /// Gets the shared automation instance.
    /// </summary>
    public UIA3Automation? Automation => _automation;

    /// <summary>
    /// Gets the main window of the application.
    /// </summary>
    public Window? MainWindow => _mainWindow;

    /// <summary>
    /// Gets the application instance.
    /// </summary>
    public FlaUIApp? Application => _application;

    public UITestsFixture()
    {
        // Initialization is deferred to first use to allow for proper error handling
    }

    /// <summary>
    /// Ensure the application is launched and ready for testing.
    /// Thread-safe for concurrent test access.
    /// </summary>
    public void EnsureApplicationLaunched()
    {
        lock (_lock)
        {
            _activeTests++;

            if (IsApplicationRunning && _mainWindow != null)
            {
                return;
            }

            LaunchApplicationInternal();
        }
    }

    /// <summary>
    /// Signal that a test has completed. Does not close the app until all tests are done.
    /// </summary>
    public void TestCompleted()
    {
        lock (_lock)
        {
            _activeTests--;
        }
    }

    /// <summary>
    /// Reset the application state for the next test (navigate to main form, close dialogs)
    /// </summary>
    public void ResetApplicationState()
    {
        if (!IsApplicationRunning || _mainWindow == null) return;

        try
        {
            // Close any open child windows
            var windows = _application?.GetAllTopLevelWindows(_automation!);
            if (windows != null)
            {
                foreach (var window in windows)
                {
                    if (window != _mainWindow && !string.IsNullOrEmpty(window.Title))
                    {
                        try
                        {
                            window.Close();
                        }
                        catch
                        {
                            // Ignore close errors on child windows
                        }
                    }
                }
            }

            // Ensure main window is focused
            _mainWindow.SetForeground();
        }
        catch
        {
            // State reset failed, but continue
        }
    }

    private void LaunchApplicationInternal()
    {
        var appPath = GetApplicationPath();

        Console.WriteLine($"[UITestsFixture] Launching app from: {appPath}");
        System.Diagnostics.Debug.WriteLine($"[UITestsFixture] Launching app from: {appPath}");

        if (!File.Exists(appPath))
        {
            throw new FileNotFoundException(
                $"Application executable not found at: {appPath}. " +
                "Please build the WileyWidget.WinForms project first or set WILEY_UI_TEST_APP_PATH environment variable.");
        }

        // Copy license.key to app directory to avoid Syncfusion licensing dialogs
        CopyTestLicenseToAppDirectory(appPath);

        // Set test mode environment variable
        Environment.SetEnvironmentVariable("WILEY_UI_TEST_MODE", "true", EnvironmentVariableTarget.Process);

        // Kill any existing instances to ensure clean state
        KillExistingProcesses();

        _automation = new UIA3Automation();

        var processStartInfo = new ProcessStartInfo(appPath)
        {
            WorkingDirectory = Path.GetDirectoryName(appPath),
            UseShellExecute = false
        };

        _application = FlaUIApp.Launch(processStartInfo);

        // Wait for main window with timeout
        _mainWindow = WaitForMainWindow(StartupTimeout);

        if (_mainWindow == null)
        {
            _application?.Kill();
            throw new TimeoutException(
                $"Main window did not appear within {StartupTimeout.TotalSeconds} seconds. " +
                "The application may have crashed or failed to start.");
        }

        // Wait for application to become idle after startup
        try
        {
            _application.WaitWhileBusy(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // Ignore timeout - continue anyway
        }

        Console.WriteLine($"[UITestsFixture] Application launched successfully. Main window: '{_mainWindow.Title}'");
    }

    /// <summary>
    /// Copy test license file to app directory to prevent Syncfusion dialogs during tests.
    /// </summary>
    private static void CopyTestLicenseToAppDirectory(string appPath)
    {
        try
        {
            var testAssemblyDir = AppDomain.CurrentDomain.BaseDirectory;
            var testLicensePath = Path.Combine(testAssemblyDir, "license.key");
            var appDir = Path.GetDirectoryName(appPath);

            if (File.Exists(testLicensePath) && !string.IsNullOrEmpty(appDir))
            {
                var destPath = Path.Combine(appDir, "license.key");
                File.Copy(testLicensePath, destPath, overwrite: true);
                Console.WriteLine($"[UITestsFixture] Copied test license to: {destPath}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UITestsFixture] Warning: Failed to copy license.key: {ex.Message}");
            // Continue anyway - not critical
        }
    }

    private static void KillExistingProcesses()
    {
        try
        {
            var processes = Process.GetProcessesByName("WileyWidget.WinForms");
            foreach (var proc in processes)
            {
                try
                {
                    if (!proc.HasExited)
                    {
                        proc.Kill();
                        proc.WaitForExit(5000);
                    }
                }
                catch
                {
                    // Ignore individual kill failures
                }
                finally
                {
                    proc.Dispose();
                }
            }
        }
        catch
        {
            // Ignore enumeration failures
        }
    }

    private Window? WaitForMainWindow(TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            try
            {
                var window = _application?.GetMainWindow(_automation!, TimeSpan.FromSeconds(1));
                if (window != null)
                {
                    return window;
                }
            }
            catch
            {
                // Window not ready yet
            }

            Thread.Sleep(RetryInterval);
        }

        return null;
    }

    private static string GetApplicationPath()
    {
        // Check for CI/CD environment variable override
        var envPath = Environment.GetEnvironmentVariable("WILEY_UI_TEST_APP_PATH");
        if (!string.IsNullOrEmpty(envPath) && File.Exists(envPath))
        {
            return envPath;
        }

        // Navigate from test assembly to WinForms output
        var testAssemblyDir = AppDomain.CurrentDomain.BaseDirectory;
        var solutionDir = Path.GetFullPath(Path.Combine(testAssemblyDir, "..", "..", "..", "..", ".."));
        var appPath = Path.Combine(solutionDir, "WileyWidget.WinForms", "bin", "Debug", "net9.0-windows", "WileyWidget.WinForms.exe");

        // Fallback to Release
        if (!File.Exists(appPath))
        {
            appPath = Path.Combine(solutionDir, "WileyWidget.WinForms", "bin", "Release", "net9.0-windows", "WileyWidget.WinForms.exe");
        }

        return appPath;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            try
            {
                // Clear test mode environment variable
                Environment.SetEnvironmentVariable("WILEY_UI_TEST_MODE", null, EnvironmentVariableTarget.Process);

                if (_application != null && !_application.HasExited)
                {
                    // Try graceful close first
                    try
                    {
                        _mainWindow?.Close();
                        _application.Close();

                        // Wait for application to exit
                        var stopwatch = Stopwatch.StartNew();
                        while (!_application.HasExited && stopwatch.Elapsed < TimeSpan.FromSeconds(5))
                        {
                            Thread.Sleep(100);
                        }
                    }
                    catch
                    {
                        // Graceful close failed, will force kill
                    }

                    // Force kill if still running
                    if (!_application.HasExited)
                    {
                        Console.WriteLine("[UITestsFixture] Application didn't close gracefully, killing process...");
                        _application.Kill();

                        // Retry kill if first attempt fails
                        for (int retry = 0; retry < 3 && !_application.HasExited; retry++)
                        {
                            Thread.Sleep(500);
                            try
                            {
                                _application.Kill();
                            }
                            catch
                            {
                                // Ignore
                            }
                        }
                    }
                }

                // Dispose the application if it implements IDisposable
                (_application as IDisposable)?.Dispose();
                _application = null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UITestsFixture] Warning during cleanup: {ex.Message}");
            }

            _automation?.Dispose();
            _automation = null;
        }

        _disposed = true;
    }
}
