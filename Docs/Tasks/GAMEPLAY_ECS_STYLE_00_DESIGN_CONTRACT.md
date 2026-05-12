# Gameplay ECS-style 00：Design Contract

> 状态：Planned / Contract（2026-05-11）

## 定位

本路线没有旧数据兼容目标。`RuntimeEntity` / `GameplayWorld` 是已存在的 v0 API，不是新架构的长期 source of truth；它们后续通过 bridge 逐步过渡到 component runtime。

当前优先把 `MxFramework.Gameplay` 收束为：

```text
Command-driven Gameplay ECS-style Runtime
= Component Store + System Pipeline + Command-driven Flow + EventQueue Output
```

重点是组件化状态、系统化逻辑、明确 command 输入、稳定 runtime 调度、按帧 event 输出，以及 Replay / SaveState / Hash / Diagnostics 可接入。底层存储和查询形态可以随着真实 Gameplay 需求演进到 SparseSet / Archetype / Chunk，但第一版先把契约、边界和测试锁住。

## 核心定义

| 概念 | 定义 |
| --- | --- |
| Entity | 只表达身份和生命周期，不承载业务逻辑。新架构直接使用 `GameplayEntityId` generation id，防止 stale reference 命中新实体。 |
| Component | 纯 gameplay 状态容器，不引用 `UnityEngine`、`UnityEditor`、Combat、UI、Demo 或 WGame 私有数据。 |
| System | 处理明确 command 或组件状态的逻辑单元。System 不直接 drain `RuntimeCommandBuffer`。 |
| World | 组合根，持有 entity lifecycle、component stores、system pipeline、event queue、diagnostics/hash/save 入口。 |
| Command | 权威输入。Input / AI / Timer / SceneFlow / Combat bridge 只能 enqueue command，不直接改 Gameplay 状态。 |
| Event | 结果输出。UI / Audio / Diagnostics / Replay 通过 `RuntimeEventQueue<GameplayRuntimeEvent>` 或 snapshot 观察。 |

## 硬规则

1. 当前无旧数据兼容目标。
2. `RuntimeEntity` 是 v0 API，不是新架构 source of truth。
3. 新 `GameplayEntityId` 直接使用 generation id，不支持裸 int entity id 作为新 store key。
4. 同一类状态只能有一个 source of truth。
5. `RuntimeCommandBuffer` 是单 drain owner 资源。`GameplayRuntimeModule` 持有的 buffer 只能由该 module drain。
6. `GameplayRuntimeModule` 长期只保留调度职责：drain command、构造 system context、运行 pipeline、暴露 event queue。
7. 业务逻辑逐步迁移到 system：Ability command、entity lifecycle、effect、buff tick、cooldown、death、cleanup。
8. Component 不引用 `UnityEngine`、`UnityEditor`、Demo、UI、Combat 实现层或 WGame 私有数据。
9. System 不直接 drain `RuntimeCommandBuffer`。
10. EventQueue 不由 Gameplay 内部强制 flush，外部观察者按 frame drain。
11. Entity destroy 必须由 World / ComponentRegistry / EntityLifecycleSystem 统一清理 registered component stores；单独的 `GameplayEntityLifecycle` 不负责 component cleanup。
12. Component value 参与 diagnostics / hash / SaveState 前必须通过显式 schema 注册；禁止把泛型 store 或反射字段顺序直接当作权威序列化格式。
13. 新增 component / system / command handler 必须补测试和接口/任务文档。

## Source of Truth

没有旧数据兼容目标，但 v0 API bridge 过程中最容易出问题的是双状态。例如：

```text
RuntimeEntity.TeamId = 1
TeamComponent.TeamId = 2
```

或：

```text
AttributeStore HP = 500
HealthComponent HP = 600
```

禁止双写同一状态。过渡采用三段式：

```text
Stage A: 新 component store 承载新状态；RuntimeEntity / GameplayWorld 测试通过 bridge 继续运行。
Stage B: 某类状态迁入 component store 后，RuntimeEntity 对该状态只做 facade，不再持有第二份权威值。
Stage C: 迁移完成后移除旧字段或标记 obsolete，并由测试锁定 source of truth。
```

第一批组件不做过碎的 `HealthComponent`、`AttackComponent`、`DefenseComponent`。这些继续通过 `AttributeComponent` 包装 `AttributeStore` 表达。

## 初始组件建议

```text
IdentityComponent
TeamComponent
AttributeComponent
BuffComponent
ModifierComponent
AbilityComponent
TagComponent
StatusComponent
LifecycleComponent
```

## System Phase

第一版 system phase 贴近 Runtime 主线，不发明复杂调度：

```text
PreCommand
Command
Simulation
Resolution
Diagnostics
```

推荐流程：

```text
Input / AI / Timer enqueue command
-> GameplayRuntimeModule drain command
-> Command Systems
   - AbilityCommandSystem
   - EntityLifecycleCommandSystem
   - InteractionCommandSystem
-> Simulation Systems
   - EffectSystem
   - BuffTickSystem
   - CooldownSystem
   - StatusSystem
-> Resolution Systems
   - DeathSystem
   - CleanupSystem
-> Systems enqueue GameplayRuntimeEvent
-> UI / Audio / Diagnostics drain events by frame
-> Hash / Snapshot / SaveState read stable world state
```

EventQueue 不由 Gameplay 内部强制 flush。外部观察者按 frame drain，避免错过事件。

## 后续 Batch

```text
GAMEPLAY_ECS_STYLE_01_COMPONENT_STORE
  - GameplayEntityId / generation
  - GameplayEntityLifecycle
  - IGameplayComponent
  - GameplayComponentStore<T>
  - stable query / snapshot
  - 不支持裸 int entity id 作为新 store key
  - no UnityEngine
  - tests

GAMEPLAY_ECS_STYLE_02_SYSTEM_PIPELINE
  - IGameplaySystem
  - GameplaySystemPhase
  - GameplaySystemContext
  - GameplaySystemPipeline
  - RuntimeCommandBuffer 仍由 GameplayRuntimeModule 单点 drain
  - stable order / enable-disable / exception policy
  - tests

GAMEPLAY_ECS_STYLE_03_V0_API_BRIDGE
  - 让现有 RuntimeEntity / GameplayWorld 测试逐步过渡
  - 不是旧数据兼容
  - 避免双状态
  - bridge / facade tests

GAMEPLAY_ABILITY_03_COMMAND_SYSTEM
  - AbilityCommandSystem
  - EntityLifecycleSystem
  - GameplayRuntimeModule 只做 drain + pipeline runner
  - RuntimeEventQueue 输出结果
  - tests

GAMEPLAY_ECS_STYLE_09_COMPONENT_SCHEMA_CONTRACT
  - 定义 component value schema 注册契约
  - 定义 diagnostics / hash / SaveState adapter 职责
  - 明确 StableId / version / payload 边界
  - 禁止泛型 store 直接反射序列化
```

## Agent 约束

Agent 新增玩法时优先选择以下工作单元：

- 新增 component。
- 新增 system。
- 新增 command factory / handler。
- 新增 effect。
- 新增 config mapper。
- 新增 test / hash / save 验证。

Agent 不应在 Demo、MonoBehaviour、RuntimeEntity 或 `GameplayRuntimeModule` 中直接塞特例业务逻辑。

## 验收标准

- 设计文档和 `Docs/Interfaces/Gameplay.md` 对 ECS-style 定义一致。
- 后续代码任务必须引用本契约并说明是否影响 source of truth。
- 后续新增 system 不直接调用 `RuntimeCommandBuffer.DrainForFrame`。
- 后续新增 component store snapshot 按 entity id / generation 稳定排序。
- 后续新增 component store 不接受裸 int entity id 作为 key。
