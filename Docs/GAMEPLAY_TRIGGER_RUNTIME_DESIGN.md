# Gameplay Trigger Runtime 框架级触发器设计

> Status: Design
>
> Version: 0.6.0
>
> Date: 2026-07-20
>
> Implementation status: Phase 1 kernel and its P0 oracle are closed; #679 adds Phase 19.2A Gameplay integration, #681 typed Signal envelope, #684 trusted source/exogenous filtering, #686 occupancy/scalar codecs, #688 catalog/authority hash, #690 Runtime checkpoint barrier, #692 canonical Trigger SaveState/atomic restore, #694 internal deferred authority, #696 reliable outbox, #698 bounded presentation Event Router, #700 full Trigger replay playback/bootstrap, #720 the Phase 19.3A deterministic config compiler, #723 the Phase 19.3B deterministic Combat sensor bridge, #725 the Phase 19.3C1 same-frame Gameplay Buff action bridge, #728 the Phase 19.3C2 reliable Gameplay component Destroy/Attribute/Ability command bridge, #730 the Phase 19.3C3 reliable Story RaiseTrigger bridge, #732 the Phase 19.3D Unity Physics occupancy observation adapter, #736 the Phase 19.4A Gameplay Trigger Playable, and #739 the Phase 19.4B bounded network authority/rollback/resync/scale slice while keeping `GameplayRuntimeModule` as the only Gameplay command drain owner. Phase 19.4 and the parent Gameplay Trigger Runtime program are complete; generic Unity action bridges remain outside this program.
>
> Decision record: `Decisions/ADR-008-gameplay-trigger-runtime-authority.md`（Accepted for phased delivery）

## 1. Context

MxFramework 已有多种带有“触发”含义的局部能力，但没有一个能表达“事实进入 -> 条件判断 -> 生命周期策略 -> 确定性动作意图”的公共 Gameplay Runtime：

| 现有能力 | 已解决 | 没有解决 |
| --- | --- | --- |
| `StoryTriggerZoneAdapter` / `Story.RaiseTrigger` | Unity 区域回调转 Story command | 通用 Enter/Stay/Exit、冷却、次数、SaveState、跨模块 action |
| `IModifierCondition` / `IModifierEffect` | 一次 modifier 调用内的条件与效果 | 观察源、成员关系、re-arm、帧序和触发实例状态 |
| Gameplay commands / typed request buffers | 可验证输入和同帧 Gameplay 请求 | 通用触发定义、条件程序和生命周期 |
| `RuntimeEventQueue<T>` / Events | typed 输出传输 | 权威状态、重放输入、可靠多消费者 fanout 和动作事务 |
| `CombatPhysicsWorld` | 确定性空间查询 | Gameplay 语义、条件、冷却和 action dispatch |
| Ability timeline、CharacterAction reaction、projectile/contact、VFX trigger | 各自领域内的明确时序 | 跨玩法的 checkpoint、机关、拾取、区域规则和 signal 规则 |

因此问题不是“再做一个 `OnTriggerEnter` 包装器”，而是补齐一个可复用、可回放、可存档、可诊断的 Gameplay 规则原语。

核心语义固定为：

```text
Observation / Signal
  -> Source Reducer                (产生 Entered / Stayed / Exited / Pulsed)
  -> Route                         (conditions + policy + arbitration)
  -> Activation
  -> Typed Action Intent / Reliable Outbox
  -> Target-owned Bridge
```

本文提出逻辑模块 `MxFramework.Gameplay.Triggers`。它是 noEngine Gameplay authority，作为 `IGameplaySystem` 集合接入现有 `GameplaySystemPipeline`，不是新的 `IRuntimeModule`，也不拥有第二条 command bus。

本文同时记录已实现的 Current contract 与后续目标设计。Phase 1、19.2A-H2、19.3A compiler、19.3B Combat sensor、19.3C1-C3 action bridges、#732 Unity occupancy adapter、#736 19.4A Playable和#739 19.4B network/scale的当前事实以 `Docs/Interfaces/Gameplay.Triggers.md` 为准；明确标为后续领域扩展的类型名/schema 不代表当前仓库已经实现。

## 2. Goals

目标：

- 用同一套模型覆盖区域 Enter/Stay/Exit、手工 Signal、事件事实和 frame-based repeat。
- 条件只读、动作数据化，模块间只传递 typed intent，不直接跨域修改状态。
- 相同初始状态和相同输入得到相同 activation 顺序、state hash 和 replay 结果。
- Trigger、Subject、cooldown、次数、membership、pending mutation 可 Save/Restore。
- 正式 Gameplay 空间事实优先来自 `CombatPhysicsWorld`；Unity 只做 adapter。
- 初始化后 Tick 高频路径 `NoAlloc`，容量不足时结构化失败，不静默丢权威事实。
- 允许模块按需接入，不迫使 Ability、Story、CharacterAction、Combat 或 VFX 的领域 trigger 全部迁移。

第一阶段非目标：

- 不把 Unity `Collider`、`OnTriggerEnter/Stay/Exit` 或 `UnityEngine.Physics` 变成框架权威。
- 不创建第二套 Runtime command bus、Gameplay ECS、EventBus、Combat query 或 Story Director。
- 不提供任意 `Action<>`、UnityEvent、反射方法名、脚本文本或字符串表达式执行。
- 不让 action 直接写 Gameplay component store、Attributes、Buffs、Story Director 或 Unity 对象。
- 不取代 Ability timeline、CharacterAction reaction、projectile/contact 或 VFX 生命周期。
- 不内置 WGame 角色、关卡、机关、任务和真实配置。
- 不在 Phase 1 noEngine kernel 交付可视化编辑器、网络预测、生产级大世界空间索引或 Unity Playable；Playable 由后续19.4A独立验收。

## 3. Terminology And Stable Identity

| 名称 | 含义 |
| --- | --- |
| Trigger Definition | 不可变规则模板：一个 source definition 与一组 routes。 |
| Source Definition | 描述输入如何归约为 occurrence；空间、阈值和 pulse 状态彼此正交。 |
| Trigger Route | 订阅一种或多种 occurrence，并拥有自己的 conditions、policy、arbitration 和 actions。 |
| Trigger Binding | 一个定义与 owner、sensor、authoring placement 的稳定绑定。 |
| Trigger Instance | Binding 在一次 Runtime session 中的 generation-safe 实例。 |
| Observation | 一帧提交的完整 occupancy 集合或 scalar sample。 |
| Signal | 不表达持续占用关系的一次 typed 输入事实。 |
| Subject | 被观察或被 signal 指向的 Gameplay entity；全局 signal 可为空。 |
| Instigator | 造成 signal/activation 的 entity；可与 Subject 不同。 |
| Membership | Trigger Instance 与 Subject 的当前持续关系。 |
| Occurrence | 从输入和旧状态派生出的 `Entered`、`Stayed`、`Exited` 或 `Pulsed`。 |
| Activation | occurrence 通过条件、policy 和 arbitration 后的一次确定性接受。 |
| Action Intent | activation 产生的只读 typed 请求；由目标模块 bridge 消费。 |

目标 ID 均为 typed value，不用裸字符串作运行时身份：

- `GameplayTriggerDefinitionId`：不可复用的定义 ID。
- `GameplayTriggerSourceDefinitionId` / `GameplayTriggerRouteId`：source 和 route 的稳定 ID。
- `GameplayTriggerBindingId`：场景/配置中的稳定绑定 ID。
- `GameplayTriggerInstanceId`：`index + generation` 的运行时 handle。
- `GameplayTriggerSourceBindingId`：occupancy、threshold 或 signal producer 的稳定绑定 ID。
- `GameplayTriggerSignalId`：typed signal 语义 ID。
- `GameplayTriggerConditionKindId` / `GameplayTriggerActionKindId`：注册表 kind ID。
- `GameplayTriggerActivationId`：session 内单调递增的 activation sequence。
- owner、subject、instigator 使用现有 generation-safe `GameplayEntityId`。

`DebugName`、Unity instance ID、GameObject 名称、LayerMask、资源路径和本地化文本只用于 authoring/diagnostics，不能参与身份、排序或 authority hash。

## 4. Source Of Truth And Ownership

| 数据 | 唯一 owner | Trigger Runtime 如何使用 |
| --- | --- | --- |
| Definition / source / route / compiled programs | Trigger Definition Registry | 初始化期冻结并计算 `DefinitionCatalogHash` |
| 静态 Binding / source binding / target slots | Trigger Binding Registry | 初始化期冻结并计算 `BindingCatalogHash` |
| Instance、source state、route policy state、pending mutation | Trigger Runtime Store | 直接拥有、hash、Save/Restore |
| Gameplay entity 生命周期和组件 | `GameplayComponentWorld` | 通过只读 query/condition adapter 读取 |
| 空间事实 | 默认 `CombatPhysicsWorld` | Combat sensor bridge 提交完整 observation |
| Story graph / blackboard | Story Runtime | Trigger 只向 Story bridge 产出 intent |
| Buff / Attribute / Resource 状态 | 各自 Gameplay/domain runtime | Trigger 不直接修改 |
| Runtime command / replay input | 现有 Runtime + Gameplay command path | 外部或非确定输入先命令化 |
| VFX / Audio / Camera / UI | presentation/domain presenter | 只观察 activation/result，不回写 authority |

核心规则：Trigger Runtime 负责“是否触发以及产生什么 intent”；目标模块负责“如何验证并执行自己的请求”。它不成为跨模块事务协调器或上帝系统。

## 5. API Reuse Plan And Dependency Direction

### 5.1 复用计划

| 现有模块/API | 复用方式 | 缺口 |
| --- | --- | --- |
| `RuntimeHost` / `RuntimeFrame` | 使用统一 frame 和生命周期 | 不新增 trigger clock |
| `GameplayRuntimeModule` | 继续作为 Gameplay command buffer 唯一 drain owner | Trigger 仅注册 systems |
| `GameplaySystemPipeline` | Command、Simulation、Resolution、Diagnostics 分阶段运行 | 组合根需验证相对顺序 |
| `RuntimeCommandBuffer` / registry / validator | 外部 Signal、SetEnabled、Reset、非确定 contact 输入 | 分配 command/source/provenance 段，并补 rich Signal replayable envelope |
| typed Gameplay request buffer | 同帧把 action intent 转为 Buff/Gameplay 等请求 | 每个 bridge 明确容量与消费顺序 |
| `RuntimeEventQueue<T>` / fixed buffer | `GameplayTriggerRuntimeEvent` 经唯一 Router 输出 | 需 bounded transport 与多 observer fanout；event 不作权威输入 |
| `GameplayEntityId` | owner/subject/instigator 的 generation safety | membership 仍需 relation store |
| `CombatPhysicsWorld` | 正式空间 observation | 需独立 Combat bridge |
| Runtime hash / SaveState registry | 注册 contributor 和 participant | relation/pending state 不能依赖 component 自动捕获 |
| Runtime replay | 记录外部/non-deterministic commands + result hash | 内部派生变化不重复录制 |
| Diagnostics | 低频 snapshot 与结构化 reason code | 不在 Tick 拼字符串 |

### 5.2 目标依赖 DAG

```text
MxFramework.Core + MxFramework.Runtime + MxFramework.Gameplay
  -> MxFramework.Gameplay.Triggers                 (noEngine authority + systems)

MxFramework.Gameplay.Triggers + MxFramework.Combat
  -> MxFramework.Gameplay.Triggers.CombatBridge    (deterministic spatial sensor)

MxFramework.Gameplay.Triggers + target domain
  -> target-owned TriggerBridge                    (typed action intent consumer)

MxFramework.Gameplay.Triggers + Runtime.Unity
  -> MxFramework.Gameplay.Triggers.Unity           (external observation/authoring adapter)

MxFramework.Gameplay.Triggers + Config
  -> MxFramework.Gameplay.Triggers.Config          (catalog/compiler)
```

`MxFramework.Gameplay.Triggers` 不引用 Combat、Story、Buffs、VFX、Audio、UnityEngine 或 UnityEditor。Bridge 依赖 core 和目标域，反向依赖禁止。

物理上建议独立 noEngine asmdef，以防公共 Gameplay assembly 逐渐吸收所有 adapter；无论物理拆分方式如何，它都必须运行在现有 Gameplay pipeline 内，不能另建一个共享 Gameplay command buffer 的 Runtime module。

## 6. Data Model

### 6.1 Immutable Definition

概念模型：

```text
GameplayTriggerDefinition
  DefinitionId
  DefinitionVersion
  SourceDefinitionId
  Routes[]

GameplayTriggerSourceDefinition
  SourceKind                  Pulse | Occupancy | Threshold
  Source-specific policy      signal ordering / hysteresis / initial sample / evaluation interval

GameplayTriggerRouteDefinition
  RouteId
  OccurrenceMask              Entered | Stayed | Exited | Pulsed
  ConditionProgramId
  ActionProgramId
  ActivationPolicy
  ExecutionOrder              lower first
  ExclusiveGroupId            0 = independent
  WinnerPriority              higher wins, only for explicit group
  ArbitrationScope            Global | PerSubject | PerInstigator | PerPair
```

Source 只负责状态归约，Route 只负责“这个 occurrence 是否以及如何 activation”。Entered 和 Exited 应使用不同 Route，避免 Enter 的 once/cooldown 意外抑制 Exit cleanup。`ExecutionOrder` 只决定多个已接受 activation 的执行顺序；`WinnerPriority` 只决定显式 exclusive group 的 winner，二者不能混为同一 priority。

Definition Registry 在 Runtime session 开始前完成以下工作：

1. 验证 ID 唯一、引用存在、program budget、policy 和 action kind。
2. 将条件树和 action authoring 数据编译为紧凑数组。
3. 按稳定 ID 冻结索引。
4. 计算 `DefinitionCatalogHash`。

活动 session 中不原地热更新 authority definition。开发期热更新必须创建新 revision，并通过受控重建/迁移开始新 session。

### 6.2 Binding And Instance

Binding 至少包含：

- stable `BindingId`、`DefinitionId` 和 `SourceBindingId`；
- optional owner `GameplayEntityId`；
- zero or more stable `GameplayTriggerBindingTarget`：`TargetSlotId + DomainId + Value + Generation`，只含数值身份；
- initial enabled state；
- owner lifetime policy；
- capacity/profile override（只允许 definition 显式开放的字段）。

`ExplicitBindingTarget` action 必须引用已注册 `TargetSlotId`。Binding Registry 同样在 session 前冻结并计算 `BindingCatalogHash`。运行时使用的 `GameplayTriggerCatalogHash = Combine(DefinitionCatalogHash, BindingCatalogHash)`；所有 restore/replay/network mismatch gate 使用 combined hash。运行时可变 target 属于 Instance state/hash/save，并有显式 stale/lifetime policy。core 不保存 Unity object、资源路径或目标模块对象引用。

Runtime Store 为 Binding 分配 generation-safe `InstanceId`。动态 spawn 只能通过确定性 allocator；allocator 的 next index/generation 参与 hash/save，销毁后旧 handle 必须 stale。

### 6.3 Runtime State

权威状态至少包括：

- instance enabled / definition revision；
- 按 `(InstanceId, SubjectEntityId)` 组织的 occupancy membership，或 threshold active/previous sample/primed source state；
- per-subject presence epoch；
- per-route/scope-key exhausted、activation count、last activated presence epoch、last/next eligible frame、next stay frame；
- next activation sequence；
- deferred internal mutation/signal（含 DueFrame 和 stable sequence）；
- reliable cross-module delivery outbox；
- `DefinitionCatalogHash`、`BindingCatalogHash`、combined `GameplayTriggerCatalogHash` 和 state schema version。

Trigger–Subject 是多对多 relation，不把可变数组塞入 component struct。目标实现使用专用、稳定排序、预分配的 relation store，并单独接入 hash/save。

Phase 19.2G1 已把 deferred internal mutation/signal 落为每个 `GameplayTriggerRuntime` 私有拥有的 fixed-capacity authority queue。Phase 19.2G2 另以独立 fixed-capacity reliable outbox 保存完整冻结 record；两条 queue 各有 allocator/capacity，但共同参与一次 activation reservation transaction，并都在 Commit 后才可见。同一 catalog 创建的 runtimes 不共享 queue、sequence 或 outbox module authority。

## 7. Observation And Signal Boundary

### 7.1 Canonical Inputs

Core 只接受三类规范输入：

1. **Complete Occupancy Observation**：一个 `SourceBindingId` 在 scheduled evaluation frame 的完整 Subject 集合。
2. **Signal Batch**：一个 producer 在本帧提交的 ordered sequence 或 unordered multiset；每项含 `SignalId + Subject? + Instigator? + fixed numeric payload`。
3. **Complete Scalar Batch**：一个 `SourceBindingId` 在 scheduled evaluation frame 的完整 `(Subject?, Fix64 value)` 集合，用于带 hysteresis 的 threshold reducer。

概念 API 使用 caller-owned 数据：

```csharp
bool TrySubmitOccupancy(
    RuntimeFrame frame,
    GameplayTriggerSourceBindingId source,
    ReadOnlySpan<GameplayEntityId> subjects,
    out GameplayTriggerInputResult result);

bool TrySubmitSignals(
    RuntimeFrame frame,
    GameplayTriggerSourceBindingId source,
    GameplayTriggerSignalOrdering ordering,
    ReadOnlySpan<GameplayTriggerSignal> signals,
    out GameplayTriggerInputResult result);

bool TrySubmitScalarBatch(
    RuntimeFrame frame,
    GameplayTriggerSourceBindingId source,
    ReadOnlySpan<GameplayTriggerScalarSample> samples,
    out GameplayTriggerInputResult result);
```

三者均目标为 `NoAlloc`。实现可采用等价 buffer writer，但不能要求调用方构造临时集合。

同一 `SourceBindingId` 只能有一个 authority provider，在每个 scheduled evaluation frame 提交一次 complete observation/sample。配置的非采样帧不是 incomplete；到了 scheduled frame 却未完成整批提交才是 failure。occupancy 输入会按 `GameplayEntityId` 稳定排序、去重，再与旧 membership 做 diff：

- new - old = `Entered`；
- new ∩ old = `Stayed`；
- old - new = `Exited`。

如果扫描未完成、容量不足或 provider 重复，必须整批拒绝并保留旧 membership；不能把“没收到的 Subject”推断为 mass exit。

Threshold source 按 `(InstanceId, Subject?)` 保存 reducer state，并固定使用 `Fix64`/raw `long` 与 hysteresis；authoring float 只能在编译期确定性量化，float、NaN 和 Infinity 不进入 Runtime：

- High mode：inactive 且 `current >= enterThreshold` 产生 Entered；active 且 `current < exitThreshold` 产生 Exited，要求 `exitThreshold <= enterThreshold`。
- Low mode 反向，要求 `exitThreshold >= enterThreshold`。
- active 且有 Stayed route 时，每次成功完整采样最多产生一次 Stayed。
- 首次采样必须显式选择 `PrimeSilently` 或 `EmitEnteredIfActive`。
- complete batch 中缺失的旧 Subject 表示离开 provider universe，并按显式 `SubjectDeparturePolicy` 执行 `RemoveSilently`（默认）或 `EmitExited`；provider failure 必须报告 incomplete，保留整批旧 state，不能把 missing 当成 `0`。

Signal 不保持 occupancy/threshold 状态，每个输入稳定地产生一次 Pulsed。排序模式是 authority contract：

- `Ordered`：producer order 和 source sequence 是输入事实，必须被 command/network/replay codec 无损保存，顺序允许影响结果；Unity callback 到达顺序不能默认成为 Ordered sequence。
- `UnorderedMultiset`：按 `SignalId -> Subject -> Instigator -> payload` 规范排序；相同项保留 multiplicity，由 core 分配 `canonicalOrdinal`，原始枚举顺序不影响结果。

包含两个 generation-safe entities、SignalId、payload 和 ordering 的完整 external Signal 无法无损塞入当前 `RuntimeCommand` 三个 payload slot。v0 external adapter 的前置条件是 replayable typed input envelope/codec；禁止用 frame-local sidecar handle 冒充可重放 payload。

Phase 19.2B 冻结 Signal command envelope v1：

- `RuntimeCommand` header 保存 frame、producer `SourceId`、Signal command id 与 `SourceBindingId`；legacy payload 保存 ordering、`ItemIndex`、`ItemCount`。
- fixed envelope 保存 `TypeId + SchemaVersion + Provenance`，八个 `long` 槽依次保存 `BatchId`、`SignalId`、packed Subject、packed Instigator、三个 payload 与 ordered sequence。
- batch key 是 `(Frame, SourceId, SourceBindingId, BatchId)`；每个非空 Signal 对应一个 record，空 batch 使用唯一的 `ItemIndex=-1 / ItemCount=0` record。
- Command system 使用初始化期固定容量 scratch 按 `ItemIndex` 重组；command acceptance sequence 不能替代 item index。缺项、重复项、metadata/schema/type/provenance 不一致或 transport capacity 超限时整批拒绝。
- Phase 19.2C 在 Runtime 增加 sealed command source registry、source-specific command allowlist 和固定 External/TrustedDerived ingress gate。Trigger external producer 只 allowlist `SubmitSignalBatch`，因此 raw `SetEnabled` / `Reset`、unknown source/command、Derived source forgery 与 provenance mismatch 都结构化拒绝；registry-aware exogenous recorder 复用同一分类/allowlist，只复制可信 Exogenous command。
- Phase 19.2D 增加 `SubmitOccupancyBatch=1002004` / `SubmitScalarBatch=1002005` 和 `OccupancyBatch=1002102` / `ScalarBatch=1002103` schema v1。共同 legacy payload 为 `ItemIndex/ItemCount/0`，envelope `Data0=BatchId`；occupancy `Data1=packed non-default Subject`，scalar `Data1=packed optional Subject`、`Data2=Fix64.RawValue`，其余 reserved slot 为 0。empty batch 使用唯一 `ItemIndex=-1 / ItemCount=0` record，且 `Data1..Data7=0`。
- occupancy/scalar encoder 在写 caller destination 前完成全部参数、容量和 item/entity 校验；失败 `written=0` 且 destination 不变。Command system 的 raw identity 包含 `CommandId + Frame + SourceId + SourceBindingId + BatchId`，使用独立预分配 scratch/seen 按 ItemIndex 重组；missing/duplicate/metadata mismatch/malformed/overflow 都把同 raw batch 的全部 records 标记 handled 并整批拒绝，不提交 partial observation/sample。
- `SourceId` / provenance 仍只是数值 metadata。19.2C本地可信边界来自composition root只向adapter/client edge暴露正确gate；19.4B另以protocol-2 profile、connected session/peer identity和Gameplay ownership把client observation映射到同一External gate。该“认证”仍不是credential或cryptography。
- `RuntimeCommandBuffer.Enqueue` 与 default envelope 调用保持 legacy/local compatibility；direct enqueue 和 legacy unfiltered recorder 都不能描述为授权或可信 exogenous replay stream。

### 7.2 Deterministic Spatial Source

Phase 19.3B 已把正式 Gameplay 空间规则落为独立 noEngine `MxFramework.Gameplay.Triggers.CombatBridge`：

1. `GameplayTriggerCombatSensorCatalog` 把 19.3A compiled Occupancy requirement 与 SourceBindingId、ProviderId、fixed world-space Sphere/Capsule/AABB/Sector、hard capacity 冻结为 canonical descriptor；`MaxMappedSubjects` 精确等于 compiled `MaxInputItemsPerEvaluation`，且 compiled `MaxActiveRelations >= MaxMappedSubjects`，保证合法满批不会先天地超过 relation authority capacity；Ray/OBB/default、contract/filter/geometry/capacity mismatch 在初始化期 fail-closed。
2. catalog hash 覆盖实际生效 query header、IncludeSourceEntity、shape raw field 和 capacity；与 compiled `GameplayTriggerCatalogHash` 合并为 composition/session 可消费的 `CombinedCatalogHash`，但不虚假声明已进入 network wire。
3. `GameplayTriggerCombatSensorSystem` 固定为 Gameplay `Simulation/950`，严格早于 `GameplayTriggerEvaluationSystem` 的 `Simulation/1000`；直接使用 `GameplaySystemContext.Frame`，不换算 CombatFrame。
4. 每个启用 source/frame 在 query 前验证冻结的 `GameplayComponentWorld` identity 和 `ColliderCount <= MaxQueryHits`；MaxQueryHits 是 caller scratch hard bound，不是结果截断上限。
5. 单次 Query 结果先按 CombatEntityId 去重，再通过现有 generation-safe `CombatEntityGameplayMap` 的只读 seam 映射；unmapped/stale 只过滤并计数，invalid mapped ID/capacity/submit reject 整 source 原子失败。
6. GameplayEntityId 二次去重并原地 canonical sort 后，至多一次提交 complete occupancy；empty 同样提交，Combat hit distance/body/collider 顺序不影响 occurrence/activation/action 顺序。
7. 初始化期预分配 List/数组/diagnostic rows，Combat broadphase 独立 warm-up 后 Tick 保持 NoAlloc。底层 Query 不提供 recoverable typed fault；意外 invariant exception 保持 submit 前 fail-fast，不用 catch 作为热路径控制流。

Combat fixed-step/contact/impact 的领域语义仍由 Combat 拥有。Bridge 只在明确的 RuntimeFrame 边界提交规范 observation，不把 `CombatFrame` 偷换成 `RuntimeFrame`，也不声称支持未定义的 substep trigger 语义。

bridge 不保存第二份 occupancy authority，Trigger membership/presence/ActivationId 仍由 Trigger SaveState/hash 拥有；derived sensor observation 不进入 exogenous replay。in-place restore 后，组合根必须在 exclusive boundary 调用 `ResetSchedulingState()` 清除 system 的 non-authority monotonic frame guard和 typed diagnostics，再从下一合法 RuntimeFrame 重采样。恢复的 active relation 因此继续产生 Stayed，而不是重复 Entered；回放组合不得同时启用 live sensor。

### 7.3 External And Unity Sources

#732 已交付 Unity Physics occupancy observation adapter。它只把 callback 合并为 transient contact level，再由持久 `PreSimulation/0` module 对每个 live + enabled source/frame 编码一个 complete occupancy batch，通过 Exogenous `RuntimeCommandSubmissionGate.SubmitBatch(...)` 在 Gameplay `Simulation/100` 唯一 drain 前原子进入现有 command path；不新增 provider accumulator，也不改变 Trigger reducer/hash/save/replay schema。adapter 不能：

- 在 `OnTriggerEnter` 中直接调用 action；
- 保存 Collider/GameObject 为权威 membership；
- 使用 callback 到达顺序决定 winner；
- 绕过 command authorizer、replay 或 frame policy。

adapter 先把显式 authoring Collider 映射为 generation-safe `GameplayEntityId`，flush 时按 logical subject 去重并 canonical sort。每个 zone 只允许一个冻结 SourceBindingId，遍历顺序来自 immutable Unity catalog，不来自 callback 或注册顺序。OnDisable/Destroy/scene unload 必须调度 final empty retirement，不能等待一个可能永远不来的 Exit callback；持久 module 在组件死亡后继续 flush，atomic admission 失败则下一帧重试。source disabled 时保留 live contact，重新 enabled 的下一帧提交当前完整 level；已经 retiring 且 source disabled时可无提交回收。Replay playback 使用 `ReplayDisabled`，保留 lifecycle bookkeeping 但禁止 live submission。

同一 pair 同帧多次 callback 先合并为最终 level state。即使最终状态为空，adapter 仍提交 explicit empty complete batch；Runtime 从上一 evaluation level 与当前 level 派生 Entered/Stayed/Exited。如果一次短接触在同一 evaluation boundary 前恢复原状态，level reducer 不产生 occurrence；需要保留该事实的领域（例如 impact）必须提交显式 Signal，而不是依赖 callback 次数。正式确定性玩法仍优先选择 Combat bridge；Unity adapter 主要用于外部事实接入、迁移和非确定性 host 场景。

### 7.4 Domain Facts And Timers

同帧领域事实不得通过 presentation EventBus 回流 authority。产生事实的 domain system 应通过显式 Trigger Signal bridge 写入预分配输入 buffer，并在 pipeline 中声明早于 Trigger evaluation。

cooldown、stay interval 和 delayed internal mutation 直接使用 `RuntimeFrame` 字段，不创建通用秒制 timer。独立“定时触发器”可桥接 `IRuntimeTimerScheduler`，但 authority 路径只能使用可序列化的 `ScheduleCommand`，`delay=0` 归一为 next frame，repeating interval 必须大于 `0`；在 scheduler 的完整 Save/Restore 和 replay ownership 明确前，它不进入 v0 authority claim。

## 8. Deterministic Evaluation Lifecycle

每个 RuntimeFrame 的目标顺序：

```text
GameplayRuntimeModule drains RuntimeCommandBuffer once
  -> PreCommand: apply deferred entries whose DueFrame == current frame
  -> Command: consume external trigger commands
  -> Simulation: sanitize stale owner/subject state after all Command systems
  -> Simulation: sensor/domain bridges submit observations and signals
  -> Simulation: normalize + reduce occupancy/threshold/signal into occurrences
  -> Simulation: conditions + activation policy + arbitration
  -> Simulation: preflight, commit activation, append action intents
  -> Resolution: target bridges route intents into typed domain requests
  -> Resolution: target domain systems consume requests
  -> Resolution: trigger stale/lifetime cleanup
  -> Resolution: Gameplay entity lifecycle cleanup
  -> Diagnostics: capture typed summaries/events
```

Phase 19.2G1 固定 `GameplayTriggerDeferredSystem` 为 `PreCommand/priority 800`、既有 `GameplayTriggerCommandSystem` 为 `Command/priority 900`。due lifecycle 在 PreCommand 应用；due Signal 只 stage/tag，并由 Command system 在既有 external command list 位置 merge/finalize，因此 `[Signal, Reset]`、`[Reset, Signal]`、`[Signal, Disable]`、`[Disable, Signal]` 继续服从 command total order，而不是把 external Signal 提前到 PreCommand。Deferred entry 永久保存 `OriginalDueFrame + stable sequence`；注册顺序扰动不能改变 phase/priority 结果。

排序分层定义，不能把 route order 混进 source occurrence identity：

```text
OccurrenceKey =
  InstanceId(index, generation)
-> SubjectEntityId(index, generation; empty first)
-> InstigatorEntityId(index, generation; empty first)
-> OccurrenceKind(stable enum value)
-> SignalId / scalar raw value / source payload
-> ProducerOrder
-> OrderedSequence or CanonicalOrdinal

CandidateIdentityKey =
  DefinitionId
-> RouteId
-> OccurrenceKey

WinnerTieKey = CandidateIdentityKey

ExclusiveWinnerKey =
  WinnerPriority(descending)
-> WinnerTieKey

AcceptedExecutionKey =
  ExecutionOrder
-> CandidateIdentityKey
```

输入枚举顺序、Dictionary 迭代顺序、Unity callback 顺序和 DebugName 均不得改变结果。

### 8.1 Source Truth Transaction

source reducer 与 route/action 是两层事务：

| Failure point | Source state | Route state / action |
| --- | --- | --- |
| 输入校验、normalization、occurrence/candidate build capacity 失败 | 整批不提交，保留旧 state | 不评估 |
| 输入已接受并完成 reduce | 新 membership/threshold truth 提交一次 | 继续评估 routes |
| condition/cooldown/arbitration reject | 不回滚 source truth | 不消耗 count/cooldown |
| core intent/outbox reservation 失败 | 不回滚 source truth | activation reject，不消耗 count/cooldown |
| target dispatch/semantic reject | 不回滚 source truth | activation 已发生，只记录 result |

不能通过回滚 membership 来“下一帧重造 Entered”。v0 不自动重试 rejected activation；若未来需要 retry，必须保存明确的 pending activation，而不是伪造 occurrence。

### 8.2 Lifecycle Rules

- duplicate level/contact command 幂等，不重复产生 Entered/Exited。
- `Stayed` 只在完整 observation/sample 确认仍 active 时产生，并由订阅它的 Route 独立控制 interval。
- disable 默认 `ClearSilently`：清空 occupancy/threshold active state，不执行 Exited route；可显式选择 `EmitExitThenClear`。无论 Exited route/action 是否接受，disable 和 clear 都必须完成。
- occupancy re-enable 后，下一份完整 observation 中的 Subject 重新产生 Entered；threshold re-enable 后的第一份 sample 重新执行 source 的 `PrimeSilently` / `EmitEnteredIfActive`，不另藏默认行为。
- owner stale 时始终 silent 销毁 instance；subject stale 时始终 `RemoveSilently` 并记录 reason。`EmitExited` 只适用于 complete source departure、disable 或 entity 仍有效的 staged pending-destroy，不对已经 stale 的 handle 运行 Route。
- reset 清除 route count/cooldown、source state 和 pending state；若 reset 允许保留 source state，必须使用不同 command，不用布尔参数制造歧义。
- 同帧递归触发禁止。Trigger action 引起的 enable/disable/reset/signal 进入内部 deferred queue，最早在下一 RuntimeFrame 应用。

Command phase 可能已销毁 entity，因此 stale sanitation 必须在所有 Command systems 之后、任何 sensor/reducer/route 之前执行，并拒绝/清理 pending input 中的 stale entity handle；这样 stale Subject 不会先被误判为 Exited，也不会借同帧 Signal 参与条件。需要 Exited 语义的销毁流程必须先进入仍可寻址的 staged pending-destroy，让本阶段生成 occurrence。

Resolution 后的第二次 cleanup 始终 silent，只关闭 state 并发 diagnostics，不生成已错过 Route pass 的 occurrence。若未来要求该时点 EmitExited，必须新增带 `DueFrame + sequence`、参与 hash/save 的 `DeferredLifecycleOccurrence`；v0 不做隐式延后。

## 9. Conditions

Trigger condition 是只读 predicate，不拥有副作用。它不直接复用 `IModifierCondition`、Story condition、Runtime AI Planner condition 或任意委托，因为这些接口的 context、生命周期和依赖不同。

Authoring 条件树支持：

- `All` / `Any` / `Not`；
- occurrence kind、signal id、owner/subject/instigator 是否存在；
- previous/current scalar、threshold direction 和 source active state；
- 通过显式 adapter 注册的 typed leaf predicate；
- 数字 payload 和稳定 tag/team/status/profile ID 比较。

加载时编译为扁平 postfix/bytecode-like program；Tick 使用预分配 evaluation stack。Leaf evaluator 概念接口只读：

```csharp
bool Evaluate(
    in GameplayTriggerConditionContext context,
    in GameplayTriggerConditionInstruction instruction);
```

禁止运行时解析 JSON、字符串表达式、反射查找、LINQ、闭包和异常流程控制。condition program 有 instruction/depth budget；未知 kind 或缺失 adapter 在 catalog load 时失败，不能运行时默认为 false 后静默继续。

cooldown、次数、presence limit、stay interval 和 exclusive arbitration 是 activation policy，不伪装成 condition leaf。

## 10. Activation Policy And Arbitration

每个 Route 的基础 policy：

```text
StateScope             PerInstance | PerSubject | PerInstigator | PerPair
MaxActivations         0 = unlimited
CooldownFrames
PresenceLimit          Ignore | OncePerPresence
StayIntervalFrames
```

`PerPair` 表示 `(Subject, Instigator)`；Instance/Route 始终隐含在 state key 中。选择 Subject、Instigator 或 Pair 时，对应实体必须存在且 generation 有效。

`MaxActivations` 是对应 state key 的 lifetime count，不在 Exited 时重置。`OncePerPresence` 使用显式 epoch：

- source 为每个 Subject 保存 presence epoch；
- `PerInstance` 使用 active count 从 `0 -> >0` 时递增的 aggregate presence epoch，直到回到 `0` 才结束；
- `PerSubject` 使用该 Subject 的 epoch；
- Exited route 仍看到正在结束的 epoch，随后 source 关闭它；
- Pulsed 或不能提供所需 presence key 的 source 不允许 `OncePerPresence`；
- Route 保存 `LastActivatedPresenceEpoch`，下一次 Entered 创建新 epoch 后自然 re-arm，不重置 lifetime count/cooldown。

这个组合可表达：

- once per instance；
- once per subject；
- once per presence（`MaxActivations=0 + OncePerPresence`）；
- cooldown repeat；
- periodic Stay；
- Pulsed-only rule。

默认 arbitration 为 `Independent`：所有合格 candidate 都按稳定顺序 activation。只有 `ExclusiveGroupId != 0` 才竞争 winner：

1. 按 `ExclusiveGroupId + ArbitrationScope key` 分组；缺失所需 Subject/Instigator 的 candidate 在 validation/evaluation 阶段结构化拒绝；
2. `WinnerPriority` 高者胜；
3. 同优先级使用第 8 节定义的完整 `WinnerTieKey` 取稳定 winner；`ExecutionOrder` 不参与 winner，只在 winner/independent candidate 接受后使用 `AcceptedExecutionKey` 排序；
4. loser 产生 `Suppressed` diagnostics，但不消耗 count/cooldown。

exclusive group 不提供跨 action 的锁或分布式事务，它只仲裁同一 evaluation batch 的 activation candidate。

## 11. Action Intent And Dispatch

Route 的 action program 是稳定顺序的只读 instruction：

```text
ActionKindId
TargetSelector         Owner | Subject | Instigator | ExplicitBindingTarget
TargetSlotId           required only for ExplicitBindingTarget
Payload0..N            fixed typed numeric/id payload
```

`ActionKindId` registry—not authoring data—declares one delivery contract：`SameFrameReserved`、`ReliableDeferred` 或 `PresentationOnly`。未注册或目标基础设施不满足 contract 时，catalog load 失败。

Core 不执行任意回调。它把 accepted activation 物化为 `GameplayTriggerActionIntent`：

- frame、ActivationId、route ID、action index；
- definition/binding/instance/source occurrence；
- owner/subject/instigator；
- ActionKindId、resolved target role、payload；
- trace/source numeric identity。

一次 activation 的提交规则：

1. 统计全部 action intent。
2. 按 stable sink/action order 执行 `Prepare`：为 core intent queue、reliable outbox、trigger-local deferred queue 和 `SameFrameReserved` sink 取得一次性 token。
3. prepared token 在 `Commit` 前对 consumer 不可见；只查询“剩余容量”不算 reservation，避免 Simulation 到 Resolution 的 TOCTOU。
4. 任一 Prepare 失败，按反向 stable order 对此前 token 执行确定性、幂等 `Abort`，然后 activation `Rejected`；不更新 route count/cooldown，也不写部分 intent，已提交的 source truth 不回滚。
5. 全部 Prepare 成功后分配 ActivationId、提交 route state，再统一 `Commit` tokens 并按 action index 发布 intents/outbox records。Commit 必须是预验证后的 no-fail/no-alloc 操作；异常属于 invariant failure。
6. target bridge 只做 typed validation/enqueue；目标域拥有最终执行结果。

Trigger 无条件保证的是“整批发布到 core-owned intent/outbox”；只有实现 consumable reservation token 的 same-frame sink 才参与 activation commit。Phase 19.3C1 已为 `GameplayBuffRequestBuffer` 增加独立 fixed reservation lane，并由 `GameplayTriggerGameplayBuffActionSink` 完成 Apply/Remove same-frame transaction；参数less legacy queue 语义不变。`RuntimeEventQueue<T>` 和其他仍自动增长的容器没有 reserve contract，不能被文档直接冒充为可预检 sink；对应 action 必须先增加 bounded/reservation contract，或改为 reliable deferred。

Trigger 不承诺 Buff + Story + VFX 跨模块原子回滚。目标域拒绝 stale target 或非法 payload 时，activation 仍然发生，但必须返回带 `ActivationId + action index` 的结构化 result；不能悄悄撤销、重试或自动 disable/exhaust Trigger。若目标结果需要影响 Trigger authority，目标域必须在下一 frame 提交一个新的显式 Signal，并遵守同样的 command/replay/save 规则。

Core 可内置的 action 仅限 deferred trigger-local mutation：enable、disable、reset、signal another trigger。Buff、Attribute、Story、Resource、VFX、Audio、Camera 和 UI action 都在 target-owned bridge。

## 12. Frame Ordering And Same-Frame Policy

`GameplayRuntimeModule` 是 Gameplay command buffer 的唯一 drain owner。Trigger systems 复用已 drain 的 `GameplaySystemContext.Commands` 并用现有 execution state 标记 command；禁止另建 Runtime module 抢同一 buffer。

当前 #679/#681/#686 已实现这条 integration spine：`GameplayTriggerCommandSystem` 在 Command phase 消费 generation-safe `SetEnabled` / `Reset`，并从 fixed envelope records 原子重组完整 Signal、occupancy 和 scalar batch；`GameplayTriggerEvaluationSystem` 在 Simulation phase 调用一次 `Evaluate(frame)`。两者不持有 `RuntimeCommandBuffer`。#684 增加 opt-in trusted source gate 与 exogenous replay filter；#688 增加 catalog/authority hash schema 与 Runtime contributor；#690 增加通用 RuntimeHost completion/quiescence checkpoint barrier；#692 增加 Trigger canonical SaveState/checker/atomic restore participant；#696 增加 target-agnostic reliable outbox module；#698 增加 bounded Event Router；#700 增加完整 Trigger replay playback/bootstrap；#720 增加 19.3A deterministic config compiler；#723 增加 19.3B `Simulation/950` deterministic Combat sensor bridge；#725 增加 19.3C1 `Simulation/1000 -> Resolution/50` Gameplay Buff reserved bridge；#728 增加 19.3C2 `PreSimulation/0 -> Gameplay Simulation/100` reliable component Destroy/Attribute/Ability command bridge；#730 增加 19.3C3 reliable Story RaiseTrigger bridge；#732 增加 `PreSimulation/0 -> Gameplay Simulation/100` Unity Physics complete occupancy ingress；#736 增加 19.4A checkpoint/door/trap Playable；#739 增加19.4B bounded protocol-2 authority、snapshot/rollback/resync与scale validation。通用Unity action bridge仍待独立后续切片。

drain 完成后 buffer 已前进到下一 frame，因此每个 action kind 必须登记明确 delivery：

| 目标/action kind | Delivery | 当前前置条件 |
| --- | --- | --- |
| trigger-local enable/disable/reset/signal | internal deferred，最早 next RuntimeFrame | core queue 自身可 reservation |
| Gameplay Buff Apply/Remove | same RuntimeFrame `Simulation/1000 -> Resolution/50` | 19.3C1 fixed reservation lane + terminal result relay 已实现 |
| Gameplay component Destroy / Set/Add Attribute / direct Ability | reliable deferred；F 写 outbox、eligible G 使用 current-G RuntimeCommand，同 Tick 稍后 drain | 19.3C2 bounded TrustedDerived gate + exact consumer identity + terminal result relay 已实现 |
| Story 或其他已运行 Runtime module | reliable deferred -> `frame.Next()` target command | pre-target bridge + replay provenance |
| VFX/Audio/Camera/UI presentation | Router fanout；consumer 若本帧已运行则下一 presentation frame | 不参与 authority，允许 observer-local drop diagnostics |
| 其他 target | action-kind-specific | 必须声明 consumer phase、reservation/outbox、failure result |

尤其是当前 Story 通常早于 Gameplay 运行，Gameplay Trigger -> Story 默认必须进入下一 RuntimeFrame。任何 bridge 若宣称 same-frame，必须证明目标 consumer 尚未运行并有 focused ordering test。

### 12.1 Reliable Cross-Module Delivery

Trigger activation 不能直接把 next-frame command 丢进一个未被 SaveState 捕获的 target buffer。`ReliableDeferred` 使用 transfer-of-ownership 协议：

1. frame `F` activation 把完整 action envelope 写入 Trigger-owned、hash/save/rollback 的 outbox，`OriginalDueFrame = F + 1`。
2. 从 frame `F + 1` 起，任意 current frame `G` 只要满足 `OriginalDueFrame <= G`，target bridge 就把 record 视为 eligible。它作为早于目标 module 的 Runtime module，只在组合根确认目标已注册、enabled 且会在本 Tick 稍后 drain 时尝试投递。
3. 只有 target buffer 接受后才从 outbox 移除；目标缺失/disabled、buffer 拒绝或 bridge 未运行时 record 保留并发 diagnostics。
4. record 永久保留 `OriginalDueFrame` 并按 `OriginalDueFrame + stable sequence` 排序；每次派生的 target command 使用 target buffer 的 current frame `G`，绝不重用过期的 F+1，否则会被判 LateCommand。
5. bridge transfer 与 target drain 必须发生在同一个完整 `RuntimeHost.Tick` 内；checkpoint 只允许在整帧结束 barrier，因此不存在可存档的“已移除但未消费”半状态。
6. 默认可靠策略无限保留；若 action kind 显式声明 `DeliveryDeadlineFrame/RetryBudget`，到期后产生 permanent-failure result 并移除，deadline/budget 必须进入 envelope/hash/save。attempt diagnostics 本身不进入 authority。
7. target semantic reject 是带 ActivationId/action index 的结果，不把 record 自动塞回 outbox。

这些派生命令使用注册的 Derived source/provenance，并明确排除在 replay 的 exogenous input 集合之外；replay 会从先前 activation/outbox 再次派生它们。Phase 19.2G2 的 production seam 保持 target-agnostic；具体 transfer adapter 必须自己通过 `RuntimeCommandSubmissionGate(TrustedDerived)`、derived source allowlist 和 target-owned bounded buffer 投递。Phase 19.3C2 已为四个 Gameplay component command 交付 production adapter：source 精确 allowlist 四个 ID，buffer 有 immutable hard pending bound，transfer 构造验证 gate/module buffer 与 consumer concrete type，运行时再验证 Host/module/system exact instance 和 enabled 状态；envelope `1002104/v1` 保存 activation/action/outbox sequence/origin frames。semantic result 由 Gameplay-owned flat numeric sink 发布，strict relay 只产生 presentation `ActionResult`，不形成 Gameplay 到 Trigger core 的反向依赖。Combat/Story 等其他 target codec 仍未由此交付。

v0 reliable outbox 明确要求 authority `RuntimeHostErrorPolicy.FailFast`。target buffer 接受后若 target Tick 抛错，当前 session 视为不可继续，supervisor 必须丢弃它并从上一 checkpoint + replay 重建；`CollectAndStopFrame` / `CollectAndContinue` 不能与该 delivery guarantee 组合。未来若要错误后继续运行，必须另行引入持久 idempotent acknowledgement ledger 或真正的整帧 rollback，不能继续沿用“接受即移除”。

## 13. Hash, Replay, SaveState And Network

### 13.1 Authority Hash

Phase 19.2E 已注册 contributor contract：

```text
ContributorId = mxframework.gameplay.triggers
```

成功创建 `GameplayTriggerCatalog` 时会缓存 schema v2 的三个 current hash：

- `DefinitionCatalogHash`：source 的 typed identity/reducer 参数、definition/version/source reference、每条 route 的 ID/mask/condition instructions/policy/arbitration/actions，以及 external sink 与 internal descriptor count/kind/delivery/operation/SignalId；
- `BindingCatalogHash`：binding/definition/source-binding identity、owner、initial enabled 和 explicit target slots；
- `GameplayTriggerCatalogHash`：schema + 上述两个分项 hash。

source、definition、route、binding、target、external sink 和 internal descriptor 作为 typed-ID set 使用 catalog freeze 后的 canonical order；condition instruction 与 action declaration order 是规则语义，不做 set-sort，交换顺序必须改变 definition hash。`TryCreate` 在排序/校验前一次捕获 external sink 的 `ActionKindId + DeliveryContract` 并复制 internal descriptor，后续 registry lookup 与 hash 只读 frozen entry；live getter mutation 不改变已冻结 identity/contract，也不会注册新的 kind。sink 对象实例身份不进入 hash，只保留引用用于 action dispatch。只有不含 internal descriptor 的 catalog 保留 internal legacy-v1 hash，用于 schema-1 SaveState migration，不作为 current public identity。

Phase 19.2G2 把 contributor state schema 升为 3，并按稳定顺序写入：

- authority schema、三个 current catalog hash 和十二项 fixed-capacity Runtime options；
- `NextActivationId`、`NextDeferredSequence`、`NextReliableOutboxSequence`、是否至少完成过一次 Evaluate、最后成功 Evaluate frame；
- 固定 `InstanceId = index + 1` 顺序下的 instance ID/generation、binding/source-binding/definition/version/resolved source-definition、owner、enabled、active count 和 aggregate presence epoch；
- 按 `(InstanceId, Subject)` 排序的 relation：active/primed、scalar raw value、presence epoch/start frame；
- 按 `(InstanceId, RouteId, Subject, Instigator)` 排序的 route policy state：activation count、activated marker/frame/presence epoch 和 Stay schedule。
- committed deferred count 与天然保持 `(OriginalDueFrame, Sequence)` 的完整 record 字段：operation、origin activation/action/kind/binding/route、target instance/source binding、enabled、SignalId、Subject/Instigator、Payload0..2。
- committed reliable count 与天然保持 `(OriginalDueFrame, Sequence)` 的完整 record 字段：origin activation/action/kind/binding/route/instance、完整 occurrence、resolved target、owner/subject/instigator 与 action payload。

relation 与 route-state canonicalization 使用 contributor 初始化期按固定 capacity 分配的 index scratch，再做原地 iterative heapsort；`Contribute(...)` warm-up 后不分配。instance backing order 已由当前静态实例分配规则保证，不创建无意义的 instance scratch。内部 typed row seam 只读取 immutable authority projection，不提供 public mutable runtime 入口。

Phase 19.2E 明确不写 occurrence/activation/intent/diagnostic/current pending input 等 Evaluate 瞬态输出、sink 对象身份、event queue、trace/presentation/Unity identity 或容器原始 relation/route 插入顺序；19.2G2 继续排除未提交 reservation、tagged staging、transfer enabled/target module live state、attempt status/detail、in-flight marker、`LastDeferredExecutionResult` 与 `LastReliableOutboxExecutionResult`。hash 只能在一次完整 `Evaluate(frame)` 或承载它的完整 Runtime Tick 成功结束后的 quiescent boundary 读取；schema-3 participant 保存相同 authority projection，但不保存这些瞬态输出。

后续 dynamic instance 切片必须在再次升级 schema 后继续写入：

- dynamic instance allocator next index/generation 与运行时可变 binding targets；
- 新增 authority capacity/config revision。

不写 event queue、diagnostic text、trace text、presentation state、Unity instance ID、系统时间或容器原始迭代顺序。

### 13.2 Replay

- 玩家、网络、Unity Physics、动画 callback、外部脚本等非确定输入必须先成为 replayable command + typed envelope。
- Combat/world 可从相同初始状态确定性重算的 observation 不重复录制。
- conditions、occurrence、activation、internal deferred mutation、action intent、outbox 和 bridge 生成的 derived command 是派生输出，不重复写成 replay input。
- replay frame input 只包含 source registry 标记为 exogenous 的 command/envelope；derived source range 必须被 recorder 过滤并由 authorizer 防伪。
- typed envelope 必须随 replay frame 复制正文并在回放时重建，不能只记录 frame-local handle。
- replay 从包含 Trigger bootstrap/SaveState 的一致初始状态开始，逐帧重放外部 commands 并核对 result hash。
- 必测：同一 occupancy/unordered signal multiset 以不同枚举顺序提交，activation event 顺序和 hash 相同；Ordered Signal 则按被录制 sequence 稳定复现。

Phase 19.2C/19.2D 当前实现边界：`RuntimeCommandSourceRegistry` 在初始化期复制并 seal SourceId/classification/command allowlist；External 与 TrustedDerived gate 对 unknown source/command、classification/provenance mismatch fail-closed，且不修改 legacy `RuntimeCommandBuffer.Enqueue`。source allowlist 可显式加入 Signal、occupancy 和 scalar command id；`RuntimeReplayRecorder.CreateExogenousStream(...)` 只复制 registry 认可的 Exogenous fixed records，并保持 caller 顺序、envelope value copy 与 caller mutation isolation；旧 recorder 构造显式保持 `LegacyUnfiltered` 兼容。

Phase 19.2H2 以 `GameplayTriggerReplayHeaderContract`、`GameplayTriggerReplayBootstrapOptions`、`IGameplayTriggerReplaySessionFactory` / builder 和 `GameplayTriggerReplayFrameDriver` 关闭完整本地 playback/bootstrap gate：

- header 固定 schema 1，并把 combined Trigger catalog hash 格式化为 `mxframework.gameplay.triggers.catalog/v2/{hash:x16}`；framework/resource/config/start frame 任一不匹配都在 factory 前拒绝。
- `Fresh` 从空 authority 开始；`CheckpointSuffix` 只接受 end-of-F checkpoint 和 start F+1，并固定执行 `Initialize -> Start -> atomic restore -> presentation reset -> Ready`。
- factory 必须交付 FailFast Host、唯一 Gameplay module、同一 Trigger runtime 的 Deferred/Command/Evaluation/Router、可选 reliable outbox、sealed source registry、restore coordinator，以及精确 ID/phase/priority/capacity/start-frame identity；相同 ID 替身和错误 runtime identity 不能通过。
- frame 必须连续；`long.MaxValue-1` 为最后可 Tick frame。每帧先完整验证 command count/order/sequence、Exogenous source/provenance、三类 typed command、batch completeness/ItemIndex/ordered Signal sequence/source kind/typed maximum/runtime capacity/每 source 唯一提交，再原子 admission、一次 Host Tick 和 Trigger authority hash。
- admission 失败恢复 command-buffer snapshot，session fault/dispose，不产生部分 submit/Tick/hash。generic runner 继续负责 expected/actual hash 比较，并保留 failure frame、commands 或 typed driver exception。
- snapshot 只含可信 exogenous observation。CommandResult、occurrence、activation/action、H1 presentation、G1/G2 queue/transfer 与 TrustedDerived target command 由 authority/checkpoint 重建；observer 容量、enabled/overflow 不影响 replay hash。
- initialized Host Tick subregion 在 steady authority、due internal deferred、due reliable outbox 与 Router 三类 64-Tick measured workload 中保持 0 B；Reset/overall RunFrame snapshot/result/hash 仍是低频 `AllocByDesign`。

### 13.3 Save / Restore

Phase 19.2G2 在 19.2G1 基础上把 current module/custom/payload schema 升为 3：

```text
ModuleId      = mxframework.gameplay.triggers
TypeId        = mxframework.gameplay.triggers.state
SchemaVersion = 3
```

restore 仍接受 module/custom/payload 三者完全一致的 legacy schema 1/2；旧字段形状必须精确匹配，迁移时注入 empty reliable outbox 与 `NextReliableOutboxSequence=1`。legacy payload 显式夹带 v3 字段、mixed version 或任一 hash/config mismatch 均 fail-closed。

只允许组合根在完整 `RuntimeHost.Tick` 成功结束后的 checkpoint barrier 捕获。Phase 19.2F1 已增加 `RuntimeCheckpointBarrier`，在调用旧 coordinator 前按 `Order -> ParticipantId ordinal` 核验注册的 `IRuntimeCheckpointQuiescenceChecker`：

- 没有未归约 input、未提交 core intent、未消费 reservation token；
- 没有 outbox -> target buffer 的 transfer in flight；
- target command/request buffer 中没有未被其 SaveState owner 捕获的 Trigger delivery。

`RuntimeSaveStateErrorCode.PendingWork = 1401` 与 `RuntimeSaveStateCoordinatorPhase.Checkpoint = 3` 已冻结。Host 未 Tick、Tick in-progress、最近 attempt 失败均 fail-closed，且不会回退更早成功帧；host invalid 时不调用 checker/provider。Trigger checker 分类固定为真实 input batch/items `1..4` -> uncommitted internal/reliable reservation `5` -> tagged Signal finalization `6` -> reliable transfer in-flight `7`。committed future/overdue G1/G2 queues 都是 schema-3 authority，Ready 且正常捕获；只有 reservation/transfer 的瞬态窗口阻止 capture。

Barrier 只有在全部 ready 后才调用 `RuntimeSaveStateCoordinator.CaptureSaveState()`；coordinator failure 保留原 errors 但 barrier 返回 `Value=null`，不发布 partial value。成功后只把根 `RuntimeSaveState.Frame` 归一为最近完整成功帧 F，其余 header/body/metadata 只读复制。旧 coordinator direct capture 保持兼容，但不具备 authority checkpoint completion/quiescence 保证。

current schema 3 保存 combined GameplayTriggerCatalogHash（并保留两个分项 hash）、十二项 runtime options、activation/deferred/reliable allocator continuation、static instances、source/presence state、route policy state 和完整 deferred/reliable records；不重复保存 definition/binding 正文。两类 due record 都验证 continuation frontier、canonical order、origin action/instance/binding/route、target shape、entity references 与 allocator range；`NextDeferredSequence` / `NextReliableOutboxSequence=long.MaxValue` 均是可恢复/hash 的 exhausted sentinel。restore 只验证冻结的历史 record，不把当前 relation presence 当作历史发生事实。

frame convention 固定为：完成 frame `F` 后捕获，`RuntimeSaveState.Frame = F`；恢复得到 end-of-`F` state，首个 Tick/ReplayStartFrame 是 `F + 1`，所以 `DueFrame = F + 1` 的 deferred/outbox record 会在恢复后的首帧处理。G1 的唯一自然提交路径在 `Evaluate(F)` 的 Commit 阶段写 `F+1`，因此 end-of-F queue 不可能含 `due > F+1`；restore 必须先守住该边界，才能保证恢复后新增 activation 仍按 `(due, sequence)` 追加，并使 capture -> restore 保持自洽。

Restore 流程：strict single-root/duplicate-aware parse -> schema/catalog/options -> capacity/key/range/invariant -> restored entity policy -> 建立完整 replacement plan -> no-fail commit。任何错误不允许半恢复；成功 commit 必须清空所有 Evaluate/deferred outputs 与 pending/reservation/staging transient，包括 public `LastDeferredExecutionResult`，且这些 transient 不得被序列化或哈希。validator false 返回 `UnknownEntity`，validator exception 保留异常并返回 `InvalidDocument`。Trigger participant 排在 Gameplay entity/component restore 之后；建议显式 world order 100、Trigger order 200，不能靠 ParticipantId 字典序。该原子性仅覆盖 Trigger 自己的排他、单线程 validate-plan-commit；不是 lock-free/thread-safe store，也不提供 coordinator 跨 participant rollback。

### 13.4 Network / Rollback（Phase 19.4B 已实现）

Phase 19.4B 以独立 `MxFramework.Gameplay.Triggers.NetworkBridge` sibling assembly把Trigger组合到既有Network/Runtime seam；Network core不反向引用Gameplay或Trigger，也没有创建第二个transport、host loop或JSON authority path。

- protocol 2精确协商后启用kinds 10..13；outer 52 B、wire schema 1、CRC field-zero计算，command/snapshot total limit分别8192/524288 B。record固定116 B且不含TraceId，command/authoritative/snapshot/request payload header分别104/120/124/56 B；protocol 1保留旧行为但不自动降级。
- client只提交profile allowlist内的Exogenous typed observation；transport/session在packet bytes外独立提供authenticated session、remote sender、local receiver和direction，runner先要求Connected并核对configured identity、metadata与header。CRC不提供sender认证；authority/snapshot ingress只用authenticated context做peer/ownership/server判断。raw lifecycle/control/action/result、Derived forgery、unknown/disconnected peer、forged header sender、wrong owner、schema/catalog/profile/frame/sequence/capacity错误都在Trigger mutation前原子拒绝。
- `GameplayTriggerNetworkProfile`保存三项Trigger catalog hash、十二项capacity、source policy、comparability与外部canonical `NetworkProfileHash`。
- outer sequence是每session、每方向、跨四种bounded kind的总anti-replay frontier；各payload semantic stream另有独立frontier。所有frontier与command gate admission一起commit，失败推进0项。
- snapshot registry固定Gameplay type `1002301`/order100与Trigger type `1002302`/order200。FullComparable必须为每个注册Gameplay component CLR store提供exact one codec；CLR type set在本地组合期精确比对，numeric TypeId/schema/order/offset/length/capacity在wire candidate期校验。全部participant成功prepare后才按order发布，失败不产生partial live state。
- end-of-F恢复从F+1继续；已提交G1/G2 queue随Trigger authority恢复并恰好执行一次，Combat/Unity observation和bridge request/result由suffix重新生成。参与预测的client使用bounded history rollback/replay：record先写满一个预分配frame scratch，全部participant capture成功后才覆盖ring slot和推进history；restore先prepare全部participant，再按`RestoreOrder -> TypeId`执行no-fail commit（Gameplay 100 -> Trigger 200），任一prepare失败则对全部participant幂等discard并保持全部live state不变。`PartialNonComparable` client不能声称full-world hash comparable。
- hash mismatch、missing history、catalog/schema/corruption/unauthorized snapshot与restore failure触发typed bounded resync；重复byte-identical snapshot幂等。steady capture/prepare/wire/rollback record为初始化后NoAlloc；Gameplay live publication沿用existing lifecycle restore，明确为LowFreqAlloc。

## 14. Config And Validation

建议 authoring/runtime schema：

```text
mx.gameplayTriggers.authoring.v1
mx.gameplayTriggers.runtimeCatalog.v1
```

Authoring 可使用 JSON；player runtime 优先消费编译 catalog/bytes，并保留受控 JSON fallback。Compiler 至少验证：

- duplicate/retired ID、missing definition/sensor/condition/action reference；
- condition depth/instruction budget；
- invalid occurrence/policy/target selector；
- missing binding target slot、unknown action delivery contract、same-frame sink 无 reservation capability；
- external Signal schema 超出 replayable envelope codec 或 ordering metadata 不完整；
- reliable outbox 已启用但 authority host 不是 `RuntimeHostErrorPolicy.FailFast`，或 target bridge 不早于 enabled consumer；
- zero-capacity 或 action count 超限；
- source hysteresis/initial sample policy 非法、Route scope 所需实体缺失、`OncePerPresence` 用于不支持相应 epoch 的 source，或 exclusive group scope 不一致；
- 无条件 trigger-signal strongly connected cycle 没有 finite activation budget，且未显式声明 `AllowRepeatingCycle + MinIntervalFrames > 0`；单纯 next-frame delay 不足以证明安全；
- Unity LayerMask、GameObject path 或任意方法名泄漏进 noEngine catalog；
- schema/version/hash 不匹配。

运行时 catalog 只含稳定 ID、预编译 instruction、数值 policy、索引和 hash；不含 editor GUID、prompt、localized text 或 Unity 对象。

## 15. Diagnostics And Failure Policy

Phase 19.2H1 使用独立 readonly numeric payload `GameplayTriggerRuntimeEvent`，不继续扩大通用 `GameplayRuntimeEvent`。transport 是显式 `GameplayTriggerRuntimeEventSource` fixed array：恰好一个 `GameplayTriggerRuntime` producer owner、恰好一个 `GameplayTriggerEventRouterSystem` destructive drain owner。Router 固定为 `mxframework.gameplay.triggers.events.route`、Gameplay `Diagnostics/1000`，不持有/消费 `RuntimeCommandBuffer`，不调用 observer callback。

source capacity `0` 合法；接受记录时才分配从 `1` 开始的 source sequence，并按 `(Frame, SourceSequence)` 原地稳定插入。`long.MaxValue` 可以作为最后一个 accepted sequence，随后 source 保持 exhausted sentinel；ActionResult payload/producer identity rejection 优先于 sequence exhausted，sequence exhausted 优先于 capacity。Router 在 frame G 只 fanout `Event.Frame <= G` 的 due prefix，future 保留；若 Router 已在 G 运行而后续 producer 写入 `Frame=G`，该记录保留到下一次 Tick 并作为 overdue 交付。

observer registry 在 constructor 内复制、按 ordinal observer id 排序并冻结；null/空/重复 id、null/重复/已绑定 buffer、未绑定 producer/已有 drain source 全部在 binding 前失败。每个 observer 是独立 fixed ring：full/zero/disabled 只增加本 observer 的 saturating dropped count，不阻塞其他 observer；disabled 保留旧 pending 但丢新 fanout，`Clear` 只清 pending。Router disabled/skipped 不 drain source、不制造 observer drop；重复 Tick 不重放已消费记录。

producer 不从 `Last*Result` 轮询重建，而在真实结果点 exact-once publish：canonical occurrence sort 后；activation policy/authority commit 后；每个 action 的对应 Commit 成功返回后；每次 runtime diagnostic（包括 legacy diagnostics buffer 已满）；每条 lifecycle command或每个 raw batch一次；每次 internal deferred result；每次 reliable transfer 实际返回。Commit/transfer 抛异常时不伪造 result。public seam 只允许经校验的 `TryPublishActionResult`，不能发布任意 internal kind/sequence/origin 字段。

当前 event kinds 固定为 `None=0 / Occurrence=1 / ActivationAccepted=2 / ActionDispatched=3 / RuntimeDiagnostic=4 / CommandResult=5 / InternalDeferredResult=6 / ReliableTransferResult=7 / ActionResult=8`；status 固定为 `None=0 / Observed=1 / Accepted=2 / Dispatched=3 / Succeeded=4 / Rejected=5 / Retained=6`；drop reason 固定为 `None=0 / SourceCapacityExceeded=1 / SourceSequenceExhausted=2 / ObserverCapacityExceeded=3 / ObserverDisabled=4 / InvalidActionResult=5`。payload 是 flat value fields，不含 string、delegate、Unity identity、sink/transfer object 或隐藏 reference；所有不适用字段为 default，所有 non-action event 的 `ActionIndex=-1`。各 kind 完整字段和 `OriginFrame / DueFrame / ProducerSequence` 语义以 `Docs/Interfaces/Gameplay.Triggers.md` 为 Current contract。

presentation source/observer pending、sequence、drop/high-water、Router/observer enabled 与 registry 不进入 catalog hash、authority hash、SaveState、replay 或 checkpoint quiescence。authority restore 不隐式清 presentation；bootstrap/rebuild 只能在 exclusive boundary 显式调用 `ResetPresentationState()`，清 pending/counters/high-water/sequence/exhausted，同时保留 producer/drain/observer bindings 和 enabled 配置。presentation overflow 从不回滚 Trigger authority，也不改变 activation、internal deferred 或 reliable outbox。

`CreateSnapshot()` 为 `AllocByDesign` 低频 API，可展示：当前 instance、membership、counts、cooldown 剩余帧、最后 occurrence/activation/result、capacity high-water mark 和 catalog hash。Debug UI 只能读 snapshot，不回写 authority。

Failure 原则：

- 非法外部 command：validator 拒绝，无状态变化。
- incomplete observation/sample：保留旧 source state，发 diagnostics。
- input normalization/occurrence/candidate capacity overflow：拒绝整批 source input；core intent/outbox reservation overflow：拒绝 activation。
- diagnostics/observer event overflow：不改变 authority，累计结构化 dropped count。
- 未注册 condition/action kind：catalog load 失败。
- target domain semantic reject：保留 activation，记录 action result。
- 内部 invariant 破坏：fail fast，由 Gameplay pipeline 报 system failure。

## 16. Performance And Allocation Contract

Trigger evaluation、membership diff、condition evaluation、arbitration 和 intent dispatch 都是 Tick 高频路径，目标分类为 `NoAlloc after initialization`。

强制约束：

- options 显式配置 definition、instance、membership、observation/sample、signal、candidate、core intent、outbox、deferred、event/observer 容量；
- host 启动后不自动扩容；capacity overflow 可重复、可诊断；
- same-frame target 只有 consumable reservation token，不允许 query-then-enqueue；现有无 reserve contract 的 request buffer 必须先升级；
- 使用预分配 contiguous buffers / relation store / stable index；
- 小集合使用稳定 insertion/radix-like sort，禁止每帧创建 comparer/array/list；
- 禁止 LINQ、反射、闭包、字符串查找/拼接、异常控制流和 Unity object lookup；
- 不为每个 contact/activation 创建 TraceId 字符串；
- Save/JSON、完整 diagnostics snapshot 是低频 `AllocByDesign`；
- replay recording 若复制 command/record，按现有 Runtime 契约视为 `AllocByDesign`，不能冒充默认 NoAlloc path。

v1 必须有初始化后 allocation test 和容量边界测试。若现有 Runtime/Gameplay hash 或 command 容器本身仍分配，报告必须区分 Trigger 自身分配与基础设施既有分配。

## 17. Bridge Contracts

| Bridge | 输入 | 输出 | 明确禁止 |
| --- | --- | --- | --- |
| Combat Sensor | binding + Combat query facts | complete occupancy | 直接执行 gameplay action |
| Domain Input | typed domain fact/sample | trigger signal/scalar buffer | 从 presentation event 反推 authority |
| Gameplay Buff（19.3C1 Current） | action intent | same-frame reserved Apply/Remove request + terminal ActionResult | 绕过 reservation、直接写 Buff store 或把派生 request/result 写入 replay/save |
| Gameplay Component Commands（19.3C2 Current） | action intent / reliable outbox | next-frame bounded RuntimeCommand + terminal ActionResult | drain 后提交 current-frame command、绕过 exact source/consumer composition 或把 derived command/result 写入 replay/save |
| Story | reliable outbox | pre-Story bridge 生成 next-frame command | 直接 step Director 或把 derived command 录成 exogenous input |
| VFX/Audio/Camera/UI | Router fanout 的 activation/result | presentation intent/event | 直接 drain source queue 或回写 trigger/gameplay state |
| Unity | callback/marker/authoring data | validated RuntimeCommand/input mapping | Collider/GameObject 成为 authority |
| Config | authoring catalog | frozen compiled registry | Tick 解析 JSON/反射 |

每个 bridge 文档都必须声明：依赖方向、输入权威性、frame delivery、容量、failure result、hash/save/replay 参与方式和分配分类。

Phase 19.3C1 的 Gameplay Buff bridge 固定 action kind `1001010/1001011` 和 exact numeric payload。Trigger Evaluate 的 Prepare 只在 Gameplay-owned fixed lane 留出不可见 request/ordinal；全部 action Prepare 成功后 Commit 才写 `ActivationId + ActionIndex + ActionKindId` correlation 并使 request 可 drain。Buff application 仍拥有 target validity、definition、duration、stack 和 store mutation，最终以 Applied/Refreshed/Removed/RemoveNoOp 或 typed rejection 回传 `ActionResult`；presentation overflow 不改变两个 authority。request/correlation/result 都是 derived transient，checkpoint suffix 从 authority 重新生成，不加入任何 hash/save/replay schema。

## 18. Migration Strategy

以下领域 trigger 暂不迁移：

- Story 的 `RaiseTrigger` 和 graph progression；
- Ability effect timeline；
- CharacterAction reaction rules；
- projectile impact/area pulse；
- Combat contact/impact sensor；
- VFX instance trigger/lifecycle。

它们可以成为 Signal producer 或 Action consumer，但继续拥有领域语义。

适合首批验证的公共用例：

- checkpoint：Enter + once per subject + SaveState；
- door/interaction zone：occupancy + condition + Story/GameFlow intent；
- trap：Enter/Stay + cooldown + Gameplay typed request；
- pickup：Enter + per-instance once + item acquisition intent；
- objective signal：Signal + condition + next-frame Story intent；
- 非角色 EffectVolume：Combat occupancy + periodic Stay + Buff request。

Area Confinement、aura 或高密度 contact 若已有更贴合领域的批处理/refresh system，不为“统一”而强迁；只有公共 Trigger 能保持语义、性能和验证 parity 时才迁移。

## 19. Test Strategy And Verification Matrix

测试不是实现完成后的附加验收，而是本 authority contract 的一部分。Trigger 的状态、排序、失败语义和跨帧交付只有在测试 oracle 唯一时才算定义完成；若同一输入可以被两个“都合理”的测试解释接受，对应规范仍是 Draft。

### 19.1 Test Oracle And Reference Model

Phase 1 必须先建立一个允许分配、实现直白的 pure C# reference model。它与生产 relation store、reducer、policy、arbitration 和 dispatch 实现分离，不能复用被测实现的排序、状态更新或 reservation 代码。reference model 只服务测试，不进入 Runtime、player build 或公共 API。

当前 #670 已为 occupancy、threshold 和 unordered Signal 建立 test-only reference state/reducer，并用固定 seed permutation/duplicate、反向 relation 插入历史、High/Low raw-unit threshold policy matrix 对照生产 occurrence 的全部公共字段；recursive-condition reference 以独立 tree evaluator 对照 postfix program 和真实 route eligibility，覆盖 literal、Not/All/Any 四行真值表、匹配/不匹配 mask、有效/无效 entity、全部 typed leaf、Fix64/payload 边界及六种 comparison，并用 Threshold `previous=-2 raw -> current=+3 raw` 验证真实非零 scalar eligibility；activation-policy reference 已独立覆盖四种 state scope、count、cooldown、stay interval、once-per-presence、`InstanceId + RouteId` 隔离和 PerInstance aggregate presence epoch；exclusive-arbitration reference 已独立覆盖 Independent、四种 arbitration scope、priority、Definition/Route/Instance/Occurrence 完整 WinnerTieKey、ExecutionOrder 解耦、condition-ineligible 排除、loser 不消耗 policy/reservation 和缺失 scope key；activation-transaction reference 已覆盖 preflight reject 不提交 authority、Prepare 第 N 项失败后的严格逆序 Abort、最后一个 sink slot 的竞争，以及 Commit invariant exception 下已提交 authority 的 fail-fast 语义；lifecycle reference 以独立 list/dictionary state 覆盖 duplicate contact canonicalization、initially-disabled、幂等 `ClearSilently` disable、reset 清 source/policy 但保留 ActivationId sequence、re-enable 新 presence、stale/default/out-of-range handle 拒绝，以及 submit-before-disable/reset 按实例丢弃 pending input 且保留其他实例 batch；生产 runtime 对 occupancy/scalar/signal pending buffer 做原地压紧，并修正了 disable 后旧 pending state 回写的缺陷；current-kernel authority projection 已用 internal NoAlloc friend read view + test-only canonical adapter 覆盖 instance/source relation、active count、aggregate/presence epoch、route policy、occurrence、activation、intent 和 ActivationId allocator，并在输入 permutation/duplicate、reservation reject、Threshold + disable、PerInstigator/PerPair、OncePerPresence、反向 relation/route-state 插入历史、多 binding/route identity 和合成乱序 row 上对照独立 oracle 或精确 typed projection；capacity/performance closeout 对全部十项 runtime option 执行 invalid-min，并以容量 `N=2` 对 input batch/item、relation、occurrence、candidate、route state、activation、intent、prepared-action 和 condition stack 分别执行 `N-1/N/N+1`，同时覆盖 relation/candidate/route-state/activation/intent/prepared-action 及既有 input/occurrence overflow 的 64 次 warm-up 后 NoAlloc。动态 destroy/index reuse、`EmitExitThenClear`、target semantic reject 和 outbox/deferred/router capacity 已拆为 Phase 2+ integration IDs，不纳入 Phase 1 kernel gate。

当前 `IGameplayTriggerActionSink.Commit(...)` 是 no-fail `void` reservation handoff，不定义 target-consume result。`TRG-TXN-I03` 的 target semantic reject 必须随 Phase 19.2 target-owned bridge/result contract 落地；Phase 1 测试不得通过让 sink `Commit` 返回或吞掉自造结果来伪造该语义。

标准 scenario 由以下数据组成：

```text
Catalog + Bindings + InitialAuthorityState
+ FrameInputs[]
+ FaultPlan
+ ExpectedExternalTargetResults[]
```

每个 frame 结束后，optimized implementation 与 reference model 必须比较完整 oracle bundle：

1. 结构化 result/error code；
2. canonical occurrence、accepted activation 和 action intent 的完整有序 trace；
3. source membership/threshold、presence epoch、route policy、allocator、deferred 和 outbox 的 canonical authority projection；
4. module authority hash；
5. 必须出现的 numeric diagnostics reason。

不能只断言 event count，也不能只比较 hash。presentation observer 允许合法 drop，不能成为 authority oracle；hash 相等也不能替代 canonical state 比较。测试用 authority projection 可以由 Trigger SaveState participant 的规范 payload 或 test-internal adapter 提供，但不得通过反射、Dictionary 原始顺序、DebugName、Unity instance ID 或诊断字符串观察私有实现。

所有随机/组合测试使用固定 seed。失败报告必须包含 seed、最小可复现 catalog、逐帧 typed input 和 fault trace；可把较大失败样本写入 `Temp/GameplayTriggerTests/`，不自动提交。除随机探索外，修复过的 seed 进入小型、可评审的 regression corpus。仓库当前没有 property-testing 库，首版使用固定 seed generator、小状态空间穷举和显式 permutation，不为此强引入运行时或测试依赖。

### 19.2 Test Layers And Repository Placement

| Layer | 目的 | 目标入口 |
| --- | --- | --- |
| L0 Catalog/compiler | definition、binding、condition/action program 与 delivery contract 的静态拒绝 | pure C# compiler tests；若 compiler 在 Authoring solution，沿用其 console test runner |
| L1 Kernel/model | reducer、condition、policy、arbitration、lifecycle、transaction | Unity focused EditMode，直接实例化 noEngine core |
| L2 Determinism/property | permutation、metamorphic relation、reference-model parity | focused EditMode；固定 seed/小状态空间穷举 |
| L3 Gameplay integration | pipeline phase、单 drain、reservation、Router、target fake | focused EditMode integration tests |
| L4 Persistence | hash coverage、SaveState、checkpoint、replay、reliable outbox | focused EditMode + Runtime golden replay harness |
| L5 Domain/Unity bridges | Combat query、Story/Buff/Attribute bridge、Collider lifecycle | bridge EditMode；真实 Collider/scene lifecycle 使用专用 PlayMode asmdef |
| L6 Network/scale | authorization、snapshot、rollback/resync、容量和预算 | focused EditMode/network harness；必要时多人 PlayMode smoke |

当前仓库 pure C# NUnit 行为测试的权威执行入口是 Editor-only `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/MxFramework.Tests.asmdef` 下的 Unity focused EditMode。`noEngine` 描述生产 assembly 依赖边界，不表示已有独立 `dotnet test` runner。实施时测试可先放在 `Tests/Gameplay/Triggers`、`Tests/Runtime` 及对应 bridge 目录；若采用独立 `MxFramework.Gameplay.Triggers` asmdef，则同步拆出窄依赖的 Editor test asmdef。

真实 Collider、多 Collider、Disable/Destroy 和 scene unload 必须新增非 Editor-only 的 `MxFramework.Tests.PlayMode.GameplayTriggers`（最终命名随 asmdef 决策冻结），不能用直接调用 adapter 方法的 EditMode 测试冒充 PlayMode。`dotnet build MxFramework.Tests.csproj --no-restore` 只是编译门，不执行 Unity NUnit；只有未来新增专用 noEngine test project 后，才能把对应 `dotnet run/test` 声明为行为测试入口。

实际执行遵守仓库 Unity 验证约定：优先 `refresh_unity`、Console error check、focused `run_tests`/job result、再次检查 Console；没有已打开 Editor 时才用匹配项目版本的 Unity BatchMode，并把 `testResults` 和 log 写入 `Temp/`。`git diff --check`、lightweight checks、standards report 和 profiler 汇总是补充门禁，不替代行为测试。

### 19.3 Core Contract Matrix

以下 ID 是规范到测试的稳定追踪键。P0 只约束 ID 明示归属的 Phase，不跨阶段追责；`K` 表示 Phase 1 kernel，`I` 表示 Phase 2+ integration。P1 是该能力对外声明前的集成/扩展门禁。

| ID | Priority | Contract And Transformation | Required Oracle |
| --- | --- | --- | --- |
| `TRG-CAT-01` | P0 | duplicate ID、missing reference/adapter、condition depth/instruction 超限、非法 scope/action/delivery/capacity | catalog 结构化失败，session 不启动，不产生半 registry |
| `TRG-SRC-OCC-01` | P0 | occupancy `empty -> A -> A -> empty`；Subject 排列、重复项和 query hit 顺序任意变化 | Entered/Stayed/Exited、membership、activation/action order、state/hash 相同 |
| `TRG-SRC-OCC-02` | P0 | incomplete、重复 provider/同帧 batch、input/normalize/occurrence/candidate 容量在第 N+1 项失败 | source 整批不提交，旧 source/policy/sequence/hash 不变，无部分 occurrence |
| `TRG-SRC-THR-01` | P0 | High/Low hysteresis；阈值相等与前后一个 raw unit；两种 first-sample policy | active/Entered/Stayed/Exited 与 reference reducer 完全一致 |
| `TRG-SRC-THR-02` | P0 | complete batch 缺少 Subject 与整批 provider failure 对照 | 前者按 departure policy；后者保留整批旧 state，不把 missing 当 `0` |
| `TRG-SRC-SIG-01` | P0 | `UnorderedMultiset` 全排列并保留 duplicate multiplicity | canonical ordinal、activation/action 和 hash 与枚举顺序无关，重复项不丢失 |
| `TRG-SRC-SIG-02` | P0 | `Ordered` typed envelope 经 command/replay/network codec roundtrip；单独改变 sequence | roundtrip 逐字段一致；改变 sequence 允许改变结果，但 replay 精确复现 |
| `TRG-COND-01` | P0 | postfix program 对照独立递归 tree model；All/Any/Not、typed leaf、Fix64/payload 边界 | bool 结果一致，求值前后 authority state/hash 不变 |
| `TRG-POL-01` | P0 | PerInstance/Subject/Instigator/Pair 交错，count/cooldown/stay/once-per-presence 边界 | 只有 accepted activation 消耗 policy；scope 不串 state，epoch/count 符合定义 |
| `TRG-ARB-01` | P0 | 打乱 candidate/route/registry 构建顺序，单独改变 `ExecutionOrder` | independent 全接受；exclusive winner 只由 priority/tie key 决定；loser 不消耗 policy/reservation |
| `TRG-LIFE-K01` | P0 | Phase 1 kernel：duplicate contact、`ClearSilently` disable、reset、invalid/stale handle、re-enable、submit-before-lifecycle pending isolation | 幂等；stale 不路由；clear 必完成；重新进入按定义 re-arm；其他 instance pending 不受影响 |
| `TRG-LIFE-I01` | P0 | Phase 2 integration：`EmitExitThenClear`、dynamic destroy、generation/index reuse、external lifecycle command ordering | clear/result 语义唯一；旧 generation 不复活；command total key 稳定 |
| `TRG-TXN-01` | P0 | condition/cooldown/arbitration/core reservation 分别 reject | 已接受 source truth 不回滚；route count/cooldown/ActivationId/intent 不提交；下一帧不伪造 Entered |
| `TRG-TXN-02` | P0 | fake sink 第 N 个 `Prepare` 失败，两个 activation 竞争最后一个 slot | 已准备 token 按稳定逆序幂等 `Abort`、从未对 consumer 可见；无 oversubscribe/部分 intent |
| `TRG-TXN-K03` | P0 | Phase 1 kernel：`Commit` 注入 invariant exception | host fail-fast，已提交 authority 不伪装回滚 |
| `TRG-TXN-I03` | P0 | Phase 2+ integration：target consume 后 semantic reject | activation/policy 已提交，只产带 ActivationId/action index 的 result，不自动 retry/disable |
| `TRG-FRM-01` | P0 | spy systems 扰动注册顺序；prior deferred 与 current external 冲突；trigger chain | 显式 phase/priority 仍满足第 8/12 节；buffer 单 drain；current external 最终胜；chain 最早 F+1 |
| `TRG-OUT-01` | P0 | target missing/disabled/full/bridge skipped 多帧后恢复 | record 保留且 `OriginalDueFrame` 不变；G 接受时 command frame=G；stable order；只移除一次 |
| `TRG-OUT-02` | P0 | future/overdue outbox checkpoint；handoff 后 target Tick 抛错 | restore 后恰好投递一次；异常使 session 作废；checkpoint+replay 重建与 baseline 等价 |
| `TRG-HASH-01` | P0 | 每次只改变一个 authority 字段；再改变 diagnostics/debug/presentation/observer | authority projection/hash 按覆盖矩阵变化；排除字段不影响 hash/save |
| `TRG-SAVE-01` | P0 | 在每个合法 end-of-F barrier 切分同一 trace；active occupancy + once-per-presence | restore state/hash/sequence 与不中断 baseline 相同；首 Tick F+1；不重造 Entered |
| `TRG-SAVE-02` | P0 | 未归约 input、未消费 reservation、in-flight transfer、未捕获 target request；坏 schema/catalog/range/duplicate/stale ref | capture 返回 PendingWork 或 restore 原子失败；预置 state/hash 完全不变；future outbox 可正常保存 |
| `TRG-REP-01` | P0 | record/replay external typed envelope；复用/修改 caller buffer；unordered permutation | recorder 深拷贝正文，只录 exogenous；逐帧 activation/outbox/state/hash 与 baseline 相同 |
| `TRG-REP-02` | P0 | 外部伪造 derived source/raw authority command；检查 replay frame | authorizer 拒绝且 state/hash 不变；derived command 不进入 exogenous replay |
| `TRG-DIAG-01` | P0 | 多 observer，其中一个为零/满容量；改变 presenter 注册顺序 | Router 只 drain 一次；仅本 observer drop count 变化，authority/hash/save 不变 |
| `TRG-CBT-01` | P1 | Combat hit/query permutation、重复 hit、多 Subject、stale mapping、restore | canonical entity set、occurrence/order/hash 与 reference 相同，relation restore 正确 |
| `TRG-BRG-01` | P1 | 每种 action delivery contract 的 accept/full/disabled/semantic reject | frame、reservation/outbox、failure result、hash/save/replay 与 bridge contract 一致 |
| `TRG-UNI-01` | P1 | c1 enter、c2 enter、c1 exit、c2 exit；callback duplicate/permutation；同边界 enter+exit；OnDisable/Destroy | 只在首个 active Collider Enter、最后一个 Exit；短接触无 pulse；flush 后无 ghost membership |
| `TRG-NET-01` | P1 | raw client authority command、catalog mismatch、late join、snapshot corruption | server 拒绝未授权输入；snapshot 原子失败或 resync；state/hash 不被半应用 |
| `TRG-NET-02` | P1 | 任意 K frame rollback 后重放；outbox handoff 前后 correction | 每帧 canonical state、activation/outbox/hash 与 uninterrupted run 相同，不重复 action |
| `TRG-PERF-01` | P0 | warm-up 后 steady state、全 enter/exit、最大 condition、last-action Prepare fail | Trigger 自身 measured region `GC.GetAllocatedBytesForCurrentThread()` delta 为 0；既有基础设施分配单列 |
| `TRG-CAP-K01` | P0 | Phase 1 kernel：十项 runtime option invalid-min；input batch/item、relation、occurrence、candidate、route-state、activation、intent、prepared-action、condition-stack 各跑 `N-1/N/N+1` | N 成功；N+1 结构化且可重复失败，不扩容/静默丢权威事实；每类运行时 overflow 错误路径 NoAlloc |
| `TRG-CAP-I01` | P0 | Phase 2 integration：dynamic instance allocator、outbox、deferred、Router 各跑 invalid-min 或合法 0、`N-1/N/N+1` | allocator generation 与 transport overflow 结构化、可重复、NoAlloc，并满足各自 authority/presentation 事务边界 |

### 19.4 Required Metamorphic Relations

以下等价关系必须由 generator/permutation suite 长期锁定：

1. occupancy、Combat hit 和 unordered signal 的枚举排列不改变 authority；unordered signal 的相同项保留 multiplicity。
2. 同一 logical entity 的重复 occupancy/hit 不改变结果；complete batch 不允许拆成多个所谓“等价增量 batch”。
3. registry/Dictionary 插入顺序、DebugName、editor GUID、Unity instance ID、localized text 和 presentation observer 不改变 authority。
4. 任意合法 checkpoint barrier 上“连续运行”与“capture -> restore -> suffix”逐帧等价。
5. checkpoint + replay suffix 与原始 suffix 等价；Phase 19.4B进一步锁定rollback K frame + replay等价。
6. 增删 observer、改变 observer 顺序或让单 observer overflow，不改变 Trigger authority。
7. Ordered Signal 重排不是等价变换；测试应证明 sequence 被保存，而不是强迫结果相同。
8. condition tree 与 postfix program 语义等价；除非 compiler 另行规定 canonicalization，不要求不同 authoring tree 得到相同 catalog bytes。

### 19.5 Fault Injection Harness

测试夹具至少提供以下可编程 fake，并允许按调用序号、frame、Instance/Route/ActionKind 精确失败：

- complete/incomplete source provider 和 typed envelope codec；
- fixed-capacity occurrence/candidate/intent/deferred/outbox buffers；
- reservation sink，记录 `Prepare -> Abort/Commit -> Consume`、token stable order 和可见性；
- target bridge/consumer，可分别模拟 missing、disabled、full、semantic reject 和 Tick exception；
- checkpoint quiescence participant，可制造每一种 PendingWork；
- command authorizer/replay recorder spy；
- Router observer，可独立 overflow。

每个 failure test 同时断言 result code、完整 authority projection、hash、activation/action trace 和必要 diagnostics；不能只断言“抛了异常”或“事件数为 0”。`Abort` 需要单独做重复调用测试。`Commit` exception 是 invariant failure，测试不得期待系统继续运行或做伪原子回滚。

### 19.6 Save, Replay, Outbox And Network Scenarios

最低 golden scenario 必须覆盖：

- frame F 同时产生 `DueFrame=F+1` 的 internal deferred 与 reliable outbox，end-of-F capture 后恢复，F+1 各执行恰好一次；
- active occupancy、threshold active、presence epoch、cooldown/count、allocator generation 和 next ActivationId 的 roundtrip，恢复后不重新 Enter；
- invalid restore 的错误尽量放在 payload 尾部，证明 validate-plan-commit 不会半替换先前 sentinel state；
- catalog hash mismatch 同时区分 definition、binding 和 combined hash diagnostics；
- 一份 exogenous Signal envelope 派生 target command，recorder 中没有 derived command，replay 后 action 仍恰好一次；
- target 在 F+1 不可用、F+N 可用时，以 current frame F+N 投递，同时永久保持原 due 和稳定次序；
- target 接受后 Tick 失败，FailFast session 不可继续；从上一 checkpoint + replay 重建不会双投；
- Phase 6 的 late join、snapshot correction、rollback 分别在 activation 前、outbox handoff 前和 handoff 后切入。

golden 只保存小而可读的 typed inputs、逐帧 hash 和 canonical trace。golden 更新不得由测试自动接受；变更必须展示输入、旧/新 trace/hash 和对应规范理由，避免把实现回归“批准”为新事实。

### 19.7 Combat And Unity Bridge Tests

Combat bridge 先用 EditMode 对 deterministic query 的 hit permutation、dedupe、stale generation、多 Subject、restore 和 warm-up 后 NoAlloc 做 model parity。Combat observation 可确定性重算，因此不进入 exogenous replay；测试必须同时证明“未录制”与“重算后逐帧相同”。

Unity adapter 的 EditMode 测试只能验证 callback 到 level mutation/command 的映射和“不在 callback 内执行 action”。真实 PlayMode 至少覆盖：

- 一个 logical Subject 的多 Collider 首进末出；
- duplicate/unknown exit 不产生负计数或重复 occurrence；
- 同一 evaluation boundary 内 enter+exit 回到旧 level 时不产生 pulse；需要事实保留时显式 Signal；
- zone/subject `OnDisable`、Destroy 和 scene unload 显式 flush；
- callback permutation 不改变最终 level、occurrence 和 authority hash；
- save/hash payload 不含 Collider、GameObject 或 Unity instance ID；
- checkpoint、door、trap/EffectVolume showcase 的操作、保存、退出、恢复后继续。

只有实际通过专用 PlayMode asmdef/Test Runner 的用例才能称为 PlayMode。手动调用 `RaiseTrigger`、方法名包含 PlayMode 或 Console 无 error 都不能替代真实 callback/lifecycle smoke。

### 19.8 Allocation, Capacity And Scale

allocation test 先完成 warm-up，并在 measured region 外创建 assertion 数据、fault plan 和输入。至少重复 64 次 steady trace，再用 `GC.GetAllocatedBytesForCurrentThread()` 断言 Trigger-owned Tick 路径为 `0`；normalization failure、最后一个 action `Prepare` failure、overflow diagnostics 等错误路径同样测量。若 Runtime/Gameplay 既有容器仍分配，报告分别列出 Trigger 自身与基础设施分配，不能把二者合并成模糊结论。

每个可配置 capacity 都做边界表：非法最小值（或合法 0）、`N-1`、`N`、`N+1`，并重复相同 trace 验证错误码、state 和 hash 可复现。authority buffer overflow 必须拒绝对应事务；presentation observer overflow 只增加本地 dropped count。capacity/config revision 属于 authority；high-water mark、attempt diagnostics 和 profiler 数据默认只属 diagnostics，不进入 hash/save，除非未来某字段直接控制 authority 决策并同步升级 schema。

Phase 19.4B 已冻结并验证规模预算：seed `0x0739194B`，64 definitions、256 instances、2048 active relations、每帧64 signals/512 candidates/256 actions（128 same-frame + 128 reliable）、128-frame history、2 participants；120 warm-up + 480 measured。Apple M4/16 GB、macOS 26.5.1、Unity 6000.5.0f1 BatchMode结果为median 1.519100 ms、P95 1.569100 ms、max 1.613900 ms，Trigger-owned/bridge bounded wire+rollback/infrastructure/managed heap peak delta均0 B，snapshot与high-water均132752 B；records/relations/candidates/actions/outbox/history/participants high-water分别64/2048/512/256/128/128/2。结果满足冻结的4/6/10 ms、1 MiB infrastructure、4 MiB heap和512 KiB snapshot预算。未给出workload与capacity profile的单一毫秒数字仍不能作为其他composition的scale claim。

### 19.9 Phase Test Gates

| Phase | Required Gate |
| --- | --- |
| 0 Spec | 第 19 节所有 normative contract 有 test ID；19.10 的 oracle ambiguity 关闭或明确移出对应 Phase；reviewer 可从 trace 唯一推导结果 |
| 1 noEngine Kernel | `TRG-CAT-01`、`TRG-SRC-*`、`TRG-COND-01`、`TRG-POL-01`、`TRG-ARB-01`、`TRG-LIFE-K01`、`TRG-TXN-01`、`TRG-TXN-02`、`TRG-TXN-K03`、`TRG-PERF-01`、`TRG-CAP-K01`；reference-model parity、固定 regression seeds、kernel measured path 0 B |
| 2 Runtime/Gameplay Integration | `TRG-LIFE-I01`、`TRG-TXN-I03`、`TRG-CAP-I01`、`TRG-FRM-01`、`TRG-OUT-*`、`TRG-HASH-01`、`TRG-SAVE-*`、`TRG-REP-*`、`TRG-DIAG-01`；单 drain、ordering、atomic restore、derived provenance、逐帧 golden 全通过 |
| 3 Combat Sensor | `CBT` permutation/dedupe/stale/restore/NoAlloc bridge suite |
| 4 Bridges And Config | 每个 compiler rule/action delivery contract；Story F+1、same-frame reservation、outbox failure/restore、Unity adapter EditMode |
| 5 Playable Showcase（19.4A 已交付） | Unity refresh/compile、Console 无 error、focused EditMode + 专用 PlayMode；checkpoint/door/trap 可操作且 save/restore 可继续 |
| 6 Network/Scale（19.4B 已交付） | `NET` authorization/snapshot/late join/rollback/resync golden；固定seed capacity/frame-time/memory evidence通过冻结预算 |

每个 Phase 的验收记录至少包含：测试过滤器/命令、Unity 或 runner 版本、通过/失败/跳过数、失败 seed/golden（如有）、Console error 结果和性能环境。只运行编译、静态检查或 report-only standards scan 不能把行为 gate 标为通过。

### 19.10 Frozen Phase 1 Testability Decisions

Phase 1 已冻结并由 focused tests 覆盖：

1. activation 在 frame F 成功且 `CooldownFrames=N` 时，首个 eligible frame 是 `F+N`；N=0 允许同帧不同 occurrence 分别接受。
2. `StayIntervalFrames` 从 presence start 或上次 accepted Stay 计时；condition、arbitration、reservation reject 不推进 schedule。
3. 同一 source/frame 第二份 complete batch 只拒绝第二份；scalar batch 同一 Subject 重复 sample 整批拒绝并保留旧 source state。
4. threshold departure 保留最后有效 sample 作为 current；`PrimeSilently` 首样本 active 时创建 presence epoch，但不发 Entered。
5. `Prepare` total key 为 accepted activation execution key 后接 action index；Abort 使用严格逆序。
6. Phase 1 disable 仅暴露 `ClearSilently`；reset 清除 source 与 route policy state。

Phase 19.2A 进一步冻结 external lifecycle command 语义：

7. lifecycle command 直接采用 `RuntimeCommandBuffer` 的全局 total key，按 `(Frame, SourceId, CommandId, TargetId, Sequence)` 升序应用；Trigger command system 不二次排序。不同 `SourceId` 的冲突由较大 source 后覆盖；前四项相同的冲突由较晚接受的 `Sequence` 后覆盖。该顺序属于 authority/replay 语义，修改必须升级契约并更新 permutation oracle。
8. command diagnostics 同时保留最后一条处理结果与最后一条拒绝结果；后续成功不得覆盖 rejection 的 sequence、instance 和 error code。当前 O(1) diagnostics 不替代后续 bounded result/event stream。

Phase 2 及以后仍需在对应实现前冻结：

1. reliable outbox `RetryBudget`、deadline attempt 和 reset/owner-stale cancellation。
2. active Collider 到 `GameplayEntityId` mapping 重绑语义。
3. `EmitExitThenClear` 在 occurrence/candidate capacity failure 时的 clear/result 语义。

## 20. Phased Delivery

| Phase | 交付 | 最低验收 |
| --- | --- | --- |
| 0 Spec | 本 Design Draft + Proposed ADR + test oracle/matrix | 权威、帧序、ID、state、capacity、delivery 无歧义；19.10 的对应 Phase blocker 已关闭 |
| 1 noEngine Kernel | registry/store、signal/occupancy/threshold reducer、routes、conditions、policy、arbitration、intent、diagnostics | 第 19 节 Phase 1 kernel IDs：`TRG-CAT-01`、`TRG-SRC-*`、`TRG-COND-01`、`TRG-POL-01`、`TRG-ARB-01`、`TRG-LIFE-K01`、`TRG-TXN-01`、`TRG-TXN-02`、`TRG-TXN-K03`、`TRG-PERF-01`、`TRG-CAP-K01` |
| 2 Runtime/Gameplay Integration | systems、typed input envelope/codec、bounded event Router、reservation/outbox、hash、checkpoint barrier、SaveState、replay | `TRG-LIFE-I01`、`TRG-TXN-I03`、`TRG-CAP-I01`、`TRG-FRM-01`、`TRG-OUT-*`、`TRG-HASH-01`、`TRG-SAVE-*`、`TRG-REP-*`、`TRG-DIAG-01`，另加单 drain、atomic restore、replay golden |
| 3 Combat Sensor（19.3B 已实现） | deterministic occupancy bridge | `CBT` query permutation、stale/dedupe、多 Subject、restore、NoAlloc |
| 4 Bridges And Config（19.3A compiler、19.3C1-C3 bridges、#732 Unity occupancy 已实现） | Gameplay Buff reserved sink/result、Gameplay component/Story reliable bridge、compiler、Unity occupancy adapter | compiler/action contract matrix、Buff transaction/result/replay/NoAlloc、outbox restore、Story F+1、Unity adapter EditMode + real Physics PlayMode |
| 5 Playable Showcase（19.4A 已实现） | checkpoint + door + trap/volume 可玩入口 | Unity compile/console、focused EditMode + 专用 PlayMode、save/restore continuation |
| 6 Network/Scale（19.4B 已实现） | bounded protocol-2 authority、snapshot/late join、rollback/resync、capacity/scale validation | `NET` authorization/resync/rollback golden、可复现budget evidence；最终network+regression EditMode 298/298 |

### 20.1 Phase 19.4A Delivered Playable Boundary

`Assets/Scenes/GameplayTriggerPlayable.unity` 是可打开、可操作、可验证入口。其组合根把 Combat motion/physics occupancy 接到 Trigger，再把door动作交给Story reliable bridge、trap动作交给Gameplay Buff same-frame bridge，并把Gameplay/Trigger/Story/Demo authority纳入Runtime checkpoint、SaveState和canonical replay-compatible hash。键盘与HUD都提供移动、Capture、Restore、Reset；Capture只写session持有的单个内存`RuntimeSaveState` slot，Restore以fresh candidate原子换入，Reset清空slot。该slot不是durable persistence，Playable也不提供network/replay UI。

19.4A不改变Trigger core hash/SaveState/replay schema，也不把Unity primitives或HUD变成authority。其Playable本身仍不提供network UI；跨进程authority由独立19.4B noEngine/network composition承担。

### 20.2 Phase 19.4B Delivered Network / Scale Boundary

#739 交付transport-agnostic bounded protocol-2 packet contract、authenticated session/remote/local/direction metadata + ownership authorization、Gameplay/Trigger binary snapshot、late join F+1 continuation、bounded rollback/resync与固定规模证据。它复用既有`NetworkSessionRunner`和`NetworkPredictionRollbackCoordinator`，不迁移CharacterTest gameplay protocol，也不替代transport可靠性、认证凭证或加密。

FullComparable只在Gameplay component CLR store拥有exact codec coverage且Gameplay 100 -> Trigger 200 candidate均成功时成立；partial client显式拒绝full hash。snapshot恢复authority而不注入derived observations/results，重复snapshot幂等。当前边界不含durable persistence、matchmaking、Relay、lobby UI、generic Unity action bridge、JSON importer或active-session hot reload。

实现阶段新增/修改 C# 前必须重新读取 `API_STANDARDS.md`、`QUALITY_GATE.md` 及相关 Interfaces，并为高频路径写明 `NoAlloc` 分类。Phase 1 + 19.2A-H2 + 19.3A compiler + 19.3B Combat sensor + 19.3C1-C3 bridges + #732 Unity occupancy adapter + #736 19.4A Playable + #739 19.4B network/scale 的 Current contract 以 `Interfaces/Gameplay.Triggers.md` 为准；剩余generic Unity action bridge属于后续独立领域扩展。

## 21. Documentation Sync Gates

当前 Phase 1 + 19.2A/19.2B/19.2C/19.2D/19.2E/19.2F1/19.2F2/19.2G1/19.2G2/19.2H1/19.2H2 + 19.3A/19.3B/19.3C1/19.3C2/19.3C3/19.3D + 19.4A/19.4B：

- `ADR-008` 已按 phased delivery 接受；
- 独立 asmdef、公共 API、测试入口和 Issue #668 已建立；
- `Interfaces/Gameplay.Triggers.md` 声明已实现的 noEngine kernel、lifecycle command factory 与 Gameplay PreCommand/Command/Simulation systems；
- `GameplayRuntimeModule` 保持唯一 command drain owner；Signal/occupancy/scalar envelope/provenance value copy、bounded atomic reconstruction、trusted source/exogenous replay enforcement、catalog/authority hash、本地 playback/bootstrap与protocol-2 bounded network authority均已完成；
- 19.2E contributor 只承诺 quiescent end-of-Evaluate/Tick hash；19.2F1 提供通用 RuntimeHost completion/quiescence barrier；19.2F2 提供 canonical Trigger payload/checker/排他单线程 atomic restore；19.2G1 提供 Trigger-owned internal deferred queue；19.2G2 提供 Trigger-owned reliable outbox、schema-3 hash/save、FailFast transfer module 与 TrustedDerived seam；19.2H1 提供 bounded live presentation Router；19.2H2 提供 exact-composition Fresh/checkpoint-suffix playback、typed preflight/fault context 与 derived regeneration；19.3A 提供 canonical noEngine config compiler/bootstrap/composition verifier；19.3B 提供 noEngine deterministic Combat sensor、canonical sensor/combined hash、typed atomic diagnostics 与 restore scheduling reset；19.3C1 提供 Gameplay-owned Buff request reservation、same-frame Apply/Remove sink、flat terminal result 与 Trigger ActionResult relay；19.3C2 提供 reliable Gameplay component Destroy/Attribute/Ability bounded command transfer/result relay；19.3C3 提供 exact default-validator Story RaiseTrigger bounded transfer、flat numeric terminal result 与 strict Trigger ActionResult relay；19.3D/#732 提供 Unity Physics occupancy catalog、fixed contact coordinator、persistent `PreSimulation/0` complete-batch flush、ReplayDisabled 与 lifecycle final-empty retry；19.4A/#736 提供checkpoint/door/trap Playable、键盘/HUD controls与内存Capture/Restore/Reset；19.4B/#739提供bounded wire/profile/auth/snapshot/rollback/resync与scale gate。Gameplay Trigger Runtime parent program至此完成；generic Unity action bridge仍不进入Current claim。

后续未交付 Phase 实现后：

- 根据最终 asmdef 新建 `Interfaces/Gameplay.Triggers.md` 或更新 `Interfaces/Gameplay.md`；
- 同步 Runtime/Gameplay/Combat/Story 相关接口、`CAPABILITIES.md`、`USAGE.md` 和验证入口；
- 19.4A已经通过Unity可打开、可操作、可验证入口门禁；19.4B已经通过network/rollback golden与固定规模预算，整个19.4可标记完成。

## 22. Spec Acceptance Checklist

- [x] 认可 Trigger Runtime 是 Gameplay authority，而不是 Unity/Events/Story authority。
- [x] 认可它作为 systems 接入现有 Gameplay pipeline，不拥有第二个 command drain。
- [x] 认可 occupancy、scalar sample、Signal 先归约为 occurrence，再由独立 Route 执行。
- [x] 认可 source truth 一经接受不因 route/action reject 回滚，v0 不隐式重试。
- [x] 认可 conditions 只读、actions 只产出 typed intent。
- [x] 认可 default independent、explicit exclusive group 和稳定 winner/order。
- [x] 认可 same-frame 仅限后续 typed request consumer，RuntimeCommand/Story 默认 next frame。
- [x] 认可 same-frame 需要 reservation token，cross-module 使用可 hash/save 的 reliable outbox。
- [x] 认可内部 trigger chain deferred，禁止同帧递归。
- [x] 认可 source/route policy/pending state 进入 hash/save，presentation 不进入。
- [x] 认可 capture 是 end-of-frame barrier：保存 F，恢复后从 F+1 开始。
- [x] 认可 Trigger event 只有一个 Router drain owner，再做 bounded fanout。
- [x] 认可 Tick `NoAlloc after initialization` 和显式 capacity failure。
- [x] 认可第 19 节的 reference model、canonical oracle、故障注入矩阵和逐 Phase 测试门禁属于框架契约。
- [x] 认可领域 trigger 不做强制迁移。

## 23. Deferred Decisions For Later Phases

1. fixed Signal envelope 的 JSON/network codec、跨进程 session identity，以及 client intent -> server authority command 转换；SourceId 数值本身不得升级为认证承诺。
2. bounded event Router 是升级现有容器还是新增 Trigger-local contract；Phase 1 action sink 已使用 consumable reservation。
3. timer bridge 在 Runtime timer 完整 Save/Restore 前的正式能力声明。
4. Network snapshot/rollback restore dependency order 是聚合进 Gameplay participant，还是升级两个 registry contract。
5. reliable outbox 的 retry/deadline attempt、reset/owner stale cancellation 语义，以及对应 hash/save schema。
6. Unity active Collider 发生 entity mapping 重绑时的权威语义。
7. `EmitExitThenClear` 容量失败语义。

## References

- `AGENT_GAME_CREATION_GUIDE.md`
- `API_STANDARDS.md`
- `QUALITY_GATE.md`
- `Interfaces/Runtime.md`
- `Interfaces/Gameplay.md`
- `Interfaces/Combat.md`
- `Interfaces/Story.Runtime.md`
- `Interfaces/Events.md`
- `Interfaces/Modifiers.md`
- `Decisions/ADR-005-story-runtime-command-boundary.md`
- `Decisions/ADR-008-gameplay-trigger-runtime-authority.md`
