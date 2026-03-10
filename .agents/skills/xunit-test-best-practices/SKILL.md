---
name: xunit-test-best-practices
description: Use when creating, refactoring, or reviewing xUnit tests in .NET (especially Syncfusion WinForms apps). Aligns with Microsoft best practices plus modern 2025-2026 patterns: FluentAssertions, async/await realism, one behavior per test, deterministic seams, public API focus, and Syncfusion event/control testing.
---

# xUnit Test Best Practices (2026 Edition)

## Purpose

Write clear, fast, isolated, deterministic, and maintainable xUnit tests that catch real regressions in Syncfusion WinForms applications.

Primary sources:

- https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices
- https://fluentassertions.com/

## Use This Skill When

- Writing new tests for ViewModels, services, panels, or Syncfusion event handlers
- Refactoring flaky or brittle test suites
- Reviewing PRs with test coverage gaps
- Testing async flows, SfDataGrid styling, docking changes, or theme switches

## Core Rules (Updated 2026)

1. Naming: `MethodName_Scenario_ExpectedOutcome`

- Example: `LoadBudgetsAsync_NoData_ReturnsEmptyCollectionAndShowsOverlay`

2. Structure: strict Arrange -> Act -> Assert (AAA) in every test

- Use comments to separate sections

3. One behavior per test

- One Act, focused assertions
- Split unrelated concerns

4. Prefer FluentAssertions over classic `Assert`

- Better messages and collection support
- Example: `actual.Should().Be(expected)`

5. Async and CancellationToken realism

- All async methods -> `async Task` test
- Await every call
- Pass real `CancellationToken` when timeout matters
- Use `ConfigureAwait(false)` in production code, not in tests

6. No logic in tests

- No loops, ifs, or computed expectations in `[Fact]`
- Use `[Theory]` with `[InlineData]` or `[MemberData]`

7. Isolated and deterministic

- Mock all external dependencies (repos, config, clock, Syncfusion events)
- No real filesystem, database, network, or `DateTime.Now`

8. Mocking

- Prefer NSubstitute (clean syntax) or Moq
- Mock interfaces only

9. Test public behavior only

- Never test private methods directly

10. Syncfusion WinForms specifics

- Fake `QueryCellStyleEventArgs`, `CurrentCellActivatedEventArgs`, etc.
- Test DPI-aware sizing, theme changes, and docking events

11. Coverage is a signal

- Focus on behavior and regression value

## Recommended Templates (2026 style)

### A. Simple async `[Fact]` with FluentAssertions

```csharp
[Fact]
public async Task RefreshBudgetsAsync_NoData_ReturnsEmptyCollectionAndShowsOverlay()
{
    // Arrange
    var mockRepo = Substitute.For<IBudgetRepository>();
    mockRepo.GetBudgetsAsync(Arg.Any<CancellationToken>())
            .ReturnsAsync(Array.Empty<BudgetEntry>());

    var sut = new BudgetViewModel(mockRepo /* other mocks */);

    // Act
    await sut.RefreshBudgetsAsync();

    // Assert
    sut.FilteredBudgetEntries.Should().BeEmpty();
    sut.NoDataOverlay.Should().BeVisible();
    sut.StatusText.Should().Be("No budgets found");
}
```

### B. Data-driven `[Theory]` with `[MemberData]`

```csharp
public static TheoryData<decimal, bool> OverBudgetHighlightData() => new()
{
    { 120_000m, true },
    { 98_000m, false },
    { 100_000m, false }
};

[Theory]
[MemberData(nameof(OverBudgetHighlightData))]
public void ShouldHighlightRow_WhenVarianceExceedsThreshold(decimal actual, bool expectedHighlight)
{
    // Arrange
    var entry = new BudgetEntry { BudgetedAmount = 100_000m, ActualAmount = actual };

    // Act
    var shouldHighlight = entry.Variance >= 5_000m || entry.Variance <= -5_000m;

    // Assert
    shouldHighlight.Should().Be(expectedHighlight);
}
```

### C. Syncfusion event handler test

```csharp
[Fact]
public void QueryCellStyle_OverBudgetVarianceCell_AppliesRedBackground()
{
    // Arrange
    var grid = new SfDataGrid();
    var args = new QueryCellStyleEventArgs
    {
        Column = new GridTextColumn { MappingName = "Variance" },
        Style = new GridCellStyleInfo { CellValue = -15_000m }
    };

    // Act
    BudgetPanel.OnQueryCellStyle(grid, args);

    // Assert
    args.Style.BackColor.Should().Be(Color.FromArgb(255, 220, 220));
    args.Style.ForeColor.Should().Be(Color.Black);
}
```

### D. Exception test with FluentAssertions

```csharp
[Fact]
public async Task LoadBudgetsAsync_RepositoryThrows_ShowsErrorMessage()
{
    // Arrange
    var mockRepo = Substitute.For<IBudgetRepository>();
    mockRepo.GetBudgetsAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new DbUpdateException("connection timeout"));

    var sut = new BudgetViewModel(mockRepo /* other mocks */);

    // Act
    Func<Task> act = async () => await sut.RefreshBudgetsAsync();

    // Assert
    await act.Should().ThrowAsync<DbUpdateException>()
             .WithMessage("*timeout*");

    sut.ErrorMessage.Should().Contain("connection timeout");
    sut.LoadingOverlay.Should().BeVisible();
}
```

## Anti-Patterns and Fixed Examples

Bad (logic plus multiple concerns):

```csharp
[Fact]
public void BadTest()
{
    for (int i = 0; i < 10; i++)
    {
        var result = calc.Add(i.ToString());
        Assert.Equal(i, result);
    }

    Assert.True(true);
}
```

Good:

```csharp
[Theory]
[InlineData("0", 0)]
[InlineData("5", 5)]
[InlineData("10", 10)]
public void Add_SingleDigit_ReturnsCorrectSum(string input, int expected)
{
    // Arrange
    var calc = CreateCalculator();

    // Act
    var actual = calc.Add(input);

    // Assert
    actual.Should().Be(expected);
}
```

Bad (real dependency):

```csharp
[Fact]
public void BadClock()
{
    var price = calculator.GetDiscountedPrice(100);
    Assert.Equal(50, price);
}
```

Good:

```csharp
[Fact]
public void GetDiscountedPrice_Tuesday_ReturnsHalf()
{
    // Arrange
    var clock = Substitute.For<IClock>();
    clock.DayOfWeek().Returns(DayOfWeek.Tuesday);
    var sut = new PriceCalculator(clock);

    // Act
    var actual = sut.GetDiscountedPrice(100);

    // Assert
    actual.Should().Be(50);
}
```

## Review Checklist (2026)

- Name follows `Method_Scenario_ExpectedOutcome`
- Uses FluentAssertions
- Async methods are awaited with realistic `CancellationToken` use
- One Act and focused assertions
- Mocks use NSubstitute or Moq (interfaces only)
- No real I/O, clock, or environment dependencies
- Syncfusion events tested with fake args
- `[Theory]` plus `[MemberData]` for multi-case behavior
- No private method testing
- Clear failure messages via FluentAssertions

## Validation Commands

```powershell
# Run single test
dotnet test --filter "FullyQualifiedName~BudgetViewModelTests.RefreshBudgetsAsync_NoData_ReturnsEmptyCollectionAndShowsOverlay"

# Verbose output
dotnet test -v detailed
```

When applying this skill, report:

1. Tests added or updated
2. Rules followed
3. Validation commands used
4. Remaining gaps
