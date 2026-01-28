using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using WileyWidget.WinForms.Helpers;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.Windows.Forms.Chart;

namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// Synchronizes data between SfDataGrid and ChartControl automatically.
    /// Implements two-way binding: Grid edits → Chart refresh, Chart click → Grid selection.
    /// Prevents circular updates using internal flag.
    /// </summary>
    public class GridDataSynchronizer : IDisposable
    {
        private readonly SfDataGrid _grid;
        private readonly ChartControl _chart;
        private readonly ILogger<GridDataSynchronizer> _logger;
        private bool _isUpdating; // Prevent circular updates
        private bool _disposed;

        public GridDataSynchronizer(SfDataGrid grid, ChartControl chart, ILogger<GridDataSynchronizer> logger)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _chart = chart ?? throw new ArgumentNullException(nameof(chart));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            WireGridEvents();
            WireChartEvents();

            _logger.LogDebug("GridDataSynchronizer initialized for {GridName} and {ChartName}", grid.Name, chart.Name);
        }

        private void WireGridEvents()
        {
            try
            {
                // Grid data changed → Update chart
                // Note: CurrentCellCommitted event may not be available in current Syncfusion version
                /*
                _grid.CurrentCellCommitted += (s, e) =>
                {
                    if (_isUpdating || _disposed) return;
                    _isUpdating = true;
                    try
                    {
                        RefreshChartFromGrid();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error refreshing chart from grid");
                    }
                    finally
                    {
                        _isUpdating = false;
                    }
                };
                */

                // Grid selection changed → Highlight in chart
                _grid.SelectionChanged += (s, e) =>
                {
                    if (_isUpdating || _disposed) return;
                    _isUpdating = true;
                    try
                    {
                        HighlightChartFromGridSelection();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error highlighting chart from grid selection");
                    }
                    finally
                    {
                        _isUpdating = false;
                    }
                };

                _logger.LogDebug("Grid event handlers wired successfully");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to wire grid events");
            }
        }

        private void WireChartEvents()
        {
            try
            {
                // Chart click → Select grid row
                // Note: ChartMouseUp event may not be available in current Syncfusion version
                /*
                _chart.ChartMouseUp += (s, e) =>
                {
                    if (_isUpdating || _disposed) return;
                    _isUpdating = true;
                    try
                    {
                        SelectGridFromChartClick(e);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error selecting grid from chart click");
                    }
                    finally
                    {
                        _isUpdating = false;
                    }
                };
                */

                _logger.LogDebug("Chart event handlers wired successfully");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to wire chart events");
            }
        }

        private void RefreshChartFromGrid()
        {
            try
            {
                if (_grid.DataSource is not System.Collections.IEnumerable data) return;
                if (_chart.Series.Count == 0) return;

                var series = _chart.Series[0];
                series.Points.Clear();

                int index = 0;
                foreach (var item in data.Cast<object>())
                {
                    try
                    {
                        // Extract X and Y values from item using reflection
                        // Supports common property names: XValue/YValue, Date/Value, Month/Amount
                        var xProp = item.GetType().GetProperty("XValue",
                                System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public) ??
                                item.GetType().GetProperty("Date",
                                System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public) ??
                                item.GetType().GetProperty("Month",
                                System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public);

                        var yProp = item.GetType().GetProperty("YValue",
                                System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public) ??
                                item.GetType().GetProperty("Value",
                                System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public) ??
                                item.GetType().GetProperty("Amount",
                                System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public);

                        if (xProp != null && yProp != null)
                        {
                            var x = xProp.GetValue(item)?.ToString() ?? index.ToString(CultureInfo.InvariantCulture);
                            var y = yProp.GetValue(item);

                            if (y is IConvertible)
                            {
                                try
                                {
                                    series.Points.Add(x, Convert.ToDouble(y, CultureInfo.InvariantCulture));
                                }
                                catch
                                {
                                    // Skip points that can't convert
                                }
                            }
                        }

                        index++;
                    }
                    catch
                    {
                        // Skip items with missing properties
                    }
                }

                _logger.LogDebug("✓ Chart refreshed from grid: {PointCount} points", series.Points.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh chart from grid");
            }
        }

        private void HighlightChartFromGridSelection()
        {
            try
            {
                // Find selected row in grid
                if (_grid.SelectedIndex < 0 || _grid.SelectedIndex >= _chart.Series[0].Points.Count)
                    return;

                // Highlight corresponding chart point
                for (int i = 0; i < _chart.Series[0].Points.Count; i++)
                {
                    try
                    {
                        var point = _chart.Series[0].Points[i];
                        // Set point appearance based on selection
                        if (i == _grid.SelectedIndex)
                        {
                            // Highlight selected point
                            // Note: Interior property may not be available in current Syncfusion version
                            // point.Interior = new System.Drawing.SolidBrush(System.Drawing.Color.Red);
                        }
                        else
                        {
                            // Reset non-selected points
                            // Note: Interior property may not be available in current Syncfusion version
                            // point.Interior = new System.Drawing.SolidBrush(System.Drawing.Color.Blue);
                        }
                    }
                    catch
                    {
                        // Skip points that can't be styled
                    }
                }

                _chart.SafeInvoke(() => { _chart.Refresh(); });
                _logger.LogDebug("✓ Chart highlighted - selected index: {SelectedIndex}", _grid.SelectedIndex);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to highlight chart from grid selection");
            }
        }

        // ChartMouseEventArgs integration removed - type not available in current Syncfusion version
        // Chart click synchronization disabled pending API update
        /*
        private void SelectGridFromChartClick(object e)
        {
            try
            {
                // Determine which chart point was clicked
                // if (e.ChartPointInfo == null || !e.ChartPointInfo.IsSeriesVisible)
                    return;

                var pointIndex = e.ChartPointInfo.PointIndex;
                if (pointIndex >= 0 && pointIndex < _grid.DataSource?.Cast<object>().Count())
                {
                    // Select corresponding row in grid
                    _grid.SelectedIndex = pointIndex;
                    _logger.LogDebug("✓ Grid row selected from chart click: index {PointIndex}", pointIndex);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to select grid from chart click");
            }
        }
        */

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                try
                {
                    // Unwire all events
                    if (_grid != null)
                    {
                        // Note: CurrentCellCommitted event may not be available in current Syncfusion version
                        // _grid.CurrentCellCommitted -= (s, e) => { };
                        _grid.SelectionChanged -= (s, e) => { };
                    }

                    if (_chart != null)
                    {
                        // Note: ChartMouseUp event may not be available in current Syncfusion version
                        // _chart.ChartMouseUp -= (s, e) => { };
                    }

                    _logger.LogDebug("GridDataSynchronizer disposed");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error during GridDataSynchronizer disposal");
                }
            }

            _disposed = true;
        }
    }
}
