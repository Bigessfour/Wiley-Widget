using System;
using System.Diagnostics;
using System.IO;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using Xunit;

namespace WileyWidget.Tests.E2E;

/// <summary>
/// Base class for WPF E2E tests using FlaUI.
/// Handles application lifecycle and provides common helpers.
/// </summary>
public abstract class WpfTestBase : IDisposable
{
    protected Application? App { get; private set; }
    protected UIA3Automation Automation { get; }
    protected Window? MainWindow { get; private set; }

    protected WpfTestBase()
    {
        Automation = new UIA3Automation();
    }

    /// <summary>
    /// Launches the WPF application and waits for the main window.
    /// </summary>
    /// <param name="exePath">Full path to the .exe file.</param>
    /// <param name="timeoutSeconds">Maximum time to wait for app launch.</param>
    protected void LaunchApplication(string exePath, int timeoutSeconds = 30)
    {
        if (!File.Exists(exePath))
        {
            throw new FileNotFoundException($"Application not found: {exePath}");
        }

        App = FlaUI.Core.Application.Launch(exePath);
        App.WaitWhileBusy(TimeSpan.FromSeconds(timeoutSeconds));

        MainWindow = App.GetMainWindow(Automation, TimeSpan.FromSeconds(timeoutSeconds));
        Assert.NotNull(MainWindow);
    }

    /// <summary>
    /// Finds an element by its AutomationId property.
    /// </summary>
    protected FlaUI.Core.AutomationElements.AutomationElement? FindElementByAutomationId(string automationId)
    {
        return MainWindow?.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
    }

    /// <summary>
    /// Finds an element by its Name property.
    /// </summary>
    protected FlaUI.Core.AutomationElements.AutomationElement? FindElementByName(string name)
    {
        return MainWindow?.FindFirstDescendant(cf => cf.ByName(name));
    }

    /// <summary>
    /// Finds an element by its ClassName property.
    /// </summary>
    protected FlaUI.Core.AutomationElements.AutomationElement? FindElementByClassName(string className)
    {
        return MainWindow?.FindFirstDescendant(cf => cf.ByClassName(className));
    }

    /// <summary>
    /// Waits for an element to appear with retry logic.
    /// </summary>
    protected FlaUI.Core.AutomationElements.AutomationElement? WaitForElement(Func<FlaUI.Core.AutomationElements.AutomationElement?> finder, int timeoutSeconds = 10)
    {
        if (finder == null)
        {
            throw new ArgumentNullException(nameof(finder));
        }

        var stopwatch = Stopwatch.StartNew();
        FlaUI.Core.AutomationElements.AutomationElement? element = null;

        while (stopwatch.Elapsed.TotalSeconds < timeoutSeconds)
        {
            element = finder();
            if (element != null)
            {
                return element;
            }

            System.Threading.Thread.Sleep(100);
        }

        return null;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            MainWindow?.Close();
            App?.Close();
            App?.Dispose();
            Automation.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
