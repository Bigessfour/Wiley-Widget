#nullable enable

using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Diagnostics;
using WileyWidget.Data.Resilience;

namespace WileyWidget.Data.Interceptors;

/// <summary>
/// DbCommand interceptor that applies database resilience policies to EF Core commands.
/// Uses DatabaseResiliencePolicy for read/write classification and Polly-backed retries.
/// </summary>
public sealed class DatabaseResilienceInterceptor : DbCommandInterceptor
{
    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        if (!ShouldApply(command))
        {
            return base.ReaderExecuting(command, eventData, result);
        }

        var reader = DatabaseResiliencePolicy.Execute(() => command.ExecuteReader());
        return InterceptionResult<DbDataReader>.SuppressWithResult(reader);
    }

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        if (!ShouldApply(command))
        {
            return await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        var reader = await DatabaseResiliencePolicy.ExecuteAsync(
            () => command.ExecuteReaderAsync(cancellationToken)).ConfigureAwait(false);
        return InterceptionResult<DbDataReader>.SuppressWithResult(reader);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        if (!ShouldApply(command))
        {
            return base.NonQueryExecuting(command, eventData, result);
        }

        var outcome = DatabaseResiliencePolicy.Execute(command.ExecuteNonQuery);
        return InterceptionResult<int>.SuppressWithResult(outcome);
    }

    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (!ShouldApply(command))
        {
            return await base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        var outcome = await DatabaseResiliencePolicy.ExecuteAsync(
            () => command.ExecuteNonQueryAsync(cancellationToken)).ConfigureAwait(false);
        return InterceptionResult<int>.SuppressWithResult(outcome);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result)
    {
        if (!ShouldApply(command))
        {
            return base.ScalarExecuting(command, eventData, result);
        }

        var outcome = DatabaseResiliencePolicy.Execute(() => command.ExecuteScalar());
        return InterceptionResult<object>.SuppressWithResult(outcome!);
    }

    public override async ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        if (!ShouldApply(command))
        {
            return await base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
        }

        var outcome = await DatabaseResiliencePolicy.ExecuteAsync(
            () => command.ExecuteScalarAsync(cancellationToken)).ConfigureAwait(false);
        return InterceptionResult<object>.SuppressWithResult(outcome!);
    }

    private static bool ShouldApply(DbCommand command)
    {
        return command is SqlCommand;
    }

    private static bool UseReadPolicy(DbCommand command)
    {
        if (command.CommandType != CommandType.Text)
        {
            return false;
        }

        var text = command.CommandText?.TrimStart();
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        return text.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("WITH", StringComparison.OrdinalIgnoreCase);
    }
}
