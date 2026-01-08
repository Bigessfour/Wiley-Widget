using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Controls;

namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// Interface for panels that can be initialized with parameters.
    /// </summary>
    public interface IParameterizedPanel
    {
        /// <summary>
        /// Initialize the panel with the provided parameters.
        /// </summary>
        /// <param name="parameters">Parameters for panel initialization.</param>
        void InitializeWithParameters(object parameters);
    }

    /// <summary>
    /// Centralized service for managing docked panels in MainForm's DockingManager.
    /// Ensures single instance per panel type, reuse, activation, and proper naming.
    /// Replaces scattered menu click handlers and legacy form-based navigation.
    /// Panels are resolved via dependency injection to support constructor parameters.
    /// </summary>
    public interface IPanelNavigationService
    {
        /// <summary>
        /// Shows or activates a docked panel. Creates it if not already present.
        /// Panel is resolved from DI container to support constructor injection.
        /// </summary>
        /// <typeparam name="TPanel">The UserControl panel type.</typeparam>
        /// <param name="panelName">Unique display name (also used as DockingManager key).</param>
        /// <param name="preferredStyle">Preferred docking position.</param>
        /// <param name="allowFloating">If true, panel can be floated by user.</param>
        void ShowPanel<TPanel>(
            string panelName,
            DockingStyle preferredStyle = DockingStyle.Right,
            bool allowFloating = true)
            where TPanel : UserControl;

        /// <summary>
        /// Shows or activates a docked panel with initialization parameters. Creates it if not already present.
        /// Panel is resolved from DI container to support constructor injection.
        /// </summary>
        /// <typeparam name="TPanel">The UserControl panel type.</typeparam>
        /// <param name="panelName">Unique display name (also used as DockingManager key).</param>
        /// <param name="parameters">Parameters to pass to panel constructor or initialization.</param>
        /// <param name="preferredStyle">Preferred docking position.</param>
        /// <param name="allowFloating">If true, panel can be floated by user.</param>
        void ShowPanel<TPanel>(
            string panelName,
            object? parameters,
            DockingStyle preferredStyle = DockingStyle.Right,
            bool allowFloating = true)
            where TPanel : UserControl;

        /// <summary>
        /// Hides a docked panel by name.
        /// </summary>
        /// <param name="panelName">Name of the panel to hide.</param>
        /// <returns>True if panel was hidden, false if panel doesn't exist.</returns>
        bool HidePanel(string panelName);
    }

    public sealed class PanelNavigationService : IPanelNavigationService, IDisposable
    {
        private readonly ILogger<PanelNavigationService> _logger;
        private readonly DockingManager _dockingManager;
        private readonly Control _parentControl; // Usually MainForm or central document container
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<string, UserControl> _cachedPanels = new();

        public PanelNavigationService(
            DockingManager dockingManager,
            Control parentControl,
            IServiceProvider serviceProvider,
            ILogger<PanelNavigationService> logger)
        {
            _dockingManager = dockingManager ?? throw new ArgumentNullException(nameof(dockingManager));
            _parentControl = parentControl ?? throw new ArgumentNullException(nameof(parentControl));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger;

            _logger.LogDebug("PanelNavigationService initialized with DI support");
        }

        public void ShowPanel<TPanel>(
            string panelName,
            DockingStyle preferredStyle = DockingStyle.Right,
            bool allowFloating = true)
            where TPanel : UserControl
        {
            ShowPanel<TPanel>(panelName, null, preferredStyle, allowFloating);
        }

        public void ShowPanel<TPanel>(
            string panelName,
            object? parameters,
            DockingStyle preferredStyle = DockingStyle.Right,
            bool allowFloating = true)
            where TPanel : UserControl
        {
            if (string.IsNullOrWhiteSpace(panelName))
                throw new ArgumentException("Panel name cannot be empty.", nameof(panelName));

            if (_parentControl.InvokeRequired)
            {
                _parentControl.Invoke(new System.Action(() => ShowPanel<TPanel>(panelName, parameters, preferredStyle, allowFloating)));
                return;
            }

            try
            {
                // Reuse existing panel if already created
                if (_cachedPanels.TryGetValue(panelName, out var existingPanel))
                {
                    _dockingManager.SetDockVisibility(existingPanel, true);
                    _dockingManager.ActivateControl(existingPanel);
                    _logger.LogDebug("Activated existing panel: {PanelName}", panelName);
                    return;
                }

                // Create new instance via DI (supports constructor injection)
                Console.WriteLine($"[PanelNavigationService] Creating panel: {panelName} ({typeof(TPanel).Name})");
                var panel = Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance<TPanel>(_serviceProvider);
                panel.Name = panelName.Replace(" ", "", StringComparison.Ordinal); // Clean name for internal use

                // If panel supports parameter initialization, pass the parameters
                if (parameters != null && panel is IParameterizedPanel parameterizedPanel)
                {
                    parameterizedPanel.InitializeWithParameters(parameters);
                }

                // Enable docking features for the panel (required for headers and buttons to appear)
                _dockingManager.SetEnableDocking(panel, true);

                _dockingManager.SetDockLabel(panel, panelName);
                _dockingManager.SetAllowFloating(panel, allowFloating);

                // Ensure caption buttons are visible for docked panels
                _dockingManager.SetCloseButtonVisibility(panel, true);
                _dockingManager.SetAutoHideButtonVisibility(panel, true);
                _dockingManager.SetMenuButtonVisibility(panel, true);

                // Dock the panel
                // Determine a sensible initial size rather than using magic numbers.
                var effectiveStyle = preferredStyle;
                if (effectiveStyle == DockingStyle.Fill)
                {
                    _logger.LogWarning(
                        "DockingStyle.Fill is not supported when docking to the DockingManager host. Falling back to DockingStyle.Right for panel: {PanelName}",
                        panelName);
                    effectiveStyle = DockingStyle.Right;
                }

                int dockSize = CalculateDockSize(effectiveStyle, _parentControl);
                _dockingManager.DockControl(panel, _parentControl, effectiveStyle, dockSize);

                // CRITICAL FIX: For ChatPanel, apply full docking configuration to ensure proper visibility
                // and prevent auto-hide state that causes clipping issues
                if (typeof(TPanel).Name == "ChatPanel")
                {
                    try
                    {
                        _logger.LogDebug("Applying ChatPanel-specific docking configuration");
                        
                        // Force visible and active
                        _dockingManager.SetDockVisibility(panel, true);
                        _dockingManager.ActivateControl(panel);  // Brings to front, expands if tabbed

                        // Set size for ChatPanel using SetControlSize to maximize space
                        // Use 450 width for right/left docking, 600 height for bottom/top
                        int chatWidth = 450;
                        int chatHeight = 600;
                        _dockingManager.SetControlSize(panel, new Size(chatWidth, chatHeight));
                        _logger.LogDebug("ChatPanel: Control size set to {Width}x{Height}", chatWidth, chatHeight);

                        // Set minimum size to prevent collapsing below usable size (400 is ChatPanel minimum)
                        _dockingManager.SetControlMinimumSize(panel, new Size(400, 400));
                        _logger.LogDebug("ChatPanel: Minimum size set to 400x400");

                        _logger.LogInformation("ChatPanel docking configuration applied successfully");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "ChatPanel docking configuration failed - continuing with defaults");
                    }
                }

                // Propagate accessibility and header/dock caption so UI automation can find panels reliably
                try
                {
                    panel.AccessibleName = panelName;
                    panel.AccessibleDescription = $"Panel: {panelName}";
                    panel.Tag = panelName;

                    // If panel has a PanelHeader control, ensure its title matches and is discoverable
                    var header = panel.Controls.OfType<PanelHeader>().FirstOrDefault();
                    if (header != null)
                    {
                        header.Title = panelName; // PanelHeader.Title sets headerLabel.Name and AccessibleName
                        try { header.AccessibleName = panelName + " header"; } catch { }
                    }

                    // If Syncfusion DockHandler is present, set its Text and a stable Name if supported (via reflection)
                    var dh = panel.GetType().GetProperty("DockHandler")?.GetValue(panel);
                    if (dh != null)
                    {
                        try
                        {
                            var txtProp = dh.GetType().GetProperty("Text");
                            if (txtProp != null) txtProp.SetValue(dh, panelName);
                        }
                        catch { }

                        try
                        {
                            var nameProp = dh.GetType().GetProperty("Name");
                            if (nameProp != null && nameProp.CanWrite) nameProp.SetValue(dh, "DockHandler_" + panelName.Replace(" ", "", StringComparison.Ordinal));
                        }
                        catch { }

                        try
                        {
                            var aidProp = dh.GetType().GetProperty("AutomationId");
                            if (aidProp != null && aidProp.CanWrite) aidProp.SetValue(dh, "DockHandler_" + panelName.Replace(" ", "", StringComparison.Ordinal));
                        }
                        catch { }
                    }
                }
                catch { }

                // Small pause to allow DockingManager to settle after ChatPanel-specific configuration
                try { System.Threading.Thread.Sleep(250); } catch { }

                // Cache for reuse
                _cachedPanels[panelName] = panel;

                Console.WriteLine($"[PanelNavigationService] Docked and activated panel: {panelName} ({typeof(TPanel).Name})");
                _logger.LogInformation("Docked and activated new panel: {PanelName} ({PanelType})", panelName, typeof(TPanel).Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show panel: {PanelName}", panelName);
                MessageBox.Show(
                    $"Unable to open {panelName}. See log for details.",
                    "Navigation Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        public void Dispose()
        {
            _cachedPanels.Clear();
        }

        private static int CalculateDockSize(DockingStyle style, Control container)
        {
            if (container == null) return 300;
            // Use sensible defaults relative to available container size.
            switch (style)
            {
                case DockingStyle.Left:
                case DockingStyle.Right:
                    return Math.Max(300, Math.Max(100, container.Width / 4));
                case DockingStyle.Top:
                case DockingStyle.Bottom:
                    return Math.Max(200, Math.Max(80, container.Height / 4));
                case DockingStyle.Tabbed:
                case DockingStyle.Fill:
                default:
                    return Math.Max(400, Math.Min(container.Width, container.Height) / 2);
            }
        }

        /// <summary>
        /// Hides a docked panel by name.
        /// </summary>
        /// <param name="panelName">Name of the panel to hide.</param>
        /// <returns>True if panel was hidden, false if panel doesn't exist.</returns>
        public bool HidePanel(string panelName)
        {
            if (string.IsNullOrWhiteSpace(panelName))
                throw new ArgumentException("Panel name cannot be empty.", nameof(panelName));

            if (_parentControl.InvokeRequired)
            {
                _parentControl.Invoke(new System.Action(() => HidePanel(panelName)));
                return false;
            }

            if (_cachedPanels.TryGetValue(panelName, out var existingPanel))
            {
                _dockingManager.SetDockVisibility(existingPanel, false);
                _logger.LogDebug("Hidden panel: {PanelName}", panelName);
                return true;
            }

            _logger.LogWarning("Cannot hide panel '{PanelName}' - not found", panelName);
            return false;
        }
    }
}
