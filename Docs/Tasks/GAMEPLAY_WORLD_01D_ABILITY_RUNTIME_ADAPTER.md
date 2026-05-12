# Gameplay World 01D：Ability Runtime Adapter

> 状态：Completed / Verified 2026-05-10
> 日期：2026-05-10
> 优先级：P0
> 父任务：`GAMEPLAY_WORLD_01_V0_FOUNDATION.md`

## 目标

把现有 `IAbility.Cast(AbilityContext)` 包装成世界级 request/service 入口。调用方可以通过 caster id、ability id 和候选目标来源触发释放，而不是在 Demo 中手工拼 `AbilityContext`。

## 建议写入范围

- `Assets/Scripts/MxFramework/Gameplay/AbilityRuntime*.cs`
- `Assets/Scripts/MxFramework/Gameplay/GameplayAbility*.cs`
- `Assets/Scripts/MxFramework/Tests/Ability/GameplayAbilityRuntimeAdapterTests.cs`
- 对应 `.meta`

不要修改 `SimpleAbility.cs`、`IAbility.cs`、`RuntimeEntity.cs` 或 asmdef。

## 建议模型

```text
GameplayAbilityRegistry
  - register ability by ability id
  - reject duplicate id
  - try get ability

GameplayAbilityCastRequest
  - caster entity id
  - ability id
  - explicit candidate ids optional
  - trace id optional

GameplayAbilityRuntimeService
  - resolve caster
  - resolve ability
  - build AbilityContext
  - call IAbility.Cast
  - return structured result
```

01D 可以先通过 `IReadOnlyList<IRuntimeEntity>` 或 01A registry 接入，不要强依赖尚未合入的 01A 类型。如果 01A 已存在，优先复用。

## 规则

- 缺 caster、缺 ability、候选目标为空都返回结构化失败，不抛难以诊断的空引用。
- 不实现 cooldown、cost、cast time、interrupt。
- 不重写 `SimpleAbility` 事件顺序。
- Registry 枚举和错误输出必须稳定。

## 测试

至少覆盖：

- 注册 / 查询 ability。
- 重复 ability id 被拒绝。
- 成功 cast 返回原始 `AbilityCastResult` 语义。
- 缺 caster 返回明确失败。
- 缺 ability 返回明确失败。
- explicit candidate ids 能限制候选目标。

## 验收

- Demo 未来可以把手工 `AbilityContext` 构造迁移到 runtime service。
- 不破坏现有 Ability slice tests。
- 不引入 Unity 或 WGame 依赖。

## 2026-05-10 实现记录

- 新增 `GameplayAbilityRegistry`，按 ability id 注册和查询，并拒绝 null / duplicate ability。
- 新增 `GameplayAbilityCastRequest`、`GameplayAbilityRuntimeFailureCode`、`GameplayAbilityRuntimeResult`。
- 新增 `GameplayAbilityRuntimeService`，从 caster id / ability id / optional candidate ids 构建 `AbilityContext` 并调用现有 `IAbility.Cast`。
- 缺 caster、缺 ability、空候选目标均返回结构化失败。
- 新增 `GameplayAbilityRuntimeAdapterTests` 覆盖注册、重复 id、成功 cast、缺 caster、缺 ability 和候选限制。
