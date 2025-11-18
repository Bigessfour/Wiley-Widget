using WileyWidget.Models;
#nullable enable
using System;

namespace WileyWidget.Models;

/// <summary>
/// Interface for entities that support soft delete (retain data for audit/compliance)
/// Municipal data often requires retention for historical/legal purposes
/// </summary>
public interface ISoftDeletable
{
    /// <summary>
    /// Whether the entity has been soft-deleted
    /// </summary>
    bool IsDeleted { get; set; }

    /// <summary>
    /// Date and time when the entity was soft-deleted (UTC)
    /// </summary>
    DateTime? DeletedDate { get; set; }

    /// <summary>
    /// User who soft-deleted the entity
    /// </summary>
    string? DeletedBy { get; set; }
}
