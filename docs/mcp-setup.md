# csharp-mcp quick start (for Wiley-Widget)

This doc shows a minimal setup to run the `csharp-mcp` server locally and connect VS Code to it for on-the-fly C# evaluation and XUnit test iteration.

**Note**: The csharp-mcp server uses stdio (stdin/stdout) communication, not HTTP. It's designed to be invoked by MCP clients (like VS Code extensions) that manage the stdio streams.

## 1. Pull the image

Open PowerShell and run:

```powershell
docker pull ghcr.io/infinityflowapp/csharp-mcp:latest
```

## 2. VS Code configuration (recommended)

The MCP server is best used through VS Code's MCP integration. Verify `.vscode/mcp.json` contains:

```json
{
  "servers": {
    "csharp-mcp": {
      "command": "C:\\Program Files\\Docker\\Docker\\resources\\bin\\docker.exe",
      "args": [
        "run",
        "-i",
        "--rm",
        "-v",
        "C:\\Users\\biges\\Desktop\\Wiley_Widget:/scripts:ro",
        "-e",
        "CSX_ALLOWED_PATH=/scripts",
        "ghcr.io/infinityflowapp/csharp-mcp:latest"
      ],
      "env": {
        "CSX_ALLOWED_PATH": "/scripts"
      }
    }
  }
}
```

The MCP extension will launch the container with `-i` for interactive stdio communication and `--rm` for automatic cleanup.

## 3. Manual stdio testing (optional)

If you need to test the server manually via command line:

```powershell
# Run interactively with stdin
docker run -i --rm -v "C:\Users\biges\Desktop\Wiley_Widget:/scripts:ro" -e CSX_ALLOWED_PATH=/scripts ghcr.io/infinityflowapp/csharp-mcp:latest

# Send JSON-RPC message (paste and press Enter):
{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}
```

The server will respond via stdout with JSON-RPC responses.

The server will respond via stdout with JSON-RPC responses.

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
