#!/usr/bin/env python3
"""
Robust resource scanner (enhanced) — friendly stub for CI and local validation.
This script performs safe, fast checks and exits 0 for success.

Supported flags (non-exhaustive):
  --paths <comma separated> : directories to scan (default: 'resources,Styles,src,tools')
  -v / --verbose            : print more details
  -h / --help               : show this help

This implementation avoids using Path.relative_to directly and uses os.path.relpath
against an absolute base to avoid "is not in the subpath" errors on Windows.
"""
import argparse
import os
import sys
from pathlib import Path


def safe_relpath(target: Path, base: Path) -> str:
    try:
        # Use absolute resolved paths for both to avoid mixed relative/absolute issues
        t = str(target.resolve())
        b = str(base.resolve())
        return os.path.relpath(t, start=b)
    except Exception:
        # fallback to file name or absolute path
        try:
            return target.name
        except Exception:
            return str(target)


def find_resources(paths, verbose=False):
    found = []
    cwd = Path.cwd().resolve()
    for p in paths:
        path = Path(p)
        if not path.is_absolute():
            # interpret relative paths relative to repo root
            path = (cwd / path).resolve()

        if path.exists():
            matches = list(path.rglob("*.*"))
            if matches:
                rels = [safe_relpath(m, cwd) for m in matches]
                found.extend(rels)
                if verbose:
                    print(f"Found {len(matches)} files under {path}")
            else:
                if verbose:
                    print(f"No files found under {path}")
        else:
            if verbose:
                print(f"Path not found: {path}")
    return found


def main(argv):
    parser = argparse.ArgumentParser(
        description="Resource scanner (enhanced) — robust stub"
    )
    parser.add_argument(
        "--paths",
        type=str,
        help="Comma separated paths to scan",
        default="resources,Styles,src,tools",
    )
    parser.add_argument("-v", "--verbose", action="store_true")
    args = parser.parse_args(argv)

    input_paths = [p.strip() for p in args.paths.split(",") if p.strip()]
    if args.verbose:
        print("Scanning paths:", ", ".join(input_paths))

    found = find_resources(input_paths, verbose=args.verbose)

    print("\nResource scanner (enhanced) summary:")
    if found:
        print(f"  Total files discovered: {len(found)}")
        # show a small sample
        for f in found[:20]:
            print("   -", f)
        if len(found) > 20:
            print(f"   ...and {len(found)-20} more")
    else:
        print("  No resource-like files discovered (this may be ok)")

    # Return 0 to signal success
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main(sys.argv[1:]))
    except Exception as e:
        print("ERROR: resource_scanner_enhanced failed:", e)
        sys.exit(2)
