# LCC-M3-T5 implementation evidence — 2026-08-25

> Scope: CharacterTest Severed presentation projection, missing-resource fallback,
> view lifecycle, developer diagnostics, focused behavior regression, and real Game View evidence.
>
> State: implementation and focused evidence complete; task remains
> `In Progress（待主代理集成复核）`. Parent milestone and ledger are unchanged.

## Delivered capability

- `CharacterTestRuntimeCharacterView` consumes the typed Severed transition once per
  `(entityId, partStateId, transition.After.Revision)`. The entity key is fixed by the
  owning view, and each part keeps a bounded revision record.
- A live transition is consumed before current-state synchronization. A late view
  attach or restored/current Severed state is projected silently and therefore does
  not replay one-shot presentation.
- `Release` clears all presentation records before destroying the runtime instance.
  A new view/respawn begins with a clean record and projects current state without
  inheriting event/VFX/SFX/limb state from the old view.
- The existing view bridge calls the projection from its normal entity sync. No
  BodyPartState, Hurtbox, CharacterAction, SaveState, Replay, Resource Manager, VFX,
  Audio, network, schema, or parent-milestone contract was changed.
- The existing CharacterTest developer `Characters` tab now adds a read-only
  `Severance Runtime Slice` card. Only this Severance-card increment in the shared
  snapshot builder belongs to T5; the pre-existing Presence-card diff belongs upstream.

## Resource capability and fail-soft result

The frozen Dragon14 resource set contains:

- `Assets/MxFrameworkGenerated/CharacterPackages/000003/resources/models/000003_basemesh2.fbx`;
- `Assets/MxFrameworkGenerated/CharacterPackages/000003/materials/000003_Body.mat`;
- `Assets/MxFrameworkGenerated/CharacterPackages/000003/materials/000003_Eye.mat`;
- the whole-character `000003_character_preview.prefab` and its Resources copy.

No independent tail renderer, tail mesh, or stable tail resource key was proven.
The delivered typed presentation status is therefore
`RuntimeSliceNoStableResource`:

- the whole Dragon14 renderer remains visible;
- no bone is disabled;
- no VFX or SFX is emitted;
- no detached view, fake limb, Rigidbody, collider, obstacle, damage source, or
  gameplay/world entity is created;
- authority, topology, and action behavior remain upstream and unaffected by the
  missing presentation resource.

This is an honest **Runtime Slice**, not a complete visual-detachment Playable.

## Focused test evidence

| Gate | Raw result |
| --- | --- |
| Presenter, event dedupe, late attach/restore, fail-soft resource, release/respawn, diagnostic separation | EditMode job `a3784ba37eda4e6398c807633fac9bb3`; 4/4 passed, 0 failed/skipped, 0.2693025 s |
| Tail-only rejection and non-tail continuation through the frozen T4 seams | EditMode job `6018280ef3694e0797684e7101fdc9e5`; 2/2 passed, 0 failed/skipped, 0.5907519 s |
| Real Unity view lifecycle: restored Severed state, deferred Destroy, respawn, no replay/leak | PlayMode job `4a50d9982422450b9f83cb3af59775f7`; 1/1 passed, 0 failed/skipped, 0.2781806 s |

The reused T4 cases were exactly:

- `Dragon14SeveredTail_KeepsAttack6BiteButRejectsTailOnlyAttack30`: the real
  tail-only Attack30 is rejected after Severed while Attack6 keeps its independent bite;
- `ActiveTailTrace_SeveredPrunesBeforeNextFrameWithoutUndoingAcceptedHit`: the active
  tail trace is removed before its next damage opportunity without undoing a hit
  already accepted by authority.

Jobs `9af1deba32f74b66a79cab47e201ee4f`,
`ce697f55f648415fbd48fe0105b281f5`, and
`953d0191dc4948beb06ad7f5b2f739cb` each discovered zero tests before the new
fixture was imported. They are explicitly excluded from acceptance evidence.

## Real CharacterTest Game View

### Before: normal Intact state

`GAME_VIEW_BEFORE_INTACT_STATUS_2026-08-25.png` is a real
`CharacterTestScene` Game View capture. Dragon14 is visible and the existing
developer `Characters` tab shows:

- Authority `Intact`, `partstate.000003.tail`, revision `0`;
- Hurtbox topology `3/3 member leaves active`, topology revision `102`;
- Action Dependency `Satisfied`;
- resource limitation `RuntimeSliceNoStableResource`;
- emitted presentation `VFX=0 SFX=0 limbs=0`.

### After: one-frame Severed convergence

`GAME_VIEW_AFTER_SEVERED_STATUS_EDITOR_DRIVE_2026-08-25.png` is a real
`CharacterTestScene` Game View capture after exactly one natural Editor frame
converged T2/T3/T4/ViewBridge. It visibly records:

- Authority `Severed`, tail revision `3`;
- Hurtbox topology `0/3 member leaves active`, topology revision `103`;
- Action Dependency `Severed`;
- Presentation `RuntimeSliceNoStableResource`;
- event dedupe `consumed=1 duplicate=0 stateSync=0`;
- emitted presentation `VFX=0 SFX=0 limbs=0` and the explicit safety boundary
  that the whole renderer is unchanged.

### Mandatory evidence-drive disclosure

The after state used a one-time **Editor-only acceptance drive** approved by the
main agent. There is no driver file and no serialized change. While Play Mode was
globally paused, in-memory editor code called only the existing T1 reducer APIs to
commit a legal Intact-to-Broken authority frame and a following Broken-to-Severed
fact. It did not write reducer private fields. `EditorApplication.Step()` then
advanced exactly one ordinary frame so the existing T2/T3/T4/ViewBridge paths
converged naturally. Stopping Play discarded the temporary state.

This capture proves the view/topology/action projection for a valid typed Severed
transition. It **does not prove a production sever ingress caller, player action,
or network path**. The repository currently has no production caller of
`StageAcceptedSever`; its only callers remain tests/acceptance seams.

## Lifecycle, scene, and Console evidence

- The PlayMode fixture proves actual Unity deferred destruction of the released
  view and a clean respawned projection with no event replay or detached-object leak.
- Stopping the acceptance Play session returned `CharacterTestScene` to five
  authoring roots with `isDirty=false`; the 65 runtime roots and the temporary
  Severed authority state were unloaded.
- The final in-Play query before stop returned Console error count `0`; the
  post-stop query also returned Console error count `0` and scene `isDirty=false`.
- Final root `validate_unity_scripts` result is recorded below after its single
  required invocation.

## Exact T5 files

Production and diagnostics:

- `Assets/Scripts/MxFramework/Game/CharacterTest/Runtime/View/CharacterTestRuntimeCharacterView.cs`
- `Assets/Scripts/MxFramework/Game/CharacterTest/Runtime/View/CharacterTestCharacterUnityViewBridge.cs`
- `Assets/Scripts/MxFramework/Game/CharacterTest/Diagnostics/CharacterTestSeverancePresentationDebug.cs`
- `Assets/Scripts/MxFramework/Game/CharacterTest/Diagnostics/CharacterTestSeverancePresentationDebug.cs.meta`
- `Assets/Scripts/MxFramework/Game/CharacterTest/Diagnostics/DebugUi/CharacterTestDeveloperDebugSnapshotBuilder.cs` (Severance-card increment only)

Focused tests:

- `Assets/Scripts/MxFramework/Tests/Game/CharacterTest/CharacterTestSeverancePresentationTests.cs`
- `Assets/Scripts/MxFramework/Tests/Game/CharacterTest/CharacterTestSeverancePresentationTests.cs.meta`
- `Assets/Scripts/MxFramework/Tests/PlayMode/CharacterTest/CharacterTestSeverancePresentationLifecyclePlayModeTests.cs`
- `Assets/Scripts/MxFramework/Tests/PlayMode/CharacterTest/CharacterTestSeverancePresentationLifecyclePlayModeTests.cs.meta`

Task record and visual evidence:

- `Docs/Guides/CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M3_T5_PRESENTATION_RESOURCES.md`
- `Docs/Evidence/LCC/LCC-M3-T5/ACCEPTANCE_2026-08-25.md`
- `Docs/Evidence/LCC/LCC-M3-T5/GAME_VIEW_BEFORE_INTACT_STATUS_2026-08-25.png`
- `Docs/Evidence/LCC/LCC-M3-T5/GAME_VIEW_AFTER_SEVERED_STATUS_EDITOR_DRIVE_2026-08-25.png`

No Dragon14 model, material, Prefab, scene, authority, topology, action, network,
parent milestone, or ledger file was written by T5.

## Limits and T6 handoff

- Production sever ingress/player reachability is still missing. T5 cannot be
  described as a complete Playable or as proof that a player can sever the tail.
- No stable tail visual resource exists, so visual detachment, VFX, SFX, and a
  gameplay limb remain intentionally absent. A future resource-backed upgrade must
  prove an independent renderer/mesh/resource key before changing this fallback.
- T6 owns independent M3 review plus rollback/network/late-join verification. It
  should verify that restored/current Severed snapshots reach a newly attached view
  silently, without introducing a new presentation payload or replaying a one-shot.
  It must keep the missing production sever caller as an explicit milestone limit.
- No full suite, commit, stage, cleanup of unrelated state, push, parent status
  change, or ledger change was performed.

## Final Unity script validation

- The required single `validate_unity_scripts({})` invocation did not return a
  passing integrated result. Unity plugin session
  `c1033c54-663c-43b9-8550-6b253001fe59` disconnected while awaiting
  `command_result`; the tool returned `success=false` and supplied no job id. It
  was not repeated and is not represented as a passing validator job.
- The main agent approved one read-only fallback for that same validation attempt.
  `Logs/Editor.log` records `[ScriptCompilation] Requested script compilation`,
  `ExitCode: 0`, `Tundra build success`, `Reloading assemblies after finishing
  script compilation`, `Mono: successfully reloaded assembly`, and Domain Reload
  Profiling with `domain reloads=1`.
- The post-reload editor snapshot reported `activity.phase=idle`,
  `is_compiling=false`, `is_domain_reload_pending=false`, Play Mode stopped, and
  `last_domain_reload_after_unix_ms=1787605868647`. Its telemetry advice remained
  `stale_status`, so this record does not overstate the failed integrated tool as a
  confirmed `ready_for_tools=true` result. Live read-only editor calls still
  succeeded, and the Console error query returned 0 entries.
- No second validator, compile trigger, test run, or broader suite was executed.
