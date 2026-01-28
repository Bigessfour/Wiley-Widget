using System;
using System.Drawing;
using System.ComponentModel;
using System.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.Drawing;
using Syncfusion.WinForms.Controls;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Services;

namespace WileyWidget.WinForms.Controls
{
    /// <summary>
    /// Shared header used by docked panels.
    /// Provides a consistent title bar with actions: Refresh, Pin, Help, and Close.
    /// Fixed height (44px) and padding (8px) to match UI guidelines.
    /// Features: Loading spinner, keyboard shortcuts (Alt+key, Esc to close), tooltips, theme-aware styling.
    /// </summary>
    public partial class PanelHeader : UserControl
    {
        private const int HEADER_HEIGHT = 52;
        private const int BUTTON_MARGIN_H = 6;
        private const int BUTTON_MARGIN_V = 6;

        private Label? _titleLabel;
        private SfButton? _btnRefresh;
        private SfButton? _btnPin;
        private SfButton? _btnHelp;
        private SfButton? _btnClose;
        private ProgressBarAdv? _loadingSpinner;
        private ToolTip? _toolTip;
        private DpiAwareImageService? _imageService;

        private bool _isPinned;
        private bool _isLoading;
        private bool _refreshInProgress;
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
            get => _titleLabel?.Text ?? string.Empty;
            set
            {
                if (_titleLabel != null)
                {
                    _titleLabel.Text = value ?? string.Empty;
                    _titleLabel.AccessibleName = value ?? string.Empty;
                }
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

        /// <summary>Get/set loading state. Disables all action buttons except Close and shows loading spinner.</summary>
        [Browsable(true)]
        [DefaultValue(false)]
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading == value) return;
                _isLoading = value;

                // Disable all action buttons except Close during loading
                if (_btnRefresh != null)
                {
                    _btnRefresh.Enabled = !value;
                    _btnRefresh.Text = value ? "Refreshing..." : "Refresh";
                }

                if (_btnPin != null)
                {
                    _btnPin.Enabled = !value;
                }

                if (_btnHelp != null)
                {
                    _btnHelp.Enabled = !value;
                }

                // Close button remains enabled to allow user to dismiss panel during loading
                if (_btnClose != null)
                {
                    _btnClose.Enabled = true;
                }

                if (_loadingSpinner != null)
                {
                    _loadingSpinner.Visible = value;
                }
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
                if (_btnHelp != null)
                {
                    _btnHelp.Visible = value;
                }
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
                if (_btnRefresh != null)
                {
                    _btnRefresh.Visible = value;
                }
            }
        }

        public PanelHeader() : this(null)
        {
        }

        /// <summary>
        /// Initialize PanelHeader with optional icon service for modern button icons.
        /// If no service is provided, buttons display text labels only.
        /// </summary>
        public PanelHeader(DpiAwareImageService? imageService)
        {
            _imageService = imageService;
            InitializeComponent();
            UpdatePinButtonAppearance();
            // Theme is applied by SfSkinManager cascade from parent form
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _toolTip?.Dispose();
                // Note: Individual controls are disposed by parent container automatically
            }

            base.Dispose(disposing);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Support Esc key to close panel (common UX pattern)
            if (keyData == Keys.Escape)
            {
                CloseClicked?.Invoke(this, EventArgs.Empty);
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void InitializeComponent()
        {
            Height = HEADER_HEIGHT;
            Padding = new Padding(8);
            Dock = DockStyle.Top;

            // Compute button sizing based on header height and padding
            var innerHeight = HEADER_HEIGHT - Padding.Vertical;
            var buttonHeight = Math.Max(20, innerHeight - (BUTTON_MARGIN_V * 2));
            var buttonWidth = 80; // reasonable width for text buttons

            _toolTip = new ToolTip();

            // Title label (use system font for theme consistency)
            var titleFont = new Font("Segoe UI", 11F, FontStyle.Bold);
            _titleLabel = new Label
            {
                Name = "headerLabel",
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = titleFont,
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

            // Refresh button (with optional icon)
            _btnRefresh = new SfButton
            {
                Text = "Refresh",
                AutoSize = false,
                Size = new Size(buttonWidth, buttonHeight),
                Margin = new Padding(BUTTON_MARGIN_H, BUTTON_MARGIN_V, BUTTON_MARGIN_H, BUTTON_MARGIN_V),
                AccessibleName = "Refresh",
                TabStop = true,
                TextImageRelation = TextImageRelation.ImageBeforeText
            };
            if (_imageService != null)
            {
                var refreshIcon = _imageService.GetImage("refresh");
                if (refreshIcon != null)
                {
                    _btnRefresh.Image = refreshIcon;
                    _btnRefresh.Text = string.Empty; // Icon-only for cleaner look
                }
            }
            _btnRefresh.Click += RefreshButton_Click;
            _btnRefresh.KeyDown += (s, e) =>
            {
                if (e.Alt && e.KeyCode == Keys.R)
                {
                    _btnRefresh.PerformClick();
                    e.Handled = true;
                }
            };
            _toolTip.SetToolTip(_btnRefresh, "Refresh data (Alt+R)");

            // Loading spinner (ProgressBarAdv, set to 50% for indeterminate appearance)
            _loadingSpinner = new ProgressBarAdv
            {
                Value = 50, // Indeterminate-like appearance
                AutoSize = false,
                Margin = new Padding(BUTTON_MARGIN_H, BUTTON_MARGIN_V, BUTTON_MARGIN_H, BUTTON_MARGIN_V),
                Size = new Size(24, Math.Max(12, buttonHeight - 8)),
                Visible = false
            };
            _loadingSpinner.AccessibleName = "Loading";

            // Pin button (with optional icon)
            _btnPin = new SfButton
            {
                Text = "Pin",
                AutoSize = false,
                Size = new Size(buttonWidth, buttonHeight),
                Margin = new Padding(BUTTON_MARGIN_H, BUTTON_MARGIN_V, BUTTON_MARGIN_H, BUTTON_MARGIN_V),
                AccessibleName = "Pin",
                TabStop = true,
                TextImageRelation = TextImageRelation.ImageBeforeText
            };
            _btnPin.Click += PinButton_Click;
            _btnPin.KeyDown += (s, e) =>
            {
                if (e.Alt && e.KeyCode == Keys.P)
                {
                    _btnPin.PerformClick();
                    e.Handled = true;
                }
            };
            UpdatePinButtonIcon(); // Apply icon after initialization

            // Help button (with optional icon)
            _btnHelp = new SfButton
            {
                Text = "Help",
                AutoSize = false,
                Size = new Size(buttonWidth, buttonHeight),
                Margin = new Padding(BUTTON_MARGIN_H, BUTTON_MARGIN_V, BUTTON_MARGIN_H, BUTTON_MARGIN_V),
                AccessibleName = "Help",
                Visible = _helpButtonVisible,
                TabStop = true,
                TextImageRelation = TextImageRelation.ImageBeforeText
            };
            if (_imageService != null)
            {
                var helpIcon = _imageService.GetImage("help");
                if (helpIcon != null)
                {
                    _btnHelp.Image = helpIcon;
                    _btnHelp.Text = string.Empty; // Icon-only for cleaner look
                }
            }
            _btnHelp.Click += (s, e) => HelpClicked?.Invoke(this, EventArgs.Empty);
            _btnHelp.KeyDown += (s, e) =>
            {
                if (e.Alt && e.KeyCode == Keys.H)
                {
                    _btnHelp.PerformClick();
                    e.Handled = true;
                }
            };
            _toolTip.SetToolTip(_btnHelp, "Show help (Alt+H)");

            // Close button (with optional icon)
            _btnClose = new SfButton
            {
                Text = "Close",
                AutoSize = false,
                Size = new Size(buttonWidth, buttonHeight),
                Margin = new Padding(BUTTON_MARGIN_H, BUTTON_MARGIN_V, BUTTON_MARGIN_H, BUTTON_MARGIN_V),
                AccessibleName = "Close",
                TabStop = true,
                TextImageRelation = TextImageRelation.ImageBeforeText
            };
            _btnClose.Click += (s, e) => CloseClicked?.Invoke(this, EventArgs.Empty);
            _btnClose.KeyDown += (s, e) =>
            {
                if (e.Alt && e.KeyCode == Keys.C)
                {
                    _btnClose.PerformClick();
                    e.Handled = true;
                }
            };
            _toolTip.SetToolTip(_btnClose, "Close panel (Alt+C or Esc)");

            // Build actions panel layout
            actionsPanel.Controls.Add(_btnRefresh);
            actionsPanel.Controls.Add(_loadingSpinner);
            actionsPanel.Controls.Add(_btnPin);
            actionsPanel.Controls.Add(_btnHelp);
            actionsPanel.Controls.Add(_btnClose);

            // Add to control hierarchy (order matters: actionsPanel on top, title label behind)
            Controls.Add(actionsPanel);
            Controls.Add(_titleLabel);

            // Accessibility
            AccessibleName = "Panel Header";
            AccessibleDescription = "Contains the title and action buttons for the panel";
        }

        private void RefreshButton_Click(object? sender, EventArgs e)
        {
            // Prevent multiple rapid clicks
            if (_refreshInProgress) return;

            _refreshInProgress = true;
            try
            {
                RefreshClicked?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                _refreshInProgress = false;
            }
        }

        private void PinButton_Click(object? sender, EventArgs e)
        {
            IsPinned = !IsPinned;
            UpdatePinButtonIcon(); // Update icon when pin state changes
            PinToggled?.Invoke(this, EventArgs.Empty);
        }

        private void UpdatePinButtonAppearance()
        {
            if (_btnPin == null) return;

            // Update button appearance based on pinned state
            // SfButton styling is managed by SfSkinManager theme cascade from parent form
            // Avoid manual BackColor/ForeColor assignments - theme system handles this
            if (string.IsNullOrEmpty(_btnPin.Image?.ToString()))
            {
                // Text-only mode (no icon service)
                _btnPin.Text = _isPinned ? "Unpin" : "Pin";
            }
            // If using icons, keep icon; don't change text

            _btnPin.AccessibleName = _isPinned ? "Unpin panel" : "Pin panel";

            if (_toolTip != null)
            {
                var tooltip = _isPinned ? "Unpin panel (keep it unlocked)" : "Pin panel (keep it open)";
                _toolTip.SetToolTip(_btnPin, tooltip);
            }
        }

        private void UpdatePinButtonIcon()
        {
            if (_btnPin == null || _imageService == null) return;

            // Update pin icon based on pinned state (filled vs outline)
            var iconName = _isPinned ? "pin_filled" : "pin";
            var icon = _imageService.GetImage(iconName);
            if (icon != null)
            {
                _btnPin.Image = icon;
                _btnPin.Text = string.Empty; // Icon-only
            }
        }

        /// <summary>
        /// Programmatically trigger the Refresh action.
        /// </summary>
        public void TriggerRefresh() => RefreshClicked?.Invoke(this, EventArgs.Empty);

        /// <summary>
        /// Set the loading state temporarily for an async operation.
        /// Usage: await SetLoadingAsync(async () => await MyLongOperationAsync());
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
