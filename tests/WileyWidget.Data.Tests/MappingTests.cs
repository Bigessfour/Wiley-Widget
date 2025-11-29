using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WileyWidget.Data;
using WileyWidget.Models;

namespace WileyWidget.Data.Tests;

public class MappingTests
{
    private static AppDbContext CreateInMemoryContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public void Department_Has_Unique_Index_On_DepartmentCode()
    {
        using var ctx = CreateInMemoryContext("dept_unique_index");

        var entityType = ctx.Model.FindEntityType(typeof(Department))!;
        entityType.Should().NotBeNull();

        var indexes = entityType.GetIndexes();
        indexes.Should().Contain(i => i.IsUnique && i.Properties.Any(p => p.Name == "DepartmentCode"));
    }

    [Fact]
    public void BudgetEntry_Has_Unique_Index_On_AccountNumber_And_FiscalYear()
    {
        using var ctx = CreateInMemoryContext("budgetentry_unique_idx");
        var entityType = ctx.Model.FindEntityType(typeof(WileyWidget.Models.BudgetEntry))!;
        entityType.Should().NotBeNull();

        var indexes = entityType.GetIndexes();
        indexes.Should().Contain(i => i.IsUnique && i.Properties.Any(p => p.Name == "AccountNumber") && i.Properties.Any(p => p.Name == "FiscalYear"));
    }

    [Fact]
    public void MunicipalAccount_Has_Owned_AccountNumber()
    {
        using var ctx = CreateInMemoryContext("municipal_owned");
        var entityType = ctx.Model.FindEntityType(typeof(WileyWidget.Models.MunicipalAccount))!;
        entityType.Should().NotBeNull();

        var owned = ctx.Model.GetEntityTypes().Where(et => et.IsOwned()).Any(o => o.ClrType != null && o.ClrType.Name.Contains("AccountNumber", StringComparison.OrdinalIgnoreCase));
        owned.Should().BeTrue("MunicipalAccount should own the AccountNumber value object");
    }
}
