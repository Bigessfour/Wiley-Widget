# MCP Filesystem Guidelines (short)

This document gives concise, actionable guidance for using the MCP filesystem tools safely and avoiding EISDIR ("Is a directory") errors when reading files.

## Principles

- Use the MCP filesystem APIs for all repository file operations (read/write/list/edit).
- Always call the activators before using MCP filesystem tools:
  - `activate_file_reading_tools()` for reads
  - `activate_directory_and_file_creation_tools()` for writes/edits
- Prefer safe, centralized helpers that normalize and restrict paths rather than making ad-hoc file calls across the codebase.

## Preventing EISDIR (common patterns)

1. Normalize and restrict paths
   - Use `Path.GetFullPath` combined with a canonical base root check (e.g., `AppDomain.CurrentDomain.BaseDirectory`) to prevent directory traversal attacks.
   - Reject or sanitize inputs that resolve outside the application root.

2. Detect path type before performing an operation
   - Check `Directory.Exists(fullPath)` and `File.Exists(fullPath)` to decide whether to list or read.
   - Provide separate behaviors:
     - `ListDirectory` for directories (returns list)
     - `ReadFile` for files (returns content)
     - `ReadOrList` wrapper that chooses automatically (returns string listing or file content)

3. Fail fast with clear errors
   - Throw `InvalidOperationException` for directory input to a file-read API (make the contract explicit).
   - Throw `ArgumentException` for malformed or empty paths; `FileNotFoundException` when the file isn't present.

4. Size limits and other safety checks
   - Limit file sizes for reads (e.g., 5 MB) to avoid resource exhaustion.
   - Consider restricting allowable file extensions if appropriate.

5. Test the edge cases
   - Unit tests covering: file reads, directory inputs, path traversal attempts, oversized files, and symlink edge cases.

## MCP APIs — recommended mapping

- To check a path: use `mcp_filesystem_list_directory` / `mcp_filesystem_read_text_file` as appropriate.
- To do edits: use `mcp_filesystem_edit_file` or `mcp_filesystem_write_file`.
- Avoid using non-MCP file APIs for repository edits (no shell `echo > file` or manual `git` operations in agents).

## Example (C# plugin helper concept)

- `ResolvePath(relativePath)`
  - Combines `AppDomain.CurrentDomain.BaseDirectory` and `Path.GetFullPath` and verifies that the resulting absolute path starts with the base directory.

- `ReadFile(relativePath)`
  - Resolve path, ensure it's a file (not a directory), enforce size limit, then return contents.

- `ListDirectory(relativeDir)`
  - Resolve path, ensure it's a directory, enumerate entries and return relative paths.

## CI and verification

- Add unit tests that cover misuse scenarios (EISDIR, path traversal).
- Add a CI step to run `dotnet restore` for the solution in PR checks to detect TFM mismatch early.

---

If you want, I can expand this into a short section under `.vscode` guidance and/or add a short test matrix that verifies common failure paths.
