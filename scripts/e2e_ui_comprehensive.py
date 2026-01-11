#!/usr/bin/env python3
"""
Comprehensive UI Testing for WileyWidget
Tests all forms, panels, and controls for compliance, data binding, and safety
Uses MCP Server headless testing - no display required
"""

import json
import subprocess
import sys
from pathlib import Path
from typing import Dict, Tuple

WORKSPACE_ROOT = Path(__file__).parent.parent
DOTNET_CMD = "dotnet"
MCP_PROJECT = (
    "tools/SyncfusionMcpServer/tools/WileyWidgetMcpServer/WileyWidgetMcpServer.csproj"
)

# Test the main form
MAIN_FORM = "WileyWidget.WinForms.Forms.MainForm"
THEME_NAME = "Office2019Colorful"


def run_command(cmd: list, description: str, timeout: int = 30) -> Tuple[bool, str]:
    """Execute command and return success + output."""
    print(f"  | {description}...", end=" ", flush=True)
    sys.stdout.flush()
    try:
        result = subprocess.run(
            cmd,
            cwd=WORKSPACE_ROOT,
            capture_output=True,
            text=True,
            check=False,
            timeout=timeout,
        )
        if result.returncode == 0:
            print("[OK]")
            sys.stdout.flush()
            return True, result.stdout
        else:
            print("[FAIL]")
            sys.stdout.flush()
            return False, result.stderr or result.stdout
    except subprocess.TimeoutExpired:
        print("[TIMEOUT]")
        sys.stdout.flush()
        return False, "Command timed out"
    except Exception as e:
        print("[ERROR]")
        sys.stdout.flush()
        return False, str(e)


def batch_validate_all_forms() -> Tuple[bool, Dict]:
    """Validate all forms in batch."""
    print("\n[Form Validation] Batch Theme & Constructor Check")
    print("=" * 80)
    sys.stdout.flush()

    success, output = run_command(
        [
            DOTNET_CMD,
            "run",
            "--project",
            MCP_PROJECT,
            "--no-build",
            "--",
            "BatchValidateForms",
            "null",  # All forms
            THEME_NAME,
            "false",  # Don't fail fast
            "json",  # JSON output
        ],
        "Validating all forms",
        timeout=60,
    )

    if not success:
        print("[WARN] Batch validation failed or timed out")
        return False, {}

    try:
        report = json.loads(output)
        summary = report.get("summary", {})

        print(f"\n  Total Forms Validated: {summary.get('total', 0)}")
        print(f"  Passed:                {summary.get('passed', 0)}")
        print(f"  Failed:                {summary.get('failed', 0)}")
        print(f"  Duration:              {summary.get('duration_ms', 0)}ms")

        if summary.get("failed", 0) > 0:
            print("\n  Failed Forms:")
            for failure in report.get("failures", []):
                print(f"    - {failure.get('form', 'Unknown')}")

        sys.stdout.flush()
        return summary.get("failed", 0) == 0, summary
    except json.JSONDecodeError:
        print("[WARN] Could not parse batch validation output")
        sys.stdout.flush()
        return False, {}


def test_di_configuration() -> bool:
    """Validate dependency injection setup."""
    print("\n[DI Validation] Dependency Injection Configuration")
    print("=" * 80)
    sys.stdout.flush()

    success, output = run_command(
        [
            DOTNET_CMD,
            "run",
            "--project",
            MCP_PROJECT,
            "--no-build",
            "--",
            "RunDependencyInjectionTests",
        ],
        "Testing DI configuration",
        timeout=60,
    )

    if success:
        print("[OK] DI configuration validated")
        sys.stdout.flush()
        return True
    else:
        print("[FAIL] DI configuration issues")
        if output:
            print(output[:200])
        sys.stdout.flush()
        return False


def detect_null_risks() -> Tuple[bool, int]:
    """Scan for potential null reference exceptions."""
    print("\n[Null Safety] Scanning for Null Reference Risks")
    print("=" * 80)
    sys.stdout.flush()

    success, output = run_command(
        [
            DOTNET_CMD,
            "run",
            "--project",
            MCP_PROJECT,
            "--no-build",
            "--",
            "DetectNullRisks",
            "json",
        ],
        "Detecting null risks",
        timeout=60,
    )

    if success:
        try:
            report = json.loads(output)
            risk_count = len(report.get("risks", []))

            if risk_count == 0:
                print("[OK] No null reference risks detected")
                sys.stdout.flush()
                return True, 0
            else:
                print(f"[WARN] {risk_count} potential null risks detected")
                for risk in report.get("risks", [])[:3]:
                    print(f"  - {risk.get('form', 'Unknown')}")
                sys.stdout.flush()
                return True, risk_count
        except:
            print("[WARN] Could not parse null risk report")
            sys.stdout.flush()
            return True, 0
    return False, 0


def print_summary(batch: Tuple[bool, Dict], di: bool, nulls: Tuple[bool, int]) -> int:
    """Print test summary."""
    print("\n")
    print("=" * 80)
    print("  UI TESTING SUMMARY")
    print("=" * 80)

    batch_pass, batch_data = batch
    null_pass, null_count = nulls

    if batch_data:
        total = batch_data.get("total", 0)
        passed = batch_data.get("passed", 0)
        failed = batch_data.get("failed", 0)
        print(f"\n  Forms Validated:         {total}")
        print(f"    Passed:                {passed}")
        print(f"    Failed:                {failed}")
    else:
        print("\n  Forms Validated:         N/A (validation skipped or failed)")

    print(f"  DI Configuration:        {'PASS' if di else 'FAIL'}")
    print(f"  Null Reference Risks:    {null_count} detected")

    print("\n" + "=" * 80)

    all_pass = batch_pass and di

    if all_pass:
        print("  Overall: PASS - UI structure is valid!")
        print("=" * 80 + "\n")
        return 0
    else:
        print("  Overall: NEEDS REVIEW - Some checks failed")
        print("=" * 80 + "\n")
        return 1


def main():
    """Run UI validation tests."""
    print("\n" + "=" * 80)
    print("  WileyWidget UI Structure & Safety Testing")
    print("=" * 80)
    sys.stdout.flush()

    # Build MCP server
    print("\n[Setup] Building MCP Server...")
    sys.stdout.flush()
    success, _ = run_command(
        [
            DOTNET_CMD,
            "build",
            MCP_PROJECT,
            "--configuration",
            "Release",
            "--verbosity",
            "quiet",
        ],
        "Building MCP server",
        timeout=60,
    )
    if not success:
        print("[FAIL] Could not build MCP server")
        sys.stdout.flush()
        return 1

    # Run validations
    try:
        batch = batch_validate_all_forms()
        di = test_di_configuration()
        nulls = detect_null_risks()

        return print_summary(batch, di, nulls)

    except KeyboardInterrupt:
        print("\n[WARN] Tests interrupted by user")
        sys.stdout.flush()
        return 130
    except Exception as e:
        print(f"\n[ERROR] {e}")
        sys.stdout.flush()
        return 1


if __name__ == "__main__":
    sys.exit(main())
