#!/usr/bin/env python3
"""
E2E Local Testing Script for WileyWidget with Progress Indicator
Validates the full data pipeline: DB -> Repositories -> Services -> ViewModels -> UI

Usage:
    python scripts/e2e_test_local.py [phase]

Phases:
    all         Run all tests (default)
    build       Phase 1: Build solution
    repo        Phase 2: Repository tests
    viewmodel   Phase 2b: ViewModel tests
    integration Phase 2c: Integration tests
    theme       Phase 3: Theme validation
    grid        Phase 3b: Grid inspection
    batch       Phase 3c: Batch validation
    e2e         Phase 4: Full E2E pipeline
    publish     Phase 5: Publish release
"""

import subprocess
import sys
import json
import time
from pathlib import Path
from typing import Tuple

# Configuration
WORKSPACE_ROOT = Path(__file__).parent.parent
DOTNET_CMD = "dotnet"
CONFIG = "Release"

# Spinner characters (ASCII only)
SPINNER = ["|", "/", "-", "\\"]

# Test phases in order
PHASES = [
    ("build", "Build Solution", 1),
    ("repo", "Repository Tests (DB Layer)", 2),
    ("viewmodel", "ViewModel Tests (Service Layer)", 3),
    ("integration", "Integration Tests (DI Layer)", 4),
    ("theme", "Theme Validation (Syncfusion)", 5),
    ("grid", "Grid Inspection (Data Binding)", 6),
    ("batch", "Batch Form Validation", 7),
    ("e2e", "Full E2E Pipeline (DB -> UI)", 8),
    ("publish", "Publish Release", 9),
]

# Test results tracking
results = {phase[0]: None for phase in PHASES}

# Global tracking
start_time = None
current_phase = 0
total_phases = len(PHASES)
spinner_index = 0


def format_time(seconds):
    """Format seconds as human readable time."""
    if seconds < 60:
        return f"{int(seconds)}s"
    minutes = int(seconds / 60)
    secs = int(seconds % 60)
    return f"{minutes}m {secs}s"


def print_divider(width=80):
    """Print a horizontal divider line."""
    print("=" * width)


def print_header(title: str, width=80):
    """Print a section header."""
    print_divider(width)
    print(f"  {title}")
    print_divider(width)


def print_phase_header(title: str, phase_num: int):
    """Print a section header for a phase."""
    global current_phase
    current_phase = phase_num
    
    elapsed = time.time() - start_time if start_time else 0
    elapsed_str = format_time(elapsed)
    
    print()
    print_divider()
    print(f"  [{phase_num}/{total_phases}] {title}  [{elapsed_str}]")
    print_divider()


def run_command(
    cmd: list, description: str, check: bool = True
) -> Tuple[bool, str]:
    """
    Execute a shell command and return success status + output.

    Args:
        cmd: Command and arguments as list
        description: Human-readable description for logging
        check: If True, raise on non-zero exit code

    Returns:
        Tuple of (success: bool, output: str)
    """
    global spinner_index
    
    # Initial print
    spinner_char = SPINNER[spinner_index % len(SPINNER)]
    print(f"  {spinner_char} {description}...", end=" ", flush=True)
    spinner_index += 1

    start = time.time()
    try:
        result = subprocess.run(
            cmd,
            cwd=WORKSPACE_ROOT,
            capture_output=True,
            text=True,
            check=check,
        )
        elapsed = time.time() - start
        print(f"[OK {format_time(elapsed)}]")
        return True, result.stdout + result.stderr
    except subprocess.CalledProcessError as e:
        elapsed = time.time() - start
        print(f"[FAIL {format_time(elapsed)}]")
        error_msg = e.stderr if e.stderr else e.stdout
        return False, error_msg
    except Exception as e:
        print("[FAIL]")
        return False, str(e)


# =============================================================================
# PHASE 1: BUILD
# =============================================================================


def phase_build():
    """Phase 1: Build the solution."""
    print_phase_header("Build Solution", 1)

    success, output = run_command(
        [DOTNET_CMD, "build", "WileyWidget.sln", "--configuration", CONFIG],
        "Building solution",
    )

    results["build"] = success
    if not success:
        print(f"[FAIL] Build failed:\n{output}")
        return False

    return True


# =============================================================================
# PHASE 2: DATA PIPELINE TESTS
# =============================================================================


def phase_repo():
    """Phase 2a: Repository tests (DB <-> Repositories)."""
    print_phase_header("Repository Tests (DB Layer)", 2)

    success, output = run_command(
        [
            DOTNET_CMD,
            "test",
            "tests/WileyWidget.Services.Tests/WileyWidget.Services.Tests.csproj",
            "--filter",
            "Repository",
            "--configuration",
            CONFIG,
            "--logger",
            "console;verbosity=minimal",
        ],
        "Running repository tests",
    )

    results["repo"] = success
    if not success:
        print(f"[FAIL] Repository tests failed:\n{output}")
        return False

    print("[OK] All repository tests passed")
    return True


def phase_viewmodel():
    """Phase 2b: ViewModel tests (Repositories -> Services -> ViewModels)."""
    print_phase_header("ViewModel Tests (Service Layer)", 3)

    success, output = run_command(
        [
            DOTNET_CMD,
            "test",
            "tests/WileyWidget.WinForms.Tests/WileyWidget.WinForms.Tests.csproj",
            "--filter",
            "ViewModel",
            "--configuration",
            CONFIG,
            "--logger",
            "console;verbosity=minimal",
        ],
        "Running ViewModel tests",
    )

    results["viewmodel"] = success
    if not success:
        print(f"[FAIL] ViewModel tests failed:\n{output}")
        return False

    print("[OK] All ViewModel tests passed")
    return True


def phase_integration():
    """Phase 2c: Integration tests (DI + DbContext validation)."""
    print_phase_header("Integration Tests (DI Layer)", 4)

    success, output = run_command(
        [
            DOTNET_CMD,
            "test",
            "tests/WileyWidget.Services.Tests/WileyWidget.Services.Tests.csproj",
            "--filter",
            "Integration",
            "--configuration",
            CONFIG,
            "--logger",
            "console;verbosity=minimal",
        ],
        "Running integration tests",
    )

    results["integration"] = success
    if not success:
        print(f"[FAIL] Integration tests failed:\n{output}")
        return False

    print("[OK] All integration tests passed")
    return True


# =============================================================================
# PHASE 3: MCP HEADLESS FORM TESTS
# =============================================================================


def phase_theme():
    """Phase 3a: Validate form theme compliance."""
    print_phase_header("Theme Validation (Syncfusion)", 5)

    success, output = run_command(
        [
            DOTNET_CMD,
            "run",
            "--project",
            "tools/WileyWidgetMcpServer/WileyWidgetMcpServer.csproj",
            "--no-build",
            "--",
            "ValidateFormTheme",
            "WileyWidget.WinForms.Forms.MainForm",
            "Office2019Colorful",
        ],
        "Validating MainForm theme",
    )

    results["theme"] = success
    if not success:
        print(f"[FAIL] Theme validation failed:\n{output}")
        return False

    print("[OK] Theme validation passed")
    return True


def phase_grid():
    """Phase 3b: Inspect DataGrid configuration."""
    print_phase_header("Grid Inspection (Data Binding)", 6)

    success, output = run_command(
        [
            DOTNET_CMD,
            "run",
            "--project",
            "tools/WileyWidgetMcpServer/WileyWidgetMcpServer.csproj",
            "--no-build",
            "--",
            "InspectSfDataGrid",
            "WileyWidget.WinForms.Forms.DashboardPanel",
        ],
        "Inspecting DashboardPanel grid",
    )

    results["grid"] = success
    if not success:
        print(f"[FAIL] Grid inspection failed:\n{output}")
        return False

    print("[OK] Grid inspection passed")
    if output.strip():
        print("\nGrid Details:")
        print(output)
    return True


def phase_batch():
    """Phase 3c: Batch validate all forms."""
    print_phase_header("Batch Form Validation", 7)

    # Run batch validation
    cmd = [
        DOTNET_CMD,
        "run",
        "--project",
        "tools/WileyWidgetMcpServer/WileyWidgetMcpServer.csproj",
        "--no-build",
        "--",
        "BatchValidateForms",
        "null",
        "Office2019Colorful",
        "false",
        "json",
    ]

    spinner_char = SPINNER[spinner_index % len(SPINNER)]
    print(f"  {spinner_char} Running batch validation...", end=" ", flush=True)
    try:
        result = subprocess.run(
            cmd,
            cwd=WORKSPACE_ROOT,
            capture_output=True,
            text=True,
            check=False,
        )
        print("[OK]")

        # Try to parse JSON output
        try:
            output = result.stdout.strip()
            if output:
                report = json.loads(output)
                summary = report.get("summary", {})

                results["batch"] = summary.get("failed", 0) == 0

                # Display summary
                print("\nBatch Validation Results:")
                print(f"  Total Forms: {summary.get('total', 0)}")
                print(f"  Passed:      {summary.get('passed', 0)}")
                print(f"  Failed:      {summary.get('failed', 0)}")
                print(f"  Duration:    {summary.get('duration_ms', 0)}ms")

                if summary.get("failed", 0) > 0:
                    print("[FAIL] Batch validation failed")
                    return False

                print("[OK] All forms passed validation")
                return True
            else:
                print("[WARN] No JSON output from batch validation")
                results["batch"] = False
                return False
        except json.JSONDecodeError:
            print("[WARN] Could not parse JSON output")
            results["batch"] = False
            return False

    except Exception as e:
        print("[FAIL]")
        print(f"[FAIL] Batch validation error: {e}")
        results["batch"] = False
        return False


# =============================================================================
# PHASE 4: FULL E2E PIPELINE
# =============================================================================


def phase_e2e():
    """Phase 4: Full E2E pipeline test."""
    print_phase_header("Full E2E Pipeline (DB -> UI)", 8)

    # Create test script
    test_script = """
using System;
using System.Linq;
using System.Threading.Tasks;
using WileyWidget.WinForms.Configuration;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.McpServer.Helpers;

Console.WriteLine("[E2E] Building DI container...");
var services = DependencyInjection.CreateServiceCollection(includeDefaults: true);
var serviceProvider = services.BuildServiceProvider();

Console.WriteLine("[E2E] Creating mock MainForm...");
var mockMainForm = MockFactory.CreateMockMainForm(enableMdi: true);

Console.WriteLine("[E2E] Resolving DashboardViewModel...");
using var scope = serviceProvider.CreateScope();
var dashboardVm = scope.ServiceProvider.GetRequiredService<DashboardViewModel>();

Console.WriteLine("[E2E] Loading dashboard data...");
await dashboardVm.LoadCommand.ExecuteAsync(null);

Console.WriteLine("[E2E] Validating results...");
Console.WriteLine($"  Fiscal Year: {dashboardVm.FiscalYear}");
Console.WriteLine($"  Is Loading: {dashboardVm.IsLoading}");
Console.WriteLine($"  Error: {dashboardVm.ErrorMessage ?? "(none)"}");
Console.WriteLine($"  Total Budgeted: ${dashboardVm.TotalBudgeted:N2}");
Console.WriteLine($"  Total Actual: ${dashboardVm.TotalActual:N2}");
Console.WriteLine($"  Metrics Count: {dashboardVm.Metrics?.Count ?? 0}");

if (string.IsNullOrEmpty(dashboardVm.ErrorMessage) && dashboardVm.TotalBudgeted >= 0)
{
    Console.WriteLine("[E2E] PASS: Full data pipeline working");
    return 0;
}
else
{
    Console.WriteLine("[E2E] FAIL: Data pipeline error");
    return 1;
}
"""

    # Save script
    script_path = WORKSPACE_ROOT / "tmp" / "e2e-test.csx"
    script_path.parent.mkdir(parents=True, exist_ok=True)
    script_path.write_text(test_script)

    # Run E2E test via MCP
    success, output = run_command(
        [
            DOTNET_CMD,
            "run",
            "--project",
            "tools/WileyWidgetMcpServer/WileyWidgetMcpServer.csproj",
            "--no-build",
            "--",
            "RunHeadlessFormTest",
            str(script_path),
        ],
        "Running full E2E pipeline test",
    )

    results["e2e"] = success
    if not success:
        print(f"[FAIL] E2E test failed:\n{output}")
        return False

    print("[OK] Full E2E pipeline passed")
    if output.strip():
        print("\nE2E Test Output:")
        print(output)
    return True


# =============================================================================
# PHASE 5: PUBLISH
# =============================================================================


def phase_publish():
    """Phase 5: Publish release build."""
    print_phase_header("Publish Release", 9)

    success, output = run_command(
        [
            DOTNET_CMD,
            "publish",
            "src/WileyWidget.WinForms/WileyWidget.WinForms.csproj",
            "--configuration",
            CONFIG,
            "--output",
            "./publish",
            "--no-restore",
        ],
        "Publishing release build",
    )

    results["publish"] = success
    if not success:
        print(f"[FAIL] Publish failed:\n{output}")
        return False

    # Check file exists
    exe_path = WORKSPACE_ROOT / "publish" / "WileyWidget.WinForms.exe"
    if exe_path.exists():
        size_mb = exe_path.stat().st_size / (1024 * 1024)
        print(f"[OK] Published executable: {size_mb:.1f}MB")
    else:
        print("[WARN] Executable not found")
        results["publish"] = False
        return False

    return True


# =============================================================================
# MAIN
# =============================================================================


def print_summary():
    """Print test results summary."""
    total_time = time.time() - start_time
    
    print()
    print_divider()
    print("  Test Results Summary")
    print_divider()

    passed = 0
    total = 0
    for phase_id, title, _ in PHASES:
        result = results[phase_id]
        if result is None:
            status = "SKIPPED"
        elif result:
            status = "PASS"
            passed += 1
        else:
            status = "FAIL"
        total += 1
        print(f"  {title:40} {status:12}")

    print_divider()
    print(f"\nOverall: {passed}/{total} phases passed  [Total time: {format_time(total_time)}]\n")

    return all(r is not False for r in results.values())


def main():
    """Main entry point."""
    global start_time
    
    # Parse arguments
    phase_arg = sys.argv[1].lower() if len(sys.argv) > 1 else "all"

    print()
    print_divider()
    print("  WileyWidget E2E Local Testing")
    print("  Validating: DB -> Repositories -> Services -> ViewModels -> UI")
    print_divider()

    start_time = time.time()

    try:
        if phase_arg in ["all", "build"]:
            if not phase_build():
                return 1

        if phase_arg in ["all", "repo"]:
            if not phase_repo():
                print("[WARN] Repo tests failed, skipping dependent phases")
                return 1

        if phase_arg in ["all", "viewmodel"]:
            if not phase_viewmodel():
                print("[WARN] ViewModel tests failed, skipping dependent phases")
                return 1

        if phase_arg in ["all", "integration"]:
            if not phase_integration():
                print("[WARN] Integration tests failed, skipping dependent phases")
                return 1

        if phase_arg in ["all", "theme"]:
            if not phase_theme():
                return 1

        if phase_arg in ["all", "grid"]:
            if not phase_grid():
                return 1

        if phase_arg in ["all", "batch"]:
            if not phase_batch():
                return 1

        if phase_arg in ["all", "e2e"]:
            if not phase_e2e():
                return 1

        if phase_arg in ["all", "publish"]:
            if not phase_publish():
                return 1

        # Print summary
        success = print_summary()
        return 0 if success else 1

    except KeyboardInterrupt:
        print("\n[WARN] Tests interrupted by user")
        return 130
    except Exception as e:
        print(f"\n[FAIL] Error: {e}")
        import traceback
        traceback.print_exc()
        return 1


if __name__ == "__main__":
    sys.exit(main())
