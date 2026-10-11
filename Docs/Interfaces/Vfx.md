# VFX 接口

> Status: Current
>
> Tick-driven VFX runtime intent / instance layer v0.1.

## 职责

`MxFramework.Vfx` 提供 noEngine 的 VFX 意图和实例层。它统一由 `RuntimeHost` tick 驱动，支持 replay hash、SaveState、诊断和资源引用；Unity `GameObject`、`ParticleSystem`、VFX Graph、Rendering event 只属于表现 adapter，不进入 Runtime authority、Replay hash 或 SaveState。

表现Pose／VFX预算的接入与当前失败边界统一见[Pose/VFX LOD工程权威](../Guides/PRESENTATION_POSE_VFX_LOD.md)。Unity表现adapter的cadence不能写入本模块Runtime authority；专项源码／回执由该说明维护，不从backend存在推定全部预算验收完成。

## 程序集

| 程序集 | 依赖 | 说明 |
| --- | --- | --- |
| `MxFramework.Vfx` | Core, Runtime, Resources, Diagnostics | noEngine VFX definition / request / instance runtime |
| `MxFramework.Vfx.GameplayBridge` | Vfx, Gameplay, Runtime | 从 `GameplayRuntimeEventFanout` 消费 Gameplay 事件并生成 VFX request |
| `MxFramework.Vfx.CombatBridge` | Vfx, Combat, Core, Runtime | 从 Combat action / hit presentation 事件和 `CombatPhysicsWorld` 生成确定性 VFX anchor |
| `MxFramework.Vfx.RenderingBridge` | Vfx, Rendering, Runtime | 可选地把 VFX runtime event 映射到 `IRenderDataPublisher` |
| `MxFramework.Vfx.Unity` | Vfx, Resources, Runtime, UnityEngine | 加载 prefab、实例化并播放 Unity 表现 |
| `MxFramework.Combat.Projectiles.Unity` | Combat, Core, Resources, UnityEngine | 读取 projectile snapshot 的 view-only 飞行表现 presenter；不是 VFX authority |

## 公开接口

| 接口/类型 | 用途 |
| --- | --- |
| `VfxDefinitionId` / `VfxInstanceId` | 稳定定义和实例 ID |
| `VfxVector3` / `VfxPose` / `VfxAnchor` | noEngine 定点姿态和锚点；`VfxPose` 同时携带欧拉旋转偏移和缩放，供 Unity presenter 在解析实体/挂点锚点后应用 |
| `VfxParameterBlock` | 四个整数参数槽，供 bridge 传递强度、伤害、事件 payload 等可 hash 值 |
| `VfxDefinition` / `VfxDefinitionRegistry` | VFX 定义、`ResourceKey` prefab 引用、默认 lifetime 和定义 hash |
| `VfxSpawnRequest` / `VfxStopRequest` / `VfxRebuildRequest` | Runtime tick 消费的创建、停止和显式重建意图；`VfxSpawnRequest.ExpireStopMode` 决定自然到期策略，`PlaybackRatePermille` 以 1000 = 1 倍的确定性整数携带实例播放速率，`SourceStartOffsetFrames / SourceEndOffsetFrames` 携带 60 Hz 源帧剪辑区间，`VfxStopMode` 支持 `Complete`、`Dissipate` 与跳过退出阶段的 `Immediate` |
| `VfxRuntimeModule` | `RuntimeHost` 模块，drain 专用 VFX `RuntimeCommandBuffer` 并推进 active instances |
| `VfxRuntimeEvent` / `VfxRuntimeEventFanout` | VFX 表现事件和单次 drain fanout |
| `VfxRuntimeHashContributor` | active instances、pending requests 和 definition hash 的 replay hash contributor |
| `VfxRuntimeSaveStateProvider` | active instances、pending requests、next ids 的 SaveState provider/restorer |
| `IVfxTriggerResolver` / `VfxTriggerMapResolver` | Gameplay / Combat / project trigger 到 VFX definition 的映射 |
| `GameplayRuntimeEventFanout` | Gameplay 公共事件单次 drain fanout，Rendering 和 VFX bridge 可共享 |
| `GameplayVfxBridge` | Gameplay ability / attribute / lifecycle event 到 VFX request 的桥 |
| `CombatVfxBridge` | Combat action frame event / hit presentation event 到 VFX request 的桥 |
| `CombatVfxPhysicsAdapter` | `CombatPhysicsWorld.Query` first hit 到 deterministic VFX anchor 的投影 |
| `VfxConfigBinaryImporter` | 读取 generated `vfx_definitions.bytes` / `vfx_trigger_maps.bytes`，生成现有 VFX definition registry 和 trigger resolver |
| `VfxRenderingBridge` | VFX spawn/stop/expire 到 Rendering publisher 的可选桥 |
| `UnityVfxPresenter` | 初始化期缓存 `ResourceKey(GameObject)` prefab handle、预热 pool、缓存 pooled view 的 `ParticleSystem[]`；支持 Spawn、同 InstanceId Rebuild 和 view-only 退出尾迹回池 |
| `UnityVfxLifecycleController` | Published Prefab 的五阶段表现控制器；缓存 `Build/Rebuild/Loop/Complete/Dissipate` 根与 ParticleSystem，阶段推进为 `NoAlloc after initialization` |
| `CombatProjectileUnityPresentationConfigJsonImporter` | 读取 generated projectile flight presentation binding JSON，生成 view-only presenter registry |
| `CombatProjectileUnityPresenter` | 热路径读取 `CombatProjectileActiveView`，兼容路径读取 `CombatProjectileRuntimeState`，按 projectile id 同步飞行 prefab 的位置和朝向 |
| `IVfxUnityPrefabBindingSource` | 初始化期按稳定 `ResourceKey(GameObject)` 提供 Unity Prefab 投影；复制绑定为 `LowFreqAlloc`，缓存后的单键解析为 `NoAlloc` |
| `VfxPublishedUnityBindingAsset` | `vfx_published_bindings.json` 的生成 Unity 对象投影；不是绑定事实源 |

## 规则

- `MxFramework.Vfx` 保持 `noEngineReferences=true`，不得引用 Gameplay、Combat、Rendering、UnityEngine 或 UnityEditor。
- VFX runtime 使用独立 `RuntimeCommandBuffer`；不得 drain Gameplay command buffer。
- Gameplay 事件需要多个消费者时使用 `GameplayRuntimeEventFanout`，不要让 Rendering / VFX bridge 分别调用 `GameplayRuntimeModule.DrainEvents`。
- VFX event 需要多个表现消费者时使用 `VfxRuntimeEventFanout`，不要让 Unity presenter / Rendering bridge 分别 drain。
- `VfxSourceDomains.ActionPresentation` (30) is reserved for view-only cues emitted by deterministic action timelines, for example CharacterAction `SpawnVisualCue` windup events. These cues may enter `VfxRuntimeModule` as pending spawn requests, but they must not create damage, projectile state, input side effects, or Unity Physics queries.
- CharacterAction bridge 把公共 `MxAttachmentTransform` 投影为 `VfxAnchor + VfxPose`：跟随挂点使用稳定 socket hash，跟随角色使用实体根锚点，固定世界使用世界锚点；偏移空间决定 pose 采用 Attached 还是 SourceEntity 语义。该公共数据不包含 VFX 生命周期、停止策略或参数槽。
- CharacterAction 的实例整体尺寸在 bridge 投影 `VfxPose.Scale` 时与公共 attachment scale 相乘；播放速率和源帧前后截断进入 request、active snapshot、SaveState 和 runtime hash。前截断先按源时间预推进 ParticleSystem、VFX Graph 和生命周期控制器，再应用实例播放速率；后截断只缩短有限资源的有效自然寿命。`UnityVfxPresenter` 从预热时缓存的 ParticleSystem 原始 `simulationSpeed` 计算实例速率，避免池化复用时倍率累乘；生命周期阶段与 VFX Graph `playRate` 使用同一倍率，预热命中后的 presenter 视图记录与 receiver 通知路径不再新建包装或搜索组件；保留含实例 ID 的诊断名称及其字符串分配。Core 每个成功 spawn 仍创建一个不可变 `VfxInstanceSnapshot`（`AllocByDesign`）；诊断名称格式化、容量增长、池耗尽及自定义 receiver 不包含在 NoAlloc 保证中，不能宣称端到端 0 GC。
- CharacterAction 时间轴把有限 VFX 当作源剪辑：普通拖边缘固定另一端并反算 `0.05–16` 的播放速率，`Alt` 拖边缘修改源帧前后截断；循环模式的普通拖拽只改人工窗口。VFX 尾巴允许越过动作结束线，但触发帧仍必须落在动作范围内。`defaultLifetimeFrames <= 0` 的资源不能使用完整播放，截止与循环仍可使用人工窗口，且不启用后截断或自动变速。
- `VfxRuntimeCommandFactory.Stop(frame, instanceId, traceId)` 默认使用 `VfxStopMode.Complete`；显式重载可选择 `Dissipate` 或 `Immediate`。`Rebuild(frame, instanceId, traceId)` 只接受 active InstanceId，保持其 Runtime 身份、Anchor 和参数，并发布 `Rebuilt`；未知或已停止实例产生确定性 `Rejected`。
- Stop 与自然 Expired 会立即移除 Runtime active state；`UnityVfxPresenter` 可让 `Complete/Dissipate` 作为 view-only 尾迹继续播放并在安全清理后回池。恢复存档或 late attach 应调用 `SynchronizeActiveInstances()` 进入 Rebuild，不伪装为首次 Build。StopMode、pending Rebuild 与事件顺序进入 SaveState/replay hash；ParticleSystem 时间和阶段帧仍不进入 gameplay authority。
- CharacterTest placeholder skill actions use a temporary minimum duration of 90 frames. Fireball / Fireball Burst windup VFX is authored on the action `presentationTrack` and bridged by the CharacterTest composition root before `VfxRuntimeModule` ticks. Its displayed clip length comes from the selected resource catalog lifetime and event-local trim/rate, with no fixed-duration fallback. `UnityVfxPresenter.SyncActiveAnchors()` may follow the caster view anchor each presentation frame; the projectile is still spawned only by the existing gameplay request frame.
- replay/hash/SaveState 只包含 VFX definition hash、pending requests、active instance ids、anchor、参数和 lifetime；Unity prefab 实例、ParticleSystem 状态、Unity instance id、Unity Physics 结果不进入权威状态。
- `vfx_definitions.json` 和 `vfx_trigger_maps.json` 保留为 authoring、diff、调试和兼容格式；Player / Playable runtime bootstrap 优先读取同路径 `vfx_definitions.bytes` 和 `vfx_trigger_maps.bytes`。binary loader 校验 header magic、formatVersion、catalogKind、schemaVersion、entryCount 和 stableHash，成功路径是启动期 `LowFreqAlloc`；`VfxDefinitionRegistry` lookup、trigger resolve、`VfxRuntimeModule` tick 和 Unity presenter 热路径不变。
- CharacterTest Player artifact refresh 会把 VFX Definition / Trigger Map JSON 和 `.bytes` mirror 到 `Assets/StreamingAssets/MxFramework/CharacterTest/RuntimeCatalogs/Vfx/...`。Player 默认先读 `Application.streamingAssetsPath` mirror；warm-run report 会把 VFX Definition / Trigger Map 纳入 expected binary catalog summary，非 Binary 命中会让 Editor build-run 汇总标记为 `finished-binary-catalog-miss`。
- Projectile persistent Flight/Trail VFX 不进入 `VfxRuntimeModule`，由 `CombatProjectileUnityPresenter` 直接读取 `CombatProjectileActiveView` 做 view-only 跟随；snapshot state 只保留给 debug / save / network 兼容路径。Projectile source one-shot 由下述 lifecycle bridge 发出；target/material/shield reaction 仍可独立通过 `CombatVfxBridge` -> `VfxRuntimeModule` -> `UnityVfxPresenter` 叠加播放。
- Current projectile lifecycle bridge (2026-07-18): v2 `spawn/directImpact/areaImpact/blockedImpact/expire` one-shots are emitted by `CombatProjectileVfxBridge` with compact `VfxDefinitionId` directly into `VfxRuntimeModule` under `VfxSourceDomains.ProjectilePresentation` (22); v2 `flightBody/trail` remain pooled persistent Unity views owned by `CombatProjectileUnityPresenter`. Late attach/restore rebuilds persistent views only and never replays one-shots. This standard projectile-source overlay is separate from target/material/shield reactions, Effect Timeline consumers and mechanic/custom presenters.
- `VfxRuntimeModule.ResetTransientState(RuntimeFrame)` is a tooling/replay-only deterministic reset (`NoAlloc after initialization`). It clears active, pending, due, drained and event state, resets instance/request/command sequences, emits no Stop/Expire events, and retains the definition registry and module ownership. A reset may intentionally seek backward; commands earlier than the supplied reset frame remain rejected by `RuntimeCommandBuffer`.
- Projectile flight prefab binding 可以由 `Assets/MxFrameworkGenerated/Combat/Projectiles/projectile_presentation_bindings.json` 生成；该 JSON 只提供 `projectileDefinitionId -> ResourceKey(GameObject)` 和 view name prefix，不进入 VFX runtime hash、SaveState 或 replay。
- 正式 VFX Prefab 绑定由 `Assets/Config/MxFramework/Vfx/vfx_published_bindings.json` 统一声明，生成投影为 `Assets/MxFrameworkGenerated/Vfx/vfx_published_unity_bindings.asset`，并单向生成 application `ResourceCatalog` 中的全部 `domain.vfx` entry。Manifest 是完整稳定 `ResourceKey`、address 与正式 Prefab 的权威；生成后的 Catalog 不得反向创建或刷新 Manifest。CharacterTest 初始化时先注册旧 SceneProfile fallback，再按投影中的同 address 覆盖，因此正式绑定存在时旧 Inspector Prefab 字段不能反向覆盖它。Editor-only `Library/MxFramework/VfxPlaytestOverrides.json` 可在 session 创建前装饰同一 binding source；Player 和非 Editor 环境不读取本机覆盖。
- `VfxPublishedUnityBindingAsset` 必须保存在同名脚本文件中，由 Unity Editor 重新生成对象投影；生成器会替换历史上 `m_Script` 为空的无效投影。Character Action VFX 资源卡、选择器和隔离时间轴预览都读取同一 `IVfxUnityPrefabBindingSource`，因此 UI 标注的实际 Prefab 与真实 Editor 试玩解析一致。
- VFX 创作工作台不选择技能槽位，也不修改 CharacterAction、Projectile 或 gameplay 配置。它只产出可评审、可发布并具有稳定 `ResourceKey` 的独立 VFX；Character Action 编辑器通过资源卡选择该 `ResourceKey`，并使用同一 `IVfxUnityPrefabBindingSource` 做真实时间轴预览。发布 Prefab 更新不会改写消费方配置，消费方改键也不会回写 VFX Draft/Profile/Catalog。
- 工作台提供两种明确的稳定烘焙动作：`烘焙并发布` 原位更新稳定 Prefab 并将状态置为 `Candidate + ReviewNeeded`；`烘焙为正式资源` 经人工确认后，在同一事务内原位更新 Prefab、生成绑定当前 GUID/hash/Composition/Preview 证据的 Ready approval receipt，并将 Published Binding 与 Authoring Catalog 置为 `Production + Ready`。后续普通烘焙会使新内容回到 `ReviewNeeded`，但不会更改 `ResourceKey`、Prefab path 或 GUID。
- Fireball Burst 继续由三个独立稳定引用组成：`vfx.magic.fireball_burst.charge`、`.flight`、`.impact`。Charge/Impact 进入 `VfxRuntimeModule -> UnityVfxPresenter`；Flight 由 `CombatProjectileUnityPresenter` 跟随 Projectile。这个运行时消费关系不赋予 VFX 创作工作台技能配置权；技能级验收或成套发布属于独立发布工具。带 `UnityVfxLifecycleController` 的 Flight 新建时走 Build、late attach/已有 Projectile 走 Rebuild、Projectile 消失走 Complete 并在退出尾迹完成后回池；旧无 Controller Prefab 保持兼容播放路径。
- Fireball Burst 和 Meteor Barrage 的 source explosion 已由 `AreaPulseTrigger -> ProjectilePresentation` 独占。CharacterTest generic `CombatHit` bridge 会精确排除这两个已迁移的 source-overlay profile，避免按 area target 数重复播放相同 impact VFX；其它 target/material/shield reaction 与未迁移的 generic hit profile 保持独立。
- CharacterTest Arc Mine 的 `vfx.magic.arc_mine.flight` 由 carrier/drop projectile presentation binding 播放；`vfx.magic.arc_mine.deploy` 由 trap runtime 在落地部署时发出一次性 VFX spawn request；`vfx.magic.arc_mine.fuse` 由 trap runtime 在地雷待机阶段周期刷新，并在引信开始时按 tuning 的引信时长覆盖引信表现；`vfx.magic.arc_mine.trigger` 在爆炸时发出。四个 prefab 都是 view-only，不能决定 projectile 落点、mine trigger、fuse 或 blast damage。
- CharacterTest 的 `Fireball_Burst.prefab` / `Fireball_BurstImpact.prefab` / `Flame_Lance.prefab` / `Flame_LanceImpact.prefab` 是测试用 view resource：flight prefab 由 projectile presenter snapshot 跟随，impact prefab 由 VFX runtime spawn；这些 Unity prefab 都不参与 projectile authority、damage、hash、SaveState 或 replay。
- 确定性物理第一版只消费 `CombatPhysicsWorld` 查询结果和 Combat 固定帧 correlation；不提供通用粒子物理、刚体、布料或 Unity Physics 确定性封装。

## Authoring 边界

- VFX 美术资产语义、Primitive/Recipe 复用、Published Effect、质量 fallback、预算、预览和 Development Agent 管理的目标设计统一见 `Docs/VFX_ASSET_AUTHORING_DESIGN.md`。
- VFX 语义属性、Source Snapshot、Component/Element/Shader Profile、Composition Draft/Receipt v3、精确重定时和烘焙属性驱动的详细合同与 Current/Design 状态矩阵见 `Docs/VFX_SEMANTIC_PROPERTY_RETIMING_DESIGN.md`。Current ParticleSystem v3 工作台包含整数帧 Layer schedule、`PlaybackWindow` 加六种内部重定时模式、完整 MinMaxCurve/Gradient、成员矩阵、Renderer/Material/Custom Data 首批 Adapter、guarded Draft/Profile 写入、Preview/Bake 等价证据和 0 B 稳态 Scheduler/Property Driver。`PlaybackWindow` 只改变 Layer 调度结束帧，保持子 VFX 原速和原始时间参数。95 项 Registry 是属性合同，不等于 95 项在任意素材上可写；真实能力必须同时具备人工确认的 Profile Binding 和 Adapter。当前持久化 Profile Manifest 仍为空；AutoSuggested Profile 可以参与隔离预览和 Candidate 编译，解析结果由 receipt 冻结。只有持久化共享 Profile 发布要求 Manifest 与 Profile 文件双方 Confirmed；建议 Profile 不赋予人工 Ready。Trails 当前仅增加 Authoring-only 结构化 Snapshot 和 `PreserveNormalizedLifetime` ExtendTail 合同，不改变公共 Runtime API；Trail 通用属性、SubEmitter/Collision、其它未确认 Shader 与 VFX Graph 继续 fail-closed。
- `UnityVfxBakedLayerScheduler` 和 `UnityVfxBakedPropertyDriver` 是烘焙 Prefab 内部的 Unity presentation 组件：初始化时缓存 Layer/ParticleSystem/Renderer/Shader Property ID，之后按生命周期浮点帧推进，分类为 `NoAlloc after initialization`。它们不进入 Core Runtime authority、SaveState、网络或 gameplay hash，也不能由 Draft 在运行时动态组装。
- `UnityVfxLayerWindowEndPolicy` 是 Scheduler 的公共、无分配配置枚举：`StopEmitting` 在窗口结束帧停止发射并允许自然消散，`StopAndClear` 在结束帧立即清理，`DissipateThenClear` 在结束前停止发射并于边界清理。`Configure(...)` 的六参数重载仅在 Editor 烘焙/初始化阶段调用，分类为 `AllocByDesign`；`AdvancePhase` 继续保持初始化后 `NoAlloc`。
- `Assets/Config/MxFramework/Vfx/vfx_authoring_catalog.json` 使用 `mx.vfx.authoring.v2`；v1 仍可读取。v2 在原稳定 ID 上增加人工审批证据、Exemplar 标记和 Ready 依赖传播，但不是 `VfxDefinitionRegistry`、Resource Catalog 或 gameplay 配置的替代品。Primitive、Recipe、review status、审批证据、Creative Brief、候选 receipt、评测、缩略图和 scanner report 都不进入 Runtime hash、SaveState、网络 snapshot 或 gameplay authority。
- `Assets/Config/MxFramework/Vfx/vfx_generation_templates.json` 是独立的 Authoring-only Provider/Style/认证模板/质量 Profile 合同。它不能提供 gameplay 参数，也不能把 ReviewNeeded 条目当成正向 Exemplar。Creative Brief → reuse/generation/evaluation/promotion/registration 的 planner 只生成计划或报告；当前 Unity Editor executor 负责白名单 ParticleSystem/LineRenderer Prefab、材质和程序纹理。Graph clone、Local CLI/HTTP 和 Cloud 生成尚未接入并保持 fail-closed。
- 当前 Catalog 已覆盖全部 40 个当前 VFX，`strictCoverage=true`；自动迁移项保持 `ReviewNeeded`。工具链包含 scanner、稳定 report、save/bake/coverage plan、Prefab/thumbnail evidence、静态预算 gate 和治理指标，`MxVfxShowcase` 只读展示语义、Recipe 摘要、预算和 fallback。strict coverage 只证明目录完整，不能代替人工美术 Ready 审批。
- `UnityVfxPresenter` 现在通过 `IUnityVfxBackendController` 支持可选表现后端；独立 `MxFramework.Vfx.Unity.VfxGraph` adapter 在 VFX Graph package 可用时提供 `VisualEffect Stop/Reinit/Play`、Value0..3 Exposed Property 映射、compute/platform capability check 和 ParticleSystem fallback 抑制。`vfx.magic.fireball_burst.impact` 已作为首个 package-template `.vfx` 技术样板接入 Published Prefab，并保留 ParticleSystem fallback；Catalog 状态仍为 `ReviewNeeded`，不能描述为已有经美术验收的 Graph 资源库。
- 外部 Authoring 工具可以读取和验证语义 JSON，但不得手写 Unity prefab、scene、`.vfx`、`.mat` 或其它 Unity 序列化资产；这些变更必须通过 Unity Editor、Unity MCP 或正式 Editor 工具完成。
- Candidate Sandbox 实体目录为 Git 忽略的 `Assets/VFX/MxCandidates/`。候选默认 `Draft`；只有通过完整硬门禁和人工选择后才可事务化晋升为 `ReviewNeeded`。任何 Development Agent、生成器、视觉评分或迁移脚本都不得自行设置 `Ready`。
- `Assets/Piloto Studio/` 是 Authoring-only、不可修改的授权原材料库。`mx.vfx.source-library.v1` 深度索引和 `vfx_source_curation.json` 只服务 Development Agent 检索、Primitive 提取与 Recipe 组装；未修改支持资产可由项目 Primitive 直接引用，需要修改时才创建带来源 diff 的派生副本。Runtime 只消费烘焙后的项目 Prefab，绝不读取源索引、组件语义、Recipe 或提取 receipt。

## 测试入口

`Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Vfx/VfxRuntimeTests.cs`

`Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Vfx/VfxCreativeCandidateTests.cs`

`Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Vfx/VfxCreativeQualityEvidenceTests.cs`

- 烘焙 Lifecycle 的自动 Unity Update 使用 scaled delta，与战斗暂停/全局速度及默认 PS 保持一致；实例 rate 只乘一次。隔离 Editor preview 仍显式调用 Advance(delta)。暂停时 Complete/Dissipate 尾迹冻结，Immediate/ForceClear 可立即清理。
- RestoreRuntimeState/ResetTransientState 不发播放事件。同会话恢复边界必须调用 UnityVfxPresenter.SynchronizeActiveInstances；该低频操作清理 active/retiring 并按恢复快照 Rebuild，不承诺粒子位级重演。
- `UnityVfxLifecycleController.Advance` 保留跨阶段剩余源帧；Continuous Loop 大步进只重启当前周期并应用余量，不按错过周期逐个追播。Complete/Dissipate 到达清理边界后停止发射，保留现有粒子并等待 `IsAlive(true)` 或源帧清理上限；原生 Trails/SubEmitters 的完整消散仍需 Unity 验收。
- `IUnityVfxPresentationBudgetSource.ShouldUpdatePresentation(VfxInstanceSnapshot)` 是可选、NoAlloc 的 active-view cadence 接口，由共享表现预算 adapter 提供同一表现遍历内稳定的决策并复用已有观察，未知 subject 应返回 true。`UnityVfxPresenter` 保留原五参数构造函数签名，委托到预算为 null 的原行为；新增六参数重载的末尾 `presentationBudgetSource` 可由组合根注入，false 只跳过 active 锚点和定时后端更新，scaled delta 累积到下次更新，在非 Immediate Stop 前先消费主体余量再开始尾巴。生命周期自动 Update、停止/重建/恢复、retiring 定时后端与完成检测/回收不经过预算门禁；该接口不改变 ParticleSystem/VFX Graph 自动模拟、renderer 可见性或 gameplay authority，不单独计算距离，也不代表项目 adapter 已接入。

- P4 ordinary ParticleSystem 可选预算（已接入可选接口，当前生产Decorative声明为0）：同一 `IUnityVfxPresentationBudgetSource` 可额外实现 `IUnityVfxParticleBudgetAdmissionSource.GetParticleBudgetImportance(VfxInstanceSnapshot)`，显式返回 `Unknown/Required/Decorative`，默认Unknown全频，不按名字判断。只接管有效owner、循环Local/unit-scale/无其它driver且通过模块安全检查的简单Decorative PS；暂停auto，复用现有积压scaled delta及LOD决策逐系统手动推进，retiring每帧推进/回收。重要性提升保持单一手动时钟并全频，恢复automatic只在Rebuild/Spawn边界；World/Custom/非循环/缩放/复杂模块不接管。退场已StopEmitting且准入禁止SubEmitter/其它driver，因此按剩余particleCount完成；暂停的循环PS粒子归零后IsAlive仍可能为true。无新增当前资源的重要性声明，无Graph预算覆盖；当前原生helper烟测及8组时标/倍率通过，但Presenter退场/后续回收与恢复完整组仍待验收，不能据此宣告生产预算或性能收益已验证。
