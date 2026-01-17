"""Find and optionally add CancellationToken propagation for repository calls."""

from __future__ import annotations

import argparse
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable

EXCLUDE_DIRS = {"bin", "obj", ".git", "TestResults", ".venv", "__pycache__"}
MIN_PYTHON = (3, 10)
MAX_TESTED_PYTHON = (3, 14)

INTERFACE_METHOD_PATTERN = re.compile(
    r"(Task(?:<[^;]+?>)?\s+\w+\s*)\(([^)]*)\)\s*;",
    re.DOTALL,
)
METHOD_NAME_PATTERN = re.compile(r"Task(?:<[^>]+>)?\s+(\w+)\s*")

CLASS_IMPLEMENTS_REPO_PATTERN = re.compile(r"class\s+\w+\s*:\s*[^\{]*I\w*Repository")

METHOD_SIGNATURE_PATTERN = re.compile(
    r"(?P<sig>(?:public|internal|protected|private)\s+(?:async\s+)?Task(?:<[^>]+>)?\s+\w+\s*\([^\)]*\))\s*\{",
    re.DOTALL,
)


@dataclass(frozen=True)
class Replacement:
    start: int
    end: int
    new_text: str
    reason: str


@dataclass(frozen=True)
class MethodSpan:
    name: str
    params: str
    body_start: int
    body_end: int
    token_name: str | None


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Find and optionally add CancellationToken to repository APIs and calls."
    )
    parser.add_argument(
        "--root",
        default="src",
        help="Root directory to scan (default: src).",
    )
    mode_group = parser.add_mutually_exclusive_group()
    mode_group.add_argument(
        "--dry-run",
        action="store_true",
        help="Analyze and report changes without writing files (default).",
    )
    mode_group.add_argument(
        "--apply",
        action="store_true",
        help="Write changes to files.",
    )
    parser.add_argument(
        "--force-apply",
        action="store_true",
        help="Allow apply on untested Python versions.",
    )
    parser.add_argument(
        "--include-tests",
        action="store_true",
        help="Include tests and tooling folders in the scan.",
    )
    parser.set_defaults(dry_run=True, apply=False, force_apply=False)
    return parser.parse_args()


def iter_cs_files(root: Path, include_tests: bool) -> Iterable[Path]:
    for path in root.rglob("*.cs"):
        if not include_tests:
            if any(part in EXCLUDE_DIRS for part in path.parts):
                continue
        yield path


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def write_text(path: Path, text: str) -> None:
    path.write_text(text, encoding="utf-8")


def validate_python_runtime(requested_apply: bool, allow_untested: bool) -> bool:
    current = sys.version_info[:3]
    if current < MIN_PYTHON:
        raise SystemExit(
            "Python 3.10+ is required for this script. "
            f"Detected {current[0]}.{current[1]}.{current[2]}."
        )

    if current > MAX_TESTED_PYTHON:
        if allow_untested:
            print(
                "Warning: applying changes on an untested Python version "
                f"({current[0]}.{current[1]}). Proceeding due to --force-apply.",
                file=sys.stderr,
            )
            return False

        print(
            "Warning: Python version is newer than tested "
            f"(max tested {MAX_TESTED_PYTHON[0]}.{MAX_TESTED_PYTHON[1]}). "
            "For safety, forcing dry-run mode.",
            file=sys.stderr,
        )
        return True

    if requested_apply:
        print(
            "Python runtime validated for apply mode: "
            f"{current[0]}.{current[1]}.{current[2]}",
            file=sys.stderr,
        )

    return False


def is_repo_interface(path: Path) -> bool:
    return path.name.startswith("I") and path.name.endswith("Repository.cs")


def extract_method_name(signature_prefix: str) -> str | None:
    match = METHOD_NAME_PATTERN.search(signature_prefix)
    if match:
        return match.group(1)
    return None


def append_token_param(params: str) -> str:
    if "CancellationToken" in params:
        return params

    if params.strip() == "":
        return "CancellationToken cancellationToken = default"

    if "\n" in params:
        return params.rstrip() + ",\n        CancellationToken cancellationToken = default"

    return params.rstrip() + ", CancellationToken cancellationToken = default"


def collect_repo_methods(interface_paths: Iterable[Path]) -> set[str]:
    methods: set[str] = set()
    for path in interface_paths:
        text = read_text(path)
        for match in INTERFACE_METHOD_PATTERN.finditer(text):
            name = extract_method_name(match.group(1))
            if name:
                methods.add(name)
    return methods


def build_interface_replacements(text: str) -> list[Replacement]:
    replacements: list[Replacement] = []
    for match in INTERFACE_METHOD_PATTERN.finditer(text):
        prefix = match.group(1)
        params = match.group(2)
        if "CancellationToken" in params:
            continue

        new_params = append_token_param(params)
        new_text = f"{prefix}({new_params});"
        replacements.append(
            Replacement(
                start=match.start(),
                end=match.end(),
                new_text=new_text,
                reason="interface signature",
            )
        )
    return replacements


def build_implementation_replacements(text: str, method_names: set[str]) -> list[Replacement]:
    if not CLASS_IMPLEMENTS_REPO_PATTERN.search(text):
        return []

    replacements: list[Replacement] = []
    for name in method_names:
        pattern = re.compile(
            rf"(?P<prefix>(?:public|internal|protected|private)\s+(?:async\s+)?Task(?:<[^>]+>)?\s+{re.escape(name)}\s*\()(?P<params>[^\)]*)(?P<suffix>\))",
            re.DOTALL,
        )
        match = pattern.search(text)
        if not match:
            continue

        params = match.group("params")
        if "CancellationToken" in params:
            continue

        new_params = append_token_param(params)
        new_text = f"{match.group('prefix')}{new_params}{match.group('suffix')}"
        replacements.append(
            Replacement(
                start=match.start(),
                end=match.end(),
                new_text=new_text,
                reason="implementation signature",
            )
        )
    return replacements


def find_matching_brace(text: str, start_index: int) -> int | None:
    depth = 0
    in_string = False
    escape = False
    for idx in range(start_index, len(text)):
        ch = text[idx]
        if in_string:
            if escape:
                escape = False
            elif ch == "\\":
                escape = True
            elif ch == '"':
                in_string = False
            continue

        if ch == '"':
            in_string = True
            continue
        if ch == "{":
            depth += 1
        elif ch == "}":
            depth -= 1
            if depth == 0:
                return idx
    return None


def parse_method_spans(text: str) -> list[MethodSpan]:
    spans: list[MethodSpan] = []
    for match in METHOD_SIGNATURE_PATTERN.finditer(text):
        signature = match.group("sig")
        body_start = match.end() - 1
        body_end = find_matching_brace(text, body_start)
        if body_end is None:
            continue

        name_match = re.search(r"\s(\w+)\s*\(", signature)
        if not name_match:
            continue

        name = name_match.group(1)
        params_match = re.search(r"\(([^)]*)\)", signature, re.DOTALL)
        params = params_match.group(1) if params_match else ""
        token_name = None
        token_match = re.search(r"CancellationToken\s+(\w+)", params)
        if token_match:
            token_name = token_match.group(1)

        spans.append(
            MethodSpan(
                name=name,
                params=params,
                body_start=body_start,
                body_end=body_end,
                token_name=token_name,
            )
        )
    return spans


def find_token_variable(body: str) -> str | None:
    token_match = re.search(r"CancellationToken\s+(\w+)", body)
    if token_match:
        return token_match.group(1)

    token_match = re.search(r"(\w+)\s*=\s*[^;]*?\.Token\b", body)
    if token_match:
        return token_match.group(1)

    return None


def find_call_paren(text: str, start_index: int) -> tuple[int, int] | None:
    depth = 0
    in_string = False
    escape = False
    for idx in range(start_index, len(text)):
        ch = text[idx]
        if in_string:
            if escape:
                escape = False
            elif ch == "\\":
                escape = True
            elif ch == '"':
                in_string = False
            continue

        if ch == '"':
            in_string = True
            continue

        if ch == "(":
            if depth == 0:
                start_index = idx
            depth += 1
        elif ch == ")":
            depth -= 1
            if depth == 0:
                return start_index, idx
    return None


def should_append_token(args_text: str, token_name: str) -> bool:
    lowered = args_text.lower()
    if token_name.lower() in lowered:
        return False
    if "cancellationtoken" in lowered:
        return False
    if "ct" in lowered or "token" in lowered:
        return False
    return True


def append_token_to_args(args_text: str, token_name: str) -> str:
    if args_text.strip() == "":
        return token_name
    return f"{args_text}, {token_name}"


def build_callsite_replacements(text: str, method_names: set[str]) -> list[Replacement]:
    replacements: list[Replacement] = []
    spans = parse_method_spans(text)

    for span in spans:
        body = text[span.body_start : span.body_end + 1]
        token_name = span.token_name or find_token_variable(body)
        if not token_name:
            continue

        for method_name in method_names:
            for call_match in re.finditer(rf"\.{re.escape(method_name)}\s*\(", body):
                call_start = span.body_start + call_match.start()
                paren_start = call_start + call_match.group(0).rfind("(")
                paren_span = find_call_paren(text, paren_start)
                if not paren_span:
                    continue

                args_start, args_end = paren_span
                args_text = text[args_start + 1 : args_end]
                if not should_append_token(args_text, token_name):
                    continue

                new_args = append_token_to_args(args_text, token_name)
                replacements.append(
                    Replacement(
                        start=args_start + 1,
                        end=args_end,
                        new_text=new_args,
                        reason="callsite",
                    )
                )

    return replacements


def apply_replacements(text: str, replacements: list[Replacement]) -> str:
    if not replacements:
        return text

    ordered = sorted(replacements, key=lambda r: r.start, reverse=True)
    updated = text
    for rep in ordered:
        updated = updated[: rep.start] + rep.new_text + updated[rep.end :]
    return updated


def main() -> int:
    args = parse_args()
    root = Path(args.root)
    if not root.exists():
        raise SystemExit(f"Root not found: {root}")

    force_dry_run = validate_python_runtime(args.apply, args.force_apply)
    apply_changes = args.apply and not force_dry_run
    dry_run = not apply_changes

    interface_paths = [p for p in iter_cs_files(root, args.include_tests) if is_repo_interface(p)]
    repo_methods = collect_repo_methods(interface_paths)

    total_changes = 0
    total_files = 0

    for path in iter_cs_files(root, args.include_tests):
        text = read_text(path)
        replacements: list[Replacement] = []

        if is_repo_interface(path):
            replacements.extend(build_interface_replacements(text))

        replacements.extend(build_implementation_replacements(text, repo_methods))
        replacements.extend(build_callsite_replacements(text, repo_methods))

        if not replacements:
            continue

        total_files += 1
        total_changes += len(replacements)

        if apply_changes:
            updated = apply_replacements(text, replacements)
            if updated != text:
                write_text(path, updated)
        else:
            print(f"{path}: {len(replacements)} change(s)")
            for rep in replacements:
                print(f"  - {rep.reason} at {rep.start}-{rep.end}")

    print(f"Scanned {total_files} file(s) with {total_changes} change(s)")
    if dry_run:
        print("Dry-run mode: no files were written.")
        print("Run with --apply to write changes (unless forced dry-run for safety).")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
