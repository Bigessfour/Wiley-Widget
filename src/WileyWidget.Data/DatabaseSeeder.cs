using System.Threading;
using Microsoft.EntityFrameworkCore;
using WileyWidget.Models;

namespace WileyWidget.Data
{
    public class DatabaseSeeder
    {
        private readonly AppDbContext _context;

        public DatabaseSeeder(AppDbContext context)
        {
            _context = context;
        }

        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            // Database seeding is handled via EF Core HasData in AppDbContext.OnModelCreating.
            // MigrateAsync applies pending migrations (and their embedded seed data) idempotently.
            // EnsureCreatedAsync was previously used here but is incompatible with migrations.
            await _context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
