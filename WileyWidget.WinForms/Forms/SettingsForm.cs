using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using WileyWidget.WinForms.Theming;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Forms
{
    internal static class SettingsFormResources
    {
        public const string FormTitle = "Settings";
    }

    [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
    public partial class SettingsForm : Form
    {
        /// <summary>
        /// Simple DataContext wrapper so host code can inspect the VM when docked
        /// </summary>
        public new object? DataContext { get; private set; }
        private readonly SettingsViewModel _vm;

        // Controls
        private Panel? _mainPanel;
        private GroupBox? _themeGroup;
        private RadioButton? _rbDark;
        private RadioButton? _rbLight;
        private GroupBox? _aboutGroup;
        private Label? _lblVersion;
        private Label? _lblDbStatus;
        private Button? _btnApply;
        private Button? _btnClose;

        public SettingsForm(SettingsViewModel vm)
        {
            _vm = vm;
            DataContext = vm;
            InitializeComponent();
            SetupUI();
            ApplyCurrentTheme();

            // Subscribe to theme changes for live preview
            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        /// <summary>
        /// Make this form suitable to embed inside a docking host (non top-level/container friendly)
        /// </summary>
        public void PrepareForDocking()
        {
            try
            {
                TopLevel = false;
                FormBorderStyle = FormBorderStyle.None;
                Dock = DockStyle.Fill;
                StartPosition = FormStartPosition.Manual;
            }
            catch { }
        }

        private void InitializeComponent()
        {
            Text = SettingsFormResources.FormTitle;
            Size = new Size(500, 400);
            try { AutoScaleMode = AutoScaleMode.Dpi; } catch { }
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
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

            // Theme group
            _themeGroup = new GroupBox
            {
                Text = "Appearance",
                Location = new Point(padding, y),
                Size = new Size(440, 100),
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };

            _rbDark = new RadioButton
            {
                Text = "Dark Theme (Fluent Dark)",
                Location = new Point(20, 30),
                AutoSize = true,
                Font = new Font("Segoe UI", 9, FontStyle.Regular),
                Checked = ThemeManager.CurrentTheme == AppTheme.Dark
            };
            _rbDark.CheckedChanged += OnThemeRadioChanged;

            _rbLight = new RadioButton
            {
                Text = "Light Theme (Fluent Light)",
                Location = new Point(20, 58),
                AutoSize = true,
                Font = new Font("Segoe UI", 9, FontStyle.Regular),
                Checked = ThemeManager.CurrentTheme == AppTheme.Light
            };
            _rbLight.CheckedChanged += OnThemeRadioChanged;

            _themeGroup.Controls.Add(_rbDark);
            _themeGroup.Controls.Add(_rbLight);
            y += 120;

            // About group
            _aboutGroup = new GroupBox
            {
                Text = "About",
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
            _btnApply = new Button
            {
                Name = "btnApply",
                Text = "Apply",
                Size = new Size(100, 35),
                Location = new Point(240, y),
                Font = new Font("Segoe UI", 9, FontStyle.Regular)
            };
            try
            {
                var iconService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Services.IThemeIconService>(Program.Services);
                var theme = WileyWidget.WinForms.Theming.ThemeManager.CurrentTheme;
                _btnApply.Image = iconService?.GetIcon("save", theme, 14);
                _btnApply.ImageAlign = ContentAlignment.MiddleLeft;
                _btnApply.TextImageRelation = TextImageRelation.ImageBeforeText;
                WileyWidget.WinForms.Theming.ThemeManager.ThemeChanged += (s, t) =>
                {
                    try { _btnApply.Image = iconService?.GetIcon("save", t, 14); } catch { }
                };
            }
            catch { }
            _btnApply.Click += BtnApply_Click;

            _btnClose = new Button
            {
                Name = "btnClose",
                Text = "Close",
                Size = new Size(100, 35),
                Location = new Point(350, y),
                DialogResult = DialogResult.Cancel,
                Font = new Font("Segoe UI", 9, FontStyle.Regular)
            };
            try
            {
                var iconService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Services.IThemeIconService>(Program.Services);
                var theme = WileyWidget.WinForms.Theming.ThemeManager.CurrentTheme;
                _btnClose.Image = iconService?.GetIcon("dismiss", theme, 14);
                _btnClose.ImageAlign = ContentAlignment.MiddleLeft;
                _btnClose.TextImageRelation = TextImageRelation.ImageBeforeText;
                WileyWidget.WinForms.Theming.ThemeManager.ThemeChanged += (s, t) =>
                {
                    try { _btnClose.Image = iconService?.GetIcon("dismiss", t, 14); } catch { }
                };
            }
            catch { }

            _mainPanel.Controls.Add(_themeGroup);
            _mainPanel.Controls.Add(_aboutGroup);
            _mainPanel.Controls.Add(_btnApply);
            _mainPanel.Controls.Add(_btnClose);

            Controls.Add(_mainPanel);
            AcceptButton = _btnApply;
            CancelButton = _btnClose;
        }

        private void ApplyCurrentTheme()
        {
            ThemeManager.ApplyTheme(this);

            // Style groups specially
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
        }

        private void OnThemeRadioChanged(object? sender, EventArgs e)
        {
            // Live preview - apply theme immediately when radio changes
            if (_rbDark?.Checked == true && ThemeManager.CurrentTheme != AppTheme.Dark)
            {
                ThemeManager.SetTheme(AppTheme.Dark);
            }
            else if (_rbLight?.Checked == true && ThemeManager.CurrentTheme != AppTheme.Light)
            {
                ThemeManager.SetTheme(AppTheme.Light);
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

        private void BtnApply_Click(object? sender, EventArgs e)
        {
            // Theme is already applied via live switching
            // Just confirm and close
            MessageBox.Show(
                "Theme settings saved successfully.\n\nSome Syncfusion controls may require an application restart to fully update.",
                "Settings Saved",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.ThemeChanged -= OnThemeChanged;
                _mainPanel?.Dispose();
                _themeGroup?.Dispose();
                _rbDark?.Dispose();
                _rbLight?.Dispose();
                _aboutGroup?.Dispose();
                _lblVersion?.Dispose();
                _lblDbStatus?.Dispose();
                _btnApply?.Dispose();
                _btnClose?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
