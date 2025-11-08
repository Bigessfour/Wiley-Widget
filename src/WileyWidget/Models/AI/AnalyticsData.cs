using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System;

namespace WileyWidget.Models
{
    /// <summary>
    /// Represents KPI data with name and value.
    /// </summary>
    public class KPI : INotifyPropertyChanged
    {
        private string _name;
        private double _value;

        /// <summary>
        /// Gets or sets the name of the KPI.
        /// </summary>
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        /// <summary>
        /// Gets or sets the value of the KPI.
        /// </summary>
        public double Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    _value = value;
                    OnPropertyChanged(nameof(Value));
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
    /// Represents statistical summary data.
    /// </summary>
    public class StatisticalSummary : INotifyPropertyChanged
    {
        private double _mean;
        private double _median;
        private double _standardDeviation;
        private double _min;
        private double _max;
        private int _count;

        /// <summary>
        /// Gets or sets the mean value.
        /// </summary>
        public double Mean
        {
            get => _mean;
            set
            {
                if (_mean != value)
                {
                    _mean = value;
                    OnPropertyChanged(nameof(Mean));
                }
            }
        }

        /// <summary>
        /// Gets or sets the median value.
        /// </summary>
        public double Median
        {
            get => _median;
            set
            {
                if (_median != value)
                {
                    _median = value;
                    OnPropertyChanged(nameof(Median));
                }
            }
        }

        /// <summary>
        /// Gets or sets the standard deviation.
        /// </summary>
        public double StandardDeviation
        {
            get => _standardDeviation;
            set
            {
                if (_standardDeviation != value)
                {
                    _standardDeviation = value;
                    OnPropertyChanged(nameof(StandardDeviation));
                }
            }
        }

        /// <summary>
        /// Gets or sets the minimum value.
        /// </summary>
        public double Min
        {
            get => _min;
            set
            {
                if (_min != value)
                {
                    _min = value;
                    OnPropertyChanged(nameof(Min));
                }
            }
        }

        /// <summary>
        /// Gets or sets the maximum value.
        /// </summary>
        public double Max
        {
            get => _max;
            set
            {
                if (_max != value)
                {
                    _max = value;
                    OnPropertyChanged(nameof(Max));
                }
            }
        }

        /// <summary>
        /// Gets or sets the count of values.
        /// </summary>
        public int Count
        {
            get => _count;
            set
            {
                if (_count != value)
                {
                    _count = value;
                    OnPropertyChanged(nameof(Count));
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
    /// Represents analytics data building on ReportData, including chart data, KPIs, and statistical summaries.
    /// </summary>
    public class AnalyticsData : ReportData
    {
        private Dictionary<string, double> _chartData;
        private ObservableCollection<KPI> _kpis;
        private StatisticalSummary _statisticalSummaries;

        /// <summary>
        /// Initializes a new instance of the AnalyticsData class.
        /// </summary>
        public AnalyticsData()
        {
            _chartData = new Dictionary<string, double>();
            _kpis = new ObservableCollection<KPI>();
            _statisticalSummaries = new StatisticalSummary();
        }

        /// <summary>
        /// Gets or sets the chart data as a dictionary of string keys and double values.
        /// </summary>
        public Dictionary<string, double> ChartData
        {
            get => _chartData;
            set
            {
                if (_chartData != value)
                {
                    _chartData = value;
                    OnPropertyChanged(nameof(ChartData));
                }
            }
        }

        /// <summary>
        /// Gets or sets the collection of KPIs.
        /// </summary>
        public ObservableCollection<KPI> KPIs
        {
            get => _kpis;
            set
            {
                if (_kpis != value)
                {
                    _kpis = value;
                    OnPropertyChanged(nameof(KPIs));
                }
            }
        }

        /// <summary>
        /// Gets or sets the statistical summaries.
        /// </summary>
        public StatisticalSummary StatisticalSummaries
        {
            get => _statisticalSummaries;
            set
            {
                if (_statisticalSummaries != value)
                {
                    _statisticalSummaries = value;
                    OnPropertyChanged(nameof(StatisticalSummaries));
                }
            }
        }

        /// <summary>
        /// Updates the analytics data based on the current enterprises.
        /// </summary>
        public void UpdateAnalytics()
        {
            // Update ChartData
            ChartData.Clear();
            foreach (var enterprise in Enterprises)
            {
                ChartData[enterprise.Name] = (double)enterprise.MonthlyRevenue;
            }

            // Update KPIs
            KPIs.Clear();
            KPIs.Add(new KPI { Name = "Total Revenue", Value = (double)TotalRevenue });
            KPIs.Add(new KPI { Name = "Average Budget Variance", Value = (double)AverageBudgetVariance });

            // Update StatisticalSummaries
            var revenues = Enterprises.Select(e => (double)e.MonthlyRevenue).ToList();
            if (revenues.Any())
            {
                StatisticalSummaries.Count = revenues.Count;
                StatisticalSummaries.Mean = revenues.Average();
                StatisticalSummaries.Min = revenues.Min();
                StatisticalSummaries.Max = revenues.Max();

                // Calculate median
                var sorted = revenues.OrderBy(x => x).ToList();
                int mid = sorted.Count / 2;
                StatisticalSummaries.Median = sorted.Count % 2 == 0
                    ? (sorted[mid - 1] + sorted[mid]) / 2
                    : sorted[mid];

                // Calculate standard deviation
                double variance = revenues.Sum(x => Math.Pow(x - StatisticalSummaries.Mean, 2)) / revenues.Count;
                StatisticalSummaries.StandardDeviation = Math.Sqrt(variance);
            }
            else
            {
                StatisticalSummaries.Count = 0;
                StatisticalSummaries.Mean = 0;
                StatisticalSummaries.Median = 0;
                StatisticalSummaries.Min = 0;
                StatisticalSummaries.Max = 0;
                StatisticalSummaries.StandardDeviation = 0;
            }
        }

        /// <summary>
        /// Raises the PropertyChanged event and updates analytics if necessary.
        /// </summary>
        /// <param name="propertyName">The name of the property that changed.</param>
        protected override void OnPropertyChanged(string propertyName)
        {
            base.OnPropertyChanged(propertyName);
            if (propertyName == nameof(Enterprises))
            {
                UpdateAnalytics();
            }
        }
    }
}