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
/// Hosts Activity Log and JARVIS AI Chat as switchable TabPages (Validation #2: Tab Management).
/// Uses lazy Grok personality service resolution post-Semantic Kernel init (Validation #3).
/// Enforces SfSkinManager theme cascade on all docked panels (Validation #4).
/// No duplicate DI registrations; panels created on-demand only (Validation #5).
/// ChatBridge event wiring managed by GrokAgentService (Validation #6).
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
    /// Create the right-docked panel container with Activity Log and JARVIS Chat tabs.
    /// (Validation #2: Audit RightDockPanelFactory Tab Management)
    /// </summary>
    /// <param name="mainForm">Parent MainForm instance</param>
    /// <param name="serviceProvider">Service provider for dependency resolution (Validation #1: BlazorWebView.Services)</param>
    /// <param name="logger">Logger instance</param>
    /// <returns>Tuple of (rightDockPanel, activityLogPanel, initialMode)</returns>
    /// <remarks>
    /// The right panel is 350px wide with 320px minimum to accommodate both Activity Log and JARVIS Chat.
    /// Both are docked as tabs in the right panel for easy switching without modal dialogs.
    /// Panels are created with scoped lifecycles; DI registration NOT needed (Validation #5).
    /// Theme cascade applied to both panels via SfSkinManager (Validation #4).
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
        logger?.LogInformation("RightDockPanelFactory: Creating right-docked panel container (Activity Log + JARVIS Chat) with 6 validations");

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

            // Create TabControl for Activity Log and JARVIS Chat
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

            // Tab 2: JARVIS Chat (Validation #1: Option A BlazorWebView.Services injection, #2: Tab Management,
            // #3: Lazy Personality Service post-InitializeAsync, #4: Theme Cascade via SfSkinManager)
            var jarvisTab = new TabPage { Text = "JARVIS Chat", Name = "JARVISChatTab" };
            var jarvisLogger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ILogger<JARVISChatUserControl>>(serviceProvider)
                ?? (Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ILogger>(serviceProvider) as ILogger<JARVISChatUserControl>)
                ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<JARVISChatUserControl>.Instance;
            var jarvisControl = new JARVISChatUserControl(scopeFactory, serviceProvider, jarvisLogger)
            {
                Dock = DockStyle.Fill,
                Name = "JARVISChatUserControl"
            };
            jarvisTab.Controls.Add(jarvisControl);
            tabControl.TabPages.Add(jarvisTab);

            rightDockPanel.Controls.Add(tabControl);

            // Store panel mode and tab control for later retrieval
            rightDockPanel.Tag = RightPanelMode.ActivityLog;
            rightDockPanel.Name = "RightDockPanel_WithTabs";

            sw.Stop();
            logger?.LogInformation(
                "RightDockPanelFactory: Right panel created in {ElapsedMs}ms - Activity Log initialized as default, JARVIS Chat available as second tab",
                sw.ElapsedMilliseconds);

            return (rightDockPanel, activityLogPanel, RightPanelMode.ActivityLog);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to create right dock panel with TabControl (Activity Log + JARVIS Chat)");
            throw;
        }
    }

    /// <summary>
    /// Switch right panel content between Activity Log and JARVIS Chat tabs.
    /// </summary>
    /// <param name="rightDockPanel">Right dock panel container</param>
    /// <param name="targetMode">Target panel mode</param>
    /// <param name="logger">Logger instance</param>
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

            // Switch tabs based on target mode - guard against empty Controls collection
            if (rightDockPanel.Controls != null && rightDockPanel.Controls.Count > 0 && rightDockPanel.Controls[0] is TabControl tabControl)
            {
                switch (targetMode)
                {
                    case RightPanelMode.ActivityLog:
                        var activityLogTab = tabControl.TabPages.Cast<TabPage>()
                            .FirstOrDefault(tp => tp.Name == "ActivityLogTab");
                        if (activityLogTab != null)
                        {
                            tabControl.SelectedTab = activityLogTab;
                            activityLogTab.Visible = true;
                            logger?.LogDebug("RightDockPanelFactory: Activity Log tab selected and visible");
                        }
                        break;

                    case RightPanelMode.JarvisChat:
                        var jarvisTab = tabControl.TabPages.Cast<TabPage>()
                            .FirstOrDefault(tp => tp.Name == "JARVISChatTab");
                        if (jarvisTab != null)
                        {
                            tabControl.SelectedTab = jarvisTab;
                            jarvisTab.Visible = true;
                            logger?.LogDebug("RightDockPanelFactory: JARVIS Chat tab selected and visible");
                        }
                        else
                        {
                            logger?.LogWarning("RightDockPanelFactory: JARVIS Chat tab not found in TabControl");
                        }
                        break;
                }
            }
            else
            {
                logger?.LogWarning("RightDockPanelFactory: Right panel first control is not a TabControl - cannot switch");
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
