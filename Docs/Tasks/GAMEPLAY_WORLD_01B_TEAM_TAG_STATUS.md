# Gameplay World 01B：Team / Tag / Status

> 状态：Completed / Verified 2026-05-10
> 日期：2026-05-10
> 优先级：P0
> 父任务：`GAMEPLAY_WORLD_01_V0_FOUNDATION.md`

## 目标

补齐玩法世界最小关系数据：Team relation、Tag set、Status set。它们服务目标选择、Ability 判定、AI 查询和诊断，不承载 WGame 具体阵营、元素或状态业务。

## 建议写入范围

- `Assets/Scripts/MxFramework/Gameplay/GameplayTeam*.cs`
- `Assets/Scripts/MxFramework/Gameplay/GameplayTag*.cs`
- `Assets/Scripts/MxFramework/Gameplay/GameplayStatus*.cs`
- `Assets/Scripts/MxFramework/Tests/Ability/GameplayTeamTagStatusTests.cs`
- 对应 `.meta`

不要修改 `RuntimeEntity.cs`、`IRuntimeEntity.cs`、`SimpleAbility.cs` 或 asmdef。

## 建议模型

```csharp
public enum GameplayTeamRelation
{
    SameTeam,
    Enemy,
    Neutral
}

public readonly struct GameplayTagId { ... }
public sealed class GameplayTagSet { ... }
public readonly struct GameplayStatusId { ... }
public sealed class GameplayStatusSet { ... }
```

可以按现有风格调整命名，但必须保持：

- Tag / Status 使用稳定 int id，不使用字符串作为运行时权威 ID。
- 集合枚举顺序稳定。
- `0` 默认非法或 None，规则要写清。

## 规则

- 不定义 WGame 具体 tag/status 常量。
- 不把 status 和 Buff 强绑定；Buff 可以影响 status，但本任务不做绑定。
- 不把 TeamId 解释成具体阵营文案。
- Tag / Status set 的 Add / Remove / Contains 行为必须幂等。

## 测试

至少覆盖：

- team relation：same / enemy / neutral。
- tag add/remove/contains 幂等。
- status add/remove/contains 幂等。
- 枚举顺序稳定。
- 非法 id 被拒绝或明确忽略，行为要有测试。

## 验收

- Team / Tag / Status 都能纯 C# 测试。
- 后续 targeting service 可以直接消费这些结构。
- 不引入 Unity 或 WGame 依赖。

## 2026-05-10 实现记录

- 新增 `GameplayTeamRelation` 和 `GameplayTeamRelations`：正数同队为 `SameTeam`，正数不同为 `Enemy`，任一非正数为 `Neutral`。
- 新增 `GameplayTagId` / `GameplayTagSet` 和 `GameplayStatusId` / `GameplayStatusSet`。
- `0/default` 为 None，set 操作忽略并返回 `false`；负数 ID 构造抛 `ArgumentOutOfRangeException`。
- Set 枚举按 id 升序稳定，`Add` / `Remove` / `Contains` 幂等。
- 新增 `GameplayTeamTagStatusTests` 覆盖上述语义。
