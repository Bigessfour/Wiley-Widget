# MCP Tool Usage Guide

This document shows correct usage patterns for the MCP filesystem tools available to the Copilot Code Agent (MCP), so you avoid EISDIR errors and other incorrect file-system operations.

## Key rules

- Use `mcp_filesystem_list_directory` to inspect directories; do NOT use file-read tools to open directories.
- Use `mcp_filesystem_read_text_file` to read textual files (source code, text, JSON, XML, .csproj). The function supports `head` and `tail` parameters for partial reads.
- Use `mcp_filesystem_read_media_file` for binary or media files (images, audio) where you need base64-encoded content.
- Use `mcp_filesystem_read_multiple_files` to read more than one file in the same call.

## Example: Common patterns

1. List a directory:
   - Call: `mcp_filesystem_list_directory({ path: "tests" })`
   - Output: list of files and directories with `[FILE]`/`[DIR]` markers.

2. Read a file's text:
   - Call: `mcp_filesystem_read_text_file({ path: "tests/WileyWidget.IntegrationTests/WileyWidget.IntegrationTests.csproj", head: 200 })`
   - Notes: Use `head` to read first N lines, `tail` to read last N lines.

3. Read a directory (WRONG approach) — leads to EISDIR:
   - Wrong: `mcp_filesystem_read_text_file({ path: "tests" })` -> EISDIR

4. Read a media file (PDF, image):
   - Use: `mcp_filesystem_read_media_file({ path: "docs/diagram.png" })` -> returns base64 and MIME type.

## Troubleshooting

- If you see `EISDIR: illegal operation on a directory, read`, switch back to `mcp_filesystem_list_directory` to explore the directory's content and then read specific files using `mcp_filesystem_read_text_file` or `mcp_filesystem_read_media_file`.
- If a file exists but a build fails with "Could not copy the file... because it was not found", rebuild with `dotnet build` using `--verbosity diagnostic` to get detailed output and check the project's `obj` and `bin` folders.

## Notes for agents

- Always validate that the path points to a file before reading it with a file-read function. If a path is a directory, use the directory-listing tool instead.
- `mcp_filesystem_list_directory` can be used recursively to investigate nested structures.

This guide helps developers and agents avoid common MCP tool usage errors.
