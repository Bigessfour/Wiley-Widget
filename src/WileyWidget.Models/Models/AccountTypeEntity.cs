#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace WileyWidget.Models.Entities;

/// <summary>
/// Represents an account type classification for chart of accounts.
/// Aligned with seed_chart_of_accounts.sql AccountTypes table.
/// Note: Named AccountTypeEntity to avoid conflict with existing AccountType enum in MunicipalAccount.
/// </summary>
public class AccountTypeEntity : IAuditable
{
    public int Id { get; set; }

    /// <summary>
    /// Type name (e.g., "Income", "Expense", "Asset", "Liability", "Equity")
    /// </summary>
    [Required, MaxLength(50)]
    public string TypeName { get; set; } = string.Empty;

    /// <summary>
    /// Description of the account type
    /// </summary>
    [MaxLength(255)]
    public string? Description { get; set; }

    /// <summary>
    /// True for Asset/Expense types (debit increases), False for Liability/Equity/Income (credit increases)
    /// </summary>
    public bool IsDebit { get; set; }

    // IAuditable implementation
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public ICollection<ChartOfAccountEntry> ChartOfAccounts { get; set; } = new List<ChartOfAccountEntry>();
}
