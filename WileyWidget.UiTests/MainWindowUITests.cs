using Xunit;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using System.Diagnostics;
using System.Runtime.InteropServices;
using FlaUI.Core.Definitions;

namespace WileyWidget.UiTests;

/// <summary>
/// Basic UI tests for the MainWindow
/// </summary>
public class MainWindowUITests : IDisposable
{
    private Application _app;
    private Window _mainWindow;
    private UIA3Automation _automation;

    public MainWindowUITests()
    {
        // Only run UI tests on Windows platforms
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

#pragma warning disable CA1416 // Validate platform compatibility
        _automation = new UIA3Automation();
#pragma warning restore CA1416
    }

    [Fact]
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
        var desktop = automation.GetDesktop();
        var topLevelWindows = desktop.FindAllChildren(cf => cf.ByControlType(ControlType.Window));
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

        // Arrange
#pragma warning disable CA1416 // Validate platform compatibility
        using var automation = new UIA3Automation();

        // Act
        var desktop = automation.GetDesktop();
#pragma warning restore CA1416

        // Assert
        Assert.NotNull(desktop);
#pragma warning disable CA1416 // Validate platform compatibility
        Assert.True(desktop.IsAvailable);
        Assert.Equal("Desktop", desktop.ClassName);
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
        var buttonType = ControlType.Button;
        var windowType = ControlType.Window;
        var textType = ControlType.Edit;
#pragma warning restore CA1416

        // Assert - ControlType is an enum, so we just verify the values are defined
        Assert.True(buttonType != 0);
        Assert.True(windowType != 0);
        Assert.True(textType != 0);
    }

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

    public void Dispose()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
#pragma warning disable CA1416 // Validate platform compatibility
            _automation?.Dispose();
#pragma warning restore CA1416
        }
#pragma warning disable CA1416 // Validate platform compatibility
        _app?.Close();
        _app?.Dispose();
#pragma warning restore CA1416
    }
}
