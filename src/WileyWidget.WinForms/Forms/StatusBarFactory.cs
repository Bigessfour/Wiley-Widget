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
    /// <para><strong>Theme Application:</strong> Uses SkinManager.SetVisualStyle() - theme cascades to all child panels and controls automatically.</para>
    /// <para><strong>Clock Updates:</strong> Caller is responsible for wiring timer to update ClockPanel.Text (not handled by factory).</para>
    /// <para><strong>Progress Bar:</strong> Embedded ProgressBarAdv uses indeterminate appearance for smooth animation during async operations.</para>
    /// </remarks>
    /// <param name="form">MainForm instance that receives panel references for status/progress management</param>
    /// <param name="logger">Optional logger for diagnostics and theme application tracking</param>
    /// <param name="themeName">Optional theme name for SkinManager. If null, uses current application theme.</param>
    /// <returns>Fully configured StatusBarAdv ready to add to form Controls collection</returns>
    /// <exception cref="ArgumentNullException">Thrown when form parameter is null</exception>
    public static StatusBarAdv CreateStatusBar(MainForm form, ILogger? logger = null, string? themeName = null)
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

        var panels = EnsurePanelBuffer(statusBar, requiredLength: 5, logger);
        if (panels.Length < 5)
        {
            logger?.LogWarning("StatusBarAdv panel buffer could not be sized to 5; created with length {Length}. Optional panels will be skipped.", panels.Length);
        }

        // Status label (left) - primary operation status
        TryAssignPanel(statusBar, 0, new StatusBarAdvPanel
        {
            Name = "StatusLabel",
            AccessibleName = "Status Label",
            Text = "Ready",
            HAlign = HorzFlowAlign.Left
        }, logger, optional: false);

        // Status text panel (center) - contextual information
        TryAssignPanel(statusBar, 1, new StatusBarAdvPanel
        {
            Name = "StatusTextPanel",
            AccessibleName = "Status Text",
            Text = string.Empty,
            Size = new Size(200, 27),
            HAlign = HorzFlowAlign.Center
        }, logger, optional: false);

        // State panel (Docking/panel state indicator)
        TryAssignPanel(statusBar, 2, new StatusBarAdvPanel
        {
            Name = "StatePanel",
            AccessibleName = "State Panel",
            Text = string.Empty,
            Size = new Size(100, 27),
            HAlign = HorzFlowAlign.Left
        }, logger, optional: true);

        // Progress panel (right) - contains embedded ProgressBarAdv
        var progressBar = new ProgressBarAdv { Name = "ProgressBar_Embedded", AccessibleName = "Operation Progress", AccessibleDescription = "Indicates progress for long running operations", Visible = false, Width = 150, Height = 18, Dock = DockStyle.Fill };

        var progressPanel = new StatusBarAdvPanel
        {
            Name = "ProgressPanel",
            AccessibleName = "Progress Panel",
            Size = new Size(160, 27),
            HAlign = HorzFlowAlign.Right
        };
        progressPanel.Controls.Add(progressBar);
        // Note: ProgressBarAdv lifecycle managed by StatusBarAdvPanel.Controls collection
        // panel.Dispose() will cascade to child controls

        TryAssignPanel(statusBar, 3, progressPanel, logger, optional: true);

        // Clock panel (right) - displays current time
        TryAssignPanel(statusBar, 4, new StatusBarAdvPanel
        {
            Name = "ClockPanel",
            AccessibleName = "System Clock",
            Text = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture),
            Size = new Size(50, 27),
            HAlign = HorzFlowAlign.Right
        }, logger, optional: true);

        // Apply theme cascade
        SfSkinManager.SetVisualStyle(statusBar, SfSkinManager.ApplicationVisualTheme ?? "Office2019Colorful");

        logger?.LogDebug("StatusBarAdv created with {PanelCount} panels and embedded progress bar",
            statusBar.Panels.Length);

        return statusBar;
    }

    private static StatusBarAdvPanel[] EnsurePanelBuffer(StatusBarAdv statusBar, int requiredLength, ILogger? logger)
    {
        var panels = statusBar.Panels;
        var currentLength = panels?.Length ?? 0;
        if (panels == null || currentLength < requiredLength)
        {
            var newLength = Math.Max(requiredLength, panels?.Length ?? 0);
            panels = new StatusBarAdvPanel[newLength];
            statusBar.Panels = panels;
            logger?.LogWarning("StatusBarAdv panels length was {CurrentLength}; resized to {NewLength}", currentLength, newLength);
        }

        return statusBar.Panels ?? Array.Empty<StatusBarAdvPanel>();
    }

    private static bool TryAssignPanel(StatusBarAdv statusBar, int index, StatusBarAdvPanel panel, ILogger? logger, bool optional)
    {
        if (statusBar.Panels == null || statusBar.Panels.Length <= index)
        {
            logger?.LogWarning(optional
                ? "Skipping optional StatusBarAdv panel at index {Index} due to insufficient capacity"
                : "StatusBarAdv missing required panel index {Index}; returning without assignment",
                index);
            return false;
        }

        statusBar.Panels[index] = panel;
        return true;
    }
}
