using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;
using WileyWidget.Data;
using WileyWidget.Models;

namespace WileyWidget.Tests.Unit.Data
{
    /// <summary>
    /// Tests to validate EF model configuration for rowversion properties.
    /// Ensures SQL Server rowversion/Concurrency tokens are configured (IsRowVersion/OnAddOrUpdate) and not given defaults.
    /// </summary>
    public class AppDbContextRowVersionTests
    {
        [Fact]
        public void UtilityCustomer_RowVersion_IsConfiguredCorrectly()
        {
            AppDbContext.SkipModelSeedingInMemoryTests = true;
            var opts = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var ctx = new AppDbContext(opts);
            var entityType = ctx.Model.FindEntityType(typeof(UtilityCustomer));
            var prop = entityType?.FindProperty(nameof(UtilityCustomer.RowVersion));

            Assert.NotNull(prop);
            Assert.True(prop!.IsConcurrencyToken);
            Assert.Equal(ValueGenerated.OnAddOrUpdate, prop.ValueGenerated);

            // When configured with IsRowVersion and HasColumnType("rowversion") the relational annotation may be present
            var colType = prop.FindAnnotation("Relational:ColumnType")?.Value?.ToString();
            if (colType is not null)
            {
                Assert.Equal("rowversion", colType);
            }
        }

        [Fact]
        public void MunicipalAccount_RowVersion_IsConfiguredCorrectly()
        {
            AppDbContext.SkipModelSeedingInMemoryTests = true;
            var opts = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var ctx = new AppDbContext(opts);
            var entityType = ctx.Model.FindEntityType(typeof(MunicipalAccount));
            var prop = entityType?.FindProperty("RowVersion");

            Assert.NotNull(prop);
            Assert.True(prop!.IsConcurrencyToken);
            Assert.Equal(ValueGenerated.OnAddOrUpdate, prop.ValueGenerated);

            var colType = prop.FindAnnotation("Relational:ColumnType")?.Value?.ToString();
            if (colType is not null)
            {
                Assert.Equal("rowversion", colType);
            }
        }

        [Fact]
        public void UtilityBill_RowVersion_IsConfiguredCorrectly()
        {
            AppDbContext.SkipModelSeedingInMemoryTests = true;
            var opts = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var ctx = new AppDbContext(opts);
            var entityType = ctx.Model.FindEntityType(typeof(UtilityBill));
            var prop = entityType?.FindProperty("RowVersion");

            Assert.NotNull(prop);
            Assert.True(prop!.IsConcurrencyToken);
            Assert.Equal(ValueGenerated.OnAddOrUpdate, prop.ValueGenerated);

            var colType = prop.FindAnnotation("Relational:ColumnType")?.Value?.ToString();
            if (colType is not null)
            {
                Assert.Equal("rowversion", colType);
            }
        }
    }
}
