#!/usr/bin/env python3
"""
Test DI Startup Script

Simulates TestDiSetup.Initialize() by invoking dotnet run with diagnostic args,
logging service resolutions (e.g., MainViewModel) to logs/python-di.log
"""

import subprocess
import time
from pathlib import Path

import debugpy


def setup_debugpy():
    """Setup debugpy for remote debugging"""
    debugpy.listen(("localhost", 5679))
    print("Debugpy listening on localhost:5679")
    print("Attach debugger to continue...")
    debugpy.wait_for_client()


def run_dotnet_diagnostic():
    """Run dotnet with diagnostic args to simulate DI setup"""
    workspace_folder = Path(__file__).parent.parent
    logs_dir = workspace_folder / "logs"
    logs_dir.mkdir(exist_ok=True)
    log_file = logs_dir / "python-di.log"

    print(f"Logging to {log_file}")

    with open(log_file, "w") as f:
        f.write(f"DI Startup Test - {time.ctime()}\n")
        f.write("=" * 50 + "\n")

        # Try to run a simple dotnet command to check DI
        # Since we can't directly call TestDiSetup, we'll run the test project with filter
        cmd = [
            "dotnet",
            "test",
            str(
                workspace_folder / "WileyWidget.UiTests" / "WileyWidget.UiTests.csproj"
            ),
            "--filter",
            "FullyQualifiedName=WileyWidget.UiTests.EndToEndStartupTests.E2E_01_FullApplicationStartup_WithTiming",
            "--logger",
            "console;verbosity=detailed",
            "--no-build",  # Assume already built
        ]

        print(f"Running: {' '.join(cmd)}")

        try:
            result = subprocess.run(
                cmd, cwd=workspace_folder, capture_output=True, text=True, timeout=120
            )

            f.write(f"Exit Code: {result.returncode}\n")
            f.write(f"STDOUT:\n{result.stdout}\n")
            f.write(f"STDERR:\n{result.stderr}\n")

            if result.returncode == 0:
                print("DI test passed")
                f.write("RESULT: PASSED\n")
            else:
                print("DI test failed")
                f.write("RESULT: FAILED\n")

                # Try to extract error info
                if "InvalidOperationException" in result.stderr:
                    print("Found InvalidOperationException in DI setup")
                    f.write("ERROR TYPE: InvalidOperationException\n")

        except subprocess.TimeoutExpired:
            f.write("RESULT: TIMEOUT\n")
            print("DI test timed out")
        except Exception as e:
            f.write(f"ERROR: {str(e)}\n")
            print(f"Error running DI test: {e}")

    print(f"Log written to {log_file}")


def main():
    import argparse

    parser = argparse.ArgumentParser(description="Test DI Startup Script")
    parser.add_argument("--verbose", action="store_true", help="Enable verbose logging")
    parser.add_argument("--debug", action="store_true", help="Enable debugpy debugging")

    args = parser.parse_args()

    if args.debug:
        setup_debugpy()

    print("Testing DI Startup...")
    run_dotnet_diagnostic()
    print("DI Startup test completed")


if __name__ == "__main__":
    main()
