#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 1 ]; then
  echo "usage: $0 <changed-paths-file>" >&2
  exit 2
fi

CHANGED_PATHS_FILE="$1"
FAILED=0

while IFS= read -r path; do
  [ -n "$path" ] || continue
  lower_path="$(printf '%s' "$path" | tr '[:upper:]' '[:lower:]')"

  case "$lower_path" in
    library/*|*/library/*|temp/*|*/temp/*|logs/*|*/logs/*|usersettings/*|*/usersettings/*|memorycaptures/*|*/memorycaptures/*|recordings/*|*/recordings/*|obj/*|*/obj/*)
      echo "forbidden generated Unity path changed: $path" >&2
      FAILED=1
      ;;
    .gitnexus/*|*/.gitnexus/*|.codex/cache/*|*/.codex/cache/*|.agents/*|*/.agents/*|.claude/*|*/.claude/*)
      echo "forbidden local tool/cache path changed: $path" >&2
      FAILED=1
      ;;
    *.ds_store|.ds_store|*/.ds_store)
      echo "forbidden OS metadata changed: $path" >&2
      FAILED=1
      ;;
  esac

  if [[ "$lower_path" != */* && ( "$lower_path" == *.csproj || "$lower_path" == *.sln ) ]]; then
    echo "forbidden root Unity project file changed: $path" >&2
    FAILED=1
  fi
done <"$CHANGED_PATHS_FILE"

if [ "$FAILED" -ne 0 ]; then
  exit 1
fi

echo "Forbidden path check passed."
