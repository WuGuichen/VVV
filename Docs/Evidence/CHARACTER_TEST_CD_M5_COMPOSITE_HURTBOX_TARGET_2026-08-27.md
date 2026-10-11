# CharacterTest CD-M5 Composite Hurtbox Target Evidence — 2026-08-27

## Delivered contract

- `target.distance` is the Fix64, ceiling-millimeter 3D surface gap from the source Completed Frame posture capsule to the persistent locked Hurtbox.
- `target.rootDistance` retains the former Transform-root XZ distance.
- Actor identity is `GameplayEntityId`; subtarget identity is `LogicalHurtboxId`. DamageZone is optional grouping metadata and HitZone remains per-Hurtbox hit metadata.
- A same-Actor Tick never scans alternative Hurtboxes. Missing/severed locked geometry remains the target identity but returns unavailable and no attack opportunity.
- AI and player Target Lock share the composite Handle. Projectile initial AimPoint and Chain Lightning first target consume the locked Hurtbox.
- Actual Combat Trace/HitResolve remains the hit authority.
- Runtime AI Lab and Editor Scene Preview always draw exactly one target indicator: exact locked-Hurtbox witnesses when available, otherwise the same Completed Geometry's frozen actor endpoints to keep the UI continuous. They draw no distance text, endpoint spheres, or second root-XZ auxiliary. The presentation fallback does not change `target.distanceAvailable` or Gameplay distance semantics.
- Hurtbox bake removes clip `RootT.*` planar translation and `RootQ.*` planar rotation before quantizing body-local shapes. Runtime authority root motion is therefore applied exactly once.

## Versioned changes

| Contract | Version |
| --- | ---: |
| Published Behavior Runtime Contract | 7 |
| Runtime AI Planner State | 14 |
| CharacterTest Network Snapshot | 34 |
| CharacterTest Replay | 4 |

Formal project behavior cards were migrated through the existing Publisher menu. The unrelated dirty Runtime AI Lab draft was not written.

## Validation

| Scope | Result |
| --- | --- |
| `CombatShapeDistanceTests` | 4/4 passed; AABB/Sphere/Capsule/OBB, failures, 4096 warmed calls 0 B |
| Composite lock / behavior contract / body-part network focused EditMode | 39/39 passed |
| Completed Geometry / Target Lock focused EditMode | 10/10 passed |
| DamageZone/authority focused EditMode | 13/13 passed |
| CD-M4 candidate + selection focused EditMode | 18/18 passed |
| CD-M2 PlayMode final | 5/5 passed；same-Actor no-rescan、距离换代、玩家复合锁、schema 34 rollback、4096 warmed cache queries 0 B |
| CD-M4 representative matrix | 1/1 passed；包含 multi-ai-16v16，zero typed truncation |
| Unity script validation job `2bdba48d37e04ae1b6d46985071baa3a` | refresh, 4 assemblies, compilation finished, domain reload, Editor ready, Console 0 error |
| Unity final validation job `1d3e598b3c174047b513532c5ab10b0b` | refresh completed, compilation started/finished, domain reload observed, Editor ready, Console 0 error |
| Distance overlay + Editor Scene Preview focused EditMode job `4c6055fddc5c426fa2dfb843698ea588` | 6/6 passed；witness geometry、label、unavailable safety、severity/color rules |
| Composite-lock PlayMode recheck job `d916e3dfa7ae411c89b9ee53aa7d9688` | 5/5 passed；same-Actor exact Hurtbox、distance refresh、player lock、schema rollback |
| Unity UI final validation job `719c23c83c414a5b9ebc5e63a3171ed6` | refresh completed, 4 assemblies compiled, domain reload observed, Editor ready, Console 0 error |
| Line-only distance overlay focused EditMode job `c54d9e938cf74018aaa807bcdc8f4c09` | 7/7 passed；single witness line data、no distance preference、unavailable safety、severity/color rules |
| Line-only Unity final validation job `66d2844a08ad4abaae2a7be60e85d918` | refresh completed, 3 assemblies compiled, domain reload observed, Editor ready, Console 0 error |
| Missing-witness continuity job `26369a72d5864e9d89830071f13b2c3c` | 2/2 passed；exact witness line and frozen-actor continuity line |
| Continuity-fix Unity validation job `0be2cae0125f4b1db90b740abd88d06f` | refresh completed, 3 assemblies compiled, domain reload observed, Editor ready, Console 0 error |
| Hurtbox-only humanoid PlayMode job `3a1442c3d75c4d5b92381c5ed80b4463` | 1/1 passed；two `codex_flow_test` actors acquire exact Hurtbox with empty DamageZone and expose formal witnesses |
| CD-M2 mixed optional-Zone PlayMode job `ebe86e8dd3664d6ab36c8e92cf3e3a48` | 6/6 passed；Hurtbox-only humanoid、Dragon14 Zone path、player lock、network rollback |
| Optional-Zone authority/network EditMode job `348b6a4161614721bfcad17544acadfb` | 10/10 passed；empty Zone accepted only with complete LogicalHurtbox identity |
| CD-M4 joint regression job `00aa3dc7fc474209b509f9bfae8d78af` | 18/18 passed |
| Optional-Zone final Unity validation job `43f14688a7244d9cb1cb97ad901fc105` | refresh completed, domain reload observed, Editor ready, Console 0 error; changed assemblies had already compiled before focused tests |
| Hurtbox root-motion catalog invariant job `f7748787857f47b6ba11a8dc29963c63` | 1/1 passed；locomotion leaves contain no accumulated RootT drift |
| Hurtbox root-motion runtime PlayMode job `7fce6856ab4f463e8ba97e411d0dcfb2` | 1/1 passed；moving Hurtbox-only actor remains body-local after 36 Tick |
| CD-M2 post-normalization PlayMode job `8ff49845254442f59863c9ae835d5edc` | 6/6 passed |
| Root-motion generator compile job `96db7fd40b9344788a6b49966e7e1b1e` | refresh, 7 assemblies compiled, domain reload, Editor ready, Console 0 error |
| Runtime AI Lab Scene View live probe | Both model views equal authority roots; both Hurtbox compounds have maximum body-local planar offset `0.346m` and visually overlap their models |
| Root-motion final Unity validation job `17244efd70e443ca88a52ffbd998c784` | refresh completed, compilation started/finished, domain reload observed, Editor ready, Console 0 error |

Final focused validation is complete.

The current project catalog suite passed 23/24; the remaining `NewProjectBehaviorCardTemplate_IsNativeAndPublishesWithoutFallbackCompatibilityLogic` expected recipe version 8 while the current workspace already produces version 9, independent of CD-M5. The broad IntegrationContract run also retained existing CharacterAction/generated-package fixture failures; CD-M5-owned exact subsets were rerun separately and are green.

## Known workspace baseline

One accidental broad `CharacterTestIntegrationContractTests` run executed 497 tests and exposed many existing CharacterAction/generated-catalog/Camera fixture failures in the current dirty workspace. The CD-M5 focused Completed Geometry/Target Lock tests from that class were rerun by exact name and passed 10/10. Unrelated Shader, Character Import, StorySource, Blend and palette work remains outside the CD-M5 change set.
