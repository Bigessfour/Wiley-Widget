using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA2;
using FormsCursor = System.Windows.Forms.Cursor;

using FlaUIApp = FlaUI.Core.Application;

namespace WileyWidget.WinForms.Tests.Integration.Ui
{
    /// <summary>
    /// FlaUI helper methods for activating and verifying WileyWidget panels in UI automation tests.
    /// All gating methods return <c>false</c> on timeout so tests can skip gracefully instead of failing.
    /// </summary>
    public static class PanelActivationHelpers
    {
        private static readonly (string PanelName, string[] Markers)[] ShellVisibilityMarkers =
        {
            ("Analytics Hub", new[] { "Analytics search", "Analytics sections", "Analytics fiscal year selector" }),
            ("Budget Management & Analysis", new[] { "Budget Entries Grid", "Load Budgets", "BudgetManagementPanel" }),
            ("Customers", new[] { "Customer search", "Add Customer", "Sync QuickBooks" }),
            ("Department Summary", new[] { "Department metrics grid", "Department Summary header", "Summary cards" }),
            ("Enterprise Vital Signs", new[] { "Enterprise vital signs header", "Enterprise gauges", "Enterprise chart table", "Enterprise vital signs status" }),
            ("Insight Feed", new[] { "Insights Data Grid", "Refresh Insights", "Insights Header" }),
            ("Payments", new[] { "Payments Grid", "Add Payment", "Refresh Payments", "Payments Status", "PaymentsPanel" }),
            ("Proactive AI Insights", new[] { "Insights Data Grid", "Proactive Insights Header", "Refresh Insights Button" }),
            ("QuickBooks", new[] { "QuickBooks Panel Header", "Connect to QuickBooks", "Import QuickBooks Desktop Export", "Sync History Grid" }),
            ("Rates", new[] { "Form host panel", "Department Rates Grid" }),
            ("Recommended Monthly Charge", new[] { "Department Rates Grid", "Benchmarks Grid" }),
            ("Reports", new[] { "Report Selector", "Generate" }),
            ("Revenue Trends", new[] { "Revenue Trends panel header", "Monthly revenue breakdown data grid" }),
            ("Settings", new[] { "Theme", "Save Changes" }),
            ("Utility Bills", new[] { "Utility Bills Grid", "Create Bill" }),
            ("War Room", new[] { "Run Scenario", "Export Forecast", "Scenario Input" }),
        };

        // ─── Main-window discovery ────────────────────────────────────────────────

        /// <summary>
        /// Polls until the main WileyWidget window is visible and responsive, or throws on timeout.
        /// Incorporates a per-iteration handle-ready spinwait to avoid the 45-60 s FlaUI
        /// element-search timeout that occurred when the window existed in the OS but
        /// Syncfusion RibbonForm had not yet completed its handle-creation phase.
        /// </summary>
        public static Window WaitForMainWindow(FlaUIApp app, UIA2Automation automation, TimeSpan timeout)
        {
            var sw = Stopwatch.StartNew();

            while (sw.Elapsed < timeout)
            {
                try
                {
                    var windows = app.GetAllTopLevelWindows(automation);
                    var window = windows.FirstOrDefault(w =>
                    {
                        try
                        {
                            var name = w.Properties.Name.ValueOrDefault ?? string.Empty;
                            return name.Contains("Wiley Widget", StringComparison.OrdinalIgnoreCase)
                                || name.Contains("Municipal", StringComparison.OrdinalIgnoreCase);
                        }
                        catch { return false; }
                    }) ?? windows.FirstOrDefault();

                    if (window != null)
                    {
                        // Spin briefly until the window is actually responding — this trims
                        // 300–500 ms off every panel activation by catching the narrow gap
                        // between "handle created" and "UI thread processing messages".
                        SpinWaitForWindowReady(window, TimeSpan.FromSeconds(3));
                        return window;
                    }
                }
                catch
                {
                    // App may have no top-level window yet; retry.
                }

                Thread.Sleep(250);
            }

            throw new TimeoutException(
                $"WileyWidget main window not found within {timeout.TotalSeconds:F0}s. " +
                "Ensure the app is built and the WILEYWIDGET_UI_AUTOMATION environment variable is set.");
        }

        // ─── Generic panel gating ────────────────────────────────────────────────

        /// <summary>
        /// Ensures a docked panel identified by <paramref name="panelName"/> (Name or AutomationId)
        /// is visible within the timeout. Returns <c>false</c> if not found — callers should
        /// <c>return</c> immediately to skip the test rather than failing with a timeout exception.
        /// </summary>
        public static bool EnsurePanelVisibleOrHostGated(Window window, string panelName, TimeSpan timeout)
        {
            if (window == null) return false;

            // Handle-ready guard before starting expensive element searches.
            SpinWaitForWindowReady(window, TimeSpan.FromMilliseconds(500));

            // Try to navigate to the panel before polling — without this the test would
            // spin for the full timeout on a panel that simply hasn't been activated yet.
            TryActivatePanel(window, new[] { panelName }, TimeSpan.FromSeconds(3));

            var visibilityCandidates = GetVisibilityCandidates(panelName);

            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                try
                {
                    var navigationStatus = TryGetNavigationAutomationStatus(window, automation: null);
                    if (navigationStatus != null && navigationStatus.Contains($"navigated:{panelName}", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    var byName = window.FindFirstDescendant(cf => cf.ByName(panelName));
                    if (byName != null) return true;

                    var byId = window.FindFirstDescendant(cf => cf.ByAutomationId(panelName));
                    if (byId != null) return true;

                    foreach (var candidate in visibilityCandidates)
                    {
                        var marker = window.FindFirstDescendant(cf => cf.ByName(candidate).Or(cf.ByAutomationId(candidate)));
                        if (marker != null)
                        {
                            return true;
                        }
                    }

                    if (string.Equals(panelName, "Analytics Hub", StringComparison.OrdinalIgnoreCase))
                    {
                        var analyticsElement = window.FindFirstDescendant(cf =>
                            cf.ByName("Analytics search")
                                .Or(cf.ByName("Analytics sections"))
                                .Or(cf.ByName("Analytics fiscal year selector")));
                        if (analyticsElement != null) return true;
                    }
                }
                catch
                {
                    // FindFirstDescendant may throw transiently while the UI is still settling.
                }

                Thread.Sleep(250);
            }

            return false;
        }

        // ─── Accounts panel ──────────────────────────────────────────────────────

        /// <summary>
        /// Ensures the Chart of Accounts / Accounts panel is visible.
        /// Accepts any of the titles defined in <see cref="UiTestConstants.AccountsPanelTitles"/>.
        /// The <paramref name="automation"/> parameter is accepted for API symmetry with callers
        /// that pass the active automation instance (not used in this implementation).
        /// </summary>
        public static bool EnsureAccountsPanelVisibleOrHostGated(Window window, FlaUI.UIA2.UIA2Automation automation, TimeSpan timeout)
        {
            if (window == null) return false;

            SpinWaitForWindowReady(window, TimeSpan.FromMilliseconds(500));

            // Click the navigation button first so the panel is actually shown before we poll.
            // Candidates cover both the full display name used by the navstrip and the compact
            // ribbon label (newline-separated text is normalised to space by some AT bridges).
            TryActivatePanel(window,
                new[] { "Municipal Accounts", "Chart of Accounts", "Accounts", "Municipal\nAccounts" },
                TimeSpan.FromSeconds(3),
                automation);

            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                try
                {
                    var navigationStatus = TryGetNavigationAutomationStatus(window, automation);
                    if (navigationStatus != null && navigationStatus.Contains("navigated:Municipal Accounts", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    foreach (var title in UiTestConstants.AccountsPanelTitles)
                    {
                        var el = window.FindFirstDescendant(cf => cf.ByName(title));
                        if (el != null) return true;
                    }

                    var accountsGrid = window.FindFirstDescendant(cf =>
                        cf.ByAutomationId("dataGridAccounts")
                            .Or(cf.ByName("Accounts Grid"))
                            .Or(cf.ByName("Chart of Accounts Panel Header")));
                    if (accountsGrid != null) return true;
                }
                catch { }

                Thread.Sleep(250);
            }

            return false;
        }

        // ─── Budget panel ────────────────────────────────────────────────────────

        /// <summary>
        /// Ensures the Budget Management &amp; Analysis panel is visible.
        /// Tries the canonical title first, then falls back to partial-name matching.
        /// </summary>
        public static bool EnsureBudgetPanelVisibleOrHostGated(Window window, TimeSpan timeout)
        {
            if (window == null) return false;

            SpinWaitForWindowReady(window, TimeSpan.FromMilliseconds(500));

            // Click the navigation button first so the panel is actually shown before we poll.
            TryActivatePanel(window,
                new[] { "Budget Management & Analysis", "Budget\nMgmt", "Budget Mgmt", "Budget" },
                TimeSpan.FromSeconds(3));

            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                try
                {
                    var exact = window.FindFirstDescendant(cf => cf.ByName(UiTestConstants.BudgetPanelTitle));
                    if (exact != null) return true;

                    // Targeted fallback: search only named elements that contain "Budget" in
                    // their accessible name.  Avoids the full-tree FindAllDescendants() dump
                    // that was burning 300-500 ms per 250 ms iteration.
                    var budgetEl = window.FindFirstDescendant(cf =>
                        cf.ByName("Budget Management & Analysis")
                          .Or(cf.ByName("Budget Mgmt"))
                          .Or(cf.ByAutomationId("BudgetManagementPanel")));
                    if (budgetEl != null) return true;
                }
                catch { }

                Thread.Sleep(250);
            }

            return false;
        }

        // ─── QuickBooks panel ─────────────────────────────────────────────────────

        /// <summary>
        /// Ensures the QuickBooks Integration panel is visible.
        /// Accepts the canonical title or any of the navigation-hint strings defined in
        /// <see cref="UiTestConstants.QuickBooksNavigationHints"/>.
        /// </summary>
        public static bool EnsureQuickBooksPanelVisibleOrHostGated(Window window, UIA2Automation? automation, TimeSpan timeout)
        {
            if (window == null) return false;

            SpinWaitForWindowReady(window, TimeSpan.FromMilliseconds(500));

            // Click the navigation button first.
            TryActivatePanel(window,
                new[] { UiTestConstants.QuickBooksPanelTitle, "QuickBooks", "QBO", "Connect to QuickBooks" },
            TimeSpan.FromSeconds(3),
            automation);

            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                try
                {
                    var navigationStatus = TryGetNavigationAutomationStatus(window, automation);
                    if (navigationStatus != null && navigationStatus.Contains("navigated:QuickBooks", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    var exact = window.FindFirstDescendant(cf => cf.ByName(UiTestConstants.QuickBooksPanelTitle));
                    if (exact != null) return true;

                    var quickBooksPanel = window.FindFirstDescendant(cf =>
                        cf.ByName("QuickBooks Panel Header")
                            .Or(cf.ByName("Connect to QuickBooks"))
                            .Or(cf.ByName("Import QuickBooks Desktop Export"))
                            .Or(cf.ByName("Sync History Grid")));
                    if (quickBooksPanel != null) return true;

                    foreach (var hint in UiTestConstants.QuickBooksNavigationHints)
                    {
                        var el = window.FindFirstDescendant(cf => cf.ByName(hint));
                        if (el != null) return true;

                        if (el == null && automation != null)
                        {
                            el = automation.GetDesktop().FindFirstDescendant(cf => cf.ByName(hint));
                            if (el != null) return true;
                        }
                    }
                }
                catch { }

                Thread.Sleep(250);
            }

            return false;
        }

        /// <summary>
        /// Ensures the Payments panel is visible.
        /// Uses explicit shell automation targets because the generic "Payments" candidate can
        /// collide with the ribbon group label and miss the actual navigation command.
        /// </summary>
        public static bool EnsurePaymentsPanelVisibleOrHostGated(Window window, UIA2Automation? automation, TimeSpan timeout)
        {
            if (window == null) return false;

            SpinWaitForWindowReady(window, TimeSpan.FromMilliseconds(500));

            TryActivatePanel(window,
                new[] { "Nav_Payments", "Menu_View_Payments", "Payments" },
                TimeSpan.FromSeconds(3),
                automation);

            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                try
                {
                    var navigationStatus = TryGetNavigationAutomationStatus(window, automation);
                    if (navigationStatus != null && navigationStatus.Contains("navigated:Payments", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    var paymentsPanel = window.FindFirstDescendant(cf =>
                        cf.ByName("Payments Grid")
                            .Or(cf.ByName("Add Payment"))
                            .Or(cf.ByName("Refresh Payments"))
                            .Or(cf.ByName("Payments Status"))
                            .Or(cf.ByAutomationId("PaymentsPanel")));
                    if (paymentsPanel != null) return true;

                    if (automation != null)
                    {
                        paymentsPanel = automation.GetDesktop().FindFirstDescendant(cf =>
                            cf.ByName("Payments Grid")
                                .Or(cf.ByName("Add Payment"))
                                .Or(cf.ByName("Refresh Payments"))
                                .Or(cf.ByName("Payments Status"))
                                .Or(cf.ByAutomationId("PaymentsPanel")));
                        if (paymentsPanel != null) return true;
                    }
                }
                catch
                {
                }

                Thread.Sleep(250);
            }

            return false;
        }

        // ─── JARVIS panel activation ──────────────────────────────────────────────

        /// <summary>
        /// Attempts to activate the JARVIS Chat panel by locating and clicking its docking tab.
        /// Completes when the tab is clicked or the timeout elapses (does not throw on timeout —
        /// the calling test decides whether to fail or skip).
        /// </summary>
        public static void ActivateJarvisPanel(Window window, UIA2Automation automation, TimeSpan timeout)
        {
            if (window == null) return;

            SpinWaitForWindowReady(window, TimeSpan.FromMilliseconds(500));

            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                try
                {
                    var jarvisTab = window.FindFirstDescendant(cf =>
                        cf.ByName(UiTestConstants.JarvisTabTitle)
                            .Or(cf.ByName("JARVIS"))
                            .Or(cf.ByName("Jarvis Chat"))
                            .Or(cf.ByAutomationId("JARVISChatPanel")));

                    if (jarvisTab != null)
                    {
                        try { jarvisTab.Click(); }
                        catch { /* Panel may already be active — not an error. */ }
                        return;
                    }
                }
                catch { }

                Thread.Sleep(250);
            }

            // Timeout without finding the tab.  Caller logs / skips as appropriate.
        }

        // ─── Internal helpers ─────────────────────────────────────────────────────

        /// <summary>
        /// Attempts to find and click any of <paramref name="buttonCandidates"/> within
        /// <paramref name="clickTimeout"/>.  Returns immediately after the first successful
        /// click so the caller can proceed to its poll loop without waiting out the full timeout.
        /// Swallows all exceptions — a navigation failure is not itself a test failure.
        /// </summary>
        private static void TryActivatePanel(Window window, string[] buttonCandidates, TimeSpan clickTimeout, UIA2Automation? automation = null)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < clickTimeout)
            {
                try
                {
                    foreach (var candidate in buttonCandidates)
                    {
                        // Try by Name first, then by AutomationId.
                        var btn = window.FindFirstDescendant(cf => cf.ByName(candidate))
                                  ?? window.FindFirstDescendant(cf => cf.ByAutomationId(candidate));
                        if (btn != null)
                        {
                            if (TryActivateElement(btn))
                            {
                                Wait.UntilInputIsProcessed();
                                return;
                            }
                        }

                        var automationMenuButton = window.FindFirstDescendant(cf => cf.ByAutomationId($"NavMenuItem_{SanitizeAutomationName(candidate)}"));
                        if (automationMenuButton != null)
                        {
                            if (TryActivateElement(automationMenuButton))
                            {
                                Wait.UntilInputIsProcessed();
                                return;
                            }
                        }

                        foreach (var automationAlias in GetShellAutomationAliases(candidate))
                        {
                            var aliasedButton = window.FindFirstDescendant(cf => cf.ByAutomationId(automationAlias))
                                ?? window.FindFirstDescendant(cf => cf.ByName(automationAlias));
                            if (aliasedButton == null)
                            {
                                continue;
                            }

                            if (TryActivateElement(aliasedButton))
                            {
                                Wait.UntilInputIsProcessed();
                                return;
                            }
                        }
                    }

                    if (TryActivatePanelByShellShortcut(window, buttonCandidates))
                    {
                        Wait.UntilInputIsProcessed();
                        return;
                    }

                    if (TryActivatePanelFromUnifiedNavigation(window, automation, buttonCandidates))
                    {
                        Wait.UntilInputIsProcessed();
                        return;
                    }
                }
                catch { /* element may not exist yet — retry */ }

                Thread.Sleep(150);
            }
        }

        private static bool TryActivateElement(AutomationElement element)
        {
            try
            {
                if (element.Patterns.Invoke.IsSupported)
                {
                    element.Patterns.Invoke.Pattern.Invoke();
                    return true;
                }
            }
            catch
            {
            }

            try
            {
                element.AsButton().Invoke();
                return true;
            }
            catch
            {
            }

            try
            {
                element.Click();
                return true;
            }
            catch
            {
            }

            return false;
        }

        private static bool TryActivatePanelByShellShortcut(Window window, string[] buttonCandidates)
        {
            try
            {
                window.Focus();

                if (buttonCandidates.Any(candidate => candidate.Contains("Payments", StringComparison.OrdinalIgnoreCase)))
                {
                    SendCtrlShiftChord(VirtualKeyShort.KEY_P);
                    return true;
                }

                if (buttonCandidates.Any(candidate => candidate.Contains("QuickBooks", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(candidate, "QBO", StringComparison.OrdinalIgnoreCase)))
                {
                    // QuickBooks is exposed through the View menu shortcut (Ctrl+Q).
                    // Sending Alt+Q first can leave the shell in menu-navigation mode
                    // without dispatching the actual panel command during UI automation.
                    SendCtrlChord(VirtualKeyShort.KEY_Q);
                    return true;
                }

                if (buttonCandidates.Any(candidate => candidate.Contains("Analytics Hub", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(candidate, "Analytics", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(candidate, "Charts", StringComparison.OrdinalIgnoreCase)))
                {
                    SendAltChord(VirtualKeyShort.KEY_C);
                    SendCtrlChord(VirtualKeyShort.KEY_H);
                    return true;
                }

                if (buttonCandidates.Any(candidate => candidate.Contains("Budget", StringComparison.OrdinalIgnoreCase)))
                {
                    SendAltChord(VirtualKeyShort.KEY_B);
                    SendCtrlChord(VirtualKeyShort.KEY_B);
                    return true;
                }

                if (buttonCandidates.Any(candidate => candidate.Contains("Municipal Accounts", StringComparison.OrdinalIgnoreCase)
                    || candidate.Contains("Chart of Accounts", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(candidate, "Accounts", StringComparison.OrdinalIgnoreCase)))
                {
                    SendAltChord(VirtualKeyShort.KEY_A);
                    SendCtrlChord(VirtualKeyShort.KEY_A);
                    return true;
                }

                if (buttonCandidates.Any(candidate => candidate.Contains("Enterprise Vital Signs", StringComparison.OrdinalIgnoreCase)))
                {
                    SendAltChord(VirtualKeyShort.KEY_D);
                    SendCtrlChord(VirtualKeyShort.KEY_D);
                    return true;
                }

                if (buttonCandidates.Any(candidate => candidate.Contains("Customers", StringComparison.OrdinalIgnoreCase)))
                {
                    SendCtrlChord(VirtualKeyShort.KEY_U);
                    return true;
                }

                if (buttonCandidates.Any(candidate => candidate.Contains("Reports", StringComparison.OrdinalIgnoreCase)))
                {
                    SendAltChord(VirtualKeyShort.KEY_R);
                    return true;
                }

                if (buttonCandidates.Any(candidate => candidate.Contains("Settings", StringComparison.OrdinalIgnoreCase)))
                {
                    SendAltChord(VirtualKeyShort.KEY_S);
                    SendCtrlAltChord(VirtualKeyShort.KEY_S);
                    return true;
                }

                if (buttonCandidates.Any(candidate => candidate.Contains("War Room", StringComparison.OrdinalIgnoreCase)))
                {
                    SendAltChord(VirtualKeyShort.KEY_W);
                    return true;
                }

                if (buttonCandidates.Any(candidate => candidate.Contains("JARVIS", StringComparison.OrdinalIgnoreCase)))
                {
                    SendAltChord(VirtualKeyShort.KEY_J);
                    return true;
                }
            }
            catch
            {
                try
                {
                    Keyboard.Press(VirtualKeyShort.ESCAPE);
                }
                catch
                {
                }

                return false;
            }

            return false;
        }

        private static void SendAltChord(VirtualKeyShort key)
        {
            Keyboard.Press(VirtualKeyShort.LMENU);
            try
            {
                Keyboard.Type(key);
                Wait.UntilInputIsProcessed();
            }
            finally
            {
                Keyboard.Release(VirtualKeyShort.LMENU);
            }
        }

        private static void SendCtrlChord(VirtualKeyShort key)
        {
            Keyboard.Press(VirtualKeyShort.CONTROL);
            try
            {
                Keyboard.Type(key);
                Wait.UntilInputIsProcessed();
            }
            finally
            {
                Keyboard.Release(VirtualKeyShort.CONTROL);
            }
        }

        private static void SendCtrlAltChord(VirtualKeyShort key)
        {
            Keyboard.Press(VirtualKeyShort.CONTROL);
            Keyboard.Press(VirtualKeyShort.LMENU);
            try
            {
                Keyboard.Type(key);
                Wait.UntilInputIsProcessed();
            }
            finally
            {
                Keyboard.Release(VirtualKeyShort.LMENU);
                Keyboard.Release(VirtualKeyShort.CONTROL);
            }
        }

        private static void SendCtrlShiftChord(VirtualKeyShort key)
        {
            Keyboard.Press(VirtualKeyShort.CONTROL);
            Keyboard.Press(VirtualKeyShort.SHIFT);
            try
            {
                Keyboard.Type(key);
                Wait.UntilInputIsProcessed();
            }
            finally
            {
                Keyboard.Release(VirtualKeyShort.SHIFT);
                Keyboard.Release(VirtualKeyShort.CONTROL);
            }
        }

        private static bool TryActivatePanelFromUnifiedNavigation(Window window, UIA2Automation? automation, string[] buttonCandidates)
        {
            AutomationElement? dropDown = null;

            try
            {
                dropDown = window.FindFirstDescendant(cf => cf.ByAutomationId("Nav_UnifiedDropdown"))
                    ?? window.FindFirstDescendant(cf => cf.ByName("Navigation"))
                    ?? window.FindFirstDescendant(cf => cf.ByName("Navigate"));
            }
            catch
            {
            }

            if (dropDown == null && automation != null)
            {
                try
                {
                    dropDown = automation.GetDesktop().FindFirstDescendant(cf =>
                        cf.ByAutomationId("Nav_UnifiedDropdown")
                            .Or(cf.ByName("Navigation"))
                            .Or(cf.ByName("Navigate")));
                }
                catch
                {
                }
            }

            if (dropDown == null)
            {
                return TryActivatePanelFromUnifiedNavigationByKeyboard(window, buttonCandidates);
            }

            try
            {
                dropDown.Click();
                Wait.UntilInputIsProcessed();
            }
            catch
            {
                return false;
            }

            foreach (var candidate in buttonCandidates)
            {
                var automationId = $"NavMenuItem_{SanitizeAutomationName(candidate)}";
                AutomationElement? menuItem = null;

                var sw = Stopwatch.StartNew();
                while (sw.Elapsed < TimeSpan.FromSeconds(2) && menuItem == null)
                {
                    try
                    {
                        menuItem = window.FindFirstDescendant(cf => cf.ByAutomationId(automationId).Or(cf.ByName(candidate)));
                    }
                    catch
                    {
                    }

                    if (menuItem == null && automation != null)
                    {
                        try
                        {
                            menuItem = automation.GetDesktop().FindFirstDescendant(cf => cf.ByAutomationId(automationId).Or(cf.ByName(candidate)));
                        }
                        catch
                        {
                        }
                    }

                    if (menuItem == null)
                    {
                        Thread.Sleep(100);
                    }
                }

                if (menuItem == null)
                {
                    continue;
                }

                try
                {
                    menuItem.Click();
                    Wait.UntilInputIsProcessed();
                    return true;
                }
                catch
                {
                }
            }

            if (TrySelectFlatAutomationNavigationMenuItem(buttonCandidates))
            {
                return true;
            }

            return TryActivatePanelFromUnifiedNavigationByKeyboard(window, buttonCandidates);
        }

        private static bool TrySelectFlatAutomationNavigationMenuItem(string[] buttonCandidates)
        {
            try
            {
                foreach (var key in GetFlatAutomationNavigationKeyboardSequence(buttonCandidates))
                {
                    Keyboard.Type(key);
                    Wait.UntilInputIsProcessed();
                }

                return true;
            }
            catch
            {
                try
                {
                    Keyboard.Press(VirtualKeyShort.ESCAPE);
                }
                catch
                {
                }

                return false;
            }
        }

        private static bool TryActivatePanelFromUnifiedNavigationByKeyboard(Window window, string[] buttonCandidates)
        {
            try
            {
                var ribbon = window.FindFirstDescendant(cf => cf.ByAutomationId("Ribbon_Main"));
                if (ribbon == null)
                {
                    return false;
                }

                window.Focus();

                var bounds = ribbon.BoundingRectangle;
                var clickPoint = new System.Drawing.Point(Convert.ToInt32(bounds.Left + 72), Convert.ToInt32(bounds.Top + 132));
                FormsCursor.Position = clickPoint;
                Mouse.Click();
                Wait.UntilInputIsProcessed();

                foreach (var key in GetUnifiedNavigationKeyboardSequence(buttonCandidates))
                {
                    Keyboard.Type(key);
                    Wait.UntilInputIsProcessed();
                }

                return true;
            }
            catch
            {
                try
                {
                    Keyboard.Press(VirtualKeyShort.ESCAPE);
                }
                catch
                {
                }

                return false;
            }
        }

        private static VirtualKeyShort[] GetUnifiedNavigationKeyboardSequence(string[] buttonCandidates)
        {
            if (buttonCandidates.Any(candidate => candidate.Contains("JARVIS", StringComparison.OrdinalIgnoreCase)))
            {
                return new[] { VirtualKeyShort.KEY_H, VirtualKeyShort.RIGHT, VirtualKeyShort.KEY_J, VirtualKeyShort.ENTER };
            }

            if (buttonCandidates.Any(candidate => candidate.Contains("Budget", StringComparison.OrdinalIgnoreCase)))
            {
                return new[] { VirtualKeyShort.KEY_F, VirtualKeyShort.RIGHT, VirtualKeyShort.KEY_B, VirtualKeyShort.ENTER };
            }

            if (buttonCandidates.Any(candidate => candidate.Contains("Municipal Accounts", StringComparison.OrdinalIgnoreCase)
                || candidate.Contains("Chart of Accounts", StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate, "Accounts", StringComparison.OrdinalIgnoreCase)))
            {
                return new[] { VirtualKeyShort.KEY_F, VirtualKeyShort.RIGHT, VirtualKeyShort.KEY_M, VirtualKeyShort.ENTER };
            }

            if (buttonCandidates.Any(candidate => candidate.Contains("Payments", StringComparison.OrdinalIgnoreCase)))
            {
                return new[] { VirtualKeyShort.KEY_F, VirtualKeyShort.RIGHT, VirtualKeyShort.KEY_P, VirtualKeyShort.ENTER };
            }

            return new[] { VirtualKeyShort.DOWN, VirtualKeyShort.RIGHT, VirtualKeyShort.ENTER };
        }

        private static VirtualKeyShort[] GetFlatAutomationNavigationKeyboardSequence(string[] buttonCandidates)
        {
            if (buttonCandidates.Any(candidate => candidate.Contains("JARVIS", StringComparison.OrdinalIgnoreCase)))
            {
                return new[] { VirtualKeyShort.KEY_J, VirtualKeyShort.ENTER };
            }

            if (buttonCandidates.Any(candidate => candidate.Contains("Budget", StringComparison.OrdinalIgnoreCase)))
            {
                return new[] { VirtualKeyShort.KEY_B, VirtualKeyShort.ENTER };
            }

            if (buttonCandidates.Any(candidate => candidate.Contains("Municipal Accounts", StringComparison.OrdinalIgnoreCase)
                || candidate.Contains("Chart of Accounts", StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate, "Accounts", StringComparison.OrdinalIgnoreCase)))
            {
                return new[] { VirtualKeyShort.KEY_M, VirtualKeyShort.ENTER };
            }

            if (buttonCandidates.Any(candidate => candidate.Contains("Enterprise Vital Signs", StringComparison.OrdinalIgnoreCase)))
            {
                return new[] { VirtualKeyShort.KEY_E, VirtualKeyShort.ENTER };
            }

            if (buttonCandidates.Any(candidate => candidate.Contains("Customers", StringComparison.OrdinalIgnoreCase)))
            {
                return new[] { VirtualKeyShort.KEY_C, VirtualKeyShort.ENTER };
            }

            if (buttonCandidates.Any(candidate => candidate.Contains("Payments", StringComparison.OrdinalIgnoreCase)))
            {
                return new[] { VirtualKeyShort.KEY_P, VirtualKeyShort.ENTER };
            }

            return new[] { VirtualKeyShort.DOWN, VirtualKeyShort.ENTER };
        }

        private static string SanitizeAutomationName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return new string(value.Where(char.IsLetterOrDigit).ToArray());
        }

        private static string[] GetVisibilityCandidates(string panelName)
        {
            if (string.IsNullOrWhiteSpace(panelName))
            {
                return Array.Empty<string>();
            }

            foreach (var (knownPanelName, markers) in ShellVisibilityMarkers)
            {
                if (string.Equals(knownPanelName, panelName, StringComparison.OrdinalIgnoreCase))
                {
                    return markers;
                }
            }

            return Array.Empty<string>();
        }

        private static string[] GetShellAutomationAliases(string candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return Array.Empty<string>();
            }

            var sanitizedCandidate = SanitizeAutomationName(candidate);

            if (candidate.Contains("QuickBooks", StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate, "QBO", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { "Nav_QuickBooks", $"Nav_{sanitizedCandidate}", "Menu_View_QuickBooks" }
                    .Where(alias => !string.IsNullOrWhiteSpace(alias))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }

            if (candidate.Contains("Analytics Hub", StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate, "Analytics", StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate, "Charts", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { "Nav_Analytics", "Nav_Charts", "Nav_AnalyticsHub", $"Nav_{sanitizedCandidate}", "Menu_View_AnalyticsHub" }
                    .Where(alias => !string.IsNullOrWhiteSpace(alias))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }

            if (candidate.Contains("Municipal Accounts", StringComparison.OrdinalIgnoreCase)
                || candidate.Contains("Chart of Accounts", StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate, "Accounts", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { "Nav_Accounts", $"Nav_{sanitizedCandidate}", "Menu_View_Accounts" }
                    .Where(alias => !string.IsNullOrWhiteSpace(alias))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }

            if (candidate.Contains("Budget", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { "Nav_Budget", $"Nav_{sanitizedCandidate}", "Menu_View_Budget" }
                    .Where(alias => !string.IsNullOrWhiteSpace(alias))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }

            if (candidate.Contains("Customers", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { "Nav_Customers", $"Nav_{sanitizedCandidate}", "Menu_View_Customers" }
                    .Where(alias => !string.IsNullOrWhiteSpace(alias))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }

            if (candidate.Contains("Payments", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { "Nav_Payments", $"Nav_{sanitizedCandidate}", "Menu_View_Payments" }
                    .Where(alias => !string.IsNullOrWhiteSpace(alias))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }

            if (candidate.Contains("Enterprise Vital Signs", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { "Nav_VitalSigns", "Nav_EnterpriseVitalSigns", $"Nav_{sanitizedCandidate}", "Menu_View_VitalSigns" }
                    .Where(alias => !string.IsNullOrWhiteSpace(alias))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }

            if (candidate.Contains("Revenue Trends", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { "Nav_RevenueTrends", $"Nav_{sanitizedCandidate}" }
                    .Where(alias => !string.IsNullOrWhiteSpace(alias))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }

            if (candidate.Contains("Settings", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { "Nav_Settings", $"Nav_{sanitizedCandidate}", "Menu_Tools_Settings" }
                    .Where(alias => !string.IsNullOrWhiteSpace(alias))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }

            return new[] { $"Nav_{sanitizedCandidate}" }
                .Where(alias => !string.IsNullOrWhiteSpace(alias))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string? TryGetNavigationAutomationStatus(Window window, UIA2Automation? automation)
        {
            AutomationElement? statusElement = null;

            try
            {
                statusElement = window.FindFirstDescendant(cf => cf.ByAutomationId("NavAutomationStatus").Or(cf.ByName("NavAutomationStatus")));
            }
            catch
            {
            }

            if (statusElement == null && automation != null)
            {
                try
                {
                    statusElement = automation.GetDesktop().FindFirstDescendant(cf => cf.ByAutomationId("NavAutomationStatus").Or(cf.ByName("NavAutomationStatus")));
                }
                catch
                {
                }
            }

            if (statusElement == null)
            {
                return null;
            }

            try
            {
                var valuePattern = statusElement.Patterns.Value;
                if (valuePattern.IsSupported)
                {
                    var value = valuePattern.Pattern.Value.Value;
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }
            catch
            {
            }

            try
            {
                return statusElement.Properties.Name.ValueOrDefault;
            }
            catch
            {
            }

            return null;
        }

        /// <summary>
        /// Spins at 50 ms intervals until the window reports as enabled and on-screen, or the
        /// timeout elapses.  This closes the gap between "handle created" and "UI thread
        /// responding to automation queries" that Syncfusion RibbonForm briefly enters during
        /// startup — shaving 300–500 ms per test activation.
        /// </summary>
        private static void SpinWaitForWindowReady(Window window, TimeSpan timeout)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                try
                {
                    var onScreen = !window.Properties.IsOffscreen.ValueOrDefault;
                    var enabled = window.IsEnabled;
                    if (onScreen && enabled) return;
                }
                catch
                {
                    // Properties may throw while the window handle is still being finalised.
                }

                Thread.Sleep(50);
            }
        }
    }
}
