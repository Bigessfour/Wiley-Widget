using System.Diagnostics.CodeAnalysis;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Theming;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Services;

namespace WileyWidget.WinForms.Controls
{
    internal static class SettingsPanelResources
    {
        public const string PanelTitle = "Settings";
        public const string AppTitleLabel = "Application Title:";
        public const string AppearanceGroup = "Appearance";
        public const string DarkThemeLabel = "Dark Theme (Fluent Dark)";
        public const string LightThemeLabel = "Light Theme (Fluent Light)";
        public const string AboutGroup = "About";
        public const string SettingsSavedTitle = "Settings Saved";
        public const string SettingsSavedMessage = "Theme changes are applied immediately and saved.";
    }

    [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
    public partial class SettingsPanel : UserControl
    {
        public object? DataContext { get; private set; }

        private readonly SettingsViewModel _vm;
        private readonly IThemeService _themeService;
        private readonly WileyWidget.Services.Threading.IDispatcherHelper? _dispatcherHelper;

        // Controls
        private Panel? _mainPanel;
        private GroupBox? _themeGroup;
        private Syncfusion.WinForms.ListView.SfComboBox? _themeCombo;
        private GroupBox? _aboutGroup;
        private Label? _lblVersion;
        private Label? _lblDbStatus;
        private Syncfusion.WinForms.Controls.SfButton? _btnClose;
        private TextBox? _txtAppTitle;
        private ErrorProvider? _error_provider;
        private EventHandler<AppTheme>? _panelThemeChangedHandler;
        private EventHandler<AppTheme>? _btnCloseThemeChangedHandler;
        private CheckBox? _chkOpenEditFormsDocked;

        public SettingsPanel() : this(
            Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<SettingsViewModel>(Program.Services),
            Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(Program.Services),
            Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.Services.Threading.IDispatcherHelper>(Program.Services))
        {
        }

        public SettingsPanel(SettingsViewModel vm, IThemeService themeService, WileyWidget.Services.Threading.IDispatcherHelper? dispatcherHelper = null)
        {
            _dispatcherHelper = dispatcherHelper;
            _vm = vm ?? throw new ArgumentNullException(nameof(vm));
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));

            DataContext = vm;

            InitializeComponent();
            SetupUI();
            ApplyCurrentTheme();

            // Subscribe to theme changes
            _panelThemeChangedHandler = OnThemeChanged;
            _themeService.ThemeChanged += _panelThemeChangedHandler;

            // Start async load
            _ = LoadViewDataAsync();
        }

        private void InitializeComponent()
        {
            Name = "SettingsPanel";
            Size = new Size(500, 400);
            Dock = DockStyle.Fill;
            try { AutoScaleMode = AutoScaleMode.Dpi; } catch { }
        }

        private void SetupUI()
        {
            var padding = 20;
            var y = padding;

            _mainPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(padding) };

            try { _error_provider = new ErrorProvider() { BlinkStyle = ErrorBlinkStyle.NeverBlink }; } catch { }

            // App title
            var lblAppTitle = new Label { Text = SettingsPanelResources.AppTitleLabel, AutoSize = true, Location = new Point(padding, y + 4), Font = new Font("Segoe UI", 9, FontStyle.Regular) };
            _txtAppTitle = new TextBox { Name = "txtAppTitle", Location = new Point(padding + 120, y), Width = 300, MaxLength = 100, Font = new Font("Segoe UI", 10F), AccessibleName = "Application Title", AccessibleDescription = "Set the friendly application title" };
            var txtToolTip = new ToolTip();
            txtToolTip.SetToolTip(_txtAppTitle, "Enter a custom title for the application window");
            _txtAppTitle.Validating += TxtAppTitle_Validating;
            _txtAppTitle.Validated += TxtAppTitle_Validated;
            _mainPanel.Controls.Add(lblAppTitle);
            _mainPanel.Controls.Add(_txtAppTitle);
            y += 40;

            // Appearance group
            _themeGroup = new GroupBox { Text = SettingsPanelResources.AppearanceGroup, Location = new Point(padding, y), Size = new Size(440, 100), Font = new Font("Segoe UI", 10, FontStyle.Bold) };

            _themeCombo = new Syncfusion.WinForms.ListView.SfComboBox { Name = "themeCombo", Location = new Point(20, 30), Size = new Size(380, 24), DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList, AllowDropDownResize = false, MaxDropDownItems = 5, AccessibleName = "Theme selection", AccessibleDescription = "Select application theme" };
            _themeCombo.DropDownListView.Style.ItemStyle.Font = new Font("Segoe UI", 10F);
            try { _themeCombo.DataSource = _vm?.Themes ?? Enum.GetValues<AppTheme>().Cast<object>().ToList(); } catch { }
            try { _themeCombo.SelectedItem = _themeService.Preference; } catch { }
            _themeCombo.SelectedIndexChanged += (s, e) => { try { if (_themeCombo.SelectedItem is AppTheme sel) _themeService.Preference = sel; } catch { } };

            _themeGroup.Controls.Add(_themeCombo);
            _mainPanel.Controls.Add(_themeGroup);
            y += 120;

            // Behavior settings
            _chkOpenEditFormsDocked = new CheckBox { Text = "Open edit forms docked (as floating tool windows)", AutoSize = true, Location = new Point(padding, y), Checked = _vm?.OpenEditFormsDocked ?? false, Font = new Font("Segoe UI", 9, FontStyle.Regular), AccessibleName = "Open edit forms docked", AccessibleDescription = "Open account edit forms as dockable floating windows" };
            var dockedToolTip = new ToolTip(); dockedToolTip.SetToolTip(_chkOpenEditFormsDocked, "Open account edit forms as dockable floating windows instead of modal dialogs"); _chkOpenEditFormsDocked.CheckedChanged += (s, e) => { if (_vm != null) _vm.OpenEditFormsDocked = _chkOpenEditFormsDocked.Checked; };
            _mainPanel.Controls.Add(_chkOpenEditFormsDocked);
            y += 35;

            // About
            _aboutGroup = new GroupBox { Text = SettingsPanelResources.AboutGroup, Location = new Point(padding, y), Size = new Size(440, 120), Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            _lblVersion = new Label { Text = $"Wiley Widget v1.0.0\n.NET {Environment.Version}\nRuntime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}", Location = new Point(20, 30), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Regular) };
            _lblDbStatus = new Label { Text = "Database: Connected", Location = new Point(20, 85), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Regular) };
            _aboutGroup.Controls.Add(_lblVersion); _aboutGroup.Controls.Add(_lblDbStatus); _mainPanel.Controls.Add(_aboutGroup); y += 140;

            // Close button
            _btnClose = new Syncfusion.WinForms.Controls.SfButton { Name = "btnClose", Text = "Close", Size = new Size(100, 35), Location = new Point(350, y), AccessibleName = "Close settings", AccessibleDescription = "Close the settings panel" };
            var closeToolTip = new ToolTip(); closeToolTip.SetToolTip(_btnClose, "Close this settings panel (Esc)"); _btnClose.Click += BtnClose_Click;
            try
            {
                var iconService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IThemeIconService>(Program.Services);
                _btnClose.Image = iconService?.GetIcon("dismiss", _themeService.CurrentTheme, 14);
                _btnClose.ImageAlign = ContentAlignment.MiddleLeft; _btnClose.TextImageRelation = TextImageRelation.ImageBeforeText;
                _btnCloseThemeChangedHandler = (s, t) =>
                {
                    try
                    {
                        if (_dispatcherHelper != null)
                        {
                            _ = _dispatcherHelper.InvokeAsync(() => _btnClose.Image = iconService?.GetIcon("dismiss", t, 14));
                        }
                        else if (_btnClose.InvokeRequired)
                        {
                            _btnClose.Invoke(() => _btnClose.Image = iconService?.GetIcon("dismiss", t, 14));
                        }
                        else
                        {
                            _btnClose.Image = iconService?.GetIcon("dismiss", t, 14);
                        }
                    }
                    catch { }
                };
                _themeService.ThemeChanged += _btnCloseThemeChangedHandler;
            }
            catch { }

            _mainPanel.Controls.Add(_btnClose);
            Controls.Add(_mainPanel);

            // Bindings
            try
            {
                var settingsBinding = new BindingSource { DataSource = _vm };
                if (_txtAppTitle != null) _txtAppTitle.DataBindings.Add("Text", settingsBinding, "AppTitle", true, DataSourceUpdateMode.OnPropertyChanged);
            }
            catch { }
        }

        private void ApplyCurrentTheme()
        {
            ThemeManager.ApplyTheme(this);
            var colors = ThemeManager.Colors;
            if (_themeGroup != null) { _themeGroup.ForeColor = colors.TextPrimary; _themeGroup.BackColor = colors.Surface; }
            if (_aboutGroup != null) { _aboutGroup.ForeColor = colors.TextPrimary; _aboutGroup.BackColor = colors.Surface; }
            try { Syncfusion.WinForms.Core.SfSkinManager.SetVisualStyle(this, ThemeManager.GetSyncfusionThemeName()); } catch { }
        }

        private async Task LoadViewDataAsync()
        {
            try { Serilog.Log.Debug("SettingsPanel: LoadViewDataAsync starting"); if (_vm != null) { await _vm.LoadSettingsAsync(); Serilog.Log.Information("SettingsPanel: settings loaded successfully"); } }
            catch (Exception ex) { Serilog.Log.Warning(ex, "SettingsPanel: LoadViewDataAsync failed"); }
        }

        private void OnThemeChanged(object? sender, AppTheme theme)
        {
            if (_dispatcherHelper != null && !_dispatcherHelper.CheckAccess())
            {
                try { _ = _dispatcherHelper.InvokeAsync(() => OnThemeChanged(sender, theme)); } catch { }
                return;
            }

            if (InvokeRequired) { Invoke(() => OnThemeChanged(sender, theme)); return; }
            ApplyCurrentTheme();
        }

        private void BtnClose_Click(object? sender, EventArgs e)
        {
            try
            {
                var parentForm = this.FindForm();
                if (parentForm is WileyWidget.WinForms.Forms.MainForm mainForm) { mainForm.CloseSettingsPanel(); return; }

                if (parentForm != null)
                {
                    var dockingManagerField = parentForm.GetType().GetField("_dockingManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (dockingManagerField?.GetValue(parentForm) is Syncfusion.Windows.Forms.Tools.DockingManager dm) { dm.SetDockVisibility(this, false); return; }
                }

                this.Parent?.Controls.Remove(this);
            }
            catch (Exception ex) { Serilog.Log.Warning(ex, "SettingsPanel: BtnClose_Click failed"); }
        }

        private void TxtAppTitle_Validating(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_txtAppTitle == null || string.IsNullOrWhiteSpace(_txtAppTitle.Text)) { try { _error_provider?.SetError(_txtAppTitle, "Application title cannot be empty."); } catch { } e.Cancel = true; }
            else { try { _error_provider?.SetError(_txtAppTitle, ""); } catch { } }
        }

        private void TxtAppTitle_Validated(object? sender, EventArgs e) { try { _error_provider?.SetError(_txtAppTitle, ""); } catch { } }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { if (_panelThemeChangedHandler != null) _themeService.ThemeChanged -= _panelThemeChangedHandler; } catch { }
                try { if (_btnCloseThemeChangedHandler != null) _themeService.ThemeChanged -= _btnCloseThemeChangedHandler; } catch { }

                try { _themeCombo.SafeClearDataSource(); } catch { }
                try { _themeCombo.SafeDispose(); } catch { }
                try { _mainPanel?.Dispose(); } catch { }
                try { _themeGroup?.Dispose(); } catch { }
                try { _aboutGroup?.Dispose(); } catch { }
                try { _btnClose?.Dispose(); } catch { }
                try { _txtAppTitle?.Dispose(); } catch { }
                try { _error_provider?.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }
}
