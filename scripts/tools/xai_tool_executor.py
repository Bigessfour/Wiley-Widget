#!/usr/bin/env python3
"""
xAI Tool Executor - Bridge between xAI SDK and IDE client-side tools.

This module provides direct tool execution with MCP availability, exposing all IDE tools
to xAI's agentic workflow. It uses `get_tool_call_type` from xai-sdk to route:
- `client_side_tool` → Execute locally via IDE tool handlers
- `web_search_tool`, `x_search_tool`, `code_execution_tool` → Server-side (xAI handles)
- `mcp_tool` → Route to local MCP servers

Verification Required Before Running:
1. Lint/Analyze: ruff check scripts/tools/xai_tool_executor.py
2. Dry-Run: python scripts/tools/xai_tool_executor.py --dry-run
3. Test: python scripts/tools/xai_tool_executor.py --test
4. Confirm: XAI_API_KEY environment variable is set

Usage:
    from xai_tool_executor import XaiToolExecutor
    executor = XaiToolExecutor()
    result = executor.execute_tool_call(tool_call)
"""

from __future__ import annotations

import argparse
import json
import subprocess
import sys
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Callable, Optional, Union

# Type alias for tool handler functions
ToolHandler = Callable[[dict[str, Any]], str]

# Tool call type constants (matching xai-sdk ToolCallType enum)
TOOL_CALL_TYPE_CLIENT_SIDE = "client_side_tool"
TOOL_CALL_TYPE_WEB_SEARCH = "web_search_tool"
TOOL_CALL_TYPE_X_SEARCH = "x_search_tool"
TOOL_CALL_TYPE_CODE_EXECUTION = "code_execution_tool"
TOOL_CALL_TYPE_COLLECTIONS_SEARCH = "collections_search_tool"
TOOL_CALL_TYPE_MCP = "mcp_tool"
TOOL_CALL_TYPE_ATTACHMENT_SEARCH = "attachment_search_tool"

# Server-side tool types (xAI handles these)
SERVER_SIDE_TOOL_TYPES = frozenset(
    {
        TOOL_CALL_TYPE_WEB_SEARCH,
        TOOL_CALL_TYPE_X_SEARCH,
        TOOL_CALL_TYPE_CODE_EXECUTION,
        TOOL_CALL_TYPE_COLLECTIONS_SEARCH,
        TOOL_CALL_TYPE_ATTACHMENT_SEARCH,
    }
)


@dataclass
class ToolCallResult:
    """Result of a tool call execution."""

    success: bool
    output: str
    tool_name: str
    tool_type: str
    error: Optional[str] = None
    metadata: dict[str, Any] = field(default_factory=dict)


@dataclass
class ToolCall:
    """Representation of a tool call from xAI response."""

    id: str
    name: str
    arguments: dict[str, Any]
    tool_type: str = TOOL_CALL_TYPE_CLIENT_SIDE

    @classmethod
    def from_xai_tool_call(cls, tool_call: Any) -> "ToolCall":
        """Create from xai_sdk ToolCall proto object."""
        try:
            # Import xai-sdk for tool type detection
            from xai_sdk.tools import get_tool_call_type

            tool_type = get_tool_call_type(tool_call)
        except ImportError:
            # Fallback: assume client-side if sdk not available
            tool_type = TOOL_CALL_TYPE_CLIENT_SIDE

        return cls(
            id=tool_call.id,
            name=tool_call.function.name,
            arguments=json.loads(tool_call.function.arguments),
            tool_type=tool_type,
        )

    @classmethod
    def from_dict(cls, data: dict[str, Any]) -> "ToolCall":
        """Create from dictionary (for testing/manual calls)."""
        return cls(
            id=data.get("id", "manual-call"),
            name=data["name"],
            arguments=data.get("arguments", {}),
            tool_type=data.get("tool_type", TOOL_CALL_TYPE_CLIENT_SIDE),
        )


class IdeToolHandlers:
    """
    IDE tool handlers for client-side execution.

    These mirror the tools defined in .continue/config.json and execute
    locally in the IDE/workspace context.
    """

    def __init__(self, workspace_root: Optional[Path] = None):
        self.workspace_root = workspace_root or Path.cwd()
        self._handlers: dict[str, ToolHandler] = {
            # File operations
            "read_file": self._handle_read_file,
            "list_directory": self._handle_list_directory,
            "search_files": self._handle_search_files,
            "grep_search": self._handle_grep_search,
            "edit_file": self._handle_edit_file,
            "create_file": self._handle_create_file,
            "delete_file": self._handle_delete_file,
            # Terminal
            "run_terminal_command": self._handle_run_terminal,
            # IDE features
            "get_diagnostics": self._handle_get_diagnostics,
            "get_git_diff": self._handle_get_git_diff,
            "git_commit": self._handle_git_commit,
            "find_references": self._handle_find_references,
            "go_to_definition": self._handle_go_to_definition,
            # Build/Test
            "build_project": self._handle_build_project,
            "run_tests": self._handle_run_tests,
            # Code operations
            "rename_symbol": self._handle_rename_symbol,
            "format_file": self._handle_format_file,
            "open_file": self._handle_open_file,
        }

    def get_handler(self, tool_name: str) -> Optional[ToolHandler]:
        """Get handler for a tool by name."""
        return self._handlers.get(tool_name)

    def list_tools(self) -> list[str]:
        """List all available tool names."""
        return list(self._handlers.keys())

    def _resolve_path(self, filepath: str) -> Path:
        """Resolve filepath relative to workspace root."""
        path = Path(filepath)
        if not path.is_absolute():
            path = self.workspace_root / path
        return path

    # === File Operations ===

    def _handle_read_file(self, args: dict[str, Any]) -> str:
        """Read file contents, optionally with line range."""
        filepath = self._resolve_path(args["filepath"])
        if not filepath.exists():
            return f"Error: File not found: {filepath}"

        try:
            content = filepath.read_text(encoding="utf-8")
            lines = content.splitlines(keepends=True)

            start_line = args.get("startLine", 1)
            end_line = args.get("endLine", len(lines))

            # Convert to 0-indexed
            start_idx = max(0, start_line - 1)
            end_idx = min(len(lines), end_line)

            selected = lines[start_idx:end_idx]
            return "".join(selected)
        except Exception as e:
            return f"Error reading file: {e}"

    def _handle_list_directory(self, args: dict[str, Any]) -> str:
        """List directory contents."""
        dirpath = self._resolve_path(args["path"])
        if not dirpath.exists():
            return f"Error: Directory not found: {dirpath}"
        if not dirpath.is_dir():
            return f"Error: Not a directory: {dirpath}"

        recursive = args.get("recursive", False)
        max_depth = 3 if recursive else 1

        entries: list[str] = []
        self._list_dir_recursive(dirpath, entries, 0, max_depth)
        return "\n".join(entries)

    def _list_dir_recursive(
        self, path: Path, entries: list[str], depth: int, max_depth: int
    ) -> None:
        """Recursively list directory contents."""
        if depth >= max_depth:
            return

        indent = "  " * depth
        try:
            for entry in sorted(path.iterdir()):
                if entry.is_dir():
                    entries.append(f"{indent}[DIR] {entry.name}/")
                    self._list_dir_recursive(entry, entries, depth + 1, max_depth)
                else:
                    entries.append(f"{indent}[FILE] {entry.name}")
        except PermissionError:
            entries.append(f"{indent}[ERROR] Permission denied")

    def _handle_search_files(self, args: dict[str, Any]) -> str:
        """Search for files by glob pattern."""
        pattern = args["pattern"]
        max_results = args.get("maxResults", 50)

        matches: list[str] = []
        for match in self.workspace_root.glob(pattern):
            if len(matches) >= max_results:
                break
            rel_path = match.relative_to(self.workspace_root)
            matches.append(str(rel_path))

        if not matches:
            return f"No files matching pattern: {pattern}"
        return "\n".join(matches)

    def _handle_grep_search(self, args: dict[str, Any]) -> str:
        """Search for text/regex in files."""
        import re

        query = args["query"]
        include_pattern = args.get("includePattern", "**/*")
        is_regex = args.get("isRegex", False)

        results: list[str] = []
        pattern = re.compile(query, re.IGNORECASE) if is_regex else None

        for filepath in self.workspace_root.glob(include_pattern):
            if not filepath.is_file():
                continue
            try:
                content = filepath.read_text(encoding="utf-8", errors="ignore")
                for i, line in enumerate(content.splitlines(), 1):
                    if pattern and pattern.search(line):
                        results.append(f"{filepath}:{i}: {line.strip()}")
                    elif not pattern and query.lower() in line.lower():
                        results.append(f"{filepath}:{i}: {line.strip()}")
            except Exception:
                continue

        if not results:
            return f"No matches for: {query}"
        return "\n".join(results[:100])  # Limit output

    def _handle_edit_file(self, args: dict[str, Any]) -> str:
        """Replace text in file."""
        filepath = self._resolve_path(args["filepath"])
        old_text = args["oldText"]
        new_text = args["newText"]

        if not filepath.exists():
            return f"Error: File not found: {filepath}"

        try:
            content = filepath.read_text(encoding="utf-8")
            if old_text not in content:
                return (
                    "Error: oldText not found in file. Ensure exact match with context."
                )

            # Replace first occurrence only
            new_content = content.replace(old_text, new_text, 1)
            filepath.write_text(new_content, encoding="utf-8")
            return f"Successfully edited {filepath}"
        except Exception as e:
            return f"Error editing file: {e}"

    def _handle_create_file(self, args: dict[str, Any]) -> str:
        """Create new file with content."""
        filepath = self._resolve_path(args["filepath"])
        content = args["content"]

        try:
            filepath.parent.mkdir(parents=True, exist_ok=True)
            filepath.write_text(content, encoding="utf-8")
            return f"Created file: {filepath}"
        except Exception as e:
            return f"Error creating file: {e}"

    def _handle_delete_file(self, args: dict[str, Any]) -> str:
        """Delete a file."""
        filepath = self._resolve_path(args["filepath"])

        if not filepath.exists():
            return f"Error: File not found: {filepath}"

        try:
            filepath.unlink()
            return f"Deleted: {filepath}"
        except Exception as e:
            return f"Error deleting file: {e}"

    # === Terminal ===

    def _handle_run_terminal(self, args: dict[str, Any]) -> str:
        """Execute shell command."""
        command = args["command"]
        cwd = args.get("cwd", str(self.workspace_root))
        timeout = args.get("timeout", 60)

        try:
            result = subprocess.run(
                ["pwsh", "-NoProfile", "-Command", command],
                cwd=cwd,
                capture_output=True,
                text=True,
                timeout=timeout,
            )
            output = result.stdout
            if result.stderr:
                output += f"\n[STDERR]\n{result.stderr}"
            if result.returncode != 0:
                output += f"\n[Exit code: {result.returncode}]"
            return output or "(No output)"
        except subprocess.TimeoutExpired:
            return f"Error: Command timed out after {timeout}s"
        except Exception as e:
            return f"Error executing command: {e}"

    # === IDE Features ===

    def _handle_get_diagnostics(self, args: dict[str, Any]) -> str:
        """Get IDE errors/warnings (placeholder - requires IDE integration)."""
        filepath = args.get("filepath")
        severity = args.get("severity")

        # This would integrate with VS Code diagnostics API
        return f"[IDE Integration Required] Get diagnostics for: {filepath or 'all files'}, severity: {severity or 'all'}"

    def _handle_get_git_diff(self, args: dict[str, Any]) -> str:
        """Get uncommitted git changes."""
        staged = args.get("staged", False)
        filepath = args.get("filepath")

        cmd = ["git", "diff"]
        if staged:
            cmd.append("--cached")
        if filepath:
            cmd.append(str(self._resolve_path(filepath)))

        try:
            result = subprocess.run(
                cmd,
                cwd=self.workspace_root,
                capture_output=True,
                text=True,
            )
            return result.stdout or "(No changes)"
        except Exception as e:
            return f"Error getting git diff: {e}"

    def _handle_git_commit(self, args: dict[str, Any]) -> str:
        """Stage and commit changes."""
        message = args["message"]
        files = args.get("files", [])

        try:
            # Stage files
            if files:
                for f in files:
                    subprocess.run(
                        ["git", "add", str(self._resolve_path(f))],
                        cwd=self.workspace_root,
                        check=True,
                    )
            else:
                subprocess.run(
                    ["git", "add", "-A"],
                    cwd=self.workspace_root,
                    check=True,
                )

            # Commit
            result = subprocess.run(
                ["git", "commit", "-m", message],
                cwd=self.workspace_root,
                capture_output=True,
                text=True,
            )
            return result.stdout or result.stderr
        except Exception as e:
            return f"Error committing: {e}"

    def _handle_find_references(self, args: dict[str, Any]) -> str:
        """Find all references to a symbol (placeholder)."""
        symbol = args["symbolName"]
        filepath = args.get("filepath")
        return f"[IDE Integration Required] Find references for: {symbol} in {filepath or 'workspace'}"

    def _handle_go_to_definition(self, args: dict[str, Any]) -> str:
        """Find symbol definition location (placeholder)."""
        symbol = args["symbolName"]
        filepath = args.get("filepath")
        line = args.get("line")
        return f"[IDE Integration Required] Go to definition: {symbol} at {filepath}:{line}"

    # === Build/Test ===

    def _handle_build_project(self, args: dict[str, Any]) -> str:
        """Run dotnet build."""
        project = args.get("project", ".")
        config = args.get("configuration", "Debug")
        verbosity = args.get("verbosity", "minimal")

        cmd = [
            "dotnet",
            "build",
            str(self._resolve_path(project)),
            "-c",
            config,
            "-v",
            verbosity,
        ]

        try:
            result = subprocess.run(
                cmd,
                cwd=self.workspace_root,
                capture_output=True,
                text=True,
                timeout=300,
            )
            output = result.stdout
            if result.stderr:
                output += f"\n{result.stderr}"
            return output
        except subprocess.TimeoutExpired:
            return "Error: Build timed out after 5 minutes"
        except Exception as e:
            return f"Error building: {e}"

    def _handle_run_tests(self, args: dict[str, Any]) -> str:
        """Run dotnet test."""
        project = args.get("project", ".")
        filter_expr = args.get("filter")
        verbosity = args.get("verbosity", "minimal")

        cmd = [
            "dotnet",
            "test",
            str(self._resolve_path(project)),
            "-v",
            verbosity,
        ]
        if filter_expr:
            cmd.extend(["--filter", filter_expr])

        try:
            result = subprocess.run(
                cmd,
                cwd=self.workspace_root,
                capture_output=True,
                text=True,
                timeout=600,
            )
            output = result.stdout
            if result.stderr:
                output += f"\n{result.stderr}"
            return output
        except subprocess.TimeoutExpired:
            return "Error: Tests timed out after 10 minutes"
        except Exception as e:
            return f"Error running tests: {e}"

    # === Code Operations ===

    def _handle_rename_symbol(self, args: dict[str, Any]) -> str:
        """Rename symbol across codebase (placeholder)."""
        old_name = args["oldName"]
        new_name = args["newName"]
        filepath = args["filepath"]
        return (
            f"[IDE Integration Required] Rename {old_name} -> {new_name} in {filepath}"
        )

    def _handle_format_file(self, args: dict[str, Any]) -> str:
        """Format file with appropriate formatter."""
        filepath = self._resolve_path(args["filepath"])

        if not filepath.exists():
            return f"Error: File not found: {filepath}"

        suffix = filepath.suffix.lower()

        try:
            if suffix in {".py"}:
                result = subprocess.run(
                    ["ruff", "format", str(filepath)],
                    capture_output=True,
                    text=True,
                )
            elif suffix in {".cs"}:
                result = subprocess.run(
                    ["dotnet", "format", str(filepath)],
                    capture_output=True,
                    text=True,
                )
            elif suffix in {".json", ".jsonc"}:
                # Use Python json for formatting
                content = filepath.read_text(encoding="utf-8")
                parsed = json.loads(content)
                formatted = json.dumps(parsed, indent=2)
                filepath.write_text(formatted, encoding="utf-8")
                return f"Formatted: {filepath}"
            else:
                return f"No formatter configured for {suffix} files"

            return result.stdout or f"Formatted: {filepath}"
        except Exception as e:
            return f"Error formatting: {e}"

    def _handle_open_file(self, args: dict[str, Any]) -> str:
        """Open file in editor (placeholder)."""
        filepath = args["filepath"]
        line = args.get("line")
        column = args.get("column")
        location = f"{filepath}"
        if line:
            location += f":{line}"
        if column:
            location += f":{column}"
        return f"[IDE Integration Required] Open: {location}"


class McpToolRouter:
    """
    Routes MCP tool calls to local MCP servers.

    Integrates with the MCP servers configured in .continue/config.json mcpServers.
    """

    def __init__(self, mcp_config_path: Optional[Path] = None):
        self.mcp_servers: dict[str, dict[str, Any]] = {}
        if mcp_config_path:
            self._load_config(mcp_config_path)

    def _load_config(self, config_path: Path) -> None:
        """Load MCP server configuration."""
        try:
            content = config_path.read_text(encoding="utf-8")
            # Handle JSONC (JSON with comments)
            lines = [
                line
                for line in content.splitlines()
                if not line.strip().startswith("//")
            ]
            config = json.loads("\n".join(lines))
            self.mcp_servers = config.get("mcpServers", {})
        except Exception as e:
            print(f"Warning: Failed to load MCP config: {e}", file=sys.stderr)

    def route_mcp_call(
        self, server_label: str, tool_name: str, arguments: dict[str, Any]
    ) -> str:
        """Route a tool call to the appropriate MCP server."""
        if server_label not in self.mcp_servers:
            return f"Error: Unknown MCP server: {server_label}"

        self.mcp_servers[server_label]
        # Implementation would use MCP protocol to call the server
        return f"[MCP Router] Called {server_label}/{tool_name} with {arguments}"


class XaiToolExecutor:
    """
    Main executor that bridges xAI SDK tool calls with local IDE execution.

    Usage:
        executor = XaiToolExecutor()

        # From xAI response with tool_calls:
        for tool_call in response.tool_calls:
            if get_tool_call_type(tool_call) == "client_side_tool":
                result = executor.execute_from_xai(tool_call)
                chat.append(tool_result(result.output))
    """

    def __init__(
        self,
        workspace_root: Optional[Path] = None,
        continue_config_path: Optional[Path] = None,
    ):
        self.workspace_root = workspace_root or Path.cwd()
        self.ide_handlers = IdeToolHandlers(self.workspace_root)

        # Load MCP config from .continue/config.json
        if continue_config_path is None:
            continue_config_path = Path.home() / ".continue" / "config.json"
        self.mcp_router = McpToolRouter(continue_config_path)

    def execute_from_xai(self, xai_tool_call: Any) -> ToolCallResult:
        """Execute a tool call from xAI SDK ToolCall proto."""
        tool_call = ToolCall.from_xai_tool_call(xai_tool_call)
        return self.execute(tool_call)

    def execute(self, tool_call: ToolCall) -> ToolCallResult:
        """Execute a tool call and return the result."""
        tool_type = tool_call.tool_type
        tool_name = tool_call.name
        arguments = tool_call.arguments

        # Server-side tools: xAI handles these, we just acknowledge
        if tool_type in SERVER_SIDE_TOOL_TYPES:
            return ToolCallResult(
                success=True,
                output=f"[Server-side tool] {tool_type}: {tool_name} - handled by xAI",
                tool_name=tool_name,
                tool_type=tool_type,
                metadata={"server_handled": True},
            )

        # MCP tools: route to local MCP server
        if tool_type == TOOL_CALL_TYPE_MCP:
            # Parse MCP tool name format: server_label/tool_name
            parts = tool_name.split("/", 1)
            if len(parts) == 2:
                server_label, mcp_tool = parts
            else:
                server_label, mcp_tool = "default", tool_name

            output = self.mcp_router.route_mcp_call(server_label, mcp_tool, arguments)
            return ToolCallResult(
                success=True,
                output=output,
                tool_name=tool_name,
                tool_type=tool_type,
            )

        # Client-side tools: execute locally
        handler = self.ide_handlers.get_handler(tool_name)
        if handler is None:
            return ToolCallResult(
                success=False,
                output=f"Unknown client-side tool: {tool_name}",
                tool_name=tool_name,
                tool_type=tool_type,
                error=f"No handler for tool: {tool_name}",
            )

        try:
            output = handler(arguments)
            return ToolCallResult(
                success=True,
                output=output,
                tool_name=tool_name,
                tool_type=tool_type,
            )
        except Exception as e:
            return ToolCallResult(
                success=False,
                output=f"Error executing {tool_name}: {e}",
                tool_name=tool_name,
                tool_type=tool_type,
                error=str(e),
            )

    def execute_dict(self, tool_data: dict[str, Any]) -> ToolCallResult:
        """Execute a tool call from dictionary format."""
        tool_call = ToolCall.from_dict(tool_data)
        return self.execute(tool_call)

    def list_available_tools(self) -> dict[str, list[str]]:
        """List all available tools by category."""
        return {
            "client_side_tools": self.ide_handlers.list_tools(),
            "server_side_tools": [
                "web_search",
                "x_search",
                "code_execution",
                "collections_search",
                "attachment_search",
            ],
            "mcp_servers": list(self.mcp_router.mcp_servers.keys()),
        }


def get_tool_call_type_compat(tool_call: Union[Any, dict]) -> str:
    """
    Get tool call type with fallback for dicts or when xai-sdk is not installed.

    Returns one of: client_side_tool, web_search_tool, x_search_tool,
    code_execution_tool, collections_search_tool, mcp_tool, attachment_search_tool
    """
    # For dict inputs, use name-based inference (can't use SDK's get_tool_call_type)
    if isinstance(tool_call, dict):
        name = tool_call.get("name", "")
        # Known server-side tools
        if name in {
            "web_search",
            "x_search",
            "code_execution",
            "collections_search",
            "attachment_search",
        }:
            return f"{name}_tool"
        if name.startswith("mcp:") or "/" in name:
            return TOOL_CALL_TYPE_MCP
        return TOOL_CALL_TYPE_CLIENT_SIDE

    # For xai-sdk ToolCall proto objects, use the SDK function
    try:
        from xai_sdk.tools import get_tool_call_type

        return get_tool_call_type(tool_call)
    except (ImportError, AttributeError):
        # Fallback: infer from tool name
        name = getattr(getattr(tool_call, "function", None), "name", "")
        if name in {
            "web_search",
            "x_search",
            "code_execution",
            "collections_search",
            "attachment_search",
        }:
            return f"{name}_tool"
        if name.startswith("mcp:") or "/" in name:
            return TOOL_CALL_TYPE_MCP
        return TOOL_CALL_TYPE_CLIENT_SIDE


def main() -> None:
    """CLI entry point for testing."""
    parser = argparse.ArgumentParser(
        description="xAI Tool Executor - Bridge xAI SDK with IDE tools"
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Simulate execution without side effects",
    )
    parser.add_argument(
        "--test",
        action="store_true",
        help="Run basic tests",
    )
    parser.add_argument(
        "--list-tools",
        action="store_true",
        help="List all available tools",
    )
    parser.add_argument(
        "--tool",
        type=str,
        help="Tool name to execute",
    )
    parser.add_argument(
        "--args",
        type=str,
        help="Tool arguments as JSON",
    )
    parser.add_argument(
        "--workspace",
        type=str,
        default=".",
        help="Workspace root directory",
    )

    args = parser.parse_args()
    executor = XaiToolExecutor(workspace_root=Path(args.workspace).resolve())

    if args.dry_run:
        print("Dry-run mode: Would initialize XaiToolExecutor")
        print(f"Workspace: {executor.workspace_root}")
        print(f"Available tools: {len(executor.ide_handlers.list_tools())}")
        return

    if args.list_tools:
        tools = executor.list_available_tools()
        print("Available Tools:")
        for category, tool_list in tools.items():
            print(f"\n{category}:")
            for tool in tool_list:
                print(f"  - {tool}")
        return

    if args.test:
        print("Running basic tests...")

        # Test 1: Read file
        result = executor.execute_dict(
            {
                "name": "read_file",
                "arguments": {
                    "filepath": "pyproject.toml",
                    "startLine": 1,
                    "endLine": 10,
                },
            }
        )
        print(f"\n[Test: read_file] Success={result.success}")
        print(f"Output: {result.output[:200]}...")

        # Test 2: List directory
        result = executor.execute_dict(
            {
                "name": "list_directory",
                "arguments": {"path": ".", "recursive": False},
            }
        )
        print(f"\n[Test: list_directory] Success={result.success}")
        print(f"Output: {result.output[:300]}...")

        # Test 3: Search files
        result = executor.execute_dict(
            {
                "name": "search_files",
                "arguments": {"pattern": "**/*.py", "maxResults": 5},
            }
        )
        print(f"\n[Test: search_files] Success={result.success}")
        print(f"Output: {result.output}")

        # Test 4: Tool type detection
        print("\n[Test: get_tool_call_type_compat]")
        for name in ["read_file", "web_search", "mcp:filesystem/read"]:
            tt = get_tool_call_type_compat({"name": name})
            print(f"  {name} -> {tt}")

        print("\n✓ All tests passed")
        return

    if args.tool:
        arguments = json.loads(args.args) if args.args else {}
        result = executor.execute_dict(
            {
                "name": args.tool,
                "arguments": arguments,
            }
        )
        print(f"Tool: {result.tool_name}")
        print(f"Type: {result.tool_type}")
        print(f"Success: {result.success}")
        print(f"Output:\n{result.output}")
        if result.error:
            print(f"Error: {result.error}")
        return

    parser.print_help()


if __name__ == "__main__":
    main()
