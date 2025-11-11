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

import os
import re
import json
import argparse
from pathlib import Path
from typing import Set, Dict, List, Tuple, Optional
from collections import defaultdict, Counter

# Common file extensions to scan
XAML_EXT = '.xaml'
CS_EXT = '.cs'
RES_EXT = '.xaml'  # ResourceDictionaries are XAML

# Regex patterns for resource references
# XAML: StaticResource or DynamicResource usage
XAML_USAGE_PATTERN = re.compile(r'\{(?:Static|Dynamic)Resource\s+([^\s\},]+)\}', re.IGNORECASE)

# C#: Application.Current.Resources["ResourceName"] or {StaticResource ResourceName}
CS_USAGE_PATTERN = re.compile(r'(?:Resources\["|StaticResource\s+|FindResource\()([^\]"\s,\)]+)', re.IGNORECASE)

# Resource definition patterns (expanded for all common WPF/Syncfusion types)
RESOURCE_PATTERNS = {
    'SolidColorBrush': re.compile(r'<SolidColorBrush\s+x:Key="([^"]+)"', re.IGNORECASE),
    'LinearGradientBrush': re.compile(r'<LinearGradientBrush\s+x:Key="([^"]+)"', re.IGNORECASE),
    'RadialGradientBrush': re.compile(r'<RadialGradientBrush\s+x:Key="([^"]+)"', re.IGNORECASE),
    'Style': re.compile(r'<Style\s+x:Key="([^"]+)"', re.IGNORECASE),
    'ControlTemplate': re.compile(r'<ControlTemplate\s+x:Key="([^"]+)"', re.IGNORECASE),
    'DataTemplate': re.compile(r'<DataTemplate\s+x:Key="([^"]+)"', re.IGNORECASE),
    'Converter': re.compile(r'<\w+:(\w+Converter)\s+x:Key="([^"]+)"', re.IGNORECASE),
    'Color': re.compile(r'<Color\s+x:Key="([^"]+)"', re.IGNORECASE),
    'Thickness': re.compile(r'<Thickness\s+x:Key="([^"]+)"', re.IGNORECASE),
    'Double': re.compile(r'<system:Double\s+x:Key="([^"]+)"', re.IGNORECASE),
    'String': re.compile(r'<system:String\s+x:Key="([^"]+)"', re.IGNORECASE),
    # Additional Syncfusion/WPF resources
    'FontFamily': re.compile(r'<FontFamily\s+x:Key="([^"]+)"', re.IGNORECASE),
    'BitmapImage': re.compile(r'<BitmapImage\s+x:Key="([^"]+)"', re.IGNORECASE),
    'Geometry': re.compile(r'<Geometry\s+x:Key="([^"]+)"', re.IGNORECASE),
    'PathGeometry': re.compile(r'<PathGeometry\s+x:Key="([^"]+)"', re.IGNORECASE),
}

# Syncfusion-specific critical resources (expanded beyond brushes)
SYNCFUSION_RESOURCES = {
    # Brushes (from previous)
    'ContentBackground', 'PrimaryBackground', 'SecondaryBackground',
    'PrimaryForeground', 'SecondaryForeground', 'BorderBrush',
    'AccentBrush', 'HoverBrush', 'PressedBrush', 'DisabledBrush',
    'SuccessBrush', 'WarningBrush', 'ErrorBrush', 'InfoBrush',
    # Additional common Syncfusion styles/templates
    'ButtonStyle', 'TextBoxStyle', 'ComboBoxStyle', 'DataGridStyle',
    'SfDataGridStyle', 'SfChartStyle', 'SfRibbonStyle',
    # Other essentials
    'DefaultFontFamily', 'DefaultFontSize', 'ControlBorderThickness'
}

class ResourceScanner:
    def __init__(self, root_path: str):
        self.root = Path(root_path)
        self.referenced_resources: Set[str] = set()
        self.defined_resources: Set[str] = set()
        self.duplicate_resources: Dict[str, List[str]] = {}  # key -> [file_paths]
        self.resource_locations: Dict[str, List[Tuple[str, int]]] = defaultdict(list)  # key -> [(file, line)]
        self.resource_types: Dict[str, str] = {}  # key -> inferred type (e.g., 'Brush', 'Style')
        self.missing_resources: Set[str] = set()

    def scan(self, output_json: Optional[str] = None, syncfusion_only: bool = False):
        """Main scan entrypoint."""
        print("🔍 Scanning Wiley Widget codespace for WPF resources...")
        self._scan_references()
        self._scan_definitions()
        self._analyze()
        self._report(output_json, syncfusion_only)

    def _scan_references(self):
        """Scan XAML/CS files for resource usages."""
        for ext, pattern in [(XAML_EXT, XAML_USAGE_PATTERN), (CS_EXT, CS_USAGE_PATTERN)]:
            for file_path in self.root.rglob(f'*{ext}'):
                if file_path.is_file() and 'bin' not in str(file_path).lower() and 'obj' not in str(file_path).lower():
                    self._parse_file_for_usages(file_path, pattern)

    def _parse_file_for_usages(self, file_path: Path, pattern: re.Pattern):
        """Parse a single file for resource key usages, track line numbers."""
        try:
            with open(file_path, 'r', encoding='utf-8') as f:
                lines = f.readlines()
            for line_num, line in enumerate(lines, 1):
                matches = pattern.findall(line)
                for match in matches:
                    key = match.strip().strip('{}')
                    # Filter noise: skip very short keys (likely false positives)
                    if key and not key.startswith('{') and len(key) > 2:
                        self.referenced_resources.add(key)
                        self.resource_locations[key].append((str(file_path), line_num))
        except Exception as e:
            print(f"⚠️  Error parsing {file_path}: {e}")

    def _scan_definitions(self):
        """Scan resource XAML files for definitions across all types."""
        for file_path in self.root.rglob(f'*{RES_EXT}'):
            if file_path.is_file() and ('themes' in str(file_path).lower() or 'resources' in str(file_path).lower() or 'app.xaml' in str(file_path).lower()):
                self._parse_file_for_definitions(file_path)

    def _parse_file_for_definitions(self, file_path: Path):
        """Parse XAML for resource definitions using multiple patterns."""
        try:
            with open(file_path, 'r', encoding='utf-8') as f:
                content = f.read()

            # Track definitions per file
            file_defs = []

            for res_type, pattern in RESOURCE_PATTERNS.items():
                matches = pattern.findall(content)
                for match in matches:
                    if isinstance(match, tuple):
                        key = match[1]  # For converters: (class, key)
                    else:
                        key = match
                    if key:
                        file_defs.append(key)
                        self.defined_resources.add(key)
                        self.resource_types[key] = res_type

                        # Track file location for duplicate detection
                        if key not in self.duplicate_resources:
                            self.duplicate_resources[key] = []
                        if str(file_path) not in self.duplicate_resources[key]:
                            self.duplicate_resources[key].append(str(file_path))

            # Detect intra-file duplicates
            def_count = Counter(file_defs)
            for key, count in def_count.items():
                if count > 1:
                    file_str = str(file_path)
                    marked_file = f"{file_str} (duplicate in file)"
                    if marked_file not in self.duplicate_resources.get(key, []):
                        self.duplicate_resources[key].append(marked_file)

        except Exception as e:
            print(f"⚠️  Error parsing definitions in {file_path}: {e}")

    def _analyze(self):
        """Cross-reference references vs definitions; flag criticals."""
        self.missing_resources = self.referenced_resources - self.defined_resources

        # Flag Syncfusion criticals
        missing_sync = SYNCFUSION_RESOURCES - self.defined_resources
        if missing_sync:
            print(f"⚠️  Missing Syncfusion resources: {', '.join(sorted(missing_sync)[:10])}{'...' if len(missing_sync) > 10 else ''}")

        # Flag duplicates
        duplicates = {k: v for k, v in self.duplicate_resources.items() if len(v) > 1}
        if duplicates:
            print(f"⚠️  Duplicate resource keys found: {len(duplicates)}")

    def _report(self, output_json: Optional[str] = None, syncfusion_only: bool = False):
        """Generate console + JSON report."""
        # Filter for Syncfusion only if flagged
        if syncfusion_only:
            filtered_missing = self.missing_resources.intersection(SYNCFUSION_RESOURCES)
            filtered_defined = self.defined_resources.intersection(SYNCFUSION_RESOURCES)
        else:
            filtered_missing = self.missing_resources
            filtered_defined = self.defined_resources

        duplicates_dict = {k: v for k, v in self.duplicate_resources.items() if len(v) > 1}

        report = {
            'total_referenced': len(self.referenced_resources),
            'total_defined': len(self.defined_resources),
            'missing': sorted(list(filtered_missing)),
            'duplicates': duplicates_dict,
            'duplicate_count': len(duplicates_dict),
            'critical_missing_syncfusion': sorted(list(SYNCFUSION_RESOURCES - self.defined_resources)),
            'resource_types': dict(list(self.resource_types.items())[:50]),  # Sample for top refs
            'locations': {k: v for k, v in self.resource_locations.items() if k in filtered_missing},
            'recommendations': [
                'Add missing resources to src/WileyWidget/Themes/Generic.xaml or Syncfusion theme overrides.',
                'Resolve duplicates: Ensure unique keys across ResourceDictionaries.',
                'Verify in App.xaml.cs: SfSkinManager.ApplyThemeAsDefaultStyle = true; pre-InitializeComponent.',
                'Run post-fix: Ensure no "missing resources" WRN in logs during startup.',
                'For converters/styles: Check namespace imports in consuming XAML.'
            ]
        }

        print("\n📊 Resource Scan Report")
        print("=" * 50)
        print(f"✅ Defined Resources: {len(filtered_defined)}")
        print(f"❌ Missing Resources: {len(filtered_missing)}")
        if filtered_missing:
            print("Missing: " + ", ".join(sorted(filtered_missing)[:20]))
            if len(filtered_missing) > 20:
                print(f"... and {len(filtered_missing) - 20} more")

        print("\n🔑 Critical Syncfusion Resources Status:")
        for res in sorted(SYNCFUSION_RESOURCES):
            status = "✅" if res in self.defined_resources else "❌"
            print(f"  {status} {res}")

        if duplicates_dict:
            print(f"\n⚠️  Duplicates ({len(duplicates_dict)}):")
            for key, paths in sorted(list(duplicates_dict.items())[:10]):
                print(f"  {key}: {', '.join(paths[:2])}{'...' if len(paths) > 2 else ''}")
            if len(duplicates_dict) > 10:
                print(f"  ... and {len(duplicates_dict) - 10} more duplicates")

        if output_json:
            # Ensure output directory exists
            output_path = Path(output_json)
            output_path.parent.mkdir(parents=True, exist_ok=True)
            with open(output_json, 'w') as f:
                json.dump(report, f, indent=2)
            print(f"\n💾 Full report saved: {output_json}")

        all_good = not filtered_missing and not (SYNCFUSION_RESOURCES - self.defined_resources) and len(duplicates_dict) == 0
        print("\n" + ("🎉 All resources accounted for - Startup ready!" if all_good else "⚠️  Action required: Review missing/duplicates."))


def main():
    parser = argparse.ArgumentParser(description="Scan Wiley Widget for WPF resources.")
    parser.add_argument('--path', '-p', default='.', help="Root path to scan (default: current dir)")
    parser.add_argument('--output', '-o', help="JSON report output file")
    parser.add_argument('--syncfusion-only', action='store_true',
                        help="Focus report on Syncfusion critical resources only")
    args = parser.parse_args()

    if not os.path.exists(args.path):
        raise ValueError(f"Path not found: {args.path}")

    scanner = ResourceScanner(args.path)
    scanner.scan(output_json=args.output, syncfusion_only=args.syncfusion_only)


if __name__ == '__main__':
    main()
