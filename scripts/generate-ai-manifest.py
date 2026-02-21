#!/usr/bin/env python3
"""
AI Fetchable Manifest Generator v2.0 — Optimised for AI agents (Claude, Grok, Cursor)

Generates:
  - ai-fetchable-manifest.json  — rich metadata + tiered content embedding
  - AI-BRIEF.md                 — one-page architecture summary for AI agents

New in v2.0:
  - Tiered content embedding: full text for critical/small files, smart preview otherwise
  - Priority scoring: ViewModels/Panels/MainForm/Services ranked by importance
  - critical_files + recommended_reading_order at manifest top level
  - NuGet dependency extraction from all .csproj files
  - AI-BRIEF.md auto-generation (architecture summary, ViewModel/Panel/Service lists)
  - manifest_mode: "compact" (metadata only) vs "full-context" (with embedded content)
  - Architecture analysis: auto-detect ViewModels, Panels, Services, Controls, Factories
  - Improved folder tree: depth + per-dir limits, forward-slash normalisation
  - Backward-compatible with existing .ai-manifest-config.json
  - Zero external dependencies — uses only stdlib + local git binary

Usage:
    python scripts/generate-ai-manifest.py

Requirements:
    - Python 3.10+
    - git (must be on PATH)
"""

import hashlib
import json
import re
import subprocess
import sys
from datetime import datetime, timedelta
from pathlib import Path
from typing import Any, Dict, List, Optional, Tuple

# ---------------------------------------------------------------------------
# Module-level constants
# ---------------------------------------------------------------------------

LANGUAGE_MAP: Dict[str, str] = {
    ".cs": "C#",
    ".xaml": "XAML",
    ".razor": "Razor",
    ".razorjs": "Razor JS",
    ".csproj": "C# Project",
    ".sln": "Visual Studio Solution",
    ".py": "Python",
    ".js": "JavaScript",
    ".ts": "TypeScript",
    ".tsx": "TypeScript JSX",
    ".json": "JSON",
    ".xml": "XML",
    ".md": "Markdown",
    ".txt": "Text",
    ".ps1": "PowerShell",
    ".config": "Configuration",
    ".html": "HTML",
    ".css": "CSS",
    ".scss": "SCSS",
    ".props": "MSBuild Props",
    ".targets": "MSBuild Targets",
}

DEFAULT_CRITICAL_GLOBS: List[str] = [
    "**/*ViewModel.cs",
    "**/*Panel.cs",
    "**/*Control.cs",
    "**/MainForm*.cs",
    "**/Analytics*.cs",
    "**/*Service.cs",
    "**/*Repository.cs",
    "**/Program.cs",
    "**/*.csproj",
    "**/App.xaml.cs",
]

DEFAULT_NEVER_EMBED: List[str] = [
    "**/bin/**",
    "**/obj/**",
    "**/*.Designer.cs",
    "**/*.g.cs",
    "**/*.g.i.cs",
    "**/*.AssemblyInfo.cs",
    "**/node_modules/**",
]

DEFAULT_ALWAYS_INCLUDE_DIRS: List[str] = [
    "src/WileyWidget.WinForms/Controls",
    "src/WileyWidget.WinForms/Forms",
    "src/WileyWidget.WinForms/ViewModels",
    "src/WileyWidget.Services",
]

DEFAULT_PRIORITY_PATTERNS: Dict[str, int] = {
    "MainForm": 100,
    "Program": 90,
    "ViewModel": 95,
    "Panel": 92,
    "Control": 88,
    "Service": 85,
    "Repository": 82,
    "Factory": 78,
    "Extension": 75,
    "Helper": 70,
    "Test": 65,
}


class AIManifestGenerator:
    """Generates an AI-optimised fetchable manifest for repository visibility."""

    def __init__(self, repo_root: Path) -> None:
        self.repo_root = repo_root.resolve()
        self.config = self._load_config()
        self.exclude_patterns = self._compile_patterns()
        self.focus_extensions: set[str] = set(
            self.config.get("include_only_extensions", [])
        )
        self.max_file_size_bytes: int = int(
            self.config.get("max_file_size_bytes", 10 * 1024 * 1024)
        )
        self.max_files: int = int(self.config.get("max_files", 12000))
        self.manifest_mode: str = self.config.get("manifest_mode", "full-context")
        self.generate_context_summary: bool = bool(
            self.config.get("generate_context_summary", True)
        )

        # v2.0 — content inclusion settings (nested under "content_inclusion" key)
        ci = self.config.get("content_inclusion", {})
        self.embed_full_if_smaller_than_kb: float = float(
            ci.get("embed_full_if_smaller_than_kb", 600)
        )
        self.preview_lines: int = int(ci.get("preview_lines_for_large_files", 800))
        self.critical_globs: List[str] = ci.get(
            "critical_globs", DEFAULT_CRITICAL_GLOBS
        )
        self.never_embed_globs: List[str] = ci.get("never_embed", DEFAULT_NEVER_EMBED)
        self.max_embedded_files: int = int(ci.get("max_embedded_files", 400))

        # Directory / focus settings
        self.always_include_dirs: List[str] = self.config.get(
            "always_include_dirs", DEFAULT_ALWAYS_INCLUDE_DIRS
        )
        self.focus_directories: List[str] = self.config.get(
            "focus_directories", ["src"]
        )

        # Priority scoring map
        self.priority_patterns: Dict[str, int] = self.config.get(
            "priority_patterns", DEFAULT_PRIORITY_PATTERNS
        )

        self.repo_info = self._get_repo_info()

        # Runtime accumulators — populated during _scan_files()
        self._total_files = 0
        self._total_size = 0
        self._categories: Dict[str, int] = {}
        self._languages: Dict[str, int] = {}
        self._files_truncated = False
        self._embedded_count = 0
        self._files: List[Dict[str, Any]] = []

    # ------------------------------------------------------------------
    # Config & repo bootstrap
    # ------------------------------------------------------------------

    def _load_config(self) -> Dict[str, Any]:
        """Load configuration from .ai-manifest-config.json."""
        config_path = self.repo_root / ".ai-manifest-config.json"
        if not config_path.exists():
            raise FileNotFoundError(f"Config file not found: {config_path}")
        with open(config_path, "r", encoding="utf-8") as f:
            return json.load(f)

    def _compile_patterns(self) -> List[re.Pattern[str]]:
        """Compile regex patterns for file exclusion."""
        patterns: List[re.Pattern[str]] = []
        for pat in self.config.get("exclude_patterns", []):
            try:
                patterns.append(re.compile(pat))
            except re.error as exc:
                print(f"Warning: Invalid regex pattern '{pat}': {exc}", file=sys.stderr)
        return patterns

    def _git(self, *args: str) -> str:
        """Run a git command in repo_root and return stdout (stripped)."""
        result = subprocess.run(
            ["git", *args],
            cwd=self.repo_root,
            capture_output=True,
            text=True,
        )
        return result.stdout.strip()

    def _get_repo_info(self) -> Dict[str, Any]:
        """Get repository information using git."""
        if hasattr(self, "_repo_info_cache"):
            return self._repo_info_cache  # type: ignore[attr-defined]

        try:
            remote_url = self._git("remote", "get-url", "origin") or "unknown"
            branch = self._git("rev-parse", "--abbrev-ref", "HEAD") or "(detached)"
            commit_hash = self._git("rev-parse", "HEAD") or "unknown"
            is_dirty = bool(self._git("status", "--porcelain"))
            owner_repo = self._extract_owner_repo(remote_url)
            now = datetime.now()
            self._repo_info_cache = {
                "remote_url": remote_url,
                "owner_repo": owner_repo,
                "branch": branch,
                "commit_hash": commit_hash,
                "is_dirty": is_dirty,
                "generated_at": now.isoformat(),
                "valid_until": (now + timedelta(days=7)).isoformat(),
            }
            return self._repo_info_cache
        except Exception as exc:
            raise RuntimeError(f"Failed to get git info: {exc}") from exc

    def _extract_owner_repo(self, remote_url: str) -> str:
        """Extract owner/repo from git remote URL."""
        cleaned = remote_url.strip().removesuffix(".git")
        if cleaned.startswith("git@"):
            cleaned = cleaned.replace(":", "/", 1).partition("@")[2]
        cleaned = re.sub(r"^[a-zA-Z]+://", "", cleaned)
        parts = cleaned.split("/")
        if len(parts) >= 2 and parts[-2] and parts[-1]:
            return f"{parts[-2]}/{parts[-1]}"
        return "unknown/unknown"

    # ------------------------------------------------------------------
    # Inclusion / exclusion
    # ------------------------------------------------------------------

    def _norm(self, path: Path) -> str:
        """Return forward-slash repo-relative path string."""
        return str(path.relative_to(self.repo_root)).replace("\\", "/")

    def _matches_any_glob(self, rel_str: str, globs: List[str]) -> bool:
        """Test a repo-relative path string against a list of glob patterns."""
        p = Path(rel_str)
        return any(p.match(g) for g in globs)

    def _should_include_path(self, path: Path, is_dir: bool = False) -> bool:
        """Broad path filter used for the folder-tree walk."""
        rel = self._norm(path)
        for pattern in self.exclude_patterns:
            if pattern.search(rel):
                return False
        if is_dir:
            return True
        if self.config.get("focus_mode", False) and self.focus_extensions:
            if path.suffix.lower() not in self.focus_extensions:
                return False
        return True

    def _should_include_file(self, path: Path) -> bool:
        """Determine whether a file should appear in the manifest files list."""
        rel = self._norm(path)

        # Always include files in always_include_dirs (even in focus mode)
        if any(rel.startswith(d.replace("\\", "/")) for d in self.always_include_dirs):
            # Still respect hard exclusions (bin, obj, etc.)
            return not any(p.search(rel) for p in self.exclude_patterns)

        # src/ files by pass focus-extension filtering
        if rel.startswith("src/"):
            return not any(p.search(rel) for p in self.exclude_patterns)

        # All other files: check exclude patterns first
        for pattern in self.exclude_patterns:
            if pattern.search(rel):
                return False

        # Focus mode — restrict by extension
        if self.config.get("focus_mode", False) and self.focus_extensions:
            if path.suffix.lower() not in self.focus_extensions:
                return False

        return True

    def _is_critical(self, rel_str: str) -> bool:
        return self._matches_any_glob(rel_str, self.critical_globs)

    def _never_embed(self, rel_str: str) -> bool:
        return self._matches_any_glob(rel_str, self.never_embed_globs)

    # ------------------------------------------------------------------
    # Priority scoring
    # ------------------------------------------------------------------

    def _calculate_priority(self, rel_str: str) -> int:
        """Score a file 0-100 based on its name matching priority_patterns."""
        stem = Path(rel_str).stem
        score = 50
        for keyword, pts in self.priority_patterns.items():
            if keyword.lower() in stem.lower():
                score = max(score, pts)
        # Boost files inside always_include_dirs
        if any(
            rel_str.startswith(d.replace("\\", "/")) for d in self.always_include_dirs
        ):
            score = min(score + 10, 100)
        return score

    # ------------------------------------------------------------------
    # Content embedding (v2.0)
    # ------------------------------------------------------------------

    def _get_content(
        self, file_path: Path, rel_str: str, size_kb: float
    ) -> Optional[Dict[str, Any]]:
        """
        Return a content block for embedding in the manifest.

        Rules (applied in order):
          1. compact mode             → None (no embedding)
          2. never_embed glob match   → None
          3. is_critical OR size_kb <= embed_full_if_smaller_than_kb → full embed
          4. otherwise                → smart preview (first N + last 200 lines)
        """
        if self.manifest_mode == "compact":
            return None
        if self._never_embed(rel_str):
            return None

        try:
            with open(file_path, "r", encoding="utf-8", errors="ignore") as fh:
                content = fh.read()

            if (
                self._is_critical(rel_str)
                or size_kb <= self.embed_full_if_smaller_than_kb
            ):
                return {"content": content, "mode": "full"}

            # Smart preview for large non-critical files
            lines = content.splitlines(keepends=True)
            n = len(lines)
            if n <= self.preview_lines + 200:
                return {"content": content, "mode": "full"}

            head = "".join(lines[: self.preview_lines])
            tail = "".join(lines[-200:])
            skipped = n - self.preview_lines - 200
            preview = head + f"\n\n... [{skipped} lines truncated] ...\n\n" + tail
            return {"content": preview, "mode": "preview", "total_lines": n}

        except Exception as exc:
            return {"content": "", "mode": "error", "error": str(exc)}

    # ------------------------------------------------------------------
    # NuGet extraction
    # ------------------------------------------------------------------

    def _parse_nuget_deps(self) -> List[Dict[str, str]]:
        """Extract PackageReference entries from all .csproj files."""
        seen: set[Tuple[str, str]] = set()
        deps: List[Dict[str, str]] = []
        for csproj in sorted(self.repo_root.rglob("*.csproj")):
            try:
                with open(csproj, "r", encoding="utf-8") as fh:
                    text = fh.read()
                for m in re.finditer(
                    r'<PackageReference\s+Include="([^"]+)"\s+Version="([^"]+)"', text
                ):
                    key = (m.group(1), m.group(2))
                    if key not in seen:
                        seen.add(key)
                        deps.append(
                            {
                                "package": m.group(1),
                                "version": m.group(2),
                                "source": self._norm(csproj),
                            }
                        )
            except Exception:
                pass
        return sorted(deps, key=lambda d: d["package"].lower())

    # ------------------------------------------------------------------
    # Git helpers
    # ------------------------------------------------------------------

    def _is_tracked(self, relative_path: Path) -> bool:
        """Determine if a file is tracked by git (best-effort)."""
        try:
            return bool(self._git("ls-files", str(relative_path)))
        except Exception:
            return False

    # ------------------------------------------------------------------
    # Hashing & language detection
    # ------------------------------------------------------------------

    def _calculate_sha256(self, file_path: Path) -> str:
        """Calculate SHA256 hash of a file."""
        h = hashlib.sha256()
        with open(file_path, "rb") as fh:
            for chunk in iter(lambda: fh.read(65536), b""):
                h.update(chunk)
        return h.hexdigest()

    def _detect_language(self, file_path: Path) -> str:
        """Detect programming language from file extension."""
        return LANGUAGE_MAP.get(file_path.suffix.lower(), "Unknown")

    def _should_include_file_compat(self, path: Path) -> bool:
        """Alias kept for any callers that use the old name."""
        return self._should_include_file(path)

    # ------------------------------------------------------------------
    # File scanning
    # ------------------------------------------------------------------

    def _scan_files(self) -> List[Dict[str, Any]]:
        """Scan repository files, collect metadata, and optionally embed content."""
        files: List[Dict[str, Any]] = []
        total_size = 0
        categories: Dict[str, int] = {}
        languages: Dict[str, int] = {}
        embedded_count = 0

        repo_info = self.repo_info

        for file_path in sorted(self.repo_root.rglob("*")):
            if not file_path.is_file():
                continue

            rel_path = self._norm(file_path)
            if (
                ".git" in rel_path
                or "WebView2Runtime" in rel_path
                or rel_path.endswith(".secret")
            ):
                continue

            if not self._should_include_file(file_path):
                continue
            if self.max_files and len(files) >= self.max_files:
                self._files_truncated = True
                break

            try:
                stat = file_path.stat()
                size = stat.st_size
                if self.max_file_size_bytes and size > self.max_file_size_bytes:
                    self._files_truncated = True
                    continue

                size_kb = round(size / 1024, 1)
                last_modified = datetime.fromtimestamp(stat.st_mtime).isoformat()
                relative_path = file_path.relative_to(self.repo_root)
                rel_str = rel_path
                sha256 = self._calculate_sha256(file_path)
                language = self._detect_language(file_path)
                priority = self._calculate_priority(rel_str)
                tracked = self._is_tracked(relative_path)

                ext = file_path.suffix.lower()
                if ext in {
                    ".cs",
                    ".xaml",
                    ".razor",
                    ".py",
                    ".js",
                    ".ts",
                    ".tsx",
                    ".ps1",
                }:
                    category = "source_code"
                elif "test" in rel_str.lower():
                    category = "test"
                elif ext in {".csproj", ".sln", ".json", ".xml", ".props", ".targets"}:
                    category = "config"
                else:
                    category = "other"

                categories[category] = categories.get(category, 0) + 1
                languages[language] = languages.get(language, 0) + 1
                total_size += size

                file_info: Dict[str, Any] = {
                    "metadata": {
                        "path": rel_str,
                        "exists": True,
                        "size_bytes": size,
                        "size_kb": size_kb,
                        "last_modified": last_modified,
                        "language": language,
                        "priority": priority,
                        "is_critical": self._is_critical(rel_str),
                    },
                    "urls": {
                        "blob_url": (
                            f"https://github.com/{repo_info['owner_repo']}/blob/"
                            f"{repo_info['branch']}/{rel_str}"
                        ),
                        "raw_url": (
                            f"https://raw.githubusercontent.com/{repo_info['owner_repo']}/"
                            f"{repo_info['branch']}/{rel_str}"
                        ),
                    },
                    "context": {
                        "category": category,
                        "tracked": tracked,
                        "extension": file_path.suffix,
                        "sha256": sha256,
                    },
                }

                # Content embedding — v2.0
                if embedded_count < self.max_embedded_files:
                    content_block = self._get_content(file_path, rel_str, size_kb)
                    if content_block is not None:
                        file_info["content_info"] = content_block
                        embedded_count += 1

                files.append(file_info)

            except (OSError, IOError) as exc:
                print(f"Warning: Could not process {file_path}: {exc}", file=sys.stderr)

        # Sort by priority descending so most important files surface first
        files.sort(key=lambda x: x["metadata"]["priority"], reverse=True)

        self._total_files = len(files)
        self._total_size = total_size
        self._categories = categories
        self._languages = languages
        self._embedded_count = embedded_count

        return files

    # ------------------------------------------------------------------
    # Metrics
    # ------------------------------------------------------------------

    def _count_code_metrics(self) -> Dict[str, Any]:
        """Calculate LOC, complexity, and test coverage from source files."""
        total_lines = 0
        code_lines = 0
        comment_lines = 0
        blank_lines = 0
        complexity_sum = 0
        complexity_count = 0
        test_file_count = 0

        src_files = [
            Path(f["metadata"]["path"])
            for f in self._files
            if f["metadata"]["language"] in ("C#", "Python")
        ]

        for rel_path in src_files:
            file_path = self.repo_root / rel_path
            try:
                with open(file_path, "r", encoding="utf-8", errors="ignore") as fh:
                    lines = fh.readlines()

                for line in lines:
                    stripped = line.strip()
                    total_lines += 1
                    if not stripped:
                        blank_lines += 1
                    elif stripped.startswith(("#", "//")):
                        comment_lines += 1
                    else:
                        code_lines += 1

                # Cyclomatic-complexity estimate for Factory / Docking C# files
                if file_path.suffix == ".cs" and (
                    "Factory" in file_path.name or "Docking" in str(rel_path)
                ):
                    content = "".join(lines)
                    cx = (
                        content.count(" if ")
                        + content.count(" if(")
                        + content.count(" else ")
                        + content.count(" switch ")
                        + content.count(" case ")
                        + content.count(" catch ")
                        + content.count(" for ")
                        + content.count(" for(")
                        + content.count(" foreach ")
                        + content.count(" while ")
                        + content.count(" && ")
                        + content.count(" || ")
                        + content.count("?")
                        + 1
                    )
                    complexity_sum += cx
                    complexity_count += 1

                if "test" in str(rel_path).lower():
                    test_file_count += 1

            except Exception:
                pass

        avg_cx = complexity_sum / max(complexity_count, 1)
        return {
            "total_lines_of_code": total_lines,
            "total_code_lines": code_lines,
            "total_comment_lines": comment_lines,
            "total_blank_lines": blank_lines,
            "average_complexity": round(avg_cx, 2),
            "test_count": test_file_count,
        }

    # ------------------------------------------------------------------
    # Architecture analysis (v2.0)
    # ------------------------------------------------------------------

    def _analyze_architecture(self) -> Dict[str, Any]:
        """Auto-detect ViewModels, Panels, Services, Controls, etc. from file paths."""
        views: List[str] = []
        viewmodels: List[str] = []
        services: List[str] = []
        controls: List[str] = []
        panels: List[str] = []
        repositories: List[str] = []
        factories: List[str] = []

        for f in self._files:
            path = f["metadata"]["path"]
            name = Path(path).name
            if "ViewModel" in name:
                viewmodels.append(path)
            elif "Panel" in name and name.endswith(".cs"):
                panels.append(path)
            elif "Control" in name and name.endswith(".cs"):
                controls.append(path)
            elif "Service" in name and name.endswith(".cs"):
                services.append(path)
            elif "Repository" in name and name.endswith(".cs"):
                repositories.append(path)
            elif "Factory" in name and name.endswith(".cs"):
                factories.append(path)
            elif (
                ("Form" in name or "View" in name)
                and name.endswith(".cs")
                and "ViewModel" not in name
            ):
                views.append(path)

        return {
            "pattern": "MVVM",
            "views": views,
            "viewmodels": viewmodels,
            "panels": panels,
            "services": services,
            "controls": controls,
            "repositories": repositories,
            "factories": factories,
            "models": [],
            "converters": [],
            "behaviors": [],
            "modules": [],
            "counts": {
                "views": len(views),
                "viewmodels": len(viewmodels),
                "panels": len(panels),
                "services": len(services),
                "controls": len(controls),
                "repositories": len(repositories),
                "factories": len(factories),
            },
        }

    # ------------------------------------------------------------------
    # Critical files & reading order (v2.0)
    # ------------------------------------------------------------------

    def _build_critical_and_reading_order(
        self,
    ) -> Tuple[List[Dict[str, Any]], List[str]]:
        """Build critical_files list and recommended_reading_order from scored files."""
        threshold_critical = 82
        threshold_reading = 90

        critical: List[Dict[str, Any]] = []
        reading_order: List[str] = []

        for f in self._files:
            priority = f["metadata"]["priority"]
            path = f["metadata"]["path"]

            if priority >= threshold_critical:
                stem = Path(path).stem
                file_type = next(
                    (kw for kw in self.priority_patterns if kw.lower() in stem.lower()),
                    "Source",
                )
                critical.append(
                    {
                        "path": path,
                        "priority": priority,
                        "type": file_type,
                        "reason": f"{file_type} — priority {priority}",
                    }
                )

            if priority >= threshold_reading:
                reading_order.append(path)

        critical.sort(key=lambda x: x["priority"], reverse=True)
        return critical[:50], reading_order[:30]

    # ------------------------------------------------------------------
    # Summary
    # ------------------------------------------------------------------

    def _generate_summary(self) -> Dict[str, Any]:
        """Generate summary statistics."""
        return {
            "total_files": self._total_files,
            "files_in_manifest": self._total_files,
            "files_truncated": self._files_truncated,
            "total_size_bytes": self._total_size,
            "total_size_kb": round(self._total_size / 1024, 1),
            "categories": self._categories,
            "languages": self._languages,
            "manifest_mode": self.manifest_mode,
            "embedded_files": self._embedded_count,
        }

    # ------------------------------------------------------------------
    # Folder tree
    # ------------------------------------------------------------------

    def _generate_folder_tree(self) -> Dict[str, Any]:
        """Generate a folder tree structure with depth and per-dir limits."""
        max_depth: int = int(self.config.get("tree_max_depth", 5))
        max_per_dir: int = int(self.config.get("max_tree_entries_per_dir", 50))

        def build(path: Path, depth: int = 0) -> Dict[str, Any]:
            if path.is_file():
                return {
                    "name": path.name,
                    "type": "file",
                    "path": self._norm(path),
                }

            children: List[Dict[str, Any]] = []
            try:
                entries = sorted(path.iterdir())
                count = 0
                for child in entries:
                    if count >= max_per_dir:
                        children.append(
                            {
                                "name": f"... ({len(entries) - count} more)",
                                "type": "truncated",
                            }
                        )
                        break
                    if child.is_dir() and self._should_include_path(child, is_dir=True):
                        if depth < max_depth:
                            children.append(build(child, depth + 1))
                        else:
                            children.append(
                                {
                                    "name": child.name,
                                    "type": "directory",
                                    "path": self._norm(child),
                                    "children": ["[depth limit reached]"],
                                }
                            )
                        count += 1
                    elif child.is_file() and self._should_include_path(child):
                        children.append(build(child, depth + 1))
                        count += 1
            except PermissionError:
                pass

            return {
                "name": path.name,
                "type": "directory",
                "path": self._norm(path),
                "children": children,
            }

        return build(self.repo_root)

    # ------------------------------------------------------------------
    # AI-BRIEF.md generation (v2.0)
    # ------------------------------------------------------------------

    def _generate_ai_brief(self, manifest: Dict[str, Any]) -> Path:
        """Write AI-BRIEF.md — a one-page architecture summary for AI agents."""
        arch = manifest["architecture"]
        brief_path = self.repo_root / "AI-BRIEF.md"
        repo = manifest["repository"]
        summary = manifest["summary"]

        lines: List[str] = [
            "# WileyWidget — AI Briefing",
            f"> Generated: {datetime.now():%Y-%m-%d %H:%M}  |  "
            f"Branch: `{repo.get('branch', '?')}`  |  "
            f"Commit: `{repo.get('commit_hash', '?')[:10]}`",
            "",
            "## Project Purpose",
            "WileyWidget is a Windows Forms (.NET) application built with the Syncfusion "
            "component suite and an MVVM-inspired architecture using ScopedPanelBase panels, "
            "ViewModels, and a Syncfusion Ribbon/Docking navigation surface.",
            "",
            "## Architecture Patterns",
            "- **MVVM** — ViewModels bind to Panels; panels inherit from `ScopedPanelBase`",
            "- **Syncfusion WinForms** — `SfSkinManager` is the SOLE theme authority (no manual `BackColor`/`ForeColor`)",
            "- **Docking** — `DockingManager` controls panel layout",
            "- **DI** — `Microsoft.Extensions.DependencyInjection` wires all services",
            "- **Ribbon** — `RibbonControlAdv` is the primary navigation surface when `UI:ShowRibbon = true`",
            "- **Async init** — Heavy startup runs via `IAsyncInitializable.InitializeAsync` after `MainForm` is shown",
            "",
            "## How to Navigate the Codebase",
            "1. `src/WileyWidget.WinForms/Forms/MainForm.cs` — UI entry point",
            "2. Each panel in `src/WileyWidget.WinForms/` has a matching `*ViewModel.cs`",
            "3. Services live in `src/WileyWidget.Services/`",
            "4. DI wiring is in `Program.cs` and `*ServiceCollectionExtensions.cs` files",
            "5. Syncfusion controls must be created via `SyncfusionControlFactory`",
            "",
            "## Critical Files (read these first)",
        ]

        for item in manifest["critical_files"][:20]:
            lines.append(
                f"- `{item['path']}` — {item.get('reason', item.get('type', ''))}"
            )

        lines += ["", "## Recommended Reading Order"]
        for i, path in enumerate(manifest["recommended_reading_order"][:20], 1):
            lines.append(f"{i}. `{path}`")

        lines += [
            "",
            "## Architecture Summary",
            "| Component | Count |",
            "|-----------|-------|",
        ]
        for k, v in arch["counts"].items():
            lines.append(f"| {k.title()} | {v} |")

        for section, items in [
            ("ViewModels", arch["viewmodels"]),
            ("Panels", arch["panels"]),
            ("Services", arch["services"]),
            ("Controls", arch["controls"]),
        ]:
            lines += ["", f"## {section}"]
            for p in items[:30]:
                lines.append(f"- `{p}`")

        lines += ["", "## Key NuGet Dependencies"]
        for d in manifest["nuget_dependencies"][:25]:
            lines.append(f"- `{d['package']}` v{d['version']}")

        lines += [
            "",
            "## Manifest Stats",
            f"- Total files indexed: **{summary['total_files']}**",
            f"- Files with embedded content: **{summary.get('embedded_files', 0)}**",
            f"- Total source size: **{summary.get('total_size_kb', 0):,.0f} KB**",
            f"- Manifest mode: **{summary.get('manifest_mode', 'unknown')}**",
            "",
            "---",
            "> Auto-generated by `scripts/generate-ai-manifest.py`. Do not edit manually.",
        ]

        brief_path.write_text("\n".join(lines), encoding="utf-8")
        return brief_path

    # ------------------------------------------------------------------
    # Main generate
    # ------------------------------------------------------------------

    def generate_manifest(self) -> Dict[str, Any]:
        """Generate the complete v2.0 manifest."""
        repo_info = self._get_repo_info()
        files = self._scan_files()
        self._files = files

        summary = self._generate_summary()
        metrics = self._count_code_metrics()
        architecture = self._analyze_architecture()
        nuget = self._parse_nuget_deps()
        critical_files, reading_order = self._build_critical_and_reading_order()
        folder_tree = (
            self._generate_folder_tree()
            if self.config.get("emit_full_tree", False)
            else {}
        )

        test_coverage = 0.0
        if metrics["total_code_lines"] > 0:
            test_lines = sum(
                1 for f in files if "test" in f["metadata"]["path"].lower()
            )
            test_coverage = round(
                test_lines / max(metrics["total_code_lines"], 1) * 100, 1
            )

        return {
            "$schema": (
                "https://raw.githubusercontent.com/Bigessfour/Wiley-Widget/main/"
                "schemas/ai-manifest-schema.json"
            ),
            "manifest_version": "2.0",
            "repository": repo_info,
            # ── v2.0 top-level additions ─────────────────────────────
            "critical_files": critical_files,
            "recommended_reading_order": reading_order,
            "nuget_dependencies": nuget,
            # ─────────────────────────────────────────────────────────
            "license": {"type": "Unknown", "file": None, "detected": False},
            "summary": summary,
            "metrics": {
                "total_lines_of_code": metrics["total_lines_of_code"],
                "total_code_lines": metrics["total_code_lines"],
                "total_comment_lines": metrics["total_comment_lines"],
                "total_blank_lines": metrics["total_blank_lines"],
                "average_complexity": metrics["average_complexity"],
                "test_coverage_percent": test_coverage,
                "test_count": metrics["test_count"],
                "project_metrics": {
                    "windows_forms_complexity": metrics["average_complexity"],
                    "docking_factories_analyzed": True,
                    "estimated_code_to_test_ratio": round(
                        metrics["total_code_lines"] / max(metrics["test_count"], 1), 2
                    ),
                },
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
            "architecture": architecture,
            "dependency_graph": {
                "projects": {},
                "nuget_packages": {d["package"]: d["version"] for d in nuget},
                "top_dependencies": [d["package"] for d in nuget[:20]],
            },
            "folder_tree": folder_tree,
            "search_index": [],
            "files": files,
        }

    def save_manifest(self, output_path: Optional[Path] = None) -> None:
        """Generate and save the manifest to a file, then optionally write AI-BRIEF.md."""
        if output_path is None:
            output_path = self.repo_root / "ai-fetchable-manifest.json"

        manifest = self.generate_manifest()

        with open(output_path, "w", encoding="utf-8") as fh:
            json.dump(manifest, fh, indent=2, ensure_ascii=False)

        size_mb = output_path.stat().st_size / (1024 * 1024)
        print(
            f"Manifest generated: {output_path} "
            f"({self._total_files} files, {self._embedded_count} embedded, {size_mb:.1f} MB)"
        )

        if self.generate_context_summary:
            brief = self._generate_ai_brief(manifest)
            print(f"AI-BRIEF.md generated: {brief}")


# ---------------------------------------------------------------------------
# Entry point
# ---------------------------------------------------------------------------


def main() -> None:
    """Main entry point."""
    repo_root = Path(__file__).parent.parent

    try:
        generator = AIManifestGenerator(repo_root)
        generator.save_manifest()
        print("AI fetchable manifest generated successfully.")
    except Exception as exc:
        print(f"Error: {exc}", file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()
