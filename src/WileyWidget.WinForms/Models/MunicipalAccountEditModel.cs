using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using WileyWidget.Models;

namespace WileyWidget.WinForms.Models
{
    /// <summary>
    /// Edit model for municipal account data binding with data-annotation validation helpers.
    /// </summary>
    public class MunicipalAccountEditModel
    {
        public MunicipalAccountEditModel(MunicipalAccount? account = null)
        {
            if (account != null)
            {
                Id = account.Id;
                AccountNumber = account.AccountNumber?.Value ?? string.Empty;
                Name = account.Name;
                Description = account.FundDescription;
                Type = account.Type;
                FundType = account.FundType;
                FundId = account.FundId;
                DepartmentId = account.DepartmentId;
                Balance = account.Balance;
                BudgetAmount = account.BudgetAmount;
                IsActive = account.IsActive;
                ParentAccountId = account.ParentAccountId;
            }
            else
            {
                IsActive = true;
                Balance = 0m;
                BudgetAmount = 0m;
            }
        }

        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        public string AccountNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        public AccountType Type { get; set; }

        [Required]
        public MunicipalFundType FundType { get; set; }

        /// <summary>
        /// Foreign key to Fund table (optional, takes precedence over FundType enum)
        /// </summary>
        public int? FundId { get; set; }

        [Required(ErrorMessage = "Department is required")]
        [Range(1, int.MaxValue, ErrorMessage = "Please select a valid department")]
        public int? DepartmentId { get; set; }

        [Range(typeof(decimal), "-9999999999", "9999999999")]
        public decimal Balance { get; set; }

        [Range(typeof(decimal), "0", "9999999999")]
        public decimal BudgetAmount { get; set; }

        public bool IsActive { get; set; }

        public int? ParentAccountId { get; set; }

        public static MunicipalAccountEditModel FromEntity(MunicipalAccount account)
        {
            if (account == null) throw new ArgumentNullException(nameof(account));

            return new MunicipalAccountEditModel
            {
                Id = account.Id,
                AccountNumber = account.AccountNumber?.Value ?? string.Empty,
                Name = account.Name,
                Description = account.FundDescription,
                Type = account.Type,
                FundType = account.FundType,
                FundId = account.FundId,
                DepartmentId = account.DepartmentId,
                Balance = account.Balance,
                BudgetAmount = account.BudgetAmount,
                IsActive = account.IsActive,
                ParentAccountId = account.ParentAccountId
            };
        }

        public MunicipalAccount ToEntity()
        {
            // Validate DepartmentId is set before converting
            if (!DepartmentId.HasValue || DepartmentId.Value <= 0)
            {
                throw new InvalidOperationException("DepartmentId must be set to a valid value before converting to entity. Please select a department.");
            }

            return new MunicipalAccount
            {
                Id = this.Id,
                AccountNumber = new AccountNumber { Value = this.AccountNumber },
                Name = this.Name,
                FundDescription = Description ?? string.Empty,
                Type = this.Type,
                FundType = this.FundType,
                FundId = this.FundId,
                DepartmentId = this.DepartmentId.Value,
                Balance = this.Balance,
                BudgetAmount = this.BudgetAmount,
                IsActive = this.IsActive,
                ParentAccountId = this.ParentAccountId,
                BudgetPeriodId = 1 // Default to first budget period
            };
        }

        public bool ValidateAll(out List<ValidationResult> results)
        {
            results = new List<ValidationResult>();
            var context = new ValidationContext(this, null, null);
            return Validator.TryValidateObject(this, context, results, validateAllProperties: true);
        }

        public bool ValidateAll()
        {
            return ValidateAll(out _);
        }

        public IList<string> GetAllErrors()
        {
            ValidateAll(out var results);
            var messages = new List<string>();
            foreach (var result in results)
            {
                if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                {
                    messages.Add(result.ErrorMessage);
                }
            }
            return messages;
        }
    }
}
