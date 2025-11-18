# WileyWidget Testing Guide (pytest standard)

We have standardized on `pytest` for all unit, integration, and UI automation testing, following the official [pytest documentation](https://docs.pytest.org/). .NET/xUnit projects were retired to reduce redundancy and simplify CI.

## What's in use

- Test runner: `pytest` (Python testing framework)
- Test discovery: `pytest` automatic discovery or `python -m pytest`
- Entry tasks: VS Code task "test-fast" (runs test suite)

## Quick start

```powershell
# Run all tests with pytest discovery (from project root)
python -m pytest tools/python -v

# Run with our consolidated pytest runner (enhanced output)
python tools/python/test_config.py --verbose

# Run a specific pytest module (example)
python -m pytest tools/python/tests/test_startup_validation.py

# Run with coverage using the consolidated runner
python tools/python/test_config.py --coverage
```

## Test Organization

### Pure Python Tests (`tools/python/tests/`)

- Unit tests for ViewModels and services
- Use pytest test functions and classes
- Mock-based testing with `pytest-mock` or `unittest.mock`

### CLR Integration Tests (`tools/python/clr_tests/`)

- pytest-based integration tests for .NET assemblies
- Tests EF Core, WPF ViewModels via pythonnet
- Requires pythonnet and built .NET assemblies

### Stress Tests (`tests/`)

- Memory leak detection, resource exhaustion simulation
- Threading stress tests with psutil monitoring

## Writing Tests

### Unit Test Example

```python
import pytest
from unittest.mock import Mock, patch
from wiley_widget.viewmodels.main_viewmodel import MainViewModel

class TestMainViewModel:
    """Test cases for MainViewModel."""

    def test_initialization(self):
        """Test that ViewModel initializes correctly."""
        viewmodel = MainViewModel()
        assert viewmodel.title == "Wiley Widget"
        assert not viewmodel.is_loading

    def test_load_data_success(self):
        """Test successful data loading."""
        # Arrange
        viewmodel = MainViewModel()
        mock_data = [{"id": 1, "name": "Test Item"}]

        # Act
        with patch('wiley_widget.services.data_service.DataService.get_data',
                  return_value=mock_data):
            result = viewmodel.load_data()

        # Assert
        assert result is True
        assert len(viewmodel.items) == 1
        assert viewmodel.items[0]["name"] == "Test Item"

    def test_load_data_failure(self):
        """Test data loading failure."""
        # Arrange
        viewmodel = MainViewModel()
        with patch('wiley_widget.services.data_service.DataService.get_data',
                  side_effect=Exception("Database error")):

            # Act & Assert
            with pytest.raises(Exception) as exc_info:
                viewmodel.load_data()

            assert "Database error" in str(exc_info.value)
```

### CLR Integration Test Example

```python
import pytest
from unittest.mock import Mock

try:
    import clr
    from System import String
    from wiley_widget.clr.models.widget import Widget
    CLR_AVAILABLE = True
except ImportError:
    CLR_AVAILABLE = False

@pytest.mark.skipif(not CLR_AVAILABLE, reason="pythonnet not available")
class TestWidgetModel:
    """Test cases for Widget CLR model."""

    def test_widget_creation(self):
        """Test Widget creation and property setting."""
        # Arrange
        widget = Widget()
        widget.Name = "Test Widget"
        widget.Description = "A test widget"

        # Act & Assert
        assert widget.Name == "Test Widget"
        assert widget.Description == "A test widget"

    def test_widget_name_validation_valid(self):
        """Test Widget name validation with valid name."""
        # Arrange
        widget = Widget()
        widget.Name = "Valid Name"

        # Act
        is_valid = widget.IsValidName()

        # Assert
        assert is_valid is True

    def test_widget_name_validation_empty(self):
        """Test Widget name validation with empty string."""
        # Arrange
        widget = Widget()
        widget.Name = ""

        # Act
        is_valid = widget.IsValidName()

        # Assert
        assert is_valid is False

    def test_widget_name_validation_none(self):
        """Test Widget name validation with None."""
        # Arrange
        widget = Widget()
        widget.Name = None

        # Act
        is_valid = widget.IsValidName()

        # Assert
        assert is_valid is False
```

## Test Fixtures and Setup

### pytest Fixtures

````python
import pytest
import tempfile
import os
import shutil

@pytest.fixture
def test_database():
    """Fixture for test database setup."""
    db = create_test_database()
    connection = db.connect()
    yield connection
    # Cleanup
    if connection:
        connection.close()
    if db:
        db.cleanup()

@pytest.fixture(scope="class")
def test_directory():
    """Class-scoped fixture for test directory."""
    test_dir = tempfile.mkdtemp()
    yield test_dir
    # Cleanup
    shutil.rmtree(test_dir)

class TestDatabaseOperations:

    def test_insert_record(self, test_database):
        """Test inserting a record."""
        record = {"name": "Test", "value": 42}
        result = test_database.insert(record)
        assert result.id is not None

    def test_query_records(self, test_database):
        """Test querying records."""
        records = test_database.query_all()
        assert isinstance(records, list)

class TestFileOperations:

    def test_write_file(self, test_directory):
        """Test writing to file."""
        test_file = os.path.join(test_directory, "test.txt")
        with open(test_file, 'w') as f:
            f.write("test content")

        assert os.path.exists(test_file)

    def test_read_file(self, test_directory):
        """Test reading from file."""
        test_file = os.path.join(test_directory, "test.txt")
        with open(test_file, 'w') as f:
            f.write("test content")

        with open(test_file, 'r') as f:
            content = f.read()

        assert content == "test content"

## Assertions

### Common Assertions

```python
class TestAssertions:

    def test_equality(self):
        """Test equality assertions."""
        assert 2 + 2 == 4
        assert 2 + 2 != 5

    def test_boolean(self):
        """Test boolean assertions."""
        assert True
        assert not False

    def test_none(self):
        """Test None assertions."""
        assert None is None
        assert "not none" is not None

    def test_instance(self):
        """Test instance type assertions."""
        assert isinstance("string", str)
        assert not isinstance(123, str)

    def test_membership(self):
        """Test membership assertions."""
        assert "a" in "banana"
        assert "x" not in "banana"

    def test_sequences(self):
        """Test sequence assertions."""
        assert [1, 2, 3] == [1, 2, 3]

    def test_exceptions(self):
        """Test exception assertions."""
        with pytest.raises(ValueError):
            raise ValueError("test error")

        with pytest.raises(ValueError, match="test"):
            raise ValueError("test error message")
````

## Mocking and Patching

### Using pytest-mock

```python
import pytest
from unittest.mock import Mock, patch, MagicMock

class TestWithMocking:

    def test_mock_method(self, mocker):
        """Test using Mock objects."""
        mock_service = mocker.Mock()
        mock_service.get_data.return_value = {"result": "mocked"}

        result = mock_service.get_data()
        assert result["result"] == "mocked"
        mock_service.get_data.assert_called_once()

    def test_patch_decorator(self, mocker):
        """Test using patch decorator."""
        with mocker.patch('module.function') as mock_func:
            mock_func.return_value = "patched"
            result = call_function()
            assert result == "patched"

    def test_patch_method(self, mocker):
        """Test using patch as method decorator."""
        mock_get = mocker.patch('requests.get')
        mock_get.return_value.status_code = 200
        mock_get.return_value.json.return_value = {"data": "test"}

        # Call function that uses requests.get
        result = fetch_data()
        assert result["data"] == "test"
```

## UI Automation

For Windows UI automation of the WPF app:

- **pytest** with **pywinauto** – simple Windows UI automation
- **pytest** with **WinAppDriver + Appium** – more structured, CI-friendly

Keep UI tests minimal and focused on critical user interactions.

## Best Practices

### Test Naming Conventions

- Use descriptive names that explain what the test verifies
- Follow the pattern: `test_method_name_condition_expected_result`
- Example: `test_calculate_total_valid_items_returns_correct_sum`

### Test Organization

- Group related tests in the same class
- Use pytest fixtures for common setup
- Keep test methods focused on a single behavior

### Defensive Imports

```python
try:
    import clr
    CLR_AVAILABLE = True
except ImportError:
    CLR_AVAILABLE = False

@pytest.mark.skipif(not CLR_AVAILABLE, reason="pythonnet required")
class TestClrIntegration:
    # CLR tests here
    pass
```

### Database Testing

- Use in-memory database for isolated testing
- Reset database state between tests
- Avoid dependencies on external databases

### Coverage Goals

- Target 70%+ code coverage
- Use `coverage.py` for reporting
- Exclude generated code and test files

## Custom Test Configuration

We provide `test_config.py` with enhanced pytest features:

- **Verbose output**: Enhanced output with progress indicators
- **Custom test suites**: Organized test loading with error handling
- **Coverage integration**: Built-in coverage.py support
- **Module filtering**: Run specific test modules
- **Fail-fast option**: Stop on first failure

```powershell
# Enhanced test runner
python test_config.py --verbose

# Coverage reporting
python test_config.py --coverage

# Run specific modules
python test_config.py --modules tools.python.tests.test_startup_validation
```

## Troubleshooting

### Common Issues

#### Tests Not Discovered

- Ensure test files follow `test_*.py` naming pattern
- Check that test functions start with `test_`
- Verify pytest is properly configured

#### CLR Tests Failing

- Ensure pythonnet is installed: `pip install pythonnet`
- Verify .NET assemblies are built and accessible
- Check CLR import paths

#### Coverage Not Working

- Install coverage: `pip install coverage`
- Use `coverage run -m pytest`
- Check coverage configuration

### Debug Tips

- Use `python -m pytest -v` for verbose output
- Add print statements or logging in tests
- Use `pytest --pdb` for interactive debugging

## Integration with CI/CD

```yaml
# Example GitHub Actions workflow
- name: Run Tests
  run: |
    python -m pytest -v
    coverage run -m pytest
    coverage report -m

- name: Upload Coverage
  uses: codecov/codecov-action@v3
  with:
    file: ./coverage.xml
```

## Maintenance

### Updating Packages

```powershell
# Update Python test packages (minimal dependencies)
python -m pip install --upgrade pytest coverage pytest-mock

# For CLR integration
python -m pip install --upgrade pythonnet

# For UI automation
python -m pip install --upgrade pywinauto
```

### Adding New Test Modules

1. Create test file following `test_*.py` naming
2. Use pytest test functions or classes
3. Include defensive imports for optional dependencies
4. Use pytest fixtures for setup/teardown
5. Update CI configuration

## Resources

- [pytest Documentation](https://docs.pytest.org/)
- [pytest-mock Documentation](https://pytest-mock.readthedocs.io/)
- [pythonnet Documentation](https://pythonnet.github.io/)
- [pywinauto Documentation](https://pywinauto.readthedocs.io/)
- [Coverage.py Documentation](https://coverage.readthedocs.io/)

---

**Last Updated:** October 2025
**Test Environment Status:** ✅ pytest framework configured
