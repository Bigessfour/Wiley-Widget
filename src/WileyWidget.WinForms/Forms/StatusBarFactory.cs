using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.Controls;
using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace WileyWidget.WinForms.Forms;

/// <summary>
/// Factory for creating and configuring production-ready StatusBarAdv with embedded progress bar and status panels.
/// </summary>
/// <remarks>
/// <para><strong>Architecture:</strong> Creates StatusBarAdv with 5 standard panels: Status, Text, State, Progress (with embedded ProgressBarAdv), and Clock.</para>
/// <para><strong>Theming:</strong> Applies SfSkinManager theme explicitly - theme cascades to all panels and embedded progress bar automatically.</para>
/// <para><strong>Panel Configuration:</strong>
/// <list type="bullet">
/// <item><description>Status Label: Left-aligned, displays operation status (e.g., "Ready", "Processing...")</description></item>
/// <item><description>Status Text: Center-aligned, displays contextual information</description></item>
/// <item><description>State Panel: Left-aligned, displays docking/panel state indicators</description></item>
/// <item><description>Progress Panel: Right-aligned, contains embedded ProgressBarAdv for async operations (hidden by default)</description></item>
/// <item><description>Clock Panel: Right-aligned, displays current time in HH:mm format</description></item>
/// </list>
/// </para>
/// <para><strong>Progress Bar:</strong> Uses WaitingGradient-style appearance for indeterminate operations.</para>
/// <para><strong>Best Practices:</strong> SizingGrip enabled per Microsoft UX guidelines for resizable windows.</para>
/// </remarks>
public static class StatusBarFactory
{
    /// <summary>
    /// Creates and configures a production-ready StatusBarAdv with standard panels and embedded progress bar.
    /// </summary>
    /// <remarks>
    /// <para><strong>Panel Back-References:</strong> Wires panel references back to MainForm via SetStatusBarPanels() to enable ApplyStatus() and ShowProgress() methods.</para>
    /// <para><strong>Theme Application:</strong> Uses SfSkinManager.SetVisualStyle() - theme cascades to all child panels and controls automatically.</para>
    /// <para><strong>Clock Updates:</strong> Caller is responsible for wiring timer to update ClockPanel.Text (not handled by factory).</para>
    /// <para><strong>Progress Bar:</strong> Embedded ProgressBarAdv uses indeterminate appearance for smooth animation during async operations.</para>
    /// </remarks>
    /// <param name="form">MainForm instance that receives panel references for status/progress management</param>
    /// <param name="logger">Optional logger for diagnostics and theme application tracking</param>
    /// <returns>Fully configured StatusBarAdv ready to add to form Controls collection</returns>
    /// <exception cref="ArgumentNullException">Thrown when form parameter is null</exception>
    public static StatusBarAdv CreateStatusBar(MainForm form, ILogger? logger = null)
    {
        if (form == null)
        {
            throw new ArgumentNullException(nameof(form));
        }

        var statusBar = new StatusBarAdv
        {
            Name = "StatusBar_Main",
            AccessibleName = "Application Status Bar",
            AccessibleDescription = "Displays current operation status, progress, and system time",
            Dock = DockStyle.Bottom,
            BeforeTouchSize = new Size(1400, 26),
            SizingGrip = true,
            Border3DStyle = Border3DStyle.Adjust
        };

        // Initialize panels array (StatusBarAdv.Panels is an array, not a collection)
        statusBar.Panels = new StatusBarAdvPanel[5];

        // Status label (left) - primary operation status
        statusBar.Panels[0] = new StatusBarAdvPanel
        {
            Name = "StatusLabel",
            AccessibleName = "Status Label",
            Text = "Ready",
            HAlign = HorzFlowAlign.Left
        };

        // Status text panel (center) - contextual information
        statusBar.Panels[1] = new StatusBarAdvPanel
        {
            Name = "StatusTextPanel",
            AccessibleName = "Status Text",
            Text = string.Empty,
            Size = new Size(200, 27),
            HAlign = HorzFlowAlign.Center
        };

        // State panel (Docking/panel state indicator)
        statusBar.Panels[2] = new StatusBarAdvPanel
        {
            Name = "StatePanel",
            AccessibleName = "State Panel",
            Text = string.Empty,
            Size = new Size(100, 27),
            HAlign = HorzFlowAlign.Left
        };

        // Progress panel (right) - contains embedded ProgressBarAdv
        var progressBar = new ProgressBarAdv { Name = "ProgressBar_Embedded", AccessibleName = "Operation Progress", Visible = false, Width = 150, Height = 18, Dock = DockStyle.Fill };

        var progressPanel = new StatusBarAdvPanel
        {
            Name = "ProgressPanel",
            AccessibleName = "Progress Panel",
            Size = new Size(160, 27),
            HAlign = HorzFlowAlign.Right
        };
        progressPanel.Controls.Add(progressBar);

        statusBar.Panels[3] = progressPanel;

        // Clock panel (right) - displays current time
        statusBar.Panels[4] = new StatusBarAdvPanel
        {
            Name = "ClockPanel",
            AccessibleName = "System Clock",
            Text = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture),
            Size = new Size(50, 27),
            HAlign = HorzFlowAlign.Right
        };

        // Apply theme cascade
        SfSkinManager.SetVisualStyle(statusBar, SfSkinManager.ApplicationVisualTheme ?? "Office2019Colorful");

        logger?.LogDebug("StatusBarAdv created with {PanelCount} panels and embedded progress bar",
            statusBar.Panels.Length);

        return statusBar;
    }
}
