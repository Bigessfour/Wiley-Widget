# Analyzer Releases (Unshipped)

## Unshipped

### WW0001 - Avoid Color.FromArgb usage

- Initial implementation (2026-01-04): Emit a warning when Code uses Color.FromArgb to encourage usage of SfSkinManager/ThemeColors.

### WW0002 - MemoryCacheEntryOptions missing required Size property

- Initial implementation (2026-01-10): Emit a warning when MemoryCacheEntryOptions is created without explicit Size property when SizeLimit is configured on MemoryCache.
- Detects: `new MemoryCacheEntryOptions()` or object initializers without Size property
- Suggests: Add `Size = 1` (or appropriate value) to the initializer when SizeLimit is configured

### Notes

- This file documents analyzers and diagnostics that are implemented but not yet included in a shipped package or release. The ReleaseTracking analyzers scan this file to verify a release-tracking entry exists for each diagnostic id.
