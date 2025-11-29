#!/usr/bin/env python3
"""
Atomic write stress-test (non-destructive) — uses tools/atomic_write.atomic_write for robust writing.

Usage:
  python tools/mcp_write_test_atomic.py --target tools/writer-target-atomic.tmp --cycles 2000
"""
import argparse
import time
from pathlib import Path

from atomic_write import atomic_write


def attempt_atomic_write(
    target: Path, cycles: int = 1000, delay: float = 0.001, retries: int = 6
):
    success = 0
    failures = 0
    errors = {}
    target = target.resolve()

    for i in range(cycles):
        try:
            content = f"cycle:{i}\n{time.time()}\n"
            atomic_write(target, content, retries=retries, delay=0.01)
            success += 1
        except Exception as ex:
            failures += 1
            errors.setdefault(type(ex).__name__, 0)
            errors[type(ex).__name__] += 1
            # small backoff
            time.sleep(delay * 10)
        time.sleep(delay)

    return success, failures, errors


def main(argv):
    p = argparse.ArgumentParser()
    p.add_argument("--target", type=str, default="tools/writer-target-atomic.tmp")
    p.add_argument("--cycles", type=int, default=1000)
    p.add_argument("--delay", type=float, default=0.001)
    p.add_argument("--retries", type=int, default=6)
    args = p.parse_args(argv)

    target = Path(args.target)
    target.parent.mkdir(parents=True, exist_ok=True)

    print("Starting atomic write test: target=", target, "cycles=", args.cycles)
    s, f, errors = attempt_atomic_write(
        target, cycles=args.cycles, delay=args.delay, retries=args.retries
    )
    print("done. success=", s, "failures=", f)
    print("errors summary:", errors)
    return 0


if __name__ == "__main__":
    import sys

    try:
        sys.exit(main(sys.argv[1:]))
    except Exception as e:
        print("Test failed:", e)
        sys.exit(2)
