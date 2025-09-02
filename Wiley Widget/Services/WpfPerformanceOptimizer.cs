using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Collections.Generic;
using System.Linq;
using Serilog;

namespace WileyWidget.Services;

/// <summary>
/// WPF Performance Optimization Service
/// Implements best practices for WPF application performance
/// </summary>
public static class WpfPerformanceOptimizer
{
    /// <summary>
    /// Finds all visual children of a specific type in the visual tree
    /// </summary>
    public static IEnumerable<T> FindVisualChildren<T>(this DependencyObject parent) where T : DependencyObject
    {
        if (parent == null) yield break;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
            {
                yield return typedChild;
            }

            foreach (var descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }
    /// <summary>
    /// Enables layout rounding for the entire application to prevent blurry rendering
    /// </summary>
    public static void EnableLayoutRounding(Window window)
    {
        if (window != null)
        {
            window.UseLayoutRounding = true;
            Log.Information("Layout rounding enabled for window: {WindowTitle}", window.Title);
        }
    }

    /// <summary>
    /// Optimizes DataGrid for large datasets
    /// </summary>
    public static void OptimizeDataGrid(DataGrid dataGrid)
    {
        if (dataGrid == null) return;

        // Enable virtualization
        VirtualizingPanel.SetIsVirtualizing(dataGrid, true);
        VirtualizingPanel.SetVirtualizationMode(dataGrid, VirtualizationMode.Recycling);

        // Enable row and column virtualization
        dataGrid.EnableRowVirtualization = true;
        dataGrid.EnableColumnVirtualization = true;

        // Optimize scrolling by setting scroll viewer properties when loaded
        dataGrid.Loaded += (sender, args) =>
        {
            var scrollViewer = dataGrid.FindVisualChildren<ScrollViewer>().FirstOrDefault();
            if (scrollViewer != null)
            {
                scrollViewer.CanContentScroll = true;
            }
        };

        Log.Information("DataGrid performance optimized with virtualization");
    }

    /// <summary>
    /// Freezes Freezable objects to improve performance
    /// </summary>
    public static void FreezeFreezable(Freezable freezable)
    {
        if (freezable != null && !freezable.IsFrozen && freezable.CanFreeze)
        {
            freezable.Freeze();
            Log.Debug("Freezable object frozen for performance optimization");
        }
    }

    /// <summary>
    /// Optimizes UI element for better rendering performance
    /// </summary>
    public static void OptimizeUIElement(UIElement element)
    {
        if (element == null) return;

        // Enable bitmap caching for complex visuals
        if (element.CacheMode == null)
        {
            element.CacheMode = new BitmapCache();
            Log.Debug("Bitmap caching enabled for UI element");
        }

        // Set appropriate rendering options
        RenderOptions.SetEdgeMode(element, EdgeMode.Aliased);
        RenderOptions.SetBitmapScalingMode(element, BitmapScalingMode.NearestNeighbor);
    }

    /// <summary>
    /// Measures layout performance for debugging
    /// </summary>
    public static IDisposable MeasureLayoutPerformance(string elementName)
    {
        var stopwatch = Stopwatch.StartNew();
        Log.Debug("Starting layout performance measurement for: {ElementName}", elementName);

        return new LayoutPerformanceScope(elementName, stopwatch);
    }

    /// <summary>
    /// Defers heavy operations to improve perceived performance
    /// </summary>
    public static void DeferOperation(Action operation, DispatcherPriority priority = DispatcherPriority.Background)
    {
        if (Application.Current?.Dispatcher != null)
        {
            Application.Current.Dispatcher.BeginInvoke(operation, priority);
        }
    }

    /// <summary>
    /// Optimizes application for low-memory scenarios
    /// </summary>
    public static void OptimizeForLowMemory()
    {
        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Log.Information("Memory optimization performed - garbage collection completed");
    }

    /// <summary>
    /// Monitors and logs UI thread blocking
    /// </summary>
    public static void MonitorUIThread()
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        dispatcher.Hooks.OperationPosted += (sender, args) =>
        {
            if (args.Operation.Priority == DispatcherPriority.Send)
            {
                Log.Warning("High-priority operation posted to UI thread - potential blocking");
            }
        };
    }

    private class LayoutPerformanceScope : IDisposable
    {
        private readonly string _elementName;
        private readonly Stopwatch _stopwatch;

        public LayoutPerformanceScope(string elementName, Stopwatch stopwatch)
        {
            _elementName = elementName;
            _stopwatch = stopwatch;
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            var elapsed = _stopwatch.ElapsedMilliseconds;

            if (elapsed > 100) // Log slow layouts
            {
                Log.Warning("Slow layout detected: {ElementName} took {ElapsedMs}ms", _elementName, elapsed);
            }
            else
            {
                Log.Debug("Layout performance: {ElementName} took {ElapsedMs}ms", _elementName, elapsed);
            }
        }
    }
}
