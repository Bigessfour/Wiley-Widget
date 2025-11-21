#!/usr/bin/env python3
"""
cleanup_wpf_prism_remnants.py

Comprehensive script to remove WPF and Prism.Uno.WinUI remnants from the Wiley-Widget codebase.
Focuses on docs, scripts, tests, and configs. Backs up files and supports dry-run.

Author: AI Agent (based on codebase analysis)
Version: 1.0
Python: 3.11.9 compatible
"""

import os
import re
import shutil
import sys
from argparse import ArgumentParser
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, List, Optional, Tuple

# Config: Patterns to find/replace. Extend as needed.
REPLACEMENT_PATTERNS = {
    # Prism.Uno.WinUI → Uno.WinUI
    r"Prism\.Wpf": "Prism.Uno.WinUI",
    r"Prism\.DryIoc\.PrismApplication": "Prism.DryIoc.Uno.WinUI.PrismApplication",
    r'xmlns:prism="clr-namespace:Prism\.Regions;assembly=Prism\.Wpf"': 'xmlns:prism="using:Prism.Navigation.Regions"',
    r'xmlns:prism="http://prismlibrary\.com/"': "",  # Delete old URI
    r"Prism\.Regions": "Prism.Navigation.Regions",
    r"Syncfusion\.SfChart\.WPF": "Syncfusion.UI.Xaml.Charts",
    r"Syncfusion\.SfDataGrid\.WPF": "Syncfusion.UI.Xaml.DataGrid",
    r"Syncfusion WPF (\d+\.\d+\.\d+)": "Syncfusion WinUI \\g<1>",  # Preserve version
    # Package refs: Remove Prism.Uno.WinUI
    r'<PackageReference Include="Prism\.Wpf" Version="[^"]*" />': "",
    r"dotnet add package Prism\.Wpf --version \d+\.\d+\.\d+": "dotnet add package Prism.Uno.WinUI --version 9.0.537",
    # Old exceptions/logs
    r"Exception thrown: .* in Prism\.Wpf\.dll": "# [REMOVED: Legacy WPF exception]",
    r"Microsoft.UI.Xaml": "Microsoft.UI.Xaml",  # WPF → WinUI
    r"WPF: Prism\.DryIoc\.PrismApplication": "Uno: Prism.DryIoc.Uno.WinUI.PrismApplication",
    r"protected override Window CreateShell\(\)": "protected override void CreateWindow()",
    # Docs-specific: Archive migration plan sections
    r"\|\s*WPF Adapter\s*\|\s*Uno Adapter\s*\|\s*": "| Uno Adapter | Description |",  # Flatten tables
    r"✅ \*\*MIGRATION COMPLETE\*\* - WPF DISCONTINUED": "[ARCHIVED] Migration Complete - See Uno/WinUI Docs",
    # NuGet checks
    r"Prism\.Wpf\.dll": "Prism.Uno.WinUI.dll",
    # Troubleshooting
    r"If XAML fails to resolve Prism types: check .* Prism\.Wpf package": "If XAML fails: Ensure Prism.Uno.WinUI is referenced and using: namespaces are correct.",
}

# Files to skip (runtime-safe)
SKIP_DIRS = {
    "src",
    "tests/WileyWidget.Services.Tests/TestData",
    "docker",
    "bin",
    "obj",
    ".git",
    "__pycache__",
    "backups_wpf_cleanup",
    ".venv",
    "venv",
    "Lib",
    "site-packages",
    ".vscode",
    "ci-logs",
    ".continue",
    "logs",
    ".copilot",
    "snapshots",
}
# Common binary/image extensions to skip. Keep JSON processing so we can selectively handle secrets directories.
SKIP_EXTS = {".exe", ".dll", ".png", ".jpg", ".pyc"}

# Files to explicitly skip by filename
SKIP_FILES = {"cleanup_log.txt", "cleanup_wpf_prism_remnants.py"}


@dataclass
class Change:
    file: Path
    line_num: int
    original: str
    replacement: str


class CleanupManager:
    def __init__(self, root_dir: Path, dry_run: bool = False, verbose: bool = False):
        self.root = Path(root_dir).resolve()
        self.dry_run = dry_run
        self.verbose = verbose
        self.changes: List[Change] = []
        self.backup_dir = self.root / "backups_wpf_cleanup"
        # Ensure backup dir is created inside the workspace root
        self.backup_dir.mkdir(parents=True, exist_ok=True)
        self.log_file = self.root / "cleanup_log.txt"

    def log(self, message: str):
        with open(self.log_file, "a", encoding="utf-8") as f:
            f.write(f"{message}\n")
        if self.verbose:
            print(message)

    def backup_file(self, file_path: Path) -> Path:
        backup_path = self.backup_dir / f"{file_path.name}.bak"
        if not self.dry_run and not backup_path.exists():
            shutil.copy2(file_path, backup_path)
            self.log(f"Backed up {file_path} to {backup_path}")
        return backup_path

    def apply_replacements(
        self, content: str, file_path: Path
    ) -> Tuple[str, List[Change]]:
        lines = content.splitlines(keepends=True)
        local_changes = []
        for i, line in enumerate(lines):
            original_line = line
            for pattern, replacement in REPLACEMENT_PATTERNS.items():
                if re.search(pattern, line, re.IGNORECASE | re.MULTILINE):
                    new_line = re.sub(pattern, replacement, line, flags=re.IGNORECASE)
                    if new_line != line:
                        change = Change(
                            file_path, i + 1, original_line.strip(), new_line.strip()
                        )
                        local_changes.append(change)
                        lines[i] = new_line
                        self.log(
                            f"  Line {i+1}: {original_line.strip()[:50]}... → {new_line.strip()[:50]}..."
                        )
            # Special handling for MD tables (simplified flatten for migration plan)
            if "WPF-TO-UNO-MIGRATION-PLAN.md" in str(file_path):
                # operate on the current (possibly already replaced) line
                lines[i] = re.sub(
                    r"\|\s*WPF.*?\|\s*(.*?)\s*\|\s*(.*?)\s*\|", r"| \1 | \2 |", lines[i]
                )
        new_content = "".join(lines)
        return new_content, local_changes

    def process_file(self, file_path: Path) -> bool:
        # Ensure file is inside the configured workspace root to avoid accidental edits outside scope
        try:
            if not file_path.resolve().is_relative_to(self.root):
                return False
        except AttributeError:
            # Fallback for older Python versions: string prefix check
            if not str(file_path.resolve()).startswith(str(self.root)):
                return False

        # Skip known non-project directories or Copilot/IDE snapshots and large logs
        path_lower = str(file_path).lower()
        # If any skip token appears anywhere in the path, skip the file
        if any(skip.lower() in path_lower for skip in SKIP_DIRS):
            return False
        # Skip files or folders that include 'copilot' in their path
        if "copilot" in path_lower:
            return False
        # Skip log files named like '*a.txt' under logs folders (common snapshots/log dumps)
        if "logs" in path_lower and re.match(r".*a\.txt$", file_path.name.lower()):
            return False
        # Skip explicit filenames (script logfile and this script)
        if file_path.name in SKIP_FILES:
            return False
        if file_path.suffix.lower() in SKIP_EXTS:
            return False
        try:
            with open(file_path, "r", encoding="utf-8") as f:
                content = f.read()
        except UnicodeDecodeError:
            self.log(f"Skipping binary/non-UTF8: {file_path}")
            return False

        new_content, local_changes = self.apply_replacements(content, file_path)
        if local_changes or self.dry_run:
            self.backup_file(file_path)
            if not self.dry_run and new_content != content:
                with open(file_path, "w", encoding="utf-8") as f:
                    f.write(new_content)
                self.changes.extend(local_changes)
                self.log(f"Modified: {file_path} ({len(local_changes)} changes)")
                return True
            else:
                self.log(
                    f"[DRY-RUN] Would modify: {file_path} ({len(local_changes)} changes)"
                )
                self.changes.extend(local_changes)
                return True
        return False

    def scan_and_process(self) -> int:
        processed = 0
        for file_path in self.root.rglob("*"):
            if file_path.is_file():
                if self.process_file(file_path):
                    processed += 1
        self.log(f"Processed {processed} files with changes.")
        return processed

    def final_validation(self) -> Dict[str, int]:
        # Run a simple grep-like check for remnants
        remnants = {"Prism.Wpf": 0, "Microsoft.UI.Xaml": 0, "WPF Adapter": 0}
        for file_path in self.root.rglob("*.md"):
            try:
                with open(file_path, "r", encoding="utf-8") as f:
                    content = f.read()
                for key in remnants:
                    # treat the key as literal text when searching
                    remnants[key] += len(
                        re.findall(re.escape(key), content, re.IGNORECASE)
                    )
            except:
                pass
        self.log("Final Validation Remnants:")
        for k, v in remnants.items():
            self.log(f"  {k}: {v} matches")
        return remnants


def main():
    parser = ArgumentParser(
        description="Cleanup WPF/Prism.Uno.WinUI remnants in Wiley-Widget repo."
    )
    parser.add_argument(
        "--dry-run", action="store_true", help="Preview changes without applying."
    )
    parser.add_argument("--verbose", "-v", action="store_true", help="Verbose logging.")
    parser.add_argument(
        "--root", default=".", help="Repo root directory (default: current)."
    )
    args = parser.parse_args()

    # Safety: ensure the provided root is inside the repository where this script resides
    script_repo_root = Path(__file__).resolve().parent
    requested_root = Path(args.root).resolve()
    try:
        if not requested_root.is_relative_to(script_repo_root):
            print(
                f"Error: requested root '{requested_root}' is outside the repository root '{script_repo_root}'. Aborting."
            )
            sys.exit(1)
    except AttributeError:
        # Fallback for older Python versions
        if not str(requested_root).startswith(str(script_repo_root)):
            print(
                f"Error: requested root '{requested_root}' is outside the repository root '{script_repo_root}'. Aborting."
            )
            sys.exit(1)

    manager = CleanupManager(requested_root, dry_run=args.dry_run, verbose=args.verbose)
    manager.log("Starting WPF/Prism cleanup...")

    num_changed = manager.scan_and_process()
    manager.log(f"Cleanup complete. {num_changed} files processed.")
    remnants = manager.final_validation()

    if all(v == 0 for v in remnants.values()):
        manager.log("✅ Validation: No remnants found! Codebase is clean.")
    else:
        manager.log("⚠️  Some remnants remain – review log and re-run if needed.")

    if not args.dry_run:
        manager.log(f"Backups saved to: {manager.backup_dir}")
        manager.log("Review changes, then commit or revert as needed.")


if __name__ == "__main__":
    main()
