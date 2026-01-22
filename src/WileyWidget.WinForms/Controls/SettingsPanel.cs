using System.Threading;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Extensions;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.Drawing;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Input;
using WileyWidget.WinForms.Controls;

namespace WileyWidget.WinForms.Controls
{
    internal static class SettingsPanelResources
    {
        public const string PanelTitle = "Settings";
        public const string AppTitleLabel = "Application Title:";
        public const string AppearanceGroup = "Appearance";
        public const string ColorfulThemeLabel = "Colorful Theme (Office 2019)";
        public const string BlackThemeLabel = "Dark Theme (Office 2019 Black)";
        public const string WhiteThemeLabel = "Light Theme (Office 2019 White)";
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
        private readonly string _themeName = ThemeColors.CurrentTheme;
        public new object? DataContext { get; private set; }

        // Controls
        private PanelHeader? _panelHeader;
        private GradientPanelExt? _mainPanel;
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
        private CheckBoxAdv? _chkOpenEditFormsDocked;
        private ErrorProviderBinding? _errorBinding;

        // Status feedback
        private StatusStrip? _statusStrip;
        private ToolStripStatusLabel? _statusLabel;

            // AI / XAI controls
            private GroupBox? _aiGroup;
        private CheckBoxAdv? _chkEnableAi;
        private TextBox? _txtXaiApiEndpoint;
        private TextBox? _txtXaiApiKey;
        private Syncfusion.WinForms.Controls.SfButton? _btnShowApiKey;
        private Syncfusion.WinForms.ListView.SfComboBox? _cmbXaiModel;
        private Syncfusion.WinForms.Input.SfNumericTextBox? _numXaiTimeout;
        private Syncfusion.WinForms.Input.SfNumericTextBox? _numXaiMaxTokens;
        private Syncfusion.WinForms.Input.SfNumericTextBox? _numXaiTemperature;

        // AI help UI
        private Label? _lblAiHelp;
        private LinkLabel? _lnkAiLearnMore;
        private ToolTip? _aiToolTip;

        // Event handlers for proper cleanup
        private EventHandler? _fontSelectionChangedHandler;
        private EventHandler? _chkOpenEditFormsDockedHandler;
        private EventHandler? _chkUseDemoDataHandler;
        private EventHandler? _chkEnableAiHandler;
        private EventHandler? _themeComboSelectedHandler;
        private EventHandler? _txtXaiApiEndpointChangedHandler;
        private EventHandler? _cmbXaiModelSelectedHandler;
        private EventHandler? _txtXaiApiKeyChangedHandler;
        private Syncfusion.WinForms.Input.ValueChangedEventHandler? _numXaiTimeoutChangedHandler;
        private Syncfusion.WinForms.Input.ValueChangedEventHandler? _numXaiMaxTokensChangedHandler;
        private Syncfusion.WinForms.Input.ValueChangedEventHandler? _numXaiTemperatureChangedHandler;
        private EventHandler? _txtDateFormatChangedHandler;
        private EventHandler? _txtCurrencyFormatChangedHandler;
        private Syncfusion.WinForms.Input.ValueChangedEventHandler? _numAutoSaveIntervalChangedHandler;
        private EventHandler? _cmbLogLevelSelectedHandler;
        private EventHandler? _btnShowApiKeyClickHandler;
        private LinkLabelLinkClickedEventHandler? _lnkAiLearnMoreHandler;
        private EventHandler? _btnResetAiClickHandler;
        private EventHandler? _btnClearAiCacheClickHandler;
        private EventHandler? _btnBrowseExportPathClickHandler;
        private EventHandler? _btnCloseClickHandler;

        /// <summary>
        /// Constructor that accepts required dependencies from DI container.
        /// </summary>
        /// <param name="scopeFactory">Factory for creating service scopes</param>
        /// <param name="logger">Logger for diagnostic logging</param>
        public SettingsPanel(
            IServiceScopeFactory scopeFactory,
            Microsoft.Extensions.Logging.ILogger<ScopedPanelBase<SettingsViewModel>> logger)
            : base(scopeFactory, logger)
        {
            InitializeComponent();

            // Apply theme via SfSkinManager (single source of truth)
            try { Syncfusion.WinForms.Controls.SfSkinManager.SetVisualStyle(this, _themeName); } catch { }
        }

        /// <summary>
        /// Parameterless constructor for designer support ONLY.
        /// DO NOT USE in production - use DI constructor instead.
        /// </summary>
        [Obsolete("Use DI constructor with IServiceScopeFactory and ILogger (DI constructor)", error: false)]
        public SettingsPanel() : this(ResolveServiceScopeFactory(), ResolveLogger())
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

        private static Microsoft.Extensions.Logging.ILogger<ScopedPanelBase<SettingsViewModel>> ResolveLogger()
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

        /// <summary>
        /// Called after ViewModel is resolved from scoped provider.
        /// Performs UI setup and initial data binding.
        /// </summary>
        protected override void OnViewModelResolved(SettingsViewModel viewModel)
        {
            base.OnViewModelResolved(viewModel);

            DataContext = viewModel;

            InitializeComponent();
            ApplyCurrentTheme();

            // Set initial font selection
            SetInitialFontSelection();

            // Start async load - fire-and-forget with error handling
            _ = LoadAsyncSafe();
        }

        public override async Task LoadAsync(CancellationToken ct = default)
        {
            try
            {
                IsBusy = true;
                UpdateStatus("Loading settings...");

                await LoadViewDataAsync(ct);
                SetInitialFontSelection();

                UpdateStatus("Settings loaded successfully");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Load failed: {ex.Message}", true);
                throw;
            }
            finally
            {
                IsBusy = false;
            }
        }

        public override async Task<ValidationResult> ValidateAsync(CancellationToken ct = default)
        {
            if (_error_provider == null) return ValidationResult.Success;

            _error_provider.Clear();
            var errors = new List<ValidationItem>();

            // Validate required fields
            if (string.IsNullOrWhiteSpace(_txtAppTitle?.Text))
                errors.Add(new ValidationItem("AppTitle", "Application title is required", ValidationSeverity.Error, _txtAppTitle));

            if (string.IsNullOrWhiteSpace(_txtExportPath?.Text))
                errors.Add(new ValidationItem("DefaultExportPath", "Export path is required", ValidationSeverity.Error, _txtExportPath));

            // Validate ViewModel properties via ErrorProviderBinding
            if (_errorBinding != null)
            {
                // The binding will set errors on controls
                // We can collect them if needed
            }

            // Set errors on controls
            foreach (var error in errors)
            {
                if (error.ControlRef != null)
                    _error_provider.SetError(error.ControlRef, error.Message);
            }

            return errors.Any() ? ValidationResult.Failed(errors.ToArray()) : ValidationResult.Success;
        }

        public override void FocusFirstError()
        {
            var firstError = ValidationErrors.FirstOrDefault(e => e.ControlRef != null);
            firstError?.ControlRef?.Focus();
        }

        public override async Task SaveAsync(CancellationToken ct = default)
        {
            var validation = await ValidateAsync(ct);
            if (!validation.IsValid)
            {
                FocusFirstError();
                return;
            }

            try
            {
                IsBusy = true;
                UpdateStatus("Saving settings...");

                ViewModel?.SaveCommand?.Execute(null);

                UpdateStatus("Settings saved successfully");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Save failed: {ex.Message}", true);
                throw;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void InitializeComponent()
        {
            var padding = 20;
            var y = padding;

            // Panel header
            _panelHeader = new PanelHeader
            {
                Dock = DockStyle.Top,
                Title = "Application Settings",
                ShowRefreshButton = false,
                ShowHelpButton = false
            };
            _panelHeader.CloseClicked += (s, e) => ClosePanel();
            Controls.Add(_panelHeader);

            _mainPanel = new GradientPanelExt
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(padding),
                BorderStyle = BorderStyle.None,
                BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty),
                AutoScroll = true,
                AutoScrollMinSize = new Size(480, 1200)
            };
            SfSkinManager.SetVisualStyle(_mainPanel, _themeName);

            try { _error_provider = new ErrorProvider() { BlinkStyle = ErrorBlinkStyle.NeverBlink }; } catch { }

            // App title
            var lblAppTitle = new Label { Text = SettingsPanelResources.AppTitleLabel, AutoSize = true, Location = new Point(padding, y + 4), Font = new Font("Segoe UI", 9, FontStyle.Regular) };
#pragma warning disable RS0030 // TextBoxExt is the approved replacement for TextBox
            _txtAppTitle = new TextBoxExt
            {
                Name = "txtAppTitle",
                Location = new Point(padding + 120, y),
                Width = 300,
                MaxLength = 100,
                Font = new Font("Segoe UI", 10F),
                AccessibleName = "Application Title",
                AccessibleDescription = "Set the friendly application title"
            };
            if (ViewModel != null)
            {
                _txtAppTitle.DataBindings.Add("Text", ViewModel, "AppTitle", true, DataSourceUpdateMode.OnPropertyChanged);
            }
#pragma warning restore RS0030
            _txtToolTip = new ToolTip();
            _txtToolTip.SetToolTip(_txtAppTitle, "Enter a custom title for the application window");
            _mainPanel.Controls.Add(lblAppTitle);
            _mainPanel.Controls.Add(_txtAppTitle);
            y += 40;

            // Appearance group
            _themeGroup = new GradientPanelExt
            {
                Location = new Point(padding, y),
                Size = new Size(440, 140)
            };
            SfSkinManager.SetVisualStyle(_themeGroup, _themeName); // Set theme on group
            var themeLabel = new Label { Text = SettingsPanelResources.AppearanceGroup, AutoSize = true, Location = new Point(5, 5), Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            _themeGroup.Controls.Add(themeLabel);

            // Theme selection label
            var lblTheme = new Label { Text = "Application Theme:", AutoSize = true, Location = new Point(20, 15), Font = new Font("Segoe UI", 9, FontStyle.Regular) };
            _themeGroup.Controls.Add(lblTheme);

            _themeCombo = new Syncfusion.WinForms.ListView.SfComboBox
            {
                Name = "themeCombo",
                Location = new Point(20, 30),
                Size = new Size(380, 24),
                DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
                AllowDropDownResize = false,
                MaxDropDownItems = 5,
                AccessibleName = "themeCombo",
                AccessibleDescription = "Theme selection - choose application theme",
                ThemeName = _themeName
            };
            _themeCombo.DropDownListView.Style.ItemStyle.Font = new Font("Segoe UI", 10F);
            try { _themeCombo.DataSource = ViewModel?.Themes?.Cast<object>().ToList(); } catch { }
            try { _themeCombo.SelectedItem = ViewModel?.SelectedTheme; } catch { }
            _themeComboSelectedHandler = (s, e) => { if (ViewModel != null && _themeCombo.SelectedItem is string theme) ViewModel.SelectedTheme = theme; SetHasUnsavedChanges(true); };
            _themeCombo.SelectedIndexChanged += _themeComboSelectedHandler;

            // Font selection combo
            var lblFont = new Label { Text = "Application Font:", AutoSize = true, Location = new Point(20, 85), Font = new Font("Segoe UI", 9, FontStyle.Regular) };
            _fontCombo = new Syncfusion.WinForms.ListView.SfComboBox
            {
                Name = "fontCombo",
                Location = new Point(20, 105),
                Size = new Size(380, 24),
                DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
                AllowDropDownResize = false,
                MaxDropDownItems = 10,
                AccessibleName = "Font selection",
                AccessibleDescription = "Select application font",
                ThemeName = _themeName
            };
            _fontCombo.DropDownListView.Style.ItemStyle.Font = new Font("Segoe UI", 10F);
            _fontCombo.DataSource = GetAvailableFonts();
            _fontSelectionChangedHandler = (s, e) => OnFontSelectionChanged(s, e);
            _fontCombo.SelectedIndexChanged += _fontSelectionChangedHandler;

            _themeGroup.Controls.Add(_themeCombo);
            _themeGroup.Controls.Add(lblFont);
            _themeGroup.Controls.Add(_fontCombo);
            _mainPanel.Controls.Add(_themeGroup);
            y += 160;

            // Behavior settings
            _chkOpenEditFormsDocked = new CheckBoxAdv
            {
                Text = "Open edit forms docked (as floating tool windows)",
                AutoSize = true,
                Location = new Point(padding, y),
                Checked = ViewModel?.OpenEditFormsDocked ?? false,
                Font = new Font("Segoe UI", 9, FontStyle.Regular),
                AccessibleName = "Open edit forms docked",
                AccessibleDescription = "Open account edit forms as dockable floating windows"
            };
            _dockedToolTip = new ToolTip();
            _dockedToolTip.SetToolTip(_chkOpenEditFormsDocked, "Open account edit forms as dockable floating windows instead of modal dialogs");
            _chkOpenEditFormsDockedHandler = (s, e) => { if (ViewModel != null) ViewModel.OpenEditFormsDocked = _chkOpenEditFormsDocked.Checked; SetHasUnsavedChanges(true); };
            _chkOpenEditFormsDocked.CheckedChanged += _chkOpenEditFormsDockedHandler;
            _mainPanel.Controls.Add(_chkOpenEditFormsDocked);
            y += 30;

            // Demo mode toggle
            _chkUseDemoData = new CheckBoxAdv
            {
                Text = "Use demo/sample data (for demonstrations)",
                AutoSize = true,
                Location = new Point(padding, y),
                Checked = ViewModel?.UseDemoData ?? false,
                Font = new Font("Segoe UI", 9, FontStyle.Regular),
                AccessibleName = "Use demo data",
                AccessibleDescription = "When enabled, views display sample data instead of database data"
            };
            _demoDataToolTip = new ToolTip();
            _demoDataToolTip.SetToolTip(_chkUseDemoData, "Enable demo mode to display sample data instead of real database data. Useful for demonstrations or when database is unavailable.");
            _chkUseDemoDataHandler = (s, e) => { if (ViewModel != null) ViewModel.UseDemoData = _chkUseDemoData.Checked; SetHasUnsavedChanges(true); };
            _chkUseDemoData.CheckedChanged += _chkUseDemoDataHandler;
            _mainPanel.Controls.Add(_chkUseDemoData);
            y += 35;

            // Data Export group
            var exportGroup = new GradientPanelExt { Location = new Point(padding, y), Size = new Size(440, 70) };
            SfSkinManager.SetVisualStyle(exportGroup, _themeName); // Set theme on group
            var exportLabel = new Label { Text = "Data Export", AutoSize = true, Location = new Point(5, 5), Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            exportGroup.Controls.Add(exportLabel);
            var lblExportPath = new Label { Text = "Export Path:", AutoSize = true, Location = new Point(20, 30) };
#pragma warning disable RS0030 // TextBoxExt is the approved replacement for TextBox
            _txtExportPath = new TextBoxExt
            {
                Name = "txtExportPath",
                Location = new Point(100, 27),
                Width = 250,
                MaxLength = 260,
                AccessibleName = "Export path",
                AccessibleDescription = "Directory for data exports"
            };
#pragma warning restore RS0030
            _exportPathToolTip = new ToolTip();
            _exportPathToolTip.SetToolTip(_txtExportPath, "Directory where exported data files will be saved");
            _btnBrowseExportPath = new Syncfusion.WinForms.Controls.SfButton
            {
                Name = "btnBrowseExportPath",
                Text = "...",
                Size = new Size(40, 24),
                Location = new Point(360, 26),
                AccessibleName = "Browse for export path",
                AccessibleDescription = "Open folder browser to select export directory",
                ThemeName = _themeName
            };
            _btnBrowseExportPathClickHandler = (s, e) => OnBrowseExportPath();
            _btnBrowseExportPath.Click += _btnBrowseExportPathClickHandler;
            exportGroup.Controls.Add(lblExportPath);
            exportGroup.Controls.Add(_txtExportPath);
            exportGroup.Controls.Add(_btnBrowseExportPath);
            _mainPanel.Controls.Add(exportGroup);
            y += 85;

            // Auto-save and Logging group
            var behaviorGroup = new GradientPanelExt { Location = new Point(padding, y), Size = new Size(440, 110) };
            SfSkinManager.SetVisualStyle(behaviorGroup, _themeName); // Set theme on group
            var behaviorLabel = new Label { Text = "Behavior & Logging", AutoSize = true, Location = new Point(5, 5), Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            behaviorGroup.Controls.Add(behaviorLabel);
            var lblAutoSave = new Label { Text = "Auto-save interval (min):", AutoSize = true, Location = new Point(20, 30), Font = new Font("Segoe UI", 9, FontStyle.Regular) };
            _numAutoSaveInterval = new Syncfusion.WinForms.Input.SfNumericTextBox
            {
                Name = "numAutoSaveInterval",
                Location = new Point(170, 27),
                Size = new Size(80, 24),
                MinValue = 1,
                MaxValue = 60,
                Value = ViewModel?.AutoSaveIntervalMinutes ?? 5,
                AccessibleName = "Auto-save interval",
                AccessibleDescription = "Interval in minutes for auto-saving",
                ThemeName = _themeName
            };
            _autoSaveToolTip = new ToolTip();
            _autoSaveToolTip.SetToolTip(_numAutoSaveInterval, "How often data is auto-saved (1-60 minutes)");
            _numAutoSaveIntervalChangedHandler = (s, e) => { try { if (ViewModel != null && _numAutoSaveInterval.Value.HasValue) { ViewModel.AutoSaveIntervalMinutes = (int)_numAutoSaveInterval.Value.Value; SetHasUnsavedChanges(true); } } catch { } };
            _numAutoSaveInterval.ValueChanged += _numAutoSaveIntervalChangedHandler;
            behaviorGroup.Controls.Add(lblAutoSave);
            behaviorGroup.Controls.Add(_numAutoSaveInterval);

            var lblLogLevel = new Label { Text = "Log Level:", AutoSize = true, Location = new Point(20, 65), Font = new Font("Segoe UI", 9, FontStyle.Regular) };
            _cmbLogLevel = new Syncfusion.WinForms.ListView.SfComboBox
            {
                Name = "cmbLogLevel",
                Location = new Point(100, 62),
                Size = new Size(150, 24),
                DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
                AccessibleName = "Log level",
                AccessibleDescription = "Select application logging verbosity",
                ThemeName = _themeName
            };
            _logLevelToolTip = new ToolTip();
            _logLevelToolTip.SetToolTip(_cmbLogLevel, "Verbosity level for application logging");
            try { _cmbLogLevel.DataSource = new[] { "Verbose", "Debug", "Information", "Warning", "Error", "Fatal" }.ToList(); _cmbLogLevel.SelectedItem = ViewModel?.LogLevel ?? "Information"; } catch { }
            _cmbLogLevelSelectedHandler = (s, e) => { try { if (ViewModel != null && _cmbLogLevel.SelectedItem is string level) { ViewModel.LogLevel = level; SetHasUnsavedChanges(true); } } catch { } };
            _cmbLogLevel.SelectedIndexChanged += _cmbLogLevelSelectedHandler;
            behaviorGroup.Controls.Add(lblLogLevel);
            behaviorGroup.Controls.Add(_cmbLogLevel);
            _mainPanel.Controls.Add(behaviorGroup);
            y += 125;

            // AI / xAI settings group
            _aiGroup = new GroupBox { Text = "AI / xAI Settings", Location = new Point(padding, y), Size = new Size(440, 240), Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            // Note: GroupBox doesn't support ThemeName property directly in standard WinForms
            // but will inherit theme from parent via SfSkinManager cascade

            _chkEnableAi = new CheckBoxAdv
            {
                Text = "Enable AI (xAI)",
                AutoSize = true,
                Location = new Point(20, 28),
                Checked = ViewModel?.EnableAi ?? false,
                Font = new Font("Segoe UI", 9, FontStyle.Regular),
                AccessibleName = "Enable AI",
                AccessibleDescription = "Enable xAI API integrations"
            };
            _chkEnableAiHandler = (s, e) => { try { if (ViewModel != null) { ViewModel.EnableAi = _chkEnableAi.Checked; SetHasUnsavedChanges(true); } } catch { } };
            _chkEnableAi.CheckedChanged += _chkEnableAiHandler;

            var lblEndpoint = new Label { Text = "API Endpoint:", AutoSize = true, Location = new Point(20, 56), Font = new Font("Segoe UI", 9, FontStyle.Regular) };
            _txtXaiApiEndpoint = new TextBox
            {
                Name = "txtXaiApiEndpoint",
                Location = new Point(120, 52),
                Width = 300,
                MaxLength = 500,
                Font = new Font("Segoe UI", 10F),
                Text = ViewModel?.XaiApiEndpoint ?? string.Empty,
                AccessibleName = "xAI API Endpoint",
                AccessibleDescription = "Endpoint for xAI Grok API"
            };
            _txtXaiApiEndpointChangedHandler = (s, e) => { try { if (ViewModel != null) { ViewModel.XaiApiEndpoint = _txtXaiApiEndpoint.Text; SetHasUnsavedChanges(true); } } catch { } };
            _txtXaiApiEndpoint.TextChanged += _txtXaiApiEndpointChangedHandler;

            var lblModel = new Label { Text = "Model:", AutoSize = true, Location = new Point(20, 84), Font = new Font("Segoe UI", 9, FontStyle.Regular) };
            _cmbXaiModel = new Syncfusion.WinForms.ListView.SfComboBox
            {
                Name = "cmbXaiModel",
                Location = new Point(120, 80),
                Size = new Size(220, 24),
                DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
                ThemeName = _themeName
            };
            try { _cmbXaiModel.DataSource = new[] { "grok-4-0709", "grok-beta", "grok-3-2024" }.ToList(); _cmbXaiModel.SelectedItem = ViewModel?.XaiModel ?? "grok-4-0709"; } catch { }
            _cmbXaiModelSelectedHandler = (s, e) => { try { if (_cmbXaiModel.SelectedItem is string str && ViewModel != null) { ViewModel.XaiModel = str; SetHasUnsavedChanges(true); } } catch { } };
            _cmbXaiModel.SelectedIndexChanged += _cmbXaiModelSelectedHandler;

            var lblApiKey = new Label { Text = "API Key:", AutoSize = true, Location = new Point(20, 112), Font = new Font("Segoe UI", 9, FontStyle.Regular) };
            _txtXaiApiKey = new TextBox
            {
                Name = "txtXaiApiKey",
                Location = new Point(120, 108),
                Width = 220,
                MaxLength = 500,
                Font = new Font("Segoe UI", 10F),
                UseSystemPasswordChar = true,
                Text = ViewModel?.XaiApiKey ?? string.Empty,
                AccessibleName = "xAI API Key",
                AccessibleDescription = "API key for xAI Grok (stored securely)"
            };
            _txtXaiApiKeyChangedHandler = (s, e) => { try { if (ViewModel != null) { ViewModel.XaiApiKey = _txtXaiApiKey.Text; SetHasUnsavedChanges(true); } } catch { } };
            _txtXaiApiKey.TextChanged += _txtXaiApiKeyChangedHandler;

            _btnShowApiKey = new Syncfusion.WinForms.Controls.SfButton
            {
                Name = "btnShowApiKey",
                Text = "Show",
                Size = new Size(50, 24),
                Location = new Point(350, 106),
                AccessibleName = "Show API Key",
                AccessibleDescription = "Toggle display of API key",
                ThemeName = _themeName
            };
            _btnShowApiKeyClickHandler = (s, e) => { try { if (_txtXaiApiKey != null) { _txtXaiApiKey.UseSystemPasswordChar = !_txtXaiApiKey.UseSystemPasswordChar; _btnShowApiKey.Text = _txtXaiApiKey.UseSystemPasswordChar ? "Show" : "Hide"; } } catch { } };
            _btnShowApiKey.Click += _btnShowApiKeyClickHandler;

            var lblTimeout = new Label { Text = "Timeout (s):", AutoSize = true, Location = new Point(20, 140), Font = new Font("Segoe UI", 9, FontStyle.Regular) };
            _numXaiTimeout = new Syncfusion.WinForms.Input.SfNumericTextBox
            {
                Name = "numXaiTimeout",
                Location = new Point(120, 136),
                Size = new Size(80, 24),
                MinValue = 1,
                MaxValue = 300,
                Value = ViewModel?.XaiTimeout ?? 30,
                ThemeName = _themeName
            };
            _numXaiTimeoutChangedHandler = (s, e) => { try { if (ViewModel != null && _numXaiTimeout.Value.HasValue) { ViewModel.XaiTimeout = (int)_numXaiTimeout.Value.Value; SetHasUnsavedChanges(true); } } catch { } };
            _numXaiTimeout.ValueChanged += _numXaiTimeoutChangedHandler;

            var lblMaxTokens = new Label { Text = "Max tokens:", AutoSize = true, Location = new Point(210, 140), Font = new Font("Segoe UI", 9, FontStyle.Regular) };
            _numXaiMaxTokens = new Syncfusion.WinForms.Input.SfNumericTextBox
            {
                Name = "numXaiMaxTokens",
                Location = new Point(290, 136),
                Size = new Size(80, 24),
                MinValue = 1,
                MaxValue = 65536,
                Value = ViewModel?.XaiMaxTokens ?? 2000,
                ThemeName = _themeName
            };
            _numXaiMaxTokensChangedHandler = (s, e) => { try { if (ViewModel != null && _numXaiMaxTokens.Value.HasValue) { ViewModel.XaiMaxTokens = (int)_numXaiMaxTokens.Value.Value; SetHasUnsavedChanges(true); } } catch { } };
            _numXaiMaxTokens.ValueChanged += _numXaiMaxTokensChangedHandler;

            var lblTemperature = new Label { Text = "Temperature:", AutoSize = true, Location = new Point(20, 168), Font = new Font("Segoe UI", 9, FontStyle.Regular) };
            _numXaiTemperature = new Syncfusion.WinForms.Input.SfNumericTextBox
            {
                Name = "numXaiTemperature",
                Location = new Point(120, 164),
                Size = new Size(80, 24),
                MinValue = 0.0,
                MaxValue = 1.0,
                Value = ViewModel?.XaiTemperature ?? 0.7,
                ThemeName = _themeName
            };
            _numXaiTemperatureChangedHandler = (s, e) => { try { if (ViewModel != null && _numXaiTemperature.Value.HasValue) { ViewModel.XaiTemperature = Convert.ToDouble(_numXaiTemperature.Value.Value); SetHasUnsavedChanges(true); } } catch { } };
            _numXaiTemperature.ValueChanged += _numXaiTemperatureChangedHandler;

            // Help and guidance for AI settings
            _lblAiHelp = new Label { Text = SettingsPanelResources.AiSettingsHelpShort, Location = new Point(20, 186), Size = new Size(400, 32), Font = new Font("Segoe UI", 8F, FontStyle.Italic), AutoEllipsis = true };
            _lnkAiLearnMore = new LinkLabel { Text = SettingsPanelResources.AiSettingsLearnMoreLabel, Location = new Point(20, 220), AutoSize = true, Font = new Font("Segoe UI", 8F) };
            _lnkAiLearnMoreHandler = (s, e) => ShowAiHelpDialog();
            _lnkAiLearnMore.LinkClicked += _lnkAiLearnMoreHandler;

            // Cache note label
            var lblCacheNote = new Label
            {
                Text = "Note: Cached recommendations remain until their expiration. Use 'Clear AI Cache' to force refresh.",
                Location = new Point(20, 204),
                Size = new Size(400, 14),
                Font = new Font("Segoe UI", 8F, FontStyle.Regular),
                AutoEllipsis = true,
                AccessibleName = "AI cache note"
            };

            _aiToolTip = new ToolTip();
            _aiToolTip.SetToolTip(_chkEnableAi, "Enable or disable AI features. When disabled, the app uses rule-based recommendations.");
            _aiToolTip.SetToolTip(_txtXaiApiEndpoint, "Set the API endpoint for xAI Grok. Changing this affects where requests are sent.");
            _aiToolTip.SetToolTip(_cmbXaiModel, "Select model used for AI recommendations. Different models may provide different outputs.");
            _aiToolTip.SetToolTip(_txtXaiApiKey, "Your API key is stored securely and not logged. Changing it will take effect for subsequent API requests.");
            _aiToolTip.SetToolTip(_numXaiTimeout, "Maximum time (seconds) to wait for API response.");
            _aiToolTip.SetToolTip(_numXaiMaxTokens, "Maximum response tokens. Higher values allow longer completions but may cost more.");
            _aiToolTip.SetToolTip(_numXaiTemperature, "Lower temperature = more deterministic responses; higher temperature = more varied outputs.");

            // Reset and Clear Cache buttons
            var btnResetAi = new Syncfusion.WinForms.Controls.SfButton
            {
                Name = "btnResetAi",
                Text = "Reset to defaults",
                Size = new Size(120, 24),
                Location = new Point(260, 220),
                AccessibleName = "Reset AI settings",
                AccessibleDescription = "Reset AI settings to their default values",
                ThemeName = _themeName
            };
            _btnResetAiClickHandler = (s, e) => {
                try
                {
                    ViewModel?.ResetAiCommand?.Execute(null);
                    MessageBox.Show(this, "AI settings reset to defaults. Save settings to persist changes.", "AI settings reset", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    SetHasUnsavedChanges(true);
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning(ex, "SettingsPanel: Reset AI defaults failed");
                }
            };
            btnResetAi.Click += _btnResetAiClickHandler;

            var btnClearAiCache = new Syncfusion.WinForms.Controls.SfButton
            {
                Name = "btnClearAiCache",
                Text = "Clear AI Cache",
                Size = new Size(120, 24),
                Location = new Point(120, 220),
                AccessibleName = "Clear AI cache",
                AccessibleDescription = "Clear cached AI recommendations and explanations",
                ThemeName = _themeName
            };
            _btnClearAiCacheClickHandler = (s, e) =>
            {
                try
                {
                    if (Program.Services == null)
                    {
                        MessageBox.Show(this, "Application services are not available; cannot clear cache.", "Clear cache", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    using var scope = Program.Services.CreateScope();
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
            btnClearAiCache.Click += _btnClearAiCacheClickHandler;

            _aiGroup.Controls.Add(_chkEnableAi);
            _aiGroup.Controls.Add(lblEndpoint);
            _aiGroup.Controls.Add(_txtXaiApiEndpoint);
            _aiGroup.Controls.Add(lblModel);
            _aiGroup.Controls.Add(_cmbXaiModel);
            _aiGroup.Controls.Add(lblApiKey);
            _aiGroup.Controls.Add(_txtXaiApiKey);
            _aiGroup.Controls.Add(_btnShowApiKey);
            _aiGroup.Controls.Add(lblTimeout);
            _aiGroup.Controls.Add(_numXaiTimeout);
            _aiGroup.Controls.Add(lblMaxTokens);
            _aiGroup.Controls.Add(_numXaiMaxTokens);
            _aiGroup.Controls.Add(lblTemperature);
            _aiGroup.Controls.Add(_numXaiTemperature);
            _aiGroup.Controls.Add(_lblAiHelp);
            _aiGroup.Controls.Add(lblCacheNote);
            _aiGroup.Controls.Add(_lnkAiLearnMore);
            _aiGroup.Controls.Add(btnResetAi);
            _aiGroup.Controls.Add(btnClearAiCache);

            _mainPanel.Controls.Add(_aiGroup);
            y += 220;

            // Format settings group
            var formatGroup = new GradientPanelExt { Location = new Point(padding, y), Size = new Size(440, 80) };
            SfSkinManager.SetVisualStyle(formatGroup, _themeName); // Set theme on group
            var formatLabel = new Label { Text = "Display Formats", AutoSize = true, Location = new Point(5, 5), Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            formatGroup.Controls.Add(formatLabel);
            var lblDateFormat = new Label { Text = "Date Format:", AutoSize = true, Location = new Point(20, 30), Font = new Font("Segoe UI", 9, FontStyle.Regular) };
#pragma warning disable RS0030 // TextBoxExt is the approved replacement for TextBox
            _txtDateFormat = new TextBoxExt
            {
                Name = "txtDateFormat",
                Location = new Point(110, 27),
                Width = 120,
                MaxLength = 50,
                Font = new Font("Segoe UI", 10F),
                Text = ViewModel?.DateFormat ?? "yyyy-MM-dd",
                AccessibleName = "Date format",
                AccessibleDescription = "Format string for displaying dates"
            };
#pragma warning restore RS0030
            _txtDateFormatChangedHandler = (s, e) => { try { if (ViewModel != null) { ViewModel.DateFormat = _txtDateFormat.Text; SetHasUnsavedChanges(true); } } catch { } };
            _txtDateFormat.TextChanged += _txtDateFormatChangedHandler;
            formatGroup.Controls.Add(lblDateFormat);
            formatGroup.Controls.Add(_txtDateFormat);

            var lblCurrencyFormat = new Label { Text = "Currency Format:", AutoSize = true, Location = new Point(250, 30), Font = new Font("Segoe UI", 9, FontStyle.Regular) };
#pragma warning disable RS0030 // TextBoxExt is the approved replacement for TextBox
            _txtCurrencyFormat = new TextBoxExt
            {
                Name = "txtCurrencyFormat",
                Location = new Point(360, 27),
                Width = 60,
                MaxLength = 10,
                Font = new Font("Segoe UI", 10F),
                Text = ViewModel?.CurrencyFormat ?? "C2",
                AccessibleName = "Currency format",
                AccessibleDescription = "Format string for displaying currency values"
            };
#pragma warning restore RS0030
            _txtCurrencyFormatChangedHandler = (s, e) => { try { if (ViewModel != null) { ViewModel.CurrencyFormat = _txtCurrencyFormat.Text; SetHasUnsavedChanges(true); } } catch { } };
            _txtCurrencyFormat.TextChanged += _txtCurrencyFormatChangedHandler;
            formatGroup.Controls.Add(lblCurrencyFormat);
            formatGroup.Controls.Add(_txtCurrencyFormat);
            _mainPanel.Controls.Add(formatGroup);
            y += 95;

            // About
            _aboutGroup = new GradientPanelExt { Location = new Point(padding, y), Size = new Size(440, 120) };
            SfSkinManager.SetVisualStyle(_aboutGroup, _themeName); // Set theme on group
            var aboutLabel = new Label { Text = SettingsPanelResources.AboutGroup, AutoSize = true, Location = new Point(5, 5), Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            _aboutGroup.Controls.Add(aboutLabel);
            _lblVersion = new Label { Text = $"Wiley Widget v1.0.0\n.NET {Environment.Version}\nRuntime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}", Location = new Point(20, 30), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Regular) };
            _lblDbStatus = new Label { Text = "Database: Connected", Location = new Point(20, 85), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Regular) };
            _aboutGroup.Controls.Add(_lblVersion);
            _aboutGroup.Controls.Add(_lblDbStatus);
            _mainPanel.Controls.Add(_aboutGroup);
            y += 140;

            // Close button
            _btnClose = new Syncfusion.WinForms.Controls.SfButton
            {
                Name = "btnClose",
                Text = "Close",
                Size = new Size(100, 35),
                Location = new Point(350, y),
                AccessibleName = "Close settings",
                AccessibleDescription = "Close the settings panel",
                ThemeName = _themeName
            };
            _closeToolTip = new ToolTip();
            _closeToolTip.SetToolTip(_btnClose, "Close this settings panel (Esc)");
            _btnCloseClickHandler = (s, e) => BtnClose_Click(s, e);
            _btnClose.Click += _btnCloseClickHandler;

            _mainPanel.Controls.Add(_btnClose);
            Controls.Add(_mainPanel);

            // StatusStrip for feedback
            _statusStrip = new StatusStrip
            {
                Dock = DockStyle.Bottom,
                Name = "StatusStrip",
                AccessibleName = "Status Bar",
                AccessibleDescription = "Displays current status and feedback"
            };
            _statusLabel = new ToolStripStatusLabel
            {
                Text = "Ready",
                Name = "StatusLabel",
                AccessibleName = "Status Message",
                AccessibleDescription = "Current status of the settings panel"
            };
            _statusStrip.Items.Add(_statusLabel);
            Controls.Add(_statusStrip);

            // Setup BindingSource
            try
            {
                _settingsBinding = new BindingSource();
                try { _settingsBinding.DataSource = ViewModel; } catch { }
            }
            catch { }

            // Setup ErrorProviderBinding
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
                if (ViewModel != null)
                {
                    ViewModel.DefaultExportPath = folderDialog.SelectedPath;
                    SetHasUnsavedChanges(true);
                }
            }
        }

        private void ApplyCurrentTheme()
        {
            var parentForm = FindForm();
            if (parentForm != null)
            {
                try
                {
                    ThemeColors.ApplyTheme(parentForm, Syncfusion.WinForms.Controls.SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme);
                }
                catch { }
            }
        }

        private async Task LoadViewDataAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                Serilog.Log.Debug("SettingsPanel: LoadViewDataAsync starting");
                if (ViewModel != null)
                {
                    await (ViewModel.LoadCommand?.ExecuteAsync(null) ?? Task.CompletedTask);
                    Serilog.Log.Information("SettingsPanel: settings loaded successfully");
                    SetHasUnsavedChanges(false);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "SettingsPanel: LoadViewDataAsync failed");
            }
        }

        private void BtnClose_Click(object? sender, EventArgs e)
        {
            try
            {
                // Validate settings before closing if there are unsaved changes
                if (HasUnsavedChanges)
                {
                    var result = MessageBox.Show(
                        this,
                        "You have unsaved changes. Save them before closing?",
                        "Unsaved Changes",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Question);

                    if (result == DialogResult.Cancel)
                    {
                        return;
                    }

                    if (result == DialogResult.Yes)
                    {
                        // Validate first
                        var validationResult = ValidateAsync(CancellationToken.None).GetAwaiter().GetResult();
                        if (!validationResult.IsValid)
                        {
                            FocusFirstError();
                            MessageBox.Show(
                                this,
                                "Please correct validation errors before saving.",
                                "Validation Error",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                            return;
                        }

                        // Save settings
                        try { ViewModel?.SaveCommand?.Execute(null); } catch (Exception ex) { Serilog.Log.Warning(ex, "SettingsPanel: Failed to save settings on close"); }
                    }
                }

                var parentForm = this.FindForm();
                if (parentForm is Forms.MainForm mainForm)
                {
                    mainForm.CloseSettingsPanel();
                    return;
                }

                if (parentForm != null)
                {
                    var dockingManagerField = parentForm.GetType().GetField("_dockingManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (dockingManagerField?.GetValue(parentForm) is Syncfusion.Windows.Forms.Tools.DockingManager dm)
                    {
                        dm.SetDockVisibility(this, false);
                        return;
                    }
                }

                this.Parent?.Controls.Remove(this);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "SettingsPanel: BtnClose_Click failed");
            }
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
                    if (ViewModel != null)
                    {
                        ViewModel.ApplicationFont = $"{selectedFont.FontFamily.Name}, {selectedFont.Size}pt";
                    }
                    SetHasUnsavedChanges(true);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "SettingsPanel: Font selection change failed");
            }
        }

        private void ShowAiHelpDialog()
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

        /// <summary>
        /// Sets the initial font selection in the combo box based on ViewModel.ApplicationFont.
        /// </summary>
        private void SetInitialFontSelection()
        {
            try
            {
                if (ViewModel?.ApplicationFont != null && _fontCombo != null)
                {
                    var font = ParseFontString(ViewModel.ApplicationFont);
                    if (font != null)
                    {
                        _fontCombo.SelectedItem = font;
                    }
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "SettingsPanel: Failed to set initial font selection");
            }
        }

        /// <summary>
        /// Parses a font string like "Segoe UI, 9pt" into a Font object.
        /// </summary>
        private Font? ParseFontString(string fontString)
        {
            try
            {
                // Expected format: "FontName, Sizept"
                var parts = fontString.Split(',');
                if (parts.Length == 2)
                {
                    var fontName = parts[0].Trim();
                    var sizePart = parts[1].Trim();
                    if (sizePart.EndsWith("pt"))
                    {
                        var sizeString = sizePart.Substring(0, sizePart.Length - 2);
                        if (float.TryParse(sizeString, out var size))
                        {
                            return new Font(fontName, size, FontStyle.Regular);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Failed to parse font string: {FontString}", fontString);
            }
            return null;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Unsubscribe all event handlers
                try { if (_fontCombo != null) _fontCombo.SelectedIndexChanged -= _fontSelectionChangedHandler; } catch { }
                try { if (_chkOpenEditFormsDocked != null) _chkOpenEditFormsDocked.CheckedChanged -= _chkOpenEditFormsDockedHandler; } catch { }
                try { if (_chkUseDemoData != null) _chkUseDemoData.CheckedChanged -= _chkUseDemoDataHandler; } catch { }
                try { if (_chkEnableAi != null) _chkEnableAi.CheckedChanged -= _chkEnableAiHandler; } catch { }
                try { if (_themeCombo != null) _themeCombo.SelectedIndexChanged -= _themeComboSelectedHandler; } catch { }
                try { if (_txtXaiApiEndpoint != null) _txtXaiApiEndpoint.TextChanged -= _txtXaiApiEndpointChangedHandler; } catch { }
                try { if (_cmbXaiModel != null) _cmbXaiModel.SelectedIndexChanged -= _cmbXaiModelSelectedHandler; } catch { }
                try { if (_txtXaiApiKey != null) _txtXaiApiKey.TextChanged -= _txtXaiApiKeyChangedHandler; } catch { }
                try { if (_numXaiTimeout != null) _numXaiTimeout.ValueChanged -= _numXaiTimeoutChangedHandler; } catch { }
                try { if (_numXaiMaxTokens != null) _numXaiMaxTokens.ValueChanged -= _numXaiMaxTokensChangedHandler; } catch { }
                try { if (_numXaiTemperature != null) _numXaiTemperature.ValueChanged -= _numXaiTemperatureChangedHandler; } catch { }
                try { if (_txtDateFormat != null) _txtDateFormat.TextChanged -= _txtDateFormatChangedHandler; } catch { }
                try { if (_txtCurrencyFormat != null) _txtCurrencyFormat.TextChanged -= _txtCurrencyFormatChangedHandler; } catch { }
                try { if (_numAutoSaveInterval != null) _numAutoSaveInterval.ValueChanged -= _numAutoSaveIntervalChangedHandler; } catch { }
                try { if (_cmbLogLevel != null) _cmbLogLevel.SelectedIndexChanged -= _cmbLogLevelSelectedHandler; } catch { }
                try { if (_btnShowApiKey != null) _btnShowApiKey.Click -= _btnShowApiKeyClickHandler; } catch { }
                try { if (_lnkAiLearnMore != null) _lnkAiLearnMore.LinkClicked -= _lnkAiLearnMoreHandler; } catch { }
                try { if (_btnBrowseExportPath != null) _btnBrowseExportPath.Click -= _btnBrowseExportPathClickHandler; } catch { }
                try { if (_btnClose != null) _btnClose.Click -= _btnCloseClickHandler; } catch { }

                // Dispose controls and resources
                try { if (_themeCombo != null && !_themeCombo.IsDisposed) { try { _themeCombo.DataSource = null; } catch { } _themeCombo.Dispose(); } } catch { }
                try { if (_fontCombo != null && !_fontCombo.IsDisposed) { try { _fontCombo.DataSource = null; } catch { } _fontCombo.Dispose(); } } catch { }
                try { if (_cmbLogLevel != null && !_cmbLogLevel.IsDisposed) { try { _cmbLogLevel.DataSource = null; } catch { } _cmbLogLevel.Dispose(); } } catch { }
                try { if (_cmbXaiModel != null && !_cmbXaiModel.IsDisposed) { try { _cmbXaiModel.DataSource = null; } catch { } _cmbXaiModel.Dispose(); } } catch { }
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
                try { _panelHeader?.Dispose(); } catch { }
                try { _statusStrip?.Dispose(); } catch { }
                try { _txtXaiApiEndpoint?.Dispose(); } catch { }
                try { _txtXaiApiKey?.Dispose(); } catch { }
                try { _btnShowApiKey?.Dispose(); } catch { }
                try { _numXaiTimeout?.Dispose(); } catch { }
                try { _numXaiMaxTokens?.Dispose(); } catch { }
                try { _numXaiTemperature?.Dispose(); } catch { }
                try { _aiGroup?.Dispose(); } catch { }
                try { _chkEnableAi?.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// Updates the status bar with the specified message.
        /// </summary>
        /// <param name="message">The status message to display.</param>
        /// <param name="isError">True if this is an error message.</param>
        private void UpdateStatus(string message, bool isError = false)
        {
            if (_statusLabel != null)
            {
                _statusLabel.Text = message;
                _statusLabel.ForeColor = isError ? Color.Red : SystemColors.ControlText;
                _statusLabel.Invalidate();
            }
        }

        private void ClosePanel()
        {
            try
            {
                var parentForm = FindForm();
                if (parentForm == null) return;

                var closePanelMethod = parentForm.GetType().GetMethod(
                    "ClosePanel",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                closePanelMethod?.Invoke(parentForm, new object[] { Name });
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "SettingsPanel: Failed to close panel via parent form");
            }
        }
    }
}
