#!/usr/bin/env python3
"""
Wiley Widget WPF Resource Scanner
===================================

This script scans the Wiley Widget codespace (WPF project) for all WPF resource references
and definitions, detecting missing resources, duplicates, and potential runtime issues.

Usage:
    python resource_scanner.py [OPTIONS]

Key Features:
- Scans XAML, CS, and resource files recursively
- Detects brushes, styles, converters, data templates, control templates, colors, etc.
- Identifies duplicate definitions (resource key conflicts)
- Cross-references usage vs definitions
- Generates comprehensive report for startup validation
- Aligns with Syncfusion WPF best practices

Dependencies: None (uses stdlib: os, re, argparse, json)

Report Output: JSON + console summary for CI/integration with Test 70 (SfSkinManager E2E)

Author: Grok (xAI) - Generated Nov 10, 2025 for Wiley Widget
"""

import argparse
import json
import os
import re
from collections import defaultdict
from pathlib import Path
from typing import Dict, List, Set, Tuple

# Common file extensions to scan
XAML_EXT = ".xaml"
CS_EXT = ".cs"
RES_EXT = ".xaml"  # ResourceDictionaries are XAML

# Regex patterns for resource references
# XAML: StaticResource or DynamicResource usage
XAML_USAGE_PATTERN = re.compile(
    r"\{(?:Static|Dynamic)Resource\s+([^\s\},]+)\}", re.IGNORECASE
)

# Resource definition patterns
RESOURCE_PATTERNS = {
    "SolidColorBrush": re.compile(r'<SolidColorBrush\s+x:Key="([^"]+)"', re.IGNORECASE),
    "LinearGradientBrush": re.compile(
        r'<LinearGradientBrush\s+x:Key="([^"]+)"', re.IGNORECASE
    ),
    "RadialGradientBrush": re.compile(
        r'<RadialGradientBrush\s+x:Key="([^"]+)"', re.IGNORECASE
    ),
    "Style": re.compile(r'<Style\s+x:Key="([^"]+)"', re.IGNORECASE),
    "ControlTemplate": re.compile(r'<ControlTemplate\s+x:Key="([^"]+)"', re.IGNORECASE),
    "DataTemplate": re.compile(r'<DataTemplate\s+x:Key="([^"]+)"', re.IGNORECASE),
    "Converter": re.compile(r'<\w+:(\w+Converter)\s+x:Key="([^"]+)"', re.IGNORECASE),
    "Color": re.compile(r'<Color\s+x:Key="([^"]+)"', re.IGNORECASE),
    "Thickness": re.compile(r'<Thickness\s+x:Key="([^"]+)"', re.IGNORECASE),
    "Double": re.compile(r'<system:Double\s+x:Key="([^"]+)"', re.IGNORECASE),
    "String": re.compile(r'<system:String\s+x:Key="([^"]+)"', re.IGNORECASE),
}

# C#: Application.Current.Resources["BrushName"] or {StaticResource BrushName}
CS_USAGE_PATTERN = re.compile(
    r'(?:Resources\["|StaticResource\s+|FindResource\()([^\]"\s,\)]+)', re.IGNORECASE
)

# Syncfusion-specific brushes (from docs/help.syncfusion.com/wpf/themes/skin-manager)
SYNCFUSION_BRUSHES = {
    "ContentBackground",
    "PrimaryBackground",
    "SecondaryBackground",
    "PrimaryForeground",
    "SecondaryForeground",
    "BorderBrush",
    "AccentBrush",
    "HoverBrush",
    "PressedBrush",
    "DisabledBrush",
    "SuccessBrush",
    "WarningBrush",
    "ErrorBrush",
    "InfoBrush",
}


class ResourceScanner:
    def __init__(self, root_path: str):
        self.root = Path(root_path)
        self.referenced_resources: Set[str] = set()
        self.defined_resources: Dict[str, str] = {}  # key -> resource_type
        self.resource_locations: Dict[str, List[Tuple[str, int]]] = defaultdict(
            list
        )  # key -> [(file, line)]
        self.definition_locations: Dict[str, List[Tuple[str, int, str]]] = defaultdict(
            list
        )  # key -> [(file, line, type)]
        self.missing_resources: Set[str] = set()
        self.duplicate_resources: Dict[str, List[Tuple[str, int, str]]] = (
            {}
        )  # key -> [(file, line, type)]

    def scan(self, output_json: str = None):
        """Main scan entrypoint."""
        print("🔍 Scanning Wiley Widget codespace for WPF resources...")
        self._scan_references()
        self._scan_definitions()
        self._analyze()
        self._report(output_json)

    def _scan_references(self):
        """Scan XAML/CS files for resource usages."""
        for ext, pattern in [
            (XAML_EXT, XAML_USAGE_PATTERN),
            (CS_EXT, CS_USAGE_PATTERN),
        ]:
            for file_path in self.root.rglob(f"*{ext}"):
                if (
                    file_path.is_file()
                    and "bin" not in str(file_path).lower()
                    and "obj" not in str(file_path).lower()
                ):
                    self._parse_file_for_usages(file_path, pattern)

    def _parse_file_for_usages(self, file_path: Path, pattern: re.Pattern):
        """Parse a single file for resource key usages, track line numbers."""
        try:
            with open(file_path, "r", encoding="utf-8") as f:
                lines = f.readlines()
            for line_num, line in enumerate(lines, 1):
                matches = pattern.findall(line)
                for match in matches:
                    key = match.strip().strip("{}")
                    # Filter noise: skip very short keys (likely false positives like "or", "a", etc.)
                    if key and not key.startswith("{") and len(key) > 2:
                        self.referenced_resources.add(key)
                        self.resource_locations[key].append((str(file_path), line_num))
        except Exception as e:
            print(f"⚠️  Error parsing {file_path}: {e}")

    def _scan_definitions(self):
        """Scan resource XAML files for all resource definitions."""
        for file_path in self.root.rglob(f"*{RES_EXT}"):
            if file_path.is_file() and (
                "themes" in str(file_path).lower()
                or "resources" in str(file_path).lower()
                or "app.xaml" in str(file_path).lower()
            ):
                self._parse_file_for_definitions(file_path)

    def _parse_file_for_definitions(self, file_path: Path):
        """Parse XAML for all resource definitions with x:Key attributes."""
        try:
            with open(file_path, "r", encoding="utf-8") as f:
                content = f.read()
                content.split("\n")

                # Scan for each resource type
                for resource_type, pattern in RESOURCE_PATTERNS.items():
                    matches = pattern.finditer(content)
                    for match in matches:
                        # Get line number
                        line_num = content[: match.start()].count("\n") + 1

                        # Extract key based on pattern
                        if resource_type == "Converter":
                            key = match.group(2)  # Converters have 2 groups
                        else:
                            key = match.group(1)

                        # Track definition location
                        self.definition_locations[key].append(
                            (str(file_path), line_num, resource_type)
                        )

                        # Check for duplicates
                        if key in self.defined_resources:
                            if key not in self.duplicate_resources:
                                # First duplicate found - add original location
                                self.duplicate_resources[key] = list(
                                    self.definition_locations[key]
                                )
                        else:
                            self.defined_resources[key] = resource_type

        except Exception as e:
            print(f"⚠️  Error parsing definitions in {file_path}: {e}")
        except Exception as e:
            print(f"⚠️  Error parsing definitions in {file_path}: {e}")

    def _analyze(self):
        """Cross-reference references vs definitions; flag Syncfusion essentials."""
        self.missing_brushes = self.referenced_brushes - self.defined_brushes
        # Ensure Syncfusion critical brushes are present
        missing_sync = SYNCFUSION_BRUSHES - self.defined_brushes
        if missing_sync:
            print(
                f"⚠️  Missing Syncfusion brushes: {', '.join(missing_sync)} - May cause startup warnings!"
            )

    def _report(self, output_json: str = None):
        """Generate console + JSON report."""
        report = {
            "total_referenced": len(self.referenced_brushes),
            "total_defined": len(self.defined_brushes),
            "missing": sorted(list(self.missing_brushes)),
            "critical_missing_syncfusion": sorted(
                list(SYNCFUSION_BRUSHES - self.defined_brushes)
            ),
            "locations": {
                k: v
                for k, v in self.brush_locations.items()
                if k in self.missing_brushes
            },
            "recommendations": [
                "Add missing brushes to src/WileyWidget/Themes/Generic.xaml or Syncfusion theme overrides.",
                "Verify in App.xaml.cs: SfSkinManager.ApplyThemeAsDefaultStyle = true; pre-InitializeComponent.",
                'Run post-fix: Ensure no "missing brushes" WRN in logs during startup.',
            ],
        }

        print("\n📊 Brush Scan Report")
        print("=" * 50)
        print(f"✅ Defined Brushes: {len(self.defined_brushes)}")
        print(f"❌ Missing Brushes: {len(self.missing_brushes)}")
        if self.missing_brushes:
            print("Missing: " + ", ".join(sorted(self.missing_brushes)))
        print("\n🔑 Critical Syncfusion Brushes Status:")
        for brush in sorted(SYNCFUSION_BRUSHES):
            status = "✅" if brush in self.defined_brushes else "❌"
            print(f"  {status} {brush}")

        if output_json:
            # Fix: Ensure output directory exists
            output_path = Path(output_json)
            os.makedirs(output_path.parent, exist_ok=True)

            with open(output_json, "w") as f:
                json.dump(report, f, indent=2)
            print(f"\n💾 Full report saved: {output_json}")

        if not self.missing_brushes and not (SYNCFUSION_BRUSHES - self.defined_brushes):
            print("\n🎉 All brushes accounted for - Startup ready!")


def main():
    parser = argparse.ArgumentParser(
        description="Scan Wiley Widget for WPF brush resources."
    )
    parser.add_argument(
        "--path", "-p", default=".", help="Root path to scan (default: current dir)"
    )
    parser.add_argument("--output", "-o", help="JSON report output file")
    parser.add_argument(
        "--syncfusion-only",
        action="store_true",
        help="Focus report on Syncfusion critical brushes only",
    )
    args = parser.parse_args()

    if not os.path.exists(args.path):
        raise ValueError(f"Path not found: {args.path}")

    scanner = BrushScanner(args.path)
    scanner.scan(output_json=args.output)


if __name__ == "__main__":
    main()
