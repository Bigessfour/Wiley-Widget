# CLR Integration Tests

This directory contains Python integration tests that use pythonnet to test .NET assemblies from the Wiley Widget application.

## Overview

These tests validate the integration between Python test code and .NET CLR assemblies, including:
- Entity Framework Core database operations
- WPF ViewModels and UI components
- Enterprise repository patterns
- XAML analysis utilities
- AI service integrations

## Prerequisites

### System Requirements

- **Python**: 3.11+
- **.NET Runtime**: .NET 8.0 or 9.0 (Desktop or Core)
- **Operating System**: Windows (required for WPF/desktop assemblies)

### Python Dependencies

```bash
pip install pythonnet==3.0.5 pytest pytest-cov pytest-xdist
```

### .NET Runtime Configuration

The tests require a compatible .NET runtime. pythonnet supports:

- **.NET Framework 4.8+** (legacy)
- **.NET Core/.NET 5+** (modern)

#### Environment Variables

Set these environment variables to configure pythonnet:

```bash
# For .NET Core/.NET 5+ (recommended)
export PYTHONNET_RUNTIME=coreclr

# Optional: Specify runtime config file
export PYTHONNET_CORECLR_RUNTIME_CONFIG=path/to/runtimeconfig.json
```

#### Runtime Config File

Create a `runtimeconfig.json` file for your .NET version:

**For .NET 8.0:**
```json
{
  "runtimeOptions": {
    "tfm": "net8.0",
    "framework": {
      "name": "Microsoft.NETCore.App",
      "version": "8.0.0"
    }
  }
}
```

**For .NET 9.0:**
```json
{
  "runtimeOptions": {
    "tfm": "net9.0",
    "frameworks": [
      {
        "name": "Microsoft.NETCore.App",
        "version": "9.0.0"
      },
      {
        "name": "Microsoft.WindowsDesktop.App",
        "version": "9.0.0"
      }
    ]
  }
}
```

## Test Assembly Dependencies

The tests require compiled .NET assemblies to be present in the `assemblies/` directory:

### Required Assemblies

- `WileyWidget.Data.dll` - Data layer with EF Core
- `WileyWidget.Models.dll` - Domain models
- `WileyWidget.Business.dll` - Business logic
- `Microsoft.EntityFrameworkCore.dll` - EF Core runtime
- `Microsoft.EntityFrameworkCore.InMemory.dll` - In-memory database provider
- `Microsoft.Extensions.*.dll` - .NET dependency injection
- `Prism*.dll` - WPF framework (for Prism tests)

### Building Assemblies

Assemblies are built as part of the main project build:

```bash
# From project root
dotnet build WileyWidget.csproj
```

Built assemblies are copied to `tools/python/clr_tests/assemblies/` during the build process.

## Running Tests

### Basic Execution

```bash
# Run all tests
python -m pytest

# Run with verbose output
python -m pytest -v

# Run specific test file
python -m pytest test_db_context.py

# Run specific test
python -m pytest test_db_context.py::test_smoke_creation
```

### Test Categories

Tests are marked with pytest markers:

```bash
# Run only CLR tests
python -m pytest -m clr

# Run only integration tests
python -m pytest -m integration

# Run only Prism-related tests
python -m pytest -m prism

# Skip slow tests
python -m pytest -m "not slow"
```

### Coverage

```bash
# Run with coverage
python -m pytest --cov=. --cov-report=html

# View coverage report
python -m http.server 8000 --directory htmlcov
```

## Test Architecture

### Conditional Execution

Tests automatically skip when dependencies are unavailable:

- **pythonnet not installed**: All CLR tests skip
- **.NET runtime unavailable**: CLR tests skip
- **Required assemblies missing**: Individual tests skip
- **Prism not available**: Prism tests skip

### Fixtures

- `clr_loader`: Loads .NET assemblies on demand
- `ensure_assemblies_present`: Validates assembly availability
- `app_db_context`: Creates EF Core context with in-memory database

### Helpers

- `dotnet_utils.py`: Utilities for .NET type loading and instantiation
- `conftest.py`: Shared pytest configuration and fixtures

## Troubleshooting

### Common Issues

#### 1. "Failed to create a .NET runtime"

**Cause**: pythonnet cannot initialize the .NET runtime.

**Solutions**:
- Install .NET 8.0 or 9.0 runtime
- Set `PYTHONNET_RUNTIME=coreclr` environment variable
- Ensure runtime config file points to correct .NET version

#### 2. "Could not load file or assembly"

**Cause**: Required .NET assemblies are missing.

**Solutions**:
- Build the main project: `dotnet build`
- Check `assemblies/` directory contains required DLLs
- Ensure assembly versions match runtime

#### 3. "AttributeError: partially initialized module 'clr'"

**Cause**: Circular import or runtime initialization failure.

**Solutions**:
- Clear Python cache: `rm -rf __pycache__`
- Restart Python interpreter
- Check .NET runtime compatibility

#### 4. Tests skip unexpectedly

**Cause**: Dependencies not detected properly.

**Check**:
- pythonnet installed: `python -c "import clr; print('OK')"`
- Assemblies present: `ls assemblies/`
- Environment variables set correctly

### Debug Mode

Enable debug logging:

```bash
export PYTHONNET_DEBUG=1
python -m pytest -v -s
```

### Environment Validation

Run the environment check:

```python
from conftest import _has_pythonnet, _has_prism
print(f"pythonnet: {_has_pythonnet()}")
print(f"Prism: {_has_prism()}")
```

## Development

### Adding New Tests

1. Use conditional imports for CLR types
2. Add appropriate pytest markers
3. Include skipif decorators for missing dependencies
4. Follow existing fixture patterns

### Test File Structure

```python
# Check dependencies
try:
    import clr
    HAS_PYTHONNET = True
except (ImportError, RuntimeError, AttributeError):
    HAS_PYTHONNET = False

pytestmark = [
    pytest.mark.clr,
    pytest.mark.skipif(not HAS_PYTHONNET, reason="pythonnet required"),
]

# Conditional imports
if HAS_PYTHONNET:
    from System import String  # type: ignore[attr-defined]
```

## CI/CD Integration

These tests are designed to run in CI environments:

- Tests skip gracefully when dependencies unavailable
- No external services required
- Self-contained with in-memory databases
- Fast execution (< 30 seconds for full suite)

## Related Documentation

- [pythonnet Documentation](https://pythonnet.github.io/)
- [pytest Documentation](https://docs.pytest.org/)
- [.NET Runtime Configuration](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-runtimeconfig-json)
- [Entity Framework Core](https://docs.microsoft.com/en-us/ef/core/)