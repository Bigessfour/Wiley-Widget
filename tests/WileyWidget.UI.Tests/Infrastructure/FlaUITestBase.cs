using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Input;
using FlaUI.UIA3;
using Xunit;
using FlaUIApp = FlaUI.Core.Application;

namespace WileyWidget.UI.Tests.Infrastructure;

/// <summary>
/// Base class for FlaUI-based WinForms UI automation tests.
/// Provides common infrastructure for launching and automating the WileyWidget.WinForms application.
///
/// Key Features:
/// - TimeSpan-based timeouts for better readability and flexibility
/// - Generic WaitUntil helper with configurable polling intervals
/// - Shared application state support for test collections
/// - Async method variants for async test support
/// - Retry wrappers with exponential backoff
/// </summary>
public abstract class FlaUITestBase : IDisposable
{
    private bool _disposed;
    private static readonly object _sharedLock = new(); // For thread-safety if parallelized later
    private static FlaUIApp? _sharedApplication;
    private static UIA3Automation? _sharedAutomation;
    private static Window? _sharedMainWindow;
    private static bool _isSharedApplicationLaunched;

    // Instance-level fields for non-shared usage (backward compatibility)
    private FlaUIApp? _instanceApplication;
    private UIA3Automation? _instanceAutomation;
    private Window? _instanceMainWindow;

    /// <summary>
    /// Gets the application instance (prefers shared, falls back to instance-level).
    /// </summary>
    protected FlaUIApp? Application => _sharedApplication ?? _instanceApplication;

    /// <summary>
    /// Gets the automation instance (prefers shared, falls back to instance-level).
    /// </summary>
    protected UIA3Automation? Automation => _sharedAutomation ?? _instanceAutomation;

    /// <summary>
    /// Gets the main window (prefers shared, falls back to instance-level).
    /// </summary>
    protected Window? MainWindow => _sharedMainWindow ?? _instanceMainWindow;

    /// <summary>
    /// Gets whether the application is currently launched and running.
    /// </summary>
    protected bool IsApplicationLaunched =>
        (_sharedApplication != null && !_sharedApplication.HasExited) ||
        (_instanceApplication != null && !_instanceApplication.HasExited);

    /// <summary>
    /// Gets whether using shared application state.
    /// </summary>
    protected bool IsUsingSharedState => _isSharedApplicationLaunched;

    #region Timeout Configuration

    /// <summary>
    /// Default timeout for UI element searches (increased for child form detection).
    /// </summary>
    protected virtual TimeSpan DefaultTimeout => TimeSpan.FromSeconds(10);

    /// <summary>
    /// Short timeout for quick operations.
    /// </summary>
    protected virtual TimeSpan ShortTimeout => TimeSpan.FromSeconds(2);

    /// <summary>
    /// Retry interval for polling operations.
    /// </summary>
    protected virtual TimeSpan RetryInterval => TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Default timeout for application startup.
    /// </summary>
    protected virtual TimeSpan StartupTimeout => TimeSpan.FromSeconds(30);

    /// <summary>
    /// Timeout for child window appearance after menu click.
    /// </summary>
    protected virtual TimeSpan ChildWindowTimeout => TimeSpan.FromSeconds(10);

    /// <summary>
    /// Whether to enable verbose debug logging for UI automation.
    /// </summary>
    protected virtual bool EnableVerboseLogging => true;

    // Backward compatibility properties (deprecated but still functional)
    [Obsolete("Use DefaultTimeout instead")]
    protected virtual int DefaultTimeoutMs => (int)DefaultTimeout.TotalMilliseconds;

    [Obsolete("Use ShortTimeout instead")]
    protected virtual int ShortTimeoutMs => (int)ShortTimeout.TotalMilliseconds;

    [Obsolete("Use RetryInterval instead")]
    protected virtual int RetryIntervalMs => (int)RetryInterval.TotalMilliseconds;

    #endregion

    /// <summary>
    /// Path to the WinForms application executable.
    /// </summary>
    protected virtual string ApplicationPath => GetApplicationPath();

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

        // Fallback to published location
        if (!File.Exists(appPath))
        {
            appPath = Path.Combine(solutionDir, "WileyWidget.WinForms", "bin", "Release", "net9.0-windows", "WileyWidget.WinForms.exe");
        }

        return appPath;
    }

    #region Application Lifecycle

    /// <summary>
    /// Launch the application and wait for the main window to appear.
    /// Uses shared static state for reuse across tests in the collection.
    /// Kills any existing instances to ensure clean state.
    /// </summary>
    /// <param name="startupTimeout">Optional startup timeout (defaults to StartupTimeout property).</param>
    /// <param name="useSharedState">If true, uses shared static state for collection reuse.</param>
    protected void LaunchApplication(TimeSpan? startupTimeout = null, bool useSharedState = true)
    {
        var timeout = startupTimeout ?? StartupTimeout;

        if (useSharedState)
        {
            LaunchSharedApplication(timeout);
        }
        else
        {
            LaunchInstanceApplication(timeout);
        }
    }

    /// <summary>
    /// Launch using shared static state (optimized for test collections).
    /// </summary>
    private void LaunchSharedApplication(TimeSpan timeout)
    {
        lock (_sharedLock)
        {
            if (_isSharedApplicationLaunched && _sharedMainWindow != null && !_sharedApplication!.HasExited)
            {
                return; // Already launched and ready
            }

            // Kill any existing instances to ensure clean state
            KillExistingProcesses();

            if (!File.Exists(ApplicationPath))
            {
                throw new FileNotFoundException(
                    $"Application executable not found at: {ApplicationPath}. " +
                    "Please build the WileyWidget.WinForms project first or set WILEY_UI_TEST_APP_PATH environment variable.");
            }

            // Initialize automation
            _sharedAutomation = new UIA3Automation();

            var processStartInfo = new ProcessStartInfo(ApplicationPath)
            {
                WorkingDirectory = Path.GetDirectoryName(ApplicationPath),
                UseShellExecute = false
            };

            // Launch new instance
            _sharedApplication = FlaUIApp.Launch(processStartInfo);

            // Wait for main window using optimized polling
            _sharedMainWindow = WaitForMainWindowInternal(_sharedApplication, _sharedAutomation, timeout);

            if (_sharedMainWindow == null)
            {
                _sharedApplication?.Kill();
                throw new TimeoutException(
                    $"Main window did not appear within {timeout.TotalSeconds}s. " +
                    "The application may have crashed or failed to start.");
            }

            _isSharedApplicationLaunched = true;
        }
    }

    /// <summary>
    /// Launch using instance-level state (backward compatible, per-test isolation).
    /// </summary>
    private void LaunchInstanceApplication(TimeSpan timeout)
    {
        if (_instanceApplication != null && !_instanceApplication.HasExited && _instanceMainWindow != null)
        {
            return;
        }

        // Kill any existing instances to ensure clean state
        KillExistingProcesses();

        if (!File.Exists(ApplicationPath))
        {
            throw new FileNotFoundException(
                $"Application executable not found at: {ApplicationPath}. " +
                "Please build the WileyWidget.WinForms project first or set WILEY_UI_TEST_APP_PATH environment variable.");
        }

        _instanceAutomation = new UIA3Automation();

        var processStartInfo = new ProcessStartInfo(ApplicationPath)
        {
            WorkingDirectory = Path.GetDirectoryName(ApplicationPath),
            UseShellExecute = false
        };

        _instanceApplication = FlaUIApp.Launch(processStartInfo);

        // Wait for main window using optimized polling
        _instanceMainWindow = WaitForMainWindowInternal(_instanceApplication, _instanceAutomation, timeout);

        if (_instanceMainWindow == null)
        {
            _instanceApplication?.Kill();
            throw new TimeoutException(
                $"Main window did not appear within {timeout.TotalSeconds}s. " +
                "The application may have crashed or failed to start.");
        }
    }

    /// <summary>
    /// Backward-compatible overload accepting milliseconds.
    /// </summary>
    [Obsolete("Use LaunchApplication(TimeSpan?) instead")]
    protected void LaunchApplication(int startupTimeoutMs)
    {
        LaunchApplication(TimeSpan.FromMilliseconds(startupTimeoutMs));
    }

    /// <summary>
    /// Async version of LaunchApplication for async test support.
    /// </summary>
    protected Task LaunchApplicationAsync(TimeSpan? startupTimeout = null, bool useSharedState = true)
    {
        return Task.Run(() => LaunchApplication(startupTimeout, useSharedState));
    }

    /// <summary>
    /// Kill any existing WileyWidget.WinForms processes.
    /// </summary>
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

    /// <summary>
    /// Optimized wait for main window using generic WaitUntil pattern.
    /// </summary>
    private Window? WaitForMainWindowInternal(FlaUIApp application, UIA3Automation automation, TimeSpan timeout)
    {
        Window? window = null;
        var success = WaitUntil(() =>
        {
            try
            {
                window = application.GetMainWindow(automation, TimeSpan.FromSeconds(1));
                return window != null;
            }
            catch
            {
                return false;
            }
        }, timeout, RetryInterval);

        return success ? window : null;
    }

    /// <summary>
    /// Attach to an already running instance of the application.
    /// </summary>
    protected void AttachToRunningApplication(string processName = "WileyWidget.WinForms")
    {
        _instanceAutomation = new UIA3Automation();

        var processes = Process.GetProcessesByName(processName);
        if (processes.Length == 0)
        {
            throw new InvalidOperationException(
                $"No running process found with name '{processName}'. " +
                "Please ensure the application is running.");
        }

        _instanceApplication = FlaUIApp.Attach(processes[0]);
        _instanceMainWindow = _instanceApplication.GetMainWindow(_instanceAutomation);
    }

    /// <summary>
    /// Close any child windows (dialogs, forms) but keep the main application running.
    /// This resets the application state between tests.
    /// </summary>
    protected void CloseChildWindows()
    {
        var app = Application;
        var automation = Automation;
        var mainWindow = MainWindow;

        if (app == null || automation == null || mainWindow == null) return;

        try
        {
            var allWindows = app.GetAllTopLevelWindows(automation);
            foreach (var window in allWindows)
            {
                // Skip the main window
                if (ReferenceEquals(window, mainWindow)) continue;

                // Skip if it looks like the main application window
                var title = window.Title ?? string.Empty;
                if (title.Contains("Wiley Widget", StringComparison.OrdinalIgnoreCase) &&
                    !title.Contains("Settings", StringComparison.OrdinalIgnoreCase) &&
                    !title.Contains("Accounts", StringComparison.OrdinalIgnoreCase) &&
                    !title.Contains("Chart", StringComparison.OrdinalIgnoreCase) &&
                    !title.Contains("Analytics", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    window.Close();
                }
                catch
                {
                    // Ignore errors closing individual windows
                }
            }
        }
        catch
        {
            // Ignore enumeration errors
        }
    }

    /// <summary>
    /// Close the application gracefully and terminate the process.
    /// Each test gets a fresh app instance to avoid state pollution.
    /// </summary>
    protected void CloseApplication()
    {
        // Handle instance-level application
        if (_instanceApplication != null)
        {
            CloseApplicationInternal(_instanceApplication, _instanceMainWindow, _instanceAutomation);
            _instanceApplication = null;
            _instanceMainWindow = null;
            _instanceAutomation = null;
        }

        // Don't close shared application from here - let the fixture handle it
    }

    /// <summary>
    /// Force close the application (terminates the process).
    /// Use for error recovery or explicit teardown.
    /// </summary>
    protected void ForceCloseApplication()
    {
        // Handle instance-level application
        if (_instanceApplication != null)
        {
            ForceCloseApplicationInternal(_instanceApplication, _instanceAutomation);
            _instanceApplication = null;
            _instanceMainWindow = null;
            _instanceAutomation = null;
        }
    }

    /// <summary>
    /// Force close the shared application (typically called from fixture).
    /// </summary>
    protected static void ForceCloseSharedApplication()
    {
        lock (_sharedLock)
        {
            if (_sharedApplication == null) return;

            ForceCloseApplicationInternal(_sharedApplication, _sharedAutomation);
            _sharedApplication = null;
            _sharedMainWindow = null;
            _sharedAutomation = null;
            _isSharedApplicationLaunched = false;
        }
    }

    private static void CloseApplicationInternal(FlaUIApp application, Window? mainWindow, UIA3Automation? automation)
    {
        try
        {
            if (application.HasExited) return;

            try
            {
                mainWindow?.Close();
                application.Close();

                // Wait for application to exit
                var stopwatch = Stopwatch.StartNew();
                while (!application.HasExited && stopwatch.Elapsed < TimeSpan.FromSeconds(5))
                {
                    Thread.Sleep(100);
                }

                if (!application.HasExited)
                {
                    application.Kill();
                }
            }
            catch
            {
                application.Kill();
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
        finally
        {
            automation?.Dispose();
        }
    }

    private static void ForceCloseApplicationInternal(FlaUIApp application, UIA3Automation? automation)
    {
        try
        {
            if (!application.HasExited)
            {
                application.Kill();
            }
        }
        catch
        {
            // Ignore errors during close
        }
        finally
        {
            automation?.Dispose();
        }
    }

    #endregion

    #region Generic Wait Helpers

    /// <summary>
    /// Generic wait method for polling until a condition is true.
    /// This is the core waiting mechanism used by all other wait methods.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="timeout">Maximum time to wait.</param>
    /// <param name="interval">Polling interval between condition checks.</param>
    /// <returns>True if condition was met, false if timed out.</returns>
    protected bool WaitUntil(Func<bool> condition, TimeSpan timeout, TimeSpan interval)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            try
            {
                if (condition()) return true;
            }
            catch
            {
                // Condition threw, continue waiting
            }

            Thread.Sleep(interval);
        }

        return false;
    }

    /// <summary>
    /// Generic wait that throws TimeoutException on failure.
    /// </summary>
    protected void WaitUntilOrThrow(Func<bool> condition, TimeSpan timeout, TimeSpan interval, string? description = null)
    {
        if (!WaitUntil(condition, timeout, interval))
        {
            var message = string.IsNullOrEmpty(description)
                ? $"Condition not met within {timeout.TotalSeconds}s."
                : $"{description} - timed out after {timeout.TotalSeconds}s.";
            throw new TimeoutException(message);
        }
    }

    /// <summary>
    /// Async generic wait method for async tests.
    /// </summary>
    protected async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout, TimeSpan interval, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                if (condition()) return true;
            }
            catch
            {
                // Condition threw, continue waiting
            }

            await Task.Delay(interval, ct);
        }

        return false;
    }

    /// <summary>
    /// Async wait that throws TimeoutException on failure.
    /// </summary>
    protected async Task WaitUntilOrThrowAsync(Func<bool> condition, TimeSpan timeout, TimeSpan interval, string? description = null, CancellationToken ct = default)
    {
        if (!await WaitUntilAsync(condition, timeout, interval, ct))
        {
            var message = string.IsNullOrEmpty(description)
                ? $"Condition not met within {timeout.TotalSeconds}s."
                : $"{description} - timed out after {timeout.TotalSeconds}s.";
            throw new TimeoutException(message);
        }
    }

    #endregion

    #region Conditional Wait Helpers

    /// <summary>
    /// Wait for a condition to become true with timeout and retry logic.
    /// </summary>
    /// <param name="condition">The condition to wait for.</param>
    /// <param name="timeout">Maximum time to wait (defaults to DefaultTimeout).</param>
    /// <param name="description">Description of what we're waiting for (for error messages).</param>
    /// <returns>True if condition was met, false if timed out.</returns>
    protected bool WaitForCondition(Func<bool> condition, TimeSpan? timeout = null, string? description = null)
    {        return WaitUntil(condition, timeout ?? DefaultTimeout, RetryInterval);
    }

    /// <summary>
    /// Backward-compatible overload accepting milliseconds.
    /// </summary>
    [Obsolete("Use WaitForCondition(Func<bool>, TimeSpan?, string?) instead")]
    protected bool WaitForCondition(Func<bool> condition, int? timeoutMs = null, string? description = null)
    {
        var timeout = timeoutMs.HasValue ? TimeSpan.FromMilliseconds(timeoutMs.Value) : DefaultTimeout;
        return WaitUntil(condition, timeout, RetryInterval);
    }

    /// <summary>
    /// Wait for an element to become enabled.
    /// </summary>
    protected bool WaitForElementEnabled(AutomationElement element, TimeSpan? timeout = null)
    {
        return WaitForCondition(
            () => element.IsEnabled,
            timeout,
            $"Element '{element.Name}' to become enabled");
    }

    /// <summary>
    /// Backward-compatible overload accepting milliseconds.
    /// </summary>
    [Obsolete("Use WaitForElementEnabled(AutomationElement, TimeSpan?) instead")]
    protected bool WaitForElementEnabled(AutomationElement element, int? timeoutMs)
    {
        var timeout = timeoutMs.HasValue ? TimeSpan.FromMilliseconds(timeoutMs.Value) : ShortTimeout;
        return WaitForElementEnabled(element, timeout);
    }

    /// <summary>
    /// Wait for an element to become visible (not offscreen).
    /// </summary>
    protected bool WaitForElementVisible(AutomationElement element, TimeSpan? timeout = null)
    {
        return WaitForCondition(
            () => !element.IsOffscreen,
            timeout,
            $"Element '{element.Name}' to become visible");
    }

    /// <summary>
    /// Backward-compatible overload accepting milliseconds.
    /// </summary>
    [Obsolete("Use WaitForElementVisible(AutomationElement, TimeSpan?) instead")]
    protected bool WaitForElementVisible(AutomationElement element, int? timeoutMs)
    {
        var timeout = timeoutMs.HasValue ? TimeSpan.FromMilliseconds(timeoutMs.Value) : DefaultTimeout;
        return WaitForElementVisible(element, timeout);
    }

    /// <summary>
    /// Wait for text content to appear in an element.
    /// </summary>
    protected bool WaitForTextContent(AutomationElement element, string expectedText, TimeSpan? timeout = null)
    {
        return WaitForCondition(
            () => element.Name?.Contains(expectedText, StringComparison.OrdinalIgnoreCase) == true,
            timeout,
            $"Element to contain text '{expectedText}'");
    }

    /// <summary>
    /// Backward-compatible overload accepting milliseconds.
    /// </summary>
    [Obsolete("Use WaitForTextContent(AutomationElement, string, TimeSpan?) instead")]
    protected bool WaitForTextContent(AutomationElement element, string expectedText, int? timeoutMs)
    {
        var timeout = timeoutMs.HasValue ? TimeSpan.FromMilliseconds(timeoutMs.Value) : DefaultTimeout;
        return WaitForTextContent(element, expectedText, timeout);
    }

    /// <summary>
    /// Wait for a status bar or text element to contain specific text.
    /// </summary>
    protected bool WaitForStatusText(string expectedText, AutomationElement? parent = null, TimeSpan? timeout = null)
    {
        var searchRoot = parent ?? MainWindow;
        if (searchRoot == null) return false;

        return WaitForCondition(() =>
        {
            var textElements = searchRoot.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Text));
            return textElements.Any(e => e.Name?.Contains(expectedText, StringComparison.OrdinalIgnoreCase) == true);
        }, timeout, $"Status text to contain '{expectedText}'");
    }

    /// <summary>
    /// Backward-compatible overload accepting milliseconds.
    /// </summary>
    [Obsolete("Use WaitForStatusText(string, AutomationElement?, TimeSpan?) instead")]
    protected bool WaitForStatusText(string expectedText, AutomationElement? parent, int? timeoutMs)
    {
        var timeout = timeoutMs.HasValue ? TimeSpan.FromMilliseconds(timeoutMs.Value) : DefaultTimeout;
        return WaitForStatusText(expectedText, parent, timeout);
    }

    /// <summary>
    /// Wait for a window to close (no longer present).
    /// </summary>
    protected bool WaitForWindowClosed(string windowTitle, TimeSpan? timeout = null)
    {
        var app = Application;
        var automation = Automation;

        return WaitForCondition(() =>
        {
            var windows = app?.GetAllTopLevelWindows(automation!);
            return windows?.All(w => !w.Title?.Contains(windowTitle, StringComparison.OrdinalIgnoreCase) == true) ?? true;
        }, timeout, $"Window '{windowTitle}' to close");
    }

    /// <summary>
    /// Backward-compatible overload accepting milliseconds.
    /// </summary>
    [Obsolete("Use WaitForWindowClosed(string, TimeSpan?) instead")]
    protected bool WaitForWindowClosed(string windowTitle, int? timeoutMs)
    {
        var timeout = timeoutMs.HasValue ? TimeSpan.FromMilliseconds(timeoutMs.Value) : DefaultTimeout;
        return WaitForWindowClosed(windowTitle, timeout);
    }

    /// <summary>
    /// Wait for data to load (grid has rows, or loading indicator disappears).
    /// </summary>
    protected bool WaitForDataLoaded(AutomationElement? gridElement = null, AutomationElement? parent = null, TimeSpan? timeout = null)
    {
        var searchRoot = parent ?? MainWindow;
        if (searchRoot == null) return false;

        // If grid element provided, wait for it to have content
        if (gridElement != null)
        {
            return WaitForCondition(() =>
            {
                var grid = gridElement.AsGrid();
                return grid.RowCount > 0;
            }, timeout, "Grid to have data rows");
        }

        // Otherwise wait for any loading indicator to disappear or status to show "Ready"
        return WaitForCondition(() =>
        {
            var textElements = searchRoot.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Text));
            var hasLoadingText = textElements.Any(e =>
                e.Name?.Contains("Loading", StringComparison.OrdinalIgnoreCase) == true);
            var hasReadyText = textElements.Any(e =>
                e.Name?.Contains("Ready", StringComparison.OrdinalIgnoreCase) == true ||
                e.Name?.Contains("accounts", StringComparison.OrdinalIgnoreCase) == true ||
                e.Name?.Contains("loaded", StringComparison.OrdinalIgnoreCase) == true);
            return !hasLoadingText || hasReadyText;
        }, timeout, "Data to finish loading");
    }

    /// <summary>
    /// Backward-compatible overload accepting milliseconds. Requires all parameters.
    /// </summary>
    [Obsolete("Use WaitForDataLoaded(AutomationElement?, AutomationElement?, TimeSpan?) instead")]
    protected bool WaitForDataLoaded(AutomationElement? gridElement, AutomationElement? parent, int timeoutMs)
    {
        return WaitForDataLoaded(gridElement, parent, TimeSpan.FromMilliseconds(timeoutMs));
    }

    /// <summary>
    /// Simple backward-compatible overload for (timeout) pattern.
    /// </summary>
    [Obsolete("Use WaitForDataLoaded(AutomationElement?, AutomationElement?, TimeSpan?) instead")]
    protected bool WaitForDataLoaded(int timeoutMs)
    {
        return WaitForDataLoaded(null, null, TimeSpan.FromMilliseconds(timeoutMs));
    }

    /// <summary>
    /// Wait for application to be ready (main window responsive and not loading).
    /// </summary>
    protected bool WaitForApplicationReady(TimeSpan? timeout = null)
    {
        var mainWindow = MainWindow;
        if (mainWindow == null) return false;

        return WaitForCondition(() =>
        {
            return mainWindow.IsEnabled && !mainWindow.IsOffscreen;
        }, timeout, "Application to become ready");
    }

    /// <summary>
    /// Backward-compatible overload accepting milliseconds.
    /// </summary>
    [Obsolete("Use WaitForApplicationReady(TimeSpan?) instead")]
    protected bool WaitForApplicationReady(int? timeoutMs)
    {
        var timeout = timeoutMs.HasValue ? TimeSpan.FromMilliseconds(timeoutMs.Value) : DefaultTimeout;
        return WaitForApplicationReady(timeout);
    }

    #endregion

    #region Element Finding

    /// <summary>
    /// Core element finding method using a condition factory delegate.
    /// All other FindElement methods delegate to this.
    /// </summary>
    protected AutomationElement? FindElement(Func<ConditionFactory, ConditionBase> conditionFunc, AutomationElement? parent = null, TimeSpan? timeout = null)
    {
        var searchRoot = parent ?? MainWindow;
        if (searchRoot == null) return null;

        AutomationElement? element = null;
        WaitUntil(() =>
        {
            try
            {
                element = searchRoot.FindFirstDescendant(conditionFunc);
                return element != null;
            }
            catch
            {
                return false;
            }
        }, timeout ?? DefaultTimeout, RetryInterval);

        return element;
    }

    /// <summary>
    /// Find an element by automation ID with retry logic.
    /// </summary>
    protected AutomationElement? FindElementByAutomationId(string automationId, AutomationElement? parent = null, TimeSpan? timeout = null)
    {
        return FindElement(cf => cf.ByAutomationId(automationId), parent, timeout);
    }

    /// <summary>
    /// Backward-compatible overload accepting milliseconds. Requires all parameters.
    /// </summary>
    [Obsolete("Use FindElementByAutomationId(string, AutomationElement?, TimeSpan?) instead")]
    protected AutomationElement? FindElementByAutomationId(string automationId, AutomationElement? parent, int timeoutMs)
    {
        return FindElementByAutomationId(automationId, parent, TimeSpan.FromMilliseconds(timeoutMs));
    }

    /// <summary>
    /// Find an element by name (text) with retry logic.
    /// </summary>
    protected AutomationElement? FindElementByName(string name, AutomationElement? parent = null, TimeSpan? timeout = null)
    {
        return FindElement(cf => cf.ByName(name), parent, timeout);
    }

    /// <summary>
    /// Backward-compatible overload accepting milliseconds. Requires all parameters.
    /// </summary>
    [Obsolete("Use FindElementByName(string, AutomationElement?, TimeSpan?) instead")]
    protected AutomationElement? FindElementByName(string name, AutomationElement? parent, int timeoutMs)
    {
        return FindElementByName(name, parent, TimeSpan.FromMilliseconds(timeoutMs));
    }

    /// <summary>
    /// Simple backward-compatible overload for (name, timeout) pattern.
    /// </summary>
    [Obsolete("Use FindElementByName(string, AutomationElement?, TimeSpan?) instead")]
    protected AutomationElement? FindElementByName(string name, int timeoutMs)
    {
        return FindElementByName(name, null, TimeSpan.FromMilliseconds(timeoutMs));
    }

    /// <summary>
    /// Find an element by class name with retry logic.
    /// </summary>
    protected AutomationElement? FindElementByClassName(string className, AutomationElement? parent = null, TimeSpan? timeout = null)
    {
        return FindElement(cf => cf.ByClassName(className), parent, timeout);
    }

    /// <summary>
    /// Backward-compatible overload accepting milliseconds. Requires all parameters.
    /// </summary>
    [Obsolete("Use FindElementByClassName(string, AutomationElement?, TimeSpan?) instead")]
    protected AutomationElement? FindElementByClassName(string className, AutomationElement? parent, int timeoutMs)
    {
        return FindElementByClassName(className, parent, TimeSpan.FromMilliseconds(timeoutMs));
    }

    /// <summary>
    /// Simple backward-compatible overload for (className, timeout) pattern.
    /// </summary>
    [Obsolete("Use FindElementByClassName(string, AutomationElement?, TimeSpan?) instead")]
    protected AutomationElement? FindElementByClassName(string className, int timeoutMs)
    {
        return FindElementByClassName(className, null, TimeSpan.FromMilliseconds(timeoutMs));
    }

    /// <summary>
    /// Find an element by control type with retry logic.
    /// </summary>
    protected AutomationElement? FindElementByControlType(FlaUI.Core.Definitions.ControlType controlType, AutomationElement? parent = null, TimeSpan? timeout = null)
    {
        return FindElement(cf => cf.ByControlType(controlType), parent, timeout);
    }

    /// <summary>
    /// Find an element by AutomationId first, falling back to Name if not found.
    /// </summary>
    protected AutomationElement? FindElementByIdOrName(string automationId, string name, AutomationElement? parent = null, TimeSpan? timeout = null)
    {
        var effectiveTimeout = timeout ?? DefaultTimeout;
        var halfTimeout = TimeSpan.FromTicks(effectiveTimeout.Ticks / 2);

        // Try AutomationId first
        var element = FindElementByAutomationId(automationId, parent, halfTimeout);
        if (element != null) return element;

        // Fallback to Name
        return FindElementByName(name, parent, halfTimeout);
    }

    /// <summary>
    /// Backward-compatible overload accepting milliseconds.
    /// </summary>
    [Obsolete("Use FindElementByIdOrName(string, string, AutomationElement?, TimeSpan?) instead")]
    protected AutomationElement? FindElementByIdOrName(string automationId, string name, AutomationElement? parent, int? timeoutMs)
    {
        var timeout = timeoutMs.HasValue ? TimeSpan.FromMilliseconds(timeoutMs.Value) : DefaultTimeout;
        return FindElementByIdOrName(automationId, name, parent, timeout);
    }

    /// <summary>
    /// Find all elements matching a condition (no retry, immediate).
    /// </summary>
    protected AutomationElement[] FindAllElements(Func<ConditionFactory, ConditionBase> conditionFunc, AutomationElement? parent = null)
    {
        var searchRoot = parent ?? MainWindow;
        if (searchRoot == null) return Array.Empty<AutomationElement>();

        try
        {
            return searchRoot.FindAllDescendants(conditionFunc);
        }
        catch
        {
            return Array.Empty<AutomationElement>();
        }
    }

    /// <summary>
    /// Wait for a window with the specified title to appear.
    /// Uses partial title matching and searches all top-level windows.
    /// </summary>
    protected Window? WaitForWindow(string titleContains, TimeSpan? timeout = null)
    {
        var app = Application;
        var automation = Automation;

        if (app == null || automation == null) return null;

        var effectiveTimeout = timeout ?? ChildWindowTimeout;
        var start = DateTime.UtcNow;

        LogDebug($"WaitForWindow: Looking for window containing '{titleContains}' (timeout: {effectiveTimeout.TotalSeconds}s)");

        Window? targetWindow = null;
        while (DateTime.UtcNow - start < effectiveTimeout)
        {
            try
            {
                var windows = app.GetAllTopLevelWindows(automation);

                // Log all available windows for debugging
                if (EnableVerboseLogging)
                {
                    var windowTitles = string.Join(", ", windows.Select(w => $"'{w.Title}'"));
                    LogDebug($"WaitForWindow: Available windows: [{windowTitles}]");
                }

                // Search for partial match (case-insensitive)
                targetWindow = windows.FirstOrDefault(w =>
                    !string.IsNullOrEmpty(w.Title) &&
                    w.Title.Contains(titleContains, StringComparison.OrdinalIgnoreCase));

                if (targetWindow != null)
                {
                    LogDebug($"WaitForWindow: Found window '{targetWindow.Title}'");
                    return targetWindow;
                }
            }
            catch (Exception ex)
            {
                LogDebug($"WaitForWindow: Exception while searching: {ex.Message}");
            }

            Thread.Sleep(RetryInterval);
        }

        // On failure, log all available windows
        LogDebug($"WaitForWindow: TIMEOUT - Window containing '{titleContains}' not found");
        LogAllTopLevelWindows();

        return null;
    }

    /// <summary>
    /// Log all top-level windows for debugging.
    /// </summary>
    protected void LogAllTopLevelWindows()
    {
        var app = Application;
        var automation = Automation;
        if (app == null || automation == null) return;

        try
        {
            var windows = app.GetAllTopLevelWindows(automation);
            var windowInfo = windows.Select(w => $"'{w.Title}' (Handle: {w.Properties.NativeWindowHandle.ValueOrDefault})");
            LogDebug($"All top-level windows: [{string.Join(", ", windowInfo)}]");
        }
        catch (Exception ex)
        {
            LogDebug($"Failed to enumerate windows: {ex.Message}");
        }
    }

    /// <summary>
    /// Get a string representation of all available window titles.
    /// </summary>
    protected string GetAvailableWindowTitles()
    {
        var app = Application;
        var automation = Automation;
        if (app == null || automation == null) return "(no app/automation)";

        try
        {
            var windows = app.GetAllTopLevelWindows(automation);
            return string.Join(", ", windows.Select(w => $"'{w.Title}'"));
        }
        catch
        {
            return "(error getting windows)";
        }
    }

    /// <summary>
    /// Backward-compatible overload accepting milliseconds.
    /// </summary>
    [Obsolete("Use WaitForWindow(string, TimeSpan?) instead")]
    protected Window? WaitForWindow(string windowTitle, int? timeoutMs)
    {
        var timeout = timeoutMs.HasValue ? TimeSpan.FromMilliseconds(timeoutMs.Value) : DefaultTimeout;
        return WaitForWindow(windowTitle, timeout);
    }

    #endregion

    #region Interactions

    /// <summary>
    /// Click a button by its name/text and wait for it to be enabled.
    /// </summary>
    protected bool ClickButton(string buttonName, AutomationElement? parent = null, TimeSpan? timeout = null)
    {
        var button = FindElementByName(buttonName, parent, timeout);
        if (button == null) return false;

        try
        {
            // Wait for button to be enabled before clicking
            if (!WaitForElementEnabled(button, ShortTimeout))
            {
                return false;
            }

            button.AsButton().Click();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Click a menu item by navigating through menu hierarchy with proper waits.
    /// Uses mouse simulation for improved reliability with WinForms menus.
    /// </summary>
    protected bool ClickMenuItem(params string[] menuPath)
    {
        if (menuPath.Length == 0) return false;

        var automation = Automation;
        var app = Application;
        if (automation == null || app == null) return false;

        try
        {
            LogDebug($"ClickMenuItem: Navigating path [{string.Join(" > ", menuPath)}]");

            AutomationElement? current = MainWindow;

            for (int i = 0; i < menuPath.Length; i++)
            {
                var menuItem = menuPath[i];
                AutomationElement? item = null;

                // For top-level menu, search from MainWindow
                // For submenus, the popup menu might be a separate window, so search Desktop
                if (i == 0)
                {
                    item = FindElementByName(menuItem, current, ShortTimeout);
                    LogDebug($"ClickMenuItem: Top-level menu '{menuItem}' found: {item != null}");
                }
                else
                {
                    // Wait a moment for submenu to appear
                    Thread.Sleep(300);

                    // Search in desktop for popup menus
                    var desktop = automation.GetDesktop();
                    item = FindElementByName(menuItem, desktop, ShortTimeout);

                    // Fallback: try MainWindow if not found on desktop
                    if (item == null)
                    {
                        item = FindElementByName(menuItem, MainWindow, ShortTimeout);
                    }

                    LogDebug($"ClickMenuItem: Submenu '{menuItem}' found: {item != null}");
                }

                if (item == null)
                {
                    LogDebug($"ClickMenuItem: Failed to find menu item '{menuItem}'");
                    DumpAllElements(current, $"Looking for '{menuItem}'");
                    return false;
                }

                // Wait for menu item to be enabled
                WaitForElementEnabled(item, ShortTimeout);

                // Use mouse simulation for more reliable menu interaction
                ClickElementWithMouse(item);

                // Wait for application to process the click
                WaitForInputIdle();

                // Brief pause for menu animation
                Thread.Sleep(200);
            }

            // After clicking the final menu item, wait for the app to become idle
            WaitForInputIdle();

            // Wait for a potential child window to appear
            var initialWindowCount = app.GetAllTopLevelWindows(automation).Length;
            LogDebug($"ClickMenuItem: Initial window count: {initialWindowCount}");

            WaitForCondition(() =>
            {
                var currentWindows = app.GetAllTopLevelWindows(automation);
                return currentWindows.Length > initialWindowCount;
            }, TimeSpan.FromSeconds(3), "Waiting for child window");

            var finalWindowCount = app.GetAllTopLevelWindows(automation).Length;
            LogDebug($"ClickMenuItem: Final window count: {finalWindowCount}");

            return true;
        }
        catch (Exception ex)
        {
            LogDebug($"ClickMenuItem: Exception: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Click an element using mouse simulation for improved reliability.
    /// </summary>
    protected void ClickElementWithMouse(AutomationElement element)
    {
        var boundingRect = element.BoundingRectangle;
        var center = new Point(
            (int)(boundingRect.Left + boundingRect.Width / 2),
            (int)(boundingRect.Top + boundingRect.Height / 2));

        LogDebug($"ClickElementWithMouse: Moving to ({center.X}, {center.Y}) for element '{element.Name}'");

        Mouse.MoveTo(center);
        Thread.Sleep(50); // Brief pause after move
        Mouse.Click(MouseButton.Left);
    }

    /// <summary>
    /// Wait for the application to become idle (no pending input).
    /// </summary>
    protected void WaitForInputIdle(TimeSpan? timeout = null)
    {
        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(2);
        try
        {
            Application?.WaitWhileBusy(effectiveTimeout);
        }
        catch
        {
            // Ignore timeout - continue anyway
        }
    }

    /// <summary>
    /// Set text in a text box with wait for enabled.
    /// </summary>
    protected bool SetTextBoxValue(string textBoxName, string value, AutomationElement? parent = null)
    {
        var textBox = FindElementByName(textBoxName, parent);
        if (textBox == null) return false;

        try
        {
            WaitForElementEnabled(textBox, ShortTimeout);
            textBox.AsTextBox().Text = value;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get text from a text box.
    /// </summary>
    protected string? GetTextBoxValue(string textBoxName, AutomationElement? parent = null)
    {
        var textBox = FindElementByName(textBoxName, parent);
        return textBox?.AsTextBox().Text;
    }

    /// <summary>
    /// Select an item from a combo box by value with wait.
    /// </summary>
    protected bool SelectComboBoxItem(AutomationElement comboBox, string itemText)
    {
        try
        {
            WaitForElementEnabled(comboBox, ShortTimeout);
            var combo = comboBox.AsComboBox();
            combo.Expand();
            WaitForCondition(() => combo.ExpandCollapseState == FlaUI.Core.Definitions.ExpandCollapseState.Expanded, ShortTimeout);

            var items = combo.Items;
            var targetItem = items.FirstOrDefault(i => i.Name?.Contains(itemText, StringComparison.OrdinalIgnoreCase) == true);
            if (targetItem == null) return false;

            targetItem.Select();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Select a combo box item by index with validation.
    /// </summary>
    protected bool SelectComboBoxItemByIndex(AutomationElement comboBox, int index)
    {
        try
        {
            WaitForElementEnabled(comboBox, ShortTimeout);
            var combo = comboBox.AsComboBox();
            combo.Expand();
            WaitForCondition(() => combo.ExpandCollapseState == FlaUI.Core.Definitions.ExpandCollapseState.Expanded, ShortTimeout);

            var items = combo.Items;
            if (index < 0 || index >= items.Length) return false;

            items[index].Select();
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Screenshot and Debugging

    /// <summary>
    /// Take a screenshot for debugging purposes.
    /// </summary>
    protected void TakeScreenshot(string filename)
    {
        try
        {
            var outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Screenshots");
            Directory.CreateDirectory(outputDir);

            var fullPath = Path.Combine(outputDir, $"{filename}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            FlaUI.Core.Capturing.Capture.Screen().ToFile(fullPath);
        }
        catch
        {
            // Screenshot failed, continue test
        }
    }

    /// <summary>
    /// Execute a test action with screenshot on failure.
    /// </summary>
    protected void ExecuteWithScreenshotOnFailure(Action testAction, [CallerMemberName] string? testName = null)
    {
        try
        {
            testAction();
        }
        catch
        {
            TakeScreenshot($"FAILURE_{testName}");
            throw;
        }
    }

    /// <summary>
    /// Execute a test action with retry on transient failures using exponential backoff.
    /// Includes application state reset between retries.
    /// </summary>
    protected void ExecuteWithRetry(Action testAction, int maxRetries = 3, [CallerMemberName] string? testName = null)
    {
        Exception? lastException = null;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                LogDebug($"ExecuteWithRetry: Attempt {attempt}/{maxRetries} for {testName}");
                testAction();
                return; // Success
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                lastException = ex;
                LogDebug($"ExecuteWithRetry: Attempt {attempt} failed: {ex.Message}");
                TakeScreenshot($"RETRY_{attempt}_{testName}");

                // Try to reset application state for next attempt
                try
                {
                    CloseChildWindows();
                }
                catch
                {
                    // Ignore cleanup errors
                }

                // Exponential backoff: 1s, 2s, 4s, etc.
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
                Thread.Sleep(delay);
            }
        }

        // Final attempt failed
        LogDebug($"ExecuteWithRetry: All {maxRetries} attempts failed for {testName}");
        TakeScreenshot($"FAILURE_{testName}");
        throw lastException!;
    }

    /// <summary>
    /// Async version of ExecuteWithRetry.
    /// </summary>
    protected async Task ExecuteWithRetryAsync(Func<Task> testAction, int maxRetries = 3, [CallerMemberName] string? testName = null)
    {
        Exception? lastException = null;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await testAction();
                return; // Success
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                lastException = ex;
                TakeScreenshot($"RETRY_{attempt}_{testName}");

                // Exponential backoff: 1s, 2s, 4s, etc.
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
                await Task.Delay(delay);
            }
        }

        // Final attempt failed
        TakeScreenshot($"FAILURE_{testName}");
        throw lastException!;
    }

    #endregion

    #region Debug Logging

    /// <summary>
    /// Log a debug message (to Debug output and console).
    /// </summary>
    protected void LogDebug(string message)
    {
        if (!EnableVerboseLogging) return;

        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var formattedMessage = $"[FlaUI {timestamp}] {message}";

        Debug.WriteLine(formattedMessage);
        Console.WriteLine(formattedMessage);
    }

    /// <summary>
    /// Dump all descendant elements of a parent for debugging.
    /// </summary>
    protected void DumpAllElements(AutomationElement? parent, string context = "")
    {
        if (!EnableVerboseLogging) return;
        if (parent == null) return;

        try
        {
            var allElements = parent.FindAllDescendants();
            var elementNames = allElements
                .Where(e => !string.IsNullOrEmpty(e.Name))
                .Select(e => e.Name)
                .Distinct()
                .Take(50); // Limit output

            LogDebug($"DumpAllElements ({context}): [{string.Join(", ", elementNames)}]");
        }
        catch (Exception ex)
        {
            LogDebug($"DumpAllElements failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Dump detailed element information for debugging menu issues.
    /// </summary>
    protected void DumpElementDetails(AutomationElement? element, string label = "Element")
    {
        if (!EnableVerboseLogging || element == null) return;

        try
        {
            LogDebug($"{label}: Name='{element.Name}', " +
                     $"ControlType={element.ControlType}, " +
                     $"IsEnabled={element.IsEnabled}, " +
                     $"IsOffscreen={element.IsOffscreen}, " +
                     $"BoundingRect={element.BoundingRectangle}");
        }
        catch (Exception ex)
        {
            LogDebug($"DumpElementDetails failed: {ex.Message}");
        }
    }

    #endregion

    #region Dispose

    /// <summary>
    /// Dispose resources.
    /// For shared state: Does NOT close the app (handled by fixture).
    /// For instance state: Closes and disposes resources.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // Clean up instance-level resources only
            if (_instanceApplication != null)
            {
                CloseApplication();
            }

            // Don't touch shared state - let the fixture handle it
        }

        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion
}
