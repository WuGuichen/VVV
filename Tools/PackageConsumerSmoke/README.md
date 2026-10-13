# UPM Consumer Smoke

`UnityProject` is only the tracked source fixture. The runner copies its minimal `Assets` and `ProjectSettings` into a newly generated project outside the repository; it never uses the fixture's own `Library` as release evidence. The fixture owns the Unity-generated URP global settings and default volume profile required for a clean first import. The runner hashes the Consumer `Assets` tree and, for a Local source, the package tree before and after Unity so implicit writes fail the gate. The default `base` profile intentionally omits FMOD, FairyGUI, Unity Transport, and VFX Graph. The runner checks that those optional packages are absent from the resolved lock and that every asmdef in their reverse-reference closure carries the expected compile gate.

Three positive profiles install one optional package at a time, require the corresponding adapter assembly to compile, and run one focused behavior test:

- `with-fairygui`
- `with-unity-transport`
- `with-vfx-graph`

FMOD remains excluded because its project-owned source under `Assets` has not yet passed dependency-completeness, licensing, LFS retrieval, and clean external Consumer validation. A local FMOD package manifest exists, but it is not yet an approved immutable optional source.

Run from the repository root:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ./Tools/Harness/run_upm_consumer_smoke.ps1
```

Run a positive local profile:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File ./Tools/Harness/run_upm_consumer_smoke.ps1 `
  -Profile with-unity-transport
```

Build a real host-platform Player after the base EditMode smoke:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File ./Tools/Harness/run_upm_consumer_smoke.ps1 `
  -Profile base `
  -IncludePlayerBuild
```

`-IncludePlayerBuild` is intentionally opt-in and currently accepts only the
`base` profile. It builds the tracked minimal Consumer scene and runtime probe,
records build evidence, and repeats both mutation guards after the build. It
does not launch the built Player; deterministic Player execution and optional
adapter Player coverage remain separate release gates.

Run the same profile against an immutable Gitea commit:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File ./Tools/Harness/run_upm_consumer_smoke.ps1 `
  -PackageSource PinnedGit `
  -Revision <full-40-character-commit-sha> `
  -Profile with-unity-transport
```

If the manifest repository URL is not reachable from the current machine, pass
the credential-free canonical repository root explicitly:

```powershell
pwsh -NoProfile -File ./Tools/Harness/run_upm_consumer_smoke.ps1 `
  -PackageSource PinnedGit `
  -GitUrl <gitea-repository-root> `
  -Revision <full-40-character-commit-sha> `
  -Profile base
```

`-GitUrl` is an execution override, not permission to silently change release
metadata. A promoted receipt must still record which repository authority was
used and require requested/resolved commit equality.

Pinned FairyGUI mode resolves both MxFramework and `Packages/com.fairygui.gui` from the same repository revision. Transport and VFX Graph resolve their exact Unity package baselines.

Pass `-UnityPath` when Unity Hub is not installed in its default location. The default external root is:

```text
C:\WGameFrameworkConsumerSmokeRuns\
  Local\
    base\
    with-fairygui\
    with-unity-transport\
    with-vfx-graph\
  PinnedGit\
    base\
    with-fairygui\
    with-unity-transport\
    with-vfx-graph\
```

Each leaf contains its own disposable `UnityProject`, `unity.log`, NUnit XML,
schema 4 JSON/Markdown Consumer reports, and `package-boundary.json`. The JSON records exact expected/observed test names, candidate/harness identity, optional provenance, Player three-state values, artifact hashes, mutation guards and forbidden-log policy. An opt-in
Player build also writes `player-build/`, `player-build.log`, and
`player-build-report.json`. The boundary report
comes from the same cross-platform Python preflight used outside Unity and is
evaluated against the package actually resolved in `Library/PackageCache` for
PinnedGit runs. Python 3 must be on `PATH`, or supplied through `PYTHON_BIN`.
The long-lived development consumers at `C:\WGameFrameworkConsumerSmoke` (local
source) and `C:\WGameFrameworkConsumerRemote` (immutable remote commit) are
separate and must not be passed as `-ConsumerRoot`. `-ConsumerRoot
<absolute-path>` can select a different generated-run root, but the runner
rejects a drive root, either long-lived consumer, the framework repository, any
repository child, or a parent directory that contains the repository.

On macOS/Linux, use PowerShell 7 (`pwsh`) and provide an absolute Unity path and
an external absolute `-ConsumerRoot`; the Windows default root is not portable:

```powershell
pwsh -NoProfile -File ./Tools/Harness/run_upm_consumer_smoke.ps1 `
  -UnityPath <unity-editor-executable> `
  -ConsumerRoot <external-generated-run-root> `
  -PackageSource Local `
  -Profile base
```

The default run removes only the selected consumer's generated project state before launching Unity. This makes merge evidence a clean import. `-ReuseLibrary` is available for faster diagnostics, but should not be used as the final package-boundary gate.
