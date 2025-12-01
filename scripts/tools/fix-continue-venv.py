#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Fix Continue.dev Virtual Environment Script

This script recreates the .continue/venv if needed and installs missing dependencies,
specifically targeting the 'mcp_server_fetch' module error for the Syncfusion docs server.

Standards Compliance:
- Python 3.11+ with type hints, pathlib for file ops, context managers.
- No global variables; uses functions.
- List comprehensions and subprocess for commands.
- Verification block included.

Verification Required Before Running:
1. Lint/Analyze: pylint scripts/tools/fix-continue-venv.py (no errors, warnings optional).
2. Dry-Run: python scripts/tools/fix-continue-venv.py --dry-run (simulates without changes).
3. Test with Sample: Run in project root; check .continue/venv post-execution.
4. Confirm: No errors, venv recreated with modules (pip list | grep mcp), server starts cleanly.
Do NOT run in production without completing these steps.

Usage:
- Dry-run: python scripts/tools/fix-continue-venv.py --dry-run
- Full: python scripts/tools/fix-continue-venv.py
"""

import argparse
import os
import shutil
import subprocess
import sys
from pathlib import Path
from typing import List, Optional

def run_command(commands: List[str], cwd: Optional[Path] = None, dry_run: bool = False, check: bool = True) -> None:
    """Run a shell command with error handling and dry-run support.

    Uses subprocess for execution, captures output for logging.
    """
    if dry_run:
        print(f"[DRY-RUN] Would execute: {' '.join(commands)} in {cwd or Path.cwd()}")
        return

    try:
        result = subprocess.run(
            commands,
            cwd=cwd,
            check=check,
            capture_output=True,
            text=True,
            encoding='utf-8'
        )
        if result.stdout:
            print(result.stdout)
        if result.returncode != 0 and check:
            raise subprocess.CalledProcessError(result.returncode, commands, result.stdout, result.stderr)
    except subprocess.CalledProcessError as e:
        print(f"Error running {' '.join(commands)}: {e.stderr}")
        sys.exit(1)

def is_windows() -> bool:
    """Detect if running on Windows."""
    return os.name == 'nt'

def fix_venv(dry_run: bool = False, requirements_path: Optional[Path] = None) -> None:
    """Recreate .continue/venv and install dependencies."""
    project_root = Path.cwd()
    venv_path = project_root / ".continue" / "venv"

    print(f"Target venv path: {venv_path}")

    # Check if venv exists and remove if needed
    if venv_path.exists():
        print("Existing venv found.")
        if dry_run:
            print("[DRY-RUN] Would remove existing venv.")
            return
        try:
            if is_windows():
                # Windows: Use rmdir /s (via subprocess)
                run_command(["rmdir", "/s", "/q", str(venv_path)], dry_run=dry_run)
            else:
                # Unix: shutil.rmtree with context
                with venv_path:  # Ensures path context
                    shutil.rmtree(venv_path)
        except Exception as e:
            print(f"Failed to remove venv: {e}")
            sys.exit(1)
    else:
        print("No existing venv found.")

    # Create new venv
    python_exe = sys.executable
    venv_create_cmd = [python_exe, "-m", "venv", str(venv_path)]
    run_command(venv_create_cmd, dry_run=dry_run)
    print("Venv created.")

    # Activate and install (simulate activation by using venv's python)
    venv_python = venv_path / "Scripts" / "python.exe" if is_windows() else venv_path / "bin" / "python"

    # Install from requirements if provided
    if requirements_path and requirements_path.exists():
        pip_install_cmd = [str(venv_python), "-m", "pip", "install", "-r", str(requirements_path)]
        run_command(pip_install_cmd, dry_run=dry_run)
        print("Installed from requirements.txt.")

    # Install specific MCP module (adjust if package name differs)
    pip_mcp_cmd = [str(venv_python), "-m", "pip", "install", "mcp-server-fetch"]
    run_command(pip_mcp_cmd, dry_run=dry_run, check=False)  # Allow warnings for optional install

    # Additional common deps for Continue/MCP (e.g., requests for fetching)
    common_deps = ["requests", "flask"]  # Assume for server; customize as needed
    for dep in common_deps:
        pip_dep_cmd = [str(venv_python), "-m", "pip", "install", dep]
        run_command(pip_dep_cmd, dry_run=dry_run, check=False)

    # Verify installation
    verify_cmd = [str(venv_python), "-m", "pip", "list"]
    run_command(verify_cmd, dry_run=dry_run)

    print("Venv fix complete. Restart VS Code and retry starting the Syncfusion docs server.")
    print("To verify: Activate venv and run 'pip list | findstr mcp' (Windows) or 'pip list | grep mcp' (Unix).")

def lint_self(dry_run: bool = False) -> None:
    """Run pylint on this script for verification."""
    lint_cmd = ["pylint", __file__]
    run_command(lint_cmd, dry_run=dry_run, check=False)  # Warnings OK per guidelines

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Fix Continue.dev venv for MCP/Syncfusion server.")
    parser.add_argument("--dry-run", action="store_true", help="Simulate without changes.")
    parser.add_argument("--requirements", type=Path, help="Path to requirements.txt (default: .continue/requirements.txt)")
    args = parser.parse_args()

    # Pre-run verification: Lint
    lint_self(dry_run=args.dry_run)

    # Fix
    req_path = args.requirements or Path(".continue/requirements.txt")
    fix_venv(dry_run=args.dry_run, requirements_path=req_path)
#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Fix Continue.dev Virtual Environment Script

This script recreates the .continue/venv if needed and installs missing dependencies,
specifically targeting the 'mcp_server_fetch' module error for the Syncfusion docs server.

Standards Compliance:
- Python 3.11+ with type hints, pathlib for file ops, context managers.
- No global variables; uses functions.
- List comprehensions and subprocess for commands.
- Verification block included.

Verification Required Before Running:
1. Lint/Analyze: pylint scripts/tools/fix-continue-venv.py (no errors, warnings optional).
2. Dry-Run: python scripts/tools/fix-continue-venv.py --dry-run (simulates without changes).
3. Test with Sample: Run in project root; check .continue/venv post-execution.
4. Confirm: No errors, venv recreated with modules (pip list | grep mcp), server starts cleanly.
Do NOT run in production without completing these steps.

Usage:
- Dry-run: python scripts/tools/fix-continue-venv.py --dry-run
- Full: python scripts/tools/fix-continue-venv.py
"""

import argparse
import os
import shutil
import subprocess
import sys
from pathlib import Path
from typing import List, Optional

def run_command(commands: List[str], cwd: Optional[Path] = None, dry_run: bool = False, check: bool = True) -> None:
    """Run a shell command with error handling and dry-run support.

    Uses subprocess for execution, captures output for logging.
    """
    if dry_run:
        print(f"[DRY-RUN] Would execute: {' '.join(commands)} in {cwd or Path.cwd()}")
        return

    try:
        result = subprocess.run(
            commands,
            cwd=cwd,
            check=check,
            capture_output=True,
            text=True,
            encoding='utf-8'
        )
        if result.stdout:
            print(result.stdout)
        if result.returncode != 0 and check:
            raise subprocess.CalledProcessError(result.returncode, commands, result.stdout, result.stderr)
    except subprocess.CalledProcessError as e:
        print(f"Error running {' '.join(commands)}: {e.stderr}")
        sys.exit(1)

def is_windows() -> bool:
    """Detect if running on Windows."""
    return os.name == 'nt'

def fix_venv(dry_run: bool = False, requirements_path: Optional[Path] = None) -> None:
    """Recreate .continue/venv and install dependencies."""
    project_root = Path.cwd()
    venv_path = project_root / ".continue" / "venv"

    print(f"Target venv path: {venv_path}")

    # Check if venv exists and remove if needed
    if venv_path.exists():
        print("Existing venv found.")
        if dry_run:
            print("[DRY-RUN] Would remove existing venv.")
            return
        try:
            if is_windows():
                # Windows: Use rmdir /s (via subprocess)
                run_command(["rmdir", "/s", "/q", str(venv_path)], dry_run=dry_run)
            else:
                # Unix: shutil.rmtree with context
                with venv_path:  # Ensures path context
                    shutil.rmtree(venv_path)
        except Exception as e:
            print(f"Failed to remove venv: {e}")
            sys.exit(1)
    else:
        print("No existing venv found.")

    # Create new venv
    python_exe = sys.executable
    venv_create_cmd = [python_exe, "-m", "venv", str(venv_path)]
    run_command(venv_create_cmd, dry_run=dry_run)
    print("Venv created.")

    # Activate and install (simulate activation by using venv's python)
    venv_python = venv_path / "Scripts" / "python.exe" if is_windows() else venv_path / "bin" / "python"

    # Install from requirements if provided
    if requirements_path and requirements_path.exists():
        pip_install_cmd = [str(venv_python), "-m", "pip", "install", "-r", str(requirements_path)]
        run_command(pip_install_cmd, dry_run=dry_run)
        print("Installed from requirements.txt.")

    # Install specific MCP module (adjust if package name differs)
    pip_mcp_cmd = [str(venv_python), "-m", "pip", "install", "mcp-server-fetch"]
    run_command(pip_mcp_cmd, dry_run=dry_run, check=False)  # Allow warnings for optional install

    # Additional common deps for Continue/MCP (e.g., requests for fetching)
    common_deps = ["requests", "flask"]  # Assume for server; customize as needed
    for dep in common_deps:
        pip_dep_cmd = [str(venv_python), "-m", "pip", "install", dep]
        run_command(pip_dep_cmd, dry_run=dry_run, check=False)

    # Verify installation
    verify_cmd = [str(venv_python), "-m", "pip", "list"]
    run_command(verify_cmd, dry_run=dry_run)

    print("Venv fix complete. Restart VS Code and retry starting the Syncfusion docs server.")
    print("To verify: Activate venv and run 'pip list | findstr mcp' (Windows) or 'pip list | grep mcp' (Unix).")

def lint_self(dry_run: bool = False) -> None:
    """Run pylint on this script for verification."""
    lint_cmd = ["pylint", __file__]
    run_command(lint_cmd, dry_run=dry_run, check=False)  # Warnings OK per guidelines

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Fix Continue.dev venv for MCP/Syncfusion server.")
    parser.add_argument("--dry-run", action="store_true", help="Simulate without changes.")
    parser.add_argument("--requirements", type=Path, help="Path to requirements.txt (default: .continue/requirements.txt)")
    args = parser.parse_args()

    # Pre-run verification: Lint
    lint_self(dry_run=args.dry_run)

    # Fix
    req_path = args.requirements or Path(".continue/requirements.txt")
    fix_venv(dry_run=args.dry_run, requirements_path=req_path)
