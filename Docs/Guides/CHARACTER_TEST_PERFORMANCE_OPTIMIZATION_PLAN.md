# CharacterTest 性能优化计划

> Status: Guide
>
> Date: 2026-06-19
>
> Scope: 基于 2026-06-19 Profiler Analyzer 快照，规划 CharacterTest Runtime、视图绑定、动画资源配置、FairyGUI player-facing UI 和 Combat physics 的性能优化。

## 目的

本文记录从 Profiler Analyzer CSV 快照整理出的优化计划：

- `/Users/vincent/Documents/frameTime.csv`
- `/Users/vincent/Documents/markerTable.csv`

本计划在排序项目工作时主动忽略异常尖峰帧。Editor 工具、一次性渲染管线重建、MCPForUnity 工具发现，以及其他非 gameplay 停顿，不作为本文的 CharacterTest Runtime 优化目标。

优化目标是在不破坏当前架构的前提下，降低 CharacterTest 可重复运行成本：

- 权威 gameplay 状态继续保存在 CharacterTest runtime data；
- Unity GameObject 仍然只是 view/resource；
- `RuntimeHost` 仍然负责生命周期和 Tick 调度；
- FairyGUI 仍然是 player-facing UI 路径；
- Debug UI 和 profiling surface 只负责观察，不进入 replay、save-state 或 runtime hash 权威状态。

## 分析口径

后续对比性能时，先使用下面这组快照基线。

| 帧集合 | 帧数 | Median | Mean | P75 | P90 | P95 | GC median | GC P95 | GC total |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 全部帧 | 1244 | 16.775 ms | 53.802 ms | 17.608 ms | 36.424 ms | 80.584 ms | 596 B | 29902 B | 101.17 MB |
| 正常帧 `<33.33ms` | 1110 | 16.677 ms | 16.508 ms | 17.107 ms | 18.913 ms | 20.847 ms | 594 B | 3528 B | 1.81 MB |
| 可玩帧 `<50ms` | 1138 | 16.691 ms | 17.091 ms | 17.190 ms | 19.860 ms | 22.230 ms | 594 B | 3836 B | 2.13 MB |
| 非尖峰帧 `<100ms` | 1190 | 16.708 ms | 19.344 ms | 17.319 ms | 21.235 ms | 44.680 ms | 594 B | 4363 B | 5.65 MB |

优化排序优先看：

1. 正常帧 median 和 upper quartile；
2. 跨数百帧重复出现的 Marker total time；
3. 正常帧 per-frame GC；
4. 只有在低频操作属于预期 gameplay 行为时，才把 spawn/config 成本纳入排序。

不要只用 Marker `Max Time` 排序。除非同一个尖峰能在干净的 Player-focused capture 中稳定复现，否则只把它当作排除项或单独排查项。

## 本计划排除的尖峰类型

当前快照包含以下停顿；在干净 capture 复现之前，不纳入 CharacterTest Runtime 优先级。

| 类型 | 快照证据 | 排除原因 |
| --- | --- | --- |
| Editor 主循环停顿 | `EditorLoop` 出现多秒帧，缺少可归因到 CharacterTest 的子栈 | 不能从本快照证明是 runtime code 导致。 |
| MCPForUnity 工具发现 | `ToolDiscoveryService.DiscoverAllTools()` 单次约 54 MB GC | Editor automation 开销，不是 gameplay runtime。 |
| URP pipeline 重建 | `UniversalRenderPipelineAsset.CreatePipeline()` / renderer creation 尖峰 | 更像 Play Mode 或 Editor render setup churn；需要独立渲染管线复现。 |
| 一次性 UI/style 重建 | UI Toolkit style 和 text shaping 在少数帧集中尖峰 | 除非 CharacterTest UI 动作在 warm-up 后复现，否则只作为测量噪声。 |

这些问题可以另开 Editor workflow 或 profiling-environment 任务，不应该阻塞下面的 runtime 优化计划。

## 优化目标

### 1. 稳态 Runtime Tick

代表性重复 Marker：

| Marker | Median | Upper quartile | Total | Frames | GC total |
| --- | ---: | ---: | ---: | ---: | ---: |
| `RuntimeHost.Tick()` | 5.833 ms | 9.129 ms | 11350.610 ms | 1140 | 27.68 MB |
| `GameManager.Update()` | 3.532 ms | 5.403 ms | 7762.289 ms | 1244 | 20.92 MB |
| `GameSlice.Tick()` | 2.920 ms | 4.570 ms | 5681.421 ms | 1140 | 13.84 MB |
| `CharacterTestCharacterRuntimeModule.Tick()` | 2.831 ms | 4.451 ms | 5489.091 ms | 1140 | 13.27 MB |

优化思路：

- 先把 `RuntimeHost.Tick()` 当作包含式根节点看待。不要在确认子模块干净之前优化 Host dispatch。
- 通过显式 `ProfilerMarker` 或现有 profiled runtime module，把 CharacterTest runtime 成本拆成 simulation、command drain、motion、interaction、presentation、diagnostics 几个桶。
- 稳态 Tick 路径目标是 `NoAlloc after initialization`。当前正常帧存在小但持续的 GC baseline；第一步要确认 594 B/frame 是 CharacterTest 代码、Unity/FairyGUI 内部，还是 Profiler 自身开销。
- 保持 services-based CharacterTest 访问模式。新的优化工作应通过 `CharacterTestSliceServices` / module boundary 推进，不要把逻辑塞回 `GameManager`。
- 不用关闭系统来制造性能收益。每个优化都必须保留相同 runtime state、command 行为、replay-ready 顺序和 diagnostics 可见性。

候选工作：

- 在 `CharacterTestSliceServices.Tick...` 周边补一层 profiler counter，后续 capture 能区分 simulation、UI refresh、camera、presentation 和 diagnostics。
- 审计 `GameManager.Update()` 是否存在 per-frame snapshot 创建、字符串创建、LINQ、分配型 `foreach`、debug message formatting。
- command/event diagnostics summary 改为 caller-buffer 或 dirty-driven 更新。
- 保持 `RuntimeHost` module registration 稳定；不要把动态注册/注销作为 per-frame feature switch。

### 2. Motion 与 Combat Physics

代表性重复 Marker：

| Marker | Median | Upper quartile | Total | Frames | GC total |
| --- | ---: | ---: | ---: | ---: | ---: |
| `CharacterTest.CharacterRuntime.Motion` | 2.019 ms | 2.202 ms | 3256.864 ms | 1140 | 1.97 MB |
| `CharacterTestCharacterMotionSystem.TickEntity()` | 0.702 ms | 0.849 ms | 1189.202 ms | 1140 | 1.95 MB |
| `CombatKinematicMotor.Step()` | 0.833 ms | 1.102 ms | 1328.406 ms | 1140 | 3.88 MB |
| `CharacterTestMovableNaturalObjectSystem.SyncCombatCollider()` | 0.613 ms | 0.627 ms | 958.405 ms | 1140 | 0 B |
| `CombatPhysicsWorld.UpsertAabbCollider()` | 0.587 ms | 0.600 ms | 918.847 ms | 1140 | 12 KB |
| `CombatPhysicsWorld.FindBodyIndex()` | 0.509 ms | 0.569 ms | 803.607 ms | 1140 | 0 B |
| `CombatBodyId.Equals()` | 0.303 ms | 0.327 ms | 476.035 ms | 1149 | 0 B |

优化思路：

- Motion 系统有稳定成本，不是异常尖峰造成的假问题。
- `FindBodyIndex()` 和 `CombatBodyId.Equals()` 的总耗时提示 collider sync/query 内可能存在重复线性查找或 equality-heavy lookup。改算法之前，应先读当前数据结构，并用 profiler marker 或聚焦测试证明真实 lookup 路径。
- 静态 collider 和动态 collider sync 应拆开。静态 collider 不应该每帧 upsert；动态 collider 也只应在权威 pose 或 shape 改变时更新。
- 优先使用 dirty flag，而不是大范围 per-frame rebuild。body 的 transform、radius、height、obstacle mask、blocking state 改变时才标记 AABB dirty。
- collision candidate list 和 query result 使用 caller-owned buffer。motion / broadphase query 不应返回新数组或新 list。
- gameplay collision 权威仍保留在 `CharacterTestCharacterMotionSystem`、`CombatKinematicMotor`、`CombatPhysicsWorld`。不要为了性能把 Unity Collider 读回成权威状态。

候选工作：

- 如果当前 `CombatPhysicsWorld` 查找是线性的，并且 body order 足够稳定，就增加 `bodyId -> index` lookup cache 和失效机制。
- 给 `CharacterTestMovableNaturalObjectSystem` 和角色 collider sync 路径增加 dirty collider sync。
- broadphase maintenance 区分 static scene obstacles 和 runtime-moving bodies。
- 检查 broadphase cell-list rent 与 candidate sort 是否能跨帧复用。
- 增加常见体量的聚焦 benchmark 或 EditMode tests：3 个角色、10 个 moving objects、50 个 obstacles，以及 showcase 使用的压力体量。

### 3. Interaction Query

代表性重复 Marker：

| Marker | Median | Upper quartile | Total | Frames | GC total |
| --- | ---: | ---: | ---: | ---: | ---: |
| `CharacterTestInteractionConsumerDispatcher.Consume()` | 0.010 ms | 1.427 ms | 729.319 ms | 1140 | 15.6 KB |
| `CharacterTestInteractionPhysicsSystem.Tick()` | 0.002 ms | 1.418 ms | 718.348 ms | 1140 | 15.1 KB |
| `CombatPhysicsWorld.QueryCapsule()` | 0.107 ms | 1.394 ms | 776.086 ms | 1211 | 13.2 KB |
| `CombatPhysicsWorld.QueryBroadphase()` | 0.104 ms | 1.382 ms | 768.696 ms | 1211 | 14.1 KB |

优化思路：

- median 很低，但 upper quartile 偏高，说明 query burst 不是罕见到可以忽略。
- interaction physics 应在存在 interaction intent、overlap state 或 nearby candidate 需求时运行；inactive consumer 不应每帧做完整 query。
- query result buffer 应为 system 持有的持久 buffer，或由 caller 提供。

候选工作：

- 在 interaction physics query 前加廉价 activation gate。
- 当 actor 和 interactable 都没有移动时，短时间缓存 last-nearby candidate set。
- 只有在 lifetime 和排序语义明确时，才和 motion 共享 broadphase candidate buffer；否则使用独立 pooled buffer，避免隐式耦合。

### 4. Presentation 与 Animation Apply

代表性重复 Marker：

| Marker | Median | Upper quartile | Total | Frames | GC total |
| --- | ---: | ---: | ---: | ---: | ---: |
| `CharacterTest.GameManager.SlicePresentation` | 0.470 ms | 0.558 ms | 1201.719 ms | 1211 | 1.43 MB |
| `CharacterTestRuntimeCharacterView.TickPresentation()` | 0.444 ms | 0.511 ms | 1148.888 ms | 1211 | 1.23 MB |
| `CharacterRuntimeLocomotionBlendController.TickRuntimeAnimation()` | 0.341 ms | 0.371 ms | 1012.467 ms | 1211 | 1.08 MB |
| `CharacterRuntimeLocomotionBlendController.ApplyAnimationBackend()` | 0.334 ms | 0.364 ms | 999.978 ms | 1211 | 1.08 MB |
| `UnityPlayablesAnimationBackend.SetBlend2D()` | 0.217 ms | 非主要指标 | 755.731 ms | 1211 | 298 KB |

优化思路：

- Presentation 优先级低于 motion，但它每帧运行且存在稳定 GC。
- view 应只 apply changed state：transform、combat state、animation blend、HeadPad text、visual toggles 都应有 dirty/version check。
- animation apply 避免临时数组、字符串和 iterator 对象。blend weight 与 clip binding 应在配置后缓存。
- 不要为了降低成本把 runtime authority 推入 view。view 可以缓存 last-applied values，但权威状态仍在 runtime component。

候选工作：

- 给 `CharacterTestRuntimeCharacterView` 增加 last-applied snapshot 或 version token。
- 在 `CharacterRuntimeLocomotionBlendController` 内缓存 animation blend 参数解析结果。
- 当 target blend id 和 blend weights 在容差内未变化时，跳过 backend apply。
- presentation diagnostics 的字符串构造放到 debug visibility 或 explicit diagnostics capture 后面。

### 5. Character Spawn 与 Animation Resource Setup

低频但 gameplay 可见的 Marker：

| Marker | Median | Upper quartile | Total | Frames | GC total |
| --- | ---: | ---: | ---: | ---: | ---: |
| `TryCreateView()` | 78.131 ms | 234.557 ms | 771.187 ms | 5 | 9.02 MB |
| `CharacterTestRuntimeCharacterLoader.Instantiate()` | 75.435 ms | 232.657 ms | 757.171 ms | 5 | 8.77 MB |
| `LogLocomotionStatus()` | 61.426 ms | 69.363 ms | 373.136 ms | 5 | 7.55 MB |
| `TryConfigureLocomotion()` | 60.998 ms | 68.863 ms | 370.540 ms | 5 | 7.32 MB |
| `TryCreateLocomotionDefinition()` | 53.492 ms | 60.210 ms | 293.420 ms | 5 | 6.90 MB |
| `MxAnimationCompiledArtifactJson.LoadSetDefinitions()` | 36.289 ms | 36.602 ms | 196.539 ms | 5 | 4.59 MB |
| `MxAnimationSetDefinitionHasher.ComputeHash()` | 31.126 ms | 36.917 ms | 169.613 ms | 5 | 1.51 MB |
| `MxAnimationCompiledArtifactJson.ParseSetDefinition()` | 18.994 ms | 19.268 ms | 103.802 ms | 5 | 956 KB |

优化思路：

- Spawn 允许比稳态 Tick 有更多分配，但当前成本足以在预期 gameplay 行为中造成可见 hitch。
- 最大机会是按 source path、content hash 或 runtime resource key 缓存不可变 parsed animation artifacts。
- 同一个 animation set definition 被复用时，不应重复 hash/canonical text generation。
- `LogLocomotionStatus()` 不应成为核心 spawn 成本。详细状态格式化应 debug-gated、sampled，或从缓存的不可变 metadata 生成。
- resource 和 animation prewarm 应发生在 gameplay-sensitive spawn 之前，比如菜单或场景加载阶段。

候选工作：

- 新增 animation runtime artifact cache，归 CharacterTest composition layer 或 `Character.RuntimeSpawn.Unity` adapter layer 所有。
- 缓存 `MxAnimationSetDefinition` 和相同 artifact content 的 computed hash。
- 把 locomotion status logging 改成 cheap structured diagnostics：成功路径廉价，只有 warning/error 或显式 debug capture 才做详细字符串格式化。
- 在菜单/场景加载阶段 prewarm selected character package、animation set、clip registry 和常用 clips。
- 只有缓存完成后再考虑把 spawn 拆帧；async staging 不能替代重复解析问题的修复。

### 6. FairyGUI Player UI

代表性重复和低频 Marker：

| Marker | Median | Upper quartile | Total | Frames | GC total |
| --- | ---: | ---: | ---: | ---: | ---: |
| `FairyGUI.Container.Update()` | 0.915 ms | 1.066 ms | 3178.339 ms | 1244 | 855 KB |
| `MxFairyGuiHost.TryCreateComponent()` | 8.535 ms | 10.390 ms | 98.713 ms | 10 | 3.34 MB |
| `UIPackage.CreateObject()` | 25.595 ms | 31.146 ms | 295.979 ms | 10 | 10.01 MB |
| `CharacterTestFairyGuiHeadPad.Create()` | 9.533 ms | 10.301 ms | 51.033 ms | 5 | 1.65 MB |
| `CreateBuffBadges()` | 2.912 ms | 3.303 ms | 14.936 ms | 5 | 691 KB |

优化思路：

- FairyGUI package/object creation 是低频成本，但如果发生在 combat 或 spawn 中，会直接被玩家感知。
- HeadPad 和 buff badge 应尽量 pool/reuse。相比每次 spawn/status refresh 重建 UI object，更新已有 control 更合适。
- 文本刷新应 dirty-driven，避免每帧设置相同文本。
- 在 scene flow 允许的非敏感阶段 preload package bytes，并构造常用 UI component。
- FairyGUI 仍是 player UI。不要为了降低成本把 player-facing HUD 迁移到 Debug UI。

候选工作：

- 增加 CharacterTest UI warm-up stage，预热 combat HUD package、HeadPad component、buff badge controls 和常用 font/characters。
- `CharacterTestFairyGuiHeadPad` 缓存 control references，只更新变化的 HP、buff、scatter pressure 和 label 值。
- 生命周期允许时，复用 detached head pads 和 badge controls。
- 给 `CharacterTestUiRuntime.Refresh()` 增加廉价 dirty model，snapshot 未变化时跳过刷新。

## 分阶段计划

### Phase 0: 干净测量

目标：建立一个可复现、能排除 profiling environment 噪声的基线。

行动：

- 在 menu/scene warm-up 后，采集 clean Player-focused 或 Play Mode warm-run profile。
- 记录 frame count、scenario、character count、UI visibility、debug panel visibility，以及 MCP/Editor profiling tools 是否启用。
- 原始 Profiler Analyzer export 默认放在源码提交范围外，除非任务明确要求提交性能证据 artifact。
- 使用正常帧 median、P75/P95 和重复 Marker totals 做对比。

退出条件：

- 有可重复 profile scenario。
- 外部尖峰类型与 runtime 优化任务分开记录。
- 每个后续 phase 都能用同一个 capture scenario 复测。

### Phase 1: 移除稳态 Tick 分配

目标：初始化后，正常帧不应从 CharacterTest 可控高频路径产生托管分配。

行动：

- 审计 `GameManager.Update()`、`CharacterTestSlicePresentationServices`、runtime command/event diagnostics 和 view presentation 的 per-frame allocation。
- diagnostics string formatting 放到 debug visibility、warning/error 或 explicit capture 后面。
- per-frame temporary collection 改成 system-owned persistent buffer 或 caller-provided buffer。
- 如果触及 public/reusable API，同步 allocation 注释或 XML 说明。

退出条件：

- CharacterTest 高频路径可归因 GC 为 0 B，或明确证明并记录剩余 GC 属于外部/framework baseline。
- `GameManager.Update()` 和 `RuntimeHost.Tick()` upper quartile 下降，且功能没有被关闭。

### Phase 2: 优化 Motion 与 Combat Physics

目标：降低稳定的 2 ms+ motion bucket，并移除 lookup-heavy collider sync。

行动：

- 给角色和 movable natural objects 增加 dirty collider sync。
- 在 `CombatPhysicsWorld` 中引入或验证 O(1) body-id lookup。
- 拆分 static 与 dynamic broadphase maintenance。
- 复用 query/candidate buffers，避免 per-query allocation。
- 为 dirty sync 和 body lookup invalidation 增加聚焦 EditMode 覆盖。

退出条件：

- 相同场景下 `CharacterTest.CharacterRuntime.Motion` median 降到 1.5 ms 以下。
- collider sync 不再每帧 upsert 未变化 body。
- lookup 正确性有测试覆盖。

### Phase 3: 缓存 Spawn 与 Animation Artifact

目标：同一个角色包重复 spawn 时，不再重复 parse 和 rehash 不可变 animation data。

行动：

- 按 stable source identity 缓存 parsed animation set definitions 和 clip registry data。
- 缓存 computed animation set hashes。
- locomotion status logging 改成 cheap structured diagnostics。
- combat-sensitive spawn 前 prewarm selected character package 和 animation resources。

退出条件：

- `TryConfigureLocomotion()` 和 `TryCreateLocomotionDefinition()` 不再主导 repeated spawn。
- 成功路径上 `LogLocomotionStatus()` 不再是 top spawn Marker。
- 同角色 repeated spawn 在 clean scenario 中没有可见 hitch。

### Phase 4: 预热并复用 FairyGUI UI

目标：player-facing UI 创建和刷新不再卡住 spawn/combat。

行动：

- 在安全阶段 preload FairyGUI package bytes，并创建常用 HUD/HeadPad templates。
- 在生命周期清晰的地方 pool HeadPad 和 buff badge controls。
- `CharacterTestUiRuntime.Refresh()` 改成 dirty-driven。
- 如果 clean profile 复现 font texture request，则 prewarm combat label 常用动态字体字符。

退出条件：

- `UIPackage.CreateObject()` 和 HeadPad creation 不再发生在 combat-sensitive spawn 中。
- 稳态 `FairyGUI.Container.Update()` 保持稳定，不随未变化 UI state 增长。

### Phase 5: 回归 Harness 与文档回写

目标：让性能修复不回退。

行动：

- 增加轻量 performance smoke scenario 或 report，记录 motion、presentation、UI refresh 和 spawn 的 runtime counters。
- 先把 budget 做成 advisory threshold；在 scenario 稳定前不要让 CI 依赖严格 timing gate。
- 每个已接受优化 PR 后，把 before/after profile 证据回写到本文。

退出条件：

- 性能证据足够稳定，能在 PR 中审查。
- 后续 CharacterTest 修改能明确影响的是 steady Tick、spawn、UI，还是 Editor-only profiling noise。

## 非目标

- 不在本 CharacterTest runtime 计划中优化 MCPForUnity、EditorLoop 或 Unity Editor tool discovery。
- 不为了隐藏 player-facing UI 成本，把 FairyGUI UI 替换成 Debug UI 或 UI Toolkit。
- 不把 gameplay authority 移入 Unity view、Animator callback 或 GameObject state。
- 不把 `unsafe` 当作第一选择。先穷尽算法、数据布局、dirty flag、artifact cache 和 caller-owned buffer。
- 不提交本地 profiler export、临时脚本或分析缓存，除非任务明确要求提交性能证据 artifact。

## 2026-06-19 落地记录

### Phase 2 已落地：Combat Physics 查找索引与 Collider Dirty Sync

本轮在 `CombatPhysicsWorld` 内增加了 body 和 collider 索引：

- `CombatBodyId -> body index`
- `(CombatBodyId, CombatColliderId) -> AABB collider index`
- `(CombatBodyId, CombatColliderId) -> sphere collider index`
- `(CombatBodyId, CombatColliderId) -> capsule collider index`

优化目的：

- 将 `TryGetBody()`、`SetBodyPosition()`、`Upsert*Collider()`、`RemoveCollider()` 和 shape replacement 中的查找从线性扫描改为 O(1) 字典查找。
- 直接降低快照中 `CombatPhysicsWorld.FindBodyIndex()`、`CombatBodyId.Equals()` 和 collider sync 的重复成本。
- 索引只在注册、删除、shape replacement 和 `Clear()` 等拓扑变化时维护；查询和稳态 upsert 不引入临时集合。

正确性保护：

- `RemoveBody()` 后重建 body 与各 shape collider 索引，避免 `RemoveAt()` 造成后续 index 偏移。
- `RemoveCollider()` 和跨 shape upsert 会重建受影响 shape 的索引。
- 新增测试覆盖“删除中间 body 后剩余 body 查询/移动仍正确”和“删除/替换 shape 后 collider lookup 仍正确”。

本轮还给 movable natural object 的 Combat collider 同步增加 dirty gate：

- `CharacterTestMovableNaturalObjectState` 维护 `CombatColliderSyncVersion`，位置、snapshot/reset、形状/尺寸/材质来源变化时递增。
- `CharacterTestCharacterPhysicsRuntime` 持有 per-runtime 的 movable object sync cache，缓存生命周期绑定到当前 `CombatPhysicsWorld`。
- `CharacterTestMovableNaturalObjectSystem.SyncCombatCollider()` 在对象版本未变化，并确认 world 中 body 和当前形状 collider 仍存在时，跳过 `UpsertBody()` / `Upsert*Collider()`。
- 如果对象移动、snapshot 改变，或外部移除了 collider，同步路径会重新写入 world 并刷新缓存。

优化目的：

- 保留 motion tick 开始前和对象推进后的同步语义，但未变化对象不再每帧重复触发 body revision / topology revision。
- 降低 `CharacterTestMovableNaturalObjectSystem.SyncCombatCollider()`、`CombatPhysicsWorld.UpsertAabbCollider()` 和 broadphase invalidation 的稳态重复成本。
- dirty cache 只使用整数版本和 per-runtime 字典查找；稳态路径不创建临时集合，不引入 LINQ/反射/字符串构造。

正确性保护：

- 新增测试覆盖未变化对象二次 sync 不改变 `CombatPhysicsWorldStats`、移动后会同步 body position、外部移除 collider 后会恢复。

本轮还给角色 Combat collider 同步增加 per-entity dirty gate：

- `CharacterTestCharacterPhysicsRuntime` 持有 per-runtime 的角色 collider sync cache，记录 entity id、阻挡状态、center position 和 half extents。
- `CharacterTestCharacterMotionSystem.SyncCharacterCombatCollider()` 在角色同步状态未变化，并确认 `CombatPhysicsWorld` 中 body 和当前阻挡语义需要的 collider 状态仍存在时，跳过 `UpsertBody()` / `UpsertAabbCollider()` / `RemoveCollider()`。
- 如果角色移动、物理半径/高度或 `BlocksCharacters` 改变，或外部移除了 body/collider，同步路径会重新写入 world 并刷新缓存。

优化目的：

- 保留 motion tick 前后角色 collider 同步的顺序语义，但未变化角色不再每帧重复触发 body revision / topology revision。
- 降低 `CharacterTestCharacterMotionSystem.SyncCharacterCombatCollider()` 对 `CombatPhysicsWorld.UpsertAabbCollider()`、`RemoveCollider()` 和 broadphase invalidation 的稳态压力。
- dirty cache 只使用 struct 状态比较和 per-runtime 字典查找；稳态路径不创建临时集合，不引入 LINQ/反射/字符串构造。

正确性保护：

- 新增测试覆盖未变化角色二次 sync 不改变 `CombatPhysicsWorldStats`、移动后会同步 body position、外部移除 collider 后会恢复、`BlocksCharacters=false` 时会移除 collider 且后续未变化 sync 不重复修改 world。

本轮还收窄了 `CombatPhysicsWorld.QueryBroadphase()` 的排序范围：

- `Query(...)` 仍然把本次命中的结果追加到调用方提供的 `List<CombatQueryResult>`。
- 旧路径会对整个 result list 排序；如果调用方复用 buffer 并保留历史内容，会把历史结果也纳入排序成本。
- 新路径只对本次追加区间排序，保留公开 `SortResults(List<CombatQueryResult>)` 的全量排序语义不变。

正确性保护：

- 新增测试覆盖 result buffer 已有哨兵结果时，query 只排序本次追加命中，不移动既有内容。

本轮继续收窄 `CombatPhysicsWorld.QueryBatch()` 的调试分配和排序范围：

- `QueryBatch()` 复用 world 内部的 `List<CombatQueryResult>` scratch 来收集单个 query hit。
- 每个 batch result 只保存必要的 hit snapshot；零命中 query 复用 `Array.Empty<CombatQueryResult>()`，不再为每个空结果创建 hit list。
- `QueryBatch()` 只排序本次追加的 batch result 区间；调用方已有的 result buffer 内容不会被重新排序。
- 公开 `SortBatchResults(List<CombatPhysicsQueryBatchResult>)` 保持全量排序语义，但内部改用静态 comparer，避免保留旧的委托排序路径。

正确性保护：

- 新增测试覆盖 batch result buffer 已有哨兵结果时，query batch 只排序本次追加结果，不移动既有内容。
- 新增测试覆盖多个零命中 batch result 共享空 hit snapshot。

暂未改动：

- 没有删除 motion 前后的同步调用。当前顺序同时承担移动前碰撞、移动后 overlap/pressure 解析的语义；本轮只让未变化 movable object 和角色在这些调用中快速跳过。
- broadphase 已有 body revision 增量更新，本轮没有重复实现 static/dynamic 拆分。
- clean profile before/after 复测仍是后续项。

### Phase 3 已落地：Animation Set Definition 与 Clip Registry 缓存

本轮在 `CharacterRuntimeAnimationResources.TryCreateLocomotionDefinition()` 的读取路径增加 animation set definition 缓存。

缓存 key：

- animation set definition path
- package id
- 文件长度
- 文件最后写入 UTC ticks

优化目的：

- 同一角色包重复 spawn 时，避免重复 `File.ReadAllText()`、`MxAnimationCompiledArtifactJson.LoadSetDefinitions()`、`ParseSetDefinition()` 和 `MxAnimationSetDefinitionHasher.ComputeHash()`。
- 降低 `TryConfigureLocomotion()` / `TryCreateLocomotionDefinition()` 在 repeated spawn 中的低频 hitch。
- 文件内容变更后通过长度或写入时间变化自然失效。

约束：

- 缓存最多保留 32 个 animation set definition 文件，超出后清空，避免长期编辑会话无限增长。
- 当前缓存以文件长度和 UTC 写入时间作为失效依据；如果外部工具能原地写回但保持这两个值不变，需在后续任务中加入内容 hash 或显式 cache invalidation。

本轮还在 `CharacterRuntimeAnimationResources.CreateCatalogEntries()` 增加 animation clip registry metadata 缓存：

- clip registry path、文件长度和文件最后写入 UTC ticks 作为 cache key。
- 缓存保存解析后的 clip metadata，不缓存 `ResourceCatalogEntry`，因此 provider id、package id、labels 和 provider data 仍由每次调用按当前 binding 生成。
- registry 内重复 runtime resource key 只在解析阶段去重一次，后续重复调用不再重新 `File.ReadAllText()` / `JObject.Parse()` / 建立去重集合。
- 默认 catalog labels 改为静态数组，避免每个 entry 都创建一组相同 labels。

正确性保护：

- 新增测试覆盖重复 resource key 只发出一次，两次 `CreateCatalogEntries()` 调用得到相同 clip entry、hash、package id 和 provider data。

本轮还把 CharacterTest spawn 成功诊断改为显式开关：

- `CharacterTestRuntimeCharacterLoader` 默认不再为成功 spawn 路径组装 info 日志。
- 默认路径仍然执行必要配置，但跳过 `Default equipment configured`、bone sync、procedural rig snapshot、hit feedback install、input ownership 和 locomotion active 等成功诊断字符串。
- `CharacterRuntimeAnimationResources.TryConfigureLocomotion()` 增加 result logging 开关；默认保持原有外部行为，CharacterTest loader 在成功路径关闭结果日志，但配置缺失时仍会记录。

优化目的：

- 直接降低快照中 `LogLocomotionStatus()` 和 spawn 成功路径日志组装的低频 hitch。
- 避免仅为了日志创建 procedural rig snapshot、transform path 和多段字符串。
- 保留 warning/error 语义；需要排查 spawn 细节时可以显式打开成功诊断。

### Phase 4 已落地：Presentation / Animation 稳态去重

本轮在 `CharacterRuntimeLocomotionBlendController.ApplyAnimationBackend()` 中增加 last-applied locomotion blend 记录。

优化目的：

- 当 blend id、参数 scale、量化后的 X/Y 和 playback speed compensation 都未变化时，跳过重复 `UnityPlayablesAnimationBackend.SetBlend2D()`。
- 仍然每帧调用 `_animationBackend.Tick()`，保证 PlayableGraph 时间、pending load、fade 和 action 状态继续推进。
- 直接对应快照中 `CharacterRuntimeLocomotionBlendController.ApplyAnimationBackend()` 和 `UnityPlayablesAnimationBackend.SetBlend2D()` 的稳定重复成本。

正确性保护：

- `ConfigureAnimationBackend()`、`ReleaseAnimationBackend()`、blend point coordinate override、action crossfade、locomotion suppression 和 manual resume 都会使 last-applied cache 失效。
- action suppression 结束后，即使 locomotion 参数没有变化，也会重新提交一次 locomotion blend，避免停留在 action clip。
- 本改动只缓存 view/backend apply 状态，不改变 runtime authority。

本轮也调整了 `ApplyAnimatorParameters()`：

- 旧路径每帧按参数名遍历 `Animator.parameters`，再用字符串形式调用 `Animator.SetFloat()`。
- 新路径在 Animator、AnimatorController 或参数名变化时重建一次 float 参数缓存，记录参数是否存在和 `Animator.StringToHash()` 结果。
- 稳态 Tick 只使用缓存的 int hash 调用 `Animator.SetFloat(int, float)`，避免每帧读取 `Animator.parameters` 数组和做参数名查找。
- 同一 Animator 参数值完全未变化时，跳过重复 `SetFloat()`；Animator、controller 或参数名变化时会清空 last-applied 值，保证新绑定第一帧仍然写入。
- 缓存重建属于低频配置/绑定变化路径；正常 Tick 路径不新增临时集合、LINQ、反射或 Unity 对象查找。

正确性保护：

- 新增测试覆盖首次写入 3 个 locomotion float 参数、重复相同值只计入 skip、blend/speed 变化后会重新写入。

本轮还调整了 `CharacterTestFairyGuiHeadPad` 的 buff badge 绑定：

- 每个 badge 缓存静态展示签名：buff id、display/short name、stack、permanent state 和 polarity。
- 静态展示未变化时，不再重复 `DrawRoundRect()`、`ResolveBuffBadgeShortName()`、label 设置和 stack `ToString()`。
- 剩余时间条按像素宽度缓存，只有可见性或取整后的宽度变化时才重绘。
- 隐藏/显示只更新 `visible` 状态，不清空已缓存的 badge 内容；重新显示同一 buff 时可以直接复用上次内容。

正确性保护：

- 扩展 HeadPad badge 测试，覆盖相同输入重复 `Sync()` 不增加 badge update count，剩余比例变化时只更新对应 badge，并保持现有文字、stack、remaining bar 和 display mode 行为。

本轮还给 `CharacterTestFairyGuiHeadPad.Create()` 增加小型控件树池：

- runtime 创建的 HeadPad 在 `Dispose()` 时从 HUD root 脱离并回到静态池，最多保留 8 个。
- 再次 `Create()` 时优先复用仍然有效的 FairyGUI root / graph / text controls，避免 repeated spawn/despawn 时重复构造 HeadPad 和 buff badge control tree。
- 归还池前会清空 last-applied 状态、可见性、动画计时和 badge signature；重新租出后仍按正常 `Sync()` 流程重建展示内容。
- `CreateDetachedForTests()` 仍使用非池化路径，测试 reset 会清空池，避免 EditMode 测试之间保留 detached FairyGUI 对象。

正确性保护：

- 新增测试覆盖 `Create()` 后 `Dispose()` 再 `Create()` 会复用同一个 HeadPad root，并重新挂回 HUD layer。

本轮继续给 HeadPad 控件树池增加预热阶段：

- 新增 `CharacterTestFairyGuiHeadPad.PrewarmPool()`，在低频初始化阶段构造 detached HeadPad 控件树并放入池。
- 预热对象不挂到 HUD layer，也不进入 active pad list；首次角色 view 需要 HeadPad 时，`Create()` 直接从池中租用并挂回 HUD。
- `CharacterTestUiRuntime.Configure()` 在启用任意 FairyGUI surface 时默认预热 3 个 HeadPad 控件树，避免首批角色 spawn/combat 时集中创建 buff badge / bar / text 控件。
- 预热池仍受 `MaxPooledPads=8` 限制；重复 Configure 只补齐缺口，不无限增长。

正确性保护：

- 新增测试覆盖预热产生 detached pooled pads，`Create()` 会租用池对象并挂回 HUD layer。
- 新增测试覆盖 `CharacterTestUiRuntime.Configure()` 启用 FairyGUI 时会触发默认 HeadPad pool 预热，且不产生 active pad。

本轮还给 `CharacterTestRuntimeCharacterView.SyncTransform()` 增加 pose dirty gate：

- 非 network smoothing 路径先比较当前 Unity `Transform` 的 position / rotation。
- pose 未变化且当前不处于 network presentation smoothing 时，跳过 `SetPositionAndRotation()` 和 HeadPad world anchor 同步。
- 从 network smoothing 回到权威 pose 时仍会清理 network presentation 状态；如果 pose 已经相同，则不做多余 transform 写入。

正确性保护：

- 新增测试覆盖首次 pose 变化会 apply、重复相同 pose 不增加 apply count、rotation 改变会再次 apply。

本轮还收窄了 presentation 路径的 `Camera.main` 查找：

- 新增 `CharacterTestMainCameraCache`，缓存 active/enabled 且 tag 为 `MainCamera` 的 Unity Camera。
- `CharacterTestCharacterUnityViewBridge.TickPresentation()`、`CharacterTestRuntimeCharacterView.TickPresentation()` 默认 fallback、HeadPad screen position 更新、`CharacterTestFairyGuiFloatingCombatTextHost` 和 `CharacterTestFloatingCombatTextProjector` 改为先复用缓存相机。
- 缓存相机失效、禁用、销毁或不再带 `MainCamera` tag 时才重新调用 `Camera.main`。
- 一次性 FairyGUI stage/camera stack 配置仍保留直接 `Camera.main`，因为它不属于 per-frame hot path。

正确性保护：

- 新增测试覆盖有效 MainCamera 会被复用，缓存相机 disabled 后会刷新到新的 active MainCamera。

本轮还给 `CharacterTestRuntimeCharacterView` 的 HeadPad 锚点解析增加缓存：

- `SyncCombatState()` 仍然每次按最新 runtime combat state 同步 HeadPad，但 `ResolveHeadPadWorldPosition()` 不再每帧重新做 `GetComponentInChildren<Animator>()` 和递归 `head` 节点查找。
- 找不到 head bone / head transform 时，renderer fallback 仍然每次读取当前 renderer bounds；但 renderer 组件数组只在第一次 fallback 时解析一次，不再每帧 `GetComponentsInChildren<Renderer>()` 创建新数组。
- 缓存生命周期绑定到当前 `CharacterTestRuntimeCharacterView` / spawned instance；`Release()` 会清空缓存，下一次 spawn 使用新的层级解析。

优化目的：

- 进一步降低 `CharacterTestRuntimeCharacterView.TickPresentation()` / `SyncCombatState()` 周边的表现层稳态成本。
- 保留 HeadPad 锚点随 Transform / Renderer bounds 变化更新的表现语义，只移除稳定层级的重复组件查找和数组分配。

正确性保护：

- 新增测试覆盖有 head transform 时重复 combat state sync 只解析一次 HeadPad 锚点，且不会进入 renderer fallback。
- 新增测试覆盖无 head transform 时 renderer fallback 的组件数组只解析一次。

本轮还给 VFX socket anchor 解析增加缓存：

- `CharacterTestRuntimeCharacterView.TryResolveVfxAnchor()` 首次按 `CharacterBoneRuntimeSync.Bindings` 构建 `socketId -> Transform` 映射。
- 后续同一 view 生命周期内，活动 VFX 每帧同步锚点时只做字典查找和 Transform position/forward 读取，不再为 socket id `ToString()`，也不再线性扫描 binding 列表。
- cache 支持现有数字 binding id，并接受 `socket:<id>` 形式；`Release()` 会清空 cache。

优化目的：

- 降低 `CharacterTestSlicePresentationServices.TickPresentation()` 中 VFX anchor sync 的逐帧绑定解析成本。
- 保持 VFX anchor 位置随 Transform 变化更新；缓存的是稳定绑定关系，不缓存坐标。

正确性保护：

- 新增测试覆盖 `socket:7` binding 可解析为 numeric socket id，重复 `TryResolveVfxAnchor(7, ...)` 只构建一次缓存，且第二次仍读取最新 Transform position。

本轮还给 `CharacterTestUiRuntime.Refresh()` 增加 modal panel 低频调度：

- MainMenu、NetworkPanel、SettingsPanel、AbilityBindingPanel 和 RuntimeAiBehaviorLoadoutPanel 先通过 `IsOpen`、`RefreshAttemptCount` 和 `Time.frameCount` 做廉价判定。
- 闭合面板只允许一次初始 refresh 检查；之后不再每帧打进 host。
- 已打开面板按 6 帧间隔刷新，保留 host 内部的 signature skip 和 UI command 后的下一轮同步。
- `Open()` / `Toggle()` 仍然同步调用各自 host 的 refresh，因此用户操作打开面板不会等调度间隔。
- StoryDialog 和 FloatingCombatText 没有做同样降频：StoryDialog 需要及时响应 story snapshot / 菜单压制，FloatingCombatText 需要逐帧消费 combat output 和推进生命周期。

正确性保护：

- 新增 scheduler helper 测试，覆盖闭合面板跳过、打开面板按帧间隔刷新。
- 扩展 `CharacterTestUiRuntime` 设置面板测试，覆盖闭合的 MainMenu / NetworkPanel / SettingsPanel 不会在连续 `Refresh()` 中重复打进 host。

本轮继续给 player modal 打开状态增加转变门：

- 首次进入任意 player modal 时，仍会关闭 Combat HUD 并清空 FloatingCombatText。
- modal 持续打开期间，不再每帧重复调用 `CombatHud.Close()` 和 `FloatingCombatText.Clear()`。
- modal 关闭后，Combat HUD 恢复逻辑仍会在下一次 `Refresh()` 立即运行；再次进入 modal 会重新触发一次 close/clear。

正确性保护：

- 新增纯逻辑测试覆盖 modal 首次进入、持续打开、退出、再次进入的 clear gate 行为。

本轮还把 Combat HUD 的 FairyGUI view 改为 keep-alive：

- `CharacterTestFairyGuiCombatHudHost` 的 runtime contract 现在设置 `KeepAlive=true`、`CloseOnSceneChange=true`。
- player modal 打开时的 `Close()` 只隐藏并缓存 HUD view；modal 关闭后的下一次 `Refresh()` 会复用同一个 view 并重新 bind view model，避免反复 `UIPackage.CreateObject()`。
- `Release()` 不走 keep-alive close，而是调用 navigator 的 scene-owned release 路径释放 open/cached HUD view，避免 UI runtime 销毁时留下 package/component。

正确性保护：

- 新增合同测试覆盖 Combat HUD runtime contract 必须 keep-alive、scene-owned 且非 modal。

本轮继续补上 CharacterTest UI runtime 的 FairyGUI package 预加载：

- `CharacterTestUiRuntime.Configure()` 在创建共享 `MxFairyGuiHost` 后，按当前启用的 generated FairyGUI surface 收集 package descriptor。
- 同一 package id 只预加载一次。Combat HUD、Main Menu、Network Panel、Settings Panel 和 Ability Binding Panel 已分别使用 `MxCharacterTestCombatHud`、`MxCharacterTestUi`、`MxCharacterTestNetworkUi`、`MxCharacterTestSettingsUi`、`MxCharacterTestAbilityUi`；Runtime AI Planner、Arena 和 Runtime AI Behavior Loadout 等 legacy 面板仍使用 `MxCharacterTestHud`。每个包独立去重和持有引用，不再假设所有 CharacterTest UI 共享一个 package。
- 预加载使用现有 `MxFairyGuiResourceBridge.LoadPackage()` + `MxFairyGuiHost.EnsurePackage()` 路径，成功后由 UI runtime 持有 package 引用，后续 host open 时复用已注册 package。
- `Release()` 会在各 host 释放后按 package id 归还预加载引用，保持 ResourceManager handle 和 FairyGUI package 生命周期闭合。
- 资源不完整或加载失败时只记录 preload status，host 自身的 catalog diagnostics / open failure 仍按原路径处理，不因为预加载失败关闭 UI runtime。

正确性保护：

- 新增集成测试覆盖启用 Combat HUD、Story Dialog 和 Main Menu 时只预加载两个唯一 package，并在 `Release()` 后释放预加载引用。

本轮还给 FloatingCombatText refresh 增加同帧空闲跳过：

- `CharacterTestFairyGuiFloatingCombatTextHost.Refresh()` 记录已消费的 slice frame。
- 同一 slice frame 内，如果 combat output 已经消费过且浮字队列为空，后续 `Refresh()` 会直接返回，不再重复 normalize root order、重新消费输出或解析相机。
- 有新 output 或排队浮字时仍按原语义生成 UI 对象；`TickPresentation()` 仍每帧推进 active floating text 的生命周期和屏幕投影，不把浮字动画降频。

正确性保护：

- 扩展浮字 UI runtime 测试，覆盖同帧第二次 `Refresh()` 会被计入 skipped refresh，同时 active floating text 仍由 presentation tick 正常过期并释放。

本轮还补上 StoryDialog closed-idle dirty gate：

- `CharacterTestFairyGuiStoryDialogHost.Refresh()` 在 StoryDialog 已关闭、Story snapshot 引用未变、文本 provider locale/revision 未变时，跳过重复 view model build 和 `Close()`。
- Story snapshot 仍由 `CharacterTestSliceServices` 按 Story event 失效；Story runtime 发生 beat / choice / presentation 变化后会得到新 snapshot，下一次 refresh 会重新评估。
- 已打开 StoryDialog 的既有 view-model signature skip 保持不变；UI runtime 仍每帧调用 StoryDialog host，以保证 story snapshot 或菜单压制变化能及时响应。

正确性保护：

- 扩展 StoryDialog host 测试，覆盖 Story 完成后第一次 refresh 关闭对话框，第二次同 snapshot idle refresh 计入 skipped，且不增加 successful refresh。

暂未改动：

- 完整 UI runtime dirty model 仍未实现；本轮已完成 generated FairyGUI package bytes 预加载、HeadPad 控件树池化/默认预热，让 Combat HUD 在 modal close/reopen 间复用 view，让 FloatingCombatText 跳过同帧空闲重复 refresh，并让 StoryDialog 跳过同 snapshot 的 closed-idle refresh。
- StoryDialog host 仍由 UI runtime 每帧调用；本轮只跳过 closed-idle 重复 build/close，不降低 story snapshot 或菜单压制变化的响应频率。FloatingCombatText 只跳过同一 slice frame 内无新输出、无排队浮字的空闲重复 refresh，不对 active 浮字 presentation tick 降频。

### Phase 5 已落地：Runtime / UI 性能计数器

本轮补齐了用于回归和后续 clean profile 对照的低频性能计数面。

Runtime 计数器：

- 新增 `CharacterTestRuntimePerformanceCounterSource`，source name 为 `CharacterTestRuntime`。
- `GameSlice.CreateDebugSourceRegistry()` 现在会注册 `CharacterTestRuntimePerformance`，与已有 `CharacterTestInteractionPerformance` 并列。
- 计数覆盖 runtime 实体数、movable object 数、spawn pending / last processed / last spawned / last failed / total spawned / total failed。
- 计数覆盖 view active count、上一帧 view sync request / applied / miss / transform apply，以及累计 sync / miss / transform apply。
- 计数覆盖 `CombatPhysicsWorldStats` 的 world revision、body count、collider count、static collider count，以及角色 / movable collider dirty cache 数量。

UI 计数器：

- `CharacterTestUiRuntime.CreatePerformanceCounterSnapshot()` 可生成 `CharacterTestUi` 计数快照。
- 计数覆盖 Combat HUD refresh attempt / success / skipped。
- 计数覆盖 FloatingCombatText refresh attempt / skipped、active、queued、spawned、dropped。
- 计数覆盖 MainMenu / NetworkPanel / SettingsPanel / AbilityBindingPanel / RuntimeAiBehaviorLoadoutPanel refresh attempt / success / skipped。
- 计数覆盖 StoryDialog success / skipped；StoryDialog 仍保持每帧 refresh 语义，没有做降频。

优化目的：

- Phase 2-4 的 dirty gate、缓存和 UI 调度需要能在同一 profile scenario 中观察是否生效，而不是只依赖 Marker 名称和人工推断。
- Runtime counter 进入现有 DebugSource registry，便于 Debug UI / snapshot aggregator 读取。
- UI counter 不从 `CharacterTestSliceServices` 注册，因为 `CharacterTestUiRuntime` 不属于 SliceServices 生命周期；本轮新增 `CharacterTestUiRuntime.RegisterPerformanceDebugSource()`，由 UI bootstrap 或 owner 显式把 `CharacterTestUiPerformance` 合入同一个 DebugSource registry。
- 新增 `CharacterTestPerformanceCounterReport.CreateText()`，把 runtime、UI 和 interaction counter snapshots 合成稳定排序的文本 smoke report，便于后续 profile 前后把 spawn、view/presentation、physics/interaction 和 UI refresh 计数一起归档。
- 新增 `CharacterTestPerformanceSmokeScenario.Run()` / `GameSlice.RunPerformanceSmokeScenario()`，可按固定 warm-up frame count、capture frame count 和 delta time 推进同一个 `GameSlice`，再导出带 start frame、capture start frame、end frame、warm-up/capture 帧数和 runtime / interaction / optional UI counters 的稳定文本 report。
- 新增 `CharacterTestPerformanceSmokeReport.CreateText()`，由 CharacterTest 程序集内部创建无 view 的固定帧 smoke scenario 并返回文本 report；Editor 程序集只负责菜单和写文件，不直接依赖 `GameSlice` 构造链上的 runtime service 细节。
- 新增 `GameSlice.CreatePerformanceSmokeReport()`，供未来 Editor menu、Play Mode smoke 或手动 profiler capture 在同一帧直接抓取 counter report。
- 新增 Editor 菜单 `MxFramework/CharacterTest/Performance/Write Smoke Report`，并注册到 Framework Manager 的 `CharacterTest` 分组。该入口默认运行无 view 的固定帧 smoke scenario，并把 report 写入 `Temp/CharacterTestPerformanceSmokeReport.txt`，便于手动 profile 复测时保留同一 scenario 的 counter 旁证。
- 新增 Editor 菜单 `MxFramework/CharacterTest/Performance/Write Warm Run Timing Report`，默认运行无 view 的 clean editor warm-run scenario，并把固定帧耗时分位数、采样帧线程分配和 runtime / interaction counters 写入 `Temp/CharacterTestPerformanceWarmRunTimingReport.txt`。该入口用于当前编辑器会话的轻量证据采集，不替代 Player-focused Profiler Analyzer 前后对比。
- 新增 Editor 菜单 `MxFramework/CharacterTest/Performance/Run Focused Unity Tests`，运行本轮性能优化相关的 EditMode 聚焦测试，并把 Unity Test Runner 汇总写入 `Temp/CharacterTestPerformanceUnityTestReport.txt`，用于在 MCP session 不可用时保留可审计的 Unity 测试证据。
- 新增 Editor 菜单 `MxFramework/CharacterTest/Performance/Run Play Mode Warm Run Test`，运行会进入 Play Mode 的 CharacterTestScene warm-run UnityTest。该测试关闭主菜单、等待正式 `GameManager` / `GameSlice` 就绪，按真实场景帧 warm-up / capture，并输出 `Temp/CharacterTestPerformancePlayModeWarmRunReport.txt`、`Temp/CharacterTestPerformancePlayModeProfilerRecorderReport.txt` 与 TestRunner 汇总 `Temp/CharacterTestPerformancePlayModeUnityTestReport.txt`。
- 新增 Editor 菜单 `MxFramework/CharacterTest/Performance/Build And Run Player Warm Run`，构建显式包含 `Assets/Scenes/CharacterTestScene.unity` 的 Development Player，并通过命令行参数 `-character-test-performance-report-dir` 激活 Player 内的 warm-run report runner。该入口输出 `Temp/CharacterTestPerformancePlayerReports/CharacterTestPerformancePlayerWarmRunReport.txt`、`CharacterTestPerformancePlayerProfilerRecorderReport.txt`、`CharacterTestPerformancePlayerRunReport.txt` 与构建/进程汇总 `Temp/CharacterTestPerformancePlayerBuildRunReport.txt`。
- Player report runner 在正式 `GameManager.Update()` 帧边界采样，先 warm-up 60 帧，再 capture 120 帧；同时通过 `ProfilerRecorder` 采集与 PlayMode report 相同的 12 个 CharacterTest 自定义 Marker。runner 只在命令行参数存在时创建，普通 gameplay 路径只保留一次 null check。
- 本地 Standalone 构建环境缺少 IL2CPP module，因此 Player warm-run 菜单会在本次 Development Player 构建期间临时切到 Mono2x scripting backend，并在构建结束后恢复原设置。该行为属于测试入口的环境适配，不应把 ProjectSettings 的临时后端变更留在提交中。
- Player warm-run 首次实跑暴露了退出释放阶段 `AssetBundle.Unload()` 的 native object 失效风险；本轮在 `AssetBundleProvider`、`SceneAssetBundleProvider` 和 `RemoteBundleProvider` 的 release 路径增加 native-alive guard，避免 Player 退出或重复释放时对已销毁 bundle 调用 `Unload()`。

分配与高频路径约束：

- Tick / Refresh 路径只维护整数计数，不创建临时集合，不引入 LINQ、反射或字符串构造。
- `FrameworkPerformanceCounterSnapshot` 创建属于显式诊断 capture；`CharacterTestUiRuntime.CreatePerformanceCounterSnapshot()` 标注为 `AllocByDesign`，不应从 per-frame hot path 调用。
- counter 样本的 `Cost` 标记为 `NoAlloc`，表示被观察计数的维护路径不分配；snapshot 对象本身仍是诊断分配。

正确性保护：

- 新增测试覆盖 Runtime counter 静态映射、排序和 `NoAlloc` cost 标记。
- 新增测试覆盖 UI counter 静态映射和 `NoAlloc` cost 标记。
- 扩展 DebugSource registry 测试，确认 `CharacterTestRuntimePerformance` 已注册并能导出 runtime entity counter。
- 扩展 DebugSource registry 测试，确认 UI runtime 可显式注册 `CharacterTestUiPerformance`，重复注册不会制造重复 source，并能导出 UI refresh counter。
- 新增 smoke report 测试，覆盖默认 scenario、frame clamp、source 排序、null snapshot 跳过，以及 spawn / view / UI refresh counter 输出。
- 新增 smoke scenario 测试，覆盖固定 warm-up/capture 帧推进、frame 元数据、runtime / interaction / UI counter 合并输出，以及 explicit report capture 不进入 Tick 热路径。
- 新增 Editor 菜单测试，覆盖 `Temp` report 写入、固定 frame 元数据，以及 runtime / interaction counter 输出。
- 新增 warm-run timing Editor 菜单测试，覆盖 `Temp` report 写入、clean editor warm-run 元数据、耗时分位数、采样帧线程分配统计和 runtime / interaction counter 输出。
- 新增 PlayMode warm-run UnityTest，覆盖正式 `CharacterTestScene` 可进入 Play Mode、`GameManager` 创建 `GameSlice`、主菜单可关闭、场景帧 warm-up/capture report 可写入 Temp，并导出 runtime / interaction / UI counters。
- 同一个 PlayMode warm-run UnityTest 还通过 `ProfilerRecorder` 采集 12 个关键自定义 Marker：`CharacterTest.GameManager.Simulation`、`SlicePresentation`、`UiRefresh`、`UiTick`、`CharacterTest.GameSlice.HostTick`、`CharacterTest.RuntimeModule.CharacterTest.CharacterRuntime`、`CharacterTest.CharacterRuntime.Motion`、`ViewSync`、`AnimationSync`、`CharacterTest.UI.CombatHudRefresh`、`FloatingTextRefresh` 和 `StoryDialogRefresh`。报告记录每个 Marker 的可用状态、sample count、median、mean、P75/P90/P95 和 max，便于和 Profiler Analyzer marker table 做人工对照。
- 新增 Player report runner 命令行解析测试，覆盖 `-character-test-performance-report-dir` 能正确解析报告目录，确保普通 Player 启动不会误触发性能采集。

仍需后续复测 / 未声称：

- counter 和 marker timing 已能通过固定帧 smoke scenario、clean editor warm-run timing scenario、正式 CharacterTestScene PlayMode warm-run UnityTest、Development Player warm-run、`ProfilerRecorder` marker report 和 Editor 菜单自动采集稳定文本 report。
- 当前 Player report 是 Player-focused 自报 warm-run 与 `ProfilerRecorder` 文本证据，不等同于 Profiler Analyzer CSV before/after artifact。正式收益结论仍需按 Phase 0 scenario 导出优化前后 Profiler Analyzer frame/marker CSV 并归档。
- 未设置严格 timing budget。后续要先稳定 Phase 0 scenario，再把 advisory threshold 纳入 PR 审查。
- 不用单次 warm-run 取代回归预算。后续收益仍以 clean profile before/after 的正常帧 median、P75/P95、Marker total 和 GC 对比为准。

### Interaction Query 审计结果

本轮审计了 `CharacterTestInteractionPhysicsSystem` 和 `CharacterTestInteractionConsumerDispatcher`：

- `CharacterTestInteractionPhysicsSystem.Tick()` 已在 `interactions == null`、`interactions.Count == 0`、physics runtime 未 ready 或 world 缺失时直接返回。
- 系统只对 `CharacterTestInteractionKind.PhysicalField` 事件发起 Combat physics query，非 physical field 只做轻量循环和计数。
- dispatcher 层保留空 interactions 仍调用 consumer 的现有语义；已有测试覆盖该行为，不能为了少一次 consumer dispatch 破坏 VFX / Audio / diagnostics consumer 的生命周期入口。

因此本轮没有在 dispatcher 层新增空 interactions gate。后续如果 clean profile 证明 interaction dispatch 自身仍是问题，应先设计 consumer-level idle contract，再更新测试和接口语义。

### 当前验证结果

- `dotnet build MxFramework.Tests.csproj -v minimal`：通过；44 个既有 Unity serialized-field `CS0649` warnings，0 errors。
- `dotnet test MxFramework.Tests.csproj --no-build -v minimal`：退出码 0，但当前生成的 Unity csproj 未输出 NUnit 明细；不把它计为 Unity EditMode 测试覆盖。
- `git diff --check`：通过。
- `python3 Tools/Harness/check_code_standards.py --all --summary-only`：通过执行，150 个 report-only finding，集中在既有 `ZString.cs` unsafe baseline；本轮未新增 unsafe。
- `Tools/Harness/run_lightweight_checks.sh`：通过；changed path count 58，changed files code standards 扫描 46 个 first-party code files，无 findings。
- Unity 6000.5.0f1 Editor 通过 `Assets > Refresh` 完成脚本编译，日志显示 `*** Tundra build success`，更新 `MxFramework.Game.CharacterTest`、`MxFramework.Game.CharacterTest.Editor` 和 `MxFramework.Tests` 后重载 assemblies。
- Editor 菜单 `MxFramework/CharacterTest/Performance/Write Smoke Report` 已在当前 Unity Editor 实跑，生成 `Temp/CharacterTestPerformanceSmokeReport.txt`。报告记录 `scenario=character-test.editor-menu.smoke`、`startFrame=1`、`captureStartFrame=61`、`frame=181`、`warmupFrames=60`、`captureFrames=120`，并输出 `CharacterTestInteraction` 与 `CharacterTestRuntime` 两组 counter。
- Editor 菜单 `MxFramework/CharacterTest/Performance/Write Warm Run Timing Report` 已在当前 Unity Editor 实跑，生成 `Temp/CharacterTestPerformanceWarmRunTimingReport.txt`。报告记录 `scenario=character-test.editor-menu.warm-run-timing`、`profileKind=clean-editor-warm-run`、`startFrame=1`、`captureStartFrame=61`、`frame=181`、`warmupFrames=60`、`captureFrames=120`、`timingSamples=120`；本次 clean editor warm-run 结果为 `elapsedMs.median=0.115`、`elapsedMs.p75=0.125`、`elapsedMs.p90=0.161`、`elapsedMs.p95=0.189`、`elapsedMs.max=1.181`，采样帧线程分配 `allocatedBytes.total=0` / `p95=0` / `max=0`，并输出 `CharacterTestInteraction` 与 `CharacterTestRuntime` 两组 counter。
- Editor 菜单 `MxFramework/CharacterTest/Performance/Run Focused Unity Tests` 已在当前 Unity Editor 实跑，生成 `Temp/CharacterTestPerformanceUnityTestReport.txt`。报告记录 `scenario=character-test.performance.focused-editmode`，`requestedTests=25`，`summary.passed=25`，`summary.failed=0`，覆盖 Combat physics lookup、animation resource cache、locomotion Animator parameter skip、runtime/view dirty gate、FairyGUI HeadPad/HUD、StoryDialog、modal scheduler、smoke report、warm-run timing report 和 Player report runner 命令行解析。
- Editor 菜单 `MxFramework/CharacterTest/Performance/Run Play Mode Warm Run Test` 已在当前 Unity Editor 实跑，生成 `Temp/CharacterTestPerformancePlayModeUnityTestReport.txt`、`Temp/CharacterTestPerformancePlayModeWarmRunReport.txt` 与 `Temp/CharacterTestPerformancePlayModeProfilerRecorderReport.txt`。TestRunner 汇总记录 `mode=EditMode(EnterPlayMode)`、`requestedTests=1`、`summary.passed=1`、`summary.failed=0`。PlayMode warm-run 报告记录 `scenario=character-test.playmode-scene.warm-run`、`profileKind=playmode-scene-warm-run`、`startFrame=1`、`captureStartFrame=10`、`frame=21`、`warmupFrames=60`、`captureFrames=120`、`timingSamples=120`；本次场景帧结果为 `elapsedMs.median=0.916`、`elapsedMs.p75=1.154`、`elapsedMs.p90=3.408`、`elapsedMs.p95=4.464`、`elapsedMs.max=5.761`，采样帧线程分配 `allocatedBytes.total=0` / `p95=0` / `max=0`，并输出 `CharacterTestInteraction`、`CharacterTestRuntime` 与 `CharacterTestUi` 三组 counter。
- 同次 PlayMode warm-run 的 `ProfilerRecorder` 报告记录 `scenario=character-test.playmode-scene.profiler-recorder`、`profileKind=playmode-profiler-recorder`、`sampledFrames=120`、`markerCount=12`，12 个 Marker 均 `available=true`。关键当前值：`CharacterTest.GameManager.Simulation` median `0.56ms` / P95 `1.786ms`，`CharacterTest.GameSlice.HostTick` median `0.531ms` / P95 `1.764ms`，`CharacterTest.RuntimeModule.CharacterTest.CharacterRuntime` median `0.475ms` / P95 `1.718ms`，`CharacterTest.CharacterRuntime.Motion` median `0.338ms` / P95 `1.583ms`，`CharacterTest.GameManager.SlicePresentation` median `0.167ms` / P95 `0.196ms`，`CharacterTest.GameManager.UiRefresh` median `0.041ms` / P95 `0.136ms`，`CharacterTest.UI.CombatHudRefresh` P95 `0.095ms`。
- Editor 菜单 `MxFramework/CharacterTest/Performance/Build And Run Player Warm Run` 已在当前 Unity Editor 实跑，生成 `Temp/CharacterTestPerformancePlayerBuildRunReport.txt` 和 `Temp/CharacterTestPerformancePlayerReports/` 下的三份 Player report。构建汇总记录 `scenario=character-test.player-development.editor-build-run`、`buildResult=Succeeded`、`buildSizeBytes=333867846`、`durationSeconds=9.376`、`processExited=true`、`processExitCode=0`，三份 report 均存在。首次构建曾因本地缺少 Standalone IL2CPP module 失败；菜单已改为本次测试构建期间临时使用 Mono2x，并在结束后恢复原后端设置。
- Development Player warm-run 报告记录 `scenario=character-test.player-development.warm-run`、`profileKind=development-player-game-manager-update`、`startFrame=1`、`captureStartFrame=62`、`frame=182`、`warmupFrames=60`、`captureFrames=120`、`timingSamples=120`；本次 Player 帧结果为 `elapsedMs.median=0.594`、`elapsedMs.p75=0.659`、`elapsedMs.p90=0.689`、`elapsedMs.p95=0.713`、`elapsedMs.max=0.866`，采样帧线程分配 `allocatedBytes.total=0` / `p95=0` / `max=0`，并输出 `CharacterTestInteraction`、`CharacterTestRuntime` 与 `CharacterTestUi` 三组 counter。
- 同次 Development Player 的 `ProfilerRecorder` 报告记录 `scenario=character-test.player-development.profiler-recorder`、`profileKind=development-player-profiler-recorder`、`sampledFrames=120`、`markerCount=12`，12 个 Marker 均 `available=true`。关键当前值：`CharacterTest.GameManager.Simulation` median `0.387ms` / P95 `0.5ms`，`CharacterTest.GameSlice.HostTick` median `0.371ms` / P95 `0.486ms`，`CharacterTest.RuntimeModule.CharacterTest.CharacterRuntime` median `0.315ms` / P95 `0.409ms`，`CharacterTest.CharacterRuntime.Motion` median `0.16ms` / P95 `0.216ms`，`CharacterTest.GameManager.SlicePresentation` median `0.078ms` / P95 `0.103ms`，`CharacterTest.GameManager.UiRefresh` median `0.031ms` / P95 `0.08ms`，`CharacterTest.UI.CombatHudRefresh` P95 `0.047ms`。
- Player 首次成功运行时在退出阶段暴露过 `AssetBundleProvider.ReleaseBundle()` / `AssetBundle.Unload()` 的 `NullReferenceException`；release guard 修复后再次运行 Player warm-run，`Player.log` 新增段未再出现 `NullReferenceException`、`AssetBundleProvider` release error 或 matching exception。
- Unity MCP 当前仍没有连接到 Editor session；同项目 Unity Editor 进程存在，`mcp-for-unity` HTTP server 可启动，但 `mcpforunity://instances` 持续返回 `instance_count=0`，`mcpforunity://editor/state` 返回 `no_unity_session`。重启 HTTP server 后 Unity 插件仍未注册 session。本轮 Unity 菜单验证通过 AppleScript 触发，没有启动 BatchMode 抢占 `Library`，因此没有 MCP 驱动的 Unity NUnit / PlayMode 运行结果。
- 性能收益仍需使用本文 Phase 0 的 clean profile scenario 做 Profiler Analyzer before/after CSV 复测；本节只记录代码层优化、Editor/PlayMode warm-run 和 Development Player warm-run 证据已落地，不把未复测收益写成最终结论。

### 2026-06-20 ProfileAnalyzer 修复记录与下一阶段优化内容

本节基于 `C:/Users/WuGC/Videos/markerTable.csv` 的最新 Profiler Analyzer marker table。该文件最后修改时间为 2026-06-20 15:22:24，当前 CSV 中 5938 条 marker 全部来自 frame 169；以下结论只描述这一帧，不把它外推为全局均值。

#### 已确认修复：Tornado active field 不再是最严重路径

- `CharacterTest.GameManager.Simulation` 当前 frame 169 total 为 `9.9995ms`，`CharacterTest.GameSlice.HostTick` 为 `9.8532ms`，其中 `CharacterRuntime.Motion` 已成为主要成本。
- `CharacterTest.RuntimeModule.CharacterTest.InteractionConsumers` 当前为 `1.0512ms`，低于 Phase 5 设定的 `<= 1.2ms` 验收目标；单次最大约 `0.5334ms`。
- `CharacterTestInteractionPhysicsSystem.QueryField()` 当前为 `0.7968ms`，其中 fast path marker 分解为：
  - `CharacterTest.InteractionPhysics.TornadoIndexRebuild`: `0.4369ms`
  - `CharacterTest.InteractionPhysics.TornadoFieldQuery`: `0.3381ms`
  - `CharacterTestDynamicFieldTargetIndex.QueryVerticalCapsule`: `0.3366ms`
- 该帧未再出现旧路径的 `TryCapsuleAabb` / `CheckSegmentAabbEdges` marker；`CombatPhysicsWorld.QueryCapsule` 仍有 `0.5216ms` / 3 次调用，但它不是 Tornado attraction fast path 的主成本。

#### 需要补上的 Tornado 收尾修复

- `TornadoFieldQuery` 当前仍有 `256B` managed allocation，来源集中在 `CharacterTestDynamicFieldTargetIndex.QueryVerticalCapsule` 之后的排序路径：`List<T>.Sort`、`Array.Sort`、`ArraySortHelper<T>.Sort`。
- 修复方式：把 hot path 的 range sort 改为 caller-owned result buffer 上的 no-allocation stable insertion sort，只排序本次追加的 `[start, count)` 区间，排序规则继续复用现有 `CombatQueryResultComparer` 语义。
- 预期收益：时间收益很小，主要目标是把 Tornado fast path 稳态 GC 清零，避免 profile 中 `TornadoFieldQuery` 被 allocation 标红。
- 回归覆盖：保留/补充 Tornado 专用查询的目标集合一致性、source exclude、垂直范围、边界半径、缺失 collider、排序稳定性和 warm-up 后 no-GC 测试。

#### 当前最严重问题：Motion / CombatKinematicMotor 碰撞路径

frame 169 的剩余热点已经转移到 Motion 和 kinematic collision：

- `CharacterTest.CharacterRuntime.Motion`: `6.5939ms`
- `CharacterTestCharacterMotionSystem.Tick()`: `6.5924ms`
- `CombatKinematicMotor.Step`: `6.5171ms` / 12 次
- `CombatKinematicMotor.ResolveCollisions`: `2.9426ms`
- `CombatKinematicMotor.TryFindFirstCollision`: `2.7466ms`
- `CombatPhysicsBroadphase.CollectCandidatesAabb`: `1.6255ms` / 14 次
- `CombatPhysicsWorld.CollectBroadphaseCandidatesAabb`: `1.1675ms` / 11 次
- `CharacterTestMovableNaturalObjectSystem.Tick`: `1.8236ms`
- `CharacterTestMovableNaturalObjectSystem.AdvanceObject`: `1.4852ms`
- `CharacterTestMovableNaturalObjectSystem.ResolveStaticContacts`: `1.4120ms`

这一帧同时有 2 次 `RuntimeHost.Tick()` / `GameSlice.HostTick`，因此它是 catch-up/burst 帧；但即便按单个 simulation tick 观察，`CombatKinematicMotor.Step` 的重复 broadphase 收集和 narrow-phase collision 仍是下一阶段主瓶颈。

#### 下一阶段具体优化内容

1. 先修 Tornado fast path 排序分配：
   - 新增内部 no-allocation range sort helper，或放在 `CharacterTestDynamicFieldTargetIndex` 内部私有方法。
   - 不修改 Tornado 半径、生命周期、tick 频率、敌我过滤、质量缩放、终端速度和 motion request 逻辑。
   - 验收指标：`TornadoFieldQuery` / `QueryVerticalCapsule` 稳态 GC 为 0，`InteractionConsumers` 保持 `<= 1.0ms` 的 frame 169 等价目标。

2. 为 Motion/CombatKinematicMotor 增加共享动态碰撞查询索引：
   - 在 CharacterTest assembly 内部建立 per-physics-tick 的动态 body index，收集角色、可移动物体和仍存在于 `CombatPhysicsWorld` 的 collider。
   - `CharacterTestCharacterMotionSystem`、`CombatKinematicMotor` 和 `CharacterTestMovableNaturalObjectSystem` 共用同一 tick 的 index/scratch buffer，避免每个 body step 重复从 world broadphase 扫描候选。
   - 保持 public API 不变；新增类型默认 internal，且 hot path 初始化后 NoAlloc。

3. 缩窄 `CombatKinematicMotor.TryFindFirstCollision` 的候选集：
   - 按 sweep AABB 先走 grid cell coarse filter，再进入 capsule/AABB 或 shape narrow phase。
   - static collider 与 dynamic collider 分 pass；static pass 可复用当前 tick 的 read-only cell 列表，dynamic pass 使用 query stamp 去重。
   - 对同一个 body 的多次 sub-step 复用 caller-owned candidate buffer，避免 `CollectCandidatesAabb` 在 `Step` 内重复扩容、重复排序或重复遍历。

4. 优化 movable object static contact：
   - `ResolveStaticContacts` 改为使用同一 static grid query 或 per-object AABB dirty cache。
   - 只有对象位置、半径、高度或相关 static cell 版本变化时才刷新静态候选；正常推进帧复用候选数组。
   - 保持原有阻挡、滑动和接触修正语义，不通过放宽碰撞精度换性能。

5. 验证与验收：
   - EditMode：Tornado no-GC、Tornado 查询集合/排序一致、Motion candidate set 与旧 broadphase 等价、movable object static contact 行为一致。
   - PlayMode/warm-run：继续使用当前 CharacterTest performance 菜单和 ProfilerRecorder 报告。
   - Profiler Analyzer：同一场景重新导出 marker CSV，继续单独标注 frame 169，并排除第 200 帧。
   - 目标：Tornado fast path GC 为 0；`InteractionConsumers` 稳定不回退；frame 169 等价 profile 中 `CharacterRuntime.Motion` 从 `6.59ms` 级别降到 `<= 3.5ms`，`CombatKinematicMotor.Step` 和 `CollectCandidatesAabb` 合计至少下降 40%。

#### 2026-06-20 实施进展

- Tornado fast path 排序分配已修复：`CharacterTestDynamicFieldTargetIndex.QueryVerticalCapsule()` 不再调用 `List<T>.Sort(start,count,comparer)`；本次追加结果区间改为内部 stable insertion sort，继续复用 `CombatQueryResultComparer` 语义，不新增 public API。
- Motion broadphase 第一刀已落地：`CombatPhysicsWorld` 记录本次 broadphase 同步前移动过的 `CombatPhysicsBody`，`CombatPhysicsBroadphase` 在 topology 未变、body revision 已变时优先只更新 dirty body 对应 candidate/cell；缺少 dirty 信息时仍回退原全量 body 扫描。
- 该 Motion 改动不改变碰撞形状、layer/source 过滤、query result 排序、`CombatKinematicMotor.TryFindFirstCollision()` narrow phase、step-up 逻辑或 public API；目标是降低多角色 / 多 sub-step 下 `CollectCandidatesAabb` 因 `SetBodyPosition()` 触发的重复全量 body 同步成本。
- `CombatPhysicsWorld` moved-body scratch 继续收窄：新增内部 `CombatBodyId -> moved body index`，同一 tick / 同一次 broadphase 消费前重复 `SetBodyPosition()` 同一个 body 时直接覆盖最后位置，不再线性扫描 `_movedBodies` 去重；topology rebuild、query / explain 消费后统一清理 list 与 index，保持热路径 NoAlloc。
- Broadphase body 查找继续收窄：`CombatPhysicsBroadphase` 重建索引时同步维护 `CombatBodyId -> indexed body index`；`UpdateIndexedBody()` 从线性扫描 `_indexedBodies` 改为 O(1) lookup，再按 body id 找到该 body 的 collider entry 列表更新 cell。全量 rebuild 时 AABB / Sphere / Capsule candidate 构建也复用同一个 body-id 索引，不再对每个 collider 反复线性扫描 body list。该索引只在 topology rebuild 时扩容/重建，moved-body 热路径不新增分配。
- Broadphase cell 去重已从 `HashSet<int>` 改为 collider-index stamp：`CombatPhysicsBroadphase` 现在用 query stamp 标记本次已访问的紧凑 collider index，保留 `BroadphaseRawCandidateCount` / `BroadphaseCandidateCount` 的语义，但去掉每次 query 的 hash add/clear 热点。stamp list 只在 collider entry count 增长时扩容，warm-up 后 NoAlloc；stamp 溢出时重置为 0 后继续从 1 开始。
- Broadphase dirty-body cell range cache 继续收窄：`CombatPhysicsBroadphase` 现在为每个 indexed entry 同步保存 min/max cell range；dirty body 增量更新时直接复用旧 cell range，只对更新后的 bounds 计算一次新 cell range，再决定是否从旧 cell 移除并加入新 cell。该改动移除 `HasSameCellRange()` / remove / add 路径中对旧 bounds 与新 bounds 的重复 `ToCell()` 计算，不改变 broadphase hit、raw/dedup/filter 统计、candidate 排序或 public API；entry cell cache 只在 topology rebuild 时扩容，moved-body 热路径 NoAlloc。
- Broadphase active cell list 回收继续收窄：`CombatPhysicsBroadphase` 为 active cell list 增加内部 list-index 字典，空 cell 回收从 `_activeCellLists.Remove(indices)` 的线性扫描改为 swap-remove O(1)，同时保持 `_cells` 字典删除和 list pool 复用语义。该改动只影响 dirty body 跨 cell 时的内部池管理，不改变 cell key、candidate 内容、排序或 public API；索引字典随 topology rebuild / cell list rent 扩容，warm-up 后跨 cell 移动 NoAlloc。
- `CombatKinematicMotor` Step 级候选缓存已落地：`ResolveCollisions()` 在单次 `Step()` 开始时用 start/end capsule bounds、skin 和 step-up 高度构造保守 AABB，只调用一次 `CombatPhysicsWorld.CollectBroadphaseCandidatesAabb()`；每个 `TryFindFirstCollision()` probe 复用这次 broadphase scratch view，并在进入 layer/source/narrow phase 前按精确 probe AABB 内联过滤。cache 未激活时仍回退原 world broadphase 路径。
- 该 Step cache 不改变窄相碰撞、best-fraction 选择、source body 排除、step-up 判定、碰撞 flag、surface material 选择或 public API；目标是降低同一 body 多次 collision iteration / step-up probe 下重复 broadphase collect、重复 cell 遍历、候选集扩容和候选结构体复制压力。热路径只复用 broadphase scratch view 与内联 AABB filter，warm-up 后不产生 managed allocation。
- Step cache 复制成本继续收窄：`CombatKinematicMotor` 现在持有本次 `CombatPhysicsWorld.CollectBroadphaseCandidatesAabb()` 返回的 broadphase scratch view，并只用 motor-owned `int` 索引列表记录通过 source/layer 预过滤的候选；同一 Step 内不再复制完整 `CombatPhysicsBroadphaseCandidate` struct，也不再维护每个 probe 的 `_queryBroadphaseCandidates` 过滤 list。该改动依赖的 lifetime 是同一 Step 内不再触发第二次 world broadphase collect，cache 未激活路径仍保持原行为。
- Step cache 重复过滤继续收窄：`CombatKinematicMotor` 在构建 Step 级候选 view 时先按 `Config.ObstacleLayerMask` 和 `sourceEntityId` 做一次预过滤，后续每个 `TryFindFirstCollision()` probe 不再重复检查 source/self 与 off-layer candidate；fallback 非 cache 路径仍保留原 per-probe `CanHitBroadphaseCandidate()` 过滤。该改动只减少同一 Step 内反复扫描的无效候选，不改变实际碰撞输出。
- Step-up AABB 判定继续收窄：`TryFindFirstCollision()` 现在把最终命中的 `CombatPhysicsBroadphaseCandidate` 一并返回给 `TryResolveStepUp()`；`CanAttemptStepUpOverAabb()` 直接使用 candidate 的 `ShapeKind` 与 world-space `Max.Y`，不再为同一个阻挡命中回查 `CombatPhysicsWorld.TryGetAabbCollider()` / `TryGetBody()`。非 AABB 阻挡仍会直接跳过 step-up probe，不改变 sphere / capsule 阻挡语义。
- Dynamic shape 窄相继续收窄：`TryFindFirstCollision()` 在单次 probe 开始时预计算 moving capsule 的 world bounds，并把 Y 范围传入 sphere / capsule target sweep；`SweptVerticalMayOverlap()` 和 `VerticalOverlapsCircleTargetAtFraction()` 不再对同一候选循环反复调用 `CapsuleProxy.GetBounds()`。capsule center 改为 lazy cache，只在 sphere / capsule target 通过垂直粗判后解析，AABB-only probe 不再支付 `GetWorldCenter()`。该改动只减少 sphere / capsule 目标窄相重复计算，不改变 AABB sweep、碰撞排序、surface material 选择、step-up 判定或 public API。
- Sweep 距离计算继续收窄：`TryFindFirstCollision()` 不再在每个 probe 开始时无条件执行 `delta.LengthSquared().Sqrt()`；`ResolveDeltaLength()` 以 probe-local cache 形式延迟到 AABB sweep 或 circle target sweep 真正生成命中 `distance` 时才解析。被 query AABB、source/layer、best-fraction prefix、垂直粗判或 narrow phase miss 过滤掉的候选不再支付移动长度开方成本；初始重叠命中仍直接返回 `distance=0`。
- AABB sweep 派生值继续收窄：AABB target narrow phase 不再对每个候选调用 `CapsuleProxy.GetSegment()`、`Min/Max(pointA, pointB)` 和构造 radius-expanded obstacle；`TrySweepAabb()` 现在直接复用 probe 开始时已计算出的 capsule `startMin/startMax` 与原始 obstacle AABB。该形式与旧的 segment-vs-expanded-AABB 轴向 sweep 等价，只减少每个 AABB 候选的重复派生值和私有包装调用，不改变 initial overlap、distance、normal 或 collision flags。
- Motion query padding 继续收窄：`CombatKinematicMotor.SkinVector()` 现在只使用 `Config.SkinWidth` 作为 broadphase query / best-prefix AABB 的额外 padding，不再把 `CapsuleProxy.Radius` 二次加入 query envelope。当前 AABB / sphere / capsule narrow phase 已经以 capsule bounds 或 capsule radius 参与真实碰撞计算，bounds 本身包含半径；因此该改动只减少 broadphase 过宽候选、Step cache view 扫描和 best-fraction prefix 的误保留候选，不改变窄相命中、step-up、surface material 或 public API。
- Step cache envelope padding 继续收窄：`PrepareStepBroadphaseCache()` 也改为使用 capsule bounds + `Config.SkinWidth`，不再在 Step 级 broadphase cache 外层额外加入一次 `CapsuleProxy.Radius`。该路径只决定单次 `Step()` 复用的粗候选 envelope；每个 probe 仍用精确 query AABB 过滤并走原窄相，因此只减少缓存候选 cell / candidate 扫描，不改变 `TryFindFirstCollision()` 命中、step-up 或 collision flags。新增 `Step_StepCacheUsesCapsuleBoundsWithoutDoubleRadiusPadding` 用 1m broadphase cell 锁定该 query 从旧 double-radius 下的 36 cells 收敛为 16 cells。
- Step-up envelope padding 继续收窄：`PrepareStepBroadphaseCache()` 现在只有在 `desiredDelta.X/Z` 存在水平位移时才为 Step cache 加 `MaxStepHeight` 垂直 padding。`TryResolveStepUp()` 本来就要求 horizontal delta 非零；因此 grounded idle、纯下落 probe 或其他纯竖直帧不再为不可能触发的 step-up 扫描额外 broadphase cell。真实 ground contact、surface material、collision flags 和 step-up 有水平位移时的探测路径不变；新增 `Step_StepCacheSkipsStepUpPaddingWithoutHorizontalDelta` 用 1m broadphase cell 锁定有 `MaxStepHeight` 但无水平输入时仍保持 16 cells，且 `StepUpProbeCount=0`。
- Step cache 空候选短路继续收窄：`PrepareStepBroadphaseCache()` 完成 source/layer 预过滤后，如果本步没有任何 eligible broadphase candidate，`ResolveCollisions()` 直接返回 `startPosition + desiredDelta`，不再进入 `TryFindFirstCollision()` 的 probe / query AABB / best-prefix / narrow-phase 路径。该短路只覆盖“按当前 collision mask 和 source 排除规则不可能命中任何目标”的 Step，保留 world broadphase collect、body sync 和最终 `SetBodyPosition()` 行为；新增 `Step_StepCacheSkipsCollisionProbeWhenNoEligibleCandidates` 用 off-layer AABB 锁定无阻挡运动、`QueryCount=0`、`NarrowphaseCandidateCount=0`。
- Safe-fraction 长度缓存继续收窄：`TryFindFirstCollision()` 现在把本次 probe 内 lazy 解析出的 `deltaLength` 一并返回给 `ResolveCollisions()` / `TryResolveStepUp()`；`CalculateSafeFraction()` 在命中后优先复用该长度，只在 initial-overlap 或其他未解析长度的分支才回退到 `delta.LengthSquared().Sqrt()`。该改动不改变 safe fraction 公式、collision distance、step-up 位置或 public API，只避免命中路径对同一 `remaining` 重复开方。
- Step cache exact-AABB 继续收窄：`ResolveBroadphaseCandidates()` 在当前 probe AABB 与已缓存的 base / step-up broadphase AABB 完全相同时，不再对缓存候选逐个重复执行 probe AABB overlap 复查。`CombatPhysicsBroadphase` 返回候选前已经做了 exact AABB overlap 和 source/layer 过滤，因此该短路只移除重复判断；较小 probe、非 exact step-up probe 和 fallback 非 cache 查询仍保留原精确过滤，不改变 narrow phase、排序、step-up 或 public API。
- Movable static contact 第一刀已落地：`CharacterTestMovableNaturalObjectContactScratch` 在静态 AABB collider 缓存重建时同步建立 4m XZ grid；`CharacterTestMovableNaturalObjectSystem.AdvanceObject()` 对每个 movable 先按自身 XZ AABB 查询附近 static obstacle，再复用原 `ResolveStaticContacts()` 窄相、阻挡、滑动和接触修正逻辑。查询使用 pooled cell list、query stamp 去重、caller-owned index/result buffer，并按原 static obstacle 顺序稳定输出；warm-up 后 steady-state 查询 NoAlloc。
- Movable static contact 查询缓存继续收窄：`CharacterTestMovableNaturalObjectContactScratch` 现在按 movable `NumericId` 维护 per-object static obstacle query cache，用 topology revision 与本次 XZ query bounds 判定命中。对象未移动且静态拓扑未变时，`QueryStaticObstacles()` 直接复用已排序候选列表，跳过 grid cell 扫描、query stamp 去重和结果复制；对象位置/查询范围变化或静态障碍拓扑变化时自动 miss 并刷新。该缓存只保存静态候选集，不缓存接触求解结果，因此 `ResolveStaticContacts()` 仍使用当前帧 position、velocity、材质和质量语义。
- Movable static contact cache-hit clear 成本继续收窄：`QueryStaticObstacles()` 现在先计算 movable XZ query bounds 并检查 per-object cache，cache hit 时直接返回该对象已排序的 cached results；只有 `state == null`、无静态障碍或 cache miss 时才清理 `_staticObstacleQueryIndices` / `_staticObstacleQueryResults` 这两个共享 miss scratch。稳定 tick 命中 cache 的对象不再支付 shared list clear 成本，同时保持候选排序、source object 语义和后续 `ResolveStaticContacts()` 接触求解完全沿用现有路径。
- 新增/保留的测试覆盖点：`Broadphase_IndexUpdatesWhenBodyMoves`、`Broadphase_MovedBodiesDoNotAllocateAfterScratchWarmup`、`Broadphase_RepeatedMovedBodyUpdatesUseLatestPositionWithoutAllocating`、`Broadphase_EmptyCellRecyclingDoesNotAllocateWhenBodyCrossesCells`、`Broadphase_ExplainQueryConsumesMovedBodyWithoutLeavingStaleIndex`；既有 Tornado 查询一致性、排序稳定和 no-GC 测试；`RuntimeMovableNaturalObjects_StaticContactScratchQueriesNearbyObstaclesOnly` 覆盖 grid 候选缩窄、稳定顺序、per-object cache hit/miss、对象移动失效和 warm-up 后 no-GC；`RuntimeMovableNaturalObjects_StaticContactScratchCachesUntilTopologyChanges` 覆盖稳定 tick 命中缓存、静态拓扑变化失效和下一 tick 重新命中；`Step_StepUpCandidateCacheDoesNotAllocateAfterWarmup` 覆盖 step-up probe 复用 Step cache 后的稳态 no-GC；`Step_StepCachePrefiltersSourceAndOffLayerCandidates` 覆盖 Step cache 不再把 self/off-layer candidate 计入 eligible probe 候选；`Step_StepCacheSkipsStepUpPaddingWithoutHorizontalDelta` 覆盖 grounded idle / 纯竖直 probe 不再加入 step-up 垂直 padding；扩展 `ShapeTargetCollision_BlocksAndReportsCollision`，锁定 sphere / capsule 阻挡不会触发 AABB step-up probe；新增 `ShapeTargetCollision_DynamicShapeSweepDoesNotAllocateAfterWarmup` 覆盖 sphere / capsule target sweep 在 warm-up 后无 GC。
- 当前已完成验证：`dotnet build MxFramework.Tests.csproj -v minimal` 通过，0 errors；当前完整 build 仍有既有 CS0649 serialized-field warning，未出现在本轮 touched files。`git diff --check` 通过；`Tools/Harness/check_code_standards.py --all --summary-only` 通过执行，150 个 report-only finding 仍为既有 unsafe baseline（`ZString.cs` 149 个，Story demo 1 个）。Unity MCP refresh / compile 后 Console 没有编译 error。
- Unity focused EditMode 已完成：`run_tests(mode=EditMode)` job `1b0b8c3258ae47dfa9b88452c18f28c3` 共 27/27 通过，覆盖 broadphase raw/dedup/filter/hit 统计、full-scan oracle 一致性、moved-body 增量更新、broadphase no-GC、KinematicMotor query/cache no-GC、step-up candidate cache、moved-body world sync、Tornado 专用查询集合一致性/稳定排序/no-GC，以及 movable static-contact grid no-GC / 候选缩窄 / topology cache。追加 moved-body scratch index 后，`CombatPhysicsBroadphaseTests` 全类 job `aa5cadd192ad4e4fa1d30d4254d3a1e0` 13/13 通过，其中新增 `Broadphase_RepeatedMovedBodyUpdatesUseLatestPositionWithoutAllocating` 验证同一 body 连续移动后使用最后位置且 warm-up 后无 GC。追加 Step cache scratch-view 优化后，KinematicMotor focused job `9667128698db4c9280438b7ba7773083` 6/6 通过，覆盖 broadphase query no-GC、buffered collision no-GC、direct candidate no-GC、step-up candidate cache no-GC、best-fraction prefix 和 moved-body world sync。移除 `_queryBroadphaseCandidates` 后，focused job `02b7c741dfb24f729e15eba4da6dac79` 51/51 通过，覆盖 `CombatKinematicMotorTests` 与 `CombatPhysicsBroadphaseTests` 两个完整类。追加 Step eligible candidate 预过滤后，`CombatKinematicMotorTests` 全类 job `cbfb3b7c2cc44937bdd9c27641c3dc31` 39/39 通过。追加 Step-up AABB candidate 复用后，`CombatKinematicMotorTests` 全类 job `0493bedc87f24b28a8268b932bec971f` 39/39 通过；broadphase + Tornado index + movable static-contact focused job `6d8166511075427ab3ffdf6ad01aac49` 19/19 通过。测试后 Console 没有编译 error；`read_console(types=error)` 只返回 Unity Test Runner 写 `TestResults.xml` 的记录。
- 追加 per-object static obstacle query cache 后，本轮 Unity focused EditMode job `8840cd4e62fc445d801fa66ce7c496f5` 2/2 通过，覆盖新增 cache hit/miss、对象移动失效、topology 失效和 no-GC；movable static/contact focused job `2b9f2644ef124a13b9e4d1d02406ceb6` 5/5 通过，覆盖 static contact warm-up 后 no-GC、候选缩窄、交互接触 warm-up 后 no-GC、topology cache 和材质/质量/边界行为。测试后 Console 没有编译 error；`read_console(types=error)` 只返回 Unity Test Runner 写 `TestResults.xml` 的记录。
- PlayMode warm-run after 验证已用 EditMode UnityTest 入口重跑：`CharacterTestProfilerGcRemediationTests.CharacterTestScene_PlayModeWarmRunReport_WritesSceneTimingAndCountersIntoTemp` job `fc6200d11e4d4d20b2cea3c06f8d1852` 1/1 通过，并于 2026-06-20 19:05 刷新 `Temp/CharacterTestPerformancePlayModeWarmRunReport.txt` 与 `Temp/CharacterTestPerformancePlayModeProfilerRecorderReport.txt`。120 个采样帧线程分配 `allocatedBytes.total=0` / `p95=0` / `max=0`。本次场景帧结果为 `elapsedMs.median=1.996`、`p75=2.613`、`p90=7.536`、`p95=8.019`、`max=9.224`。ProfilerRecorder 12 个 Marker 均 `available=true`；关键 after 值：`CharacterTest.CharacterRuntime.Motion` median `0.347ms` / P95 `0.465ms`，`CharacterTest.RuntimeModule.CharacterTest.CharacterRuntime` median `0.521ms` / P95 `0.694ms`，`CharacterTest.GameSlice.HostTick` median `0.584ms` / P95 `0.777ms`，`CharacterTest.GameManager.Simulation` median `0.614ms` / P95 `0.819ms`。
- 追加 dynamic shape 窄相预计算后，本轮 Unity focused EditMode job `5c08af45929b4d99b23d071fd01e6507` 6/6 通过，覆盖 sphere / capsule 目标阻挡、初始重叠向外移动和新增 no-GC sweep；`CombatKinematicMotorTests` 全类 job `77a82f874aa642daa1209d625441f6e1` 41/41 通过。PlayMode warm-run job `20cfa08eef224bb6b0439e3ecab62393` 1/1 通过，并于 2026-06-20 19:14 刷新 `Temp/CharacterTestPerformancePlayModeWarmRunReport.txt` 与 `Temp/CharacterTestPerformancePlayModeProfilerRecorderReport.txt`。120 个采样帧线程分配 `allocatedBytes.total=0` / `p95=0` / `max=0`。本次场景帧结果为 `elapsedMs.median=1.915`、`p75=2.228`、`p90=7.351`、`p95=7.768`、`max=8.390`。ProfilerRecorder 12 个 Marker 均 `available=true`；关键 after 值：`CharacterTest.CharacterRuntime.Motion` median `0.340ms` / P95 `0.376ms`，`CharacterTest.RuntimeModule.CharacterTest.CharacterRuntime` median `0.511ms` / P95 `0.550ms`，`CharacterTest.GameSlice.HostTick` median `0.572ms` / P95 `0.612ms`，`CharacterTest.GameManager.Simulation` median `0.601ms` / P95 `0.642ms`。
- 追加 sweep 距离 lazy cache / capsule center lazy cache 后，本轮 `dotnet build MxFramework.Tests.csproj -v minimal` 通过，0 errors、127 个既有 CS0649 serialized-field warning；`git diff --check` 通过；`Tools/Harness/check_code_standards.py --all --summary-only` 通过执行，仍为 150 个 report-only unsafe baseline。Unity MCP refresh / compile 后 Console 没有编译 error。`CombatKinematicMotorTests` 全类 EditMode job `252dcc343acf4d3a87e533ca41afd1b0` 41/41 通过。warm-run 继续使用 EditMode UnityTest 入口重跑，job `ce8ab882f66044ebbd980ac60727a38e` 1/1 通过，并于 2026-06-20 19:23 刷新 `Temp/CharacterTestPerformancePlayModeWarmRunReport.txt` 与 `Temp/CharacterTestPerformancePlayModeProfilerRecorderReport.txt`。120 个采样帧线程分配 `allocatedBytes.total=0` / `p95=0` / `max=0`。本次场景帧结果为 `elapsedMs.median=1.848`、`p75=2.408`、`p90=6.999`、`p95=7.632`、`max=9.175`。ProfilerRecorder 12 个 Marker 均 `available=true`；关键 after 值：`CharacterTest.CharacterRuntime.Motion` median `0.347ms` / P95 `0.424ms`，`CharacterTest.RuntimeModule.CharacterTest.CharacterRuntime` median `0.529ms` / P95 `0.656ms`，`CharacterTest.GameSlice.HostTick` median `0.593ms` / P95 `0.727ms`，`CharacterTest.GameManager.Simulation` median `0.623ms` / P95 `0.759ms`。测试后 `read_console(types=error)` 只返回 Unity Test Runner 写 `TestResults.xml` 的记录。
- 追加 AABB sweep 复用 capsule bounds 后，本轮 `dotnet build MxFramework.Tests.csproj -v minimal` 通过，0 errors、127 个既有 CS0649 serialized-field warning；`git diff --check` 通过；`Tools/Harness/check_code_standards.py --all --summary-only` 通过执行，仍为 150 个 report-only unsafe baseline。Unity MCP refresh / compile 后 Console 没有编译 error。`CombatKinematicMotorTests` 全类 EditMode job `f798d00810a1476da168da23d76ac53d` 41/41 通过。warm-run 继续使用 EditMode UnityTest 入口重跑，job `bde8e4fdec51487b9e88b7e15c6b5f85` 1/1 通过，并于 2026-06-20 19:30 刷新 `Temp/CharacterTestPerformancePlayModeWarmRunReport.txt` 与 `Temp/CharacterTestPerformancePlayModeProfilerRecorderReport.txt`。120 个采样帧线程分配 `allocatedBytes.total=0` / `p95=0` / `max=0`。本次场景帧结果为 `elapsedMs.median=1.867`、`p75=2.323`、`p90=6.945`、`p95=7.213`、`max=7.859`。ProfilerRecorder 12 个 Marker 均 `available=true`；关键 after 值：`CharacterTest.CharacterRuntime.Motion` median `0.338ms` / P95 `0.359ms`，`CharacterTest.RuntimeModule.CharacterTest.CharacterRuntime` median `0.513ms` / P95 `0.541ms`，`CharacterTest.GameSlice.HostTick` median `0.578ms` / P95 `0.618ms`，`CharacterTest.GameManager.Simulation` median `0.608ms` / P95 `0.658ms`。测试后 `read_console(types=error)` 只返回 Unity Test Runner 写 `TestResults.xml` 的记录。
- 追加 broadphase dirty-body cell range cache 后，本轮 `dotnet build MxFramework.Tests.csproj -v minimal` 通过，0 warnings、0 errors；工作树 `git diff --check` 通过；`Tools/Harness/check_code_standards.py --all --summary-only` 通过执行，仍为 150 个 report-only unsafe baseline。Unity MCP 已连接当前 `WGameFramework@260bf48306dcb36a`，`refresh_unity(scope=scripts, compile=request)` 后 Console 没有编译 error。`CombatPhysicsBroadphaseTests` 与 `CombatKinematicMotorTests` 两个完整类 focused EditMode job `5b3ebd87453d462883e659a5cb68b904` 54/54 通过。warm-run 继续使用 EditMode UnityTest 入口重跑，job `746eb98f55f9468a87da7d44733493f1` 1/1 通过，并刷新 `Temp/CharacterTestPerformancePlayModeWarmRunReport.txt` 与 `Temp/CharacterTestPerformancePlayModeProfilerRecorderReport.txt`。120 个采样帧线程分配 `allocatedBytes.total=0` / `p95=0` / `max=0`。本次场景帧结果为 `elapsedMs.median=2.073`、`p75=2.528`、`p90=7.855`、`p95=8.057`、`max=9.853`。ProfilerRecorder 12 个 Marker 均 `available=true`；关键 after 值：`CharacterTest.CharacterRuntime.Motion` median `0.333ms` / P95 `0.461ms`，`CharacterTest.RuntimeModule.CharacterTest.CharacterRuntime` median `0.513ms` / P95 `0.723ms`，`CharacterTest.GameSlice.HostTick` median `0.574ms` / P95 `0.811ms`，`CharacterTest.GameManager.Simulation` median `0.601ms` / P95 `0.851ms`。测试后 `read_console(types=error)` 只返回 Unity Test Runner 写 `TestResults.xml` 的记录。
- 追加 broadphase active cell list O(1) 回收后，本轮 `dotnet build MxFramework.Tests.csproj -v minimal` 通过，0 errors、127 个既有 CS0649 serialized-field warning；工作树 `git diff --check` 通过；`Tools/Harness/check_code_standards.py --all --summary-only` 通过执行，仍为 150 个 report-only unsafe baseline。Unity MCP `refresh_unity(scope=scripts, compile=request)` 后 Console 没有编译 error。`CombatPhysicsBroadphaseTests` 全类 job `bbf90f729b404dc2b50f807c141696c0` 14/14 通过，覆盖新增 `Broadphase_EmptyCellRecyclingDoesNotAllocateWhenBodyCrossesCells`；`CombatKinematicMotorTests` 全类 job `161b72cee7814b9a85993c4b173175d8` 41/41 通过。warm-run 继续使用 EditMode UnityTest 入口重跑，job `b964068df24142869d1c3a0da8719164` 1/1 通过，并刷新 `Temp/CharacterTestPerformancePlayModeWarmRunReport.txt` 与 `Temp/CharacterTestPerformancePlayModeProfilerRecorderReport.txt`。120 个采样帧线程分配 `allocatedBytes.total=0` / `p95=0` / `max=0`。本次场景帧结果为 `elapsedMs.median=2.057`、`p75=2.585`、`p90=7.776`、`p95=8.651`、`max=9.854`。ProfilerRecorder 12 个 Marker 均 `available=true`；关键 after 值：`CharacterTest.CharacterRuntime.Motion` median `0.336ms` / P95 `0.472ms`，`CharacterTest.RuntimeModule.CharacterTest.CharacterRuntime` median `0.516ms` / P95 `0.719ms`，`CharacterTest.GameSlice.HostTick` median `0.581ms` / P95 `0.829ms`，`CharacterTest.GameManager.Simulation` median `0.611ms` / P95 `0.882ms`。测试后 `read_console(types=error)` 只返回 Unity Test Runner 写 `TestResults.xml` 的记录。
- 追加 movable static-contact cache-hit clear 收窄后，本轮 `dotnet build MxFramework.Tests.csproj -v minimal` 通过，0 errors、44 个既有 CS0649 serialized-field warning；工作树 `git diff --check` 通过；`Tools/Harness/check_code_standards.py --all --summary-only` 通过执行，仍为 150 个 report-only unsafe baseline。Unity MCP `refresh_unity(scope=scripts, compile=request)` 后 Console 没有编译 error。movable static-contact focused EditMode job `435cc583b1354e2681881b6445c3147b` 4/4 通过，覆盖 static-contact 候选缩窄、per-object cache hit/miss、对象移动失效、topology 失效、warm-up 后 no-GC 与材质/质量/边界行为。warm-run 继续使用 EditMode UnityTest 入口重跑，job `981998497bf543d4be1ee4698065c8ae` 1/1 通过，并刷新 `Temp/CharacterTestPerformancePlayModeWarmRunReport.txt` 与 `Temp/CharacterTestPerformancePlayModeProfilerRecorderReport.txt`。120 个采样帧线程分配 `allocatedBytes.total=0` / `p95=0` / `max=0`。本次场景帧结果为 `elapsedMs.median=1.627`、`mean=2.455`、`p75=1.926`、`p90=6.740`、`p95=6.961`、`max=8.212`。ProfilerRecorder 12 个 Marker 均 `available=true`；关键 after 值：`CharacterTest.CharacterRuntime.Motion` median `0.329ms` / P95 `0.351ms`，`CharacterTest.RuntimeModule.CharacterTest.CharacterRuntime` median `0.505ms` / P95 `0.549ms`，`CharacterTest.GameSlice.HostTick` median `0.568ms` / P95 `0.619ms`，`CharacterTest.GameManager.Simulation` median `0.596ms` / P95 `0.649ms`。测试后 `read_console(types=error)` 只返回 Unity Test Runner 写 `TestResults.xml` 的记录。
- 追加 Motion query padding 收窄后，本轮 `dotnet build MxFramework.Tests.csproj -v minimal` 通过，0 errors、127 个既有 CS0649 serialized-field warning；工作树 `git diff --check` 通过；`Tools/Harness/check_code_standards.py --all --summary-only` 通过执行，仍为 150 个 report-only unsafe baseline。Unity MCP `refresh_unity(scope=scripts, compile=request)` 后 Console 没有编译 error。`CombatKinematicMotorTests` 全类 EditMode job `9ed2b8975c5041959ed64998c205cb46` 41/41 通过，覆盖地面、墙、ceiling、step-up、dynamic sphere/capsule、surface material、world sync、step cache、best-fraction prefix 和 no-GC 路径。warm-run 继续使用 EditMode UnityTest 入口重跑，job `3181d58e69c94cacb836834a643d7257` 1/1 通过，并刷新 `Temp/CharacterTestPerformancePlayModeWarmRunReport.txt` 与 `Temp/CharacterTestPerformancePlayModeProfilerRecorderReport.txt`。120 个采样帧线程分配 `allocatedBytes.total=0` / `p95=0` / `max=0`。本次场景帧结果为 `elapsedMs.median=1.826`、`mean=2.716`、`p75=2.238`、`p90=6.898`、`p95=7.429`、`max=8.135`。ProfilerRecorder 12 个 Marker 均 `available=true`；关键 after 值：`CharacterTest.CharacterRuntime.Motion` median `0.328ms` / P95 `0.334ms`，`CharacterTest.RuntimeModule.CharacterTest.CharacterRuntime` median `0.502ms` / P95 `0.517ms`，`CharacterTest.GameSlice.HostTick` median `0.564ms` / P95 `0.585ms`，`CharacterTest.GameManager.Simulation` median `0.593ms` / P95 `0.613ms`。测试后 `read_console(types=error)` 只返回 Unity Test Runner 写 `TestResults.xml` 的记录。
- 追加 Step cache index-view 收窄后，本轮 `dotnet build MxFramework.Tests.csproj -v minimal` 通过，0 errors、127 个既有 CS0649 serialized-field warning；工作树 `git diff --check` 通过；`Tools/Harness/check_code_standards.py --all --summary-only` 通过执行，仍为 150 个 report-only unsafe baseline。Unity MCP `refresh_unity(scope=scripts, compile=request)` 后 Console 没有编译 error。`CombatKinematicMotorTests` 全类 EditMode job `6d703a7b1a0d4093866e831608060cc7` 41/41 通过，覆盖 step cache source/layer 预过滤、step-up probe、dynamic sphere/capsule、surface material、world sync、best-fraction prefix 和 no-GC 路径。warm-run 继续使用 EditMode UnityTest 入口重跑，job `e6d4e713a64041d7b5654a20f2d0d880` 1/1 通过，并刷新 `Temp/CharacterTestPerformancePlayModeWarmRunReport.txt` 与 `Temp/CharacterTestPerformancePlayModeProfilerRecorderReport.txt`。120 个采样帧线程分配 `allocatedBytes.total=0` / `p95=0` / `max=0`。本次场景帧结果为 `elapsedMs.median=1.843`、`mean=2.788`、`p75=2.374`、`p90=7.286`、`p95=7.721`、`max=9.656`。ProfilerRecorder 12 个 Marker 均 `available=true`；关键 after 值：`CharacterTest.CharacterRuntime.Motion` median `0.345ms` / P95 `0.484ms`，`CharacterTest.RuntimeModule.CharacterTest.CharacterRuntime` median `0.575ms` / P95 `0.722ms`，`CharacterTest.GameSlice.HostTick` median `0.649ms` / P95 `0.805ms`，`CharacterTest.GameManager.Simulation` median `0.677ms` / P95 `0.846ms`。测试后 `read_console(types=error)` 只返回 Unity Test Runner 写 `TestResults.xml` 的记录。
- 追加 safe-fraction 长度缓存后，本轮 `dotnet build MxFramework.Tests.csproj -v minimal` 通过，0 errors、127 个既有 CS0649 serialized-field warning；工作树 `git diff --check` 通过；`Tools/Harness/check_code_standards.py --all --summary-only` 通过执行，仍为 150 个 report-only unsafe baseline。Unity MCP `refresh_unity(scope=scripts, compile=request)` 后 Console 没有编译 error。`CombatKinematicMotorTests` 全类 EditMode job `c868ad60989a4f53845619d8bb575ec4` 41/41 通过。warm-run 继续使用 EditMode UnityTest 入口重跑，job `16df974806da4b2ca5779bde38b93291` 1/1 通过，并刷新 `Temp/CharacterTestPerformancePlayModeWarmRunReport.txt` 与 `Temp/CharacterTestPerformancePlayModeProfilerRecorderReport.txt`。120 个采样帧线程分配 `allocatedBytes.total=0` / `p95=0` / `max=0`。本次场景帧结果为 `elapsedMs.median=1.642`、`mean=2.334`、`p75=1.875`、`p90=6.539`、`p95=6.705`、`max=7.730`。ProfilerRecorder 12 个 Marker 均 `available=true`；关键 after 值：`CharacterTest.CharacterRuntime.Motion` median `0.335ms` / P95 `0.456ms`，`CharacterTest.RuntimeModule.CharacterTest.CharacterRuntime` median `0.508ms` / P95 `0.695ms`，`CharacterTest.GameSlice.HostTick` median `0.569ms` / P95 `0.780ms`，`CharacterTest.GameManager.Simulation` median `0.598ms` / P95 `0.820ms`。测试后 `read_console(types=error)` 只返回 Unity Test Runner 写 `TestResults.xml` 的记录。
- 追加 Step cache envelope padding 收窄后，本轮 `dotnet build MxFramework.Tests.csproj -v minimal` 通过，0 errors、127 个既有 CS0649 serialized-field warning。Unity MCP `refresh_unity(scope=scripts, compile=request)` 后 Console 没有编译 error；新增 focused EditMode job `d838e59f204545d29a003d6ad01fe97e` 1/1 通过，`CombatKinematicMotorTests` 全类 job `3b3470374c664095a4ed246c0a0eef5a` 42/42 通过。warm-run 继续使用 EditMode UnityTest 入口重跑，job `2f19eb93103d4b8299a6ae82f36b96cf` 1/1 通过，并刷新 `Temp/CharacterTestPerformancePlayModeWarmRunReport.txt` 与 `Temp/CharacterTestPerformancePlayModeProfilerRecorderReport.txt`。120 个采样帧线程分配 `allocatedBytes.total=0` / `p95=0` / `max=0`。本次场景帧结果为 `elapsedMs.median=1.930`、`mean=2.905`、`p75=2.360`、`p90=7.473`、`p95=7.912`、`max=9.228`。ProfilerRecorder 12 个 Marker 均 `available=true`；关键 after 值：`CharacterTest.CharacterRuntime.Motion` median `0.332ms` / P95 `0.407ms`，`CharacterTest.RuntimeModule.CharacterTest.CharacterRuntime` median `0.504ms` / P95 `0.629ms`，`CharacterTest.GameSlice.HostTick` median `0.576ms` / P95 `0.712ms`，`CharacterTest.GameManager.Simulation` median `0.608ms` / P95 `0.750ms`。测试后 `read_console(types=error)` 只返回 Unity Test Runner 写 `TestResults.xml` 的记录。本轮工作树 `git diff --check` 通过；`Tools/Harness/check_code_standards.py --all --summary-only` 通过执行，仍为 150 个 report-only unsafe baseline；`Tools/Harness/run_lightweight_checks.sh` 本轮执行到 184 秒超时且没有返回可用摘要，因此未作为通过证据。
- 追加 Step-up envelope padding 水平位移 gate 后，本轮 `dotnet build MxFramework.Tests.csproj -v minimal` 通过，0 errors、127 个既有 CS0649 serialized-field warning；工作树 `git diff --check` 通过；`Tools/Harness/check_code_standards.py --all --summary-only` 通过执行。Unity MCP `refresh_unity(scope=scripts, compile=request)` 后 Console 没有编译 error；新增 focused EditMode job `6cf97bb9ea224da6b28b9bc9d21f8703` 1/1 通过，`CombatKinematicMotorTests` 全类 job `0fff1b8d3dcf40c3ace61bab2d55daa1` 43/43 通过。测试后 `read_console(types=error)` 只返回 Unity Test Runner 写 `TestResults.xml` 的记录。
- 追加 Step cache 空 eligible candidate 短路后，本轮 `dotnet build MxFramework.Tests.csproj -v minimal` 通过，0 errors、127 个既有 CS0649 serialized-field warning；工作树 `git diff --check` 通过；`Tools/Harness/check_code_standards.py --all --summary-only` 通过执行。Unity MCP `refresh_unity(scope=scripts, compile=request)` 后 Console 没有编译 error；新增 focused EditMode job `a89ad478f23141cb95cea0f21c33f838` 1/1 通过，`CombatKinematicMotorTests` 全类 job `686b34c65cc24be5b5c016880420c9e9` 44/44 通过。Git Bash 执行 `Tools/Harness/run_lightweight_checks.sh` 通过：changed path count 27，forbidden path、Unity `.meta` pairing、Docs health、code standards 均通过，code standards 扫描 15 个 first-party code file 且 no findings。warm-run 继续使用 EditMode UnityTest 入口重跑，job `fdb60df3197446a795d167c69e3e028a` 1/1 通过，并刷新 `Temp/CharacterTestPerformancePlayModeWarmRunReport.txt` 与 `Temp/CharacterTestPerformancePlayModeProfilerRecorderReport.txt`。120 个采样帧线程分配 `allocatedBytes.total=0` / `p95=0` / `max=0`。本次场景帧结果为 `elapsedMs.median=1.775`、`mean=2.625`、`p75=2.083`、`p90=6.736`、`p95=7.192`、`max=9.288`。ProfilerRecorder 12 个 Marker 均 `available=true`；关键 after 值：`CharacterTest.CharacterRuntime.Motion` median `0.331ms` / P95 `0.417ms`，`CharacterTest.RuntimeModule.CharacterTest.CharacterRuntime` median `0.507ms` / P95 `0.605ms`，`CharacterTest.GameSlice.HostTick` median `0.574ms` / P95 `0.690ms`，`CharacterTest.GameManager.Simulation` median `0.604ms` / P95 `0.728ms`。测试后 `read_console(types=error)` 只返回 Unity Test Runner 写 `TestResults.xml` 的记录。
- 追加 Step cache exact-AABB 短路后，本轮 `dotnet build MxFramework.Tests.csproj -v minimal` 通过，0 errors、既有 CS0649 serialized-field warnings；focused `dotnet test` 过滤 `Step_StepCacheUsesCapsuleBoundsWithoutDoubleRadiusPadding|Step_StepCacheDefersStepUpEnvelopeUntilWallCandidate|Step_BestFractionPrefixSkipsFarCandidates|Step_StepCacheSkipsCollisionProbeWhenNoEligibleCandidates` 退出 0 但仍只报告 build success，作为弱证据；`git diff --check` 通过；`Tools/Harness/check_code_standards.py --all --summary-only` 通过执行，仍为既有 150 个 report-only unsafe baseline；Git Bash `Tools/Harness/run_lightweight_checks.sh` 通过，changed path count 63，forbidden path、Unity `.meta` pairing、Docs health、changed-files code standards 均通过。当前 Unity Editor 已打开本项目但 MCP 端口 `127.0.0.1:8080` 未监听，因此没有新增 MCP Console / TestRunner 证据。
- 追加 Character collider body-sync 收窄后，`CharacterTestCharacterPhysicsRuntime` 增加 internal shape-sync 查询，`CharacterTestCharacterMotionSystem.SyncCharacterCombatCollider()` 在 body/collider 记录存在且 collider 形状未变时只调用 `CombatPhysicsWorld.SetBodyPosition()` 并更新 sync cache，跳过 full `UpsertBody()` / `UpsertAabbCollider()` 路径。缺失记录、blocking 状态变化、半径/高度变化和首次注册仍走原 full sync；不新增 public API，热路径复用既有字典和值类型，warm-up 后 NoAlloc。本轮 `dotnet build MxFramework.Tests.csproj -v minimal` 通过，0 errors、既有 CS0649 serialized-field warnings；focused `dotnet test` 过滤 `CharacterMotion_FrameworkPhysicsWorld_CombatColliderSyncSkipsUnchangedState` 退出 0 但无 NUnit 明细，只作弱证据；`git diff --check`、`Tools/Harness/check_code_standards.py --all --summary-only` 和 Git Bash `Tools/Harness/run_lightweight_checks.sh` 均通过，code standards 仍为既有 150 个 report-only unsafe baseline，changed-files code standards no findings。当前 Unity MCP 资源读取仍失败于 `http://127.0.0.1:8080/mcp`，所以没有新增 MCP Console / TestRunner 证据。
- 追加 raw profiler Tornado marker-window exporter 后，本轮 `dotnet build MxFramework.Tests.csproj -v minimal` 通过，0 warnings、0 errors；工作树 `git diff --check` 通过。Unity MCP `refresh_unity(scope=scripts, compile=request)` 后 Console 没有编译 error；profiler exporter focused EditMode job `46e5318ad3c54466bd6ee49392ad469f` 3/3 通过，覆盖普通 raw CSV headers/summary、marker-centered frame window clamp，以及 marker 缺失时仍输出 summary / CSV headers。该工具新增菜单 `MxFramework/CharacterTest/Performance/Export Current Profiler Raw CSV (Tornado Window)`，默认搜索 `CharacterTest.InteractionPhysics.TornadoFieldQuery` 并导出命中帧前后小窗口，解决最近 30 frame 默认导出容易错过 Tornado active field 的取证问题；它仍是当前 Editor profiler buffer 的 after-side review aid，不替代正式 Profile Analyzer before/after capture。
- Profiler Analyzer artifact 审计继续补齐：`Tools/Harness/profile_analyzer_gc_summary.py` 现在可从 `ProfileAnalyzer/tmp1.pdata` 与 `ProfileAnalyzer/tmp2.pdata` 直接导出 frameTime、GC markerTable 和 Profile Analyzer-like marker timing 表。全量导出已写入 `Temp/ProfileAnalyzer/tmp1_full_frameTime.csv`、`tmp1_full_gcMarkerTable.csv`、`tmp1_full_markerTiming.csv`、`tmp2_full_frameTime.csv`、`tmp2_full_gcMarkerTable.csv`、`tmp2_full_markerTiming.csv`；frame 169 focused 导出已写入 `Temp/ProfileAnalyzer/tmp2_frame169_markerTiming.csv` / `tmp2_frame169_markerTiming_check.csv`。
- `C:/Users/WuGC/Videos/frameTime.csv` / `markerTable.csv` 与 `tmp2.pdata` 的 frame 169 timing 和 GC 关键行一致：`CharacterTest.GameManager.Simulation` `9.9995ms` / `336 bytes`，`CharacterTest.GameSlice.HostTick` `9.8532ms` / `336 bytes`，`CharacterTest.RuntimeModule.CharacterTest.CharacterRuntime` `9.4933ms` / `256 bytes`，`CharacterTest.CharacterRuntime.Motion` `6.5939ms` / `0 bytes`，`CombatKinematicMotor.Step()` `6.5171ms` / `0 bytes`，`CharacterTest.CharacterRuntime.InteractionConsumers` `1.0512ms` / `256 bytes`。因此该 helper 已能作为 `.pdata` artifact 的 repeatable timing/GC 审计工具，但输出仍是 review aid，不替代 Profile Analyzer UI 的正式可视化比对。
- `tmp1_full_markerTiming.csv` 与 `tmp2_full_markerTiming.csv` 可做历史 artifact 辅助对照，但这两个 `.pdata` 并非本轮代码改动后重新采集的同一 clean scenario before/after。当前只把它们作为“最新用户 CSV 可复现解析”和“热点定位可追溯”的证据，不把二者的全量 median 差异写成优化收益结论。
- Editor 当前 Profiler buffer 审计也已补齐：新增菜单 `MxFramework/CharacterTest/Performance/Export Current Profiler Raw CSV`，默认导出 `Temp/ProfileAnalyzer/current_raw_frameTime.csv`、`Temp/ProfileAnalyzer/current_raw_markerTiming.csv` 和 `Temp/ProfileAnalyzer/current_raw_profiler_export_summary.txt`。该入口直接读取 `RawFrameDataView`，输出的 marker timing header 与 `profile_analyzer_gc_summary.py --marker-timing-out` 对齐；默认只导出最近 30 个 raw frame 的 Main Thread，避免全量 Profiler buffer 导出长时间阻塞 Editor，测试和手动 API 仍可传入更大的 `maxFrameCount`。追加的 `Export Current Profiler Raw CSV (Tornado Window)` 会在当前 buffer 内从后往前搜索 `CharacterTest.InteractionPhysics.TornadoFieldQuery`，命中后导出其前后 `8` 个 raw frame，输出到 `Temp/ProfileAnalyzer/current_raw_tornado_frameTime.csv`、`current_raw_tornado_markerTiming.csv` 和 `current_raw_tornado_profiler_export_summary.txt`，summary 记录 focus marker、命中 frame、搜索 frame/window 和扫描 sample 数。
- 本轮 MCP `profiler_start` / warm-run / `profiler_stop` 已生成 `Temp/ProfileAnalyzer/current_after_warmrun.raw`，raw 文件约 4.59GB，仅作为本地临时证据，不进入提交范围。随后执行 `Export Current Profiler Raw CSV` 菜单成功，summary 记录 `firstRawFrameIndex=213`、`lastRawFrameIndex=242`、`exportedFrames=30`、`scannedThreads=4127`、`exportedMarkerSamples=3095768`、`exportedMarkers=6079`。
- 当前 30-frame raw CSV 只覆盖 warm-run 末尾，Tornado active field marker 已不在该窗口内，因此该窗口不能作为 Tornado fast path 正式 after 结论。它能作为当前代码 Editor profiler-buffer 证据：`CharacterTest.GameManager.Simulation` median `3.4153ms` / max `7.6894ms` / GC `4544B`，`CharacterTest.GameSlice.HostTick` median `3.2928ms` / max `7.5488ms` / GC `4544B`，`CharacterTest.RuntimeModule.CharacterTest.CharacterRuntime` median `3.0373ms` / max `7.0639ms` / GC `0B`，`CharacterTest.CharacterRuntime.Motion` median `2.3702ms` / max `5.7813ms` / GC `0B`，`CombatKinematicMotor.Step()` median `1.5973ms` / max `4.7574ms` / GC `0B`，`CharacterTest.CharacterRuntime.InteractionConsumers` median `0.0186ms` / max `0.0354ms` / GC `0B`。
- 初次尝试导出完整 243 个 raw frame 时，Profiler marker sample 达到约 2,948 万，MCP 菜单调用 30 秒超时且 Editor 长时间忙碌；因此默认窗口改为最近 30 帧。那次全窗口导出曾覆盖 Tornado marker，观察到 `CharacterTest.CharacterRuntime.InteractionConsumers` median `0.3787ms`、`CharacterTest.InteractionPhysics.TornadoFieldQuery` median `0.1542ms`、`CharacterTest.InteractionPhysics.TornadoIndexRebuild` median `0.2184ms`，但因文件随后被 30-frame 默认导出覆盖，不能作为可归档 artifact。后续改用显式 Profile Analyzer `.pdata` capture test 解决该问题。
- 当前代码 after 侧正式 `.pdata` 已补齐：新增 explicit PlayMode test `CharacterTestScene_PlayModeTornadoProfileAnalyzerCapture_WritesPDataIntoTemp`，job `a693aaf5d3f24e6f9a9051f97e2ac795` 1/1 通过，写出 `Temp/ProfileAnalyzer/current_after_tornado_playmode.pdata` 与 `Temp/ProfileAnalyzer/current_after_tornado_playmode_profile_analyzer_capture_summary.txt`。summary 记录 `scenario=character-test.tornado.playmode-scene.profile-analyzer-after`、`pDataWritten=true`、`firstDisplayFrameIndex=1`、`lastDisplayFrameIndex=238`、`maxCaptureFrames=240`。
- `Tools/Harness/profile_analyzer_gc_summary.py` 现支持重复 `--exclude-frame`，并在 `.pdata` marker aggregation、frameTime 导出和 CSV frame summary 中统一排除指定 display frame。本轮已用 `--exclude-frame 200` 从 after `.pdata` 导出 `Temp/ProfileAnalyzer/current_after_tornado_playmode_frameTime.csv`、`current_after_tornado_playmode_gcMarkerTable.csv` 与 `current_after_tornado_playmode_markerTiming.csv`，导出 display frame 数为 237。
- 排除第 200 帧后的 after 聚合结论：`CharacterTest.CharacterRuntime.InteractionConsumers` median `0.0349ms` / mean `0.3169ms` / max `2.7715ms`，`CharacterTestInteractionPhysicsSystem.QueryField()` median `0.6014ms` / mean `0.7518ms` / max `2.1919ms`，`CharacterTest.InteractionPhysics.TornadoIndexRebuild` median `0.4113ms` / mean `0.5251ms` / max `1.5428ms`，`CharacterTest.InteractionPhysics.TornadoFieldQuery` median `0.1668ms` / mean `0.1898ms` / max `0.5845ms` / GC total `0B`。`FallbackFieldQuery` 在 after `.pdata` 无 active row，PlayMode ProfilerRecorder 同场景也记录 active samples `0`。
- 上述为 2026-06-20 after-only artifact；2026-06-21 的同链路 before/after `TornadoFieldQuery` delta 以 `ProfileAnalyzer/paired_tornado_capture/fresh_before_after_marker_comparison.md` 为准。
- 同一 after `.pdata` 的 frame 169 单帧导出也已生成，但该帧受 Editor / ProfilerWindow repaint 噪声影响：`CharacterTest.CharacterRuntime.Motion=9.4356ms`、`CombatKinematicMotor.Step()=8.1620ms`，而 `CharacterTest.CharacterRuntime.InteractionConsumers=0.0923ms`、`CombatPhysicsWorld.QueryCapsule()=0.0391ms`。因此 frame 169 只作为单帧审计记录，不作为稳定收益结论。
- 新增 report-only 对照工具 `Tools/Harness/profile_analyzer_compare_markers.py`，读取 `profile_analyzer_gc_summary.py --marker-timing-out` 生成的分号分隔 marker timing 表，输出关键 marker 的 median / mean / max / GC delta CSV 与 Markdown。默认 marker 集覆盖 Simulation、HostTick、CharacterRuntime、Motion、KinematicMotor、Broadphase、QueryCapsule、InteractionConsumers 和 Tornado fast path marker；自定义 marker 可用重复 `--marker`。
- 本轮已用该工具生成两个临时 review aid：`Temp/ProfileAnalyzer/historical_tmp2_vs_current_after_marker_comparison.csv` / `.md`，以及 `Temp/ProfileAnalyzer/historical_tmp2_frame169_vs_current_after_frame169_marker_comparison.csv` / `.md`。前者显示历史 tmp2 到当前 after 的 `InteractionConsumers` median 从 `0.3787ms` 到 `0.0349ms`、`TornadoFieldQuery` GC total 从 `24480B` 到 `0B`，但因为不是同一 fresh capture scenario，不能写成正式收益百分比；后者显示 frame 169 的 `InteractionConsumers` 从 `1.0512ms` 到 `0.0923ms`，同时也暴露当前 after frame 169 的 Motion/Step 受 Editor repaint 噪声污染，因此只保留为单帧审计。
- 追加审计 `ProfileAnalyzer/tmp0.pdata`：为避免误把它当作 fresh before，本轮用 `.pdata` frame filter 快速导出 `Temp/ProfileAnalyzer/tmp0_frame169_*`。该 artifact 的 frame 169 是 TestRunner / Editor 主导帧，`Frame GC=174664B`、`UpdateScene=66.1839ms`、`Application.Tick=54.1441ms`；关键 runtime marker 也已呈当前 fast-path 形态：`InteractionConsumers=0.1137ms`、`CombatPhysicsWorld.QueryCapsule()=0.0386ms`、GC total `0B`。因此 `tmp0.pdata` 不是缺失的 before baseline，不能用于正式收益结论。
- `Tools/Harness/run_lightweight_checks.sh` 本轮改用 Git Bash 显式执行通过：`Harness base: origin/main`、`Changed path count: 27`，forbidden path、Unity `.meta` pairing、docs health、changed-files code standards、`git diff --check` 均通过；脚本输出大量 Git 行尾转换 warning，但未作为失败。直接从 PowerShell 调 `.sh` 没有可用输出，WSL `bash` 244 秒超时，因此本轮采用 `C:\Program Files\Git\bin\bash.exe Tools/Harness/run_lightweight_checks.sh` 作为有效门禁证据。
- 当前剩余验收缺口已经从“缺少同一 instrumentation”继续收窄为“fresh baseline 未复现用户 CSV 中的旧多毫秒 Tornado 热点”。本轮通过 baseline-only marker patch 给 `origin/main` 旧 `QueryCapsule` Tornado 路径补上同名 `CharacterTest.InteractionPhysics.TornadoFieldQuery` marker，且 before/after capture summary 都记录 Tornado projectile、移动、tick VFX、拉拽、伤害和重复伤害为 `true`。排除第 200 帧后，同名 `TornadoFieldQuery` 行现在可比：median `0.0209ms -> 0.0038ms`，mean `0.0492ms -> 0.0171ms`，max `3.5682ms -> 1.4962ms`，GC total `1104B -> 16B`。但该 fresh 场景的 before 本身只有 `0.0209ms` median，未复现历史 `tmp2.pdata` / 用户 CSV 的旧热点量级；因此大幅收益结论仍以历史/user CSV 热点证据为边界，fresh pair 仅作为同链路回归和行为保持证据。

#### 边界

- 本阶段仍不修改 Tornado 玩法参数，不使用 `unsafe`、Burst/Jobs 或 Unity Physics 作为权威碰撞输入。
- 不新增 MxFramework public API，除非 Motion/Combat 共享索引在后续设计评审中证明必须框架化。
- 不把 catch-up frame 的单帧数据当作最终均值；最终结论必须来自同一 capture scenario 的 before/after Profiler Analyzer CSV 与 warm-run 报告。

## 2026-06-20 Tornado Pair Capture Status

- Added `Tools/Harness/profile_analyzer_capture_pair.ps1` as the repeatable
  Tornado before/after Profile Analyzer artifact harness.
- The script writes artifacts to `ProfileAnalyzer/paired_tornado_capture/`, uses
  a detached `origin/main` worktree at `E:\WGF_pa_base` for the before side, and
  can apply a marker-only baseline instrumentation patch for same-marker Tornado
  query comparison.
- Added `CharacterTestProfileAnalyzerCommandLineCapture.RunPairCapture` so the
  same Tornado capture IEnumerator can run from Unity `-executeMethod`; the
  runner waits for real Play Mode `Time.frameCount` progress and writes `.pdata`
  files to absolute paths outside the tested project's `Temp/`.
- Latest local paired run produced `fresh_before.pdata` with display frames
  `1-247` from `E:\WGF_pa_base_marker` and `fresh_after.pdata` with display
  frames `1-248` from isolated after worktree `E:\WGF_pa_after_current`; both
  exports use `--exclude-frame 200`.
- The refreshed before and after summaries both record `tornadoEvidence` with
  projectile flight, projectile movement, tick VFX, attraction movement, damage,
  and repeated damage all `true`, so the paired capture covers the Tornado
  active-field window on both sides.
- 2026-06-21 follow-up: added
  `Tools/Harness/profile_analyzer_baseline_tornado_markers.patch`, applied only
  inside the detached `origin/main` baseline. It wraps the old generic Tornado
  `QueryCapsule` path in `CharacterTest.InteractionPhysics.TornadoFieldQuery`
  without adding the dynamic index or fast query to the baseline.
- 2026-06-21 follow-up: regenerated
  `fresh_before_after_marker_comparison.md` now includes median, mean, max, and
  GC columns. The same-marker `TornadoFieldQuery` row is comparable: median
  `0.0209ms -> 0.0038ms`, mean `0.0492ms -> 0.0171ms`, max
  `3.5682ms -> 1.4962ms`, GC total `1104B -> 16B`. `TornadoIndexRebuild` remains
  after-only and is listed in validity notes.
- The fresh pair is a same-harness regression/behavior-preservation result, not
  a reproduction of the original user CSV's multi-millisecond Tornado hotspot:
  the fresh baseline `TornadoFieldQuery` median is only `0.0209ms`. Keep the
  large-benefit estimate tied to the historical/user CSV until a heavier fresh
  scenario reproduces that load.
- 2026-06-21 stress follow-up: added a 96-target Tornado pair capture path via
  `CharacterTestProfileAnalyzerCommandLineCapture.RunStressPairCapture` and
  `Tools/Harness/profile_analyzer_capture_pair.ps1 -StressScenario
  -StressTargetCount 96`. The validated run used `E:\WGF_pa_base_stress2` and
  `E:\WGF_pa_after_stress2`, with artifacts under
  `ProfileAnalyzer/paired_tornado_stress_capture/`.
- The stress summaries record `requestedStressTargetCount=96` and
  `preparedHostileCount=96` on both sides. Excluding display frame 200,
  `CharacterTest.InteractionPhysics.TornadoFieldQuery` is now a heavier fresh
  comparable row: median `0.6217ms -> 0.1934ms`, mean
  `0.7413ms -> 0.2039ms`, max `4.4050ms -> 2.0528ms`, GC total
  `26400B -> 16B`.
- The same stress capture shows `InteractionConsumers` already in the target
  range: median `0.0083ms -> 0.0078ms`, mean `0.1518ms -> 0.0970ms`.
  The remaining dominant bucket is Motion: `CharacterTest.CharacterRuntime.Motion`
  median `4.4535ms -> 4.0387ms`, mean `5.4477ms -> 4.5518ms`.
- 2026-06-21 stress GC prewarm follow-up: `CharacterTestDynamicFieldTargetIndex`
  now preallocates targets, XZ cells, active cell lists, pooled cell lists, and
  lazy comparer/dictionary setup during construction; `CharacterTestInteractionPhysicsSystem`
  now starts Tornado query/processed-body buffers at `128`. Added
  `CharacterTestDynamicFieldTargetIndex_FirstStressRebuildAndQueryDoesNotAllocate`
  to cover a 96 hostile target first `Rebuild()` + `QueryVerticalCapsule()` path
  with `0` thread allocation after the index and caller-owned result buffer are
  constructed. This is a code-level fix for the older after `.pdata`
  `TornadoFieldQuery` `16B` / index-rebuild GC artifact; a fresh stress recapture
  is still needed before replacing the artifact row.
- Validation for the stress GC prewarm follow-up: `dotnet build
  MxFramework.Tests.csproj -v minimal`, `git diff --check`, and
  `Tools/Harness/check_code_standards.py --all --summary-only` passed execution.
  Unity MCP is still unavailable at `http://127.0.0.1:8080/mcp`; because the
  current project is open in Unity Editor, BatchMode was not started. Git Bash
  `Tools/Harness/run_lightweight_checks.sh` timed out after 244 seconds in this
  follow-up and is not counted as passed evidence for the incremental change.
- Current conclusion: the Tornado dedicated query optimization is validated
  under a fresh heavy dynamic-target load. If the scene is still too slow, the
  next phase should be Motion / `CombatKinematicMotor` shared dynamic index and
  narrow-phase work, not further Tornado query specialization.
- 2026-06-21 Motion follow-up: `CombatKinematicMotor` now splits its per-step
  cached broadphase view into base movement candidates and step-up-extension
  candidates. The world broadphase is still collected once with the conservative
  step-up envelope, but ordinary movement probes use only candidates that
  overlap the base swept capsule AABB; step-up probes continue to use the full
  extended candidate list. When the base list is empty, the motor skips the main
  collision probe and returns the requested movement directly.
- This Motion follow-up does not change collision shapes, public API, layer /
  source filtering, step-up rules, or surface-material selection. It is a
  hot-path candidate scan reduction for frames where the step-up vertical
  envelope captures high or low objects that cannot collide with the ordinary
  movement sweep.
- Added focused coverage:
  `CombatKinematicMotorTests.Step_StepCacheSkipsBaseProbeWhenOnlyStepPaddingCandidatesExist`.
  The test builds a scene where candidates exist only inside the step-up padding
  band and verifies the motor reports no collision, moves normally, and keeps
  `LastQueryStats.QueryCount == 0` / `NarrowphaseCandidateCount == 0`.
- Validation for this Motion follow-up: `dotnet build
  MxFramework.Tests.csproj -v minimal` passed with existing Unity serialized
  field `CS0649` warnings and `0` errors. `dotnet test` with the focused filter
  exited `0` but still printed no Unity/NUnit details, so it is not counted as
  strong Unity EditMode evidence. `git diff --check` passed with the existing
  line-ending warning for `CharacterTestInteractionPhysicsSystem.cs`.
  `Tools/Harness/check_code_standards.py --all --summary-only` executed with the
  existing `150` report-only unsafe baseline findings. Git Bash
  `Tools/Harness/run_lightweight_checks.sh` passed; changed-files code standards
  scanned `16` first-party files with no findings. Unity MCP `read_console`
  still fails to send to `http://127.0.0.1:8080/mcp`, so a focused Unity
  TestRunner job is still pending.
- 2026-06-21 Motion / Combat broadphase follow-up: `CombatPhysicsBroadphase`
  now has an internal source/layer filtered candidate collection path.
  `CombatPhysicsWorld.Query()` and `CombatKinematicMotor` use it so candidates
  excluded by layer mask or source entity are not copied into the broadphase
  candidate scratch list before the caller scans them. `CombatKinematicMotor`
  also skips collision resolution entirely when `ObstacleLayerMask` is empty,
  avoiding the Step broadphase/cache/probe path for explicit no-collision
  configurations.
- This follow-up does not change public API, collision shapes, hit sorting,
  narrow-phase math, source/layer semantics, step-up behavior, or surface
  material selection. It moves an existing eligibility filter earlier in the
  same query pipeline and removes an impossible-query path.
- Added focused coverage:
  `CombatKinematicMotorTests.Step_ObstacleLayerMaskNoneSkipsBroadphaseAndCollisionProbes`.
  The test verifies a blocking AABB is ignored when the motor's obstacle layer
  mask is `None`, and that `LastQueryStats` reports zero query, broadphase cell,
  broadphase candidate, and narrow-phase candidate work for that step.
- Validation for this broadphase-filter follow-up: `dotnet build
  MxFramework.Tests.csproj -v minimal` passed with existing Unity serialized
  field `CS0649` warnings and `0` errors. `dotnet test` with the focused filter
  exited `0` but printed no Unity/NUnit details, so it is still not counted as
  strong Unity EditMode evidence. `git diff --check` passed with the existing
  CRLF warning for `CharacterTestInteractionPhysicsSystem.cs`.
  `Tools/Harness/check_code_standards.py --all --summary-only` passed execution
  with the existing `150` report-only unsafe baseline findings. Git Bash
  `Tools/Harness/run_lightweight_checks.sh` passed; changed-files code standards
  scanned `16` first-party files with no findings.
- Validation passed for Python syntax, pair script dry run, refreshed detached
  baseline capture, isolated after capture, `git diff --check`,
  `dotnet build MxFramework.Tests.csproj -v minimal`,
  `Tools/Harness/check_code_standards.py --all --summary-only`, and Git Bash
  `Tools/Harness/run_lightweight_checks.sh`; Unity MCP was not available at
  `http://127.0.0.1:8080/mcp`, so no new MCP-driven Unity Console/TestRunner
  result was added in this follow-up.
- 2026-06-21 Motion initialization / contact GC follow-up:
  `CombatKinematicMotor` now initializes its collision scratch and per-step
  candidate-index lists from `Config.MaxCollisionIterations`;
  `CharacterTestCharacterMotionSystem` now creates its shared
  `CombatMotionStepBuffer` with
  `CombatMotionConfig.DefaultMaxCollisionIterations` capacity; and
  `CharacterTestCharacterContactResolver` caches `character:<id>` stable ids so
  repeated movable-object contact pushes do not concatenate a string every hit.
  This does not change public API, collision semantics, Tornado gameplay
  parameters, movable push math, or contact pair text output. Added
  `CombatKinematicMotorTests.Step_BufferedCollisionResultDoesNotAllocateOnFirstCollisionAfterQueryWarmup`.
  `dotnet build MxFramework.Tests.csproj -v minimal` passed with existing
  Unity serialized-field `CS0649` warnings and `0` errors; the focused
  `dotnet test` filter exited `0` but still does not provide strong Unity
  TestRunner evidence.
- 2026-06-21 dynamic contact lookup follow-up:
  `CharacterTestCharacterContactResolver` now prepares a per-frame
  `CombatBodyId -> CharacterTestCharacterEntity` lookup for dynamic characters,
  and `CharacterTestCharacterMotionSystem` builds it once after combat collider
  sync for the current physics tick. `RecordDynamicContactHit()` no longer scans
  `world.Entities` for every dynamic collision target on the normal motion tick
  path; non-prepared callers keep the old fallback scan. Added
  `CharacterTestIntegrationContractTests.CharacterMotion_ContactResolverPreparedLookupResolvesDynamicContactsWithoutSteadyStateAllocation`,
  which covers a 96-character world, dynamic source/target contact counts, and
  `0` thread allocation for repeated prepared lookup cycles after warm-up.
  `dotnet build MxFramework.Tests.csproj -v minimal` passed with existing
   Unity serialized-field `CS0649` warnings and `0` errors; the focused
   `dotnet test` filter exited `0` but still does not provide strong Unity
   TestRunner evidence.
- 2026-06-21 contact pressure grid follow-up:
  `CharacterTestCharacterContactResolver.ResolveCharacterContactPressure()` now
  builds a resolver-owned XZ grid for blocking characters when attempted contact
  velocities exist. Each source queries nearby cells using source radius, the
  maximum current character radius, and the existing pressure slop, then keeps
  the old vertical-range, distance, normal, contact-count, and pressure-velocity
  checks as the authority. Candidate indices are sorted before processing so the
  surviving pair order matches the old nested loop. Cell lists and query buffers
  are pooled after warm-up. `ResolveCharacterOverlaps()` remains a full scan
  because it mutates positions during iteration and needs a separate behavior
  audit before using a static broadphase snapshot. Added
  `CharacterTestIntegrationContractTests.CharacterMotion_ContactResolverPressureUsesSpatialCandidatesWithoutSteadyStateAllocation`,
  which covers a 96-character world, a real nearby contact, a candidate count
  below the full pair scan, and `0` thread allocation for repeated pressure
  resolution after warm-up. `dotnet build MxFramework.Tests.csproj -v minimal`
  passed with `0` warnings and `0` errors; the focused `dotnet test` filter
  exited `0` but produced an empty log, so focused Unity TestRunner evidence is
  still pending. Unity MCP resources still fail to send to
  `http://127.0.0.1:8080/mcp`, so no new MCP-driven Console or Unity
  TestRunner result was added for this incremental change.
- 2026-06-21 contact state apply follow-up:
  `CharacterTestCharacterContactResolver.ApplyCharacterContactState()` no longer
  rewrites every character after the resolver's initial compatibility full
  apply. The resolver records current contact entities as contact counts,
  displacements, or velocities are accumulated, keeps the previous tick's
  applied contact entities, and then writes only current contact entities plus
  previous contact entities that need clearing. A no-contact tick still clears
  the previous contact state, but unrelated characters are not rewritten.
  `ResetCharacterContactState()` remains the full-scan fallback when framework
  physics is unavailable. The pressure-grid focused test now also verifies the
  warmed apply count stays below the 96-character world size and that the next
  no-contact apply clears the prior source/target contact state without touching
  the full world. `dotnet build MxFramework.Tests.csproj -v minimal` passed with
  `0` warnings and `0` errors; the focused `dotnet test` filter exited `0` but
  again produced an empty log, so focused Unity TestRunner evidence is still
  pending. Unity MCP resources still fail to send to
  `http://127.0.0.1:8080/mcp`, so no new MCP-driven Console or Unity
  TestRunner result was added for this incremental change.
- 2026-06-21 contact overlap prefilter follow-up:
  `CharacterTestCharacterContactResolver.ResolveCharacterOverlaps()` still avoids
  a static spatial grid because the solver mutates character XZ positions while
  iterating, but it now builds an internal blocking-character list before the
  nested loop. Each entry caches radius, vertical range, and push weight, while
  the loop continues reading live XZ transforms so earlier displacement remains
  visible to later pairs. The loop scans only blocking entries in the same
  relative order, so null/non-blocking entities are filtered once instead of
  being rechecked for every pair. Added
  `CharacterTestIntegrationContractTests.CharacterMotion_ContactResolverOverlapFiltersNonBlockingCharactersWithoutSteadyStateAllocation`,
  which covers a 96-character world with one real overlap pair and many
  non-blocking characters, verifies source/target contact counts, confirms the
  overlap pair count is below a full world pair scan, checks warmed contact-state
  apply is not full-world, and asserts repeated overlap solve/apply allocates
  `0` bytes after warm-up. `dotnet build MxFramework.Tests.csproj -v minimal`
  passed with `0` warnings and `0` errors; the focused `dotnet test` filter
  exited `0` but again produced an empty log, so focused Unity TestRunner
  evidence is still pending. Unity MCP resources still fail to send to
  `http://127.0.0.1:8080/mcp`, so no new MCP-driven Console or Unity
  TestRunner result was added for this incremental change.
- 2026-06-21 contact info reuse follow-up:
  the overlap prefilter now feeds the pressure phase in the normal Motion tick
  order. `ResolveCharacterContactPressure()` tracks whether the resolver already
  has a blocking contact info list for the current entity collection; if so, it
  builds the pressure XZ grid from that list instead of scanning the full world
  again. The pressure loop also iterates the blocking info list, reusing cached
  radius and vertical range while still reading live XZ transforms from the
  entities, so overlap displacement remains visible to pressure. Standalone
  pressure callers still build the info list on demand. The overlap focused test
  now samples overlap followed by pressure, verifies both overlap and pressure
  pair counts stay below a full 96-character world pair scan, and keeps the
  warmed solve/apply `0` allocation assertion. `dotnet build
  MxFramework.Tests.csproj -v minimal` passed with `0` warnings and `0` errors;
  the focused `dotnet test` filter exited `0` but again produced an empty log,
  so focused Unity TestRunner evidence is still pending. Unity MCP resources
  still fail to send to `http://127.0.0.1:8080/mcp`, so no new MCP-driven
  Console or Unity TestRunner result was added for this incremental change.

- 2026-06-21 lazy Step-up envelope follow-up:
  `CombatKinematicMotor.PrepareStepBroadphaseCache()` now collects the base
  sweep envelope first and stores the widened step-up envelope as a pending
  query. `TryResolveStepUp()` promotes the cache to the widened envelope only
  after a wall AABB candidate passes the existing step-up gates. This keeps the
  previous step-up behavior for real wall/step frames, while ordinary horizontal
  movement with no wall candidate no longer scans the taller Y envelope up
  front. Added
  `CombatKinematicMotorTests.Step_StepCacheDefersStepUpEnvelopeUntilWallCandidate`
  to lock `QueryCount=1`, `BroadphaseCellCount=16`, and `StepUpProbeCount=0`
  for the no-wall horizontal movement path. Current validation: `dotnet build
  MxFramework.Tests.csproj -v minimal` passed with `0` warnings and `0` errors;
  focused `dotnet test` exited `0` but only reported build success, so it is
  weak evidence; `git diff --check` passed with the existing CRLF warning;
  `Tools/Harness/check_code_standards.py --all --summary-only` passed execution
  with the existing 150 report-only unsafe baseline; Git Bash
  `Tools/Harness/run_lightweight_checks.sh` passed with changed path count `63`
  and no changed-file code-standard findings. Unity MCP resources still fail to
  send to `http://127.0.0.1:8080/mcp`, so no new Unity Console/TestRunner
  evidence was added for this follow-up.
- 2026-06-21 movable object body-sync follow-up:
  `CharacterTestMovableNaturalObjectSystem.SyncCombatCollider()` now separates
  unchanged-version skip from dirty position-only sync. If a dirty movable
  object still has the same collider kind, local shape, material, body id, and
  collider id, sync uses `CombatPhysicsWorld.SetBodyPosition()` and marks the
  sync version current instead of running `UpsertBody()` plus collider upsert.
  First registration, missing records, shape/material/extent changes, and shape
  kind replacement still use the full path. The existing
  `RuntimeMovableNaturalObjects_CombatColliderSyncSkipsUnchangedState` coverage
  now also resets an AABB movable object to an elastic sphere and verifies the
  collider is replaced, guarding against incorrectly applying the body-only path
  to shape changes. Current validation: `dotnet build
  MxFramework.Tests.csproj -v minimal` passed with `0` errors; focused
  `dotnet test` exited `0` but only reported build success, so it is weak
  evidence pending Unity TestRunner; `git diff --check` passed with the existing
  CRLF warning; `Tools/Harness/check_code_standards.py --all --summary-only`
  passed execution with the existing 150 report-only unsafe baseline; Git Bash
  `Tools/Harness/run_lightweight_checks.sh` passed with changed path count `63`
  and no changed-file code-standard findings. Unity MCP resources still fail to
  send to `http://127.0.0.1:8080/mcp`, so no new Unity Console/TestRunner
  evidence was added for this follow-up.
- 2026-06-21 movable object contact-grid follow-up:
  `CharacterTestMovableNaturalObjectSystem.ResolveObjectContacts()` now uses a
  pooled scratch-owned XZ grid when `CharacterTestMovableNaturalObjectContactScratch`
  is available, while preserving the old full-scan fallback for no-scratch
  callers. Candidate queries keep source order and stable ascending candidate
  order; the index rebuilds at solver-iteration start and after a resolved pair
  when more later candidates remain, so live contact displacement remains
  visible without switching to a stale pair table. This targets the remaining
  movable-object object-contact `N * N` scan inside the Motion bucket, without
  changing shape overlap tests, vertical checks, displacement split, velocity
  transfer, contact-pair text, material behavior, solver iteration count, public
  API, or introducing unsafe/Burst/Jobs/Unity Physics authority. Added
  `RuntimeMovableNaturalObjects_ObjectContactScratchMatchesFullScanPairResolution`
  to compare the grid path against the old full scan for a three-object contact
  chain, and
  `RuntimeMovableNaturalObjects_ObjectContactScratchQueriesNearbyPairsOnlyAndDoesNotAllocateAfterWarmup`
  to verify nearby-pair candidate narrowing and warmed `0` allocation over
  repeated ticks. `dotnet build MxFramework.Tests.csproj -v minimal` passed with
  `0` warnings and `0` errors. The focused `dotnet test --no-build --filter`
  exited `0` but only reported build success, so it remains weak evidence
  pending Unity TestRunner. Unity MCP tools still fail to send to
  `http://127.0.0.1:8080/mcp`, so no MCP-driven Console/TestRunner result was
  added. `git diff --check` passed with the existing CRLF warning, and
  `Tools/Harness/check_code_standards.py --all --summary-only` passed execution
  with the existing 150 report-only unsafe baseline. Git Bash
  `Tools/Harness/run_lightweight_checks.sh` completed but failed on broad
  branch asset issues outside this follow-up (`katana.fbx` trailing whitespace
  and missing FMOD mac bundle `.meta` files; changed path count `708`), so it is
  not a clean validation signal for this incremental change.
- 2026-06-21 movable object contact cell-envelope guard follow-up:
  `CharacterTestMovableNaturalObjectContactScratch` now stores object-contact
  cell candidates in pooled `ObjectContactCellEntry` values that contain both
  the candidate index list and the aggregate XZ bounds of objects indexed in the
  cell. `QueryObjectContactCandidateIndices()` skips an occupied cell when the
  source query AABB cannot overlap that cell's aggregate object bounds, then
  keeps the existing per-candidate exact bounds filter before emitting sorted
  candidate indices. This preserves object overlap tests, vertical checks,
  contact axis selection, displacement split, velocity transfer, material
  behavior, solver iteration count, fallback full-scan behavior, public API, and
  authority input, while reducing same-cell false-positive scans when a source
  barely crosses a 4m grid boundary. Added
  `RuntimeMovableNaturalObjects_ObjectContactScratchSkipsCellsOutsideQueryEnvelopeWithoutAllocation`
  to verify near candidates survive, far boundary-cell candidates are skipped,
  skipped-cell diagnostics are non-zero, and warmed direct scratch queries
  allocate `0` bytes. `dotnet build MxFramework.Tests.csproj -v minimal` passed
  with `0` errors and the existing CS0649 serialized-field warning baseline.
  Focused `dotnet test --no-build --filter` over the object-contact scratch
  tests exited `0` but again only reported build success, so it remains weak
  evidence pending Unity TestRunner. `git diff --check`, `Tools/Harness/check_docs_health.py`,
  `Tools/Harness/check_code_standards.py --all --summary-only`, and Git Bash
  `Tools/Harness/run_lightweight_checks.sh` all passed; code standards still
  reports only the existing 150 report-only unsafe baseline, and lightweight
  changed-files code standards reported no findings. Focused Unity TestRunner
  evidence remains pending because Unity MCP resources and `read_console` still
  fail to send to `http://127.0.0.1:8080/mcp`.

- 2026-06-21 character overlap contact-grid follow-up:
  `CharacterTestCharacterContactResolver.ResolveCharacterOverlaps()` now uses the
  resolver-owned pooled XZ contact grid for overlap candidate selection instead
  of scanning every blocking character pair for each solver iteration. The grid
  is rebuilt at the start of each overlap solver iteration, candidate indices are
  sorted ascending to preserve source/target order, and the pressure phase keeps
  its existing wider pressure slop while overlap uses radius-only query padding.
  Candidate collection now applies the cached vertical range predicate and exact
  XZ radius/slop range predicate before writing into the caller-owned candidate
  list, so same-cell blockers collected by the 1m grid but outside the current
  solve range no longer enter the pair-solve loop.
  `CharacterTestCharacterMotionSystem.Tick()` also drops the redundant full
  `SyncAllCharacterCombatColliders()` immediately after movable-object tick;
  each character already syncs its own collider after `TickEntity()`, and overlap
  displacement still uses the resolver sync hook after moved iterations. This
  keeps overlap radius/vertical checks, inverse-mass displacement split, contact
  counts, contact displacement accumulation, solver iteration count, public API,
  and authority input unchanged. Added
  `CharacterMotion_ContactResolverOverlapUsesSpatialCandidatesWithoutSteadyStateAllocation`
  to cover 96 blocking characters with one nearby overlap pair plus same-cell
  small blocking decoys outside contact radius, assert candidate pair narrowing,
  and verify warmed overlap solve/apply allocation is `0` bytes.
  `dotnet build MxFramework.Tests.csproj -v minimal` passed with `0` errors and
  the existing CS0649 serialized-field warning baseline. The focused
  `dotnet test --no-build --filter` exited `0` but only reported build success,
  so it remains weak evidence pending Unity TestRunner. `git diff --check`
  passed with the existing CRLF warning, and
  `Tools/Harness/check_code_standards.py --all --summary-only` passed execution
  with the existing 150 report-only unsafe baseline. Git Bash
  `Tools/Harness/run_lightweight_checks.sh` passed with changed path count `63`;
  forbidden path, Unity `.meta` pairing, Docs health, and changed-files code
  standards all passed. Unity MCP resources and `read_console` still fail to
  send to `http://127.0.0.1:8080/mcp`, so no MCP-driven Console/TestRunner
  result was added.
- 2026-06-21 contact pressure active-source guard follow-up:
  `CharacterTestCharacterContactResolver.ResolveCharacterContactPressure()` now
  probes `_pressureVelocities` before each directional `ApplyContactPressure()`
  call and passes the already-read source velocity into the private helper. The
  second direction is still checked after the first direction runs, preserving
  same-pair pressure propagation when `a -> b` creates a new `b` pressure
  velocity. Contact pair detection, contact counts, pressure slop, normal math,
  mass transfer, push scale, ordering, public API, and authority input are
  unchanged; the change only skips directions the old callee rejected for missing
  source pressure velocity and removes duplicate source dictionary lookups for
  active directions. `dotnet build MxFramework.Tests.csproj -v minimal` passed
  with `0` errors and the existing CS0649 serialized-field warning baseline.
  The focused `dotnet test --no-build --filter` over pressure/overlap contact
  resolver tests exited `0` but again only reported build success, so it remains
  weak evidence pending Unity TestRunner. `git diff --check` passed; the current
  broad working tree still emits CRLF normalization warnings, but no whitespace
  error was reported for this follow-up. `Tools/Harness/check_code_standards.py
  --all --summary-only` passed execution with the existing 150 report-only
  unsafe baseline, and `Tools/Harness/check_docs_health.py` passed against the
  current `origin/main` diff after this documentation update. Git Bash
  `Tools/Harness/run_lightweight_checks.sh` did not pass cleanly in the current
  broad working tree: forbidden path, Docs health, and changed-files code
  standards passed, but the harness failed on branch asset issues outside this
  follow-up (`katana.fbx` trailing whitespace and missing FMOD mac bundle
  `.meta` files; changed path count `708`). Unity MCP resources and
  `read_console` still fail to send to `http://127.0.0.1:8080/mcp`, so no new
  MCP-driven Console/TestRunner result was added.
- 2026-06-21 zero attempted pressure velocity guard follow-up:
  `CharacterTestCharacterContactResolver.RecordAttemptedContactVelocity()` now
  applies the existing contact-velocity epsilon before writing the resolver's
  attempted-velocity dictionary. Zero or below-epsilon samples remove the entity
  id instead of leaving a dictionary entry for
  `ResolveCharacterContactPressure()` to reject later; the pressure pass now
  copies already-filtered attempted entries directly into `_pressureVelocities`
  before propagation. This preserves pressure slop, candidate selection,
  contact counts, displacement, pressure propagation, mass transfer, push scale,
  ordering, public API, and authority input, while avoiding
  per-stationary-character attempted-velocity writes and duplicate per-entry
  magnitude filtering in quiet or mostly idle Motion frames. Added
  `CharacterMotion_ContactResolverPressureSkipsZeroAttemptedVelocityWithoutSteadyStateAllocation`
  to place a valid nearby target beside the source, record zero attempted
  velocity, assert no pressure grid cells or candidate pairs are queried, verify
  no contact state is written, and keep warmed `0` allocation coverage.
  `dotnet build MxFramework.Tests.csproj -v minimal` passed with `0` errors and
  the existing CS0649 serialized-field warning baseline. The focused
  `dotnet test --no-build --filter` over the zero-velocity and existing pressure
  resolver tests exited `0` but again only reported build success, so it remains
  weak evidence pending Unity TestRunner. Unity MCP resources and
  `read_console` still fail to send to `http://127.0.0.1:8080/mcp`, so no new
  MCP-driven Console/TestRunner result was added.
- 2026-06-21 contact grid cell-radius guard follow-up:
  `CharacterTestCharacterContactResolver` now records the maximum character
  radius per occupied contact-grid cell and skips a whole occupied cell when the
  minimum XZ distance from source center to that cell AABB is still greater than
  `source radius + cell max radius + slop`. This keeps the existing global-max
  query rectangle as a conservative outer bound, but avoids iterating candidates
  in cells that were only reached because some other far cell had a large
  character radius. The exact vertical/radius predicates and ascending candidate
  sort still decide emitted pairs, so pressure slop, overlap strictness, contact
  counts, displacement, pressure propagation, ordering, public API, and authority
  input are unchanged. The pressure focused test now includes a far diagonal
  large-radius blocker plus same-cell blockers and asserts skipped-cell
  diagnostics are non-zero while keeping warmed `0` allocation coverage.
  `dotnet build MxFramework.Tests.csproj -v minimal` passed with `0` errors and
  the existing CS0649 serialized-field warning baseline. The focused
  `dotnet test --no-build --filter` over pressure/overlap contact resolver tests
  exited `0` but again only reported build success, so it remains weak evidence
  pending Unity TestRunner. `git diff --check` passed for the files touched by
  this follow-up; `Tools/Harness/check_code_standards.py --all --summary-only`
  passed execution with the existing 150 report-only unsafe baseline; and
  `Tools/Harness/check_docs_health.py` passed against the current `origin/main`
  diff. Git Bash `Tools/Harness/run_lightweight_checks.sh` did not pass cleanly
  in the current broad working tree: forbidden path, Docs health, and
  changed-files code standards passed, but the harness failed on branch asset
  issues outside this follow-up (`katana.fbx` trailing whitespace and missing
  FMOD mac bundle `.meta` files; changed path count `708`). Unity MCP resources
  and `read_console` still fail to send to `http://127.0.0.1:8080/mcp`, so no
  new MCP-driven Console/TestRunner result was added.
- 2026-06-21 contact grid entry consolidation follow-up:
  the contact-grid cell-radius metadata is now stored in the same pooled
  `ContactCellEntry` as the candidate index list instead of a second parallel
  dictionary. `QueryContactCandidateIndices()` does one
  `Dictionary<ContactCell, ContactCellEntry>` lookup per occupied cell, then
  reads both `Indices` and `MaxRadiusMillimeters` from that entry; release clears
  and pools the entry and its internal list together. Candidate cell skipping,
  exact vertical/radius predicates, pressure slop, overlap strictness, contact
  counts, displacement, pressure propagation, ordering, public API, and authority
  input are unchanged. `dotnet build MxFramework.Tests.csproj -v minimal` passed
  with `0` errors and the existing CS0649 serialized-field warning baseline. The
  focused `dotnet test --no-build --filter` over pressure/overlap contact
  resolver tests exited `0` but again only reported build success, so it remains
  weak evidence pending Unity TestRunner. `git diff --check` passed for the
  files touched by this follow-up; `Tools/Harness/check_code_standards.py --all
  --summary-only` passed execution with the existing 150 report-only unsafe
  baseline; and `Tools/Harness/check_docs_health.py` passed against the current
  `origin/main` diff. Git Bash `Tools/Harness/run_lightweight_checks.sh` did not
  pass cleanly in the current broad working tree: forbidden path, Docs health,
  and changed-files code standards passed, but the harness failed on branch
  asset issues outside this follow-up (`katana.fbx` trailing whitespace and
  missing FMOD mac bundle `.meta` files; changed path count `708`). Unity MCP
  resources and `read_console` still fail to send to
  `http://127.0.0.1:8080/mcp`, so no new MCP-driven Console/TestRunner result
  was added.

- 2026-06-21 character contact cell aggregate-bounds guard follow-up:
  `CharacterTestCharacterContactResolver.ContactCellEntry` now stores aggregate
  XZ bounds for the character circles indexed in each 1m contact-grid cell.
  `QueryContactCandidateIndices()` first checks whether the source circle AABB
  expanded by the current query slop can overlap that aggregate cell AABB; if it
  cannot, the whole occupied cell is skipped before reading candidate indices.
  The existing cell max-radius guard, exact vertical/radius predicates, pressure
  slop, overlap strictness, contact counts, displacement, pressure propagation,
  sorting, public API, and authority input are unchanged. Added
  `CharacterMotion_ContactResolverPressureSkipsCellAggregateBoundsWithoutSteadyStateAllocation`
  to place a source on a 1m grid boundary, preserve a real nearby pressure
  target, add neighboring-cell decoys whose cell AABB is reachable but aggregate
  bounds are not, and verify skipped-cell diagnostics plus warmed `0` allocation
  over repeated pressure resolve/apply. `dotnet build
  MxFramework.Tests.csproj -v minimal` passed with `0` errors and the existing
  CS0649 serialized-field warning baseline. The focused
  `dotnet test --no-build --filter` over the new pressure aggregate-bounds guard
  and existing pressure/overlap spatial tests exited `0` but again only reported
  build success, so it remains weak evidence pending Unity TestRunner.
  `git diff --check`, `Tools/Harness/check_code_standards.py --all
  --summary-only`, and Git Bash `Tools/Harness/run_lightweight_checks.sh` all
  passed; code standards still reports only the existing 150 report-only unsafe
  baseline, and lightweight changed-files code standards reported no findings.
  Unity MCP `read_console(types=["error"])` and focused EditMode `run_tests`
  still fail to send to `http://127.0.0.1:8080/mcp`, so no MCP-driven Unity
  Console/TestRunner result was added.

- 2026-06-21 character contact cell vertical-bounds guard follow-up:
  `CharacterTestCharacterContactResolver.ContactCellEntry` now also stores the
  aggregate vertical range for character capsules indexed in each contact-grid
  cell. `QueryContactCandidateIndices()` skips an occupied cell when the source
  vertical range cannot overlap that cell's aggregate vertical range, before
  reading candidate indices. The existing XZ aggregate-bounds guard, cell
  max-radius guard, exact per-candidate vertical/radius predicates, pressure
  slop, overlap strictness, contact counts, displacement, pressure propagation,
  sorting, public API, and authority input are unchanged. Added
  `CharacterMotion_ContactResolverPressureSkipsCellVerticalBoundsWithoutSteadyStateAllocation`
  to keep a real same-layer pressure target while adding neighboring-cell decoys
  on a higher vertical layer; the test verifies skipped-cell diagnostics,
  unchanged source/target contact counts, zero decoy contact counts, and warmed
  `0` allocation over repeated pressure resolve/apply. `dotnet build
  MxFramework.Tests.csproj -v minimal` passed with `0` errors and the existing
  CS0649 serialized-field warning baseline. The focused
  `dotnet test --no-build --filter` over the new vertical-bounds guard, the XZ
  aggregate-bounds guard, and existing pressure/overlap spatial tests exited
  `0` but again only reported build success, so it remains weak evidence
  pending Unity TestRunner. `git diff --check`,
  `Tools/Harness/check_code_standards.py --all --summary-only`, and Git Bash
  `Tools/Harness/run_lightweight_checks.sh` all passed; code standards still
  reports only the existing 150 report-only unsafe baseline, and lightweight
  changed-files code standards reported no findings. Unity MCP
  `read_console(types=["error"])` and focused EditMode `run_tests` still fail to
  send to `http://127.0.0.1:8080/mcp`, so no MCP-driven Unity Console/TestRunner
  result was added.

- 2026-06-21 movable object contact vertical-candidate guard follow-up:
  `CharacterTestMovableNaturalObjectContactScratch.QueryObjectContactCandidateIndices()`
  now computes the active object's query Y range once and
  `ObjectContactBoundsOverlap()` rejects vertically disjoint movable-object
  candidates before they enter the sorted candidate result list. This keeps the
  existing XZ grid, query stamp deduplication, candidate sorting,
  `ObjectsOverlap()` / `ResolveObjectPair()` narrow phase, displacement,
  velocity transfer, public API, and authority input unchanged. It is documented
  as a candidate-level guard rather than a cell-level vertical aggregate guard
  because the current object-contact query cells are the source footprint cells,
  so a simple cell aggregate Y range would usually include the source itself.
  Added
  `RuntimeMovableNaturalObjects_ObjectContactScratchSkipsVerticalCandidateBoundsWithoutAllocation`
  to keep a real low-layer target while adding same-footprint high-layer AABB
  and sphere decoys; the test verifies only the low-layer target is emitted,
  raw candidate count remains higher than final candidate count, and warmed
  repeated queries allocate `0` bytes. `dotnet build
  MxFramework.Tests.csproj -v minimal` passed with `0` errors and the existing
  CS0649 serialized-field warning baseline. The focused
  `dotnet test --no-build --filter` over the new vertical candidate guard and
  existing object-contact scratch tests exited `0` but again only reported build
  success, so it remains weak evidence pending Unity TestRunner. `git diff
  --check` passed with only the existing CRLF normalization warning for
  `CharacterTestInteractionPhysicsSystem.cs`; `Tools/Harness/check_code_standards.py
  --all --summary-only` passed execution with the existing 150 report-only
  unsafe baseline; and Git Bash `Tools/Harness/run_lightweight_checks.sh` did
  not pass cleanly in the current broad working tree because of branch asset
  issues outside this follow-up (`katana.fbx` trailing whitespace and missing
  FMOD mac bundle `.meta` files), though forbidden path, Docs health, and
  changed-files code standards passed. Unity MCP resource reads for `instances`,
  `custom-tools`, and `editor/state` still fail to send to
  `http://127.0.0.1:8080/mcp`, so no MCP-driven Console/TestRunner result was
  added.

- 2026-06-21 movable object contact bounds-cache follow-up:
  `CharacterTestMovableNaturalObjectContactScratch.RebuildObjectContactIndex()`
  now refreshes a scratch-owned `ObjectContactStateBounds` list alongside the
  object-contact XZ grid. `QueryObjectContactCandidateIndices()` reads cached
  source and candidate XZ/Y bounds instead of recomputing them from live
  movable-object state for every raw candidate. The cache is cleared by
  `PrepareObjectContactCandidates()` and rebuilt on every existing
  object-contact index rebuild, including the rebuild that follows a resolved
  pair move, so live displacement remains visible to later candidate queries.
  This keeps query stamp deduplication, sorted candidates, XZ envelope guard,
  vertical candidate guard, `ObjectsOverlap()` / `ResolveObjectPair()` narrow
  phase, displacement, velocity transfer, solver count, static-contact path,
  fallback full scan, public API, and authority input unchanged. Bounds cache
  capacity is prepared from the active object count, preserving warmed NoAlloc
  behavior. `dotnet build MxFramework.Tests.csproj -v minimal` passed with `0`
  warnings and `0` errors. The focused `dotnet test --no-build --filter` over
  the existing object-contact scratch tests exited `0` but again only reported
  build success, so it remains weak evidence pending Unity TestRunner.
  `git diff --check` passed with only the existing CRLF normalization warning for
  `CharacterTestInteractionPhysicsSystem.cs`;
  `Tools/Harness/check_code_standards.py --all --summary-only` passed execution
  with the existing 150 report-only unsafe baseline; and Git Bash
  `Tools/Harness/run_lightweight_checks.sh` passed with changed path count `63`
  and no changed-file code-standard findings. Unity MCP resources and
  `read_console(types=["error"])` still fail to send to
  `http://127.0.0.1:8080/mcp`, so focused Unity TestRunner evidence is still
  pending.
- 2026-06-21 contact grid active-cell scan follow-up:
  `CharacterTestCharacterContactResolver.QueryContactCandidateIndices()` now
  keeps the coordinate-rectangle lookup only when that rectangle is no larger
  than the occupied contact-cell list. If one large character radius widens the
  conservative rectangle beyond the number of occupied cells, the resolver scans
  `_activeContactCellEntries` directly and then reuses the same vertical
  aggregate guard, XZ aggregate guard, per-cell max-radius guard, exact
  candidate predicate, and ascending candidate sort. `ContactCellEntry` now
  caches its current `ContactCell` coordinate so the active-cell branch can run
  the existing cell-distance guard without rebuilding keys. This keeps contact
  pair detection, pressure slop, overlap strictness, contact counts,
  displacement, pressure propagation, ordering, public API, and authority input
  unchanged while avoiding empty-cell dictionary probes in sparse wide-radius
  scenes. Added
  `CharacterMotion_ContactResolverPressureScansActiveCellsForSparseWideQueriesWithoutSteadyStateAllocation`
  to lock the wide sparse case, skipped-cell diagnostics, unchanged real
  contact, far-decoy rejection, bounded query-cell count, and warmed `0`
  allocation behavior. `dotnet build MxFramework.Tests.csproj -v minimal`
  passed with `0` errors and the existing CS0649 serialized-field warning
  baseline. The focused `dotnet test --no-build --filter` over the new active
  cell scan test plus existing pressure cell guards exited `0` but again only
  reported build success, so it remains weak evidence pending Unity TestRunner.
  `git diff --check` passed with only the existing CRLF normalization warning for
  `CharacterTestInteractionPhysicsSystem.cs`;
  `Tools/Harness/check_code_standards.py --all --summary-only` passed execution
  with the existing 150 report-only unsafe baseline; and Git Bash
  `Tools/Harness/run_lightweight_checks.sh` passed with changed path count `63`
  and no changed-file code-standard findings. Unity MCP resources and
  `read_console(types=["error"])` still fail to send to
  `http://127.0.0.1:8080/mcp`, so focused Unity TestRunner evidence is still
  pending.

- 2026-06-21 Step cache sweep-entry priority follow-up:
  `CombatKinematicMotor` now records the per-step start bounds / desired delta
  and runs a linear near-first pass over cached broadphase candidate indices.
  The pass estimates swept AABB entry fraction and swaps the most likely
  earliest blocker into the first slot of the base / step-up candidate index
  lists, so the existing best-fraction prefix AABB can skip far candidates
  sooner when grid order starts from the far end of a reverse sweep. It does not
  change public API, collision shapes, layer/source filtering, step-up behavior,
  deterministic result selection, surface material resolution, or authority
  input, and it adds no new collections, unsafe code, Burst/Jobs, LINQ,
  reflection, or Unity Physics dependency. Added
  `CombatKinematicMotorTests.Step_StepCachePrioritizesReverseSweepCandidateBeforePrefixSkip`
  to cover a negative-X sweep where far endpoint AABBs would otherwise be seen
  before the near wall; the test asserts the near wall remains the collision
  target, prefix skips are recorded, and narrow-phase candidate count stays
  bounded. `dotnet build MxFramework.Tests.csproj -v minimal` passed with `0`
  errors and the existing 127 CS0649 serialized-field warnings. The focused
  `dotnet test --no-build --filter` over the new reverse-sweep priority test,
  best-fraction prefix test, and dynamic shape no-GC test exited `0` but again
  only reported build success, so it remains weak evidence pending Unity
  TestRunner; `dotnet vstest` has no suitable provider for the Unity
  `.NETStandard` test assembly. `git diff --check` passed with only the
  existing CRLF normalization warning for `CharacterTestInteractionPhysicsSystem.cs`;
  `Tools/Harness/check_code_standards.py --all --summary-only` passed execution
  with the existing 150 report-only unsafe baseline. Git Bash
  `Tools/Harness/run_lightweight_checks.sh` failed in the current broad working
  tree on unrelated asset issues (`katana.fbx` trailing whitespace and missing
  FMOD mac bundle `.meta` files), while forbidden path, Docs health, and
  changed-files code standards passed. Unity MCP resources still fail at
  `http://127.0.0.1:8080/mcp`; because a Unity 6000.5.0f1 Editor for this
  project is already open, no BatchMode validation was started.

## 验证清单

每个实现 phase 都要：

1. 修改前后运行同一个 clean profile scenario。
2. 对比正常帧 median、P75/P95 和重复 Marker totals。
3. 检查高频路径 GC allocation。
4. 对被修改系统运行相关 EditMode / PlayMode tests。
5. C# 改动运行 `Tools/Harness/check_code_standards.py`，除非任务设置更严格门禁，否则把结果作为 report-only 说明。
6. 文档或 docs/code 混合改动提交前运行 `Tools/Harness/run_lightweight_checks.sh`。
