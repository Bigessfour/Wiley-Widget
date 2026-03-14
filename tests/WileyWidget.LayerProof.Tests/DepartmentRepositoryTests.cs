using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.Data;
using WileyWidget.Models;

namespace WileyWidget.LayerProof.Tests;

public sealed class DepartmentRepositoryTests
{
    [Fact]
    public async Task GetAllAsync_ReturnsDepartmentsSortedByName()
    {
        var factory = CreateFactory();
        await SeedDepartmentsAsync(factory);
        using var cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 32 });
        var repository = new DepartmentRepository(factory, cache, Mock.Of<ILogger<DepartmentRepository>>());

        var departments = (await repository.GetAllAsync()).Select(department => department.Name).ToArray();

        departments.Should().Equal("Finance", "Sewer", "Water", "Water Billing", "Water Operations");
    }

    [Fact]
    public async Task GetChildDepartmentsAsync_ReturnsOnlyChildrenForRequestedParent()
    {
        var factory = CreateFactory();
        await SeedDepartmentsAsync(factory);
        using var cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 32 });
        var repository = new DepartmentRepository(factory, cache, Mock.Of<ILogger<DepartmentRepository>>());

        var children = (await repository.GetChildDepartmentsAsync(1)).Select(department => department.Name).ToArray();

        children.Should().Equal("Water Billing", "Water Operations");
    }

    private static TestDbContextFactory CreateFactory()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new TestDbContextFactory(options);
    }

    private static async Task SeedDepartmentsAsync(TestDbContextFactory factory)
    {
        await using var context = await factory.CreateDbContextAsync();
        context.Departments.AddRange(
            new Department { Id = 1, Name = "Water", DepartmentCode = "WTR" },
            new Department { Id = 2, Name = "Finance", DepartmentCode = "FIN" },
            new Department { Id = 3, Name = "Sewer", DepartmentCode = "SEW" },
            new Department { Id = 4, Name = "Water Operations", DepartmentCode = "WOP", ParentId = 1 },
            new Department { Id = 5, Name = "Water Billing", DepartmentCode = "WAT", ParentId = 1 });
        await context.SaveChangesAsync();
    }

    private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext()
        {
            return new AppDbContext(options);
        }

        public ValueTask<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(CreateDbContext());
        }
    }
}