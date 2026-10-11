# LCC-M5-T2 Animated Appendage Soft Contact Evidence - 2026-08-26

## Result

T2 production implementation is complete inside the frozen motion/contact owner boundary. It consumes the prior completed DamageGeometry snapshot before motor mutation and the final current snapshot after the existing second Hurtbox `SyncAll`; no third sync, global Hurtbox obstacle mask, direct displacement, Combat/Hurtbox/DamageGeometry/Network mutation or schema 34 was added.

This evidence proves the Runtime Slice and focused deterministic/NoAlloc behavior only. Dragon14 Playable/Game View, full rollback/replay acceptance and timing calibration remain T3.

## Schedule and authority chain

```text
frame N Motion.Tick begins
  clear frame-local contact accumulators
  copy selected v7-authored leaves from prior completed/restore-rebuilt DamageGeometry snapshot
  existing motor + M1 Body Presence pressure
  existing Hurtbox SyncAll #1
RuntimeModule authority-pose stage
  M4 TickAuthorityPose
  existing Hurtbox SyncAll #2 -> final frame-N DamageGeometry snapshot
  M5 capture current selected leaves -> build/validate -> three pressure iterations
  ApplyCharacterContactState again, composing M1 + M5 observations
combat trace follows unchanged
```

The final M5 substep calls `CombatPhysicsWorld.CalculateCapsuleSeparation` directly for each authored logical leaf. It does not scan `CharacterHurtboxLayer`, and `MotionObstacleLayerMask` is unchanged.

## Binding, capacity and failure contract

- source binding: Body Presence v2 / geometry v7 `AnimatedHurtboxSources`, maximum 4 leaves per owner;
- source owners: maximum 8; selected previous/current witness capacity: 32 each;
- acceptance roster: maximum 9 entities (8 source owners plus one player);
- directed constraint key and sort: `(capsule source entity, appendage owner entity, logical collider id, combat collider id, shape kind)`;
- directed constraint capacity: 256; phase-fact capacity: 512 for a full Ended/Started turnover;
- exact identity: logical collider id and `CharacterHurtboxColliderIdStart + authored collider ordinal`, CharacterHurtbox layer, authored PartId and supported Sphere/Capsule/OBB kind must all match;
- inactive/missing topology leaves are omitted, so Severed tail sources disappear without a second severance table;
- invalid source, duplicate/conflict, unsupported/invalid separation, policy, arithmetic, roster/witness/constraint overflow reject the M5 substep before pressure/contact-state mutation.

Current penetration remains usable when the prior motion witness is missing, while animated surface velocity fails closed. Consecutive frame, binding revision/hash, collider/PartId/kind and legal owner-root delta are required before previous/current shape-center velocity is enabled. Surface velocity, penetration bias and composed contact speed reuse the existing profile-derived fixed-point clamp.

## Pressure and presentation behavior

- reuses the existing three pressure iterations, effective mass, effective PushScale, target `CanBePushed`, `ApplyBoundedPressureDelta`, contact velocity/count accumulators and next-tick motor/static-sweep consumption;
- writes no transform/contact displacement and produces no Damage, HitResolve or Active Trace;
- ordinary contact records both owner and target observation counts; only the target capsule receives bounded velocity pressure;
- Started/Stayed/Ended are presentation-only typed facts derived after the gameplay constraint set succeeds;
- spawn/restore/source reset establishes a silent baseline; later canonical diff produces one Ended when a Severed tail contact disappears, while retained head contact stays Stayed.

## Focused EditMode results

| Fixture | Job | Result | Coverage |
| --- | --- | --- | --- |
| `CharacterTestAnimatedAppendageSoftContactTests` | `2126679d197e40e8a33d9f6794d360ef` | 5/5 Passed, 0.6357802 s | head OBB, tail-mid/tail-tip capsules, separated/contact/deep/same-center, no direct displacement, clamp, dead/unpushable, capacity fail-closed, Severed single Ended |
| `CharacterTestAnimatedAppendageSoftContactNetworkTests` | `00c6f4366d7842a1a4f0f4945d9ab424` | 2/2 Passed, 0.1812911 s | roster/leaf order perturbation parity; restore silent baseline then Stayed |
| `CharacterTestAnimatedAppendageSoftContactPerformanceTests` | `8ed594ae7f404041ae29106bc535be24` | 1/1 Passed, 0.3166422 s | warmed 8 owners x 4 leaves, 256 directed constraints, 128 measured build/apply frames, `GC.GetAllocatedBytesForCurrentThread` delta 0 B |

The first implementation fixture run exposed two test-only boundary assumptions (an exact-contact expectation and one leaf at the separation boundary). The fixture positions were changed to deterministic overlap; production code was not loosened to satisfy them.

## Narrow regressions

| Existing fixture | Job | Result |
| --- | --- | --- |
| `CharacterTestLargeCharacterPresenceContactTests` | `5b190f70435e4ceba2b6d5aa9b6839e1` | 7/7 Passed |
| `CharacterTestLargeCharacterPresenceMotionSafetyTests` | `50581b4216a34ca6a93259a3be3b47f6` | 6/6 Passed |
| `CharacterTestHurtboxTopologyTests` | `10cd8cafa5aa45bb8da1fd4eb9321fe0` | 6/6 Passed |
| `CharacterTestAuthorityPoseConsumerTests` | `4c3e23d9e6d848e29a55d1ac4409b770` | 8/9 Passed |

The sole M4 consumer failure is a stale pre-existing assertion named `Dragon14_HasNoContactPointConfigOrAttachment_AndAdapterStaysClosed`: it expects zero Dragon14 ContactPoint configs, while the current formal package contains the three PresentationOnly ContactPoints added before T2. T2 does not read those ContactPoints and did not modify that M4 fixture because it is outside the T2 owner boundary.

## Unity script gate

The one integrated gate was job `7fd7c9d722664bc5b2aeaaae6de82312`:

```text
outcome=passed
refresh requested/completed=true/true
compilation requested/started/finished=true/true/true
domain_reload_observed=true
editor_ready=true
console.error_count=0
```

## Files and remaining limits

Production:

- declaration/schedule-only T2 hunks in `CharacterTestCharacterMotionSystem.cs`, `CharacterTestCharacterContactResolver.cs`, `CharacterTestCharacterRuntimeModule.cs`;
- new `CharacterTestCharacterMotionSystem.AnimatedAppendageSoftContact.cs`;
- new `CharacterTestCharacterContactResolver.AnimatedAppendageSoftContact.cs`.

Tests:

- `CharacterTestAnimatedAppendageSoftContactTests.cs`;
- `CharacterTestAnimatedAppendageSoftContactNetworkTests.cs`;
- `CharacterTestAnimatedAppendageSoftContactPerformanceTests.cs`;
- matching `.meta` files.

Not run: PlayMode/Game View, Dragon14 hands-on collision tuning, full network snapshot/replay suite, full repository tests, `.csproj` builds, per-stage 8-Dragon p50/p95/max timing, T3 acceptance or independent final review. No staging, commit or push was performed.

## T3 tail-mid pressure defect repair

T3 formal PlayMode job `8672ed0e717743eda20c036cf620e764` subsequently proved that `hurt.tail_mid.capsule.0` built a valid contact (`ContactCount=1`) but emitted zero contact velocity while the head case passed. The leaf identity, formal geometry and separation path were therefore valid; the failure was isolated to the T2 motion policy.

Root cause: penetration bias was clamped by `AccelerationStepMillimetersPerSecond`. A runtime movement profile may legitimately expose a zero acceleration step. A stationary animated tail surface then had neither surface velocity nor penetration bias, so the pressure solver observed contact but had zero closing speed.

The repair changes only the T2 constraint-motion clamp: penetration bias remains `penetration * tickRate`, but its upper bound is the already frozen profile-derived `MaximumComposedSpeedMillimetersPerSecond`. Mass, PushScale, `CanBePushed`, three pressure iterations and `ApplyBoundedPressureDelta` remain unchanged. No minimum impulse, new constant, direct displacement or geometry change was added.

The existing shape-boundary test now also runs a deep overlap with zero profile acceleration and requires non-zero bounded pressure magnitude. Repair verification:

| Fixture | Job | Result |
| --- | --- | --- |
| T2 behavior focused | `cbd68de60fc044848304b344fabac512` | 5/5 Passed |
| T2 network | `5575fa1013624520841edb1897a40f87` | 2/2 Passed |
| T2 performance / 0 B | `7d21a931a66f4483a8a7d208b1674f08` | 1/1 Passed |
| M1 contact | `32e24b012e624e8997b55af218666b63` | 7/7 Passed |
| M1 motion safety | `62608202f0eb43a6b470491f354e8ed0` | 6/6 Passed |
| M3 Hurtbox topology | `06031a49493c4010a55900fead966dc5` | 6/6 Passed |

Repair Unity gate `cd31676b20ca4b46b7058a01831cd1ca` passed with refresh/compile/domain reload/Editor ready all observed and Console error count 0. T3 must rerun the formal Dragon14 PlayMode fixture to prove the production tail-mid outcome.
