#!/usr/bin/env python3
"""
Clean up orphaned .NET processes during development
Run this before starting new development sessions to prevent conflicts
"""

import argparse
import os
import shutil
import subprocess
import sys


def get_dotnet_processes():
    """Get all .NET-related processes"""
    try:
        result = subprocess.run(
            ["tasklist", "/FO", "CSV", "/NH"],
            capture_output=True,
            text=True,
            check=True,
        )

        processes = []
        for line in result.stdout.strip().split("\n"):
            if line.strip():
                parts = line.split(",")
                if len(parts) >= 2:
                    name = parts[0].strip('"')
                    pid = parts[1].strip('"')
                    if (
                        "dotnet" in name.lower()
                        or "testhost" in name.lower()
                        or name == "WileyWidget.exe"
                    ):
                        processes.append((name, pid))

        return processes

    except Exception as e:
        print(f"❌ Error getting processes: {e}")
        return []


def cleanup_processes(force=False, dry_run=False):
    """Clean up .NET processes"""
    print("=== .NET Process Cleanup ===")

    processes = get_dotnet_processes()

    if not processes:
        print("✅ No orphaned .NET processes found")
        return True

    print(f"Found {len(processes)} .NET-related process(es):")
    for name, pid in processes:
        print(f"  - {name} (PID: {pid})")

    if dry_run:
        print("Dry run mode - no processes killed")
        return True

    if not force:
        try:
            response = input("Kill these processes? (y/N): ").lower().strip()
            if response not in ["y", "yes"]:
                print("Aborted")
                return False
        except KeyboardInterrupt:
            print("Aborted")
            return False

    # Kill processes
    killed_count = 0
    for name, pid in processes:
        try:
            subprocess.run(
                ["taskkill", "/PID", pid, "/F"], capture_output=True, check=True
            )
            print(f"✅ Killed {name} (PID: {pid})")
            killed_count += 1
        except subprocess.CalledProcessError as e:
            print(f"❌ Failed to kill {name} (PID: {pid}): {e}")

    print(f"Cleaned up {killed_count} process(es)")

    # Clean up temporary files
    print("Cleaning up temporary files...")
    for dir_name in ["bin", "obj"]:
        if os.path.exists(dir_name):
            try:
                shutil.rmtree(dir_name)
                print(f"✅ Removed {dir_name}/")
            except Exception as e:
                print(f"⚠️  Could not remove {dir_name}/: {e}")

    print("✅ Temporary build files cleaned")
    return True


def main():
    parser = argparse.ArgumentParser(description="Clean up .NET processes")
    parser.add_argument(
        "-f", "--force", action="store_true", help="Force kill without confirmation"
    )
    parser.add_argument(
        "-d",
        "--dry-run",
        action="store_true",
        help="Show what would be killed without actually killing",
    )

    args = parser.parse_args()

    success = cleanup_processes(force=args.force, dry_run=args.dry_run)
    sys.exit(0 if success else 1)


if __name__ == "__main__":
    main()
