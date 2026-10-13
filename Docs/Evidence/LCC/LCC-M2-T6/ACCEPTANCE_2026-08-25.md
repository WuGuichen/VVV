# LCC-M2-T6 verified acceptance evidence — 2026-08-25

> Scope: T6 CharacterStudio, formal Dragon14 calibration, focused integration/performance evidence and direct browser/Game View capture.
>
> State: Verified. Main-agent reruns and the unique independent review closed with P0=0 and P1=0; the milestone authority documents promote LCC-M2 to Verified.

## CharacterStudio

- The Current document surface supports create/edit/delete, all T1 fields, member mapping, validation display, independent dirty state, Undo/Redo, save/reopen, ordinary refresh recovery and draft-only up/down reorder. The UI explicitly says that save canonicalizes by `partStateId`; it does not claim persistent user order.
- A BodyPart owned by another state is visibly disabled. `normalizeOptions`, select rendering and checkbox rendering preserve the disabled flag.
- Dedicated load/save calls are `GET /api/character/body-part-damage` and `POST /api/character/body-part-damage/save`. Top-level package save calls the same dedicated transaction for a dirty body-part draft; `/api/character/save` remains preservation-only and is not a second writer.
- Missing requires explicit creation. Future/Invalid render the exact raw payload without create/edit/save controls. This fail-closed branch is covered by source/smoke; no temporary future-schema package was introduced into the formal Authoring tree for a browser screenshot.
- A Current document requires at least one definition. Deleting the last card may remain a recoverable UI draft, but save is blocked by the T1 typed issue `CHARBPD016_EMPTY_DEFINITIONS`; RuntimeSpawn independently rejects v5 Authored definition counts outside `[1,8]` as `CHARBPD101_INVALID_BINDING`.
- Ordinary `刷新状态` was exercised with Head `authoredPriorityDeltaOpen` changed from 150 to 151: value, dirty badge, Undo availability and selection survived reload. Undo returned 150/clean, Redo returned 151/dirty, and final Undo returned 150/clean.
- Clicking explicit discard while dirty did not silently discard when the browser session did not accept a native confirmation. The accepted-confirmation clear/reload branch is source/smoke covered; it was not claimed as a completed direct-browser confirmation run.
- Draft reorder → Undo → Redo → T1 save/reopen completed in the browser. The reopened order was `head | left_wing | right_wing | tail`, and the success subtitle explicitly reported canonical normalization. This browser transaction occurred before the final threshold calibration; its before/after SHA-256 `7408C2B25D907EB8A5F073E174F18F2CDB889FCB1A522329453859FBD6884E3E` proves that transaction did not rewrite the then-current file, not the identity of the final calibrated asset.
- The service reopened Dragon14 as `Current`, write-eligible, four definitions and ten member mappings. After the approved calibration and formal generation, the current authoring-file SHA-256 is `CCC85EC37FEF549B6A67D3C0ABF8937780550B5C79A1CEC145A175E6975F924B`, and generated body-part content identity is `sha256:9e7ba22de3fb2741850363cfe371844456a73401714bae5e3b0d082636158d1d`.

### Direct browser captures

| Evidence | Direct observation |
| --- | --- |
| `characterstudio-000003-body-part-standard-1600x1000.png` | 1600×1000, four cards, each card `clientWidth=scrollWidth=324`; document and inspector had no horizontal overflow. |
| `characterstudio-000003-body-part-narrow-820x1000.png` | 820×1000, four stacked cards, each card `clientWidth=scrollWidth=764`; document HTML `clientWidth=scrollWidth=814`, no horizontal overflow, save control reachable by ordinary vertical scroll. |
| `characterstudio-000003-body-part-member-mapping-820x1000.png` | Narrow edit form exposes CRUD/reorder, canonical-order note, checked Head members, and wing/tail members visibly disabled because another definition owns them. |

These captures predate the final numeric calibration. They remain direct evidence for four-group UI structure, member ownership, reorder disclosure and responsive layout; any threshold values or document hash visible in them are not claimed as the final asset values.

Absolute paths:

- `C:\UnityProjects\WGameFramework\Docs\Evidence\LCC\LCC-M2-T6\characterstudio-000003-body-part-standard-1600x1000.png`
- `C:\UnityProjects\WGameFramework\Docs\Evidence\LCC\LCC-M2-T6\characterstudio-000003-body-part-narrow-820x1000.png`
- `C:\UnityProjects\WGameFramework\Docs\Evidence\LCC\LCC-M2-T6\characterstudio-000003-body-part-member-mapping-820x1000.png`

### Smoke

- `C:\Users\WGH\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe Tools/MxFramework.CharacterStudio/scripts/smoke.mjs`
- Result: exit 0, `CharacterStudio smoke ok`.
- The smoke covers Current/Future normalization, Dragon14 4-definition/10-member fixture, independent dirty tracking, CRUD/reorder and canonical-order disclosure, disabled cross-owner members, every T1 field, the Current non-empty typed-validation boundary, dedicated GET/POST and sole-writer preservation, Undo/session/refresh/discard gates, cache busting and responsive styles.
- Node syntax checks passed for the changed CharacterStudio entry/state/session/api/context/inspector modules and smoke script.
- `check_code_standards.py --all --summary-only` completed over 3447 first-party files. It remained report-only with 153 existing findings; the reported top files are historical `ZString`/Story/VFX/Runtime AI Planner/Toast files, not either T6 test file.

## Formal Dragon14 calibration

The initial 60-damage bridge sample was not a valid HP-lifecycle calibration. Formal CharacterTest HP is 100, and the current authoritative `CharacterActionSource/Libraries/Common/character_actions.json` uses `light_attack` Attack scale `2.65`; the embedded fallback's older `0.65` value is not the formal source. T6 therefore recalibrated only thresholds in the authoring truth, retained all damage/priority multipliers, and regenerated the formal package:

| Part state | Wound capacity | Max integrity | Open scale | Integrity scale |
| --- | ---: | ---: | ---: | ---: |
| Head | 34 | 68 | 1250 | 1000 |
| Left Wing | 51 | 85 | 1150 | 1000 |
| Right Wing | 51 | 85 | 1150 | 1000 |
| Tail | 68 | 90 | 1200 | 1000 |

`character import-unity --check-files --check-hashes --build-target StandaloneWindows64` exited 0 with `status=WarningOnly`, `canWrite=True`, `canSpawn=True`, 49 unchanged artifacts and 0 conflicts/errors. The only geometry warning remained the pre-existing `CHARPRES013_ROOT_CAPSULE_CENTER_XZ_NOT_CONSUMED`; the generated v5 binding contains the four calibrated definitions and ten-member lookup.

The formal Head chain uses the actual default main-hand binding, actual action configs, live Attack/Defense attributes, `CharacterTestCombatDamageResolver`, the accepted BodyPart bridge/state, and `CharacterTestCombatDamageApplier` rather than a copied formula or manual HP mutation:

1. Frame N `light_attack` (`2.65`) hits formal collider `hurt.head.obb.0`, whose actual weak-point override is `1.25`. The production resolver returns 66; the applier moves HP `100 → 34`, Head becomes Open with Integrity 2, and the target remains Alive with no death event.
2. Frame N+1 formal `light_attack_2` (`0.65`) resolves 21 on the same collider. Its frame-start Head state is Open, so BodyPart scaling produces 26; the applier moves HP `34 → 8`, Head becomes Broken, and the target is still Alive. Open and Broken each project exactly once; the fixture intentionally has no break reaction id.
3. Frame N+2 the same real follow-up resolves/scales to 26, HP reaches 0, lifecycle reaches `Dead`, and exactly one death event is emitted. Thus Broken is demonstrated before death, while death remains owned by the production HP/lifecycle chain.

The second formal PlayMode test uses four actually hittable Dragon14 collider members: `head`, `left_wing_membrane`, `right_wing_membrane`, and `tail_tip`. Each isolated world runs one formal `light_attack_2` through resolver → applier → accepted state; final damage is 21 for the 1.25 Head weak point and 17 for the three 1.0 colliders. Only the target definition changes. The frozen root members `left_wing_root` / `right_wing_root` are additionally asserted to map to their wing definitions, but are not misrepresented as dedicated playable colliders.

Same-authority-frame immutable-view behavior remains covered by the existing focused bridge/runtime suite. The formal HP=100 fixture intentionally does not fabricate a second same-frame `light_attack`: two current 66-damage Head hits cannot remain alive. Typed Focus adapters/lifecycle/source-policy and non-empty reaction dispatch likewise remain covered by the existing focused tests.

## M1 Body Presence compatibility regression closure

The first final-regression run `7dfb16ffac4b471faf7c8a2a0e9af1ba` exposed a real compatibility seam after Dragon14 moved to geometry binding v5: `CharacterImportedPackageJson` retained the authored Presence fields, but `CharacterTestCharacterBodyPresenceSystem.ValidateBinding` still accepted only the exact v4/4.0 envelope. The formal Dragon14 binding was therefore classified Invalid, producing `SourceUnavailable` instead of projecting its three Presence leaves.

The runtime gate now preserves the existing strict v4 Authored path and applies the same volume/revision/hash validation to strict v5 Authored bindings. A v5 `CompatibilityHardRoot` binding is treated as Missing only when revision is 0, content hash is empty and volumes are empty; malformed compatibility state remains Invalid, and v6 remains an unsupported future binding. No contact, motion, geometry or test expectation changed.

Focused EditMode job `5479c3d55ae24b0481b04070d6a83115` passed 2/2 for the v4/v5/Authored/Compatibility/future boundary. The original three-test PlayMode class then passed 3/3 in job `84423f03eff940ffadba18d271de7854`, including formal Dragon14 translation/lifecycle/rollback, four-layer diagnostics/appendage exclusion/natural attack, and static-obstacle isolation. Unified script validation job `e1a59fee64f841f9bbaabb7d5bad6620` observed refresh, compilation, domain reload and Editor ready with Console error count 0.

## Unity jobs

| Job | Filter / purpose | Result |
| --- | --- | --- |
| `e1a59fee64f841f9bbaabb7d5bad6620` | final script refresh/compile/domain reload/editor-ready validation after the v5 Presence seam fix | passed; Console error count 0 |
| `84423f03eff940ffadba18d271de7854` | `CharacterTestLargeCharacterPresencePlayModeTests` | PlayMode 3/3 passed, 0 failed/skipped, 2.305 s |
| `5479c3d55ae24b0481b04070d6a83115` | focused Body Presence runtime v4/v5/Compatibility/future boundary | EditMode 2/2 passed, 0 failed/skipped, 0.331 s |
| `3a228feb0bae4dccbbe6f3ffb5c72df6` | `CharacterTestDragon14BodyPartDamagePlayModeTests` | PlayMode 2/2 passed, 0 failed/skipped, 0.630 s |
| `a70db0ae1c4341b880b56f0b22a6731c` | `CharacterBodyPartDamageRuntimeBindingTests` | EditMode 4/4 passed, including v5 empty-definition fail-closed and final Dragon14 hash |
| `6c603f9a7b0345d0b3520a3fb5a2cb9e` | `EightCharacters_StatePreHitDamageAndSelector_Warmed4096IterationsAllocateZeroBytes` | EditMode 1/1 passed, 0 failed/skipped, 0.199 s test / 0.466 s job |
| `9e382b0a5963413ca7c731bad1cc4735` | existing BodyPart Integration + StateRuntime + ZoneSelection + StateNetwork focused classes | EditMode 59/59 passed, 0 failed/skipped, 2.903 s |

### Main-agent final rerun

| Job | Filter / purpose | Result |
| --- | --- | --- |
| `f4b4ff25b5a24395ba4fe7e98a66d562` | final Unity refresh/compile/domain reload/editor-ready validation | passed; Console error count 0 |
| `f7c8616b42334a7dbb7ab9bb417c0eb9` | four M2 BodyPart focused classes | EditMode 59/59 passed |
| `20781d53c61c4671b6b49a6b3c167ecd` | RuntimeSpawn body-part binding | EditMode 4/4 passed |
| `553f7d1297f74fad8491402dbd9b245b` | Presence authority plus two catalog replay identities | EditMode 13/13 passed |
| `703efd340cba41738ab40c941f08f760` | eight-character 4096-iteration NoAlloc | EditMode 1/1 passed |
| `30f2cd691c0f47fd820c07f449aaca55` | formal Dragon14 body-part chain and four-group isolation | PlayMode 2/2 passed |
| `ebbb74aeb7d049f1b7896c85e6944c73` | v4/v5/Compatibility/future Presence boundary | EditMode 2/2 passed |
| `52cc88fb5eb64ae7a75f8ae8ef367d14` | existing Presence lifecycle/diagnostics/natural-attack aggregate | PlayMode 3/3 passed |
| `af0c9482b3804fc5b551d88f50211ff2` | standalone Dragon14 natural attack | PlayMode 1/1 passed |

Authoring `character-body-part-damage` tests passed after adding the Current non-empty validator/save rollback cases. CharacterStudio smoke and changed-file Node syntax checks also exited 0. Diagnostic PlayMode jobs `babc258e2d234f0bb56c03fe1d7a14ba` and `c7e746e57fbe4e438449116f784be7eb` were intentionally superseded: the first exposed the stale 0.65 calibration assumption (actual Head result 66), and the second showed the actual lifecycle reaches `Dead` in the death-owning applier call. Neither is counted as passing evidence.

The 59-test focused run is the evidence for frame-start/next-frame semantics, HP-after-accepted ordering, full source-policy exclusions, typed Focus ingress/lifecycle, telemetry/reaction projection, dual-axis terminal/respawn behavior, dynamic Focus/priority/lock, legacy no-definition behavior, schema 30 canonical bytes/hashes, rollback, replay, late join and definition-source mismatch. T6 did not duplicate those scenarios in a second harness.

The new allocation fixture uses eight distinct target entity ids (`2..9`) sharing one `CombatPhysicsWorld`. Each iteration executes fixed-capacity state access, the actual pre-hit `CharacterTestBodyPartDamageAuthorityBridge`, accepted-damage staging/commit, and the existing selector. After 32 warmups, 4096 measured iterations across all eight characters allocated 0 B on the measured thread; every character accumulated 4128 sample damage facts and retained the selected `zone.beta` lock.

## Direct Game View

- `C:\UnityProjects\WGameFramework\Docs\Evidence\LCC\LCC-M2-T6\character-test-000003-gameplay-1600x900.png`
- Captured from the formal `Assets/Scenes/CharacterTestScene.unity` Game View after invoking the same main-menu Start command path. It directly shows Dragon14 and the player character present in the playable scene.
- This framebuffer is only direct playable/visual evidence. It does not show an Open/Broken state overlay; the numerical Open/Broken and frame-boundary claims come from the formal generated-package PlayMode job above.
- Unity exited Play Mode afterward; the scene remained unsaved/clean and a post-Play Console query returned 0 errors.

## Final independent review

- The unique read-only final review recorded P0=0 and P1=0, so T6 and M2 may be promoted to Verified.
- Accepted P2: Focus consume has a typed ingress and focused fact tests but no production Action/Ability/Interaction caller. All formal tags are empty; a non-empty formal tag requires a real accepted-fact adapter plus formal PlayMode before any player-facing claim.
- Accepted P3: the fixed production playable catalog is unique, but the current duplicate-root guard compares only adjacent entries after character-id-first sorting. External, dynamic or Mod catalogs require a normalized-root set and a non-adjacent duplicate test.
- The accepted branch of the native explicit-discard confirmation and a direct-browser Future fixture were not captured; their fail-closed behavior is covered by source/smoke and is recorded here rather than overstated as direct browser evidence.
- The three browser captures predate final threshold calibration; they prove UI behavior/layout only. Final numeric and hash claims come from the authoring truth, generation report and focused Unity jobs above.
- No Severed topology/action invalidation, Hurtbox topology changes, root-XZ/GeometryInReach/Approachable/pose-path changes, generic serializer/transaction/UI abstraction, BigInteger, or alternate formal storage was added.
