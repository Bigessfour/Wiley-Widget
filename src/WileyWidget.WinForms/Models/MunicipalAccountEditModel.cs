using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using WileyWidget.Models;

namespace WileyWidget.WinForms.Models
{
    /// <summary>
    /// Edit model for municipal account data binding with data-annotation validation helpers.
    /// </summary>
    /// <summary>
    /// Represents a class for municipalaccounteditmodel.
    /// </summary>
    /// <summary>
    /// Represents a class for municipalaccounteditmodel.
    /// </summary>
    /// <summary>
    /// Represents a class for municipalaccounteditmodel.
    /// </summary>
    /// <summary>
    /// Represents a class for municipalaccounteditmodel.
    /// </summary>
    public class MunicipalAccountEditModel
    {
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        /// <summary>
        /// Gets or sets the accountnumber.
        /// </summary>
        /// <summary>
        /// Gets or sets the accountnumber.
        /// </summary>
        /// <summary>
        /// Gets or sets the accountnumber.
        /// </summary>
        /// <summary>
        /// Gets or sets the accountnumber.
        /// </summary>
        /// <summary>
        /// Gets or sets the accountnumber.
        /// </summary>
        public string AccountNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        /// <summary>
        /// Gets or sets the type.
        /// </summary>
        /// <summary>
        /// Gets or sets the type.
        /// </summary>
        /// <summary>
        /// Gets or sets the type.
        /// </summary>
        /// <summary>
        /// Gets or sets the type.
        /// </summary>
        /// <summary>
        /// Gets or sets the type.
        /// </summary>
        public AccountType Type { get; set; }

        [Required]
        /// <summary>
        /// Gets or sets the fund.
        /// </summary>
        /// <summary>
        /// Gets or sets the fund.
        /// </summary>
        /// <summary>
        /// Gets or sets the fund.
        /// </summary>
        /// <summary>
        /// Gets or sets the fund.
        /// </summary>
        public MunicipalFundType Fund { get; set; }

        public int? DepartmentId { get; set; }

        [Range(typeof(decimal), "-9999999999", "9999999999")]
        /// <summary>
        /// Gets or sets the balance.
        /// </summary>
        /// <summary>
        /// Gets or sets the balance.
        /// </summary>
        /// <summary>
        /// Gets or sets the balance.
        /// </summary>
        /// <summary>
        /// Gets or sets the balance.
        /// </summary>
        /// <summary>
        /// Gets or sets the balance.
        /// </summary>
        public decimal Balance { get; set; }

        [Range(typeof(decimal), "0", "9999999999")]
        /// <summary>
        /// Gets or sets the budgetamount.
        /// </summary>
        /// <summary>
        /// Gets or sets the budgetamount.
        /// </summary>
        /// <summary>
        /// Gets or sets the budgetamount.
        /// </summary>
        /// <summary>
        /// Gets or sets the budgetamount.
        /// </summary>
        public decimal BudgetAmount { get; set; }
        /// <summary>
        /// Gets or sets the isactive.
        /// </summary>
        /// <summary>
        /// Gets or sets the isactive.
        /// </summary>
        /// <summary>
        /// Gets or sets the isactive.
        /// </summary>
        /// <summary>
        /// Gets or sets the isactive.
        /// </summary>
        /// <summary>
        /// Gets or sets the isactive.
        /// </summary>

        public bool IsActive { get; set; }

        public int? ParentAccountId { get; set; }
        /// <summary>
        /// Performs fromentity. Parameters: account.
        /// </summary>
        /// <param name="account">The account.</param>
        /// <summary>
        /// Performs fromentity. Parameters: account.
        /// </summary>
        /// <param name="account">The account.</param>
        /// <summary>
        /// Performs fromentity. Parameters: account.
        /// </summary>
        /// <param name="account">The account.</param>
        /// <summary>
        /// Performs fromentity. Parameters: account.
        /// </summary>
        /// <param name="account">The account.</param>

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
                Fund = account.Fund,
                DepartmentId = account.DepartmentId,
                Balance = account.Balance,
                BudgetAmount = account.BudgetAmount,
                IsActive = account.IsActive,
                ParentAccountId = account.ParentAccountId
            };
        }
        /// <summary>
        /// Performs validateall. Parameters: results.
        /// </summary>
        /// <param name="results">The results.</param>
        /// <summary>
        /// Performs validateall. Parameters: results.
        /// </summary>
        /// <param name="results">The results.</param>
        /// <summary>
        /// Performs validateall.
        /// </summary>
        /// <summary>
        /// Performs validateall. Parameters: results.
        /// </summary>
        /// <param name="results">The results.</param>
        /// <summary>
        /// Performs validateall.
        /// </summary>
        /// <summary>
        /// Performs validateall. Parameters: results.
        /// </summary>
        /// <param name="results">The results.</param>
        /// <summary>
        /// Performs validateall.
        /// </summary>
        /// <summary>
        /// Performs validateall. Parameters: results.
        /// </summary>
        /// <param name="results">The results.</param>
        /// <summary>
        /// Performs validateall.
        /// </summary>

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
