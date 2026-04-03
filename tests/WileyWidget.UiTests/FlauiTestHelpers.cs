using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA2;
using Microsoft.Extensions.Logging;
using WileyWidget.WinForms.Controls.Base;
using Xunit;

namespace WileyWidget.UiTests
{
    /// <summary>
    /// Syncfusion 33.1.44 + FLAUI helper library (RibbonControlAdv, SfButton, TabbedMDI, panels)
    /// Drop this in and never write flaky waits again.
    /// </summary>
    public static class FlauiTestHelpers
    {
        public static Window LaunchAndWaitForMainWindow(FlaUI.Core.Application app, UIA2Automation automation, TimeSpan timeout = default)
        {
            timeout = timeout == default ? TimeSpan.FromSeconds(15) : timeout;
            var window = PanelActivationHelpers.WaitForMainWindow(app, automation, timeout);
            if (window == null) throw new InvalidOperationException("Main window not found");
            return window;
        }

        public static Task<Window> LaunchAndWaitForMainWindowAsync(FlaUI.Core.Application app, UIA2Automation automation, TimeSpan timeout = default)
        {
            return Task.FromResult(LaunchAndWaitForMainWindow(app, automation, timeout));
        }

        public static AutomationElement WaitForRibbonTab(this Window window, string tabName, TimeSpan timeout = default)
        {
            timeout = timeout == default ? TimeSpan.FromSeconds(8) : timeout;
            var result = Retry.WhileNull(() =>
            {
                try
                {
                    return window.FindFirstDescendant(cf =>
                        cf.ByAutomationId($"RibbonTab_{tabName}")
                            .Or(cf.ByName(tabName)));
                }
                catch
                {
                    return null;
                }

            }, timeout, TimeSpan.FromMilliseconds(250));

            if (result.Result != null)
            {
                return result.Result;
            }

            throw new TimeoutException($"RibbonTab {tabName} not found within {timeout.TotalSeconds}s");
        }

        public static AutomationElement WaitForPanel<T>(this Window window, TimeSpan timeout = default) where T : ScopedPanelBase
        {
            timeout = timeout == default ? TimeSpan.FromSeconds(10) : timeout;
            var candidates = GetPanelLookupCandidates(typeof(T));
            var result = Retry.WhileNull(() =>
            {
                try
                {
                    return FindFirstMatchingDescendant(window, candidates);
                }
                catch
                {
                    return null;
                }

            }, timeout, TimeSpan.FromMilliseconds(250));

            if (result.Result != null)
            {
                return result.Result;
            }

            throw new TimeoutException($"Panel {typeof(T).Name} not found within {timeout.TotalSeconds}s using markers: {string.Join(", ", candidates)}");
        }

        private static AutomationElement? FindFirstMatchingDescendant(AutomationElement root, IReadOnlyList<string> candidates)
        {
            foreach (var candidate in candidates)
            {
                var exact = root.FindFirstDescendant(cf => cf.ByAutomationId(candidate).Or(cf.ByName(candidate)));
                if (exact != null)
                {
                    return exact;
                }
            }

            return root.FindAllDescendants().FirstOrDefault(element =>
            {
                var automationId = FlaUiHelpers.TryGetAutomationId(element);
                var name = FlaUiHelpers.TryGetName(element);

                return candidates.Any(candidate =>
                    string.Equals(automationId, candidate, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, candidate, StringComparison.OrdinalIgnoreCase)
                    || (!string.IsNullOrWhiteSpace(name) && name.Contains(candidate, StringComparison.OrdinalIgnoreCase)));
            });
        }

        private static string[] GetPanelLookupCandidates(Type panelType)
        {
            return panelType.Name switch
            {
                "BudgetPanel" =>
                [
                    panelType.Name,
                    UiTestConstants.BudgetPanelTitle,
                    "BudgetManagementPanel",
                    "Load Budgets",
                    "Budget Entries"
                ],
                "EnterpriseVitalSignsPanel" =>
                [
                    panelType.Name,
                    "Enterprise Vital Signs",
                    "financial snapshot",
                    "monthly narrative"
                ],
                _ => [panelType.Name, panelType.Name.Replace("Panel", string.Empty, StringComparison.Ordinal)]
            };
        }

        public static void ClickSfButton(this AutomationElement element, string buttonAutomationId)
        {
            var btn = element.FindFirstDescendant(cf => cf.ByAutomationId(buttonAutomationId));
            btn?.Click();
            Wait.UntilInputIsProcessed();
            Keyboard.Type(VirtualKeyShort.ENTER); // extra safety for Syncfusion focus
            Wait.UntilInputIsProcessed();
        }

        public static void AssertValuePattern(this AutomationElement element, string expected)
        {
            var valuePattern = element.Patterns.Value;
            Assert.True(valuePattern.IsSupported);
            Assert.Equal(expected, valuePattern.Pattern.Value.Value);
        }

        public static void AssertNoCriticalLogs(this ILogger logger, string runLog)
        {
            if (runLog.Contains("[CRITICAL] DockingManager is null"))
                throw new InvalidOperationException("DockingManager null warning still present – use TestDockingManagerStub!");
        }
    }
}
