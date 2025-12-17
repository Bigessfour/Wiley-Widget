# Trunk CLI & VS Code extension — Setup

This project uses Trunk (https://trunk.io) for repository checks and some automation (see `.trunk/trunk.yaml`). The repo expects the `trunk` CLI to be available on PATH (min CLI version: 1.25.0).

## Install Trunk CLI (recommended)

### Windows (PowerShell)

1. Open an elevated PowerShell (or a shell with appropriate permissions).
2. Run the bundled setup script to install the CLI and optionally the VS Code extension:

```powershell
pwsh .\scripts\trunk\setup-trunk.ps1 -InstallVSCodeExtension
```

### macOS / Linux

```bash
bash scripts/trunk/setup-trunk.sh --install-vscode
# or run without extension install:
bash scripts/trunk/setup-trunk.sh
```

The scripts install the official launcher package via npm: `npm install -g @trunkio/launcher` and verify that `trunk --version` is present.

## Authenticate

After installation, authenticate locally (interactive):

```bash
trunk login
```

## Verify

Run a quick check to ensure Trunk works in this repo:

```bash
trunk --version
trunk check --ci
```

## Recommended VS Code Extension

We recommend installing the Trunk VS Code extension. If you have the `code` CLI available, the setup script attempts to install it: `code --install-extension trunkio.trunk`.

If the CLI isn't available, open the Extensions pane and search for "Trunk".

## Notes

- The repo's PowerShell module `scripts/trunk/TrunkMergeQueue.psm1` requires `trunk` in PATH and will error if it's not available.
- If you cannot install global npm packages, consider using a node version manager (nvm) and ensuring global npm bin directory is on your PATH.
