#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TOOLS_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
AUTO_EXTERNAL_PROJECT=""
SIBLING_FRAMEWORK_ROOT="$(cd "$TOOLS_ROOT/.." && pwd)/WGameFramework"
if [[ "$SIBLING_FRAMEWORK_ROOT" != "$TOOLS_ROOT" &&
      -f "$SIBLING_FRAMEWORK_ROOT/Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj" &&
      -f "$TOOLS_ROOT/ProjectSettings/ProjectVersion.txt" ]]; then
  PROJECT_ROOT="$TOOLS_ROOT"
  TOOLS_ROOT="$SIBLING_FRAMEWORK_ROOT"
  AUTO_EXTERNAL_PROJECT="1"
else
  PROJECT_ROOT="${3:-${MXFRAMEWORK_EDITOR_HUB_PROJECT_ROOT:-$TOOLS_ROOT}}"
  PROJECT_ROOT="$(cd "$PROJECT_ROOT" && pwd)"
fi
CLI_PROJECT="$TOOLS_ROOT/Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj"
DEFAULT_PACKAGE="Tools/MxFramework.Authoring/samples/character-codex-flow-test"
if [[ -n "${1:-}" ]]; then
  PORT="$1"
elif [[ -n "${MXFRAMEWORK_EDITOR_HUB_PORT:-}" ]]; then
  PORT="$MXFRAMEWORK_EDITOR_HUB_PORT"
elif [[ "$AUTO_EXTERNAL_PROJECT" == "1" ]]; then
  PORT="4973"
else
  PORT="4873"
fi

if [[ -n "${2:-}" ]]; then
  PACKAGE_RELATIVE="$2"
elif [[ -n "${MXFRAMEWORK_EDITOR_HUB_PACKAGE:-}" ]]; then
  PACKAGE_RELATIVE="$MXFRAMEWORK_EDITOR_HUB_PACKAGE"
elif [[ "$AUTO_EXTERNAL_PROJECT" == "1" && -f "$PROJECT_ROOT/Authoring/CharacterPackages/wgame-b-prototype/manifest.json" ]]; then
  PACKAGE_RELATIVE="Authoring/CharacterPackages/wgame-b-prototype"
else
  PACKAGE_RELATIVE="$DEFAULT_PACKAGE"
fi
OPEN_BROWSER="${MXFRAMEWORK_EDITOR_HUB_OPEN_BROWSER:-1}"
URL=""
HEALTH_URL=""
ANIMATION_HEALTH_URL=""
DAMAGE_HEALTH_URL=""
BUFF_HEALTH_URL=""
UI_COMPOSER_HEALTH_URL=""
COMBAT_DESIGN_HEALTH_URL=""
AUDIO_GENERATION_KEYCHAIN_SERVICE="${MX_AUDIO_GENERATION_KEYCHAIN_SERVICE:-MxFramework.AudioGenerationToken}"

die() {
  printf '[ERROR] %s\n' "$*" >&2
  exit 1
}

warn() {
  printf '[WARN] %s\n' "$*" >&2
}

load_audio_generation_token() {
  if [[ -n "${MX_AUDIO_GENERATION_TOKEN:-}" ]]; then
    return
  fi

  if [[ "$(uname -s)" == "Darwin" ]] && command -v security >/dev/null 2>&1; then
    local token
    token="$(security find-generic-password -a "$(id -un)" -s "$AUDIO_GENERATION_KEYCHAIN_SERVICE" -w 2>/dev/null || true)"
    if [[ -n "$token" ]]; then
      export MX_AUDIO_GENERATION_TOKEN="$token"
      return
    fi
  fi

  warn "MX_AUDIO_GENERATION_TOKEN is unavailable; Audio Workbench generation jobs will be disabled."
}

configure_urls() {
  URL="http://127.0.0.1:${PORT}/Tools/MxFramework.EditorHub/web/"
  HEALTH_URL="http://127.0.0.1:${PORT}/api/character/packages"
  ANIMATION_HEALTH_URL="http://127.0.0.1:${PORT}/api/authoring/animation/packages"
  DAMAGE_HEALTH_URL="http://127.0.0.1:${PORT}/api/character-actions/damage-analysis"
  BUFF_HEALTH_URL="http://127.0.0.1:${PORT}/api/authoring/buffs/options"
  UI_COMPOSER_HEALTH_URL="http://127.0.0.1:${PORT}/api/authoring/ui-composer/capabilities"
  COMBAT_DESIGN_HEALTH_URL="http://127.0.0.1:${PORT}/api/combat-design/query"
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

is_editor_hub_server_ready() {
  command -v curl >/dev/null 2>&1 &&
    curl -fsS "$HEALTH_URL" >/dev/null 2>&1 &&
    curl -fsS "$ANIMATION_HEALTH_URL" >/dev/null 2>&1 &&
    is_action_damage_analysis_api_ready &&
    is_buff_authoring_api_ready &&
    is_ui_composer_api_ready &&
    is_combat_design_api_ready &&
    curl -fsS "$URL" >/dev/null 2>&1
}

is_action_damage_analysis_api_ready() {
  command -v curl >/dev/null 2>&1 &&
    curl -fsS "$DAMAGE_HEALTH_URL" 2>/dev/null | grep -q '"actions"'
}

is_buff_authoring_api_ready() {
  command -v curl >/dev/null 2>&1 &&
    curl -fsS "$BUFF_HEALTH_URL" >/dev/null 2>&1
}

is_ui_composer_api_ready() {
  command -v curl >/dev/null 2>&1 &&
    curl -fsS "$UI_COMPOSER_HEALTH_URL" 2>/dev/null | grep -q 'mx.uiComposer.authoring.v1'
}

is_combat_design_api_ready() {
  command -v curl >/dev/null 2>&1 || return 1
  local response
  response="$(curl -fsS --max-time 5 "$COMBAT_DESIGN_HEALTH_URL" 2>/dev/null)" || return 1
  printf '%s' "$response" | grep -E '"schema"[[:space:]]*:[[:space:]]*"mx\.combat-design\.query\.v1"' >/dev/null
}

wait_and_open_url() {
  if ! command -v curl >/dev/null 2>&1; then
    sleep 3
    open_url
    return
  fi

  for _ in $(seq 1 30); do
    if is_editor_hub_server_ready; then
      open_url
      return
    fi
    sleep 1
  done

  warn "Editor Hub server did not become ready in 30 seconds. Open manually after it starts: $URL"
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

select_available_port() {
  configure_urls
  if ! is_port_in_use; then
    return
  fi

  if is_editor_hub_server_ready; then
    printf 'MxFramework Authoring server is already running on port %s.\n' "$PORT"
    printf 'URL: %s\n' "$URL"
    [[ "$OPEN_BROWSER" == "0" ]] || open_url
    exit 0
  fi

  if command -v curl >/dev/null 2>&1 &&
     curl -fsS "$HEALTH_URL" >/dev/null 2>&1 &&
     curl -fsS "$ANIMATION_HEALTH_URL" >/dev/null 2>&1 &&
     is_action_damage_analysis_api_ready &&
     is_buff_authoring_api_ready &&
     is_ui_composer_api_ready &&
     ! is_combat_design_api_ready; then
    warn "Port $PORT has an Authoring server, but Combat Design API/schema is unavailable."
  elif command -v curl >/dev/null 2>&1 &&
     curl -fsS "$HEALTH_URL" >/dev/null 2>&1 &&
     curl -fsS "$ANIMATION_HEALTH_URL" >/dev/null 2>&1 &&
     ! is_action_damage_analysis_api_ready; then
    warn "Port $PORT has an older Authoring server without Action Damage Analysis API."
  elif command -v curl >/dev/null 2>&1 &&
     curl -fsS "$HEALTH_URL" >/dev/null 2>&1 &&
     curl -fsS "$ANIMATION_HEALTH_URL" >/dev/null 2>&1 &&
     is_action_damage_analysis_api_ready &&
     is_buff_authoring_api_ready &&
     ! is_ui_composer_api_ready; then
    warn "Port $PORT has an older Authoring server without UI Composer APIs."
  elif command -v curl >/dev/null 2>&1 &&
     curl -fsS "$HEALTH_URL" >/dev/null 2>&1 &&
     curl -fsS "$ANIMATION_HEALTH_URL" >/dev/null 2>&1 &&
     is_action_damage_analysis_api_ready &&
     ! is_buff_authoring_api_ready; then
    warn "Port $PORT has an older Authoring server without Buff v2 Workbench APIs."
  elif command -v curl >/dev/null 2>&1 && curl -fsS "$HEALTH_URL" >/dev/null 2>&1 && ! curl -fsS "$ANIMATION_HEALTH_URL" >/dev/null 2>&1; then
    warn "Port $PORT has an older Authoring server without Animation Editor APIs."
  else
    warn "Port $PORT is already in use, but it is not an Editor Hub-compatible Authoring server."
  fi

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
    if is_editor_hub_server_ready; then
      printf 'MxFramework Authoring server is already running on port %s.\n' "$PORT"
      printf 'URL: %s\n' "$URL"
      [[ "$OPEN_BROWSER" == "0" ]] || open_url
      exit 0
    fi
  done

  die "No free Authoring server port found in range $((original_port + 1))-$((original_port + 30)). Stop the old process or pass an explicit free port."
}

resolve_package_dir() {
  if [[ -d "$PACKAGE_RELATIVE" ]]; then
    printf '%s\n' "$PACKAGE_RELATIVE"
    return
  fi

  if [[ -d "$PROJECT_ROOT/$PACKAGE_RELATIVE" ]]; then
    printf '%s\n' "$PROJECT_ROOT/$PACKAGE_RELATIVE"
    return
  fi

  if [[ -d "$TOOLS_ROOT/$PACKAGE_RELATIVE" ]]; then
    printf '%s\n' "$TOOLS_ROOT/$PACKAGE_RELATIVE"
    return
  fi
}

cd "$PROJECT_ROOT"

[[ "$PORT" =~ ^[0-9]+$ ]] || die "Port must be numeric. Example: $0 4874"
[[ -f "$CLI_PROJECT" ]] || die "Authoring CLI project not found: $CLI_PROJECT"
PACKAGE_DIR="$(resolve_package_dir || true)"
[[ -n "$PACKAGE_DIR" ]] || die "Character package folder not found: $PACKAGE_RELATIVE (project root: $PROJECT_ROOT, tools root: $TOOLS_ROOT)"
[[ -f "$PACKAGE_DIR/manifest.json" ]] || die "Character package manifest not found: $PACKAGE_RELATIVE/manifest.json"

if ! command -v dotnet >/dev/null 2>&1; then
  die "dotnet SDK not found in PATH. Install .NET 9 SDK first."
fi

if ! dotnet --list-sdks | grep -Eq '^[[:space:]]*(9|[1-9][0-9])\.'; then
  die "Authoring CLI targets net9.0, but .NET 9+ SDK was not found. Installed SDKs: $(dotnet --list-sdks | tr '\n' '; ')"
fi

if [[ ! -d "$TOOLS_ROOT/Tools/MxFramework.CharacterStudio/node_modules/three" ]]; then
  warn "CharacterStudio npm dependency three is missing. GLB preview may fall back."
  warn "Run once from the tools root: npm --prefix Tools/MxFramework.CharacterStudio install"
fi

load_audio_generation_token
select_available_port

printf 'MxFramework Editor Hub\n'
printf 'Tools root  : %s\n' "$TOOLS_ROOT"
printf 'Project root: %s\n' "$PROJECT_ROOT"
printf 'Package     : %s\n' "$PACKAGE_RELATIVE"
printf 'Port        : %s\n' "$PORT"
printf 'URL         : %s\n' "$URL"
printf 'Stop        : Ctrl+C\n\n'

if [[ "$OPEN_BROWSER" != "0" ]]; then
  wait_and_open_url &
fi

exec dotnet run --project "$CLI_PROJECT" -- editor serve --tools-root "$TOOLS_ROOT" --project-root "$PROJECT_ROOT" --port "$PORT" --package "$PACKAGE_RELATIVE"
