using Syncfusion.Windows.Forms.Tools;
using System.Drawing;
using System.Windows.Forms;

namespace WileyWidget.WinForms.Forms;

/// <summary>
/// Factory for creating and configuring MainForm StatusBar with embedded progress bar.
/// Follows Syncfusion StatusBarAdv API - single source for status bar initialization.
/// </summary>
public static class StatusBarFactory
{
    /// <summary>
    /// Create and configure StatusBarAdv with standard panels (status, text, state, progress, clock).
    /// Wires back-references to MainForm for ApplyStatus/ShowProgress methods.
    /// </summary>
    /// <param name="form">MainForm instance - receives field assignments for panel references</param>
    /// <returns>Fully configured StatusBarAdv ready to add to Controls</returns>
    public static StatusBarAdv CreateStatusBar(MainForm form)
    {
        if (form == null)
        {
            throw new System.ArgumentNullException(nameof(form));
        }

        var statusBar = new StatusBarAdv
        {
            Name = "StatusBar_Main",
            AccessibleName = "StatusBar_Main",
            AccessibleDescription = "Application status bar showing current operation status and information",
            Dock = DockStyle.Bottom,
            BeforeTouchSize = new Size(1400, 26),
            SizingGrip = true  // Standard UX per MS Docs
        };

        // Status label (left)
        var statusLabel = new StatusBarAdvPanel
        {
            Name = "StatusLabel",
            Text = "Ready",
            HAlign = HorzFlowAlign.Left
        };

        // Status text panel (center)
        var statusTextPanel = new StatusBarAdvPanel
        {
            Name = "StatusTextPanel",
            Text = string.Empty,
            Size = new Size(200, 27),
            HAlign = HorzFlowAlign.Center
        };

        // State panel (Docking indicator)
        var statePanel = new StatusBarAdvPanel
        {
            Name = "StatePanel",
            Text = string.Empty,
            Size = new Size(100, 27),
            HAlign = HorzFlowAlign.Left
        };

        // Progress panel (for async operations)
        var progressPanel = new StatusBarAdvPanel
        {
            Name = "ProgressPanel",
            Text = string.Empty,
            Size = new Size(150, 27),
            HAlign = HorzFlowAlign.Right,
            Visible = false
        };

        // Embedded ProgressBarAdv per Syncfusion API
        var progressBar = new ProgressBarAdv
        {
            Name = "ProgressBar",
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            Dock = DockStyle.Fill,
            ProgressStyle = ProgressBarStyles.WaitingGradient
        };
        progressPanel.Controls.Add(progressBar);

        // Clock panel (right)
        var clockPanel = new StatusBarAdvPanel
        {
            Name = "ClockPanel",
            Text = System.DateTime.Now.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture),
            Size = new Size(80, 27),
            HAlign = HorzFlowAlign.Right
        };

        // Add all panels to status bar
        statusBar.Controls.Add(statusLabel);
        statusBar.Controls.Add(statusTextPanel);
        statusBar.Controls.Add(statePanel);
        statusBar.Controls.Add(progressPanel);
        statusBar.Controls.Add(clockPanel);

        // Wire back-references to MainForm (internal fields)
        // These allow ApplyStatus/ShowProgress methods to work
        form.SetStatusBarPanels(statusBar, statusLabel, statusTextPanel, statePanel, progressPanel, progressBar, clockPanel);

        return statusBar;
    }
}
