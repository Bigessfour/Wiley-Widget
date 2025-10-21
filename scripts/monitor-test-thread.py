#!/usr/bin/env python3
"""
Monitor Test Thread Script for WileyWidget

This script monitors CPU/memory/disk during dotnet test runs, logs to logs/test-thread.log,
and parses coverage.cobertura.xml for test coverage.
"""

import argparse
import logging
import subprocess
import time
import xml.etree.ElementTree as ET
from pathlib import Path

import psutil

# Setup logging
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s - %(levelname)s - %(message)s",
    handlers=[logging.FileHandler("logs/test-thread.log"), logging.StreamHandler()],
)
logger = logging.getLogger(__name__)


class TestThreadMonitor:
    def __init__(self, timeout=90, verbose=False):
        self.timeout = timeout
        self.verbose = verbose

    def run_test_with_monitoring(self):
        """Run dotnet test while monitoring resources"""
        try:
            project_path = (
                Path(__file__).parent.parent
                / "WileyWidget.UiTests"
                / "WileyWidget.UiTests.csproj"
            )

            if not project_path.exists():
                logger.error(f"Test project not found at {project_path}")
                return False

            logger.info("=== Starting Test Thread Monitoring ===")
            start_time = time.time()

            # Start dotnet test process
            cmd = [
                "dotnet",
                "test",
                str(project_path),
                "--filter",
                "FullyQualifiedName=WileyWidget.UiTests.EndToEndStartupTests.E2E_01_FullApplicationStartup_WithTiming",
                "--verbosity",
                "normal",
                "--collect",
                "XPlat Code Coverage",
                "--results-directory",
                "TestResults/Monitoring",
            ]

            if self.verbose:
                logger.info(f"Running command: {' '.join(cmd)}")

            process = subprocess.Popen(
                cmd,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                text=True,
                cwd=Path(__file__).parent.parent,
            )

            # Monitor system resources while test runs
            self.monitor_resources(process, start_time)

            # Wait for completion
            stdout, stderr = process.communicate()

            end_time = time.time()
            total_time = end_time - start_time

            logger.info(f"=== Test Completed in {total_time:.2f}s ===")
            logger.info(f"Exit code: {process.returncode}")

            # Parse coverage results
            self.parse_coverage_results()

            if stderr:
                logger.error(f"Stderr: {stderr}")

            return process.returncode == 0

        except Exception as e:
            logger.error(f"Failed to run monitored test: {e}")
            return False

    def monitor_resources(self, test_process, start_time):
        """Monitor CPU, memory, and disk usage"""
        try:
            test_ps_process = psutil.Process(test_process.pid)

            peak_cpu = 0
            peak_memory = 0
            disk_reads = 0
            disk_writes = 0

            logger.info("Starting resource monitoring...")

            while test_process.poll() is None:
                elapsed = time.time() - start_time

                if elapsed > self.timeout:
                    logger.warning(
                        f"Test timeout reached ({self.timeout}s). Terminating."
                    )
                    test_process.terminate()
                    try:
                        test_process.wait(timeout=5)
                    except subprocess.TimeoutExpired:
                        test_process.kill()
                    break

                try:
                    # CPU and memory
                    cpu_percent = test_ps_process.cpu_percent(interval=1)
                    memory_info = test_ps_process.memory_info()
                    memory_mb = memory_info.rss / 1024 / 1024

                    # Disk I/O
                    io_counters = test_ps_process.io_counters()
                    if io_counters:
                        current_reads = io_counters.read_count
                        current_writes = io_counters.write_count

                        if disk_reads > 0:
                            read_diff = current_reads - disk_reads
                            write_diff = current_writes - disk_writes
                            if read_diff > 0 or write_diff > 0:
                                logger.info(
                                    f"[{elapsed:.2f}s] Disk I/O - Reads: {read_diff}, Writes: {write_diff}"
                                )

                        disk_reads = current_reads
                        disk_writes = current_writes

                    # Track peaks
                    peak_cpu = max(peak_cpu, cpu_percent)
                    peak_memory = max(peak_memory, memory_mb)

                    # Log significant resource usage
                    if self.verbose or cpu_percent > 10 or memory_mb > 100:
                        logger.info(
                            f"[{elapsed:.2f}s] CPU: {cpu_percent:.1f}%, Memory: {memory_mb:.1f}MB"
                        )

                except psutil.NoSuchProcess:
                    break
                except Exception as e:
                    logger.error(f"Error monitoring resources: {e}")

                time.sleep(2)

            logger.info(f"Peak CPU usage: {peak_cpu:.1f}%")
            logger.info(f"Peak memory usage: {peak_memory:.1f}MB")

        except Exception as e:
            logger.error(f"Resource monitoring failed: {e}")

    def parse_coverage_results(self):
        """Parse coverage XML results"""
        try:
            coverage_file = Path("TestResults/Monitoring/coverage.cobertura.xml")

            if not coverage_file.exists():
                logger.warning("Coverage file not found")
                return

            tree = ET.parse(coverage_file)
            root = tree.getroot()

            # Extract coverage summary
            total_lines = 0
            covered_lines = 0

            for package in root.findall(".//package"):
                for cls in package.findall(".//class"):
                    for line in cls.findall(".//line"):
                        if line.get("type") == "stmt":
                            total_lines += 1
                            if int(line.get("hits", 0)) > 0:
                                covered_lines += 1

            if total_lines > 0:
                coverage_percent = (covered_lines / total_lines) * 100
                logger.info(
                    f"Code coverage: {coverage_percent:.1f}% ({covered_lines}/{total_lines} lines)"
                )
            else:
                logger.warning("No coverage data found")

        except Exception as e:
            logger.error(f"Failed to parse coverage results: {e}")


def main():
    parser = argparse.ArgumentParser(
        description="Monitor Test Threading for WileyWidget"
    )
    parser.add_argument("--verbose", "-v", action="store_true", help="Verbose logging")
    parser.add_argument(
        "--timeout", "-t", type=int, default=90, help="Timeout in seconds"
    )

    args = parser.parse_args()

    # Ensure logs directory exists
    Path("logs").mkdir(exist_ok=True)

    monitor = TestThreadMonitor(timeout=args.timeout, verbose=args.verbose)

    success = monitor.run_test_with_monitoring()

    if success:
        logger.info("Test monitoring completed successfully")
    else:
        logger.error("Test monitoring failed")


if __name__ == "__main__":
    main()
