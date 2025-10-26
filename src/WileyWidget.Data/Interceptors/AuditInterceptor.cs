using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace WileyWidget.Data.Interceptors
{
    /// <summary>
    /// Lightweight EF Core SaveChanges interceptor that records metadata about changes for audit purposes.
    /// This implementation is intentionally minimal: it logs changed entries and avoids heavy dependencies.
    /// You can extend it to call IAuditService or persist AuditEntry entities as needed.
    /// </summary>
    public class AuditInterceptor : SaveChangesInterceptor
    {
        private readonly ILogger<AuditInterceptor> _logger;

        public AuditInterceptor(ILogger<AuditInterceptor> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            try
            {
                var context = eventData.Context;
                if (context != null)
                {
                    var entries = context.ChangeTracker.Entries()
                        .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted)
                        .ToList();

                    if (entries.Count > 0)
                    {
                        _logger.LogInformation("AuditInterceptor: {Count} pending changes detected before SaveChanges", entries.Count);
                        // Keep this lightweight — don't query services or do additional DB work here.
                    }
                }
            }
            catch (Exception ex)
            {
                try { _logger.LogWarning(ex, "AuditInterceptor encountered an error (non-fatal)"); } catch { }
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
