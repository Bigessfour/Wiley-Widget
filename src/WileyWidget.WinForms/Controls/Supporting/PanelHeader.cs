using System;
using System.Drawing;
using System.ComponentModel;
using System.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.Drawing;
using Syncfusion.WinForms.Controls;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Factories;

namespace WileyWidget.WinForms.Controls.Supporting
{
    /// <summary>
    /// Shared header used by docked panels.
    /// Provides a consistent title bar with actions: Refresh, Pin, Help, and Close.
    ///
    /// Features:
    /// - Fixed height (52px) with consistent padding and button spacing
    /// - Four action buttons with full keyboard shortcut support (Alt+R, Alt+P, Alt+H, Alt+C, Esc)
    /// - Loading indicator (ProgressBarAdv) during async operations
    /// - Theme-aware styling via SfSkinManager cascade
    /// - Optional icon support via DpiAwareImageService (graceful text fallback)
    /// - Rich tooltips with usage guidance and keyboard shortcuts
    /// - Full accessibility support (AccessibleName, AccessibleDescription)
    ///
    /// Usage:
    ///   var header = new PanelHeader(imageService) { Title = "My Panel" };
    ///   header.RefreshClicked += async (s,e) => await LoadDataAsync();
    ///   header.IsLoading = true;  // During long operations
    ///   this.Controls.Add(header);
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
        /// <summary>Optional factory injected via the Sacred Panel Skeleton constructor path.</summary>
        private SyncfusionControlFactory? _factory;

        private bool _isPinned;
        private bool _isLoading;
        private bool _isRefreshing;
        private bool _refreshInProgress;
        private bool _helpButtonVisible = true;
        private bool _refreshButtonVisible = true;
        private bool _pinButtonVisible = true;
        private bool _closeButtonVisible = true;

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
                    UpdateRefreshButtonText();
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

        /// <summary>Show/hide the Pin button (default: true).</summary>
        [Browsable(true)]
        [DefaultValue(true)]
        public bool ShowPinButton
        {
            get => _pinButtonVisible;
            set
            {
                if (_pinButtonVisible == value) return;
                _pinButtonVisible = value;
                if (_btnPin != null)
                {
                    _btnPin.Visible = value;
                }
            }
        }

        /// <summary>Show/hide the Close button (default: true).</summary>
        [Browsable(true)]
        [DefaultValue(true)]
        public bool ShowCloseButton
        {
            get => _closeButtonVisible;
            set
            {
                if (_closeButtonVisible == value) return;
                _closeButtonVisible = value;
                if (_btnClose != null)
                {
                    _btnClose.Visible = value;
                }
            }
        }

        /// <summary>Get/set refreshing state. Controls Refresh button text independently of general loading.</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsRefreshing
        {
            get => _isRefreshing;
            set
            {
                if (_isRefreshing == value) return;
                _isRefreshing = value;
                UpdateRefreshButtonText();
            }
        }

        public PanelHeader() : this((DpiAwareImageService?)null)
        {
        }

        /// <summary>
        /// Initialises the header via the <see cref="SyncfusionControlFactory"/> (Sacred Panel
        /// Skeleton §1 and Syncfusion Control Creation Rule in WileyWidgetUIStandards §6).
        /// All <see cref="Syncfusion.WinForms.Controls.SfButton"/> and
        /// <see cref="ProgressBarAdv"/> instances are created through the factory so that
        /// mandatory theming and property enforcement is guaranteed.
        /// </summary>
        public PanelHeader(SyncfusionControlFactory factory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            Margin = Padding.Empty;
            Padding = Padding.Empty;

            try
            {
                var theme = SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme;
                SfSkinManager.SetVisualStyle(this, theme);
            }
            catch { /* Theme application is best-effort */ }

            InitializeComponent();
            UpdatePinButtonAppearance();
        }

        /// <summary>
        /// Initialize PanelHeader with optional icon service for modern button icons.
        /// If no service is provided, buttons display text labels only.
        ///
        /// Icons are loaded from the provided DpiAwareImageService using these keys:
        /// - "refresh" : Refresh/reload icon
        /// - "pin" : Unpinned state pin icon (outline)
        /// - "pin_filled" : Pinned state pin icon (filled)
        /// - "help" : Question mark or help icon
        /// - "close" : X or close icon
        ///
        /// If any icon fails to load, buttons gracefully fall back to text labels.
        /// </summary>
        public PanelHeader(DpiAwareImageService? imageService)
        {
            _imageService = imageService;
            Margin = Padding.Empty;
            Padding = Padding.Empty;

            // Apply Syncfusion theme to header
            try
            {
                var theme = SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme;
                SfSkinManager.SetVisualStyle(this, theme);
            }
            catch { /* Theme application is best-effort */ }

            InitializeComponent();
            UpdatePinButtonAppearance();
            // Theme cascade from SetVisualStyle applies to all child controls
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

        // ── Factory-aware control-creation helpers ──────────────────────────

        /// <summary>
        /// Creates a header action button via the injected <see cref="SyncfusionControlFactory"/>
        /// when available, or falls back to direct instantiation for the
        /// <see cref="PanelHeader(DpiAwareImageService?)"/> code-path and design-time use.
        /// </summary>
        private SfButton CreateActionButton(string text, int width, int height, string accessibleName)
        {
            var margin = new Padding(BUTTON_MARGIN_H, BUTTON_MARGIN_V, BUTTON_MARGIN_H, BUTTON_MARGIN_V);
            var size = new Size(width, height);

            if (_factory != null)
            {
                return _factory.CreateSfButton(text, btn =>
                {
                    btn.AutoSize = false;
                    btn.Size = size;
                    btn.Margin = margin;
                    btn.AccessibleName = accessibleName;
                    btn.TabStop = true;
                    btn.TextImageRelation = TextImageRelation.ImageBeforeText;
                    btn.ImageAlign = ContentAlignment.MiddleCenter;
                });
            }

            return new SfButton
            {
                Text = text,
                AutoSize = false,
                Size = size,
                Margin = margin,
                AccessibleName = accessibleName,
                TabStop = true,
                TextImageRelation = TextImageRelation.ImageBeforeText,
                ImageAlign = ContentAlignment.MiddleCenter,
            };
        }

        /// <summary>
        /// Creates the loading spinner via the factory when available, otherwise falls back
        /// to direct instantiation.
        /// </summary>
        private ProgressBarAdv CreateLoadingSpinner(int width, int height)
        {
            var size = new Size(width, height);
            var margin = new Padding(BUTTON_MARGIN_H, BUTTON_MARGIN_V, BUTTON_MARGIN_H, BUTTON_MARGIN_V);

            if (_factory != null)
            {
                return _factory.CreateProgressBarAdv(pb =>
                {
                    pb.Value = 50;
                    pb.Maximum = 100;
                    pb.Minimum = 0;
                    pb.AutoSize = false;
                    pb.Margin = margin;
                    pb.Size = size;
                    pb.Visible = false;
                    pb.ProgressStyle = ProgressBarStyles.WaitingGradient;
                    pb.WaitingGradientWidth = 10;
                    pb.TextVisible = false;
                    pb.TextShadow = false;
                });
            }

            return new ProgressBarAdv
            {
                Value = 50,
                Maximum = 100,
                Minimum = 0,
                AutoSize = false,
                Margin = margin,
                Size = size,
                Visible = false,
                ProgressStyle = ProgressBarStyles.WaitingGradient,
                WaitingGradientWidth = 10,
                TextVisible = false,
                TextShadow = false,
            };
        }

        // ── Layout init ────────────────────────────────────────────────────────

        private void InitializeComponent()
        {
            Height = HEADER_HEIGHT;
            MinimumSize = new Size(0, HEADER_HEIGHT); // Prevent collapse
            AutoSize = false; // Explicit false - we control the height
            Padding = new Padding(8);
            Dock = DockStyle.Top;

            // Compute button sizing based on header height and padding
            var innerHeight = HEADER_HEIGHT - Padding.Vertical;
            var buttonHeight = Math.Max(20, innerHeight - (BUTTON_MARGIN_V * 2));
            var buttonWidth = 80; // reasonable width for text buttons

            _toolTip = new ToolTip();

            // Title label (use inherited theme font for consistency)
            _titleLabel = new Label
            {
                Name = "headerLabel",
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
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

            // Refresh button — created via factory when available (Syncfusion Control Creation Rule)
            _btnRefresh = CreateActionButton("Refresh", buttonWidth, buttonHeight, "Refresh");
            if (_imageService != null)
            {
                try
                {
                    var refreshIcon = _imageService.GetImage("refresh");
                    if (refreshIcon != null)
                    {
                        _btnRefresh.Image = refreshIcon;
                        _btnRefresh.Text = string.Empty; // Icon-only for cleaner look
                        _btnRefresh.Size = new Size(40, buttonHeight); // Compact width for icon-only
                    }
                }
                catch (Exception ex)
                {
                    // Gracefully fall back to text if icon loading fails
                    System.Diagnostics.Debug.WriteLine($"Warning: Failed to load refresh icon: {ex.Message}");
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
            _toolTip.SetToolTip(_btnRefresh, "Refresh data\n\nReloads the current view with latest information from the server.\n\nKeyboard: Alt+R");

            // Loading spinner (ProgressBarAdv, marquee-style) — created via factory when available
            _loadingSpinner = CreateLoadingSpinner(24, Math.Max(12, buttonHeight - 4));
            _loadingSpinner.AccessibleName = "Loading indicator - Data is being refreshed, please wait";
            _toolTip.SetToolTip(_loadingSpinner, "Loading in progress\n\nOperation may take a few moments");

            // Pin button — created via factory when available
            _btnPin = CreateActionButton("Pin", buttonWidth, buttonHeight, "Pin");
            _btnPin.Click += PinButton_Click;
            _btnPin.KeyDown += (s, e) =>
            {
                if (e.Alt && e.KeyCode == Keys.P)
                {
                    _btnPin.PerformClick();
                    e.Handled = true;
                }
            };
            UpdatePinButtonIcon(); // Apply icon and tooltip after initialization

            // Help button — created via factory when available
            _btnHelp = CreateActionButton("Help", buttonWidth, buttonHeight, "Help");
            _btnHelp.Visible = _helpButtonVisible;
            if (_imageService != null)
            {
                try
                {
                    var helpIcon = _imageService.GetImage("help");
                    if (helpIcon != null)
                    {
                        _btnHelp.Image = helpIcon;
                        _btnHelp.Text = string.Empty; // Icon-only for cleaner look
                        _btnHelp.Size = new Size(40, buttonHeight); // Compact width for icon-only
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Warning: Failed to load help icon: {ex.Message}");
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
            _toolTip.SetToolTip(_btnHelp, "Show help\n\nDisplays additional information and guidance for this panel.\n\nKeyboard: Alt+H");

            // Close button — created via factory when available
            _btnClose = CreateActionButton("Close", buttonWidth, buttonHeight, "Close");
            if (_imageService != null)
            {
                try
                {
                    var closeIcon = _imageService.GetImage("close");
                    if (closeIcon != null)
                    {
                        _btnClose.Image = closeIcon;
                        _btnClose.Text = string.Empty; // Icon-only for cleaner look
                        _btnClose.Size = new Size(40, buttonHeight); // Compact width for icon-only
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Warning: Failed to load close icon: {ex.Message}");
                }
            }
            _btnClose.Click += (s, e) => CloseClicked?.Invoke(this, EventArgs.Empty);
            _btnClose.KeyDown += (s, e) =>
            {
                if (e.Alt && e.KeyCode == Keys.C)
                {
                    _btnClose.PerformClick();
                    e.Handled = true;
                }
            };
            _toolTip.SetToolTip(_btnClose, "Close panel\n\nDismisses this panel and returns to the main view.\n\nKeyboard: Alt+C or Esc");

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

            UpdatePinButtonIcon(); // Ensure icon is loaded

            // Update button appearance based on pinned state
            // SfButton styling is managed by SfSkinManager theme cascade from parent form
            // Avoid manual BackColor/ForeColor assignments - theme system handles this
            if (_btnPin.Image == null)
            {
                // Text-only mode (no icon service)
                _btnPin.Text = _isPinned ? "Unpin" : "Pin";
            }
            // If using icons, keep icon; don't change text

            _btnPin.AccessibleName = _isPinned ? "Unpin panel" : "Pin panel";

            if (_toolTip != null)
            {
                var tooltip = _isPinned
                    ? "Unpin panel\n\nRemoves the pin to allow this panel to auto-close or be rearranged.\n\nKeyboard: Alt+P"
                    : "Pin panel\n\nKeeps this panel open and prevents it from auto-closing when navigating.\n\nKeyboard: Alt+P";
                _toolTip.SetToolTip(_btnPin, tooltip);
            }
        }

        private void UpdateRefreshButtonText()
        {
            if (_btnRefresh == null) return;

            if (_isLoading && _isRefreshing)
            {
                _btnRefresh.Text = "Refreshing...";
                _btnRefresh.Size = new Size(80, _btnRefresh.Height); // Wider for text
            }
            else if (_btnRefresh.Image != null)
            {
                _btnRefresh.Text = string.Empty;
                _btnRefresh.Size = new Size(40, _btnRefresh.Height); // Compact for icon
            }
            else
            {
                _btnRefresh.Text = "Refresh";
                _btnRefresh.Size = new Size(80, _btnRefresh.Height); // Default width
            }
        }

        private void UpdatePinButtonIcon()
        {
            if (_btnPin == null || _imageService == null) return;

            try
            {
                // Update pin icon based on pinned state (filled vs outline)
                var iconName = _isPinned ? "pin_filled" : "pin";
                var icon = _imageService.GetImage(iconName);
                if (icon != null)
                {
                    _btnPin.Image = icon;
                    _btnPin.Text = string.Empty; // Icon-only
                    _btnPin.Size = new Size(40, _btnPin.Height); // Compact width for icon-only
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Warning: Failed to load pin button icon: {ex.Message}");
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
