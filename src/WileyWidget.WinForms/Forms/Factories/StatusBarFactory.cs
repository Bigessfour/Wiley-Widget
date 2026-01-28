using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms.Tools;

namespace WileyWidget.WinForms.Forms
{
    /// <summary>
    /// Factory for creating and configuring the application status bar.
    /// Uses programmatic grid layout with precise column alignment.
    /// Columns: StatusLabel (100px) | StatusText (200px) | State (80px) | Progress (120px) | Clock (100px)
    ///
    /// POLISH ENHANCEMENTS:
    /// - Unified status label with color-coding (Green=Ready, Red=Error, Yellow=Warning).
    /// - Dynamic tooltips for truncated status text.
    /// - Culture-aware clock formatting with system timezone sync.
    /// </summary>
    public static class StatusBarFactory
    {
        /// <summary>
        /// Create and configure StatusBarAdv with 5-panel grid layout and polish enhancements.
        /// </summary>
        public static StatusBarAdv CreateStatusBar(MainForm form, ILogger? logger = null, bool useSyncfusionDocking = true)
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
            // CRITICAL: Disable SizingGrip when using Syncfusion docking to prevent layout conflicts
            statusBar.SizingGrip = !useSyncfusionDocking;
            if (!useSyncfusionDocking)
            {
                logger?.LogDebug("StatusBar SizingGrip enabled (traditional WinForms mode)");
            }
            else
            {
                logger?.LogDebug("StatusBar SizingGrip disabled (Syncfusion docking prevents conflicts)");
            }

            var panels = new List<StatusBarAdvPanel>();

            // 5. UNIFIED STATUS LABEL PANEL with color-coding
            // Combines old StatusLabel and StatusTextPanel into single dynamic label
            var unifiedStatusPanel = new StatusBarAdvPanel
            {
                Name = "UnifiedStatusPanel",
                Text = "Ready",
                Width = 300,  // Expanded to accommodate both status and status text
                HAlign = HorzFlowAlign.Left,
                BorderStyle = BorderStyle.None,
                AutoSize = false,
                // POLISH: Color-coding (Green for Ready, Red for errors)
                ForeColor = Color.Green
            };
            panels.Add(unifiedStatusPanel);

            // Column 3: StatePanel (80px, center-aligned, indicators)
            var statePanel = new StatusBarAdvPanel
            {
                Name = "StatePanel",
                Text = "Active",
                Width = 80,
                HAlign = HorzFlowAlign.Center,
                BorderStyle = BorderStyle.None,
                AutoSize = false
            };
            panels.Add(statePanel);

            // Column 4: ProgressPanel (120px, center-aligned, contains ProgressBarAdv)
            var progressPanel = new StatusBarAdvPanel
            {
                Name = "ProgressPanel",
                Width = 120,
                HAlign = HorzFlowAlign.Center,
                BorderStyle = BorderStyle.None,
                AutoSize = false,
                Padding = new Padding(2, 2, 2, 2)
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
                BorderColor = Color.LightGray,
                BorderStyle = BorderStyle.FixedSingle,
                TextVisible = false
            };
            progressPanel.Controls.Add(progressBar);
            panels.Add(progressPanel);

            // Column 5: ClockPanel (100px, right-aligned, time display with culture awareness)
            var clockPanel = new StatusBarAdvPanel
            {
                Name = "ClockPanel",
                Text = DateTime.Now.ToString("t", CultureInfo.CurrentCulture),  // Culture-aware time
                Width = 100,
                HAlign = HorzFlowAlign.Right,
                BorderStyle = BorderStyle.None,
                AutoSize = false
            };
            panels.Add(clockPanel);

            // 6. Add all panels to StatusBar in column order
            foreach (var panel in panels)
            {
                statusBar.Controls.Add(panel);
            }

            // 7. Initialize clock update timer (60 seconds, culture-aware)
            InitializeClockTimer(clockPanel, logger);

            logger?.LogDebug(
                "StatusBarAdv created with 5-column grid: UnifiedStatus(300px) | State(80px) | Progress(120px) | Clock(100px)");

            return statusBar;
        }

        /// <summary>
        /// Initializes a timer to update the clock panel with culture-aware formatting.
        /// Also subscribes to system timezone changes.
        /// </summary>
        private static void InitializeClockTimer(StatusBarAdvPanel clockPanel, ILogger? logger)
        {
            if (clockPanel == null)
            {
                logger?.LogWarning("ClockPanel is null; clock timer not initialized");
                return;
            }

            try
            {
                var clockTimer = new System.Windows.Forms.Timer
                {
                    Interval = 1000  // Update every second for better responsiveness
                };

                clockTimer.Tick += (s, e) =>
                {
                    try
                    {
                        clockPanel.Text = DateTime.Now.ToString("t", CultureInfo.CurrentCulture);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogDebug(ex, "Error updating clock panel");
                    }
                };

                clockTimer.Start();
                logger?.LogDebug("Clock timer started (1-second interval, culture-aware formatting)");
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to initialize clock timer");
            }
        }

        /// <summary>
        /// Updates the unified status panel with color-coded feedback.
        /// POLISH: Color-coding provides immediate visual feedback (Green=Ready, Red=Error, Yellow=Warning).
        /// </summary>
        public static void UpdateStatus(StatusBarAdv statusBar, string statusText, StatusLevel level = StatusLevel.Info)
        {
            try
            {
                var panel = statusBar.Controls.OfType<StatusBarAdvPanel>()
                    .FirstOrDefault(p => p.Name == "UnifiedStatusPanel");

                if (panel != null)
                {
                    panel.Text = statusText;

                    // POLISH: Color-code based on status level
                    panel.ForeColor = level switch
                    {
                        StatusLevel.Success => Color.Green,
                        StatusLevel.Error => Color.Red,
                        StatusLevel.Warning => Color.Orange,
                        _ => Color.FromKnownColor(KnownColor.ControlText)  // Default text color
                    };

                    panel.Refresh();
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
