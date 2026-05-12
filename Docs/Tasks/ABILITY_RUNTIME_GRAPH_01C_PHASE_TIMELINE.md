# Ability Runtime Graph 01C：Phase Timeline

> 状态：Completed / Verified 2026-05-10
> 日期：2026-05-10
> 优先级：P0
> 父任务：`ABILITY_RUNTIME_GRAPH_01_V0_FOUNDATION.md`

## 目标

为 Ability Graph v0 增加纯运行时阶段时间线。它负责描述 Prepare / Active / Recovery / Complete 这类阶段和 deterministic tick 推进，供图节点通过 phase gate 等待或切换阶段。

## 建议写入范围

- `Assets/Scripts/MxFramework/Gameplay/AbilityGraphPhase*.cs`
- `Assets/Scripts/MxFramework/Gameplay/AbilityGraphTimeline*.cs`
- `Assets/Scripts/MxFramework/Tests/Ability/AbilityRuntimeGraphPhaseTimelineTests.cs`
- 对应 `.meta`

不要引用 Unity coroutine、Animator、Timeline asset 或 `Time.deltaTime`。

## 建议模型

```text
AbilityGraphPhaseId
  - stable id

AbilityGraphPhase
  - phase id
  - duration frames or duration seconds in runtime clock units
  - next phase id optional

AbilityGraphTimeline
  - ordered phases
  - entry phase

AbilityGraphTimelineState
  - current phase
  - elapsed frame / time
  - completion status

AbilityGraphPhaseScheduler
  - advance by explicit runtime step
  - emit stable transition result
```

## 规则

- 时间推进必须由显式输入驱动，不读取墙钟或 Unity 全局时间。
- 同一 timeline + 同一 tick 序列必须产生同一阶段序列。
- duration 为 0、缺 next、循环 phase 等 authoring 问题要有验证或明确语义。
- phase gate 与 01B 集成时，只阻塞 / 释放节点，不隐式执行动画或资源。

## 测试

至少覆盖：

- timeline 按帧稳定推进。
- 0 duration phase 的语义明确且有测试。
- invalid phase reference 被稳定报告。
- phase cycle 在需要禁止时被验证器拦截；如果允许循环，需要 step budget 测试。
- phase gate 在目标 phase 到达前不执行后续节点，到达后释放。

## 验收

- 01B 可以接入 phase gate，而不把执行器变成 coroutine。
- Timeline 状态可被 01E 诊断和 hash。
- 不引入 Unity 或 WGame 依赖。

## 2026-05-10 实现记录

- 新增 `AbilityGraphPhaseId`、`AbilityGraphPhaseDefinition`、`AbilityGraphTimelineDefinition`、`AbilityGraphTimelineState`。
- 新增 `AbilityGraphTimelineScheduler`、advance result、phase transition 和 timeline validation。
- 新增 `AbilityGraphPhaseGate` helper 和 `AbilityGraphTimelinePhaseGate`，可直接接入 01B 的 `IAbilityGraphPhaseGate`。
- 0 duration phase 会在显式 advance 中立即 transition 且不消耗 frame；cycle 由 validation 拦截。
