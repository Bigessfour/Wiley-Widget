using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WileyWidget.Models.Entities;

/// <summary>
/// Represents a fund for organizing budget entries and chart of accounts.
/// Aligned with seed_chart_of_accounts.sql schema.
/// </summary>
public class Fund : IAuditable
{
    public int Id { get; set; }

    /// <summary>
    /// Unique fund code (e.g., "TOWN-GENERAL", "WSD-GENERAL", "WILEY-REC")
    /// </summary>
    [Required, MaxLength(50)]
    public string FundCode { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the fund (e.g., "Town General Fund")
    /// </summary>
    [Required, MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of the fund's purpose
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Fund type classification for GASB reporting
    /// </summary>
    public FundType Type { get; set; }

    /// <summary>
    /// Whether the fund is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    // IAuditable implementation
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public ICollection<BudgetEntry> BudgetEntries { get; set; } = new List<BudgetEntry>();
    public ICollection<ChartOfAccountEntry> ChartOfAccounts { get; set; } = new List<ChartOfAccountEntry>();
}
