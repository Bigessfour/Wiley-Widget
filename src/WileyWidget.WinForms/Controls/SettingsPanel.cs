using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Theming;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WileyWidget.WinForms.Extensions;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.Drawing;
using Syncfusion.WinForms.Controls;
using WileyWidget.WinForms.Controls;

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

        // AI settings help
        public const string AiSettingsHelpShort = "AI settings control the xAI integration used for recommendations and explanations. Changes apply to subsequent requests and API keys are stored securely.";
        public const string AiSettingsHelpLong = "This group controls the application's xAI (Grok) integration:\n\n- Enable AI: Turn on/off xAI features. When disabled, the application falls back to rule-based recommendations.\n- API Endpoint: URL used to send requests.\n- Model: Select the AI model. Different models may produce different recommendations and explanations.\n- API Key: Your private API key. It is stored securely and is never written to logs. Changing it takes effect for subsequent requests.\n- Timeout: Maximum seconds to wait for a response.\n- Max Tokens: Maximum response size; higher values allow longer completions and may increase cost.\n- Temperature: Controls randomness; lower values make outputs more deterministic.\n\nNote: Changing these settings only affects future AI requests. Cached recommendations or explanations will remain until they expire or are cleared.";
        public const string AiSettingsLearnMoreLabel = "Learn more...";
        public const string AiSettingsDialogTitle = "AI Settings Help";
    }

    [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
    public partial class SettingsPanel : ScopedPanelBase<SettingsViewModel>
    {
        public new object? DataContext { get; private set; }

        private readonly IThemeService _themeService;
        private readonly Services.IThemeIconService? _iconService;

        // Controls
        private Syncfusion.Windows.Forms.Tools.GradientPanelExt? _mainPanel;
        private GradientPanelExt? _themeGroup;
        private Syncfusion.WinForms.ListView.SfComboBox? _themeCombo;
        private Syncfusion.WinForms.ListView.SfComboBox? _fontCombo;
        private GradientPanelExt? _aboutGroup;
        private Label? _lblVersion;
        private Label? _lblDbStatus;
        private Syncfusion.WinForms.Controls.SfButton? _btnClose;
        private Syncfusion.WinForms.Controls.SfButton? _btnBrowseExportPath;
        private TextBoxExt? _txtAppTitle;
        private TextBoxExt? _txtExportPath;
        private Syncfusion.WinForms.Input.SfNumericTextBox? _numAutoSaveInterval;
        private Syncfusion.WinForms.ListView.SfComboBox? _cmbLogLevel;
        private TextBoxExt? _txtDateFormat;
        private TextBoxExt? _txtCurrencyFormat;
        private CheckBoxAdv? _chkUseDemoData;
        private ToolTip? _demoDataToolTip;
        private ErrorProvider? _error_provider;
        // ToolTips and binding source hold IDisposable instances - keep them as fields so we dispose them
        private ToolTip? _txtToolTip;
        private ToolTip? _dockedToolTip;
        private ToolTip? _closeToolTip;
        private ToolTip? _exportPathToolTip;
        private ToolTip? _autoSaveToolTip;
        private ToolTip? _logLevelToolTip;
        private BindingSource? _settingsBinding;
        private EventHandler<AppTheme>? _panelThemeChangedHandler;
        private EventHandler<AppTheme>? _btnCloseThemeChangedHandler;
        private CheckBoxAdv? _chkOpenEditFormsDocked;
        private ErrorProviderBinding? _errorBinding;
        private EventHandler? _browseExportPathHandler;

        // AI / XAI controls
        private GradientPanelExt? _aiGroup;
        private CheckBoxAdv? _chkEnableAi;
        private TextBoxExt? _txtXaiApiEndpoint;
        private TextBoxExt? _txtXaiApiKey;
        private Syncfusion.WinForms.Controls.SfButton? _btnShowApiKey;
        private Syncfusion.WinForms.ListView.SfComboBox? _cmbXaiModel;
        private Syncfusion.WinForms.Input.SfNumericTextBox? _numXaiTimeout;
        private Syncfusion.WinForms.Input.SfNumericTextBox? _numXaiMaxTokens;
        private Syncfusion.WinForms.Input.SfNumericTextBox? _numXaiTemperature;

        // AI help UI
        private Label? _lblAiHelp;
        private LinkLabel? _lnkAiLearnMore;
        private ToolTip? _aiToolTip;

        /// <summary>
        /// Constructor that accepts required dependencies from DI container.
        /// </summary>
        /// <param name="scopeFactory">Factory for creating service scopes</param>
        /// <param name="logger">Logger for diagnostic logging</param>
        /// <param name="themeService">Theme service for applying themes</param>
        /// <param name="iconService">Optional icon service for theme icons</param>
        public SettingsPanel(
            IServiceScopeFactory scopeFactory,
            ILogger<ScopedPanelBase<SettingsViewModel>> logger,
            IThemeService themeService,
            Services.IThemeIconService? iconService = null)
            : base(scopeFactory, logger)
        {
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            _iconService = iconService;
            InitializeComponent();
        }

        /// <summary>
        /// Parameterless constructor for designer support ONLY.
        /// DO NOT USE in production - use DI constructor instead.
        /// </summary>
        [Obsolete("Use DI constructor with IServiceScopeFactory, ILogger, and IThemeService parameters", error: false)]
        public SettingsPanel() : this(ResolveServiceScopeFactory(), ResolveLogger(), ResolveThemeService(), ResolveIconService())
        {
        }

        private static IServiceScopeFactory ResolveServiceScopeFactory()
        {
            if (Program.Services == null)
            {
                Serilog.Log.Error("SettingsPanel: Program.Services is null - cannot resolve IServiceScopeFactory");
                throw new InvalidOperationException("SettingsPanel requires DI services to be initialized. Ensure Program.Services is set before creating SettingsPanel.");
            }
            try
            {
                var factory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(Program.Services);
                Serilog.Log.Debug("SettingsPanel: IServiceScopeFactory resolved from DI container");
                return factory;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "SettingsPanel: Failed to resolve IServiceScopeFactory from DI");
                throw;
            }
        }

        private static ILogger<ScopedPanelBase<SettingsViewModel>> ResolveLogger()
        {
            if (Program.Services == null)
            {
                Serilog.Log.Error("SettingsPanel: Program.Services is null - cannot resolve ILogger");
                throw new InvalidOperationException("SettingsPanel requires DI services to be initialized. Ensure Program.Services is set before creating SettingsPanel.");
            }
            try
            {
                var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<ScopedPanelBase<SettingsViewModel>>>(Program.Services);
                Serilog.Log.Debug("SettingsPanel: ILogger resolved from DI container");
                return logger;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "SettingsPanel: Failed to resolve ILogger from DI");
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

        private static IThemeIconService? ResolveIconService()
        {
            if (Program.Services == null)
            {
                return null;
            }
            try
            {
                var service = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IThemeIconService>(Program.Services);
                Serilog.Log.Debug("SettingsPanel: IThemeIconService resolved from DI container");
                return service;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "SettingsPanel: Failed to resolve IThemeIconService from DI");
                return null;
            }
        }

        /// <summary>
        /// Called after ViewModel is resolved from scoped provider.
        /// Performs UI setup and initial data binding.
        /// </summary>
        protected override void OnViewModelResolved(SettingsViewModel viewModel)
        {
            base.OnViewModelResolved(viewModel);

            DataContext = viewModel;

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
            AccessibleName = SettingsPanelResources.PanelTitle; // "Settings"
            Size = new Size(500, 400);
            MinimumSize = new Size((int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(800f), (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(600f));
            AutoScroll = true;
            Padding = new Padding(8);
            // DockingManager will handle docking; do not set Dock here.
            try { AutoScaleMode = AutoScaleMode.Dpi; } catch { }
        }

        private void SetupUI()
        {
            var padding = 20;
            var y = padding;

            _mainPanel = new Syncfusion.Windows.Forms.Tools.GradientPanelExt { Dock = DockStyle.Fill, Padding = new Padding(padding), BorderStyle = BorderStyle.None, BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty) };
            SfSkinManager.SetVisualStyle(_mainPanel, "Office2019Colorful");

            try { _error_provider = new ErrorProvider() { BlinkStyle = ErrorBlinkStyle.NeverBlink }; } catch { }

            // App title
            var lblAppTitle = new Label { Text = SettingsPanelResources.AppTitleLabel, AutoSize = true, Location = new Point(padding, y + 4), Font = new Font("Segoe UI", 9, FontStyle.Regular) };
#pragma warning disable RS0030 // TextBoxExt is the approved replacement for TextBox
            _txtAppTitle = new TextBoxExt { Name = "txtAppTitle", Location = new Point(padding + 120, y), Width = 300, MaxLength = 100, Font = new Font("Segoe UI", 10F), AccessibleName = "Application Title", AccessibleDescription = "Set the friendly application title" };
#pragma warning restore RS0030
            _txtToolTip = new ToolTip();
            _txtToolTip.SetToolTip(_txtAppTitle, "Enter a custom title for the application window");
            // Validation is now handled via ErrorProviderBinding to ViewModel
            _mainPanel.Controls.Add(lblAppTitle);
            _mainPanel.Controls.Add(_txtAppTitle);
            y += 40;

            // Appearance group
            _themeGroup = new GradientPanelExt { Location = new Point(padding, y), Size = new Size(440, 140) };
            var themeLabel = new Label { Text = SettingsPanelResources.AppearanceGroup, AutoSize = true, Location = new Point(5, 5), Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            _themeGroup.Controls.Add(themeLabel);

            _themeCombo = new Syncfusion.WinForms.ListView.SfComboBox { Name = "themeCombo", Location = new Point(20, 50), Size = new Size(380, 24), DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList, AllowDropDownResize = false, MaxDropDownItems = 5, AccessibleName = "themeCombo", AccessibleDescription = "Theme selection - choose application theme" }; _themeCombo.AccessibleName = "themeCombo"; // Expose as automation id for E2E tests            _themeCombo.DropDownListView.Style.ItemStyle.Font = new Font("Segoe UI", 10F);
            try { _themeCombo.DataSource = ViewModel?.Themes?.Cast<object>().ToList() ?? Enum.GetValues<AppTheme>().Cast<object>().ToList(); } catch { }
            try { _themeCombo.SelectedItem = _themeService.Preference; } catch { }
            _themeCombo.SelectedIndexChanged += (s, e) => { try { if (_themeCombo.SelectedItem is AppTheme sel) _themeService.SetTheme(sel); } catch { } };

            // Font selection combo
            var lblFont = new Label { Text = "Application Font:", AutoSize = true, Location = new Point(20, 85), Font = new Font("Segoe UI", 9, FontStyle.Regular) };
            _fontCombo = new Syncfusion.WinForms.ListView.SfComboBox { Name = "fontCombo", Location = new Point(20, 105), Size = new Size(380, 24), DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList, AllowDropDownResize = false, MaxDropDownItems = 10, AccessibleName = "Font selection", AccessibleDescription = "Select application font" };
            _fontCombo.DropDownListView.Style.ItemStyle.Font = new Font("Segoe UI", 10F);
            _fontCombo.DataSource = GetAvailableFonts();
            _fontCombo.SelectedItem = FontService.Instance.CurrentFont;
            _fontCombo.SelectedIndexChanged += OnFontSelectionChanged;

            _themeGroup.Controls.Add(_themeCombo);
            _themeGroup.Controls.Add(lblFont);
            _themeGroup.Controls.Add(_fontCombo);
            _mainPanel.Controls.Add(_themeGroup);
            y += 160;

            // Behavior settings
            _chkOpenEditFormsDocked = new CheckBoxAdv { Text = "Open edit forms docked (as floating tool windows)", AutoSize = true, Location = new Point(padding, y), Checked = ViewModel?.OpenEditFormsDocked ?? false, Font = new Font("Segoe UI", 9, FontStyle.Regular), AccessibleName = "Open edit forms docked", AccessibleDescription = "Open account edit forms as dockable floating windows" };
            _dockedToolTip = new ToolTip(); _dockedToolTip.SetToolTip(_chkOpenEditFormsDocked, "Open account edit forms as dockable floating windows instead of modal dialogs"); _chkOpenEditFormsDocked.CheckedChanged += (s, e) => { if (ViewModel != null) ViewModel.OpenEditFormsDocked = _chkOpenEditFormsDocked.Checked; };
            _mainPanel.Controls.Add(_chkOpenEditFormsDocked);
            y += 30;

            // Demo mode toggle
            _chkUseDemoData = new CheckBoxAdv { Text = "Use demo/sample data (for demonstrations)", AutoSize = true, Location = new Point(padding, y), Checked = ViewModel?.UseDemoData ?? false, Font = new Font("Segoe UI", 9, FontStyle.Regular), ForeColor = Color.Orange, AccessibleName = "Use demo data", AccessibleDescription = "When enabled, views display sample data instead of database data" };  // Semantic warning color (allowed exception)
            _demoDataToolTip = new ToolTip(); _demoDataToolTip.SetToolTip(_chkUseDemoData, "Enable demo mode to display sample data instead of real database data. Useful for demonstrations or when database is unavailable.");
            _chkUseDemoData.CheckedChanged += (s, e) => { if (ViewModel != null) ViewModel.UseDemoData = _chkUseDemoData.Checked; };
            _mainPanel.Controls.Add(_chkUseDemoData);
            y += 35;

            // Data Export group
            var exportGroup = new GradientPanelExt { Location = new Point(padding, y), Size = new Size(440, 70) };
            var exportLabel = new Label { Text = "Data Export", AutoSize = true, Location = new Point(5, 5), Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            exportGroup.Controls.Add(exportLabel);
            var lblExportPath = new Label { Text = "Export Path:", AutoSize = true, Location = new Point(20, 30) };
#pragma warning disable RS0030 // TextBoxExt is the approved replacement for TextBox
            _txtExportPath = new TextBoxExt { Name = "txtExportPath", Location = new Point(100, 27), Width = 250, AccessibleName = "Export path", AccessibleDescription = "Directory for data exports" };
#pragma warning restore RS0030
            _exportPathToolTip = new ToolTip(); _exportPathToolTip.SetToolTip(_txtExportPath, "Directory where exported data files will be saved");
            _btnBrowseExportPath = new Syncfusion.WinForms.Controls.SfButton { Name = "btnBrowseExportPath", Text = "...", Size = new Size(40, 24), Location = new Point(360, 26), AccessibleName = "Browse for export path", AccessibleDescription = "Open folder browser to select export directory" };
            _browseExportPathHandler = (s, e) => OnBrowseExportPath();
            try { if (ViewModel != null) ViewModel.BrowseExportPathRequested += _browseExportPathHandler; } catch { }
            _btnBrowseExportPath.Click += (s, e) => { try { ViewModel?.BrowseExportPathCommand?.Execute(null); } catch { } };
            exportGroup.Controls.Add(lblExportPath); exportGroup.Controls.Add(_txtExportPath); exportGroup.Controls.Add(_btnBrowseExportPath);
            _mainPanel.Controls.Add(exportGroup);
            y += 85;

            // Auto-save and Logging group
            var behaviorGroup = new GradientPanelExt { Location = new Point(padding, y), Size = new Size(440, 110) };
            var behaviorLabel = new Label { Text = "Behavior & Logging", AutoSize = true, Location = new Point(5, 5), Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            behaviorGroup.Controls.Add(behaviorLabel);
            var lblAutoSave = new Label { Text = "Auto-save interval (min):", AutoSize = true, Location = new Point(20, 30), Font = new Font("Segoe UI", 9, FontStyle.Regular) };
            _numAutoSaveInterval = new Syncfusion.WinForms.Input.SfNumericTextBox { Name = "numAutoSaveInterval", Location = new Point(170, 27), Size = new Size(80, 24), MinValue = 1, MaxValue = 60, Value = ViewModel?.AutoSaveIntervalMinutes ?? 5, AccessibleName = "Auto-save interval", AccessibleDescription = "Interval in minutes for auto-saving" };
            _autoSaveToolTip = new ToolTip(); _autoSaveToolTip.SetToolTip(_numAutoSaveInterval, "How often data is auto-saved (1-60 minutes)");
            _numAutoSaveInterval.ValueChanged += (s, e) => { try { if (ViewModel != null && _numAutoSaveInterval.Value.HasValue) ViewModel.AutoSaveIntervalMinutes = (int)_numAutoSaveInterval.Value.Value; } catch { } };
            behaviorGroup.Controls.Add(lblAutoSave); behaviorGroup.Controls.Add(_numAutoSaveInterval);

            var lblLogLevel = new Label { Text = "Log Level:", AutoSize = true, Location = new Point(20, 65), Font = new Font("Segoe UI", 9, FontStyle.Regular) };
            _cmbLogLevel = new Syncfusion.WinForms.ListView.SfComboBox { Name = "cmbLogLevel", Location = new Point(100, 62), Size = new Size(150, 24), DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList, AccessibleName = "Log level", AccessibleDescription = "Select application logging verbosity" };
            _logLevelToolTip = new ToolTip(); _logLevelToolTip.SetToolTip(_cmbLogLevel, "Verbosity level for application logging");
            try { _cmbLogLevel.DataSource = new[] { "Verbose", "Debug", "Information", "Warning", "Error", "Fatal" }.ToList(); _cmbLogLevel.SelectedItem = ViewModel?.LogLevel ?? "Information"; } catch { }
            _cmbLogLevel.SelectedIndexChanged += (s, e) => { try { if (ViewModel != null && _cmbLogLevel.SelectedItem is string level) ViewModel.LogLevel = level; } catch { } };
            behaviorGroup.Controls.Add(lblLogLevel); behaviorGroup.Controls.Add(_cmbLogLevel);
            _mainPanel.Controls.Add(behaviorGroup);
            y += 125;

            // AI / xAI settings group
            _aiGroup = new GradientPanelExt { Location = new Point(padding, y), Size = new Size(440, 240) };
            var aiLabel = new Label { Text = "AI / xAI Settings", AutoSize = true, Location = new Point(5, 5), Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            _aiGroup.Controls.Add(aiLabel);

            _chkEnableAi = new CheckBoxAdv { Text = "Enable AI (xAI)", AutoSize = true, Location = new Point(20, 28), Checked = ViewModel?.EnableAi ?? false, Font = new Font("Segoe UI", 9, FontStyle.Regular), AccessibleName = "Enable AI", AccessibleDescription = "Enable xAI API integrations" };
            _chkEnableAi.CheckedChanged += (s, e) =>
            {
                try
                {
                    if (ViewModel is not null)
                    {
                        ViewModel.EnableAi = _chkEnableAi.Checked;
                    }
                }
                catch { }
            };

            var lblEndpoint = new Label { Text = "API Endpoint:", AutoSize = true, Location = new Point(20, 56), Font = new Font("Segoe UI", 9, FontStyle.Regular) };
#pragma warning disable RS0030 // TextBoxExt is the approved replacement for TextBox
            _txtXaiApiEndpoint = new TextBoxExt { Name = "txtXaiApiEndpoint", Location = new Point(120, 52), Width = 300, Font = new Font("Segoe UI", 10F), Text = ViewModel?.XaiApiEndpoint ?? string.Empty, AccessibleName = "xAI API Endpoint", AccessibleDescription = "Endpoint for xAI Grok API" };
#pragma warning restore RS0030
            _txtXaiApiEndpoint.TextChanged += (s, e) => { try { if (ViewModel != null) ViewModel.XaiApiEndpoint = _txtXaiApiEndpoint.Text; } catch { } };

            var lblModel = new Label { Text = "Model:", AutoSize = true, Location = new Point(20, 84), Font = new Font("Segoe UI", 9, FontStyle.Regular) };
            _cmbXaiModel = new Syncfusion.WinForms.ListView.SfComboBox { Name = "cmbXaiModel", Location = new Point(120, 80), Size = new Size(220, 24), DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList };
            try { _cmbXaiModel.DataSource = new[] { "grok-4-0709", "grok-beta", "grok-3-2024" }.ToList(); _cmbXaiModel.SelectedItem = ViewModel?.XaiModel ?? "grok-4-0709"; } catch { }
            _cmbXaiModel.SelectedIndexChanged += (s, e) => { try { if (_cmbXaiModel.SelectedItem is string str && ViewModel != null) ViewModel.XaiModel = str; } catch { } };

            var lblApiKey = new Label { Text = "API Key:", AutoSize = true, Location = new Point(20, 112), Font = new Font("Segoe UI", 9, FontStyle.Regular) };
#pragma warning disable RS0030 // TextBoxExt is the approved replacement for TextBox
            _txtXaiApiKey = new TextBoxExt { Name = "txtXaiApiKey", Location = new Point(120, 108), Width = 220, Font = new Font("Segoe UI", 10F), UseSystemPasswordChar = true, Text = ViewModel?.XaiApiKey ?? string.Empty, AccessibleName = "xAI API Key", AccessibleDescription = "API key for xAI Grok (stored securely)" };
#pragma warning restore RS0030
            _txtXaiApiKey.TextChanged += (s, e) => { try { if (ViewModel != null) ViewModel.XaiApiKey = _txtXaiApiKey.Text; } catch { } };
            _btnShowApiKey = new Syncfusion.WinForms.Controls.SfButton { Name = "btnShowApiKey", Text = "Show", Size = new Size(50, 24), Location = new Point(350, 106), AccessibleName = "Show API Key", AccessibleDescription = "Toggle display of API key" };
            _btnShowApiKey.Click += (s, e) => { try { if (_txtXaiApiKey != null) { _txtXaiApiKey.UseSystemPasswordChar = !_txtXaiApiKey.UseSystemPasswordChar; _btnShowApiKey.Text = _txtXaiApiKey.UseSystemPasswordChar ? "Show" : "Hide"; } } catch { } };

            var lblTimeout = new Label { Text = "Timeout (s):", AutoSize = true, Location = new Point(20, 140), Font = new Font("Segoe UI", 9, FontStyle.Regular) };
            _numXaiTimeout = new Syncfusion.WinForms.Input.SfNumericTextBox { Name = "numXaiTimeout", Location = new Point(120, 136), Size = new Size(80, 24), MinValue = 1, MaxValue = 300, Value = ViewModel?.XaiTimeout ?? 30 };
            _numXaiTimeout.ValueChanged += (s, e) => { try { if (ViewModel != null && _numXaiTimeout.Value.HasValue) ViewModel.XaiTimeout = (int)_numXaiTimeout.Value.Value; } catch { } };

            var lblMaxTokens = new Label { Text = "Max tokens:", AutoSize = true, Location = new Point(210, 140), Font = new Font("Segoe UI", 9, FontStyle.Regular) };
            _numXaiMaxTokens = new Syncfusion.WinForms.Input.SfNumericTextBox { Name = "numXaiMaxTokens", Location = new Point(290, 136), Size = new Size(80, 24), MinValue = 1, MaxValue = 65536, Value = ViewModel?.XaiMaxTokens ?? 2000 };
            _numXaiMaxTokens.ValueChanged += (s, e) => { try { if (ViewModel != null && _numXaiMaxTokens.Value.HasValue) ViewModel.XaiMaxTokens = (int)_numXaiMaxTokens.Value.Value; } catch { } };

            var lblTemperature = new Label { Text = "Temperature:", AutoSize = true, Location = new Point(20, 168), Font = new Font("Segoe UI", 9, FontStyle.Regular) };
            _numXaiTemperature = new Syncfusion.WinForms.Input.SfNumericTextBox { Name = "numXaiTemperature", Location = new Point(120, 164), Size = new Size(80, 24), MinValue = 0.0, MaxValue = 1.0, Value = ViewModel?.XaiTemperature ?? 0.7 };
            _numXaiTemperature.ValueChanged += (s, e) => { try { if (ViewModel != null && _numXaiTemperature.Value.HasValue) ViewModel.XaiTemperature = Convert.ToDouble(_numXaiTemperature.Value.Value); } catch { } };

            // Help and guidance for AI settings
            _lblAiHelp = new Label { Text = SettingsPanelResources.AiSettingsHelpShort, Location = new Point(20, 186), Size = new Size(400, 32), Font = new Font("Segoe UI", 8F, FontStyle.Italic), AutoEllipsis = true };
            _lnkAiLearnMore = new LinkLabel { Text = SettingsPanelResources.AiSettingsLearnMoreLabel, Location = new Point(20, 220), AutoSize = true, Font = new Font("Segoe UI", 8F) };
            _lnkAiLearnMore.LinkClicked += (s, e) => ShowAiHelpDialog();

            // Cache note label
            var lblCacheNote = new Label { Text = "Note: Cached recommendations remain until their expiration. Use 'Clear AI Cache' to force refresh.", Location = new Point(20, 204), Size = new Size(400, 14), Font = new Font("Segoe UI", 8F, FontStyle.Regular), AutoEllipsis = true, AccessibleName = "AI cache note" };

            _aiToolTip = new ToolTip();
            _aiToolTip.SetToolTip(_chkEnableAi, "Enable or disable AI features. When disabled, the app uses rule-based recommendations.");
            _aiToolTip.SetToolTip(_txtXaiApiEndpoint, "Set the API endpoint for xAI Grok. Changing this affects where requests are sent.");
            _aiToolTip.SetToolTip(_cmbXaiModel, "Select model used for AI recommendations. Different models may provide different outputs.");
            _aiToolTip.SetToolTip(_txtXaiApiKey, "Your API key is stored securely and not logged. Changing it will take effect for subsequent API requests.");
            _aiToolTip.SetToolTip(_numXaiTimeout, "Maximum time (seconds) to wait for API response.");
            _aiToolTip.SetToolTip(_numXaiMaxTokens, "Maximum response tokens. Higher values allow longer completions but may cost more.");
            _aiToolTip.SetToolTip(_numXaiTemperature, "Lower temperature = more deterministic responses; higher temperature = more varied outputs.");

            // Reset and Clear Cache buttons
            var btnResetAi = new Syncfusion.WinForms.Controls.SfButton { Name = "btnResetAi", Text = "Reset to defaults", Size = new Size(120, 24), Location = new Point(260, 220), AccessibleName = "Reset AI settings", AccessibleDescription = "Reset AI settings to their default values" };
            btnResetAi.Click += (s, e) => { try { ViewModel?.ResetAiCommand?.Execute(null); MessageBox.Show(this, "AI settings reset to defaults. Save settings to persist changes.", "AI settings reset", MessageBoxButtons.OK, MessageBoxIcon.Information); } catch (Exception ex) { Serilog.Log.Warning(ex, "SettingsPanel: Reset AI defaults failed"); } };

            var btnClearAiCache = new Syncfusion.WinForms.Controls.SfButton { Name = "btnClearAiCache", Text = "Clear AI Cache", Size = new Size(120, 24), Location = new Point(120, 220), AccessibleName = "Clear AI cache", AccessibleDescription = "Clear cached AI recommendations and explanations" };
            btnClearAiCache.Click += (s, e) =>
            {
                try
                {
                    using var scope = ScopeFactory.CreateScope();
                    var svc = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.Business.Interfaces.IGrokRecommendationService>(scope.ServiceProvider);
                    if (svc == null)
                    {
                        MessageBox.Show(this, "AI recommendation service is not available in the current context.", "Clear cache", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    svc.ClearCache();
                    MessageBox.Show(this, "AI recommendation cache cleared.", "Clear cache", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning(ex, "SettingsPanel: Clear AI cache failed");
                    MessageBox.Show(this, "Failed to clear AI cache. See logs for details.", "Clear cache", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            _aiGroup.Controls.Add(_chkEnableAi);
            _aiGroup.Controls.Add(lblEndpoint); _aiGroup.Controls.Add(_txtXaiApiEndpoint);
            _aiGroup.Controls.Add(lblModel); _aiGroup.Controls.Add(_cmbXaiModel);
            _aiGroup.Controls.Add(lblApiKey); _aiGroup.Controls.Add(_txtXaiApiKey); _aiGroup.Controls.Add(_btnShowApiKey);
            _aiGroup.Controls.Add(lblTimeout); _aiGroup.Controls.Add(_numXaiTimeout);
            _aiGroup.Controls.Add(lblMaxTokens); _aiGroup.Controls.Add(_numXaiMaxTokens);
            _aiGroup.Controls.Add(lblTemperature); _aiGroup.Controls.Add(_numXaiTemperature);
            _aiGroup.Controls.Add(_lblAiHelp); _aiGroup.Controls.Add(lblCacheNote); _aiGroup.Controls.Add(_lnkAiLearnMore); _aiGroup.Controls.Add(btnResetAi); _aiGroup.Controls.Add(btnClearAiCache);

            // Add controls to group
            _aiGroup.Controls.Add(_chkEnableAi);
            _aiGroup.Controls.Add(lblEndpoint); _aiGroup.Controls.Add(_txtXaiApiEndpoint);
            _aiGroup.Controls.Add(lblModel); _aiGroup.Controls.Add(_cmbXaiModel);
            _aiGroup.Controls.Add(lblApiKey); _aiGroup.Controls.Add(_txtXaiApiKey); _aiGroup.Controls.Add(_btnShowApiKey);
            _aiGroup.Controls.Add(lblTimeout); _aiGroup.Controls.Add(_numXaiTimeout);
            _aiGroup.Controls.Add(lblMaxTokens); _aiGroup.Controls.Add(_numXaiMaxTokens);
            _aiGroup.Controls.Add(lblTemperature); _aiGroup.Controls.Add(_numXaiTemperature);
            _aiGroup.Controls.Add(_lblAiHelp); _aiGroup.Controls.Add(_lnkAiLearnMore);

            _mainPanel.Controls.Add(_aiGroup);
            y += 220;

            // Format settings group
            var formatGroup = new GradientPanelExt { Location = new Point(padding, y), Size = new Size(440, 80) };
            var formatLabel = new Label { Text = "Display Formats", AutoSize = true, Location = new Point(5, 5), Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            formatGroup.Controls.Add(formatLabel);
            var lblDateFormat = new Label { Text = "Date Format:", AutoSize = true, Location = new Point(20, 30), Font = new Font("Segoe UI", 9, FontStyle.Regular) };
#pragma warning disable RS0030 // TextBoxExt is the approved replacement for TextBox
            _txtDateFormat = new TextBoxExt { Name = "txtDateFormat", Location = new Point(110, 27), Width = 120, Font = new Font("Segoe UI", 10F), Text = ViewModel?.DateFormat ?? "yyyy-MM-dd", AccessibleName = "Date format", AccessibleDescription = "Format string for displaying dates" };
#pragma warning restore RS0030
            _txtDateFormat.TextChanged += (s, e) => { try { if (ViewModel != null) ViewModel.DateFormat = _txtDateFormat.Text; } catch { } };
            formatGroup.Controls.Add(lblDateFormat); formatGroup.Controls.Add(_txtDateFormat);

            var lblCurrencyFormat = new Label { Text = "Currency Format:", AutoSize = true, Location = new Point(250, 30), Font = new Font("Segoe UI", 9, FontStyle.Regular) };
#pragma warning disable RS0030 // TextBoxExt is the approved replacement for TextBox
            _txtCurrencyFormat = new TextBoxExt { Name = "txtCurrencyFormat", Location = new Point(360, 27), Width = 60, Font = new Font("Segoe UI", 10F), Text = ViewModel?.CurrencyFormat ?? "C2", AccessibleName = "Currency format", AccessibleDescription = "Format string for displaying currency values" };
#pragma warning restore RS0030
            _txtCurrencyFormat.TextChanged += (s, e) => { try { if (ViewModel != null) ViewModel.CurrencyFormat = _txtCurrencyFormat.Text; } catch { } };
            formatGroup.Controls.Add(lblCurrencyFormat); formatGroup.Controls.Add(_txtCurrencyFormat);
            _mainPanel.Controls.Add(formatGroup);
            y += 95;

            // About
            _aboutGroup = new GradientPanelExt { Location = new Point(padding, y), Size = new Size(440, 120) };
            var aboutLabel = new Label { Text = SettingsPanelResources.AboutGroup, AutoSize = true, Location = new Point(5, 5), Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            _aboutGroup.Controls.Add(aboutLabel);
            _lblVersion = new Label { Text = $"Wiley Widget v1.0.0\n.NET {Environment.Version}\nRuntime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}", Location = new Point(20, 30), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Regular) };
            _lblDbStatus = new Label { Text = "Database: Connected", Location = new Point(20, 85), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Regular) };
            _aboutGroup.Controls.Add(_lblVersion); _aboutGroup.Controls.Add(_lblDbStatus); _mainPanel.Controls.Add(_aboutGroup); y += 140;

            // Close button
            _btnClose = new Syncfusion.WinForms.Controls.SfButton { Name = "btnClose", Text = "Close", Size = new Size(100, 35), Location = new Point(350, y), AccessibleName = "Close settings", AccessibleDescription = "Close the settings panel" };
            _closeToolTip = new ToolTip(); _closeToolTip.SetToolTip(_btnClose, "Close this settings panel (Esc)"); _btnClose.Click += BtnClose_Click;
            try
            {
                var iconService = _iconService ?? (ServiceProvider != null ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IThemeIconService>(ServiceProvider) : null);
                if (iconService != null)
                {
                    _btnClose.Image = iconService.GetIcon("dismiss", _themeService.CurrentTheme, 14);
                    _btnClose.ImageAlign = ContentAlignment.MiddleLeft; _btnClose.TextImageRelation = TextImageRelation.ImageBeforeText;
                    _btnCloseThemeChangedHandler = (s, t) => { try { if (_btnClose.InvokeRequired) _btnClose.Invoke(() => _btnClose.Image = iconService.GetIcon("dismiss", t, 14)); else _btnClose.Image = iconService.GetIcon("dismiss", t, 14); } catch { } };
                }
                _themeService.ThemeChanged += _btnCloseThemeChangedHandler;
            }
            catch { }

            _mainPanel.Controls.Add(_btnClose);
            Controls.Add(_mainPanel);

            // Show/hide help dialog method to provide extended guidance
            // (kept near UI setup for easier readability and maintenance)
            void ShowAiHelpDialog()
            {
                try
                {
                    MessageBox.Show(this, SettingsPanelResources.AiSettingsHelpLong, SettingsPanelResources.AiSettingsDialogTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning(ex, "SettingsPanel: ShowAiHelpDialog failed");
                    MessageBox.Show(this, SettingsPanelResources.AiSettingsHelpShort, SettingsPanelResources.AiSettingsDialogTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            try
            {
                // Ensure we have a BindingSource connected to the ViewModel for data bindings
                _settingsBinding = new BindingSource();
                try { _settingsBinding.DataSource = ViewModel; } catch { }
            }
            catch { }

            try
            {
                if (_settingsBinding != null)
                {
                    _numXaiTemperature.DataBindings.Add("Value", _settingsBinding, "XaiTemperature", true, DataSourceUpdateMode.OnPropertyChanged);
                }
            }
            catch { }

            // Setup ErrorProviderBinding for ViewModel-driven validation
            try
            {
                if (_error_provider != null && ViewModel != null)
                {
                    _errorBinding = new ErrorProviderBinding(_error_provider, ViewModel);
                    _errorBinding.MapControl(nameof(ViewModel.DefaultExportPath), _txtExportPath!);
                    _errorBinding.MapControl(nameof(ViewModel.DateFormat), _txtDateFormat!);
                    _errorBinding.MapControl(nameof(ViewModel.CurrencyFormat), _txtCurrencyFormat!);
                    _errorBinding.MapControl(nameof(ViewModel.LogLevel), _cmbLogLevel!);

                    // XAI mappings
                    try { _errorBinding.MapControl(nameof(ViewModel.XaiApiEndpoint), _txtXaiApiEndpoint!); } catch { }
                    try { _errorBinding.MapControl(nameof(ViewModel.XaiApiKey), _txtXaiApiKey!); } catch { }
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
                InitialDirectory = Directory.Exists(ViewModel?.DefaultExportPath) ? ViewModel.DefaultExportPath : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            if (folderDialog.ShowDialog(this) == DialogResult.OK)
            {
                if (ViewModel != null) ViewModel.DefaultExportPath = folderDialog.SelectedPath;
            }
        }

        private void ApplyCurrentTheme()
        {
            var parentForm = FindForm();
            if (parentForm != null)
            {
                // Apply theme to the parent form using the theme service
                // _themeService.ApplyTheme(parentForm); // Method not found in IThemeService
                // Use SetVisualStyle for Syncfusion WinForms controls
                try { ThemeColors.ApplyTheme(parentForm, _themeService.CurrentTheme.ToString()); } catch { }
            }
            // Group box colors handled by SkinManager theme cascade
            // Syncfusion per-form skinning is handled by SfSkinManager above
        }

        private async Task LoadViewDataAsync()
        {
            try { Serilog.Log.Debug("SettingsPanel: LoadViewDataAsync starting"); if (ViewModel != null) { await (ViewModel.LoadCommand?.ExecuteAsync(null) ?? Task.CompletedTask); Serilog.Log.Information("SettingsPanel: settings loaded successfully"); } }
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
                if (ViewModel.HasUnsavedChanges)
                {
                    if (!ViewModel.ValidateSettings())
                    {
                        var errors = ViewModel.GetValidationSummary();
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

                    // Persist settings if a Save command is available
                    try { ViewModel.SaveCommand?.Execute(null); } catch (Exception ex) { Serilog.Log.Warning(ex, "SettingsPanel: Failed to save settings on close"); }
                }

                var parentForm = this.FindForm();
                if (parentForm is Forms.MainForm mainForm) { mainForm.CloseSettingsPanel(); return; }

                if (parentForm != null)
                {
                    var dockingManagerField = parentForm.GetType().GetField("_dockingManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (dockingManagerField?.GetValue(parentForm) is Syncfusion.Windows.Forms.Tools.DockingManager dm) { dm.SetDockVisibility(this, false); return; }
                }

                this.Parent?.Controls.Remove(this);
            }
            catch (Exception ex) { Serilog.Log.Warning(ex, "SettingsPanel: BtnClose_Click failed"); }
        }

        /// <summary>
        /// Gets a list of available fonts suitable for UI applications.
        /// </summary>
        private List<Font> GetAvailableFonts()
        {
            var fonts = new List<Font>();
            var fontFamilies = new[] { "Segoe UI", "Calibri", "Arial", "Tahoma", "Verdana" };

            foreach (var familyName in fontFamilies)
            {
                try
                {
                    var family = new FontFamily(familyName);
                    if (family.IsStyleAvailable(FontStyle.Regular))
                    {
                        fonts.Add(new Font(familyName, 9f, FontStyle.Regular));
                        fonts.Add(new Font(familyName, 10f, FontStyle.Regular));
                        fonts.Add(new Font(familyName, 11f, FontStyle.Regular));
                    }
                }
                catch
                {
                    // Skip fonts that can't be loaded
                }
            }

            return fonts;
        }

        /// <summary>
        /// Handles font selection changes.
        /// </summary>
        private void OnFontSelectionChanged(object? sender, EventArgs e)
        {
            try
            {
                if (_fontCombo?.SelectedItem is Font selectedFont)
                {
                    FontService.Instance.SetApplicationFont(selectedFont);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "SettingsPanel: Font selection change failed");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { if (_panelThemeChangedHandler != null) _themeService.ThemeChanged -= _panelThemeChangedHandler; } catch { }
                try { if (_btnCloseThemeChangedHandler != null) _themeService.ThemeChanged -= _btnCloseThemeChangedHandler; } catch { }
                try { if (_browseExportPathHandler != null) ViewModel.BrowseExportPathRequested -= _browseExportPathHandler; } catch { }

                try { if (_themeCombo != null && !_themeCombo.IsDisposed) { try { _themeCombo.DataSource = null; } catch { } _themeCombo.Dispose(); } } catch { }
                try { if (_fontCombo != null && !_fontCombo.IsDisposed) { try { _fontCombo.DataSource = null; } catch { } _fontCombo.Dispose(); } } catch { }
                try { if (_cmbLogLevel != null && !_cmbLogLevel.IsDisposed) { try { _cmbLogLevel.DataSource = null; } catch { } _cmbLogLevel.Dispose(); } } catch { }
                try { _aiToolTip?.Dispose(); } catch { }
                try { _lnkAiLearnMore?.Dispose(); } catch { }
                try { _lblAiHelp?.Dispose(); } catch { }
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
                // Dispose Syncfusion controls safely
                _themeCombo?.SafeDispose();
                _fontCombo?.SafeDispose();
                _cmbLogLevel?.SafeDispose();
            }
            base.Dispose(disposing);
        }
    }
}
