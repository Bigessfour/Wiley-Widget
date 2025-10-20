# WileyWidget Testing Guide (unittest standard)

We have standardized on Python's built-in `unittest` library for all unit, integration, and UI automation testing, following the official [unittest documentation](https://docs.python.org/3/library/unittest.html). .NET/xUnit projects were retired to reduce redundancy and simplify CI.

## What’s in use

- Test runner: `unittest` (Python standard library)
- Test discovery: `unittest.main()` or `python -m unittest discover`
- Entry tasks: VS Code task "test-fast" (runs unit test suite)

## Quick start

```powershell
# Run all tests with standard unittest discovery (from project root)
python -m unittest discover -s tools/python -p "test_*.py"

# Run with our consolidated unittest runner (enhanced output)
python tools/python/test_config.py --verbose

# Run a specific unittest module (example)
python -m unittest tools.python.unittests.test_startup_validation

# Run with coverage using the consolidated runner
python tools/python/test_config.py --coverage
```

## Test Organization

### Pure Python Tests (`tools/python/tests/`)
- Unit tests for ViewModels and services
- Use `unittest.TestCase` base class
- Mock-based testing with `unittest.mock`

### CLR Integration Tests (`tools/python/clr_tests/`)
- `unittest`-based integration tests for .NET assemblies
- Tests EF Core, WPF ViewModels via pythonnet
- Requires pythonnet and built .NET assemblies

### Stress Tests (`tests/`)
- Memory leak detection, resource exhaustion simulation
- Threading stress tests with psutil monitoring

## Writing Tests

### Unit Test Example

```python
import unittest
from unittest.mock import Mock, patch
from wiley_widget.viewmodels.main_viewmodel import MainViewModel

class TestMainViewModel(unittest.TestCase):
    """Test cases for MainViewModel."""

    def setUp(self):
        """Set up test fixtures before each test method."""
        self.viewmodel = MainViewModel()

    def tearDown(self):
        """Clean up test fixtures after each test method."""
        pass

    def test_initialization(self):
        """Test that ViewModel initializes correctly."""
        self.assertEqual(self.viewmodel.title, "Wiley Widget")
        self.assertFalse(self.viewmodel.is_loading)

    def test_load_data_success(self):
        """Test successful data loading."""
        # Arrange
        mock_data = [{"id": 1, "name": "Test Item"}]

        # Act
        with patch('wiley_widget.services.data_service.DataService.get_data',
                  return_value=mock_data):
            result = self.viewmodel.load_data()

        # Assert
        self.assertTrue(result)
        self.assertEqual(len(self.viewmodel.items), 1)
        self.assertEqual(self.viewmodel.items[0]["name"], "Test Item")

    def test_load_data_failure(self):
        """Test data loading failure."""
        # Arrange
        with patch('wiley_widget.services.data_service.DataService.get_data',
                  side_effect=Exception("Database error")):

            # Act & Assert
            with self.assertRaises(Exception) as context:
                self.viewmodel.load_data()

            self.assertIn("Database error", str(context.exception))
```

### CLR Integration Test Example

```python
import unittest
from unittest.mock import Mock

try:
    import clr
    from System import String
    from wiley_widget.clr.models.widget import Widget
    CLR_AVAILABLE = True
except ImportError:
    CLR_AVAILABLE = False

@unittest.skipUnless(CLR_AVAILABLE, "pythonnet not available")
class TestWidgetModel(unittest.TestCase):
    """Test cases for Widget CLR model."""

    def setUp(self):
        """Set up test fixtures."""
        self.widget = Widget()

    def test_widget_creation(self):
        """Test Widget creation and property setting."""
        # Arrange
        self.widget.Name = "Test Widget"
        self.widget.Description = "A test widget"

        # Act & Assert
        self.assertEqual(self.widget.Name, "Test Widget")
        self.assertEqual(self.widget.Description, "A test widget")

    def test_widget_name_validation_valid(self):
        """Test Widget name validation with valid name."""
        # Arrange
        self.widget.Name = "Valid Name"

        # Act
        is_valid = self.widget.IsValidName()

        # Assert
        self.assertTrue(is_valid)

    def test_widget_name_validation_empty(self):
        """Test Widget name validation with empty string."""
        # Arrange
        self.widget.Name = ""

        # Act
        is_valid = self.widget.IsValidName()

        # Assert
        self.assertFalse(is_valid)

    def test_widget_name_validation_none(self):
        """Test Widget name validation with None."""
        # Arrange
        self.widget.Name = None

        # Act
        is_valid = self.widget.IsValidName()

        # Assert
        self.assertFalse(is_valid)
```

## Test Fixtures and Setup

### setUp and tearDown Methods

```python
class TestDatabaseOperations(unittest.TestCase):

    def setUp(self):
        """Set up test database before each test."""
        self.db = create_test_database()
        self.connection = self.db.connect()

    def tearDown(self):
        """Clean up test database after each test."""
        if self.connection:
            self.connection.close()
        if self.db:
            self.db.cleanup()

    def test_insert_record(self):
        """Test inserting a record."""
        record = {"name": "Test", "value": 42}
        result = self.connection.insert(record)
        self.assertIsNotNone(result.id)

    def test_query_records(self):
        """Test querying records."""
        records = self.connection.query_all()
        self.assertIsInstance(records, list)
```

### Class-level setUp and tearDown

```python
class TestFileOperations(unittest.TestCase):

    @classmethod
    def setUpClass(cls):
        """Set up shared fixtures for all tests in the class."""
        cls.test_dir = tempfile.mkdtemp()
        cls.test_file = os.path.join(cls.test_dir, "test.txt")

    @classmethod
    def tearDownClass(cls):
        """Clean up shared fixtures after all tests in the class."""
        shutil.rmtree(cls.test_dir)

    def test_write_file(self):
        """Test writing to file."""
        with open(self.test_file, 'w') as f:
            f.write("test content")

        self.assertTrue(os.path.exists(self.test_file))

    def test_read_file(self):
        """Test reading from file."""
        with open(self.test_file, 'w') as f:
            f.write("test content")

        with open(self.test_file, 'r') as f:
            content = f.read()

        self.assertEqual(content, "test content")
```

## Assertions

### Common Assertions

```python
class TestAssertions(unittest.TestCase):

    def test_equality(self):
        """Test equality assertions."""
        self.assertEqual(2 + 2, 4)
        self.assertNotEqual(2 + 2, 5)

    def test_boolean(self):
        """Test boolean assertions."""
        self.assertTrue(True)
        self.assertFalse(False)

    def test_none(self):
        """Test None assertions."""
        self.assertIsNone(None)
        self.assertIsNotNone("not none")

    def test_instance(self):
        """Test instance type assertions."""
        self.assertIsInstance("string", str)
        self.assertNotIsInstance(123, str)

    def test_membership(self):
        """Test membership assertions."""
        self.assertIn("a", "banana")
        self.assertNotIn("x", "banana")

    def test_sequences(self):
        """Test sequence assertions."""
        self.assertEqual([1, 2, 3], [1, 2, 3])
        self.assertCountEqual([1, 2, 2, 3], [3, 2, 2, 1])  # ignores order

    def test_exceptions(self):
        """Test exception assertions."""
        with self.assertRaises(ValueError):
            raise ValueError("test error")

        with self.assertRaisesRegex(ValueError, "test"):
            raise ValueError("test error message")
```

## Mocking and Patching

### Using unittest.mock

```python
import unittest
from unittest.mock import Mock, patch, MagicMock

class TestWithMocking(unittest.TestCase):

    def test_mock_method(self):
        """Test using Mock objects."""
        mock_service = Mock()
        mock_service.get_data.return_value = {"result": "mocked"}

        result = mock_service.get_data()
        self.assertEqual(result["result"], "mocked")
        mock_service.get_data.assert_called_once()

    def test_patch_decorator(self):
        """Test using patch decorator."""
        with patch('module.function') as mock_func:
            mock_func.return_value = "patched"
            result = call_function()
            self.assertEqual(result, "patched")

    @patch('requests.get')
    def test_patch_method(self, mock_get):
        """Test using patch as method decorator."""
        mock_get.return_value.status_code = 200
        mock_get.return_value.json.return_value = {"data": "test"}

        # Call function that uses requests.get
        result = fetch_data()
        self.assertEqual(result["data"], "test")
```

## UI Automation

For Windows UI automation of the WPF app:
- **unittest** with **pywinauto** – simple Windows UI automation
- **unittest** with **WinAppDriver + Appium** – more structured, CI-friendly

Keep UI tests minimal and focused on critical user interactions.

## Best Practices

### Test Naming Conventions
- Use descriptive names that explain what the test verifies
- Follow the pattern: `test_method_name_condition_expected_result`
- Example: `test_calculate_total_valid_items_returns_correct_sum`

### Test Organization
- Group related tests in the same class
- Use `setUp` and `tearDown` for common fixtures
- Keep test methods focused on a single behavior

### Defensive Imports
```python
try:
    import clr
    CLR_AVAILABLE = True
except ImportError:
    CLR_AVAILABLE = False

@unittest.skipUnless(CLR_AVAILABLE, "pythonnet required")
class TestClrIntegration(unittest.TestCase):
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

We provide `test_config.py` with enhanced unittest features:

- **VerboseTestRunner**: Enhanced output with progress indicators
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
- Check that test classes inherit from `unittest.TestCase`
- Verify test methods start with `test_`

#### CLR Tests Failing
- Ensure pythonnet is installed: `pip install pythonnet`
- Verify .NET assemblies are built and accessible
- Check CLR import paths

#### Coverage Not Working
- Install coverage: `pip install coverage`
- Use `coverage run -m unittest discover`
- Check coverage configuration

### Debug Tips
- Use `python -m unittest -v` for verbose output
- Add print statements or logging in tests
- Use `pdb` for interactive debugging

## Integration with CI/CD

```yaml
# Example GitHub Actions workflow
- name: Run Tests
  run: |
    python -m unittest discover -v
    coverage run -m unittest discover
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
python -m pip install --upgrade coverage

# For CLR integration
python -m pip install --upgrade pythonnet

# For UI automation
python -m pip install --upgrade pywinauto
```

### Adding New Test Modules

1. Create test file following `test_*.py` naming
2. Inherit from `unittest.TestCase`
3. Include defensive imports for optional dependencies
4. Add `setUp`/`tearDown` methods as needed
5. Update CI configuration

## Resources

- [unittest Documentation](https://docs.python.org/3/library/unittest.html)
- [unittest.mock Documentation](https://docs.python.org/3/library/unittest.mock.html)
- [pythonnet Documentation](https://pythonnet.github.io/)
- [pywinauto Documentation](https://pywinauto.readthedocs.io/)
- [Coverage.py Documentation](https://coverage.readthedocs.io/)

---

**Last Updated:** October 2025
**Test Environment Status:** ✅ unittest standard library configured