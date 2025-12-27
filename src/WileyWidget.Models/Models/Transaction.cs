#nullable enable
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WileyWidget.Models.Entities;

namespace WileyWidget.Models;

/// <summary>
/// Represents a financial transaction against a budget entry
/// </summary>
/// <summary>
/// Represents a class for transaction.
/// </summary>
public class Transaction : IAuditable
{
    /// <summary>
    /// Unique identifier for the transaction
    /// </summary>
    [Key]
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The budget entry this transaction belongs to
    /// </summary>
    [Required]
    /// <summary>
    /// Gets or sets the budgetentryid.
    /// </summary>
    public int BudgetEntryId { get; set; }
    [ForeignKey("BudgetEntryId")]
    /// <summary>
    /// Gets or sets the budgetentry.
    /// </summary>
    public BudgetEntry BudgetEntry { get; set; } = null!;

    /// <summary>
    /// Transaction amount
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    /// <summary>
    /// Gets or sets the amount.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Transaction description
    /// </summary>
    [Required]
    [StringLength(200)]
    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Transaction date
    /// </summary>
    [Required]
    /// <summary>
    /// Gets or sets the transactiondate.
    /// </summary>
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Transaction type
    /// </summary>
    [Required]
    [StringLength(50)]
    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    public string Type { get; set; } = string.Empty; // e.g., "Payment", "Adjustment"

    /// <summary>
    /// QuickBooks invoice ID for syncing
    /// </summary>
    [StringLength(50)]
    public string? QuickBooksInvoiceId { get; set; }

    /// <summary>
    /// QuickBooks entity ID
    /// </summary>
    [StringLength(50)]
    public string? QuickBooksId { get; set; }

    // Auditing
    /// <summary>
    /// Gets or sets the createdat.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

/// <summary>
/// Transaction types
/// </summary>
/// <summary>
/// Defines the transactiontype enumeration.
/// </summary>
public enum TransactionType
{
    Debit,
    Credit
}
