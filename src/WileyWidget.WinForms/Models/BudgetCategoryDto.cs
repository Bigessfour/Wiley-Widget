using CommunityToolkit.Mvvm.ComponentModel;

namespace WileyWidget.WinForms.Models
{
    /// <summary>
    /// DTO representing a budget category with computed status/variance helpers.
    /// </summary>
    /// <summary>
    /// Represents a class for budgetcategorydto.
    /// </summary>
    /// <summary>
    /// Represents a class for budgetcategorydto.
    /// </summary>
    /// <summary>
    /// Represents a class for budgetcategorydto.
    /// </summary>
    /// <summary>
    /// Represents a class for budgetcategorydto.
    /// </summary>
    public class BudgetCategoryDto : ObservableObject
    {
        /// <summary>
        /// Represents the id.
        /// </summary>
        private int id;
        private string category = string.Empty;
        private string accountNumber = string.Empty;
        /// <summary>
        /// Represents the budgetedamount.
        /// </summary>
        /// <summary>
        /// Represents the budgetedamount.
        /// </summary>
        private decimal budgetedAmount;
        /// <summary>
        /// Represents the actualamount.
        /// </summary>
        private decimal actualAmount;
        /// <summary>
        /// Represents the encumbranceamount.
        /// </summary>
        private decimal encumbranceAmount;
        /// <summary>
        /// Represents the fiscalyear.
        /// </summary>
        private int fiscalYear;
        private string departmentName = string.Empty;
        private string? fundName;

        public int Id
        {
            get => id;
            set => SetProperty(ref id, value);
        }

        public string Category
        {
            get => category;
            set => SetProperty(ref category, value);
        }

        public string AccountNumber
        {
            get => accountNumber;
            set => SetProperty(ref accountNumber, value);
        }

        public decimal BudgetedAmount
        {
            get => budgetedAmount;
            set
            {
                if (SetProperty(ref budgetedAmount, value))
                {
                    NotifyBudgetFieldsChanged();
                }
            }
        }

        public decimal ActualAmount
        {
            get => actualAmount;
            set
            {
                if (SetProperty(ref actualAmount, value))
                {
                    NotifyBudgetFieldsChanged();
                }
            }
        }

        public decimal EncumbranceAmount
        {
            get => encumbranceAmount;
            set
            {
                if (SetProperty(ref encumbranceAmount, value))
                {
                    NotifyBudgetFieldsChanged();
                }
            }
        }

        public int FiscalYear
        {
            get => fiscalYear;
            set => SetProperty(ref fiscalYear, value);
        }

        public string DepartmentName
        {
            get => departmentName;
            set => SetProperty(ref departmentName, value);
        }

        public string? FundName
        {
            get => fundName;
            set => SetProperty(ref fundName, value);
        }

        public decimal Variance => BudgetedAmount - ActualAmount - EncumbranceAmount;

        public decimal PercentUsed => BudgetedAmount == 0 ? 0m : (ActualAmount + EncumbranceAmount) / BudgetedAmount;

        public string Status
        {
            get
            {
                var percentUsed = PercentUsed;
                if (percentUsed < 0.75m)
                {
                    return "Under Budget";
                }

                if (percentUsed < 0.90m)
                {
                    return "On Track";
                }

                if (percentUsed < 1.0m)
                {
                    return "Approaching Limit";
                }

                return "Over Budget";
            }
        }

        public string Trend => Variance >= 0 ? "↗️" : "↘️";
        /// <summary>
        /// Performs notifybudgetfieldschanged.
        /// </summary>

        private void NotifyBudgetFieldsChanged()
        {
            OnPropertyChanged(nameof(Variance));
            OnPropertyChanged(nameof(PercentUsed));
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(Trend));
        }
    }
}
