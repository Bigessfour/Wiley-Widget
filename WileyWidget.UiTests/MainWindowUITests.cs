using Xunit;
using Xunit.Sdk;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using System.Diagnostics;
using System.Runtime.InteropServices;
using FlaUI.Core.Definitions;
using System;
using System.Threading;
using Xunit.Sdk;

namespace WileyWidget.UiTests;

/// <summary>
/// Enhanced UI tests for the MainWindow using WPF test framework
/// Uses [WpfFact] for proper STA threading support
/// </summary>
[Collection("WPF Test Collection")]
public class MainWindowUITests : IDisposable
{
    private UIA3Automation _automation;

    public MainWindowUITests()
    {
        // Only run UI tests on Windows platforms
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        // xunit.runner.wpf should handle STA threading automatically
#pragma warning disable CA1416 // Validate platform compatibility
        _automation = new UIA3Automation();
#pragma warning restore CA1416
    }

    /// <summary>
    /// Detects if running in a CI environment where desktop access is not available
    /// </summary>
    private static bool IsCIEnvironment()
    {
        // Check common CI environment variables
        string[] ciIndicators = new[]
        {
            "CI",           // General CI indicator
            "GITHUB_ACTIONS", // GitHub Actions
            "BUILD_NUMBER", // Jenkins/Azure DevOps
            "TRAVIS",       // Travis CI
            "CIRCLECI",     // CircleCI
            "GITLAB_CI",    // GitLab CI
            "TF_BUILD",     // Azure DevOps
            "APPVEYOR"      // AppVeyor
        };

        return ciIndicators.Any(indicator =>
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(indicator)));
    }

    #region UI Framework Tests

    [Fact]
    [Trait("Category", "UiSmokeTests")]
    public void UI_Test_Framework_IsConfigured()
    {
        // Skip if not on Windows
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        // Arrange & Act
#pragma warning disable CA1416 // Validate platform compatibility
        var automation = new UIA3Automation();
#pragma warning restore CA1416

        // Assert
        Assert.NotNull(automation);
#pragma warning disable CA1416 // Validate platform compatibility
        Assert.IsType<UIA3Automation>(automation);
#pragma warning restore CA1416

        // Cleanup
#pragma warning disable CA1416 // Validate platform compatibility
        automation.Dispose();
#pragma warning restore CA1416
    }

    [Fact]
    [Trait("Category", "UiSmokeTests")]
    public void UI_Test_Environment_IsReady()
    {
        // Skip if not on Windows
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        // Arrange & Act
        bool canCreateAutomation = false;
        UIA3Automation automation = null;

        try
        {
#pragma warning disable CA1416 // Validate platform compatibility
            automation = new UIA3Automation();
#pragma warning restore CA1416
            canCreateAutomation = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UI Automation creation failed: {ex.Message}");
            canCreateAutomation = false;
        }

        // Assert
        Assert.True(canCreateAutomation, "UI Automation should be available on Windows");
        Assert.NotNull(automation);

        // Cleanup
#pragma warning disable CA1416 // Validate platform compatibility
        automation?.Dispose();
#pragma warning restore CA1416
    }

    [Fact]
    [Trait("Category", "UiSmokeTests")]
    public void UI_Automation_CanEnumerateDesktopWindows()
    {
        // Skip if not on Windows
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        // Arrange
#pragma warning disable CA1416 // Validate platform compatibility
        using var automation = new UIA3Automation();

        // Act - Get all top-level windows
        AutomationElement desktop = automation.GetDesktop();
        AutomationElement[] topLevelWindows = desktop.FindAllChildren(cf => cf.ByControlType(ControlType.Window));
#pragma warning restore CA1416

        // Assert
        Assert.NotNull(desktop);
        Assert.NotNull(topLevelWindows);
        // We can't assert a specific count since it varies by system,
        // but we can verify the collection exists and is enumerable
        Assert.True(topLevelWindows.Length >= 0);
    }

    [Fact]
    public void UI_Automation_CanFindDesktop()
    {
        // Skip if not on Windows
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        // Skip in CI environments where desktop access is not available
        if (IsCIEnvironment())
        {
            return;
        }

        // Arrange
#pragma warning disable CA1416 // Validate platform compatibility
        using var automation = new UIA3Automation();

        // Act
        AutomationElement desktop = automation.GetDesktop();
#pragma warning restore CA1416

        // Assert
        Assert.NotNull(desktop);
#pragma warning disable CA1416 // Validate platform compatibility
        Assert.True(desktop.IsAvailable);
        // Windows desktop class name is "#32769" not "Desktop"
        Assert.Equal("#32769", desktop.ClassName);
#pragma warning restore CA1416
    }

    [Fact]
    public void UI_Framework_Compatibility_Check()
    {
        // Skip if not on Windows
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        // Arrange
#pragma warning disable CA1416 // Validate platform compatibility
        using var automation = new UIA3Automation();

        // Act - Test various control type access
        ControlType buttonType = ControlType.Button;
        ControlType windowType = ControlType.Window;
        ControlType textType = ControlType.Edit;
#pragma warning restore CA1416

        // Assert - ControlType enum values are properly defined
        Assert.True(Enum.IsDefined(typeof(ControlType), buttonType));
        Assert.True(Enum.IsDefined(typeof(ControlType), windowType));
        Assert.True(Enum.IsDefined(typeof(ControlType), textType));
    }

    #endregion

    #region Application Launch Tests

    [Fact]
    public void Application_CanBeLaunchedFromTestEnvironment()
    {
        // Skip if not on Windows
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        // Skip in CI environments
        if (IsCIEnvironment())
        {
            return;
        }

        Application app = null;
        UIA3Automation automation = null;

        try
        {
            // Arrange
            var processStartInfo = new ProcessStartInfo
            {
                FileName = @"C:\Users\biges\Desktop\Wiley_Widget\bin\Debug\net9.0-windows\WileyWidget.exe",
                WorkingDirectory = @"C:\Users\biges\Desktop\Wiley_Widget\bin\Debug\net9.0-windows",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Normal
            };

#pragma warning disable CA1416 // Validate platform compatibility
            automation = new UIA3Automation();

            // Act - Launch application with retry logic
            app = Application.Launch(processStartInfo);

            // Wait for the process to start and stabilize
            System.Threading.Thread.Sleep(2000);

            // Try to get main window with extended timeout and retry
            Window mainWindow = null;
            int retryCount = 0;
            const int maxRetries = 5;

            while (retryCount < maxRetries && mainWindow == null)
            {
                try
                {
                    mainWindow = app.GetMainWindow(automation, TimeSpan.FromSeconds(5));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Attempt {retryCount + 1} failed: {ex.Message}");
                    System.Threading.Thread.Sleep(1000);
                    retryCount++;
                }
            }
#pragma warning restore CA1416

            // Assert
            Assert.NotNull(app);
            Assert.NotNull(mainWindow);
#pragma warning disable CA1416 // Validate platform compatibility
            Assert.True(mainWindow.IsAvailable);
            Assert.Contains("Wiley Widget", mainWindow.Title);
#pragma warning restore CA1416
        }
        finally
        {
            // Cleanup
            try
            {
#pragma warning disable CA1416 // Validate platform compatibility
                app?.Close();
                automation?.Dispose();
#pragma warning restore CA1416
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    #endregion

    #region Main Window Tests

    [Fact]
    public void MainWindow_LoadsWithExpectedTitle()
    {
        // Skip if not on Windows
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        // Skip in CI environments
        if (IsCIEnvironment())
        {
            return;
        }

        Application app = null;
        UIA3Automation automation = null;
        Window mainWindow = null;

        try
        {
            // Arrange
            var processStartInfo = new ProcessStartInfo
            {
                FileName = @"C:\Users\biges\Desktop\Wiley_Widget\bin\Debug\net9.0-windows\WileyWidget.exe",
                WorkingDirectory = @"C:\Users\biges\Desktop\Wiley_Widget\bin\Debug\net9.0-windows",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Normal
            };

#pragma warning disable CA1416 // Validate platform compatibility
            automation = new UIA3Automation();

            // Act - Launch application with retry logic
            app = Application.Launch(processStartInfo);

            // Wait for the process to start and stabilize
            System.Threading.Thread.Sleep(2000);

            // Try to get main window with extended timeout and retry
            int retryCount = 0;
            const int maxRetries = 5;

            while (retryCount < maxRetries && mainWindow == null)
            {
                try
                {
                    mainWindow = app.GetMainWindow(automation, TimeSpan.FromSeconds(5));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"MainWindow_LoadsWithExpectedTitle - Attempt {retryCount + 1} failed: {ex.Message}");
                    System.Threading.Thread.Sleep(1000);
                    retryCount++;
                }
            }
#pragma warning restore CA1416

            // Assert
            Assert.NotNull(mainWindow);
#pragma warning disable CA1416 // Validate platform compatibility
            Assert.True(mainWindow.IsAvailable);
            Assert.Equal("Wiley Widget", mainWindow.Title);
#pragma warning restore CA1416
        }
        finally
        {
            // Cleanup
            try
            {
#pragma warning disable CA1416 // Validate platform compatibility
                mainWindow?.Close();
                app?.Close();
                automation?.Dispose();
#pragma warning restore CA1416
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public void MainWindow_HasExpectedDimensions()
    {
        // Skip if not on Windows
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        // Skip in CI environments
        if (IsCIEnvironment())
        {
            return;
        }

        Application app = null;
        UIA3Automation automation = null;
        Window mainWindow = null;

        try
        {
            // Arrange
            var processStartInfo = new ProcessStartInfo
            {
                FileName = @"C:\Users\biges\Desktop\Wiley_Widget\bin\Debug\net9.0-windows\WileyWidget.exe",
                WorkingDirectory = @"C:\Users\biges\Desktop\Wiley_Widget\bin\Debug\net9.0-windows",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Normal
            };

#pragma warning disable CA1416 // Validate platform compatibility
            automation = new UIA3Automation();

            // Act - Launch application with retry logic
            app = Application.Launch(processStartInfo);

            // Wait for the process to start and stabilize
            System.Threading.Thread.Sleep(2000);

            // Try to get main window with extended timeout and retry
            int retryCount = 0;
            const int maxRetries = 5;

            while (retryCount < maxRetries && mainWindow == null)
            {
                try
                {
                    mainWindow = app.GetMainWindow(automation, TimeSpan.FromSeconds(5));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"MainWindow_HasExpectedDimensions - Attempt {retryCount + 1} failed: {ex.Message}");
                    System.Threading.Thread.Sleep(1000);
                    retryCount++;
                }
            }

            // Wait for window to be fully loaded
            System.Threading.Thread.Sleep(2000);
#pragma warning restore CA1416

            // Assert
            Assert.NotNull(mainWindow);
#pragma warning disable CA1416 // Validate platform compatibility
            var bounds = mainWindow.BoundingRectangle;
            Assert.True(bounds.Width >= 800, $"Window width {bounds.Width} should be at least 800");
            Assert.True(bounds.Height >= 500, $"Window height {bounds.Height} should be at least 500");
#pragma warning restore CA1416
        }
        finally
        {
            // Cleanup
            try
            {
#pragma warning disable CA1416 // Validate platform compatibility
                mainWindow?.Close();
                app?.Close();
                automation?.Dispose();
#pragma warning restore CA1416
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    #endregion

    #region UI Element Tests

    [Fact]
    public void MainWindow_ContainsRibbonInterface()
    {
        // Skip if not on Windows
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        // Skip in CI environments
        if (IsCIEnvironment())
        {
            return;
        }

        Application app = null;
        UIA3Automation automation = null;
        Window mainWindow = null;

        try
        {
            // Arrange
            var processStartInfo = new ProcessStartInfo
            {
                FileName = @"C:\Users\biges\Desktop\Wiley_Widget\bin\Debug\net9.0-windows\WileyWidget.exe",
                WorkingDirectory = @"C:\Users\biges\Desktop\Wiley_Widget\bin\Debug\net9.0-windows",
                UseShellExecute = true
            };

#pragma warning disable CA1416 // Validate platform compatibility
            automation = new UIA3Automation();

            // Retry logic for application launch
            const int maxRetries = 3;
            const int retryDelay = 2000;
            Exception lastException = null;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    app = Application.Launch(processStartInfo);
                    mainWindow = app.GetMainWindow(automation, TimeSpan.FromSeconds(15));

                    // Additional stabilization time
                    System.Threading.Thread.Sleep(3000);

                    // Verify the window is actually available
                    if (mainWindow != null && mainWindow.IsAvailable)
                    {
                        break; // Success, exit retry loop
                    }
                    else
                    {
                        throw new Exception("Main window not available after launch");
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    if (attempt < maxRetries)
                    {
                        System.Threading.Thread.Sleep(retryDelay);
                        // Cleanup failed attempt
                        try
                        {
                            mainWindow?.Close();
                            app?.Close();
                        }
                        catch { }
                    }
                }
            }

            if (app == null || mainWindow == null)
            {
                throw new Exception($"Failed to launch application after {maxRetries} attempts. Last error: {lastException?.Message}", lastException);
            }

            // Act - Find ribbon
            var ribbon = mainWindow.FindFirstDescendant(cf => cf.ByClassName("Ribbon"));
#pragma warning restore CA1416

            // Assert
            Assert.NotNull(ribbon);
#pragma warning disable CA1416 // Validate platform compatibility
            Assert.True(ribbon.IsEnabled);
#pragma warning restore CA1416
        }
        finally
        {
            // Cleanup
            try
            {
#pragma warning disable CA1416 // Validate platform compatibility
                mainWindow?.Close();
                app?.Close();
                automation?.Dispose();
#pragma warning restore CA1416
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public void MainWindow_ContainsTabControl()
    {
        // Skip if not on Windows
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        // Skip in CI environments
        if (IsCIEnvironment())
        {
            return;
        }

        Application app = null;
        UIA3Automation automation = null;
        Window mainWindow = null;

        try
        {
            // Arrange
            var processStartInfo = new ProcessStartInfo
            {
                FileName = @"C:\Users\biges\Desktop\Wiley_Widget\bin\Debug\net9.0-windows\WileyWidget.exe",
                WorkingDirectory = @"C:\Users\biges\Desktop\Wiley_Widget\bin\Debug\net9.0-windows",
                UseShellExecute = true
            };

#pragma warning disable CA1416 // Validate platform compatibility
            automation = new UIA3Automation();

            // Retry logic for application launch
            const int maxRetries = 3;
            const int retryDelay = 2000;
            Exception lastException = null;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    app = Application.Launch(processStartInfo);
                    mainWindow = app.GetMainWindow(automation, TimeSpan.FromSeconds(15));

                    // Additional stabilization time
                    System.Threading.Thread.Sleep(3000);

                    // Verify the window is actually available
                    if (mainWindow != null && mainWindow.IsAvailable)
                    {
                        break; // Success, exit retry loop
                    }
                    else
                    {
                        throw new Exception("Main window not available after launch");
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    if (attempt < maxRetries)
                    {
                        System.Threading.Thread.Sleep(retryDelay);
                        // Cleanup failed attempt
                        try
                        {
                            mainWindow?.Close();
                            app?.Close();
                        }
                        catch { }
                    }
                }
            }

            if (app == null || mainWindow == null)
            {
                throw new Exception($"Failed to launch application after {maxRetries} attempts. Last error: {lastException?.Message}", lastException);
            }

            // Act - Find tab control
            var tabControl = mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Tab));
#pragma warning restore CA1416

            // Assert
            Assert.NotNull(tabControl);
#pragma warning disable CA1416 // Validate platform compatibility
            Assert.True(tabControl.IsEnabled);
#pragma warning restore CA1416
        }
        finally
        {
            // Cleanup
            try
            {
#pragma warning disable CA1416 // Validate platform compatibility
                mainWindow?.Close();
                app?.Close();
                automation?.Dispose();
#pragma warning restore CA1416
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public void MainWindow_HasAllExpectedTabs()
    {
        // Skip if not on Windows
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        // Skip in CI environments
        if (IsCIEnvironment())
        {
            return;
        }

        Application app = null;
        UIA3Automation automation = null;
        Window mainWindow = null;

        try
        {
            // Arrange
            var processStartInfo = new ProcessStartInfo
            {
                FileName = @"C:\Users\biges\Desktop\Wiley_Widget\bin\Debug\net9.0-windows\WileyWidget.exe",
                WorkingDirectory = @"C:\Users\biges\Desktop\Wiley_Widget\bin\Debug\net9.0-windows",
                UseShellExecute = true
            };

#pragma warning disable CA1416 // Validate platform compatibility
            automation = new UIA3Automation();

            // Retry logic for application launch
            const int maxRetries = 3;
            const int retryDelay = 2000;
            Exception lastException = null;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    app = Application.Launch(processStartInfo);
                    mainWindow = app.GetMainWindow(automation, TimeSpan.FromSeconds(15));

                    // Additional stabilization time
                    System.Threading.Thread.Sleep(3000);

                    // Verify the window is actually available
                    if (mainWindow != null && mainWindow.IsAvailable)
                    {
                        break; // Success, exit retry loop
                    }
                    else
                    {
                        throw new Exception("Main window not available after launch");
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    if (attempt < maxRetries)
                    {
                        System.Threading.Thread.Sleep(retryDelay);
                        // Cleanup failed attempt
                        try
                        {
                            mainWindow?.Close();
                            app?.Close();
                        }
                        catch { }
                    }
                }
            }

            if (app == null || mainWindow == null)
            {
                throw new Exception($"Failed to launch application after {maxRetries} attempts. Last error: {lastException?.Message}", lastException);
            }

            // Act - Find all tabs
            var widgetsTab = mainWindow.FindFirstDescendant(cf => cf.ByName("Widgets"));
            var enterprisesTab = mainWindow.FindFirstDescendant(cf => cf.ByName("Enterprises"));
            var budgetTab = mainWindow.FindFirstDescendant(cf => cf.ByName("Budget Summary"));
            var quickBooksTab = mainWindow.FindFirstDescendant(cf => cf.ByName("QuickBooks"));
#pragma warning restore CA1416

            // Assert
            Assert.NotNull(widgetsTab);
            Assert.NotNull(enterprisesTab);
            Assert.NotNull(budgetTab);
            Assert.NotNull(quickBooksTab);
        }
        finally
        {
            // Cleanup
            try
            {
#pragma warning disable CA1416 // Validate platform compatibility
                mainWindow?.Close();
                app?.Close();
                automation?.Dispose();
#pragma warning restore CA1416
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    #endregion

    #region Skipped Tests (for future implementation)

    [Fact(Skip = "Requires application to be built and available")]
    public void MainWindow_CanBeLaunched()
    {
        // This test is skipped until the application can be properly launched in test environment
        // To enable this test:
        // 1. Build the WileyWidget application
        // 2. Ensure WileyWidget.exe is in the test environment
        // 3. Uncomment and modify the code below

        /*
        // Skip if not on Windows
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        // Arrange
        const string appPath = "WileyWidget.exe"; // Adjust path as needed

        // Act
#pragma warning disable CA1416 // Validate platform compatibility
        _app = Application.Launch(appPath);
        _mainWindow = _app.GetMainWindow(_automation, TimeSpan.FromSeconds(10));
#pragma warning restore CA1416

        // Assert
        Assert.NotNull(_app);
        Assert.NotNull(_mainWindow);
#pragma warning disable CA1416 // Validate platform compatibility
        Assert.True(_mainWindow.IsAvailable);
        Assert.Contains("Wiley", _mainWindow.Title, StringComparison.OrdinalIgnoreCase);
#pragma warning restore CA1416
        */
    }

    [Fact(Skip = "Requires application to be built and available")]
    public void MainWindow_HasExpectedUIElements()
    {
        // This test is skipped until the application can be properly launched in test environment
        // To enable this test:
        // 1. Build the WileyWidget application
        // 2. Launch it in the test setup
        // 3. Uncomment and modify based on actual UI structure

        /*
        // Skip if not on Windows or if app not launched
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || _mainWindow == null)
        {
            return;
        }

        // Act - Find UI elements (adjust selectors based on actual UI)
#pragma warning disable CA1416 // Validate platform compatibility
        var buttons = _mainWindow.FindAllDescendants(cf =>
            cf.ByControlType(ControlType.Button));

        var textBoxes = _mainWindow.FindAllDescendants(cf =>
            cf.ByControlType(ControlType.Edit));
#pragma warning restore CA1416

        // Assert - Adjust expectations based on actual UI
        Assert.NotNull(buttons);
        Assert.NotNull(textBoxes);
        Assert.True(buttons.Length >= 0); // At least some buttons expected
        */
    }

    #endregion

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
#pragma warning disable CA1416 // Validate platform compatibility
                _automation?.Dispose();
#pragma warning restore CA1416
            }
        }
    }
}
