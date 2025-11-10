#nullable enable

using System;
using Microsoft.EntityFrameworkCore;
using WileyWidget.Models;
using WileyWidget.Models.Entities;

namespace WileyWidget.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
            // DbSets are now auto-initialized properties - no manual initialization needed
        }

        // Global conventions
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            if (configurationBuilder is null)
                throw new ArgumentNullException(nameof(configurationBuilder));

            // Apply a global precision for decimal columns unless explicitly overridden
            configurationBuilder.Properties<decimal>().HavePrecision(19, 4);
        }

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        if (modelBuilder is null)
        {
            throw new ArgumentNullException(nameof(modelBuilder));
        }

        base.OnModelCreating(modelBuilder);

        // BudgetEntry (updated)
        modelBuilder.Entity<BudgetEntry>(entity =>
        {
            entity.HasOne(e => e.Parent)
                  .WithMany(e => e.Children)
                  .HasForeignKey(e => e.ParentId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.ParentId);
            entity.HasIndex(e => new { e.AccountNumber, e.FiscalYear }).IsUnique();
            entity.HasIndex(e => e.SourceRowNumber); // New: Excel import queries
            entity.HasIndex(e => e.ActivityCode); // New: GASB reporting
            entity.Property(e => e.BudgetedAmount).HasColumnType("decimal(18,2)").HasDefaultValue(0);
            entity.Property(e => e.ActualAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.EncumbranceAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.SourceFilePath).HasMaxLength(500);
            entity.Property(e => e.ActivityCode).HasMaxLength(10);
            entity.ToTable(t => t.HasCheckConstraint("CK_Budget_Positive", "[BudgetedAmount] > 0"));
        });

        // Department hierarchy
        modelBuilder.Entity<Department>(entity =>
        {
            entity.HasOne(e => e.Parent)
                  .WithMany(e => e.Children)
                  .HasForeignKey(e => e.ParentId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.DepartmentCode).IsUnique(); // New: Unique code
        });

        // BudgetPeriod configuration
        modelBuilder.Entity<BudgetPeriod>(entity =>
        {
            entity.HasIndex(e => e.Year);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => new { e.Year, e.Status });
        });

        // MunicipalAccount configuration
        modelBuilder.Entity<MunicipalAccount>(entity =>
        {
            entity.OwnsOne(e => e.AccountNumber, owned =>
            {
                owned.Property(a => a.Value).HasColumnName("AccountNumber").HasMaxLength(20).IsRequired();
            });
            entity.Property(e => e.AccountNumber_Value)
                  .HasComputedColumnSql("[AccountNumber]");
            entity.HasOne(e => e.Department)
                  .WithMany()
                  .HasForeignKey(e => e.DepartmentId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.BudgetPeriod)
                  .WithMany(bp => bp.Accounts)
                  .HasForeignKey(e => e.BudgetPeriodId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ParentAccount)
                  .WithMany(e => e.ChildAccounts)
                  .HasForeignKey(e => e.ParentAccountId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.DepartmentId);
            entity.HasIndex(e => e.BudgetPeriodId);
            entity.HasIndex(e => e.ParentAccountId);
            entity.HasIndex(e => new { e.Fund, e.Type });
            entity.Property(e => e.Balance).HasColumnType("decimal(18,2)");
            entity.Property(e => e.BudgetAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.RowVersion)
                  .IsRowVersion();
        });

        // Fund relations
        modelBuilder.Entity<Fund>(entity =>
        {
            entity.HasMany(f => f.BudgetEntries)
                  .WithOne(be => be.Fund)
                  .HasForeignKey(be => be.FundId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // New: Transaction
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasOne(t => t.BudgetEntry)
                  .WithMany(be => be.Transactions)
                  .HasForeignKey(t => t.BudgetEntryId)
            .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(t => t.TransactionDate);
            entity.Property(t => t.Amount).HasColumnType("decimal(18,2)");
            entity.Property(t => t.Type).HasMaxLength(50);
            entity.Property(t => t.Description).HasMaxLength(200);
            entity.ToTable(t => t.HasCheckConstraint("CK_Transaction_NonZero", "[Amount] != 0"));
        });

        // New: Invoice
        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasOne(i => i.Vendor)
                  .WithMany(v => v.Invoices)
                  .HasForeignKey(i => i.VendorId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(i => i.MunicipalAccount)
                  .WithMany(ma => ma.Invoices)
                  .HasForeignKey(i => i.MunicipalAccountId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(i => i.VendorId);
            entity.HasIndex(i => i.MunicipalAccountId);
            entity.HasIndex(i => i.InvoiceDate);
            entity.Property(i => i.Amount).HasColumnType("decimal(18,2)");
            entity.Property(i => i.InvoiceNumber).HasMaxLength(50);
            entity.Property(i => i.Status).HasMaxLength(50).HasDefaultValue("Pending");
        });

        // New: Vendor configuration
        modelBuilder.Entity<Vendor>(entity =>
        {
            entity.ToTable("Vendor"); // Match existing database table name
            entity.HasIndex(v => v.Name);
            entity.HasIndex(v => v.IsActive);
            entity.Property(v => v.Name).HasMaxLength(100).IsRequired();
            entity.Property(v => v.ContactInfo).HasMaxLength(200);
        });

        // Precision for TaxRevenueSummary decimal columns to prevent truncation/rounding issues
        modelBuilder.Entity<TaxRevenueSummary>(entity =>
        {
            entity.Property(e => e.PriorYearLevy).HasPrecision(19, 4);
            entity.Property(e => e.PriorYearAmount).HasPrecision(19, 4);
            entity.Property(e => e.CurrentYearLevy).HasPrecision(19, 4);
            entity.Property(e => e.CurrentYearAmount).HasPrecision(19, 4);
            entity.Property(e => e.BudgetYearLevy).HasPrecision(19, 4);
            entity.Property(e => e.BudgetYearAmount).HasPrecision(19, 4);
            entity.Property(e => e.IncDecLevy).HasPrecision(19, 4);
            entity.Property(e => e.IncDecAmount).HasPrecision(19, 4);
        });

        // New: BudgetInteraction relationships
        modelBuilder.Entity<BudgetInteraction>(entity =>
        {
            entity.HasOne(bi => bi.PrimaryEnterprise)
                  .WithMany()
                  .HasForeignKey(bi => bi.PrimaryEnterpriseId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(bi => bi.SecondaryEnterprise)
                  .WithMany()
                  .HasForeignKey(bi => bi.SecondaryEnterpriseId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(bi => bi.PrimaryEnterpriseId);
            entity.HasIndex(bi => bi.SecondaryEnterpriseId);
            entity.Property(bi => bi.MonthlyAmount).HasColumnType("decimal(18,2)");
            entity.Property(bi => bi.InteractionType).HasMaxLength(50);
            entity.Property(bi => bi.Description).HasMaxLength(200);
            entity.Property(bi => bi.Notes).HasMaxLength(300);
        });

        // UtilityBill configuration
        modelBuilder.Entity<UtilityBill>(entity =>
        {
            entity.HasKey(ub => ub.Id);
            entity.HasOne(ub => ub.Customer)
                  .WithMany()
                  .HasForeignKey(ub => ub.CustomerId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(ub => ub.CustomerId);
            entity.HasIndex(ub => ub.BillNumber).IsUnique();
            entity.HasIndex(ub => ub.BillDate);
            entity.HasIndex(ub => ub.DueDate);
            entity.HasIndex(ub => ub.Status);
            entity.Property(ub => ub.BillNumber).HasMaxLength(50).IsRequired();
            entity.Property(ub => ub.WaterCharges).HasColumnType("decimal(18,2)").HasDefaultValue(0);
            entity.Property(ub => ub.SewerCharges).HasColumnType("decimal(18,2)").HasDefaultValue(0);
            entity.Property(ub => ub.GarbageCharges).HasColumnType("decimal(18,2)").HasDefaultValue(0);
            entity.Property(ub => ub.StormwaterCharges).HasColumnType("decimal(18,2)").HasDefaultValue(0);
            entity.Property(ub => ub.LateFees).HasColumnType("decimal(18,2)").HasDefaultValue(0);
            entity.Property(ub => ub.OtherCharges).HasColumnType("decimal(18,2)").HasDefaultValue(0);
            entity.Property(ub => ub.AmountPaid).HasColumnType("decimal(18,2)").HasDefaultValue(0);
        });

        // Charge configuration
        modelBuilder.Entity<Charge>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.HasOne(c => c.Bill)
                  .WithMany()
                  .HasForeignKey(c => c.BillId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(c => c.BillId);
            entity.HasIndex(c => c.ChargeType);
            entity.Property(c => c.ChargeType).HasMaxLength(50).IsRequired();
            entity.Property(c => c.Description).HasMaxLength(200).IsRequired();
            entity.Property(c => c.Amount).HasColumnType("decimal(18,2)").IsRequired();
        });

        // Auditing
        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
            .Where(e => typeof(WileyWidget.Models.Entities.IAuditable).IsAssignableFrom(e.ClrType)))
        {
            modelBuilder.Entity(entityType.ClrType).Property("CreatedAt").HasDefaultValueSql("GETUTCDATE()");
            modelBuilder.Entity(entityType.ClrType).Property("UpdatedAt").ValueGeneratedOnAddOrUpdate();
        }

        // Set all foreign keys to Restrict to avoid SQL Server cascade path issues
        foreach (var relationship in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            relationship.DeleteBehavior = DeleteBehavior.Restrict;
        }

        // Override restrict for cascade deletes on MunicipalAccount relationships
        modelBuilder.Entity<MunicipalAccount>()
            .HasOne(ma => ma.ParentAccount)
            .WithMany(pa => pa.ChildAccounts)
            .HasForeignKey(ma => ma.ParentAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Invoice>()
            .HasOne(i => i.MunicipalAccount)
            .WithMany(ma => ma.Invoices)
            .HasForeignKey(i => i.MunicipalAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        // Seed: BudgetPeriod required for MunicipalAccount FK
        modelBuilder.Entity<BudgetPeriod>().HasData(
            new BudgetPeriod
            {
                Id = 1,
                Year = 2025,
                Name = "2025 Adopted",
                CreatedDate = new DateTime(2025, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                Status = BudgetStatus.Adopted,
                StartDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2025, 12, 31, 23, 59, 59, DateTimeKind.Utc),
                IsActive = true
            }
        );

        // Seed: Departments already have Id=1 (Public Works) from prior migration

        // Seed: Core Departments (ensure ids exist for FK references)
        modelBuilder.Entity<Department>().HasData(
            new Department { Id = 1, Name = "Administration", DepartmentCode = "ADMIN" },
            new Department { Id = 2, Name = "Public Works", DepartmentCode = "DPW" },
            new Department { Id = 3, Name = "Culture and Recreation", DepartmentCode = "CULT" },
            new Department { Id = 4, Name = "Sanitation", DepartmentCode = "SAN", ParentId = 2 },
            new Department { Id = 5, Name = "Utilities", DepartmentCode = "UTIL" },
            new Department { Id = 6, Name = "Community Center", DepartmentCode = "COMM" },
            new Department { Id = 7, Name = "Conservation", DepartmentCode = "CONS" },
            new Department { Id = 8, Name = "Recreation", DepartmentCode = "REC" }
        );

        // Seed: Funds (lookup)
        modelBuilder.Entity<Fund>().HasData(
            new Fund { Id = 1, FundCode = "100-GEN", Name = "General Fund", Type = FundType.GeneralFund },
            new Fund { Id = 2, FundCode = "200-ENT", Name = "Enterprise Fund", Type = FundType.EnterpriseFund },
            new Fund { Id = 3, FundCode = "300-UTIL", Name = "Utility Fund", Type = FundType.EnterpriseFund },
            new Fund { Id = 4, FundCode = "400-COMM", Name = "Community Center Fund", Type = FundType.SpecialRevenue },
            new Fund { Id = 5, FundCode = "500-CONS", Name = "Conservation Trust Fund", Type = FundType.PermanentFund },
            new Fund { Id = 6, FundCode = "600-REC", Name = "Recreation Fund", Type = FundType.SpecialRevenue }
        );

        // Seed: A few common vendors to make invoices and testing easier
        modelBuilder.Entity<Vendor>().HasData(
            new Vendor { Id = 1, Name = "Acme Supplies", ContactInfo = "contact@acmesupplies.example.com", IsActive = true },
            new Vendor { Id = 2, Name = "Municipal Services Co.", ContactInfo = "info@muniservices.example.com", IsActive = true },
            new Vendor { Id = 3, Name = "Trail Builders LLC", ContactInfo = "projects@trailbuilders.example.com", IsActive = true }
        );

        // Seed: FY 26 Proposed Budget - Property Tax Revenues Summary
        modelBuilder.Entity<TaxRevenueSummary>().HasData(
            new TaxRevenueSummary { Id = 1, Description = "ASSESSED VALUATION-COUNTY FUND", PriorYearLevy = 1069780m, PriorYearAmount = 1069780m, CurrentYearLevy = 1072691m, CurrentYearAmount = 1072691m, BudgetYearLevy = 1880448m, BudgetYearAmount = 1880448m, IncDecLevy = 807757m, IncDecAmount = 807757m },
            new TaxRevenueSummary { Id = 2, Description = "GENERAL", PriorYearLevy = 45.570m, PriorYearAmount = 48750m, CurrentYearLevy = 45.570m, CurrentYearAmount = 48883m, BudgetYearLevy = 45.570m, BudgetYearAmount = 85692m, IncDecLevy = 0m, IncDecAmount = 36809m },
            new TaxRevenueSummary { Id = 3, Description = "UTILITY", PriorYearLevy = 0m, PriorYearAmount = 0m, CurrentYearLevy = 0m, CurrentYearAmount = 0m, BudgetYearLevy = 0m, BudgetYearAmount = 0m, IncDecLevy = 0m, IncDecAmount = 0m },
            new TaxRevenueSummary { Id = 4, Description = "COMMUNITY CENTER", PriorYearLevy = 0m, PriorYearAmount = 0m, CurrentYearLevy = 0m, CurrentYearAmount = 0m, BudgetYearLevy = 0m, BudgetYearAmount = 0m, IncDecLevy = 0m, IncDecAmount = 0m },
            new TaxRevenueSummary { Id = 5, Description = "CONSERVATION TRUST FUND", PriorYearLevy = 0m, PriorYearAmount = 0m, CurrentYearLevy = 0m, CurrentYearAmount = 0m, BudgetYearLevy = 0m, BudgetYearAmount = 0m, IncDecLevy = 0m, IncDecAmount = 0m },
            new TaxRevenueSummary { Id = 6, Description = "TEMPORARY MILL LEVY CREDIT", PriorYearLevy = 0m, PriorYearAmount = 0m, CurrentYearLevy = 0m, CurrentYearAmount = 0m, BudgetYearLevy = 0m, BudgetYearAmount = 0m, IncDecLevy = 0m, IncDecAmount = 0m },
            new TaxRevenueSummary { Id = 7, Description = "TOTAL", PriorYearLevy = 45.570m, PriorYearAmount = 48750m, CurrentYearLevy = 45.570m, CurrentYearAmount = 48883m, BudgetYearLevy = 45.570m, BudgetYearAmount = 85692m, IncDecLevy = 0m, IncDecAmount = 36810m }
        );

        // Seed: BudgetPeriod for FY 2026
        modelBuilder.Entity<BudgetPeriod>().HasData(
            new BudgetPeriod
            {
                Id = 2,
                Year = 2026,
                Name = "2026 Proposed",
                CreatedDate = new DateTime(2025, 10, 28, 0, 0, 0, DateTimeKind.Utc),
                Status = BudgetStatus.Proposed,
                StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc),
                IsActive = false
            }
        );

        // Seed: FY 26 Proposed Budget - General Fund Revenues
        modelBuilder.Entity<BudgetEntry>().HasData(
            // Intergovernmental Revenue
            new BudgetEntry { Id = 1, AccountNumber = "332.1", Description = "Federal: Mineral Lease", FiscalYear = 2026, BudgetedAmount = 360m, DepartmentId = 1, FundId = 1, FundType = FundType.GeneralFund, IsGASBCompliant = true, CreatedAt = new DateTime(2025, 10, 28), UpdatedAt = new DateTime(2025, 10, 28) },
            new BudgetEntry { Id = 2, AccountNumber = "333.00", Description = "State: Cigarette Taxes", FiscalYear = 2026, BudgetedAmount = 240m, DepartmentId = 1, FundId = 1, FundType = FundType.GeneralFund, IsGASBCompliant = true, CreatedAt = new DateTime(2025, 10, 28), UpdatedAt = new DateTime(2025, 10, 28) },
            new BudgetEntry { Id = 3, AccountNumber = "334.31", Description = "Highways Users", FiscalYear = 2026, BudgetedAmount = 18153m, DepartmentId = 1, FundId = 1, FundType = FundType.GeneralFund, IsGASBCompliant = true, CreatedAt = new DateTime(2025, 10, 28), UpdatedAt = new DateTime(2025, 10, 28) },
            new BudgetEntry { Id = 4, AccountNumber = "313.00", Description = "Additional MV", FiscalYear = 2026, BudgetedAmount = 1775m, DepartmentId = 1, FundId = 1, FundType = FundType.GeneralFund, IsGASBCompliant = true, CreatedAt = new DateTime(2025, 10, 28), UpdatedAt = new DateTime(2025, 10, 28) },
            new BudgetEntry { Id = 5, AccountNumber = "337.17", Description = "County Road & Bridge", FiscalYear = 2026, BudgetedAmount = 1460m, DepartmentId = 1, FundId = 1, FundType = FundType.GeneralFund, IsGASBCompliant = true, CreatedAt = new DateTime(2025, 10, 28), UpdatedAt = new DateTime(2025, 10, 28) },
            // Other Revenue
            new BudgetEntry { Id = 6, AccountNumber = "311.20", Description = "Senior Homestead Exemption", FiscalYear = 2026, BudgetedAmount = 1500m, DepartmentId = 1, FundId = 1, FundType = FundType.GeneralFund, IsGASBCompliant = true, CreatedAt = new DateTime(2025, 10, 28), UpdatedAt = new DateTime(2025, 10, 28) },
            new BudgetEntry { Id = 7, AccountNumber = "312.00", Description = "Specific Ownership Taxes", FiscalYear = 2026, BudgetedAmount = 5100m, DepartmentId = 1, FundId = 1, FundType = FundType.GeneralFund, IsGASBCompliant = true, CreatedAt = new DateTime(2025, 10, 28), UpdatedAt = new DateTime(2025, 10, 28) },
            new BudgetEntry { Id = 8, AccountNumber = "314.00", Description = "Tax A", FiscalYear = 2026, BudgetedAmount = 2500m, DepartmentId = 1, FundId = 1, FundType = FundType.GeneralFund, IsGASBCompliant = true, CreatedAt = new DateTime(2025, 10, 28), UpdatedAt = new DateTime(2025, 10, 28) },
            new BudgetEntry { Id = 9, AccountNumber = "319.00", Description = "Penalties & Interest on Delinquent Taxes", FiscalYear = 2026, BudgetedAmount = 35m, DepartmentId = 1, FundId = 1, FundType = FundType.GeneralFund, IsGASBCompliant = true, CreatedAt = new DateTime(2025, 10, 28), UpdatedAt = new DateTime(2025, 10, 28) },
            new BudgetEntry { Id = 10, AccountNumber = "336.00", Description = "Sales Tax", FiscalYear = 2026, BudgetedAmount = 120000m, DepartmentId = 1, FundId = 1, FundType = FundType.GeneralFund, IsGASBCompliant = true, CreatedAt = new DateTime(2025, 10, 28), UpdatedAt = new DateTime(2025, 10, 28) },
            new BudgetEntry { Id = 11, AccountNumber = "318.20", Description = "Franchise Fee", FiscalYear = 2026, BudgetedAmount = 7058m, DepartmentId = 1, FundId = 1, FundType = FundType.GeneralFund, IsGASBCompliant = true, CreatedAt = new DateTime(2025, 10, 28), UpdatedAt = new DateTime(2025, 10, 28) },
            new BudgetEntry { Id = 12, AccountNumber = "322.70", Description = "Animal Licenses", FiscalYear = 2026, BudgetedAmount = 50m, DepartmentId = 1, FundId = 1, FundType = FundType.GeneralFund, IsGASBCompliant = true, CreatedAt = new DateTime(2025, 10, 28), UpdatedAt = new DateTime(2025, 10, 28) },
            new BudgetEntry { Id = 13, AccountNumber = "310.00", Description = "Charges for Services: WSD Collection Fee", FiscalYear = 2026, BudgetedAmount = 6000m, DepartmentId = 1, FundId = 1, FundType = FundType.GeneralFund, IsGASBCompliant = true, CreatedAt = new DateTime(2025, 10, 28), UpdatedAt = new DateTime(2025, 10, 28) },
            new BudgetEntry { Id = 14, AccountNumber = "370.00", Description = "Housing Authority Mgt Fee", FiscalYear = 2026, BudgetedAmount = 12000m, DepartmentId = 1, FundId = 1, FundType = FundType.GeneralFund, IsGASBCompliant = true, CreatedAt = new DateTime(2025, 10, 28), UpdatedAt = new DateTime(2025, 10, 28) },
            new BudgetEntry { Id = 15, AccountNumber = "373.00", Description = "Pickup Usage Fee", FiscalYear = 2026, BudgetedAmount = 2400m, DepartmentId = 1, FundId = 1, FundType = FundType.GeneralFund, IsGASBCompliant = true, CreatedAt = new DateTime(2025, 10, 28), UpdatedAt = new DateTime(2025, 10, 28) },
            new BudgetEntry { Id = 16, AccountNumber = "361.00", Description = "Miscellaneous Receipts: Interest Earnings", FiscalYear = 2026, BudgetedAmount = 325m, DepartmentId = 1, FundId = 1, FundType = FundType.GeneralFund, IsGASBCompliant = true, CreatedAt = new DateTime(2025, 10, 28), UpdatedAt = new DateTime(2025, 10, 28) },
            new BudgetEntry { Id = 17, AccountNumber = "365.00", Description = "Dividends", FiscalYear = 2026, BudgetedAmount = 100m, DepartmentId = 1, FundId = 1, FundType = FundType.GeneralFund, IsGASBCompliant = true, CreatedAt = new DateTime(2025, 10, 28), UpdatedAt = new DateTime(2025, 10, 28) },
            new BudgetEntry { Id = 18, AccountNumber = "363.00", Description = "Lease", FiscalYear = 2026, BudgetedAmount = 1100m, DepartmentId = 1, FundId = 1, FundType = FundType.GeneralFund, IsGASBCompliant = true, CreatedAt = new DateTime(2025, 10, 28), UpdatedAt = new DateTime(2025, 10, 28) },
            new BudgetEntry { Id = 19, AccountNumber = "350.00", Description = "Wiley Hay Days Donations", FiscalYear = 2026, BudgetedAmount = 10000m, DepartmentId = 1, FundId = 1, FundType = FundType.GeneralFund, IsGASBCompliant = true, CreatedAt = new DateTime(2025, 10, 28), UpdatedAt = new DateTime(2025, 10, 28) },
            new BudgetEntry { Id = 20, AccountNumber = "362.00", Description = "Donations", FiscalYear = 2026, BudgetedAmount = 2500m, DepartmentId = 1, FundId = 1, FundType = FundType.GeneralFund, IsGASBCompliant = true, CreatedAt = new DateTime(2025, 10, 28), UpdatedAt = new DateTime(2025, 10, 28) }
        );

        // Seed: Default AppSettings row so code that expects Id=1 has a baseline
        modelBuilder.Entity<AppSettings>().HasData(
            new AppSettings
            {
                Id = 1,
                Theme = "FluentDark",
                EnableDataCaching = true,
                CacheExpirationMinutes = 30,
                SelectedLogLevel = "Information",
                EnableFileLogging = true,
                LogFilePath = "logs/wiley-widget.log",
                QuickBooksEnvironment = "sandbox",
                QboTokenExpiry = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastSelectedEnterpriseId = 1
            }
        );

    // Seed: Conservation Trust Fund Chart of Accounts (MunicipalAccount)
        modelBuilder.Entity<MunicipalAccount>().HasData(
            // Assets / Cash & equivalents
            new MunicipalAccount { Id = 1,  Name = "CASH IN BANK",                         Type = AccountType.Cash,               Fund = MunicipalFundType.ConservationTrust, DepartmentId = 1, BudgetPeriodId = 1, IsActive = true },
            new MunicipalAccount { Id = 2,  Name = "CASH-BASEBALL FIELD PROJECT",         Type = AccountType.Cash,               Fund = MunicipalFundType.ConservationTrust, DepartmentId = 1, BudgetPeriodId = 1, IsActive = true },
            new MunicipalAccount { Id = 3,  Name = "INVESTMENTS",                         Type = AccountType.Investments,        Fund = MunicipalFundType.ConservationTrust, DepartmentId = 1, BudgetPeriodId = 1, IsActive = true },
            new MunicipalAccount { Id = 4,  Name = "INTERGOVERNMENTAL RECEIVABLE",        Type = AccountType.Receivables,        Fund = MunicipalFundType.ConservationTrust, DepartmentId = 1, BudgetPeriodId = 1, IsActive = true },
            new MunicipalAccount { Id = 5,  Name = "GRANT RECEIVABLE",                    Type = AccountType.Receivables,        Fund = MunicipalFundType.ConservationTrust, DepartmentId = 1, BudgetPeriodId = 1, IsActive = true },

            // Liabilities
            new MunicipalAccount { Id = 6,  Name = "ACCOUNTS PAYABLE",                    Type = AccountType.Payables,           Fund = MunicipalFundType.ConservationTrust, DepartmentId = 1, BudgetPeriodId = 1, IsActive = true },
            new MunicipalAccount { Id = 7,  Name = "BASEBALL FIELD PROJECT LOAN",         Type = AccountType.Debt,               Fund = MunicipalFundType.ConservationTrust, DepartmentId = 1, BudgetPeriodId = 1, IsActive = true },
            new MunicipalAccount { Id = 8,  Name = "WALKING TRAIL LOAN",                  Type = AccountType.Debt,               Fund = MunicipalFundType.ConservationTrust, DepartmentId = 1, BudgetPeriodId = 1, IsActive = true },
            new MunicipalAccount { Id = 9,  Name = "DUE TO/FROM TOW GENERAL FUND",        Type = AccountType.AccruedLiabilities, Fund = MunicipalFundType.ConservationTrust, DepartmentId = 1, BudgetPeriodId = 1, IsActive = true },
            new MunicipalAccount { Id = 10, Name = "DUE TO/FROM TOW UTILITY FUND",        Type = AccountType.AccruedLiabilities, Fund = MunicipalFundType.ConservationTrust, DepartmentId = 1, BudgetPeriodId = 1, IsActive = true },

            // Equity / Fund Balance
            new MunicipalAccount { Id = 11, Name = "FUND BALANCE",                        Type = AccountType.FundBalance,        Fund = MunicipalFundType.ConservationTrust, DepartmentId = 1, BudgetPeriodId = 1, IsActive = true },
            new MunicipalAccount { Id = 12, Name = "Opening Bal Equity",                  Type = AccountType.RetainedEarnings,   Fund = MunicipalFundType.ConservationTrust, DepartmentId = 1, BudgetPeriodId = 1, IsActive = true },
            new MunicipalAccount { Id = 13, Name = "Retained Earnings",                   Type = AccountType.RetainedEarnings,   Fund = MunicipalFundType.ConservationTrust, DepartmentId = 1, BudgetPeriodId = 1, IsActive = true },

            // Revenues (Income)
            new MunicipalAccount { Id = 14, Name = "STATE APPORTIONMENT",                 Type = AccountType.Revenue,            Fund = MunicipalFundType.ConservationTrust, DepartmentId = 1, BudgetPeriodId = 1, IsActive = true },
            new MunicipalAccount { Id = 15, Name = "WALKING TRAIL DONATION",              Type = AccountType.Grants,             Fund = MunicipalFundType.ConservationTrust, DepartmentId = 1, BudgetPeriodId = 1, IsActive = true },
            new MunicipalAccount { Id = 16, Name = "BASEBALL FIELD DONATIONS",            Type = AccountType.Grants,             Fund = MunicipalFundType.ConservationTrust, DepartmentId = 1, BudgetPeriodId = 1, IsActive = true },
            new MunicipalAccount { Id = 17, Name = "GRANT REVENUES",                      Type = AccountType.Grants,             Fund = MunicipalFundType.ConservationTrust, DepartmentId = 1, BudgetPeriodId = 1, IsActive = true },
            new MunicipalAccount { Id = 18, Name = "MISC REVENUE",                        Type = AccountType.Revenue,            Fund = MunicipalFundType.ConservationTrust, DepartmentId = 1, BudgetPeriodId = 1, IsActive = true },
            new MunicipalAccount { Id = 19, Name = "WALKING TRAIL REVENUE",               Type = AccountType.Revenue,            Fund = MunicipalFundType.ConservationTrust, DepartmentId = 1, BudgetPeriodId = 1, IsActive = true },
            new MunicipalAccount { Id = 20, Name = "INTEREST ON INVESTMENTS",             Type = AccountType.Interest,           Fund = MunicipalFundType.ConservationTrust, DepartmentId = 1, BudgetPeriodId = 1, IsActive = true },
            new MunicipalAccount { Id = 21, Name = "TRANSFER FROM REC FUND",              Type = AccountType.Transfers,          Fund = MunicipalFundType.ConservationTrust, DepartmentId = 1, BudgetPeriodId = 1, IsActive = true },

            // Expenses
            new MunicipalAccount { Id = 22, Name = "BALLFIELD ACCRUED INTEREST",         Type = AccountType.Expense,            Fund = MunicipalFundType.ConservationTrust, DepartmentId = 1, BudgetPeriodId = 1, IsActive = true },
            new MunicipalAccount { Id = 23, Name = "WALKING TRAIL ACCRUED INTEREST",      Type = AccountType.Expense,            Fund = MunicipalFundType.ConservationTrust, DepartmentId = 1, BudgetPeriodId = 1, IsActive = true },
            new MunicipalAccount { Id = 24, Name = "CAPITAL IMP - BALL COMPLEX",          Type = AccountType.CapitalOutlay,      Fund = MunicipalFundType.ConservationTrust, DepartmentId = 1, BudgetPeriodId = 1, IsActive = true },
            new MunicipalAccount { Id = 25, Name = "PARKS - DEVELOPMENT",                 Type = AccountType.CapitalOutlay,      Fund = MunicipalFundType.ConservationTrust, DepartmentId = 1, BudgetPeriodId = 1, IsActive = true },

            // Additional expenses to complete updated chart (31 total)
            new MunicipalAccount { Id = 26, Name = "MISC EXPENSE",                        Type = AccountType.Expense,            Fund = MunicipalFundType.ConservationTrust, DepartmentId = 1, BudgetPeriodId = 1, IsActive = true, FundDescription = "Conservation Trust Fund" },
            new MunicipalAccount { Id = 27, Name = "TRAIL MAINTENANCE",                   Type = AccountType.Expense,            Fund = MunicipalFundType.ConservationTrust, DepartmentId = 1, BudgetPeriodId = 1, IsActive = true, FundDescription = "Conservation Trust Fund" },
            new MunicipalAccount { Id = 28, Name = "PARK IMPROVEMENTS",                   Type = AccountType.CapitalOutlay,      Fund = MunicipalFundType.ConservationTrust, DepartmentId = 1, BudgetPeriodId = 1, IsActive = true, FundDescription = "Conservation Trust Fund" },
            new MunicipalAccount { Id = 29, Name = "EQUIPMENT PURCHASES",                 Type = AccountType.CapitalOutlay,      Fund = MunicipalFundType.ConservationTrust, DepartmentId = 1, BudgetPeriodId = 1, IsActive = true, FundDescription = "Conservation Trust Fund" },
            new MunicipalAccount { Id = 30, Name = "PROJECTS - SMALL",                    Type = AccountType.Expense,            Fund = MunicipalFundType.ConservationTrust, DepartmentId = 1, BudgetPeriodId = 1, IsActive = true, FundDescription = "Conservation Trust Fund" },
            new MunicipalAccount { Id = 31, Name = "RESERVES ALLOCATION",                 Type = AccountType.Transfers,          Fund = MunicipalFundType.ConservationTrust, DepartmentId = 1, BudgetPeriodId = 1, IsActive = true, FundDescription = "Conservation Trust Fund" }
        );

        // Seed: Owned type values for AccountNumber on MunicipalAccounts
        modelBuilder.Entity<MunicipalAccount>()
            .OwnsOne(e => e.AccountNumber)
            .HasData(
                new { MunicipalAccountId = 1,  Value = "110"   },
                new { MunicipalAccountId = 2,  Value = "110.1" },
                new { MunicipalAccountId = 3,  Value = "120"   },
                new { MunicipalAccountId = 4,  Value = "130"   },
                new { MunicipalAccountId = 5,  Value = "140"   },
                new { MunicipalAccountId = 6,  Value = "210"   },
                new { MunicipalAccountId = 7,  Value = "211"   },
                new { MunicipalAccountId = 8,  Value = "212"   },
                new { MunicipalAccountId = 9,  Value = "230"   },
                new { MunicipalAccountId = 10, Value = "240"   },
                new { MunicipalAccountId = 11, Value = "290"   },
                new { MunicipalAccountId = 12, Value = "3000"  },
                new { MunicipalAccountId = 13, Value = "33000" },
                new { MunicipalAccountId = 14, Value = "310"   },
                new { MunicipalAccountId = 15, Value = "314"   },
                new { MunicipalAccountId = 16, Value = "315"   },
                new { MunicipalAccountId = 17, Value = "320"   },
                new { MunicipalAccountId = 18, Value = "323"   },
                new { MunicipalAccountId = 19, Value = "325"   },
                new { MunicipalAccountId = 20, Value = "360"   },
                new { MunicipalAccountId = 21, Value = "370"   },
                new { MunicipalAccountId = 22, Value = "2111"  },
                new { MunicipalAccountId = 23, Value = "2112"  },
                new { MunicipalAccountId = 24, Value = "410"   },
                new { MunicipalAccountId = 25, Value = "420"   },
                new { MunicipalAccountId = 26, Value = "425"   },
                new { MunicipalAccountId = 27, Value = "430"   },
                new { MunicipalAccountId = 28, Value = "435"   },
                new { MunicipalAccountId = 29, Value = "440"   },
                new { MunicipalAccountId = 30, Value = "445"   },
                new { MunicipalAccountId = 31, Value = "450"   }
            );
    }

    // Hierarchy query for UI (e.g., BudgetView SfTreeGrid)
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

    // New: Transaction query for UI (e.g., MunicipalAccountView)
    public IQueryable<Transaction> GetTransactionsForBudget(int budgetEntryId)
    {
        return Transactions
            .Include(t => t.BudgetEntry)
            .Where(t => t.BudgetEntryId == budgetEntryId)
            .OrderByDescending(t => t.TransactionDate)
            .AsNoTracking();
    }

    // New: Excel import validation query
    public IQueryable<BudgetEntry> GetBudgetEntriesBySource(string sourceFilePath, int? rowNumber = null)
    {
        return BudgetEntries
            .Where(be => be.SourceFilePath == sourceFilePath && (rowNumber == null || be.SourceRowNumber == rowNumber))
            .AsNoTracking();
    }
    }
}
