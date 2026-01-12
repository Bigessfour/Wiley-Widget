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
        var cf = Automation.ConditionFactory;
        var condition = cf.ByControlType(controlType).And(cf.ByAutomationId(automationId));
        return RetryFind(() => Window.FindFirstDescendant(condition), timeout ?? DefaultTimeout);
    }

    /// <summary>
    /// Find element by ControlType and Name.
    /// </summary>
    protected AutomationElement? FindElementByTypeAndName(ControlType controlType, string name, TimeSpan? timeout = null)
    {
        var cf = Automation.ConditionFactory;
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

        try
        {
            var result = FlaUI.Core.Tools.Retry.WhileException(
                () =>
                {
#pragma warning disable CA1062 // Validate arguments of public methods
                    if (!element.IsAvailable || element.IsOffscreen)
                    {
                        throw new InvalidOperationException("Element not ready");
                    }
                    return true;
#pragma warning restore CA1062 // Validate arguments of public methods
                },
                maxWait,
                TimeSpan.FromMilliseconds(100));

            return result.Success && result.Result;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Wait for window to be ready (responsive and not busy).
    /// </summary>
    protected void WaitForWindowReady(TimeSpan? timeout = null)
    {
        var maxWait = timeout ?? DefaultTimeout;

        var result = FlaUI.Core.Tools.Retry.WhileException(
            () =>
            {
                if (!Window.IsAvailable || Window.IsModal)
                {
                    throw new InvalidOperationException("Window not ready");
                }
            },
            maxWait,
            TimeSpan.FromMilliseconds(100));

        if (!result.Success)
        {
            throw new TimeoutException("Window did not become ready within timeout");
        }
    }

    /// <summary>
    /// Wait for busy indicator to disappear (common in Syncfusion controls).
    /// </summary>
    protected void WaitForBusyIndicator(TimeSpan? timeout = null)
    {
        var maxWait = timeout ?? DefaultTimeout;

        try
        {
            FlaUI.Core.Tools.Retry.WhileTrue(
                () =>
                {
                    var busyIndicator = Window.FindFirstDescendant(cf => cf.ByName("BusyIndicator").Or(cf.ByControlType(ControlType.ProgressBar)));
                    return busyIndicator != null && !busyIndicator.IsOffscreen;
                },
                maxWait,
                TimeSpan.FromMilliseconds(100));
        }
        catch
        {
            // Don't throw - busy indicator absence is common
        }
    }

    #endregion

    #region Interaction Helpers

    /// <summary>
    /// Click element with retry logic and WaitUntilResponsive.
    /// </summary>
    protected void Click(AutomationElement element)
    {
        if (element == null) throw new ArgumentNullException(nameof(element));

        WaitForElement(element);

        // Use Retry.WhileException instead of custom retry
        var result = FlaUI.Core.Tools.Retry.WhileException(() =>
        {
            if (!element.IsEnabled || element.IsOffscreen)
            {
                throw new InvalidOperationException("Element not ready for click");
            }

            if (element.Patterns.Invoke.IsSupported)
            {
                element.Patterns.Invoke.Pattern.Invoke();
            }
            else
            {
                element.Click();
            }
        }, TimeSpan.FromSeconds(3));

        if (!result.Success)
        {
            throw new InvalidOperationException("Failed to click element");
        }
    }

    /// <summary>
    /// Set text value with retry logic.
    /// </summary>
    protected void SetText(AutomationElement element, string text)
    {
        if (element == null) throw new ArgumentNullException(nameof(element));

        WaitForElement(element);

        var result = FlaUI.Core.Tools.Retry.WhileException(() =>
        {
            if (!element.IsEnabled)
            {
                throw new InvalidOperationException("Element not enabled for text entry");
            }

            if (element.Patterns.Value.IsSupported)
            {
                element.Patterns.Value.Pattern.SetValue(text);
            }
            else
            {
                element.AsTextBox().Text = text;
            }
        }, TimeSpan.FromSeconds(3));

        if (!result.Success)
        {
            throw new InvalidOperationException("Failed to set text on element");
        }
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
    /// Retry finding element with FlaUI's Retry.WhileNull.
    /// </summary>
    protected AutomationElement? RetryFind(Func<AutomationElement?> findAction, TimeSpan timeout)
    {
        var result = FlaUI.Core.Tools.Retry.WhileNull(
            () =>
            {
                try
                {
                    var element = findAction();
                    if (element != null && element.IsAvailable)
                    {
                        return element;
                    }
                    return null;
                }
                catch
                {
                    return null; // Suppress exceptions during retry
                }
            },
            timeout,
            TimeSpan.FromMilliseconds(100));

        return result.Result;
    }

    /// <summary>
    /// Retry action with FlaUI's Retry.WhileException.
    /// </summary>
    private void RetryAction(Action action, int maxAttempts = 3)
    {
        var timeout = TimeSpan.FromSeconds(maxAttempts);
        var result = FlaUI.Core.Tools.Retry.WhileException(action, timeout, TimeSpan.FromMilliseconds(200));

        if (!result.Success)
        {
            throw new InvalidOperationException("Retry action failed");
        }
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
