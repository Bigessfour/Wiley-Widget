#!/usr/bin/env bash
set -euo pipefail

REQUIRED_VERSION="1.25.0"
INSTALL_VSCODE=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    --install-vscode)
      INSTALL_VSCODE=true
      shift
      ;;
    *)
      echo "Unknown option: $1"
      exit 1
      ;;
  esac
done

command -v npm >/dev/null 2>&1 || { echo "npm not found. Install Node.js (https://nodejs.org/)" >&2; exit 2; }

get_trunk_version() {
  if command -v trunk >/dev/null 2>&1; then
    trunk --version 2>/dev/null | awk -F" " '{for (i=1;i<=NF;i++) if ($i ~ /[0-9]+\.[0-9]+\.[0-9]+/) print $i; exit}' || true
  fi
}

current=$(get_trunk_version || true)
if [[ -n "$current" && "$current" > "$REQUIRED_VERSION" ]] || [[ "$current" == "$REQUIRED_VERSION" ]]; then
  echo "Found trunk CLI $current >= $REQUIRED_VERSION. Skipping installation."
else
  echo "Installing Trunk CLI via npm (@trunkio/launcher)"
  npm install -g @trunkio/launcher
  new=$(get_trunk_version || true)
  if [[ -z "$new" ]]; then
    echo "Installation succeeded but 'trunk' is not on PATH. Ensure npm global bin is on PATH." >&2
    exit 3
  fi
  echo "Installed trunk $new"
fi

if $INSTALL_VSCODE; then
  if command -v code >/dev/null 2>&1; then
    echo "Installing VS Code extension 'trunkio.trunk'"
    code --install-extension trunkio.trunk --force || echo "Failed to install extension via 'code' CLI; install manually from Marketplace"
  else
    echo "VS Code 'code' CLI not found; skipping extension install. Use Marketplace to install 'Trunk' extension or run 'code --install-extension trunkio.trunk' when available"
  fi
fi

echo "Run 'trunk login' to authenticate then test with 'trunk check --ci'"
