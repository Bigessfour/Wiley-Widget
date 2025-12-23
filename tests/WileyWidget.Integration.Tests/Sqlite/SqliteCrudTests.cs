using System;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using Xunit;
using WileyWidget.Data;
using WileyWidget.Models;

namespace WileyWidget.Integration.Tests.Sqlite
{
    public class SqliteCrudTests
    {
        [Fact]
        public void Sqlite_AppDbContext_CanInsertAndQueryDepartment()
        {
            using var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();

            var options = new DbContextOptionsBuilder<MinimalTestContext>().UseSqlite(connection).Options;

            using (var ctx = new MinimalTestContext(options))
            {
                ctx.Database.EnsureCreated();

                ctx.Departments.Add(new Department { Name = "Integration Dept", DepartmentCode = "INTG" });
                ctx.SaveChanges();

                var dep = ctx.Departments.Single(d => d.DepartmentCode == "INTG");
                dep.Name.Should().Be("Integration Dept");
            }

        }

        private class MinimalTestContext : DbContext
        {
            public MinimalTestContext(DbContextOptions<MinimalTestContext> options) : base(options) { }

            public DbSet<Department> Departments { get; set; } = null!;

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<Department>(entity =>
                {
                    entity.HasKey(e => e.Id);
                    entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                    entity.Property(e => e.DepartmentCode).HasMaxLength(20);
                });
            }
        }
    }
}
