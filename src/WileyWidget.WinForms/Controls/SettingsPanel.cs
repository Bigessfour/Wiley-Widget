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
        #region Constants
        private const int GROUP_PADDING = 16;
        private const int CONTROL_SPACING = 12;
        private const int LABEL_WIDTH = 160;
        #endregion

        private readonly string _themeName = ThemeColors.CurrentTheme;
        public new object? DataContext { get; private set; }

        #region UI Control Fields
        // Header and main container
        private PanelHeader? _panelHeader;
        private GradientPanelExt? _mainPanel;
        private StatusStrip? _statusStrip;
        private ToolStripStatusLabel? _statusLabel;

        // Appearance group
        private GradientPanelExt? _themeGroup;
        private Syncfusion.WinForms.ListView.SfComboBox? _themeCombo;
        private Syncfusion.WinForms.ListView.SfComboBox? _fontCombo;

        // General settings group
        private TextBoxExt? _txtAppTitle;
        private CheckBoxAdv? _chkOpenEditFormsDocked;
        private CheckBoxAdv? _chkUseDemoData;
        private TextBoxExt? _txtExportPath;
        private Syncfusion.WinForms.Controls.SfButton? _btnBrowseExportPath;
        private Syncfusion.WinForms.Input.SfNumericTextBox? _numAutoSaveInterval;
        private Syncfusion.WinForms.ListView.SfComboBox? _cmbLogLevel;
        private TextBoxExt? _txtDateFormat;
        private TextBoxExt? _txtCurrencyFormat;

        // AI/xAI group
        private GradientPanelExt? _aiGroup;
        private CheckBoxAdv? _chkEnableAi;
        private TextBox? _txtXaiApiEndpoint;
        private TextBox? _txtXaiApiKey;
        private Syncfusion.WinForms.Controls.SfButton? _btnShowApiKey;
        private Syncfusion.WinForms.ListView.SfComboBox? _cmbXaiModel;
        private Syncfusion.WinForms.Input.SfNumericTextBox? _numXaiTimeout;
        private Syncfusion.WinForms.Input.SfNumericTextBox? _numXaiMaxTokens;
        private Syncfusion.WinForms.Input.SfNumericTextBox? _numXaiTemperature;
        private Label? _lblAiHelp;
        private LinkLabel? _lnkAiLearnMore;

        // About group
        private GradientPanelExt? _aboutGroup;
        private Label? _lblVersion;
        private Label? _lblDbStatus;

        // Buttons
        private Syncfusion.WinForms.Controls.SfButton? _btnClose;

        // Validation and binding
        private ErrorProvider? _errorProvider;
        private BindingSource? _bindingSource;
        private ErrorProviderBinding? _errorBinding;

        // Tooltips
        private ToolTip? _tooltip;
        private ToolTip? _aiToolTip;
        #endregion

        #region Event Handler Fields
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
        private EventHandler? _btnBrowseExportPathClickHandler;
        private EventHandler? _btnCloseClickHandler;
        #endregion

        /// <summary>
        /// Constructor that accepts required dependencies from DI container.
        /// </summary>
        public SettingsPanel(
            IServiceScopeFactory scopeFactory,
            Microsoft.Extensions.Logging.ILogger<ScopedPanelBase<SettingsViewModel>> logger)
            : base(scopeFactory, logger)
        {
        }

        /// <summary>
        /// Parameterless constructor for designer support ONLY.
        /// </summary>
        [Obsolete("Use DI constructor with IServiceScopeFactory and ILogger", error: false)]
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
            SetupBindings();
            SetupEventHandlers();
            ApplyCurrentTheme();

            // Set initial font selection
            SetInitialFontSelection();

            // Watch for unsaved changes - enable/disable Save button
            viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SettingsViewModel.HasUnsavedChanges) && _btnSave != null)
                {
                    _btnSave.Enabled = viewModel.HasUnsavedChanges;
                    UpdateStatus(
                        viewModel.HasUnsavedChanges
                            ? "You have unsaved changes"
                            : "All changes saved",
                        isError: false);
                }
            };

            // Set initial Save button state
            if (_btnSave != null)
            {
                _btnSave.Enabled = viewModel.HasUnsavedChanges;
            }

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
            if (_errorProvider == null) return ValidationResult.Success;

            _errorProvider.Clear();
            var errors = new List<ValidationItem>();

            // Validate required fields
            if (string.IsNullOrWhiteSpace(_txtAppTitle?.Text))
                errors.Add(new ValidationItem("AppTitle", "Application title is required", ValidationSeverity.Error, _txtAppTitle));

            // Validate ViewModel properties via ErrorProviderBinding
            if (_errorBinding != null)
            {
                // The binding will set errors on controls
            }

            // Set errors on controls
            foreach (var error in errors)
            {
                if (error.ControlRef != null)
                    _errorProvider.SetError(error.ControlRef, error.Message);
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
                SetHasUnsavedChanges(false);
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

        #region Initialization
        private void InitializeComponent()
        {
            SuspendLayout();
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.Transparent;
            Padding = new Padding(GROUP_PADDING);

            // Panel header
            _panelHeader = new PanelHeader
            {
                Dock = DockStyle.Top,
                Title = "Application Settings",
                ShowRefreshButton = false,
                ShowHelpButton = false,
                Height = 44
            };
            _panelHeader.CloseClicked += (s, e) => ClosePanel();
            Controls.Add(_panelHeader);

            // Status strip (bottom)
            _statusStrip = new StatusStrip { SizingGrip = false, Dock = DockStyle.Bottom };
            _statusLabel = new ToolStripStatusLabel { Text = "Ready", Spring = true };
            _statusStrip.Items.Add(_statusLabel);
            Controls.Add(_statusStrip);

            // Main scrollable content area
            _mainPanel = new GradientPanelExt
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, CONTROL_SPACING, 0, GROUP_PADDING),
                BorderStyle = BorderStyle.None,
                BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty),
                AutoScroll = true,
                AutoScrollMinSize = new Size(520, 3000)
            };
            SfSkinManager.SetVisualStyle(_mainPanel, _themeName);

            _errorProvider = new ErrorProvider { BlinkStyle = ErrorBlinkStyle.NeverBlink };
            _tooltip = new ToolTip();
            _aiToolTip = new ToolTip();

            int y = CONTROL_SPACING;

            // Create sections
            CreateAppTitleSection(ref y);
            CreateAppearanceSection(ref y);
            CreateGeneralSettingsSection(ref y);
            CreateExportSection(ref y);
            CreateBehaviorSection(ref y);
            CreateAiSection(ref y);
            CreateFormatSection(ref y);
            CreateAboutSection(ref y);
            CreateButtonRow(ref y);

            _mainPanel.Controls.Add(_panelHeader);
            Controls.Add(_mainPanel);
            Controls.Add(_statusStrip);

            ResumeLayout(false);
        }

        private void CreateAppTitleSection(ref int y)
        {
            var lblAppTitle = new Label
            {
                Text = SettingsPanelResources.AppTitleLabel,
                AutoSize = true,
                Location = new Point(GROUP_PADDING, y + 4),
                Font = new Font("Segoe UI", 9, FontStyle.Regular)
            };
#pragma warning disable RS0030
            _txtAppTitle = new TextBoxExt
            {
                Name = "txtAppTitle",
                Location = new Point(GROUP_PADDING + LABEL_WIDTH, y),
                Width = 300,
                MaxLength = 100,
                Font = new Font("Segoe UI", 10F),
                AccessibleName = "Application Title",
                AccessibleDescription = "Set the friendly application title"
            };
#pragma warning restore RS0030
            if (ViewModel != null)
            {
                _txtAppTitle.DataBindings.Add("Text", ViewModel, "AppTitle", true, DataSourceUpdateMode.OnPropertyChanged);
            }
            _tooltip?.SetToolTip(_txtAppTitle, "Enter a custom title for the application window");
            _mainPanel.Controls.Add(lblAppTitle);
            _mainPanel.Controls.Add(_txtAppTitle);
            y += 50;
        }

        private void CreateAppearanceSection(ref int y)
        {
            _themeGroup = CreateGroupPanel("Appearance", ref y);

            var lblTheme = new Label { Text = "Theme:", AutoSize = true, Location = new Point(GROUP_PADDING, 50), Font = new Font("Segoe UI", 9) };
            _themeCombo = new Syncfusion.WinForms.ListView.SfComboBox
            {
                Name = "themeCombo",
                Location = new Point(GROUP_PADDING + LABEL_WIDTH, 48),
                Size = new Size(220, 24),
                DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
                AllowDropDownResize = false,
                MaxDropDownItems = 5,
                ThemeName = _themeName
            };
            _themeCombo.DropDownListView.Style.ItemStyle.Font = new Font("Segoe UI", 10F);

            try
            {
                if (ViewModel?.Themes != null && ViewModel.Themes.Count > 0)
                {
                    _themeCombo.DataSource = new List<string>(ViewModel.Themes);
                    if (!string.IsNullOrEmpty(ViewModel.SelectedTheme))
                    {
                        _themeCombo.SelectedItem = ViewModel.SelectedTheme;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "SettingsPanel: Error populating theme dropdown");
            }

            var lblFont = new Label { Text = "Font:", AutoSize = true, Location = new Point(GROUP_PADDING, 85), Font = new Font("Segoe UI", 9) };
            _fontCombo = new Syncfusion.WinForms.ListView.SfComboBox
            {
                Name = "fontCombo",
                Location = new Point(GROUP_PADDING + LABEL_WIDTH, 82),
                Size = new Size(220, 24),
                DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
                AllowDropDownResize = false,
                MaxDropDownItems = 10,
                ThemeName = _themeName
            };
            _fontCombo.DropDownListView.Style.ItemStyle.Font = new Font("Segoe UI", 10F);
            _fontCombo.DataSource = GetAvailableFonts();

            _themeGroup.Controls.Add(lblTheme);
            _themeGroup.Controls.Add(_themeCombo);
            _themeGroup.Controls.Add(lblFont);
            _themeGroup.Controls.Add(_fontCombo);

            _mainPanel.Controls.Add(_themeGroup);
            y += 200;
        }

        private void CreateGeneralSettingsSection(ref int y)
        {
            _chkOpenEditFormsDocked = new CheckBoxAdv
            {
                Text = "Open edit forms docked (as floating tool windows)",
                AutoSize = true,
                Location = new Point(GROUP_PADDING, y),
                Checked = ViewModel?.OpenEditFormsDocked ?? false,
                Font = new Font("Segoe UI", 9),
                AccessibleName = "Open edit forms docked"
            };
            _tooltip?.SetToolTip(_chkOpenEditFormsDocked, "Open account edit forms as dockable floating windows instead of modal dialogs");
            _mainPanel.Controls.Add(_chkOpenEditFormsDocked);
            y += 40;

            _chkUseDemoData = new CheckBoxAdv
            {
                Text = "Use demo/sample data (for demonstrations)",
                AutoSize = true,
                Location = new Point(GROUP_PADDING, y),
                Checked = ViewModel?.UseDemoData ?? false,
                Font = new Font("Segoe UI", 9),
                AccessibleName = "Use demo data"
            };
            _tooltip?.SetToolTip(_chkUseDemoData, "Enable demo mode to display sample data instead of real database data.");
            _mainPanel.Controls.Add(_chkUseDemoData);
            y += 45;
        }

        private void CreateExportSection(ref int y)
        {
            var exportGroup = CreateGroupPanel("Data Export", ref y);

            var lblExportPath = new Label { Text = "Path:", AutoSize = true, Location = new Point(GROUP_PADDING, 50) };
#pragma warning disable RS0030
            _txtExportPath = new TextBoxExt
            {
                Name = "txtExportPath",
                Location = new Point(GROUP_PADDING + LABEL_WIDTH, 48),
                Width = 200,
                MaxLength = 260,
                AccessibleName = "Export path"
            };
#pragma warning restore RS0030
            if (ViewModel != null && !string.IsNullOrEmpty(ViewModel.DefaultExportPath))
            {
                _txtExportPath.Text = ViewModel.DefaultExportPath;
            }

            _btnBrowseExportPath = new Syncfusion.WinForms.Controls.SfButton
            {
                Name = "btnBrowseExportPath",
                Text = "...",
                Size = new Size(40, 24),
                Location = new Point(GROUP_PADDING + LABEL_WIDTH + 200 + 8, 48),
                ThemeName = _themeName
            };

            _tooltip?.SetToolTip(_txtExportPath, "Directory where exported data files will be saved");
            _tooltip?.SetToolTip(_btnBrowseExportPath, "Open folder browser to select export directory");

            exportGroup.Controls.Add(lblExportPath);
            exportGroup.Controls.Add(_txtExportPath);
            exportGroup.Controls.Add(_btnBrowseExportPath);
            _mainPanel.Controls.Add(exportGroup);
            y += 85;
        }

        private void CreateBehaviorSection(ref int y)
        {
            var behaviorGroup = CreateGroupPanel("Behavior & Logging", ref y);

            var lblAutoSave = new Label { Text = "Auto-save (min):", AutoSize = true, Location = new Point(GROUP_PADDING, 50), Font = new Font("Segoe UI", 9) };
            _numAutoSaveInterval = new Syncfusion.WinForms.Input.SfNumericTextBox
            {
                Name = "numAutoSaveInterval",
                Location = new Point(GROUP_PADDING + LABEL_WIDTH, 48),
                Size = new Size(80, 24),
                MinValue = 1,
                MaxValue = 60,
                Value = ViewModel != null ? ViewModel.AutoSaveIntervalMinutes : 5,
                ThemeName = _themeName
            };
            _tooltip?.SetToolTip(_numAutoSaveInterval, "How often data is auto-saved (1-60 minutes)");

            var lblLogLevel = new Label { Text = "Log Level:", AutoSize = true, Location = new Point(GROUP_PADDING, 85), Font = new Font("Segoe UI", 9) };
            _cmbLogLevel = new Syncfusion.WinForms.ListView.SfComboBox
            {
                Name = "cmbLogLevel",
                Location = new Point(GROUP_PADDING + LABEL_WIDTH, 82),
                Size = new Size(150, 24),
                DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
                ThemeName = _themeName
            };
            var logLevels = new[] { "Verbose", "Debug", "Information", "Warning", "Error", "Fatal" }.ToList();
            _cmbLogLevel.DataSource = logLevels;
            _cmbLogLevel.SelectedItem = ViewModel != null ? ViewModel.LogLevel : "Information";
            _tooltip?.SetToolTip(_cmbLogLevel, "Verbosity level for application logging");

            behaviorGroup.Controls.Add(lblAutoSave);
            behaviorGroup.Controls.Add(_numAutoSaveInterval);
            behaviorGroup.Controls.Add(lblLogLevel);
            behaviorGroup.Controls.Add(_cmbLogLevel);
            _mainPanel.Controls.Add(behaviorGroup);
            y += 125;
        }

        private void CreateAiSection(ref int y)
        {
            _aiGroup = CreateGroupPanel("AI / xAI Settings", ref y);

            _chkEnableAi = new CheckBoxAdv
            {
                Text = "Enable AI (xAI)",
                AutoSize = true,
                Location = new Point(GROUP_PADDING, 50),
                Checked = ViewModel?.EnableAi ?? false,
                Font = new Font("Segoe UI", 9)
            };

            var lblEndpoint = new Label { Text = "Endpoint:", AutoSize = true, Location = new Point(GROUP_PADDING, 78), Font = new Font("Segoe UI", 9) };
            _txtXaiApiEndpoint = new TextBox
            {
                Name = "txtXaiApiEndpoint",
                Location = new Point(GROUP_PADDING + LABEL_WIDTH, 75),
                Width = 250,
                MaxLength = 500,
                Text = ViewModel?.XaiApiEndpoint ?? string.Empty
            };

            var lblModel = new Label { Text = "Model:", AutoSize = true, Location = new Point(GROUP_PADDING, 106), Font = new Font("Segoe UI", 9) };
            _cmbXaiModel = new Syncfusion.WinForms.ListView.SfComboBox
            {
                Name = "cmbXaiModel",
                Location = new Point(GROUP_PADDING + LABEL_WIDTH, 103),
                Size = new Size(220, 24),
                DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
                ThemeName = _themeName
            };
            try
            {
                _cmbXaiModel.DataSource = new[] { "grok-4-0709", "grok-beta", "grok-3-2024" }.ToList();
                _cmbXaiModel.SelectedItem = ViewModel?.XaiModel ?? "grok-4-0709";
            }
            catch { }

            var lblApiKey = new Label { Text = "API Key:", AutoSize = true, Location = new Point(GROUP_PADDING, 134), Font = new Font("Segoe UI", 9) };
            _txtXaiApiKey = new TextBox
            {
                Name = "txtXaiApiKey",
                Location = new Point(GROUP_PADDING + LABEL_WIDTH, 131),
                Width = 180,
                MaxLength = 500,
                UseSystemPasswordChar = true,
                Text = ViewModel?.XaiApiKey ?? string.Empty
            };
            _btnShowApiKey = new Syncfusion.WinForms.Controls.SfButton
            {
                Name = "btnShowApiKey",
                Text = "Show",
                Size = new Size(50, 24),
                Location = new Point(GROUP_PADDING + LABEL_WIDTH + 180 + 8, 131),
                ThemeName = _themeName
            };

            var lblTimeout = new Label { Text = "Timeout (s):", AutoSize = true, Location = new Point(GROUP_PADDING, 162), Font = new Font("Segoe UI", 9) };
            _numXaiTimeout = new Syncfusion.WinForms.Input.SfNumericTextBox
            {
                Name = "numXaiTimeout",
                Location = new Point(GROUP_PADDING + LABEL_WIDTH, 159),
                Size = new Size(80, 24),
                MinValue = 1,
                MaxValue = 300,
                Value = ViewModel?.XaiTimeout ?? 30,
                ThemeName = _themeName
            };

            var lblMaxTokens = new Label { Text = "Max tokens:", AutoSize = true, Location = new Point(GROUP_PADDING + LABEL_WIDTH + 90, 162), Font = new Font("Segoe UI", 9) };
            _numXaiMaxTokens = new Syncfusion.WinForms.Input.SfNumericTextBox
            {
                Name = "numXaiMaxTokens",
                Location = new Point(GROUP_PADDING + LABEL_WIDTH + 170, 159),
                Size = new Size(100, 24),
                MinValue = 1,
                MaxValue = 65536,
                Value = ViewModel?.XaiMaxTokens ?? 2000,
                ThemeName = _themeName
            };

            var lblTemperature = new Label { Text = "Temperature:", AutoSize = true, Location = new Point(GROUP_PADDING, 190), Font = new Font("Segoe UI", 9) };
            _numXaiTemperature = new Syncfusion.WinForms.Input.SfNumericTextBox
            {
                Name = "numXaiTemperature",
                Location = new Point(GROUP_PADDING + LABEL_WIDTH, 187),
                Size = new Size(80, 24),
                MinValue = 0.0,
                MaxValue = 1.0,
                Value = ViewModel?.XaiTemperature ?? 0.7,
                ThemeName = _themeName
            };

            _lblAiHelp = new Label
            {
                Text = SettingsPanelResources.AiSettingsHelpShort,
                Location = new Point(GROUP_PADDING, 220),
                Size = new Size(400, 32),
                Font = new Font("Segoe UI", 8F, FontStyle.Italic),
                AutoEllipsis = true
            };

            _lnkAiLearnMore = new LinkLabel
            {
                Text = SettingsPanelResources.AiSettingsLearnMoreLabel,
                Location = new Point(GROUP_PADDING, 255),
                AutoSize = true,
                Font = new Font("Segoe UI", 8F)
            };

            var btnResetAi = new Syncfusion.WinForms.Controls.SfButton
            {
                Name = "btnResetAi",
                Text = "Reset to defaults",
                Size = new Size(120, 24),
                Location = new Point(GROUP_PADDING + 150, 255),
                ThemeName = _themeName
            };

            _aiToolTip?.SetToolTip(_chkEnableAi, "Enable or disable AI features.");
            _aiToolTip?.SetToolTip(_txtXaiApiEndpoint, "API endpoint for xAI Grok.");
            _aiToolTip?.SetToolTip(_cmbXaiModel, "Select model used for recommendations.");
            _aiToolTip?.SetToolTip(_txtXaiApiKey, "API key stored securely (not logged).");
            _aiToolTip?.SetToolTip(_numXaiTimeout, "Maximum time (seconds) to wait for response.");
            _aiToolTip?.SetToolTip(_numXaiMaxTokens, "Maximum response tokens.");
            _aiToolTip?.SetToolTip(_numXaiTemperature, "Response randomness (0=deterministic, 1=varied).");

            _btnResetAiClickHandler = (s, e) =>
            {
                try
                {
                    ViewModel?.ResetAiCommand?.Execute(null);
                    MessageBox.Show(this, "AI settings reset to defaults. Save to persist changes.", "AI Reset", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    SetHasUnsavedChanges(true);
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning(ex, "SettingsPanel: Reset AI defaults failed");
                }
            };
            btnResetAi.Click += _btnResetAiClickHandler;

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
            _aiGroup.Controls.Add(_lnkAiLearnMore);
            _aiGroup.Controls.Add(btnResetAi);

            _mainPanel.Controls.Add(_aiGroup);
            y += 320;
        }

        private void CreateFormatSection(ref int y)
        {
            var formatGroup = CreateGroupPanel("Display Formats", ref y);

            var lblDateFormat = new Label { Text = "Date:", AutoSize = true, Location = new Point(GROUP_PADDING, 50), Font = new Font("Segoe UI", 9) };
#pragma warning disable RS0030
            _txtDateFormat = new TextBoxExt
            {
                Name = "txtDateFormat",
                Location = new Point(GROUP_PADDING + LABEL_WIDTH, 48),
                Width = 120,
                MaxLength = 50,
                Text = ViewModel?.DateFormat ?? "yyyy-MM-dd"
            };
#pragma warning restore RS0030

            var lblCurrencyFormat = new Label { Text = "Currency:", AutoSize = true, Location = new Point(GROUP_PADDING + LABEL_WIDTH + 140, 50), Font = new Font("Segoe UI", 9) };
#pragma warning disable RS0030
            _txtCurrencyFormat = new TextBoxExt
            {
                Name = "txtCurrencyFormat",
                Location = new Point(GROUP_PADDING + LABEL_WIDTH + 220, 48),
                Width = 80,
                MaxLength = 10,
                Text = ViewModel?.CurrencyFormat ?? "C2"
            };
#pragma warning restore RS0030

            formatGroup.Controls.Add(lblDateFormat);
            formatGroup.Controls.Add(_txtDateFormat);
            formatGroup.Controls.Add(lblCurrencyFormat);
            formatGroup.Controls.Add(_txtCurrencyFormat);
            _mainPanel.Controls.Add(formatGroup);
            y += 95;
        }

        private void CreateAboutSection(ref int y)
        {
            _aboutGroup = CreateGroupPanel("About", ref y);

            _lblVersion = new Label
            {
                Text = $"Wiley Widget v1.0.0\n.NET {Environment.Version}\nRuntime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}",
                Location = new Point(GROUP_PADDING, 50),
                AutoSize = true,
                Font = new Font("Segoe UI", 9)
            };
            _lblDbStatus = new Label
            {
                Text = "Database: Connected",
                Location = new Point(GROUP_PADDING, 105),
                AutoSize = true,
                Font = new Font("Segoe UI", 9)
            };

            _aboutGroup.Controls.Add(_lblVersion);
            _aboutGroup.Controls.Add(_lblDbStatus);
            _mainPanel.Controls.Add(_aboutGroup);
            y += 140;
        }

        private void CreateButtonRow(ref int y)
        {
            var buttonBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 54,
                Padding = new Padding(GROUP_PADDING, 8, GROUP_PADDING, 8),
                FlowDirection = FlowDirection.RightToLeft,
                BackColor = Color.Transparent,
                AutoSize = false,
                WrapContents = false
            };

            // Close button
            var btnClose = new Syncfusion.WinForms.Controls.SfButton
            {
                Name = "btnClose",
                Text = "Close",
                Width = 100,
                Height = 36,
                Margin = new Padding(8, 0, 0, 0),
                ThemeName = _themeName
            };
            _tooltip?.SetToolTip(btnClose, "Close this settings panel");
            _btnCloseClickHandler = (s, e) => BtnClose_Click(s, e);
            btnClose.Click += _btnCloseClickHandler;
            buttonBar.Controls.Add(btnClose);

            // Reset AI button
            var btnResetAi = new Syncfusion.WinForms.Controls.SfButton
            {
                Name = "btnResetAi",
                Text = "Reset AI",
                Width = 120,
                Height = 36,
                Margin = new Padding(8, 0, 0, 0),
                ThemeName = _themeName
            };
            _tooltip?.SetToolTip(btnResetAi, "Reset AI settings to defaults");
            _btnResetAiClickHandler = (s, e) =>
            {
                try
                {
                    ViewModel?.ResetAiCommand?.Execute(null);
                    MessageBox.Show(this, "AI settings reset to defaults. Click Save to persist.", "AI Reset", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    SetHasUnsavedChanges(true);
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning(ex, "SettingsPanel: Reset AI failed");
                }
            };
            btnResetAi.Click += _btnResetAiClickHandler;
            buttonBar.Controls.Add(btnResetAi);

            // SAVE button (primary action)
            _btnSave = new Syncfusion.WinForms.Controls.SfButton
            {
                Name = "btnSave",
                Text = "Save Settings",
                Width = 140,
                Height = 36,
                Margin = new Padding(8, 0, 0, 0),
                ThemeName = _themeName,
                Enabled = false
            };
            _tooltip?.SetToolTip(_btnSave, "Save all changes (validation runs first)");
            _btnSave.Click += (s, e) => BtnSave_Click(s, e);
            buttonBar.Controls.Add(_btnSave);

            _btnClose = btnClose;
            _mainPanel?.Controls.Add(buttonBar);
            y += 54;
        }

        private GradientPanelExt CreateGroupPanel(string caption, ref int yPos)
        {
            var panel = new GradientPanelExt
            {
                Location = new Point(GROUP_PADDING, yPos),
                Size = new Size(500, 300),
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right,
                BorderStyle = BorderStyle.FixedSingle,
                BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.WhiteSmoke, Color.White),
                AutoSize = false
            };

            var lblCaption = new Label
            {
                Text = caption,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Location = new Point(GROUP_PADDING, GROUP_PADDING),
                AutoSize = true
            };
            panel.Controls.Add(lblCaption);

            SfSkinManager.SetVisualStyle(panel, _themeName);
            _mainPanel.Controls.Add(panel);
            yPos += panel.Height + CONTROL_SPACING;

            return panel;
        }
        #endregion

        #region Data Binding
        private void SetupBindings()
        {
            _bindingSource = new BindingSource { DataSource = ViewModel };

            // Theme binding
            if (_themeCombo != null && ViewModel != null)
            {
                _themeCombo.DataBindings.Add(
                    nameof(Syncfusion.WinForms.ListView.SfComboBox.SelectedItem),
                    _bindingSource,
                    nameof(ViewModel.SelectedTheme),
                    true,
                    DataSourceUpdateMode.OnPropertyChanged);
            }

            // General settings bindings
            if (_txtAppTitle != null)
            {
                _txtAppTitle.DataBindings.Add(
                    nameof(TextBoxExt.Text),
                    _bindingSource,
                    nameof(ViewModel.AppTitle),
                    true,
                    DataSourceUpdateMode.OnPropertyChanged);
            }

            if (_chkOpenEditFormsDocked != null)
            {
                _chkOpenEditFormsDocked.DataBindings.Add(
                    nameof(CheckBoxAdv.Checked),
                    _bindingSource,
                    nameof(ViewModel.OpenEditFormsDocked),
                    true,
                    DataSourceUpdateMode.OnPropertyChanged);
            }

            if (_chkUseDemoData != null)
            {
                _chkUseDemoData.DataBindings.Add(
                    nameof(CheckBoxAdv.Checked),
                    _bindingSource,
                    nameof(ViewModel.UseDemoData),
                    true,
                    DataSourceUpdateMode.OnPropertyChanged);
            }

            if (_txtExportPath != null)
            {
                _txtExportPath.DataBindings.Add(
                    nameof(TextBoxExt.Text),
                    _bindingSource,
                    nameof(ViewModel.DefaultExportPath),
                    true,
                    DataSourceUpdateMode.OnPropertyChanged);
            }

            if (_numAutoSaveInterval != null)
            {
                _numAutoSaveInterval.DataBindings.Add(
                    nameof(Syncfusion.WinForms.Input.SfNumericTextBox.Value),
                    _bindingSource,
                    nameof(ViewModel.AutoSaveIntervalMinutes),
                    true,
                    DataSourceUpdateMode.OnPropertyChanged);
            }

            if (_cmbLogLevel != null)
            {
                _cmbLogLevel.DataBindings.Add(
                    nameof(Syncfusion.WinForms.ListView.SfComboBox.SelectedItem),
                    _bindingSource,
                    nameof(ViewModel.LogLevel),
                    true,
                    DataSourceUpdateMode.OnPropertyChanged);
            }

            if (_txtDateFormat != null)
            {
                _txtDateFormat.DataBindings.Add(
                    nameof(TextBoxExt.Text),
                    _bindingSource,
                    nameof(ViewModel.DateFormat),
                    true,
                    DataSourceUpdateMode.OnPropertyChanged);
            }

            if (_txtCurrencyFormat != null)
            {
                _txtCurrencyFormat.DataBindings.Add(
                    nameof(TextBoxExt.Text),
                    _bindingSource,
                    nameof(ViewModel.CurrencyFormat),
                    true,
                    DataSourceUpdateMode.OnPropertyChanged);
            }

            // AI bindings
            if (_chkEnableAi != null)
            {
                _chkEnableAi.DataBindings.Add(
                    nameof(CheckBoxAdv.Checked),
                    _bindingSource,
                    nameof(ViewModel.EnableAi),
                    true,
                    DataSourceUpdateMode.OnPropertyChanged);
            }

            if (_txtXaiApiEndpoint != null)
            {
                _txtXaiApiEndpoint.DataBindings.Add(
                    nameof(TextBox.Text),
                    _bindingSource,
                    nameof(ViewModel.XaiApiEndpoint),
                    true,
                    DataSourceUpdateMode.OnPropertyChanged);
            }

            if (_txtXaiApiKey != null)
            {
                _txtXaiApiKey.DataBindings.Add(
                    nameof(TextBox.Text),
                    _bindingSource,
                    nameof(ViewModel.XaiApiKey),
                    true,
                    DataSourceUpdateMode.OnPropertyChanged);
            }

            if (_cmbXaiModel != null)
            {
                _cmbXaiModel.DataBindings.Add(
                    nameof(Syncfusion.WinForms.ListView.SfComboBox.SelectedItem),
                    _bindingSource,
                    nameof(ViewModel.XaiModel),
                    true,
                    DataSourceUpdateMode.OnPropertyChanged);
            }

            if (_numXaiTimeout != null)
            {
                _numXaiTimeout.DataBindings.Add(
                    nameof(Syncfusion.WinForms.Input.SfNumericTextBox.Value),
                    _bindingSource,
                    nameof(ViewModel.XaiTimeout),
                    true,
                    DataSourceUpdateMode.OnPropertyChanged);
            }

            if (_numXaiMaxTokens != null)
            {
                _numXaiMaxTokens.DataBindings.Add(
                    nameof(Syncfusion.WinForms.Input.SfNumericTextBox.Value),
                    _bindingSource,
                    nameof(ViewModel.XaiMaxTokens),
                    true,
                    DataSourceUpdateMode.OnPropertyChanged);
            }

            if (_numXaiTemperature != null)
            {
                _numXaiTemperature.DataBindings.Add(
                    nameof(Syncfusion.WinForms.Input.SfNumericTextBox.Value),
                    _bindingSource,
                    nameof(ViewModel.XaiTemperature),
                    true,
                    DataSourceUpdateMode.OnPropertyChanged);
            }

            // Setup ErrorProviderBinding
            try
            {
                if (_errorProvider != null && ViewModel != null)
                {
                    _errorBinding = new ErrorProviderBinding(_errorProvider, ViewModel);
                    _errorBinding.MapControl(nameof(ViewModel.AppTitle), _txtAppTitle!);
                    _errorBinding.MapControl(nameof(ViewModel.DefaultExportPath), _txtExportPath!);
                    _errorBinding.MapControl(nameof(ViewModel.DateFormat), _txtDateFormat!);
                    _errorBinding.MapControl(nameof(ViewModel.CurrencyFormat), _txtCurrencyFormat!);
                    _errorBinding.MapControl(nameof(ViewModel.LogLevel), _cmbLogLevel!);

                    try { _errorBinding.MapControl(nameof(ViewModel.XaiApiEndpoint), _txtXaiApiEndpoint!); } catch { }
                    try { _errorBinding.MapControl(nameof(ViewModel.XaiApiKey), _txtXaiApiKey!); } catch { }
                    try { _errorBinding.MapControl(nameof(ViewModel.XaiModel), _cmbXaiModel!); } catch { }
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "SettingsPanel: Failed to setup ErrorProviderBinding");
            }
        }

        private void SetupEventHandlers()
        {
            _fontSelectionChangedHandler = (s, e) => OnFontSelectionChanged(s, e);
            if (_fontCombo != null) _fontCombo.SelectedIndexChanged += _fontSelectionChangedHandler;

            _chkOpenEditFormsDockedHandler = (s, e) => { if (IsLoaded) SetHasUnsavedChanges(true); };
            if (_chkOpenEditFormsDocked != null) _chkOpenEditFormsDocked.CheckedChanged += _chkOpenEditFormsDockedHandler;

            _chkUseDemoDataHandler = (s, e) => { if (IsLoaded) SetHasUnsavedChanges(true); };
            if (_chkUseDemoData != null) _chkUseDemoData.CheckedChanged += _chkUseDemoDataHandler;

            _chkEnableAiHandler = (s, e) => { if (IsLoaded) SetHasUnsavedChanges(true); };
            if (_chkEnableAi != null) _chkEnableAi.CheckedChanged += _chkEnableAiHandler;

            _themeComboSelectedHandler = (s, e) => { if (IsLoaded) SetHasUnsavedChanges(true); };
            if (_themeCombo != null) _themeCombo.SelectedIndexChanged += _themeComboSelectedHandler;

            _txtXaiApiEndpointChangedHandler = (s, e) => { if (IsLoaded) SetHasUnsavedChanges(true); };
            if (_txtXaiApiEndpoint != null) _txtXaiApiEndpoint.TextChanged += _txtXaiApiEndpointChangedHandler;

            _cmbXaiModelSelectedHandler = (s, e) => { if (IsLoaded) SetHasUnsavedChanges(true); };
            if (_cmbXaiModel != null) _cmbXaiModel.SelectedIndexChanged += _cmbXaiModelSelectedHandler;

            _txtXaiApiKeyChangedHandler = (s, e) => { if (IsLoaded) SetHasUnsavedChanges(true); };
            if (_txtXaiApiKey != null) _txtXaiApiKey.TextChanged += _txtXaiApiKeyChangedHandler;

            _numXaiTimeoutChangedHandler = (s, e) => { if (IsLoaded) SetHasUnsavedChanges(true); };
            if (_numXaiTimeout != null) _numXaiTimeout.ValueChanged += _numXaiTimeoutChangedHandler;

            _numXaiMaxTokensChangedHandler = (s, e) => { if (IsLoaded) SetHasUnsavedChanges(true); };
            if (_numXaiMaxTokens != null) _numXaiMaxTokens.ValueChanged += _numXaiMaxTokensChangedHandler;

            _numXaiTemperatureChangedHandler = (s, e) => { if (IsLoaded) SetHasUnsavedChanges(true); };
            if (_numXaiTemperature != null) _numXaiTemperature.ValueChanged += _numXaiTemperatureChangedHandler;

            _txtDateFormatChangedHandler = (s, e) => { if (IsLoaded) SetHasUnsavedChanges(true); };
            if (_txtDateFormat != null) _txtDateFormat.TextChanged += _txtDateFormatChangedHandler;

            _txtCurrencyFormatChangedHandler = (s, e) => { if (IsLoaded) SetHasUnsavedChanges(true); };
            if (_txtCurrencyFormat != null) _txtCurrencyFormat.TextChanged += _txtCurrencyFormatChangedHandler;

            _numAutoSaveIntervalChangedHandler = (s, e) => { if (IsLoaded) SetHasUnsavedChanges(true); };
            if (_numAutoSaveInterval != null) _numAutoSaveInterval.ValueChanged += _numAutoSaveIntervalChangedHandler;

            _cmbLogLevelSelectedHandler = (s, e) => { if (IsLoaded) SetHasUnsavedChanges(true); };
            if (_cmbLogLevel != null) _cmbLogLevel.SelectedIndexChanged += _cmbLogLevelSelectedHandler;

            _btnShowApiKeyClickHandler = (s, e) => { if (_txtXaiApiKey != null) { _txtXaiApiKey.UseSystemPasswordChar = !_txtXaiApiKey.UseSystemPasswordChar; if (s is Syncfusion.WinForms.Controls.SfButton btn) btn.Text = _txtXaiApiKey.UseSystemPasswordChar ? "Show" : "Hide"; } };
            if (_btnShowApiKey != null) _btnShowApiKey.Click += _btnShowApiKeyClickHandler;

            _lnkAiLearnMoreHandler = (s, e) => ShowAiHelpDialog();
            if (_lnkAiLearnMore != null) _lnkAiLearnMore.LinkClicked += _lnkAiLearnMoreHandler;

            _btnBrowseExportPathClickHandler = (s, e) => OnBrowseExportPath();
            if (_btnBrowseExportPath != null) _btnBrowseExportPath.Click += _btnBrowseExportPathClickHandler;
        }
        #endregion

        #region Event Handlers
        /// <summary>
        /// Handles Save button click: validates, saves, and provides feedback.
        /// </summary>
        private async void BtnSave_Click(object? sender, EventArgs e)
        {
            try
            {
                IsBusy = true;
                UpdateStatus("Validating settings...");

                var validation = await ValidateAsync(CancellationToken.None);
                if (!validation.IsValid)
                {
                    FocusFirstError();
                    UpdateStatus("Validation failed — please review errors", isError: true);
                    return;
                }

                UpdateStatus("Saving settings...");
                ViewModel?.SaveCommand?.Execute(null);

                UpdateStatus("Settings saved successfully", isError: false);
                SetHasUnsavedChanges(false);
                Serilog.Log.Information("SettingsPanel: Settings saved");

                MessageBox.Show(
                    this,
                    "Settings saved successfully.\nTheme changes applied immediately.",
                    "Save Successful",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                UpdateStatus($"Save failed: {ex.Message}", isError: true);
                Serilog.Log.Error(ex, "SettingsPanel: Save failed");
            }
            finally
            {
                IsBusy = false;
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
            if (folderDialog.ShowDialog(this) == DialogResult.OK && ViewModel != null)
            {
                ViewModel.DefaultExportPath = folderDialog.SelectedPath;
                SetHasUnsavedChanges(true);
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
                if (HasUnsavedChanges)
                {
                    var result = MessageBox.Show(
                        this,
                        "You have unsaved changes. Save them before closing?",
                        "Unsaved Changes",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Question);

                    if (result == DialogResult.Cancel)
                        return;

                    if (result == DialogResult.Yes)
                    {
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

                        try { ViewModel?.SaveCommand?.Execute(null); }
                        catch (Exception ex) { Serilog.Log.Warning(ex, "SettingsPanel: Failed to save settings on close"); }
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
                catch { }
            }

            return fonts;
        }

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

        private Font? ParseFontString(string fontString)
        {
            try
            {
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
        #endregion

        #region Disposal
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
                try { _tooltip?.Dispose(); } catch { }
                try { _lnkAiLearnMore?.Dispose(); } catch { }
                try { _lblAiHelp?.Dispose(); } catch { }
                try { _mainPanel?.Dispose(); } catch { }
                try { _themeGroup?.Dispose(); } catch { }
                try { _aboutGroup?.Dispose(); } catch { }
                try { _aiGroup?.Dispose(); } catch { }
                try { _btnClose?.Dispose(); } catch { }
                try { _txtAppTitle?.Dispose(); } catch { }
                try { _chkOpenEditFormsDocked?.Dispose(); } catch { }
                try { _lblVersion?.Dispose(); } catch { }
                try { _lblDbStatus?.Dispose(); } catch { }
                try { _errorProvider?.Dispose(); } catch { }
                try { _errorBinding?.Dispose(); } catch { }
                try { _bindingSource?.Dispose(); } catch { }
                try { _txtExportPath?.Dispose(); } catch { }
                try { _btnBrowseExportPath?.Dispose(); } catch { }
                try { _numAutoSaveInterval?.Dispose(); } catch { }
                try { _txtDateFormat?.Dispose(); } catch { }
                try { _txtCurrencyFormat?.Dispose(); } catch { }
                try { _chkUseDemoData?.Dispose(); } catch { }
                try { _panelHeader?.Dispose(); } catch { }
                try { _statusStrip?.Dispose(); } catch { }
                try { _txtXaiApiEndpoint?.Dispose(); } catch { }
                try { _txtXaiApiKey?.Dispose(); } catch { }
                try { _btnShowApiKey?.Dispose(); } catch { }
                try { _numXaiTimeout?.Dispose(); } catch { }
                try { _numXaiMaxTokens?.Dispose(); } catch { }
                try { _numXaiTemperature?.Dispose(); } catch { }
                try { _chkEnableAi?.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
        #endregion
    }
}
