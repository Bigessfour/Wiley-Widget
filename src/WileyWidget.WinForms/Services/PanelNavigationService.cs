using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Themes;

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

        private static readonly Dictionary<Type, PanelSizing> PanelSizeOverrides = new()
        {
            { typeof(DashboardPanel), new PanelSizing(new Size(560, 0), new Size(0, 420), new Size(450, 420)) },
            { typeof(AccountsPanel), new PanelSizing(new Size(620, 0), new Size(0, 380), new Size(520, 420)) },
            { typeof(ChartPanel), new PanelSizing(new Size(560, 0), new Size(0, 460), new Size(480, 420)) },
            { typeof(BudgetOverviewPanel), new PanelSizing(new Size(540, 0), new Size(0, 420), new Size(480, 360)) },
            { typeof(AnalyticsPanel), new PanelSizing(new Size(560, 0), new Size(0, 400), new Size(460, 380)) },
            { typeof(AuditLogPanel), new PanelSizing(new Size(520, 0), new Size(0, 380), new Size(440, 320)) },
            { typeof(ReportsPanel), new PanelSizing(new Size(560, 0), new Size(0, 400), new Size(460, 360)) },
            { typeof(ProactiveInsightsPanel), new PanelSizing(new Size(560, 0), new Size(0, 400), new Size(460, 360)) },
            { typeof(WarRoomPanel), new PanelSizing(new Size(560, 0), new Size(0, 420), new Size(460, 380)) },
            { typeof(QuickBooksPanel), new PanelSizing(new Size(620, 0), new Size(0, 400), new Size(540, 360)) },
            { typeof(BudgetPanel), new PanelSizing(new Size(560, 0), new Size(0, 400), new Size(460, 360)) },
            { typeof(DepartmentSummaryPanel), new PanelSizing(new Size(540, 0), new Size(0, 400), new Size(440, 360)) },
            { typeof(SettingsPanel), new PanelSizing(new Size(500, 0), new Size(0, 360), new Size(420, 320)) },
            { typeof(RevenueTrendsPanel), new PanelSizing(new Size(560, 0), new Size(0, 440), new Size(460, 380)) },
            { typeof(UtilityBillPanel), new PanelSizing(new Size(560, 0), new Size(0, 400), new Size(460, 360)) },
        };

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
                    ApplyCaptionSettings(existingPanel, panelName, allowFloating);
                    _dockingManager.SetDockVisibility(existingPanel, true);
                    try { existingPanel.BringToFront(); } catch { }
                    _dockingManager.ActivateControl(existingPanel);
                    ApplyPanelTheme(existingPanel);
                    _logger.LogDebug("Activated existing panel: {PanelName}", panelName);
                    return;
                }

                // Create new instance via DI (supports constructor injection)
                Console.WriteLine($"[PanelNavigationService] Creating panel: {panelName} ({typeof(TPanel).Name})");
                var panel = Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance<TPanel>(_serviceProvider);
                panel.Name = panelName.Replace(" ", "", StringComparison.Ordinal); // Clean name for internal use

                // Apply sensible defaults so charts/grids have usable space on first show
                ApplyDefaultPanelSizing(panel, preferredStyle, typeof(TPanel));

                // If panel supports parameter initialization, pass the parameters
                if (parameters != null && panel is IParameterizedPanel parameterizedPanel)
                {
                    parameterizedPanel.InitializeWithParameters(parameters);
                }

                // Enable docking features and caption buttons (required for headers and buttons to appear)
                ApplyCaptionSettings(panel, panelName, allowFloating);

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

                // Respect desired default dimension when we have one
                var (desiredSize, _) = GetDefaultPanelSizes(typeof(TPanel), effectiveStyle);
                if (effectiveStyle is DockingStyle.Left or DockingStyle.Right && desiredSize.Width > 0)
                {
                    dockSize = desiredSize.Width;
                }
                else if (effectiveStyle is DockingStyle.Top or DockingStyle.Bottom && desiredSize.Height > 0)
                {
                    dockSize = desiredSize.Height;
                }
                _dockingManager.DockControl(panel, _parentControl, effectiveStyle, dockSize);

                // Ensure theme cascade reaches the newly created panel and children
                ApplyPanelTheme(panel);

                // CRITICAL FIX: For ChatPanel, apply full docking configuration to ensure proper visibility
                // and prevent auto-hide state that causes clipping issues
                if (typeof(TPanel).Name == "ChatPanel")
                {
                    try
                    {
                        _logger.LogDebug("Applying ChatPanel-specific docking configuration");

                        // Force visible and active
                        _dockingManager.SetDockVisibility(panel, true);
                        try { panel.BringToFront(); } catch { }
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

        private static void ApplyPanelTheme(Control panel)
        {
            try
            {
                SfSkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);
                SfSkinManager.SetVisualStyle(panel, ThemeColors.DefaultTheme);
            }
            catch
            {
                // Best-effort: if theming fails, continue without blocking panel display
            }
        }

        private void ApplyCaptionSettings(UserControl panel, string panelName, bool allowFloating)
        {
            if (panel == null)
            {
                return;
            }

            try { _dockingManager.EnableContextMenu = true; } catch { }

            try { _dockingManager.SetEnableDocking(panel, true); } catch { }
            try { _dockingManager.SetDockLabel(panel, panelName); } catch { }
            try { _dockingManager.SetAllowFloating(panel, allowFloating); } catch { }
            try { _dockingManager.SetCloseButtonVisibility(panel, true); } catch { }
            try { _dockingManager.SetAutoHideButtonVisibility(panel, true); } catch { }
            try { _dockingManager.SetMenuButtonVisibility(panel, true); } catch { }
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

                // If Syncfusion exposes DesiredDockSize, set it via reflection without hard dependency.
                try
                {
                    var prop = panel.GetType().GetProperty("DesiredDockSize");
                    prop?.SetValue(panel, desired);
                }
                catch { /* Safe fallback if property is missing */ }
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

            var minimum = sizing.Minimum;

            // Enforce reasonable minima for orientation
            if (style is DockingStyle.Top or DockingStyle.Bottom)
            {
                if (minimum.Width < 800) minimum.Width = 800;
                if (minimum.Height < 300) minimum.Height = 300;
            }
            else if (style is DockingStyle.Left or DockingStyle.Right)
            {
                if (minimum.Height < 360) minimum.Height = 360;
            }
            else
            {
                // Floating/tabbed/fill: ensure a sensible default canvas
                if (minimum.Width < 800) minimum.Width = 800;
                if (minimum.Height < 600) minimum.Height = 600;
            }

            return (desired, minimum);
        }

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
