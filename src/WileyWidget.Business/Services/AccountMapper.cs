using System;
using System.Collections.Generic;
using System.Linq;
using WileyWidget.Abstractions.Models;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;

namespace WileyWidget.Business.Services
{
    /// <summary>
    /// Simple, manual mapper from domain models to display DTOs.
    /// No external dependencies — pure mapping logic that preserves fallback/default behavior.
    /// </summary>
    public class AccountMapper : IAccountMapper
    {
        public virtual IEnumerable<MunicipalAccountDisplay> MapToDisplay(IEnumerable<MunicipalAccount> domainAccounts)
        {
            if (domainAccounts == null) return Enumerable.Empty<MunicipalAccountDisplay>();

            return domainAccounts.Select(MapToDisplay).ToList();
        }

        public virtual MunicipalAccountDisplay MapToDisplay(MunicipalAccount account)
        {
            if (account == null) throw new ArgumentNullException(nameof(account));

            return new MunicipalAccountDisplay
            {
                Id = account.Id,
                AccountNumber = string.IsNullOrEmpty(account.AccountNumber?.Value) ? "N/A" : account.AccountNumber.Value,
                Name = string.IsNullOrEmpty(account.Name) ? "(Unnamed)" : account.Name,
                Description = account.FundDescription ?? string.Empty,
                Type = account.Type.ToString(),
                Fund = account.Fund.ToString(),
                Balance = account.Balance,
                BudgetAmount = account.BudgetAmount,
                Department = account.Department?.Name ?? "(Unassigned)",
                IsActive = account.IsActive,
                HasParent = account.ParentAccountId.HasValue
            };
        }
    }
}
