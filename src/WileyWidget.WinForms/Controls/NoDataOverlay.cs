using System;
using System.Drawing;
using System.ComponentModel;
using System.Windows.Forms;
using WileyWidget.WinForms.Theming;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.Drawing;

namespace WileyWidget.WinForms.Controls
{
    /// <summary>
    /// Simple centered message for no-data states. Designed to be docked Fill and shown when the panel's collection is empty.
    /// </summary>
    public class NoDataOverlay : Syncfusion.Windows.Forms.Tools.GradientPanelExt
    {
        private Label _messageLabel = null!;

        public NoDataOverlay()
        {
            InitializeComponent();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { _messageLabel?.Dispose(); } catch { }
            }

            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            // Configure gradient panel - let SkinManager handle background via theme cascade
            this.BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty);
            Syncfusion.WinForms.Controls.SfSkinManager.SetVisualStyle(this, "Office2019Colorful");

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
            this.Layout += (s, e) => CenterMessageLabel();
            this.Resize += (s, e) => CenterMessageLabel();

            // Make overlay accessible and keyboard-visible when present
            AccessibleName = "No data overlay";
            AccessibleDescription = "Indicates there is currently no data to display in this panel";

            Controls.Add(_messageLabel);
            CenterMessageLabel();
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Message
        {
            get => _messageLabel.Text;
            set
            {
                _messageLabel.Text = value ?? string.Empty;
                CenterMessageLabel();
            }
        }

        private void CenterMessageLabel()
        {
            if (_messageLabel == null) return;
            try
            {
                // Constrain message width to parent's client width minus padding so it wraps sensibly.
                _messageLabel.MaximumSize = new Size(Math.Max(80, this.ClientSize.Width - 40), 0);
                // Force layout update to get correct size
                _messageLabel.AutoSize = true;
                _messageLabel.PerformLayout();
                var x = Math.Max(0, (ClientSize.Width - _messageLabel.Width) / 2);
                var y = Math.Max(0, (ClientSize.Height - _messageLabel.Height) / 2);
                _messageLabel.Location = new Point(x, y);
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
                if (value) CenterMessageLabel();
                // If becoming visible, attempt to set accessibility focus for screen readers
                try { if (value) _messageLabel?.Focus(); } catch { }
            }
        }

        /// <summary>
        /// Common binding pattern: bind this overlay's Visible property to a ViewModel boolean like HasData (or inverted IsEmpty).
        /// Example: var bs = new BindingSource { DataSource = viewModel }; noDataOverlay.DataBindings.Add("Visible", bs, "IsEmpty", true, DataSourceUpdateMode.OnPropertyChanged);
        /// </summary>
    }
}
