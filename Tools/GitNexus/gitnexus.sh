#!/usr/bin/env bash
set -euo pipefail

VERSION="${GITNEXUS_VERSION:-1.6.4}"
LBUG_VERSION="${GITNEXUS_LBUG_VERSION:-0.16.1}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
TOOL_HOME="${GITNEXUS_TOOL_HOME:-$REPO_ROOT/.codex/cache/gitnexus-tool}"
BIN="$TOOL_HOME/node_modules/.bin/gitnexus"
LBUG_NATIVE="$TOOL_HOME/node_modules/@ladybugdb/core/lbugjs.node"

NEEDS_INSTALL=0
if [[ ! -x "$BIN" || ! -f "$LBUG_NATIVE" ]]; then
  NEEDS_INSTALL=1
else
  INSTALLED_VERSION="$("$BIN" --version 2>/dev/null || true)"
  if [[ "$INSTALLED_VERSION" != "$VERSION" ]]; then
    NEEDS_INSTALL=1
  fi
fi

if [[ "$NEEDS_INSTALL" == "1" ]]; then
  mkdir -p "$TOOL_HOME"
  rm -f "$TOOL_HOME/package-lock.json"
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
    LBUG_PACKAGE="@ladybugdb/core-darwin-arm64@$LBUG_VERSION"
  elif [[ "$PLATFORM" == "linux" && "$ARCH" == "x64" ]]; then
    LBUG_PACKAGE="@ladybugdb/core-linux-x64@$LBUG_VERSION"
  elif [[ "$PLATFORM" == "linux" && "$ARCH" == "arm64" ]]; then
    LBUG_PACKAGE="@ladybugdb/core-linux-arm64@$LBUG_VERSION"
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

if [[ "${1:-}" == "analyze" ]]; then
  HAS_SKIP_AGENTS_MD=0
  for ARG in "$@"; do
    if [[ "$ARG" == "--skip-agents-md" ]]; then
      HAS_SKIP_AGENTS_MD=1
      break
    fi
  done

  if [[ "$HAS_SKIP_AGENTS_MD" == "0" ]]; then
    set -- analyze --skip-agents-md "${@:2}"
  fi
fi

exec "$BIN" "$@"
