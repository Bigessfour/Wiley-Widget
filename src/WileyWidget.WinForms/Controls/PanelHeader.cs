using System;
using System.Drawing;
using System.ComponentModel;
using System.Windows.Forms;
using Syncfusion.WinForms.Controls;

namespace WileyWidget.WinForms.Controls
{
    /// <summary>
    /// Shared header used by docked panels.
    /// Provides a consistent title bar with actions: Refresh, Pin and Close.
    /// Fixed height (44px) and padding (8px) to match UI guidelines.
    /// </summary>
    public partial class PanelHeader : UserControl
    {
        private Label _titleLabel = null!;
        private SfButton _btnRefresh = null!;
        private SfButton _btnPin = null!;
        private SfButton _btnClose = null!;
        private bool _isPinned; // Track toggle state for pin button

        /// <summary>Raised when the user clicks Refresh.</summary>
        public event EventHandler? RefreshClicked;

        /// <summary>Raised when the user toggles Pin.</summary>
        public event EventHandler? PinToggled;

        /// <summary>Raised when the user clicks Close.</summary>
        public event EventHandler? CloseClicked;

        /// <summary>Title shown in the header.</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Title
        {
            get => _titleLabel.Text;
            set
            {
                _titleLabel.Text = value ?? string.Empty;
                try { _titleLabel.AccessibleName = value ?? string.Empty; } catch { }
                try { _titleLabel.Name = "headerLabel"; } catch { }
            }
        }

        /// <summary>Whether the panel is pinned (persisted by parent if desired).</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsPinned
        {
            get => _isPinned;
            set
            {
                _isPinned = value;
                UpdatePinButtonAppearance();
            }
        }

        public PanelHeader()
        {
            InitializeComponent();
            UpdatePinButtonAppearance();

            // Theme is applied by SfSkinManager cascade from parent form
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { _titleLabel?.Dispose(); } catch { }
                try { _btnRefresh?.Dispose(); } catch { }
                try { _btnPin?.Dispose(); } catch { }
                try { _btnClose?.Dispose(); } catch { }
            }

            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            Height = 44;
            Padding = new Padding(8);
            Dock = DockStyle.Top;

            // Title label
            _titleLabel = new Label
            {
                Name = "headerLabel",
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Margin = new Padding(0),
                AccessibleName = "Header title"
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
            _btnRefresh = new SfButton
            {
                Text = "Refresh",
                AutoSize = true,
                Margin = new Padding(4, 6, 4, 6),
                AccessibleName = "Refresh"
            };
            _btnRefresh.Click += (s, e) => RefreshClicked?.Invoke(this, EventArgs.Empty);

            // Pin
            _btnPin = new SfButton
            {
                Text = "Pin",
                AutoSize = true,
                Margin = new Padding(4, 6, 4, 6),
                AccessibleName = "Pin"
            };
            _btnPin.Click += PinButton_Click;

            // Close
            _btnClose = new SfButton
            {
                Text = "Close",
                AutoSize = true,
                Margin = new Padding(4, 6, 4, 6),
                AccessibleName = "Close"
            };
            _btnClose.Click += (s, e) => CloseClicked?.Invoke(this, EventArgs.Empty);

            actionsPanel.Controls.Add(_btnRefresh);
            actionsPanel.Controls.Add(_btnPin);
            actionsPanel.Controls.Add(_btnClose);

            Controls.Add(actionsPanel);
            Controls.Add(_titleLabel);

            // Accessibility
            AccessibleName = "Header";
            AccessibleDescription = "Contains the title and actions for the header";
        }

        private void PinButton_Click(object? sender, EventArgs e)
        {
            IsPinned = !IsPinned;
            PinToggled?.Invoke(this, EventArgs.Empty);
        }

        private void UpdatePinButtonAppearance()
        {
            // Update button appearance based on pinned state
            // SfButton styling is managed by SfSkinManager theme cascade from parent form
            // Avoid manual BackColor/ForeColor assignments - theme system handles this
            try
            {
                _btnPin.Text = _isPinned ? "Unpin" : "Pin";
                _btnPin.AccessibleName = _isPinned ? "Unpin panel" : "Pin panel";
                // Theme colors are inherited from SfSkinManager - no manual override
            }
            catch { /* best effort */ }
        }

        /// <summary>
        /// Programmatically trigger the Refresh action.
        /// </summary>
        public void TriggerRefresh() => RefreshClicked?.Invoke(this, EventArgs.Empty);
    }
}
