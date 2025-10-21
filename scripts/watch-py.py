#!/usr/bin/env python3
"""
Python Watch Mode Script for Wiley Widget
Watches Python files for changes and auto-restarts the specified command.
"""

import argparse
import logging
import os
import subprocess
import sys

try:
    from watchgod import watch
except ImportError:
    print("watchgod not found. Installing...")
    subprocess.run([sys.executable, "-m", "pip", "install", "watchgod"], check=True)
    from watchgod import watch

# Import DefaultWatcher
from watchgod.watcher import DefaultWatcher

# Configure logging
logging.basicConfig(level=logging.INFO, format="[%(levelname)s] %(message)s")


class PythonWatcher:
    def __init__(self, command, watch_paths=None, ignore_patterns=None):
        self.command = command
        self.watch_paths = watch_paths or ["."]
        self.ignore_patterns = ignore_patterns or ["__pycache__", "*.pyc", ".git"]
        self.process = None
        self.should_restart = False

    def should_ignore(self, path):
        """Check if path should be ignored"""
        path_str = str(path)
        for pattern in self.ignore_patterns:
            if pattern in path_str:
                return True
        return False

    def start_process(self):
        """Start the subprocess"""
        if self.process and self.process.poll() is None:
            logging.info("Terminating existing process...")
            self.process.terminate()
            try:
                self.process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                logging.warning("Force killing process...")
                self.process.kill()
                self.process.wait()

        logging.info(f"Starting: {' '.join(self.command)}")
        try:
            # On Windows, use shell for builtin commands
            use_shell = os.name == "nt" and self.command[0] in [
                "echo",
                "dir",
                "type",
                "copy",
                "move",
                "del",
                "mkdir",
                "rmdir",
            ]
            if use_shell:
                self.process = subprocess.Popen(
                    " ".join(self.command),
                    stdout=subprocess.PIPE,
                    stderr=subprocess.STDOUT,
                    text=True,
                    bufsize=1,
                    universal_newlines=True,
                    shell=True,
                )
            else:
                self.process = subprocess.Popen(
                    self.command,
                    stdout=subprocess.PIPE,
                    stderr=subprocess.STDOUT,
                    text=True,
                    bufsize=1,
                    universal_newlines=True,
                )
            logging.info(f"Process started with PID: {self.process.pid}")
        except Exception as e:
            logging.error(f"Failed to start process: {e}")
            return False
        return True

    def monitor_output(self):
        """Monitor process output in real-time"""
        if not self.process or not self.process.stdout:
            return

        try:
            for line in iter(self.process.stdout.readline, ""):
                stripped_line = line.strip()
                if stripped_line:
                    logging.info(stripped_line)
                    print(f"[PROCESS] {stripped_line}")
                if self.should_restart:
                    break
        except Exception as e:
            logging.error(f"Error monitoring output: {e}")
            print(f"Error monitoring output: {e}")

    def run(self):
        logging.info(f"Watching paths: {self.watch_paths}")
        logging.info(f"Ignoring patterns: {self.ignore_patterns}")
        logging.info("Press Ctrl+C to stop watching")
        print("Press Ctrl+C to stop watching")

        # Start initial process
        if not self.start_process():
            return

        # Start output monitoring in background
        import threading

        output_thread = threading.Thread(target=self.monitor_output, daemon=True)
        output_thread.start()

        try:
            # Create ignore function for watchgod
            def ignore_func(path):
                return self.should_ignore(path)

            for changes in watch(*self.watch_paths, watcher_cls=DefaultWatcher):
                logging.info(f"\nDetected changes: {len(changes)} files")
                for change_type, path in changes:
                    logging.info(f"  {change_type.name}: {path}")

                self.should_restart = True
                if self.start_process():
                    self.should_restart = False
                    # Restart output monitoring
                    output_thread = threading.Thread(
                        target=self.monitor_output, daemon=True
                    )
                    output_thread.start()
                else:
                    logging.error("Failed to restart process")
                    print("Failed to restart process")
        except KeyboardInterrupt:
            logging.info("Stopping watch mode...")
        finally:
            if self.process and self.process.poll() is None:
                logging.info("Terminating process...")
                self.process.terminate()
                try:
                    self.process.wait(timeout=5)
                except subprocess.TimeoutExpired:
                    self.process.kill()
            logging.info("Watch mode stopped")
            print("Watch mode stopped")


def main():
    parser = argparse.ArgumentParser(description="Python Watch Mode for Wiley Widget")
    parser.add_argument("command", nargs="+", help="Command to run and watch")
    parser.add_argument(
        "--watch",
        "-w",
        nargs="+",
        default=["."],
        help="Paths to watch (default: current directory)",
    )
    parser.add_argument(
        "--ignore",
        "-i",
        nargs="+",
        default=["__pycache__", "*.pyc", ".git", "*.log", "logs/"],
        help="Patterns to ignore",
    )

    args = parser.parse_args()

    # Convert watch paths to absolute
    watch_paths = [os.path.abspath(p) for p in args.watch]

    watcher = PythonWatcher(args.command, watch_paths, args.ignore)
    watcher.run()


if __name__ == "__main__":
    main()
