# WileyWidget Testing Guide

## Overview

This document provides comprehensive information about the xUnit testing environment configured for the WileyWidget project, including setup details, package dependencies, and execution instructions.

## Test Environment Architecture

### Project Structure

```text
WileyWidget.sln
├── WileyWidget.csproj (Main application)
├── WileyWidget.Tests\ (Unit tests)
│   └── WileyWidget.Tests.csproj
└── WileyWidget.UiTests\ (UI tests)
    └── WileyWidget.UiTests.csproj
```

### Test Types

- **Unit Tests** (`WileyWidget.Tests`): Traditional unit testing for business logic, data access, and services
- **UI Tests** (`WileyWidget.UiTests`): Automated UI testing using FlaUI framework for WPF application testing

## Core Dependencies

### xUnit Testing Framework

| Package                     | Version | Purpose                                                       |
| --------------------------- | ------- | ------------------------------------------------------------- |
| `xunit`                     | 2.9.2   | Core testing framework providing test discovery and execution |
| `xunit.runner.visualstudio` | 2.8.2   | Visual Studio Test Explorer integration                       |
| `Microsoft.NET.Test.Sdk`    | 17.14.1 | .NET test host and test adapters                              |

### Code Coverage

| Package              | Version | Purpose                                |
| -------------------- | ------- | -------------------------------------- |
| `coverlet.collector` | 6.0.4   | Code coverage collection and reporting |

### UI Testing (FlaUI)

| Package      | Version | Purpose                                                 |
| ------------ | ------- | ------------------------------------------------------- |
| `FlaUI.Core` | 5.0.0   | Core UI automation framework                            |
| `FlaUI.UIA3` | 5.0.0   | Windows UI Automation 3.0 provider for WPF applications |

### Database Testing

| Package                                  | Version | Purpose                                               |
| ---------------------------------------- | ------- | ----------------------------------------------------- |
| `Microsoft.EntityFrameworkCore.InMemory` | 9.0.8   | In-memory database provider for isolated unit testing |

## Project Configuration

### WileyWidget.Tests.csproj (Unit Tests)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <RuntimeIdentifier>win7-x64</RuntimeIdentifier>
    <LangVersion>latest</LangVersion>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>disable</Nullable>
    <IsPackable>false</IsPackable>
    <NoWarn>$(NoWarn);NU1605</NoWarn>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
    <GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="coverlet.collector" Version="6.0.4">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="9.0.8" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\WileyWidget.csproj" />
  </ItemGroup>
</Project>
```

### WileyWidget.UiTests.csproj (UI Tests)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <RuntimeIdentifier>win7-x64</RuntimeIdentifier>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>disable</Nullable>
    <ShadowCopy>true</ShadowCopy>
    <NoWarn>$(NoWarn);NU1605;MSB3026;MSB3027;MSB3021</NoWarn>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
    <GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\WileyWidget.csproj" />
    <PackageReference Include="FlaUI.Core" Version="5.0.0" />
    <PackageReference Include="FlaUI.UIA3" Version="5.0.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.CodeAnalysis.NetAnalyzers" Version="9.0.0" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

## SDK Configuration

### global.json

The project uses a specific .NET SDK version to ensure consistency across development environments:

```json
{
  "sdk": {
    "version": "8.0.413"
  }
}
```

## Configuration Details

### Target Framework

- **Framework**: .NET 8.0 Windows (`net8.0-windows`)
- **Purpose**: Ensures compatibility with WPF and Windows-specific features

### Runtime Identifier

- **RID**: `win7-x64`
- **Purpose**: Provides legacy Windows compatibility for test execution
- **Note**: Required for FlaUI UI testing on Windows platforms

### Key Settings

| Setting          | Value             | Purpose                                                      |
| ---------------- | ----------------- | ------------------------------------------------------------ |
| `ImplicitUsings` | `enable`          | Reduces boilerplate using statements                         |
| `Nullable`       | `disable`         | Follows project guidelines to avoid nullable reference types |
| `ShadowCopy`     | `true` (UI tests) | Prevents file locking during UI test execution               |
| `IsPackable`     | `false`           | Excludes test projects from NuGet packaging                  |

### Warning Suppressions

| Warning Code              | Reason                                                              |
| ------------------------- | ------------------------------------------------------------------- |
| `NU1605`                  | Suppresses package downgrade warnings from QuickBooks SDK conflicts |
| `MSB3026/MSB3027/MSB3021` | Suppresses file copy warnings for UI test artifacts                 |

## Running Tests

### Execute All Tests

```powershell
# Run all tests in the solution
dotnet test WileyWidget.sln
```

### Execute Specific Test Projects

```powershell
# Run only unit tests
dotnet test WileyWidget.Tests\WileyWidget.Tests.csproj

# Run only UI tests
dotnet test WileyWidget.UiTests\WileyWidget.UiTests.csproj
```

### Run Tests with Code Coverage

```powershell
# Generate code coverage report
dotnet test WileyWidget.sln --collect:"XPlat Code Coverage"
```

### Run Tests in Visual Studio

1. Open Test Explorer (View → Test Explorer)
2. Build the solution
3. Click "Run All Tests" or select specific tests
4. View results and coverage in Test Explorer

### Run Tests from Command Line with Filters

```powershell
# Run tests with specific traits
dotnet test --filter "Category=Unit"

# Run tests with specific names
dotnet test --filter "Name~WidgetTests"

# Run tests in a specific class
dotnet test --filter "ClassName=WileyWidget.Tests.WidgetTests"
```

## Test Organization

### Unit Test Structure

```
WileyWidget.Tests/
├── WidgetTests.cs          # Widget entity tests
├── DatabaseIntegrationTests.cs  # Database operations
├── SettingsServiceTests.cs     # Configuration services
├── MainWindowTests.cs         # Main window logic
└── WileyWidget.Tests.csproj
```

### UI Test Structure

```
WileyWidget.UiTests/
├── MainWindowUITests.cs     # Main window UI interactions
├── AboutWindowUITests.cs    # About dialog tests
└── WileyWidget.UiTests.csproj
```

## Writing Tests

### Unit Test Example

```csharp
using Xunit;
using WileyWidget.Models;

namespace WileyWidget.Tests
{
    public class WidgetTests
    {
        [Fact]
        public void Widget_Creation_SetsProperties()
        {
            // Arrange
            var widget = new Widget
            {
                Name = "Test Widget",
                Description = "A test widget"
            };

            // Act & Assert
            Assert.Equal("Test Widget", widget.Name);
            Assert.Equal("A test widget", widget.Description);
        }

        [Theory]
        [InlineData("Valid Name", true)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void Widget_Name_Validation(string name, bool expected)
        {
            // Arrange
            var widget = new Widget { Name = name };

            // Act
            var isValid = !string.IsNullOrEmpty(widget.Name);

            // Assert
            Assert.Equal(expected, isValid);
        }
    }
}
```

### UI Test Example (FlaUI)

```csharp
using Xunit;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace WileyWidget.UiTests
{
    public class MainWindowUITests : IDisposable
    {
        private readonly Application _app;
        private readonly Window _mainWindow;

        public MainWindowUITests()
        {
            // Start the application
            _app = Application.Launch("WileyWidget.exe");

            // Get the main window
            var automation = new UIA3Automation();
            _mainWindow = _app.GetMainWindow(automation);
        }

        [Fact]
        public void MainWindow_Loads_Correctly()
        {
            // Assert
            Assert.NotNull(_mainWindow);
            Assert.True(_mainWindow.IsAvailable);
            Assert.Contains("Wiley Widget", _mainWindow.Title);
        }

        [Fact]
        public void MainWindow_HasExpectedControls()
        {
            // Arrange
            var automation = new UIA3Automation();

            // Act
            var buttons = _mainWindow.FindAllDescendants(cf => cf.ByControlType(automation.ControlType.Button));
            var textBoxes = _mainWindow.FindAllDescendants(cf => cf.ByControlType(automation.ControlType.Edit));

            // Assert
            Assert.NotEmpty(buttons);
            Assert.NotEmpty(textBoxes);
        }

        public void Dispose()
        {
            _app?.Close();
            _app?.Dispose();
        }
    }
}
```

## Best Practices

### Test Naming Conventions

- Use descriptive names that explain what the test verifies
- Follow the pattern: `MethodName_Condition_ExpectedResult`
- Example: `CalculateTotal_ValidItems_ReturnsCorrectSum`

### Test Organization

- Group related tests in the same class
- Use `[Trait]` attributes for categorization
- Keep test methods focused on a single behavior

### Database Testing

- Use in-memory database for isolated testing
- Reset database state between tests
- Avoid dependencies on external databases

### UI Testing

- Use appropriate waits for UI elements to load
- Clean up application instances after tests
- Test both positive and negative scenarios
- Consider using Page Object pattern for complex UIs

## Troubleshooting

### Common Issues

#### Tests Not Discovered

- Ensure `xunit.runner.visualstudio` package is installed
- Check that test methods are public and have `[Fact]` or `[Theory]` attributes
- Verify target framework compatibility

#### FlaUI Tests Failing

- Ensure application can be launched from test directory
- Check that UI elements have proper automation IDs
- Verify Windows UI Automation is enabled

#### Code Coverage Not Working

- Install `coverlet.collector` package
- Use `--collect:"XPlat Code Coverage"` parameter
- Check that test assemblies are being instrumented

### Debug Tips

- Use `Debugger.Launch()` in test methods for debugging
- Add logging to understand test execution flow
- Use conditional breakpoints for specific test scenarios

## Integration with CI/CD

The test environment is designed to work seamlessly with CI/CD pipelines:

```yaml
# Example GitHub Actions workflow
- name: Run Tests
  run: dotnet test WileyWidget.sln --collect:"XPlat Code Coverage" --results-directory ./test-results

- name: Upload Coverage
  uses: codecov/codecov-action@v3
  with:
    file: ./test-results/*/coverage.cobertura.xml
```

## Maintenance

### Updating Packages

```powershell
# Update xUnit packages
dotnet add package xunit --version 2.9.2
dotnet add package xunit.runner.visualstudio --version 2.8.2

# Update FlaUI packages
dotnet add package FlaUI.Core --version 5.0.0
dotnet add package FlaUI.UIA3 --version 5.0.0
```

### Adding New Test Projects

1. Create new test project using template
2. Add necessary package references
3. Configure project settings (RID, warnings, etc.)
4. Add project reference to main application
5. Update solution file

## Resources

- [xUnit Documentation](https://xunit.net/)
- [FlaUI Documentation](https://github.com/FlaUI/FlaUI)
- [Microsoft Testing Documentation](https://docs.microsoft.com/en-us/dotnet/core/testing/)
- [Code Coverage with Coverlet](https://github.com/coverlet-coverage/coverlet)

---

**Last Updated:** August 28, 2025
**Test Environment Status:** ✅ Configured and Ready
