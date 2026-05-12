# Combat Physics M11D.2：Broadphase v0

> **状态**：已完成（2026-05-09）
> **优先级**：P0
> 前置任务：`COMBAT_PHYSICS_M11D_1_SHAPE_QUERY_CONTRACT.md`（已完成，Unity EditMode Combat 全组 78/78 通过）
> 设计依据：`Docs/COMBAT_ANIMATION_PHYSICS.md`、`Docs/Tasks/COMBAT_ANIMATION_PHYSICS_EPIC.md`、`Docs/Tasks/COMBAT_PHYSICS_M11D_1_SHAPE_QUERY_CONTRACT.md`
> 派发对象：Combat Physics 子代理

## 背景

M11D.1 已经统一了 `CombatPhysicsShape`、`CombatPhysicsQuery`、`CombatPhysicsQueryBatch` 和 `CombatPhysicsQueryDebugReport`，并保留旧 Ray / AABB / Sphere / Capsule / Sector query 行为。上一轮已通过 Unity EditMode Combat 全组 78/78，说明 contract v0 和旧查询语义可以作为本阶段的稳定基线。

Broadphase 不能早于 query contract 落地，否则候选收集、过滤、排序和 debug report 会与多套旧入口交织。本阶段只在 M11D.1 统一入口之后引入 grid / spatial hash broadphase v0，用于降低 narrowphase 前的候选数量，并继续保证同输入、同 world、同 query 输出稳定。

本文档已评审通过并完成实现。

## 目标

把 Combat Physics 的候选收集从全量遍历推进到可解释、可测试的 broadphase v0：

1. 引入 grid / spatial hash broadphase，用 query AABB 收集可能相交的 collider candidate。
2. 支持静态 collider 与动态 collider 的基础索引或重建策略。
3. candidate 进入 narrowphase 前必须显式稳定排序。
4. `CombatPhysicsQueryDebugReport` 扩展 broadphase 诊断信息，能解释 raw candidate、去重、过滤和最终 hit 数量。
5. 统一 `Query` 和 `QueryBatch` 继续保持 M11D.1 的排序和行为契约。
6. 补充规模测试，覆盖几十到数百 collider 的 candidate 数量变化和稳定性。

## 范围

本阶段只优化 candidate collection，不改变 query 命中语义：

- 旧 API 和 M11D.1 `Query` / `QueryBatch` 行为不得回归。
- 同一 world、同一 query 的最终 hit 结果必须与 M11D.1 全量遍历一致。
- broadphase 可以作为 `CombatPhysicsWorld` 内部实现细节，也可以提供轻量内部类型，但不应迫使调用方改变 query contract。
- 允许在 debug report 中新增 broadphase 字段或 row 信息。
- 允许新增测试辅助构建 large world，但测试数据必须纯 C#，不依赖 Unity Scene。

## 技术约束

- Runtime 代码不引用 `UnityEngine`。
- Runtime 代码不引用 `UnityEditor`。
- 不引入 Gameplay / Buff / Ability 依赖。
- 不让 `Dictionary` 枚举顺序、hash bucket 顺序或注册顺序影响最终输出。
- Hash 只用于定位桶；candidate 输出必须进入显式排序。
- 不用 `float` / `double` 作为权威空间索引换算依据；优先复用 fixed-point / integer AABB 信息。
- 不做完整物理引擎，不做 Rigidbody、摩擦、堆叠、关节或连续碰撞求解。
- 不改变 layer mask、source entity、include source、owner/team/filter 的 M11D.1 语义。
- 不用 Console log 作为验收输出；诊断信息进入 report 或 test assertion。

## 建议实现文件

可按现有代码风格调整，但建议把范围控制在 Combat Physics 内：

```text
Assets/Scripts/MxFramework/Combat/Physics/CombatPhysicsBroadphase.cs
Assets/Scripts/MxFramework/Combat/Physics/CombatPhysicsBroadphaseCell.cs
Assets/Scripts/MxFramework/Combat/Physics/CombatPhysicsBroadphaseConfig.cs
Assets/Scripts/MxFramework/Combat/Physics/CombatPhysicsQueryDebugReport.cs
Assets/Scripts/MxFramework/Combat/Physics/CombatPhysicsWorld.cs
Docs/Tasks/COMBAT_PHYSICS_M11D_2_BROADPHASE_V0.md
```

如现有 `CombatPhysicsWorld` 结构更适合先内聚实现，也可以只新增一个 broadphase helper 文件，避免过早拆抽象。

## 建议测试文件

```text
Assets/Scripts/MxFramework/Tests/Combat/Physics/CombatPhysicsBroadphaseTests.cs
Assets/Scripts/MxFramework/Tests/Combat/Physics/CombatPhysicsWorldTests.cs
Assets/Scripts/MxFramework/Tests/Combat/Physics/CombatQueryContractTests.cs
```

重点覆盖：

- 单 query 与 M11D.1 全量遍历结果一致。
- Ray / AABB / Sphere / Capsule / Sector 的 query AABB 候选收集。
- 乱序注册 collider 后，candidate 和 hit 输出稳定。
- `QueryBatch` 在乱序 query、乱序 collider 注册下稳定。
- 大规模 collider world 下 candidate 数量减少且 report 可解释。
- source / layer filter 计数不因 broadphase 改变语义。

## 验收标准

- 同一输入下，最终 query hit 结果与 M11D.1 全量遍历一致。
- candidate 数量减少可解释：debug report 能区分 broadphase raw candidate、去重后 candidate、filter 后 candidate、hit count。
- 乱序注册 body / collider 不影响单 query 输出。
- `QueryBatch` 输出稳定，且每个 query 内部 hit 继续使用 M11D.1 的稳定排序规则。
- 规模测试覆盖至少 100 个 collider；建议再覆盖 500 个 collider 的 candidate collection 稳定性。
- 对跨 cell、大尺寸 collider、边界落点和空 query AABB 有测试。
- Unity Console 无 error。
- 现有 Combat EditMode 测试继续通过；至少覆盖 `MxFramework.Tests.Combat.Physics.*`，如环境允许跑 `MxFramework.Tests.Combat` 全组。

## 非目标

- 不改变 query hit 判断算法。
- 不改变 public query API 或 M11D.1 contract。
- 不做 OBB narrowphase 实现。
- 不做多线程 broadphase。
- 不做 job system / burst / native container 版本。
- 不接 Unity Physics 或 Unity Collider。
- 不做 Authoring UI / Gizmo 可视化，除非 debug report 字段需要最小同步说明。
- 不做完整静态场景 bake pipeline。

## 测试要求

子代理完成后不要提交 SVN，由主代理复核和提交。子代理至少自查：

- Unity MCP refresh / Console error 检查。
- `MxFramework.Tests.Combat.Physics.*`。
- `MxFramework.Tests.Combat.Animation.WeaponTraceQueryBuilderTests`。
- `MxFramework.Tests.Combat.Hit.*`。
- 如测试集合稳定，可跑 `MxFramework.Tests.Combat` 全组。

测试中必须保留一个可对照的 full scan oracle，确保 broadphase 只减少候选，不改变最终命中。

## 完成记录

- 2026-05-09：完成 broadphase v0。
  - 新增 `CombatPhysicsBroadphaseConfig`、`CombatPhysicsBroadphase`、`CombatPhysicsBroadphaseCell`，统一 `Query` / `QueryBatch` / `ExplainQuery` 通过 fixed grid broadphase 收集候选。
  - 旧 `QueryRay` / `QuerySphere` / `QueryCapsule` / `QueryAabb` / `QuerySector` 继续保持 full scan，作为语义基线和测试 oracle。
  - `CombatPhysicsQueryDebugReport` 增加 broadphase raw candidate、去重 candidate、filter 后 candidate 和 cell count 统计。
  - 新增 `CombatPhysicsBroadphaseTests`，覆盖 full scan oracle 一致性、所有 query shape、乱序注册稳定性、batch 稳定性、raw / dedup / filter / hit 诊断计数。
  - M11D.2 只改变统一入口候选收集，不改变 narrowphase 命中判断和旧 public query API。

验证记录：

- `dotnet build WGameFramework.sln --no-restore -v minimal`：通过，0 error，10 个既有 warning。
- Unity MCP EditMode：`CombatPhysicsBroadphaseTests` / `CombatPhysicsWorldTests` / `CombatQueryContractTests` 共 26 / 26 passed。
- Unity MCP EditMode：`MxFramework.Tests.Combat` 共 83 / 83 passed。
- Unity Console：0 error；仅出现 MCP 插件自身 WebSocket warning，与本任务无关。

实现开工前确认：

- 本文档已评审通过。
- M11D.1 基线测试仍可复现。
- 当前工作树没有需要合并的他人 Combat Physics 改动。

## 提交边界

本任务实现阶段只允许修改 Combat Physics / Combat Tests / 本任务文档，必要时最小同步 epic 链接。不要混入：

- Gameplay / Buff / Ability 模块改动。
- Authoring UI / Showcase 表现改动。
- `Docs/CAPABILITIES.md` 改动，除非主代理明确要求。
- 本地工具缓存、个人插件状态、临时脚本或 Unity 生成噪音。
