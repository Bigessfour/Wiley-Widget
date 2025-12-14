#!/usr/bin/env python3
"""
Test Trunk merge queue handlers in xai_tool_executor.

This script directly tests the Trunk merge queue tool handlers.
"""

import sys
from pathlib import Path

# Add scripts/tools to path
scripts_tools = Path(__file__).parent.parent / "scripts" / "tools"
sys.path.insert(0, str(scripts_tools))

try:
    from xai_tool_executor import IdeToolHandlers  # type: ignore
except ImportError:
    from xai_tool_executor import XaiToolHandlers as IdeToolHandlers  # type: ignore


def test_trunk_handlers():
    """Test Trunk merge queue handlers."""
    print("=== Testing Trunk Merge Queue Handlers ===\n")

    handlers = IdeToolHandlers()

    # Test 1: Check that handlers are registered
    print("[Test 1] Checking handler registration...")
    trunk_tools = [
        "trunk_merge_status",
        "trunk_merge_submit",
        "trunk_merge_cancel",
        "trunk_merge_pause",
        "trunk_merge_resume",
    ]

    for tool in trunk_tools:
        handler = handlers.get_handler(tool)
        if handler:
            print(f"  ✓ Handler found: {tool}")
        else:
            print(f"  ✗ Handler missing: {tool}")
            return False

    # Test 2: Test status query (no PR number)
    print("\n[Test 2] Testing trunk_merge_status (overall queue)...")
    try:
        result = handlers.get_handler("trunk_merge_status")({})
        if result and ("Error" not in result or "Trunk Merge Status" in result):
            print("  ✓ Status query executed")
            print(f"  Output preview: {result[:200]}...")
        else:
            print(f"  ⚠ Status query returned: {result}")
    except Exception as e:
        print(f"  ✗ Error: {e}")

    # Test 3: Test status query with PR number
    print("\n[Test 3] Testing trunk_merge_status (PR #9)...")
    try:
        result = handlers.get_handler("trunk_merge_status")({"pr_number": 9})
        if result and "Error" not in result:
            print("  ✓ PR status query executed")
            print(f"  Output preview: {result[:200]}...")
        else:
            print(f"  ⚠ PR status query returned: {result}")
    except Exception as e:
        print(f"  ✗ Error: {e}")

    # Test 4: Test submit (dry run - will fail if not authenticated)
    print("\n[Test 4] Testing trunk_merge_submit (will fail if not a real PR)...")
    try:
        result = handlers.get_handler("trunk_merge_submit")({"pr_number": 99999})
        print(f"  Result: {result[:200]}...")
    except Exception as e:
        print(f"  Error (expected if PR doesn't exist): {e}")

    # Test 5: Test cancel (dry run - will fail if not in queue)
    print("\n[Test 5] Testing trunk_merge_cancel (will fail if not in queue)...")
    try:
        result = handlers.get_handler("trunk_merge_cancel")({"pr_number": 99999})
        print(f"  Result: {result[:200]}...")
    except Exception as e:
        print(f"  Error (expected if PR not in queue): {e}")

    print("\n=== Test Complete ===")
    return True


if __name__ == "__main__":
    success = test_trunk_handlers()
    sys.exit(0 if success else 1)
