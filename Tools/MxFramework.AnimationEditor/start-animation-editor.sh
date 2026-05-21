#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
CLI_PROJECT="$ROOT_DIR/Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj"
DEFAULT_PACKAGE="Tools/MxFramework.Authoring/samples/character-iron-vanguard"
PORT="${1:-${MXFRAMEWORK_ANIMATION_EDITOR_PORT:-4873}}"
PACKAGE_RELATIVE="${2:-${MXFRAMEWORK_ANIMATION_EDITOR_PACKAGE:-$DEFAULT_PACKAGE}}"
OPEN_BROWSER="${MXFRAMEWORK_ANIMATION_EDITOR_OPEN_BROWSER:-1}"
URL_PACKAGE=""
URL=""
HEALTH_PACKAGES_URL=""
HEALTH_LOAD_URL=""

die() {
  printf '[ERROR] %s\n' "$*" >&2
  exit 1
}

warn() {
  printf '[WARN] %s\n' "$*" >&2
}

configure_urls() {
  URL_PACKAGE="${PACKAGE_RELATIVE// /%20}"
  URL="http://127.0.0.1:${PORT}/Tools/MxFramework.AnimationEditor/web/?package=${URL_PACKAGE}"
  HEALTH_PACKAGES_URL="http://127.0.0.1:${PORT}/api/authoring/animation/packages"
  HEALTH_LOAD_URL="http://127.0.0.1:${PORT}/api/authoring/animation/load?package=${URL_PACKAGE}"
}

open_url() {
  case "$(uname -s)" in
    Darwin)
      open "$URL" >/dev/null 2>&1 || warn "Could not open browser automatically: $URL"
      ;;
    *)
      if command -v xdg-open >/dev/null 2>&1; then
        xdg-open "$URL" >/dev/null 2>&1 || warn "Could not open browser automatically: $URL"
      else
        warn "Open this URL manually: $URL"
      fi
      ;;
  esac
}

select_available_port() {
  configure_urls
  if ! is_port_in_use; then
    return
  fi

  if is_animation_editor_server_ready; then
    printf 'Animation Editor-compatible Authoring server is already running on port %s.\n' "$PORT"
    printf 'URL: %s\n' "$URL"
    [[ "$OPEN_BROWSER" == "0" ]] || open_url
    exit 0
  fi

  warn "Port $PORT is already in use, but it is not an Animation Editor-compatible Authoring server."
  if command -v lsof >/dev/null 2>&1; then
    lsof -iTCP:"$PORT" -sTCP:LISTEN -n -P >&2 || true
  fi

  local original_port="$PORT"
  local candidate
  for candidate in $(seq $((PORT + 1)) $((PORT + 30))); do
    PORT="$candidate"
    configure_urls
    if ! is_port_in_use; then
      warn "Using free fallback port $PORT instead of $original_port."
      return
    fi
    if is_animation_editor_server_ready; then
      printf 'Animation Editor-compatible Authoring server is already running on port %s.\n' "$PORT"
      printf 'URL: %s\n' "$URL"
      [[ "$OPEN_BROWSER" == "0" ]] || open_url
      exit 0
    fi
  done

  die "No free Authoring server port found in range $((original_port + 1))-$((original_port + 30)). Stop the old process or pass an explicit free port."
}

is_port_in_use() {
  if command -v lsof >/dev/null 2>&1; then
    lsof -iTCP:"$PORT" -sTCP:LISTEN -n -P >/dev/null 2>&1
    return
  fi

  if command -v nc >/dev/null 2>&1; then
    nc -z 127.0.0.1 "$PORT" >/dev/null 2>&1
    return
  fi

  return 1
}

is_animation_editor_server_ready() {
  command -v curl >/dev/null 2>&1 &&
    curl -fsS "$HEALTH_PACKAGES_URL" >/dev/null 2>&1 &&
    curl -fsS "$HEALTH_LOAD_URL" >/dev/null 2>&1
}

wait_and_open_url() {
  if ! command -v curl >/dev/null 2>&1; then
    sleep 3
    open_url
    return
  fi

  for _ in $(seq 1 30); do
    if is_animation_editor_server_ready; then
      open_url
      return
    fi
    sleep 1
  done

  warn "Animation Editor server did not become ready in 30 seconds. Open manually after it starts: $URL"
}

cd "$ROOT_DIR"

[[ "$PORT" =~ ^[0-9]+$ ]] || die "Port must be numeric. Example: $0 4874"
[[ -f "$CLI_PROJECT" ]] || die "Authoring CLI project not found: $CLI_PROJECT"
[[ -d "$PACKAGE_RELATIVE" ]] || die "Package context folder not found: $PACKAGE_RELATIVE"
[[ -f "$PACKAGE_RELATIVE/manifest.json" ]] || die "Package context manifest not found: $PACKAGE_RELATIVE/manifest.json"

if ! command -v dotnet >/dev/null 2>&1; then
  die "dotnet SDK not found in PATH. Install .NET 9 SDK first."
fi

if ! dotnet --list-sdks | grep -Eq '^[[:space:]]*(9|[1-9][0-9])\.'; then
  die "Authoring CLI targets net9.0, but .NET 9+ SDK was not found. Installed SDKs: $(dotnet --list-sdks | tr '\n' '; ')"
fi

select_available_port

printf 'MxFramework Animation Editor\n'
printf 'Root   : %s\n' "$ROOT_DIR"
printf 'Context: %s\n' "$PACKAGE_RELATIVE"
printf 'Port   : %s\n' "$PORT"
printf 'URL    : %s\n' "$URL"
printf 'Stop   : Ctrl+C\n\n'

if [[ "$OPEN_BROWSER" != "0" ]]; then
  wait_and_open_url &
fi

exec dotnet run --project "$CLI_PROJECT" -- editor serve --root "$ROOT_DIR" --port "$PORT" --package "$PACKAGE_RELATIVE"
