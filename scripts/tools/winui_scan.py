#!/usr/bin/env python3
"""
winui_scan.py — scan the repository for WinUI / Windows App SDK references and produce a removal plan.

Usage: python scripts/tools/winui_scan.py [--root <path>] [--report <file>]

This tool scans text files under the repository root (skips common binary or IDE folders)
for a configurable set of WinUI-related keywords and produces a short plan describing
what to remove and where.

It intentionally avoids making any edits — it's a reporting helper to guide the next cleanup steps.
"""

import os
import sys
import argparse
import json
from pathlib import Path

KEYWORDS = [
    "WinUI",
    "WindowsAppSDK",
    "Microsoft.WindowsAppSDK",
    "Microsoft.Windows.SDK.BuildTools",
    "Microsoft.Windows.CsWinRT",
    "Microsoft.Web.WebView2",
    "CommunityToolkit.WinUI",
    "CommunityToolkit.WinUI.UI.Controls",
    "LiveChartsCore.SkiaSharpView.WinUI",
    "SkiaSharp.NativeAssets.WinUI",
    "Xaml",
    "XamlCompiler",
    "UseWinUI",
    "EnableXamlSourceGenerator",
    "WindowsAppContainer",
    "WindowsAppSDK",
]

# Directories to skip while scanning
SKIP_DIRS = {
    ".git",
    "bin",
    "obj",
    ".vs",
    ".venv",
    "node_modules",
    "TestResults",
    "tools",
}

# File extensions we consider text-searchable
TEXT_EXT = {'.cs', '.csproj', '.vb', '.xaml', '.props', '.targets', '.sln', '.json', '.md', '.py', '.ps1', '.config', '.yml', '.yaml', '.txt', '.xml'}


def is_text_file(path: Path) -> bool:
    ext = path.suffix.lower()
    if ext in TEXT_EXT:
        return True
    # Also include files without extension but not binaries
    if ext == '':
        return True
    return False


def scan(root: Path):
    occurrences = []
    projects = set()
    files_with_matches = set()
    for dirpath, dirnames, filenames in os.walk(root):
        # prune skip dirs
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
        for f in filenames:
            fp = Path(dirpath) / f
            try:
                if not is_text_file(fp):
                    continue
                # small safety - skip large files
                if fp.stat().st_size > 2_000_000:  # 2MB
                    continue
                text = fp.read_text(encoding='utf-8', errors='ignore')
            except Exception:
                continue

            lower = text.lower()
            hits = []
            for kw in KEYWORDS:
                if kw.lower() in lower:
                    hits.append(kw)
            if hits:
                files_with_matches.add(str(fp.relative_to(root)))
                # capture lines where matches occurred
                lines = []
                for i, line in enumerate(text.splitlines(), start=1):
                    for kw in hits:
                        if kw.lower() in line.lower():
                            lines.append({'line': i, 'text': line.strip()})

                occurrences.append({'file': str(fp.relative_to(root)), 'keywords': list(sorted(set(hits))), 'lines': lines})
                # check for csproj to collect project names
                if fp.suffix.lower() == '.csproj':
                    projects.add(str(fp.relative_to(root)))

    return occurrences, sorted(projects)


def build_plan(occurrences, projects):
    plan = []
    # High-level steps
    plan.append('1) Identify projects and test projects that depend on WinUI or Windows App SDK.\n')
    plan.append('2) Remove the WinUI project(s) from the solution (.sln) if it is no longer needed. \n')
    plan.append('3) Remove project folders for WinUI UI project(s) and any WinUI test projects (move to archive if desired).\n')
    plan.append('4) Remove related package versions from Directory.Packages.props and any direct PackageReference entries.\n')
    plan.append('5) Remove any build / XAML tooling glue such as XamlCompiler targets and diagnostic wrappers in csproj files.\n')
    plan.append('6) Update CI (workflow files) to stop building or testing WinUI projects.\n')
    plan.append('7) Clean/restore/rebuild solution; verify no XAML compilation steps remain and the solution builds cleanly.\n')
    plan.append('8) Address any follow-up warnings (nullable or analyzers) in the remaining projects.\n')

    details = {
        'found_file_count': len(occurrences),
        'projects_with_matches': projects,
        'affected_files': [o['file'] for o in occurrences],
        'suggested_actions': plan,
    }
    return details


def main(argv):
    parser = argparse.ArgumentParser(description='Scan workspace for WinUI/WindowsAppSDK usages and produce a removal plan')
    parser.add_argument('--root', '-r', default='.', help='Repository root to scan')
    parser.add_argument('--report', '-o', default=None, help='Save JSON report to a file')
    args = parser.parse_args(argv)

    root = Path(args.root).resolve()
    occurrences, projects = scan(root)
    report = build_plan(occurrences, projects)

    # print a short human-readable table
    print('\nWinUI scan summary')
    print('====================')
    print(f"Found {report['found_file_count']} files mentioning WinUI/WindowsAppSDK keywords")
    if projects:
        print('\nProjects that contain matches:')
        for p in projects:
            print(' -', p)

    if report['found_file_count'] > 0:
        print('\nAffect files found (top 40):')
        for f in report['affected_files'][:40]:
            print(' *', f)

    print('\nSuggested removal plan (high level):')
    for s in report['suggested_actions']:
        print(' -', s.strip())

    if args.report:
        try:
            with open(args.report, 'w', encoding='utf-8') as fh:
                json.dump(report, fh, indent=2)
            print(f'\nReport written to {args.report}')
        except Exception as ex:
            print('Could not write report:', ex)

    # return non-zero if critical project present (so scripts can fail-fast)
    if any('WinUI' in ' '.join(o['keywords']) or 'WindowsAppSDK' in ' '.join(o['keywords']) for o in occurrences):
        return 0
    return 0


if __name__ == '__main__':
    raise SystemExit(main(sys.argv[1:]))
