#!/usr/bin/env python3
"""
Simple test script to verify repository functionality with seeded data
"""
import os
import sys

# Add the project root to Python path
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

try:
    # Try to import and test basic functionality
    print("Testing repository data access...")

    # Test database connection by running a simple query
    import subprocess

    result = subprocess.run(
        [
            "sqlcmd",
            "-S",
            ".\\SQLEXPRESS",
            "-d",
            "WileyWidgetDev",
            "-Q",
            "SELECT COUNT(*) as MunicipalAccounts FROM MunicipalAccounts; SELECT COUNT(*) as BudgetEntries FROM BudgetEntries;",
        ],
        capture_output=True,
        text=True,
        cwd=os.path.dirname(os.path.abspath(__file__)),
    )

    if result.returncode == 0:
        print("Database connection successful!")
        print("Query results:")
        print(result.stdout)
    else:
        print("Database connection failed!")
        print("Error:", result.stderr)

except Exception as e:
    print(f"Error during testing: {e}")
    import traceback

    traceback.print_exc()
