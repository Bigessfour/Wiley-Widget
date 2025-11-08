# Build Performance Optimizations Guide

## MSBuild Build Tasks

When working with .NET builds, always use the appropriate build task for optimal performance:

- **build**: Default parallel build with incremental compilation enabled.
- **build-fast**: Use for quick rebuilds when dependencies haven't changed (skips NuGet restore).
- **build-incremental**: Explicitly ensures incremental builds for development workflow.
- **build-parallel**: Maximizes CPU utilization for large builds.
- **build-graph**: Use for solutions with complex project dependencies to optimize build order.

## Build Process Guidelines

- Prefer incremental builds over clean builds to leverage MSBuild's fast up-to-date checks.
- Use parallel execution (-m flag) to utilize multiple CPU cores.
- Optimize project reference graphs to minimize unnecessary rebuilds.
- Run Trunk checks after any build configuration changes.

## When to Use Each Task

- **Development**: Use `build-incremental` or `build-fast` for iterative coding.
- **CI/CD**: Use `build-parallel` or `build-graph` for maximum speed.
- **First-time builds**: Use `build` (includes restore).
- **Large solutions**: Use `build-graph` for dependency optimization.

## Implementation Details

### Directory.Build.props Optimizations

- `<BuildInParallel>true</BuildInParallel>`: Enables parallel project builds.
- `<MaxCpuCount>$(NUMBER_OF_PROCESSORS)</MaxCpuCount>`: Uses all available cores.
- `<UseSharedCompilation>true</UseSharedCompilation>`: Shares compilation across builds.
- `<AccelerateBuildsInVisualStudio>true</AccelerateBuildsInVisualStudio>`: Optimizes for IDE builds.

### Task Configurations

All build tasks include:

- `-m`: Parallel processing flag
- `--no-restore`: Skips NuGet restore for faster rebuilds
- `/property:GenerateFullPaths=true`: Better error reporting
- Error logging to `build-errors.log`

## Copilot Integration

To ensure Copilot uses these optimizations:

1. Reference this guide in prompts: "Follow the build optimization guide"
2. Use task names in comments: `// Use build-incremental for fast rebuilds`
3. Specify build methods in development requests

## Validation

Always run `trunk check --ci` after build configuration changes to maintain code quality.
