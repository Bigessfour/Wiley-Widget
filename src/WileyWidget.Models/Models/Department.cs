#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WileyWidget.Models;

/// <summary>
/// Represents a municipal department with hierarchical support
/// </summary>
public class Department
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public int? ParentId { get; set; } // Nested departments
    [ForeignKey("ParentId")]
    public Department? Parent { get; set; }
    public ICollection<Department> Children { get; set; } = new List<Department>();

    public ICollection<BudgetEntry> BudgetEntries { get; set; } = new List<BudgetEntry>();
    // New: Department code for Excel mapping
    [MaxLength(20)]
    public string? DepartmentCode { get; set; } // e.g., "DPW" for Public Works
}
