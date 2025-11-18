using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System;

namespace WileyWidget.Models
{
    /// <summary>
    /// Represents the compliance status.
    /// </summary>
    public enum ComplianceStatus
    {
        Compliant,
        NonCompliant,
        Warning,
        Critical
    }

    /// <summary>
    /// Represents the severity level of a compliance violation.
    /// </summary>
    public enum ViolationSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    /// <summary>
    /// Represents a compliance violation with details about the issue and corrective actions.
    /// </summary>
    public class ComplianceViolation : INotifyPropertyChanged
    {
        private string? _regulation;
        private string? _description;
        private ViolationSeverity _severity;
        private string? _correctiveAction;

        /// <summary>
        /// Gets or sets the regulation that was violated.
        /// </summary>
        public string? Regulation
        {
            get => _regulation;
            set
            {
                if (_regulation != value)
                {
                    _regulation = value;
                    OnPropertyChanged(nameof(Regulation));
                }
            }
        }

        /// <summary>
        /// Gets or sets the description of the violation.
        /// </summary>
        public string? Description
        {
            get => _description;
            set
            {
                if (_description != value)
                {
                    _description = value;
                    OnPropertyChanged(nameof(Description));
                }
            }
        }

        /// <summary>
        /// Gets or sets the severity level of the violation.
        /// </summary>
        public ViolationSeverity Severity
        {
            get => _severity;
            set
            {
                if (_severity != value)
                {
                    _severity = value;
                    OnPropertyChanged(nameof(Severity));
                }
            }
        }

        /// <summary>
        /// Gets or sets the recommended corrective action.
        /// </summary>
        public string? CorrectiveAction
        {
            get => _correctiveAction;
            set
            {
                if (_correctiveAction != value)
                {
                    _correctiveAction = value;
                    OnPropertyChanged(nameof(CorrectiveAction));
                }
            }
        }

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

    /// <summary>
    /// Represents a compliance item with name, status, and description.
    /// </summary>
    public class ComplianceItem : INotifyPropertyChanged
    {
        private string? _name;
        private ComplianceStatus _status;
        private string? _description;

        /// <summary>
        /// Gets or sets the name of the compliance item.
        /// </summary>
        public string? Name
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
        /// Gets or sets the compliance status.
        /// </summary>
        public ComplianceStatus Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged(nameof(Status));
                }
            }
        }

        /// <summary>
        /// Gets or sets the description of the compliance item.
        /// </summary>
        public string? Description
        {
            get => _description;
            set
            {
                if (_description != value)
                {
                    _description = value;
                    OnPropertyChanged(nameof(Description));
                }
            }
        }

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

    /// <summary>
    /// Represents a compliance report building on AnalyticsData, including compliance items, overall status, and recommendations.
    /// </summary>
    public class ComplianceReport : AnalyticsData
    {
        private ObservableCollection<ComplianceItem> _complianceItems;
        private ComplianceStatus _overallStatus;
        private List<string> _recommendations;
        private int _enterpriseId;
        private DateTime _generatedDate;
        private List<ComplianceViolation> _violations;
        private int _complianceScore;

        /// <summary>
        /// Initializes a new instance of the ComplianceReport class.
        /// </summary>
        public ComplianceReport()
        {
            _complianceItems = new ObservableCollection<ComplianceItem>();
            _overallStatus = ComplianceStatus.Compliant;
            _recommendations = new List<string>();
            _generatedDate = DateTime.Now;
            _violations = new List<ComplianceViolation>();
            _complianceScore = 100;
        }

        /// <summary>
        /// Gets or sets the collection of compliance items.
        /// </summary>
        public ObservableCollection<ComplianceItem> ComplianceItems
        {
            get => _complianceItems;
            set
            {
                if (_complianceItems != value)
                {
                    _complianceItems = value;
                    OnPropertyChanged(nameof(ComplianceItems));
                    UpdateOverallStatus();
                }
            }
        }

        /// <summary>
        /// Gets or sets the overall compliance status.
        /// </summary>
        public ComplianceStatus OverallStatus
        {
            get => _overallStatus;
            set
            {
                if (_overallStatus != value)
                {
                    _overallStatus = value;
                    OnPropertyChanged(nameof(OverallStatus));
                }
            }
        }

        /// <summary>
        /// Gets or sets the recommendations for compliance.
        /// </summary>
        public List<string> Recommendations
        {
            get => _recommendations;
            set
            {
                if (_recommendations != value)
                {
                    _recommendations = value;
                    OnPropertyChanged(nameof(Recommendations));
                }
            }
        }

        /// <summary>
        /// Gets or sets the enterprise ID this report is for.
        /// </summary>
        public int EnterpriseId
        {
            get => _enterpriseId;
            set
            {
                if (_enterpriseId != value)
                {
                    _enterpriseId = value;
                    OnPropertyChanged(nameof(EnterpriseId));
                }
            }
        }

        /// <summary>
        /// Gets or sets the date when this report was generated.
        /// </summary>
        public DateTime GeneratedDate
        {
            get => _generatedDate;
            set
            {
                if (_generatedDate != value)
                {
                    _generatedDate = value;
                    OnPropertyChanged(nameof(GeneratedDate));
                }
            }
        }

        /// <summary>
        /// Gets or sets the list of compliance violations found.
        /// </summary>
        public List<ComplianceViolation> Violations
        {
            get => _violations;
            set
            {
                if (_violations != value)
                {
                    _violations = value;
                    OnPropertyChanged(nameof(Violations));
                }
            }
        }

        /// <summary>
        /// Gets or sets the compliance score.
        /// </summary>
        public int ComplianceScore
        {
            get => _complianceScore;
            set
            {
                if (_complianceScore != value)
                {
                    _complianceScore = value;
                    OnPropertyChanged(nameof(ComplianceScore));
                }
            }
        }

        /// <summary>
        /// Gets or sets the next audit date.
        /// </summary>
        public DateTime? NextAuditDate { get; set; }

        /// <summary>
        /// Updates the compliance report based on the current enterprises.
        /// </summary>
        public void UpdateCompliance()
        {
            ComplianceItems.Clear();

            foreach (var enterprise in Enterprises)
            {
                // Revenue Compliance: Check if monthly revenue is positive
                var revenueItem = new ComplianceItem
                {
                    Name = $"{enterprise.Name} - Revenue Compliance",
                    Status = enterprise.MonthlyRevenue > 0 ? ComplianceStatus.Compliant : ComplianceStatus.NonCompliant,
                    Description = enterprise.MonthlyRevenue > 0
                        ? "Monthly revenue is positive, meeting basic compliance requirements."
                        : "Monthly revenue is zero or negative, indicating potential financial issues."
                };
                ComplianceItems.Add(revenueItem);

                // Variance Compliance: Check if budget variance is within acceptable range (e.g., within 10% of revenue)
                double varianceThreshold = Math.Abs((double)enterprise.MonthlyRevenue) * 0.1;
                var varianceItem = new ComplianceItem
                {
                    Name = $"{enterprise.Name} - Budget Variance Compliance",
                    Status = Math.Abs((double)enterprise.CalculateBreakEvenVariance()) <= varianceThreshold ? ComplianceStatus.Compliant : ComplianceStatus.NonCompliant,
                    Description = Math.Abs((double)enterprise.CalculateBreakEvenVariance()) <= varianceThreshold
                        ? "Budget variance is within acceptable limits."
                        : $"Budget variance exceeds 10% threshold. Current variance: {enterprise.CalculateBreakEvenVariance():C}"
                };
                ComplianceItems.Add(varianceItem);
            }

            // Overall Statistics Compliance
            var statsItem = new ComplianceItem
            {
                Name = "Statistical Compliance",
                Status = StatisticalSummaries.StandardDeviation < StatisticalSummaries.Mean * 0.5 ? ComplianceStatus.Compliant : ComplianceStatus.NonCompliant,
                Description = StatisticalSummaries.StandardDeviation < StatisticalSummaries.Mean * 0.5
                    ? "Revenue distribution shows acceptable variability."
                    : "High revenue variability detected, may indicate operational inconsistencies."
            };
            ComplianceItems.Add(statsItem);

            UpdateOverallStatus();
            GenerateRecommendations();
        }

        /// <summary>
        /// Updates the overall compliance status based on individual items.
        /// </summary>
        private void UpdateOverallStatus()
        {
            OverallStatus = ComplianceItems.Any(item => item.Status == ComplianceStatus.NonCompliant)
                ? ComplianceStatus.NonCompliant
                : ComplianceStatus.Compliant;
        }

        /// <summary>
        /// Generates recommendations based on the compliance status.
        /// </summary>
        private void GenerateRecommendations()
        {
            var nonCompliantItems = ComplianceItems.Where(item => item.Status == ComplianceStatus.NonCompliant).ToList();

            if (!nonCompliantItems.Any())
            {
                Recommendations.Clear();
                Recommendations.Add("All compliance checks passed. Continue monitoring and maintaining current practices.");
                return;
            }

            var recommendations = new List<string>
            {
                "Compliance Issues Detected:",
                ""
            };

            foreach (var item in nonCompliantItems)
            {
                recommendations.Add($"- {item.Name}: {item.Description}");
            }

            recommendations.Add("");
            recommendations.Add("Recommended Actions:");
            recommendations.Add("- Review and address revenue shortfalls immediately.");
            recommendations.Add("- Analyze budget variances and adjust financial planning.");
            recommendations.Add("- Implement measures to reduce operational variability.");
            recommendations.Add("- Schedule regular compliance audits.");
            recommendations.Add("- Consult with financial advisors for corrective measures.");

            Recommendations.Clear();
            Recommendations.AddRange(recommendations);
        }

        /// <summary>
        /// Raises the PropertyChanged event and updates compliance if necessary.
        /// </summary>
        /// <param name="propertyName">The name of the property that changed.</param>
        protected override void OnPropertyChanged(string propertyName)
        {
            base.OnPropertyChanged(propertyName);
            if (propertyName == nameof(Enterprises))
            {
                UpdateCompliance();
            }
        }
    }
}