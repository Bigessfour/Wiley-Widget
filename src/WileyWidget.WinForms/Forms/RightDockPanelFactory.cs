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
    public const string ActivityLogTabName = "RightDockTab_ActivityLog";
    public const string JarvisTabName = "RightDockTab_JARVIS";
    public const int ActivityLogPreferredWidth = 420;
    public const int JarvisPreferredWidth = 500;

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
        TabPageAdv jarvisTab
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
            // Padding(left, top, right, bottom): 8 px right inset prevents the tab content
            // from touching the window chrome, giving visual breathing room on the right edge.
            var rightDockPanel = new Panel
            {
                Dock = DockStyle.Right,
                Width = ActivityLogPreferredWidth,
                MinimumSize = new Size(350, 0),
                BorderStyle = BorderStyle.None,
                Padding = new Padding(0, 0, 8, 0),
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

            var activityTab = new TabPageAdv
            {
                Name = ActivityLogTabName,
                Text = "Activity Log"
            };
            activityTab.Tag = ActivityLogPreferredWidth;
            activityTab.Controls.Add(activityLogPanel);

            var jarvisTab = new TabPageAdv
            {
                Name = JarvisTabName,
                Text = "JARVIS Chat"
            };
            jarvisTab.Tag = JarvisPreferredWidth;
            var jarvisPlaceholder = new Panel
            {
                Name = "RightDockJarvisPlaceholder",
                Dock = DockStyle.Fill,
                Padding = new Padding(12)
            };
            var jarvisPlaceholderLabel = new Label
            {
                Name = "RightDockJarvisPlaceholderLabel",
                Dock = DockStyle.Fill,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "JARVIS loads on first open."
            };
            jarvisPlaceholder.Controls.Add(jarvisPlaceholderLabel);
            jarvisTab.Controls.Add(jarvisPlaceholder);

            rightDockTabs.TabPages.Add(activityTab);
            rightDockTabs.TabPages.Add(jarvisTab);
            rightDockTabs.SelectedTab = activityTab; // Keep startup responsive; JARVIS opens on explicit navigation

            rightDockPanel.Controls.Add(rightDockTabs);
            SfSkinManager.SetVisualStyle(rightDockPanel, themeName);

            sw.Stop();
            logger?.LogDebug(
                "RightDockPanelFactory: Right dock tabs initialized - Theme: {ThemeName}, Width={Width}",
                themeName, rightDockPanel.Width);
            logger?.LogInformation(
                "RightDockPanelFactory: Right panel created successfully in {ElapsedMs}ms (persistent Activity Log + JARVIS tabs)",
                sw.ElapsedMilliseconds);

            return (rightDockPanel, rightDockTabs, activityLogPanel, jarvisTab);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to create right dock panel with persistent tabs (Activity Log + JARVIS Chat)");
            throw;
        }
    }
}
