using Syncfusion.Windows.Forms;

namespace WileyWidget.WinForms.Theming
{
    /// <summary>
    /// Layout constants for chart-related controls. Keep values logical (device-independent)
    /// and expose DPI-aware device pixel conversions where needed.
    /// </summary>
    internal static class ChartLayoutConstants
    {
        // Logical (96-DPI) width for pie/summary panel
        public const float PiePanelMinWidthLogical = 240f;

        // Device-adjusted pixel width (DPI-aware)
        public static int PiePanelMinWidth => (int)DpiAware.LogicalToDeviceUnits(PiePanelMinWidthLogical);
    }
}
