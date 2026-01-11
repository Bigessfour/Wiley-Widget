# Unreleased

### WW0001 - Avoid Color.FromArgb usage

- Initial implementation (2026-01-04): Emit a warning when code uses Color.FromArgb to encourage use of SfSkinManager/ThemeColors.

### WW0002 - MemoryCacheEntryOptions missing required Size property

- Initial implementation (2026-01-10): Emit a warning when MemoryCacheEntryOptions is created without explicit Size property when SizeLimit is configured on MemoryCache.
- Detects: `new MemoryCacheEntryOptions()` or `new MemoryCacheEntryOptions { /* no Size */ }`
- Suggests: Add `Size = 1` (or appropriate value) to the initializer
- Reference: [Microsoft Caching Memory - SetSize, Size, and SizeLimit](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/memory#use-setsize-size-and-sizelimit-to-limit-cache-size)
