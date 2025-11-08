using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace WileyWidget.Models
{
    /// <summary>
    /// Represents report data containing enterprises and calculated metrics.
    /// </summary>
    public class ReportData : INotifyPropertyChanged
    {
        private ObservableCollection<Enterprise> _enterprises;

        /// <summary>
        /// Initializes a new instance of the ReportData class.
        /// </summary>
        public ReportData()
        {
            _enterprises = new ObservableCollection<Enterprise>();
        }

        /// <summary>
        /// Gets or sets the collection of enterprises.
        /// </summary>
        public ObservableCollection<Enterprise> Enterprises
        {
            get => _enterprises;
            set
            {
                if (_enterprises != value)
                {
                    _enterprises = value;
                    OnPropertyChanged(nameof(Enterprises));
                    OnPropertyChanged(nameof(TotalRevenue));
                    OnPropertyChanged(nameof(AverageBudgetVariance));
                }
            }
        }

        /// <summary>
        /// Gets the total revenue from all enterprises.
        /// </summary>
        public decimal TotalRevenue => Enterprises?.Sum(e => e.MonthlyRevenue) ?? 0;

        /// <summary>
        /// Gets the average budget variance from all enterprises.
        /// </summary>
        public decimal AverageBudgetVariance => Enterprises?.Any() == true ? Enterprises.Average(e => e.CalculateBreakEvenVariance()) : 0;

        /// <summary>
        /// Gets the count of enterprises in the report.
        /// </summary>
        public int EnterpriseCount => Enterprises?.Count ?? 0;

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
}