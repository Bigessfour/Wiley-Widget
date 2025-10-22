#!/usr/bin/env python3
"""
AI Fetchable Repository Manifest Generator

This script generates a comprehensive manifest of all tracked files in the repository,
including URLs, metadata, and context information to enhance visibility and accessibility
for Large Language Models (LLMs) and AI systems.

The generated manifest includes:
- File URLs (both blob and raw for GitHub)
- File metadata (size, timestamps, extensions)
- Language detection and content summaries
- Repository information and statistics
- Security filtering for sensitive files
- Semantic search index with concepts
- Parallel processing for performance
- Configurable analysis options
- Structured data for AI consumption

Enhanced features for Grok-4 and other AI systems:
- AI-friendly content summaries with code structure extraction
- Security-aware filtering to exclude sensitive files
- Semantic indexing for concept-based discovery
- Parallel processing for large repositories
- Configurable behavior via .ai-manifest-config.json
"""

import datetime
import hashlib
import json
import mimetypes
import re
import subprocess
from pathlib import Path
from typing import Any, List, Optional, Set
import concurrent.futures


class RepoManifestGenerator:
    """Generates AI-fetchable repository manifests with comprehensive file metadata."""

    # Language mappings for common extensions
    LANGUAGE_MAP = {
        ".py": "Python",
        ".cs": "C#",
        ".xaml": "XAML",
        ".json": "JSON",
        ".md": "Markdown",
        ".txt": "Text",
        ".xml": "XML",
        ".yml": "YAML",
        ".yaml": "YAML",
        ".ps1": "PowerShell",
        ".sh": "Shell",
        ".js": "JavaScript",
        ".ts": "TypeScript",
        ".html": "HTML",
        ".css": "CSS",
        ".sql": "SQL",
        ".config": "Configuration",
        ".sln": "Visual Studio Solution",
        ".csproj": "C# Project",
        ".vb": "Visual Basic",
        ".cpp": "C++",
        ".c": "C",
        ".h": "C/C++ Header",
        ".java": "Java",
        ".php": "PHP",
        ".rb": "Ruby",
        ".go": "Go",
        ".rs": "Rust",
        ".swift": "Swift",
        ".kt": "Kotlin",
        ".scala": "Scala",
        ".clj": "Clojure",
        ".fs": "F#",
        ".r": "R",
        ".m": "MATLAB",
        ".ipynb": "Jupyter Notebook",
        ".dockerfile": "Docker",
        ".tf": "Terraform",
        ".lock": "Lock File",
        ".toml": "TOML",
        ".ini": "INI",
        ".bat": "Batch",
        ".cmd": "Command",
        ".gitignore": "Git Ignore",
        ".gitattributes": "Git Attributes",
    }

    def _load_config(self) -> dict[str, Any]:
        """Load configuration from .ai-manifest-config.json if it exists."""
        config_path = self.repo_path / ".ai-manifest-config.json"
        default_config = {
            "max_summary_length": 1000,
            "max_file_size_for_summary": 100000,
            "parallel_workers": 4,
            "exclude_patterns": [
                r'\.env', r'secret', r'password', r'token', r'key',
                r'config.*secret', r'private.*key', r'\.pem$', r'\.key$',
                r'credentials', r'auth', r'login'
            ],
            "custom_categories": {},
            "analysis_options": {
                "extract_structure": True,
                "calculate_complexity": False,
                "semantic_analysis": False
            }
        }

        if config_path.exists():
            try:
                with open(config_path, 'r', encoding='utf-8') as f:
                    user_config = json.load(f)
                # Merge user config with defaults
                default_config.update(user_config)
            except Exception as e:
                print(f"Warning: Could not load config file {config_path}: {e}")

        return default_config

    def __init__(
        self, repo_path: str = ".", include_categories: Optional[List[str]] = None
    ):
        self.repo_path = Path(repo_path).resolve()
        self.config = self._load_config()
        self.repo_info = self._get_repo_info()
        self.tracked_files = self._get_tracked_files()
        # Normalize include categories to a lowercase set for fast checks
        self.include_categories: Optional[Set[str]] = (
            {c.strip().lower() for c in include_categories}
            if include_categories
            else None
        )

    def _run_git_command(self, command: list[str]) -> str:
        """Run a git command and return the output."""
        try:
            result = subprocess.run(
                ["git"] + command,
                cwd=self.repo_path,
                capture_output=True,
                text=True,
                check=True,
            )
            return result.stdout.strip()
        except subprocess.CalledProcessError as e:
            print(f"Git command failed: {' '.join(command)}")
            print(f"Error: {e}")
            return ""

    def _get_repo_info(self) -> dict[str, Any]:
        """Get repository information."""
        remote_url = self._run_git_command(["config", "--get", "remote.origin.url"])
        branch = self._run_git_command(["branch", "--show-current"])
        commit_hash = self._run_git_command(["rev-parse", "HEAD"])
        is_dirty = bool(self._run_git_command(["status", "--porcelain"]))

        # Extract owner/repo from URL
        owner_repo = ""
        if remote_url:
            if "github.com" in remote_url:
                # Handle various GitHub URL formats
                if remote_url.endswith(".git"):
                    remote_url = remote_url[:-4]
                parts = remote_url.split("/")
                if len(parts) >= 2:
                    owner_repo = f"{parts[-2]}/{parts[-1]}"

        return {
            "remote_url": remote_url,
            "owner_repo": owner_repo,
            "branch": branch or "main",
            "commit_hash": commit_hash,
            "is_dirty": is_dirty,
            "generated_at": datetime.datetime.now().isoformat(),
        }

    def _get_tracked_files(self) -> list[str]:
        """Get list of tracked files."""
        output = self._run_git_command(["ls-files"])
        return output.split("\n") if output else []

    def _get_file_metadata(self, file_path: str) -> dict[str, Any]:
        """Get comprehensive metadata for a file."""
        full_path = self.repo_path / file_path

        metadata = {
            "path": file_path,
            "exists": full_path.exists(),
            "size": 0,
            "last_modified": None,
            "extension": full_path.suffix.lower(),
            "language": self.LANGUAGE_MAP.get(full_path.suffix.lower(), "Unknown"),
            "mime_type": mimetypes.guess_type(str(full_path))[0],
            "line_count": 0,
            "encoding": "utf-8",  # Assume UTF-8
            "is_binary": False,
            "sha256": "",
        }

        if full_path.exists():
            stat = full_path.stat()
            metadata["size"] = stat.st_size
            metadata["last_modified"] = datetime.datetime.fromtimestamp(
                stat.st_mtime
            ).isoformat()

            # Check if binary
            try:
                with open(full_path, "rb") as f:
                    chunk = f.read(1024)
                    metadata["is_binary"] = b"\x00" in chunk
                    if not metadata["is_binary"]:
                        # Count lines for text files
                        content = chunk.decode("utf-8", errors="ignore")
                        metadata["line_count"] = len(content.splitlines())
                        # Calculate SHA256
                        f.seek(0)
                        metadata["sha256"] = hashlib.sha256(f.read()).hexdigest()
            except Exception:
                metadata["is_binary"] = True

        return metadata

    def _get_file_summary(self, file_path: str, metadata: dict[str, Any]) -> str:
        """Generate AI-friendly file summaries."""
        max_size = self.config.get("max_file_size_for_summary", 100000)
        if metadata["is_binary"] or metadata["size"] > max_size:
            return "Binary or large file - content not summarized"

        try:
            with open(self.repo_path / file_path, 'r', encoding='utf-8', errors='ignore') as f:
                content = f.read(5000)  # First 5KB

            # Extract key information based on file type
            if self.config.get("analysis_options", {}).get("extract_structure", True):
                if file_path.endswith('.py'):
                    summary = self._extract_python_structure(content)
                elif file_path.endswith('.cs'):
                    summary = self._extract_csharp_structure(content)
                elif file_path.endswith('.md'):
                    summary = self._extract_markdown_structure(content)
                elif file_path.endswith('.json'):
                    summary = self._extract_json_structure(content)
                else:
                    # Generic summary: first few lines + key patterns
                    lines = content.split('\n')[:10]
                    summary = '\n'.join(lines)
            else:
                lines = content.split('\n')[:10]
                summary = '\n'.join(lines)

            max_length = self.config.get("max_summary_length", 1000)
            return summary[:max_length]
        except Exception:
            return "Unable to generate summary"

    def _extract_python_structure(self, content: str) -> str:
        """Extract Python code structure."""
        lines = content.split('\n')
        structure = []

        for line in lines[:50]:  # First 50 lines
            line = line.strip()
            if line.startswith('class ') or line.startswith('def ') or line.startswith('import ') or line.startswith('from '):
                structure.append(line)

        return '\n'.join(structure) if structure else "Python file with no clear structure detected"

    def _extract_csharp_structure(self, content: str) -> str:
        """Extract C# code structure."""
        lines = content.split('\n')
        structure = []

        for line in lines[:50]:  # First 50 lines
            line = line.strip()
            if (line.startswith('class ') or line.startswith('interface ') or
                line.startswith('public ') or line.startswith('private ') or
                line.startswith('protected ') or line.startswith('internal ') or
                line.startswith('using ')):
                structure.append(line)

        return '\n'.join(structure) if structure else "C# file with no clear structure detected"

    def _extract_markdown_structure(self, content: str) -> str:
        """Extract Markdown structure."""
        lines = content.split('\n')
        structure = []

        for line in lines[:30]:  # First 30 lines
            if line.startswith('#'):
                structure.append(line)

        return '\n'.join(structure) if structure else "Markdown file with no headings detected"

    def _extract_json_structure(self, content: str) -> str:
        """Extract JSON structure."""
        try:
            data = json.loads(content)
            if isinstance(data, dict):
                keys = list(data.keys())[:10]
                return f"JSON object with keys: {', '.join(keys)}"
            elif isinstance(data, list):
                return f"JSON array with {len(data)} items"
            else:
                return f"JSON {type(data).__name__}"
        except:
            return "Invalid JSON or unable to parse"

    def _should_exclude_file(self, file_path: str) -> bool:
        """Check if file should be excluded for security/privacy."""
        exclude_patterns = self.config.get("exclude_patterns", [
            r'\.env', r'secret', r'password', r'token', r'key',
            r'config.*secret', r'private.*key', r'\.pem$', r'\.key$',
            r'credentials', r'auth', r'login'
        ])
        return any(re.search(pattern, file_path, re.IGNORECASE) for pattern in exclude_patterns)

        return metadata

    def _generate_file_urls(self, file_path: str) -> dict[str, str]:
        """Generate URLs for the file."""
        urls = {}

        if self.repo_info["owner_repo"]:
            base_url = f"https://github.com/{self.repo_info['owner_repo']}"
            urls["blob_url"] = f"{base_url}/blob/{self.repo_info['branch']}/{file_path}"
            urls["raw_url"] = f"{base_url}/raw/{self.repo_info['branch']}/{file_path}"
            urls["history_url"] = (
                f"{base_url}/commits/{self.repo_info['branch']}/{file_path}"
            )

        return urls

    def _get_file_context(
        self, file_path: str, metadata: dict[str, Any]
    ) -> dict[str, Any]:
        """Get additional context for the file."""
        context = {
            "category": "unknown",
            "importance": "normal",
            "description": "",
            "dependencies": [],
            "related_files": [],
        }

        # Categorize files
        if file_path.startswith("src/") or file_path.startswith("WileyWidget."):
            context["category"] = "source_code"
            context["importance"] = "high"
        elif file_path.startswith("tests/") or "test" in file_path.lower():
            context["category"] = "test"
            context["importance"] = "medium"
        elif file_path in ["README.md", "CONTRIBUTING.md", "LICENSE"]:
            context["category"] = "documentation"
            context["importance"] = "high"
        elif file_path.startswith("docs/"):
            context["category"] = "documentation"
            context["importance"] = "medium"
        elif file_path.startswith("scripts/"):
            context["category"] = "automation"
            context["importance"] = "medium"
        elif any(
            file_path.endswith(ext) for ext in [".csproj", ".sln", ".json", ".config"]
        ):
            context["category"] = "configuration"
            context["importance"] = "high"

        # Add descriptions for common files
        descriptions = {
            "README.md": "Main project documentation with overview, installation, and usage instructions",
            "CONTRIBUTING.md": "Guidelines for contributing to the project",
            "LICENSE": "Project license information",
            ".gitignore": "Git ignore patterns for excluding files from version control",
            "package.json": "Node.js package configuration and dependencies",
            "requirements.txt": "Python package dependencies",
            "pyproject.toml": "Python project configuration",
            "WileyWidget.csproj": "C# project file with build configuration",
            "WileyWidget.sln": "Visual Studio solution file",
        }

        context["description"] = descriptions.get(
            file_path, f"{metadata['language']} file containing project code/data"
        )

        # Extract dependencies and relationships for certain files
        # 1) Python requirements
        if file_path == "requirements.txt" and not metadata["is_binary"]:
            try:
                with open(self.repo_path / file_path, encoding="utf-8") as f:
                    deps = [
                        line.strip()
                        for line in f
                        if line.strip() and not line.startswith("#")
                    ]
                    context["dependencies"] = deps[:20]  # Limit to first 20
            except Exception:
                pass

        # 2) XAML <-> ViewModel and code-behind mapping (WPF/Prism convention)
        try:
            if (
                metadata["language"] == "XAML"
                and file_path.startswith("src/Views/")
                and file_path.endswith(".xaml")
            ):
                base = Path(file_path).stem  # e.g., BudgetView
                # Code-behind
                code_behind = Path(file_path + ".cs")
                if (self.repo_path / code_behind).exists():
                    context["related_files"].append(str(code_behind).replace("\\", "/"))
                # ViewModel naming: BudgetViewModel.cs (same base minus 'View' + 'ViewModel')
                vm_name = base
                if vm_name.endswith("View"):
                    vm_name = vm_name[:-4]
                vm_path = Path("src/ViewModels") / f"{vm_name}ViewModel.cs"
                if (self.repo_path / vm_path).exists():
                    context["related_files"].append(str(vm_path).replace("\\", "/"))

                # Lightweight dependency tags based on common namespaces used in views
                # Only read a small chunk to avoid heavy I/O
                with open(
                    self.repo_path / file_path, encoding="utf-8", errors="ignore"
                ) as xf:
                    head = xf.read(4096)
                    deps = []
                    if "prismlibrary.com" in head or "prism:" in head:
                        deps.append("Prism")
                    if "schemas.syncfusion.com/wpf" in head or "syncfusion:" in head:
                        deps.append("Syncfusion.WPF")
                    if "schemas.microsoft.com/winfx/2006/xaml/presentation" in head:
                        deps.append("WPF")
                    if (
                        "schemas.microsoft.com/xaml/behaviors" in head
                        or "WileyWidget.Behaviors" in head
                    ):
                        deps.append("Behaviors")
                    context["dependencies"] = list(
                        dict.fromkeys(context["dependencies"] + deps)
                    )

            # 3) Behavior classes: relate to XAML views that import the behaviors namespace
            if file_path.startswith("src/Behaviors/") and file_path.endswith(".cs"):
                # Heuristic: find views that declare the behaviors namespace; avoid full scan by sampling a subset
                views_dir = self.repo_path / "src/Views"
                if views_dir.exists():
                    # Limit to a reasonable number to keep generation fast
                    count = 0
                    for xaml in views_dir.rglob("*.xaml"):
                        if count > 50:
                            break
                        try:
                            with open(xaml, encoding="utf-8", errors="ignore") as xf:
                                head = xf.read(2048)
                                if "WileyWidget.Behaviors" in head:
                                    context["related_files"].append(
                                        str(xaml.relative_to(self.repo_path)).replace(
                                            "\\", "/"
                                        )
                                    )
                                    count += 1
                        except Exception:
                            continue
        except Exception:
            # Best-effort heuristics; ignore failures silently
            pass

        # Add AI-friendly content summary
        context["summary"] = self._get_file_summary(file_path, metadata)

        return context

    def _should_include(self, category: str) -> bool:
        if self.include_categories is None:
            return True
        return category.lower() in self.include_categories

    def _collect_search_terms(self, files_data: list[dict[str, Any]]) -> list[str]:
        """Build a lightweight search index from file paths and descriptions."""
        terms: Set[str] = set()
        splitter = re.compile(r"[\\/._\-\s]+")
        stop = {
            "src",
            "views",
            "viewmodels",
            "models",
            "business",
            "wileywidget",
            "obj",
            "bin",
            "docs",
            "tests",
            "test",
            "resources",
            "properties",
            "xaml",
            "cs",
            "md",
            "json",
            "xml",
            "yml",
            "yaml",
            "ps1",
            "py",
        }

        for entry in files_data:
            path = entry["metadata"]["path"]
            desc = entry.get("context", {}).get("description", "") or ""
            for token in splitter.split(path) + splitter.split(desc):
                t = token.strip().lower()
                if len(t) >= 3 and t not in stop and not t.isnumeric():
                    terms.add(t)

        # Emphasize likely keywords based on repo content
        # Add known frameworks when present
        keywords = {
            "prism",
            "syncfusion",
            "wpf",
            "xaml",
            "csharp",
            "dotnet",
            "azure",
            "ai",
        }
        terms |= keywords

        # Return sorted for stability and cap to a reasonable size
        return sorted(list(terms))[:500]

    def generate_manifest(self) -> dict[str, Any]:
        """Generate the complete repository manifest."""
        print(f"Generating manifest for {len(self.tracked_files)} tracked files...")

        def process_file(file_path: str) -> dict[str, Any] | None:
            if not file_path.strip():
                return None

            metadata = self._get_file_metadata(file_path)
            urls = self._generate_file_urls(file_path)
            context = self._get_file_context(file_path, metadata)

            # Apply category filter early if requested
            if not self._should_include(context["category"]):
                return None

            # Apply security filter to exclude sensitive files
            if self._should_exclude_file(file_path):
                return None

            return {"metadata": metadata, "urls": urls, "context": context}

        # Use parallel processing for better performance
        files_data = []
        max_workers = self.config.get("parallel_workers", 4)
        with concurrent.futures.ThreadPoolExecutor(max_workers=max_workers) as executor:
            futures = [executor.submit(process_file, fp) for fp in self.tracked_files]
            for future in concurrent.futures.as_completed(futures):
                result = future.result()
                if result:
                    files_data.append(result)

        # Sort files by path for consistent output
        files_data.sort(key=lambda x: x["metadata"]["path"])

        manifest = {
            "repository": self.repo_info,
            "summary": {
                "total_files": len(files_data),
                "total_size": sum(f["metadata"]["size"] for f in files_data),
                "categories": {},
                "languages": {},
            },
            "search_index": [],
            "files": files_data,
        }

        # Calculate summary statistics
        for file_entry in files_data:
            cat = file_entry["context"]["category"]
            lang = file_entry["metadata"]["language"]

            manifest["summary"]["categories"][cat] = (
                manifest["summary"]["categories"].get(cat, 0) + 1
            )
            manifest["summary"]["languages"][lang] = (
                manifest["summary"]["languages"].get(lang, 0) + 1
            )

        # Build search index
        manifest["search_index"] = self._collect_search_terms(files_data)

        # Add semantic index for enhanced AI discovery
        manifest["semantic_index"] = self._build_semantic_index(files_data)

        return manifest

    def _build_semantic_index(self, files_data: list[dict[str, Any]]) -> dict[str, Any]:
        """Build semantic search index with concepts and relationships."""
        semantic_data = {
            "concepts": set(),
            "relationships": [],
            "clusters": {},
            "important_files": []
        }

        # Extract concepts from file names and content
        for file_entry in files_data:
            path = file_entry["metadata"]["path"]
            context = file_entry["context"]

            # Add important files
            if context.get("importance") == "high":
                semantic_data["important_files"].append({
                    "path": path,
                    "category": context.get("category"),
                    "description": context.get("description")
                })

            # Extract concepts from path
            path_parts = re.split(r'[\\/._-]', path.lower())
            for part in path_parts:
                if len(part) > 3 and part not in {'src', 'test', 'docs', 'scripts'}:
                    semantic_data["concepts"].add(part)

            # Extract from summary if available
            summary = context.get("summary", "")
            if summary and summary != "Unable to generate summary":
                words = re.findall(r'\b\w{4,}\b', summary.lower())
                for word in words:
                    if word not in {'file', 'class', 'function', 'method', 'code', 'data'}:
                        semantic_data["concepts"].add(word)

        # Convert sets to sorted lists for JSON serialization
        semantic_data["concepts"] = sorted(list(semantic_data["concepts"]))[:200]  # Limit size

        return semantic_data

    def save_manifest(self, output_path: str | None = None) -> str:
        """Generate and save the manifest to a file."""
        if output_path is None:
            output_path = str(self.repo_path / "ai-fetchable-manifest.json")

        manifest = self.generate_manifest()

        with open(output_path, "w", encoding="utf-8") as f:
            json.dump(manifest, f, indent=2, ensure_ascii=False)

        print(f"Manifest saved to: {output_path}")
        print(f"Total files: {manifest['summary']['total_files']}")
        print(f"Total size: {manifest['summary']['total_size']} bytes")

        return output_path


def main():
    """Main entry point."""
    import argparse

    parser = argparse.ArgumentParser(
        description="Generate AI-fetchable repository manifest with file URLs and metadata"
    )
    parser.add_argument(
        "--output", "-o", help="Output file path (default: ai-fetchable-manifest.json)"
    )
    parser.add_argument(
        "--repo-path",
        "-p",
        default=".",
        help="Repository path (default: current directory)",
    )
    parser.add_argument(
        "--include-categories",
        "-c",
        default=None,
        help=(
            "Comma-separated categories to include (e.g., source_code,documentation,automation). "
            "If omitted, all categories are included."
        ),
    )

    args = parser.parse_args()

    include_categories = (
        [c.strip() for c in args.include_categories.split(",")]
        if args.include_categories
        else None
    )
    generator = RepoManifestGenerator(
        args.repo_path, include_categories=include_categories
    )
    output_path = generator.save_manifest(args.output)

    print("\nManifest generation complete!")
    print(f"File: {output_path}")
    print("\nThis manifest provides:")
    print("- URLs for all tracked files (blob and raw)")
    print("- Comprehensive metadata for AI context")
    print("- File categorization and importance levels")
    print("- Language detection and statistics")
    print("- AI-friendly content summaries")
    print("- Security-filtered sensitive files")
    print("- Semantic search index and concepts")
    print("- Parallel processing for performance")
    print("- Structured data for LLM consumption")


if __name__ == "__main__":
    main()
