# LCC-M3-T2 implementation evidence — 2026-08-25

> Scope: stable Hurtbox leaf filtering/removal, typed unavailable geometry, independent topology witness, rebuild behavior, and the frozen focused gates.
>
> State: implementation and focused evidence complete; parent milestone status remains unchanged pending main-agent integration review.

## Implemented contract

- `CharacterTestCharacterHurtboxSystem.SyncAll` consumes T1 `GetBodyPartFilterStatus(bodyPartId)` against the original formal binding order. `Severed` leaves are omitted from the active compound write set, so the existing `EndCompoundColliderUpdate` seam removes the previously registered leaves. Remaining `CombatColliderId` values stay `CharacterHurtboxColliderIdStart + original binding ordinal`; no active-set compression or reindexing is performed.
- Dragon14 removes only `hurt.tail_base.capsule.1`, `hurt.tail_mid.capsule.0`, and `hurt.tail_tip.capsule.0`. The formal 18-leaf binding remains unchanged; the severed active set contains 15 leaves.
- A stable pose refit advances the per-sync `HurtboxRevision` but does not advance `CombatPhysicsWorld.TopologyRevision`. `CharacterTestDamageGeometrySnapshot.TopologyRevision` is sealed from the final shared-world topology witness independently of the animated Hurtbox revision.
- When every formal explicit leaf is inactive, the Hurtbox body remains registered with an empty compound, the old leaves are removed, and geometry is committed as `ExplicitLeavesInactive`. `TryGetDamageGeometrySnapshot` returns false while `TryGetDamageGeometrySnapshotState` returns the typed committed state; no compatibility whole-body AABB is created.
- `InvalidBinding` fails closed: the Hurtbox body is removed and geometry is committed as `InvalidBodyPartBinding`. The legacy compatibility AABB path remains only for packages with no explicit Hurtbox bindings.
- Clear/recreate and explicit BodyPartState reinitialization rebuild the active set from the unchanged formal binding plus current authority state. Restoring `Intact` restores the original collider IDs. Body Presence code and data were not modified.
- The existing Combat compound implementation already supports omission-based removal, zero leaves, topology rebuild on active-set changes, and refit on stable sets. No Combat core API or generic topology manager was added.

## Frozen focused results

| Gate | Raw result |
| --- | --- |
| Unity script validator | final job `55043d137af04f1aa27998961315f0bc`; refresh completed, compilation started/finished, domain reload observed, Editor ready, Console error count 0 |
| T2 focused fixture | EditMode job `6e84e34ad1984c8e9780a37fd1ca7307`; 5/5 passed, 0 failed/skipped, 0.8073431 s |
| Existing Hurtbox removal regression | EditMode job `f5330a52b5ab4d8e92d0598a7641d189`; 1/1 passed, 0 failed/skipped, 0.27569 s |
| Stable warmed NoAlloc | `SeveredTail_StableWarmedSync_AllocatesZeroBytes`; 64 warm syncs plus 512 measured syncs, stable 15-leaf snapshots, asserted 0 B; included in focused job `6e84e34ad1984c8e9780a37fd1ca7307` |

Focused fixture coverage:

- real Dragon14 RuntimeSpawn/package plus real `SyncAll`: 18 -> 15 leaves, exact frozen tail IDs absent, query count 18 -> 15, and every survivor retains its original binding ordinal ID;
- stable second sync before and after sever: topology witness unchanged while the animated Hurtbox revision remains independent;
- all explicit leaves inactive: body present, compound leaf count 0, typed `ExplicitLeavesInactive`, unavailable normal snapshot, zero query hits, and no compatibility AABB;
- invalid authored binding: typed `InvalidBodyPartBinding`, body removed, and zero query hits;
- Intact reinitialize restores all 18 original IDs; clear/new Hurtbox system rebuilds the severed 15-leaf set from binding plus state;
- isolated existing Combat seam: single-leaf omission, active-write order perturbation, stable query identity, and no topology rebuild for a repeated stable active set.

Non-acceptance attempts are retained for audit but are not counted as passing evidence: validator `6c85243b38dc4fcaba3ee3edc1cd9702` found one local-name collision; validator `a07d5ea8e4634f6fb07fd8e79cb41a02` found two test-only property-name errors; focused job `1b8c583ee00e46739baa60df94547f19` was 4/5 because the InvalidBinding test had not declared the expected fail-closed error log. Each was fixed only at the reported source/test site; no scope expansion followed.

## Files and ownership

- Runtime: `CharacterTestCharacterHurtboxSystem.cs`; topology/typed-state portion of `CharacterTestDamageGeometrySnapshot.cs`.
- Focused tests: `CharacterTestHurtboxTopologyTests.cs` and Unity-generated `.meta`.
- Documentation: this evidence file and the LCC-M3-T2 task execution record.
- Combat core, Body Presence, Zone/Anchor production behavior, action gating, presentation, schema/network, and all T3-T6 owners were not modified.

## T3 DamageGeometry handoff

- Use `TryGetDamageGeometrySnapshot` only for available active geometry. Use `TryGetDamageGeometrySnapshotState` when T3 must distinguish `ExplicitLeavesInactive` or `InvalidBodyPartBinding` from an uninitialized/missing snapshot.
- `ColliderCount`, bounds, `PartId`, `HitZoneId`, and collider lookup expose active leaves only. For a typed unavailable state the count is zero and bounds are zero; T3 must not synthesize compatibility geometry.
- Compare `TopologyRevision` for active-set topology changes and `HurtboxRevision` for animated pose freshness. Production `SyncAll` supplies the real Combat world topology witness; the legacy three-argument fixture commit deliberately carries witness 0.
- Zone/Anchor invalidation policy and any target/action response remain T3/T4 work. Formal snapshot/late-join behavior remains T6.

## Deliberate limits

- No full repository suite, PlayMode scenario, visual mesh/presenter validation, formal network/rollback test, or independent final milestone review was run for T2.
- The acceptance claim is limited to the default Unity validator, the five frozen T2 EditMode tests, and one existing Hurtbox body-removal regression. Parent milestone promotion and the single independent M3 terminal review remain outside this task.
