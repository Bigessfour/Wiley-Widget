using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System;

namespace WileyWidget.Models.Legacy
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
    /// Represents insights derived from budget analysis, including variances, projections, recommendations, and a health score.
    /// </summary>
    public class BudgetInsights
    {
        /// <summary>
        /// List of budget variances for different categories.
        /// </summary>
        public List<BudgetVariance> Variances { get; set; } = new List<BudgetVariance>();

        /// <summary>
        /// List of budget projections for future periods.
        /// </summary>
        public List<BudgetProjection> Projections { get; set; } = new List<BudgetProjection>();

        /// <summary>
        /// List of recommendations based on the budget analysis.
        /// </summary>
        public List<string> Recommendations { get; set; } = new List<string>();

        /// <summary>
        /// Overall health score of the budget (0-100).
        /// </summary>
        public int HealthScore { get; set; }
    }
}
