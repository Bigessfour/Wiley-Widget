using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.Drawing;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Supporting;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Themes;
using LegacyGradientPanel = WileyWidget.WinForms.Controls.Base.LegacyGradientPanel;

namespace WileyWidget.WinForms.Forms;

/// <summary>
/// Factory for creating the JARVIS Chat panel as a separate, fixed (non-dockable) sidebar.
/// This prevents WebView2 HWND invalidation by disabling docking/reparenting.
/// Positioned as right-side sidebar (DockStyle.Right) with user-resizable width.
/// </summary>
public static class JarvisDockPanelFactory
{
    /// <summary>
    /// Create the JARVIS Chat panel as a fixed-position, non-dockable sidebar.
    /// </summary>
    /// <param name="mainForm">Parent MainForm instance</param>
    /// <param name="serviceProvider">Service provider for dependency resolution</param>
    /// <param name="logger">Logger instance</param>
    /// <returns>Tuple of (jarvisDockPanel, jarvisChatUserControl)</returns>
    /// <remarks>
    /// Panel is positioned on the right side with 380px width, resizable via built-in splitter.
    /// DockingManager docking/persistence is disabled to prevent parent HWND changes.
    /// JARVIS initializes synchronously in OnHandleCreated (no parent reparenting = stable HWND).
    /// Theme applied via SfSkinManager cascade from parent MainForm.
    /// </remarks>
    public static (
        LegacyGradientPanel jarvisDockPanel,
        JARVISChatUserControl jarvisChatUserControl
    ) CreateJarvisDockPanel(
        MainForm mainForm,
        IServiceProvider serviceProvider,
        ILogger? logger)
    {
        if (mainForm == null) throw new ArgumentNullException(nameof(mainForm));
        if (serviceProvider == null) throw new ArgumentNullException(nameof(serviceProvider));

        var sw = Stopwatch.StartNew();
        logger?.LogInformation("JarvisDockPanelFactory: Creating JARVIS Chat fixed panel (non-dockable sidebar)");

        WileyWidget.WinForms.Themes.ThemeColors.EnsureThemeAssemblyLoaded(logger);
        var themeName = SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;

        try
        {
            // Create container panel for JARVIS (right-side sidebar)
            var jarvisDockPanel = new LegacyGradientPanel
            {
                Dock = DockStyle.Right,
                Width = 380,  // Wide enough for chat bubbles in SfAIAssistView
                MinimumSize = new Size(300, 0),
                MaximumSize = new Size(800, 0),  // Allow resizing up to 800px
                BorderStyle = BorderStyle.None,
                BackgroundColor = new BrushInfo(Color.WhiteSmoke),
                Name = "JARVISDockPanel",
                AccessibleName = "JARVIS Chat Panel",
                AccessibleDescription = "JARVIS AI Assistant chat interface (fixed, non-dockable)"
            };

            // Create and initialize JARVISChatUserControl
            var scopeFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetRequiredService<IServiceScopeFactory>(serviceProvider);
            var jarvisLogger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                    .GetService<ILogger<JARVISChatUserControl>>(serviceProvider)
                ?? (Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                    .GetService<ILogger>(serviceProvider) as ILogger<JARVISChatUserControl>)
                ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<JARVISChatUserControl>.Instance;

            logger?.LogDebug("Creating JARVISChatUserControl for fixed panel...");
            var jarvisChatUserControl = new JARVISChatUserControl(scopeFactory, serviceProvider, jarvisLogger)
            {
                Dock = DockStyle.Fill,
                Name = "JARVISChatUserControl"
            };
            logger?.LogDebug("JARVISChatUserControl created successfully");

            // Add JARVIS control to panel
            jarvisDockPanel.Controls.Add(jarvisChatUserControl);

            // Apply theme to panel (cascades to children via SfSkinManager)
            SfSkinManager.SetVisualStyle(jarvisDockPanel, themeName);

            // Ensure theme cascade to child controls
            ApplyThemeRecursive(jarvisDockPanel, themeName);

            logger?.LogDebug("Theme applied to JARVIS panel: {ThemeName}", themeName);

            sw.Stop();
            logger?.LogInformation(
                "JarvisDockPanelFactory: JARVIS Chat panel created in {ElapsedMs}ms - Fixed sidebar configured, theme applied, ready for MainForm integration",
                sw.ElapsedMilliseconds);

            return (jarvisDockPanel, jarvisChatUserControl);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to create JARVIS Chat fixed panel");
            throw;
        }
    }

    /// <summary>
    /// Apply theme recursively to all child controls in the panel.
    /// Ensures consistent theming throughout the JARVIS panel hierarchy.
    /// </summary>
    private static void ApplyThemeRecursive(Control control, string themeName)
    {
        foreach (Control child in control.Controls)
        {
            try
            {
                SfSkinManager.SetVisualStyle(child, themeName);
                if (child.HasChildren)
                {
                    ApplyThemeRecursive(child, themeName);
                }
            }
            catch
            {
                // Skip controls that don't support theming
            }
        }
    }

    /// <summary>
    /// Configure JARVIS panel as fixed (non-dockable) with DockingManager.
    /// Call this after JARVIS panel is added to MainForm.
    /// </summary>
    /// <param name="dockingManager">DockingManager instance</param>
    /// <param name="jarvisDockPanel">JARVIS panel to configure</param>
    /// <param name="logger">Logger instance</param>
    public static void ConfigureJarvisPanelAsDockingDisabled(
        DockingManager dockingManager,
        LegacyGradientPanel jarvisDockPanel,
        ILogger? logger)
    {
        if (dockingManager == null) throw new ArgumentNullException(nameof(dockingManager));
        if (jarvisDockPanel == null) throw new ArgumentNullException(nameof(jarvisDockPanel));

        try
        {
            // Disable docking: prevents user from dragging/reparenting JARVIS panel
            dockingManager.SetEnableDocking(jarvisDockPanel, false);
            logger?.LogDebug("SetEnableDocking(false) applied to JARVIS panel");

            // Always visible: JARVIS is a fixed sidebar, no toggle visibility
            dockingManager.SetDockVisibility(jarvisDockPanel, true);
            logger?.LogDebug("SetDockVisibility(true) applied to JARVIS panel");

            logger?.LogInformation("JARVIS panel configured as fixed (non-dockable, always visible)");
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to configure JARVIS panel as docking-disabled");
            throw;
        }
    }
}
