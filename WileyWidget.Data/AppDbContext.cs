#nullable enable

using Microsoft.EntityFrameworkCore;
using WileyWidget.Models;
using WileyWidget.Models.Entities;

namespace WileyWidget.Data
{
    public class AppDbContext : DbContext
    {
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
        // Initialize DbSets to satisfy nullable reference types
        MunicipalAccounts = Set<MunicipalAccount>();
        Departments = Set<Department>();
        BudgetEntries = Set<BudgetEntry>();
        Funds = Set<Fund>();
        Transactions = Set<Transaction>();
        Enterprises = Set<Enterprise>();
        AppSettings = Set<AppSettings>();
        FiscalYearSettings = Set<FiscalYearSettings>();
        UtilityCustomers = Set<UtilityCustomer>();
        BudgetPeriods = Set<BudgetPeriod>();
        Invoices = Set<Invoice>();
        Vendors = Set<Vendor>();
        AuditEntries = Set<AuditEntry>();
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
    }

    public DbSet<MunicipalAccount> MunicipalAccounts { get; set; }
    public DbSet<Department> Departments { get; set; }
    public DbSet<BudgetEntry> BudgetEntries { get; set; }
    public DbSet<Fund> Funds { get; set; }
    public DbSet<Transaction> Transactions { get; set; } // New
    public DbSet<Enterprise> Enterprises { get; set; } // New
    public DbSet<AppSettings> AppSettings { get; set; } // New
    public DbSet<FiscalYearSettings> FiscalYearSettings { get; set; } // New
    public DbSet<UtilityCustomer> UtilityCustomers { get; set; } // New
    public DbSet<BudgetPeriod> BudgetPeriods { get; set; } // New
    public DbSet<Invoice> Invoices { get; set; } // New
    public DbSet<Vendor> Vendors { get; set; } // New
    public DbSet<AuditEntry> AuditEntries { get; set; } // New

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
            entity.Property(e => e.RowVersion).IsConcurrencyToken();
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
            new MunicipalAccount { Id = 25, Name = "PARKS - DEVELOPMENT",                 Type = AccountType.CapitalOutlay,      Fund = MunicipalFundType.ConservationTrust, DepartmentId = 1, BudgetPeriodId = 1, IsActive = true }
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
                new { MunicipalAccountId = 25, Value = "420"   }
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
