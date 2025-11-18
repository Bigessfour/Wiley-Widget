# Nullable Reference Types - Migration Guidance for WileyWidget

This document describes a conservative, low-risk approach to enabling nullable reference types for parts of the WileyWidget repository.

Goal

- Improve null-safety gradually by enabling nullable reference types in new or refactored code (WPF ViewModels, converters, and new services) while keeping legacy code unchanged.

Strategy

1. Do not flip `Nullable` globally right away. Instead prefer per-file or per-project enabling.
2. Add helper `.editorconfig` diagnostics (already added) to surface common nullability issues as warnings.
3. Use `#nullable enable` in specific files or add a lightweight `Directory.Build.props` for new projects.
4. Use `#nullable disable` at top of legacy files you can't change immediately.
5. Treat warnings as errors incrementally in CI once the codebase is stable.

Quick snippets

- Per-file opt-in (recommended for selective migration):

  At the top of a C# file you want to migrate:

  ```csharp
  #nullable enable
  // file contents
  ```

- Project opt-in (for a single project):

  Edit the project's `.csproj` and add the following inside a `<PropertyGroup>`:

  ```xml
  <Nullable>enable</Nullable>
  <WarningsAsErrors>$(WarningsAsErrors);CS8602;CS8603</WarningsAsErrors>
  ```

  Only add `WarningsAsErrors` after you have a green pipeline on that project.

- Centralized sample (safe to copy under a new file and include manually):

  `nullable-enable.props` (do not auto-apply):

  ```xml
  <Project>
    <PropertyGroup>
      <Nullable>enable</Nullable>
      <LangVersion>latest</LangVersion>
    </PropertyGroup>
  </Project>
  ```

- `.editorconfig` nullability diagnostics (we added these already):

  The repository now maps common `CS86xx` diagnostics to `warning` so editors will surface them.

Migration checklist

- [ ] Identify key UI projects (e.g., `WileyWidget.UI`, `WileyWidget.Startup`) and pick one small project to migrate first.
- [ ] Convert a couple of ViewModels and fix nullability warnings.
- [ ] Add `#nullable enable` to migrated files.
- [ ] After the project is stable, flip the project's `Nullable` to `enable` and run CI.
- [ ] Gradually increase strictness (WarningsAsErrors) for that project.

Troubleshooting

- If you enable nullable in a file and see many warnings, use `#nullable enable` on a smaller surface area first and address public API annotations.
- Use `Nullable` pragmas or `!` (null-forgiving) only as a last resort; prefer correct annotations and checks.

Notes

- Enabling nullable is low-risk if done incrementally. It catches many runtime null issues earlier and improves developer confidence, especially in WPF ViewModels and data-bound code.

If you'd like, I can:

- Add a sample `nullable-enable.props` file under `signing/` or `scripts/` for maintainers to copy into a project.
- Apply `#nullable enable` to a specific file you pick (for example, a ViewModel) and fix the resulting warnings.

Tell me which option you prefer next.
