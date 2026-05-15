# Gameplay 接口

> Phase 11 Runtime Gameplay Foundation 已 Accepted / Closed（2026-05-09）。本页记录当前已验收的 Gameplay Runtime v0 公共接口和边界。

## 职责

Gameplay 提供最小游戏行为运行时核心：实体、技能、目标选择、效果执行和技能事件。它把 Demo 中验证过的 Entity -> Ability -> Target -> Effect -> Attributes -> Buff -> Events 闭环提升为框架 API。

下一阶段 Gameplay 的架构方向是 `Command-driven Gameplay ECS-style Runtime`：使用组件化状态、系统化逻辑、`RuntimeCommandBuffer` 权威输入、`RuntimeHost` 明确调度和 `RuntimeEventQueue` 输出结果。当前无旧数据兼容目标；`RuntimeEntity` / `GameplayWorld` 是 v0 API bridge，不是新架构的长期 source of truth。新 component store 直接使用 generation id，不支持裸 int entity id 作为 key。底层存储和查询形态可以随着真实 Gameplay 需求演进到 SparseSet / Archetype / Chunk。

## 为什么不依赖 Unity

`MxFramework.Gameplay` 是 `noEngineReferences=true` 的纯 C# 程序集，不引用 `UnityEngine` 或 `UnityEditor`。时间、输入、动画、碰撞、GameObject 绑定和场景生命周期都由外层 Unity Demo 或项目层负责传入和编排，因此同一套 Gameplay API 可以被 Unity、EditMode 测试、CLI 工具、预览服务和 Mod 数据验证共同使用。

## 公开接口

| 接口/类型 | 用途 |
|-----------|------|
| `IRuntimeEntity` | 最小运行时实体契约，组合身份、队伍、存活判断、Buff 目标和 Ability 事件 |
| `RuntimeEntity` | 默认纯 C# 组合实现，包含 `AttributeStore`、`BuffPipeline`、`ModifierPipeline` 和事件总线 |
| `GameplayWorld` | Gameplay world v0 组合根，持有实体注册表、稳定 tick 和 world snapshot |
| `RuntimeEntityRegistry` | 按 `EntityId` 注册、查询、移除和稳定排序枚举实体 |
| `GameplayWorldSnapshot` | World tick 与实体列表的只读拷贝 |
| `GameplayTeamRelation` / `GameplayTeamRelations` | SameTeam / Enemy / Neutral 判定工具 |
| `GameplayTagId` / `GameplayTagSet` | 稳定 int tag id 和升序枚举 tag set |
| `GameplayStatusId` / `GameplayStatusSet` | 稳定 int status id 和升序枚举 status set |
| `IAbility` | 技能释放接口 |
| `AbilityContext` | 单次释放上下文，包含 caster 和候选目标 |
| `AbilityCastResult` | 释放成功/失败结果和命中目标 |
| `GameplayAbilityRegistry` | 按 ability id 注册和查询运行时 Ability |
| `GameplayAbilityCastRequest` | 通过 caster id、ability id、candidate ids 和 trace id 表达释放请求 |
| `GameplayAbilityRuntimeService` | 将世界实体 / ability id 解析为 `AbilityContext` 并调用 `IAbility.Cast` |
| `GameplayAbilityRuntimeResult` / `GameplayAbilityRuntimeFailureCode` | Ability runtime adapter 的结构化结果和失败码 |
| `GameplayRuntimeModule` | `RuntimeHost` 模块入口，drain `RuntimeCommandBuffer`、执行 Gameplay command、tick world 并输出 frame event |
| `GameplayRuntimeCommandIds` / `GameplayRuntimeCommandFactory` | Gameplay command id 和 `RuntimeCommand` 构造工具 |
| `GameplayRuntimeEvent` / `GameplayRuntimeEventType` | Gameplay 按帧事件 DTO，用于 UI、Audio、Diagnostics 和 Replay 边界 |
| `GameplayEntityId` | 新 ECS-style component runtime 的 generation entity id |
| `GameplayEntityLifecycle` | 创建/销毁 generation entity id，并防止 stale id 命中新实体 |
| `IGameplayComponent` | 纯 gameplay component marker |
| `IGameplayComponentStore` | Component registry 清理 registered stores 所需的非泛型 store 契约 |
| `GameplayComponentStore<T>` / `GameplayComponentSnapshot<T>` | 只接受 `GameplayEntityId` 的稳定 component store 和 snapshot entry |
| `GameplayComponentRegistry` | 组合 `GameplayEntityLifecycle` 和 registered component stores，统一 destroy cleanup |
| `GameplayComponentPair<TPrimary,TSecondary>` / `GameplayComponentQuery` | 稳定 component query helper，支持单组件拷贝和双组件 join |
| `GameplayComponentWorld` / `GameplayComponentWorldSnapshot` | ECS-style component runtime 组合根，聚合 component registry 和 gameplay runtime event queue |
| `GameplayComponentWorldDiagnostics` / `GameplayComponentWorldDiagnosticSnapshot` | Component runtime 诊断快照，稳定输出 alive entities、store 摘要和 pending event queue 概要 |
| `GameplayComponentSchema` / `GameplayComponentSchemaRegistry` | Component value 的 schema 契约入口，用稳定 id 注册诊断、hash 和 SaveState adapter |
| `GameplayComponentDiagnosticWriter` / `GameplayComponentDiagnosticField` | Component diagnostics capability 的稳定 key/value 输出工具 |
| `GameplayCoreComponentSchemaDescriptors` | Core component diagnostics schema 注册入口 |
| `GameplayComponentSpawnDefinition` / `GameplayComponentSpawnRegistry` | 显式注册的 component entity spawn 定义与稳定 id 查询入口 |
| `IGameplayComponentSpawnInitializer` / `GameplayComponentSpawnInitializer<T>` | Spawn definition 的初始 component 写入器 |
| `GameplayComponentSpawnCommandSystem` / `GameplayComponentSpawnEvents` | 处理 `SpawnComponentEntity` command，创建带初始 components 的 component entity |
| `GameplayAttributeValue` / `GameplayAttributeSetComponent` | Component-native int attribute state，按 attribute id 稳定排序 |
| `GameplayAttributeCommandSystem` / `GameplayAttributeEvents` | 处理 component entity attribute set/add command，并输出 attribute changed event |
| `GameplayAttributeComponentSchemaDescriptors` | Attribute set diagnostics、hash 和 SaveState schema adapters |
| `IGameplayComponentAbility` / `GameplayComponentAbilityRegistry` | Component-native ability 定义和稳定 id registry，不依赖旧 `RuntimeEntity` |
| `GameplayComponentAttributeDeltaAbility` | v0 最小 component ability，对 self target 的 attribute current value 应用 delta |
| `GameplayComponentAbilityCommandSystem` / `GameplayComponentAbilityEvents` | 处理 `CastComponentAbility` command，读写 component world 并输出 ability runtime event |
| `GameplayComponentTargetCandidate` / `GameplayComponentTargetQuery` / `GameplayComponentTargetingService` | Component-native generation-safe targeting snapshot、query 和 filter service |
| `GameplayComponentAbilityRequest` / `GameplayComponentAbilityRequestStore` | `CastComponentAbilityRequest` 使用的 transient request store，保存完整 caster / candidate generation id |
| `GameplayComponentBuffEntry` / `GameplayComponentBuffSetComponent` | Component-native buff state，按 buff id 稳定排序并拒绝重复 buff id |
| `GameplayComponentModifierEntry` / `GameplayComponentModifierSetComponent` | Component-native additive modifier state，按 modifier id 稳定排序并支持按 source buff 清理 |
| `GameplayComponentModifierEvaluator` | 只读计算 attribute current value 加 additive modifier 的结果，不写回 attribute source of truth |
| `GameplayComponentBuffCleanupSystem` / `GameplayComponentBuffEvents` | Resolution phase 清理过期 component buffs，并移除 linked modifiers |
| `GameplayComponentBuffSchemaDescriptors` / `GameplayComponentModifierSchemaDescriptors` | Buff / modifier diagnostics、hash 和 SaveState schema adapters |
| `GameplayIdentityComponent` | ECS-style component runtime 的配置身份数据 |
| `GameplayTeamComponent` | ECS-style team 数据，复用 `GameplayTeamRelations` |
| `GameplayLifecycleComponent` / `GameplayLifecycleState` | ECS-style lifecycle state 数据 |
| `GameplayTagComponent` / `GameplayStatusComponent` | ECS-style tag/status 数据，构造时稳定排序、去重并拷贝输入 |
| `IGameplaySystem` | Gameplay ECS-style system 契约，按 phase / priority 执行 |
| `GameplaySystemPhase` | PreCommand / Command / Simulation / Resolution / Diagnostics |
| `GameplaySystemContext` | System tick 上下文，包含 frame、delta、world、已 drain commands、command handled state 和 event queue |
| `GameplayCommandExecutionState` | Command pipeline 的帧内处理状态，供 command systems 标记 handled，供 unsupported system 判断未处理 command |
| `GameplaySystemPipeline` | 稳定 system 调度管线，不拥有 `RuntimeCommandBuffer` drain 权限 |
| `GameplayAbilityCommandSystem` | 处理 `CastAbility` command，调用 Ability runtime adapter 并输出 runtime event |
| `GameplayEntityLifecycleCommandSystem` | 处理 `DespawnEntity` command |
| `GameplayComponentEntityCommandSystem` | 处理 component runtime 的 `CreateComponentEntity` / `DestroyComponentEntity` command |
| `GameplayLifecycleCleanupSystem` / `GameplayLifecycleEvents` | Resolution phase 清理 `PendingDestroy` component entity，并输出稳定 reason 的 runtime event |
| `GameplayUnsupportedCommandSystem` | 拒绝 default pipeline 中未识别的 Gameplay command id |
| `ITargetSelector` | 从候选目标中选择技能目标 |
| `GameplayTargetCandidate` | 可目标选择的实体快照，包含 entity/team/alive/tag/status |
| `GameplayTargetQuery` | 通用目标查询：caster、alive、team relation、required tags、blocked statuses、max targets |
| `GameplayTargetingService` | 对候选目标执行 query/filter 并返回 selected/rejected |
| `GameplayTargetingResult` / `GameplayTargetRejectReason` | 目标选择结果和稳定拒绝原因 |
| `IAbilityEffect` | 对单个目标执行效果 |
| `AbilityEvent` / `AbilityEventType` | 技能生命周期事件 |
| `SimpleAbility` | 默认实现：选目标、按顺序执行效果、发布事件 |
| `GameplayDiagnosticSnapshot` | 纯 C# 运行时诊断快照，汇总 Entity / Attribute / Buff / Modifier / Ability / Event 状态 |
| `GameplayDiagnosticSnapshotBuilder` | 从公开 runtime 对象和事件日志构建诊断快照 |
| `GameplayWorldDiagnostics` / `GameplayWorldDiagnosticsSummary` | World/entity 诊断摘要入口 |
| `GameplayHashContributor` | 将 Gameplay entity/world 状态贡献给 Runtime hash contract |
| `AbilityGraphDefinition` / `AbilityGraphNode` / `AbilityGraphEdge` | Ability Runtime Graph v0 定义、节点和边 |
| `AbilityGraphValidator` / `AbilityGraphValidationResult` | 图定义结构化校验，覆盖入口、边、payload、cycle |
| `AbilityGraphRuntimeExecutor` | 确定性执行 Ability Graph v0 节点 |
| `AbilityGraphExecutionContext` / `AbilityGraphExecutionResult` | 单次 graph 执行输入、输出、失败码和 trace |
| `AbilityGraphRuntimeEffectRegistry` | 按 effect id 解析 `IAbilityEffect` 的最小运行时 registry |
| `AbilityGraphTimelineDefinition` / `AbilityGraphTimelineScheduler` | 纯 C# phase timeline 定义和显式 frame 推进 |
| `AbilityGraphTimelinePhaseGate` | 将 timeline state 适配到 executor phase gate |
| `AbilityGraphDiagnosticSnapshot` | 图定义、校验和执行 trace 的只读诊断快照 |
| `AbilityGraphExecutionTrace` | 节点、目标决策、事件和失败原因的稳定执行 trace |
| `AbilityGraphHashContributor` | Ability Graph definition 的 Runtime hash contributor |
| `SelfTargetSelector` | 选择 caster 自身 |
| `SingleEnemyTargetSelector` | 选择第一个不同队伍且存活的实体 |
| `DamageEffect` | `max(1, attack - defense)` 扣目标 HP |
| `ApplyBuffEffect` | 通过工厂创建 Buff 并添加到目标 |

## 最小关系

```text
RuntimeEntity
  owns AttributeStore / BuffPipeline / ModifierPipeline / AbilityEvents
  implements IBuffTarget

AbilityContext
  Caster + Candidates

IAbility.Cast(context)
  -> ITargetSelector.SelectTargets(context)
  -> IAbilityEffect.Apply(context, target)
  -> AttributeStore / BuffPipeline
  -> AbilityEvent

GameplayWorld
  owns RuntimeEntityRegistry
  -> stable Tick(deltaTime)
  -> world snapshot / diagnostics / hash contributor

GameplayRuntimeModule
  drains RuntimeCommandBuffer
  -> CastAbility / DespawnEntity command
  -> GameplayWorld + GameplayAbilityRuntimeService
  -> RuntimeEventQueue<GameplayRuntimeEvent>

AbilityGraphRuntimeExecutor
  validates AbilityGraphDefinition
  -> TargetQuery via GameplayTargetingService
  -> ApplyEffect via IAbilityEffect
  -> EmitEvent via AbilityEvent
  -> optional PhaseGate via AbilityGraphTimelinePhaseGate
  -> execution trace / diagnostics / hash
```

## Gameplay World v0

`GameplayWorld` 是纯 C# world root，不读取 Unity 时间，也不持有场景对象。它只管理 `IRuntimeEntity` 的注册关系、稳定 tick 和 world snapshot。

最小能力：

- `RuntimeEntityRegistry.Register(entity)`：拒绝 null、非法 `EntityId <= 0` 和重复 id。
- `TryGet(entityId, out entity)` / `Remove(entityId)`：提供可诊断查询与移除。
- Registry 枚举和 snapshot 按 `EntityId` 升序稳定输出。
- `GameplayWorld.Tick(deltaTime)`：校验 finite non-negative delta，并按 registry snapshot tick 每个实体的 `BuffPipeline`。
- `GameplayWorldSnapshot`：复制 tick count 和实体列表，后续 registry 变化不改变旧 snapshot。

Team / Tag / Status 边界：

- Team 只表达 SameTeam / Enemy / Neutral，不包含项目阵营文案。
- `GameplayTagId` / `GameplayStatusId` 使用稳定 int id；`0/default` 为 None，负数构造非法。
- Tag / Status set 的 add/remove/contains 幂等，枚举按 id 升序稳定。

Targeting service 边界：

- `GameplayTargetingService` 只做实体集合上的逻辑过滤，不做 Unity Physics、Combat range 或 NavMesh。
- `GameplayTargetQuery` 支持 alive、self/same-team/enemy relation、required tags、blocked statuses 和 max targets。
- `GameplayTargetingResult` 保留 selected targets 和 rejected reasons，便于 Debug、AI 和 Authoring Preview 使用。

Ability runtime adapter：

- `GameplayAbilityRegistry` 按 ability id 注册 `IAbility`，拒绝 null / duplicate。
- `GameplayAbilityRuntimeService.Cast(request)` 解析 caster、ability 和 optional candidate ids，再复用现有 `IAbility.Cast(AbilityContext)`。
- 缺 caster、缺 ability、空候选目标都会返回结构化 failure code，不抛难以诊断的空引用。

Runtime module / command loop：

- `GameplayRuntimeModule` 可注册到 `RuntimeHost`，默认 `Simulation` stage、priority `100`，让 timer 等更早 priority 的模块先投递 command。
- Module 每帧调用 `RuntimeCommandBuffer.DrainForFrame(frame)`，按 `RuntimeCommandBuffer` 的稳定排序执行 Gameplay command。
- 一个 `RuntimeCommandBuffer` 应只有一个 drain owner。传给 `GameplayRuntimeModule` 的 command buffer 不应再被其他模块调用 `DrainForFrame`；Input、AI、Timer、SceneFlow 等模块可以 `Enqueue`，但不能消费这个 buffer。
- v0 command 包含 `CastAbility` 和 `DespawnEntity`。`CastAbility` payload 约定为 `payload0=casterEntityId`、`payload1=abilityId`、`payload2=optional single candidateEntityId`。
- `GameplayRuntimeCommandFactory` 提供 command 构造入口，避免 Demo / 项目层手写 command id 和 payload 位序。
- Module 默认在 command 后调用 `GameplayWorld.Tick(deltaTime)`；需要外部手动 tick 时可关闭 `tickWorldAutomatically`。
- Module 将结果写入 `RuntimeEventQueue<GameplayRuntimeEvent>`，事件包含 frame、command、caster、ability、target、failure code、reason 和 traceId。UI / Audio / Diagnostics 应消费事件队列，而不是直接监听内部私有状态。
- `GameplayRuntimeModule.AbilityResults` 只保留最近 N 条 ability cast 诊断结果，默认容量为 `DefaultAbilityResultCapacity`。需要长期日志时应 drain runtime event 或由外部诊断系统接管，不要把该列表当完整历史。

ECS-style 设计契约：

- Entity 只表达身份和生命周期；新架构直接使用 `GameplayEntityId` generation id。
- 新 component store 不支持裸 int entity id 作为 key。
- Component 是纯 gameplay 状态，不引用 Unity、Combat、UI、Demo 或 WGame 私有数据。
- System 处理 command 或组件状态，不直接 drain `RuntimeCommandBuffer`。
- 当前无旧数据兼容目标；`RuntimeEntity` 是 v0 API bridge，不是新架构 source of truth。
- 同一类状态只能有一个 source of truth。bridge 阶段允许 facade，但禁止 `RuntimeEntity` 和 component store 双写同一状态。
- `GameplayRuntimeModule` 后续只保留调度职责：drain command、构造 system context、运行 pipeline、暴露 event queue。
- EventQueue 不由 Gameplay 内部强制 flush，UI / Audio / Diagnostics 等外部观察者按 frame drain。
- 详细设计契约见 `Docs/Tasks/GAMEPLAY_ECS_STYLE_00_DESIGN_CONTRACT.md`。

Component store v0：

- `GameplayEntityId` 是 `Index + Generation` 组成的值类型，`default` 为 invalid。
- `GameplayEntityLifecycle.Create()` 分配 generation id；`Destroy(id)` 推进 generation，旧 id 失效。
- `GameplayEntityLifecycle.CreateSnapshot()` 按 entity index 稳定输出 alive ids。
- `GameplayEntityLifecycle` 只负责 id 生命周期，不负责 component cleanup；后续 World / ComponentRegistry / EntityLifecycleSystem 必须在 destroy entity 时统一清理 registered stores。
- `GameplayComponentStore<T>` 约束 `T : struct, IGameplayComponent`，组件是纯数据。
- Store 只接受 `GameplayEntityId`，没有裸 int key API。
- `GameplayComponentStore<T>.Set` 是 upsert：component 不存在时新增，存在时覆盖。
- Store snapshot / copy 按 `GameplayEntityId` 稳定排序，供 Diagnostics、Hash、SaveState 后续接入。
- `IGameplayComponentStore` 是 registry cleanup 用的非泛型契约，只暴露 component type、count、remove 和 clear。
- `GameplayComponentRegistry` 组合 entity lifecycle 和 registered stores。`DestroyEntity(id)` 只有在 lifecycle 接受该 alive id 时才清理所有 registered stores；stale / invalid id 不清理 store。
- `GameplayComponentRegistry.Clear()` 会同时清空 lifecycle alive state 和所有 registered stores。
- `GameplayComponentRegistry.GetOrCreateStore<T>()` 返回已有 typed store，或创建并注册一个新 store。
- `GameplayComponentQuery` 提供稳定查询辅助：`CopyEntities`、`CopyComponents`、`CopyEntries` 和 `CopyPairs`。
- `GameplayComponentQuery.CopyPairs(primary, secondary, output)` 以 primary store 的稳定 entity id 顺序输出交集。
- Query 方法 append 到调用方 output，不隐式 clear，也不暴露 store 内部容器。
- `GameplayComponentWorld` 是 component runtime 组合根，聚合 `GameplayComponentRegistry` 和 `RuntimeEventQueue<GameplayRuntimeEvent>`。
- `GameplayComponentWorld.Schemas` 是 component runtime 的 schema metadata / capability registry，默认存在，也支持组合根注入。
- `GameplayComponentWorld.Clear()` 清空 component registry state 和 pending events，不处理旧 `GameplayWorld` / `RuntimeEntity`；只应用于 session reset / world reset。
- `GameplayRuntimeModule.ComponentWorld` 默认存在；module 的 `Events` 与 `ComponentWorld.Events` 是同一个 queue。
- `GameplayComponentWorldDiagnostics` 输出 component runtime 的结构摘要：alive entity ids、registered store type/count 和 event queue snapshot。
- Store diagnostics 按 component type full name 稳定排序；当前不保存泛型 component value，不定义 SaveState schema。
- Component value 参与 diagnostics / hash / SaveState 前必须先注册显式 schema。Schema 使用长期稳定的 `StableId` 作为权威 component type key，不使用 `Type.FullName`、反射字段顺序或泛型 store 的自动 JSON 形态作为权威格式。
- Component schema descriptor 负责声明 schema version、诊断 writer、hash writer 和 SaveState adapter。一个 component 可以分阶段只支持 diagnostics，不支持 hash/save。
- 同一 component 在 schema registry 中只能有一个 schema entry。Diagnostics、hash 和 SaveState capability 可以由同一个 descriptor 实现，也可以挂在同一个 entry 下，但不能用多个 schema entry 重复注册同一 `StableId` 或 `ComponentType`。
- Capability adapter 的泛型 component type 必须匹配 `Schema.ComponentType`，且 schema 必须显式声明 `SupportsDiagnostics` / `SupportsHash` / `SupportsSaveState` 才能注册对应 adapter。
- `SupportsHash` 等 support flag 描述的是 schema entry 支持的 capability 集合，不表示每一个注册到该 entry 的 descriptor 都必须自己实现对应 writer / adapter。
- Registry snapshot 只暴露 schema metadata；真正执行 diagnostics / hash / SaveState 时，executor 必须通过 registry 解析对应 capability adapter，不能拿 metadata 后自行反射 component value。
- Diagnostics executor 后续统一写入 `schemaId` / `schemaVersion`；单个 component diagnostics writer 只写 entity 和 component fields。
- `GameplayCoreComponentSchemaDescriptors.RegisterDiagnostics` 注册 core diagnostics descriptors；`RegisterRuntimeHash` 注册 core hash writers；`RegisterSaveState` 注册 core SaveState adapters，三者可以按任意顺序挂到同一个 schema entry。
- `GameplayComponentWorldHashContributor` 通过 schema registry 接入 `RuntimeHashCombiner`，按 alive entity 顺序、schema `StableId` 顺序和 component 字段显式 writer 顺序写入；集合字段必须排序，浮点必须量化。
- `GameplayComponentWorldSaveStateProvider` 通过 `RuntimeModuleSaveState.CustomState.PayloadJson` 保存 `schemaId`、`schemaVersion`、`entityIndex`、`entityGeneration` 和 adapter 写出的结构化 payload；restore 遇到 missing schema、missing adapter、unsupported version 或 invalid entity id 返回结构化错误。
- 未注册 schema 的 component store 只能出现在 store type/count 摘要中，不得通过反射展开 value 或直接进入 hash/save。
- 详细 schema 契约见 `Docs/Tasks/GAMEPLAY_ECS_STYLE_09_COMPONENT_SCHEMA_CONTRACT.md`；runtime hash 实现见 `Docs/Tasks/GAMEPLAY_ECS_STYLE_11_COMPONENT_RUNTIME_HASH.md`；component SaveState 实现见 `Docs/Tasks/GAMEPLAY_ECS_STYLE_12_COMPONENT_SAVE_STATE.md`；component state system 实现见 `Docs/Tasks/GAMEPLAY_ECS_STYLE_13_COMPONENT_STATE_SYSTEMS.md`；component spawn definition 实现见 `Docs/Tasks/GAMEPLAY_ECS_STYLE_14_COMPONENT_SPAWN_DEFINITIONS.md`；component attribute runtime 实现见 `Docs/Tasks/GAMEPLAY_ECS_STYLE_15_COMPONENT_ATTRIBUTE_RUNTIME.md`；component ability command bridge 实现见 `Docs/Tasks/GAMEPLAY_ECS_STYLE_16_COMPONENT_ABILITY_COMMAND_BRIDGE.md`；component ability targeting 实现见 `Docs/Tasks/GAMEPLAY_ECS_STYLE_17_COMPONENT_ABILITY_TARGETING.md`；component ability rules 实现见 `Docs/Tasks/GAMEPLAY_ECS_STYLE_18_COMPONENT_ABILITY_RULES.md`；component runtime vertical slice 验收见 `Docs/Tasks/GAMEPLAY_ECS_STYLE_19_COMPONENT_RUNTIME_VERTICAL_SLICE.md`。
- 本批次不迁移 `RuntimeEntity` / `GameplayWorld` 的权威状态，不建立双写 source of truth。

Component attribute runtime v0：

- `GameplayAttributeValue` 表达 `AttributeId`、`BaseValue` 和 `CurrentValue`，`AttributeId` 必须大于 0；v0 只支持 int 属性，不做浮点量化。
- `GameplayAttributeSetComponent` 是 component-native attribute source of truth，内部按 `AttributeId` 升序保存，构造时拒绝重复 attribute id，`ToArray()` 返回副本。
- Attribute set mutate API 返回新的 component value；system 必须把更新后的 value 写回 `GameplayComponentStore<GameplayAttributeSetComponent>`。
- `SetBaseValue` 保留已有 current value；`SetCurrentValue` 在属性不存在时创建 base/current 都等于目标 current；`AddCurrentValue` 在属性存在时累加 current。
- `GameplayAttributeCommandSystem` 运行在 `GameplaySystemPhase.Command`，默认 priority 为 `40`，在 spawn command system 之后、unsupported command system 之前。
- `SetComponentAttribute` command 使用 `targetId=entity.index`、`payload0=entity.generation`、`payload1=attributeId`、`payload2=value`；语义只设置 current value，不设置 base value；已有 attribute 的 `BaseValue` 不变，缺少 attribute set / attribute 时会创建 base/current 都等于 value 的 attribute。
- `AddComponentAttribute` command 使用相同 payload 形态，`payload2=delta`；缺少 attribute set 或 attribute id 时会拒绝，避免把未知属性从 0 隐式创建为 delta。
- 成功更新属性会输出 `GameplayRuntimeEventType.ComponentAttributeChanged`，并写入 `AttributeId`、`OldAttributeValue`、`NewAttributeValue` 和 `AttributeDelta`。
- `GameplayAttributeComponentSchemaDescriptors` 提供 attribute set diagnostics、runtime hash 和 SaveState adapters；payload 使用稳定 JSON 字段名，不序列化旧 `AttributeStore`。
- Spawn definition 可以通过 `GameplayComponentSpawnInitializer<GameplayAttributeSetComponent>` 初始化 attribute set；spawn definition 本身仍不是 world state。
- 旧 `RuntimeEntity.AttributeStore` 继续服务 v0 Ability / Demo；新 component runtime 不与旧 AttributeStore 双写同一个玩法对象状态。

Component ability command bridge v0：

- `CastComponentAbility` 是 component runtime 专用 command，不复用旧 `CastAbility` 的裸 int entity payload；v0 只支持 self target。
- `GameplayRuntimeCommandFactory.CastComponentAbility` 使用 `targetId=caster.index`、`payload0=caster.generation`、`payload1=abilityId`、`payload2=0`；v0 不把 candidate generation 偷塞进 `traceId`，非 0 `payload2` 会被拒绝。
- `GameplayComponentAbilityRegistry` 只保存 component-native abilities，拒绝 `AbilityId <= 0` 和重复 id，snapshot 按 ability id 升序输出；registry 不是 world state，不进入 hash / SaveState。
- `IGameplayComponentAbility.Cast` 只接收 `GameplayComponentAbilityContext`，上下文包含 `RuntimeFrame`、`GameplayComponentWorld`、caster `GameplayEntityId`、target ids 和 trace id，不依赖 `AbilityContext` / `IRuntimeEntity`。
- `GameplayComponentAttributeDeltaAbility` 是 v0 最小 effect adapter，只支持 `GameplayComponentTargetMode.Self`，读取 self entity 的 `GameplayAttributeSetComponent`，对已有 attribute 执行 current value delta 并写回 component store。
- 缺少 caster、ability、attribute set / attribute 或 effect overflow 会返回结构化失败结果，并由 command system 输出 `AbilityCastFailed` event。
- 成功 cast 会输出 `AbilityCastSucceeded / CastComponentAbility`，并把 component entity id 写入 `GameplayRuntimeEvent.ComponentEntityId`；attribute delta 同时输出 `ComponentAttributeChanged` event。事件顺序是 effect event 先入队，command system 的 final cast event 后入队，因此一次 self delta 成功 cast 的稳定顺序为 `ComponentAttributeChanged` -> `AbilityCastSucceeded`。
- Ability 修改后的 attribute state 通过 `GameplayAttributeComponentSchemaDescriptors` 参与 component world hash / SaveState；Restore 只恢复结果状态，不依赖 ability registry。
- v0 不支持 explicit candidate target、cooldown、cost、cast time、buff / modifier pipeline 或旧 `IAbility` adapter；future candidate / target entity payload 必须通过新的 command schema 或 side request store 引入，不能 ad-hoc 复用 `payload2`，也不能让旧 `RuntimeEntity` 和 component store 双写同一状态。

Component ability targeting v0：

- `GameplayComponentTargetCandidate` 是 generation-safe target snapshot，保存 `GameplayEntityId`、team、lifecycle、tag ids 和 status ids，不持有 store 引用。
- `GameplayComponentTargetCandidates.CopyFromWorld` 以 `GameplayComponentWorld.CreateEntitySnapshot()` 为基准，按 entity id 稳定顺序构建候选；team 缺失为 `0`，lifecycle 缺失为 `None`，tag/status 缺失为空集合。
- `GameplayComponentTargetQuery` 使用 caster `GameplayEntityId`、caster team、alive requirement、`GameplayTargetRelationFilter`、required tags、blocked statuses 和 max targets 表达 filter。
- `GameplayComponentTargetingService` 只处理 component candidates，不复用旧 `IRuntimeEntity` / 裸 int targeting result；输入顺序就是候选优先级，selected / rejected 输出顺序稳定。
- Rejected reason 复用 `GameplayTargetRejectReason`，但 rejected target 保存完整 `GameplayEntityId`。
- `GameplayComponentAbilityRequestStore` 是 transient input store，不属于 `GameplayComponentWorld` state，不参与 component world hash / SaveState；组合根负责持有，world/session reset 时应调用 `Clear()`。
- `GameplayComponentAbilityRequestStore.Clear()` 只清 pending requests，不重置 allocator index / generation；旧 handle 在 clear 后仍然失效，不会因为下次 request 复用而重新变有效。
- `CastComponentAbilityRequest` 使用 `targetId=requestHandle.index`、`payload0=requestHandle.index`、`payload1=requestHandle.generation`、`payload2=abilityId`，通过 request store 读取完整 caster id、candidate ids 和 query。
- Request command 成功或结构化失败后都会 remove request handle，避免 transient request 泄漏；missing request 无法 remove。
- Explicit target ability 通过 `GameplayComponentTargetMode.ExplicitSingle` 读取 selected target list 的第一个 target；`Self` mode 仍只修改 caster。
- 如果 request 没有 candidate ids，command system 会从 component world 构建全部 candidates；如果 request candidate id stale / missing，cast 失败为 `MissingComponentAbilityTarget`；如果 targeting filter 后没有 selected target，失败为 `NoValidComponentAbilityTarget`。
- Targeting request 不进入 SaveState。如果保存发生在 request 入队后、command 执行前，当前 v0 不捕获 pending request；后续需要由 Runtime command/save orchestration 处理，而不是 ComponentWorld SaveState。

Component ability rules v0：

- `IGameplayComponentAbility.Rules` 是 ability definition 的一部分，不是 world state；registry 只注册 ability，rule set 跟随 ability object。
- `GameplayComponentAbilityRuleSet` 当前支持 cooldown frame gate 和 attribute costs；cost 按 `AttributeId` 升序保存和执行，避免注册顺序影响行为或 hash。
- `GameplayAbilityCost` 要求 `AttributeId > 0`、`Amount >= 0`；`Amount == 0` 是 no-op cost。
- `GameplayAbilityCooldownComponent` 是 component-native cooldown state，按 `AbilityId` 升序保存 `EndFrame`；`EndFrame > currentFrame` 表示仍在 cooldown 中。
- `GameplayComponentAbilityRules.Evaluate` 只读，不修改 state；command system 当前按 `Evaluate -> CommitCosts -> Cast ability -> CommitCooldown on success -> final event` 执行。
- `GameplayComponentAbilityCommandSystem` 在 ability cast 前会先清理 caster 上已过期 cooldown，再执行 rule evaluate；rule rejected 输出 `AbilityCastFailed` 且不调用 ability effect。
- 过期 cooldown 采用惰性清理：只有当前 caster 在 cast 前会清理；闲置 entity 的过期 cooldown 可能继续留在 hash / SaveState 中，直到该 entity cast 或后续 cleanup system 处理。
- 成功 cast 且有 cost 的事件顺序固定为 cost commit event、ability effect event、final `AbilityCastSucceeded` event；如果没有 cost，则没有 cost commit event。
- Cost commit 扣 caster 的 `GameplayAttributeSetComponent` current value，不修改 base value；insufficient cost 输出 `ComponentAbilityInsufficientCost`。
- 当前 v0 不是完整事务 / rollback 模型：cost 在 effect 前提交，effect 失败不会自动 refund；cooldown 只在 effect success 后启动。若未来需要退费、预演或回滚，应通过显式 refund policy / transaction adapter 扩展。
- Cooldown rejected 输出 `ComponentAbilityOnCooldown`；invalid rule 输出 `InvalidComponentAbilityRule`。
- `GameplayAbilityCooldownComponentSchemaDescriptors` 提供 diagnostics、runtime hash 和 SaveState adapters；cooldown state 参与 ComponentWorld hash / SaveState，cost definition 不进入 SaveState。
- v0 不做 cast time、channel、interrupt、projectile、global cooldown、buff / modifier gate，也不自动加入 default pipeline；只要项目注册 `GameplayComponentAbilityCommandSystem`，rules 就随 ability 生效。

Component buff / modifier runtime v0：

- `GameplayComponentBuffEntry` 表达 runtime buff state：`BuffId`、`StackCount`、`MaxStackCount`、`EndFrame`、`IsPermanent` 和 `SourceId`；`BuffId`、stack 字段必须为正数，`EndFrame` / `SourceId` 不允许为负数。
- `GameplayComponentBuffSetComponent` 是 component-native buff source of truth，内部按 `BuffId` 升序保存，构造时拒绝重复 buff id，`ToArray()` 返回副本。
- `GameplayComponentModifierEntry` 表达 additive modifier state：`ModifierId`、`AttributeId`、`AddValue` 和 `SourceBuffId`；v0 只支持 int additive modifier，不做乘区、优先级或 stat formula graph。
- `GameplayComponentModifierSetComponent` 按 `ModifierId` 升序保存，拒绝重复 modifier id；`GetAdditiveValue(attributeId)` 只汇总目标 attribute 的 additive value。
- `GameplayComponentModifierEvaluator.GetModifiedCurrentValue` 是只读 helper：读取 `GameplayAttributeSetComponent.CurrentValue` 并叠加 modifier set，返回展示 / 计算值；它不会写回 `GameplayAttributeSetComponent`，也不会改变 base/current source of truth。
- `GameplayComponentBuffCleanupSystem` 运行在 `GameplaySystemPhase.Resolution`，默认 priority 为 `80`；它 snapshot buff store，移除 `!IsPermanent && EndFrame <= currentFrame` 的 buffs。
- 当过期 buff 被移除时，cleanup system 会同步移除同 entity modifier set 中 `SourceBuffId` 命中过期 buff id 的 modifiers；buff set / modifier set 变空时会移除对应 component。
- Cleanup 缺少 `GameplaySystemContext.ComponentWorld` 时输出 `CommandRejected / MissingComponentWorld` 诊断事件，不抛空引用异常；v0 不为每个过期 buff 输出 typed event，避免继续膨胀 `GameplayRuntimeEvent`。
- `GameplayComponentBuffSchemaDescriptors` 和 `GameplayComponentModifierSchemaDescriptors` 提供 diagnostics、runtime hash 和 SaveState adapters；payload 使用显式 DTO，不序列化旧 `BuffPipeline` / `ModifierPipeline`。
- Buff / modifier runtime v0 不迁移旧 `BuffSnapshot`、`ModifierSnapshot`、`BuffPipeline` 或 `ModifierPipeline`，也不让旧 `RuntimeEntity` 与 component store 双写同一状态；Combat bridge、复杂 stack 规则和 authoring import 留给后续任务。

Component runtime vertical slice v0：

- 当前 component gameplay runtime v0 已通过最小闭环验收：`RuntimeHost -> RuntimeCommandBuffer -> GameplayRuntimeModule -> GameplaySystemPipeline -> spawn -> attributes -> targeting -> ability rules -> effect -> lifecycle cleanup -> events -> hash -> SaveState`。
- 验收场景是 Hero 通过 spawn command 创建、Enemy 通过 spawn command 创建，Hero 使用 `CastComponentAbilityRequest` 对 explicit enemy target 释放 Strike。
- Strike 使用 component-native ability、attribute set、cost 和 cooldown；成功 cast 后稳定输出 cost attribute event、effect attribute event、final ability success event。
- Cooldown rejected 不执行 effect，也不改变 component world state hash，除非该 cast 触发过期 cooldown 惰性清理。
- Enemy HP 到 0 后，当前 v0 不自动死亡；测试 helper 显式把 lifecycle component 标记为 `PendingDestroy`，再由 `GameplayLifecycleCleanupSystem` 在 Resolution phase 销毁 entity 并清理 registered stores。
- ComponentWorld SaveState 捕获 entity、core components、attributes 和 cooldown；restore 不需要 spawn registry、ability registry 或 request store。
- Restore 后继续 cast 需要重新提供 runtime registries 和 transient request store；这些是组合根输入依赖，不是 SaveState 内容。
- 旧 `RuntimeEntity` / `GameplayWorld` route 仍保留，component runtime vertical slice 不与旧 source of truth 双写同一玩法对象。
- Buff / Modifier component runtime、Combat bridge、cast time / interrupt、UI diagnostics 和 playable demo scene 是下一阶段，不属于当前 v0 闭环。

Component spawn definitions v0：

- `GameplayComponentSpawnDefinition` 是显式注册对象，包含 `DefinitionId`、`StableId`、`SchemaVersion` 和稳定顺序的 initializers；不做程序集扫描。
- `GameplayComponentSpawnRegistry` 只保存 definitions，不拥有 `GameplayComponentWorld`；重复 `DefinitionId` 或 `StableId` 会抛错，snapshot 按 `DefinitionId` 稳定排序。
- `StableId` 使用小写 dotted id；空白、前后空格、首尾点、连续点和大写字符都会被拒绝。
- 同一个 `GameplayComponentSpawnDefinition` 内不允许出现重复 initializer `SchemaId`，避免同一 component 初始化被静默覆盖或诊断语义冲突。
- `IGameplayComponentSpawnInitializer` 只负责把初始 component 写入新 entity；`GameplayComponentSpawnInitializer<T>` 使用 `world.GetOrCreateStore<T>().Set(entityId, component)`，不读取 Unity object、时间、随机数或外部 mutable state。
- Initializer `SchemaId` 在 v0 中是稳定描述 / diagnostics key，不会在 `Apply()` 时强制 lookup `GameplayComponentWorld.Schemas`，也不会校验该 schema id 与 `T` 的 component type 匹配；需要强校验时后续通过显式 validation pass 接入。
- `SpawnComponentEntity` command 使用 `GameplayRuntimeCommandIds.SpawnComponentEntity`，payload 只携带 `spawnDefinitionId` 和 `variantId` 等稳定 id，不携带任意 component 字段。
- `GameplayComponentSpawnCommandSystem` 运行在 `GameplaySystemPhase.Command`，默认 priority 为 `30`，在 component entity command system 之后、unsupported command system 之前。
- Spawn command 成功时创建 entity、按 definition initializer 顺序写入 components，并输出 `ComponentEntityCreated / SpawnComponentEntity` event。
- 任一 initializer 失败时，system 会调用 `GameplayComponentWorld.DestroyEntity(entityId)` 回滚新建 entity，并输出 `CommandRejected / SpawnInitializerFailed`。
- 缺少 component world、spawn registry、definition 或空 initializer definition 时，system 输出稳定 reason 的 `CommandRejected` event，并标记 command handled。
- Spawn definition 本身不是 world state；hash / SaveState 只保存 spawn 之后的 entity 和 component stores，restore 不需要 spawn registry 仍存在。
- `GameplayRuntimeModule.CreateDefaultSystemPipeline` 暂不自动注册 spawn system；项目组合根通过 `configureDefaultPipeline` 显式提供 spawn registry。

Core components v0：

- `GameplayIdentityComponent` 表达 definition / variant 身份，不替代 `GameplayEntityId`。
- `GameplayTeamComponent` 表达 team id，并通过 `GameplayTeamRelations` 判断关系。
- `GameplayLifecycleComponent` 表达 component runtime lifecycle state，不替代 generation id lifecycle。
- `GameplayTagComponent` / `GameplayStatusComponent` 构造时过滤 invalid id、排序、去重并拷贝输入；`ToArray()` 返回副本。
- Core components 不引用 Unity / Editor / Demo / WGame 私有数据，也不复制 `RuntimeEntity` 的现有状态。

System pipeline v0：

- `GameplaySystemPipeline` 按 `GameplaySystemPhase`、`Priority`、注册顺序稳定执行 systems。
- 同 phase / priority 下 registration order 具有语义；注册顺序变化会影响执行顺序。
- Disabled system 会被跳过，但保留在 pipeline snapshot 中。
- `GameplaySystemContext.Commands` 是 `GameplayRuntimeModule` 已 drain 的帧内临时只读 view；system 不拿 `RuntimeCommandBuffer`，也不能调用 `DrainForFrame`。需要跨 Tick 保留 command 时必须复制值，不能持有列表引用。
- `GameplaySystemContext.CommandState` 是同一帧 pipeline-local 状态；处理或明确拒绝 command 的 system 必须调用 `MarkHandled(command)`，并且必须使用从 `GameplaySystemContext.Commands` 读到的原始 command 值，不要重构 command 后标记。
- `GameplaySystemContext.Events` 是 module 的 `RuntimeEventQueue<GameplayRuntimeEvent>`，system 可以 enqueue frame event，但 Gameplay 内部不强制 flush。
- `GameplaySystemContext.ComponentWorld` 是新 component runtime 入口；component systems 应通过它访问 registry / stores，不要长期持有私自 new 出来的 stores。
- `GameplaySystemContext.Events` 与 `GameplaySystemContext.ComponentWorld.Events` 必须一致；手动构造 context 时如果传入不同 queue 会抛异常。
- `GameplayRuntimeModule` 驱动的 context 保证 `ComponentWorld` 非 null；手动构造 context 时，只有不访问 component runtime 的测试 / system 可以省略它。
- `GameplayRuntimeModule` 默认创建 command systems pipeline。v0 执行顺序是 drain command、pipeline PreCommand、pipeline Command、pipeline Simulation、pipeline Resolution、pipeline Diagnostics、可选 world tick。
- 要基于默认 pipeline 扩展时，优先使用 `GameplayRuntimeModule` 的 `configureDefaultPipeline` 构造参数追加自定义 system；这样 module 仍会把 ability result sink 接到 `AbilityResults`。
- 显式传入 custom pipeline 表示调用方完全接管 pipeline 注册；module 不再执行内置 command switch，也不会自动重接外部 pipeline 中 ability system 的 result sink。
- Custom command systems 应使用低于 `GameplayUnsupportedCommandSystem` 默认 `int.MaxValue` 的 priority，除非调用方要替换 unsupported handling。
- System 抛异常时，pipeline 用 `GameplaySystemPipelineException` 包装 system id 和 phase 后重新抛出。

Component state systems v0：

- `GameplayLifecycleCleanupSystem` 运行在 `GameplaySystemPhase.Resolution`，默认 system id 为 `mxframework.gameplay.lifecycle.cleanup`。
- Cleanup system 只读取 `GameplaySystemContext.ComponentWorld` 中的 `GameplayLifecycleComponent` store，不读取或迁移旧 `RuntimeEntity` / `GameplayWorld` 状态。
- Cleanup tick 会 snapshot lifecycle store，找出 `State == PendingDestroy` 的 entity，按 `GameplayEntityId` 稳定顺序调用 `GameplayComponentWorld.DestroyEntity(id)`。
- Destroy 必须走 `GameplayComponentWorld.DestroyEntity`，让 `GameplayComponentRegistry` 清理所有 registered component stores。
- 成功清理后输出 `GameplayRuntimeEventType.ComponentEntityDestroyed`，reason 为 `GameplayLifecycleEvents.PendingDestroyCleanupReason` / `PendingDestroyCleanup`，`CommandId` 为 `0`。
- `Alive` 和 `Destroyed` lifecycle state 不会被 cleanup system 销毁；`Destroyed` 不应长期保留在 alive entity 的 component store 中，真正销毁仍应由 cleanup 或显式 destroy command 完成。
- 手动构造 context 且缺少 component world 时，cleanup system 输出 `CommandRejected / MissingComponentWorld` 诊断事件，不抛空引用异常。
- `GameplayRuntimeModule.CreateDefaultSystemPipeline` 暂不自动注册 lifecycle cleanup；项目层可通过 `configureDefaultPipeline` 或自定义 pipeline 显式加入。

Gameplay command systems v0：

- `GameplayAbilityCommandSystem` 处理 `GameplayRuntimeCommandIds.CastAbility`，复用 `GameplayAbilityRuntimeService`，输出 `AbilityCastSucceeded` / `AbilityCastFailed` event，并标记 command handled。
- `GameplayEntityLifecycleCommandSystem` 处理 `GameplayRuntimeCommandIds.DespawnEntity`，移除 `GameplayWorld` v0 entity 并输出 `EntityDespawned` / `CommandRejected` event，并标记 command handled。
- `GameplayComponentEntityCommandSystem` 处理 `CreateComponentEntity` / `DestroyComponentEntity`，读写 `GameplayComponentWorld` generation entity，并输出 `ComponentEntityCreated` / `ComponentEntityDestroyed` / `CommandRejected` event。
- `GameplayComponentSpawnCommandSystem` 处理 `SpawnComponentEntity`，按 `GameplayComponentSpawnRegistry` 中的 definition 创建带初始 components 的 component entity，并输出 `ComponentEntityCreated / SpawnComponentEntity` 或 `CommandRejected` event。
- `GameplayAttributeCommandSystem` 处理 `SetComponentAttribute` / `AddComponentAttribute`，读写 `GameplayAttributeSetComponent` 并输出 `ComponentAttributeChanged` 或 `CommandRejected` event。
- `GameplayComponentAbilityCommandSystem` 处理 `CastComponentAbility`，通过 `GameplayComponentAbilityRegistry` 调用 component-native ability，并输出 `AbilityCastSucceeded` / `AbilityCastFailed` event。
- `GameplayComponentAbilityCommandSystem` 同时处理 `CastComponentAbilityRequest`，通过 `GameplayComponentAbilityRequestStore` 和 `GameplayComponentTargetingService` 解析 generation-safe explicit targets。
- `GameplayComponentEntityCommandSystem` 依赖 `GameplaySystemContext.ComponentWorld`。缺少 component world 时输出 `CommandRejected / MissingComponentWorld`，不抛出 NRE。
- `DestroyComponentEntity` command 使用 `payload0=index`、`payload1=generation`，拒绝 stale / invalid id，避免误删复用后的新 entity。
- `GameplayRuntimeEvent.ComponentEntityId` 是 component runtime 的 generation-safe event id；旧 `TargetEntityId` 仍服务 v0 `RuntimeEntity` / Ability 事件。事件构造会校验 component entity index / generation 必须同时为 default 或有效值，需要安全读取时使用 `TryGetComponentEntityId`。
- `GameplayRuntimeEvent` 当前只承载 common header 和少量已落地字段；后续 target list、cost、cooldown、cast time、interrupt 等 ability detail 不应继续扩充到该 DTO，优先使用 event detail / custom state / typed event stream。
- `GameplayUnsupportedCommandSystem` 在 default pipeline 中拒绝未 handled command，reason 为 `UnsupportedGameplayCommand`；它不维护硬编码 command id 白名单。
- `GameplayRuntimeModule.AbilityResults` 由 ability command system 的 result sink 写入，仍只保留最近 N 条诊断结果。

Hash / diagnostics：

- `GameplayHashContributor` 实现 `IRuntimeHashContributor`，可接 entity list 或 `GameplayWorld`。
- Hash 输入按 entity id、attribute id、buff id、modifier id 等稳定顺序写入。
- `GameplayComponentWorldHashContributor` 实现 `IRuntimeHashContributor`，只写入显式注册 hash writer 的 component runtime state。
- `GameplayWorldDiagnostics` 复用 `GameplayDiagnosticSnapshotBuilder`，并提供 entity/alive/attribute/buff/modifier 计数摘要。

## Ability Runtime Graph v0

Ability Runtime Graph 是 `SimpleAbility` 之上的数据驱动运行时表达。它不替代现有 `IAbility`，而是给后续编辑器、配置映射、Replay 和 Preview 提供稳定图模型。

最小能力：

- `AbilityGraphDefinition` 持有 graph id、version、entry node、稳定排序 nodes / edges。
- v0 节点：`Entry`、`Sequence`、`TargetQuery`、`ApplyEffect`、`EmitEvent`、`PhaseGate`。
- `AbilityGraphValidator` 返回结构化 error code、node id、edge index、field path，不把 authoring 错误变成空引用异常。
- `AbilityGraphRuntimeExecutor` 按稳定 edge 顺序执行图，并用 step budget 防止损坏图自旋。
- `TargetQuery` 复用 `GameplayTargetingService`；`ApplyEffect` 复用 `IAbilityEffect.Apply(AbilityContext, target)`。
- `AbilityGraphTimelineScheduler` 通过显式 frame 推进 phase，不读取 Unity time、coroutine、Animator 或 Timeline asset。
- `AbilityGraphTimelinePhaseGate` 可以把 timeline state 接入 executor 的 `IAbilityGraphPhaseGate`。
- `AbilityGraphDiagnosticSnapshot` / `AbilityGraphExecutionTrace` / `AbilityGraphHashContributor` 支持诊断、Replay golden fixture 和 Runtime hash。

Ability timeline frame 契约：

- Ability timeline frame 是 `AbilityGraphTimelineScheduler.Advance(timeline, state, deltaFrames)` 消费的整数步数，保存在 `AbilityGraphTimelineState.ElapsedFramesInPhase` 和 `TotalElapsedFrames` 中。
- 它是 timeline-local phase 计数单位，不天然等于 `RuntimeTickContext.FrameIndex`、`RuntimeFrame` 或 `CombatFrame`。组合根必须声明本条 timeline 由哪个 frame domain 驱动。
- 当 Ability timeline 由 Combat action / hit / cast window 驱动时，`deltaFrames` 必须来自 Combat fixed simulation step 数；常规路径是一帧 Combat authority step 调一次 `Advance(..., 1)`，或在 catch-up 时传入同一 Runtime tick 内产生的 Combat step 数。
- 当 Ability timeline 由纯 Gameplay RuntimeHost loop 驱动时，调用方可以选择 Runtime frame delta 作为 `deltaFrames`，但必须把该选择写入组合根或模块契约；不得在同一 timeline state 中混用 Runtime frame 和 Combat fixed step。
- `RuntimeTickContext.DeltaTime` 不能直接传给 timeline scheduler；需要秒数到 frame 的转换时，转换器必须先选定固定 step 配置、accumulator 和 max-step 策略，再把结果作为整数 `deltaFrames` 传入。
- Timeline transition、phase gate、diagnostics、Replay fixture 和 hash 只记录 timeline frame 结果；如需跨 Runtime / Combat 对齐，外层事件或 trace 应同时记录来源 Runtime frame 与权威 Combat frame。

边界：

- v0 不包含可视化编辑器、GraphView、Unity Inspector authoring。
- v0 不包含 cooldown、cost、cast time、interrupt、公式 DSL 或条件 DSL。
- v0 不绑定 projectile、physics、range、navigation、animation event 或资源引用。
- v0 不迁移 WGame 真实 Ability JSON；项目层应先映射到 Config.Runtime 的 synthetic graph config。

## 最小示例

```csharp
using MxFramework.Gameplay;

const int AttrHp = 1;
const int AttrAttack = 2;
const int AttrDefense = 3;

var caster = new RuntimeEntity(entityId: 1, teamId: 1, hpAttributeId: AttrHp);
var enemy = new RuntimeEntity(entityId: 2, teamId: 2, hpAttributeId: AttrHp);

caster.AttributeStore.RegisterAttribute(AttrHp, 1000);
caster.AttributeStore.RegisterAttribute(AttrAttack, 120);
caster.AttributeStore.RegisterAttribute(AttrDefense, 20);

enemy.AttributeStore.RegisterAttribute(AttrHp, 600);
enemy.AttributeStore.RegisterAttribute(AttrAttack, 80);
enemy.AttributeStore.RegisterAttribute(AttrDefense, 10);

var ability = new SimpleAbility(
    abilityId: 1,
    targetSelector: new SingleEnemyTargetSelector(),
    effects: new IAbilityEffect[]
    {
        new DamageEffect(AttrAttack, AttrDefense, AttrHp)
    });

var context = new AbilityContext(caster, new IRuntimeEntity[] { caster, enemy });
AbilityCastResult result = ability.Cast(context);
```

## Diagnostic Snapshot

`GameplayDiagnosticSnapshotBuilder` 用于把一次 Gameplay 运行片段整理成可测试、可展示、可给 AI 上下文读取的数据对象。它不读取 Unity 场景，也不做 JSON 序列化，只基于调用方传入的实体、属性 ID、最后一次释放结果和事件日志构建快照。

最小输入：

```csharp
var builder = new GameplayDiagnosticSnapshotBuilder();
GameplayDiagnosticSnapshot snapshot = builder.Build(
    sourceName: "ability-slice",
    abilitySource: "BasicAbilityConfig -> ConfigAbilityFactory",
    entities: new[] { caster, enemy },
    attributeIds: new[] { AttrHp, AttrAttack, AttrDefense },
    lastCastResult: result,
    abilityEvents: abilityEvents,
    attributeEvents: attributeEvents);
```

快照内容：

- `Entities`：每个实体的 `EntityId`、`TeamId`、`IsAlive`、指定属性的 `FinalValue`、当前 Buff 快照和 Modifier 快照。
- `AbilitySource` / `LastCast`：最后一次释放的来源、是否成功、失败原因和最后目标实体 ID 列表；常用字段也可通过 `LastCastSuccess`、`LastFailureReason`、`LastTargetEntityIds` 直接读取。
- `AbilityEvents`：按输入顺序保留技能事件，用于验证 `CastStarted -> TargetSelected -> EffectApplied -> CastFinished` 等生命周期。
- `AttributeEvents`：按输入顺序保留属性变化事件，用于追踪 HP、攻击、防御等属性变化。

边界：

- Snapshot 是只读诊断视图，不是存档格式、网络协议或回放系统。
- Builder 只消费公开 API，不访问 `AttributeStore`、`BuffPipeline`、`ModifierPipeline` 的私有字段。
- Builder 不负责事件订阅；调用方需要在运行时自行收集 `AbilityEvent` 和 `AttributeChangedEvent`。
- `attributeIds` 决定哪些属性进入快照；未传入的属性不会被枚举。

## v0 支持

- 默认实体组合实现：属性、Buff、Modifier、技能事件。
- GameplayWorld v0：实体 registry、稳定 tick、world snapshot。
- Team / Tag / Status 基础数据结构。
- GameplayTargetingService 逻辑目标过滤与 rejected reasons。
- GameplayAbilityRuntimeService 世界级 Ability cast adapter。
- GameplayRuntimeModule：RuntimeHost / RuntimeCommandBuffer 驱动的 Gameplay command loop。
- GameplayRuntimeEvent：按帧 drain 的 Gameplay runtime event queue。
- Command-driven Gameplay ECS-style 设计契约：组件化状态、系统化逻辑、generation entity id、v0 API bridge 和 source of truth 规则。
- Gameplay ECS-style component store v0：generation entity id、entity lifecycle、component marker 和稳定 store snapshot。
- Gameplay ECS-style system pipeline v0：phase/context/pipeline、稳定顺序、disabled skip、module 单点 drain command。
- Gameplay ECS-style v0 API bridge：component registry 统一 entity destroy cleanup registered stores，不复制 `RuntimeEntity` 状态。
- Gameplay ECS-style core components v0：Identity、Team、Lifecycle、Tag、Status 纯数据组件。
- Gameplay command systems v0：CastAbility、DespawnEntity 和 unsupported command rejection 从 module switch 迁入 systems。
- Gameplay component state systems v0：Lifecycle cleanup 在 Resolution phase 清理 `PendingDestroy` entity，并复用 ComponentWorld destroy cleanup / event queue / hash / SaveState 闭环。
- Gameplay component spawn definitions v0：`SpawnComponentEntity` command 通过显式 definition 初始化 component entity，失败时回滚半初始化状态。
- Gameplay component attribute runtime v0：component-native int attribute set、attribute commands、attribute changed event 和 schema-backed hash / SaveState。
- Ability Runtime Graph v0：图契约、确定性执行、phase timeline、diagnostics、hash。
- 自身目标和单敌方目标选择。
- 直接伤害效果。
- 添加 Buff 效果。
- 技能生命周期事件顺序：`CastStarted`、`TargetSelected`、`EffectApplied`、`CastFinished`，失败时为 `CastStarted`、`CastFailed`。
- 运行时诊断快照：Entity / Attribute / Buff / Modifier / Ability / Event 状态汇总。
- Runtime hash contributor：Gameplay entity/world 状态和 ComponentWorld 状态可接入 `RuntimeHashCombiner`。
- 纯 C# EditMode 测试覆盖。

## v0 不支持

- Cooldown、Cost、Mana、CastTime、Interrupt。
- Range、Projectile、Physics、Navigation。
- Animation、Input、Localization、AssetKey。
- WGame Ability JSON 导入或配置表绑定。
- 可视化 Ability Graph 编辑器、GraphView、Timeline asset、Animation Event 绑定。
- 物理范围、Combat bridge、复杂多目标规则库、公式系统、战斗判定优先级。
- 通用 Gameplay SaveState restore、完整 Ability cooldown/cost/cast/interruption 管线、Snapshot JSON 序列化、编辑器面板和 Runtime Preview 协议接入。

## Config Driven Ability

`MxFramework.Config.Runtime` 提供最小配置桥接，不把配置逻辑放进 `MxFramework.Gameplay` 本体：

| 类型 | 用途 |
|------|------|
| `BasicAbilityConfig` | Ability 配置行，ID 范围 `300000-399999` |
| `AbilityTargetSelectorKind` | 目标选择枚举，当前支持 `Self`、`SingleEnemy` |
| `AbilityEffectConfig` | 单个效果配置，包含 `Kind` 和命名化 `AbilityEffectParameters`；旧 `Parameters` 数组保留兼容 |
| `AbilityEffectKind` | 效果枚举，当前支持 `DamageByAttackDefense`、`ApplyBuff` |
| `ConfigAbilityFactory` | 从 `IConfigProvider` 创建 `IAbility` |
| `RuntimeAbilityConfigResolver` | 配置变更后的 Ability 重建入口 |
| `RuntimeConfigChangeSummary` | source、changed ids、rebuilt ids、failed ids 和错误摘要 |
| `AbilityGraphConfig` | synthetic Ability Graph config DTO，不绑定真实项目 JSON |
| `AbilityGraphConfigMapper` | 将 config DTO 映射为 `AbilityGraphDefinition` 并返回带 config path 的 diagnostics |

### Ability Authoring Contract

`AbilityAuthoringContract` 是面向 AI、编辑器表单和 JSON 的工具输入层；`BasicAbilityConfig` 仍然是运行时配置入口。当前链路是：

```text
AI / Editor / JSON
  -> AbilityAuthoringContract
  -> AbilityAuthoringContractValidator
  -> AbilityAuthoringContractMapper
  -> BasicAbilityConfig
  -> ConfigAbilityFactory
  -> SimpleAbility
```

Authoring contract 使用独立枚举和命名字段：

| 类型 | 用途 |
|------|------|
| `AbilityAuthoringContract` | 版本化 Ability 输入，包含 `AbilityId`、`DisplayName`、`Description`、`TargetSelectorKind`、`Effects` |
| `AbilityAuthoringEffectContract` | 单个效果输入，使用 `AttackAttributeId`、`DefenseAttributeId`、`HpAttributeId`、`BuffId` 命名参数 |
| `AbilityAuthoringTargetSelectorKind` | 工具层目标选择，当前支持 `Self`、`SingleEnemy` |
| `AbilityAuthoringEffectKind` | 工具层效果类型，当前支持 `DamageByAttackDefense`、`ApplyBuff` |
| `AbilityAuthoringValidationCode` | 稳定错误码，供测试、AI 修复和编辑器提示使用 |
| `AbilityAuthoringValidationIssue` / `AbilityAuthoringValidationReport` | 结构化校验结果，包含 code、字段路径和 message |
| `AbilityAuthoringSchema` / `AbilityAuthoringSchemaSummary` | 纯 C# schema summary，列出字段中文名、类型、说明、允许值和错误码 |

稳定错误码包括 `MissingAbilityId`、`InvalidAbilityId`、`MissingDisplayName`、`UnknownTargetSelector`、`MissingEffect`、`UnknownEffectKind`、`MissingEffectParameter`、`InvalidAttributeId`、`InvalidBuffId`、`UnsupportedContractVersion`。测试应断言 code，不依赖本地化 message。

新代码应优先使用命名化工厂：

- `AbilityEffectConfig.DamageByAttackDefense(attackAttributeId, defenseAttributeId, hpAttributeId)`
- `AbilityEffectConfig.ApplyBuff(buffId)`

`AbilityEffectConfig.Parameters` 是兼容旧 Demo/测试和早期配置导入器的位序数组：

- `DamageByAttackDefense`：`Parameters[0]=attackAttributeId`，`Parameters[1]=defenseAttributeId`，`Parameters[2]=hpAttributeId`。
- `ApplyBuff`：`Parameters[0]=buffId`。

`ApplyBuff` 必须通过传入 `IBuffFactory` 创建 Buff。`ConfigAbilityFactory` 不硬编码 Demo Buff，也不会静默跳过未知 selector、未知 effect 或参数不足的配置；这些情况会在 `TryCreate` 中返回 `false` 和明确 error。

运行时配置变更使用重建语义：`RuntimeAbilityConfigResolver` 每次通过当前 `IConfigProvider` 创建新的 Ability，旧 `SimpleAbility` 不在原对象上热替换 selector、effects 或 parameters。已挂载 Buff / Modifier 不根据新配置回溯重算；新配置只影响后续新创建实例。`RuntimeConfigChangeSummary` 用于 Demo、测试和后续 Runtime Preview 展示 config source、变更数量、重建结果和失败原因。

## 后续配置接入

配置层不应直接依赖 Demo。`BasicAbilityConfig` 是 v0 配置入口。后续 WGame Ability 迁移应先映射到这套稳定 API，或在项目组合根中扩展自定义 `ITargetSelector` / `IAbilityEffect`，不要让 `MxFramework.Gameplay` 直接依赖 WGame 数据结构。

## 测试入口

`Assets/Scripts/MxFramework/Tests/Ability/AbilitySliceTests.cs`
