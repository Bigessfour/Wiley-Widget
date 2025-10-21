#!/usr/bin/env python3
"""
Wiley Widget Development Startup Script with debugpy Remote Debugging
This version includes debugpy for advanced debugging capabilities
"""

import argparse
import logging
import os
import signal
import subprocess
import time
from datetime import datetime
from pathlib import Path

# Import debugpy for remote debugging
import debugpy

# Configure logging to file under /logs directory
LOG_DIR = Path("logs")
LOG_DIR.mkdir(parents=True, exist_ok=True)
LOG_FILE = LOG_DIR / f"debug-startup-debugpy-{datetime.now():%Y%m%d}.log"

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s - %(levelname)s - %(message)s",
    handlers=[logging.FileHandler(LOG_FILE, encoding="utf-8"), logging.StreamHandler()],
)

logger = logging.getLogger(__name__)

DEBUG_BREAKS_ENABLED = False


def maybe_debug_breakpoint():
    """Trigger a debugpy breakpoint only when explicitly enabled."""
    if not DEBUG_BREAKS_ENABLED:
        return

    try:
        debugpy.breakpoint()
    except Exception as exc:  # pragma: no cover - defensive logging only
        logger.debug("Skipping debug breakpoint: %s", exc)


def setup_debugpy(port=5678, wait_for_client=True):
    """Setup debugpy for remote debugging with port conflict handling"""
    print(f"🔍 Setting up debugpy on port {port}")

    try:
        # Check if debugpy is already listening
        if debugpy.is_client_connected():
            print("✅ debugpy already connected to client")
            return

        # Try to listen on the specified port, with fallback ports
        ports_to_try = [port, port + 1, port + 2, 5679, 5680]
        listening_port = None

        for try_port in ports_to_try:
            try:
                debugpy.listen(try_port)
                listening_port = try_port
                print(f"✅ debugpy listening on port {try_port}")
                logger.info("debugpy listening on port %s", try_port)
                break
            except Exception:
                continue

        if listening_port is None:
            print("❌ Failed to setup debugpy on any available port")
            print("   Continuing without debugpy...")
            return

        if wait_for_client:
            print("⏳ Waiting for debugger to attach...")
            logger.info("Waiting for debugger to attach on port %s", listening_port)
            print(
                f"   In VS Code: Run 'Python: Attach' debug configuration (port {listening_port})"
            )
            debugpy.wait_for_client()
            print("✅ Debugger attached!")
            logger.info("Debugger attached")

        # Set breakpoint right after setup
        maybe_debug_breakpoint()

    except Exception:
        print("❌ Failed to setup debugpy")
        print("   Continuing without debugpy...")
        logger.exception("Failed to setup debugpy")


def cleanup_dotnet_processes():
    """Clean up orphaned .NET processes with debugging"""
    print("=== .NET Process Cleanup ===")
    logger.info("Starting .NET process cleanup")
    maybe_debug_breakpoint()

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

        if processes:
            print(f"Found {len(processes)} .NET processes to clean up:")
            for name, pid in processes:
                print(f"  - {name} (PID: {pid})")

            # Debug breakpoint before killing processes
            maybe_debug_breakpoint()

            for name, pid in processes:
                try:
                    subprocess.run(
                        ["taskkill", "/F", "/PID", pid], capture_output=True, check=True
                    )
                    print(f"  ✅ Killed {name} (PID: {pid})")
                    logger.info("Killed process %s (PID %s)", name, pid)
                except subprocess.CalledProcessError:
                    print(f"  ❌ Failed to kill {name} (PID: {pid})")
                    logger.exception("Failed to kill process %s (PID %s)", name, pid)
                finally:
                    pass
        else:
            print("  ✅ No .NET processes found to clean up")
            logger.info("No .NET processes found to clean up")

    except subprocess.CalledProcessError as e:
        print(f"  ❌ Failed to list processes: {e}")
        logger.exception("Failed to list processes")


def cleanup_dotnet_artifacts():
    """Clean up .NET build artifacts with debugging"""
    print("\n=== .NET Artifact Cleanup ===")
    logger.info("Starting .NET artifact cleanup")
    maybe_debug_breakpoint()

    cleanup_dirs = ["bin", "obj", "TestResults"]

    for dir_name in cleanup_dirs:
        if os.path.exists(dir_name):
            print(f"  🗑️  Removing {dir_name}/")
            try:
                # Debug breakpoint before deletion
                maybe_debug_breakpoint()

                if os.name == "nt":  # Windows
                    subprocess.run(
                        ["rmdir", "/S", "/Q", dir_name], shell=True, check=True
                    )
                else:  # Unix-like
                    subprocess.run(["rm", "-rf", dir_name], check=True)
                print(f"  ✅ Removed {dir_name}/")
                logger.info("Removed %s/", dir_name)
            except subprocess.CalledProcessError as e:
                print(f"  ❌ Failed to remove {dir_name}/: {e}")
                logger.exception("Failed to remove %s/", dir_name)
        else:
            print(f"  ℹ️  {dir_name}/ not found")
            logger.info("%s/ not found", dir_name)


def build_application(incremental=True):
    """Build the WileyWidget application with debugging"""
    print("\n=== Building Application ===")
    logger.info("Building application (incremental=%s)", incremental)
    maybe_debug_breakpoint()

    try:
        if not incremental:
            # Clean first (only for full rebuilds)
            print("  🧹 Cleaning project...")
            try:
                result = subprocess.run(
                    ["dotnet", "clean", "WileyWidget.csproj"],
                    capture_output=True,
                    text=True,
                    check=True,
                )
                print("  ✅ Project cleaned")
            except subprocess.CalledProcessError:
                logger.exception("Failed to clean WileyWidget.csproj")
                raise

        # For incremental builds, ensure packages are restored if needed
        if incremental and not os.path.exists("obj/project.assets.json"):
            print("  📦 Restoring packages for incremental build...")
            try:
                result = subprocess.run(
                    ["dotnet", "restore", "WileyWidget.csproj"],
                    capture_output=True,
                    text=True,
                    check=True,
                )
                print("  ✅ Packages restored")
            except subprocess.CalledProcessError:
                logger.exception("Failed to restore packages for WileyWidget.csproj")
                raise

        # Debug breakpoint before build
        maybe_debug_breakpoint()

        # Build (incremental by default)
        build_type = "incrementally" if incremental else "from scratch"
        print(f"  🔨 Building project {build_type}...")
        try:
            result = subprocess.run(
                ["dotnet", "build", "WileyWidget.csproj", "--no-restore"],
                capture_output=True,
                text=True,
                check=True,
            )
            print("  ✅ Build successful")
            logger.info("Build successful")

            if result.stdout:
                print(f"Build output:\n{result.stdout}")
                logger.debug("Build output:\n%s", result.stdout)
        except subprocess.CalledProcessError:
            logger.exception("Failed to build WileyWidget.csproj")
            raise

    except subprocess.CalledProcessError as e:
        print(f"  ❌ Build failed: {e}")
        if e.stdout:
            print(f"STDOUT:\n{e.stdout}")
            logger.error("Build failed STDOUT:\n%s", e.stdout)
        if e.stderr:
            print(f"STDERR:\n{e.stderr}")
            logger.error("Build failed STDERR:\n%s", e.stderr)
        return False

    return True


def terminate_process(process, timeout=10):
    """Terminate the spawned application, killing the tree on Windows if needed."""
    if process is None or process.poll() is not None:
        return

    try:
        if os.name == "nt":
            try:
                process.send_signal(signal.CTRL_BREAK_EVENT)
                process.wait(timeout=timeout)
                return
            except Exception:
                pass

            try:
                subprocess.run(
                    ["taskkill", "/PID", str(process.pid), "/T", "/F"],
                    capture_output=True,
                    check=True,
                )
                process.wait(timeout=timeout)
                return
            except Exception:
                pass
        else:
            process.terminate()
            try:
                process.wait(timeout=timeout)
                return
            except subprocess.TimeoutExpired:
                pass

        process.kill()
        process.wait(timeout=timeout)
    except Exception:
        logger.exception("Failed to terminate process tree for PID %s", process.pid)


def run_application(debug_mode=False, startup_timeout=30):
    """Run the WileyWidget application with debugging"""
    print("\n=== Running Application ===")
    logger.info("Running application (debug_mode=%s)", debug_mode)
    maybe_debug_breakpoint()

    try:
        cmd = ["dotnet", "run", "--project", "WileyWidget.csproj"]

        if debug_mode:
            # Add debug configuration if needed
            print("  🐛 Running in debug mode...")
            logger.info("Running application in debug mode")
        else:
            print("  🚀 Running application...")
            logger.info("Running application")

        # Debug breakpoint before execution
        maybe_debug_breakpoint()

        # Run the application
        spawn_kwargs = {
            "stdout": subprocess.PIPE,
            "stderr": subprocess.PIPE,
            "text": True,
        }
        if os.name == "nt":
            spawn_kwargs["creationflags"] = subprocess.CREATE_NEW_PROCESS_GROUP
        else:
            spawn_kwargs["preexec_fn"] = os.setsid  # type: ignore[attr-defined]

        try:
            process = subprocess.Popen(cmd, **spawn_kwargs)
        except Exception:
            logger.exception("Failed to launch WileyWidget.exe")
            raise

        print(f"  ✅ Application started (PID: {process.pid})")
        logger.info("Application started PID %s", process.pid)
        print("     Press Ctrl+C to stop")

        # Monitor the process
        try:
            stdout, stderr = process.communicate(timeout=startup_timeout)
            if stdout:
                print(f"Application output:\n{stdout}")
                logger.debug("Application output:\n%s", stdout)
            if stderr:
                print(f"Application errors:\n{stderr}")
                logger.error("Application errors:\n%s", stderr)
        except subprocess.TimeoutExpired:
            print(f"  ⏳ Application is running after {startup_timeout}s...")
            logger.info("Application still running after %ss", startup_timeout)
            return process
        except KeyboardInterrupt:
            print("\n⏹️  Stopping application...")
            terminate_process(process)
            raise

    except Exception as e:
        print(f"  ❌ Failed to run application: {e}")
        logger.exception("Failed to run application")
        return None


def monitor_startup_timing(skip_cleanup=False):
    """Monitor startup timing with debugging"""
    print("\n=== Startup Timing Analysis ===")
    maybe_debug_breakpoint()

    start_time = time.time()

    # Measure each phase
    phases = []

    # Phase 1: Process cleanup (optional)
    if not skip_cleanup:
        phase_start = time.time()
        cleanup_dotnet_processes()
        phases.append(("Process Cleanup", time.time() - phase_start))

    # Phase 2: Artifact cleanup (optional)
    if not skip_cleanup:
        phase_start = time.time()
        cleanup_dotnet_artifacts()
        phases.append(("Artifact Cleanup", time.time() - phase_start))

    # Phase 3: Build
    phase_start = time.time()
    build_success = build_application(incremental=True)
    phases.append(("Build", time.time() - phase_start))

    if build_success:
        # Phase 4: Application startup
        phase_start = time.time()
        run_application()
        phases.append(("Application Start", time.time() - phase_start))

    total_time = time.time() - start_time

    # Debug breakpoint for timing analysis
    maybe_debug_breakpoint()

    print("\n=== Timing Report ===")
    for phase, duration in phases:
        print(f"  {phase}: {duration:.2f}s")
    print(f"  Total: {total_time:.2f}s")


def main():
    """Main startup function with debugpy integration"""
    parser = argparse.ArgumentParser(description="Wiley Widget Startup with debugpy")
    parser.add_argument(
        "--debug-port", type=int, default=5678, help="Port for debugpy (default: 5678)"
    )
    parser.add_argument(
        "--no-wait", action="store_true", help="Don't wait for debugger to attach"
    )
    parser.add_argument(
        "--timing", action="store_true", help="Run with timing analysis"
    )
    parser.add_argument(
        "--skip-cleanup",
        action="store_true",
        help="Skip cleanup steps for faster development iterations",
    )
    parser.add_argument(
        "--skip-debugpy",
        action="store_true",
        help="Skip debugpy setup and run normally",
    )
    parser.add_argument(
        "--debug-breaks",
        action="store_true",
        help="Pause at scripted debug breakpoints",
    )
    parser.add_argument(
        "--startup-timeout",
        type=int,
        default=30,
        help="Seconds to wait for app output before assuming it is running",
    )

    args = parser.parse_args()

    global DEBUG_BREAKS_ENABLED
    DEBUG_BREAKS_ENABLED = args.debug_breaks and not args.skip_debugpy
    if args.debug_breaks and args.skip_debugpy:
        logger.warning("--debug-breaks ignored because --skip-debugpy is set")
    elif DEBUG_BREAKS_ENABLED:
        print("🐞 Debug breakpoints enabled (script will pause at key checkpoints)")
        logger.info("Debug breakpoints enabled")

    print("🚀 Wiley Widget Development Startup (debugpy enabled)")
    print("=" * 50)

    # Setup debugpy unless skipped
    if not args.skip_debugpy:
        setup_debugpy(port=args.debug_port, wait_for_client=not args.no_wait)

    # Main startup logic with debugging
    maybe_debug_breakpoint()

    if args.timing:
        monitor_startup_timing(skip_cleanup=args.skip_cleanup)
    else:
        # Standard startup sequence
        if not args.skip_cleanup:
            cleanup_dotnet_processes()
            cleanup_dotnet_artifacts()

    if build_application(incremental=args.skip_cleanup):
        process = run_application(startup_timeout=args.startup_timeout)
        if process:
            try:
                process.wait()
            except KeyboardInterrupt:
                print("\n⏹️  Stopping application...")
                terminate_process(process)


if __name__ == "__main__":
    main()
