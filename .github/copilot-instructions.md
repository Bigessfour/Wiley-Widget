# Python Standards

This repository prefers consistent, lint-friendly Python code. Add this block to guide Copilot suggestions and help reviewers.

- Use PEP 8 style: 4 spaces indent, no tabs.
- Prefer snake_case for variables/functions.
- Always add type hints (e.g., def func(x: int) -> str:).
- No unused imports or variables—clean that up.
- Include docstrings for functions/classes.
- Avoid deprecated stuff; prefer modern libs like pathlib over os.path.
- Aim to pass Pylint/Flake8/Ruff in CI; disable rules only when necessary (e.g., --disable=missing-docstring for quick prototypes).

Notes

- These guidelines are meant to produce code that is lint-happy and review-friendly without being overly strict for prototypes.
- Tailor the exact lint rule list in `.vscode/settings.json` per your team's tolerances.

# C# Standards

This repository prefers modern, analyzer-friendly C# code for the WinForms/.NET codebase and related scripts.

- Follow Microsoft style: camelCase for locals, PascalCase for methods/properties.
- Use var only when the type is obvious from the right-hand side.
- Add XML doc comments for public members.
- Prefer async/await and avoid blocking calls (no .Result/.Wait()).
- Remove unused using directives and variables; keep IDEs quiet.
- Adhere to `.editorconfig` rules (for example, indent_size=4).
- Target .NET 8+ where possible and use newer features (e.g., records, file-scoped namespaces).

These guidelines train Copilot to produce analyzer-friendly code and reduce noise from Roslyn/C# analyzers.

# PowerShell Standards

This repository prefers modern, cross-platform, PSScriptAnalyzer-friendly PowerShell scripts.

- Use PowerShell 7+ idioms and avoid legacy cmdlets (e.g., prefer Get-CimInstance over Get-WmiObject).
- Functions follow Verb-Noun naming and include param blocks with types and validation attributes.
- Include proper error handling (try/catch) and use ShouldProcess for operations that change state.
- Output structured objects instead of text (avoid Write-Host for data outputs).
- Provide comment-based help and accompanying Pester tests for modules and functions.
- Aim to pass PSScriptAnalyzer (e.g., avoid PSUseShouldProcessForStateChangingFunctions where intentional).
- Keep scripts cross-platform (Windows/Linux) and avoid hard-coded platform-specific paths/secrets.

These guidelines prime Copilot to generate PowerShell code that's analyzer-friendly and production safe.
