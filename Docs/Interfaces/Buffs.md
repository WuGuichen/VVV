# Buff v2 Interface

> Status: Current

Buff v2 is no longer the old `MxFramework.Buffs` object lifecycle model. The old `IBuff`, `BuffBase`, `IBuffTarget`, `IBuffFactory`, `IBuffPipeline`, `BuffPipeline`, `BuffSnapshot`, `ConfigBuffFactory`, external Buff editor runtime data, WGame `BuffType`, and preview `DamageByAttr` path are retired.

The current source of truth is component-native Gameplay state:

| Type | Purpose |
| --- | --- |
| `GameplayBuffId` | Stable positive runtime buff id value. |
| `GameplayBuffDefinition` | Registry-owned definition: default duration, max stack count, apply stack count, stack policy, permanent flag, and effects. |
| `GameplayBuffDefinitionRegistry` | Deterministic sorted definition lookup and definition hash input. |
| `GameplayBuffEffectDefinition` | v2.0 effect definition. Currently supports `AttributeModifier` and `Status`. |
| `GameplayBuffApplicationRequest` | Transient request to apply or remove a component buff. |
| `GameplayBuffRequestCorrelation` | Optional flat numeric request identity: request id, operation index, and operation kind id. |
| `GameplayBuffRequestBuffer` | Legacy frame-local queue plus an independent fixed-capacity reservation lane consumed by `GameplayBuffApplicationSystem`. |
| `GameplayBuffRequestReservationToken` | Opaque generation-safe reservation handle; callers cannot construct a live token. |
| `GameplayBuffApplicationResult` / status / reason | Flat numeric terminal result for one correlated request. |
| `IGameplayBuffApplicationResultSink` | Optional no-throw/no-fail result sink invoked exact-once for each correlated terminal request. |
| `GameplayComponentBuffEntry` | Active buff state: `BuffId`, `StackCount`, `StartFrame`, `EndFrame`, `IsPermanent`, `SourceId`. |
| `GameplayComponentBuffSetComponent` | Deterministic sorted active buff set per `GameplayEntityId`. |
| `GameplayBuffCommandSystem` | Converts `RuntimeCommand` payloads into buff requests. |
| `GameplayBuffApplicationSystem` | Applies, refreshes, stacks, replaces, or removes active buff entries from requests. |
| `GameplayBuffEffectProjectionSystem` | Projects active buff state into source-aware attribute modifiers and statuses. |
| `GameplayComponentBuffCleanupSystem` | Removes expired active buff entries. |
| `GameplayComponentBuffSchemaDescriptors` | Diagnostics, runtime hash, and SaveState adapters for active buff state. |
| `GameplayBuffDefinitionJsonImporter` | Imports generated `buff_definitions.json` into `GameplayBuffDefinitionRegistry`. |
| `GameplayBuffDefinitionBinaryImporter` | Imports generated `buff_definitions.bytes` into `GameplayBuffDefinitionRegistry` for Player-first loading. |
| `GameplayBuffPresentationDefinitionRegistry` | Non-deterministic presentation metadata lookup for HUD/head pad display. |
| `GameplayBuffPresentationSnapshotBuilder` | Builds read-only UI snapshots from active buff state, runtime definitions, and current frame; use `BuildInto` for caller-owned buffers on hot paths. |
| `GameplayBuffPresentationDefinitionJsonImporter` | Imports generated `buff_presentation_definitions.json` into `GameplayBuffPresentationDefinitionRegistry`. |
| `GameplayBuffPresentationDefinitionBinaryImporter` | Imports generated `buff_presentation_definitions.bytes` into the same presentation registry. |

## Commands

`GameplayRuntimeCommandIds.ApplyComponentBuff = 1001010`

Payload:

- `targetId`: target component entity index
- `payload0`: target component entity generation
- `payload1`: buff id
- `payload2`: reserved, must be `0`
- `sourceId`: optional source context id
- `traceId`: diagnostics correlation

`GameplayRuntimeCommandIds.RemoveComponentBuff = 1001011`

Payload:

- `targetId`: target component entity index
- `payload0`: target component entity generation
- `payload1`: buff id
- `payload2`: must be `0`
- `sourceId`: optional source id
- `traceId`: diagnostics correlation

## Reserved Requests And Terminal Results

`GameplayBuffRequestBuffer(reservationCapacity, legacyPrewarmCapacity)` adds a bounded lane without changing the parameterless legacy queue contract. `TryReserve(...)` validates and reserves both one invisible slot and one future visible-order ordinal. `AbortReservation(...)` is idempotent; `CommitReservation(...)` is no-fail/exactly-once for a live token and makes the request visible with its supplied `GameplayBuffRequestCorrelation`. Default, stale, foreign, duplicate and already-consumed tokens are no-ops. Prepared work is excluded from `Count`, snapshots and drain; committed and legacy requests share one monotonic visible order based on legacy enqueue or reservation commit time.

Ordinal wrap is forbidden. Prepare reserves ordinal capacity up front; when `long.MaxValue` is exhausted while any legacy/prepared/committed work remains, reservation rejects and legacy enqueue throws. The ordinal resets to `1` only after the buffer is completely empty or at an exclusive `Clear()` boundary. `Clear()` also invalidates all live reservations and is intended only for reset/restore composition code, never concurrently with producers or consumer drain.

The six-argument `GameplayBuffApplicationRequest` constructor and default correlation preserve the previous equality and hash result exactly. Correlated Apply/Remove requests receive one `GameplayBuffApplicationResult` at their target-owned terminal branch; uncorrelated legacy requests preserve their existing event-only behavior and publish no typed result. Success reason values are `Applied=1`, `Refreshed=2`, `Removed=3`, and idempotent absent-target-buff `RemoveNoOp=4`. Rejection values are `MissingComponentWorld=1001`, `MissingComponentEntity=1002`, `MissingBuffDefinition=1003`, and `DurationOverflow=1004`.

The result payload contains only frame, request kind, target, Buff/source ids, numeric correlation, status/reason, and old/new stack/end-frame values. It never embeds the original request, strings, references or callbacks. `IGameplayBuffApplicationResultSink.Publish(...)` must not throw, retry, mutate authority outside its owned transport, or turn presentation overflow into Gameplay failure; an unexpected exception remains a fail-fast invariant violation.

Allocation contract: reservation prepare/abort/commit, a prewarmed drain, and result publish are `NoAlloc after initialization`. `GameplayComponentBuffSetComponent` remains an immutable sorted value container, but stores the common zero/one-Buff case inline. Apply/Refresh of a single active Buff therefore does not create replacement arrays; sets containing two or more active Buffs use one copy-on-write replacement array when their contents change. Bridge-owned validation/reservation/result relay adds no per-Tick allocation above the target Gameplay path.

## Effects

Buffs do not directly mutate attributes or statuses. A buff stores deterministic state only. `GameplayBuffEffectProjectionSystem` derives effects from active buff entries:

- `AttributeModifier`: creates a `GameplayComponentModifierEntry` with `SourceBuffId = BuffId`; the value is `AddValue * StackCount`.
- `Status`: creates a `GameplayStatusEntry` with `SourceBuffId = BuffId`.

When a buff expires or is removed, the projection system removes only source-owned modifier/status entries. Non-buff modifiers and statuses are preserved.

## Presentation

Buff presentation is a read-only layer over deterministic buff state. It does not add commands, runtime events, hash input, save data, or effect logic. A composition root gives `GameplayBuffPresentationSnapshotBuilder`:

- the target `GameplayEntityId`
- `GameplayComponentWorld`
- `GameplayBuffDefinitionRegistry`
- optional `GameplayBuffPresentationDefinitionRegistry`
- current frame
- requested surface (`PlayerHud`, `TargetHud`, or `HeadPad`)

The builder outputs sorted `GameplayBuffPresentationItem` rows containing `buffId`, `displayName`, `shortName`, `stackCount`, remaining frames, normalized remaining time, permanence, polarity, icon resource key, effect summary, and priority. Missing presentation metadata falls back to `GameplayBuffDefinition`: display name / stable id, generated short name, inferred polarity, and effect summary.

Allocation contract:

- `Build(...)` is `AllocByDesign`: it returns a new array for low-frequency diagnostics, tests, or tooling.
- `BuildInto(...)` is the hot-path entry: callers provide `List<GameplayBuffPresentationItem>` output and `List<GameplayComponentBuffEntry>` scratch buffers. It is `NoAlloc after initialization` when buffers have enough capacity and presentation metadata avoids fallback string generation; invalid-call and fallback-metadata paths may allocate diagnostics or display strings.

CharacterTest consumes these snapshots only in presentation code: the FairyGUI Combat HUD displays player and target Buff/Debuff/Aura icon strips, while character head pads show up to four high-priority small status badges with short names such as `DEF+` / `DEF-`, polarity colors, stack count, and a lightweight remaining-time bar. HeadPad badges are a filtered combat readout and do not replace the full Combat HUD Buff list. Runtime Buff application, stack/duration behavior, projection, hash, and SaveState are unchanged.

## Duration And Stack Semantics

`BuffId` is the complete Buff identity. Duration, stack policy, stack count per apply, max stack count, permanent flag, and effects come only from `GameplayBuffDefinition`. Commands, hit events, projectiles, and auras cannot override duration or source id.

The active store keeps one `GameplayComponentBuffEntry` per `(target, BuffId)`. Multiple sources applying the same BuffId update that single entry and record the most recent source context id. v1 stack state is aggregated: all stacks share one `EndFrame`, and expiry removes the whole group.

- `RefreshDuration`: does not increase stacks; `EndFrame = currentFrame + defaultDurationFrames`; updates most recent source context.
- `Replace`: resets `StartFrame`, `StackCount = applyStackCount`, `EndFrame = currentFrame + defaultDurationFrames`; updates most recent source context.
- `Stack`: `StackCount = min(maxStackCount, current + applyStackCount)`; applying at max stacks still refreshes `EndFrame = currentFrame + defaultDurationFrames`; updates most recent source context.

## System Order

The intended default order for the v2 chain is:

```text
Command:    GameplayBuffCommandSystem
Resolution: AuraSourceCleanup(40)
Resolution: BuffExpireCleanup(42)
Resolution: CombatAuraRefresh(45)
Resolution: BuffApplication(50)
Resolution: BuffEffectProjection(52)
Resolution: CombatDamageApplication(60)
```

This keeps expired sources from refreshing, lets aura refresh submit apply requests before buff application drains them, and ensures attribute projections are available before combat damage reads attributes.

`CombatOnHitBuffApplicationSystem` is not part of the pre-damage aura chain. A composition root should run it only after accepted hit results are available, then run `GameplayBuffApplicationSystem` and `GameplayBuffEffectProjectionSystem` if on-hit effects must become visible in the same frame.

## Aura Integration

`CombatGameplayAuraBuffAbility` creates a caster-following `GameplayComponentAuraSourceEntry`. `CombatGameplayAuraBuffSystem` queries `CombatPhysicsWorld.QuerySphere`, filters targets through `CombatEntityGameplayMap` and `GameplayComponentTargetingService`, and enqueues buff apply requests. It does not directly write the buff store.

CharacterTest `Guard Aura` is the first runtime slice:

- `AuraId = 501008`
- `BuffId = 601008`
- source duration `300` frames
- Buff definition default duration `2` frames
- radius `4m`
- relation `SameTeam`
- includes caster
- effect: `Defense +5`

Leaving the aura range or ending the aura source does not send an immediate exit event. The target buff simply stops receiving apply requests and is removed by cleanup after its definition duration expires.

## On-Hit Combat Integration

`CombatOnHitBuffApplicationSystem` is the v0 Combat bridge for applying Buffs from Damage-kind hits. It reads `HitResolveResult` rows plus the matching `CombatHitEventConfig.OnHitBuffs`, maps the target through `CombatEntityGameplayMap`, and enqueues `GameplayBuffRequestBuffer.EnqueueApply(...)`. It never writes `GameplayComponentBuffSetComponent` directly and does not validate Buff definitions; missing definitions are rejected by `GameplayBuffApplicationSystem`.

`CombatHitBuffApplicationConfig` fields:

- `buffId`: required positive Buff definition id.

The hit reference intentionally does not carry duration, stack, source override, or effect data. `BuffId` is the complete Buff identity; if a hit needs a different duration or effect, author a different Buff definition id. The source context comes from the accepted hit result.

Trigger boundary: v0 triggers for `HitResolveKind.Damage` rows that carry configured `OnHitBuffs`. `AcceptedDamageCount` remains a telemetry counter for `HitResolveResult.IsAcceptedDamage` (`Damage > 0`) only, so zero-damage control hits can enqueue Buffs without being counted as accepted HP damage. `Evaded`, `Blocked`, `Friendly`, `Duplicate`, `SelfDamage`, dead/invincible/parried, and other non-Damage results do not enqueue Buffs.

CharacterTest includes a test Buff slice:

- `BuffId = 601009`
- stable id `buff.character_test.on_hit.defense_break`
- default duration `300` frames
- effect: `Defense -3`

## Authoring v0

Buff v2 authoring starts from package-local JSON:

```text
Tools/MxFramework.Authoring/samples/buff-character-test/config/buff_authoring.json
```

Schema version `1` contains:

- `buffs`: `buffId`, `stableId`, `displayName`, player-facing multi-type `types`, `defaultDurationFrames`, `maxStackCount`, `applyStackCount`, `stackPolicy`, `isPermanent`, and `effects`.
- `effects`: v0 supports `AttributeModifier` and `Status`, mapping directly to `GameplayBuffEffectDefinition`.
- `buffs[].presentation`: optional HUD metadata: `shortName`, `description`, `iconResourceKey`, `priority`, `showOnPlayerHud`, `showOnTargetHud`, and `showOnHeadPad`. Generated presentation `polarity` remains a compatibility UI field and is inferred from `types`.
- `auras`: `auraId`, `buffId`, `radiusMeters`, `layerMask`, `relationFilter`, and `includeCaster`, mapping to `CombatGameplayAuraBuffDefinition`.

The Authoring CLI owns validation and stable compile output:

```bash
mx-authoring buff validate --package <BuffPackage>
mx-authoring buff compile --package <BuffPackage> --out <dir>
mx-authoring buff import-unity --package <BuffPackage> --project-root <repoRoot> [--dry-run]
```

Unity import writes:

- `Assets/MxFrameworkGenerated/Gameplay/Buffs/buff_definitions.json`
- `Assets/MxFrameworkGenerated/Gameplay/Buffs/buff_definitions.bytes`
- `Assets/MxFrameworkGenerated/Gameplay/Buffs/buff_presentation_definitions.json`
- `Assets/MxFrameworkGenerated/Gameplay/Buffs/buff_presentation_definitions.bytes`
- `Assets/MxFrameworkGenerated/Combat/Auras/aura_buff_definitions.json`
- `Assets/MxFrameworkGenerated/Combat/Auras/aura_buff_definitions.bytes`

JSON is the authoring, diff, debug, and compatibility format. The `.bytes` files are versioned Player runtime artifacts generated by the same CLI compile/import path. CharacterTest `Guard Aura` loads generated Buff, presentation, and Aura bytes first, then JSON, and falls back to embedded definitions only when generated catalogs are missing or empty.

CharacterTest Player builds mirror the Buff, Buff Presentation, and Aura Buff generated JSON / `.bytes` files under `Assets/StreamingAssets/MxFramework/CharacterTest/RuntimeCatalogs/...`. Editor and tests prefer `Assets/MxFrameworkGenerated/...`; Player prefers `Application.streamingAssetsPath` mirror and only then tries the generated path. The CharacterTest Player warm-run report lists these expected catalog kinds in `runtimeCatalogLoad.*` and sets `runtimeCatalogLoad.allExpectedBinary=true` only when all expected catalogs loaded from `.bytes`.

Binary catalog loading is `LowFreqAlloc` during startup / runtime bootstrap. Registry lookup, Buff application, Buff projection, presentation `BuildInto`, and Aura refresh hot paths keep their existing allocation contracts. A present but invalid `.bytes` file produces explicit diagnostics before JSON fallback.

## Boundaries

v2.0 does not include DOT, periodic heal, fixed ground auras, generalized Web editing UI, external legacy Buff authoring compatibility, WGame `BuffType`, or old Buff data migration. The v0 presentation layer is HUD/head pad metadata and snapshots only; 3D attached VFX, material swaps, audio, and screen-edge warnings should use a later presentation/VFX bridge. DOT/heal should later produce Damage/Heal requests into the shared combat resolution path instead of mutating HP inside a Buff tick.

Projectile behavior is not part of Buff. CharacterTest projectile skills use `MxFramework.Combat.Projectiles` and projectile events.
