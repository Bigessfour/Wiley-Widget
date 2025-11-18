using WileyWidget.Models;
#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace WileyWidget.Data;

/// <summary>
/// Exception raised when an optimistic concurrency conflict is detected during a repository operation.
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    /// <summary>
    /// The entity type involved in the conflict.
    /// </summary>
    public string EntityName { get; }

    /// <summary>
    /// Values currently stored in the database, if they could be loaded.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? DatabaseValues { get; }

    /// <summary>
    /// Values submitted by the caller when the conflict occurred.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? ClientValues { get; }

    public ConcurrencyConflictException(
        string entityName,
        IReadOnlyDictionary<string, object?>? databaseValues,
        IReadOnlyDictionary<string, object?>? clientValues,
        Exception innerException)
        : base($"The {entityName} was modified by another process. Reload the data and try again.", innerException)
    {
        EntityName = entityName;
        DatabaseValues = databaseValues;
        ClientValues = clientValues;
    }

    internal static IReadOnlyDictionary<string, object?>? ToDictionary(PropertyValues? values)
    {
        if (values == null)
        {
            return null;
        }

        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in values.Properties)
        {
            result[property.Name] = values[property];
        }
        return result;
    }
}
