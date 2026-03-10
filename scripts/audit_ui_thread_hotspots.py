#!/usr/bin/env python3
"""UI-thread hotspot auditor for Wiley Widget WinForms startup paths.

This script combines:
1) Log analysis for blocking/slow startup phases.
2) Static code scanning for known UI-thread anti-patterns.

Outputs:
- JSON report (machine-readable)
- Markdown report (human-readable)

Default output paths:
- Reports/thread_hotspot_audit.json
- Reports/thread_hotspot_audit.md

Microsoft guidance references used for rule design:
- https://learn.microsoft.com/dotnet/desktop/winforms/controls/how-to-make-thread-safe-calls
- https://learn.microsoft.com/dotnet/desktop/winforms/forms/events#async-event-handlers
- https://learn.microsoft.com/dotnet/api/system.windows.forms.control.invokeasync
"""

from __future__ import annotations

import argparse
import json
import re
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Iterable, Sequence


@dataclass(frozen=True)
class Finding:
    """Represents one detected weak spot."""

    source: str
    category: str
    severity: str
    title: str
    detail: str
    location: str
    recommendation: str


@dataclass(frozen=True)
class AuditConfig:
    """Thresholds and configurable paths for the audit run."""

    workspace_root: Path
    src_root: Path
    logs_root: Path
    reports_root: Path
    blocking_phase_ms: int = 2500
    slow_phase_ms: int = 2000
    deferred_phase_warn_ms: int = 1500


SEVERITY_ORDER = {"critical": 0, "high": 1, "medium": 2, "low": 3}


LOG_PATTERNS: Sequence[tuple[str, re.Pattern[str]]] = (
    (
        "timeline_blocking_phase",
        re.compile(
            r"BLOCKING PHASE: '([^']+)' took ([0-9]+(?:\.[0-9]+)?)ms", re.IGNORECASE
        ),
    ),
    (
        "timeline_slow_phase",
        re.compile(
            r"SLOW PHASE: '([^']+)' took ([0-9]+(?:\.[0-9]+)?)ms", re.IGNORECASE
        ),
    ),
    (
        "deferred_primary_panel",
        re.compile(
            r"Deferred Phase\s+3a\s+\(Enterprise Vital Signs\)\s+in\s+([0-9]+)ms",
            re.IGNORECASE,
        ),
    ),
    (
        "initialize_chrome",
        re.compile(r"InitializeChrome completed in\s+([0-9]+)ms", re.IGNORECASE),
    ),
)


SYNC_WAIT_PATTERN = re.compile(r"\.Wait\(|GetAwaiter\(\)\.GetResult\(|Thread\.Sleep\(")
BLOCKING_RESULT_PATTERN = re.compile(
    r"\)\s*\.Result\b|\b\w*task\w*\.Result\b", re.IGNORECASE
)
BEGININVOKE_PATTERN = re.compile(r"\bBeginInvoke\s*\(")
INVOKE_PATTERN = re.compile(r"\bInvoke\s*\(")

# Heuristic: potentially expensive work inside OnLoad/OnShown methods or invoke callbacks.
HOT_PATH_PATTERN = re.compile(
    r"ShowPanel<|EnsureRightDockPanelInitialized\(|LoadWorkspaceLayout\(|"
    r"ActivatorUtilities\.CreateInstance|InitializeLayoutComponents\(|"
    r"CreateRightDockPanel\(|SaveWorkspaceLayout\("
)

# Warn when sync InvokeAsync overload appears to carry async-looking work.
INVOKEASYNC_SYNC_OVERLOAD_HINT = re.compile(
    r"InvokeAsync\s*\(\s*\(?(?:Action|Func<[^>]+>)?\s*\)?\s*\("
)


def parse_args() -> argparse.Namespace:
    """Parse command-line arguments."""

    parser = argparse.ArgumentParser(
        description="Audit WinForms startup UI-thread weak spots using logs + static scan."
    )
    parser.add_argument(
        "--root",
        type=Path,
        default=Path.cwd(),
        help="Workspace root (defaults to current working directory).",
    )
    parser.add_argument(
        "--json-out",
        type=Path,
        default=None,
        help="Optional override path for JSON output.",
    )
    parser.add_argument(
        "--md-out",
        type=Path,
        default=None,
        help="Optional override path for Markdown output.",
    )
    parser.add_argument(
        "--blocking-ms",
        type=int,
        default=2500,
        help="Critical threshold (ms) for blocking phase findings.",
    )
    parser.add_argument(
        "--slow-ms",
        type=int,
        default=2000,
        help="High threshold (ms) for slow phase findings.",
    )
    parser.add_argument(
        "--deferred-ms",
        type=int,
        default=1500,
        help="Warn threshold (ms) for deferred primary panel phase.",
    )
    return parser.parse_args()


def build_config(args: argparse.Namespace) -> AuditConfig:
    """Build runtime config from parsed args."""

    root = args.root.resolve()
    return AuditConfig(
        workspace_root=root,
        src_root=root / "src" / "WileyWidget.WinForms",
        logs_root=root / "logs",
        reports_root=root / "Reports",
        blocking_phase_ms=args.blocking_ms,
        slow_phase_ms=args.slow_ms,
        deferred_phase_warn_ms=args.deferred_ms,
    )


def iter_text_files(root: Path, patterns: Sequence[str]) -> Iterable[Path]:
    """Yield files matching glob patterns under root."""

    if not root.exists():
        return

    for pattern in patterns:
        yield from root.glob(pattern)


def analyze_logs(config: AuditConfig) -> list[Finding]:
    """Analyze startup and error logs for measured UI-thread pressure."""

    findings: list[Finding] = []
    log_files = sorted(
        {
            *iter_text_files(
                config.logs_root,
                ["startup-*.txt", "errors-*.log", "wiley-widget-*.log"],
            ),
        }
    )

    for file_path in log_files:
        try:
            content = file_path.read_text(
                encoding="utf-8", errors="ignore"
            ).splitlines()
        except OSError:
            continue

        for line_no, line in enumerate(content, start=1):
            for pattern_name, pattern in LOG_PATTERNS:
                match = pattern.search(line)
                if not match:
                    continue

                if pattern_name == "timeline_blocking_phase":
                    phase_name = match.group(1)
                    duration = float(match.group(2))
                    if duration >= config.blocking_phase_ms:
                        findings.append(
                            Finding(
                                source="logs",
                                category="ui-thread-blocking",
                                severity="critical",
                                title=f"Blocking startup phase: {phase_name}",
                                detail=f"Measured {duration:.1f}ms on UI thread (>= {config.blocking_phase_ms}ms).",
                                location=f"{file_path.relative_to(config.workspace_root)}:{line_no}",
                                recommendation=(
                                    "Split phase into smaller chunks; keep UI callbacks short and move non-UI work to Task.Run."
                                ),
                            )
                        )

                elif pattern_name == "timeline_slow_phase":
                    phase_name = match.group(1)
                    duration = float(match.group(2))
                    if duration >= config.slow_phase_ms:
                        findings.append(
                            Finding(
                                source="logs",
                                category="startup-latency",
                                severity="high",
                                title=f"Slow startup phase: {phase_name}",
                                detail=f"Measured {duration:.1f}ms (>= {config.slow_phase_ms}ms).",
                                location=f"{file_path.relative_to(config.workspace_root)}:{line_no}",
                                recommendation=(
                                    "Delay non-essential UI construction until after first paint and ensure async APIs are used."
                                ),
                            )
                        )

                elif pattern_name == "deferred_primary_panel":
                    duration = int(match.group(1))
                    if duration >= config.deferred_phase_warn_ms:
                        severity = (
                            "high" if duration >= config.blocking_phase_ms else "medium"
                        )
                        findings.append(
                            Finding(
                                source="logs",
                                category="deferred-panel-load",
                                severity=severity,
                                title="Deferred Enterprise Vital Signs panel is slow",
                                detail=f"Deferred phase took {duration}ms (threshold {config.deferred_phase_warn_ms}ms).",
                                location=f"{file_path.relative_to(config.workspace_root)}:{line_no}",
                                recommendation=(
                                    "Show lightweight shell first; defer heavy layout/control realization to later UI turns."
                                ),
                            )
                        )

                elif pattern_name == "initialize_chrome":
                    duration = int(match.group(1))
                    if duration >= config.slow_phase_ms:
                        findings.append(
                            Finding(
                                source="logs",
                                category="chrome-init",
                                severity="high",
                                title="Chrome initialization exceeds budget",
                                detail=f"InitializeChrome completed in {duration}ms.",
                                location=f"{file_path.relative_to(config.workspace_root)}:{line_no}",
                                recommendation=(
                                    "Lazy-create non-essential ribbon groups and defer expensive refresh/layout calls."
                                ),
                            )
                        )

    return findings


def method_context(lines: Sequence[str], index: int) -> str:
    """Return a compact method/context marker around a line index."""

    start = max(0, index - 40)
    for i in range(index, start, -1):
        line = lines[i].strip()
        if (
            line.startswith("private ")
            or line.startswith("protected ")
            or line.startswith("public ")
        ):
            return line[:120]
    return "<unknown-context>"


def strip_single_line_comments(line: str) -> str:
    """Strip // comments while preserving the code portion before comments."""

    return line.split("//", maxsplit=1)[0].rstrip()


def analyze_code(config: AuditConfig) -> list[Finding]:
    """Analyze C# sources for UI-thread anti-pattern heuristics."""

    findings: list[Finding] = []
    if not config.src_root.exists():
        return findings

    for file_path in sorted(config.src_root.rglob("*.cs")):
        try:
            lines = file_path.read_text(encoding="utf-8", errors="ignore").splitlines()
        except OSError:
            continue

        in_onshown_or_onload = False
        brace_depth = 0

        for idx, raw_line in enumerate(lines):
            line_no = idx + 1
            line = strip_single_line_comments(raw_line).strip()

            if re.search(r"\bOnShown\s*\(|\bOnLoad\s*\(", line):
                in_onshown_or_onload = True
                brace_depth = line.count("{") - line.count("}")
            elif in_onshown_or_onload:
                brace_depth += line.count("{") - line.count("}")
                if brace_depth <= 0 and "}" in line:
                    in_onshown_or_onload = False

            if line and (
                SYNC_WAIT_PATTERN.search(line) or BLOCKING_RESULT_PATTERN.search(line)
            ):
                findings.append(
                    Finding(
                        source="code",
                        category="blocking-call",
                        severity="critical",
                        title="Blocking wait in UI code path",
                        detail=f"Found blocking call pattern: {line[:120]}",
                        location=f"{file_path.relative_to(config.workspace_root)}:{line_no}",
                        recommendation="Replace with async/await and avoid .Result/.Wait/Thread.Sleep on UI code paths.",
                    )
                )

            if in_onshown_or_onload and HOT_PATH_PATTERN.search(line):
                findings.append(
                    Finding(
                        source="code",
                        category="startup-hotpath",
                        severity="high",
                        title="Potential heavy work in OnShown/OnLoad",
                        detail=f"Startup lifecycle includes potential heavy call: {line[:120]}",
                        location=f"{file_path.relative_to(config.workspace_root)}:{line_no}",
                        recommendation=(
                            "Move non-essential work to deferred async pipeline and keep OnShown/OnLoad callback short."
                        ),
                    )
                )

            if BEGININVOKE_PATTERN.search(line) or INVOKE_PATTERN.search(line):
                # Look ahead small window for known expensive operations in invoke callback.
                window = "\n".join(lines[idx : min(len(lines), idx + 18)])
                if HOT_PATH_PATTERN.search(window):
                    findings.append(
                        Finding(
                            source="code",
                            category="invoke-callback-weight",
                            severity="high",
                            title="Heavy work inside UI invoke callback",
                            detail=(
                                "Invoke/BeginInvoke callback appears to contain heavy operation(s) "
                                "that can starve the message pump."
                            ),
                            location=f"{file_path.relative_to(config.workspace_root)}:{line_no}",
                            recommendation=(
                                "Use invoke callback only for short UI updates; move heavy logic to background work."
                            ),
                        )
                    )

            if INVOKEASYNC_SYNC_OVERLOAD_HINT.search(line):
                if "async" in line or "Task" in line:
                    findings.append(
                        Finding(
                            source="code",
                            category="invokeasync-overload",
                            severity="medium",
                            title="Check InvokeAsync overload usage",
                            detail=(
                                "Potential sync InvokeAsync overload used for async work. "
                                "Microsoft recommends async ValueTask overload for long-running callbacks."
                            ),
                            location=f"{file_path.relative_to(config.workspace_root)}:{line_no}",
                            recommendation=(
                                "Prefer InvokeAsync(Func<CancellationToken, ValueTask>) for async callbacks "
                                "and keep sync callbacks short."
                            ),
                        )
                    )

            if "Task.Run(" in line and (
                "OnShown" in method_context(lines, idx)
                or "OnLoad" in method_context(lines, idx)
            ):
                findings.append(
                    Finding(
                        source="code",
                        category="startup-background-work",
                        severity="low",
                        title="Background work launched during startup lifecycle",
                        detail=f"Task.Run found in startup context: {method_context(lines, idx)}",
                        location=f"{file_path.relative_to(config.workspace_root)}:{line_no}",
                        recommendation=(
                            "Confirm cancellation, exception handling, and that UI updates marshal back safely."
                        ),
                    )
                )

    return findings


def deduplicate_findings(findings: Sequence[Finding]) -> list[Finding]:
    """Remove exact duplicate findings and keep stable ordering."""

    seen: set[tuple[str, str, str, str, str, str, str]] = set()
    deduped: list[Finding] = []

    for finding in findings:
        key = (
            finding.source,
            finding.category,
            finding.severity,
            finding.title,
            finding.detail,
            finding.location,
            finding.recommendation,
        )
        if key in seen:
            continue
        seen.add(key)
        deduped.append(finding)

    return deduped


def sort_findings(findings: Sequence[Finding]) -> list[Finding]:
    """Sort findings by severity, category, then location."""

    return sorted(
        findings,
        key=lambda f: (SEVERITY_ORDER.get(f.severity, 99), f.category, f.location),
    )


def write_json_report(findings: Sequence[Finding], output_path: Path) -> None:
    """Write machine-readable JSON report."""

    payload = {
        "findingCount": len(findings),
        "findings": [asdict(f) for f in findings],
    }
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(json.dumps(payload, indent=2), encoding="utf-8")


def summarize_by_severity(findings: Sequence[Finding]) -> dict[str, int]:
    """Count findings by severity label."""

    counts = {"critical": 0, "high": 0, "medium": 0, "low": 0}
    for finding in findings:
        counts[finding.severity] = counts.get(finding.severity, 0) + 1
    return counts


def write_markdown_report(findings: Sequence[Finding], output_path: Path) -> None:
    """Write human-readable Markdown report with actionable triage list."""

    counts = summarize_by_severity(findings)
    lines: list[str] = []
    lines.append("# UI Thread Hotspot Audit")
    lines.append("")
    lines.append(f"Total findings: **{len(findings)}**")
    lines.append(
        f"- Critical: {counts.get('critical', 0)} | High: {counts.get('high', 0)} | "
        f"Medium: {counts.get('medium', 0)} | Low: {counts.get('low', 0)}"
    )
    lines.append("")
    lines.append("## Weak Spots")
    lines.append("")

    if not findings:
        lines.append("No weak spots detected with current heuristics.")
    else:
        for finding in findings:
            lines.append(f"### [{finding.severity.upper()}] {finding.title}")
            lines.append(f"- Category: {finding.category}")
            lines.append(f"- Source: {finding.source}")
            lines.append(f"- Location: {finding.location}")
            lines.append(f"- Detail: {finding.detail}")
            lines.append(f"- Recommendation: {finding.recommendation}")
            lines.append("")

    lines.append("## Microsoft Guidance Basis")
    lines.append("")
    lines.append(
        "- https://learn.microsoft.com/dotnet/desktop/winforms/controls/how-to-make-thread-safe-calls"
    )
    lines.append(
        "- https://learn.microsoft.com/dotnet/desktop/winforms/forms/events#async-event-handlers"
    )
    lines.append(
        "- https://learn.microsoft.com/dotnet/api/system.windows.forms.control.invokeasync"
    )

    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def print_console_summary(
    findings: Sequence[Finding], json_out: Path, md_out: Path
) -> None:
    """Print concise CLI summary."""

    counts = summarize_by_severity(findings)
    print("=" * 78)
    print("UI THREAD HOTSPOT AUDIT")
    print("=" * 78)
    print(f"Findings: {len(findings)}")
    print(
        "Severity: "
        f"critical={counts.get('critical', 0)}, "
        f"high={counts.get('high', 0)}, "
        f"medium={counts.get('medium', 0)}, "
        f"low={counts.get('low', 0)}"
    )
    print(f"JSON report: {json_out}")
    print(f"Markdown report: {md_out}")

    if findings:
        print("\nTop weak spots:")
        for finding in findings[:8]:
            print(f"- [{finding.severity}] {finding.title} @ {finding.location}")


def main() -> int:
    """Program entry point."""

    args = parse_args()
    config = build_config(args)

    json_out = args.json_out or (config.reports_root / "thread_hotspot_audit.json")
    md_out = args.md_out or (config.reports_root / "thread_hotspot_audit.md")

    findings = []
    findings.extend(analyze_logs(config))
    findings.extend(analyze_code(config))

    findings = deduplicate_findings(findings)
    findings = sort_findings(findings)

    write_json_report(findings, json_out)
    write_markdown_report(findings, md_out)
    print_console_summary(findings, json_out, md_out)

    # Non-zero return only when critical issues are present.
    has_critical = any(f.severity == "critical" for f in findings)
    return 2 if has_critical else 0


if __name__ == "__main__":
    raise SystemExit(main())
