# Code snapshot mirror

> Status: Current

`sync_code_mirror.py` reads a fixed commit from the configured source branch and
exports selected code and documentation for browsing. Gitea remains the primary
Git repository and Git LFS authority, as defined by
[ADR-001](../../Docs/Decisions/ADR-001-Version-Control-Gitea.md).

Machine assignment and Issue comment handoffs for user-requested collaboration
are defined by [the multi-machine Issue execution policy](../../Docs/Guides/MULTI_MACHINE_ISSUE_EXECUTION.md).

This tool has two separate actions: `plan` writes a local preview; `publish`
explicitly publishes the files in that preview. Running it without arguments
defaults to `plan`. It never syncs Issues, PRs, comments, permissions, default
branch settings or development branches.

## Requirements and configured destinations

- Python 3.10+ and Git with SHA-1 repository support.
- Source: `vincent/WGameFramework`, branch `main`, on the NAS Gitea.
- Configured mirror: `https://github.com/WuGuichen/VVV.git`, branch `code-mirror`.
- Gitee can be added after its exact repository address is known.
- Publication uses the existing Git credential helper or SSH agent. Browser and
  connector logins do not establish command-line Git authentication.

Git commands run noninteractively. The script does not create credentials, read
credential files, weaken certificate verification or print raw Git/helper error
output. Authentication and transport failures stop the affected operation.

## Preview

From the repository root, in PowerShell:

```powershell
python Tools/RepositoryMirror/sync_code_mirror.py plan --output "$env:TEMP\WGameFramework-code-preview"
```

The output must be a **new directory outside the project**. Omitting `--output`
creates a new directory in the system temporary directory and prints its path.

The script fetches only the configured source branch into an isolated bare
repository, pins its commit SHA and reads selected Git blobs directly. It does
not check out Unity assets, download LFS objects, execute repository hooks, modify
the project working tree or include uncommitted local changes. Ordinary Git
blobs in the fetched commit may still be downloaded into the private source
cache; only the selected snapshot files are eligible for publication.

Outputs:

- `export/`: exact files proposed for the mirror.
- `PREVIEW.md`: source commit and per-target additions, modifications and deletions.
- `plan.json`: configuration, pinned source/target SHAs, file list, Git modes,
  byte sizes and content digests. This file is local state, not mirror content.
- `source.git` / `targets/`: isolated local caches, not mirror content.

If only an export is needed, without reading destinations:

```powershell
python Tools/RepositoryMirror/sync_code_mirror.py plan --export-only
```

An export-only plan cannot be published. Generate a new ordinary plan when the
destination can be inspected. Select destinations with repeated `--target NAME`;
without this option, an ordinary plan inspects all configured destinations.

The source configuration bypasses the HTTP proxy for the NAS connection only.
If a configured proxy prevents access to the mirrors, explicitly add
`--no-proxy` to `plan` to bypass HTTP proxies for this plan's Git connections.
This setting is recorded in the plan and reused by `publish`; it does not change
global Git or operating system settings. `--timeout SECONDS` sets the limit for
each Git command, with a default of 180 seconds.

On Windows, the script enables `core.longpaths=true` for its own Git commands.
This allows valid source paths to remain readable after the temporary export
directory prefix makes their absolute paths exceed the legacy 260-character
limit. It does not change global Git settings.

## Export policy

`code-mirror.json` is the maintained export policy. The initial whitelist covers
code under `Assets/`, `Packages/` and `Tools/`, plus Markdown documentation under
`Docs/`. Source extensions include C#, shader/UI source, Python, shell scripts,
web source and build project files. Package manifests and selected root/project
metadata are included explicitly. Other JSON is included only for `package.json`,
`*.schema.json` or an explicitly listed file.

The initial profile excludes `Docs/Tasks/`, `Docs/Plans/`, `Docs/Reviews/` and
`Docs/Archive/`. It also excludes resource files and machine state such as
`Library`, `Temp`, `Logs`, `UserSettings`, `.agent`, `node_modules`, `bin`, `obj`
and `__pycache__`, including accidentally tracked files. Known credential paths
are excluded. **This is a path/content-type filter, not a secret scanner:** review
the exported code and documentation for embedded confidential information before
publishing to a public repository.

For a reviewed publication subset, `profile.exclude_files` accepts exact tracked
paths and takes precedence over the whitelist and `extra_files`. Excluding a
source file also omits its paired `.meta`; a metadata file can be excluded on its
own. Keep the temporary reviewed policy and scan results outside the working
tree. Omit unsafe files instead of rewriting source bytes and claiming an exact
copy of the source commit.

Only `.meta` files paired with selected files are included. Folder metadata and
orphan resource metadata are omitted. If a selected code file is an LFS pointer,
binary, symlink, submodule, exceeds the configured size limit, has an unsafe
Windows path or collides by case, planning fails instead of silently publishing
an incomplete snapshot. Source bytes and executable Git modes are preserved.

Generated mirror files:

- `.code-mirror.json`: tool identity, source commit, export policy digest and
  selected content digest; no private cache paths or source server address.
- `CODE_MIRROR.md`: explains the mirror's purpose and limitations.
- `.gitattributes`: disables LFS filtering and text normalization for snapshot
  files. The original resource/LFS attribute rules are not copied.

The snapshot has its own history. Its first commit has no source-history parent;
subsequent snapshots append to the managed mirror branch. It is suitable for
code browsing and future snapshot comparisons, not original-history archaeology,
Unity execution or resource recovery. An assets-only source commit can still
produce a metadata update identifying the newer source SHA.

## Explicit publication

After reviewing the preview files and destination, publish that exact plan:

```powershell
python Tools/RepositoryMirror/sync_code_mirror.py publish --plan "$env:TEMP\WGameFramework-code-preview\plan.json" --target github
```

Publication validates the plan digest and complete export file set, then checks
the destination branch against the SHA recorded during planning. A pre-existing
branch must contain this tool's marker for the same source repository; otherwise
publication is refused. Choose a new mirror branch instead of replacing an
unmanaged branch such as the existing VVV `main`.

The commit is built from exactly the reviewed blobs. The destination head is
checked again immediately before pushing. A new branch uses an ordinary push,
with no force option. For updates to an existing managed branch, the parent is
the reviewed destination head and the push uses an **exact
`--force-with-lease=<ref>:<expected-sha>`** to reject concurrent changes. This
lease is a compare-and-swap guard; constructed update commits always extend the
reviewed head. The push names only this branch. There is no unconditional force,
history rewrite, `--mirror`, tag push or ref deletion.

Rerunning an already published plan checks the complete destination tree and
returns `unchanged` without creating another commit. If the destination changed
to different content, generate a new preview. Changes to the source branch after
planning do not change a pinned plan; a fresh plan is needed to include them.

`publish-receipt.json` records each selected target's status and verified commit.
Targets are independent: one failure does not undo a successful publication to
another, and the command exits nonzero if any selected target failed. A push or
readback error can leave its remote outcome uncertain; read the destination or
retry the same plan to confirm it. No cross-service atomicity is claimed.

## Adding Gitee

Add its verified address to `targets` in a local configuration copy:

```json
"gitee": {
  "url": "https://gitee.com/VERIFIED_OWNER/VERIFIED_REPOSITORY.git",
  "branch": "code-mirror",
  "bypass_proxy": false
}
```

Replace the placeholders with the exact verified repository; do not embed a
password or token in the URL. Use `plan --config PATH --target gitee`, review the
new plan, then explicitly publish it. HTTPS and SSH transports are supported;
absolute local repository paths support offline integration tests. Git URL
rewrites to different addresses are rejected so repository identity stays
explicit. A target pointing to the source repository is rejected.

## Validation

```powershell
python -B -m unittest discover -s Tools/RepositoryMirror/tests -v
```

Tests use local disposable repositories. They verify committed-source isolation,
resource/cache filtering, source byte preservation, independent snapshot history,
incremental deletion, Unicode paths, unchanged original `main`, repeat publication,
export paths longer than the Windows legacy limit,
modified-plan/export rejection, unmanaged-branch rejection, destination races,
LFS/binary/symlink/size rejection, export-only boundaries, redacted diagnostics
and independent target receipts. They do not access GitHub, Gitee or Gitea.

Git plumbing behavior is documented by
[hash-object](https://git-scm.com/docs/git-hash-object),
[update-index](https://git-scm.com/docs/git-update-index),
[commit-tree](https://git-scm.com/docs/git-commit-tree), and
[push](https://git-scm.com/docs/git-push).
