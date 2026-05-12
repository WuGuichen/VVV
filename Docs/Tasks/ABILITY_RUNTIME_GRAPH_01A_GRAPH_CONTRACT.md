# Ability Runtime Graph 01A：Graph Contract

> 状态：Completed / Verified 2026-05-10
> 日期：2026-05-10
> 优先级：P0
> 父任务：`ABILITY_RUNTIME_GRAPH_01_V0_FOUNDATION.md`

## 目标

定义 Ability Runtime Graph v0 的纯运行时契约：图、节点、边、端口、节点种类、验证结果。该任务只冻结模型和验证，不实现节点执行。

## 建议写入范围

- `Assets/Scripts/MxFramework/Gameplay/AbilityGraph*.cs`
- `Assets/Scripts/MxFramework/Tests/Ability/AbilityRuntimeGraphContractTests.cs`
- 对应 `.meta`

不要修改 `SimpleAbility.cs`、`IAbility.cs`、`IAbilityEffect.cs`、`RuntimeEntity.cs`。如确实需要 asmdef 变化，先在结果中说明。

## 建议模型

```text
AbilityGraphDefinition
  - graph id / version
  - entry node id
  - nodes in stable order
  - edges in stable order

AbilityGraphNode
  - node id
  - kind
  - optional payload contract for v0 node kinds

AbilityGraphEdge
  - from node id
  - output port
  - to node id

AbilityGraphValidationResult
  - is valid
  - stable ordered errors
```

v0 node kind 建议包含：`Entry`、`Sequence`、`TargetQuery`、`ApplyEffect`、`EmitEvent`、`PhaseGate`。如果实现时发现 `Sequence` 可由边表达，也要保留清晰的 node kind 语义，方便后续编辑器和诊断。

## 规则

- 所有集合枚举顺序必须稳定。
- 图定义在构建后应表现为只读；不得让外部调用方绕过验证直接改内部集合。
- 验证错误使用结构化 code + message + node id / edge index，不只返回字符串。
- 预期 authoring 错误不抛异常，返回 validation result。
- 禁止使用 ad-hoc JSON 字符串作为 runtime payload 的唯一表达。

## 测试

至少覆盖：

- 合法最小图验证通过。
- duplicate node id 被稳定报告。
- missing entry node 被稳定报告。
- unresolved edge endpoint 被稳定报告。
- cycle 被稳定报告，错误顺序稳定。
- invalid node payload 被稳定报告。

## 验收

- 01B / 01C / 01D / 01E 可以只依赖本任务的公开模型推进。
- 不引入 Unity 或 WGame 依赖。
- 不破坏现有 Ability / GameplayWorld 测试。

## 2026-05-10 实现记录

- 新增 `AbilityGraphDefinition`、`AbilityGraphNode`、`AbilityGraphEdge`、`AbilityGraphNodeKind`、`AbilityGraphPorts`。
- 新增 v0 payload：`AbilityGraphTargetQueryPayload`、`AbilityGraphApplyEffectPayload`、`AbilityGraphEmitEventPayload`、`AbilityGraphPhaseGatePayload`。
- 新增 `AbilityGraphValidationResult` / `AbilityGraphValidationError` / `AbilityGraphValidationErrorCode` 和 `AbilityGraphValidator`。
- 覆盖合法最小图、duplicate node id、missing entry、unresolved edge、cycle、invalid node payload。
