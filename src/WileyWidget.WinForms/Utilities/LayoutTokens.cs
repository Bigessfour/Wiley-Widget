using System;
using System.Drawing;
using System.Windows.Forms;

namespace WileyWidget.WinForms.Utilities
{
    /// <summary>
    /// Shared layout constants for spacing, sizing, and DPI-aware scaling.
    /// All hardcoded heights, paddings, and margins throughout the panel layer
    /// should be expressed via these tokens so values stay consistent and
    /// DPI-correct across every display configuration.
    /// </summary>
    public static class LayoutTokens
    {
        // DPI scale (cached at type initialization, single read per process).

        private static readonly float _dpiScale = ComputeDpiScale();

        private static float ComputeDpiScale()
        {
            try
            {
                using var g = Graphics.FromHwnd(IntPtr.Zero);
                return g.DpiX / 96f;
            }
            catch
            {
                return 1f;
            }
        }

        /// <summary>
        /// Gets the current process scale factor relative to 96 DPI.
        /// </summary>
        public static float ScaleFactor => _dpiScale;

        /// <summary>
        /// Converts a logical pixel value (at 96 DPI) to physical pixels at the
        /// primary display scale factor.
        /// </summary>
        public static int Dp(int px) => (int)Math.Round(px * _dpiScale);

        /// <summary>
        /// Scales a logical integer value to device pixels.
        /// </summary>
        public static int GetScaled(int baseValue) => Dp(baseValue);

        /// <summary>
        /// Scales a logical size to device pixels.
        /// </summary>
        public static Size GetScaled(Size baseSize) => new(Dp(baseSize.Width), Dp(baseSize.Height));

        /// <summary>
        /// Scales logical padding to device pixels.
        /// </summary>
        public static Padding GetScaled(Padding basePadding) => new(
            Dp(basePadding.Left),
            Dp(basePadding.Top),
            Dp(basePadding.Right),
            Dp(basePadding.Bottom));

        // Spacing tokens (logical pixels; pass through GetScaled/Dp for physical).

        /// <summary>Standard inner padding for panels and containers.</summary>
        public const int PanelPadding = 12;

        /// <summary>Panel shell outer padding token.</summary>
        public static readonly Padding PanelOuterPadding = new(PanelPadding);

        /// <summary>Inner content-host padding token.</summary>
        public static readonly Padding ContentInnerPadding = new(8);

        /// <summary>Standard section shell padding for card-like panel regions.</summary>
        public static readonly Padding SectionPanelPadding = new(12, 8, 12, 8);

        /// <summary>Compact margin token used by metric cards and small containers.</summary>
        public static readonly Padding CardMargin = new(4);

        /// <summary>Compact uniform panel padding token.</summary>
        public static readonly Padding PanelPaddingCompact = new(8);

        /// <summary>Tight panel padding token for dense container shells.</summary>
        public static readonly Padding PanelPaddingTight = new(5);

        /// <summary>Spacious panel padding token for chart/table host containers.</summary>
        public static readonly Padding PanelPaddingSpacious = new(12);

        /// <summary>Outer margin token for metric summary cards.</summary>
        public static readonly Padding MetricCardMargin = new(6);

        /// <summary>Compact toolbar padding token used in dense top-row controls.</summary>
        public static readonly Padding ToolbarPadding = new(4);

        /// <summary>Padding token for filter/tool rows.</summary>
        public static readonly Padding FilterRowPadding = new(6, 4, 6, 4);

        /// <summary>Padding token for button-like interactive surfaces.</summary>
        public static readonly Padding ButtonPadding = new(12, 6, 12, 6);

        /// <summary>Uniform inner padding token for dense text inputs and controls.</summary>
        public static readonly Padding InputTextPadding = new(8);

        /// <summary>Compact button padding token for dense panel toolbars.</summary>
        public static readonly Padding ButtonCompactPadding = new(8, 4, 8, 4);

        /// <summary>Compact text margin token for button captions.</summary>
        public static readonly Padding CompactTextMargin = new(2);

        /// <summary>Dialog content padding token.</summary>
        public static readonly Padding DialogContentPadding = new(10);

        /// <summary>Outer dialog shell padding token.</summary>
        public static readonly Padding DialogShellPadding = new(20);

        /// <summary>Standard input margin token for dense filter rows.</summary>
        public static readonly Padding InputControlMargin = new(5);

        /// <summary>Standard panel / section header height.</summary>
        public const int HeaderHeight = 40;

        /// <summary>Minimum compact header host height for panels with toolbars.</summary>
        public const int HeaderMinimumHeight = 60;

        /// <summary>Large panel header height used by high-density management screens.</summary>
        public const int HeaderHeightLarge = 50;

        /// <summary>Default large panel shell size (logical pixels).</summary>
        public static readonly Size DefaultDashboardPanelSize = new(1400, 900);

        /// <summary>Standard panel minimum size floor (logical pixels).</summary>
        public static readonly Size StandardPanelMinimumSize = new(1024, 720);

        /// <summary>Standard status bar height.</summary>
        public const int StatusBarHeight = 24;

        /// <summary>Default Syncfusion grid row height.</summary>
        public const int GridRowHeight = 28;

        /// <summary>Comfortable Syncfusion grid row height for dense history tables.</summary>
        public const int GridRowHeightComfortable = 30;

        /// <summary>Medium Syncfusion grid row height for interactive data-entry tables.</summary>
        public const int GridRowHeightMedium = 32;

        /// <summary>Tall Syncfusion grid row height for highly legible financial tables.</summary>
        public const int GridRowHeightTall = 36;

        /// <summary>Extra-tall Syncfusion grid row height for register-style records.</summary>
        public const int GridRowHeightExtraTall = 40;

        /// <summary>Default Syncfusion grid header row height.</summary>
        public const int GridHeaderRowHeight = 32;

        /// <summary>Comfortable Syncfusion grid header row height for high-information grids.</summary>
        public const int GridHeaderRowHeightComfortable = 38;

        /// <summary>Medium Syncfusion grid header row height for transaction-heavy screens.</summary>
        public const int GridHeaderRowHeightMedium = 36;

        /// <summary>Tall Syncfusion grid header row height for dense register views.</summary>
        public const int GridHeaderRowHeightTall = 40;

        /// <summary>Default action button size.</summary>
        public static readonly Size DefaultButtonSize = new(110, 34);

        /// <summary>Compact run/save button size.</summary>
        public static readonly Size ButtonSizeRunScenario = new(120, 32);

        /// <summary>Compact save button size.</summary>
        public static readonly Size ButtonSizeSaveCompact = new(96, 32);

        /// <summary>Proactive insights refresh button size.</summary>
        public static readonly Size ButtonSizeInsightsRefresh = new(135, 32);

        /// <summary>Proactive insights clear button size.</summary>
        public static readonly Size ButtonSizeInsightsClear = new(125, 32);

        /// <summary>Small square help/action button size.</summary>
        public static readonly Size ButtonSizeSquareSmall = new(32, 32);

        /// <summary>Tiny compact button size for settings forms.</summary>
        public static readonly Size ButtonSizeTiny = new(40, 24);

        /// <summary>Small compact button size for settings forms.</summary>
        public static readonly Size ButtonSizeSmallCompact = new(50, 24);

        /// <summary>Compact input size token (small).</summary>
        public static readonly Size InputSizeCompactSmall = new(80, 24);

        /// <summary>Compact input size token (medium).</summary>
        public static readonly Size InputSizeCompactMedium = new(100, 24);

        /// <summary>Compact input size token (large).</summary>
        public static readonly Size InputSizeCompactLarge = new(150, 24);

        /// <summary>Compact action button size for dense toolbars.</summary>
        public static readonly Size ButtonSizeCompact = new(95, 32);

        /// <summary>Medium action button size for dense toolbars.</summary>
        public static readonly Size ButtonSizeMedium = new(100, 32);

        /// <summary>Wide compact action button size for dense toolbars.</summary>
        public static readonly Size ButtonSizeWideCompact = new(110, 32);

        /// <summary>Small square icon size for compact symbol rendering.</summary>
        public static readonly Size CompactIconSize = new(16, 16);

        /// <summary>Chart marker symbol size token.</summary>
        public static readonly Size ChartMarkerSize = new(8, 8);

        /// <summary>Metric card minimum shell size token.</summary>
        public static readonly Size MetricCardMinimumSize = new(80, 80);

        /// <summary>Preferred metric card width for summary strips.</summary>
        public const int MetricCardWidth = 220;

        /// <summary>Preferred metric card height for compact summary strips.</summary>
        public const int MetricCardHeight = 40;

        /// <summary>Standard splitter width token for nested split containers.</summary>
        public const int SplitterWidth = 13;

        /// <summary>Default fixed tab-item size.</summary>
        public static readonly Size TabItemSize = new(150, 32);

        /// <summary>Tall fixed tab-item size used in high-density navigation hubs.</summary>
        public static readonly Size TabItemSizeTall = new(150, 36);

        /// <summary>Standard action button height.</summary>
        public const int ButtonHeight = 34;

        /// <summary>Standard summary panel height for KPI strips.</summary>
        public const int SummaryPanelHeight = 80;

        /// <summary>Standard text input / combo height.</summary>
        public const int StandardControlHeight = 28;

        /// <summary>Comfortable text input / combo height.</summary>
        public const int StandardControlHeightComfortable = 32;

        /// <summary>Expanded text input / combo height used in toolbars.</summary>
        public const int StandardControlHeightExpanded = 30;

        /// <summary>Toolbar action button height token.</summary>
        public const int ToolbarButtonHeight = 38;

        /// <summary>Tall action/control height token used on entry forms.</summary>
        public const int StandardControlHeightLarge = 40;

        /// <summary>Primary action control height token used for high-emphasis actions.</summary>
        public const int PrimaryActionControlHeight = 42;

        /// <summary>Dialog primary action button height token.</summary>
        public const int DialogButtonHeight = 36;

        /// <summary>Compact control height for secondary progress/status elements.</summary>
        public const int CompactControlHeight = 22;

        /// <summary>Margin between major content blocks.</summary>
        public const int ContentMargin = 16;

        // Typography tokens (logical epx intent, mapped to WinForms point sizes where used).

        /// <summary>Caption text minimum token.</summary>
        public const float FontSizeCaption = 9f;

        /// <summary>Body text minimum token.</summary>
        public const float FontSizeBody = 9f;

        /// <summary>Title text token for panel sections.</summary>
        public const float FontSizeTitle = 10f;

        // Geometry tokens.

        /// <summary>Standard in-page control corner radius token.</summary>
        public const int ControlCornerRadius = 4;

        /// <summary>Standard overlay/flyout corner radius token.</summary>
        public const int OverlayCornerRadius = 8;

        /// <summary>Straight-edge corner radius token.</summary>
        public const int NoCornerRadius = 0;

        // Iconography tokens.

        /// <summary>Small icon footprint token.</summary>
        public const int IconSizeSmall = 16;

        /// <summary>Medium icon footprint token.</summary>
        public const int IconSizeMedium = 20;

        /// <summary>Large icon footprint token.</summary>
        public const int IconSizeLarge = 24;

        // Motion tokens (for rare WinForms transitions; keep minimal and purposeful).

        /// <summary>Fast motion duration token.</summary>
        public const int MotionDurationFastMs = 167;

        /// <summary>Medium motion duration token.</summary>
        public const int MotionDurationMediumMs = 250;

        /// <summary>Slow motion duration token.</summary>
        public const int MotionDurationSlowMs = 333;

        /// <summary>Bare-minimum fade duration token.</summary>
        public const int MotionDurationFadeMs = 83;

        /// <summary>Standard corner radius token for lightweight card surfaces.</summary>
        public const int CornerRadius = ControlCornerRadius;

        /// <summary>Standard single-pixel border thickness token.</summary>
        public const int BorderThickness = 1;
    }
}
