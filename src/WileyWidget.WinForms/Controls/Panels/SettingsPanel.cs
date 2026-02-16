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
        private BindingSource? _bindingSource;
        private WileyWidget.WinForms.Controls.Supporting.ErrorProviderBinding? _errorBinding;

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
        private EventHandler? _btnBrowseExportPathClickHandler;
        #endregion

        /// <summary>
        /// Constructor that accepts required dependencies from DI container.
        /// </summary>
        public SettingsPanel(
            IServiceScopeFactory scopeFactory,
            Microsoft.Extensions.Logging.ILogger<ScopedPanelBase<SettingsViewModel>> logger)
            : base(scopeFactory, logger)
        {
            // Set preferred size for proper docking display (matches PreferredDockSize extension)
            Size = new Size(500, 360);
            MinimumSize = new Size(420, 360);
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
        protected override void OnViewModelResolved(object? viewModel)
        {
            base.OnViewModelResolved(viewModel);
            if (viewModel is not SettingsViewModel typedViewModel)
            {
                return;
            }

            DataContext = typedViewModel;

            InitializeComponent();
            SetupBindings();
            SetupEventHandlers();
            ApplyCurrentTheme();

            // Set initial font selection
            SetInitialFontSelection();

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
            
            AutoScaleMode = AutoScaleMode.Font;
            Padding = Padding.Empty;

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

            // Main container: FlowLayoutPanel for vertical stacking of groups
            var mainFlowPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                AutoScroll = true,
                Padding = new Padding(GROUP_PADDING)
            };
            Controls.Add(mainFlowPanel);

            _errorProvider = new ErrorProvider { BlinkStyle = ErrorBlinkStyle.NeverBlink };
            _tooltip = new ToolTip();
            _aiToolTip = new ToolTip();

            // Group 1: Appearance (using GroupBox for visual separation)
            var appearanceGroup = new GroupBox { Text = SettingsPanelResources.AppearanceGroup, AutoSize = true };
            var appearanceTable = CreateTableLayoutPanel(3, new ColumnStyle(SizeType.Absolute, LABEL_WIDTH), new ColumnStyle(SizeType.Percent, 100F));  // 3 rows, 2 columns (label + control)
            appearanceTable.Controls.Add(new Label { Text = SettingsPanelResources.AppTitleLabel, Anchor = AnchorStyles.Left }, 0, 0);
            _txtAppTitle = ControlFactory.CreateTextBoxExt(textBox =>
            {
                textBox.Name = "txtAppTitle";
                textBox.Width = 300;
                textBox.MaxLength = 100;
                textBox.Font = new Font("Segoe UI", 10F);
                textBox.AccessibleName = "Application Title";
                textBox.AccessibleDescription = "Set the friendly application title";
            });
            appearanceTable.Controls.Add(_txtAppTitle, 1, 0);
            _tooltip?.SetToolTip(_txtAppTitle, "Enter a custom title for the application window");
            appearanceTable.Controls.Add(new Label { Text = "Theme:", Anchor = AnchorStyles.Left }, 0, 1);
            _themeCombo = ControlFactory.CreateSfComboBox(combo =>
            {
                combo.Name = "themeCombo";
                combo.Size = new Size(220, 24);
                combo.AllowDropDownResize = false;
                combo.MaxDropDownItems = 5;
            });
            appearanceTable.Controls.Add(_themeCombo, 1, 1);
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
            appearanceTable.Controls.Add(new Label { Text = "Font:", Anchor = AnchorStyles.Left }, 0, 2);
            _fontCombo = ControlFactory.CreateSfComboBox(combo =>
            {
                combo.Name = "fontCombo";
                combo.Size = new Size(220, 24);
                combo.AllowDropDownResize = false;
                combo.MaxDropDownItems = 10;
            });
            appearanceTable.Controls.Add(_fontCombo, 1, 2);
            _fontCombo.DropDownListView.Style.ItemStyle.Font = new Font("Segoe UI", 10F);
            _fontCombo.DataSource = GetAvailableFonts();
            appearanceGroup.Controls.Add(appearanceTable);
            mainFlowPanel.Controls.Add(appearanceGroup);

            // Group 2: General Settings
            var generalGroup = new GroupBox { Text = "General Settings", AutoSize = true };
            var generalTable = CreateTableLayoutPanel(2, new ColumnStyle(SizeType.Absolute, LABEL_WIDTH), new ColumnStyle(SizeType.Percent, 100F));
            generalTable.Controls.Add(new Label { Text = "Open edit forms docked:", Anchor = AnchorStyles.Left }, 0, 0);
            _chkOpenEditFormsDocked = ControlFactory.CreateCheckBoxAdv("", checkBox =>
            {
                checkBox.AutoSize = true;
                checkBox.Checked = ViewModel?.OpenEditFormsDocked ?? false;
                checkBox.Font = new Font("Segoe UI", 9);
                checkBox.AccessibleName = "Open edit forms docked";
            });
            generalTable.Controls.Add(_chkOpenEditFormsDocked, 1, 0);
            _tooltip?.SetToolTip(_chkOpenEditFormsDocked, "Open account edit forms as dockable floating windows instead of modal dialogs");
            generalTable.Controls.Add(new Label { Text = "Use demo data:", Anchor = AnchorStyles.Left }, 0, 1);
            _chkUseDemoData = ControlFactory.CreateCheckBoxAdv("", checkBox =>
            {
                checkBox.AutoSize = true;
                checkBox.Checked = ViewModel?.UseDemoData ?? false;
                checkBox.Font = new Font("Segoe UI", 9);
                checkBox.AccessibleName = "Use demo data";
            });
            generalTable.Controls.Add(_chkUseDemoData, 1, 1);
            _tooltip?.SetToolTip(_chkUseDemoData, "Enable demo mode to display sample data instead of real database data.");
            generalGroup.Controls.Add(generalTable);
            mainFlowPanel.Controls.Add(generalGroup);

            // Group 3: Data Export
            var exportGroup = new GroupBox { Text = "Data Export", AutoSize = true };
            var exportTable = CreateTableLayoutPanel(1, new ColumnStyle(SizeType.Absolute, LABEL_WIDTH), new ColumnStyle(SizeType.Percent, 100F), new ColumnStyle(SizeType.Absolute, 50));
            exportTable.Controls.Add(new Label { Text = "Path:", Anchor = AnchorStyles.Left }, 0, 0);
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
                button.Size = new Size(40, 24);
            });
            exportTable.Controls.Add(_btnBrowseExportPath, 2, 0);
            _tooltip?.SetToolTip(_btnBrowseExportPath, "Open folder browser to select export directory");
            exportGroup.Controls.Add(exportTable);
            mainFlowPanel.Controls.Add(exportGroup);

            // Group 4: Behavior & Logging
            var behaviorGroup = new GroupBox { Text = "Behavior & Logging", AutoSize = true };
            var behaviorTable = CreateTableLayoutPanel(2, new ColumnStyle(SizeType.Absolute, LABEL_WIDTH), new ColumnStyle(SizeType.Percent, 100F));
            behaviorTable.Controls.Add(new Label { Text = "Auto-save (min):", Anchor = AnchorStyles.Left }, 0, 0);
            _numAutoSaveInterval = ControlFactory.CreateSfNumericTextBox(textBox =>
            {
                textBox.Name = "numAutoSaveInterval";
                textBox.Size = new Size(80, 24);
                textBox.MinValue = 1;
                textBox.MaxValue = 60;
                textBox.Value = ViewModel != null ? ViewModel.AutoSaveIntervalMinutes : 5;
            });
            behaviorTable.Controls.Add(_numAutoSaveInterval, 1, 0);
            _tooltip?.SetToolTip(_numAutoSaveInterval, "How often data is auto-saved (1-60 minutes)");
            behaviorTable.Controls.Add(new Label { Text = "Log Level:", Anchor = AnchorStyles.Left }, 0, 1);
            _cmbLogLevel = ControlFactory.CreateSfComboBox(combo =>
            {
                combo.Name = "cmbLogLevel";
                combo.Size = new Size(150, 24);
            });
            behaviorTable.Controls.Add(_cmbLogLevel, 1, 1);
            var logLevels = new[] { "Verbose", "Debug", "Information", "Warning", "Error", "Fatal" }.ToList();
            _cmbLogLevel.DataSource = logLevels;
            _cmbLogLevel.SelectedItem = ViewModel != null ? ViewModel.LogLevel : "Information";
            _tooltip?.SetToolTip(_cmbLogLevel, "Verbosity level for application logging");
            behaviorGroup.Controls.Add(behaviorTable);
            mainFlowPanel.Controls.Add(behaviorGroup);

            // Group 5: AI Settings
            var aiGroup = new GroupBox { Text = "AI / xAI Settings", AutoSize = true };
            var aiTable = CreateTableLayoutPanel(8, new ColumnStyle(SizeType.Absolute, LABEL_WIDTH), new ColumnStyle(SizeType.Percent, 100F));
            aiTable.Controls.Add(new Label { Text = "Enable AI:", Anchor = AnchorStyles.Left }, 0, 0);
            _chkEnableAi = ControlFactory.CreateCheckBoxAdv("", checkBox =>
            {
                checkBox.AutoSize = true;
                checkBox.Checked = ViewModel?.EnableAi ?? false;
                checkBox.Font = new Font("Segoe UI", 9);
            });
            aiTable.Controls.Add(_chkEnableAi, 1, 0);
            _aiToolTip?.SetToolTip(_chkEnableAi, "Enable or disable AI features.");
            aiTable.Controls.Add(new Label { Text = "Endpoint:", Anchor = AnchorStyles.Left }, 0, 1);
            _txtXaiApiEndpoint = ControlFactory.CreateTextBoxExt(textBox =>
            {
                textBox.Name = "txtXaiApiEndpoint";
                textBox.Width = 250;
                textBox.MaxLength = 500;
                textBox.Text = ViewModel?.XaiApiEndpoint ?? string.Empty;
            });
            aiTable.Controls.Add(_txtXaiApiEndpoint, 1, 1);
            _aiToolTip?.SetToolTip(_txtXaiApiEndpoint, "API endpoint for xAI Grok.");
            aiTable.Controls.Add(new Label { Text = "Model:", Anchor = AnchorStyles.Left }, 0, 2);
            _cmbXaiModel = ControlFactory.CreateSfComboBox(combo =>
            {
                combo.Name = "cmbXaiModel";
                combo.Size = new Size(220, 24);
            });
            aiTable.Controls.Add(_cmbXaiModel, 1, 2);
            try
            {
                _cmbXaiModel.DataSource = new[] { "grok-4.1", "grok-4-1-fast", "grok-4-1-fast-reasoning", "grok-4-1-fast-non-reasoning", "grok-3-2024" }.ToList();
                _cmbXaiModel.SelectedItem = ViewModel?.XaiModel ?? "grok-4.1";
            }
            catch { }
            _aiToolTip?.SetToolTip(_cmbXaiModel, "Select model used for recommendations.");
            aiTable.Controls.Add(new Label { Text = "API Key:", Anchor = AnchorStyles.Left }, 0, 3);
            var apiKeyPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
            _txtXaiApiKey = ControlFactory.CreateTextBoxExt(textBox =>
            {
                textBox.Name = "txtXaiApiKey";
                textBox.Width = 180;
                textBox.MaxLength = 500;
                textBox.UseSystemPasswordChar = true;
                textBox.Text = ViewModel?.XaiApiKey ?? string.Empty;
            });
            apiKeyPanel.Controls.Add(_txtXaiApiKey);
            _btnShowApiKey = ControlFactory.CreateSfButton("Show", button =>
            {
                button.Name = "btnShowApiKey";
                button.Size = new Size(50, 24);
            });
            apiKeyPanel.Controls.Add(_btnShowApiKey);
            aiTable.Controls.Add(apiKeyPanel, 1, 3);
            _aiToolTip?.SetToolTip(_txtXaiApiKey, "API key stored securely (not logged).");
            aiTable.Controls.Add(new Label { Text = "Timeout (s):", Anchor = AnchorStyles.Left }, 0, 4);
            _numXaiTimeout = ControlFactory.CreateSfNumericTextBox(textBox =>
            {
                textBox.Name = "numXaiTimeout";
                textBox.Size = new Size(80, 24);
                textBox.MinValue = 1;
                textBox.MaxValue = 300;
                textBox.Value = ViewModel?.XaiTimeout ?? 30;
            });
            aiTable.Controls.Add(_numXaiTimeout, 1, 4);
            _aiToolTip?.SetToolTip(_numXaiTimeout, "Maximum time (seconds) to wait for response.");
            aiTable.Controls.Add(new Label { Text = "Max tokens:", Anchor = AnchorStyles.Left }, 0, 5);
            _numXaiMaxTokens = ControlFactory.CreateSfNumericTextBox(textBox =>
            {
                textBox.Name = "numXaiMaxTokens";
                textBox.Size = new Size(100, 24);
                textBox.MinValue = 1;
                textBox.MaxValue = 65536;
                textBox.Value = ViewModel?.XaiMaxTokens ?? 2000;
            });
            aiTable.Controls.Add(_numXaiMaxTokens, 1, 5);
            aiTable.Controls.Add(new Label { Text = "Temperature:", Anchor = AnchorStyles.Left }, 0, 6);
            _numXaiTemperature = ControlFactory.CreateSfNumericTextBox(textBox =>
            {
                textBox.Name = "numXaiTemperature";
                textBox.Size = new Size(80, 24);
                textBox.MinValue = 0.0;
                textBox.MaxValue = 1.0;
                textBox.Value = ViewModel?.XaiTemperature ?? 0.7;
            });
            aiTable.Controls.Add(_numXaiTemperature, 1, 6);
            _aiToolTip?.SetToolTip(_numXaiTemperature, "Response randomness (0=deterministic, 1=varied).");
            aiTable.Controls.Add(new Label { Text = SettingsPanelResources.AiSettingsHelpShort, Anchor = AnchorStyles.Left }, 0, 7);
            aiTable.Controls.Add(_lnkAiLearnMore = new LinkLabel
            {
                Text = SettingsPanelResources.AiSettingsLearnMoreLabel,
                AutoSize = true,
                Font = new Font("Segoe UI", 8F)
            }, 1, 7);
            aiGroup.Controls.Add(aiTable);
            mainFlowPanel.Controls.Add(aiGroup);

            // Group 6: Display Formats
            var formatGroup = new GroupBox { Text = "Display Formats", AutoSize = true };
            var formatTable = CreateTableLayoutPanel(1, new ColumnStyle(SizeType.Absolute, LABEL_WIDTH), new ColumnStyle(SizeType.Absolute, 130), new ColumnStyle(SizeType.Absolute, LABEL_WIDTH), new ColumnStyle(SizeType.Absolute, 90));
            formatTable.Controls.Add(new Label { Text = "Date:", Anchor = AnchorStyles.Left }, 0, 0);
            _txtDateFormat = ControlFactory.CreateTextBoxExt(textBox =>
            {
                textBox.Name = "txtDateFormat";
                textBox.Width = 120;
                textBox.MaxLength = 50;
                textBox.Text = ViewModel?.DateFormat ?? "yyyy-MM-dd";
            });
            formatTable.Controls.Add(_txtDateFormat, 1, 0);
            formatTable.Controls.Add(new Label { Text = "Currency:", Anchor = AnchorStyles.Left }, 2, 0);
            _txtCurrencyFormat = ControlFactory.CreateTextBoxExt(textBox =>
            {
                textBox.Name = "txtCurrencyFormat";
                textBox.Width = 80;
                textBox.MaxLength = 10;
                textBox.Text = ViewModel?.CurrencyFormat ?? "C2";
            });
            formatTable.Controls.Add(_txtCurrencyFormat, 3, 0);
            formatGroup.Controls.Add(formatTable);
            mainFlowPanel.Controls.Add(formatGroup);

            // Group 7: About
            var aboutGroup = new GroupBox { Text = SettingsPanelResources.AboutGroup, AutoSize = true };
            var aboutFlow = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true };
            aboutFlow.Controls.Add(_lblVersion = new Label
            {
                Text = $"Wiley Widget v1.0.0\n.NET {Environment.Version}\nRuntime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}",
                AutoSize = true,
                Font = new Font("Segoe UI", 9)
            });
            aboutFlow.Controls.Add(_lblDbStatus = new Label
            {
                Text = "Database: Connected",
                AutoSize = true,
                Font = new Font("Segoe UI", 9)
            });
            aboutGroup.Controls.Add(aboutFlow);
            mainFlowPanel.Controls.Add(aboutGroup);

            // Bottom Buttons
            var buttonFlow = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
            _btnClose = ControlFactory.CreateSfButton("Close", button =>
            {
                button.Name = "btnClose";
                button.Width = 100;
                button.Height = 36;
            });
            buttonFlow.Controls.Add(_btnClose);
            _tooltip?.SetToolTip(_btnClose, "Close this settings panel");
            _btnSave = ControlFactory.CreateSfButton("Save Settings", button =>
            {
                button.Name = "btnSave";
                button.Width = 140;
                button.Height = 36;
                button.Enabled = false;
            });
            buttonFlow.Controls.Add(_btnSave);
            _tooltip?.SetToolTip(_btnSave, "Save all changes (validation runs first)");
            mainFlowPanel.Controls.Add(buttonFlow);

            // Apply theme
            SfSkinManager.SetVisualStyle(this, _themeName);

            ResumeLayout(false);
        }

        // Helper: Create a TableLayoutPanel with auto-sizing columns/rows
        private TableLayoutPanel CreateTableLayoutPanel(int rowCount, params ColumnStyle[] columnStyles)
        {
            var table = new TableLayoutPanel
            {
                RowCount = rowCount,
                ColumnCount = columnStyles.Length,
                AutoSize = true,
                Padding = new Padding(CONTROL_SPACING),
                Dock = DockStyle.Fill
            };
            foreach (var style in columnStyles)
            {
                table.ColumnStyles.Add(style);
            }
            for (int i = 0; i < rowCount; i++)
            {
                table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }
            return table;
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
            _cmbXaiModel?.DataBindings.Clear();
            _numXaiTimeout?.DataBindings.Clear();
            _numXaiMaxTokens?.DataBindings.Clear();
            _numXaiTemperature?.DataBindings.Clear();
        }

        private void SetupBindings()
        {
            _bindingSource = new BindingSource { DataSource = ViewModel };

            // Clear any existing bindings to prevent duplicates when handle is recreated
            ClearExistingBindings();

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
                    try { _errorBinding.MapControl(nameof(ViewModel.XaiModel), _cmbXaiModel!); } catch { }
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "SettingsPanel: Failed to setup WileyWidget.WinForms.Controls.Supporting.ErrorProviderBinding");
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
                    if (_themeCombo?.SelectedItem is string sel && ViewModel != null)
                    {
                        if (!string.Equals(ViewModel.SelectedTheme, sel, StringComparison.Ordinal))
                        {
                            ViewModel.SelectedTheme = sel; // triggers SettingsViewModel.OnSelectedThemeChanged -> ThemeService.ApplyTheme
                        }
                    }

                    SetHasUnsavedChanges(true);
                }
                catch (Exception ex)
                {
                    Serilog.Log.Debug(ex, "SettingsPanel: theme selection handler error");
                }
            };
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
                        dm.TrySetDockVisibilitySafe(this, false, context: "SettingsPanel.BtnClose_Click");
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
            // Always marshal to UI thread (non-blocking) to protect against cross-thread updates
            this.InvokeIfRequired(() =>
            {
                try
                {
                    if (_statusLabel != null && !_statusLabel.IsDisposed)
                    {
                        _statusLabel.Text = message ?? string.Empty;
                        _statusLabel.ForeColor = isError ? Color.Red : SystemColors.ControlText;
                        try { _statusLabel.Invalidate(); } catch { }
                    }
                }
                catch { }
            });
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
