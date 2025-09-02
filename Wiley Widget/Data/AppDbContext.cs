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
    /// DbSet for AI analysis results
    /// </summary>
    public DbSet<AiAnalysisResult> AiAnalysisResults { get; set; }

    /// <summary>
    /// DbSet for AI recommendations
    /// </summary>
    public DbSet<AiRecommendation> AiRecommendations { get; set; }

    /// <summary>
    /// DbSet for AI analysis audit trail
    /// </summary>
    public DbSet<AiAnalysisAudit> AiAnalysisAudits { get; set; }

    /// <summary>
    /// DbSet for AI response cache
    /// </summary>
    public DbSet<AiResponseCache> AiResponseCache { get; set; }

    /// <summary>
    /// Constructor for dependency injection
    /// </summary>
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Parameterless constructor for testing purposes only
    /// </summary>
    protected AppDbContext()
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

        // Configure AiAnalysisResult entity
        modelBuilder.Entity<AiAnalysisResult>(entity =>
        {
            entity.ToTable("AiAnalysisResults");
            entity.HasKey(aar => aar.Id);

            entity.Property(aar => aar.AnalysisDate)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(aar => aar.AnalysisType)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(aar => aar.InputHash)
                .IsRequired()
                .HasMaxLength(64);

            entity.Property(aar => aar.AiResponse)
                .IsRequired();

            entity.Property(aar => aar.ProcessingTimeMs)
                .IsRequired();

            entity.Property(aar => aar.IsSuccessful)
                .IsRequired()
                .HasDefaultValue(true);

            entity.Property(aar => aar.ApiCost)
                .HasColumnType("decimal(18,4)")
                .HasDefaultValue(0);

            entity.Property(aar => aar.Notes)
                .HasMaxLength(1000);

            // Indexes for performance
            entity.HasIndex(aar => aar.InputHash).IsUnique();
            entity.HasIndex(aar => aar.AnalysisDate);
            entity.HasIndex(aar => aar.AnalysisType);
        });

        // Configure AiRecommendation entity
        modelBuilder.Entity<AiRecommendation>(entity =>
        {
            entity.ToTable("AiRecommendations");
            entity.HasKey(ar => ar.Id);

            entity.Property(ar => ar.GeneratedDate)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(ar => ar.RecommendationType)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(ar => ar.Priority)
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue("Medium");

            entity.Property(ar => ar.RecommendationText)
                .IsRequired();

            entity.Property(ar => ar.ExpectedImpact)
                .HasColumnType("decimal(18,2)")
                .HasDefaultValue(0);

            entity.Property(ar => ar.ConfidenceLevel)
                .HasDefaultValue(50);

            entity.Property(ar => ar.Status)
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue("Pending");

            entity.Property(ar => ar.Notes)
                .HasMaxLength(500);

            // Foreign key relationship
            entity.HasOne(ar => ar.Enterprise)
                .WithMany()
                .HasForeignKey(ar => ar.EnterpriseId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes for performance
            entity.HasIndex(ar => ar.EnterpriseId);
            entity.HasIndex(ar => ar.RecommendationType);
            entity.HasIndex(ar => ar.Status);
            entity.HasIndex(ar => ar.Priority);
        });

        // Configure AiAnalysisAudit entity
        modelBuilder.Entity<AiAnalysisAudit>(entity =>
        {
            entity.ToTable("AiAnalysisAudits");
            entity.HasKey(aaa => aaa.Id);

            entity.Property(aaa => aaa.Timestamp)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(aaa => aaa.OperationType)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(aaa => aaa.UserId)
                .HasMaxLength(100)
                .HasDefaultValue("System");

            entity.Property(aaa => aaa.Description)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(aaa => aaa.Metadata)
                .HasDefaultValue("{}");

            entity.Property(aaa => aaa.Source)
                .HasMaxLength(50)
                .HasDefaultValue("LocalSystem");

            entity.Property(aaa => aaa.IsSuccessful)
                .IsRequired()
                .HasDefaultValue(true);

            entity.Property(aaa => aaa.ErrorMessage)
                .HasMaxLength(500);

            // Foreign key relationship
            entity.HasOne(aaa => aaa.Enterprise)
                .WithMany()
                .HasForeignKey(aaa => aaa.EnterpriseId)
                .OnDelete(DeleteBehavior.SetNull);

            // Indexes for performance
            entity.HasIndex(aaa => aaa.Timestamp);
            entity.HasIndex(aaa => aaa.OperationType);
            entity.HasIndex(aaa => aaa.UserId);
            entity.HasIndex(aaa => aaa.EnterpriseId);
        });

        // Configure AiResponseCache entity
        modelBuilder.Entity<AiResponseCache>(entity =>
        {
            entity.ToTable("AiResponseCache");
            entity.HasKey(arc => arc.CacheKey);

            entity.Property(arc => arc.CacheKey)
                .HasMaxLength(64);

            entity.Property(arc => arc.Response)
                .IsRequired();

            entity.Property(arc => arc.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(arc => arc.ExpiresAt)
                .IsRequired();

            entity.Property(arc => arc.AccessCount)
                .IsRequired()
                .HasDefaultValue(0);

            entity.Property(arc => arc.LastAccessedAt)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            // Index for performance
            entity.HasIndex(arc => arc.ExpiresAt);
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
