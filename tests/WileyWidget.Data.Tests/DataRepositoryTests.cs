using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using WileyWidget.Data;
using WileyWidget.Models;

namespace WileyWidget.Data.Tests;

public class DataRepositoryTests
{
    [Fact]
    public async Task CanInsertAndReadBasicEntity()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: "test_can_insert_and_read")
            .Options;

        await using var ctx = new AppDbContext(options);

        // arrange
        var entity = new Department { Name = "TestDept" };
        ctx.Add(entity);
        await ctx.SaveChangesAsync();

        // act
        var loaded = await ctx.Set<Department>().FirstOrDefaultAsync(d => d.Name == "TestDept");

        // assert
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("TestDept");
    }
}
