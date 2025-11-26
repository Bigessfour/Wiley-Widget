using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace WileyWidget.WinForms.ViewModels
{
    public class ChartViewModel
    {
        public ISeries[] ChartSeries { get; set; } = Array.Empty<ISeries>();
        public Axis[] XAxes { get; set; } = Array.Empty<Axis>();
        public Axis[] YAxes { get; set; } = Array.Empty<Axis>();
        public ISeries[] PieChartSeries { get; set; } = Array.Empty<ISeries>();

        public async Task LoadChartDataAsync()
        {
            // Load your chart data here
            await Task.Delay(100); // Simulate loading

            ChartSeries = new ISeries[]
            {
                new LineSeries<double>
                {
                    Values = new double[] { 2, 1, 3, 5, 3, 4, 6 },
                    Fill = null
                }
            };

            XAxes = new Axis[]
            {
                new Axis
                {
                    Labels = new string[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul" }
                }
            };

            PieChartSeries = new ISeries[]
            {
                new PieSeries<double> { Values = new double[] { 2 }, Name = "Category 1" },
                new PieSeries<double> { Values = new double[] { 4 }, Name = "Category 2" },
                new PieSeries<double> { Values = new double[] { 1 }, Name = "Category 3" }
            };
        }
    }
}