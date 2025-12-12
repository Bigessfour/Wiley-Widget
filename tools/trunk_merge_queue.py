"""
Python wrapper for Trunk Merge Queue CLI operations.

This module provides a Python interface to interact with Trunk's merge queue
via the trunk CLI. It supports querying status, submitting PRs, canceling,
and pausing/resuming the queue.

Requirements:
    - trunk CLI >= 1.25.0 installed and in PATH
    - trunk login completed (authenticated session)
"""

import json
import subprocess
from dataclasses import dataclass
from datetime import datetime
from typing import Optional


@dataclass
class TrunkCliResult:
    """Result from a trunk CLI command execution."""

    success: bool
    exit_code: int
    output: str
    pr_number: Optional[int] = None
    timestamp: Optional[datetime] = None

    def __post_init__(self) -> None:
        if self.timestamp is None:
            self.timestamp = datetime.now()

    def to_dict(self) -> dict:
        """Convert result to dictionary."""
        return {
            "success": self.success,
            "exit_code": self.exit_code,
            "output": self.output,
            "pr_number": self.pr_number,
            # trunk-ignore(pyright/reportOptionalMemberAccess)
            "timestamp": self.timestamp.isoformat() if self.timestamp else "",
        }


class TrunkMergeQueue:
    """Interface for Trunk merge queue operations via CLI."""

    @staticmethod
    def check_cli_installed() -> tuple[bool, Optional[str]]:
        """
        Check if trunk CLI is installed and get version.

        Returns:
            Tuple of (installed: bool, version: Optional[str])
        """
        try:
            result = subprocess.run(
                ["trunk", "--version"],
                capture_output=True,
                text=True,
                check=False,
                timeout=5,
            )
            if result.returncode == 0:
                return True, result.stdout.strip()
            return False, None
        except FileNotFoundError:
            return False, None
        except subprocess.TimeoutExpired:
            return False, None

    @staticmethod
    def get_status(
        pr_number: Optional[int] = None, verbose: bool = False
    ) -> TrunkCliResult:
        """
        Get merge queue status.

        Args:
            pr_number: Optional PR number to check specific PR status
            verbose: Show detailed output

        Returns:
            TrunkCliResult with command output
        """
        cmd = ["trunk", "merge", "status"]
        if pr_number is not None:
            cmd.append(str(pr_number))
        if verbose:
            cmd.append("--verbose")

        try:
            result = subprocess.run(
                cmd, capture_output=True, text=True, check=False, timeout=30
            )
            return TrunkCliResult(
                success=(result.returncode == 0),
                exit_code=result.returncode,
                output=result.stdout + result.stderr,
                pr_number=pr_number,
            )
        except subprocess.TimeoutExpired:
            return TrunkCliResult(
                success=False,
                exit_code=-1,
                output="Command timed out after 30 seconds",
                pr_number=pr_number,
            )
        except Exception as e:
            return TrunkCliResult(
                success=False,
                exit_code=-1,
                output=f"Error executing command: {str(e)}",
                pr_number=pr_number,
            )

    @staticmethod
    def submit_pr(pr_number: int, priority: Optional[int] = None) -> TrunkCliResult:
        """
        Submit a PR to the merge queue.

        Args:
            pr_number: PR number to submit
            priority: Queue priority (0-255, where 0 is highest priority)

        Returns:
            TrunkCliResult with command output
        """
        cmd = ["trunk", "merge", str(pr_number)]
        if priority is not None:
            if not 0 <= priority <= 255:
                raise ValueError("Priority must be between 0 and 255")
            cmd.extend(["--priority", str(priority)])

        try:
            result = subprocess.run(
                cmd, capture_output=True, text=True, check=False, timeout=30
            )
            return TrunkCliResult(
                success=(result.returncode == 0),
                exit_code=result.returncode,
                output=result.stdout + result.stderr,
                pr_number=pr_number,
            )
        except subprocess.TimeoutExpired:
            return TrunkCliResult(
                success=False,
                exit_code=-1,
                output="Command timed out after 30 seconds",
                pr_number=pr_number,
            )
        except Exception as e:
            return TrunkCliResult(
                success=False,
                exit_code=-1,
                output=f"Error executing command: {str(e)}",
                pr_number=pr_number,
            )

    @staticmethod
    def cancel_pr(pr_number: int) -> TrunkCliResult:
        """
        Cancel a PR from the merge queue.

        Args:
            pr_number: PR number to cancel

        Returns:
            TrunkCliResult with command output
        """
        cmd = ["trunk", "merge", "cancel", str(pr_number)]

        try:
            result = subprocess.run(
                cmd, capture_output=True, text=True, check=False, timeout=30
            )
            return TrunkCliResult(
                success=(result.returncode == 0),
                exit_code=result.returncode,
                output=result.stdout + result.stderr,
                pr_number=pr_number,
            )
        except subprocess.TimeoutExpired:
            return TrunkCliResult(
                success=False,
                exit_code=-1,
                output="Command timed out after 30 seconds",
                pr_number=pr_number,
            )
        except Exception as e:
            return TrunkCliResult(
                success=False,
                exit_code=-1,
                output=f"Error executing command: {str(e)}",
                pr_number=pr_number,
            )

    @staticmethod
    def pause_queue() -> TrunkCliResult:
        """
        Pause the merge queue (admin only).

        Returns:
            TrunkCliResult with command output
        """
        cmd = ["trunk", "merge", "pause"]

        try:
            result = subprocess.run(
                cmd, capture_output=True, text=True, check=False, timeout=30
            )
            return TrunkCliResult(
                success=(result.returncode == 0),
                exit_code=result.returncode,
                output=result.stdout + result.stderr,
            )
        except subprocess.TimeoutExpired:
            return TrunkCliResult(
                success=False, exit_code=-1, output="Command timed out after 30 seconds"
            )
        except Exception as e:
            return TrunkCliResult(
                success=False, exit_code=-1, output=f"Error executing command: {str(e)}"
            )

    @staticmethod
    def resume_queue() -> TrunkCliResult:
        """
        Resume the merge queue (admin only).

        Returns:
            TrunkCliResult with command output
        """
        cmd = ["trunk", "merge", "resume"]

        try:
            result = subprocess.run(
                cmd, capture_output=True, text=True, check=False, timeout=30
            )
            return TrunkCliResult(
                success=(result.returncode == 0),
                exit_code=result.returncode,
                output=result.stdout + result.stderr,
            )
        except subprocess.TimeoutExpired:
            return TrunkCliResult(
                success=False, exit_code=-1, output="Command timed out after 30 seconds"
            )
        except Exception as e:
            return TrunkCliResult(
                success=False, exit_code=-1, output=f"Error executing command: {str(e)}"
            )


def main() -> None:
    """CLI entry point for testing."""
    import argparse

    parser = argparse.ArgumentParser(description="Trunk Merge Queue CLI wrapper")
    parser.add_argument(
        "action",
        choices=["check", "status", "submit", "cancel", "pause", "resume"],
        help="Action to perform",
    )
    parser.add_argument("--pr", type=int, help="PR number (for status, submit, cancel)")
    parser.add_argument(
        "--priority", type=int, help="Priority for submit (0-255, 0 is highest)"
    )
    parser.add_argument("--verbose", action="store_true", help="Show verbose output")
    parser.add_argument("--json", action="store_true", help="Output result as JSON")

    args = parser.parse_args()

    queue = TrunkMergeQueue()

    if args.action == "check":
        installed, version = queue.check_cli_installed()
        result = {"installed": installed, "version": version}
        if args.json:
            print(json.dumps(result, indent=2))
        else:
            print(f"Trunk CLI installed: {installed}")
            if version:
                print(f"Version: {version}")

    elif args.action == "status":
        result = queue.get_status(pr_number=args.pr, verbose=args.verbose)
        if args.json:
            print(json.dumps(result.to_dict(), indent=2))
        else:
            print(f"Success: {result.success}")
            print(f"Exit Code: {result.exit_code}")
            print(f"Output:\n{result.output}")

    elif args.action == "submit":
        if args.pr is None:
            parser.error("--pr is required for submit action")
        result = queue.submit_pr(pr_number=args.pr, priority=args.priority)
        if args.json:
            print(json.dumps(result.to_dict(), indent=2))
        else:
            print(f"Success: {result.success}")
            print(f"Output:\n{result.output}")

    elif args.action == "cancel":
        if args.pr is None:
            parser.error("--pr is required for cancel action")
        result = queue.cancel_pr(pr_number=args.pr)
        if args.json:
            print(json.dumps(result.to_dict(), indent=2))
        else:
            print(f"Success: {result.success}")
            print(f"Output:\n{result.output}")

    elif args.action == "pause":
        result = queue.pause_queue()
        if args.json:
            print(json.dumps(result.to_dict(), indent=2))
        else:
            print(f"Success: {result.success}")
            print(f"Output:\n{result.output}")

    elif args.action == "resume":
        result = queue.resume_queue()
        if args.json:
            print(json.dumps(result.to_dict(), indent=2))
        else:
            print(f"Success: {result.success}")
            print(f"Output:\n{result.output}")


if __name__ == "__main__":
    main()
