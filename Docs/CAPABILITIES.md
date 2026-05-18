# MxFramework 能力清单

> 框架当前（2026-05-13 文档校准）**已经能做**什么。按你想做的事查找，不要按 Phase 翻。
>
> 状态标记：✅ v1 可用 | ✅ v0.x 部分可用 | 🔄 设计中 | 📋 规划中

---

## 1. 运行时核心：给游戏实体加属性、挂 Buff、跑 Modifier

### 1.1 属性系统 — 给任何实体挂数值属性

| 能力 | 状态 | 关键 API | 对应模块 |
|------|------|----------|----------|
| 注册属性，读取 base/final value | ✅ v1 | `AttributeStore.RegisterAttribute()` / `GetAttribute()` | Attributes |
| 添加计算阶段修改器（Add/Mul/FinalMul） | ✅ v1 | `IAttributeModifier` / `AttributeStore.AddModifier()` | Attributes |
| 属性变化时收到事件 | ✅ v1 | `AttributeStore.OnAttributeChanged` | Attributes |
| 调试快照 | ✅ v1 | `AttributeStore.CreateSnapshot()` | Attributes |

→ 代码示例：`USAGE.md §3`
→ 测试：`Tests/Attributes/AttributeStoreTests.cs`
→ Demo 运行：`RuntimeVerticalSliceRunner._useHardcoded = true`

### 1.2 Buff 系统 — 生命期管理 + 堆叠策略

| 能力 | 状态 | 关键 API | 对应模块 |
|------|------|----------|----------|
| 添加/移除/清空 Buff | ✅ v1 | `BuffPipeline.AddBuff()` / `RemoveBuff()` | Buffs |
| Tick 驱动过期 | ✅ v1 | `BuffPipeline.TickAll(dt)` | Buffs |
| 同 ID 堆叠刷新 | ✅ v1 | `IBuffStackingPolicy` 默认策略 | Buffs |
| 多层叠加 | ✅ v1 | `maxLayers` 参数 | Buffs |
| 快照 | ✅ v1 | `BuffPipeline.CreateSnapshot()` | Buffs |
| 自定义工厂 | ✅ v1 | `IBuffFactory` | Buffs |
| 配置驱动创建 | ✅ v1 | `ConfiguredBuff` / `ConfigBuffFactory<TConfig>` | Config.Runtime |

→ 代码示例：`USAGE.md §4`
→ 测试：`Tests/Buffs/BuffPipelineTests.cs`

### 1.3 Modifier 系统 — 条件 + 效果组合

| 能力 | 状态 | 关键 API | 对应模块 |
|------|------|----------|----------|
| 添加/替换/移除 Modifier | ✅ v1 | `ModifierPipeline.AddModifier()` / `RemoveModifier()` | Modifiers |
| 条件评估 + 效果执行 | ✅ v1 | `IModifierCondition` / `IModifierEffect` | Modifiers |
| 上下文池化（零分配） | ✅ v1 | `ModifierContext.Get()` / `Push()` | Modifiers |
| 计数器系统 | ✅ v1 | `CounterStore.SetCounter()` / `AddCounter()` / `GetCounter()` | Modifiers |
| 配置驱动创建 | ✅ v1 | `ConfiguredModifier` / `ConfigModifierFactory<TConfig>` | Config.Runtime |

→ 代码示例：`USAGE.md §5`
→ 测试：`Tests/Modifiers/ModifierPipelineTests.cs`, `Tests/Modifiers/CounterStoreTests.cs`

### 1.4 事件总线 — 模块间消息

| 能力 | 状态 | 关键 API | 对应模块 |
|------|------|----------|----------|
| 类型安全订阅/发布 | ✅ v1 | `IEventBus<T>` / `EventBus<T>` | Events |
| 防重入 | ✅ v1 | 发布期间新订阅下次生效 | Events |
| 异常传播 | ✅ v1 | handler 抛异常停止本次发布 | Events |

→ 代码示例：`USAGE.md §2`
→ 测试：`Tests/Events/EventBusTests.cs`

### 1.5 Runtime Host — 运行时组合根和生命周期调度

| 能力 | 状态 | 关键 API | 对应模块 |
|------|------|----------|----------|
| 模块注册和重复 ID 检测 | ✅ v0.1 | `RuntimeHost.RegisterModule()` / `IRuntimeModule.ModuleId` | Runtime |
| 生命周期调度 | ✅ v0.1 | `Initialize()` / `Start()` / `Tick()` / `Stop()` / `Dispose()` | Runtime |
| Tick 分组和稳定排序 | ✅ v0.1 | `RuntimeTickStage` / `Priority` / `ModuleId` | Runtime |
| 错误策略和错误快照 | ✅ v0.1 | `RuntimeHostErrorPolicy` / `RuntimeHostError` / `RuntimeHostDiagnostics` | Runtime |
| 组合根服务表 | ✅ v0.1 | `RuntimeServiceRegistry` / `IRuntimeServiceRegistry` | Runtime |
| 显式 Frame Clock | ✅ v0.1 | `RuntimeFrame` / `RuntimeClock.Step()` / `Reset()` | Runtime |
| Command Buffer | ✅ v0.1 | `RuntimeCommand` / `RuntimeCommandBuffer.Enqueue()` / `DrainForFrame()` | Runtime |
| Command validation result | ✅ v0.1 | `RuntimeCommandValidationResult` / `RuntimeCommandError` / `IRuntimeCommandValidator` | Runtime |
| Replay frame recorder | ✅ v0.1 | `RuntimeReplayRecorder` / `RuntimeReplayFrameRecord` / `RuntimeReplaySnapshot` | Runtime |
| Replay playback runner | ✅ v0.1 | `RuntimeReplayPlaybackRunner` / `IRuntimeReplayFrameDriver` / `RuntimeReplayPlaybackResult` | Runtime |
| Runtime result hash contract | ✅ v0.1 | `IRuntimeHashContributor` / `RuntimeHashAccumulator` / `RuntimeHashCombiner` | Runtime |
| SaveState DTO contract | ✅ v0.1 | `RuntimeSaveState` / Entity / Attribute / Buff / Modifier / Ability / Counter / CustomState DTO | Runtime |
| SaveState provider/restorer contract | ✅ v0.1 | `IRuntimeSaveStateProvider` / `IRuntimeSaveStateRestorer` | Runtime |
| SaveState orchestration | ✅ v0.1 | `RuntimeSaveStateRegistry` / `RuntimeSaveStateCoordinator` | Runtime |
| SaveState structured errors | ✅ v0.1 | `RuntimeSaveStateErrorCode` / `RuntimeSaveStateError` / `RuntimeSaveStateResult<T>` | Runtime |
| SaveState migration pipeline | ✅ v0.1 | `IRuntimeSaveStateMigration` / `RuntimeSaveStateMigrationPipeline` | Runtime |
| SaveState JSON roundtrip | ✅ v0.1 | `RuntimeSaveStateJson.SaveToJson()` / `LoadFromJson()` | Runtime |
| Preview Host adapter | ✅ v0.1 | `RuntimePreviewHostAdapter` | Preview.Runtime + Runtime |

→ 接口：`Interfaces/Runtime.md`
→ 测试：`Tests/Runtime/RuntimeHostTests.cs`, `Tests/Runtime/RuntimeFrameCommandReplayTests.cs`, `Tests/Runtime/RuntimeReplayPlaybackTests.cs`, `Tests/Runtime/RuntimeHashContributorTests.cs`, `Tests/Runtime/RuntimeSaveStateTests.cs`, `Tests/Runtime/RuntimeSaveStateOrchestrationTests.cs`, `Tests/Preview/RuntimePreviewHostAdapterTests.cs`, `Tests/Ability/RuntimeAbilitySliceRuntimeFoundationTests.cs`
→ 验证夹具：`Demo/Tetris/TetrisRuntimeValidation.cs` 用纯 C# Tetris 覆盖 RuntimeHost、CommandBuffer、Replay hash playback、SaveState JSON roundtrip
→ Ability Showcase 已接入：HUD 手动命令经 `RuntimeCommandBuffer` 入队，由 `RuntimeHost` 帧执行；每帧记录 replay result hash；支持 Ability Slice save -> reset -> load -> continue。
→ **不含**: JSON replay export/playback、具体 Gameplay/Combat hash contributor adapter、通用 Gameplay restore 编排接入

### 1.6 App / Scene Flow — 游戏启动状态和场景切换骨架

| 能力 | 状态 | 关键 API | 对应模块 |
|------|------|----------|----------|
| App 状态注册和启动 | ✅ v0.1 | `AppFlowController.RegisterState()` / `Start()` | Runtime |
| Pending 状态切换 | ✅ v0.1 | `AppFlowController.RequestTransition()` / `IAppFlowState` | Runtime |
| AppFlow 诊断快照 | ✅ v0.1 | `AppFlowSnapshot` | Runtime |
| Scene 加载请求和 busy 拒绝 | ✅ v0.1 | `SceneFlowController.RequestLoad()` / `SceneFlowResult` | Runtime |
| SceneFlow 进度和结果快照 | ✅ v0.1 | `SceneFlowSnapshot` / `SceneFlowErrorCode` | Runtime |
| RuntimeHost 模块入口 | ✅ v0.1 | `AppFlowRuntimeModule` / `SceneFlowRuntimeModule` | Runtime |
| Unity SceneManager 适配 | ✅ v0.1 | `UnitySceneFlowDriver` | Runtime.Unity |

→ 接口：`Interfaces/AppFlow.md`
→ 测试：`Tests/Runtime/AppFlowTests.cs`, `Tests/Runtime/SceneFlowTests.cs`
→ **不含**: Loading UI、Addressables 场景加载、项目级 scene manifest、联网 session、WGame 关卡规则

### 1.7 Input — 输入意图、上下文和重绑定

| 能力 | 状态 | 关键 API | 对应模块 |
|------|------|----------|----------|
| Unity Input System 采集适配 | ✅ v0.1 | `InputService` / `InputActionAsset` | Input |
| 每帧输入快照 | ✅ v0.1 | `IInputProvider.Snapshot` / `InputSnapshot` | Input |
| 瞬时意图命令队列 | ✅ v0.1 | `InputCommand` / `InputCommandQueue` | Input |
| Gameplay/UI 等上下文栈 | ✅ v0.1 | `InputContextStack` / `InputContextPolicy` | Input |
| 运行时重绑定和保存 | ✅ v0.1 | `IInputRebindingService` / `InputRebindingService` | Input |
| 本地多人 PlayerInput 适配 | ✅ v0.1 | `LocalUserInputAdapter` | Input |
| 测试/回放输入源 | ✅ v0.1 | `FakeInputProvider` / `RecordedInputProvider` | Input |

→ 接口：`Interfaces/Input.md`
→ 默认配置：`Assets/Input/MxFramework/Config/MxFrameworkInputActions.inputactions`
→ 测试：`Tests/Input/InputCoreTests.cs`
→ **不含**: 项目专属按键布局、UI 提示美术、联网预测和录制文件格式

### 1.8 Character Control — 角色控制编排

| 能力 | 状态 | 关键 API | 对应模块 |
|------|------|----------|----------|
| 统一角色命令 DTO | ✅ v0.1 | `CharacterCommand` / `ICharacterCommandSource` / `CharacterActionRequest` | CharacterControl |
| 控制状态机 | ✅ v0.1 | `CharacterControlStateMachine` / `CharacterControlLockMask` / `CharacterStateChangedEvent` | CharacterControl |
| Combat Motion resolver | ✅ v0.1 | `CharacterMotionResolver` / `CharacterMotionSettings` / `CharacterMotionResult` | CharacterControl + Combat |
| Combat / Gameplay action bridge | ✅ v0.1 | `CharacterActionController` / `ICharacterActionConstraint` / `CharacterActionEvent` | CharacterControl + Runtime + Gameplay + Combat |

→ 接口：`Interfaces/CharacterControl.md`
→ 设计：`Tasks/CHARACTER_CONTROL_RUNTIME_00_DESIGN_CONTRACT.md`
→ 测试：`Tests/CharacterControl/`
→ **边界**: Character Control 不读取 Unity 输入、不调用 Unity Physics、不写 Gameplay HP/Buff/Ability source of truth，也不让 MxAnimation / Animator / Playables root motion 反向驱动权威状态。

---

## 2. 配置系统：用配置数据驱动运行时

### 2.1 配置存取

| 能力 | 状态 | 关键 API | 对应模块 |
|------|------|----------|----------|
| 注册/读取配置表 | ✅ v1 | `IConfigProvider` / `IConfigRegistry` / `MemoryConfigProvider` | Config |
| 表结构定义 | ✅ v1 | `ConfigSchema` / `ConfigField` / `ConfigFieldType` | Config |
| 跨表引用校验 | ✅ v1 | `IConfigReferenceProvider` / `ConfigReferenceRule` / `ConfigValidator` | Config |
| ID 范围校验 | ✅ v1 | `ConfigIdRange` | Config |
| 多语言 key 定义 | ✅ v1 | `LocalizedTextKey` / `LocaleId` / `ILocalizationProvider` | Config |

### 2.2 配置驱动运行时

| 能力 | 状态 | 关键 API | 对应模块 |
|------|------|----------|----------|
| 从配置创建 Buff | ✅ v1 | `ConfigBuffFactory<TConfig>` / `ConfiguredBuff` | Config.Runtime |
| 从配置创建 Modifier | ✅ v1 | `ConfigModifierFactory<TConfig>` / `ConfiguredModifier` | Config.Runtime |
| 从配置创建 Ability | ✅ v0.3 | `BasicAbilityConfig` / `AbilityEffectParameters` / `ConfigAbilityFactory` -> `SimpleAbility` | Config.Runtime + Gameplay |
| Ability Graph 配置映射 | ✅ v0.1 | `AbilityGraphConfig` / `AbilityGraphConfigMapper` -> `AbilityGraphDefinition` | Config.Runtime + Gameplay |
| Ability Authoring Contract | ✅ v0 | `AbilityAuthoringContract` / `AbilityAuthoringContractValidator` / `AbilityAuthoringContractMapper` / `AbilityAuthoringSchema` | Config.Runtime |
| Runtime 配置变更处理 | ✅ v0 | `RuntimeAbilityConfigResolver` / `RuntimeConfigChangeSummary`，Ability 重建、Buff/Modifier 不回溯 | Config.Runtime + Gameplay |
| JSON Patch 文件加载 + 合并 | ✅ v1 | `RuntimeConfigPatchJsonLoader` / `RuntimeConfigPatchMerger` | Config.Runtime |
| 运行时变更集展示 | ✅ v1 | `ConfigChangeSet` | Config.Runtime |

→ Demo Patch 文件：`StreamingAssets/MxFramework/Demo/runtime_config_patch.json`
→ Demo 场景：`RuntimeVerticalSlice.unity` — `_usePatchFile = true`

---

## 3. Runtime AI Planner（运行时 AI 规划系统）

| 能力 | 状态 | 关键 API | 对应模块 |
|------|------|----------|----------|
| 世界状态定义 | ✅ v1 | `IAiWorldState` | AI |
| 顺序规划器 | ✅ v1 | `SequentialPlanner` | AI |
| 优先级目标选择 | ✅ v1 | `PriorityGoalSelector` | AI |
| 动作/条件/效果/传感器 | ✅ v1 | `IAiAction` / `IAiCondition` / `IAiEffect` / `IAiSensor` | AI |
| Agent 根接口 | ✅ v1 | `IAiAgent` / `IAiGoal` | AI |

→ 测试：`Tests/AI/` 目录
→ **不含**: 行为树、GOAP 完整搜索、WGame 专用 AI

---

## 4. 资源管理系统

| 能力 | 状态 | 关键 API | 对应模块 |
|------|------|----------|----------|
| 稳定资源引用 | ✅ v0.5 | `ResourceKey` / `ResourceTypeIds` | Resources |
| Catalog 合并和覆盖 | ✅ v0.5 | `ResourceCatalog` / `ResourceManager.AddCatalog()` / `allowOverride` | Resources |
| Memory Provider | ✅ v0.5 | `MemoryResourceProvider` | Resources |
| Unity Resources Provider | ✅ v0.5 | `ResourcesProvider` | Resources.Unity |
| AssetBundle Provider | ✅ v0.5 | `AssetBundleProvider` / `IAssetBundleDependencyProvider` | Resources.Unity |
| Streaming Catalog Loader | ✅ v0.5 | `StreamingResourceCatalogLoader` | Resources.Unity |
| Mod Package 资源 Catalog | ✅ v0.5 | `mod.json.resourceCatalog` / `ResourceCatalogFilePath` | Config.Runtime + Resources.Unity |
| 资源诊断快照 | ✅ v0.5 | `ResourceDebugSource` / `ResourceDebugSnapshot` | Resources + Diagnostics |
| Catalog 校验 | ✅ v0.5 | `ResourceCatalogValidator` / `ResourceCatalogEditorValidator` | Resources + Editor |
| Sample Catalog Builder | ✅ v0.6.5 | `SampleResourceCatalogBuilder` / `MxFramework/Samples/Generate Resource Catalog` | Editor + Resources |
| Sample Player AssetBundle Catalog | ✅ v0.6.6 | `SamplePlayerResourceCatalogBuilder` / `MxFramework/Samples/Build Player Resource Catalog` | Editor + Resources.Unity |
| Runtime Demo Resource Binding | ✅ v0.6.7 | `RuntimeVerticalSliceRunner.ResourceWarmupSummary` / resource binding diagnostics | Demo + Resources |
| Preload Group / Scene Warmup | ✅ v0.6.8 | `ResourcePreloadService` / `ResourcePreloadPlan.MaxConcurrentLoads` / progress + cancellation | Resources |
| Variant Catalog / Retain Policy | ✅ v0.6.2 | `ResourceVariantProfile` / `ResourceRetainPolicy` / retain diagnostics | Resources |
| Retain Budget / Eviction Policy | ✅ v0.6.9 | `ResourceRetainPolicy.Budgeted` / retained bytes diagnostics / budget eviction | Resources |
| Remote Bundle Provider | ✅ v0.6.3 | `RemoteBundleProvider` / `providerData.url` / SHA-256 cache validation | Resources.Unity |
| Runtime Samples Resource Chain | ✅ v0.6.4 | `RuntimeVerticalSliceSampleResourceTest` / `TempImportedResourceCatalog` / `ResourceKeyConfigProfile.CreateSample()` | Demo + Resources |
| Addressables Adapter | Deferred / Optional | 独立 `MxFramework.Resources.Addressables`，仅在项目已采用 Addressables 时实现 | 不进入默认依赖 |

→ 接口：`Interfaces/Resources.md`
→ 设计：`RESOURCE_MANAGEMENT_SYSTEM.md`
→ 测试：`Tests/Resources/`、`Tests/Config/ModPackageCatalogTests.cs`

---

## 5. MxAnimation 表现动画系统

| 能力 | 状态 | 关键 API | 对应模块 |
|------|------|----------|----------|
| noEngine 动画表现契约 | ✅ v0.1 | `MxAnimationSetDefinition` / `MxAnimationActionBinding` / `IMxAnimationBackend` | Animation |
| Unity Playables Backend | ✅ v0.1 | `UnityPlayablesAnimationBackend` | Animation.Unity |
| Combat 表现桥 | ✅ v0.1 | `CombatMxAnimationUnityBridge` / `ICombatMxAnimationPresentationEventSink` | Combat.Animation.Unity |
| Clip Registry + Mapping | ✅ v0.2 | `MxAnimationClipRegistry` / `IMxAnimationMappingProvider` / `MxAnimationClipRegistryExporter` | Animation + Editor |
| Presentation Sync Contract | ✅ v0.2 | `MxAnimationPresentationSyncState` / `MxAnimationPresentationEventDedupeKey` | Animation |
| Layer Weight + AvatarMask | ✅ v0.2 | `MxAnimationLayerDefinition` / `MxAnimationLayerWeightRequest` / `ResourceTypeIds.AvatarMask` | Animation + Animation.Unity |
| Warmup + Resource Version Validation | ✅ v0.2 | `MxAnimationWarmupService` / `MxAnimationWarmupDefinition` / `MxAnimationWarmupIssue` | Animation + Resources |
| 1D Locomotion Blend Demo | ✅ v0.2 | `MxAnimationBlend1DDefinition` / `MxAnimationBlend1DRequest` / `MxAnimationBlend1DCalculator` | Animation + Animation.Unity + Demo |
| Bake MVP | ✅ v0.2 | `MxAnimationBakeArtifact` / `MxAnimationBakeArtifactValidator` / `CombatBakedWeaponTraceAdapter` | Animation + Editor + Combat |
| Provider-switchable Animation Package Loading | ✅ v0.3 | `MxAnimationPackageExpectation` / `MxAnimationPackageCatalogValidator` / `MxAnimationResourceTypeIds` | Animation + Resources |
| Mod Animation Package Override | ✅ v0.3 | `MxAnimationModOverrideDefinition` / `MxAnimationModOverrideMerger` / `MxAnimationModOverrideMergeResult` | Animation + Resources |
| MxAnimation System Showcase | ✅ playable validation | `MxAnimationShowcaseDemoBootstrap` / `MxAnimationSystemShowcase.unity` / `MxAnimationShowcaseDemoTests` | Demo + Animation.Unity |

→ 接口：`Interfaces/Animation.md`
→ 手测：`Demo/MX_ANIMATION_SYSTEM_SHOWCASE.md`
→ 测试：`Tests/Animation/`
→ **边界**: MxAnimation 只负责表现状态和资源加载诊断；不进入 Combat authority、Replay hash、命中、取消、伤害或权威移动。

---

## 6. Audio / FMOD 音频系统

| 能力 | 状态 | 关键 API | 对应模块 |
|------|------|----------|----------|
| noEngine 音频服务契约 | ✅ v0.1 | `IAudioService` / `AudioService` | Audio |
| 可替换音频后端 | ✅ v0.1 | `IAudioBackend` / `NullAudioBackend` | Audio |
| 稳定音频定义查询 | ✅ v0.1 | `IAudioDefinitionProvider` / `AudioEventDefinition` / `AudioBusDefinition` / `AudioParameterDefinition` | Audio |
| 播放请求、句柄和结构化结果 | ✅ v0.1 | `AudioPlayRequest` / `AudioHandle` / `AudioResult` / `AudioPlayResult` | Audio |
| 参数、Bus 音量和静音控制 | ✅ v0.1 | `SetParameter()` / `SetBusVolume()` / `SetBusMuted()` | Audio |
| 音频诊断快照 | ✅ v0.1 | `AudioDebugSnapshot` / `AudioDebugActiveEvent` / `AudioDebugBusState` | Audio |
| FMOD 后端运行时脚手架 | ✅ Runtime Scaffold | `FmodAudioBackend` / `FmodAudioBackendOptions` | Audio.FMOD |
| RuntimeHost 音频 Tick 模块 | ✅ v0.1 | `AudioRuntimeModule` / `RuntimeTickStage.PostSimulation` | Audio.FMOD + Runtime |
| FMOD 设置和 bank 校验工具 | ✅ v0.1 | `FmodAudioSetupValidator` / `FmodAudioSetupReport` | Audio.FMOD.Editor |

→ 接口：`Interfaces/Audio.md`
→ 设计：`AUDIO_SYSTEM_FMOD.md`
→ 任务：`Tasks/AUDIO_FMOD_M1_CONTRACT.md`, `Tasks/AUDIO_FMOD_M2_BACKEND.md`, `Tasks/AUDIO_FMOD_M3_VALIDATION.md`
→ 测试：`Tests/Audio/`
→ **边界**: `MxFramework.Audio` 不依赖 `UnityEngine`、`UnityEditor`、`FMODUnity` 或 `FMOD.Studio`；FMOD 真实播放仍依赖项目提供 FMOD bank / event / bus 配置。

---

## 7. Observability / Debug UI：统一开发观察入口

| 能力 | 状态 | 关键 API | 对应模块 |
|------|------|----------|----------|
| Debug source 注册 | ✅ v0.1 | `FrameworkDebugSourceRegistry.Register()` / `Unregister()` | DebugUI + Diagnostics |
| Snapshot 聚合和错误隔离 | ✅ v0.1 | `DebugUiSnapshotAggregator.Refresh()` / `DebugUiDashboardViewModel` | DebugUI |
| UI 可见性状态 | ✅ v0.1 | `DebugUiVisibility.Hidden / Collapsed / Expanded` | DebugUI |
| UI Toolkit overlay shell | ✅ v0.1 | `DebugUiOverlayController` / `DebugUiOverlayViewModelBinder` | DebugUI.Toolkit + UI.Toolkit |
| RuntimeHost adapter | ✅ v0.1 | `RuntimeHostDebugSource` | DebugUI.Adapters + Runtime |
| Gameplay / Combat adapters | ✅ v0.1 | `GameplayDiagnosticSnapshotDebugSource` / `GameplayComponentWorldDebugSource` / `CombatDebugSnapshotDebugSource` | DebugUI.Adapters |
| Logging / Resources 接入 | ✅ v0.1 | 复用 `LogDebugSource` / `ResourceDebugSource` | Logging.Diagnostics / Resources |

→ 接口：`Interfaces/DebugUI.md`
→ 任务：`Tasks/PHASE13_OBSERVABILITY_AND_DEVELOPER_WORKFLOW.md`
→ 测试：`Tests/DebugUI/`
→ **边界**: Debug UI 默认只读；Debug UI 展开、折叠、tab、刷新暂停等表现状态不进入 Replay、SaveState 或 Runtime hash。

---

## 8. 运行时 Demo：打开 Unity 按 Play 就能看到效果

| 能力 | 状态 | 说明 |
|------|------|------|
| 硬编码属性+Buff+Modifier 闭环 | ✅ v1 | `RuntimeVerticalSliceRunner._useHardcoded = true` |
| 配置驱动闭环 | ✅ v1 | `_useConfigDriven = true`，从 Demo 配置创建对象 |
| Patch 文件驱动闭环 | ✅ v1 | `_usePatchFile = true`，加载 JSON Patch 覆盖配置 |
| 场景对象预览 | ✅ v0.5 | `MxPreviewSceneTargetConfig` 编辑态配置，运行时动态生成 `MxPreviewSceneTarget` (IBuffTarget) |
| 属性变化 / Buff 状态 / ChangeSet 显示 | ✅ v1 | 运行中实时显示 Attack、Buff 列表、变更集 |
| Gameplay Runtime Core | ✅ v0.2 | `MxFramework.Gameplay` 提供 `RuntimeEntity` / `SimpleAbility` / Target / Effect，`_useAbilitySlice = true` 自动挂载 Demo Runner |
| Gameplay World v0 | ✅ v0.1 | `GameplayWorld` / `RuntimeEntityRegistry` / stable tick / world snapshot |
| Gameplay Team / Tag / Status | ✅ v0.1 | `GameplayTeamRelations` / `GameplayTagSet` / `GameplayStatusSet` |
| Gameplay Targeting Service | ✅ v0.1 | `GameplayTargetQuery` / `GameplayTargetingService` / rejected reasons |
| Gameplay Ability Runtime Adapter | ✅ v0.1 | `GameplayAbilityRegistry` / `GameplayAbilityRuntimeService` / structured runtime result |
| Ability Runtime Graph Contract | ✅ v0.1 | `AbilityGraphDefinition` / `AbilityGraphNode` / `AbilityGraphValidator` |
| Ability Runtime Graph Executor | ✅ v0.1 | `AbilityGraphRuntimeExecutor` / `AbilityGraphExecutionContext` / `AbilityGraphExecutionResult` |
| Ability Graph Phase Timeline | ✅ v0.1 | `AbilityGraphTimelineDefinition` / `AbilityGraphTimelineScheduler` / `AbilityGraphTimelinePhaseGate` |
| Ability Graph Diagnostics / Trace | ✅ v0.1 | `AbilityGraphDiagnosticSnapshot` / `AbilityGraphExecutionTrace` |
| Ability Graph Runtime Hash | ✅ v0.1 | `AbilityGraphHashContributor` 接入 `IRuntimeHashContributor` |
| Gameplay Runtime Hash | ✅ v0.1 | `GameplayHashContributor` / `GameplayComponentWorldHashContributor` 接入 `IRuntimeHashContributor` |
| Gameplay Component SaveState | ✅ v0.1 | `GameplayComponentWorldSaveStateProvider` 通过 schema SaveState adapters 捕获 / 恢复 ComponentWorld |
| Gameplay Component State Systems | ✅ v0.1 | `GameplayLifecycleCleanupSystem` 在 Resolution phase 清理 `PendingDestroy` component entity |
| Gameplay Component Spawn Definitions | ✅ v0.1 | `GameplayComponentSpawnRegistry` / `GameplayComponentSpawnCommandSystem` 按显式 definition 创建带初始 components 的 entity |
| Gameplay Component Attribute Runtime | ✅ v0.1 | `GameplayAttributeSetComponent` / `GameplayAttributeCommandSystem` 提供 component-native 属性状态、事件、hash 和 SaveState |
| Gameplay Component Ability Command Bridge | ✅ v0.1 | `GameplayComponentAbilityCommandSystem` / `GameplayComponentAttributeDeltaAbility` 支持 component entity 执行 self attribute delta ability |
| Gameplay Component Ability Targeting | ✅ v0.1 | `GameplayComponentTargetingService` / `GameplayComponentAbilityRequestStore` 支持 generation-safe explicit target、team/lifecycle/tag/status filter |
| Gameplay Component Ability Rules | ✅ v0.1 | `GameplayComponentAbilityRuleSet` / `GameplayAbilityCooldownComponent` 支持 cooldown gate、attribute cost gate、hash 和 SaveState |
| Gameplay Component Runtime Vertical Slice | ✅ v0.1 | `RuntimeHost` 驱动 spawn、attribute、targeting、ability rules、cleanup、hash 和 SaveState 的最小闭环 |
| Gameplay Component Runtime Showcase | ✅ Runtime Slice | `GameplayComponentRuntimeShowcase` / `GameplayComponentRuntimeShowcaseRunner` 展示 spawn、target、ability rules、cleanup、events、hash 和 SaveState；Editor 菜单可生成 Unity 场景，尚未提交 Playable 场景 |
| Gameplay Component Buff / Modifier Runtime | ✅ v0.1 | `GameplayComponentBuffSetComponent` / `GameplayComponentModifierSetComponent` 提供 component-native buff / additive modifier state、cleanup、hash 和 SaveState |
| Config Driven Ability | ✅ v0.3 | `_useAbilitySlice = true` + `_useConfigDrivenAbility = true`，通过命名化 `AbilityEffectConfig` 驱动 Strike / Ignite |
| Gameplay Diagnostic Snapshot | ✅ v0.1 | `GameplayDiagnosticSnapshotBuilder.Build(...)` 汇总 Entity / Attribute / Buff / Modifier / Ability / Event 只读诊断状态 |
| Gameplay World Diagnostics | ✅ v0.1 | `GameplayWorldDiagnostics` / `GameplayWorldDiagnosticsSummary` |
| Runtime Config Change Handling | ✅ v0 | config-driven Ability 通过 `RuntimeAbilityConfigResolver` 创建，Demo 显示 source / policy / changed / rebuilt / failed 摘要 |
| UI Toolkit Runtime Showcase | ✅ M6 accepted | `RuntimeVerticalSliceConfigWindow` 编辑配置资产；HUD 支持手动控制、Patch / Mod Package 加载、Ability 重建、Old/New 配置对比、Diagnostic View、Mini Game Feedback，以及 `MxStatBar` / `MxCommandButton` / `MxStatusBadge` / `MxEventLog` / `MxPanelTabs` 复用控件 token |
| Runtime Foundation Showcase Path | ✅ v0.1 | `RuntimeAbilitySliceRunner` 通过 `RuntimeHost` 驱动 command drain、simulation tick、diagnostics/replay hash；HUD 手动按钮走 `RuntimeCommandBuffer`；Ability Slice 支持 SaveState 恢复 |
| Tetris Runtime Validation | ✅ v0.1 | 纯 C# Tetris validation fixture 覆盖固定帧输入、gravity/lock/line clear、replay playback hash、hash mismatch 和 SaveState JSON roundtrip |
| Tetris Playable Demo | ✅ v0.1 | `Assets/Scenes/TetrisRuntimeValidation.unity`，UI Toolkit UXML/USS 棋盘；键盘输入经 `RuntimeCommandBuffer` 进入 `RuntimeHost` 后推进 Tetris |
| Breakout Runtime Validation | ✅ v0.3 | 纯 C# Breakout validation fixture 覆盖连续运动、AABB 碰撞、关卡推进、砖块类型/HP/道具砖、多球、Wide/Slow/Multi/ExtraLife/Laser 道具、预发球滚动、按球拍位置发射方向、Replay hash、hash mismatch 和 SaveState JSON roundtrip |
| Breakout Playable Demo | 待补齐场景 | 当前仓库保留 `BreakoutPlayableDemo` / `BreakoutAppFlowDemo` / UI Toolkit 资产，但 Breakout 场景资产尚未提交，因此不标记为 Playable |
| Marble Maze Playable Demo | ✅ v0.1 playable | `Assets/Scenes/MarbleMazeBoot.unity` / `Assets/Scenes/MarbleMazeGameplay.unity`，Unity Physics 作为 Rigidbody/Collider 边界权威；输入经 `DefaultInputService` / `InputSnapshot` 转为 `RuntimeCommandBuffer` tilt 命令；RuntimeHost 记录 physics sample/checkpoint、计时、诊断 hash、Replay playback 和 SaveState JSON roundtrip |

→ 场景：`Assets/Scenes/RuntimeVerticalSlice.unity`, `Assets/Scenes/TetrisRuntimeValidation.unity`
→ 运行器：`Demo/RuntimeVerticalSliceRunner.cs`, `Demo/GameplayComponentRuntime/GameplayComponentRuntimeShowcaseRunner.cs`, `Demo/Tetris/TetrisPlayableDemo.cs`, `Demo/Breakout/BreakoutPlayableDemo.cs`, `Demo/Breakout/BreakoutAppFlowDemo.cs`
→ UI：`Assets/UI/MxFramework/Showcase/GameplayShowcase.uxml`, `Assets/UI/MxFramework/Tetris/TetrisPlayableDemo.uxml`, `Assets/UI/MxFramework/Breakout/BreakoutPlayableDemo.uxml`

---

## 8. Unity Editor 工具

| 能力 | 状态 | 说明 |
|------|------|------|
| Framework Manager 窗口 | ✅ v1 | 框架状态面板 |
| Config Workbench | ✅ v1 | 配置编辑工作台 |
| Field Inspector | ✅ v1 | 字段检查器 |
| Buff Authoring Workflow | ✅ v1 | Unity 内 Buff 创作 |
| WGame Buff Logic 面板 | ✅ v1 | WGame Buff 逻辑展示 |
| Runtime Preview Server | ✅ v0.5 partial | 菜单 `MxFramework / Runtime Preview / Start Server`；03.5A/B 已落地，03.5C result mapping / 03.5E UI status 继续收口 |
| Resource Catalog Validator | ✅ v0.5 | `ResourceCatalogEditorValidator` 使用 `AssetDatabase` 校验资源存在性和类型 |

---

## 9. 外部 Buff 创作（独立于 Unity）

### 8.1 Authoring Core / CLI

| 能力 | 状态 | 说明 |
|------|------|------|
| Buff Schema 定义和校验 | ✅ v0.2 | `MxFramework.Authoring.Core` |
| Patch 创建/覆盖/合并预览 | ✅ v0.2 | `authoring cli merge-preview` |
| 校验报告生成 | ✅ v0.2 | `authoring cli report` |
| Workflow 步骤上下文 | ✅ v0.1 | step-by-step 上下文 |
| 项目包 Manifest 导出 | ✅ v0.1 | Schema/Enum/Reference/Workflow 导出 |
| 运行时 Patch 导出 | ✅ v0.1 | `authoring runtime-patch export` → `mx.runtimeConfigPatch.v1` |
| EditorServer 本地 API | ✅ v0.2 | `editor serve` (port 4873)；新增 `/api/mod/diagnose`（GET/POST） |
| Mod Package 校验 | ✅ v0.1 | `package validate` — schemaVersion/路径穿越/格式/kind-layer 10 条规则 |
| Mod Package 运行时加载 | ✅ v0.1 | `RuntimeModPackageLoader.LoadFromDirectory()` — StreamingAssets 包驱动 Demo |
| Package Catalog / LoadPlan | ✅ v0.1 | `RuntimeModPackageDiscovery.Discover()` + `RuntimeModPackageLoadPlanBuilder.Build()` — 包扫描/校验/排序 |
| 多包 Runtime Patch 合并 | ✅ v0.1 | `RuntimeModPackagePatchMerger.Merge(loadPlan, baseRegistry)` — 按 LoadPlan 顺序合并，last-write-wins，输出 override 报告 |
| Mod Package Loadout / 启用状态 | ✅ v0.1 | `RuntimeModPackageLoadoutJson` + `RuntimeModPackageLoadPlanBuilder.Build(catalog, loadout)` — 按 `PackageKey` 持久化启用组合 |
| Mod Diagnostic Snapshot | ✅ v0.1 | `RuntimeModDiagnosticSnapshotBuilder.Build(...)` + `RuntimeModDiagnosticSnapshotJson.SaveToJson(...)` — 统一导出 catalog/loadout/loadPlan/merge 诊断快照 |
| Mod Diagnostic CLI | ✅ v0.1 | `mod diagnose` — 包发现/加载计划/合并诊断 → JSON 快照；支持 `--container`/`-c`、`--loadout`、`--output`、`--pretty`、`--fail-on-warning`；退出码 0/2/5/1 |
| Mod Diagnostic Service | ✅ v0.2 | `ModDiagnosticService.BuildSnapshot(...)` 由 CLI 与 EditorServer 共用 |

### 8.2 Buff 编辑器 Web UI

| 能力 | 状态 | 说明 |
|------|------|------|
| 选择包 / 创建编辑 Buff | ✅ v0.4 | `http://127.0.0.1:4873/Tools/MxFramework.Authoring.Editor/web/` |
| BuffType 感知字段（DamageByAttr 等） | ✅ v0.4 | 按类型显示专属字段 |
| 字段校验 / 必填标记 / 即时提示 | ✅ v0.4 | 编辑器内实时校验 |
| 保存 Patch | ✅ v0.4 | 写入包目录 |
| 导出运行时 Patch 按钮 | ✅ v0.4 | 调用 CLI 导出为 Unity 可用 JSON |
| 生成校验报告 | ✅ v0.4 | 字段级报告 |
| 运行时预览按钮 | ✅ v0.4 | 连接 Unity Preview Server，回传结果 |
| Mod 诊断面板 | ✅ v0.5 | 诊断 Summary/Packages/Loadout/Load Plan/Overrides/Issues，支持“刷新诊断”和“复制诊断 JSON” |

→ 使用说明：`AUTHORING_EDITOR_USAGE.md`
→ 目录：`Tools/MxFramework.Authoring/samples/buff-preview/`

### 8.3 Unity Preview Server

| 能力 | 状态 | 说明 |
|------|------|------|
| WebSocket RPC 服务器 | ✅ v0.4 | `PreviewRpcServer` — EditMode 可启动 |
| 预览世界（场景目标优先） | ✅ v0.4 | `ScenePreviewWorld` — 回退到 Dummy |
| Patch 加载 / applyBuff / reset / getSnapshot / getLogs | ✅ v0.5 partial | Preview RPC 协议；`RuntimePreviewAdapter` 已统一 apply / tick / snapshot / reset，`RuntimePreviewResult` 已映射 Buff / Attribute / Damage explanation / errors / performance / `configMetadata` |
| 全链路：编辑器→Export→Scene→PlayMode | ✅ v0.5 partial | Scene preview 已接入 Runtime Patch loader / merger、factory 主路径和 result mapping；03.5E 仍需 UI 状态收口 |

---

## 10. Combat 确定性物理 / 动作运行时

| 能力 | 状态 | 说明 |
|------|------|------|
| Combat Physics Query v0 | ✅ v1 | `CombatPhysicsWorld` 支持 Ray / AABB / Sphere / Capsule / Sector 查询和稳定排序 |
| Broadphase v0 | ✅ v1 | Grid / spatial hash candidate 裁剪，debug report 输出 raw / dedup / post-filter / hit 计数 |
| Hit Query Debug Report | ✅ accepted | `CombatPhysicsQueryDebugReport`、Showcase 摘要、Play Mode hit / miss explain 和 authoring 测试已验收 |
| Hit Resolve Bridge v0 | ✅ v0.1 | `HitResolveSystem` 支持 hit-once 去重、owner 防护、阵营过滤、动态目标状态解析、blocked/resolved 事件派发和 Action window state adapter |
| Combat Animation RuntimeHost Modules | ✅ v0.1 | `CombatActionRuntimeModule` / `CombatWeaponTraceRuntimeModule` / `CombatAnimationDiagnosticsModule` 使用 RuntimeHost stage + priority 推进动作、武器轨迹和诊断快照 |
| Runtime World Lifecycle | ✅ accepted | `Revision`、`CreateStats()`、`MoveBody()`、`RemoveBody()`、`RemoveCollider()`、`Clear()`、`CopyBodiesTo()`、`CopyAabbCollidersTo()` 已验收 |
| Combat Motion v0 | ✅ accepted | 固定帧移动、重力、跳跃、grounded、静态 AABB 阻挡、Motion -> Physics World 同步和 Play Mode smoke 已验收 |
| Combat Motion v1 Capsule Proxy | ✅ accepted | `CombatMotionCapsuleProxy`、capsule narrow-phase sweep、skin width / no penetration、Showcase capsule summary 和 Combat regression 已验收 |

---

## 11. WGame 数据审计（框架设计依据）

| 文档 | 状态 | 覆盖内容 |
|------|------|----------|
| `WGAME_DATA_AUDIT.md` | ✅ | 数据源规模、职责、证据 |
| `WGAME_DATA_RELATION_AUDIT.md` | ✅ | 跨源引用关系 |
| `WGAME_SPLIT_GRAPH_AUDIT.md` | ✅ | Split JSON 字段位序 |
| `WGAME_TABLE_FIELD_INDEX.md` | ✅ | Luban/BaseData 表字段 |
| `WGAME_ENUM_MAPPING_AUDIT.md` | ✅ | 枚举/Flags 映射 |
| `ABILITY_JSON_AUDIT_RESULT.md` | ✅ | Ability JSON 结构审计 |
| `CONFIG_FORMAT_STRATEGY.md` | ✅ | 配置格式策略与 Phase 9 契约输入 |
| `Interfaces/ConfigSchemaSeeds.md` | ✅ | 首批 Schema 种子清单 |
| `Interfaces/ConfigReferenceRulesPhase9.md` | ✅ | Phase 9 引用规则白名单 |
| `Tasks/PHASE9_CLOSEOUT_REPORT.md` | ✅ closed | Phase 9 closeout 接受记录 |

---

## 12. Core 工具层

| 能力 | 状态 | 说明 |
|------|------|------|
| 优先级堆 `Heap<T>` | ✅ v1 | `MxFramework.Core` (noEngine) |
| 无序列表 `UnsortList<T>` | ✅ v1 | `MxFramework.Core` (noEngine) |
| 位运算 `BitUtils` | ✅ v1 | `MxFramework.Core` (noEngine) |
| 字符串工具 `ZString` | ✅ v1 | `MxFramework.Core` (noEngine) |
| 向量扩展 | ✅ v1 | `MxFramework.Core.Unity` (Unity 依赖) |
| 随机抽表 | ✅ v1 | `MxFramework.Core.Unity` (Unity 依赖) |

→ 测试：`Tests/Core/` 目录

---

## 📋 规划中 / 部分完成

| 能力 | 状态 | 任务文档 |
|------|------|----------|
| Phase 11 Runtime Gameplay Foundation | ✅ Accepted / Closed | `Tasks/PHASE11_RUNTIME_GAMEPLAY_GOAL.md`, `Tasks/PHASE11_RUNTIME_GAMEPLAY_CLOSEOUT.md` |
| Runtime Foundation 01 Host Core | ✅ v0.1 Host Core + Ability Showcase 接入 | `Tasks/RUNTIME_FOUNDATION_01_RUNTIME_HOST.md` |
| Runtime Foundation 02 Frame/Command/Replay | ✅ v0.1 Core + Ability Showcase Command/Hash 接入；Replay playback runner 已完成，JSON replay 待办 | `Tasks/RUNTIME_FOUNDATION_02_FRAME_COMMAND_REPLAY.md`, `Tasks/RUNTIME_FOUNDATION_04A_REPLAY_PLAYBACK.md` |
| Runtime Foundation 03 SaveState | ✅ v0.1 Contract + Ability Showcase restore + SaveState orchestration；通用 Gameplay restore 接入待办 | `Tasks/RUNTIME_FOUNDATION_03_SAVE_STATE_SERIALIZATION.md`, `Tasks/RUNTIME_FOUNDATION_04C_SAVE_STATE_ORCHESTRATION.md` |
| Runtime Foundation 04 v1 parallel closeout | ✅ Completed / Verified | `Tasks/RUNTIME_FOUNDATION_04_V1_PARALLEL_CLOSEOUT.md` |
| App / Scene Flow 01 Foundation | ✅ v0.1 Foundation | `Tasks/APP_SCENE_FLOW_01_FOUNDATION.md` |
| Breakout Runtime Showcase 01 | ✅ Runtime Validation v0.3 | `Tasks/BREAKOUT_RUNTIME_SHOWCASE_01.md`；当前仓库未提交 Breakout 场景资产，不标记为 Playable |
| Marble Maze Unity Physics Showcase 01 | ✅ playable | `Tasks/MARBLE_MAZE_UNITY_PHYSICS_SHOWCASE_01.md` |
| AI Assist 闭环（Authoring AI Assist） | 📋 | `Tasks/AUTHORING_EDITOR_04_AI_ASSIST.md` |
| Mod/Dev 双模式 | 📋 | `Tasks/AUTHORING_EDITOR_05_MOD_DEV_MODES.md` |
| Unity Bridge 导出 | 📋 | `Tasks/AUTHORING_EDITOR_06_UNITY_BRIDGE.md` |
| Runtime Preview 03.5 closeout | 🔄 | `Tasks/AUTHORING_EDITOR_10_RUNTIME_PREVIEW_03_5_REAL_GAMEPLAY_LOOP.md` |
| AIAction 迁移试点准备（AIAction Config） | 🔄 Pilot 01 verified, Pilot 02 planned | `Tasks/AI_ACTION_MIGRATION_PILOT_02_REAL_DATA_ENTRY.md` |
