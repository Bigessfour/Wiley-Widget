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
                panel.Dock = DockStyle.Fill;

                // If panel supports parameter initialization, pass the parameters
                if (parameters != null && panel is IParameterizedPanel parameterizedPanel)
                {
                    parameterizedPanel.InitializeWithParameters(parameters);
                }

                _dockingManager.SetDockLabel(panel, panelName);
                _dockingManager.SetAllowFloating(panel, allowFloating);

                // Dock the panel
                _dockingManager.DockControl(
                    panel,
                    _parentControl,
                    preferredStyle,
                    allowFloating ? 193 : 1); // DockVisibility values: 1=Docked, 193=AutoHideOrDockedOrFloating

                _dockingManager.SetDockVisibility(panel, true);
                _dockingManager.ActivateControl(panel);

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

                // Small pause to allow DockingManager to update UI (helps FlaUI detection)
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

        /// <summary>
        /// Hides a docked panel by name.
        /// </summary>
        /// <param name="panelName">Name of the panel to hide.</param>
        /// <returns>True if panel was hidden, false if panel doesn't exist.</returns>
        public bool HidePanel(string panelName)
        {
            if (string.IsNullOrWhiteSpace(panelName))
                throw new ArgumentException("Panel name cannot be empty.", nameof(panelName));

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
