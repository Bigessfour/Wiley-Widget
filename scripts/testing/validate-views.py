#!/usr/bin/env python3
"""
Validates WinForms views for proper Syncfusion control usage, theming, and essential elements.

This script performs comprehensive static analysis on WinForms Form classes to ensure:
- Proper Syncfusion control usage and configuration
- Consistent theming applied to all controls
- Essential form elements (DockingManager, Ribbon, StatusBar, etc.)
- Data binding to ViewModels
- Proper disposal of resources
- Thread-safe UI updates
"""

import json
import re
import sys
from dataclasses import dataclass
from datetime import datetime
from enum import Enum
from pathlib import Path
from typing import List, Optional


class Severity(Enum):
    """Violation severity levels."""

    ERROR = "Error"
    WARNING = "Warning"
    INFO = "Info"


@dataclass
class Violation:
    """Represents a validation violation."""

    rule: str
    severity: Severity
    message: str
    file: str
    line: Optional[int] = None

    def to_dict(self) -> dict:
        """Convert to dictionary for JSON serialization."""
        return {
            "Rule": self.rule,
            "Severity": self.severity.value,
            "Message": self.message,
            "File": self.file,
            "Line": self.line,
        }


class ViewValidator:
    """Validates WinForms view files."""

    SYNCFUSION_CONTROLS = [
        "SfDataGrid",
        "DockingManager",
        "RibbonControlAdv",
        "StatusBarAdv",
        "ChartControl",
        "SfChart",
        "GridControl",
        "GridGroupingControl",
        "ButtonAdv",
        "TextBoxExt",
        "ComboBoxAdv",
        "DateTimePickerAdv",
        "TabControlAdv",
        "PanelAdv",
        "SplitContainerAdv",
        "ToolStripEx",
        "SfListView",
        "SfTreeView",
        "SfComboBox",
        "SfTextBox",
    ]

    THEME_PROPERTIES = [
        "ThemeName",
        "Style",
        "ThemeStyle",
        "VisualStyle",
        "ColorScheme",
    ]

    def __init__(self, verbose: bool = False):
        self.verbose = verbose
        self.violations: List[Violation] = []

    def validate_file(self, file_path: Path) -> List[Violation]:
        """Validate a single view file."""
        violations = []
        filename = file_path.name  # Define filename before try block

        try:
            content = file_path.read_text(encoding="utf-8")

            if self.verbose:
                print(f"  Validating {filename}...")

            violations.extend(self._check_form_inheritance(content, filename))
            violations.extend(self._check_syncfusion_controls(content, filename))
            violations.extend(self._check_theming(content, filename))
            violations.extend(self._check_viewmodel_binding(content, filename))
            violations.extend(self._check_disposal(content, filename))
            violations.extend(self._check_docking_manager(content, filename))
            violations.extend(self._check_ribbon_controls(content, filename))
            violations.extend(self._check_data_binding(content, filename))
            violations.extend(self._check_thread_safety(content, filename))
            violations.extend(self._check_initialization(content, filename))
            violations.extend(self._check_accessibility(content, filename))
            violations.extend(self._check_performance(content, filename))
            violations.extend(self._check_error_handling(content, filename))
            violations.extend(self._check_localization(content, filename))
            violations.extend(self._check_control_naming(content, filename))
            violations.extend(self._check_layout_patterns(content, filename))
            violations.extend(self._check_event_handlers(content, filename))
            violations.extend(self._check_async_patterns(content, filename))
        except Exception as e:
            violations.append(
                Violation(
                    rule="VW000",
                    severity=Severity.ERROR,
                    message=f"Failed to parse file: {e}",
                    file=filename,
                )
            )

        return violations

    def _check_form_inheritance(self, content: str, filename: str) -> List[Violation]:
        """Check that forms properly inherit from Form or SfForm."""
        violations = []

        # Look for class declaration
        class_match = re.search(
            r"public\s+(?:partial\s+)?class\s+(\w+)\s*:\s*(\w+)", content
        )

        if class_match:
            class_name = class_match.group(1)
            base_class = class_match.group(2)

            # Check if it's a Form class
            if "Form" in filename or "Window" in filename:
                if base_class not in ["Form", "SfForm", "MetroForm", "Office2019Form"]:
                    violations.append(
                        Violation(
                            rule="VW001",
                            severity=Severity.WARNING,
                            message=f"Form '{class_name}' should inherit from Form, SfForm, or Syncfusion themed form",
                            file=filename,
                            line=self._get_line_number(content, class_match.start()),
                        )
                    )

        return violations

    def _check_syncfusion_controls(
        self, content: str, filename: str
    ) -> List[Violation]:
        """Check for proper Syncfusion control usage."""
        violations = []

        # Check if file uses Syncfusion controls
        uses_syncfusion = any(ctrl in content for ctrl in self.SYNCFUSION_CONTROLS)

        if uses_syncfusion:
            # Check for proper using statements
            if (
                "using Syncfusion.Windows.Forms" not in content
                and "using Syncfusion.WinForms" not in content
            ):
                violations.append(
                    Violation(
                        rule="VW002",
                        severity=Severity.ERROR,
                        message="Missing Syncfusion.Windows.Forms or Syncfusion.WinForms using statement",
                        file=filename,
                    )
                )

            # Check for control initialization
            for control in self.SYNCFUSION_CONTROLS:
                pattern = rf"new\s+{control}\s*\("
                if re.search(pattern, content):
                    # Check if control is added to Controls collection
                    control_var_pattern = (
                        rf"(?:var|{control})\s+(\w+)\s*=\s*new\s+{control}"
                    )
                    matches = re.finditer(control_var_pattern, content)

                    for match in matches:
                        var_name = match.group(1)
                        # Look for .Controls.Add or similar
                        if not re.search(rf"Controls\.Add\s*\(\s*{var_name}", content):
                            violations.append(
                                Violation(
                                    rule="VW003",
                                    severity=Severity.WARNING,
                                    message=f"Syncfusion control '{var_name}' may not be added to Controls collection",
                                    file=filename,
                                    line=self._get_line_number(content, match.start()),
                                )
                            )

        return violations

    def _check_theming(self, content: str, filename: str) -> List[Violation]:
        """Check for consistent theming."""
        violations = []

        # Check if any Syncfusion controls are used
        uses_syncfusion = any(ctrl in content for ctrl in self.SYNCFUSION_CONTROLS)

        if uses_syncfusion:
            # Check for theme initialization
            theme_init_patterns = [
                r"SfSkinManager\.SetVisualStyle\(",
                r"ThemeName\s*=",
                r"Style\s*=\s*Syncfusion",
                r"ApplyTheme\s*\(",
                r"Office2019Theme",
                r"MetroTheme",
            ]

            has_theming = any(
                re.search(pattern, content) for pattern in theme_init_patterns
            )

            if not has_theming:
                violations.append(
                    Violation(
                        rule="VW004",
                        severity=Severity.WARNING,
                        message="No theme initialization found for Syncfusion controls",
                        file=filename,
                    )
                )

            # Check for inconsistent theming
            theme_assignments = re.findall(
                r'(ThemeName|Style|VisualStyle)\s*=\s*["\']?(\w+)', content
            )
            unique_themes = set(theme[1] for theme in theme_assignments)

            if len(unique_themes) > 1:
                violations.append(
                    Violation(
                        rule="VW005",
                        severity=Severity.WARNING,
                        message=f"Inconsistent themes detected: {', '.join(unique_themes)}",
                        file=filename,
                    )
                )

        return violations

    def _check_viewmodel_binding(self, content: str, filename: str) -> List[Violation]:
        """Check for proper ViewModel binding."""
        violations = []

        # Check if form references a ViewModel
        viewmodel_pattern = r"(\w+ViewModel)\s+\w+"
        viewmodel_matches = re.findall(viewmodel_pattern, content)

        if viewmodel_matches:
            # Check for proper data binding setup
            binding_patterns = [
                r"DataBindings\.Add\(",
                r"SetBinding\(",
                r"BindingContext\s*=",
                r"DataSource\s*=",
            ]

            has_binding = any(
                re.search(pattern, content) for pattern in binding_patterns
            )

            if not has_binding:
                violations.append(
                    Violation(
                        rule="VW006",
                        severity=Severity.INFO,
                        message="ViewModel detected but no data binding found",
                        file=filename,
                    )
                )

        return violations

    def _check_disposal(self, content: str, filename: str) -> List[Violation]:
        """Check for proper resource disposal."""
        violations = []

        # Check if Dispose pattern is implemented
        has_dispose = re.search(
            r"protected\s+override\s+void\s+Dispose\s*\(\s*bool\s+disposing\s*\)",
            content,
        )

        # Check if form uses disposable resources
        disposable_patterns = [
            r"new\s+(?:Timer|FileStream|StreamReader|StreamWriter|Image|Bitmap)",
            r"DockingManager",
            r"RibbonControlAdv",
        ]

        uses_disposables = any(
            re.search(pattern, content) for pattern in disposable_patterns
        )

        if uses_disposables and not has_dispose:
            violations.append(
                Violation(
                    rule="VW007",
                    severity=Severity.WARNING,
                    message="Form uses disposable resources but doesn't override Dispose",
                    file=filename,
                )
            )

        return violations

    def _check_docking_manager(self, content: str, filename: str) -> List[Violation]:
        """Check DockingManager configuration."""
        violations = []

        if "DockingManager" in content:
            # Check for proper initialization (handle both patterns)
            pattern1 = r"DockingManager\s*=\s*new\s+DockingManager"
            pattern2 = r"new\s+DockingManager\s*\("
            if not re.search(pattern1, content) and not re.search(pattern2, content):
                violations.append(
                    Violation(
                        rule="VW008",
                        severity=Severity.ERROR,
                        message="DockingManager not properly initialized",
                        file=filename,
                    )
                )

            # Check for EnableDocumentMode or similar essential properties
            essential_props = ["EnableDocumentMode", "DockBehavior", "HostControl"]
            missing_props = [prop for prop in essential_props if prop not in content]

            if missing_props and len(missing_props) == len(essential_props):
                violations.append(
                    Violation(
                        rule="VW009",
                        severity=Severity.INFO,
                        message="DockingManager may be missing essential configuration properties",
                        file=filename,
                    )
                )

        return violations

    def _check_ribbon_controls(self, content: str, filename: str) -> List[Violation]:
        """Check Ribbon control configuration."""
        violations = []

        if "RibbonControlAdv" in content:
            # Check for proper initialization
            if not re.search(r"RibbonControlAdv\s*=\s*new\s+RibbonControlAdv", content):
                violations.append(
                    Violation(
                        rule="VW010",
                        severity=Severity.ERROR,
                        message="RibbonControlAdv not properly initialized",
                        file=filename,
                    )
                )

            # Check for Tabs
            if "ToolStripTabItem" not in content and "RibbonTab" not in content:
                violations.append(
                    Violation(
                        rule="VW011",
                        severity=Severity.WARNING,
                        message="RibbonControlAdv has no tabs defined",
                        file=filename,
                    )
                )

        return violations

    def _check_data_binding(self, content: str, filename: str) -> List[Violation]:
        """Check for proper data binding setup."""
        violations = []

        # Check for SfDataGrid
        if "SfDataGrid" in content:
            # Check for DataSource assignment
            if not re.search(r"DataSource\s*=", content):
                violations.append(
                    Violation(
                        rule="VW012",
                        severity=Severity.WARNING,
                        message="SfDataGrid found but no DataSource assignment detected",
                        file=filename,
                    )
                )

            # Check for column definitions or AutoGenerateColumns
            if "Columns.Add" not in content and "AutoGenerateColumns" not in content:
                violations.append(
                    Violation(
                        rule="VW013",
                        severity=Severity.INFO,
                        message="SfDataGrid should have Columns defined or AutoGenerateColumns enabled",
                        file=filename,
                    )
                )

        return violations

    def _check_thread_safety(self, content: str, filename: str) -> List[Violation]:
        """Check for thread-safe UI updates."""
        violations = []

        # Check for async/await patterns
        async_methods = re.findall(r"async\s+Task(?:<\w+>)?\s+(\w+)", content)

        if async_methods:
            # Check for InvokeRequired pattern
            if "InvokeRequired" not in content and "Invoke(" not in content:
                violations.append(
                    Violation(
                        rule="VW014",
                        severity=Severity.WARNING,
                        message="Async methods found but no InvokeRequired check for thread-safe UI updates",
                        file=filename,
                    )
                )

        return violations

    def _check_initialization(self, content: str, filename: str) -> List[Violation]:
        """Check for proper initialization order."""
        violations = []

        # Check if InitializeComponent is called
        constructor_pattern = r"public\s+\w+\s*\([^)]*\)\s*{([^}]*)}"
        constructor_match = re.search(constructor_pattern, content)

        if constructor_match:
            constructor_body = constructor_match.group(1)

            if "InitializeComponent" not in constructor_body:
                violations.append(
                    Violation(
                        rule="VW015",
                        severity=Severity.ERROR,
                        message="Constructor must call InitializeComponent()",
                        file=filename,
                        line=self._get_line_number(content, constructor_match.start()),
                    )
                )

            # Check if InitializeComponent is called first
            if constructor_body.strip() and not constructor_body.strip().startswith(
                "InitializeComponent"
            ):
                first_statement = constructor_body.strip().split(";")[0].strip()
                if "InitializeComponent" not in first_statement:
                    violations.append(
                        Violation(
                            rule="VW016",
                            severity=Severity.WARNING,
                            message="InitializeComponent() should be called first in constructor",
                            file=filename,
                            line=self._get_line_number(
                                content, constructor_match.start()
                            ),
                        )
                    )

        return violations

    def _check_accessibility(self, content: str, filename: str) -> List[Violation]:
        """Check for accessibility features (keyboard navigation, screen readers)."""
        violations = []

        # Check for TabIndex assignments
        has_tab_index = re.search(r"TabIndex\s*=", content)
        has_controls = any(control in content for control in self.SYNCFUSION_CONTROLS)

        if has_controls and not has_tab_index:
            violations.append(
                Violation(
                    rule="VW017",
                    severity=Severity.INFO,
                    message="Consider setting TabIndex for keyboard navigation",
                    file=filename,
                )
            )

        # Check for AccessibleName/Description
        has_accessible_props = re.search(
            r"Accessible(Name|Description|Role)\s*=", content
        )

        if has_controls and not has_accessible_props:
            violations.append(
                Violation(
                    rule="VW018",
                    severity=Severity.INFO,
                    message="Consider adding AccessibleName/Description for screen readers",
                    file=filename,
                )
            )

        return violations

    def _check_performance(self, content: str, filename: str) -> List[Violation]:
        """Check for common performance issues."""
        violations = []

        # Check for BeginUpdate/EndUpdate pattern with grids
        if "SfDataGrid" in content or "GridControl" in content:
            has_begin_update = "BeginUpdate" in content
            has_end_update = "EndUpdate" in content

            if not (has_begin_update and has_end_update):
                violations.append(
                    Violation(
                        rule="VW019",
                        severity=Severity.INFO,
                        message="Consider using BeginUpdate/EndUpdate for large data operations",
                        file=filename,
                    )
                )

        # Check for SuspendLayout/ResumeLayout
        has_suspend = "SuspendLayout" in content
        has_resume = "ResumeLayout" in content

        if "Controls.Add" in content and not (has_suspend and has_resume):
            violations.append(
                Violation(
                    rule="VW020",
                    severity=Severity.INFO,
                    message="Consider using SuspendLayout/ResumeLayout when adding multiple controls",
                    file=filename,
                )
            )

        return violations

    def _check_error_handling(self, content: str, filename: str) -> List[Violation]:
        """Check for proper error handling in event handlers and async methods."""
        violations = []

        # Find event handlers (methods with object sender, EventArgs e pattern)
        event_handlers = re.findall(
            r"private\s+(?:async\s+)?void\s+(\w+)\s*\(\s*object\s+sender,\s*\w+\s+e\s*\)",
            content,
        )

        if event_handlers:
            # Check for try-catch in event handlers
            try_catch_count = len(re.findall(r"\btry\s*{", content))

            if try_catch_count == 0:
                violations.append(
                    Violation(
                        rule="VW021",
                        severity=Severity.WARNING,
                        message=f"Found {len(event_handlers)} event handlers but no try-catch blocks",
                        file=filename,
                    )
                )

        # Check async methods for error handling
        async_methods = re.findall(r"async\s+Task(?:<\w+>)?\s+(\w+)", content)

        if async_methods:
            for method in async_methods:
                # Check if method body contains try-catch
                method_pattern = (
                    rf"async\s+Task(?:<\w+>)?\s+{method}\s*\([^)]*\)\s*{{([^}}]+)}}"
                )
                method_match = re.search(method_pattern, content, re.DOTALL)

                if method_match and "try" not in method_match.group(1):
                    violations.append(
                        Violation(
                            rule="VW022",
                            severity=Severity.WARNING,
                            message=f"Async method '{method}' should include error handling",
                            file=filename,
                        )
                    )

        return violations

    def _check_localization(self, content: str, filename: str) -> List[Violation]:
        """Check for localization support."""
        violations = []

        # Check for hardcoded strings in UI
        hardcoded_strings = re.findall(
            r'(?:Text|Caption|HeaderText|ToolTipText)\s*=\s*"([^"]+)"', content
        )

        if hardcoded_strings and len(hardcoded_strings) > 3:
            violations.append(
                Violation(
                    rule="VW023",
                    severity=Severity.INFO,
                    message=f"Found {len(hardcoded_strings)} hardcoded strings - consider using resource files for localization",
                    file=filename,
                )
            )

        # Check for Localizable attribute
        has_localizable = re.search(r"\[Localizable\(true\)\]", content)

        if hardcoded_strings and not has_localizable:
            violations.append(
                Violation(
                    rule="VW024",
                    severity=Severity.INFO,
                    message="Consider adding [Localizable(true)] attribute to form class",
                    file=filename,
                )
            )

        return violations

    def _check_control_naming(self, content: str, filename: str) -> List[Violation]:
        """Check for proper control naming conventions."""
        violations = []

        # Find control declarations
        control_declarations = re.findall(
            r"(private|protected)\s+(\w+)\s+(\w+)\s*;", content
        )

        poorly_named = []

        for _, control_type, control_name in control_declarations:
            # Check if it's a Syncfusion control
            if any(
                sf_control in control_type for sf_control in self.SYNCFUSION_CONTROLS
            ):
                # Check for generic names like "control1", "panel1", etc.
                if re.match(r"^(control|panel|button|textBox)\d+$", control_name):
                    poorly_named.append(control_name)

                # Check for Hungarian notation (old WinForms pattern)
                if control_name.startswith(
                    ("btn", "txt", "lbl", "cmb", "chk", "dgv", "grp")
                ):
                    violations.append(
                        Violation(
                            rule="VW025",
                            severity=Severity.INFO,
                            message=f"Control '{control_name}' uses Hungarian notation - consider descriptive names",
                            file=filename,
                        )
                    )

        if poorly_named:
            violations.append(
                Violation(
                    rule="VW026",
                    severity=Severity.WARNING,
                    message=f"Found {len(poorly_named)} controls with generic names: {', '.join(poorly_named[:3])}{'...' if len(poorly_named) > 3 else ''}",
                    file=filename,
                )
            )

        return violations

    def _check_layout_patterns(self, content: str, filename: str) -> List[Violation]:
        """Check for proper layout management."""
        violations = []

        # Check for Dock/Anchor usage
        has_dock = "Dock" in content
        has_anchor = "Anchor" in content

        if "Controls.Add" in content and not (has_dock or has_anchor):
            violations.append(
                Violation(
                    rule="VW027",
                    severity=Severity.INFO,
                    message="Consider using Dock or Anchor properties for responsive layout",
                    file=filename,
                )
            )

        # Check for TableLayoutPanel or FlowLayoutPanel
        has_layout_panel = "TableLayoutPanel" in content or "FlowLayoutPanel" in content

        # Check for hardcoded sizes
        hardcoded_sizes = re.findall(
            r"Size\s*=\s*new\s+Size\s*\(\s*\d+\s*,\s*\d+\s*\)", content
        )

        if hardcoded_sizes and len(hardcoded_sizes) > 5 and not has_layout_panel:
            violations.append(
                Violation(
                    rule="VW028",
                    severity=Severity.INFO,
                    message=f"Found {len(hardcoded_sizes)} hardcoded sizes - consider using layout panels for flexibility",
                    file=filename,
                )
            )

        return violations

    def _check_event_handlers(self, content: str, filename: str) -> List[Violation]:
        """Check for proper event handler patterns."""
        violations = []

        # Find event subscriptions
        event_subscriptions = re.findall(r"(\w+)\s*\+=\s*(\w+);", content)

        # Find event unsubscriptions
        event_unsubscriptions = re.findall(r"(\w+)\s*-=\s*(\w+);", content)

        # Check if events are subscribed but not unsubscribed
        subscribed = set(handler for _, handler in event_subscriptions)
        unsubscribed = set(handler for _, handler in event_unsubscriptions)

        leaked_handlers = subscribed - unsubscribed

        if leaked_handlers and len(leaked_handlers) > 0:
            violations.append(
                Violation(
                    rule="VW029",
                    severity=Severity.WARNING,
                    message=f"Found {len(leaked_handlers)} event handlers not unsubscribed (potential memory leak)",
                    file=filename,
                )
            )

        # Check for async void event handlers (should be avoided)
        async_void_handlers = re.findall(
            r"private\s+async\s+void\s+(\w+)\s*\(\s*object\s+sender,", content
        )

        if async_void_handlers:
            violations.append(
                Violation(
                    rule="VW030",
                    severity=Severity.WARNING,
                    message=f"Found {len(async_void_handlers)} async void event handlers - consider async Task and proper error handling",
                    file=filename,
                )
            )

        return violations

    def _check_async_patterns(self, content: str, filename: str) -> List[Violation]:
        """Check for proper async/await patterns."""
        violations = []

        # Check for ConfigureAwait(false) in library code
        awaits = re.findall(r"await\s+([^;]+);", content)

        if awaits:
            awaits_with_configure = [a for a in awaits if "ConfigureAwait" in a]

            # In UI code, ConfigureAwait(false) is not needed and can cause issues
            if awaits_with_configure:
                violations.append(
                    Violation(
                        rule="VW031",
                        severity=Severity.WARNING,
                        message="ConfigureAwait(false) should not be used in UI code (can cause context issues)",
                        file=filename,
                    )
                )

        # Check for Task.Run in UI methods
        if "Task.Run" in content:
            violations.append(
                Violation(
                    rule="VW032",
                    severity=Severity.INFO,
                    message="Task.Run found - ensure UI updates use Invoke/InvokeRequired",
                    file=filename,
                )
            )

        # Check for .Result or .Wait() (blocking calls)
        blocking_calls = re.findall(r"\.(?:Result|Wait\(\))", content)

        if blocking_calls:
            violations.append(
                Violation(
                    rule="VW033",
                    severity=Severity.ERROR,
                    message=f"Found {len(blocking_calls)} blocking async calls (.Result/.Wait) - use await instead",
                    file=filename,
                )
            )

        return violations

    def _get_line_number(self, content: str, position: int) -> int:
        """Get line number from string position."""
        return content[:position].count("\n") + 1


def print_header():
    """Print validation header."""
    print("\n╔════════════════════════════════════════════════════════════╗")
    print("║           WinForms View Validation Framework              ║")
    print("║           Syncfusion Controls & Theming Checker           ║")
    print("╚════════════════════════════════════════════════════════════╝\n")


def print_violations(violations: List[Violation], file: str):
    """Print violations for a file."""
    if not violations:
        return

    print(f"\n📄 {file}")
    print("─" * 60)

    for violation in violations:
        icon = (
            "❌"
            if violation.severity == Severity.ERROR
            else "⚠️ " if violation.severity == Severity.WARNING else "ℹ️ "
        )

        line_info = f" (Line {violation.line})" if violation.line else ""
        print(f"  {icon} [{violation.rule}] {violation.message}{line_info}")


def print_summary(all_violations: List[Violation], total_files: int):
    """Print validation summary."""
    errors = [v for v in all_violations if v.severity == Severity.ERROR]
    warnings = [v for v in all_violations if v.severity == Severity.WARNING]
    infos = [v for v in all_violations if v.severity == Severity.INFO]

    print("\n╔════════════════════════════════════════════════════════════╗")
    print("║                    Validation Summary                      ║")
    print("╚════════════════════════════════════════════════════════════╝")

    if errors:
        print(f"  ❌ Errors:   {len(errors)}")
    else:
        print("  ✅ Errors:   0")

    if warnings:
        print(f"  ⚠️  Warnings: {len(warnings)}")
    else:
        print("  ✅ Warnings: 0")

    if infos:
        print(f"  ℹ️  Info:     {len(infos)}")

    print()

    return {"Errors": len(errors), "Warnings": len(warnings), "Info": len(infos)}


def main():
    """Main entry point."""
    import argparse

    parser = argparse.ArgumentParser(
        description="Validate WinForms views for Syncfusion controls and theming"
    )
    parser.add_argument(
        "--path",
        type=Path,
        default=Path(__file__).parent.parent.parent,
        help="Root path to scan for views",
    )
    parser.add_argument(
        "--fail-on-violations",
        action="store_true",
        help="Exit with non-zero code when violations are found",
    )
    parser.add_argument(
        "--generate-report",
        action="store_true",
        help="Generate a JSON report of all findings",
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=Path("view-validation-report.json"),
        help="Path for the JSON report",
    )
    parser.add_argument("--verbose", action="store_true", help="Enable verbose output")

    args = parser.parse_args()

    print_header()

    # Find all Form files
    forms_path = args.path / "src" / "WileyWidget.WinForms" / "Forms"

    if not forms_path.exists():
        print(f"❌ Forms directory not found: {forms_path}")
        return 1

    view_files = list(forms_path.glob("*.cs"))
    view_files = [f for f in view_files if not f.name.endswith(".Designer.cs")]

    if not view_files:
        print(f"❌ No view files found in {forms_path}")
        return 1

    print(f"Found {len(view_files)} view files to validate...\n")

    # Validate all files
    validator = ViewValidator(verbose=args.verbose)
    all_violations = []

    for view_file in view_files:
        violations = validator.validate_file(view_file)

        if violations:
            all_violations.extend(violations)
            print_violations(violations, view_file.name)

    # Display summary
    summary = print_summary(all_violations, len(view_files))

    # Generate report if requested
    if args.generate_report:
        report = {
            "Timestamp": datetime.now().isoformat(),
            "TotalFiles": len(view_files),
            "Summary": summary,
            "Violations": [v.to_dict() for v in all_violations],
        }

        output_path = args.path / args.output
        output_path.write_text(json.dumps(report, indent=2), encoding="utf-8")
        print(f"📊 Report generated: {output_path}")

    # Exit with appropriate code
    if args.fail_on_violations and (summary["Errors"] > 0 or summary["Warnings"] > 0):
        print(
            f"\n❌ Validation failed with {summary['Errors']} errors and {summary['Warnings']} warnings"
        )
        return 1

    if all_violations:
        return 0
    else:
        print("🎉 All views passed validation!")
        print(f"   All {len(view_files)} views are properly configured.")
        return 0


if __name__ == "__main__":
    sys.exit(main())
