# MxAnimation Phase 3 Agent Runbook

> Source: Gitea Issue #122.
> Purpose: before starting any MxAnimation Phase 3 task, read this file to keep queue order, validation scope, and merge flow consistent.

## Phase Goal

MxAnimation Phase 3 turns the Phase 2 vertical slice into an extensible production pipeline:

- PlayableGraph backend can be abstracted, reused, and extended.
- Blend support grows from 1D locomotion to deterministic 2D blend tree alternatives.
- Skeleton, AvatarMask, socket, clip, and bake compatibility become diagnosable.
- Bake artifacts become reusable inputs for Combat references, Authoring preview, and timeline alignment.
- Editor preview moves from text summaries toward a read-only Timeline / Scrubber MVP.
- Animation resources can move from sample catalog loading toward bundle, remote bundle, and mod package override flows.

The boundary does not change: Combat authority never reads Animator, PlayableGraph, or live bone pose state. Animation output remains presentation-side unless an explicit baked reference contract says otherwise.

## Queue

Only one child Issue should be `status/agent-ready` at a time.

| Order | Issue | Task | Expected Weight | Gate |
| --- | --- | --- | --- | --- |
| 1 | #123 | MxAnimation 14: PlayableGraph Backend Abstraction | S2 | Starts Phase 3; currently agent-ready |
| 2 | #124 | MxAnimation 15: PlayableGraph State Cache + Reuse | S2 | Open after #123 is merged |
| 3 | #125 | MxAnimation 16: 2D Blend Tree Runtime Contract | S2 | Open after #124 is merged |
| 4 | #126 | MxAnimation 17: Skeleton / Avatar Compatibility Validation | S2 | Can follow #123, but keep queue order unless user changes it |
| 5 | #127 | MxAnimation 18: Bake Pipeline Extended Data | S2 | Requires compatibility vocabulary from #126 |
| 6 | #128 | MxAnimation 19: Timeline / Scrubber Preview MVP | S3 | Requires extended bake data from #127 |
| 7 | #129 | MxAnimation 20: Addressable / Bundle Animation Package Loading | S3 | Requires compatibility and bake expectations |
| 8 | #130 | MxAnimation 21: Mod Animation Package Override | S3 | Requires package loading from #129 |

Do not start #124-#130 while #123 is still open unless the user explicitly changes the queue.

## Standard Loop

1. Sync `main` from Gitea `origin`.
2. Check dirty state with `git status --short --branch`.
3. Ignore unrelated local changes, especially `Assets/Plugins/FMOD/Cache/Editor/FMODStudioCache.asset`, unless the user explicitly asks to handle them.
4. Confirm the active child Issue is open and labeled `status/agent-ready`.
5. Read the bounded Context Pack:
   - `AGENTS.md`
   - `Assets/AGENTS.md`
   - `Assets/Scripts/MxFramework/AGENTS.md`
   - the active Gitea Issue
   - `Docs/Interfaces/Animation.md`
   - the relevant existing MxAnimation task docs
   - only the source and tests needed for the active task
6. Create a task branch named `codex/issue-<id>-<short-mxanimation-topic>`.
7. Before coding, state the API reuse plan:
   - noEngine contract
   - Unity backend or Editor adapter
   - ResourceManager / ResourceCatalog / warmup usage
   - Combat boundary impact
   - tests and docs to update
8. Implement narrowly. Do not mix future child Issue work into the current Issue.
9. Run the validation matrix that matches the task weight.
10. Create a PR to `main`, with validation evidence and notes about unrelated dirty files.
11. Review the PR. If mergeable, review comments are empty or fixed, and checks passed, merge automatically.
12. After merge:
   - switch to `main`
   - `git pull --ff-only origin main`
   - remove `status/agent-ready` from the completed Issue
   - add `status/done`
   - close or confirm auto-close
   - comment with PR and validation summary
   - delete local and remote task branch
   - move the next child Issue from `status/spec-draft` to `status/agent-ready`

## Validation Matrix

Use the narrowest sufficient matrix, but do not skip boundary checks for public API or Unity backend work.

| Task Type | Required Checks |
| --- | --- |
| noEngine contract | `dotnet build MxFramework.Animation.csproj --no-restore -v minimal`; relevant noEngine tests; noEngine Unity reference scan |
| Unity backend | `dotnet build MxFramework.Animation.Unity.csproj --no-restore -v minimal`; backend focused EditMode tests; smoke test when behavior is visible |
| Combat adapter | `dotnet build MxFramework.Combat.csproj --no-restore -v minimal`; Combat adapter tests; verify Combat does not read Unity pose |
| Editor tool | Editor focused tests; Unity Console error check; use Unity Editor / Unity MCP / Editor API for serialized assets |
| Scene or Playable demo | Unity MCP or batchmode Play Mode check; scene opens and plays without manual wiring; Console has no new runtime errors |
| Cross-module or public API | GitNexus impact analysis; update `Docs/Interfaces/Animation.md` and related usage docs |

Useful boundary scans:

```sh
rg -n "UnityEngine|UnityEditor|Playable" Assets/Scripts/MxFramework/Animation
rg -n "Animator|PlayableGraph|GetBoneTransform" Assets/Scripts/MxFramework/Combat
git diff --check -- '*.cs' '*.md' '*.uxml' '*.uss'
```

Do not rely on `git diff --check` for Unity YAML files alone; Unity can emit blank scalar spacing. Use source-file diff checks and Unity validation for serialized assets.

## Child Issue Notes

### #123 PlayableGraph Backend Abstraction

Keep the public `IMxAnimationBackend` behavior stable. The goal is internal graph construction and mixer boundary extraction, not graph cache, 2D blend, package loading, or scrubber UI.

Acceptance focus:

- existing play / stop / crossfade behavior preserved
- layer weight and AvatarMask behavior preserved
- 1D blend still works
- `CombatMxAnimationUnityBridge` and smoke demo do not bypass backend
- docs explain the new abstraction boundary

### #124 PlayableGraph State Cache + Reuse

Do not change ResourceManager ownership semantics. Cache only where release behavior is clear.

Acceptance focus:

- repeated idle / walk / run changes do not grow handle counts indefinitely
- repeated upper body attacks release or reuse completed playables predictably
- diagnostics show cache or resident state
- backend release returns graph and resource references to expected state

### #125 2D Blend Tree Runtime Contract

2D weight calculation belongs in noEngine code. Unity backend consumes deterministic weights; it does not invent authoritative state from Unity floats.

Acceptance focus:

- deterministic 2D weight tests
- stable definition hash
- warmup includes all point clips
- Unity backend diagnostics show active 2D blend and weights

### #126 Skeleton / Avatar Compatibility Validation

Separate noEngine diagnostics from Unity extraction. Unity Editor code may read Avatar, ModelImporter, and clips; runtime DTOs must remain Unity-free.

Acceptance focus:

- missing bone / socket / mask / skeleton hash mismatch reports
- reusable report structure for mapping, warmup, bake, package loading, and mod override
- no automatic repair or silent fallback

### #127 Bake Pipeline Extended Data

Bake artifacts are derived cache. They can provide Combat reference data only through explicit deterministic adapter inputs.

Acceptance focus:

- root / socket / weapon / event data included in artifact hash
- mismatch diagnostics identify source, profile, skeleton, and artifact
- Combat adapter never reads live Animator or PlayableGraph state

### #128 Timeline / Scrubber Preview MVP

This is a read-only Editor preview. Do not turn it into a full timeline editor in this Issue.

Acceptance focus:

- select action / clip / bake artifact
- scrub by frame
- show event, CombatActionTimeline, baked root/socket/weapon trace alignment
- missing or mismatched data appears as diagnostics

### #129 Addressable / Bundle Animation Package Loading

Addressables remain optional. Prefer the existing ResourceManager and provider abstractions unless a concrete gap is proven.

Acceptance focus:

- same mapping can load through sample provider or bundle provider
- bundle load failure and hash/version mismatch are diagnosed
- warmup can preload package clips, masks, and bake artifacts

### #130 Mod Animation Package Override

Mod packages may override presentation mapping only. They must not modify Combat hit, damage, cancel, invulnerability, replay hash, or authoritative movement rules.

Acceptance focus:

- base + override merge produces stable mapping hash
- valid override changes presentation clip / mask / blend / bake keys
- incompatible override is rejected with diagnostics
- load and unload release resource handles predictably

## PR Review And Auto-Merge Rules

Auto-merge is allowed for this task chain when all of these are true:

- PR targets `main` and is mergeable.
- Review comments are empty, non-blocking, or already addressed.
- Required validation is documented in the PR body.
- No unrelated files are staged or committed.
- Unity serialized assets were created or updated through Unity Editor, Unity MCP, or existing Editor API.
- The PR does not advance a later child Issue by accident.

After merging, always update the Gitea labels before starting the next Issue.

## Phase Closeout

Close #122 only when #123-#130 are all closed with `status/done`, docs reflect the final public API, and the final comment on #122 lists each merged PR and validation class.
