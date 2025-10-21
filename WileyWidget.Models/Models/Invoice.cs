#nullable enable
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WileyWidget.Models;

/// <summary>
/// Represents an invoice from a vendor
/// </summary>
public class Invoice
{
    /// <summary>
    /// Unique identifier for the invoice
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// The vendor this invoice is from
    /// </summary>
    [Required]
    public int VendorId { get; set; }
    public Vendor? Vendor { get; set; }

    /// <summary>
    /// The municipal account this invoice is charged to
    /// </summary>
    [Required]
    public int MunicipalAccountId { get; set; }
    public MunicipalAccount? MunicipalAccount { get; set; }

    /// <summary>
    /// Invoice number
    /// </summary>
    [Required]
    [StringLength(50)]
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>
    /// Invoice amount
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    /// <summary>
    /// Invoice date
    /// </summary>
    [Required]
    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Due date
    /// </summary>
    public DateTime? DueDate { get; set; }

    /// <summary>
    /// Invoice status
    /// </summary>
    [Required]
    [StringLength(50)]
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// Whether the invoice is paid
    /// </summary>
    public bool IsPaid { get; set; } = false;

    /// <summary>
    /// Payment date
    /// </summary>
    public DateTime? PaymentDate { get; set; }
}