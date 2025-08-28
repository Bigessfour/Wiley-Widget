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
    /// DbSet for Widget entities
    /// </summary>
    public DbSet<Widget> Widgets { get; set; }

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

        // Configure Widget entity
        modelBuilder.Entity<Widget>(entity =>
        {
            // Table name
            entity.ToTable("Widgets");

            // Primary key
            entity.HasKey(w => w.Id);

            // Property configurations
            entity.Property(w => w.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(w => w.Description)
                .HasMaxLength(500);

            entity.Property(w => w.Price)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            entity.Property(w => w.Quantity)
                .HasDefaultValue(0);

            entity.Property(w => w.IsActive)
                .HasDefaultValue(true);

            entity.Property(w => w.CreatedDate)
                .HasDefaultValueSql("GETUTCDATE()")
                .ValueGeneratedOnAdd();

            entity.Property(w => w.ModifiedDate)
                .ValueGeneratedOnUpdate();

            entity.Property(w => w.Category)
                .HasMaxLength(50);

            entity.Property(w => w.SKU)
                .HasMaxLength(20);

            // Indexes for performance
            entity.HasIndex(w => w.Name);
            entity.HasIndex(w => w.Category);
            entity.HasIndex(w => w.SKU).IsUnique();
            entity.HasIndex(w => w.IsActive);
            entity.HasIndex(w => w.CreatedDate);
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
    /// Saves changes to the database with automatic timestamp updates
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Update ModifiedDate for modified entities
        foreach (var entry in ChangeTracker.Entries<Widget>()
            .Where(e => e.State == EntityState.Modified))
        {
            entry.Entity.MarkAsModified();
        }

        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Saves changes to the database with automatic timestamp updates
    /// </summary>
    public override int SaveChanges()
    {
        // Update ModifiedDate for modified entities
        foreach (var entry in ChangeTracker.Entries<Widget>()
            .Where(e => e.State == EntityState.Modified))
        {
            entry.Entity.MarkAsModified();
        }

        return base.SaveChanges();
    }
}
