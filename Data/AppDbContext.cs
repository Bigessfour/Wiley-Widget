using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Diagnostics;
using WileyWidget.Models;

namespace WileyWidget.Data;

/// <summary>
/// Application database context for Entity Framework Core
/// Configures database schema and provides access to entity sets
/// </summary>
public class AppDbContext : DbContext
{
    /// <summary>
    /// DbSet for Enterprise entities (Water, Sewer, Trash, Apartments)
    /// </summary>
    public DbSet<Enterprise> Enterprises { get; set; }

    /// <summary>
    /// DbSet for BudgetInteraction entities (shared costs between enterprises)
    /// </summary>
    public DbSet<BudgetInteraction> BudgetInteractions { get; set; }

    /// <summary>
    /// DbSet for OverallBudget entities (municipal budget snapshots)
    /// </summary>
    public DbSet<OverallBudget> OverallBudgets { get; set; }

    /// <summary>
    /// Constructor for dependency injection
    /// </summary>
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Configures the model and relationships
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Enterprise entity
        modelBuilder.Entity<Enterprise>(entity =>
        {
            // Table name
            entity.ToTable("Enterprises");

            // Primary key
            entity.HasKey(e => e.Id);

            // Property configurations
            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.CurrentRate)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            entity.Property(e => e.MonthlyExpenses)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            entity.Property(e => e.MonthlyRevenue)
                .HasColumnType("decimal(18,2)");

            entity.Property(e => e.CitizenCount)
                .IsRequired();

            entity.Property(e => e.Notes)
                .HasMaxLength(500);

            // Indexes for performance
            entity.HasIndex(e => e.Name).IsUnique();
        });

        // Configure BudgetInteraction entity
        modelBuilder.Entity<BudgetInteraction>(entity =>
        {
            // Table name
            entity.ToTable("BudgetInteractions");

            // Primary key
            entity.HasKey(bi => bi.Id);

            // Foreign key relationships
            entity.HasOne(bi => bi.PrimaryEnterprise)
                .WithMany(e => e.BudgetInteractions)
                .HasForeignKey(bi => bi.PrimaryEnterpriseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(bi => bi.SecondaryEnterprise)
                .WithMany()
                .HasForeignKey(bi => bi.SecondaryEnterpriseId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            // Property configurations
            entity.Property(bi => bi.InteractionType)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(bi => bi.Description)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(bi => bi.MonthlyAmount)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            entity.Property(bi => bi.IsCost)
                .IsRequired()
                .HasDefaultValue(true);

            entity.Property(bi => bi.Notes)
                .HasMaxLength(300);

            // Indexes for performance
            entity.HasIndex(bi => bi.PrimaryEnterpriseId);
            entity.HasIndex(bi => bi.SecondaryEnterpriseId);
            entity.HasIndex(bi => bi.InteractionType);
        });

        // Configure OverallBudget entity
        modelBuilder.Entity<OverallBudget>(entity =>
        {
            // Table name
            entity.ToTable("OverallBudgets");

            // Primary key
            entity.HasKey(ob => ob.Id);

            // Property configurations
            entity.Property(ob => ob.SnapshotDate)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(ob => ob.TotalMonthlyRevenue)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            entity.Property(ob => ob.TotalMonthlyExpenses)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            entity.Property(ob => ob.TotalMonthlyBalance)
                .HasColumnType("decimal(18,2)");

            entity.Property(ob => ob.TotalCitizensServed)
                .IsRequired();

            entity.Property(ob => ob.AverageRatePerCitizen)
                .HasColumnType("decimal(18,2)");

            entity.Property(ob => ob.Notes)
                .HasMaxLength(500);

            entity.Property(ob => ob.IsCurrent)
                .IsRequired()
                .HasDefaultValue(false);

            // Ensure only one current budget snapshot
            entity.HasIndex(ob => ob.IsCurrent)
                .IsUnique()
                .HasFilter("IsCurrent = 1");

            // Indexes for performance
            entity.HasIndex(ob => ob.SnapshotDate);
        });
    }

    /// <summary>
    /// Configures database context options
    /// </summary>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        // Enable sensitive data logging in development only
        if (Debugger.IsAttached)
        {
            optionsBuilder.EnableSensitiveDataLogging();
        }

        // Configure query tracking and other behaviors
        optionsBuilder.ConfigureWarnings(warnings =>
        {
            // Suppress common warnings that are expected
            warnings.Ignore(RelationalEventId.MultipleCollectionIncludeWarning);
        });
    }

    /// <summary>
    /// Saves changes to the database
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Saves changes to the database
    /// </summary>
    public override int SaveChanges()
    {
        return base.SaveChanges();
    }
}
