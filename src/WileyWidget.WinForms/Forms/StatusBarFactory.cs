using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms.Tools;

namespace WileyWidget.WinForms.Forms
{
    public static class StatusBarFactory
    {
        public static StatusBarAdv CreateStatusBar(MainForm form, ILogger? logger = null)
        {
            // 1. Validation - Intake form and logger
            if (form == null) throw new ArgumentNullException(nameof(form));
            logger?.LogDebug("Initializing StatusBarAdv for MainForm.");

            // 2. Control Initialization - Basic setup
            var statusBar = new StatusBarAdv
            {
                Name = "statusBarAdv",
                Dock = DockStyle.Bottom,
                Height = 26 // Fallback height
            };

            // 3. Layout Configuration - Alignment and Spacing
            statusBar.Alignment = FlowAlignment.Far;
            statusBar.Spacing = new Size(2, 2);

            // 4. Features - SizingGrip and Border Styles
            statusBar.SizingGrip = true;
            statusBar.BorderSides = Border3DSide.Top;
            statusBar.BorderStyle = BorderStyle.FixedSingle;

            var panels = new List<StatusBarAdvPanel>();

            // 5. Panel Type: StatusLabel (Information)
            var labelPanel = new StatusBarAdvPanel
            {
                Name = "StatusLabel",
                Text = "Ready",
                Width = 100,
                HAlign = HorzFlowAlign.Left
            };
            panels.Add(labelPanel);

            // 6. Panel Type: StatusTextPanel (Dynamic Content)
            var textPanel = new StatusBarAdvPanel
            {
                Name = "StatusTextPanel",
                Text = string.Empty,
                Width = 200,
                HAlign = HorzFlowAlign.Left
            };
            panels.Add(textPanel);

            // 7. Panel Type: StatePanel (Indicators)
            var statePanel = new StatusBarAdvPanel
            {
                Name = "StatePanel",
                Text = "Active",
                Width = 80,
                HAlign = HorzFlowAlign.Center
            };
            panels.Add(statePanel);

            // 8. Panel Type: ProgressPanel (Managed Controls)
            var progressPanel = new StatusBarAdvPanel
            {
                Name = "ProgressPanel",
                Width = 120,
                HAlign = HorzFlowAlign.Center
            };

            var progressBar = new ProgressBarAdv
            {
                Name = "statusBarProgressBar",
                Dock = DockStyle.Fill,
                Visible = false,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                BorderColor = Color.LightGray,
                BorderStyle = BorderStyle.FixedSingle
            };
            progressPanel.Controls.Add(progressBar);
            panels.Add(progressPanel);

            // 9. Panel Type: ClockPanel (Auto-updating)
            var clockPanel = new StatusBarAdvPanel
            {
                Name = "ClockPanel",
                Width = 100,
                HAlign = HorzFlowAlign.Right // Use Right alignment instead of Far
            };
            panels.Add(clockPanel);

            // 10. Implementation & Theming - Critical Syncfusion Rule
            // Add controls first for .NET Core WinForms compatibility
            foreach (var panel in panels)
            {
                statusBar.Controls.Add(panel);
            }

            // Theme cascades from the parent form via SfSkinManager; no per-control styling needed.

            logger?.LogDebug("StatusBarAdv initialization complete with {PanelCount} panels.", panels.Count);
            return statusBar;
        }
    }
}
