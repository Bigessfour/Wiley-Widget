using Microsoft.EntityFrameworkCore;
using WileyWidget.Models;
using System;

namespace WileyWidget.Data
{
    /// <summary>
    /// Represents a interface for iappdbcontext.
    /// </summary>
    public interface IAppDbContext : IDisposable
    {
        DbSet<MunicipalAccount> MunicipalAccounts { get; }
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
