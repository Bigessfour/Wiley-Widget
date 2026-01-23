using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Syncfusion.Windows.Forms;
using SfSkinManager = Syncfusion.WinForms.Controls.SfSkinManager;

namespace WileyWidget.WinForms.Controls
{
    /// <summary>
    /// Lightweight KPI card used in summary panels. Small, reusable UserControl
    /// that displays a title, large value, and optional subtitle. DPI-aware
    /// sizing is applied using Syncfusion's DpiAware helper to match the app.
    /// </summary>
    public partial class KpiCardControl : UserControl
    {
        private Label _lblTitle;
        private Label _lblValue;
        private Label _lblSubtitle;

        /// <summary>
        /// Title text shown above the value.
        /// </summary>
        [Browsable(true)]
        [Category("Appearance")]
        [Description("Title text shown above the KPI value.")]
        [Localizable(true)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string Title
        {
            get => _lblTitle.Text;
            set => _lblTitle.Text = value;
        }

        /// <summary>
        /// Value text shown prominently.
        /// </summary>
        [Browsable(true)]
        [Category("Data")]
        [Description("Value text shown prominently in the card.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Value
        {
            get => _lblValue.Text;
            set => _lblValue.Text = value;
        }

        /// <summary>
        /// Optional subtitle (small, bottom-aligned).
        /// </summary>
        [Browsable(true)]
        [Category("Appearance")]
        [Description("Optional subtitle text shown below the value.")]
        [Localizable(true)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string Subtitle
        {
            get => _lblSubtitle.Text;
            set => _lblSubtitle.Text = value;
        }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public KpiCardControl()
        {
            InitializeComponent();
            ApplyThemeSafe();
        }

        private void InitializeComponent()
        {
            SuspendLayout();

            _lblTitle = new Label
            {
                Dock = DockStyle.Top,
                Height = Dpi(20f),
                Text = "Title",
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = false
            };

            _lblValue = new Label
            {
                Dock = DockStyle.Fill,
                Text = "0",
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = false
            };

            _lblSubtitle = new Label
            {
                Dock = DockStyle.Bottom,
                Height = Dpi(16f),
                Text = string.Empty,
                Font = new Font("Segoe UI", 8f, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = false
            };

            BorderStyle = BorderStyle.FixedSingle;
            // Slightly larger padding for better separation on high DPI
            Padding = new Padding(Dpi(10f));
            // Conservative minimum size to avoid layout collapse; allow parent to control height
            MinimumSize = new Size(Dpi(100f), Dpi(64f));
            // Do not force Height here; let parent layout manage the control size to avoid runtime layout exceptions

            Controls.Add(_lblValue);
            Controls.Add(_lblTitle);
            Controls.Add(_lblSubtitle);

            ResumeLayout(false);
        }

        private static int Dpi(float logicalPixels) => (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(logicalPixels);

        private void ApplyThemeSafe()
        {
            try
            {
                // Apply current app theme; swallow failures to avoid breaking designers
                SfSkinManager.SetVisualStyle(this, SfSkinManager.ApplicationVisualTheme ?? "Office2019Colorful");
            }
            catch { }
        }

        /// <summary>
        /// Convenience helper to set a numeric value and format it using the current culture.
        /// </summary>
        public void SetValueNumber(long n)
        {
            _lblValue.Text = n.ToString("N0");
        }

        /// <summary>
        /// Convenience helper to set a floating-point value with one decimal.
        /// </summary>
        public void SetValueDouble(double d)
        {
            _lblValue.Text = d.ToString("N1");
        }
    }
}
