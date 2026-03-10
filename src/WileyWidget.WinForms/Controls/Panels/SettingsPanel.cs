using System.Threading;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Utilities;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.Windows.Forms;
using Syncfusion.WinForms.DataGrid.Events;
using Syncfusion.WinForms.Controls;



using Syncfusion.Windows.Forms.Chart;
using Syncfusion.Windows.Forms.Gauge;

using Syncfusion.WinForms.DataGrid;


using Syncfusion.WinForms.ListView;

using Syncfusion.WinForms.Input;
using System.ComponentModel;

using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Supporting;
using ValidationResult = WileyWidget.WinForms.Controls.Base.ValidationResult;
using Syncfusion.Drawing;
using ThemeColors = WileyWidget.WinForms.Themes.ThemeColors;
using WileyWidget.WinForms.Helpers;
using WileyWidget.WinForms.Factories;

namespace WileyWidget.WinForms.Controls.Panels
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
        public const string AiSettingsLearnMoreLabel = "Learn more";
        public const string AiSettingsDialogTitle = "AI Settings Help";
        public const string SecretSaveFailedTitle = "Key Save Failed";
        public const string SecretSaveWarningsTitle = "Key Save Warnings";
    }

    [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
    public partial class SettingsPanel : ScopedPanelBase<SettingsViewModel>
    {
        private sealed class FontOption
        {
            public string Display { get; init; } = string.Empty;
            public Font Font { get; init; } = SystemFonts.DefaultFont;

            public override string ToString() => Display;
        }

        #region Constants
        private const int GROUP_PADDING = 16;
        private const int CONTROL_SPACING = 12;
        private const int LABEL_WIDTH = 160;
        #endregion

        private SettingsSecretsPersistenceService? _settingsSecretsPersistenceService;
        public new object? DataContext { get; private set; }

        #region UI Control Fields
        // Header and main container
        private PanelHeader? _panelHeader;
        private StatusStrip? _statusStrip;
        private ToolStripStatusLabel? _statusLabel;

        // Appearance group
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
        private CheckBoxAdv? _chkEnableAi;
        private TextBoxExt? _txtXaiApiEndpoint;
        private TextBoxExt? _txtXaiApiKey;
        private Syncfusion.WinForms.Controls.SfButton? _btnShowApiKey;
        private TextBoxExt? _txtSyncfusionLicenseKey;
        private Syncfusion.WinForms.Controls.SfButton? _btnShowSyncfusionLicenseKey;
        private Syncfusion.WinForms.ListView.SfComboBox? _cmbXaiModel;
        private Syncfusion.WinForms.Input.SfNumericTextBox? _numXaiTimeout;
        private Syncfusion.WinForms.Input.SfNumericTextBox? _numXaiMaxTokens;
        private Syncfusion.WinForms.Input.SfNumericTextBox? _numXaiTemperature;
        private LinkLabel? _lnkAiLearnMore;

        // About group
        private Label? _lblVersion;
        private Label? _lblDbStatus;

        // Buttons
        private Syncfusion.WinForms.Controls.SfButton? _btnClose;
        private Syncfusion.WinForms.Controls.SfButton? _btnSave;

        // Validation and binding
        private ErrorProvider? _errorProvider;

        // Canonical skeleton fields
        private readonly SyncfusionControlFactory? _factory;
        private TableLayoutPanel? _content;
        private FlowLayoutPanel? _mainFlowPanel;
        private LoadingOverlay? _loader;
        private BindingSource? _bindingSource;
        private WileyWidget.WinForms.Controls.Supporting.ErrorProviderBinding? _errorBinding;

        private new SyncfusionControlFactory ControlFactory =>
            _factory ?? throw new InvalidOperationException("SettingsPanel SyncfusionControlFactory is not initialized.");

        // Tooltips
        private ToolTip? _tooltip;
        private ToolTip? _aiToolTip;
        #endregion

        #region Event Handler Fields
        private EventHandler? _fontSelectionChangedHandler;
        private EventHandler? _chkOpenEditFormsDockedHandler;
        private EventHandler? _chkUseDemoDataHandler;
        private EventHandler? _chkEnableAiHandler;
        private EventHandler? _chkEnableAiManualValueSyncHandler;
        private EventHandler? _themeComboSelectedHandler;
        private EventHandler? _txtXaiApiEndpointChangedHandler;
        private EventHandler? _cmbXaiModelSelectedHandler;
        private EventHandler? _txtXaiApiKeyChangedHandler;
        private EventHandler? _txtSyncfusionLicenseKeyChangedHandler;
        private Syncfusion.WinForms.Input.ValueChangedEventHandler? _numXaiTimeoutChangedHandler;
        private Syncfusion.WinForms.Input.ValueChangedEventHandler? _numXaiMaxTokensChangedHandler;
        private Syncfusion.WinForms.Input.ValueChangedEventHandler? _numXaiTemperatureChangedHandler;
        private EventHandler? _txtDateFormatChangedHandler;
        private EventHandler? _txtCurrencyFormatChangedHandler;
        private Syncfusion.WinForms.Input.ValueChangedEventHandler? _numAutoSaveIntervalChangedHandler;
        private EventHandler? _cmbLogLevelSelectedHandler;
        private EventHandler? _btnShowApiKeyClickHandler;
        private EventHandler? _btnShowSyncfusionLicenseKeyClickHandler;
        private LinkLabelLinkClickedEventHandler? _lnkAiLearnMoreHandler;
        private EventHandler? _btnBrowseExportPathClickHandler;
        private EventHandler? _btnSaveClickHandler;
        private EventHandler? _btnCloseClickHandler;
        private bool _isPanelInitialized;
        #endregion

        /// <summary>
        /// Constructor that accepts required dependencies from DI container.
        /// </summary>
        [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
        public SettingsPanel(SettingsViewModel viewModel, SyncfusionControlFactory controlFactory)
            : base(viewModel)
        {
            _factory = controlFactory ?? throw new ArgumentNullException(nameof(controlFactory));
            // Set preferred size for proper docking display (matches PreferredDockSize extension)
            Size = ScaleLogicalToDevice(new Size(1100, 760));
            MinimumSize = ScaleLogicalToDevice(new Size(1024, 720));
            SafeSuspendAndLayout(InitializeComponent);
            InitializeResolvedViewModelState();
        }

        /// <summary>
        /// Parameterless constructor for designer support ONLY.
        /// </summary>
        [Obsolete("Use DI constructor with SettingsViewModel and SyncfusionControlFactory", error: false)]
        internal SettingsPanel() : this(ResolveViewModel(), ResolveFactory())
        {
        }

        private static SettingsViewModel ResolveViewModel()
        {
            if (Program.Services == null)
            {
                Serilog.Log.Error("SettingsPanel: Program.Services is null - cannot resolve SettingsViewModel");
                throw new InvalidOperationException("SettingsPanel requires DI services to be initialized. Ensure Program.Services is set before creating SettingsPanel.");
            }
            try
            {
                var viewModel = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<SettingsViewModel>(Program.Services);
                Serilog.Log.Debug("SettingsPanel: SettingsViewModel resolved from DI container");
                return viewModel;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "SettingsPanel: Failed to resolve SettingsViewModel from DI");
                throw;
            }
        }

        private static SyncfusionControlFactory ResolveFactory()
        {
            if (Program.Services == null)
            {
                Serilog.Log.Error("SettingsPanel: Program.Services is null - cannot resolve SyncfusionControlFactory");
                throw new InvalidOperationException("SettingsPanel requires DI services to be initialized. Ensure Program.Services is set before creating SettingsPanel.");
            }
            try
            {
                var factory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<SyncfusionControlFactory>(Program.Services);
                Serilog.Log.Debug("SettingsPanel: SyncfusionControlFactory resolved from DI container");
                return factory;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "SettingsPanel: Failed to resolve SyncfusionControlFactory from DI");
                throw;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            MinimumSize = ScaleLogicalToDevice(new Size(1024, 720));
            PerformLayout();
        }

        /// <summary>
        /// Called after ViewModel is resolved from scoped provider.
        /// Performs UI setup and initial data binding.
        /// </summary>
        protected override void OnViewModelResolved(SettingsViewModel? viewModel)
        {
            if (viewModel == null)
            {
                Logger.LogWarning("SettingsPanel: ViewModel resolved as null — controls will not initialize.");
                return;
            }

            DataContext = viewModel;
            var provider = GetServiceProvider();
            _settingsSecretsPersistenceService = provider?.GetService(typeof(SettingsSecretsPersistenceService)) as SettingsSecretsPersistenceService;

            if (_factory == null)
            {
                return;
            }

            InitializeResolvedViewModelState();
        }

        private void InitializeResolvedViewModelState()
        {
            if (_isPanelInitialized || ViewModel == null)
            {
                return;
            }

            _isPanelInitialized = true;
            SafeSuspendAndLayout(() =>
            {
                BindViewModel();
                SetupEventHandlers();
                ApplyCurrentTheme();

                // Set initial font selection
                SetInitialFontSelection();
            });

            // Watch for unsaved changes - enable/disable Save button
            ViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SettingsViewModel.HasUnsavedChanges) && _btnSave != null)
                {
                    _btnSave.Enabled = ViewModel.HasUnsavedChanges;
                    UpdateStatus(
                        ViewModel.HasUnsavedChanges
                            ? "You have unsaved changes"
                            : "All changes saved",
                        isError: false);
                }
            };

            // Set initial Save button state
            if (_btnSave != null)
            {
                _btnSave.Enabled = ViewModel.HasUnsavedChanges;
            }

            try
            {
                BeginInvoke(new MethodInvoker(async () =>
                {
                    await Task.Delay(50);
                    await LoadAsync(RegisterOperation());
                }));
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "SettingsPanel: Failed to queue delayed LoadAsync");
            }
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

            // AI-specific validation
            if (_chkEnableAi?.Checked == true)
            {
                if (string.IsNullOrWhiteSpace(_txtXaiApiKey?.Text))
                    errors.Add(new ValidationItem("XaiApiKey", "xAI API key is required when AI is enabled", ValidationSeverity.Error, _txtXaiApiKey));
                if (!Uri.TryCreate(_txtXaiApiEndpoint?.Text, UriKind.Absolute, out _))
                    errors.Add(new ValidationItem("XaiApiEndpoint", "Valid xAI API endpoint is required when AI is enabled", ValidationSeverity.Error, _txtXaiApiEndpoint));
            }

            // Validate ViewModel properties via WileyWidget.WinForms.Controls.Supporting.ErrorProviderBinding
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

            // Set automation ID for UI testing
            this.Name = "Panel_Settings";

            AutoScaleMode = AutoScaleMode.Dpi;
            Padding = Padding.Empty;
            MinimumSize = ScaleLogicalToDevice(new Size(1024, 720));

            // Apply theme for cascade to all child controls
            SfSkinManager.SetVisualStyle(this, SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme);

            // Panel header
            _panelHeader = ControlFactory.CreatePanelHeader(header =>
            {
                header.Dock = DockStyle.Fill;
                header.Title = "Application Settings";
                header.ShowRefreshButton = false;
                header.ShowHelpButton = false;
                header.Height = LayoutTokens.GetScaled(LayoutTokens.HeaderHeightLarge);
            });
            _panelHeader.CloseClicked += (s, e) => ClosePanel();

            // Canonical _content root
            _content = ControlFactory.CreateTableLayoutPanel(table =>
            {
                table.Dock = DockStyle.Fill;
                table.ColumnCount = 1;
                table.RowCount = 2;
                table.Padding = Padding.Empty;
                table.Margin = Padding.Empty;
                table.AutoSize = false;
                table.Name = "SettingsPanelContent";
            });
            _content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _content.RowStyles.Add(new RowStyle(SizeType.Absolute, LayoutTokens.HeaderHeight));
            _content.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            _content.Controls.Add(_panelHeader, 0, 0);

            // Main container: FlowLayoutPanel for vertical stacking of groups
            _mainFlowPanel = ControlFactory.CreateFlowLayoutPanel(flow =>
            {
                flow.Dock = DockStyle.Fill;
                flow.FlowDirection = FlowDirection.TopDown;
                flow.AutoScroll = true;
                flow.Padding = new Padding(GROUP_PADDING);
                flow.WrapContents = false;
            });
            _content.Controls.Add(_mainFlowPanel, 0, 1);

            // Status strip (bottom)
            _statusStrip = ControlFactory.CreateStatusStrip(statusStrip =>
            {
                statusStrip.SizingGrip = false;
                statusStrip.Dock = DockStyle.Bottom;
            });
            _statusLabel = ControlFactory.CreateToolStripStatusLabel(label =>
            {
                label.Text = "Ready";
                label.Spring = true;
            });
            _statusStrip.Items.Add(_statusLabel);
            Controls.Add(_statusStrip);

            // Add content root to controls
            Controls.Add(_content);

            // Loading overlay
            if (_factory != null)
            {
                _loader = _factory.CreateLoadingOverlay(overlay =>
                {
                    overlay.Dock = DockStyle.Fill;
                    overlay.Visible = false;
                });
                Controls.Add(_loader);
            }

            _errorProvider = ControlFactory.CreateErrorProvider();
            _tooltip = ControlFactory.CreateToolTip();
            _aiToolTip = ControlFactory.CreateToolTip();

            // Group 1: Appearance (using GroupBox for visual separation)
            var appearanceGroup = ControlFactory.CreateGroupBox(SettingsPanelResources.AppearanceGroup);
            var appearanceTable = CreateTableLayoutPanel(3, new ColumnStyle(SizeType.Absolute, LABEL_WIDTH), new ColumnStyle(SizeType.Percent, 100F));  // 3 rows, 2 columns (label + control)
            appearanceTable.Controls.Add(CreateRowLabel(SettingsPanelResources.AppTitleLabel), 0, 0);
            _txtAppTitle = ControlFactory.CreateTextBoxExt(textBox =>
            {
                textBox.Name = "txtAppTitle";
                textBox.Width = 300;
                textBox.MaxLength = 100;
                textBox.AccessibleName = "Application Title";
                textBox.AccessibleDescription = "Set the friendly application title";
            });
            appearanceTable.Controls.Add(_txtAppTitle, 1, 0);
            _tooltip?.SetToolTip(_txtAppTitle, "Enter a custom title for the application window");
            appearanceTable.Controls.Add(CreateRowLabel("Theme:"), 0, 1);
            _themeCombo = ControlFactory.CreateSfComboBox(combo =>
            {
                combo.Name = "themeCombo";
                combo.Size = LayoutTokens.GetScaled(new Size(280, LayoutTokens.StandardControlHeightLarge));
                combo.MinimumSize = LayoutTokens.GetScaled(new Size(240, LayoutTokens.StandardControlHeightLarge));
                combo.AllowDropDownResize = false;
                combo.MaxDropDownItems = 5;
                combo.AccessibleName = "Theme";
                combo.DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList;
            });
            appearanceTable.Controls.Add(_themeCombo, 1, 1);
            PopulateThemeOptions();
            appearanceTable.Controls.Add(CreateRowLabel("Font:"), 0, 2);
            _fontCombo = ControlFactory.CreateSfComboBox(combo =>
            {
                combo.Name = "fontCombo";
                combo.Size = LayoutTokens.GetScaled(new Size(420, LayoutTokens.StandardControlHeightLarge));
                combo.MinimumSize = LayoutTokens.GetScaled(new Size(360, LayoutTokens.StandardControlHeightLarge));
                combo.AllowDropDownResize = false;
                combo.MaxDropDownItems = 10;
                combo.DropDownWidth = LayoutTokens.GetScaled(460);
                combo.DisplayMember = nameof(FontOption.Display);
            });
            appearanceTable.Controls.Add(_fontCombo, 1, 2);
            _fontCombo.DataSource = GetAvailableFonts();
            appearanceGroup.Controls.Add(appearanceTable);
            _mainFlowPanel.Controls.Add(appearanceGroup);

            // Group 2: General Settings
            var generalGroup = ControlFactory.CreateGroupBox("General Settings");
            var generalTable = CreateTableLayoutPanel(2, new ColumnStyle(SizeType.Absolute, LABEL_WIDTH), new ColumnStyle(SizeType.Percent, 100F));
            generalTable.Controls.Add(CreateRowLabel("Open edit forms docked:"), 0, 0);
            _chkOpenEditFormsDocked = ControlFactory.CreateCheckBoxAdv("", checkBox =>
            {
                checkBox.AutoSize = true;
                checkBox.Checked = ViewModel?.OpenEditFormsDocked ?? false;
                checkBox.AccessibleName = "Open edit forms docked";
            });
            generalTable.Controls.Add(_chkOpenEditFormsDocked, 1, 0);
            _tooltip?.SetToolTip(_chkOpenEditFormsDocked, "Open account edit forms as dockable floating windows instead of modal dialogs");
            generalTable.Controls.Add(CreateRowLabel("Use demo data:"), 0, 1);
            _chkUseDemoData = ControlFactory.CreateCheckBoxAdv("", checkBox =>
            {
                checkBox.AutoSize = true;
                checkBox.Checked = ViewModel?.UseDemoData ?? false;
                checkBox.AccessibleName = "Use demo data";
            });
            generalTable.Controls.Add(_chkUseDemoData, 1, 1);
            _tooltip?.SetToolTip(_chkUseDemoData, "Enable demo mode to display sample data instead of real database data.");
            generalGroup.Controls.Add(generalTable);
            _mainFlowPanel.Controls.Add(generalGroup);

            // Group 3: Data Export
            var exportGroup = ControlFactory.CreateGroupBox("Data Export");
            var exportTable = CreateTableLayoutPanel(1, new ColumnStyle(SizeType.Absolute, LABEL_WIDTH), new ColumnStyle(SizeType.Percent, 100F), new ColumnStyle(SizeType.Absolute, 112));
            exportTable.Controls.Add(CreateRowLabel("Path:"), 0, 0);
            _txtExportPath = ControlFactory.CreateTextBoxExt(textBox =>
            {
                textBox.Name = "txtExportPath";
                textBox.Width = 200;
                textBox.MaxLength = 260;
                textBox.AccessibleName = "Export path";
            });
            exportTable.Controls.Add(_txtExportPath, 1, 0);
            if (ViewModel != null && !string.IsNullOrEmpty(ViewModel.DefaultExportPath))
            {
                _txtExportPath.Text = ViewModel.DefaultExportPath;
            }
            _tooltip?.SetToolTip(_txtExportPath, "Directory where exported data files will be saved");
            _btnBrowseExportPath = ControlFactory.CreateSfButton("...", button =>
            {
                button.Name = "btnBrowseExportPath";
                button.Text = "Browse";
                button.Size = LayoutTokens.GetScaled(new Size(96, LayoutTokens.StandardControlHeightLarge));
                button.MinimumSize = LayoutTokens.GetScaled(new Size(96, LayoutTokens.StandardControlHeightLarge));
                button.Margin = Padding.Empty;
            });
            exportTable.Controls.Add(_btnBrowseExportPath, 2, 0);
            _tooltip?.SetToolTip(_btnBrowseExportPath, "Open folder browser to select export directory");
            exportGroup.Controls.Add(exportTable);
            _mainFlowPanel.Controls.Add(exportGroup);

            // Group 4: Behavior & Logging
            var behaviorGroup = ControlFactory.CreateGroupBox("Behavior & Logging");
            var behaviorTable = CreateTableLayoutPanel(2, new ColumnStyle(SizeType.Absolute, LABEL_WIDTH), new ColumnStyle(SizeType.Percent, 100F));
            behaviorTable.Controls.Add(CreateRowLabel("Auto-save (min):"), 0, 0);
            _numAutoSaveInterval = ControlFactory.CreateSfNumericTextBox(textBox =>
            {
                textBox.Name = "numAutoSaveInterval";
                textBox.Size = LayoutTokens.GetScaled(new Size(100, LayoutTokens.StandardControlHeightLarge));
                textBox.MinimumSize = LayoutTokens.GetScaled(new Size(100, LayoutTokens.StandardControlHeightLarge));
                textBox.MinValue = 1;
                textBox.MaxValue = 60;
                textBox.Value = ViewModel != null ? ViewModel.AutoSaveIntervalMinutes : 5;
            });
            behaviorTable.Controls.Add(_numAutoSaveInterval, 1, 0);
            _tooltip?.SetToolTip(_numAutoSaveInterval, "How often data is auto-saved (1-60 minutes)");
            behaviorTable.Controls.Add(CreateRowLabel("Log Level:"), 0, 1);
            _cmbLogLevel = ControlFactory.CreateSfComboBox(combo =>
            {
                combo.Name = "cmbLogLevel";
                combo.Size = LayoutTokens.GetScaled(new Size(180, LayoutTokens.StandardControlHeightLarge));
                combo.MinimumSize = LayoutTokens.GetScaled(new Size(180, LayoutTokens.StandardControlHeightLarge));
                combo.DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList;
            });
            behaviorTable.Controls.Add(_cmbLogLevel, 1, 1);
            var logLevels = new[] { "Verbose", "Debug", "Information", "Warning", "Error", "Fatal" }.ToList();
            _cmbLogLevel.DataSource = logLevels;
            _cmbLogLevel.SelectedItem = ViewModel != null ? ViewModel.LogLevel : "Information";
            _tooltip?.SetToolTip(_cmbLogLevel, "Verbosity level for application logging");
            behaviorGroup.Controls.Add(behaviorTable);
            _mainFlowPanel.Controls.Add(behaviorGroup);

            // Group 5: AI Settings
            var aiGroup = ControlFactory.CreateGroupBox("AI / xAI Settings");
            var aiTable = CreateTableLayoutPanel(9, new ColumnStyle(SizeType.Absolute, LABEL_WIDTH), new ColumnStyle(SizeType.Percent, 100F));
            aiTable.Controls.Add(CreateRowLabel("Enable AI:"), 0, 0);
            _chkEnableAi = ControlFactory.CreateCheckBoxAdv("", checkBox =>
            {
                checkBox.AutoSize = true;
                checkBox.Checked = ViewModel?.EnableAi ?? false;
            });
            aiTable.Controls.Add(_chkEnableAi, 1, 0);
            _aiToolTip?.SetToolTip(_chkEnableAi, "Enable or disable AI features.");
            aiTable.Controls.Add(CreateRowLabel("Endpoint:"), 0, 1);
            _txtXaiApiEndpoint = ControlFactory.CreateTextBoxExt(textBox =>
            {
                textBox.Name = "txtXaiApiEndpoint";
                textBox.Width = 250;
                textBox.MinimumSize = LayoutTokens.GetScaled(new Size(320, LayoutTokens.StandardControlHeightLarge));
                textBox.Height = LayoutTokens.GetScaled(LayoutTokens.StandardControlHeightLarge);
                textBox.MaxLength = 500;
                textBox.Text = ViewModel?.XaiApiEndpoint ?? string.Empty;
            });
            aiTable.Controls.Add(_txtXaiApiEndpoint, 1, 1);
            _aiToolTip?.SetToolTip(_txtXaiApiEndpoint, "API endpoint for xAI Grok.");
            aiTable.Controls.Add(CreateRowLabel("Model:"), 0, 2);
            _cmbXaiModel = ControlFactory.CreateSfComboBox(combo =>
            {
                combo.Name = "cmbXaiModel";
                combo.Size = LayoutTokens.GetScaled(new Size(260, LayoutTokens.StandardControlHeightLarge));
                combo.MinimumSize = LayoutTokens.GetScaled(new Size(220, LayoutTokens.StandardControlHeightLarge));
                combo.DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList;
            });
            aiTable.Controls.Add(_cmbXaiModel, 1, 2);
            try
            {
                _cmbXaiModel.DataSource = new[] { "grok-4.1", "grok-4-1-fast", "grok-4-1-fast-reasoning", "grok-4-1-fast-non-reasoning", "grok-3-2024" }.ToList();
                _cmbXaiModel.SelectedItem = ViewModel?.XaiModel ?? "grok-4.1";
            }
            catch { }
            _aiToolTip?.SetToolTip(_cmbXaiModel, "Select model used for recommendations.");
            aiTable.Controls.Add(CreateRowLabel("API Key:"), 0, 3);
            var apiKeyPanel = ControlFactory.CreateFlowLayoutPanel(flow =>
            {
                flow.FlowDirection = FlowDirection.LeftToRight;
                flow.AutoSize = false;
                flow.WrapContents = false;
                flow.Margin = Padding.Empty;
                flow.Padding = Padding.Empty;
                flow.Dock = DockStyle.Fill;
                flow.Height = LayoutTokens.GetScaled(LayoutTokens.StandardControlHeightLarge);
            });
            _txtXaiApiKey = ControlFactory.CreateTextBoxExt(textBox =>
            {
                textBox.Name = "txtXaiApiKey";
                textBox.Width = 240;
                textBox.MinimumSize = LayoutTokens.GetScaled(new Size(240, LayoutTokens.StandardControlHeightLarge));
                textBox.Height = LayoutTokens.GetScaled(LayoutTokens.StandardControlHeightLarge);
                textBox.MaxLength = 500;
                textBox.UseSystemPasswordChar = true;
                textBox.Text = ViewModel?.XaiApiKey ?? string.Empty;
            });
            apiKeyPanel.Controls.Add(_txtXaiApiKey);
            _btnShowApiKey = ControlFactory.CreateSfButton("Show", button =>
            {
                button.Name = "btnShowApiKey";
                button.Size = LayoutTokens.GetScaled(new Size(96, LayoutTokens.StandardControlHeightLarge));
                button.MinimumSize = LayoutTokens.GetScaled(new Size(96, LayoutTokens.StandardControlHeightLarge));
                button.Margin = new Padding(LayoutTokens.GetScaled(8), 0, 0, 0);
            });
            apiKeyPanel.Controls.Add(_btnShowApiKey);
            aiTable.Controls.Add(apiKeyPanel, 1, 3);
            _aiToolTip?.SetToolTip(_txtXaiApiKey, "API key stored securely (not logged).");
            aiTable.Controls.Add(CreateRowLabel("Syncfusion License:"), 0, 4);
            var syncfusionLicensePanel = ControlFactory.CreateFlowLayoutPanel(flow =>
            {
                flow.FlowDirection = FlowDirection.LeftToRight;
                flow.AutoSize = false;
                flow.WrapContents = false;
                flow.Margin = Padding.Empty;
                flow.Padding = Padding.Empty;
                flow.Dock = DockStyle.Fill;
                flow.Height = LayoutTokens.GetScaled(LayoutTokens.StandardControlHeightLarge);
            });
            _txtSyncfusionLicenseKey = ControlFactory.CreateTextBoxExt(textBox =>
            {
                textBox.Name = "txtSyncfusionLicenseKey";
                textBox.Width = 240;
                textBox.MinimumSize = LayoutTokens.GetScaled(new Size(240, LayoutTokens.StandardControlHeightLarge));
                textBox.Height = LayoutTokens.GetScaled(LayoutTokens.StandardControlHeightLarge);
                textBox.MaxLength = 500;
                textBox.UseSystemPasswordChar = true;
                textBox.Text = ViewModel?.SyncfusionLicenseKey ?? string.Empty;
            });
            syncfusionLicensePanel.Controls.Add(_txtSyncfusionLicenseKey);
            _btnShowSyncfusionLicenseKey = ControlFactory.CreateSfButton("Show", button =>
            {
                button.Name = "btnShowSyncfusionLicenseKey";
                button.Size = LayoutTokens.GetScaled(new Size(96, LayoutTokens.StandardControlHeightLarge));
                button.MinimumSize = LayoutTokens.GetScaled(new Size(96, LayoutTokens.StandardControlHeightLarge));
                button.Margin = new Padding(LayoutTokens.GetScaled(8), 0, 0, 0);
            });
            syncfusionLicensePanel.Controls.Add(_btnShowSyncfusionLicenseKey);
            aiTable.Controls.Add(syncfusionLicensePanel, 1, 4);
            _aiToolTip?.SetToolTip(_txtSyncfusionLicenseKey, "Syncfusion license key stored securely.");
            aiTable.Controls.Add(CreateRowLabel("Timeout (s):"), 0, 5);
            _numXaiTimeout = ControlFactory.CreateSfNumericTextBox(textBox =>
            {
                textBox.Name = "numXaiTimeout";
                textBox.Size = LayoutTokens.GetScaled(new Size(100, LayoutTokens.StandardControlHeightLarge));
                textBox.MinimumSize = LayoutTokens.GetScaled(new Size(100, LayoutTokens.StandardControlHeightLarge));
                textBox.MinValue = 1;
                textBox.MaxValue = 300;
                textBox.Value = ViewModel?.XaiTimeout ?? 30;
            });
            aiTable.Controls.Add(_numXaiTimeout, 1, 5);
            _aiToolTip?.SetToolTip(_numXaiTimeout, "Maximum time (seconds) to wait for response.");
            aiTable.Controls.Add(CreateRowLabel("Max tokens:"), 0, 6);
            _numXaiMaxTokens = ControlFactory.CreateSfNumericTextBox(textBox =>
            {
                textBox.Name = "numXaiMaxTokens";
                textBox.Size = LayoutTokens.GetScaled(new Size(120, LayoutTokens.StandardControlHeightLarge));
                textBox.MinimumSize = LayoutTokens.GetScaled(new Size(120, LayoutTokens.StandardControlHeightLarge));
                textBox.MinValue = 1;
                textBox.MaxValue = 65536;
                textBox.Value = ViewModel?.XaiMaxTokens ?? 2000;
            });
            aiTable.Controls.Add(_numXaiMaxTokens, 1, 6);
            aiTable.Controls.Add(CreateRowLabel("Temperature:"), 0, 7);
            _numXaiTemperature = ControlFactory.CreateSfNumericTextBox(textBox =>
            {
                textBox.Name = "numXaiTemperature";
                textBox.Size = LayoutTokens.GetScaled(new Size(100, LayoutTokens.StandardControlHeightLarge));
                textBox.MinimumSize = LayoutTokens.GetScaled(new Size(100, LayoutTokens.StandardControlHeightLarge));
                textBox.MinValue = 0.0;
                textBox.MaxValue = 1.0;
                textBox.Value = ViewModel?.XaiTemperature ?? 0.7;
            });
            aiTable.Controls.Add(_numXaiTemperature, 1, 7);
            _aiToolTip?.SetToolTip(_numXaiTemperature, "Response randomness (0=deterministic, 1=varied).");

            var aiHelpPanel = ControlFactory.CreateTableLayoutPanel(layout =>
            {
                layout.ColumnCount = 1;
                layout.RowCount = 2;
                layout.AutoSize = true;
                layout.Dock = DockStyle.Fill;
                layout.Margin = new Padding(0, LayoutTokens.GetScaled(4), 0, 0);
                layout.Padding = Padding.Empty;
                layout.MaximumSize = ScaleLogicalToDevice(new Size(980, 0));
            });
            aiHelpPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            aiHelpPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            aiHelpPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            aiHelpPanel.Controls.Add(ControlFactory.CreateLabel(label =>
            {
                label.Text = SettingsPanelResources.AiSettingsHelpShort;
                label.AutoSize = true;
                label.MaximumSize = ScaleLogicalToDevice(new Size(980, 0));
                label.Margin = Padding.Empty;
                label.UseMnemonic = false;
            }), 0, 0);
            aiHelpPanel.Controls.Add(_lnkAiLearnMore = ControlFactory.CreateLinkLabel(link =>
            {
                link.Text = SettingsPanelResources.AiSettingsLearnMoreLabel;
                link.AutoSize = true;
                link.Margin = new Padding(0, LayoutTokens.GetScaled(6), 0, 0);
            }), 0, 1);
            aiTable.Controls.Add(aiHelpPanel, 0, 8);
            aiTable.SetColumnSpan(aiHelpPanel, 2);
            aiGroup.Controls.Add(aiTable);
            _mainFlowPanel.Controls.Add(aiGroup);

            // Group 6: Display Formats
            var formatGroup = ControlFactory.CreateGroupBox("Display Formats");
            var formatTable = CreateTableLayoutPanel(1, new ColumnStyle(SizeType.Absolute, LABEL_WIDTH), new ColumnStyle(SizeType.Absolute, 130), new ColumnStyle(SizeType.Absolute, LABEL_WIDTH), new ColumnStyle(SizeType.Absolute, 90));
            formatTable.Controls.Add(CreateRowLabel("Date:"), 0, 0);
            _txtDateFormat = ControlFactory.CreateTextBoxExt(textBox =>
            {
                textBox.Name = "txtDateFormat";
                textBox.Width = 120;
                textBox.MaxLength = 50;
                textBox.Text = ViewModel?.DateFormat ?? "yyyy-MM-dd";
            });
            formatTable.Controls.Add(_txtDateFormat, 1, 0);
            formatTable.Controls.Add(CreateRowLabel("Currency:"), 2, 0);
            _txtCurrencyFormat = ControlFactory.CreateTextBoxExt(textBox =>
            {
                textBox.Name = "txtCurrencyFormat";
                textBox.Width = 80;
                textBox.MaxLength = 10;
                textBox.Text = ViewModel?.CurrencyFormat ?? "C2";
            });
            formatTable.Controls.Add(_txtCurrencyFormat, 3, 0);
            formatGroup.Controls.Add(formatTable);
            _mainFlowPanel.Controls.Add(formatGroup);

            // Group 7: About
            var aboutGroup = ControlFactory.CreateGroupBox(SettingsPanelResources.AboutGroup);
            var aboutFlow = ControlFactory.CreateFlowLayoutPanel(flow =>
            {
                flow.FlowDirection = FlowDirection.TopDown;
                flow.AutoSize = true;
            });
            aboutFlow.Controls.Add(_lblVersion = ControlFactory.CreateLabel(label =>
            {
                label.Text = $"Wiley Widget v1.0.0\n.NET {Environment.Version}\nRuntime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}";
                label.AutoSize = true;
            }));
            aboutFlow.Controls.Add(_lblDbStatus = ControlFactory.CreateLabel(label =>
            {
                label.Text = "Database: Connected";
                label.AutoSize = true;
            }));
            aboutGroup.Controls.Add(aboutFlow);
            _mainFlowPanel.Controls.Add(aboutGroup);

            // Bottom Buttons
            var buttonFlow = ControlFactory.CreateFlowLayoutPanel(flow =>
            {
                flow.FlowDirection = FlowDirection.RightToLeft;
                flow.AutoSize = true;
                flow.WrapContents = false;
                flow.Margin = Padding.Empty;
                flow.Padding = Padding.Empty;
            });
            _btnClose = ControlFactory.CreateSfButton("Close", button =>
            {
                button.Name = "btnClose";
                button.Width = 120;
                button.Height = LayoutTokens.GetScaled(LayoutTokens.StandardControlHeightLarge);
                button.Margin = Padding.Empty;
            });
            buttonFlow.Controls.Add(_btnClose);
            _tooltip?.SetToolTip(_btnClose, "Close this settings panel");
            _btnSave = ControlFactory.CreateSfButton("Save Settings", button =>
            {
                button.Name = "btnSave";
                button.Width = 160;
                button.Height = LayoutTokens.GetScaled(LayoutTokens.StandardControlHeightLarge);
                button.Margin = new Padding(LayoutTokens.GetScaled(8), 0, 0, 0);
                button.Enabled = false;
                button.AccessibleName = "Save Changes";
                button.Text = "Save Changes";
            });
            buttonFlow.Controls.Add(_btnSave);
            _tooltip?.SetToolTip(_btnSave, "Save all changes (validation runs first)");
            _mainFlowPanel.Controls.Add(buttonFlow);

            // Apply theme
            SfSkinManager.SetVisualStyle(this, ThemeColors.CurrentTheme);

            ResumeLayout(false);
        }

        // Helper: Create a TableLayoutPanel with auto-sizing columns/rows
        private TableLayoutPanel CreateTableLayoutPanel(int rowCount, params ColumnStyle[] columnStyles)
        {
            var rowHeight = LayoutTokens.GetScaled(LayoutTokens.StandardControlHeightLarge + 6);
            var table = ControlFactory.CreateTableLayoutPanel(layout =>
            {
                layout.RowCount = rowCount;
                layout.ColumnCount = columnStyles.Length;
                layout.AutoSize = true;
                layout.Padding = new Padding(CONTROL_SPACING, LayoutTokens.GetScaled(4), CONTROL_SPACING, LayoutTokens.GetScaled(4));
                layout.Margin = Padding.Empty;
                layout.Dock = DockStyle.Fill;
            });
            foreach (var style in columnStyles)
            {
                if (style.SizeType == SizeType.Absolute)
                {
                    table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, LayoutTokens.GetScaled((int)Math.Round(style.Width))));
                }
                else
                {
                    table.ColumnStyles.Add(style);
                }
            }
            for (int i = 0; i < rowCount; i++)
            {
                table.RowStyles.Add(new RowStyle(SizeType.Absolute, rowHeight));
            }
            return table;
        }

        private Label CreateRowLabel(string text)
        {
            return ControlFactory.CreateLabel(text, label =>
            {
                label.AutoSize = false;
                label.Dock = DockStyle.Fill;
                label.Margin = new Padding(0, 0, LayoutTokens.GetScaled(CONTROL_SPACING), 0);
                label.Padding = Padding.Empty;
                label.TextAlign = ContentAlignment.MiddleRight;
                label.UseMnemonic = false;
            });
        }

        private void PopulateThemeOptions()
        {
            if (_themeCombo == null)
            {
                return;
            }

            try
            {
                IReadOnlyList<string>? themeOptions = ViewModel?.AvailableThemes;

                if (themeOptions == null || themeOptions.Count == 0)
                {
                    themeOptions = ViewModel?.Themes;
                }

                if (themeOptions == null || themeOptions.Count == 0)
                {
                    themeOptions = ThemeColors.GetSupportedThemes();
                }

                var items = new List<string>(themeOptions.Where(static x => !string.IsNullOrWhiteSpace(x)));

                var selectedTheme = ViewModel?.SelectedTheme;
                if (!string.IsNullOrWhiteSpace(selectedTheme) && !items.Contains(selectedTheme, StringComparer.OrdinalIgnoreCase))
                {
                    items.Insert(0, selectedTheme);
                }

                _themeCombo.DataSource = null;
                _themeCombo.DataSource = items;

                if (!string.IsNullOrWhiteSpace(selectedTheme))
                {
                    _themeCombo.SelectedItem = selectedTheme;
                    _themeCombo.Text = selectedTheme;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "SettingsPanel: Error populating theme dropdown");
            }
        }
        #endregion

        #region Data Binding
        private void ClearExistingBindings()
        {
            // Clear bindings from all controls to prevent duplicates when handle is recreated
            _themeCombo?.DataBindings.Clear();
            _txtAppTitle?.DataBindings.Clear();
            _chkOpenEditFormsDocked?.DataBindings.Clear();
            _chkUseDemoData?.DataBindings.Clear();
            _txtExportPath?.DataBindings.Clear();
            _numAutoSaveInterval?.DataBindings.Clear();
            _cmbLogLevel?.DataBindings.Clear();
            _txtDateFormat?.DataBindings.Clear();
            _txtCurrencyFormat?.DataBindings.Clear();
            _chkEnableAi?.DataBindings.Clear();
            _txtXaiApiEndpoint?.DataBindings.Clear();
            _txtXaiApiKey?.DataBindings.Clear();
            _txtSyncfusionLicenseKey?.DataBindings.Clear();
            _cmbXaiModel?.DataBindings.Clear();
            _numXaiTimeout?.DataBindings.Clear();
            _numXaiMaxTokens?.DataBindings.Clear();
            _numXaiTemperature?.DataBindings.Clear();

            if (_chkEnableAi != null && _chkEnableAiManualValueSyncHandler != null)
            {
                _chkEnableAi.CheckedChanged -= _chkEnableAiManualValueSyncHandler;
                _chkEnableAiManualValueSyncHandler = null;
            }
        }

        private bool TryResolveBindingMember(string preferredMember, out string resolvedMember, params string[] alternateMembers)
        {
            resolvedMember = preferredMember;

            // If the binding source hasn't been initialized or has no DataSource, nothing to resolve.
            if (_bindingSource?.DataSource == null)
            {
                return false;
            }

            PropertyDescriptorCollection? descriptors = null;
            try
            {
                descriptors = _bindingSource.GetItemProperties(null);
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "SettingsPanel: GetItemProperties failed while resolving binding member '{BindingMember}'", preferredMember);
            }

            if ((descriptors == null || descriptors.Count == 0) && _bindingSource.DataSource != null)
            {
                descriptors = TypeDescriptor.GetProperties(_bindingSource.DataSource);
            }

            if (descriptors == null || descriptors.Count == 0)
            {
                return false;
            }

            var preferredDescriptor = descriptors.Find(preferredMember, true);
            if (preferredDescriptor != null)
            {
                resolvedMember = preferredDescriptor.Name;
                return true;
            }

            foreach (var alternate in alternateMembers)
            {
                if (string.IsNullOrWhiteSpace(alternate))
                {
                    continue;
                }

                var alternateDescriptor = descriptors.Find(alternate, true);
                if (alternateDescriptor != null)
                {
                    resolvedMember = alternateDescriptor.Name;
                    return true;
                }
            }

            return false;
        }

        private void UseManualEnableAiBindingFallback()
        {
            Logger.LogWarning(
                "SettingsPanel: Falling back to manual EnableAi synchronization for DataSource type {DataSourceType}",
                _bindingSource?.DataSource?.GetType().FullName ?? "<null>");

            if (_chkEnableAi == null)
            {
                return;
            }

            _chkEnableAi.Checked = ViewModel?.EnableAi ?? false;

            _chkEnableAiManualValueSyncHandler = (_, _) =>
            {
                if (ViewModel != null && _chkEnableAi != null)
                {
                    ViewModel.EnableAi = _chkEnableAi.Checked;
                }
            };

            _chkEnableAi.CheckedChanged += _chkEnableAiManualValueSyncHandler;

            if (ViewModel != null)
            {
                ViewModel.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(SettingsViewModel.EnableAi) && _chkEnableAi != null)
                    {
                        var desiredValue = ViewModel.EnableAi;
                        if (_chkEnableAi.Checked != desiredValue)
                        {
                            _chkEnableAi.Checked = desiredValue;
                        }
                    }
                };
            }
        }

        private void BindViewModel()
        {
            SetupBindings();
        }

        private void SetupBindings()
        {
            if (ViewModel == null)
            {
                return;
            }

            _bindingSource = ControlFactory.CreateBindingSource(ViewModel);

            var bindingSource = _bindingSource;

#pragma warning disable CS8604

            // Clear any existing bindings to prevent duplicates when handle is recreated
            ClearExistingBindings();

            // Theme binding
            if (_themeCombo != null && ViewModel != null)
            {
                _themeCombo.DataBindings.Add(
                    nameof(Syncfusion.WinForms.ListView.SfComboBox.Text),
                    bindingSource,
                    nameof(ViewModel.SelectedTheme),
                    true,
                    DataSourceUpdateMode.OnPropertyChanged);
            }

            // General settings bindings
            if (_txtAppTitle != null)
            {
                _txtAppTitle.DataBindings.Add(
                    nameof(TextBoxExt.Text),
                    bindingSource,
                    nameof(ViewModel.AppTitle),
                    true,
                    DataSourceUpdateMode.OnPropertyChanged);
            }

            if (_chkOpenEditFormsDocked != null)
            {
                _chkOpenEditFormsDocked.DataBindings.Add(
                    nameof(CheckBoxAdv.Checked),
                    bindingSource,
                    nameof(ViewModel.OpenEditFormsDocked),
                    true,
                    DataSourceUpdateMode.OnPropertyChanged);
            }

            if (_chkUseDemoData != null)
            {
                _chkUseDemoData.DataBindings.Add(
                    nameof(CheckBoxAdv.Checked),
                    bindingSource,
                    nameof(ViewModel.UseDemoData),
                    true,
                    DataSourceUpdateMode.OnPropertyChanged);
            }

            if (_txtExportPath != null)
            {
                _txtExportPath.DataBindings.Add(
                    nameof(TextBoxExt.Text),
                    bindingSource,
                    nameof(ViewModel.DefaultExportPath),
                    true,
                    DataSourceUpdateMode.OnPropertyChanged);
            }

            if (_numAutoSaveInterval != null)
            {
                _numAutoSaveInterval.DataBindings.Add(
                    nameof(Syncfusion.WinForms.Input.SfNumericTextBox.Value),
                    bindingSource,
                    nameof(ViewModel.AutoSaveIntervalMinutes),
                    true,
                    DataSourceUpdateMode.OnPropertyChanged);
            }

            if (_cmbLogLevel != null)
            {
                _cmbLogLevel.DataBindings.Add(
                    nameof(Syncfusion.WinForms.ListView.SfComboBox.SelectedItem),
                    bindingSource,
                    nameof(ViewModel.LogLevel),
                    true,
                    DataSourceUpdateMode.OnPropertyChanged);
            }

            if (_txtDateFormat != null)
            {
                _txtDateFormat.DataBindings.Add(
                    nameof(TextBoxExt.Text),
                    bindingSource,
                    nameof(ViewModel.DateFormat),
                    true,
                    DataSourceUpdateMode.OnPropertyChanged);
            }

            if (_txtCurrencyFormat != null)
            {
                _txtCurrencyFormat.DataBindings.Add(
                    nameof(TextBoxExt.Text),
                    bindingSource,
                    nameof(ViewModel.CurrencyFormat),
                    true,
                    DataSourceUpdateMode.OnPropertyChanged);
            }

            // AI bindings
            if (_chkEnableAi != null)
            {
                if (TryResolveBindingMember(nameof(ViewModel.EnableAi), out var enableAiMember, "EnableAI"))
                {
                    try
                    {
                        _chkEnableAi.DataBindings.Add(
                            nameof(CheckBoxAdv.Checked),
                            bindingSource!,
                            enableAiMember,
                            true,
                            DataSourceUpdateMode.OnPropertyChanged);
                    }
                    catch (ArgumentException ex)
                    {
                        Logger.LogWarning(
                            ex,
                            "SettingsPanel: Failed to bind EnableAi using member '{BindingMember}' on DataSource type {DataSourceType}",
                            enableAiMember,
                            _bindingSource?.DataSource?.GetType().FullName ?? "<null>");
                        UseManualEnableAiBindingFallback();
                    }
                }
                else
                {
                    Logger.LogWarning("SettingsPanel: No compatible bindable member found for EnableAi/EnableAI on DataSource type {DataSourceType}; using manual sync fallback", _bindingSource?.DataSource?.GetType().FullName ?? "<null>");
                    UseManualEnableAiBindingFallback();
                }
            }

            if (_txtXaiApiEndpoint != null)
            {
                _txtXaiApiEndpoint.DataBindings.Add(
                    nameof(TextBox.Text),
                    bindingSource,
                    nameof(ViewModel.XaiApiEndpoint),
                    true,
                    DataSourceUpdateMode.OnPropertyChanged);
            }

            if (_txtXaiApiKey != null)
            {
                _txtXaiApiKey.DataBindings.Add(
                    nameof(TextBox.Text),
                    bindingSource,
                    nameof(ViewModel.XaiApiKey),
                    true,
                    DataSourceUpdateMode.OnPropertyChanged);
            }

            if (_txtSyncfusionLicenseKey != null)
            {
                _txtSyncfusionLicenseKey.DataBindings.Add(
                    nameof(TextBox.Text),
                    bindingSource,
                    nameof(ViewModel.SyncfusionLicenseKey),
                    true,
                    DataSourceUpdateMode.OnPropertyChanged);
            }

            if (_cmbXaiModel != null)
            {
                _cmbXaiModel.DataBindings.Add(
                    nameof(Syncfusion.WinForms.ListView.SfComboBox.SelectedItem),
                    bindingSource,
                    nameof(ViewModel.XaiModel),
                    true,
                    DataSourceUpdateMode.OnPropertyChanged);
            }

            if (_numXaiTimeout != null)
            {
                _numXaiTimeout.DataBindings.Add(
                    nameof(Syncfusion.WinForms.Input.SfNumericTextBox.Value),
                    bindingSource,
                    nameof(ViewModel.XaiTimeout),
                    true,
                    DataSourceUpdateMode.OnPropertyChanged);
            }

            if (_numXaiMaxTokens != null)
            {
                _numXaiMaxTokens.DataBindings.Add(
                    nameof(Syncfusion.WinForms.Input.SfNumericTextBox.Value),
                    bindingSource,
                    nameof(ViewModel.XaiMaxTokens),
                    true,
                    DataSourceUpdateMode.OnPropertyChanged);
            }

            if (_numXaiTemperature != null)
            {
                _numXaiTemperature.DataBindings.Add(
                    nameof(Syncfusion.WinForms.Input.SfNumericTextBox.Value),
                    bindingSource,
                    nameof(ViewModel.XaiTemperature),
                    true,
                    DataSourceUpdateMode.OnPropertyChanged);
            }

#pragma warning restore CS8604

            // Setup WileyWidget.WinForms.Controls.Supporting.ErrorProviderBinding
            try
            {
                if (_errorProvider != null && ViewModel != null)
                {
                    _errorBinding = new WileyWidget.WinForms.Controls.Supporting.ErrorProviderBinding(_errorProvider, ViewModel);
                    _errorBinding.MapControl(nameof(ViewModel.AppTitle), _txtAppTitle!);
                    _errorBinding.MapControl(nameof(ViewModel.DefaultExportPath), _txtExportPath!);
                    _errorBinding.MapControl(nameof(ViewModel.DateFormat), _txtDateFormat!);
                    _errorBinding.MapControl(nameof(ViewModel.CurrencyFormat), _txtCurrencyFormat!);
                    _errorBinding.MapControl(nameof(ViewModel.LogLevel), _cmbLogLevel!);

                    try { _errorBinding.MapControl(nameof(ViewModel.XaiApiEndpoint), _txtXaiApiEndpoint!); } catch { }
                    try { _errorBinding.MapControl(nameof(ViewModel.XaiApiKey), _txtXaiApiKey!); } catch { }
                    try { _errorBinding.MapControl(nameof(ViewModel.SyncfusionLicenseKey), _txtSyncfusionLicenseKey!); } catch { }
                    try { _errorBinding.MapControl(nameof(ViewModel.XaiModel), _cmbXaiModel!); } catch { }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "SettingsPanel: Failed to setup WileyWidget.WinForms.Controls.Supporting.ErrorProviderBinding");
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

            _themeComboSelectedHandler = (s, e) =>
            {
                try
                {
                    if (!IsLoaded) return;

                    // Ensure ViewModel.SelectedTheme is updated immediately when user picks a theme
                    // Some versions of Syncfusion SfComboBox do not push SelectedItem bindings reliably
                    // on selection changes, so set the ViewModel explicitly to trigger theme application.
                    var selectedTheme = _themeCombo?.Text;

                    if (!string.IsNullOrWhiteSpace(selectedTheme) && ViewModel != null)
                    {
                        if (!string.Equals(ViewModel.SelectedTheme, selectedTheme, StringComparison.Ordinal))
                        {
                            ViewModel.SelectedTheme = selectedTheme; // triggers SettingsViewModel.OnSelectedThemeChanged -> ThemeService.ApplyTheme
                        }

                        ApplyCurrentTheme();
                    }

                    SetHasUnsavedChanges(true);
                }
                catch (Exception ex)
                {
                    Logger.LogDebug(ex, "SettingsPanel: theme selection handler error");
                }
            };
            if (_themeCombo != null) _themeCombo.SelectedIndexChanged += _themeComboSelectedHandler;

            _txtXaiApiEndpointChangedHandler = (s, e) => { if (IsLoaded) SetHasUnsavedChanges(true); };
            if (_txtXaiApiEndpoint != null) _txtXaiApiEndpoint.TextChanged += _txtXaiApiEndpointChangedHandler;

            _cmbXaiModelSelectedHandler = (s, e) => { if (IsLoaded) SetHasUnsavedChanges(true); };
            if (_cmbXaiModel != null) _cmbXaiModel.SelectedIndexChanged += _cmbXaiModelSelectedHandler;

            _txtXaiApiKeyChangedHandler = (s, e) => { if (IsLoaded) SetHasUnsavedChanges(true); };
            if (_txtXaiApiKey != null) _txtXaiApiKey.TextChanged += _txtXaiApiKeyChangedHandler;

            _txtSyncfusionLicenseKeyChangedHandler = (s, e) => { if (IsLoaded) SetHasUnsavedChanges(true); };
            if (_txtSyncfusionLicenseKey != null) _txtSyncfusionLicenseKey.TextChanged += _txtSyncfusionLicenseKeyChangedHandler;

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

            _btnShowSyncfusionLicenseKeyClickHandler = (s, e) =>
            {
                if (_txtSyncfusionLicenseKey != null)
                {
                    _txtSyncfusionLicenseKey.UseSystemPasswordChar = !_txtSyncfusionLicenseKey.UseSystemPasswordChar;
                    if (s is Syncfusion.WinForms.Controls.SfButton btn)
                    {
                        btn.Text = _txtSyncfusionLicenseKey.UseSystemPasswordChar ? "Show" : "Hide";
                    }
                }
            };
            if (_btnShowSyncfusionLicenseKey != null) _btnShowSyncfusionLicenseKey.Click += _btnShowSyncfusionLicenseKeyClickHandler;

            _lnkAiLearnMoreHandler = (s, e) => ShowAiHelpDialog();
            if (_lnkAiLearnMore != null) _lnkAiLearnMore.LinkClicked += _lnkAiLearnMoreHandler;

            _btnBrowseExportPathClickHandler = (s, e) => OnBrowseExportPath();
            if (_btnBrowseExportPath != null) _btnBrowseExportPath.Click += _btnBrowseExportPathClickHandler;

            _btnSaveClickHandler = BtnSave_Click;
            if (_btnSave != null) _btnSave.Click += _btnSaveClickHandler;

            _btnCloseClickHandler = BtnClose_Click;
            if (_btnClose != null) _btnClose.Click += _btnCloseClickHandler;
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

                if (_settingsSecretsPersistenceService != null && ViewModel != null)
                {
                    var persistenceResult = await _settingsSecretsPersistenceService
                        .PersistAsync(ViewModel.SyncfusionLicenseKey, ViewModel.XaiApiKey, CancellationToken.None)
                        .ConfigureAwait(true);

                    if (!persistenceResult.Success)
                    {
                        var message = string.IsNullOrWhiteSpace(persistenceResult.ErrorMessage)
                            ? "Unable to persist secure keys to user-secrets/environment."
                            : persistenceResult.ErrorMessage;

                        UpdateStatus(message, isError: true);
                        MessageBox.Show(
                            this,
                            message,
                            SettingsPanelResources.SecretSaveFailedTitle,
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                        return;
                    }

                    if (persistenceResult.Warnings.Count > 0)
                    {
                        MessageBox.Show(
                            this,
                            string.Join(Environment.NewLine, persistenceResult.Warnings),
                            SettingsPanelResources.SecretSaveWarningsTitle,
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }
                }

                ViewModel?.SaveCommand?.Execute(null);

                UpdateStatus("Settings saved successfully", isError: false);
                SetHasUnsavedChanges(false);
                Logger.LogInformation("SettingsPanel: Settings saved");

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
                Logger.LogError(ex, "SettingsPanel: Save failed");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void OnBrowseExportPath()
        {
            using var folderDialog = ControlFactory.CreateFolderBrowserDialog(dialog =>
            {
                dialog.Description = "Select Export Directory";
                dialog.InitialDirectory = Directory.Exists(ViewModel?.DefaultExportPath)
                    ? ViewModel.DefaultExportPath
                    : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            });
            if (folderDialog.ShowDialog(this) == DialogResult.OK && ViewModel != null)
            {
                ViewModel.DefaultExportPath = folderDialog.SelectedPath;
                SetHasUnsavedChanges(true);
            }
        }

        private void ApplyCurrentTheme()
        {
            try
            {
                var currentTheme = ThemeColors.ValidateTheme(
                    Syncfusion.WinForms.Controls.SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme,
                    Logger);

                ThemeColors.EnsureThemeAssemblyLoadedForTheme(currentTheme, Logger);
                ApplyThemeRecursively(this, currentTheme);

                var parentForm = FindForm();
                if (parentForm != null)
                {
                    parentForm.ApplySyncfusionTheme(currentTheme, Logger);
                }
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "SettingsPanel: failed to reapply current theme");
            }
        }

        private void ApplyThemeRecursively(Control? control, string themeName)
        {
            if (control == null || control.IsDisposed)
            {
                return;
            }

            control.ApplySyncfusionTheme(themeName, Logger);

            foreach (Control child in control.Controls)
            {
                ApplyThemeRecursively(child, themeName);
            }
        }

        private async Task LoadViewDataAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                Logger.LogDebug("SettingsPanel: LoadViewDataAsync starting");
                if (ViewModel != null)
                {
                    await (ViewModel.LoadCommand?.ExecuteAsync(null) ?? Task.CompletedTask);

                    if (_settingsSecretsPersistenceService != null)
                    {
                        var secretsSnapshot = await _settingsSecretsPersistenceService.LoadCurrentAsync(cancellationToken).ConfigureAwait(true);
                        ViewModel.SyncfusionLicenseKey = secretsSnapshot.SyncfusionLicenseKey ?? string.Empty;
                        ViewModel.XaiApiKey = string.IsNullOrWhiteSpace(secretsSnapshot.XaiApiKey)
                            ? ViewModel.XaiApiKey
                            : secretsSnapshot.XaiApiKey;
                    }

                    PopulateThemeOptions();

                    Logger.LogInformation("SettingsPanel: settings loaded successfully");
                    SetHasUnsavedChanges(false);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "SettingsPanel: LoadViewDataAsync failed");
            }
        }

        private async void BtnClose_Click(object? sender, EventArgs e)
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
                        var validationResult = await ValidateAsync(CancellationToken.None).ConfigureAwait(true);
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
                        catch (Exception ex) { Logger.LogWarning(ex, "SettingsPanel: Failed to save settings on close"); }
                    }
                }

                var parentForm = this.FindForm();
                if (parentForm is Forms.MainForm mainForm)
                {
                    mainForm.CloseSettingsPanel();
                    return;
                }

                // Fallback: hide the panel directly
                this.Visible = false;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "SettingsPanel: BtnClose_Click failed");
            }
        }

        private List<FontOption> GetAvailableFonts()
        {
            var fonts = new List<FontOption>();
            var fontFamilies = new[] { "Segoe UI", "Calibri", "Arial", "Tahoma", "Verdana" };
            var fontSizes = new[] { 9f, 10f, 11f };

            foreach (var familyName in fontFamilies)
            {
                try
                {
                    var family = new FontFamily(familyName);
                    if (!family.IsStyleAvailable(FontStyle.Regular))
                    {
                        continue;
                    }

                    foreach (var fontSize in fontSizes)
                    {
                        var font = new Font(familyName, fontSize, FontStyle.Regular);
                        fonts.Add(new FontOption
                        {
                            Display = $"{familyName} {fontSize:0.#} pt",
                            Font = font
                        });
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
                if (_fontCombo?.SelectedItem is FontOption selectedFontOption)
                {
                    var selectedFont = selectedFontOption.Font;
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
                Logger.LogWarning(ex, "SettingsPanel: Font selection change failed");
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
                Logger.LogWarning(ex, "SettingsPanel: ShowAiHelpDialog failed");
                MessageBox.Show(this, SettingsPanelResources.AiSettingsHelpShort, SettingsPanelResources.AiSettingsDialogTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void SetInitialFontSelection()
        {
            try
            {
                if (ViewModel?.ApplicationFont != null && _fontCombo != null)
                {
                    var desiredFont = ParseFontString(ViewModel.ApplicationFont);
                    if (desiredFont != null && _fontCombo.DataSource is List<FontOption> fontOptions)
                    {
                        foreach (var fontOption in fontOptions)
                        {
                            if (string.Equals(fontOption.Font.FontFamily.Name, desiredFont.FontFamily.Name, StringComparison.OrdinalIgnoreCase)
                                && Math.Abs(fontOption.Font.Size - desiredFont.Size) < 0.1f)
                            {
                                _fontCombo.SelectedItem = fontOption;
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "SettingsPanel: Failed to set initial font selection");
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
                Logger.LogWarning(ex, "Failed to parse font string: {FontString}", fontString);
            }
            return null;
        }

        private void UpdateStatus(string message, bool isError = false)
        {
            // Always marshal to UI thread (non-blocking) to protect against cross-thread updates
            this.InvokeIfRequired(() =>
            {
                try
                {
                    if (_statusLabel != null && !_statusLabel.IsDisposed)
                    {
                        _statusLabel.Text = message ?? string.Empty;
                        _statusLabel.ForeColor = isError ? Color.Red : Color.Empty;
                        try { _statusLabel.Invalidate(); } catch { }
                    }
                }
                catch { }
            });
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
                try { if (_chkEnableAi != null && _chkEnableAiManualValueSyncHandler != null) _chkEnableAi.CheckedChanged -= _chkEnableAiManualValueSyncHandler; } catch { }
                try { if (_themeCombo != null) _themeCombo.SelectedIndexChanged -= _themeComboSelectedHandler; } catch { }
                try { if (_txtXaiApiEndpoint != null) _txtXaiApiEndpoint.TextChanged -= _txtXaiApiEndpointChangedHandler; } catch { }
                try { if (_cmbXaiModel != null) _cmbXaiModel.SelectedIndexChanged -= _cmbXaiModelSelectedHandler; } catch { }
                try { if (_txtXaiApiKey != null) _txtXaiApiKey.TextChanged -= _txtXaiApiKeyChangedHandler; } catch { }
                try { if (_txtSyncfusionLicenseKey != null) _txtSyncfusionLicenseKey.TextChanged -= _txtSyncfusionLicenseKeyChangedHandler; } catch { }
                try { if (_numXaiTimeout != null) _numXaiTimeout.ValueChanged -= _numXaiTimeoutChangedHandler; } catch { }
                try { if (_numXaiMaxTokens != null) _numXaiMaxTokens.ValueChanged -= _numXaiMaxTokensChangedHandler; } catch { }
                try { if (_numXaiTemperature != null) _numXaiTemperature.ValueChanged -= _numXaiTemperatureChangedHandler; } catch { }
                try { if (_txtDateFormat != null) _txtDateFormat.TextChanged -= _txtDateFormatChangedHandler; } catch { }
                try { if (_txtCurrencyFormat != null) _txtCurrencyFormat.TextChanged -= _txtCurrencyFormatChangedHandler; } catch { }
                try { if (_numAutoSaveInterval != null) _numAutoSaveInterval.ValueChanged -= _numAutoSaveIntervalChangedHandler; } catch { }
                try { if (_cmbLogLevel != null) _cmbLogLevel.SelectedIndexChanged -= _cmbLogLevelSelectedHandler; } catch { }
                try { if (_btnShowApiKey != null) _btnShowApiKey.Click -= _btnShowApiKeyClickHandler; } catch { }
                try { if (_btnShowSyncfusionLicenseKey != null) _btnShowSyncfusionLicenseKey.Click -= _btnShowSyncfusionLicenseKeyClickHandler; } catch { }
                try { if (_lnkAiLearnMore != null) _lnkAiLearnMore.LinkClicked -= _lnkAiLearnMoreHandler; } catch { }
                try { if (_btnBrowseExportPath != null) _btnBrowseExportPath.Click -= _btnBrowseExportPathClickHandler; } catch { }
                try { if (_btnSave != null) _btnSave.Click -= _btnSaveClickHandler; } catch { }
                try { if (_btnClose != null) _btnClose.Click -= _btnCloseClickHandler; } catch { }

                // Dispose controls and resources
                try { if (_themeCombo != null && !_themeCombo.IsDisposed) { try { _themeCombo.DataSource = null; } catch { } _themeCombo.Dispose(); } } catch { }
                try { if (_fontCombo != null && !_fontCombo.IsDisposed) { try { _fontCombo.DataSource = null; } catch { } _fontCombo.Dispose(); } } catch { }
                try { if (_cmbLogLevel != null && !_cmbLogLevel.IsDisposed) { try { _cmbLogLevel.DataSource = null; } catch { } _cmbLogLevel.Dispose(); } } catch { }
                try { if (_cmbXaiModel != null && !_cmbXaiModel.IsDisposed) { try { _cmbXaiModel.DataSource = null; } catch { } _cmbXaiModel.Dispose(); } } catch { }
                try { _aiToolTip?.Dispose(); } catch { }
                try { _tooltip?.Dispose(); } catch { }
                try { _lnkAiLearnMore?.Dispose(); } catch { }
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
                try { _txtSyncfusionLicenseKey?.Dispose(); } catch { }
                try { _btnShowSyncfusionLicenseKey?.Dispose(); } catch { }
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
