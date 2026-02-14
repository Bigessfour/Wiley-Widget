#!/usr/bin/env python3.14
"""
Panel Visibility Diagnostic Script
Identifies blocking behavior preventing Syncfusion DockingManager panels from appearing.

Usage:
    python tools/diagnose-panel-visibility.py [--fix] [--verbose]

Based on analysis from approved-workflow.md and Syncfusion v32.x+ best practices.
"""

import argparse
import json
import re
import sys
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any


@dataclass
class Issue:
    """Represents a detected panel visibility issue."""

    severity: str  # "critical", "high", "medium", "low"
    category: str
    file_path: Path
    line_num: int
    line_content: str
    description: str
    fix_suggestion: str
    auto_fixable: bool = False


@dataclass
class DiagnosticReport:
    """Complete diagnostic report for panel visibility issues."""

    issues: list[Issue] = field(default_factory=list)
    summary: dict[str, Any] = field(default_factory=dict)
    docking_system_used: str = ""
    panels_created: list[str] = field(default_factory=list)
    theme_application_timing: str = ""


class PanelVisibilityDiagnostic:
    """Diagnose panel visibility blocking issues in WinForms docking setup."""

    def __init__(self, workspace_root: Path, verbose: bool = False):
        self.workspace_root = workspace_root
        self.verbose = verbose
        self.report = DiagnosticReport()

    def log(self, message: str) -> None:
        """Log message if verbose mode enabled."""
        if self.verbose:
            print(f"[DEBUG] {message}")

    def scan_for_visible_false_assignments(self) -> None:
        """Scan for critical 'Visible = false' assignments that block panels."""
        self.log("Scanning for Visible=false assignments...")

        patterns = [
            (
                r"hostControl\.Visible\s*=\s*false",
                "DockingHostFactory hostControl visibility blocker",
            ),
            (
                r"_dockingHost\.Visible\s*=\s*false",
                "MainForm docking host visibility blocker",
            ),
            (r"leftDockPanel\.Visible\s*=\s*false", "Left panel explicitly hidden"),
            (r"rightDockPanel\.Visible\s*=\s*false", "Right panel explicitly hidden"),
            (
                r"centralDocumentPanel\.Visible\s*=\s*false",
                "Central panel explicitly hidden",
            ),
        ]

        cs_files = list(self.workspace_root.rglob("src/**/*.cs"))

        for cs_file in cs_files:
            if any(x in str(cs_file) for x in ["bin", "obj", ".git"]):
                continue

            try:
                content = cs_file.read_text(encoding="utf-8")
                lines = content.splitlines()

                for line_num, line in enumerate(lines, start=1):
                    for pattern, description in patterns:
                        if re.search(pattern, line):
                            # Check if it's in a comment
                            if "//" in line and line.index("//") < line.index(
                                "Visible"
                            ):
                                continue

                            self.report.issues.append(
                                Issue(
                                    severity="critical",
                                    category="visibility_blocker",
                                    file_path=cs_file,
                                    line_num=line_num,
                                    line_content=line.strip(),
                                    description=f"CRITICAL: {description} - Sets Visible=false which prevents panels from rendering",
                                    fix_suggestion="Remove this line or set to 'true' after panels are docked",
                                    auto_fixable=True,
                                )
                            )
                            self.log(f"Found visibility blocker: {cs_file}:{line_num}")
            except Exception as e:
                self.log(f"Error scanning {cs_file}: {e}")

    def check_docking_system_usage(self) -> None:
        """Identify which docking initialization system is actually being used."""
        self.log("Checking docking system usage...")

        mainform_docking = (
            self.workspace_root
            / "src/WileyWidget.WinForms/Forms/MainForm/MainForm.Docking.cs"
        )

        # Check if MainForm calls DockingHostFactory
        if mainform_docking.exists():
            content = mainform_docking.read_text(encoding="utf-8")
            if "DockingHostFactory.CreateDockingHost" in content:
                self.report.docking_system_used = "DockingHostFactory (factory pattern)"
                self.log("MainForm uses DockingHostFactory - recommended pattern")
            elif "private void InitializeSyncfusionDocking()" in content:
                self.report.docking_system_used = (
                    "MainForm.Docking.cs (direct initialization)"
                )
                self.log("MainForm uses direct docking initialization (not factory)")
                self.report.issues.append(
                    Issue(
                        severity="high",
                        category="architecture",
                        file_path=mainform_docking,
                        line_num=0,
                        line_content="",
                        description="MainForm.Docking.cs does NOT use DockingHostFactory - creates panels directly",
                        fix_suggestion="Consider migrating to DockingHostFactory.CreateDockingHost for consistency and testability",
                        auto_fixable=False,
                    )
                )
            else:
                self.report.issues.append(
                    Issue(
                        severity="critical",
                        category="architecture",
                        file_path=mainform_docking,
                        line_num=0,
                        line_content="",
                        description="CRITICAL: Cannot determine docking initialization system",
                        fix_suggestion="Ensure InitializeSyncfusionDocking() is implemented in MainForm.Docking.cs",
                        auto_fixable=False,
                    )
                )

    def check_panel_creation(self) -> None:
        """Verify panels are created with correct visibility."""
        self.log("Checking panel creation...")

        mainform_docking = (
            self.workspace_root
            / "src/WileyWidget.WinForms/Forms/MainForm/MainForm.Docking.cs"
        )

        if mainform_docking.exists():
            content = mainform_docking.read_text(encoding="utf-8")
            lines = content.splitlines()

            # Check if using factory pattern (panels created by factory)
            if "DockingHostFactory.CreateDockingHost" in content:
                self.log("Using DockingHostFactory - panel creation handled by factory")
                # Factory creates panels, so skip detailed panel creation checks
                return

            # Track panel creation
            panel_patterns = {
                "leftDockPanel": r"_leftDockPanel\s*=\s*new\s+Panel",
                "rightDockPanel": r"_rightDockPanel\s*=\s*new\s+Panel",
                "centralDocumentPanel": r"_centralDocumentPanel\s*=\s*new\s+Panel",
                "dockingHost": r"_dockingHost\s*=\s*new\s+ContainerControl",
            }

            for panel_name, pattern in panel_patterns.items():
                found = False
                for line_num, line in enumerate(lines, start=1):
                    if re.search(pattern, line):
                        self.report.panels_created.append(panel_name)
                        self.log(
                            f"Found panel creation: {panel_name} at line {line_num}"
                        )
                        found = True

                        # Check if Visible is set in creation block
                        # Look ahead a few lines
                        for offset in range(0, min(10, len(lines) - line_num)):
                            check_line = lines[line_num + offset - 1]
                            if (
                                "Visible = false" in check_line
                                and panel_name.replace("_", "") in check_line
                            ):
                                self.report.issues.append(
                                    Issue(
                                        severity="critical",
                                        category="visibility_blocker",
                                        file_path=mainform_docking,
                                        line_num=line_num + offset,
                                        line_content=check_line.strip(),
                                        description=f"Panel {panel_name} created with Visible=false",
                                        fix_suggestion="Remove Visible=false or set to true",
                                        auto_fixable=True,
                                    )
                                )
                        break

                if not found and panel_name != "dockingHost":
                    self.report.issues.append(
                        Issue(
                            severity="medium",
                            category="panel_creation",
                            file_path=mainform_docking,
                            line_num=0,
                            line_content="",
                            description=f"Panel {panel_name} not found in CreateDockPanels()",
                            fix_suggestion="Verify panel is created in CreateDockPanels() method",
                            auto_fixable=False,
                        )
                    )

    def check_z_order_issues(self) -> None:
        """Check for Z-order issues with ribbon/status bar covering panels."""
        self.log("Checking Z-order configuration...")

        mainform_docking = (
            self.workspace_root
            / "src/WileyWidget.WinForms/Forms/MainForm/MainForm.Docking.cs"
        )

        if mainform_docking.exists():
            content = mainform_docking.read_text(encoding="utf-8")

            # If using factory pattern, host management is internal
            uses_factory = "DockingHostFactory.CreateDockingHost" in content

            if not uses_factory:
                # Check for proper Z-order management
                has_sendtoback = "_dockingHost?.SendToBack()" in content
                if not has_sendtoback:
                    self.report.issues.append(
                        Issue(
                            severity="high",
                            category="z_order",
                            file_path=mainform_docking,
                            line_num=0,
                            line_content="",
                            description="Docking host not sent to back - may be covered by other controls",
                            fix_suggestion="Add '_dockingHost?.SendToBack();' after docking host creation",
                            auto_fixable=False,
                        )
                    )
            else:
                self.log("Using DockingHostFactory - Z-order handled by factory")

            # Always check ribbon/status bar (independent of factory)
            has_bringtofront_ribbon = "_ribbon?.BringToFront()" in content
            has_bringtofront_status = "_statusBar?.BringToFront()" in content

            if not (has_bringtofront_ribbon and has_bringtofront_status):
                self.report.issues.append(
                    Issue(
                        severity="medium",
                        category="z_order",
                        file_path=mainform_docking,
                        line_num=0,
                        line_content="",
                        description="Ribbon/StatusBar not brought to front - may not be visible",
                        fix_suggestion="Add '_ribbon?.BringToFront(); _statusBar?.BringToFront();' in ConfigureDockingManagerChromeLayout()",
                        auto_fixable=False,
                    )
                )

    def check_theme_application_timing(self) -> None:
        """Check when theme is applied relative to panel creation."""
        self.log("Checking theme application timing...")

        mainform_cs = (
            self.workspace_root / "src/WileyWidget.WinForms/Forms/MainForm/MainForm.cs"
        )
        mainform_docking = (
            self.workspace_root
            / "src/WileyWidget.WinForms/Forms/MainForm/MainForm.Docking.cs"
        )

        # Check if theme applied in constructor (too early)
        if mainform_cs.exists():
            content = mainform_cs.read_text(encoding="utf-8")
            lines = content.splitlines()

            in_constructor = False
            for line_num, line in enumerate(lines, start=1):
                if "public MainForm(" in line:
                    in_constructor = True
                elif in_constructor and "ThemeColors.ApplyTheme" in line:
                    # Theme in constructor is OK (global theme)
                    self.report.theme_application_timing = (
                        "Constructor (global theme - OK)"
                    )
                    self.log("Theme applied in constructor (acceptable)")
                    in_constructor = False
                elif in_constructor and (
                    "protected override void OnLoad" in line
                    or "protected override void OnShown" in line
                ):
                    in_constructor = False

        # Check if theme applied after docking initialization
        if mainform_docking.exists():
            content = mainform_docking.read_text(encoding="utf-8")

            if "private void ApplyDockingTheme()" in content:
                self.report.theme_application_timing = "Post-docking (ideal)"
                self.log("Theme has dedicated ApplyDockingTheme() method (good)")
            else:
                self.report.issues.append(
                    Issue(
                        severity="low",
                        category="theme_timing",
                        file_path=mainform_docking,
                        line_num=0,
                        line_content="",
                        description="No dedicated ApplyDockingTheme() method found",
                        fix_suggestion="Add ApplyDockingTheme() method to apply theme after docking initialization",
                        auto_fixable=False,
                    )
                )

    def check_legacy_gradient_panel_issues(self) -> None:
        """Check if LegacyGradientPanel has custom paint issues."""
        self.log("Checking LegacyGradientPanel implementation...")

        legacy_panel = (
            self.workspace_root
            / "src/WileyWidget.WinForms/Controls/Base/LegacyGradientPanel.cs"
        )

        if legacy_panel.exists():
            content = legacy_panel.read_text(encoding="utf-8")

            # Check if it's just a stub (good)
            if (
                "DEPRECATED: This class exists only for backward compatibility"
                in content
            ):
                self.log(
                    "LegacyGradientPanel is just a stub/wrapper - no custom paint issues"
                )
            elif "protected override void OnPaint" in content:
                # Has custom paint - check if base.OnPaint called
                if "base.OnPaint" not in content:
                    self.report.issues.append(
                        Issue(
                            severity="high",
                            category="custom_paint",
                            file_path=legacy_panel,
                            line_num=0,
                            line_content="",
                            description="LegacyGradientPanel overrides OnPaint but doesn't call base.OnPaint",
                            fix_suggestion="Add 'base.OnPaint(e);' in OnPaint override",
                            auto_fixable=False,
                        )
                    )

    def check_docking_host_integration(self) -> None:
        """Check if docking host is properly integrated into MainForm."""
        self.log("Checking docking host integration...")

        mainform_docking = (
            self.workspace_root
            / "src/WileyWidget.WinForms/Forms/MainForm/MainForm.Docking.cs"
        )

        if mainform_docking.exists():
            content = mainform_docking.read_text(encoding="utf-8")

            # If using factory pattern, host integration is handled by factory
            uses_factory = "DockingHostFactory.CreateDockingHost" in content

            if not uses_factory:
                # Check if docking host added to form controls
                if "Controls.Add(_dockingHost)" not in content:
                    self.report.issues.append(
                        Issue(
                            severity="critical",
                            category="integration",
                            file_path=mainform_docking,
                            line_num=0,
                            line_content="",
                            description="CRITICAL: Docking host not added to MainForm.Controls",
                            fix_suggestion="Add 'Controls.Add(_dockingHost);' in InitializeSyncfusionDocking()",
                            auto_fixable=False,
                        )
                    )

                # Check if docking host docked to fill
                if "_dockingHost.Dock = DockStyle.Fill" not in content:
                    self.report.issues.append(
                        Issue(
                            severity="high",
                            category="integration",
                            file_path=mainform_docking,
                            line_num=0,
                            line_content="",
                            description="Docking host Dock property not set to Fill",
                            fix_suggestion="Set '_dockingHost.Dock = DockStyle.Fill;' in InitializeSyncfusionDocking()",
                            auto_fixable=False,
                        )
                    )
            else:
                self.log(
                    "Using DockingHostFactory - host integration handled by factory"
                )
                return

    def check_suspend_resume_layout_balance(self) -> None:
        """Check for SuspendLayout without matching ResumeLayout."""
        self.log("Checking SuspendLayout/ResumeLayout balance...")

        cs_files = list(self.workspace_root.rglob("src/**/*.cs"))

        for cs_file in cs_files:
            if any(x in str(cs_file) for x in ["bin", "obj", ".git"]):
                continue

            try:
                content = cs_file.read_text(encoding="utf-8")
                lines = content.splitlines()

                suspend_count = 0
                resume_count = 0

                for line_num, line in enumerate(lines, start=1):
                    if (
                        "SuspendLayout()" in line
                        and "//" not in line[: line.index("SuspendLayout")]
                    ):
                        suspend_count += 1
                    if (
                        "ResumeLayout" in line
                        and "//" not in line[: line.index("ResumeLayout")]
                    ):
                        resume_count += 1

                if suspend_count > resume_count:
                    self.report.issues.append(
                        Issue(
                            severity="high",
                            category="layout_suspend",
                            file_path=cs_file,
                            line_num=0,
                            line_content="",
                            description=f"SuspendLayout/ResumeLayout imbalance: {suspend_count} suspends, {resume_count} resumes - controls won't render",
                            fix_suggestion="Ensure every SuspendLayout() has matching ResumeLayout(true)",
                            auto_fixable=False,
                        )
                    )
            except (OSError, ValueError, UnicodeDecodeError) as exc:
                self.log(f"Error checking layout balance in {cs_file}: {exc}")

    def check_begin_end_init_balance(self) -> None:
        """Check for BeginInit without matching EndInit."""
        self.log("Checking BeginInit/EndInit balance...")

        docking_factory = (
            self.workspace_root / "src/WileyWidget.WinForms/Forms/DockingHostFactory.cs"
        )

        if docking_factory.exists():
            content = docking_factory.read_text(encoding="utf-8")

            begin_count = content.count("BeginInit()")
            end_count = content.count("EndInit()")

            if begin_count > end_count:
                self.report.issues.append(
                    Issue(
                        severity="high",
                        category="init_balance",
                        file_path=docking_factory,
                        line_num=0,
                        line_content="",
                        description=f"BeginInit/EndInit imbalance: {begin_count} begins, {end_count} ends - DockingManager not properly initialized",
                        fix_suggestion="Ensure BeginInit() has matching EndInit() in finally block",
                        auto_fixable=False,
                    )
                )

    def check_enable_docking_calls(self) -> None:
        """Check if SetEnableDocking is called for all panels."""
        self.log("Checking SetEnableDocking calls...")

        mainform_docking = (
            self.workspace_root
            / "src/WileyWidget.WinForms/Forms/MainForm/MainForm.Docking.cs"
        )
        docking_factory = (
            self.workspace_root / "src/WileyWidget.WinForms/Forms/DockingHostFactory.cs"
        )

        # Check mainform first
        if mainform_docking.exists():
            content = mainform_docking.read_text(encoding="utf-8")
            uses_factory = "DockingHostFactory.CreateDockingHost" in content

            if uses_factory:
                # Factory handles SetEnableDocking internally
                self.log(
                    "Using DockingHostFactory - SetEnableDocking handled by factory"
                )
                return

        # Only check if not using factory
        for file_path in [mainform_docking, docking_factory]:
            if not file_path.exists():
                continue

            content = file_path.read_text(encoding="utf-8")

            # Check if panels are created but not enabled for docking
            has_left_panel = "leftDockPanel" in content or "_leftDockPanel" in content
            has_right_panel = (
                "rightDockPanel" in content or "_rightDockPanel" in content
            )
            has_central_panel = (
                "centralDocumentPanel" in content or "_centralDocumentPanel" in content
            )

            has_enable_docking = "SetEnableDocking" in content

            if (
                has_left_panel or has_right_panel or has_central_panel
            ) and not has_enable_docking:
                self.report.issues.append(
                    Issue(
                        severity="critical",
                        category="docking_config",
                        file_path=file_path,
                        line_num=0,
                        line_content="",
                        description="CRITICAL: Panels created but SetEnableDocking() never called - panels won't dock",
                        fix_suggestion="Call dockingManager.SetEnableDocking(panel, true) for each panel",
                        auto_fixable=False,
                    )
                )
                break

    def check_dock_control_calls(self) -> None:
        """Check if DockControl is called to actually dock the panels."""
        self.log("Checking DockControl calls...")

        mainform_docking = (
            self.workspace_root
            / "src/WileyWidget.WinForms/Forms/MainForm/MainForm.Docking.cs"
        )
        docking_factory = (
            self.workspace_root / "src/WileyWidget.WinForms/Forms/DockingHostFactory.cs"
        )

        for file_path in [mainform_docking, docking_factory]:
            if not file_path.exists():
                continue

            content = file_path.read_text(encoding="utf-8")

            # Check if SetEnableDocking exists but DockControl doesn't
            has_enable_docking = "SetEnableDocking" in content
            has_dock_control = "DockControl" in content

            if has_enable_docking and not has_dock_control:
                self.report.issues.append(
                    Issue(
                        severity="critical",
                        category="docking_config",
                        file_path=file_path,
                        line_num=0,
                        line_content="",
                        description="CRITICAL: SetEnableDocking called but DockControl never called - panels enabled but not docked",
                        fix_suggestion="Call dockingManager.DockControl(panel, host, DockingStyle.Left/Right, width) for each panel",
                        auto_fixable=False,
                    )
                )
                break

    def check_perform_layout_calls(self) -> None:
        """Check if PerformLayout is called after adding controls."""
        self.log("Checking PerformLayout calls...")

        mainform_docking = (
            self.workspace_root
            / "src/WileyWidget.WinForms/Forms/MainForm/MainForm.Docking.cs"
        )

        if mainform_docking.exists():
            content = mainform_docking.read_text(encoding="utf-8")

            has_controls_add = "Controls.Add" in content
            has_perform_layout = "PerformLayout" in content

            if has_controls_add and not has_perform_layout:
                self.report.issues.append(
                    Issue(
                        severity="medium",
                        category="layout_refresh",
                        file_path=mainform_docking,
                        line_num=0,
                        line_content="",
                        description="Controls added but PerformLayout() never called - layout may not refresh",
                        fix_suggestion="Call PerformLayout() after adding all controls to force layout refresh",
                        auto_fixable=False,
                    )
                )

    def check_size_and_bounds_issues(self) -> None:
        """Check for potential size/bounds issues that could make panels invisible."""
        self.log("Checking size and bounds configurations...")

        cs_files = []
        for pattern in ["**/MainForm*.cs", "**/DockingHostFactory.cs"]:
            cs_files.extend(self.workspace_root.rglob(pattern))

        for cs_file in cs_files:
            if any(x in str(cs_file) for x in ["bin", "obj", ".git"]):
                continue

            try:
                content = cs_file.read_text(encoding="utf-8")
                lines = content.splitlines()

                # Check for zero size assignments
                for line_num, line in enumerate(lines, start=1):
                    if re.search(
                        r"(Width|Height|Size)\s*=\s*(0|new\s+Size\(0[^0-9])", line
                    ):
                        if "//" in line and line.index("/") < line.index("="):
                            continue

                        self.report.issues.append(
                            Issue(
                                severity="high",
                                category="size_bounds",
                                file_path=cs_file,
                                line_num=line_num,
                                line_content=line.strip(),
                                description="Control size set to zero - will be invisible",
                                fix_suggestion="Set proper Width/Height or use Dock/Anchor properties",
                                auto_fixable=False,
                            )
                        )
            except Exception as exc:
                self.log(f"Error checking size/bounds in {cs_file}: {exc}")

    def check_opacity_issues(self) -> None:
        """Check for Opacity set to 0 making controls invisible."""
        self.log("Checking opacity settings...")

        cs_files = list(self.workspace_root.rglob("src/**/*.cs"))

        for cs_file in cs_files:
            if any(x in str(cs_file) for x in ["bin", "obj", ".git"]):
                continue

            try:
                content = cs_file.read_text(encoding="utf-8")
                lines = content.splitlines()

                for line_num, line in enumerate(lines, start=1):
                    if re.search(r"Opacity\s*=\s*0", line):
                        if "//" in line:
                            continue

                        self.report.issues.append(
                            Issue(
                                severity="medium",
                                category="opacity",
                                file_path=cs_file,
                                line_num=line_num,
                                line_content=line.strip(),
                                description="Opacity set to 0 - control will be invisible",
                                fix_suggestion="Remove opacity setting or set to 1.0",
                                auto_fixable=True,
                            )
                        )
            except Exception as exc:
                self.log(f"Error checking opacity in {cs_file}: {exc}")

    def check_parent_not_set(self) -> None:
        """Check if controls are created but never added to a parent."""
        self.log("Checking for orphaned controls (no parent)...")

        mainform_docking = (
            self.workspace_root
            / "src/WileyWidget.WinForms/Forms/MainForm/MainForm.Docking.cs"
        )
        docking_factory = (
            self.workspace_root / "src/WileyWidget.WinForms/Forms/DockingHostFactory.cs"
        )

        for file_path in [mainform_docking, docking_factory]:
            if not file_path.exists():
                continue

            content = file_path.read_text(encoding="utf-8")
            lines = content.splitlines()

            # Track panel creation and parent assignment
            panels_created = set()
            panels_parented = set()

            for line in lines:
                # Track creation
                if match := re.search(
                    r"(\w+DockPanel|_\w+DockPanel|\w+Host)\s*=\s*new\s+", line
                ):
                    panel_name = match.group(1)
                    panels_created.add(panel_name)

                # Track parenting
                if match := re.search(
                    r"Controls\.Add\((\w+(?:DockPanel|Host))\)", line
                ):
                    panel_name = match.group(1)
                    panels_parented.add(panel_name)
                if match := re.search(r"DockControl\((\w+(?:DockPanel|Host))", line):
                    panel_name = match.group(1)
                    panels_parented.add(panel_name)

            orphaned = panels_created - panels_parented
            if orphaned:
                for panel in orphaned:
                    self.report.issues.append(
                        Issue(
                            severity="high",
                            category="parent_missing",
                            file_path=file_path,
                            line_num=0,
                            line_content="",
                            description=f"Control '{panel}' created but never added to parent - will not be visible",
                            fix_suggestion=f"Add '{panel}' to parent via Controls.Add() or DockControl()",
                            auto_fixable=False,
                        )
                    )

    def check_handle_creation_timing(self) -> None:
        """Check if controls are manipulated before handle is created."""
        self.log("Checking handle creation timing...")

        mainform_docking = (
            self.workspace_root
            / "src/WileyWidget.WinForms/Forms/MainForm/MainForm.Docking.cs"
        )

        if mainform_docking.exists():
            content = mainform_docking.read_text(encoding="utf-8")

            # Check if CreateHandle or EnsureCreated is called
            if "IsHandleCreated" not in content and "EnsureCreated" not in content:
                self.report.issues.append(
                    Issue(
                        severity="low",
                        category="handle_timing",
                        file_path=mainform_docking,
                        line_num=0,
                        line_content="",
                        description="No handle creation checks - controls might be manipulated before handles exist",
                        fix_suggestion="Check IsHandleCreated before manipulating control properties",
                        auto_fixable=False,
                    )
                )

    def check_event_handler_wiring(self) -> None:
        """Check if critical layout events are wired up."""
        self.log("Checking event handler wiring...")

        mainform_docking = (
            self.workspace_root
            / "src/WileyWidget.WinForms/Forms/MainForm/MainForm.Docking.cs"
        )

        if mainform_docking.exists():
            content = mainform_docking.read_text(encoding="utf-8")

            # Check for NewDockStateEndLoad handler (important for Syncfusion)
            if "NewDockStateEndLoad" not in content:
                self.report.issues.append(
                    Issue(
                        severity="medium",
                        category="event_handler",
                        file_path=mainform_docking,
                        line_num=0,
                        line_content="",
                        description="NewDockStateEndLoad event not handled - layout may not apply correctly",
                        fix_suggestion="Subscribe to dockingManager.NewDockStateEndLoad to ensure layout is applied",
                        auto_fixable=False,
                    )
                )

    def generate_summary(self) -> None:
        """Generate summary statistics for the report."""
        self.report.summary = {
            "total_issues": len(self.report.issues),
            "critical": len(
                [i for i in self.report.issues if i.severity == "critical"]
            ),
            "high": len([i for i in self.report.issues if i.severity == "high"]),
            "medium": len([i for i in self.report.issues if i.severity == "medium"]),
            "low": len([i for i in self.report.issues if i.severity == "low"]),
            "auto_fixable": len([i for i in self.report.issues if i.auto_fixable]),
            "docking_system": self.report.docking_system_used,
            "panels_created": self.report.panels_created,
            "theme_timing": self.report.theme_application_timing,
        }

    def run_diagnostics(self) -> DiagnosticReport:
        """Run all diagnostic checks."""
        print("🔍 Starting Panel Visibility Diagnostics (Enhanced)...")
        print(f"📂 Workspace: {self.workspace_root}")
        print()

        # Core visibility blockers
        self.scan_for_visible_false_assignments()
        self.check_docking_system_usage()
        self.check_panel_creation()
        self.check_z_order_issues()
        self.check_theme_application_timing()
        self.check_legacy_gradient_panel_issues()
        self.check_docking_host_integration()

        # Layout and initialization issues
        self.check_suspend_resume_layout_balance()
        self.check_begin_end_init_balance()

        # Syncfusion-specific configuration
        self.check_enable_docking_calls()
        self.check_dock_control_calls()

        # Control state and size issues
        self.check_perform_layout_calls()
        self.check_size_and_bounds_issues()
        self.check_opacity_issues()
        self.check_parent_not_set()

        # Timing and event issues
        self.check_handle_creation_timing()
        self.check_event_handler_wiring()

        self.generate_summary()

        return self.report

    def print_report(self) -> None:
        """Print human-readable report."""
        report = self.report

        print("=" * 80)
        print("PANEL VISIBILITY DIAGNOSTIC REPORT")
        print("=" * 80)
        print()

        print("📊 SUMMARY")
        print(f"  Total Issues: {report.summary['total_issues']}")
        print(f"  🔴 Critical: {report.summary['critical']}")
        print(f"  🟠 High: {report.summary['high']}")
        print(f"  🟡 Medium: {report.summary['medium']}")
        print(f"  🟢 Low: {report.summary['low']}")
        print(f"  ✅ Auto-fixable: {report.summary['auto_fixable']}")
        print()

        print(f"🏗️  Docking System: {report.summary['docking_system']}")
        print(
            f"📦 Panels Created: {', '.join(report.summary['panels_created']) or 'None detected'}"
        )
        print(f"🎨 Theme Timing: {report.summary['theme_timing'] or 'Not detected'}")
        print()

        if report.issues:
            print("=" * 80)
            print("ISSUES FOUND")
            print("=" * 80)
            print()

            # Group by severity
            for severity in ["critical", "high", "medium", "low"]:
                severity_issues = [i for i in report.issues if i.severity == severity]
                if not severity_issues:
                    continue

                icon = {"critical": "🔴", "high": "🟠", "medium": "🟡", "low": "🟢"}[
                    severity
                ]
                print(f"{icon} {severity.upper()} Issues ({len(severity_issues)})")
                print("-" * 80)

                for idx, issue in enumerate(severity_issues, start=1):
                    print(f"\n{idx}. [{issue.category.upper()}] {issue.description}")
                    if issue.line_num > 0:
                        print(
                            f"   📄 File: {issue.file_path.relative_to(self.workspace_root)}:{issue.line_num}"
                        )
                        print(f"   📝 Code: {issue.line_content}")
                    else:
                        print(
                            f"   📄 File: {issue.file_path.relative_to(self.workspace_root)}"
                        )
                    print(f"   💡 Fix: {issue.fix_suggestion}")
                    if issue.auto_fixable:
                        print("   ✅ Auto-fixable: Yes")
                print()
        else:
            print("✅ No issues found! Panels should be visible.")

        print("=" * 80)

    def export_json(self, output_path: Path) -> None:
        """Export report as JSON."""
        data = {
            "summary": self.report.summary,
            "issues": [
                {
                    "severity": i.severity,
                    "category": i.category,
                    "file": str(i.file_path.relative_to(self.workspace_root)),
                    "line": i.line_num,
                    "code": i.line_content,
                    "description": i.description,
                    "fix": i.fix_suggestion,
                    "auto_fixable": i.auto_fixable,
                }
                for i in self.report.issues
            ],
        }

        output_path.write_text(json.dumps(data, indent=2), encoding="utf-8")
        print(f"📄 JSON report exported to: {output_path}")


def main() -> int:
    """Main entry point."""
    parser = argparse.ArgumentParser(
        description="Diagnose panel visibility blocking issues in Syncfusion DockingManager"
    )
    parser.add_argument(
        "--verbose", "-v", action="store_true", help="Enable verbose logging"
    )
    parser.add_argument(
        "--json", type=Path, help="Export report as JSON to specified path"
    )
    parser.add_argument(
        "--workspace",
        type=Path,
        default=Path.cwd(),
        help="Workspace root directory (default: current directory)",
    )

    args = parser.parse_args()

    workspace = args.workspace.resolve()
    if not workspace.exists():
        print(f"❌ Error: Workspace not found: {workspace}", file=sys.stderr)
        return 1

    diagnostic = PanelVisibilityDiagnostic(workspace, verbose=args.verbose)
    diagnostic.run_diagnostics()
    diagnostic.print_report()

    if args.json:
        diagnostic.export_json(args.json)

    # Return exit code based on critical issues
    critical_count = diagnostic.report.summary["critical"]
    if critical_count > 0:
        print(
            f"\n❌ {critical_count} critical issue(s) found. Panels are likely blocked."
        )
        return 1
    else:
        print("\n✅ No critical issues found.")
        return 0


if __name__ == "__main__":
    sys.exit(main())
