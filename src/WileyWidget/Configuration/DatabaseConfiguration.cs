#nullable enable

using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using WileyWidget.Data;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.Business.Interfaces;
using System.Data.Common;
using System.Net.Http;
using Microsoft.EntityFrameworkCore.Storage;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using WileyWidget.Models;
using WileyWidget.Models.Entities;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System.Collections.Generic;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace WileyWidget.Configuration;

/// <summary>
/// Enterprise-grade database configuration for SQL Server
/// Implements passwordless authentication, connection pooling, health checks, and monitoring
/// </summary>
public static class DatabaseConfiguration
{
    /// <summary>
    /// Circuit breaker policy for database connections
    /// Tuned for resilience: 5 failures before breaking, 60 second recovery period
    /// </summary>
    internal static readonly AsyncCircuitBreakerPolicy CircuitBreaker = Policy
        .Handle<SqlException>(ex => ex.Number is 4060 or 40197 or 40501 or 40613 or 49918 or 49919 or 49920 or 11001)
        .Or<TimeoutException>()
        .CircuitBreakerAsync(5, TimeSpan.FromSeconds(60));

    /// <summary>
    /// Retry policy for development database connections
    /// Uses exponential backoff: 3 retries with 1s, 2s, 4s delays
    /// </summary>
    internal static readonly AsyncRetryPolicy DevelopmentRetryPolicy = Policy
        .Handle<SqlException>(ex => ex.Number is 4060 or 40197 or 40501 or 40613 or 49918 or 49919 or 49920 or 11001)
        .Or<DbException>()
        .Or<TimeoutException>()
        .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - 1)));

    /// <summary>
    /// Adds enterprise-grade database services to the dependency injection container
    /// </summary>
        public static IServiceCollection AddEnterpriseDatabaseServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
            // Register EF interceptors and helpers used by DbContext
            // Use runtime type resolution to avoid temporary wpftmp compilation issues
            // where the same types may be compiled into multiple assemblies during WPF markup
            // compilation. Resolve the interceptor type from the WileyWidget.Data assembly at runtime.
            var auditInterceptorType = Type.GetType("WileyWidget.Data.Interceptors.AuditInterceptor, WileyWidget.Data");
            if (auditInterceptorType != null)
            {
                services.AddSingleton(auditInterceptorType, sp =>
                {
                    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("AuditInterceptor");
                    var userContext = sp.GetRequiredService<IUserContext>();
                    var auditService = sp.GetRequiredService<IAuditService>();
                    return Activator.CreateInstance(auditInterceptorType, logger, userContext, auditService);
                });
            }

        // EF Core DI Lifetime Fix: Use AddDbContextFactory with Singleton lifetime for factory
        // This provides a singleton factory that creates scoped DbContexts
        services.AddDbContextFactory<AppDbContext>((sp, options) =>
        {
            ConfigureAppDbContext(sp, options);
        }, ServiceLifetime.Singleton);

        // Register DbContext as Scoped for direct injection
        services.AddScoped<AppDbContext>(sp => sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());

        // Add enterprise health checks
        ConfigureEnterpriseHealthChecks(services, configuration);

        // Register repositories with enterprise patterns
        RegisterEnterpriseRepositories(services);

        // Register enterprise services
        RegisterEnterpriseServices(services);

        return services;
    }

    /// <summary>
    /// Configures AppDbContext regardless of registration mechanism (scoped or factory).
    /// </summary>
    private static void ConfigureAppDbContext(IServiceProvider sp, DbContextOptionsBuilder options)
    {
        ArgumentNullException.ThrowIfNull(sp);
        ArgumentNullException.ThrowIfNull(options);

        var config = sp.GetRequiredService<IConfiguration>();
        var logger = sp.GetRequiredService<ILogger<AppDbContext>>();
        var hostEnvironment = sp.GetService<IHostEnvironment>();
        var environmentName = hostEnvironment?.EnvironmentName ?? "Production";

        var connectionString = BuildEnterpriseConnectionString(config, logger, environmentName);

        logger.LogInformation("🔍 DEBUG: SQL Server connection string configured for {Environment}",
            environmentName);

        // FIXED: Configure general EF options (including warnings) BEFORE provider configuration
        // This prevents ArgumentException in EF Core 9.0 due to options builder state conflicts
        ConfigureEnterpriseDbContextOptions(options, logger);
        ConfigureSqlServer(options, connectionString, logger, environmentName);

        // Add registered interceptors (AuditInterceptor) into DbContext options
        try
        {
            // Resolve the interceptor at runtime to avoid cross-assembly duplicate-type issues
            var auditInterceptorTypeRuntime = Type.GetType("WileyWidget.Data.Interceptors.AuditInterceptor, WileyWidget.Data");
            if (auditInterceptorTypeRuntime != null)
            {
                var auditInterceptorInstance = sp.GetService(auditInterceptorTypeRuntime);
                if (auditInterceptorInstance is Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor efInterceptor)
                {
                    options.AddInterceptors(efInterceptor);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to attach AuditInterceptor to DbContext options (non-fatal)");
        }

        // Configure EF 9 seeding methods for automatic database initialization
        // ConfigureDatabaseSeeding(options, logger);
    }

    /// <summary>
    /// Determines if connection string is for local SQL Server
    /// </summary>
    internal static bool IsLocalSqlServerConnection(string connectionString)
    {
        return connectionString.Contains("(localdb)", StringComparison.OrdinalIgnoreCase) ||
               connectionString.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
               connectionString.Contains(Environment.MachineName, StringComparison.OrdinalIgnoreCase) ||
               connectionString.Contains("SQLEXPRESS", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Configures local SQL Server connection for development
    /// </summary>
    private static void ConfigureSqlServer(DbContextOptionsBuilder options, string connectionString, ILogger logger, string environmentName)
    {
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            // Specify migrations assembly since DbContext is in WileyWidget.Data project
            sqlOptions.MigrationsAssembly("WileyWidget.Data");

            // Development retry configuration
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);

            // Connection timeout
            sqlOptions.CommandTimeout(30);

            // EF Core 10: Enable SQL Server 2025 JSON type support (compatibility level 170)
            // Falls back to nvarchar(max) on older SQL Server versions automatically
            sqlOptions.UseCompatibilityLevel(170);

            // EF Core 10: Optimize parameterized collection translation
            // Uses multiple scalar parameters by default (better than EF9's JSON array approach)
            // This provides better query plan optimization with cardinality information
            // sqlOptions.UseParameterTranslationMode(Microsoft.EntityFrameworkCore.Infrastructure.ParameterTranslationMode.Parameter); // Commented out - requires EF Core 10+
        });

        logger.LogInformation("✅ Configured SQL Server connection for {Environment} environment", environmentName);
    }

    /// <summary>
    /// Extracts server name from connection string for logging
    /// </summary>
    private static string ExtractServerFromConnectionString(string connectionString)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            return builder.DataSource ?? "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }



    /// <summary>
    /// Builds enterprise connection string from configuration
    /// Uses SQL Server Express for Development, SQL Server for Production
    /// </summary>
    internal static string BuildEnterpriseConnectionString(IConfiguration config, ILogger logger, string environment)
    {
        logger?.LogInformation("🏭 CONFIGURING DATABASE CONNECTION - Environment: {Environment}", environment);

        var connectionString = config.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("DefaultConnection must be configured for local database access");
        }

        logger?.LogInformation("Using database connection: {Server}",
            ExtractServerFromConnectionString(connectionString));
        return connectionString;
    }

    /// <summary>
    /// Configures enterprise-grade DbContext options
    /// </summary>
    private static void ConfigureEnterpriseDbContextOptions(DbContextOptionsBuilder options, ILogger logger)
    {
        // Enable sensitive data logging in development only
        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
        {
            options.EnableSensitiveDataLogging();
            logger.LogInformation("Sensitive data logging enabled for local diagnostics");
        }
        options.EnableDetailedErrors();

        // Configure query tracking - optimized for read-heavy workloads
        // EF Core 9.0.0 Optimization: Default to NoTracking to eliminate change detection overhead
        // Individual queries can still use .AsTracking() when modification is needed
        options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);

        // Configure warnings
        options.ConfigureWarnings(warnings =>
        {
            warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.MultipleCollectionIncludeWarning);
        });

        // Reduce noise: only log executed SQL at Information; suppress change-tracking chatter
        options.LogTo(message => logger.LogInformation("EF SQL: {Message}", message),
            new[] { Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.CommandExecuted },
            LogLevel.Information);

        logger.LogInformation("Enterprise DbContext options configured for local environment");
    }

    /// <summary>
    /// Configures EF 9 database seeding using UseSeeding and UseAsyncSeeding methods
    /// Note: Seeding disabled for simplified budget schema
    /// </summary>
    private static void ConfigureDatabaseSeeding(DbContextOptionsBuilder options, ILogger logger)
    {
        logger.LogInformation("Configuring EF Core UseSeeding for FY 2026 budget data");

        options.UseSeeding((context, _) =>
        {
            var dbContext = (AppDbContext)context;

            // Seed enterprises
            SeedEnterprises(dbContext);

            // Seed departments
            SeedDepartments(dbContext);

            // Seed funds
            SeedFunds(dbContext);

            // Seed FY 2026 budget data
            SeedBudgetData(dbContext);
        });

        options.UseAsyncSeeding(async (context, _, cancellationToken) =>
        {
            var dbContext = (AppDbContext)context;

            // Seed enterprises
            SeedEnterprises(dbContext);

            // Seed departments
            SeedDepartments(dbContext);

            // Seed funds
            SeedFunds(dbContext);

            // Seed FY 2026 budget data
            SeedBudgetData(dbContext);

            await Task.CompletedTask;
        });
    }

    private static void SeedEnterprises(AppDbContext context)
    {
        if (!context.Enterprises.Any())
        {
            context.Enterprises.AddRange(
                new Enterprise
                {
                    Id = 1,
                    Name = "Town of Wiley Water Department",
                    BudgetAmount = 285755.00m,
                    CitizenCount = 12500,
                    CurrentRate = 45.50m,
                    Type = "Water",
                    Status = EnterpriseStatus.Active,
                    CreatedDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    ModifiedDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Enterprise
                {
                    Id = 2,
                    Name = "Town of Wiley Sewer Department",
                    BudgetAmount = 5879527.00m,
                    CitizenCount = 12500,
                    CurrentRate = 125.75m,
                    Type = "Sewer",
                    Status = EnterpriseStatus.Active,
                    CreatedDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    ModifiedDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Enterprise
                {
                    Id = 3,
                    Name = "Town of Wiley Electric Department",
                    BudgetAmount = 285755.00m,
                    CitizenCount = 12500,
                    CurrentRate = 0.12m,
                    Type = "Electric",
                    Status = EnterpriseStatus.Active,
                    CreatedDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    ModifiedDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            );
        }
    }

    private static void SeedDepartments(AppDbContext context)
    {
        if (!context.Departments.Any())
        {
            context.Departments.AddRange(
                new Department { Id = 1, Name = "Administration", DepartmentCode = "ADMIN" },
                new Department { Id = 2, Name = "Public Works", DepartmentCode = "DPW" },
                new Department { Id = 3, Name = "Culture and Recreation", DepartmentCode = "CULT" },
                new Department { Id = 4, Name = "Sanitation", DepartmentCode = "SAN", ParentId = 2 },
                new Department { Id = 5, Name = "Utilities", DepartmentCode = "UTIL" },
                new Department { Id = 6, Name = "Community Center", DepartmentCode = "COMM" },
                new Department { Id = 7, Name = "Conservation", DepartmentCode = "CONS" },
                new Department { Id = 8, Name = "Recreation", DepartmentCode = "REC" }
            );
        }
    }

    private static void SeedFunds(AppDbContext context)
    {
        if (!context.Funds.Any())
        {
            context.Funds.AddRange(
                new Fund { Id = 1, FundCode = "100-GEN", Name = "General Fund", Type = FundType.GeneralFund },
                new Fund { Id = 2, FundCode = "200-ENT", Name = "Enterprise Fund", Type = FundType.EnterpriseFund },
                new Fund { Id = 3, FundCode = "300-UTIL", Name = "Utility Fund", Type = FundType.EnterpriseFund },
                new Fund { Id = 4, FundCode = "400-COMM", Name = "Community Center Fund", Type = FundType.SpecialRevenue },
                new Fund { Id = 5, FundCode = "500-CONS", Name = "Conservation Trust Fund", Type = FundType.PermanentFund },
                new Fund { Id = 6, FundCode = "600-REC", Name = "Recreation Fund", Type = FundType.SpecialRevenue }
            );
        }
    }

    private static void SeedBudgetData(AppDbContext context)
    {
        if (!context.BudgetEntries.Any())
        {
            var budgetEntries = new List<BudgetEntry>();

            // === WILEY SANITATION DISTRICT ===

            // General Fund Revenues
            budgetEntries.AddRange(new[]
            {
                new BudgetEntry { Id = 1, AccountNumber = "101", Description = "Property Taxes - General Fund",
                    BudgetedAmount = 8221.00m, FiscalYear = 2026, FundType = FundType.GeneralFund,
                    DepartmentId = 4, FundId = 1, ActivityCode = "GOV",
                    StartPeriod = new DateTime(2026, 1, 1), EndPeriod = new DateTime(2026, 12, 31),
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BudgetEntry { Id = 2, AccountNumber = "102", Description = "Other Revenues - General Fund",
                    BudgetedAmount = 915.00m, FiscalYear = 2026, FundType = FundType.GeneralFund,
                    DepartmentId = 4, FundId = 1, ActivityCode = "GOV",
                    StartPeriod = new DateTime(2026, 1, 1), EndPeriod = new DateTime(2026, 12, 31),
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BudgetEntry { Id = 3, AccountNumber = "103", Description = "Unappropriated Fund Balance Beginning - General Fund",
                    BudgetedAmount = 14106.00m, FiscalYear = 2026, FundType = FundType.GeneralFund,
                    DepartmentId = 4, FundId = 1, ActivityCode = "GOV",
                    StartPeriod = new DateTime(2026, 1, 1), EndPeriod = new DateTime(2026, 12, 31),
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
            });

            // General Fund Expenditures
            budgetEntries.AddRange(new[]
            {
                new BudgetEntry { Id = 4, AccountNumber = "201", Description = "Bank Service Charge",
                    BudgetedAmount = 50.00m, FiscalYear = 2026, FundType = FundType.GeneralFund,
                    DepartmentId = 4, FundId = 1, ActivityCode = "GOV",
                    StartPeriod = new DateTime(2026, 1, 1), EndPeriod = new DateTime(2026, 12, 31),
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BudgetEntry { Id = 5, AccountNumber = "202", Description = "Management Fee",
                    BudgetedAmount = 5400.00m, FiscalYear = 2026, FundType = FundType.GeneralFund,
                    DepartmentId = 4, FundId = 1, ActivityCode = "GOV",
                    StartPeriod = new DateTime(2026, 1, 1), EndPeriod = new DateTime(2026, 12, 31),
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BudgetEntry { Id = 6, AccountNumber = "203", Description = "Miscellaneous Expenses",
                    BudgetedAmount = 500.00m, FiscalYear = 2026, FundType = FundType.GeneralFund,
                    DepartmentId = 4, FundId = 1, ActivityCode = "GOV",
                    StartPeriod = new DateTime(2026, 1, 1), EndPeriod = new DateTime(2026, 12, 31),
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BudgetEntry { Id = 7, AccountNumber = "204", Description = "Office Supplies",
                    BudgetedAmount = 300.00m, FiscalYear = 2026, FundType = FundType.GeneralFund,
                    DepartmentId = 4, FundId = 1, ActivityCode = "GOV",
                    StartPeriod = new DateTime(2026, 1, 1), EndPeriod = new DateTime(2026, 12, 31),
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BudgetEntry { Id = 8, AccountNumber = "205", Description = "Treasurer Fees",
                    BudgetedAmount = 250.00m, FiscalYear = 2026, FundType = FundType.GeneralFund,
                    DepartmentId = 4, FundId = 1, ActivityCode = "GOV",
                    StartPeriod = new DateTime(2026, 1, 1), EndPeriod = new DateTime(2026, 12, 31),
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
            });

            // Enterprise Fund Revenues
            budgetEntries.AddRange(new[]
            {
                new BudgetEntry { Id = 9, AccountNumber = "301", Description = "Other Revenues - Enterprise Fund",
                    BudgetedAmount = 5879527.00m, FiscalYear = 2026, FundType = FundType.EnterpriseFund,
                    DepartmentId = 4, FundId = 2, ActivityCode = "BUS",
                    StartPeriod = new DateTime(2026, 1, 1), EndPeriod = new DateTime(2026, 12, 31),
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BudgetEntry { Id = 10, AccountNumber = "302", Description = "Unappropriated Fund Balance Beginning - Enterprise Fund",
                    BudgetedAmount = 172542.00m, FiscalYear = 2026, FundType = FundType.EnterpriseFund,
                    DepartmentId = 4, FundId = 2, ActivityCode = "BUS",
                    StartPeriod = new DateTime(2026, 1, 1), EndPeriod = new DateTime(2026, 12, 31),
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
            });

            // Enterprise Fund Expenditures (detailed breakdown)
            budgetEntries.AddRange(new[]
            {
                new BudgetEntry { Id = 11, AccountNumber = "401", Description = "Permits and Assessments",
                    BudgetedAmount = 976.00m, FiscalYear = 2026, FundType = FundType.EnterpriseFund,
                    DepartmentId = 4, FundId = 2, ActivityCode = "BUS",
                    StartPeriod = new DateTime(2026, 1, 1), EndPeriod = new DateTime(2026, 12, 31),
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BudgetEntry { Id = 12, AccountNumber = "402", Description = "Bank Service and Interest",
                    BudgetedAmount = 35.00m, FiscalYear = 2026, FundType = FundType.EnterpriseFund,
                    DepartmentId = 4, FundId = 2, ActivityCode = "BUS",
                    StartPeriod = new DateTime(2026, 1, 1), EndPeriod = new DateTime(2026, 12, 31),
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BudgetEntry { Id = 13, AccountNumber = "403", Description = "Outside Service Lab Fees",
                    BudgetedAmount = 700.00m, FiscalYear = 2026, FundType = FundType.EnterpriseFund,
                    DepartmentId = 4, FundId = 2, ActivityCode = "BUS",
                    StartPeriod = new DateTime(2026, 1, 1), EndPeriod = new DateTime(2026, 12, 31),
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BudgetEntry { Id = 14, AccountNumber = "404", Description = "Budget Audit Legal",
                    BudgetedAmount = 18000.00m, FiscalYear = 2026, FundType = FundType.EnterpriseFund,
                    DepartmentId = 4, FundId = 2, ActivityCode = "BUS",
                    StartPeriod = new DateTime(2026, 1, 1), EndPeriod = new DateTime(2026, 12, 31),
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BudgetEntry { Id = 15, AccountNumber = "405", Description = "Supplies and Expenses",
                    BudgetedAmount = 2000.00m, FiscalYear = 2026, FundType = FundType.EnterpriseFund,
                    DepartmentId = 4, FundId = 2, ActivityCode = "BUS",
                    StartPeriod = new DateTime(2026, 1, 1), EndPeriod = new DateTime(2026, 12, 31),
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BudgetEntry { Id = 16, AccountNumber = "406", Description = "Insurance",
                    BudgetedAmount = 7500.00m, FiscalYear = 2026, FundType = FundType.EnterpriseFund,
                    DepartmentId = 4, FundId = 2, ActivityCode = "BUS",
                    StartPeriod = new DateTime(2026, 1, 1), EndPeriod = new DateTime(2026, 12, 31),
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BudgetEntry { Id = 17, AccountNumber = "407", Description = "Sewer Cleaning",
                    BudgetedAmount = 7600.00m, FiscalYear = 2026, FundType = FundType.EnterpriseFund,
                    DepartmentId = 4, FundId = 2, ActivityCode = "BUS",
                    StartPeriod = new DateTime(2026, 1, 1), EndPeriod = new DateTime(2026, 12, 31),
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BudgetEntry { Id = 18, AccountNumber = "408", Description = "Capital Outlay",
                    BudgetedAmount = 5725427.00m, FiscalYear = 2026, FundType = FundType.EnterpriseFund,
                    DepartmentId = 4, FundId = 2, ActivityCode = "BUS",
                    StartPeriod = new DateTime(2026, 1, 1), EndPeriod = new DateTime(2026, 12, 31),
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
            });

            // === TOWN OF WILEY ===

            // General Fund Revenues
            budgetEntries.AddRange(new[]
            {
                new BudgetEntry { Id = 19, AccountNumber = "501", Description = "Property Taxes - General Fund",
                    BudgetedAmount = 85692.00m, FiscalYear = 2026, FundType = FundType.GeneralFund,
                    DepartmentId = 1, FundId = 1, ActivityCode = "GOV",
                    StartPeriod = new DateTime(2026, 1, 1), EndPeriod = new DateTime(2026, 12, 31),
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BudgetEntry { Id = 20, AccountNumber = "502", Description = "Other Revenues - General Fund",
                    BudgetedAmount = 192683.00m, FiscalYear = 2026, FundType = FundType.GeneralFund,
                    DepartmentId = 1, FundId = 1, ActivityCode = "GOV",
                    StartPeriod = new DateTime(2026, 1, 1), EndPeriod = new DateTime(2026, 12, 31),
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BudgetEntry { Id = 21, AccountNumber = "503", Description = "Unappropriated Fund Balance Beginning - General Fund",
                    BudgetedAmount = 442211.00m, FiscalYear = 2026, FundType = FundType.GeneralFund,
                    DepartmentId = 1, FundId = 1, ActivityCode = "GOV",
                    StartPeriod = new DateTime(2026, 1, 1), EndPeriod = new DateTime(2026, 12, 31),
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
            });

            // General Fund Expenditures
            budgetEntries.AddRange(new[]
            {
                new BudgetEntry { Id = 22, AccountNumber = "601", Description = "Administration",
                    BudgetedAmount = 142618.00m, FiscalYear = 2026, FundType = FundType.GeneralFund,
                    DepartmentId = 1, FundId = 1, ActivityCode = "GOV",
                    StartPeriod = new DateTime(2026, 1, 1), EndPeriod = new DateTime(2026, 12, 31),
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BudgetEntry { Id = 23, AccountNumber = "602", Description = "Public Works",
                    BudgetedAmount = 194500.00m, FiscalYear = 2026, FundType = FundType.GeneralFund,
                    DepartmentId = 2, FundId = 1, ActivityCode = "GOV",
                    StartPeriod = new DateTime(2026, 1, 1), EndPeriod = new DateTime(2026, 12, 31),
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BudgetEntry { Id = 24, AccountNumber = "603", Description = "Culture and Recreation",
                    BudgetedAmount = 7050.00m, FiscalYear = 2026, FundType = FundType.GeneralFund,
                    DepartmentId = 3, FundId = 1, ActivityCode = "GOV",
                    StartPeriod = new DateTime(2026, 1, 1), EndPeriod = new DateTime(2026, 12, 31),
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
            });

            // Utility Fund Revenues
            budgetEntries.AddRange(new[]
            {
                new BudgetEntry { Id = 25, AccountNumber = "701", Description = "Other Revenues - Utility Fund",
                    BudgetedAmount = 285755.00m, FiscalYear = 2026, FundType = FundType.EnterpriseFund,
                    DepartmentId = 5, FundId = 3, ActivityCode = "BUS",
                    StartPeriod = new DateTime(2026, 1, 1), EndPeriod = new DateTime(2026, 12, 31),
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BudgetEntry { Id = 26, AccountNumber = "702", Description = "Unappropriated Fund Balance Beginning - Utility Fund",
                    BudgetedAmount = 71549.00m, FiscalYear = 2026, FundType = FundType.EnterpriseFund,
                    DepartmentId = 5, FundId = 3, ActivityCode = "BUS",
                    StartPeriod = new DateTime(2026, 1, 1), EndPeriod = new DateTime(2026, 12, 31),
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
            });

            // Utility Fund Expenditures
            budgetEntries.AddRange(new[]
            {
                new BudgetEntry { Id = 27, AccountNumber = "801", Description = "Utility Fund Expenditures",
                    BudgetedAmount = 238978.00m, FiscalYear = 2026, FundType = FundType.EnterpriseFund,
                    DepartmentId = 5, FundId = 3, ActivityCode = "BUS",
                    StartPeriod = new DateTime(2026, 1, 1), EndPeriod = new DateTime(2026, 12, 31),
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
            });

            // Community Center Fund Revenues
            budgetEntries.AddRange(new[]
            {
                new BudgetEntry { Id = 28, AccountNumber = "901", Description = "Other Revenues - Community Center Fund",
                    BudgetedAmount = 14740.00m, FiscalYear = 2026, FundType = FundType.SpecialRevenue,
                    DepartmentId = 6, FundId = 4, ActivityCode = "GOV",
                    StartPeriod = new DateTime(2026, 1, 1), EndPeriod = new DateTime(2026, 12, 31),
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BudgetEntry { Id = 29, AccountNumber = "902", Description = "Unappropriated Fund Balance Beginning - Community Center Fund",
                    BudgetedAmount = 26777.00m, FiscalYear = 2026, FundType = FundType.SpecialRevenue,
                    DepartmentId = 6, FundId = 4, ActivityCode = "GOV",
                    StartPeriod = new DateTime(2026, 1, 1), EndPeriod = new DateTime(2026, 12, 31),
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
            });

            // Community Center Fund Expenditures
            budgetEntries.AddRange(new[]
            {
                new BudgetEntry { Id = 30, AccountNumber = "1001", Description = "Community Center Fund Expenditures",
                    BudgetedAmount = 12740.00m, FiscalYear = 2026, FundType = FundType.SpecialRevenue,
                    DepartmentId = 6, FundId = 4, ActivityCode = "GOV",
                    StartPeriod = new DateTime(2026, 1, 1), EndPeriod = new DateTime(2026, 12, 31),
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
            });

            // Conservation Trust Fund Revenues
            budgetEntries.AddRange(new[]
            {
                new BudgetEntry { Id = 31, AccountNumber = "1101", Description = "Other Revenues - Conservation Trust Fund",
                    BudgetedAmount = 5215.00m, FiscalYear = 2026, FundType = FundType.PermanentFund,
                    DepartmentId = 7, FundId = 5, ActivityCode = "GOV",
                    StartPeriod = new DateTime(2026, 1, 1), EndPeriod = new DateTime(2026, 12, 31),
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BudgetEntry { Id = 32, AccountNumber = "1102", Description = "Unappropriated Fund Balance Beginning - Conservation Trust Fund",
                    BudgetedAmount = 40557.00m, FiscalYear = 2026, FundType = FundType.PermanentFund,
                    DepartmentId = 7, FundId = 5, ActivityCode = "GOV",
                    StartPeriod = new DateTime(2026, 1, 1), EndPeriod = new DateTime(2026, 12, 31),
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
            });

            // Conservation Trust Fund Expenditures
            budgetEntries.AddRange(new[]
            {
                new BudgetEntry { Id = 33, AccountNumber = "1201", Description = "Conservation Trust Fund Expenditures",
                    BudgetedAmount = 8500.00m, FiscalYear = 2026, FundType = FundType.PermanentFund,
                    DepartmentId = 7, FundId = 5, ActivityCode = "GOV",
                    StartPeriod = new DateTime(2026, 1, 1), EndPeriod = new DateTime(2026, 12, 31),
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
            });

            // Recreation Fund Revenues
            budgetEntries.AddRange(new[]
            {
                new BudgetEntry { Id = 34, AccountNumber = "1301", Description = "Other Revenues - Recreation Fund",
                    BudgetedAmount = 20325.00m, FiscalYear = 2026, FundType = FundType.SpecialRevenue,
                    DepartmentId = 8, FundId = 6, ActivityCode = "GOV",
                    StartPeriod = new DateTime(2026, 1, 1), EndPeriod = new DateTime(2026, 12, 31),
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BudgetEntry { Id = 35, AccountNumber = "1302", Description = "Unappropriated Fund Balance Beginning - Recreation Fund",
                    BudgetedAmount = 15311.00m, FiscalYear = 2026, FundType = FundType.SpecialRevenue,
                    DepartmentId = 8, FundId = 6, ActivityCode = "GOV",
                    StartPeriod = new DateTime(2026, 1, 1), EndPeriod = new DateTime(2026, 12, 31),
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
            });

            // Recreation Fund Expenditures (detailed breakdown)
            budgetEntries.AddRange(new[]
            {
                new BudgetEntry { Id = 36, AccountNumber = "1401", Description = "Baseball/Softball",
                    BudgetedAmount = 6500.00m, FiscalYear = 2026, FundType = FundType.SpecialRevenue,
                    DepartmentId = 8, FundId = 6, ActivityCode = "GOV",
                    StartPeriod = new DateTime(2026, 1, 1), EndPeriod = new DateTime(2026, 12, 31),
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BudgetEntry { Id = 37, AccountNumber = "1402", Description = "Football",
                    BudgetedAmount = 5000.00m, FiscalYear = 2026, FundType = FundType.SpecialRevenue,
                    DepartmentId = 8, FundId = 6, ActivityCode = "GOV",
                    StartPeriod = new DateTime(2026, 1, 1), EndPeriod = new DateTime(2026, 12, 31),
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BudgetEntry { Id = 38, AccountNumber = "1403", Description = "Soccer",
                    BudgetedAmount = 4000.00m, FiscalYear = 2026, FundType = FundType.SpecialRevenue,
                    DepartmentId = 8, FundId = 6, ActivityCode = "GOV",
                    StartPeriod = new DateTime(2026, 1, 1), EndPeriod = new DateTime(2026, 12, 31),
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BudgetEntry { Id = 39, AccountNumber = "1404", Description = "Basketball",
                    BudgetedAmount = 4000.00m, FiscalYear = 2026, FundType = FundType.SpecialRevenue,
                    DepartmentId = 8, FundId = 6, ActivityCode = "GOV",
                    StartPeriod = new DateTime(2026, 1, 1), EndPeriod = new DateTime(2026, 12, 31),
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BudgetEntry { Id = 40, AccountNumber = "1405", Description = "Volleyball",
                    BudgetedAmount = 1275.00m, FiscalYear = 2026, FundType = FundType.SpecialRevenue,
                    DepartmentId = 8, FundId = 6, ActivityCode = "GOV",
                    StartPeriod = new DateTime(2026, 1, 1), EndPeriod = new DateTime(2026, 12, 31),
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BudgetEntry { Id = 41, AccountNumber = "1406", Description = "Wrestling",
                    BudgetedAmount = 1000.00m, FiscalYear = 2026, FundType = FundType.SpecialRevenue,
                    DepartmentId = 8, FundId = 6, ActivityCode = "GOV",
                    StartPeriod = new DateTime(2026, 1, 1), EndPeriod = new DateTime(2026, 12, 31),
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
            });

            context.BudgetEntries.AddRange(budgetEntries);
        }
    }

    /// <summary>
    /// Registers enterprise-grade repositories
    /// </summary>
    private static void RegisterEnterpriseRepositories(IServiceCollection services)
    {
        services.AddScoped<WileyWidget.Business.Interfaces.IBudgetRepository, WileyWidget.Data.BudgetRepository>();
        services.AddScoped<WileyWidget.Business.Interfaces.IDepartmentRepository, WileyWidget.Data.DepartmentRepository>();
        services.AddScoped<WileyWidget.Business.Interfaces.IMunicipalAccountRepository, WileyWidget.Data.MunicipalAccountRepository>();
        services.AddScoped<WileyWidget.Business.Interfaces.IEnterpriseRepository, WileyWidget.Data.EnterpriseRepository>();
        services.AddScoped<WileyWidget.Business.Interfaces.IUtilityCustomerRepository, WileyWidget.Data.UtilityCustomerRepository>();
        services.AddScoped<WileyWidget.Business.Interfaces.IAuditRepository, WileyWidget.Data.AuditRepository>();
    }

    /// <summary>
    /// Registers enterprise-grade services
    /// </summary>
    private static void RegisterEnterpriseServices(IServiceCollection services)
    {
        // Register QuickBooks service using runtime type bindings to avoid duplicate-type/interface mismatches
        var qbInterfaceType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("WileyWidget.Services.IQuickBooksService"))
            .FirstOrDefault(t => t != null);

        if (qbInterfaceType != null)
        {
            services.AddScoped(qbInterfaceType, sp =>
            {
                // FIXED: Resolve ISettingsService from DI instead of using static Instance
                // This prevents circular dependency and timeout issues during container resolution
                object? settings = null;
                var settingsInterfaceType = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("WileyWidget.Services.ISettingsService"))
                    .FirstOrDefault(t => t != null);
                if (settingsInterfaceType != null)
                {
                    try
                    {
                        settings = sp.GetService(settingsInterfaceType);
                    }
                    catch { }
                }

                if (settings == null)
                {
                    // If settings service not available, QuickBooks service will need to handle gracefully
                    Serilog.Log.Warning("ISettingsService not available for QuickBooksService registration");
                }

                // Resolve ISecretVaultService at runtime
                object? secretVaultService = null;
                var secretVaultInterfaceType = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("WileyWidget.Services.ISecretVaultService"))
                    .FirstOrDefault(t => t != null);
                if (secretVaultInterfaceType != null)
                {
                    try
                    {
                        secretVaultService = sp.GetService(secretVaultInterfaceType);
                    }
                    catch { }
                }

                // Obtain QuickBooksService type and create an instance via Activator to avoid direct compile-time coupling
                var qbImplType = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("WileyWidget.Services.QuickBooksService"))
                    .FirstOrDefault(t => t != null);

                var loggerType = typeof(ILogger<>).MakeGenericType(qbImplType ?? typeof(object));
                var logger = sp.GetRequiredService(loggerType);
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpClientFactory.CreateClient("QuickBooks");

                if (qbImplType != null)
                {
                    // Try to construct using the (SettingsService, ISecretVaultService, ILogger<QuickBooksService>, HttpClient) signature
                    try
                    {
                        object? instance;
                        if (secretVaultService != null)
                        {
                            instance = Activator.CreateInstance(qbImplType, settings, secretVaultService, logger, httpClient);
                        }
                        else
                        {
                            instance = Activator.CreateInstance(qbImplType, settings, null, logger, httpClient);
                        }

                        if (instance != null)
                            return instance;
                    }
                    catch { }
                }

                // Last-resort: try to resolve an existing registration from DI container
                var resolved = sp.GetService(qbInterfaceType);
                return resolved!;
            });
        }

        services.AddScoped<IAIService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<IAIService>>();
            var configuration = sp.GetRequiredService<IConfiguration>();
            var secretVaultService = sp.GetService<ISecretVaultService>();

            // Priority order: Environment variable -> local secret vault -> appsettings
            var xaiApiKey = Environment.GetEnvironmentVariable("XAI_API_KEY") ??
                           TryGetFromSecretVaultAsync(secretVaultService, "XAI-API-KEY", logger).GetAwaiter().GetResult() ??
                           configuration["XAI:ApiKey"];

            var requireAi = string.Equals(Environment.GetEnvironmentVariable("REQUIRE_AI_SERVICE"), "true", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(configuration["XAI:RequireService"], "true", StringComparison.OrdinalIgnoreCase);

            // Log configuration status
            logger.LogInformation("🤖 XAI CONFIGURATION: API_KEY_SET={ApiKeySet}, REQUIRE_AI={RequireAi}, API_KEY_LENGTH={Length}, SOURCE={Source}",
                !string.IsNullOrEmpty(xaiApiKey) && xaiApiKey != "${XAI_API_KEY}",
                requireAi,
                string.IsNullOrEmpty(xaiApiKey) ? 0 : xaiApiKey.Length,
                GetApiKeySource(xaiApiKey));

            if (string.IsNullOrEmpty(xaiApiKey) || xaiApiKey == "${XAI_API_KEY}")
            {
                if (requireAi)
                {
                    logger.LogError("AI service required but XAI_API_KEY not set. Falling back to stub; functionality limited.");
                }
                else
                {
                    logger.LogWarning("XAI_API_KEY not set. Using DevNullAIService stub. Configure XAI:ApiKey in appsettings.json or set XAI_API_KEY environment variable.");
                }
                return new DevNullAIService();
            }

            // Real service initialization
            logger.LogInformation("Initializing XAIService with provided API key (length {Len}).", xaiApiKey.Length);
            try
            {
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                var contextService = sp.GetRequiredService<IWileyWidgetContextService>();
                var aiLoggingService = sp.GetRequiredService<IAILoggingService>();
                var memoryCache = sp.GetRequiredService<IMemoryCache>();
                return new XAIService(httpClientFactory, configuration, sp.GetRequiredService<ILogger<XAIService>>(), contextService, aiLoggingService, memoryCache);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to initialize XAIService. Falling back to DevNullAIService");
                return new DevNullAIService();
            }
        });

        // Register as Singleton for better performance and state management
        services.TryAddSingleton<IChargeCalculatorService, ServiceChargeCalculatorService>();
        services.TryAddSingleton<IWhatIfScenarioEngine, WhatIfScenarioEngine>();

        // Register FiscalYearSettings as singleton (configuration data)
        services.AddSingleton<FiscalYearSettings>();

        // Register health check configuration (service lifetime singleton)
        services.AddSingleton<Models.HealthCheckConfiguration>();
        // NOTE: HealthCheckService is registered as a singleton in WPF hosting extensions.
        // Do not register it here to avoid conflicting lifetimes.
    }

    private static async Task<string?> TryGetFromSecretVaultAsync(object? secretVaultService, string secretName, ILogger logger)
    {
        // If no secret vault available, return null
        if (secretVaultService == null)
            return null;

        try
        {
            // Use dynamic invocation to call GetSecretAsync on the runtime-resolved secret vault instance.
            dynamic dyn = secretVaultService;
            Task<string?> getSecretTask = dyn.GetSecretAsync(secretName);
            var secret = await getSecretTask.ConfigureAwait(false);

            if (!string.IsNullOrEmpty(secret))
            {
                logger.LogInformation("Retrieved XAI API key from local secret vault");
                return secret;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to retrieve XAI API key from secret vault, trying next source");
        }
        return null;
    }

    private static string GetApiKeySource(string? apiKey)
    {
        if (string.IsNullOrEmpty(apiKey) || apiKey == "${XAI_API_KEY}")
            return "None";

        if (Environment.GetEnvironmentVariable("XAI_API_KEY") == apiKey)
            return "Environment";

        return "SecretVault"; // Since we check env first, if we get here it must be from the local vault
    }

    /// <summary>
    /// Configures additional DbContext options
    /// </summary>
    private static void ConfigureDbContextOptions(DbContextOptionsBuilder options)
    {
        // Add any additional configuration here
        // This method can be extended for different environments

#if DEBUG
        // Enable detailed errors in development
        options.EnableDetailedErrors();
        options.EnableSensitiveDataLogging();
#endif
    }

    /// <summary>
    /// Ensures the database is created and migrated with robust retry logic and metrics
    /// Call this during application startup
    /// </summary>
    public static async Task EnsureDatabaseCreatedAsync(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger(nameof(DatabaseConfiguration));
        var metricsService = serviceProvider.GetService<ApplicationMetricsService>();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var config = serviceProvider.GetRequiredService<IConfiguration>();
        var hostEnvironment = serviceProvider.GetService<IHostEnvironment>();
        var environmentName = hostEnvironment?.EnvironmentName ?? "Production";
        var stopwatch = Stopwatch.StartNew();

        try
        {
            logger.LogInformation("Starting database initialization with retry logic for {Environment}", environmentName);

            // Get local connection string and log the server for diagnostics
            var connectionString = BuildEnterpriseConnectionString(config, logger, environmentName);
            logger.LogInformation("SQL Server target: {Server}", ExtractServerFromConnectionString(connectionString));

            // Apply SQL Server retry policy
            IAsyncPolicy policy = DevelopmentRetryPolicy;

            await policy.ExecuteAsync(async () =>
            {
                using var scope = scopeFactory.CreateScope();
                var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
                await using var context = await contextFactory.CreateDbContextAsync();

                logger.LogInformation("Initializing SQL Server database");

                // Check if database connection is available
                var canConnect = await context.Database.CanConnectAsync();
                logger.LogInformation("Database.CanConnectAsync() returned: {CanConnect}", canConnect);
                if (!canConnect)
                {
                    logger.LogError("Cannot connect to SQL Server database - check connection string and server availability");
                    throw new InvalidOperationException("SQL Server database connection failed. Check the configured connection string and ensure the database is accessible.");
                }

                logger.LogInformation("SQL Server database connection verified - applying migrations");

                // Check for pending model changes (EF Core 8.0+)
                // This helps detect when migrations are needed but not yet created
                try
                {
                    var hasPendingChanges = context.Database.HasPendingModelChanges();
                    if (hasPendingChanges)
                    {
                        logger.LogWarning("⚠️ Pending model changes detected! The model has changed since the last migration. Run 'dotnet ef migrations add <MigrationName>' to create a new migration.");
                    }
                    else
                    {
                        logger.LogInformation("✓ No pending model changes - database schema is up to date with the model");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Unable to check for pending model changes - this may indicate a migration issue");
                }

                // Apply migrations
                await context.Database.MigrateAsync();

                // Temporary additive schema hotfixes to unblock runtime until full migrations are reconciled
                await ApplySchemaHotfixesAsync(context, logger);

                // Run seeding if needed
                await RunSeedingIfNeededAsync(context, logger, metricsService);
            });

            logger.LogInformation("Database initialization completed successfully in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            metricsService?.RecordMigration(stopwatch.Elapsed.TotalMilliseconds, true);
        }
        catch (CircuitBreakerOpenException ex)
        {
            logger.LogError(ex, "Database initialization failed - circuit breaker is open after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            metricsService?.RecordMigration(stopwatch.Elapsed.TotalMilliseconds, false);
            throw new InvalidOperationException("Database initialization failed due to repeated connection issues", ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database initialization failed after {ElapsedMs}ms: {Message}", stopwatch.ElapsedMilliseconds, ex.Message);
            metricsService?.RecordMigration(stopwatch.Elapsed.TotalMilliseconds, false);
            throw;
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    /// <summary>
    /// Applies small, additive SQL changes that are safe to run repeatedly.
    /// This unblocks startup when specific columns are missing in environments where
    /// migrations history has drifted. Once migrations are fully aligned, this can be removed.
    /// </summary>
    private static async Task ApplySchemaHotfixesAsync(AppDbContext context, ILogger logger)
    {
        try
        {
            // Add QBO client columns to AppSettings if missing
            var addQboClientId = @"IF COL_LENGTH('dbo.AppSettings','QboClientId') IS NULL
                                    ALTER TABLE dbo.AppSettings ADD QboClientId NVARCHAR(MAX) NULL;";
            var addQboClientSecret = @"IF COL_LENGTH('dbo.AppSettings','QboClientSecret') IS NULL
                                       ALTER TABLE dbo.AppSettings ADD QboClientSecret NVARCHAR(MAX) NULL;";

            // Add computed mirror for value object AccountNumber if missing (used by queries)
            var addAccountNumberValue = @"IF COL_LENGTH('dbo.MunicipalAccounts','AccountNumber_Value') IS NULL
                                          ALTER TABLE dbo.MunicipalAccounts ADD [AccountNumber_Value] AS ([AccountNumber]);";

            await context.Database.ExecuteSqlRawAsync(addQboClientId);
            await context.Database.ExecuteSqlRawAsync(addQboClientSecret);
            await context.Database.ExecuteSqlRawAsync(addAccountNumberValue);

            logger.LogInformation("Schema hotfixes applied (if needed): QboClientId, QboClientSecret, AccountNumber_Value");
        }
        catch (Exception ex)
        {
            // Do not fail startup if hotfixes cannot be applied; just log a warning.
            logger.LogWarning(ex, "Schema hotfix execution failed; proceeding without hotfixes");
        }
    }

    /// <summary>
    /// Ensures the local database is initialized at startup.
    /// Safe to call multiple times.
    /// </summary>
    public static async Task EnsureLocalDatabaseInitializedAsync(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger(nameof(DatabaseConfiguration));
        try
        {
            logger.LogInformation("Ensuring local database is initialized");
            await EnsureDatabaseCreatedAsync(serviceProvider);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Local database initialization failed");
            throw;
        }
    }

    /// <summary>
    /// Runs database seeding if the database is empty
    /// </summary>
    private static async Task RunSeedingIfNeededAsync(AppDbContext context, ILogger logger, ApplicationMetricsService? metricsService)
    {
        try
        {
            // Check if database needs seeding
            if (await context.Enterprises.AnyAsync())
            {
                logger.LogDebug("Database already contains data, skipping seeding");
                return;
            }

            logger.LogInformation("Database is empty, running seeding process");

            var seeder = new DatabaseSeeder(context);
            await seeder.SeedAsync();

            logger.LogInformation("Database seeding completed successfully");
            metricsService?.RecordSeeding(true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Database seeding failed, but continuing with application startup");
            metricsService?.RecordSeeding(false);
            // Don't throw - seeding failure shouldn't prevent app startup
        }
    }

    /// Validates the database schema by checking if required tables exist
    /// </summary>
    public static async Task ValidateDatabaseSchemaAsync(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger(nameof(DatabaseConfiguration));
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var scope = scopeFactory.CreateScope();
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();

            // Check if Enterprises table exists by attempting a simple query
            var enterpriseCount = await context.Enterprises.CountAsync();
            logger.LogInformation("Database schema validation passed: {EnterpriseCount} enterprises found in {ElapsedMs}ms",
                enterpriseCount, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Database schema validation failed after {ElapsedMs}ms: {Message}. Application will continue with limited database functionality",
                stopwatch.ElapsedMilliseconds, ex.Message);
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    /// <summary>
    /// Configures enterprise-grade health checks
    /// </summary>
    private static void ConfigureEnterpriseHealthChecks(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHealthChecks()
            // Database health check with enterprise diagnostics
            .AddCheck<DatabaseHealthCheck>("Database",
                tags: new[] { "database", "sql", "enterprise" })
            // SQL Server health check with connection string validation and connectivity test
            .AddCheck<SqlServerHealthCheck>("SQL Server",
                tags: new[] { "database", "sql", "server" })
            // Memory health check
            .AddCheck<MemoryHealthCheck>("Memory",
                tags: new[] { "resources", "memory" })
            // Custom application health check
            .AddCheck<EnterpriseApplicationHealthCheck>("Enterprise Application",
                tags: new[] { "application", "enterprise" });

        // DatabaseConnectivityDiagnostic removed - functionality covered by DatabaseHealthCheck
    }
}

/// <summary>
/// Local dev stub to avoid AI dependency in development environments.
/// </summary>
internal sealed class DevNullAIService : WileyWidget.Services.Abstractions.IAIService
{
    public Task<string> GetInsightsAsync(string context, string question, CancellationToken cancellationToken = default) =>
        Task.FromResult("[Dev Stub] AI insights disabled. Set XAI_API_KEY to enable.");

    public Task<string> AnalyzeDataAsync(string data, string analysisType, CancellationToken cancellationToken = default) =>
        Task.FromResult("[Dev Stub] AI analysis disabled.");

    public Task<string> ReviewApplicationAreaAsync(string areaName, string currentState, CancellationToken cancellationToken = default) =>
        Task.FromResult("[Dev Stub] AI review disabled.");

    public Task<string> GenerateMockDataSuggestionsAsync(string dataType, string requirements, CancellationToken cancellationToken = default) =>
        Task.FromResult("[Dev Stub] AI mock data generation disabled.");

    public Task<AIResponseResult> GetInsightsWithStatusAsync(string context, string question, CancellationToken cancellationToken = default)
        => Task.FromResult(new AIResponseResult("[Dev Stub] AI insights disabled. Set XAI_API_KEY to enable.", 200));

    public Task<AIResponseResult> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
        => Task.FromResult(new AIResponseResult("Dev stub: validation not available in development.", 403, "AuthFailure", null));

    public Task UpdateApiKeyAsync(string newApiKey)
    {
        // No-op for dev stub
        return Task.CompletedTask;
    }

    public Task<AIResponseResult> SendPromptAsync(string prompt, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new AIResponseResult("[Dev Stub] Prompt disabled.", 200));
    }
}

/// <summary>
/// Custom memory health check
/// </summary>
public class MemoryHealthCheck : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    public Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var allocated = GC.GetTotalMemory(forceFullCollection: false);
        var threshold = 100 * 1024 * 1024; // 100 MB

        if (allocated > threshold)
        {
            return Task.FromResult(Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded(
                $"High memory usage: {allocated / 1024 / 1024} MB"));
        }

        return Task.FromResult(Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(
            $"Memory usage normal: {allocated / 1024 / 1024} MB"));
    }
}

/// <summary>
/// Custom database health check for SQL Server
/// </summary>
public class DatabaseHealthCheck : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public DatabaseHealthCheck(IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            // Simple query to test connectivity and authentication
            var count = await dbContext.Enterprises.CountAsync(cancellationToken);

            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(
                $"Database connection successful - {count} enterprises found");
        }
        catch (Exception ex)
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
                $"Database health check failed: {ex.Message}");
        }
    }
}

/// <summary>
/// Custom health check for enterprise application status
/// </summary>
public class EnterpriseApplicationHealthCheck : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    private readonly IServiceScopeFactory _scopeFactory;

    public EnterpriseApplicationHealthCheck(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if we can create a DbContext (tests DI and basic connectivity)
            using var scope = _scopeFactory.CreateScope();
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            await using var dbContext = await contextFactory.CreateDbContextAsync(cancellationToken);

            // Simple database connectivity test
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
            {
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
                    "Cannot connect to database");
            }

            // Check if critical tables exist
            var enterpriseCount = await dbContext.Enterprises.CountAsync(cancellationToken);

            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(
                $"Application healthy. Database accessible with {enterpriseCount} enterprises.");
        }
        catch (Exception ex)
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
                $"Application health check failed: {ex.Message}");
        }
    }
}

/// <summary>
/// SQL Server health check with connection string validation and connectivity testing
/// for local or hosted environments.
/// </summary>
public class SqlServerHealthCheck : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    private readonly IConfiguration _configuration;

    public SqlServerHealthCheck(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connectionString = _configuration.GetConnectionString("AzureConnection") ??
                                    _configuration.GetConnectionString("DefaultConnection");

            // Check if a connection string is configured
            if (string.IsNullOrEmpty(connectionString))
            {
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
            "SQL Server connection string not configured");
            }

            // Validate connection string format
            var validationResult = ValidateSqlConnectionString(connectionString);
            if (!validationResult.IsValid)
            {
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
            $"Invalid SQL connection string format: {validationResult.ErrorMessage}");
            }

            // Test connectivity using ADO.NET
            using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            // Simple query to test database access
            using var command = new Microsoft.Data.SqlClient.SqlCommand("SELECT 1", connection);
            var result = await command.ExecuteScalarAsync(cancellationToken);

            if (result == null || (int)result != 1)
            {
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
                    "SQL Server connectivity test failed - unexpected query result");
            }

            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(
                "SQL Server connection successful and validated");
        }
        catch (Exception ex)
        {
            // Improved diagnostics: differentiate credential vs connectivity vs general errors
            string category = "General";
            if (ex is Microsoft.Data.SqlClient.SqlException sqlEx)
            {
                if (sqlEx.Message.Contains("Login failed", StringComparison.OrdinalIgnoreCase))
                    category = "Authentication";
                else if (sqlEx.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                    category = "Timeout";
                else if (sqlEx.Message.Contains("single-user", StringComparison.OrdinalIgnoreCase))
                    category = "SingleUserMode";
            }
            else if (ex is TimeoutException)
            {
                category = "Timeout";
            }

            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
                $"SQL Server health check failed ({category}): {ex.Message}");
        }
    }

    private (bool IsValid, string ErrorMessage) ValidateSqlConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return (false, "Connection string is empty");

        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);

        bool hasServer = false;
        bool hasInitialCatalog = false;
        bool hasUserId = false;
        bool hasPassword = false;
        bool integratedSecurity = false;

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("Server=", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
            {
                hasServer = true;
            }
            else if (trimmed.StartsWith("Initial Catalog=", StringComparison.OrdinalIgnoreCase) ||
                     trimmed.StartsWith("Database=", StringComparison.OrdinalIgnoreCase))
            {
                hasInitialCatalog = true;
            }
            else if (trimmed.StartsWith("User ID=", StringComparison.OrdinalIgnoreCase) ||
                     trimmed.StartsWith("Uid=", StringComparison.OrdinalIgnoreCase))
            {
                hasUserId = true;
            }
            else if (trimmed.StartsWith("Password=", StringComparison.OrdinalIgnoreCase) ||
                     trimmed.StartsWith("Pwd=", StringComparison.OrdinalIgnoreCase))
            {
                hasPassword = true;
            }
            else if (trimmed.StartsWith("Trusted_Connection=", StringComparison.OrdinalIgnoreCase) ||
                     trimmed.StartsWith("Integrated Security=", StringComparison.OrdinalIgnoreCase))
            {
                if (trimmed.Contains("True", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.Contains("SSPI", StringComparison.OrdinalIgnoreCase))
                {
                    integratedSecurity = true;
                }
            }
        }

        // If using Integrated Security we don't require explicit User ID / Password
        if (integratedSecurity)
        {
            hasUserId = true;
            hasPassword = true;
        }

        if (!hasServer)
            return (false, "Server parameter is required");
        if (!hasInitialCatalog)
            return (false, "Initial Catalog / Database parameter is required");
        if (!hasUserId)
            return (false, integratedSecurity ? "Integrated security flag missing" : "User ID parameter is required");
        if (!hasPassword)
            return (false, integratedSecurity ? "Integrated security flag missing" : "Password parameter is required");

        return (true, string.Empty);
    }
}
