#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;

namespace WileyWidget.Data;

public class QuickBooksConflictRepository : IQuickBooksConflictRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public QuickBooksConflictRepository(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task AddAsync(QuickBooksSyncConflict conflict)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.QuickBooksSyncConflicts.Add(conflict);
        await context.SaveChangesAsync();
    }

    public async Task<IEnumerable<QuickBooksSyncConflict>> GetPendingAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.QuickBooksSyncConflicts.AsNoTracking().Where(c => c.Status == "Pending").ToListAsync();
    }

    public async Task UpdateAsync(QuickBooksSyncConflict conflict)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.QuickBooksSyncConflicts.Update(conflict);
        await context.SaveChangesAsync();
    }
}
