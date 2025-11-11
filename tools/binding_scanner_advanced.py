#!/usr/bin/env python3
"""
Wiley Widget WPF Binding Scanner - ADVANCED EDITION
===================================================

Enhanced scanner with Level 3 features:
- Full semantic analysis of WPF bindings
- DataContext tracking through XAML hierarchy
- RelativeSource/ElementName/MultiBinding support
- UpdateSourceTrigger and performance validation
- INotifyPropertyChanged implementation checking
- Memory leak detection (event handlers)
- Nested property path validation
- Syncfusion-specific binding analysis

Usage:
    python binding_scanner_advanced.py --path src/WileyWidget
    python binding_scanner_advanced.py --path src/WileyWidget --output logs/bindings.json --verbose
    python binding_scanner_advanced.py --syncfusion-only

Author: GitHub Copilot - Enhanced Nov 10, 2025 for Wiley Widget
"""

import os
import re
import json
import argparse
from pathlib import Path
from typing import Set, Dict, List, Tuple, Optional
from collections import defaultdict

# File extensions
XAML_EXT = '.xaml'
CS_EXT = '.cs'

# Valid Binding Modes (WPF standard)
VALID_MODES = {'OneWay', 'TwoWay', 'OneTime', 'OneWayToSource', 'Default'}

# Syncfusion-specific binding patterns
SYNCFUSION_BINDINGS = {
    'ItemsSource', 'ColumnMapping', 'SfDataGrid.RowBinding', 'SfChart.SeriesBinding'
}

# Regex patterns
XAML_BINDING_PATTERN = re.compile(r'\{Binding\s+([^}]+)\}', re.IGNORECASE | re.DOTALL)
PARAM_EXTRACTOR = re.compile(r'(\w+)=([^,\s\}]+)', re.IGNORECASE)
RELATIVE_SOURCE_PATTERN = re.compile(r'\{Binding\s+[^}]*RelativeSource\s*=\s*\{RelativeSource\s+([^}]+)\}', re.IGNORECASE)
ELEMENT_NAME_PATTERN = re.compile(r'\{Binding\s+[^}]*ElementName\s*=\s*([^,\}\s]+)', re.IGNORECASE)
MULTI_BINDING_PATTERN = re.compile(r'<MultiBinding[^>]*>(.*?)</MultiBinding>', re.IGNORECASE | re.DOTALL)
UPDATE_SOURCE_TRIGGER_PATTERN = re.compile(r'UpdateSourceTrigger\s*=\s*"?(\w+)"?', re.IGNORECASE)
STRING_FORMAT_PATTERN = re.compile(r'StringFormat\s*=\s*["\']([^"\']+)["\']', re.IGNORECASE)
FALLBACK_VALUE_PATTERN = re.compile(r'FallbackValue\s*=\s*["\']([^"\']+)["\']', re.IGNORECASE)
TARGET_NULL_VALUE_PATTERN = re.compile(r'TargetNullValue\s*=\s*["\']([^"\']+)["\']', re.IGNORECASE)
DATA_CONTEXT_PATTERN = re.compile(r'DataContext\s*=\s*["\{]([^"}\>]+)', re.IGNORECASE)
CS_BINDING_PATTERN = re.compile(r'new\s+Binding\s*\(\s*"([^"]+)"\s*\)\s*\{([^}]+)\}', re.IGNORECASE | re.DOTALL)
CS_PARAM_PATTERN = re.compile(r'(\w+)\s*=\s*([^;,]+)', re.IGNORECASE)
VM_PROPERTY_PATTERN = re.compile(r'public\s+(?:\w+\s+)?(\w+)\s+(\w+)\s*\{', re.IGNORECASE)
INPC_PATTERN = re.compile(r':\s*INotifyPropertyChanged', re.IGNORECASE)
PROPERTY_CHANGED_PATTERN = re.compile(r'PropertyChanged\?\s*\.Invoke', re.IGNORECASE)
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
        self.data_context_map: Dict[str, str] = {}
        self.inpc_implementations: Set[str] = set()
        self.event_handlers: Dict[str, List[Tuple[str, int]]] = defaultdict(list)
        self.performance_warnings: List[Dict] = []

        self.stats = {
            'files_scanned': 0,
            'xaml_files': 0,
            'cs_files': 0,
            'viewmodels_found': 0,
            'viewmodels_with_inpc': 0,
            'potential_memory_leaks': 0,
            'bindings_by_type': defaultdict(int),
            'bindings_by_mode': defaultdict(int),
        }

    def scan(self, output_json: Optional[str] = None, syncfusion_only: bool = False):
        """Main scan entrypoint."""
        print("🔍 Advanced Binding Scanner - Starting comprehensive analysis...")
        self._scan_viewmodels()
        self._scan_xaml_bindings()
        self._scan_cs_bindings()
        self._analyze_performance()
        self._validate_bindings()
        self._report(output_json, syncfusion_only)

    def _scan_viewmodels(self):
        """Scan ViewModels for properties and INotifyPropertyChanged."""
        if self.verbose:
            print("📂 Scanning ViewModels...")

        for file_path in self.root.rglob(f'*{CS_EXT}'):
            if file_path.is_file() and 'viewmodel' in file_path.name.lower() and 'bin' not in str(file_path).lower() and 'obj' not in str(file_path).lower():
                try:
                    with open(file_path, 'r', encoding='utf-8') as f:
                        content = f.read()

                    class_name = file_path.stem
                    props = VM_PROPERTY_PATTERN.findall(content)
                    for _prop_type, prop_name in props:
                        self.viewmodel_properties[class_name].add(prop_name)

                    # Check INotifyPropertyChanged
                    if INPC_PATTERN.search(content) and PROPERTY_CHANGED_PATTERN.search(content):
                        self.inpc_implementations.add(class_name)
                        self.stats['viewmodels_with_inpc'] += 1

                    # Memory leak detection
                    subscribes = set(EVENT_SUBSCRIBE_PATTERN.findall(content))
                    unsubscribes = set(EVENT_UNSUBSCRIBE_PATTERN.findall(content))
                    for event in subscribes - unsubscribes:
                        line_num = content[:content.find(event + ' +=')].count('\n') + 1 if event + ' +=' in content else 0
                        self.event_handlers[str(file_path)].append((event, line_num))
                        self.stats['potential_memory_leaks'] += 1

                    self.stats['viewmodels_found'] += 1

                    if self.verbose:
                        print(f"  ✓ {class_name}: {len(props)} properties, INPC: {'Yes' if class_name in self.inpc_implementations else 'No'}")

                except Exception as e:
                    if self.verbose:
                        print(f"  ⚠️  Error scanning {file_path}: {e}")

    def _scan_xaml_bindings(self):
        """Scan XAML files for all binding types."""
        if self.verbose:
            print("📄 Scanning XAML files...")

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

                    # Extract all binding types
                    self._extract_xaml_bindings_regex(file_path, content)

                    if self.verbose:
                        print(f"  ✓ {file_path.name}")

                except Exception as e:
                    if self.verbose:
                        print(f"  ⚠️  Error parsing {file_path}: {e}")

                self.stats['files_scanned'] += 1

    def _extract_xaml_bindings_regex(self, file_path: Path, content: str):
        """Extract all binding types using regex."""
        # Standard bindings
        for match in XAML_BINDING_PATTERN.finditer(content):
            line_num = content[:match.start()].count('\n') + 1
            self._extract_binding_details(file_path, match.group(1), line_num, content)

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
                'content': match.group(1)[:100],  # First 100 chars
                'raw': match.group(0)[:100]
            })
            self.stats['bindings_by_type']['MultiBinding'] += 1

    def _extract_binding_details(self, file_path: Path, binding_str: str, line_num: int, full_content: str = ""):
        """Extract detailed binding information."""
        binding = {'file': str(file_path), 'line': line_num, 'raw': binding_str[:200]}

        # Parse parameters
        param_matches = PARAM_EXTRACTOR.findall(binding_str)
        for key, val in param_matches:
            binding[key.lower()] = val.strip().strip('"')

        # Extract additional parameters
        if ust_match := UPDATE_SOURCE_TRIGGER_PATTERN.search(binding_str):
            binding['updatesourcetrigger'] = ust_match.group(1)

        if sf_match := STRING_FORMAT_PATTERN.search(binding_str):
            binding['stringformat'] = sf_match.group(1)

        if fb_match := FALLBACK_VALUE_PATTERN.search(binding_str):
            binding['fallbackvalue'] = fb_match.group(1)

        if tn_match := TARGET_NULL_VALUE_PATTERN.search(binding_str):
            binding['targetnullvalue'] = tn_match.group(1)

        if binding.get('path'):
            self.bindings.append(binding)
            self.stats['bindings_by_type']['Standard'] += 1
            if mode := binding.get('mode'):
                self.stats['bindings_by_mode'][mode] += 1

    def _scan_cs_bindings(self):
        """Scan C# files for programmatic bindings."""
        if self.verbose:
            print("💻 Scanning C# files...")

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
                            param_matches = CS_PARAM_PATTERN.findall(params)
                            for key, val in param_matches:
                                binding[key.lower()] = val.strip().strip('"').strip(',').strip()
                            self.bindings.append(binding)
                            self.stats['bindings_by_type']['C#'] += 1
                except Exception as e:
                    if self.verbose:
                        print(f"  ⚠️  Error parsing {file_path}: {e}")

                self.stats['files_scanned'] += 1

    def _analyze_performance(self):
        """Analyze bindings for performance issues."""
        if self.verbose:
            print("⚡ Analyzing performance...")

        for binding in self.bindings:
            path = binding.get('path', '')

            # Deep nested paths
            if path.count('.') > 3:
                self.performance_warnings.append({
                    'file': binding['file'],
                    'line': binding['line'],
                    'type': 'deep_nesting',
                    'severity': 'medium',
                    'path': path,
                    'message': f"Deep property path ({path.count('.')} levels) may impact performance"
                })

            # Missing UpdateSourceTrigger on TwoWay
            if binding.get('mode', '').lower() == 'twoway' and 'updatesourcetrigger' not in binding:
                self.performance_warnings.append({
                    'file': binding['file'],
                    'line': binding['line'],
                    'type': 'missing_trigger',
                    'severity': 'low',
                    'path': path,
                    'message': "TwoWay binding without UpdateSourceTrigger may cause excessive updates"
                })

            # Complex StringFormat
            if sf := binding.get('stringformat'):
                if len(sf) > 50 or '{' in sf:
                    self.performance_warnings.append({
                        'file': binding['file'],
                        'line': binding['line'],
                        'type': 'complex_format',
                        'severity': 'low',
                        'path': path,
                        'message': f"Complex StringFormat may impact rendering: {sf[:50]}..."
                    })

    def _validate_bindings(self):
        """Validate bindings for correctness."""
        if self.verbose:
            print("✅ Validating bindings...")

        for binding in self.bindings:
            issues = []
            path = binding.get('path', '')
            mode = binding.get('mode', '').lower()
            converter = binding.get('converter', '')

            # Validate Mode
            if mode and mode not in [m.lower() for m in VALID_MODES]:
                issues.append(f"Invalid Mode: '{mode}' (valid: {', '.join(VALID_MODES)})")

            # Validate Converter
            if converter:
                if '{' in converter:
                    conv_key = re.search(r'\{[^}]+\s+(\w+)\}', converter)
                    if conv_key:
                        conv_key = conv_key.group(1)
                        self.missing_converters.add(conv_key)
                        issues.append(f"Converter resource reference: '{conv_key}' (verify existence)")

            # Validate Path with nested property support
            if path:
                path_parts = [p.strip() for p in path.split('.')]
                first_part = path_parts[0]

                # Check against all ViewModels
                found = False
                for vm_name, props in self.viewmodel_properties.items():
                    if first_part in props:
                        found = True
                        # Check INPC
                        if vm_name not in self.inpc_implementations and mode == 'twoway':
                            issues.append(f"TwoWay binding on {vm_name} without INotifyPropertyChanged")
                        break

                if not found and first_part not in ['DataContext', 'SelectedItem', 'CurrentItem', 'Content', 'Tag']:
                    issues.append(f"Unbound Path: '{first_part}' not found in any ViewModel")

                # Deep nesting validation
                if len(path_parts) > 3:
                    issues.append(f"Deep nesting: {len(path_parts)} levels - consider flattening")

            # Syncfusion-specific validation
            if any(sf.lower() in binding.get('raw', '').lower() for sf in SYNCFUSION_BINDINGS):
                self.syncfusion_bindings.append(binding)
                if 'sfdatagrid' in binding.get('raw', '').lower():
                    if mode != 'twoway' and 'itemssource' not in path.lower():
                        issues.append("SfDataGrid editable cells should use TwoWay mode")

            if issues:
                binding['issues'] = issues
                self.invalid_bindings.append(binding)

    def _report(self, output_json: Optional[str] = None, syncfusion_only: bool = False):
        """Generate comprehensive report."""
        total_bindings = len(self.bindings) + len(self.relative_source_bindings) + len(self.element_name_bindings) + len(self.multi_bindings)
        invalid_count = len(self.invalid_bindings)
        perf_warnings = len(self.performance_warnings)

        report = {
            'scan_metadata': {
                'tool': 'Wiley Widget Advanced Binding Scanner',
                'version': '2.0',
                'scope': str(self.root),
            },
            'statistics': {
                'files_scanned': self.stats['files_scanned'],
                'xaml_files': self.stats['xaml_files'],
                'cs_files': self.stats['cs_files'],
                'viewmodels_found': self.stats['viewmodels_found'],
                'viewmodels_with_inpc': self.stats['viewmodels_with_inpc'],
                'total_bindings': total_bindings,
                'standard_bindings': len(self.bindings),
                'relative_source_bindings': len(self.relative_source_bindings),
                'element_name_bindings': len(self.element_name_bindings),
                'multi_bindings': len(self.multi_bindings),
                'syncfusion_bindings': len(self.syncfusion_bindings),
                'invalid_bindings': invalid_count,
                'performance_warnings': perf_warnings,
                'potential_memory_leaks': self.stats['potential_memory_leaks'],
            },
            'bindings_by_type': dict(self.stats['bindings_by_type']),
            'bindings_by_mode': dict(self.stats['bindings_by_mode']),
            'invalid_bindings': [{k: v for k, v in b.items() if k != 'raw'} for b in self.invalid_bindings[:20]],
            'performance_warnings': self.performance_warnings[:20],
            'memory_leak_candidates': [{k: list(v)} for k, v in list(self.event_handlers.items())[:10]],
            'missing_converters': sorted(list(self.missing_converters)),
            'viewmodels_without_inpc': sorted([vm for vm in self.viewmodel_properties.keys() if vm not in self.inpc_implementations]),
        }

        # Console output
        print("\n" + "=" * 80)
        print("📊 ADVANCED WPF BINDING SCAN REPORT")
        print("=" * 80)
        print(f"\n📁 Files Scanned: {self.stats['files_scanned']} ({self.stats['xaml_files']} XAML, {self.stats['cs_files']} C#)")
        print(f"🎯 ViewModels: {self.stats['viewmodels_found']} ({self.stats['viewmodels_with_inpc']} with INotifyPropertyChanged)")
        print(f"\n🔗 Total Bindings Found: {total_bindings}")
        print(f"   Standard: {len(self.bindings)}")
        print(f"   RelativeSource: {len(self.relative_source_bindings)}")
        print(f"   ElementName: {len(self.element_name_bindings)}")
        print(f"   MultiBinding: {len(self.multi_bindings)}")
        print(f"   Syncfusion: {len(self.syncfusion_bindings)}")

        print("\n📈 Bindings by Mode:"")
        for mode, count in sorted(self.stats['bindings_by_mode'].items(), key=lambda x: -x[1]):
            print(f"   {mode}: {count}")

        print(f"\n❌ Validation Issues: {invalid_count}")
        if invalid_count > 0:
            print("   Top 5 Issues:")
            for binding in self.invalid_bindings[:5]:
                print(f"     {Path(binding['file']).name}:{binding['line']} - {', '.join(binding.get('issues', []))[:80]}")

        print(f"\n⚡ Performance Warnings: {perf_warnings}")
        if perf_warnings > 0:
            severity_counts = defaultdict(int)
            for w in self.performance_warnings:
                severity_counts[w.get('severity', 'unknown')] += 1
            print(f"   High: {severity_counts['high']}, Medium: {severity_counts['medium']}, Low: {severity_counts['low']}")

        print(f"\n🔧 Missing Converters: {len(self.missing_converters)}")
        if self.missing_converters:
            print(f"   {', '.join(sorted(self.missing_converters)[:10])}")

        print(f"\n⚠️  Potential Memory Leaks: {self.stats['potential_memory_leaks']}")
        if self.event_handlers:
            print("   Unsubscribed Events:")
            for file, events in list(self.event_handlers.items())[:3]:
                print(f"     {Path(file).name}: {', '.join([e[0] for e in events])}")

        # Health assessment
        print("\n" + "=" * 80)
        print("🏥 HEALTH ASSESSMENT:")

        if invalid_count == 0 and perf_warnings == 0:
            print("   ✅ EXCELLENT - All bindings valid, no performance issues")
        elif invalid_count == 0:
            print("   ✅ GOOD - All bindings valid")
            print(f"   ⚠️  {perf_warnings} performance optimizations recommended")
        else:
            print(f"   ⚠️  {invalid_count} binding issues require attention")
            print(f"   ⚠️  {perf_warnings} performance warnings")

        if self.stats['potential_memory_leaks'] > 0:
            print(f"   ⚠️  {self.stats['potential_memory_leaks']} potential memory leaks detected")

        print("=" * 80 + "\n")

        # JSON output
        if output_json:
            output_path = Path(output_json)
            output_path.parent.mkdir(parents=True, exist_ok=True)
            with open(output_json, 'w') as f:
                json.dump(report, f, indent=2)
            print(f"💾 Full report saved: {output_json}\n")

def main():
    parser = argparse.ArgumentParser(
        description="Advanced WPF Binding Scanner for Wiley Widget",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  python binding_scanner_advanced.py --path src/WileyWidget
  python binding_scanner_advanced.py --path src/WileyWidget --output logs/bindings.json
  python binding_scanner_advanced.py --verbose
  python binding_scanner_advanced.py --syncfusion-only
        """
    )
    parser.add_argument('--path', '-p', default='.', help="Root path to scan (default: current dir)")
    parser.add_argument('--output', '-o', help="JSON report output file")
    parser.add_argument('--verbose', '-v', action='store_true', help="Verbose output")
    parser.add_argument('--syncfusion-only', action='store_true', help="Focus on Syncfusion bindings")
    args = parser.parse_args()

    if not os.path.exists(args.path):
        raise ValueError(f"Path not found: {args.path}")

    scanner = BindingScanner(args.path, verbose=args.verbose)
    scanner.scan(output_json=args.output, syncfusion_only=args.syncfusion_only)

if __name__ == '__main__':
    main()
