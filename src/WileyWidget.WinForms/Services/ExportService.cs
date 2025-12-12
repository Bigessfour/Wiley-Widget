using System.IO;
using System.Threading.Tasks;
using Syncfusion.WinForms.DataGrid;

namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// Placeholder export service for Syncfusion grids.
    /// </summary>
    public static class ExportService
    {
        public static Task ExportGridToExcelAsync(SfDataGrid grid, string filePath)
        {
            // Stub implementation writes a placeholder file to satisfy build.
            _ = grid;
            File.WriteAllText(filePath, "Export not yet implemented.");
            return Task.CompletedTask;
        }

        public static Task ExportGridToPdfAsync(SfDataGrid grid, string filePath)
        {
            _ = grid;
            File.WriteAllText(filePath, "Export not yet implemented.");
            return Task.CompletedTask;
        }

        public static Task ExportChartToPdfAsync(object chart, string filePath)
        {
            _ = chart;
            File.WriteAllText(filePath, "Chart export not yet implemented.");
            return Task.CompletedTask;
        }
    }
}
