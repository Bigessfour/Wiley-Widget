#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using WileyWidget.Models;

namespace WileyWidget.Business.Configuration;

/// <summary>
/// Validator for account types according to GASB standards
/// </summary>
public class AccountTypeValidator
{
    // Account number ranges for different types (GASB compliant)
    private static readonly Dictionary<AccountType, (int Min, int Max)[]> AccountRanges = new()
    {
        // Assets (100-199)
        [AccountType.Cash] = new[] { (100, 199) },
        [AccountType.Investments] = new[] { (100, 199) },
        [AccountType.Receivables] = new[] { (100, 199) },
        [AccountType.Inventory] = new[] { (100, 199) },
        [AccountType.FixedAssets] = new[] { (100, 199) },

        // Liabilities (200-299)
        [AccountType.Payables] = new[] { (200, 299) },
        [AccountType.Debt] = new[] { (200, 299) },
        [AccountType.AccruedLiabilities] = new[] { (200, 299) },

        // Equity (300-399)
        [AccountType.RetainedEarnings] = new[] { (300, 399) },
        [AccountType.FundBalance] = new[] { (300, 399) },

        // Revenues (400-499)
        [AccountType.Taxes] = new[] { (400, 499) },
        [AccountType.Fees] = new[] { (400, 499) },
        [AccountType.Grants] = new[] { (400, 499) },
        [AccountType.Interest] = new[] { (400, 499) },
        [AccountType.Sales] = new[] { (400, 499) },
        [AccountType.Revenue] = new[] { (400, 499) },

        // Expenses (500-699)
        [AccountType.Salaries] = new[] { (500, 699) },
        [AccountType.Supplies] = new[] { (500, 699) },
        [AccountType.Services] = new[] { (500, 699) },
        [AccountType.Utilities] = new[] { (500, 699) },
        [AccountType.Maintenance] = new[] { (500, 699) },
        [AccountType.Insurance] = new[] { (500, 699) },
        [AccountType.Depreciation] = new[] { (500, 699) },
        [AccountType.PermitsAndAssessments] = new[] { (500, 699) },
        [AccountType.ProfessionalServices] = new[] { (500, 699) },
        [AccountType.ContractLabor] = new[] { (500, 699) },
        [AccountType.DuesAndSubscriptions] = new[] { (500, 699) },
        [AccountType.CapitalOutlay] = new[] { (500, 699) },
        [AccountType.Transfers] = new[] { (500, 699) }
    };

    // Fund class restrictions for account types
    private static readonly Dictionary<AccountType, FundClass[]> AllowedFundClasses = new()
    {
        // General fund accounts - can be in governmental funds
        [AccountType.Cash] = new[] { FundClass.Governmental, FundClass.Proprietary, FundClass.Fiduciary },
        [AccountType.Investments] = new[] { FundClass.Governmental, FundClass.Proprietary, FundClass.Fiduciary },
        [AccountType.Receivables] = new[] { FundClass.Governmental, FundClass.Proprietary, FundClass.Fiduciary },
        [AccountType.Payables] = new[] { FundClass.Governmental, FundClass.Proprietary, FundClass.Fiduciary },
        [AccountType.Debt] = new[] { FundClass.Governmental, FundClass.Proprietary },
        [AccountType.AccruedLiabilities] = new[] { FundClass.Governmental, FundClass.Proprietary },

        // Revenue accounts
        [AccountType.Taxes] = new[] { FundClass.Governmental },
        [AccountType.Fees] = new[] { FundClass.Governmental, FundClass.Proprietary },
        [AccountType.Grants] = new[] { FundClass.Governmental },
        [AccountType.Interest] = new[] { FundClass.Governmental, FundClass.Proprietary, FundClass.Fiduciary },
        [AccountType.Sales] = new[] { FundClass.Proprietary },
        [AccountType.Revenue] = new[] { FundClass.Governmental, FundClass.Proprietary },

        // Expense accounts
        [AccountType.Salaries] = new[] { FundClass.Governmental, FundClass.Proprietary },
        [AccountType.Supplies] = new[] { FundClass.Governmental, FundClass.Proprietary },
        [AccountType.Services] = new[] { FundClass.Governmental, FundClass.Proprietary },
        [AccountType.Utilities] = new[] { FundClass.Governmental, FundClass.Proprietary },
        [AccountType.Maintenance] = new[] { FundClass.Governmental, FundClass.Proprietary },
        [AccountType.Insurance] = new[] { FundClass.Governmental, FundClass.Proprietary },
        [AccountType.PermitsAndAssessments] = new[] { FundClass.Governmental },
        [AccountType.ProfessionalServices] = new[] { FundClass.Governmental, FundClass.Proprietary },
        [AccountType.ContractLabor] = new[] { FundClass.Governmental, FundClass.Proprietary },
        [AccountType.DuesAndSubscriptions] = new[] { FundClass.Governmental, FundClass.Proprietary },
        [AccountType.CapitalOutlay] = new[] { FundClass.Governmental },
        [AccountType.Transfers] = new[] { FundClass.Governmental, FundClass.Proprietary, FundClass.Fiduciary },

        // Balance sheet accounts
        [AccountType.FixedAssets] = new[] { FundClass.Governmental, FundClass.Proprietary },
        [AccountType.Inventory] = new[] { FundClass.Proprietary },
        [AccountType.RetainedEarnings] = new[] { FundClass.Proprietary },
        [AccountType.FundBalance] = new[] { FundClass.Governmental, FundClass.Proprietary, FundClass.Fiduciary },
        [AccountType.Depreciation] = new[] { FundClass.Governmental, FundClass.Proprietary }
    };

    /// <summary>
    /// Validates that an account type is appropriate for the given account number
    /// </summary>
    /// <param name="accountType">The account type to validate</param>
    /// <param name="accountNumber">The account number</param>
    /// <returns>True if the account type is valid for the account number, false otherwise</returns>
    public bool ValidateAccountTypeForNumber(AccountType accountType, string accountNumber)
    {
        if (!TryGetAccountRootValue(accountNumber, out var accountRoot))
            return false;

        if (!AccountRanges.TryGetValue(accountType, out var ranges))
            return false;

        return ranges.Any(range => accountRoot >= range.Min && accountRoot <= range.Max);
    }

    /// <summary>
    /// Validates that an account type is allowed in the specified fund class
    /// </summary>
    /// <param name="accountType">The account type to validate</param>
    /// <param name="fundClass">The fund class</param>
    /// <returns>True if the account type is allowed in the fund class, false otherwise</returns>
    public bool ValidateAccountTypeForFund(AccountType accountType, FundClass fundClass)
    {
        if (!AllowedFundClasses.TryGetValue(accountType, out var allowedFunds))
            return false;

        return allowedFunds.Contains(fundClass);
    }

    /// <summary>
    /// Gets all valid account types for a given account number
    /// </summary>
    /// <param name="accountNumber">The account number</param>
    /// <returns>A collection of valid account types</returns>
    public IEnumerable<AccountType> GetValidAccountTypesForNumber(string accountNumber)
    {
        if (!TryGetAccountRootValue(accountNumber, out var accountRoot))
            return Enumerable.Empty<AccountType>();

        return AccountRanges
            .Where(kvp => kvp.Value.Any(range => accountRoot >= range.Min && accountRoot <= range.Max))
            .Select(kvp => kvp.Key);
    }

    /// <summary>
    /// Gets all valid account types for a given fund class
    /// </summary>
    /// <param name="fundClass">The fund class</param>
    /// <returns>A collection of valid account types</returns>
    public IEnumerable<AccountType> GetValidAccountTypesForFund(FundClass fundClass)
    {
        return AllowedFundClasses
            .Where(kvp => kvp.Value.Contains(fundClass))
            .Select(kvp => kvp.Key);
    }

    /// <summary>
    /// Validates a municipal account for GASB compliance
    /// </summary>
    /// <param name="account">The municipal account to validate</param>
    /// <returns>A list of validation errors, empty if valid</returns>
    public List<string> ValidateAccount(MunicipalAccount account)
    {
        var errors = new List<string>();

        if (account == null)
        {
            errors.Add("Account cannot be null");
            return errors;
        }

        if (account.AccountNumber == null || string.IsNullOrWhiteSpace(account.AccountNumber.Value))
        {
            errors.Add($"Account {account.Id} ({account.Name}) is missing account number");
            return errors;
        }

        // Validate account type against account number
        if (!ValidateAccountTypeForNumber(account.Type, account.AccountNumber.Value))
        {
            errors.Add($"Account {account.Id} ({account.Name}): Account type '{account.Type}' is not valid for account number '{account.AccountNumber.Value}'");
        }

        // Validate account type against fund class
        if (account.FundClass.HasValue && !ValidateAccountTypeForFund(account.Type, account.FundClass.Value))
        {
            errors.Add($"Account {account.Id} ({account.Name}): Account type '{account.Type}' is not allowed in fund class '{account.FundClass.Value}'");
        }

        return errors;
    }

    /// <summary>
    /// Validates account type compliance for a collection of municipal accounts
    /// </summary>
    /// <param name="accounts">The accounts to validate</param>
    /// <returns>Validation result with success status and any errors</returns>
    public ValidationResult ValidateAccountTypeCompliance(IEnumerable<MunicipalAccount> accounts)
    {
        if (accounts == null) throw new ArgumentNullException(nameof(accounts));

        var allErrors = new List<string>();

        foreach (var account in accounts)
        {
            var accountErrors = ValidateAccount(account);
            allErrors.AddRange(accountErrors);
        }

        return new ValidationResult
        {
            IsValid = !allErrors.Any(),
            Errors = allErrors
        };
    }

    /// <summary>
    /// Result of a validation operation
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    private static bool TryGetAccountRootValue(string accountNumber, out int accountRoot)
    {
        accountRoot = 0;

        if (string.IsNullOrWhiteSpace(accountNumber))
            return false;

        var rootSegment = accountNumber.Split(new[] { '.', '-' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (string.IsNullOrEmpty(rootSegment))
            return false;

        if (rootSegment.Length > 3)
        {
            rootSegment = rootSegment[..3];
        }

        return int.TryParse(rootSegment, out accountRoot);
    }
}
