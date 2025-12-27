#nullable enable
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WileyWidget.Models;

/// <summary>
/// Represents an invoice from a vendor
/// </summary>
/// <summary>
/// Represents a class for invoice.
/// </summary>
public class Invoice
{
    /// <summary>
    /// Unique identifier for the invoice
    /// </summary>
    [Key]
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The vendor this invoice is from
    /// </summary>
    [Required]
    /// <summary>
    /// Gets or sets the vendorid.
    /// </summary>
    public int VendorId { get; set; }
    public Vendor? Vendor { get; set; }

    /// <summary>
    /// The municipal account this invoice is charged to
    /// </summary>
    [Required]
    /// <summary>
    /// Gets or sets the municipalaccountid.
    /// </summary>
    public int MunicipalAccountId { get; set; }
    public MunicipalAccount? MunicipalAccount { get; set; }

    /// <summary>
    /// Invoice number
    /// </summary>
    [Required]
    [StringLength(50)]
    /// <summary>
    /// Gets or sets the invoicenumber.
    /// </summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>
    /// Invoice amount
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    /// <summary>
    /// Gets or sets the amount.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Invoice date
    /// </summary>
    [Required]
    /// <summary>
    /// Gets or sets the invoicedate.
    /// </summary>
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
    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// Whether the invoice is paid
    /// </summary>
    /// <summary>
    /// Gets or sets the ispaid.
    /// </summary>
    public bool IsPaid { get; set; } = false;

    /// <summary>
    /// Payment date
    /// </summary>
    public DateTime? PaymentDate { get; set; }
}
