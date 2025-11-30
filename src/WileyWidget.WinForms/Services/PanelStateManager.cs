using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.Windows.Forms.Chart;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Theming;

namespace WileyWidget.WinForms.Services;

/// <summary>
/// Manages per-panel state persistence beyond basic dock layout.
/// Serializes grid view states, chart zoom/pan, combo filters, and splitter positions.
/// </summary>
public class PanelStateManager
{
    private const string StateFileName = "panel_state.json";
    private readonly string _stateFilePath;

    /// <summary>
    /// Root state object containing all panel states.
    /// </summary>
    public PanelStateData StateData { get; private set; } = new();

    public PanelStateManager()
    {
        _stateFilePath = Path.Combine(AppContext.BaseDirectory, StateFileName);
    }

    public PanelStateManager(string stateFilePath)
    {
        _stateFilePath = stateFilePath;
    }

    #region Save/Load Core Methods

    /// <summary>
    /// Saves all panel states to disk. Call from FormClosing.
    /// </summary>
    public void SavePanelState()
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            var json = JsonSerializer.Serialize(StateData, options);
            File.WriteAllText(_stateFilePath, json);
            Log.Debug("PanelStateManager: saved panel state to {Path}", _stateFilePath);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "PanelStateManager: failed to save panel state");
        }
    }

    /// <summary>
    /// Loads all panel states from disk. Call from FormLoad.
    /// </summary>
    public void LoadPanelState()
    {
        try
        {
            if (!File.Exists(_stateFilePath))
            {
                Log.Debug("PanelStateManager: no state file found at {Path}", _stateFilePath);
                StateData = new PanelStateData();
                return;
            }

            var json = File.ReadAllText(_stateFilePath);
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };

            StateData = JsonSerializer.Deserialize<PanelStateData>(json, options) ?? new PanelStateData();
            Log.Debug("PanelStateManager: loaded panel state from {Path}", _stateFilePath);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "PanelStateManager: failed to load panel state");
            StateData = new PanelStateData();
        }
    }

    #endregion

    #region AccountsPanel State

    /// <summary>
    /// Saves AccountsPanel state including grid view state and filter selections.
    /// </summary>
    public void SaveAccountsPanelState(AccountsPanel panel)
    {
        if (panel == null) return;

        try
        {
            StateData.AccountsPanel ??= new AccountsPanelState();

            // Save combo filter selections
            SaveAccountsPanelFilters(panel);

            // Save grid view state
            SaveGridViewState(panel, "AccountsGrid");

            // Save splitter positions if any
            SaveSplitterPositions(panel, StateData.AccountsPanel.SplitterPositions);

            Log.Debug("PanelStateManager: saved AccountsPanel state");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "PanelStateManager: failed to save AccountsPanel state");
        }
    }

    /// <summary>
    /// Restores AccountsPanel state including grid view state and filter selections.
    /// </summary>
    public void LoadAccountsPanelState(AccountsPanel panel)
    {
        if (panel == null || StateData.AccountsPanel == null) return;

        try
        {
            // Restore combo filter selections
            LoadAccountsPanelFilters(panel);

            // Restore grid view state
            LoadGridViewState(panel, "AccountsGrid");

            // Restore splitter positions
            LoadSplitterPositions(panel, StateData.AccountsPanel.SplitterPositions);

            Log.Debug("PanelStateManager: restored AccountsPanel state");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "PanelStateManager: failed to restore AccountsPanel state");
        }
    }

    private void SaveAccountsPanelFilters(AccountsPanel panel)
    {
        try
        {
            // Find combo boxes by name using reflection to access private fields
            var fundCombo = FindControlByName<Syncfusion.WinForms.ListView.SfComboBox>(panel, "comboFund");
            var typeCombo = FindControlByName<Syncfusion.WinForms.ListView.SfComboBox>(panel, "comboAccountType");

            StateData.AccountsPanel!.SelectedFund = fundCombo?.SelectedValue?.ToString();
            StateData.AccountsPanel.SelectedType = typeCombo?.SelectedValue?.ToString();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "PanelStateManager: failed to save AccountsPanel filters");
        }
    }

    private void LoadAccountsPanelFilters(AccountsPanel panel)
    {
        try
        {
            var fundCombo = FindControlByName<Syncfusion.WinForms.ListView.SfComboBox>(panel, "comboFund");
            var typeCombo = FindControlByName<Syncfusion.WinForms.ListView.SfComboBox>(panel, "comboAccountType");

            if (fundCombo != null && !string.IsNullOrEmpty(StateData.AccountsPanel?.SelectedFund))
            {
                fundCombo.SelectedValue = StateData.AccountsPanel.SelectedFund;
            }

            if (typeCombo != null && !string.IsNullOrEmpty(StateData.AccountsPanel?.SelectedType))
            {
                typeCombo.SelectedValue = StateData.AccountsPanel.SelectedType;
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "PanelStateManager: failed to restore AccountsPanel filters");
        }
    }

    #endregion

    #region ChartPanel State

    /// <summary>
    /// Saves ChartPanel state including zoom/pan and filter selections.
    /// </summary>
    public void SaveChartPanelState(ChartPanel panel)
    {
        if (panel == null) return;

        try
        {
            StateData.ChartPanel ??= new ChartPanelState();

            // Save combo filter selections
            SaveChartPanelFilters(panel);

            // Save chart zoom/pan state
            SaveChartZoomState(panel);

            // Save splitter positions if any
            SaveSplitterPositions(panel, StateData.ChartPanel.SplitterPositions);

            Log.Debug("PanelStateManager: saved ChartPanel state");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "PanelStateManager: failed to save ChartPanel state");
        }
    }

    /// <summary>
    /// Restores ChartPanel state including zoom/pan and filter selections.
    /// </summary>
    public void LoadChartPanelState(ChartPanel panel)
    {
        if (panel == null || StateData.ChartPanel == null) return;

        try
        {
            // Restore combo filter selections
            LoadChartPanelFilters(panel);

            // Restore chart zoom/pan state
            LoadChartZoomState(panel);

            // Restore splitter positions
            LoadSplitterPositions(panel, StateData.ChartPanel.SplitterPositions);

            Log.Debug("PanelStateManager: restored ChartPanel state");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "PanelStateManager: failed to restore ChartPanel state");
        }
    }

    private void SaveChartPanelFilters(ChartPanel panel)
    {
        try
        {
            var fundCombo = FindControlByName<Syncfusion.WinForms.ListView.SfComboBox>(panel, "comboFund");
            var typeCombo = FindControlByName<Syncfusion.WinForms.ListView.SfComboBox>(panel, "comboType");
            var deptCombo = FindControlByName<Syncfusion.WinForms.ListView.SfComboBox>(panel, "_comboDepartmentFilter");

            StateData.ChartPanel!.SelectedFund = fundCombo?.SelectedValue?.ToString();
            StateData.ChartPanel.SelectedType = typeCombo?.SelectedValue?.ToString();
            StateData.ChartPanel.SelectedDepartment = deptCombo?.SelectedValue?.ToString();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "PanelStateManager: failed to save ChartPanel filters");
        }
    }

    private void LoadChartPanelFilters(ChartPanel panel)
    {
        try
        {
            var fundCombo = FindControlByName<Syncfusion.WinForms.ListView.SfComboBox>(panel, "comboFund");
            var typeCombo = FindControlByName<Syncfusion.WinForms.ListView.SfComboBox>(panel, "comboType");
            var deptCombo = FindControlByName<Syncfusion.WinForms.ListView.SfComboBox>(panel, "_comboDepartmentFilter");

            if (fundCombo != null && !string.IsNullOrEmpty(StateData.ChartPanel?.SelectedFund))
            {
                fundCombo.SelectedValue = StateData.ChartPanel.SelectedFund;
            }

            if (typeCombo != null && !string.IsNullOrEmpty(StateData.ChartPanel?.SelectedType))
            {
                typeCombo.SelectedValue = StateData.ChartPanel.SelectedType;
            }

            if (deptCombo != null && !string.IsNullOrEmpty(StateData.ChartPanel?.SelectedDepartment))
            {
                deptCombo.SelectedValue = StateData.ChartPanel.SelectedDepartment;
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "PanelStateManager: failed to restore ChartPanel filters");
        }
    }

    private void SaveChartZoomState(ChartPanel panel)
    {
        try
        {
            var chart = FindControlByType<ChartControl>(panel);
            if (chart == null) return;

            StateData.ChartPanel!.ChartZoom ??= new ChartZoomState();

            // Save primary axis visible range
            if (chart.PrimaryXAxis != null)
            {
                StateData.ChartPanel.ChartZoom.XAxisVisibleStart = chart.PrimaryXAxis.VisibleRange.Start;
                StateData.ChartPanel.ChartZoom.XAxisVisibleEnd = chart.PrimaryXAxis.VisibleRange.End;
            }

            if (chart.PrimaryYAxis != null)
            {
                StateData.ChartPanel.ChartZoom.YAxisVisibleStart = chart.PrimaryYAxis.VisibleRange.Start;
                StateData.ChartPanel.ChartZoom.YAxisVisibleEnd = chart.PrimaryYAxis.VisibleRange.End;
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "PanelStateManager: failed to save chart zoom state");
        }
    }

    private void LoadChartZoomState(ChartPanel panel)
    {
        try
        {
            var chart = FindControlByType<ChartControl>(panel);
            if (chart == null || StateData.ChartPanel?.ChartZoom == null) return;

            var zoom = StateData.ChartPanel.ChartZoom;

            // Restore axis visible ranges if they were saved
            if (chart.PrimaryXAxis != null && zoom.XAxisVisibleStart.HasValue && zoom.XAxisVisibleEnd.HasValue)
            {
                chart.PrimaryXAxis.VisibleRange = new Syncfusion.Windows.Forms.Chart.MinMaxInfo(
                    zoom.XAxisVisibleStart.Value, zoom.XAxisVisibleEnd.Value, 1);
            }

            if (chart.PrimaryYAxis != null && zoom.YAxisVisibleStart.HasValue && zoom.YAxisVisibleEnd.HasValue)
            {
                chart.PrimaryYAxis.VisibleRange = new Syncfusion.Windows.Forms.Chart.MinMaxInfo(
                    zoom.YAxisVisibleStart.Value, zoom.YAxisVisibleEnd.Value, 1);
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "PanelStateManager: failed to restore chart zoom state");
        }
    }

    #endregion

    #region DashboardPanel State

    /// <summary>
    /// Saves DashboardPanel state including details grid view state.
    /// </summary>
    public void SaveDashboardPanelState(DashboardPanel panel)
    {
        if (panel == null) return;

        try
        {
            StateData.DashboardPanel ??= new DashboardPanelState();

            // Save details grid view state
            SaveGridViewState(panel, "DashboardDetailsGrid");

            // Save splitter positions
            SaveSplitterPositions(panel, StateData.DashboardPanel.SplitterPositions);

            Log.Debug("PanelStateManager: saved DashboardPanel state");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "PanelStateManager: failed to save DashboardPanel state");
        }
    }

    /// <summary>
    /// Restores DashboardPanel state including details grid view state.
    /// </summary>
    public void LoadDashboardPanelState(DashboardPanel panel)
    {
        if (panel == null || StateData.DashboardPanel == null) return;

        try
        {
            // Restore details grid view state
            LoadGridViewState(panel, "DashboardDetailsGrid");

            // Restore splitter positions
            LoadSplitterPositions(panel, StateData.DashboardPanel.SplitterPositions);

            Log.Debug("PanelStateManager: restored DashboardPanel state");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "PanelStateManager: failed to restore DashboardPanel state");
        }
    }

    #endregion

    #region Grid View State Helpers

    private void SaveGridViewState(System.Windows.Forms.Control panel, string gridKey)
    {
        try
        {
            var grid = FindControlByType<SfDataGrid>(panel);
            if (grid?.View == null) return;

            var state = grid.View.GetState();
            if (state == null) return;

            // Convert state to serializable format
            var gridState = new GridViewState
            {
                GroupedColumns = state.GroupDescriptors?.Select(g => g.PropertyName).ToList(),
                SortedColumns = state.SortDescriptors?.Select(s => new SortColumnState
                {
                    PropertyName = s.PropertyName,
                    Direction = s.Direction.ToString()
                }).ToList(),
                FilterPredicates = state.FilterDescriptors?.Select(f => new FilterPredicateState
                {
                    PropertyName = f.PropertyName,
                    FilterText = f.FilterValue?.ToString()
                }).ToList()
            };

            // Store column widths
            gridState.ColumnWidths = new Dictionary<string, double>();
            foreach (var col in grid.Columns)
            {
                if (!string.IsNullOrEmpty(col.MappingName))
                {
                    gridState.ColumnWidths[col.MappingName] = col.Width;
                }
            }

            StateData.GridStates ??= new Dictionary<string, GridViewState>();
            StateData.GridStates[gridKey] = gridState;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "PanelStateManager: failed to save grid view state for {Key}", gridKey);
        }
    }

    private void LoadGridViewState(System.Windows.Forms.Control panel, string gridKey)
    {
        try
        {
            if (StateData.GridStates == null ||
                !StateData.GridStates.TryGetValue(gridKey, out var gridState) ||
                gridState == null)
            {
                return;
            }

            var grid = FindControlByType<SfDataGrid>(panel);
            if (grid?.View == null) return;

            // Restore column widths
            if (gridState.ColumnWidths != null)
            {
                foreach (var col in grid.Columns)
                {
                    if (!string.IsNullOrEmpty(col.MappingName) &&
                        gridState.ColumnWidths.TryGetValue(col.MappingName, out var width))
                    {
                        col.Width = width;
                    }
                }
            }

            // Restore grouping
            if (gridState.GroupedColumns != null)
            {
                grid.GroupColumnDescriptions.Clear();
                foreach (var colName in gridState.GroupedColumns)
                {
                    grid.GroupColumnDescriptions.Add(new Syncfusion.WinForms.DataGrid.GroupColumnDescription { ColumnName = colName });
                }
            }

            // Restore sorting
            if (gridState.SortedColumns != null)
            {
                grid.SortColumnDescriptions.Clear();
                foreach (var sort in gridState.SortedColumns)
                {
                    var direction = Enum.TryParse<System.ComponentModel.ListSortDirection>(sort.Direction, out var d)
                        ? d : System.ComponentModel.ListSortDirection.Ascending;
                    grid.SortColumnDescriptions.Add(new Syncfusion.WinForms.DataGrid.SortColumnDescription
                    {
                        ColumnName = sort.PropertyName,
                        SortDirection = direction
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "PanelStateManager: failed to restore grid view state for {Key}", gridKey);
        }
    }

    #endregion

    #region Splitter Position Helpers

    private void SaveSplitterPositions(System.Windows.Forms.Control panel, Dictionary<string, int>? positions)
    {
        try
        {
            positions ??= new Dictionary<string, int>();

            var splitters = FindAllControlsByType<System.Windows.Forms.SplitContainer>(panel);
            foreach (var splitter in splitters)
            {
                var key = !string.IsNullOrEmpty(splitter.Name) ? splitter.Name : $"splitter_{splitter.GetHashCode()}";
                positions[key] = splitter.SplitterDistance;
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "PanelStateManager: failed to save splitter positions");
        }
    }

    private void LoadSplitterPositions(System.Windows.Forms.Control panel, Dictionary<string, int>? positions)
    {
        if (positions == null || positions.Count == 0) return;

        try
        {
            var splitters = FindAllControlsByType<System.Windows.Forms.SplitContainer>(panel);
            foreach (var splitter in splitters)
            {
                var key = !string.IsNullOrEmpty(splitter.Name) ? splitter.Name : $"splitter_{splitter.GetHashCode()}";
                if (positions.TryGetValue(key, out var distance))
                {
                    // Ensure distance is within valid bounds
                    if (distance >= splitter.Panel1MinSize &&
                        distance <= splitter.Width - splitter.Panel2MinSize)
                    {
                        splitter.SplitterDistance = distance;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "PanelStateManager: failed to restore splitter positions");
        }
    }

    #endregion

    #region Control Finding Helpers

    private T? FindControlByName<T>(System.Windows.Forms.Control parent, string name) where T : System.Windows.Forms.Control
    {
        if (parent == null) return null;

        foreach (System.Windows.Forms.Control ctrl in parent.Controls)
        {
            if (ctrl is T t && ctrl.Name == name)
                return t;

            var found = FindControlByName<T>(ctrl, name);
            if (found != null) return found;
        }

        // Also check private fields via reflection
        var field = parent.GetType().GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field?.GetValue(parent) is T fieldValue)
            return fieldValue;

        return null;
    }

    private T? FindControlByType<T>(System.Windows.Forms.Control parent) where T : System.Windows.Forms.Control
    {
        if (parent == null) return null;

        foreach (System.Windows.Forms.Control ctrl in parent.Controls)
        {
            if (ctrl is T t)
                return t;

            var found = FindControlByType<T>(ctrl);
            if (found != null) return found;
        }

        return null;
    }

    private List<T> FindAllControlsByType<T>(System.Windows.Forms.Control parent) where T : System.Windows.Forms.Control
    {
        var results = new List<T>();
        if (parent == null) return results;

        foreach (System.Windows.Forms.Control ctrl in parent.Controls)
        {
            if (ctrl is T t)
                results.Add(t);

            results.AddRange(FindAllControlsByType<T>(ctrl));
        }

        return results;
    }

    #endregion
}

#region State Data Models

/// <summary>
/// Root object for all panel state data.
/// </summary>
public class PanelStateData
{
    public AccountsPanelState? AccountsPanel { get; set; }
    public ChartPanelState? ChartPanel { get; set; }
    public DashboardPanelState? DashboardPanel { get; set; }
    public Dictionary<string, GridViewState>? GridStates { get; set; }
}

/// <summary>
/// AccountsPanel-specific state.
/// </summary>
public class AccountsPanelState
{
    public string? SelectedFund { get; set; }
    public string? SelectedType { get; set; }
    public string? SelectedDepartment { get; set; }
    public Dictionary<string, int>? SplitterPositions { get; set; }
}

/// <summary>
/// ChartPanel-specific state.
/// </summary>
public class ChartPanelState
{
    public string? SelectedFund { get; set; }
    public string? SelectedType { get; set; }
    public string? SelectedDepartment { get; set; }
    public ChartZoomState? ChartZoom { get; set; }
    public Dictionary<string, int>? SplitterPositions { get; set; }
}

/// <summary>
/// DashboardPanel-specific state.
/// </summary>
public class DashboardPanelState
{
    public Dictionary<string, int>? SplitterPositions { get; set; }
}

/// <summary>
/// Chart zoom/pan state.
/// </summary>
public class ChartZoomState
{
    public double? XAxisVisibleStart { get; set; }
    public double? XAxisVisibleEnd { get; set; }
    public double? YAxisVisibleStart { get; set; }
    public double? YAxisVisibleEnd { get; set; }
}

/// <summary>
/// SfDataGrid view state.
/// </summary>
public class GridViewState
{
    public List<string>? GroupedColumns { get; set; }
    public List<SortColumnState>? SortedColumns { get; set; }
    public List<FilterPredicateState>? FilterPredicates { get; set; }
    public Dictionary<string, double>? ColumnWidths { get; set; }
}

/// <summary>
/// Sort column state for serialization.
/// </summary>
public class SortColumnState
{
    public string PropertyName { get; set; } = string.Empty;
    public string Direction { get; set; } = "Ascending";
}

/// <summary>
/// Filter predicate state for serialization.
/// </summary>
public class FilterPredicateState
{
    public string PropertyName { get; set; } = string.Empty;
    public string? FilterText { get; set; }
}

#endregion
