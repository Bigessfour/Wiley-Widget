#!/usr/bin/env python3
"""
Test DI Threading Script for WileyWidget

This script simulates TestDiSetup.GetServiceProvider() by running dotnet test
with diagnostic args, logging service resolutions to logs/di-thread.log
"""

import argparse
import logging
import subprocess
import time
from pathlib import Path

# Setup logging
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s - %(levelname)s - %(message)s",
    handlers=[logging.FileHandler("logs/di-thread.log"), logging.StreamHandler()],
)
logger = logging.getLogger(__name__)


class DIThreadTester:
    def __init__(self, verbose=False):
        self.verbose = verbose

    def run_di_test(self):
        """Run dotnet test with DI diagnostics"""
        try:
            project_path = (
                Path(__file__).parent.parent
                / "WileyWidget.UiTests"
                / "WileyWidget.UiTests.csproj"
            )

            if not project_path.exists():
                logger.error(f"Test project not found at {project_path}")
                return False

            logger.info("=== Starting DI Threading Test ===")
            start_time = time.time()

            # Run dotnet test with specific filter and diagnostics
            cmd = [
                "dotnet",
                "test",
                str(project_path),
                "--filter",
                "FullyQualifiedName=WileyWidget.UiTests.EndToEndStartupTests.E2E_01_FullApplicationStartup_WithTiming",
                "--verbosity",
                "normal",
                "--logger",
                "console;verbosity=detailed",
                "--collect",
                "XPlat Code Coverage",
                "--results-directory",
                "TestResults/DI",
            ]

            if self.verbose:
                logger.info(f"Running command: {' '.join(cmd)}")

            # Run the test
            process = subprocess.Popen(
                cmd,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                text=True,
                cwd=Path(__file__).parent.parent,
            )

            # Monitor output in real-time
            service_resolutions = []
            timing_data = {}

            if process.stdout:
                while True:
                    output = process.stdout.readline()
                    if output == "" and process.poll() is not None:
                        break
                    if output:
                        line = output.strip()
                        if self.verbose:
                            print(line)

                        # Log service resolution attempts
                        if "resolving" in line.lower() or "resolved" in line.lower():
                            logger.info(f"Service resolution: {line}")
                            service_resolutions.append((time.time() - start_time, line))

                        # Log timing information
                        if "ms" in line and any(
                            phase in line.lower()
                            for phase in ["provider", "service", "viewmodel", "window"]
                        ):
                            logger.info(f"Timing: {line}")
                            # Extract phase name and timing
                            parts = line.split()
                            for i, part in enumerate(parts):
                                if part.endswith("ms") and i > 0:
                                    phase = " ".join(parts[:i])
                                    timing = part
                                    timing_data[phase] = timing
                                    break

                        # Log any errors
                        if "error" in line.lower() or "fail" in line.lower():
                            logger.error(f"Test error: {line}")
            else:
                logger.warning("No stdout available for monitoring")

            # Wait for completion
            stdout, stderr = process.communicate()

            end_time = time.time()
            total_time = end_time - start_time

            logger.info(f"=== DI Test Completed in {total_time:.2f}s ===")
            logger.info(f"Exit code: {process.returncode}")

            # Log service resolution summary
            logger.info(f"Service resolutions detected: {len(service_resolutions)}")
            for timestamp, resolution in service_resolutions:
                logger.info(f"  [{timestamp:.2f}s] {resolution}")

            # Log timing summary
            logger.info("Timing summary:")
            for phase, timing in timing_data.items():
                logger.info(f"  {phase}: {timing}")

            if stderr:
                logger.error(f"Stderr: {stderr}")

            return process.returncode == 0

        except Exception as e:
            logger.error(f"Failed to run DI test: {e}")
            return False


def main():
    parser = argparse.ArgumentParser(description="Test DI Threading for WileyWidget")
    parser.add_argument("--verbose", "-v", action="store_true", help="Verbose logging")

    args = parser.parse_args()

    # Ensure logs directory exists
    Path("logs").mkdir(exist_ok=True)

    tester = DIThreadTester(verbose=args.verbose)

    success = tester.run_di_test()

    if success:
        logger.info("DI threading test passed")
    else:
        logger.error("DI threading test failed")


if __name__ == "__main__":
    main()
