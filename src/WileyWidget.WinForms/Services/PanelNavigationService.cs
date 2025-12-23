using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Forms;

namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// Interface for panels that can be initialized with parameters.
    /// </summary>
    /// <summary>
    /// Represents a interface for iparameterizedpanel.
    /// </summary>
    /// <summary>
    /// Represents a interface for iparameterizedpanel.
    /// </summary>
    /// <summary>
    /// Represents a interface for iparameterizedpanel.
    /// </summary>
    /// <summary>
    /// Represents a interface for iparameterizedpanel.
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
    /// <summary>
    /// Represents a interface for ipanelnavigationservice.
    /// </summary>
    /// <summary>
    /// Represents a interface for ipanelnavigationservice.
    /// </summary>
    /// <summary>
    /// Represents a interface for ipanelnavigationservice.
    /// </summary>
    /// <summary>
    /// Represents a interface for ipanelnavigationservice.
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
        /// Optional explicit initializer to attach the `MainForm` instance after it is created.
        /// Call this from `MainForm` after docking initialization to avoid DI-time circular references.
        /// </summary>
        /// <param name="mainForm">The application's `MainForm` instance.</param>
        void Initialize(IMainFormDockingProvider mainForm);

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
        /// <summary>
        /// Represents the _serviceprovider.
        /// </summary>
        /// <summary>
        /// Represents the _serviceprovider.
        /// </summary>
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<string, UserControl> _cachedPanels = new();
        // Keep a per-panel IServiceScope so scoped dependencies of panels remain alive
        private readonly Dictionary<string, IServiceScope> _panelScopes = new();
        private readonly object _sync = new();
        private IMainFormDockingProvider? _mainForm;

        public PanelNavigationService(
            IServiceProvider serviceProvider,
            ILogger<PanelNavigationService> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _logger.LogDebug("PanelNavigationService initialized (MainForm resolved lazily)");
        }

        /// <summary>
        /// Explicitly attach the MainForm instance after it is created.
        /// This avoids resolving MainForm from the DI container during startup.
        /// </summary>
        /// <param name="mainForm">The application's MainForm instance.</param>
        /// <summary>
        /// Performs initialize. Parameters: mainForm.
        /// </summary>
        /// <param name="mainForm">The mainForm.</param>
        /// <summary>
        /// Performs initialize. Parameters: mainForm.
        /// </summary>
        /// <param name="mainForm">The mainForm.</param>
        /// <summary>
        /// Performs initialize. Parameters: mainForm.
        /// </summary>
        /// <param name="mainForm">The mainForm.</param>
        /// <summary>
        /// Performs initialize. Parameters: mainForm.
        /// </summary>
        /// <param name="mainForm">The mainForm.</param>
        public void Initialize(IMainFormDockingProvider mainForm)
        {
            _mainForm = mainForm ?? throw new ArgumentNullException(nameof(mainForm));
            _logger.LogDebug("PanelNavigationService attached to MainForm instance");
        }

        private IMainFormDockingProvider MainForm => _mainForm ?? (IMainFormDockingProvider)Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IMainFormDockingProvider>(_serviceProvider);

        private DockingManager? DockingManager
        {
            get
            {
                try
                {
                    return MainForm.GetDockingManager();
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogDebug(ex, "DockingManager not available yet - deferring docking operations.");
                    return null;
                }
            }
        }

        private Control? ParentControl
        {
            get
            {
                try
                {
                    return MainForm.GetCentralDocumentPanel();
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogDebug(ex, "Central document panel not available yet - deferring docking operations.");
                    return null;
                }
            }
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
                    var dm = DockingManager;
                    if (dm != null)
                    {
                        try
                        {
                            dm.SetDockVisibility(existingPanel, true);
                            dm.ActivateControl(existingPanel);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error activating existing panel: {PanelName}", panelName);
                        }

                        _logger.LogDebug("Activated existing panel: {PanelName}", panelName);
                    }
                    else
                    {
                        _logger.LogDebug("DockingManager unavailable; activated cached panel in-memory: {PanelName}", panelName);
                    }
                    return;
                }

                // Create new instance via DI (supports constructor injection)
                IServiceScope? panelScope = null;
                try
                {
                    panelScope = _serviceProvider.CreateScope();
                    // Resolve from DI when possible (preserves registered singletons/scoped deps), otherwise create an instance
                    var panel = Microsoft.Extensions.DependencyInjection.ActivatorUtilities.GetServiceOrCreateInstance<TPanel>(panelScope.ServiceProvider);
                    panel.Name = panelName.Replace(" ", "", StringComparison.Ordinal); // Clean name for internal use
                    panel.Dock = DockStyle.Fill;

                    // If panel supports parameter initialization, pass the parameters
                    if (parameters != null && panel is IParameterizedPanel parameterizedPanel)
                    {
                        parameterizedPanel.InitializeWithParameters(parameters);
                    }

                    // Cache for reuse and keep scope alive for panel lifetime BEFORE docking.
                    // This ensures unit tests can find the panel even if DockingManager operations aren't available/mocked.
                    lock (_sync)
                    {
                        _cachedPanels[panelName] = panel;
                        _panelScopes[panelName] = panelScope;
                        panelScope = null; // ownership transferred
                    }

                    try
                    {
                        var dm = DockingManager;
                        var parent = ParentControl;
                        if (dm != null && parent != null)
                        {
                            dm.SetDockLabel(panel, panelName);
                            dm.SetAllowFloating(panel, allowFloating);

                            // Dock the panel
                            dm.DockControl(
                                panel,
                                parent,
                                preferredStyle,
                                allowFloating ? 193 : 1); // DockVisibility values: 1=Docked, 193=AutoHideOrDockedOrFloating

                            dm.SetDockVisibility(panel, true);
                            dm.ActivateControl(panel);

                            _logger.LogInformation("Docked and activated new panel: {PanelName} ({PanelType})", panelName, typeof(TPanel).Name);
                        }
                        else
                        {
                            _logger.LogDebug("DockingManager or ParentControl unavailable; cached panel '{PanelName}' will be available in-memory", panelName);
                        }
                    }
                    catch (Exception exDock)
                    {
                        // Log and continue with cached panel to allow tests and non-UI environments to function
                        _logger.LogError(exDock, "Docking failed for panel '{PanelName}', panel remains cached but may not be visible.", panelName);
                    }
                }
                finally
                {
                    // If creation failed, dispose the scope
                    panelScope?.Dispose();
                }
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
        /// <summary>
        /// Performs dispose.
        /// </summary>
        /// <summary>
        /// Performs dispose.
        /// </summary>
        /// <summary>
        /// Performs dispose.
        /// </summary>
        /// <summary>
        /// Performs dispose.
        /// </summary>
        /// <summary>
        /// Performs dispose.
        /// </summary>

        public void Dispose()
        {
            lock (_sync)
            {
                // Dispose panels safely
                foreach (var panel in _cachedPanels.Values)
                {
                    try
                    {
                        panel.SafeDispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error disposing panel during PanelNavigationService.Dispose");
                    }
                }

                _cachedPanels.Clear();

                // Dispose associated scopes
                foreach (var scope in _panelScopes.Values)
                {
                    try
                    {
                        scope.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error disposing panel scope during PanelNavigationService.Dispose");
                    }
                }

                _panelScopes.Clear();
            }
        }

        /// <summary>
        /// Hides a docked panel by name.
        /// </summary>
        /// <param name="panelName">Name of the panel to hide.</param>
        /// <returns>True if panel was hidden, false if panel doesn't exist.</returns>
        /// <summary>
        /// Performs hidepanel. Parameters: panelName.
        /// </summary>
        /// <param name="panelName">The panelName.</param>
        /// <summary>
        /// Performs hidepanel. Parameters: panelName.
        /// </summary>
        /// <param name="panelName">The panelName.</param>
        /// <summary>
        /// Performs hidepanel. Parameters: panelName.
        /// </summary>
        /// <param name="panelName">The panelName.</param>
        /// <summary>
        /// Performs hidepanel. Parameters: panelName.
        /// </summary>
        /// <param name="panelName">The panelName.</param>
        public bool HidePanel(string panelName)
        {
            if (string.IsNullOrWhiteSpace(panelName))
                throw new ArgumentException("Panel name cannot be empty.", nameof(panelName));

            if (_cachedPanels.TryGetValue(panelName, out var existingPanel))
            {
                try
                {
                    var dm = DockingManager;
                    if (dm != null)
                    {
                        dm.SetDockVisibility(existingPanel, false);
                    }
                    else
                    {
                        _logger.LogDebug("DockingManager unavailable - hide operation skipped for panel '{PanelName}'", panelName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error while hiding panel '{PanelName}' - proceeding", panelName);
                }

                _logger.LogDebug("Hidden panel: {PanelName}", panelName);
                return true;
            }

            _logger.LogWarning("Cannot hide panel '{PanelName}' - not found", panelName);
            return false;
        }
    }
}
