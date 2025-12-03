#!/usr/bin/env python3.14
# wiley-null-conditional-refactor.py
# Run with: python3.14 wiley-null-conditional-refactor.py
# Dry-run first — it will only print. Remove --dry-run to commit.

from __future__ import annotations

import re
import sys
from argparse import ArgumentParser
from pathlib import Path

# C# 14 null-conditional assignment patterns we want to murder
PATTERNS = [
    # if (foo != null) foo.Bar = baz;  →  foo?.Bar = baz;
    (
        r"if\s*\(\s*([a-zA-Z_][\w.]*(?:\?\.\w+)*)\s*!=\s*null\s*\)\s*"
        r"([a-zA-Z_][\w.]*)\s*=\s*(.+?);",
        r"\1?\2 = \3;",
    ),
    # if (foo?.Bar != null) foo.Bar.Baz = qux; → foo?.Bar?.Baz = qux;
    (
        r"if\s*\(\s*([a-zA-Z_][\w?.]*(?:\?\.\w+)*)\s*!=\s*null\s*\)\s*"
        r"([a-zA-Z_][\w.]*)\s*=\s*(.+?);",
        r"\1?\2 = \3;",
    ),
    # if (obj != null) { obj.Prop = value; } → obj?.Prop = value;
    (
        r"if\s*\(\s*([a-zA-Z_][\w.]*)\s*!=\s*null\s*\)\s*\{\s*"
        r"([a-zA-Z_][\w.]*)\s*=\s*(.+?);\s*\}",
        r"\1?\2 = \3;",
    ),
]


def convert_line(line: str) -> tuple[str, int]:
    changes = 0
    for pattern, repl in PATTERNS:
        new_line, count = re.subn(pattern, repl, line, flags=re.DOTALL)
        if count:
            line = new_line
            changes += count
    return line, changes


def main() -> None:
    parser = ArgumentParser(
        description="Wiley Widget → C# 14 null-conditional massacre"
    )
    parser.add_argument(
        "--dry-run", action="store_true", help="Only show what would change"
    )
    parser.add_argument("--write", action="store_true", help="Actually overwrite files")
    args = parser.parse_args()

    if not args.dry_run and not args.write:
        print("Nothing to do. Use --dry-run or --write")
        sys.exit(0)

    root = Path(".")
    cs_files = list(root.rglob("*.cs"))
    print(f"Found {len(cs_files)} .cs files\n")

    total_changes = 0

    for path in cs_files:
        try:
            text = path.read_text(encoding="utf-8")
        except UnicodeDecodeError:
            continue  # skip weird binaries

        lines = text.splitlines(keepends=True)
        new_lines = []
        file_changes = 0

        for ln, line in enumerate(lines, 1):
            new_line, changes = convert_line(line)
            file_changes += changes
            if changes:
                print(f"{path}:{ln}  →  {changes} change(s)")
                print(f"   -  {line.strip()}")
                print(f"   +  {new_line.strip()}\n")
            new_lines.append(new_line)

        total_changes += file_changes

        if file_changes and args.write:
            path.write_text("".join(new_lines), encoding="utf-8")

    print(f"\nFinished. Total null-checks obliterated: {total_changes}")

    if total_changes <= 5:
        print(
            "Bet settled — you owe me a coffee. Less than or equal to 5 linting errors incoming."
        )
    else:
        print(
            f"Over 5 changes ({total_changes}). You win the bet — but your codebase wins more."
        )


if __name__ == "__main__":
    try:
        main()
    except* SystemExit:
        raise
    except* Exception as eg:
        print(f"Script exploded: {eg.exceptions}")
        sys.exit(1)
