# LCC-M5-T3 Final Independent Read-only Review - 2026-08-26

> Scope: the single different-context M5 final review. Source, formal Dragon14 Authoring/generated data, tests, Evidence, CAPABILITIES and relevant interfaces were read only. The two submitted PNG files were inspected at original resolution. No production, test, Authoring, generated, scene, parent-status or other documentation file was changed; no staging, commit or push was performed.

## 1. Verdict

`Ready for M5 Verified` after the bounded P1 closure check recorded in section 3.1.

- P0: **0**.
- P1: **0 open**; the original two P1 findings are retained below as audit history and are now **Closed**.
- P2: **1**, does not independently block the current pressure slice but must be resolved before a phase consumer is promoted.
- P3: **2**, accepted only as explicit limitations.

The production slice is materially narrower and safer than a Hurtbox-as-collider shortcut: the review found no global Hurtbox obstacle promotion, no navigation/projectile/damage expansion, no Unity presentation state used as authority, no third production `SyncAll`, no schema 34, no direct displacement, and no unbounded pressure output. Formal Dragon14 v2/v7 data contains exactly the three frozen references and the generated binding matches it. The bounded closure reruns replaced the invalid visual and resolver-only performance evidence with a real-view multi-frame PlayMode path and a formal full-`RuntimeModule` 8-Dragon measurement, so no P0/P1 remains open.

## 2. P0 findings

None.

## 3. P1 findings

### P1-1 — Submitted “formal Game View” does not show Dragon14, the player, appendage contact, or tests-only markers `[Historical; Closed in 3.1]`

**Files / lines**

- `Docs/Evidence/LCC/LCC-M5-T3/LCC-M5-T3-Dragon14-Appendage-GameView.png`: the image is a pause menu headed `暂停菜单`; no Dragon14, player, head/tail, contact marker or contact reaction is visible.
- `Docs/Evidence/LCC/LCC-M5-T3/LCC-M5-T3-CharacterTestScene-DebugView.png`: the image is a distant empty Scene view and contains no inspectable M5 contact overlay or Dragon14 contact setup.
- `Docs/Evidence/LCC/LCC-M5-T3/PLAYABLE_2026-08-26.md:31-39`: the Evidence describes the Game View as formal appendage contact evidence with tests-only colored contact markers.
- `Assets/Scripts/MxFramework/Tests/PlayMode/CharacterTest/CharacterTestDragon14AnimatedAppendageSoftContactPlayModeTests.cs:45-57`: the fixture constructs the runtime with `createViews: false`, so it cannot display the spawned Dragon14 or player through the production view path.
- Same fixture `:83-137`: it teleports the player into three sampled bounds, observes one tick of contact velocity, teleports the player away before the next tick, then captures the screenshot only after all three samples.
- Same fixture `:146-176`: screenshot success checks only that a file exists; tests-only primitive markers are created but the assertion never proves that they are visible in the captured Game View.

**Reproduction**

Open both PNGs at original resolution. The Game View is fully covered by the pause menu. The Scene image is an empty distant overview. Neither reproduces the claimed player-facing appendage interaction.

**Impact**

The formal test is useful authority/runtime evidence for three static sampled contacts, but it never demonstrates the resulting velocity being consumed on the following tick, the player sliding/pushing away, or the player being prevented from passing fully through the animated model. The required moving, turning, high-speed, wall/corner and Severed player-facing scenarios also have no direct M5 visual/runtime sequence; the Automation Evidence itself acknowledges these gaps. Dependency tests from M1/M3/M4 do not replace the missing end-to-end M5 experience proof.

**Required disposition**

Blocks M5/T3 `Verified`. Re-capture one real, unobscured Game View run with production views enabled and no pause/menu overlay. It must visibly exercise at least head, tail-mid and tail-tip contact through the actual next-tick motor response. Add bounded direct runs for moving/turning appendage, high-speed, wall/corner and Severed tail; these may be one compact fixture/recording rather than new framework code. The Scene screenshot may remain contextual only and must not be cited as production contact evidence.

### P1-2 — The frozen 8-Dragon “full chain” performance gate is not measured `[Historical; Closed in 3.1]`

**Files / lines**

- `Assets/Scripts/MxFramework/Tests/Game/CharacterTest/CharacterTestAnimatedAppendageSoftContactPerformanceTests.cs:81-109`: the performance fixture times only direct calls to `ResolveAnimatedAppendageSoftContacts` and `ApplyCharacterContactState` using synthetic witness arrays. It does not run either Hurtbox `SyncAll`, previous/final witness capture, the formal Dragon14 package, `CharacterTestCharacterMotionSystem`, or the runtime-module schedule.
- `Docs/Evidence/LCC/LCC-M5-T3/PERFORMANCE_2026-08-26.md:1-24`: the report labels the result a measured contact chain while explicitly admitting independent witness/two-Sync timers are unavailable.
- `Docs/Evidence/LCC/LCC-M5-T0/AUDIT_2026-08-26.md:115`: the frozen performance contract requires both Hurtbox SyncAll stages, witness capture, query/build, pressure/apply and whole-frame p50/p95/max plus steady-state GC.
- `Docs/Guides/CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M5_T3_ACCEPTANCE.md:59`: the acceptance matrix repeats the 8 Dragon, warm 300 / measure 1800, Hurtbox sync, witness, query/build, pressure/apply and full-chain requirement.

**Reproduction**

Read `RunFrame`: timing starts immediately before the resolver and ends after contact-state apply. No Hurtbox system or motion/runtime module exists in the fixture. Therefore the reported `whole measured contact chain` is only the M5 resolver/apply slice, not the frozen full chain.

**Impact**

The current numbers do prove fixed 192-constraint resolver capacity and 0 B for that synthetic warmed slice. They do not prove the documented 8-Dragon formal full-chain budget or 0 B once both existing Hurtbox syncs and witness capture are included. Calling the missing stages an instrumentation limitation cannot satisfy a mandatory acceptance gate.

**Required disposition**

Blocks M5/T3 `Verified`. Add one bounded integration performance fixture or temporary profiler markers that measure the actual formal 8-Dragon runtime schedule. If separate stage timers are intentionally waived, the parent contract must first be explicitly revised and the actual full scheduled chain still needs a whole-frame timing/GC measurement; the current resolver-only number must retain its narrower label.

### 3.1 P1 closure check — both original blockers Closed

This was a bounded closure check of the exact two findings above, not a second broad independent review. No new production scope or review loop was introduced.

#### P1-1 Closed — real view, multi-frame motion and direct safety matrix

- PlayMode job `49385ded7f134ef58881d523976b9ad9` passed **5/5** M5/M4 tests.
- The corrected formal-scene fixture closes the main menu, instantiates formal Dragon14 and combat-girl prefabs as real `CharacterTestRuntimeCharacterView` instances, synchronizes them from authority entities, disables competing cameras and uses a tests-only evidence camera/light.
- Head, tail-mid and tail-tip produced bounded contact speeds and were consumed by the next motor tick: **75 mm**, **41 mm** and **74 mm** root displacement respectively.
- The direct M5 safety fixture covers moving head **4274 mm/s / 75 mm**, turning head **970 mm/s / 26 mm**, high-speed tail-tip clamped to **4401 mm/s**, static wall/corner sweep, and Severed tail contact **0** while head remains active with contact count **1 / 4270 mm/s**.
- Ordinary appendage contact keeps HP unchanged and does not increase typed `CombatHit` trace count.
- `LCC-M5-T3-Dragon14-Appendage-GameView.png` was re-inspected at original resolution. It now visibly contains Dragon14, the player and three green tests-only contact markers; it is no longer the pause-menu capture. The image is dense with existing Dragon/Gizmo layers, but it materially proves the submitted actors and marker relationship. `LCC-M5-T3-CharacterTestScene-DebugView.png` remains context-only and is not authority evidence.

Therefore the original invalid-visual and no-next-tick/motion-safety gaps are closed.

#### P1-2 Closed — formal full RuntimeModule timing and 0 B

- Focused/network/performance job `58457a8e727b4a8d8acd069dc937108f` passed **13/13**.
- `FormalV7EightDragonRuntimeModule_Warm300Measure1800_WholeTickAndStagesAreZeroBytes` loads the formal Dragon14 geometry v7 package, asserts exactly three animated sources, spawns **8 Dragon14 + 1 player**, warms **300** `RuntimeModule` ticks and measures **1800** ticks.
- Timing surrounds the real `RuntimeHost -> CharacterTestCharacterRuntimeModule.Tick` schedule. That schedule includes Motion and Hurtbox `SyncAll` #1, M4 authority pose plus derived-topology Hurtbox `SyncAll` #2, and M5 witness/resolve/apply.
- Whole RuntimeModule result: p50/p95/max **2125.7 / 3010.4 / 3964.0 us**, measured-thread allocation **0 B**, final frame **2101**.
- Existing `Motion` and `AuthorityPose` profiler recorder handles were valid, but their EditMode `LastValue` remained zero. The performance Evidence explicitly reports this instrumentation limitation and does not fabricate per-stage timing from those zero values. The whole scheduled-tick stopwatch measurement is the acceptance authority; the older synthetic 192-constraint resolver fixture remains narrower substage evidence.

Therefore the original resolver-only/full-chain gap is closed.

Closure support also includes narrow frozen regression job `4a23acc7da774d9096896eb7cb7a2255` at **39/39** and final Unity script gate `3fbfe6ceae0d4f00a72f8d799cf0f18a` with refresh/compile/domain reload observed, Editor ready and Console error count **0**.

## 4. P2 findings

### P2-1 — Presentation phase entity roles are reversed relative to the runtime constraint owner/target naming, and tests do not pin the IDs

**Files / lines**

- `Assets/Scripts/MxFramework/Game/CharacterTest/Runtime/Systems/Character/CharacterTestCharacterContactResolver.AnimatedAppendageSoftContact.cs:208-210,251-255`: `owner` is the entity that owns the authored animated Hurtbox; `target` is the character capsule being pushed.
- Same file `:299-304`: `AnimatedAppendageContactKey` is constructed as `(target.Id, owner.Id, ...)`.
- Same file `:782-788,878-896`: those values are published verbatim as `SourceEntityId` and `TargetEntityId`.
- `Assets/Scripts/MxFramework/Tests/Game/CharacterTest/CharacterTestAnimatedAppendageSoftContactTests.cs:247-263` and `CharacterTestAnimatedAppendageSoftContactNetworkTests.cs:51-53`: tests assert only phase counts/kind and never assert source/target entity identity.

**Reproduction**

For appendage owner entity 1 contacting pushed character entity 2, the fact is published as `SourceEntityId=2, TargetEntityId=1`, while the resolver's own constraint names entity 1 as Owner/source and entity 2 as Target. No current production consumer exposes the mismatch, which is why existing tests pass.

**Impact / disposition**

Does not alter the pressure result and no production Audio/VFX consumer is currently connected, so this is P2 rather than a second gameplay blocker. Before connecting phase facts to Audio/VFX, freeze the intended semantic names, either reverse the constructor arguments or rename the fact fields, and add exact Started/Stayed/Ended entity-ID assertions including the Severed N+1 case.

## 5. P3 findings and accepted limitations

### P3-1 — CharacterStudio full smoke did not reach the new Body Presence assertions

`Docs/Evidence/LCC/LCC-M5-T1/AUTHORING_RUNTIME_BINDING_2026-08-26.md` records an unrelated ActionSet fixture `TypeError` before the Body Presence smoke assertions execute. The focused Authoring test, RuntimeSpawn test, deterministic double compile and formal import provide adequate source/binding evidence for this milestone, so this does not block M5. Keep the full-Studio smoke limitation visible; do not claim that the entire Studio route passed.

### P3-2 — Production appendage Audio/VFX remains unsupported

`Docs/Interfaces/Audio.md:140` accurately states that M5 phases are not connected to a formal appendage cue or VFX. The current delivered player effect is soft motion pressure only. This is acceptable only if M5 is described as soft-contact movement with presentation facts, not as shipped audible/visual contact feedback. A later cue/VFX task must consume the corrected phase identity and remain PresentationOnly.

## 6. Contract audit checklist

| Review focus | Result |
| --- | --- |
| Hurtbox promoted to hard obstacle/navigation/projectile/global damage duty | **No finding.** `MotionObstacleLayerMask` remains static + dynamic character + movable object; M5 calls fixed-point separation directly for authored leaves only. |
| Unity Transform/Physics/Animator used as gameplay authority | **No finding.** M5 production files use package binding, deterministic entity transform and committed DamageGeometry shapes; no Unity presentation read exists. |
| Two SyncAll / no third sync | **Confirmed.** motion tick performs the first sync and post-authority-pose topology sync performs the second; M5 resolves after the second. |
| Same-tick witness / schema 34 / restore and source mismatch | **No P0/P1 found.** Witness arrays are fixed scratch state; physics contact velocity/count already use existing snapshot state; restore suppresses presentation edges and rebuilds derived topology. No schema 34 or private persistent witness cache was added. |
| Capacity/prevalidation/overflow before gameplay mutation | **No P0/P1 found.** 8 owners, 4 leaves, 9 roster and 256 directed constraints are fixed. Identity/separation/arithmetic checks precede pressure/contact-state mutation; overflow rejects the M5 build. |
| M1 pressure/mass/PushScale/CanBePushed/static sweep/direct displacement | **Confirmed.** Three pressure iterations, transfer scales, pushability and bounded accumulator are reused. No direct `SetTransform`/contact displacement path exists in production M5 code; the next motor tick retains static-sweep precedence. |
| Tail-mid zero-acceleration repair / unbounded velocity | **No unbounded path found; closure evidence complete.** Penetration bias and composed output are clamped to `MaximumComposedSpeedMillimetersPerSecond`; pressure scale and arithmetic remain checked. The direct closure fixture covers high-speed tail-tip and wall/corner safety. |
| v7 RuntimeSpawn/NetworkSession/BodyPresence compatibility | **No P0/P1 found.** v7 is additively accepted; legacy paths remain version-gated and future formats fail closed. Formal source/generated Dragon14 contains the exact head/tail-mid/tail-tip references. |
| Ordinary contact Damage/Trace and Severed behavior | **Passes.** M5 production code does not publish Damage/HitResolve/Trace. Closure PlayMode evidence keeps HP and typed `CombatHit` count unchanged, removes Severed-tail contact and retains head contact. |
| Playable/performance evidence overstated | **Closed.** The invalid Game View and resolver-only performance result remain historical evidence; section 3.1 records their valid real-view and full-RuntimeModule replacements. |

## 7. Accepted baseline limits

- The recorded six unrelated EditMode assertions and one legacy PlayMode ActionId assertion are not counted as M5 failures because the same aggregate reruns removed the v7/root-Presence M5 failures and dedicated M5 filters passed. They must remain disclosed and must not be described as a green full regression suite.
- Wing membranes, full-body animated soft contact, ragdoll, dedicated server/public transport, production appendage audio/VFX and detachable-tail art remain outside M5.
- Existing tests-only markers and screenshots are never production feedback or authority. A replacement screenshot/video is acceptance evidence only.

## 8. Final gate decision

The exact P1-1 and P1-2 rerun paths are closed. With **P0=0 and P1=0 open**, this review is **Ready for M5 Verified**. P2-1 remains a non-blocking phase-identity issue to resolve in the original T2 owner before any Audio/VFX phase consumer is enabled; P3-1/P3-2 and the section 7 limits remain accepted and must stay visible. No second broad independent-review loop is required.
