#!/usr/bin/env bash
set -euo pipefail

VERSION="${GITNEXUS_VERSION:-1.6.3}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
TOOL_HOME="${GITNEXUS_TOOL_HOME:-$REPO_ROOT/.codex/cache/gitnexus-tool}"
BIN="$TOOL_HOME/node_modules/.bin/gitnexus"
LBUG_NATIVE="$TOOL_HOME/node_modules/@ladybugdb/core/lbugjs.node"

if [[ ! -x "$BIN" || ! -f "$LBUG_NATIVE" ]]; then
  mkdir -p "$TOOL_HOME"
  if [[ ! -f "$TOOL_HOME/package.json" ]]; then
    npm init -y --prefix "$TOOL_HOME" >/dev/null
  fi
  npm install "gitnexus@$VERSION" \
    --prefix "$TOOL_HOME" \
    --ignore-scripts \
    --omit=optional

  PLATFORM="$(node -p 'process.platform')"
  ARCH="$(node -p 'process.arch')"
  if [[ "$PLATFORM" == "darwin" && "$ARCH" == "arm64" ]]; then
    LBUG_PACKAGE="@ladybugdb/core-darwin-arm64@0.15.4"
  elif [[ "$PLATFORM" == "linux" && "$ARCH" == "x64" ]]; then
    LBUG_PACKAGE="@ladybugdb/core-linux-x64@0.15.4"
  elif [[ "$PLATFORM" == "linux" && "$ARCH" == "arm64" ]]; then
    LBUG_PACKAGE="@ladybugdb/core-linux-arm64@0.15.4"
  else
    echo "Unsupported GitNexus LadybugDB prebuilt platform: $PLATFORM-$ARCH" >&2
    exit 1
  fi

  npm install "$LBUG_PACKAGE" \
    --prefix "$TOOL_HOME" \
    --ignore-scripts \
    --omit=optional

  LBUG_PACKAGE_DIR="$TOOL_HOME/node_modules/${LBUG_PACKAGE%@*}"
  cp "$LBUG_PACKAGE_DIR/lbugjs.node" "$LBUG_NATIVE"
fi

exec "$BIN" "$@"
