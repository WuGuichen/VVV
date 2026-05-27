# Combat Physics M11D.1：Shape Contract and Unified Query API

> **状态**：已完成（2026-05-09）
> **优先级**：P0
> 前置任务：`COMBAT_ANIMATION_PHYSICS_EPIC.md` M9A / M9B / M9D、`COMBAT_AUTHORING_M10H_1_SHOWCASE_LAUNCH.md`
> 设计依据：`Docs/COMBAT_ANIMATION_PHYSICS.md`、`Docs/Tasks/COMBAT_ANIMATION_PHYSICS_EPIC.md`
> 派发对象：Combat Physics 子代理

## 背景

当前 Combat 物理已经有 `CombatPhysicsWorld`、body / AABB collider 注册，以及 Ray / AABB / Sphere / Capsule / Sector 查询 v0。Showcase 和 Authoring 已经能通过 Capsule trace、hit resolve、HUD 和 Play Mode 手测验证最小闭环。

下一步如果直接做 broadphase，容易把已有 query 方法和 collider 数据结构改乱。因此本阶段先补“物理基础框架”的契约层：

- 统一 runtime shape descriptor。
- 统一 query request / batch request。
- 明确 collider shape、query shape、layer/mask/filter 和 debug report 的边界。
- 保持现有 query 行为稳定，为后续 `M11D.2 Broadphase v0` 提供输入结构。

## 目标

把 Combat Physics 从“多个独立 query 方法”推进到“可扩展的物理查询契约”。

完成后应能做到：

1. Runtime 用统一 shape 数据表达 AABB / Sphere / Capsule / Sector，预留 OBB。
2. Query 可以通过统一入口分发到现有 Ray / AABB / Sphere / Capsule / Sector 实现。
3. Batch query 有稳定输入、稳定输出和稳定排序。
4. Collider / query 的 layer、mask、source entity、owner/team/filter 语义清晰。
5. Query debug report 能说明：
   - 生成了什么 query。
   - 参与候选的 collider 数量。
   - 被过滤的原因。
   - 最终命中结果数量。
6. 现有 Showcase / WeaponTrace / HitResolve 不发生行为回归。

## 范围

本阶段实现 contract v0，优先稳定 API，不做性能优化：

- 新增或整理 shape descriptor：
  - `CombatPhysicsShapeKind`
  - `CombatPhysicsShape`
  - AABB / Sphere / Capsule / Sector 字段。
  - OBB 可只定义 kind 和字段，查询实现可暂缓并返回明确 unsupported。
- 新增 unified query request：
  - `CombatPhysicsQuery`
  - 复用或包含现有 `CombatQueryHeader`。
  - 支持从现有 `CombatRayQuery`、`CombatSphereQuery`、`CombatCapsuleQuery`、`CombatAabbQuery`、`CombatSectorQuery` 转换。
- 新增 batch query contract：
  - `CombatPhysicsQueryBatch`
  - `CombatPhysicsQueryBatchResult`
  - 每个 query 的结果按现有 `CombatQueryResult.CompareTo` 稳定排序。
  - 多 query 输出按 query header / sourceOrder 稳定排序。
- 新增 filter contract v0：
  - 保留现有 `includeSourceEntity` 语义。
  - 定义 owner / team filter 的字段或扩展点，但不强行接入 Gameplay 阵营系统。
  - 不改变现有 layer mask 行为。
- 新增 debug / diagnostics DTO：
  - `CombatPhysicsQueryDebugReport`
  - `CombatPhysicsQueryDebugRow`
  - 记录 candidateCount、filteredSourceCount、filteredLayerCount、hitCount、unsupportedReason。
- 给 `CombatPhysicsWorld` 增加统一入口：
  - `Query(in CombatPhysicsQuery query, List<CombatQueryResult> results, ...)`
  - `QueryBatch(..., List<CombatPhysicsQueryBatchResult> results, ...)`
  - `ExplainQuery(...)` 或返回 debug report 的等价方法。

## 技术约束

- 不引用 `UnityEngine`。
- 不引用 `UnityEditor`。
- 不改变现有 public query 方法的行为；旧 API 继续可用。
- 不把 Gameplay / Buff / Ability 依赖引入 `MxFramework.Combat`。
- 不做 spatial hash / grid broadphase；这里只允许为下一阶段预留字段。
- 不引入浮点作为权威结果。
- 所有排序必须显式且可测试，不能依赖 `Dictionary` / Unity / runtime 枚举顺序。

## 建议实现文件

可按现有代码风格调整，但建议范围控制在：

```text
Assets/Scripts/MxFramework/Combat/Physics/CombatPhysicsShape.cs
Assets/Scripts/MxFramework/Combat/Physics/CombatPhysicsQuery.cs
Assets/Scripts/MxFramework/Combat/Physics/CombatPhysicsQueryBatch.cs
Assets/Scripts/MxFramework/Combat/Physics/CombatPhysicsQueryDebugReport.cs
Assets/Scripts/MxFramework/Combat/Physics/CombatPhysicsWorld.cs
Assets/Scripts/MxFramework/Tests/Combat/Physics/...
Docs/Tasks/COMBAT_PHYSICS_M11D_1_SHAPE_QUERY_CONTRACT.md
```

## 非目标

- 不做 broadphase。
- 不做大规模性能优化。
- 不做 OBB 实际相交算法。
- 不做 Unity Collider 适配。
- 不做 AnimationClip / bone / socket bake。
- 不改 Authoring UI，除非测试需要最小编译适配。

## 验收标准

- 现有 Ray / AABB / Sphere / Capsule / Sector 单 query 测试全部继续通过。
- 新统一 query 入口对同一 world、同一 query，输出与旧 API 一致。
- Batch query 对乱序输入输出稳定：
  - 同一批 query 多次执行结果一致。
  - 不同插入顺序的 collider/world 输出一致。
- Unsupported OBB query 不静默失败，返回明确 report 或状态。
- Debug report 能说明至少：
  - query kind。
  - candidate count。
  - layer/source filter count。
  - hit count。
- Showcase 的 WeaponTrace / Resolve 流程仍通过现有 Combat EditMode tests。
- Unity Console 无 error。

## 测试要求

子代理完成后不要提交 SVN，由主代理复核和提交。子代理至少自查：

- Unity MCP refresh / Console error 检查。
- `MxFramework.Tests.Combat.Physics.*`。
- `MxFramework.Tests.Combat.Animation.WeaponTraceQueryBuilderTests`。
- `MxFramework.Tests.Combat.Hit.*`。
- 如测试集合稳定，可跑 `MxFramework.Tests.Combat` 全组。

## 完成记录

- 2026-05-09：完成 contract v0。
  - 新增统一 shape descriptor：`CombatPhysicsShapeKind` / `CombatPhysicsShape`，覆盖 Ray / AABB / Sphere / Capsule / Sector，预留 OBB 字段。
  - 新增统一 query / filter / batch / debug DTO：`CombatPhysicsQuery`、`CombatPhysicsQueryFilter`、`CombatPhysicsQueryBatch`、`CombatPhysicsQueryBatchResult`、`CombatPhysicsQueryDebugReport`、`CombatPhysicsQueryDebugRow`。
  - `CombatPhysicsWorld` 新增 `Query`、`QueryBatch`、`ExplainQuery`，统一入口分发到现有 Ray / AABB / Sphere / Capsule / Sector 实现；OBB query 明确返回 unsupported report，直接 `Query` 会抛出 `NotSupportedException`。
  - Batch 输出按 query header 的 `TraceId / ActionId / SourceOrder / QueryId / Kind / SourceEntityId` 显式排序，每个 query 内部 hit 继续使用 `CombatQueryResult.CompareTo`。
  - Debug report 记录 query kind、candidate count、source/layer filter count、hit count、unsupported reason 和逐 collider row。
  - 补充 Combat Physics 测试覆盖统一入口一致性、filter include source、batch 稳定排序、debug report、unsupported OBB、shape/query/filter contract。
  - 公共 API 文档：当前没有 `Docs/Interfaces/Combat/` 入口；`Docs/CAPABILITIES.md` 已存在无关本任务的本地改动，本任务未混入修改。

验证记录：

- 临时 SDK 编译检查：`dotnet build Temp/VerifyCombatPhysicsTests.csproj`，覆盖 Combat runtime 和 `MxFramework.Tests.Combat.Physics` 两个测试文件，0 warning / 0 error。临时 project 已删除。
- Unity MCP：当前无可用 Unity session。
- Unity batchmode EditMode：未能执行，Unity 报告该 project 已被另一个 Unity 实例打开，拒绝 batchmode 并退出。
- 根目录生成的 `MxFramework.Combat.csproj` / `MxFramework.Tests.csproj` 未作为最终验证依据；它们是 Unity 生成文件，当前没有包含新增脚本，且本任务不修改生成 csproj。

## 提交边界

本任务只允许修改 Combat Physics / Combat Tests / 本任务文档。不要提交或改动以下未跟踪本地文件：

- `Assets/CombatActionAuthoringAsset.asset`
- `Assets/CombatSceneBindingAsset.asset`
- `.claude` / `Tools/*.py`
