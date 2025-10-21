#!/usr/bin/env python3
"""
Debug WPF Threading Script for WileyWidget

This script launches WileyWidget.exe via subprocess, monitors CPU/memory usage
with psutil, and logs dispatcher activity. Includes a 90-second timeout to detect hangs.
"""

import argparse
import logging
import os
import subprocess
import time
from pathlib import Path

import psutil

# Setup logging
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s - %(levelname)s - %(message)s",
    handlers=[logging.FileHandler("logs/wpf-thread.log"), logging.StreamHandler()],
)
logger = logging.getLogger(__name__)


class WPFThreadMonitor:
    def __init__(self, timeout=90, verbose=False):
        self.timeout = timeout
        self.verbose = verbose
        self.process = None
        self.start_time = None

    def launch_application(self):
        """Launch WileyWidget.exe and monitor it"""
        try:
            exe_path = (
                Path(__file__).parent.parent
                / "bin"
                / "Debug"
                / "net8.0-windows"
                / "WileyWidget.exe"
            )

            if not exe_path.exists():
                logger.error(f"WileyWidget.exe not found at {exe_path}")
                return False

            logger.info(f"Launching {exe_path}")
            self.start_time = time.time()

            # Launch the process
            self.process = subprocess.Popen(
                [str(exe_path)],
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0,
            )

            logger.info(f"Process started with PID: {self.process.pid}")
            return True

        except Exception as e:
            logger.error(f"Failed to launch application: {e}")
            return False

    def monitor_process(self):
        """Monitor CPU, memory, and detect hangs"""
        if not self.process or not self.start_time:
            return

        try:
            ps_process = psutil.Process(self.process.pid)
            last_cpu = 0
            last_memory = 0
            hang_detected = False

            while self.process.poll() is None:
                elapsed = time.time() - self.start_time

                if elapsed > self.timeout:
                    logger.warning(
                        f"Timeout reached ({self.timeout}s). Terminating process."
                    )
                    self.process.terminate()
                    try:
                        self.process.wait(timeout=5)
                    except subprocess.TimeoutExpired:
                        self.process.kill()
                    break

                try:
                    cpu_percent = ps_process.cpu_percent(interval=1)
                    memory_info = ps_process.memory_info()
                    memory_mb = memory_info.rss / 1024 / 1024

                    # Detect potential hangs (low CPU activity for extended periods)
                    if elapsed > 10 and cpu_percent < 1.0 and not hang_detected:
                        logger.warning(
                            f"Potential hang detected at {elapsed:.2f}s - CPU: {cpu_percent:.1f}%"
                        )
                        hang_detected = True

                    if (
                        self.verbose
                        or cpu_percent > 5
                        or abs(memory_mb - last_memory) > 10
                    ):
                        logger.info(
                            f"[{elapsed:.2f}s] CPU: {cpu_percent:.1f}%, Memory: {memory_mb:.1f}MB"
                        )
                    last_cpu = cpu_percent
                    last_memory = memory_mb

                except psutil.NoSuchProcess:
                    break
                except Exception as e:
                    logger.error(f"Error getting process info: {e}")
                    break

                time.sleep(2)

            # Get exit code
            exit_code = self.process.poll()
            logger.info(f"Process exited with code: {exit_code}")

            # Check for errors in stdout/stderr
            stdout, stderr = self.process.communicate()
            if stderr:
                logger.error(f"Stderr: {stderr.decode()}")
            if stdout:
                logger.info(f"Stdout: {stdout.decode()}")

        except Exception as e:
            logger.error(f"Error monitoring process: {e}")


def main():
    parser = argparse.ArgumentParser(description="Debug WPF Threading for WileyWidget")
    parser.add_argument("--verbose", "-v", action="store_true", help="Verbose logging")
    parser.add_argument(
        "--timeout", "-t", type=int, default=90, help="Timeout in seconds"
    )

    args = parser.parse_args()

    # Ensure logs directory exists
    Path("logs").mkdir(exist_ok=True)

    logger.info("=== WPF Threading Debug Script Started ===")
    logger.info(f"Timeout: {args.timeout}s, Verbose: {args.verbose}")

    monitor = WPFThreadMonitor(timeout=args.timeout, verbose=args.verbose)

    if monitor.launch_application():
        monitor.monitor_process()
    else:
        logger.error("Failed to launch application")

    logger.info("=== WPF Threading Debug Script Completed ===")


if __name__ == "__main__":
    main()
