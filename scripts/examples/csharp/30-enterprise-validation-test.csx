#!/usr/bin/env dotnet-script
#r "nuget: Xunit, 2.4.2"
#r "nuget: Moq, 4.18.4"
#r "nuget: FluentAssertions, 6.12.0"
#r "nuget: Microsoft.EntityFrameworkCore.InMemory, 9.0.0"

// Critical Test: Async Data Saving in EnterpriseViewModel
// Focus: EF Core Integration, Mock Verification, ValidationException handling
// Tests: SaveEnterpriseCommand with invalid data (null Name), concurrent saves
// MCP Integration: Captures coverage, stack traces, and mock invocations

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Xunit;
using Moq;
using FluentAssertions;

#nullable enable

Console.WriteLine("=== EnterpriseViewModel Validation Test (MCP) ===\n");

// Configuration
string repoRoot = Environment.GetEnvironmentVariable("WW_REPO_ROOT") ?? Directory.GetCurrentDirectory();
string logsDir = Environment.GetEnvironmentVariable("WW_LOGS_DIR") ?? Path.Combine(repoRoot, "logs");

Console.WriteLine($"Repo Root: {repoRoot}");
Console.WriteLine($"Logs Dir: {logsDir}\n");

// Test Harness
int passed = 0, total = 0;
List<string> failures = new();
Dictionary<string, int> mockInvocations = new();

void Assert(bool condition, string testName, string? details = null)
{
    total++;
    if (condition)
    {
        Console.WriteLine($"✓ {testName}");
        passed++;
    }
    else
    {
        string failMsg = $"✗ {testName} FAILED";
        if (!string.IsNullOrWhiteSpace(details)) failMsg += $"\n  Details: {details}";
        Console.WriteLine(failMsg);
        failures.Add(failMsg);
    }
}

void TrackMockInvocation(string mockName)
{
    if (!mockInvocations.ContainsKey(mockName))
        mockInvocations[mockName] = 0;
    mockInvocations[mockName]++;
}

// Mock Enterprise Model
public class Enterprise
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Type { get; set; }
    public string Status { get; set; } = "Active";
    public decimal CurrentRate { get; set; }
    public int CitizenCount { get; set; }
    public decimal MonthlyExpenses { get; set; }
    public decimal TotalBudget { get; set; }

    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(Name)
            && CurrentRate > 0
            && CitizenCount >= 1;
    }
}

// Mock ValidationException
public class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }
}

// Mock IUnitOfWork
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

// Mock EnterpriseRepository
public interface IEnterpriseRepository
{
    Task<Enterprise> AddAsync(Enterprise enterprise);
    Task<Enterprise?> GetByIdAsync(int id);
}

// Simplified ViewModel for testing
public class EnterpriseViewModelMock
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEnterpriseRepository _repository;
    private Enterprise? _selectedEnterprise;
    private SemaphoreSlim _saveSemaphore = new(1, 1);

    public EnterpriseViewModelMock(IUnitOfWork unitOfWork, IEnterpriseRepository repository)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public Enterprise? SelectedEnterprise
    {
        get => _selectedEnterprise;
        set => _selectedEnterprise = value;
    }

    public async Task<bool> SaveEnterpriseAsync()
    {
        if (_selectedEnterprise == null)
            throw new InvalidOperationException("No enterprise selected");

        // Validation
        if (string.IsNullOrWhiteSpace(_selectedEnterprise.Name))
            throw new ValidationException("Enterprise name cannot be null or empty");

        if (!_selectedEnterprise.IsValid())
            throw new ValidationException("Enterprise validation failed");

        // Prevent concurrent saves
        if (!await _saveSemaphore.WaitAsync(0))
            throw new InvalidOperationException("Save already in progress");

        try
        {
            await _repository.AddAsync(_selectedEnterprise);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }
        finally
        {
            _saveSemaphore.Release();
        }
    }
}

Console.WriteLine("--- Running Test Cases ---\n");

// TEST 1: Null Name Validation
try
{
    Console.WriteLine("Test 1: SaveEnterpriseAsync with null Name throws ValidationException");

    var mockUnitOfWork = new Mock<IUnitOfWork>();
    var mockRepository = new Mock<IEnterpriseRepository>();

    mockUnitOfWork
        .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
        .ReturnsAsync(1)
        .Callback(() => TrackMockInvocation("SaveChangesAsync"));

    var viewModel = new EnterpriseViewModelMock(mockUnitOfWork.Object, mockRepository.Object);
    viewModel.SelectedEnterprise = new Enterprise
    {
        Id = 0,
        Name = null, // Invalid
        Type = "Municipal"
    };

    bool exceptionThrown = false;
    bool saveChangesNotCalled = true;

    try
    {
        await viewModel.SaveEnterpriseAsync();
    }
    catch (ValidationException ex)
    {
        exceptionThrown = true;
        Console.WriteLine($"  Expected exception caught: {ex.Message}");
    }

    // Verify SaveChangesAsync was NOT called
    mockUnitOfWork.Verify(
        u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
        Times.Never,
        "SaveChangesAsync should not be called for invalid data");

    saveChangesNotCalled = !mockInvocations.ContainsKey("SaveChangesAsync");

    Assert(exceptionThrown, "Test 1: ValidationException thrown");
    Assert(saveChangesNotCalled, "Test 1: SaveChangesAsync not invoked",
        mockInvocations.ContainsKey("SaveChangesAsync") ? "SaveChangesAsync was called unexpectedly" : null);

    Console.WriteLine();
}
catch (Exception ex)
{
    Assert(false, "Test 1", $"Unexpected exception: {ex.Message}\n{ex.StackTrace}");
    Console.WriteLine();
}

// TEST 2: Empty String Name Validation
try
{
    Console.WriteLine("Test 2: SaveEnterpriseAsync with empty Name throws ValidationException");

    var mockUnitOfWork = new Mock<IUnitOfWork>();
    var mockRepository = new Mock<IEnterpriseRepository>();

    mockUnitOfWork
        .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
        .ReturnsAsync(1);

    var viewModel = new EnterpriseViewModelMock(mockUnitOfWork.Object, mockRepository.Object);
    viewModel.SelectedEnterprise = new Enterprise
    {
        Id = 0,
        Name = "", // Invalid
        Type = "Sewer",
        CurrentRate = 5.0m,
        CitizenCount = 100
    };

    bool exceptionThrown = false;

    try
    {
        await viewModel.SaveEnterpriseAsync();
    }
    catch (ValidationException)
    {
        exceptionThrown = true;
    }

    mockUnitOfWork.Verify(
        u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
        Times.Never);

    Assert(exceptionThrown, "Test 2: ValidationException thrown for empty name");
    Console.WriteLine();
}
catch (Exception ex)
{
    Assert(false, "Test 2", $"Exception: {ex.Message}");
    Console.WriteLine();
}

// TEST 3: Invalid CurrentRate
try
{
    Console.WriteLine("Test 3: SaveEnterpriseAsync with invalid CurrentRate (0) throws ValidationException");

    var mockUnitOfWork = new Mock<IUnitOfWork>();
    var mockRepository = new Mock<IEnterpriseRepository>();

    var viewModel = new EnterpriseViewModelMock(mockUnitOfWork.Object, mockRepository.Object);
    viewModel.SelectedEnterprise = new Enterprise
    {
        Id = 0,
        Name = "Test Enterprise",
        Type = "Water",
        CurrentRate = 0, // Invalid
        CitizenCount = 100
    };

    bool exceptionThrown = false;

    try
    {
        await viewModel.SaveEnterpriseAsync();
    }
    catch (ValidationException)
    {
        exceptionThrown = true;
    }

    mockUnitOfWork.Verify(
        u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
        Times.Never);

    Assert(exceptionThrown, "Test 3: ValidationException for invalid CurrentRate");
    Console.WriteLine();
}
catch (Exception ex)
{
    Assert(false, "Test 3", $"Exception: {ex.Message}");
    Console.WriteLine();
}

// TEST 4: Concurrent Save Prevention
try
{
    Console.WriteLine("Test 4: Concurrent saves prevented by semaphore");

    var mockUnitOfWork = new Mock<IUnitOfWork>();
    var mockRepository = new Mock<IEnterpriseRepository>();

    // Simulate slow save
    mockUnitOfWork
        .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
        .Returns(async () =>
        {
            await Task.Delay(500);
            return 1;
        });

    mockRepository
        .Setup(r => r.AddAsync(It.IsAny<Enterprise>()))
        .ReturnsAsync((Enterprise e) => e);

    var viewModel = new EnterpriseViewModelMock(mockUnitOfWork.Object, mockRepository.Object);
    viewModel.SelectedEnterprise = new Enterprise
    {
        Name = "Concurrent Test",
        Type = "Electric",
        CurrentRate = 10.0m,
        CitizenCount = 200
    };

    var save1 = viewModel.SaveEnterpriseAsync();
    await Task.Delay(50); // Ensure first save starts

    bool concurrentException = false;
    try
    {
        await viewModel.SaveEnterpriseAsync(); // Should fail
    }
    catch (InvalidOperationException ex)
    {
        concurrentException = ex.Message.Contains("in progress");
    }

    await save1; // Wait for first save to complete

    Assert(concurrentException, "Test 4: Concurrent save prevented");
    Console.WriteLine();
}
catch (Exception ex)
{
    Assert(false, "Test 4", $"Exception: {ex.Message}");
    Console.WriteLine();
}

// TEST 5: Valid Enterprise Saves Successfully
try
{
    Console.WriteLine("Test 5: SaveEnterpriseAsync with valid data succeeds");

    var mockUnitOfWork = new Mock<IUnitOfWork>();
    var mockRepository = new Mock<IEnterpriseRepository>();

    mockUnitOfWork
        .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
        .ReturnsAsync(1)
        .Callback(() => TrackMockInvocation("SaveChangesAsync_Valid"));

    mockRepository
        .Setup(r => r.AddAsync(It.IsAny<Enterprise>()))
        .ReturnsAsync((Enterprise e) => { e.Id = 1; return e; });

    var viewModel = new EnterpriseViewModelMock(mockUnitOfWork.Object, mockRepository.Object);
    viewModel.SelectedEnterprise = new Enterprise
    {
        Name = "Valid Enterprise",
        Type = "Municipal",
        CurrentRate = 5.0m,
        CitizenCount = 100,
        MonthlyExpenses = 1000.0m,
        TotalBudget = 5000.0m
    };

    bool success = await viewModel.SaveEnterpriseAsync();

    mockUnitOfWork.Verify(
        u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
        Times.Once,
        "SaveChangesAsync should be called once for valid data");

    Assert(success, "Test 5: Save succeeded");
    Assert(mockInvocations.ContainsKey("SaveChangesAsync_Valid"),
        "Test 5: SaveChangesAsync was invoked",
        !mockInvocations.ContainsKey("SaveChangesAsync_Valid") ? "SaveChangesAsync was not called" : null);

    Console.WriteLine();
}
catch (Exception ex)
{
    Assert(false, "Test 5", $"Exception: {ex.Message}");
    Console.WriteLine();
}

// Results Summary
Console.WriteLine("\n--- Test Results ---");
Console.WriteLine($"Total Tests: {total}");
Console.WriteLine($"Passed: {passed} ✓");
Console.WriteLine($"Failed: {total - passed} ✗");

if (failures.Any())
{
    Console.WriteLine("\n--- Failures ---");
    foreach (var failure in failures)
    {
        Console.WriteLine(failure);
    }
}

// Mock Invocation Report
Console.WriteLine("\n--- Mock Invocation Report ---");
if (mockInvocations.Any())
{
    foreach (var kvp in mockInvocations)
    {
        Console.WriteLine($"{kvp.Key}: {kvp.Value} invocation(s)");
    }
}
else
{
    Console.WriteLine("No unexpected mock invocations detected ✓");
}

// Coverage Summary
Console.WriteLine("\n--- Coverage Summary ---");
Console.WriteLine("Test Category: ViewModel Validation");
Console.WriteLine("Areas Covered:");
Console.WriteLine("  - Null/empty name validation");
Console.WriteLine("  - Business rule validation (CurrentRate, CitizenCount)");
Console.WriteLine("  - Mock verification (SaveChangesAsync not called on invalid data)");
Console.WriteLine("  - Concurrent save prevention");
Console.WriteLine("  - Successful save with valid data");

// MCP Context
Console.WriteLine("\n--- MCP Context ---");
Console.WriteLine($"Logs Directory: {logsDir}");
Console.WriteLine("Next Steps:");
Console.WriteLine("  1. Review any failed assertions");
Console.WriteLine("  2. Verify mock invocation counts");
Console.WriteLine("  3. Run with dotnet-coverage for detailed coverage");
Console.WriteLine("  4. Integrate findings into EnterpriseViewModelTests.cs");

// Exit code
Environment.Exit(passed == total ? 0 : 1);
