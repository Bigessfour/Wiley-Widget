#!/usr/bin/env python3
"""
Python Shell Integration Setup for Wiley Widget
Ensures Python is properly integrated into the development environment
"""

import os
import platform
import subprocess
import sys
from pathlib import Path


def check_python_setup():
    """Check Python installation and setup"""
    print("=== Python Setup Check ===")

    print(f"Python version: {sys.version}")
    print(f"Python executable: {sys.executable}")
    print(f"Platform: {platform.platform()}")

    # Check if python command is available
    try:
        result = subprocess.run(
            ["python", "--version"], capture_output=True, text=True, check=True
        )
        print(f"✅ 'python' command available: {result.stdout.strip()}")
    except (subprocess.CalledProcessError, FileNotFoundError):
        print("❌ 'python' command not found in PATH")

    # Check pip
    try:
        result = subprocess.run(
            ["pip", "--version"], capture_output=True, text=True, check=True
        )
        print(f"✅ pip available: {result.stdout.strip()}")
    except (subprocess.CalledProcessError, FileNotFoundError):
        print("❌ pip not found")

    # Check key packages
    required_packages = ["debugpy", "azure-identity"]
    missing_packages = []
    for package in required_packages:
        try:
            __import__(package.replace("-", "_"))
            print(f"✅ {package} available")
        except ImportError:
            print(f"⚠️  {package} not installed")
            missing_packages.append(package)

    return missing_packages


def install_missing_packages(missing_packages):
    """Install missing required packages"""
    if not missing_packages:
        return

    print("\n=== Installing Missing Packages ===")
    for package in missing_packages:
        try:
            print(f"Installing {package}...")
            subprocess.run(
                [sys.executable, "-m", "pip", "install", package], check=True
            )
            print(f"✅ {package} installed successfully")
        except subprocess.CalledProcessError as e:
            print(f"❌ Failed to install {package}: {e}")


def setup_python_path():
    """Set up Python path for the project"""
    print("\n=== Python Path Setup ===")

    project_root = Path(__file__).parent.parent
    scripts_dir = project_root / "scripts"

    # Add to Python path
    if str(scripts_dir) not in sys.path:
        sys.path.insert(0, str(scripts_dir))
        print(f"✅ Added {scripts_dir} to Python path")

    # Set PYTHONPATH environment variable
    current_pythonpath = os.environ.get("PYTHONPATH", "")
    if str(project_root) not in current_pythonpath:
        new_pythonpath = f"{project_root}{os.pathsep}{current_pythonpath}".rstrip(
            os.pathsep
        )
        os.environ["PYTHONPATH"] = new_pythonpath
        print(f"✅ Set PYTHONPATH to: {new_pythonpath}")


def create_shell_aliases():
    """Create shell aliases for common Python commands"""
    print("\n=== Shell Integration ===")

    if platform.system() == "Windows":
        # Create PowerShell profile entry
        profile_path = (
            Path.home()
            / "Documents"
            / "PowerShell"
            / "Microsoft.PowerShell_profile.ps1"
        )
        profile_path.parent.mkdir(parents=True, exist_ok=True)

        alias_content = """
# Wiley Widget Python Aliases
function dev-start { python $PSScriptRoot/../scripts/dev-start.py @args }
function cleanup-dotnet { python $PSScriptRoot/../scripts/cleanup-dotnet.py @args }
function load-env { python $PSScriptRoot/../scripts/load-env.py @args }
function azure-setup { python $PSScriptRoot/../scripts/azure-setup.py @args }
"""

        try:
            with open(profile_path, "a") as f:
                f.write(alias_content)
            print(f"✅ Added PowerShell aliases to {profile_path}")
        except Exception as e:
            print(f"⚠️  Could not update PowerShell profile: {e}")

    else:
        print("ℹ️  Shell aliases setup for Linux/macOS not implemented yet")


def test_debug_setup():
    """Test the debug setup"""
    print("\n=== Debug Setup Test ===")

    # Test importing debugpy
    try:
        import debugpy

        print("✅ debugpy available for debugging")
        print(f"   Version: {debugpy.__version__}")
    except ImportError:
        print("❌ debugpy not available")
        print("   Install with: pip install debugpy")

    # Test our debug script
    debug_script = Path(__file__).parent / "dev-start-debug.py"
    if debug_script.exists():
        print(f"✅ Debug script found: {debug_script}")
    else:
        print(f"❌ Debug script not found: {debug_script}")


def main():
    """Main setup function"""
    print("🔧 Wiley Widget Python Shell Integration Setup")
    print("=" * 50)

    missing = check_python_setup()
    install_missing_packages(missing)
    setup_python_path()
    create_shell_aliases()
    test_debug_setup()

    print("\n" + "=" * 50)
    print("✅ Python shell integration setup complete!")
    print("\nNext steps:")
    print("1. Restart your terminal/PowerShell")
    print("2. Use 'dev-start' instead of 'python scripts/dev-start.py'")
    print("3. Use VS Code debug configurations for Python debugging")
    print("4. Run 'python scripts/setup-python.py' to re-run this setup")


if __name__ == "__main__":
    main()
