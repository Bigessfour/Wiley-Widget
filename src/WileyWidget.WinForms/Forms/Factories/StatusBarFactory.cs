using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Themes;

namespace WileyWidget.WinForms.Forms
{
    /// <summary>
    /// Factory for creating and configuring the application status bar.
    /// Uses programmatic layout with a single primary message panel plus progress and clock.
    /// The richer professional panels are appended by MainForm after creation.
    ///
    /// POLISH ENHANCEMENTS:
    /// - Unified status label with color-coding (Green=Ready, Red=Error, Yellow=Warning).
    /// - Dynamic tooltips for truncated status text.
    /// - Culture-aware clock formatting with system timezone sync.
    /// </summary>
    public static class StatusBarFactory
    {
        /// <summary>
        /// Create and configure StatusBarAdv with the core professional status panels.
        /// </summary>
        public static StatusBarAdv CreateStatusBar(MainForm form, ILogger? logger = null, bool useSyncfusionDocking = false)
        {
            // 1. Validation - Intake form and logger
            if (form == null) throw new ArgumentNullException(nameof(form));
            logger?.LogDebug("Initializing StatusBarAdv with programmatic 5-column grid layout");

            // 2. Create StatusBarAdv container
            var statusBar = new StatusBarAdv
            {
                Name = "statusBarAdv",
                Dock = DockStyle.Bottom,
                Height = 26 // Standard status bar height
            };

            // 3. Layout Configuration - Flat Office 2019 style
            statusBar.Alignment = FlowAlignment.Far;
            statusBar.Spacing = new Size(0, 0); // No spacing between panels - precise grid layout
            statusBar.BorderStyle = BorderStyle.None; // Theme manager handles separator

            // 4. Features - SizingGrip configuration
            // Keep the grip disabled in the Syncfusion shell. The main window already exposes normal
            // form resizing, and enabling the StatusBarAdv grip can recurse through non-client message
            // handling on RibbonForm/TabbedMDI startup.
            statusBar.SizingGrip = false;
            if (useSyncfusionDocking)
            {
                logger?.LogDebug("StatusBar SizingGrip disabled (Syncfusion docking prevents conflicts)");
            }
            else
            {
                logger?.LogDebug("StatusBar SizingGrip disabled for the main Syncfusion shell");
            }

            var panels = new List<StatusBarAdvPanel>();

            // Primary status message panel — this replaces the old mini status strip.
            // ✅ Semantic status color exception (see SfSkinManager rule): Ready=Green, Error=Red, Warning=Yellow.
            var primaryStatusPanel = new StatusBarAdvPanel
            {
                Name = "PrimaryStatusPanel",
                Text = "Ready",
                Width = 360,
                HAlign = HorzFlowAlign.Left,
                BorderStyle = BorderStyle.None,
                AutoSize = false,
                ForeColor = ThemeColors.Success,
                AccessibleName = "Primary status",
                AccessibleDescription = "Primary application status message"
            };
            panels.Add(primaryStatusPanel);

            // ProgressPanel (120px, center-aligned, contains ProgressBarAdv)
            var progressPanel = new StatusBarAdvPanel
            {
                Name = "ProgressPanel",
                Width = 120,
                HAlign = HorzFlowAlign.Center,
                BorderStyle = BorderStyle.None,
                AutoSize = false,
                Padding = new Padding(2, 2, 2, 2),
                AccessibleName = "Progress",
                AccessibleDescription = "Shows progress for the current operation"
            };

            // Create ProgressBarAdv with proper theming
            var progressBar = new ProgressBarAdv
            {
                Name = "statusBarProgressBar",
                Dock = DockStyle.Fill,
                Visible = false,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                BorderStyle = BorderStyle.FixedSingle,
                TextVisible = false,
                AccessibleName = "Operation progress",
                AccessibleDescription = "Progress indicator for the current operation"
            };
            progressPanel.Controls.Add(progressBar);
            panels.Add(progressPanel);

            // ClockPanel (100px, right-aligned, time display with culture awareness)
            var clockPanel = new StatusBarAdvPanel
            {
                Name = "ClockPanel",
                Text = DateTime.Now.ToString("t", CultureInfo.CurrentCulture),  // Culture-aware time
                Width = 100,
                HAlign = HorzFlowAlign.Right,
                BorderStyle = BorderStyle.None,
                AutoSize = false,
                AccessibleName = "Clock",
                AccessibleDescription = "Current time"
            };
            panels.Add(clockPanel);

            // 6. Add all panels to StatusBar in column order
            statusBar.Panels = panels.ToArray();

            logger?.LogDebug(
                "StatusBarAdv created with primary status + progress + clock core panels");

            return statusBar;
        }

        /// <summary>
        /// Updates the unified status panel with color-coded feedback.
        /// POLISH: Color-coding provides immediate visual feedback (Green=Ready, Red=Error, Yellow=Warning).
        /// THREAD-SAFE: Uses InvokeRequired to ensure UI thread access.
        /// </summary>
        public static void UpdateStatus(StatusBarAdv statusBar, string statusText, StatusLevel level = StatusLevel.Info)
        {
            try
            {
                // Ensure thread safety for UI updates
                if (statusBar.InvokeRequired)
                {
                    statusBar.Invoke(new System.Action(() => UpdateStatus(statusBar, statusText, level)));
                    return;
                }

                var panels = statusBar.Controls.OfType<StatusBarAdvPanel>().ToArray();
                var labelPanel = panels.FirstOrDefault(p => p.Name == "StatusLabelPanel");
                var textPanel = panels.FirstOrDefault(p => p.Name == "StatusTextPanel");

                if (labelPanel != null)
                {
                    labelPanel.Text = level switch
                    {
                        StatusLevel.Success => "Ready",
                        StatusLevel.Error => "Error",
                        StatusLevel.Warning => "Warning",
                        _ => "Info"
                    };

                    if (level == StatusLevel.Success)
                    {
                        labelPanel.ForeColor = ThemeColors.Success;
                    }
                    else if (level == StatusLevel.Error)
                    {
                        labelPanel.ForeColor = ThemeColors.Error;
                    }
                    else if (level == StatusLevel.Warning)
                    {
                        labelPanel.ForeColor = ThemeColors.Warning;
                    }
                    else
                    {
                        labelPanel.ResetForeColor();
                    }

                    labelPanel.Refresh();
                }

                if (textPanel != null)
                {
                    textPanel.Text = statusText;
                    textPanel.ResetForeColor();
                    textPanel.Refresh();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating status: {ex.Message}");
            }
        }

        /// <summary>
        /// Status level enum for color-coded display.
        /// </summary>
        public enum StatusLevel
        {
            Info = 0,
            Success = 1,
            Warning = 2,
            Error = 3
        }
    }
}
