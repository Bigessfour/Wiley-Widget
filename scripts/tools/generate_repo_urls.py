"""
Generate a lightweight ai-fetchable-manifest.json for the repository.
This script is intentionally simple and fast: it walks the workspace, records file paths, sizes,
and a small summary (total files, total size). It attempts to capture git metadata when available.

Usage:
  python scripts/tools/generate_repo_urls.py -o ../ai-fetchable-manifest.json

By default it writes to <workspace root>/ai-fetchable-manifest.json

This is a replacement/fallback for historical tooling.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import subprocess
import sys
from datetime import datetime, timedelta
from pathlib import Path
from typing import Any

EXCLUDE_DIRS = {
    ".git",
    "node_modules",
    "bin",
    "obj",
    ".venv",
    "__pycache__",
    "TestResults",
    "logs",
    "xaml-logs",
    ".vs",
}
TEXT_EXTS = {
    ".md",
    ".txt",
    ".json",
    ".yaml",
    ".yml",
    ".py",
    ".ps1",
    ".cs",
    ".csproj",
    ".sln",
    ".xml",
    ".xaml",
    ".sql",
    ".sh",
    ".psd1",
    ".psm1",
    ".gitignore",
    ".gitattributes",
    ".editorconfig",
    ".toml",
    ".html",
    ".htm",
    ".csv",
}
LANG_BY_EXT = {
    ".py": "Python",
    ".ps1": "PowerShell",
    ".cs": "C#",
    ".xaml": "XAML",
    ".csproj": "C# Project",
    ".sln": "Solution",
    ".md": "Markdown",
    ".json": "JSON",
    ".yaml": "YAML",
    ".yml": "YAML",
    ".xml": "XML",
    ".sql": "SQL",
    ".sh": "Shell",
}
CATEGORY_HINTS = [
    ("test", "test"),
    ("tests", "test"),
    ("scripts", "automation"),
    ("tools", "automation"),
    ("docs", "documentation"),
    ("README", "documentation"),
    (".github", "automation"),
    ("sql", "repository"),
    ("src", "source_code"),
    ("Views", "views"),
    ("ViewModels", "viewmodels"),
]


def repo_git_info(root: Path) -> dict:
    data: dict[str, Any] = {
        "remote_url": None,
        "owner_repo": None,
        "branch": None,
        "commit_hash": None,
        "is_dirty": None,
    }

    # Only try if .git exists
    if (root / ".git").exists():

        def run_git(args):
            try:
                out = subprocess.check_output(
                    ["git", "-C", str(root)] + args, stderr=subprocess.DEVNULL
                )
                return out.decode("utf-8", errors="replace").strip()
            except Exception:
                return None

        remote = run_git(["config", "--get", "remote.origin.url"])
        branch = run_git(["rev-parse", "--abbrev-ref", "HEAD"])
        commit = run_git(["rev-parse", "HEAD"])
        dirty = run_git(["status", "--porcelain"])

        data["remote_url"] = remote

        # only proceed if the remote string exists and looks like a git remote
        if remote and remote.endswith(".git"):
            # normalise ssh urls
            cleaned = remote
            if cleaned.startswith("git@"):
                cleaned = cleaned.replace(":", "/").replace("git@", "https://")
            if cleaned.startswith("https://") and cleaned.endswith(".git"):
                cleaned = cleaned[:-4]
            try:
                owner_repo = "/".join(cleaned.split("/")[-2:])
                data["owner_repo"] = owner_repo
            except Exception:
                data["owner_repo"] = None

        # assign branch/commit/dirty metadata regardless of remote parsing success
        data["branch"] = branch
        data["commit_hash"] = commit
        data["is_dirty"] = bool(dirty)

    return data


def safe_hash(path: Path, max_bytes=8192) -> str:
    """Return a short SHA1 hash of the first max_bytes of a file to give a stable fingerprint.

    Use usedforsecurity=False to suppress security lint warnings for non-security fingerprints.
    Provide a fallback for Python versions that don't accept usedforsecurity.
    """
    try:
        # In Python 3.11+ the `usedforsecurity` kwarg can be set to False to indicate this is not for security.
        h = hashlib.sha1(usedforsecurity=False)  # type: ignore[arg-type]
    except TypeError:
        # Older Python versions don't support usedforsecurity; fall back to default sha1.
        # This is intentionally a non-security fingerprint used only for manifest stability;
        # mark as nosec so static scanners don't treat it as a security issue.
        h = hashlib.sha1()  # nosec
    try:
        with path.open("rb") as f:
            chunk = f.read(max_bytes)
            h.update(chunk)
    except Exception:
        return ""
    return h.hexdigest()


def scan_files(root: Path) -> tuple[list[dict], dict]:
    files = []
    totals = {"total_files": 0, "total_size": 0, "by_ext": {}}
    for p in root.rglob("*"):
        try:
            if p.is_dir():
                # skip common large dirs
                if p.name in EXCLUDE_DIRS:
                    # skip walking into dir
                    continue
                # else continue
            elif p.is_file():
                rel = p.relative_to(root)
                # skip files in excluded paths
                if any(part in EXCLUDE_DIRS for part in rel.parts):
                    continue
                # skip generated manifest itself
                if str(rel).endswith("ai-fetchable-manifest.json"):
                    continue
                size = p.stat().st_size
                totals["total_files"] += 1
                totals["total_size"] += size
                ext = p.suffix or "NOEXT"
                totals["by_ext"].setdefault(ext, 0)
                totals["by_ext"][ext] += 1
                files.append(
                    {
                        "path": str(rel).replace("\\", "/"),
                        "size": size,
                        "sha1_head": safe_hash(p),
                        "is_text": ext.lower() in TEXT_EXTS,
                    }
                )
        except PermissionError:
            continue
        except Exception:
            continue
    return files, totals


def build_manifest(root: Path, out: Path) -> dict:
    now = datetime.utcnow().isoformat()
    git = repo_git_info(root)
    files, totals = scan_files(root)
    manifest = {
        "$schema": "../schemas/ai-fetchable-manifest.json",
        "repository": {
            "remote_url": git.get("remote_url"),
            "owner_repo": git.get("owner_repo"),
            "branch": git.get("branch"),
            "commit_hash": git.get("commit_hash"),
            "is_dirty": git.get("is_dirty"),
            "generated_at": now,
            "valid_until": (datetime.utcnow() + timedelta(days=7)).isoformat(),
        },
        "summary": {
            "total_files": totals["total_files"],
            "files_in_manifest": len(files),
            "files_truncated": False,
            "total_size": totals["total_size"],
            "categories": {},
            "languages": {k: v for k, v in totals["by_ext"].items()},
        },
        "files": files[:10000],
        "notes": "This is a lightweight manifest generated by scripts/tools/generate_repo_urls.py",
    }
    return manifest


def main(argv=None):
    parser = argparse.ArgumentParser(
        description="Generate ai-fetchable-manifest.json for repo"
    )
    parser.add_argument(
        "-o",
        "--output",
        default="ai-fetchable-manifest.json",
        help="Output file path (relative to repo root)",
    )
    parser.add_argument(
        "-r", "--root", default=".", help="Repository root path (default: .)"
    )
    args = parser.parse_args(argv)

    repo_root = Path(args.root).resolve()
    out_path = (repo_root / args.output).resolve()

    print(f"Scanning repo: {repo_root}")
    manifest = build_manifest(repo_root, out_path)

    try:
        out_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
        print(f"Wrote manifest: {out_path} (files={len(manifest['files'])})")
    except Exception as e:
        print("Failed to write manifest:", e, file=sys.stderr)
        sys.exit(2)


if __name__ == "__main__":
    main()
