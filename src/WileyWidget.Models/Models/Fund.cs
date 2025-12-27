using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WileyWidget.Models.Entities;

/// <summary>
/// Represents a fund for organizing budget entries
/// </summary>
/// <summary>
/// Represents a class for fund.
/// </summary>
public class Fund
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public int Id { get; set; }

    [Required, MaxLength(20)]
    /// <summary>
    /// Gets or sets the fundcode.
    /// </summary>
    public string FundCode { get; set; } = string.Empty; // e.g., "100-General"

    [Required, MaxLength(100)]
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the type.
    /// </summary>

    public FundType Type { get; set; }

    public ICollection<BudgetEntry> BudgetEntries { get; set; } = new List<BudgetEntry>();
}
