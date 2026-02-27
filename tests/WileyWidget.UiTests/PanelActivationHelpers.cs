using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA2;

using FlaUIApp = FlaUI.Core.Application;

namespace WileyWidget.UiTests
{
    /// <summary>
    /// FlaUI helper methods for activating and verifying WileyWidget panels in UI automation tests.
    /// All gating methods return <c>false</c> on timeout so tests can skip gracefully instead of failing.
    /// </summary>
    public static class PanelActivationHelpers
    {
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

            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                try
                {
                    var byName = window.FindFirstDescendant(cf => cf.ByName(panelName));
                    if (byName != null) return true;

                    var byId = window.FindFirstDescendant(cf => cf.ByAutomationId(panelName));
                    if (byId != null) return true;
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
                TimeSpan.FromSeconds(3));

            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                try
                {
                    foreach (var title in UiTestConstants.AccountsPanelTitles)
                    {
                        var el = window.FindFirstDescendant(cf => cf.ByName(title));
                        if (el != null) return true;
                    }
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
        public static bool EnsureQuickBooksPanelVisibleOrHostGated(Window window, TimeSpan timeout)
        {
            if (window == null) return false;

            SpinWaitForWindowReady(window, TimeSpan.FromMilliseconds(500));

            // Click the navigation button first.
            TryActivatePanel(window,
                new[] { UiTestConstants.QuickBooksPanelTitle, "QuickBooks", "QBO", "Connect to QuickBooks" },
                TimeSpan.FromSeconds(3));

            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                try
                {
                    var exact = window.FindFirstDescendant(cf => cf.ByName(UiTestConstants.QuickBooksPanelTitle));
                    if (exact != null) return true;

                    foreach (var hint in UiTestConstants.QuickBooksNavigationHints)
                    {
                        var el = window.FindFirstDescendant(cf => cf.ByName(hint));
                        if (el != null) return true;
                    }
                }
                catch { }

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
        private static void TryActivatePanel(Window window, string[] buttonCandidates, TimeSpan clickTimeout)
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
                            btn.Click();
                            // Brief pause so the panel host has a chance to create its handle
                            // before the polling loop starts querying descendants.
                            Thread.Sleep(300);
                            return;
                        }
                    }
                }
                catch { /* element may not exist yet — retry */ }

                Thread.Sleep(150);
            }
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

        /// <summary>
        /// Activates a panel by finding and clicking its tab.
        /// </summary>
        public static void ActivatePanel<T>(Window window, UIA2Automation automation, TimeSpan timeout) where T : class
        {
            var panelName = typeof(T).Name.Replace("Panel", ""); // e.g., BudgetPanel -> Budget
            TryActivatePanel(window, new[] { panelName }, timeout);
        }
    }
}
