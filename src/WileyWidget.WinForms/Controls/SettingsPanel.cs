using System.Diagnostics.CodeAnalysis;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Theming;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Extensions;

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
        public new object? DataContext { get; private set; }

        private readonly SettingsViewModel _vm;
        private readonly IThemeService _themeService;

        // Controls
        private Panel? _mainPanel;
        private GroupBox? _themeGroup;
        private Syncfusion.WinForms.ListView.SfComboBox? _themeCombo;
        private GroupBox? _aboutGroup;
        private GroupBox? _exportGroup;
        private Label? _lblVersion;
        private Label? _lblDbStatus;
        private Syncfusion.WinForms.Controls.SfButton? _btnClose;
        private Syncfusion.WinForms.Controls.SfButton? _btnSave;
        private Syncfusion.WinForms.Controls.SfButton? _btnBrowseExportPath;
        private TextBox? _txtAppTitle;
        private TextBox? _txtExportPath;
        private Syncfusion.WinForms.Input.SfNumericTextBox? _numAutoSaveInterval;
        private Syncfusion.WinForms.ListView.SfComboBox? _comboLogLevel;
        private Syncfusion.WinForms.ListView.SfComboBox? _comboDateFormat;
        private Syncfusion.WinForms.ListView.SfComboBox? _cmbLogLevel;
        private TextBox? _txtDateFormat;
        private TextBox? _txtCurrencyFormat;
        private CheckBox? _chkAutoSave;
        private CheckBox? _chkEnableLogging;
        private CheckBox? _chkUseDemoData;
        private ToolTip? _demoDataToolTip;
        private ErrorProvider? _error_provider;
        // ToolTips and binding source hold IDisposable instances — keep them as fields so we dispose them
        private ToolTip? _txtToolTip;
        private ToolTip? _dockedToolTip;
        private ToolTip? _closeToolTip;
        private ToolTip? _exportPathToolTip;
        private ToolTip? _autoSaveToolTip;
        private ToolTip? _logLevelToolTip;
        private BindingSource? _settingsBinding;
        private EventHandler<AppTheme>? _panelThemeChangedHandler;
        private EventHandler<AppTheme>? _btnCloseThemeChangedHandler;
        private CheckBox? _chkOpenEditFormsDocked;
        private ErrorProviderBinding? _errorBinding;
        private EventHandler? _browseExportPathHandler;

        /// <summary>
        /// Parameterless constructor for DI/designer support.
        /// Guards against null Program.Services.
        /// </summary>
        public SettingsPanel() : this(ResolveSettingsViewModel(), ResolveThemeService())
        {
        }

        private static SettingsViewModel ResolveSettingsViewModel()
        {
            if (Program.Services == null)
            {
                Serilog.Log.Error("SettingsPanel: Program.Services is null - cannot resolve SettingsViewModel");
                throw new InvalidOperationException("SettingsPanel requires DI services to be initialized. Ensure Program.Services is set before creating SettingsPanel.");
            }
            try
            {
                var vm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<SettingsViewModel>(Program.Services);
                Serilog.Log.Debug("SettingsPanel: SettingsViewModel resolved from DI container");
                return vm;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "SettingsPanel: Failed to resolve SettingsViewModel from DI");
                throw;
            }
        }

        private static IThemeService ResolveThemeService()
        {
            if (Program.Services == null)
            {
                Serilog.Log.Error("SettingsPanel: Program.Services is null - cannot resolve IThemeService");
                throw new InvalidOperationException("SettingsPanel requires DI services to be initialized. Ensure Program.Services is set before creating SettingsPanel.");
            }
            try
            {
                var service = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(Program.Services);
                Serilog.Log.Debug("SettingsPanel: IThemeService resolved from DI container");
                return service;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "SettingsPanel: Failed to resolve IThemeService from DI");
                throw;
            }
        }

        public SettingsPanel(SettingsViewModel vm, IThemeService themeService)
        {
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
            _txtToolTip = new ToolTip();
            _txtToolTip.SetToolTip(_txtAppTitle, "Enter a custom title for the application window");
            // Validation is now handled via ErrorProviderBinding to ViewModel
            _mainPanel.Controls.Add(lblAppTitle);
            _mainPanel.Controls.Add(_txtAppTitle);
            y += 40;

            // Appearance group
            _themeGroup = new GroupBox { Text = SettingsPanelResources.AppearanceGroup, Location = new Point(padding, y), Size = new Size(440, 100), Font = new Font("Segoe UI", 10, FontStyle.Bold) };

            _themeCombo = new Syncfusion.WinForms.ListView.SfComboBox { Name = "themeCombo", Location = new Point(20, 30), Size = new Size(380, 24), DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList, AllowDropDownResize = false, MaxDropDownItems = 5, AccessibleName = "Theme selection", AccessibleDescription = "Select application theme" };
            _themeCombo.DropDownListView.Style.ItemStyle.Font = new Font("Segoe UI", 10F);
            try { _themeCombo.DataSource = _vm?.Themes?.Cast<object>().ToList() ?? Enum.GetValues<AppTheme>().Cast<object>().ToList(); } catch { }
            try { _themeCombo.SelectedItem = _themeService.Preference; } catch { }
            _themeCombo.SelectedIndexChanged += (s, e) => { try { if (_themeCombo.SelectedItem is AppTheme sel) _themeService.Preference = sel; } catch { } };

            _themeGroup.Controls.Add(_themeCombo);
            _mainPanel.Controls.Add(_themeGroup);
            y += 120;

            // Behavior settings
            _chkOpenEditFormsDocked = new CheckBox { Text = "Open edit forms docked (as floating tool windows)", AutoSize = true, Location = new Point(padding, y), Checked = _vm?.OpenEditFormsDocked ?? false, Font = new Font("Segoe UI", 9, FontStyle.Regular), AccessibleName = "Open edit forms docked", AccessibleDescription = "Open account edit forms as dockable floating windows" };
            _dockedToolTip = new ToolTip(); _dockedToolTip.SetToolTip(_chkOpenEditFormsDocked, "Open account edit forms as dockable floating windows instead of modal dialogs"); _chkOpenEditFormsDocked.CheckedChanged += (s, e) => { if (_vm != null) _vm.OpenEditFormsDocked = _chkOpenEditFormsDocked.Checked; };
            _mainPanel.Controls.Add(_chkOpenEditFormsDocked);
            y += 30;

            // Demo mode toggle
            _chkUseDemoData = new CheckBox { Text = "Use demo/sample data (for demonstrations)", AutoSize = true, Location = new Point(padding, y), Checked = _vm?.UseDemoData ?? false, Font = new Font("Segoe UI", 9, FontStyle.Regular), ForeColor = Color.DarkOrange, AccessibleName = "Use demo data", AccessibleDescription = "When enabled, views display sample data instead of database data" };
            _demoDataToolTip = new ToolTip(); _demoDataToolTip.SetToolTip(_chkUseDemoData, "Enable demo mode to display sample data instead of real database data. Useful for demonstrations or when database is unavailable.");
            _chkUseDemoData.CheckedChanged += (s, e) => { if (_vm != null) _vm.UseDemoData = _chkUseDemoData.Checked; };
            _mainPanel.Controls.Add(_chkUseDemoData);
            y += 35;

            // Data Export group
            var exportGroup = new GroupBox { Text = "Data Export", Location = new Point(padding, y), Size = new Size(440, 70), Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            var lblExportPath = new Label { Text = "Export Path:", AutoSize = true, Location = new Point(20, 30), Font = new Font("Segoe UI", 9, FontStyle.Regular) };
            _txtExportPath = new TextBox { Name = "txtExportPath", Location = new Point(100, 27), Width = 250, Font = new Font("Segoe UI", 10F), AccessibleName = "Export path", AccessibleDescription = "Directory for data exports" };
            _exportPathToolTip = new ToolTip(); _exportPathToolTip.SetToolTip(_txtExportPath, "Directory where exported data files will be saved");
            _btnBrowseExportPath = new Syncfusion.WinForms.Controls.SfButton { Name = "btnBrowseExportPath", Text = "...", Size = new Size(40, 24), Location = new Point(360, 26), AccessibleName = "Browse for export path", AccessibleDescription = "Open folder browser to select export directory" };
            _browseExportPathHandler = (s, e) => OnBrowseExportPath();
            try { if (_vm != null) _vm.BrowseExportPathRequested += _browseExportPathHandler; } catch { }
            _btnBrowseExportPath.Click += (s, e) => { try { _vm?.BrowseExportPathCommand?.Execute(null); } catch { } };
            exportGroup.Controls.Add(lblExportPath); exportGroup.Controls.Add(_txtExportPath); exportGroup.Controls.Add(_btnBrowseExportPath);
            _mainPanel.Controls.Add(exportGroup);
            y += 85;

            // Auto-save and Logging group
            var behaviorGroup = new GroupBox { Text = "Behavior & Logging", Location = new Point(padding, y), Size = new Size(440, 110), Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            var lblAutoSave = new Label { Text = "Auto-save interval (min):", AutoSize = true, Location = new Point(20, 30), Font = new Font("Segoe UI", 9, FontStyle.Regular) };
            _numAutoSaveInterval = new Syncfusion.WinForms.Input.SfNumericTextBox { Name = "numAutoSaveInterval", Location = new Point(170, 27), Size = new Size(80, 24), MinValue = 1, MaxValue = 60, Value = _vm?.AutoSaveIntervalMinutes ?? 5, AccessibleName = "Auto-save interval", AccessibleDescription = "Interval in minutes for auto-saving" };
            _autoSaveToolTip = new ToolTip(); _autoSaveToolTip.SetToolTip(_numAutoSaveInterval, "How often data is auto-saved (1-60 minutes)");
            _numAutoSaveInterval.ValueChanged += (s, e) => { try { if (_vm != null && _numAutoSaveInterval.Value.HasValue) _vm.AutoSaveIntervalMinutes = (int)_numAutoSaveInterval.Value.Value; } catch { } };
            behaviorGroup.Controls.Add(lblAutoSave); behaviorGroup.Controls.Add(_numAutoSaveInterval);

            var lblLogLevel = new Label { Text = "Log Level:", AutoSize = true, Location = new Point(20, 65), Font = new Font("Segoe UI", 9, FontStyle.Regular) };
            _cmbLogLevel = new Syncfusion.WinForms.ListView.SfComboBox { Name = "cmbLogLevel", Location = new Point(100, 62), Size = new Size(150, 24), DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList, AccessibleName = "Log level", AccessibleDescription = "Select application logging verbosity" };
            _logLevelToolTip = new ToolTip(); _logLevelToolTip.SetToolTip(_cmbLogLevel, "Verbosity level for application logging");
            try { _cmbLogLevel.DataSource = new[] { "Verbose", "Debug", "Information", "Warning", "Error", "Fatal" }.ToList(); _cmbLogLevel.SelectedItem = _vm?.LogLevel ?? "Information"; } catch { }
            _cmbLogLevel.SelectedIndexChanged += (s, e) => { try { if (_vm != null && _cmbLogLevel.SelectedItem is string level) _vm.LogLevel = level; } catch { } };
            behaviorGroup.Controls.Add(lblLogLevel); behaviorGroup.Controls.Add(_cmbLogLevel);
            _mainPanel.Controls.Add(behaviorGroup);
            y += 125;

            // Format settings group
            var formatGroup = new GroupBox { Text = "Display Formats", Location = new Point(padding, y), Size = new Size(440, 80), Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            var lblDateFormat = new Label { Text = "Date Format:", AutoSize = true, Location = new Point(20, 30), Font = new Font("Segoe UI", 9, FontStyle.Regular) };
            _txtDateFormat = new TextBox { Name = "txtDateFormat", Location = new Point(110, 27), Width = 120, Font = new Font("Segoe UI", 10F), Text = _vm?.DateFormat ?? "yyyy-MM-dd", AccessibleName = "Date format", AccessibleDescription = "Format string for displaying dates" };
            _txtDateFormat.TextChanged += (s, e) => { try { if (_vm != null) _vm.DateFormat = _txtDateFormat.Text; } catch { } };
            formatGroup.Controls.Add(lblDateFormat); formatGroup.Controls.Add(_txtDateFormat);

            var lblCurrencyFormat = new Label { Text = "Currency Format:", AutoSize = true, Location = new Point(250, 30), Font = new Font("Segoe UI", 9, FontStyle.Regular) };
            _txtCurrencyFormat = new TextBox { Name = "txtCurrencyFormat", Location = new Point(360, 27), Width = 60, Font = new Font("Segoe UI", 10F), Text = _vm?.CurrencyFormat ?? "C2", AccessibleName = "Currency format", AccessibleDescription = "Format string for displaying currency values" };
            _txtCurrencyFormat.TextChanged += (s, e) => { try { if (_vm != null) _vm.CurrencyFormat = _txtCurrencyFormat.Text; } catch { } };
            formatGroup.Controls.Add(lblCurrencyFormat); formatGroup.Controls.Add(_txtCurrencyFormat);
            _mainPanel.Controls.Add(formatGroup);
            y += 95;

            // About
            _aboutGroup = new GroupBox { Text = SettingsPanelResources.AboutGroup, Location = new Point(padding, y), Size = new Size(440, 120), Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            _lblVersion = new Label { Text = $"Wiley Widget v1.0.0\n.NET {Environment.Version}\nRuntime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}", Location = new Point(20, 30), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Regular) };
            _lblDbStatus = new Label { Text = "Database: Connected", Location = new Point(20, 85), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Regular) };
            _aboutGroup.Controls.Add(_lblVersion); _aboutGroup.Controls.Add(_lblDbStatus); _mainPanel.Controls.Add(_aboutGroup); y += 140;

            // Close button
            _btnClose = new Syncfusion.WinForms.Controls.SfButton { Name = "btnClose", Text = "Close", Size = new Size(100, 35), Location = new Point(350, y), AccessibleName = "Close settings", AccessibleDescription = "Close the settings panel" };
            _closeToolTip = new ToolTip(); _closeToolTip.SetToolTip(_btnClose, "Close this settings panel (Esc)"); _btnClose.Click += BtnClose_Click;
            try
            {
                var iconService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IThemeIconService>(Program.Services);
                _btnClose.Image = iconService?.GetIcon("dismiss", _themeService.CurrentTheme, 14);
                _btnClose.ImageAlign = ContentAlignment.MiddleLeft; _btnClose.TextImageRelation = TextImageRelation.ImageBeforeText;
                _btnCloseThemeChangedHandler = (s, t) => { try { if (_btnClose.InvokeRequired) _btnClose.Invoke(() => _btnClose.Image = iconService?.GetIcon("dismiss", t, 14)); else _btnClose.Image = iconService?.GetIcon("dismiss", t, 14); } catch { } };
                _themeService.ThemeChanged += _btnCloseThemeChangedHandler;
            }
            catch { }

            _mainPanel.Controls.Add(_btnClose);
            Controls.Add(_mainPanel);

            // Bindings
            try
            {
                _settingsBinding = new BindingSource { DataSource = _vm };
                _txtAppTitle!.DataBindings.Add("Text", _settingsBinding, "AppTitle", true, DataSourceUpdateMode.OnPropertyChanged);
                _txtExportPath!.DataBindings.Add("Text", _settingsBinding, "DefaultExportPath", true, DataSourceUpdateMode.OnPropertyChanged);
            }
            catch { }

            // Setup ErrorProviderBinding for ViewModel-driven validation
            try
            {
                if (_error_provider != null && _vm != null)
                {
                    _errorBinding = new ErrorProviderBinding(_error_provider, _vm);
                    _errorBinding.MapControl(nameof(_vm.DefaultExportPath), _txtExportPath!);
                    _errorBinding.MapControl(nameof(_vm.DateFormat), _txtDateFormat!);
                    _errorBinding.MapControl(nameof(_vm.CurrencyFormat), _txtCurrencyFormat!);
                    _errorBinding.MapControl(nameof(_vm.LogLevel), _cmbLogLevel!);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "SettingsPanel: Failed to setup ErrorProviderBinding");
            }
        }

        private void OnBrowseExportPath()
        {
            using var folderDialog = new FolderBrowserDialog
            {
                Description = "Select Export Directory",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true,
                InitialDirectory = Directory.Exists(_vm?.DefaultExportPath) ? _vm.DefaultExportPath : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            if (folderDialog.ShowDialog(this) == DialogResult.OK)
            {
                if (_vm != null) _vm.DefaultExportPath = folderDialog.SelectedPath;
            }
        }

        private void ApplyCurrentTheme()
        {
            var parentForm = FindForm();
            if (parentForm != null)
            {
                ThemeManager.ApplyTheme(parentForm);
            }
            var colors = ThemeManager.Colors;
            if (_themeGroup != null) { _themeGroup.ForeColor = colors.TextPrimary; _themeGroup.BackColor = colors.Surface; }
            if (_aboutGroup != null) { _aboutGroup.ForeColor = colors.TextPrimary; _aboutGroup.BackColor = colors.Surface; }
            // Syncfusion per-form skinning is handled by ThemeManager.ApplyTheme(parentForm) above
        }

        private async Task LoadViewDataAsync()
        {
            try { Serilog.Log.Debug("SettingsPanel: LoadViewDataAsync starting"); if (_vm != null) { await (_vm.LoadCommand?.ExecuteAsync(null) ?? Task.CompletedTask); Serilog.Log.Information("SettingsPanel: settings loaded successfully"); } }
            catch (Exception ex) { Serilog.Log.Warning(ex, "SettingsPanel: LoadViewDataAsync failed"); }
        }

        private void OnThemeChanged(object? sender, AppTheme theme)
        {
            if (InvokeRequired) { Invoke(() => OnThemeChanged(sender, theme)); return; }
            ApplyCurrentTheme();
        }

        private void BtnClose_Click(object? sender, EventArgs e)
        {
            try
            {
                // Validate settings before closing if there are unsaved changes
                if (_vm.HasUnsavedChanges && !_vm.ValidateSettings())
                {
                    var errors = _vm.GetValidationSummary();
                    if (errors.Count > 0)
                    {
                        MessageBox.Show(
                            $"Please correct the following errors before closing:\n\n{string.Join("\n", errors)}",
                            "Validation Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        return;
                    }
                }

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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { if (_panelThemeChangedHandler != null) _themeService.ThemeChanged -= _panelThemeChangedHandler; } catch { }
                try { if (_btnCloseThemeChangedHandler != null) _themeService.ThemeChanged -= _btnCloseThemeChangedHandler; } catch { }
                try { if (_browseExportPathHandler != null) _vm.BrowseExportPathRequested -= _browseExportPathHandler; } catch { }

                try { if (_themeCombo != null && !_themeCombo.IsDisposed) { try { _themeCombo.DataSource = null; } catch { } _themeCombo.Dispose(); } } catch { }
                try { if (_cmbLogLevel != null && !_cmbLogLevel.IsDisposed) { try { _cmbLogLevel.DataSource = null; } catch { } _cmbLogLevel.Dispose(); } } catch { }
                try { _mainPanel?.Dispose(); } catch { }
                try { _themeGroup?.Dispose(); } catch { }
                try { _aboutGroup?.Dispose(); } catch { }
                try { _btnClose?.Dispose(); } catch { }
                try { _txtAppTitle?.Dispose(); } catch { }
                try { _chkOpenEditFormsDocked?.Dispose(); } catch { }
                try { _lblVersion?.Dispose(); } catch { }
                try { _lblDbStatus?.Dispose(); } catch { }
                try { _error_provider?.Dispose(); } catch { }
                try { _errorBinding?.Dispose(); } catch { }
                try { _settingsBinding?.Dispose(); } catch { }
                try { _txtToolTip?.Dispose(); } catch { }
                try { _dockedToolTip?.Dispose(); } catch { }
                try { _closeToolTip?.Dispose(); } catch { }
                // Dispose new controls
                try { _txtExportPath?.Dispose(); } catch { }
                try { _btnBrowseExportPath?.Dispose(); } catch { }
                try { _numAutoSaveInterval?.Dispose(); } catch { }
                try { _txtDateFormat?.Dispose(); } catch { }
                try { _txtCurrencyFormat?.Dispose(); } catch { }
                try { _exportPathToolTip?.Dispose(); } catch { }
                try { _autoSaveToolTip?.Dispose(); } catch { }
                try { _logLevelToolTip?.Dispose(); } catch { }
                try { _chkUseDemoData?.Dispose(); } catch { }
                try { _demoDataToolTip?.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }
}
