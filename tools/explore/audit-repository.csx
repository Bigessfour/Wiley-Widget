// Exploratory harness for AuditRepository
// Prerequisite: dotnet build -c Debug ensures binaries in ../src/**/bin/Debug
#r "nuget: Microsoft.EntityFrameworkCore.InMemory, 8.0.2"
#r "nuget: Microsoft.Extensions.Logging.Console, 8.0.0"
#r "nuget: Microsoft.Extensions.DependencyInjection, 8.0.0"
#r "nuget: Microsoft.Extensions.Caching.Memory, 8.0.0"
#r "../../src/WileyWidget.Models/bin/Debug/net9.0-windows10.0.19041.0/win-x64/WileyWidget.Models.dll"
#r "../../src/WileyWidget.Business/bin/Debug/net9.0-windows10.0.19041.0/win-x64/WileyWidget.Business.dll"
#r "../../src/WileyWidget.Data/bin/Debug/net9.0-windows10.0.19041.0/win-x64/WileyWidget.Data.dll"

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WileyWidget.Data;
using WileyWidget.Models;

var services = new ServiceCollection();
services.AddLogging(builder => builder
    .AddSimpleConsole(options => options.SingleLine = true)
    .SetMinimumLevel(LogLevel.Debug));
services.AddMemoryCache();
services.AddDbContextFactory<AppDbContext>(options =>
{
    options.UseInMemoryDatabase("AuditRepoTests");
});

var provider = services.BuildServiceProvider();
var factory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
var cache = provider.GetRequiredService<IMemoryCache>();

await SeedAuditEntriesAsync(factory);

var repository = new AuditRepository(factory, cache);

var start = DateTime.UtcNow.AddDays(-5);
var end = DateTime.UtcNow.AddDays(1);

var recentEntries = await repository.GetAuditTrailAsync(start, end);
Console.WriteLine($"Recent entries: {recentEntries.Count()}");

var budgetEntries = await repository.GetAuditTrailForEntityAsync("BudgetEntry", start, end);
Console.WriteLine($"BudgetEntry entries: {budgetEntries.Count()}");

var specificEntityEntries = await repository.GetAuditTrailForEntityAsync("UtilityBill", 201, start, end);
Console.WriteLine($"UtilityBill 201 entries: {specificEntityEntries.Count()}");

var (pageItems, totalCount) = await repository.GetPagedAsync(
    pageNumber: 1,
    pageSize: 3,
    sortBy: "timestamp",
    sortDescending: true,
    startDate: start,
    endDate: end);

Console.WriteLine($"Paged entries returned {pageItems.Count()} items out of {totalCount}");
foreach (var entry in pageItems)
{
    Console.WriteLine($"  {entry.Timestamp:u} | {entry.EntityType}#{entry.EntityId} | {entry.Action} by {entry.User}");
}

var (entityOrdered, _) = await repository.GetPagedAsync(
    pageNumber: 1,
    pageSize: 5,
    sortBy: "entitytype",
    sortDescending: false);

Console.WriteLine("Entity type ascending (first page):");
foreach (var entry in entityOrdered)
{
    Console.WriteLine($"  {entry.EntityType} | {entry.Action}");
}

if (provider is IDisposable disposable)
{
    disposable.Dispose();
}

static async Task SeedAuditEntriesAsync(IDbContextFactory<AppDbContext> factory)
{
    await using var context = await factory.CreateDbContextAsync();
    await context.Database.EnsureDeletedAsync();
    await context.Database.EnsureCreatedAsync();

    var now = DateTime.UtcNow;
    var entries = new List<AuditEntry>
    {
        new()
        {
            EntityType = "BudgetEntry",
            EntityId = 101,
            Action = "CREATE",
            User = "system",
            Timestamp = now.AddDays(-4),
            NewValues = "{\"Amount\":1000}"
        },
        new()
        {
            EntityType = "BudgetEntry",
            EntityId = 101,
            Action = "UPDATE",
            User = "finance-admin",
            Timestamp = now.AddDays(-2),
            OldValues = "{\"Amount\":1000}",
            NewValues = "{\"Amount\":1250}"
        },
        new()
        {
            EntityType = "UtilityBill",
            EntityId = 201,
            Action = "DELETE",
            User = "auditor",
            Timestamp = now.AddDays(-1),
            Changes = "Soft delete requested"
        },
        new()
        {
            EntityType = "UtilityCustomer",
            EntityId = 301,
            Action = "UPDATE",
            User = "support",
            Timestamp = now.AddHours(-6),
            Changes = "Status changed to Active"
        },
        new()
        {
            EntityType = "Enterprise",
            EntityId = 401,
            Action = "CREATE",
            User = "system",
            Timestamp = now.AddMinutes(-30)
        }
    };

    context.AuditEntries.AddRange(entries);
    await context.SaveChangesAsync();
}
