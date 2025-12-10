using System;
using System.Drawing;
using System.ComponentModel;
using System.Windows.Forms;

namespace WileyWidget.WinForms.Controls
{
    /// <summary>
    /// Simple centered message for no-data states. Designed to be docked Fill and shown when the panel's collection is empty.
    /// </summary>
    public class NoDataOverlay : Panel
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
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(16, Color.Gray); // subtle tint to indicate empty state
            Visible = false;

            _messageLabel = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 11.0f, FontStyle.Regular),
                ForeColor = Color.DimGray,
                Text = "No data available",
                AccessibleName = "No data message"
            };

            // Make overlay accessible and keyboard-visible when present
            AccessibleName = "No data overlay";
            AccessibleDescription = "Indicates there is currently no data to display in this panel";

            Controls.Add(_messageLabel);
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Message
        {
            get => _messageLabel.Text;
            set => _messageLabel.Text = value ?? string.Empty;
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
