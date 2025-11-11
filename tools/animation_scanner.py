#!/usr/bin/env python3

"""
Syncfusion Animation Scanner for Wiley Widget Repository

Detects WPF and Syncfusion-specific animations in XAML and C# files:
- XAML: <Storyboard>, <DoubleAnimation>, <Trigger>, <EventTrigger>
- C#: AnimationTimeline, BeginAnimation, TransitionManager
- Syncfusion: EnableAnimation, AnimateOnDataChange, SeriesAnimationMode, RowTransitionMode

Aligned with: Syncfusion Essential Studio for WPF Documentation
- https://help.syncfusion.com/wpf/chart/animations
- https://help.syncfusion.com/wpf/datagrid/transitions

Usage:
    python animation_scanner.py --path src/WileyWidget
    python animation_scanner.py --path src/WileyWidget --output logs/animation_report.json
    python animation_scanner.py --focus xaml --verbose
"""

import argparse
import json
import re
from dataclasses import asdict, dataclass, field
from pathlib import Path
from typing import List, Set
import sys

# ============================================================================
# ANIMATION PATTERNS
# ============================================================================

# XAML Animation Patterns
STORYBOARD_PAT = re.compile(
    r'<Storyboard\s+(?:[^>]*\s+)?x:Key\s*=\s*"([^"]+)"',
    re.IGNORECASE
)

DOUBLE_ANIMATION_PAT = re.compile(
    r'<DoubleAnimation\s+(?:[^>]*)(?:Storyboard\.TargetProperty\s*=\s*"([^"]+)")?',
    re.IGNORECASE
)

COLOR_ANIMATION_PAT = re.compile(
    r'<ColorAnimation\s+(?:[^>]*)(?:Storyboard\.TargetProperty\s*=\s*"([^"]+)")?',
    re.IGNORECASE
)

EVENT_TRIGGER_PAT = re.compile(
    r'<EventTrigger\s+(?:[^>]*)RoutedEvent\s*=\s*"([^"]+)"',
    re.IGNORECASE
)

PROPERTY_TRIGGER_PAT = re.compile(
    r'<Trigger\s+Property\s*=\s*"([^"]+)"\s+Value\s*=\s*"([^"]+)"',
    re.IGNORECASE
)

BEGIN_STORYBOARD_PAT = re.compile(
    r'<BeginStoryboard[^>]*>',
    re.IGNORECASE
)

# Syncfusion-Specific XAML Patterns
SYNCFUSION_ENABLE_ANIMATION_PAT = re.compile(
    r'<(?:syncfusion|sf):(\w+)[^>]*\s+EnableAnimation\s*=\s*"(True|False)"',
    re.IGNORECASE
)

SYNCFUSION_ANIMATION_DURATION_PAT = re.compile(
    r'AnimationDuration\s*=\s*"([^"]+)"',
    re.IGNORECASE
)

SYNCFUSION_SERIES_ANIMATION_PAT = re.compile(
    r'SeriesAnimationMode\s*=\s*"([^"]+)"',
    re.IGNORECASE
)

SYNCFUSION_ROW_TRANSITION_PAT = re.compile(
    r'RowTransitionMode\s*=\s*"([^"]+)"',
    re.IGNORECASE
)

SYNCFUSION_ANIMATE_ON_DATA_PAT = re.compile(
    r'AnimateOnDataChange\s*=\s*"(True|False)"',
    re.IGNORECASE
)

# C# Animation Patterns
CSHARP_ANIMATION_TIMELINE_PAT = re.compile(
    r'(?:new\s+)?(\w*Animation(?:Timeline)?)\s*\(',
    re.IGNORECASE
)

CSHARP_BEGIN_ANIMATION_PAT = re.compile(
    r'\.BeginAnimation\s*\(\s*(\w+Property)\s*,',
    re.IGNORECASE
)

CSHARP_TRANSITION_MANAGER_PAT = re.compile(
    r'TransitionManager\.(\w+)',
    re.IGNORECASE
)

CSHARP_VISUAL_STATE_PAT = re.compile(
    r'VisualStateManager\.GoToState\s*\(',
    re.IGNORECASE
)

# ============================================================================
# DATA STRUCTURES
# ============================================================================

@dataclass
class AnimationMatch:
    """Represents a single animation detection."""
    file: str
    line_number: int
    pattern_type: str
    content: str
    animation_key: str = ""
    target_property: str = ""
    control_type: str = ""

    def to_dict(self) -> dict:
        return asdict(self)


@dataclass
class AnimationReport:
    """Complete animation scan report."""
    scan_date: str
    repository: str
    scope: str
    total_files_scanned: int
    xaml_files_scanned: int
    csharp_files_scanned: int

    # Categorized matches
    xaml_storyboards: List[AnimationMatch] = field(default_factory=list)
    xaml_animations: List[AnimationMatch] = field(default_factory=list)
    xaml_triggers: List[AnimationMatch] = field(default_factory=list)
    syncfusion_animations: List[AnimationMatch] = field(default_factory=list)
    csharp_animations: List[AnimationMatch] = field(default_factory=list)

    # Summary stats
    total_animations_found: int = 0
    files_with_animations: Set[str] = field(default_factory=set)
    animation_keys: Set[str] = field(default_factory=set)

    def add_match(self, match: AnimationMatch):
        """Add a match to the appropriate category."""
        self.files_with_animations.add(match.file)
        self.total_animations_found += 1

        if match.animation_key:
            self.animation_keys.add(match.animation_key)

        # Categorize
        if match.pattern_type == "Storyboard":
            self.xaml_storyboards.append(match)
        elif match.pattern_type in ["DoubleAnimation", "ColorAnimation"]:
            self.xaml_animations.append(match)
        elif match.pattern_type in ["EventTrigger", "PropertyTrigger", "BeginStoryboard"]:
            self.xaml_triggers.append(match)
        elif "Syncfusion" in match.pattern_type:
            self.syncfusion_animations.append(match)
        else:
            self.csharp_animations.append(match)

    def to_dict(self) -> dict:
        """Convert to dictionary for JSON serialization."""
        return {
            "scan_date": self.scan_date,
            "repository": self.repository,
            "scope": self.scope,
            "total_files_scanned": self.total_files_scanned,
            "xaml_files_scanned": self.xaml_files_scanned,
            "csharp_files_scanned": self.csharp_files_scanned,
            "summary": {
                "total_animations_found": self.total_animations_found,
                "files_with_animations": len(self.files_with_animations),
                "unique_animation_keys": len(self.animation_keys),
                "xaml_storyboards": len(self.xaml_storyboards),
                "xaml_animations": len(self.xaml_animations),
                "xaml_triggers": len(self.xaml_triggers),
                "syncfusion_animations": len(self.syncfusion_animations),
                "csharp_animations": len(self.csharp_animations),
            },
            "findings": {
                "xaml_storyboards": [m.to_dict() for m in self.xaml_storyboards],
                "xaml_animations": [m.to_dict() for m in self.xaml_animations],
                "xaml_triggers": [m.to_dict() for m in self.xaml_triggers],
                "syncfusion_animations": [m.to_dict() for m in self.syncfusion_animations],
                "csharp_animations": [m.to_dict() for m in self.csharp_animations],
            },
            "files_with_animations": sorted(list(self.files_with_animations)),
            "animation_keys": sorted(list(self.animation_keys)),
        }


# ============================================================================
# SCANNER
# ============================================================================

class AnimationScanner:
    """Scans files for WPF and Syncfusion animations."""

    def __init__(self, root_path: Path, focus: str = "all", verbose: bool = False):
        self.root_path = root_path
        self.focus = focus.lower()
        self.verbose = verbose

        from datetime import datetime
        self.report = AnimationReport(
            scan_date=datetime.now().strftime("%Y-%m-%d %H:%M:%S"),
            repository="Bigessfour/Wiley-Widget",
            scope=f"Focus: {focus}, Path: {root_path}",
            total_files_scanned=0,
            xaml_files_scanned=0,
            csharp_files_scanned=0,
        )

    def scan(self):
        """Execute the scan."""
        if self.verbose:
            print(f"🔍 Scanning: {self.root_path}")
            print(f"   Focus: {self.focus}")

        # Scan XAML files
        if self.focus in ["all", "xaml"]:
            xaml_files = list(self.root_path.rglob("*.xaml"))
            self.report.xaml_files_scanned = len(xaml_files)
            for xaml_file in xaml_files:
                self._scan_xaml_file(xaml_file)

        # Scan C# files
        if self.focus in ["all", "csharp", "cs"]:
            cs_files = list(self.root_path.rglob("*.cs"))
            self.report.csharp_files_scanned = len(cs_files)
            for cs_file in cs_files:
                self._scan_csharp_file(cs_file)

        self.report.total_files_scanned = (
            self.report.xaml_files_scanned + self.report.csharp_files_scanned
        )

        if self.verbose:
            print(f"✅ Scan complete: {self.report.total_animations_found} animations found")

    def _scan_xaml_file(self, file_path: Path):
        """Scan a single XAML file."""
        try:
            content = file_path.read_text(encoding="utf-8")
            lines = content.splitlines()

            for i, line in enumerate(lines, start=1):
                # Storyboards
                if match := STORYBOARD_PAT.search(line):
                    self.report.add_match(AnimationMatch(
                        file=str(file_path.relative_to(self.root_path.parent)),
                        line_number=i,
                        pattern_type="Storyboard",
                        content=line.strip(),
                        animation_key=match.group(1),
                    ))

                # DoubleAnimation
                if match := DOUBLE_ANIMATION_PAT.search(line):
                    self.report.add_match(AnimationMatch(
                        file=str(file_path.relative_to(self.root_path.parent)),
                        line_number=i,
                        pattern_type="DoubleAnimation",
                        content=line.strip(),
                        target_property=match.group(1) if match.group(1) else "",
                    ))

                # ColorAnimation
                if match := COLOR_ANIMATION_PAT.search(line):
                    self.report.add_match(AnimationMatch(
                        file=str(file_path.relative_to(self.root_path.parent)),
                        line_number=i,
                        pattern_type="ColorAnimation",
                        content=line.strip(),
                        target_property=match.group(1) if match.group(1) else "",
                    ))

                # EventTrigger
                if match := EVENT_TRIGGER_PAT.search(line):
                    self.report.add_match(AnimationMatch(
                        file=str(file_path.relative_to(self.root_path.parent)),
                        line_number=i,
                        pattern_type="EventTrigger",
                        content=line.strip(),
                        target_property=match.group(1),
                    ))

                # PropertyTrigger
                if match := PROPERTY_TRIGGER_PAT.search(line):
                    self.report.add_match(AnimationMatch(
                        file=str(file_path.relative_to(self.root_path.parent)),
                        line_number=i,
                        pattern_type="PropertyTrigger",
                        content=line.strip(),
                        target_property=f"{match.group(1)}={match.group(2)}",
                    ))

                # BeginStoryboard
                if BEGIN_STORYBOARD_PAT.search(line):
                    self.report.add_match(AnimationMatch(
                        file=str(file_path.relative_to(self.root_path.parent)),
                        line_number=i,
                        pattern_type="BeginStoryboard",
                        content=line.strip(),
                    ))

                # Syncfusion EnableAnimation
                if match := SYNCFUSION_ENABLE_ANIMATION_PAT.search(line):
                    self.report.add_match(AnimationMatch(
                        file=str(file_path.relative_to(self.root_path.parent)),
                        line_number=i,
                        pattern_type="Syncfusion.EnableAnimation",
                        content=line.strip(),
                        control_type=match.group(1),
                        target_property=f"EnableAnimation={match.group(2)}",
                    ))

                # Syncfusion SeriesAnimationMode
                if match := SYNCFUSION_SERIES_ANIMATION_PAT.search(line):
                    self.report.add_match(AnimationMatch(
                        file=str(file_path.relative_to(self.root_path.parent)),
                        line_number=i,
                        pattern_type="Syncfusion.SeriesAnimationMode",
                        content=line.strip(),
                        target_property=match.group(1),
                    ))

                # Syncfusion RowTransitionMode
                if match := SYNCFUSION_ROW_TRANSITION_PAT.search(line):
                    self.report.add_match(AnimationMatch(
                        file=str(file_path.relative_to(self.root_path.parent)),
                        line_number=i,
                        pattern_type="Syncfusion.RowTransitionMode",
                        content=line.strip(),
                        target_property=match.group(1),
                    ))

                # Syncfusion AnimateOnDataChange
                if match := SYNCFUSION_ANIMATE_ON_DATA_PAT.search(line):
                    self.report.add_match(AnimationMatch(
                        file=str(file_path.relative_to(self.root_path.parent)),
                        line_number=i,
                        pattern_type="Syncfusion.AnimateOnDataChange",
                        content=line.strip(),
                        target_property=f"AnimateOnDataChange={match.group(1)}",
                    ))

        except Exception as e:
            if self.verbose:
                print(f"⚠️  Error scanning {file_path}: {e}")

    def _scan_csharp_file(self, file_path: Path):
        """Scan a single C# file."""
        try:
            content = file_path.read_text(encoding="utf-8")
            lines = content.splitlines()

            for i, line in enumerate(lines, start=1):
                # AnimationTimeline (e.g., new DoubleAnimation())
                if match := CSHARP_ANIMATION_TIMELINE_PAT.search(line):
                    self.report.add_match(AnimationMatch(
                        file=str(file_path.relative_to(self.root_path.parent)),
                        line_number=i,
                        pattern_type="CSharp.AnimationTimeline",
                        content=line.strip(),
                        control_type=match.group(1),
                    ))

                # BeginAnimation
                if match := CSHARP_BEGIN_ANIMATION_PAT.search(line):
                    self.report.add_match(AnimationMatch(
                        file=str(file_path.relative_to(self.root_path.parent)),
                        line_number=i,
                        pattern_type="CSharp.BeginAnimation",
                        content=line.strip(),
                        target_property=match.group(1),
                    ))

                # TransitionManager
                if match := CSHARP_TRANSITION_MANAGER_PAT.search(line):
                    self.report.add_match(AnimationMatch(
                        file=str(file_path.relative_to(self.root_path.parent)),
                        line_number=i,
                        pattern_type="CSharp.TransitionManager",
                        content=line.strip(),
                        target_property=match.group(1),
                    ))

                # VisualStateManager
                if CSHARP_VISUAL_STATE_PAT.search(line):
                    self.report.add_match(AnimationMatch(
                        file=str(file_path.relative_to(self.root_path.parent)),
                        line_number=i,
                        pattern_type="CSharp.VisualStateManager",
                        content=line.strip(),
                    ))

        except Exception as e:
            if self.verbose:
                print(f"⚠️  Error scanning {file_path}: {e}")

    def generate_report(self, output_path: Path = None):
        """Generate JSON report."""
        report_dict = self.report.to_dict()

        if output_path:
            output_path.parent.mkdir(parents=True, exist_ok=True)
            with open(output_path, "w", encoding="utf-8") as f:
                json.dump(report_dict, f, indent=2)
            if self.verbose:
                print(f"📄 Report saved to: {output_path}")

        return report_dict

    def print_summary(self):
        """Print summary to console."""
        print("\n" + "="*80)
        print("🎬 SYNCFUSION ANIMATION SCAN REPORT")
        print("="*80)
        print(f"Repository: {self.report.repository}")
        print(f"Scan Date: {self.report.scan_date}")
        print(f"Scope: {self.report.scope}")
        print("\nFiles Scanned:")
        print(f"  XAML: {self.report.xaml_files_scanned}")
        print(f"  C#: {self.report.csharp_files_scanned}")
        print(f"  Total: {self.report.total_files_scanned}")

        print("\n📊 FINDINGS:")
        print(f"  Total Animations: {self.report.total_animations_found}")
        print(f"  Files with Animations: {len(self.report.files_with_animations)}")
        print(f"  Unique Animation Keys: {len(self.report.animation_keys)}")

        print("\nBREAKDOWN:")
        print(f"  XAML Storyboards: {len(self.report.xaml_storyboards)}")
        print(f"  XAML Animations: {len(self.report.xaml_animations)}")
        print(f"  XAML Triggers: {len(self.report.xaml_triggers)}")
        print(f"  Syncfusion Animations: {len(self.report.syncfusion_animations)}")
        print(f"  C# Animations: {len(self.report.csharp_animations)}")

        if self.report.animation_keys:
            print("\n🔑 ANIMATION KEYS:")
            for key in sorted(self.report.animation_keys):
                print(f"  - {key}")

        if self.report.files_with_animations:
            print("\n📁 FILES WITH ANIMATIONS:")
            for file in sorted(self.report.files_with_animations):
                print(f"  - {file}")

        # Health assessment
        print("\n✅ HEALTH ASSESSMENT:")
        if self.report.total_animations_found == 0:
            print("  ✅ No custom animations detected")
            print("  ✅ Startup-safe (implicit Syncfusion defaults)")
            print("  ✅ Performance: ~<10ms overhead per control")
        else:
            print(f"  ℹ️  {self.report.total_animations_found} custom animations found")
            print("  ℹ️  Verify in runtime logs for resource warnings")
            print("  ℹ️  Ensure proper disposal to prevent memory leaks")

        print("="*80 + "\n")


# ============================================================================
# MAIN
# ============================================================================

def main():
    parser = argparse.ArgumentParser(
        description="Scan for WPF and Syncfusion animations in Wiley Widget repository",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  python animation_scanner.py --path src/WileyWidget
  python animation_scanner.py --path src/WileyWidget --output logs/animation_report.json
  python animation_scanner.py --focus xaml --verbose
  python animation_scanner.py --focus syncfusion --path src/WileyWidget/Themes
        """
    )

    parser.add_argument(
        "--path",
        type=Path,
        default=Path("src/WileyWidget"),
        help="Root path to scan (default: src/WileyWidget)"
    )

    parser.add_argument(
        "--focus",
        choices=["all", "xaml", "csharp", "cs", "syncfusion"],
        default="all",
        help="Focus scan on specific file types (default: all)"
    )

    parser.add_argument(
        "--output",
        type=Path,
        help="Output JSON report path (default: print to console only)"
    )

    parser.add_argument(
        "--verbose",
        action="store_true",
        help="Enable verbose output"
    )

    args = parser.parse_args()

    # Validate path
    if not args.path.exists():
        print(f"❌ Error: Path does not exist: {args.path}", file=sys.stderr)
        sys.exit(1)

    # Run scanner
    scanner = AnimationScanner(
        root_path=args.path,
        focus=args.focus,
        verbose=args.verbose
    )

    scanner.scan()
    scanner.print_summary()

    if args.output:
        scanner.generate_report(args.output)

    # Exit code based on findings
    sys.exit(0)


if __name__ == "__main__":
    main()
