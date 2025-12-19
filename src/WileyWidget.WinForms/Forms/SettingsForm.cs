using System.Diagnostics.CodeAnalysis;
using Serilog;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Theming;
using Syncfusion.WinForms.Controls;
using Syncfusion.Windows.Forms;
using Syncfusion.WinForms.Themes;
using WileyWidgetThemeColors = WileyWidget.WinForms.Themes.ThemeColors;

namespace WileyWidget.WinForms.Forms
{
    internal static class SettingsFormResources
    {
        public const string FormTitle = "Settings";
        public const string FormDescription = "Application Configuration";
    }

    /// <summary>
    /// Settings dialog form that hosts the SettingsPanel control.
    /// All controls are Syncfusion components with proper theming applied via SkinManager.
    /// </summary>
    [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
    public partial class SettingsForm : Form
    {
        private readonly ILogger _logger;
        private readonly SettingsViewModel _vm;
        private readonly IThemeService _themeService;
        private SettingsPanel? _settingsPanel;

        public SettingsForm(ILogger logger, SettingsViewModel vm, MainForm mainForm)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _vm = vm ?? throw new ArgumentNullException(nameof(vm));
            if (mainForm == null)
            {
                throw new ArgumentNullException(nameof(mainForm));
            }

            _logger.Debug("SettingsForm constructor started");
            _themeService = ResolveThemeService();

            // Only set MdiParent if MainForm is in MDI mode
            // In DockingManager mode, forms are shown as owned windows, not MDI children
            if (mainForm.IsMdiContainer)
            {
                MdiParent = mainForm;
            }

            // InitializeComponent is not needed - all controls are created programmatically
            Text = SettingsFormResources.FormTitle;

            // Set Name for UI test identification (used as AutomationId in UI Automation)
            this.Name = "Settings";
            this.AccessibleName = "SettingsForm";
            this.AccessibleDescription = "Configure application settings including theme, database connection, and user preferences";

            // Apply Syncfusion theme to form and all child controls
            // NOTE: ThemeColors.ApplyTheme internally calls SkinManager.SetVisualStyle
            // REMOVED: WileyWidgetThemeColors.ApplyTheme(this); // Global theming only - no per-form theme sets
            // REMOVED: SkinManager.SetVisualStyle(this, "Office2019Colorful"); // Duplicate - already called by ApplyTheme

            // Set minimum size to prevent the form from being resized too small
            // SettingsPanel is 500x400, so minimum should account for borders/title bar
            MinimumSize = new Size(550, 480);

            // Initialize form controls and settings panel
            InitializeFormControls();

            _logger.Information("SettingsForm initialized successfully");
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            _logger.Information("SettingsForm loaded");

            if (MdiParent is MainForm mf)
            {
                try
                {
                    mf.RegisterMdiChildWithDocking(this);
                }
                catch (Exception ex)
                {
                    // Docking registration is best-effort; fall back to standard MDI when unavailable.
                    _logger.Debug(ex, "Docking registration failed for SettingsForm - falling back to standard MDI");
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _logger.Information("SettingsForm closed");
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _logger.Debug("SettingsForm shown to user");
        }

        private static IThemeService ResolveThemeService()
        {
            // Try to get from Program.Services if available (normal runtime scenario)
            if (Program.Services != null)
            {
                return Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(Program.Services);
            }

            // Fall back to a simple implementation for test scenarios where Program.Services is not initialized
            // This allows the form to be instantiated during DI resolution tests without failing
            return new DefaultThemeService();
        }

        /// <summary>
        /// Default theme service implementation used when DI container is not available (e.g., in tests).
        /// </summary>
        private class DefaultThemeService : IThemeService
        {
            public AppTheme CurrentTheme => AppTheme.Office2019Colorful;
            public AppTheme Preference => AppTheme.Office2019Colorful;
            public event EventHandler<AppTheme>? ThemeChanged;
            public void SetTheme(AppTheme theme) => ThemeChanged?.Invoke(this, theme);
        }

        private void InitializeFormControls()
        {
            try
            {
                if (Program.Services != null)
                {
                    var iconService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IThemeIconService>(Program.Services);
                    if (iconService != null)
                    {
                        var icon = iconService.GetIcon("settings", _themeService.CurrentTheme, 16);
                        if (icon != null && icon is System.Drawing.Bitmap bitmap)
                        {
                            this.Icon = Icon.FromHandle(bitmap.GetHicon());
                        }
                    }
                }
            }
            catch { /* Icon loading is optional */ }

            // Host the SettingsPanel which contains all the actual settings controls
            _settingsPanel = new SettingsPanel(_vm, _themeService)
            {
                Dock = DockStyle.Fill,
                AccessibleName = "Settings Panel"
            };

            Controls.Add(_settingsPanel);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Use SafeDispose to prevent Syncfusion crashes (nullable ? already handled by extension method)
                _settingsPanel.SafeDispose();
            }
            base.Dispose(disposing);
        }
    }
}
