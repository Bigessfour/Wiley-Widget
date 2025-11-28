#!/usr/bin/env python3
"""
Simple test to repeatedly write a temp file and atomically rename it into place
to reproduce intermittent EPERM/permission issues on Windows.

Usage: python tools/mcp_write_test.py --target <path> --cycles 1000
"""
import argparse
import os
import time
from pathlib import Path


def attempt_write(target: Path, cycles: int = 1000, delay: float = 0.01):
    success = 0
    failures = 0
    errors = {}
    target = target.resolve()
    tmp = target.parent / (target.name + '.tmp')

    for i in range(cycles):
        try:
            # write a temp file
            tmp.write_text(f"cycle:{i}\n{time.time()}\n")
            # try move (atomic)
            os.replace(str(tmp), str(target))
            success += 1
        except Exception as ex:
            failures += 1
            errors.setdefault(type(ex).__name__, 0)
            errors[type(ex).__name__] += 1
            # small backoff
            time.sleep(delay * 5)
        # small delay to allow other processes to interact
        time.sleep(delay)

    return success, failures, errors


def main(argv):
    p = argparse.ArgumentParser()
    p.add_argument('--target', type=str, default='tools/test-output.tmp', help='target file to repeatedly write/replace')
    p.add_argument('--cycles', type=int, default=1000)
    p.add_argument('--delay', type=float, default=0.001)
    args = p.parse_args(argv)

    target = Path(args.target)
    target.parent.mkdir(parents=True, exist_ok=True)

    print('Starting write/replace test: target=', target, 'cycles=', args.cycles)
    s, f, errors = attempt_write(target, cycles=args.cycles, delay=args.delay)
    print('done. success=', s, 'failures=', f)
    print('errors summary:', errors)
    return 0

if __name__ == '__main__':
    import sys
    try:
        sys.exit(main(sys.argv[1:]))
    except Exception as e:
        print('Test failed:', e)
        sys.exit(2)
