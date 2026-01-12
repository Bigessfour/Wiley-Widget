using System;
using System.Reflection;
using Syncfusion.Windows.Forms.Chart;

namespace WileyWidget.WinForms.Extensions
{
    /// <summary>
    /// Centralized wiring/unwiring for Syncfusion classic ChartControl ChartRegion events.
    /// Uses strongly-typed mouse-region events and reflection for click/double-click to avoid
    /// hard dependency on event availability/signature across Syncfusion builds.
    /// </summary>
    internal sealed class ChartControlRegionEventWiring : IDisposable
    {
        private readonly ChartControl _chart;
        private readonly ChartRegionMouseEventHandler _mouseRegionHandler;
        private readonly Delegate? _chartRegionClickHandler;
        private readonly Delegate? _chartRegionDoubleClickHandler;
        private bool _disposed;

        public ChartControlRegionEventWiring(ChartControl chart)
        {
            _chart = chart ?? throw new ArgumentNullException(nameof(chart));

            _mouseRegionHandler = OnChartRegionMouseEvent;

            // Documented chart region mouse events
            _chart.ChartRegionMouseDown += _mouseRegionHandler;
            _chart.ChartRegionMouseUp += _mouseRegionHandler;
            _chart.ChartRegionMouseMove += _mouseRegionHandler;
            _chart.ChartRegionMouseHover += _mouseRegionHandler;
            _chart.ChartRegionMouseEnter += _mouseRegionHandler;
            _chart.ChartRegionMouseLeave += _mouseRegionHandler;

            // Optional chart region click events (varies by Syncfusion build/signature)
            _chartRegionClickHandler = TryCreateChartRegionHandler(_chart, "ChartRegionClick");
            if (_chartRegionClickHandler != null)
            {
                TryAddEventHandler(_chart, "ChartRegionClick", _chartRegionClickHandler);
            }

            _chartRegionDoubleClickHandler = TryCreateChartRegionHandler(_chart, "ChartRegionDoubleClick");
            if (_chartRegionDoubleClickHandler != null)
            {
                TryAddEventHandler(_chart, "ChartRegionDoubleClick", _chartRegionDoubleClickHandler);
            }
        }

        private static void OnChartRegionMouseEvent(object sender, ChartRegionMouseEventArgs e)
        {
            // Intentionally no-op: wiring is required so region interaction is available consistently.
            // Panels can add additional behavior later without needing per-surface event discovery.
            _ = sender;
            _ = e;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try { _chart.ChartRegionMouseDown -= _mouseRegionHandler; } catch { }
            try { _chart.ChartRegionMouseUp -= _mouseRegionHandler; } catch { }
            try { _chart.ChartRegionMouseMove -= _mouseRegionHandler; } catch { }
            try { _chart.ChartRegionMouseHover -= _mouseRegionHandler; } catch { }
            try { _chart.ChartRegionMouseEnter -= _mouseRegionHandler; } catch { }
            try { _chart.ChartRegionMouseLeave -= _mouseRegionHandler; } catch { }

            if (_chartRegionClickHandler != null)
            {
                TryRemoveEventHandler(_chart, "ChartRegionClick", _chartRegionClickHandler);
            }

            if (_chartRegionDoubleClickHandler != null)
            {
                TryRemoveEventHandler(_chart, "ChartRegionDoubleClick", _chartRegionDoubleClickHandler);
            }
        }

        private static Delegate? TryCreateChartRegionHandler(ChartControl chart, string eventName)
        {
            try
            {
                var evt = chart.GetType().GetEvent(eventName, BindingFlags.Instance | BindingFlags.Public);
                var handlerType = evt?.EventHandlerType;
                if (handlerType == null) return null;

                var method = typeof(ChartControlRegionEventWiring).GetMethod(
                    nameof(OnChartRegionMouseEvent),
                    BindingFlags.Static | BindingFlags.NonPublic);

                if (method == null) return null;

                return Delegate.CreateDelegate(handlerType, method, throwOnBindFailure: false);
            }
            catch
            {
                return null;
            }
        }

        private static void TryAddEventHandler(ChartControl chart, string eventName, Delegate handler)
        {
            try
            {
                var evt = chart.GetType().GetEvent(eventName, BindingFlags.Instance | BindingFlags.Public);
                evt?.AddEventHandler(chart, handler);
            }
            catch
            {
                // Ignore: optional event or incompatible signature.
            }
        }

        private static void TryRemoveEventHandler(ChartControl chart, string eventName, Delegate handler)
        {
            try
            {
                var evt = chart.GetType().GetEvent(eventName, BindingFlags.Instance | BindingFlags.Public);
                evt?.RemoveEventHandler(chart, handler);
            }
            catch
            {
                // Ignore: best-effort cleanup.
            }
        }
    }
}
