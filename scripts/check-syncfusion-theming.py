#!/usr/bin/env python3
"""
Syncfusion Theming Compliance Checker
Scans for forbidden manual color assignments in WinForms C# code.
Blocks BackColor/ForeColor/Color.FromArgb except for semantic status colors (Red/Green/Orange).
"""
import sys
import re
from pathlib import Path

FORBIDDEN_PATTERNS = [
    r"\.BackColor\s*=\s*Color\.(?!Red|Green|Orange)",
    r"\.ForeColor\s*=\s*Color\.(?!Red|Green|Orange)",
    r"Color\.FromArgb",
    r"new\s+Syncfusion\.Drawing\.BrushInfo\(Color\.FromArgb",
]

ALLOWED_SEMANTIC = {"Red", "Green", "Orange"}


def scan_file(path):
    with open(path, encoding="utf-8", errors="ignore") as f:
        lines = f.readlines()
    violations = []
    for i, line in enumerate(lines, 1):
        for pat in FORBIDDEN_PATTERNS:
            if re.search(pat, line):
                # Allow semantic status colors
                if any(f"Color.{color}" in line for color in ALLOWED_SEMANTIC):
                    continue
                violations.append((i, line.strip()))
    return violations


def main():
    root = Path("src/WileyWidget.WinForms")
    cs_files = list(root.rglob("*.cs"))
    total = 0
    failed = 0
    for file in cs_files:
        violations = scan_file(file)
        if violations:
            print(f"::error file={file}::Syncfusion theming violation(s):")
            for lineno, code in violations:
                print(f"  L{lineno}: {code}")
            failed += 1
        total += 1
    if failed:
        print(f"::error ::{failed} file(s) have forbidden manual color assignments.")
        sys.exit(1)
    print(f"Syncfusion theming check passed: {total} files scanned, 0 violations.")

if __name__ == "__main__":
    main()
