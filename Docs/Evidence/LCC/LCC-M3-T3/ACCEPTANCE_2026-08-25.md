# LCC-M3-T3 implementation evidence — 2026-08-25

> Scope: topology-derived DamageZone availability, exact lock invalidation,
> Anchor/Planner exclusion, reservation release ordering, and focused gates.
>
> State: implementation and focused evidence complete; parent milestone status
> remains unchanged pending main-agent integration review.

## Implemented contract

- `CharacterTestDamageZoneSnapshot` now retains every authored Zone identity.
  A Zone with no active Hurtbox member has `IsAvailable == false`, zero members,
  and zero bounds; `TryGetAvailableZone` is the common runtime eligibility gate.
  An authored `ExplicitLeavesInactive` geometry state therefore yields a valid
  authored Zone view with zero available Zones instead of a legacy synthetic
  whole-target Zone. `InvalidBodyPartBinding` remains fail-closed.
- The snapshot records `TopologyRevision` only as a diagnostic witness captured
  from the geometry used for that Zone build. `ZoneRevision` hashes binding,
  authored identity, availability, and active member identity; it deliberately
  excludes the shared Combat world's global topology revision. A later actor's
  topology change therefore cannot invalidate an unrelated actor or Zone by
  global revision alone.
- A previous targeting lock is current only when target identity, resolved
  action revision, binding revision, locked authored Zone, hit zone, and logical
  Hurtbox member still match an active member. Dragon14 tail removal makes an
  old tail lock stale and reselects through the existing opportunity/hysteresis
  ordering, while an exact surviving head lock remains current.
- The independent selector, single-Zone Attack Opportunity evaluation, joint
  Zone projection, Engagement Planner, and reservation authority all reject an
  unavailable Zone. Authored `anchor.tail.rear` remains in the immutable binding
  but its `zone.tail` dependency makes it runtime-unavailable after severance.
- `CharacterTestRuntimeAiJointRuntime.Select` invokes the existing
  `ReservationAuthority.SweepInvalid` once before actor/action/Zone candidate
  collection. Stale tail capacity is released in the same planner frame; an
  unrelated `zone.core` reservation remains active and its canonical capacity
  slot is recomputed deterministically.
- Reservation currentness no longer compares the aggregate `ZoneRevision`.
  It validates the exact authored Zone's availability and approach eligibility,
  so Dragon14 `zone.core` may lose `tail_base` while remaining valid from chest
  members. Binding, Anchor, phase, graph, action, physics, owner, target, and
  renewal witnesses remain unchanged.
- Root-XZ distance, `GeometryInReach`, legal-Zone definition, CandidatePose,
  path-cost calculation, comparison ordering, and authored Zone/Anchor data were
  not changed.

## Authority order

The tested order is BodyPartState commit -> T2 Hurtbox active-leaf sync -> Zone
view rebuild -> final Combat world-revision seal -> joint reservation sweep ->
candidate collection/selection. `TopologyRevision` is diagnostic; exact Zone
availability/member identity drives T3 invalidation.

## Frozen focused results

| Gate | Raw result |
| --- | --- |
| Unity script validator | final job `aeea777391eb4cdca9769728c58852c1`; refresh completed, compilation started/finished, domain reload observed, Editor ready, Console error count 0 |
| T3 focused fixture | EditMode job `8a5af3a31f024646ac1f68c1fe6ce9cf`; 5/5 passed, 0 failed/skipped, 1.2358639 s |
| Existing CD-M2/CD-M3 regression | EditMode job `5a2a1ff46ef84d448b5b934d1e964f4d`; 26/26 passed, 0 failed/skipped, 0.3440313 s |
| Warmed NoAlloc | `TargetingInvalidation_WarmedSelectorAndJointProjectionAllocateZeroBytes`; 4096 selector + unavailable joint-projection iterations, asserted 0 B; included in focused job `8a5af3a31f024646ac1f68c1fe6ce9cf` |

Focused fixture coverage:

- real Dragon14 package and real Hurtbox sync: 18 -> 15 active leaves;
  `zone.tail` identity remains with zero members/unavailable; `zone.core` loses
  only `hurt.tail_base.capsule.1`, retains chest support, and stays available;
- rebuild from the same explicit BodyPartState-derived geometry plus immutable
  binding reproduces Zone identity, availability, member order, binding revision,
  and Zone revision;
- an explicit all-leaves-inactive typed geometry state retains authored Zone
  identities with zero available Zones and is rejected by joint projection;
- tail lock stale/reselect, surviving head lock kept, topology-only global witness
  change leaves ZoneRevision unchanged, and unavailable tail is not evaluated by
  either Attack Opportunity path;
- real CharacterTest runtime with two AI owners: joint selection releases the
  tail reservation before candidate generation, keeps the core reservation, and
  the Planner returns `InvalidZone` only for the tail Anchor dependency.

Non-acceptance attempts are retained only for audit and are not passing evidence:
validator `d46044c6ffbd4e50825a43d680683428` found one test-constructor parameter-name
drift; focused job `a12c3c3446a347e98d3bf1eca60acfa3` was 4/5 because the
test-only PhysicsRuntime was not marked ready (`staticColliderCount: 0`). Both
were fixed only in the T3 fixture. The isolated corrected reservation case passed
as job `18407cd335444404bfba98971272695c` before the final full focused run.

## Files and ownership

- Runtime: `CharacterTestDamageZoneSnapshot.cs`, topology-only portions of
  `CharacterTestDamageZoneSelector.cs`, `CharacterTestEngagementPlanner.cs`,
  `CharacterTestEngagementReservationAuthority.cs`, and
  `CharacterTestRuntimeAiPlannerAttackOpportunityAdapter.Joint.cs`; one approved
  pre-candidate sweep call in `CharacterTestRuntimeAiJointRuntime.cs`.
- `CharacterTestRuntimeAiPlannerAttackOpportunityAdapter.cs` required no T3
  production edit: its existing selector path consumes the same Zone snapshot.
- Focused tests: `CharacterTestSeveredTargetingInvalidationTests.cs` and
  Unity-generated `.meta`.
- Documentation: this evidence file and the LCC-M3-T3 task execution record.
- T2 Hurtbox active-leaf/status, T4 action/trace, T5 presentation, T6 network,
  authored Dragon14 assets, distance semantics, and navigation/path math were not
  modified by this task.

## T4 handoff

- T4 can treat an unavailable Zone/leaf as already excluded from selector,
  Attack Opportunity, joint generation, Planner projection, and reservation
  capacity. T4 still owns action/trace dependency cancellation and must not
  reintroduce a root-distance or authored-definition deletion gate.
- The exact lock witness is `(target, action revision, binding revision, ZoneId,
  HitZoneId, LogicalHurtboxId)`. T4 action decisions should consume the resulting
  selection/failure rather than compare the global topology witness.

## T6 handoff

- Formal rollback/late join must restore BodyPartState and immutable binding,
  rebuild T2 geometry, then rebuild Zone availability before applying/validating
  locks and reservations. The T3 in-memory rebuild proves deterministic derived
  state but is not a formal snapshot or `rollback + 1 command/hash` claim.
- Future-affecting T3 witnesses are Zone `BindingRevision`/`ZoneRevision`, active
  Zone/member identities, the exact lock fields, and the existing reservation
  Zone/Anchor/phase/action/graph witnesses. `TopologyRevision` on the Zone view is
  diagnostic and is intentionally not part of ZoneRevision or lock validity.
- Existing reservation snapshots still carry aggregate `ZoneRevision` as audit
  state; runtime currentness is exact Zone availability, not aggregate equality.
  T6 owns schema/hash compatibility and the final decision on serialized derived
  diagnostic witnesses.

## Deliberate limits

- No full repository suite, PlayMode/visual scenario, formal network/rollback,
  late-join/hash proof, T4 action trace, T5 severed-mesh presentation, commit,
  push, or independent milestone review was performed.
- The acceptance claim is limited to the final Unity validator, five T3 EditMode
  tests, and the 26 existing CD-M2/CD-M3 regressions above. Parent milestone
  promotion and the single independent M3 terminal review remain outside T3.
