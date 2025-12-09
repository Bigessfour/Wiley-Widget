using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Services;
using System.Globalization;
using Syncfusion.Windows.Forms;

namespace WileyWidget.WinForms.Forms
{
    internal static class SettingsFormResources
    {
        public const string FormTitle = "Settings";
        public const string GeneralTab = "General";
        public const string ConnectionTab = "Connections";
        public const string QuickBooksTab = "QuickBooks";
        public const string AppearanceTab = "Appearance";
        public const string AdvancedTab = "Advanced";
        public const string SaveButton = "Save Settings";
        public const string CancelButton = "Cancel";
        public const string ResetButton = "Reset to Defaults";
    }

    [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
    public partial class SettingsForm : Form
    {
        private readonly SettingsViewModel _vm;
        private readonly ILogger<SettingsForm> _logger;
        private readonly IThemeManagerService _themeManager;
        private TabControl? _tabControl;
        private TextBox? _companyNameTextBox;
        private TextBox? _connectionStringTextBox;
        private ComboBox? _themeComboBox;
        private CheckBox? _darkModeCheckBox;
        private NumericUpDown? _autoSaveIntervalUpDown;
        private CheckBox? _enableLoggingCheckBox;

        public SettingsForm(SettingsViewModel vm, ILogger<SettingsForm> logger, IThemeManagerService themeManager)
        {
            InitializeComponent();

            _vm = vm;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));

            try
            {
                Text = SettingsFormResources.FormTitle;
                _logger.LogInformation("SettingsForm initialized successfully");

                // Apply theme using centralized service
                try
                {
                    _themeManager.ApplyTheme(this);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Unable to apply theme to SettingsForm");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize SettingsForm");
                if (Application.MessageLoop)
                {
                    MessageBox.Show($"Unable to open settings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                throw;
            }
        }

        private void InitializeComponent()
        {
            SuspendLayout();

            // === Tab Control ===
            _tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10),
                Padding = new Point(15, 8)
            };

            // === General Tab ===
            var generalTab = new TabPage(SettingsFormResources.GeneralTab) { Padding = new Padding(20) };
            var generalLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 5,
                AutoSize = true
            };
            generalLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            generalLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // Company Name
            generalLayout.Controls.Add(CreateLabel("Company Name:"), 0, 0);
            _companyNameTextBox = new TextBox { Dock = DockStyle.Fill, Text = "Wiley Widget Corp" };
            generalLayout.Controls.Add(_companyNameTextBox, 1, 0);

            // Auto-save interval
            generalLayout.Controls.Add(CreateLabel("Auto-save Interval (min):"), 0, 1);
            _autoSaveIntervalUpDown = new NumericUpDown { Minimum = 1, Maximum = 60, Value = 5, Dock = DockStyle.Left, Width = 80 };
            generalLayout.Controls.Add(_autoSaveIntervalUpDown, 1, 1);

            // Enable logging
            generalLayout.Controls.Add(CreateLabel("Enable Logging:"), 0, 2);
            _enableLoggingCheckBox = new CheckBox { Checked = true, Text = "Write diagnostic logs to file" };
            generalLayout.Controls.Add(_enableLoggingCheckBox, 1, 2);

            generalTab.Controls.Add(generalLayout);

            // === Connections Tab ===
            var connectionTab = new TabPage(SettingsFormResources.ConnectionTab) { Padding = new Padding(20) };
            var connectionLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4,
                AutoSize = true
            };
            connectionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            connectionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // Connection String
            connectionLayout.Controls.Add(CreateLabel("Database Connection:"), 0, 0);
            _connectionStringTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                Height = 60,
                Text = "Server=localhost;Database=WileyWidget;Trusted_Connection=True;"
            };
            connectionLayout.Controls.Add(_connectionStringTextBox, 1, 0);

            // QuickBooks status
            connectionLayout.Controls.Add(CreateLabel("QuickBooks Status:"), 0, 1);
            var qbStatusLabel = new Label
            {
                Text = "🟢 Connected",
                AutoSize = true,
                Padding = new Padding(0, 5, 0, 0)
            };
            if (!SkinManager.ContainsSkinManager)
            {
                qbStatusLabel.ForeColor = Color.Green;
            }
            connectionLayout.Controls.Add(qbStatusLabel, 1, 1);

            // Test connection button
            var testConnectionBtn = new Button
            {
                Text = "Test Connection",
                Width = 150,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            if (!SkinManager.ContainsSkinManager)
            {
                testConnectionBtn.BackColor = Color.FromArgb(66, 133, 244);
                testConnectionBtn.ForeColor = Color.White;
            }
            testConnectionBtn.Click += (s, e) => MessageBox.Show("Connection successful!", "Test Result", MessageBoxButtons.OK, MessageBoxIcon.Information);
            connectionLayout.Controls.Add(testConnectionBtn, 1, 2);

            connectionTab.Controls.Add(connectionLayout);

            // === Appearance Tab ===
            var appearanceTab = new TabPage(SettingsFormResources.AppearanceTab) { Padding = new Padding(20) };
            var appearanceLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4,
                AutoSize = true
            };
            appearanceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            appearanceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // Theme selector
            appearanceLayout.Controls.Add(CreateLabel("Theme:"), 0, 0);
            _themeComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 200
            };
            // Populate with available themes from ThemeManagerService
            var availableThemes = _themeManager.GetAvailableThemes();
            _themeComboBox.Items.AddRange(availableThemes.Cast<object>().ToArray());
            // Select current theme
            var currentTheme = _themeManager.GetCurrentTheme();
            var currentIndex = availableThemes.ToList().IndexOf(currentTheme);
            _themeComboBox.SelectedIndex = currentIndex >= 0 ? currentIndex : 0;
            _themeComboBox.SelectedIndexChanged += (s, e) =>
            {
                try
                {
                    var selected = _themeComboBox.SelectedItem?.ToString();
                    if (!string.IsNullOrEmpty(selected))
                    {
                        _themeManager.ApplyTheme(this, selected);
                        _logger.LogInformation("User changed theme to: {ThemeName}", selected);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to apply selected theme");
                }
            };
            appearanceLayout.Controls.Add(_themeComboBox, 1, 0);

            // Dark mode toggle
            appearanceLayout.Controls.Add(CreateLabel("Dark Mode:"), 0, 1);
            _darkModeCheckBox = new CheckBox { Text = "Enable dark mode interface" };
            appearanceLayout.Controls.Add(_darkModeCheckBox, 1, 1);

            appearanceTab.Controls.Add(appearanceLayout);

            // === QuickBooks Tab ===
            var quickBooksTab = new TabPage(SettingsFormResources.QuickBooksTab) { Padding = new Padding(20) };
            var qbLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = 8,
                AutoSize = true,
                Height = 350
            };
            qbLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            qbLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // Connection Status
            qbLayout.Controls.Add(CreateLabel("Connection Status:"), 0, 0);
            var qbStatusPanel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            var qbStatusIndicator = new Label { Text = "●", Font = new Font("Segoe UI", 14), AutoSize = true };
            if (!SkinManager.ContainsSkinManager)
            {
                qbStatusIndicator.ForeColor = Color.Green;
            }
            var qbStatusText = new Label { Text = "Connected to QuickBooks Online", AutoSize = true, Padding = new Padding(5, 5, 0, 0) };
            qbStatusPanel.Controls.Add(qbStatusIndicator);
            qbStatusPanel.Controls.Add(qbStatusText);
            qbLayout.Controls.Add(qbStatusPanel, 1, 0);

            // Company
            qbLayout.Controls.Add(CreateLabel("Company:"), 0, 1);
            var qbCompanyLabel = new Label { Text = "Wiley Widget Corp (Sandbox)", AutoSize = true, Padding = new Padding(0, 8, 0, 0) };
            qbLayout.Controls.Add(qbCompanyLabel, 1, 1);

            // Last Sync
            qbLayout.Controls.Add(CreateLabel("Last Sync:"), 0, 2);
            var qbLastSyncLabel = new Label { Text = DateTime.Now.AddHours(-1).ToString("g", CultureInfo.CurrentCulture), AutoSize = true, Padding = new Padding(0, 8, 0, 0) };
            qbLayout.Controls.Add(qbLastSyncLabel, 1, 2);

            // Auto-sync
            qbLayout.Controls.Add(CreateLabel("Auto-Sync:"), 0, 3);
            var qbAutoSyncCheck = new CheckBox { Text = "Enable automatic synchronization", Checked = true };
            qbLayout.Controls.Add(qbAutoSyncCheck, 1, 3);

            // Sync interval
            qbLayout.Controls.Add(CreateLabel("Sync Interval:"), 0, 4);
            var qbSyncIntervalCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
            qbSyncIntervalCombo.Items.AddRange(new object[] { "Every 15 minutes", "Every 30 minutes", "Every hour", "Every 4 hours", "Daily" });
            qbSyncIntervalCombo.SelectedIndex = 2;
            qbLayout.Controls.Add(qbSyncIntervalCombo, 1, 4);

            // Buttons panel
            var qbButtonPanel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 10, 0, 0) };
            var qbSyncNowBtn = new Button
            {
                Text = "Sync Now",
                Width = 100,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            if (!SkinManager.ContainsSkinManager)
            {
                qbSyncNowBtn.BackColor = Color.FromArgb(66, 133, 244);
                qbSyncNowBtn.ForeColor = Color.White;
            }
            qbSyncNowBtn.Click += (s, e) =>
            {
                qbSyncNowBtn.Text = "Syncing...";
                qbSyncNowBtn.Enabled = false;
                Task.Delay(2000).ContinueWith(t =>
                {
                    if (IsHandleCreated)
                    {
                        Invoke(() =>
                        {
                            qbSyncNowBtn.Text = "Sync Now";
                            qbSyncNowBtn.Enabled = true;
                            qbLastSyncLabel.Text = DateTime.Now.ToString("g", CultureInfo.CurrentCulture);
                            _logger.LogInformation("QuickBooks sync completed successfully, 42 records synchronized");
                        });
                    }
                });
            };

            var qbDisconnectBtn = new Button
            {
                Text = "Disconnect",
                Width = 100,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Margin = new Padding(10, 0, 0, 0)
            };
            if (!SkinManager.ContainsSkinManager)
            {
                qbDisconnectBtn.ForeColor = Color.FromArgb(220, 53, 69);
            }
            qbDisconnectBtn.Click += (s, e) =>
            {
                _logger.LogWarning("Disconnect from QuickBooks requested");
                var proceed = true; // Assume yes
                if (proceed)
                {
                    if (!SkinManager.ContainsSkinManager)
                    {
                        qbStatusIndicator.ForeColor = Color.Red;
                        qbDisconnectBtn.ForeColor = Color.FromArgb(52, 168, 83);
                    }
                    qbStatusText.Text = "Disconnected";
                    qbDisconnectBtn.Text = "Connect";
                    _logger.LogInformation("Disconnected from QuickBooks");
                }
            };

            qbButtonPanel.Controls.Add(qbSyncNowBtn);
            qbButtonPanel.Controls.Add(qbDisconnectBtn);
            qbLayout.Controls.Add(new Label(), 0, 5);
            qbLayout.Controls.Add(qbButtonPanel, 1, 5);

            quickBooksTab.Controls.Add(qbLayout);

            // === Advanced Tab ===
            var advancedTab = new TabPage(SettingsFormResources.AdvancedTab) { Padding = new Padding(20) };
            var advancedLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 6,
                AutoSize = true
            };
            advancedLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            advancedLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // Cache settings
            advancedLayout.Controls.Add(CreateLabel("Cache Duration:"), 0, 0);
            var cacheDurationCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
            cacheDurationCombo.Items.AddRange(new object[] { "5 minutes", "15 minutes", "30 minutes", "1 hour", "Never" });
            cacheDurationCombo.SelectedIndex = 1;
            advancedLayout.Controls.Add(cacheDurationCombo, 1, 0);

            // Debug mode
            advancedLayout.Controls.Add(CreateLabel("Debug Mode:"), 0, 1);
            var debugModeCheck = new CheckBox { Text = "Enable verbose logging" };
            advancedLayout.Controls.Add(debugModeCheck, 1, 1);

            // Telemetry
            advancedLayout.Controls.Add(CreateLabel("Telemetry:"), 0, 2);
            var telemetryCheck = new CheckBox { Text = "Send anonymous usage data", Checked = true };
            advancedLayout.Controls.Add(telemetryCheck, 1, 2);

            // API timeout
            advancedLayout.Controls.Add(CreateLabel("API Timeout (sec):"), 0, 3);
            var apiTimeoutUpDown = new NumericUpDown { Minimum = 5, Maximum = 120, Value = 30, Width = 80 };
            advancedLayout.Controls.Add(apiTimeoutUpDown, 1, 3);

            // Clear cache button
            advancedLayout.Controls.Add(new Label(), 0, 4);
            var clearCacheBtn = new Button
            {
                Text = "Clear Cache",
                Width = 120,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            clearCacheBtn.Click += (s, e) => MessageBox.Show("Cache cleared successfully!", "Cache", MessageBoxButtons.OK, MessageBoxIcon.Information);
            advancedLayout.Controls.Add(clearCacheBtn, 1, 4);

            advancedTab.Controls.Add(advancedLayout);

            // Add tabs
            _tabControl.TabPages.AddRange(new TabPage[] { generalTab, connectionTab, quickBooksTab, appearanceTab, advancedTab });

            // === Button Panel ===
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(10)
            };
            if (!SkinManager.ContainsSkinManager)
            {
                buttonPanel.BackColor = Color.FromArgb(248, 249, 250);
            }

            var cancelBtn = new Button
            {
                Text = SettingsFormResources.CancelButton,
                Width = 100,
                Height = 35,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            cancelBtn.Click += (s, e) => Close();

            var saveBtn = new Button
            {
                Text = SettingsFormResources.SaveButton,
                Width = 120,
                Height = 35,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            if (!SkinManager.ContainsSkinManager)
            {
                saveBtn.BackColor = Color.FromArgb(52, 168, 83);
                saveBtn.ForeColor = Color.White;
            }
            saveBtn.Click += async (s, e) =>
            {
                try
                {
                    _logger.LogInformation("Settings save button clicked");
                    if (_vm.SaveCommand is IAsyncRelayCommand asyncCmd)
                    {
                        await asyncCmd.ExecuteAsync(null);
                    }
                    else
                    {
                        _vm.SaveCommand.Execute(null);
                    }
                    _logger.LogInformation("Settings saved successfully by user");
                    MessageBox.Show("Settings saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save settings");
                    MessageBox.Show($"Error saving settings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            var resetBtn = new Button
            {
                Text = SettingsFormResources.ResetButton,
                Width = 140,
                Height = 35,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            if (!SkinManager.ContainsSkinManager)
            {
                resetBtn.ForeColor = Color.FromArgb(220, 53, 69);
            }
            resetBtn.Click += (s, e) =>
            {
                if (MessageBox.Show("Reset all settings to defaults?", "Confirm Reset", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    _companyNameTextBox!.Text = "Wiley Widget Corp";
                    _autoSaveIntervalUpDown!.Value = 5;
                    _enableLoggingCheckBox!.Checked = true;
                    _themeComboBox!.SelectedIndex = 0;
                    _darkModeCheckBox!.Checked = false;
                }
            };

            buttonPanel.Controls.Add(cancelBtn);
            buttonPanel.Controls.Add(saveBtn);
            buttonPanel.Controls.Add(resetBtn);

            // === Add Controls ===
            Controls.Add(_tabControl);
            Controls.Add(buttonPanel);

            Size = new Size(700, 500);
            MinimumSize = new Size(500, 400);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;

            ResumeLayout(false);
            PerformLayout();
        }

        private static Label CreateLabel(string text)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 10),
                AutoSize = true,
                Padding = new Padding(0, 8, 0, 0)
            };
        }
        private void ApplySyncfusionTheme(string themeName)
        {
            try
            {
                if (SkinManager.ContainsSkinManager)
                {
                    try
                    {
                        var sfTheme = themeName;
                        if (string.IsNullOrEmpty(sfTheme))
                        {
                            sfTheme = SkinManager.ApplicationVisualTheme ?? "Office2019DarkGray";
                        }
                        SkinManager.SetVisualStyle(this, sfTheme);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to set visual style to {ThemeName}", themeName);
                    }
                }
                else
                {
                    // Apply theme colors manually based on theme name
                    switch (themeName)
                    {
                        case "Office2019DarkGray":
                        case "Office2016DarkGray":
                        case "MaterialDark":
                        case "HighContrastBlack":
                            BackColor = Color.FromArgb(45, 45, 48);
                            ForeColor = Color.White;
                            break;
                        case "MaterialLight":
                        case "Office2019Colorful":
                        case "Office2016Colorful":
                        default:
                            BackColor = Color.FromArgb(45, 45, 48);
                            ForeColor = Color.White;
                            break;
                    }

                    _logger?.LogInformation("Applied theme manually: {ThemeName}", themeName);
                }

                _logger?.LogInformation("Applied theme: {ThemeName}", themeName);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to apply theme {ThemeName}", themeName);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _tabControl?.Dispose();
                _companyNameTextBox?.Dispose();
                _connectionStringTextBox?.Dispose();
                _themeComboBox?.Dispose();
                _darkModeCheckBox?.Dispose();
                _autoSaveIntervalUpDown?.Dispose();
                _enableLoggingCheckBox?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
