import os
import re
import subprocess
import time
from pathlib import Path

import pytest

# Project root (adjust if needed; assumes script is in workspace root)
PROJECT_ROOT = Path.cwd()
LOGS_DIR = PROJECT_ROOT / "logs"
APP_LOG = LOGS_DIR / "app.log"
SELFLOG = LOGS_DIR / "serilog-selflog.txt"

# Key log patterns to check
SUCCESS_PATTERNS = [
    r"DI container built successfully",
    r"Main window activated successfully",
    r"Successfully navigated to BudgetOverviewPage",  # From our fix
    r"DbContextFactory configured with SQL Server|activating degraded mode",  # Allows fallback
]

ERROR_PATTERNS = [
    r"DefaultConnection missing",  # Should be gone after appsettings.json
    r"InvalidOperationException.*EntityFrameworkCore",  # EF build failures
    r"AccessViolationException.*Navigate",  # The fatal AV crash
    r"Fatal error.*System\.AccessViolationException",
    r"Backend registration failed",  # Should not fatal-crash now
]


@pytest.fixture(scope="module", autouse=True)
def setup_test_env():
    """Prepare environment: Clean logs, ensure appsettings.json exists."""
    # Clean old logs
    for log_file in [APP_LOG, SELFLOG]:
        if log_file.exists():
            log_file.unlink()

    # Ensure logs dir exists
    LOGS_DIR.mkdir(exist_ok=True)

    # Verify appsettings.json (from our fix) exists with DefaultConnection
    appsettings = PROJECT_ROOT / "appsettings.json"
    if not appsettings.exists():
        pytest.skip("appsettings.json not found - create it with DefaultConnection")

    with open(appsettings, "r") as f:
        content = f.read()
        if "DefaultConnection" not in content:
            pytest.skip("appsettings.json missing DefaultConnection - add it for test")

    # Clean build artifacts (optional, catch error if multiple projects)
    # Detect project file (prefer exact known filename, fallback to any .csproj in root)
    global PROJECT_FILE
    PROJECT_FILE = PROJECT_ROOT / "Wiley-Widget.csproj"
    if not PROJECT_FILE.exists():
        # Fallback: first *.csproj in root
        candidates = list(PROJECT_ROOT.glob("*.csproj"))
        PROJECT_FILE = candidates[0] if candidates else None

    if PROJECT_FILE is None or not PROJECT_FILE.exists():
        print(
            "Warning: project file not found in workspace root - skipping clean (check project name/path)"
        )
    else:
        try:
            result = subprocess.run(
                ["dotnet", "clean", str(PROJECT_FILE)],
                cwd=PROJECT_ROOT,
                check=True,
                capture_output=True,
            )
            print("Clean successful.")
        except subprocess.CalledProcessError as e:
            out = (
                e.stdout.decode()
                if e.stdout
                else (e.stderr.decode() if e.stderr else "Unknown error")
            )
            print(f"Clean skipped (non-critical): {out}")
        except FileNotFoundError:
            print("dotnet not found - ensure .NET SDK is installed.")

    yield
    # Cleanup: No need to kill app here; process handles it


def run_dotnet_command(cmd, timeout=30):
    """Run dotnet command and return (proc, stdout, stderr, timed_out).

    timed_out is True if the command exceeded the timeout and was terminated.
    """
    proc = subprocess.Popen(
        cmd,
        cwd=PROJECT_ROOT,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        bufsize=1,
        universal_newlines=True,
    )
    try:
        stdout, stderr = proc.communicate(timeout=timeout)
        return proc, stdout, stderr, False
    except subprocess.TimeoutExpired:
        try:
            proc.kill()
        except Exception:
            pass
        stdout, stderr = proc.communicate()
        return proc, stdout, stderr, True


def parse_logs_for_patterns(log_files, patterns, invert=False):
    """Check if patterns appear in log files (invert: check for absence). Returns list of matches."""
    matches = []
    for log_file in log_files:
        if log_file.exists():
            with open(log_file, "r", encoding="utf-8") as f:
                content = f.read()
                for pattern in patterns:
                    found = re.findall(pattern, content, re.IGNORECASE)
                    if found:
                        matches.extend(found)
    return matches


def test_startup_sequence():
    """Test the full startup: Build, run, check logs for success/no errors."""
    # Step 1: Build the project
    print("Building project...")
    build_proc, build_out, build_err, build_timed_out = run_dotnet_command(
        ["dotnet", "build", str(PROJECT_FILE), "--no-restore"], timeout=120
    )
    assert not build_timed_out, "Build timed out"
    assert build_proc.returncode == 0, f"Build failed:\n{build_out}\n{build_err}"
    print("Build successful.")

    # Step 2: Run the app (briefly to test startup)
    print("Running app...")
    # Set environment to skip UI navigation in tests to avoid WinUI headless issues
    os.environ["TEST_NO_NAV"] = "1"
    run_cmd = ["dotnet", "run", str(PROJECT_FILE), "--no-build"]
    proc, run_out, run_err, run_timed_out = run_dotnet_command(
        run_cmd, timeout=20
    )  # 20s for startup + nav

    # Accept either a clean exit or a timeout termination for GUI runs
    if not run_timed_out and proc.returncode != 0:
        raise AssertionError(
            f"App exited abnormally with code {proc.returncode}:\n{run_out}\n{run_err}"
        )
    print("App startup completed (timed out gracefully if GUI).")

    # Step 3: Wait a bit for logs to flush (Serilog writes async)
    time.sleep(3)
    # If the app didn't write to disk logs (headless), dump captured stdout/stderr to app.log for parsing
    if not APP_LOG.exists():
        with open(APP_LOG, "w", encoding="utf-8") as lf:
            lf.write((run_out or "") + "\n" + (run_err or ""))

    # Step 4: Parse logs
    log_files = [APP_LOG, SELFLOG]
    success_matches = parse_logs_for_patterns(log_files, SUCCESS_PATTERNS)
    error_matches = parse_logs_for_patterns(log_files, ERROR_PATTERNS)

    # Assertions
    assert (
        len(success_matches) >= 2
    ), f"Missing key success logs. Found: {success_matches}. Check app.log."
    assert (
        len(error_matches) == 0
    ), f"Found error patterns in logs: {error_matches}. Check app.log for details."

    # Optional: Check for degraded mode
    degraded = any(
        re.search(
            r"degraded mode|InMemoryDatabase|fallback",
            log_file.read_text() if log_file.exists() else "",
            re.IGNORECASE,
        )
        for log_file in log_files
    )
    if degraded:
        print("✓ App in degraded mode (InMemory DB fallback) - expected if no LocalDB.")
    else:
        print("✓ App in full SQL mode.")

    print("Startup sequence test PASSED!")


# Additional test for degraded mode
def test_degraded_mode_startup():
    """Test startup with invalid connection to force fallback."""
    appsettings = PROJECT_ROOT / "appsettings.json"
    backup_path = PROJECT_ROOT / "appsettings.backup.json"

    # Backup and create invalid config
    appsettings.replace(backup_path)
    invalid_config = {
        "ConnectionStrings": {"DefaultConnection": "Server=invalid;Database=Test;"},
        "Logging": {"LogLevel": {"Default": "Information"}},
        "Database": {
            "DegradedModeName": "WileyWidget_Degraded",
            "EnableSensitiveDataLogging": True,
        },
    }

    import json

    with open(appsettings, "w") as f:
        json.dump(invalid_config, f, indent=2)

    try:
        # Build and run
        build_proc, _, _, build_timed_out = run_dotnet_command(
            ["dotnet", "build", str(PROJECT_FILE)], timeout=120
        )
        assert not build_timed_out
        assert build_proc.returncode == 0

        # Set test env to skip UI navigation
        os.environ["TEST_NO_NAV"] = "1"
        proc, _, _, run_timed_out = run_dotnet_command(
            ["dotnet", "run", str(PROJECT_FILE), "--no-build"], timeout=20
        )
        if not run_timed_out and proc.returncode != 0:
            raise AssertionError(f"Degraded run failed with code {proc.returncode}")

        time.sleep(3)
        if not APP_LOG.exists():
            # If log wasn't written to disk, dump captured output for parsing
            with open(APP_LOG, "w", encoding="utf-8") as lf:
                lf.write(
                    ("" if "run_out" not in locals() else (run_out or ""))
                    + "\n"
                    + ("" if "run_err" not in locals() else (run_err or ""))
                )
        log_files = [APP_LOG, SELFLOG]
        degraded_matches = parse_logs_for_patterns(
            log_files, [r"degraded mode|InMemory|fallback"], invert=False
        )
        error_matches = parse_logs_for_patterns(log_files, ERROR_PATTERNS)

        assert len(degraded_matches) > 0, "Expected degraded mode logs."
        assert (
            len(error_matches) == 0
        ), f"Unexpected errors in degraded mode: {error_matches}"

        print("Degraded mode test PASSED!")
    finally:
        # Restore
        appsettings.unlink(missing_ok=True)
        backup_path.replace(appsettings)
