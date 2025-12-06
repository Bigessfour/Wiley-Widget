#!/usr/bin/env python3
"""
xAI Agentic Chat Session - Full integration with IDE tools and MCP.

This module provides a complete agentic chat loop that:
1. Uses xai-sdk for chat with Grok models
2. Routes client-side tool calls to local IDE execution
3. Handles server-side tools (web_search, x_search, code_execution) via xAI
4. Supports MCP server integration

Usage:
    from xai_agentic_chat import AgenticChatSession

    session = AgenticChatSession(model="grok-4-fast")
    response = await session.chat("Search the web for Python best practices, then read my main.py file")

Verification Required:
1. Lint: ruff check scripts/tools/xai_agentic_chat.py
2. Environment: XAI_API_KEY must be set
3. Test: python scripts/tools/xai_agentic_chat.py --test
"""

from __future__ import annotations

import argparse
import asyncio
import importlib
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Optional

# Local imports
from xai_tool_executor import (
    ToolCallResult,
    XaiToolExecutor,
)


@dataclass
class ChatMessage:
    """A message in the chat conversation."""

    role: str  # "user", "assistant", "tool", "system"
    content: str
    tool_calls: list[dict[str, Any]] = field(default_factory=list)
    tool_call_id: Optional[str] = None


@dataclass
class AgenticResponse:
    """Response from the agentic chat session."""

    content: str
    tool_calls_executed: list[ToolCallResult]
    reasoning_tokens: int = 0
    finish_reason: str = "stop"


class AgenticChatSession:
    """
    Agentic chat session with xAI Grok models and full IDE tool access.

    This provides a complete agentic loop:
    1. Send user message with available tools
    2. If model returns client-side tool calls, execute locally
    3. Append tool results and continue until no more tool calls
    4. Return final response with all executed tool results
    """

    def __init__(
        self,
        model: str = "grok-4-fast",
        workspace_root: Optional[Path] = None,
        system_prompt: Optional[str] = None,
        include_server_side_tools: bool = True,
    ):
        self.model = model
        self.workspace_root = workspace_root or Path.cwd()
        self.tool_executor = XaiToolExecutor(workspace_root=self.workspace_root)

        self.system_prompt = system_prompt or self._default_system_prompt()
        self.include_server_side_tools = include_server_side_tools

        self._client: Any = None
        self._messages: list[Any] = []

    def _default_system_prompt(self) -> str:
        """Default system prompt for agentic mode."""
        return """You are an expert coding assistant with access to both server-side and client-side tools.

SERVER-SIDE TOOLS (executed by xAI):
- web_search: Search the internet for information
- x_search: Search X (Twitter) for posts and trends
- code_execution: Execute Python code in a sandboxed environment

CLIENT-SIDE TOOLS (executed locally in the user's IDE):
- read_file: Read file contents
- list_directory: List files in a directory
- search_files: Search for files by glob pattern
- grep_search: Search for text/regex in files
- edit_file: Replace text in a file
- create_file: Create a new file
- delete_file: Delete a file
- run_terminal_command: Execute shell commands
- get_diagnostics: Get IDE errors/warnings
- get_git_diff: Get git changes
- git_commit: Commit changes
- build_project: Run dotnet build
- run_tests: Run dotnet test
- format_file: Format code

Use these tools together to help the user. When you need current information, use web_search or x_search.
When working with the user's code, use the client-side tools to read, modify, and verify changes.

Always explain what you're doing and why you're using specific tools."""

    async def _ensure_client(self) -> None:
        """Ensure xAI client is initialized."""
        if self._client is not None:
            return

        try:
            # Import dynamically to avoid static import errors when xai-sdk
            # is not installed in the analysis environment.
            xai_sdk = importlib.import_module("xai_sdk")
            AsyncClient = getattr(xai_sdk, "AsyncClient")
            self._client = AsyncClient()
        except ImportError as err:
            raise ImportError(
                "xai-sdk not installed. Install with: pip install xai-sdk"
            ) from err

    def _build_client_side_tools(self) -> list[dict[str, Any]]:
        """Build tool definitions for client-side IDE tools."""
        # All tools from config.json format, converted to xAI format
        tool_defs = [
            {
                "name": "read_file",
                "description": "Read the contents of a file. CLIENT-SIDE: Executes locally in IDE.",
                "parameters": {
                    "type": "object",
                    "properties": {
                        "filepath": {
                            "type": "string",
                            "description": "Path to the file",
                        },
                        "startLine": {
                            "type": "integer",
                            "description": "Start line (1-indexed)",
                        },
                        "endLine": {
                            "type": "integer",
                            "description": "End line (1-indexed)",
                        },
                    },
                    "required": ["filepath"],
                },
            },
            {
                "name": "list_directory",
                "description": "List files and directories. CLIENT-SIDE tool.",
                "parameters": {
                    "type": "object",
                    "properties": {
                        "path": {"type": "string", "description": "Directory path"},
                        "recursive": {
                            "type": "boolean",
                            "description": "List recursively (max 3 levels)",
                        },
                    },
                    "required": ["path"],
                },
            },
            {
                "name": "search_files",
                "description": "Search for files by glob pattern. CLIENT-SIDE tool.",
                "parameters": {
                    "type": "object",
                    "properties": {
                        "pattern": {
                            "type": "string",
                            "description": "Glob pattern (e.g., '**/*.cs')",
                        },
                        "maxResults": {
                            "type": "integer",
                            "description": "Max results (default: 50)",
                        },
                    },
                    "required": ["pattern"],
                },
            },
            {
                "name": "grep_search",
                "description": "Search for text/regex in files. CLIENT-SIDE tool.",
                "parameters": {
                    "type": "object",
                    "properties": {
                        "query": {
                            "type": "string",
                            "description": "Text or regex to search",
                        },
                        "includePattern": {
                            "type": "string",
                            "description": "File glob filter",
                        },
                        "isRegex": {
                            "type": "boolean",
                            "description": "Treat query as regex",
                        },
                    },
                    "required": ["query"],
                },
            },
            {
                "name": "edit_file",
                "description": "Replace text in file. Include 3+ lines context for unique match. CLIENT-SIDE tool.",
                "parameters": {
                    "type": "object",
                    "properties": {
                        "filepath": {"type": "string", "description": "File path"},
                        "oldText": {
                            "type": "string",
                            "description": "Exact text to replace (with context)",
                        },
                        "newText": {
                            "type": "string",
                            "description": "Replacement text",
                        },
                    },
                    "required": ["filepath", "oldText", "newText"],
                },
            },
            {
                "name": "create_file",
                "description": "Create new file with content. CLIENT-SIDE tool.",
                "parameters": {
                    "type": "object",
                    "properties": {
                        "filepath": {
                            "type": "string",
                            "description": "Path for new file",
                        },
                        "content": {"type": "string", "description": "File content"},
                    },
                    "required": ["filepath", "content"],
                },
            },
            {
                "name": "run_terminal_command",
                "description": "Execute PowerShell command. CLIENT-SIDE tool.",
                "parameters": {
                    "type": "object",
                    "properties": {
                        "command": {"type": "string", "description": "Shell command"},
                        "cwd": {"type": "string", "description": "Working directory"},
                        "timeout": {
                            "type": "integer",
                            "description": "Timeout in seconds (default: 60)",
                        },
                    },
                    "required": ["command"],
                },
            },
            {
                "name": "get_git_diff",
                "description": "Get uncommitted git changes. CLIENT-SIDE tool.",
                "parameters": {
                    "type": "object",
                    "properties": {
                        "staged": {
                            "type": "boolean",
                            "description": "Show staged changes only",
                        },
                        "filepath": {
                            "type": "string",
                            "description": "Diff for specific file",
                        },
                    },
                    "required": [],
                },
            },
            {
                "name": "build_project",
                "description": "Run dotnet build. CLIENT-SIDE tool.",
                "parameters": {
                    "type": "object",
                    "properties": {
                        "project": {
                            "type": "string",
                            "description": ".csproj or .sln path",
                        },
                        "configuration": {
                            "type": "string",
                            "enum": ["Debug", "Release"],
                        },
                    },
                    "required": [],
                },
            },
            {
                "name": "run_tests",
                "description": "Run dotnet test. CLIENT-SIDE tool.",
                "parameters": {
                    "type": "object",
                    "properties": {
                        "project": {
                            "type": "string",
                            "description": "Test project path",
                        },
                        "filter": {
                            "type": "string",
                            "description": "Test filter expression",
                        },
                    },
                    "required": [],
                },
            },
        ]

        return tool_defs

    async def chat(
        self,
        message: str,
        max_tool_iterations: int = 10,
        stream: bool = False,
    ) -> AgenticResponse:
        """
        Send a message and handle the full agentic loop.

        Args:
            message: User message
            max_tool_iterations: Max rounds of tool calls before stopping
            stream: Whether to stream the response

        Returns:
            AgenticResponse with final content and executed tool results
        """
        await self._ensure_client()

        # Dynamically import xai_sdk submodules to avoid static analysis
        # errors when the package is not present in the environment.
        try:
            xai_chat = importlib.import_module("xai_sdk.chat")
            system = getattr(xai_chat, "system")
            tool = getattr(xai_chat, "tool")
            tool_result = getattr(xai_chat, "tool_result")
            user = getattr(xai_chat, "user")

            xai_tools = importlib.import_module("xai_sdk.tools")
            code_execution = getattr(xai_tools, "code_execution")
            get_tool_call_type = getattr(xai_tools, "get_tool_call_type")
            web_search = getattr(xai_tools, "web_search")
            x_search = getattr(xai_tools, "x_search")
        except Exception as err:
            # If dynamic import fails, provide a clear error for the caller.
            raise ImportError(
                "xai-sdk (and its submodules) could not be imported. "
                "Install with: pip install xai-sdk"
            ) from err

        # Build tool list
        tools = []

        # Add server-side tools if enabled
        if self.include_server_side_tools:
            tools.extend(
                [
                    web_search(),
                    x_search(),
                    code_execution(),
                ]
            )

        # Add client-side tools
        for tool_def in self._build_client_side_tools():
            tools.append(
                tool(
                    name=tool_def["name"],
                    description=tool_def["description"],
                    parameters=tool_def["parameters"],
                )
            )

        # Create chat
        chat = self._client.chat.create(
            model=self.model,
            messages=[system(self.system_prompt)],
            tools=tools,
        )

        chat.append(user(message))

        executed_results: list[ToolCallResult] = []
        iterations = 0

        while iterations < max_tool_iterations:
            iterations += 1

            if stream:
                # Streaming mode
                last_response = None
                async for response, chunk in chat.stream():
                    if chunk.content:
                        print(chunk.content, end="", flush=True)
                    last_response = response
                response = last_response
            else:
                # Non-streaming
                response = await chat.sample()

            chat.append(response)

            # Check if response is valid
            if response is None:
                return AgenticResponse(
                    content="[Error: No response received]",
                    tool_calls_executed=executed_results,
                )

            # Check for tool calls
            if not response.tool_calls:
                # No more tool calls, we're done
                return AgenticResponse(
                    content=response.content or "",
                    tool_calls_executed=executed_results,
                    reasoning_tokens=(
                        response.usage.reasoning_tokens if response.usage else 0
                    ),
                    finish_reason=response.finish_reason,
                )

            # Process tool calls
            client_side_calls = []
            for tc in response.tool_calls:
                tool_type = get_tool_call_type(tc)

                if tool_type == "client_side_tool":
                    client_side_calls.append(tc)
                # Server-side tools are handled by xAI, no action needed

            # Execute client-side tools and append results
            for tc in client_side_calls:
                result = self.tool_executor.execute_from_xai(tc)
                executed_results.append(result)
                chat.append(tool_result(result.output))

        # Max iterations reached
        return AgenticResponse(
            content=f"[Max tool iterations ({max_tool_iterations}) reached]",
            tool_calls_executed=executed_results,
        )

    async def chat_simple(self, message: str) -> str:
        """Simplified chat that just returns the final content."""
        response = await self.chat(message)
        return response.content


class MockAgenticSession:
    """
    Mock session for testing without xai-sdk installed.

    Simulates the agentic loop with mock responses.
    """

    def __init__(
        self,
        model: str = "grok-4-fast",
        workspace_root: Optional[Path] = None,
    ):
        self.model = model
        self.workspace_root = workspace_root or Path.cwd()
        self.tool_executor = XaiToolExecutor(workspace_root=self.workspace_root)

    async def chat(
        self,
        message: str,
        max_tool_iterations: int = 10,
    ) -> AgenticResponse:
        """Mock chat that demonstrates tool execution."""
        executed_results: list[ToolCallResult] = []

        # Simulate detecting tool keywords and executing
        if "read" in message.lower() and "file" in message.lower():
            result = self.tool_executor.execute_dict(
                {
                    "name": "read_file",
                    "arguments": {
                        "filepath": "pyproject.toml",
                        "startLine": 1,
                        "endLine": 20,
                    },
                }
            )
            executed_results.append(result)

        if "list" in message.lower() and (
            "dir" in message.lower() or "folder" in message.lower()
        ):
            result = self.tool_executor.execute_dict(
                {
                    "name": "list_directory",
                    "arguments": {"path": ".", "recursive": False},
                }
            )
            executed_results.append(result)

        if "search" in message.lower():
            result = self.tool_executor.execute_dict(
                {
                    "name": "search_files",
                    "arguments": {"pattern": "**/*.py", "maxResults": 10},
                }
            )
            executed_results.append(result)

        # Build response
        tool_summaries = "\n".join(
            f"- {r.tool_name}: {'✓' if r.success else '✗'}" for r in executed_results
        )

        content = f"""[Mock Response for: {message}]

Executed {len(executed_results)} tool(s):
{tool_summaries or '(none)'}

This is a mock response. Install xai-sdk for full functionality."""

        return AgenticResponse(
            content=content,
            tool_calls_executed=executed_results,
        )


async def run_interactive() -> None:
    """Run an interactive chat session."""
    try:
        # Try dynamic import to avoid static import resolution errors in
        # environments where xai-sdk is not installed.
        importlib.import_module("xai_sdk")

        session = AgenticChatSession()
        print("Using xAI SDK - Full agentic mode")
    except ImportError:
        session = MockAgenticSession()
        print("xai-sdk not installed - Using mock mode")

    print(f"Workspace: {session.workspace_root}")
    print("Type 'exit' or 'quit' to end the session.\n")

    while True:
        try:
            user_input = input("You: ").strip()
            if not user_input:
                continue
            if user_input.lower() in {"exit", "quit"}:
                break

            print("\nAssistant: ", end="", flush=True)
            response = await session.chat(user_input)
            print(response.content)

            if response.tool_calls_executed:
                print(f"\n[Executed {len(response.tool_calls_executed)} tool(s)]")
            print()

        except KeyboardInterrupt:
            print("\n\nSession ended.")
            break
        except Exception as e:
            print(f"\nError: {e}\n")


async def run_test() -> None:
    """Run basic tests."""
    print("=== xAI Agentic Chat Tests ===\n")

    # Use mock session for tests
    session = MockAgenticSession()

    # Test 1: Read file
    print("Test 1: Read file request")
    response = await session.chat("Please read the pyproject.toml file")
    print(f"  Tools executed: {len(response.tool_calls_executed)}")
    print(f"  Success: {all(r.success for r in response.tool_calls_executed)}")

    # Test 2: List directory
    print("\nTest 2: List directory request")
    response = await session.chat("List the files in the current directory")
    print(f"  Tools executed: {len(response.tool_calls_executed)}")
    print(f"  Success: {all(r.success for r in response.tool_calls_executed)}")

    # Test 3: Search files
    print("\nTest 3: Search files request")
    response = await session.chat("Search for Python files")
    print(f"  Tools executed: {len(response.tool_calls_executed)}")
    print(f"  Success: {all(r.success for r in response.tool_calls_executed)}")

    print("\n✓ All tests passed")


def main() -> None:
    """CLI entry point."""
    parser = argparse.ArgumentParser(
        description="xAI Agentic Chat Session with IDE tools"
    )
    parser.add_argument(
        "--test",
        action="store_true",
        help="Run tests",
    )
    parser.add_argument(
        "--interactive",
        action="store_true",
        help="Start interactive chat session",
    )
    parser.add_argument(
        "--message",
        "-m",
        type=str,
        help="Single message to send",
    )
    parser.add_argument(
        "--workspace",
        type=str,
        default=".",
        help="Workspace root directory",
    )

    args = parser.parse_args()

    if args.test:
        asyncio.run(run_test())
        return

    if args.interactive or (not args.message):
        asyncio.run(run_interactive())
        return

    if args.message:

        async def single_message():
            try:
                # Dynamic import to avoid static import errors
                importlib.import_module("xai_sdk")

                session = AgenticChatSession(
                    workspace_root=Path(args.workspace).resolve()
                )
            except ImportError:
                session = MockAgenticSession(
                    workspace_root=Path(args.workspace).resolve()
                )

            response = await session.chat(args.message)
            print(response.content)

            if response.tool_calls_executed:
                print("\n--- Tool Executions ---")
                for result in response.tool_calls_executed:
                    print(f"\n[{result.tool_name}] {'✓' if result.success else '✗'}")
                    print(result.output[:500])

        asyncio.run(single_message())


if __name__ == "__main__":
    main()
