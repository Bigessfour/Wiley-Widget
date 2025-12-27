#nullable enable

using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WileyWidget.Data;
using WileyWidget.Services;
using Xunit;

namespace WileyWidget.Services.Tests.ServiceTests;

public sealed class DataSeedingServiceTests : IDisposable
{
    private readonly DbContextOptions<AppDbContext> _options;

    public DataSeedingServiceTests()
    {
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
    }

    public void Dispose()
    {
        // nothing to dispose - DB exists in memory and is ephemeral per test
    }

    [Fact]
    public async Task SeedBudgetDataAsync_PopulatesDatabase_WhenEmpty()
    {
        await using var ctx = new AppDbContext(_options);
        var svc = new DataSeedingService(ctx, NullLogger<DataSeedingService>.Instance);

        var result = await svc.SeedBudgetDataAsync();

        // The seeding may be skipped if model-level HasData already populated the DB (e.g., migrations seed data).
        // Accept either: new records inserted OR existing records present.
        (result.InsertedRecords > 0 || result.ExistingRecords > 0).Should().BeTrue();
        (await ctx.BudgetEntries.CountAsync()).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SeedBudgetDataAsync_IsIdempotent()
    {
        await using var ctx = new AppDbContext(_options);
        var svc = new DataSeedingService(ctx, NullLogger<DataSeedingService>.Instance);

        var r1 = await svc.SeedBudgetDataAsync();
        var count1 = await ctx.BudgetEntries.CountAsync();

        var r2 = await svc.SeedBudgetDataAsync();
        var count2 = await ctx.BudgetEntries.CountAsync();

        count2.Should().Be(count1);
        r2.InsertedRecords.Should().Be(0);
    }
}
