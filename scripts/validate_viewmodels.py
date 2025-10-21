#!/usr/bin/env python3
"""Validate XAML view bindings against their corresponding view models."""

from __future__ import annotations

import argparse
import json
import re
import sys
from dataclasses import dataclass, field
from pathlib import Path
from typing import Iterable

_DATA_CONTEXT_PATTERN = re.compile(r'DataContext="\{Binding\s+([^,}]+)')
_BINDING_PATTERN = re.compile(r"\{Binding\s+([^}]+)\}")

_OBSERVABLE_FIELD_PATTERN = re.compile(
    r"\[ObservableProperty[^\]]*\][\s\r\n]*private\s+[^\s]+\s+([_\w]+)",
    flags=re.MULTILINE,
)
_PUBLIC_PROPERTY_PATTERN = re.compile(
    r"public\s+[^\s]+\s+(\w+)\s*\{",
    flags=re.MULTILINE,
)
_RELAY_COMMAND_PATTERN = re.compile(
    r"\[RelayCommand[^\]]*\][\s\r\n]*(?:private|public)?\s*(?:async\s+)?[\w<>,\s]+\s+(\w+)\s*\(",
    flags=re.MULTILINE,
)
_MANUAL_COMMAND_PATTERN = re.compile(
    r"public\s+ICommand\s+(\w+)",
    flags=re.MULTILINE,
)

_EXCLUDED_FOLDERS = {"obj", "bin"}
_VIEW_SUFFIXES = ("View", "Window")
_EXPECTED_VIEW_VARIANTS = ("View", "Window", "PanelView")


@dataclass
class ViewBindingInfo:
    properties: set[str] = field(default_factory=set)
    commands: set[str] = field(default_factory=set)


@dataclass
class ViewInfo:
    name: str
    path: Path
    data_context: str | None
    bindings: ViewBindingInfo


@dataclass
class ViewModelInfo:
    name: str
    path: Path
    properties: set[str] = field(default_factory=set)
    commands: set[str] = field(default_factory=set)


@dataclass
class ValidationIssue:
    type: str
    severity: str
    view: str | None
    view_model: str | None
    message: str
    file: Path

    def to_dict(self) -> dict[str, str | None]:
        return {
            "type": self.type,
            "severity": self.severity,
            "view": self.view,
            "viewModel": self.view_model,
            "message": self.message,
            "file": str(self.file),
        }


def _convert_field_name_to_property(field_name: str) -> str:
    if not field_name:
        return field_name

    trimmed = field_name.strip("_")
    if not trimmed:
        return field_name

    if "_" in trimmed:
        segments = [segment for segment in trimmed.split("_") if segment]
        return "".join(segment[0].upper() + segment[1:] for segment in segments)

    return trimmed[0].upper() + trimmed[1:]


def _is_relevant_path(path: Path) -> bool:
    return not any(part in _EXCLUDED_FOLDERS for part in path.parts)


def _find_data_context(xaml_content: str) -> str | None:
    match = _DATA_CONTEXT_PATTERN.search(xaml_content)
    if match:
        return match.group(1).strip()
    return None


def _parse_binding_info(xaml_content: str) -> ViewBindingInfo:
    info = ViewBindingInfo()

    for match in _BINDING_PATTERN.finditer(xaml_content):
        expression = match.group(1)
        parts = [part.strip() for part in expression.split(",") if part.strip()]
        binding_path: str | None = None

        for part in parts:
            if "=" in part:
                key, value = (segment.strip() for segment in part.split("=", 1))
                if key.lower() == "path":
                    binding_path = value
                    break
        else:
            if parts and "=" not in parts[0]:
                binding_path = parts[0]

        if not binding_path or binding_path == ".":
            continue

        binding_path = binding_path.split(".")[0].strip()
        binding_path = binding_path.split("[")[0].strip()

        if not binding_path:
            continue
        if binding_path.startswith(("ElementName", "RelativeSource", "StaticResource")):
            continue

        if binding_path.endswith("Command"):
            info.commands.add(binding_path)
        else:
            info.properties.add(binding_path)

    return info


def _iter_view_files(src_root: Path) -> Iterable[Path]:
    for path in src_root.rglob("*.xaml"):
        if path.is_file() and _is_relevant_path(path):
            yield path


def _gather_views(project_root: Path) -> list[ViewInfo]:
    src_root = project_root / "src"
    views: list[ViewInfo] = []

    for xaml_path in _iter_view_files(src_root):
        name = xaml_path.stem
        if not name.endswith(_VIEW_SUFFIXES):
            continue

        content = xaml_path.read_text(encoding="utf-8")
        binding_info = _parse_binding_info(content)
        data_context = _find_data_context(content)

        views.append(
            ViewInfo(
                name=name,
                path=xaml_path,
                data_context=data_context,
                bindings=binding_info,
            )
        )

    return views


def _iter_view_model_files(src_root: Path) -> Iterable[Path]:
    for path in src_root.rglob("*ViewModel.cs"):
        if path.is_file() and _is_relevant_path(path):
            yield path


def _gather_view_models(project_root: Path) -> list[ViewModelInfo]:
    src_root = project_root / "src"
    view_models: list[ViewModelInfo] = []

    for vm_path in _iter_view_model_files(src_root):
        content = vm_path.read_text(encoding="utf-8")
        properties: set[str] = set()
        commands: set[str] = set()

        for match in _OBSERVABLE_FIELD_PATTERN.finditer(content):
            property_name = _convert_field_name_to_property(match.group(1))
            if property_name:
                properties.add(property_name)

        for match in _PUBLIC_PROPERTY_PATTERN.finditer(content):
            properties.add(match.group(1))

        for match in _RELAY_COMMAND_PATTERN.finditer(content):
            method_name = match.group(1)
            command_name = f"{method_name.removesuffix('Async')}Command"
            commands.add(command_name)

        for match in _MANUAL_COMMAND_PATTERN.finditer(content):
            commands.add(match.group(1))

        view_models.append(
            ViewModelInfo(
                name=vm_path.stem,
                path=vm_path,
                properties=properties,
                commands=commands,
            )
        )

    return view_models


def _find_view_model_for_view(
    view: ViewInfo, view_models: list[ViewModelInfo]
) -> ViewModelInfo | None:
    candidates: list[str] = []

    if view.data_context and view.data_context.endswith("ViewModel"):
        candidates.append(view.data_context)

    candidates.append(f"{view.name}ViewModel")

    if view.name.endswith("View"):
        base = view.name[:-4]
        candidates.append(f"{base}ViewModel")
    if view.name.endswith("Window"):
        base = view.name[:-6]
        candidates.append(f"{base}ViewModel")
    if view.name.endswith("PanelView"):
        base = view.name[:-9]
        candidates.append(f"{base}ViewModel")

    unique_candidates = []
    for candidate in candidates:
        if candidate and candidate not in unique_candidates:
            unique_candidates.append(candidate)

    vm_lookup = {vm.name: vm for vm in view_models}
    for candidate in unique_candidates:
        if candidate in vm_lookup:
            return vm_lookup[candidate]

    return None


def _expected_views_for_view_model(view_model: ViewModelInfo) -> list[str]:
    base = view_model.name.removesuffix("ViewModel")
    candidates = [base]
    candidates.extend(f"{base}{suffix}" for suffix in _EXPECTED_VIEW_VARIANTS)
    return candidates


def _validate(
    views: list[ViewInfo], view_models: list[ViewModelInfo]
) -> list[ValidationIssue]:
    issues: list[ValidationIssue] = []
    vm_lookup = {vm.name: vm for vm in view_models}

    for view in views:
        view_model = _find_view_model_for_view(view, view_models)
        if view_model is None:
            issues.append(
                ValidationIssue(
                    type="MissingViewModel",
                    severity="Error",
                    view=view.name,
                    view_model=f"{view.name}ViewModel",
                    message=f"No matching ViewModel found for view '{view.name}'",
                    file=view.path,
                )
            )
            continue

        for binding in sorted(view.bindings.properties):
            if binding not in view_model.properties:
                issues.append(
                    ValidationIssue(
                        type="MissingProperty",
                        severity="Error",
                        view=view.name,
                        view_model=view_model.name,
                        message=f"Binding '{binding}' not found in ViewModel '{view_model.name}'",
                        file=view.path,
                    )
                )

        for command in sorted(view.bindings.commands):
            if command not in view_model.commands:
                issues.append(
                    ValidationIssue(
                        type="MissingCommand",
                        severity="Error",
                        view=view.name,
                        view_model=view_model.name,
                        message=f"Command '{command}' not found in ViewModel '{view_model.name}'",
                        file=view.path,
                    )
                )

    view_names = {view.name for view in views}
    for view_model in view_models:
        expected_views = _expected_views_for_view_model(view_model)
        if not any(expected in view_names for expected in expected_views):
            issues.append(
                ValidationIssue(
                    type="MissingView",
                    severity="Warning",
                    view=", ".join(expected_views),
                    view_model=view_model.name,
                    message=f"No view found for ViewModel '{view_model.name}'",
                    file=view_model.path,
                )
            )

    return issues


def _print_report(
    views: list[ViewInfo],
    view_models: list[ViewModelInfo],
    issues: list[ValidationIssue],
    *,
    detailed: bool,
) -> None:
    print("=== VIEW-VIEWMODEL VALIDATION REPORT ===")
    print(f"Views analyzed   : {len(views)}")
    print(f"ViewModels found : {len(view_models)}")
    print(f"Issues detected  : {len(issues)}")

    errors = [issue for issue in issues if issue.severity == "Error"]
    warnings = [issue for issue in issues if issue.severity == "Warning"]

    if errors:
        print(f"-- Errors ({len(errors)}) --")
        for issue in errors:
            print(f"[{issue.type}] {issue.message}")
            print(f"    View: {issue.view} | ViewModel: {issue.view_model}")
            print(f"    File: {issue.file}")

    if warnings:
        print(f"-- Warnings ({len(warnings)}) --")
        for issue in warnings:
            print(f"[{issue.type}] {issue.message}")
            print(f"    ViewModel: {issue.view_model}")
            print(f"    File: {issue.file}")

    if not issues:
        print("✅ All View-ViewModel relationships validated successfully.")

    if detailed:
        vm_lookup = {vm.name: vm for vm in view_models}
        print("-- Detailed Mapping --")
        for view in sorted(views, key=lambda item: item.name):
            view_model = _find_view_model_for_view(view, view_models)
            status = "OK" if view_model else "Missing VM"
            vm_name = view_model.name if view_model else "(none)"
            print(f"{status}: {view.name} -> {vm_name}")

        print("-- Orphaned ViewModels --")
        view_names = {view.name for view in views}
        for vm in sorted(view_models, key=lambda item: item.name):
            expected = _expected_views_for_view_model(vm)
            if not any(candidate in view_names for candidate in expected):
                print(f"Orphan: {vm.name}")


def _parse_args() -> argparse.Namespace:
    default_root = Path(__file__).resolve().parent.parent

    parser = argparse.ArgumentParser(
        description="Validate that XAML bindings align with their ViewModels",
    )
    parser.add_argument(
        "--project-root",
        type=Path,
        default=default_root,
        help="Path to the project root (defaults to repository root)",
    )
    parser.add_argument(
        "--detailed",
        action="store_true",
        help="Print detailed mapping information",
    )
    parser.add_argument(
        "--json",
        action="store_true",
        help="Emit results as JSON instead of human-readable text",
    )

    return parser.parse_args()


def _emit_json_report(
    views: list[ViewInfo],
    view_models: list[ViewModelInfo],
    issues: list[ValidationIssue],
) -> None:
    payload = {
        "views": len(views),
        "viewModels": len(view_models),
        "issues": [issue.to_dict() for issue in issues],
        "errors": [issue.to_dict() for issue in issues if issue.severity == "Error"],
        "warnings": [
            issue.to_dict() for issue in issues if issue.severity == "Warning"
        ],
    }
    json.dump(payload, sys.stdout, indent=2)
    sys.stdout.write("\n")


def main() -> int:
    args = _parse_args()
    project_root = args.project_root.resolve()

    if not project_root.exists():
        print(f"Project root does not exist: {project_root}", file=sys.stderr)
        return 2

    views = _gather_views(project_root)
    view_models = _gather_view_models(project_root)
    issues = _validate(views, view_models)

    if args.json:
        _emit_json_report(views, view_models, issues)
    else:
        _print_report(views, view_models, issues, detailed=args.detailed)

    return 1 if any(issue.severity == "Error" for issue in issues) else 0


if __name__ == "__main__":
    raise SystemExit(main())
