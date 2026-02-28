using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.Drawing;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.Business.Interfaces;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Panels;
using WileyWidget.WinForms.Factories;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Forms;

/// <summary>
/// Factory for creating and managing the right-docked panel container.
/// Hosts Activity Log while JARVIS is managed via DockingManager panel navigation.
/// Enforces SfSkinManager theme cascade on all docked panels.
/// </summary>
public static class RightDockPanelFactory
{

    /// <summary>
    /// Create the right-docked panel container with Activity Log tab.
    /// JARVIS Chat is shown through panel navigation inside DockingManager.
    /// </summary>
    /// <param name="mainForm">Parent MainForm instance</param>
    /// <param name="serviceProvider">Service provider for dependency resolution</param>
    /// <param name="logger">Logger instance</param>
    /// <returns>Tuple of (rightDockPanel, activityLogPanel)</returns>
    /// <remarks>
    /// The right panel is 350px wide with 320px minimum to accommodate Activity Log.
    /// Panel is created with scoped lifecycles; DI registration NOT needed.
    /// Theme cascade applied via SfSkinManager.
    /// </remarks>
    public static (
        Panel rightDockPanel,
        ActivityLogPanel activityLogPanel
    ) CreateRightDockPanel(
        MainForm mainForm,
        IServiceProvider serviceProvider,
        ILogger? logger)
    {
        if (mainForm == null) throw new ArgumentNullException(nameof(mainForm));
        if (serviceProvider == null) throw new ArgumentNullException(nameof(serviceProvider));

        var sw = Stopwatch.StartNew();
        logger?.LogInformation("RightDockPanelFactory: Creating right-docked panel with Activity Log");

        ThemeColors.EnsureThemeAssemblyLoaded(logger);
        var themeName = SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme;

        try
        {
            // Create container panel
            var rightDockPanel = new Panel
            {
                Dock = DockStyle.Right,
                Width = 350,
                MinimumSize = new Size(320, 0),
                BorderStyle = BorderStyle.None,
                Name = "RightDockPanel"
            };

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

            rightDockPanel.Controls.Add(activityLogPanel);

            sw.Stop();
            logger?.LogDebug(
                "RightDockPanelFactory: Activity Log panel initialized - Theme: {ThemeName}, Size: {Width}x{Height}",
                themeName, 350, 0);
            logger?.LogInformation(
                "RightDockPanelFactory: Right panel created successfully in {ElapsedMs}ms (Activity Log inline, no tabs)",
                sw.ElapsedMilliseconds);

            return (rightDockPanel, activityLogPanel);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to create right dock panel with TabControl (Activity Log + JARVIS Chat)");
            throw;
        }
    }


}
