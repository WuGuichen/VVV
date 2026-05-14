# Combat 接口

> 状态：Combat Physics / Motion / Hit Resolve / RuntimeHost animation modules 已实现并有测试覆盖。本文记录当前源码中已经存在的 `MxFramework.Combat` 公开契约，不收录设计文档中尚未落地的 `CombatWorld`、`ActionSystem` 等草案类型。

## 职责

Combat 提供 noEngine 的确定性战斗物理、动作时间轴、命中结算、角色运动和诊断能力。它不依赖 `UnityEngine.Physics`、`Animator` 或场景对象；Unity 侧只能作为表现、authoring 或调试入口。

核心边界：
- `MxFramework.Combat` 引用 `MxFramework.Core` 和 `MxFramework.Runtime`，`noEngineReferences=true`。
- `MxFramework.Combat.GameplayBridge` 是独立桥接层，负责把命中结果转成 Gameplay / Buff 侧可消费的事件。
- `Combat.Authoring` 和 `Combat.Editor` 不属于运行时契约；本文只在测试入口中标注它们的验证路径。

## 公开接口概览

| 分组 | 公开类型 | 用途 |
|------|----------|------|
| Core | `CombatFrame` / `CombatFrameClock` / `CombatStepConfig` | 固定帧时钟、步进配置和帧推进 |
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
| Animation | `CombatActionTimeline` / `CombatActionWindow` / `CombatFrameRange` / `CombatActionWindowKind` | 动作时间轴、窗口和帧范围 |
| Animation | `CombatActionRunner` / `CombatActionRegistry` / `CombatActionInstance` / `CombatActionState` / `CombatActionPhase` | 动作注册、运行、状态和阶段 |
| Animation | `ActionResult` / `ActionStartedEvent` / `ActionPhaseChangedEvent` / `ActionFinishedEvent` / `ActionCanceledEvent` / `ActionCancelRejectedEvent` | 动作运行结果和生命周期事件 |
| Animation | `ICombatAnimationContext` / `CombatAnimationContext` / `CombatAnimationSnapshot` | 动作系统可读写上下文和快照 |
| Animation Runtime | `CombatActionRuntimeModule` / `CombatWeaponTraceRuntimeModule` / `CombatAnimationDiagnosticsModule` | `RuntimeHost` 模块化动作、武器轨迹和诊断推进 |
| Weapon Trace | `WeaponTraceFrame` / `WeaponTraceSegment` / `WeaponHitOnceKey` / `CombatWeaponTraceEvaluator` / `WeaponTraceQueryBuilder` | 武器轨迹采样、查询构建和 hit-once key |
| Trace Provider | `ICombatActionTraceProvider` / `CombatActionTimelineTraceProvider` | 从动作时间轴读取 weapon trace |
| Diagnostics | `CombatDebugSnapshot` / `CombatDebugSnapshotBuilder` / `CombatHitExplain` / `CombatQueryTrace` | 运行时可读诊断快照和命中 explain |
| Replay | `CombatReplayInput` / `CombatReplayRecorder` / `CombatDesyncDump` | replay 输入记录和 desync dump |
| Gameplay Bridge | `CombatGameplayEventBridge` | 将 Combat 命中结果桥接到 Gameplay / Buff 侧 |

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
