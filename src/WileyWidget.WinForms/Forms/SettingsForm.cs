using System.Diagnostics.CodeAnalysis;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Services;
using Syncfusion.Windows.Forms;
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
    /// All controls are Syncfusion components with proper theming applied via SfSkinManager.
    /// </summary>
    [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
    public partial class SettingsForm : Form
    {
        private readonly SettingsViewModel _vm;
        private readonly IThemeService _themeService;
        private SettingsPanel? _settingsPanel;

        public SettingsForm(SettingsViewModel vm)
        {
            _vm = vm ?? throw new ArgumentNullException(nameof(vm));
            _themeService = ResolveThemeService();

            InitializeComponent();
            Text = SettingsFormResources.FormTitle;

            // Apply Syncfusion theme to form and all child controls
            WileyWidgetThemeColors.ApplyTheme(this);
        }

        private static IThemeService ResolveThemeService()
        {
            if (Program.Services == null)
            {
                throw new InvalidOperationException("SettingsForm requires DI services to be initialized. Ensure Program.Services is set.");
            }
            return Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(Program.Services);
        }

        private void InitializeComponent()
        {
            // Form properties
            Name = "SettingsForm";
            Text = SettingsFormResources.FormTitle;
            Size = new Size(520, 850);
            MinimumSize = new Size(520, 600);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.Dpi;

            // Set form icon if available
            try
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
            catch { /* Icon loading is optional */ }

            // Host the SettingsPanel which contains all the actual settings controls
            _settingsPanel = new SettingsPanel
            {
                Dock = DockStyle.Fill
            };

            Controls.Add(_settingsPanel);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _settingsPanel?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
