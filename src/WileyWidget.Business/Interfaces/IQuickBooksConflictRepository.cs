#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using WileyWidget.Models;

namespace WileyWidget.Business.Interfaces;

/// <summary>
/// Repository for storing and querying QuickBooks sync conflicts
/// </summary>
public interface IQuickBooksConflictRepository
{
    Task AddAsync(QuickBooksSyncConflict conflict);

    Task<IEnumerable<QuickBooksSyncConflict>> GetPendingAsync();

    Task UpdateAsync(QuickBooksSyncConflict conflict);
}
