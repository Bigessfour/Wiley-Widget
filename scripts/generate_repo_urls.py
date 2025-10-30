#!/usr/bin/env python3
"""
Generate AI-Fetchable Manifest (ai-fetchable-manifest.json)

This script creates a comprehensive manifest of the repository with:
- File metadata (size, timestamps, hashes, languages)
- GitHub URLs (blob, raw, history)
- Categorization (source_code, test, documentation, etc.)
- Search index for AI tools
- Related files and dependencies analysis
"""

import argparse
import hashlib
import json
import mimetypes
import os
import re
import subprocess
from datetime import datetime
from pathlib import Path
from typing import Dict, List, Optional

# Configuration defaults
DEFAULT_CONFIG_FILE = ".ai-manifest-config.json"
DEFAULT_OUTPUT_FILE = "ai-fetchable-manifest.json"

# Language detection by extension
LANGUAGE_MAP = {
    ".cs": "C#",
    ".csproj": "C# Project",
    ".sln": "Visual Studio Solution",
    ".xaml": "XAML",
    ".json": "JSON",
    ".yaml": "YAML",
    ".yml": "YAML",
    ".md": "Markdown",
    ".txt": "Text",
    ".xml": "XML",
    ".config": "Configuration",
    ".ps1": "PowerShell",
    ".psm1": "PowerShell",
    ".psd1": "PowerShell",
    ".py": "Python",
    ".sql": "SQL",
    ".js": "JavaScript",
    ".ts": "TypeScript",
    ".html": "HTML",
    ".css": "CSS",
    ".sh": "Shell",
    ".bat": "Batch",
    ".toml": "TOML",
}

# Binary file extensions
BINARY_EXTENSIONS = {
    ".dll",
    ".exe",
    ".pdb",
    ".cache",
    ".png",
    ".jpg",
    ".jpeg",
    ".gif",
    ".ico",
    ".bmp",
    ".zip",
    ".7z",
    ".tar",
    ".gz",
    ".db",
    ".sqlite",
    ".bin",
}

# Exclude patterns
DEFAULT_EXCLUDE_PATTERNS = [
    r"\.git/",
    r"\.vs/",
    r"\.vscode/(?!.*\.json$)",  # Exclude .vscode except .json files
    r"bin/",
    r"obj/",
    r"node_modules/",
    r"\.venv/",
    r"__pycache__/",
    r"\.pytest_cache/",
    r"\.trunk/",
    r"TestResults/",
    r"\.idea/",
    r"coverage/",
    r"htmlcov/",
    r"\.dll$",
    r"\.exe$",
    r"\.pdb$",
    r"\.cache$",
    r"\.suo$",
    r"\.user$",
    r"Thumbs\.db$",
    r"Desktop\.ini$",
    r"\.DS_Store$",
]


class RepositoryAnalyzer:
    """Analyzes repository and generates AI-fetchable manifest."""

    def __init__(self, config_path: Optional[str] = None):
        self.root_path = Path.cwd()
        self.config = self._load_config(config_path)
        self.exclude_patterns = self._compile_exclude_patterns()
        self.git_info = self._get_git_info()

    def _load_config(self, config_path: Optional[str]) -> Dict:
        """Load configuration from JSON file."""
        config_file = Path(config_path or DEFAULT_CONFIG_FILE)
        if config_file.exists():
            try:
                with open(config_file, "r", encoding="utf-8") as f:
                    return json.load(f)
            except Exception as e:
                print(f"Warning: Could not load config file: {e}")
        return {}

    def _compile_exclude_patterns(self) -> List[re.Pattern]:
        """Compile exclude patterns from config and defaults."""
        patterns = DEFAULT_EXCLUDE_PATTERNS.copy()
        if "exclude_patterns" in self.config:
            patterns.extend(self.config["exclude_patterns"])
        return [re.compile(pattern) for pattern in patterns]

    def _should_exclude(self, path: str) -> bool:
        """Check if path should be excluded."""
        normalized = path.replace("\\", "/")
        for pattern in self.exclude_patterns:
            if pattern.search(normalized):
                return True
        return False

    def _get_git_info(self) -> Dict:
        """Get git repository information."""
        try:
            remote_url = subprocess.check_output(
                ["git", "config", "--get", "remote.origin.url"],
                stderr=subprocess.DEVNULL,
                text=True,
            ).strip()

            branch = subprocess.check_output(
                ["git", "rev-parse", "--abbrev-ref", "HEAD"],
                stderr=subprocess.DEVNULL,
                text=True,
            ).strip()

            commit_hash = subprocess.check_output(
                ["git", "rev-parse", "HEAD"],
                stderr=subprocess.DEVNULL,
                text=True,
            ).strip()

            # Check if working tree is dirty
            status = subprocess.check_output(
                ["git", "status", "--porcelain"],
                stderr=subprocess.DEVNULL,
                text=True,
            ).strip()
            is_dirty = len(status) > 0

            # Extract owner/repo from URL
            owner_repo = ""
            if "github.com" in remote_url:
                match = re.search(r"github\.com[:/](.+?)(?:\.git)?$", remote_url)
                if match:
                    owner_repo = match.group(1)

            return {
                "remote_url": remote_url,
                "owner_repo": owner_repo,
                "branch": branch,
                "commit_hash": commit_hash,
                "is_dirty": is_dirty,
            }
        except subprocess.CalledProcessError:
            return {
                "remote_url": "",
                "owner_repo": "",
                "branch": "main",
                "commit_hash": "",
                "is_dirty": False,
            }

    def _get_file_language(self, file_path: Path) -> str:
        """Determine file language from extension."""
        extension = file_path.suffix.lower()
        return LANGUAGE_MAP.get(extension, "Unknown")

    def _is_binary(self, file_path: Path) -> bool:
        """Check if file is binary."""
        if file_path.suffix.lower() in BINARY_EXTENSIONS:
            return True
        try:
            with open(file_path, "rb") as f:
                chunk = f.read(8192)
                return b"\x00" in chunk
        except Exception:
            return True

    def _calculate_sha256(self, file_path: Path) -> str:
        """Calculate SHA256 hash of file."""
        sha256_hash = hashlib.sha256()
        try:
            with open(file_path, "rb") as f:
                for byte_block in iter(lambda: f.read(4096), b""):
                    sha256_hash.update(byte_block)
            return sha256_hash.hexdigest()
        except Exception:
            return ""

    def _count_lines(self, file_path: Path) -> int:
        """Count lines in text file."""
        try:
            with open(file_path, "r", encoding="utf-8", errors="ignore") as f:
                return sum(1 for _ in f)
        except Exception:
            return 0

    def _detect_encoding(self, file_path: Path) -> str:
        """Detect file encoding."""
        try:
            with open(file_path, "rb") as f:
                raw = f.read(4096)
                if raw.startswith(b"\xef\xbb\xbf"):
                    return "utf-8-sig"
                elif raw.startswith(b"\xff\xfe") or raw.startswith(b"\xfe\xff"):
                    return "utf-16"
                else:
                    # Try to decode as UTF-8
                    raw.decode("utf-8")
                    return "utf-8"
        except Exception:
            return "unknown"

    def _categorize_file(self, file_path: Path) -> str:
        """Categorize file based on path and extension."""
        path_str = str(file_path).replace("\\", "/")

        # Check custom categories from config
        if "custom_categories" in self.config:
            for custom_cat in self.config["custom_categories"]:
                for pattern in custom_cat.get("patterns", []):
                    if Path(path_str).match(pattern):
                        return custom_cat["name"]

        # Default categorization
        if path_str.startswith("src/") or "WileyWidget." in path_str:
            return "source_code"
        elif path_str.startswith("tests/") or path_str.startswith("test/"):
            return "test"
        elif path_str.startswith("docs/") or file_path.name in [
            "README.md",
            "CHANGELOG.md",
            "CONTRIBUTING.md",
        ]:
            return "documentation"
        elif path_str.startswith("scripts/") or path_str.startswith(".github/"):
            return "automation"
        elif file_path.suffix in [
            ".json",
            ".yaml",
            ".yml",
            ".config",
            ".csproj",
            ".sln",
            ".props",
            ".targets",
        ]:
            return "configuration"
        else:
            return "unknown"

    def _determine_importance(self, file_path: Path, category: str) -> str:
        """Determine file importance."""
        name = file_path.name.lower()
        path_str = str(file_path).lower()

        # High importance files
        if name in [
            "readme.md",
            "changelog.md",
            "contributing.md",
            "license",
            "security.md",
        ]:
            return "high"
        elif ".ai-manifest-config.json" in path_str:
            return "high"
        elif "app.xaml" in path_str or "app.cs" in path_str:
            return "high"
        elif category == "configuration" and file_path.suffix in [".csproj", ".sln"]:
            return "high"
        elif category == "test":
            return "low"
        else:
            return "normal"

    def _get_file_description(
        self, file_path: Path, category: str, language: str
    ) -> str:
        """Generate file description."""
        name = file_path.name

        # Special files
        if name == "README.md":
            return "Main project documentation with overview, installation, and usage instructions"
        elif name == "CHANGELOG.md":
            return "Project version history and changes"
        elif name == "CONTRIBUTING.md":
            return "Guidelines for contributing to the project"
        elif name == "LICENSE":
            return "Project license information"
        elif name.endswith(".csproj"):
            if "WileyWidget.csproj" == name:
                return "C# project file with build configuration"
            return "C# Project file containing project code/data"
        elif name.endswith(".sln"):
            return "Visual Studio solution file"
        elif name == "package.json":
            return "Node.js package configuration and dependencies"
        elif name == "pyproject.toml":
            return "Python project configuration and dependencies"

        # Generic descriptions
        if language != "Unknown":
            return f"{language} file containing project code/data"
        else:
            return f"{category.replace('_', ' ').title()} file"

    def _extract_dependencies(self, file_path: Path, content: str) -> List[str]:
        """Extract dependencies from file content."""
        dependencies = set()

        # C# dependencies
        if file_path.suffix == ".cs":
            # Look for using statements
            if "using Prism" in content:
                dependencies.add("Prism")
            if "using Syncfusion" in content:
                dependencies.add("Syncfusion.WPF")
            if "System.Windows" in content:
                dependencies.add("WPF")

        # XAML dependencies
        elif file_path.suffix == ".xaml":
            if "prism:" in content.lower():
                dependencies.add("Prism")
            if "syncfusion:" in content.lower():
                dependencies.add("Syncfusion.WPF")
            if "WileyWidget.Behaviors" in content:
                dependencies.add("Behaviors")

        # Python dependencies
        elif file_path.suffix == ".py":
            imports = re.findall(r"^import\s+(\w+)", content, re.MULTILINE)
            from_imports = re.findall(r"^from\s+(\w+)", content, re.MULTILINE)
            dependencies.update(imports)
            dependencies.update(from_imports)

        return sorted(list(dependencies))

    def _find_related_files(self, file_path: Path) -> List[str]:
        """Find related files based on conventions."""
        related = []
        path_str = str(file_path)

        # XAML view to code-behind and ViewModel
        if file_path.suffix == ".xaml" and not path_str.endswith("App.xaml"):
            # Code-behind
            code_behind = file_path.with_suffix(".xaml.cs")
            if code_behind.exists():
                related.append(
                    str(code_behind.relative_to(self.root_path)).replace("\\", "/")
                )

            # ViewModel (e.g., BudgetView.xaml -> BudgetViewModel.cs)
            view_name = file_path.stem
            if view_name.endswith("View"):
                vm_name = view_name.replace("View", "ViewModel") + ".cs"
                # Search in common ViewModel directories
                for vm_dir in ["ViewModels", "ViewModel", "."]:
                    vm_path = file_path.parent / vm_dir / vm_name
                    if vm_path.exists():
                        related.append(
                            str(vm_path.relative_to(self.root_path)).replace("\\", "/")
                        )
                        break

        # Code-behind to XAML
        elif file_path.suffix == ".cs" and path_str.endswith(".xaml.cs"):
            xaml_file = file_path.with_suffix("").with_suffix(".xaml")
            if xaml_file.exists():
                related.append(
                    str(xaml_file.relative_to(self.root_path)).replace("\\", "/")
                )

        return related

    def _generate_summary(
        self, file_path: Path, is_binary: bool, max_length: int = 1500
    ) -> str:
        """Generate file content summary."""
        if is_binary or file_path.stat().st_size > self.config.get(
            "max_file_size_for_summary", 200000
        ):
            return "Binary or large file - content not summarized"

        try:
            with open(file_path, "r", encoding="utf-8", errors="ignore") as f:
                content = f.read(max_length * 2)

            # Extract meaningful content
            if file_path.suffix == ".md":
                # Extract headers
                headers = re.findall(r"^#+\s+(.+)$", content, re.MULTILINE)
                return "\n".join(headers[:10]) if headers else content[:max_length]

            elif file_path.suffix in [".cs", ".py", ".js"]:
                # Extract class/function definitions
                lines = content.split("\n")
                important_lines = []
                for line in lines[:50]:
                    stripped = line.strip()
                    if any(
                        keyword in stripped
                        for keyword in [
                            "class ",
                            "def ",
                            "function ",
                            "interface ",
                            "namespace ",
                        ]
                    ):
                        important_lines.append(stripped)
                return (
                    "\n".join(important_lines[:10])
                    if important_lines
                    else content[:max_length]
                )

            elif file_path.suffix == ".json":
                # Try to parse JSON and show structure
                try:
                    data = json.loads(content)
                    if isinstance(data, dict):
                        return f"JSON object with keys: {', '.join(list(data.keys())[:20])}"
                    elif isinstance(data, list):
                        return f"JSON array with {len(data)} items"
                except Exception:
                    return "Invalid JSON or unable to parse"

            return content[:max_length]

        except Exception as e:
            return f"Unable to generate summary: {str(e)}"

    def _generate_github_urls(self, relative_path: str) -> Dict:
        """Generate GitHub URLs for file."""
        if not self.git_info["owner_repo"]:
            return {"blob_url": "", "raw_url": "", "history_url": ""}

        base_url = f"https://github.com/{self.git_info['owner_repo']}"
        branch = self.git_info["branch"]
        file_path = relative_path.replace("\\", "/")

        return {
            "blob_url": f"{base_url}/blob/{branch}/{file_path}",
            "raw_url": f"{base_url}/raw/{branch}/{file_path}",
            "history_url": f"{base_url}/commits/{branch}/{file_path}",
        }

    def _collect_files(self) -> List[Path]:
        """Collect all files in repository."""
        all_files = []
        for root, dirs, files in os.walk(self.root_path):
            # Filter out excluded directories
            dirs[:] = [
                d for d in dirs if not self._should_exclude(os.path.join(root, d))
            ]

            for file in files:
                file_path = Path(root) / file
                relative = file_path.relative_to(self.root_path)
                if not self._should_exclude(str(relative)):
                    all_files.append(file_path)

        return sorted(all_files)

    def _build_search_index(self, files_data: List[Dict]) -> List[str]:
        """Build search index from file paths and descriptions."""
        keywords = set()

        for file_data in files_data:
            path = file_data["metadata"]["path"]
            description = file_data["context"]["description"]

            # Extract keywords from path
            path_parts = (
                path.replace("\\", "/").replace("-", " ").replace("_", " ").lower()
            )
            for part in re.findall(r"[a-z0-9]+", path_parts):
                if len(part) > 2:  # Skip very short words
                    keywords.add(part)

            # Extract keywords from description
            desc_words = re.findall(r"[a-z0-9]+", description.lower())
            for word in desc_words:
                if len(word) > 3:  # Skip short words
                    keywords.add(word)

        # Limit to reasonable size
        return sorted(list(keywords))[:500]

    def analyze_file(self, file_path: Path) -> Dict:
        """Analyze a single file and return metadata."""
        relative_path = str(file_path.relative_to(self.root_path)).replace("\\", "/")
        stat = file_path.stat()
        is_binary = self._is_binary(file_path)
        language = self._get_file_language(file_path)
        category = self._categorize_file(file_path)
        importance = self._determine_importance(file_path, category)

        # Read content if not binary
        content = ""
        if not is_binary:
            try:
                with open(file_path, "r", encoding="utf-8", errors="ignore") as f:
                    content = f.read()
            except Exception:
                pass

        dependencies = self._extract_dependencies(file_path, content)
        related_files = self._find_related_files(file_path)
        summary = self._generate_summary(
            file_path, is_binary, self.config.get("max_summary_length", 1500)
        )

        return {
            "metadata": {
                "path": relative_path,
                "exists": True,
                "size": stat.st_size,
                "last_modified": datetime.fromtimestamp(stat.st_mtime).isoformat(),
                "extension": file_path.suffix,
                "language": language,
                "mime_type": mimetypes.guess_type(str(file_path))[0],
                "line_count": self._count_lines(file_path) if not is_binary else 0,
                "encoding": (
                    self._detect_encoding(file_path) if not is_binary else "binary"
                ),
                "is_binary": is_binary,
                "sha256": self._calculate_sha256(file_path),
                "git": {
                    "last_commit": None,
                    "last_commit_hash": None,
                    "last_commit_author": None,
                    "last_commit_date": None,
                    "last_commit_message": None,
                },
            },
            "urls": self._generate_github_urls(relative_path),
            "context": {
                "category": category,
                "importance": importance,
                "description": self._get_file_description(
                    file_path, category, language
                ),
                "dependencies": dependencies,
                "related_files": related_files,
                "summary": summary,
            },
        }

    def generate_manifest(
        self, output_path: str, filter_categories: Optional[List[str]] = None
    ) -> Dict:
        """Generate complete manifest."""
        print("Collecting files...")
        files = self._collect_files()
        print(f"Found {len(files)} files to process")

        print("Analyzing files...")
        files_data = []
        total_size = 0
        categories_count = {}
        languages_count = {}

        for i, file_path in enumerate(files, 1):
            if i % 50 == 0:
                print(f"  Processed {i}/{len(files)} files...")

            file_data = self.analyze_file(file_path)

            # Filter by category if specified
            if (
                filter_categories
                and file_data["context"]["category"] not in filter_categories
            ):
                continue

            files_data.append(file_data)
            total_size += file_data["metadata"]["size"]

            # Update counts
            category = file_data["context"]["category"]
            categories_count[category] = categories_count.get(category, 0) + 1

            language = file_data["metadata"]["language"]
            languages_count[language] = languages_count.get(language, 0) + 1

        print("Building search index...")
        search_index = self._build_search_index(files_data)

        manifest = {
            "repository": {
                "remote_url": self.git_info["remote_url"],
                "owner_repo": self.git_info["owner_repo"],
                "branch": self.git_info["branch"],
                "commit_hash": self.git_info["commit_hash"],
                "is_dirty": self.git_info["is_dirty"],
                "generated_at": datetime.now().isoformat(),
            },
            "summary": {
                "total_files": len(files_data),
                "total_size": total_size,
                "categories": categories_count,
                "languages": languages_count,
            },
            "search_index": search_index,
            "files": files_data,
        }

        print(f"Writing manifest to {output_path}...")
        with open(output_path, "w", encoding="utf-8") as f:
            json.dump(manifest, f, indent=2)

        print("✅ Manifest generated successfully!")
        print(f"   Files: {len(files_data)}")
        print(f"   Size: {total_size:,} bytes")
        print(f"   Categories: {', '.join(categories_count.keys())}")
        print(f"   Search keywords: {len(search_index)}")

        return manifest


def main():
    parser = argparse.ArgumentParser(description="Generate AI-Fetchable Manifest")
    parser.add_argument(
        "-o",
        "--output",
        default=DEFAULT_OUTPUT_FILE,
        help=f"Output file path (default: {DEFAULT_OUTPUT_FILE})",
    )
    parser.add_argument(
        "-c",
        "--categories",
        help="Comma-separated list of categories to include (e.g., source_code,documentation)",
    )
    parser.add_argument(
        "--config",
        default=DEFAULT_CONFIG_FILE,
        help=f"Config file path (default: {DEFAULT_CONFIG_FILE})",
    )

    args = parser.parse_args()

    filter_categories = None
    if args.categories:
        filter_categories = [c.strip() for c in args.categories.split(",")]

    analyzer = RepositoryAnalyzer(args.config)
    analyzer.generate_manifest(args.output, filter_categories)


if __name__ == "__main__":
    main()
