using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WileyWidget.Models.Entities;

/// <summary>
/// Represents a fund for organizing budget entries
/// </summary>
public class Fund
{
    public int Id { get; set; }

    [Required, MaxLength(20)]
    public string FundCode { get; set; } = string.Empty; // e.g., "100-General"

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public FundType Type { get; set; }

    public ICollection<BudgetEntry> BudgetEntries { get; set; } = new List<BudgetEntry>();
}