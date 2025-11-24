using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WileyWidget.WinUI.ViewModels
{
    /// <summary>
    /// ViewModel for ChartView - handles chart data and visualization.
    /// Uses LiveCharts2 for WinUI 3 charts.
    /// </summary>
    public partial class ChartViewModel : ObservableObject
    {
        private readonly ILogger<ChartViewModel> _logger;

        [ObservableProperty]
        private ISeries[] _chartSeries = Array.Empty<ISeries>();

        [ObservableProperty]
        private ISeries[] _pieChartSeries = Array.Empty<ISeries>();

        [ObservableProperty]
        private Axis[] _xAxes = Array.Empty<Axis>();

        [ObservableProperty]
        private Axis[] _yAxes = Array.Empty<Axis>();

        [ObservableProperty]
        private bool _isLoading;

        public ChartViewModel(ILogger<ChartViewModel> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _logger.LogInformation("ChartViewModel initialized");

            InitializeAxes();
        }

        public async Task LoadChartDataAsync()
        {
            try
            {
                IsLoading = true;
                _logger.LogInformation("Loading chart data");

                // Simulate async data loading
                await Task.Delay(500);

                // Generate sample chart data
                ChartSeries = new ISeries[]
                {
                    new LineSeries<double>
                    {
                        Name = "Revenue",
                        Values = GenerateSampleData(12, 1000, 5000),
                        Fill = null,
                        Stroke = new SolidColorPaint(SKColors.Blue) { StrokeThickness = 2 }
                    },
                    new LineSeries<double>
                    {
                        Name = "Expenses",
                        Values = GenerateSampleData(12, 500, 3000),
                        Fill = null,
                        Stroke = new SolidColorPaint(SKColors.Red) { StrokeThickness = 2 }
                    }
                };

                PieChartSeries = new ISeries[]
                {
                    new PieSeries<double> { Name = "Revenue", Values = new[] { 42.0 } },
                    new PieSeries<double> { Name = "Expenses", Values = new[] { 25.0 } },
                    new PieSeries<double> { Name = "Profit", Values = new[] { 17.0 } },
                    new PieSeries<double> { Name = "Other", Values = new[] { 16.0 } }
                };

                _logger.LogInformation("Chart data loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load chart data");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void InitializeAxes()
        {
            XAxes = new Axis[]
            {
                new Axis
                {
                    Name = "Month",
                    Labels = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", 
                                   "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" }
                }
            };

            YAxes = new Axis[]
            {
                new Axis
                {
                    Name = "Amount ($)",
                    NamePaint = new SolidColorPaint(SKColors.Black)
                }
            };
        }

        private double[] GenerateSampleData(int count, double min, double max)
        {
            var random = new Random();
            return Enumerable.Range(0, count)
                .Select(_ => min + (random.NextDouble() * (max - min)))
                .ToArray();
        }
    }
}
