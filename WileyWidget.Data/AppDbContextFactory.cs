#nullable enable

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.IO;

namespace WileyWidget.Data;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? "Server=(localdb)\\mssqllocaldb;Database=WileyWidgetDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=true";

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlServer(connectionString);
        optionsBuilder.EnableDetailedErrors();
#pragma warning disable CA2000 // UseLoggerFactory takes ownership of the ILoggerFactory and disposes it when the DbContext is disposed
        optionsBuilder.UseLoggerFactory(LoggerFactory.Create(builder => builder.AddConsole()));
#pragma warning restore CA2000

        return new AppDbContext(optionsBuilder.Options);
    }
}
