using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WileyWidget.Data
{
    /// <summary>
    /// Design-time DbContext factory used by EF tools so they do not need to run the application's
    /// entry point (which may start UI or other long-running services). This factory prefers
    /// an environment variable `EF_MIGRATION_CONNECTION` and falls back to a LocalDB connection.
    /// </summary>
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var connectionString = Environment.GetEnvironmentVariable("EF_MIGRATION_CONNECTION")
                                   ?? "Server=(localdb)\\MSSQLLocalDB;Database=WileyWidget;Trusted_Connection=True;MultipleActiveResultSets=true";

            var builder = new DbContextOptionsBuilder<AppDbContext>();
            builder.UseSqlServer(connectionString, sql => sql.MigrationsAssembly("WileyWidget.Data"));

            return new AppDbContext(builder.Options);
        }
    }
}
