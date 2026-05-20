# Combat Physics M11D.3：OBB Query v0

> **状态**：已完成（2026-05-20）
> **优先级**：P0
> 前置任务：`COMBAT_PHYSICS_M11D_1_SHAPE_QUERY_CONTRACT.md`、`COMBAT_PHYSICS_M11D_2_BROADPHASE_V0.md`
> 设计依据：`Docs/COMBAT_ANIMATION_PHYSICS.md`、`Docs/Tasks/COMBAT_ANIMATION_PHYSICS_EPIC.md`、`Docs/Tasks/COMBAT_PHYSICS_M11D_1_SHAPE_QUERY_CONTRACT.md`、`Docs/Tasks/COMBAT_PHYSICS_M11D_2_BROADPHASE_V0.md`
> 派发对象：Combat Physics 子代理

## 背景

M11D.1 已经在 `CombatPhysicsShapeKind`、`CombatPhysicsShape` 和 `CombatQueryKind` 中预留 OBB，并要求 OBB unsupported 状态必须显式返回。M11D.2 已经把统一 query 入口接入 fixed grid broadphase，但明确不做 OBB narrowphase。

当前缺口是：战斗系统文档把 OBB 规划为“矩形攻击盒、武器盒”，但 runtime 仍无法执行 OBB query。继续只依赖 AABB / Capsule 会让部分横斩、长方体判定、武器盒和可视化调试偏离策划预期。

本阶段只把 OBB 作为 **query shape** 落地，目标 collider 仍然沿用现有 `CombatPhysicsAabbCollider`。不要在本阶段引入 OBB collider、旋转 body、完整 collision solver 或 Character Motion 的 OBB 障碍物支持。

## 目标

把 Combat Physics 从“预留 OBB 契约”推进到“可用于攻击判定的 OBB query v0”：

1. 新增 `CombatObbQuery` 或等价专用 DTO，并接入 `CombatPhysicsQuery.From(...)` / `ToObbQuery()`。
2. `CombatPhysicsWorld.Query` / `QueryBatch` / `ExplainQuery` 支持 OBB query，不再返回 unsupported。
3. broadphase 能根据 OBB 的 world-space AABB 收集 candidate。
4. narrowphase 至少支持 OBB query vs AABB collider overlap。
5. 命中结果排序继续沿用 `CombatQueryResult.CompareTo`，不受注册顺序影响。
6. debug report 能解释 OBB query 的 candidate、filter、hit / miss 状态。
7. 现有 Ray / Sphere / Capsule / AABB / Sector 查询行为不回归。

## 范围

本阶段实现 OBB query v0，范围控制在 Combat Physics 内：

- OBB query 数据：
  - center。
  - half extents。
  - axisX / axisY / axisZ。
  - 轴必须非零；建议在构造或 query 执行前归一化，归一化失败必须抛出结构化异常。
- broadphase：
  - 从 OBB 的 3 个轴和 half extents 计算 conservative AABB。
  - 复用 M11D.2 的 grid candidate collection、去重和稳定排序。
- narrowphase：
  - 支持 OBB overlap AABB。
  - 推荐使用 Separating Axis Theorem，至少覆盖 OBB 三轴和世界 X/Y/Z 三轴；如实现 3D 完整 SAT，应覆盖 cross axes 并处理平行轴退化。
  - 对 v0 来说，命中 point / normal 可以先返回稳定近似值，但必须在文档和测试中固定语义。
- debug report：
  - OBB 不再进入 unsupported report。
  - row status 继续使用 Hit / Miss / FilteredLayer / FilteredSource。
  - broadphase raw / dedup / post-filter / hit count 继续可解释。

## 技术约束

- Runtime 代码不引用 `UnityEngine`。
- Runtime 代码不引用 `UnityEditor`。
- 不引入 Gameplay / Buff / Ability 依赖。
- 不用 `float` / `double` 作为权威碰撞结果。
- 不改变现有 AABB collider 注册 API 的语义。
- 不改变旧 `QueryRay` / `QuerySphere` / `QueryCapsule` / `QueryAabb` / `QuerySector` 的行为。
- 不让 `Dictionary` 枚举顺序、注册顺序或 broadphase cell 顺序影响最终输出。
- OBB axis 输入如果不是单位向量，必须有明确处理策略：归一化后使用，或拒绝输入；不要静默产生非确定性误差。
- 零尺寸 OBB 必须有测试覆盖；允许作为退化盒参与 overlap，但行为必须稳定。

## 建议实现文件

可按现有代码风格调整，但建议范围控制在：

```text
Assets/Scripts/MxFramework/Combat/Physics/CombatQueries.cs
Assets/Scripts/MxFramework/Combat/Physics/CombatPhysicsShape.cs
Assets/Scripts/MxFramework/Combat/Physics/CombatPhysicsQuery.cs
Assets/Scripts/MxFramework/Combat/Physics/CombatPhysicsBroadphase.cs
Assets/Scripts/MxFramework/Combat/Physics/CombatPhysicsWorld.cs
Assets/Scripts/MxFramework/Tests/Combat/Physics/CombatQueryContractTests.cs
Assets/Scripts/MxFramework/Tests/Combat/Physics/CombatPhysicsBroadphaseTests.cs
Assets/Scripts/MxFramework/Tests/Combat/Physics/CombatPhysicsWorldTests.cs
Docs/Tasks/COMBAT_PHYSICS_M11D_3_OBB_QUERY_V0.md
```

如 narrowphase helper 代码明显膨胀，可以新增：

```text
Assets/Scripts/MxFramework/Combat/Physics/CombatPhysicsObbMath.cs
```

新增 helper 必须保持 `internal` 或最小 public surface，避免提前冻结不成熟 API。

## 建议测试文件

```text
Assets/Scripts/MxFramework/Tests/Combat/Physics/CombatPhysicsWorldTests.cs
Assets/Scripts/MxFramework/Tests/Combat/Physics/CombatPhysicsBroadphaseTests.cs
Assets/Scripts/MxFramework/Tests/Combat/Physics/CombatQueryContractTests.cs
```

重点覆盖：

- `CombatObbQuery` 构造校验：header kind、half extents、zero axis、非单位 axis 处理策略。
- `CombatPhysicsQuery.From(CombatObbQuery)` 与 `ToObbQuery()` roundtrip。
- axis-aligned OBB 与同尺寸 AABB query 结果一致。
- 旋转 OBB 命中 AABB collider。
- 旋转 OBB 与远离 AABB collider 不命中。
- OBB broadphase AABB 能覆盖旋转后的外包盒，不漏 candidate。
- OBB query 在乱序 body / collider 注册下输出稳定。
- `QueryBatch` 混合 Ray / Capsule / OBB / Sector 时输出稳定。
- `ExplainQuery` 对 OBB 返回 candidate、filter、hit / miss 统计，而不是 unsupported。
- 零厚度或极薄 OBB 的稳定行为。

## 验收标准

- OBB query 不再抛出 `NotSupportedException`。
- `ExplainQuery` / `QueryBatch` 中 OBB 不再标记 `IsUnsupported`。
- OBB vs AABB overlap 覆盖轴对齐、旋转、边界接触、完全包含、完全分离和退化尺寸。
- OBB broadphase 不漏掉 narrowphase 应命中的 AABB collider。
- OBB query 输出排序稳定，且不依赖注册顺序。
- Ray / Sphere / Capsule / AABB / Sector 既有测试全部继续通过。
- `MxFramework.Tests.Combat.Physics.*` 通过；如环境允许，跑 `MxFramework.Tests.Combat` 全组。
- Unity Console 无 error。

## 非目标

- 不新增 `CombatPhysicsObbCollider`。
- 不新增 `UpsertObbCollider` / `TryGetObbCollider` / `CopyObbCollidersTo`。
- 不把 `CombatPhysicsBody` 从 position-only 扩展为 rotation-aware body。
- 不做 OBB collider vs Ray / Sphere / Capsule / Sector 的完整矩阵。
- 不做 Character Motion 对 OBB 障碍物的 sweep / slide。
- 不做 Unity Collider / Gizmo / Authoring UI 适配。
- 不做 Burst / Job System / NativeContainer 优化。
- 不做连续旋转扫掠、穿透恢复、摩擦、堆叠或刚体模拟。

## 后续切片

OBB query v0 完成后，再按需要拆分后续任务：

1. `M11D.4 Combat Physics Collider Shape Contract`：把 collider 从 AABB-only 过渡到 shape-backed collider，先定义数据和 copy / debug / lifecycle 边界。
2. `M11D.5 OBB Collider Narrowphase`：补 Ray / Sphere / Capsule / AABB / Sector / OBB 对 OBB collider 的必要相交矩阵。
3. `M11D.6 Motion Against Oriented Obstacles`：让 `CombatKinematicMotor` 支持 capsule proxy 对 OBB 障碍物 sweep / slide。
4. Authoring / Debug 可视化：在 runtime 算法稳定后，再补 Unity 场景可视化和编辑器入口。

## 测试要求

子代理完成后不要提交 SVN，由主代理复核和提交。子代理至少自查：

- Unity MCP refresh / Console error 检查。
- `MxFramework.Tests.Combat.Physics.*`。
- `MxFramework.Tests.Combat.Animation.WeaponTraceQueryBuilderTests`。
- `MxFramework.Tests.Combat.Hit.*`。
- 如测试集合稳定，可跑 `MxFramework.Tests.Combat` 全组。

实现中必须保留或新增一个可对照 oracle：axis-aligned OBB 的结果应与等价 AABB query 一致，确保 OBB 接入不改变既有 AABB 语义。

## 完成记录

- 2026-05-20：完成 OBB query v0。
  - 新增 `CombatObbQuery`，接入 `CombatPhysicsQuery.From(CombatObbQuery)` 和 `ToObbQuery()`。
  - `CombatPhysicsShape.Obb` / `CombatObbQuery` 对 axis 做 fixed-point 归一化；zero axis 和负 half extents 继续抛结构化异常。
  - 新增内部 `CombatPhysicsObbMath`，提供 OBB conservative AABB 和 OBB query vs AABB collider SAT overlap。
  - `CombatPhysicsWorld.Query` / `QueryBatch` / `ExplainQuery` 支持 OBB，不再返回 unsupported。
  - OBB 命中结果的 `Distance` 固定为 `0`；`Point` 使用 query center 到目标 AABB 的 closest point，`Normal` 使用该向量的归一化结果，center 已在 AABB 内时为 `FixVector3.Zero`。
  - broadphase 继续复用 M11D.2 fixed grid candidate collection、去重和稳定排序。
  - 补充 Combat Physics 测试覆盖 OBB DTO roundtrip、axis normalization、axis-aligned AABB oracle、旋转命中、边界接触、包含、分离、零厚度 thin box、稳定排序、batch 混合查询和 debug report。

验证记录：

- `dotnet build MxFramework.Tests.csproj -v minimal`：通过，0 error，既有 warning。
- Unity MCP refresh / Console error check：0 error。
- Unity MCP EditMode：`MxFramework.Tests.Combat.Physics.CombatQueryContractTests` / `CombatPhysicsBroadphaseTests` / `CombatPhysicsWorldTests` 共 40 / 40 passed。
- Unity MCP EditMode：`MxFramework.Tests.Combat.Animation.WeaponTraceQueryBuilderTests`、`MxFramework.Tests.Combat.Hit.HitResolveSystemTests`、`MxFramework.Tests.Combat.GameplayBridge.CombatHitApplicationSystemTests` 共 29 / 29 passed。
- Unity MCP EditMode：尝试运行 `MxFramework.Tests.Combat` 全范围；MCP job 在 Authoring 预览测试 `CombatAuthoringPreviewExplainerTests.Explain_DisplayTextIncludesPhysicsQueryDebugRows` 处报告 `editor_unfocused` stuck，已完成 67 / 254 且当时 0 failure。该卡顿未显示 Combat Physics / OBB 失败。

实现边界：

- 未新增 `CombatPhysicsObbCollider`、`UpsertObbCollider`、rotation-aware body、OBB collider interaction matrix 或 Character Motion OBB obstacle 支持。
- 未修改 Gameplay / Buff / Ability、Unity Scene、Prefab、ScriptableObject、Authoring UI 或 Gizmo。
- 未更新 `Docs/COMBAT_ANIMATION_PHYSICS.md`，因为当前改动未改变该文档的长期边界，只收口本任务文档。

## 实现开工前确认

- M11D.1 unsupported OBB 测试要先改成新预期，不能删除 OBB 覆盖。
- M11D.2 broadphase 测试仍可通过，并新增 OBB query AABB 覆盖。
- 当前工作树没有需要合并的他人 Combat Physics 改动。
- 如果要把 OBB 接入 WeaponTrace，必须先确认现有 `WeaponTraceQueryBuilder` 是否已有 shape authoring 输入；否则本阶段只做 Combat Physics runtime，不混入 Animation / Authoring 改动。

## 提交边界

本任务实现阶段只允许修改 Combat Physics / Combat Tests / 本任务文档，必要时最小同步 `Docs/COMBAT_ANIMATION_PHYSICS.md` 或任务索引。不要混入：

- Gameplay / Buff / Ability 模块改动。
- Character Motion 对 OBB 障碍物支持。
- Authoring UI / Showcase 表现改动。
- Unity 场景、Prefab、ScriptableObject 资产。
- 本地工具缓存、个人插件状态、临时脚本或 Unity 生成噪音。
