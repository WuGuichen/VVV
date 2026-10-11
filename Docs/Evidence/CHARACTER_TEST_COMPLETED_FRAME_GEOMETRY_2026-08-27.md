# CharacterTest Completed Frame Geometry evidence — 2026-08-27

## Contract

- Formal `target.distance` remains Transform-root XZ distance with the existing float calculation order.
- Post-motion `Building[N]` is shared by Combat N and is committed only after the Character Runtime Tick completes.
- Planner and Runtime Commands N+1 reuse `Committed[N]`; stage query resets no longer discard Pair metrics.
- One unordered Pair memo owns planar/spatial squared values and the XZ real distance; A→B/B→A own separate directed projections.
- CD-M4 keeps its existing XYZ soft-rank semantics and reads `SqrSpatialDistanceMillimeters` from the frozen Pair.
- Geometry cache state is derived and excluded from SaveState, network snapshot, authority hash and schema.

## Unity validation

- `af75ba547d8a484d84a506705555d88e`: refresh completed, compilation started/finished, four assemblies compiled, domain reload observed, Editor ready, Console errors 0.
- `d9ce3d8fe2df4117b9bb2e931967a4e6`: final consumer-wiring compilation, domain reload observed, Editor ready, Console errors 0.
- `96c9540086034ee198a79ca4748b3911`: typed M4 fixture and final test-assembly compilation, domain reload observed, Editor ready, Console errors 0.
- `086b9634b9b4457ab6df8232ea0e6257`: final closeout refresh/compile observation, domain reload observed, Editor ready, Console errors 0.
- `2aa387a251f1440d881f7511a01c77eb`: final source and preview compilation across four assemblies, domain reload observed, Editor ready, Console errors 0.

Existing obsolete-API and duplicate-using warnings remain outside this change; no new warning is attributed to Completed Frame Geometry.

## Focused tests

| Job | Mode | Result | Coverage |
| --- | --- | --- | --- |
| `d9b23ea1467c4e6b96184a949c083cdd` | EditMode | 4/4 | existing geometry reuse plus cross-phase Pair reuse, symmetric Pair/directed projection and legal zero distance |
| `cb2a017a8ccf4049aaa8c0206bcdf91c` | EditMode | 2/2 | post-motion mutation recapture and same-Frame rollback generation replacement |
| `5497e04b6cb845adba1f55fb37c16dc0` | EditMode | 2/2 | warmed double-buffer 0 B and post-Combat entity removal exclusion |
| `da40f8c296dc4d36a6091474b95497d2` | EditMode | 8/8 | target-lock switch/network convergence and behavior-card distance movement |
| `979fd1f7815e4c21af935124c362e274` | PlayMode | 13/13 | CD-M1～M3 formal Playable/runtime/rollback paths |
| `3dd1906ff8144dccb7487bf3fe676db7` | PlayMode | 1/1 | representative M4 16v16 zero-truncation calibration |
| `1c4ef443c491451885ce3ca24d399e93` | EditMode | 18/18 | M4 candidate generation, typed budgets, reducer, two-level cache and rollback clear |
| `9daa31a95f6345ad9a3f8faa324ad350` | EditMode | 159/166 | aggregate CD-M0～M4 run; no Completed Frame Geometry, target-lock, M2, or M4 failure |
| `2cd76f181ad44c6eb5b53e00598e41b4` | EditMode | 2/2 | final preview distance formatting and warmed double-buffer 0 B recheck |

The first combined M4 EditMode run exposed three stale fixture comparisons that treated the current numeric `CharacterActionId` as the legacy input string. The fixtures now compare typed IDs; the complete M4 candidate/selection set passes 18/18.

The aggregate CD run retained seven unrelated current-workspace failures: four M1 hit-policy network fixtures whose action identities are pruned before snapshot, two M1 adapter/source-revision expectations in untouched adapter code, and one M3 snapshot schema expectation (`12` versus current `13`). None failed in the new Completed Frame Geometry, target-lock, movement, M2, M4 or PlayMode paths; they are reported rather than hidden by the focused pass counts.

## Limits

- This work removes duplicate root-distance construction and mixed-frame presentation reads; it does not add DamageSurfaceGap, Hurtbox surface distance or AttackEnvelopeSeparation.
- Standalone compatibility services may still calculate from live Transform when intentionally constructed without the formal perception cache; the normal GameSlice composition uses Completed Frame Geometry.
- Visibility, Navigation and Attack Opportunity remain parameter/revision-bound query caches and are not made persistent merely because they consume the same frozen root geometry.
