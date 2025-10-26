#nullable enable

using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace WileyWidget.Data
{
    /// <summary>
    /// Adapter that delegates to an inner IDbContextFactory to allow the DI container to hold
    /// a concrete instance for the interface registration.
    /// </summary>
    public sealed class AppDbContextFactoryAdapter : IDbContextFactory<AppDbContext>
    {
        private readonly IDbContextFactory<AppDbContext> _inner;

        public AppDbContextFactoryAdapter(IDbContextFactory<AppDbContext> inner)
        {
            _inner = inner ?? throw new System.ArgumentNullException(nameof(inner));
        }

        public AppDbContext CreateDbContext()
        {
            return _inner.CreateDbContext();
        }

        public async ValueTask<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            var ctx = await _inner.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            return ctx;
        }
    }
}
