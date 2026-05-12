# Ability Runtime Graph 01：v0 Foundation

> 状态：Completed / Verified 2026-05-10
> 日期：2026-05-10
> 优先级：P0
> 前置：`GAMEPLAY_WORLD_01_V0_FOUNDATION.md`、`RUNTIME_FOUNDATION_04_V1_PARALLEL_CLOSEOUT.md`

## 目标

把当前 `SimpleAbility` / `GameplayAbilityRuntimeService` 推进到数据驱动的 Ability Runtime Graph v0。v0 不做编辑器和完整战斗系统，只冻结运行时图模型、图执行、阶段时间线、配置映射种子、诊断和 hash，使后续 Ability Editor、WGame JSON 迁移、Combat bridge、Replay 校验都有统一接入口。

## 公共契约冻结

- `MxFramework.Gameplay` 继续保持纯 C#，不得引用 `UnityEngine` 或 `UnityEditor`。
- `MxFramework.Config.Runtime` 可以映射到 Gameplay 图模型，但不得引用 WGame 命名空间或真实项目私有 JSON。
- Runtime 图定义应可稳定枚举、稳定验证、稳定 hash；不得依赖 `object.GetHashCode()`、字典枚举顺序或墙钟时间。
- v0 只支持最小节点族：入口、顺序、目标查询、应用效果、事件输出、阶段门控。
- 不改写现有 `SimpleAbility`、`IAbility`、`IAbilityEffect`、`RuntimeEntity` 的兼容语义。
- 子任务只写自己的文件范围；遇到需要改公共 asmdef 或现有 API 时，先在结果中说明原因和风险。

## 并行任务

| 任务 | 状态 | 负责人范围 | 任务文档 |
|------|------|------------|----------|
| 01A Graph Contract | Completed | 图定义 / 节点 / 边 / 验证 | `ABILITY_RUNTIME_GRAPH_01A_GRAPH_CONTRACT.md` |
| 01B Node Execution | Completed | 图执行器 / 执行上下文 / 节点处理器 | `ABILITY_RUNTIME_GRAPH_01B_NODE_EXECUTION.md` |
| 01C Phase Timeline | Completed | 阶段模型 / deterministic timeline / phase gate | `ABILITY_RUNTIME_GRAPH_01C_PHASE_TIMELINE.md` |
| 01D Config Mapping | Completed | Config DTO / mapper / authoring validation seed | `ABILITY_RUNTIME_GRAPH_01D_CONFIG_MAPPING.md` |
| 01E Diagnostics Hash | Completed | graph diagnostics / execution trace / runtime hash | `ABILITY_RUNTIME_GRAPH_01E_DIAGNOSTICS_HASH.md` |

## 集成顺序

1. 01A 先冻结图模型和验证错误格式。
2. 01B 基于 01A 和现有 GameplayWorld / Targeting / AbilityEffect 执行最小图。
3. 01C 与 01B 并行推进，但最终由 01B 接入 phase gate 或 timeline state。
4. 01D 基于 01A 做 synthetic config mapper，不等待真实 JSON。
5. 01E 可先围绕 01A 做定义 hash，再在 01B 合入后补 execution trace hash。

## 非目标

- 不做可视化节点编辑器、Unity Inspector 节点面板、GraphView 或 UI Toolkit authoring。
- 不迁移 WGame 真实 Ability JSON，不绑定 WGame 字段名。
- 不做 projectile、碰撞体查询、导航、动画事件、Timeline asset、Addressables 资源绑定。
- 不做 cooldown、cost、cast time、interrupt、公式 DSL、条件 DSL 或完整 Gameplay Ability System。
- 不做网络同步、rollback、服务器权威模拟。

## 验收

- 可以构建一个 synthetic Strike 图：入口 -> 目标查询 -> 应用伤害效果 -> 输出事件，并通过 runtime service 执行。
- 验证器能稳定报告 duplicate node id、missing entry、unresolved edge、cycle、invalid node payload。
- 图执行对同一 world / frame / input 产生稳定节点顺序、稳定结果和稳定 trace。
- phase timeline 不依赖 Unity time 或 coroutine，同样输入 tick 序列得到同样结果。
- config mapper 可以从 synthetic config DTO 构建图并返回带路径的验证错误。
- graph hash 对同一定义稳定，节点 / 边 / payload 变化时 hash 变化。
- `dotnet build MxFramework.Gameplay.csproj --no-restore` 通过。
- `dotnet build MxFramework.Tests.csproj --no-restore` 通过。

## 分发规则

子代理开始前必须读取本文件和对应子任务文档。所有子代理都不是独自在代码库中工作，不能回退或覆盖其他人的改动；如果遇到未提交改动，应保留并围绕它们实现。每个子代理最终必须列出改动文件、验证命令、未完成风险。

## 2026-05-10 实现记录

- 01A 已完成 `AbilityGraphDefinition`、node / edge / payload contract、`AbilityGraphValidator` 和结构化 validation result。
- 01B 已完成 `AbilityGraphRuntimeExecutor`、execution context/result、effect registry、event sink、phase gate interface 和 execution trace adapter。
- 01C 已完成 `AbilityGraphTimelineDefinition`、phase definition/state/scheduler、timeline validation、phase gate helper 和 executor phase gate adapter。
- 01D 已完成 synthetic `AbilityGraphConfig` / mapper / diagnostics，并把 runtime validation error 回映射到 config path。
- 01E 已完成 `AbilityGraphDiagnosticSnapshot`、`AbilityGraphExecutionTrace` 和 `AbilityGraphHashContributor`。
- 父级验证：`dotnet build MxFramework.Gameplay.csproj --no-restore` 通过，0 warning / 0 error。
- 父级验证：`dotnet build MxFramework.Config.Runtime.csproj --no-restore` 通过，0 warning / 0 error。
- 父级验证：`dotnet build MxFramework.Tests.csproj --no-restore` 通过，13 个既有 Demo 未赋值 warning / 0 error。
- Unity EditMode：`AbilityRuntimeGraphContractTests`、`AbilityRuntimeGraphExecutionTests`、`AbilityRuntimeGraphPhaseTimelineTests`、`AbilityRuntimeGraphDiagnosticsHashTests`、`AbilityGraphConfigMappingTests` 共 36/36 passed。
