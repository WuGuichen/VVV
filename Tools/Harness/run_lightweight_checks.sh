#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(git -C "$SCRIPT_DIR" rev-parse --show-toplevel)"

cd "$REPO_ROOT"

BASE_REF="${HARNESS_BASE_REF:-origin/main}"
CHANGED_PATHS_FILE="$(mktemp)"
trap 'rm -f "$CHANGED_PATHS_FILE"' EXIT

if git rev-parse --verify --quiet "$BASE_REF" >/dev/null; then
  MERGE_BASE="$(git merge-base HEAD "$BASE_REF")"
  git diff --name-only --diff-filter=ACMRTUXB "$MERGE_BASE"...HEAD >>"$CHANGED_PATHS_FILE"
else
  MERGE_BASE=""
  echo "warning: base ref '$BASE_REF' not found; skipping committed range diff" >&2
fi

git diff --name-only --diff-filter=ACMRTUXB >>"$CHANGED_PATHS_FILE"
git diff --cached --name-only --diff-filter=ACMRTUXB >>"$CHANGED_PATHS_FILE"
git ls-files --others --exclude-standard >>"$CHANGED_PATHS_FILE"

sort -u "$CHANGED_PATHS_FILE" -o "$CHANGED_PATHS_FILE"

echo "Harness base: $BASE_REF"
echo "Changed path count: $(wc -l <"$CHANGED_PATHS_FILE" | tr -d ' ')"

FAILED=0

while IFS= read -r shell_script; do
  bash -n "$shell_script" || FAILED=1
done < <(find "$SCRIPT_DIR" -maxdepth 1 -type f -name '*.sh' | sort)

"$SCRIPT_DIR/check_forbidden_paths.sh" "$CHANGED_PATHS_FILE" || FAILED=1
"$SCRIPT_DIR/check_unity_meta.py" "$CHANGED_PATHS_FILE" || FAILED=1

if [ -n "$MERGE_BASE" ]; then
  git diff --check "$MERGE_BASE"...HEAD || FAILED=1
fi

git diff --check || FAILED=1
git diff --cached --check || FAILED=1

if [ "$FAILED" -ne 0 ]; then
  echo "Harness checks failed. See errors above." >&2
  exit 1
fi

echo "Lightweight harness checks passed."
