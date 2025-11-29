#!/usr/bin/env python3
"""
Startup validator stub — lightweight checks used by the repository's workflow.
Supports: --focus <comma-list> where focus can include licenses,resources,prism,deprecated

This stub prints what it checked and exits 0. It can be extended for stronger validation later.
"""
import argparse
import sys
from pathlib import Path


def check_licenses():
    # simple heuristic: look for LICENSE or licenses/ folder
    files = list(Path(".").rglob("LICENSE*"))
    files += list(Path("licenses").glob("*")) if Path("licenses").exists() else []
    return files


def check_resources():
    # reuse simple resource scanning
    found = list(Path("resources").rglob("*")) if Path("resources").exists() else []
    found += list(Path("Styles").rglob("*")) if Path("Styles").exists() else []
    return found


def check_prism():
    # look for Prism in csproj or packages
    matches = list(Path(".").rglob("*.csproj"))
    prism_found = False
    for p in matches:
        txt = p.read_text(encoding="utf-8", errors="ignore")
        if "Prism" in txt or "DryIoc" in txt:
            prism_found = True
            break
    return prism_found


def check_deprecated():
    # scan for words marked deprecated in docs
    matches = []
    for p in Path(".").rglob("*.md"):
        text = p.read_text(encoding="utf-8", errors="ignore")
        if "DEPRECATED" in text or "deprecated" in text:
            matches.append(str(p))
    return matches


def main(argv):
    parser = argparse.ArgumentParser(description="Startup validator (lightweight stub)")
    parser.add_argument(
        "--focus",
        type=str,
        help="Comma-separated list of checks (licenses,resources,prism,deprecated)",
        default="licenses,resources",
    )
    parser.add_argument("-v", "--verbose", action="store_true")
    args = parser.parse_args(argv)

    focus = [f.strip().lower() for f in args.focus.split(",") if f.strip()]

    print("Startup validator - running checks:", ", ".join(focus))

    overall_ok = True

    if "licenses" in focus:
        lic = check_licenses()
        print("  licenses: found", len(lic))

    if "resources" in focus:
        res = check_resources()
        print("  resources: found", len(res))

    if "prism" in focus:
        prism_ok = check_prism()
        print("  prism present:", prism_ok)

    if "deprecated" in focus:
        depr = check_deprecated()
        print("  deprecated mentions:", len(depr))

    # This script intentionally does not fail the CI unless critical -- return success
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main(sys.argv[1:]))
    except Exception as e:
        print("ERROR: startup_validator failed:", e)
        sys.exit(2)
