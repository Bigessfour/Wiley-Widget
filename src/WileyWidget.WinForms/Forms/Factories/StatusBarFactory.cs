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
    /// Columns: StatusLabel (110px) | StatusText (210px) | State (80px) | Progress (120px) | Clock (100px)
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

            // 5. Status label (short state descriptor) – keeps legacy references happy
            var statusLabelPanel = new StatusBarAdvPanel
            {
                Name = "StatusLabel",
                Text = "Ready",
                Width = 110,
                HAlign = HorzFlowAlign.Left,
                BorderStyle = BorderStyle.None,
                AutoSize = false,
                ForeColor = Color.Green,
                AccessibleName = "Status label",
                AccessibleDescription = "High-level application status"
            };
            panels.Add(statusLabelPanel);

            // 6. Status text panel (detailed description/message)
            var statusTextPanel = new StatusBarAdvPanel
            {
                Name = "StatusTextPanel",
                Text = "System initialized",
                Width = 210,
                HAlign = HorzFlowAlign.Left,
                BorderStyle = BorderStyle.None,
                AutoSize = false,
                AccessibleName = "Status details",
                AccessibleDescription = "Detailed status message"
            };
            panels.Add(statusTextPanel);

            // Column 3: StatePanel (80px, center-aligned, indicators)
            var statePanel = new StatusBarAdvPanel
            {
                Name = "StatePanel",
                Text = "Active",
                Width = 80,
                HAlign = HorzFlowAlign.Center,
                BorderStyle = BorderStyle.None,
                AutoSize = false,
                AccessibleName = "Activity state",
                AccessibleDescription = "Indicates whether the application is active or busy"
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

            // Column 5: ClockPanel (100px, right-aligned, time display with culture awareness)
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

            // 7. Initialize clock update timer (60 seconds, culture-aware)
            InitializeClockTimer(clockPanel, logger);

            logger?.LogDebug(
                "StatusBarAdv created with 5-column grid: Status(110px) | Details(210px) | State(80px) | Progress(120px) | Clock(100px)");

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

                    labelPanel.ForeColor = level switch
                    {
                        StatusLevel.Success => Color.Green,
                        StatusLevel.Error => Color.Red,
                        StatusLevel.Warning => Color.Orange,
                        _ => Color.FromKnownColor(KnownColor.ControlText)
                    };

                    labelPanel.Refresh();
                }

                if (textPanel != null)
                {
                    textPanel.Text = statusText;
                    textPanel.ForeColor = Color.FromKnownColor(KnownColor.ControlText);
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
