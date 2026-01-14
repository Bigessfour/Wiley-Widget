using System;
using Syncfusion.WinForms.Controls;
using System.Drawing;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using WileyWidget.WinForms.Themes;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.Drawing;

namespace WileyWidget.WinForms.Controls
{
    /// <summary>
    /// A lightweight semi-transparent loading overlay with a marquee progress indicator and optional message.
    /// Designed to be docked Fill inside panels and toggled visible when panels are loading.
    /// </summary>
    public class LoadingOverlay : Syncfusion.Windows.Forms.Tools.GradientPanelExt
    {
        private Label? _messageLabel;
        private ProgressBarAdv? _progress;
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
            // Configure gradient panel - let SkinManager handle background via theme cascade
            this.BackgroundColor = new Syncfusion.Drawing.BrushInfo(Syncfusion.Drawing.GradientStyle.Vertical, Color.Empty, Color.Empty);
            Syncfusion.WinForms.Controls.SfSkinManager.SetVisualStyle(this, ThemeColors.DefaultTheme);

            // Overlay should cover parent fully
            Dock = DockStyle.Fill;
            // BackColor inherited from theme cascade
            Visible = false;
            TabStop = false;

            // Center container
            var container = new WileyWidget.WinForms.Controls.GradientPanelExt
            {
                AutoSize = true,
                Padding = new Padding(12),
                BorderStyle = BorderStyle.None,
                BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
            };
            SfSkinManager.SetVisualStyle(container, ThemeColors.DefaultTheme);

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
                _progress = new Syncfusion.Windows.Forms.Tools.ProgressBarAdv
                {
                    ProgressStyle = Syncfusion.Windows.Forms.Tools.ProgressBarStyles.WaitingGradient,
                    WaitingGradientWidth = 20,
                    Width = 180,
                    Height = 18,
                    Dock = DockStyle.Bottom,
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
