# Quick Start: Running Tests in Wiley Widget

## ✅ One-Command Test Execution

```bash
# From solution root
dotnet test
```

## 🎯 What Gets Tested

| Category       | Files                        | Purpose                             |
| -------------- | ---------------------------- | ----------------------------------- |
| **ViewModels** | `DashboardViewModelTests.cs` | MVVM property changes, data binding |
| **Services**   | `QuickBooksServiceTests.cs`  | OAuth2 flows, token management      |

**Total Tests**: ~15 meaningful tests (not aiming for 100% coverage)

## 📊 Expected Output

```
Test run for C:\...\WileyWidget.Services.Tests.dll (.NET 9.0)
Microsoft (R) Test Execution Command Line Tool Version 17.12.0

Starting test execution, please wait...
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:    15, Skipped:     0, Total:    15
```

## 🚀 Integration with Your Workflow

### Daily Development Loop

```bash
# 1. Morning health check
trunk check --monitor

# 2. Make code changes to ViewModels or Services

# 3. Run tests before commit
dotnet test

# 4. Pre-commit validation
trunk fmt --all
trunk check --fix
trunk check --ci

# 5. Commit & push
git add .
git commit -m "feat: added X feature"
git push
```

### CI/CD Integration

Tests run automatically in GitHub Actions via your existing `ci-optimized.yml`:

```yaml
- name: Build & Test Matrix
  run: |
    dotnet build --no-restore
    dotnet test --no-build --collect:"XPlat Code Coverage"
```

## 🔧 Visual Studio Integration

1. Open **Test Explorer**: `Test > Test Explorer`
2. Click **Run All** (or `Ctrl+R, A`)
3. View results and click failed tests to jump to code

## 📖 Test Philosophy

**20-30 Meaningful Tests > 100% Coverage**

We focus on:

- ✅ Critical business logic (QuickBooks OAuth)
- ✅ MVVM binding behavior (Syncfusion controls)
- ✅ Error handling (API failures, token expiry)
- ❌ Skip: Auto-generated code, trivial properties

## 📂 Test Structure

```
tests/WileyWidget.Services.Tests/
├── ViewModelTests/
│   └── DashboardViewModelTests.cs      # 6 tests
├── ServiceTests/
│   └── QuickBooksServiceTests.cs       # 9 tests
└── README.md                           # Full documentation
```

## 💡 Adding Your Own Tests

### Template: ViewModel Test

```csharp
[Fact]
public void MyProperty_SetValue_RaisesPropertyChanged()
{
    // Arrange
    var vm = new MyViewModel();
    var raised = false;
    vm.PropertyChanged += (s, e) => raised = true;

    // Act
    vm.MyProperty = "new value";

    // Assert
    raised.Should().BeTrue();
}
```

### Template: Service Test

```csharp
[Fact]
public async Task MyMethod_ValidInput_ReturnsSuccess()
{
    // Arrange
    var mockHttp = new Mock<HttpClient>();
    var service = new MyService(mockHttp.Object);

    // Act
    var result = await service.MyMethodAsync();

    // Assert
    result.Should().BeTrue();
}
```

## 🐛 Troubleshooting

### "Tests not found"

- Run: `dotnet restore`
- Rebuild: `dotnet build`

### "Mock not configured"

- Check `SetupDefaultSecretVault()` in test constructor
- Verify mock setup happens before test execution

### "Test fails locally but passes in CI"

- Check for hardcoded paths (use `Path.Combine`)
- Verify timezone-independent date handling

## 📚 Resources

- Full test documentation: `tests/WileyWidget.Services.Tests/README.md`
- xUnit: https://xunit.net/
- Moq: https://github.com/moq/moq4/wiki/Quickstart
- FluentAssertions: https://fluentassertions.com/

---

**Remember**: Tests should **save** you debugging time, not create more work. If a test is flaky or hard to maintain, delete it and write a simpler one.
