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
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromSeconds(5);
    // Post-click delay increased to give DockingManager time to render and update UI tree
    private static readonly TimeSpan PostClickDelay = TimeSpan.FromMilliseconds(2000);

    /// <summary>
    /// Opens a view by clicking navigation button and finding the resulting window/form.
    /// Searches MainWindow descendants first (for docked panels), then desktop (for separate windows).
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

        // Wait for button to be responsive before clicking - focus first to ensure click triggers docking activation
        var clickResult = Retry.WhileException(() =>
        {
            if (!navButton.IsEnabled || navButton.IsOffscreen)
            {
                throw new InvalidOperationException("Navigation button not ready");
            }

            try
            {
                // Try to focus the element before invoking to ensure event handlers run on the correct control
                try { navButton.Focus(); } catch { }
                System.Threading.Thread.Sleep(200);

                if (navButton.Patterns.Invoke.IsSupported)
                {
                    navButton.Patterns.Invoke.Pattern.Invoke();
                }
                else
                {
                    navButton.Click();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NavigationHelper] Error invoking nav button: {ex.Message}");
                throw;
            }
        }, TimeSpan.FromSeconds(5));

        if (!clickResult.Success)
        {
            throw new InvalidOperationException($"Failed to click navigation button '{navAutomationId}'");
        }

        Console.WriteLine($"[NavigationHelper] Focused & invoked '{navAutomationId}', waiting for panel '{expectedFormIdentifier}' to appear...");

        // Wait longer to allow panel to fully render after click, especially for docked panels
        System.Threading.Thread.Sleep((int)PostClickDelay.TotalMilliseconds);

        // Step 2: Search for form window using multiple strategies
        var formWindow = Retry.WhileNull(() =>
        {
            // Strategy 1: Find as docked panel header label (Syncfusion often exposes a Text label inside the dock tab)
            var headerPanel = FindDockedPanel(mainWindow, expectedFormIdentifier, maxWait);
            if (headerPanel != null)
            {
                // If the found element is a Window type, return directly
                if (headerPanel.ControlType == ControlType.Window)
                {
                    return headerPanel.AsWindow();
                }

                // If the header label isn't itself a Window, try to find its ancestor Window
                var parentWindow = TraverseToParentWindow(headerPanel);
                if (parentWindow != null) return parentWindow;
                // Otherwise continue searching (we couldn't wrap the header as a Window)
            }

            // Strategy 2: Find as child by conventional means (Name/AutomationId/AccessibleName)
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

            // Strategy 3: Find as separate window on desktop (for separate forms)
            var separateWindow = FindFormAsDesktopWindow(automation, expectedFormIdentifier);
            if (separateWindow != null)
            {
                return separateWindow;
            }

            return null;
        }, maxWait, TimeSpan.FromMilliseconds(250)).Result;

        if (formWindow == null)
        {
            Console.WriteLine($"[NavigationHelper] FAILED to find panel '{expectedFormIdentifier}' after {maxWait.TotalSeconds}s. " +
                            $"Nav button '{navAutomationId}' was clicked successfully but panel did not appear.");
            // Dump diagnostic info to help triage - top panes and desktop windows
            try { DumpTopDescendants(mainWindow, 40); } catch { }
            try
            {
                var desktop = automation.GetDesktop();
                var tops = desktop.FindAllChildren().Take(20).Select(w => $"'{w.Name ?? "?"}'({w.ControlType})");
                Console.WriteLine($"[NavigationHelper] Desktop windows: {string.Join(", ", tops)}");
            }
            catch { }
            // Full tree dump for detailed debugging
            try { DumpAutomationTree(mainWindow, maxDepth: 4, maxChildren: 15); } catch { }

            throw new InvalidOperationException(
                $"View window not found after navigation. Expected identifier: '{expectedFormIdentifier}'. " +
                $"Searched MainWindow descendants (docked panels) and desktop windows (separate forms). " +
                $"Verify form opens correctly and has AutomationId/AccessibleName/Name property matching identifier.");
        }

        Console.WriteLine($"[NavigationHelper] Successfully found panel '{expectedFormIdentifier}' (control type: {formWindow.ControlType})");
        return formWindow;
    }

    /// <summary>
    /// Finds form element as a child of MainWindow (for docked forms).
    /// Searches by Name (exact and substring), AutomationId, and AccessibleName.
    /// Enhanced to find Syncfusion DockingManager panels by SetDockLabel name.
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

            // ENHANCED: For Syncfusion docked panels, search for panel with SetDockLabel matching identifier
            // Syncfusion panels may have Name property set to the dock label
            var allDescendants = mainWindow.FindAllDescendants();

            // Try substring name match with priority for Pane/Custom controls (typical for docked panels)
            var substringMatch = allDescendants.FirstOrDefault(el =>
                !string.IsNullOrEmpty(el.Name) &&
                el.Name.Contains(identifier, StringComparison.OrdinalIgnoreCase) &&
                el.IsAvailable &&
                (el.ControlType == ControlType.Pane || el.ControlType == ControlType.Custom));

            if (substringMatch != null)
            {
                Console.WriteLine($"[NavigationHelper] Found docked panel by name substring: '{substringMatch.Name}' (type: {substringMatch.ControlType})");
                return substringMatch;
            }

            // Fallback: Try substring match on any control type
            substringMatch = allDescendants.FirstOrDefault(el =>
                !string.IsNullOrEmpty(el.Name) &&
                el.Name.Contains(identifier, StringComparison.OrdinalIgnoreCase) &&
                el.IsAvailable);
            if (substringMatch != null)
            {
                Console.WriteLine($"[NavigationHelper] Found element by name substring: '{substringMatch.Name}' (type: {substringMatch.ControlType})");
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
    /// Finds form as a separate window on the desktop (for separate forms).
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
    /// Useful for docked forms that are Pane/Custom controls inside a parent window.
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
    /// Helper: checks whether AutomationElement matches identifier via common properties.
    /// </summary>
    private static bool MatchesIdentifier(AutomationElement el, string identifier)
    {
        if (el == null || string.IsNullOrWhiteSpace(identifier)) return false;
        try
        {
            if (!string.IsNullOrEmpty(el.Name) && el.Name.IndexOf(identifier, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            var aid = el.Properties.AutomationId?.Value?.ToString() ?? string.Empty;
            if (!string.IsNullOrEmpty(aid) && aid.IndexOf(identifier, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            var ht = el.Properties.HelpText?.Value?.ToString() ?? string.Empty;
            if (!string.IsNullOrEmpty(ht) && ht.IndexOf(identifier, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }
        catch { }
        return false;
    }

    /// <summary>
    /// Dumps the top Pane/Custom descendant names to the console for diagnostics.
    /// </summary>
    private static void DumpTopDescendants(AutomationElement root, int max = 30)
    {
        try
        {
            var panes = root.FindAllDescendants(cf => cf.ByControlType(ControlType.Pane).Or(cf.ByControlType(ControlType.Custom)));
            var list = panes.Take(max).Select(p => $"{(string.IsNullOrEmpty(p.Name) ? "?" : p.Name)}({p.ControlType})");
            Console.WriteLine($"[NavigationHelper] Top {max} panes: {string.Join(", ", list)}");
        }
        catch { }
    }

    /// <summary>
    /// Searches for a docked panel by locating its header label (ControlType.Text) containing the identifier.
    /// Enhanced to inspect parent content and fallback to broader island searches; emits diagnostics on failures.
    /// </summary>
    private static AutomationElement? FindDockedPanel(AutomationElement root, string identifier, TimeSpan timeout)
    {
        var endTime = DateTime.Now + timeout;
        while (DateTime.Now < endTime)
        {
            try
            {
                var allDescendants = root.FindAllDescendants();

                // Try to find a header label first (exact or substring match)
                var headerLabel = allDescendants.FirstOrDefault(el => el.ControlType == ControlType.Text && el.IsAvailable && MatchesIdentifier(el, identifier));
                if (headerLabel != null)
                {
                    var parent = headerLabel.Parent;
                    var parentTypeStr = parent != null ? parent.ControlType.ToString() : "null";
                    Console.WriteLine($"[NavigationHelper] Found docked header Label '{headerLabel.Name}' - parent: {parentTypeStr}");

                    // Walk up to nearest Pane/Custom/Window ancestor
                    while (parent != null && parent.ControlType != ControlType.Pane && parent.ControlType != ControlType.Custom && parent.ControlType != ControlType.Window)
                    {
                        parent = parent.Parent;
                    }

                    if (parent != null)
                    {
                        // Prefer a Pane/Custom descendant within this parent if present (actual content)
                        var content = parent.FindAllDescendants().FirstOrDefault(el => (el.ControlType == ControlType.Pane || el.ControlType == ControlType.Custom) && MatchesIdentifier(el, identifier));
                        if (content != null)
                        {
                            Console.WriteLine($"[NavigationHelper] Found content inside parent for identifier '{identifier}'");
                            return content;
                        }

                        Console.WriteLine($"[NavigationHelper] Returning parent panel (type: {parent.ControlType}) for identifier '{identifier}'");
                        return parent;
                    }

                    return headerLabel.Parent;
                }

                // Strategy 2: Direct Pane/Custom window with matching properties
                var directPanel = allDescendants.FirstOrDefault(el =>
                    (el.ControlType == ControlType.Pane || el.ControlType == ControlType.Custom || el.ControlType == ControlType.Window)
                    && MatchesIdentifier(el, identifier));
                if (directPanel != null)
                {
                    Console.WriteLine($"[NavigationHelper] Found direct panel '{directPanel.Name}' (type: {directPanel.ControlType})");
                    return directPanel;
                }

                // Strategy 3: Fallback substring match across all descendants
                var substringMatch = allDescendants.FirstOrDefault(el => !string.IsNullOrEmpty(el.Name) && el.Name.IndexOf(identifier, StringComparison.OrdinalIgnoreCase) >= 0);
                if (substringMatch != null)
                {
                    Console.WriteLine($"[NavigationHelper] Found element by name substring: '{substringMatch.Name}' (type: {substringMatch.ControlType})");
                    return substringMatch;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NavigationHelper] FindDockedPanel exception: {ex.Message}");
            }

            System.Threading.Thread.Sleep(500);
        }

        // On timeout, dump top panes for diagnostics and return null
        DumpTopDescendants(root, 20);
        return null;
    }

    /// <summary>
    /// Finds navigation button using AutomationId with comprehensive fallback strategies.
    /// Searches ribbon controls, navigation strips, and toolbars.
    /// </summary>
    private static AutomationElement? FindNavigationButton(Window mainWindow, string navAutomationId, string fallbackName)
    {
        // Primary: Aid
        var byAid = mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(navAutomationId));
        if (byAid != null) return byAid;

        // Fallback: Name substring (e.g., "Accounts" for Nav_Accounts)
        var fallbackId = navAutomationId.Replace("Nav_", "");
        var byName = Retry.WhileNull(() =>
            mainWindow.FindFirstDescendant(cf =>
                cf.ByControlType(ControlType.Button).And(
                    new OrCondition(cf.ByName(fallbackId), cf.ByName(navAutomationId))
                )), TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(500)).Result;
        return byName;
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
    /// Dumps the automation tree starting from the root element, up to a maximum depth.
    /// Useful for debugging UI element discovery issues.
    /// </summary>
    public static void DumpAutomationTree(AutomationElement root, int maxDepth = 3, int maxChildren = 10)
    {
        Console.WriteLine($"[TreeDump] Automation Tree for '{root.Name ?? "Unnamed"}' (ControlType: {root.ControlType}):");
        DumpElementRecursive(root, 0, maxDepth, maxChildren);
    }

    private static void DumpElementRecursive(AutomationElement element, int depth, int maxDepth, int maxChildren)
    {
        if (depth > maxDepth) return;

        var indent = new string(' ', depth * 2);
        var aid = element.AutomationId ?? "no-aid";
        var name = element.Name ?? "no-name";
        var type = element.ControlType.ToString();
        var enabled = element.IsEnabled ? "enabled" : "disabled";
        var visible = element.IsOffscreen ? "offscreen" : "visible";

        Console.WriteLine($"{indent}{type}: '{name}' [AID:{aid}] ({enabled}, {visible})");

        if (depth < maxDepth)
        {
            try
            {
                var children = element.FindAllChildren();
                var count = 0;
                foreach (var child in children)
                {
                    if (count >= maxChildren)
                    {
                        Console.WriteLine($"{indent}  ... and {children.Length - count} more children");
                        break;
                    }
                    DumpElementRecursive(child, depth + 1, maxDepth, maxChildren);
                    count++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{indent}  Error getting children: {ex.Message}");
            }
        }
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
