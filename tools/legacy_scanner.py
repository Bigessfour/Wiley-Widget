#!/usr/bin/env python3
"""
Legacy Scanner for Syncfusion and Prism Remnants in Wiley Widget Codebase.
Scans C#/XAML/.csproj files for legacy references. Outputs console report and JSON file.

Usage: python legacy_scanner.py [OPTIONS]
Options:
  --root DIR        Root directory to scan (default: .)
  --patterns FILE   JSON file with custom patterns (default: built-in)
  --output FILE     Output JSON file (default: legacy_scan_report.json)
  --verbose         Show all matches (default: summary only)
  --fail-on-hits    Exit with code 1 if any hits found (for CI)
"""

import argparse
import json
import re
import sys
from concurrent.futures import ThreadPoolExecutor
from dataclasses import asdict, dataclass
from datetime import datetime
from pathlib import Path
from typing import Any, Dict, List, Optional


@dataclass
class ScanHit:
    """Represents a single detection of legacy code."""

    file_path: str
    line_num: int
    line_content: str
    pattern_type: str  # e.g., 'Prism_Namespace', 'Syncfusion_Control'
    suggestion: str  # Optional refactor hint


class LegacyScanner:
    """Main scanner class for detecting Syncfusion and Prism legacy code."""

    def __init__(self, root_dir: Path = Path(".")):
        self.root_dir = root_dir.resolve()
        self.ignore_dirs = {
            "bin",
            "obj",
            "packages",
            "node_modules",
            ".git",
            "__pycache__",
            ".vs",
            ".vscode",
            "TestResults",
            "coverage",
            "logs",
            "secrets",
            "temp",
            "temp_test",
            "ci-logs",
            "xaml-logs",
        }
        self.file_extensions = {
            ".cs",
            ".xaml",
            ".csproj",
            ".config",
            ".xml",
            ".targets",
            ".props",
        }
        self.hits: List[ScanHit] = []

        # Built-in regex patterns (case-insensitive, multi-line aware where needed)
        self.patterns: Dict[str, Dict[str, Any]] = {
            "Prism": {
                "Namespace_Using": r"(?:using|global\s+using)\s+Prism(?:\.\w+)*\s*;",
                "Classes_Methods": r"\b(BindableBase|DelegateCommand|IEventAggregator|IRegionManager|ContainerLocator|PrismApplication|ViewModelBase|INavigationAware|IConfirmNavigation)\b",
                "XAML_Regions": r"prism:(?:RegionManager\.RegionName|ClearChildContent|ViewModelLocator)",
                "Package_Reference": r'<PackageReference\s+Include="Prism\.',
                "Suggestion": "Replace with CommunityToolkit.Mvvm (e.g., ObservableObject, RelayCommand) or manual DI.",
            },
            "Syncfusion": {
                "Namespace_Using": r"(?:using|global\s+using)\s+Syncfusion(?:\.\w+)*\s*;",
                "Controls_Methods": r"\b(SfDataGrid|SfChart|SfTreeView|SfBusyIndicator|SfEditors|SfProgressBar|SfGauge|SfDatePicker|SfTextBox|SfComboBox|SfNumericTextBox)\b",
                "XAML_Tags": r"<(?:syncfusion:)?(?:SfDataGrid|SfChart|SfTreeView|SfBusyIndicator|SfEditors|SfProgressBar|SfGauge|SfDatePicker|SfTextBox|SfComboBox|SfNumericTextBox)",
                "XAML_Namespace": r"xmlns:syncfusion=",
                "Licensing": r"SyncfusionLicenseProvider\.RegisterLicense",
                "Package_Reference": r'<PackageReference\s+Include="Syncfusion\.',
                "Suggestion": "Replace with native WinUI controls (e.g., DataGrid, ProgressRing) from Microsoft.UI.Xaml.Controls.",
            },
        }

    def is_ignorable(self, path: Path) -> bool:
        """Check if path should be ignored based on ignore_dirs."""
        return any(ignored in path.parts for ignored in self.ignore_dirs)

    def scan_file(self, file_path: Path) -> List[ScanHit]:
        """Scan a single file for legacy patterns."""
        hits = []

        # Skip if file is too large (>5MB likely binary)
        try:
            if file_path.stat().st_size > 5 * 1024 * 1024:
                return hits
        except Exception:
            return hits

        try:
            with open(file_path, "r", encoding="utf-8", errors="ignore") as f:
                lines = f.readlines()
        except Exception:
            return hits  # Skip unreadable files

        for i, line in enumerate(lines, 1):
            line_stripped = line.strip()
            if not line_stripped or line_stripped.startswith("//"):
                continue  # Skip empty lines and full-line comments

            for category, pats in self.patterns.items():
                for pat_name, pat_regex in pats.items():
                    if pat_name == "Suggestion":
                        continue

                    if re.search(pat_regex, line_stripped, re.IGNORECASE):
                        suggestion = pats.get("Suggestion", "")
                        hits.append(
                            ScanHit(
                                file_path=str(file_path.relative_to(self.root_dir)),
                                line_num=i,
                                line_content=line_stripped[:200],  # Truncate long lines
                                pattern_type=f"{category}_{pat_name}",
                                suggestion=suggestion,
                            )
                        )
        return hits

    def scan(self, max_workers: int = 4) -> List[ScanHit]:
        """Scan all relevant files in the codebase."""
        all_files = []

        print(f"Scanning from root: {self.root_dir}")
        print(f"Looking for extensions: {', '.join(self.file_extensions)}")

        for ext in self.file_extensions:
            all_files.extend(self.root_dir.rglob(f"*{ext}"))

        filtered_files = [
            f for f in all_files if not self.is_ignorable(f) and f.is_file()
        ]
        print(f"Scanning {len(filtered_files)} files...")

        with ThreadPoolExecutor(max_workers=max_workers) as executor:
            results = executor.map(self.scan_file, filtered_files)
            for file_hits in results:
                self.hits.extend(file_hits)

        return self.hits

    def generate_report(self, verbose: bool = False) -> Dict[str, Any]:
        """Generate structured report from scan results."""
        prism_hits = [h for h in self.hits if "Prism" in h.pattern_type]
        syncfusion_hits = [h for h in self.hits if "Syncfusion" in h.pattern_type]

        report: Dict[str, Any] = {
            "summary": {
                "total_hits": len(self.hits),
                "prism_hits": len(prism_hits),
                "syncfusion_hits": len(syncfusion_hits),
                "affected_files": len(set(h.file_path for h in self.hits)),
                "scan_date": datetime.now().isoformat(),
                "root_dir": str(self.root_dir),
            }
        }

        if verbose:
            # Include all hits with full details
            report["hits"] = [asdict(hit) for hit in self.hits]
        else:
            # Group by file for summary
            file_groups: Dict[str, List[ScanHit]] = {}
            for hit in self.hits:
                if hit.file_path not in file_groups:
                    file_groups[hit.file_path] = []
                file_groups[hit.file_path].append(hit)

            # Convert to serializable format
            report["file_summary"] = {
                file_path: [asdict(hit) for hit in hits]
                for file_path, hits in file_groups.items()
            }

        return report

    def print_report(self, report: Dict[str, Any], output_file: Optional[str] = None):
        """Print human-readable report to console."""
        summary = report["summary"]

        print("\n" + "=" * 60)
        print("   LEGACY SCAN REPORT - Wiley Widget")
        print("=" * 60)
        print(
            f"Total Hits: {summary['total_hits']} "
            f"(Prism: {summary['prism_hits']}, Syncfusion: {summary['syncfusion_hits']})"
        )
        print(f"Affected Files: {summary['affected_files']}")
        print(f"Scanned: {summary['root_dir']}")
        print(f"Date: {summary['scan_date']}")
        print("=" * 60)

        if summary["total_hits"] == 0:
            print("\n✅ SUCCESS: No legacy remnants found! Codebase is clean.")
            return

        print("\n⚠️  FILES WITH LEGACY CODE DETECTED:\n")

        if "file_summary" in report:
            sorted_files = sorted(
                report["file_summary"].items(), key=lambda x: len(x[1]), reverse=True
            )

            for file_path, hits in sorted_files:
                print(f"\n📄 {file_path} ({len(hits)} hits):")

                # Show top 3 hits per file
                for hit in hits[:3]:
                    print(f"  Line {hit['line_num']:4d}: {hit['line_content'][:100]}")
                    print(f"           Type: {hit['pattern_type']}")
                    if (
                        hit["suggestion"] and hit == hits[0]
                    ):  # Show suggestion once per file
                        print(f"           💡 {hit['suggestion']}")

                if len(hits) > 3:
                    print(
                        f"  ... and {len(hits)-3} more hits. Run with --verbose for full list."
                    )

        if output_file:
            output_path = Path(output_file)
            with open(output_path, "w", encoding="utf-8") as f:
                json.dump(report, f, indent=2, ensure_ascii=False)
            print(f"\n📊 Full report saved to: {output_path.resolve()}")

        print("\n" + "=" * 60)
        print("Next Steps:")
        print("  1. Review affected files listed above")
        print("  2. Refactor using suggested replacements")
        print("  3. Re-run scanner to verify cleanup")
        print("  4. Consider adding to CI with --fail-on-hits")
        print("=" * 60 + "\n")


def main():
    parser = argparse.ArgumentParser(
        description="Scan Wiley Widget codebase for Syncfusion/Prism legacy code.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  python legacy_scanner.py                         # Basic scan
  python legacy_scanner.py --verbose               # Show all hits
  python legacy_scanner.py --root src              # Scan specific dir
  python legacy_scanner.py --fail-on-hits          # CI mode (exit 1 if hits)
  python legacy_scanner.py --patterns custom.json  # Use custom patterns
        """,
    )
    parser.add_argument(
        "--root",
        type=Path,
        default=Path("."),
        help="Root directory to scan (default: current directory)",
    )
    parser.add_argument(
        "--patterns",
        type=str,
        default=None,
        help="JSON file with custom patterns to merge with built-in",
    )
    parser.add_argument(
        "--output",
        type=str,
        default="legacy_scan_report.json",
        help="Output JSON file (default: legacy_scan_report.json)",
    )
    parser.add_argument(
        "--verbose",
        action="store_true",
        help="Show all hits in detail (default: summary only)",
    )
    parser.add_argument(
        "--fail-on-hits",
        action="store_true",
        help="Exit with code 1 if any hits found (for CI integration)",
    )
    parser.add_argument(
        "--max-workers",
        type=int,
        default=4,
        help="Max parallel workers for scanning (default: 4)",
    )

    args = parser.parse_args()

    # Initialize scanner
    scanner = LegacyScanner(args.root)

    # Load custom patterns if provided
    if args.patterns:
        try:
            with open(args.patterns, "r", encoding="utf-8") as f:
                custom_pats = json.load(f)
                scanner.patterns.update(custom_pats)
                print(f"Loaded custom patterns from: {args.patterns}")
        except Exception as e:
            print(f"Warning: Failed to load custom patterns: {e}")

    # Run scan
    scanner.scan(max_workers=args.max_workers)

    # Generate and print report
    report = scanner.generate_report(verbose=args.verbose)
    scanner.print_report(report, args.output)

    # Exit with appropriate code
    if args.fail_on_hits and report["summary"]["total_hits"] > 0:
        sys.exit(1)
    else:
        sys.exit(0)


if __name__ == "__main__":
    main()
