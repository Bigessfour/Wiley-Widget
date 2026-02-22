using System;
using System.Reflection;
using Syncfusion.Windows.Forms.Chart;

namespace WileyWidget.WinForms.Extensions
{
    /// <summary>
    /// Centralized wiring/unwiring for Syncfusion classic ChartControl ChartRegion events.
    /// Subscriptions are performed via reflection to avoid a compile-time dependency on
    /// ChartRegionMouseEventHandler/ChartRegionMouseEventArgs, which reside in the local
    /// Syncfusion.Chart.Windows DLL whose internal CLR version may conflict with the NuGet
    /// Syncfusion.Shared.Base package. All handlers are intentional no-ops; the wiring
    /// ensures region interaction is consistently available for future customisation.
    /// </summary>
    internal sealed class ChartControlRegionEventWiring : IDisposable
    {
        private readonly ChartControl _chart;
        private readonly Delegate? _mouseRegionHandler;
        private readonly Delegate? _chartRegionClickHandler;
        private readonly Delegate? _chartRegionDoubleClickHandler;
        private bool _disposed;

        private static readonly string[] _mouseRegionEvents = new[]
        {
            "ChartRegionMouseDown", "ChartRegionMouseUp", "ChartRegionMouseMove",
            "ChartRegionMouseHover", "ChartRegionMouseEnter", "ChartRegionMouseLeave"
        };

        public ChartControlRegionEventWiring(ChartControl chart)
        {
            _chart = chart ?? throw new ArgumentNullException(nameof(chart));

            _mouseRegionHandler  = TryCreateNoOpHandler(_chart, "ChartRegionMouseDown");
            _chartRegionClickHandler       = TryCreateNoOpHandler(_chart, "ChartRegionClick");
            _chartRegionDoubleClickHandler = TryCreateNoOpHandler(_chart, "ChartRegionDoubleClick");

            foreach (var eventName in _mouseRegionEvents)
                TryAddEventHandler(_chart, eventName, _mouseRegionHandler);

            if (_chartRegionClickHandler != null)
                TryAddEventHandler(_chart, "ChartRegionClick", _chartRegionClickHandler);

            if (_chartRegionDoubleClickHandler != null)
                TryAddEventHandler(_chart, "ChartRegionDoubleClick", _chartRegionDoubleClickHandler);
        }

        /// <summary>Creates a no-op delegate matching the event's handler type via reflection.</summary>
        private static Delegate? TryCreateNoOpHandler(ChartControl chart, string eventName)
        {
            try
            {
                var evt = chart.GetType().GetEvent(eventName, BindingFlags.Instance | BindingFlags.Public);
                var handlerType = evt?.EventHandlerType;
                if (handlerType == null) return null;

                var method = typeof(ChartControlRegionEventWiring).GetMethod(
                    nameof(OnNoOpEvent),
                    BindingFlags.Static | BindingFlags.NonPublic);

                if (method == null) return null;
                return Delegate.CreateDelegate(handlerType, method, throwOnBindFailure: false);
            }
            catch { return null; }
        }

        // No-op handler with the most permissive signature (object, object).
        // Delegate.CreateDelegate binds this only if the event's delegate matches.
        private static void OnNoOpEvent(object sender, EventArgs e) { }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (var eventName in _mouseRegionEvents)
                TryRemoveEventHandler(_chart, eventName, _mouseRegionHandler);

            if (_chartRegionClickHandler != null)
                TryRemoveEventHandler(_chart, "ChartRegionClick", _chartRegionClickHandler);

            if (_chartRegionDoubleClickHandler != null)
                TryRemoveEventHandler(_chart, "ChartRegionDoubleClick", _chartRegionDoubleClickHandler);
        }

        private static void TryAddEventHandler(ChartControl chart, string eventName, Delegate? handler)
        {
            if (handler == null) return;
            try
            {
                var evt = chart.GetType().GetEvent(eventName, BindingFlags.Instance | BindingFlags.Public);
                evt?.AddEventHandler(chart, handler);
            }
            catch { }
        }

        private static void TryRemoveEventHandler(ChartControl chart, string eventName, Delegate? handler)
        {
            if (handler == null) return;
            try
            {
                var evt = chart.GetType().GetEvent(eventName, BindingFlags.Instance | BindingFlags.Public);
                evt?.RemoveEventHandler(chart, handler);
            }
            catch { }
        }
    }
}
