#!/usr/bin/env python3
"""
XAML Converter Usage Evaluator

This script analyzes how converters are used in XAML files throughout the Wiley Widget application.
It validates converter definitions, usage patterns, and identifies potential issues.

Analysis includes:
1. Converter definitions in resource dictionaries
2. Converter usage in XAML bindings
3. Missing converter definitions
4. Unused converter definitions
5. Parameter usage patterns
6. Binding context validation
"""

import os
import re
from dataclasses import dataclass
from enum import Enum
from pathlib import Path
from typing import Any, Dict, List, Optional


class AnalysisResult(Enum):
    PASS = "PASS"
    WARNING = "WARNING"
    ERROR = "ERROR"


@dataclass
class ConverterDefinition:
    """Represents a converter definition in XAML"""

    name: str
    class_name: str
    namespace: str
    file_path: str
    line_number: int


@dataclass
class ConverterUsage:
    """Represents a converter usage in XAML"""

    converter_name: str
    property_name: str
    binding_path: str
    parameters: Optional[str]
    file_path: str
    line_number: int
    context: str = ""


@dataclass
class AnalysisIssue:
    """Represents an issue found during analysis"""

    issue_type: AnalysisResult
    message: str
    file_path: str
    line_number: int
    suggestion: str = ""


class XamlConverterAnalyzer:
    """Analyzes converter usage in XAML files"""

    def __init__(self, project_root: str):
        self.project_root = Path(project_root)
        self.defined_converters: Dict[str, ConverterDefinition] = {}
        self.used_converters: List[ConverterUsage] = []
        self.analysis_issues: List[AnalysisIssue] = []

    def analyze_project(self) -> Dict[str, Any]:
        """Perform complete analysis of converter usage in the project"""
        print("🔍 Starting XAML Converter Usage Analysis...")

        # Find all XAML files
        xaml_files = self._find_xaml_files()
        print(f"📁 Found {len(xaml_files)} XAML files to analyze")

        # Analyze each XAML file
        for xaml_file in xaml_files:
            self._analyze_xaml_file(xaml_file)

        # Perform cross-analysis
        self._analyze_definitions_vs_usage()
        self._analyze_usage_patterns()

        return {
            "defined_converters": self.defined_converters,
            "used_converters": self.used_converters,
            "issues": self.analysis_issues,
            "summary": self._generate_summary(),
        }

    def _find_xaml_files(self) -> List[Path]:
        """Find all XAML files in the project"""
        xaml_files = []
        for root, dirs, files in os.walk(self.project_root):
            for file in files:
                if file.endswith(".xaml"):
                    xaml_files.append(Path(root) / file)
        return xaml_files

    def _analyze_xaml_file(self, xaml_file: Path):
        """Analyze a single XAML file for converter definitions and usage"""
        try:
            with open(xaml_file, "r", encoding="utf-8") as f:
                content = f.read()

            lines = content.split("\n")

            # Parse XML to get namespace mappings
            namespaces = self._extract_namespaces(content)

            # Find converter definitions
            self._find_converter_definitions(content, xaml_file, lines, namespaces)

            # Find converter usage
            self._find_converter_usage(content, xaml_file, lines)

        except Exception as e:
            self.analysis_issues.append(
                AnalysisIssue(
                    AnalysisResult.ERROR,
                    f"Failed to analyze XAML file: {str(e)}",
                    str(xaml_file),
                    0,
                    "Check file encoding and XML syntax",
                )
            )

    def _resolve_namespace(
        self, namespace_prefix: str, namespaces: Dict[str, str]
    ) -> str:
        """Resolve namespace prefix to full namespace name"""
        if namespace_prefix in namespaces:
            namespace_uri = namespaces[namespace_prefix]
            # Extract namespace from URI (simplified)
            if "clr-namespace:" in namespace_uri:
                return namespace_uri.split("clr-namespace:")[1].split(";")[0]
            else:
                return namespace_uri
        return "Unknown"

    def _extract_namespaces(self, content: str) -> Dict[str, str]:
        """Extract namespace mappings from XAML content"""
        namespaces = {}
        # Find xmlns declarations
        xmlns_pattern = r'xmlns(?::(\w+))?\s*=\s*["\']([^"\']+)["\']'
        matches = re.findall(xmlns_pattern, content)

        for prefix, uri in matches:
            if prefix:
                namespaces[prefix] = uri
            else:
                namespaces["default"] = uri

        return namespaces

    def _find_converter_definitions(
        self,
        content: str,
        file_path: Path,
        lines: List[str],
        namespaces: Dict[str, str],
    ):
        """Find converter definitions in XAML resource dictionaries"""
        # Pattern for converter definitions like:
        # <converters:BalanceColorConverter x:Key="BalanceColorConverter" />
        # <local:BoolToVisibilityConverter x:Key="BoolToVisibilityConverter" />

        converter_patterns = [
            # WPF converters without namespace prefix: <BooleanToVisibilityConverter x:Key="KeyName" />
            r'<(\w+Converter)\s+x:Key\s*=\s*["\']([^"\']+)["\'][^>]*>',
            # Namespaced converters: <local:BalanceColorConverter x:Key="BalanceColorConverter" />
            r'<(\w+):(\w+)\s+x:Key\s*=\s*["\']([^"\']+)["\'][^>]*>',
        ]

        for line_num, line in enumerate(lines, 1):
            for pattern_idx, pattern in enumerate(converter_patterns):
                matches = re.finditer(pattern, line)
                for match in matches:
                    if pattern_idx == 0:  # WPF converter without namespace
                        class_name = match.group(1)
                        key_name = match.group(2)
                        full_namespace = "System.Windows"  # WPF standard namespace
                    else:  # Namespaced converter
                        namespace_prefix = match.group(1)
                        class_name = match.group(2)
                        key_name = match.group(3)
                        full_namespace = self._resolve_namespace(
                            namespace_prefix, namespaces
                        )

                    converter_def = ConverterDefinition(
                        name=key_name,
                        class_name=f"{full_namespace}.{class_name}",
                        namespace=full_namespace,
                        file_path=str(file_path),
                        line_number=line_num,
                    )

                    if key_name in self.defined_converters:
                        self.analysis_issues.append(
                            AnalysisIssue(
                                AnalysisResult.WARNING,
                                f"Duplicate converter definition: '{key_name}'",
                                str(file_path),
                                line_num,
                                "Remove duplicate definition or use unique key names",
                            )
                        )
                    else:
                        self.defined_converters[key_name] = converter_def

    def _find_converter_usage(self, content: str, file_path: Path, lines: List[str]):
        """Find converter usage in XAML bindings"""
        # Pattern for converter usage in bindings:
        # Converter={StaticResource ConverterName}
        # Converter={StaticResource ConverterName}, ConverterParameter=value

        usage_pattern = r"Converter\s*=\s*\{StaticResource\s+([^}]+)\}(?:\s*,\s*ConverterParameter\s*=\s*([^}]+))?"

        for line_num, line in enumerate(lines, 1):
            matches = re.finditer(usage_pattern, line)
            for match in matches:
                converter_name = match.group(1).strip()
                parameters = match.group(2).strip() if match.group(2) else None

                # Extract binding path and property for context
                binding_match = re.search(r'(\w+)\s*=\s*["\']\{Binding\s+([^,]+)', line)
                if binding_match:
                    property_name = binding_match.group(1)
                    binding_path = binding_match.group(2).strip()
                else:
                    property_name = "Unknown"
                    binding_path = "Unknown"

                usage = ConverterUsage(
                    converter_name=converter_name,
                    property_name=property_name,
                    binding_path=binding_path,
                    parameters=parameters,
                    file_path=str(file_path),
                    line_number=line_num,
                    context=line.strip(),
                )

                self.used_converters.append(usage)

    def _analyze_definitions_vs_usage(self):
        """Analyze the relationship between defined and used converters"""
        defined_names = set(self.defined_converters.keys())
        used_names = set(usage.converter_name for usage in self.used_converters)

        # Find unused converters
        unused = defined_names - used_names
        for unused_converter in unused:
            definition = self.defined_converters[unused_converter]
            self.analysis_issues.append(
                AnalysisIssue(
                    AnalysisResult.WARNING,
                    f"Unused converter definition: '{unused_converter}'",
                    definition.file_path,
                    definition.line_number,
                    "Remove unused converter or ensure it's used in XAML bindings",
                )
            )

        # Find undefined converters
        undefined = used_names - defined_names
        for undefined_converter in undefined:
            # Find usage locations
            usages = [
                u
                for u in self.used_converters
                if u.converter_name == undefined_converter
            ]
            for usage in usages:
                self.analysis_issues.append(
                    AnalysisIssue(
                        AnalysisResult.ERROR,
                        f"Undefined converter usage: '{undefined_converter}'",
                        usage.file_path,
                        usage.line_number,
                        "Add converter definition to resource dictionary or fix spelling",
                    )
                )

    def _analyze_usage_patterns(self):
        """Analyze converter usage patterns for potential issues"""
        for usage in self.used_converters:
            # Check for common parameter issues
            if usage.parameters:
                # Check for spacing issues in parameters
                if usage.parameters.startswith(" ") or usage.parameters.endswith(" "):
                    self.analysis_issues.append(
                        AnalysisIssue(
                            AnalysisResult.WARNING,
                            f"Converter parameter has leading/trailing spaces: '{usage.parameters}'",
                            usage.file_path,
                            usage.line_number,
                            "Remove unnecessary spaces from ConverterParameter value",
                        )
                    )

                # Check for boolean parameter patterns
                if usage.parameters.lower() in ["invert", "inverted", "not", "reverse"]:
                    if usage.converter_name not in [
                        "BoolToVis",
                        "BoolToVisibilityConverter",
                        "InverseBooleanConverter",
                    ]:
                        self.analysis_issues.append(
                            AnalysisIssue(
                                AnalysisResult.WARNING,
                                f"Boolean inversion parameter '{usage.parameters}' used with non-boolean converter",
                                usage.file_path,
                                usage.line_number,
                                "Consider using InverseBooleanConverter or BoolToVisibilityConverter with 'invert' parameter",
                            )
                        )

    def _generate_summary(self) -> Dict[str, Any]:
        """Generate analysis summary"""
        total_definitions = len(self.defined_converters)
        total_usages = len(self.used_converters)
        total_issues = len(self.analysis_issues)

        issues_by_type = {}
        for issue in self.analysis_issues:
            issues_by_type[issue.issue_type] = (
                issues_by_type.get(issue.issue_type, 0) + 1
            )

        # Usage statistics
        usage_stats = {}
        for usage in self.used_converters:
            usage_stats[usage.converter_name] = (
                usage_stats.get(usage.converter_name, 0) + 1
            )

        return {
            "total_definitions": total_definitions,
            "total_usages": total_usages,
            "total_issues": total_issues,
            "issues_by_type": issues_by_type,
            "usage_statistics": usage_stats,
            "most_used_converters": sorted(
                usage_stats.items(), key=lambda x: x[1], reverse=True
            )[:5],
        }


class XamlConverterEvaluator:
    """Main evaluator class for XAML converter analysis"""

    def __init__(self, project_root: str):
        self.project_root = project_root
        self.analyzer = XamlConverterAnalyzer(project_root)

    def run_evaluation(self) -> Dict[str, Any]:
        """Run the complete XAML converter evaluation"""
        results = self.analyzer.analyze_project()
        self._print_results(results)
        return results

    def _print_results(self, results: Dict[str, Any]):
        """Print formatted evaluation results"""
        print("\n" + "=" * 80)
        print("XAML CONVERTER USAGE EVALUATION RESULTS")
        print("=" * 80)

        summary = results["summary"]

        print("\n📊 SUMMARY:")
        print(f"  Converter Definitions: {summary['total_definitions']}")
        print(f"  Converter Usages:      {summary['total_usages']}")
        print(f"  Analysis Issues:       {summary['total_issues']}")

        if summary["issues_by_type"]:
            print("\n🚨 ISSUES BY TYPE:")
            for issue_type, count in summary["issues_by_type"].items():
                icon = (
                    "❌"
                    if issue_type == AnalysisResult.ERROR
                    else "⚠️" if issue_type == AnalysisResult.WARNING else "ℹ️"
                )
                print(f"  {icon} {issue_type.value}: {count}")

        if summary["most_used_converters"]:
            print("\n🔥 MOST USED CONVERTERS:")
            for converter, count in summary["most_used_converters"]:
                print(f"  {converter}: {count} usages")

        print("\n📋 DETAILED ISSUES:")
        if results["issues"]:
            for i, issue in enumerate(results["issues"], 1):
                icon = "❌" if issue.issue_type == AnalysisResult.ERROR else "⚠️"
                print(f"  {i}. {icon} {issue.message}")
                print(f"     File: {Path(issue.file_path).name}:{issue.line_number}")
                if issue.suggestion:
                    print(f"     💡 {issue.suggestion}")
                print()
        else:
            print("  ✅ No issues found!")

        print("\n📈 CONVERTER DEFINITIONS:")
        for name, definition in results["defined_converters"].items():
            print(f"  {name} -> {definition.class_name}")
            print(f"    📁 {Path(definition.file_path).name}:{definition.line_number}")

        print("\n🔍 CONVERTER USAGE SAMPLES:")
        usage_samples = results["used_converters"][:10]  # Show first 10
        for usage in usage_samples:
            params = f" (Parameter: {usage.parameters})" if usage.parameters else ""
            print(f"  {usage.converter_name} -> {usage.property_name} binding{params}")
            print(f"    📁 {Path(usage.file_path).name}:{usage.line_number}")


def main():
    """Main evaluation function"""
    import sys

    # Get project root from command line or current directory
    project_root = sys.argv[1] if len(sys.argv) > 1 else "."

    print(f"Evaluating XAML converter usage in: {project_root}")

    evaluator = XamlConverterEvaluator(project_root)
    results = evaluator.run_evaluation()

    # Return exit code based on issues
    summary = results["summary"]
    has_errors = summary["issues_by_type"].get(AnalysisResult.ERROR, 0) > 0

    return 1 if has_errors else 0


if __name__ == "__main__":
    import sys

    sys.exit(main())
