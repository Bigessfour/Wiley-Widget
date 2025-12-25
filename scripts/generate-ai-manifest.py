#!/usr/bin/env python3
"""
AI Fetchable Manifest Generator

Generates ai-fetchable-manifest.json so AI agents can inspect the repository.
Uses `.ai-manifest-config.json` for include/exclude rules, but falls back to
sensible defaults when that file is missing.

Changes in this version:
- GitPython is required for reliable git metadata (no subprocess fallback).
- If `.ai-manifest-config.json` is missing, safe defaults are used and the
  manifest will still be generated.
- Improved detached HEAD handling and untracked-file detection for is_dirty.
- Added `line_count`, `encoding`, and `is_binary` to file metadata.
- Added `mime_type` to file metadata.
- Better file categorization: source_code, test, documentation,
  configuration, automation, and assets.
- Expanded language detection map.
- WinUI-aware architecture detection: views include Page/Window patterns and
  related `.xaml` files are linked.
- Approximate `total_lines_of_code` metric is based on source_code files.

Requirements:
- Python 3.9+
- git
- gitpython (pip install gitpython)
"""

import hashlib
import json
import mimetypes
import re
import sys
from datetime import datetime, timedelta
from pathlib import Path
from typing import Any, Dict, List

from git import Repo


class AIManifestGenerator:
    """Generates AI fetchable manifest for repository visibility."""

    def __init__(self, repo_root: Path):
        self.repo_root = repo_root.resolve()
        self.config = self._load_config()
        self.exclude_patterns = self._compile_patterns()
        self.focus_extensions = set(self.config.get("include_only_extensions", []))

        # Max files logic: support 'max_files' and legacy 'max_files_in_manifest'.
        # If neither key is present, default to a safe limit (800). An explicit
        # null/None value in the config means unlimited.
        if "max_files" in self.config:
            self.max_files = self.config.get("max_files")
        elif "max_files_in_manifest" in self.config:
            self.max_files = self.config.get("max_files_in_manifest")
        else:
            self.max_files = 800

        if self.max_files is not None:
            try:
                self.max_files = int(self.max_files)
            except Exception:
                self.max_files = None

        self.emit_full_tree = bool(self.config.get("emit_full_tree", True))
        self.tree_max_depth = int(self.config.get("tree_max_depth", 4))
        self.max_tree_entries_per_dir = int(self.config.get("max_tree_entries_per_dir", 50))

        # Populated during scanning
        self._included_paths: List[Path] = []
        self._files_truncated = False

    def _load_config(self) -> Dict[str, Any]:
        """Load configuration — use defaults if the config file is missing."""
        config_path = self.repo_root / ".ai-manifest-config.json"
        default_config: Dict[str, Any] = {
            "exclude_patterns": [],
            "include_only_extensions": [],
            "focus_mode": False,
        }

        if not config_path.exists():
            msg = f"Warning: {config_path} not found; using defaults."
            print(msg, file=sys.stderr)
            return default_config

        try:
            with open(config_path, "r", encoding="utf-8") as f:
                return json.load(f)
        except Exception as e:
            msg = f"Error loading config: {e}. Using defaults."
            print(msg, file=sys.stderr)
            return default_config

    def _compile_patterns(self) -> List[re.Pattern[str]]:
        patterns = []
        for pattern in self.config.get("exclude_patterns", []):
            try:
                # Compile with IGNORECASE so patterns match consistently across platforms
                patterns.append(re.compile(pattern, re.IGNORECASE))
            except re.error as e:
                msg = f"Warning: Invalid regex '{pattern}': {e}"
                print(msg, file=sys.stderr)
        return patterns

    def _should_include_file(self, file_path: Path) -> bool:
        relative_path = file_path.relative_to(self.repo_root)
        # Normalize to POSIX-style path for consistent matching on Windows and Unix
        path_str = relative_path.as_posix().lower()
        parts_lower = [p.lower() for p in relative_path.parts]

        # Exclude any paths under APPDATA (e.g. '%APPDATA%')
        if "appdata" in path_str:
            return False

        # Force-include anything under 'tests' or containing 'test' — BUT when focus_mode
        # is enabled, only include tests whose file extensions are within the focus set.
        if "tests" in parts_lower or "test" in path_str:
            if self.config.get("focus_mode", False) and self.focus_extensions:
                if file_path.suffix.lower() in self.focus_extensions:
                    return True
                return False
            return True

        # Exclude by pattern
        for pattern in self.exclude_patterns:
            if pattern.search(path_str):
                return False

        # Focus mode: only specific extensions
        if self.config.get("focus_mode", False):
            if self.focus_extensions:
                if file_path.suffix.lower() not in self.focus_extensions:
                    return False

        return True

    def _get_repo_info(self) -> Dict[str, Any]:
        """Get repository information using GitPython (required)."""
        repo = Repo(self.repo_root, search_parent_directories=True)

        remote_url = ""
        if repo.remotes:
            if "origin" in repo.remotes:
                remote_url = repo.remotes.origin.url
            else:
                remote_url = next(iter(repo.remotes)).url

        branch = "HEAD"
        if not repo.head.is_detached:
            branch = repo.active_branch.name
        else:
            branch = f"detached ({repo.head.commit.hexsha[:8]})"

        commit_hash = repo.head.commit.hexsha
        is_dirty = repo.is_dirty(untracked_files=True)

        owner_repo = self._extract_owner_repo(remote_url)

        generated_at = datetime.now().isoformat()
        valid_until = (datetime.now() + timedelta(days=7)).isoformat()

        return {
            "remote_url": remote_url,
            "owner_repo": owner_repo,
            "branch": branch,
            "commit_hash": commit_hash,
            "is_dirty": is_dirty,
            "generated_at": generated_at,
            "valid_until": valid_until,
        }

    def _extract_owner_repo(self, remote_url: str) -> str:
        patterns = [
            r"github\.com[/:]([^/]+/[^/\.]+)",
            r"gitlab\.com[/:]([^/]+/[^/\.]+)",
            r"bitbucket\.org[/:]([^/]+/[^/\.]+)",
        ]
        for pattern in patterns:
            match = re.search(pattern, remote_url, re.IGNORECASE)
            if match:
                return match.group(1).rstrip(".git")
        return "unknown/unknown"

    def _get_file_hash_and_info(self, file_path: Path) -> Dict[str, Any]:
        with open(file_path, "rb") as f:
            content = f.read()
            sha256 = hashlib.sha256(content).hexdigest()

        try:
            text = content.decode("utf-8")
            line_count = text.count("\n") + (1 if text else 0)
            encoding = "utf-8"
            is_binary = False
        except UnicodeDecodeError:
            line_count = 0
            encoding = "binary"
            is_binary = True

        mime_type = (
            mimetypes.guess_type(str(file_path))[0] or "application/octet-stream"
        )

        return {
            "sha256": sha256,
            "line_count": line_count,
            "encoding": encoding,
            "is_binary": is_binary,
            "mime_type": mime_type,
        }

    def _detect_language(self, file_path: Path) -> str:
        ext = file_path.suffix.lower()
        language_map = {
            ".cs": "C#",
            ".xaml": "XAML",
            ".py": "Python",
            ".js": "JavaScript",
            ".ts": "TypeScript",
            ".tsx": "TypeScript JSX",
            ".json": "JSON",
            ".yaml": "YAML",
            ".yml": "YAML",
            ".md": "Markdown",
            ".html": "HTML",
            ".css": "CSS",
            ".xml": "XML",
            ".ps1": "PowerShell",
            ".sh": "Shell",
            ".sql": "SQL",
            ".csproj": "MSBuild Project",
            ".sln": "Visual Studio Solution",
            ".txt": "Text",
            ".csv": "CSV",
            ".toml": "TOML",
            ".dockerfile": "Dockerfile",
            ".gitignore": "Git Ignore",
        }
        default_lang = ext[1:].upper() + " File" if ext else "Unknown"
        return language_map.get(ext, default_lang)

    def _determine_category(self, file_path: Path, relative_path: Path) -> str:
        path_str = str(relative_path).lower()
        suffix = file_path.suffix.lower()

        if "test" in path_str or "tests" in relative_path.parts:
            return "test"
        if suffix in {".cs", ".xaml", ".ts", ".tsx", ".js", ".py"}:
            return "source_code"
        if suffix in {".md", ".markdown"}:
            return "documentation"
        if suffix in {
            ".json",
            ".yaml",
            ".yml",
            ".xml",
            ".config",
            ".csproj",
            ".props",
            ".targets",
        }:
            return "configuration"
        if suffix in {".ps1", ".py", ".sh", ".bat", ".cmd"}:
            return "automation"
        if suffix in {
            ".png",
            ".jpg",
            ".jpeg",
            ".gif",
            ".svg",
            ".ico",
        }:
            return "assets"
        return "unknown"

    def _scan_files(self) -> List[Dict[str, Any]]:
        files = []
        total_size = 0
        categories: Dict[str, int] = {}
        languages: Dict[str, int] = {}
        total_loc = 0

        repo_info = self._get_repo_info()

        # Enforce max files and focused scanning for performance and predictable outputs
        self._included_paths = []
        self._files_truncated = False
        scanned = 0

        # Build candidate list: if focus_mode is enabled, only search files with
        # allowed extensions which is much faster for large repositories.
        candidates: List[Path] = []
        if self.config.get("focus_mode", False) and self.focus_extensions:
            for ext in sorted(self.focus_extensions):
                for p in self.repo_root.rglob(f"*{ext}"):
                    if p.is_file():
                        candidates.append(p)
        else:
            for p in self.repo_root.rglob("*"):
                candidates.append(p)

        # Deduplicate and sort for deterministic ordering
        unique_candidates = sorted({p for p in candidates}, key=lambda p: str(p).lower())

        for file_path in unique_candidates:
            scanned += 1
            if scanned % 200 == 0:
                # Progress update to stderr so long runs show activity
                print(
                    f"Scanned {scanned} filesystem entries... (included {len(files)} files so far)",
                    file=sys.stderr,
                    flush=True,
                )

            if not file_path.is_file():
                continue

            # Skip by _should_include_file which handles path-based excludes and test inclusion rules
            if not self._should_include_file(file_path):
                continue

            # If focus_mode is on, ensure the file extension is in the focus set
            if self.config.get("focus_mode", False) and self.focus_extensions:
                if file_path.suffix.lower() not in self.focus_extensions:
                    continue

            # Enforce max_files limit if configured (None = unlimited)
            if self.max_files is not None and len(files) >= self.max_files:
                self._files_truncated = True
                print(
                    f"Reached max_files limit ({self.max_files}); stopping scan. Set 'max_files' or 'max_files_in_manifest' in config to adjust.",
                    file=sys.stderr,
                    flush=True,
                )
                break

            relative_path = file_path.relative_to(self.repo_root)
            stat = file_path.stat()
            size = stat.st_size
            last_modified = datetime.fromtimestamp(stat.st_mtime).isoformat()

            file_info = self._get_file_hash_and_info(file_path)
            language = self._detect_language(file_path)
            category = self._determine_category(file_path, relative_path)

            categories[category] = categories.get(category, 0) + 1
            languages[language] = languages.get(language, 0) + 1
            total_size += size
            if category == "source_code" and not file_info["is_binary"]:
                total_loc += file_info["line_count"]

            owner_repo = repo_info["owner_repo"]
            branch = repo_info["branch"]
            blob_url = (
                f"https://github.com/{owner_repo}/blob/{branch}/" f"{relative_path}"
            )
            raw_url = (
                f"https://raw.githubusercontent.com/{owner_repo}/"
                f"{branch}/{relative_path}"
            )

            file_entry = {
                "metadata": {
                    "path": str(relative_path),
                    "exists": True,
                    "size": size,
                    "last_modified": last_modified,
                    "language": language,
                    "line_count": file_info["line_count"],
                    "encoding": file_info["encoding"],
                    "is_binary": file_info["is_binary"],
                    "mime_type": file_info["mime_type"],
                },
                "urls": {"blob_url": blob_url, "raw_url": raw_url},
                "context": {
                    "category": category,
                    "tracked": True,
                    "extension": file_path.suffix,
                    "sha256": file_info["sha256"],
                },
            }
            files.append(file_entry)
            self._included_paths.append(file_path)

        self._total_files = len(files)
        self._total_size = total_size
        self._categories = categories
        self._languages = languages
        self._total_loc = total_loc

        return files

    def _generate_summary(self) -> Dict[str, Any]:
        return {
            "total_files": self._total_files,
            "files_in_manifest": self._total_files,
            "files_truncated": bool(getattr(self, "_files_truncated", False)),
            "total_size": self._total_size,
            "categories": self._categories,
            "languages": self._languages,
        }

    def _detect_architecture(self) -> Dict[str, List[Dict[str, Any]]]:
        arch: Dict[str, List[Dict[str, Any]]] = {
            "views": [],
            "viewmodels": [],
            "models": [],
            "services": [],
            "repositories": [],
            "converters": [],
            "behaviors": [],
            "modules": [],
        }

        patterns = {
            "viewmodels": re.compile(r"class\s+(\w*ViewModel)\b"),
            "views": re.compile(
                r"class\s+("
                r"\w*(?:View|Page|Window|Control|UserControl|Form|Panel)"
                r")\b"
            ),
            "models": re.compile(r"class\s+(\w*(?:Model|Dto|Entity))\b"),
            "services": re.compile(
                r"interface\s+(I\w*Service)\b|class\s+(\w*Service)\b"
            ),
            "repositories": re.compile(
                r"interface\s+(I\w*Repository)\b|class\s+(\w*Repository)\b"
            ),
            "converters": re.compile(
                r"interface\s+(I\w*Converter)\b|class\s+(\w*Converter)\b"
            ),
            "behaviors": re.compile(
                r"interface\s+(I\w*Behavior)\b|class\s+(\w*Behavior)\b"
            ),
            "modules": re.compile(r"class\s+(\w*Module)\b"),
        }

        for file_path in getattr(self, "_included_paths", []):
            if file_path.suffix.lower() != ".cs":
                continue
            try:
                text = file_path.read_text(encoding="utf-8", errors="ignore")
            except Exception:
                continue

            relative = str(file_path.relative_to(self.repo_root))

            for key, pattern in patterns.items():
                for match in pattern.finditer(text):
                    name = match.group(1) or match.group(2)
                    if not name:
                        continue
                    entry: Dict[str, Any] = {
                        "name": name,
                        "path": relative,
                    }
                    if key == "views":
                        xaml_path = file_path.with_name(file_path.stem + ".xaml")
                        related: List[str] = []
                        if xaml_path.exists():
                            xaml_rel = str(xaml_path.relative_to(self.repo_root))
                            related.append(xaml_rel)
                        entry["related_files"] = related
                    arch[key].append(entry)

        return arch

    def _generate_folder_tree(self) -> Dict[str, Any]:
        def build_tree(path: Path) -> Dict[str, Any]:
            if path.is_file():
                return {
                    "name": path.name,
                    "type": "file",
                    "path": str(path.relative_to(self.repo_root)),
                }
            children = []
            try:
                for child in sorted(path.iterdir()):
                    if child.is_file() and self._should_include_file(child):
                        children.append(build_tree(child))
                    elif child.is_dir():
                        subtree = build_tree(child)
                        if subtree["children"] or self._should_include_file(child):
                            children.append(subtree)
            except PermissionError:
                pass
            return {
                "name": path.name,
                "type": "directory",
                "path": str(path.relative_to(self.repo_root)),
                "children": children,
            }

        return build_tree(self.repo_root)

    def generate_manifest(self) -> Dict[str, Any]:
        repo_info = self._get_repo_info()
        files = self._scan_files()
        summary = self._generate_summary()
        arch = self._detect_architecture()

        manifest = {
            "$schema": (
                "https://raw.githubusercontent.com/Bigessfour/Wiley-Widget"
                "/main/schemas/ai-manifest-schema.json"
            ),
            "repository": repo_info,
            "license": {"type": "Unknown", "file": None, "detected": False},
            "summary": summary,
            "metrics": {
                "total_lines_of_code": self._total_loc,
                "total_code_lines": 0,
                "total_comment_lines": 0,
                "total_blank_lines": 0,
                "average_complexity": 0.0,
                "test_coverage_percent": 0.0,
                "test_count": self._categories.get("test", 0),
                "project_metrics": {},
            },
            "security": {
                "vulnerable_packages": [],
                "outdated_packages": [],
                "secrets_detected": False,
                "last_security_scan": datetime.now().isoformat(),
                "note": "Security scanning not implemented in this generator",
            },
            "quality": {
                "build_status": "unknown",
                "analyzers_enabled": True,
                "documentation_coverage": 0.0,
                "technical_debt_minutes": 0,
            },
            "architecture": {
                "pattern": "MVVM",
                **{k: arch.get(k, []) for k in arch},
                "counts": {k: len(arch.get(k, [])) for k in arch},
            },
            "dependency_graph": {
                "projects": {},
                "nuget_packages": {},
                "top_dependencies": [],
            },
            "folder_tree": self._generate_folder_tree() if self.config.get("emit_full_tree", True) else {},
            "search_index": [],
            "files": files,
        }

        return manifest

    def save_manifest(self, output_path: Path | None = None) -> None:
        if output_path is None:
            output_path = self.repo_root / "ai-fetchable-manifest.json"

        print(
            f"Starting manifest generation for {self.repo_root} (focus_mode={self.config.get('focus_mode', False)}, max_files={self.max_files}, emit_full_tree={self.config.get('emit_full_tree', True)})",
            file=sys.stderr,
            flush=True,
        )
        manifest = self.generate_manifest()

        with open(output_path, "w", encoding="utf-8") as f:
            json.dump(manifest, f, indent=2, ensure_ascii=False)

        print(f"Manifest generated successfully: {output_path} ({self._total_files} files)")


def main():
    repo_root = Path(__file__).parent.parent
    try:
        generator = AIManifestGenerator(repo_root)
        generator.save_manifest()
    except Exception as e:
        import traceback

        traceback.print_exc()
        print(f"Error: {e}", file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()
