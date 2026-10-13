# MxFramework Lightweight Harness

This folder contains the first repository-level checks intended for local use
and later Gitea Actions wiring.

## Local Usage

The Windows CharacterTest launcher (`build_character_test_player.ps1`) checks the
selected Unity executable's ProductVersion (including revision) against
`ProjectSettings/ProjectVersion.txt` before creating output or starting Unity.
This also applies to `-UnityExe` and `UNITY_EXE` overrides. It rejects an Editor
lock and nonempty output directories so a build uses an isolated project and a
fresh output path without overwriting retained evidence. Outputs must not be in
Assets, Packages, ProjectSettings, Library or project Temp.

`CharacterTestBuildInvocation.json` records the selected Editor, project pin,
configuration, package/settings hashes and Git commit/status. This is invocation
evidence, not proof of compilation or a full source snapshot. An isolated project
may provide `SourceSnapshot.json` for full copied-file provenance; its hash is
included. The Unity log's actual engine version is checked after process exit.
`BuildOptimizationOverlay.json`, when supplied, contributes the original hashes
and actual invocation hashes of approved source edits on top of that snapshot.
The base snapshot remains immutable; validation applies to that explicit overlay,
not to concurrent changes in the source checkout.

Run the independent preflight regressions without launching Unity:

```powershell
./Tools/Harness/tests/test_character_test_build_identity.ps1
```

Create an isolated disk snapshot without launching Unity:

```powershell
./Tools/Harness/new_character_test_build_snapshot.ps1 -DestinationRoot E:/Unity/V66/S-unique
```

The destination must be new, separate and outside the source tree. The command
copies current disk authority, including uncommitted files, formal generated
assets, `.meta` and current StreamingAssets. It excludes Unity local state,
personal state, Git metadata, generated IDE projects and Tools build/dependency
caches. Source before copy, destination bytes, source after copy and Git status
must agree. Changes or links cause rejection; rejected copies remain for
inspection with `SourceSnapshotAttempt.json` and have no verified
`SourceSnapshot.json`. No files are linked to the source project and no Unity
Editor is launched. A successful snapshot is an observed disk state, not a claim
that unsaved Editor state was captured or that the source will stay unchanged.
The build launcher rejects attempts marked Copying/Rejected or missing the
verified snapshot receipt. Its invocation receipt includes the base snapshot's
fingerprint and source Git state alongside the actual invocation settings.

Run snapshot regression checks independently:

```powershell
./Tools/Harness/tests/test_character_test_build_snapshot.ps1
```

`audit_character_test_resource_inclusion.ps1 -ProjectRoot <verified-snapshot>
-DetailsPath <retained-Player-details.json> -OutputPath <new-external-audit.json>`
joins historical dual-entry paths to snapshot mapping/catalog entries and direct
YAML GUID referencers. It verifies inspected inputs against the snapshot hashes
and writes outside the snapshot. It does not traverse FBX/binary dependencies,
prove dynamic loading behavior, classify assets as unused, modify inclusion or
estimate compressed savings. Historical Player data keeps its build identity.

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

Python checks use `python3` when available, then `python`. Set `PYTHON_BIN` if
the runner uses a custom interpreter path.
On Windows checkouts, prefer Git for Windows Bash for the `.sh` entrypoint.
Running the shell entrypoint through WSL against the Windows worktree can make
WSL Git report broad CRLF-only diffs and should be avoided.

### Cross-platform UPM boundary preflight

Run the Unity-independent E0 package check from the repository root:

```bash
python3 Tools/Harness/check_upm_package_boundary.py
```

To audit an immutable candidate, extract `Packages/com.vincent.wgameframework` from that
full commit into a clean temporary directory and pass that directory explicitly:

```bash
python3 Tools/Harness/check_upm_package_boundary.py \
  --package-root <clean-candidate-package-root> \
  --candidate-commit <full-40-character-commit-sha> \
  --report-json <outside-package-root>/package-boundary.json
```

The default preview policy blocks malformed manifests/asmdefs, dependency-set
drift, optional-adapter reverse-closure violations, unguarded direct uGUI source
imports, duplicate assembly names, and generated or personal files in the
package tree. It reports planned
surface and release work as deterministic readiness gaps. Use
`--require-surface-manifest` after UPM surface L0 is adopted, and
`--require-release-metadata` for release-candidate policy. A passing E0 result
does not mean Unity resolved, imported, compiled, or tested the package.

`run_upm_consumer_smoke.ps1` invokes this same checker against the resolved
package and writes `package-boundary.json` beside its Unity report. Python 3 is
therefore required for the Consumer runner; set `PYTHON_BIN` if it is not on
`PATH`. When multiple `python3` executables are visible, the runner selects the
first Application returned by PowerShell; use `PYTHON_BIN` to make a different
interpreter authoritative.

P7 evidence commands:

```powershell
python Tools/Harness/validate_upm_consumer_evidence.py profile <report.json> --artifact-root <row-root>
python Tools/Harness/validate_upm_consumer_evidence.py matrix <matrix.json> --artifact-root <matrix-root>
python Tools/Harness/validate_upm_consumer_evidence.py receipt-candidate <receipt.json> --artifact-root <matrix-root>

pwsh -File Tools/Harness/run_upm_consumer_matrix.ps1 `
  -Revision <full-40-character-sha> `
  -OutputRoot <short-external-path>
```

The matrix command requires a clean checkout at the exact candidate, runs the fixed eight Local/PinnedGit rows, validates every referenced artifact, and computes `promotable`; callers cannot force it true. Keep `OutputRoot` short because Unity PackageCache and test paths remain subject to Windows path limits.

## CharacterTest Windows Player Package Test

Run the local Windows64 CharacterTest package test from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File Tools\Harness\build_character_test_player.ps1
```

The script resolves the Unity editor from `ProjectSettings/ProjectVersion.txt`
through the Unity Hub install path. Pass `-UnityExe <path>` or set `UNITY_EXE`
when Unity is installed elsewhere. It runs Unity BatchMode with
`MxFramework.Game.CharacterTest.Editor.CharacterTestPackageTestCommand.RunDefaultPackageTest`,
builds the global Player resource catalog and AssetBundles, builds a
`StandaloneWindows64` Release Player for `Assets/Scenes/CharacterTestScene.unity`,
starts the Player warm-run, and validates the reports.

The default Windows policy remains IL2CPP OptimizeSpeed, Low managed stripping,
Release C++ compiler and managed-code variant, D3D11, incremental GC and LZ4HC.
`-Configuration Development` uses the Checked managed variant and LZ4, and keeps
the owned Bundle cache for identical inputs. Cache identity includes Unity
version, active platform, configuration, profile/build-plan fingerprint, Bundle
options and Standalone defines. Release cleans only its own Bundle cache on
every build. Local caches live in ignored `.agent/BuildCache/AssetBundles`, not
Library (Unity rejects Bundle build output inside Library).

Bundle manifests and `Library/LastBuild.buildreport` are captured immediately
after the Bundle build, before the Player can replace the latest report. The
Player report and matching Unity 6.6 BuildHistory directory are copied into
`BuildAnalysis/Player`; Bundle evidence is in `BuildAnalysis/AssetBundles`.
Ordinary Bundle builds may have a zero build GUID and no BuildHistory directory;
the receipt records actual report type/result, timing, manifest hash and source
identity rather than fabricating a Player-style GUID. Existing resource marker,
publication hashes and before/after input checkpoints remain acceptance gates.

The existing prefab producer publishes all playable character preview Prefabs
through the sole ResourceMapping profile aggregator/compiler. During Player
build only, generated Resources mirrors and fully mapped model/texture inputs
can be moved through AssetDatabase into owned non-Resources paths. The scope
checks the formal catalog, GUIDs, payload/meta hashes and scene references, then
restores on success or exception. Original assets, unknown content, animations,
VFX and direct scene references remain. Details reports normalize temporary
paths back to source paths while retaining the actual build paths for evidence.
Do not delete a source asset merely because it appears in both Player and Bundle.

Run independent experiments into separate, new external directories:

```powershell
pwsh -File Tools/Harness/build_character_test_player.ps1 `
  -ProjectPath <isolated-project> -OutputRoot <new-size-output> `
  -Configuration Release -Experiment OptimizeSize -JobWorkerCount 2

pwsh -File Tools/Harness/build_character_test_player.ps1 `
  -ProjectPath <isolated-project> -OutputRoot <new-medium-output> `
  -Configuration Release -Experiment MediumStripping -JobWorkerCount 2
```

`OptimizeSize` changes code generation only and retains Low stripping.
`MediumStripping` changes stripping only and retains OptimizeSpeed. Both require
Release; neither changes the ordinary Release default. Run one build at a time,
using an isolated disk snapshot when the shared Editor is occupied. The launcher
uses BelowNormal priority and defaults to two Unity job workers; this is not a
promise that the external native compiler uses exactly two threads.

Immediately after **each** build, before another build replaces Library data,
run the PowerShell 7 metadata checks:

```powershell
pwsh -File Tools/Harness/verify_character_test_json_preservation.ps1 `
  -ProjectPath <isolated-project> -BuildOutputRoot <that-output>
pwsh -File Tools/Harness/verify_character_test_bundle_preservation.ps1 `
  -ProjectPath <isolated-project> -BuildOutputRoot <that-output>
```

The JSON check compares explicit link.xml types before/after stripping and binds
each inspected DLL to the build's hashed archived managed assemblies. Stale
Library data is rejected before output creation. The Bundle check reads captured
manifests, resolves MonoScript GUIDs and SerializeReference class identifiers,
and checks the actual build-bound stripped assemblies. It also works against
retained build outputs; changed/missing script sources fail rather than being
guessed. Both archive JSON evidence, reject ambiguous/missing types and preserve
existing evidence directories. `-EvidenceDirectoryName` permits a separately
named recheck without overwriting prior results. They never load Player DLLs.

Type presence plus the configured Player probe does not prove all content,
reflection paths, long sessions or graphics compatibility. Warm-run Stopwatch
samples cover the existing GameManager loop only. Release ProfilerRecorder
markers can be unavailable: zero values for unavailable markers are not zero
execution cost. Compare one variable at a time; single sequential captures on a
contended workstation do not establish performance superiority or clean build
speed. Retain OptimizeSpeed/Low until broader acceptance warrants promotion.

Defaults use Windows x64 IL2CPP Release; pass `-Configuration Development` for a development build. Outputs are written under the system temporary directory `WGF-Pkg-<id>/` (or explicit `-OutputRoot`), including
`unity_batch.log`, `CharacterTestPackageTestSummary.txt`, the editor build/run
report, and the Player warm-run reports under `Reports/`. The script returns 0
only when Unity exits successfully, the summary has `status: Passed`, the Player
run report has `status: finished`, timing/profiler reports both exist, and the
Player animation report has `status: ready`. The package command stages
generated Codex Flow Test package JSON into
`Assets/StreamingAssets/MxFramework/Generated/CharacterPackages/codex_flow_test/`
before building the Player, including runtime-required `config/` files and
`package_cache/import_report.json`; that StreamingAssets mirror is ignored build
output.
Diagnostic options: `-SkipResourceBuild` skips resource generation, and `-NoRun`
builds the Player without launching the warm-run.

## Character Authoring Source Delivery

`check_character_authoring_sources.py` remains mandatory in the lightweight
Harness. Its delivery scope is the Git index plus untracked, nonignored candidate
files. Declared optional sources are always checked for existence and SHA, even
when ignored. Force-added backups still fail. Existing HEAD publication archives
are retained as historical data; new or changed archives cannot conceal active
sources. Scope, manifest ownership and retained-archive rules are defined in the
[numeric character authority](../../Docs/Guides/WGAME_NUMERIC_CHARACTER_AUTHORING.md#制作清单审计与本地备份).

```powershell
python -B Tools/Harness/check_character_authoring_sources.py
python -B -m unittest discover -s Tools/Harness/tests -p test_character_authoring_sources.py
```

The regressions use temporary Git fixtures; their commits/deletions never touch
the project or user backups. The checker does not refresh hashes, delete files,
run Blender/Unity or certify visual/import acceptance. Published bake outputs use
the existing package catalog/binding registration. The checker verifies the
producer's four fixed outputs against package identity, adopted source provenance,
matching binding data (except producer-remapped relative file paths), unique
catalog keys, content hashes, actual Final/package bytes and LFS. This avoids a
second optional-DCC registry. Published source hashes are frozen independently of
the current managed Work hash. A registered save reports unpublished authoring
changes without invalidating intact old artifacts; a current Work/manifest hash
mismatch reports registration pending and still fails strict DCC validation in
this same unified entry. Missing files, unsafe paths/hashes and broken artifact,
binding or catalog identities/content remain blocking.

Source evidence states are separate from artifact checks: matching current bytes,
HEAD bytes or HEAD/LFS cached bytes are verified as available; cached bytes alone
leave versioned linkage unverified. Unavailable source bytes are explicitly
unverified. A valid hash string is not source acceptance. The binding retains no
managed bake-snapshot path, so its snapshot bytes remain unverified here. Checks
neither fetch/restore sources nor depend on Temp or arbitrary manual backups.

## Code and Authority Document Delivery

Reuse `check_docs_health.py`, already invoked by `run_lightweight_checks.sh`.
The first registered features are the Runtime AI Lab CardTrial and Pose/VFX
budget authorities; other source is explicitly outside this mapping coverage.
Declare each relevant repository path/glob in its authority Markdown:

```text
> Code-Doc: `Packages/com.vincent.wgameframework/Module/Feature*.cs`
```

Changed related source requires that same authority document in the delivery;
updating only README/PROJECT_INDEX is insufficient. Pure refactoring can document
the review and unchanged contract; never invent implementation or passing evidence
to satisfy the check. Baseline declarations prevent silently disabling a feature
by removing its mapping. Explicit authority renames transfer the existing patterns
to the changed replacement document.

```powershell
python Tools/Harness/check_docs_health.py <changed-paths-file> --base-ref HEAD
python -m unittest discover -s Tools/Harness/tests -p test_docs_code_delivery.py
```

The legacy single changed-paths-file invocation remains supported; the Python
checker consumes the supplied list and does not collect Git changes itself.
Running `Tools/Harness/run_lightweight_checks.sh` invokes this check unconditionally
and combines unstaged, staged and untracked paths with the committed range from
the merge base of `HEAD` and `HARNESS_BASE_REF` (default `origin/main`) to `HEAD`.
Deleted paths are included and rename collapse is disabled, so both old and new
source paths are checked. If the base ref is missing, the shell warns and skips
the committed range; use an existing branch/ref to check that range. The Python
`--base-ref` selects the declaration baseline; it does not enable the check.
This is enforcement when the local Harness command runs, not an installed hook
or an enabled CI workflow.

Passing validates mapping and delivery presence, not semantic correctness: a
whitespace-only or incorrect prose edit can pass. The owner/reviewer must compare
claims to source and applicable execution receipts; unmapped source is not claimed
as audited. Defaults and card descriptions keep their existing generators and
published projections rather than a hand-maintained duplicate table.

## Current Checks

- `check_forbidden_paths.sh`: fails when changed files include Unity generated
  state, local tool caches, OS metadata, or root Unity `.csproj` / `.sln`
  project files.
- `check_unity_meta.py`: fails when changed files under `Assets/` introduce
  Unity assets without matching `.meta` files.
- `check_docs_health.py`: validates changed-doc Status/entrypoints/removed terms,
  and checks declared code-to-authority delivery using `Code-Doc` lines. It does
  not verify prose or behavior truth; see the delivery section below.
- `check_code_standards.py`: report-only scan for first-party `.cs` and
  `.asmdef` files under `Packages/com.vincent.wgameframework` and `Tools`. It reports
  likely hot-path allocation risks, obsolete Unity API usage, runtime
  `UnityEditor` references, direct framework `Time.*` reads, public hot APIs
  without allocation behavior notes, unsafe constructs, and first-party unsafe
  assembly switches. Set `CODE_STANDARDS_FAIL_ON_FINDINGS=1` to make findings
  blocking after a baseline cleanup.
- `check_upm_package_boundary.py`: cross-platform, Unity-independent E0 audit
  of package metadata, exact mandatory dependencies, all asmdefs, optional
  integration reverse closures, direct non-mandatory uGUI source imports,
  package-tree hygiene, surface ownership status,
  initializer classification status, and release metadata readiness. Its JSON
  output is deterministic and must be written outside the inspected package.
- `check_audio_assets.py`: release-oriented Audio audit for Semantic/Physical
  lifecycle and audition evidence, Generated/Streaming catalog mirrors,
  Physical/global link hash, FMOD snapshot freshness, Active zero-length
  Events, Desktop Bank mirrors, and unresolved Git LFS pointers. It is kept
  separate from the lightweight harness because an incomplete content checkout
  is expected to fail it; use `--report-only` only when collecting a baseline.
- `profile_analyzer_gc_summary.py`: report-only helper for Profile Analyzer
  `frameTime.csv` / `markerTable.csv` exports and best-effort `.pdata` reads. It
  summarizes frame GC and ranks markers by GC allocation totals for profiler
  review notes.
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

## Code Standards Baseline

To inspect the current first-party code baseline without relying on changed files:

```bash
Tools/Harness/check_code_standards.py --all
```

For PR comments or quick baseline checks, use the compact summary:

```bash
Tools/Harness/check_code_standards.py --all --summary-only
```

This command is expected to report historical debt until the relevant modules
are cleaned up. New agent work should at minimum avoid increasing the report and
should explain any new finding it intentionally leaves in place.

Unsafe policy baseline snapshot on 2026-06-18 reported 150 findings across 1799
first-party `.cs` / `.asmdef` files. The largest bucket is the historical
`Core/Extensions/ZString.cs` unsafe implementation, plus one fixed-size
`stackalloc` in a Story demo. Treat that number as a cleanup baseline, not as an
accepted target state or evidence that unsafe should spread to ordinary logic.

## Profile Analyzer GC And Timing Summary

After exporting Profile Analyzer CSV files, generate a compact GC summary with:

```bash
python3 Tools/Harness/profile_analyzer_gc_summary.py \
  --frame-time /Users/vincent/Documents/frameTime.csv \
  --marker-table /Users/vincent/Documents/markerTable.csv \
  --frame-start 59 \
  --frame-end 94 \
  --exclude-frame 200 \
  --drop-last-frame \
  --top-markers 20 \
  --exclude-editor-noise
```

The helper is report-only. It does not replace Unity Profiler / Profile Analyzer
inspection; it gives PR comments and remediation notes a repeatable summary of
frame GC, marker GC allocation totals, and `.pdata` marker timing exports.
For CSV `markerTable.csv` inputs, frame-window filtering depends on the Profile
Analyzer export window. For `.pdata` inputs, `--frame-start`, `--frame-end`,
`--exclude-frame`, and `--drop-last-frame` are applied to marker aggregation as
well as frame summaries. Repeat `--exclude-frame` to omit more than one display
frame. When a `.pdata` frame filter selects only part of a capture, unselected
frames are skipped directly instead of fully parsed; this keeps single-frame
audits usable on large Profile Analyzer files.

When a Profile Analyzer `.pdata` file is available, the same helper can generate
CSV-compatible exports without opening the Profile Analyzer window:

```bash
python3 Tools/Harness/profile_analyzer_gc_summary.py \
  --pdata Temp/ProfileAnalyzer/after_p1_p4_smoke.pdata \
  --pdata-thread "1:Main Thread" \
  --frame-time-out Temp/ProfileAnalyzer/after_frameTime.csv \
  --marker-table-out Temp/ProfileAnalyzer/after_markerTable.csv \
  --marker-timing-out Temp/ProfileAnalyzer/after_markerTiming.csv \
  --exclude-frame 200 \
  --drop-last-frame \
  --top-markers 20 \
  --top-time-markers 20 \
  --exclude-editor-noise
```

The `.pdata` parser targets Unity Profile Analyzer version 8/9 files and should
be treated as a review aid, not a replacement for the package UI when exact
visual comparison is needed. `--marker-timing-out` writes a Profile Analyzer-like
marker timing table with median/min/max, per-frame counts, individual timing,
and GC columns for the selected `.pdata` thread and frame window.

To compare two marker timing exports, use the report-only comparison helper:

```bash
python3 Tools/Harness/profile_analyzer_compare_markers.py \
  --before Temp/ProfileAnalyzer/before_markerTiming.csv \
  --after Temp/ProfileAnalyzer/after_markerTiming.csv \
  --before-label before_clean_capture \
  --after-label after_clean_capture \
  --out-csv Temp/ProfileAnalyzer/before_after_marker_comparison.csv \
  --out-md Temp/ProfileAnalyzer/before_after_marker_comparison.md
```

The comparison helper expects the semicolon-delimited table written by
`profile_analyzer_gc_summary.py --marker-timing-out`. It reports median/mean/max
time and GC deltas for the CharacterTest performance marker set by default;
repeat `--marker` to compare a custom marker or glob. If the inputs are not the
same clean scenario, put that limitation in `--note` and do not use the output as
a formal performance delta.

For the CharacterTest Tornado active-field after capture, run the explicit Unity
test
`MxFramework.Tests.Game.CharacterTest.CharacterTestFireballPlayModeTests.CharacterTestScene_PlayModeTornadoProfileAnalyzerCapture_WritesPDataIntoTemp`.
It writes `Temp/ProfileAnalyzer/current_after_tornado_playmode.pdata` and
`Temp/ProfileAnalyzer/current_after_tornado_playmode_profile_analyzer_capture_summary.txt`.
Then export review CSVs with:

```bash
python3 Tools/Harness/profile_analyzer_gc_summary.py \
  --pdata Temp/ProfileAnalyzer/current_after_tornado_playmode.pdata \
  --pdata-thread "1:Main Thread" \
  --frame-time-out Temp/ProfileAnalyzer/current_after_tornado_playmode_frameTime.csv \
  --marker-table-out Temp/ProfileAnalyzer/current_after_tornado_playmode_gcMarkerTable.csv \
  --marker-timing-out Temp/ProfileAnalyzer/current_after_tornado_playmode_markerTiming.csv \
  --exclude-frame 200 \
  --top-markers 25 \
  --top-time-markers 25 \
  --exclude-editor-noise
```

To generate a repeatable CharacterTest Tornado baseline/current artifact pair,
use:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File Tools\Harness\profile_analyzer_capture_pair.ps1 `
  -SkipAfterCapture `
  -AfterPData Temp\ProfileAnalyzer\current_after_tornado_playmode.pdata
```

For the heavier 96-target Tornado stress pair used to validate the dedicated
dynamic-field query under a fresh baseline load, use:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File Tools\Harness\profile_analyzer_capture_pair.ps1 `
  -StressScenario `
  -StressTargetCount 96 `
  -AfterWorktree E:\WGF_pa_after_stress2
```

The pair script creates or reuses a detached `origin/main` worktree at
`E:\WGF_pa_base_stress_current`, applies the capture harness patch plus the marker-only
`profile_analyzer_baseline_tornado_markers.patch`, runs Unity through the
command-line `CharacterTestProfileAnalyzerCommandLineCapture.RunPairCapture`
entrypoint for the ordinary pair or `RunStressPairCapture` for
`-StressScenario`, and writes artifacts under `ProfileAnalyzer/paired_tornado_capture`
or `ProfileAnalyzer/paired_tornado_stress_capture`.
It writes `.pdata` files to absolute paths outside the tested project's `Temp/`
folder because Unity can clean project `Temp/` on shutdown. The runner waits for
real Play Mode `Time.frameCount` progress so `yield return null` records frame
data instead of only Editor update ticks. The capture summary appends a
`tornadoEvidence` block and asserts that the window contained Tornado projectile
flight, movement, tick VFX, and target damage. The ordinary pair also asserts a
single-target attraction movement check; the stress pair instead asserts the
requested target count and non-zero Tornado marker samples because target crowd
motion makes one target's X displacement a poor acceptance signal.
If the default baseline worktree already contains an older capture harness patch,
the script reports it as stale instead of silently reusing it; pass
`-BaselineWorktree` with a clean path or refresh the dedicated baseline worktree.
If the main project is already open in Unity, use `-AfterWorktree` with a
detached worktree that contains the current tracked diff instead of starting a
second Editor against the same project path.
Do not use `-BatchMode` for the formal paired artifact unless you have verified
the saved `.pdata` frame count; Unity batch mode can produce a very short
ProfilerDriver history for this editor-driven capture.

The generated comparison is report-only for rows named in the Markdown validity
notes. Rows that have both before and after values are comparable. The baseline
marker patch gives the old Tornado active-field `QueryCapsule` path a compatible
`CharacterTest.InteractionPhysics.TornadoFieldQuery` marker without changing the
old query algorithm; after-only markers such as `TornadoIndexRebuild` still have
no formal delta.

For CharacterTest sessions that only have the current Unity Profiler buffer, use
the editor menu `MxFramework/CharacterTest/Performance/Export Current Profiler
Raw CSV`. It writes `Temp/ProfileAnalyzer/current_raw_frameTime.csv`,
`Temp/ProfileAnalyzer/current_raw_markerTiming.csv`, and
`Temp/ProfileAnalyzer/current_raw_profiler_export_summary.txt` from
`RawFrameDataView`. The menu defaults to the most recent 30 Main Thread frames to
avoid long editor stalls; treat this as after-side profiler-buffer evidence, not
as a replacement for saved Profile Analyzer before/after captures.

If the latest 30 frames miss the Tornado active-field window, use
`MxFramework/CharacterTest/Performance/Export Current Profiler Raw CSV (Tornado
Window)`. It searches the current buffer for
`CharacterTest.InteractionPhysics.TornadoFieldQuery` on the Main Thread, exports
a small frame window around the newest match to
`Temp/ProfileAnalyzer/current_raw_tornado_frameTime.csv` and
`Temp/ProfileAnalyzer/current_raw_tornado_markerTiming.csv`, and records the
search/window metadata in
`Temp/ProfileAnalyzer/current_raw_tornado_profiler_export_summary.txt`.


### Native size evidence in package overview

`analyze_package_native_size.ps1` is called automatically by the Unity package report collector on Windows. It runs with Windows PowerShell 5.1 and uses the installed Visual Studio DIA SDK plus the .NET Framework SDK TlbImp. Generated interop assemblies stay in `Library/PackageOverview/Dia/` and are not Player content.

`PackageNativeSizeAnalysis.cs` validates DLL/PDB GUID and age, clips section contributions to PE file-backed ranges, counts overlapping module ownership once, and emits native attribution JSON. Missing tools are reported as unavailable instead of estimating native size from managed DLLs. The combined build-bound asset/native snapshot is `CharacterTestPackageDetails.json`; file sizes and hashes reject evidence from another Player. Unity asset figures are serialized, pre-final-compression figures, not downloadable bytes saved.
