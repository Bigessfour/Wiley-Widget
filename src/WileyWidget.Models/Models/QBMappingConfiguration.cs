#nullable enable
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WileyWidget.Models.Entities;

namespace WileyWidget.Models;

/// <summary>
/// Configuration for mapping QuickBooks entities to WileyWidget budget entries
/// </summary>
public class QBMappingConfiguration : IAuditable
{
    /// <summary>
    /// Unique identifier for the mapping configuration
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Type of QuickBooks entity being mapped (Class, Department, Customer, CustomField, etc.)
    /// </summary>
    [Required]
    [StringLength(50)]
    public string QBEntityType { get; set; } = string.Empty;

    /// <summary>
    /// QuickBooks entity ID (Class ID, Department ID, Customer ID, etc.)
    /// </summary>
    [Required]
    [StringLength(100)]
    public string QBEntityId { get; set; } = string.Empty;

    /// <summary>
    /// QuickBooks entity name for display purposes
    /// </summary>
    [StringLength(200)]
    public string? QBEntityName { get; set; }

    /// <summary>
    /// Mapped WileyWidget Budget Entry ID
    /// </summary>
    [Required]
    public int BudgetEntryId { get; set; }

    [ForeignKey("BudgetEntryId")]
    public BudgetEntry BudgetEntry { get; set; } = null!;

    /// <summary>
    /// Mapping strategy used (ClassRef, DepartmentRef, CustomField, CustomerRef, RuleBased)
    /// </summary>
    [Required]
    [StringLength(50)]
    public string MappingStrategy { get; set; } = string.Empty;

    /// <summary>
    /// Priority for rule-based mappings (higher numbers = higher priority)
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Is this mapping active?
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Optional description or notes for this mapping
    /// </summary>
    [StringLength(500)]
    public string? Description { get; set; }

    // Auditing
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

/// <summary>
/// QuickBooks entity types for mapping
/// </summary>
public enum QBEntityType
{
    Class,
    Department,
    Customer,
    Vendor,
    CustomField,
    Item,
    Project
}

/// <summary>
/// Mapping strategies
/// </summary>
public enum MappingStrategy
{
    ClassRef,
    DepartmentRef,
    CustomerRef,
    VendorRef,
    CustomField,
    ItemRef,
    ProjectRef,
    RuleBased
}
