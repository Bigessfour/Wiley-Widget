using System;
using System.Drawing;
using System.ComponentModel;
using System.Windows.Forms;
using WileyWidget.WinForms.Theming;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.Drawing;
using Syncfusion.WinForms.Controls;

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

        public event EventHandler? ActionButtonClicked;

        public NoDataOverlay()
        {
            InitializeComponent();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { _messageLabel?.Dispose(); } catch { }
                try { _actionButton?.Dispose(); } catch { }
            }

            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            // Configure gradient panel - let SkinManager handle background via theme cascade
            this.BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty);
            SfSkinManager.SetVisualStyle(this, "Office2019Colorful");

            Dock = DockStyle.Fill;
            // BackColor inherited from theme cascade
            Visible = false;

            _messageLabel = new Label
            {
                AutoSize = true,
                Anchor = AnchorStyles.None,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 11.0f, FontStyle.Regular),
                // ForeColor inherited from theme cascade
                Text = "No data available",
                AccessibleName = "No data message",
                TabStop = true
            };

            // Center message on layout/resize and constrain width for wrapping
            this.Layout += (s, e) => CenterControls();
            this.Resize += (s, e) => CenterControls();

            // Make overlay accessible and keyboard-visible when present
            AccessibleName = "No data overlay";
            AccessibleDescription = "Indicates there is currently no data to display in this panel";

            Controls.Add(_messageLabel);
            CenterControls();
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
            if (_messageLabel == null) return;
            try
            {
                // Constrain message width to parent's client width minus padding so it wraps sensibly.
                _messageLabel.MaximumSize = new Size(Math.Max(80, this.ClientSize.Width - 40), 0);
                // Force layout update to get correct size
                _messageLabel.AutoSize = true;
                _messageLabel.PerformLayout();

                // Calculate positions
                int totalHeight = _messageLabel.Height;
                if (_actionButton != null && !string.IsNullOrEmpty(ActionButtonText))
                {
                    totalHeight += _actionButton.Height + 20; // 20 pixels spacing
                }

                int startY = Math.Max(0, (ClientSize.Height - totalHeight) / 2);

                // Center message
                var msgX = Math.Max(0, (ClientSize.Width - _messageLabel.Width) / 2);
                _messageLabel.Location = new Point(msgX, startY);

                // Center button if visible
                if (_actionButton != null && !string.IsNullOrEmpty(ActionButtonText))
                {
                    int btnX = Math.Max(0, (ClientSize.Width - _actionButton.Width) / 2);
                    _actionButton.Location = new Point(btnX, startY + _messageLabel.Height + 20);
                    _actionButton.Visible = true;
                }
            }
            catch { }
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
                if (value) CenterControls();
                // If becoming visible, attempt to set accessibility focus for screen readers
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
                    TabStop = true
                };
                SfSkinManager.SetVisualStyle(_actionButton, "Office2019Colorful");
                _actionButton.Click += (s, e) =>
                {
                    ActionButtonClicked?.Invoke(this, EventArgs.Empty);
                    clickHandler?.Invoke(s, e);
                };
                Controls.Add(_actionButton);
                _actionButton.BringToFront();
            }

            _actionButton.Text = buttonText;
            CenterControls();
        }

        public void HideActionButton()
        {
            ActionButtonText = null;
            if (_actionButton != null)
            {
                _actionButton.Visible = false;
            }
            CenterControls();
        }

        /// <summary>
        /// Common binding pattern: bind this overlay's Visible property to a ViewModel boolean like HasData (or inverted IsEmpty).
        /// Example: var bs = new BindingSource { DataSource = viewModel }; noDataOverlay.DataBindings.Add("Visible", bs, "IsEmpty", true, DataSourceUpdateMode.OnPropertyChanged);
        /// </summary>
    }
}
