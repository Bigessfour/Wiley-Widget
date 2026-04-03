using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using WileyWidget.WinForms.Services;

namespace WileyWidget.WinForms.Tests.Integration.Ui
{
    [Collection("FlaUI Tests")]
    public class PanelRegistrySmokeTests : FlaUiTestBase
    {
        private static readonly HashSet<string> ContextualPanels = new(StringComparer.OrdinalIgnoreCase)
        {
            "Account Editor",
            "Activity Log",
            "Audit Log & Activity",
            "Data Mapper",
            "Payment Editor"
        };

        private static readonly HashSet<string> ExplicitShellPanels = new(StringComparer.OrdinalIgnoreCase)
        {
            "QuickBooks",
            "Settings"
        };

        public static IEnumerable<object[]> ShellPanelData =>
            PanelRegistry.Panels
                .Where(entry => entry.ShowInRibbonPanelsMenu || ExplicitShellPanels.Contains(entry.DisplayName))
                .Where(entry => !string.Equals(entry.DisplayName, UiTestConstants.JarvisTabTitle, StringComparison.OrdinalIgnoreCase))
                .OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(entry => new object[] { entry.DisplayName });

        [StaFact]
        [Trait("Category", "Smoke")]
        [Trait("Area", "PanelRegistry")]
        public void PanelRegistry_AllPanelsHaveProofClassification()
        {
            var coveredPanels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in PanelRegistry.Panels)
            {
                if (entry.ShowInRibbonPanelsMenu || ExplicitShellPanels.Contains(entry.DisplayName) || ContextualPanels.Contains(entry.DisplayName))
                {
                    coveredPanels.Add(entry.DisplayName);
                }
            }

            coveredPanels.Add(UiTestConstants.JarvisTabTitle);

            var uncoveredPanels = PanelRegistry.Panels
                .Select(entry => entry.DisplayName)
                .Where(displayName => !coveredPanels.Contains(displayName))
                .OrderBy(displayName => displayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Assert.True(
                uncoveredPanels.Count == 0,
                $"Each panel must be accounted for by the proof strategy. Missing: {string.Join(", ", uncoveredPanels)}");
        }

        [StaTheory]
        [MemberData(nameof(ShellPanelData))]
        [Trait("Category", "Smoke")]
        [Trait("Area", "Panels")]
        public void ShellPanel_CanBeActivatedFromMainWindow(string displayName)
        {
            EnsureAppLaunched();
            var window = SharedMainWindow ?? throw new InvalidOperationException("Main window not initialized.");

            var isVisible = EnsureShellPanelVisible(window, displayName, TimeSpan.FromSeconds(45));

            Assert.True(isVisible, $"Panel '{displayName}' did not become visible from the main shell.");
        }

        [StaTheory]
        [InlineData("Budget Management & Analysis")]
        [InlineData("Reports")]
        [InlineData("Settings")]
        [InlineData("QuickBooks")]
        [Trait("Category", "Smoke")]
        [Trait("Area", "CriticalControls")]
        public void CriticalPanels_RenderPrimaryControls(string displayName)
        {
            EnsureAppLaunched();
            var window = SharedMainWindow ?? throw new InvalidOperationException("Main window not initialized.");

            var isVisible = EnsureShellPanelVisible(window, displayName, TimeSpan.FromSeconds(45));
            Assert.True(isVisible, $"Panel '{displayName}' did not become visible from the main shell.");

            switch (displayName)
            {
                case "Budget Management & Analysis":
                    Assert.True(
                        FlaUiHelpers.FindElementByName(window, "Load Budgets", TimeSpan.FromSeconds(10)) != null,
                        "Budget panel should expose the Load Budgets action.");
                    Assert.True(
                        FlaUiHelpers.FindElementByName(window, "Budget Entries Grid", TimeSpan.FromSeconds(10)) != null,
                        "Budget panel should expose the Budget Entries Grid.");
                    break;

                case "Reports":
                    Assert.True(
                        FlaUiHelpers.FindElementByNameOrId(window, "Report Selector", "reportSelector", TimeSpan.FromSeconds(10)) != null,
                        "Reports panel should expose the report selector.");
                    Assert.True(
                        FlaUiHelpers.FindElementByNameOrId(window, "Generate Report", "Toolbar_Generate", TimeSpan.FromSeconds(10)) != null,
                        "Reports panel should expose the Generate action.");
                    break;

                case "Settings":
                    Assert.True(
                        FlaUiHelpers.FindElementByName(window, "Theme", TimeSpan.FromSeconds(10)) != null,
                        "Settings panel should expose the theme selector.");
                    Assert.True(
                        FlaUiHelpers.FindElementByName(window, "Save Changes", TimeSpan.FromSeconds(10)) != null,
                        "Settings panel should expose the Save Changes action.");
                    break;

                case "QuickBooks":
                    Assert.True(
                        FlaUiHelpers.FindElementByName(window, "Connect to QuickBooks", TimeSpan.FromSeconds(10)) != null,
                        "QuickBooks panel should expose the connect action.");
                    Assert.True(
                        FlaUiHelpers.FindElementByName(window, "Sync History Grid", TimeSpan.FromSeconds(10)) != null,
                        "QuickBooks panel should expose the history grid.");
                    break;

                default:
                    throw new InvalidOperationException($"No critical-control proof defined for panel '{displayName}'.");
            }
        }

        private static bool EnsureShellPanelVisible(FlaUI.Core.AutomationElements.Window window, string displayName, TimeSpan timeout)
        {
            return displayName switch
            {
                "Budget Management & Analysis" => PanelActivationHelpers.EnsureBudgetPanelVisibleOrHostGated(window, timeout),
                "Municipal Accounts" => PanelActivationHelpers.EnsureAccountsPanelVisibleOrHostGated(window, EnsureAutomation(), timeout),
                "Payments" => PanelActivationHelpers.EnsurePaymentsPanelVisibleOrHostGated(window, EnsureAutomation(), timeout),
                "QuickBooks" => PanelActivationHelpers.EnsureQuickBooksPanelVisibleOrHostGated(window, EnsureAutomation(), timeout),
                _ => PanelActivationHelpers.EnsurePanelVisibleOrHostGated(window, displayName, timeout, EnsureAutomation())
            };
        }
    }
}
