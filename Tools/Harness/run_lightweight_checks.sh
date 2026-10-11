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
  git diff --no-renames --name-only --diff-filter=ACDMRTUXB "$MERGE_BASE"...HEAD >>"$CHANGED_PATHS_FILE"
else
  MERGE_BASE=""
  echo "warning: base ref '$BASE_REF' not found; skipping committed range diff" >&2
fi

git diff --no-renames --name-only --diff-filter=ACDMRTUXB >>"$CHANGED_PATHS_FILE"
git diff --cached --no-renames --name-only --diff-filter=ACDMRTUXB >>"$CHANGED_PATHS_FILE"
git ls-files --others --exclude-standard >>"$CHANGED_PATHS_FILE"

sort -u "$CHANGED_PATHS_FILE" -o "$CHANGED_PATHS_FILE"

echo "Harness base: $BASE_REF"
echo "Changed path count: $(wc -l <"$CHANGED_PATHS_FILE" | tr -d ' ')"

FAILED=0
PYTHON_BIN="${PYTHON_BIN:-}"
if [ -z "$PYTHON_BIN" ]; then
  for candidate in python python3; do
    if command -v "$candidate" >/dev/null 2>&1 && "$candidate" -c "import sys" >/dev/null 2>&1; then
      PYTHON_BIN="$candidate"
      break
    fi
  done
fi
if [ -z "$PYTHON_BIN" ]; then
  echo "Python interpreter not found; set PYTHON_BIN to run Python harness checks." >&2
  exit 1
fi

while IFS= read -r shell_script; do
  bash -n "$shell_script" || FAILED=1
done < <(find "$SCRIPT_DIR" -maxdepth 1 -type f -name '*.sh' | sort)

"$SCRIPT_DIR/check_forbidden_paths.sh" "$CHANGED_PATHS_FILE" || FAILED=1
"$PYTHON_BIN" "$SCRIPT_DIR/check_unity_meta.py" "$CHANGED_PATHS_FILE" || FAILED=1
"$PYTHON_BIN" "$SCRIPT_DIR/check_docs_health.py" "$CHANGED_PATHS_FILE" --base-ref "${MERGE_BASE:-HEAD}" || FAILED=1
"$PYTHON_BIN" "$SCRIPT_DIR/check_code_standards.py" "$CHANGED_PATHS_FILE" || FAILED=1
"$PYTHON_BIN" "$SCRIPT_DIR/check_character_authoring_sources.py" || FAILED=1
"$PYTHON_BIN" -m unittest discover -s "$SCRIPT_DIR/tests" -p 'test_*.py' || FAILED=1

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
