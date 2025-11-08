# WileyWidget UI Testing with Python

This directory contains Python-based UI testing capabilities for the WileyWidget application using `pywinauto` for Windows UI automation.

## Setup

### 1. Install Dependencies

```bash
# Install UI testing dependencies
pip install -r requirements-test.txt

# Or install manually
pip install pywinauto pillow opencv-python psutil
```

### 2. Build the Application

```bash
# Build WileyWidget for testing
dotnet build WileyWidget.csproj --configuration Debug
```

## Running UI Tests

### Basic UI Tests

```bash
# Run all UI tests
python -m pytest tests/ -m ui -v

# Run specific UI test file
python -m pytest tests/test_ui_main_window.py -v

# Run with coverage
python -m pytest tests/ -m ui --cov=. --cov-report=html
```

### UI Testing Runner Script

Use the convenient runner script for common UI testing tasks:

```bash
# Install dependencies and run tests
python scripts/ui_test_runner.py --install-deps --run-tests

# Inspect UI elements
python scripts/ui_test_runner.py --inspect-ui --app-path bin/Debug/net9.0-windows/WileyWidget.exe

# Take UI screenshot
python scripts/ui_test_runner.py --screenshot ui_screenshot.png --app-path bin/Debug/net9.0-windows/WileyWidget.exe
```

## UI Debugging Tools

### UI Inspection

```bash
# Inspect all UI elements and save to JSON
python scripts/ui_debug.py --app bin/Debug/net9.0-windows/WileyWidget.exe --inspect --output ui_elements.json
```

### UI Screenshots

```bash
# Take screenshot of the running application
python scripts/ui_debug.py --app bin/Debug/net9.0-windows/WileyWidget.exe --screenshot screenshot.png
```

### Interaction Testing

```bash
# Test basic UI interactions
python scripts/ui_debug.py --app bin/Debug/net9.0-windows/WileyWidget.exe --test-interactions --output interaction_results.json
```

## Test Structure

### UI Test Files

- `test_ui_main_window.py` - Tests for the main application window
- `test_ui_dashboard.py` - Tests for dashboard-specific functionality
- `conftest.py` - Pytest fixtures for UI testing

### Test Categories

- `@pytest.mark.ui` - UI tests (require running application)
- `@pytest.mark.slow` - Slow tests (> 5s)
- `@pytest.mark.smoke` - Critical path tests

## UI Test Fixtures

### `ui_app`

- Launches the WileyWidget application
- Automatically handles cleanup

### `ui_main_window`

- Provides access to the main application window
- Waits for window to be ready

### `ui_app_path`

- Path to the WileyWidget executable

## Debugging UI Issues

### 1. Element Inspection

```python
# In a test or debug session
from pywinauto import Application

app = Application(backend="uia").start("path/to/WileyWidget.exe")
main_window = app.window(title_re=".*Wiley.*")

# Inspect all elements
elements = main_window.find_elements()
for elem in elements:
    print(f"Type: {elem.control_type}, Title: {elem.window_text()}")
```

### 2. Visual Debugging

```python
# Take screenshot for visual inspection
from PIL import ImageGrab
screenshot = ImageGrab.grab()
screenshot.save("debug_screenshot.png")
```

### 3. Interaction Recording

```python
# Record UI interactions
main_window.click()
main_window.type_keys("test input")
# Check application response
```

### 4. Debug UI Inspection Script

For debugging Syncfusion WPF control rendering issues (e.g., SfDataGrid, Chart controls):

1. **Set Exception Breakpoints**: In VS Code debugger panel, enable "Python: Uncaught Exceptions" and "Python: Raised Exceptions" in the Breakpoints section
2. **Run Configuration**: Use VS Code's "Debug UI Inspection Script" launch configuration
3. **Inspect WPF Rendering**: The script has debugpy breakpoints before subprocess operations to catch:
   - Syncfusion control initialization failures
   - Data binding problems
   - WPF rendering issues
   - UI thread blocking

```python
# Run from VS Code debugger to catch WPF rendering issues
python scripts/quick_ui_inspect.py
```

**Note**: Exception breakpoints for Python debugging are configured in the VS Code debugger UI, not in launch.json.

## Common Issues

### Application Won't Start

- Ensure the application is built in Debug configuration
- Check that all dependencies are installed
- Verify the executable path is correct

### Elements Not Found

- Wait for UI to fully load with `time.sleep(2)`
- Use different search criteria (title, control_type, class_name)
- Check if elements are in child windows

### Test Timeouts

- Increase wait times for slower operations
- Use `pytest-timeout` plugin for long-running tests
- Split complex tests into smaller ones

## Best Practices

### Test Organization

- Keep UI tests separate from unit tests
- Use descriptive test names
- Group related tests in classes

### Reliability

- Wait for UI elements before interacting
- Handle expected failures gracefully
- Use appropriate assertion messages

### Performance

- Minimize screenshot usage in CI
- Use `pytest-xdist` for parallel execution
- Skip UI tests when not needed

## CI/CD Integration

Add to your CI pipeline:

```yaml
- name: Run UI Tests
  run: |
    pip install -r requirements-test.txt
    dotnet build WileyWidget.csproj --configuration Debug
    python -m pytest tests/ -m "ui and not slow" --tb=short
```

## Troubleshooting

### Import Errors

```bash
# Ensure all dependencies are installed
pip install pywinauto pillow opencv-python psutil
```

### Application Crashes

- Check application logs
- Use try/catch blocks in tests
- Run tests individually to isolate issues

### Element Not Interactable

- Wait for element to be enabled: `elem.wait('enabled', timeout=10)`
- Check if element is obscured by other windows
- Use different interaction methods (click_input, double_click, etc.)
