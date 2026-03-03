#!/usr/bin/env python3
"""Ship blocker auditor for Wiley Widget.

This script scans source files for known and likely release blockers, then emits:
- Reports/ship_blocker_audit.json
- Reports/ship_blocker_audit.md

The report includes:
- Exact file/line locations
- Severity and blocker category
- Concrete development actions needed to ship
"""

from __future__ import annotations

import argparse
import json
import re
from dataclasses import asdict, dataclass
from datetime import UTC, datetime
from pathlib import Path
from typing import Any, Iterable, Sequence

SEVERITY_ORDER: dict[str, int] = {
    "critical": 0,
    "high": 1,
    "medium": 2,
    "low": 3,
}

PANEL_CLASS_PATTERN = re.compile(
    r"\bclass\s+(?P<name>\w*Panel)\s*:\s*(?:UserControl|ScopedPanelBase(?:<[^>]+>)?)"
)
METHOD_SIGNATURE_PATTERN = re.compile(
    r"\b(?:protected|private|public)\s+(?:override\s+)?(?:async\s+)?(?:void|Task(?:<[^>]+>)?)\s+(OnShown|OnLoad)\s*\("
)
HEAVY_ONSHOWN_WORK_PATTERN = re.compile(
    r"ShowPanel<|ActivatorUtilities|CreateInstance\(|EnsureRightDock|LoadWorkspaceLayout\(|InitializeLayoutComponents\(",
    re.IGNORECASE,
)
LAYOUT_MANAGER_PATTERN = re.compile(r"DockingManager|TabbedMDIManager|DockStateChanged")
LAYOUT_SYNC_CALL_PATTERN = re.compile(
    r"PerformLayout\(|TriggerForceFullLayout\(|ForceFullLayout\(|RequestMdiConstrain\("
)
DISPOSE_CALL_PATTERN = re.compile(r"\.Dispose\s*\(")
DISPOSE_GUARD_PATTERN = re.compile(r"IsHandleCreated|IsDisposed|Disposing|if\s*\(")


def is_comment_line(line: str) -> bool:
    """Return True for single-line comments."""

    return line.strip().startswith("//")


def is_excluded_source_file(path: Path) -> bool:
    """Apply common file exclusions used by heuristics."""

    normalized = str(path).replace("\\", "/")
    if "/tests/" in normalized:
        return True
    if normalized.endswith(".Designer.cs"):
        return True
    if normalized.endswith("MainForm.Testing.cs"):
        return True
    return False


def find_method_block(
    lines: Sequence[str], start_line_index: int
) -> tuple[int, int] | None:
    """Return inclusive 1-based method body range using brace matching from a signature line index."""

    brace_depth = 0
    body_started = False

    for index in range(start_line_index, len(lines)):
        line = lines[index]
        opens = line.count("{")
        closes = line.count("}")
        if opens > 0:
            body_started = True
        brace_depth += opens - closes

        if body_started and brace_depth <= 0:
            return (start_line_index + 1, index + 1)

    return None


@dataclass(frozen=True)
class Finding:
    """A single ship-blocker or release-risk finding."""

    rule_id: str
    category: str
    severity: str
    title: str
    detail: str
    location: str
    needed_development_item: str
    matched_text: str


@dataclass(frozen=True)
class Rule:
    """Line-level scan rule."""

    rule_id: str
    category: str
    severity: str
    title: str
    detail_template: str
    needed_development_item: str
    pattern: re.Pattern[str]
    include_paths: tuple[str, ...] = ("src/**/*.cs",)
    exclude_path_contains: tuple[str, ...] = ()
    exclude_line_regex: re.Pattern[str] | None = None


def parse_args() -> argparse.Namespace:
    """Parse CLI arguments."""

    parser = argparse.ArgumentParser(
        description="Audit Wiley Widget workspace for shipping blockers and release risks."
    )
    parser.add_argument("--root", type=Path, default=Path.cwd())
    parser.add_argument("--json-out", type=Path, default=None)
    parser.add_argument("--md-out", type=Path, default=None)
    parser.add_argument(
        "--fail-on",
        choices=("critical", "high", "medium", "low", "none"),
        default="none",
        help="Exit non-zero when findings at or above this severity are present.",
    )
    parser.add_argument(
        "--strict",
        action="store_true",
        help="Strict mode for CI: equivalent to --fail-on high.",
    )
    return parser.parse_args()


def iter_files(root: Path, include_patterns: Iterable[str]) -> Iterable[Path]:
    """Yield source files matching include globs."""

    seen: set[Path] = set()
    for pattern in include_patterns:
        for path in root.glob(pattern):
            if path in seen:
                continue
            if not path.is_file():
                continue
            if any(
                part in {"bin", "obj", ".git", "TestResults"} for part in path.parts
            ):
                continue
            seen.add(path)
            yield path


def read_lines(path: Path) -> list[str]:
    """Read text file as UTF-8 with tolerant fallback."""

    try:
        return path.read_text(encoding="utf-8", errors="ignore").splitlines()
    except OSError:
        return []


def location(path: Path, line_no: int, root: Path) -> str:
    """Build consistent workspace-relative location format."""

    return f"{path.relative_to(root)}:{line_no}"


def matches_exclusions(rule: Rule, path: Path, line: str) -> bool:
    """Return True when finding should be suppressed for this path/line."""

    normalized = str(path).replace("\\", "/")
    for part in rule.exclude_path_contains:
        if part.replace("\\", "/") in normalized:
            return True

    if rule.exclude_line_regex and rule.exclude_line_regex.search(line):
        return True

    return False


def scan_with_rule(root: Path, rule: Rule) -> list[Finding]:
    """Scan workspace with one rule and return findings."""

    findings: list[Finding] = []

    for path in iter_files(root, rule.include_paths):
        lines = read_lines(path)
        if not lines:
            continue

        for line_no, line in enumerate(lines, start=1):
            if not rule.pattern.search(line):
                continue

            if matches_exclusions(rule, path, line):
                continue

            findings.append(
                Finding(
                    rule_id=rule.rule_id,
                    category=rule.category,
                    severity=rule.severity,
                    title=rule.title,
                    detail=rule.detail_template,
                    location=location(path, line_no, root),
                    needed_development_item=rule.needed_development_item,
                    matched_text=line.strip()[:220],
                )
            )

    return findings


def analyze_accessibility_missing(root: Path) -> list[Finding]:
    """Detect panel classes that appear to miss accessibility metadata."""

    findings: list[Finding] = []

    scoped_panel_base_path = (
        root
        / "src"
        / "WileyWidget.WinForms"
        / "Controls"
        / "Base"
        / "ScopedPanelBase.cs"
    )
    scoped_panel_base_lines = read_lines(scoped_panel_base_path)
    scoped_base_has_accessible_name = any(
        "AccessibleName" in line for line in scoped_panel_base_lines
    )
    scoped_base_has_accessible_role = any(
        "AccessibleRole" in line for line in scoped_panel_base_lines
    )

    for path in iter_files(root, ("src/WileyWidget.WinForms/Controls/Panels/*.cs",)):
        if is_excluded_source_file(path):
            continue

        lines = read_lines(path)
        if not lines:
            continue

        class_line_no = -1
        for line_no, line in enumerate(lines, start=1):
            match = PANEL_CLASS_PATTERN.search(line)
            if match:
                class_line_no = line_no
                break

        if class_line_no < 0:
            continue

        has_accessible_name = any("AccessibleName" in line for line in lines)
        has_accessible_role = any("AccessibleRole" in line for line in lines)

        class_signature = lines[class_line_no - 1]
        is_scoped_panel_base_derived = "ScopedPanelBase" in class_signature

        if is_scoped_panel_base_derived:
            has_accessible_name = has_accessible_name or scoped_base_has_accessible_name
            has_accessible_role = has_accessible_role or scoped_base_has_accessible_role

        if has_accessible_name and has_accessible_role:
            continue

        missing_parts: list[str] = []
        if not has_accessible_name:
            missing_parts.append("AccessibleName")
        if not has_accessible_role:
            missing_parts.append("AccessibleRole")

        findings.append(
            Finding(
                rule_id="accessibility-missing",
                category="accessibility",
                severity="high",
                title="Panel missing AccessibleName or Role (Microsoft accessibility requirement)",
                detail=(
                    "All controls must expose automation properties. "
                    f"Detected missing metadata: {', '.join(missing_parts)}"
                ),
                location=location(path, class_line_no, root),
                needed_development_item=(
                    "Add .AccessibleName and .AccessibleRole assignments for controls and key containers in this panel."
                ),
                matched_text=lines[class_line_no - 1].strip()[:220],
            )
        )

    return findings


def analyze_heavy_onshown_work(root: Path) -> list[Finding]:
    """Detect heavy calls inside OnLoad/OnShown handlers."""

    findings: list[Finding] = []

    for path in iter_files(root, ("src/WileyWidget.WinForms/**/*.cs",)):
        if is_excluded_source_file(path):
            continue

        lines = read_lines(path)
        if not lines:
            continue

        for line_no, line in enumerate(lines, start=1):
            if not METHOD_SIGNATURE_PATTERN.search(line):
                continue

            method_range = find_method_block(lines, line_no - 1)
            if method_range is None:
                continue

            start, end = method_range
            for method_line_no in range(start, end + 1):
                method_line = lines[method_line_no - 1]
                if is_comment_line(method_line):
                    continue
                if not HEAVY_ONSHOWN_WORK_PATTERN.search(method_line):
                    continue

                findings.append(
                    Finding(
                        rule_id="heavy-onshown-work",
                        category="ui-responsiveness",
                        severity="high",
                        title="Heavy work in OnShown/OnLoad (Microsoft responsiveness violation)",
                        detail=(
                            "OnShown/OnLoad should remain lightweight and avoid expensive panel realization or DI-heavy creation paths."
                        ),
                        location=location(path, method_line_no, root),
                        needed_development_item=(
                            "Move heavy work to deferred timers or IAsyncInitializable flows after first paint."
                        ),
                        matched_text=method_line.strip()[:220],
                    )
                )

    return findings


def analyze_missing_performlayout(root: Path) -> list[Finding]:
    """Detect docking manager usage without explicit layout sync calls in same file."""

    findings: list[Finding] = []

    for path in iter_files(root, ("src/WileyWidget.WinForms/**/*.cs",)):
        if is_excluded_source_file(path):
            continue
        normalized_path = str(path).replace("\\", "/")
        if "/Services/Abstractions/" in normalized_path:
            continue

        lines = read_lines(path)
        if not lines:
            continue

        first_layout_line = -1
        for line_no, line in enumerate(lines, start=1):
            if is_comment_line(line):
                continue
            if LAYOUT_MANAGER_PATTERN.search(line):
                first_layout_line = line_no
                break

        if first_layout_line < 0:
            continue

        has_layout_sync = any(
            not is_comment_line(line) and LAYOUT_SYNC_CALL_PATTERN.search(line)
            for line in lines
        )
        if has_layout_sync:
            continue

        findings.append(
            Finding(
                rule_id="missing-performlayout",
                category="layout-stability",
                severity="medium",
                title="Docking change without PerformLayout / ForceFullLayout (Syncfusion 32.2.3)",
                detail=(
                    "This file uses docking manager patterns but no explicit layout synchronization call was found."
                ),
                location=location(path, first_layout_line, root),
                needed_development_item=(
                    "Add PerformLayout or TriggerForceFullLayout after major docking state changes."
                ),
                matched_text=lines[first_layout_line - 1].strip()[:220],
            )
        )

    return findings


def analyze_dispose_race(root: Path) -> list[Finding]:
    """Detect unguarded Dispose calls that may race with handle/create-dispose transitions."""

    findings: list[Finding] = []

    for path in iter_files(
        root,
        (
            "src/WileyWidget.WinForms/Forms/MainForm/*.cs",
            "src/WileyWidget.WinForms/Forms/RightDockPanelFactory.cs",
            "src/WileyWidget.WinForms/Controls/Base/ScopedPanelBase.cs",
            "src/WileyWidget.WinForms/Services/PanelNavigationService.cs",
        ),
    ):
        if is_excluded_source_file(path):
            continue

        lines = read_lines(path)
        if not lines:
            continue

        for line_no, line in enumerate(lines, start=1):
            if is_comment_line(line):
                continue
            if not DISPOSE_CALL_PATTERN.search(line):
                continue
            if "base.Dispose(" in line:
                continue

            dispose_target_hint = line.lower()
            if not any(
                token in dispose_target_hint
                for token in ("panel", "control", "dock", "ribbon", "form", "tab")
            ):
                continue

            start_window = max(1, line_no - 4)
            context_text = "\n".join(lines[start_window - 1 : line_no])
            if DISPOSE_GUARD_PATTERN.search(context_text):
                continue

            findings.append(
                Finding(
                    rule_id="dispose-race",
                    category="resource-management",
                    severity="high",
                    title="Potential CreateHandle/Dispose race (Syncfusion 32.2.3 known issue)",
                    detail=(
                        "Dispose call appears without nearby handle/disposal guards; this can be risky during dynamic UI initialization and teardown."
                    ),
                    location=location(path, line_no, root),
                    needed_development_item=(
                        "Guard dispose paths with IsHandleCreated/IsDisposed/Disposing checks where needed for dynamic controls."
                    ),
                    matched_text=line.strip()[:220],
                )
            )

    return findings


def build_rules() -> list[Rule]:
    """Build detection rules for known blockers and broad release gaps."""

    return [
        Rule(
            rule_id="not-implemented",
            category="implementation-completeness",
            severity="critical",
            title="Runtime path throws NotImplementedException",
            detail_template="A not-implemented runtime code path can hard-fail user workflows.",
            needed_development_item="Implement the method end-to-end or remove/gate the path from production navigation.",
            pattern=re.compile(r"throw\s+new\s+NotImplementedException\s*\("),
            include_paths=("src/**/*.cs",),
            exclude_path_contains=("/tests/", "MainForm.Testing.cs"),
        ),
        Rule(
            rule_id="placeholder-implementation",
            category="implementation-completeness",
            severity="high",
            title="Placeholder implementation in production code",
            detail_template="Placeholder logic indicates partially developed behavior that may not satisfy intended runtime outcomes.",
            needed_development_item="Replace placeholder behavior with production implementation and add integration validation for the affected workflow.",
            pattern=re.compile(
                r"placeholder implementation|placeholder for future|replace with real implementation|coming soon",
                re.IGNORECASE,
            ),
            include_paths=("src/**/*.cs",),
            exclude_path_contains=("/tests/", "MainForm.Testing.cs"),
            exclude_line_regex=re.compile(r"PlaceholderText\s*=", re.IGNORECASE),
        ),
        Rule(
            rule_id="dummy-external-call",
            category="integration-readiness",
            severity="high",
            title="Dummy/simulated external integration call detected",
            detail_template="Simulation or dummy endpoints in production services indicate incomplete external integration paths.",
            needed_development_item="Replace simulated HTTP calls with real provider integration, resilient error handling, and verified contract tests.",
            pattern=re.compile(
                r"new\s+Uri\(\s*\"http://dummy\"\s*\)|simulate\s+http|simulate\s+db\s+health\s+check",
                re.IGNORECASE,
            ),
            include_paths=("src/**/*.cs",),
            exclude_path_contains=("/tests/", "MainForm.Testing.cs"),
        ),
        Rule(
            rule_id="report-preview-placeholder",
            category="feature-completeness",
            severity="high",
            title="Report preview remains placeholder-only",
            detail_template="Reports UI includes explicit placeholder-only preview behavior rather than production rendering implementation.",
            needed_development_item="Implement production report rendering/viewer flow and validate export + preview user journeys end-to-end.",
            pattern=re.compile(
                r"placeholder\s+panel|placeholder\s+Panel", re.IGNORECASE
            ),
            include_paths=("src/WileyWidget.WinForms/Controls/Panels/ReportsPanel.cs",),
        ),
        Rule(
            rule_id="dev-legal-placeholder",
            category="compliance-readiness",
            severity="critical",
            title="Development placeholder legal/policy endpoint",
            detail_template="Sandbox placeholder privacy/EULA content is not suitable for production release compliance.",
            needed_development_item="Publish production privacy/EULA content and validate endpoint ownership/domain configuration for release.",
            pattern=re.compile(
                r"Privacy Policy \(Sandbox\)|EULA \(Sandbox\)|development placeholder",
                re.IGNORECASE,
            ),
            include_paths=("src/WileyWidget.Webhooks/**/*.cs",),
        ),
        Rule(
            rule_id="stub-runtime-fallback",
            category="runtime-resilience",
            severity="high",
            title="Runtime degraded to stub/fallback UI path",
            detail_template="Fallback stub paths can mask initialization failures and leave features partially functional at runtime.",
            needed_development_item="Harden initialization/factory paths, add failure telemetry with root-cause capture, and ensure functional fallback behavior or disable feature gracefully.",
            pattern=re.compile(
                r"degrading to stub|fallback stub panel|stub right dock panel",
                re.IGNORECASE,
            ),
            include_paths=("src/WileyWidget.WinForms/Forms/MainForm/*.cs",),
            exclude_path_contains=("MainForm.Testing.cs",),
        ),
        Rule(
            rule_id="obsolete-incomplete",
            category="architecture-readiness",
            severity="high",
            title="Obsolete/incomplete architecture path present",
            detail_template="An obsolete incomplete subsystem indicates unresolved architecture work that may block or confuse production flows.",
            needed_development_item="Finalize the subsystem or remove dead routes/references and document supported architecture path for release.",
            pattern=re.compile(r"incomplete|Not implemented", re.IGNORECASE),
            include_paths=(
                "src/WileyWidget.WinForms/Forms/MainTabbedLayoutFactory.cs",
            ),
        ),
        Rule(
            rule_id="syncfusion-skinmanager-only",
            category="theming-compliance",
            severity="high",
            title="Direct color/style set bypasses SfSkinManager (Syncfusion 32.2.3 violation)",
            detail_template="Syncfusion best practice requires SfSkinManager to remain the primary theming authority.",
            needed_development_item="Replace direct BackColor/ForeColor/ThemeName assignments with SfSkinManager.SetVisualStyle and ControlFactory patterns where possible.",
            pattern=re.compile(r"(?:\.|\b)(BackColor|ForeColor|ThemeName)\s*="),
            include_paths=("src/WileyWidget.WinForms/Controls/Panels/*.cs",),
            exclude_path_contains=("/tests/", "Designer.cs"),
            exclude_line_regex=re.compile(
                r"Color\.(Red|Green|Orange|OrangeRed)|//\s*Semantic|PlaceholderText\s*=",
                re.IGNORECASE,
            ),
        ),
        Rule(
            rule_id="blazorwebview-dependency",
            category="runtime-resilience",
            severity="critical",
            title="JARVIS references missing BlazorWebView dependency",
            detail_template="Microsoft.WinForms.Utilities.Shared.dll is required for BlazorWebView-powered runtime paths.",
            needed_development_item="Ensure required runtime dependency is installed/bundled and remove production stub fallback behavior.",
            pattern=re.compile(
                r"Could not load file or assembly.*Microsoft\.WinForms\.Utilities\.Shared\.dll|"
                r"FileNotFoundException.*Microsoft\.WinForms\.Utilities\.Shared\.dll|"
                r"Missing required dependency.*Microsoft\.WinForms\.Utilities\.Shared\.dll",
                re.IGNORECASE,
            ),
            include_paths=(
                "logs/**/*.txt",
                "logs/**/*.log",
            ),
            exclude_path_contains=("/tests/",),
        ),
        Rule(
            rule_id="secrets-in-code",
            category="security",
            severity="critical",
            title="Potential secret in source",
            detail_template="Potential API key/secret literal detected in source. Secrets should come from configuration or secure stores.",
            needed_development_item="Remove literal secret values and use IConfiguration/user-secrets/environment variables.",
            pattern=re.compile(
                r"\b(?:xai-|sk-)[A-Za-z0-9_-]{20,}\b|\bapi[_-]?key\b\s*=\s*[\"'][^\"']{20,}[\"']",
                re.IGNORECASE,
            ),
            include_paths=("src/**/*.cs",),
            exclude_path_contains=("/tests/",),
            exclude_line_regex=re.compile(r"PLACEHOLDER|example|sample", re.IGNORECASE),
        ),
    ]


def deduplicate(findings: list[Finding]) -> list[Finding]:
    """Deduplicate findings by rule and exact source location."""

    unique: dict[tuple[str, str], Finding] = {}
    for item in findings:
        key = (item.rule_id, item.location)
        if key not in unique:
            unique[key] = item

    return list(unique.values())


def sort_findings(findings: list[Finding]) -> list[Finding]:
    """Sort by severity then location."""

    return sorted(
        findings,
        key=lambda f: (
            SEVERITY_ORDER.get(f.severity, 99),
            f.location.lower(),
            f.rule_id,
        ),
    )


def summarize(findings: list[Finding]) -> dict[str, Any]:
    """Build summary counters for report metadata."""

    by_severity = {
        "critical": 0,
        "high": 0,
        "medium": 0,
        "low": 0,
    }
    by_category: dict[str, int] = {}

    for finding in findings:
        by_severity[finding.severity] = by_severity.get(finding.severity, 0) + 1
        by_category[finding.category] = by_category.get(finding.category, 0) + 1

    return {
        "total": len(findings),
        "bySeverity": by_severity,
        "byCategory": dict(sorted(by_category.items())),
        "shipBlocked": by_severity["critical"] > 0 or by_severity["high"] > 0,
    }


def write_json_report(path: Path, payload: dict[str, Any]) -> None:
    """Write machine-readable JSON report."""

    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2), encoding="utf-8")


def write_markdown_report(
    path: Path, payload: dict[str, Any], findings: list[Finding]
) -> None:
    """Write human-readable markdown report."""

    summary = payload["summary"]
    lines: list[str] = [
        "# Ship Blocker Audit",
        "",
        f"Generated: {payload['generatedAtUtc']}",
        "",
        "## Summary",
        f"- Total findings: **{summary['total']}**",
        f"- Critical: **{summary['bySeverity']['critical']}**",
        f"- High: **{summary['bySeverity']['high']}**",
        f"- Medium: **{summary['bySeverity']['medium']}**",
        f"- Low: **{summary['bySeverity']['low']}**",
        f"- Ship blocked: **{summary['shipBlocked']}**",
        "",
        "## Findings",
        "",
    ]

    if not findings:
        lines.append("No blockers detected by current rule set.")
    else:
        for item in findings:
            lines.extend(
                [
                    f"### [{item.severity.upper()}] {item.title}",
                    f"- Category: `{item.category}`",
                    f"- Location: `{item.location}`",
                    f"- Evidence: `{item.matched_text}`",
                    f"- Detail: {item.detail}",
                    f"- Needed development item: {item.needed_development_item}",
                    "",
                ]
            )

    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines), encoding="utf-8")


def severity_meets_fail_threshold(severity: str, fail_on: str) -> bool:
    """Return True when severity should trigger non-zero exit based on threshold."""

    if fail_on == "none":
        return False

    severity_rank = SEVERITY_ORDER.get(severity, 99)
    threshold_rank = SEVERITY_ORDER.get(fail_on, 99)
    return severity_rank <= threshold_rank


def main() -> int:
    """Run the audit and emit reports."""

    args = parse_args()
    root = args.root.resolve()

    effective_fail_on = "high" if args.strict else args.fail_on

    findings: list[Finding] = []
    for rule in build_rules():
        findings.extend(scan_with_rule(root, rule))

    findings.extend(analyze_accessibility_missing(root))
    findings.extend(analyze_heavy_onshown_work(root))
    findings.extend(analyze_missing_performlayout(root))
    findings.extend(analyze_dispose_race(root))

    findings = sort_findings(deduplicate(findings))

    summary = summarize(findings)
    payload: dict[str, Any] = {
        "generatedAtUtc": datetime.now(UTC).isoformat(),
        "workspaceRoot": str(root),
        "summary": summary,
        "findings": [asdict(item) for item in findings],
    }

    json_out = (
        args.json_out.resolve()
        if args.json_out
        else root / "Reports" / "ship_blocker_audit.json"
    )
    md_out = (
        args.md_out.resolve()
        if args.md_out
        else root / "Reports" / "ship_blocker_audit.md"
    )

    write_json_report(json_out, payload)
    write_markdown_report(md_out, payload, findings)

    summary = payload["summary"]
    print(
        "Ship blocker audit completed: "
        f"total={summary['total']} critical={summary['bySeverity']['critical']} "
        f"high={summary['bySeverity']['high']} medium={summary['bySeverity']['medium']} "
        f"low={summary['bySeverity']['low']} fail_on={effective_fail_on}"
    )
    print(f"JSON report: {json_out}")
    print(f"Markdown report: {md_out}")

    should_fail = any(
        severity_meets_fail_threshold(item.severity, effective_fail_on)
        for item in findings
    )
    return 1 if should_fail else 0


if __name__ == "__main__":
    raise SystemExit(main())
