#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WileyWidget.Models.Entities;

/// <summary>
/// Represents an entry in the Chart of Accounts, linking account numbers to funds and account types.
/// Aligned with seed_chart_of_accounts.sql ChartOfAccounts table.
/// </summary>
public class ChartOfAccountEntry : IAuditable
{
    public int Id { get; set; }

    /// <summary>
    /// Account number (e.g., "300", "401", "485.2")
    /// </summary>
    [Required, MaxLength(50)]
    public string AccountNumber { get; set; } = string.Empty;

    /// <summary>
    /// Account name/description (e.g., "GENERAL REVENUES", "SALARIES EMPLOYEES")
    /// </summary>
    [Required, MaxLength(255)]
    public string AccountName { get; set; } = string.Empty;

    /// <summary>
    /// Fund this account belongs to
    /// </summary>
    [Required]
    public int FundId { get; set; }

    [ForeignKey(nameof(FundId))]
    public Fund? Fund { get; set; }

    /// <summary>
    /// Account type classification (Income, Expense, Asset, etc.)
    /// </summary>
    [Required]
    public int AccountTypeId { get; set; }

    [ForeignKey(nameof(AccountTypeId))]
    public AccountTypeEntity? AccountType { get; set; }

    /// <summary>
    /// Parent account for hierarchical chart of accounts (null for root accounts)
    /// </summary>
    public int? ParentAccountId { get; set; }

    [ForeignKey(nameof(ParentAccountId))]
    public ChartOfAccountEntry? ParentAccount { get; set; }

    /// <summary>
    /// Child accounts in the hierarchy
    /// </summary>
    public ICollection<ChartOfAccountEntry> ChildAccounts { get; set; } = new List<ChartOfAccountEntry>();

    /// <summary>
    /// Whether this account is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    // IAuditable implementation
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Display name combining account number and name
    /// </summary>
    [NotMapped]
    public string DisplayName => $"{AccountNumber} - {AccountName}";

    /// <summary>
    /// Returns true if this is an income/revenue account
    /// </summary>
    [NotMapped]
    public bool IsIncomeAccount => AccountType?.TypeName == "Income";

    /// <summary>
    /// Returns true if this is an expense account
    /// </summary>
    [NotMapped]
    public bool IsExpenseAccount => AccountType?.TypeName == "Expense";
}
