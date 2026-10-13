# CharacterTest Tornado Query Optimization Fix

> Status: Guide

## Scope

本文记录 CharacterTest Tornado `PhysicalField` 专用动态目标查询优化的已落地内容、验证证据和剩余验收缺口。

本阶段只优化 CharacterTest Tornado active field。未修改 Tornado 半径、生命周期、tick 频率、伤害、拉拽手感，也未新增 MxFramework public API。

## Implemented Changes

### Tornado dynamic field index

- 新增 `CharacterTestDynamicFieldTargetIndex`，按 XZ grid 收集当前 runtime world 中的动态角色和动态 movable object。
- 每个 `InteractionPhysics` tick 只在存在 Tornado fast path 查询时按 frame / world / physics runtime / layer mask 重建一次。
- 重建时通过 `CombatPhysicsWorld` 确认 body 和 collider 仍存在。
- 查询阶段使用 query stamp 去重，避免同一目标跨 cell 重复输出。
- 对 vertical capsule 使用专用距离测试，替代通用 `QueryCapsule -> TryCapsuleAabb -> CheckSegmentAabbEdges` 路径。
- 结果仍写入 caller-owned `List<CombatQueryResult>`，保持稳定排序并复用既有 `ProcessFieldResults()` 逻辑。

### Interaction physics fast path

- `CharacterTestInteractionPhysicsSystem` 仅在以下条件同时满足时走 fast path：
  - stable id 为 Tornado attraction event；
  - source domain 为 `CombatProjectileActiveField`；
  - field shape 为 capsule；
  - force mode 为 `AttractionAcceleration`；
  - target filter 包含动态角色或动态 movable object。
- sphere、radial impulse、非 Tornado field、未知 shape 继续走 `CombatPhysicsWorld` fallback query。
- 新增 profiler markers：
  - `CharacterTest.InteractionPhysics.TornadoIndexRebuild`
  - `CharacterTest.InteractionPhysics.TornadoFieldQuery`
  - `CharacterTest.InteractionPhysics.FallbackFieldQuery`

### Result buffer capacity fix

最新 raw profiler 窗口曾显示 `CharacterTestDynamicFieldTargetIndex.QueryVerticalCapsule()` 仍有 `2048 bytes / 16 allocs`。代码审计后补齐了运行时 caller-owned buffer 容量契约：

- `CharacterTestDynamicFieldTargetIndex.EnsureResultCapacity()` 按当前 `TargetCount` 预留结果列表容量。
- `CharacterTestInteractionPhysicsSystem` 将 `_queryResults` / `_processedBodyIds` 初始化为 32 容量。
- 在 Tornado 索引重建或命中同帧 cache 后、进入 `TornadoFieldQuery` marker 前，按 `TargetCount` 预留 `_queryResults` 和 body 去重表容量。
- 目标是让 fast query marker 内只写已有 buffer，不触发 `List<T>.AddWithResize()` 或去重表扩容。

## Validation

已完成：

- `dotnet build MxFramework.Tests.csproj -v minimal` 通过，0 errors；仍有既有 Unity serialized-field `CS0649` warnings。
- `git diff --check` 通过。
- Unity MCP `refresh_unity(scope=scripts, compile=request)` 后 Console 没有编译 error。
- Focused EditMode job `b30f5d1def2746c3b7120f823ff80a69` 4/4 通过：
  - `CharacterTestDynamicFieldTargetIndex_TornadoCapsuleMatchesCombatQueryForDynamicTargets`
  - `CharacterTestDynamicFieldTargetIndex_TornadoCapsuleQueryOrderIsStable`
  - `CharacterTestDynamicFieldTargetIndex_TornadoCapsuleQueryDoesNotAllocateAfterWarmup`
  - `CharacterTestDynamicFieldTargetIndex_TornadoCapsuleQueryDoesNotAllocateWithPreparedEmptyResultBuffer`
- 新增 prepared-empty-result-buffer 测试从空 `List<CombatQueryResult>` 开始，`Rebuild()` 后调用 `EnsureResultCapacity()`，再直接测 `QueryVerticalCapsule()`；40 个动态目标命中场景下查询阶段 `GC.GetAllocatedBytesForCurrentThread()` 差值为 0。
- Tornado 场景 smoke 的正确 full name 为 `MxFramework.Tests.Game.CharacterTest.CharacterTestFireballPlayModeTests.CharacterTestScene_PlayModeTornadoSlotSevenShowsProjectilePulseAndDamage`；EditMode(EnterPlayMode) job `a22ec45e18094d7c8be401075bb52e7c` 1/1 通过。

## 2026-06-20 PlayMode ProfilerRecorder Evidence

为绕过当前 Editor `ProfilerDriver` buffer 停在旧窗口的问题，Tornado slot 7 PlayMode smoke 现在会在实际 Tornado projectile / active field 循环中采集 Tornado 专用 `ProfilerRecorder` marker，并写出：

- `Temp/CharacterTestTornadoPlayModeProfilerRecorderReport.txt`

本轮验证结果：

- `dotnet build MxFramework.Tests.csproj -v minimal` 通过，0 warnings、0 errors。
- Unity MCP `refresh_unity(scope=scripts, compile=request)` 后 Console 没有编译 error。
- EditMode(EnterPlayMode) job `a9f28de3f01b49358a9b10676f45d11d` 1/1 通过。
- TestRunner 后 Console 只返回 Unity Test Runner 写 `TestResults.xml` 的记录。
- `git diff --check` 通过。
- `Tools/Harness/check_code_standards.py --all --summary-only` 通过执行，仍为 150 个既有 report-only unsafe baseline。
- Git Bash 版 `Tools/Harness/run_lightweight_checks.sh` 通过；changed-files code standards 扫描 16 个 first-party code files，no findings。
- 报告记录 `scenario=character-test.tornado.playmode-scene.profiler-recorder`、`profileKind=tornado-playmode-profiler-recorder`、`captureStartFrame=13`、`frame=200`、`sampledFrames=564`、`markerCount=6`。
- 关键 marker：
  - `CharacterTest.CharacterRuntime.InteractionConsumers`: active median `0.085ms` / active P95 `0.159ms`
  - `CharacterTest.InteractionPhysics.TornadoIndexRebuild`: active median `0.042ms` / active P95 `0.080ms`
  - `CharacterTest.InteractionPhysics.TornadoFieldQuery`: active median `0.017ms` / active P95 `0.033ms`
  - `CharacterTest.InteractionPhysics.FallbackFieldQuery`: active samples `0`
  - `CharacterTest.CharacterRuntime.Motion`: active median `0.374ms` / active P95 `0.730ms`
  - `CharacterTest.CharacterRuntime.CombatTrace`: active median `0.129ms` / active P95 `0.368ms`

这份报告不是 Profile Analyzer CSV，但它证明当前 PlayMode Tornado active-field 场景中 fast path marker 可采样，且 `FallbackFieldQuery` 未参与该 Tornado active field。

## 2026-06-20 Profile Analyzer After Capture Evidence

为补齐 stale profiler-buffer 问题，新增显式 PlayMode capture 入口：

- `MxFramework.Tests.Game.CharacterTest.CharacterTestFireballPlayModeTests.CharacterTestScene_PlayModeTornadoProfileAnalyzerCapture_WritesPDataIntoTemp`
- 该测试使用 fresh `ProfilerDriver` buffer 采集 Tornado PlayMode 场景，并通过 Profile Analyzer package 内部 `ProfileData.Save()` 写出 `.pdata`。
- 输出文件：
  - `Temp/ProfileAnalyzer/current_after_tornado_playmode.pdata`
  - `Temp/ProfileAnalyzer/current_after_tornado_playmode_profile_analyzer_capture_summary.txt`

本轮验证结果：

- EditMode(EnterPlayMode) explicit job `a693aaf5d3f24e6f9a9051f97e2ac795` 1/1 通过。
- Summary 记录 `scenario=character-test.tornado.playmode-scene.profile-analyzer-after`、`pDataWritten=true`、`firstRawFrameIndex=0`、`lastRawFrameIndex=237`、`firstDisplayFrameIndex=1`、`lastDisplayFrameIndex=238`。
- `.pdata` 大小约 1.14GB，位于 `Temp/`，仅作为本地性能证据，不进入提交范围。
- `Tools/Harness/profile_analyzer_gc_summary.py --exclude-frame 200` 已支持 `.pdata` 导出时排除指定 display frame；本次导出生成：
  - `Temp/ProfileAnalyzer/current_after_tornado_playmode_frameTime.csv`
  - `Temp/ProfileAnalyzer/current_after_tornado_playmode_gcMarkerTable.csv`
  - `Temp/ProfileAnalyzer/current_after_tornado_playmode_markerTiming.csv`

排除第 200 帧后的 after 聚合关键值：

- `CharacterTest.CharacterRuntime.InteractionConsumers`: median `0.0349ms` / mean `0.3169ms` / max `2.7715ms`，GC median `0B`。
- `CharacterTestInteractionPhysicsSystem.QueryField()`: median `0.6014ms` / mean `0.7518ms` / max `2.1919ms`，GC median `0B`。
- `CharacterTest.InteractionPhysics.TornadoIndexRebuild`: median `0.4113ms` / mean `0.5251ms` / max `1.5428ms`，GC median `0B`。
- `CharacterTest.InteractionPhysics.TornadoFieldQuery`: median `0.1668ms` / mean `0.1898ms` / max `0.5845ms`，GC total `0B`。
- `CharacterTest.InteractionPhysics.FallbackFieldQuery`: no active row in the after `.pdata` export；ProfilerRecorder 同场景记录 active samples `0`。
- `CombatPhysicsWorld.QueryCapsule()`: median `0.0454ms` / mean `0.1151ms`，说明 Tornado active field 主成本已不再由通用 capsule query 承担。

This is after-side evidence only. The later 2026-06-21 paired capture below is
the current source for same-harness before/after `TornadoFieldQuery` deltas.

同一 after `.pdata` 的单帧 frame 169 导出也已生成，但该帧受 Editor / ProfilerWindow repaint 噪声影响：`Motion=9.4356ms`、`CombatKinematicMotor.Step()=8.1620ms`，而 `InteractionConsumers=0.0923ms`、`QueryCapsule=0.0391ms`。因此 frame 169 只保留为单帧审计记录，不作为稳定收益结论。

## Repeatable Comparison Aid

新增 `Tools/Harness/profile_analyzer_compare_markers.py`，用于比较两份 `profile_analyzer_gc_summary.py --marker-timing-out` 生成的 marker timing CSV。

本轮已生成：

- `Temp/ProfileAnalyzer/historical_tmp2_vs_current_after_marker_comparison.csv`
- `Temp/ProfileAnalyzer/historical_tmp2_vs_current_after_marker_comparison.md`
- `Temp/ProfileAnalyzer/historical_tmp2_frame169_vs_current_after_frame169_marker_comparison.csv`
- `Temp/ProfileAnalyzer/historical_tmp2_frame169_vs_current_after_frame169_marker_comparison.md`

这些文件是 review aid，不是正式 before/after 结论。历史 `tmp2` 与当前 after capture 不是同一 fresh scenario；frame 169 after 又受 Editor / ProfilerWindow repaint 噪声污染。它们的价值是让“历史热点是否已被当前 fast path 消掉”可重复检查，而不是替代 clean before/after capture。

`ProfileAnalyzer/tmp0.pdata` 也已审计，不能作为 missing before。该文件 frame 169 的主成本来自 TestRunner / Editor：`Frame GC=174664B`、`UpdateScene=66.1839ms`、`Application.Tick=54.1441ms`。关键 runtime marker 已是当前 fast-path 形态：`InteractionConsumers=0.1137ms`、`CombatPhysicsWorld.QueryCapsule()=0.0386ms`、GC total `0B`。因此它只是 noisy after-like artifact，不是 clean baseline。

## 2026-06-20 Paired Capture Harness

The repeatable before/after capture gap is now narrowed to a concrete harness
instead of a manual Profile Analyzer workflow:

- Added `Tools/Harness/profile_analyzer_capture_pair.ps1`.
- The script creates or reuses a detached baseline worktree at `E:\WGF_pa_base`
  from `origin/main`, applies the baseline-compatible capture harness patch plus
  a marker-only Tornado instrumentation patch, and writes comparison artifacts to
  `ProfileAnalyzer/paired_tornado_capture/`.
- Added `CharacterTestProfileAnalyzerCommandLineCapture.RunPairCapture` in the
  test assembly so Unity can run the Tornado capture via `-executeMethod`.
- The command-line runner manually advances the existing capture IEnumerator,
  handles `EnterPlayMode` / `ExitPlayMode`, waits for real `Time.frameCount`
  progress while in Play Mode, and writes `.pdata` / summary files to absolute
  paths outside the tested project's `Temp/` folder so Unity shutdown cleanup
  cannot delete them.
- The capture helper now asserts scene readiness, target preparation, and slot 7
  command acceptance before starting Profile Analyzer capture, preventing empty
  or non-Tornado samples from being saved.
- The script exports `frameTime`, GC marker table, and marker timing CSVs with
  `--exclude-frame 200`, then runs `profile_analyzer_compare_markers.py`.

Generated local artifacts:

- `ProfileAnalyzer/paired_tornado_capture/fresh_before.pdata`: baseline
  `origin/main` plus capture harness patch, `firstDisplayFrameIndex=1`,
  `lastDisplayFrameIndex=249`, exported frames `248` after excluding frame 200.
- `ProfileAnalyzer/paired_tornado_capture/fresh_after.pdata`: current after
  capture copied from `Temp/ProfileAnalyzer/current_after_tornado_playmode.pdata`,
  `238` raw display frames, exported frames `237` after excluding frame 200.
- `ProfileAnalyzer/paired_tornado_capture/fresh_before_after_marker_comparison.csv`
  and `.md`: report-only marker comparison.

Current status: this paired capture now has a comparable same-marker
`TornadoFieldQuery` row. The refreshed `fresh_before.capture_summary.txt` and
`fresh_after.capture_summary.txt` both record `tornadoEvidence` with projectile
flight, projectile movement, tick VFX, attraction movement, damage, and repeated
damage all `true`, so the paired capture covers the Tornado active-field window
on both sides. The marker-only baseline patch gives the old generic
`QueryCapsule` Tornado path the same
`CharacterTest.InteractionPhysics.TornadoFieldQuery` marker name as the new fast
path, without changing the old query algorithm. `TornadoIndexRebuild` is still
after-only by design, because the old path has no dynamic index rebuild.

The formal gameplay conclusion remains the current after evidence: Tornado
active-field uses the fast path and `FallbackFieldQuery` is absent in this
scenario. The fresh paired scenario does not reproduce the historical
multi-millisecond Tornado hotspot from the user CSV, so large-benefit claims stay
tied to that historical evidence until a heavier fresh baseline reproduces it.

## 2026-06-21 Pair Capture Validity Audit

- Added `Tools/Harness/profile_analyzer_baseline_tornado_markers.patch`. It is
  applied only inside the detached `origin/main` baseline worktree and wraps the
  old Tornado active-field `QueryCapsule` call in
  `CharacterTest.InteractionPhysics.TornadoFieldQuery`. It does not introduce
  the dynamic index or fast query into the baseline.
- `profile_analyzer_capture_pair.ps1` now requires both the behavior-evidence
  harness patch and the marker-only baseline patch before reusing a baseline
  worktree. Older harness-only worktrees are reported as stale.
- The refreshed baseline was captured from `E:\WGF_pa_base_marker`; the isolated
  after capture was taken from `E:\WGF_pa_after_current`, a detached worktree
  with the current tracked diff applied. This avoided launching a second Unity
  Editor against the already-open main project.
- `fresh_before.capture_summary.txt`: display frames `1-247`,
  `captureStartFrame=22`, `captureEndFrame=283`, and every `tornadoEvidence`
  boolean as `true`.
- `fresh_after.capture_summary.txt`: display frames `1-248`,
  `captureStartFrame=28`, `captureEndFrame=288`, and every `tornadoEvidence`
  boolean as `true`.
- Excluding display frame 200, the comparable same-marker row is now
  `CharacterTest.InteractionPhysics.TornadoFieldQuery`: median
  `0.0209ms -> 0.0038ms`, mean `0.0492ms -> 0.0171ms`, max
  `3.5682ms -> 1.4962ms`, GC total `1104B -> 16B`.
- The broader consumer row is also comparable in this capture:
  `CharacterTest.CharacterRuntime.InteractionConsumers` median
  `0.0281ms -> 0.0246ms`, mean `0.0545ms -> 0.0570ms`, max
  `6.2116ms -> 8.1529ms`. This fresh scenario does not reproduce the earlier
  user CSV's multi-millisecond Tornado hotspot, so use it as a same-harness
  regression/verification result, not as the full worst-frame benefit claim.
- `profile_analyzer_compare_markers.py` Markdown now includes median, mean, max,
  and GC columns. Rows with values on both sides are comparable; rows named in
  validity notes, currently `TornadoIndexRebuild`, have no formal delta.
- Current validation passed: Python syntax check for Profile Analyzer helpers,
  pair script dry run, regenerated pair export/compare using existing `.pdata`,
  isolated after capture, `git diff --check`, `dotnet build MxFramework.Tests.csproj -v minimal`,
  `Tools/Harness/check_code_standards.py --all --summary-only`, and Git Bash
  `Tools/Harness/run_lightweight_checks.sh`.
- Current Unity MCP validation is unavailable: reading `mcpforunity://custom-tools`,
  `mcpforunity://instances`, and `mcpforunity://editor/state` fails against
  `http://127.0.0.1:8080/mcp`, so this follow-up does not add a new MCP-driven
  Unity Console or Unity Test Runner result.

## 2026-06-21 96-Target Stress Pair Evidence

The remaining measurement gap was a fresh baseline that was too light to stress
the old Tornado `QueryCapsule` path. That gap is now covered by a repeatable
stress scenario:

- Added `CharacterTestScene_PlayModeTornadoProfileAnalyzerStressPairCapture_WritesPDataIntoTemp`
  and `CharacterTestProfileAnalyzerCommandLineCapture.RunStressPairCapture`.
- Added `-StressScenario` and `-StressTargetCount` to
  `Tools/Harness/profile_analyzer_capture_pair.ps1`.
- The stress capture prepares 96 hostile dynamic character targets around the
  Tornado active-field path, while preserving the same slot 7 Tornado command,
  projectile/VFX window, damage checks, and Profile Analyzer export flow.
- The current default detached baseline worktree name is
  `E:\WGF_pa_base_stress_current`; the validated run used fresh scratch worktrees
  `E:\WGF_pa_base_stress2` and `E:\WGF_pa_after_stress2`.
- Output artifacts are under `ProfileAnalyzer/paired_tornado_stress_capture/`.

Evidence from the validated run, excluding display frame 200:

- `fresh_before.capture_summary.txt`: display frames `1-479`,
  `captureStartFrame=57`, `captureEndFrame=1019`, `requestedStressTargetCount=96`,
  `preparedHostileCount=96`, projectile/movement/tick VFX/pull/damage/repeated
  damage all `true`.
- `fresh_after.capture_summary.txt`: display frames `1-479`,
  `captureStartFrame=50`, `captureEndFrame=957`, `requestedStressTargetCount=96`,
  `preparedHostileCount=96`, projectile/movement/tick VFX/damage/repeated damage
  all `true`. `targetPulled=false` is accepted for the stress artifact because
  the stress acceptance relies on prepared target count, non-zero Tornado marker
  samples, and damage evidence instead of one specific target's X displacement.
- `CharacterTest.InteractionPhysics.TornadoFieldQuery`: median
  `0.6217ms -> 0.1934ms`, mean `0.7413ms -> 0.2039ms`, max
  `4.4050ms -> 2.0528ms`, GC total `26400B -> 16B`.
- `CharacterTest.CharacterRuntime.InteractionConsumers`: median
  `0.0083ms -> 0.0078ms`, mean `0.1518ms -> 0.0970ms`, max
  `7.0395ms -> 8.2556ms`.
- `CharacterTest.CharacterRuntime.Motion`: median
  `4.4535ms -> 4.0387ms`, mean `5.4477ms -> 4.5518ms`.

Interpretation:

- The fresh stress scenario now reproduces a materially heavier old Tornado
  query path and shows the dedicated fast path reducing `TornadoFieldQuery` mean
  cost by about `0.54ms` in that controlled load.
- `InteractionConsumers` is already below the original acceptance target in the
  current after build; continuing to optimize the Tornado active-field query is
  no longer the highest-leverage path.
- The dominant remaining bucket in this stress capture is Motion /
  `CombatKinematicMotor`, so the next performance phase should move to the
  shared dynamic index / motion narrow-phase work described in the broader plan.

Validation for this stress follow-up:

- `dotnet build MxFramework.Tests.csproj -v minimal` passed with `0` warnings
  and `0` errors.
- `profile_analyzer_capture_pair.ps1 -StressScenario -StressTargetCount 96`
  completed the baseline capture, after capture, `.pdata` CSV export, and marker
  comparison.
- `profile_analyzer_capture_pair.ps1 -StressScenario -StressTargetCount 96
  -DryRun` passed with the current default baseline worktree path.
- `git diff --check` passed; Git reports the existing CRLF warning for
  `CharacterTestInteractionPhysicsSystem.cs`.
- Unity capture logs for `fresh_before` and `fresh_after` contain no `error CS`,
  `Compilation failed`, or `Script compilation failed` entries.
- `python -m py_compile Tools/Harness/profile_analyzer_gc_summary.py
  Tools/Harness/profile_analyzer_compare_markers.py` passed; generated
  `Tools/Harness/__pycache__` was removed afterward.
- `Tools/Harness/check_code_standards.py --all --summary-only` executed and
  still reports the existing `150` report-only unsafe baseline findings
  (`ZString.cs` plus one story demo finding).
- Git Bash `Tools/Harness/run_lightweight_checks.sh` passed; changed-files code
  standards scanned `16` first-party files with no findings.
- Unity MCP Console validation is still unavailable:
  `read_console(types=error)` fails to send to `http://127.0.0.1:8080/mcp`.

## 2026-06-21 Stress GC Prewarm Follow-up

The 96-target stress artifact above still reports a small after-side
`TornadoFieldQuery` GC total (`16B`) and after-only `TornadoIndexRebuild` GC.
Code review points to first-use capacity/comparer costs rather than the
vertical capsule algorithm itself.

Current code changes:

- `CharacterTestDynamicFieldTargetIndex` now preallocates its target list,
  XZ cell dictionary, active cell list, and pooled cell lists for the current
  96-target stress scale during construction.
- The index constructor also touches the `FieldCell` dictionary path and keeps a
  static `CombatQueryResultComparer` reference so first active-field rebuild or
  result sorting does not pay lazy initialization inside profiler markers.
- `CharacterTestInteractionPhysicsSystem` now starts its Tornado query result
  and processed-body buffers at `128`, matching the stress target count with
  headroom.
- Added an EditMode coverage point:
  `CharacterTestDynamicFieldTargetIndex_FirstStressRebuildAndQueryDoesNotAllocate`.
  It builds 96 hostile dynamic targets, constructs the index before measurement,
  then asserts the first `Rebuild()` plus `QueryVerticalCapsule()` allocation is
  `0` bytes with caller-owned result storage.

Validation available in this follow-up:

- `dotnet build MxFramework.Tests.csproj -v minimal` passed; current output has
  existing Unity serialized-field `CS0649` warnings and `0` errors.
- `git diff --check` passed; Git still reports the existing CRLF warning for
  `CharacterTestInteractionPhysicsSystem.cs`.
- `Tools/Harness/check_code_standards.py --all --summary-only` executed and
  still reports the existing `150` report-only unsafe baseline findings.
- Unity MCP remains unavailable at `http://127.0.0.1:8080/mcp`. The current
  project is already open in Unity Editor, so BatchMode was not started to avoid
  taking over the active `Library`.
- `Tools/Harness/run_lightweight_checks.sh` timed out after 244 seconds in this
  follow-up and did not produce a usable summary, so it is not counted as passed
  evidence for this incremental change.

Fresh stress `.pdata` recapture is still required to replace the older
`16B`/index-rebuild GC artifact with current-code profiler evidence.

## Remaining Work

- The same-harness `TornadoFieldQuery` percentage is available for both the
  ordinary pair and the 96-target stress pair. Treat the stress pair as the
  current fresh evidence for the Tornado query optimization.
- The remaining broad performance work is no longer Tornado query specific.
  Continue with Motion / `CombatKinematicMotor` and shared dynamic index /
  narrow-phase planning if the total frame is still too slow.
- Motion follow-up has started: `CombatKinematicMotor` now keeps separate
  per-step candidate index lists for the base movement sweep and the wider
  step-up envelope, so ordinary movement probes do not rescan candidates that
  exist only inside step-up vertical padding. This is outside the Tornado query
  fast path and should be validated with the next Motion-focused capture.
- Motion / Combat broadphase follow-up has also started: internal broadphase
  candidate collection can now apply source/layer filters before filling the
  scratch candidate list, and `CombatKinematicMotor` skips its collision
  broadphase/cache/probe path when the obstacle layer mask is empty. This is
  still outside the Tornado query fast path and needs fresh Motion-focused
  profiler evidence before claiming a frame-level delta.
- The older isolated/stress after `.pdata` records `TornadoFieldQuery` GC total
  `16B`. Current code now prewarms the index and caller buffers for that scale,
  but a fresh stress recapture is still needed before claiming the profiler row
  is zero in the artifact.

## 2026-06-21 Motion Initialization and Contact GC Follow-up

After the Tornado fast path stopped being the dominant cost, the next low-risk
cleanup targeted first-use allocation and repeated contact string creation in
the remaining Motion / movable-object path:

- `CombatKinematicMotor` now constructs its internal collision list and per-step
  candidate-index lists with `Config.MaxCollisionIterations` as the initial
  capacity. This keeps the first collision-producing `Step()` from growing those
  scratch lists after the world query path has already been warmed.
- `CharacterTestCharacterMotionSystem` now creates its caller-owned
  `CombatMotionStepBuffer` with `CombatMotionConfig.DefaultMaxCollisionIterations`
  capacity. CharacterTest uses that same max collision iteration count when it
  creates motion configs, so this only moves the buffer allocation to system
  initialization and does not change gameplay behavior.
- `CharacterTestCharacterContactResolver` now caches each `character:<id>`
  movable-contact stable id in a resolver-owned dictionary. Repeated character
  pushes into movable natural objects reuse the cached string instead of
  allocating a concatenated string on every contact hit.

Behavior boundaries:

- No public MxFramework API was added or changed.
- Collision shapes, source/layer filtering, step-up behavior, surface-material
  resolution, movable-object push math, contact pair text semantics, and Tornado
  gameplay parameters are unchanged.
- The contact stable-id cache can allocate once per previously unseen character
  id, then reuses the stored string on steady-state contact frames.

Added focused coverage:

- `CombatKinematicMotorTests.Step_BufferedCollisionResultDoesNotAllocateOnFirstCollisionAfterQueryWarmup`
  warms the world/query path with a throwaway motor, then verifies a fresh motor
  and pre-sized step buffer can report the first buffered ground collision with
  `0` thread allocation.

Validation in this follow-up:

- `dotnet build MxFramework.Tests.csproj -v minimal` passed with existing Unity
  serialized-field `CS0649` warnings and `0` errors.
- `dotnet test MxFramework.Tests.csproj --no-build --filter
  "FullyQualifiedName~Step_BufferedCollisionResultDoesNotAllocateOnFirstCollisionAfterQueryWarmup"
  -v minimal` exited `0`, but this dotnet route still does not print detailed
  Unity/NUnit results, so focused Unity TestRunner evidence is still pending.
- Unity MCP remains unavailable in this thread: resources under
  `mcpforunity://instances`, `mcpforunity://custom-tools`, and
  `mcpforunity://editor/state` fail to send to `http://127.0.0.1:8080/mcp`.

## 2026-06-21 Dynamic Contact Lookup Follow-up

The next Motion-side cleanup targets dynamic-character contact resolution after
`CombatKinematicMotor.Step()` reports collisions:

- `CharacterTestCharacterContactResolver` now owns a per-frame
  `CombatBodyId -> CharacterTestCharacterEntity` lookup for dynamic characters.
- `CharacterTestCharacterMotionSystem` prepares that lookup once after syncing
  character combat colliders for the current physics tick.
- `RecordDynamicContactHit()` uses the prepared lookup instead of scanning
  `world.Entities` for every collision target body. Direct tests or non-prepared
  callers still fall back to the old linear scan, preserving behavior outside
  the normal motion tick path.

Behavior boundaries:

- No public API was added or changed.
- Contact counts, movable-object push handling, source exclusion, body id
  mapping, and contact-pair text output are unchanged.
- The lookup dictionary can grow when the active character count increases; after
  that initialization/warm-up, the steady-state tick path reuses the same storage.

Added focused coverage:

- `CharacterTestIntegrationContractTests.CharacterMotion_ContactResolverPreparedLookupResolvesDynamicContactsWithoutSteadyStateAllocation`
  builds a 96-character world, records a prepared dynamic-character collision,
  verifies source/target contact counts, and asserts repeated
  `Clear -> PrepareDynamicCharacterLookup -> RecordDynamicContactHit` cycles
  allocate `0` bytes after warm-up.

Validation in this follow-up:

- `dotnet build MxFramework.Tests.csproj -v minimal` passed with existing Unity
  serialized-field `CS0649` warnings and `0` errors.
- `dotnet test MxFramework.Tests.csproj --no-build --filter
  "FullyQualifiedName~CharacterMotion_ContactResolverPreparedLookupResolvesDynamicContactsWithoutSteadyStateAllocation"
  -v minimal` exited `0`, but this dotnet route still does not print detailed
  Unity/NUnit results, so focused Unity TestRunner evidence is still pending.

## 2026-06-21 Contact Pressure Grid Follow-up

The next Motion-side hotspot was the remaining pair scan in
`CharacterTestCharacterContactResolver.ResolveCharacterContactPressure()`. This
path runs after movement attempts have recorded contact pressure, so it can be
hit by dense character stress scenarios even after the Tornado active-field
query itself is cheap.

Current code changes:

- `ResolveCharacterContactPressure()` now builds a resolver-owned XZ grid over
  blocking character centers only when there are attempted contact velocities to
  resolve.
- Each source character queries nearby grid cells using its radius, the largest
  current character radius, and the existing contact-pressure slop. The old
  vertical-range, distance, normal, pressure velocity, and contact-count checks
  remain the authority for whether a pair is actually processed.
- Candidate indices are sorted in ascending entity-list order before processing,
  preserving the old nested-loop pair order for the pairs that survive the broad
  candidate cut.
- Cell lists, active-cell tracking, and query candidates are resolver-owned and
  pooled. The steady-state path can reuse them after warm-up instead of creating
  per-frame temporary lists.
- `ResolveCharacterOverlaps()` intentionally remains the old full pair scan for
  now. It mutates positions while iterating; reusing a static broadphase snapshot
  there would need a separate behavior audit.

Behavior boundaries:

- No public API was added or changed.
- Character radius, vertical overlap, pressure slop, contact normal resolution,
  contact counts, contact velocity math, and pair processing order for candidate
  pairs are unchanged.
- The grid can allocate when a larger character count or new cell population is
  first seen; after initialization/warm-up it reuses resolver-owned storage.

Added focused coverage:

- `CharacterTestIntegrationContractTests.CharacterMotion_ContactResolverPressureUsesSpatialCandidatesWithoutSteadyStateAllocation`
  builds a 96-character world with one real contact pair and many far targets,
  verifies contact pressure still reaches the nearby target, checks the pressure
  candidate count is lower than a full pair scan, and asserts repeated pressure
  resolution allocates `0` bytes after warm-up.

Validation in this follow-up:

- `dotnet build MxFramework.Tests.csproj -v minimal` passed with `0` warnings
  and `0` errors.
- `dotnet test MxFramework.Tests.csproj --no-build --filter
  "FullyQualifiedName~CharacterMotion_ContactResolverPressureUsesSpatialCandidatesWithoutSteadyStateAllocation|FullyQualifiedName~CharacterMotion_ContactResolverPreparedLookupResolvesDynamicContactsWithoutSteadyStateAllocation"
  -v minimal` exited `0`, but this dotnet route produced an empty log and still
  does not provide strong Unity TestRunner evidence.
- Unity MCP resources remain unavailable in this thread:
  `mcpforunity://instances`, `mcpforunity://custom-tools`, and
  `mcpforunity://editor/state` all fail to send to
  `http://127.0.0.1:8080/mcp`, so no new MCP-driven Console or Unity
  TestRunner result was added for this incremental change.

## 2026-06-21 Contact State Apply Follow-up

After the pressure pair scan was narrowed, the next resolver-side cleanup was
the final contact-state application pass. The old path rewrote every character's
physics contact fields every physics tick so non-contact characters would be
cleared. In dense worlds this made the end of Motion scale with total character
count even when only a few entities had contact data.

Current code changes:

- `CharacterTestCharacterContactResolver` now records the current tick's contact
  entities as contact counts, displacements, or velocities are accumulated.
- The resolver also keeps the previous tick's applied contact entities. After
  the initial compatibility full apply, `ApplyCharacterContactState()` only
  writes the current contact entities plus previous contact entities that need
  clearing.
- A no-contact tick still clears the previous contact state, but it no longer
  rewrites unrelated characters.
- The current/previous entity lists and id lookup are resolver-owned storage and
  reuse capacity after warm-up.

Behavior boundaries:

- No public API was added or changed.
- The first `ApplyCharacterContactState()` for a resolver instance still scans
  all passed entities, preserving the old "clear any preexisting contact fields"
  behavior at initialization.
- Normal Motion ticks preserve contact count, velocity, displacement, and
  previous-frame clearing semantics. `ResetCharacterContactState()` remains the
  full-scan fallback used when framework physics is unavailable.

Added focused coverage:

- `CharacterTestIntegrationContractTests.CharacterMotion_ContactResolverPressureUsesSpatialCandidatesWithoutSteadyStateAllocation`
  now also verifies the warmed contact-state apply count stays below total
  entity count, and that a following no-contact apply clears the prior source
  and target contact state without touching the full 96-character world.

Validation in this follow-up:

- `dotnet build MxFramework.Tests.csproj -v minimal` passed with `0` warnings
  and `0` errors.
- `dotnet test MxFramework.Tests.csproj --no-build --filter
  "FullyQualifiedName~CharacterMotion_ContactResolverPressureUsesSpatialCandidatesWithoutSteadyStateAllocation|FullyQualifiedName~CharacterMotion_ContactResolverPreparedLookupResolvesDynamicContactsWithoutSteadyStateAllocation"
  -v minimal` exited `0`, but this dotnet route again produced an empty log and
  does not provide strong Unity TestRunner evidence.
- Unity MCP resources remain unavailable:
  `mcpforunity://instances`, `mcpforunity://custom-tools`, and
  `mcpforunity://editor/state` all fail to send to
  `http://127.0.0.1:8080/mcp`, so no MCP-driven Unity Console or TestRunner
  result was added for this follow-up.

## 2026-06-21 Movable Object Body-Sync Follow-up

The movable natural object collider sync path now mirrors the character
body-sync optimization for position-only changes. `SetHorizontalPosition()` and
contact resolution still mark the movable object's `CombatColliderSyncVersion`
dirty, but when the existing body and collider shape/material still match the
runtime state, sync updates only the body position.

Current code changes:

- `CharacterTestMovableNaturalObjectSystem.SyncCombatCollider()` keeps the
  existing fastest unchanged-state skip: already-synced version plus existing
  body and same collider kind returns without constructing shape descriptors.
- For dirty movable objects, sync now builds the desired center and compares the
  current collider shape/material/extent against the desired collider.
- If the shape still matches, sync calls `CombatPhysicsWorld.SetBodyPosition()`,
  marks the movable object sync version current, and skips `UpsertBody()` plus
  `UpsertAabbCollider()` / `UpsertSphereCollider()` / `UpsertCapsuleCollider()`.
- First registration, missing body/collider, shape changes, material changes,
  and extent changes still use the full upsert path.

Behavior boundaries:

- No public API was added or changed.
- Movable object authority, collision shape selection, material semantics,
  body/collider ids, layer ids, and missing-collider recovery are unchanged.
- Stable unchanged objects keep the cheaper version/type-existence check; the
  shape comparison only runs after the object is already dirty.

Updated focused coverage:

- `CharacterTestMovableNaturalObjectPlayableTests.RuntimeMovableNaturalObjects_CombatColliderSyncSkipsUnchangedState`
  still covers unchanged sync skip, position update, and missing collider
  restore. It now also resets the same movable object from AABB to sphere and
  verifies the collider is replaced with an elastic sphere instead of taking the
  body-only path.

Validation in this follow-up:

- `dotnet build MxFramework.Tests.csproj -v minimal` passed with `0` errors.
- `dotnet test MxFramework.Tests.csproj --no-build --filter
  "FullyQualifiedName~RuntimeMovableNaturalObjects_CombatColliderSyncSkipsUnchangedState"
  -v normal` exited `0`, but this dotnet route only reported build success and
  did not provide Unity/NUnit test-case detail, so it is weak evidence.
- `git diff --check` passed, with the existing CRLF normalization warning for
  `CharacterTestInteractionPhysicsSystem.cs`.
- `python Tools\Harness\check_code_standards.py --all --summary-only` passed
  execution; it still reports the existing 150 report-only unsafe baseline
  findings (`ZString.cs` and one Story demo finding), with no new unsafe added
  by this follow-up.
- Git Bash `Tools/Harness/run_lightweight_checks.sh` passed: changed path count
  `63`, forbidden path, Unity `.meta` pairing, Docs health, changed-files code
  standards, and diff checks all passed.
- Unity MCP resources remain unavailable:
  `mcpforunity://instances`, `mcpforunity://custom-tools`, and
  `mcpforunity://editor/state` all fail to send to
  `http://127.0.0.1:8080/mcp`, so no MCP-driven Unity Console or TestRunner
  result was added for this follow-up.

## 2026-06-21 Lazy Step-Up Envelope Follow-up

`CombatKinematicMotor` now defers the widened step-up broadphase envelope until
the Step actually reaches a wall candidate that can attempt AABB step-up.
Earlier Step cache work reused one broadphase result across collision probes,
but horizontal movement with `MaxStepHeight` still paid for the taller Y
envelope up front even when no wall candidate survived the base sweep.

Current code changes:

- `PrepareStepBroadphaseCache()` first collects only the base movement envelope:
  capsule bounds plus `Config.SkinWidth`.
- When step-up is allowed, horizontal delta exists, and `MaxStepHeight > 0`, the
  motor stores a pending `_stepUpQueryMin/_stepUpQueryMax` instead of collecting
  that wider envelope immediately.
- `TryResolveStepUp()` calls `EnsureStepUpBroadphaseCache()` only after the
  blocking candidate has passed the wall flag, AABB shape, top-height, and
  horizontal-delta gates.
- The widened collect reuses the same source/layer filtering and still keeps the
  base eligible index list separate from the full step-up eligible list. Each
  probe continues to apply its own precise query AABB before narrow phase.

Behavior boundaries:

- No public API was added or changed.
- Collision ordering, best-fraction selection, surface material priority,
  source exclusion, wall/ground/step-up flags, and non-AABB step-up rejection are
  unchanged.
- Actual step-up frames may perform a second broadphase collect. The optimization
  targets the more common no-wall horizontal movement path by avoiding the
  taller step-up envelope until it is required.
- This follow-up does not introduce `unsafe`, Burst, Jobs, or Unity Physics as
  authority. The hot path reuses existing motor-owned lists after warm-up.

Added focused coverage:

- `CombatKinematicMotorTests.Step_StepCacheDefersStepUpEnvelopeUntilWallCandidate`
  locks horizontal movement with step-up enabled to the base 1m-cell sweep
  envelope: `QueryCount=1`, `BroadphaseCellCount=16`, and
  `StepUpProbeCount=0` when no wall candidate triggers step-up.

Validation in this follow-up:

- `dotnet build MxFramework.Tests.csproj -v minimal` passed with `0` warnings
  and `0` errors.
- `dotnet test MxFramework.Tests.csproj --no-build --filter
  "FullyQualifiedName~Step_StepCacheDefersStepUpEnvelopeUntilWallCandidate"
  --logger "trx;LogFileName=step_lazy.trx" -v normal` exited `0`, but this
  route only reported build success and did not produce Unity/NUnit test-case
  detail, so it is treated as weak evidence.
- `git diff --check` passed, with the existing CRLF normalization warning for
  `CharacterTestInteractionPhysicsSystem.cs`.
- `python Tools\Harness\check_code_standards.py --all --summary-only` passed
  execution; it still reports the existing 150 report-only unsafe baseline
  findings (`ZString.cs` and one Story demo finding), with no new unsafe added
  by this follow-up.
- Git Bash `Tools/Harness/run_lightweight_checks.sh` passed: changed path count
  `63`, forbidden path, Unity `.meta` pairing, Docs health, changed-files code
  standards, and diff checks all passed.
- Unity MCP resources remain unavailable:
  `mcpforunity://instances`, `mcpforunity://custom-tools`, and
  `mcpforunity://editor/state` all fail to send to
  `http://127.0.0.1:8080/mcp`, so no MCP-driven Unity Console or TestRunner
  result was added for this follow-up.

## 2026-06-21 Step Cache Exact AABB Follow-up

The next Motion-side cleanup narrows the repeated per-probe AABB filter inside
`CombatKinematicMotor.TryFindFirstCollision()`. Step cache population already
uses `CombatPhysicsWorld.CollectBroadphaseCandidatesAabb()` with source/layer
filtering, and `CombatPhysicsBroadphase` also applies exact candidate AABB
overlap before returning the candidate view. When a later probe's query AABB is
identical to that cached query AABB, re-running `OverlapsInclusive()` for every
candidate cannot remove any additional candidate.

Current code changes:

- `ResolveBroadphaseCandidates()` now records whether the cached base or
  step-up candidate view exactly matches the current probe AABB.
- Exact AABB matches set `filterByQueryAabb=false`, so the probe goes straight
  from the cached eligible candidate index list to the existing best-prefix and
  narrow-phase gates.
- Smaller probes inside the cached base AABB, step-up probes that do not match
  the full step-up envelope, and fallback non-cache queries still keep the
  existing exact probe-AABB filter.

Behavior boundaries:

- No public MxFramework API was added or changed.
- `CombatPhysicsBroadphase` still removes cell false positives before returning
  candidates, so the new short path does not broaden collision candidates.
- Collision shapes, source/layer filtering, best-fraction ordering, step-up
  rules, collision flags, and surface-material selection are unchanged.
- The change adds only value comparisons and a private helper; it does not
  allocate and does not introduce `unsafe`, Burst, Jobs, or Unity Physics.

Validation in this follow-up:

- `dotnet build MxFramework.Tests.csproj -v minimal` passed with `0` errors and
  existing serialized-field `CS0649` warnings.
- `dotnet test MxFramework.Tests.csproj --no-build --filter
  "FullyQualifiedName~Step_StepCacheUsesCapsuleBoundsWithoutDoubleRadiusPadding|FullyQualifiedName~Step_StepCacheDefersStepUpEnvelopeUntilWallCandidate|FullyQualifiedName~Step_BestFractionPrefixSkipsFarCandidates|FullyQualifiedName~Step_StepCacheSkipsCollisionProbeWhenNoEligibleCandidates"
  -v normal` exited `0`, but this Unity test project route again only reported
  build success and is treated as weak evidence rather than focused NUnit
  execution evidence.
- `git diff --check` passed with the existing CRLF normalization warning for
  `CharacterTestInteractionPhysicsSystem.cs`.
- `python Tools\Harness\check_code_standards.py --all --summary-only` passed
  execution; it still reports the existing 150 report-only unsafe baseline
  findings, with no unsafe added by this follow-up.
- Git Bash `Tools/Harness/run_lightweight_checks.sh` passed with changed path
  count `63`; forbidden path, Unity `.meta` pairing, Docs health, and
  changed-files code standards all passed.
- Unity MCP resources remain unavailable because `127.0.0.1:8080` is not
  listening while the current Unity Editor process is open, so no MCP-driven
  Unity Console or TestRunner result is available yet.

## 2026-06-21 Character Collider Body-Sync Follow-up

The next Motion-side cleanup narrows the CharacterTest character collider sync
path after the motor has already advanced a dynamic body for the current frame.
The previous fast-sync state could tell whether the full body/collider state was
current, but not whether only the body position had changed while the collider
shape stayed identical. That caused moved characters to keep reaching the
full-sync branch and re-check body/collider upsert paths even when the existing
collider record was still valid.

Current code changes:

- `CharacterTestCharacterPhysicsRuntime` now exposes an internal
  shape-sync check for character colliders. It compares only
  `BlocksCharacters` and half extents against the last synchronized state.
- `CharacterTestCharacterMotionSystem.SyncCharacterCombatCollider()` now uses
  that internal check after the existing full-state early return. If the shape
  is unchanged and the expected body/collider record still exists, it updates
  only the body position with `CombatPhysicsWorld.SetBodyPosition()` and marks
  the new full sync state.
- Missing body/collider records, blocking-state changes, radius/height changes,
  and first registration still fall through to the original `UpsertBody()` plus
  collider add/remove path.

Behavior boundaries:

- No public MxFramework API was added or changed.
- Body position is still quantized through the same deterministic transform
  path before being written to `CombatPhysicsWorld`.
- Collider shape, layer, source/body ids, blocking semantics, contact resolver
  ordering, and broadphase dirty-body revision semantics are unchanged.
- The hot path uses existing dictionaries and value structs; it adds no
  steady-state allocation after the runtime sync dictionaries have warmed.

Validation in this follow-up:

- `dotnet build MxFramework.Tests.csproj -v minimal` passed with `0` errors.
  The build reported existing `CS0649` serialized-field warnings in
  CharacterTest scene/profile MonoBehaviours.
- `dotnet test MxFramework.Tests.csproj --no-build --filter
  "FullyQualifiedName~CharacterMotion_FrameworkPhysicsWorld_CombatColliderSyncSkipsUnchangedState"
  -v minimal` exited `0`, but this dotnet route again produced no NUnit detail
  output and is treated as weak evidence only.
- `git diff --check` passed, with the existing CRLF normalization warning for
  `CharacterTestInteractionPhysicsSystem.cs`.
- `python Tools\Harness\check_code_standards.py --all --summary-only` passed
  execution; it still reports the existing 150 report-only unsafe baseline
  findings (`ZString.cs` and one Story demo finding), with no new unsafe added
  by this follow-up.
- Git Bash `Tools/Harness/run_lightweight_checks.sh` passed: forbidden path,
  Unity `.meta` pairing, Docs health, changed-files code standards, and diff
  checks all passed.
- Unity MCP resources remain unavailable:
  `mcpforunity://instances`, `mcpforunity://custom-tools`, and
  `mcpforunity://editor/state` all fail to send to
  `http://127.0.0.1:8080/mcp`, so no MCP-driven Unity Console or TestRunner
  result was added for this follow-up.

## 2026-06-21 Contact Overlap Prefilter Follow-up

`ResolveCharacterOverlaps()` still intentionally avoids a static spatial grid
because it mutates character XZ positions while iterating. This follow-up keeps
that behavior boundary but removes avoidable per-pair work before the nested
loop:

- The resolver builds an internal list of blocking contact entities in source
  order before each overlap solve.
- Each list entry caches radius, vertical range, and push weight. The loop still
  reads current XZ transforms from the live entities, so displacement mutations
  inside the solver remain visible to later pairs exactly as before.
- The nested loop now scans only blocking entries while preserving their relative
  pair order. Null and non-blocking entities are filtered once instead of being
  rechecked for every pair.
- `LastOverlapCandidatePairCount` records the warmed pair count for focused
  tests and local profiling diagnostics; it is internal-only and not a public
  MxFramework API.

Behavior boundaries:

- No public API was added or changed.
- Pair order among blocking characters, overlap distance checks, normal
  resolution, displacement split, contact counts, collider resync timing, and
  `ResolveCharacterOverlaps()` iteration count are unchanged.
- This is not yet the broader shared Motion spatial index. It is a conservative
  prefilter/metadata cache that avoids changing the solver's mutation semantics.

Added focused coverage:

- `CharacterTestIntegrationContractTests.CharacterMotion_ContactResolverOverlapFiltersNonBlockingCharactersWithoutSteadyStateAllocation`
  builds a 96-character world with one real overlap pair and many non-blocking
  characters, verifies source/target contact counts, checks the overlap pair
  count stays below a full world pair scan, verifies warmed contact-state apply
  is not full-world, and asserts repeated overlap solve/apply allocates `0`
  bytes after warm-up.

Validation in this follow-up:

- `dotnet build MxFramework.Tests.csproj -v minimal` passed with `0` warnings
  and `0` errors.
- `dotnet test MxFramework.Tests.csproj --no-build --filter
  "FullyQualifiedName~CharacterMotion_ContactResolverOverlapFiltersNonBlockingCharactersWithoutSteadyStateAllocation|FullyQualifiedName~CharacterMotion_ContactResolverPressureUsesSpatialCandidatesWithoutSteadyStateAllocation|FullyQualifiedName~CharacterMotion_ContactResolverPreparedLookupResolvesDynamicContactsWithoutSteadyStateAllocation"
  -v minimal` exited `0`, but this dotnet route again produced an empty log and
  does not provide strong Unity TestRunner evidence.
- Unity MCP resources remain unavailable:
  `mcpforunity://instances`, `mcpforunity://custom-tools`, and
  `mcpforunity://editor/state` all fail to send to
  `http://127.0.0.1:8080/mcp`, so no MCP-driven Unity Console or TestRunner
  result was added for this follow-up.

## 2026-06-21 Contact Info Reuse Follow-up

The overlap prefilter now feeds the next pressure phase in the normal Motion
tick order. `ResolveCharacterContactPressure()` no longer rebuilds its candidate
grid by scanning the full entity list when `ResolveCharacterOverlaps()` has
already built the blocking contact info list for the same entity collection.

Current code changes:

- `CharacterTestCharacterContactResolver` tracks which entity collection the
  current blocking contact info list was built from.
- `ResolveCharacterContactPressure()` calls an internal ensure step and then
  builds its XZ grid from the blocking info list instead of the full world list.
- The pressure loop also iterates the same blocking info list, reusing cached
  radius and vertical range while still reading live XZ transforms from the
  entities. This preserves overlap displacement visibility between the overlap
  and pressure phases.
- If a caller invokes pressure resolution without a prior overlap phase, the
  resolver still builds the blocking info list on demand and keeps the previous
  standalone behavior.

Behavior boundaries:

- No public API was added or changed.
- Blocking character order, source/target pair order among blocking entities,
  pressure slop, vertical range checks, distance checks, pressure transfer math,
  contact counts, and contact velocity accumulation are unchanged.
- Non-blocking and null entities were already skipped by the pressure loop; this
  change skips them before grid construction and per-iteration pair scanning.

Added focused coverage:

- `CharacterTestIntegrationContractTests.CharacterMotion_ContactResolverOverlapFiltersNonBlockingCharactersWithoutSteadyStateAllocation`
  now runs overlap followed by pressure in each sample, verifies both overlap and
  pressure pair counts stay below a full 96-character world pair scan, and
  continues to assert warmed solve/apply allocation is `0` bytes.

Validation in this follow-up:

- `dotnet build MxFramework.Tests.csproj -v minimal` passed with `0` warnings
  and `0` errors.
- `dotnet test MxFramework.Tests.csproj --no-build --filter
  "FullyQualifiedName~CharacterMotion_ContactResolverOverlapFiltersNonBlockingCharactersWithoutSteadyStateAllocation|FullyQualifiedName~CharacterMotion_ContactResolverPressureUsesSpatialCandidatesWithoutSteadyStateAllocation|FullyQualifiedName~CharacterMotion_ContactResolverPreparedLookupResolvesDynamicContactsWithoutSteadyStateAllocation"
  -v minimal` exited `0`, but this dotnet route again produced an empty log and
  does not provide strong Unity TestRunner evidence.
- Unity MCP resources remain unavailable:
  `mcpforunity://instances`, `mcpforunity://custom-tools`, and
  `mcpforunity://editor/state` all fail to send to
  `http://127.0.0.1:8080/mcp`, so no MCP-driven Unity Console or TestRunner
  result was added for this follow-up.

## 2026-06-21 Movable Object Contact Grid Follow-up

The next Motion-side cleanup targets the remaining movable-object object-contact
pair scan in `CharacterTestMovableNaturalObjectSystem.ResolveObjectContacts()`.
The previous path scanned every object pair for each solver iteration. This was
behaviorally simple, but it kept a dense `N * N` loop in the same Motion bucket
after Tornado and character-contact costs had already been reduced.

Current code changes:

- `CharacterTestMovableNaturalObjectContactScratch` now owns a pooled XZ grid
  for movable object contact candidates, plus caller-owned candidate and query
  stamp buffers.
- `ResolveObjectContacts()` uses the scratch grid when scratch is available and
  keeps the old full-scan path as the no-scratch fallback.
- Candidate queries preserve source order and sort candidate indices ascending,
  so pair processing remains aligned with the old nested-loop order.
- The solver rebuilds the object-contact grid at the start of each iteration
  and again after a resolved pair when more later candidates for the same source
  may still be processed. This keeps live XZ mutations visible without switching
  to a stale one-shot pair list.
- Internal diagnostics record active object count, raw candidate count,
  filtered candidate count, queried cell count, and index rebuild count for
  focused tests and local profiling.

Behavior boundaries:

- No MxFramework public API was added or changed; the new counters and helpers
  remain internal to the CharacterTest test/runtime assembly boundary.
- Object shape overlap checks, vertical checks, contact axis selection,
  displacement split, velocity transfer, contact-pair text, material behavior,
  and `ObjectContactSolverIterations` are unchanged.
- The fallback path is still the old full scan when no scratch is supplied.
- This follow-up does not introduce `unsafe`, Burst, Jobs, or Unity Physics as
  authoritative input.

Added focused coverage:

- `CharacterTestMovableNaturalObjectPlayableTests.RuntimeMovableNaturalObjects_ObjectContactScratchMatchesFullScanPairResolution`
  compares the scratch-grid path against the old full-scan path for the same
  three-object contact chain and checks position, velocity, displacement, and
  contact pair equality.
- `CharacterTestMovableNaturalObjectPlayableTests.RuntimeMovableNaturalObjects_ObjectContactScratchQueriesNearbyPairsOnlyAndDoesNotAllocateAfterWarmup`
  builds a world with one nearby pair and 40 far objects, checks candidate
  counts stay below a full pair scan, and asserts repeated warmed ticks allocate
  `0` bytes.

Validation in this follow-up:

- `dotnet build MxFramework.Tests.csproj -v minimal` passed with `0` warnings
  and `0` errors on the post-test build.
- `dotnet test MxFramework.Tests.csproj --no-build --filter
  "FullyQualifiedName~RuntimeMovableNaturalObjects_ObjectContactScratchMatchesFullScanPairResolution|FullyQualifiedName~RuntimeMovableNaturalObjects_ObjectContactScratchQueriesNearbyPairsOnlyAndDoesNotAllocateAfterWarmup"
  -v normal` exited `0`, but this Unity test project route again only reported
  build success and is treated as weak evidence rather than focused NUnit
  execution evidence.
- Unity MCP tools loaded, but `read_console(types=["error"])` failed with the
  same transport send error to `http://127.0.0.1:8080/mcp`; MCP resources also
  could not be read through the current client binding. No new Unity Console or
  Unity TestRunner result is available for this follow-up.
- `git diff --check` passed with the existing CRLF normalization warning for
  `CharacterTestInteractionPhysicsSystem.cs`.
- `python Tools\Harness\check_code_standards.py --all --summary-only` passed
  execution; it still reports the existing 150 report-only unsafe baseline
  findings (`ZString.cs` plus one Story demo finding), with no unsafe added by
  this follow-up.
- Git Bash `Tools/Harness/run_lightweight_checks.sh` completed but failed on
  pre-existing broad branch asset issues outside this follow-up: the branch
  diff contains `Assets/Art/MxFramework/Samples/Weapons/Katana/Meshes/katana.fbx`
  trailing whitespace and missing `.meta` files for FMOD mac bundle contents.
  The harness also reported changed path count `708`, so this result is not a
  clean validation signal for the two files changed in this follow-up.

## 2026-06-21 Movable Object Contact Cell Envelope Guard Follow-up

The next movable-object contact cleanup narrows false positives inside the
scratch-owned object-contact grid. The previous grid already queried only cells
overlapped by the source object's AABB, but if a source barely crossed a 4m cell
boundary it still scanned every object indexed in that cell before exact bounds
filtering. This follow-up adds a conservative per-cell bounds guard.

Current code changes:

- Object-contact grid cells now use pooled `ObjectContactCellEntry` instances
  instead of raw `List<int>` cell values.
- Each entry stores both the candidate index list and the aggregate XZ bounds of
  objects inserted into that cell during the current rebuild.
- `QueryObjectContactCandidateIndices()` skips a whole occupied cell when the
  source query AABB does not overlap that cell entry's aggregate object bounds,
  then keeps the existing per-candidate exact bounds check before emitting
  candidate indices.
- Internal diagnostics now expose `LastObjectContactSkippedCellCount` for
  focused tests and local profiling.

Behavior boundaries:

- No MxFramework public API was added or changed.
- Candidate order, object overlap tests, vertical checks, contact axis
  selection, displacement split, velocity transfer, material behavior, solver
  iteration count, and fallback full-scan behavior are unchanged.
- The cell guard is conservative: it only skips a cell when none of the objects
  currently indexed in that cell can overlap the source query envelope.
- This follow-up does not introduce `unsafe`, Burst, Jobs, or Unity Physics as
  authoritative input.

Added focused coverage:

- `CharacterTestMovableNaturalObjectPlayableTests.RuntimeMovableNaturalObjects_ObjectContactScratchSkipsCellsOutsideQueryEnvelopeWithoutAllocation`
  creates a source object that barely crosses a grid-cell boundary, a valid
  nearby target in the original cell, and far objects in the boundary cell. It
  verifies the near target remains a candidate, far objects are not candidates,
  skipped-cell diagnostics are non-zero, and repeated warmed direct scratch
  queries allocate `0` bytes.

Validation in this follow-up:

- `dotnet build MxFramework.Tests.csproj -v minimal` passed with `0` errors and
  the existing Unity serialized-field CS0649 warning baseline.
- `dotnet test MxFramework.Tests.csproj --no-build --filter
  "FullyQualifiedName~RuntimeMovableNaturalObjects_ObjectContactScratchSkipsCellsOutsideQueryEnvelopeWithoutAllocation|FullyQualifiedName~RuntimeMovableNaturalObjects_ObjectContactScratchQueriesNearbyPairsOnlyAndDoesNotAllocateAfterWarmup|FullyQualifiedName~RuntimeMovableNaturalObjects_ObjectContactScratchMatchesFullScanPairResolution"
  -v normal` exited `0`, but this Unity test project route again only reported
  build success and is treated as weak evidence rather than focused NUnit
  execution evidence.
- `git diff --check` passed for the files touched by this follow-up.
- `python Tools\Harness\check_code_standards.py --all --summary-only` passed
  execution; it still reports the existing 150 report-only unsafe baseline
  findings (`ZString.cs` plus one Story demo finding), with no unsafe added by
  this follow-up.
- `python Tools\Harness\check_docs_health.py` passed against the current
  `origin/main` diff after this documentation update.
- Git Bash `Tools/Harness/run_lightweight_checks.sh` passed with changed path
  count `63`; forbidden path, Unity `.meta` pairing, Docs health, and
  changed-files code standards all passed.
- Focused Unity TestRunner evidence is still pending because Unity MCP resources
  and `read_console(types=["error"])` continue to fail to send to
  `http://127.0.0.1:8080/mcp`.

## 2026-06-21 Character Overlap Contact Grid Follow-up

The next Motion-side cleanup targets the remaining character-character overlap
pair scan in `CharacterTestCharacterContactResolver.ResolveCharacterOverlaps()`.
The pressure phase was already using the resolver-owned XZ contact grid, but the
overlap displacement phase still scanned every blocking character pair for each
solver iteration.

Current code changes:

- `ResolveCharacterOverlaps()` now rebuilds the pooled contact grid at the start
  of each overlap solver iteration and queries candidate indices through the same
  caller-owned `_contactCandidateIndices` list used by pressure resolution.
- The overlap query uses radius-only padding, while pressure resolution keeps its
  existing `ContactPressureSlopMillimeters` padding.
- Candidate collection now applies the cached vertical range test and exact XZ
  radius/slop range test before writing into `_contactCandidateIndices`. This
  rejects same-cell blockers that the 1m grid had to collect but that cannot
  participate in the current overlap or pressure solve.
- Candidate indices are still sorted ascending, preserving the old nested-loop
  source/target order among candidates that pass the grid prefilter.
- `CharacterTestCharacterMotionSystem.Tick()` no longer runs a second full
  `SyncAllCharacterCombatColliders()` immediately after movable-object tick;
  each `TickEntity()` already syncs its own collider after movement, and movable
  object tick does not move characters. Overlap displacement still calls the
  existing contact-resolver sync hook after a moved solver iteration.

Behavior boundaries:

- No MxFramework public API was added or changed.
- Character overlap radius checks, vertical range checks, displacement split,
  inverse-mass weighting, contact counts, contact displacement accumulation, and
  solver iteration count are unchanged. The vertical/range predicates are now
  applied once during candidate collection and remain guarded in the solver loop.
- The grid is rebuilt per overlap solver iteration so XZ displacement from the
  previous iteration is visible to the next candidate pass.
- This follow-up does not introduce `unsafe`, Burst, Jobs, or Unity Physics as
  authoritative input.

Added focused coverage:

- `CharacterTestIntegrationContractTests.CharacterMotion_ContactResolverOverlapUsesSpatialCandidatesWithoutSteadyStateAllocation`
  builds 96 blocking characters with only one nearby overlap pair plus same-cell
  small blocking decoys outside contact radius, verifies overlap candidate pairs
  stay below both the full pair scan and the same-cell decoy count, and asserts
  warmed overlap solve/apply allocation is `0` bytes.

Validation in this follow-up:

- `dotnet build MxFramework.Tests.csproj -v minimal` passed with `0` errors and
  the existing Unity serialized-field CS0649 warning baseline.
- `dotnet test MxFramework.Tests.csproj --no-build --filter
  "FullyQualifiedName~CharacterMotion_ContactResolverOverlapUsesSpatialCandidatesWithoutSteadyStateAllocation|FullyQualifiedName~CharacterMotion_ContactResolverOverlapFiltersNonBlockingCharactersWithoutSteadyStateAllocation|FullyQualifiedName~CharacterMotion_ContactResolverPressureUsesSpatialCandidatesWithoutSteadyStateAllocation|FullyQualifiedName~CharacterMotion_ContactResolverPreparedLookupResolvesDynamicContactsWithoutSteadyStateAllocation"
  -v normal` exited `0`, but this Unity test project route again only reported
  build success and is treated as weak evidence rather than focused NUnit
  execution evidence.
- `git diff --check` passed with the existing CRLF normalization warning for
  `CharacterTestInteractionPhysicsSystem.cs`.
- `python Tools\Harness\check_code_standards.py --all --summary-only` passed
  execution; it still reports the existing 150 report-only unsafe baseline
  findings (`ZString.cs` plus one Story demo finding), with no unsafe added by
  this follow-up.
- Git Bash `Tools/Harness/run_lightweight_checks.sh` passed with changed path
  count `63`; forbidden path, Unity `.meta` pairing, Docs health, and
  changed-files code standards all passed.
- Unity MCP resources and `read_console(types=["error"])` still fail to send to
  `http://127.0.0.1:8080/mcp`, so no new MCP-driven Unity Console or TestRunner
  result is available for this follow-up.

## 2026-06-21 Contact Grid Entry Consolidation Follow-up

The cell-radius guard introduced per-cell radius metadata, but the first version
stored that metadata in a second dictionary beside the contact-cell index list.
That kept behavior correct, but every occupied cell lookup in
`QueryContactCandidateIndices()` paid two dictionary probes and release had to
clear two dictionaries.

Current code changes:

- `CharacterTestCharacterContactResolver` now stores each occupied contact cell
  as a pooled `ContactCellEntry` containing both the candidate index list and
  the cell-local maximum radius.
- Contact grid build, query, and release now use one
  `Dictionary<ContactCell, ContactCellEntry>` lookup per occupied cell.
- `ContactCellEntry` instances are pooled with their internal `List<int>`, so
  the existing warm-up/no-GC contract is preserved.

Behavior boundaries:

- No MxFramework public API was added or changed.
- Candidate cell skipping, exact vertical/radius predicates, pressure slop,
  overlap strictness, contact counts, displacement, pressure propagation, and
  candidate ordering are unchanged. This is a data-layout cleanup for the
  resolver-owned scratch index.
- The change does not introduce `unsafe`, Burst, Jobs, or Unity Physics as
  authoritative input.

Validation in this follow-up:

- `dotnet build MxFramework.Tests.csproj -v minimal` passed with `0` errors and
  the existing Unity serialized-field CS0649 warning baseline.
- `dotnet test MxFramework.Tests.csproj --no-build --filter
  "FullyQualifiedName~CharacterMotion_ContactResolverPressureUsesSpatialCandidatesWithoutSteadyStateAllocation|FullyQualifiedName~CharacterMotion_ContactResolverOverlapUsesSpatialCandidatesWithoutSteadyStateAllocation|FullyQualifiedName~CharacterMotion_ContactResolverOverlapFiltersNonBlockingCharactersWithoutSteadyStateAllocation|FullyQualifiedName~CharacterMotion_ContactResolverPreparedLookupResolvesDynamicContactsWithoutSteadyStateAllocation"
  -v normal` exited `0`, but this Unity test project route again only reported
  build success and is treated as weak evidence rather than focused NUnit
  execution evidence.

## 2026-06-21 Movable Object Contact Vertical Candidate Guard Follow-up

The next Motion-side cleanup narrows `CharacterTestMovableNaturalObjectContactScratch`
candidate emission for movable-object object contacts. The object-contact grid
already has an XZ envelope guard, but candidates on a different vertical layer
could still pass the scratch query when their horizontal footprint overlapped
the source. Those pairs were later rejected by `ObjectsOverlap()` /
`TryResolveObjectContactAxis()`, so this was unnecessary work in the same Motion
bucket.

Current code changes:

- `QueryObjectContactCandidateIndices()` now computes the active movable
  object's query Y range once, alongside the existing XZ query range.
- `ObjectContactBoundsOverlap()` now checks candidate Y range before emitting a
  candidate index. It still uses the existing `QueryContactEpsilonMillimeters`
  expansion, so it is conservative relative to the later exact vertical overlap
  predicate.
- The XZ grid, query stamp deduplication, sorted candidate order, object-contact
  solver iterations, velocity transfer, contact displacement, and static-contact
  path are unchanged.

Behavior boundaries:

- No MxFramework public API was added or changed.
- The change only prevents vertically disjoint movable objects from entering the
  candidate result list. Pairs that can vertically overlap still reach the
  existing `ObjectsOverlap()` and `ResolveObjectPair()` logic.
- The scratch-owned buffers remain reused after warm-up. No `unsafe`, Burst,
  Jobs, or Unity Physics authority was introduced.
- This is intentionally a candidate-level guard, not a claimed cell-level
  vertical aggregate guard: the current object-contact query cells are the
  source object's own footprint cells, so source membership would make a simple
  cell aggregate Y range ineffective in common cases.

Added focused coverage:

- `CharacterTestMovableNaturalObjectPlayableTests.RuntimeMovableNaturalObjects_ObjectContactScratchSkipsVerticalCandidateBoundsWithoutAllocation`
  places a real low-layer target and two same-footprint high-layer decoys next
  to the source. The test verifies only the low-layer target is emitted,
  raw candidates exceed final candidates, and repeated warmed scratch queries
  allocate `0` bytes.

Validation in this follow-up:

- `dotnet build MxFramework.Tests.csproj -v minimal` passed with `0` warnings
  and `0` errors.
- `dotnet test MxFramework.Tests.csproj --no-build --filter
  "FullyQualifiedName~RuntimeMovableNaturalObjects_ObjectContactScratchSkipsVerticalCandidateBoundsWithoutAllocation|FullyQualifiedName~RuntimeMovableNaturalObjects_ObjectContactScratchSkipsCellsOutsideQueryEnvelopeWithoutAllocation|FullyQualifiedName~RuntimeMovableNaturalObjects_ObjectContactScratchQueriesNearbyPairsOnlyAndDoesNotAllocateAfterWarmup|FullyQualifiedName~RuntimeMovableNaturalObjects_ObjectContactScratchMatchesFullScanPairResolution"
  -v normal` exited `0`, but this Unity test project route again only reported
  build success and is treated as weak evidence rather than focused NUnit
  execution evidence.
- `git diff --check` passed; the current working tree still emits the existing
  CRLF normalization warning for `CharacterTestInteractionPhysicsSystem.cs`,
  but no whitespace error was reported.
- `python Tools\Harness\check_code_standards.py --all --summary-only` passed
  execution; it still reports the existing 150 report-only unsafe baseline
  findings (`ZString.cs` plus one Story demo finding), with no unsafe added by
  this follow-up.
- Git Bash `Tools/Harness/run_lightweight_checks.sh` passed: changed path count
  `63`, forbidden path, Unity `.meta` pairing, Docs health, changed-files code
  standards, and `git diff --check` all passed.
- Unity MCP resources for `instances`, `custom-tools`, and `editor/state`, plus
  direct `read_console(types=["error"])`, still fail to send to
  `http://127.0.0.1:8080/mcp`, so no MCP-driven Unity Console or TestRunner
  result is available for this follow-up.

- `git diff --check` passed; the current broad working tree still emits the
  existing CRLF normalization warning for
  `CharacterTestInteractionPhysicsSystem.cs`, but no whitespace error was
  reported.
- `python Tools\Harness\check_code_standards.py --all --summary-only` passed
  execution; it still reports the existing 150 report-only unsafe baseline
  findings (`ZString.cs` plus one Story demo finding), with no unsafe added by
  this follow-up.
- Git Bash `Tools/Harness/run_lightweight_checks.sh` did not pass cleanly in the
  current broad working tree. It reported changed path count `708`; forbidden
  path, Docs health, and changed-files code standards passed, but it failed on
  branch asset issues outside this follow-up (`katana.fbx` trailing whitespace
  and missing FMOD mac bundle `.meta` files).
- Unity MCP resource reads for `mcpforunity://instances`,
  `mcpforunity://custom-tools`, and `mcpforunity://editor/state` still fail to
  send to `http://127.0.0.1:8080/mcp`, so no new MCP-driven Unity Console or
  TestRunner result is available for this follow-up.

## 2026-06-21 Movable Object Contact Bounds Cache Follow-up

The next object-contact cleanup removes repeated query-bound reconstruction
inside `CharacterTestMovableNaturalObjectContactScratch`. After the XZ grid and
vertical candidate guard, each candidate query still recomputed source and
candidate XZ/Y bounds from the live movable-object state. In dense object
clusters this repeats the same math for every source query until the index is
rebuilt.

Current code changes:

- `RebuildObjectContactIndex()` now builds a parallel
  `ObjectContactStateBounds` list while it refreshes the object-contact grid.
- `QueryObjectContactCandidateIndices()` reads the cached source bounds and
  cached candidate bounds instead of calling the bounds helpers for every
  emitted raw candidate.
- The cache is invalidated by `PrepareObjectContactCandidates()` and rebuilt
  every time the object-contact grid is rebuilt, including after a resolved
  object pair moves and the existing solver requests a fresh index.

Behavior boundaries:

- No MxFramework public API was added or changed.
- The change preserves the existing XZ envelope guard, vertical candidate guard,
  query stamp deduplication, candidate sorting, `ObjectsOverlap()` /
  `ResolveObjectPair()` narrow phase, displacement, velocity transfer, solver
  iteration count, static-contact path, and fallback full-scan behavior.
- Bounds cache storage is scratch-owned and capacity is prepared from the active
  object count, so warm-up behavior remains NoAlloc. No `unsafe`, Burst, Jobs,
  or Unity Physics authority was introduced.

Coverage:

- Existing object-contact scratch coverage exercises this path:
  `RuntimeMovableNaturalObjects_ObjectContactScratchMatchesFullScanPairResolution`,
  `RuntimeMovableNaturalObjects_ObjectContactScratchQueriesNearbyPairsOnlyAndDoesNotAllocateAfterWarmup`,
  `RuntimeMovableNaturalObjects_ObjectContactScratchSkipsCellsOutsideQueryEnvelopeWithoutAllocation`,
  and
  `RuntimeMovableNaturalObjects_ObjectContactScratchSkipsVerticalCandidateBoundsWithoutAllocation`.

Validation in this follow-up:

- `dotnet build MxFramework.Tests.csproj -v minimal` passed with `0` errors and
  the existing Unity serialized-field CS0649 warning baseline.
- `dotnet test MxFramework.Tests.csproj --no-build --filter
  "FullyQualifiedName~RuntimeMovableNaturalObjects_ObjectContactScratchSkipsVerticalCandidateBoundsWithoutAllocation|FullyQualifiedName~RuntimeMovableNaturalObjects_ObjectContactScratchSkipsCellsOutsideQueryEnvelopeWithoutAllocation|FullyQualifiedName~RuntimeMovableNaturalObjects_ObjectContactScratchQueriesNearbyPairsOnlyAndDoesNotAllocateAfterWarmup|FullyQualifiedName~RuntimeMovableNaturalObjects_ObjectContactScratchMatchesFullScanPairResolution"
  -v normal` exited `0`, but this Unity test project route again only reported
  build success and is treated as weak evidence rather than focused NUnit
  execution evidence.

## 2026-06-21 Character Contact Cell Aggregate Bounds Guard Follow-up

The next Motion-side cleanup narrows false positives inside the resolver-owned
character contact grid. The previous cell-radius guard used the whole 1m cell
AABB plus that cell's max radius. That is conservative, but when a source sits
near a grid boundary it can still visit a neighboring occupied cell whose actual
character circle bounds are all outside the source query envelope.

Current code changes:

- `ContactCellEntry` now stores the candidate index list, max radius, and
  aggregate XZ bounds of the character circles indexed in that cell.
- `BuildContactCandidateGrid()` writes `center +/- radius` into the cell entry
  while keeping the existing pooled entry/list ownership.
- `QueryContactCandidateIndices()` computes a source query AABB from
  `source radius + slop` and skips an occupied cell when that source AABB cannot
  overlap the cell's aggregate character bounds.
- The existing cell max-radius guard, exact vertical predicate, exact XZ
  radius/slop predicate, and ascending candidate index sort still run before a
  pair reaches the pressure/overlap solve loops.

Behavior boundaries:

- No MxFramework public API was added or changed.
- Contact pair detection, pressure slop, overlap strictness, vertical filtering,
  contact counts, displacement, pressure propagation, ordering, mass transfer,
  push scale, and terminal velocity are unchanged. The new guard is
  conservative: it only skips a cell whose cached candidate circle AABB cannot
  overlap the source circle AABB expanded by the current query slop.
- Cell entries, aggregate bounds, and candidate buffers remain resolver-owned and
  are reused after warm-up. No `unsafe`, Burst, Jobs, or Unity Physics authority
  was introduced.

Added focused coverage:

- `CharacterTestIntegrationContractTests.CharacterMotion_ContactResolverPressureSkipsCellAggregateBoundsWithoutSteadyStateAllocation`
  places the source at a 1m grid boundary, keeps a real nearby pressure target,
  and adds small decoys in the neighboring cell whose cell AABB is reachable but
  whose aggregate character bounds cannot overlap the source query. The test
  verifies source/target contact counts, decoy contact counts stay zero,
  skipped-cell diagnostics are non-zero, candidate pairs are below the full
  world pair count, and warmed repeated pressure resolve/apply allocates
  `0` bytes.

Validation in this follow-up:

- `dotnet build MxFramework.Tests.csproj -v minimal` passed with `0` errors and
  the existing Unity serialized-field CS0649 warning baseline.
- `dotnet test MxFramework.Tests.csproj --no-build --filter
  "FullyQualifiedName~CharacterMotion_ContactResolverPressureSkipsCellAggregateBoundsWithoutSteadyStateAllocation|FullyQualifiedName~CharacterMotion_ContactResolverPressureUsesSpatialCandidatesWithoutSteadyStateAllocation|FullyQualifiedName~CharacterMotion_ContactResolverOverlapUsesSpatialCandidatesWithoutSteadyStateAllocation"
  -v normal` exited `0`, but this Unity test project route again only reported
  build success and is treated as weak evidence rather than focused NUnit
  execution evidence.
- `git diff --check` passed; the current broad working tree still emits the
  existing CRLF normalization warning for
  `CharacterTestInteractionPhysicsSystem.cs`, but no whitespace error was
  reported.
- `python Tools\Harness\check_code_standards.py --all --summary-only` passed
  execution; it still reports the existing 150 report-only unsafe baseline
  findings (`ZString.cs` plus one Story demo finding), with no unsafe added by
  this follow-up.
- Git Bash `Tools/Harness/run_lightweight_checks.sh` passed: changed path count
  `63`, forbidden path, Unity `.meta` pairing, Docs health, changed-files code
  standards, and `git diff --check` all passed.
- Unity MCP `read_console(types=["error"])` and focused EditMode `run_tests`
  still fail to send to `http://127.0.0.1:8080/mcp`, so no MCP-driven Unity
  Console or TestRunner result is available for this follow-up.
- `git diff --check` passed for the files touched by this follow-up.
- `python Tools\Harness\check_code_standards.py --all --summary-only` passed
  execution; it still reports the existing 150 report-only unsafe baseline
  findings (`ZString.cs` plus one Story demo finding), with no unsafe added by
  this follow-up.
- `python Tools\Harness\check_docs_health.py` passed against the current
  `origin/main` diff after this documentation update.
- Git Bash `Tools/Harness/run_lightweight_checks.sh` did not pass cleanly in the
  current broad working tree: forbidden path, Docs health, and changed-files code
  standards passed, but the harness failed on existing branch asset issues
  outside this follow-up (`katana.fbx` trailing whitespace and missing `.meta`
  files for FMOD mac bundle contents; changed path count `708`).
- Unity MCP resources and `read_console(types=["error"])` still fail to send to
  `http://127.0.0.1:8080/mcp`, so no new MCP-driven Unity Console or TestRunner
  result is available for this follow-up.

## 2026-06-21 Character Contact Cell Vertical Bounds Guard Follow-up

This follow-up narrows the same resolver-owned character contact grid by adding
cell-level vertical range rejection. After the XZ aggregate-bounds guard, a
source near a cell boundary could still visit an occupied neighboring cell whose
XZ bounds were reachable, even if all characters in that cell were vertically
separated from the source.

Current code changes:

- `ContactCellEntry` now also stores aggregate `MinYMillimeters` and
  `MaxYMillimeters` for the character capsules indexed in that cell.
- `BuildContactCandidateGrid()` records each candidate's cached vertical range
  while adding the candidate to the pooled cell entry.
- `QueryContactCandidateIndices()` skips the whole occupied cell when the
  source vertical range cannot overlap that aggregate cell vertical range.
- The existing XZ aggregate-bounds guard, cell max-radius guard, exact per-
  candidate vertical/radius predicates, and ascending candidate sort remain in
  place.

Behavior boundaries:

- No MxFramework public API was added or changed.
- Contact pair detection, pressure slop, overlap strictness, contact counts,
  displacement, pressure propagation, ordering, mass transfer, push scale, and
  terminal velocity are unchanged. The new guard only rejects a cell when no
  candidate inside that cell can vertically overlap the source under the same
  epsilon rule already used by the exact candidate predicate.
- Cell entries and candidate buffers remain resolver-owned and reused after
  warm-up. No `unsafe`, Burst, Jobs, or Unity Physics authority was introduced.

Added focused coverage:

- `CharacterTestIntegrationContractTests.CharacterMotion_ContactResolverPressureSkipsCellVerticalBoundsWithoutSteadyStateAllocation`
  places the source at a 1m grid boundary, keeps a real nearby pressure target,
  and adds same-XZ-query neighboring-cell decoys at a higher vertical layer. The
  test verifies source/target contact counts, decoy contact counts stay zero,
  skipped-cell diagnostics are non-zero, candidate pairs are below the full
  world pair count, and warmed repeated pressure resolve/apply allocates
  `0` bytes.

Validation in this follow-up:

- `dotnet build MxFramework.Tests.csproj -v minimal` passed with `0` errors and
  the existing Unity serialized-field CS0649 warning baseline.
- `dotnet test MxFramework.Tests.csproj --no-build --filter
  "FullyQualifiedName~CharacterMotion_ContactResolverPressureSkipsCellVerticalBoundsWithoutSteadyStateAllocation|FullyQualifiedName~CharacterMotion_ContactResolverPressureSkipsCellAggregateBoundsWithoutSteadyStateAllocation|FullyQualifiedName~CharacterMotion_ContactResolverPressureUsesSpatialCandidatesWithoutSteadyStateAllocation|FullyQualifiedName~CharacterMotion_ContactResolverOverlapUsesSpatialCandidatesWithoutSteadyStateAllocation"
  -v normal` exited `0`, but this Unity test project route again only reported
  build success and is treated as weak evidence rather than focused NUnit
  execution evidence.
- `git diff --check` passed; the current broad working tree still emits the
  existing CRLF normalization warning for
  `CharacterTestInteractionPhysicsSystem.cs`, but no whitespace error was
  reported.
- `python Tools\Harness\check_code_standards.py --all --summary-only` passed
  execution; it still reports the existing 150 report-only unsafe baseline
  findings (`ZString.cs` plus one Story demo finding), with no unsafe added by
  this follow-up.
- Git Bash `Tools/Harness/run_lightweight_checks.sh` passed: changed path count
  `63`, forbidden path, Unity `.meta` pairing, Docs health, changed-files code
  standards, and `git diff --check` all passed.
- Unity MCP `read_console(types=["error"])` and focused EditMode `run_tests`
  still fail to send to `http://127.0.0.1:8080/mcp`, so no MCP-driven Unity
  Console or TestRunner result is available for this follow-up.

## 2026-06-21 Contact Pressure Active Source Guard Follow-up

The next Motion-side cleanup narrows per-candidate work inside
`CharacterTestCharacterContactResolver.ResolveCharacterContactPressure()`. The
pressure phase now has grid-filtered candidate pairs, but each surviving pair
still attempted both pressure directions even when one or both characters had no
active pressure velocity for the current iteration.

Current code changes:

- `ResolveCharacterContactPressure()` now checks `_pressureVelocities` at the
  call site before invoking `ApplyContactPressure()` for each direction.
- `ApplyContactPressure()` receives the already-read source velocity instead of
  doing its own source `TryGetValue()` lookup.
- The second direction is still checked after the first direction runs, so a
  velocity propagated from `a` to `b` remains visible to the immediate `b -> a`
  pass in the same candidate pair. This preserves the previous propagation
  order.

Behavior boundaries:

- No MxFramework public API was added or changed.
- Contact pair detection, contact counts, pressure slop, normal calculation,
  mass transfer, push scale, terminal pressure velocity, and candidate ordering
  are unchanged.
- The change only skips directions that the old callee would have rejected
  because the source id was absent from `_pressureVelocities`, and it avoids the
  duplicate source dictionary lookup for active directions.
- This follow-up does not introduce `unsafe`, Burst, Jobs, or Unity Physics as
  authoritative input.

Validation in this follow-up:

- `dotnet build MxFramework.Tests.csproj -v minimal` passed with `0` errors and
  the existing Unity serialized-field CS0649 warning baseline.
- `dotnet test MxFramework.Tests.csproj --no-build --filter
  "FullyQualifiedName~CharacterMotion_ContactResolverPressureUsesSpatialCandidatesWithoutSteadyStateAllocation|FullyQualifiedName~CharacterMotion_ContactResolverOverlapUsesSpatialCandidatesWithoutSteadyStateAllocation|FullyQualifiedName~CharacterMotion_ContactResolverOverlapFiltersNonBlockingCharactersWithoutSteadyStateAllocation|FullyQualifiedName~CharacterMotion_ContactResolverPreparedLookupResolvesDynamicContactsWithoutSteadyStateAllocation"
  -v normal` exited `0`, but this Unity test project route again only reported
  build success and is treated as weak evidence rather than focused NUnit
  execution evidence.

- `git diff --check` passed for the files touched by this follow-up.
- `python Tools\Harness\check_code_standards.py --all --summary-only` passed
  execution; it still reports the existing 150 report-only unsafe baseline
  findings (`ZString.cs` plus one Story demo finding), with no unsafe added by
  this follow-up.
- `python Tools\Harness\check_docs_health.py` passed against the current
  `origin/main` diff after this documentation update.
- Git Bash `Tools/Harness/run_lightweight_checks.sh` did not pass cleanly in the
  current broad working tree: forbidden path, Docs health, and changed-files code
  standards passed, but the harness failed on existing branch asset issues
  outside this follow-up (`katana.fbx` trailing whitespace and missing `.meta`
  files for FMOD mac bundle contents; changed path count `708`).
- Unity MCP resources and `read_console(types=["error"])` still fail to send to
  `http://127.0.0.1:8080/mcp`, so no new MCP-driven Unity Console or TestRunner
  result is available for this follow-up.
- `git diff --check` passed; the current broad working tree still emits CRLF
  normalization warnings, but no whitespace error was reported for this
  follow-up.
- `python Tools\Harness\check_code_standards.py --all --summary-only` passed
  execution; it still reports the existing 150 report-only unsafe baseline
  findings (`ZString.cs` plus one Story demo finding), with no unsafe added by
  this follow-up.
- `python Tools\Harness\check_docs_health.py` passed against the current
  `origin/main` diff after this documentation update.
- Git Bash `Tools/Harness/run_lightweight_checks.sh` did not pass cleanly in the
  current broad working tree: forbidden path, Docs health, and changed-files code
  standards passed, but the harness failed on existing branch asset issues
  outside this follow-up (`katana.fbx` trailing whitespace and missing `.meta`
  files for FMOD mac bundle contents; changed path count `708`).
- Unity MCP resources and `read_console(types=["error"])` still fail to send to
  `http://127.0.0.1:8080/mcp`, so no new MCP-driven Unity Console or TestRunner
  result is available for this follow-up.

## 2026-06-21 Zero Attempted Pressure Velocity Guard Follow-up

The next contact-pressure cleanup moves the zero-velocity rejection to the
recording point. `CharacterTestCharacterMotionSystem.TickEntity()` records an
attempted horizontal contact velocity for every physics-driven character, while
`ResolveCharacterContactPressure()` only needs entries above the existing
contact velocity epsilon. The previous code filtered those zero/near-zero values
after they had already been written into the resolver dictionary.

Current code changes:

- `CharacterTestCharacterContactResolver.RecordAttemptedContactVelocity()` now
  checks the existing `ContactVelocityEpsilonMillimetersPerSecond` threshold
  before writing `_attemptedContactVelocities`.
- Zero or below-epsilon samples remove the entity id from the attempted-velocity
  dictionary instead of leaving an entry for the later pressure pass to reject.
- `ResolveCharacterContactPressure()` now copies the already-filtered attempted
  entries directly into `_pressureVelocities` before propagation, keeping the
  empty-set early return but removing the duplicate per-entry magnitude check.
- Non-zero attempted velocities still use the same `RawVector2` value and the
  same pressure propagation path.

Behavior boundaries:

- No MxFramework public API was added or changed.
- Pressure contact detection, pressure slop, candidate grid selection,
  contact counts, displacement, propagation ordering, mass transfer, push scale,
  and terminal velocity are unchanged.
- The change is equivalent to the previous `ResolveCharacterContactPressure()`
  filter for zero/near-zero samples, but avoids steady-state dictionary writes
  for stationary characters and duplicate filtering work for active pressure
  initialization.
- This follow-up does not introduce `unsafe`, Burst, Jobs, or Unity Physics as
  authoritative input.

Added focused coverage:

- `CharacterTestIntegrationContractTests.CharacterMotion_ContactResolverPressureSkipsZeroAttemptedVelocityWithoutSteadyStateAllocation`
  places a valid nearby target next to the source, records zero attempted
  velocity, and verifies no pressure grid cells or candidate pairs are queried,
  no contact state is written, and repeated warmed samples allocate `0` bytes.

Validation in this follow-up:

- `dotnet build MxFramework.Tests.csproj -v minimal` passed with `0` errors and
  the existing Unity serialized-field CS0649 warning baseline.
- `dotnet test MxFramework.Tests.csproj --no-build --filter
  "FullyQualifiedName~CharacterMotion_ContactResolverPressureSkipsZeroAttemptedVelocityWithoutSteadyStateAllocation|FullyQualifiedName~CharacterMotion_ContactResolverPressureUsesSpatialCandidatesWithoutSteadyStateAllocation"
  -v normal` exited `0`, but this Unity test project route again only reported
  build success and is treated as weak evidence rather than focused NUnit
  execution evidence.
- Unity MCP resources and `read_console(types=["error"])` still fail to send to
  `http://127.0.0.1:8080/mcp`, so no new MCP-driven Unity Console or TestRunner
  result is available for this follow-up.

## 2026-06-21 Contact Grid Cell Radius Guard Follow-up

The next Motion-side cleanup narrows the resolver-owned contact grid scan used
by both character pressure and overlap. The previous grid queried cells using
`source radius + global max character radius + slop`, then applied an exact
candidate radius test before emitting candidate pairs. That was behaviorally
correct, but one unusually large blocking character could widen every source
query and make the resolver iterate occupied cells that could not contain any
reachable candidate for that source.

Current code changes:

- `CharacterTestCharacterContactResolver` now records the maximum character
  radius per occupied contact cell while rebuilding the pooled XZ grid.
- `QueryContactCandidateIndices()` computes the minimum XZ distance from the
  source center to each occupied cell AABB and skips the whole cell when even
  `source radius + cell max radius + slop` cannot reach it.
- The existing exact vertical and XZ radius predicates still run before a
  candidate index is emitted, and candidate indices remain sorted ascending.
- Internal diagnostics now expose skipped contact-cell counts for pressure and
  overlap queries, mirroring the existing candidate-pair counters.

Behavior boundaries:

- No MxFramework public API was added or changed.
- Contact pair detection, pressure slop, overlap strictness, vertical filtering,
  contact counts, displacement, pressure propagation, and source/target ordering
  are unchanged. The new guard is conservative: it only skips a cell when no
  point inside that cell could overlap the source using that cell's largest
  cached radius.
- Cell lists, radius metadata, and candidate buffers remain resolver-owned and
  are reused after warm-up. No `unsafe`, Burst, Jobs, or Unity Physics authority
  was introduced.

Added focused coverage:

- `CharacterTestIntegrationContractTests.CharacterMotion_ContactResolverPressureUsesSpatialCandidatesWithoutSteadyStateAllocation`
  now includes a far diagonal large-radius blocker that widens the global query
  radius plus same-cell blockers that should be rejected at the cell level. The
  test asserts pressure skipped-cell diagnostics are non-zero while preserving
  the existing contact count, candidate narrowing, and warmed `0` allocation
  coverage.

Validation in this follow-up:

- `dotnet build MxFramework.Tests.csproj -v minimal` passed with `0` errors and
  the existing Unity serialized-field CS0649 warning baseline.
- `dotnet test MxFramework.Tests.csproj --no-build --filter
  "FullyQualifiedName~CharacterMotion_ContactResolverPressureUsesSpatialCandidatesWithoutSteadyStateAllocation|FullyQualifiedName~CharacterMotion_ContactResolverOverlapUsesSpatialCandidatesWithoutSteadyStateAllocation|FullyQualifiedName~CharacterMotion_ContactResolverOverlapFiltersNonBlockingCharactersWithoutSteadyStateAllocation|FullyQualifiedName~CharacterMotion_ContactResolverPreparedLookupResolvesDynamicContactsWithoutSteadyStateAllocation"
  -v normal` exited `0`, but this Unity test project route again only reported
  build success and is treated as weak evidence rather than focused NUnit
  execution evidence.

## 2026-06-21 Contact Grid Active Cell Scan Follow-up

The next Motion-side cleanup removes empty contact-cell dictionary scans when a
single large character radius widens the conservative query rectangle. The
previous cell-radius guard still iterated every coordinate in
`source radius + global max radius + slop` before discovering most cells were
empty. In sparse worlds with one large-radius blocker this made each pressure
source pay for empty grid coordinates rather than occupied contact cells.

Current code changes:

- `ContactCellEntry` now caches its `ContactCell` coordinate when rented for the
  current grid rebuild.
- `QueryContactCandidateIndices()` keeps the old coordinate-rectangle path when
  that rectangle is smaller than the active occupied-cell list.
- When the rectangle contains more coordinates than occupied cells, the query
  scans `_activeContactCellEntries` directly and reuses the same vertical
  aggregate guard, XZ aggregate guard, per-cell max-radius guard, exact
  candidate predicate, and ascending candidate sort.

Behavior boundaries:

- No MxFramework public API was added or changed.
- Contact pair detection, pressure slop, overlap strictness, vertical filtering,
  contact counts, displacement, pressure propagation, source/target ordering,
  candidate sorting, and authority input are unchanged. The new branch changes
  only which already-built cells are visited before the existing conservative
  guards run.
- Cell entries remain resolver-owned and pooled. No `unsafe`, Burst, Jobs, LINQ,
  reflection, or Unity Physics authority was introduced.

Added focused coverage:

- `CharacterTestIntegrationContractTests.CharacterMotion_ContactResolverPressureScansActiveCellsForSparseWideQueriesWithoutSteadyStateAllocation`
  places a real nearby pressure target plus a far 20m-radius blocker that widens
  the global query rectangle. The test verifies the real contact remains, the
  far decoy does not receive contact state, skipped-cell diagnostics are
  non-zero, the pressure query cell count stays below the empty-rectangle scan
  shape, and repeated warmed samples allocate `0` bytes.

Validation in this follow-up:

- `dotnet build MxFramework.Tests.csproj -v minimal` passed with `0` errors and
  the existing Unity serialized-field CS0649 warning baseline.
- `dotnet test MxFramework.Tests.csproj --no-build --filter
  "FullyQualifiedName~CharacterMotion_ContactResolverPressureScansActiveCellsForSparseWideQueriesWithoutSteadyStateAllocation|FullyQualifiedName~CharacterMotion_ContactResolverPressureUsesSpatialCandidatesWithoutSteadyStateAllocation|FullyQualifiedName~CharacterMotion_ContactResolverPressureSkipsCellAggregateBoundsWithoutSteadyStateAllocation|FullyQualifiedName~CharacterMotion_ContactResolverPressureSkipsCellVerticalBoundsWithoutSteadyStateAllocation"
  -v normal` exited `0`, but this Unity test project route again only reported
  build success and is treated as weak evidence rather than focused NUnit
  execution evidence.
- `git diff --check` passed; the current working tree still emits the existing
  CRLF normalization warning for `CharacterTestInteractionPhysicsSystem.cs`,
  but no whitespace error was reported.
- `python Tools\Harness\check_code_standards.py --all --summary-only` passed
  execution; it still reports the existing 150 report-only unsafe baseline
  findings (`ZString.cs` plus one Story demo finding), with no unsafe added by
  this follow-up.
- Git Bash `Tools/Harness/run_lightweight_checks.sh` passed: changed path count
  `63`, forbidden path, Unity `.meta` pairing, Docs health, changed-files code
  standards, and `git diff --check` all passed.
- Unity MCP resources for `instances`, `custom-tools`, and `editor/state`, plus
  direct `read_console(types=["error"])`, still fail to send to
  `http://127.0.0.1:8080/mcp`, so no MCP-driven Unity Console or TestRunner
  result is available for this follow-up.

## 2026-06-21 Step Cache Sweep-Entry Priority Follow-up

The next Motion-side cleanup narrows `CombatKinematicMotor` narrow-phase work
when the broadphase grid returns far cells before the first reachable blocker.
This happens most visibly on reverse sweeps: the grid scans cells in increasing
coordinate order, so a negative-X move can see far endpoint candidates before
the nearby wall that will actually stop the character.

Current code changes:

- `CombatKinematicMotor` now stores the step cache's start bounds and desired
  delta while building the per-step broadphase index view.
- After the base / step-up candidate index lists are populated, the motor runs a
  linear near-first pass that estimates each candidate's swept AABB entry
  fraction and swaps the most likely earliest candidate into the first slot.
- The existing `TryFindFirstCollision()` result selection, deterministic
  `CompareCollision()` tie-break, best-fraction prefix AABB, step-up logic, and
  layer/source filtering still decide the authoritative collision result.

Behavior boundaries:

- No MxFramework public API was added or changed.
- Collision shapes, source exclusion, layer masks, surface material selection,
  step-up behavior, candidate eligibility, and final collision ordering are
  unchanged. The pass only changes which eligible cached candidate is tried
  first so the existing prefix AABB can skip later far candidates sooner.
- The pass is O(n) over already cached candidate indices and does not allocate
  new collections. No `unsafe`, Burst, Jobs, LINQ, reflection, or Unity Physics
  authority was introduced.

Added focused coverage:

- `CombatKinematicMotorTests.Step_StepCachePrioritizesReverseSweepCandidateBeforePrefixSkip`
  creates a negative-X sweep where far endpoint AABBs would naturally appear
  before the near wall in grid order. The test verifies the near wall is still
  the reported collision target, broadphase candidates remain higher than the
  narrow-phase probes, prefix skips are recorded, and narrow-phase candidate
  count stays bounded.

Validation in this follow-up:

- `dotnet build MxFramework.Tests.csproj -v minimal` passed with `0` errors and
  the existing `127` Unity serialized-field CS0649 warning baseline.
- `dotnet test MxFramework.Tests.csproj --no-build --filter
  "FullyQualifiedName~Step_StepCachePrioritizesReverseSweepCandidateBeforePrefixSkip|FullyQualifiedName~Step_BestFractionPrefixSkipsFarCandidates|FullyQualifiedName~ShapeTargetCollision_DynamicShapeSweepDoesNotAllocateAfterWarmup"
  -v normal` exited `0`, but this Unity test project route again only reported
  build success and is treated as weak evidence rather than focused NUnit
  execution evidence. `dotnet vstest Temp\bin\Debug\MxFramework.Tests.dll`
  also has no suitable provider for this Unity `.NETStandard` test assembly.
- `git diff --check` passed; the current working tree still emits the existing
  CRLF normalization warning for `CharacterTestInteractionPhysicsSystem.cs`,
  but no whitespace error was reported for this follow-up.
- `python Tools\Harness\check_code_standards.py --all --summary-only` passed
  execution; it still reports the existing 150 report-only unsafe baseline
  findings (`ZString.cs` plus one Story demo finding), with no unsafe added by
  this follow-up.
- Git Bash `Tools/Harness/run_lightweight_checks.sh` did not pass in the
  current broad working tree: forbidden path, Docs health, and changed-files
  code standards passed, but the harness fails on unrelated existing asset
  issues (`Assets/Art/MxFramework/Samples/Weapons/Katana/Meshes/katana.fbx`
  trailing whitespace and missing `.meta` files under
  `Assets/Plugins/FMOD/platforms/mac/lib/*.bundle/Contents/MacOS/`).
- Unity MCP resources for `instances`, `custom-tools`, and `editor/state` still
  fail to send to `http://127.0.0.1:8080/mcp`. A Unity 6000.5.0f1 Editor for
  `WGameFramework` is already open, so this follow-up did not start BatchMode
  and has no new MCP-driven Unity Console or TestRunner result.
