# Ability Runtime Graph 01E：Diagnostics Hash

> 状态：Completed / Verified 2026-05-10
> 日期：2026-05-10
> 优先级：P1
> 父任务：`ABILITY_RUNTIME_GRAPH_01_V0_FOUNDATION.md`

## 目标

为 Ability Graph v0 提供诊断快照、执行 trace 和稳定 hash。目标是让 Runtime Preview、Replay、SaveState 检查和测试 golden fixture 能解释“图是什么、跑了哪些节点、为什么失败、hash 为什么变了”。

## 建议写入范围

- `Assets/Scripts/MxFramework/Gameplay/AbilityGraphDiagnostic*.cs`
- `Assets/Scripts/MxFramework/Gameplay/AbilityGraphTrace*.cs`
- `Assets/Scripts/MxFramework/Gameplay/AbilityGraphHash*.cs`
- `Assets/Scripts/MxFramework/Tests/Ability/AbilityRuntimeGraphDiagnosticsHashTests.cs`
- 对应 `.meta`

如需要复用 `IRuntimeHashContributor`，保持 `MxFramework.Gameplay.asmdef` 现有 Runtime 引用，不新增 Unity 依赖。

## 建议模型

```text
AbilityGraphDiagnosticSnapshot
  - graph id / version
  - validation summary
  - node count / edge count
  - optional timeline summary

AbilityGraphExecutionTrace
  - ordered node trace entries
  - target decisions
  - emitted events
  - failure code and message

AbilityGraphHashContributor
  - add graph definition hash
  - optionally add execution state hash
```

## 规则

- hash 输入必须显式排序，不使用 raw object hash 或 dictionary 枚举顺序。
- hash 应包含 graph id、version、node id/kind/payload、edge source/port/target。
- display name / localized text 默认不进入 runtime result hash，除非它会影响执行。
- diagnostics 是只读快照，不暴露可修改 runtime 内部集合。
- trace 文本面向调试，但 failure code 必须结构化。

## 测试

至少覆盖：

- 同一定义多次 hash 一致。
- node payload 变化导致 hash 变化。
- edge 顺序或目标变化导致 hash 变化。
- execution trace 按节点执行顺序稳定输出。
- target rejected / missing effect / validation failure 能进入诊断。
- 不依赖 Unity 或 WGame。

## 验收

- Runtime replay / golden harness 可以消费 graph hash。
- Preview 或调试面板可以消费 diagnostic snapshot，不需要反射 runtime 对象。
- 不破坏现有 `GameplayDiagnosticsHashTests`。

## 2026-05-10 实现记录

- 新增 `AbilityGraphDiagnosticSnapshot` / validation diagnostic summary / snapshot builder。
- 新增 `AbilityGraphExecutionTrace`、node / target / event trace entry 和 adapter-friendly trace builder。
- 新增 `AbilityGraphHashContributor`，可直接作为 `IRuntimeHashContributor` 或通过 `ComputeDefinitionHash` 计算 graph definition hash。
- Hash 覆盖 graph id/version、entry、node kind/payload、edge source/port/target；同定义稳定，payload / edge 改动会变更 hash。
