using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Respawn;
using Respawn.Graph;
using Testcontainers.MsSql;
using WileyWidget.Data;
using Xunit;

namespace WileyWidget.LayerProof.Tests.Data.E2E;

public class DatabaseFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("YourStrong@Passw0rd")
        .WithPortBinding(1433, true)
        .Build();

    public string ConnectionString => _container.GetConnectionString();
    private Respawner _respawner = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ConnectionString, o => o.EnableRetryOnFailure())
            .Options;

        await using var context = new AppDbContext(options);
        var seeder = new DatabaseSeeder(context);
        await seeder.SeedAsync();

        _respawner = await Respawner.CreateAsync(ConnectionString, new RespawnerOptions
        {
            TablesToIgnore = new[] { new Table("__EFMigrationsHistory") }
        });
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public async Task ResetDatabaseAsync() => await _respawner.ResetAsync(ConnectionString);
}
