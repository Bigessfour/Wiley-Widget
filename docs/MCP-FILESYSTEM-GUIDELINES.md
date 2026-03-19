# MCP Filesystem Guidelines

Use these rules when working with repository files through MCP.

## Read Safely

1. List first.
2. Read second.
3. Re-read only when the file changed or new context is required.

This prevents directory-read mistakes and keeps repeated file scanning under control.

## Path Rules

- Use workspace-relative paths with MCP filesystem tools.
- Use absolute paths only for tools that explicitly require them.
- Confirm the target exists before editing.

## Editing Rules

- Prefer precise edits over broad rewrites unless the doc is clearly stale end to end.
- When a doc references a missing file, either fix the link or create the missing file in the same change.
- Keep release-facing docs truthful, even if that means removing older guidance.

## Validation Rules

- After documentation changes, verify that referenced files exist.
- Do not count historical reports or generated snapshots as current evidence.
