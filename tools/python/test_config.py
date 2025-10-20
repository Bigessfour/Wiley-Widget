"""
unittest configuration for Wiley Widget project.
Follows Python unittest documentation: https://docs.python.org/3/library/unittest.html

This module provides custom test configuration, test suites, and utilities
for running tests with the unittest framework.
"""

import os
import sys
import unittest

# Make the repository root importable so package-style imports like
# `tools.python.unittests...` work when this script is invoked from
# the tools/python folder or from the repo root.
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
if ROOT not in sys.path:
    sys.path.insert(0, ROOT)


def load_tests_from_module(module_name):
    """
    Load tests from a specific module.

    Args:
        module_name: Name of the module to load tests from

    Returns:
        unittest.TestSuite: Test suite containing all tests from the module
    """
    loader = unittest.TestLoader()
    try:
        module = __import__(module_name, fromlist=[""])
        return loader.loadTestsFromModule(module)
    except (ImportError, ModuleNotFoundError, unittest.SkipTest) as e:
        # Skip modules that can't be imported due to missing dependencies
        print(f"Skipping module {module_name}: {e}", file=sys.stderr)
        return unittest.TestSuite()
    except Exception as e:
        print(f"Warning: Could not load tests from {module_name}: {e}", file=sys.stderr)
        return unittest.TestSuite()


def create_test_suite(test_modules=None, include_clr=False):
    """
    Create a comprehensive test suite from multiple modules.

    Args:
        test_modules: List of module names to include. If None, discovers all.

    Returns:
        unittest.TestSuite: Combined test suite
    """
    suite = unittest.TestSuite()

    if test_modules is None:
        # Discover pure-python unit tests under tools/python/unittests
        start_dir = os.path.join(ROOT, "tools", "python", "unittests")
        if os.path.isdir(start_dir):
            discovered = unittest.defaultTestLoader.discover(start_dir, pattern="test_*.py", top_level_dir=ROOT)
            suite.addTest(discovered)

        # Discover CLR integration tests only when explicitly requested to avoid
        # importing assemblies during normal runs (which can cause import errors
        # when pythonnet/.NET assemblies are not available).
        if include_clr:
            clr_dir = os.path.join(ROOT, "tools", "python", "clr_tests")
            if os.path.isdir(clr_dir):
                discovered_clr = unittest.defaultTestLoader.discover(clr_dir, pattern="test_*.py", top_level_dir=ROOT)
                suite.addTest(discovered_clr)

        return suite

    # If explicit modules provided, try loading them as module names
    for module_name in test_modules:
        try:
            module_suite = load_tests_from_module(module_name)
            if module_suite.countTestCases() > 0:  # Only add if there are tests
                suite.addTest(module_suite)
        except Exception as e:
            print(
                f"Warning: Could not load tests from {module_name}: {e}",
                file=sys.stderr,
            )

    return suite


class VerboseTestResult(unittest.TextTestResult):
    """
    Custom test result class with enhanced verbosity.
    """

    def startTest(self, test):
        """Called when a test is about to run."""
        super().startTest(test)
        if self.showAll:
            self.stream.write(f"Running {test._testMethodName}... ")
            self.stream.flush()

    def addSuccess(self, test):
        """Called when a test succeeds."""
        super().addSuccess(test)
        if self.showAll:
            self.stream.writeln("✓")

    def addError(self, test, err):
        """Called when a test raises an error."""
        super().addError(test, err)
        if self.showAll:
            self.stream.writeln("✗ ERROR")

    def addFailure(self, test, err):
        """Called when a test fails."""
        super().addFailure(test, err)
        if self.showAll:
            self.stream.writeln("✗ FAIL")


class VerboseTestRunner(unittest.TextTestRunner):
    """
    Custom test runner with enhanced output formatting.
    """

    def __init__(self, **kwargs):
        """Initialize the test runner."""
        kwargs.setdefault("resultclass", VerboseTestResult)
        super().__init__(**kwargs)

    def run(self, test):
        """Run the test suite."""
        self.stream.writeln(f"Running {test.countTestCases()} tests...")
        self.stream.writeln("=" * 50)
        return super().run(test)


def run_tests_with_coverage(test_modules=None, include_clr=False):
    """
    Run tests with coverage reporting using coverage.py.

    Args:
        test_modules: List of specific modules to test, or None for all

    Returns:
        bool: True if all tests passed, False otherwise
    """
    try:
        import coverage
    except ImportError:
        print("coverage package not installed. Install with: pip install coverage")
        return False

    # Start coverage
    cov = coverage.Coverage(
        source=["tools/python", "wiley_widget"],
        omit=["*/tests/*", "*/test_*.py", "*/__pycache__/*"],
    )
    cov.start()

    try:
        # Run tests
        suite = create_test_suite(test_modules, include_clr=include_clr)
        runner = VerboseTestRunner(verbosity=2)
        result = runner.run(suite)

        # Generate coverage report
        cov.stop()
        cov.save()

        print("\nCoverage Report:")
        print("-" * 50)
        cov.report()

        # Generate HTML report
        cov.html_report(directory="htmlcov")
        print(f"HTML coverage report generated in: {os.path.abspath('htmlcov')}")

        return result.wasSuccessful()
    except Exception as e:
        print(f"Error running tests with coverage: {e}")
        return False
def _filter_out_clr_tests(suite: unittest.TestSuite) -> unittest.TestSuite:
    """Return a new TestSuite with any tests from tools.python.clr_tests removed.

    This is a conservative filter applied when CLR integration tests are not requested.
    """
    out = unittest.TestSuite()

    def _keep(test):
        name = getattr(test, "__module__", None) or str(test)
        return "tools.python.clr_tests" not in name

    for test in suite:
        if isinstance(test, unittest.TestSuite):
            # recurse
            sub = _filter_out_clr_tests(test)
            if sub.countTestCases() > 0:
                out.addTest(sub)
        else:
            if _keep(test):
                out.addTest(test)
    return out


def main():
    """Main entry point for running tests."""
    import argparse

    parser = argparse.ArgumentParser(description="Run Wiley Widget tests with unittest")
    parser.add_argument("--coverage", action="store_true", help="Run tests with coverage reporting")
    parser.add_argument("--verbose", "-v", action="store_true", help="Verbose output")
    parser.add_argument("--failfast", "-f", action="store_true", help="Stop on first failure")
    parser.add_argument("--modules", nargs="*", help="Specific modules to test")
    parser.add_argument(
        "--include-clr",
        action="store_true",
        help="Include CLR integration tests (requires pythonnet and dependent assemblies)",
    )

    args = parser.parse_args()

    include_clr = args.include_clr or os.environ.get("WILEY_ENABLE_CLR_TESTS") == "1"

    if args.coverage:
        success = run_tests_with_coverage(args.modules, include_clr=include_clr)
    else:
        suite = create_test_suite(args.modules, include_clr=include_clr)

        runner = VerboseTestRunner(verbosity=2 if args.verbose else 1, failfast=args.failfast)
        result = runner.run(suite)
        success = result.wasSuccessful()

    sys.exit(0 if success else 1)


if __name__ == "__main__":
    main()
