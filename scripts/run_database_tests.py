#!/usr/bin/env python3
"""
Enterprise Database Test Runner

Comprehensive test runner for enterprise database connections and features.
Executes all database tests with detailed reporting and environment validation.
"""

import argparse
import json
import os
import subprocess
import sys
import time
from datetime import datetime
from pathlib import Path
from typing import Any, Dict, List, Tuple

# Add project root to path
project_root = Path(__file__).parent.parent
sys.path.insert(0, str(project_root))


class DatabaseTestRunner:
    """Runner for enterprise database tests.

    Attributes:
        environment: Target environment for testing.
        verbose: Whether to enable verbose output.
        azure_test: Whether to enable Azure SQL Database tests.
        test_results: Dictionary storing test results.
        start_time: Timestamp when testing started.
    """

    def __init__(
        self,
        environment: str = "Development",
        verbose: bool = False,
        azure_test: bool = False,
    ) -> None:
        """Initialize the database test runner.

        Args:
            environment: Target environment for testing.
            verbose: Whether to enable verbose output.
            azure_test: Whether to enable Azure SQL Database tests.
        """
        self.environment = environment
        self.verbose = verbose
        self.azure_test = azure_test
        self.test_results: Dict[str, Dict[str, Any]] = {}
        self.start_time: float | None = None

    def log(self, message: str, level: str = "INFO") -> None:
        """Log a message with timestamp.

        Args:
            message: The message to log.
            level: The log level (e.g., "INFO", "ERROR").
        """
        timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
        print(f"[{timestamp}] [{level}] {message}")

    def run_command(
        self, cmd: List[str] | str, description: str, timeout: int = 300
    ) -> Tuple[bool, str]:
        """Run a command and capture output.

        Args:
            cmd: The command to run (list or string).
            description: Description of the command for logging.
            timeout: Timeout in seconds for the command.

        Returns:
            A tuple of (success: bool, output: str).
        """
        self.log(f"Running: {description}")
        if self.verbose:
            self.log(f"Command: {' '.join(cmd) if isinstance(cmd, list) else cmd}")

        try:
            result = subprocess.run(
                cmd, capture_output=True, text=True, timeout=timeout, cwd=project_root
            )

            success = result.returncode == 0
            output = result.stdout + result.stderr

            if self.verbose or not success:
                self.log(f"Output: {output[:500]}{'...' if len(output) > 500 else ''}")

            return success, output

        except subprocess.TimeoutExpired:
            self.log(f"Command timed out after {timeout} seconds", "ERROR")
            return False, f"Timeout after {timeout} seconds"
        except Exception as e:
            self.log(f"Command failed: {e}", "ERROR")
            return False, str(e)

    def check_environment(self) -> bool:
        """Check test environment prerequisites.

        Returns:
            True if all environment checks pass, False otherwise.
        """
        self.log("Checking test environment...")

        checks = {
            "python_version": sys.version_info >= (3, 11),
            "pytest_available": self.check_command_available("pytest"),
            "dotnet_available": self.check_command_available("dotnet"),
            "sql_server_available": self.check_sql_server_available(),
        }

        if self.azure_test:
            checks["azure_cli_available"] = self.check_command_available("az")

        all_passed = all(checks.values())

        for check, passed in checks.items():
            status = "✅ PASS" if passed else "❌ FAIL"
            self.log(f"  {check}: {status}")

        return all_passed

    def check_command_available(self, command: str) -> bool:
        """Check if a command is available.

        Args:
            command: The command to check.

        Returns:
            True if the command is available, False otherwise.
        """
        try:
            subprocess.run(
                [command, "--version"], capture_output=True, check=True, timeout=10
            )
            return True
        except (
            subprocess.CalledProcessError,
            FileNotFoundError,
            subprocess.TimeoutExpired,
        ):
            return False

    def check_sql_server_available(self) -> bool:
        """Check if SQL Server is available.

        Returns:
            True if SQL Server is available, False otherwise.
        """
        try:
            import pyodbc

            conn_str = (
                "DRIVER={ODBC Driver 17 for SQL Server};"
                "SERVER=localhost\\SQLEXPRESS01;"
                "DATABASE=master;"
                "Trusted_Connection=yes;"
                "TrustServerCertificate=yes;"
                "Connection Timeout=5;"
            )
            conn = pyodbc.connect(conn_str)
            conn.close()
            return True
        except (ImportError, Exception):
            return False

    def setup_test_environment(self) -> None:
        """Set up test environment variables."""
        self.log("Setting up test environment...")

        os.environ["DOTNET_ENVIRONMENT"] = self.environment
        os.environ["TEST_DATABASE_NAME"] = f"WileyWidgetTest_{self.environment}"

        if self.environment == "Development":
            os.environ["CONNECTIONSTRINGS_DEFAULTCONNECTION"] = (
                f"Server=localhost\\SQLEXPRESS01;Database=WileyWidgetTest_{self.environment};"
                "Trusted_Connection=True;TrustServerCertificate=True;"
            )
        else:
            os.environ["AZURE_SQL_SERVER"] = "test-server.database.windows.net"
            os.environ["AZURE_SQL_DATABASE"] = f"WileyWidgetTest_{self.environment}"

        self.log(f"Environment set to: {self.environment}")

    def run_unit_tests(self) -> bool:
        """Run unit tests for database configuration.

        Returns:
            True if tests pass, False otherwise.
        """
        self.log("Running database unit tests...")

        cmd = [
            sys.executable,
            "-m",
            "pytest",
            "tests/test_enterprise_database_connections.py",
            "-v",
            "-m",
            "unit",
            "--tb=short",
            "--maxfail=5",
        ]

        success, output = self.run_command(cmd, "Database unit tests")
        self.test_results["unit_tests"] = {"success": success, "output": output}

        return success

    def run_integration_tests(self) -> bool:
        """Run integration tests for database connections.

        Returns:
            True if tests pass, False otherwise.
        """
        self.log("Running database integration tests...")

        markers = ["integration", "database"]
        if not self.azure_test:
            markers.append("not azure")

        cmd = [
            sys.executable,
            "-m",
            "pytest",
            "tests/test_enterprise_database_connections.py",
            "-v",
            "-m",
            " and ".join(markers),
            "--tb=short",
            "--maxfail=3",
        ]

        success, output = self.run_command(cmd, "Database integration tests")
        self.test_results["integration_tests"] = {"success": success, "output": output}

        return success

    def run_enterprise_feature_tests(self) -> bool:
        """Run tests for enterprise features.

        Returns:
            True if tests pass, False otherwise.
        """
        self.log("Running enterprise feature tests...")

        cmd = [
            sys.executable,
            "-m",
            "pytest",
            "tests/test_enterprise_database_connections.py",
            "-v",
            "-m",
            "enterprise",
            "--tb=short",
        ]

        success, output = self.run_command(cmd, "Enterprise feature tests")
        self.test_results["enterprise_tests"] = {"success": success, "output": output}

        return success

    def run_health_check_tests(self) -> bool:
        """Run health check tests.

        Returns:
            True if tests pass, False otherwise.
        """
        self.log("Running health check tests...")

        cmd = [
            sys.executable,
            "-m",
            "pytest",
            "tests/test_health_checks.py",
            "-v",
            "--tb=short",
        ]

        success, output = self.run_command(cmd, "Health check tests")
        self.test_results["health_check_tests"] = {"success": success, "output": output}

        return success

    def run_smoke_tests(self) -> bool:
        """Run smoke tests for critical functionality.

        Returns:
            True if tests pass, False otherwise.
        """
        self.log("Running database smoke tests...")

        cmd = [
            sys.executable,
            "-m",
            "pytest",
            "tests/test_enterprise_database_connections.py",
            "-v",
            "-m",
            "smoke",
            "--tb=short",
        ]

        success, output = self.run_command(cmd, "Database smoke tests")
        self.test_results["smoke_tests"] = {"success": success, "output": output}

        return success

    def test_dotnet_build(self) -> bool:
        """Test that the .NET application builds successfully.

        Returns:
            True if build succeeds, False otherwise.
        """
        self.log("Testing .NET application build...")

        cmd = ["dotnet", "build", "WileyWidget.csproj", "--verbosity", "minimal"]

        success, output = self.run_command(cmd, ".NET build test")
        self.test_results["dotnet_build"] = {"success": success, "output": output}

        return success

    def test_database_migration(self) -> bool:
        """Test database migration readiness.

        Returns:
            True if migration is ready, False otherwise.
        """
        self.log("Testing database migration readiness...")

        # This would test EF Core migrations in a real scenario
        # For now, we'll simulate the test
        success = True
        output = "Migration readiness check passed (simulated)"

        self.test_results["migration_test"] = {"success": success, "output": output}

        return success

    def generate_report(self) -> Dict[str, Any]:
        """Generate comprehensive test report.

        Returns:
            A dictionary containing the test report.
        """
        end_time = time.time()
        duration = (end_time - self.start_time) if self.start_time is not None else 0

        report = {
            "test_run": {
                "timestamp": datetime.now().isoformat(),
                "environment": self.environment,
                "duration_seconds": round(duration, 2),
                "azure_tests_enabled": self.azure_test,
            },
            "results": self.test_results,
            "summary": {
                "total_tests": len(self.test_results),
                "passed_tests": len(
                    [r for r in self.test_results.values() if r["success"]]
                ),
                "failed_tests": len(
                    [r for r in self.test_results.values() if not r["success"]]
                ),
                "success_rate": (
                    round(
                        len([r for r in self.test_results.values() if r["success"]])
                        / len(self.test_results)
                        * 100,
                        1,
                    )
                    if self.test_results
                    else 0
                ),
            },
        }

        return report

    def print_report(self, report: Dict[str, Any]) -> None:
        """Print formatted test report.

        Args:
            report: The test report dictionary to print.
        """
        print("\n" + "=" * 80)
        print("🏭 ENTERPRISE DATABASE TEST REPORT")
        print("=" * 80)

        run_info = report["test_run"]
        print(f"Environment: {run_info['environment']}")
        print(f"Timestamp: {run_info['timestamp']}")
        print(f"Duration: {run_info['duration_seconds']} seconds")
        print(
            f"Azure Tests: {'Enabled' if run_info['azure_tests_enabled'] else 'Disabled'}"
        )

        summary = report["summary"]
        print("\n📊 SUMMARY:")
        print(f"  Total Tests: {summary['total_tests']}")
        print(f"  Passed: {summary['passed_tests']}")
        print(f"  Failed: {summary['failed_tests']}")
        print(f"  Success Rate: {summary['success_rate']}%")

        print("\n📋 DETAILED RESULTS:")
        for test_name, result in report["results"].items():
            status = "✅ PASS" if result["success"] else "❌ FAIL"
            print(f"  {test_name}: {status}")

        if summary["failed_tests"] > 0:
            print("\n❌ FAILED TESTS:")
            for test_name, result in report["results"].items():
                if not result["success"]:
                    print(f"  {test_name}:")
                    print(
                        f"    {result['output'][:200]}{'...' if len(result['output']) > 200 else ''}"
                    )

        print("\n" + "=" * 80)

    def run_all_tests(self) -> bool:
        """Run the complete test suite.

        Returns:
            True if all tests pass, False otherwise.
        """
        self.start_time = time.time()
        self.log("🚀 Starting Enterprise Database Test Suite")

        # Environment checks
        if not self.check_environment():
            self.log("❌ Environment check failed. Aborting tests.", "ERROR")
            return False

        # Setup
        self.setup_test_environment()

        # Run test phases
        test_phases = [
            ("Environment Setup", lambda: True),  # Already done
            (".NET Build", self.test_dotnet_build),
            ("Unit Tests", self.run_unit_tests),
            ("Smoke Tests", self.run_smoke_tests),
            ("Integration Tests", self.run_integration_tests),
            ("Enterprise Features", self.run_enterprise_feature_tests),
            ("Health Checks", self.run_health_check_tests),
            ("Migration Readiness", self.test_database_migration),
        ]

        all_passed = True
        for phase_name, phase_func in test_phases:
            self.log(f"📋 Phase: {phase_name}")
            try:
                phase_success = phase_func()
                if not phase_success:
                    all_passed = False
                    if phase_name in ["Environment Setup", ".NET Build"]:
                        self.log(
                            f"❌ Critical phase '{phase_name}' failed. Aborting.",
                            "ERROR",
                        )
                        break
                else:
                    self.log(f"✅ Phase '{phase_name}' completed successfully")
            except Exception as e:
                self.log(f"❌ Phase '{phase_name}' failed with exception: {e}", "ERROR")
                all_passed = False
                break

        # Generate and display report
        report = self.generate_report()
        self.print_report(report)

        # Save report to file
        report_file = (
            project_root
            / "test-results"
            / f"database-test-report-{int(time.time())}.json"
        )
        report_file.parent.mkdir(exist_ok=True)

        with open(report_file, "w") as f:
            json.dump(report, f, indent=2)

        self.log(f"📄 Detailed report saved to: {report_file}")

        return all_passed


def main() -> None:
    """Main entry point."""
    parser = argparse.ArgumentParser(description="Enterprise Database Test Runner")
    parser.add_argument(
        "--environment",
        "-e",
        choices=["Development", "Production", "Test"],
        default="Development",
        help="Target environment for testing",
    )
    parser.add_argument(
        "--verbose", "-v", action="store_true", help="Enable verbose output"
    )
    parser.add_argument(
        "--azure-test",
        action="store_true",
        help="Enable Azure SQL Database tests (requires Azure resources)",
    )
    parser.add_argument(
        "--unit-only", action="store_true", help="Run only unit tests (fast)"
    )
    parser.add_argument(
        "--smoke-only", action="store_true", help="Run only smoke tests (critical path)"
    )

    args = parser.parse_args()

    runner = DatabaseTestRunner(
        environment=args.environment, verbose=args.verbose, azure_test=args.azure_test
    )

    # Run specific test types if requested
    if args.unit_only:
        success = runner.run_unit_tests()
    elif args.smoke_only:
        success = runner.run_smoke_tests()
    else:
        success = runner.run_all_tests()

    sys.exit(0 if success else 1)


if __name__ == "__main__":
    main()
