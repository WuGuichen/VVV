# LCC-M3-T4 implementation evidence — 2026-08-25

> Scope: typed CharacterAction part-state dependency, Runtime/Planner eligibility,
> active trace cancellation, the two frozen Dragon14 events, and focused gates.
>
> State: implementation and focused evidence complete; task remains
> `In Progress（待主代理集成复核）`. Parent milestone and ledger are unchanged.

## Implemented contract

- `CombatTrackEvent` carries the generic `RequiredPartStateId` and raw
  `CharacterActionPartStructureStateMask`. JSON DTO serialization, import, domain
  storage, and validation preserve the integer mask. A missing half-dependency,
  zero mask, non-`HitTraceWindow` dependency, or unknown/future mask bit produces
  `ACT_PART_STATE_DEPENDENCY_INVALID`; validation therefore fails closed without
  normalizing away the original raw value.
- CharacterTest owns one bounded, read-only eligibility evaluator. It distinguishes
  action unavailable, stale action binding, event unavailable, invalid dependency,
  unknown part state, invalid part-state binding, Severed, and other disallowed
  states. Runtime intent/action start and both ordinary and Joint Planner paths
  consume this evaluator instead of copying BodyPartState logic.
- Planner exposes the narrow typed failure
  `CharacterTestAttackOpportunityFailureReason.PartStateDependencyRejected` and
  its matching flag. It is used only for the part-state gate. Existing trace-plan
  failures such as missing socket/bake remain `TracePlanUnavailable`; unsupported
  action/category resolution remains `TraceEvaluatorUnsupported`.
- Action eligibility is event-aware: an action with at least one eligible
  `HitTraceWindow` stays startable. Thus real `940002` keeps its independent bite
  while its tail sweep is rejected after tail Severed; real tail-only `940004`
  is rejected, including the natural `Skill1` intent path.
- Each authority frame prunes active ineligible part-dependent windows after start
  request/ended-window handling and before trace provider configuration and trace
  generation. It clears that window's hit-once keys but does not revisit damage
  already accepted in an earlier/current authority frame.
- The current formal package contains no tail projectile, ability, socket, or
  interaction emitter requiring a T4 gate: **none found**. No generic emitter,
  presenter, T3 targeting, or network code was changed for T4.

## Frozen Dragon14 data

Only these two existing `BakedSocketCapsule` events gained the dependency fields
in both the authoring source and formal generated projection:

| Action / event | Existing trace contract | Added dependency |
| --- | --- | --- |
| `940002` / `000003.attack6a` / `000003.attack6.tail_sweep` | frame 112..126, `tailSweepStart -> tailSweepEnd`, radius 0.55 | `requiredPartStateId = partstate.000003.tail`; `allowedPartStructureStateMask = 3` (Intact or Broken) |
| `940004` / `000003.attack30b` / `000003.attack30b.tail_spin` | frame 108..134, `tailSweepStart -> tailSweepEnd`, radius 0.55 | `requiredPartStateId = partstate.000003.tail`; `allowedPartStructureStateMask = 3` (Intact or Broken) |

The bite event in `940002` is byte-for-byte unaffected by the dependency. The
focused fixture compares each complete source event with its generated projection,
then imports and validates the real source. This finishing pass did not hand-edit
or regenerate a serialized asset.

## Focused EditMode evidence

| Gate | Raw result |
| --- | --- |
| Corrected Planner parity single | job `ee2c89609e684916b581b5ddef8bb6c4`; 1/1 passed, 0 failed/skipped, 0.9886 s |
| Final T4 focused fixture | job `d8829e8e7db44354b68e59437240a00e`; 8/8 passed, 0 failed/skipped, 0.5240448 s |
| Warmed NoAlloc | `EligibilityAndTracePrune_WarmedHotPathAllocatesZeroBytes`; 64 warmup then 4096 eligibility + active-window-prune iterations; asserted **0 B**; included in final focused job |

The eight final cases cover:

- real source/generated JSON equality, importer propagation, and validation;
- missing id, missing mask, and future raw mask `8`, all preserved and rejected;
- real `940002` action/bite remains eligible while its tail sweep becomes
  `PartStateSevered`, and real `940004` becomes tail-only ineligible;
- a running tail trace is removed before the next-frame generation seam while a
  previously accepted 7-damage hit remains applied;
- ordinary/Joint Planner parity: intact missing-bake returns
  `TracePlanUnavailable`, while the same candidate after Severed returns the typed
  `PartStateDependencyRejected` in both paths;
- the warmed evaluator/prune hot path remains allocation-free.

Non-acceptance attempts are retained only for audit. Jobs
`112eb690f0754f0dbba210d8694c3530` and
`969f003d0b414ec5b32cfc914cd4422f` discovered zero tests before Unity imported
the new fixture and are not evidence. Initial full job
`c26ce9f3a7b24ba6a230e3e4d9d524fb` was 7/8 because the Planner fixture exposed
no target physics geometry. Repair singles
`d38bc32850b349a5afd0d73df2de9d00`,
`9780d9ad05024c0eb45262cf4f60e35a`, and
`dcb3c6965d8c40dfa2c5ace153db990a` were each 0/1 before the fixture's synthetic
physics world was corrected. No production behavior was broadened to repair them.

## Unity script validation

- The required single `validate_unity_scripts({})` invocation did not return a
  passing job: Unity plugin session
  `6a79d11c-36dc-4b98-9304-3aa72def7df9` disconnected while awaiting
  `command_result` during domain reload. The tool supplied no job id. It was not
  repeated and is not represented as a passed integrated-validator result.
- Per the root `AGENTS.md` explicit-failure fallback, a forced scripts refresh /
  compile request returned success with `compile_requested: true` and
  `resulting_state: compiling`. `Logs/Editor.log` then recorded `ExitCode: 0`,
  `Tundra build success`, `Reloading assemblies after finishing script
  compilation`, `Mono: successfully reloaded assembly`, and one completed domain
  reload. The fallback tool does not assign a job id.
- A subsequent readiness-only refresh returned success with
  `resulting_state: idle` and `Unity refresh completed; editor should be ready`.
  A detailed Console error query returned 0 entries. This is the actual fallback
  compile/reload/ready/Console evidence; the transport limitation above remains
  explicit for main-agent review.

## Exact T4 files

CharacterAction contract:

- `Assets/Scripts/MxFramework/CharacterAction/CharacterActionDiagnostic.cs`
- `Assets/Scripts/MxFramework/CharacterAction/CharacterActionJsonDtos.cs`
- `Assets/Scripts/MxFramework/CharacterAction/CharacterActionJsonImporter.Tracks.cs`
- `Assets/Scripts/MxFramework/CharacterAction/CharacterActionTracks.cs`
- `Assets/Scripts/MxFramework/CharacterAction/CharacterActionValidation.cs`

CharacterTest runtime and Planner (only the T4-owned portions of shared files):

- `Assets/Scripts/MxFramework/Game/CharacterTest/Combat/CharacterTestCharacterActionRuntime.cs`
- `Assets/Scripts/MxFramework/Game/CharacterTest/Runtime/Systems/Combat/CharacterTestAttackOpportunityService.cs`
- `Assets/Scripts/MxFramework/Game/CharacterTest/Runtime/Systems/Combat/CharacterTestCharacterCombatTraceSystem.cs`
- `Assets/Scripts/MxFramework/Game/CharacterTest/Runtime/Systems/Combat/CharacterTestHitTraceWindowState.cs`
- `Assets/Scripts/MxFramework/Game/CharacterTest/Runtime/Systems/RuntimeAiPlanner/CharacterTestRuntimeAiPlannerAttackOpportunityAdapter.cs`
- `Assets/Scripts/MxFramework/Game/CharacterTest/Runtime/Systems/RuntimeAiPlanner/CharacterTestRuntimeAiPlannerAttackOpportunityAdapter.Joint.cs`

Formal data, tests, and task evidence:

- `Authoring/CharacterPackages/character-000003/config/character_actions.json`
- `Assets/MxFrameworkGenerated/CharacterActions/Libraries/000003/character_actions.json`
- `Assets/Scripts/MxFramework/Tests/Game/CharacterTest/CharacterTestActionPartStateDependencyTests.cs`
- `Assets/Scripts/MxFramework/Tests/Game/CharacterTest/CharacterTestActionPartStateDependencyTests.cs.meta`
- `Docs/Guides/CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M3_T4_ACTION_TRACE_DEPENDENCY.md`
- `Docs/Evidence/LCC/LCC-M3-T4/ACCEPTANCE_2026-08-25.md`

## Deliberate limits and handoff

- No full repository suite, PlayMode/GameView/visual scenario, presentation,
  network, formal rollback/late join/hash proof, commit, stage, cleanup, or push
  was performed. No parent milestone or ledger state was changed.
- Main-agent integration review should preserve the narrow Planner reason mapping:
  part-state rejection is `PartStateDependencyRejected`; missing trace bake/socket
  stays `TracePlanUnavailable`; unsupported action/category stays
  `TraceEvaluatorUnsupported`.
- T6 remains the single independent M3 terminal review and owns rollback/network
  integration. The T4 evidence proves only source/import/runtime/Planner/trace
  behavior and the focused allocation/compile gates listed above.
