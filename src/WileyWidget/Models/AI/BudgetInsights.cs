using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System;

namespace WileyWidget.Models
{
    /// <summary>
    /// Represents a projection with date and projected value.
    /// </summary>
    public class Projection : INotifyPropertyChanged
    {
        private DateTime _date;
        private double _projectedValue;

        /// <summary>
        /// Gets or sets the date of the projection.
        /// </summary>
        public DateTime Date
        {
            get => _date;
            set
            {
                if (_date != value)
                {
                    _date = value;
                    OnPropertyChanged(nameof(Date));
                }
            }
        }

        /// <summary>
        /// Gets or sets the projected value.
        /// </summary>
        public double ProjectedValue
        {
            get => _projectedValue;
            set
            {
                if (_projectedValue != value)
                {
                    _projectedValue = value;
                    OnPropertyChanged(nameof(ProjectedValue));
                }
            }
        }

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raises the PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">The name of the property that changed.</param>
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Represents budget insights building on AnalyticsData, including variance analysis, trend projections, and summary.
    /// </summary>
    public class BudgetInsights : AnalyticsData
    {
        private Dictionary<string, double> _varianceAnalysis;
        private List<Projection> _trendProjections;
        private string _summary;

        /// <summary>
        /// Initializes a new instance of the BudgetInsights class.
        /// </summary>
        public BudgetInsights()
        {
            _varianceAnalysis = new Dictionary<string, double>();
            _trendProjections = new List<Projection>();
            _summary = string.Empty;
        }

        /// <summary>
        /// Gets or sets the variance analysis as a dictionary of string keys and double values.
        /// </summary>
        public Dictionary<string, double> VarianceAnalysis
        {
            get => _varianceAnalysis;
            set
            {
                if (_varianceAnalysis != value)
                {
                    _varianceAnalysis = value;
                    OnPropertyChanged(nameof(VarianceAnalysis));
                }
            }
        }

        /// <summary>
        /// Gets or sets the trend projections as a list of Projection objects.
        /// </summary>
        public List<Projection> TrendProjections
        {
            get => _trendProjections;
            set
            {
                if (_trendProjections != value)
                {
                    _trendProjections = value;
                    OnPropertyChanged(nameof(TrendProjections));
                }
            }
        }

        /// <summary>
        /// Gets or sets the summary of insights.
        /// </summary>
        public string Summary
        {
            get => _summary;
            set
            {
                if (_summary != value)
                {
                    _summary = value;
                    OnPropertyChanged(nameof(Summary));
                }
            }
        }

        /// <summary>
        /// Updates the budget insights based on the current enterprises.
        /// </summary>
        public void UpdateInsights()
        {
            // Update VarianceAnalysis
            VarianceAnalysis.Clear();
            foreach (var enterprise in Enterprises)
            {
                VarianceAnalysis[enterprise.Name] = (double)enterprise.CalculateBreakEvenVariance();
            }

            // Update TrendProjections - Simple linear projection based on current revenues
            TrendProjections.Clear();
            if (Enterprises.Any())
            {
                double averageRevenue = Enterprises.Average(e => (double)e.MonthlyRevenue);
                double growthRate = 0.05; // Assume 5% monthly growth for projection
                DateTime startDate = DateTime.Now.AddMonths(1);
                for (int i = 0; i < 12; i++) // 12 months projection
                {
                    double projected = averageRevenue * Math.Pow(1 + growthRate, i + 1);
                    TrendProjections.Add(new Projection { Date = startDate.AddMonths(i), ProjectedValue = projected });
                }
            }

            // Update Summary
            GenerateSummary();
        }

        /// <summary>
        /// Generates a summary of the budget insights.
        /// </summary>
        private void GenerateSummary()
        {
            if (!Enterprises.Any())
            {
                Summary = "No enterprise data available for analysis.";
                return;
            }

            double totalVariance = VarianceAnalysis.Values.Sum();
            double avgVariance = VarianceAnalysis.Values.Average();
            double projectedTotal = TrendProjections.Sum(p => p.ProjectedValue);

            Summary = $"Budget Analysis Summary:\n" +
                     $"- Total Enterprises: {Enterprises.Count}\n" +
                     $"- Total Revenue: {TotalRevenue:C}\n" +
                     $"- Average Budget Variance: {AverageBudgetVariance:C}\n" +
                     $"- Total Variance: {totalVariance:C}\n" +
                     $"- Average Variance: {avgVariance:C}\n" +
                     $"- 12-Month Projected Revenue: {projectedTotal:C}\n" +
                     $"- Revenue Statistics: Mean={StatisticalSummaries.Mean:C}, Median={StatisticalSummaries.Median:C}\n" +
                     $"- Key Insights: {(totalVariance > 0 ? "Positive variance indicates budget surplus." : "Negative variance indicates budget deficit.")}";
        }

        /// <summary>
        /// Raises the PropertyChanged event and updates insights if necessary.
        /// </summary>
        /// <param name="propertyName">The name of the property that changed.</param>
        protected override void OnPropertyChanged(string propertyName)
        {
            base.OnPropertyChanged(propertyName);
            if (propertyName == nameof(Enterprises))
            {
                UpdateInsights();
            }
        }
    }
}