using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;
using System;
using System.Drawing;
using System.Windows.Forms;
using WileyWidget.WinForms.Themes;

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
/// <para><strong>Progress Bar:</strong> Uses ProgressBarStyles.WaitingGradient for indeterminate operations per Syncfusion API.</para>
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
    /// <para><strong>Progress Bar:</strong> Embedded ProgressBarAdv uses WaitingGradient style for smooth indeterminate animation during async operations.</para>
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
            SizingGrip = true,  // Standard UX per Microsoft Design Guidelines
            // Border style for professional appearance
            Border3DStyle = Border3DStyle.Adjust
        };

        // Status label (left) - primary operation status
        var statusLabel = new StatusBarAdvPanel
        {
            Name = "StatusLabel",
            AccessibleName = "Status Label",
            Text = "Ready",
            HAlign = HorzFlowAlign.Left
        };

        // Status text panel (center) - contextual information
        var statusTextPanel = new StatusBarAdvPanel
        {
            Name = "StatusTextPanel",
            AccessibleName = "Status Text",
            Text = string.Empty,
            Size = new Size(200, 27),
            HAlign = HorzFlowAlign.Center
        };

        // State panel (Docking/panel state indicator)
        var statePanel = new StatusBarAdvPanel
        {
            Name = "StatePanel",
            AccessibleName = "State Panel",
            Text = string.Empty,
            Size = new Size(100, 27),
            HAlign = HorzFlowAlign.Left
        };

        // Progress panel (for async operations) - hidden by default
        var progressPanel = new StatusBarAdvPanel
        {
            Name = "ProgressPanel",
            AccessibleName = "Progress Panel",
            AccessibleDescription = "Progress indicator for long-running operations",
            Text = string.Empty,
            Size = new Size(150, 27),
            HAlign = HorzFlowAlign.Right,
            Visible = false
        };

        // Embedded ProgressBarAdv per Syncfusion API best practices
        // Uses WaitingGradient for indeterminate progress (per Syncfusion docs)
        var progressBar = new ProgressBarAdv
        {
            Name = "ProgressBar",
            AccessibleName = "Progress Bar",
            AccessibleDescription = "Progress indicator",
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            Dock = DockStyle.Fill,
            ProgressStyle = ProgressBarStyles.WaitingGradient,
            // Performance: reduce animation overhead
            WaitingGradientWidth = 20,
            // Theme cascade will handle colors
            BackSegments = false  // Cleaner appearance
        };
        progressPanel.Controls.Add(progressBar);

        // Clock panel (right) - displays current time
        var clockPanel = new StatusBarAdvPanel
        {
            Name = "ClockPanel",
            AccessibleName = "Clock",
            AccessibleDescription = "Current time display",
            Text = DateTime.Now.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture),
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
        // These enable ApplyStatus/ShowProgress methods to manipulate panels
        form.SetStatusBarPanels(statusBar, statusLabel, statusTextPanel, statePanel, progressPanel, progressBar, clockPanel);

        // CRITICAL: SfSkinManager is SOLE PROPRIETOR of all theme and color decisions (per approved workflow)
        // Explicit theme application (defensive coding - ensures theme applied even if cascade fails)
        // NO manual color assignments (BackColor, ForeColor) - theme cascade handles all panels
        var currentTheme = SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme;
        try
        {
            SfSkinManager.SetVisualStyle(statusBar, currentTheme);
            logger?.LogDebug("[STATUSBAR_FACTORY] Theme explicitly applied via SfSkinManager: {Theme}", currentTheme);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "[STATUSBAR_FACTORY] Failed to apply explicit theme to StatusBar - relying on cascade from parent");
        }

        return statusBar;
    }
}
