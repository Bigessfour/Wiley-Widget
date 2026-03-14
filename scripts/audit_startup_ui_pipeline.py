#!/usr/bin/env python3
"""Startup UI pipeline auditor for MainForm lifecycle methods.

This script focuses on documented responsiveness guidance for WinForms startup:
- Keep OnLoad/OnShown handlers short.
- Defer expensive work until after first paint.
- Avoid heavy panel/control realization in a single UI-thread turn.

Outputs:
- Reports/startup_ui_pipeline_audit.json
- Reports/startup_ui_pipeline_audit.md
"""

from __future__ import annotations

import argparse
import json
import re
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Iterable


@dataclass(frozen=True)
class Finding:
    source: str
    category: str
    severity: str
    title: str
    detail: str
    location: str
    recommendation: str


@dataclass(frozen=True)
class AuditConfig:
    workspace_root: Path
    reports_root: Path
    logs_root: Path
    program_cs: Path
    mainform_cs: Path
    mainform_init_cs: Path
    mainform_chrome_cs: Path
    onload_warn_ms: int = 400
    onshown_warn_ms: int = 120
    chrome_warn_ms: int = 1200
    blocking_phase_ms: int = 2500


SEVERITY_ORDER = {"critical": 0, "high": 1, "medium": 2, "low": 3}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Audit MainForm startup lifecycle for UI-thread bottlenecks."
    )
    parser.add_argument("--root", type=Path, default=Path.cwd())
    parser.add_argument("--json-out", type=Path, default=None)
    parser.add_argument("--md-out", type=Path, default=None)
    parser.add_argument("--onload-ms", type=int, default=400)
    parser.add_argument("--onshown-ms", type=int, default=120)
    parser.add_argument("--chrome-ms", type=int, default=1200)
    parser.add_argument("--blocking-ms", type=int, default=2500)
    return parser.parse_args()


def build_config(args: argparse.Namespace) -> AuditConfig:
    root = args.root.resolve()
    return AuditConfig(
        workspace_root=root,
        reports_root=root / "Reports",
        logs_root=root / "logs",
        program_cs=root / "src" / "WileyWidget.WinForms" / "Program.cs",
        mainform_cs=root
        / "src"
        / "WileyWidget.WinForms"
        / "Forms"
        / "MainForm"
        / "MainForm.cs",
        mainform_init_cs=root
        / "src"
        / "WileyWidget.WinForms"
        / "Forms"
        / "MainForm"
        / "MainForm.Initialization.cs",
        mainform_chrome_cs=root
        / "src"
        / "WileyWidget.WinForms"
        / "Forms"
        / "MainForm"
        / "MainForm.Chrome.cs",
        onload_warn_ms=args.onload_ms,
        onshown_warn_ms=args.onshown_ms,
        chrome_warn_ms=args.chrome_ms,
        blocking_phase_ms=args.blocking_ms,
    )


def read_lines(path: Path) -> list[str]:
    try:
        return path.read_text(encoding="utf-8", errors="ignore").splitlines()
    except OSError:
        return []


def find_method_block(
    lines: list[str], signature_regex: re.Pattern[str]
) -> tuple[int, int] | None:
    start_index = -1
    for i, line in enumerate(lines):
        if signature_regex.search(line):
            start_index = i
            break

    if start_index < 0:
        return None

    brace_depth = 0
    started_body = False
    for i in range(start_index, len(lines)):
        text = lines[i]
        opens = text.count("{")
        closes = text.count("}")
        if opens > 0:
            started_body = True
        brace_depth += opens - closes
        if started_body and brace_depth <= 0:
            return (start_index + 1, i + 1)

    return (start_index + 1, len(lines))


def scan_method_for_heavy_calls(
    file_path: Path,
    lines: list[str],
    method_name: str,
    method_range: tuple[int, int],
    heavy_patterns: list[tuple[str, str, str]],
    workspace_root: Path,
) -> list[Finding]:
    findings: list[Finding] = []
    start, end = method_range

    for line_no in range(start, end + 1):
        line = lines[line_no - 1].strip()
        if not line or line.startswith("//"):
            continue

        for pattern, severity, recommendation in heavy_patterns:
            if pattern in line:
                findings.append(
                    Finding(
                        source="code",
                        category=f"{method_name.lower()}-hotpath",
                        severity=severity,
                        title=f"Potential heavy work in {method_name}",
                        detail=f"{method_name} contains startup-expensive call: {line[:140]}",
                        location=f"{file_path.relative_to(workspace_root)}:{line_no}",
                        recommendation=recommendation,
                    )
                )

    return findings


def scan_method_for_modal_calls(
    file_path: Path,
    lines: list[str],
    method_name: str,
    method_range: tuple[int, int],
    severity: str,
    workspace_root: Path,
) -> list[Finding]:
    findings: list[Finding] = []
    start, end = method_range

    for line_no in range(start, end + 1):
        line = lines[line_no - 1].strip()
        if not line or line.startswith("//"):
            continue

        if (
            "MessageBox.Show(" in line
            or ".ShowDialog(" in line
            or "ShowErrorDialog(" in line
        ):
            findings.append(
                Finding(
                    source="code",
                    category="startup-modal-risk",
                    severity=severity,
                    title=f"Modal UI call in {method_name}",
                    detail=f"{method_name} may block startup waiting for user input: {line[:140]}",
                    location=f"{file_path.relative_to(workspace_root)}:{line_no}",
                    recommendation="Avoid modal dialogs on startup path; log and defer user prompts until UI is interactive.",
                )
            )

    return findings


def analyze_startup_methods(config: AuditConfig) -> list[Finding]:
    findings: list[Finding] = []

    onload_lines = read_lines(config.mainform_cs)
    init_lines = read_lines(config.mainform_init_cs)
    chrome_lines = read_lines(config.mainform_chrome_cs)

    onload_range = find_method_block(
        onload_lines,
        re.compile(r"protected\s+override\s+void\s+OnLoad\s*\(EventArgs\s+e\)"),
    )
    onshown_range = find_method_block(
        init_lines,
        re.compile(r"protected\s+override\s+void\s+OnShown\s*\(EventArgs\s+e\)"),
    )
    init_chrome_range = find_method_block(
        chrome_lines, re.compile(r"private\s+void\s+InitializeChrome\s*\(")
    )

    if onload_range:
        findings.extend(
            scan_method_for_heavy_calls(
                config.mainform_cs,
                onload_lines,
                "OnLoad",
                onload_range,
                [
                    (
                        "InitializeChrome(",
                        "high",
                        "Keep OnLoad minimal; defer expensive chrome/ribbon population until after first paint.",
                    ),
                    (
                        "LoadMruList(",
                        "medium",
                        "Move file I/O and MRU hydration to deferred startup phases.",
                    ),
                    (
                        "ShowPanel<",
                        "critical",
                        "Avoid panel realization in OnLoad; load initial panel after first paint.",
                    ),
                ],
                config.workspace_root,
            )
        )

    if onshown_range:
        findings.extend(
            scan_method_for_heavy_calls(
                config.mainform_init_cs,
                init_lines,
                "OnShown",
                onshown_range,
                [
                    (
                        "InitializeLayoutComponents(",
                        "high",
                        "Move heavy docking/layout setup into phased deferred callbacks.",
                    ),
                    (
                        "ShowPanel<",
                        "critical",
                        "Avoid heavy panel creation in the first shown turn; phase it with idle/timer checkpoints.",
                    ),
                    (
                        "EnsureRightDockPanelInitialized(",
                        "high",
                        "Defer right-dock creation and tab materialization to later startup phases.",
                    ),
                    (
                        "InitializeAsync(",
                        "medium",
                        "Kick async initialization in a later startup phase after first interaction is possible.",
                    ),
                ],
                config.workspace_root,
            )
        )

    if init_chrome_range:
        findings.extend(
            scan_method_for_heavy_calls(
                config.mainform_chrome_cs,
                chrome_lines,
                "InitializeChrome",
                init_chrome_range,
                [
                    (
                        "InitializeRibbon(",
                        "high",
                        "Prefer minimal chrome shell first, then deferred ribbon hydration.",
                    ),
                    (
                        "InitializeStatusBar(",
                        "medium",
                        "Measure status bar setup; defer non-essential panel wiring if expensive.",
                    ),
                    (
                        "InitializeNavigationStrip(",
                        "medium",
                        "Build alternative navigation only when required by runtime mode.",
                    ),
                ],
                config.workspace_root,
            )
        )

    return findings


def analyze_non_thread_lock_risks(config: AuditConfig) -> list[Finding]:
    findings: list[Finding] = []

    program_lines = read_lines(config.program_cs)
    mainform_lines = read_lines(config.mainform_cs)
    init_lines = read_lines(config.mainform_init_cs)
    chrome_lines = read_lines(config.mainform_chrome_cs)

    main_range = find_method_block(
        program_lines, re.compile(r"static\s+void\s+Main\s*\(")
    )
    onload_range = find_method_block(
        mainform_lines,
        re.compile(r"protected\s+override\s+void\s+OnLoad\s*\(EventArgs\s+e\)"),
    )
    onshown_range = find_method_block(
        init_lines,
        re.compile(r"protected\s+override\s+void\s+OnShown\s*\(EventArgs\s+e\)"),
    )
    chrome_range = find_method_block(
        chrome_lines,
        re.compile(r"private\s+void\s+InitializeChrome\s*\("),
    )

    if main_range:
        findings.extend(
            scan_method_for_modal_calls(
                config.program_cs,
                program_lines,
                "Program.Main",
                main_range,
                "medium",
                config.workspace_root,
            )
        )

    if onload_range:
        findings.extend(
            scan_method_for_modal_calls(
                config.mainform_cs,
                mainform_lines,
                "OnLoad",
                onload_range,
                "high",
                config.workspace_root,
            )
        )

    if onshown_range:
        findings.extend(
            scan_method_for_modal_calls(
                config.mainform_init_cs,
                init_lines,
                "OnShown",
                onshown_range,
                "medium",
                config.workspace_root,
            )
        )

    if chrome_range:
        findings.extend(
            scan_method_for_modal_calls(
                config.mainform_chrome_cs,
                chrome_lines,
                "InitializeChrome",
                chrome_range,
                "medium",
                config.workspace_root,
            )
        )

    thread_exception_sites: list[str] = []
    for path, lines in (
        (config.program_cs, program_lines),
        (config.mainform_cs, mainform_lines),
    ):
        for line_no, line in enumerate(lines, start=1):
            if re.search(r"\bApplication\.ThreadException\s*\+=", line):
                thread_exception_sites.append(
                    f"{path.relative_to(config.workspace_root)}:{line_no}"
                )

    if len(thread_exception_sites) >= 2:
        findings.append(
            Finding(
                source="code",
                category="global-exception-handling",
                severity="medium",
                title="Multiple Application.ThreadException subscriptions",
                detail=(
                    "Multiple UI thread exception handlers can produce duplicate dialogs or "
                    "re-entrant error handling during startup failures."
                ),
                location="; ".join(thread_exception_sites[:4]),
                recommendation="Consolidate startup-era UI exception handling into one global pipeline with consistent behavior.",
            )
        )

    trial_popup_line = next(
        (
            index
            for index, line in enumerate(program_lines, start=1)
            if "trial/evaluation popup" in line
            or "show trial popup" in line
            or "show trial/evaluation popup" in line
        ),
        None,
    )
    if trial_popup_line is not None:
        findings.append(
            Finding(
                source="code",
                category="license-interaction",
                severity="medium",
                title="Startup may show Syncfusion trial popup",
                detail=(
                    "Missing or failed Syncfusion license registration can trigger an interactive popup "
                    "that appears as startup lock until dismissed."
                ),
                location=f"{config.program_cs.relative_to(config.workspace_root)}:{trial_popup_line}",
                recommendation="Validate license key resolution before UI startup and fail fast to logs in unattended runs.",
            )
        )

    if onload_range:
        start, end = onload_range
        restore_line = None
        onload_block = mainform_lines[start - 1 : end]
        for offset, line in enumerate(onload_block, start=start):
            if "RestoreWindowState(this)" in line:
                restore_line = offset
                break

        has_bounds_guard = any(
            marker in line
            for line in onload_block
            for marker in (
                "EnsureWindow",
                "ValidateWindow",
                "Normalize",
                "Clamp",
                "WorkingArea",
                "Screen.From",
            )
        )

        if restore_line is not None and not has_bounds_guard:
            findings.append(
                Finding(
                    source="code",
                    category="window-restore-visibility",
                    severity="low",
                    title="Window restore visibility guard not explicit in OnLoad",
                    detail=(
                        "Startup restores persisted window state but OnLoad does not explicitly "
                        "verify on-screen bounds in this method."
                    ),
                    location=f"{config.mainform_cs.relative_to(config.workspace_root)}:{restore_line}",
                    recommendation="Ensure restored bounds are clamped to current monitors to avoid off-screen startup that looks frozen.",
                )
            )

    has_blazor_webview_registration = any(
        "AddWindowsFormsBlazorWebView" in line for line in program_lines
    )
    has_jarvis_startup_toggle = any(
        "WILEYWIDGET_AUTO_OPEN_JARVIS" in line
        or "WILEYWIDGET_UI_AUTOMATION_JARVIS" in line
        or "TryOpenJarvisStartupPanel" in line
        for line in init_lines
    )
    if has_blazor_webview_registration and has_jarvis_startup_toggle:
        location_line = next(
            (
                index
                for index, line in enumerate(program_lines, start=1)
                if "AddWindowsFormsBlazorWebView" in line
            ),
            1,
        )
        findings.append(
            Finding(
                source="code",
                category="webview2-startup-risk",
                severity="low",
                title="WebView2-backed panel can be auto-opened during startup",
                detail=(
                    "When JARVIS auto-open toggles are enabled, WebView2/Blazor composition can "
                    "increase perceived startup lock even without thread blocking."
                ),
                location=f"{config.program_cs.relative_to(config.workspace_root)}:{location_line}",
                recommendation="Keep WebView2 panels opt-in for startup and defer composition until after first interaction.",
            )
        )

    has_single_instance_guard = any(
        re.search(
            r"\bMutex\b|SingleInstance|named\s*mutex|WaitOne\(", line, re.IGNORECASE
        )
        for line in program_lines
    )
    if not has_single_instance_guard:
        findings.append(
            Finding(
                source="code",
                category="process-lifecycle",
                severity="low",
                title="No explicit single-instance startup guard detected",
                detail=(
                    "A stale non-responding process can hold output assemblies and make `dotnet run` "
                    "look like a startup lock via MSBuild copy failures."
                ),
                location=str(config.program_cs.relative_to(config.workspace_root)),
                recommendation="Add a lightweight named mutex or preflight check to detect/notify about stale instances before startup.",
            )
        )

    return findings


def iter_log_files(logs_root: Path) -> Iterable[Path]:
    for pattern in ("startup-*.txt", "wiley-widget-*.log", "errors-*.log"):
        yield from logs_root.glob(pattern)


def analyze_logs(config: AuditConfig) -> list[Finding]:
    findings: list[Finding] = []

    pattern_specs: list[tuple[str, re.Pattern[str]]] = [
        (
            "onload",
            re.compile(
                r"\[ONLOAD\]\s+Completed(?:[^\d]+)?(?:in\s+)?([0-9]+)ms", re.IGNORECASE
            ),
        ),
        (
            "onshown",
            re.compile(r"OnShown\s+completed\s+in\s+([0-9]+)ms", re.IGNORECASE),
        ),
        (
            "chrome",
            re.compile(
                r"InitializeChrome\s+completed\s+in\s+([0-9]+)ms", re.IGNORECASE
            ),
        ),
        (
            "blocking-phase",
            re.compile(
                r"BLOCKING PHASE:\s+'([^']+)'\s+took\s+([0-9]+(?:\.[0-9]+)?)ms",
                re.IGNORECASE,
            ),
        ),
    ]

    for log_file in sorted(set(iter_log_files(config.logs_root))):
        lines = read_lines(log_file)
        if not lines:
            continue

        for line_no, line in enumerate(lines, start=1):
            for kind, pattern in pattern_specs:
                match = pattern.search(line)
                if not match:
                    continue

                location = f"{log_file.relative_to(config.workspace_root)}:{line_no}"

                if kind == "onload":
                    duration = int(match.group(1))
                    if duration >= config.onload_warn_ms:
                        findings.append(
                            Finding(
                                source="logs",
                                category="onload-duration",
                                severity="high",
                                title="OnLoad exceeds startup budget",
                                detail=f"OnLoad completed in {duration}ms (budget {config.onload_warn_ms}ms).",
                                location=location,
                                recommendation="Reduce synchronous OnLoad work to minimal shell setup and defer the rest.",
                            )
                        )

                elif kind == "onshown":
                    duration = int(match.group(1))
                    if duration >= config.onshown_warn_ms:
                        findings.append(
                            Finding(
                                source="logs",
                                category="onshown-duration",
                                severity="high",
                                title="OnShown exceeds responsiveness budget",
                                detail=f"OnShown completed in {duration}ms (budget {config.onshown_warn_ms}ms).",
                                location=location,
                                recommendation="Keep OnShown callback short; split startup actions into separate deferred turns.",
                            )
                        )

                elif kind == "chrome":
                    duration = int(match.group(1))
                    if duration >= config.chrome_warn_ms:
                        severity = (
                            "critical"
                            if duration >= config.blocking_phase_ms
                            else "high"
                        )
                        findings.append(
                            Finding(
                                source="logs",
                                category="chrome-duration",
                                severity=severity,
                                title="InitializeChrome exceeds budget",
                                detail=f"InitializeChrome completed in {duration}ms.",
                                location=location,
                                recommendation="Defer ribbon tab/group hydration and non-essential chrome work until after first paint.",
                            )
                        )

                elif kind == "blocking-phase":
                    phase_name = match.group(1)
                    duration = float(match.group(2))
                    if duration >= config.blocking_phase_ms:
                        findings.append(
                            Finding(
                                source="logs",
                                category="blocking-phase",
                                severity="critical",
                                title=f"Blocking startup phase: {phase_name}",
                                detail=f"Measured {duration:.1f}ms on UI thread.",
                                location=location,
                                recommendation="Break this phase into smaller queued steps and keep each UI callback bounded.",
                            )
                        )

    return findings


def dedupe_findings(findings: list[Finding]) -> list[Finding]:
    deduped: list[Finding] = []
    seen: set[tuple[str, str, str, str, str, str, str]] = set()

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


def sort_findings(findings: list[Finding]) -> list[Finding]:
    return sorted(
        findings,
        key=lambda f: (SEVERITY_ORDER.get(f.severity, 99), f.category, f.location),
    )


def summarize(findings: list[Finding]) -> dict[str, int]:
    counts = {"critical": 0, "high": 0, "medium": 0, "low": 0}
    for finding in findings:
        counts[finding.severity] = counts.get(finding.severity, 0) + 1
    return counts


def write_json(findings: list[Finding], path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    payload = {
        "findingCount": len(findings),
        "findings": [asdict(item) for item in findings],
    }
    path.write_text(json.dumps(payload, indent=2), encoding="utf-8")


def write_markdown(findings: list[Finding], path: Path) -> None:
    counts = summarize(findings)
    lines: list[str] = []
    lines.append("# Startup UI Pipeline Audit")
    lines.append("")
    lines.append(f"Total findings: **{len(findings)}**")
    lines.append(
        f"- Critical: {counts['critical']} | High: {counts['high']} | Medium: {counts['medium']} | Low: {counts['low']}"
    )
    lines.append("")
    lines.append("## Findings")
    lines.append("")

    if not findings:
        lines.append(
            "No startup pipeline bottlenecks detected with current heuristics."
        )
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
        "- https://learn.microsoft.com/dotnet/desktop/winforms/order-of-events-in-windows-forms"
    )
    lines.append(
        "- https://learn.microsoft.com/dotnet/desktop/winforms/forms/events#async-event-handlers"
    )
    lines.append(
        "- https://learn.microsoft.com/dotnet/desktop/winforms/controls/how-to-make-thread-safe-calls"
    )
    lines.append(
        "- https://learn.microsoft.com/windows/win32/win7appqual/preventing-hangs-in-windows-applications"
    )

    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def print_summary(findings: list[Finding], json_out: Path, md_out: Path) -> None:
    counts = summarize(findings)
    print("=" * 78)
    print("STARTUP UI PIPELINE AUDIT")
    print("=" * 78)
    print(f"Findings: {len(findings)}")
    print(
        "Severity: "
        f"critical={counts['critical']}, "
        f"high={counts['high']}, "
        f"medium={counts['medium']}, "
        f"low={counts['low']}"
    )
    print(f"JSON report: {json_out}")
    print(f"Markdown report: {md_out}")

    if findings:
        print("\nTop startup bottlenecks:")
        for finding in findings[:8]:
            print(f"- [{finding.severity}] {finding.title} @ {finding.location}")


def main() -> int:
    args = parse_args()
    config = build_config(args)

    json_out = args.json_out or (config.reports_root / "startup_ui_pipeline_audit.json")
    md_out = args.md_out or (config.reports_root / "startup_ui_pipeline_audit.md")

    findings: list[Finding] = []
    findings.extend(analyze_logs(config))
    findings.extend(analyze_startup_methods(config))
    findings.extend(analyze_non_thread_lock_risks(config))

    findings = dedupe_findings(findings)
    findings = sort_findings(findings)

    write_json(findings, json_out)
    write_markdown(findings, md_out)
    print_summary(findings, json_out, md_out)

    has_critical = any(f.severity == "critical" for f in findings)
    return 2 if has_critical else 0


if __name__ == "__main__":
    raise SystemExit(main())
