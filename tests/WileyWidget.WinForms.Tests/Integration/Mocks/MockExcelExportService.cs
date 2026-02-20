#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WileyWidget.Models;
using WileyWidget.Services.Export;

namespace WileyWidget.WinForms.Tests.Integration.Mocks;

/// <summary>
/// Mock <see cref="IExcelExportService"/> that no-ops all exports without writing real files.
/// Suitable for integration tests where export side-effects must be suppressed.
/// </summary>
public sealed class MockExcelExportService : IExcelExportService
{
    public Task<string> ExportBudgetEntriesAsync(
        IEnumerable<BudgetEntry> entries,
        string filePath,
        CancellationToken cancellationToken = default)
        => Task.FromResult(filePath);

    public Task<string> ExportMunicipalAccountsAsync(
        IEnumerable<MunicipalAccount> accounts,
        string filePath,
        CancellationToken cancellationToken = default)
        => Task.FromResult(filePath);

    public Task<string> ExportEnterpriseDataAsync<T>(
        IEnumerable<T> data,
        string filePath) where T : class
        => Task.FromResult(filePath);

    public Task<string> ExportGenericDataAsync<T>(
        IEnumerable<T> data,
        string filePath,
        string worksheetName,
        Dictionary<string, Func<T, object>> columns)
        => Task.FromResult(filePath);

    public Task<string> ExportBudgetForecastAsync(
        BudgetForecastResult forecast,
        string filePath,
        CancellationToken cancellationToken = default)
        => Task.FromResult(filePath);
}
