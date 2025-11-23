#!/usr/bin/env python3
"""
Wiley Widget Startup Validator - UNIFIED E2E SCANNER
====================================================

Comprehensive startup validation targeting Syncfusion/WPF/Prism integration issues
that can block initialization or cause runtime failures. Prioritized by impact:

HIGH PRIORITY (Startup Blockers):
1. License Key Registrations - Syncfusion/Bold licensing
2. Assembly References - Missing/outdated Syncfusion DLLs
3. Merged Resource Dictionaries - Theme/resource conflicts

MEDIUM PRIORITY (Perf/UI Issues):
4. Control-Specific Integrations - SfDataGrid/SfChart setup
5. Prism Module & DI Registrations - Service/view registrations

LOW PRIORITY (Polish):
6. Classic vs Non-Classic Controls - Deprecated control detection
7. API Reference Compliance - Proper API usage validation

Usage:
    python startup_validator.py --scan all
    python startup_validator.py --scan licenses,assemblies
    python startup_validator.py --path src/WileyWidget --output logs/startup_report.json
    python startup_validator.py --verbose

Integration with CI:
    assert report['blocking_issues'] == 0

Author: GitHub Copilot - Nov 10, 2025 for Wiley Widget
References: Syncfusion Essential Studio WPF Documentation
"""

import argparse
import json
import logging
import os
import re
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Dict, List, Optional

# ============================================================================
# PYTHON ENVIRONMENT AUTO-DETECTION
# ============================================================================


def find_python_executable() -> Optional[str]:
    """
    Auto-detect Python executable on Windows.
    Checks multiple common locations and returns the first valid Python found.
    """
    # Common Python locations on Windows
    python_candidates = [
        # WindowsApps Python launcher
        os.path.expandvars(
            r"%USERPROFILE%\AppData\Local\Microsoft\WindowsApps\python3.11.exe"
        ),
        os.path.expandvars(
            r"%USERPROFILE%\AppData\Local\Microsoft\WindowsApps\python3.exe"
        ),
        os.path.expandvars(
            r"%USERPROFILE%\AppData\Local\Microsoft\WindowsApps\python.exe"
        ),
        # Standard Python installations
        r"C:\Python311\python.exe",
        r"C:\Python310\python.exe",
        r"C:\Python39\python.exe",
        # Anaconda
        os.path.expandvars(r"%USERPROFILE%\anaconda3\python.exe"),
        os.path.expandvars(r"%USERPROFILE%\miniconda3\python.exe"),
        # System-wide
        shutil.which("python3"),
        shutil.which("python"),
        shutil.which("py"),
    ]

    for candidate in python_candidates:
        if candidate and os.path.exists(candidate):
            try:
                # Verify it's actually Python
                result = subprocess.run(
                    [candidate, "--version"], capture_output=True, text=True, timeout=5
                )
                if result.returncode == 0 and "Python" in result.stdout:
                    return candidate
            except (subprocess.TimeoutExpired, FileNotFoundError, PermissionError):
                continue

    return None


# Try to ensure we're running with a valid Python
if __name__ == "__main__":
    # If running directly, verify Python environment
    current_python = sys.executable

    # WindowsApps Python works fine, so only re-exec if we have a BETTER alternative
    # Don't re-exec if we're already running from WindowsApps and it's working
    if current_python and "WindowsApps" in current_python:
        # Already running with WindowsApps Python - this is acceptable
        pass
    elif not current_python:
        # No Python executable detected - try to find one
        better_python = find_python_executable()
        if better_python and better_python != current_python:
            print("⚠️  No Python executable detected")
            print(f"✓ Re-executing with: {better_python}")
            os.execv(better_python, [better_python] + sys.argv)

# Configure logging
logging.basicConfig(level=logging.INFO, format="%(levelname)s: %(message)s")

# Configure UTF-8 encoding for Windows console
if sys.platform == "win32":
    try:
        sys.stdout.reconfigure(encoding="utf-8")
        sys.stderr.reconfigure(encoding="utf-8")
    except AttributeError:
        # Python < 3.7 doesn't have reconfigure
        import codecs

        sys.stdout = codecs.getwriter("utf-8")(sys.stdout.detach())
        sys.stderr = codecs.getwriter("utf-8")(sys.stderr.detach())

# ============================================================================
# PATTERNS - LICENSE VALIDATION
# ============================================================================

# Syncfusion license registration patterns - matches both string literals and variables
SF_LICENSE_PATTERN = re.compile(
    r"(?:SyncfusionLicenseProvider|SfSkinManager|Syncfusion\.Licensing\.SyncfusionLicenseProvider)\.(?:RegisterLicense|SetLicenseKey)\s*\(\s*([^)]+?)\s*\)",
    re.IGNORECASE | re.DOTALL,
)

BOLD_LICENSE_PATTERN = re.compile(
    r"(?:BoldLicenseProvider|BoldLicensing|Bold\.Licensing\.BoldLicenseProvider)\.(?:RegisterLicense)\s*\(\s*([^)]+?)\s*\)",
    re.IGNORECASE | re.DOTALL,
)

# Environment variable patterns
ENV_VAR_ASSIGNMENT_PATTERN = re.compile(
    r'(?:var|string|const)\s+(\w+)\s*=\s*Environment\.GetEnvironmentVariable\s*\(\s*["\']([^"\']+)["\']',
    re.IGNORECASE,
)

# License key validation patterns
VALID_LICENSE_FORMAT = re.compile(r"^[A-Za-z0-9+/]{40,}={0,2}$")  # Base64-ish format
STRING_LITERAL_PATTERN = re.compile(r'^["\'](.+)["\']$')  # Detect string literals

# Placeholder detection
PLACEHOLDER_PATTERN = re.compile(r"\{[^}]*LicenseKey[^}]*\}", re.IGNORECASE)

# ============================================================================
# PATTERNS - ASSEMBLY VALIDATION
# ============================================================================

# Syncfusion package references in csproj
PACKAGE_REF_PATTERN = re.compile(
    r'<PackageReference\s+Include\s*=\s*"(Syncfusion\.[^"]+)"\s+Version\s*=\s*"([^"]+)"',
    re.IGNORECASE,
)

# XAML namespace declarations
XMLNS_SYNCFUSION_PATTERN = re.compile(
    r'xmlns:(\w+)\s*=\s*"clr-namespace:Syncfusion\.([^;"]+)', re.IGNORECASE
)

# Minimum required versions for Syncfusion packages
MIN_REQUIRED_VERSIONS = {
    # All Syncfusion WPF packages should be >= 27.1.48 for .NET 9 compatibility
    "default": (27, 1, 48),
    # Specific overrides if needed
    "Syncfusion.Licensing": (27, 1, 48),
}

# Version consistency rules - all Syncfusion packages should match
VERSION_CONSISTENCY_CHECK = True

# Known breaking changes between versions
BREAKING_CHANGES = {
    (26, 0, 0): "API changes in SfDataGrid, requires code migration",
    (27, 0, 0): ".NET 8+ required, legacy APIs removed",
    (28, 0, 0): "Theme system refactored, custom styles need updates",
}

# Assembly dependency requirements
REQUIRED_DEPENDENCIES = {
    "Syncfusion.SfGrid.WPF": ["Syncfusion.SfGridCommon.WPF", "Syncfusion.Data.WPF"],
    "Syncfusion.SfChart.WPF": ["Syncfusion.Data.WPF"],
    "Syncfusion.SfDataGrid.WPF": ["Syncfusion.SfGridCommon.WPF", "Syncfusion.Data.WPF"],
}

# License key validation patterns
VALID_LICENSE_FORMAT = re.compile(r"^[A-Za-z0-9+/]{40,}={0,2}$")  # Base64-ish format


def parse_version(version_str: str) -> tuple:
    """Parse version string to tuple of integers for comparison."""
    try:
        parts = version_str.split(".")
        return tuple(int(p) for p in parts[:3])  # Major, Minor, Patch
    except (ValueError, AttributeError):
        return (0, 0, 0)


def is_version_outdated(package_name: str, version_str: str) -> bool:
    """Check if package version is below minimum requirement."""
    current = parse_version(version_str)
    min_required = MIN_REQUIRED_VERSIONS.get(
        package_name, MIN_REQUIRED_VERSIONS["default"]
    )
    return current < min_required


def check_breaking_changes(version_str: str) -> list:
    """
    Check if current version has breaking changes.
    Only warns about the CURRENT major version's breaking changes, not all historical ones.
    This prevents false positives for projects already on newer versions.
    """
    current = parse_version(version_str)
    warnings = []

    # Find the breaking change version that applies to current version
    # Only warn about the breaking changes introduced IN the current major version
    current_major = current[0]

    for breaking_ver, message in BREAKING_CHANGES.items():
        breaking_major = breaking_ver[0]
        # Only include if the breaking change is in the same major version
        # This avoids warning about v26 changes when you're already on v31
        if breaking_major == current_major and current >= breaking_ver:
            warnings.append(
                {
                    "version": f"{breaking_ver[0]}.{breaking_ver[1]}.{breaking_ver[2]}",
                    "message": message,
                }
            )

    return warnings


# ============================================================================
# PATTERNS - RESOURCE DICTIONARY VALIDATION
# ============================================================================

# MergedDictionaries in XAML
MERGED_DICT_PATTERN = re.compile(
    r'<ResourceDictionary\s+Source\s*=\s*"([^"]+)"', re.IGNORECASE
)

# Required Syncfusion theme dictionaries
REQUIRED_SF_DICTS = [
    "SfSkinManager",
    "FluentLight",
    "FluentDark",
]

# ============================================================================
# PATTERNS - RUNTIME RESOURCE EVALUATION (HIGH PRIORITY - NEW)
# ============================================================================

# XAML resource key patterns
BRUSH_RESOURCE_PATTERN = re.compile(
    r'<(?:SolidColorBrush|LinearGradientBrush|RadialGradientBrush)\s+x:Key\s*=\s*"([^"]+)"',
    re.IGNORECASE,
)

STYLE_RESOURCE_PATTERN = re.compile(
    r'<Style\s+x:Key\s*=\s*"([^"]+)"(?:\s+TargetType\s*=\s*"(?:\{x:Type\s+)?([^}"]+)\}?")?',
    re.IGNORECASE,
)

STATIC_RESOURCE_REF_PATTERN = re.compile(
    r"\{(?:StaticResource|DynamicResource)\s+([^}]+)\}", re.IGNORECASE
)

DATA_TEMPLATE_PATTERN = re.compile(
    r'<DataTemplate\s+x:Key\s*=\s*"([^"]+)"(?:\s+DataType\s*=\s*"(?:\{x:Type\s+)?([^}"]+)\}?")?',
    re.IGNORECASE,
)

# FluentLight theme conflicts (conflicting brush names)
FLUENTLIGHT_RESERVED_BRUSHES = {
    "PrimaryBrush",
    "SecondaryBrush",
    "TertiaryBrush",
    "AccentBrush",
    "BackgroundBrush",
    "ForegroundBrush",
    "BorderBrush",
    "HoverBrush",
    "PressedBrush",
    "DisabledBrush",
    "SelectionBrush",
    "HighlightBrush",
}

# Syncfusion reserved style keys
SF_RESERVED_STYLE_KEYS = {
    "SfDataGridStyle",
    "SfChartStyle",
    "SfBusyIndicatorStyle",
    "SyncfusionAccentBrush",
    "SyncfusionBackground",
    "SyncfusionForeground",
}

# Resource dictionary merge order issues
MERGE_ORDER_CRITICAL = [
    "Syncfusion.Themes",  # Must be first
    "Syncfusion.SfSkinManager",  # Theme manager
    "FluentLight",  # Theme resources
    "DataTemplates",  # App-specific templates
    "Strings",  # String resources (should be last)
]

# Potential silent exit patterns
SILENT_EXIT_PATTERNS = [
    re.compile(
        r'Setter\s+Property\s*=\s*"[^"]*"\s+Value\s*=\s*"\{StaticResource\s+NonExistent',
        re.IGNORECASE,
    ),
    re.compile(r'Style\s+BasedOn\s*=\s*"\{StaticResource\s+([^}"]+)\}"', re.IGNORECASE),
    # Note: DataTemplate with x:Type is VALID - removed false positive pattern
]

# .NET 9 WPF dispatcher deadlock patterns
DISPATCHER_DEADLOCK_PATTERNS = [
    re.compile(r"Dispatcher\.Invoke\s*\(\s*\(\s*\)\s*=>", re.IGNORECASE),
    re.compile(r"Application\.Current\.Dispatcher\.Invoke", re.IGNORECASE),
    re.compile(
        r"await\s+Dispatcher\.InvokeAsync.*ConfigureAwait\s*\(\s*false\s*\)",
        re.IGNORECASE,
    ),
]

# ============================================================================
# PATTERNS - CONTROL INTEGRATION VALIDATION
# ============================================================================

# Syncfusion control declarations (legacy)
SF_CONTROL_PATTERN = re.compile(
    r"<(?:syncfusion|sf):(\w+)(?:\s+([^/>]+))?", re.IGNORECASE
)

# Native / WinUI control detection (new)
# Detects built-in DataGrid variants and WinUI/MUXC control namespaces
GENERIC_DATAGRID_PATTERN = re.compile(
    r"<(?:DataGrid|muxc:DataGrid|controls:DataGrid|winui:DataGrid|Microsoft\.UI\.Xaml\.Controls\.DataGrid)\b",
    re.IGNORECASE,
)

# Detect CommunityToolkit.Mvvm usage in ViewModels/commands
COMMUNITY_TOOLKIT_PATTERN = re.compile(
    r"CommunityToolkit\.Mvvm|ObservableObject|ObservableRecipient|AsyncRelayCommand|IAsyncRelayCommand|RelayCommand",
    re.IGNORECASE,
)

# Control property binding patterns (ENHANCED) for both Syncfusion and generic DataGrid
SF_CHART_BINDING_PATTERN = re.compile(
    r'<(?:syncfusion|sf):SfChart[^>]*(?:Series|ItemsSource)\s*=\s*["\']?\{Binding\s+([^}]+)\}',
    re.IGNORECASE,
)

SF_DATAGRID_BINDING_PATTERN = re.compile(
    r'<(?:syncfusion|sf):SfDataGrid[^>]*ItemsSource\s*=\s*["\']?\{Binding\s+([^}]+)\}',
    re.IGNORECASE,
)

# Required properties for specific controls
REQUIRED_CONTROL_PROPS = {
    "SfDataGrid": ["ItemsSource"],
    "SfChart": ["Series"],
    "SfBusyIndicator": ["IsBusy"],
}

# ============================================================================
# PATTERNS - PRISM/DI VALIDATION
# ============================================================================

# Container registrations
REGISTER_TYPE_PATTERN = re.compile(
    r"container\.RegisterType<([^,>]+),\s*([^>]+)>", re.IGNORECASE
)

# Region registrations
REGISTER_VIEW_PATTERN = re.compile(
    r'regionManager\.RegisterViewWithRegion\s*\(\s*"([^"]+)"\s*,\s*typeof\(([^)]+)\)',
    re.IGNORECASE,
)

# Module initialization
MODULE_INIT_PATTERN = re.compile(r"class\s+(\w+)\s*:\s*IModule", re.IGNORECASE)

# ============================================================================
# PATTERNS - DEPRECATED CONTROLS
# ============================================================================

CLASSIC_CONTROL_PATTERN = re.compile(
    r"(?:Classic|Legacy)(?:Sf|Syncfusion)(\w+)", re.IGNORECASE
)

# ============================================================================
# PATTERNS - CONFIGURATION VALIDATION (NEW)
# ============================================================================

CONNECTION_STRING_PATTERN = re.compile(
    r'"ConnectionStrings"\s*:\s*\{[^}]+\}', re.IGNORECASE | re.DOTALL
)

ENV_VAR_PLACEHOLDER_PATTERN = re.compile(r"\$\{([^}]+)\}")

# ============================================================================
# PATTERNS - SHELL/WINDOW VALIDATION (NEW)
# ============================================================================

REGION_DEFINITION_PATTERN = re.compile(
    r'(?:prism:RegionManager\.RegionName|RegionName)\s*=\s*["\']([^"\']+)["\']',
    re.IGNORECASE,
)

VIEWMODEL_LOCATOR_PATTERN = re.compile(
    r'prism:ViewModelLocator\.AutoWireViewModel\s*=\s*["\']?(True|False)["\']?',
    re.IGNORECASE,
)

# ============================================================================
# PATTERNS - SERVICE/VIEWMODEL REGISTRATION (NEW)
# ============================================================================

# DryIoc container registrations
REGISTER_SINGLETON_PATTERN = re.compile(
    r"container\.Register(?:Singleton)?<([^,>]+?)(?:,\s*([^>]+?))?>\s*\(", re.IGNORECASE
)

REGISTER_INSTANCE_PATTERN = re.compile(
    r"container\.RegisterInstance<([^>]+)>\s*\(", re.IGNORECASE
)

# ViewModel class pattern
VIEWMODEL_CLASS_PATTERN = re.compile(
    r"class\s+(\w+ViewModel)\s*:\s*(?:BindableBase|ViewModelBase|INotifyPropertyChanged|ObservableObject|ObservableRecipient)",
    re.IGNORECASE,
)

# ============================================================================
# PATTERNS - DATABASE VALIDATION (NEW)
# ============================================================================

DBCONTEXT_CLASS_PATTERN = re.compile(r"class\s+(\w+)\s*:\s*DbContext", re.IGNORECASE)

CONNECTION_STRING_USAGE_PATTERN = re.compile(
    r'(?:GetConnectionString|ConnectionStrings)\s*\(\s*["\']([^"\']+)["\']',
    re.IGNORECASE,
)

DATABASE_ENSURE_CREATED_PATTERN = re.compile(
    r"Database\.EnsureCreated|MigrateAsync|Migrate\(\)", re.IGNORECASE
)

# ============================================================================
# VALIDATOR CLASS
# ============================================================================


class StartupValidator:
    def __init__(self, root_path: str, verbose: bool = False):
        self.root = Path(root_path)
        self.verbose = verbose

        # Setup logger
        self.logger = logging.getLogger(__name__)
        if verbose:
            self.logger.setLevel(logging.DEBUG)
        else:
            self.logger.setLevel(logging.INFO)

        # Load centralized package versions if available
        self.central_package_versions = self._load_central_package_versions()

        # Results storage
        self.licenses = {
            "registrations": [],
            "env_fallbacks": [],
            "placeholders": [],
            "missing": [],
        }

        self.assemblies = {
            "packages": [],
            "outdated": [],
            "missing_refs": [],
            "xmlns_mismatches": [],
            "version_mismatches": [],  # NEW: Inconsistent versions across packages
            "missing_dependencies": [],  # NEW: Required dependency missing
            "breaking_changes": [],  # NEW: Version with breaking changes
        }

        self.resources = {
            "merged_dicts": [],
            "missing_required": [],
            "duplicate_sources": [],
        }

        # NEW: Runtime resource evaluation results (HIGH PRIORITY)
        self.resource_evals = {
            "xaml_files_parsed": [],
            "brush_conflicts": [],  # FluentLight conflicts
            "style_conflicts": [],  # Syncfusion style conflicts
            "missing_references": [],  # Unresolved StaticResource refs
            "merge_order_issues": [],  # Incorrect merge order
            "silent_exit_risks": [],  # Patterns that cause silent exits
            "dispatcher_deadlocks": [],  # .NET 9 dispatcher issues
            "data_template_issues": [],  # DataTemplate conflicts
        }

        self.controls = {"found": [], "misconfigured": [], "missing_props": []}

        self.prism = {
            "modules": [],
            "registrations": [],
            "missing_registrations": [],
            "issues": [],  # FIX: Change from dict to list for validation issues
        }

        self.deprecated = {"classic_controls": [], "legacy_apis": []}

        # NEW: Configuration validation
        self.configuration = {
            "files_found": [],
            "missing_files": [],
            "connection_strings": [],
            "env_vars_used": [],
            "missing_sections": [],
        }

        # NEW: Shell/Window validation
        self.shell = {
            "main_window_found": False,
            "regions_defined": [],
            "viewmodel_locator_enabled": False,
            "missing_windows": [],
        }

        # NEW: Service registrations
        self.services = {
            "registered": [],
            "implementations_found": [],
            "missing_registrations": [],
            "duplicate_registrations": [],
        }

        # NEW: ViewModel registrations
        self.viewmodels = {
            "found": [],
            "registered": [],
            "unregistered": [],
            "naming_violations": [],
        }

        # NEW: Database validation
        self.database = {
            "dbcontexts_found": [],
            "connection_strings_used": [],
            "migrations_found": False,
            "initializer_found": False,
            "missing_setup": [],
        }

        self.stats = {
            "files_scanned": 0,
            "xaml_files": 0,
            "cs_files": 0,
            "csproj_files": 0,
            "json_files": 0,
        }

    def _load_central_package_versions(self) -> Dict[str, str]:
        """Load centralized package versions from Directory.Packages.props if it exists."""
        central_versions = {}

        # Try multiple possible locations for Directory.Packages.props
        possible_paths = [
            self.root / "Directory.Packages.props",  # Same directory
            self.root.parent / "Directory.Packages.props",  # Parent directory
            Path.cwd() / "Directory.Packages.props",  # Current working directory
        ]

        packages_props_path = None
        for path in possible_paths:
            if path.exists():
                packages_props_path = path
                break

        if not packages_props_path:
            return central_versions

        try:
            tree = ET.parse(packages_props_path)
            root = tree.getroot()

            # Look for PackageVersion elements
            for elem in root.iter():
                if "PackageVersion" in elem.tag:
                    pkg_name = elem.get("Include", "")
                    version = elem.get("Version", "")
                    if pkg_name and version:
                        central_versions[pkg_name] = version

            if self.verbose and central_versions:
                print(
                    f"✓ Loaded {len(central_versions)} package versions from {packages_props_path}"
                )
        except Exception as e:
            self.logger.warning(f"Could not load Directory.Packages.props: {e}")

        return central_versions

    def _count_files(self):
        """Count files by type in the workspace for statistics."""
        try:
            # Count XAML files
            xaml_files = list(self.root.rglob("*.xaml"))
            self.stats["xaml_files"] = len(
                [
                    f
                    for f in xaml_files
                    if "bin" not in str(f).lower() and "obj" not in str(f).lower()
                ]
            )

            # Count C# files
            cs_files = list(self.root.rglob("*.cs"))
            self.stats["cs_files"] = len(
                [
                    f
                    for f in cs_files
                    if "bin" not in str(f).lower() and "obj" not in str(f).lower()
                ]
            )

            # Count CSProj files
            csproj_files = list(self.root.rglob("*.csproj"))
            self.stats["csproj_files"] = len(
                [
                    f
                    for f in csproj_files
                    if "bin" not in str(f).lower() and "obj" not in str(f).lower()
                ]
            )

            # Count JSON files
            json_files = list(self.root.rglob("*.json"))
            self.stats["json_files"] = len(
                [
                    f
                    for f in json_files
                    if "bin" not in str(f).lower() and "obj" not in str(f).lower()
                ]
            )

            # Total files scanned
            self.stats["files_scanned"] = (
                self.stats["xaml_files"]
                + self.stats["cs_files"]
                + self.stats["csproj_files"]
                + self.stats["json_files"]
            )

            if self.verbose:
                print(
                    f"  ✓ Found {self.stats['xaml_files']} XAML, "
                    + f"{self.stats['cs_files']} C#, "
                    + f"{self.stats['csproj_files']} CSProj, "
                    + f"{self.stats['json_files']} JSON files"
                )
        except Exception as e:
            self.logger.warning(f"Error counting files: {e}")

    def scan(self, scan_types: List[str], output_json: Optional[str] = None):
        """Execute selected scans."""
        scan_map = {
            "licenses": self._scan_licenses,
            "assemblies": self._scan_assemblies,
            "resources": self._scan_resources,
            "resource_evals": self._scan_resource_evals,  # NEW: HIGH PRIORITY runtime simulation
            "controls": self._scan_controls,
            "prism": self._scan_prism,
            "deprecated": self._scan_deprecated,
            "configuration": self._scan_configuration,  # NEW
            "shell": self._scan_shell,  # NEW
            "services": self._scan_services,  # NEW
            "viewmodels": self._scan_viewmodels,  # NEW
            "database": self._scan_database,  # NEW
        }

        if "all" in scan_types:
            scan_types = list(scan_map.keys())

        print(f"🔍 Wiley Widget Startup Validator - Scanning: {', '.join(scan_types)}")

        # Count files before scanning (fixes files_scanned=0 bug)
        if self.verbose:
            print("📊 Counting files in workspace...")
        self._count_files()

        for scan_type in scan_types:
            if scan_type in scan_map:
                if self.verbose:
                    print(f"\n{'='*60}")
                    print(f"Running: {scan_type.upper()}")
                    print(f"{'='*60}")
                scan_map[scan_type]()

        self._report(output_json)

    # ========================================================================
    # SCAN 1: LICENSE KEY REGISTRATIONS (HIGH PRIORITY)
    # ========================================================================

    def _scan_licenses(self):
        """Scan for Syncfusion/Bold license registrations with context-aware validation."""
        if self.verbose:
            print("🔑 Scanning license key registrations...")

        for file_path in self.root.rglob("*.cs"):
            if "bin" in str(file_path).lower() or "obj" in str(file_path).lower():
                continue

            try:
                with open(file_path, "r", encoding="utf-8") as f:
                    content = f.read()

                # Build a map of variable names to environment variables
                env_var_map = {}
                for match in ENV_VAR_ASSIGNMENT_PATTERN.finditer(content):
                    var_name = match.group(1)
                    env_var_name = match.group(2)
                    env_var_map[var_name] = env_var_name

                # Syncfusion licenses
                for match in SF_LICENSE_PATTERN.finditer(content):
                    key_or_var = match.group(1).strip()
                    line = content[: match.start()].count("\n") + 1

                    entry = {
                        "file": str(file_path.relative_to(self.root.parent)),
                        "line": line,
                        "type": "Syncfusion",
                    }

                    # Check if it's a string literal
                    string_match = STRING_LITERAL_PATTERN.match(key_or_var)
                    if string_match:
                        # Extract actual key from quotes
                        actual_key = string_match.group(1)
                        entry["key"] = (
                            actual_key[:20] + "..."
                            if len(actual_key) > 20
                            else actual_key
                        )
                        entry["method"] = "hardcoded"

                        # Check for placeholders
                        if PLACEHOLDER_PATTERN.search(actual_key):
                            entry["severity"] = "CRITICAL"
                            entry["issue"] = (
                                "Placeholder license key will cause startup failure"
                            )
                            self.licenses["placeholders"].append(entry)
                        # Validate license format
                        elif not VALID_LICENSE_FORMAT.match(actual_key):
                            entry["severity"] = "HIGH"
                            entry["issue"] = "Invalid license key format"
                            self.licenses["placeholders"].append(entry)
                        else:
                            entry["note"] = (
                                "Consider using environment variable for security"
                            )
                            self.licenses["registrations"].append(entry)
                    else:
                        # It's a variable name - check if it's from environment variable
                        entry["key"] = key_or_var
                        if key_or_var in env_var_map:
                            entry["method"] = "environment_variable"
                            entry["env_var"] = env_var_map[key_or_var]
                            entry["note"] = "Best practice - using environment variable"
                            self.licenses["registrations"].append(entry)
                        else:
                            entry["method"] = "variable"
                            entry["note"] = "Verify variable is properly initialized"
                            self.licenses["registrations"].append(entry)

                # Bold licenses
                for match in BOLD_LICENSE_PATTERN.finditer(content):
                    key_or_var = match.group(1).strip()
                    line = content[: match.start()].count("\n") + 1

                    entry = {
                        "file": str(file_path.relative_to(self.root.parent)),
                        "line": line,
                        "type": "Bold",
                    }

                    string_match = STRING_LITERAL_PATTERN.match(key_or_var)
                    if string_match:
                        actual_key = string_match.group(1)
                        entry["key"] = (
                            actual_key[:20] + "..."
                            if len(actual_key) > 20
                            else actual_key
                        )
                        entry["method"] = "hardcoded"

                        if PLACEHOLDER_PATTERN.search(actual_key):
                            entry["severity"] = "CRITICAL"
                            entry["issue"] = (
                                "Placeholder license key will cause startup failure"
                            )
                            self.licenses["placeholders"].append(entry)
                        elif not VALID_LICENSE_FORMAT.match(actual_key):
                            entry["severity"] = "HIGH"
                            entry["issue"] = "Invalid license key format"
                            self.licenses["placeholders"].append(entry)
                        else:
                            entry["note"] = (
                                "Consider using environment variable for security"
                            )
                            self.licenses["registrations"].append(entry)
                    else:
                        entry["key"] = key_or_var
                        if key_or_var in env_var_map:
                            entry["method"] = "environment_variable"
                            entry["env_var"] = env_var_map[key_or_var]
                            entry["note"] = "Best practice - using environment variable"
                            self.licenses["registrations"].append(entry)
                        else:
                            entry["method"] = "variable"
                            entry["note"] = "Verify variable is properly initialized"
                            self.licenses["registrations"].append(entry)

                # Track env var usage separately for informational purposes
                for _, env_var_name in env_var_map.items():
                    if "license" in env_var_name.lower():
                        self.licenses["env_fallbacks"].append(
                            {
                                "variable": env_var_name,
                                "note": "Environment variable detected - ensure it's set in production",
                            }
                        )

            except Exception as e:
                if self.verbose:
                    print(f"  ⚠️  Error scanning {file_path}: {e}")

        # TOUGHER: Check for missing registrations
        # If no registration is found, this is only a problem if Syncfusion packages are still present.
        if not self.licenses["registrations"] and not self.licenses["env_fallbacks"]:
            self.licenses["missing"].append(
                {
                    "issue": "No Syncfusion license registrations found",
                    "severity": "LOW",
                    "impact": "Only relevant if Syncfusion controls are present; otherwise expected post-refactor",
                    "recommendation": "If you still use Syncfusion, register a license or set an environment variable; otherwise this is informational",
                }
            )

        # TOUGHER: Warn if only env fallbacks (no hardcoded backup)
        if self.licenses["env_fallbacks"] and not self.licenses["registrations"]:
            self.licenses["missing"].append(
                {
                    "issue": "Relying solely on environment variables for licensing",
                    "severity": "MEDIUM",
                    "impact": "Application will fail if environment variable is missing",
                    "recommendation": "Consider fallback mechanism or validation on startup",
                }
            )

        if self.verbose:
            print(f"  ✓ Found {len(self.licenses['registrations'])} registrations")
            print(f"  ✓ Found {len(self.licenses['env_fallbacks'])} env fallbacks")
            print(
                f"  ⚠️  Found {len(self.licenses['placeholders'])} placeholders/invalid"
            )  # ========================================================================

    # SCAN 2: ASSEMBLY REFERENCES (HIGH PRIORITY)
    # ========================================================================

    def _scan_assemblies(self):
        """Scan for Syncfusion assembly references with TOUGHER validation."""
        if self.verbose:
            print("📦 Scanning assembly references...")

        package_versions = {}  # Track versions for consistency check

        # Scan .csproj files
        for file_path in self.root.rglob("*.csproj"):
            self.stats["csproj_files"] += 1
            try:
                tree = ET.parse(file_path)
                root = tree.getroot()

                # Find PackageReference elements
                for elem in root.iter():
                    if "PackageReference" in elem.tag:
                        pkg_name = elem.get("Include", "")
                        version = elem.get("Version", "")

                        # If no version specified in csproj, check central package management
                        if not version and pkg_name in self.central_package_versions:
                            version = self.central_package_versions[pkg_name]

                        if "syncfusion" in pkg_name.lower():
                            pkg_info = {
                                "file": str(file_path.relative_to(self.root.parent)),
                                "package": pkg_name,
                                "version": version,
                            }
                            self.assemblies["packages"].append(pkg_info)

                            # Track version for consistency check
                            if version:
                                if version not in package_versions:
                                    package_versions[version] = []
                                package_versions[version].append(pkg_name)

                            # Check for outdated versions
                            if is_version_outdated(pkg_name, version):
                                min_ver = MIN_REQUIRED_VERSIONS.get(
                                    pkg_name, MIN_REQUIRED_VERSIONS["default"]
                                )
                                self.assemblies["outdated"].append(
                                    {
                                        **pkg_info,
                                        "current_version": version,
                                        "recommended": f"{min_ver[0]}.{min_ver[1]}.{min_ver[2]}+",
                                        "severity": "HIGH",
                                    }
                                )

                            # TOUGHER: Check for breaking changes
                            breaking = check_breaking_changes(version)
                            if breaking:
                                self.assemblies["breaking_changes"].append(
                                    {
                                        **pkg_info,
                                        "warnings": breaking,
                                        "severity": "MEDIUM",
                                    }
                                )

            except ET.ParseError as e:
                self.logger.warning(f"Malformed .csproj XML in {file_path}: {e}")
                continue
            except Exception as e:
                self.logger.warning(f"Error parsing {file_path}: {e}")
                continue

        # TOUGHER: Check version consistency
        if VERSION_CONSISTENCY_CHECK and len(package_versions) > 1:
            # Find the maximum version used
            max_version = max(
                (parse_version(v) for v in package_versions.keys()), default=(0, 0, 0)
            )
            max_version_str = ".".join(map(str, max_version))

            for version, packages in package_versions.items():
                current_ver = parse_version(version)
                # Flag if not at max version
                if current_ver < max_version:
                    self.assemblies["version_mismatches"].append(
                        {
                            "version": version,
                            "packages": packages,
                            "severity": "HIGH",
                            "issue": f"{len(packages)} package(s) on version {version}, max version is {max_version_str}",
                            "recommendation": f"Update all Syncfusion packages to {max_version_str} for consistency",
                        }
                    )

        # Check required dependencies
        # NOTE: Modern NuGet with PackageReference handles transitive dependencies automatically
        # These checks are informational/warnings, not blocking issues
        installed_packages = {pkg["package"] for pkg in self.assemblies["packages"]}
        for pkg_name in installed_packages:
            if pkg_name in REQUIRED_DEPENDENCIES:
                for required_dep in REQUIRED_DEPENDENCIES[pkg_name]:
                    if not any(required_dep in p for p in installed_packages):
                        self.assemblies["missing_dependencies"].append(
                            {
                                "package": pkg_name,
                                "missing_dependency": required_dep,
                                "severity": "MEDIUM",  # Changed from HIGH - NuGet handles transitives
                                "issue": f"{pkg_name} requires {required_dep} (may be transitive)",
                                "recommendation": f"Verify {required_dep} is available (NuGet may resolve automatically)",
                            }
                        )

        # Scan XAML for xmlns declarations
        xmlns_declared = set()
        for file_path in self.root.rglob("*.xaml"):
            if "bin" in str(file_path).lower() or "obj" in str(file_path).lower():
                continue

            try:
                with open(file_path, "r", encoding="utf-8") as f:
                    content = f.read()

                for match in XMLNS_SYNCFUSION_PATTERN.finditer(content):
                    _prefix = match.group(1)
                    namespace = match.group(2)
                    xmlns_declared.add(namespace)

            except Exception as e:
                if self.verbose:
                    print(f"  ⚠️  Error scanning {file_path}: {e}")

        if self.verbose:
            print(f"  ✓ Found {len(self.assemblies['packages'])} Syncfusion packages")
            print(f"  ⚠️  Found {len(self.assemblies['outdated'])} outdated packages")
            print(
                f"  ⚠️  Found {len(self.assemblies['version_mismatches'])} version mismatches"
            )
            print(
                f"  ⚠️  Found {len(self.assemblies['missing_dependencies'])} missing dependencies"
            )
            print(
                f"  ⚠️  Found {len(self.assemblies['breaking_changes'])} packages with breaking changes"
            )
            print(
                f"  ✓ Found {len(xmlns_declared)} xmlns declarations"
            )  # ========================================================================

    # SCAN 3: MERGED RESOURCE DICTIONARIES (HIGH PRIORITY)
    # ========================================================================

    def _scan_resources(self):
        """Scan for merged resource dictionaries."""
        if self.verbose:
            print("🎨 Scanning merged resource dictionaries...")

        # Focus on App.xaml and theme files
        app_xaml = self.root / "App.xaml"
        sfskinmanager_used = False

        if app_xaml.exists():
            try:
                with open(app_xaml, "r", encoding="utf-8") as f:
                    content = f.read()

                for match in MERGED_DICT_PATTERN.finditer(content):
                    source = match.group(1)
                    self.resources["merged_dicts"].append(
                        {"file": "App.xaml", "source": source}
                    )

                # Check if SfSkinManager is being used programmatically
                if "SfSkinManager" in content:
                    sfskinmanager_used = True

            except Exception as e:
                if self.verbose:
                    print(f"  ⚠️  Error scanning App.xaml: {e}")

        # Check App.xaml.cs for SfSkinManager programmatic theme application
        app_xaml_cs = self.root / "App.xaml.cs"
        if app_xaml_cs.exists():
            try:
                with open(app_xaml_cs, "r", encoding="utf-8") as f:
                    cs_content = f.read()

                # Check for SfSkinManager.ApplyThemeAsDefaultStyle or SfSkinManager.SetTheme
                if re.search(
                    r"SfSkinManager\.(ApplyThemeAsDefaultStyle|SetTheme|ApplicationTheme)",
                    cs_content,
                ):
                    sfskinmanager_used = True

            except Exception as e:
                if self.verbose:
                    print(f"  ⚠️  Error scanning App.xaml.cs: {e}")

        # Check for required Syncfusion dictionaries ONLY if SfSkinManager is NOT used
        # When SfSkinManager is used, themes are applied programmatically
        if not sfskinmanager_used:
            merged_sources = [d["source"] for d in self.resources["merged_dicts"]]
            for required in REQUIRED_SF_DICTS:
                if not any(required.lower() in src.lower() for src in merged_sources):
                    self.resources["missing_required"].append(
                        {
                            "dictionary": required,
                            "severity": "MEDIUM",
                            "recommendation": f"Add {required} to App.xaml MergedDictionaries or use SfSkinManager programmatically",
                        }
                    )
        else:
            if self.verbose:
                print("  ✓ SfSkinManager detected - themes applied programmatically")

        if self.verbose:
            print(
                f"  ✓ Found {len(self.resources['merged_dicts'])} merged dictionaries"
            )
            print(
                f"  ⚠️  Missing {len(self.resources['missing_required'])} required dictionaries"
            )

    # ========================================================================
    # SCAN 3B: RUNTIME RESOURCE EVALUATION (HIGH PRIORITY - NEW)
    # ========================================================================

    def _scan_resource_evals(self):
        """
        Simulate runtime resource loading to detect FluentLight conflicts,
        merged dictionary issues, and potential silent exits.

        HIGH PRIORITY: Parses XAML resource files (DataTemplates.xaml, Strings.xaml)
        and validates against Syncfusion theme resources to prevent runtime crashes.
        """
        if self.verbose:
            print("🔬 Simulating runtime resource evaluation...")

        # Track all defined resources across all XAML files
        all_brushes = {}  # key -> (file, line)
        all_styles = {}  # key -> (file, line)
        all_data_templates = {}  # key -> (file, line)
        all_static_refs = set()  # All referenced resources

        # Collect all XAML excluding build artifacts)
        xaml_files = []
        for file_path in self.root.rglob("*.xaml"):
            if (
                "bin" not in str(file_path).lower()
                and "obj" not in str(file_path).lower()
            ):
                xaml_files.append(file_path)

        # Phase 1: Parse all XAML files and collect resource definitions
        for file_path in xaml_files:
            try:
                with open(file_path, "r", encoding="utf-8") as f:
                    content = f.read()

                file_rel = str(file_path.relative_to(self.root))
                self.resource_evals["xaml_files_parsed"].append(file_rel)

                # Extract brush definitions
                for match in BRUSH_RESOURCE_PATTERN.finditer(content):
                    brush_key = match.group(1)
                    line_num = content[: match.start()].count("\n") + 1

                    if brush_key in all_brushes:
                        # Duplicate brush definition
                        prev_file, prev_line = all_brushes[brush_key]
                        self.resource_evals["brush_conflicts"].append(
                            {
                                "key": brush_key,
                                "file1": prev_file,
                                "line1": prev_line,
                                "file2": file_rel,
                                "line2": line_num,
                                "severity": "HIGH",
                                "issue": f'Duplicate brush key "{brush_key}" defined in multiple files',
                                "recommendation": "Remove duplicate or use unique key names",
                            }
                        )

                    all_brushes[brush_key] = (file_rel, line_num)

                    # Check for FluentLight reserved names
                    if brush_key in FLUENTLIGHT_RESERVED_BRUSHES:
                        self.resource_evals["brush_conflicts"].append(
                            {
                                "key": brush_key,
                                "file": file_rel,
                                "line": line_num,
                                "severity": "CRITICAL",
                                "issue": f'Brush key "{brush_key}" conflicts with FluentLight theme',
                                "recommendation": f'Rename brush to avoid FluentLight reserved name (e.g., "App{brush_key}")',
                            }
                        )

                # Extract style definitions
                for match in STYLE_RESOURCE_PATTERN.finditer(content):
                    style_key = match.group(1)
                    # Note: target_type available in group(2) if needed for future validation
                    line_num = content[: match.start()].count("\n") + 1

                    if style_key in all_styles:
                        prev_file, prev_line = all_styles[style_key]
                        self.resource_evals["style_conflicts"].append(
                            {
                                "key": style_key,
                                "file1": prev_file,
                                "line1": prev_line,
                                "file2": file_rel,
                                "line2": line_num,
                                "severity": "HIGH",
                                "issue": f'Duplicate style key "{style_key}" defined in multiple files',
                                "recommendation": "Remove duplicate or use unique key names",
                            }
                        )

                    all_styles[style_key] = (file_rel, line_num)

                    # Check for Syncfusion reserved style keys
                    if style_key in SF_RESERVED_STYLE_KEYS:
                        self.resource_evals["style_conflicts"].append(
                            {
                                "key": style_key,
                                "file": file_rel,
                                "line": line_num,
                                "severity": "CRITICAL",
                                "issue": f'Style key "{style_key}" conflicts with Syncfusion reserved key',
                                "recommendation": f'Rename style to avoid Syncfusion conflict (e.g., "Custom{style_key}")',
                            }
                        )

                # Extract DataTemplate definitions
                for match in DATA_TEMPLATE_PATTERN.finditer(content):
                    template_key = match.group(1)
                    # Note: data_type available in group(2) if needed for future validation
                    line_num = content[: match.start()].count("\n") + 1

                    if template_key in all_data_templates:
                        prev_file, prev_line = all_data_templates[template_key]
                        self.resource_evals["data_template_issues"].append(
                            {
                                "key": template_key,
                                "file1": prev_file,
                                "line1": prev_line,
                                "file2": file_rel,
                                "line2": line_num,
                                "severity": "MEDIUM",
                                "issue": f'Duplicate DataTemplate key "{template_key}"',
                                "recommendation": "Ensure only one DataTemplate per key or use DataType",
                            }
                        )

                    all_data_templates[template_key] = (file_rel, line_num)

                # Extract StaticResource/DynamicResource references
                for match in STATIC_RESOURCE_REF_PATTERN.finditer(content):
                    resource_ref = match.group(1).strip()
                    all_static_refs.add(resource_ref)

                # Check for silent exit patterns
                for pattern in SILENT_EXIT_PATTERNS:
                    for match in pattern.finditer(content):
                        line_num = content[: match.start()].count("\n") + 1
                        self.resource_evals["silent_exit_risks"].append(
                            {
                                "file": file_rel,
                                "line": line_num,
                                "pattern": match.group(0)[:100],
                                "severity": "CRITICAL",
                                "issue": "Pattern detected that may cause silent application exit",
                                "recommendation": "Verify all StaticResource references exist before runtime",
                            }
                        )

                # Check for dispatcher deadlock patterns (C# files)
                if file_path.suffix.lower() == ".cs":
                    for pattern in DISPATCHER_DEADLOCK_PATTERNS:
                        for match in pattern.finditer(content):
                            line_num = content[: match.start()].count("\n") + 1
                            self.resource_evals["dispatcher_deadlocks"].append(
                                {
                                    "file": file_rel,
                                    "line": line_num,
                                    "pattern": match.group(0)[:100],
                                    "severity": "HIGH",
                                    "issue": ".NET 9 WPF Dispatcher pattern may cause deadlock",
                                    "recommendation": "Use await Dispatcher.InvokeAsync with ConfigureAwait(true) in WPF",
                                }
                            )

            except Exception as e:
                if self.verbose:
                    print(f"  ⚠️  Error parsing {file_path}: {e}")

        # Phase 2: Validate all StaticResource references
        all_defined_keys = (
            set(all_brushes.keys())
            | set(all_styles.keys())
            | set(all_data_templates.keys())
        )

        for ref_key in all_static_refs:
            # Skip system resources and binding expressions
            if ref_key.startswith("{") or ref_key.startswith("x:"):
                continue

            if ref_key not in all_defined_keys:
                self.resource_evals["missing_references"].append(
                    {
                        "key": ref_key,
                        "severity": "HIGH",
                        "issue": f'StaticResource "{ref_key}" referenced but not defined',
                        "recommendation": "Define the resource or check for typos",
                    }
                )

        # Phase 3: Validate merge order in App.xaml
        app_xaml = self.root / "src" / "WileyWidget" / "App.xaml"
        if app_xaml.exists():
            try:
                with open(app_xaml, "r", encoding="utf-8") as f:
                    content = f.read()

                merged_dicts = MERGED_DICT_PATTERN.findall(content)

                # Check if merge order matches MERGE_ORDER_CRITICAL
                for i, critical_item in enumerate(MERGE_ORDER_CRITICAL):
                    found_index = None
                    for j, merged_dict in enumerate(merged_dicts):
                        if critical_item.lower() in merged_dict.lower():
                            found_index = j
                            break

                    # Check if order is correct relative to others
                    for prev_critical in MERGE_ORDER_CRITICAL[:i]:
                        for j, merged_dict in enumerate(merged_dicts):
                            if prev_critical.lower() in merged_dict.lower():
                                if found_index is not None and j > found_index:
                                    self.resource_evals["merge_order_issues"].append(
                                        {
                                            "file": "App.xaml",
                                            "severity": "HIGH",
                                            "issue": f"{critical_item} must be loaded after {prev_critical}",
                                            "recommendation": f"Reorder MergedDictionaries: {prev_critical} before {critical_item}",
                                        }
                                    )

            except Exception as e:
                if self.verbose:
                    print(f"  ⚠️  Error validating merge order: {e}")

        # Phase 4: Mock .NET 9 WPF Dispatcher simulation
        # Detect patterns that would fail during actual dispatcher invoke
        if self.verbose:
            print("  🔧 Simulating .NET 9 WPF Dispatcher resource loading...")

        # Simulate loading resources in merge order
        simulated_resources = set()
        for merged_dict in self.resources["merged_dicts"]:
            source = merged_dict["source"]

            # Simulate loading resources from this dictionary
            for xaml_file in xaml_files:
                if source.lower() in str(xaml_file).lower():
                    try:
                        with open(xaml_file, "r", encoding="utf-8") as f:
                            content = f.read()

                        # Extract keys from this file
                        for match in BRUSH_RESOURCE_PATTERN.finditer(content):
                            simulated_resources.add(match.group(1))
                        for match in STYLE_RESOURCE_PATTERN.finditer(content):
                            simulated_resources.add(match.group(1))
                        for match in DATA_TEMPLATE_PATTERN.finditer(content):
                            simulated_resources.add(match.group(1))

                    except Exception:
                        pass

        # Check if all referenced resources would be available
        for ref_key in all_static_refs:
            if ref_key.startswith("{") or ref_key.startswith("x:"):
                continue

            if ref_key not in simulated_resources and ref_key not in all_defined_keys:
                # This would cause a silent exit in WPF
                self.resource_evals["silent_exit_risks"].append(
                    {
                        "key": ref_key,
                        "severity": "CRITICAL",
                        "issue": f'Resource "{ref_key}" would not be available during dispatcher invoke',
                        "recommendation": "Ensure resource is defined before first reference or use DynamicResource",
                    }
                )

        if self.verbose:
            print(
                f"  ✓ Parsed {len(self.resource_evals['xaml_files_parsed'])} XAML files"
            )
            print(
                f"  ✓ Found {len(all_brushes)} brushes, {len(all_styles)} styles, {len(all_data_templates)} templates"
            )
            print(f"  ⚠️  {len(self.resource_evals['brush_conflicts'])} brush conflicts")
            print(f"  ⚠️  {len(self.resource_evals['style_conflicts'])} style conflicts")
            print(
                f"  ⚠️  {len(self.resource_evals['missing_references'])} missing references"
            )
            print(
                f"  ⚠️  {len(self.resource_evals['silent_exit_risks'])} silent exit risks"
            )
            print(
                f"  ⚠️  {len(self.resource_evals['dispatcher_deadlocks'])} dispatcher deadlock risks"
            )

    # ========================================================================
    # SCAN 4: CONTROL-SPECIFIC INTEGRATIONS (MEDIUM PRIORITY)
    # ========================================================================

    def _scan_controls(self):
        """Scan controls and UI control usage.

        This scanner looks for legacy Syncfusion controls (if still present),
        native/winui DataGrid usage, and general toolkit/command usage.
        """
        if self.verbose:
            print("🎮 Scanning controls (Syncfusion + native WinUI controls)...")

        for file_path in self.root.rglob("*.xaml"):
            if "bin" in str(file_path).lower() or "obj" in str(file_path).lower():
                continue

            self.stats["xaml_files"] += 1
            try:
                with open(file_path, "r", encoding="utf-8") as f:
                    content = f.read()

                # Legacy Syncfusion controls
                for match in SF_CONTROL_PATTERN.finditer(content):
                    control_name = match.group(1)
                    attributes = match.group(2) or ""
                    line = content[: match.start()].count("\n") + 1

                    control_info = {
                        "file": str(file_path.relative_to(self.root.parent)),
                        "line": line,
                        "control": f"Syncfusion:{control_name}",
                        "attributes": attributes[:100],  # Truncate
                    }

                    self.controls["found"].append(control_info)

                    # Check required properties for Syncfusion controls
                    if control_name in REQUIRED_CONTROL_PROPS:
                        missing = []
                        for req_prop in REQUIRED_CONTROL_PROPS[control_name]:
                            if req_prop not in attributes:
                                missing.append(req_prop)

                        if missing:
                            self.controls["missing_props"].append(
                                {
                                    **control_info,
                                    "missing_properties": missing,
                                    "severity": "MEDIUM",
                                }
                            )

                # Detect native/generic DataGrid usage (post-refactor validation)
                for match in GENERIC_DATAGRID_PATTERN.finditer(content):
                    line = content[: match.start()].count("\n") + 1
                    control_info = {
                        "file": str(file_path.relative_to(self.root.parent)),
                        "line": line,
                        "control": "DataGrid",
                        "attributes": content[match.start() : match.start() + 140],
                    }
                    self.controls["found"].append(control_info)

            except Exception as e:
                if self.verbose:
                    print(f"  ⚠️  Error scanning {file_path}: {e}")

        if self.verbose:
            syncfusion_count = len(
                [
                    c
                    for c in self.controls["found"]
                    if str(c.get("control") or "").startswith("Syncfusion:")
                ]
            )
            native_count = len(
                [c for c in self.controls["found"] if c.get("control") == "DataGrid"]
            )
            print(f"  ✓ Found {syncfusion_count} legacy Syncfusion controls (if any)")
            print(f"  ✓ Found {native_count} native DataGrid usages")
            print(
                f"  ⚠️  Found {len(self.controls['missing_props'])} controls with missing properties"
            )

    # ========================================================================
    # SCAN 5: PRISM MODULE & DI REGISTRATIONS (MEDIUM PRIORITY - ENHANCED)
    # ========================================================================

    def _scan_prism(self):
        """Scan for Prism module and DI registrations with enhanced patterns."""
        if self.verbose:
            print("🔧 Scanning Prism modules and DI registrations...")

        singleton_count = 0
        scoped_count = 0
        transient_count = 0
        instance_count = 0

        for file_path in self.root.rglob("*.cs"):
            if "bin" in str(file_path).lower() or "obj" in str(file_path).lower():
                continue

            self.stats["cs_files"] += 1
            try:
                with open(file_path, "r", encoding="utf-8") as f:
                    content = f.read()

                # Module declarations (IModule implementations)
                for match in MODULE_INIT_PATTERN.finditer(content):
                    module_name = match.group(1)
                    line = content[: match.start()].count("\n") + 1
                    self.prism["modules"].append(
                        {
                            "file": str(file_path.relative_to(self.root.parent)),
                            "line": line,
                            "module": module_name,
                        }
                    )

                # Enhanced DI registration patterns (DryIoc/Prism)
                # RegisterSingleton<Interface, Implementation> or RegisterSingleton<Implementation>
                for match in REGISTER_SINGLETON_PATTERN.finditer(content):
                    interface_or_impl = match.group(1)
                    implementation = (
                        match.group(2) if match.group(2) else interface_or_impl
                    )
                    line = content[: match.start()].count("\n") + 1
                    self.prism["registrations"].append(
                        {
                            "file": str(file_path.relative_to(self.root.parent)),
                            "line": line,
                            "type": "singleton",
                            "interface": interface_or_impl,
                            "implementation": implementation,
                        }
                    )
                    singleton_count += 1

                # RegisterInstance<T>(instance)
                for match in REGISTER_INSTANCE_PATTERN.finditer(content):
                    interface = match.group(1)
                    line = content[: match.start()].count("\n") + 1
                    self.prism["registrations"].append(
                        {
                            "file": str(file_path.relative_to(self.root.parent)),
                            "line": line,
                            "type": "instance",
                            "interface": interface,
                            "implementation": "(instance)",
                        }
                    )
                    instance_count += 1

                # RegisterScoped pattern
                if "RegisterScoped" in content:
                    scoped_matches = re.finditer(
                        r"RegisterScoped<([^,>]+?)(?:,\s*([^>]+?))?>",
                        content,
                        re.IGNORECASE,
                    )
                    for match in scoped_matches:
                        interface = match.group(1)
                        implementation = match.group(2) if match.group(2) else interface
                        line = content[: match.start()].count("\n") + 1
                        self.prism["registrations"].append(
                            {
                                "file": str(file_path.relative_to(self.root.parent)),
                                "line": line,
                                "type": "scoped",
                                "interface": interface,
                                "implementation": implementation,
                            }
                        )
                        scoped_count += 1

                # Old Unity/Prism pattern (legacy)
                for match in REGISTER_TYPE_PATTERN.finditer(content):
                    interface = match.group(1)
                    implementation = match.group(2)
                    line = content[: match.start()].count("\n") + 1
                    self.prism["registrations"].append(
                        {
                            "file": str(file_path.relative_to(self.root.parent)),
                            "line": line,
                            "type": "transient",
                            "interface": interface,
                            "implementation": implementation,
                        }
                    )
                    transient_count += 1

                # View registrations
                for match in REGISTER_VIEW_PATTERN.finditer(content):
                    region = match.group(1)
                    view = match.group(2)
                    line = content[: match.start()].count("\n") + 1
                    self.prism["registrations"].append(
                        {
                            "file": str(file_path.relative_to(self.root.parent)),
                            "line": line,
                            "type": "view",
                            "region": region,
                            "view": view,
                        }
                    )

            except Exception as e:
                if self.verbose:
                    print(f"  ⚠️  Error scanning {file_path}: {e}")

        # Comprehensive summary
        total_registrations = len(self.prism["registrations"])
        if self.verbose:
            print(f"  ✓ Found {len(self.prism['modules'])} Prism modules")
            print(f"  ✓ Found {total_registrations} total registrations:")
            print(f"    - {singleton_count} Singleton registrations")
            print(f"    - {scoped_count} Scoped registrations")
            print(f"    - {transient_count} Transient registrations")
            print(f"    - {instance_count} Instance registrations")

        # New: If any Prism modules/registrations exist this is treated as a legacy dependency.
        # For the refactor path where Prism is being removed, presence of Prism usage is a red flag.
        if len(self.prism["modules"]) > 0 or any(
            "Prism." in (r.get("file") or "") for r in self.prism["registrations"]
        ):
            self.prism["issues"].append(
                {
                    "severity": "HIGH",
                    "issue": f"Prism usage detected (modules={len(self.prism['modules'])}, registrations={len(self.prism['registrations'])})",
                    "recommendation": "Remove Prism references or migrate to CommunityToolkit/Microsoft DI patterns",
                }
            )
        else:
            # No Prism modules found — good for post-refactor validation
            if self.verbose:
                print("  ✓ No Prism modules detected — migration likely complete")

        # NEW: View Discovery Validation against deprecated configs
        if self.verbose:
            print("  🔍 Validating Prism view discovery patterns...")

        # Collect all views registered
        view_registrations = [
            r for r in self.prism["registrations"] if r.get("type") == "view"
        ]

        # Scan for ViewModelLocator usage and deprecated patterns
        for file_path in self.root.rglob("*.xaml"):
            if "bin" in str(file_path).lower() or "obj" in str(file_path).lower():
                continue

            try:
                with open(file_path, "r", encoding="utf-8") as f:
                    content = f.read()

                # Check for manual ViewModel assignments (deprecated)
                manual_datacontext = re.search(
                    r'DataContext\s*=\s*"\{(?:Binding|StaticResource|DynamicResource)',
                    content,
                    re.IGNORECASE,
                )

                if manual_datacontext:
                    # Check if AutoWireViewModel is also enabled
                    auto_wire_match = VIEWMODEL_LOCATOR_PATTERN.search(content)
                    if auto_wire_match and auto_wire_match.group(1).lower() == "true":
                        line_num = content[: manual_datacontext.start()].count("\n") + 1
                        self.prism["issues"].append(
                            {
                                "file": str(file_path.relative_to(self.root)),
                                "line": line_num,
                                "severity": "MEDIUM",
                                "issue": "Manual DataContext binding conflicts with ViewModelLocator.AutoWireViewModel",
                                "recommendation": "Remove manual DataContext or disable AutoWireViewModel",
                            }
                        )

                # Check for deprecated region adapter registrations
                deprecated_region_adapters = [
                    "ContentControlRegionAdapter",
                    "SelectorRegionAdapter",
                    "ItemsControlRegionAdapter",
                ]

                for deprecated in deprecated_region_adapters:
                    if deprecated in content:
                        self.prism["issues"].append(
                            {
                                "file": str(file_path.relative_to(self.root)),
                                "severity": "LOW",
                                "issue": f"Using deprecated region adapter: {deprecated}",
                                "recommendation": "Modern Prism versions auto-register region adapters",
                            }
                        )

            except Exception as e:
                if self.verbose:
                    print(f"  ⚠️  Error checking view discovery in {file_path}: {e}")

        # Check for missing view registrations for discovered views
        discovered_views = set()
        for file_path in self.root.rglob("Views/*.xaml"):
            if (
                "bin" not in str(file_path).lower()
                and "obj" not in str(file_path).lower()
            ):
                view_name = file_path.stem  # e.g., "DashboardView"
                discovered_views.add(view_name)

        registered_views = {r.get("view") for r in view_registrations if r.get("view")}

        # Find views that are not registered
        unregistered_views = discovered_views - registered_views
        if unregistered_views:
            for view_name in unregistered_views:
                self.prism["missing_registrations"].append(
                    {
                        "view": view_name,
                        "severity": "MEDIUM",
                        "issue": f'View "{view_name}" found but not registered with RegionManager',
                        "recommendation": f'Register in module: regionManager.RegisterViewWithRegion("RegionName", typeof({view_name}))',
                    }
                )

        if self.verbose and unregistered_views:
            print(f"  ⚠️  Found {len(unregistered_views)} unregistered views")

    # ========================================================================
    # SCAN 6: DEPRECATED CONTROLS (LOW PRIORITY)
    # ========================================================================

    def _scan_deprecated(self):
        """Scan for deprecated/classic Syncfusion controls."""
        if self.verbose:
            print("⚠️  Scanning for deprecated controls...")

        for file_path in self.root.rglob("*.xaml"):
            if "bin" in str(file_path).lower() or "obj" in str(file_path).lower():
                continue

            try:
                with open(file_path, "r", encoding="utf-8") as f:
                    content = f.read()

                for match in CLASSIC_CONTROL_PATTERN.finditer(content):
                    control = match.group(1)
                    line = content[: match.start()].count("\n") + 1
                    self.deprecated["classic_controls"].append(
                        {
                            "file": str(file_path.relative_to(self.root.parent)),
                            "line": line,
                            "control": control,
                            "severity": "LOW",
                            "recommendation": f"Migrate to modern Sf{control}",
                        }
                    )

            except Exception as e:
                if self.verbose:
                    print(f"  ⚠️  Error scanning {file_path}: {e}")

        if self.verbose:
            print(
                f"  ⚠️  Found {len(self.deprecated['classic_controls'])} deprecated controls"
            )

    # ========================================================================
    # SCAN 7: CONFIGURATION VALIDATION (NEW)
    # ========================================================================

    def _scan_configuration(self):
        """Scan for configuration files and validate structure."""
        if self.verbose:
            print("⚙️  Scanning configuration files...")

        # Check for appsettings files
        config_files = [
            "appsettings.json",
            "appsettings.Development.json",
            "appsettings.Production.json",
        ]

        for config_file in config_files:
            config_path = self.root / config_file
            if config_path.exists():
                self.configuration["files_found"].append(str(config_file))
                self.stats["json_files"] += 1

                try:
                    with open(config_path, "r", encoding="utf-8") as f:
                        config_data = json.load(f)

                    # Check for ConnectionStrings section
                    if "ConnectionStrings" in config_data:
                        for conn_name in config_data["ConnectionStrings"]:
                            self.configuration["connection_strings"].append(
                                {"name": conn_name, "file": config_file}
                            )

                    # Check for environment variable placeholders
                    content = open(config_path, "r").read()
                    for match in ENV_VAR_PLACEHOLDER_PATTERN.finditer(content):
                        env_var = match.group(1)
                        self.configuration["env_vars_used"].append(
                            {"variable": env_var, "file": config_file}
                        )

                    # Check for required sections
                    required_sections = ["Logging", "ConnectionStrings"]
                    for section in required_sections:
                        if section not in config_data:
                            self.configuration["missing_sections"].append(
                                {
                                    "section": section,
                                    "file": config_file,
                                    "severity": "MEDIUM",
                                    "recommendation": f"Add {section} section to configuration",
                                }
                            )

                except json.JSONDecodeError as e:
                    self.configuration["missing_files"].append(
                        {
                            "file": config_file,
                            "error": f"Invalid JSON: {e}",
                            "severity": "HIGH",
                        }
                    )
            else:
                if config_file == "appsettings.json":  # Only required file
                    self.configuration["missing_files"].append(
                        {
                            "file": config_file,
                            "severity": "HIGH",
                            "recommendation": "Create appsettings.json configuration file",
                        }
                    )

        if self.verbose:
            print(f"  ✓ Found {len(self.configuration['files_found'])} config files")
            print(
                f"  ✓ Found {len(self.configuration['connection_strings'])} connection strings"
            )
            print(
                f"  ⚠️  Missing {len(self.configuration['missing_sections'])} required sections"
            )

    # ========================================================================
    # SCAN 8: SHELL/WINDOW VALIDATION (NEW)
    # ========================================================================

    def _scan_shell(self):
        """Scan for shell window and region definitions."""
        if self.verbose:
            print("🏠 Scanning shell windows and regions...")

        # Check for MainWindow.xaml or Shell.xaml
        shell_files = ["MainWindow.xaml", "Shell.xaml", "ShellView.xaml"]

        for shell_file in shell_files:
            for file_path in self.root.rglob(shell_file):
                if "bin" in str(file_path).lower() or "obj" in str(file_path).lower():
                    continue

                self.shell["main_window_found"] = True

                try:
                    with open(file_path, "r", encoding="utf-8") as f:
                        content = f.read()

                    # Find region definitions
                    for match in REGION_DEFINITION_PATTERN.finditer(content):
                        region_name = match.group(1)
                        self.shell["regions_defined"].append(
                            {
                                "region": region_name,
                                "file": str(file_path.relative_to(self.root.parent)),
                            }
                        )

                    # Check ViewModelLocator
                    locator_match = VIEWMODEL_LOCATOR_PATTERN.search(content)
                    if locator_match and locator_match.group(1).lower() == "true":
                        self.shell["viewmodel_locator_enabled"] = True

                except Exception as e:
                    if self.verbose:
                        print(f"  ⚠️  Error scanning {file_path}: {e}")

        if not self.shell["main_window_found"]:
            self.shell["missing_windows"].append(
                {
                    "severity": "HIGH",
                    "issue": "No MainWindow or Shell found",
                    "recommendation": "Create MainWindow.xaml as application shell",
                }
            )

        if self.verbose:
            print(
                f"  {'✓' if self.shell['main_window_found'] else '✗'} Main window found"
            )
            print(f"  ✓ Found {len(self.shell['regions_defined'])} regions")
            print(
                f"  {'✓' if self.shell['viewmodel_locator_enabled'] else '⚠️ '} ViewModelLocator enabled"
            )

        # NEW: Shell Integration Crash Detection (Syncfusion theme)
        if self.verbose:
            print("  🔥 Testing for Syncfusion theme apply crashes...")

        # Check App.xaml.cs for proper theme initialization order
        app_xaml_cs = self.root / "src" / "WileyWidget" / "App.xaml.cs"
        if app_xaml_cs.exists():
            try:
                with open(app_xaml_cs, "r", encoding="utf-8") as f:
                    cs_content = f.read()

                # Check for SfSkinManager usage
                theme_apply_match = re.search(
                    r"SfSkinManager\.(SetTheme|ApplyThemeAsDefaultStyle|SetVisualStyle)",
                    cs_content,
                    re.IGNORECASE,
                )

                if theme_apply_match:
                    # Verify it's called BEFORE InitializeComponent()
                    init_component_pos = cs_content.find("InitializeComponent()")
                    theme_apply_pos = theme_apply_match.start()

                    if (
                        init_component_pos != -1
                        and theme_apply_pos > init_component_pos
                    ):
                        self.shell["missing_windows"].append(
                            {
                                "severity": "CRITICAL",
                                "issue": "SfSkinManager theme apply called AFTER InitializeComponent()",
                                "recommendation": "Move SfSkinManager theme setup BEFORE InitializeComponent() to prevent crashes",
                                "file": "App.xaml.cs",
                            }
                        )

                    # Check for license registration before theme apply
                    license_match = re.search(
                        r"(?:SyncfusionLicenseProvider|SfSkinManager)\.(?:RegisterLicense|SetLicenseKey)",
                        cs_content,
                        re.IGNORECASE,
                    )

                    if license_match:
                        license_pos = license_match.start()
                        if theme_apply_pos < license_pos:
                            self.shell["missing_windows"].append(
                                {
                                    "severity": "HIGH",
                                    "issue": "Theme applied before license registration",
                                    "recommendation": "Register Syncfusion license BEFORE applying theme",
                                    "file": "App.xaml.cs",
                                }
                            )
                    else:
                        self.shell["missing_windows"].append(
                            {
                                "severity": "CRITICAL",
                                "issue": "No Syncfusion license registration found in App.xaml.cs",
                                "recommendation": "Add SyncfusionLicenseProvider.RegisterLicense() before theme apply",
                                "file": "App.xaml.cs",
                            }
                        )

                # Check for Application_Startup event handler
                startup_match = re.search(
                    r"protected\s+override\s+void\s+OnStartup|Application_Startup",
                    cs_content,
                    re.IGNORECASE,
                )

                if not startup_match:
                    self.shell["missing_windows"].append(
                        {
                            "severity": "MEDIUM",
                            "issue": "No OnStartup or Application_Startup event handler found",
                            "recommendation": "Add OnStartup override for proper initialization sequence",
                            "file": "App.xaml.cs",
                        }
                    )

                # Check for synchronous Dispatcher operations in startup
                sync_dispatcher = re.findall(
                    r"Dispatcher\.Invoke\s*\([^)]*\)\s*(?!\.Wait|ConfigureAwait)",
                    cs_content,
                )

                if sync_dispatcher:
                    self.shell["missing_windows"].append(
                        {
                            "severity": "HIGH",
                            "issue": f"Found {len(sync_dispatcher)} synchronous Dispatcher.Invoke calls in App.xaml.cs",
                            "recommendation": "Use await Dispatcher.InvokeAsync() to prevent startup deadlocks",
                            "file": "App.xaml.cs",
                        }
                    )

                # Check for theme resource loading in wrong thread context
                theme_resource_match = re.findall(
                    r"Application\.Current\.Resources\.MergedDictionaries\.Add",
                    cs_content,
                )

                if theme_resource_match:
                    # Verify it's in UI thread context
                    for match_obj in re.finditer(
                        r"Application\.Current\.Resources\.MergedDictionaries\.Add",
                        cs_content,
                    ):
                        pos = match_obj.start()
                        # Check if inside Dispatcher.Invoke or Task.Run
                        context_before = cs_content[max(0, pos - 200) : pos]

                        if (
                            "Task.Run" in context_before
                            and "Dispatcher.Invoke" not in context_before
                        ):
                            self.shell["missing_windows"].append(
                                {
                                    "severity": "CRITICAL",
                                    "issue": "Theme resources loaded in background thread without Dispatcher",
                                    "recommendation": "Wrap Application.Current.Resources access with Dispatcher.Invoke()",
                                    "file": "App.xaml.cs",
                                }
                            )

            except Exception as e:
                if self.verbose:
                    print(f"  ⚠️  Error checking theme initialization: {e}")

        # Check MainWindow.xaml.cs for post-theme initialization issues
        main_window_cs = self.root / "src" / "WileyWidget" / "MainWindow.xaml.cs"
        if main_window_cs.exists():
            try:
                with open(main_window_cs, "r", encoding="utf-8") as f:
                    cs_content = f.read()

                # Check for theme-dependent control access in constructor
                constructor_match = re.search(
                    r"public\s+MainWindow\s*\([^)]*\)\s*\{([^}]+)\}",
                    cs_content,
                    re.DOTALL,
                )

                if constructor_match:
                    constructor_body = constructor_match.group(1)

                    # Check for SfSkinManager calls in constructor (bad practice)
                    if "SfSkinManager" in constructor_body:
                        self.shell["missing_windows"].append(
                            {
                                "severity": "HIGH",
                                "issue": "SfSkinManager used in MainWindow constructor",
                                "recommendation": "Move theme setup to App.xaml.cs OnStartup",
                                "file": "MainWindow.xaml.cs",
                            }
                        )

                    # Check for resource dictionary access before Loaded event
                    if (
                        "Resources[" in constructor_body
                        or "FindResource(" in constructor_body
                    ):
                        if "Loaded +=" not in constructor_body:
                            self.shell["missing_windows"].append(
                                {
                                    "severity": "MEDIUM",
                                    "issue": "Resource access in constructor before Loaded event",
                                    "recommendation": "Move resource access to Loaded event handler",
                                    "file": "MainWindow.xaml.cs",
                                }
                            )

            except Exception as e:
                if self.verbose:
                    print(f"  ⚠️  Error checking MainWindow initialization: {e}")

        if self.verbose:
            theme_issues = [
                w
                for w in self.shell["missing_windows"]
                if "theme" in w.get("issue", "").lower()
                or "SfSkinManager" in w.get("issue", "")
            ]
            if theme_issues:
                print(f"  ⚠️  Found {len(theme_issues)} theme-related crash risks")
            else:
                print("  ✓ No theme initialization issues detected")

    # ========================================================================
    # SCAN 9: SERVICE REGISTRATIONS (NEW)
    # ========================================================================

    def _scan_services(self):
        """Scan for service registrations and implementations."""
        if self.verbose:
            print("🔧 Scanning service registrations...")

        # Find all service interfaces
        service_interfaces = set()
        for file_path in self.root.rglob("**/*Service.cs"):
            if "bin" in str(file_path).lower() or "obj" in str(file_path).lower():
                continue

            try:
                with open(file_path, "r", encoding="utf-8") as f:
                    content = f.read()

                # Find interface definitions
                interface_pattern = re.compile(
                    r"interface\s+(I\w+Service)", re.IGNORECASE
                )
                for match in interface_pattern.finditer(content):
                    service_interfaces.add(match.group(1))

                # Find implementations
                impl_pattern = re.compile(
                    r"class\s+(\w+)\s*:\s*I\w+Service", re.IGNORECASE
                )
                for match in impl_pattern.finditer(content):
                    self.services["implementations_found"].append(
                        {
                            "class": match.group(1),
                            "file": str(file_path.relative_to(self.root.parent)),
                        }
                    )

            except Exception as e:
                if self.verbose:
                    print(f"  ⚠️  Error scanning {file_path}: {e}")

        # Find registrations in App.DependencyInjection.cs
        di_file = self.root / "App.DependencyInjection.cs"
        if di_file.exists():
            try:
                with open(di_file, "r", encoding="utf-8") as f:
                    content = f.read()

                # Find all container registrations
                for match in REGISTER_SINGLETON_PATTERN.finditer(content):
                    interface = match.group(1)
                    impl = match.group(2) if match.group(2) else interface
                    self.services["registered"].append(
                        {"interface": interface, "implementation": impl}
                    )

            except Exception as e:
                if self.verbose:
                    print(f"  ⚠️  Error scanning DI file: {e}")

        # Check for missing registrations
        registered_interfaces = {s["interface"] for s in self.services["registered"]}
        for interface in service_interfaces:
            if interface not in registered_interfaces:
                self.services["missing_registrations"].append(
                    {
                        "interface": interface,
                        "severity": "MEDIUM",
                        "recommendation": f"Register {interface} in App.DependencyInjection.cs",
                    }
                )

        if self.verbose:
            print(f"  ✓ Found {len(self.services['registered'])} service registrations")
            print(
                f"  ✓ Found {len(self.services['implementations_found'])} implementations"
            )
            print(
                f"  ⚠️  Missing {len(self.services['missing_registrations'])} registrations"
            )

    # ========================================================================
    # SCAN 10: VIEWMODEL REGISTRATIONS (NEW)
    # ========================================================================

    def _scan_viewmodels(self):
        """Scan for ViewModels and their registrations."""
        if self.verbose:
            print("📋 Scanning ViewModels...")

        # Find all ViewModel classes
        for file_path in self.root.rglob("**/*ViewModel.cs"):
            if "bin" in str(file_path).lower() or "obj" in str(file_path).lower():
                continue

            try:
                with open(file_path, "r", encoding="utf-8") as f:
                    content = f.read()

                for match in VIEWMODEL_CLASS_PATTERN.finditer(content):
                    vm_name = match.group(1)
                    vm_entry = {
                        "name": vm_name,
                        "file": str(file_path.relative_to(self.root.parent)),
                    }

                    # Detect modern CommunityToolkit usage inside the ViewModel
                    if COMMUNITY_TOOLKIT_PATTERN.search(content):
                        vm_entry["toolkit"] = "CommunityToolkit.Mvvm"

                    self.viewmodels["found"].append(vm_entry)

            except Exception as e:
                if self.verbose:
                    print(f"  ⚠️  Error scanning {file_path}: {e}")

        # Check registrations in DI
        di_file = self.root / "App.DependencyInjection.cs"
        if di_file.exists():
            try:
                with open(di_file, "r", encoding="utf-8") as f:
                    content = f.read()

                for vm in self.viewmodels["found"]:
                    if vm["name"] in content:
                        self.viewmodels["registered"].append(vm["name"])
                    else:
                        self.viewmodels["unregistered"].append(
                            {
                                **vm,
                                "severity": "LOW",
                                "note": "ViewModel not explicitly registered (may use AutoWireViewModel)",
                            }
                        )

            except Exception as e:
                if self.verbose:
                    print(f"  ⚠️  Error checking registrations: {e}")

        if self.verbose:
            print(f"  ✓ Found {len(self.viewmodels['found'])} ViewModels")
            print(f"  ✓ Registered {len(self.viewmodels['registered'])} ViewModels")
            print(
                f"  ⚠️  Unregistered {len(self.viewmodels['unregistered'])} ViewModels"
            )

    # ========================================================================
    # SCAN 11: DATABASE VALIDATION (NEW)
    # ========================================================================

    def _scan_database(self):
        """Scan for database configuration and initialization."""
        if self.verbose:
            print("🗄️  Scanning database configuration...")

        # Find DbContext classes
        for file_path in self.root.rglob("**/*Context.cs"):
            if "bin" in str(file_path).lower() or "obj" in str(file_path).lower():
                continue

            try:
                with open(file_path, "r", encoding="utf-8") as f:
                    content = f.read()

                for match in DBCONTEXT_CLASS_PATTERN.finditer(content):
                    context_name = match.group(1)
                    self.database["dbcontexts_found"].append(
                        {
                            "name": context_name,
                            "file": str(file_path.relative_to(self.root.parent)),
                        }
                    )

                # Check for connection string usage
                for match in CONNECTION_STRING_USAGE_PATTERN.finditer(content):
                    conn_name = match.group(1)
                    self.database["connection_strings_used"].append(conn_name)

                # Check for database initialization
                if DATABASE_ENSURE_CREATED_PATTERN.search(content):
                    self.database["initializer_found"] = True

            except Exception as e:
                if self.verbose:
                    print(f"  ⚠️  Error scanning {file_path}: {e}")

        # Check for migrations folder
        migrations_folder = self.root / "Migrations"
        if migrations_folder.exists():
            self.database["migrations_found"] = True

        # Validate database setup
        if self.database["dbcontexts_found"] and not self.database["initializer_found"]:
            self.database["missing_setup"].append(
                {
                    "issue": "DbContext found but no initialization code detected",
                    "severity": "MEDIUM",
                    "recommendation": "Add Database.EnsureCreated() or MigrateAsync() in startup",
                }
            )

        if self.database["connection_strings_used"]:
            # Check if connection strings exist in configuration
            config_conn_strings = {
                cs["name"] for cs in self.configuration.get("connection_strings", [])
            }
            for conn_name in self.database["connection_strings_used"]:
                if conn_name not in config_conn_strings:
                    self.database["missing_setup"].append(
                        {
                            "issue": f'Connection string "{conn_name}" used but not defined in configuration',
                            "severity": "HIGH",
                            "recommendation": f'Add "{conn_name}" to ConnectionStrings in appsettings.json',
                        }
                    )

        if self.verbose:
            print(
                f"  ✓ Found {len(self.database['dbcontexts_found'])} DbContext classes"
            )
            print(
                f"  {'✓' if self.database['migrations_found'] else '⚠️ '} Migrations folder found"
            )
            print(
                f"  {'✓' if self.database['initializer_found'] else '⚠️ '} Database initializer found"
            )
            print(f"  ⚠️  Found {len(self.database['missing_setup'])} setup issues")

    # ========================================================================
    # REPORTING
    # ========================================================================

    def _report(self, output_json: Optional[str] = None):
        """Generate comprehensive startup validation report."""

        # Calculate issues with comprehensive criteria
        blocking_issues = (
            len(self.licenses["missing"])
            + len(self.licenses["placeholders"])
            + len(self.assemblies["outdated"])
            + len(self.assemblies["version_mismatches"])
            + len(
                [
                    r
                    for r in self.resource_evals["brush_conflicts"]
                    if r.get("severity") == "CRITICAL"
                ]
            )  # NEW
            + len(
                [
                    r
                    for r in self.resource_evals["style_conflicts"]
                    if r.get("severity") == "CRITICAL"
                ]
            )  # NEW
            + len(
                self.resource_evals["silent_exit_risks"]
            )  # NEW: Silent exits are CRITICAL
            + len(
                [
                    f
                    for f in self.configuration["missing_files"]
                    if f.get("severity") == "HIGH"
                ]
            )
            + len(
                [
                    w
                    for w in self.shell["missing_windows"]
                    if w.get("severity") in ["HIGH", "CRITICAL"]
                ]
            )  # Enhanced
            + len(
                [
                    s
                    for s in self.database["missing_setup"]
                    if s.get("severity") == "HIGH"
                ]
            )
        )

        medium_issues = (
            len(self.resources["missing_required"])
            + len(self.controls["missing_props"])
            + len(self.assemblies["breaking_changes"])
            + len(self.assemblies["missing_dependencies"])
            + len(
                [
                    r
                    for r in self.resource_evals["brush_conflicts"]
                    if r.get("severity") == "HIGH"
                ]
            )  # NEW
            + len(
                [
                    r
                    for r in self.resource_evals["style_conflicts"]
                    if r.get("severity") == "HIGH"
                ]
            )  # NEW
            + len(self.resource_evals["missing_references"])  # NEW
            + len(self.resource_evals["merge_order_issues"])  # NEW
            + len(self.resource_evals["dispatcher_deadlocks"])  # NEW
            + len(self.configuration["missing_sections"])
            + len(self.services["missing_registrations"])
            + len(
                [
                    s
                    for s in self.database["missing_setup"]
                    if s.get("severity") == "MEDIUM"
                ]
            )
        )

        low_issues = len(self.deprecated["classic_controls"]) + len(
            self.viewmodels["unregistered"]
        )  # NEW - may use AutoWireViewModel

        report = {
            "metadata": {
                "tool": "Wiley Widget Startup Validator",
                "version": "2.0",  # Updated version
                "scope": str(self.root),
            },
            "summary": {
                "files_scanned": self.stats["files_scanned"],
                "xaml_files": self.stats["xaml_files"],
                "cs_files": self.stats["cs_files"],
                "csproj_files": self.stats["csproj_files"],
                "json_files": self.stats["json_files"],  # NEW
                "blocking_issues": blocking_issues,
                "medium_issues": medium_issues,
                "low_issues": low_issues,
                "total_issues": blocking_issues + medium_issues + low_issues,
            },
            "licenses": self.licenses,
            "assemblies": self.assemblies,
            "resources": self.resources,
            "resource_evals": self.resource_evals,  # NEW: Runtime resource evaluation results
            "controls": self.controls,
            "prism": self.prism,
            "deprecated": self.deprecated,
            "configuration": self.configuration,  # NEW
            "shell": self.shell,  # NEW
            "services": self.services,  # NEW
            "viewmodels": self.viewmodels,  # NEW
            "database": self.database,  # NEW
        }

        # Console output
        print("\n" + "=" * 80)
        print("🚀 WILEY WIDGET STARTUP VALIDATION REPORT")
        print("=" * 80)

        print("\n📊 Statistics:")
        print(f"   Files Scanned: {self.stats['files_scanned']}")
        print(
            f"   XAML: {self.stats['xaml_files']}, C#: {self.stats['cs_files']}, CSProj: {self.stats['csproj_files']}"
        )

        print("\n🚨 Issues Summary:")
        print(f"   🔴 BLOCKING (High): {blocking_issues}")
        print(f"   🟡 MEDIUM: {medium_issues}")
        print(f"   🟢 LOW: {low_issues}")
        print(f"   📋 TOTAL: {blocking_issues + medium_issues + low_issues}")

        # License details
        if self.licenses["registrations"] or self.licenses["missing"]:
            print("\n🔑 License Registrations:")
            print(f"   ✓ Found: {len(self.licenses['registrations'])}")
            print(f"   ⚠️  Placeholders: {len(self.licenses['placeholders'])}")
            print(f"   🌐 Env Fallbacks: {len(self.licenses['env_fallbacks'])}")
            if self.licenses["missing"]:
                print("   ❌ CRITICAL: No license registrations found!")

        # Assembly details
        if self.assemblies["packages"]:
            print("\n📦 Assemblies:")
            print(f"   ✓ Syncfusion Packages: {len(self.assemblies['packages'])}")
            print(f"   ⚠️  Outdated: {len(self.assemblies['outdated'])}")
            for pkg in self.assemblies["outdated"]:
                print(
                    f"      - {pkg['package']} @ {pkg['current_version']} (update to {pkg['recommended']})"
                )

        # Resource details
        if self.resources["merged_dicts"] or self.resources["missing_required"]:
            print("\n🎨 Resources:")
            print(f"   ✓ Merged Dictionaries: {len(self.resources['merged_dicts'])}")
            print(f"   ⚠️  Missing Required: {len(self.resources['missing_required'])}")
            for missing in self.resources["missing_required"]:
                print(f"      - {missing['dictionary']}")

        # NEW: Resource Evaluation details (HIGH PRIORITY)
        if (
            self.resource_evals["brush_conflicts"]
            or self.resource_evals["style_conflicts"]
            or self.resource_evals["silent_exit_risks"]
            or self.resource_evals["dispatcher_deadlocks"]
        ):
            print("\n🔬 Runtime Resource Evaluation (HIGH PRIORITY):")
            print(
                f"   ✓ XAML Files Parsed: {len(self.resource_evals['xaml_files_parsed'])}"
            )

            if self.resource_evals["brush_conflicts"]:
                critical_brushes = [
                    b
                    for b in self.resource_evals["brush_conflicts"]
                    if b.get("severity") == "CRITICAL"
                ]
                print(f"   ❌ CRITICAL Brush Conflicts: {len(critical_brushes)}")
                for conflict in critical_brushes[:3]:  # Show first 3
                    print(f"      - {conflict['key']}: {conflict['issue']}")

            if self.resource_evals["style_conflicts"]:
                critical_styles = [
                    s
                    for s in self.resource_evals["style_conflicts"]
                    if s.get("severity") == "CRITICAL"
                ]
                print(f"   ❌ CRITICAL Style Conflicts: {len(critical_styles)}")
                for conflict in critical_styles[:3]:
                    print(f"      - {conflict['key']}: {conflict['issue']}")

            if self.resource_evals["silent_exit_risks"]:
                print(
                    f"   ❌ CRITICAL Silent Exit Risks: {len(self.resource_evals['silent_exit_risks'])}"
                )
                for risk in self.resource_evals["silent_exit_risks"][:3]:
                    print(f"      - {risk.get('file', 'Unknown')}: {risk['issue']}")

            if self.resource_evals["missing_references"]:
                print(
                    f"   ⚠️  Missing Resource References: {len(self.resource_evals['missing_references'])}"
                )

            if self.resource_evals["dispatcher_deadlocks"]:
                print(
                    f"   ⚠️  .NET 9 Dispatcher Deadlock Risks: {len(self.resource_evals['dispatcher_deadlocks'])}"
                )

            if self.resource_evals["merge_order_issues"]:
                print(
                    f"   ⚠️  Merge Order Issues: {len(self.resource_evals['merge_order_issues'])}"
                )

        # Control details
        if self.controls["found"]:
            print("\n🎮 Syncfusion Controls:")
            print(f"   ✓ Found: {len(self.controls['found'])}")
            print(f"   ⚠️  Missing Props: {len(self.controls['missing_props'])}")

        # Prism details
        if self.prism["modules"] or self.prism["registrations"]:
            print("\n🔧 Prism Modules:")
            print(f"   ✓ Modules: {len(self.prism['modules'])}")
            print(f"   ✓ Registrations: {len(self.prism['registrations'])}")

        # Deprecated controls
        if self.deprecated["classic_controls"]:
            print("\n⚠️  Deprecated:")
            print(f"   Classic Controls: {len(self.deprecated['classic_controls'])}")

        # Health assessment
        print("\n" + "=" * 80)
        print("🏥 HEALTH ASSESSMENT:")

        if blocking_issues == 0 and medium_issues == 0:
            print("   ✅ EXCELLENT - Ready for production startup")
        elif blocking_issues == 0:
            print("   ✅ GOOD - No blocking issues")
            print(f"   ℹ️  {medium_issues} medium-priority optimizations recommended")
        else:
            print(
                f"   ❌ CRITICAL - {blocking_issues} blocking issues must be resolved before startup"
            )
            if self.licenses["missing"]:
                print("   🔑 Missing license registration will cause runtime dialogs")
            if self.assemblies["outdated"]:
                print("   📦 Outdated assemblies may cause compatibility issues")

        # Syncfusion-specific recommendations based on version
        if self.assemblies["packages"]:
            versions_found = {
                parse_version(pkg["version"]) for pkg in self.assemblies["packages"]
            }
            max_ver = max(versions_found, default=(0, 0, 0))

            print("\n💡 SYNCFUSION RECOMMENDATIONS:")

            # .NET 9 compatibility check
            if max_ver < (28, 0, 0):
                print("   ⚠️  For .NET 9 support, upgrade to Syncfusion 28.x or later")
                print("      Current versions detected may have compatibility issues")

            # Theme optimization check
            if max_ver >= (27, 0, 0):
                print("   ✓ Theme system: Modern SfSkinManager supported")
                print(
                    "     Recommended: Use SfSkinManager.ApplyThemeAsDefaultStyle() for best performance"
                )
            else:
                print("   ⚠️  Consider upgrading for improved theme system (27.x+)")

            # Performance recommendations
            if len(self.controls["found"]) > 20:
                print(
                    "   💡 Performance tip: Consider virtualization for large data grids"
                )
                print(
                    "      Use SfDataGrid.EnableDataVirtualization for collections > 1000 items"
                )

        print("=" * 80 + "\n")

        # JSON output
        if output_json:
            output_path = Path(output_json)
            output_path.parent.mkdir(parents=True, exist_ok=True)
            with open(output_json, "w") as f:
                json.dump(report, f, indent=2)
            print(f"💾 Full report saved: {output_json}\n")

        # CI/CD assertion helper
        print(
            f"CI/CD Check: assert report['summary']['blocking_issues'] == 0  # Currently: {blocking_issues}"
        )


def main():
    parser = argparse.ArgumentParser(
        description="Wiley Widget Startup Validator - E2E Syncfusion/WPF/Prism validation",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Scan Types:
  licenses      - License key registrations (HIGH)
  assemblies    - Assembly references and versions (HIGH)
  resources     - Merged resource dictionaries (HIGH)
  resource_evals - Runtime resource loading simulation (HIGH - NEW)
                  Parses XAML for FluentLight conflicts, validates merged dictionaries,
                  detects silent exit risks, mocks .NET 9 WPF Dispatcher
  controls      - Control-specific integrations (MEDIUM)
  prism         - Prism module & DI registrations + view discovery (MEDIUM)
  deprecated    - Classic/deprecated controls (LOW)
  configuration - Config files validation (MEDIUM)
  shell         - Shell/window + Syncfusion theme crash detection (MEDIUM)
  services      - Service registrations (MEDIUM)
  viewmodels    - ViewModel registrations (LOW)
  database      - Database setup validation (MEDIUM)
  all           - Run all scans

Examples:
  python startup_validator.py --scan all
  python startup_validator.py --scan resource_evals  # High-priority resource validation
  python startup_validator.py --scan licenses,assemblies,resource_evals
  python startup_validator.py --path src/WileyWidget --output logs/startup.json --verbose
  python startup_validator.py --scan all --ci  # CI mode: quiet, exit code based on blocking issues
        """,
    )

    parser.add_argument(
        "--path", "-p", default=".", help="Root path to scan (default: current dir)"
    )
    parser.add_argument(
        "--scan", "-s", default="all", help="Comma-separated scan types or 'all'"
    )
    parser.add_argument("--output", "-o", help="JSON report output file")
    parser.add_argument("--verbose", "-v", action="store_true", help="Verbose output")
    parser.add_argument(
        "--ci",
        action="store_true",
        help="CI mode: quiet output, exit code 0 if no blocking issues",
    )

    args = parser.parse_args()

    if not os.path.exists(args.path):
        raise ValueError(f"Path not found: {args.path}")

    scan_types = [s.strip() for s in args.scan.split(",")]

    # Validate scan types
    valid_scan_types = {
        "licenses",
        "assemblies",
        "resources",
        "resource_evals",
        "controls",
        "prism",
        "deprecated",
        "configuration",
        "shell",
        "services",
        "viewmodels",
        "database",
        "all",
    }

    invalid_types = [st for st in scan_types if st not in valid_scan_types]
    if invalid_types:
        parser.error(
            f"Invalid scan type(s): {', '.join(invalid_types)}. "
            f"Valid types: {', '.join(sorted(valid_scan_types))}"
        )

    # CI mode: suppress verbose output
    verbose = args.verbose and not args.ci

    validator = StartupValidator(args.path, verbose=verbose)
    validator.scan(scan_types, output_json=args.output)

    # CI mode: exit with proper code
    if args.ci:
        # Count blocking issues
        blocking_issues = (
            len(validator.licenses["missing"])
            + len(validator.licenses["placeholders"])
            + len(validator.assemblies["outdated"])
            + len(validator.assemblies["version_mismatches"])
        )
        sys.exit(0 if blocking_issues == 0 else 1)


if __name__ == "__main__":
    main()
