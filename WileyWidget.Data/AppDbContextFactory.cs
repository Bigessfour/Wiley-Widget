#nullable enable

using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace WileyWidget.Data
{
    /// <summary>
    /// Application DbContext factory. Provides IDbContextFactory&lt;AppDbContext&gt; using
    /// configured DbContextOptions. This is the single canonical factory used by the
    /// application.
    /// </summary>
    public sealed class AppDbContextFactory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;

        public AppDbContextFactory(DbContextOptions<AppDbContext> options)
        {
            _options = options ?? throw new System.ArgumentNullException(nameof(options));
        }

        public AppDbContext CreateDbContext()
        {
#pragma warning disable CA2000 // Factory method - caller is responsible for disposal
            return new AppDbContext(_options);
#pragma warning restore CA2000
        }

        public ValueTask<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
#pragma warning disable CA2000 // Factory method - caller is responsible for disposal
            return new ValueTask<AppDbContext>(new AppDbContext(_options));
#pragma warning restore CA2000
        }
    }
}
