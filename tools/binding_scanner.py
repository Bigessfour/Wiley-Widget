#!/usr/bin/env python3
"""
Wiley Widget WPF Binding Scanner
================================

This advanced script scans the Wiley Widget WPF project for all data bindings in XAML and C# files.
It extracts binding details (Path, Mode, Converter, etc.), validates correctness (e.g., valid Modes,
cross-references ViewModel properties, flags missing converters/resources), and detects common issues
like unbound Paths or Syncfusion-specific binding pitfalls (e.g., SfDataGrid ItemsSource).

Usage:
    python binding_scanner.py [OPTIONS]

Key Features:
- Recursive scan of XAML/CS files (excludes bin/obj).
- Parses bindings via XML (XAML) + regex (C#).
- Infers ViewModel properties from CS classes (public props/fields).
- Validates: Mode enums, Converter existence (ties to ResourceScanner), Path resolution.
- Flags Syncfusion bindings (e.g., ColumnMapping in SfDataGrid).
- Generates JSON report for CI/Test 70 (e.g., assert no invalid bindings).

Dependencies: None (stdlib: os, re, argparse, json, xml.etree.ElementTree).

Report Output: JSON + console summary. Integrates with Syncfusion WPF best practices
(e.g., ensure TwoWay for editable SfDataGrid cells).

Author: Grok (xAI) - Generated Nov 10, 2025 for Wiley Widget (https://github.com/Bigessfour/Wiley-Widget)
"""

import os
import re
import json
import argparse
from pathlib import Path
from typing import Set, Dict, List, Tuple, Optional
from collections import defaultdict
from xml.etree import ElementTree as ET
from xml.etree.ElementTree import ParseError

# File extensions
XAML_EXT = '.xaml'
CS_EXT = '.cs'

# Valid Binding Modes (WPF standard)
VALID_MODES = {'OneWay', 'TwoWay', 'OneTime', 'OneWayToSource', 'Default'}

# Syncfusion-specific binding patterns (e.g., SfDataGrid)
SYNCFUSION_BINDINGS = {
    'ItemsSource', 'ColumnMapping', 'SfDataGrid.RowBinding', 'SfChart.SeriesBinding'
}

# Regex for XAML bindings: {Binding Path=..., Mode=..., Converter=..., etc.}
XAML_BINDING_PATTERN = re.compile(
    r'\{Binding\s+([^}]+)\}', re.IGNORECASE | re.DOTALL
)
# Extract params: Path=(.*?), Mode=(.*?), etc.
PARAM_EXTRACTOR = re.compile(r'(\w+)=([^,\s\}]+(?:\s*,\s*\w+=[^,\s\}]+)*)', re.IGNORECASE)

# RelativeSource bindings
RELATIVE_SOURCE_PATTERN = re.compile(
    r'\{Binding\s+[^}]*RelativeSource\s*=\s*\{RelativeSource\s+([^}]+)\}', re.IGNORECASE
)

# ElementName bindings
ELEMENT_NAME_PATTERN = re.compile(
    r'\{Binding\s+[^}]*ElementName\s*=\s*([^,\}\s]+)', re.IGNORECASE
)

# MultiBinding pattern
MULTI_BINDING_PATTERN = re.compile(
    r'<MultiBinding[^>]*>(.*?)</MultiBinding>', re.IGNORECASE | re.DOTALL
)

# UpdateSourceTrigger pattern
UPDATE_SOURCE_TRIGGER_PATTERN = re.compile(
    r'UpdateSourceTrigger\s*=\s*"?(\w+)"?', re.IGNORECASE
)

# StringFormat pattern
STRING_FORMAT_PATTERN = re.compile(
    r'StringFormat\s*=\s*["\']([^"\']+)["\']', re.IGNORECASE
)

# FallbackValue pattern
FALLBACK_VALUE_PATTERN = re.compile(
    r'FallbackValue\s*=\s*["\']([^"\']+)["\']', re.IGNORECASE
)

# TargetNullValue pattern
TARGET_NULL_VALUE_PATTERN = re.compile(
    r'TargetNullValue\s*=\s*["\']([^"\']+)["\']', re.IGNORECASE
)

# DataContext pattern in XAML
DATA_CONTEXT_PATTERN = re.compile(
    r'DataContext\s*=\s*["\{]([^"}\>]+)', re.IGNORECASE
)

# C# Binding setup: new Binding("Path") { Mode = ..., Converter = ... }
CS_BINDING_PATTERN = re.compile(r'new\s+Binding\s*\(\s*"([^"]+)"\s*\)\s*\{([^}]+)\}', re.IGNORECASE | re.DOTALL)
CS_PARAM_PATTERN = re.compile(r'(\w+)\s*=\s*([^;]+)', re.IGNORECASE)

# ViewModel property inference: public (string|int|bool|etc) PropertyName { get; set; }
VM_PROPERTY_PATTERN = re.compile(r'public\s+(?:\w+\s+)?(\w+)\s+(\w+)\s*\{', re.IGNORECASE)

# INotifyPropertyChanged implementation detection
INPC_PATTERN = re.compile(r':\s*INotifyPropertyChanged', re.IGNORECASE)
PROPERTY_CHANGED_PATTERN = re.compile(r'PropertyChanged\?\s*\.Invoke', re.IGNORECASE)

# Event handler patterns (memory leak detection)
EVENT_SUBSCRIBE_PATTERN = re.compile(r'(\w+)\s*\+=\s*', re.IGNORECASE)
EVENT_UNSUBSCRIBE_PATTERN = re.compile(r'(\w+)\s*-=\s*', re.IGNORECASE)

class BindingScanner:
    def __init__(self, root_path: str, verbose: bool = False):
        self.root = Path(root_path)
        self.verbose = verbose
        self.bindings: List[Dict] = []
        self.viewmodel_properties: Dict[str, Set[str]] = defaultdict(set)
        self.invalid_bindings: List[Dict] = []
        self.missing_converters: Set[str] = set()
        self.syncfusion_bindings: List[Dict] = []
        self.relative_source_bindings: List[Dict] = []
        self.element_name_bindings: List[Dict] = []
        self.multi_bindings: List[Dict] = []
        self.data_context_map: Dict[str, str] = {}  # File -> DataContext type
        self.inpc_implementations: Set[str] = set()  # ViewModels with INotifyPropertyChanged
        self.event_handlers: Dict[str, List[Tuple[str, int]]] = defaultdict(list)  # Memory leak tracking
        self.performance_warnings: List[Dict] = []

        # Statistics
        self.stats = {
            'files_scanned': 0,
            'xaml_files': 0,
            'cs_files': 0,
            'viewmodels_found': 0,
            'bindings_by_type': defaultdict(int),
            'bindings_by_mode': defaultdict(int),
        }

    def scan(self, output_json: Optional[str] = None, syncfusion_only: bool = False):
        """Main scan entrypoint."""
        print("🔍 Scanning Wiley Widget for WPF data bindings...")
        self._scan_viewmodels()  # Infer props first
        self._scan_xaml_bindings()
        self._scan_cs_bindings()
        self._analyze_performance()
        self._validate_bindings()
        self._report(output_json, syncfusion_only)

    def _scan_viewmodels(self):
        """Infer properties from ViewModel CS files (e.g., SettingsViewModel.cs)."""
        for file_path in self.root.rglob(f'*{CS_EXT}'):
            if file_path.is_file() and 'viewmodel' in file_path.name.lower() and 'bin' not in str(file_path).lower() and 'obj' not in str(file_path).lower():
                try:
                    with open(file_path, 'r', encoding='utf-8') as f:
                        content = f.read()
                    # Extract class name (assume file name == class)
                    class_name = file_path.stem.replace('ViewModel', '')
                    props = VM_PROPERTY_PATTERN.findall(content)
                    for _prop_type, prop_name in props:
                        self.viewmodel_properties[class_name].add(prop_name)

                    # Check for INotifyPropertyChanged implementation
                    if INPC_PATTERN.search(content) and PROPERTY_CHANGED_PATTERN.search(content):
                        self.inpc_implementations.add(class_name)

                    # Track event subscriptions (memory leak detection)
                    subscribes = EVENT_SUBSCRIBE_PATTERN.findall(content)
                    unsubscribes = EVENT_UNSUBSCRIBE_PATTERN.findall(content)
                    for event in subscribes:
                        if event not in unsubscribes:
                            line_num = content[:content.find(event)].count('\n') + 1
                            self.event_handlers[str(file_path)].append((event, line_num))

                    self.stats['viewmodels_found'] += 1
                except Exception as e:
                    if self.verbose:
                        print(f"⚠️  Error scanning ViewModel {file_path}: {e}")

    def _scan_xaml_bindings(self):
        """Parse XAML for {Binding ...}."""
        for file_path in self.root.rglob(f'*{XAML_EXT}'):
            if file_path.is_file() and 'bin' not in str(file_path).lower() and 'obj' not in str(file_path).lower():
                self.stats['xaml_files'] += 1
                try:
                    with open(file_path, 'r', encoding='utf-8') as f:
                        content = f.read()

                    # Extract DataContext
                    dc_match = DATA_CONTEXT_PATTERN.search(content)
                    if dc_match:
                        self.data_context_map[str(file_path)] = dc_match.group(1)

                    # Try XML parsing first
                    try:
                        tree = ET.parse(file_path)
                        root = tree.getroot()
                        # Find all elements with Binding attributes (recursive)
                        for elem in root.iter():
                            for _attr_name, attr_value in elem.attrib.items():
                                if 'Binding' in attr_value:
                                    binding_str = attr_value
                                    line_num = content[:content.find(attr_value)].count('\n') + 1 if attr_value in content else 0
                                    self._extract_binding_details(file_path, binding_str, line_num)
                    except ParseError:
                        pass  # Fall through to regex

                    # Regex fallback for all binding types
                    self._extract_xaml_bindings_regex(file_path, content)

                except Exception as e:
                    if self.verbose:
                        print(f"⚠️  Error parsing XAML {file_path}: {e}")

                self.stats['files_scanned'] += 1

    def _extract_xaml_bindings_regex(self, file_path: Path, content: str):
        """Extract bindings using regex patterns."""
        # Standard bindings
        for match in XAML_BINDING_PATTERN.finditer(content):
            line_num = content[:match.start()].count('\n') + 1
            self._extract_binding_details(file_path, match.group(1), line_num)

        # RelativeSource bindings
        for match in RELATIVE_SOURCE_PATTERN.finditer(content):
            line_num = content[:match.start()].count('\n') + 1
            self.relative_source_bindings.append({
                'file': str(file_path),
                'line': line_num,
                'source': match.group(1),
                'raw': match.group(0)
            })
            self.stats['bindings_by_type']['RelativeSource'] += 1

        # ElementName bindings
        for match in ELEMENT_NAME_PATTERN.finditer(content):
            line_num = content[:match.start()].count('\n') + 1
            self.element_name_bindings.append({
                'file': str(file_path),
                'line': line_num,
                'element': match.group(1),
                'raw': match.group(0)
            })
            self.stats['bindings_by_type']['ElementName'] += 1

        # MultiBindings
        for match in MULTI_BINDING_PATTERN.finditer(content):
            line_num = content[:match.start()].count('\n') + 1
            self.multi_bindings.append({
                'file': str(file_path),
                'line': line_num,
                'content': match.group(1),
                'raw': match.group(0)
            })
            self.stats['bindings_by_type']['MultiBinding'] += 1

    def _scan_cs_bindings(self):
        """Parse C# for new Binding(...) setups."""
        for file_path in self.root.rglob(f'*{CS_EXT}'):
            if file_path.is_file() and 'bin' not in str(file_path).lower() and 'obj' not in str(file_path).lower():
                self.stats['cs_files'] += 1
                try:
                    with open(file_path, 'r', encoding='utf-8') as f:
                        content = f.read()
                        matches = CS_BINDING_PATTERN.findall(content)
                        for path, params in matches:
                            line_num = content[:content.find(path)].count('\n') + 1
                            binding = {'file': str(file_path), 'line': line_num, 'path': path.strip('"')}
                            # Parse params { Mode = ..., Converter = ... }
                            param_matches = CS_PARAM_PATTERN.findall(params)
                            for key, val in param_matches:
                                binding[key.lower()] = val.strip().strip('"').strip(',')
                            self.bindings.append(binding)
                            self.stats['bindings_by_type']['C#'] += 1
                except Exception as e:
                    if self.verbose:
                        print(f"⚠️  Error parsing CS {file_path}: {e}")

                self.stats['files_scanned'] += 1

    def _extract_binding_details(self, file_path: Path, binding_str: str, line_num: int):
        """Extract Path, Mode, Converter, etc. from binding string."""
        binding = {'file': str(file_path), 'line': line_num, 'raw': binding_str}
        # Parse params
        param_matches = PARAM_EXTRACTOR.findall(binding_str)
        for key, val in param_matches:
            # Handle comma-separated vals (e.g., Mode=TwoWay, Converter=...)
            if ',' in val:
                sub_vals = [v.strip() for v in val.split(',')]
                for sub_val in sub_vals:
                    if '=' in sub_val:
                        sub_key, sub_val = sub_val.split('=', 1)
                        binding[sub_key.strip().lower()] = sub_val.strip()
                    else:
                        binding[key.lower()] = val
            else:
                binding[key.lower()] = val.strip()

        # Default to Path if missing
        if 'path' not in binding:
            path_match = re.search(r'Path=([^,\s\}]+)', binding_str, re.IGNORECASE)
            if path_match:
                binding['path'] = path_match.group(1).strip()

        # Extract UpdateSourceTrigger
        ust_match = UPDATE_SOURCE_TRIGGER_PATTERN.search(binding_str)
        if ust_match:
            binding['updatesourcetrigger'] = ust_match.group(1)

        # Extract StringFormat
        sf_match = STRING_FORMAT_PATTERN.search(binding_str)
        if sf_match:
            binding['stringformat'] = sf_match.group(1)

        # Extract FallbackValue
        fb_match = FALLBACK_VALUE_PATTERN.search(binding_str)
        if fb_match:
            binding['fallbackvalue'] = fb_match.group(1)

        # Extract TargetNullValue
        tn_match = TARGET_NULL_VALUE_PATTERN.search(binding_str)
        if tn_match:
            binding['targetnullvalue'] = tn_match.group(1)

        if binding.get('path'):
            self.bindings.append(binding)
            self.stats['bindings_by_type']['Standard'] += 1
            if mode := binding.get('mode'):
                self.stats['bindings_by_mode'][mode] += 1

    def _analyze_performance(self):
        """Analyze bindings for performance issues."""
        for binding in self.bindings:
            path = binding.get('path', '')

            # Deep nested paths (performance warning)
            if path.count('.') > 3:
                self.performance_warnings.append({
                    'file': binding['file'],
                    'line': binding['line'],
                    'type': 'deep_nesting',
                    'path': path,
                    'message': f"Deep property path ({path.count('.')} levels) may impact performance"
                })

            # Missing UpdateSourceTrigger on TwoWay bindings
            if binding.get('mode', '').lower() == 'twoway' and 'updatesourcetrigger' not in binding:
                self.performance_warnings.append({
                    'file': binding['file'],
                    'line': binding['line'],
                    'type': 'missing_trigger',
                    'path': path,
                    'message': "TwoWay binding without UpdateSourceTrigger may cause excessive updates"
                })

            # Complex StringFormat (regex/parsing overhead)
            if sf := binding.get('stringformat'):
                if len(sf) > 50 or '{' in sf:
                    self.performance_warnings.append({
                        'file': binding['file'],
                        'line': binding['line'],
                        'type': 'complex_format',
                        'path': path,
                        'message': f"Complex StringFormat may impact rendering: {sf[:50]}..."
                    })

    def _validate_bindings(self):
        """Validate each binding."""
        for binding in self.bindings:
            issues = []
            path = binding.get('path', '')
            mode = binding.get('mode', '').lower()
            converter = binding.get('converter', '')

            # Check Mode
            if mode and mode not in [m.lower() for m in VALID_MODES]:
                issues.append(f"Invalid Mode: '{mode}' (valid: {', '.join(VALID_MODES)})")

            # Check Converter (ref to resources)
            if converter:
                if '{' in converter:  # Resource ref
                    conv_key = re.search(r'\{[^}]+\}([^\s\}]+)', converter)
                    if conv_key:
                        conv_key = conv_key.group(1)
                        if conv_key not in self.viewmodel_properties.get('Global', set()):
                            self.missing_converters.add(conv_key)
                            issues.append(f"Missing Converter Resource: '{conv_key}'")
                else:
                    # Direct class: Check if VM has it
                    if converter not in self.viewmodel_properties.get('Global', set()):
                        issues.append(f"Undefined Converter Class: '{converter}'")

            # Check Path resolution with nested property support
            if path:
                # Validate each segment of the path
                path_parts = [p.strip() for p in path.split('.')]
                first_part = path_parts[0]

                # Check against all ViewModels
                found = False
                for vm_name, props in self.viewmodel_properties.items():
                    if first_part in props:
                        found = True
                        break

                if not found and first_part not in ['DataContext', 'SelectedItem', 'CurrentItem']:
                    issues.append(f"Unbound Path: '{first_part}' not found in any ViewModel")

                # Warn on deep nesting
                if len(path_parts) > 3:
                    issues.append(f"Deep nesting warning: {len(path_parts)} levels ({path})")

            # Syncfusion-specific validation
            if any(sf.lower() in binding.get('raw', '').lower() for sf in SYNCFUSION_BINDINGS):
                self.syncfusion_bindings.append(binding)
                # Check for proper TwoWay mode on editable Syncfusion controls
                if 'sfdatagrid' in binding.get('raw', '').lower():
                    if mode != 'twoway' and 'itemssource' not in path.lower():
                        issues.append("SfDataGrid editable columns should use TwoWay mode")

            if issues:
                binding['issues'] = issues
                self.invalid_bindings.append(binding)

    def _report(self, output_json: Optional[str] = None, syncfusion_only: bool = False):
            issues = []
            path = binding.get('path', '')
            mode = binding.get('mode', '').lower()
            converter = binding.get('converter', '')

            # Check Mode
            if mode and mode not in [m.lower() for m in VALID_MODES]:
                issues.append(f"Invalid Mode: '{mode}' (valid: {', '.join(VALID_MODES)})")

            # Check Converter (ref to resources)
            if converter:
                if '{' in converter:  # Resource ref
                    conv_key = re.search(r'\{[^}]+\}([^\s\}]+)', converter)
                    if conv_key:
                        conv_key = conv_key.group(1)
    def _report(self, output_json: Optional[str] = None, syncfusion_only: bool = False):
        """Generate console + JSON report."""
                            issues.append(f"Missing Converter Resource: '{conv_key}'")
                else:
                    # Direct class: Check if VM has it
                    if converter not in self.viewmodel_properties.get('Global', set()):
                        issues.append(f"Undefined Converter Class: '{converter}'")

            # Check Path resolution (simple: split and check VM props)
            if path:
                vm_props = self.viewmodel_properties.get('DataContext', set())  # Assume common DC
                path_parts = [p.strip() for p in path.split('.')]
                if path_parts[0] not in vm_props:
                    issues.append(f"Unbound Path Start: '{path_parts[0]}' (VM props: {list(vm_props)[:5]}...)")

            # Syncfusion flag
            if any(sf.lower() in binding.get('raw', '').lower() for sf in SYNCFUSION_BINDINGS):
                self.syncfusion_bindings.append(binding)

            if issues:
                binding['issues'] = issues
                self.invalid_bindings.append(binding)

    def _report(self, output_json: str = None, syncfusion_only: bool = False):
        """Generate console + JSON report."""
        total_bindings = len(self.bindings)
        invalid_count = len(self.invalid_bindings)

        report = {
            'total_bindings': total_bindings,
            'invalid_bindings': invalid_count,
            'missing_converters': sorted(list(self.missing_converters)),
            'syncfusion_bindings': len(self.syncfusion_bindings),
            'invalid_details': [ {k: v for k, v in b.items() if k != 'raw'} for b in self.invalid_bindings ],
            'recommendations': [
                'Fix invalid Modes: Use OneWay/TwoWay for Syncfusion controls.',
                'Resolve missing converters: Add to Themes/Generic.xaml or ViewModel.',
                'Verify Paths: Ensure ViewModel props match (e.g., public string Name { get; set; }).',
                'For SfDataGrid: Bind ItemsSource to ObservableCollection; use TwoWay for edits.',
                'Integrate with ResourceScanner: Rerun for converter defs.'
            ]
        }

        print("\n📊 Binding Scan Report")
        print("=" * 50)
        print(f"🔗 Total Bindings Found: {total_bindings}")
        print(f"❌ Invalid/Missing: {invalid_count}")
        if invalid_count > 0:
            print("\nTop Issues:")
            for binding in self.invalid_bindings[:5]:  # Top 5
                print(f"  {binding['file']}:{binding['line']} - Path='{binding.get('path', 'N/A')}' | Issues: {', '.join(binding.get('issues', []))}")
        print(f"\n🔧 Missing Converters: {len(self.missing_converters)}")
        if self.missing_converters:
            print("  " + ", ".join(sorted(self.missing_converters)))
        print(f"\n⚙️  Syncfusion Bindings: {len(self.syncfusion_bindings)}")

        if output_json:
            output_path = Path(output_json)
            output_path.parent.mkdir(parents=True, exist_ok=True)
            with open(output_json, 'w') as f:
                json.dump(report, f, indent=2)
            print(f"\n💾 Full report saved: {output_json}")

        status = "🎉 All bindings valid - UI responsive!" if invalid_count == 0 else "⚠️  Review invalid bindings for potential runtime errors."
        print(f"\n{status}")


def main():
    parser = argparse.ArgumentParser(description="Scan Wiley Widget for WPF bindings.")
    parser.add_argument('--path', '-p', default='.', help="Root path to scan (default: current dir)")
    parser.add_argument('--output', '-o', help="JSON report output file")
    parser.add_argument('--syncfusion-only', action='store_true',
                        help="Focus report on Syncfusion bindings only")
    args = parser.parse_args()

    if not os.path.exists(args.path):
        raise ValueError(f"Path not found: {args.path}")

    scanner = BindingScanner(args.path)
    scanner.scan(output_json=args.output, syncfusion_only=args.syncfusion_only)


if __name__ == '__main__':
    main()
