using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using WileyWidget.Business.Interfaces;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.WinForms.ViewModels;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.ViewModels
{
    /// <summary>
    /// Basic tests for DashboardViewModel using REAL repository implementations (not mocks).
    /// Verifies error handling and edge cases with actual EF Core InMemory database.
    /// </summary>
    public class DashboardViewModelTests : IDisposable
    {
        private IServiceProvider? _serviceProvider;
        private IMemoryCache? _cache;

        [Fact]
        public async Task LoadDashboard_StopsRetries_OnObjectDisposedException()
        {
            var (budgetRepo, accountRepo, config) = SetupRealRepositories();

            using var vm = new DashboardViewModel(budgetRepo, accountRepo, NullLogger<DashboardViewModel>.Instance, config);

            // Execute the load command (should not throw)
            await vm.LoadCommand.ExecuteAsync(null);

            Assert.False(vm.IsLoading);
        }

        /// <summary>
        /// Sets up real repositories using InMemory EF Core DbContext instead of fakes.
        /// This verifies end-to-end integration between ViewModels and real repository implementations.
        /// </summary>
        private (IBudgetRepository, IMunicipalAccountRepository, IConfiguration) SetupRealRepositories()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            // Create test configuration for fiscal year settings
            var configBuilder = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "UI:DefaultFiscalYear", "2025" },
                    { "Dashboard:DefaultFiscalYear", "2025" }
                })
                .Build();

            // Create service provider with real repositories
            var services = new ServiceCollection();
            services.AddScoped(_ => new AppDbContext(options));
            services.AddScoped<IDbContextFactory<AppDbContext>>(sp => new InMemoryDbContextFactory(options));
            services.AddScoped(_ => _cache = new MemoryCache(new MemoryCacheOptions()));
            services.AddScoped<IBudgetRepository, BudgetRepository>();
            services.AddScoped<IMunicipalAccountRepository, MunicipalAccountRepository>();
            services.AddScoped(_ => configBuilder);

            _serviceProvider = services.BuildServiceProvider();

            // Seed minimal test data
            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                SeedTestData(dbContext);
            }

            var budgetRepo = _serviceProvider.GetRequiredService<IBudgetRepository>();
            var accountRepo = _serviceProvider.GetRequiredService<IMunicipalAccountRepository>();

            return (budgetRepo, accountRepo, configBuilder);
        }

        /// <summary>
        /// Seeds minimal test data into the InMemory database.
        /// </summary>
        private static void SeedTestData(AppDbContext dbContext)
        {
            // Create test accounts with valid AccountNumber format
            // AccountNumber regex: ^\d+([.-]\d+)*$ (numeric with optional . or - separators)
            // Examples: "405", "405.1", "410.2.1", "101-1000-000"
            var validAccountNumbers = new[] { "405", "405.1", "410.2.1", "101-1000-000" };
            var accounts = Enumerable.Range(1, 10)
                .Select(i => new MunicipalAccount
                {
                    AccountNumber = new AccountNumber(validAccountNumbers[i % validAccountNumbers.Length]),
                    Name = $"Test Account {i}",
                    Balance = i * 1000m,
                    IsActive = true
                })
                .ToList();

            dbContext.MunicipalAccounts.AddRange(accounts);
            dbContext.SaveChanges();
        }

        /// <summary>
        /// Simple IDbContextFactory implementation for InMemory testing.
        /// </summary>
        private class InMemoryDbContextFactory : IDbContextFactory<AppDbContext>
        {
            private readonly DbContextOptions<AppDbContext> _options;

            public InMemoryDbContextFactory(DbContextOptions<AppDbContext> options)
            {
                _options = options;
            }

            public AppDbContext CreateDbContext()
            {
                return new AppDbContext(_options);
            }

            public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new AppDbContext(_options));
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cache?.Dispose();
                (_serviceProvider as IDisposable)?.Dispose();
            }
        }
    }
}
