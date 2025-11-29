# Local C# evaluator (no Docker)

This folder contains a small dotnet-based C# evaluator that can run C# snippets or script files without Docker.

Why this exists

- Quick, lightweight alternative to the csharp-mcp container for debugging and local experiments.
- Useful from the Python environment or CI where you don't want to use Docker.

Location

- Project: scripts/tools/csharp_mcp_local
- CLI wrapper: scripts/tools/csharp_eval.py

Usage examples

- Evaluate a simple expression from Python:

```pwsh
python scripts/tools/csharp_eval.py --expr "1+2"
```

- Evaluate a script file (e.g., debug_combo.csx):

```pwsh
python scripts/tools/csharp_eval.py --file scripts/tools/debug_combo.csx
```

- Send JSON payload:

```pwsh
python scripts/tools/csharp_eval.py --json '{"code":"System.Environment.MachineName"}'
```

Notes & caveats

- Syncfusion is a WinForms-targeted library and may require additional packages or runtime compatibility; the local evaluator attempts best-effort behavior but may encounter assembly or licensing issues for heavy libraries.
- The evaluator uses dotnet run to execute the project dynamically. For frequent use, consider publishing the project and launching the binary directly for speed.

Docker support & behavior

- The wrapper now includes optional Docker-awareness. You can request the tool to prefer Docker or try to start Docker Desktop on Windows.

Flags available in the Python wrapper (scripts/tools/csharp_eval.py):

- --prefer-docker: checks whether Docker is running and prints a message if it is; this is informational — the wrapper does not attempt to run container evaluations automatically.
- --ensure-docker: if Docker is not running and you're on Windows, the wrapper will attempt to launch Docker Desktop (best-effort) and wait up to ~30 seconds for the daemon to become available. If it does not become available the wrapper falls back to the local evaluator and informs you.

Why Docker matters here

- Some libraries used during evaluation (particularly Syncfusion and WinForms-related packages) may have platform-specific requirements or licensing behavior that are best handled in the pre-built Docker image we ship (`ghcr.io/infinityflowapp/csharp-mcp:latest`). Using Docker ensures consistent runtime and dependencies similar to CI.

When to prefer Docker

- Use Docker when you need parity with the CI/test environment, especially for code that touches Syncfusion, System.Drawing, WinForms, or platform-specific APIs.
- Use the local evaluator for quick, lightweight experiments that don't rely on those heavy runtime dependencies.

Next improvements (ideas)

- Add an always-running server mode with stdin/stdout protocol.
- Add a simple TCP/HTTP JSON RPC wrapper for easier remote debugging.
- Improve assembly-reference resolution and support loading PackageReferences dynamically.
