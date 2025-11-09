// Exploratory harness for UtilityBillRepository
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
    options.UseInMemoryDatabase("UtilityBillRepoTests");
});

var provider = services.BuildServiceProvider();
var factory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
var cache = provider.GetRequiredService<IMemoryCache>();
var logger = provider.GetRequiredService<ILogger<UtilityBillRepository>>();

await SeedUtilityDataAsync(factory);

var repository = new UtilityBillRepository(factory, logger, cache);

var customerBills = await repository.GetByCustomerIdAsync(1);
Console.WriteLine($"Customer 1 bills: {customerBills.Count()}");

var overdueBills = await repository.GetOverdueBillsAsync();
Console.WriteLine($"Overdue bills: {overdueBills.Count()}");

var balance = await repository.GetCustomerBalanceAsync(1);
Console.WriteLine($"Outstanding balance for customer 1: {balance:C}");

var paged = await repository.GetAllAsync();
Console.WriteLine($"Total bills cached fetch: {paged.Count()}");

var firstBillId = customerBills.First().Id;
var charges = await repository.GetChargesByBillIdAsync(firstBillId);
Console.WriteLine($"Bill {firstBillId} charge count: {charges.Count()}");

await repository.RecordPaymentAsync(firstBillId, 120m, DateTime.Today);
var updatedBill = await repository.GetByIdAsync(firstBillId);
Console.WriteLine($"Bill {updatedBill?.BillNumber} status: {updatedBill?.Status}, amount paid: {updatedBill?.AmountPaid:C}");

await repository.AddChargeAsync(new Charge
{
    BillId = firstBillId,
    ChargeType = "Stormwater",
    Description = "Stormwater remediation",
    Amount = 15m,
    Quantity = 1,
    Rate = 15m
});

var updatedCharges = await repository.GetChargesByBillIdAsync(firstBillId);
Console.WriteLine($"Bill {firstBillId} charge count after add: {updatedCharges.Count()}");

try
{
    await repository.AddAsync(new UtilityBill
    {
        CustomerId = 999,
        BillNumber = "BILL-ERROR",
        BillDate = DateTime.Today,
        DueDate = DateTime.Today.AddDays(15),
        PeriodStartDate = DateTime.Today.AddMonths(-1),
        PeriodEndDate = DateTime.Today,
        WaterCharges = 10m,
        SewerCharges = 5m,
        GarbageCharges = 5m,
        Status = BillStatus.Pending
    });
}
catch (Exception ex)
{
    Console.WriteLine($"Expected add failure: {ex.Message}");
}

try
{
    await repository.AddChargeAsync(new Charge
    {
        BillId = 99999,
        ChargeType = "Invalid",
        Description = "Invalid bill",
        Amount = 1m,
        Quantity = 1,
        Rate = 1m
    });
}
catch (Exception ex)
{
    Console.WriteLine($"Expected charge failure: {ex.Message}");
}

if (provider is IDisposable disposable)
{
    disposable.Dispose();
}

static async Task SeedUtilityDataAsync(IDbContextFactory<AppDbContext> factory)
{
    await using var context = await factory.CreateDbContextAsync();
    await context.Database.EnsureDeletedAsync();
    await context.Database.EnsureCreatedAsync();

    var customer = new UtilityCustomer
    {
        Id = 1,
        AccountNumber = "0001",
        FirstName = "Alex",
        LastName = "Rivera",
        CustomerType = CustomerType.Residential,
        ServiceAddress = "10 Main St",
        ServiceCity = "Greenfield",
        ServiceState = "IL",
        ServiceZipCode = "60001",
        Status = CustomerStatus.Active,
        CurrentBalance = 250m,
        ServiceLocation = ServiceLocation.InsideCityLimits
    };

    context.UtilityCustomers.Add(customer);

    var bills = new List<UtilityBill>
    {
        new()
        {
            CustomerId = 1,
            BillNumber = "BILL-001",
            BillDate = DateTime.Today.AddDays(-45),
            DueDate = DateTime.Today.AddDays(-15),
            PeriodStartDate = DateTime.Today.AddMonths(-2),
            PeriodEndDate = DateTime.Today.AddMonths(-1),
            WaterCharges = 80m,
            SewerCharges = 40m,
            GarbageCharges = 20m,
            StormwaterCharges = 5m,
            AmountPaid = 0m,
            Status = BillStatus.Overdue,
            Charges = new List<Charge>
            {
                new()
                {
                    ChargeType = "Water",
                    Description = "Usage",
                    Amount = 80m,
                    Quantity = 1,
                    Rate = 80m
                },
                new()
                {
                    ChargeType = "Sewer",
                    Description = "Usage",
                    Amount = 40m,
                    Quantity = 1,
                    Rate = 40m
                }
            }
        },
        new()
        {
            CustomerId = 1,
            BillNumber = "BILL-002",
            BillDate = DateTime.Today.AddDays(-20),
            DueDate = DateTime.Today.AddDays(10),
            PeriodStartDate = DateTime.Today.AddMonths(-1),
            PeriodEndDate = DateTime.Today,
            WaterCharges = 60m,
            SewerCharges = 30m,
            GarbageCharges = 15m,
            AmountPaid = 20m,
            Status = BillStatus.Pending,
            Charges = new List<Charge>
            {
                new()
                {
                    ChargeType = "Water",
                    Description = "Usage",
                    Amount = 60m,
                    Quantity = 1,
                    Rate = 60m
                }
            }
        }
    };

    context.UtilityBills.AddRange(bills);
    await context.SaveChangesAsync();
}
