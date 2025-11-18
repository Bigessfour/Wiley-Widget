Local Docker CI Guide for Wiley Widget

Overview
- This repo includes Docker resources to run local CI-like tests without pushing to GitHub.
- `docker-compose.dev.yml` provides services for CSX, unit tests, Uno builds, WinUI validation (Windows-only), Python validation, and Trunk QA checks.

Prerequisites
- Docker Desktop installed and running.
  - For WinUI validation you must switch Docker Desktop to Windows containers or run on a Windows Docker host.
- At least 8 GB disk and 4 GB RAM available for builds.
- On Windows PowerShell (pwsh) terminal.

Quick commands (PowerShell)

# Build/run Uno and unit tests (Linux containers)
pwsh -Command "docker compose -f docker-compose.dev.yml up --build --profile testing unit-test-runner uno-build-tester"

# Run CSX tests only
pwsh -Command "docker compose -f docker-compose.dev.yml up --build --profile testing csx-test-runner"

# Run Python validators
pwsh -Command "docker compose -f docker-compose.dev.yml up --build --profile python-validation python-validator"

# Run Trunk QA checks (needs network access and trunk token if uploading)
pwsh -Command "docker compose -f docker-compose.dev.yml up --build --profile qa quality-assurance"

# WinUI validation (Windows containers required)
pwsh -Command "docker compose -f docker-compose.dev.yml up --build --profile winui-testing winui-validator"

Notes
- Test results are written to `./TestResults` (bind-mounted). Inspect TRX/coverage files after runs.
- NuGet cache is persisted in the named volume `wiley-widget-nuget-cache` to speed subsequent builds.
- For faster iteration, run a single service; stop the rest. Use `docker compose stop <service>`.
- If you see XAML/WinUI compile errors on Linux, focus on Uno tests; WinUI requires Windows containers.

Troubleshooting
- If a container exits with build errors, run the same stage interactively:

pwsh -Command "docker run --rm -it -v ${PWD}:/workspace -w /workspace mcr.microsoft.com/dotnet/sdk:9.0 bash"

Then run the failing `dotnet` restore/build/test commands inside the container to inspect logs.

Contact
- If you want me to run specific local iterations or tweak timeouts/memory, tell me which service to prioritize.
