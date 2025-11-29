"""
Generate a rich ai-fetchable-manifest.json for the repository.
This improved generator includes file-level metadata to help AI agents locate and fetch files:
- file path, size, partial+full SHA1, MIME type
- language (best-effort), category (heuristic), line counts and short snippet for text files
- raw fetchable URL (raw.githubusercontent.com) when git remote + commit/branch available

Usage:
  python scripts/tools/generate_repo_manifest.py -o ../ai-fetchable-manifest.json

By default it writes to <workspace root>/ai-fetchable-manifest.json
"""

from __future__ import annotations

import argparse
import json
import mimetypes
import subprocess
import sys
from datetime import datetime, timedelta
from hashlib import sha1
from pathlib import Path
from typing import Optional, TypedDict


class RepoGitInfo(TypedDict):
    remote_url: Optional[str]
    owner_repo: Optional[str]
    branch: Optional[str]
    commit_hash: Optional[str]
    is_dirty: Optional[bool]


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
    ".py": "python",
    ".ps1": "powershell",
    ".psm1": "powershell",
    ".psd1": "powershell",
    ".cs": "csharp",
    ".csproj": "xml",
    ".sln": "plaintext",
    ".js": "javascript",
    ".ts": "typescript",
    ".java": "java",
    ".html": "html",
    ".htm": "html",
    ".css": "css",
    ".md": "markdown",
    ".markdown": "markdown",
    ".json": "json",
    ".yml": "yaml",
    ".yaml": "yaml",
    ".xml": "xml",
    ".txt": "text",
    ".sh": "shell",
    ".sql": "sql",
    ".toml": "toml",
    ".csv": "csv",
}

# Heuristic hints (substring, category) for quick categorization of files and dirs.
CATEGORY_HINTS = [
    ("test", "tests"),
    ("tests", "tests"),
    ("doc", "documentation"),
    ("docs", "documentation"),
    ("example", "examples"),
    ("examples", "examples"),
    ("sample", "examples"),
    ("script", "scripts"),
    ("build", "build"),
    ("ci", "ci"),
    ("config", "configuration"),
    ("configs", "configuration"),
    ("bin", "build"),
    ("lib", "library"),
    ("src", "source_code"),
    ("source", "source_code"),
    ("resources", "assets"),
    ("assets", "assets"),
]


def repo_git_info(root: Path) -> RepoGitInfo:
    data: RepoGitInfo = {
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
                # trunk-ignore(bandit/B603)
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
        if remote:
            cleaned = remote
            # normalize common patterns like git@github.com:owner/repo.git to https://raw.githubusercontent.com/owner/repo/
            if cleaned.startswith("git@"):
                cleaned = cleaned.replace(":", "/").replace("git@", "https://")
            if cleaned.endswith(".git"):
                cleaned = cleaned[:-4]
            try:
                owner_repo = "/".join(cleaned.split("/")[-2:])
                data["owner_repo"] = owner_repo
            except Exception:
                data["owner_repo"] = None
        data["branch"] = branch
        data["commit_hash"] = commit
        data["is_dirty"] = bool(dirty)

    return data


def safe_hash(path: Path, max_bytes=8192) -> str:
    """Return a short SHA1 hash of the first max_bytes of a file to give a stable fingerprint."""
    # Use SHA1 for non-security fingerprinting purposes and mark it explicitly
    # to avoid security lint warnings (usedforsecurity=False).
    h = sha1(usedforsecurity=False)
    try:
        with path.open("rb") as f:
            chunk = f.read(max_bytes)
            if chunk:
                h.update(chunk)
            else:
                return ""
    except Exception:
        return ""
    return h.hexdigest()


def full_sha1(path: Path) -> str:
    # Use SHA1 for non-security full-file fingerprinting and mark explicitly
    # usedforsecurity=False to suppress security warnings where appropriate.
    h = sha1(usedforsecurity=False)
    try:
        with path.open("rb") as fh:
            while True:
                chunk = fh.read(8192)
                if not chunk:
                    break
                h.update(chunk)
    except Exception:
        return ""
    return h.hexdigest()


def make_fetch_url(
    owner_repo: str | None, commit: str | None, branch: str | None, relpath: str
) -> str | None:
    if not owner_repo:
        return None
    ref = commit or branch or "main"
    # ensure forward slashes and no leading slashes
    path = relpath.replace("\\", "/").lstrip("/")
    return f"https://raw.githubusercontent.com/{owner_repo}/{ref}/{path}"


def scan_files(
    root: Path, include_snippets=True, compute_full_hash=True, max_files=10000
) -> tuple[list[dict], dict]:
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

                is_text = ext.lower() in TEXT_EXTS
                mime, _ = mimetypes.guess_type(str(p))
                language = LANG_BY_EXT.get(ext.lower())

                # read a short snippet for text files
                snippet = None
                lines = None
                sha1_full = ""
                try:
                    if is_text and include_snippets:
                        with p.open("rb") as fh:
                            preview = fh.read(4096)
                        try:
                            snippet = preview.decode("utf-8", errors="replace")[:2048]
                            lines = snippet.count("\n") + 1
                        except Exception:
                            snippet = None
                            lines = None
                    if compute_full_hash:
                        sha1_full = full_sha1(p)
                except Exception:
                    snippet = None
                    lines = None
                    sha1_full = ""

                # category heuristic
                cat = "unknown"
                lower_parts = [p_.lower() for p_ in rel.parts]
                for hint, cname in CATEGORY_HINTS:
                    if (
                        any(hint in part for part in lower_parts)
                        or hint.lower() in str(rel).lower()
                        or ext.lower().strip(".") == hint
                    ):
                        cat = cname
                        break
                if cat == "unknown":
                    if ext.lower() in {".md", ".markdown"}:
                        cat = "documentation"
                    elif ext.lower() in {".json", ".yaml", ".yml", ".xml"}:
                        cat = "configuration"
                    elif ext.lower() in {
                        ".cs",
                        ".py",
                        ".sh",
                        ".ps1",
                        ".java",
                        ".ts",
                        ".js",
                    }:
                        cat = "source_code"

                files.append(
                    {
                        "path": str(rel).replace("\\", "/"),
                        "size": size,
                        "sha1_head": safe_hash(p),
                        "sha1": sha1_full,
                        "is_text": bool(is_text),
                        "mime_type": mime,
                        "language": language,
                        "lines": lines,
                        "snippet": snippet,
                        "category": cat,
                    }
                )

                if len(files) >= max_files:
                    break
        except PermissionError:
            continue
        except Exception:
            continue
    return files, totals


def build_manifest(
    root: Path,
    out: Path,
    include_snippets=True,
    compute_full_hash=True,
    max_files=10000,
) -> dict:
    now = datetime.utcnow().isoformat()
    git = repo_git_info(root)
    files, totals = scan_files(
        root,
        include_snippets=include_snippets,
        compute_full_hash=compute_full_hash,
        max_files=max_files,
    )

    # enhance files with fetch_url when possible
    for f in files:
        f["fetch_url"] = make_fetch_url(
            git.get("owner_repo"), git.get("commit_hash"), git.get("branch"), f["path"]
        )

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
            "files_truncated": len(files) >= max_files,
            "total_size": totals["total_size"],
            "categories": {},
            "languages": {k: v for k, v in totals["by_ext"].items()},
        },
        "files": files,
        "notes": "This manifest contains fetchable URLs when git remote + commit/branch are present. Use for AI agents to fetch repository files quickly.",
    }
    return manifest


def main(argv=None):
    parser = argparse.ArgumentParser(
        description="Generate rich ai-fetchable-manifest.json for repo"
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
    parser.add_argument(
        "--no-snippets",
        dest="snippets",
        action="store_false",
        help="Do not include text snippets in manifest",
    )
    parser.add_argument(
        "--no-full-hash",
        dest="fullhash",
        action="store_false",
        help="Do not compute full SHA1 hashes",
    )
    parser.add_argument(
        "--max-files",
        dest="max_files",
        type=int,
        default=10000,
        help="Maximum files to include in manifest",
    )
    args = parser.parse_args(argv)

    repo_root = Path(args.root).resolve()
    out_path = (repo_root / args.output).resolve()

    print(f"Scanning repo: {repo_root}")
    manifest = build_manifest(
        repo_root,
        out_path,
        include_snippets=args.snippets,
        compute_full_hash=args.fullhash,
        max_files=args.max_files,
    )

    try:
        out_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
        print(f"Wrote manifest: {out_path} (files={len(manifest['files'])})")
    except Exception as e:
        print("Failed to write manifest:", e, file=sys.stderr)
        sys.exit(2)


if __name__ == "__main__":
    main()
