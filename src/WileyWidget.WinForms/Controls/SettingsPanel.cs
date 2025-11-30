using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using WileyWidget.WinForms.Theming;
using WileyWidget.WinForms.ViewModels;

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
        public const string ApplyButton = "Apply";
        public const string SettingsSavedTitle = "Settings Saved";
        public const string SettingsSavedMessage = "Theme settings saved successfully.\n\nSome Syncfusion controls may require an application restart to fully update.";
    }

    /// <summary>
    /// Settings panel (UserControl) for application configuration.
    /// Designed for embedding in DockingManager.
    /// </summary>
    [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
    public partial class SettingsPanel : UserControl
    {
        public object? DataContext { get; private set; }
        private readonly SettingsViewModel _vm;

        // Controls
        private Panel? _mainPanel;
        private GroupBox? _themeGroup;
        private Syncfusion.WinForms.Controls.SfButton? _rbDark;
        private Syncfusion.WinForms.Controls.SfButton? _rbLight;
        private GroupBox? _aboutGroup;
        private Label? _lblVersion;
        private Label? _lblDbStatus;
        private Syncfusion.WinForms.Controls.SfButton? _btnApply;
        private CommunityToolkit.Mvvm.Input.IRelayCommand? _applyCommand;
        private Syncfusion.WinForms.Controls.SfButton? _btnClose;
        private EventHandler<AppTheme>? _btnApplyThemeChangedHandler;
        private TextBox? _txtAppTitle;
        private ErrorProvider? _errorProvider;
        private EventHandler<AppTheme>? _btnCloseThemeChangedHandler;
        private Syncfusion.WinForms.ListView.SfComboBox? _themeCombo;
        private EventHandler<AppTheme>? _panelThemeChangedHandler;
        private CheckBox? _chkOpenEditFormsDocked;

        public SettingsPanel() : this(Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<SettingsViewModel>(Program.Services))
        {
        }

        public SettingsPanel(SettingsViewModel vm)
        {
            _vm = vm;
            DataContext = vm;
            InitializeComponent();
            SetupUI();
            ApplyCurrentTheme();

            // Subscribe to theme changes
            _panelThemeChangedHandler = OnThemeChanged;
            ThemeManager.ThemeChanged += _panelThemeChangedHandler;

            // Start an async load for settings
#pragma warning disable CS4014
            _ = LoadViewDataAsync();
#pragma warning restore CS4014
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

            // Main panel
            _mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(padding)
            };

            // ErrorProvider for validation
            try { _errorProvider = new ErrorProvider() { BlinkStyle = ErrorBlinkStyle.NeverBlink }; } catch { }

            // App Title input
            var lblAppTitle = new Label
            {
                Text = SettingsPanelResources.AppTitleLabel,
                AutoSize = true,
                Location = new Point(padding, y + 4),
                Font = new Font("Segoe UI", 9, FontStyle.Regular)
            };

            _txtAppTitle = new TextBox
            {
                Name = "txtAppTitle",
                Location = new Point(padding + 120, y),
                Width = 300,
                MaxLength = 100, // Per demos: set max length
                Font = new Font("Segoe UI", 10F), // Per demos: consistent font
                AccessibleName = "Application Title",
                AccessibleDescription = "Set the friendly application title"
            };
            // Add tooltip for better UX
            var txtToolTip = new ToolTip();
            txtToolTip.SetToolTip(_txtAppTitle, "Enter a custom title for the application window");
            _txtAppTitle.Validating += TxtAppTitle_Validating;
            _txtAppTitle.Validated += TxtAppTitle_Validated;

            _mainPanel.Controls.Add(lblAppTitle);
            _mainPanel.Controls.Add(_txtAppTitle);
            y += 40;

            // Theme group
            _themeGroup = new GroupBox
            {
                Text = SettingsPanelResources.AppearanceGroup,
                Location = new Point(padding, y),
                Size = new Size(440, 100),
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };

            _rbDark = new Syncfusion.WinForms.Controls.SfButton
            {
                Name = "btnDarkTheme",
                Text = SettingsPanelResources.DarkThemeLabel,
                Size = new Size(180, 25),
                Location = new Point(20, 30),
                AccessibleName = "Dark theme",
                AccessibleDescription = "Select dark theme for the application"
            };
            // Add tooltip for better UX
            var darkToolTip = new ToolTip();
            darkToolTip.SetToolTip(_rbDark, "Apply Fluent Dark theme with darker colors");
            if (ThemeManager.CurrentTheme == AppTheme.Dark)
            {
                var colors = ThemeManager.Colors;
                _rbDark.BackColor = colors.Accent;
                _rbDark.ForeColor = colors.TextOnAccent;
            }
            _rbDark.Click += (s, e) =>
            {
                _vm.SelectedTheme = AppTheme.Dark;
                OnThemeButtonClicked(AppTheme.Dark);
            };

            _rbLight = new Syncfusion.WinForms.Controls.SfButton
            {
                Name = "btnLightTheme",
                Text = SettingsPanelResources.LightThemeLabel,
                Size = new Size(180, 25),
                Location = new Point(220, 30),
                AccessibleName = "Light theme",
                AccessibleDescription = "Select light theme for the application"
            };
            // Add tooltip for better UX
            var lightToolTip = new ToolTip();
            lightToolTip.SetToolTip(_rbLight, "Apply Fluent Light theme with brighter colors");
            if (ThemeManager.CurrentTheme == AppTheme.Light)
            {
                var colors = ThemeManager.Colors;
                _rbLight.BackColor = colors.Accent;
                _rbLight.ForeColor = colors.TextOnAccent;
            }
            _rbLight.Click += (s, e) =>
            {
                _vm.SelectedTheme = AppTheme.Light;
                OnThemeButtonClicked(AppTheme.Light);
            };

            _themeGroup.Controls.Add(_rbDark);
            _themeGroup.Controls.Add(_rbLight);

            // Theme combo - configured per Syncfusion SfComboBox demos
            _themeCombo = new Syncfusion.WinForms.ListView.SfComboBox
            {
                Name = "themeCombo",
                Location = new Point(20, 60),
                Size = new Size(380, 24),
                DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList, // Per demos: prevent editing
                AllowDropDownResize = false, // Per demos: prevent dropdown resize
                MaxDropDownItems = 5, // Per demos: limit dropdown height
                AccessibleName = "Theme selection",
                AccessibleDescription = "Select application theme"
            };
            // Per demos: configure DropDownListView styling
            _themeCombo.DropDownListView.Style.ItemStyle.Font = new Font("Segoe UI", 10F);
            try { _themeCombo.DataSource = _vm?.Themes ?? Enum.GetValues<AppTheme>().Cast<object>().ToList(); } catch { }
            _themeGroup.Controls.Add(_themeCombo);
            y += 120;

            // Behavior settings - checkbox for docked edit forms
            _chkOpenEditFormsDocked = new CheckBox
            {
                Text = "Open edit forms docked (as floating tool windows)",
                AutoSize = true,
                Location = new Point(padding, y),
                Checked = _vm?.OpenEditFormsDocked ?? false,
                Font = new Font("Segoe UI", 9, FontStyle.Regular),
                AccessibleName = "Open edit forms docked",
                AccessibleDescription = "When checked, account edit forms will open as dockable floating tool windows"
            };
            var dockedToolTip = new ToolTip();
            dockedToolTip.SetToolTip(_chkOpenEditFormsDocked, "Open account edit forms as dockable floating windows instead of modal dialogs");
            _chkOpenEditFormsDocked.CheckedChanged += (s, e) =>
            {
                if (_vm != null)
                    _vm.OpenEditFormsDocked = _chkOpenEditFormsDocked.Checked;
            };
            _mainPanel.Controls.Add(_chkOpenEditFormsDocked);
            y += 35;

            // About group
            _aboutGroup = new GroupBox
            {
                Text = SettingsPanelResources.AboutGroup,
                Location = new Point(padding, y),
                Size = new Size(440, 120),
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };

            _lblVersion = new Label
            {
                Text = $"Wiley Widget v1.0.0\n.NET {Environment.Version}\nRuntime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}",
                Location = new Point(20, 30),
                AutoSize = true,
                Font = new Font("Segoe UI", 9, FontStyle.Regular)
            };

            _lblDbStatus = new Label
            {
                Text = "Database: Connected",
                Location = new Point(20, 85),
                AutoSize = true,
                Font = new Font("Segoe UI", 9, FontStyle.Regular)
            };

            _aboutGroup.Controls.Add(_lblVersion);
            _aboutGroup.Controls.Add(_lblDbStatus);
            y += 140;

            // Buttons
            _btnApply = new Syncfusion.WinForms.Controls.SfButton
            {
                Name = "btnApply",
                Text = SettingsPanelResources.ApplyButton,
                Size = new Size(100, 35),
                Location = new Point(240, y),
                AccessibleName = "Apply settings",
                AccessibleDescription = "Apply settings and persist preferences"
            };
            // Add tooltip for better UX
            var applyToolTip = new ToolTip();
            applyToolTip.SetToolTip(_btnApply, "Save and apply all settings changes");
            // Add icon from theme icon service
            try
            {
                var iconService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Services.IThemeIconService>(Program.Services);
                var theme = WileyWidget.WinForms.Theming.ThemeManager.CurrentTheme;
                _btnApply.Image = iconService?.GetIcon("save", theme, 14);
                _btnApply.ImageAlign = ContentAlignment.MiddleLeft;
                _btnApply.TextImageRelation = TextImageRelation.ImageBeforeText;
                _btnApplyThemeChangedHandler = (s, t) =>
                {
                    try
                    {
                        if (_btnApply.InvokeRequired)
                        {
                            _btnApply.Invoke(() => _btnApply.Image = iconService?.GetIcon("save", t, 14));
                        }
                        else
                        {
                            _btnApply.Image = iconService?.GetIcon("save", t, 14);
                        }
                    }
                    catch { }
                };
                WileyWidget.WinForms.Theming.ThemeManager.ThemeChanged += _btnApplyThemeChangedHandler;
            }
            catch { }
            _applyCommand = _vm.ApplyCommand;
            _btnApply.Click += (s, e) =>
            {
                try
                {
                    _applyCommand?.Execute(null);
                    MessageBox.Show(SettingsPanelResources.SettingsSavedMessage,
                        SettingsPanelResources.SettingsSavedTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "Failed to apply settings");
                    MessageBox.Show($"Failed to apply settings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            _btnClose = new Syncfusion.WinForms.Controls.SfButton
            {
                Name = "btnClose",
                Text = "Close",
                Size = new Size(100, 35),
                Location = new Point(350, y),
                AccessibleName = "Close settings",
                AccessibleDescription = "Close the settings panel"
            };
            // Add tooltip for better UX
            var closeToolTip = new ToolTip();
            closeToolTip.SetToolTip(_btnClose, "Close this settings panel (Esc)");
            // Wire up close button click handler per Syncfusion demos
            _btnClose.Click += BtnClose_Click;
            // Add icon from theme icon service
            try
            {
                var iconService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Services.IThemeIconService>(Program.Services);
                var theme = WileyWidget.WinForms.Theming.ThemeManager.CurrentTheme;
                _btnClose.Image = iconService?.GetIcon("dismiss", theme, 14);
                _btnClose.ImageAlign = ContentAlignment.MiddleLeft;
                _btnClose.TextImageRelation = TextImageRelation.ImageBeforeText;
                _btnCloseThemeChangedHandler = (s, t) =>
                {
                    try
                    {
                        if (_btnClose.InvokeRequired)
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
                WileyWidget.WinForms.Theming.ThemeManager.ThemeChanged += _btnCloseThemeChangedHandler;
            }
            catch { }

            _mainPanel.Controls.Add(_themeGroup);
            _mainPanel.Controls.Add(_aboutGroup);
            _mainPanel.Controls.Add(_btnApply);
            _mainPanel.Controls.Add(_btnClose);

            Controls.Add(_mainPanel);

            // Two-way binding
            try
            {
                var settingsBinding = new BindingSource { DataSource = _vm };
                if (_txtAppTitle != null) _txtAppTitle.DataBindings.Add("Text", settingsBinding, "AppTitle", true, DataSourceUpdateMode.OnPropertyChanged);

                if (_vm.SelectedTheme == AppTheme.Dark)
                {
                    var colors = ThemeManager.Colors;
                    _rbDark.BackColor = colors.Accent;
                    _rbDark.ForeColor = colors.TextOnAccent;
                    _rbLight.BackColor = colors.Surface;
                    _rbLight.ForeColor = colors.TextPrimary;
                }
            }
            catch { }
        }

        private void ApplyCurrentTheme()
        {
            ThemeManager.ApplyTheme(this);

            var colors = ThemeManager.Colors;
            if (_themeGroup != null)
            {
                _themeGroup.ForeColor = colors.TextPrimary;
                _themeGroup.BackColor = colors.Surface;
            }
            if (_aboutGroup != null)
            {
                _aboutGroup.ForeColor = colors.TextPrimary;
                _aboutGroup.BackColor = colors.Surface;
            }
            try { Syncfusion.WinForms.Core.SfSkinManager.SetVisualStyle(this, ThemeManager.GetSyncfusionThemeName()); } catch { }
        }

        private async Task LoadViewDataAsync()
        {
            try
            {
                Serilog.Log.Debug("SettingsPanel: LoadViewDataAsync starting");
                if (_vm != null)
                {
                    await _vm.LoadSettingsAsync();
                    Serilog.Log.Information("SettingsPanel: settings loaded successfully");
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "SettingsPanel: LoadViewDataAsync failed");
            }
        }

        private void OnThemeButtonClicked(AppTheme theme)
        {
            if (ThemeManager.CurrentTheme != theme)
            {
                ThemeManager.SetTheme(theme);
                Serilog.Log.Information("SettingsPanel: live preview applied theme {Theme}", theme);

                if (_rbDark != null)
                {
                    var colors = ThemeManager.Colors;
                    if (theme == AppTheme.Dark)
                    {
                        _rbDark.BackColor = colors.Accent;
                        _rbDark.ForeColor = colors.TextOnAccent;
                    }
                    else
                    {
                        _rbDark.BackColor = colors.Surface;
                        _rbDark.ForeColor = colors.TextPrimary;
                    }
                }

                if (_rbLight != null)
                {
                    var colors = ThemeManager.Colors;
                    if (theme == AppTheme.Light)
                    {
                        _rbLight.BackColor = colors.Accent;
                        _rbLight.ForeColor = colors.TextOnAccent;
                    }
                    else
                    {
                        _rbLight.BackColor = colors.Surface;
                        _rbLight.ForeColor = colors.TextPrimary;
                    }
                }
            }
        }

        private void OnThemeChanged(object? sender, AppTheme theme)
        {
            if (InvokeRequired)
            {
                Invoke(() => OnThemeChanged(sender, theme));
                return;
            }

            ApplyCurrentTheme();
        }

        /// <summary>
        /// Handles close button click - hides the panel via MainForm.CloseSettingsPanel().
        /// Per Syncfusion demos pattern for panel closing.
        /// </summary>
        private void BtnClose_Click(object? sender, EventArgs e)
        {
            try
            {
                // Use the public method on MainForm instead of reflection
                var parentForm = this.FindForm();
                if (parentForm is WileyWidget.WinForms.Forms.MainForm mainForm)
                {
                    mainForm.CloseSettingsPanel();
                    return;
                }

                // Fallback for other host forms: try to find DockingManager
                if (parentForm != null)
                {
                    var dockingManagerField = parentForm.GetType().GetField("_dockingManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (dockingManagerField?.GetValue(parentForm) is Syncfusion.Windows.Forms.Tools.DockingManager dm)
                    {
                        dm.SetDockVisibility(this, false);
                        return;
                    }
                }

                // Last resort: remove from parent controls
                this.Parent?.Controls.Remove(this);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "SettingsPanel: BtnClose_Click failed");
            }
        }

        private void TxtAppTitle_Validating(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_txtAppTitle == null || string.IsNullOrWhiteSpace(_txtAppTitle.Text))
            {
                try { _errorProvider?.SetError(_txtAppTitle, "Application title cannot be empty."); } catch { }
                e.Cancel = true;
            }
            else
            {
                try { _errorProvider?.SetError(_txtAppTitle, ""); } catch { }
            }
        }

        private void TxtAppTitle_Validated(object? sender, EventArgs e)
        {
            try { _errorProvider?.SetError(_txtAppTitle, ""); } catch { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { if (_panelThemeChangedHandler != null) ThemeManager.ThemeChanged -= _panelThemeChangedHandler; } catch { }
                try { WileyWidget.WinForms.Theming.ThemeManager.ThemeChanged -= _btnApplyThemeChangedHandler; } catch { }
                try { WileyWidget.WinForms.Theming.ThemeManager.ThemeChanged -= _btnCloseThemeChangedHandler; } catch { }

                // Clear DataSource before disposing Syncfusion controls
                try
                {
                    if (_themeCombo != null && !_themeCombo.IsDisposed)
                    {
                        try { _themeCombo.DataSource = null; } catch { }
                        _themeCombo.Dispose();
                    }
                }
                catch (NullReferenceException) { /* Syncfusion bug */ }
                catch (ObjectDisposedException) { }
                catch { }

                try { _mainPanel?.Dispose(); } catch { }
                try { _themeGroup?.Dispose(); } catch { }
                try { _rbDark?.Dispose(); } catch { }
                try { _rbLight?.Dispose(); } catch { }
                try { _aboutGroup?.Dispose(); } catch { }
                try { _lblVersion?.Dispose(); } catch { }
                try { _lblDbStatus?.Dispose(); } catch { }
                try { _btnApply?.Dispose(); } catch { }
                try { _btnClose?.Dispose(); } catch { }
                try { _txtAppTitle?.Dispose(); } catch { }
                try { _errorProvider?.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }
}
