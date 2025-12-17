using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using System;
using System.Linq;

namespace WileyWidget.WinForms.E2ETests.Helpers;

/// <summary>
/// Centralized navigation helper for UI tests. Provides unified logic for clicking navigation buttons
/// and finding form/window elements using MainWindow descendant search with multiple fallback strategies.
/// </summary>
public static class NavigationHelper
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Opens a view by clicking navigation button and finding the resulting window/form.
    /// Searches MainWindow descendants first (for MDI children), then desktop (for separate windows).
    /// </summary>
    /// <param name="automation">FlaUI automation instance</param>
    /// <param name="mainWindow">Main application window</param>
    /// <param name="navAutomationId">Navigation button AutomationId (e.g., "Nav_Dashboard")</param>
    /// <param name="expectedFormIdentifier">Form title substring, AutomationId, or AccessibleName to search for</param>
    /// <param name="timeout">Optional timeout for finding form (default 30s)</param>
    /// <returns>Window element for the opened view</returns>
    /// <exception cref="InvalidOperationException">Thrown if navigation fails or form not found</exception>
    public static Window OpenView(
        AutomationBase automation,
        Window mainWindow,
        string navAutomationId,
        string expectedFormIdentifier,
        TimeSpan? timeout = null)
    {
        if (automation == null) throw new ArgumentNullException(nameof(automation));
        if (mainWindow == null) throw new ArgumentNullException(nameof(mainWindow));
        if (string.IsNullOrWhiteSpace(navAutomationId)) throw new ArgumentException("Navigation AutomationId required", nameof(navAutomationId));
        if (string.IsNullOrWhiteSpace(expectedFormIdentifier)) throw new ArgumentException("Form identifier required", nameof(expectedFormIdentifier));

        var maxWait = timeout ?? DefaultTimeout;

        // Step 1: Find and click navigation button
        var navButton = FindNavigationButton(mainWindow, navAutomationId, expectedFormIdentifier);
        if (navButton == null)
        {
            throw new InvalidOperationException(
                $"Navigation button not found: AutomationId='{navAutomationId}', fallback identifier='{expectedFormIdentifier}'. " +
                $"Ensure navigation ribbon/panel has loaded and button exists.");
        }

        // Wait for button to be responsive before clicking
        var clickResult = Retry.WhileException(() =>
        {
            if (!navButton.IsEnabled || navButton.IsOffscreen)
            {
                throw new InvalidOperationException("Navigation button not ready");
            }

            if (navButton.Patterns.Invoke.IsSupported)
            {
                navButton.Patterns.Invoke.Pattern.Invoke();
            }
            else
            {
                navButton.Click();
            }
        }, TimeSpan.FromSeconds(3));

        if (!clickResult.Success)
        {
            throw new InvalidOperationException($"Failed to click navigation button '{navAutomationId}'");
        }

        // Step 2: Search for form window using multiple strategies
        var formWindow = Retry.WhileNull(() =>
        {
            // Strategy 1: Find as MDI child (descendant of MainWindow)
            var childElement = FindFormAsChild(mainWindow, expectedFormIdentifier);
            if (childElement != null)
            {
                // If found as Window type, return directly
                if (childElement.ControlType == ControlType.Window)
                {
                    return childElement.AsWindow();
                }

                // If found as Pane/Custom (docked form), traverse up to find parent Window
                var parentWindow = TraverseToParentWindow(childElement);
                if (parentWindow != null)
                {
                    return parentWindow;
                }
            }

            // Strategy 2: Find as separate window on desktop (non-MDI mode)
            var separateWindow = FindFormAsDesktopWindow(automation, expectedFormIdentifier);
            if (separateWindow != null)
            {
                return separateWindow;
            }

            return null;
        }, maxWait, TimeSpan.FromMilliseconds(250)).Result;

        if (formWindow == null)
        {
            throw new InvalidOperationException(
                $"View window not found after navigation. Expected identifier: '{expectedFormIdentifier}'. " +
                $"Searched MainWindow descendants (MDI) and desktop windows (non-MDI). " +
                $"Verify form opens correctly and has AutomationId/AccessibleName/Name property matching identifier.");
        }

        return formWindow;
    }

    /// <summary>
    /// Finds form element as a child of MainWindow (for MDI or docked forms).
    /// Searches by Name (exact and substring), AutomationId, and AccessibleName.
    /// </summary>
    private static AutomationElement? FindFormAsChild(Window mainWindow, string identifier)
    {
        try
        {
            // Try exact name match first
            var exactMatch = mainWindow.FindFirstDescendant(cf => cf.ByName(identifier));
            if (exactMatch != null && exactMatch.IsAvailable)
            {
                return exactMatch;
            }

            // Try substring name match
            var substringMatch = mainWindow.FindAllDescendants().FirstOrDefault(el =>
                !string.IsNullOrEmpty(el.Name) &&
                el.Name.Contains(identifier, StringComparison.OrdinalIgnoreCase) &&
                el.IsAvailable);
            if (substringMatch != null)
            {
                return substringMatch;
            }

            // Try AutomationId
            var automationIdMatch = mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(identifier));
            if (automationIdMatch != null && automationIdMatch.IsAvailable)
            {
                return automationIdMatch;
            }

            // Try by ControlType.Window with name containing identifier
            var windowMatch = mainWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.Window))
                .FirstOrDefault(el => !string.IsNullOrEmpty(el.Name) &&
                                     el.Name.Contains(identifier, StringComparison.OrdinalIgnoreCase) &&
                                     el.IsAvailable);
            if (windowMatch != null)
            {
                return windowMatch;
            }

            // Try by ControlType.Pane (for docked forms)
            var paneMatch = mainWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.Pane))
                .FirstOrDefault(el => !string.IsNullOrEmpty(el.Name) &&
                                     el.Name.Contains(identifier, StringComparison.OrdinalIgnoreCase) &&
                                     el.IsAvailable);
            if (paneMatch != null)
            {
                return paneMatch;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Finds form as a separate window on the desktop (for non-MDI mode).
    /// Searches all top-level windows by name substring.
    /// </summary>
    private static Window? FindFormAsDesktopWindow(AutomationBase automation, string identifier)
    {
        try
        {
            var desktop = automation.GetDesktop();
            var windows = desktop.FindAllChildren(cf => cf.ByControlType(ControlType.Window));

            foreach (var window in windows)
            {
                if (!string.IsNullOrEmpty(window.Name) &&
                    window.Name.Contains(identifier, StringComparison.OrdinalIgnoreCase))
                {
                    return window.AsWindow();
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Traverses up the UI tree from an element to find its parent Window.
    /// Useful for docked forms that are Pane/Custom controls inside an MDI parent.
    /// </summary>
    private static Window? TraverseToParentWindow(AutomationElement element)
    {
        try
        {
            var current = element.Parent;
            while (current != null)
            {
                if (current.ControlType == ControlType.Window)
                {
                    return current.AsWindow();
                }
                current = current.Parent;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Finds navigation button using AutomationId with comprehensive fallback strategies.
    /// Searches ribbon controls, navigation strips, and toolbars.
    /// </summary>
    private static AutomationElement? FindNavigationButton(Window mainWindow, string navAutomationId, string fallbackName)
    {
        var result = Retry.WhileNull(() =>
        {
            try
            {
                var trimmedName = navAutomationId.StartsWith("Nav_", StringComparison.OrdinalIgnoreCase)
                    ? navAutomationId.Substring(4)
                    : navAutomationId;
                var friendlyName = trimmedName.Replace('_', ' ');

                // Strategy 1: Try exact AutomationId match (most reliable)
                var byId = mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(navAutomationId));
                if (byId != null && byId.IsAvailable && !byId.IsOffscreen)
                {
                    return byId;
                }

                // Strategy 2: Try exact Name match
                var byName = mainWindow.FindFirstDescendant(cf => cf.ByName(navAutomationId));
                if (byName != null && byName.IsAvailable && !byName.IsOffscreen)
                {
                    return byName;
                }

                // Strategy 3: Try fallback form name
                var byFallback = mainWindow.FindFirstDescendant(cf => cf.ByName(fallbackName));
                if (byFallback != null && byFallback.IsAvailable && !byFallback.IsOffscreen)
                {
                    return byFallback;
                }

                // Strategy 4: Look in ribbon/toolbar controls specifically
                var allDescendants = mainWindow.FindAllDescendants();
                var ribbonOrToolbars = allDescendants
                    .Where(el => el.ControlType == ControlType.ToolBar || el.ControlType == ControlType.Menu)
                    .SelectMany(el => el.FindAllDescendants())
                    .FirstOrDefault(el => !string.IsNullOrEmpty(el.Name) &&
                                         (el.Name.Equals(navAutomationId, StringComparison.OrdinalIgnoreCase) ||
                                          el.Name.Equals(friendlyName, StringComparison.OrdinalIgnoreCase)) &&
                                         el.IsAvailable && !el.IsOffscreen);
                if (ribbonOrToolbars != null)
                {
                    return ribbonOrToolbars;
                }

                // Strategy 5: Substring search across all descendants (least strict, fallback)
                var bySubstring = allDescendants
                    .FirstOrDefault(el => !string.IsNullOrEmpty(el.Name) &&
                                         (el.Name.Contains(navAutomationId, StringComparison.OrdinalIgnoreCase) ||
                                          el.Name.Contains(fallbackName, StringComparison.OrdinalIgnoreCase) ||
                                          el.Name.Contains(trimmedName, StringComparison.OrdinalIgnoreCase) ||
                                          el.Name.Contains(friendlyName, StringComparison.OrdinalIgnoreCase)) &&
                                         el.IsAvailable && !el.IsOffscreen);
                if (bySubstring != null)
                {
                    return bySubstring;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }, ShortTimeout, TimeSpan.FromMilliseconds(250)).Result;

        return result;
    }

    /// <summary>
    /// Finds a specific child control within a form (e.g., grid, button, textbox).
    /// Useful for PageObject initializations to verify form structure.
    /// </summary>
    /// <param name="parent">Parent window or control</param>
    /// <param name="controlIdentifier">AutomationId, Name, or substring of Name</param>
    /// <param name="controlType">Expected control type (optional)</param>
    /// <param name="timeout">Optional timeout (default 10s)</param>
    /// <returns>Found element or null</returns>
    public static AutomationElement? FindChildControl(
        AutomationElement parent,
        string controlIdentifier,
        ControlType? controlType = null,
        TimeSpan? timeout = null)
    {
        if (parent == null) throw new ArgumentNullException(nameof(parent));
        if (string.IsNullOrWhiteSpace(controlIdentifier)) throw new ArgumentException("Control identifier required", nameof(controlIdentifier));

        var maxWait = timeout ?? ShortTimeout;

        var result = Retry.WhileNull(() =>
        {
            try
            {
                // Try AutomationId
                var byId = controlType.HasValue
                    ? parent.FindFirstDescendant(cf => cf.ByAutomationId(controlIdentifier).And(cf.ByControlType(controlType.Value)))
                    : parent.FindFirstDescendant(cf => cf.ByAutomationId(controlIdentifier));

                if (byId != null && byId.IsAvailable)
                {
                    return byId;
                }

                // Try exact Name
                var byName = controlType.HasValue
                    ? parent.FindFirstDescendant(cf => cf.ByName(controlIdentifier).And(cf.ByControlType(controlType.Value)))
                    : parent.FindFirstDescendant(cf => cf.ByName(controlIdentifier));

                if (byName != null && byName.IsAvailable)
                {
                    return byName;
                }

                // Try substring Name
                var allElements = controlType.HasValue
                    ? parent.FindAllDescendants(cf => cf.ByControlType(controlType.Value))
                    : parent.FindAllDescendants();

                var bySubstring = allElements.FirstOrDefault(el =>
                    !string.IsNullOrEmpty(el.Name) &&
                    el.Name.Contains(controlIdentifier, StringComparison.OrdinalIgnoreCase) &&
                    el.IsAvailable);

                if (bySubstring != null)
                {
                    return bySubstring;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }, maxWait, TimeSpan.FromMilliseconds(250)).Result;

        return result;
    }

    /// <summary>
    /// Closes a view window by clicking the close button or using window close.
    /// </summary>
    public static void CloseView(Window viewWindow, TimeSpan? timeout = null)
    {
        if (viewWindow == null) return;

        var maxWait = timeout ?? ShortTimeout;

        try
        {
            // Try to find and click the close button (X button)
            var closeButton = Retry.WhileNull(() =>
                viewWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName("Close"))), maxWait).Result;
            if (closeButton != null)
            {
                closeButton.AsButton().Click();
                return;
            }

            // Fallback: use window close method
            viewWindow.Close();
        }
        catch
        {
            // Ignore if close fails
        }
    }
}
