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
        private SfButton _btnHelp = null!;
        private SfButton _btnClose = null!;
        private Label? _loadingLabel;
        private bool _isPinned; // Track toggle state for pin button
        private bool _isLoading; // Loading state
        private bool _helpButtonVisible = true;
        private bool _refreshButtonVisible = true;

        /// <summary>Raised when the user clicks Refresh.</summary>
        public event EventHandler? RefreshClicked;

        /// <summary>Raised when the user toggles Pin.</summary>
        public event EventHandler? PinToggled;

        /// <summary>Raised when the user clicks Help.</summary>
        public event EventHandler? HelpClicked;

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

        /// <summary>Get/set loading state. Disables Refresh button and shows loading indicator.</summary>
        [Browsable(true)]
        [DefaultValue(false)]
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading == value) return;
                _isLoading = value;
                try
                {
                    _btnRefresh.Enabled = !value;
                    if (_loadingLabel != null)
                    {
                        _loadingLabel.Visible = value;
                        _loadingLabel.Text = value ? "Loading..." : "";
                    }
                    _btnRefresh.Text = value ? "Refreshing..." : "Refresh";
                }
                catch { /* best effort */ }
            }
        }

        /// <summary>Show/hide the Help button (default: true).</summary>
        [Browsable(true)]
        [DefaultValue(true)]
        public bool ShowHelpButton
        {
            get => _helpButtonVisible;
            set
            {
                if (_helpButtonVisible == value) return;
                _helpButtonVisible = value;
                try { _btnHelp.Visible = value; } catch { }
            }
        }

        /// <summary>Show/hide the Refresh button (default: true).</summary>
        [Browsable(true)]
        [DefaultValue(true)]
        public bool ShowRefreshButton
        {
            get => _refreshButtonVisible;
            set
            {
                if (_refreshButtonVisible == value) return;
                _refreshButtonVisible = value;
                try { _btnRefresh.Visible = value; } catch { }
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
                try { _btnHelp?.Dispose(); } catch { }
                try { _btnClose?.Dispose(); } catch { }
                try { _loadingLabel?.Dispose(); } catch { }
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
                AccessibleName = "Refresh",
                TabStop = true
            };
            _btnRefresh.Click += (s, e) => RefreshClicked?.Invoke(this, EventArgs.Empty);
            _btnRefresh.KeyDown += (s, e) => { if (e.Alt && e.KeyCode == Keys.R) { _btnRefresh.PerformClick(); e.Handled = true; } };

            // Loading indicator
            _loadingLabel = new Label
            {
                Text = "",
                AutoSize = true,
                Margin = new Padding(4, 6, 4, 6),
                Visible = false,
                Font = new Font("Segoe UI", 9F, FontStyle.Italic)
            };

            // Pin
            _btnPin = new SfButton
            {
                Text = "Pin",
                AutoSize = true,
                Margin = new Padding(4, 6, 4, 6),
                AccessibleName = "Pin",
                TabStop = true
            };
            _btnPin.Click += PinButton_Click;
            _btnPin.KeyDown += (s, e) => { if (e.Alt && e.KeyCode == Keys.P) { _btnPin.PerformClick(); e.Handled = true; } };

            // Help
            _btnHelp = new SfButton
            {
                Text = "Help",
                AutoSize = true,
                Margin = new Padding(4, 6, 4, 6),
                AccessibleName = "Help",
                Visible = _helpButtonVisible,
                TabStop = true
            };
            _btnHelp.Click += (s, e) => HelpClicked?.Invoke(this, EventArgs.Empty);
            _btnHelp.KeyDown += (s, e) => { if (e.Alt && e.KeyCode == Keys.H) { _btnHelp.PerformClick(); e.Handled = true; } };

            // Close
            _btnClose = new SfButton
            {
                Text = "Close",
                AutoSize = true,
                Margin = new Padding(4, 6, 4, 6),
                AccessibleName = "Close",
                TabStop = true
            };
            _btnClose.Click += (s, e) => CloseClicked?.Invoke(this, EventArgs.Empty);
            _btnClose.KeyDown += (s, e) => { if (e.Alt && e.KeyCode == Keys.C) { _btnClose.PerformClick(); e.Handled = true; } };

            actionsPanel.Controls.Add(_btnRefresh);
            actionsPanel.Controls.Add(_loadingLabel);
            actionsPanel.Controls.Add(_btnPin);
            actionsPanel.Controls.Add(_btnHelp);
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

        /// <summary>
        /// Set the loading state temporarily for an async operation.
        /// Usage: SetLoadingAsync(async () => await MyLongOperationAsync());
        /// </summary>
        public async System.Threading.Tasks.Task SetLoadingAsync(Func<System.Threading.Tasks.Task> operation)
        {
            if (operation == null) return;
            try
            {
                IsLoading = true;
                await operation();
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
