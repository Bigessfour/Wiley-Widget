#!/usr/bin/env python3
"""
Quick runtime error checker for Syncfusion themes
"""

import json
import shutil
import subprocess  # nosec B404 - Using subprocess with validated inputs only
from pathlib import Path


def check_recent_crashes():
    """Check for recent application crashes in Event Viewer"""
    print("🔍 Checking recent crashes...")

    try:
        # Secure PowerShell command to check recent errors - using fixed command string
        ps_args = [
            "powershell.exe",
            "-Command",
            "Get-WinEvent -FilterHashtable @{LogName='Application'; StartTime=(Get-Date).AddHours(-2); Level=1,2} | Where-Object {$_.Message -match 'WileyWidget|Syncfusion'} | Select-Object -First 5 TimeCreated, LevelDisplayName, Message | ConvertTo-Json -Depth 2",
        ]

        result = subprocess.run(
            ps_args, capture_output=True, text=True, timeout=30, check=False
        )  # nosec B603 - Using fixed command with no user input

        if result.returncode == 0 and result.stdout.strip():
            try:
                events = json.loads(result.stdout)
                if isinstance(events, dict):
                    events = [events]

                print(f"Found {len(events)} recent crashes:")
                for event in events:
                    print(f"  {event['TimeCreated']}: {event['LevelDisplayName']}")
                    print(f"    {event['Message'][:100]}...")
                    print()

            except json.JSONDecodeError:
                print("No crashes found in JSON output")
        else:
            print("✅ No recent crashes found")

    except Exception as e:
        print(f"Error checking crashes: {e}")


def check_debug_log():
    """Check debug.log for recent errors"""
    print("📋 Checking debug.log...")

    debug_log = Path("debug.log")
    if debug_log.exists():
        try:
            with open(debug_log, "r", encoding="utf-8") as f:
                lines = f.readlines()

            # Get last 20 lines
            recent_lines = lines[-20:] if len(lines) > 20 else lines

            error_lines = []
            for line in recent_lines:
                if any(
                    keyword in line.lower()
                    for keyword in ["error", "exception", "crash", "syncfusion"]
                ):
                    error_lines.append(line.strip())

            if error_lines:
                print(f"Found {len(error_lines)} recent errors:")
                for line in error_lines:
                    print(f"  {line}")
            else:
                print("✅ No recent errors in debug.log")

        except Exception as e:
            print(f"Error reading debug.log: {e}")
    else:
        print("No debug.log found")


def check_build_output():
    """Check if app can build and run"""
    print("🔨 Testing build...")

    try:
        # Find dotnet executable securely
        dotnet_exe = shutil.which("dotnet")
        if not dotnet_exe:
            print("❌ dotnet.exe not found in PATH")
            return False

        # Secure build test with fixed arguments and validated executable
        build_args = [
            dotnet_exe,
            "build",
            "WileyWidget.csproj",
            "--verbosity",
            "quiet",
        ]

        # Secure subprocess call with validated executable and fixed arguments
        result = subprocess.run(  # nosec B603 - Using validated executable with fixed arguments
            build_args,
            capture_output=True,
            text=True,
            timeout=60,
            check=False,
            cwd=Path.cwd(),  # Explicit working directory
            shell=False,  # Explicitly disable shell for security
        )

        if result.returncode == 0:
            print("✅ Build successful")
            return True
        else:
            print("❌ Build failed:")
            print(result.stderr[:500])
            return False

    except Exception as e:
        print(f"Build test failed: {e}")
        return False


def main():
    print("🎨 Quick Syncfusion Theme Error Check")
    print("=" * 40)

    check_recent_crashes()
    print()
    check_debug_log()
    print()

    if check_build_output():
        print("\n🚀 Ready to run full debugging with:")
        print("   python scripts/debug_syncfusion_themes.py")
    else:
        print("\n⚠️  Fix build errors first before running theme debug")


if __name__ == "__main__":
    main()
