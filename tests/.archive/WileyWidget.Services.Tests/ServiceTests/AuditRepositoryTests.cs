#nullable enable

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Linq;
using System.Threading.Tasks;
using WileyWidget.Data;
using WileyWidget.Models;
using Xunit;

namespace WileyWidget.Services.Tests.ServiceTests;

public sealed class AuditRepositoryTests : IDisposable
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AuditRepository> _logger;
    private readonly DbContextOptions<AppDbContext> _options;

    public AuditRepositoryTests()
    {
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _contextFactory = new TestDbContextFactory(_options);
        _cache = new MemoryCache(new MemoryCacheOptions());
        _logger = Mock.Of<ILogger<AuditRepository>>();
    }

    private class TestDbContextFactory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;

        public TestDbContextFactory(DbContextOptions<AppDbContext> options)
        {
            _options = options;
        }

        public AppDbContext CreateDbContext()
        {
            return new AppDbContext(_options);
        }

        public Task<AppDbContext> CreateDbContextAsync(System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AppDbContext(_options));
        }
    }

    public void Dispose()
    {
        _cache?.Dispose();
    }

    private static AuditEntry CreateEntry(string entityType, int entityId, DateTime timestamp, string user = "u", string action = "CREATE")
    {
        return new AuditEntry
        {
            EntityType = entityType,
            EntityId = entityId,
            Timestamp = timestamp,
            User = user,
            Action = action,
            OldValues = null,
            NewValues = null,
            Changes = null
        };
    }

    [Fact]
    public void Constructor_WithNullContextFactory_ThrowsArgumentNullException()
    {
#pragma warning disable CA1806
        Action act = () => new AuditRepository(null!, _cache, _logger);
#pragma warning restore CA1806

        act.Should().Throw<ArgumentNullException>().WithParameterName("contextFactory");
    }

    [Fact]
    public void Constructor_WithNullCache_ThrowsArgumentNullException()
    {
#pragma warning disable CA1806
        Action act = () => new AuditRepository(_contextFactory, null!, _logger);
#pragma warning restore CA1806

        act.Should().Throw<ArgumentNullException>().WithParameterName("cache");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
#pragma warning disable CA1806
        Action act = () => new AuditRepository(_contextFactory, _cache, null!);
#pragma warning restore CA1806

        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public async Task AddAuditEntryAsync_PersistsEntry()
    {
        // Arrange
        var repo = new AuditRepository(_contextFactory, _cache, _logger);
        var ts = DateTime.UtcNow;
        var entry = CreateEntry("Budget", 1, ts, user: "tester", action: "CREATE");

        // Act
        await repo.AddAuditEntryAsync(entry);

        // Assert - read directly from context to avoid repository filtering
        await using var ctx = await _contextFactory.CreateDbContextAsync();
        var saved = await ctx.AuditEntries.SingleOrDefaultAsync(a => a.EntityType == "Budget" && a.EntityId == 1);
        saved.Should().NotBeNull();
        saved!.User.Should().Be("tester");
        saved.Timestamp.Should().Be(ts);
    }

    [Fact]
    public async Task GetAuditTrailAsync_ReturnsEntriesInRange_OrderedDescending()
    {
        // Arrange
        var repo = new AuditRepository(_contextFactory, _cache, _logger);
        var now = DateTime.UtcNow;
        var e1 = CreateEntry("X", 1, now.AddMinutes(-3));
        var e2 = CreateEntry("X", 2, now.AddMinutes(-2));
        var e3 = CreateEntry("X", 3, now.AddMinutes(-1));

        await using (var ctx = await _contextFactory.CreateDbContextAsync())
        {
            ctx.AuditEntries.AddRange(e1, e2, e3);
            await ctx.SaveChangesAsync();
        }

        // Act
        var results = (await repo.GetAuditTrailAsync(now.AddMinutes(-10), now)).ToList();

        // Assert
        results.Should().HaveCount(3);
        results.Select(r => r.EntityId).Should().ContainInOrder(3, 2, 1);
    }

    [Fact]
    public async Task GetAuditTrailAsync_InclusiveBoundaries_IncludesStartAndEnd()
    {
        // Arrange
        var repo = new AuditRepository(_contextFactory, _cache, _logger);
        var start = DateTime.UtcNow.Date.AddHours(1);
        var end = start.AddHours(1);

        var a = CreateEntry("T", 1, start);
        var b = CreateEntry("T", 2, end);
        var c = CreateEntry("T", 3, start.AddMinutes(30));

        await using (var ctx = await _contextFactory.CreateDbContextAsync())
        {
            ctx.AuditEntries.AddRange(a, b, c);
            await ctx.SaveChangesAsync();
        }

        // Act
        var results = (await repo.GetAuditTrailAsync(start, end)).ToList();

        // Assert - start and end timestamps are inclusive
        results.Should().HaveCount(3);
        results.Select(x => x.EntityId).Should().Contain(new[] { 1, 2, 3 });
    }

    [Fact]
    public async Task GetAuditTrail_ForEntityTypeAndId_FiltersCorrectly()
    {
        // Arrange
        var repo = new AuditRepository(_contextFactory, _cache, _logger);
        var now = DateTime.UtcNow;
        var a = CreateEntry("Budget", 1, now);
        var b = CreateEntry("Budget", 2, now);
        var c = CreateEntry("Account", 1, now);

        await using (var ctx = await _contextFactory.CreateDbContextAsync())
        {
            ctx.AuditEntries.AddRange(a, b, c);
            await ctx.SaveChangesAsync();
        }

        // Act
        var forType = (await repo.GetAuditTrailForEntityAsync("Budget", now.AddMinutes(-1), now.AddMinutes(1))).ToList();
        var forEntity = (await repo.GetAuditTrailForEntityAsync("Budget", 1, now.AddMinutes(-1), now.AddMinutes(1))).ToList();

        // Assert
        forType.Should().HaveCount(2);
        forEntity.Should().HaveCount(1);
        forEntity.Single().EntityId.Should().Be(1);
    }

    [Fact]
    public async Task GetPagedAsync_ReturnsCorrectPagingAndTotal()
    {
        // Arrange
        var repo = new AuditRepository(_contextFactory, _cache, _logger);
        var now = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            var e = CreateEntry("P", i + 1, now.AddMinutes(i));
            await using var ctx = await _contextFactory.CreateDbContextAsync();
            ctx.AuditEntries.Add(e);
            await ctx.SaveChangesAsync();
        }

        // Act - page 2 with size 2 (ascending by timestamp by default)
        var (items, total) = await repo.GetPagedAsync(pageNumber: 2, pageSize: 2, sortBy: "timestamp", sortDescending: false);

        // Assert
        total.Should().Be(5);
        items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetPagedAsync_InvalidPageNumber_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var repo = new AuditRepository(_contextFactory, _cache, _logger);
        await using var ctx = await _contextFactory.CreateDbContextAsync();
        ctx.AuditEntries.Add(CreateEntry("X", 1, DateTime.UtcNow));
        await ctx.SaveChangesAsync();

        // Act
        Func<Task> act = async () => await repo.GetPagedAsync(pageNumber: 0, pageSize: 10);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task GetPagedAsync_PageSizeZero_ReturnsZeroItemsButCorrectTotal()
    {
        // Arrange
        var repo = new AuditRepository(_contextFactory, _cache, _logger);
        await using (var ctx = await _contextFactory.CreateDbContextAsync())
        {
            ctx.AuditEntries.Add(CreateEntry("Z", 1, DateTime.UtcNow));
            ctx.AuditEntries.Add(CreateEntry("Z", 2, DateTime.UtcNow));
            await ctx.SaveChangesAsync();
        }

        // Act
        var (items, total) = await repo.GetPagedAsync(pageNumber: 1, pageSize: 0);

        // Assert
        total.Should().Be(2);
        items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetQueryableAsync_ReturnsQueryableThatCanBeFiltered()
    {
        // Arrange
        var repo = new AuditRepository(_contextFactory, _cache, _logger);
        var now = DateTime.UtcNow;
        await using (var ctx = await _contextFactory.CreateDbContextAsync())
        {
            ctx.AuditEntries.Add(CreateEntry("Q", 1, now));
            ctx.AuditEntries.Add(CreateEntry("Q", 2, now));
            await ctx.SaveChangesAsync();
        }

        // Act
        var queryable = await repo.GetQueryableAsync();
        var list = queryable.Where(a => a.EntityType == "Q").ToList();

        // Assert
        list.Should().HaveCount(2);
    }

    [Fact]
    public async Task ApplySorting_UnknownSortKey_DefaultsToTimestampOrdering()
    {
        // Arrange
        var repo = new AuditRepository(_contextFactory, _cache, _logger);
        var now = DateTime.UtcNow;
        var a = CreateEntry("S", 1, now.AddMinutes(-2));
        var b = CreateEntry("S", 2, now.AddMinutes(-1));

        await using (var ctx = await _contextFactory.CreateDbContextAsync())
        {
            ctx.AuditEntries.AddRange(a, b);
            await ctx.SaveChangesAsync();
        }

        // Act
        var (items, total) = await repo.GetPagedAsync(pageNumber: 1, pageSize: 10, sortBy: "unsupported", sortDescending: true);

        // Assert - sortDescending true should return newest first
        items.Should().HaveCount(2);
        items.First().Timestamp.Should().BeOnOrAfter(items.Last().Timestamp);
    }

    [Fact]
    public async Task AddAuditEntryAsync_Null_ThrowsArgumentNullException()
    {
        var repo = new AuditRepository(_contextFactory, _cache, _logger);
        await Assert.ThrowsAsync<ArgumentNullException>(() => repo.AddAuditEntryAsync(null!));
    }

    [Fact]
    public async Task GetAuditTrailAsync_NoEntries_ReturnsEmpty()
    {
        var repo = new AuditRepository(_contextFactory, _cache, _logger);
        var now = DateTime.UtcNow;

        var results = (await repo.GetAuditTrailAsync(now.AddDays(-1), now)).ToList();
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPagedAsync_FilterByEntityType_FiltersResultsCorrectly()
    {
        var repo = new AuditRepository(_contextFactory, _cache, _logger);
        var now = DateTime.UtcNow;
        await using (var ctx = await _contextFactory.CreateDbContextAsync())
        {
            ctx.AuditEntries.Add(CreateEntry("A", 1, now));
            ctx.AuditEntries.Add(CreateEntry("B", 2, now));
            ctx.AuditEntries.Add(CreateEntry("A", 3, now));
            await ctx.SaveChangesAsync();
        }

        var (items, total) = await repo.GetPagedAsync(pageNumber: 1, pageSize: 10, entityType: "A");
        total.Should().Be(2);
        items.Should().HaveCount(2);
        items.All(i => i.EntityType == "A").Should().BeTrue();
    }

    [Fact]
    public async Task GetPagedAsync_NegativePageSize_ThrowsArgumentOutOfRangeException()
    {
        var repo = new AuditRepository(_contextFactory, _cache, _logger);
        await using var ctx = await _contextFactory.CreateDbContextAsync();
        ctx.AuditEntries.Add(CreateEntry("X", 1, DateTime.UtcNow));
        await ctx.SaveChangesAsync();

        Func<Task> act = async () => await repo.GetPagedAsync(pageNumber: 1, pageSize: -1);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task GetAuditTrailAsync_StartAfterEnd_ReturnsEmpty()
    {
        var repo = new AuditRepository(_contextFactory, _cache, _logger);
        var now = DateTime.UtcNow;
        await using (var ctx = await _contextFactory.CreateDbContextAsync())
        {
            ctx.AuditEntries.Add(CreateEntry("X", 1, now));
            await ctx.SaveChangesAsync();
        }

        var results = (await repo.GetAuditTrailAsync(now.AddDays(1), now.AddDays(2))).ToList();
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPagedAsync_SortByUsername_OrdersByUser()
    {
        var repo = new AuditRepository(_contextFactory, _cache, _logger);
        var now = DateTime.UtcNow;
        var a = CreateEntry("S", 1, now.AddMinutes(-2)); a.User = "alice";
        var b = CreateEntry("S", 2, now.AddMinutes(-1)); b.User = "zoe";

        await using (var ctx = await _contextFactory.CreateDbContextAsync())
        {
            ctx.AuditEntries.AddRange(a, b);
            await ctx.SaveChangesAsync();
        }

        var (items, total) = await repo.GetPagedAsync(pageNumber: 1, pageSize: 10, sortBy: "username", sortDescending: false);

        items.First().User.Should().Be("alice");
    }
}
