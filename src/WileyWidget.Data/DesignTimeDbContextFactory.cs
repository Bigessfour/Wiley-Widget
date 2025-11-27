using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System;

namespace WileyWidget.Data
{
    /// <summary>
    /// EF Core design-time factory so the `dotnet ef` tools can create AppDbContext.
    /// Uses the WW_DB_CONNECTION environment variable if set, otherwise falls back to the local sqlite file.
    /// </summary>
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

            var conn = Environment.GetEnvironmentVariable("WW_DB_CONNECTION") ?? "Data Source=wileywidget.db";

            optionsBuilder.UseSqlite(conn, sql => sql.MigrationsAssembly("WileyWidget.Data"));

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}