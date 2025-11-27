#nullable enable

using System;
using Microsoft.EntityFrameworkCore;
using WileyWidget.Models;
using WileyWidget.Models.Entities;

namespace WileyWidget.Data
{
    public class AppDbContext : DbContext, IAppDbContext
    {
        private const string DecimalColumnType = "decimal(18,2)";

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
            // DbSets are now auto-initialized properties - no manual initialization needed
        }

        // Temporarily commented to fix syntax error
        // protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        // {
        //     if (configurationBuilder is null)
        //         throw new ArgumentNullException(nameof(configurationBuilder));
        //
        //     // Apply a global precision for decimal columns unless explicitly overridden
        //     configurationBuilder.Properties<decimal>().HavePrecision(19, 4);
        // }

        // OnConfiguring removed: All configuration is handled via DI in DatabaseConfiguration.cs
        // This prevents EF Core 9.0 ArgumentException: "At least one object must implement IComparable"
        // which occurs when trying to modify already-configured DbContextOptions

        public DbSet<MunicipalAccount> MunicipalAccounts { get; set; } = null!;
        public DbSet<Department> Departments { get; set; } = null!;
        public DbSet<BudgetEntry> BudgetEntries { get; set; } = null!;
        public DbSet<Fund> Funds { get; set; } = null!;
        public DbSet<Transaction> Transactions { get; set; } = null!;
        public DbSet<Enterprise> Enterprises { get; set; } = null!;
        public DbSet<AppSettings> AppSettings { get; set; } = null!;
        public DbSet<FiscalYearSettings> FiscalYearSettings { get; set; } = null!;
        public DbSet<UtilityCustomer> UtilityCustomers { get; set; } = null!;
        public DbSet<UtilityBill> UtilityBills { get; set; } = null!;
        public DbSet<Charge> Charges { get; set; } = null!;
        public DbSet<BudgetPeriod> BudgetPeriods { get; set; } = null!;
        public DbSet<Invoice> Invoices { get; set; } = null!;
        public DbSet<Vendor> Vendors { get; set; } = null!;
        public DbSet<AuditEntry> AuditEntries { get; set; } = null!;
        public DbSet<TaxRevenueSummary> TaxRevenueSummaries { get; set; } = null!;

        public IQueryable<BudgetEntry> GetBudgetHierarchy(int fiscalYear)
        {
            return BudgetEntries
                .Include(be => be.Parent)
                .Include(be => be.Children)
                .Include(be => be.Department)
                .Include(be => be.Fund)
                .Where(be => be.FiscalYear == fiscalYear)
                .AsNoTracking();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Explicitly configure BudgetInteraction relationships to remove EF ambiguity
            modelBuilder.Entity<BudgetInteraction>(entity =>
            {
                entity.HasOne(b => b.PrimaryEnterprise)
                      .WithMany(e => e.BudgetInteractions)
                      .HasForeignKey(b => b.PrimaryEnterpriseId)
                      .OnDelete(DeleteBehavior.Restrict);

                // The secondary enterprise is optional and does not have a collection on Enterprise,
                // so configure it without creating a second inverse collection.
                entity.HasOne(b => b.SecondaryEnterprise)
                      .WithMany()
                      .HasForeignKey(b => b.SecondaryEnterpriseId)
                      .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
