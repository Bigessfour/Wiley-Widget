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
    public class LoadingOverlay : GradientPanelExt
    {
        private Label? _messageLabel;
        private ProgressBarAdv? _progress;
        private Control? _spinnerHost;
        private ToolTip? _toolTip;

        public LoadingOverlay()
        {
            InitializeComponent();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Dispose controls
                try { _progress?.Dispose(); } catch { }
                try { _messageLabel?.Dispose(); } catch { }
                try { _spinnerHost?.Dispose(); } catch { }
                _toolTip?.Dispose();
            }

            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            // Configure gradient panel - let SFSkinManager handle background via theme cascade
            var currentTheme = ThemeColors.DefaultTheme;
            try
            {
                currentTheme = SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme;
                SfSkinManager.SetVisualStyle(this, currentTheme);
            }
            catch
            {
                currentTheme = ThemeColors.DefaultTheme;
            }
            ThemeName = currentTheme;

            // Overlay should cover parent fully
            Dock = DockStyle.Fill;
            Visible = false;
            TabStop = false;

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
            var container = new WileyWidget.WinForms.Controls.GradientPanelExt
            {
                AutoSize = true,
                Padding = new Padding(12),
                BorderStyle = BorderStyle.None,
                Dock = DockStyle.Fill
            };
            try
            {
                SfSkinManager.SetVisualStyle(container, currentTheme);
            }
            catch
            {
                // SfSkinManager may not be available in designer context
            }
            container.ThemeName = currentTheme;

            // Use ProgressBarAdv with waiting gradient style for consistent loading indicator
            _progress = new Syncfusion.Windows.Forms.Tools.ProgressBarAdv
            {
                ProgressStyle = Syncfusion.Windows.Forms.Tools.ProgressBarStyles.WaitingGradient,
                WaitingGradientWidth = 20,
                Width = 180,
                Height = 18,
                Dock = DockStyle.Bottom,
                Anchor = AnchorStyles.None,
                ThemeName = currentTheme
            };
            try
            {
                SfSkinManager.SetVisualStyle(_progress, currentTheme);
            }
            catch
            {
                // SfSkinManager may not be available in designer context
            }
            _spinnerHost = _progress;

            _messageLabel = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 9.0f, FontStyle.Regular),
                Text = WileyWidget.WinForms.Forms.MainFormResources.LoadingText,
                TextAlign = ContentAlignment.MiddleCenter,
                Anchor = AnchorStyles.None,
                Margin = new Padding(0, 6, 0, 0),
                AccessibleName = "Loading Message",
                AccessibleDescription = "Displays the current loading status message"
            };

            _toolTip = new ToolTip();
            _toolTip.SetToolTip(_messageLabel, "Loading data, please wait");

            var inner = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Anchor = AnchorStyles.None,
                AccessibleName = "Loading Indicator Container",
                AccessibleDescription = "Contains the loading spinner and message"
            };

            // Add the spinner control and message
            inner.Controls.Add(_spinnerHost);
            inner.Controls.Add(_messageLabel);

            container.Controls.Add(inner);

            table.Controls.Add(container, 1, 1);
            Controls.Add(table);
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
