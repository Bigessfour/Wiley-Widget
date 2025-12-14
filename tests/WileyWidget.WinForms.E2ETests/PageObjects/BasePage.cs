using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace WileyWidget.WinForms.E2ETests.PageObjects;

/// <summary>
/// Base class for all page objects providing common FlaUI interaction patterns.
/// Implements retry logic, wait helpers, and element location strategies.
/// </summary>
public abstract class BasePage
{
    protected readonly AutomationBase Automation;
    protected readonly Window Window;
    protected readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);
    protected readonly TimeSpan ShortTimeout = TimeSpan.FromSeconds(3);

    protected BasePage(AutomationBase automation, Window window)
    {
        Automation = automation ?? throw new ArgumentNullException(nameof(automation));
        Window = window ?? throw new ArgumentNullException(nameof(window));
    }

    #region Element Locators

    protected AutomationElement? FindElementByAutomationId(string automationId, TimeSpan? timeout = null)
    {
        var cf = Automation.ConditionFactory;
        var condition = cf.ByAutomationId(automationId);
        return RetryFind(() => Window.FindFirstDescendant(condition), timeout ?? DefaultTimeout);
    }

    /// <summary>
    /// Find element by Name with retry and wait logic.
    /// </summary>
    protected AutomationElement? FindElementByName(string name, TimeSpan? timeout = null)
    {
        var cf = Automation.ConditionFactory;
        var condition = cf.ByName(name);
        return RetryFind(() => Window.FindFirstDescendant(condition), timeout ?? DefaultTimeout);
    }

    /// <summary>
    /// Find element by ControlType and AutomationId.
    /// </summary>
    protected AutomationElement? FindElementByTypeAndId(ControlType controlType, string automationId, TimeSpan? timeout = null)
    {
        var cf = new ConditionFactory((IPropertyLibrary)Automation);
        var condition = cf.ByControlType(controlType).And(cf.ByAutomationId(automationId));
        return RetryFind(() => Window.FindFirstDescendant(condition), timeout ?? DefaultTimeout);
    }

    /// <summary>
    /// Find element by ControlType and Name.
    /// </summary>
    protected AutomationElement? FindElementByTypeAndName(ControlType controlType, string name, TimeSpan? timeout = null)
    {
        var cf = new ConditionFactory((IPropertyLibrary)Automation);
        var condition = cf.ByControlType(controlType).And(cf.ByName(name));
        return RetryFind(() => Window.FindFirstDescendant(condition), timeout ?? DefaultTimeout);
    }

    /// <summary>
    /// Find all elements matching a condition.
    /// </summary>
    protected AutomationElement[] FindAllElements(ConditionBase condition)
    {
        return Window.FindAllDescendants(condition);
    }

    #endregion

    #region Wait Helpers

    /// <summary>
    /// Wait for element to be visible and responsive.
    /// </summary>
    protected bool WaitForElement(AutomationElement element, TimeSpan? timeout = null)
    {
        var maxWait = timeout ?? DefaultTimeout;
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < maxWait)
        {
            try
            {
#pragma warning disable CA1062 // Validate arguments of public methods
                if (element.IsAvailable && element.IsOffscreen == false)
                {
                    return true;
                }
#pragma warning restore CA1062 // Validate arguments of public methods
            }
            catch
            {
                // Element not ready yet
            }

            Thread.Sleep(100);
        }

        return false;
    }

    /// <summary>
    /// Wait for window to be ready (responsive and not busy).
    /// </summary>
    protected void WaitForWindowReady(TimeSpan? timeout = null)
    {
        var maxWait = timeout ?? DefaultTimeout;
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < maxWait)
        {
            try
            {
                if (Window.IsAvailable && !Window.IsModal)
                {
                    Thread.Sleep(200); // Small delay to ensure rendering completes
                    return;
                }
            }
            catch
            {
                // Window not ready yet
            }

            Thread.Sleep(100);
        }

        throw new TimeoutException($"Window did not become ready within {maxWait.TotalSeconds} seconds");
    }

    /// <summary>
    /// Wait for busy indicator to disappear (common in Syncfusion controls).
    /// </summary>
    protected void WaitForBusyIndicator(TimeSpan? timeout = null)
    {
        var maxWait = timeout ?? DefaultTimeout;
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < maxWait)
        {
            var busyIndicator = Window.FindFirstDescendant(cf => cf.ByName("BusyIndicator").Or(cf.ByControlType(ControlType.ProgressBar)));
            if (busyIndicator == null || busyIndicator.IsOffscreen)
            {
                return;
            }

            Thread.Sleep(100);
        }

        // Don't throw - busy indicator absence is common
    }

    #endregion

    #region Interaction Helpers

    /// <summary>
    /// Click element with retry logic.
    /// </summary>
    protected void Click(AutomationElement element)
    {
        if (element == null) throw new ArgumentNullException(nameof(element));

        WaitForElement(element);

        RetryAction(() =>
        {
            if (element.Patterns.Invoke.IsSupported)
            {
                element.Patterns.Invoke.Pattern.Invoke();
            }
            else
            {
                element.Click();
            }
        });

        Thread.Sleep(200); // Allow UI to respond
    }

    /// <summary>
    /// Set text value with retry logic.
    /// </summary>
    protected void SetText(AutomationElement element, string text)
    {
        if (element == null) throw new ArgumentNullException(nameof(element));

        WaitForElement(element);

        RetryAction(() =>
        {
            if (element.Patterns.Value.IsSupported)
            {
                element.Patterns.Value.Pattern.SetValue(text);
            }
            else
            {
                element.AsTextBox().Text = text;
            }
        });

        Thread.Sleep(100);
    }

    /// <summary>
    /// Get text value from element.
    /// </summary>
    protected string GetText(AutomationElement element)
    {
        if (element == null) throw new ArgumentNullException(nameof(element));

        if (element.Patterns.Value.IsSupported)
        {
            return element.Patterns.Value.Pattern.Value.Value ?? string.Empty;
        }

        return element.Properties.Name.ValueOrDefault ?? string.Empty;
    }

    /// <summary>
    /// Check if element is visible and enabled.
    /// </summary>
    protected bool IsVisible(AutomationElement element)
    {
        if (element == null) return false;

        try
        {
            return element.IsAvailable &&
                   element.IsOffscreen == false &&
                   element.IsEnabled;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Retry Logic

    /// <summary>
    /// Retry finding element with exponential backoff.
    /// </summary>
    protected AutomationElement? RetryFind(Func<AutomationElement?> findAction, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        var delay = 100;

        while (stopwatch.Elapsed < timeout)
        {
            try
            {
                var element = findAction();
                if (element != null && element.IsAvailable)
                {
                    return element;
                }
            }
            catch
            {
                // Suppress exceptions during retry
            }

            Thread.Sleep(delay);
            delay = Math.Min(delay * 2, 1000); // Max 1 second delay
        }

        return null; // Not found within timeout
    }

    /// <summary>
    /// Retry action with exponential backoff.
    /// </summary>
    private void RetryAction(Action action, int maxAttempts = 3)
    {
        var attempt = 0;
        Exception? lastException = null;

        while (attempt < maxAttempts)
        {
            try
            {
                action();
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
                attempt++;
                Thread.Sleep(200 * attempt);
            }
        }

        throw new InvalidOperationException(
            $"Action failed after {maxAttempts} attempts",
            lastException);
    }

    #endregion

    #region Syncfusion-Specific Helpers

    /// <summary>
    /// Find Syncfusion SfDataGrid by AutomationId or Name.
    /// </summary>
    protected AutomationElement? FindSfDataGrid(string identifier, TimeSpan? timeout = null)
    {
        // Try AutomationId first
        var grid = FindElementByAutomationId(identifier, timeout);
        if (grid != null) return grid;

        // Fallback to Name
        return FindElementByName(identifier, timeout);
    }

    /// <summary>
    /// Get row count from Syncfusion SfDataGrid.
    /// </summary>
    protected int GetGridRowCount(AutomationElement grid)
    {
        if (grid == null) return 0;

        var rows = grid.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem));
        return rows.Length;
    }

    /// <summary>
    /// Find Syncfusion DockingManager panel by name.
    /// </summary>
    protected AutomationElement? FindDockPanel(string panelName, TimeSpan? timeout = null)
    {
        var cf = Automation.ConditionFactory;
        var condition = cf.ByControlType(ControlType.Pane)
            .And(cf.ByName(panelName, PropertyConditionFlags.MatchSubstring));

        return RetryFind(() => Window.FindFirstDescendant(condition), timeout ?? DefaultTimeout);
    }

    #endregion
}
