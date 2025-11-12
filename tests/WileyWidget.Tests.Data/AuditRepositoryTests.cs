using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Xunit;
using WileyWidget.Data;
using WileyWidget.Models;

namespace WileyWidget.Tests.Data
{
    /// <summary>
    /// Unit tests for AuditRepository
    /// Focus on testing repository methods that are currently untested
    /// </summary>
    public class AuditRepositoryTests : IDisposable
    {
        private readonly DbConnection _connection;
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly AuditRepository _repository;

        public AuditRepositoryTests()
        {
            // Create and open SQLite in-memory connection
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            
            // Disable foreign key constraints for test flexibility
            var command = _connection.CreateCommand();
            command.CommandText = "PRAGMA foreign_keys = OFF;";
            command.ExecuteNonQuery();
            
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.AmbientTransactionWarning))
                .Options;
            _context = new AppDbContext(options);
            
            // Ensure database schema is created
            _context.Database.EnsureCreated();
            
            // SQLite workaround: Update RowVersion for all MunicipalAccounts with non-empty values
            // This prevents "NOT NULL constraint failed: MunicipalAccounts.RowVersion" errors
            _context.MunicipalAccounts.ToList().ForEach(ma => 
            {
                if (ma.RowVersion == null || ma.RowVersion.Length == 0)
                {
                    ma.RowVersion = new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 };
                }
            });
            _context.SaveChanges();
            
            // Use real MemoryCache instead of mock
            _cache = new MemoryCache(new MemoryCacheOptions());
            
            var contextFactoryMock = new Mock<IDbContextFactory<AppDbContext>>();
            contextFactoryMock.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => {
                    // Return new context with same SQLite connection
                    return new AppDbContext(options);
                });
            _contextFactory = contextFactoryMock.Object;
            _repository = new AuditRepository(_contextFactory, _cache);
        }

        public void Dispose()
        {
            _context.Dispose();
            _cache.Dispose();
            _connection.Dispose();
        }

        [Fact]
        public async Task GetAuditTrailAsync_ReturnsEntriesWithinDateRange()
        {
            // Arrange
            var startDate = new DateTime(2024, 1, 1);
            var endDate = new DateTime(2024, 12, 31);
            _context.AuditEntries.AddRange(
                new AuditEntry { Id = 1, Timestamp = new DateTime(2024, 6, 15), EntityType = "Test", Action = "Create" },
                new AuditEntry { Id = 2, Timestamp = new DateTime(2024, 7, 20), EntityType = "Test", Action = "Update" }
            );
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetAuditTrailAsync(startDate, endDate);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count());
        }

        [Fact]
        public async Task GetAuditTrailForEntityAsync_ReturnsEntriesForSpecificEntity()
        {
            // Arrange
            var entityType = "TestEntity";
            var startDate = new DateTime(2024, 1, 1);
            var endDate = new DateTime(2024, 12, 31);
            _context.AuditEntries.Add(
                new AuditEntry { Id = 1, EntityType = entityType, Timestamp = new DateTime(2024, 6, 15), Action = "Create" }
            );
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetAuditTrailForEntityAsync(entityType, startDate, endDate);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(entityType, result.First().EntityType);
        }

        [Fact]
        public async Task GetAuditTrailForEntityAsync_WithEntityId_ReturnsEntriesForSpecificEntity()
        {
            // Arrange
            var entityType = "TestEntity";
            var entityId = 123;
            var startDate = new DateTime(2024, 1, 1);
            var endDate = new DateTime(2024, 12, 31);
            _context.AuditEntries.Add(
                new AuditEntry { Id = 1, EntityType = entityType, EntityId = entityId, Timestamp = new DateTime(2024, 6, 15), Action = "Create" }
            );
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetAuditTrailForEntityAsync(entityType, entityId, startDate, endDate);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(entityType, result.First().EntityType);
            Assert.Equal(entityId, result.First().EntityId);
        }

        [Fact]
        public async Task AddAuditEntryAsync_AddsEntryToDatabase()
        {
            // Arrange
            var auditEntry = new AuditEntry
            {
                EntityType = "TestEntity",
                EntityId = 123,
                Action = "Create",
                User = "TestUser",
                Timestamp = DateTime.UtcNow,
                OldValues = "old",
                NewValues = "new"
            };

            // Act
            await _repository.AddAuditEntryAsync(auditEntry);

            // Assert - Use fresh context to verify data
            using var verifyContext = await _contextFactory.CreateDbContextAsync();
            var entries = verifyContext.AuditEntries.ToList();
            Assert.Single(entries);
            Assert.Equal("TestEntity", entries[0].EntityType);
        }

        [Fact]
        public async Task GetPagedAsync_ReturnsPagedResultsWithTotalCount()
        {
            // Arrange
            var pageNumber = 1;
            var pageSize = 10;
            var sortBy = "entitytype";
            var sortDescending = true;
            var startDate = new DateTime(2024, 1, 1);
            var endDate = new DateTime(2024, 12, 31);
            var entityType = "TestEntity";

            _context.AuditEntries.AddRange(
                new AuditEntry { Id = 1, EntityType = entityType, Timestamp = new DateTime(2024, 6, 15), Action = "Create" },
                new AuditEntry { Id = 2, EntityType = entityType, Timestamp = new DateTime(2024, 6, 20), Action = "Update" }
            );
            await _context.SaveChangesAsync();

            // Act
            var (items, totalCount) = await _repository.GetPagedAsync(pageNumber, pageSize, sortBy, sortDescending, startDate, endDate, entityType);

            // Assert
            Assert.NotNull(items);
            Assert.Equal(2, totalCount);
        }
    }
}
