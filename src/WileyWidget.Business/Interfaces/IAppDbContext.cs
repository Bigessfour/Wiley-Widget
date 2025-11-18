using Microsoft.EntityFrameworkCore;
using WileyWidget.Models;
using System;

namespace WileyWidget.Data
{
    public interface IAppDbContext : IDisposable
    {
        DbSet<MunicipalAccount> MunicipalAccounts { get; }
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}