# Wiley Widget Development Scripts (Python-First)

This directory contains **Python-based development scripts** that provide a complete, cross-platform development environment management system. The Python approach follows industry best practices for maintainability, testing, and cross-platform compatibility.

## 🐍 Python Script Ecosystem

### Core Development Workflow

The Python scripts handle the complete development lifecycle:

#### `dev-start.py` - Main Orchestrator

**Purpose**: Complete development environment startup and management

- **Process Cleanup**: Removes orphaned .NET processes
- **Conflict Detection**: Checks for running WileyWidget instances
- **Build Hygiene**: Cleans artifacts with `dotnet clean`
- **Performance Locking**: Applies Azure performance optimizations
- **Development Launch**: Starts `dotnet watch` with proper monitoring

#### `load-env.py` - Environment Management

**Purpose**: Secure environment variable loading and validation

- **.env Loading**: Loads configuration from encrypted .env file
- **Azure Validation**: Tests Key Vault and database connections
- **Status Reporting**: Shows current environment state
- **Security**: Masks sensitive data in logs

#### `cleanup-dotnet.py` - Process Management

**Purpose**: Safe cleanup of development processes

- **Orphan Detection**: Finds hanging .NET processes
- **Build Cleanup**: Removes temporary files
- **Interactive Mode**: Confirms before killing processes
- **Force Mode**: Automated cleanup for CI/CD

#### `azure-setup.py` - Azure Configuration

**Purpose**: Azure resource setup and connectivity

- **CLI Configuration**: Sets up Azure CLI with proper defaults
- **Subscription Management**: Configures target subscription
- **Connection Testing**: Validates Azure resource access
- **MCP Integration**: Sets up Model Context Protocol servers

#### `setup-python.py` - Python Environment

**Purpose**: Ensures proper Python environment for development

- **Dependency Check**: Validates required Python packages
- **Virtual Environment**: Sets up isolated development environment
- **Path Configuration**: Configures Python paths and executables

## 📊 Evaluation Against Best Practices

### ✅ Industry Standards Compliance

- **Cross-Platform**: Works on Windows, Linux, macOS
- **Maintainable**: Clean code with proper error handling
- **Testable**: Modular design supports unit testing
- **Documented**: Comprehensive inline documentation
- **Version Controlled**: Text-based, diff-friendly scripts

### ✅ Azure Documentation Alignment

- **Python SDK Usage**: Leverages Azure SDK for Python
- **Security Best Practices**: Environment variables, Key Vault integration
- **Resource Management**: Proper cleanup and monitoring
- **Performance Optimization**: Caching and connection pooling
- **Monitoring**: Structured logging and status reporting

### ✅ Migration Benefits

**Before**: Scattered PowerShell scripts with platform limitations
**After**: Unified Python ecosystem with enterprise-grade features

## 🧹 Legacy Script Cleanup

### Archived Scripts (in `deprecated-backup/`)

The following PowerShell scripts have been replaced by Python equivalents:

- `dev-start.ps1` → `dev-start.py`
- `load-env.ps1` → `load-env.py`
- `cleanup-dotnet.ps1` → `cleanup-dotnet.py`
- `setup-azure.ps1` → `azure-setup.py`
- `setup-license.ps1` → Integrated into application startup
- `test-database-connection.ps1` → Integrated into `load-env.py`

### Migration Notes

- All functionality preserved and enhanced
- Python versions include additional validation and error handling
- Cross-platform compatibility added
- Performance optimizations integrated
- Comprehensive logging and monitoring added

## 🧪 Testing Framework

The Python scripts are designed with **testability first** - all business logic is in importable functions that can be unit tested.

### Test Structure

```
tests/
├── test_cleanup_dotnet.py    # Tests for cleanup-dotnet.py
├── test_load_env.py         # Tests for load-env.py
└── test_dev_start.py        # Tests for dev-start.py
```

### Running Tests

```bash
# Install test dependencies
pip install -r requirements-test.txt

# Run all tests
pytest

# Run specific test file
pytest tests/test_cleanup_dotnet.py

# Run with coverage
pytest --cov=scripts --cov-report=html

# Run parallel tests
pytest -n auto
```

### Test Examples

```python
# Example test for get_dotnet_processes()
@patch('subprocess.run')
def test_get_dotnet_processes_success(self, mock_run):
    mock_result = MagicMock()
    mock_result.stdout = '"dotnet.exe","1234","Console","1","10,000 K"'
    mock_run.return_value = mock_result

    processes = get_dotnet_processes()
    assert len(processes) == 1
    assert processes[0] == ('dotnet.exe', '1234')
```

### Testing Benefits

- **Confidence**: Verify script behavior before deployment
- **Regression Prevention**: Catch breaking changes
- **Documentation**: Tests serve as usage examples
- **CI/CD Integration**: Automated testing in pipelines

## 📈 Improvements Over Legacy Approach

| Aspect           | Legacy (PowerShell) | Current (Python)       |
| ---------------- | ------------------- | ---------------------- |
| Platform Support | Windows-only        | Cross-platform         |
| Maintainability  | Limited             | High (rich ecosystem)  |
| Testing          | Difficult           | pytest framework       |
| Error Handling   | Basic               | Comprehensive          |
| Documentation    | Inline comments     | Structured docs        |
| Performance      | CLI-dependent       | Optimized with caching |
| Security         | Variable            | Enhanced validation    |
| Monitoring       | Basic output        | Structured logging     |

## 🔧 Development Guidelines

- **Python First**: All new scripts should be Python-based
- **Modular Design**: Separate concerns into focused scripts
- **Error Handling**: Use try/catch with proper exit codes
- **Logging**: Use structured logging for all operations
- **Testing**: Include unit tests for critical functions
- **Documentation**: Update this README for any new scripts

## 📚 Related Documentation

- [Azure Python SDK Documentation](https://learn.microsoft.com/en-us/azure/developer/python/)
- [Python Best Practices](https://python-guide.org/)
- [Azure CLI Documentation](https://learn.microsoft.com/en-us/cli/azure/)
- [Syncfusion Licensing](https://help.syncfusion.com/windowsforms/licensing/)

# Show current status

python scripts/load-env.py --status

# Test Azure connections

python scripts/load-env.py --test-connections

````

### `azure-setup.py`
**Azure configuration and testing**
- Loads Azure environment variables
- Tests Azure CLI authentication
- Tests Azure SQL connections

```bash
# Full Azure setup
python scripts/azure-setup.py

# Test connections only
python scripts/azure-setup.py --test-connection
````

## VS Code Integration

All scripts are integrated with VS Code tasks:

- **"dev-start"**: Main development startup
- **"cleanup-dotnet"**: Process cleanup
- **"load-env"**: Load environment variables
- **"azure-setup"**: Azure configuration
- **"azure-test-connection"**: Test Azure connections

## Migration from PowerShell

The Python scripts provide the same functionality as the previous PowerShell versions:

| PowerShell Script    | Python Equivalent   | Status      |
| -------------------- | ------------------- | ----------- |
| `dev-start.ps1`      | `dev-start.py`      | ✅ Complete |
| `cleanup-dotnet.ps1` | `cleanup-dotnet.py` | ✅ Complete |
| `load-env.ps1`       | `load-env.py`       | ✅ Complete |
| `azure-setup.ps1`    | `azure-setup.py`    | ✅ Complete |

## Requirements

- Python 3.8+
- .NET SDK
- Azure CLI (for Azure scripts)
- SQL Server tools (optional, for SQL testing)

## Benefits of Python Scripts

- **Cross-platform**: Works on Windows, macOS, Linux
- **Better error handling**: More robust exception handling
- **Easier maintenance**: Python is more widely known
- **Integration**: Better integration with other tools
- **Performance**: Generally faster execution

## Legacy PowerShell Scripts

The original PowerShell scripts are kept for reference but are no longer maintained. They may be removed in future updates.
