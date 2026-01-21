using System;
using System.Drawing;
using System.ComponentModel;
using System.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.Drawing;
using Syncfusion.WinForms.Controls;
using WileyWidget.WinForms.Themes;

namespace WileyWidget.WinForms.Controls
{
    /// <summary>
    /// Simple centered message for no-data states. Designed to be docked Fill and shown when the panel's collection is empty.
    /// Can optionally display an action button.
    /// </summary>
    public class NoDataOverlay : Syncfusion.Windows.Forms.Tools.GradientPanelExt
    {
        private Label _messageLabel = null!;
        private SfButton? _actionButton;
        private LayoutEventHandler? _layoutHandler;
        private EventHandler? _resizeHandler;  // Resize event uses EventHandler, not LayoutEventHandler
        private EventHandler? _actionButtonClickHandler;

        public event EventHandler? ActionButtonClicked;

        public NoDataOverlay()
        {
            InitializeComponent();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Unsubscribe events (Pattern K)
                if (_layoutHandler != null)
                {
                    this.Layout -= _layoutHandler;
                }
                if (_resizeHandler != null)
                {
                    this.Resize -= _resizeHandler;  // EventHandler type
                }
                if (_actionButton != null && _actionButtonClickHandler != null)
                {
                    _actionButton.Click -= _actionButtonClickHandler;
                }

                // Dispose controls
                _messageLabel?.Dispose();
                _actionButton?.Dispose();
            }

            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            // Configure gradient panel - SfSkinManager handles all theming
            var theme = SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme;
            SfSkinManager.SetVisualStyle(this, theme);
            this.ThemeName = theme;

            Dock = DockStyle.Fill;
            Visible = false;

            // Use TableLayoutPanel for dock-based centering
            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 3
            };
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            // Center container
            var container = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Anchor = AnchorStyles.None,
                WrapContents = false
            };
            SfSkinManager.SetVisualStyle(container, theme);

            _messageLabel = new Label
            {
                AutoSize = true,
                Anchor = AnchorStyles.None,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 11.0f, FontStyle.Regular),
                Text = "No data available",
                AccessibleName = "No data message",
                AccessibleDescription = "Displays a message when no data is available",
                TabIndex = 1,
                TabStop = true
            };
            var tooltip = new ToolTip();
            tooltip.SetToolTip(_messageLabel, "No data is currently available for display");

            container.Controls.Add(_messageLabel);

            table.Controls.Add(container, 1, 1);
            Controls.Add(table);

            // Store handlers for cleanup (Pattern A & K)
            _layoutHandler = (s, e) => CenterControls();
            _resizeHandler = (s, e) => CenterControls();
            this.Layout += _layoutHandler;
            this.Resize += _resizeHandler;

            // Make overlay accessible
            AccessibleName = "No data overlay";
            AccessibleDescription = "Indicates there is currently no data to display in this panel";
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Message
        {
            get => _messageLabel.Text;
            set
            {
                _messageLabel.Text = value ?? string.Empty;
                CenterControls();
            }
        }

        [Browsable(true)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string? ActionButtonText { get; set; }

        private void CenterControls()
        {
            // Layout handled by TableLayoutPanel and FlowLayoutPanel - no manual positioning needed
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new bool Visible
        {
            get => base.Visible;
            set
            {
                base.Visible = value;
                if (Parent != null && value)
                    BringToFront();
                // If becoming visible, attempt to set accessibility focus
                try { if (value && _actionButton?.Visible == true) _actionButton?.Focus(); else _messageLabel?.Focus(); } catch { }
            }
        }

        public void ShowActionButton(string buttonText, EventHandler clickHandler)
        {
            ActionButtonText = buttonText;

            if (_actionButton == null)
            {
                _actionButton = new SfButton
                {
                    Size = new Size(140, 40),
                    Font = new Font("Segoe UI", 10.0f, FontStyle.Bold),
                    AccessibleName = "Action button",
                    AccessibleDescription = "Button to perform an action when no data is available",
                    TabIndex = 2,
                    TabStop = true
                };
                var theme = SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme;
                SfSkinManager.SetVisualStyle(_actionButton, theme);
                _actionButton.ThemeName = theme;

                var tooltip = new ToolTip();
                tooltip.SetToolTip(_actionButton, "Click to perform an action");

                _actionButtonClickHandler = (s, e) =>
                {
                    ActionButtonClicked?.Invoke(this, EventArgs.Empty);
                    clickHandler?.Invoke(s, e);
                };
                _actionButton.Click += _actionButtonClickHandler;

                // Add to container for proper layout
                var container = Controls[0] as TableLayoutPanel; // Assuming table is first control
                var innerContainer = container?.GetControlFromPosition(1, 1) as FlowLayoutPanel;
                innerContainer?.Controls.Add(_actionButton);
            }

            _actionButton.Text = buttonText;
        }

        public void HideActionButton()
        {
            ActionButtonText = null;
            if (_actionButton != null)
            {
                _actionButton.Visible = false;
            }
        }

        /// <summary>
        /// Common binding pattern: bind this overlay's Visible property to a ViewModel boolean like HasData (or inverted IsEmpty).
        /// Example: var bs = new BindingSource { DataSource = viewModel }; noDataOverlay.DataBindings.Add("Visible", bs, "IsEmpty", true, DataSourceUpdateMode.OnPropertyChanged);
        /// </summary>
    }
}
