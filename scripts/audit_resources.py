#!/usr/bin/env python3
"""Audit WPF XAML resource keys vs StaticResource references with advanced diagnostics.

Outputs JSON with:
    - defined_keys
    - referenced_keys
    - missing_keys
    - duplicate_keys
    - case_collisions
    - unused_keys
    - merged_dictionary_hints (heuristic order & sources)
    - potential_pruned_keys (keys that WOULD be removed by override guard simulation)
    - protected_override_conflicts (keys both in theme + custom but preserved via allowlist)

Enhancements (Steps 8 & 9 integration):
    * Simulates the PreventCustomOverridesOfThemeKeys logic to show which keys are at risk.
    * Provides allowlist/denylist controls via environment variables:
             AWR_ALLOWLIST (comma-separated) – keys ALWAYS preserved even if collision.
             AWR_DENYLIST  (comma-separated) – keys ALWAYS considered for pruning if collision.
    * Detects encoded path variants for the primary custom dictionary ("Wiley%20Widget/Resources/SyncfusionResources.xaml").

Usage:
    python scripts/audit_resources.py > resource_audit.json

Exit Codes:
    0 = OK
    2 = Missing or duplicate keys
    3 = At-risk prunable keys detected (no hard failure yet) if AWR_STRICT_PRUNE=1
"""
from __future__ import annotations

import json
import os
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
XAML_GLOB = "**/*.xaml"
CS_GLOB = "**/*.cs"

# Regex patterns
KEY_DEF = re.compile(r'x:Key\s*=\s*"([^"]+)"')
STATIC_RES = re.compile(
    r'\{StaticResource\s+([^}\s]+)\}|StaticResource\s+Key\s*=\s*"([^"]+)"'
)
CODE_LOOK = re.compile(
    r'FindResource\(\s*"([^"]+)"\s*\)|TryFindResource\(\s*"([^"]+)"\s*\)'
)

# Heuristic: identify merged dictionary declarations to approximate ordering
MERGED_DICT_LINE = re.compile(r'<ResourceDictionary\s+Source=\s*"([^"]+)"')

defined: dict[str, list[str]] = {}
refs: dict[str, list[str]] = {}
merged_sources: list[dict[str, str]] = []  # order encountered (file,line,source)


# Helper to record occurrences
def add(d: dict[str, list[str]], key: str, loc: str):
    d.setdefault(key, []).append(loc)


# Scan XAML definitions & references
for xaml in ROOT.glob(XAML_GLOB):
    try:
        text = xaml.read_text(encoding="utf-8", errors="ignore").splitlines()
    except Exception as ex:
        print(f"WARN: Failed reading {xaml}: {ex}", file=sys.stderr)
        continue
    for i, line in enumerate(text, 1):
        # capture merged dictionary source usage (heuristic; not 100%)
        for m in MERGED_DICT_LINE.finditer(line):
            merged_sources.append(
                {"file": str(xaml), "line": str(i), "source": m.group(1)}
            )
        for m in KEY_DEF.finditer(line):
            add(defined, m.group(1), f"{xaml}:{i}")
        for m in STATIC_RES.finditer(line):
            k = m.group(1) or m.group(2)
            if k:
                add(refs, k, f"{xaml}:{i}")

# Scan code for runtime lookups
for cs in ROOT.glob(CS_GLOB):
    try:
        text = cs.read_text(encoding="utf-8", errors="ignore").splitlines()
    except Exception as ex:
        print(f"WARN: Failed reading {cs}: {ex}", file=sys.stderr)
        continue
    for i, line in enumerate(text, 1):
        for m in CODE_LOOK.finditer(line):
            k = m.group(1) or m.group(2)
            if k:
                add(refs, k, f"{cs}:{i}")

all_refs = set(refs.keys())
missing = sorted(k for k in all_refs if k not in defined)
duplicates = {k: v for k, v in defined.items() if len(v) > 1}

# Case-insensitive collisions
ci_map: dict[str, set[str]] = {}
for k in defined:
    ci_map.setdefault(k.lower(), set()).add(k)
case_collisions = {
    low: sorted(list(vars)) for low, vars in ci_map.items() if len(vars) > 1
}

unused = sorted(k for k in defined if k not in all_refs)

# --- Theme override simulation (mirrors PreventCustomOverridesOfThemeKeys intent) ---
ALLOWLIST = {
    k.strip() for k in os.environ.get("AWR_ALLOWLIST", "").split(",") if k.strip()
}
DENYLIST = {
    k.strip() for k in os.environ.get("AWR_DENYLIST", "").split(",") if k.strip()
}
STRICT_PRUNE = os.environ.get("AWR_STRICT_PRUNE", "0") == "1"

# Heuristic classification of sources: treat any source containing FluentLight.xaml / FluentDark.xaml as theme
theme_keys_ci = set()
custom_keys_ci = set()
custom_physical_keys = set()

custom_source_markers = [
    "Wiley%20Widget/Resources/SyncfusionResources.xaml".lower(),
    "Wiley Widget/Resources/SyncfusionResources.xaml".lower(),
]

for entry in merged_sources:
    src_low = entry["source"].lower()
    try:
        # Collect keys only for broad classification; actual key sets come from definitions since we don't parse remote theme files
        if "fluentlight.xaml" in src_low or "fluentdark.xaml" in src_low:
            # We cannot open theme assembly XAML; mark placeholder to show collision detection will rely on name presence only
            pass
        elif any(marker in src_low for marker in custom_source_markers):
            # We'll tag all defined keys as potential custom later
            pass
    except Exception:
        pass

# Since we cannot introspect Syncfusion theme dictionaries directly, approximate theme vs custom collisions:
# If a key name follows a pattern often defined by our custom dictionary (present in defined) and its name suggests general purpose,
# we conservatively assume theme may also define similarly named keys if they are generic (PrimaryBrush, SecondaryBrush, etc.).
GENERIC_THEME_LIKE = {
    "PrimaryBrush",
    "SecondaryBrush",
    "SuccessBrush",
    "WarningBrush",
    "ErrorBrush",
    "InfoBrush",
    "PrimaryAccentBrush",
}

for k in defined.keys():
    if k in GENERIC_THEME_LIKE:
        theme_keys_ci.add(k.lower())
    # custom dictionary contains everything we define; we'll treat ALL defined keys as custom candidates
    custom_keys_ci.add(k.lower())
    custom_physical_keys.add(k)

potential_pruned = []
protected_conflicts = []
for k in custom_physical_keys:
    lower = k.lower()
    if lower in theme_keys_ci and k not in ALLOWLIST:
        if k in DENYLIST or k not in ALLOWLIST:
            potential_pruned.append(k)
    if lower in theme_keys_ci and k in ALLOWLIST:
        protected_conflicts.append(k)

potential_pruned.sort()
protected_conflicts.sort()

report = {
    "defined_count": len(defined),
    "reference_count": len(all_refs),
    "defined_keys": defined,
    "referenced_keys": refs,
    "missing_keys": missing,
    "duplicate_keys": duplicates,
    "case_collisions": case_collisions,
    "unused_keys": unused,
    "merged_dictionary_hints": merged_sources,
    "potential_pruned_keys": potential_pruned,
    "protected_override_conflicts": protected_conflicts,
}

json.dump(report, sys.stdout, indent=2)
sys.stdout.write("\n")

# CI enforcement: fail (exit code 2) if duplicates or missing keys detected
exit_code = 0
if duplicates or missing:
    # Provide concise stderr summary for pipeline logs
    if duplicates:
        print(
            f"ERROR: Duplicate resource keys detected: {', '.join(sorted(duplicates.keys())[:10])}"
            + ("..." if len(duplicates) > 10 else ""),
            file=sys.stderr,
        )
    if missing:
        print(
            f"ERROR: Missing resource definitions for keys: {', '.join(missing[:10])}"
            + ("..." if len(missing) > 10 else ""),
            file=sys.stderr,
        )
    exit_code = 2

if STRICT_PRUNE and potential_pruned and exit_code == 0:
    print(
        f"WARN: {len(potential_pruned)} resource keys would be pruned by override guard (STRICT_PRUNE on)",
        file=sys.stderr,
    )
    exit_code = 3

sys.exit(exit_code)
