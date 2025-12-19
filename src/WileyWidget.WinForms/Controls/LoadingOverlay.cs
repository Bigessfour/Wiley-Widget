using System;
using System.Drawing;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using WileyWidget.WinForms.Theming;

namespace WileyWidget.WinForms.Controls
{
    /// <summary>
    /// A lightweight semi-transparent loading overlay with a marquee progress indicator and optional message.
    /// Designed to be docked Fill inside panels and toggled visible when panels are loading.
    /// </summary>
    public class LoadingOverlay : Panel
    {
        private Label? _messageLabel;
        private ProgressBar? _progress;
        private Control? _spinnerHost;

        public LoadingOverlay()
        {
            InitializeComponent();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { _progress?.Dispose(); } catch { }
                try { _messageLabel?.Dispose(); } catch { }
                try { _spinnerHost?.Dispose(); } catch { }
            }

            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            // Overlay should cover parent fully
            Dock = DockStyle.Fill;
            // BackColor inherited from theme cascade
            Visible = false;
            TabStop = false;

            // Center container
            var container = new Panel
            {
                AutoSize = true,
                Padding = new Padding(12)
            };

            // Prefer using Syncfusion waiting control when available (reflection to avoid hard dependency)
            object? syncControl = null;
            try
            {
                var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name?.StartsWith("Syncfusion", StringComparison.Ordinal) ?? false);
                if (assembly != null)
                {
                    var waitingBarType = assembly.GetType("Syncfusion.Windows.Forms.Tools.WaitingBar");
                    if (waitingBarType != null)
                    {
                        syncControl = Activator.CreateInstance(waitingBarType);
                    }
                }
            }
            catch { /* best effort - fall back to standard progress bar */ }

            if (syncControl is Control sfWait)
            {
                // Use the Syncfusion waiting control when available
                _progress = null; // leave _progress null when using Syncfusion control
                sfWait.Width = 40;
                sfWait.Height = 40;
                sfWait.Anchor = AnchorStyles.None;
                _spinnerHost = sfWait;
            }
            else
            {
                _progress = new ProgressBar
                {
                    Style = ProgressBarStyle.Marquee,
                    MarqueeAnimationSpeed = 30,
                    Width = 180,
                    Height = 18,
                    Anchor = AnchorStyles.None
                };
                _spinnerHost = _progress;

                _messageLabel = new Label
                {
                    AutoSize = true,
                    // ForeColor inherited from theme cascade
                    Font = new Font("Segoe UI", 9.0f, FontStyle.Regular),
                    Text = WileyWidget.WinForms.Forms.MainFormResources.LoadingText,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Anchor = AnchorStyles.None,
                    Margin = new Padding(0, 6, 0, 0)
                };

                var inner = new FlowLayoutPanel
                {
                    FlowDirection = FlowDirection.TopDown,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    Anchor = AnchorStyles.None
                };

                // Add whichever spinner control we resolved
                inner.Controls.Add(_spinnerHost);
                inner.Controls.Add(_messageLabel);

                container.Controls.Add(inner);

                Controls.Add(container);

                // Layout center
                container.Location = new Point((Width - container.Width) / 2, (Height - container.Height) / 2);
                container.Anchor = AnchorStyles.None | AnchorStyles.Top;

                this.Resize += (s, e) =>
                {
                    container.Location = new Point((Width - container.Width) / 2, (Height - container.Height) / 2);
                };
            }

        }

        /// <summary>
        /// Set or get the overlay message shown under the progress indicator.
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Message
        {
            get => _messageLabel?.Text ?? string.Empty;
            set { if (_messageLabel != null) _messageLabel.Text = value ?? string.Empty; }
        }

        /// <summary>
        /// Allow data-binding with a boolean property (for convenience).
        /// Use a BindingSource in the host panel like:
        /// var bs = new BindingSource { DataSource = viewModel }; loadingOverlay.DataBindings.Add("Visible", bs, "IsLoading", true, DataSourceUpdateMode.OnPropertyChanged);
        /// </summary>

        /// <summary>
        /// Toggle the overlay.
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new bool Visible
        {
            get => base.Visible;
            set
            {
                base.Visible = value;
                // keep the overlay on top
                if (Parent != null && value)
                {
                    BringToFront();
                }
            }
        }
    }
}
