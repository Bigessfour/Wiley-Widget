#!/usr/bin/env python3
"""Compatibility wrapper for the WinForms layout conformance audit.

This preserves the historical `audit-layout-tokens.py` entrypoint used by
tasks, docs, and local workflow snippets while delegating to the renamed
`audit_winforms_layout_conformance.py` implementation.
"""

from __future__ import annotations

import runpy
import sys
from pathlib import Path


def normalize_args(argv: list[str]) -> list[str]:
    """Strip legacy arguments that the new implementation no longer needs."""

    normalized = [argv[0]]
    index = 1

    while index < len(argv):
        arg = argv[index]

        if arg == "--scope":
            index += 2
            continue

        normalized.append(arg)
        index += 1

    return normalized


if __name__ == "__main__":
    script_path = Path(__file__).with_name("audit_winforms_layout_conformance.py")
    sys.argv = normalize_args(sys.argv)
    runpy.run_path(str(script_path), run_name="__main__")
