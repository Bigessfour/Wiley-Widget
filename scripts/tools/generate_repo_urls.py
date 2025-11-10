#!/usr/bin/env python3
"""
Generate AI-Fetchable Manifest (ai-fetchable-manifest.json)

Enhanced repository intelligence manifest with:
- File metadata (size, timestamps, hashes, languages)
- GitHub URLs (blob, raw, history)
- Advanced categorization with sub-categories
- Search index with weighted keywords
- Related files and dependency graphs
- Code structure analysis (classes, methods, namespaces)
- Build and test status integration
- License and compliance tracking
- Recent commit history per file
"""

import argparse
import hashlib
import json
import mimetypes
import os
import re
import shutil
import subprocess
import time
from concurrent.futures import ThreadPoolExecutor, as_completed
from datetime import datetime, timedelta
from pathlib import Path
from typing import Dict, List, Optional

# Configuration defaults
DEFAULT_CONFIG_FILE = ".ai-manifest-config.json"
DEFAULT_OUTPUT_FILE = "ai-fetchable-manifest.json"
KNOWN_CATEGORIES = {
    "source_code", "test", "documentation", "configuration",
    "automation", "assets", "unknown"
}

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
    ".csx": "C# Script",
    ".resx": "Resource File",
    ".rdl": "Report Definition",
    ".gitignore": "Git Configuration",
    ".gitattributes": "Git Configuration",
    ".editorconfig": "Editor Configuration",
    ".prettierrc": "Prettier Configuration",
    ".prettierignore": "Prettier Configuration",
    ".eslintrc": "ESLint Configuration",
    ".dockerignore": "Docker Configuration",
    "dockerfile": "Docker",
    ".lock": "Lock File",
    ".svg": "SVG",
    ".props": "MSBuild Properties",
    ".targets": "MSBuild Targets",
    ".snk": "Strong Name Key",
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
    ".pdf",  # Syncfusion reports
    ".woff",
    ".woff2",
    ".ttf",
    ".eot",
    ".otf",
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
    r"\.tmp\.driveupload/",  # Exclude temp drive upload directory
    r"\.mypy_cache/",  # Exclude mypy cache
    r"\.ruff_cache/",  # Exclude ruff cache
    r"test-logs/",  # Exclude test logs
    r"logs/",  # Exclude logs directory
    r"ai-fetchable-manifest\.json$",  # Exclude the manifest itself
    r"\.dll$",
    r"\.exe$",
    r"\.pdb$",
    r"\.cache$",
    r"\.suo$",
    r"\.user$",
    r"Thumbs\.db$",
    r"Desktop\.ini$",
    r"\.DS_Store$",
    # Security exclusions - sensitive files
    r"\.env$",  # Environment files
    r"\.env\.example$",  # Environment example files
    r"\.env\.local$",  # Local environment files
    r"secrets/",  # Secrets directory
    r"signing/",  # Signing keys directory
    r"licenses/",  # License keys directory
    r"\.pem$",  # PEM files (certificates/keys)
    r"\.key$",  # Key files
    r"\.p12$",  # PKCS#12 files
    r"\.pfx$",  # PFX files
    r"config/app\.config$",  # App config with secrets
    r"appsettings\.json$",  # App settings with secrets
    r"\.gitleaks\.toml$",  # Gitleaks config (may contain patterns)
]


class RepositoryAnalyzer:
    """Analyzes repository and generates AI-fetchable manifest."""

    def __init__(self, config_path: Optional[str] = None):
        self.root_path = Path.cwd()
        self.config = self._load_config(config_path)
        self.exclude_patterns = self._compile_exclude_patterns()
        # Cache git executable path to avoid repeated lookups
        self.git_exe = shutil.which("git")
        self.git_info = self._get_git_info()
        # Set parallel workers based on CPU count
        cpu_count = os.cpu_count() or 4
        self.max_workers = self.config.get("parallel_workers", max(2, cpu_count // 2))

    def _load_config(self, config_path: Optional[str]) -> Dict:
        """Load configuration from JSON file."""
        config_file = Path(config_path or DEFAULT_CONFIG_FILE)
        if config_file.exists():
            try:
                with open(config_file, "r", encoding="utf-8") as f:
                    config = json.load(f)
                    # Basic JSON schema validation
                    if not isinstance(config, dict):
                        print("Warning: Config must be a JSON object, using defaults")
                        return {}
                    return config
            except json.JSONDecodeError as e:
                print(f"Warning: Invalid JSON in config file: {e}")
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

        # Regular exclusion patterns (apply to all paths)
        for pattern in self.exclude_patterns:
            if pattern.search(normalized):
                return True

        # Focus mode: only include files with specific extensions
        # CRITICAL: Only filter ACTUAL FILES, not directories that happen to have dots in their names
        if self.config.get("focus_mode", False):
            include_exts = self.config.get("include_only_extensions", [])
            if include_exts:
                path_obj = Path(path)
                # Only filter if it's an actual file (exists and is not a directory)
                if path_obj.exists() and path_obj.is_file():
                    file_ext = path_obj.suffix.lower()
                    if file_ext not in include_exts:
                        return True

        return False

    def _cleanup_temp_files(self) -> None:
        """Delete temporary files and directories before generating manifest."""
        temp_dirs = [
            ".tmp.driveupload",
            ".mypy_cache",
            ".ruff_cache",
            "test-logs",
            "__pycache__",
            ".pytest_cache",
            "htmlcov",
        ]

        temp_files = [
            "ai-fetchable-manifest.json",  # Old manifest
        ]

        deleted_dirs = []
        deleted_files = []

        # Delete temporary directories
        for temp_dir in temp_dirs:
            dir_path = self.root_path / temp_dir
            if dir_path.exists() and dir_path.is_dir():
                try:
                    shutil.rmtree(dir_path)
                    deleted_dirs.append(temp_dir)
                    print(f"  Deleted temp directory: {temp_dir}")
                except Exception as e:
                    print(f"  Warning: Could not delete {temp_dir}: {e}")

        # Delete temporary files
        for temp_file in temp_files:
            file_path = self.root_path / temp_file
            if file_path.exists() and file_path.is_file():
                try:
                    file_path.unlink()
                    deleted_files.append(temp_file)
                    print(f"  Deleted temp file: {temp_file}")
                except Exception as e:
                    print(f"  Warning: Could not delete {temp_file}: {e}")

        if deleted_dirs or deleted_files:
            print(
                f"Cleanup complete: {len(deleted_dirs)} directories, {len(deleted_files)} files removed"
            )
        else:
            print("No temp files to clean up")

    def _get_git_info(self) -> Dict:
        """Get git repository information."""
        if not self.git_exe:
            # Git not available on PATH - return safe defaults
            return {
                "remote_url": "",
                "owner_repo": "",
                "branch": "main",
                "commit_hash": "",
                "is_dirty": False,
                "git_available": False,
            }

        def run_git(args: List[str]) -> str:
            # Run git with explicit executable path and without a shell to reduce injection risk (B603/B404).
            # Note: self.git_exe is guaranteed to be non-None when this function is called
            try:
                assert self.git_exe is not None  # Type narrowing for mypy/pyright
                completed = subprocess.run(
                    [self.git_exe] + args, check=True, capture_output=True, text=True
                )
                return completed.stdout.strip()
            except subprocess.CalledProcessError:
                return ""

        # Safe, explicit git calls
        remote_url = run_git(["config", "--get", "remote.origin.url"]) or ""
        branch = run_git(["rev-parse", "--abbrev-ref", "HEAD"]) or "main"
        commit_hash = run_git(["rev-parse", "HEAD"]) or ""
        status = run_git(["status", "--porcelain"]) or ""
        is_dirty = len(status) > 0

        # Extract owner/repo from URL
        owner_repo = ""
        if remote_url and "github.com" in remote_url:
            match = re.search(r"github\.com[:/](.+?)(?:\.git)?$", remote_url)
            if match:
                owner_repo = match.group(1)

        return {
            "remote_url": remote_url,
            "owner_repo": owner_repo,
            "branch": branch,
            "commit_hash": commit_hash,
            "is_dirty": is_dirty,
            "git_available": True,
        }

    def _get_file_git_history(self, relative_path: str, max_commits: int = 5) -> List[Dict]:
        """Get recent git commit history for a file."""
        if not self.git_info.get("git_available", False) or not self.git_exe:
            return []

        try:
            # Get last N commits for the file with format: hash|author|date|message
            result = subprocess.run(
                [
                    self.git_exe,
                    "log",
                    f"-{max_commits}",
                    "--pretty=format:%H|%an|%ai|%s",
                    "--",
                    relative_path,
                ],
                check=True,
                capture_output=True,
                text=True,
                cwd=self.root_path,
            )

            commits = []
            for line in result.stdout.strip().split("\n"):
                if not line:
                    continue
                parts = line.split("|", 3)
                if len(parts) == 4:
                    commits.append({
                        "hash": parts[0],
                        "author": parts[1],
                        "date": parts[2],
                        "message": parts[3],
                    })

            return commits
        except subprocess.CalledProcessError:
            return []

    def _get_file_language(self, file_path: Path) -> str:
        """Determine file language from extension or filename."""
        # Check lowercase filename for special cases
        filename_lower = file_path.name.lower()

        # Special filenames without extensions
        if filename_lower in ["dockerfile", "makefile", "rakefile", "gemfile", "guardfile"]:
            return "Docker" if filename_lower == "dockerfile" else "Ruby"
        elif filename_lower in ["cmakelists.txt"]:
            return "CMake"
        elif filename_lower.startswith(".env"):
            return "Environment"

        # Extension-based detection
        extension = file_path.suffix.lower()
        if extension in LANGUAGE_MAP:
            return LANGUAGE_MAP[extension]

        # Try parent directory context for unknown extensions
        parent_name = file_path.parent.name.lower()
        if parent_name in ["scripts", "tools"]:
            # Check if it looks like a script based on content
            try:
                with open(file_path, "r", encoding="utf-8", errors="ignore") as f:
                    first_line = f.readline().strip()
                    if first_line.startswith("#!"):
                        if "python" in first_line:
                            return "Python"
                        elif "bash" in first_line or "sh" in first_line:
                            return "Shell"
                        elif "pwsh" in first_line or "powershell" in first_line:
                            return "PowerShell"
            except:
                pass

        return "Unknown"

    def _is_binary(self, file_path: Path) -> bool:
        """Check if file is binary."""
        if file_path.suffix.lower() in BINARY_EXTENSIONS:
            return True
        try:
            with open(file_path, "rb") as f:
                chunk = f.read(8192)
                return b"\x00" in chunk
        except OSError:
            # If the file can't be read, treat as binary to avoid processing issues
            return True

    def _calculate_sha256(self, file_path: Path) -> str:
        """Calculate SHA256 hash of file."""
        sha256_hash = hashlib.sha256()
        try:
            with open(file_path, "rb") as f:
                for byte_block in iter(lambda: f.read(4096), b""):
                    sha256_hash.update(byte_block)
            return sha256_hash.hexdigest()
        except OSError:
            return ""

    def _count_lines(self, file_path: Path) -> int:
        """Count lines in text file."""
        try:
            with open(file_path, "r", encoding="utf-8", errors="ignore") as f:
                return sum(1 for _ in f)
        except (OSError, UnicodeDecodeError):
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
        except OSError:
            return "unknown"

    def _categorize_file(self, file_path: Path) -> tuple[str, str]:
        """
        Categorize file and determine sub-category.
        Returns: (category, sub_category)
        """
        path_str = str(file_path).lower().replace("\\", "/")
        path_parts = path_str.split("/")
        filename = file_path.name.lower()
        extension = file_path.suffix.lower()

        # Check for migrations first (high priority path-based categorization)
        if "migrations/" in path_str or "migration" in filename:
            if extension == ".sql":
                return "source_code", "data"

        # Configuration files
        if extension in [".json", ".yaml", ".yml", ".toml", ".config", ".xml"] or filename in [
            ".editorconfig",
            ".prettierrc",
            ".eslintrc",
            "package.json",
            "pyproject.toml",
            "global.json",
        ]:
            return "configuration", "build" if "build" in path_str or extension in [".props", ".targets"] else "settings"

        # Documentation
        if extension in [".md", ".txt", ".rst"] or "docs/" in path_str or "documentation/" in path_str:
            sub_cat = "api" if "api" in path_str else "guide" if any(x in path_str for x in ["guide", "tutorial"]) else "general"
            return "documentation", sub_cat

        # CSX (C# Script) files - explicit handling for test infrastructure
        if extension == ".csx":
            if "test" in path_str or "examples" in path_str:
                return "test", "integration"
            return "automation", "scripts"

        # Automation scripts
        if extension in [".ps1", ".psm1", ".psd1", ".py", ".sh", ".bat"]:
            if "test" in path_str:
                return "automation", "test-scripts"
            elif "maintenance" in path_str or "cleanup" in filename:
                return "automation", "maintenance"
            elif "tools" in path_str or "setup" in path_str:
                return "automation", "tools"
            return "automation", "scripts"

        # Test files
        if "test" in path_str or extension == ".test.cs" or filename.endswith("tests.cs"):
            if "unit" in path_str:
                return "test", "unit"
            elif "integration" in path_str:
                return "test", "integration"
            elif "e2e" in path_str or "end-to-end" in path_str:
                return "test", "e2e"
            return "test", "unit"

        # Source code with enhanced sub-categorization
        if extension in [".cs", ".csproj", ".xaml"]:
            # XAML views
            if extension == ".xaml":
                if "app.xaml" in filename:
                    return "source_code", "application"
                return "source_code", "ui"

            # C# files
            if extension == ".cs":
                # ViewModels
                if "viewmodel" in filename:
                    return "source_code", "viewmodel"
                # Views (code-behind)
                if filename.endswith(".xaml.cs"):
                    return "source_code", "ui"
                # Models
                if "model" in path_str and "viewmodel" not in filename:
                    if "migration" in filename or "migrations/" in path_str:
                        return "source_code", "migration"
                    return "source_code", "model"
                # Services
                if "service" in path_str or filename.endswith("service.cs"):
                    if "abstractions" in path_str or filename.startswith("i") and filename.endswith("service.cs"):
                        return "source_code", "interface"
                    return "source_code", "service"
                # Repositories
                if "repository" in filename or "repositories/" in path_str:
                    return "source_code", "data"
                # Data contexts
                if "context" in filename or "dbcontext" in filename:
                    return "source_code", "data"
                # Converters
                if "converter" in filename or "converters/" in path_str:
                    return "source_code", "converter"
                # Behaviors
                if "behavior" in filename or "behaviors/" in path_str:
                    return "source_code", "behavior"
                # Validators
                if "validator" in filename or "validation" in path_str:
                    return "source_code", "validation"
                # Modules (Prism)
                if "module" in filename:
                    return "source_code", "module"
                # Helpers/Utilities
                if "helper" in filename or "utility" in filename or "utils" in path_str:
                    return "source_code", "utility"
                # Configuration
                if "config" in filename or "settings" in filename:
                    return "source_code", "configuration"
                # General business logic
                if "business" in path_str:
                    return "source_code", "business"
                # Facades
                if "facade" in path_str or "facade" in filename:
                    return "source_code", "facade"

            # Project files
            if extension == ".csproj":
                return "source_code", "project"

            # Default for C# files
            return "source_code", "general"

        # SQL files
        if extension == ".sql":
            if "migration" in filename:
                return "source_code", "migration"
            return "source_code", "data"

        # Assets
        if extension in [".png", ".jpg", ".jpeg", ".gif", ".ico", ".svg", ".bmp"]:
            return "assets", "image"

        # Web files
        if extension in [".html", ".css", ".js", ".ts"]:
            return "source_code", "web"

        # Report files
        if extension in [".rdl", ".rdlc"]:
            return "source_code", "report"

        return "unknown", "unclassified"
        """Categorize file based on path and extension. Returns (category, sub_category)."""
        path_str = str(file_path).replace("\\", "/")

        # Check custom categories from config
        if "custom_categories" in self.config:
            for custom_cat in self.config["custom_categories"]:
                for pattern in custom_cat.get("patterns", []):
                    if Path(path_str).match(pattern):
                        return (custom_cat["name"], custom_cat.get("sub_category", ""))

        # Default categorization - CHECK TEST PATTERNS FIRST
        if (
            path_str.startswith("tests/")
            or path_str.startswith("test/")
            or ".Tests/" in path_str
            or ".Test/" in path_str
            or path_str.endswith("Tests.cs")
            or path_str.endswith("Test.cs")
        ):
            return ("test", "unit_test" if "Unit" in path_str else "integration_test" if "Integration" in path_str else "")
        elif path_str.startswith("src/") or "WileyWidget." in path_str:
            # Sub-categorize source code
            if file_path.suffix == ".xaml":
                return ("source_code", "ui")
            elif "ViewModel" in path_str:
                return ("source_code", "viewmodel")
            elif "View" in path_str and file_path.suffix == ".cs":
                return ("source_code", "view")
            elif "Model" in path_str or "/Models/" in path_str:
                return ("source_code", "model")
            elif "Service" in path_str or "/Services/" in path_str:
                return ("source_code", "service")
            elif "Repository" in path_str or "/Repositories/" in path_str:
                return ("source_code", "data")
            elif "Converter" in path_str or "/Converters/" in path_str:
                return ("source_code", "converter")
            elif "Behavior" in path_str or "/Behaviors/" in path_str:
                return ("source_code", "behavior")
            else:
                return ("source_code", "")
        elif path_str.startswith("docs/") or file_path.name in [
            "README.md",
            "CHANGELOG.md",
            "CONTRIBUTING.md",
            "SECURITY.md",
            "LICENSE",
            "LICENSE.txt",
            "LICENSE.md",
        ]:
            return ("documentation", "")
        elif path_str.startswith("scripts/") or file_path.suffix in [".ps1", ".psm1", ".psd1"]:
            return ("automation", "scripts")
        elif path_str.startswith(".github/"):
            return ("automation", "ci_cd")
        elif file_path.suffix in [
            ".json",
            ".yaml",
            ".yml",
            ".config",
            ".csproj",
            ".sln",
            ".props",
            ".targets",
            ".toml",
            ".editorconfig",
        ]:
            return ("configuration", "build" if file_path.suffix in [".csproj", ".sln", ".props", ".targets"] else "")
        elif path_str.startswith("docker/") or file_path.name.lower().startswith("dockerfile"):
            return ("automation", "docker")
        elif path_str.startswith("sql/") or file_path.suffix == ".sql":
            return ("source_code", "data")
        elif file_path.suffix in [".svg", ".png", ".jpg", ".jpeg", ".gif", ".ico"]:
            return ("assets", "image")
        elif path_str.startswith("wwwroot/") or path_str.startswith("public/"):
            return ("assets", "web")
        else:
            return ("unknown", "")

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

        # C# Project file dependencies (NuGet packages)
        if file_path.suffix == ".csproj":
            # Extract PackageReference items
            package_refs = re.findall(
                r'<PackageReference\s+Include="([^"]+)"', content
            )
            dependencies.update(package_refs)

            # Extract ProjectReference items
            project_refs = re.findall(
                r'<ProjectReference\s+Include="([^"]+)"', content
            )
            for ref in project_refs:
                # Extract just the project name without path
                proj_name = Path(ref).stem
                dependencies.add(f"Project:{proj_name}")

        # C# dependencies
        elif file_path.suffix == ".cs":
            # Look for using statements
            if "using Prism" in content:
                dependencies.add("Prism")
            if "using Syncfusion" in content:
                dependencies.add("Syncfusion.WPF")
            if "System.Windows" in content:
                dependencies.add("WPF")
            if "using Microsoft.EntityFrameworkCore" in content:
                dependencies.add("EntityFrameworkCore")
            if "using Moq" in content:
                dependencies.add("Moq")
            if "using Xunit" in content or "using xunit" in content.lower():
                dependencies.add("xUnit")

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

        # PowerShell module dependencies
        elif file_path.suffix in [".ps1", ".psm1"]:
            imports = re.findall(r"Import-Module\s+(\S+)", content)
            dependencies.update(imports)

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

    def _analyze_test_file(self, content: str) -> Dict:
        """Extract test-specific information from C# test files."""
        test_info = {
            "test_methods": [],
            "test_classes": [],
            "mock_setups": [],
            "test_attributes": set(),
            "test_fixtures": [],
        }

        # Split content into lines for analysis
        lines = content.split("\n")

        # Extract test methods by looking for [Fact]/[Theory] followed by method signature
        test_methods = []
        for i, line in enumerate(lines):
            # Check if this line has a test attribute
            if re.search(r"\[\s*(?:Fact|Theory|Test|TestMethod)\s*\]", line):
                # Look ahead for the method signature (within next 10 lines)
                for j in range(i + 1, min(i + 10, len(lines))):
                    method_match = re.search(
                        r"public\s+(?:async\s+)?(?:Task|void)\s+(\w+)\s*\(", lines[j]
                    )
                    if method_match:
                        test_methods.append(method_match.group(1))
                        break

        test_info["test_methods"] = list(
            set(test_methods)
        )  # All unique methods, no limit

        # Extract test class names
        class_pattern = r"public\s+class\s+(\w+Tests?)\b"
        test_classes = re.findall(class_pattern, content)
        test_info["test_classes"] = list(set(test_classes))

        # Extract test fixtures (IDisposable, constructor, etc.)
        fixture_pattern = r"public\s+class\s+(\w+)\s*:\s*IDisposable"
        fixtures = re.findall(fixture_pattern, content)
        test_info["test_fixtures"] = fixtures

        # Extract mock setup patterns - capture more detail
        mock_pattern = r"(_mock\w+|mock\w+)\.Setup\("
        mocks = re.findall(mock_pattern, content)
        test_info["mock_setups"] = list(set(mocks))  # All unique mocks

        # Extract test attributes
        attr_pattern = r"\[(Fact|Theory|Test|TestMethod|TestCase|InlineData)\]"
        attributes = re.findall(attr_pattern, content)
        test_info["test_attributes"] = list(set(attributes))

        return test_info

    def _analyze_csharp_file(self, content: str) -> Dict:
        """Extract comprehensive C# file structure."""
        structure = {
            "namespaces": [],
            "classes": [],
            "interfaces": [],
            "enums": [],
            "public_methods": [],
            "properties": [],
        }

        # Extract namespaces
        namespace_pattern = r"namespace\s+([\w\.]+)"
        structure["namespaces"] = re.findall(namespace_pattern, content)

        # Extract classes
        class_pattern = r"public\s+(?:partial\s+)?(?:static\s+)?class\s+(\w+)"
        structure["classes"] = list(set(re.findall(class_pattern, content)))

        # Extract interfaces
        interface_pattern = r"public\s+interface\s+(I\w+)"
        structure["interfaces"] = list(set(re.findall(interface_pattern, content)))

        # Extract enums
        enum_pattern = r"public\s+enum\s+(\w+)"
        structure["enums"] = list(set(re.findall(enum_pattern, content)))

        # Extract public methods (limit to first 100)
        method_pattern = r"public\s+(?:static\s+)?(?:async\s+)?(?:virtual\s+)?(?:override\s+)?[\w<>]+\s+(\w+)\s*\("
        methods = re.findall(method_pattern, content)
        structure["public_methods"] = list(set(methods))[:100]

        # Extract properties
        prop_pattern = r"public\s+(?:static\s+)?[\w<>?]+\s+(\w+)\s*\{\s*get"
        properties = re.findall(prop_pattern, content)
        structure["properties"] = list(set(properties))[:50]

        return structure

    def _generate_summary(
        self, file_path: Path, is_binary: bool, max_length: int = 1500
    ) -> str:
        """Generate file content summary with enhanced analysis for tests."""
        if is_binary or file_path.stat().st_size > self.config.get(
            "max_file_size_for_summary", 200000
        ):
            return "Binary or large file - content not summarized"

        try:
            # For test files, read more content to capture test methods
            is_test_file = "test" in str(file_path).lower()
            if is_test_file:
                test_length = self.config.get("test_file_summary_length", 50000)
                read_limit = test_length
            else:
                read_limit = max_length * 2

            with open(file_path, "r", encoding="utf-8", errors="ignore") as f:
                content = f.read(read_limit)

            # Provide a short head/tail preview to give immediate context
            head_preview = content[:500].strip()
            tail_preview = content[-500:].strip() if len(content) > 500 else ""

            # Helper to build preview block
            def preview_block(title: str, text: str) -> str:
                if not text:
                    return ""
                lines = text.splitlines()
                # Keep max 20 lines for preview
                excerpt = "\n".join(lines[:20])
                return f"{title}:\n{excerpt}\n"

            # Markdown: return headers or a preview
            if file_path.suffix == ".md":
                headers = re.findall(r"^#+\s+(.+)$", content, re.MULTILINE)
                if headers:
                    return "\n".join(headers[:10])
                return preview_block("Preview (start)", head_preview)

            elif file_path.suffix == ".cs":
                # Enhanced C# analysis, especially for tests
                if is_test_file:
                    test_info = self._analyze_test_file(content)
                    summary_parts = []

                    if test_info["test_classes"]:
                        summary_parts.append(
                            f"Test Classes: {', '.join(test_info['test_classes'])}"
                        )

                    if test_info["test_methods"]:
                        method_count = len(test_info["test_methods"])
                        sample_methods = test_info["test_methods"][:40]
                        summary_parts.append(
                            f"Test Methods ({method_count} total):\n  - "
                            + "\n  - ".join(sample_methods)
                        )

                    if test_info["test_attributes"]:
                        summary_parts.append(
                            f"Test Attributes: {', '.join(test_info['test_attributes'])}"
                        )

                    if test_info["mock_setups"]:
                        mock_names = list(
                            set(
                                [
                                    m.replace(".Setup(", "")
                                    for m in test_info["mock_setups"]
                                ]
                            )
                        )
                        summary_parts.append(
                            f"Mocked Dependencies ({len(mock_names)}): {', '.join(mock_names[:15])}"
                        )

                    if test_info["test_fixtures"]:
                        summary_parts.append(
                            f"Test Fixtures: {', '.join(test_info['test_fixtures'])}"
                        )

                    # Add preview head to help quickly identify failing tests
                    if head_preview:
                        summary_parts.append(
                            preview_block("Preview (start)", head_preview)
                        )
                    return "\n\n".join(summary_parts)

                # Regular C# file analysis
                cs_structure = self._analyze_csharp_file(content)
                summary_parts = []

                if cs_structure["namespaces"]:
                    summary_parts.append(
                        f"Namespace: {', '.join(cs_structure['namespaces'][:3])}"
                    )

                if cs_structure["classes"]:
                    summary_parts.append(
                        f"Classes ({len(cs_structure['classes'])}): {', '.join(cs_structure['classes'][:10])}"
                    )

                if cs_structure["interfaces"]:
                    summary_parts.append(
                        f"Interfaces ({len(cs_structure['interfaces'])}): {', '.join(cs_structure['interfaces'][:10])}"
                    )

                if cs_structure["enums"]:
                    summary_parts.append(
                        f"Enums: {', '.join(cs_structure['enums'][:5])}"
                    )

                if cs_structure["public_methods"]:
                    method_count = len(cs_structure["public_methods"])
                    sample_methods = cs_structure["public_methods"][:20]
                    summary_parts.append(
                        f"Public Methods ({method_count} total): {', '.join(sample_methods)}"
                    )

                if cs_structure["properties"]:
                    summary_parts.append(
                        f"Properties ({len(cs_structure['properties'])}): {', '.join(cs_structure['properties'][:15])}"
                    )

                # Extract using statements and top-level declarations for more context
                usings = re.findall(r"^using\s+[\w\.\<\>]+;", content, re.MULTILINE)
                if usings:
                    summary_parts.append(f"Usings: {', '.join(usings[:12])}")

                # Attach previews
                if head_preview:
                    summary_parts.append(preview_block("Preview (start)", head_preview))
                if tail_preview:
                    summary_parts.append(preview_block("Preview (end)", tail_preview))

                return (
                    "\n\n".join(summary_parts)
                    if summary_parts
                    else content[:max_length]
                )

            elif file_path.suffix in [".py", ".js"]:
                lines = content.split("\n")
                important_lines = []
                for line in lines[:200]:
                    stripped = line.strip()
                    if any(
                        keyword in stripped
                        for keyword in [
                            "def ",
                            "class ",
                            "function ",
                            "async def ",
                            "async function ",
                        ]
                    ):
                        important_lines.append(stripped)
                preview = "\n".join(important_lines[:20])
                if head_preview:
                    preview = (
                        preview
                        + "\n\n"
                        + preview_block("Preview (start)", head_preview)
                    )
                return preview if preview else content[:max_length]

            elif file_path.suffix == ".json":
                try:
                    data = json.loads(content)
                    if isinstance(data, dict):
                        keys = list(data.keys())[:20]
                        return (
                            f"JSON object with keys: {', '.join(keys)}\n\n"
                            + preview_block("Preview (start)", head_preview)
                        )
                    elif isinstance(data, list):
                        return f"JSON array with {len(data)} items\n\n" + preview_block(
                            "Preview (start)", head_preview
                        )
                except Exception:
                    return preview_block("Preview (start)", head_preview)

            # Default: provide head/tail preview
            return preview_block("Preview (start)", head_preview) + preview_block(
                "Preview (end)", tail_preview
            )

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
        """Collect all files in repository using efficient rglob."""
        all_files = []
        # Use rglob for more efficient traversal
        for file_path in self.root_path.rglob("*"):
            if file_path.is_file():
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

    def _build_dependency_graph(self, files_data: List[Dict]) -> Dict:
        """Build project-level dependency graph from files."""
        graph = {
            "projects": {},
            "nuget_packages": {},
            "top_dependencies": [],
        }

        # Collect all dependencies across files
        all_deps = {}

        for file_data in files_data:
            deps = file_data["context"]["dependencies"]
            for dep in deps:
                all_deps[dep] = all_deps.get(dep, 0) + 1

        # Find project files and their dependencies
        for file_data in files_data:
            if file_data["metadata"]["path"].endswith(".csproj"):
                project_name = Path(file_data["metadata"]["path"]).stem
                project_deps = file_data["context"]["dependencies"]

                # Separate NuGet packages from project references
                nuget_deps = [d for d in project_deps if not d.startswith("Project:")]
                project_refs = [d.replace("Project:", "") for d in project_deps if d.startswith("Project:")]

                graph["projects"][project_name] = {
                    "path": file_data["metadata"]["path"],
                    "nuget_packages": nuget_deps,
                    "project_references": project_refs,
                }

        # Count NuGet package usage across all projects
        for project_info in graph["projects"].values():
            for pkg in project_info["nuget_packages"]:
                if pkg not in graph["nuget_packages"]:
                    graph["nuget_packages"][pkg] = {"used_by_projects": []}
                graph["nuget_packages"][pkg]["used_by_projects"].append(
                    Path(project_info["path"]).stem
                )

        # Identify top dependencies by usage count
        sorted_deps = sorted(all_deps.items(), key=lambda x: x[1], reverse=True)
        graph["top_dependencies"] = [
            {"name": dep, "usage_count": count}
            for dep, count in sorted_deps[:20]
        ]

        return graph

    def _generate_architecture_summary(self, files_data: List[Dict]) -> Dict:
        """Generate architecture overview for MVVM projects."""
        summary = {
            "pattern": "MVVM",
            "views": [],
            "viewmodels": [],
            "models": [],
            "services": [],
            "repositories": [],
            "converters": [],
            "behaviors": [],
            "modules": [],
        }

        for file_data in files_data:
            path = file_data["metadata"]["path"]
            category = file_data["context"]["category"]
            sub_category = file_data["context"]["sub_category"]

            if category == "source_code":
                if sub_category == "ui":
                    summary["views"].append({
                        "path": path,
                        "name": Path(path).stem,
                        "related_files": file_data["context"]["related_files"],
                    })
                elif sub_category == "viewmodel":
                    summary["viewmodels"].append({
                        "path": path,
                        "name": Path(path).stem,
                        "dependencies": file_data["context"]["dependencies"],
                    })
                elif sub_category == "model":
                    summary["models"].append({
                        "path": path,
                        "name": Path(path).stem,
                    })
                elif sub_category == "service":
                    summary["services"].append({
                        "path": path,
                        "name": Path(path).stem,
                    })
                elif sub_category == "data":
                    summary["repositories"].append({
                        "path": path,
                        "name": Path(path).stem,
                    })
                elif sub_category == "converter":
                    summary["converters"].append({
                        "path": path,
                        "name": Path(path).stem,
                    })
                elif sub_category == "behavior":
                    summary["behaviors"].append({
                        "path": path,
                        "name": Path(path).stem,
                    })
                elif "Module" in path:
                    summary["modules"].append({
                        "path": path,
                        "name": Path(path).stem,
                    })

        # Add counts
        summary["counts"] = {
            "views": len(summary["views"]),
            "viewmodels": len(summary["viewmodels"]),
            "models": len(summary["models"]),
            "services": len(summary["services"]),
            "repositories": len(summary["repositories"]),
            "converters": len(summary["converters"]),
            "behaviors": len(summary["behaviors"]),
            "modules": len(summary["modules"]),
        }

        return summary

    def _scan_vulnerabilities(self) -> Dict:
        """Scan for package vulnerabilities using dotnet list package --vulnerable."""
        print("Scanning for NuGet package vulnerabilities...")

        dotnet_exe = shutil.which("dotnet")
        if not dotnet_exe:
            print("  Warning: dotnet CLI not found - skipping vulnerability scan")
            return {
                "vulnerable_packages": [],
                "outdated_packages": [],
                "secrets_detected": False,
                "last_security_scan": datetime.now().isoformat(),
            }

        vulnerable_packages = []
        outdated_packages = []

        # Use dictionaries for immediate deduplication
        vuln_dict = {}
        outdated_dict = {}

        # Find all .csproj files
        csproj_files = list(self.root_path.rglob("*.csproj"))

        for csproj in csproj_files:
            if self._should_exclude(str(csproj.relative_to(self.root_path))):
                continue

            try:
                # Check for vulnerable packages
                result = subprocess.run(
                    [dotnet_exe, "list", str(csproj), "package", "--vulnerable", "--include-transitive"],
                    capture_output=True,
                    text=True,
                    timeout=30,
                )

                if result.returncode == 0 and result.stdout:
                    # Parse output for vulnerabilities
                    lines = result.stdout.split("\n")
                    project_name = csproj.stem

                    for line in lines:
                        # Look for vulnerability markers
                        if ">" in line and ("Critical" in line or "High" in line or "Moderate" in line or "Low" in line):
                            parts = line.split()
                            if len(parts) >= 4:
                                package_name = parts[1] if len(parts) > 1 else "Unknown"
                                version = parts[2] if len(parts) > 2 else "Unknown"
                                severity = next((s for s in ["Critical", "High", "Moderate", "Low"] if s in line), "Unknown")

                                # Deduplicate immediately
                                key = f"{package_name}:{version}"
                                if key in vuln_dict:
                                    vuln_dict[key]["affected_projects"].append(project_name)
                                else:
                                    vuln_dict[key] = {
                                        "package_name": package_name,
                                        "installed_version": version,
                                        "severity": severity,
                                        "advisory_url": f"https://github.com/advisories?query={package_name}",
                                        "affected_projects": [project_name],
                                    }

                # Check for outdated packages
                result_outdated = subprocess.run(
                    [dotnet_exe, "list", str(csproj), "package", "--outdated"],
                    capture_output=True,
                    text=True,
                    timeout=30,
                )

                if result_outdated.returncode == 0 and result_outdated.stdout:
                    lines = result_outdated.stdout.split("\n")
                    for line in lines:
                        if ">" in line and len(line.split()) >= 5:
                            parts = line.split()
                            if len(parts) >= 5:
                                package_name = parts[1]
                                installed_ver = parts[2]
                                latest_ver = parts[4] if len(parts) > 4 else "Unknown"

                                # Deduplicate immediately
                                key = f"{package_name}:{installed_ver}"
                                if key in outdated_dict:
                                    outdated_dict[key]["used_by_projects"].append(project_name)
                                else:
                                    outdated_dict[key] = {
                                        "package_name": package_name,
                                        "installed_version": installed_ver,
                                        "latest_version": latest_ver,
                                        "used_by_projects": [project_name],
                                    }

            except (subprocess.TimeoutExpired, subprocess.CalledProcessError) as e:
                print(f"  Warning: Failed to scan {csproj.name}: {e}")
                continue

        result = {
            "vulnerable_packages": list(vuln_dict.values()),
            "outdated_packages": list(outdated_dict.values())[:50],  # Limit to 50
            "secrets_detected": False,  # Could integrate with gitleaks
            "last_security_scan": datetime.now().isoformat(),
        }

        print(f"  Found {len(result['vulnerable_packages'])} vulnerable packages")
        print(f"  Found {len(result['outdated_packages'])} outdated packages")

        return result

    def _calculate_metrics(self, files_data: List[Dict]) -> Dict:
        """Calculate repository-wide code metrics."""
        print("Calculating code metrics...")

        # Language-specific code/comment ratios
        LANGUAGE_RATIOS = {
            "C#": (0.75, 0.25),
            "XAML": (0.85, 0.15),
            "PowerShell": (0.60, 0.40),
            "Python": (0.70, 0.30),
            "JavaScript": (0.65, 0.35),
            "SQL": (0.70, 0.30),
        }
        DEFAULT_RATIO = (0.70, 0.30)

        total_lines = 0
        total_code_lines = 0
        total_comment_lines = 0
        test_count = 0
        project_metrics = {}

        for file_data in files_data:
            metadata = file_data["metadata"]
            context = file_data["context"]

            # Count lines
            if metadata.get("line_count"):
                total_lines += metadata["line_count"]

            # Estimate code vs comments using language-specific ratios
            if context["category"] == "source_code" and metadata.get("line_count"):
                language = metadata.get("language", "Unknown")
                code_ratio, comment_ratio = LANGUAGE_RATIOS.get(language, DEFAULT_RATIO)
                code_lines = int(metadata["line_count"] * code_ratio)
                comment_lines = int(metadata["line_count"] * comment_ratio)
                total_code_lines += code_lines
                total_comment_lines += comment_lines

            # Count tests
            if context["category"] == "test":
                summary = context.get("summary", "")
                if "test_methods" in summary.lower():
                    # Try to extract test count
                    matches = re.findall(r"(\d+) test", summary.lower())
                    if matches:
                        test_count += int(matches[0])

            # Project-level metrics
            path = metadata["path"]
            if "src/" in path:
                project = path.split("src/")[1].split("/")[0] if "/" in path else "root"
                if project not in project_metrics:
                    project_metrics[project] = {
                        "lines_of_code": 0,
                        "file_count": 0,
                        "average_complexity": 0.0,
                        "test_coverage": 0.0,
                    }

                project_metrics[project]["lines_of_code"] += metadata.get("line_count", 0)
                project_metrics[project]["file_count"] += 1

        return {
            "total_lines_of_code": total_code_lines,
            "total_code_lines": total_lines,
            "total_comment_lines": total_comment_lines,
            "average_complexity": 0.0,  # Would require Roslyn analysis
            "test_coverage_percent": 0.0,  # Would require coverlet
            "test_count": test_count,
            "project_metrics": project_metrics,
        }

    def _build_folder_tree(self, files: List[Path]) -> Dict:
        """Build hierarchical folder structure as nested dict."""
        print("Building folder tree...")

        root = {
            "name": self.root_path.name,
            "type": "directory",
            "path": ".",
            "children": []
        }

        # Group files by directory
        for file_path in files:
            relative = file_path.relative_to(self.root_path)
            parts = relative.parts

            # Build directory structure
            current = root
            current_path = []

            for i, part in enumerate(parts[:-1]):  # All but last (file name)
                current_path.append(part)
                path_key = "/".join(current_path)

                # Find or create directory entry
                existing = next((c for c in current["children"] if c["name"] == part and c["type"] == "directory"), None)
                if not existing:
                    new_dir = {
                        "name": part,
                        "type": "directory",
                        "path": path_key,
                        "children": []
                    }
                    current["children"].append(new_dir)
                    current = new_dir
                else:
                    current = existing

            # Add file
            if parts:  # Ensure we have parts
                current["children"].append({
                    "name": parts[-1],
                    "type": "file",
                    "path": str(relative).replace("\\", "/"),
                })

        # Sort children recursively
        def sort_tree(node):
            if "children" in node:
                node["children"].sort(key=lambda x: (x["type"] == "file", x["name"]))
                for child in node["children"]:
                    sort_tree(child)

        sort_tree(root)
        return root

    def _extract_license_info(self) -> Dict:
        """Extract license information from LICENSE file."""
        license_files = ["LICENSE", "LICENSE.txt", "LICENSE.md", "LICENSE.rst"]

        for license_file in license_files:
            license_path = self.root_path / license_file
            if license_path.exists():
                try:
                    with open(license_path, "r", encoding="utf-8", errors="ignore") as f:
                        content = f.read(2000)  # Read first 2000 chars

                    # Detect common license types
                    license_type = "Unknown"
                    if "MIT License" in content or "MIT" in content[:100]:
                        license_type = "MIT"
                    elif "Apache License" in content:
                        license_type = "Apache-2.0"
                    elif "GNU GENERAL PUBLIC LICENSE" in content:
                        if "Version 3" in content:
                            license_type = "GPL-3.0"
                        elif "Version 2" in content:
                            license_type = "GPL-2.0"
                    elif "BSD" in content[:200]:
                        license_type = "BSD"

                    return {
                        "type": license_type,
                        "file": license_file,
                        "detected": True,
                    }
                except Exception:
                    pass

        return {
            "type": "Unknown",
            "file": None,
            "detected": False,
        }

    def analyze_file(self, file_path: Path) -> Dict:
        """Analyze a single file and return metadata."""
        relative_path = str(file_path.relative_to(self.root_path)).replace("\\", "/")
        stat = file_path.stat()
        is_binary = self._is_binary(file_path)
        language = self._get_file_language(file_path)
        category, sub_category = self._categorize_file(file_path)
        importance = self._determine_importance(file_path, category)

        # Read content if not binary
        content = ""
        if not is_binary:
            try:
                with open(file_path, "r", encoding="utf-8", errors="ignore") as f:
                    content = f.read()
            except PermissionError as e:
                print(f"Warning: Permission denied reading {relative_path}: {e}")
            except OSError as e:
                print(f"Warning: OS error reading {relative_path}: {e}")
            except UnicodeDecodeError as e:
                print(f"Warning: Unicode decode error reading {relative_path}: {e}")

        dependencies = self._extract_dependencies(file_path, content)
        related_files = self._find_related_files(file_path)
        summary = self._generate_summary(
            file_path, is_binary, self.config.get("max_summary_length", 1500)
        )

        # Get git history if enabled in config
        git_history = []
        if self.config.get("include_git_history", True):
            git_history = self._get_file_git_history(relative_path, max_commits=5)

        # Extract last commit info if available
        git_metadata = {
            "last_commit": None,
            "last_commit_hash": None,
            "last_commit_author": None,
            "last_commit_date": None,
            "last_commit_message": None,
            "recent_commits": git_history,
        }

        if git_history:
            last = git_history[0]
            git_metadata.update({
                "last_commit": last["hash"][:7],
                "last_commit_hash": last["hash"],
                "last_commit_author": last["author"],
                "last_commit_date": last["date"],
                "last_commit_message": last["message"],
            })

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
                "git": git_metadata,
            },
            "urls": self._generate_github_urls(relative_path),
            "context": {
                "category": category,
                "sub_category": sub_category,
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
        """Generate complete manifest with parallel processing."""
        print("Cleaning up temporary files...")
        self._cleanup_temp_files()
        print()

        print("Collecting files...")
        files = self._collect_files()
        print(f"Found {len(files)} files to process")

        print("Analyzing files...")
        files_data = []
        total_size = 0
        categories_count = {}
        languages_count = {}

        # Use cached max_workers from __init__
        start_time = time.time()

        # Process files in parallel
        with ThreadPoolExecutor(max_workers=self.max_workers) as executor:
            # Submit all analysis tasks
            future_to_file = {
                executor.submit(self.analyze_file, file_path): file_path
                for file_path in files
            }

            # Process completed tasks
            completed = 0
            for future in as_completed(future_to_file):
                completed += 1
                file_path = future_to_file[future]

                # Progress update every 50 files
                if completed % 50 == 0:
                    elapsed = time.time() - start_time
                    rate = completed / elapsed if elapsed > 0 else 0
                    remaining = len(files) - completed
                    eta = remaining / rate if rate > 0 else 0
                    print(
                        f"  Processed {completed}/{len(files)} files... "
                        f"({rate:.1f} files/sec, ETA: {eta:.0f}s)"
                    )

                try:
                    file_data = future.result()

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

                except Exception as e:
                    print(f"  Error processing {file_path}: {str(e)}")

        elapsed_total = time.time() - start_time
        print(
            f"  Completed {len(files)} files in {elapsed_total:.1f}s "
            f"({len(files)/elapsed_total:.1f} files/sec)"
        )

        print("Building search index...")
        search_index = self._build_search_index(files_data)

        print("Building dependency graph...")
        dependency_graph = self._build_dependency_graph(files_data)

        print("Building folder tree...")
        folder_tree = self._build_folder_tree(files)

        print("Generating architecture summary...")
        architecture = self._generate_architecture_summary(files_data)

        print("Extracting license information...")
        license_info = self._extract_license_info()

        print("Scanning for vulnerabilities...")
        security_info = self._scan_vulnerabilities()

        print("Calculating code metrics...")
        metrics_info = self._calculate_metrics(files_data)

        # Calculate manifest validity period (default 7 days)
        validity_hours = self.config.get("manifest_validity_hours", 168)
        valid_until = datetime.now() + timedelta(hours=validity_hours)

        # Pagination/filtering support
        max_files_in_manifest = self.config.get("max_files_in_manifest", None)
        files_truncated = False
        original_file_count = len(files_data)

        if max_files_in_manifest and len(files_data) > max_files_in_manifest:
            files_truncated = True
            # Prioritize important files
            files_data_sorted = sorted(
                files_data,
                key=lambda x: (
                    0 if x["context"]["importance"] == "high" else
                    1 if x["context"]["importance"] == "normal" else 2,
                    -x["metadata"]["size"]  # Then by size
                )
            )
            files_data = files_data_sorted[:max_files_in_manifest]

        manifest = {
            "$schema": "https://raw.githubusercontent.com/Bigessfour/Wiley-Widget/main/schemas/ai-manifest-schema.json",
            "repository": {
                "remote_url": self.git_info["remote_url"],
                "owner_repo": self.git_info["owner_repo"],
                "branch": self.git_info["branch"],
                "commit_hash": self.git_info["commit_hash"],
                "is_dirty": self.git_info["is_dirty"],
                "generated_at": datetime.now().isoformat(),
                "valid_until": valid_until.isoformat(),
            },
            "license": license_info,
            "summary": {
                "total_files": original_file_count,
                "files_in_manifest": len(files_data),
                "files_truncated": files_truncated,
                "total_size": total_size,
                "categories": categories_count,
                "languages": languages_count,
            },
            "metrics": metrics_info,
            "security": security_info,
            "quality": {
                "build_status": "unknown",
                "analyzers_enabled": True,
                "documentation_coverage": 0.0,
                "technical_debt_minutes": 0,
            },
            "architecture": architecture,
            "dependency_graph": dependency_graph,
            "folder_tree": folder_tree,
            "search_index": search_index,
            "files": files_data,
        }

        print(f"Writing manifest to {output_path}...")
        with open(output_path, "w", encoding="utf-8") as f:
            json.dump(manifest, f, indent=2)

        print("[SUCCESS] Manifest generated successfully!")
        print(f"   Files: {len(files_data)}" + (f" of {original_file_count} (truncated)" if files_truncated else ""))
        print(f"   Size: {total_size:,} bytes")
        print(f"   Categories: {', '.join(categories_count.keys())}")
        print(f"   Search keywords: {len(search_index)}")
        print(f"   Projects: {len(dependency_graph['projects'])}")
        print(f"   NuGet packages: {len(dependency_graph['nuget_packages'])}")
        print(f"   Architecture: {architecture['counts']['viewmodels']} ViewModels, {architecture['counts']['views']} Views")
        print(f"   Metrics: {metrics_info['total_lines_of_code']:,} LOC, {metrics_info['test_count']} tests")
        print(f"   Security: {len(security_info['vulnerable_packages'])} vulnerable packages, {len(security_info['outdated_packages'])} outdated")
        print(f"   License: {license_info['type']}")
        print(f"   Valid until: {valid_until.strftime('%Y-%m-%d %H:%M')}")

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
        # Validate categories
        invalid_cats = [c for c in filter_categories if c not in KNOWN_CATEGORIES]
        if invalid_cats:
            print(f"Warning: Unknown categories will be ignored: {', '.join(invalid_cats)}")
            print(f"Valid categories: {', '.join(sorted(KNOWN_CATEGORIES))}")
            filter_categories = [c for c in filter_categories if c in KNOWN_CATEGORIES]
            if not filter_categories:
                print("Error: No valid categories specified")
                return

    analyzer = RepositoryAnalyzer(args.config)
    analyzer.generate_manifest(args.output, filter_categories)


if __name__ == "__main__":
    main()
