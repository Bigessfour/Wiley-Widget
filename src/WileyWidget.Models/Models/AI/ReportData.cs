using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using WileyWidget.Models.Entities;

namespace WileyWidget.Models
{
    /// <summary>
    /// Represents report data containing enterprises and calculated metrics.
    /// </summary>
    public class ReportData : INotifyPropertyChanged
    {
        private ObservableCollection<Enterprise> _enterprises;
        private string _title;
        private DateTime _generatedAt;
        private BudgetVarianceAnalysis _budgetSummary;
        private BudgetVarianceAnalysis _varianceAnalysis;
        private ObservableCollection<DepartmentSummary> _departments;
        private ObservableCollection<FundSummary> _funds;
        private ObservableCollection<AuditEntry> _auditEntries;
        private BudgetVarianceAnalysis _yearEndSummary;

        /// <summary>
        /// Initializes a new instance of the ReportData class.
        /// </summary>
        public ReportData()
        {
            _enterprises = new ObservableCollection<Enterprise>();
            _title = string.Empty;
            _generatedAt = DateTime.Now;
            _budgetSummary = new BudgetVarianceAnalysis();
            _varianceAnalysis = new BudgetVarianceAnalysis();
            _departments = new ObservableCollection<DepartmentSummary>();
            _funds = new ObservableCollection<FundSummary>();
            _auditEntries = new ObservableCollection<AuditEntry>();
            _yearEndSummary = new BudgetVarianceAnalysis();
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
                    OnPropertyChanged(nameof(EnterpriseCount));
                }
            }
        }

        /// <summary>
        /// Gets the count of enterprises in the report.
        /// </summary>
        public int EnterpriseCount => Enterprises?.Count ?? 0;

        /// <summary>
        /// Gets or sets the title of the report.
        /// </summary>
        public string Title
        {
            get => _title;
            set
            {
                if (_title != value)
                {
                    _title = value;
                    OnPropertyChanged(nameof(Title));
                }
            }
        }

        /// <summary>
        /// Gets or sets the generated at date.
        /// </summary>
        public DateTime GeneratedAt
        {
            get => _generatedAt;
            set
            {
                if (_generatedAt != value)
                {
                    _generatedAt = value;
                    OnPropertyChanged(nameof(GeneratedAt));
                }
            }
        }

        /// <summary>
        /// Gets or sets the budget summary.
        /// </summary>
        public BudgetVarianceAnalysis BudgetSummary
        {
            get => _budgetSummary;
            set
            {
                if (_budgetSummary != value)
                {
                    _budgetSummary = value;
                    OnPropertyChanged(nameof(BudgetSummary));
                }
            }
        }

        /// <summary>
        /// Gets or sets the variance analysis.
        /// </summary>
        public BudgetVarianceAnalysis VarianceAnalysis
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
        /// Gets or sets the departments.
        /// </summary>
        public ObservableCollection<DepartmentSummary> Departments
        {
            get => _departments;
            set
            {
                if (_departments != value)
                {
                    _departments = value;
                    OnPropertyChanged(nameof(Departments));
                }
            }
        }

        /// <summary>
        /// Gets or sets the funds.
        /// </summary>
        public ObservableCollection<FundSummary> Funds
        {
            get => _funds;
            set
            {
                if (_funds != value)
                {
                    _funds = value;
                    OnPropertyChanged(nameof(Funds));
                }
            }
        }

        /// <summary>
        /// Gets or sets the audit entries.
        /// </summary>
        public ObservableCollection<AuditEntry> AuditEntries
        {
            get => _auditEntries;
            set
            {
                if (_auditEntries != value)
                {
                    _auditEntries = value;
                    OnPropertyChanged(nameof(AuditEntries));
                }
            }
        }

        /// <summary>
        /// Gets or sets the year end summary.
        /// </summary>
        public BudgetVarianceAnalysis YearEndSummary
        {
            get => _yearEndSummary;
            set
            {
                if (_yearEndSummary != value)
                {
                    _yearEndSummary = value;
                    OnPropertyChanged(nameof(YearEndSummary));
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
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

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