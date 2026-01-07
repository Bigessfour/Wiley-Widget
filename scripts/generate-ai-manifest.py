#!/usr/bin/env python3
"""
AI Fetchable Manifest Generator

Generates ai-fetchable-manifest.json for AI agent visibility into the repository.
Uses .ai-manifest-config.json for file inclusion/exclusion rules.

Usage:
    python scripts/generate-ai-manifest.py

Requirements:
    - Python 3.14+
    - git (for repository information)
    - Optional: gitpython for better git integration
"""

import hashlib
import json
import re
import subprocess
import sys
from datetime import datetime, timedelta
from pathlib import Path
from typing import Any, Dict, List, Optional

try:
    from git import Repo  # type: ignore
except Exception:  # pragma: no cover - optional dependency
    Repo = None


class AIManifestGenerator:
    """Generates AI fetchable manifest for repository visibility."""

    def __init__(self, repo_root: Path):
        self.repo_root = repo_root
        self.config = self._load_config()
        self.exclude_patterns = self._compile_patterns()
        self.focus_extensions = set(self.config.get("include_only_extensions", []))
        self.max_file_size_bytes = int(
            self.config.get("max_file_size_bytes", 5 * 1024 * 1024)
        )
        self.max_files = int(self.config.get("max_files", 5000))

        self.repo = self._load_repo()
        self.repo_info = self._get_repo_info()

    def _load_repo(self):
        """Load git repository using gitpython when available."""
        if Repo is None:
            return None
        try:
            return Repo(self.repo_root)
        except Exception:
            return None

    def _load_config(self) -> Dict[str, Any]:
        """Load configuration from .ai-manifest-config.json."""
        config_path = self.repo_root / ".ai-manifest-config.json"
        if not config_path.exists():
            raise FileNotFoundError(f"Config file not found: {config_path}")

        with open(config_path, "r", encoding="utf-8") as f:
            return json.load(f)

    def _compile_patterns(self) -> List[re.Pattern[str]]:
        """Compile regex patterns for file exclusion."""
        patterns = []
        for pattern in self.config.get("exclude_patterns", []):
            try:
                patterns.append(re.compile(pattern))
            except re.error as e:
                print(
                    f"Warning: Invalid regex pattern '{pattern}': {e}", file=sys.stderr
                )
        return patterns

    def _should_include_path(self, path: Path, is_dir: bool = False) -> bool:
        """Determine if a path should be included in the manifest/tree."""
        relative_path = path.relative_to(self.repo_root)
        path_str = str(relative_path)

        for pattern in self.exclude_patterns:
            if pattern.search(path_str):
                return False

        if is_dir:
            return True

        if self.config.get("focus_mode", False):
            if self.focus_extensions:
                ext = path.suffix.lower()
                if ext not in self.focus_extensions:
                    return False

        return True

    def _is_tracked(self, relative_path: Path) -> bool:
        """Determine if a file is tracked by git (best-effort)."""
        if self.repo:
            try:
                tracked = self.repo.git.ls_files(str(relative_path))
                return bool(tracked.strip())
            except Exception:
                return False
        return True

    def _get_repo_info(self) -> Dict[str, Any]:
        """Get repository information using git."""
        if hasattr(self, "_repo_info_cache"):
            return self._repo_info_cache  # type: ignore[attr-defined]

        try:
            if self.repo:
                remote_url = next(self.repo.remote().urls)
                branch = (
                    self.repo.active_branch.name
                    if not self.repo.head.is_detached
                    else "(detached)"
                )
                commit_hash = self.repo.head.commit.hexsha
                is_dirty = self.repo.is_dirty(untracked_files=True)
            else:
                remote_url = subprocess.check_output(
                    ["git", "config", "--get", "remote.origin.url"],
                    cwd=self.repo_root,
                    text=True,
                ).strip()

                branch = subprocess.check_output(
                    ["git", "branch", "--show-current"], cwd=self.repo_root, text=True
                ).strip()

                commit_hash = subprocess.check_output(
                    ["git", "rev-parse", "HEAD"], cwd=self.repo_root, text=True
                ).strip()

                status = subprocess.run(
                    ["git", "status", "--porcelain"],
                    cwd=self.repo_root,
                    capture_output=True,
                    text=True,
                )
                is_dirty = bool(status.stdout.strip())

            # Extract owner/repo from URL
            owner_repo = self._extract_owner_repo(remote_url)

            generated_at = datetime.now().isoformat()
            valid_until = (datetime.now() + timedelta(days=7)).isoformat()

            self._repo_info_cache = {
                "remote_url": remote_url,
                "owner_repo": owner_repo,
                "branch": branch,
                "commit_hash": commit_hash,
                "is_dirty": is_dirty,
                "generated_at": generated_at,
                "valid_until": valid_until,
            }
            return self._repo_info_cache

        except subprocess.CalledProcessError as e:
            raise RuntimeError(f"Failed to get git info: {e}") from e

    def _extract_owner_repo(self, remote_url: str) -> str:
        """Extract owner/repo from git remote URL."""
        cleaned = remote_url.strip()
        cleaned = cleaned.removesuffix(".git")

        # Normalize ssh and https forms
        if cleaned.startswith("git@"):  # git@github.com:owner/repo
            cleaned = cleaned.replace(":", "/", 1)
            cleaned = cleaned.partition("@")[2]

        # Drop protocol prefixes
        cleaned = re.sub(r"^[a-zA-Z]+://", "", cleaned)

        parts = cleaned.split("/")
        if len(parts) >= 2:
            owner = parts[-2]
            repo = parts[-1]
            if owner and repo:
                return f"{owner}/{repo}"

        return "unknown/unknown"

    def _calculate_sha256(self, file_path: Path) -> str:
        """Calculate SHA256 hash of a file."""
        hash_sha256 = hashlib.sha256()
        with open(file_path, "rb") as f:
            for chunk in iter(lambda: f.read(4096), b""):
                hash_sha256.update(chunk)
        return hash_sha256.hexdigest()

    def _detect_language(self, file_path: Path) -> str:
        """Detect programming language from file extension."""
        ext = file_path.suffix.lower()
        language_map = {
            ".cs": "C#",
            ".xaml": "XAML",
            ".csproj": "C# Project",
            ".sln": "Visual Studio Solution",
            ".py": "Python",
            ".js": "JavaScript",
            ".ts": "TypeScript",
            ".json": "JSON",
            ".xml": "XML",
            ".md": "Markdown",
            ".txt": "Text",
            ".ps1": "PowerShell",
        }
        return language_map.get(ext, "Unknown")

    def _scan_files(self) -> List[Dict[str, Any]]:
        """Scan repository files and collect metadata."""
        files = []
        total_size = 0
        categories = {}
        languages = {}

        repo_info = self.repo_info
        files_truncated = False

        for file_path in self.repo_root.rglob("*"):
            if not file_path.is_file():
                continue

            if not self._should_include_path(file_path):
                continue

            if self.max_files and len(files) >= self.max_files:
                files_truncated = True
                break

            try:
                stat = file_path.stat()
                size = stat.st_size
                if self.max_file_size_bytes and size > self.max_file_size_bytes:
                    files_truncated = True
                    continue
                last_modified = datetime.fromtimestamp(stat.st_mtime).isoformat()

                relative_path = file_path.relative_to(self.repo_root)
                sha256 = self._calculate_sha256(file_path)
                language = self._detect_language(file_path)

                # Categorize
                if file_path.suffix in [".cs", ".xaml", ".py", ".js", ".ts"]:
                    category = "source_code"
                elif "test" in str(relative_path).lower():
                    category = "test"
                else:
                    category = "unknown"

                categories[category] = categories.get(category, 0) + 1
                languages[language] = languages.get(language, 0) + 1

                total_size += size

                tracked = self._is_tracked(relative_path)

                file_info = {
                    "metadata": {
                        "path": str(relative_path),
                        "exists": True,
                        "size": size,
                        "last_modified": last_modified,
                        "language": language,
                    },
                    "urls": {
                        "blob_url": f"https://github.com/{repo_info['owner_repo']}/blob/{repo_info['branch']}/{relative_path}",
                        "raw_url": f"https://raw.githubusercontent.com/{repo_info['owner_repo']}/{repo_info['branch']}/{relative_path}",
                    },
                    "context": {
                        "category": category,
                        "tracked": tracked,
                        "extension": file_path.suffix,
                        "sha256": sha256,
                    },
                }

                files.append(file_info)

            except (OSError, IOError) as e:
                print(f"Warning: Could not process {file_path}: {e}", file=sys.stderr)

        # Store for summary
        self._total_files = len(files)
        self._total_size = total_size
        self._categories = categories
        self._languages = languages
        self._files_truncated = files_truncated

        return files

    def _generate_summary(self) -> Dict[str, Any]:
        """Generate summary statistics."""
        return {
            "total_files": self._total_files,
            "files_in_manifest": self._total_files,
            "files_truncated": self._files_truncated,
            "total_size": self._total_size,
            "categories": self._categories,
            "languages": self._languages,
        }

    def generate_manifest(self) -> Dict[str, Any]:
        """Generate the complete manifest."""
        repo_info = self._get_repo_info()
        files = self._scan_files()
        summary = self._generate_summary()

        manifest = {
            "$schema": "https://raw.githubusercontent.com/Bigessfour/Wiley-Widget/main/schemas/ai-manifest-schema.json",
            "repository": repo_info,
            "license": {
                "type": "Unknown",
                "file": None,
                "detected": False,
            },
            "summary": summary,
            "metrics": {
                "total_lines_of_code": 0,  # Placeholder
                "total_code_lines": 0,
                "total_comment_lines": 0,
                "total_blank_lines": 0,
                "average_complexity": 0.0,
                "test_coverage_percent": 0.0,
                "test_count": 0,
                "project_metrics": {},  # Placeholder
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
                "views": [],  # Placeholder
                "viewmodels": [],  # Placeholder
                "models": [],  # Placeholder
                "services": [],  # Placeholder
                "repositories": [],  # Placeholder
                "converters": [],
                "behaviors": [],
                "modules": [],
                "counts": {},  # Placeholder
            },
            "dependency_graph": {
                "projects": {},
                "nuget_packages": {},
                "top_dependencies": [],
            },
            "folder_tree": self._generate_folder_tree(),
            "search_index": [],  # Placeholder
            "files": files,
        }

        return manifest

    def _generate_folder_tree(self) -> Dict[str, Any]:
        """Generate a folder tree structure."""

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
                    if child.is_dir():
                        if self._should_include_path(child, is_dir=True):
                            children.append(build_tree(child))
                    elif self._should_include_path(child):
                        children.append(build_tree(child))
            except PermissionError:
                pass

            return {
                "name": path.name,
                "type": "directory",
                "path": str(path.relative_to(self.repo_root)),
                "children": children,
            }

        return build_tree(self.repo_root)

    def save_manifest(self, output_path: Optional[Path] = None) -> None:
        """Generate and save the manifest to a file."""
        if output_path is None:
            output_path = self.repo_root / "ai-fetchable-manifest.json"

        manifest = self.generate_manifest()

        with open(output_path, "w", encoding="utf-8") as f:
            json.dump(manifest, f, indent=2, ensure_ascii=False)

        print(f"Manifest generated: {output_path}")


def main():
    """Main entry point."""
    repo_root = Path(__file__).parent.parent

    try:
        generator = AIManifestGenerator(repo_root)
        generator.save_manifest()
        print("AI fetchable manifest generated successfully.")
    except Exception as e:
        print("Error: {}".format(e), file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()
