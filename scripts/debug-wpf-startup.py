#!/usr/bin/env python3
"""
Debug WPF Startup Script

Launches WileyWidget.exe via subprocess, monitors for hangs,
and logs dispatcher activity using debugpy breakpoints.

Usage:
    python scripts/debug-wpf-startup.py [--verbose] [--timeout=60]
"""

import os
import subprocess
import sys
import threading
import time
from pathlib import Path

import debugpy


def setup_debugpy():
    """Setup debugpy for remote debugging"""
    debugpy.listen(("localhost", 5678))
    print("Debugpy listening on localhost:5678")
    print("Attach debugger to continue...")
    debugpy.wait_for_client()


def monitor_process(proc, timeout=60):
    """Monitor the process for hangs and log activity"""
    start_time = time.time()
    last_output = start_time

    def timeout_handler():
        if proc.poll() is None:
            print(f"Process timed out after {timeout} seconds")
            proc.terminate()
            try:
                proc.wait(timeout=5)
            except subprocess.TimeoutExpired:
                proc.kill()

    timer = threading.Timer(timeout, timeout_handler)
    timer.start()

    try:
        while proc.poll() is None:
            time.sleep(0.1)
            current_time = time.time()

            # Log periodic status
            if current_time - last_output >= 5:
                elapsed = current_time - start_time
                print(f"Process running for {elapsed:.1f} seconds...")
                last_output = current_time

        return_code = proc.returncode
        elapsed = time.time() - start_time
        print(
            f"Process completed with return code {return_code} in {elapsed:.1f} seconds"
        )

    finally:
        timer.cancel()


def run_startup_test():
    """Run the startup test to capture full output"""
    workspace_folder = Path(__file__).parent.parent
    logs_dir = workspace_folder / "logs"
    logs_dir.mkdir(exist_ok=True)
    log_file = logs_dir / "python-debug-startup.log"

    print(f"Running startup test and logging to {log_file}")

    with open(log_file, "w") as f:
        f.write(f"Startup Test Debug - {time.ctime()}\n")
        f.write("=" * 50 + "\n")

        # Run the test
        cmd = [
            "dotnet",
            "test",
            str(
                workspace_folder / "WileyWidget.UiTests" / "WileyWidget.UiTests.csproj"
            ),
            "--filter",
            "FullyQualifiedName=WileyWidget.UiTests.EndToEndStartupTests.E2E_01_FullApplicationStartup_WithTiming",
            "--verbosity",
            "normal",
            "--logger",
            "console;verbosity=detailed",
        ]

        print(f"Running: {' '.join(cmd)}")

        try:
            result = subprocess.run(
                cmd,
                cwd=workspace_folder,
                capture_output=True,
                text=True,
                timeout=120,  # 2 minutes for test
            )

            f.write(f"Exit Code: {result.returncode}\n")
            f.write(f"STDOUT:\n{result.stdout}\n")
            f.write(f"STDERR:\n{result.stderr}\n")

            if result.returncode == 0:
                print("Startup test passed")
                f.write("RESULT: PASSED\n")
            else:
                print("Startup test failed")
                f.write("RESULT: FAILED\n")

                # Look for exceptions
                if "Exception" in result.stdout or "Exception" in result.stderr:
                    print("Found exception in output")
                    f.write("EXCEPTION FOUND\n")

            # Print summary
            print(f"Test completed with exit code {result.returncode}")
            if result.returncode != 0:
                print("STDOUT (last 1000 chars):")
                print(result.stdout[-1000:])
                print("STDERR (last 1000 chars):")
                print(result.stderr[-1000:])

        except subprocess.TimeoutExpired:
            f.write("RESULT: TIMEOUT\n")
            print("Startup test timed out")
        except Exception as e:
            f.write(f"ERROR: {str(e)}\n")
            print(f"Error running startup test: {e}")

    print(f"Log written to {log_file}")


def run_exe_startup(args):
    """Run the exe startup monitoring"""
    # Get workspace folder
    workspace_folder = Path(__file__).parent.parent
    exe_path = workspace_folder / "bin" / "Debug" / "net9.0-windows" / "WileyWidget.exe"

    if not exe_path.exists():
        print(f"Error: WileyWidget.exe not found at {exe_path}")
        print("Please build the project first")
        sys.exit(1)

    print(f"Launching {exe_path}")
    print(f"Timeout: {args.timeout} seconds")
    print(f"Verbose: {args.verbose}")

    # Set environment
    env = os.environ.copy()
    env["DEBUG_STARTUP"] = "true"
    env["PYTHONPATH"] = str(workspace_folder)

    try:
        # Launch the process
        proc = subprocess.Popen(
            [str(exe_path)],
            stdout=subprocess.PIPE if args.verbose else subprocess.DEVNULL,
            stderr=subprocess.PIPE if args.verbose else subprocess.DEVNULL,
            env=env,
            cwd=workspace_folder,
        )

        print(f"Started process with PID {proc.pid}")

        # Monitor the process
        monitor_process(proc, args.timeout)

        # Get output if verbose
        if args.verbose:
            stdout, stderr = proc.communicate()
            if stdout:
                print("STDOUT:")
                print(stdout.decode())
            if stderr:
                print("STDERR:")
                print(stderr.decode())

    except KeyboardInterrupt:
        print("Interrupted by user")
        if "proc" in locals() and proc.poll() is None:
            proc.terminate()
    except Exception as e:
        print(f"Error: {e}")
        sys.exit(1)


def main():
    parser = argparse.ArgumentParser(description="Debug WPF Startup Script")
    parser.add_argument("--verbose", action="store_true", help="Enable verbose logging")
    parser.add_argument("--timeout", type=int, default=60, help="Timeout in seconds")
    parser.add_argument("--debug", action="store_true", help="Enable debugpy debugging")
    parser.add_argument(
        "--test", action="store_true", help="Run the startup test instead of the exe"
    )

    args = parser.parse_args()

    if args.debug:
        setup_debugpy()

    if args.test:
        run_startup_test()
    else:
        run_exe_startup(args)


if __name__ == "__main__":
    import argparse

    main()
