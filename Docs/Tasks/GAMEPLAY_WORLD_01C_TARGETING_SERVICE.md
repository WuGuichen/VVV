# Gameplay World 01C：Targeting Service

> 状态：Completed / Verified 2026-05-10
> 日期：2026-05-10
> 优先级：P0
> 父任务：`GAMEPLAY_WORLD_01_V0_FOUNDATION.md`

## 目标

建立通用目标查询服务，把当前 `SingleEnemyTargetSelector` 的硬编码规则扩展为可测试、可诊断的 query/filter 模型。01C 不做物理范围查询和 Combat bridge，只做 entity 集合上的逻辑过滤。

## 建议写入范围

- `Assets/Scripts/MxFramework/Gameplay/GameplayTarget*.cs`
- `Assets/Scripts/MxFramework/Gameplay/Targeting*.cs`
- `Assets/Scripts/MxFramework/Tests/Ability/GameplayTargetingServiceTests.cs`
- 对应 `.meta`

不要修改 `RuntimeEntity.cs`、`SimpleAbility.cs`、01A/01B 文件或 asmdef。

## 建议模型

```text
GameplayTargetQuery
  - caster id
  - require alive
  - relation filter
  - required tags
  - blocked statuses
  - max targets

GameplayTargetingService.Select(query, candidates)
  -> GameplayTargetingResult
  -> selected targets + rejected target reasons
```

如果 01B 的 tag/status 类型已存在，可以消费它们；如果并行时尚未存在，先让 query 支持 int id 列表，后续再统一类型。

## 规则

- 不依赖 Unity Physics、CombatPhysicsWorld 或场景坐标。
- 默认枚举输出顺序稳定，按输入顺序或 entity id 排序必须写清并测试。
- 过滤失败要有原因，例如 dead、same team、missing tag、blocked status。
- Query 本身不修改实体状态。

## 测试

至少覆盖：

- alive 过滤。
- enemy / same team 过滤。
- required tag 过滤。
- blocked status 过滤。
- max targets 限制。
- rejected reasons 可读且稳定。

## 验收

- Targeting service 可被 `IAbility` 或后续 Ability runtime adapter 调用。
- 不破坏现有 `SingleEnemyTargetSelector`。
- 不引入 Unity 或 Combat 依赖。

## 2026-05-10 实现记录

- 新增 `GameplayTargetCandidate`、`GameplayTargetQuery`、`GameplayTargetRelationFilter`、`GameplayTargetRejectReason`、`GameplayTargetingResult`、`GameplayTargetingService`。
- 支持 alive、self / same-team / enemy relation、required tags、blocked statuses、max targets。
- Rejected reasons 包含稳定 enum、token、label 和可选 detail id。
- 同时支持 int id 输入和 01B 的 `GameplayTagSet` / `GameplayStatusSet`。
- 新增 `GameplayTargetingServiceTests`，Unity EditMode 9/9 passed。
