using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using WileyWidget.WinForms.Validation;

namespace WileyWidget.WinForms.ViewModels
{
    /// <summary>
    /// Validation-related partial class for <see cref="AccountsViewModel"/>.
    /// Provides validation helper methods and a validatable edit model for municipal accounts.
    /// </summary>
    public partial class AccountsViewModel
    {
        /// <summary>
        /// Validates a <see cref="MunicipalAccountEditModel"/> using data annotations.
        /// Use this for edit forms that bind to the edit model.
        /// </summary>
        /// <param name="editModel">The edit model to validate.</param>
        /// <returns>A list of validation error messages, empty if valid.</returns>
        public IReadOnlyList<string> ValidateEditModel(MunicipalAccountEditModel editModel)
        {
            if (editModel == null)
                return new[] { "Account edit model is required." };

            var results = new List<ValidationResult>();
            var context = new ValidationContext(editModel);
            Validator.TryValidateObject(editModel, context, results, validateAllProperties: true);

            return results
                .Where(r => r != ValidationResult.Success && r.ErrorMessage != null)
                .Select(r => r.ErrorMessage!)
                .ToList();
        }
    }

    /// <summary>
    /// Validatable edit model for municipal account creation/editing forms.
    /// Uses <see cref="ObservableValidator"/> for <see cref="System.ComponentModel.INotifyDataErrorInfo"/> support.
    /// </summary>
    /// <remarks>
    /// This model separates UI-bound properties from the EF Core entity, enabling:
    /// <list type="bullet">
    /// <item>Real-time validation with <see cref="INotifyDataErrorInfo"/></item>
    /// <item>ErrorProvider binding in WinForms via <see cref="Extensions.ErrorProviderBinding"/></item>
    /// <item>Data annotation validation without polluting the domain model</item>
    /// </list>
    /// </remarks>
    public partial class MunicipalAccountEditModel : ObservableValidator
    {
        /// <summary>
        /// Gets or sets the database ID. Zero for new accounts.
        /// </summary>
        public int Id { get; set; }

        [ObservableProperty]
        [NotifyDataErrorInfo]
        [Required(ErrorMessage = "Account number is required.")]
        [ValidAccountNumber(MaxLength = 20, ErrorMessage = "Account number must be 20 characters or less.")]
        private string accountNumber = string.Empty;

        [ObservableProperty]
        [NotifyDataErrorInfo]
        [Required(ErrorMessage = "Account name is required.")]
        [MaxLength(100, ErrorMessage = "Account name cannot exceed 100 characters.")]
        private string name = string.Empty;

        [ObservableProperty]
        [NotifyDataErrorInfo]
        [Range(1, int.MaxValue, ErrorMessage = "Please select a department.")]
        private int departmentId;

        [ObservableProperty]
        private int? budgetPeriodId;

        [ObservableProperty]
        [NotifyDataErrorInfo]
        [PositiveValue(AllowZero = true, ErrorMessage = "Budget amount cannot be negative.")]
        private decimal budgetAmount;

        [ObservableProperty]
        [NotifyDataErrorInfo]
        [PositiveValue(AllowZero = true, ErrorMessage = "Balance cannot be negative.")]
        private decimal balance;

        [ObservableProperty]
        private bool isActive = true;

        [ObservableProperty]
        private string? fundType;

        [ObservableProperty]
        private string? accountType;

        /// <summary>
        /// Validates all properties and returns whether the model is valid.
        /// </summary>
        /// <returns><c>true</c> if no validation errors; otherwise, <c>false</c>.</returns>
        public bool ValidateAll()
        {
            ValidateAllProperties();
            return !HasErrors;
        }

        /// <summary>
        /// Gets all validation error messages as a single collection.
        /// </summary>
        public IReadOnlyList<string> GetAllErrors()
        {
            return GetErrors()
                .Cast<ValidationResult>()
                .Where(r => r != ValidationResult.Success && r.ErrorMessage != null)
                .Select(r => r.ErrorMessage!)
                .ToList();
        }

        /// <summary>
        /// Creates an edit model from an existing MunicipalAccount entity.
        /// </summary>
        public static MunicipalAccountEditModel FromEntity(WileyWidget.Models.MunicipalAccount account)
        {
            if (account is null) throw new ArgumentNullException(nameof(account));
            return new MunicipalAccountEditModel
            {
                Id = account.Id,
                AccountNumber = account.AccountNumber?.Value ?? string.Empty,
                Name = account.Name,
                DepartmentId = account.DepartmentId,
                BudgetPeriodId = account.BudgetPeriodId,
                BudgetAmount = account.BudgetAmount,
                Balance = account.Balance,
                IsActive = account.IsActive,
                FundType = account.Fund.ToString(),
                AccountType = account.Type.ToString()
            };
        }
    }
}
