# csharp-mcp quick start (for Wiley-Widget)

This doc shows a minimal setup to run the `csharp-mcp` server locally and connect VS Code to it for on-the-fly C# evaluation and XUnit test iteration.

**Note**: The csharp-mcp server uses stdio (stdin/stdout) communication, not HTTP. It's designed to be invoked by MCP clients (like VS Code extensions) that manage the stdio streams.

## 1. Local-first setup (no Docker)

The C# MCP server is already configured to run locally via stdio in `.vscode/mcp.json` using `InfinityFlow.CSharp.Eval`. No Docker is required.

Verify `.vscode/mcp.json` contains a block like:

```json
{
  "servers": {
    "csharp-mcp": {
      "command": "InfinityFlow.CSharp.Eval",
      "args": [],
      "env": {
        "CSX_ALLOWED_PATH": "${workspaceFolder}",
        "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
        "DOTNET_NOLOGO": "1"
      }
    }
  }
}
```

Start it from VS Code by opening `.vscode/mcp.json` and clicking Start.

## 2. Optional: Docker-based setup (legacy)

Prefer the local stdio configuration above. Only use Docker if local execution is unavailable in your environment.

## 3. Manual stdio testing (optional)

If needed, you can send JSON-RPC to the process launched by VS Code using the MCP client’s debug tools. Direct manual stdio invocation isn’t necessary for normal use.

## 4. Example C# + XUnit snippet

You can send snippets like the following through the MCP integration (via VS Code extension) to compile and run small tests quickly:

```csharp
#r "nuget: xunit, 2.4.1"
#r "nuget: Microsoft.NET.Test.Sdk, 17.11.1"

using Xunit;
using System;

public class SampleTest
{
    [Fact]
    public void TestAddition()
    {
        Assert.Equal(4, 2 + 2);
    }
}
```

The MCP server will compile and execute the code via Roslyn, returning results or errors.

Note: adjust NuGet versions if you need newer packages.

## 5. Security & limits

- `CSX_ALLOWED_PATH` restricts file access inside the container to `/scripts` (your mounted workspace).
- The container implements execution timeouts to prevent runaway code.
- Using `:ro` (read-only) mount prevents the container from modifying your workspace files.

## 6. Validation

To verify the MCP server is configured correctly, restart VS Code and check that the MCP extension recognizes the `csharp-mcp` server in its list of available servers.

## 7. Next steps / suggestions

- The `run-mcp.ps1` script is now deprecated for manual runs since MCP works best through VS Code's managed stdio connections.
- Add a small harness script that tests MCP evaluation programmatically if needed for CI.
- For Wiley-Widget: use this to generate/validate XUnit tests for ViewModels, repositories, or business logic without full builds.
