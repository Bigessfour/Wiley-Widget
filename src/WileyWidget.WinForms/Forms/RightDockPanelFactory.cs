using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Controls.Panels;
using WileyWidget.WinForms.Factories;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Forms;

/// <summary>
/// Factory for creating and managing the right-docked panel container.
/// Hosts Activity Log and JARVIS Chat as persistent, always-available tabs.
/// Enforces SfSkinManager theme cascade on all docked panels.
/// </summary>
public static class RightDockPanelFactory
{

    /// <summary>
    /// Creates the right-docked panel container with persistent Syncfusion tabs.
    /// </summary>
    /// <param name="mainForm">Parent MainForm instance</param>
    /// <param name="serviceProvider">Service provider for dependency resolution</param>
    /// <param name="logger">Logger instance</param>
    /// <returns>Tuple of right panel container, tab host, and hosted panels.</returns>
    /// <remarks>
    /// The right panel is 350px wide with 320px minimum to accommodate Activity Log.
    /// Panel is created with scoped lifecycles; DI registration NOT needed.
    /// Theme cascade applied via SfSkinManager.
    /// </remarks>
    public static (
        Panel rightDockPanel,
        TabControlAdv rightDockTabs,
        ActivityLogPanel activityLogPanel,
        JARVISChatUserControl jarvisChatPanel
    ) CreateRightDockPanel(
        MainForm mainForm,
        IServiceProvider serviceProvider,
        ILogger? logger)
    {
        if (mainForm == null) throw new ArgumentNullException(nameof(mainForm));
        if (serviceProvider == null) throw new ArgumentNullException(nameof(serviceProvider));

        var sw = Stopwatch.StartNew();
        logger?.LogInformation("RightDockPanelFactory: Creating right-docked panel with Activity Log + JARVIS Chat");

        ThemeColors.EnsureThemeAssemblyLoaded(logger);
        var themeName = SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme;

        try
        {
            // Create container panel
            var rightDockPanel = new Panel
            {
                Dock = DockStyle.Right,
                Width = 420,
                MinimumSize = new Size(360, 0),
                BorderStyle = BorderStyle.None,
                Name = "RightDockPanel"
            };

            var rightDockTabs = new TabControlAdv
            {
                Dock = DockStyle.Fill,
                Name = "RightDockTabs",
                ThemeName = themeName,
                ThemesEnabled = true,
                BorderStyle = BorderStyle.None
            };
            SfSkinManager.SetVisualStyle(rightDockTabs, themeName);

            // Create Activity Log panel
            var activityLogViewModel = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ActivityLogViewModel>(serviceProvider);
            var controlFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<SyncfusionControlFactory>(serviceProvider);
            logger?.LogDebug("Creating ActivityLogPanel...");
            var activityLogPanel = new ActivityLogPanel(activityLogViewModel, controlFactory)
            {
                Dock = DockStyle.Fill,
                Name = "ActivityLogPanel"
            };
            logger?.LogDebug("ActivityLogPanel created successfully");

            var jarvisChatPanel = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<JARVISChatUserControl>(serviceProvider);
            jarvisChatPanel.Dock = DockStyle.Fill;
            jarvisChatPanel.Name = "JARVISDockPanel";

            var activityTab = new TabPageAdv
            {
                Name = "RightDockTab_ActivityLog",
                Text = "Activity Log"
            };
            activityTab.Controls.Add(activityLogPanel);

            var jarvisTab = new TabPageAdv
            {
                Name = "RightDockTab_JARVIS",
                Text = "JARVIS Chat"
            };
            jarvisTab.Controls.Add(jarvisChatPanel);

            rightDockTabs.TabPages.Add(activityTab);
            rightDockTabs.TabPages.Add(jarvisTab);
            rightDockTabs.SelectedTab = activityTab;

            rightDockPanel.Controls.Add(rightDockTabs);
            SfSkinManager.SetVisualStyle(rightDockPanel, themeName);

            sw.Stop();
            logger?.LogDebug(
                "RightDockPanelFactory: Right dock tabs initialized - Theme: {ThemeName}, Width={Width}",
                themeName, rightDockPanel.Width);
            logger?.LogInformation(
                "RightDockPanelFactory: Right panel created successfully in {ElapsedMs}ms (persistent Activity Log + JARVIS tabs)",
                sw.ElapsedMilliseconds);

            return (rightDockPanel, rightDockTabs, activityLogPanel, jarvisChatPanel);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to create right dock panel with persistent tabs (Activity Log + JARVIS Chat)");
            throw;
        }
    }
}
