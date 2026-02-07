#nullable enable
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WileyWidget.Models;

/// <summary>
/// Represents a vendor/supplier for municipal transactions
/// </summary>
public class Vendor
{
    /// <summary>
    /// Unique identifier for the vendor
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Vendor name
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Vendor contact notes or primary contact name
    /// </summary>
    [StringLength(200)]
    public string? ContactInfo { get; set; }

    /// <summary>
    /// Vendor primary email address
    /// </summary>
    [StringLength(200)]
    public string? Email { get; set; }

    /// <summary>
    /// Vendor primary phone number
    /// </summary>
    [StringLength(50)]
    public string? Phone { get; set; }

    /// <summary>
    /// Mailing address line 1
    /// </summary>
    [StringLength(200)]
    public string? MailingAddressLine1 { get; set; }

    /// <summary>
    /// Mailing address line 2
    /// </summary>
    [StringLength(200)]
    public string? MailingAddressLine2 { get; set; }

    /// <summary>
    /// Mailing address city
    /// </summary>
    [StringLength(100)]
    public string? MailingAddressCity { get; set; }

    /// <summary>
    /// Mailing address state or province
    /// </summary>
    [StringLength(50)]
    public string? MailingAddressState { get; set; }

    /// <summary>
    /// Mailing address postal or ZIP code
    /// </summary>
    [StringLength(20)]
    public string? MailingAddressPostalCode { get; set; }

    /// <summary>
    /// Mailing address country
    /// </summary>
    [StringLength(100)]
    public string? MailingAddressCountry { get; set; }

    /// <summary>
    /// QuickBooks vendor identifier for sync
    /// </summary>
    [StringLength(50)]
    public string? QuickBooksId { get; set; }

    /// <summary>
    /// Whether the vendor is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Navigation property for related invoices
    /// </summary>
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
