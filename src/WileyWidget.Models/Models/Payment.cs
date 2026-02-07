#nullable enable
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WileyWidget.Models.Entities;

namespace WileyWidget.Models;

/// <summary>
/// Represents a payment/check disbursement (check register entry)
/// </summary>
public class Payment : IAuditable
{
    /// <summary>
    /// Unique identifier for the payment
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Check number
    /// </summary>
    [Required]
    [StringLength(20)]
    public string CheckNumber { get; set; } = string.Empty;

    /// <summary>
    /// Payment date
    /// </summary>
    [Required]
    public DateTime PaymentDate { get; set; } = DateTime.Now;

    /// <summary>
    /// Payee name (e.g., "Amanda Brown", "Acme Supplies")
    /// </summary>
    [Required]
    [StringLength(200)]
    public string Payee { get; set; } = string.Empty;

    /// <summary>
    /// Payment amount
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    /// <summary>
    /// Description/purpose of payment (e.g., "Budget / Accountant")
    /// </summary>
    [Required]
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Optional reference to municipal account this payment affects
    /// </summary>
    public int? MunicipalAccountId { get; set; }
    [ForeignKey("MunicipalAccountId")]
    public MunicipalAccount? MunicipalAccount { get; set; }

    /// <summary>
    /// Optional reference to vendor if payment is to a vendor
    /// </summary>
    public int? VendorId { get; set; }
    [ForeignKey("VendorId")]
    public Vendor? Vendor { get; set; }

    /// <summary>
    /// Optional reference to invoice if payment is for an invoice
    /// </summary>
    public int? InvoiceId { get; set; }
    [ForeignKey("InvoiceId")]
    public Invoice? Invoice { get; set; }

    /// <summary>
    /// Payment status
    /// </summary>
    [Required]
    [StringLength(50)]
    public string Status { get; set; } = "Pending"; // Cleared, Void, Pending

    /// <summary>
    /// Whether the check has cleared the bank
    /// </summary>
    public bool IsCleared { get; set; } = false;

    /// <summary>
    /// Memo/notes
    /// </summary>
    [StringLength(1000)]
    public string? Memo { get; set; }

    // Auditing
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Payment status types
/// </summary>
public enum PaymentStatus
{
    Cleared,
    Void,
    Pending,
    Cancelled
}
