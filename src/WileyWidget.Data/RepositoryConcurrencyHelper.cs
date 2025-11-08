using WileyWidget.Models;
#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace WileyWidget.Data;

/// <summary>
/// Shared helper for repositories to surface optimistic concurrency conflicts with contextual information.
/// </summary>
internal static class RepositoryConcurrencyHelper
{
    public static async Task HandleAsync(DbUpdateConcurrencyException exception, string entityName)
    {
        EntityEntry? entry = exception.Entries.FirstOrDefault();
        IReadOnlyDictionary<string, object?>? databaseValues = null;
        IReadOnlyDictionary<string, object?>? clientValues = null;

        if (entry != null)
        {
            var dbValues = await entry.GetDatabaseValuesAsync().ConfigureAwait(false);
            databaseValues = ConcurrencyConflictException.ToDictionary(dbValues);
            clientValues = ConcurrencyConflictException.ToDictionary(entry.CurrentValues);
        }

        throw new ConcurrencyConflictException(entityName, databaseValues, clientValues, exception);
    }
}
