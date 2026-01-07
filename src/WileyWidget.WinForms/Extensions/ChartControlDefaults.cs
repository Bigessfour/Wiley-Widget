using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using Syncfusion.Drawing;
using Syncfusion.Windows.Forms.Chart;

namespace WileyWidget.WinForms.Extensions
{
    /// <summary>
    /// Centralized, version-tolerant defaults for Syncfusion classic <see cref="ChartControl"/>.
    /// Keep this focused on non-domain, non-theme settings so charts remain visually consistent
    /// while still relying on SfSkinManager for theming.
    /// </summary>
    internal static class ChartControlDefaults
    {
        internal sealed class Options
        {
            public SmoothingMode SmoothingMode { get; init; } = SmoothingMode.AntiAlias;
            public int ElementsSpacing { get; init; } = 5;
            public bool ShowToolTips { get; init; } = true;
            public bool TransparentChartArea { get; init; } = false;
            public bool EnableZooming { get; init; } = true;
            public bool EnableAxisScrollBar { get; init; } = true;
        }

        public static void Apply(ChartControl chart, Options? options = null)
        {
            if (chart == null) throw new ArgumentNullException(nameof(chart));

            var opt = options ?? new Options();

            try { chart.SmoothingMode = opt.SmoothingMode; } catch { }
            try { chart.ElementsSpacing = opt.ElementsSpacing; } catch { }
            try { chart.BorderAppearance.SkinStyle = ChartBorderSkinStyle.None; } catch { }
            try { chart.ShowToolTips = opt.ShowToolTips; } catch { }

            if (opt.TransparentChartArea)
            {
                try { chart.ChartArea.BackInterior = new BrushInfo(Color.Transparent); } catch { }
            }

            if (opt.EnableAxisScrollBar)
            {
                TrySetAxisScrollBar(chart.PrimaryXAxis, enabled: true);
            }

            if (opt.EnableZooming)
            {
                TryEnableZooming(chart, enabled: true);
            }
        }

        public static void TryEnableZooming(ChartControl chart, bool enabled)
        {
            if (chart == null) return;

            try
            {
                var chartType = chart.GetType();

                var propEnableZooming = chartType.GetProperty("EnableZooming");
                if (propEnableZooming != null && propEnableZooming.CanWrite)
                {
                    propEnableZooming.SetValue(chart, enabled);
                }

                var propEnableMouseWheelZooming = chartType.GetProperty("EnableMouseWheelZooming");
                if (propEnableMouseWheelZooming != null && propEnableMouseWheelZooming.CanWrite)
                {
                    propEnableMouseWheelZooming.SetValue(chart, enabled);
                }
            }
            catch
            {
                // Ignore: optional properties differ by Syncfusion build.
            }
        }

        public static void TrySetAxisScrollBar(object? axis, bool enabled)
        {
            if (axis == null) return;

            try
            {
                // Syncfusion builds differ: some use EnableScrollBar, others EnableAxisScrollBar.
                var axisType = axis.GetType();

                var propEnableScrollBar = axisType.GetProperty("EnableScrollBar");
                if (propEnableScrollBar != null && propEnableScrollBar.CanWrite)
                {
                    propEnableScrollBar.SetValue(axis, enabled);
                }

                var propEnableAxisScrollBar = axisType.GetProperty("EnableAxisScrollBar");
                if (propEnableAxisScrollBar != null && propEnableAxisScrollBar.CanWrite)
                {
                    propEnableAxisScrollBar.SetValue(axis, enabled);
                }
            }
            catch
            {
                // Ignore: optional properties differ by Syncfusion build.
            }
        }
    }
}
