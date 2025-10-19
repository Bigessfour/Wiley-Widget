#nullable enable

using Microsoft.EntityFrameworkCore;
using WileyWidget.Models;
using WileyWidget.Models.Entities;

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
