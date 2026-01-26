using System;
using System.Collections.Generic;
using System.Drawing;
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
    /// </summary>
    public static class StatusBarFactory
    {
        /// <summary>
        /// Create and configure StatusBarAdv with 5-panel grid layout.
        /// Each panel is a column in a precise grid for consistent alignment.
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

            // 5. Create 5-column panel grid with fixed widths
            // Column 1: StatusLabel (100px, left-aligned)
            var labelPanel = new StatusBarAdvPanel
            {
                Name = "StatusLabel",
                Text = "Ready",
                Width = 100,
                HAlign = HorzFlowAlign.Left,
                BorderStyle = BorderStyle.None,
                AutoSize = false
            };
            panels.Add(labelPanel);

            // Column 2: StatusTextPanel (200px, left-aligned, dynamic content)
            var textPanel = new StatusBarAdvPanel
            {
                Name = "StatusTextPanel",
                Text = string.Empty,
                Width = 200,
                HAlign = HorzFlowAlign.Left,
                BorderStyle = BorderStyle.None,
                AutoSize = false
            };
            panels.Add(textPanel);

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

            // Column 5: ClockPanel (100px, right-aligned, time display)
            var clockPanel = new StatusBarAdvPanel
            {
                Name = "ClockPanel",
                Text = DateTime.Now.ToString("HH:mm:ss"),
                Width = 100,
                HAlign = HorzFlowAlign.Right,
                BorderStyle = BorderStyle.None,
                AutoSize = false
            };
            panels.Add(clockPanel);

            // 6. Add all panels to StatusBar in column order
            // StatusBarAdv will lay them out left-to-right with specified widths
            foreach (var panel in panels)
            {
                statusBar.Controls.Add(panel);
            }

            // 7. Theming
            // Theme cascades from parent form via SfSkinManager
            // No per-panel color assignments - all theming via SfSkinManager
            // (Compliant with SfSkinManager as sole source of truth)

            logger?.LogDebug(
                "StatusBarAdv created with 5-column grid: StatusLabel(100px) | StatusText(200px) | State(80px) | Progress(120px) | Clock(100px)");

            return statusBar;
        }
    }
}
