using System;

namespace WileyWidget.Models;

/// <summary>
/// Interface for entities that require auditing
/// </summary>
public interface IAuditable
{
    DateTime CreatedAt { get; set; }
    DateTime? UpdatedAt { get; set; }
}
