using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Syncfusion.Windows.Forms;
using SfSkinManager = Syncfusion.WinForms.Controls.SfSkinManager;

namespace WileyWidget.WinForms.Controls.Panels
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
                Height = Dpi(22f),
                Text = "Title",
                TextAlign = ContentAlignment.BottomLeft,
                Font = new Font(Font.FontFamily, 7.75f, FontStyle.Regular),
                AutoSize = false,
                AutoEllipsis = true,
                AccessibleRole = AccessibleRole.StaticText,
                UseMnemonic = false
            };

            _lblValue = new Label
            {
                Dock = DockStyle.Fill,
                Text = "0",
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font(Font.FontFamily, 20f, FontStyle.Bold),
                AutoSize = false,
                AccessibleRole = AccessibleRole.StaticText,
                AccessibleName = "KPI Value",
                UseMnemonic = false
            };

            _lblSubtitle = new Label
            {
                Dock = DockStyle.Bottom,
                Height = Dpi(16f),
                Text = string.Empty,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = false,
                AccessibleRole = AccessibleRole.StaticText,
                UseMnemonic = false
            };

            BorderStyle = BorderStyle.None;
            // Top padding is slightly larger to give the accent bar visual breathing room
            Padding = new Padding(Dpi(8f), Dpi(10f), Dpi(8f), Dpi(6f));
            // Conservative minimum size to avoid layout collapse; allow parent to control height
            MinimumSize = new Size(Dpi(100f), Dpi(64f));
            // Accessibility improvements
            AccessibleRole = AccessibleRole.Grouping;
            AccessibleName = "KPI Card";
            AccessibleDescription = "Displays a key performance indicator metric";

            Controls.Add(_lblValue);
            Controls.Add(_lblTitle);
            Controls.Add(_lblSubtitle);

            ResumeLayout(false);
        }

        private static int Dpi(float logicalPixels) => (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(logicalPixels);

        /// <inheritdoc />
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // 3px accent bar at top — geometric structural decoration; adapts to the Windows accent color
            using var accentPen = new Pen(SystemColors.Highlight, 3f);
            e.Graphics.DrawLine(accentPen, 0, 1, Width, 1);
            // Subtle 1px card border for visual separation without the heavy FixedSingle shadow
            using var borderPen = new Pen(SystemColors.ControlLight, 1f);
            e.Graphics.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
        }

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
