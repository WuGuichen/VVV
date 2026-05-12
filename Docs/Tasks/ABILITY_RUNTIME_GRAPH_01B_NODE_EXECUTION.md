# Ability Runtime Graph 01B：Node Execution

> 状态：Completed / Verified 2026-05-10
> 日期：2026-05-10
> 优先级：P0
> 父任务：`ABILITY_RUNTIME_GRAPH_01_V0_FOUNDATION.md`

## 目标

实现 Ability Graph 的最小确定性执行器。调用方给出 graph definition、caster、ability id、world / targeting / effect 输入后，执行器按图顺序处理节点，并返回结构化结果和节点 trace。

## 建议写入范围

- `Assets/Scripts/MxFramework/Gameplay/AbilityGraphRuntime*.cs`
- `Assets/Scripts/MxFramework/Gameplay/AbilityGraphExecution*.cs`
- `Assets/Scripts/MxFramework/Tests/Ability/AbilityRuntimeGraphExecutionTests.cs`
- 对应 `.meta`

不要重写 `GameplayAbilityRuntimeService`。如需要接入它，应新增 adapter 或 overload，保持旧 request 行为兼容。

## 建议模型

```text
AbilityGraphExecutionContext
  - GameplayWorld / registry
  - caster id
  - ability id
  - optional explicit candidate ids
  - target query service
  - effect registry or explicit effect resolver
  - event sink

AbilityGraphRuntimeExecutor
  - validate graph
  - walk node graph with deterministic step budget
  - dispatch node handlers
  - return execution result

AbilityGraphExecutionResult
  - succeeded / failure code
  - selected targets
  - emitted events
  - node trace
```

## 规则

- 缺 caster、缺 target、缺 effect、validation failure 都返回结构化失败，不抛空引用。
- 节点执行顺序必须可复现；多条边按稳定 port / insertion index 处理。
- 执行器必须有 step budget，防止未验证或损坏图导致死循环。
- `ApplyEffect` 优先复用现有 `IAbilityEffect.Apply(AbilityContext)` 语义。
- 不实现 cooldown、cost、cast time、interrupt。

## 测试

至少覆盖：

- synthetic Strike 图成功执行，并产生与现有 `SimpleAbility` 等价的关键效果。
- 目标查询节点能复用 `GameplayTargetingService` 的 team / tag / status 过滤。
- `ApplyEffect` 节点按稳定顺序执行多个 effect。
- 缺 caster、缺 target、缺 effect 返回明确 failure code。
- validation failure 阻止执行。
- step budget 对损坏图返回明确 failure code。

## 验收

- 现有 `GameplayAbilityRuntimeService` 行为不回退。
- 01E 可以读取 execution trace 做 diagnostics / hash。
- 不引入 Unity 或 WGame 依赖。

## 2026-05-10 实现记录

- 新增 `AbilityGraphExecutionContext`、`AbilityGraphExecutionResult`、`AbilityGraphExecutionFailureCode`、`AbilityGraphExecutionTraceEntry`。
- 新增 `AbilityGraphRuntimeExecutor`，支持 `Entry` / `Sequence` / `TargetQuery` / `ApplyEffect` / `EmitEvent` / `PhaseGate`。
- 新增 `IAbilityGraphEffectResolver` / `AbilityGraphRuntimeEffectRegistry`。
- `TargetQuery` 复用 `GameplayTargetingService`，`ApplyEffect` 复用 `IAbilityEffect.Apply(AbilityContext, target)`。
- 失败场景覆盖 missing caster、missing target、missing effect、validation failure、step budget exceeded、phase gate inactive。
