using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using Microsoft.Extensions.Logging;
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

        public static void Apply(ChartControl chart, Options? options = null, ILogger? logger = null)
        {
            if (chart == null) throw new ArgumentNullException(nameof(chart));
            if (chart.IsDisposed)
            {
                logger?.LogWarning("ChartControlDefaults.Apply: Chart is disposed, skipping initialization");
                return;
            }

            var opt = options ?? new Options();
            logger?.LogDebug("Applying ChartControl defaults with SmoothingMode={SmoothingMode}, Spacing={Spacing}",
                opt.SmoothingMode, opt.ElementsSpacing);

            try { chart.SmoothingMode = opt.SmoothingMode; }
            catch (Exception ex) { logger?.LogDebug(ex, "Failed to set SmoothingMode"); }

            try { chart.ElementsSpacing = opt.ElementsSpacing; }
            catch (Exception ex) { logger?.LogDebug(ex, "Failed to set ElementsSpacing"); }

            try { chart.BorderAppearance.SkinStyle = ChartBorderSkinStyle.None; }
            catch (Exception ex) { logger?.LogDebug(ex, "Failed to set BorderAppearance"); }

            try { chart.ShowToolTips = opt.ShowToolTips; }
            catch (Exception ex) { logger?.LogDebug(ex, "Failed to set ShowToolTips"); }

            if (opt.TransparentChartArea)
            {
                try { chart.ChartArea.BackInterior = new BrushInfo(Color.Transparent); }
                catch (Exception ex) { logger?.LogDebug(ex, "Failed to set transparent chart area"); }
            }

            if (opt.EnableAxisScrollBar)
            {
                TrySetAxisScrollBar(chart.PrimaryXAxis, enabled: true, logger: logger);
            }

            if (opt.EnableZooming)
            {
                TryEnableZooming(chart, enabled: true, logger: logger);
            }
        }

        public static void TryEnableZooming(ChartControl chart, bool enabled, ILogger? logger = null)
        {
            if (chart == null) return;

            try
            {
                var chartType = chart.GetType();
                var enabledCount = 0;

                var propEnableZooming = chartType.GetProperty("EnableZooming");
                if (propEnableZooming != null && propEnableZooming.CanWrite)
                {
                    propEnableZooming.SetValue(chart, enabled);
                    enabledCount++;
                }

                var propEnableMouseWheelZooming = chartType.GetProperty("EnableMouseWheelZooming");
                if (propEnableMouseWheelZooming != null && propEnableMouseWheelZooming.CanWrite)
                {
                    propEnableMouseWheelZooming.SetValue(chart, enabled);
                    enabledCount++;
                }

                logger?.LogDebug("Zoom enabled on {EnabledCount} properties", enabledCount);
            }
            catch (Exception ex)
            {
                logger?.LogDebug(ex, "TryEnableZooming: Failed (optional Syncfusion feature may not be available)");
            }
        }

        public static void TrySetAxisScrollBar(object? axis, bool enabled, ILogger? logger = null)
        {
            if (axis == null) return;

            try
            {
                // Syncfusion builds differ: some use EnableScrollBar, others EnableAxisScrollBar.
                var axisType = axis.GetType();
                var enabledCount = 0;

                var propEnableScrollBar = axisType.GetProperty("EnableScrollBar");
                if (propEnableScrollBar != null && propEnableScrollBar.CanWrite)
                {
                    propEnableScrollBar.SetValue(axis, enabled);
                    enabledCount++;
                }

                var propEnableAxisScrollBar = axisType.GetProperty("EnableAxisScrollBar");
                if (propEnableAxisScrollBar != null && propEnableAxisScrollBar.CanWrite)
                {
                    propEnableAxisScrollBar.SetValue(axis, enabled);
                    enabledCount++;
                }

                logger?.LogDebug("Axis scroll bar enabled on {EnabledCount} properties", enabledCount);
            }
            catch (Exception ex)
            {
                logger?.LogDebug(ex, "TrySetAxisScrollBar: Failed (optional Syncfusion feature may not be available)");
            }
        }
    }
}
