using System;
using System.Drawing;
using System.Windows.Forms;
using WileyWidget.WinForms.Theming;

namespace WileyWidget.WinForms.Controls
{
    /// <summary>
    /// Shared header used by docked panels.
    /// Provides a consistent title bar with actions: Refresh, Pin and Close.
    /// Fixed height (44px) and padding (8px) to match UI guidelines.
    /// </summary>
    public partial class PanelHeader : UserControl
    {
        private Label _titleLabel;
        private Button _btnRefresh;
        private CheckBox _btnPin;
        private Button _btnClose;

        /// <summary>Raised when the user clicks Refresh.</summary>
        public event EventHandler? RefreshClicked;

        /// <summary>Raised when the user toggles Pin.</summary>
        public event EventHandler? PinToggled;

        /// <summary>Raised when the user clicks Close.</summary>
        public event EventHandler? CloseClicked;

        /// <summary>Title shown in the header.</summary>
        public string Title
        {
            get => _titleLabel.Text;
            set => _titleLabel.Text = value ?? string.Empty;
        }

        /// <summary>Whether the panel is pinned (persisted by parent if desired).</summary>
        public bool IsPinned
        {
            get => _btnPin.Checked;
            set => _btnPin.Checked = value;
        }

        public PanelHeader()
        {
            InitializeComponent();

            // Apply theme on construction so the header looks correct early
            try { ThemeManager.ApplyTheme(this); } catch { }
        }

        private void InitializeComponent()
        {
            Height = 44;
            Padding = new Padding(8);
            Dock = DockStyle.Top;

            // Title label
            _titleLabel = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 10F, FontStyle.SemiBold),
                Margin = new Padding(0),
                AccessibleName = "Panel title"
            };

            // Right-aligned container for actions
            var actionsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                WrapContents = false,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };

            // Refresh
            _btnRefresh = new Button
            {
                Text = "Refresh",
                AutoSize = true,
                Margin = new Padding(4, 6, 4, 6),
                AccessibleName = "Refresh panel"
            };
            _btnRefresh.Click += (s, e) => RefreshClicked?.Invoke(this, EventArgs.Empty);

            // Pin
            _btnPin = new CheckBox
            {
                Text = "Pin",
                AutoSize = true,
                Appearance = Appearance.Button,
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(4, 6, 4, 6),
                AccessibleName = "Pin panel"
            };
            _btnPin.CheckedChanged += (s, e) => PinToggled?.Invoke(this, EventArgs.Empty);

            // Close
            _btnClose = new Button
            {
                Text = "Close",
                AutoSize = true,
                Margin = new Padding(4, 6, 4, 6),
                AccessibleName = "Close panel"
            };
            _btnClose.Click += (s, e) => CloseClicked?.Invoke(this, EventArgs.Empty);

            actionsPanel.Controls.Add(_btnRefresh);
            actionsPanel.Controls.Add(_btnPin);
            actionsPanel.Controls.Add(_btnClose);

            Controls.Add(actionsPanel);
            Controls.Add(_titleLabel);

            // Accessibility
            AccessibleName = "Panel header";
            AccessibleDescription = "Contains the title and actions for the panel";
        }

        /// <summary>
        /// Programmatically trigger the Refresh action.
        /// </summary>
        public void TriggerRefresh() => RefreshClicked?.Invoke(this, EventArgs.Empty);
    }
}
