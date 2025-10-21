#!/usr/bin/env python3
"""
Simple debugpy test for Wiley Widget startup debugging
"""

import time

import debugpy


def test_debugpy_setup():
    """Test debugpy basic functionality"""
    print("🔍 Testing debugpy setup...")

    # Setup debugpy on a test port
    try:
        debugpy.listen(5679)  # Use different port for testing
        print("✅ debugpy listening on port 5679")
        print("   You can attach VS Code debugger to localhost:5679")

        # Set a breakpoint
        print("⏸️  Setting test breakpoint...")
        debugpy.breakpoint()

        print("✅ Breakpoint test complete")

    except Exception as e:
        print(f"❌ debugpy test failed: {e}")


def test_startup_phases():
    """Test the startup phase monitoring"""
    print("\n=== Testing Startup Phase Monitoring ===")

    phases = []

    # Phase 1: Test phase
    print("Phase 1: Initialize...")
    start_time = time.time()
    time.sleep(0.1)  # Simulate work
    phases.append(("Initialize", time.time() - start_time))

    # Set breakpoint between phases
    debugpy.breakpoint()

    # Phase 2: Another test phase
    print("Phase 2: Process...")
    start_time = time.time()
    time.sleep(0.1)  # Simulate work
    phases.append(("Process", time.time() - start_time))

    # Final breakpoint
    debugpy.breakpoint()

    # Report timing
    print("\n=== Timing Report ===")
    total_time = sum(duration for _, duration in phases)
    for phase, duration in phases:
        print(f"  {phase}: {duration:.3f}s")
    print(f"  Total: {total_time:.3f}s")


def main():
    """Main test function"""
    print("🚀 debugpy Test for Wiley Widget Startup")
    print("=" * 45)

    test_debugpy_setup()
    test_startup_phases()

    print("\n✅ debugpy test complete!")
    print("   You can now use the full dev-start-debugpy.py script")


if __name__ == "__main__":
    main()
