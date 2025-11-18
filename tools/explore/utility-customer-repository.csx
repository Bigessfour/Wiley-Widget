// Exploratory harness for UtilityCustomerRepository
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
    options.UseInMemoryDatabase("UtilityCustomerRepoTests");
});

var provider = services.BuildServiceProvider();
var factory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
var cache = provider.GetRequiredService<IMemoryCache>();
var logger = provider.GetRequiredService<ILogger<UtilityCustomerRepository>>();

await SeedCustomersAsync(factory);

var repository = new UtilityCustomerRepository(factory, logger, cache);

var firstFetch = await repository.GetAllAsync();
Console.WriteLine($"Initial fetch count: {firstFetch.Count()}");

var secondFetch = await repository.GetAllAsync();
Console.WriteLine($"Cache reuse detected: {ReferenceEquals(firstFetch, secondFetch)}");

var (pageItems, total) = await repository.GetPagedAsync(pageNumber: 1, pageSize: 2, sortBy: "name");
Console.WriteLine($"Paged fetch (size 2) returned {pageItems.Count()} items out of {total} total");
Console.WriteLine("Page details:");
foreach (var customer in pageItems)
{
    Console.WriteLine($"  {customer.AccountNumber} | {customer.DisplayName} | Balance {customer.CurrentBalance:C}");
}

var searchResults = await repository.SearchAsync("Acme");
Console.WriteLine($"Search for 'Acme' matched {searchResults.Count()} customers");

var balanceCustomers = await repository.GetCustomersWithBalanceAsync();
Console.WriteLine($"Customers with outstanding balance: {balanceCustomers.Count()}");

var newCustomer = new UtilityCustomer
{
    AccountNumber = "0004",
    FirstName = "Jamie",
    LastName = "Clark",
    CustomerType = CustomerType.Residential,
    ServiceAddress = "44 Elm St",
    ServiceCity = "Greenfield",
    ServiceState = "IL",
    ServiceZipCode = "60004",
    Status = CustomerStatus.Active,
    CurrentBalance = 45m,
    ServiceLocation = ServiceLocation.InsideCityLimits
};
await repository.AddAsync(newCustomer);

var existenceCheck = await repository.ExistsByAccountNumberAsync("0004");
Console.WriteLine($"New account persisted: {existenceCheck}");

var activeCustomers = await repository.GetActiveCustomersAsync();
Console.WriteLine($"Active customers: {activeCustomers.Count()}");

if (provider is IDisposable disposable)
{
    disposable.Dispose();
}

static async Task SeedCustomersAsync(IDbContextFactory<AppDbContext> factory)
{
    await using var context = await factory.CreateDbContextAsync();
    await context.Database.EnsureDeletedAsync();
    await context.Database.EnsureCreatedAsync();

    var customers = new List<UtilityCustomer>
    {
        new()
        {
            AccountNumber = "0001",
            FirstName = "Alex",
            LastName = "Rivera",
            CompanyName = "Rivera Builders",
            CustomerType = CustomerType.Commercial,
            ServiceAddress = "10 Main St",
            ServiceCity = "Greenfield",
            ServiceState = "IL",
            ServiceZipCode = "60001",
            Status = CustomerStatus.Active,
            CurrentBalance = 120.55m,
            ServiceLocation = ServiceLocation.InsideCityLimits
        },
        new()
        {
            AccountNumber = "0002",
            FirstName = "Taylor",
            LastName = "Nguyen",
            CustomerType = CustomerType.Residential,
            ServiceAddress = "22 Oak Ave",
            ServiceCity = "Greenfield",
            ServiceState = "IL",
            ServiceZipCode = "60002",
            Status = CustomerStatus.Active,
            CurrentBalance = 0m,
            ServiceLocation = ServiceLocation.OutsideCityLimits
        },
        new()
        {
            AccountNumber = "0003",
            FirstName = "Jordan",
            LastName = "Shaw",
            CompanyName = "Acme Industrial",
            CustomerType = CustomerType.Industrial,
            ServiceAddress = "98 Commerce Rd",
            ServiceCity = "Greenfield",
            ServiceState = "IL",
            ServiceZipCode = "60003",
            Status = CustomerStatus.Suspended,
            CurrentBalance = 540.10m,
            ServiceLocation = ServiceLocation.InsideCityLimits
        }
    };

    context.UtilityCustomers.AddRange(customers);
    await context.SaveChangesAsync();
}
