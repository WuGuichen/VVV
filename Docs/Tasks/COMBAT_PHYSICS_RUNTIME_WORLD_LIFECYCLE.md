# Combat Physics：Runtime World Lifecycle

> **状态**：Accepted / Closed（2026-05-09）
> **优先级**：P0
> **功能包类型**：Runtime 基础能力
> **前置完成项**：
> - `COMBAT_PHYSICS_M11D_1_SHAPE_QUERY_CONTRACT.md`
> - `COMBAT_PHYSICS_M11D_2_BROADPHASE_V0.md`
> - `COMBAT_PHYSICS_HIT_QUERY_DEBUG_VISUALIZATION.md`
> **设计依据**：
> - `Docs/COMBAT_ANIMATION_PHYSICS.md`
> - `Docs/Tasks/COMBAT_ANIMATION_PHYSICS_EPIC.md`

## 功能目标

把 `CombatPhysicsWorld` 从“能注册 collider 并查询”推进到“可长期运行、可重置、可复制、可诊断的 Runtime World”。

## 校准记录

本文件原状态仍写作 `Ready for dispatch`，但当前代码和测试已经出现 lifecycle 实现证据。文档校准时先标为 `Implemented / Closeout pending`；2026-05-09 已完成正式 closeout，状态收口为 `Accepted / Closed`。

已发现的实现证据：

- `CombatPhysicsWorld` 已暴露 `Revision`、`CreateStats()`、`TryGetBody()`、`TryGetAabbCollider()`、`SetBodyPosition()`、`MoveBody()`、`RemoveBody()`、`RemoveCollider()`、`Clear()`、`CopyBodiesTo()`、`CopyAabbCollidersTo()`。
- `CombatPhysicsWorldTests` 已覆盖 body / collider 删除、clear 后 query 为空、batch result snapshot 不受后续 world mutation 影响、revision / stats、stable copy order。
- `CombatKinematicMotorTests` 和 `RuntimeCombatShowcaseRunnerTests` 已依赖 world revision 与移动后 body position 查询，证明 Motion v0 使用了 lifecycle 基础能力。

## Closeout 记录

验收日期：2026-05-09。

实现范围确认：

- `CombatPhysicsWorld` 支持 body / collider 注册、查找、移动、删除、清空、revision、stats 和稳定 copy 输出。
- Body 移动后，后续 query / debug report 读取新 world position。
- Body / collider 删除后，query 不再返回已删除 collider，debug report 不保留 orphan row。
- `Clear()` 后 stats 归零，query 返回空结果，空 world 重复 clear 不继续增加 revision。
- Query batch result 已验证为 snapshot 结果，world 后续 mutation 不污染已返回结果。
- Broadphase scale / regression 已覆盖 120 collider line world、全 shape oracle 对比、注册顺序稳定、batch 稳定、raw / dedup / post-filter report。

验收命令与结果：

- `dotnet build WGameFramework.sln --no-restore -v minimal`
  - 通过，0 warning / 0 error，用时 00:00:02.89。
- Unity MCP EditMode `MxFramework.Tests.Combat`
  - 通过，105 / 105 passed，0 failed，0 skipped，用时 5.029s。
- Unity Console
  - 通过，0 error，0 warning。
- `git diff --stat && git diff --check`
  - 影响面检查完成。

关键测试覆盖：

- `CombatPhysicsWorldTests.RemoveBody_CascadesColliderRemoval`
- `CombatPhysicsWorldTests.RemoveCollider_RemovesOnlyRequestedCollider`
- `CombatPhysicsWorldTests.Clear_RemovesBodiesCollidersAndDebugCandidates`
- `CombatPhysicsWorldTests.QueryBatch_ResultSnapshotRemainsStableAfterWorldMutation`
- `CombatPhysicsWorldTests.RuntimeLifecycleApi_TracksRevisionLookupMovementAndStats`
- `CombatPhysicsWorldTests.RuntimeLifecycleApi_CopiesBodiesAndCollidersInStableOrder`
- `CombatPhysicsBroadphaseTests.Broadphase_ReducesCandidatesButMatchesFullScanOracle`
- `CombatPhysicsBroadphaseTests.Broadphase_AllQueryShapesMatchFullScanOracle`
- `CombatPhysicsBroadphaseTests.Broadphase_RegistrationOrderDoesNotAffectCandidatesOrHits`
- `CombatPhysicsBroadphaseTests.Broadphase_QueryBatchRemainsStable`

剩余风险：

- 当前 lifecycle 仍是 v0，未实现完整 runtime clone object API；已返回的 query batch snapshot 和 `CopyBodiesTo` / `CopyAabbCollidersTo` 覆盖了本阶段只读隔离需求。
- 未做动态刚体、移动平台、多线程、Job/Burst 或 native container；这些仍属于后续功能包。

完成后，Combat Physics 应能稳定支撑以下运行时场景：

1. 战斗实体进入世界，注册 body 和一个或多个 collider。
2. 实体移动、朝向变化、collider 形状替换、layer/filter 调整后，后续 query 立即读取新状态。
3. 实体死亡、离场、换场或 rollback reset 时，body / collider 能被显式删除或整体清空。
4. 世界 revision / stats 能说明本帧 world 是否变化、变化了多少 body / collider / broadphase cell。
5. Snapshot / copy 能为 replay、preview、authoring explain 和测试提供稳定只读视图。
6. 生命周期操作不会改变既有 Ray / Sphere / Capsule / AABB / Sector 查询语义。
7. 在几十到数百 collider 的规模下，移动、删除、替换和 query 结果仍稳定可复现。

这个功能包按“运行时世界生命周期”验收，不再拆成微任务编号。子代理内部可以拆 registry、mutation、snapshot、stats、tests，但顶层交付必须是一组可被战斗运行时持续使用的基础能力。

## 用户可见结果

- 制作人或工具侧可以在 Showcase / Authoring explain 中看到 world 当前 body / collider / cell / revision 摘要。
- 移动一个目标后，再触发同一 query，命中结果和 debug report 反映新位置，而不是残留旧 broadphase candidate。
- 删除一个目标后，再触发 query，不再命中已删除 collider，也不会留下 orphan collider。
- 替换 hurtbox / hitbox 形状后，query explain 能显示新形状参与查询。
- 同一 world 做 snapshot / copy 后，复制世界的 query 结果与原 world 在复制时刻一致；之后原 world 继续变化不会污染旧 snapshot。
- 重复运行同一组生命周期脚本，revision、stats、query result 和 debug summary 顺序稳定。

## 范围

允许修改：

- `CombatPhysicsWorld` 的 body / collider 生命周期 API。
- body / collider 移动、删除、替换、启用禁用、layer/filter 更新的最小 runtime contract。
- broadphase 在 world mutation 后的更新或重建策略。
- world revision、stats、snapshot、copy / clone 的只读 DTO。
- query debug report 中与 lifecycle / revision / stats 对齐的最小字段。
- Combat Physics tests、Combat Authoring explain tests、规模测试。
- 本任务文档和 Epic 的当前制作优先级。

建议关注文件范围：

```text
Assets/Scripts/MxFramework/Combat/Physics/
Assets/Scripts/MxFramework/Combat/Diagnostics/
Assets/Scripts/MxFramework/Tests/Combat/Physics/
Assets/Scripts/MxFramework/Tests/Combat/Authoring/
Docs/Tasks/COMBAT_PHYSICS_RUNTIME_WORLD_LIFECYCLE.md
Docs/Tasks/COMBAT_ANIMATION_PHYSICS_EPIC.md
```

如现有 `CombatPhysicsWorld` 已经有部分能力，优先补齐 contract、诊断和测试，不做无意义重命名。

## 非目标

- 不做完整刚体模拟、碰撞响应、摩擦、堆叠、关节或连续碰撞求解。
- 不接入 Unity Physics / Unity Collider 作为权威结果。
- 不改变 query narrowphase 命中算法。
- 不改变 M11D.1 / M11D.2 已验证的 query hit 排序和 filter 语义。
- 不引入 Gameplay / Buff / Ability 业务依赖。
- 不引入 WGame 真实角色、技能、Buff 或关卡数据。
- 不做 Scene View Gizmo / EditorWindow UI；本包只提供 runtime world 能力和可诊断数据。
- 不把 Console log 作为验收输出。
- 不做多线程、job system、Burst 或 native container 版本。

## 串行链路

本功能包内部必须保持以下先后顺序：

1. 梳理现有 world 生命周期入口：确认 body / collider 注册、更新、查询、debug report 和 broadphase rebuild 的当前边界。
2. 稳定 mutation contract：定义移动、删除、替换、启用禁用、layer/filter 更新后的 revision 和 broadphase 行为。
3. 补 world stats / revision：让 query、snapshot 和 debug report 能指向同一个 world 版本。
4. 补 snapshot / copy：提供稳定只读视图或复制 world，服务 replay、preview 和 authoring explain。
5. 回归旧 query：证明生命周期改动不改变 Ray / AABB / Sphere / Capsule / Sector 命中语义。
6. 规模测试：覆盖大量 body / collider 的移动、删除、替换和重复 query。

没有 mutation contract 前，不先做 snapshot；没有 revision / stats 前，不把 debug UI 或 Authoring 文本绑定到临时字段。

## 可并行工作

在 mutation contract 确定后，可以并行派发：

- Runtime World 子代理：实现 body / collider mutation、revision、stats。
- Broadphase 子代理：验证移动、删除、替换后 broadphase index 不残留旧 candidate。
- Snapshot 子代理：实现 snapshot / copy DTO 和稳定排序。
- Test 子代理：补 lifecycle contract tests、规模测试、query 回归测试。
- Authoring / Diagnostics 子代理：把 world stats / revision 接入现有 explain 文本，但只能读取公开 snapshot。

这些子任务只通过 world contract、snapshot 和 debug report 协作，不互相读取私有容器。

## 验收标准

功能验收：

- 可以注册一个 body 和多个 collider，并通过 unified query 命中。
- body 移动后，query 命中位置和 debug report 使用新位置。
- collider 删除后，query 不再返回该 collider，debug report 不残留 orphan row。
- collider 替换 shape / layer 后，query 结果和 filter reason 使用新配置。
- world clear / reset 后，stats 归零，query 返回空结果，revision 变化可解释。
- snapshot / copy 在复制时刻与原 world 查询结果一致；复制后原 world 继续变化，不影响旧 snapshot / copy。
- 同一生命周期脚本重复执行，结果顺序、stats、revision 增长规则稳定。

技术验收：

- Combat Physics Runtime 不引用 `UnityEditor`。
- Combat Physics Runtime 不引用 `UnityEngine.Physics`。
- 纯 Physics lifecycle 不依赖 Demo / UI / Gameplay / Authoring。
- 不改变现有 query hit 语义和排序。
- broadphase mutation 后没有 stale candidate、duplicate candidate 或 orphan collider。
- 所有公开 snapshot / stats 输出都有显式排序，不依赖 `Dictionary` 枚举顺序。
- Console 最终无 error。

文档验收：

- 本文档记录最终实现范围、验收结果和规模测试数据。
- Epic 的当前制作优先级指向本功能包。
- 如发现必须拆后续包，只记录为后续功能点，不把当前包拆回微任务列表。

## 测试门槛

最低测试：

```text
dotnet build WGameFramework.sln --no-restore -v minimal
Unity EditMode: MxFramework.Tests.Combat.Physics.*
Unity EditMode: MxFramework.Tests.Combat
Unity Console: 0 error
git diff --stat && git diff --check
```

必须补充或确认覆盖：

- Body register / move / remove lifecycle tests。
- Collider add / remove / replace / layer update lifecycle tests。
- World clear / reset revision and stats tests。
- Snapshot / copy isolation tests。
- Query regression tests：Ray / AABB / Sphere / Capsule / Sector。
- Broadphase stale candidate tests：移动出 cell、跨 cell、删除后 query、替换大尺寸 collider。
- Scale tests：至少 100 个 collider，建议再覆盖 500 个 collider。

验收输出不要只写“测试通过”，必须记录：

- 生命周期脚本覆盖了哪些操作；
- world revision 和 stats 的关键变化；
- 至少一种 query 在移动 / 删除 / 替换前后的结果变化；
- snapshot / copy 的隔离结论；
- Console 最终状态；
- 影响面风险说明。

## 完成后演示方式

推荐演示脚本：

1. 创建或加载一个包含 Player、Enemy、Obstacle 的 CombatPhysicsWorld。
2. 注册 Player body，以及 Enemy 的 hurtbox collider 和 Obstacle 的 block layer collider。
3. 触发一次 Capsule 或 Sector query，记录 hit、candidate 和 world stats。
4. 移动 Enemy 到范围外，再触发同一 query，确认 hit 变为 miss，broadphase / debug report 不残留旧 cell。
5. 将 Enemy collider 替换为更大或更小 shape，再触发 query，确认结果随 shape 变化。
6. 删除 Enemy body，再触发 query，确认没有 orphan collider。
7. 对 world 做 snapshot / copy，随后修改原 world，确认旧 snapshot / copy 查询结果保持复制时刻状态。
8. 执行 world clear / reset，确认 stats 归零，query 返回空结果。

演示通过标准：制作人无需阅读内部容器，就能判断“这个物理世界当前有多少对象、何时变化、为什么这次查询读到了新状态或空状态”。

## 派发提示

派发给子代理时使用功能包口径：

```text
你负责 WGameFramework 功能包 `Combat Physics：Runtime World Lifecycle` 的实现。
目标不是新增一个小接口，而是交付 CombatPhysicsWorld 可长期运行的 body/collider 生命周期、revision/stats、snapshot/copy、query regression 和规模测试。
先读 AGENTS.md、COMBAT_PHYSICS_RUNTIME_WORLD_LIFECYCLE.md、COMBAT_PHYSICS_M11D_2_BROADPHASE_V0.md、COMBAT_PHYSICS_HIT_QUERY_DEBUG_VISUALIZATION.md。
不要回退他人改动。
只在文档允许范围内修改文件；如需越界，先说明原因。
完成后必须跑 build、Combat 测试、Unity Console 检查和影响面检查，并记录生命周期脚本、stats/revision、snapshot/copy 和规模测试结论。
```

## 当前记录

- 2026-05-09：创建功能包任务文档。
- 2026-05-09：文档校准发现 lifecycle API 与测试已存在，状态从 `Ready for dispatch` 调整为 `Implemented / Closeout pending`。下一步不是重做实现，而是补最终 closeout 验收记录。
- 2026-05-09：正式 closeout 完成，build / Unity Combat EditMode / Console / 影响面检查均通过，状态收口为 `Accepted / Closed`。
