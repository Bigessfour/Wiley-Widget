using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.Windows.Forms;
using Syncfusion.WinForms.Themes;
using Syncfusion.WinForms.Controls;
using WileyWidget.Abstractions;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Controls.Analytics;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Panels;
using WileyWidget.WinForms.Controls.Supporting;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Themes;
using AppThemeColors = WileyWidget.WinForms.Themes.ThemeColors;

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
        /// Shows or activates a docked Form by hosting it inside a `FormHostPanel`.
        /// This preserves docking/floating behavior while allowing form-based UI to be used.
        /// </summary>
        /// <typeparam name="TForm">The Form type to host.</typeparam>
        /// <param name="panelName">Unique display name for the hosted form.</param>
        /// <param name="preferredStyle">Preferred docking position.</param>
        /// <param name="allowFloating">If true, the hosted form can be floated.</param>
        void ShowForm<TForm>(
            string panelName,
            DockingStyle preferredStyle = DockingStyle.Right,
            bool allowFloating = true)
            where TForm : Form;

        /// <summary>
        /// Shows or activates a docked Form with initialization parameters.
        /// </summary>
        void ShowForm<TForm>(
            string panelName,
            object? parameters,
            DockingStyle preferredStyle = DockingStyle.Right,
            bool allowFloating = true)
            where TForm : Form;

        /// <summary>
        /// Hides a docked panel by name.
        /// </summary>
        /// <param name="panelName">Name of the panel to hide.</param>
        /// <returns>True if panel was hidden, false if panel doesn't exist.</returns>
        bool HidePanel(string panelName);

        /// <summary>
        /// Adds an existing panel instance to the docking manager asynchronously.
        /// </summary>
        /// <param name="panel">The panel instance to add.</param>
        /// <param name="panelName">The name/title of the panel.</param>
        /// <param name="preferredStyle">The preferred docking style.</param>
        /// <param name="allowFloating">Whether the panel can be floated.</param>
        /// <returns>A task that completes when the panel is added.</returns>
        Task AddPanelAsync(UserControl panel, string panelName, DockingStyle preferredStyle = DockingStyle.Right, bool allowFloating = true);

        /// <summary>
        /// Get the currently active panel name for ribbon button state tracking.
        /// </summary>
        /// <returns>The name of the currently active panel, or null if no panel is active.</returns>
        string? GetActivePanelName();

        /// <summary>
        /// Event raised when a panel is activated, for ribbon button highlighting.
        /// </summary>
        event EventHandler<PanelActivatedEventArgs>? PanelActivated;
    }

    public sealed class PanelNavigationService : IPanelNavigationService, IDisposable
    {
        /// <summary>
        /// C# 14: Logger property for cleaner access.
        /// </summary>
        private readonly ILogger<PanelNavigationService> Logger;

        private readonly DockingManager _dockingManager;
        private readonly Control _parentControl; // Usually MainForm or central document container
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<string, UserControl> _cachedPanels = new();
        private readonly UI.Helpers.PanelAnimationHelper _animationHelper;

        // Map panels to their DockStateChanged handlers so we can unsubscribe cleanly when panels are disposed/removed
        private readonly System.Collections.Concurrent.ConcurrentDictionary<UserControl, Syncfusion.Windows.Forms.Tools.DockStateChangeEventHandler> _dockEventHandlers = new();

        /// <summary>
        /// Tracks the currently active panel name for ribbon button highlighting.
        /// </summary>
        private string? _activePanelName;

        /// <summary>
        /// Event raised when the active panel changes, for ribbon button state updates.
        /// </summary>
        public event EventHandler<PanelActivatedEventArgs>? PanelActivated;

        private static readonly Dictionary<Type, PanelSizing> PanelSizeOverrides = new()
        {
            // FormHostPanel hosts Forms such as BudgetDashboardForm; provide dashboard sizing when hosting forms
            { typeof(WileyWidget.WinForms.Controls.Panels.FormHostPanel), new PanelSizing(new Size(560, 0), new Size(0, 420), new Size(450, 420)) },
            { typeof(AccountsPanel), new PanelSizing(new Size(620, 0), new Size(0, 380), new Size(520, 420)) },
            { typeof(AnalyticsHubPanel), new PanelSizing(new Size(600, 0), new Size(0, 500), new Size(500, 450)) },
            { typeof(AuditLogPanel), new PanelSizing(new Size(520, 0), new Size(0, 380), new Size(440, 320)) },
            { typeof(WarRoomPanel), new PanelSizing(new Size(560, 0), new Size(0, 420), new Size(460, 380)) },
            { typeof(QuickBooksPanel), new PanelSizing(new Size(620, 0), new Size(0, 400), new Size(540, 360)) },
            { typeof(DepartmentSummaryPanel), new PanelSizing(new Size(540, 0), new Size(0, 400), new Size(440, 360)) },
            { typeof(SettingsPanel), new PanelSizing(new Size(500, 0), new Size(0, 360), new Size(420, 320)) },
            { typeof(ProactiveInsightsPanel), new PanelSizing(new Size(560, 0), new Size(0, 400), new Size(460, 360)) },
            { typeof(UtilityBillPanel), new PanelSizing(new Size(560, 0), new Size(0, 400), new Size(460, 360)) },
            { typeof(CustomersPanel), new PanelSizing(new Size(560, 0), new Size(0, 400), new Size(460, 360)) },
        };

        /// <summary>
        /// Initializes the panel navigation service with docking infrastructure and dependency injection support.
        /// DockingManager must be non-null and properly initialized before construction.
        /// </summary>
        /// <param name="dockingManager">Initialized DockingManager instance (required, non-null).</param>
        /// <param name="parentControl">Parent control hosting the docked panels.</param>
        /// <param name="serviceProvider">Service provider for DI resolution of panel types.</param>
        /// <param name="logger">Logger instance.</param>
        /// <exception cref="ArgumentNullException">Thrown if any required parameter is null.</exception>
        public PanelNavigationService(
            DockingManager dockingManager,
            Control parentControl,
            IServiceProvider serviceProvider,
            ILogger<PanelNavigationService> logger)
        {
            _dockingManager = dockingManager ?? throw new ArgumentNullException(nameof(dockingManager), "DockingManager must be initialized before PanelNavigationService construction.");
            // No global subscription here; per-panel subscriptions are added when the panel is docked. (Keep handler registration localized.)
            _parentControl = parentControl ?? throw new ArgumentNullException(nameof(parentControl));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _animationHelper = new UI.Helpers.PanelAnimationHelper(logger);

            Logger.LogDebug("PanelNavigationService initialized with non-null DockingManager");
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
            // Validation: Panel name
            if (string.IsNullOrWhiteSpace(panelName))
                throw new ArgumentException("Panel name cannot be empty.", nameof(panelName));

            // Validation: Critical dependencies
            if (_dockingManager == null)
            {
                var ex = new InvalidOperationException("DockingManager is null - cannot show panel without docking infrastructure");
                Logger.LogError(ex, "[PANEL-CRITICAL] Cannot show panel {PanelName} - DockingManager is null", panelName);
                throw ex;
            }

            if (_parentControl == null)
            {
                var ex = new InvalidOperationException("Parent control is null - cannot show panel without parent container");
                Logger.LogError(ex, "[PANEL-CRITICAL] Cannot show panel {PanelName} - Parent control is null", panelName);
                throw ex;
            }

            if (_serviceProvider == null)
            {
                var ex = new InvalidOperationException("Service provider is null - cannot resolve panel via DI");
                Logger.LogError(ex, "[PANEL-CRITICAL] Cannot show panel {PanelName} - Service provider is null", panelName);
                throw ex;
            }

            // Validation: Parent control state
            if (_parentControl.IsDisposed)
            {
                var ex = new ObjectDisposedException(nameof(_parentControl), "Parent control has been disposed - cannot show panel");
                Logger.LogError(ex, "[PANEL-CRITICAL] Cannot show panel {PanelName} - Parent control disposed", panelName);
                throw ex;
            }

            Logger.LogDebug("[PANEL-SHOW] ShowPanel<{Type}> called for '{PanelName}' - Thread={Thread}, InvokeRequired={InvokeReq}, HandleCreated={HasHandle}",
                typeof(TPanel).Name, panelName, System.Threading.Thread.CurrentThread.ManagedThreadId,
                _parentControl.InvokeRequired, _parentControl.IsHandleCreated);

            // If called from a non-UI thread or before the handle exists, marshal the call to the UI thread.
            if (_parentControl.InvokeRequired || !_parentControl.IsHandleCreated)
            {
                Logger.LogDebug("[PANEL-SHOW] Marshalling required for {PanelName} - InvokeRequired={InvokeReq}, HandleCreated={HasHandle}",
                    panelName, _parentControl.InvokeRequired, _parentControl.IsHandleCreated);

                try
                {
                    // Use BeginInvoke only when parent control is valid and not disposed
                    if (!_parentControl.IsDisposed)
                    {
                        if (_parentControl.IsHandleCreated)
                        {
                            Logger.LogDebug("[PANEL-SHOW] Using BeginInvoke to marshal {PanelName} to UI thread", panelName);
                            _parentControl.BeginInvoke(new System.Action(() =>
                            {
                                try
                                {
                                    ShowPanel<TPanel>(panelName, parameters, preferredStyle, allowFloating);
                                }
                                catch (Exception invokeEx)
                                {
                                    Logger.LogError(invokeEx, "[PANEL-SHOW] Exception in BeginInvoke callback for {PanelName}", panelName);
                                    throw;
                                }
                            }));
                            return;
                        }

                        // If no handle yet, defer until handle creation to avoid cross-thread access.
                        Logger.LogInformation("[PANEL-SHOW] Deferring {PanelName} until parent handle is created", panelName);
                        EventHandler? handleCreatedHandler = null;
                        var handlerRegistered = false;

                        handleCreatedHandler = (s, e) =>
                        {
                            if (!handlerRegistered) return; // Prevent duplicate execution

                            Logger.LogDebug("[PANEL-SHOW] HandleCreated fired for deferred panel {PanelName}", panelName);
                            try
                            {
                                _parentControl.HandleCreated -= handleCreatedHandler;
                                handlerRegistered = false;
                            }
                            catch (Exception unsubEx)
                            {
                                Logger.LogDebug(unsubEx, "[PANEL-SHOW] Failed to unsubscribe HandleCreated handler (non-critical)");
                            }

                            try
                            {
                                if (_parentControl.IsDisposed)
                                {
                                    Logger.LogWarning("[PANEL-SHOW] Parent control disposed before deferred ShowPanel for {PanelName}", panelName);
                                    return;
                                }

                                ShowPanel<TPanel>(panelName, parameters, preferredStyle, allowFloating);
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError(ex, "[PANEL-SHOW] Deferred ShowPanel failed after handle creation for panel {PanelName}", panelName);
                            }
                        };

                        _parentControl.HandleCreated += handleCreatedHandler;
                        handlerRegistered = true;
                        Logger.LogDebug("[PANEL-SHOW] HandleCreated handler registered for {PanelName}", panelName);
                        return;
                    }
                    else
                    {
                        Logger.LogWarning("[PANEL-SHOW] Parent control disposed during marshalling attempt for {PanelName}", panelName);
                    }
                }
                catch (Exception ex)
                {
                    // Log and continue on current thread if marshalling fails for any reason
                    Logger.LogWarning(ex, "[PANEL-SHOW] Failed to marshal ShowPanel to UI thread for panel {PanelName}, attempting direct execution", panelName);
                }
            }

            try
            {
                Logger.LogInformation("[PANEL] Showing {PanelName} - type: {Type}, existing: {Exists}",
                    panelName, typeof(TPanel).Name, _cachedPanels.ContainsKey(panelName));

                // Reuse existing panel if already created
                if (_cachedPanels.TryGetValue(panelName, out var existingPanel))
                {
                    Logger.LogDebug("[PANEL] Reusing existing panel instance for {PanelName}", panelName);

                    // Validate existing panel state
                    if (existingPanel == null)
                    {
                        Logger.LogWarning("[PANEL] Cached panel for {PanelName} is null, removing from cache and creating new", panelName);
                        _cachedPanels.Remove(panelName);
                    }
                    else if (existingPanel.IsDisposed)
                    {
                        Logger.LogWarning("[PANEL] Cached panel for {PanelName} is disposed, removing from cache and creating new", panelName);
                        _cachedPanels.Remove(panelName);
                    }
                    else
                    {
                        try
                        {
                            ActivateExistingPanel(existingPanel, panelName, allowFloating);
                            Logger.LogInformation("[PANEL] ✅ Successfully activated existing panel {PanelName}", panelName);
                            return;
                        }
                        catch (Exception activateEx)
                        {
                            Logger.LogError(activateEx, "[PANEL] Failed to activate existing panel {PanelName}, will recreate", panelName);
                            _cachedPanels.Remove(panelName);
                            // Continue to create new panel
                        }
                    }
                }

                // Create new instance via DI (supports constructor injection)
                Logger.LogDebug("[PANEL] Creating new panel instance: {PanelName} ({PanelType})", panelName, typeof(TPanel).Name);

                UserControl? panel = null;
                try
                {
                    panel = Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance<TPanel>(_serviceProvider);

                    Logger.LogDebug("[PANEL] Panel instance created successfully: {PanelName}, Type={Type}, IsDisposed={IsDisposed}",
                        panelName, panel.GetType().FullName, panel.IsDisposed);
                }
                catch (Exception createEx)
                {
                    Logger.LogError(createEx, "[PANEL] Failed to create panel instance via DI for {PanelName} ({PanelType})",
                        panelName, typeof(TPanel).Name);
                    throw new InvalidOperationException(
                        $"Failed to create panel '{panelName}' of type {typeof(TPanel).Name}. " +
                        $"Check that all constructor dependencies are registered in DI container.", createEx);
                }

                // Initialize with parameters if provided
                if (parameters is not null)
                {
                    if (panel is IParameterizedPanel parameterizedPanel)
                    {
                        try
                        {
                            Logger.LogDebug("[PANEL] Initializing {PanelName} with parameters (type: {ParamType})",
                                panelName, parameters.GetType().Name);
                            parameterizedPanel.InitializeWithParameters(parameters);
                            Logger.LogDebug("[PANEL] Parameter initialization completed for {PanelName}", panelName);
                        }
                        catch (Exception paramEx)
                        {
                            Logger.LogError(paramEx, "[PANEL] Parameter initialization failed for {PanelName}", panelName);
                            throw new InvalidOperationException(
                                $"Failed to initialize panel '{panelName}' with provided parameters.", paramEx);
                        }
                    }
                    else
                    {
                        Logger.LogWarning("[PANEL] Parameters provided for {PanelName} but panel does not implement IParameterizedPanel", panelName);
                    }
                }

                // Dock the panel
                Logger.LogDebug("[PANEL] Calling DockPanelInternal for {PanelName}", panelName);
                DockPanelInternal(panel, panelName, preferredStyle, allowFloating);
                Logger.LogInformation("[PANEL] ✅ Successfully created and docked new panel {PanelName}", panelName);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[PANEL] ❌ Failed to show panel: {PanelName} - {ErrorType}: {ErrorMessage}",
                    panelName, ex.GetType().Name, ex.Message);
                throw new InvalidOperationException(
                    $"Unable to show panel '{panelName}' of type {typeof(TPanel).Name}. " +
                    $"Error: {ex.Message}. Check logs for detailed stack trace.", ex);
            }
        }

        public void ShowForm<TForm>(
            string panelName,
            DockingStyle preferredStyle = DockingStyle.Right,
            bool allowFloating = true)
            where TForm : Form
        {
            ShowForm<TForm>(panelName, null, preferredStyle, allowFloating);
        }

        public void ShowForm<TForm>(
            string panelName,
            object? parameters,
            DockingStyle preferredStyle = DockingStyle.Right,
            bool allowFloating = true)
            where TForm : Form
        {
            if (string.IsNullOrWhiteSpace(panelName))
                throw new ArgumentException("Panel name cannot be empty.", nameof(panelName));

            if (_dockingManager == null)
            {
                var ex = new InvalidOperationException("DockingManager is null - cannot show form without docking infrastructure");
                Logger.LogError(ex, "[PANEL-CRITICAL] Cannot show form {PanelName} - DockingManager is null", panelName);
                throw ex;
            }

            if (_parentControl == null)
            {
                var ex = new InvalidOperationException("Parent control is null - cannot show form without parent container");
                Logger.LogError(ex, "[PANEL-CRITICAL] Cannot show form {PanelName} - Parent control is null", panelName);
                throw ex;
            }

            if (_serviceProvider == null)
            {
                var ex = new InvalidOperationException("Service provider is null - cannot resolve form via DI");
                Logger.LogError(ex, "[PANEL-CRITICAL] Cannot show form {PanelName} - Service provider is null", panelName);
                throw ex;
            }

            if (_parentControl.IsDisposed)
            {
                var ex = new ObjectDisposedException(nameof(_parentControl), "Parent control has been disposed - cannot show form");
                Logger.LogError(ex, "[PANEL-CRITICAL] Cannot show form {PanelName} - Parent control disposed", panelName);
                throw ex;
            }

            Logger.LogDebug("[PANEL-SHOW] ShowForm<{Type}> called for '{PanelName}' - Thread={Thread}, InvokeRequired={InvokeReq}, HandleCreated={HasHandle}",
                typeof(TForm).Name, panelName, System.Threading.Thread.CurrentThread.ManagedThreadId,
                _parentControl.InvokeRequired, _parentControl.IsHandleCreated);

            if (_parentControl.InvokeRequired || !_parentControl.IsHandleCreated)
            {
                Logger.LogDebug("[PANEL-SHOW] Marshalling required for {PanelName} - InvokeRequired={InvokeReq}, HandleCreated={HasHandle}",
                    panelName, _parentControl.InvokeRequired, _parentControl.IsHandleCreated);

                try
                {
                    if (!_parentControl.IsDisposed)
                    {
                        if (_parentControl.IsHandleCreated)
                        {
                            Logger.LogDebug("[PANEL-SHOW] Using BeginInvoke to marshal {PanelName} to UI thread", panelName);
                            _parentControl.BeginInvoke(new System.Action(() =>
                            {
                                try
                                {
                                    ShowForm<TForm>(panelName, parameters, preferredStyle, allowFloating);
                                }
                                catch (Exception invokeEx)
                                {
                                    Logger.LogError(invokeEx, "[PANEL-SHOW] Exception in BeginInvoke callback for {PanelName}", panelName);
                                    throw;
                                }
                            }));
                            return;
                        }

                        Logger.LogInformation("[PANEL-SHOW] Deferring {PanelName} until parent handle is created", panelName);
                        EventHandler? handleCreatedHandler = null;
                        var handlerRegistered = false;

                        handleCreatedHandler = (s, e) =>
                        {
                            if (!handlerRegistered) return;

                            Logger.LogDebug("[PANEL-SHOW] HandleCreated fired for deferred form {PanelName}", panelName);
                            try
                            {
                                _parentControl.HandleCreated -= handleCreatedHandler;
                                handlerRegistered = false;
                            }
                            catch (Exception unsubEx)
                            {
                                Logger.LogDebug(unsubEx, "[PANEL-SHOW] Failed to unsubscribe HandleCreated handler (non-critical)");
                            }

                            try
                            {
                                if (_parentControl.IsDisposed)
                                {
                                    Logger.LogWarning("[PANEL-SHOW] Parent control disposed before deferred ShowForm for {PanelName}", panelName);
                                    return;
                                }

                                ShowForm<TForm>(panelName, parameters, preferredStyle, allowFloating);
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError(ex, "[PANEL-SHOW] Deferred ShowForm failed after handle creation for form {PanelName}", panelName);
                            }
                        };

                        _parentControl.HandleCreated += handleCreatedHandler;
                        handlerRegistered = true;
                        Logger.LogDebug("[PANEL-SHOW] HandleCreated handler registered for {PanelName}", panelName);
                        return;
                    }
                    else
                    {
                        Logger.LogWarning("[PANEL-SHOW] Parent control disposed during marshalling attempt for {PanelName}", panelName);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "[PANEL-SHOW] Failed to marshal ShowForm to UI thread for form {PanelName}, attempting direct execution", panelName);
                }
            }

            try
            {
                Logger.LogInformation("[PANEL] Showing form {PanelName} - type: {Type}, existing: {Exists}",
                    panelName, typeof(TForm).Name, _cachedPanels.ContainsKey(panelName));

                // Reuse existing panel if already created
                if (_cachedPanels.TryGetValue(panelName, out var existingPanel))
                {
                    Logger.LogDebug("[PANEL] Reusing existing hosted panel instance for {PanelName}", panelName);

                    if (existingPanel == null)
                    {
                        Logger.LogWarning("[PANEL] Cached panel for {PanelName} is null, removing from cache and creating new", panelName);
                        _cachedPanels.Remove(panelName);
                    }
                    else if (existingPanel.IsDisposed)
                    {
                        Logger.LogWarning("[PANEL] Cached panel for {PanelName} is disposed, removing from cache and creating new", panelName);
                        _cachedPanels.Remove(panelName);
                    }
                    else
                    {
                        try
                        {
                            ActivateExistingPanel(existingPanel, panelName, allowFloating);
                            Logger.LogInformation("[PANEL] ✅ Successfully activated existing hosted panel {PanelName}", panelName);
                            return;
                        }
                        catch (Exception activateEx)
                        {
                            Logger.LogError(activateEx, "[PANEL] Failed to activate existing hosted panel {PanelName}, will recreate", panelName);
                            _cachedPanels.Remove(panelName);
                        }
                    }
                }

                // Create form instance via DI
                TForm? form = null;
                try
                {
                    form = Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance<TForm>(_serviceProvider);
                    Logger.LogDebug("[PANEL-FORM] Form instance created successfully: {PanelName}, Type={Type}", panelName, form.GetType().FullName);
                }
                catch (Exception createEx)
                {
                    Logger.LogError(createEx, "[PANEL-FORM] Failed to create form instance via DI for {PanelName} ({FormType})", panelName, typeof(TForm).Name);
                    throw new InvalidOperationException(
                        $"Failed to create form '{panelName}' of type {typeof(TForm).Name}. Check that all constructor dependencies are registered in DI container.", createEx);
                }

                // Initialize with parameters if provided
                if (parameters is not null)
                {
                    if (form is IParameterizedPanel parameterizedForm)
                    {
                        try
                        {
                            Logger.LogDebug("[PANEL-FORM] Initializing form {PanelName} with parameters (type: {ParamType})", panelName, parameters.GetType().Name);
                            parameterizedForm.InitializeWithParameters(parameters);
                            Logger.LogDebug("[PANEL-FORM] Parameter initialization completed for {PanelName}", panelName);
                        }
                        catch (Exception paramEx)
                        {
                            Logger.LogError(paramEx, "[PANEL-FORM] Parameter initialization failed for {PanelName}", panelName);
                            throw new InvalidOperationException($"Failed to initialize form '{panelName}' with provided parameters.", paramEx);
                        }
                    }
                    else
                    {
                        Logger.LogWarning("[PANEL-FORM] Parameters provided for {PanelName} but form does not implement IParameterizedPanel", panelName);
                    }
                }

                // Host the form inside a UserControl so the docking manager can dock it like other panels
                var host = new FormHostPanel();
                try
                {
                    host.HostForm(form);
                }
                catch (Exception hostEx)
                {
                    Logger.LogError(hostEx, "[PANEL-FORM] Failed to host form instance for {PanelName}", panelName);
                    throw;
                }

                // Dock the hosted form
                DockPanelInternal(host, panelName, preferredStyle, allowFloating);
                Logger.LogInformation("[PANEL-FORM] ✅ Successfully created and docked new hosted form {PanelName}", panelName);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[PANEL-FORM] ❌ Failed to show form: {PanelName} - {ErrorType}: {ErrorMessage}",
                    panelName, ex.GetType().Name, ex.Message);
                throw new InvalidOperationException(
                    $"Unable to show hosted form '{panelName}' of type {typeof(TForm).Name}. Error: {ex.Message}. Check logs for detailed stack trace.", ex);
            }
        }

        public async Task AddPanelAsync(UserControl panel, string panelName, DockingStyle preferredStyle = DockingStyle.Right, bool allowFloating = true)
        {
            if (panel is null) throw new ArgumentNullException(nameof(panel));
            if (string.IsNullOrWhiteSpace(panelName)) throw new ArgumentException("Panel name cannot be empty.", nameof(panelName));

            try
            {
                // Reuse existing panel if already created
                if (_cachedPanels.TryGetValue(panelName, out var existingPanel))
                {
                    await _parentControl.InvokeAsync(() => ActivateExistingPanel(existingPanel, panelName, allowFloating));
                    return;
                }

                await _parentControl.InvokeAsync(() => DockPanelInternal(panel, panelName, preferredStyle, allowFloating));
                await Task.Yield(); // Allow UI to breathe
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to add panel async: {PanelName}", panelName);
                throw new InvalidOperationException($"Unable to add panel '{panelName}'. Check logs for details.", ex);
            }
        }

        private void ActivateExistingPanel(UserControl existingPanel, string panelName, bool allowFloating)
        {
            Logger.LogDebug("[PANEL-ACTIVATE] Activating existing panel {PanelName} - IsDisposed={IsDisposed}, Visible={Visible}",
                panelName, existingPanel?.IsDisposed, existingPanel?.Visible);

            // Validation: Check panel state
            if (existingPanel == null)
            {
                var ex = new ArgumentNullException(nameof(existingPanel), "Existing panel is null");
                Logger.LogError(ex, "[PANEL-ACTIVATE] Cannot activate null panel for {PanelName}", panelName);
                throw ex;
            }

            if (existingPanel.IsDisposed)
            {
                var ex = new ObjectDisposedException(nameof(existingPanel), "Panel has been disposed");
                Logger.LogError(ex, "[PANEL-ACTIVATE] Cannot activate disposed panel {PanelName}", panelName);
                throw ex;
            }

            // Validation: Check docking manager state
            if (_dockingManager == null)
            {
                var ex = new InvalidOperationException("DockingManager is null");
                Logger.LogError(ex, "[PANEL-ACTIVATE] Cannot activate panel {PanelName} without DockingManager", panelName);
                throw ex;
            }

            try
            {
                // Update caption settings
                Logger.LogDebug("[PANEL-ACTIVATE] Applying caption settings to {PanelName}", panelName);
                ApplyCaptionSettings(existingPanel, panelName, allowFloating);

                // Set visibility through DockingManager
                Logger.LogDebug("[PANEL-ACTIVATE] Setting dock visibility for {PanelName}", panelName);
                _dockingManager.SetDockVisibility(existingPanel, true);

                // Ensure the control is visible and rendered immediately
                Logger.LogDebug("[PANEL-ACTIVATE] Forcing panel visibility and refresh for {PanelName}", panelName);
                try
                {
                    existingPanel.Visible = true;
                    try { existingPanel.BringToFront(); }
                    catch (Exception bfEx)
                    {
                        Logger.LogDebug(bfEx, "[PANEL-ACTIVATE] BringToFront failed for {PanelName} (non-critical)", panelName);
                    }

                    _dockingManager.ActivateControl(existingPanel);
                    existingPanel.Refresh();
                    Logger.LogDebug("[PANEL-ACTIVATE] Panel visibility forced successfully for {PanelName}", panelName);
                }
                catch (Exception visEx)
                {
                    Logger.LogWarning(visEx, "[PANEL-ACTIVATE] Failed to force existing panel visibility/refresh for {PanelName}", panelName);
                    // Continue - panel may still be functional
                }

                // Apply theme
                Logger.LogDebug("[PANEL-ACTIVATE] Applying theme to {PanelName}", panelName);
                try
                {
                    ApplyPanelTheme(existingPanel);
                }
                catch (Exception themeEx)
                {
                    Logger.LogWarning(themeEx, "[PANEL-ACTIVATE] Theme application failed for {PanelName} (non-critical)", panelName);
                }

                // Try async initialization if panel implements IAsyncInitializable
                Logger.LogDebug("[PANEL-ACTIVATE] Checking async initialization for {PanelName}", panelName);
                try
                {
                    TryInitializeAsyncPanel(existingPanel, panelName);
                }
                catch (Exception initEx)
                {
                    Logger.LogWarning(initEx, "[PANEL-ACTIVATE] Async initialization attempt failed for {PanelName}", panelName);
                    // Continue - panel may already be initialized
                }

                // Track active panel and raise event for ribbon button highlighting
                _activePanelName = panelName;
                Logger.LogDebug("[PANEL-ACTIVATE] Raising PanelActivated event for {PanelName}", panelName);
                try
                {
                    PanelActivated?.Invoke(this, new PanelActivatedEventArgs(panelName, existingPanel.GetType()));
                }
                catch (Exception eventEx)
                {
                    Logger.LogWarning(eventEx, "[PANEL-ACTIVATE] PanelActivated event handler threw exception for {PanelName}", panelName);
                    // Continue - event handler failure shouldn't prevent panel activation
                }

                // POLISH: Announce panel visibility change to screen readers
                try
                {
                    AnnounceForAccessibility(existingPanel, $"{panelName} panel is now visible");
                }
                catch (Exception accEx)
                {
                    Logger.LogDebug(accEx, "[PANEL-ACTIVATE] Accessibility announcement failed for {PanelName} (non-critical)", panelName);
                }

                Logger.LogInformation("[PANEL-ACTIVATE] ✅ Successfully activated existing panel {PanelName} - Visible={Visible}, Bounds={Bounds}, Docked={Docked}",
                    panelName, existingPanel.Visible, existingPanel.Bounds, _dockingManager.GetEnableDocking(existingPanel));

                // Log detailed visibility diagnostics
                DiagnosticLogPanelVisibility(existingPanel, panelName);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[PANEL-ACTIVATE] ❌ Failed to activate existing panel {PanelName}", panelName);
                throw;
            }
        }

        private void DockPanelInternal(UserControl panel, string panelName, DockingStyle preferredStyle, bool allowFloating)
        {
            // Already on UI thread - DockPanelInternal is called only from ShowPanel or AddPanelAsync
            // which have already marshalled execution via InvokeAsync
            panel.Name = panelName.Replace(" ", "", StringComparison.Ordinal); // Clean name for internal use
            Logger.LogDebug("Docking panel: {PanelName} with style {Style}, allowFloating={AllowFloating}", panelName, preferredStyle, allowFloating);

            // Apply sensible defaults so charts/grids have usable space on first show
            ApplyDefaultPanelSizing(panel, preferredStyle, panel.GetType());

            // Enable docking features and caption buttons (required for headers and buttons to appear)
            var forceFloating = ShouldFloatByDefault(preferredStyle);
            ApplyCaptionSettings(panel, panelName, allowFloating || forceFloating);

            // Dock or float the panel
            var effectiveStyle = preferredStyle;

            var hostControl = _dockingManager.HostControl ?? _parentControl;
            if (hostControl.IsDisposed)
            {
                hostControl = _parentControl;
                Logger.LogWarning("DockingManager.HostControl was disposed, falling back to parent control for panel: {PanelName}", panelName);
            }

            if (forceFloating)
            {
                FloatPanel(panel, panelName, preferredStyle, hostControl);
                // CRITICAL FIX: For floating panels, explicitly trigger async initialization
                // Floating windows may not have Visible state propagated correctly,
                // so we must force initialization after the panel is created and floated.
                Logger.LogInformation("[PANEL-FLOAT] Panel {PanelName} floated - forcing async initialization trigger", panelName);
                TryInitializeAsyncPanel(panel, panelName);
                return;
            }

            if (effectiveStyle == DockingStyle.Fill)
            {
                Logger.LogWarning(
                    "DockingStyle.Fill is not supported when docking to the DockingManager host. Falling back to DockingStyle.Right for panel: {PanelName}",
                    panelName);
                effectiveStyle = DockingStyle.Right;
            }

            int dockSize = CalculateDockSize(effectiveStyle, hostControl);
            Logger.LogDebug("Calculated dock size: {Size} for style {Style}", dockSize, effectiveStyle);

            // Respect desired default dimension when we have one
            var (desiredSize, _) = GetDefaultPanelSizes(panel.GetType(), effectiveStyle);
            if (effectiveStyle is DockingStyle.Left or DockingStyle.Right && desiredSize.Width > 0)
            {
                dockSize = desiredSize.Width;
                Logger.LogDebug("Using desired width: {Width} for panel: {PanelName}", dockSize, panelName);
            }
            else if (effectiveStyle is DockingStyle.Top or DockingStyle.Bottom && desiredSize.Height > 0)
            {
                dockSize = desiredSize.Height;
                Logger.LogDebug("Using desired height: {Height} for panel: {PanelName}", dockSize, panelName);
            }

            // Dock the panel with calculated size
            // Priority 4: Log side panel availability check before docking
            var targetName = effectiveStyle switch
            {
                DockingStyle.Left => "LeftDockPanel",
                DockingStyle.Right => "RightDockPanel",
                _ => "HostControl"
            };
            var targetControl = effectiveStyle switch
            {
                DockingStyle.Left => _parentControl.Controls["LeftDockPanel"],
                DockingStyle.Right => _parentControl.Controls["RightDockPanel"],
                _ => hostControl
            };
            Logger.LogDebug("SIDE AVAIL CHECK | Style={Style} | Target={TargetName} | Target.Visible={Vis} | EnableDocking={Enable}",
                effectiveStyle,
                targetName,
                targetControl?.Visible ?? false,
                _dockingManager?.GetEnableDocking(targetControl) ?? false);

            _dockingManager.DockControl(panel, hostControl, effectiveStyle, dockSize);
            Logger.LogInformation("Panel docked successfully: {PanelName} at {Style} with size {Size}", panelName, effectiveStyle, dockSize);

            // QUICK TEST FIX: Force parent if missing (Syncfusion workaround)
            if (panel.Parent != _dockingManager.HostControl)
            {
                try
                {
                    _dockingManager.HostControl?.Controls.Add(panel);
                    panel.Dock = DockStyle.Fill;
                    Logger.LogWarning("FORCE PARENTED {PanelName} to HostControl (SetControl workaround)", panelName);
                }
                catch (Exception forceParentEx)
                {
                    Logger.LogWarning(forceParentEx, "Failed to force parent panel {PanelName} to HostControl", panelName);
                }
            }
            _dockingManager.HostControl?.Invalidate(true);

            // QuickBooksPanel: Prevent resizing due to explicit internal sizing (prevents StackOverflow)
            // Since all controls in QuickBooksPanel have explicit AutoSize=false + Heights,
            // user resizing the panel would break the layout stability guarantees.
            // Lock the panel to its initial size.
            if (panel.GetType() == typeof(QuickBooksPanel))
            {
                try
                {
                    // Set MaximumSize equal to initial size to prevent resize
                    var currentSize = panel.Size;
                    panel.MaximumSize = new Size(currentSize.Width, currentSize.Height);
                    Logger.LogDebug("QuickBooksPanel locked to size {Width}x{Height} to preserve explicit control sizing", currentSize.Width, currentSize.Height);
                }
                catch (Exception lockEx)
                {
                    Logger.LogDebug(lockEx, "Failed to lock QuickBooksPanel size (non-critical), continuing");
                }
            }

            // Apply minimum size to ensure usable bounds (Syncfusion API - no reflection needed)
            var (_, minimumSize) = GetDefaultPanelSizes(panel.GetType(), effectiveStyle);
            if (minimumSize.Width > 0 || minimumSize.Height > 0)
            {
                try
                {
                    _dockingManager.SetControlMinimumSize(panel, minimumSize);
                }
                catch (Exception minSizeEx)
                {
                    Logger.LogDebug(minSizeEx, "SetControlMinimumSize failed (non-critical), continuing");
                }
            }

            // Ensure theme cascade reaches the newly created panel and children
            ApplyPanelTheme(panel);

            // Set panel accessibility properties for UI automation
            try
            {
                panel.AccessibleName = panelName;
                panel.AccessibleDescription = $"Panel: {panelName}";
                panel.Tag = panelName;
            }
            catch (Exception accEx)
            {
                Logger.LogDebug(accEx, "Failed to set accessibility properties (non-critical)");
            }

            // Set dock label and caption via documented Syncfusion API (no reflection)
            try
            {
                _dockingManager.SetDockLabel(panel, panelName);
            }
            catch (Exception labelEx)
            {
                Logger.LogDebug(labelEx, "SetDockLabel failed (non-critical)");
            }

            // Update PanelHeader control if present
            try
            {
                // C# 14: Using 'is not null' pattern for more expressive null checking
                var header = panel.Controls.OfType<PanelHeader>().FirstOrDefault();
                if (header is not null)
                {
                    header.Title = panelName;
                    try { header.AccessibleName = panelName + " header"; } catch { }
                }
            }
            catch (Exception headerEx)
            {
                Logger.LogDebug(headerEx, "Failed to update PanelHeader (non-critical)");
            }

            // Force visibility and activation
            try
            {
                _dockingManager.SetDockVisibility(panel, true);
                _dockingManager.ActivateControl(panel);

                // POLISH: Apply fade-in animation on panel show
                _animationHelper.FadeIn(panel, durationMs: 200);
            }
            catch (Exception visEx)
            {
                Logger.LogDebug(visEx, "Failed to set visibility/activation (non-critical)");
            }

            // Ensure the panel is visible and refreshed after docking
            try
            {
                panel.Visible = true;
                try { panel.BringToFront(); } catch { }
                panel.Refresh();
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Failed to force panel visibility/refresh (non-critical)");
            }

            TryInitializeAsyncPanel(panel, panelName);

            // Subscribe to DockStateChanged event to verify panel rendering
            // This replaces the Thread.Sleep(250) hack with proper event-driven synchronization
            try
            {
                // Create a typed handler and store it so we can unsubscribe later
                Syncfusion.Windows.Forms.Tools.DockStateChangeEventHandler handler = (sender, e) => OnDockStateChanged(panel, panelName);
                _dockEventHandlers[panel] = handler;
                _dockingManager.DockStateChanged += handler;

                // Ensure we remove the handler if the panel is disposed
                panel.Disposed += (s, e) =>
                {
                    if (_dockEventHandlers.TryRemove(panel, out var existing))
                    {
                        try { _dockingManager.DockStateChanged -= existing; } catch { }
                    }
                };
            }
            catch (Exception eventEx)
            {
                Logger.LogDebug(eventEx, "Failed to subscribe to DockStateChanged (non-critical)");
            }

            // Cache for reuse
            _cachedPanels[panelName] = panel;

            // Track active panel and raise event for ribbon button highlighting
            _activePanelName = panelName;
            PanelActivated?.Invoke(this, new PanelActivatedEventArgs(panelName, panel.GetType()));

            Logger.LogInformation("Docked and activated new panel: {PanelName} ({PanelType})", panelName, panel.GetType().Name);
            Logger.LogInformation("[PANEL] {PanelName} docked - Visible={Visible}, Bounds={Bounds}", panelName, panel.Visible, panel.Bounds);

            // Priority 1: DOCK SUCCESS CHECK - verify panel is in HostControl
            Logger.LogInformation("DOCK SUCCESS CHECK | {PanelName} | HostControl.Contains={Contains} | Panel.Parent={ParentName} | HostControl.Controls.Count={Count} | Panel.Bounds={Bounds}",
                panelName,
                _dockingManager?.HostControl?.Controls.Contains(panel) ?? false,
                panel.Parent?.Name ?? "null",
                _dockingManager?.HostControl?.Controls.Count ?? 0,
                panel.Bounds);

            // Priority 2: List all HostControl children
            if (_dockingManager?.HostControl?.Controls.Count > 0)
            {
                foreach (Control c in _dockingManager.HostControl.Controls)
                {
                    Logger.LogInformation("HOST CHILD | Name={Name} | Type={Type} | Visible={Vis}", c.Name, c.GetType().Name, c.Visible);
                }
            }
            else
            {
                Logger.LogWarning("HOST EMPTY | No children in HostControl");
            }

            // Log detailed visibility diagnostics
            DiagnosticLogPanelVisibility(panel, panelName);
        }

        private void TryInitializeAsyncPanel(UserControl panel, string panelName)
        {
            Logger.LogDebug("[PANEL-INIT] TryInitializeAsyncPanel called for {PanelName} - Type={Type}",
                panelName, panel?.GetType().Name);

            if (panel is null)
            {
                Logger.LogWarning("[PANEL-INIT] Panel is null for {PanelName}", panelName);
                return;
            }

            if (panel.IsDisposed)
            {
                Logger.LogWarning("[PANEL-INIT] Panel {PanelName} is already disposed", panelName);
                return;
            }

            if (panel is not IAsyncInitializable asyncInitializable)
            {
                Logger.LogDebug("[PANEL-INIT] Panel {PanelName} does not implement IAsyncInitializable", panelName);
                return;
            }

            Logger.LogInformation("[PANEL-INIT] Panel {PanelName} implements IAsyncInitializable - scheduling initialization", panelName);

            void BeginInitialize()
            {
                Logger.LogInformation("[PANEL-INIT] BeginInitialize executing for {PanelName} on thread {Thread}",
                    panelName, System.Threading.Thread.CurrentThread.ManagedThreadId);
                _ = InitializePanelAsync(asyncInitializable, panelName);
            }

            if (panel.IsHandleCreated)
            {
                Logger.LogInformation("[PANEL-INIT] Handle already created for {PanelName}, calling BeginInitialize immediately", panelName);
                BeginInitialize();
                return;
            }

            Logger.LogInformation("[PANEL-INIT] Handle not yet created for {PanelName}, subscribing to HandleCreated event", panelName);
            EventHandler? handleCreated = null;
            handleCreated = (_, __) =>
            {
                Logger.LogInformation("[PANEL-INIT] HandleCreated event fired for {PanelName}", panelName);
                panel.HandleCreated -= handleCreated;
                if (!panel.IsDisposed)
                {
                    Logger.LogInformation("[PANEL-INIT] Panel {PanelName} not disposed, calling BeginInitialize", panelName);
                    BeginInitialize();
                }
                else
                {
                    Logger.LogWarning("[PANEL-INIT] Panel {PanelName} disposed during HandleCreated callback", panelName);
                }
            };
            panel.HandleCreated += handleCreated;
        }

        private async Task InitializePanelAsync(IAsyncInitializable asyncInitializable, string panelName)
        {
            Logger.LogInformation("[PANEL-INIT] InitializePanelAsync STARTING for {PanelName}", panelName);
            try
            {
                await asyncInitializable.InitializeAsync().ConfigureAwait(true);
                Logger.LogInformation("✅ [PANEL-INIT] Async initialization completed successfully for panel {PanelName}", panelName);
            }
            catch (OperationCanceledException)
            {
                Logger.LogWarning("⚠️ [PANEL-INIT] Async initialization cancelled for panel {PanelName}", panelName);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "❌ [PANEL-INIT] Async initialization FAILED for panel {PanelName}", panelName);
            }
        }

        /// <summary>
        /// Event handler for DockStateChanged. Validates panel visibility and forces rendering.
        /// Replaces Thread.Sleep(250) timing hack with proper event synchronization.
        /// </summary>
        /// <summary>
        /// Event handler for DockStateChanged. Validates panel visibility and forces rendering.
        /// C# 14: Using 'is not' pattern for cleaner control flow.
        /// </summary>
        private void OnDockStateChanged(UserControl panel, string panelName)
        {
            try
            {
                if (panel is not null && !panel.IsDisposed && panel.Visible)
                {
                    // Panel is visible - invalidate to force clean rendering
                    if (!panel.IsDisposed)
                    {
                        panel.Invalidate(true);
                        panel.Update();
                    }
                    Logger.LogDebug("Panel {PanelName} verified visible via DockStateChanged event", panelName);
                }
            }
            catch (ObjectDisposedException)
            {
                // Panel was disposed - safe to ignore
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Error in OnDockStateChanged for panel {PanelName}", panelName);
            }
        }

        private void ApplyPanelTheme(Control panel)
        {
            try
            {
                // C# 14: Extension method for safe theme application with null-conditional.
                // SfSkinManager is the single source of truth for theming.
                // Theme cascade from parent form automatically applies to all child controls.
                AppThemeColors.EnsureThemeAssemblyLoaded(Logger);
                var themeName = GetCurrentThemeName();
                panel?.ApplySyncfusionTheme(themeName, Logger);
            }
            catch
            {
                // Best-effort: if theming fails, continue without blocking panel display
            }
        }

        /// <summary>
        /// Gets the current active theme name from SfSkinManager.
        /// C# 14 feature: Uses simplified pattern matching and null-coalescing.
        /// SfSkinManager is Syncfusion's single source of truth for theming.
        /// </summary>
        private static string GetCurrentThemeName() =>
            SfSkinManager.ApplicationVisualTheme ?? AppThemeColors.DefaultTheme;

        private void ApplyCaptionSettings(UserControl panel, string panelName, bool allowFloating)
        {
            // Already on UI thread - ApplyCaptionSettings is called only from ActivateExistingPanel or DockPanelInternal
            // which are themselves called only from ShowPanel or AddPanelAsync (both marshalled via InvokeAsync)
            if (panel is null)
            {
                return;
            }

            try { _dockingManager.EnableContextMenu = true; } catch { }

            // Set caption, floating, and close button settings directly using Syncfusion DockingManager API
            try
            {
                // Set the caption text
                _dockingManager.SetDockLabel(panel, panelName);
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "SetDockLabel failed (non-critical)");
            }

            try
            {
                // Enable/disable floating
                _dockingManager.SetEnableDocking(panel, true);
                _dockingManager.SetAllowFloating(panel, allowFloating);
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "SetAllowFloating failed (non-critical)");
            }

            try
            {
                // Show close button
                _dockingManager.SetCloseButtonVisibility(panel, true);
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "SetCloseButtonVisibility failed (non-critical)");
            }

            try
            {
                _dockingManager.SetAutoHideButtonVisibility(panel, true);
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "SetAutoHideButtonVisibility failed (non-critical)");
            }

            try
            {
                _dockingManager.SetMenuButtonVisibility(panel, true);
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "SetMenuButtonVisibility failed (non-critical)");
            }
        }

        private static bool ShouldFloatByDefault(DockingStyle preferredStyle)
        {
            // Only panels explicitly designated for left/right docking should dock by default.
            return preferredStyle is not DockingStyle.Left and not DockingStyle.Right;
        }

        private void FloatPanel(UserControl panel, string panelName, DockingStyle preferredStyle, Control hostControl)
        {
            var bounds = CalculateFloatingBounds(panel, preferredStyle, hostControl);
            _dockingManager.FloatControl(panel, bounds);

            try
            {
                _dockingManager.SetDockVisibility(panel, true);
                Logger.LogDebug("[PANEL-FLOAT] SetDockVisibility(true) called for {PanelName}", panelName);
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "SetDockVisibility failed for floating panel {PanelName}", panelName);
            }

            try
            {
                panel.Visible = true;
                Logger.LogDebug("[PANEL-FLOAT] Panel.Visible set to true for {PanelName}", panelName);
                panel.BringToFront();
                Logger.LogDebug("[PANEL-FLOAT] BringToFront() called for {PanelName}", panelName);
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Failed to update floating panel visibility for {PanelName}", panelName);
            }

            try
            {
                _dockingManager.ActivateControl(panel);
                Logger.LogDebug("[PANEL-FLOAT] ActivateControl() called for {PanelName}", panelName);
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Failed to activate floating panel {PanelName}", panelName);
            }

            // CRITICAL FIX: Force panel invalidation and refresh to ensure rendering
            try
            {
                panel.Invalidate(true);
                panel.Update();
                Logger.LogDebug("[PANEL-FLOAT] Panel invalidated and updated for {PanelName}", panelName);
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Failed to invalidate/update floating panel for {PanelName}", panelName);
            }

            Logger.LogInformation("Panel floated by default: {PanelName} at {Bounds}, Visible={Visible}, Handle={Handle}",
                panelName, bounds, panel.Visible, panel.IsHandleCreated);
        }

        private static System.Drawing.Rectangle CalculateFloatingBounds(UserControl panel, DockingStyle preferredStyle, Control hostControl)
        {
            var (desiredSize, minimumSize) = GetDefaultPanelSizes(panel.GetType(), preferredStyle);
            var width = desiredSize.Width > 0 ? desiredSize.Width : Math.Max(minimumSize.Width, Math.Max(640, hostControl.Width / 2));
            var height = desiredSize.Height > 0 ? desiredSize.Height : Math.Max(minimumSize.Height, Math.Max(480, hostControl.Height / 2));

            var screen = Screen.FromControl(hostControl).WorkingArea;
            width = Math.Min(width, screen.Width);
            height = Math.Min(height, screen.Height);

            var x = screen.Left + Math.Max(0, (screen.Width - width) / 2);
            var y = screen.Top + Math.Max(0, (screen.Height - height) / 2);

            return new System.Drawing.Rectangle(x, y, width, height);
        }

        public void Dispose()
        {
            // Ensure disposal happens on UI thread to safely dispose controls
            // Dictionary iteration and panel disposal must be thread-safe
            if (_parentControl.InvokeRequired)
            {
                _parentControl.Invoke(new System.Action(() => Dispose()));
                return;
            }

            try
            {
                // Dispose all cached panels to release their resources
                // This is important for panels with Syncfusion controls (grids, charts) that hold resources
                foreach (var panel in _cachedPanels.Values)
                {
                    try
                    {
                        panel?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(ex, "Exception disposing cached panel (continuing)");
                    }
                }

                Logger.LogInformation("Disposed {Count} cached panels", _cachedPanels.Count);

                // Dispose animation helper
                try
                {
                    _animationHelper?.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Exception disposing animation helper (continuing)");
                }
            }
            finally
            {
                _cachedPanels.Clear();
            }
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

        private static void ApplyDefaultPanelSizing(UserControl panel, DockingStyle style, Type panelType)
        {
            var (desired, minimum) = GetDefaultPanelSizes(panelType, style);

            if (minimum.Width > 0 || minimum.Height > 0)
            {
                var mergedMin = new Size(
                    Math.Max(panel.MinimumSize.Width, minimum.Width),
                    Math.Max(panel.MinimumSize.Height, minimum.Height));
                panel.MinimumSize = mergedMin;
            }

            if (desired.Width > 0 || desired.Height > 0)
            {
                // Set control size so DockingManager honors the initial width/height.
                try { panel.Size = new Size(Math.Max(desired.Width, panel.Width), Math.Max(desired.Height, panel.Height)); } catch { }
            }
        }

        private static (Size desiredSize, Size minimumSize) GetDefaultPanelSizes(Type panelType, DockingStyle style)
        {
            var sizing = DefaultPanelSizing;
            if (PanelSizeOverrides.TryGetValue(panelType, out var overrideSizing))
            {
                sizing = MergeSizing(DefaultPanelSizing, overrideSizing);
            }

            var desired = style switch
            {
                DockingStyle.Left or DockingStyle.Right => sizing.Side,
                DockingStyle.Top or DockingStyle.Bottom => sizing.TopBottom,
                _ => Size.Empty
            };

            // C# 14: Use extension property for cleaner minimum size calculation.
            // Creates a temporary UserControl to compute style-aware minimums.
            // In production, this would be a direct type check; shown here for illustration.
            var minimum = sizing.Minimum;

            // Enforce reasonable minima for orientation using C# 14 patterns
            minimum = style switch
            {
                DockingStyle.Top or DockingStyle.Bottom => EnforceMinimum(minimum, new Size(800, 300)),
                DockingStyle.Left or DockingStyle.Right => EnforceMinimum(minimum, new Size(420, 360)),
                DockingStyle.Tabbed or DockingStyle.Fill or _ => EnforceMinimum(minimum, new Size(800, 600))
            };

            return (desired, minimum);
        }

        /// <summary>
        /// C# 14: Helper method using required return type semantics.
        /// Enforces minimum size by taking max of current and required.
        /// </summary>
        private static Size EnforceMinimum(Size current, Size required) => new(
            Math.Max(current.Width, required.Width),
            Math.Max(current.Height, required.Height)
        );

        private static PanelSizing MergeSizing(PanelSizing defaults, PanelSizing overrides)
        {
            Size MergeSize(Size @default, Size @override)
            {
                return new Size(
                    @override.Width > 0 ? @override.Width : @default.Width,
                    @override.Height > 0 ? @override.Height : @default.Height);
            }

            var mergedSide = MergeSize(defaults.Side, overrides.Side);
            var mergedTopBottom = MergeSize(defaults.TopBottom, overrides.TopBottom);
            var mergedMinimum = new Size(
                Math.Max(defaults.Minimum.Width, overrides.Minimum.Width),
                Math.Max(defaults.Minimum.Height, overrides.Minimum.Height));

            return new PanelSizing(mergedSide, mergedTopBottom, mergedMinimum);
        }

        private readonly record struct PanelSizing(Size Side, Size TopBottom, Size Minimum);

        private static readonly PanelSizing DefaultPanelSizing = new PanelSizing(
            new Size(540, 0),
            new Size(0, 400),
            new Size(420, 360));

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
                return (bool)_parentControl.Invoke(new Func<bool>(() => HidePanel(panelName)));
            }

            if (_cachedPanels.TryGetValue(panelName, out var existingPanel))
            {
                _dockingManager.SetDockVisibility(existingPanel, false);
                Logger.LogDebug("Hidden panel: {PanelName}", panelName);
                return true;
            }

            Logger.LogWarning("Cannot hide panel '{PanelName}' - not found", panelName);
            return false;
        }

        /// <summary>
        /// Get the currently active panel name.
        /// Thread-safe: Routes through UI thread if called from background thread.
        /// C# 14: Simplified with null-conditional operator and pattern matching.
        /// </summary>
        /// <returns>The name of the currently active panel, or null if no panel is active.</returns>
        public string? GetActivePanelName()
        {
            // C# 14: Null-conditional operator with method invocation.
            // If InvokeRequired is true, marshal to UI thread.
            if (_parentControl.InvokeRequired)
            {
                return (string?)_parentControl.Invoke(new Func<string?>(() => GetActivePanelName()));
            }
            // Return the currently active panel name (or null if none)
            return _activePanelName;
        }

        /// <summary>
        /// POLISH: Announces a message to screen readers via AccessibleName update.
        /// This provides accessibility feedback when panel visibility changes occur.
        /// </summary>
        /// <param name="control">The control to announce from.</param>
        /// <param name="announcementText">The text to announce to screen readers.</param>
        private void AnnounceForAccessibility(Control control, string announcementText)
        {
            if (control == null || string.IsNullOrWhiteSpace(announcementText))
            {
                return;
            }

            try
            {
                // WinForms accessibility: Update AccessibleName to trigger screen reader announcement
                control.AccessibleName = announcementText;
                Logger.LogDebug("Accessibility announcement: {Text}", announcementText);
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Failed to announce accessibility message (non-critical)");
            }
        }

        /// <summary>
        /// Diagnostic method: Captures all visibility-related properties of a single panel.
        /// Logs: DockState, Parent, Visible, DockVisibility, AutoHideMode, ZIndex, Bounds.
        /// </summary>
        private void DiagnosticLogPanelVisibility(UserControl? panel, string panelName)
        {
            if (panel == null || panel.IsDisposed)
            {
                Logger.LogWarning("[PANEL-VISIBILITY-DIAG] ❌ NULL/DISPOSED | {PanelName}", panelName);
                return;
            }

            try
            {
                var dockState = DockState.Float;
                var parentName = panel.Parent?.Name ?? "(no parent)";
                var isVisible = panel.Visible;
                var dockVisibility = false;
                var autoHideMode = false;
                var zIndex = -1;
                var bounds = panel.Bounds;

                if (_dockingManager != null)
                {
                    try { dockState = _dockingManager.GetDockState(panel); } catch { }
                    try { dockVisibility = _dockingManager.GetDockVisibility(panel); } catch { }
                    try { autoHideMode = _dockingManager.GetAutoHideMode(panel); } catch { }
                    try { zIndex = panel.Parent?.Controls.GetChildIndex(panel, false) ?? -1; } catch { }
                }

                var visibilityStatus = (isVisible && dockVisibility && !autoHideMode) ? "✅ VISIBLE" : "⚠️ HIDDEN";
                Logger.LogInformation(
                    "[PANEL-VISIBILITY-DIAG] {Status} {PanelName} | DockState={DockState} | Parent={Parent} | " +
                    "Visible={Visible} | DockVis={DockVis} | AutoHide={AutoHide} | ZIndex={ZIndex} | " +
                    "Bounds=({X},{Y},{W}x{H})",
                    visibilityStatus, panelName, dockState, parentName, isVisible, dockVisibility,
                    autoHideMode, zIndex, bounds.X, bounds.Y, bounds.Width, bounds.Height);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "[PANEL-VISIBILITY-DIAG] Exception logging panel visibility for {PanelName}", panelName);
            }
        }

        /// <summary>
        /// Diagnostic method: Logs visibility state of ALL cached panels (summary view).
        /// Also logs availability of side panels (LeftDockPanel, RightDockPanel).
        /// </summary>
        private void DiagnosticLogAllPanelVisibility()
        {
            try
            {
                var sidePanelsAvailable = (_parentControl.Controls["LeftDockPanel"]?.Visible ?? false) ||
                                         (_parentControl.Controls["RightDockPanel"]?.Visible ?? false);

                Logger.LogInformation(
                    "[PANEL-VISIBILITY-DIAG] Summary: {PanelCount} panels cached | SidePanelsAvailable={Available}",
                    _cachedPanels.Count, sidePanelsAvailable);

                var visibleCount = 0;
                var hiddenCount = 0;

                foreach (var kvp in _cachedPanels)
                {
                    var panelName = kvp.Key;
                    var panel = kvp.Value;

                    if (panel == null || panel.IsDisposed)
                    {
                        Logger.LogDebug("[PANEL-VISIBILITY-DIAG] Panel '{PanelName}' is null/disposed", panelName);
                        hiddenCount++;
                        continue;
                    }

                    var isVisible = false;
                    var dockState = DockState.Float;

                    if (_dockingManager != null)
                    {
                        try { isVisible = panel.Visible && _dockingManager.GetDockVisibility(panel); } catch { }
                        try { dockState = _dockingManager.GetDockState(panel); } catch { }
                    }

                    if (isVisible)
                        visibleCount++;
                    else
                        hiddenCount++;

                    Logger.LogDebug(
                        "[PANEL-VISIBILITY-DIAG]   - {PanelName}: {Status} | DockState={DockState}",
                        panelName, isVisible ? "✅" : "❌", dockState);
                }

                Logger.LogInformation(
                    "[PANEL-VISIBILITY-DIAG] Final tally: {VisibleCount} visible, {HiddenCount} hidden",
                    visibleCount, hiddenCount);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "[PANEL-VISIBILITY-DIAG] Exception logging all panel visibility");
            }
        }
    }

    /// <summary>
    /// Event args for panel activation events.
    /// </summary>
    public class PanelActivatedEventArgs : EventArgs
    {
        public string PanelName { get; set; }
        public Type PanelType { get; set; }

        public PanelActivatedEventArgs(string panelName, Type panelType)
        {
            PanelName = panelName;
            PanelType = panelType;
        }
    }
}
