// Exploratory harness for DataAnonymizerService
// Prerequisite: dotnet build -c Debug ensures binaries in ../src/**/bin/Debug
#r "nuget: Microsoft.Extensions.Logging.Console, 8.0.0"
#r "nuget: Microsoft.Extensions.DependencyInjection, 8.0.0"
#r "../../src/WileyWidget.Models/bin/Debug/net9.0-windows10.0.19041.0/win-x64/WileyWidget.Models.dll"
#r "../../src/WileyWidget.Services/bin/Debug/net9.0-windows10.0.19041.0/win-x64/WileyWidget.Services.dll"

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WileyWidget.Models;
using WileyWidget.Services;

var services = new ServiceCollection();
services.AddLogging(builder => builder.AddSimpleConsole(options => options.SingleLine = true).SetMinimumLevel(LogLevel.Debug));
var provider = services.BuildServiceProvider();

var logger = provider.GetRequiredService<ILogger<DataAnonymizerService>>();
var anonymizer = new DataAnonymizerService(logger);

var enterprise = new Enterprise
{
    Id = 42,
    Name = "Springfield Water Works",
    Description = "Contact john.doe@example.com or call (555) 123-4567.",
    CitizenCount = 1200,
    CurrentRate = 45.50m,
    MonthlyExpenses = 38000m,
    TotalBudget = 500000m,
    BudgetAmount = 420000m,
    Status = EnterpriseStatus.Active
};

var anonymizedEnterprise = anonymizer.AnonymizeEnterprise(enterprise);
Console.WriteLine($"Enterprise name masked: {anonymizedEnterprise?.Name}");
Console.WriteLine($"Enterprise description: {anonymizedEnterprise?.Description}");

var budget = new BudgetData
{
    EnterpriseId = 42,
    FiscalYear = 2025,
    TotalBudget = 1_200_000m,
    TotalExpenditures = 950_000m,
    RemainingBudget = 250_000m
};

var anonymizedBudget = anonymizer.AnonymizeBudgetData(budget);
Console.WriteLine($"Budget totals preserved: {anonymizedBudget.TotalBudget} remaining {anonymizedBudget.RemainingBudget}");

var enterpriseCollection = anonymizer.AnonymizeEnterprises(new[]
{
    enterprise,
    new Enterprise { Id = 43, Name = "Downtown Energy LLC", Description = "Billing lead jane@energy.example" }
});
Console.WriteLine($"Collection anonymized count: {enterpriseCollection.Count()}");

var rawText = "Mayor John Smith reachable at mayor@city.gov or (555) 987-6543. Account 123456789.";
var anonymizedText = anonymizer.Anonymize(rawText);
Console.WriteLine($"Raw text anonymized: {anonymizedText}");

var statsBeforeClear = anonymizer.GetCacheStatistics();
Console.WriteLine($"Cache entries before clear: {statsBeforeClear["TotalEntries"]}");

anonymizer.ClearCache();
var statsAfterClear = anonymizer.GetCacheStatistics();
Console.WriteLine($"Cache entries after clear: {statsAfterClear["TotalEntries"]}");

if (provider is IDisposable disposable)
{
    disposable.Dispose();
}
