#!/usr/bin/env python3
"""
Quick E2E Test - Single command to validate everything
Usage: python scripts/quick_e2e.py
"""

import subprocess
import sys
from pathlib import Path

WORKSPACE = Path(__file__).parent.parent

def run(cmd, desc):
    """Run a command and report status."""
    print(f"\n[TEST] {desc}")
    result = subprocess.run(cmd, cwd=WORKSPACE, shell=True)
    return result.returncode == 0

def main():
    print("=" * 80)
    print("  QUICK E2E TEST - Full Data Pipeline Validation")
    print("=" * 80)

    tests = [
        ("dotnet build WileyWidget.sln --configuration Release", 
         "1. Building solution"),
        
        ("dotnet test tests/WileyWidget.Services.Tests/WileyWidget.Services.Tests.csproj --filter Repository --configuration Release --logger console;verbosity=minimal", 
         "2. Testing Repositories (DB layer)"),
        
        ("dotnet test tests/WileyWidget.WinForms.Tests/WileyWidget.WinForms.Tests.csproj --filter ViewModel --configuration Release --logger console;verbosity=minimal", 
         "3. Testing ViewModels (Service layer)"),
        
        ("dotnet test tests/WileyWidget.Services.Tests/WileyWidget.Services.Tests.csproj --filter Integration --configuration Release --logger console;verbosity=minimal", 
         "4. Testing Integration (DI layer)"),
        
        ("dotnet run --project tools/WileyWidgetMcpServer/WileyWidgetMcpServer.csproj --no-build -- BatchValidateForms null Office2019Colorful false json > nul 2>&1", 
         "5. Validating Theme Compliance (all forms)"),
    ]

    passed = 0
    for cmd, desc in tests:
        if run(cmd, desc):
            print(f"  [PASS] {desc}")
            passed += 1
        else:
            print(f"  [FAIL] {desc}")
            break

    print("\n" + "=" * 80)
    print(f"  Results: {passed}/{len(tests)} tests passed")
    print("=" * 80)

    return 0 if passed == len(tests) else 1

if __name__ == "__main__":
    sys.exit(main())
