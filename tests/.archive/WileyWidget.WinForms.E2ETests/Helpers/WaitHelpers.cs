using System;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;

namespace WileyWidget.WinForms.E2ETests
{
    /// <summary>
    /// Small collection of test-wide wait helpers exposed as static methods.
    /// Tests call these unqualified via a global using static.
    /// </summary>
    public static class WaitHelpers
    {
        /// <summary>
        /// Waits for any visible busy indicator (ProgressBar or BusyIndicator) across top-level windows to disappear.
        /// This is intentionally generic so tests can call it without needing an Automation instance.
        /// </summary>
        public static void WaitForBusyIndicator(TimeSpan? timeout = null)
        {
            var maxWait = timeout ?? TimeSpan.FromSeconds(10);
            try
            {
                using var automation = new UIA3Automation();

                FlaUI.Core.Tools.Retry.WhileTrue(() =>
                {
                    var desktop = automation.GetDesktop();
                    var windows = desktop.FindAllChildren(cf => cf.ByControlType(ControlType.Window));
                    foreach (var w in windows)
                    {
                        if (!w.IsAvailable) continue;
                        var busy = w.FindFirstDescendant(cf => cf.ByName("BusyIndicator").Or(cf.ByControlType(ControlType.ProgressBar)));
                        if (busy != null && !busy.IsOffscreen)
                        {
                            return true; // keep waiting
                        }
                    }
                    return false; // no busy indicators found
                }, maxWait, TimeSpan.FromMilliseconds(100));
            }
            catch
            {
                // Swallow exceptions - absence of automation or UI access should not fail tests here
            }
        }
    }
}
