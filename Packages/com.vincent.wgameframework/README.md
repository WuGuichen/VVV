# MxFramework Unity Package

MxFramework is the reusable Unity 6 framework extracted from WGame. This package is currently a preview: its APIs and package layout may still change before the first stable release.

## Supported editor and required packages

- Unity `6000.5` or newer within the Unity 6 line.
- Input System `1.19.0`.
- Unity built-in modules used directly by the package: Android JNI, Animation, Asset Bundle, Audio, Image Conversion, IMGUI, JSON Serialize, Particle System, Physics, UI Elements, and Unity Web Request (`1.0.0`).
- Newtonsoft Json `3.2.2`.
- Universal Render Pipeline `17.5.0`.
- Unity Test Framework `1.7.0`.

These versions describe the authoritative baseline validated by this repository. A consuming project may resolve compatible newer versions, but must run its own compile and smoke tests.

## Install during preview

For local development, add the package with a `file:` reference that points to the embedded package root in a framework checkout:

```json
{
  "dependencies": {
    "com.vincent.wgameframework": "file:../../WGameFramework/Packages/com.vincent.wgameframework"
  }
}
```

The relative path is resolved from the consumer's `Packages/manifest.json` directory. A remote candidate that has passed the pinned Consumer Smoke uses the repository URL, package subdirectory, and a full immutable commit SHA:

```json
{
  "dependencies": {
    "com.vincent.wgameframework": "git+http://192.168.1.210:3002/vincent/WGameFramework.git?path=/Packages/com.vincent.wgameframework#<full-40-character-commit-sha>"
  }
}
```

The `path` query must precede the `#revision`. Do not consume an unpinned branch as a release, and do not assume that every commit is package-ready merely because it resolves. The repository does not declare a stable channel until a stable SemVer tag has passed the external consumer gate.

## Optional integrations

`optional-integrations.json` is the machine authority for profile name, package/version provenance, define, adapter assembly, behavior test and support tier. FairyGUI, Unity Transport and VFX Graph are O2 matching profiles; FMOD and the project-plugin Magica Cloth adapter remain O0 and have no positive Consumer profile.

The base package does not require the following integrations:

- FMOD assemblies require the consuming project to provide the FMOD Unity integration and define `MXFRAMEWORK_FMOD` for every supported Build Profile/platform. `Assets/Plugins/FMOD` already contains local package metadata, but that source has not passed the dependency, licensing, LFS, and clean external Consumer gates required to make it an approved optional package; the symbol therefore remains a temporary manual gate.
- FairyGUI assemblies activate when package `com.fairygui.gui` version `5.2.0` or newer is installed.
- Unity Transport assemblies activate when package `com.unity.transport` version `6.5.0` or newer is installed.
- VFX Graph assemblies activate when package `com.unity.visualeffectgraph` version `17.0.0` or newer is installed.

Consumers that do not install an optional integration should still import and compile the base package.

FairyGUI remains an explicit consumer dependency rather than a transitive dependency of MxFramework. Local consumers can reference this repository's `Packages/com.fairygui.gui` folder. A reviewed remote consumer can pin that package to the same immutable repository commit:

```json
{
  "dependencies": {
    "com.vincent.wgameframework": "git+http://192.168.1.210:3002/vincent/WGameFramework.git?path=/Packages/com.vincent.wgameframework#<full-40-character-commit-sha>",
    "com.fairygui.gui": "git+http://192.168.1.210:3002/vincent/WGameFramework.git?path=/Packages/com.fairygui.gui#<same-full-40-character-commit-sha>"
  }
}
```

The package owns only self-contained package contract tests and the explicit `RuntimeHostMinimalLifecycle` sample. CharacterTest, repository validation tests and full playable demos remain project-owned. Optional adapters are compiled only when their matching dependency and define constraints are present.

## Verification

Run the fast local package-boundary smoke from the repository root:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Harness/run_upm_consumer_smoke.ps1
```

Run an enabled optional-adapter profile by selecting one of the supported profiles:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Harness/run_upm_consumer_smoke.ps1 `
  -Profile with-fairygui

powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Harness/run_upm_consumer_smoke.ps1 `
  -Profile with-unity-transport

powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Harness/run_upm_consumer_smoke.ps1 `
  -Profile with-vfx-graph
```

After a candidate commit is reachable from Gitea, run the immutable remote boundary smoke:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Harness/run_upm_consumer_smoke.ps1 `
  -PackageSource PinnedGit `
  -Revision <full-40-character-commit-sha>
```

Add the same `-Profile` argument to run a pinned optional-adapter profile. Both modes recreate isolated generated Unity state by default, run the shared cross-platform E0 boundary checker, verify the resolved package manifest, lock, optional-package/asmdef gates and Editor log, and execute a minimal `RuntimeHost` lifecycle test. Enabled adapter profiles additionally require the selected optional package, compiled adapter assembly, and one focused behavior test. Pinned mode requires `source=git` and exact equality between the requested and resolved framework commit hashes, reads the observed package metadata from the Consumer's `Library/PackageCache`, and records the deterministic package tree hash in its report. The pinned FairyGUI profile also resolves `Packages/com.fairygui.gui` from the same full commit SHA. Python 3 must be available on `PATH`, or selected with `PYTHON_BIN`.

All generated Consumer projects, Libraries, logs, and reports live outside the repository under `C:\WGameFrameworkConsumerSmokeRuns\<Local|PinnedGit>\<profile>\` by default. `C:\WGameFrameworkConsumerSmoke` and `C:\WGameFrameworkConsumerRemote` are reserved for the long-lived local-source and immutable-remote development consumers and are never used as generated gate output. Override `-ConsumerRoot` only when the replacement is also outside the repository and is dedicated to disposable gate runs; the runner rejects a drive root, either long-lived consumer, the framework repository, any repository child, or a parent that contains the repository.

FMOD does not yet have a positive external Consumer profile. Its project-owned source under `Assets` has a package manifest, but it does not yet have an approved, dependency-complete, license-reviewed, LFS-verified immutable Consumer source. Pass `-ReuseLibrary` only for a diagnostic rerun; release evidence must use the default clean mode.

See `Docs/UPM_EXTERNAL_CONSUMPTION_IMPLEMENTATION_PLAN.md` for Phase A-C ownership and version authority, and `Docs/UPM_OPTIMIZATION_ROADMAP.md` for the audited Phase D optimization plans.
