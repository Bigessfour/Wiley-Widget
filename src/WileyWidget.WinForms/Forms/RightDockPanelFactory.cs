using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.Drawing;
using WileyWidget.Business.Interfaces;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Services;
using GradientPanelExt = WileyWidget.WinForms.Controls.GradientPanelExt;

namespace WileyWidget.WinForms.Forms;

/// <summary>
/// Factory for creating and managing the right-docked panel container.
/// Hosts Activity Log only; JARVIS AI chat is available via modal JARVISChatHostForm.
/// </summary>
public static class RightDockPanelFactory
{
    /// <summary>
    /// Panel mode enumeration for tracking active view.
    /// </summary>
    public enum RightPanelMode
    {
        ActivityLog,
        JarvisChat
    }

    /// <summary>
    /// Create the right-docked panel container for Activity Log content.
    /// </summary>
    /// <param name="mainForm">Parent MainForm instance</param>
    /// <param name="serviceProvider">Service provider for dependency resolution</param>
    /// <param name="logger">Logger instance</param>
    /// <returns>Tuple of (rightDockPanel, activityLogPanel, initialMode)</returns>
    /// <remarks>
    /// The right panel is 350px wide with 320px minimum to accommodate Activity Log.
    /// JARVIS chat is launched via JARVISChatHostForm (modal) and is not docked.
    /// </remarks>
    public static (
        GradientPanelExt rightDockPanel,
        ActivityLogPanel activityLogPanel,
        RightPanelMode initialMode
    ) CreateRightDockPanel(
        MainForm mainForm,
        IServiceProvider serviceProvider,
        ILogger? logger)
    {
        if (mainForm == null) throw new ArgumentNullException(nameof(mainForm));
        if (serviceProvider == null) throw new ArgumentNullException(nameof(serviceProvider));

        var sw = Stopwatch.StartNew();
        logger?.LogInformation("RightDockPanelFactory: Creating right-docked panel container (Activity Log)");

        try
        {
            // Create container panel
            var rightDockPanel = new GradientPanelExt
            {
                Dock = DockStyle.Right,
                Width = 350,
                MinimumSize = new Size(320, 0),
                BorderStyle = BorderStyle.None,
                BackgroundColor = new BrushInfo(Color.WhiteSmoke),
                Name = "RightDockPanel"
            };

            // Create TabControl for Activity Log
            var tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Name = "RightPanelTabControl"
            };

            // Tab 1: Activity Log
            var activityLogTab = new TabPage { Text = "Activity Log", Name = "ActivityLogTab" };
            var scopeFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(serviceProvider);
            var activityLogLogger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ILogger<ActivityLogPanel>>(serviceProvider)
                ?? (Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ILogger>(serviceProvider) as ILogger<ActivityLogPanel>)
                ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ActivityLogPanel>.Instance;
            var activityLogPanel = new ActivityLogPanel(scopeFactory, activityLogLogger)
            {
                Dock = DockStyle.Fill,
                Name = "ActivityLogPanel"
            };
            activityLogTab.Controls.Add(activityLogPanel);
            tabControl.TabPages.Add(activityLogTab);

            rightDockPanel.Controls.Add(tabControl);

            // Store panel mode and tab control for later retrieval
            rightDockPanel.Tag = RightPanelMode.ActivityLog;
            rightDockPanel.Name = "RightDockPanel_WithTabs";

            sw.Stop();
            logger?.LogInformation(
                "RightDockPanelFactory: Right panel created in {ElapsedMs}ms - Activity Log initialized as default",
                sw.ElapsedMilliseconds);

            return (rightDockPanel, activityLogPanel, RightPanelMode.ActivityLog);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to create right dock panel with TabControl");
            throw;
        }
    }

    /// <summary>
    /// Toggle right panel content between Activity Log and JARVIS Chat.
    /// If JARVIS modal is preferred, this becomes a no-op (modal launched via ribbon instead).
    /// </summary>
    /// <param name="rightDockPanel">Right dock panel container</param>
    /// <param name="targetMode">Target panel mode</param>
    /// <param name="logger">Logger instance</param>
    /// <remarks>
    /// Current implementation keeps Activity Log as default.
    /// To add JARVIS docked tab:
    /// 1. Create JARVISChatUserControl (convert JARVISChatHostForm to UserControl)
    /// 2. Implement TabControl or segmented control in rightDockPanel
    /// 3. Switch between tabs in this method
    /// 4. Update MainForm.Tag to persist selected tab in layout save/load
    /// </remarks>
    public static void SwitchRightPanelContent(
        GradientPanelExt rightDockPanel,
        RightPanelMode targetMode,
        ILogger? logger)
    {
        if (rightDockPanel == null) throw new ArgumentNullException(nameof(rightDockPanel));
        if (rightDockPanel.IsDisposed)
        {
            logger?.LogWarning("RightDockPanelFactory: Cannot switch panel - right dock panel is disposed");
            return;
        }

        try
        {
            var currentMode = (RightPanelMode?)rightDockPanel.Tag ?? RightPanelMode.ActivityLog;

            if (currentMode == targetMode)
            {
                logger?.LogDebug("RightDockPanelFactory: Already in {TargetMode} mode - skipping switch", targetMode);
                return;
            }

            logger?.LogInformation("RightDockPanelFactory: Switching right panel from {CurrentMode} to {TargetMode}",
                currentMode, targetMode);

            // FEATURE: Implement tab switching when JARVIS docked tab is added
            // For now, Activity Log is the only docked content
            switch (targetMode)
            {
                case RightPanelMode.ActivityLog:
                    // Ensure Activity Log is visible via TabControl
                    if (rightDockPanel.Controls[0] is TabControl tabControl)
                    {
                        var activityLogTab = tabControl.TabPages.Cast<TabPage>()
                            .FirstOrDefault(tp => tp.Name == "ActivityLogTab");
                        if (activityLogTab != null)
                        {
                            tabControl.SelectedTab = activityLogTab;
                            activityLogTab.Visible = true;
                            logger?.LogDebug("RightDockPanelFactory: Activity Log tab selected and visible");
                        }
                    }
                    else
                    {
                        // Fallback: Direct control visibility
                        var activityPanel = rightDockPanel.Controls["ActivityLogPanel"] as ActivityLogPanel;
                        if (activityPanel != null && !activityPanel.IsDisposed)
                        {
                            activityPanel.Visible = true;
                            activityPanel.BringToFront();
                            logger?.LogDebug("RightDockPanelFactory: Activity Log panel brought to front (fallback)");
                        }
                    }
                    break;

                case RightPanelMode.JarvisChat:
                    // FUTURE: Implement when JARVISChatUserControl is created from JARVISChatHostForm
                    // Implementation steps:
                    // 1. Create JARVISChatUserControl.cs by converting JARVISChatHostForm to UserControl
                    // 2. Add as second TabPage in CreateRightDockPanel:
                    //    var jarvisTab = new TabPage { Text = "JARVIS Chat", Name = "JARVISChatTab" };
                    //    var jarvisControl = new JARVISChatUserControl(...) { Dock = DockStyle.Fill };
                    //    jarvisTab.Controls.Add(jarvisControl);
                    //    tabControl.TabPages.Add(jarvisTab);
                    // 3. Then uncomment this implementation to switch tabs:
                    if (rightDockPanel.Controls[0] is TabControl tc)
                    {
                        var jarvisTab = tc.TabPages.Cast<TabPage>()
                            .FirstOrDefault(tp => tp.Name == "JARVISChatTab");
                        if (jarvisTab != null)
                        {
                            tc.SelectedTab = jarvisTab;
                            jarvisTab.Visible = true;
                            logger?.LogDebug("RightDockPanelFactory: JARVIS Chat tab selected");
                        }
                        else
                        {
                            logger?.LogInformation("RightDockPanelFactory: JARVIS Chat tab not yet created - use modal dialog instead");
                        }
                    }
                    break;
            }

            // Update tracked mode
            rightDockPanel.Tag = targetMode;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to switch right panel content to {TargetMode}", targetMode);
        }
    }

    /// <summary>
    /// Get the current mode of the right dock panel.
    /// </summary>
    /// <param name="rightDockPanel">Right dock panel container</param>
    /// <returns>Current RightPanelMode</returns>
    public static RightPanelMode GetCurrentMode(GradientPanelExt rightDockPanel)
    {
        if (rightDockPanel == null) throw new ArgumentNullException(nameof(rightDockPanel));
        return (RightPanelMode?)rightDockPanel.Tag ?? RightPanelMode.ActivityLog;
    }

    /// <summary>
    /// Set the right panel mode (for layout persistence restoration).
    /// </summary>
    /// <param name="rightDockPanel">Right dock panel container</param>
    /// <param name="mode">Mode to set</param>
    public static void SetMode(GradientPanelExt rightDockPanel, RightPanelMode mode)
    {
        if (rightDockPanel == null) throw new ArgumentNullException(nameof(rightDockPanel));
        rightDockPanel.Tag = mode;
    }
}
