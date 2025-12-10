using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.ListView;
using Syncfusion.WinForms.Input;

namespace WileyWidget.WinForms.Extensions
{
    /// <summary>
    /// Safe dispose helpers to avoid exceptions in shutdown code paths.
    /// </summary>
    public static class ControlSafeExtensions
    {
        public static void SafeClearDataSource(this SfDataGrid? grid)
        {
            if (grid == null) return;
            try { grid.DataSource = null; } catch { }
        }

        public static void SafeDispose(this SfDataGrid? grid)
        {
            try { grid?.Dispose(); } catch { }
        }

        public static void SafeClearDataSource(this SfComboBox? combo)
        {
            if (combo == null) return;
            try { combo.DataSource = null; } catch { }
        }

        public static void SafeDispose(this SfComboBox? combo)
        {
            try { combo?.Dispose(); } catch { }
        }

        public static void SafeClearDataSource(this SfListView? list)
        {
            if (list == null) return;
            try { list.DataSource = null; } catch { }
        }

        public static void SafeDispose(this SfListView? list)
        {
            try { list?.Dispose(); } catch { }
        }
    }
}
