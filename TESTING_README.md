# Wiley Widget Testing Tools Guide

This guide covers the comprehensive testing toolchain available for the Wiley Widget project.

## ✅ Current Status

**All testing tools are fully integrated and operational:**
- ✅ 12/12 Entity Validation Tests Passing
- ✅ AutoFixture with Circular Reference Handling
- ✅ Coverlet Code Coverage Analysis
- ✅ FluentAssertions for Readable Assertions
- ✅ Bogus for Realistic Test Data
- ✅ Stryker.NET Mutation Testing Ready
- ✅ CustomAutoData Attribute for Advanced Scenarios

## 🛠️ Available Testing Tools

### Core Testing Framework
- **xUnit.net** - Primary test framework
- **Microsoft.NET.Test.Sdk** - Test execution and discovery
- **xunit.runner.visualstudio** - VS integration
- **xunit.runner.wpf** - WPF test support

### Advanced Testing Libraries
- **AutoFixture** - Automatic test data generation
- **AutoFixture.Xunit2** - xUnit integration for AutoFixture
- **FluentAssertions** - Readable assertion library
- **Bogus** - Realistic fake data generation
- **Moq** - Mocking framework
- **CustomAutoData** - Enhanced AutoFixture with circular reference handling

### Code Coverage & Quality
- **Coverlet** - Code coverage analysis
- **Stryker.NET** - Mutation testing
- **Microsoft.CodeAnalysis.NetAnalyzers** - Static code analysis

## 🚀 Running Tests

### Using the Enhanced Test Runner Script

```powershell
# Run all tests with advanced tools
.\scripts\run-tests.ps1 -TestType All -UseAdvancedTools

# Run only entity validation tests
.\scripts\run-tests.ps1 -TestType EntityValidation

# Run mutation testing
.\scripts\run-tests.ps1 -TestType Mutation

# Run coverage analysis with advanced reporting
.\scripts\run-tests.ps1 -TestType Coverage -UseAdvancedTools

# Run specific test category
.\scripts\run-tests.ps1 -TestType Unit -Filter "MainViewModelTests"
```

### Manual Test Execution

```bash
# Basic test run
dotnet test WileyWidget.Tests\WileyWidget.Tests.csproj

# With coverage
dotnet test WileyWidget.Tests\WileyWidget.Tests.csproj --collect:"XPlat Code Coverage"

# With custom settings
dotnet test WileyWidget.Tests\WileyWidget.Tests.csproj --settings WileyWidget.Tests\.runsettings
```

### Advanced Coverage with Coverlet

```bash
# Generate detailed coverage report
coverlet WileyWidget.Tests\bin\Debug\net9.0-windows\WileyWidget.Tests.dll `
  --target "dotnet" `
  --targetargs "test WileyWidget.Tests\WileyWidget.Tests.csproj --no-build" `
  --format "lcov;html;opencover" `
  --output "TestResults/coverage" `
  --include "[WileyWidget*]*" `
  --exclude "[xunit.*]*,[Moq]*,[FluentAssertions]*,[AutoFixture]*,[Bogus]*"
```

### Mutation Testing with Stryker

```bash
# Run mutation tests
dotnet-stryker

# Stryker will create mutants of your code and verify tests catch them
```

## 📊 Test Categories

### Entity Validation Tests ✅
**Status: 12/12 Tests Passing**
Located in `EntityValidationTests.cs` - comprehensive validation of all entities using:
- CustomAutoData for automatic test data generation with circular reference handling
- FluentAssertions for readable assertions
- Property-based testing patterns
- RangeAttribute overflow protection
- Realistic data constraints

### Main ViewModel Tests ✅
**Status: All Tests Passing**
Located in `ComprehensiveViewModelTests.cs` - tests for the main application logic with DI resolution

### Database Integration Tests
Located in `DatabaseIntegrationTests.cs` - tests for database operations

### UI Tests
Located in `WileyWidget.UiTests\` - WPF UI testing

## 🔧 Tool-Specific Usage

### AutoFixture Examples

```csharp
[Theory]
[AutoData]
public void Enterprise_WithValidData_ShouldPassValidation(Enterprise enterprise)
{
    // Arrange - AutoFixture generates valid data automatically

    // Act
    var validationResults = ValidateModel(enterprise);

    // Assert
    validationResults.Should().BeEmpty();
}
```

### CustomAutoData Attribute

For advanced scenarios with circular references and complex data generation:

```csharp
[Theory]
[CustomAutoData]
public void Enterprise_WithCircularReferences_ShouldHandleGracefully(Enterprise enterprise)
{
    // CustomAutoData handles circular references automatically
    // Uses OmitOnRecursionBehavior to prevent infinite loops

    // Arrange - data generated with proper constraints
    enterprise.Name.Should().NotBeNullOrEmpty();
    enterprise.Name.Length.Should().BeLessOrEqualTo(50);

    // Act & Assert
    var validationResults = ValidateModel(enterprise);
    validationResults.Should().BeEmpty();
}
```

**CustomAutoData Features:**
- ✅ Circular reference handling with `OmitOnRecursionBehavior`
- ✅ String length constraints to prevent validation errors
- ✅ Realistic data generation for decimal ranges
- ✅ Automatic handling of RangeAttribute overflow issues

### Recent Fixes & Improvements

**RangeAttribute Overflow Fix:**
```csharp
// Before: Could cause overflow with decimal properties
[Range(0, 79228162514264337593543950335)]

// After: Explicit type specification prevents overflow
[Range(typeof(decimal), "0", "79228162514264337593543950335")]
```

**Circular Reference Resolution:**
```csharp
// CustomAutoData automatically handles circular references
fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
    .ForEach(b => fixture.Behaviors.Remove(b));
fixture.Behaviors.Add(new OmitOnRecursionBehavior());
```

### FluentAssertions Examples

```csharp
// Traditional assertions
Assert.Equal(expected, actual);

// FluentAssertions
actual.Should().Be(expected);
enterprise.CurrentRate.Should().BeGreaterThan(0);
validationResults.Should().Contain(r => r.ErrorMessage.Contains("required"));
```

### Bogus for Realistic Data

```csharp
private readonly Faker<Enterprise> _enterpriseFaker = new Faker<Enterprise>()
    .RuleFor(e => e.Name, f => f.Company.CompanyName())
    .RuleFor(e => e.CurrentRate, f => f.Random.Decimal(10, 100))
    .RuleFor(e => e.CitizenCount, f => f.Random.Int(1, 10000));
```

## 📈 Coverage Goals

- **Target Coverage**: 80%+ line coverage
- **Critical Paths**: 90%+ coverage for business logic
- **Entity Validation**: 100% coverage for all validation rules

## 🔍 Quality Gates

### Code Coverage Requirements
- Minimum 75% overall coverage
- Entity classes: 90%+ coverage
- Business logic: 85%+ coverage
- UI logic: 70%+ coverage

### Mutation Testing
- Mutation score > 80%
- No critical surviving mutants in business logic

### Static Analysis
- Zero critical issues
- < 5 high-priority warnings

## 📋 Best Practices

### Test Organization
- Use descriptive test names: `MethodName_Condition_ExpectedResult`
- Group related tests in separate classes
- Use `[Trait]` attributes for categorization

### Entity Testing
- Test all validation rules
- Test boundary conditions
- Test serialization/deserialization
- Use realistic test data

### Mocking Strategy
- Mock external dependencies
- Use interfaces for testability
- Avoid mocking concrete classes when possible

## 🐛 Troubleshooting

### Common Issues

**AutoFixture Circular Reference Errors:**
```csharp
// Solution: Use CustomAutoData attribute
[CustomAutoData] // Handles circular references automatically
public void TestMethod(Enterprise enterprise) { ... }
```

**RangeAttribute Overflow with Decimals:**
```csharp
// Solution: Specify type explicitly
[Range(typeof(decimal), "0", "79228162514264337593543950335")]
```

**String Length Validation Errors:**
```csharp
// CustomAutoData generates strings of appropriate length
fixture.Customize<Enterprise>(composer => composer
    .With(e => e.Name, () => new string(Enumerable.Range(0, 50)
        .Select(_ => fixture.Create<char>()).ToArray())));
```

**Tests not discovered:**
```bash
# Clean and rebuild
dotnet clean
dotnet build
```

**Coverage not generating:**
```bash
# Ensure coverlet.collector is referenced
dotnet add package coverlet.collector
```

**Mutation testing slow:**
```bash
# Use targeted mutation testing
dotnet-stryker --test-project WileyWidget.Tests.csproj --test-case-filter "EntityValidationTests"
```

## 📚 Additional Resources

- [xUnit Documentation](https://xunit.net/)
- [AutoFixture Documentation](https://autofixture.github.io/)
- [FluentAssertions Documentation](https://fluentassertions.com/)
- [Bogus Documentation](https://github.com/bchavez/Bogus)
- [Coverlet Documentation](https://github.com/coverlet-coverage/coverlet)
- [Stryker.NET Documentation](https://stryker-mutator.io/)

## 🎯 Continuous Integration

The testing tools are configured for CI/CD pipelines:

```yaml
# Example GitHub Actions workflow
- name: Run Tests with Coverage
  run: .\scripts\run-tests.ps1 -TestType All -UseAdvancedTools

- name: Upload Coverage
  uses: codecov/codecov-action@v3
  with:
    file: ./TestResults/coverage/coverage.lcov
```

This comprehensive testing setup ensures high-quality, well-tested code with excellent maintainability and reliability.

## 🎉 Recent Achievements

- ✅ **Full Tool Integration**: All advanced testing tools successfully integrated
- ✅ **Entity Validation**: 12/12 tests passing with comprehensive coverage
- ✅ **Circular Reference Handling**: CustomAutoData attribute resolves complex scenarios
- ✅ **RangeAttribute Fixes**: Overflow issues resolved for decimal properties
- ✅ **Industry Best Practices**: Following professional testing standards
- ✅ **CI/CD Ready**: Tools configured for automated pipelines

The Wiley Widget project now has a robust, professional-grade testing environment that supports:
- Automatic test data generation
- Comprehensive entity validation
- Code coverage analysis
- Mutation testing capabilities
- Readable, maintainable test assertions
- Realistic fake data generation
