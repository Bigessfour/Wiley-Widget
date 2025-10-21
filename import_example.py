#!/usr/bin/env python3
"""
Example script to import WileyWidget .NET types using pythonnet.
"""

from pathlib import Path

# Path to the assemblies directory
repo_root = Path(__file__).resolve().parent
assemblies_dir = repo_root / "tools" / "python" / "clr_tests" / "assemblies"

try:
    import clr

    # Add reference using full path to the dll
    clr.AddReference(str(assemblies_dir / "WileyWidget.dll"))
    # Import successful if we reach this point
    print("Successfully imported WileyWidget.dll")
except ImportError as e:
    print(f"Failed to import: {e}")
    print("Make sure pythonnet is installed: pip install pythonnet")
