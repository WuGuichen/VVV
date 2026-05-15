# Combat 接口

> 状态：Combat Physics / Motion / Hit Resolve / RuntimeHost animation modules 已实现并有测试覆盖。本文记录当前源码中已经存在的 `MxFramework.Combat` 公开契约，不收录设计文档中尚未落地的 `CombatWorld`、`ActionSystem` 等草案类型。

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
| Physics Diagnostics | `CombatPhysicsQueryDebugReport` / `CombatPhysicsQueryDebugSummary` / `CombatPhysicsQueryDebugRow` / `CombatPhysicsQueryDebugRowStatus` | 查询 explain / broadphase / filter / hit 诊断 |
| Motion | `CombatKinematicMotor` | 固定帧运动、重力、跳跃、碰撞阻挡和可选物理世界同步 |
| Motion | `CombatMotionState` / `CombatMotionInput` / `CombatMotionConfig` / `CombatMotionStepResult` | 运动状态、输入、配置和步进结果 |
| Motion | `CombatMotionCapsuleProxy` / `CombatMotionCollision` / `CombatMotionCollisionFlags` | capsule proxy 与运动碰撞摘要 |
| Hit | `HitResolveSystem` | 对命中候选执行稳定排序、hit-once、防友伤和目标状态过滤 |
| Hit | `HitCandidate` / `HitResolveResult` / `HitResolveKind` / `HitTargetStateFlags` | 命中候选、结算结果、结果类型和目标状态 flags |
| Hit | `ITeamRelationProvider` / `IHitTargetStateResolver` / `ICombatEventDispatcher` | 队伍关系、目标状态和命中事件派发扩展点 |
| Animation | `CombatActionTimeline` / `CombatActionFrameEvent` / `CombatActionWindow` / `CombatFrameRange` / `CombatActionWindowKind` | 动作时间轴、表现关联事件、窗口和帧范围 |
| Animation | `CombatActionRunner` / `CombatActionRegistry` / `CombatActionInstance` / `CombatActionState` / `CombatActionPhase` | 动作注册、运行、状态和阶段 |
| Animation | `ActionResult` / `ActionStartedEvent` / `ActionPhaseChangedEvent` / `ActionFrameEventRaisedEvent` / `ActionFinishedEvent` / `ActionCanceledEvent` / `ActionCancelRejectedEvent` | 动作运行结果和生命周期事件 |
| Animation | `ICombatAnimationContext` / `CombatAnimationContext` / `CombatAnimationSnapshot` | 动作系统可读写上下文和快照 |
| Animation Runtime | `CombatActionRuntimeModule` / `CombatWeaponTraceRuntimeModule` / `CombatAnimationDiagnosticsModule` | `RuntimeHost` 模块化动作、武器轨迹和诊断推进 |
| Weapon Trace | `WeaponTraceFrame` / `WeaponTraceSegment` / `WeaponHitOnceKey` / `CombatWeaponTraceEvaluator` / `WeaponTraceQueryBuilder` | 武器轨迹采样、查询构建和 hit-once key |
| Trace Provider | `ICombatActionTraceProvider` / `CombatActionTimelineTraceProvider` | 从动作时间轴读取 weapon trace |
| Diagnostics | `CombatDebugSnapshot` / `CombatDebugSnapshotBuilder` / `CombatHitExplain` / `CombatQueryTrace` | 运行时可读诊断快照和命中 explain |
| Replay | `CombatReplayInput` / `CombatReplayRecorder` / `CombatDesyncDump` | replay 输入记录和 desync dump |
| Gameplay Bridge | `CombatGameplayEventBridge` | 将 Combat 命中结果桥接到 Gameplay / Buff 侧 |

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

`CombatPhysicsWorld` 是当前战斗物理权威入口。已实现能力：
- `UpsertBody` / `RemoveBody` / `TryGetBody` / `MoveBody` / `SetBodyPosition` / `Clear`。
- `UpsertAabbCollider` / `RemoveCollider` / `TryGetAabbCollider`。
- `Query` 支持 Ray、Sphere、Capsule、AABB、Sector；OBB 当前显式返回不支持。
- `QueryBatch` 按 query header 和 source index 稳定排序。
- `ExplainQuery` 输出 raw candidate、dedup、post-filter、hit / miss / filtered row 和 broadphase cell 统计。
- `CopyBodiesTo` / `CopyAabbCollidersTo` 提供稳定排序快照。
- `Revision` / `CreateStats()` 用于生命周期和诊断变更检测。

查询结果排序遵守源码中的稳定比较：query header、distance、target entity、body、collider 等字段确定性排序，避免依赖集合插入顺序。

## Motion v1

`CombatKinematicMotor` 是当前 noEngine 角色运动入口。它消费 `CombatMotionState` 和 `CombatMotionInput`，输出 `CombatMotionStepResult`。

已实现能力：
- 固定帧水平移动、重力、跳跃、最大下落速度和 grounded 判定。
- `CombatMotionCapsuleProxy` 作为 capsule 角色代理。
- 通过 `CombatPhysicsWorld` 查询障碍，并按 `CombatMotionCollisionFlags` 报告 Grounded、Wall、Ceiling、BlockedX/Y/Z、IterationLimit。
- 可选把运动结果同步回 `CombatPhysicsWorld.SetBodyPosition`。

## Hit Resolve v0

`HitResolveSystem` 对 `HitCandidate` 执行结算，调用方提供 consumed hit-once key set 和结果列表。

已实现能力：
- hit-once 去重，重复命中输出 `HitResolveKind.Duplicate`。
- owner / self damage 防护。
- `ITeamRelationProvider` 支持 same-team / friendly / hostile 判定和 friendly fire 配置。
- `IHitTargetStateResolver` 支持动态目标状态覆盖候选状态。
- `HitTargetStateFlags` 支持 Alive、Invincible、Parrying、Blocking、SuperArmor 等状态过滤。
- `ICombatEventDispatcher` 可接收 resolved / blocked 事件。

## Action / RuntimeHost 模块

动作运行时使用 `CombatActionTimeline`、`CombatActionRunner` 和 `CombatAnimationContext`，并通过 RuntimeHost 模块接入统一 tick：
- `CombatActionRuntimeModule` 推进动作状态。
- `CombatWeaponTraceRuntimeModule` 根据动作时间轴和 trace provider 生成武器轨迹查询。
- `CombatAnimationDiagnosticsModule` 输出动作 / trace / hit 诊断快照。

`CombatActionRunner.ActionFrameEventRaised` 发布 noEngine 的固定帧表现关联事件。`StartAction` / `ForceStartAction` 成功时先发布 `ActionStartedEvent`，再发布 local frame 0 的 `CombatActionFrameEvent`；`TickActions` 每次推进 running action 后，按稳定 entity id 顺序和 `CombatActionFrameEvent.CompareTo` 顺序发布该 local frame 的事件。payload `ActionFrameEventRaisedEvent` 包含 entity、action、action instance、world frame、local frame 和原始 frame event。

frame event 只提供 deterministic presentation correlation，不承载 VFX / SFX / Camera / Footstep / UI kind，也不承载 `ResourceKey`。Unity 或 MxAnimation bridge 需要从 animation binding、bridge 配置或其他表现层配置解析资源和表现类型；该事件不得反向驱动取消窗口、命中、伤害、replay hash 或 Combat 权威状态。

`MxFramework.Combat.Animation.Unity` 是独立 Unity bridge assembly。它消费 `CombatActionRunner` lifecycle events 和 `ActionFrameEventRaised`，按默认 `action:<ActionId>` key 或显式 bridge config 查找 `MxAnimationSetDefinition` / `MxAnimationActionBinding`，再向 `IMxAnimationBackend` 发 play / stop / crossfade 请求或向表现事件 sink dispatch `MxAnimationPresentationEvent`。bridge diagnostics 保留 entity、action、action instance、world frame、local frame 和原始 frame event correlation。

旧 `MxFramework.Runtime.Unity.CombatAnimationUnityModule` / `CombatAnimatorDriver` 仍保留为 opt-in Animator 迁移路径。新 MxAnimation bridge 不自动注册旧 module；项目层 composition root 不应在同一 entity 上同时启用两套 bridge，以免同一 Combat event 双触发动画。

`CombatActionState` 包含 `ActionInstanceId`，用于在 multi-step Runtime tick 内保留每个动作实例的 hit-once 身份；RuntimeHost weapon trace 模块基于每个 fixed step 后的动作状态快照计算候选，不从 Runtime frame 直接推导 Combat frame。

默认模块位于 RuntimeHost 阶段中运行，具体 priority 和组合根由 Demo / 项目层配置。动作系统不读取 Unity Animator 状态作为权威。

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

主要测试目录：`Assets/Scripts/MxFramework/Tests/Combat/`

当前已存在的测试入口包括：
- `Tests/Combat/Core/CombatFrameClockTests.cs`
- `Tests/Combat/Core/CombatFixedStepDriverTests.cs`
- `Tests/Combat/Core/CombatHashTests.cs`
- `Tests/Combat/Core/CombatSortKeyTests.cs`
- `Tests/Combat/Physics/CombatPhysicsWorldTests.cs`
- `Tests/Combat/Physics/CombatPhysicsBroadphaseTests.cs`
- `Tests/Combat/Physics/CombatQueryContractTests.cs`
- `Tests/Combat/Motion/CombatKinematicMotorTests.cs`
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
