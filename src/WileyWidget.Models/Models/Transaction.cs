#nullable enable
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WileyWidget.Models.Entities;

namespace WileyWidget.Models;

/// <summary>
/// Represents a financial transaction against a budget entry
/// </summary>
public class Transaction : IAuditable
{
    /// <summary>
    /// Unique identifier for the transaction
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// The budget entry this transaction belongs to
    /// </summary>
    [Required]
    public int BudgetEntryId { get; set; }
    [ForeignKey("BudgetEntryId")]
    public BudgetEntry BudgetEntry { get; set; } = null!;

    /// <summary>
    /// Transaction amount
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    /// <summary>
    /// Transaction description
    /// </summary>
    [Required]
    [StringLength(200)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Transaction date
    /// </summary>
    [Required]
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Transaction type
    /// </summary>
    [Required]
    [StringLength(50)]
    public string Type { get; set; } = string.Empty; // e.g., "Payment", "Adjustment"

    // Auditing
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Transaction types
/// </summary>
public enum TransactionType
{
    Debit,
    Credit
}