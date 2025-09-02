#!/usr/bin/env python3
"""
Quick analysis of Wiley Widget project using Python tools
"""

import json
import os
from pathlib import Path


def analyze_project():
    print("Wiley Widget Project Analysis")
    print("=" * 40)

    # Check CI/CD results
    if os.path.exists("cicd-results.json"):
        with open("cicd-results.json", "r") as f:
            data = json.load(f)
        print("CI/CD Status:")
        print(f"   Success Rate: {data.get('success_rate', 'N/A')}")
        print(f"   Total Runs: {len(data.get('runs', []))}")
        print(f"   Last Updated: {data.get('timestamp', 'N/A')}")
    else:
        print("CI/CD Status: No results file found")

    # Count project files
    project_root = Path(".")
    cs_files = list(project_root.rglob("*.cs"))
    xaml_files = list(project_root.rglob("*.xaml"))
    py_files = list(project_root.rglob("*.py"))

    print("\nProject Files:")
    print(f"   C# Files: {len(cs_files)}")
    print(f"   XAML Files: {len(xaml_files)}")
    print(f"   Python Files: {len(py_files)}")

    # Check Python environment
    print("\nPython Environment:")
    print(f"   Python Scripts: {len(list(project_root.glob('scripts/*.ps1')))}")
    print(f"   Requirements: {len(list(project_root.glob('requirements*.txt')))}")

    print("\nPython tools ready to help with:")
    print("   - CI/CD workflow validation")
    print("   - Git repository analysis")
    print("   - JSON/YAML processing")
    print("   - Code quality checks")
    print("   - Build automation")


if __name__ == "__main__":
    analyze_project()
