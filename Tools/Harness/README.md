# MxFramework Lightweight Harness

This folder contains the first repository-level checks intended for local use
and later Gitea Actions wiring.

## Local Usage

Run from the repository root:

```bash
Tools/Harness/run_lightweight_checks.sh
```

By default the harness compares committed changes against `origin/main`, then
also checks staged, unstaged, and untracked files. To compare against another
base ref:

```bash
HARNESS_BASE_REF=main Tools/Harness/run_lightweight_checks.sh
```

## Current Checks

- `check_forbidden_paths.sh`: fails when changed files include Unity generated
  state, local tool caches, OS metadata, or root Unity `.csproj` / `.sln`
  project files.
- `check_unity_meta.py`: fails when changed files under `Assets/` introduce
  Unity assets without matching `.meta` files.
- `check_docs_health.py`: validates changed docs for standard `Status` headers,
  checks the documentation entrypoints, and blocks removed code-index terms
  from returning to live docs or agent instructions.
- `bash -n`: validates the Harness shell scripts themselves.
- `git diff --check`: runs against the committed range from the merge base to
  `HEAD`, then separately checks unstaged and staged local changes.

The checks are deterministic from the repository root. The entrypoint runs all
checks it can, reports every failure it sees, and then returns non-zero if any
check failed.

## Intended Gitea Actions Usage

The initial runner contract is a single command:

```bash
Tools/Harness/run_lightweight_checks.sh
```

Gitea workflow wiring is intentionally left for a follow-up after the runner
environment is confirmed. A workflow should fetch `origin/main` before running
the command, or set `HARNESS_BASE_REF` to the pull request target branch.
