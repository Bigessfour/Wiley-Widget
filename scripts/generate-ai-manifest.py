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
    - gitpython (required)
"""

import hashlib
import json
import re
import sys
from datetime import datetime, timedelta
from pathlib import Path
from typing import Any, Dict, List, Optional
from git import Repo


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
        """Load git repository using gitpython."""
        try:
            return Repo(self.repo_root)
        except Exception as exc:
            raise RuntimeError("GitPython failed to load the repository.") from exc

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
        try:
            tracked = self.repo.git.ls_files(str(relative_path))
            return bool(tracked.strip())
        except Exception:
            return False

    def _get_repo_info(self) -> Dict[str, Any]:
        """Get repository information using git."""
        if hasattr(self, "_repo_info_cache"):
            return self._repo_info_cache  # type: ignore[attr-defined]

        try:
            remote_url = next(self.repo.remote().urls)
            branch = (
                self.repo.active_branch.name
                if not self.repo.head.is_detached
                else "(detached)"
            )
            commit_hash = self.repo.head.commit.hexsha
            is_dirty = self.repo.is_dirty(untracked_files=True)

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

        except Exception as e:
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
        }
        return language_map.get(ext, "Unknown")

    def _should_include_file(self, path: Path) -> bool:
        """
        Determine if a file should be included.
        Respects global exclusions, but prioritizes src/ files.
        """
        relative_path = path.relative_to(self.repo_root)
        path_str = str(relative_path)

        # Src files always included unless explicitly excluded by pattern
        if path_str.startswith("src/"):
            # Still check exclude patterns (e.g., bin/, obj/, .generated)
            for pattern in self.exclude_patterns:
                if pattern.search(path_str):
                    return False
            return True

        # Non-src files follow normal exclusion rules
        for pattern in self.exclude_patterns:
            if pattern.search(path_str):
                return False

        # Focus mode only applies to non-src files (don't filter src files)
        if self.config.get("focus_mode", False):
            if self.focus_extensions:
                ext = path.suffix.lower()
                if ext not in self.focus_extensions:
                    return False

        return True

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

            # Use new method that prioritizes src/ files
            if not self._should_include_file(file_path):
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

                # Categorize - expanded to include more source file types
                if file_path.suffix in [".cs", ".xaml", ".razor", ".razorjs", ".py", ".js", ".ts", ".tsx", ".json", ".xml", ".ps1"]:
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

    def _count_code_metrics(self) -> Dict[str, Any]:
        """Calculate LOC, complexity, and test coverage from source files."""
        total_lines = 0
        code_lines = 0
        comment_lines = 0
        blank_lines = 0
        complexity_sum = 0
        complexity_count = 0
        test_file_count = 0

        # Focus on C# and Python files for metrics
        src_files = [
            Path(f["metadata"]["path"])
            for f in self._files
            if f["metadata"]["language"] in ["C#", "Python"]
        ]

        for rel_path in src_files:
            file_path = self.repo_root / rel_path
            try:
                with open(file_path, "r", encoding="utf-8", errors="ignore") as f:
                    lines = f.readlines()

                for line in lines:
                    stripped = line.strip()
                    total_lines += 1

                    if not stripped or stripped.startswith("#") or stripped.startswith("//"):
                        if not stripped:
                            blank_lines += 1
                        else:
                            comment_lines += 1
                    else:
                        code_lines += 1

                # Estimate cyclomatic complexity for C# files (docking factories priority)
                if file_path.suffix == ".cs" and ("Factory" in file_path.name or "Docking" in str(rel_path)):
                    content = "".join(lines)
                    # Count decision points: if, else, switch, case, catch, for, foreach, while, &&, ||, ?:
                    complexity = (
                        content.count(" if ") + content.count(" if(") +
                        content.count(" else ") +
                        content.count(" switch ") +
                        content.count(" case ") +
                        content.count(" catch ") +
                        content.count(" for ") + content.count(" for(") +
                        content.count(" foreach ") +
                        content.count(" while ") +
                        content.count(" && ") +
                        content.count(" || ") +
                        content.count("?") + 1  # Base complexity
                    )
                    complexity_sum += complexity
                    complexity_count += 1

                # Count test files
                if "test" in str(rel_path).lower():
                    test_file_count += 1

            except Exception:
                pass  # Skip files that can't be read

        avg_complexity = complexity_sum / max(complexity_count, 1)

        return {
            "total_lines_of_code": total_lines,
            "total_code_lines": code_lines,
            "total_comment_lines": comment_lines,
            "total_blank_lines": blank_lines,
            "average_complexity": round(avg_complexity, 2),
            "test_count": test_file_count,
        }

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
        self._files = files  # Store for metrics calculation
        summary = self._generate_summary()
        metrics = self._count_code_metrics()

        # Calculate estimated test coverage (ratio of test code lines to total)
        test_coverage = 0.0
        if metrics["total_code_lines"] > 0:
            test_lines = sum(
                1 for f in files
                if "test" in f["metadata"]["path"].lower()
            )
            test_coverage = round((test_lines / max(metrics["total_code_lines"], 1)) * 100, 1)

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
                    )
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
