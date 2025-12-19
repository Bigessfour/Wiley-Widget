using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms.Tools;

namespace WileyWidget.WinForms.Services
{
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
                var panel = Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance<TPanel>(_serviceProvider);
                panel.Name = panelName.Replace(" ", ""); // Clean name for internal use
                panel.Dock = DockStyle.Fill;

                // Optional: Set DataContext or inject dependencies via DI if panel uses ScopedPanelBase or constructor injection
                // (Handled automatically if panel uses constructor DI and is resolved via IServiceProvider — see advanced note below)

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

                // Cache for reuse
                _cachedPanels[panelName] = panel;

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
    }
}
