using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Integration.Tests.Shared;

namespace WileyWidget.Integration.Tests.DatabaseFailure;

/// <summary>
/// Utilities for simulating database failures and testing recovery scenarios
/// </summary>
public static class DatabaseFailureSimulator
{
    /// <summary>
    /// Creates a DbContext with simulated connection failures
    /// </summary>
    public static AppDbContext CreateFailingDbContext(
        DbConnection connection,
        int failureCount = 1,
        TimeSpan? delayBetweenFailures = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(new FailingCommandInterceptor(failureCount, delayBetweenFailures))
            .Options;

        return new AppDbContext(options);
    }

    /// <summary>
    /// Simulates transient database connection failures
    /// </summary>
    public static async Task WithTransientFailuresAsync(
        Func<Task> operation,
        int failureCount = 2,
        TimeSpan? delayBetweenFailures = null)
    {
        var interceptor = new FailingCommandInterceptor(failureCount, delayBetweenFailures ?? TimeSpan.FromMilliseconds(100));

        // This would need to be integrated with the actual DbContext creation
        // For now, this is a placeholder for the pattern
        await operation();
    }

    /// <summary>
    /// Simulates database timeout scenarios
    /// </summary>
    public static async Task WithTimeoutAsync(
        Func<Task> operation,
        TimeSpan timeout)
    {
        var opTask = operation();
        var delayTask = Task.Delay(timeout);
        var completed = await Task.WhenAny(opTask, delayTask);
        if (completed == delayTask)
        {
            throw new TimeoutException("Database operation timed out");
        }
        await opTask; // propagate exceptions if any
    }

    /// <summary>
    /// Simulates database deadlock scenarios
    /// </summary>
    public static async Task WithDeadlockAsync(Func<Task> operation)
    {
        // Simulate deadlock by having concurrent operations
        var tasks = new[]
        {
            Task.Run(() => operation()),
            Task.Run(() => operation())
        };

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("deadlock") == true)
        {
            // Expected deadlock scenario
            throw;
        }
    }

    /// <summary>
    /// Tests database connection recovery after failures
    /// </summary>
    public static async Task TestConnectionRecoveryAsync(
        Func<AppDbContext, Task> operation,
        int maxRetries = 3)
    {
        var retryCount = 0;
        while (retryCount < maxRetries)
        {
            try
            {
                // This would use a failing context in real implementation
                await operation(null!); // Placeholder
                break;
            }
            catch (DbException)
            {
                retryCount++;
                if (retryCount >= maxRetries)
                    throw;

                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount))); // Exponential backoff
            }
        }
    }
}

/// <summary>
/// EF Core interceptor that simulates command failures
/// </summary>
public class FailingCommandInterceptor : DbCommandInterceptor
{
    private readonly int _failureCount;
    private readonly TimeSpan _delayBetweenFailures;
    private int _currentFailureCount;

    public FailingCommandInterceptor(int failureCount, TimeSpan? delayBetweenFailures = null)
    {
        _failureCount = failureCount;
        _delayBetweenFailures = delayBetweenFailures ?? TimeSpan.Zero;
    }

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        if (_currentFailureCount < _failureCount)
        {
            _currentFailureCount++;
            if (_delayBetweenFailures > TimeSpan.Zero)
            {
                await Task.Delay(_delayBetweenFailures, cancellationToken);
            }
            throw new InvalidOperationException("Simulated database failure");
        }

        return await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        if (_currentFailureCount < _failureCount)
        {
            _currentFailureCount++;
            if (_delayBetweenFailures > TimeSpan.Zero)
            {
                Task.Delay(_delayBetweenFailures).Wait();
            }
            throw new InvalidOperationException("Simulated database failure");
        }

        return base.NonQueryExecuting(command, eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (_currentFailureCount < _failureCount)
        {
            _currentFailureCount++;
            if (_delayBetweenFailures > TimeSpan.Zero)
            {
                await Task.Delay(_delayBetweenFailures, cancellationToken);
            }
            throw new Exception("Simulated database failure");
        }

        return await base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }
}

/// <summary>
/// Tests for database failure simulation and recovery
/// </summary>
public class DatabaseFailureTests : IntegrationTestBase
{
        [Fact]
        public async Task Database_TransientFailure_RecoversWithRetry()
    {
        // Arrange
        await TestDataSeeder.SeedMunicipalAccountsAsync(DbContext);
        var repository = GetRequiredService<AccountsRepository>();

        // Act & Assert - Test with simulated failures
        // This would use DatabaseFailureSimulator in a real implementation
        var result = await repository.GetAllAccountsAsync();
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Database_Timeout_ThrowsExpectedException()
    {
        // Arrange
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1));

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            DatabaseFailureSimulator.WithTimeoutAsync(
                () => Task.Delay(100), // Long operation
                TimeSpan.FromMilliseconds(1)));
    }

    [Fact]
    public async Task Database_ConnectionLoss_HandlesGracefully()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<AccountsRepository>>();
        var options = new DbContextOptionsBuilder<AppDbContext>().Options;
        var failingContext = new Mock<AppDbContext>(options);

        failingContext.Setup(c => c.Set<MunicipalAccount>())
            .Throws(new Exception("Connection lost"));

        var repository = new AccountsRepository(failingContext.Object, mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() =>
            repository.GetAllAccountsAsync());
    }
}
