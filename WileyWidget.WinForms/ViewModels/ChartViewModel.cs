using System.Threading.Tasks;

namespace WileyWidget.WinForms.ViewModels
{
    // Charting is disabled in this WinForms build (LiveCharts/Skia removed).
    // Keep a lightweight view model for DI and the UI to show a placeholder.
    public class ChartViewModel
    {
        public string Message => "Charts are currently disabled. Use Syncfusion charts if you need advanced rendering.";

        public Task LoadChartDataAsync() => Task.CompletedTask;
    }
}
