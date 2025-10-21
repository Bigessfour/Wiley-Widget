#!/usr/bin/env python3
"""
Quick UI Inspection Script for WileyWidget Syncfusion WPF Interop
Provides debugging capabilities for Syncfusion control rendering and WPF UI issues
"""

import argparse
import subprocess

# Import debugpy for remote debugging
import debugpy


def inspect_syncfusion_controls():
    """Inspect Syncfusion controls in the running WPF application"""
    print("🔍 Inspecting Syncfusion WPF Controls...")

    # Debug breakpoint before subprocess call
    debugpy.breakpoint()

    try:
        # Launch WileyWidget with UI inspection mode
        cmd = ["dotnet", "run", "--project", "WileyWidget.csproj", "--", "--ui-inspect"]

        # Debug breakpoint before execution
        debugpy.breakpoint()

        process = subprocess.Popen(
            cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True
        )

        print(f"✅ UI Inspection started (PID: {process.pid})")
        print("   Inspecting Syncfusion controls: SfDataGrid, RibbonControlAdv, etc.")

        # Monitor for UI rendering issues
        try:
            stdout, stderr = process.communicate(timeout=10)
            if stdout:
                print(f"UI Output:\n{stdout}")
            if stderr:
                print(f"UI Errors:\n{stderr}")
                # Debug breakpoint on errors
                debugpy.breakpoint()
        except subprocess.TimeoutExpired:
            print("  ⏳ UI inspection running...")
            return process

    except Exception as e:
        print(f"  ❌ UI inspection failed: {e}")
        # Debug breakpoint on failure
        debugpy.breakpoint()
        return None


def check_wpf_rendering():
    """Check WPF rendering pipeline for Syncfusion controls"""
    print("\n🎨 Checking WPF Rendering Pipeline...")

    # Debug breakpoint before subprocess call
    debugpy.breakpoint()

    try:
        # Run WPF diagnostics
        cmd = [
            "dotnet",
            "run",
            "--project",
            "WileyWidget.csproj",
            "--",
            "--diagnostics",
        ]

        # Debug breakpoint before execution
        debugpy.breakpoint()

        result = subprocess.run(cmd, capture_output=True, text=True, timeout=15)

        if result.returncode == 0:
            print("✅ WPF rendering diagnostics passed")
            if result.stdout:
                print(f"Diagnostics output:\n{result.stdout}")
        else:
            print("❌ WPF rendering issues detected")
            if result.stderr:
                print(f"Rendering errors:\n{result.stderr}")
            # Debug breakpoint on rendering issues
            debugpy.breakpoint()

    except subprocess.TimeoutExpired:
        print("⚠️  WPF diagnostics timed out")
        debugpy.breakpoint()
    except Exception as e:
        print(f"❌ WPF diagnostics failed: {e}")
        debugpy.breakpoint()


def inspect_data_binding():
    """Inspect data binding for Syncfusion controls"""
    print("\n🔗 Inspecting Data Binding...")

    # Debug breakpoint before subprocess call
    debugpy.breakpoint()

    try:
        # Test data binding with debug output
        cmd = [
            "dotnet",
            "run",
            "--project",
            "WileyWidget.csproj",
            "--",
            "--binding-test",
        ]

        # Debug breakpoint before execution
        debugpy.breakpoint()

        result = subprocess.run(cmd, capture_output=True, text=True, timeout=10)

        if "binding successful" in result.stdout.lower():
            print("✅ Data binding working correctly")
        else:
            print("⚠️  Potential data binding issues")
            if result.stdout:
                print(f"Binding output:\n{result.stdout}")
            # Debug breakpoint on binding issues
            debugpy.breakpoint()

    except Exception as e:
        print(f"❌ Data binding inspection failed: {e}")
        debugpy.breakpoint()


def main():
    """Main UI inspection function"""
    parser = argparse.ArgumentParser(
        description="WileyWidget UI Inspection with debugpy"
    )
    parser.add_argument(
        "--verbose", "-v", action="store_true", help="Enable verbose output"
    )
    parser.add_argument(
        "--no-wait", action="store_true", help="Don't wait for debugger attachment"
    )

    args = parser.parse_args()

    print("🔧 WileyWidget UI Inspection Script")
    print("=" * 50)

    if args.verbose:
        print("Verbose mode enabled")

    # Setup debugpy if not in no-wait mode
    if not args.no_wait:
        print("🔍 Setting up debugpy for UI inspection...")
        debugpy.listen(5679)  # Different port for UI inspection
        print("⏳ Waiting for debugger to attach on port 5679...")
        debugpy.wait_for_client()
        print("✅ Debugger attached!")

    # Run inspections
    inspect_syncfusion_controls()
    check_wpf_rendering()
    inspect_data_binding()

    print("\n" + "=" * 50)
    print("✅ UI inspection complete!")
    print("\nDebug breakpoints set at:")
    print("- Before each subprocess call to WileyWidget.exe")
    print("- On UI rendering errors")
    print("- On data binding issues")
    print("- On general inspection failures")


if __name__ == "__main__":
    main()
