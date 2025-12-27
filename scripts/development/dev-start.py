#!/usr/bin/env python3
"""
Wiley Widget Development Startup Script (Python)
Handles cleanup and proper process management
"""

import argparse
import os
import subprocess
import sys


def cleanup_dotnet_processes():
    """Clean up orphaned .NET processes"""
    print("=== .NET Process Cleanup ===")

    try:
        # Get all processes
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
                    if "dotnet" in name.lower() or name == "WileyWidget.exe":
                        processes.append((name, pid))

        if not processes:
            print("[OK] No orphaned .NET processes found")
            return True

        print(f"Found {len(processes)} .NET-related process(es):")
        for name, pid in processes:
            print(f"  - {name} (PID: {pid})")

        # Kill processes
        killed_count = 0
        for name, pid in processes:
            try:
                subprocess.run(
                    ["taskkill", "/PID", pid, "/F"], capture_output=True, check=True
                )
                print(f"[OK] Killed {name} (PID: {pid})")
                killed_count += 1
            except subprocess.CalledProcessError as e:
                print(f"[ERROR] Failed to kill {name} (PID: {pid}): {e}")

        print(f"Cleaned up {killed_count} process(es)")

        # Clean up temporary files
        print("Cleaning up temporary files...")
        for dir_name in ["bin", "obj"]:
            if os.path.exists(dir_name):
                import shutil

                shutil.rmtree(dir_name, ignore_errors=True)
        print("[OK] Temporary build files cleaned")

        return True

    except Exception as e:
        print(f"[ERROR] Error during cleanup: {e}")
        return False


def check_conflicts():
    """Check for conflicting WileyWidget processes"""
    print("Checking for conflicts...")

    try:
        result = subprocess.run(
            ["tasklist", "/FI", "IMAGENAME eq WileyWidget.exe", "/FO", "CSV", "/NH"],
            capture_output=True,
            text=True,
        )

        lines = result.stdout.strip().split("\n")
        conflicts = [
            line for line in lines if line.strip() and "WileyWidget.exe" in line
        ]

        if conflicts:
            print(
                f"[WARN] Found {len(conflicts)} WileyWidget process(es) still running"
            )
            for conflict in conflicts:
                parts = conflict.split(",")
                if len(parts) >= 2:
                    pid = parts[1].strip('"')
                    print(f"  - PID: {pid}")

            response = input("Kill these processes? (y/N): ").lower().strip()
            if response == "y":
                for conflict in conflicts:
                    parts = conflict.split(",")
                    if len(parts) >= 2:
                        pid = parts[1].strip('"')
                        try:
                            subprocess.run(
                                ["taskkill", "/PID", pid, "/F"],
                                capture_output=True,
                                check=True,
                            )
                            print(f"[OK] Killed process (PID: {pid})")
                        except subprocess.CalledProcessError as e:
                            print(f"[ERROR] Failed to kill process (PID: {pid}): {e}")
                return True

        print("[OK] No conflicting processes found")
        return True

    except Exception as e:
        print(f"[ERROR] Error checking conflicts: {e}")
        return False


def clean_build_artifacts():
    """Clean build artifacts"""
    print("Cleaning build artifacts...")

    try:
        result = subprocess.run(
            ["dotnet", "clean", "WileyWidget.csproj"],
            capture_output=True,
            text=True,
            cwd=os.getcwd(),
        )

        if result.returncode == 0:
            print("[OK] Build artifacts cleaned")
            return True
        else:
            print(f"[WARN] Clean failed: {result.stderr}")
            return False

    except Exception as e:
        print(f"[ERROR] Error cleaning build: {e}")
        return False


def lock_azure_performance():
    """Lock in Azure performance enhancements"""
    print("Locking Azure performance enhancements...")
    try:
        result = subprocess.run(
            ["pwsh", "-File", "scripts/lock-azure-performance.ps1", "-SkipAuth"],
            capture_output=True,
            text=True,
            cwd=os.getcwd(),
        )
        if result.returncode == 0:
            print("[OK] Azure performance enhancements locked")
            return True
        else:
            print(f"[WARN] Performance locking failed: {result.stderr}")
            return False
    except Exception as e:
        print(f"[ERROR] Error locking performance: {e}")
        return False


def start_development(no_watch=False):
    """Start development environment"""
    print("Starting development...")

    if no_watch:
        print("Running without watch mode...")
        cmd = ["dotnet", "run", "--project", "WileyWidget.csproj"]
    else:
        print("Starting dotnet watch...")
        print("Press Ctrl+C to stop, then run cleanup script if needed")
        cmd = ["dotnet", "watch", "run", "--project", "WileyWidget.csproj"]

    try:
        subprocess.run(cmd, cwd=os.getcwd())
        return True
    except KeyboardInterrupt:
        print("Development session interrupted")
        return True
    except Exception as e:
        print(f"[ERROR] Error starting development: {e}")
        return False


def main():
    parser = argparse.ArgumentParser(description="Wiley Widget Development Startup")
    parser.add_argument(
        "--clean-only", action="store_true", help="Only clean up and exit"
    )
    parser.add_argument(
        "--no-watch", action="store_true", help="Start without watch mode"
    )
    parser.add_argument("--verbose", action="store_true", help="Verbose output")

    args = parser.parse_args()

    print("=== Wiley Widget Development Startup (Python) ===")

    # Step 1: Clean up orphaned processes
    print("Step 1: Cleaning up orphaned processes...")
    if not cleanup_dotnet_processes():
        print("[ERROR] Cleanup failed")
        return 1

    if args.clean_only:
        print("Clean-only mode - exiting")
        return 0

    # Step 2: Check for conflicts
    print("Step 2: Checking for conflicts...")
    if not check_conflicts():
        print("[ERROR] Conflict check failed")
        return 1

    # Step 3: Clean build artifacts
    print("Step 3: Cleaning build artifacts...")
    if not clean_build_artifacts():
        print("[WARN] Build clean failed, continuing...")

    # Step 3.5: Lock Azure performance
    print("Step 3.5: Locking Azure performance enhancements...")
    lock_azure_performance()

    # Step 4: Start development
    print("Step 4: Starting development...")
    if not start_development(args.no_watch):
        print("[ERROR] Failed to start development")
        return 1

    print("Development session ended")
    return 0


if __name__ == "__main__":
    sys.exit(main())
