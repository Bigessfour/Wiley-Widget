using WileyWidget.Models;

namespace WileyWidget.WinForms.Models
{
    /// <summary>
    /// Edit model for municipal account data binding.
    /// </summary>
    public class MunicipalAccountEditModel
    {
        public int Id { get; set; }
        public string AccountNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Fund { get; set; } = string.Empty;
        public string? Department { get; set; }
        public decimal Balance { get; set; }
        public decimal BudgetAmount { get; set; }
        public bool IsActive { get; set; }
        public int? ParentAccountId { get; set; }

        public static MunicipalAccountEditModel FromEntity(MunicipalAccount account)
        {
            return new MunicipalAccountEditModel
            {
                Id = account.Id,
                AccountNumber = account.AccountNumber,
                Name = account.Name,
                Description = account.Description,
                Type = account.Type,
                Fund = account.Fund,
                Department = account.Department,
                Balance = account.Balance,
                BudgetAmount = account.BudgetAmount,
                IsActive = account.IsActive,
                ParentAccountId = account.ParentAccountId
            };
        }
    }
}
