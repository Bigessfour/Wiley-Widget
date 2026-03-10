#!/usr/bin/env python3
"""WinForms layout conformance auditor for Wiley Widget.

This script audits WinForms C# source for layout practices derived from:
- Syncfusion WinForms Layout FAQ
- WileyWidget UI standards

Outputs:
- Reports/winforms_layout_audit.json
- Reports/winforms_layout_audit.md
"""

from __future__ import annotations

import argparse
import json
import re
from dataclasses import asdict, dataclass
from datetime import UTC, datetime
from pathlib import Path
from typing import Any, Iterable

SEVERITY_ORDER: dict[str, int] = {
    "critical": 0,
    "high": 1,
    "medium": 2,
    "low": 3,
}

BASELINES = {
    "right_dock_min_width": 350,
    "mainform_min_size": (1280, 800),
    "splitter_width": 13,
}

PANEL_CLASS_PATTERN = re.compile(
    r"\bclass\s+(?P<name>\w*Panel)\s*:\s*(?P<base>[^{]+)",
)
FORM_CLASS_PATTERN = re.compile(
    r"\bclass\s+(?P<name>\w*Form)\s*:\s*(?P<base>[^{]+)",
)
DOCK_FILL_PATTERN = re.compile(r"\bDock\s*=\s*DockStyle\.Fill\b")
MINIMUM_SIZE_PATTERN = re.compile(r"\bMinimumSize\s*=\s*new\s+Size\s*\(")
AUTOSCALE_DPI_PATTERN = re.compile(r"\bAutoScaleMode\s*=\s*AutoScaleMode\.Dpi\b")
AUTOSCROLL_TRUE_PATTERN = re.compile(r"\bAutoScroll\s*=\s*true\b")
TABLELAYOUT_PATTERN = re.compile(r"\bTableLayoutPanel\b")
FLOWLAYOUT_PATTERN = re.compile(r"\bFlowLayoutPanel\b")
ABSOLUTE_STYLE_PATTERN = re.compile(
    r"new\s+(?:ColumnStyle|RowStyle)\s*\(\s*SizeType\.Absolute",
)
PERCENT_STYLE_PATTERN = re.compile(
    r"new\s+(?:ColumnStyle|RowStyle)\s*\(\s*SizeType\.Percent",
)
LOCATION_ASSIGN_PATTERN = re.compile(
    r"(?:\.|\b)(?:Location|Left|Top)\s*=\s*|new\s+Point\s*\(",
)
SIZE_ASSIGN_PATTERN = re.compile(
    r"(?:\.|\b)(?:Size|Width|Height)\s*=\s*|new\s+Size\s*\(",
)
START_POSITION_MANUAL_PATTERN = re.compile(
    r"\bStartPosition\s*=\s*FormStartPosition\.Manual\b",
)
ONRESIZE_PATTERN = re.compile(r"\boverride\s+void\s+OnResize\s*\(")
DEFERRED_LAYOUT_PATTERN = re.compile(
    r"\bApplication\.Idle\b|\bOnIdle\s*\(|\bPerformLayout\s*\("
)
SUSPEND_LAYOUT_PATTERN = re.compile(r"\bSuspendLayout\s*\(")
RESUME_LAYOUT_PATTERN = re.compile(r"\bResumeLayout\s*\(")
SPLITTER_WIDTH_LITERAL_PATTERN = re.compile(r"\bSplitterWidth\s*=\s*(?P<value>\d+)\b")
SPLITCONTAINERADV_HINT_PATTERN = re.compile(r"\bSplitContainerAdv\b")
SIZE_LITERAL_PATTERN = re.compile(
    r"new\s+Size\s*\(\s*(?P<width>\d+)\s*,\s*(?P<height>\d+)\s*\)"
)
MINIMUM_SIZE_LITERAL_PATTERN = re.compile(
    r"\bMinimumSize\s*=\s*new\s+Size\s*\(\s*(?P<width>\d+)\s*,\s*(?P<height>\d+)\s*\)"
)
RIGHT_DOCK_MINIMUM_SIZE_PATTERN = re.compile(
    r"_rightDockPanel\.MinimumSize\s*=\s*new\s+Size\s*\(\s*(?P<width>\d+)\s*,\s*(?P<height>\d+)\s*\)"
)
CONTENT_HOST_NAME_PATTERN = re.compile(r'Name\s*=\s*"ContentHostPanel"')
CONTENT_HOST_RESIZE_HOOK_PATTERN = re.compile(
    r"_contentHostPanel\.Resize\s*\+=\s*ContentHostPanel_Resize"
)
CONTENT_HOST_LAYOUT_HOOK_PATTERN = re.compile(
    r"_contentHostPanel\.(?:Layout|SizeChanged)\s*\+="
)
CONTENT_HOST_CONSTRAIN_REQUEST_PATTERN = re.compile(
    r"RequestMdiConstrain\(\"ContentHostPanel\.(?:Resize|Layout|SizeChanged)\"\)"
)
CONSTRAIN_METHOD_PATTERN = re.compile(r"ConstrainMdiClientToContentHost\s*\(")
SAFE_TOP_FLOOR_PATTERN = re.compile(r"safeTopFloor\s*=\s*_contentHostPanel\.Top")
TOP_CLAMP_PATTERN = re.compile(r"topY\s*=\s*Math\.Max\(topY,\s*safeTopFloor\)")
MDI_TARGET_BOUNDS_ASSIGN_PATTERN = re.compile(r"mdiClient\.Bounds\s*=\s*targetBounds")
HOST_EDGE_USAGE_PATTERN = re.compile(r"_contentHostPanel\.(?:Top|Bottom|Left|Right)")
MAXIMUM_SIZE_ASSIGN_PATTERN = re.compile(r"\bMaximumSize\s*=")
MAINFORM_PARTIAL_GLOB = "src/WileyWidget.WinForms/Forms/MainForm/MainForm*.cs"


@dataclass(frozen=True)
class Finding:
    """One conformance finding."""

    rule_id: str
    severity: str
    title: str
    detail: str
    location: str
    recommendation: str
    evidence: str
    source: str


@dataclass(frozen=True)
class RuleSummary:
    """Aggregated view of findings for one rule."""

    rule_id: str
    title: str
    severity: str
    count: int
    recommendation: str
    source: str


def parse_args() -> argparse.Namespace:
    """Parse command line arguments."""

    parser = argparse.ArgumentParser(
        description=(
            "Audit WinForms layout conformance against Syncfusion FAQ and local UI standards."
        )
    )
    parser.add_argument("--root", type=Path, default=Path.cwd())
    parser.add_argument("--json-out", type=Path, default=None)
    parser.add_argument("--md-out", type=Path, default=None)
    parser.add_argument(
        "--fail-on",
        choices=("critical", "high", "medium", "low", "none"),
        default="none",
        help="Exit non-zero when findings at or above this severity exist.",
    )
    parser.add_argument(
        "--strict",
        action="store_true",
        help="Strict mode for CI; equivalent to --fail-on medium.",
    )
    parser.add_argument(
        "--include-tests",
        action="store_true",
        help="Include tests in scan scope.",
    )
    return parser.parse_args()


def iter_files(root: Path, include_patterns: Iterable[str]) -> Iterable[Path]:
    """Yield files matching include globs with common exclusions."""

    seen: set[Path] = set()
    for pattern in include_patterns:
        for path in root.glob(pattern):
            if path in seen or not path.is_file():
                continue
            if any(
                part in {"bin", "obj", ".git", "TestResults"} for part in path.parts
            ):
                continue
            seen.add(path)
            yield path


def read_lines(path: Path) -> list[str]:
    """Read text file lines with tolerant UTF-8 handling."""

    try:
        return path.read_text(encoding="utf-8", errors="ignore").splitlines()
    except OSError:
        return []


def location(path: Path, line_no: int, root: Path) -> str:
    """Build a workspace-relative location token."""

    return f"{path.relative_to(root)}:{line_no}"


def is_excluded_source_file(path: Path, include_tests: bool) -> bool:
    """Determine if the file should be excluded from analysis."""

    normalized = str(path).replace("\\", "/")
    if not include_tests and "/tests/" in normalized:
        return True
    if normalized.endswith(".Designer.cs"):
        return True
    if normalized.endswith(".g.cs"):
        return True
    if normalized.endswith(".generated.cs"):
        return True
    return False


def first_line_match(lines: list[str], pattern: re.Pattern[str]) -> int:
    """Return first matching 1-based line number or -1."""

    for line_no, line in enumerate(lines, start=1):
        if pattern.search(line):
            return line_no
    return -1


def count_matches(lines: list[str], pattern: re.Pattern[str]) -> int:
    """Count matching lines."""

    return sum(1 for line in lines if pattern.search(line))


def find_first_match_in_files(
    files_data: list[tuple[Path, list[str]]],
    pattern: re.Pattern[str],
) -> tuple[Path, int, str, re.Match[str]] | None:
    """Return first match tuple across files: path, line number, line text, match."""

    for file_path, lines in files_data:
        for line_no, line in enumerate(lines, start=1):
            match = pattern.search(line)
            if match is not None:
                return file_path, line_no, line, match
    return None


def collect_file_sizing_metrics(
    path: Path, lines: list[str], root: Path
) -> dict[str, Any]:
    """Collect sizing assignment metrics for one file."""

    size_literals = 0
    minimum_size_assignments = 0
    maximum_size_assignments = 0
    dock_fill_assignments = 0
    splitter_width_assignments = 0
    min_literal_width: int | None = None
    min_literal_height: int | None = None
    max_literal_width: int | None = None
    max_literal_height: int | None = None

    for line in lines:
        if DOCK_FILL_PATTERN.search(line):
            dock_fill_assignments += 1
        if MAXIMUM_SIZE_ASSIGN_PATTERN.search(line):
            maximum_size_assignments += 1
        if SPLITTER_WIDTH_LITERAL_PATTERN.search(line):
            splitter_width_assignments += 1

        minimum_match = MINIMUM_SIZE_LITERAL_PATTERN.search(line)
        if minimum_match is not None:
            minimum_size_assignments += 1

        for size_match in SIZE_LITERAL_PATTERN.finditer(line):
            size_literals += 1
            width = int(size_match.group("width"))
            height = int(size_match.group("height"))

            min_literal_width = (
                width if min_literal_width is None else min(min_literal_width, width)
            )
            min_literal_height = (
                height
                if min_literal_height is None
                else min(min_literal_height, height)
            )
            max_literal_width = (
                width if max_literal_width is None else max(max_literal_width, width)
            )
            max_literal_height = (
                height
                if max_literal_height is None
                else max(max_literal_height, height)
            )

    return {
        "file": str(path.relative_to(root)).replace("\\", "/"),
        "sizeLiteralCount": size_literals,
        "minimumSizeAssignments": minimum_size_assignments,
        "maximumSizeAssignments": maximum_size_assignments,
        "dockFillAssignments": dock_fill_assignments,
        "splitterWidthAssignments": splitter_width_assignments,
        "minLiteralSize": {
            "width": min_literal_width,
            "height": min_literal_height,
        },
        "maxLiteralSize": {
            "width": max_literal_width,
            "height": max_literal_height,
        },
    }


def build_sizing_inventory(file_metrics: list[dict[str, Any]]) -> dict[str, Any]:
    """Aggregate workspace-wide sizing metrics."""

    totals = {
        "sizeLiteralCount": 0,
        "minimumSizeAssignments": 0,
        "maximumSizeAssignments": 0,
        "dockFillAssignments": 0,
        "splitterWidthAssignments": 0,
    }

    for metric in file_metrics:
        totals["sizeLiteralCount"] += int(metric.get("sizeLiteralCount", 0))
        totals["minimumSizeAssignments"] += int(metric.get("minimumSizeAssignments", 0))
        totals["maximumSizeAssignments"] += int(metric.get("maximumSizeAssignments", 0))
        totals["dockFillAssignments"] += int(metric.get("dockFillAssignments", 0))
        totals["splitterWidthAssignments"] += int(
            metric.get("splitterWidthAssignments", 0)
        )

    top_size_literal_files = sorted(
        (
            metric
            for metric in file_metrics
            if int(metric.get("sizeLiteralCount", 0)) > 0
        ),
        key=lambda metric: (
            -int(metric.get("sizeLiteralCount", 0)),
            metric.get("file", ""),
        ),
    )[:30]

    return {
        "totals": totals,
        "topFilesBySizeLiteralCount": top_size_literal_files,
    }


def is_form_base(base_declaration: str) -> bool:
    """Return True when a base declaration appears to derive from WinForms form types."""

    normalized = base_declaration.replace(" ", "")
    known_tokens = (
        "Form",
        "SfForm",
        "MetroForm",
        "Office2007Form",
    )
    return any(token in normalized for token in known_tokens)


def detect_findings_for_file(path: Path, root: Path, lines: list[str]) -> list[Finding]:
    """Evaluate one C# file and return matching findings."""

    findings: list[Finding] = []
    normalized = str(path).replace("\\", "/")

    panel_match = None
    form_match = None
    for line in lines:
        if panel_match is None:
            panel_match = PANEL_CLASS_PATTERN.search(line)
        if form_match is None:
            form_match = FORM_CLASS_PATTERN.search(line)
        if panel_match and form_match:
            break

    is_panel_file = panel_match is not None and "Controls/Panels/" in normalized
    is_form_file = (
        form_match is not None
        and "/Forms/" in normalized
        and is_form_base(form_match.group("base"))
    )

    if is_panel_file:
        dock_line = first_line_match(lines, DOCK_FILL_PATTERN)
        if dock_line < 0:
            findings.append(
                Finding(
                    rule_id="panel-dock-fill",
                    severity="high",
                    title="Panel root is missing DockStyle.Fill",
                    detail=(
                        "Panel standards require root panel surfaces to fill host containers "
                        "to prevent clipping and overlap drift."
                    ),
                    location=location(path, 1, root),
                    recommendation="Set root panel Dock to DockStyle.Fill in initialization.",
                    evidence=(
                        panel_match.group(0)
                        if panel_match
                        else "Panel class declaration"
                    )[:220],
                    source="docs/WileyWidgetUIStandards.md",
                )
            )

        minimum_size_line = first_line_match(lines, MINIMUM_SIZE_PATTERN)
        if minimum_size_line < 0:
            findings.append(
                Finding(
                    rule_id="panel-minimum-size",
                    severity="medium",
                    title="Panel does not set a minimum size baseline",
                    detail=(
                        "Panel standards define minimum logical dimensions to reduce clipped content "
                        "and maintain predictable resizing behavior."
                    ),
                    location=location(path, 1, root),
                    recommendation=(
                        "Set MinimumSize to a role-appropriate baseline (e.g., 1024x720 docked, 960x600 embedded)."
                    ),
                    evidence=(
                        panel_match.group(0)
                        if panel_match
                        else "Panel class declaration"
                    )[:220],
                    source="docs/WileyWidgetUIStandards.md",
                )
            )

        has_layout_container = any(
            pattern.search(line)
            for pattern in (TABLELAYOUT_PATTERN, FLOWLAYOUT_PATTERN)
            for line in lines
        )
        manual_location_count = count_matches(lines, LOCATION_ASSIGN_PATTERN)
        manual_size_count = count_matches(lines, SIZE_ASSIGN_PATTERN)

        if manual_location_count + manual_size_count >= 10 and not has_layout_container:
            line_no = first_line_match(lines, LOCATION_ASSIGN_PATTERN)
            findings.append(
                Finding(
                    rule_id="manual-pixel-layout-heavy",
                    severity="medium",
                    title="Panel relies heavily on manual pixel layout",
                    detail=(
                        "Large volumes of point/size assignments without layout containers increase "
                        "DPI and resize fragility."
                    ),
                    location=location(path, line_no if line_no > 0 else 1, root),
                    recommendation=(
                        "Move primary composition to TableLayoutPanel/FlowLayoutPanel and keep only exceptional absolute placement."
                    ),
                    evidence=(
                        f"manualLocation={manual_location_count}, manualSize={manual_size_count}, "
                        f"hasLayoutContainer={has_layout_container}"
                    ),
                    source="docs/WileyWidgetUIStandards.md",
                )
            )

    if is_form_file:
        location_line = first_line_match(lines, re.compile(r"\bLocation\s*="))
        has_start_position_manual = any(
            START_POSITION_MANUAL_PATTERN.search(line) for line in lines
        )
        if location_line > 0 and not has_start_position_manual:
            findings.append(
                Finding(
                    rule_id="form-location-without-manual-startposition",
                    severity="medium",
                    title="Form sets Location without StartPosition.Manual",
                    detail=(
                        "Syncfusion FAQ notes that dialog/form placement should set StartPosition to Manual "
                        "when Location is set programmatically."
                    ),
                    location=location(path, location_line, root),
                    recommendation=(
                        "Add StartPosition = FormStartPosition.Manual where Location is explicitly assigned."
                    ),
                    evidence=lines[location_line - 1].strip()[:220],
                    source="https://www.syncfusion.com/faq/windowsforms/layout/how-can-i-programmatically-set-the-initial-position-of-a-form-so-that-it-is-displayed",
                )
            )

        autoscale_line = first_line_match(lines, AUTOSCALE_DPI_PATTERN)
        if autoscale_line < 0:
            findings.append(
                Finding(
                    rule_id="form-autoscale-dpi-missing",
                    severity="low",
                    title="Form does not explicitly set AutoScaleMode.Dpi",
                    detail=(
                        "UI standards prefer DPI-aware scaling for modern displays. "
                        "This may already be set in a designer file."
                    ),
                    location=location(path, 1, root),
                    recommendation="Set AutoScaleMode = AutoScaleMode.Dpi in form initialization if not designer-managed.",
                    evidence=(
                        form_match.group(0) if form_match else "Form class declaration"
                    )[:220],
                    source="docs/WileyWidgetUIStandards.md",
                )
            )

    table_layout_count = count_matches(lines, TABLELAYOUT_PATTERN)
    absolute_style_count = count_matches(lines, ABSOLUTE_STYLE_PATTERN)
    percent_style_count = count_matches(lines, PERCENT_STYLE_PATTERN)
    if table_layout_count > 0 and absolute_style_count > 0 and percent_style_count == 0:
        line_no = first_line_match(lines, ABSOLUTE_STYLE_PATTERN)
        findings.append(
            Finding(
                rule_id="tablelayout-absolute-only",
                severity="high",
                title="TableLayoutPanel uses only absolute row/column sizing",
                detail=(
                    "Complex layouts with only absolute row/column styles are prone to clipping and poor resize adaptation."
                ),
                location=location(path, line_no if line_no > 0 else 1, root),
                recommendation="Keep at least one Percent stretch row/column in each complex table layout.",
                evidence=(
                    f"tableLayouts={table_layout_count}, absoluteStyles={absolute_style_count}, "
                    f"percentStyles={percent_style_count}"
                ),
                source="docs/WileyWidgetUIStandards.md",
            )
        )

    on_resize_line = first_line_match(lines, ONRESIZE_PATTERN)
    if on_resize_line > 0:
        has_deferred_pattern = any(
            DEFERRED_LAYOUT_PATTERN.search(line) for line in lines
        )
        manual_mutations = count_matches(
            lines, LOCATION_ASSIGN_PATTERN
        ) + count_matches(lines, SIZE_ASSIGN_PATTERN)
        if manual_mutations >= 6 and not has_deferred_pattern:
            findings.append(
                Finding(
                    rule_id="resize-without-deferred-layout",
                    severity="low",
                    title="OnResize with heavy layout mutations lacks deferred layout pattern",
                    detail=(
                        "Syncfusion FAQ references deferring expensive layout/paint updates until idle when frequent resize updates occur."
                    ),
                    location=location(path, on_resize_line, root),
                    recommendation=(
                        "Consider deferring expensive layout updates via Application.Idle + PerformLayout for resize-heavy surfaces."
                    ),
                    evidence=(
                        f"OnResize at line {on_resize_line}; manualLayoutMutations={manual_mutations}"
                    ),
                    source="https://www.syncfusion.com/faq/windowsforms/layout/i-have-a-form-with-several-controls-on-it-as-i-size-the-form-the-controls-are-being-resized",
                )
            )

    suspend_layout_line = first_line_match(lines, SUSPEND_LAYOUT_PATTERN)
    has_resume_layout = any(RESUME_LAYOUT_PATTERN.search(line) for line in lines)
    if suspend_layout_line > 0 and not has_resume_layout:
        findings.append(
            Finding(
                rule_id="suspendlayout-without-resume",
                severity="high",
                title="SuspendLayout call has no matching ResumeLayout in file",
                detail=(
                    "Missing ResumeLayout can leave control trees in an unstable layout state and mask size/position updates."
                ),
                location=location(path, suspend_layout_line, root),
                recommendation="Ensure each SuspendLayout path has a corresponding ResumeLayout call.",
                evidence=lines[suspend_layout_line - 1].strip()[:220],
                source="docs/WileyWidgetUIStandards.md",
            )
        )

    has_splitter_control_hint = any(
        SPLITCONTAINERADV_HINT_PATTERN.search(line) for line in lines
    )
    for line_no, line in enumerate(lines, start=1):
        splitter_match = SPLITTER_WIDTH_LITERAL_PATTERN.search(line)
        if splitter_match is None:
            continue

        value = int(splitter_match.group("value"))
        if value >= 13:
            continue

        if not has_splitter_control_hint and "SplitterWidth" not in line:
            continue

        findings.append(
            Finding(
                rule_id="splitterwidth-below-syncfusion-baseline",
                severity="high",
                title="Splitter width is below Syncfusion baseline",
                detail=(
                    "SplitContainer-based layouts should keep a usable splitter width. "
                    "In this workspace, Syncfusion sample-aligned baseline is 13 logical px (DPI-scaled)."
                ),
                location=location(path, line_no, root),
                recommendation="Set splitter width to at least 13 logical px and apply DPI scaling where appropriate.",
                evidence=line.strip()[:220],
                source="Syncfusion SplitContainerAdv sample baseline (workspace usage)",
            )
        )

    if is_panel_file:
        has_auto_scroll = any(AUTOSCROLL_TRUE_PATTERN.search(line) for line in lines)
        has_fixed_columns = absolute_style_count > 0
        if has_fixed_columns and not has_auto_scroll:
            line_no = first_line_match(lines, ABSOLUTE_STYLE_PATTERN)
            findings.append(
                Finding(
                    rule_id="fixed-layout-without-autoscroll",
                    severity="low",
                    title="Fixed layout regions detected without AutoScroll",
                    detail=(
                        "Embedded fixed-width content is safer with AutoScroll enabled to reduce clipping on smaller hosts."
                    ),
                    location=location(path, line_no if line_no > 0 else 1, root),
                    recommendation="Set AutoScroll = true on the hosting panel when fixed-width regions are present.",
                    evidence=(
                        f"absoluteStyles={absolute_style_count}, autoScroll={has_auto_scroll}"
                    ),
                    source="docs/WileyWidgetUIStandards.md",
                )
            )

    return findings


def analyze_mainform_sizing_contract(
    root: Path,
) -> tuple[list[Finding], dict[str, Any]]:
    """Audit MainForm host and sizing contract across MainForm partial files."""

    findings: list[Finding] = []
    mainform_files = sorted(root.glob(MAINFORM_PARTIAL_GLOB))
    files_data: list[tuple[Path, list[str]]] = []

    for path in mainform_files:
        lines = read_lines(path)
        if lines:
            files_data.append((path, lines))

    profile: dict[str, Any] = {
        "mainFormFiles": [
            str(path.relative_to(root)).replace("\\", "/") for path in mainform_files
        ],
        "minimumSizeAssignments": [],
        "rightDockMinimumSizes": [],
    }

    if not files_data:
        findings.append(
            Finding(
                rule_id="mainform-files-missing",
                severity="critical",
                title="MainForm partial files not found for sizing audit",
                detail="MainForm sizing contract cannot be verified because MainForm partial files were not discovered.",
                location="src/WileyWidget.WinForms/Forms/MainForm",
                recommendation="Restore MainForm partial files before running this audit.",
                evidence=MAINFORM_PARTIAL_GLOB,
                source="docs/WileyWidgetUIStandards.md",
            )
        )
        return findings, profile

    autoscale_hit = find_first_match_in_files(files_data, AUTOSCALE_DPI_PATTERN)
    if autoscale_hit is None:
        first_path = files_data[0][0]
        findings.append(
            Finding(
                rule_id="mainform-autoscale-dpi-missing",
                severity="high",
                title="MainForm is missing AutoScaleMode.Dpi",
                detail="MainForm should explicitly use DPI scaling for consistent sizing across monitors.",
                location=location(first_path, 1, root),
                recommendation="Set AutoScaleMode = AutoScaleMode.Dpi in MainForm startup/chrome initialization.",
                evidence="No AutoScaleMode.Dpi assignment found in MainForm partials",
                source="docs/WileyWidgetUIStandards.md",
            )
        )
    else:
        profile["autoScaleDpi"] = {
            "location": location(autoscale_hit[0], autoscale_hit[1], root),
            "evidence": autoscale_hit[2].strip()[:220],
        }

    mainform_minimum_sizes: list[dict[str, Any]] = []
    for file_path, lines in files_data:
        for line_no, line in enumerate(lines, start=1):
            min_match = MINIMUM_SIZE_LITERAL_PATTERN.search(line)
            if min_match is None:
                continue
            width = int(min_match.group("width"))
            height = int(min_match.group("height"))
            item = {
                "location": location(file_path, line_no, root),
                "width": width,
                "height": height,
                "evidence": line.strip()[:220],
            }
            mainform_minimum_sizes.append(item)

    profile["minimumSizeAssignments"] = mainform_minimum_sizes
    if not mainform_minimum_sizes:
        first_path = files_data[0][0]
        findings.append(
            Finding(
                rule_id="mainform-minimum-size-missing",
                severity="high",
                title="MainForm minimum size is not defined",
                detail="MainForm requires a minimum size baseline to protect host layout and avoid panel clipping.",
                location=location(first_path, 1, root),
                recommendation="Set MainForm MinimumSize to an enforced baseline (typically at least 1280x800).",
                evidence="No MainForm MinimumSize assignment found in MainForm partials",
                source="docs/WileyWidgetUIStandards.md",
            )
        )
    else:
        largest_minimum = max(
            mainform_minimum_sizes,
            key=lambda item: (int(item["width"]), int(item["height"])),
        )
        profile["largestMinimumSize"] = largest_minimum
        if int(largest_minimum["width"]) < 1280 or int(largest_minimum["height"]) < 800:
            findings.append(
                Finding(
                    rule_id="mainform-minimum-size-below-baseline",
                    severity="medium",
                    title="MainForm minimum size is below baseline",
                    detail="MainForm host sizing baseline should preserve ribbon/content host docking and avoid clipped MDI content.",
                    location=str(largest_minimum["location"]),
                    recommendation="Raise MainForm MinimumSize to at least 1280x800 unless a deliberate smaller baseline is documented.",
                    evidence=str(largest_minimum["evidence"]),
                    source="docs/WileyWidgetUIStandards.md",
                )
            )

    content_host_name_hit = find_first_match_in_files(
        files_data, CONTENT_HOST_NAME_PATTERN
    )
    content_host_dock_hit = find_first_match_in_files(files_data, DOCK_FILL_PATTERN)
    if content_host_name_hit is None:
        first_path = files_data[0][0]
        findings.append(
            Finding(
                rule_id="mainform-contenthost-missing",
                severity="critical",
                title="MainForm ContentHostPanel is not declared",
                detail="MainForm requires ContentHostPanel as the sizing host between ribbon and status bar.",
                location=location(first_path, 1, root),
                recommendation="Declare and initialize ContentHostPanel as the central host container.",
                evidence="No ContentHostPanel name assignment found",
                source="src/WileyWidget.WinForms/Forms/MainForm/MainForm.cs",
            )
        )
    else:
        profile["contentHostName"] = {
            "location": location(
                content_host_name_hit[0], content_host_name_hit[1], root
            ),
            "evidence": content_host_name_hit[2].strip()[:220],
        }

    if content_host_dock_hit is None:
        first_path = files_data[0][0]
        findings.append(
            Finding(
                rule_id="mainform-contenthost-dock-fill-missing",
                severity="high",
                title="MainForm host container does not enforce DockStyle.Fill",
                detail="Content host should fill available region so MDI and right dock sizing stay stable.",
                location=location(first_path, 1, root),
                recommendation="Set ContentHostPanel Dock = DockStyle.Fill during initialization.",
                evidence="No DockStyle.Fill assignment found in MainForm partials",
                source="src/WileyWidget.WinForms/Forms/MainForm/MainForm.cs",
            )
        )

    resize_hook_hit = find_first_match_in_files(
        files_data, CONTENT_HOST_RESIZE_HOOK_PATTERN
    )
    layout_hook_hit = find_first_match_in_files(
        files_data, CONTENT_HOST_LAYOUT_HOOK_PATTERN
    )
    constrain_request_hit = find_first_match_in_files(
        files_data,
        CONTENT_HOST_CONSTRAIN_REQUEST_PATTERN,
    )
    if resize_hook_hit is None and layout_hook_hit is None:
        first_path = files_data[0][0]
        findings.append(
            Finding(
                rule_id="mainform-contenthost-events-missing",
                severity="medium",
                title="MainForm host resize/layout hooks are missing",
                detail="MainForm should react to ContentHostPanel sizing events to maintain MDI bounds.",
                location=location(first_path, 1, root),
                recommendation="Attach ContentHostPanel resize/layout/size-changed handlers in MainForm.",
                evidence="No ContentHostPanel resize/layout event subscription found",
                source="src/WileyWidget.WinForms/Forms/MainForm/MainForm.DocumentManagement.cs",
            )
        )
    if constrain_request_hit is None:
        first_path = files_data[0][0]
        findings.append(
            Finding(
                rule_id="mainform-contenthost-constrain-request-missing",
                severity="medium",
                title="MainForm host events do not request MDI constrain",
                detail="ContentHostPanel sizing events should trigger RequestMdiConstrain to keep panel content below ribbon.",
                location=location(first_path, 1, root),
                recommendation="Call RequestMdiConstrain from ContentHostPanel Resize/Layout/SizeChanged handlers.",
                evidence='No RequestMdiConstrain("ContentHostPanel.*") call found',
                source="src/WileyWidget.WinForms/Forms/MainForm/MainForm.cs",
            )
        )

    constrain_method_hit = find_first_match_in_files(
        files_data, CONSTRAIN_METHOD_PATTERN
    )
    if constrain_method_hit is None:
        first_path = files_data[0][0]
        findings.append(
            Finding(
                rule_id="mainform-constrain-method-missing",
                severity="critical",
                title="MainForm MDI constrain method is missing",
                detail="MainForm must constrain MdiClient to ContentHostPanel bounds to avoid overlap and clipping.",
                location=location(first_path, 1, root),
                recommendation="Restore ConstrainMdiClientToContentHost and ensure it runs on host/layout changes.",
                evidence="No ConstrainMdiClientToContentHost method detected",
                source="src/WileyWidget.WinForms/Forms/MainForm/MainForm.cs",
            )
        )

    safe_top_hit = find_first_match_in_files(files_data, SAFE_TOP_FLOOR_PATTERN)
    top_clamp_hit = find_first_match_in_files(files_data, TOP_CLAMP_PATTERN)
    target_bounds_hit = find_first_match_in_files(
        files_data, MDI_TARGET_BOUNDS_ASSIGN_PATTERN
    )
    host_edge_hit = find_first_match_in_files(files_data, HOST_EDGE_USAGE_PATTERN)

    if safe_top_hit is None or top_clamp_hit is None:
        evidence = (
            "Missing safe top-floor clamp"
            if safe_top_hit is None and top_clamp_hit is None
            else (
                "Missing safeTopFloor assignment"
                if safe_top_hit is None
                else "Missing topY clamp to safeTopFloor"
            )
        )
        location_path = (safe_top_hit or top_clamp_hit or files_data[0])[0]
        location_line = (
            safe_top_hit or top_clamp_hit or (files_data[0][0], 1, "", None)
        )[1]
        findings.append(
            Finding(
                rule_id="mainform-safe-top-floor-missing",
                severity="high",
                title="MainForm MDI top-edge safety clamp is incomplete",
                detail="MainForm should clamp MDI top bounds against ContentHostPanel/ribbon floor to prevent overlap under ribbon.",
                location=location(location_path, location_line, root),
                recommendation="Ensure safeTopFloor is derived from ContentHostPanel/ribbon and applied via topY = Math.Max(topY, safeTopFloor).",
                evidence=evidence,
                source="src/WileyWidget.WinForms/Forms/MainForm/MainForm.cs",
            )
        )

    if target_bounds_hit is None:
        first_path = files_data[0][0]
        findings.append(
            Finding(
                rule_id="mainform-mdi-target-bounds-missing",
                severity="high",
                title="MainForm does not apply computed MDI target bounds",
                detail="Constrain path should compute and apply target bounds to MdiClient for reliable host sizing.",
                location=location(first_path, 1, root),
                recommendation="Set mdiClient.Bounds = targetBounds in constrain flow.",
                evidence="No mdiClient.Bounds = targetBounds assignment detected",
                source="src/WileyWidget.WinForms/Forms/MainForm/MainForm.cs",
            )
        )

    if host_edge_hit is None:
        first_path = files_data[0][0]
        findings.append(
            Finding(
                rule_id="mainform-host-edge-usage-missing",
                severity="medium",
                title="MainForm constrain flow does not use ContentHostPanel edges",
                detail="MainForm MDI constraints should derive geometry from ContentHostPanel top/left/right/bottom boundaries.",
                location=location(first_path, 1, root),
                recommendation="Use ContentHostPanel edges when calculating MDI target bounds.",
                evidence="No ContentHostPanel edge references detected in MainForm constrain path",
                source="src/WileyWidget.WinForms/Forms/MainForm/MainForm.cs",
            )
        )

    right_dock_min_sizes: list[dict[str, Any]] = []
    for file_path, lines in files_data:
        for line_no, line in enumerate(lines, start=1):
            right_dock_match = RIGHT_DOCK_MINIMUM_SIZE_PATTERN.search(line)
            if right_dock_match is None:
                continue
            width = int(right_dock_match.group("width"))
            height = int(right_dock_match.group("height"))
            right_dock_min_sizes.append(
                {
                    "location": location(file_path, line_no, root),
                    "width": width,
                    "height": height,
                    "evidence": line.strip()[:220],
                }
            )

    profile["rightDockMinimumSizes"] = right_dock_min_sizes
    if not right_dock_min_sizes:
        first_path = files_data[0][0]
        findings.append(
            Finding(
                rule_id="mainform-rightdock-minimum-size-missing",
                severity="low",
                title="MainForm right dock minimum size is not defined",
                detail="Right dock panels are more stable when minimum width is explicitly constrained.",
                location=location(first_path, 1, root),
                recommendation="Assign _rightDockPanel.MinimumSize to a safe width baseline (e.g., 300+ logical px).",
                evidence="No _rightDockPanel.MinimumSize assignment detected",
                source="src/WileyWidget.WinForms/Forms/MainForm/MainForm.Navigation.cs",
            )
        )
    else:
        smallest = min(
            right_dock_min_sizes,
            key=lambda item: (int(item["width"]), int(item["height"])),
        )
        profile["smallestRightDockMinimumSize"] = smallest
        if int(smallest["width"]) < 300:
            findings.append(
                Finding(
                    rule_id="mainform-rightdock-minimum-size-too-small",
                    severity="low",
                    title="MainForm right dock minimum width is small",
                    detail="A very small right dock minimum width can collapse navigation/assistant panels and reduce usability.",
                    location=str(smallest["location"]),
                    recommendation="Increase _rightDockPanel.MinimumSize width to at least 300 logical px.",
                    evidence=str(smallest["evidence"]),
                    source="src/WileyWidget.WinForms/Forms/MainForm/MainForm.Navigation.cs",
                )
            )

    return findings, profile


def sort_findings(findings: list[Finding]) -> list[Finding]:
    """Sort findings by severity and location."""

    return sorted(
        findings,
        key=lambda finding: (
            SEVERITY_ORDER.get(finding.severity, 99),
            finding.location.lower(),
            finding.rule_id,
        ),
    )


def summarize(findings: list[Finding]) -> dict[str, Any]:
    """Build top-level summary fields."""

    by_severity = {"critical": 0, "high": 0, "medium": 0, "low": 0}
    by_rule: dict[str, int] = {}

    for finding in findings:
        by_severity[finding.severity] = by_severity.get(finding.severity, 0) + 1
        by_rule[finding.rule_id] = by_rule.get(finding.rule_id, 0) + 1

    total = len(findings)
    base_score = 100
    score_penalty = (
        by_severity["critical"] * 15
        + by_severity["high"] * 8
        + by_severity["medium"] * 4
        + by_severity["low"] * 2
    )
    conformance_score = max(0, base_score - score_penalty)

    return {
        "total": total,
        "bySeverity": by_severity,
        "byRule": dict(sorted(by_rule.items(), key=lambda item: (-item[1], item[0]))),
        "conformanceScore": conformance_score,
    }


def build_rule_summaries(findings: list[Finding]) -> list[RuleSummary]:
    """Aggregate findings into actionable rule summaries."""

    grouped: dict[str, list[Finding]] = {}
    for finding in findings:
        grouped.setdefault(finding.rule_id, []).append(finding)

    summaries: list[RuleSummary] = []
    for rule_id, items in grouped.items():
        first = items[0]
        summaries.append(
            RuleSummary(
                rule_id=rule_id,
                title=first.title,
                severity=first.severity,
                count=len(items),
                recommendation=first.recommendation,
                source=first.source,
            )
        )

    summaries.sort(
        key=lambda item: (
            SEVERITY_ORDER.get(item.severity, 99),
            -item.count,
            item.rule_id,
        )
    )
    return summaries


def write_json_report(path: Path, payload: dict[str, Any]) -> None:
    """Write machine-readable output."""

    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2), encoding="utf-8")


def write_markdown_report(path: Path, payload: dict[str, Any]) -> None:
    """Write human-readable output with prioritized recommendations."""

    summary = payload["summary"]
    sizing_inventory = payload.get("sizingInventory", {})
    sizing_totals = sizing_inventory.get("totals", {})
    mainform_profile = payload.get("mainFormSizingProfile", {})
    rules: list[dict[str, Any]] = payload["ruleSummaries"]
    findings: list[dict[str, Any]] = payload["findings"]

    lines: list[str] = [
        "# WinForms Layout Conformance Audit",
        "",
        f"Generated: {payload['generatedAtUtc']}",
        "",
        "## Summary",
        f"- Total findings: **{summary['total']}**",
        f"- Conformance score: **{summary['conformanceScore']} / 100**",
        f"- Critical: **{summary['bySeverity']['critical']}**",
        f"- High: **{summary['bySeverity']['high']}**",
        f"- Medium: **{summary['bySeverity']['medium']}**",
        f"- Low: **{summary['bySeverity']['low']}**",
        f"- Size literals scanned: **{sizing_totals.get('sizeLiteralCount', 0)}**",
        f"- MinimumSize assignments scanned: **{sizing_totals.get('minimumSizeAssignments', 0)}**",
        "",
        "## Sizing Inventory",
        f"- DockStyle.Fill assignments: **{sizing_totals.get('dockFillAssignments', 0)}**",
        f"- MaximumSize assignments: **{sizing_totals.get('maximumSizeAssignments', 0)}**",
        f"- SplitterWidth assignments: **{sizing_totals.get('splitterWidthAssignments', 0)}**",
        "",
        "## MainForm Host Coverage",
        f"- MainForm partials scanned: **{len(mainform_profile.get('mainFormFiles', []))}**",
        f"- MainForm minimum size assignments: **{len(mainform_profile.get('minimumSizeAssignments', []))}**",
        f"- Right dock minimum size assignments: **{len(mainform_profile.get('rightDockMinimumSizes', []))}**",
        "",
        "## Top Recommendations",
        "",
    ]

    top_size_files = sizing_inventory.get("topFilesBySizeLiteralCount", [])
    if top_size_files:
        lines.extend(["### Most Sizing-Dense Files", ""])
        for item in top_size_files[:10]:
            lines.append(
                f"- `{item.get('file', '')}`: sizeLiterals={item.get('sizeLiteralCount', 0)}, "
                f"minimumSize={item.get('minimumSizeAssignments', 0)}, splitterWidth={item.get('splitterWidthAssignments', 0)}"
            )
        lines.append("")

    largest_mainform_minimum = mainform_profile.get("largestMinimumSize")
    if largest_mainform_minimum:
        lines.extend(
            [
                "### MainForm Baseline Detected",
                "",
                f"- Location: `{largest_mainform_minimum.get('location', '')}`",
                f"- Size: `{largest_mainform_minimum.get('width', '?')}x{largest_mainform_minimum.get('height', '?')}`",
                "",
            ]
        )

    if not rules:
        lines.append("No findings detected by current rule set.")
    else:
        for item in rules:
            lines.extend(
                [
                    f"### [{item['severity'].upper()}] {item['title']} (x{item['count']})",
                    f"- Rule: `{item['rule_id']}`",
                    f"- Recommendation: {item['recommendation']}",
                    f"- Source: {item['source']}",
                    "",
                ]
            )

    lines.extend(["## Findings", ""])
    if not findings:
        lines.append("No file-level findings.")
    else:
        for finding in findings:
            lines.extend(
                [
                    f"### [{finding['severity'].upper()}] {finding['title']}",
                    f"- Rule: `{finding['rule_id']}`",
                    f"- Location: `{finding['location']}`",
                    f"- Evidence: `{finding['evidence']}`",
                    f"- Detail: {finding['detail']}",
                    f"- Recommendation: {finding['recommendation']}",
                    f"- Source: {finding['source']}",
                    "",
                ]
            )

    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines), encoding="utf-8")


def severity_meets_fail_threshold(severity: str, fail_on: str) -> bool:
    """Return True when severity should fail the run for threshold."""

    if fail_on == "none":
        return False

    severity_rank = SEVERITY_ORDER.get(severity, 99)
    threshold_rank = SEVERITY_ORDER.get(fail_on, 99)
    return severity_rank <= threshold_rank


def main() -> int:
    """Run the conformance audit and emit reports."""

    args = parse_args()
    root = args.root.resolve()
    include_patterns = ("src/WileyWidget.WinForms/**/*.cs",)

    findings: list[Finding] = []
    scanned_files = 0
    file_metrics: list[dict[str, Any]] = []

    for path in iter_files(root, include_patterns):
        if is_excluded_source_file(path, include_tests=args.include_tests):
            continue

        lines = read_lines(path)
        if not lines:
            continue

        scanned_files += 1
        findings.extend(detect_findings_for_file(path, root, lines))
        file_metrics.append(collect_file_sizing_metrics(path, lines, root))

    mainform_findings, mainform_profile = analyze_mainform_sizing_contract(root)
    findings.extend(mainform_findings)

    findings = sort_findings(findings)
    summary = summarize(findings)
    rule_summaries = build_rule_summaries(findings)
    sizing_inventory = build_sizing_inventory(file_metrics)

    effective_fail_on = "medium" if args.strict else args.fail_on

    payload: dict[str, Any] = {
        "generatedAtUtc": datetime.now(UTC).isoformat(),
        "workspaceRoot": str(root),
        "scan": {
            "includePatterns": list(include_patterns),
            "scannedFiles": scanned_files,
            "excludedDesignerFiles": True,
            "includedTests": args.include_tests,
        },
        "sizingInventory": sizing_inventory,
        "mainFormSizingProfile": mainform_profile,
        "sources": [
            "docs/WileyWidgetUIStandards.md",
            "https://www.syncfusion.com/faq/windowsforms/layout",
            "https://www.syncfusion.com/faq/windowsforms/layout/i-have-a-form-with-several-controls-on-it-as-i-size-the-form-the-controls-are-being-resized",
            "https://www.syncfusion.com/faq/windowsforms/layout/how-can-i-programmatically-set-the-initial-position-of-a-form-so-that-it-is-displayed",
            "src/WileyWidget.WinForms/Forms/MainForm/MainForm.cs",
            "src/WileyWidget.WinForms/Forms/MainForm/MainForm.Chrome.cs",
            "src/WileyWidget.WinForms/Forms/MainForm/MainForm.DocumentManagement.cs",
        ],
        "summary": summary,
        "ruleSummaries": [asdict(item) for item in rule_summaries],
        "findings": [asdict(item) for item in findings],
    }

    json_out = (
        args.json_out.resolve()
        if args.json_out
        else root / "Reports" / "winforms_layout_audit.json"
    )
    md_out = (
        args.md_out.resolve()
        if args.md_out
        else root / "Reports" / "winforms_layout_audit.md"
    )

    write_json_report(json_out, payload)
    write_markdown_report(md_out, payload)

    print(
        "WinForms layout audit completed: "
        f"files={scanned_files} findings={summary['total']} "
        f"critical={summary['bySeverity']['critical']} "
        f"high={summary['bySeverity']['high']} "
        f"medium={summary['bySeverity']['medium']} "
        f"low={summary['bySeverity']['low']} "
        f"score={summary['conformanceScore']}"
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
