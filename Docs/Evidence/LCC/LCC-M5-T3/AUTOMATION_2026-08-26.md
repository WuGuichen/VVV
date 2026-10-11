# LCC-M5-T3 Automation Candidate Evidence - 2026-08-26

## Result

`Candidate Ready for Final Review after recorded repairs`. The first formal Dragon14 PlayMode matrix failed and remains recorded below; T2 repaired the zero-velocity pressure defect, T1 repaired both additive v7 consumers, and T3 reran the original failure paths before promoting the candidate. The historical failures are retained rather than rewritten as initial passes.

## Completed checks

| Fixture | Unity job | Result | Evidence |
| --- | --- | --- | --- |
| `CharacterTestAuthorityPoseConsumerTests` | `0a8e6f1cf98449d2b94b15ed1078c04d` | 9/9 Passed, 0 failed, 0 skipped, 0.6698269 s | The one allowed stale assertion now verifies 3 Dragon14 `PresentationAudio` / `PresentationOnly` configs, 0 authority-pose ContactPoint attachments, and the adapter remains closed to undeclared authority input. |
| `CharacterTestDragon14AnimatedAppendageSoftContactPlayModeTests` | `8672ed0e717743eda20c036cf620e764` | 0/1 Passed, 1 failed | Formal `CharacterTestScene`, generated Dragon14 geometry v7, exactly 3 animated sources and the real `CharacterTestCharacterRuntimeModule`; head contact completed, then tail-mid produced a contact observation without pressure velocity. |

The PlayMode filter was:

```text
MxFramework.Tests.PlayMode.CharacterTest.CharacterTestDragon14AnimatedAppendageSoftContactPlayModeTests
```

## First production failure

```text
collider=hurt.tail_mid.capsule.0
target_mm=(1032,0,-1592)
shape=Capsule
world_bounds=(0.337001,0.050000,-2.134003)..(1.528002,0.899000,-1.051002)
contact_count=1
contact_velocity_mmps=0
expected=bounded positive soft-contact velocity
```

The same fixture had already completed the formal head assertion with positive bounded contact velocity, unchanged HP and zero M1 Body Presence constraints. The tail-mid failure therefore is not a missing v7 binding, missing Hurtbox leaf, scene-load failure or total M5 dispatch failure: separation/contact observation exists, but the production pressure path applies no displacement-driving velocity for this formal contact.

## Not executed after stop gate

- tail-tip, movement, turn, wall/corner/high-speed and Severed PlayMode matrix;
- full/owned/non-owned hash, rollback + 1, replay, late join and source mismatch aggregation;
- frozen M1-M4 narrow regressions and existing PlayMode regressions;
- 8 Dragon warm 300 / measure 1800 timing and steady-state GC;
- final Unity script gate and final P0/P1 review.

No production, Authoring or formal asset file was changed by T3.

## Repair-after rerun

After the T2 owner changed the penetration-bias clamp to the existing maximum composed-speed bound, the exact same formal PlayMode fixture passed:

| Fixture | Unity job | Result | Evidence |
| --- | --- | --- | --- |
| `CharacterTestDragon14AnimatedAppendageSoftContactPlayModeTests` | `6588b559b01e43bd9781d38b940df8ee` | 1/1 Passed, 8.0072538 s | head `4274 mm/s`, tail-mid `2722 mm/s`, tail-tip `4401 mm/s`; each contact count 1; HP unchanged; M1 Presence constraints absent at sampled appendage contacts. |
| `CharacterTestAnimatedAppendageSoftContactPerformanceTests` | `e001f235a1254181ba41ce31cf88b859` | 1/1 Passed, 1.769538 s | 8 owners x 3 sources + 1 player, warm 300 / measure 1800, 192 constraints, measured allocation 0 B. |

The next frozen aggregate job `3567dc40ddc54bc9bcef8d039ff0ea88` ran 79 EditMode tests and exposed a second production gate before acceptance could continue: formal Dragon14 now uses `mx.characterGeometryBinding.v7`, while network/replay playable-package authority identity rejects it as an invalid geometry format/schema. Multiple network/replay cases fail before bootstrap with:

```text
Playable character package authority identity rejected.
packageRoot=Assets/MxFrameworkGenerated/CharacterPackages/000003
reason=geometry format/schema is invalid
```

The same job also contains stale fixture expectations for v6 and older snapshot counts. T3 stopped and returned the v7 additive authority-identity compatibility defect to the T1/network owner. The 79-test aggregate is not reported as passed.

Source inspection after the failed aggregate found the same additive-version omission in the M1 root Presence consumer: `CharacterTestCharacterBodyPresenceSystem.ValidateBinding` accepts authored geometry only through v4/v5/v6 and returns `BindingState.Invalid` for v7. Thus the formal Dragon14 v7 package can disable its three ModelRoot Body Presence volumes even while the new M5 animated appendage path runs. The PlayMode fixture's `M1 constraint count=0` only isolates its sampled appendage position; it is not proof that root Presence remained active.

Both omissions are one T1 compatibility repair boundary:

- `CharacterTestNetworkSessionFactory.ValidatePlayableCharacterAuthorityPackage`: v1-v6 only, so network/replay authority bootstrap rejects formal v7;
- `CharacterTestCharacterBodyPresenceSystem.ValidateBinding`: v4-v6 only, so formal v7 fails M1 root Presence projection.

T3 made no production fix and remains stopped until both consumers accept v7 additively without weakening legacy/future fail-closed behavior.

## v7 consumer repair rerun

T1 repaired both additive v7 consumers and reported its own 30/30 focused result. T3 then reran the same aggregate as job `0979b1189c92422b87592191ed89ccf5`:

- all prior `geometry format/schema is invalid` failures disappeared;
- M1 Body Presence authority fixtures no longer failed on formal Dragon14 v7;
- 79 tests completed; 6 unrelated pre-existing assertions remained: two schema 32/33 expected-count/legacy cases, one localized Replay projection text expectation and three pinned-card/deck assertions.

Those six existing fixtures are read-only under T3 and are recorded as external baseline limits, not M5 passes. A dedicated M5/network acceptance job `8c2bce466cc7440e8fe37510862f417c` passed 3/3: canonical roster/leaf order parity, restore silent baseline followed by Stayed, and formal Dragon14 schema-33 rollback+1 with matching pose, Hurtbox, BakedSocket trace, Planner command and state hash.

The required PlayMode aggregate job `e1c36110c3a0406b9b9d28fe65f434c6` completed 7 tests. Six passed, including the M5 formal fixture and the Dragon14 terrain-pose regression. The sole failure is an existing M1 diagnostics fixture that still expects string action id `000003.attack1a` while the current authoritative action identity is numeric `940000`; T3 did not migrate this unrelated read-only fixture.

Final M5 focused job `fc16dfa4183448c8bad087d3771e32b6` passed 11/11, covering Body Presence v2 runtime binding, M5 constraints/phases/Severed behavior, order/restore network behavior and the 300+1800 performance fixture. The unique final Unity script gate was job `48e4dc0197b14d958637eecd78e67994`: refresh requested/completed, compilation requested/started/finished, domain reload observed, Editor ready and Console error count 0.

## Acceptance mapping and visual limits

| Requirement | Actual evidence | Candidate conclusion / remaining visual limit |
| --- | --- | --- |
| Static head / tail-mid / tail-tip | Formal PlayMode job `6588b559b01e43bd9781d38b940df8ee`; `HeadObbAndTailCapsules_ProduceBoundedPressureWithoutDirectDisplacement` in final 11/11 focused job | Passed for the three formal sampled contacts; Game View is available with tests-only markers. |
| Moving appendage witness | M4 frozen movement/turning Hurtbox/pose regressions are among the six relevant PlayMode passes in job `e1c36110c3a0406b9b9d28fe65f434c6`; M5 consumes those same previous/current DamageGeometry leaves and `HeadObbAndTailCapsules...` proves the M5 pressure endpoint | No separate player-facing M5 moving-appendage visual sequence was captured. This is dependency/reuse evidence, not a claim that a dedicated M5 movement presentation test passed. |
| Turning source | `YawNinetyHalfTurnAndWrap_UseShortestAngleAndOmegaCrossRadiusSign` and formal `MotionLifecycleAndRollbackMatrix_UsesFormalDragon14Presence` cover the reused M1 bounded pressure/static-sweep contract; M4 PlayMode covers turn-updated Hurtbox pose | No separate Dragon14 turning-head/tail M5 visual sequence; only the reused authority chain and bounded pressure endpoint are proven. |
| High-speed safety | `HeadObbAndTailCapsules...` asserts bounded contact speed; `SeparatedTouchingDeepAndSameCenter_AreTypedAndStable` covers deep/same-center and zero-acceleration penetration; production reuses the existing profile maximum-composed-speed clamp | No authored high-speed tail-animation player-facing sequence or teleport/夹死 video was captured. Candidate claims bounded solver behavior, not visual high-speed tuning completion. |
| Wall / corner / corridor | `PresenceVelocity_IsConsumedByNextTickStaticWallSweepWithoutDirectDisplacement` and PlayMode `StaticObstacleAndSemanticIsolationMatrix_RemainsBoundedAndLayerSafe` passed in their frozen jobs; M5 writes the same contact-velocity accumulator consumed by the same next-tick static sweep | Static sweep precedence is covered by reuse; no appendage-specific wall/corner Game View sequence was captured. |
| Severed tail | M5 `SeveredTail_DerivesOneEndedAndKeepsHeadStayed` in final 11/11 focused job; M3 `Dragon14_SeveredTail_RemovesThreeLeavesPreservesOrdinalsAndStableTopology` in the frozen aggregate | Passed at topology/contact-phase authority level: one tail Ended, head Stayed, stable leaf removal. No production detachable-tail asset or separate player-facing Severed contact sequence exists. |
| Ordinary contact isolation | Formal PlayMode HP unchanged; M5 focused writes no direct displacement; Combat contract keeps Damage/HitResolve/Trace ownership unchanged | Passed for sampled ordinary contacts; production appendage Audio/VFX remains unsupported. |

The table deliberately distinguishes direct M5 tests from M1/M3/M4 dependency reuse. Items lacking a dedicated player-facing visual sequence remain explicit presentation/coverage limits rather than being promoted to direct Playable passes.

## P1 evidence repair

The unique final review rejected the earlier single-frame/menu-obscured visual and synthetic-only performance evidence. T3 changed tests/evidence only and reran the corrected paths:

| Scope | Unity job | Result | Direct evidence |
| --- | --- | --- | --- |
| Real multi-frame view + M5 motion/safety + M4 narrow regression | `49385ded7f134ef58881d523976b9ad9` | 5/5 Passed | Formal scene menu closed; real Dragon14/player prefab views; head/tail-mid/tail-tip next-tick displacement 75/41/74 mm; moving head 4274 mm/s + 75 mm, turning head 970 mm/s + 26 mm, high-speed tail-tip clamped to 4401 mm/s, wall/corner sweep stayed outside static planes, Severed tail contact 0 and head contact 1. |
| M5 focused + v1/v7 binding + formal rollback+1 + both performance fixtures | `58457a8e727b4a8d8acd069dc937108f` | 13/13 Passed | Formal RuntimeModule performance, synthetic substage, phase/Severed, network restore/order, v7 source identity and schema-33 rollback+1 hash all passed together. |

The corrected formal PlayMode path also records unchanged HP and an unchanged count of typed `CombatHit` trace events across ordinary appendage contacts. Unlike the earlier dependency-only mapping, moving, turning, high-speed clamp, wall/corner and Severed now have direct M5-only numeric assertions in `FormalDragon14_MovingTurningHighSpeedWallCornerAndSeveredStayBounded`.

The earlier 79-test six-item non-M5 stale baseline and one string-ActionId PlayMode baseline remain documented; T3 did not edit those read-only fixtures.

Final narrow frozen regression job `4a23acc7da774d9096896eb7cb7a2255` passed 39/39 across M1 Presence authority/contact/motion safety, M3 Hurtbox topology and M4 authority-pose consumer fixtures. The post-P1 unique Unity script gate was job `3fbfe6ceae0d4f00a72f8d799cf0f18a`: refresh requested/completed, compilation requested/started/finished, domain reload observed, Editor ready and Console error count 0.
