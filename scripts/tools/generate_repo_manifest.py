"""
Wiley Widget Manifest Generator v3.14 (π Edition)
──────────────────────────────────────────────────
Generates a rich, AI-fetchable repository manifest with:
  • raw.githubusercontent.com URLs
  • full SHA1 + head fingerprint
  • language detection, MIME, snippets, line counts
  • smart categorization (MVVM-aware!)
  • git metadata + clean/dirty status
  • valid_until + schema reference

Now with 3.14% more precision, 100% more π.

Usage:
  python scripts/tools/generate_repo_manifest.py -o ai-fetchable-manifest.json
"""

from __future__ import annotations

import argparse
import json
import mimetypes
import subprocess
from datetime import datetime, timedelta
from hashlib import sha1
from pathlib import Path
from typing import Any

# typing.Optional not required in this file

# ──────────────────────────────────────────────────────────────
# Configuration (3.14 Edition)
# ──────────────────────────────────────────────────────────────
EXCLUDE_DIRS = {
    ".git",
    "node_modules",
    "bin",
    "obj",
    ".vs",
    ".venv",
    "__pycache__",
    "TestResults",
    "logs",
    "xaml-logs",
    "artifacts",
    "packages",
}

TEXT_EXTS = {
    ".cs",
    ".xaml",
    ".json",
    ".yaml",
    ".yml",
    ".xml",
    ".md",
    ".txt",
    ".py",
    ".ps1",
    ".psm1",
    ".psd1",
    ".sql",
    ".sh",
    ".toml",
    ".html",
    ".htm",
    ".css",
    ".js",
    ".ts",
    ".csproj",
    ".sln",
    ".config",
    ".props",
}

LANG_BY_EXT = {
    ".cs": "C#",
    ".xaml": "XAML",
    ".csproj": "C# Project",
    ".sln": "Solution",
    ".py": "Python",
    ".ps1": "PowerShell",
    ".psm1": "PowerShell",
    ".psd1": "PowerShell",
    ".md": "Markdown",
    ".json": "JSON",
    ".yaml": "YAML",
    ".yml": "YAML",
    ".xml": "XML",
    ".html": "HTML",
    ".htm": "HTML",
    ".sql": "SQL",
    ".sh": "Shell",
    ".toml": "TOML",
    ".config": "XML",
    ".props": "MSBuild Properties",
}

# MVVM-aware category hints (Wiley Widget special sauce)
CATEGORY_HINTS = [
    ("viewmodels", "viewmodels"),
    ("viewmodel", "viewmodels"),
    ("views", "views"),
    ("view", "views"),
    ("models", "models"),
    ("model", "models"),
    ("services", "services"),
    ("service", "services"),
    ("src", "source_code"),
    ("source", "source_code"),
    ("tests", "test"),
    ("test", "test"),
    ("docs", "documentation"),
    ("doc", "documentation"),
    ("scripts", "automation"),
    ("tools", "automation"),
    ("assets", "assets"),
    ("resources", "assets"),
    ("wwwroot", "web"),
    ("privacy.html", "legal"),
    ("terms.html", "legal"),
]

MANIFEST_SCHEMA = (
    "https://raw.githubusercontent.com/Bigessfour/Wiley-Widget"
    "/main/schemas/ai-fetchable-manifest.json"
)


# ──────────────────────────────────────────────────────────────
# Core Functions
# ──────────────────────────────────────────────────────────────
def repo_git_info(root: Path) -> dict[str, Any]:
    info: dict[str, Any] = {
        "remote_url": None,
        "owner_repo": None,
        "branch": None,
        "commit_hash": None,
        "is_dirty": None,
    }

    if not (root / ".git").exists():
        return info

    def git(*args):
        try:
            return (
                subprocess.check_output(
                    ["git", "-C", str(root)] + list(args), stderr=subprocess.DEVNULL
                )
                .decode("utf-8", errors="replace")
                .strip()
            )
        except Exception:
            return None

    remote = git("config", "--get", "remote.origin.url")
    info["remote_url"] = remote
    if remote:
        cleaned = remote
        if cleaned.startswith("git@"):
            cleaned = "https://" + cleaned.replace(":", "/")
            cleaned = cleaned.replace("git@", "")
        if cleaned.endswith(".git"):
            cleaned = cleaned[:-4]
        try:
            info["owner_repo"] = "/".join(cleaned.split("/")[-2:])
        except Exception:
            pass

    info["branch"] = git("rev-parse", "--abbrev-ref", "HEAD") or "HEAD"
    info["commit_hash"] = git("rev-parse", "HEAD")
    info["is_dirty"] = bool(git("status", "--porcelain"))

    return info


def fingerprint_head(path: Path, bytes: int = 8192) -> str:
    h = sha1(usedforsecurity=False)
    try:
        with path.open("rb") as f:
            h.update(f.read(bytes))
    except Exception:
        return ""
    return h.hexdigest()


def fingerprint_full(path: Path) -> str:
    h = sha1(usedforsecurity=False)
    try:
        with path.open("rb") as f:
            for chunk in iter(lambda: f.read(65536), b""):
                h.update(chunk)
    except Exception:
        return ""
    return h.hexdigest()


def make_fetch_url(
    owner_repo: str | None,
    commit: str | None,
    branch: str | None,
    path: str,
) -> str | None:
    if not owner_repo:
        return None
    ref = commit or branch or "main"
    clean_path = path.replace("\\", "/").lstrip("/")
    return f"https://raw.githubusercontent.com/{owner_repo}/{ref}/{clean_path}"


def detect_category(rel_path: str) -> str:
    parts = [p.lower() for p in Path(rel_path).parts]
    path_str = rel_path.lower()

    for hint, cat in CATEGORY_HINTS:
        if any(hint in p for p in parts) or hint in path_str:
            return cat

    # Fallback heuristics
    if rel_path.lower().endswith((".md", ".markdown")):
        return "documentation"
    if Path(rel_path).suffix.lower() in {".json", ".yaml", ".yml", ".xml", ".config"}:
        return "configuration"
    if Path(rel_path).suffix.lower() in {".cs", ".py", ".ps1", ".sh"}:
        return "source_code"
    if "wwwroot" in parts:
        return "web"
    return "unknown"


def scan_repository(
    root: Path,
    max_files: int = 15_000,
) -> tuple[list[dict], dict]:
    files = []
    totals = {"total_files": 0, "total_size": 0, "by_ext": {}}

    for p in root.rglob("*"):
        if len(files) >= max_files:
            break
        if p.is_dir():
            if p.name in EXCLUDE_DIRS:
                continue
            continue

        rel = p.relative_to(root)
        if any(part in EXCLUDE_DIRS for part in rel.parts):
            continue
        if str(rel).endswith("ai-fetchable-manifest.json"):
            continue

        try:
            stat = p.stat()
            size = stat.st_size
            ext = p.suffix.lower() or "NOEXT"
            totals["total_files"] += 1
            totals["total_size"] += size
            totals["by_ext"][ext] = totals["by_ext"].get(ext, 0) + 1

            is_text = ext in TEXT_EXTS
            mime, _ = mimetypes.guess_type(str(p))
            language = LANG_BY_EXT.get(ext)

            snippet = lines = None
            if is_text:
                try:
                    preview = p.read_bytes()[:4096]
                    text = preview.decode("utf-8", errors="replace")
                    snippet = text[:2048]
                    lines = text.count("\n") + (0 if text.endswith("\n") else 1)
                except Exception:
                    # ignore non-decodable preview
                    pass

            entry = {
                "path": str(rel).replace("\\", "/"),
                "size": size,
                "last_modified": datetime.fromtimestamp(stat.st_mtime).isoformat(),
                "extension": ext,
                "sha1_head": fingerprint_head(p),
                "sha1": fingerprint_full(p) if size < 5_000_000 else "",
                "is_text": is_text,
                "mime_type": mime or "application/octet-stream",
                "language": language,
                "lines": lines,
                "snippet": snippet,
                "category": detect_category(str(rel)),
            }

            git_info = repo_git_info(root)
            entry["fetch_url"] = make_fetch_url(
                git_info.get("owner_repo"),
                git_info.get("commit_hash"),
                git_info.get("branch"),
                entry["path"],
            )

            files.append(entry)

        except (PermissionError, OSError):
            continue
        except Exception:
            # ignore unexpected single-file errors and continue
            continue

    return files, totals


# ──────────────────────────────────────────────────────────────
# Manifest Builder v3.14
# ──────────────────────────────────────────────────────────────
def build_manifest_v314(root: Path, output_path: Path) -> dict:
    now = datetime.utcnow()
    git = repo_git_info(root)
    files, totals = scan_repository(root)

    manifest = {
        "$schema": MANIFEST_SCHEMA,
        "generator": "Wiley Widget Manifest Generator v3.14 (π Edition)",
        "generated_at": now.isoformat() + "Z",
        "valid_until": (now + timedelta(days=7)).isoformat() + "Z",
        "repository": {
            "remote_url": git["remote_url"],
            "owner_repo": git["owner_repo"],
            "branch": git["branch"],
            "commit_hash": git["commit_hash"],
            "is_dirty": git["is_dirty"],
        },
        "summary": {
            "total_files_scanned": totals["total_files"],
            "files_in_manifest": len(files),
            "total_size_bytes": totals["total_size"],
            "languages": totals["by_ext"],
        },
        "files": files,
        "note": (
            "Upgraded to 3.14 — now with more precision,"
            " better MVVM detection,"
            " and a hint of irrational excellence."
        ),
    }

    output_path.write_text(
        json.dumps(manifest, indent=2, ensure_ascii=False), encoding="utf-8"
    )
    print(f"Manifest v3.14 generated: {output_path}")
    commit_short = (git.get("commit_hash") or "")[:10]
    print(
        "   Files: {} | Size: {:,} bytes | Commit: {}".format(
            len(files), totals["total_size"], commit_short
        )
    )

    return manifest


# ──────────────────────────────────────────────────────────────
# CLI
# ──────────────────────────────────────────────────────────────
def main():
    parser = argparse.ArgumentParser(
        description=(
            "Wiley Widget Manifest Generator v3.14 " "— AI-ready repo manifest"
        )
    )
    parser.add_argument(
        "-o",
        "--output",
        default="ai-fetchable-manifest.json",
        help="Output path",
    )
    parser.add_argument(
        "-r",
        "--root",
        default=".",
        help="Repository root",
    )
    args = parser.parse_args()

    root = Path(args.root).resolve()
    out = (root / args.output).resolve()

    build_manifest_v314(root, out)


if __name__ == "__main__":
    main()
