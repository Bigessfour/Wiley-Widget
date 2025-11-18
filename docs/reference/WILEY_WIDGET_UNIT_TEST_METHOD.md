# Wiley Widget Unit Test Method (C# .csx + MCP)

This guide standardizes how we write and run C# script-based unit tests (.csx) for Wiley Widget using the C# MCP evaluation environment. It includes a robust test template, Windows-friendly run instructions (PowerShell), and CI notes so tests run the same locally and in pipelines.

## Why .csx tests

- Fast, self-contained scripts for evaluating target code or small units in isolation
- Clear pass/fail via exit code (0 = pass, 1 = fail)
- Portable execution inside Docker for deterministic results

## What success looks like

- Tests use clear assertions with meaningful error messages
- Floating point comparisons use tolerance
- Collections are compared structurally
- Failures produce actionable output and non-zero exit

---

## Wiley Widget specifics (Shell, paths, image names)

The Wiley Widget repo primarily targets Windows with PowerShell (pwsh) as the default shell.

- Default shell: PowerShell 7+ (pwsh)
- Recommended test script location: `tests/csx/` (create if missing)
- Suggested local Docker image tag: `wiley-widget/csx-mcp:local`
- When writing wrapper scripts in pwsh, set:
  - `-NoProfile -ExecutionPolicy Bypass` on invocations
  - `$ErrorActionPreference = "Stop"` to fail fast
  - Quote volume paths: `-v "${PWD}/tests/csx:/app"` (handles spaces in paths)

Optional pwsh preamble for reliability:

```powershell
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
```

### PowerShell quality and analyzer requirements (mandatory)

- Version and language compatibility:
  - Place `#requires -Version 7.5.4` at the top of any PowerShell wrapper scripts used to orchestrate tests.
  - Author scripts targeting PowerShell 7.5.4 language features only.
- Output and messaging:
  - Do not use `Write-Host` (fails PSAvoidUsingWriteHost). Prefer `Write-Information` for human-readable status and `Write-Output` for pipeline data.
  - Set `$InformationPreference = 'Continue'` if you want informational messages emitted by default.
- Naming and design:
  - Use approved verbs for functions (e.g., Get/Set/New/Remove/Test/Start/Stop/Invoke/Enable/Disable). See PSUseApprovedVerbs.
  - Use `PascalCase` for function names and `camelCase` for parameters.
  - For functions that change state or external resources, add `[CmdletBinding(SupportsShouldProcess)]` and gate with `if ($PSCmdlet.ShouldProcess(...))`.
- Error handling and strictness:
  - `Set-StrictMode -Version Latest` and use `try { } catch { throw }` patterns. Avoid unhandled non-terminating errors; use `-ErrorAction Stop`.
  - Validate parameters with attributes (e.g., `[ValidateNotNullOrEmpty()]`).
- Encoding:
  - Save scripts as UTF-8. If required by downstream Windows tooling, save as UTF-8 with BOM.
    - PowerShell 7.3+ example: `Set-Content -Path .\script.ps1 -Encoding utf8BOM -Value $content`
    - Legacy alternative: `[System.IO.File]::WriteAllText($path, $content, New-Object System.Text.UTF8Encoding($true))`
- Analyzer gate (must pass before running tests):
  - Via Trunk (preferred): `trunk check --filter=psscriptanalyzer --fix`
  - Or directly (module must be installed):
    ```powershell
    Import-Module PSScriptAnalyzer -MinimumVersion 1.24.0
    Invoke-ScriptAnalyzer -Path .\tests -Recurse -EnableExit
    ```

---

## .csx Test Template

Use this as your starting point. Replace placeholders like <TestSuiteName>, <TestName>, and integrate your target code under test (inline or by loading a file).

```csharp
// <TestSuiteName> - C# Evaluation Test
// This script tests <brief description of scope>.
// Target: <path or description of the thing being tested>

using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;

Console.WriteLine($"=== <TestSuiteName> ===\n");

try {
    // Setup: load or define target code/context (optional)
    // Example: var targetSource = File.ReadAllText("<relative-or-absolute-path>");

    // Test 1: <TestName1>
    Console.WriteLine("Test 1: <TestName1>");
    var msg = "Hello from Target";
    var answer = 42;
    var pi = 3.14159;

    AssertEqual("String", msg, "Hello from Target", "String mismatch");
    AssertEqual("Integer", answer, 42, "Integer mismatch");
    AssertEqual("Double", pi, 3.14159, "Double mismatch", tolerance: 0.0001);

    // Test 2: <TestName2 - Collections/LINQ>
    Console.WriteLine("\nTest 2: <TestName2>");
    var nums = new[] { 1,2,3,4,5,6,7,8,9,10 };
    var evens = nums.Where(n => n % 2 == 0).ToList();
    var sum = nums.Sum();

    AssertCollectionEqual("Evens", evens, new List<int> { 2,4,6,8,10 }, "Even numbers mismatch");
    AssertEqual("Sum", sum, 55, "Sum mismatch");

    // Test 3: <TestName3 - Exceptions>
    Console.WriteLine("\nTest 3: <TestName3>");
    AssertThrows<ArgumentException>("Throws on bad input", () =>
    {
        // simulate invalid operation
        throw new ArgumentException("bad input");
    }, "Expected ArgumentException not thrown");

    // Test 4: <Performance check - optional>
    Console.WriteLine("\nTest 4: <Performance>");
    var elapsed = MeasureTime(() => {
        var x = 0; for (var i = 0; i < 100_000; i++) x += i;
    });
    AssertLessThan("Perf under 200ms", elapsed, TimeSpan.FromMilliseconds(200));

    Console.WriteLine("\n✅ All tests completed successfully!");
    return 0; // success
} catch (Exception ex) {
    Console.WriteLine($"\n❌ Test failed: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    return 1; // failure
}

// ===== Assertion helpers =====
void AssertEqual<T>(string testName, T actual, T expected, string failureMessage, double tolerance = 0)
{
    if (typeof(T) == typeof(double))
    {
        if (Math.Abs((double)(object)actual - (double)(object)expected) > tolerance)
            throw new Exception($"{testName} failed: {failureMessage}. Actual: {actual}, Expected: {expected}, Tol: {tolerance}");
    }
    else if (!EqualityComparer<T>.Default.Equals(actual, expected))
    {
        throw new Exception($"{testName} failed: {failureMessage}. Actual: {actual}, Expected: {expected}");
    }
    Console.WriteLine($"{testName}: Passed");
}

void AssertCollectionEqual<T>(string testName, IEnumerable<T> actual, IEnumerable<T> expected, string failureMessage)
{
    if (!actual.SequenceEqual(expected))
        throw new Exception($"{testName} failed: {failureMessage}. Actual: [{string.Join(", ", actual)}], Expected: [{string.Join(", ", expected)}]");
    Console.WriteLine($"{testName}: Passed");
}

void AssertTrue(string testName, bool condition, string failureMessage)
{
    if (!condition) throw new Exception($"{testName} failed: {failureMessage}");
    Console.WriteLine($"{testName}: Passed");
}

void AssertThrows<TEx>(string testName, Action action, string failureMessage) where TEx : Exception
{
    try { action(); }
    catch (Exception ex) {
        if (ex is TEx) { Console.WriteLine($"{testName}: Passed"); return; }
        throw new Exception($"{testName} failed: Expected {typeof(TEx).Name}, got {ex.GetType().Name}");
    }
    throw new Exception($"{testName} failed: {failureMessage}");
}

TimeSpan MeasureTime(Action action)
{
    var sw = Stopwatch.StartNew();
    action();
    sw.Stop();
    return sw.Elapsed;
}

void AssertLessThan(string testName, TimeSpan actual, TimeSpan threshold)
{
    if (actual > threshold)
        throw new Exception($"{testName} failed: Took {actual.TotalMilliseconds} ms, threshold {threshold.TotalMilliseconds} ms");
    Console.WriteLine($"{testName}: Passed");
}
```

### Targeting real code

- Inline code: paste a minimal slice under test directly in the script
- Load file content: `var src = File.ReadAllText("relative/path/to/file.cs");`
- Call compiled code: if running in an environment that exposes your compiled assemblies, reference and invoke types directly; otherwise, favor testable pure functions embedded into the script for evaluation

#### Referencing Wiley Widget compiled assemblies (optional)

To test real compiled code, you can reference the built DLLs from a .csx script.

1. Build the project locally (adjust configuration/TFM as needed):

```powershell
# Example: Build Wiley Widget
dotnet build .\WileyWidget.csproj -c Release
```

2. In your `.csx`, reference the DLLs using `#r` (paths are relative to the script’s working directory inside the container):

```csharp
#r "bin/Release/net8.0/WileyWidget.Services.dll"
#r "bin/Release/net8.0/WileyWidget.Models.dll"

using WileyWidget.Services;
using WileyWidget.Models;
```

3. When running in Docker, mount both the `tests/csx` folder and the compiled output:

```powershell
# Mount tests and compiled output as read-only
docker run -it --rm `
    -v "${PWD}/tests/csx:/app" `
    -v "${PWD}/bin/Release/net8.0:/app/bin/Release/net8.0:ro" `
    wiley-widget/csx-mcp:local compiled-api-test.csx
```

---

## Run tests in Docker (Windows PowerShell)

Prereqs: Docker Desktop, a folder containing your `.csx` test(s), and a Docker image with `dotnet script` support.
Repo requirement: .NET SDK 9 (per `global.json`). Use the .NET 9 SDK image by default.
Before any Docker run, your PowerShell wrapper scripts (if used) MUST pass PSScriptAnalyzer, using Trunk or direct invocation, per the Analyzer gate above.

0. Analyzer gate (mandatory) — choose one:
   - Trunk: `trunk check --filter=psscriptanalyzer --fix`
   - Direct: `Invoke-ScriptAnalyzer -Path .\tests -Recurse -EnableExit`

1. Create a Dockerfile tailored for csx tests (recommended path: `docker/Dockerfile.csx-tests`):

```dockerfile
# docker/Dockerfile.csx-tests
FROM mcr.microsoft.com/dotnet/sdk:9.0

ARG DOTNET_CLI_TELEMETRY_OPTOUT=1
ARG DOTNET_NOLOGO=1
ENV DOTNET_CLI_TELEMETRY_OPTOUT=${DOTNET_CLI_TELEMETRY_OPTOUT}
ENV DOTNET_NOLOGO=${DOTNET_NOLOGO}

WORKDIR /app

# Install dotnet-script for .csx execution
RUN dotnet tool install -g dotnet-script \
 && echo 'export PATH="$PATH:/root/.dotnet/tools"' >> /etc/profile \
 && echo 'PATH="$PATH:/root/.dotnet/tools"' >> /etc/environment

ENV PATH="$PATH:/root/.dotnet/tools"

ENTRYPOINT ["dotnet", "script"]
```

2. Build the image (from your repo root where the Dockerfile resides):

```powershell
# Build with an explicit Dockerfile
docker build -t wiley-widget/csx-mcp:local -f docker/Dockerfile.csx-tests .
```

3. Run a test script by mounting your tests folder:

```powershell
# Assuming tests live in .\tests\csx and you have .\tests\csx\basic-test.csx
# ${PWD} works in pwsh; keep quotes for spaces
docker run -it --rm -v "${PWD}/tests/csx:/app" wiley-widget/csx-mcp:local basic-test.csx

# With compiled outputs mounted (optional)
docker run -it --rm `
    -v "${PWD}/tests/csx:/app" `
    -v "${PWD}/bin/Release/net8.0:/app/bin/Release/net8.0:ro" `
    wiley-widget/csx-mcp:local compiled-api-test.csx
```

Notes for Windows Docker Desktop:

- If your daemon uses Linux containers (default), keep quotes around volume paths to preserve spaces
- If `${PWD}` doesn’t resolve correctly, substitute an absolute path
- Add `:ro` to mounts for read-only access inside the container

### Approved CSX Test Image (Wiley Widget)

- Image tag: `wiley-widget/csx-mcp:local`
- Built from: `docker/Dockerfile.csx-tests`
- Purpose: Executes `.csx` tests using `dotnet script` on .NET SDK 9.0 (required by repo `global.json`)
- Build:
  ```powershell
  docker build -t wiley-widget/csx-mcp:local -f docker/Dockerfile.csx-tests .
  ```
- Run (example):

  ```powershell
  docker run -it --rm -v "${PWD}/tests/csx:/app" wiley-widget/csx-mcp:local basic-test.csx
  ```

- Exit code 0 = success, 1 = failure. CI can key off this.
- For debugging, remove `--rm` and reattach as needed.

### Optional: Local execution

If you have dotnet-script installed locally, you can run:

```powershell
# Install once (if needed)
# dotnet tool install -g dotnet-script
# dotnet script --version

dotnet script .\tests\basic-test.csx
```

---

## CI integration (GitHub Actions example)

Below adds a simple job to run a specific csx test. Adapt path/image names as needed.

```yaml
name: Run C# .csx Tests
on: [push, pull_request]
jobs:
    csx-tests-linux:
        runs-on: ubuntu-latest
        steps:
            - uses: actions/checkout@v4
            - name: Build csx test image
                run: docker build -t wiley-widget/csx-mcp:ci -f docker/Dockerfile.csx-tests .
            - name: Run basic csx test
                run: docker run --rm -v "$(pwd)/tests/csx:/app" wiley-widget/csx-mcp:ci basic-test.csx

    csx-tests-windows:
        runs-on: windows-latest
        steps:
            - uses: actions/checkout@v4
            - name: Build csx test image (Windows runner)
                run: docker build -t wiley-widget/csx-mcp:ci -f docker/Dockerfile.csx-tests .
            - name: Run basic csx test
                run: docker run --rm -v "${{ github.workspace }}\\tests\\csx:/app" wiley-widget/csx-mcp:ci basic-test.csx
```

If you prefer Windows agents, use `runs-on: windows-latest` and `"${{ github.workspace }}\\tests:/app"` volume syntax.

---

## Authoring guidance and accuracy boosters

- Prefer pure functions and deterministic data where possible
- Always use the assertion helpers; don’t rely on ad-hoc `if` statements
- Add tolerance for floating point, and structural comparisons for collections
- Cover edge cases (null/empty, extremes, error paths)
- Add an exception test via `AssertThrows<T>()` when validating error behavior
- For performance-sensitive functions, add a coarse-grained timing assertion with generous thresholds
- Make output scannable: each test section prints a heading and uses consistent labels

### Contract (tiny)

- Input: none (script executes self-contained logic or loads target file)
- Output: console text; exit code 0 on success, 1 on first failing assertion/exception
- Error modes: any failed assertion throws with a clear, labeled message; script prints stack trace
- Success criteria: all assertions pass, final success banner prints

### Typical edge cases to cover

- Empty collections and null inputs
- Negative/zero/maximum values
- Non-happy-path control flow (exceptions, early returns)
- Locale/format-sensitive operations (parsing, casing)
- Large inputs or repeated operations (performance)

---

## Minimal example (drop in tests/basic-test.csx)

This is a ready-to-run sample you can adapt.

```csharp
// Wiley Widget - Basic Sanity Test
using System;
using System.Linq;
using System.Collections.Generic;

try {
    Console.WriteLine("=== Basic Sanity Test ===\n");
    var data = Enumerable.Range(1, 5).ToList();
    AssertCollectionEqual("Range(1..5)", data, new List<int>{1,2,3,4,5}, "Range mismatch");
    Console.WriteLine("\n✅ All tests completed successfully!");
    return 0;
} catch (Exception ex) {
    Console.WriteLine($"\n❌ Test failed: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    return 1;
}

void AssertCollectionEqual<T>(string testName, IEnumerable<T> actual, IEnumerable<T> expected, string failureMessage)
{
    if (!actual.SequenceEqual(expected))
        throw new Exception($"{testName} failed: {failureMessage}. Actual: [{string.Join(", ", actual)}], Expected: [{string.Join(", ", expected)}]");
    Console.WriteLine($"{testName}: Passed");
}
```

---

## Troubleshooting

- “dotnet: command not found” in container: ensure SDK image and (if needed) dotnet-script tool installed; see Dockerfile section
- Volume mount path issues on Windows: keep quotes around `"${PWD}/tests:/app"`; ensure the `tests` folder exists
- Non-zero exit without error text: verify try/catch wrapper is intact and that exceptions aren’t being swallowed
- Script cannot find referenced DLLs: confirm the container’s working directory and your `#r` paths align with mounted volumes
- Mixed SDK/TFM: default image uses .NET 8; to test .NET 9 as well, duplicate the Dockerfile with `mcr.microsoft.com/dotnet/sdk:9.0` and adjust `bin/Release/net9.0` paths

---

## Notes for CI/CD alignment

- This .csx method complements existing .NET test projects, and can be invoked from workflows alongside Trunk checks
- Ensure csx test steps propagate exit code so failures fail the job
- Keep scripts small and focused; prefer separate .csx files per subject under `tests/`

```
Example paths:
- docs/WILEY_WIDGET_UNIT_TEST_METHOD.md (this guide)
- tests/csx/basic-test.csx (sample script)
```

---

## Included CSX E2E scripts (20 & 21)

This repository includes two streamed, log-based end-to-end (E2E) C# script tests intended to run inside the C# MCP server container. Add these to your local testing and CI playbooks for quick Prism/DI/module validation without launching the full WPF shell.

- `scripts/examples/csharp/20-prism-container-e2e-test.csx`
  - Purpose: Validate Prism container wiring, DI factory checks, ViewModel factory behavior, and root-cause logging by scanning the latest startup logs.
  - Exit codes: 0 = all assertions passed; 2 = logs directory or log file not found; 3 = one or more test assertions failed.

- `scripts/examples/csharp/21-prism-modules-e2e-test.csx`
  - Purpose: Validate Prism module catalog configuration, region adapter registrations, module lifecycle messages, and fail-friendly module initialization using streamed log parsing.
  - Exit codes: same convention as script 20.

Why use these scripts

- Fast, non-invasive checks that parse existing startup logs.
- Safe to run in CI or inside a container (no display or WPF UI required).

Run scripts locally using the csx test runner image (recommended)

1. Build the csx runner image (matches repo .NET SDK via `global.json`):

```powershell
docker build -t wiley-widget/csx-mcp:local -f docker/Dockerfile.csx-tests .
```

2. Run script 20 (isolated script mount + separate logs mount to avoid project scanning):

```powershell
docker run --rm -w /app \
    -v "${PWD}/scripts/examples/csharp:/app:ro" \
    -v "${PWD}/logs:/logs:ro" \
    -e WW_REPO_ROOT=/app -e WW_LOGS_DIR=/logs \
    wiley-widget/csx-mcp:local 20-prism-container-e2e-test.csx
```

3. Run script 21:

```powershell
docker run --rm -w /app \
    -v "${PWD}/scripts/examples/csharp:/app:ro" \
    -v "${PWD}/logs:/logs:ro" \
    -e WW_REPO_ROOT=/app -e WW_LOGS_DIR=/logs \
    wiley-widget/csx-mcp:local 21-prism-modules-e2e-test.csx
```

Using VS Code tasks:

- `Terminal` > `Run Task…` > `csx:run-20`
- `Terminal` > `Run Task…` > `csx:run-21`

Notes:

- The csx runner image entrypoint is `dotnet script`. We mount only the `scripts/examples/csharp` folder into `/app` and set `-w /app` to avoid scanning the repository root with multiple `.csproj` files.
- If you see "Specify which project file to use because this ... contains more than one project file", you likely mounted too broadly. Use the isolated mount above, or run `dotnet script` with `--cwd` from the script folder, or copy the script to `/tmp` and execute from there.
- If `csc.exe` is missing when compiling manually, continue to use `dotnet script` or this Docker image; it contains the necessary SDK/tooling.

Troubleshooting MCP server connectivity

- The image `ghcr.io/infinityflowapp/csharp-mcp:latest` is an MCP stdio server (stdin/stdout) for clients like VS Code Copilot. It does not execute a `.csx` file when you pass a path as the container command. Use the csx runner image above for executing scripts. Use the MCP image only when an MCP client connects over stdio.
- If the MCP client logs show repeated "Waiting for server to respond to `initialize` request..." lines, common causes are:
  - The container image is still pulling layers or starting (wait for pull to complete).
  - The MCP client launched the container without attaching stdio (`-i` missing) — ensure the client/VS Code config runs Docker with `-i --rm` for the MCP server.
  - The container exited early due to missing environment or wrong entrypoint. The MCP server expects to be run as a stdio service, not with a `.csx` path.

What to check quickly

- `docker ps` to confirm the container is running.
- `docker logs <container>` to inspect stdout/stderr for messages like "GitHub MCP Server running on stdio" or image pull progress.
- When in doubt, run the script directly in the container (see example above) to see `dotnet script` runtime errors and confirm environment variables are visible inside the container.

Local alternatives

- If you prefer not to use Docker, install `dotnet-script` globally (`dotnet tool install -g dotnet-script`) and run the `.csx` from inside the `scripts/examples/csharp` folder to avoid the "multiple project files" message.

Why E2E scripts can appear to “freeze” (and how to avoid it)

- Symptom: Running from the repository root causes `dotnet-script` to detect multiple `.csproj` files and prompt/abort with "Specify which project file to use..."; in some setups this looks like a hang.
- Fixes:
  - Mount only the script directory and set the working directory to that folder (`-w /app` as above).
  - Alternatively, run `dotnet script --cwd ./scripts/examples/csharp 20-...csx`.
  - Or copy the script into a temp folder inside the container and run it from there.
- If you must run from the repo root, specify the project explicitly with `dotnet script --project ./WileyWidget.csproj <script>`, but isolation is strongly recommended for speed and determinism.

Right project file (when needed)

- For repo-level `dotnet` operations that require selecting a project, use `WileyWidget.csproj` at the repo root.

Add these scripts to CI

- Call the container with `docker run` in a CI job, mount the repository (read-only), set `WW_REPO_ROOT` and `WW_LOGS_DIR`, and fail the job if the container exits non-zero.
