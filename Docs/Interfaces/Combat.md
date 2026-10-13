# Combat 接口

> Status: Current
> Code-Doc: `Packages/com.vincent.wgameframework/Combat/Projectiles/CombatProjectileRuntime.cs`
> Code-Doc: `Packages/com.vincent.wgameframework/Combat/Projectiles/CombatProjectileRuntimeTypes.cs`
> 文档版本：2026-10-11.1
> Code-Doc: `Packages/com.vincent.wgameframework/Combat/Hit/HitTargetStateContributionStore.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Combat/Hit/HitTargetStateContributionStoreTests.cs`

> 状态：Combat Physics / Motion / Hit Resolve / RuntimeHost animation modules 已实现并有测试覆盖。本文记录当前源码中已经存在的 `MxFramework.Combat` 公开契约，不收录设计文档中尚未落地的 `CombatWorld`、`ActionSystem` 等草案类型。

2026-10-12补充：`CombatProjectileSpawnEvent` 增加 `HasRequest` 与 `Request`。Runtime发出的出生事件保留实际接受的完整请求（含SourceOrder），供调用方冻结来源；旧仅snapshot构造函数不提供请求证明。该瞬时事件不把游戏特定状态带入Combat；CharacterTest自行保存出生来源并进入阶段hash。见[双刃配件交接](../Tasks/TACTICAL_DOUBLE_EDGE_ACCESSORY_IMPLEMENTATION.md)。

## 职责

Combat 提供 noEngine 的确定性战斗物理、动作时间轴、命中结算、角色运动和诊断能力。它不依赖 `UnityEngine.Physics`、`Animator` 或场景对象；Unity 侧只能作为表现、authoring 或调试入口。

核心边界：
- `MxFramework.Combat` 引用 `MxFramework.Core` 和 `MxFramework.Runtime`，`noEngineReferences=true`。
- `MxFramework.Combat.GameplayBridge` 是独立桥接层，负责把命中结果转成 Gameplay / Buff 侧可消费的事件。
- `Combat.Authoring` 和 `Combat.Editor` 不属于运行时契约；本文只在测试入口中标注它们的验证路径。
- Runtime source 保持 Combat-agnostic；RuntimeHost 只提供 `RuntimeTickContext`，不内置 Combat frame、固定步进 accumulator 或 Combat-aware service。

## 公开接口概览

| 分组 | 公开类型 | 用途 |
|------|----------|------|
| Core | `CombatFrame` / `CombatFrameClock` / `CombatStepConfig` / `CombatFixedStepDriver` / `CombatFixedStepBatch` | 固定帧时钟、步进配置、Runtime delta accumulator 和帧推进批次 |
| Core | `CombatEntityId` / `CombatBodyId` / `CombatColliderId` | 稳定 ID 值对象 |
| Core | `CombatSortKey` / `CombatHash` | 稳定排序和诊断 hash |
| Physics | `CombatPhysicsWorld` | noEngine 战斗物理世界，管理 body / AABB collider / query |
| Physics | `CombatPhysicsBody` / `CombatPhysicsAabbCollider` / `CombatPhysicsWorldStats` | 物理世界状态和统计 |
| Physics | `CombatPhysicsShape` / `CombatPhysicsShapeKind` / `CombatPhysicsLayerMask` | 查询形状和层过滤 |
| Physics | `CombatRayQuery` / `CombatSphereQuery` / `CombatCapsuleQuery` / `CombatAabbQuery` / `CombatSectorQuery` | 已落地的查询 DTO |
| Physics | `CombatPhysicsQuery` / `CombatQueryHeader` / `CombatPhysicsQueryFilter` / `CombatQueryKind` / `CombatQueryResult` | 统一查询契约和稳定结果 |
| Physics | `CombatPhysicsQueryBatch` / `CombatPhysicsQueryBatchResult` | 批量查询输入和结果 |
| Physics Ground Probe | `CombatGroundProbeQuery` / `CombatGroundProbeResult` / `CombatGroundProbeHit` / `CombatGroundProbeStatus` / `CombatGroundProbeHitComparer` | noEngine fixed-point static ground probe、typed failure、量化 stable hit 和完整排序 |
| Physics Separation | `CombatShapeSeparationResult` / `CombatShapeSeparationStatus` / `CombatShapeSeparationResultComparer` | capsule 对受支持目标形状的确定性分离事实、typed 状态和稳定排序 |
| Physics Contacts | `CombatPhysicalContactEvent` / `CombatPhysicalContactEventBuffer` / `CombatPhysicalContactSensorSample` / `CombatPhysicalContactSensorRunner` / `CombatBodyPartContactPointRuntime` | Combat 物理接触事实、单帧固定容量 buffer、确定性 sensor sample、部位碰撞点运行时和接触 phase 生成 |
| Physics Diagnostics | `CombatPhysicsQueryDebugReport` / `CombatPhysicsQueryDebugSummary` / `CombatPhysicsQueryDebugRow` / `CombatPhysicsQueryDebugRowStatus` | 查询 explain / broadphase / filter / hit 诊断 |
| Levels | `CombatLevelDefinition` / `CombatLevelValidator` / `CombatLevelWorldBuilder` / `CombatLevelHash` / `CombatLevelJson` | noEngine Level v2 foundation for static AABB/OBB colliders, surface materials, spawn points, checkpoints, strict v1 migration, deterministic shape-aware hash, JSON roundtrip, and `CombatPhysicsWorld` construction |
| Motion | `CombatKinematicMotor` / `CombatKinematicMotorCandidateCache` | 固定帧运动、重力、跳跃、AABB/Sphere/Capsule/静态 OBB 碰撞阻挡、可选物理世界同步和批量步进候选缓存 |
| Motion | `CombatMotionState` / `CombatMotionInput` / `CombatMotionConfig` / `CombatMotionStepResult` | 运动状态、输入、配置和步进结果 |
| Motion | `CombatMotionCapsuleProxy` / `CombatMotionCollision` / `CombatMotionCollisionFlags` | capsule proxy 与运动碰撞摘要 |
| Hit | `HitResolveSystem` | 对命中候选执行稳定排序、hit-once、防友伤和目标状态过滤 |
| Hit | `HitCandidate` / `HitResolveResult` / `HitResolveKind` / `HitTargetStateFlags` | 命中候选、结算结果、结果类型和目标状态 flags |
| Hit | `HitTargetStateContributionStore` | 按 entity / action instance / stable event 隔离临时目标状态贡献，合并 flags 并支持单来源、实例和实体清理 |
| Hit | `ITeamRelationProvider` / `IHitTargetStateResolver` / `ICombatEventDispatcher` | 队伍关系、目标状态和命中事件派发扩展点 |
| Hit Config | `CombatHitEventConfig` / `CombatHitDamageConfig` / `CombatHitTargetTypes` / `CombatEnvironmentHitPolicy` / `CombatHitSameTargetPolicy` | 武器 trace、projectile impact 等命中生产者共享的目标类型、环境触发、稳定事件 identity、命中次数约束和受限伤害属性 |
| Damage | `DamageRequest` / `DamageProfileUsage` / `DamageProfileResolver` / `DamageProfileResolveResult` / `DamageResolvedEvent` / `HitPresentationEvent` / `DamageTelemetryRow` | 结构化伤害来源上下文、伤害计算、结果事件和 telemetry；旧 profile catalog 类型仍属于通用 Combat legacy 工具，不是 CharacterAction / CharacterTest 正式配置入口 |
| Projectiles | `CombatProjectileDefinitionRegistry` / `CombatProjectileDefinition` / `CombatProjectileMotionConfig` / `CombatProjectileCollisionConfig` / `CombatProjectileImpactConfig` | noEngine 通用飞行体定义、运动形态、碰撞和 impact 配置 |
| Projectiles | `CombatProjectileDefinitionConfig` / `CombatProjectileConfigJsonImporter` / `CombatProjectileConfigBinaryImporter` | runtime projectile catalog importer；JSON 供 authoring/debug，`.bytes` 供 Player-first loading，二者生成同一 `CombatProjectileDefinitionRegistry` |
| Projectiles | `CombatProjectileSpawnProfileRegistry` / `CombatProjectileSpawnProfile` / `CombatProjectileSpawnProfileConfigJsonImporter` / `CombatProjectileSpawnProfileConfigBinaryImporter` | runtime projectile spawn profile importer；把 CharacterAction request id 映射到 projectile definition、确定性 socket 起点和瞄准策略 |
| Projectiles | `CombatProjectileSpawnRequest` / `CombatProjectileSpawnRequestBuffer` / `ICombatProjectileSpawnSource` | typed spawn 输入；v0 不通过 `RuntimeCommand` 承载 `FixVector3` |
| Projectiles | `CombatProjectileRuntime` / `CombatProjectileRuntimeModule` | 固定帧 projectile 状态推进和 RuntimeHost 模块包装 |
| Projectiles | `CombatProjectileSnapshot` / `CombatProjectileRuntimeState` / `CombatProjectileFrameEvents` / `CombatProjectileAreaPulseTriggerEvent` / `CombatProjectileImpactEvent` / `CombatProjectilePhysicalEvent` / `CombatProjectileDebugSnapshot` | projectile 只读状态、帧事件、area pulse 触发事件、impact 事件、物理事件事实和调试快照 |
| Projectiles | `ICombatProjectileImpactResolver` / `CombatProjectileRuntimeHashContributor` / `CombatProjectileRuntimeSaveStateProvider` | impact 转换扩展点、Runtime hash 和 SaveState |
| Projectiles.Unity | `CombatProjectileUnityPresentationBindingV2` / `CombatProjectileUnityPresentationSlot` / `CombatProjectileUnityPresentationExitPolicy` / `CombatProjectileUnityPresentationRegistry` / JSON、binary importer / `CombatProjectileUnityPresenter` | Unity view-only 表现合同和 presenter；v2 固定七槽与退出策略，不参与权威逻辑、Combat hash 或 SaveState；旧 flight-only binding 是 obsolete adapter |
| Shields | `CombatShieldPotencyTriplet` / `CombatShieldPotencyComponent` / `CombatShieldPressureBand` / `CombatShieldImpactMapping` | noEngine 物理防护盾三势状态、状态段和命中输入到三势增量的配置映射 |
| Shields | `CombatShieldRuntime` / `CombatShieldRuntimeModule` / `CombatShieldImpactRequest` | 固定帧护盾状态推进、冲击请求消费、延迟恢复、破盾 collider 移除和 RuntimeHost 模块包装 |
| Shields | `CombatShieldOcclusionFilter` / `CombatShieldBlockEvent` / `CombatShieldPotencyChangedEvent` / `CombatShieldBreakEvent` | projectile / weapon trace hit resolve 前的护盾遮挡过滤，以及挡住、三势变化和破盾 typed events |
| Shields | `CombatShieldRuntimeHashContributor` / `CombatShieldRuntimeSaveStateProvider` / `CombatShieldRuntimeState` / `CombatShieldDebugSnapshot` | 护盾 Runtime hash、SaveState roundtrip 和诊断快照 |

Damage source coverage note: CharacterTest currently proves `WeaponTrace`, `AreaPulse`, and `ProjectileImpact` sources through the shared `hitEvent.damage` payload -> CharacterTest damage resolver -> HitResolve -> Gameplay HP / scatter pressure application chain. Weapon trace proximal range extension remains part of `WeaponTrace`; diagnostics may distinguish blade vs proximal query geometry, but damage telemetry does not introduce a separate source. Accepted `impactPower` is applied through `GameplayScatterPressureComponent`; action-level reactions now use the shared CharacterAction reaction helper / CharacterControl reaction bridge path after CharacterTest has decided whether the event is cohesion break, broken-window hit, launch, or death. Ordinary accepted damage does not imply action interruption; projects must explicitly submit hit metadata through `CharacterActionReactionBridge.ApplyHit(...)` / `CharacterActionController.SubmitReaction(...)`. CharacterTest `Guard Aura` proves Aura Buff range refresh, but Aura Buff v0 only refreshes component-native Buff state through `CombatGameplayAuraBuffSystem`; damaging Buff `StatusTick` must produce the same common damage payload shape.
| Animation | `CombatActionTimeline` / `CombatActionSupportProfile` / `CombatActionFrameEvent` / `CombatActionWindow` / `CombatFrameRange` / `CombatActionWindowKind` | 动作时间轴、支撑参数、表现关联事件、窗口和帧范围 |
| Animation | `CombatActionRunner` / `CombatActionRegistry` / `CombatActionInstance` / `CombatActionState` / `CombatActionPhase` | 动作注册、运行、状态和阶段 |
| Animation | `ActionResult` / `ActionStartedEvent` / `ActionPhaseChangedEvent` / `ActionFrameEventRaisedEvent` / `ActionFinishedEvent` / `ActionCanceledEvent` / `ActionCancelRejectedEvent` | 动作运行结果和生命周期事件 |
| Animation | `ICombatAnimationContext` / `CombatAnimationContext` / `CombatAnimationSnapshot` | 动作系统可读写上下文和快照 |
| Animation Runtime | `CombatActionRuntimeModule` / `CombatWeaponTraceRuntimeModule` / `CombatAnimationDiagnosticsModule` | `RuntimeHost` 模块化动作、武器轨迹和诊断推进 |
| Weapon Trace | `WeaponTraceFrame` / `WeaponTraceSegment` / `WeaponHitOnceKey` / `CombatWeaponTraceEvaluator` / `WeaponTraceQueryBuilder` | 武器轨迹采样、blade/proximal 查询构建和 hit-once key |
| Baked Weapon Trace | `CombatBakedWeaponTraceReferenceFrame` / `CombatBakedWeaponRuntimeProfile` / `CombatBakedWeaponTraceAdapter` | 将 MxAnimation bake reference 与运行时 weapon / character profile 合成确定性 `WeaponTraceFrame` / `CombatCapsuleQuery` |
| Trace Provider | `ICombatActionTraceProvider` / `CombatActionTimelineTraceProvider` | 从动作时间轴读取 weapon trace |
| Diagnostics | `CombatDebugSnapshot` / `CombatDebugSnapshotBuilder` / `CombatHitExplain` / `CombatQueryTrace` | 运行时可读诊断快照和命中 explain |
| Replay | `CombatReplayInput` / `CombatReplayRecorder` / `CombatDesyncDump` | replay 输入记录和 desync dump |
| Gameplay Bridge | `CombatEntityGameplayMap` / `ICombatEntityGameplayMapReader` | generation-safe CombatEntityId ↔ GameplayEntityId 双向映射 authority，及供 noEngine sibling bridge 使用的 NoAlloc Combat→Gameplay 只读 seam |
| Gameplay Bridge | `CombatGameplayEventBridge` / `CombatCohesionLimitAdapter` / `CombatDamageApplicationSystem` / `CombatGuardArmorApplicationSystem` / `CombatGameplayAuraBuffAbility` / `CombatGameplayAuraBuffSystem` | 将 Combat 命中结果、动作支撑线、防御压力、护甲完整度、破聚事实和 caster-following aura range refresh 桥接到 Gameplay / Buff；正式 CharacterAction 反应由 CharacterControl bridge 单独处理 |

## 时间域与 RuntimeHost bridge

Combat 的权威时间域是固定模拟步，而不是 Runtime frame。

| 名称 | 所属域 | 契约 |
|------|--------|------|
| Runtime frame | `MxFramework.Runtime` | `RuntimeTickContext.FrameIndex`，只表示 Host tick 序号和 command / replay / diagnostics 通用帧键。 |
| Runtime tick delta | `MxFramework.Runtime` | `RuntimeTickContext.DeltaTime`，只作为 Combat-owned bridge 的 accumulator 输入。 |
| Fixed simulation step | `MxFramework.Combat` | `1 / CombatStepConfig.TicksPerSecond` 秒的固定逻辑步，每个 step 执行一次 Combat authority 推进。 |
| Combat frame | `MxFramework.Combat` | `CombatFrameClock.Step()` 产生的权威战斗帧，驱动 action、weapon trace、motion、hit resolve、Combat hash 和 replay。 |
| Ability timeline frame | `MxFramework.Gameplay` | Ability phase timeline 的整数帧；接入 Combat 时消费 Combat fixed step 数。 |

Runtime `DeltaTime` 到 Combat fixed step 的 bridge 归 Combat 所有，可以注册进 `RuntimeHost`，但不能下沉到 `MxFramework.Runtime` 源码。Bridge 累计 Runtime tick delta，按 `CombatStepConfig.TicksPerSecond` 切 fixed step，并在单个 Runtime tick 内最多执行 `CombatStepConfig.MaxStepsPerUpdate` 个 step。

Bridge 语义：zero-step 不推进 `CombatFrameClock`；multi-step 每个 step 都产生独立 Combat frame；超过 max-step 的剩余 accumulator 在没有显式 drop / clamp policy 前保留到后续 Runtime tick。`RuntimeTickContext.FrameIndex -> CombatFrame` 是 lossy / non-bijective 映射，不能作为 `CombatActionRuntimeModule`、`CombatWeaponTraceRuntimeModule` 等 RuntimeHost 集成模块的权威推进契约。

## Physics v0

### Animated character hurtboxes

`CombatPhysicsWorld` supports AABB, sphere, capsule, and OBB target colliders. All ray, sphere, capsule, AABB, sector, and OBB queries use the same broadphase and deterministic narrow phase for these target shapes. Collider `Priority` is carried into `CombatQueryResult.TargetPriority`; equal-distance hits sort by descending target priority and then by stable entity/body/collider identity.

Character projects must keep movement obstruction and damage reception separate. CharacterTest uses its Dynamic Character layer for movement/navigation and the dedicated `CharacterHurtbox` layer for damage queries. A character with explicit body colliders receives damage only through those hurtboxes. Older packages without explicit body colliders receive one compatibility AABB identical to their previous damage bounds.

Animated hurtboxes are offline-authored data. Unity Editor samples formal extracted `.anim` clips at 60 FPS and writes `generated/character_hurtbox_bake_catalog.json`; FBX files remain source files only. Runtime selects package-local catalog frames from deterministic action/locomotion state, blends locomotion and action transitions, transforms the quantized root-space shapes by authoritative root position/Yaw, and updates all colliders through one batched topology invalidation. Runtime must not read `Animator`, bones, `Transform`, `Renderer`, FBX sub-assets, or Humanoid APIs. Missing package, clip, collider, or frame evidence for an explicit hurtbox fails closed.

`BeginColliderBatchUpdate()` / `EndColliderBatchUpdate()` are the no-allocation bulk-update boundary. A complete character hurtbox update changes topology at most once. OBBs are included in stable snapshots/copies, replay physics-world cloning, debug drawing, surface material resolution, query statistics, and deterministic result hashing.

CharacterTest CD-M0 在每轮 Hurtbox 批量同步完成并取得最终 `CombatPhysicsWorld.Revision` 后，封存内部 `CharacterTestDamageGeometrySnapshot`。其中 actor aggregate bounds 与稳定 collider 元数据索引只从本轮写入 CombatPhysics 的同一组确定性形状生成，按稳定 ID 排序，并同时支持 logical collider ID 与 `CombatColliderId` 查询；角色移除、清空或下一轮同步会使旧视图失效并移除对应物理 body。该视图是 CharacterTest 内部合同，不新增 Combat 框架公共 API。

CharacterTest 的 direct-hit 实际执行已改为消费按 source/action 隔离的 Trace Plan sample。共用 sampler 覆盖 `WeaponTrace`、`ManualCapsule`、`CasterBody` 和 `BakedSocketCapsule`，以定点 source pose 生成稳定 sample ID、完整 Active-frame 顺序和保守 `SourceActionBroadphaseBounds`。Plan view 绑定 RuntimeFrame、CombatPhysics revision、source/action identity、seal revision 与 buffer generation；任何不匹配都拒绝读取。预演构建入口只接受调用方提供的连续 Active-frame pose 与已解析局部几何，不读取或推进可变 Runtime。CD-M0 尚不归约 Attack Opportunity，也不改变 Planner；根距离仍只能作为软排序信息。

### CharacterTest CD-M1 Preview / authority contract

CD-M1 在 `PreSimulation` 使用上一已完成模拟帧的 sealed CombatPhysics/Hurtbox snapshot，并独立构建 preview plan；当前帧尚未生成的 actual plan 不可复用。首版只预测 static target：目标姿态、队伍、状态、mask、world classification、blocker 顺序和 multi-hit policy 在该快照内冻结，目标未来运动不被预测。`CurrentPose`、`TurnInPlace`、`RequiresTranslation` 的归约只允许使用同一 source pose/yaw authority；CD-M3 已让正式 authored Anchor 生成的量化 source pose 进入同一 evaluator 的 `CandidatePose` 分支，未新增第二套命中判断。

Preview 与 actual 共享 M0 sampler、root-motion/socket/baked source、target qualification、blocker ordering、direct-hit policy 与 damage eligibility 入口。正式 `TraceId` 保持 binding/profile 的执行语义；独立 `PolicyEventId` / stable event identity 用于多 HitEvent 精确匹配，不能把策略元数据塞入 Query TraceId。缺失或非法 `hitEvent.damage` 在两条路径都失败关闭；M1 Preview 只冻结 HitZone identity 并保留既有 `Default/body` fallback，不计算 actual package/profile 的伤害、硬直或冲量倍率，也不提前实现 M2 Zone 严格语义。未知 world classification、stale source pose/facing revision、缺失 policy 或不兼容 hurtbox 必须 fail closed，并保留按 source 的稳定 decision evidence。`CastAbilitySlot` 等非 direct-hit 特殊动作不使用该 evaluator，而走显式 `CompatibilityLegacyRange` 兼容路径。

formal CharacterTest network snapshot 的 schema 24 保存 hit policy ledger；schema 25 同时保存活动 HitTrace window、source-instance mapping、next instance/window IDs 与 legacy hit-once state；schema 26 保存独立 source pose/facing revisions；schema 27 保存 CasterBody previous-center sweep map。稳定排序后的数组参与 full/owned/non-owned hash，rollback/replay 恢复活动窗口与可恢复的跨 tick sweep 状态后继续真实生命周期，不依赖重放已经发生的历史 track event。previous-center 允许合法稀疏：首次成功采样前或 CasterBody 解析失败时可以不存在。

CD-M2 在 sealed `CharacterTestDamageGeometrySnapshot` 之上生成只读 `CharacterTestDamageZoneSnapshot`。Runtime 只消费 Compiler 输出的 binding v2，不直读 Authoring JSON；成员以稳定 Zone/HitZone/logical Hurtbox/collider identity 聚合，Hittable、Aimable、Approachable 保持正交。binding/member identity 变化分别推进稳定 revision，坐标或 world authority 不匹配使旧视图 stale；热路径构建与查询 warmed 后为 0 B GC。Attack Opportunity 只在当前动作要求的正交属性成立时消费 Zone，非法 binding 或空/禁用成员 fail closed；旧 v1 binding 保持 M1 Hurtbox 行为。

CD-M3 的 authored Engagement Anchor 不成为新碰撞权威：长期位置只由 sealed target ModelRoot pose 和局部毫米/yaw 毫度投影；站立、静态阻挡和动态占用全部查询现有 `CombatPhysicsWorld.QueryCapsule`。target 自身允许重叠，其他动态角色归为 `Occupied`，静态 body 归为 `Blocked`；world/pose/Zone/Anchor/phase/graph witness 任一失配均 fail closed。

CD-M4 不新增 Combat 命中或碰撞权威。联合 selector 只预览已 sealed 的 Hurtbox/Zone、现有 direct-hit trace 与 CandidatePose/path 结果；只有最终胜者 Commit 后，实际 CharacterAction/Combat trace 与 `HitResolveSystem` 才决定命中。没有可选 joint leaf 时 Planner 可以继续非攻击移动，但 PrimaryAttack 仍由同一 evaluator 失败关闭；预算截断、cache 和 overlay 都不能伪造 Combat 可达性。

`CombatPhysicsWorld` 是当前战斗物理权威入口。已实现能力：
- `UpsertBody` / `RemoveBody` / `TryGetBody` / `MoveBody` / `SetBodyPosition` / `Clear`。
- `UpsertAabbCollider` / `RemoveCollider` / `TryGetAabbCollider`。
- `Query` 支持 Ray、Sphere、Capsule、AABB、Sector 和 OBB query，并对 AABB、Sphere、Capsule 与 OBB target collider 使用确定性窄相位。
- `QueryBatch` 按 query header 和 source index 稳定排序。
- `ExplainQuery` 输出 raw candidate、dedup、post-filter、hit / miss / filtered row 和 broadphase cell 统计。
- `CopyBodiesTo` / `CopyAabbCollidersTo` 提供稳定排序快照。
- `Revision` / `CreateStats()` 用于生命周期和诊断变更检测。
- `CalculateCapsuleDistance` 返回 capsule→AABB/Sphere/Capsule/OBB/compound leaf 的确定性表面净空与双侧 witness；Separated 为正 gap，Touching/Overlapping 为 0，Invalid/Unsupported 无距离。该 API NoAlloc，不替代现有 penetration-oriented `CalculateCapsuleSeparation`。

CD-M5 使用上述距离构建 CharacterTest internal Actor + LogicalHurtbox 复合目标。它只影响接敌、机会预判和 AimPoint；实际 HitTrace/HitResolve 仍可命中轨迹首先接触的其它合法身体部位。

查询结果排序遵守源码中的稳定比较：query header、distance、target entity、body、collider 等字段确定性排序，避免依赖集合插入顺序。

`CombatPhysicsWorld.CalculateCapsuleSeparation(...)` 提供 capsule 对 AABB、sphere、capsule 和 OBB 的单目标 narrow-phase 事实；`QueryCapsuleSeparations(...)` 向调用方列表追加所有通过 layer/source filter 的目标结果，包括 `Separated`、`Touching`、`Overlapping`、`UnsupportedShape` 和 `InvalidShape`。接触法线始终从 target 指向 source，penetration 非负，contact point 位于 target surface；相离或失败结果不伪造 contact。完全重合使用固定 `+X` fallback，AABB 内含或多轴等距按 `+X/-X/+Y/-Y/+Z/-Z` 固定顺序决胜。OBB 必须提供现有 fixed-point 合同下的正交单位基，非法基或中间算术溢出返回 `InvalidShape`。

分配约定：上述单目标 API 为 `NoAlloc`；bulk API 使用 caller-owned `List<CombatShapeSeparationResult>`，容量 warm-up 后为 `NoAlloc after initialization`。bulk 结果按 source query、target entity/body/collider/compound leaf、shape/status、penetration/priority 和几何字段完整稳定排序，不依赖 collider 或 compound leaf 的插入顺序。API 只报告几何事实，不施加位移、速度、伤害或 Gameplay 状态。

`CombatPhysicsWorld.QueryGroundProbe(...)` 是单结果 static surface 查询。调用方必须提供向下的 signed-normal 单位方向、正 distance、`0..100 m` radius/distance、非空 static layer mask 和 `0..1` minimum slope cosine；Combat 只消费该 mask，不定义项目层号或自行把动态角色分类为 static。`radius=0` 复用现有 ray narrow phase；`radius>0` 仍复用同一 Combat broadphase，并对 AABB、sphere、capsule 和 OBB target 做解析 fixed-point sphere cast，不采样、不读取 Unity Physics。非零半径 hit 的 `Distance` 是 probe 球心沿归一化 direction 到首次接触的轴向行程，`Point` 是该时刻 target 原始表面点，`Normal` 从 target 指向 probe 球心。

ground probe 直接选 single-best，不创建 hit buffer。候选先按 raw axial distance、target priority、entity/body/collider/shape identity，再按 primitive/compound leaf 标记与 raw point/normal 完整决胜；公开 hit comparer 对量化后的 distance、priority、surface entity/body/collider/leaf/shape、point、normal、slope 和 material raw fields 给出全序。输出 point/distance 量化到 `1 mm`，normal 各分量量化到 signed `1/32767` 后重归一，material raw fields 与 surface identity 原样保留。失败仅返回 `InvalidInput`、`NoHit`、`TooSteep`、`OutOfRange`、`UnsupportedShape`、`SurfaceIdentityUnavailable` 或 `SurfaceMaterialUnavailable`，且 `HasHit=false`；single-best 没有容量失败状态。

`CombatPhysicsWorld.TryGetSurfaceMaterial(in CombatQueryResult, out CombatSurfaceMaterial)` 按既有 entity/body/collider/shape identity 精确回查 primitive 或 compound leaf；找不到、重复或 primitive/compound identity 冲突时 fail closed，不使用 fallback material。两项 API 都不判断 Character eligibility、IK、binding/source stale 或 Gameplay 状态；world broadphase/compound 容量 warm-up 后为 `NoAlloc`。

CharacterTest LCC-M1 是该事实 API 的项目级消费者，不新增 Combat 通用接触求解抽象。它把编译后的最多 4 个 ModelRoot Body Presence volume 注册到独立 layer/body-id 区间，以 28 raw / 28 unordered pair / 224 directed constraint 固定容量稳定构建三轮 soft-pressure，并只把有界结果写入既有 contact velocity，交给下一次 kinematic motor 静态 sweep。Presence layer 不进入 motion/static obstacle、Hurtbox、damage、projectile 或 navigation mask；`BlocksCharacters=false` 的实体不参与 Presence，`CanBePushed=false` 只禁止目标速度而不删除接触观测。该路径不调用直接 Transform displacement，也不把每 tick 接触包装为通用 Interaction event。

CharacterTest LCC-M2 仍是项目级组合，不向 Combat core 增加通用伤口框架。`CharacterTestBodyPartStateComponent` 以固定容量保存 Closed/Open/Consumed 与 Intact/Broken 双轴权威状态；`CharacterTestBodyPartDamageAuthorityBridge` 只对具备显式部位证据且 source-policy 允许的 accepted damage 在 authority frame 末提交。触发 Open/Broken 的命中和同帧后续命中都读取 frame-start 旧倍率，下一 authority frame 才使用新倍率；default fallback、无部位范围、status tick、无定义角色和 rejected hit 不累计。桥接层提供 accepted Action/Ability/Interaction Focus fact 的typed ingress：它只消费frame-start Open，Consumed为终态并在respawn重置；当前生产链没有调用方，正式包tag均为空，任何非空正式tag都必须先接真实accepted fact并补Playable。动态投影保留authored Hittable/Aimable/Approachable，只改变Focus可见性、priority和lock选择；不改root-XZ distance、GeometryInReach或pose path。

CharacterTest LCC-M3 在同一 Body Part authority 上增加 `Broken -> Severed` 单向终态与一次性 transition event；Intact 直接 Severed、同帧刚 Broken 后直接 Severed、重复 trigger 和 tag/source 不匹配均失败关闭。Severed 的 tail member leaves 由既有稳定 collider binding 派生为 inactive，`zone.tail` 的 authored identity 保留但变为 unavailable，相关 reservation/Planner/action dependency/后续 Trace 由只读 state/revision witness 清理或拒绝；其它 Zone、root-XZ distance、bite 与非依赖动作不受影响。CharacterTest schema32 在 schema31 Body Part authority state/revision/accepted-hit witness 上允许 Severed，不保存 active leaves、Zone/priority、presentation、VFX 或 view-only limb。当前没有生产 `StageAcceptedSever` 调用方或玩家输入路径；正式 after 证据来自 Editor-only drive，因此该能力仍是 Runtime Slice，不是完整 Playable severance。

CharacterTest LCC-M4 是 `QueryGroundProbe` 的项目级 authority consumer，不扩展 Combat 命中、Hurtbox 或接触真源。每个 eligible limb 每个 authority tick 最多执行一次 single-best static probe；4 个 probe 的量化 point/normal/surface identity/material 进入现有定点 two-bone solve 与唯一 resolved pose frame。`NoHit`、`TooSteep`、`OutOfRange`、非法/不支持形状及 source/binding stale 都保持 typed fail-closed，并按项目层有界历史恢复到 base pose；Combat API 不判断 action、motion、death 或 Severed eligibility。Hurtbox、`BakedSocketCapsule` actual/preview 与 Planner 只消费同一 frame/revision 的 authored attachment delta，不读取 Unity Physics、Animator、bone 或 Transform，也不把测试专用可视标记变成 Combat authority。

CharacterTest LCC-M5 仍是项目级 soft-pressure consumer，不改变 Combat Hurtbox 的伤害职责。它只按 geometry v7 明确列出的 logical collider identity，从第二次既有 Hurtbox `SyncAll` 后的同帧 `CharacterTestDamageGeometrySnapshot` 读取 Sphere/Capsule/OBB leaf，并调用现有 fixed-point capsule separation；不扫描或提升整个 Hurtbox layer，不进入 hard motion/static obstacle、navigation、projectile blocker 或 Active Trace。当前/previous witness 不新增 Combat 状态或网络 schema，Severed leaf 缺失即代表 source inactive。普通附肢接触只写有界 contact velocity/count 与 PresentationOnly phase fact，不发布 Damage/HitResolve/Trace。

### Gameplay Trigger Combat sensor consumer

`MxFramework.Gameplay.Triggers.CombatBridge` 是 Combat 的 noEngine sibling consumer，不属于 Combat core，也不让 Trigger core 反向依赖 Combat。它在 Gameplay RuntimeFrame 的 `Simulation/950` 用 unified `CombatPhysicsWorld.Query` 读取固定 world-space Sphere/Capsule/AABB/Sector，并经 `ICombatEntityGameplayMapReader.TryGetGameplayId` 复用现有 `CombatEntityGameplayMap` authority；该 interface 不增加注册、移除或第二套 map 所有权，现有 map 的 bijection 行为不变。

由于 unified Query 接收 caller-owned `List<CombatQueryResult>` 且没有 bounded/TryQuery overload，sensor 的 `MaxQueryHits` 是 scratch hard bound：调用前保守验证 `world.ColliderCount <= MaxQueryHits`，不能 query 后截断。List capacity 和 ID scratch 在初始化期固定；world 首次 topology/broadphase 建索引属于 Combat infrastructure warm-up，之后 bridge-owned Tick 不扩容。当前 owner/team/target mask filter 在 physics 查询中未生效，所以 19.3B catalog 明确拒绝非零值；`IncludeSourceEntity` 仍是有效且参与 hash 的 filter。底层 invariant exception 继续 fail-fast，不转换成 recoverable typed query failure。

## Level Foundation v2

`MxFramework.Combat.Levels` provides the noEngine data foundation for future level authoring. It does not provide editor UI, Unity scene baking, CLI authoring, dynamic platforms, triggers, or runtime level editing.

Implemented contracts:

- `CombatLevelDefinition.CurrentSchemaVersion` is `2`. It stores stable level identity, display name, separate static AABB and OBB lists, spawn points, and checkpoints; all collections are non-null canonical values.
- `CombatLevelAabbColliderDefinition` preserves stable/numeric identity, layer, body position, local min/max, and full raw `CombatSurfaceMaterial`. `CombatLevelObbColliderDefinition` additionally preserves local center, positive half extents, and raw world `AxisX/AxisY/AxisZ`; Level-built OBB collider priority is currently fixed to `0`.
- `CombatLevelValidator` performs cross-shape duplicate stable/body/collider checks and validates layer/material/range. OBB validation rejects invalid position/local center/half extents, zero or non-unit axes outside tolerance, non-orthogonal bases and non-right-handed bases; invalid input cannot partially mutate a world.
- `CombatLevelJson` accepts schema v1 only through explicit migration to v2 with an empty OBB list, always saves canonical v2, rejects missing/wrong-token/unknown/duplicate fields before DTO construction, and does not silently reinterpret a v1 payload containing OBB data.
- `CombatLevelHash.ComputeContentHash` uses separate AABB/OBB shape discriminators, sorts each list by stable identity, and covers every persisted OBB position/center/extent/axis/material field. A migrated v1 AABB level hashes exactly like its canonical v2 representation with no OBBs; list insertion order does not change the hash.
- `CombatLevelWorldBuilder` validates before any mutation, then creates AABB and OBB bodies/colliders while preserving identity, layer and material; Level-built collider priority remains the existing fixed `0`. `clearExisting=false/true` both retain strong no-partial-mutation behavior on invalid definitions.

CharacterTest uses `CharacterTestCombatLevelSceneAdapter` as the Unity-side consumer: official `CharacterTestScene` data comes from `GameManager.WorldObjectSceneCache`, which the M3 static-Box compiler converts to a transient Level v2 definition before runtime world construction. Identity transforms remain AABB; valid rotated/scaled Box transforms compile to OBB from the complete ancestor transform chain. Non-finite values, reflection, shear, degenerate/overflowing bases or invalid quantization fail closed. Transform is authoring input and the compiled Fix64 geometry, not Unity Collider state, is runtime authority.
`CharacterTestCombatPhysicsSceneBridge` and its editor preview remain as compatibility diagnostics for older scenes that still carry bridge/source components. The official scene path uses the GameManager scene cache and Level Editor validation instead of selecting a bridge MonoBehaviour.

CharacterTest Pathfinding Foundation PF-M2 的静态 navigation graph 是游戏层 internal 派生拓扑，不扩展 Combat public level schema，也不成为第二个碰撞权威。它复用 `FixVector3` 和 Combat layer mask 数值作为稳定 build metadata；PF-M2 查询本身不调用 CombatPhysics。PF-M3 在同一图上增加确定性有界 A*，其中 authored Edge 长度不得小于两端 Node 的几何距离，area/layer mask 只作为显式过滤 metadata；它仍不扫描或修改 CombatPhysics。PF-M4 已把 session graph 接入 CharacterTest navigation/perception：角色 clearance 取所有可玩 physics profile 的保守上界，direct 和 waypoint segment obstruction 均通过现有 `CombatPhysicsWorld.QueryCapsule` 及 world revision witness 派生，动态 segment 阻挡先走 L1 candidate steering。graph/path sample 不能越过 `CharacterTestCharacterMotionSystem`/CombatPhysics 权威直接移动实体，也不进入 Combat snapshot/hash/replay；rollback/reset 后只清空并重算派生 sample。

## Motion v1

`CombatKinematicMotor` 是当前 noEngine 角色运动入口。它消费 `CombatMotionState` 和 `CombatMotionInput`，输出 `CombatMotionStepResult`。

已实现能力：
- 固定帧水平移动、重力、跳跃、最大下落速度和 grounded 判定。
- `CombatMotionCapsuleProxy` 作为 capsule 角色代理。
- 通过 `CombatPhysicsWorld` 查询障碍，并按 `CombatMotionCollisionFlags` 报告 Grounded、Wall、Ceiling、BlockedX/Y/Z、IterationLimit。
- movement candidate 对 AABB、Sphere、Capsule 和 OBB 有显式窄相位；OBB 不使用其 `Min/Max` 字段冒充 AABB 接触。
- 可选把运动结果同步回 `CombatPhysicsWorld.SetBodyPosition`。

静态 OBB movement contact 的 Current 合同如下：

- `CombatMotionObbCast` 与 `CombatObbCastStatus` 是 `MxFramework.Combat` internal 实现，不是公共扩展点；`MxFramework.Tests` 仅通过 friend assembly 直接验证该 primitive。
- primitive 消费 world-space capsule proxy、起点、desired delta 和完整 `CombatPhysicsShapeKind.Obb`，返回尚未应用 skin 的保守 fraction、distance 与从 target 指向 capsule 的归一化 world normal。Motor 随后只在既有 safe-fraction 路径扣除一次 `CombatMotionConfig.SkinWidth`。
- separated cast 的生产入口固定使用最多 40 轮、16 raw contact threshold。internal core另有只供friend tests使用的小预算 seam，以真实循环耗尽验证`IterationLimitConservativeHit`，不改变生产预算。普通 hit、`PrecisionLimitConservativeHit` 和 `IterationLimitConservativeHit` 都是 fail-closed 阻挡结果；后两者不映射为 Motor 的 `CombatMotionCollisionFlags.IterationLimit`，该 flag 只表示 collide-and-slide 循环耗尽。
- initial touching/overlap 由 bool 与 typed status 共同表达：继续进入时阻挡，away/tangent 时不阻挡；非法 OBB、越过冻结定点容量或 impossible bool/status 组合 fail fast，Motor 诊断包含 target body/collider identity，不能静默当作 miss。
- OBB collision 继续复用现有 collision comparator、surface material merge、candidate cache、source/layer filter 和 remaining-delta loop。static topology rebuild、dynamic body incremental update、cold/warm cache 与 uncached world path必须给出完整相同的 `CombatMotionStepResult`。
- `CombatKinematicMotor.Step(...)` 在 caller-owned `CombatMotionStepBuffer` 与 prepared candidate cache 容量 warm-up 后维持 `NoAlloc after initialization`；当前 OBB closeout 覆盖 hit、miss、rotated、precision fallback 和 AABB/Sphere/Capsule/OBB mixed candidate。

静态坡面与Ground Snap的Current合同如下：

- `CombatMotionConfig.GroundMinNormalY`是walkable阈值，`GroundSnapDistance`是可选向下支撑距离；legacy constructor与generic default保持snap=`0`。`CombatMotionCollisionFlags.SteepSlope`和`GroundSnap`是additive位，旧bit值不变。`CombatMotionStepOptions.SuppressGroundSnap`用于typed jump/向上Root Motion/external intent，旧`Step`重载等价于未抑制。
- 普通`Wall`在先移动到skin-safe point后，以`1-safeFraction`保留该fixed tick尚未消耗的合法切向时间；Walkable、Steep和Ceiling继续使用原有collision-fraction合同。普通墙的remaining delta会同时对classification normal与OBB raw non-entering witness做strict raw后置，任一方向无法证明、overflow或无合法切向时保持fail-closed。两面真实正交墙仍会消除全部水平切向并停止。
- `CombatMotionStepOptions.WallSlideMinimumHorizontalSpeed`是可选caller-owned locomotion floor，单位为Fix64米/秒；default、旧单bool构造和旧`Step`重载均为`0`。正值只会在已有合法水平墙切向方向时，把剩余时间内的horizontal tangent恢复到不低于caller提供的速度；不会创建方向、不会改变vertical分量，也不会超过caller自己限定的locomotion值。Root Motion、Impulse、knockback或其它external motion若不显式传值，继续使用普通物理正交投影，不会被Motor自动补速。
- 分类顺序为Y-first：`normal.Y >= GroundMinNormalY`且向上时是walkable/ground candidate；向上但低于阈值时是`SteepSlope + Wall`；向下/侧向仍按ceiling/wall处理。walkable坡面不会仅因normal含X/Z就被误分类为wall。
- walkable接触把remaining motion投影到非进入平面并保留允许的上/下坡与切向分量；steep接触移除向内分量，不能通过普通输入、Root Motion或external request向上穿入/攀爬。fixed-plane/raw witness无法严格证明安全时继续fail closed。
- Ground Snap只在上一状态Grounded、主运动未找到walkable support、有配置距离、没有jump/向上vertical/root-motion/external意图且未设置`SuppressGroundSnap`时执行。它只应用安全向下Y距离、最多一次skin、使用earliest walkable support并保留typed identity/material；超过配置距离的真实边缘必须进入Airborne。
- ground-only downward support不会投影出横向移动；snap、step-up、bounce/elastic、friction、candidate cache和surface summary继续复用同一Motor结果。CharacterControl adapter在有typed upward intent时显式抑制snap，不能通过旧probe或Unity Transform建立第二套ground authority。
- CharacterTest正式配置把默认45°转换为`GroundMinNormalY raw=707128`，把snap固定为`0.10m / raw100000`，并在Offline、Core runner与Unity Transport持有physics的runtime clone中逐字段保留。Core snapshot mirror不持有physics，不能称为clone。
- CharacterTest same-frame motion diagnostic投影target identity/shape、normal、flags、classification、threshold、snap applied distance、requested/applied delta、resolved velocity与material；它是transient observation，不进入snapshot/hash/network。snapshot/replay恢复后由下一Tick重新计算。

当前限制：本合同只支持角色对静态有限平面OBB的kinematic slope/contact。AI typed movement若到达坡面会服从同一Motor，但Combat/CharacterTest不因此提供height-aware slope navigation。movable natural object不获得OBB slope dynamics。动态/旋转/moving platform、curve、Triangle Mesh、Terrain、MeshCollider、通用GJK/EPA、rigidbody和IK不在当前范围。Combat Level content hash尚未成为typed network compatibility handshake；CharacterTest的0.10m snap也不是generic Combat默认值。

`CombatKinematicMotorCandidateCache` 是可选 additive API，用于同一 fixed tick 内大量角色共享 kinematic motor broadphase 候选。调用方先执行 `Prepare(world, staticLayerMask, dynamicLayerMask)`，再把缓存传给 `CombatKinematicMotor.Step(...)` 的缓存重载。缓存 mask 必须覆盖 `CombatMotionConfig.ObstacleLayerMask`；覆盖不足、world/topology 不匹配或缓存未准备时，motor 自动回退旧 `CombatPhysicsWorld` broadphase 路径。该缓存不改变窄相、step-up、surface material、排序或 world sync 语义。

分配约定：`CombatKinematicMotorCandidateCache.Prepare(...)`、缓存查询和缓存 `Step(...)` 重载在集合容量 warm-up 后为 `NoAlloc after initialization`。`Clear()` 是低频生命周期操作；`Invalidate()` 保留容量并标记索引失效。静态层前提是 body/collider 在一次 prepared tick 内不移动；如果静态层 body 移动，调用方必须 `Invalidate()` 或重新 `Prepare(...)`。当 world、mask、topology 或 collider shape 未变化且仅 `BodyRevision` 变化时，`Prepare(...)` 优先通过 `CombatPhysicsWorld` 的 moved-body snapshot 增量更新动态索引；如果 snapshot 已被普通 query 路径消费但 topology/body map 仍匹配，则改用完整 body snapshot 做差异增量更新，不重建 broadphase。只有动态 body map 不匹配、拓扑变化或检测到静态层 body 变化时才回退对应 rebuild。动态层 body 在 motor 成功 `SetBodyPosition` 后由缓存增量更新，保证同一 tick 后续 motor step 能看到已移动的动态 body。

`CombatKinematicMotorCandidateCacheStats` 和 `CombatKinematicMotorQueryStats.CandidateCacheHitCount/FallbackCount` 用于验证缓存命中、回退、动态 body 增量更新、动态重建次数、增量 prepare 次数和最近 moved-body 数量。它们是诊断快照，不参与运动结果语义。

### Surface Material v0

`CombatSurfaceMaterial` is the noEngine contact material value used by `CombatPhysicsAabbCollider`, `CombatMotionCollision`, and `CombatMotionStepResult`. It carries stable `CombatSurfaceMaterialKind` / `StableId`, `Friction`, `Elasticity`, and optional fixed bounce height. `HardSlick` uses low non-zero friction as an ice-style traction limit for consumers that model grounded acceleration and braking.

Legacy friction-only collider and motion constructors remain compatible: they map to `CombatSurfaceMaterialKind.Default` with the supplied `SurfaceFriction` and `SurfaceElasticity = 0`.

`CombatKinematicMotor` selects a deterministic per-step surface material summary. Grounded non-step-up contacts choose the highest friction material for ground damping; non-ground contacts choose the strongest elastic material. Elastic response applies only when contact speed is at least `CombatMotionConfig.MinElasticImpactSpeed`, so resting grounded probes do not bounce.

### Physical Contact Events v0

`CombatPhysicalContactEvent` 表达一帧物理接触事实，包含 `CombatFrame`、sequence、kind、phase、source/target body/collider、source slot、contact pair key、hit point、normal、surface material、relative / normal / tangential speed、pressure、intensity、contact point id、body part id 和 activation mode。由 Combat authority 生成的事件可以进入 replay/hash 诊断；由项目表现层生成的事件只能作为 Audio/VFX/UI 输入，不回写 Combat authority、SaveState 或网络状态。

`CombatPhysicalContactEventBuffer` 是 caller-owned 单帧固定容量 buffer，提供 `Clear` / `Add` / indexer / `Count` / `DroppedCount` / hash contribution。`Add` 在容量满时只增加 `DroppedCount` 并返回 `false`，不抛异常。该 buffer 设计为高频路径复用，构造后 `NoAlloc after initialization`。

`CombatPhysicalContactSensorRunner` 消费 caller-owned `CombatPhysicalContactSensorSample` 列表，用 `CombatPhysicsWorld.QuerySphere` 生成 `Started` / `Stayed` / `Ended` 接触 phase。runner 维护每个 sensor id 的上一帧命中 body/collider、point、normal 和 material；新增 sensor id 可能增长内部 state dictionary，steady-state 在预设容量足够时为 `NoAlloc after initialization`。材质解析优先从命中的 collider 反查 `CombatSurfaceMaterial`，失败时使用 sample fallback material。

`CombatBodyPartContactPointDefinition` / `CombatBodyPartContactPointSample` / `CombatBodyPartContactPointRuntime` 是确定性部位碰撞点入口。调用方提供角色权威 body pose、local offset、半径、layer mask、contact point id、body part id 和 `CombatBodyPartContactActivationMode`；runtime 按 source order / entity / body / contact point / body part 稳定排序，用 `CombatPhysicsWorld.QuerySphere` 生成 `BodyPartContact` 的 `Started` / `Stayed` / `Ended`。`Disabled` 点在上一帧接触时会发出一次 `Ended` 后停止查询；`ActionWindow`、`ExplicitState`、`MotionState` 的开关必须来自 deterministic action frame、replayable state 或 Combat motion state。

v0 sensor 只提供 sphere probe。Combat-authority 用法不读取 Unity Animator、脚骨、Rigidbody 或 Unity Physics；项目层若为了表现效果读取 Animator/socket/bone，必须明确标记为 PresentationOnly，不得进入 replay/hash/save/network authority。新逻辑必须使用 `BodyPartContact`，由 consumer 再把 foot / claw / hand / knee / tail / weapon mount 等 body part 映射到音频、VFX、战斗判定或环境触发。`MxFramework.Audio.Physical` 是当前第一个 consumer：只播放 `Started` contact 的 one-shot，并用 pair/slot/contact point/body part cooldown 防止持续挤压刷音。

Physical Audio 的双方声学属性、Basis Sound 选择和播放参数属于 Audio 表现层，不扩展 `CombatPhysicalContactEvent` 的权威职责。CharacterTest 当前 adapter 把配置脚部碰撞点产生的 `BodyPartContact Started` 统一映射为 `Footstep/Step`，接触速度只进入响应参数，不再根据速度切换为专用 Landing 事实；通用 resolver 仍兼容显式 `Landing` 输入。contact/weapon facts 与项目内稳定材质映射会合成为 Audio-owned `PhysicalAudioInteractionFact`，并在有效 Physical Catalog 下解析 Footstep、Landing、ObjectImpact、WeaponHit/Block；fact、resolver trace 和 FMOD 结果不回写 Combat authority。武器、身体部位、装备或 Collider 的动态声学 property provider，以及 Air Displacement 动作 bridge 尚未接入；Air Displacement 不得伪造成 Combat Collider contact。接受设计见 `Docs/AUDIO_PHYSICAL_EXPRESSION_DESIGN.md` 和 ADR-007；Current Combat API 仍以本节已实现字段为准。

## Hit Resolve v0

`HitResolveSystem` 对 `HitCandidate` 执行结算，调用方提供 consumed hit-once key set 和结果列表。

已实现能力：
- hit-once 去重，重复命中输出 `HitResolveKind.Duplicate`。
- owner / self damage 防护。
- `ITeamRelationProvider` 支持 same-team / friendly / hostile 判定和 friendly fire 配置。
- `IHitTargetStateResolver` 支持动态目标状态覆盖候选状态。
- `IHitTargetStateCandidateResolver` 是可选的候选级状态扩展：在 hit-once 写入前读取完整 `HitCandidate`，不得修改或消费世界状态。旧 entity-only resolver 保持原行为。`HitCandidate.IncomingDirection` 表示生产者两帧物理采样中的世界位移，方向朝向接收者；零值表示没有可靠运动见证，不得替换为接触法线、击退或攻击者当前位置。既有构造器保留，新的重载允许明确传入位移。
- CharacterTest 普通姿态防御使用水平正面半空间（左右90度边界包含在内），仅在可靠来袭位移与朝向满足条件时保留 actor 的 Blocking；没有水平运动见证则普通命中。静态环境的 Blocking 表示物理阻挡，保留原语义。该策略不新增 GuardPressure 或伤害公式，也不替代 HP／Scatter owner。默认弹体中心扫掠携带实际起终点差；tip sweep、blade 与 proximal 可以使用生产者两帧见证，blade／proximal 在接触最近的材料点计算位移。自定义扫掠和静止范围不得伪造来向。CharacterTest 烘焙轨迹的来向使用本模拟帧开始／结束的权威根位置、转向和 socket offset 见证；帧号与实体实例须匹配，缺少见证输出零值。该见证只供来向判定，既有碰撞查询形状不因此改写；真实角色装配和原生运行验收仍须单独完成。
- `HitTargetStateFlags` 支持 Alive、Invincible、Parrying、Blocking、SuperArmor 和 Unhittable。`Unhittable` 表示目标仍可锁定且仍有物理实体，但本次重叠不成立为命中；它与命中后免疫伤害的 `Invincible` 是两个合同。
- `HitResolveKind.Evaded` 在 hit-once key 写入之前产生，不消耗本次 hit-once；窗口结束后，同一仍有效的攻击窗口可以再次命中。Evaded 不产生伤害、硬直、击退或命中表现，Projectile 也不因该结果被消费。
- `ICombatEventDispatcher` 可接收 resolved / blocked 事件。

`HitResolveKind.Blocked` 保留本次 incoming damage 到 `HitResolveResult.Damage`，用于 bridge 层作为 GuardPressure 输入；但 `HitResolveResult.IsAcceptedDamage` 仍只在 `Kind == Damage && Damage > 0` 时为 true，因此 Blocked 不会被旧 HP 应用路径当作已接受伤害。

`HitTargetStateContributionStore` 以 `(CombatEntityId, actionInstanceId, stableEventId)` 为贡献 identity，`ResolveFlags` 返回该实体所有临时贡献的按位 OR；`Alive` 仍由生命周期 owner 管理。`RemoveInstance` 只清理指定实体和动作实例的全部事件，保留同实体其他实例及其他实体；`ClearEntity` 清理指定实体的全部实例；`Count` 统计贡献条目，`Clear` 同时清空贡献与合并状态。

分配约定：贡献、实体及批量清理匹配键数量的高水位增长允许 `LowFreqAlloc`。批量清理复用 store-owned 匹配键列表，先收集再删除，避免修改正在枚举的字典；筛选不创建捕获委托。列表容量预热后，`Upsert` → `Remove`、`Upsert` → `RemoveInstance`、`Upsert` → `ClearEntity`、无匹配的 `RemoveInstance` 和空 store 清理均为 `NoAlloc`；`Clear` 保留内部集合容量，后续同高水位循环也维持该合同。调用方按 store 的原有串行使用方式执行操作，缓冲区不跨 store 共享。

## Shields v1

`MxFramework.Combat.Shields` 提供 noEngine 物理防护盾权威状态。护盾是绑定 owner 朝向和局部偏移的 Combat object，用 `CombatPhysicsWorld` body / collider 表达可被 projectile 和 weapon trace 查询的遮挡体；它不进入角色运动 obstacle mask，因此不阻挡移动。

权威状态使用 `CombatShieldPotencyTriplet(Motion, Cohesion, Scatter)`，派生 `ShieldPressure = max(0, Motion + Scatter - Cohesion)` 与 `CohesionBreakRisk = ShieldPressure / CurrentCohesionLimit`。状态段为 `Stable / Pressed / Cracked / Critical / Broken`，上行阈值为 `35% / 65% / 85% / 100%`，下行滞回线为 `30% / 60% / 80% / 95%`。`Broken` 后不会自然恢复；`CombatShieldRuntime` 会移除绑定 collider，并保留 broken 状态进入 hash/save/debug。

已落地契约：
- `CombatShieldPotencyComponent` 保存 shield entity、owner entity、body/collider id、基础三势、当前三势、基础/当前凝聚限、恢复参数、最后受击帧、状态段和 broken 标记；不新增普通角色 AttributeId。
- `CombatShieldImpactRequest` 是 projectile、weapon trace 或其他 Combat 命中施加到护盾的 transient 输入；`CombatShieldImpactMapping` 提供 `ImpactPower` / damage 到三势增量的确定性映射。
- `CombatShieldRuntime.TryApplyImpact` 消费冲击并发布 `CombatShieldBlockEvent`、`CombatShieldPotencyChangedEvent` 和 `CombatShieldBreakEvent`；`Tick(frame)` 处理排队冲击和延迟恢复。
- `CombatShieldRuntime.ReadStateView(moduleId)` 提供按既有顺序排列的借用状态视图，供同步 hash 读取；初始化、容量准备后无分配。下次读取或 runtime 修改即可失效，不能保留为历史或跨异步使用；保留数据继续使用 `CreateStateSnapshot()`。
- `CombatShieldOcclusionFilter.FilterWeaponTraceCandidates` 使用 caller-owned `List<T>` 在普通 `HitResolveSystem` 前裁剪同一 trace 中被最近未破护盾遮挡的 owner 目标；`TryFindNearestShieldHit` 供 projectile resolver 先识别最近护盾命中。
- `CombatShieldRuntimeHashContributor` 写入 module id 和所有 shield authority fields；`CombatShieldRuntimeSaveStateProvider` 保存/恢复 schema version、三势、状态段、broken 标记和 collider 绑定。

分配约定：impact、trace occlusion 和 projectile shield hit 识别属于高频路径，集合容量 warm-up 后按 `NoAlloc after initialization` 使用；调用方必须复用结果列表，不在每次命中中引入 LINQ、临时集合、字符串拼接、反射查找或异常流程控制。

## Hit Event Config v1

`CombatHitEventConfig` 是 noEngine 的通用命中配置，供武器 `StartHitTrace`、projectile direct hit / area pulse 和后续 on-hit producer 共享。它不直接执行查询、扣血或选择表现，只声明“这次命中事件允许命中什么、怎样处理环境命中、同一事件最多接受多少次有效命中，以及这次命中的基础伤害属性”。

已落地字段：
- `targetTypes` 用 `CombatHitTargetTypes` flags 表达可命中对象：`HostileCharacter`、`FriendlyCharacter`、`Self`、`ThirdPartyCharacter`、`DamageableObject`、`WorldWall`、`WorldGround`、`WorldStatic`。`NeutralCharacter` 只作为旧数据别名读取，新的 authoring 应写 `ThirdPartyCharacter`。项目组合根把这些类型解析成 `CombatPhysicsLayerMask`；authoring 首屏不再直接维护 raw layer mask。
- `environmentHitPolicy` 当前支持 `None` 和 `BlockedPresentation`。墙面 / 地面 / 静态场景命中在 `BlockedPresentation` 下输出 blocked / trigger-only 语义和后续触发点，但不造成 HP damage。
- `stableEventId` 为 telemetry、debug 和后续 on-hit effect 提供稳定 identity；它不是 damage profile 或 hit presentation profile。
- `maxAcceptedHits` 与 `hitSameTargetPolicy` 是通用命中次数约束，武器窗口和 projectile 穿透都复用这组语义。

`CombatHitDamageConfig` 是 `CombatHitEventConfig.damage` 的通用命中伤害值对象，包含 `damageTypeId`、`baseDamage`、`sourceAttributeScales`、`impactPower`、`launchPower`、`tags` 和 `analysisCategory`。缺省来源属性系数为空，即攻击系数 0。CharacterTest 各正式伤害来源统一按 `CharacterTestSoftDefenseV1` 结算：`round((baseDamage + Σ来源属性×系数) × (1 - 0.6×防御/(防御+50)) × 外部倍率)`，结果非负且中点远离零。旧 `formulaId` 仅供迁移读取，新作者数据不写出，运行时不按事件公式编号选分支。`damageTypeId` 用于日志、分析、筛选和后续扩展；`impactPower` 与 `launchPower` 分别控制冲击和击飞，不加入 HP 公式。`tags` 按稳定排序去重并参与 equals / hash。

正式规则：CharacterAction / Projectile / CharacterTest 伤害配置只读取 `hitEvent.damage`。`damageProfileId` 和旧顶层 damage 字段会被 importer 判为配置错误；`areaHitEvent.damage` 可省略并继承 direct hit damage。

## Projectile v0 / Motion + Impact Policy

Projectile motion is classified into three runtime categories. `Kinematic` covers `Linear`, `BoomerangReturn`, `SwayForward`, `HomingToTarget`, and `BezierToTarget`: these are deterministic authored paths or target-following paths without gameplay gravity. `DesignedArc` is the existing `ArcToTarget`; it remains a designer-controlled curve driven by `motion.arcHeight`, not physics gravity. `BallisticArcToTarget` is the gameplay-gravity category: runtime uses `motion.speedPerFrame` as max initial speed, resolves a target point, chooses the range-optimal launch angle for the current height delta unless `motion.maxLaunchAngleTangent` caps it lower, solves the actual per-shot launch speed, and then samples `origin + initialVelocity * age + 0.5 * gravityVector * age^2` with fixed-point math. Same-height physical maximum range is `speedPerFrame^2 / gravityPerFrameSquared` when uncapped; explicit launch angle caps reduce that range. `aim.maxRange` is ignored/normalized to zero for ballistic motion, and `lifetimeFrames` is a safety/expiry cap rather than the range source.

`motion.gravityPerFrameSquared` and `motion.maxLaunchAngleTangent` are resolved deterministic gameplay values stored in the runtime projectile definition. Gravity is only valid for `BallisticArcToTarget`, must be positive there, and is normalized to zero for other motion kinds. `motion.maxLaunchAngleTangent=0` means automatic range-optimal angle; same-height automatic angle is 45 degrees. Projectile tick never reads Unity `Physics.gravity`, profile strings, Unity objects, or authoring data; collision remains the existing `CombatPhysicsWorld.QueryCapsule(old,new)` sweep path.

`MxFramework.Combat.Projectiles` 提供 noEngine 通用飞行体运行时。motion v1 支持 `Linear`、确定性 `ArcToTarget`、`BoomerangReturn`、`SwayForward`、`HomingToTarget`、`BezierToTarget` 和 `BallisticArcToTarget`。非 ballistic motion 由 spawn origin、normalized direction / target point、speed / combat frame、sweep radius 和 lifetime frames 驱动，并共享确定性的速度曲线进度采样；`speedPerFrame * definition.lifetimeFrames` 只对这些非 ballistic motion 表示默认设计射程。`BallisticArcToTarget` 使用 `speedPerFrame` 作为最大初速度，使用 `gravityPerFrameSquared`、`maxLaunchAngleTangent` 和目标点解出射方向与本次实际初速度；同高度自动最远角为 45 度，未显式限制时最大射程为 `v^2/g`，目标不可达时拒绝 spawn，`lifetimeFrames` 只作为安全上限，active projectile 保存解算出的 `launchSpeedPerFrame` 和 effective lifetime。`CombatProjectileSpeedProfileKind` 只重新分配生命周期内的路径进度，当前支持 `Constant`、`EaseInQuadratic`、`EaseOutQuadratic` 和 `EaseInOutSmoothstep`。`ArcToTarget` 会按 `origin -> targetPoint` 和有效生命周期采样，垂直偏移为 `4 * arcHeight * progress * (1 - progress)`；锁定目标发射时可用 `ceil(distance(origin,targetPoint) / speedPerFrame)` 得到本次生命周期，使弧线终点精确等于发射时解析出的 target point。`BoomerangReturn` 前半程飞向目标点、后半程按同一路径返回 origin；`SwayForward` 在主路径上叠加固定点三角波侧向摆动，使用 `lateralAmplitude` 和 `lateralCycles` 表达幅度与周期；`HomingToTarget` 保存发射时解析出的 deterministic tracking target id，每帧通过接入方提供的 `ICombatProjectileTrackingTargetProvider` 重新解析目标中心，并用 `motion.homing.turnRatePerFrame` 做固定点转向响应，`1` 表示兼容旧行为的当帧完全转向，`0.15` 表示每帧只消化 15% 方向差；目标无效或 provider 缺失时沿上一帧方向继续飞行；`BezierToTarget` 使用三次贝塞尔曲线从 origin 采样到 target point，`bezierControlOffsetA/B` 为投射物局部坐标偏移（X=right、Y=up、Z=forward），分别作用于起点和目标点控制点。Direct hit sweep 只通过 `CombatPhysicsWorld.QueryCapsule(old,new)` 查询命中；`AreaPulseOnImpact` 在 accepted / blocked / expire 触发时只通过 `CombatPhysicsWorld.QuerySphere` 查询范围目标。Projectile runtime 不引用 `UnityEngine.Physics`，也不驱动飞行中 Unity VFX。

已落地契约：
- `CombatProjectileConfigJsonImporter` 从 `projectile_definitions.json` 生成 `CombatProjectileDefinitionRegistry`。当前 Unity 工程 generated 路径为 `Assets/MxFrameworkGenerated/Combat/Projectiles/projectile_definitions.json`；字段固定为 `definitionId`、`stableId`、`physicalEventsEnabled`、`motion.*`、`collision.*`、`impact.hitEvent`、`impact.areaHitEvent`、`impact.policyKind`、`impact.areaRadius`、`impact.areaLayerMask`、`impact.triggerOnExpire` 和 `impact.effectTimelineId`。`impact.hitEvent.damage` 是 projectile direct hit 的正式伤害入口且必填；`areaHitEvent.damage` 可省略并继承 direct hit damage。`impact.damageProfileId`、`hitEvent.damageProfileId` 和旧顶层 damage 字段都会导入失败。运动、碰撞和 presentation 字段仍按对应 importer 的确定性规则解析。
- `CombatProjectileSpawnProfileConfigJsonImporter` 从 `projectile_spawn_profiles.json` 生成 `CombatProjectileSpawnProfileRegistry`。当前 Unity 工程 generated 路径为 `Assets/MxFrameworkGenerated/Combat/Projectiles/projectile_spawn_profiles.json`；字段固定为 `stableId`、`projectileStableId`、`origin.kind`、`origin.socketId`、`origin.localOffset`、`origin.fallbackRootOffset`、`aim.kind`、`aim.targetHeightOffset`、`aim.fallbackKind` 和 `aim.maxRange`。v2 支持 `CasterForwardRange`、`LockedTargetCenter` 和 `LockedTargetOrCasterForwardRange`；CharacterTest 的 locked-target aim 只读取确定性 runtime target lock state，不读取 Unity live Transform。
- `projectile_definitions.bytes`、`projectile_spawn_profiles.bytes` 和 `projectile_presentation_bindings.bytes` 与上述 JSON 并列生成。Player / Playable bootstrap 优先走 binary importer，缺失时回退 JSON；二进制 loader 成功路径是启动期 `LowFreqAlloc`，registry lookup、projectile tick、impact resolve 和 Unity presenter sync 热路径不变。
- CharacterTest authoring source 不再把投射物请求维护在 `ability_authoring.json` 的三个大数组中；每个 projectile request 使用 `Tools/MxFramework.Authoring/samples/ability-character-test/config/projectiles/<stableId>.json` 保存 `schemaVersion`、`stableId`、`definition`、`spawnProfile` 和 `presentationBinding`。Per-ID source 禁止写 `definition.collision.layerMask` 和 `definition.impact.areaLayerMask`；direct collision query mask 由 `impact.hitEvent.targetTypes` 推导，`AreaPulseOnImpact` 的 area query mask 由 `impact.areaHitEvent.targetTypes` 推导，缺少 `areaHitEvent` 时继承 direct hit targetTypes。CharacterTest 当前把角色类目标映射到 DynamicCharacter layer，把 `WorldWall` / `WorldGround` / `WorldStatic` 映射到 obstacle/static environment mask。Authoring CLI 在 `ability validate/compile/import-unity` 时先读取根 `ability_authoring.json`，再合并这些 per-ID 文件，并继续输出现有三份 generated JSON / `.bytes`，因此 runtime 加载协议不变。共享 definition 的请求可以只在 canonical 文件保存 `definition` / `presentationBinding`，其它 request 文件只保存自己的 `spawnProfile`。
- CharacterTest Player artifact refresh 会把 projectile definition、spawn profile、presentation binding、ability catalog 和 effect timeline JSON / `.bytes` mirror 到 `Assets/StreamingAssets/MxFramework/CharacterTest/RuntimeCatalogs/...`。Player warm-run build 前会刷新这批 mirror；如果 generated JSON 存在但 `.bytes` 缺失，build 前报告会阻断并要求先重建 runtime binaries。CharacterTest 正式伤害链路不再 mirror `damage_profiles` catalog。
- `CombatProjectileSpawnProfileRegistry.TryGetByStableId` 用于把上层 gameplay request id 映射到 spawn profile，再由 `projectileStableId` 映射到 projectile definition。CharacterTest generated catalog 为空时才回退 embedded sample spawn profile / definition。
- 独立 `MxFramework/Combat/Projectile Authoring` 是 projectile 唯一 Unity 可写入口。Socket 优先从当前角色资源包 `geometry/sockets.json` 下拉选择，缺源包时回退 generated `character_geometry_binding.json`，最后才使用 `ability_authoring.json.knownSocketIds`；飞行 VFX Resource 使用 catalog 候选，碰撞和范围查询编辑 hit target toggles（敌方、友方、自身、第三方、可破坏对象、墙面、地面、静态场景），不暴露 raw LayerMask 输入。CharacterAction 只保存 Request Stable ID/触发帧并显示只读摘要与 deep link；其 Save/session 不拥有 projectile source、projector 或 generated writer。Generated-only fallback 记录可显示 resolved mask 方便诊断，但仍只读。Damage Profile 和 hit presentation Profile 不作为普通编辑项暴露；`knownSocketIds` 只是 ability package校验/兜底声明，不是权威 socket 姿态来源。
- `CombatProjectileSpawnRequestBuffer` 是 typed spawn 输入，适合接入方在 action/gameplay event 后写入；新 spawn 当帧只进入 active list，不移动。`CombatProjectileSpawnRequest` 的基础推进语义仍是 `Origin + Direction`；可选 `TargetPoint` 用于 `ArcToTarget` / `BoomerangReturn` / `SwayForward` 采样目标，并进入 debug、SaveState、network late-join 和编辑预览路径。可选 `EffectiveLifetimeFrames` 表示本次发射按解析出的距离和速度计算出的有效生命周期；未提供时兼容使用 definition lifetime。可选 `TrackingTargetEntityId` 只供 `HomingToTarget` 使用：普通 locked-target aim 是发射时定点，spawn 后目标移动不会改变 active projectile 的方向、目标点或有效生命周期；追踪 projectile 会保存 tracking target id，并把该 id 纳入 hash / save / network snapshot。
- `CombatProjectileRuntime.ReadStateView(moduleId)` / `CombatProjectileRuntimeModule.ReadStateView()` 为同步 hash 提供借用视图，保留完整 accepted-impact 状态及既有排序；初始化与容量准备后无分配。视图及内部 hit 列表在下次读取或 runtime 变更后失效，不能保存为 rollback/history，也不能交给异步消费者；`CreateStateSnapshot()` 继续返回独立副本。
- active projectile 按 projectile id 稳定排序推进；每帧先按 motion kind 与 speed profile 采样 old position / new position，再构造 `CombatCapsuleQuery`。默认 DirectHit 仍使用一条中心 capsule sweep；组合根可传入 `ICombatProjectileDirectSweepProvider` 追加多条 capsule sweep，runtime 会合并所有查询结果后重新按 `CombatQueryResultComparer` 语义排序，并继续复用同一套 `HitResolveSystem`、同目标去重、穿透上限和 blocked consume 逻辑。该 provider 属于 projectile tick 高频路径，接口约定为 caller-owned list，warm-up 后 `NoAlloc after initialization`；provider 返回 0 条查询时回落中心 capsule。
- query result 使用 `CombatPhysicsWorld` 的稳定排序，再由 `ICombatProjectileImpactResolver` 转成 `HitCandidate` 所需的 damage / stagger / knockback / target state。
- `HitResolveSystem` 处理 self、friendly、dead、evaded、duplicate、blocked 和 damage。默认 `StopOnFirstAcceptedImpact`；projectile accepted impact 是 `HitResolveKind.Damage`（包含 0 HP damage 的控制命中），或配置允许的 `Blocked`。`Evaded` 不是 accepted impact，Projectile 继续飞行。`HitResolveResult.IsAcceptedDamage` 仍只表示 `Damage > 0`，供 HP / positive-damage telemetry 使用。
- `CombatProjectileImpactPolicyKind.DirectHit` 保持单体 sweep 命中行为；`AreaPulseOnImpact` 在 projectile 被 Damage-kind impact、配置允许的 blocked hit 或 lifetime expire 触发时，以 hit / expire 位置发起 sphere query，并对范围内目标逐个走同一个 `ICombatProjectileImpactResolver` + `HitResolveSystem` 链路。每次 area pulse 触发都会先输出一个 `CombatProjectileAreaPulseTriggerEvent`，即使 sphere query 没有任何范围目标也会输出；area hit 目标仍继续输出 `CombatProjectileImpactEvent`，不新增 damage application 链路。
- `CombatProjectilePhysicalEvent` 是 projectile-authored 物理事件事实，进入 `CombatProjectileFrameEvents.PhysicalEvents`。开启 `physicalEventsEnabled` 后，runtime 会在有效半径和 `impactPower` 都大于 0 时输出：`ActiveFrame` 使用 active projectile position/direction、`collision.radius`、`impact.hitEvent.damage.impactPower`；`Impact` 使用命中点、projectile direction、`collision.radius`、direct hit `impactPower`；`AreaPulse` 使用 pulse center、`impact.areaRadius`、`areaHitEvent.damage.impactPower`，缺省继承 direct hit `impactPower`；`Expiration` 使用过期点，若 `AreaPulseOnImpact + triggerOnExpire` 则复用 area pulse 半径/强度，否则复用 collision 半径/direct hit 强度。事件不查询或修改角色、环境物体、Rigidbody 或 Unity prefab；项目侧 consumer 负责解释 stable id 和 trigger kind。
- DirectHit 可通过 `stopOnFirstAcceptedImpact=false`、`maxAcceptedImpacts>1` 和 `hitSameTargetPolicy=OncePerProjectile` 表达穿透多目标 projectile。active projectile 会保存已 accepted 的 target key；friendly / self / dead / duplicate 不消耗穿透次数，同一目标跨帧重叠不会重复受伤。同一 projectile frame 内由多条 direct sweep 命中同一目标时，后续 `Duplicate` 结果只用于去重，不再发布额外 impact 事件。
- `CombatProjectileFrameEvents` 输出 spawn / impact / expiration / rejection，保留给调试、测试和低频兼容路径；`CombatProjectileRuntime.Tick(frame, CombatProjectileFrameEventBuffer)` 是热路径 NoAlloc 事件输出入口，调用方复用 buffer 以避免每帧复制 list / read-only wrapper。`CombatProjectileImpactEvent` 包含 projectile snapshot、physics hit、`HitResolveResult`、resolver payload 和 `CombatProjectileImpactContext`，用于区分 direct sweep 与 area pulse 等公共 hit event 解析上下文。
- `CombatProjectileActiveView` 是热路径 active projectile 轻量视图，不携带 accepted-hit history；`CombatProjectileRuntime.CopyActiveViewsTo(List<CombatProjectileActiveView>)` 供 CharacterTest tornado 逻辑和 Unity-facing flight presenter 复用 caller-owned list。
- `CombatProjectileRuntimeHashContributor` 写入 module id、definition hash、next projectile id 和 active snapshot；`CombatProjectileRuntimeSaveStateProvider` 保存 schema version、definition hash、next id 和 active projectile state，并在 restore 时校验 custom state type、schema version 和 definition hash。active snapshot 包含 origin、target point、effective lifetime、tracking target id 和 accepted hit history，确保 late-join/debug path 不丢失，穿透 projectile save/restore 后不会重复命中已穿透目标，追踪 projectile restore 后继续使用同一个 deterministic target id。
- CharacterTest network full snapshot 会携带 projectile runtime state，并把 active projectile、origin、target point、effective lifetime 和 accepted hit history 加入 full hash / owned hash；offline 和 LAN mirror 都从同一 `CombatProjectileRuntimeState` 恢复。旧 snapshot 缺少 `projectiles` 字段时按空 projectile 状态处理，用于兼容本字段落地前的 full snapshot；definition hash 不匹配会拒绝 restore。

CharacterTest projectile 样例包含 Fireball 系列、Tornado、Beam/Channel/Laser Lance、Chain Lightning、Poison Bottle 和 Stun Bolt。`stun_bolt` 第 30 帧发 `SendGameplayRequest(requestId=character_test.stun_bolt.projectile)`，走 `Linear + DirectHit + Constant` 单体 hostile projectile，32m range、0.18m radius、首个 Damage-kind hostile hit 后停止；direct HP damage 为 0，通过 `OnHitBuffs` 施加 `Stunned` (`buffId=601013`) 并触发 `character_test.stun_bolt.impact` 表现。所有 projectile 都先用 request id 查询 generated-first spawn profile，再解析 projectile definition；起点优先使用动作本地帧的 baked `mainHand` socket pose，缺失时回退到 root forward/up offset 并记录诊断。Fireball / Flame Lance / Fireball Lob / Fireball Homing / Stun Bolt 使用 `LockedTargetOrCasterForwardRange`：有有效 hostile locked target 时目标点取 deterministic body / transform center，effective lifetime 由 `ceil(distance / speedPerFrame)` 计算；无锁定、友方、死亡或超距时回退 `origin + casterForward * range` 并记录诊断。`LockedTargetCenter` 是严格锁定模式，无有效目标时拒绝 spawn。命中配置通过 `CombatHitEventConfig` 解析 target types、穿透约束和 `hitEvent.damage` / `areaHitEvent.damage`；CharacterTest resolver 将这组伤害属性展开为固定 flat damage，并通过 stable event 兼容现有 impact VFX trigger map。墙面 / 地面等环境命中只有在 hit event 明确配置 `BlockedPresentation` 时触发 blocked/presentation 语义，不造成 HP damage。

`MxFramework.Combat.Projectiles.Unity` 是 Unity-facing 表现层，不属于 noEngine projectile authority。当前 writer/importer 合同是 Presentation Binding v2：`CombatProjectileUnityPresentationBindingV2` 按固定 enum/index 保存 `spawn`、`flightBody`、`trail`、`directImpact`、`areaImpact`、`blockedImpact`、`expire` 七槽，槽值包含完整 `ResourceKey`、受控 orientation、local offset、lifetime override，以及 one-shot 槽的紧凑 `VfxDefinitionId`；`CombatProjectileUnityPresentationExitPolicy` 保存 accepted/blocked/expiration/external-removal/runtime-reset 退出语义。registry 只建立一套按数值 `ProjectileDefinitionId` 的 v2 索引，`TryGetV2` 和 `TryGetSlot` 是初始化后 NoAlloc 查询。旧 `CombatProjectileUnityPresentationBinding`、`TryGet` 和 `CopyBindingsTo` 仅作为 obsolete flightBody adapter 保留，不形成第二套权威 registry。

Projectile Authoring 的一次性槽合同长度由低频 `SynchronizeVfxContractLifetime` mutation 统一写入 Presentation Slot、VFX Definition 默认生命周期与 VFX Presentation Profile 覆盖值。该操作通过 `config/ability_authoring.json` 的专用精确路径授权和 projectile source hash guard 一并提交；任一源冲突、编译阻断或生成失败都会整体回滚。该 API 属于 `LowFreqAlloc` Editor/CLI authoring 路径，不进入 runtime Tick。

Projectile Authoring 的“保存并同步到游戏”调用同一个 `projectile apply` 事务，同时提交 source、package `generated/`、`Assets/MxFrameworkGenerated/Combat/Projectiles/` 与 CharacterTest StreamingAssets Player mirror。事务写完后逐项反向校验六个 runtime JSON/bytes hash；任一写入、冲突或最终校验失败都会回滚全部目标，不能报告部分保存成功。该链路属于 `LowFreqAlloc` Editor/CLI 发布路径，不进入 runtime Tick。

`CombatProjectileUnityPresentationConfigJsonImporter` / `CombatProjectileUnityPresentationConfigBinaryImporter` 写入和优先读取 schema v2，并通过显式兼容入口继续读取 schema v1：旧 `prefabResource` 只映射到 `flightBody`，不会推断其它槽。JSON 是 authoring/debug/diff 格式，`.bytes` 是 Player 优先 runtime artifact；无效 schema、slot/orientation、重复 definition 或截断 binary 必须整体失败，不留下部分 registry。M4 presenter 仍只消费 `flightBody`；其余生命周期槽和 exit policy 由后续 lifecycle event bridge 消费。`CombatProjectileUnityPresenter.Sync(CombatProjectileRuntimeState)` 保留给 snapshot/debug 兼容路径；`CombatProjectileUnityPresenter.Sync(IReadOnlyList<CombatProjectileActiveView>)` 是 CharacterTest presentation 热路径，按 `ProjectileId` 创建/复用 prefab view，并把 view 的 position / forward 同步到 active view 的 `Position` / `Direction`。`PrewarmBindings` 在初始化期缓存 prefab handle、实例化 pool，并缓存每个 view 的 `ParticleSystem[]`；warmed spawn/stop 不再调用 `ResourceManager.Load` 或 `GetComponentsInChildren`。snapshot / active view 消失时 presenter 停止并回收/销毁 view；缺少 binding、缺少 flightBody、资源加载失败或 pool exhausted 只记录 diagnostics，不影响 projectile runtime、damage、cooldown、HitResolve、Replay hash 或 SaveState。

## Damage v1

Combat 的正式命中伤害配置入口是结构化 `hitEvent.damage` payload，不要求项目维护独立 damage profile catalog。CharacterTest 把基础伤害、冲击强度、击飞效果值和少量分析标签写在通用 `hitEvent.damage` 数据结构上；伤害公式由游戏层 resolver 固定选择，不在动作事件或公共 hit event 中给作者配置。

已落地契约：
- `CombatHitDamageConfig` 保存公共命中伤害字段：`damageTypeId`、`baseDamage`、`sourceAttributeScales`、`impactPower`、`launchPower`、`tags` 和 `analysisCategory`。Projectile Definition JSON/二进制 schema v5 均保存来源属性系数；缺省为空，旧 v4 二进制需重新生成。`baseDamage` 是固定 HP 输入；`impactPower` 是冲击和散势压力；`launchPower` 是独立击飞效果输入。
- CharacterAction 的 `CharacterActionDamagePayload` 来自 `hitEvent.damage`，并在 CharacterTest 组合根中直接进入 damage resolver；不再通过 `damageProfileId` 或旧顶层 damage payload 迁移。
- `IDamageProfileCatalog` / `DamageProfileConfig` 可继续作为通用 Combat legacy 工具存在，但 CharacterAction / Projectile / CharacterTest 的正式伤害链路不读取 profile catalog，也不加载 `damage_profiles.bytes`。
- CharacterTest Player warm-run report 不再把 Damage Profile catalog 纳入 expected binary catalog gate。
- v1 先支持主 HP damage 分量；`DamageComponentConfig[]` 保留多分量扩展口，后续可加姿态、护甲、元素或资源扣减。
- `DamageRequest` 保存来源上下文和已解析出的 common damage payload identity，是武器轨迹、范围脉冲、投射物、状态 tick 和脚本伤害进入公共结算链路的统一请求形状。
- `DamageProfileUsage` 保存来源上下文：`sourceKind`、action、action instance、stable event、装备槽、武器、目标、hit zone、body part、world frame 和 local frame。
- CharacterTest damage resolver 只接受通用 damage payload、属性读取函数和 hit-zone 倍率；禁止表达式字符串 fallback 或 profile catalog fallback。
- CharacterTest v1 的默认 `light_attack` 平衡锚点是 `baseDamage=6`、`Attack * 0.65`、`impactPower=34`；在默认 `Attack=20`、`Defense=10`、身体命中倍率 `1.0` 下造成 17 点 HP 伤害。同质量、命中部位 `ImpulseScale=1.0` 时会派生约 `ScatterImpact=34` 和约 `1.0m` 的一次性击退预估距离；同级普通目标 `BaseCohesion=100` 时，第 3 次连续轻击触发 Broken。
- 内置公式选择由 CharacterTest resolver 固定，不作为新动作命中 authoring 项；未知配置应返回失败结果，由调用方输出配置错误。
- `DamageResolvedEvent` 和 `HitPresentationEvent` 是表现层入口；表现层不能直接读取 physics query 或直接扣 HP。
- `DamageTelemetryRow` 是运行时结构化分析行；不要从 Debug log 反推数值分析。 `AnalysisCategory` 单独保存制作期整数分析类别，伤害解析、发布及 `WithImpact` 复制均保留该值；`DamageProfileId` 保持配置身份语义，不承载分析类别。

运行时分工：
- 物理查询和 hit-once 仍由 weapon trace / `CombatPhysicsWorld` / `HitResolveSystem` 处理。
- 进入 `HitResolveSystem` 前，调用方用 CharacterTest damage resolver 计算 incoming HP damage，再写入 `HitCandidate`。accepted / blocked 后，CharacterTest 用 `impactPower`、双方质量、命中部位 `ImpulseScale` 和目标运动减速参数派生 `ScatterImpact` 与 `Impulse` 击退初速度。最终硬直、实际击退距离、表现和 CharacterTest 格挡受压效果不由伤害事件单方面决定。
- `HitResolveSystem` 只判断 self / friendly / duplicate / block / invincible / super armor 等命中是否成立。
- HP 和散势压力应用应由 Gameplay / Attributes bridge 或项目组合根消费 accepted / blocked 结果完成；表现层只消费 resolved/presentation event。
- 数据分析使用结构化 telemetry 行，不从 Debug log 反推伤害数据。
- Aura Buff v0 已接入 component-native Buff 刷新，但不产生伤害。未来 Buff / DOT 只能作为 `DamageRequest(sourceKind=StatusTick)` 生产者进入同一 Damage / HitResolve / DamageApplication 链路，不能在 Buff tick 内直接修改 HP。

## Gameplay Bridge：Aura Buff

`CombatGameplayAuraBuffAbility` 是 component-native ability：通过现有 `GameplayComponentAbilityCommandSystem`、cost 和 cooldown rules，在 caster 的 `GameplayComponentAuraSourceSetComponent` 上 upsert `GameplayComponentAuraSourceEntry`。它不新增 RuntimeCommand id。

`CombatGameplayAuraBuffSystem` 运行在 `GameplaySystemPhase.Resolution`，默认 priority 为 `45`。系统读取 aura source store，通过 `CombatEntityGameplayMap` 找到 caster 的 `CombatEntityId`，再由 `ICombatGameplayAuraAnchorProvider` 提供当前 combat-space center，最后用 `CombatPhysicsWorld.QuerySphere` 查询范围内目标。

刷新规则：

Generated authoring note: `CombatGameplayAuraBuffDefinitionJsonImporter` / `CombatGameplayAuraBuffDefinitionBinaryImporter` import generated aura buff catalogs into `CombatGameplayAuraBuffDefinitionRegistry`. JSON is kept for authoring/debug/diff, while `aura_buff_definitions.bytes` is the Player-first runtime artifact. `layerMask` is the explicit noEngine Combat layer mask value, and `radiusMeters` is imported as fixed-point meters.
- `CombatGameplayAuraBuffDefinition` 以 `AuraId` 绑定 `BuffId`、radius、layer mask、`GameplayTargetRelationFilter` 和 `IncludeCaster`；duration、stack policy 和 effects 只来自 `GameplayBuffDefinitionRegistry`。
- 目标筛选复用 `GameplayComponentTargetingService`；Combat 只提供空间候选，team / alive / self / enemy 语义仍由 Gameplay component state 决定。
- 对命中的目标写入一条 `GameplayBuffRequestBuffer.EnqueueApply(...)` 请求；后续 `GameplayBuffApplicationSystem` 负责 upsert 单条 `GameplayComponentBuffEntry`，并把 `EndFrame` 解析为 `currentFrame + BuffDefinition.DefaultDurationFrames`，`SourceId` 写入 `AuraId`。
- 目标离开范围或 aura source 结束时不发即时退出事件；停止刷新后由 `GameplayComponentBuffCleanupSystem` 移除过期 Buff。
- `LastTickSummary` 记录 source、query、apply / refresh、rejected target、missing definition / map / anchor / gameplay target 计数，用于测试和诊断。

边界：
- v0 只支持 caster-following sphere aura，center 由 anchor provider 提供；不支持固定地面光环、周期伤害、DOT、治疗 tick、VFX、UI 或 Unity Physics。
- 同一个 Buff 在同一目标上只维护一条 `GameplayComponentBuffEntry`；多个 aura source 会刷新同一条 Buff，而不是按来源拆分实例。

## Action / RuntimeHost 模块

动作运行时使用 `CombatActionTimeline`、`CombatActionRunner` 和 `CombatAnimationContext`，并通过 RuntimeHost 模块接入统一 tick：
- `CombatActionRuntimeModule` 推进动作状态。
- `CombatWeaponTraceRuntimeModule` 根据动作时间轴和 trace provider 生成武器轨迹查询。
- `CombatAnimationDiagnosticsModule` 输出动作 / trace / hit 诊断快照。

`CombatActionTimeline.SupportProfile` 可选保存 `CombatActionSupportProfile`，用于描述动作当前的姿态支撑参数。该 profile 只包含 Combat noEngine 数据：`SupportRate`、`MinSupportRatio` 和可选 hyper armor frame window，不引用 Gameplay。`CombatActionRunner.GetCurrentSupportProfile(entityId)` 和 `GetCurrentLocalFrame(entityId)` 提供只读查询；没有 running action 时分别返回 `null` 和 `-1`，不改变 action start / tick / cancel 生命周期。

`CombatActionRunner.ActionFrameEventRaised` 发布 noEngine 的固定帧表现关联事件。`StartAction` / `ForceStartAction` 成功时先发布 `ActionStartedEvent`，再发布 local frame 0 的 `CombatActionFrameEvent`；`TickActions` 每次推进 running action 后，按稳定 entity id 顺序和 `CombatActionFrameEvent.CompareTo` 顺序发布该 local frame 的事件。payload `ActionFrameEventRaisedEvent` 包含 entity、action、action instance、world frame、local frame 和原始 frame event。

frame event 只提供 deterministic presentation correlation，不承载 VFX / SFX / Camera / UI kind，也不承载 `ResourceKey`。Unity 或 MxAnimation bridge 需要从 animation binding、bridge 配置或其他表现层配置解析资源和表现类型；该事件不得反向驱动取消窗口、命中、伤害、replay hash 或 Combat 权威状态。

`MxFramework.Combat.Animation.Unity` 是独立 Unity bridge assembly。它消费 `CombatActionRunner` lifecycle events 和 `ActionFrameEventRaised`，按默认 `action:<ActionId>` key 或显式 bridge config 查找 `MxAnimationSetDefinition` / `MxAnimationActionBinding`，再向 `IMxAnimationBackend` 发 play / stop / crossfade 请求或向表现事件 sink dispatch `MxAnimationPresentationEvent`。dispatch 使用 actor/entity id、action instance、world/local frame、event id 和 source order 组成 `MxAnimationPresentationEventDedupeKey`，重复 key 会被 bridge 丢弃并写入 diagnostics。bridge diagnostics 保留 entity、action、action instance、world frame、local frame 和原始 frame event correlation。

MxAnimation 的 presentation sync contract 可以把 Combat action lifecycle 复制为表现恢复状态：actor / entity id、action id 或 action key、action instance id、started-at Combat frame、current local frame、layer sync state、量化表现参数和 event dedupe key。该状态只用于 late join、补包和预测纠偏时恢复表现，不进入 Combat hash / replay / 命中 / 取消 / 伤害 / 权威移动，也不允许 Unity Playable / Animator / bone pose 反向写回 Combat。一次性表现事件默认不因 late join 补播；项目层只有在事件显式标记为 catch-up safe 时，才可以按自己的网络策略补发。

旧 `MxFramework.Runtime.Unity.CombatAnimationUnityModule` / `CombatAnimatorDriver` 仍保留为 opt-in Animator 迁移路径。新 MxAnimation bridge 不自动注册旧 module；项目层 composition root 不应在同一 entity 上同时启用两套 bridge，以免同一 Combat event 双触发动画。

`CombatActionState` 包含 `ActionInstanceId`，用于在 multi-step Runtime tick 内保留每个动作实例的 hit-once 身份；RuntimeHost weapon trace 模块基于每个 fixed step 后的动作状态快照计算候选，不从 Runtime frame 直接推导 Combat frame。

默认模块位于 RuntimeHost 阶段中运行，具体 priority 和组合根由 Demo / 项目层配置。动作系统不读取 Unity Animator 状态作为权威。

## Gameplay Bridge：聚限支撑和破聚打断

`CombatCohesionLimitAdapter.CalculateCohesionLimit(basePressure, supportProfile)` 使用固定点参数计算 cohesion limit：

```text
baseLine = basePressure
actionLine = baseLine * supportProfile.SupportRate
minLine = baseLine * supportProfile.MinSupportRatio
cohesionLimit = Max(baseLine, Max(actionLine, minLine))
```

当 support profile 为空时返回 `basePressure`。`Fix64` 结果通过 `ToInt()` 量化，当前语义是向零截断。`CombatCohesionLimitAdapter.IsInCohesionSupportWindow(profile, localFrame)` 只在 profile 显式启用 hyper armor 且 local frame 落在窗口内时返回 true。

`CombatActionRunner` 仍是 Combat 模块内部攻击时间线的权威，但不能成为角色外层动作的第二权威。正式 CharacterAction 需要 Combat 子时间线时，由 `MxFramework.CharacterControl.CharacterActionCombatTrackBridge` 消费 `StartCombatAction` track；外层 CharacterAction 终止时，bridge 清理对应子动作，子动作完成或取消不反向决定外层状态。CohesionBreak、GuardBreak、ArmorBreak、Hit 和 Death 的正式动作反应只通过 `CharacterActionReactionBridge` → `CharacterActionExecutionGateway`，普通反应被 `interruptRules` 拒绝时保留原外层动作。旧 `CombatCohesionBreakAdapter` 已删除。

## Gameplay Bridge：Damage Application / GuardPressure / ArmorIntegrity

`CombatDamageApplicationSystem` 是当前通用伤害结果进入 Gameplay 的 bridge。它读取 `HitResolveResult + DamageProfileResolveResult`，通过 `CombatEntityGameplayMap` 将 target `CombatEntityId` 映射到 `GameplayEntityId`，再统一处理 HP command、blocked guard pressure、armor absorb、resolved/presentation event 和 `DamageTelemetryRow`。

项目组合根不能同时让 `CombatDamageApplicationSystem`、旧 `CombatHitApplicationSystem` 或 `CombatGuardArmorApplicationSystem` 消费同一 hit stream 输出 HP command，否则同一 Damage 结果会重复扣 HP。旧 `CombatGuardArmorApplicationSystem` 保留兼容和局部测试，但新项目应优先使用 `CombatDamageApplicationSystem`。

处理规则：
- `HitResolveKind.Blocked`：Blocked 不扣普通 HP。通用 bridge 仍可在项目显式传入 `GameplayGuardPressureSystem` 时写入 GuardPressure 请求；CharacterTest v1 不接这条独立 GuardPressure 链路，而是用 `ScatterImpact` 表达格挡受压和破绽风险。
- `HitResolveKind.Damage`：读取目标 `GameplayArmorIntegrityComponent`，按 armor 调整后的实际 HP 伤害输出 `GameplayRuntimeCommandFactory.AddComponentAttribute(..., -finalDamage, traceId)`。
- 同一 `Tick` 的批量结果按输入顺序结算。每个 `GameplayEntityId` 的剩余 HP 同时计入本批已输出的 HP 扣减；每击限幅、`DamageResolvedEvent.FinalDamage` 及 telemetry 的 `HpBefore/HpAfter` 使用同一逐击投影。例：HP=10、伤害=8/8 时输出 -8/-2，记录 10→2→0；HP=100、伤害=12/12 时记录 100→88→76。投影不直接写组件 HP，正式 HP 写入仍由属性命令消费者执行。
- `HitResolveKind.Duplicate` / `Friendly` / `SelfDamage` 等拒绝结果只写 telemetry，不输出 HP delta。
- Blocked／拒绝结果的 HP telemetry 可观察同目标在本批先前已结算的伤害，但自身不消耗剩余 HP。不同目标的批内剩余 HP 独立；耗尽 HP 后不再输出扣血命令，既有逐击 armor 衰减语义保持。
- 没有 armor component，或 armor `MaxIntegrity <= 0`、`CurrentIntegrity <= 0`、`CurrentIntegrity > MaxIntegrity`、`IsBroken == true` 时，应用完整 incoming damage。
- 可用 armor 的固定口径：先用命中前的 `CurrentIntegrity` 计算 `absorbed = incomingDamage * CurrentIntegrity / MaxIntegrity`（整数截断）；再按 incoming damage 衰减 `CurrentIntegrity = max(0, CurrentIntegrity - incomingDamage)`；最终 `adjustedDamage = incomingDamage - absorbed`。
- armor 从未破裂状态衰减到 0 时只发布 `ArmorBreakEvent`，不新增 `HitResolveKind.ArmorBroken`，也不改变原始 hit resolve kind。

批内 HP 投影只在当前 `Tick` 有效，正常退出或异常退出均清空；下一次调用重新读取组件 HP，不进入 SaveState／Replay authority。调用方须在两次 `Tick` 之间消费或丢弃上一批命令，不能跨调用积累未提交 HP delta，也不能在批次中途提交 HP／修改 HP authority。逐条 Tick 后立即提交的入口保持兼容。投影复用 system 自有集合，首次目标容量扩展发生在 warm-up；容量与调用方输出／telemetry／event 缓冲预热后不为批内投影创建临时集合。

`CombatTargetStateProvider` 可选配置 `guardBrokenStatusId`。当目标同时拥有 Blocking status 和 GuardBroken status 时，provider 不再设置 `HitTargetStateFlags.Blocking`；Invincible、Parrying、SuperArmor 等其他映射不受影响。GuardBreak 后如何写入该 status 归 Gameplay / 组合根负责。

## Baked Weapon Trace Adapter

MxAnimation bake 产出的 socket / root / weapon reference 只能作为确定性参考数据进入 Combat。Combat noEngine 侧使用 `CombatBakedWeaponTraceAdapter` 将 reference 与显式 runtime profile 组合：

- `CombatBakedWeaponTraceReferenceFrame` 保存 local frame、trace id、socket id、socket prev/now、tip direction prev/now，坐标全部使用 `FixVector3`。
- `CombatBakedWeaponRuntimeProfile` 保存 character scale、weapon length、weapon radius、socket offset 和 target layer mask。
- `CombatBakedWeaponTraceAdapter.BuildFrame` 输出现有 `WeaponTraceFrame`。
- `CombatBakedWeaponTraceAdapter.BuildCurrentBladeCapsule` 输出现有 `CombatCapsuleQuery`，后续仍走 `CombatPhysicsWorld` / `HitResolveSystem`。

这条路径不引用 `MxFramework.Animation`、`AnimationClip`、Animator、PlayableGraph 或 Unity bone pose。动态武器、角色尺寸和 socket offset 都必须作为显式 profile 输入；如果 IK / 动态骨骼修正要影响权威命中，也必须先转成可复现 Combat 输入或确定性 solver 数据。MxAnimation bake artifact 只在离线或加载侧帮助生成/校验 reference，Combat runtime 不直接读取 artifact 或 Unity 当前姿态。

## 最小使用示例

```csharp
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;

var world = new CombatPhysicsWorld();
var entityId = new CombatEntityId(1);
var bodyId = new CombatBodyId(10);
var colliderId = new CombatColliderId(100);

world.UpsertBody(new CombatPhysicsBody(entityId, bodyId, FixVector3.Zero));
world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
    bodyId,
    colliderId,
    layer: 0,
    new FixVector3(Fix64.FromInt(-1), Fix64.Zero, Fix64.FromInt(-1)),
    new FixVector3(Fix64.One, Fix64.FromInt(2), Fix64.One)));

var header = new CombatQueryHeader(
    queryId: 1,
    kind: CombatQueryKind.Ray,
    sourceEntityId: CombatEntityId.None,
    traceId: 0,
    actionId: 0,
    sourceOrder: 0,
    layerMask: CombatPhysicsLayerMask.All);

var query = CombatPhysicsQuery.From(new CombatRayQuery(
    header,
    new FixVector3(Fix64.FromInt(-4), Fix64.One, Fix64.Zero),
    new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
    Fix64.FromInt(10)));

var results = new List<CombatQueryResult>();
world.Query(query, results);
```

## 测试入口

主要测试目录：`Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Combat/`

当前已存在的测试入口包括：
- `Tests/Combat/Core/CombatFrameClockTests.cs`
- `Tests/Combat/Core/CombatFixedStepDriverTests.cs`
- `Tests/Combat/Core/CombatHashTests.cs`
- `Tests/Combat/Core/CombatSortKeyTests.cs`
- `Tests/Combat/CombatHitEventConfigTests.cs`
- `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Combat/Hit/HitTargetStateContributionStoreTests.cs`：来源隔离、批量清理与 `Clear` 后复用，以及固定高水位预热后循环零分配。
- `Tests/Combat/Physics/CombatPhysicsWorldTests.cs`
- `Tests/Combat/Physics/CombatPhysicsBroadphaseTests.cs`
- `Tests/Combat/Physics/CombatQueryContractTests.cs`
- `Tests/Combat/Levels/CombatLevelDefinitionTests.cs`
- `Tests/Combat/Projectiles/CombatProjectileConfigJsonImporterTests.cs`
- `Tests/Combat/Projectiles/CombatProjectileRuntimeTests.cs`
- `Tests/Combat/Projectiles/CombatProjectileUnityPresenterTests.cs`
- `Tests/Combat/Shields/CombatShieldRuntimeTests.cs`
- `Tests/Combat/Motion/CombatKinematicMotorTests.cs`
- `Tests/Combat/Motion/CombatMotionObbCastTests.cs`
- `Tests/Combat/Motion/CombatMotionObbCastOverlapSkinTests.cs`
- `Tests/Combat/Motion/CombatKinematicMotorObbIntegrationTests.cs`
- `Tests/Combat/Motion/CombatMotionObbCloseoutTests.cs`
- `Tests/Combat/Motion/CombatKinematicMotorSlopeSurfaceTests.cs`
- `Tests/Combat/Motion/CombatKinematicMotorGroundSnapTests.cs`
- `Tests/Combat/Motion/CombatMotionSlopeRegressionCloseoutTests.cs`
- `Tests/Combat/Motion/CombatKinematicMotorWallSlideTests.cs`
- `Tests/Game/CharacterTest/CharacterTestSlopePlayableBehaviorTests.cs`
- `Tests/Game/CharacterTest/CharacterTestSlopeFullMotionNoAllocTests.cs`
- `Tests/Combat/Animation/CombatActionRunnerTests.cs`
- `Tests/Combat/Animation/CombatActionTimelineTests.cs`
- `Tests/Combat/Animation/CombatAnimationRuntimeModuleTests.cs`
- `Tests/Combat/Animation/CombatWeaponTraceEvaluatorTests.cs`
- `Tests/Combat/Diagnostics/CombatDiagnosticsTests.cs`
- `Tests/Combat/GameplayBridge/CombatGameplayEventBridgeTests.cs`

Authoring / Editor 相关测试位于 `Tests/Combat/Authoring/`，但不改变运行时接口边界。

## 不支持

- 不提供通用 3D rigidbody、摩擦、堆叠、弹性、关节或布娃娃模拟。
- 不把 Unity Animator、AnimationClip、Timeline、Physics.Raycast 或 Rigidbody 作为权威运行时输入。
- 不迁移 WGame 具体角色、技能、元素体系或真实 Buff 配置。
- 不声明 `Docs/COMBAT_ANIMATION_PHYSICS.md` 中尚未落地的草案 API 为当前接口。
> Current Buff v2 note (2026-06-12): Aura Buff bridge now enqueues `GameplayBuffRequestBuffer` apply requests instead of writing `GameplayComponentBuffSetComponent` directly. The default resolution order is `AuraSourceCleanup(40) -> BuffExpireCleanup(42) -> CombatAuraRefresh(45) -> BuffApplication(50) -> BuffEffectProjection(52) -> CombatDamageApplication(60)`. `CombatGameplayAuraBuffDefinition` owns aura-to-buff range refresh data only; stack policy and effects come from `GameplayBuffDefinitionRegistry`.

> Current on-hit Buff note (2026-06-23): `CombatHitEventConfig` supports `OnHitBuffs`, a deterministic sorted list of `CombatHitBuffApplicationConfig` entries that only carry `buffId`. `CombatOnHitBuffApplicationSystem` is the parallel Combat -> Gameplay bridge: it reads `HitResolveKind.Damage` results with configured on-hit Buffs, maps the target through `CombatEntityGameplayMap`, and enqueues `GameplayBuffRequestBuffer` apply requests with the hit trace as source context. Zero-damage control hits may apply Buffs, while `AcceptedDamageCount` still counts only `HitResolveResult.IsAcceptedDamage` (`Damage > 0`). Blocked, friendly, duplicate, self-damage, dead/invincible/parried, and other non-Damage results are skipped, and Buff definition validation remains in `GameplayBuffApplicationSystem`.

## Projectile Presentation Lifecycle Bridge

`CombatProjectileVfxBridge` is the sole standard seven-slot presentation consumer of `CombatProjectileFrameEventBuffer`. It routes accepted Spawn, Damage-kind Direct Impact (including zero-HP-damage accepted control hits), Blocked Impact, Area Pulse Trigger and natural Expire in fixed order. Area source cues come only from `CombatProjectileAreaPulseTriggerEvent`, never from per-target area impacts. A bounded correlation ring deduplicates repeated frame-buffer submission without entering Combat SaveState, replay hash or authority.

The bridge sends one-shot compact `VfxDefinitionId` values directly to `VfxRuntimeModule` and is the sole standard consumer that delegates persistent Flight Body/Trail Build, late-attach Rebuild, Update and exit-policy handling to `CombatProjectileUnityPresenter`. CharacterTest presentation ticks may call only `TickRetiringViews()` for lifecycle cleanup; that `NoAlloc` API consumes no projectile snapshot and cannot build, update or exit an active view. Natural expiration, external disappearance and runtime reset remain distinct. Optional custom extensions use side-effect-free `CanHandle` arbitration followed by exactly one deterministic first-claimant `Handle` callback; a claim suppresses only the corresponding one-shot slot and never takes ownership of standard Flight/Trail Build, Rebuild or terminal exit. Duplicate claims are diagnostic and cannot double-play. Tracking and correlation capacities are fixed at construction; tracking overflow is diagnosed and the untracked projectile is not allowed to create a standard persistent view, preventing an uncollectable view leak. All frame-routing APIs are `NoAlloc after initialization`; registration, prewarm, resource loading and diagnostic snapshots are low-frequency allocation paths.

`CombatProjectileUnityPresenter` accepts an optional `ICombatProjectileUnityObjectHost`. The default host preserves runtime behavior (`Object.Instantiate`, then play-mode `Destroy` or editor `DestroyImmediate`); all prewarm, pool-growth, failure-cleanup and dispose releases are routed through the same host. Projectile Authoring uses this extension with `PreviewRenderUtility` so Flight/Trail instances are added directly to the isolated preview scene and are released symmetrically without creating roots or dirty state in the user's active Scene. This is a presentation ownership seam only and does not change M5 routing, pooling, lifecycle, gameplay hash, or SaveState semantics.

## CharacterTest效果机制前置边界（2026-10-08）

项目层已抽取完整目标AABB净空及Motion安全停止重定位，内部owner/调用边界见[CharacterControl](CharacterControl.md#charactertest共用落点与停止重定位前置2026-10-08)，验证单一来源为[任务09](../../StorySource/玩法基石/任务/任务09_替身准备与体型切换基础.md)。这些查询和位移基础没有提供通用替身API；尚未将命中接受预算、护甲、产物终态及命中附加全部分离为纯查询与最终提交，也未实现统一体型尺度owner/消费者。

新替身合同未定，旧预存位置及落点失败受击方案已否定；体型只确认到期安全还原与可能延长的后果。当前Combat接口不得把[主基石](../../StorySource/玩法基石/行动卡与交互效果卡设计基石.md)的推荐协议当作已有运行API或完整机制验证。

## 非写入 motor 步进（2026-10-07）

`CombatKinematicMotor.Step(..., candidateCache, CombatMotionStepOptions options, bool syncBodyPosition)` 显式控制是否将步进后的 actor body 位置同步到 world。旧 overload 保持 syncBodyPosition=true 的行为。CharacterControl.Query 使用 false，调用方仅观察预测 motion/result；scratch 可以变化，world body 不变。正式 Step 仍执行一次权威 body 同步；调用方不得把 query 当作已发生的接触或命中。

## 接受来源与护甲后 HP 限额（2026-10-12）

`ICombatProjectileSpawnObserver.OnProjectileSpawned(in birth)` 由实际 runtime 在 native id/sequence 分配后、任何 impact 前同步调用；恢复已有快照不重新发布出生。`ICombatProjectileImpactRelations.ResolveTeamRelations(in projectile, fallback)` 可为每个弹体选择冻结出生关系，默认保持原 team provider。观察者由调用方提供，项目层可在低频实际出生/排队时分配冻结属性数组；这不等于通用高频 collision 路径获准分配。

`CombatDamageApplicationSystem` 保留原构造入口，新增接收 `ICombatHpDebitPolicy` 的重载。policy 在护甲消费后、HP command／事件／遥测前收到最新 currentHp 和 maximumDebit，只能返回 [0, maximumDebit]，越界拒绝。护甲行为保持，政策不回补 HP。`CombatOnHitBuffApplicationSystem.RequestCorrelation` 是可选 correlation 工厂；应由共同接受子集调用，其结果只关联申请，实际出生仍以成功 Buff application 为准。

具体非致死、一拳消费和来源谱系留在 CharacterTest：[第二批实施交接](../Tasks/TACTICAL_ACCESSORY_SECOND_BATCH_IMPLEMENTATION.md)。公共 API 已离线编译，Unity、package consumer 与生产场景验收待统一协调者执行。



### 物理生产者身份补充（P1 修复合同，2026-10-12）

`HitCandidate`、`HitResolveResult` 和 `WeaponHitOnceKey` 携带 `CombatHitProducerIdentity(Kind, Id, Generation, Occurrence)`。旧构造函数仍产生空身份，保持既有近战调用合同。实际 `CombatProjectileRuntime` 在进入 `HitResolveSystem` 前赋值：Id 为原生 ProjectileId，Generation 为 SpawnSequence，Kind 区分 direct sweep 与 area pulse，Occurrence 为该相位内的 0；目标和帧继续由外层命中键提供。相同相位的重复 collider 分类仍去重，两个原生出生不因 action/trace/frame/target 相同而合并。

`WithProducerIdentity` 用于保留或补充不可变身份；伤害缩放和衍生请求复制结果时必须保留。身份参与候选比较、命中结果相等性和 HitOnce key。弹体身份由已持久化的原生 ID/出生序列恢复，不增加另一个未经恢复的临时计数器。

本合同对应独立覆盖补丁；离线证据不表示 Unity 或正式发布验收。项目层因果收据和来源移除边界见 `CharacterTest.TacticalCard.md` 的同日 P1 补充。

Tornado periodic damage uses the same CombatPhysicsWorld capsule query over CharacterHurtbox colliders as other damage reception paths. The movement-only dynamic field index is not a damage target source. Original collider IDs and deterministic query ordering reach hit-zone and producer resolution; periodic source-removal attribution still uses the frozen birth actor.

For each Tornado periodic producer and tick, the sorted capsule results select the first eligible hurtbox per target before invoking damage resolution or recording damage/body-part evidence. A failed source/target/payload filter leaves the target eligible for a later hurtbox; a successful candidate prevents later hurtboxes from overwriting its evidence. The selector resets for every producer occurrence. Target-state immunity and final hit resolution retain their existing ordering, and the entity-level hit-once key remains unchanged (no collider identity is added).
