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
    /// Vendor contact information
    /// </summary>
    [StringLength(200)]
    public string? ContactInfo { get; set; }

    /// <summary>
    /// Whether the vendor is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Navigation property for related invoices
    /// </summary>
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}