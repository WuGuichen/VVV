# MxFramework 使用手册

> Status: Guide
>
> 版本 0.3.19 | 2026-08-10
>
> 本文面向业务开发和 AI 辅助开发。目标是“先看这里就能接入”，不要靠通读源码理解基础模块。

2026-10-12补充：配件调用方应从真实ACK及 `CombatProjectileSpawnEvent.Request` 冻结来源，再使用阶段owner。不能把当前Host、动作模板或外来JSON作为延迟伤害来源权威。三张双刃配件目前提供待发布配置和验证交接，尚非已发布示例；见[实施说明](Tasks/TACTICAL_DOUBLE_EDGE_ACCESSORY_IMPLEMENTATION.md)。

## 1. 使用原则

### Tactical Card Demo：混合切片与进度（2026-10-08）

打开 `Assets/Scenes/TacticalCardDemoScene.unity`，进入首页后新建或 Continue 到整备，确认部署并保存 checkpoint 后开战。Planning 停世界，选牌预留费用，确认才推进双方 AI、世界和动作冷却；执行结束停稳后按本关卡当前补齐目标3保留未用牌并补差额，再暂停；Stage的三张硬检查是现有实现差距，不是设计上的三张上限。普通敌目标自动选择，地点卡另行选点；敌方真实当前行为卡及已选定待推进卡按已确认[UI 基石](../StorySource/玩法基石/半即时动作卡牌UI基石.md#敌方行为卡明牌2026-10-05-已确认待实现核查)推进；2026-10-08 源码与FairyGUI发布已接入，原生短验1/1通过（展开、换敌不改目标、暂停冻结、重开）；自然hover、死亡、离屏与换卡视觉未纳入，范围见[明牌实现与验证边界](Interfaces/CharacterTest.TacticalCard.md#敌方行为卡明牌设计2026-10-05-已确认待实现核查)。Continue 恢复战斗外清单/整备，新建保留其他进度，不恢复战斗现场。具体操作、当前显式默认卡组与历史验收范围见[Tactical Card Demo走查](Walkthroughs/TACTICAL_CARD_PLAYABLE_DEMO_WALKTHROUGH.md#入口与操作)，保存权威见[战术内容指南](Guides/CHARACTER_TACTICAL_CONTENT.md#存档)。基地更新见[基地场景组装](Guides/ENVIRONMENT_BASE_SCENE_ASSEMBLY.md)。

整备中选中玩家控制角色，展开顶部工具栏，经「更多 → 领取机制试用卡」领取已发布D417/40定点牵引、41记录回返点、42返回记录点r1；点完成，再点出战卡组旁＋，从卡库点卡面＋加入。零动作槽不要求再绑定动作；领取不自动加入出战，不改默认获取范围或新进度清单。操作、精确版本及拒绝/重试边界见[战术内容指南](Guides/CHARACTER_TACTICAL_CONTENT.md#整备中的机制试用卡领取与装配)。

正式战斗HUD当前接入手牌拖放直出和地点卡的范围/落点输入；独立确认、取消按钮在当前FairyGUI视图隐藏，底层仍通过原选择、输入、确认命令结算。设置音频页源码接入音量滑条及键盘微调；这些本地UI改动的完整自然游玩、跨分辨率效果与回归状态须看各自实际验收，本文同步不追加通过结论。

行动/效果设计及替身、体型最新确认边界统一见[主基石](../StorySource/玩法基石/行动卡与交互效果卡设计基石.md)。共用净空/停止重定位前置及31项测试不等于完整机制。AI Lab真实出牌操作与测试准备边界见[工作台工程权威](Guides/CHARACTER_TEST_RUNTIME_AI_LAB_BEHAVIOR_WORKBENCH.md#真实出牌试验当前实现与边界)。

### AI Lab真实出牌测试（2026-10-08）

进入方式、独立试验的费用／卡区／准备事务、复位与当前验收边界统一见[工作台工程权威](Guides/CHARACTER_TEST_RUNTIME_AI_LAB_BEHAVIOR_WORKBENCH.md#真实出牌试验当前实现与边界)。其他行为创作仍依该说明关联的专项设计推进。

### 角色头像与立绘（2026-10-05）

角色图的制作、基础／外观逐槽继承、`portraitResourceKey`／`standingResourceKey`、正式资源 usage 与发布投影统一见[角色图契约](Guides/CHARACTER_PRESENTATION_IMAGES.md)。该契约记录实际 tactical 整备角色库头像消费者与 Web 预览 fallback；当前战斗界面没有立绘展示位置，不能由配置存在推定全身立绘已显示。离线验证与六包 dry-run 不代替原生测试、正式六包 import-unity 或 Global Player Resources 产物验收，后者仍待唯一 Editor 操作方的回执。

### 表现 LOD 与五万植被场景（2026-10-05）

五万株样例为 `Assets/Scenes/MeadowWindSample50kScene.unity`。表现／几何LOD与Pose/VFX预算读[当前工程权威](Guides/PRESENTATION_POSE_VFX_LOD.md)，模块合同见[Rendering](Interfaces/Rendering.md)，布料保护见[CharacterCloth](Interfaces/CharacterCloth.md#9-显式角色组的表现-lod-暂停)，阴影操作见[定向阴影指南](Guides/DIRECTIONAL_TRIANGLE_SHADOWS.md)。历史样例回执见[Meadow50k](Tasks/Meadow50kSample.md)，不在此复制专项完成状态。

植被仅以独立 `CharacterTestVegetationData` 资产为数据权威，场景绑定保存 OwnerId、Data、Anchor、SurfaceRoot 与 Enabled。初始化直接创建资产并绑定；Level Editor 同一“场景植被”模块直接管理类型、Prefab、LOD、距离、阴影开关、资产选择与绑定启用。旧 Layout、旧数据兼容/实例导入/迁移入口及 renderer 自注册相机回调已删除；GPU 设备安全回退继续保留。操作见[场景植被指南](Guides/BASIC_SCENE_VEGETATION.md#在-charactertestscene-从零创建)，合同见[Rendering独立数据与会话](Interfaces/Rendering.md#植被独立数据与场景会话2026-10-05)，数据保护及专项验证范围见[植被任务回执](Tasks/VegetationDataSession.md#工程验证)。

### 本批资源与剩余门禁（2026-10-08）

以下状态来自父线程及唯一Editor操作方回执，本次文档同步未重新执行验证：

- Global Player Resources 原生 `BuildReport=Succeeded`，0 error、4 warning，24个artifact校验通过；它不等于所有Consumer、资源导入或Player运行门禁通过。
- 本批门禁18/18、Consumer20/20、strict E0零阻断，已替代此前Consumer14项与E0两项失败状态。当前包边界读[UPM当前状态](UPM_CURRENT_STATUS.md)，本回执不扩为全部Player／平台验证。
- 确定性修复已完成24文件本地主合，离线357/357、原生关键分组121/121（恢复网络100、真实历史耗尽1、快照/replay6、单卡恢复6、回执身份容量8）。save/hash v2明确拒绝旧不完整格式，CharacterTest snapshot40与wire协议不变；正式协议读[Runtime](Interfaces/Runtime.md)及[Network](Interfaces/Network.md)。
- StageFlow广组42/43，`ReleasingTacticalControlPreservesTheMenuOwnerAndResources` 控制释放合同冲突已由基线单例复现；正确规则（保留或取消待执行卡／预留费用）待用户选择，关键分组通过不关闭该项。过程曾发生4条 `BakeClipMissing` 和工具断连／超时，保留为历史过程证据；随后四包Combo02＋katana共118条映射以及11武器118条接触／actor BakedSocket已有通过回执，旧“已定位未修”状态已被修复回执替代。关键分组通过仍不关闭控制释放合同选择。
- 整场战斗StageRun保存／Continue、多机器／平台／Player构建与GC未由该确定性批次验收；既有Continue仍为战斗外恢复。独立Lab试验的现行范围读[工程权威](Guides/CHARACTER_TEST_RUNTIME_AI_LAB_BEHAVIOR_WORKBENCH.md#真实出牌试验当前实现与边界)，不在此复制实现状态。

第11项Docs整理已完成约定范围：两份Genesis剩余历史的有效内容归入现行工程合同，旧引用修复，B1旧截图缺链与矛盾陈述纠正；原图未找回且未伪造替代。本轮不再扩展历史清理。

本批现行实施与剩余边界如下；主合、原生验收、资产发布和后续候选分别判断：

| 工作 | 当前状态 |
|---|---|
| AI Lab真实出牌独立UI | [当前工程权威与验收边界](Guides/CHARACTER_TEST_RUNTIME_AI_LAB_BEHAVIOR_WORKBENCH.md#真实出牌试验当前实现与边界) |
| 规则交互卡／替身与体型 | 正式替身／体型机制尚未实现，仍按[主基石](../StorySource/玩法基石/行动卡与交互效果卡设计基石.md)推进。 |
| 普通粒子LOD | [Pose/VFX工程权威](Guides/PRESENTATION_POSE_VFX_LOD.md) |
| 武器拖尾入口 | [默认样式、HitTraceWindow与发布／回归边界](Guides/WEAPON_BLADE_TRAIL.md) |
| Consumer／strict E0 | 本批门禁18/18、Consumer20/20、strict E0零阻断，旧失败状态已被本次回执替代。 |
| 控制释放合同 | 基线单例复现合同冲突；保留或取消待执行卡／预留费用仍待用户选择，未自行改玩法规则。 |
| 角色物品基础 | [唯一设计计划](Plans/CharacterActionInventoryFoundationPlan.md)已独立复核、修3点并通过，Library已交；用户角色也是特殊物品的定义已明确。首步6文件47离线通过，仍为未主合候选；真实存档、后续学习与战斗不改。 |

战前准备generation空引用修复和右键取消原生通过。四包Combo02＋katana共118条映射、11武器118条接触／actor BakedSocket已有通过回执；旧 `CurrentIssue:321` 诊断与四包“已定位未修”状态不再作为当前阻塞。

C006（D413／668367838）注册rev1175／emit、Blender反馈、Default预览兼容及16661单角点法线修复完成；本次模型贴图发布Unity状态为completed。其后用户布料配置改变sourcePackageHash，属于另一次未验变更，不能并入该资产发布完成结论；新C006长杖仅为隔离制作任务，不算资产交付。

当前阴影中心由80×60／3072改为64×48／4096，外围2048、动态1024与GI不变；center-only bake/cache已保存。释放后new dirty保留未覆盖，不为文档同步保存或丢弃场景。当前所有权与Git统一放行由父线程协调。

### CharacterTest 单段战术行为卡（2026-09-06）

编辑 `StorySource/玩法基石/任务/任务03首卡源示例.json`，在 Unity EditMode 使用 `MxFramework → CharacterTest → Tactical Card → Compile Default Loadout` 读取当前有效摘要并编译默认验证装配。需要重新测量时明确使用 `Measure and Compile Default Loadout`。报告位于 `Library/Task03ExecutionEvidence/default-loadout-compilation.txt`。

源格式、共用 Host 接入和支持范围见[单段战术行为卡指南](Guides/CHARACTER_TEST_TACTICAL_CARD.md)。本入口不改变已发布行为组，交付等级为 Runtime Slice；编译报告与实际运行验收证据分开。

### Platform Core / Unity host facts (2026-07-20)

Platform Core 只提供可注入的宿主事实，不直接读取 Unity `Application.*`，也不执行保存、Runtime pause、场景或 UI policy。Unity 组合根显式创建 adapters 和 callback proxy；proxy 不自建 singleton 或隐藏 lifetime：

```csharp
var environment = new UnityPlatformEnvironmentProvider();
var locations = new UnityPlatformLocations();
var lifecycle = UnityApplicationLifecycleReporter.CreateForCurrentApplication(capacity: 16);
var permissions = new UnityPlatformPermissionService();

// The composition root owns this component and its lifetime.
UnityApplicationLifecycleProxy proxy = lifecycleOwner.AddComponent<UnityApplicationLifecycleProxy>();
proxy.Bind(lifecycle);

// Application safe phase: one sole drain owner.
while (lifecycle.TryDequeue(out ApplicationLifecycleChange change))
{
    if (change.Kind == ApplicationLifecycleChangeKind.StateResyncRequired
        && (change.SawSuspendInLostRange || change.SawQuittingInLostRange))
    {
        // Request the application-owned conservative policy; Platform itself does not save.
    }
}

// Permission host completion becomes visible only through this main-thread pump.
permissions.Pump();
```

Location 必须先按 kind 验证，不能假设 StreamingContent 一定是 filesystem path：

```csharp
PlatformLocationResult streaming = locations.Resolve(PlatformLocationId.StreamingContent);
if (streaming.Success && streaming.Location.Kind == PlatformLocationKind.FileSystemPath)
{
    // A filesystem consumer may continue only after checking its required access flags.
}
```

UTC metadata 在发布前通过 fail-closed helper 读取：

```csharp
if (PlatformUtcClock.TryReadUtc(wallClock, out DateTime utcNow, out PlatformError error))
{
    // Publish non-authoritative metadata only.
}
```

不要自动修复 Local/Unspecified wall clock，也不要把 wall clock、focus、suspend 或 location 写入 Runtime authority、Replay、hash、Gameplay Trigger 或 SaveState conflict order。当前 phase 已交付 Unity adapter、尚未迁移 consumer；完整 API、线程边界、分配分类及 overflow resync 语义见 `Docs/Interfaces/Platform.md`。

### Gameplay Trigger Runtime Phase 1 + 19.2A-H2 + 19.3A-D + 19.4A-B (2026-07-20)

19.3A 可在启动/构建期把 in-memory typed authoring 编译成 runtime catalog plan。先由可信组合根创建 retired ID、SourceBinding provider/replay/capacity contract 与 action delivery requirement registries；authoring document不能自报这些权限：

```csharp
GameplayTriggerCompileResult compiled = GameplayTriggerConfigCompiler.Compile(
    authoringDocument,
    GameplayTriggerCompilerOptions.Default,
    compilerEnvironment);
if (!compiled.Succeeded)
{
    // GetDiagnostic(i) 已按 numeric code/path/IDs canonical 排序；不要使用 partial artifact。
    return;
}

GameplayTriggerCatalogBootstrapResult bootstrap = GameplayTriggerCatalogBootstrap.TryCreateCatalog(
    compiled.CompiledCatalog,
    actualActionBindings, // actual external sinks 或 exact internal descriptors
    runtimeCapacityProfile);
if (!bootstrap.Succeeded) return;

var runtime = new GameplayTriggerRuntime(bootstrap.Catalog, bootstrap.RuntimeOptions.Value);
// 先把实际 outbox/target modules 注册到 Created host；若 catalog 无 Reliable action，outbox 必须为 null。
if (!GameplayTriggerCompositionVerifier.TryVerify(
        compiled.CompiledCatalog,
        host,
        runtime,
        reliableOutboxOrNull,
        out GameplayTriggerCompositionVerificationResult composition))
    return;

host.Initialize();
```

Compiler 对 ID-keyed set 做 canonicalize，但保留 route action 与 condition child 的语义顺序。compiled plan只投影definitions实际引用的action requirements；全局registry未使用行不改变hash或强迫注入sink。所有Signal replay ordering必须在authoring与trusted source contract间一致；non-Signal使用唯一no-replay sentinel。composition verification必须发生在`RuntimeHost.State == Created`，Reliable还要求实际FailFast Host、同runtime且registered/enabled的outbox、exact enabled transfer set、已注册target module及严格更晚的调度tuple。此入口不读取JSON/Unity object，不支持active-session hot reload；JSON importer与通用Unity action bridge仍属后续独立扩展。19.4A Playable与19.4B network composition都在外层复用compiled catalog，不改变compiler契约。

Unity Physics occupancy 先用同一 compiled plan 创建 immutable catalog，再由 exact composition factory 注入 Gameplay-owned bounded command buffer。factory 会注册 occupancy-only Exogenous source并seal registry；创建成功后必须把 returned flush module 与同一 Gameplay module 都注册到 Host。live session 使用 `LiveExternal`，replay session必须使用`ReplayDisabled`：

```csharp
GameplayTriggerUnityOccupancyCatalog.TryCreate(
    new[]
    {
        new GameplayTriggerUnityOccupancyDescriptor(
            sourceBindingId,
            providerId,
            maxSubjects: 16)
    },
    compiled.CompiledCatalog,
    out GameplayTriggerUnityOccupancyCatalog unityCatalog,
    out _);

var unityOptions = new GameplayTriggerUnityOccupancyOptions(
    maxSubjectBindings: 64,
    maxSubjectColliders: 128,
    maxActiveContactPairs: 256);
GameplayTriggerUnityOccupancyComposition.TryCreate(
    commandSourceId: triggerUnitySourceId,
    commandSources,
    gameplayModule,          // default Simulation/100 + hard-bounded buffer
    triggerCommandSystem,    // enabled default Command/900 in this exact pipeline
    triggerRuntime,
    compiled.CompiledCatalog,
    unityCatalog,
    GameplayTriggerUnitySubmissionMode.LiveExternal,
    in unityOptions,
    out GameplayTriggerUnityOccupancyAdapter unityAdapter,
    out GameplayTriggerUnityOccupancyFlushModule unityFlush,
    out _);

host.RegisterModule(unityFlush);       // PreSimulation/0
host.RegisterModule(gameplayModule);   // Simulation/100, unique command drain
```

场景中的 `GameplayTriggerUnityOccupancyZone` 只配置 frozen SourceBindingId 与 trigger Collider，并在组合根创建 adapter 后调用 `TryAttach(...)`。`GameplayTriggerUnitySubjectBinding` 显式配置该 logical subject 的全部 Colliders，Gameplay entity 创建后调用 `TryBind(...)`；generation变化使用`TryRebind(...)`。不要从 callback 直接执行 action，也不要把 Collider/GameObject/scene handle 写进 Trigger SaveState/hash。zone/subject Disable、Destroy 和 scene unload 会由组件与持久 module完成final-empty/unbind；不需要也不应手动伪造 Exit。

先用 `GameplayTriggerCatalog.TryCreate(...)` 在初始化期冻结 source、definition、binding、route 和 action sinks，再按固定容量创建 `GameplayTriggerRuntime`。每个 scheduled frame 为 source binding 提交且只提交一个 complete batch，最后调用 `Evaluate(frame)`：

```csharp
runtime.TrySubmitOccupancy(frame, sourceBindingId, subjects, out GameplayTriggerResult submit);
GameplayTriggerResult evaluated = submit.Success
    ? runtime.Evaluate(frame)
    : submit;

for (int i = 0; i < runtime.IntentCount; i++)
{
    GameplayTriggerActionIntent intent = runtime.GetIntent(i);
    // 交给注册过的 target-owned typed sink/bridge，不直接写其它模块状态。
}
```

19.3C1 的 Gameplay Buff Apply/Remove 使用 same-frame reserved bridge，不经过已经 drain 的 Runtime command buffer。先创建 positive fixed reservation capacity 的 Gameplay-owned request buffer，把 sink 注入 Trigger catalog，并把同一 presentation event source 注入 terminal result relay：

```csharp
var buffRequests = new GameplayBuffRequestBuffer(
    reservationCapacity: 64,
    legacyPrewarmCapacity: 16);
var applyBuffSink = new GameplayTriggerGameplayBuffActionSink(
    GameplayTriggerGameplayBuffActionKinds.ApplyComponentBuff,
    buffRequests);
var removeBuffSink = new GameplayTriggerGameplayBuffActionSink(
    GameplayTriggerGameplayBuffActionKinds.RemoveComponentBuff,
    buffRequests);

var triggerEvents = new GameplayTriggerRuntimeEventSource(capacity: 128);
GameplayTriggerCatalog.TryCreate(
    sources,
    definitions,
    bindings,
    new IGameplayTriggerActionSink[] { applyBuffSink, removeBuffSink },
    out GameplayTriggerCatalog triggerCatalog,
    out GameplayTriggerCatalogValidationResult triggerValidation);
var triggerRuntimeWithBuffs = new GameplayTriggerRuntime(
    triggerCatalog,
    triggerOptions,
    triggerEvents);
var buffResultRelay = new GameplayTriggerGameplayBuffResultRelay(triggerEvents);
var buffApplication = new GameplayBuffApplicationSystem(
    buffDefinitions,
    buffRequests,
    resultSink: buffResultRelay);
var triggerEventRouter = new GameplayTriggerEventRouterSystem(
    triggerEvents,
    triggerObservers);

pipeline.Add(new GameplayTriggerEvaluationSystem(triggerRuntimeWithBuffs)); // Simulation/1000
pipeline.Add(buffApplication);                                      // Resolution/50
pipeline.Add(triggerEventRouter);                                  // Diagnostics/1000
```

两个 action 的 target 必须是 domain-1 `GameplayEntityId`；`Payload0=BuffId`、`Payload1=SourceId`、`Payload2=0`。sink 生成的 request 固定 `CommandId=0` 与 empty TraceId。完整 activation transaction Prepare 成功后 request 才可见；Buff consumer 最终发布 Applied/Refreshed/Removed/RemoveNoOp 或 typed rejection，relay 以 `ActivationId + ActionIndex + ActionKindId` 关联 Trigger `ActionResult`。observer overflow 只影响 presentation。request/correlation/result 不进入 hash、SaveState 或 replay；checkpoint suffix 必须从 Gameplay + Trigger authority 重新生成，restore boundary 可对 transient Buff buffer 调用 `Clear()`。

19.3C2 的 Gameplay component command bridge 使用 `ReliableDeferred`，在下一帧把 Destroy、Attribute Set/Add 与 direct Ability Cast 投递到 hard-bounded Runtime command buffer。组合根必须为专用 `Derived` source 注册且只注册这四个 command，使用 `TrustedDerived` gate，并把 result relay 注入实际注册在 Gameplay pipeline 中的三个 consumer：

```csharp
RuntimeCommandBuffer gameplayCommands = RuntimeCommandBuffer.CreateBounded(
    validator,
    RuntimeFrame.Zero,
    pendingCapacity: 64);
var commandSources = new RuntimeCommandSourceRegistry(1);
commandSources.TryRegister(
    triggerGameplaySourceId,
    RuntimeCommandSourceKind.Derived,
    new[]
    {
        GameplayRuntimeCommandIds.DestroyComponentEntity,
        GameplayRuntimeCommandIds.SetComponentAttribute,
        GameplayRuntimeCommandIds.AddComponentAttribute,
        GameplayRuntimeCommandIds.CastComponentAbility
    },
    out _);
var gate = new RuntimeCommandSubmissionGate(
    commandSources,
    gameplayCommands,
    RuntimeCommandIngressKind.TrustedDerived);
var relay = new GameplayTriggerGameplayCommandResultRelay(
    triggerEvents,
    triggerGameplaySourceId);
var attributeCommands = new GameplayAttributeCommandSystem(
    GameplayAttributeCommandSystem.DefaultSystemId,
    priority: 40,
    resultSink: relay);
var setAttributeSink = new GameplayTriggerGameplayReliableCommandActionSink(
    GameplayTriggerGameplayCommandActionKinds.SetComponentAttribute);
var setAttributeTransfer = new GameplayTriggerGameplayReliableCommandTransfer(
    setAttributeSink.ActionKindId,
    triggerGameplaySourceId,
    host,
    gameplayModule,
    attributeCommands,
    gate);
```

Destroy consumer 必须是 priority 20 的 `GameplayComponentEntityCommandSystem`，Attribute consumer 必须是 priority 40 的 `GameplayAttributeCommandSystem`，Ability consumer 必须是 priority 50 的 `GameplayComponentAbilityCommandSystem`；transfer 会按具体实例、Host/module 注册状态和 source allowlist fail-closed。四种 action id 固定为 `1001004/1001006/1001007/1001008`，target 必须是 domain-1 entity；Destroy payload 全零，Attribute 使用 `Payload0=AttributeId`、`Payload1=int value/delta`，Ability 使用 `Payload0=AbilityId`。terminal result 只含 flat numeric facts；command sequence 可为零，reliable correlation sequence 必须来自 envelope `Data3>0`。command/result 是 derived transient，checkpoint payload、hash、SaveState 和 replay schema 均不增加字段；suffix playback 会重新生成一次。

19.3C3 的 Story bridge 只投递 `Story.RaiseTrigger=1003001`。bounded admission 和 Story module 必须共享 exact default validator 实例；source registry 必须把 fixed `GameplayBridge=1003102` 注册为 `Derived`，并且 allowlist 只有 `RaiseTrigger`：

```csharp
var director = new StoryDirector();
var storyValidator = new StoryRuntimeCommandValidator(director);
var storyCommands = RuntimeCommandBuffer.CreateBounded(
    storyValidator,
    RuntimeFrame.Zero,
    pendingCapacity: 64);
var storySources = new RuntimeCommandSourceRegistry(1);
storySources.TryRegister(
    StoryRuntimeCommandSources.GameplayBridge,
    RuntimeCommandSourceKind.Derived,
    new[] { StoryRuntimeCommandIds.RaiseTrigger },
    out _);
var storyGate = new RuntimeCommandSubmissionGate(
    storySources,
    storyCommands,
    RuntimeCommandIngressKind.TrustedDerived);
var storyRelay = new GameplayTriggerStoryCommandResultRelay(triggerEvents);
var storyModule = new StoryRuntimeModule(
    director,
    storyCommands,
    events: null,
    validator: storyValidator,
    commandResultSink: storyRelay);
var storySink = new GameplayTriggerStoryReliableCommandActionSink(
    GameplayTriggerStoryCommandActionKinds.RaiseTrigger);
var storyTransfer = new GameplayTriggerStoryReliableCommandTransfer(
    storySink.ActionKindId,
    StoryRuntimeCommandSources.GameplayBridge,
    host,
    storyModule,
    storyGate);
```

action target 固定为 domain-2 Trigger source binding，`Payload0=positive triggerId`、`Payload1/2=int param0/param1`。Story module只对带固定 Derived envelope 的 RaiseTrigger发布一次 flat numeric terminal result；relay只接受 Success、TriggerNotFound 与 DirectorGuardExceeded。command/result/correlation不进入hash、SaveState或replay；checkpoint必须在完整成功Tick后的quiescent boundary capture，restore从Trigger-owned outbox在下一帧恰好重新生成一次。custom validator registry、未启动Host、未注册module、full/disabled target均fail-closed或保留outbox。通用Unity action不属于此Story bridge入口；19.4A Playable和19.4B snapshot suffix均复用同一outbox恢复语义。

#### Gameplay Trigger 19.4A Playable

打开 `Assets/Scenes/GameplayTriggerPlayable.unity` 并进入 Play；需要重建场景时使用 Unity 菜单 `MxFramework/Gameplay Triggers/Create Playable Scene`。场景把可观察链路固定为 `CombatPhysicsWorld occupancy -> Gameplay Trigger -> Gameplay Buff / Story -> Runtime SaveState / replay-compatible authority hash`：通过 checkpoint 后启用 door Trigger，进入 door 后以 reliable Story `RaiseTrigger` 打开门，进入 trap 后以 same-frame Gameplay Buff bridge累计 activation 并刷新 slow。Unity primitives、Camera 和 UI Toolkit HUD 只做 view/validation surface，不成为 occupancy 或玩法 authority。

键盘与 HUD controls：

- `WASD` / 方向键，或按住 HUD 的 `LEFT / RIGHT / UP / DOWN`：移动；HUD 按钮单击也会提交一次方向 pulse，pointer release/capture-out 会停止 held movement。
- `E` 或 `CAPTURE`：在 checkpoint 已获得后捕获最近一次完整、成功、quiescent 的 end-of-Tick authority。
- `F6` 或 `RESTORE`：把内存 slot 恢复到 fresh candidate composition，成功后原子替换 live composition，并可继续移动、触发 door/trap 和比较 authority hash。
- `F4` 或 `RESET`：发布全新的默认 composition，并清空内存 slot。

HUD 同时显示 Frame、位置、Checkpoint、Door、Trap activation/slow、最新 occurrence/action/result、authority hash 和 capture 状态。`CAPTURE / RESTORE` 使用的是当前 `GameplayTriggerPlayableSession` 持有的单个 `RuntimeSaveState` 内存 slot；它不写磁盘、不经过 profile/slot store，也不代表 durable persistence。可重复输入 trace 与 capture 后 suffix 的 canonical hash 相等用于验证本地 replay/restore 组合，但 Playable 没有 replay timeline 或跨进程 replay/network UI。19.4B由独立noEngine/network harness交付，不给该场景增加联机入口。

#### Gameplay Trigger 19.4B Network Authority

19.4B是protocol-2 opt-in组合，不替换legacy protocol-1 session。初始化时先冻结`GameplayTriggerNetworkProfile`，为每个允许的client producer登记positive SourceId与Signal/Occupancy/Scalar mask，并把同一外部canonical network profile hash交给双方`NetworkSessionRunner.ConfigureBoundedProtocol(...)`。`networkProfileHash`必须由组合根按项目自己的canonical network compatibility contract生成。server ingress必须使用connected-peer directory、Gameplay ownership provider和现有hard-bounded External gate：

```csharp
var profile = new GameplayTriggerNetworkProfile(
    catalog.DefinitionCatalogHash,
    catalog.BindingCatalogHash,
    catalog.GameplayTriggerCatalogHash,
    options,
    networkProfileHash,
    NetworkHashComparability.FullComparable,
    new[]
    {
        new GameplayTriggerNetworkSourcePolicy(
            clientSourceId,
            GameplayTriggerNetworkObservationMask.Signal)
    });

var authorityIngress = new GameplayTriggerNetworkAuthorityIngress(
    profile,
    connectedPeers,
    ownership,
    externalSubmissionGate);
serverRunner.ConfigureBoundedProtocol(
    sessionHandle,
    serverPeerHandle,
    profile.NetworkProfileHash,
    authorityIngress);
```

bounded transport接收还必须在packet bytes之外独立返回`NetworkBoundedAuthenticatedMetadata(session, remote, local, direction)`。runner只在Connected状态下接收，并在调用ingress前核对configured session/local、metadata、header sender/receiver和kind-direction-role；CRC32不认证sender。authority/snapshot ingress只使用authenticated context做connected peer、ownership和server身份判断，不能信任包头sender来覆盖transport身份。

FullComparable snapshot必须为`GameplayComponentWorld`里每个已注册CLR component store显式提供exact one `IGameplayComponentNetworkStateCodec`。`ComponentType`只用于本地组合校验，wire身份使用稳定numeric `TypeId + Schema`；codec capture/prepare/discard必须NoAlloc，prepare成功后的Commit必须no-fail。若world没有component store，可使用不带codec数组的兼容构造：

```csharp
var stateBundle = new GameplayTriggerNetworkStateBundle(
    profile,
    gameplayWorld,
    triggerRuntime,
    componentCodecs,
    maxGameplayEntities: 512);
var snapshotIngress = new GameplayTriggerNetworkSnapshotIngress(
    profile,
    stateBundle,
    serverPeerHandle);
var rollback = new GameplayTriggerNetworkRollbackResync(
    predictionRollbackCoordinator,
    stateBundle,
    historyCapacity: 128);
```

snapshot固定先完整prepare Gameplay component/entity candidate，再prepare Trigger entity references，全部成功后按order 100/200发布；end-of-F恢复后从F+1继续。rollback history在`Seal()`时预分配完整frame record scratch：一次record只有在全部participant capture成功后才覆盖ring slot并推进history；restore先prepare全部participant，再按`RestoreOrder -> TypeId`执行no-fail commit（Gameplay 100 -> Trigger 200），任一prepare失败都会对全部participant调用幂等discard并保持全部live state不变。已提交G1/G2 queue会在首个due frame恰好执行一次，Combat/Unity observations、bridge commands/results和Router observers必须重建而不是写进network input。不模拟完整Trigger state的client使用`PartialNonComparable`并拒绝full-world hash比较。hash mismatch、missing history、catalog/schema/corrupt/unauthorized snapshot或restore failure进入typed resync；duplicate byte-identical snapshot幂等。`Seal()`后的rollback record/restore为NoAlloc。

这里的authentication只表示transport/session独立提供并由runner核对的connected session/remote/local/direction事实，以及Gameplay ownership授权；当前没有credential storage、transport encryption、account、matchmaking、Relay、lobby UI、durable persistence或CharacterTest gameplay protocol迁移。packet record不携带TraceId；不要把diagnostic string、Unity object、Router observer或derived result写入wire/snapshot/hash。

成功创建的 catalog 会缓存 `DefinitionCatalogHash`、`BindingCatalogHash` 和 `GameplayTriggerCatalogHash`。需要把 Trigger authority 纳入现有 Runtime hash 时，在初始化期注册 contributor：

```csharp
var triggerHashContributor = new GameplayTriggerRuntimeHashContributor(runtime);
var hashCombiner = new RuntimeHashCombiner(new IRuntimeHashContributor[]
{
    triggerHashContributor,
    // 其他 authority contributor；combiner 会按 ContributorId canonical 排序。
});

// 仅在完整 Evaluate/Tick 成功结束的 quiescent boundary 读取。
long endOfFrameHash = hashCombiner.ComputeHash(frame);
```

Contributor 构造时按固定 relation/route-state capacity 分配 canonical-order scratch；`ContributorId` 和初始化后的 `Contribute` 为 `NoAlloc`。19.2F2 提供 Trigger participant；Gameplay world 必须先恢复，Trigger 才能验证其中的 entity references：

```csharp
var triggerParticipant = new GameplayTriggerRuntimeSaveStateParticipant(
    runtime,
    restoredEntityValidator);

var saveStates = new RuntimeSaveStateRegistry();
saveStates.Register("gameplay.world", 100, gameplayWorldProvider, gameplayWorldRestorer);
saveStates.Register(
    GameplayTriggerRuntimeSaveStateParticipant.ParticipantId,
    GameplayTriggerRuntimeSaveStateParticipant.DefaultSaveStateOrder,
    triggerParticipant,
    triggerParticipant);

var saveStateCoordinator = new RuntimeSaveStateCoordinator(saveStates);
var quiescence = new RuntimeCheckpointQuiescenceRegistry();
quiescence.Register(
    GameplayTriggerRuntimeSaveStateParticipant.ParticipantId,
    GameplayTriggerRuntimeSaveStateParticipant.DefaultQuiescenceOrder,
    triggerParticipant);

var checkpointBarrier = new RuntimeCheckpointBarrier(host, saveStateCoordinator, quiescence);
RuntimeSaveStateCoordinatorResult<RuntimeSaveState> checkpoint = checkpointBarrier.CaptureCheckpoint();
```

19.2G1 的 Trigger-local enable/reset/Signal 不注册 mutable sink；在 catalog 构建时提供 immutable descriptor，并把 action 的 explicit target slot 指到已登记的 Trigger source binding。若同一 catalog 还包含 `ReliableDeferred` action，使用十二参数 options 同时提供 internal queue 与 reliable outbox capacity，并把 deferred system 与既有 command/evaluation system 一起注册：

```csharp
var target = GameplayTriggerTargetRef.FromTriggerSourceBinding(targetSourceBindingId);
var originBinding = new GameplayTriggerBinding(
    originBindingId,
    originDefinitionId,
    originSourceBindingId,
    owner,
    explicitTargets: new[] { new GameplayTriggerBindingTarget(1, target) });

var internalActions = new[]
{
    new GameplayTriggerInternalDeferredActionDescriptor(
        signalActionKindId,
        GameplayTriggerInternalDeferredOperationKind.Signal,
        signalId: 4001)
};

GameplayTriggerCatalog.TryCreate(
    sources,
    definitions,
    new[] { originBinding, targetBinding },
    externalSinks,
    internalActions,
    out GameplayTriggerCatalog catalog,
    out GameplayTriggerCatalogValidationResult validation);

var options = new GameplayTriggerRuntimeOptions(
    maxInputBatches, maxInputItems, maxRelations, maxOccurrences, maxCandidates,
    maxRouteStates, maxActivations, maxIntents, maxPreparedActions,
    maxConditionStackDepth, maxDeferredRecords: 64,
    maxReliableOutboxRecords: 64);
var eventSource = new GameplayTriggerRuntimeEventSource(capacity: 128);
var triggerRuntime = new GameplayTriggerRuntime(catalog, options, eventSource);

var hudEvents = new GameplayTriggerRuntimeEventObserverBuffer(capacity: 64);
var debugEvents = new GameplayTriggerRuntimeEventObserverBuffer(capacity: 256);
var eventRouter = new GameplayTriggerEventRouterSystem(
    eventSource,
    new[]
    {
        new GameplayTriggerRuntimeEventObserverRegistration("debug", debugEvents),
        new GameplayTriggerRuntimeEventObserverRegistration("hud", hudEvents)
    });

pipeline.Add(new GameplayTriggerDeferredSystem(triggerRuntime)); // PreCommand, priority 800
pipeline.Add(new GameplayTriggerCommandSystem(triggerRuntime));  // Command, priority 900
pipeline.Add(new GameplayTriggerEvaluationSystem(triggerRuntime));
pipeline.Add(eventRouter); // Diagnostics, priority 1000；唯一 presentation source drain owner
```

Runtime 必须先绑定 `eventSource`，Router 才能绑定同一 source；observer registration 会在构造时复制、校验并按 ordinal id 冻结。每个 observer 只消费自己的 bounded buffer，不能直接 drain source。authority restore 不会隐式清除 presentation pending/drop/high-water/sequence；只有确定没有并发 Tick/reader 的 bootstrap/rebuild 排他边界，才显式重置：

```csharp
// authority restore 完成后，仍处在 exclusive bootstrap/rebuild boundary。
eventRouter.ResetPresentationState();
```

该重置保留 Runtime/Router/observer ownership binding 和 enabled 配置。H2 playback 会在 Ready 前自动执行同样的 presentation reset；snapshot 仍只注入可信 exogenous observation，不从 observer 回写 authority。

Internal action 在 frame F 成功 Commit 后写入 Trigger-owned queue，最早 F+1 执行；Signal 在 PreCommand 只 stage，由既有 Command system 与 external Signal/lifecycle command 按 total order merge/finalize。Reliable action 同样只在 Commit 后写入另一条 Trigger-owned outbox，`OriginalDueFrame=F+1`。组合根为每个 reliable action kind 注册 target transfer，并把 outbox module 放在目标 module 之前：

```csharp
var reliableOutbox = new GameplayTriggerReliableOutboxModule(
    triggerRuntime,
    reliableTargetTransfers);

host.RegisterModule(reliableOutbox); // default PreSimulation / priority 0
host.RegisterModule(targetModule);   // scheduling tuple must be strictly later
```

authority host 必须使用 `RuntimeHostErrorPolicy.FailFast`。只有 transfer 返回 `Accepted` 才从 outbox 移除；missing/disabled/capacity/transient/unavailable 都保留 record。具体 adapter 若生成 Runtime command，必须走 `RuntimeCommandSubmissionGate(TrustedDerived)` 并使用当前 retry frame G；derived command 会被 exogenous replay recorder 排除。future/overdue committed G1/G2 queues 都是可 hash/save 的 Ready authority；未提交 internal/reliable reservation 返回 pending detail 5，tagged Signal window 返回 detail 6，transfer callback in-flight 返回 detail 7。

`CaptureCheckpoint()` 只接受最近一次完整成功且已结束的 Tick；成功 F 后失败 F+1 不会回退 F。所有 checker 都返回 ready 后才会调用 SaveState provider，成功时根 `RuntimeSaveState.Frame` 统一为 F。合法 pending 使用正数 participant detail；default/non-positive result 或 checker exception fail-closed。旧 `RuntimeSaveStateCoordinator.CaptureSaveState()` 保持兼容，但没有这些 checkpoint 保证。checker 是纯只读 `NoAlloc`，registry 初始化和 checkpoint capture 为低频分配路径。

Trigger 的 `CaptureSaveState()` 会在任意真实 pending batch/item、未提交 internal/reliable reservation、tagged Signal finalization 或 reliable transfer callback 存在时返回 `PendingWork`，但直接 capture 仍没有 RuntimeHost/frame authority；正式 checkpoint 必须走 barrier。恢复只允许在排他、单线程 bootstrap 阶段执行，不能与 Tick 或并发 reader 同时发生；单 participant validate-plan-commit 原子性不等于 coordinator 跨 participant rollback。建议 Gameplay world order 100、Trigger save/quiescence order 200；这些只是建议值，复杂组合根应分配无冲突 order。

19.2H2 使用组合根提供的 `IGameplayTriggerReplaySessionFactory` 创建一套独占 replay composition。factory 必须遵守正常运行时相同的 composition rules、使用同类 Host/Gameplay module/Trigger systems/Router/source registry/save coordinator，并在每次 `Create` 时创建 fresh composition，按需注册同一 Trigger runtime 的 reliable outbox。禁止返回 live/current composition 中的任何实例，也禁止复用此前已移交给 replay session、随后已由它 Dispose 的实例；不要在 factory 中 Tick，也不要返回相同 ID 的替身。header 的 config identity 由 combined Trigger catalog hash 创建：

```csharp
var replayOptions = new GameplayTriggerReplayBootstrapOptions(
    expectedFrameworkVersion,
    expectedResourceCatalogHash,
    catalog.GameplayTriggerCatalogHash,
    maxCommandsPerFrame: 64,
    maxSignalCommandItems: 64,
    maxOccupancyCommandItems: 64,
    maxScalarCommandItems: 64);

RuntimeReplayHeader header = GameplayTriggerReplayHeaderContract.Create(
    expectedFrameworkVersion,
    expectedResourceCatalogHash,
    catalog.GameplayTriggerCatalogHash,
    startFrame);

using var driver = new GameplayTriggerReplayFrameDriver(
    replaySessionFactory,
    replayOptions,
    GameplayTriggerReplayBootstrapMode.Fresh);
RuntimeReplayPlaybackResult result =
    new RuntimeReplayPlaybackRunner().Play(snapshot, driver);
```

snapshot frame 必须从 header start 连续排列，包含每个 frame 的可信 exogenous Signal/occupancy/scalar fixed records 与 expected Trigger authority hash；空帧也要保留。每 frame commands 必须使用严格 Runtime drain order、唯一 sequence、完整 batch ItemIndex，并满足 command/typed/runtime capacity。raw lifecycle、legacy envelope、Derived provenance、unknown source/command、重复 source batch、malformed/incomplete payload 都会在任何 submit/Tick 前使 session fault/dispose。generic runner 的 `HashMismatch` 保留 failure frame、expected/actual hash 与 commands；typed driver exception 保留 frame、command index 或 checkpoint error。

从 end-of-F checkpoint 继续时，header start 必须严格为 F+1：

```csharp
using var suffixDriver = new GameplayTriggerReplayFrameDriver(
    replaySessionFactory,
    replayOptions,
    GameplayTriggerReplayBootstrapMode.CheckpointSuffix,
    endOfFrameCheckpoint);
RuntimeReplayPlaybackResult suffixResult =
    new RuntimeReplayPlaybackRunner().Play(suffixSnapshot, suffixDriver);
```

suffix bootstrap 固定执行 `Initialize -> Start -> atomic restore -> presentation reset -> Ready`。checkpoint 中 committed G1/G2 queue 会在到期帧重新执行；occurrence、activation、ActionResult/ActionDispatched、reliable transfer 和 TrustedDerived target command 都由 runtime 重建，不能重复写入 snapshot。observer 容量/overflow 不改变 replay hash。`long.MaxValue-1` 是最后可 Tick frame，`long.MaxValue` record 永不执行。

Occupancy 和 unordered Signal 的输入顺序不影响结果；scalar duplicate、incomplete batch、错误 source kind 和容量不足会结构化拒绝。Action sink 必须实现可消费 reservation；但 `ReliableDeferred` 的 legacy sink callbacks不会被调用，reservation/commit由Trigger-owned outbox接管。catalog创建时会冻结sink的`ActionKindId + DeliveryContract`；创建后修改live getter不会改变已注册kind/contract。19.3B Combat sensor、19.3C1-C3 Gameplay Buff、Gameplay component command、Story RaiseTrigger bridges、19.3D Unity Physics occupancy adapter、19.4A Playable与19.4B bounded network/scale均已交付。不要把本地playback driver、Playable内存slot或generic transfer seam描述为durable persistence、credential authentication或transport encryption。完整API、payload/hash字段边界和分配分类见`Docs/Interfaces/Gameplay.Triggers.md`。

### Buff Framework v2 Quick Start (2026-06-12)

Old `BuffPipeline`, `BasicBuffConfig`, `ConfigBuffFactory`, WGame `BuffType`, and external Buff editor runtime data are retired. Use the component-native Buff v2 chain:

```csharp
var definitions = new GameplayBuffDefinitionRegistry(new GameplayBuffDefinition(
    buffId: 601008,
    stableId: "buff.character_test.guard_aura.defense",
    defaultDurationFrames: 2,
    maxStackCount: 1,
    stackPolicy: GameplayBuffStackPolicy.RefreshDuration,
    effects: new[]
    {
        GameplayBuffEffectDefinition.AttributeModifier(1, CharacterTestCharacterAttributeIds.Defense, 5)
    }));

var requests = new GameplayBuffRequestBuffer();
pipeline.Add(new GameplayBuffCommandSystem(definitions, requests));
pipeline.Add(new GameplayComponentBuffCleanupSystem(priority: 42));
pipeline.Add(new GameplayBuffApplicationSystem(definitions, requests, priority: 50));
pipeline.Add(new GameplayBuffEffectProjectionSystem(definitions, priority: 52));

commandBuffer.Enqueue(GameplayRuntimeCommandFactory.ApplyComponentBuff(
    frame,
    targetEntityId,
    buffId: 601008,
    sourceId: 501008));
```

Combat auras should enqueue `GameplayBuffRequestBuffer` requests through `CombatGameplayAuraBuffSystem`; they should not directly mutate the Buff store. See `Docs/Interfaces/Buffs.md` for the authoritative v2 API.

### CharacterTest Abilities Full-Screen Panel (2026-06-12)

CharacterTest ability testing uses the Composer-owned FairyGUI `MxCharacterTestAbilityUi/AbilityBindingPanel` as the player-facing full-screen binding UI. The source component is authored at `1920x1080`; resolution adaptation is declared by its Screen Blueprint and materialized as FairyGUI relations on full-screen bands, the stretchable ability list, and right/bottom anchored action controls. Runtime C# only sizes the root component to the active FairyGUI layer/stage so those relations can resolve; it must not manually reposition child controls for this panel.

The panel is opened from the pause menu `Abilities` button, edits the local 1-9 hotbar, and saves to `Application.persistentDataPath/MxFramework/CharacterTest/ability_hotbar.json`. The 1-9 keys remain CharacterTest gameplay-context adapters that submit `CastAbilitySlot`; they are not added to `DefaultInputBindingCatalog`, `.inputactions`, or rebinding overrides. `Area Confinement`, `Beam Lance`, `Channel Beam`, `Laser Lance`, `Chain Lightning`, `Poison Bottle`, `Stun Bolt`, `Meteor Barrage`, `Sword Wave`, `Dash Strike`, `Hook Chain`, `Arc Mine`, and `Rune Turret` are available as bindable candidates but do not occupy default 1-9 slots. For current CharacterTest skill implementation patterns and module boundaries, use `Docs/Guides/CHARACTER_TEST_SKILL_IMPLEMENTATION_PATTERNS.md`.

### CharacterTest Player UI Surface Policy (2026-07-08)

CharacterTest player-facing FairyGUI follows a single-session surface policy. HUD, floating combat text, and Player Guidance are `LightOverlay` presentation surfaces and do not take modal input. Story dialog is a narrative modal and is not a conflict-close target for player panels. MainMenu, NetworkPanel, SettingsPanel, AbilityBindingPanel, Runtime AI Planner loadout, ArenaSetupPanel, and ArenaBehaviorGroupPanel are large player surfaces; opening one of them closes any conflicting large player surface after the requested surface has opened successfully.

Main menu child flow is explicit. Opening Settings or Abilities from MainMenu first opens the target panel, then closes MainMenu. Closing Settings or Abilities returns to MainMenu only when that panel was actually entered from an already-open MainMenu; tests and direct command-sink opens do not force a menu return. Arena behavior group editing is treated as a transactional panel for future inventory/stash/trade-style sessions: it can represent a grouped UI session, but the session as a whole still excludes MainMenu, Settings, Network, Runtime AI Planner loadout, and Arena setup.

While any large player surface is open, CharacterTest uses exclusive `InputContext.UI`, released cursor scope, and the existing FairyGUI modal command gate. Gameplay, Debug, and camera input should not read through or restore capture until the last player modal closes. `StoryDialog` is suppressed during the player surface session and re-evaluated on later UI refresh instead of being destroyed. Floating combat text, combat HUD, and Guidance presentation can be cleared or paused by policy during large panels; they resume after gameplay owns the screen again. MainMenu, SettingsPanel, Combat HUD, and Guidance management are formal for the CharacterTest slice under this internal policy; this is not yet a project-wide Surface Policy API.

### MxFramework VFX Showcase (2026-06-21)

Use `Assets/Scenes/MxVfxShowcase.unity` to browse and replay existing VFX without entering `CharacterTestScene`. Regenerate it from `MxFramework / VFX / Generate VFX Showcase Scene`. The scene builds its list from `TempImportedResourceCatalog` `domain.vfx` entries and the generated `VfxDefinitionRegistry`: formal definitions play through `VfxRuntimeModule` + `UnityVfxPresenter`, while catalog-only prefabs such as status auras or flight prefabs are instantiated through `ResourceManager` + `MemoryResourceProvider`.

The UI Toolkit panel supports search, grouped scrolling, Play/Replay/Stop, Loop, Auto Advance, a length slider for laser/beam/chain-arc path prefabs, a target distance slider for target/impact previews, and a details panel showing definition id, resource key, lifetime, asset path, labels, entry kind, and current parameters. See `Docs/Demo/MX_VFX_SHOWCASE.md` for the manual checklist and focused tests.

### CharacterTest Skill Showcase (2026-06-21)

Use `Assets/Scenes/CharacterTestSkillShowcase.unity` to preview real CharacterTest skill behavior without expanding the pure VFX Showcase. Regenerate it from `MxFramework / CharacterTest / Generate Skill Showcase Scene`. The scene starts a fresh CharacterTest runtime with a caster, hostile targets, an optional friendly target, real character views, existing CharacterTest VFX/resource bindings, and a UI Toolkit stage.

The Skill Showcase list comes from the CharacterTest ability catalog and puts recent skills first: Beam Lance, Channel Beam, Laser Lance, Chain Lightning, Poison Bottle, Stun Bolt, Meteor Barrage, Sword Wave, Dash Strike, Hook Chain, Arc Mine, and Rune Turret. Replay assigns the selected ability to a preview-only runtime slot and submits the existing `CastAbilitySlot` command path, so Ability cooldown, CharacterAction, Combat.Projectiles / HitResolve, Buffs, VFX, and telemetry all execute normally without writing the player's saved hotbar. See `Docs/Demo/CHARACTER_TEST_SKILL_SHOWCASE.md` for the manual checklist and focused tests, and `Docs/Guides/CHARACTER_TEST_SKILL_IMPLEMENTATION_PATTERNS.md` for the implementation pattern map.

### CharacterTest NetworkPanel Ready Room (2026-06-10)

CharacterTest player-facing LAN testing now starts in the Composer-owned full-screen `MxCharacterTestNetworkUi/NetworkPanel` and stays there after Host or Join. The default mode is LAN Room. Players first verify connectivity in the panel: each connected member clicks `Ready`; after all required connected members are ready, only Host can click `Enter Game`. Host `Enter Game` starts room gameplay, then Host and Clients close NetworkPanel/MainMenu automatically after their local gameplay handshake, owned entity assignment, and first full snapshot are all verified.

NetworkPanel exposes an `Auto Probe` trigger and a manual `Run Sync Probe` diagnostic. Enable `Auto Probe` before pressing `Enter Game` to run the 15 second / 900 frame synchronization probe automatically after this local instance has entered gameplay; Host and Client keep independent local triggers and no diagnostic broadcast protocol is added. The probe injects a fixed `MoveStopLoop` `SetControlIntent` pattern through `GameSlice.EnqueueRuntimeCommand()`: forward, stop, backward, stop, with jump/attack pulses. It reports whether the current issue is likely auth-command late/drop/no echo, missing stop intent, full-snapshot storm, reliable queue backlog, frame drift, or abnormal snapshot bytes. It is a diagnostic action and will briefly move/act with the owned character. The final report is written to Unity Console, remains in NetworkPanel `syncProbeSummary` when the panel is reopened, and writes per-frame JSONL under `Application.persistentDataPath/MxFramework/CharacterTest/SyncProbe/`.

When the active runner is authoritative Host/server, Sync Probe also starts `CharacterTestReplayRecorder`. It records only server-accepted scheduled authoritative commands and per-frame CharacterTest result hashes. After the probe finishes, `CharacterTestReplayFrameDriver` replays the capture in a clean headless `GameSlice`; a passing replay means the server command stream and deterministic rules reproduce locally, while a replay failure points to missing stop command data or a rules/hash determinism mismatch. NetworkPanel shows this as `Replay Check: ...` beside the probe summary; Client instances without authoritative replay show `Replay Check: host only`. If replay passes but another client still sees stale movement, the remaining suspect is the client receive/enqueue/apply path.

Direct LAN remains available as a quick connection diagnostic mode and does not add a room ready protocol. In Direct LAN, `Enter Game` is enabled only after the gameplay client is connected, an owned entity is assigned, and a full snapshot has been received. While a network session is active, `Back` is disabled; use `Disconnect` to leave the active lobby/session.

CharacterTest Host/Join gameplay synchronization is deterministic-command first. The initial reliable full snapshot is only a bootstrap step for owned entity assignment and initial state; after gameplay entry, routine digest/full snapshots and snapshot-request resync are disabled by the default `NetworkStateSyncPolicy.BootstrapOnly`. Remote movement and action visibility should come from server-authoritative command broadcast and fixed-frame command buffer execution. If Sync Probe sees full snapshot, digest snapshot, or snapshot request counters grow after entry, it reports `STATE_SYNC_ACTIVE`.

CharacterTest network snapshot schema v1 uses named integer authority units for transform, input, motion scalars, and physics profile/runtime modifiers: millimeters for position/distance/velocity, axis milli-units for input, milli-degrees for yaw, scale milli-units for ratios, and grams for mass. Float / `Vector2` APIs remain as Unity input and presentation adapters, but snapshot JSON and CharacterTest state hashes use the integer fields as the synchronization source.

Unity Multiplayer Play Mode local smoke path: open `Assets/Scenes/CharacterTestScene.unity`, start Main Editor Player plus at least one Virtual Player, use Main Editor Player as `LAN Room -> Host`, use Virtual Player as `LAN Room -> Join` with address `127.0.0.1`, optionally enable `Auto Probe` on each instance that should self-test, click `Ready` on both, then click `Enter Game` on Host. Standalone double-instance testing uses the same panel flow.

Automated MPPM smoke path: select/start an MPPM Scenario with Main Editor Player plus at least one Virtual Player, then run PlayMode test `MxFramework.Tests.Game.CharacterTest.CharacterTestNetworkAutomationTests.RoomHostAndOneVirtualPlayer_AutoSyncProbeAndExit` through Unity Test Framework or Unity MCP. The test writes a shared automation request, enters Play, lets Main Editor Host and Virtual Player Join/Ready automatically, has Host enter the room, runs the 15 second `MoveStopLoop` Sync Probe on both players, validates the Host authoritative replay capture when available, waits for both JSON reports under `Application.persistentDataPath/CharacterTest/NetworkAutomation`, then exits PlayMode. If the MPPM scenario is missing, the test fails with a message asking for Main + 1 Virtual Player.

### CharacterTest Windows Package Test (2026-06-21)

当前工程编辑器版本为 `6000.6.3f1`；多台开发机使用相同补丁版本，分别安装对应操作系统的编辑器与构建模块。Unity 6.6 的 Windows 构建显式选择 Managed Code Variant：Release 使用 `Release`，Development 使用 `Checked`，以保留开发构建的安全检查和性能分析能力。临时构建结束后恢复此前的托管诊断配置，构建配置回执记录实际 `managedCodeVariant`。这项设置独立于 C++ 编译优化级别。

Windows 本机打包测试入口是 `Tools/Harness/build_character_test_player.ps1`。默认流程会在 Unity BatchMode 中运行 `MxFramework.Game.CharacterTest.Editor.CharacterTestPackageTestCommand.RunDefaultPackageTest`：先构建 Global Player Resource Catalog / AssetBundle，再显式用 `Assets/Scenes/CharacterTestScene.unity` 构建 `StandaloneWindows64` x64 **IL2CPP Release** Player，随后启动玩法验收和 warm-run 并收集报告。`-Configuration Development` 切换至 IL2CPP 开发验收版，后端不会回退 Mono。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File Tools\Harness\build_character_test_player.ps1
```

脚本默认从 `ProjectSettings/ProjectVersion.txt` 推导 Unity Hub 或 `Program Files/Unity <版本>/` 安装路径；也可以用 `-UnityExe` 或环境变量 `UNITY_EXE` 指定 Unity。脚本以 `-buildTarget win64` 启动 BatchMode，等待 Unity 进程结束并读取实际退出码；Editor 再核对 active target 为 `StandaloneWindows64`，并在资源写入前检查 IL2CPP 模块、Visual Studio C++ x64 工具与 Windows SDK。默认每次在系统临时目录的 `WGF-Pkg-<随机码>/` 生成独立输出，可用 `-OutputRoot` 覆盖。报告须放在项目 `Temp/` 外，否则 Unity BatchMode 退出可能清理报告。默认路径保持简短；若自定义输出路径导致 StreamingAssets 的完整文件路径达到 260 字符，构建会明确拒绝，避免 Windows IL2CPP 运行时找不到实际存在的文件。关键文件包括 `unity_batch.log`、`CharacterTestPackageTestSummary.txt`、`CharacterTestPackageInputReceipt.txt`、`CharacterTestPerformancePlayerBuildRunReport.txt`、相邻的 `.configuration.json` 以及 `Reports/` 下的 Player 报告。

Unity 菜单 `MxFramework → Build → Windows64` 提供 `Apply IL2CPP Release Defaults`、`Build Release And Verify` 和 `Build Development And Verify`。Release 使用 C++ Release/OptimizeSpeed、Low stripping、LZ4HC，Development 使用 Release/OptimizeSpeed、Low stripping、LZ4；均使用 D3D11、增量 GC，关闭 Script Debugging/自动连接 Profiler/Deep Profiling/等待调试器。独立 Warm Run 固定使用 Development。Editor 菜单调用 Unity 同步构建，会在构建期间占用主编辑器；较长的发布构建应在已关闭对应 Editor 的项目或独立验证副本中运行 BatchMode 脚本。不能仅凭外部编译器仍有 CPU 活动就认定主编辑器可交互。调试符号与 Unity 的 DoNotShip 产物归档到输出目录的 `Symbols/<配置>-<标识>/`，完整清单和 SHA-256 位于构建配置回执，Player 目录只保留交付文件；网页分别显示 Player 体积和归档符号体积。Development 保留用于运行时源码行号堆栈的 `il2cpp.usym`，它计入 Player 数据；Release 只保留方法名堆栈。详细参数与实际验收状态见 `Docs/PROJECT_PACKAGING_OPTIMIZATION_PLAN.md` §13。

完整运行模式的 PASS 要求 Unity exit code 为 0、summary 为 `Passed`、Player 正常退出且 exit code 为 0、run 为 `finished`、timing/profiler 报告存在、animation 为 `ready`，并且本次 manifest 要求的每个 Runtime Catalog 在逐条加载记录中恰好一次命中 Binary。自报计数、列表或 `allExpectedBinary` 与逐条记录不一致会失败。角色包暂存集合由关卡中启用的角色引用和数字身份目录解析，再加入 `000001` 默认运行依赖；当前关卡为 `000001/000002/000003/000009`。构建前会验证关卡角色 ID、导入包及场景缓存，缺包或旧 ID 在资源与 Player 构建前阻断。镜像是本机构建产物。

The Combat HUD action field only describes the current Combat/ability action.
`Action: None` / `动作: 无` means no combat action is active; idle and movement
animation should still be driven independently by the locomotion animation
backend.

诊断选项：`-SkipResourceBuild` 复用经过当前 G 域源身份、marker/report 及逐文件哈希核对的资源产物；`-NoRun` 只构建 Player，summary 的 `acceptanceMode: BuildOnly` 不表示 Player 完整运行通过。构建前会阻断 Play Mode 和相关脏场景，不会代为保存其他打开场景。输入回执记录 G（全局资源）、C（本次关卡选择的完整角色包集合及默认依赖）、R（Runtime Catalog 镜像）在准备前后与 Player 构建前后的身份。运行结果还会交叉核对实际 IL2CPP 后端与 Release/Development 标志；旧报告文件名和 scenario 标识保留兼容，实际配置以配置回执和运行字段为准。

每次完整打包结束后，在同一输出目录打开 `CharacterTestPackageOverview.html` 可查看最终状态、整包和 `StreamingAssets` 各自的文件体积占比、正式 Bundle 的资源规则分类、最大资源文件与清单外 Bundle。`CharacterTestPackageTestSummary.txt` 的 `overviewReport` 和 `overviewReportStatus` 指向并核对该文件；脚本成功时也会打印路径。单独执行 **Build And Run Player Warm Run** 时，产物默认写入系统临时目录下的 `WGF-Warm-<标识>/`，网页位于其中 `Reports/CharacterTestPackageOverview.html`；Unity Console 会打印实际路径，该目录不会被 Unity BatchMode 退出时清空。残留台账证据复制为报告目录内的 `CharacterTestBundleResidueReport.txt`，网页链接可随输出目录一起保留。报告按 Player 目录中实际文件长度统计（MiB = 1,048,576 B）；Bundle 内部单项素材大小、压缩下载大小与磁盘分配空间不由该报告推断。若构建未成功，本次报告只显示失败状态，不沿用旧 Player 的体积；清单外 Bundle 仅报告并保留。

概览首页先显示打包评审，区分需要修复、待确认、优化候选、正常/保留与证据不足，并给出依据和建议；评审不替代原始构建/运行验收，也不会自动删除资源。

概览还会显示 Player 运行文件与数据文件的用途、GameAssembly 原生模块归属、内置资源压缩前类型/来源汇总和最大资源引用链。详细快照为 `CharacterTestPackageDetails.json`；原生分组使用匹配 PDB 的文件区间，资源分组使用本次 BuildReport，二者不会混作压缩后素材占比。正式 Bundle 目录中的同路径声明会提示复核，报告不自动删资源；原生分析工具不可用时明确标注。已有旧包没有明细快照时只显示实际文件列表，不从其他构建的日志补数。详见优化文档 §14。

Windows Package Test / Player Warm Run 的完整运行验收还会通过既有输入命令链执行移动、停止、普攻和跳跃落地，并核对普攻及跳跃实际播放的 clip 与 Authoring 引用一致。逐帧姿态与动作证据位于 `Reports/CharacterTestPlayerGameplayProbe.csv`（单独 Warm Run 位于其报告目录）；缺少 `gameplayProbe.*` 的旧 idle-only 报告不再表示玩法验收通过。正式 CharacterActions JSON 会镜像到 `StreamingAssets/MxFramework/CharacterActions` 并纳入 R 域源/输出身份核对；Player 从实际 `Application.streamingAssetsPath` 读取，缺少角色默认装备动作集时在构建前阻断。

### CharacterTest Ability Hotbar UI (2026-06-12)

Dash Strike root motion uses `rootMotion.obstaclePolicy=StaticOnly`: static world still blocks the dash, while hostile hits come from the `combatTrack` hitbox window rather than a gameplay request.

CharacterTest Play mode now uses the FairyGUI Combat HUD and AbilityBindingPanel as the default ability hotbar UI. The old UI Toolkit temporary skill panel remains as legacy component code, but the scene startup path no longer creates it from `CharacterTestSceneProfile`. The default hotbar mapping is `1` Fireball, `2` Burst, `3` Flame Lance, `4` Lob, `5` Surge, `6` Return, `7` Tornado, `8` Guard Aura, and `9` Fireball Explosion. Fireball Sway, Area Confinement, Beam Lance, Channel Beam, Laser Lance, Chain Lightning, Poison Bottle, Stun Bolt, Meteor Barrage, Sword Wave, Dash Strike, Hook Chain, Arc Mine, and Rune Turret remain available as bindable ability choices. Beam Lance is the renamed `910014` piercing beam and still applies `STOP` Stasis for 90 frames on accepted hostile hits. Channel Beam is the 30/60/30 sustained beam. Laser Lance is the new `910016` point-to-point single-target laser and does not apply Stasis. Chain Lightning is the new `910017` locked-target-first chain skill and does not apply Stasis. Poison Bottle is the renamed `910018` toxic pool skill and applies `POIS` Poisoned through its ground pool, not Stasis. Sword Wave is the bindable `910022` linear crescent slash: frames 13/73/133 projectiles, 30m range, five 0.22m capsule samples across a 0.85m half-width blade, up to 4 hostile hits, wall/static blocking, no Buff/status. Dash Strike is the bindable `910023` mobility + caster body hit window skill: frame 6..19 root motion moves 0.3m local forward per frame, frame 6..20 opens `StartHitTrace.hitVolume.sourceKind=CasterBody` with `OncePerEvent`, and its hit range is derived from the caster runtime physics body plus action-local CasterBody modifiers while static world blocking still comes from the kinematic motion authority. Hook Chain is the bindable `910024` projectile hit -> tether pull skill: frame 12 fires `character_test.hook_chain.projectile`, a 12m Linear DirectHit hook that stops on the first accepted hostile hit and then applies deterministic `ExternalForceRaw` pull requests to move that target toward the caster's front anchor. Arc Mine is the bindable `910025` projectile carrier -> drop projectile -> fuse mine skill: frame 12 throws `character_test.arc_mine.projectile` on a ballistic arc to the locked hostile's feet within 8m or caster forward 5m fallback; carrier hostile hit deals no damage and spawns `character_test.arc_mine.drop_projectile`, while carrier/drop landing/static impact/expiration creates the mine. A hostile trigger starts a 60f fuse, then fuse completion creates the 2.25m blast through `HitResolveSystem` and removes the mine. Its presentation uses dedicated `vfx.magic.arc_mine.flight/deploy/fuse/trigger` resources: projectile flight is view-only, and deploy/fuse/trigger VFX only read runtime events. Rune Turret is the bindable `910026` deployable persistent turret skill: frame 12 throws `character_test.rune_turret.deploy_projectile` on a ballistic arc to the locked hostile's feet within 8m or caster forward 5m fallback; landing/static impact/expiration creates a deterministic turret that waits 18f, scans 10m every 12f, and fires up to 8 `character_test.rune_turret.shot_projectile` shots at 45f intervals. Shot damage uses `damage.rune_turret_shot` through `HitResolveSystem`; VFX only read deploy/idle/fire/flight/impact/expire runtime events.

Click a slot to select and, if occupied, release it. Click a candidate to equip it into the selected slot. Keyboard `1..9` submits the same local skill-slot intent through `SetControlIntent`; it does not read Unity keyboard directly from gameplay rules. The slot table is temporary UI/runtime state for the current Play session only: it is not saved to authoring JSON, SaveState, replay, or network loadout state. Cooldown rejection, empty slots, ability lookup failure, projectile spawning, hit events, damage, VFX, and Area Confinement telegraph/channel/zone creation continue to use the existing AbilityRuntime, CharacterAction, CombatPhysicsWorld, Buff v2, VFX runtime, and motion request paths.

### CharacterTest Authority Pose Preview Hotkeys (2026-06-13)

CharacterTest Play mode includes temporary authority pose preview hotkeys. Close the main menu, then press F5 for `LightSecondary`, F6 for `HeavySecondary`, F7 for `FullRigPulse`, or F8 for `SideSecondary`. If a target is locked, the preview applies to that target; if no target is locked, it applies to the local player. The preview enqueues `CharacterTest.ApplyAuthorityPosePreview`, so it enters the fixed-frame runtime command, replay, hash, and network authority paths. It changes only authority pose offsets for configured regions/sockets; it does not change HP, scatter pressure, action state, movement authority transform, or gameplay attributes.

F5 and F6 no longer trigger the older Fireball Burst / Fireball Lob debug shortcuts. Those projectile abilities remain available through the temporary skill panel / slot flow. If the selected target or self fallback cannot accept the command, the hotkey is rejected with diagnostics; the developer debug snapshot shows the last preview key/effect, target id, accepted/rejected state, active secondary count, known region count, resolved region, match source, and latest issue. Unity views mirror the authority pose after animation sampling, and socket bindings are re-applied so weapons and socket VFX anchors follow the same pose state that weapon trace/projectile origin reads.

- 框架只提供通用机制；具体属性 ID、Buff ID、AI 事实 key、技能和怪物逻辑都由游戏层定义。
- 游戏层负责组合根，把 Config、Buff、Modifier、AI 的默认实现装配起来。
- Runtime 不依赖 UnityEditor，不依赖 WGame、Entitas、Luban 或 AI 插件。
- 配置表先通过 `ConfigSchema` 描述，再由项目自己的 Excel/CSV/Json/Luban/ScriptableObject 适配器转成 `ConfigTable<T>`。

### CharacterTest Surface Material Probe (2026-06-13)

CharacterTest surface material testing uses `CombatSurfaceMaterial` end to end. In the official `CharacterTestScene`, surface and boundary collider instances are centralized in `GameManager.WorldObjectSceneCache`; material, shape, layer, and default collider data resolve from the noEngine `CharacterTestWorldObjectDefinitionCatalog`. The GameManager scene cache builds those records into `CombatPhysicsWorld` colliders without converting them to friction-only data. Legacy per-object `CharacterTestCombatPhysicsAabbMarker` and source/bridge components are still readable as fallback for older scenes, but they are no longer the recommended authoring path. The kinematic motor result writes the full material back to `CharacterTestCharacterEntity`, and network snapshots / state hashes include material kind, stable id, friction, elasticity, and fixed bounce height.

`HardSlick` is a low-traction ice-style surface: it keeps the normal run speed but limits grounded acceleration, braking, and reverse-turn response, so the player starts slowly, slides after releasing input, and keeps momentum when turning around. `Elastic` is restitution-style bounce: it restores the incoming normal velocity by `elasticity` and does not add extra energy. `BouncePad` is fixed-height bounce: it sets the outgoing normal speed from `fixedBounceHeight` and caps the launch to that height, so it can add energy on low-speed impacts without growing unbounded over repeated bounces.

The Developer Debug panel toolbar exposes `Impulse`, `Hit`, `Push`, and `Reset`. Each button enqueues `CharacterTest.SurfaceProbe` through `GameSlice.EnqueueSurfaceProbe`; the UI never writes transforms or velocities directly. `Impulse` and `Push` enqueue fixed motion requests, `Hit` also applies a short hit reaction state, and `Reset` returns the local player to the surface probe start position through the same runtime command boundary. These controls intentionally do not use `1..9`, pause/menu input, or F5-F8.

Open `Assets/Scenes/CharacterTestScene.unity`, close the main menu, and start from `TestCourse_00_Start` near the default player spawn. `TestCourse_10_SurfaceMaterials` sits behind the start area on four labeled ground strips (`SurfaceProbe_HardSlick`, `SurfaceProbe_Rough`, `SurfaceProbe_Elastic`, and `SurfaceProbe_BouncePad`). Expand Developer Debug and use the probe buttons there; the character debug card shows the resolved `surface`, `Surface Friction`, `Surface Elasticity`, and `Surface Bounce Height`, while the Commands tab shows accepted/drained `CharacterTest.SurfaceProbe` commands and rejection diagnostics.

### CharacterTest Static Planar Slope Course (2026-08-24)

Open `Assets/Scenes/CharacterTestScene.unity`, enter Play Mode, and use `Start` / close the default main menu. The formal slope course is under `TestCourse_30_MotionGeometry/CharacterTest_Physics_SmoothRamps`:

- `CharacterTest_Physics_SmoothRamp_15deg/SlopeSurface`
- `CharacterTest_Physics_SmoothRamp_30deg/SlopeSurface`
- `CharacterTest_Physics_SmoothRamp_50deg_Blocker/SlopeSurface`

The three visible Cube children are authoring/presentation geometry only and intentionally have no Unity `BoxCollider`. Runtime authority comes from the compiled Fix64 Level v2 OBBs in `GameManager.WorldObjectSceneCache`/`CombatPhysicsWorld`. Their identities and frozen compiled geometry are:

| Surface | Stable identity | Body.Collider | Center raw | Half extents raw | Axis X / Y / Z raw | Material |
| --- | --- | --- | --- | --- | --- | --- |
| 15° | `character-test.slope.15deg` | `10080.1` | `(-22500000,534976,-7000000)` | `(1250000,250000,3000000)` | `X=(0,0,-1000000)`; `Y=(258819,965926,0)`; `Z=(965926,-258819,0)` | Rough, layer 1, friction raw `60000000` |
| 30° | `character-test.slope.30deg` | `10081.1` | `(-22500000,1283494,15000000)` | `(1250000,250000,3000000)` | `X=(0,0,-1000000)`; `Y=(500000,866025,0)`; `Z=(866025,-500000,0)` | Rough, layer 1, friction raw `60000000` |
| 50° | `character-test.slope.50deg` | `10082.1` | `(-23200001,2137436,18500000)` | `(1250000,250000,3000000)` | `X=(0,0,-1000000)`; `Y=(766045,642788,0)`; `Z=(642788,-766044,0)` | Rough, layer 1, friction raw `60000000` |

The formal labels read `Static OBB slopes · slope limit 45°`, `15° / 30° walkable`, and `50° blocked`. The neighboring terraced course remains the AABB step-up comparison and is labelled `AABB terraced ramp: small 0.10m increments`; it is not the smooth-slope implementation.

Use normal movement to approach the 15°/30° surfaces, walk uphill, release input to stop, walk downhill, and traverse sideways. With the current CharacterTest default `SlopeLimitDegrees=45°` (`GroundMinNormalY raw=707128`) and `GroundSnapDistance=0.10m` (`raw=100000`), the expected authority behavior is:

- 15° and 30° remain `Grounded/Walkable`, climb and descend continuously, stop without X/Z drift after braking, and preserve legal lateral motion.
- Downhill support may report `GroundSnap`; its applied distance must stay within 0.10m. Moving beyond a real finite edge by more than the expanded capsule support guard becomes `Airborne` and must not remain snapped to the slope.
- Jump leaves the slope without snap on the upward-intent tick and can land back on the typed OBB.
- 50° reports `Obb/Steep` with `SteepSlope`/wall-style blocking. “Blocked” means the character cannot climb or penetrate it beyond the configured skin; legal tangent/downhill motion and falling back to the base AABB are allowed.
- Dash Strike (`abilityId=910023`, action327, authored Root Motion on frames6..19) and typed external impulse use the same kinematic authority as ordinary movement. To test Dash manually, bind it through the Abilities panel first; the automated slope acceptance validates the formal `CastAbilitySlot` runtime command, not a particular physical key or UI click path.

Developer diagnostics should be read as same-frame observation. The motion row exposes target body/collider/shape, normal, `GroundMinNormalY`, `Walkable/Steep`, `GroundSnapAppliedDistance`, collision flags, requested/applied delta, resolved velocity, grounded and surface material. It is not saved in CharacterTest snapshot/hash/network payload; replay/restore recomputes it on the next fixed tick. A visible Unity mesh or Collider Inspector is not authority evidence.

普通玩家/AI active locomotion贴单面AABB或OBB墙时会保留本帧已达到、且不高于target的水平移动速度；skin-safe stop不会再重复吞掉切向时间。Dash Root Motion、Impulse、击退、external acceleration和velocity override仍走普通物理投影，不由墙滑策略补速。真实凹角或第二个实体仍会正常阻挡：例如`CharacterTest_Physics_Wall_X`中央同时存在`CharacterTest_Physics_Step_055_Block`，该块高0.55m而正式`MaxStepHeight=0.35m`，从墙边正面撞上它时X/Z均被阻挡是预期行为，不是`IterationLimit`或隐形墙。

两个相向OBB侧壁形成窄缝、且角色同时受到两侧相向的水平约束时，Motor会提取两侧共同水平切线并重新经过其余正式碰撞体求解；沿缝出口的输入因此可以脱离，不会在侧壁与斜坡复合法线之间循环到`IterationLimit`。该识别不要求两次命中都恰好为零距离：例如30°坡面可先产生带Y分量的`SteepSlope`命中，随后50°侧壁产生零距离Wall命中。无共同切线的静止或横向挤压会确定性停止本帧而不耗尽迭代；横向分量不会被保留，正交凹角、第三个阻挡体、地面、斜坡和普通单墙仍按原合同阻挡。`CharacterTestScene`中30°/50°正式斜坡保持原1m物理夹缝，未通过移动场景物体或扩大通道规避该问题。

For the repeatable developer-only Game View gate, run the custom Unity tool `character-test-slope-game-view-acceptance` from the formal active Scene. It pauses at eight screenshot points: 15° up/stop/down, 30° up/stop/down, 15°/30° traverse, 50° ordinary blocking, 30° jump/land, 30° real edge, Dash327 on30°/50°, and Impulse on30°/50°. Capture the real Game View framebuffer at each `screenshot_ready` hold, then invoke `advance`. The terminal report is written to `Temp/MxFramework/TriggerTests/mx.trigger.test.character-test.slope-game-view/latest.json`; this harness uses test-only direct setup between independent segments and does not certify Windows keyboard/mouse devices or UI hit testing.

Current limitations are deliberate: AI commands that physically reach a slope obey the same Motor, but Runtime AI Planner/navigation does not understand height-aware slope routes; movable natural objects have no OBB slope dynamics; dynamic/rotating/moving platforms, curved surfaces, Terrain, triangle meshes, arbitrary MeshCollider slopes, foot IK and slope-specific animation blending are unsupported. Generic Combat callers still get zero ground snap unless they explicitly configure it; CharacterTest's 0.10m value is project-specific.

### CharacterTest Character Potency Reception / Living Natural Attributes (2026-07-08)

In current CharacterTest, Genesis potency on characters is read through the combat/body reception chain rather than direct `D / P / S` material mutation. Real combat hits use `impactPower`, hit zone impulse scale, and source/target mass to produce `ScatterImpact`, knockback, and `GameplayScatterPressureRequest`; action support windows update `CurrentCohesionLimit`. The visible/readable result is `ScatterDebt / CurrentCohesionLimit`, pressure band, `CohesionBreak`, reaction action, floating text, HeadPad/HUD, and Developer Debug. This is the current role-side three-potency projection.

`CharacterTest.GenesisLivingNaturalAttributesRuntimeState` is a developer/runtime command for the body predicates `Embody`, `ApplyExternalPressure`, `ReleaseAndRecover`, `OverlimitDeactivate`, and `Reset`; it targets a character entity id and writes CharacterTest-internal authority state. It is not a player input, not a public API, and not a non-character material effect system.

### CharacterTest Movable Natural Object Sandbox (2026-06-14)

CharacterTestScene now contains an offline movable natural object sandbox in `TestCourse_20_MovableNaturalObjects`, to the right of the default start area. Close the main menu, move the existing player character into that sandbox, and push objects with normal movement; there is no separate push input. The scene keeps `MovableNaturalObjectsSandbox` plus named visual transforms for AABB cubes, a Sphere, a Capsule, and a nearby rough boundary for wall-pressure tests. The seven movable instances are centralized in `GameManager.WorldObjectSceneCache` as definition id, numeric instance id, optional instance stable id, transform reference, and transform/scale source; rule data such as category, shape, material, mass, base Genesis potency, and explicit potency profiles resolves from the noEngine `CharacterTestWorldObjectDefinitionCatalog`. `GameManager` collects the resolved spawn specs into `GameSlice`; with valid CharacterTest static collision authority, the fixed-frame runtime applies deterministic gravity and static floor/platform/ceiling contact before horizontal side-contact resolution, while the Unity view bridge mirrors the resulting three-dimensional runtime position back to the referenced transforms. Without that physics authority, harness use retains the previous fixed-height behavior. `CharacterTestWorldObjectInstanceMarker`, `CharacterTestMovableNaturalObjectMarker`, and source MonoBehaviours remain as legacy compatibility readers only.

World-object, level-point, and scene-test-character editing should go through `MxFramework/Level Editor`, not direct cache editing in the GameManager Inspector. The file `Assets/Config/MxFramework/Levels/CharacterTest/character_test_scene.world_objects.json` is now a modular `mx.level.authoring.v3` envelope. The CharacterTest world-object module id is `characterTest.worldObjects`; its payload stores explicit `movableInstances`, `singleStaticColliders`, `staticColliderBatches`, `points`, and `regions`, and resolves references by `UnityEditor.GlobalObjectId` first with hierarchy path fallback. The CharacterTest character module id is `characterTest.characters`; its payload stores explicit scene test character instances with enabled state, stable id, display name, transform reference, character id, team id, control owner, group, and notes. Static batches scan only direct children and generate the expanded static collider cache on Apply; generated collider rows are not written back to JSON. The UI Toolkit editor shell owns the structure tree, fixed-column virtualized list, inspector, scene placement, and diagnostics panel, while the CharacterTest modules supply rows, definition controls, validation, and Apply behavior. Apply writes world objects to `GameManager.WorldObjectSceneCache` and characters to `GameManager.CharacterSceneCache`; runtime/build code still reads those GameManager scene caches instead of authoring JSON.

完整窗口以中文优先显示，标题为 `Mx 关卡编辑器`，模块选择显示为 `世界对象` 和 `场景角色`。中文对象名、角色、枚举、状态和诊断说明都来自 Editor-only 展示映射；原始 module id、stable id、schema、JSON path、GameObject path、枚举保存值以及内置 `CharacterTestWorldObjectDefinition.DisplayName` 不会被翻译或写回。因此切换和操作中文界面不会迁移已有 JSON，也不会改变 authoring content hash、运行时定义 hash 或场景缓存结果。诊断界面始终保留稳定 code 与 JSON path，原始英文消息位于 tooltip 技术详情中。

当前窗口按“常用操作可见、上下文操作按需展开”组织：顶部直接显示重新加载、校验、工具和保存；`保存`是唯一提交入口，会统一保存关卡 Authoring 与受管 Scene，并在校验成功后同步重建 Runtime 场景缓存；左侧在按类型与按分组之间切换；中间任务栏提供搜索、添加当前选中和`放置`，放置参数在按钮旁的临时弹层中配置，不占用列表常驻高度；右侧在属性与运行时检查之间切换；底部校验状态提供查看详情入口。`工具` 菜单保留打开 JSON、选择场景根节点，以及模块自己的导出、修复引用或 Runtime AI Planner 入口。

第一阶段的快速布置能力统一覆盖世界对象和场景角色：点击`放置`在按钮旁打开临时参数弹层，Scene 视图按可见表面显示真实网格/材质或实体形状预览，并以左键确认、`Esc`或`结束放置`取消；可选网格图标读取 Unity 的移动吸附步长且只吸附世界 X/Z，场景标记图标只控制标签、范围和辅助轮廓，不影响放置交互；普通对象属性区提供`重新放置`、`复制并放置`和`更多`，区域属性区把高频操作收成`选择 / 定位 / 重新放置 / 更多`一行，复制、聚焦、网格与删除放在`更多`。关卡编辑器打开时，在 Scene 对已登记对象右键单击，会在鼠标位置提供保存、复制并放置、重新放置、对齐网格和删除等上下文操作；右键拖动及空白处右键保持 Unity 原行为，模型子节点会解析到已登记根对象。复制会创建新的 GameObject、Transform 引用和数值 ID 或稳定 ID，同时保留玩法属性、分组、旋转和缩放。`更多`中的`仅移除登记`会保留场景对象，`删除场景对象`会确认后同时清理当前模块登记；仍被其他模块或静态批次引用时会阻止删除。新建、复制、重新放置、移除登记和删除均与 Unity Undo/Redo 同步；直接用 Unity 工具移动、旋转或缩放已登记对象，以及其他使受管场景变脏的编辑，也会进入未保存状态。`保存`会先预检全部有改动的模块，再依次写入 JSON 与场景缓存，最后只保存一次 Unity 场景；校验失败不会提前保存场景，写入异常会恢复配置快照并保留未保存状态。Transform 以场景为权威，JSON 不重复保存坐标。关闭窗口时使用 Unity 原生`保存 / 不保存 / 取消`提示：保存会提交全部模块 JSON 与场景，不保存会重新载入最近保存的场景和配置并放弃当前活动场景的全部未保存修改，取消则继续编辑。场景根下未被任何关卡模块登记的顶层对象会显示`未登记 N`警告和定位入口，已登记对象的模型子节点不会误报。静态碰撞体批次暂不支持复制或重新放置。操作说明见 [Mx 关卡编辑器：快速布置指南](Guides/LEVEL_EDITOR_QUICK_PLACEMENT_GUIDE.md)。

世界对象定义现在具有独立数据源 `Assets/Config/MxFramework/Levels/CharacterTest/world_object_definitions.json`。它确定性生成 Editor runtime catalog 与 `Assets/StreamingAssets/MxFramework/CharacterTest/RuntimeCatalogs/Levels/world_object_definitions.runtime.json` Player mirror；两者保存同一 source/content hash。菜单 `MxFramework/Character Test/Definitions/Export Built-in World Objects` 可重新执行 built-in 迁移，`Rebuild World Object Artifacts` 可从当前 Authoring 重建产物。世界对象模块左侧的`对象定义`进入定义工作区：沿用虚拟化列表、搜索和分类列，可新建、停用并编辑名称、稳定 ID、分类、ResourceKey 和类型化玩法属性；保存、撤销、重做及关闭时恢复与关卡文档处于同一用户流程。定义工作区不提供容易与场景复制混淆的“复制为变体”，场景对象统一在 Scene 右键菜单使用`复制并放置`。Prefab 只保存结构化 `GameObject` `ResourceKey`，编辑器通过正式 Resource Catalog 解析并显示缩略图、真实 Scene 预览及保持关联的 Prefab 实例；资源缺失会阻止放置，只有显式选择 `PrimitiveAuthoring` 才会生成基础几何体。示例 `101 Slick Light` 已绑定 `art.level.world_object.block_01`，其余迁移定义继续保留显式 primitive 表现。启动时 Editor 会拒绝与 Authoring source hash 不一致的 stale generated catalog，Player 会验证 runtime content hash；全部候选不可用时保留 built-in fallback 并提供结构化诊断。场景角色的定义下拉也改为读取 Character Application/RuntimeSpawn 的可生成角色 registry，不再只显示硬编码角色。

P3.3 把世界对象 Region Inspector 的大卡片迁到顶层`玩法触发器`工作区。顶部选择该工作区后，左侧按实际内容派生`区域玩法 / 关卡流程 / 战斗状态`分类并支持搜索；右侧以紧凑`事件 / 条件 / 动作`条款编辑当前规则，底部只显示一条确定性效果句。锁定来源、身份和模板事实使用普通文字，不伪装成禁用输入框；阵营、参照队伍、Buff、来源机制和 Story 选择器直接嵌入条款，动作目标不再重复占用独立卡片。P3.3e 在条件区增加可保存的`全部满足（且） / 任一满足（或）`根组和逐条`非`，直接读写现有 Gameplay Trigger Condition Tree；区域来源只开放`触发对象存在`，玩家生命阈值来源额外开放触发前/当前生命值比较，复杂历史树只读保真。名称、启用、P3.2 目标筛选、附加条件、Story/Buff 参数、来源机制、冷却与次数、复制和删除继续修改同一份 `GameplayTriggerAuthoringDocument`，并复用世界对象 module 的同一 Undo Session、脏状态、保存与关闭恢复。Region Inspector 现在只显示`关联触发器 N 个`、编辑入口和新建入口；Scene 右键也可打开或新建关联规则，多个规则共用 Region 时先显示过滤列表，不擅自选择第一条。窄于 920 px 时左侧列表可折叠，不保留旧 Inspector 空白栏。P3.3d 与 P3.3e 在标准和窄窗口验收完成前保持 Partial。

当前新建菜单只提供已有正式编译和 Runtime 消费链路的`进入时触发剧情`、`进入时施加 Buff`、`区域内维持 Buff`和`当前玩家生命值低于阈值时触发 Story`。其他 Signal、Threshold、时间、交互、任务和属性来源不会作为占位项出现；固定模板仍拥有 Route/Action 拓扑，使用者编辑其已开放参数，不直接操作 action kind、payload 数组或任意 Route。模板仍由正式 `GameplayTriggerConfigCompiler` 预检；Buff 候选来自当前 `GameplayBuffDefinitionRegistry`，持续时间、叠层和效果由 Buff Definition 权威决定，Trigger 冷却和次数只属于 Route。`高级规则`渐进暴露策略与技术信息。非法旋转、缺失 Transform、未知引用或 Compiler 错误会阻止保存。完整边界与手工测试见 [P3.3 设计](LEVEL_EDITOR_UNIFIED_TRIGGER_WORKSPACE_DESIGN.md)和[开发指南](Guides/LEVEL_EDITOR_UNIFIED_TRIGGER_WORKSPACE_DEVELOPMENT.md)。

P3.0 已增加 Editor-only 通用创作引用控件，但没有新增万能 Runtime 目标/来源模型。`作用目标`仍由当前 Trigger route、`GameplayTriggerTargetSelector`和 binding 解析；当前 Inspector 将结果人性化显示为`作用对象`：区域 Buff 为`进入区域的角色`，Story 为`当前区域`，Owner、Instigator 和不兼容 Domain 不会伪装成可选项。Buff 的用户界面字段名为`来源机制`，底层仍写入既有 `Payload1`并映射到 `GameplayBuffApplicationRequest.SourceId`；它只记录最近施加来源，不改变伤害、持续时间、叠层或移除规则，也不会执行来源定义自己的 Effect，并与 Trigger Source 提供的`触发对象`及 Runtime 授权/溯源的`命令来源`保持隔离。普通区候选按正式 Aura/Mechanic Source 目录稳定显示`未指定 / 501008 守护光环 / 501010 区域禁锢 / 501012 毒瓶池`，没有正式 Buff 来源定义的 501013/501014 不出现。0 个候选只显示诊断、1 个只读、多个使用紧凑选择框；历史未登记值会保留并提示，只有用户显式重选或在高级规则中迁移时才改变。详细边界见 [P3.0 通用创作引用设计](LEVEL_EDITOR_COMMON_AUTHORING_REFERENCES_DESIGN.md)。

P3.1 进一步把“目标从哪里来”和“目标是什么”拆开。Gameplay 基础身份只包含`Character / WorldObject / SpatialPoint / SpatialRegion`，能力首版为`Damageable / Movable / Static`；Self/SameTeam/Enemy/Neutral 属于关系，Player/NPC/Boss/Summon 属于控制、定义或 Tag，不创建万能身份枚举。Component target query 可依次按基础身份、必需能力、存活、关系、Tag、Status 和数量筛选；未配置身份/能力时兼容旧 candidate，配置后对未声明 classification 的 candidate 给出 typed rejection，未定义 relation/identity/capability 在 query 构造阶段直接拒绝。Character spawn 与 CharacterTest runtime entity 都声明`Character + Damageable + Movable`。关卡 Trigger Inspector 不再把技术层级直接摊给使用者，而是在`作用对象`中显示区域 Buff 的`进入区域的角色 / 可伤害 · 阵营不限`或 Story 的`当前区域`；Trigger Source、Subject、完整能力与 Tag/Status 规则保留在 Tooltip，本阶段不修改 Trigger JSON，也不增加无效选择框。详细边界见 [P3.1 目标语义基础设计](LEVEL_EDITOR_TARGET_SEMANTICS_DESIGN.md) 与 [开发指南](Guides/LEVEL_EDITOR_TARGET_SEMANTICS_DEVELOPMENT.md)。

P3.2 已把第一批目标筛选变成可保存、可编译并在运行时真实生效的规则。区域 Buff 的`作用对象`可选择`阵营不限 / 仅同队 / 仅敌对 / 仅中立`，非不限关系再从当前场景角色文档的 TeamId 选择参照队伍；Story 仍以`当前区域`为作用对象，并把同一套规则放在独立的`触发对象`区。角色、存活和 Buff 的可伤害最低要求由模板锁定，`必须可移动`放在`高级规则`按需开启。Runtime 继续由 Combat Sensor 查询并映射 Gameplay entity，在提交 complete occupancy 前读取 classification、team 和 lifecycle 执行 NoAlloc 筛选；被筛除或运行时不再匹配的对象不会创建错误 activation，并会通过完整采样产生正常 Exited。CharacterTest Trigger module/document/cache 已升级到 v2：旧 v1 文档加载时按模板补默认规则且不会仅因打开而变脏，点击`保存`会写入 v2 并重建旧 Scene cache。关系、参照队伍和额外能力均支持单步 Undo/Redo，运行时检查显示命中、映射、通过、过滤和首个 typed rejection。详细见 [P3.2 目标筛选创作设计](LEVEL_EDITOR_TARGET_FILTER_AUTHORING_DESIGN.md) 与 [开发指南](Guides/LEVEL_EDITOR_TARGET_FILTER_AUTHORING_DEVELOPMENT.md)。

P2.5 已完成 CharacterTest offline Playable 闭环。执行`保存`会在同一协调事务中先校验全部关卡模块，再保存 Authoring、重建 `GameManager.GameplayTriggerSceneCache` 的 canonical Trigger payload、fixed Combat AABB sensor rows 和 Trigger/sensor/combined hash，最后保存受管 Scene；任一步失败都不会把半套缓存作为成功结果。进入 Play Mode 后，在`玩法触发器`工作区选择规则并从`更多 > 运行时检查`查看实际 query、sensor 命中/映射、Entered/Stayed/Exited 与 Buff/Story 动作结果。正式场景示例使用 `region.start / trigger.region.start.1`：区域内对象获得并持续刷新 `601012 · 中毒`，来源机制为 `501012 · 毒瓶池`，离开时移除；该 Buff 持续 6 秒、重复施加刷新且不可叠加。查询候选容量为 64，映射主体容量为 8。Runtime bootstrap 会重新编译并复验缓存，schema/content/hash、source binding 或动作组合不一致时整份 fail-closed；区域对象只有 Transform，不需要 Unity Collider，正式查询只走 `CombatPhysicsWorld`。Gameplay、Trigger 与 Story state 已按稳定顺序进入 checkpoint/result hash，恢复后不会重复 Entered，Trigger 稳态 Tick 保持初始化后零分配。当前仍不支持活动 session 热重载或网络会话注入；保存后需要重新进入 Play Mode 或重建 offline session。

The `CharacterTest Characters` module is the preferred place to configure who participates in a scene test. Its toolbar can create a default "local player + AiControlled enemy" template, add selected transforms, start SceneView character placement, and open the Runtime AI Planner editor. Runtime AI Planner behavior has a scene default plus character override model: `ControlOwner = AiControlled` characters use their row-level Runtime AI Planner JSON profile when set, otherwise they inherit `CharacterTestSceneProfile.RuntimeAiPlannerProfilePath` after the CharacterTest session is rebuilt.

The official `CharacterTestScene` may still contain legacy `player spawn`, `AI spawn`, and nearby AI spawn point definitions, but the current scene-test-character editing path does not use Points as a creation workflow. Offline session startup first uses `GameManager.CharacterSceneCache` when it contains character instances; any remaining point-cache startup behavior is legacy fallback only and should not be treated as the authoring source for new tests.

Developer Debug exposes a `Movables` tab, a numeric movable target field, `Apply Fire`, `Apply Hammer`, `Apply Condense`, and `Reset Objects`. Target `0` applies to all objects; a numeric id applies to one object. These buttons use `CharacterTest.MovableNaturalObjectPotencyInfluence` or `CharacterTest.MovableNaturalObjectReset` through the runtime command buffer, so UI does not mutate transforms directly. Generic `CharacterTestSliceDebugSource` also includes movable summaries for runtime inspection.

Movable natural objects are part of CharacterTest authoritative runtime state, snapshot schema v5, rollback clones, and all full/owned/non-owned hashes. Schema v5 includes `ShapeKind`, base/current/pending potency, potency profile, and the latest potency application record. Schema 4 payloads default potency to zero with disabled thresholds, schema 3 payloads default movables back to AABB, and schema 2 payloads normalize missing movable arrays to empty. This slice does not add player-facing SaveState load/save UX and does not require LAN or Multiplayer Play Mode validation as an acceptance gate.

### CharacterTest Combat Physics Shape Registry Probe (2026-06-14)

`CharacterTestScene` also contains `TestCourse_40_ColliderShapes` near the start area, with two registry probe objects: `PhysicsProbe_SphereTarget` and `PhysicsProbe_CapsuleTarget`. These probe transforms are referenced by `GameManager.WorldObjectSceneCache` static collider records; GameManager cache runtime construction appends them to the same `CombatPhysicsWorld` as Sphere/Capsule target colliders while keeping legacy Sphere/Capsule marker and bridge fallback compatible.

This area validates core query/registry integration only. The visible Unity primitives are static authoring references, not authoritative Unity Physics colliders; focused scene tests verify that `CombatPhysicsWorld.Query` hits the static Sphere/Capsule targets generated from the GameManager scene cache. Playable push testing for movable Sphere/Capsule objects lives in `TestCourse_20_MovableNaturalObjects`.

### 1.1 角色内容作者交付链路

角色内容作者默认从 `Tools/MxFramework.EditorHub/start-editor-hub.sh` 进入。Editor Hub 的 `角色交付流` 读取 `GET /api/character/readiness?package=...`，按 `角色包 -> 资源绑定 -> 动画绑定 -> 动作时间轴 -> Unity 导入 -> Runtime 验证 -> 能力运行时绑定` 显示当前能否进游戏。

推荐 happy path：

1. 启动 Editor Hub。
2. 在 CharacterStudio / Resource Manager 修角色包和资源绑定。
3. 在 Animation Editor 修动画 Set、Group、Clip 和资源引用。
4. 在 CharacterStudio 的动作槽表选择普通攻击 1/2/3、重击、闪避及动态要求槽所使用的“角色专属 / 公共动作”；Profile 与 ActionSet 只读显示编译器确定的 ID，不手填 JSON。
5. 公共动作可在 Unity 只读查看；需要修改时先点“复制为角色专属并编辑”，由 CharacterStudio 原子复制动作正文并更新当前槽引用。
6. 在 Unity Character Action Authoring 修所选动作的时间轴、命中、位移和表现；保存时若动作源已被外部修改会拒绝覆盖，并把冲突状态回传 CharacterStudio。
7. 在 CharacterStudio 跑 `Prefab 重建预检`。
8. 在 CharacterStudio 跑 `Unity 导入`。
9. 打开 CharacterTest 或 locomotion calibration 场景做运行时验证。

`AbilityRuntimeBinding: Deferred` 表示角色配置已经解析出 effective ability ids，但默认 `CharacterGameplayAbilityRegistryBuilder.BuildDeferred` 不会把这些 ID 自动注册进 `GameplayComponentAbilityRegistry`。需要运行时执行能力时，游戏层必须提供显式 `CharacterGameplayAbilityBindingCatalog` 映射；框架不会从角色配置自动发明技能语义。

角色资源包 Runtime Spawn 第一切片当前是 `Runtime Slice`：使用 `CharacterImportedPackageJson.LoadFromDirectory("Assets/MxFrameworkGenerated/CharacterPackages/codex_flow_test")` 读取 #222 导入产物，再通过 `CharacterRuntimeSpawnResolver.Resolve(...)` 或 `CharacterRuntimeSpawnModule` 得到 `CharacterRuntimeBinding`。该切片用于验证 resolver、gate、resource mapping、geometry binding 和 runtime id plan。

导入完成后可以在 Unity 中生成可摆放到实际场景的预览 Prefab：执行菜单 `MxFramework/Character/Create Preview Prefab For Codex Flow Test`，产物写入 `Assets/MxFrameworkGenerated/CharacterPackages/codex_flow_test/prefabs/codex_flow_test_character_preview.prefab`。也可以执行 `MxFramework/Character/Create Preview Scene For Codex Flow Test` 生成 `Assets/Scenes/MxFramework/CharacterImportedPreview.unity`。需要运行时手测 locomotion 动画时，执行 `MxFramework/Character/Create Locomotion Calibration Scene For Codex Flow Test` 生成 `Assets/Scenes/MxFramework/CharacterLocomotionCalibration.unity`；该场景只放置 `CharacterRuntimeResourceBootstrap` / `CharacterLocomotionCalibrationRunner` 组合根，Play Mode 中通过 `ResourceManager`、`ResourcesProvider`、`animation_set_definition.json` 和 `animation_clip_registry.json` 加载 Codex Flow Test、默认武器和动画资源，不把最终角色 prefab 作为常驻场景对象。校准 HUD 会显示状态徽标、手动方向/速度/慢速/暂停/单步控件、实际速度与动画原生速度、建议播放速度、blend domain、当前 sample、BlendTree 点、不可达诊断、当前 clip weights 和 dominant clip，并显示左右脚 contact confidence、slip cm/s、max slip distance 和 OK/WARN/BAD 等级；Game 视图中会用 cyan/yellow 速度线和足底轨迹/锚点辅助观察脚滑；HUD 的 8 向方向按钮会自动进入 Manual 并高亮当前方向，`Facing Lock` 会固定角色朝向以便观察前后左右/斜向 blend sample，`Follow On` / `View Rear|Side|Front|Top` 提供跟随镜头和多视角观察，`Camera` 滑条和 `+/-` 可以调整镜头远近，`Loop On` 会在角色离开观测区域时回绕到平面另一侧以模拟无限平面，`Grid On` 会显示 0.2m 间距格子地面来观察相对位移。`Clip` 行会显示当前 dominant clip 的 playbackSpeed，可用 `+/-` 微调或用 `Use Suggested` 应用当前建议值；`Save Draft` 会把这些播放速度覆盖导出为 calibration draft JSON。HUD 的 `Speed` 是 0-5 的移动速度倍率：输入方向保持单位方向，倍率写入运行时 `MoveSpeedScale`，会同时影响逻辑移动速度和 2D blend sample 半径；可用 `+/-` 以 0.05 步进微调。点击 `Run Presets` 会依次运行 Idle、Walk Forward、Run Forward、Walk Back、Strafe Left、Strafe Right、Diagonal、Speed Ramp，并在 HUD 中汇总每个 preset 的平均速度误差、最大脚滑、dominant clip、不可达点和建议调参字段；`Copy Summary` 复制文本报告，`Save JSON` 写入 `Application.persistentDataPath`。blend probe 数据也会进入 `CharacterRuntimeAnimationDebugSource` 的 `Locomotion Blend Probe` 区块。该 Prefab 会读取导入后的 `config/*.json`，复用 `CharacterRuntimeSpawnResolver` 做 gate / binding 校验，然后把角色主体模型、武器模型、挂点和 authoring 碰撞体装配成 Unity GameObject 层级；如果项目缺少 GLB importer，会使用占位体并在 Console 输出提示。

网络抽象第一版是 `MxFramework.Network`：noEngine、只依赖 Runtime、服务端权威。业务侧不要直接把 Unity Transport/WebSocket 或资源下载写进核心网络层；真实传输通过 `INetworkTransport` + `INetworkMessageCodec` 适配，当前已提供 JSON wire codec、codec-backed byte loopback 和独立 Unity-facing 的 `MxFramework.Network.UnityTransport`。UTP 适配使用 `UnityTransportNetworkTransport.StartServer(...)` / `StartClient(...)` 建立 UDP/loopback 连接，wire 包仍是 `NetworkMessageEnvelope` + codec bytes，不引入 NGO/FishNet/Relay。Room Session 是 gameplay session 上层编排：`NetworkRoomSessionRunner` 负责 LAN room 的 join / ready / leave / start / handoff；room 进入 `InGame` 后组合根停止 room polling，再启动现有 gameplay `NetworkSessionRunner`。

CharacterTest 进入 Play 默认创建离线 `GameSlice`，不会再通过 scene/profile 序列化值自动进入联机；默认还会显示 FairyGUI `CharacterTestFairyGuiMainMenuHost` 暂停菜单弹窗。菜单加载 `MxCharacterTestUi/MainMenuPanel` 这个真实 FairyGUI 源包组件（源文件 `FGUIProject/assets/MxCharacterTestUi/MainMenuPanel.xml`，发布资源 `Assets/Bundles/FGUI/MxCharacterTestUi/MxCharacterTestUi_fui.bytes`），Pause input 可打开菜单，MainMenu 打开时同一有效按键会走 UI Cancel 关闭菜单；Unity Editor Play 下 CharacterTest 会把现有 `Gameplay/Pause` 和 `UI/Cancel` 的键盘默认临时切到 `P`，让 `Esc` 保留给 Game 视图释放鼠标，Player 包仍使用默认 `Esc`。菜单或从菜单打开的 NetworkPanel 可见时默认暂停 runtime ticking；该 UI 组合策略由代码内默认值固定，不再作为 `CharacterTestSceneProfile` 的可编辑项。Start / Close 会隐藏菜单并按手动暂停状态恢复或保持当前会话，Reset Local 会重建干净的本地离线会话并保持菜单可见，Network 会按需打开 `CharacterTestFairyGuiNetworkPanelHost`，Settings 会按需打开 `CharacterTestFairyGuiSettingsPanelHost`。玩家侧 LAN 联机大厅加载 `MxCharacterTestNetworkUi/NetworkPanel`（源文件 `FGUIProject/assets/MxCharacterTestNetworkUi/NetworkPanel.xml`），不是运行时动态拼接的临时面板：玩家先选择 Direct LAN 或 LAN Room，填写地址/端口、玩家名和房间名；面板会持久化上次使用的模式、地址、端口、玩家名和房间名，但不会持久化运行时 client peer id；Host / Join 继续表示角色，`CharacterTestNetworkSessionOptions.LaunchMode` 决定启动 `UnityTransportHost` / `UnityTransportClient` 还是 `UnityTransportRoomHost` / `UnityTransportRoomClient`；Host 会开本机权威 server 并用本机 client peer 连接自己，房主也通过 assignment、command packet 和 snapshot/resync 路径获得 owned entity；玩家面板默认会为每个运行实例生成稳定的唯一 client peer id，避免多实例默认都以 `client-0` 接入而撞到同一 ownership；Host / Join 启动成功会停留在 NetworkPanel 中继续验通，Room 模式由成员 Ready 后 Host 点击 Enter Game，随后各实例在本地 gameplay handshake 完成后自动关闭联机界面并恢复 gameplay tick；Direct LAN 保留为诊断模式，连接验通后本实例本地点 Enter Game 进入；Disconnect 会释放当前 runner 并重建离线会话，Back 只在非 active session 下返回主菜单。面板展示连接状态、endpoint、room ready/connected/member 摘要、room reason 和 transport / peer / RTT / frame / snapshot / disconnect 诊断摘要；它只提交 options/commands 给 `ICharacterTestNetworkSessionController`，不直接改 `GameSlice` 或 transport。第一版切换时重启 CharacterTest 会话，不迁移当前离线战斗状态，也不新增 AppFlow/SceneFlow 状态。`CharacterTestSceneProfile` 不保存玩家会在 NetworkPanel 中填写的地址、端口、玩家名或房间名；这些连接/房间值来自 `CharacterTestNetworkSessionOptions` 默认常量和 `CharacterTestNetworkPanelPreferences` 持久化输入。Profile 只保留 network input delay / snapshot / heartbeat、loopback impairment 和 UTP queue / window / capacity 等低层调参；修改后需要重新进入 Play 让新 driver 生效。

Settings 打开的 `MxCharacterTestSettingsUi/SettingsPanel` 使用左侧画面/音频/按键导航；按键页可切换键盘鼠标与手柄，并用动态滚动列表展示 8 个 Gameplay 动作和 `UI/Submit`、`UI/Cancel`。`TargetLock` 在键鼠页提供 `L` 与 `Middle Button` 两个独立槽位。点击槽位进入监听，Esc 只取消本次监听；Apply 一次性提交 profile 与输入 override，Cancel/Close 回滚到打开面板时的 profile 和输入状态。具体 target catalog、事务兼容边界和测试入口见 `Docs/Interfaces/Settings.md`。

Unity Multiplayer Play Mode 已作为本地多人手测入口接入，包版本为 `com.unity.multiplayer.playmode@1.6.3`。使用时先确认 `Edit > Preferences > Multiplayer Play Mode` 中已启用 Multiplayer Play Mode，再打开 `Window > Multiplayer > Multiplayer Play Mode` 激活至少 1 个 Virtual Player；`Window > Multiplayer > Play Mode Status` 可查看各实例状态。默认手测 LAN Room：Main Editor Player 进入 `CharacterTestScene` 后打开 NetworkPanel 并选择 `LAN Room -> Host`，Virtual Player 打开同一面板选择 `LAN Room -> Join`，地址填 `127.0.0.1`，端口保持一致；双方 Ready 后只有 Host 点击 `Enter Game`，之后各实例在本地 gameplay handshake、owned entity 和 full snapshot 验通后自动关闭面板并恢复 gameplay tick。Direct LAN 仍作为诊断模式：Host/Join 连通并完成本地验通后各自点击 `Enter Game`；Address 也接受 `host:port`，例如 `192.168.1.201:7777`，此时地址里的端口会覆盖 Port 字段。每个运行实例默认生成不同 client peer id，Host 和 Virtual Player 应分别控制不同 owned entity。Standalone 双实例仍是等价 fallback：Editor 或一个 build 作为 Host，另一个 build 以 `127.0.0.1` 或 Host LAN IP Join。

普通 Play 首屏只显示暂停菜单：`StoryDialog` 会在 MainMenu、NetworkPanel 或 SettingsPanel 打开期间被 Surface Policy suppress，不作为冲突关闭目标；菜单关闭后的后续 UI refresh 会按当前 Story 状态重新评估是否显示。Debug UI 默认 `Hidden` 且不自启。MainMenu、StoryDialog、NetworkPanel、SettingsPanel 都是 player-interactive modal FairyGUI view，打开时会推入 exclusive `InputContext.UI`，屏蔽 Gameplay / Debug 等非 UI action map，并通过 `MxCursorController` push released cursor scope，关闭最后一个玩家 modal 后按 Debug / camera cursor policy 恢复。

本地 move / jump / primary attack 输入会先采样成 `SetControlIntent` runtime command，和 camera / target-lock 等命令一起提交为 client packet，server slice 授权、排帧并执行。server 为 connected peer 维护 `peerId -> controlledEntityId` ownership，角色创建/分配由 server 负责；client 不自行申请 spawn，连接后等待 reliable full snapshot 下发 assignment，只对 owned entity 做本地预测，未拥有的 entity 不接收本地输入。Host 本机玩家也使用同一 client packet / assignment / snapshot 路径，只是 client transport 连接到本机 server。loopback/headless mirror 通过 initial / requested full snapshot 收敛；`UnityTransportClient` 拿到 assignment 后会把 owned 本地命令按 `inputDelayFrames + smoothed estimated one-way frames` 排入本地 command buffer 做固定帧预测，prediction lead 限制在 `1..inputDelayFrames+10`。server 会把已接受并排到 server scheduled frame 的输入广播为 authoritative command；其它 client 将远端 owned entity 标记为 `RemotePlayer`，把权威命令写入同一 `RuntimeCommandBuffer` 确定性推进移动和动画。regular snapshot 只发 digest 做低频校验；full snapshot 仅用于初次 assignment、late join、snapshot request / resync。client 已接入通用 rollback coordinator：authoritative command echo / 远端权威命令晚到时可从受影响帧的内存状态恢复并重放到当前帧；权威帧未落后本地 current frame 时只补入 command ledger / command buffer，不产生 replay；`SnapshotVerification` 下 full snapshot correction 可恢复权威 snapshot 后 replay 本地预测命令；`BootstrapOnly` 下仍可只靠 authoritative command correction 回滚。client 会记录固定容量 128 帧 prediction history 的 owned state hash 和 rollback frame history，digest/full 诊断和 mismatch 计数保留用于调试；缺历史只计 missing diagnostic，不算 mismatch。client 断线后会按固定间隔检查底层 UTP connection，必要时重建 transport 并重新 handshake；server 断开 peer 时会清 per-peer ack、input schedule 和 reliable sequence，并把该 peer 的角色 ownership 清理为 retained/None，允许同 peer id 的新 client 进程重连。这是 live rollback + MVP 级断线重连，不包含认证 token、房间恢复、生产级角色恢复协议或视觉误差平滑。Debug UI 的 `Network Session` 区块只负责诊断，显示 transport mode/id、room status/id/members/ready/version/reason、local peer、owned entity、connected peers、ownership summary、server/client frame、rtt estimate、avg/max RTT、prediction lead frames、prediction history、rollback history/drop/count/failure/snapshot correction/replayed/max/last frame/failure、snapshot interval、frame drift、pending commands、last snapshot hash、resync count、snapshot mismatch / suppressed mismatch、comparable/missing hash、snapshot request、full/digest snapshot count、authoritative command count、wire bytes、room/command/snapshot bytes、duplicate/out-of-order messages、codec errors 和 disconnect reason。具体接口见 `Docs/Interfaces/Network.md`。当前 UTP 已接入 focused tests、`CharacterTestScene` 默认离线、同进程本机 loopback、Host/Join 单端入口、多 client ownership 验证、LAN room MVP、Room Host/Client 双向状态同步验证、NetworkSession rollback coordinator 测试、CharacterTest rollback/prediction focused 子集和 `CharacterTestReplayTests`；Steam Lobby、Relay/WebSocket/matchmaking、房间列表/discovery、binary codec、生产级断线恢复、视觉误差平滑和长时 replay UI 仍是后续工作。

进入角色控制后的 FairyGUI `CombatHudPanel` 会在网络 session active 时显示简洁延迟徽标，例如 `NET 2f / 33ms`；离线本地游玩隐藏。完整 RTT、frame drift、late/dropped command、rollback 和 snapshot 诊断仍保留在 Debug UI `Network Session` 和 NetworkPanel 诊断区。

CharacterTest default input comes from `DefaultInputBindingCatalog`: movement uses WASD/Arrow, jump uses Space, primary attack uses Mouse Left, secondary/projectile uses Mouse Right, and skill slots `1..9` submit `CastAbilitySlot` runtime commands through the CharacterTest hotbar adapter. In Unity Editor Play, CharacterTest remaps gameplay pause to `P` so `Escape` can keep releasing the Game view mouse capture. F5-F8 are temporary authority pose preview commands; Fireball Burst / Fireball Lob are no longer bound to F5 / F6 and remain available through skill slots 2 / 4.

## 2. Events 事件总线

`EventBus<T>` 是同步、类型安全的事件总线。事件 payload 推荐使用 `readonly struct`。

```csharp
public readonly struct DamageEvent
{
    public DamageEvent(int value)
    {
        Value = value;
    }

    public int Value { get; }
}

var events = new EventBus<DamageEvent>();
IDisposable subscription = events.Subscribe(e =>
{
    // 处理伤害事件。
    int damage = e.Value;
});

events.Publish(new DamageEvent(100));
subscription.Dispose();
```

约定：

- `Subscribe` 会分配订阅句柄；高频路径不要反复订阅/取消。
- `Publish` 是同步调用，handler 抛异常会直接向外传播。
- 发布过程中新增的 handler 从下一次发布开始生效。

## 3. Attributes 属性存储

`AttributeStore` 同时实现 `IAttributeOwner` 和 `IAttributeModifierOwner`。游戏层用 int 定义属性 ID。

```csharp
const int AttrHp = 1001;
const int AttrAtk = 1002;

var attributes = new AttributeStore();
attributes.RegisterAttribute(AttrHp, 1000);
attributes.RegisterAttribute(AttrAtk, 50);

IDisposable changed = attributes.OnAttributeChanged.Subscribe(e =>
{
    int attrId = e.AttributeId;
    int oldValue = e.OldValue;
    int newValue = e.NewValue;
});

attributes.AddAttribute(AttrHp, -100);
int hp = attributes.GetAttribute(AttrHp);

changed.Dispose();
```

添加属性修改器：

```csharp
public sealed class FlatAttributeModifier : IAttributeModifier
{
    public FlatAttributeModifier(int id, int attributeId, int value)
    {
        Id = id;
        AttributeId = attributeId;
        Value = value;
    }

    public int Id { get; }
    public int AttributeId { get; }
    public int Value { get; }
    public AttributeModifierPhase Phase => AttributeModifierPhase.Add;
    public int Priority => 0;

    public int Modify(int currentValue, IAttributeOwner owner)
    {
        return currentValue + Value;
    }
}

attributes.AddModifier(new FlatAttributeModifier(1, AttrAtk, 20));
int finalAtk = attributes.GetAttribute(AttrAtk);
attributes.RemoveModifier(1);
```

约定：

- `GetAttribute` 对不存在属性返回 `0`；需要区分缺失时用 `TryGetAttribute`。
- Modifier 按 `Phase`、`Priority`、`Id` 排序。
- Modifier 应保持通用，不直接读取游戏实体。

## 4. Buff v2 component-native 用法

旧 `BuffPipeline` / `IBuff` / `BuffBase` / `ConfigBuffFactory` 路径已经退役。新 Buff 状态只存在于 `GameplayComponentWorld` 的 component store 中，由 request buffer 或 runtime command 申请变更，再由系统投影为属性 modifier / status。

```csharp
var definitions = new GameplayBuffDefinitionRegistry(new GameplayBuffDefinition(
    601008,
    "buff.character_test.guard_aura.defense",
    defaultDurationFrames: 2,
    maxStackCount: 1,
    GameplayBuffStackPolicy.RefreshDuration,
    new[] { GameplayBuffEffectDefinition.AttributeModifier(1, AttrDefense, 5) }));

var requests = new GameplayBuffRequestBuffer();
requests.EnqueueApply(targetEntityId, 601008, sourceId: 501008);

pipeline.Add(new GameplayBuffApplicationSystem(definitions, requests));
pipeline.Add(new GameplayBuffEffectProjectionSystem(definitions));
```

约定：

- Buff 定义只来自 `GameplayBuffDefinitionRegistry`；active state 不保存最大层数、默认持续时间或效果定义。
- `GameplayBuffApplicationSystem` 只维护 active Buff state。
- `GameplayBuffEffectProjectionSystem` 负责生成 source-aware modifier / status，过期或移除后自动清理 Buff 来源的投影。
- 范围光环通过 `CombatGameplayAuraBuffSystem` 写入 `GameplayBuffRequestBuffer`，不直接改 Buff store。
- 命中后 Buff 通过 `CombatHitEventConfig.onHitBuffs` 配置，并由 `CombatOnHitBuffApplicationSystem` 在有效 `HitResolveKind.Damage` 命中后写入 `GameplayBuffRequestBuffer`。`Evaded`、`Blocked`、`Friendly`、`Duplicate`、`SelfDamage` 和 0 damage 不触发。

`hitEvent.onHitBuffs` 示例：

```json
{
  "hitEvent": {
    "targetTypes": ["HostileCharacter"],
    "damage": { "damageTypeId": "slash", "baseDamage": 8 },
    "onHitBuffs": [
      {
        "buffId": 601009
      }
    ]
  }
}
```

Buff v2 authoring v0 使用 `Tools/MxFramework.Authoring` 的 JSON + CLI，不使用旧 Buff 编辑器：

```bash
dotnet run --no-build --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- buff validate --package Tools/MxFramework.Authoring/samples/buff-character-test
dotnet run --no-build --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- buff compile --package Tools/MxFramework.Authoring/samples/buff-character-test --out Temp/BuffAuthoringSmoke
dotnet run --no-build --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- buff import-unity --package Tools/MxFramework.Authoring/samples/buff-character-test --project-root .
```

`buff import-unity` 默认生成 `Assets/MxFrameworkGenerated/Gameplay/Buffs/buff_definitions.json` 和 `Assets/MxFrameworkGenerated/Combat/Auras/aura_buff_definitions.json`。`GameplayBuffDefinitionJsonImporter` 与 `CombatGameplayAuraBuffDefinitionJsonImporter` 把这些文件导入 runtime registry；CharacterTest `Guard Aura` 会优先加载生成 JSON，缺失或为空时才回退到内置定义。CharacterTest 还包含 on-hit 示例 Buff `601009`，稳定名 `buff.character_test.on_hit.defense_break`，效果是 300 帧 `Defense -3`，由轻攻击、Fireball direct 和 Fireball Burst area 的 `onHitBuffs` 验证。

CharacterAction Inspector 可在所有当前伤害事件上编辑 `onHitBuffs`：武器 `StartHitTrace`、范围 `ApplyGameplayEffect`、projectile direct `impact.hitEvent` 和 projectile area `impact.areaHitEvent`。BuffId 下拉候选来自 `Assets/MxFrameworkGenerated/Gameplay/Buffs/buff_definitions.json`；未知 BuffId 允许手输保存但会提示，运行时仍由 Buff v2 registry 统一校验。Inspector 只暴露 Buff 引用；持续时间、叠层、来源和效果本体由 Buff definition 与命中上下文决定。

## 5. ModifierPipeline 和 Counter

Modifier 用于把条件和效果组合成可应用的运行时逻辑。

```csharp
public sealed class AlwaysCondition : IModifierCondition
{
    public bool Evaluate(ModifierContext context)
    {
        return true;
    }
}

public sealed class AddCounterEffect : IModifierEffect
{
    private readonly int _counterId;
    private readonly int _value;

    public AddCounterEffect(int counterId, int value)
    {
        _counterId = counterId;
        _value = value;
    }

    public void Execute(ModifierContext context)
    {
        context.Counters.AddCounter(_counterId, _value, this);
    }
}

var modifier = new ModifierBase(
    id: 200001,
    conditions: new IModifierCondition[] { new AlwaysCondition() },
    effects: new IModifierEffect[] { new AddCounterEffect(1, 1) });

var pipeline = new ModifierPipeline(attributeOwner);
pipeline.AddModifier(modifier);
var context = ModifierContext.Get();
context.Target = attributeOwner;
context.Counters = pipeline.Counters;
pipeline.ApplyAll(context);
ModifierContext.Push(context);
```

Counter 单独使用：

```csharp
var counters = new CounterStore();
counters.SetCounter(1, 0);
counters.AddCounter(1, 3);
int value = counters.GetCounter(1);
```

约定：

- `ModifierContext` 是显式上下文，不要在 Modifier 里读取全局单例。
- 缺失 Counter 默认返回 `0`；需要区分缺失时用 `TryGetCounter`。
- 配置驱动创建请看后面的 `ConfigModifierFactory<TConfig>`。

## 6. Entity / Ability / Target / Effect 最小闭环

Ability Slice 是当前游戏行为层的最小样板：实体释放技能、选择目标、执行效果，并回到 Attributes、Buffs 和 Events。

代码位置：

- 核心 API：`Packages/com.vincent.wgameframework/Gameplay/`
- Demo 装配：`Assets/Scripts/WGameFrameworkProject/Demos/Ability/RuntimeAbilitySliceRunner.cs`
- 测试：`Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Ability/AbilitySliceTests.cs`

Demo 运行：

1. 打开 `Assets/Scenes/RuntimeVerticalSlice.unity`。
2. 在 `RuntimeVerticalSliceRunner` 上勾选 `_useAbilitySlice`。
3. Play 后 Runner 会自动挂载 `RuntimeAbilitySliceRunner`。
4. OnGUI 会显示 Player / Enemy 属性、Buff 列表、Ability 事件和 AttributeChanged 事件。

最小调用链：

```csharp
using MxFramework.Gameplay;

var player = new RuntimeEntity(entityId: 1, teamId: 1, hpAttributeId: AttrHp);
var enemy = new RuntimeEntity(entityId: 2, teamId: 2, hpAttributeId: AttrHp);

player.AttributeStore.RegisterAttribute(AttrHp, 1000);
player.AttributeStore.RegisterAttribute(AttrAttack, 120);
player.AttributeStore.RegisterAttribute(AttrDefense, 20);

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

var context = new AbilityContext(player, new IRuntimeEntity[] { player, enemy });
AbilityCastResult result = ability.Cast(context);
```

当前边界：

- 核心类型位于 `MxFramework.Gameplay`，Demo 只负责装配示例数据和 Unity OnGUI 展示。
- 这是运行时垂直切片，不是 WGame Ability JSON 迁移。
- v0 不包含冷却、资源、吟唱、打断、动画、输入、范围检测。
- 详细接口见 `Docs/Interfaces/Gameplay.md`。

### 6.1 Component Runtime Showcase

Component Runtime Showcase 展示新的 component gameplay runtime v0：

```text
RuntimeHost
-> RuntimeCommandBuffer
-> GameplayRuntimeModule
-> GameplayComponentWorld
-> spawn / explicit target / ability rules / cleanup
-> event queue / hash / SaveState
```

代码位置：

- Runtime slice：`Assets/Scripts/WGameFrameworkProject/Demos/GameplayComponentRuntime/GameplayComponentRuntimeShowcase.cs`
- Unity runner：`Assets/Scripts/WGameFrameworkProject/Demos/GameplayComponentRuntime/GameplayComponentRuntimeShowcaseRunner.cs`
- UI：`Assets/UI/MxFramework/GameplayComponentRuntime/GameplayComponentRuntimeShowcase.uxml`
- 场景生成菜单：`MxFramework/Gameplay Component Runtime/Create Showcase Scene`
- 任务记录：`Docs/Tasks/GAMEPLAY_COMPONENT_RUNTIME_SHOWCASE_01.md`

当前交付等级是 `Runtime Slice`。本次提交不手写 `.unity` 场景；需要可打开场景时，在 Unity 中运行菜单：

```text
MxFramework/Gameplay Component Runtime/Create Showcase Scene
```

生成后打开：

```text
Assets/Scenes/GameplayComponentRuntimeShowcase.unity
```

操作：

1. `Spawn` 创建 hero / enemy。
2. `Cast Strike` 通过 request store 选择 explicit enemy target，扣 mana、伤害 enemy、启动 cooldown。
3. cooldown 中再次 `Cast Strike` 会输出 rejected event，不改变 hp / mana。
4. cooldown 到期后再次 `Cast Strike`，enemy hp 到 0。
5. `Cleanup` 标记 `PendingDestroy`，由 `GameplayLifecycleCleanupSystem` 清理 entity 和 registered stores。
6. `Save` / `Restore` 展示 ComponentWorld SaveState roundtrip。
7. `Run Full Flow` 自动执行完整展示链。

### 6.2 Config Driven Ability

`MxFramework.Config.Runtime` 可以把 `BasicAbilityConfig` 转成 `MxFramework.Gameplay.SimpleAbility`：

```csharp
using MxFramework.Config;
using MxFramework.Config.Runtime;
using MxFramework.Gameplay;

var table = new ConfigTable<BasicAbilityConfig>(BasicAbilityConfig.CreateSchema());
table.Add(new BasicAbilityConfig(
    id: 300001,
    nameText: new LocalizedTextKey("ability.strike.name"),
    descriptionText: new LocalizedTextKey("ability.strike.desc"),
    targetSelectorKind: AbilityTargetSelectorKind.SingleEnemy,
    effects: new[]
    {
        AbilityEffectConfig.DamageByAttackDefense(
            attackAttributeId: AttrAttack,
            defenseAttributeId: AttrDefense,
            hpAttributeId: AttrHp)
    }));

var factory = new ConfigAbilityFactory(table);
if (factory.TryCreate(300001, out IAbility ability, out string error))
{
    ability.Cast(new AbilityContext(player, new IRuntimeEntity[] { player, enemy }));
}
```

当前支持：

- `AbilityTargetSelectorKind.Self`：选择 caster 自身。
- `AbilityTargetSelectorKind.SingleEnemy`：选择第一个不同队伍且存活的候选实体。
- `AbilityEffectConfig.DamageByAttackDefense(...)`：命名传入 `attackAttributeId`、`defenseAttributeId`、`hpAttributeId`。
- Buff 申请不再通过 `AbilityEffectConfig` / `IBuffFactory`；组件能力应写入 `GameplayBuffRequestBuffer` 或提交 `ApplyComponentBuff` command。
- 旧的 `AbilityEffectConfig(kind, int[] parameters)` 仍保留兼容；新代码和编辑器接入应优先使用命名参数或静态工厂，避免依赖数组位序。

### 6.2.1 Ability Authoring Contract

AI、外部 JSON 和后续编辑器不要直接生成 `BasicAbilityConfig`。先生成工具层 `AbilityAuthoringContract`，经过结构化校验后再映射到运行时配置：

```csharp
using MxFramework.Config;
using MxFramework.Config.Runtime;
using MxFramework.Gameplay;

var contract = new AbilityAuthoringContract
{
    AbilityId = 300001,
    DisplayName = "Strike",
    Description = "Deal attack minus defense damage.",
    TargetSelectorKind = AbilityAuthoringTargetSelectorKind.SingleEnemy,
    Effects = new[]
    {
        AbilityAuthoringEffectContract.DamageByAttackDefense(
            attackAttributeId: AttrAttack,
            defenseAttributeId: AttrDefense,
            hpAttributeId: AttrHp)
    }
};

if (!AbilityAuthoringContractMapper.TryMap(
        contract,
        out BasicAbilityConfig config,
        out AbilityAuthoringValidationReport report))
{
    foreach (AbilityAuthoringValidationIssue issue in report.Issues)
    {
        // issue.Code is stable; issue.Message may be localized.
    }
}

var table = new ConfigTable<BasicAbilityConfig>(BasicAbilityConfig.CreateSchema());
table.Add(config);

var factory = new ConfigAbilityFactory(table);
if (factory.TryCreate(300001, out IAbility ability, out string error))
{
    ability.Cast(new AbilityContext(player, new IRuntimeEntity[] { player, enemy }));
}
```

工具上下文可通过 `AbilityAuthoringSchema.CreateSummary()` 获取字段中文名、类型、说明、允许值和稳定错误码。当前错误码包括 `MissingAbilityId`、`InvalidAbilityId`、`MissingDisplayName`、`UnknownTargetSelector`、`MissingEffect`、`UnknownEffectKind`、`MissingEffectParameter`、`InvalidAttributeId`、`InvalidBuffId`、`UnsupportedContractVersion`。

Demo 运行：

1. 打开 `Assets/Scenes/RuntimeVerticalSlice.unity`。
2. 在 `RuntimeVerticalSliceRunner` 上勾选 `_useAbilitySlice`。
3. 勾选 `_useConfigDrivenAbility` 时，Ability Runner 使用 `BasicAbilityConfig -> RuntimeAbilityConfigResolver -> ConfigAbilityFactory`；不勾选时保留硬编码 `SimpleAbility` 示例。

边界：这不是 WGame Ability JSON 迁移，也不包含冷却、资源、吟唱、打断、动画、输入、范围、碰撞、寻路或弹道。

### 6.2.2 Ability Package Authoring CLI

CharacterTest 火球使用 `Tools/MxFramework.Authoring/samples/ability-character-test/config/ability_authoring.json` 作为能力包样例。该样例声明 ability、通用 `hitEvent.damage` 伤害数据、impact VFX、action reference、`projectileDefinitions`、`projectileSpawnProfiles` 和 `projectilePresentationBindings`，用于生成 Unity 运行时读取的 JSON catalog。

```powershell
dotnet run --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli -- `
  ability compile `
  --package Tools/MxFramework.Authoring/samples/ability-character-test `
  --out Temp/AbilityCharacterTest

dotnet run --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli -- `
  ability import-unity `
  --package Tools/MxFramework.Authoring/samples/ability-character-test `
  --project-root . `
  --dry-run
```

`ability import-unity` 默认写入：
- `Assets/MxFrameworkGenerated/Abilities/ability_catalog.json`
- `Assets/MxFrameworkGenerated/Abilities/ability_action_references.json`
- `Assets/MxFrameworkGenerated/Vfx/vfx_definitions.json`
- `Assets/MxFrameworkGenerated/Vfx/vfx_trigger_maps.json`
- `Assets/MxFrameworkGenerated/Combat/Projectiles/projectile_definitions.json`
- `Assets/MxFrameworkGenerated/Combat/Projectiles/projectile_spawn_profiles.json`
- `Assets/MxFrameworkGenerated/Combat/Projectiles/projectile_presentation_bindings.json`

Projectile definition catalog 字段固定为 `definitionId`、`stableId`、`physicalEventsEnabled`、`motion.kind`、`motion.speedPerFrame`、`motion.lifetimeFrames`、`motion.arcHeight`、`motion.gravityPerFrameSquared`、`motion.maxLaunchAngleDegrees`、`motion.maxLaunchAngleTangent`、`motion.lateralAmplitude`、`motion.lateralCycles`、`motion.bezierControlOffsetA`、`motion.bezierControlOffsetB`、`motion.speedProfile.kind`、`collision.radius`、`collision.layerMask`、`collision.stopOnFirstAcceptedImpact`、`collision.stopOnBlockedImpact`、`collision.maxAcceptedImpacts`、`collision.hitSameTargetPolicy`、`impact.hitEvent`、`impact.areaHitEvent`、`impact.policyKind`、`impact.areaRadius`、`impact.areaLayerMask`、`impact.triggerOnExpire` 和 `impact.effectTimelineId`。`physicalEventsEnabled` 缺失时默认为 false；开启后 projectile runtime 只输出 `CombatProjectilePhysicalEvent` 事实，半径直接复用 `collision.radius` 或 `impact.areaRadius`，强度直接复用 `impactPower`，具体推物体、吸附或径向冲量仍由项目 consumer 解释。`impact.hitEvent.damage` 使用 `damageTypeId`、`baseDamage`、`impactPower`、`launchPower`、`tags`、`analysisCategory` 表达 projectile 伤害属性，direct hit 必须显式配置；`areaHitEvent.damage` 可省略并继承 direct hit damage。`impact.damageProfileId`、`hitEvent.damageProfileId` 和旧顶层伤害字段会导入失败。`impact.effectTimelineId` 可在命中/过期时以 projectile 当前点作为 captured anchor 启动 Ability Effect Timeline，例如 CharacterTest `Fireball Explosion` 首爆由 `AreaPulseOnImpact` 结算，二爆由 `character_test.fireball_explosion.afterblast` 在第 120 帧执行。`motion.kind` 支持 `Linear`、`ArcToTarget`、`BoomerangReturn`、`SwayForward`、`HomingToTarget`、`BezierToTarget` 和 `BallisticArcToTarget`；旧 JSON 缺字段时按 `Linear` 导入，`ArcToTarget` 使用 `origin -> targetPoint`、本次发射 effective lifetime 和 `arcHeight` 采样确定性抛物线，锁定目标时 effective lifetime 由 `ceil(distance / speedPerFrame)` 计算，保证弧线终点精确等于发射时解析出的目标点；`BallisticArcToTarget` 使用 `speedPerFrame` 作为最大初速度、`gravityPerFrameSquared` 作为确定性游戏重力，按目标点和 `maxLaunchAngleTangent` 限制解本次实际初速度 / 出射方向，`maxLaunchAngleDegrees=0` 表示自动使用当前高度差下的最远射程角（同高度 45 度），`lifetimeFrames` 只作为安全上限；`BoomerangReturn` 以目标点为折返点，`SwayForward` 使用 `lateralAmplitude` / `lateralCycles` 做确定性三角波侧向摆动，`HomingToTarget` 使用 spawn request 保存的 tracking target id 每帧重新解析 deterministic target center 并更新 direction / target point，目标无效时沿上一帧方向继续；`BezierToTarget` 使用 `bezierControlOffsetA/B` 作为投射物局部坐标控制点偏移（X=右、Y=上、Z=前），按三次贝塞尔曲线从 origin 采样到 target point。`motion.speedProfile.kind` 支持 `Constant`、`EaseInQuadratic`、`EaseOutQuadratic` 和 `EaseInOutSmoothstep`；旧 JSON 缺字段时按 `Constant` 导入，速度曲线只改变生命周期内路径进度分配，不改变总射程、碰撞规则或伤害配置。Projectile spawn profile catalog 字段固定为 `stableId`、`projectileStableId`、`origin.kind`、`origin.socketId`、`origin.localOffset`、`origin.fallbackRootOffset`、`aim.kind`、`aim.targetHeightOffset`、`aim.fallbackKind` 和 `aim.maxRange`。`aim.kind` 支持 `CasterForwardRange`、`LockedTargetCenter` 和 `LockedTargetOrCasterForwardRange`；CharacterTest 的 locked target 解析只读取 runtime deterministic target lock state。`LockedTargetOrCasterForwardRange` 在无锁定、友方、死亡或超距时回退正前方射程；`LockedTargetCenter` 是严格锁定，无有效目标时拒绝 spawn。CharacterTest 约定 `SendGameplayRequest.requestId` 先匹配 spawn profile `stableId`，再由 `projectileStableId` 引用 projectile definition；generated catalog 为空时才回退 embedded sample spawn profile / definition。

Projectile motion categories are `Kinematic` (`Linear`, `BoomerangReturn`, `SwayForward`, `HomingToTarget`, `BezierToTarget`), `DesignedArc` (`ArcToTarget`, driven by `motion.arcHeight`), and `BallisticArcToTarget` (gameplay-gravity arc). `BallisticArcToTarget` uses `motion.speedPerFrame` as max initial speed, `motion.gravityPerFrameSquared` as resolved gameplay gravity, and the resolved target point plus launch-angle cap to solve this shot's launch vector and actual launch speed. Same-height uncapped maximum range is derived from `speedPerFrame^2 / gravityPerFrameSquared`; explicit `maxLaunchAngleDegrees` / `maxLaunchAngleTangent` values below the range-optimal angle reduce reach. `aim.maxRange` is ignored/normalized to zero for ballistic motion, and `lifetimeFrames` is only a safety expiry cap. Other motion kinds normalize `gravityPerFrameSquared` and max launch angle fields to zero.

CharacterTest 技能按实现类型的模块链路、数据入口、权威边界和示例映射集中维护在 `Docs/Guides/CHARACTER_TEST_SKILL_IMPLEMENTATION_PATTERNS.md`。

在 Unity 的 `MxFramework/Character/Character Action Authoring` 窗口中，从“共享资源 → Projectile”编辑 projectile request。普通编辑路径应使用受限输入：Socket 从角色资源包 `geometry/sockets.json` / generated `character_geometry_binding.json` / `knownSocketIds` 候选选择；飞行 VFX Resource 从 authoring/generated catalog 候选选择；LayerMask 使用 Unity mask field。Damage Profile 和 hit presentation Profile 不作为普通编辑项暴露。运动形态可在“直线 / 抛物线 / 折返 / 左右摆动 / 追踪目标”之间切换；抛物线显示“弧高 m”字段，摆动显示“摆动幅度 m”和“摆动周期”，追踪目标显示 tracking target / fallback 诊断。内嵌预览使用隔离的 `PreviewRenderUtility` 场景观察角色、VFX 和 Projectile 路径；需要拖动射程、半径、爆炸范围、弧高等 Handles 时，通过顶部“场景编辑”进入 SceneView 独占编辑。SceneView 路径会显示生命周期进度刻度，锁定目标弧线会显示目标点、距离和本次 effective lifetime；无锁定目标时显示正前方 fallback 终点和中文诊断。速度曲线可在“匀速 / 加速 / 减速 / 先慢后快再慢”之间切换。选择角色包中存在但 `knownSocketIds` 未声明的 socket 时，窗口会把该 socket 自动补入 ability authoring 的校验清单并同步 generated JSON。

### 6.2.3 CharacterTest Ability Hotbar

CharacterTest has a local 1-9 ability hotbar for testing; it is not a generic framework hotbar. `CharacterTestAbilityHotbarRuntime` builds choices from `AbilityDefinitionRegistry`; defaults are 1 Fireball, 2 Fireball Burst, 3 Flame Lance, 4 Fireball Lob, 5 Fireball Surge, 6 Fireball Return, 7 Tornado, 8 Guard Aura, and 9 Fireball Explosion. Fireball Sway, Area Confinement, Beam Lance, Channel Beam, Laser Lance, Chain Lightning, Poison Bottle, Stun Bolt, Meteor Barrage, Sword Wave, Dash Strike, Hook Chain, Arc Mine, and Rune Turret remain bindable and the same ability can be assigned to multiple slots.

Detailed skill implementation notes are intentionally kept out of this usage chapter. For projectile, beam, chain, trap, aura, shield, zone, mechanic-source, and physical-interaction patterns, read `Docs/Guides/CHARACTER_TEST_SKILL_IMPLEMENTATION_PATTERNS.md`.

Combat HUD 使用 Composer-owned FairyGUI `MxCharacterTestCombatHud/CombatHudPanel` 常驻显示 1~9 槽位、短名、空槽、选中态和 cooldown remaining。暂停菜单 `MxCharacterTestUi/MainMenuPanel` 的 Abilities 按钮会打开全屏 `MxCharacterTestAbilityUi/AbilityBindingPanel`；面板支持选择槽位、选择能力、Clear、Reset Defaults、Save 和 Cancel。Save 写入 `Application.persistentDataPath/MxFramework/CharacterTest/ability_hotbar.json`，Cancel 丢弃本次 working copy。

数字键 1~9 只由 `CharacterTestLocalInputCommandModule` 的本地输入 adapter 在 Gameplay context 下采样；UI、Rebinding、Cutscene 等独占 context 会阻断。它们不会加入 `DefaultInputBindingCatalog`，也不会写入 `.inputactions` 或输入重绑定 override。数字键或 UI Cast 都提交 CharacterTest 专用 runtime command `CastAbilitySlot = 901021`，payload 固定为 `targetId=controlled entity`、`payload0=slot 1-9`、`payload1=abilityId`、`payload2=schemaVersion 1`；动作系统再按 abilityId 动态解析 action intent 并复用现有 cooldown / ability runtime。

### 6.3 Runtime Config Change Handling

Runtime 配置变更采用重建语义，不做 Ability 热替换，也不回溯修改已挂载 Buff / Modifier：

```csharp
var resolver = new RuntimeAbilityConfigResolver(
    configs: registry,
    sourceName: "RuntimeAbilitySliceDemoData",
    changeSet: changeSet);

if (resolver.TryCreate(300001, out IAbility rebuiltAbility, out string error))
{
    rebuiltAbility.Cast(new AbilityContext(player, new IRuntimeEntity[] { player, enemy }));
}

string summary = resolver.CreateSummary();
```

约定：

- `TryCreate` 每次从当前 `IConfigProvider` 创建新的 Ability；调用方负责替换自己持有的 Ability 引用。
- 旧 Ability 不会在原对象上替换 selector / effects / parameters。
- 已挂载 Buff / Modifier 保持挂载时捕获的运行时状态；配置变更只影响后续新创建实例。
- `RuntimeConfigChangeSummary` 可展示 source、policy、changed ids、rebuilt ids、failed ids 和错误摘要。

### 6.3 Gameplay Diagnostic Snapshot

`GameplayDiagnosticSnapshotBuilder` 把运行中的 Entity / Ability / Buff / Modifier / Event 状态整理成纯 C# 只读快照，适合 EditMode 测试、Demo 摘要、AI 上下文和后续编辑器读取。

最小用法：

```csharp
using System.Collections.Generic;
using MxFramework.Attributes;
using MxFramework.Gameplay;

var abilityEvents = new List<AbilityEvent>();
var attributeEvents = new List<AttributeChangedEvent>();

player.AbilityEvents.Subscribe(e => abilityEvents.Add(e));
player.Store.OnAttributeChanged.Subscribe(e => attributeEvents.Add(e));
enemy.Store.OnAttributeChanged.Subscribe(e => attributeEvents.Add(e));

AbilityCastResult result = ability.Cast(new AbilityContext(player, new IRuntimeEntity[] { player, enemy }));

var builder = new GameplayDiagnosticSnapshotBuilder();
GameplayDiagnosticSnapshot snapshot = builder.Build(
    sourceName: "runtime-ability-slice",
    abilitySource: "BasicAbilityConfig -> ConfigAbilityFactory",
    entities: new[] { player, enemy },
    attributeIds: new[] { AttrHp, AttrAttack, AttrDefense },
    lastCastResult: result,
    abilityEvents: abilityEvents,
    attributeEvents: attributeEvents);

int entityCount = snapshot.Entities.Count;
int targetId = snapshot.LastTargetEntityIds.Count > 0
    ? snapshot.LastTargetEntityIds[0]
    : 0;
```

约定：

- Builder 不订阅事件；调用方负责维护 `abilityEvents` / `attributeEvents` 日志。
- `attributeIds` 是白名单，决定每个 Entity 输出哪些属性。
- 空实体、空属性、空事件日志应返回空集合，不要求 Unity 场景或 MonoBehaviour。

### 6.4 UI Toolkit Runtime Showcase

现有 UI Toolkit Runtime Showcase 是 repository validation / diagnostics 入口。正式 player-facing UI 与 Showcase / Debug / Editor 的分工见 [UI Toolkit 接口](Interfaces/UI.Toolkit.md)；正式界面创作操作见 [FairyGUI UI Composer 使用指南](Guides/FAIRYGUI_UI_COMPOSER_USER_GUIDE.md)。本节按 2026-10-04 源码和静态资产快照说明启动与配置，不代表本次已运行 Play Mode 或重新验收 M1-M6。

当前 Ability Showcase 的 HUD 动态装配链为：

```text
RuntimeAbilitySliceRunner
  -> RuntimeAbilitySliceShowcaseUi
  -> MxRuntimeHudController
  -> GameplayShowcase.uxml / GameplayShowcase.uss
```

实际场景装配规则：

```text
RuntimeVerticalSliceSceneConfig asset
  -> RuntimeVerticalSliceBootstrap
  -> dynamically creates RuntimeVerticalSliceRunner
  -> dynamically mounts RuntimeAbilitySliceRunner
  -> dynamically mounts MxRuntimeHudController
  -> dynamically mounts RuntimeAbilitySliceShowcaseUi
```

`RuntimeVerticalSlice.unity` 中保留 Bootstrap 舞台入口；当前静态场景未预挂 Runner、PreviewTarget 或 PreviewCaster。场景 Bootstrap 的 `_config` 引用 `Assets/Config/MxFramework/Demo/RuntimeVerticalSliceSceneConfig.asset`，Play 时按它动态创建 `RuntimeVerticalSliceRuntime`、ApplyConfig，再挂 Ability Runner / HUD / UI 适配器。要持久调整该场景，在 Project 中选中这份配置资产，通过 Inspector 编辑并确认 Bootstrap 的引用；运行时生成 Runner 的自定义 Inspector 可查看 Showcase 开关、Patch / Mod 相对路径、初始值和诊断设置，不应把 Play Mode 修改当持久资产保存。

手测方式：

1. 打开 `Assets/Scenes/RuntimeVerticalSlice.unity`。
2. 在 Project 选中 `Assets/Config/MxFramework/Demo/RuntimeVerticalSliceSceneConfig.asset`，通过 Inspector 查看配置；确认场景 `RuntimeVerticalSliceBootstrap` 的 `_config` 引用该资产。
3. 在该资产确认 `_autoStartInScene`、`_useAbilitySlice`、`_enableShowcaseUi` 已开启，`_sceneName` 与场景名一致；Play 后可在生成的 Runner 自定义 Inspector 查看生效字段。
4. Play 后会看到 UI Toolkit HUD，展示 Player / Enemy、Mini Game Feedback、Buff 摘要、Ability source、Config summary、Snapshot summary、Diagnostic View 和 Event Log。
5. Config / Patch / Rebuild 面板可手动执行 `Load Patch`、`Load Mod Package`、`Rebuild Ability`、`Compare Old/New`。对比结果会显示旧 Ability 对象保持旧配置效果，新重建 Ability 使用当前配置效果。
6. Diagnostic View 可在 `Summary` / `Technical` 间切换，分区显示 Entity、Ability Events、AttributeChanged Events、Config Source、last cast failure 和 config errors；无错误时显示 `No runtime errors`。
7. Mini Game Feedback 会显示 Player / Enemy HP 状态徽章、Buff 层数和剩余时间、技能按钮可用反馈，以及最近动作摘要。

配置与退役边界：

- `RuntimeVerticalSliceConfigWindow`、其 Scene Config 菜单、旧 `MxPreviewSceneTargetProfile` 与 `ScenePreviewWorld` 已由提交 `33da25c110d2d8e1edb33507d1c15d054df38a9b`（2026-06-13，Retire legacy Buff authoring editor）删除；当前仓内没有对应窗口/菜单实现，不再用旧菜单作为操作步骤。现存 Runner 自定义 Inspector 使用 IMGUI，不能描述成该 UI Toolkit EditorWindow 的同名替代。
- Bootstrap 优先用显式 `_config`；未绑定或场景没有 Bootstrap 时，`LoadDefault()` 创建运行时默认配置，不自动按 `DefaultAssetPath` 读取磁盘资产。启动还要求 AutoStartInScene、SceneName 匹配，且不重复创建已存在的 Runner。只修改配置资产而没有场景引用，不保证它会生效。
- 配置资产继续拥有启动、Ability/config-driven/HUD/legacy OnGUI、Patch/Mod/LoadPlan/loadout 相对路径、初始 HP/Attack/Defense 和诊断输出；默认 legacy OnGUI 关闭，Ability Showcase 开启 HUD 时不叠加它。HUD 的 PanelSettings/UXML/USS/font 引用随配置传给 Controller。非 Ability 分支仍可保留 legacy OnGUI，不宣称所有旧模式都已迁移。
- `Assets/Config/MxFramework/Preview/RuntimeVerticalSlicePreviewTargets.asset` 仍存在，其中 TestTarget/TestCaster、初始数值、resetOnPreviewRun、showOverlay=false、createRuntimeTarget 是旧序列化内容；其旧 script GUID 当前未找到可解析的 `.meta`，旧自动生成 target 的 consumer 已删除，不能把它当现行可编辑/可用 profile。保留资产，后续迁移或移除由资产所有者另行决定；本次未改场景、资产或 GUID。
- 部分旧手测命令仍有可见入口，但不是完整 Buff v2 验收：当前 Ignite / ApplyBuff / Tick 映射到旧 Ignite command，UI 显示 retired / component Buff v2 提示；不能由按钮存在推断旧 Ignite/Buff 功能仍有效。是否修复或替换这些 legacy Showcase 命令需要实现决策，本次只注明范围，不改变按钮或 runtime。
- 原任务中中文分组、tooltip、校验提示、打开场景/定位资产按钮属于已退役窗口的历史结果；本节不把这些 UI 保证转授给默认资产 Inspector。实际 Play Mode 外观、持久配置生效和未完成 Phase 12 的验收范围仍需另行验证；当前战斗 HUD、Runtime AI Lab 和正式 FairyGUI 界面不在本次修改范围。

### 6.4.1 FairyGUI Runtime UI Adapter

FairyGUI 是正式 runtime game UI 的 preferred adapter；UI Toolkit 继续承担 Debug UI、Editor 工具、现有 showcase 和轻量验证 surface。这个结论来自 M3 `MxRuntimeHud` 竖切和 M4 manifest/resource validation，不表示所有 UI 已经迁移。

当前可用边界：

- `MxFramework.UI` 是 noEngine UI core，保存 `MxUiViewId`、`MxUiViewContract`、`MxUiViewDescriptor`、`MxUiLayer`、`IMxUiNavigator`、`MxUiOpenResult` 和 `MxUiCommand`。
- `MxFramework.UI.FairyGUI` 是 Unity/FairyGUI adapter，负责 package bytes 加载、package ref-count、component 创建、typed ViewModel binder 和 button -> `MxUiCommand` 映射。
- `MxFairyGuiTooltipService` 是 player-facing FairyGUI 的共享 Tooltip 生命周期与定位策略：普通 Tooltip 由 hover 显示、pointer exit 隐藏，点击目标控件不会把它当弹窗关闭；同一目标 rebind 时原位刷新文本。需要玩家持续阅读时可显式 pin，退出 pin 后恢复 transient 行为。自动定位会让宽控件优先显示在下方或上方，让方形图标控件优先显示在朝屏幕内部的一侧；空间不足时翻边并限制在 root safe margin 内。业务 binder 只绑定文案和可选 placement，不直接使用 `UIConfig.tooltipsWin` 或自行拼接屏幕坐标。
- `MxFramework.UI.FairyGUI.Manifest` 是 noEngine validation sidecar，可读取 framework-owned FairyGUI source XML、package bytes 和 `ResourceCatalog`，检查 schema、component、controls、commands 和 resource drift。
- `FGUIProject/assets/MxRuntimeHud` 是当前已验证的 Runtime Ability HUD FairyGUI source package；发布输出是 `Assets/Bundles/FGUI/MxRuntimeHud/MxRuntimeHud_fui.bytes`。
- `FGUIProject/assets/MxStoryDialog` 是 Story dialog / choice FairyGUI source package；发布输出是 `Assets/Bundles/FGUI/MxStoryDialog/MxStoryDialog_fui.bytes`。
- `RuntimeAbilitySliceFairyGuiHudShell` 是当前 Runtime Ability HUD 的 FairyGUI 组合壳：复用生成 manifest、`MxFairyGuiNavigator`、resource bridge 和 `RuntimeAbilitySliceUiCommandSink`，通过 `Open` / `Refresh` / `Close` 驱动 HUD。
- `StoryRuntimeFairyGuiDialogShell` 是 Story vertical slice 的 opt-in FairyGUI dialog 组合壳：ViewModel 来自 `StoryRuntimeVerticalSliceSnapshot`，按钮发出 `MxUiCommand` 后映射到 `StoryRuntimeCommandFactory.CompletePresentation` / `SelectChoice`。
- `CharacterTestFairyGuiStoryDialogHost` 是 CharacterTest 的 player-facing Story dialog 组合壳：它复用已发布的 `MxStoryDialog_fui.bytes`，但在 `MxFramework.Game.CharacterTest` 内声明自己的 view id、binder 和 command sink，不依赖 `MxFramework.Demo.FairyGUI`。
- `CharacterTestFairyGuiMainMenuHost` 是 CharacterTest 的 player-facing 暂停菜单组合壳：它加载 Composer-owned `MxCharacterTestUi/MainMenuPanel`，按钮只发出 `MxUiCommand`，由 internal main menu coordinator 的 `ICharacterTestMainMenuController` 实现恢复、离线重启和按需打开 NetworkPanel，并通过 ability/settings panel host callback 打开 AbilityBindingPanel 或 SettingsPanel；菜单打开时 runtime 默认停住，该策略不再从 `CharacterTestSceneProfile` 暴露。
- `CharacterTestFairyGuiAbilityBindingPanelHost` 是 CharacterTest 专用能力绑定面板组合壳：它加载 Composer-owned `MxCharacterTestAbilityUi/AbilityBindingPanel`，只编辑 `CharacterTestAbilityHotbarRuntime` working copy，Save 才写入 `ability_hotbar.json`，Cancel 不修改当前绑定。
- `MxFairyGuiGuidancePresenter` 是通用正式提示 Presenter：它通过 Composer-owned `MxPlayerGuidanceUi` 在 `Hud` layer 显示一个固定 Guidance Card、在 `Toast` layer 显示最多两条 Notice；初始化后稳态 `Tick` 为 NoAlloc，两个 root 都不拦截输入。CharacterTest P3.4 已把教学序列接入 Mx Level Editor 的独立`教学引导`模块：正式数据是共享 `mx.level.authoring.v3` envelope 中的 `characterTest.guidance`；作者可从空白步骤或六个玩家意图预设开始，在三栏界面完整编辑有序步骤，并在五类白名单 typed completion condition 间安全切换。缺失模块显示根关闭零步骤，未完成草稿在任意启停组合下都阻止保存；右侧预览只检查语义结构。保存通过统一协调器同时 upsert payload、编译 GameManager `CharacterTestGuidanceSceneCache` 并保存 Scene；未知 completion type 可显式修复，未知 module schema 只读，cache/hash/schema 不一致会在 Play/build preflight 与 Runtime 显式 fail closed，不回退硬编码六步。
- `CharacterTestFairyGuiGuidanceHost` 默认在普通 CharacterTest 会话中启用并使用 `TopCornersOccupied` 布局，只从 Scene cache 取得正式六步 plan：移动停步、跳跃落地、相机切换、目标锁定、取消锁定和普通攻击伤害。Sequence Coordinator 唯一拥有步骤状态，typed observers 读取确定性角色状态、`CameraModeChanged`、`TargetLockChanged` / `TargetLockRejected` 与 Combat `DamageTelemetryRow`，Snapshot Mapper 只生成既有 Guidance DTO。每一步按自己的 `inputActionId` 通过 `InputBindingDisplayService` 显式读取 binding index `0`；binding revision 会刷新当前提示，但不会仅因最近活动设备改变而自动切换 binding。`Gameplay/GuidancePrevious` / `GuidanceNext`（默认 `[` / `]`）只做有界导航，不修改玩法权威或伪造完成。目标锁定文案已从 Combat HUD feedback 原子移除，避免同一事件两个出口；`ShowDevelopmentPreview()` 仍只用于人工视觉验收并明确标注预览。
- `CharacterTestFairyGuiArenaSetupPanelHost` 是 `CharacterTestArenaScene` 的可玩测试入口：它加载 `MxCharacterTestHud/ArenaSetupPanel`，只编辑当前 Arena 会话配置，`开始战斗` 通过 `CharacterTestArenaSessionController` 重建 clean offline session 并锁定 Runtime AI Planner behavior selection phase；战斗中只显示只读状态和重置入口，不做行为组编辑、JSON 注入或战斗中策略切换。
- `CharacterTestFairyGuiNetworkPanelHost` 现在支持默认自动打开和 main-menu on-demand 两种模式；main-menu 模式下 active session 禁用 Back，使用 Disconnect 离开。Host / Join 成功后停留在全屏 NetworkPanel；Room 模式由成员 Ready 后 Host 点击 Enter Game，随后各实例在本地 gameplay handshake 验通后自动关闭面板并恢复 gameplay tick。面板是 Direct LAN / LAN Room 联机大厅，Host / Join / Disconnect 仍只通过 `ICharacterTestNetworkSessionController` 控制组合根，mode tab 只改变提交的 `CharacterTestNetworkSessionOptions.LaunchMode`。
- `MxFairyGuiRuntimeCatalog` 是 runtime composition catalog：组合根显式注册本场景允许的 FairyGUI views、package descriptors 和 typed binders，再用 `MxFairyGuiRuntimeShellComposition.CreateNavigator(...)` 创建局部 navigator。它不是全局 UIManager，也不自动发现所有 view。
- `MxFairyGuiRuntimePreloadSurface` 可从 catalog 生成 preload plan 并通过 `ResourcePreloadService` 预热 package bytes / required resources；`MxFairyGuiRuntimeCatalogDiagnostics` 和 `MxFairyGuiRuntimePreloadDiagnostics` 暴露 view/package/resource count、missing package/resource、preload requested/loaded/failed 等只读 runtime diagnostics。
- UI Toolkit `MxRuntimeHudController` 继续作为 Ability Showcase / diagnostics surface。FairyGUI Runtime HUD 是正式玩家 HUD 方向，本切片不替换 UI Toolkit 诊断面板。

局部组合示例：

```csharp
var catalog = new MxFairyGuiRuntimeCatalog();
RuntimeAbilitySliceFairyGuiHudComposition.Register(catalog, abilityUiCommandSink, textProvider);
StoryRuntimeFairyGuiDialogComposition.Register(catalog, storyUiCommandSink, textProvider);

using var preload = new MxFairyGuiRuntimePreloadSurface(new ResourcePreloadService(resourceManager));
preload.Preload(catalog, "runtime.ui.shell");

MxFairyGuiNavigator navigator = MxFairyGuiRuntimeShellComposition.CreateNavigator(
    resourceManager,
    catalog,
    host,
    layerHost,
    inputBridge,
    new MxFairyGuiViewTransitionController());
```

产品化 opt-in shell：

```csharp
MxFairyGuiProductRuntimeShell shell = MxFairyGuiProductRuntimeComposition.CreateShell(
    resourceManager,
    abilityHudCommandTarget,
    storyDialogCommandTarget,
    layerHost: layerHost,
    inputBridge: inputBridge,
    textProvider: textProvider);

shell.RuntimeHud.Open(RuntimeAbilitySliceHudViewModelBuilder.Build(abilityRunner));
shell.StoryDialog.Open(StoryRuntimeVerticalSliceFairyGuiViewModelBuilder.Build(storySnapshot));
```

`MxFairyGuiProductRuntimeComposition` 只注册当前场景显式 opt-in 的
Runtime Ability HUD 和 Story Dialog。它复用 generated manifest、
package bytes `ResourceKey`、typed binder、`MxUiCommand` sink、local catalog
diagnostics 和 preload plan；不会创建全局 UIManager，也不会自动发现或接管其他
UI surface。组合根可以先调用 `CreateCatalog(...).CreateDiagnostics(...)`
和 `CreatePreloadPlan(...)`，再按场景需要 open / refresh / close 各 view。

Runtime Ability HUD 与 Story Dialog 当前是 FairyGUI formal runtime path；
UI Toolkit `MxRuntimeHudController`、Story vertical-slice UXML、Debug UI overlay
和 Editor 工具仍是 diagnostics / showcase / fallback surface。场景不得把
FairyGUI HUD 与 UI Toolkit HUD 同时当作默认玩家 HUD；如果两者共存，必须把
一个声明为 opt-in diagnostics 或 fallback。

本地校验：

```bash
dotnet run --project Tools/MxFramework.NoEngineTests/FairyGUI.Manifest.Tests/FairyGUI.Manifest.Tests.csproj
dotnet run --project Tools/MxFramework.NoEngineTests/FairyGUI.Manifest.Tests/FairyGUI.Manifest.Tests.csproj -- --check-generated
dotnet run --project Tools/MxFramework.NoEngineTests/FairyGUI.Manifest.Tests/FairyGUI.Manifest.Tests.csproj -- --review-report
dotnet build MxFramework.UI.FairyGUI.csproj /nr:false -m:1 -v:minimal
dotnet build MxFramework.UI.FairyGUI.Manifest.csproj /nr:false -m:1 -v:minimal
dotnet build MxFramework.Demo.FairyGUI.csproj /nr:false -m:1 -v:minimal
dotnet build MxFramework.Tests.UI.FairyGUI.csproj /nr:false -m:1 -v:minimal
```

约定：

- FairyGUI view 只消费 ViewModel 并发出 `MxUiCommand`；不得直接读写 Gameplay、Combat、Story 的权威状态或全局单例。
- `MxFairyGuiViewTransitionController` 是 adapter-level show/hide transition coordinator。`Close` / `Dispose` 会先 cancel 当前 transition，再释放 input scope、component、package scope 和 FairyGUI package ref，避免 close during transition 泄漏。
- Keyboard / gamepad focus intent 必须由 composition root 从 framework input 转成 `MxFairyGuiFocusInputBridge.Process(...)` 调用；view 不 poll devices。Binder 可用 `MxFairyGuiFocusNavigationMetadata` 声明 default focus 和 next/previous 顺序，submit 只触发当前 focused button 的正常 `onClick` -> `MxUiCommand` 流程。
- `MxFairyGuiFocusInputBridge` 的输入优先级为 top popup -> top modal -> 当前 surface。Popup 打开时，cancel 先通过 `IMxFairyGuiPopupService.HideTop(...)` 关闭轻量弹层；没有 Popup 时，modal cancel 才通过 `MxFairyGuiCancelCommandBridge` 发出 `ui.cancel` 并经过 `MxFairyGuiModalCommandGate`。
- Tooltip、Popover、Modal 的生命周期不可混用：Tooltip 只承载悬停/焦点说明；Popover 用于不暂停游戏的轻量选择和上下文操作，必须通过 `MxFairyGuiPopupService` 使用 FairyGUI 原生 Popup 栈，内部点击保持、外部点击关闭且不吞掉目标点击；Modal 才用于阻断式确认或流程。禁止再用透明全屏 `GGraph` 模拟 Popover。完整契约见 `Docs/Interfaces/UI.FairyGUI.md`。
- `*_fui.bytes` 是 FairyGUI Editor publish output，不手写。需要重新发布时打开 `FGUIProject/FGUIProject.fairy`，使用 `WGameFramework/Publish Runtime HUD Package`、`WGameFramework/Publish Story Dialog Package`、`WGameFramework/Publish Character Test HUD Package` 等 helper 菜单。
- 新 FairyGUI package 必须有 manifest 或 generator output，且至少通过 package source XML、component XML、package bytes header、catalog entry、required controls 和 command button 校验。
- Runtime HUD manifest 生成输出可用
  `dotnet run --project Tools/MxFramework.NoEngineTests/FairyGUI.Manifest.Tests/FairyGUI.Manifest.Tests.csproj -- --write-runtime-hud-manifest`
  刷新；Story dialog manifest 生成输出可用
  `dotnet run --project Tools/MxFramework.NoEngineTests/FairyGUI.Manifest.Tests/FairyGUI.Manifest.Tests.csproj -- --write-story-dialog-manifest`
  刷新。也可以用 `--write-manifests` 批量刷新当前 registry 中的 Runtime HUD 和 Story Dialog 生成输出。默认 noEngine suite 和 `--check-generated` 会在任一生成输出 stale 时失败。
- `--check-generated` 走 batch-friendly manifest registry，一次覆盖 Runtime HUD 与 Story Dialog 两个 package/view；`--review-report [path]` 输出 deterministic review report，列出每个目标的 generated path、package/view 映射、controls/localization/commands 计数和 diagnostics，适合 CI artifact 或 PR 审阅。
- UI Art Resource Library 当前主入口是 `Tools/MxFramework.FairyGuiArtSlices/web/?library=MxUiFoundation`。它维护 `ArtSource/UIKits/MxUiFoundation/ui-art-resources.json`、资源库 PNG、推荐 FairyGUI 参数和替换约束，并能为 package.xml-backed 目标包生成只读接入计划及稳定 JSON/Markdown 配方。接入计划会报告建议路径、已有 image id、内容漂移、参数漂移和发布要求，但不会写 `package.xml`、component XML、atlas 或 `*_fui.bytes`；真实导入、组件接线和发布仍由 FairyGUI Editor/helper plugin 完成。完整操作、状态解释、Development Agent 使用规则与排障见 `Docs/Guides/UI_ART_RESOURCE_LIBRARY_GUIDE.md`。旧 `MxCharacterTestHud` Art Slice scanner 继续通过 `--check-art-slices`、`--write-art-slice-report` 和 `--review-report` 提供包内使用点审计。

Package Editor 不内置 WGame `Tools/` authoring sample 路径。需要 Projectile/CharacterAction 默认项目输入的 Consumer，应在自己的 Editor assembly 实现 `IMxFrameworkEditorProjectPathProvider`，并通过 `MxFrameworkEditorProjectPaths.Register` 注册 `AbilityAuthoringJson`、`ProjectileAuthoringPackage` 和 `ProjectileSourceDirectory`。未注册时相关窗口保持 unavailable/empty，不回退到 WGame 仓库路径；WGameFramework 主工程的实现位于 `Assets/Scripts/WGameFrameworkProject/Editor/WGameFrameworkEditorProjectPathProvider.cs`。
- 使用语义资源制作真实 FairyGUI 界面时，打开 `Tools/MxFramework.FairyGuiUiComposer/web/`。当前 Authoring API v5 统一编辑 `MxUiFoundation` 资源、标准 Button / Composite 配方和 allowlisted Screen Blueprint，并为 Blueprint v3 List 节点提供新旧客户端隔离；已发布并接入运行时的十一个目标包括 `MxUiFoundationDemo`、`MxCharacterTestUi`、`MxCharacterTestCombatHud`、`MxCharacterTestSettingsUi`、`MxCharacterTestNetworkUi`、`MxCharacterTestAbilityUi`、`MxCharacterTestRuntimeAiLabUi`、`MxCharacterTestRuntimeAiLabRecoveryUi`、`MxCharacterTestBehaviorDeckUi`、`MxCharacterTestTacticalDeckUi` 和 `MxPlayerGuidanceUi`。Player Guidance 使用一个 package authority Screen 与两个 exported Runtime component；Runtime AI Lab 加载专用包的统一行为组工作区与正式时间轴；`MxCharacterTestTacticalDeckUi` 独立承载玩家战术行为卡组装配，不复用 Runtime AI Behavior Deck 页面；`MxCharacterTestHud` 继续提供常驻启动器和其他尚未迁移的界面。流程固定为保存分层草稿、统一预检、预览 Apply plan、由 FairyGUI helper plugin 物化、可选从 FairyGUI 回读、发布；浏览器和 Authoring Server 都不直接写 Composer-owned `package.xml`、component XML、atlas 或 `*_fui.bytes`。日常操作见 `Docs/Guides/FAIRYGUI_UI_COMPOSER_USER_GUIDE.md`，完整权威边界见 `Docs/FAIRYGUI_UI_COMPOSER.md` 与 `Docs/Decisions/ADR-007-fairygui-semantic-ui-composer-authority.md`。
- 生成器只拥有 `Packages/com.vincent.wgameframework/**/FairyGUI/Generated/**/*.g.cs`。不要用它覆盖手写 binder / ids / composition，也不要覆盖 `FGUIProject/**` 或 `Assets/Bundles/FGUI/**`。
- Runtime HUD Shell 可作为 opt-in FairyGUI path 接入 Ability Slice；默认手测和诊断仍保留 UI Toolkit Showcase，避免双 HUD 和诊断能力倒退。
- FairyGUI / UI Toolkit 共存策略见
  `Docs/Tasks/FAIRYGUI_UITOOLKIT_COEXISTENCE_529.md`。Debug UI、Editor
  工具、现有 showcase 和 validation HUD 默认继续使用 UI Toolkit；只有明确
  issue 指定的 player-facing surface 才进入 FairyGUI 迁移。
- Localization key 现在通过 UI core 的 `MxUiTextKey` / `MxUiLocaleId` /
  `IMxUiTextProvider` 表达，FairyGUI manifest 使用
  `MxFairyGuiLocalizedTextBinding` 声明 text control 到 key 的映射。UI /
  FairyGUI 不直接依赖 `MxFramework.Config`，真实语言库只能在 composition
  root 适配成 `IMxUiTextProvider` 后注入。
- Runtime HUD 和 Story Dialog composition 现在都接受可选
  `IMxUiTextProvider`。可用 `MxDelegateUiTextProvider` 在组合根把真实语言库
  或测试表包成 provider；locale / revision 变化后调用 shell `Refresh(...)`
  会通过 navigator rebind 刷新已打开 FairyGUI view。
  Config-backed 项目可以在组合根用委托把 `MxUiTextKey.Value` /
  `MxUiLocaleId.Value` 映射到 `LocalizedTextKey` / `LocaleId`，而不是让 UI
  core 或 FairyGUI assembly 直接引用 Config。
- Story dialog / choice 的第一条 FairyGUI path 由 #537 落地：Story runtime
  不依赖 FairyGUI，dialog / choice / continue 通过 ViewModel 和
  `MxUiCommand` 表达；当前最小切片支持单个可用 choice，并具备 adapter
  级 default focus / submit / cancel route。完整 choice list 和更复杂的
  product transition sequencing 仍属后续 product hardening。
- 复杂 transition UX 和 Debug UI 迁移还属于后续 product hardening，迁移复杂 runtime panels 前必须先补齐对应 issue。

### 6.5 Combat Physics Playground

Combat Physics 的当前手测入口复用 `CombatAnimationPhysicsTest` 场景，用于验证自研 `CombatPhysicsWorld`、Combat Motion、统一 query、hit resolve 和场景反馈是否能形成可玩闭环。

手测方式：

1. 打开 `Assets/Scenes/CombatAnimationPhysicsTest.unity`。
2. Play 后 HUD 顶部模式显示 `Physics Game`，场景中应有 `Combat_Player_Marker` 和 `Combat_Enemy_Marker`。
3. 用 `WASD` 或方向键驱动 Player；输入先进入 `InputCharacterCommandSource`，再经 `CharacterControlStateMachine` / `CharacterMotionResolver` 驱动 Combat Motion；`Space` 起跳，角色会受重力下落、落地、撞墙或撞顶。
4. 左键拖动 Player / Enemy marker 可直接改位置；右键拖动镜头。核心 follow / orbit 已通过 `MxCameraUnityRig` 求值并应用，`Camera.main` 只保留给 pointer ray 这类 Unity 查询。
5. 按 `Q` 在 `Capsule / Ray / Sphere / Aabb / Sector` 间切换。
6. 按 `P` 或点击 `Probe` 执行一次只探测不扣血的查询。
7. 按 `J` 执行正式 CharacterAction；命令经 `CharacterActionController` / Gateway，Combat Track 再由 `CharacterActionCombatTrackBridge` 启动子 CombatAction，并复用现有 HitResolve 可视化伤害链路。点击 `Attack` 仍执行当前选中 marker 的手动攻击。
8. 按 `T` 或点击 `AI Cmd` 运行一次 **Runtime AI Planner** command source；点击 `Break` 触发 Gameplay pressure -> Character Control reaction -> MxAnimation presentation -> Debug UI snapshot。
9. 按 `R` 重置 Showcase；HUD 的 `Mini Game Feedback` 会显示回合、分数、连击、当前 shape、Motion position / velocity / grounded / collision flags、world revision、最近一次 `CombatPhysicsQueryDebugReport` 和 Character Control 诊断摘要。

Combat Physics Playground 约定：

- 物理命中权威来自 `CombatPhysicsWorld.Query` / `ExplainQuery`，不使用 `UnityEngine.Physics` 作为结果来源。
- Player 移动权威来自 `MxFramework.Combat.Motion.CombatKinematicMotor`，并在同一固定帧写回 `CombatPhysicsWorld` body position；移动后 Probe / Attack 必须读取写回后的新位置。
- Character Control 可玩切片权威来自 `RuntimeCombatCharacterControlSlice` 组合根：Local Input、Runtime AI Planner、state machine、motion resolver、action bridge、pressure reaction、MxAnimation presentation 和 `CharacterControlDebugSource` 在同一场景内形成回归闭环。
- Combat Playground 的相机表现来自 `MxFramework.Camera`：`RuntimeCombatShowcaseInputController` 采样 Player / Enemy marker 为 `MxCameraTargetSnapshot`，由 `MxCameraUnityRig` 在 LateUpdate 路径应用；Character Control facing basis 通过 `MxCameraFacingBasisResolver` 从相机 state 派生，Character Control core 不依赖 Camera。
- 场景 marker 只负责给 Demo 提供输入和表现；Runtime Combat Physics / Motion 仍保持纯运行时模块边界。
- 该入口是制作人手测和验收入口，不承载 WGame 具体角色、关卡或业务配置。

### 6.6 Combat Animation Playable Demo

Combat Animation 的可玩验证入口用于串联输入、`RuntimeHost` 三模块管线、WeaponTrace、HitResolve、Combat -> Gameplay Bridge HP、Unity 表现驱动和 UI Toolkit HUD。

代码入口：

- Demo runtime：`Assets/Scripts/WGameFrameworkProject/Demos/CombatAnimation/CombatAnimationDemoBootstrap.cs`
- 输入桥接：`Assets/Scripts/WGameFrameworkProject/Demos/CombatAnimation/DemoInputToActionAdapter.cs`
- 位姿适配：`Assets/Scripts/WGameFrameworkProject/Demos/CombatAnimation/CombatDemoPoseSource.cs`
- HUD：`Assets/Scripts/WGameFrameworkProject/Demos/CombatAnimation/CombatAnimationHudController.cs`
- 场景生成：`Assets/Scripts/WGameFrameworkProject/Demos/Editor/CreateCombatAnimationDemoScene.cs`
- 可玩场景：`Assets/Scenes/CombatAnimationDemo.unity`
- UI Toolkit：`Assets/UI/MxFramework/CombatAnimationHud.uxml` / `.uss`

手测方式：

1. 如需重新生成场景，执行 `MxFramework / Combat / Generate Animation Demo Scene`。
2. 打开 `Assets/Scenes/CombatAnimationDemo.unity`，直接 Play。
3. 用 `WASD` 移动 Player；位姿由 `CombatDemoPoseSource` 提供给 `CombatTransformDriver`。
4. 按 `J` 启动 LightAttack，按 `K` 启动 HeavyAttack，按 `Space` 启动 DodgeRoll。
5. HUD 显示 Player 当前动作、阶段、localFrame、Player/Dummy HP、WeaponTrace candidate 数、最近 Bridge / Attribute 事件，以及只读 Runtime Diagnostic 面板。
6. 当前 Demo 不生成 AnimatorController；`DemoCombatAnimatorDriver` 收到 action lifecycle 事件后输出 `[CombatAnimatorDriver] Entity ... ActionStarted ...` 日志。未来绑定 AnimatorController 后可直接按 mapping state name 走 CrossFade。

Combat Animation Playable 约定：

- 权威动作推进来自 `CombatActionRuntimeModule`，WeaponTrace 来自 `CombatWeaponTraceRuntimeModule`，诊断来自 `CombatAnimationDiagnosticsModule`。
- 当前没有通用 RuntimeCommand 到 CombatActionRunner 的正式桥接；Demo 使用 `DemoInputToActionAdapter` 从 `InputCommandQueue` 消费输入并调用 `CombatActionRunner`。
- `J` / `Space` 来自默认输入配置；`K` 是本 Demo 在 `CombatAnimationDemoBootstrap.EnqueueDemoKeyboardCommands` 中补充写入 `InputCommandQueue` 的临时绑定。
- HP 权威状态来自 `GameplayAttributeSetComponent`；命中后由 `HitResolveSystem` 写入 `_hitResults`，再经 `CombatHitApplicationSystem` 输出 `AddComponentAttribute`，最后由 `GameplayAttributeCommandSystem` 修改 HP 并驱动 HUD。
- Runtime Diagnostic 面板只读展示 `CombatActionStateComponent`、hit application output commands、`GameplayAttributeSetComponent`、`CombatEntityGameplayMap`、Demo diagnostic hash 和 `RuntimeEventQueue<GameplayRuntimeEvent>.CreateSnapshot()` 摘要；它不 drain event queue，也不修改 Combat / Gameplay 状态。
- Demo 的动作伤害仍是 `CombatAnimationDemoBootstrap` 内的手测动作配置；按钮或键盘输入只触发 Combat 动作，不直接修改 HP。

### 6.7 MxAnimation Play Mode Smoke

MxAnimation 的可视化 smoke 场景用于肉眼验证真实 Skeleton 模型通过正式 sample resource catalog 加载 `.anim` / `AvatarMask`，并由 `UnityPlayablesAnimationBackend` 播放 1D locomotion blend 和 upper body layer。

代码入口：

- Demo runtime：`Assets/Scripts/WGameFrameworkProject/Demos/MxAnimationSmoke/MxAnimationSmokeDemoBootstrap.cs`
- 场景生成：`Assets/Scripts/WGameFrameworkProject/Demos/Editor/CreateMxAnimationSmokeScene.cs`
- 可视化场景：`Assets/Scenes/MxAnimationPlayModeSmoke.unity`
- HUD：`Assets/UI/MxFramework/MxAnimationSmoke/MxAnimationSmokeHud.uxml` / `.uss`

手测方式：

1. 如需重新生成场景，执行 `MxFramework / MxAnimation / Generate Play Mode Smoke Scene`。
2. 打开 `Assets/Scenes/MxAnimationPlayModeSmoke.unity`，直接 Play。
3. 按 `I` / `O` / `P` 把 locomotion speed 设置为 idle / walk / run，`Space` 播放 upper body attack。
4. 观察 Skeleton 模型下半身持续响应 locomotion blend，上半身 layer 可叠加播放攻击；HUD 会显示 speed parameter、blend weights、layer weight、warmup、backend graph、resource loaded/ref count 和 Combat bridge 状态。

资源加载约定：

- Skeleton model、`.anim` clip 和 upper body `AvatarMask` 都先写入 `mxframework.samples` catalog，运行时只使用 `ResourceKey`。
- Editor Play Mode smoke 使用 catalog-backed serialized reference provider 注册 Unity asset，再由 `ResourceManager.Load<GameObject>` / `LoadAsync<AnimationClip>` / `LoadAsync<AvatarMask>` 加载。
- `UnityPlayablesAnimationBackend` 只从 `IResourceManager` 获取 `AnimationClip` handle；demo 不直接把 `AnimationClip` 塞给 backend。
- 1D blend definition、warmup group 和 upper body layer 都来自 `MxAnimationSetDefinition`，不从场景脚本绕过正式 mapping / resource loading 流程。
- Combat action 通过 `CombatMxAnimationUnityBridge` 转成 `CrossFade` 请求，动画时间不反向写入 Combat 权威状态。

### 6.8 MxAnimation System Showcase

MxAnimation System Showcase 是比 smoke 更完整的可视化验收场景。它在同一场景中放置四个 Skeleton actor，分别展示 1D locomotion blend、2D directional blend、upper-body layer + AvatarMask + Combat bridge、Mod override / fallback diagnostics，并在 UI Toolkit HUD 中展示 warmup、package validation、compatibility validation、bake artifact hash、Playable cache 和 resource handle 状态。详细手测流程和验收清单见 `Docs/Demo/MX_ANIMATION_SYSTEM_SHOWCASE.md`。

代码入口：

- Demo runtime：`Assets/Scripts/WGameFrameworkProject/Demos/MxAnimationShowcase/MxAnimationShowcaseDemoBootstrap.cs`
- 场景生成：`Assets/Scripts/WGameFrameworkProject/Demos/Editor/CreateMxAnimationShowcaseScene.cs`
- 可视化场景：`Assets/Scenes/MxAnimationSystemShowcase.unity`
- HUD：`Assets/UI/MxFramework/MxAnimationShowcase/MxAnimationShowcaseHud.uxml` / `.uss`

### 6.9 Marble Maze Runtime Showcase

Marble Maze 用于验证框架物理与 MxFramework Runtime 的边界：球体移动、墙体阻挡、GEM 拾取和 EXIT 判定必须由框架 runtime / physics 模块负责，`MarbleMazeRuntimeModule` 负责命令、计时、checkpoint、diagnostics、Replay hash 和 SaveState。Unity 场景对象只作为显示和输入适配，不使用 `Rigidbody` / `Collider` / trigger 作为玩法权威。

代码入口：

- Runtime：`Assets/Scripts/WGameFrameworkProject/Demos/MarbleMaze/MarbleMazeRuntime.cs`
- Unity view / input 适配：`Assets/Scripts/WGameFrameworkProject/Demos/MarbleMaze/MarbleMazePhysicsDemo.cs`
- Framework physics adapter：`Assets/Scripts/WGameFrameworkProject/Demos/MarbleMaze/MarbleMazeFrameworkPhysicsWorld.cs`
- AppFlow 适配：`Assets/Scripts/WGameFrameworkProject/Demos/MarbleMaze/MarbleMazeAppFlowDemo.cs`
- 场景生成：`Assets/Scripts/WGameFrameworkProject/Demos/Editor/CreateMarbleMazeScenes.cs`
- 可玩场景：`Assets/Scenes/MarbleMazeBoot.unity` / `Assets/Scenes/MarbleMazeGameplay.unity`
- UI Toolkit：`Assets/UI/MxFramework/MarbleMaze/MarbleMazePlayableDemo.uxml` / `.uss`
- 测试：`Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Demo/MarbleMaze/MarbleMazeRuntimeTests.cs`

手测装配方式：

1. 打开 `Assets/Scenes/MarbleMazeBoot.unity` 或 `Assets/Scenes/MarbleMazeGameplay.unity`，直接 Play。
2. 如需重建场景，运行 Unity 菜单 `MxFramework / Marble Maze / Create Playable Scenes`；不要手写 `.unity` YAML。
3. 生成器会创建场景根对象、`DefaultInputService`、`MarbleMazePhysicsDemo`、`MarbleMazeAppFlowDemo`、`UIDocument`、棋盘、球、墙、GEM 和 EXIT view；不得依赖 Unity trigger 完成玩法。
4. WASD / 方向键控制倾斜；HUD 按钮提供 Pause / Reset / Save / Load。

约定：

- Unity 输入通过 `DefaultInputService` 采集为 `InputSnapshot`，再入队 `RuntimeCommandBuffer` 命令；UI Button 也只入队命令或触发 SaveState 操作，不直接改 Runtime 权威状态。
- 框架物理的 position / query 结果通过 `PhysicsSample` / `Checkpoint` / `Finish` 命令同步给 Runtime。
- Runtime SaveState 使用 `IRuntimeSaveStateProvider` / `IRuntimeSaveStateRestorer`，手测 HUD 的 Save / Load 按钮执行 JSON roundtrip。
- 该 Demo 必须使用框架物理；旧 Unity Physics 方向不再符合 `AGENT_GAME_CREATION_GUIDE.md`。

### 6.10 Rendering Demo Slices Playable Showcase

Rendering Demo Slices Showcase 用一个 framework-safe 可玩场景验证 Rendering 基础设施可被 Demo 内容消费。当前包含一个 PC URP 优先的全局高度体积雾 slice；不交付局部雾体、完整多光源体积光、草、水、角色或项目业务资产。

代码入口：

- Demo runtime：`Assets/Scripts/WGameFrameworkProject/Demos/Rendering/RenderingDemoSlicesShowcaseRuntime.cs`
- Unity composition root：`Assets/Scripts/WGameFrameworkProject/Demos/Rendering/RenderingDemoSlicesShowcaseRoot.cs`
- HUD：`Assets/Scripts/WGameFrameworkProject/Demos/Rendering/RenderingDemoSlicesHudController.cs`
- 场景生成：`Assets/Scripts/WGameFrameworkProject/Demos/Editor/CreateRenderingDemoSlicesShowcaseScene.cs`
- 可玩场景：`Assets/Scenes/RenderingDemoSlicesShowcase.unity`
- UI Toolkit：`Assets/UI/MxFramework/RenderingDemoSlices/RenderingDemoSlicesHud.uxml` / `.uss`

手测方式：

1. 如需重建场景，执行 `MxFramework / Rendering / Create Demo Slices Showcase Scene`。
2. 打开 `Assets/Scenes/RenderingDemoSlicesShowcase.unity`，直接 Play。
3. 按 `1` / `2` / `3` / `4` / `5` 或点击 HUD 按钮切换 wind context、material pulse、publisher event burst、VolumeBlender priority 和 Volumetric Fog quality；按 `R` 或 `Reset` 重置。
4. HUD 会显示 Context、SharedRT / FeaturePipeline、MaterialBindingHub、RenderDataPublisher、VolumeBlender diagnostics、Volumetric Fog diagnostics 和事件日志。

约定：

- Context slice 只通过 `GlobalFrameContext` / `MxRenderingShaderIds` 更新 `_MxWindDirection` 等全局值；Demo 逻辑不直接调用 `Shader.SetGlobal*`。
- SharedRT slice 使用 synthetic `IMxRenderPassProvider` / `IMxRenderPass` 和 `ISharedRenderTextureRegistry` 记录 writer / reader 诊断，不新增独立 `ScriptableRendererFeature`。
- Material slice 只通过 `IMaterialBindingHub` / `IMaterialBindingWriter` 写 tint 和 debug overlay；Demo 逻辑不直接调用 `Renderer.SetPropertyBlock`。
- Publisher slice 使用 `IRenderDataPublisher`、`MxRenderSubjectId` 和 generic lifecycle/event counters，不包含项目业务命名。
- Volume slice 只展示 `IVolumeBlender` priority、weight、suppressed 和 applied diagnostics；当前不创建或修改 URP runtime `Volume` 对象。
- Volumetric Fog slice 通过 `MxVolumetricFogPassProvider` / `MxVolumetricFogPass` 注册到 `MxRenderingPipelineFeature`，使用 depth、half/quarter-res SharedRT diagnostics、quality preset 和 fullscreen composite shader；不新增独立 `ScriptableRendererFeature`。
- 该 showcase 是单场景展示，不触发 Resource catalog、preload 或 SceneFlow；Rendering state 不进入 Runtime authority、Replay hash、Runtime result hash 或 SaveState。

Runtime Showcase 通用约定：

- UXML / USS 是 UI 结构和视觉真源，C# 只负责绑定状态。
- `MxFramework.UI.Toolkit` 可以引用 UnityEngine / UI Toolkit。
- `MxFramework.Gameplay`、`Config.Runtime`、`Buffs`、`Modifiers` 等纯运行时模块不得反向依赖 UI。
- OnGUI 暂时保留为后备显示；正式手测入口优先使用 UI Toolkit HUD 的手动控制和 Mini Game Feedback。
- 配置项由当前 Showcase / Demo 的组合根显式管理；子 Runner 不应要求制作人手动挂载或单独配置。
- 旧 `MxFramework.Preview` / Preview Target 资产链路已退役；新的运行时验证应通过当前 Gameplay / Combat / CharacterTest 切片实现。
- 默认 HUD 是单一紧凑面板；不要恢复旧 Preview Target overlay。
- Ability Slice 可同时存在 FairyGUI player HUD 和 UI Toolkit diagnostics
  HUD 的代码路径，但场景默认不得把两者都当作 player HUD 打开。需要双开
  时必须明确一个是 opt-in diagnostics/showcase。
- Snapshot v0 不包含 JSON 序列化、编辑器 UI、Runtime Preview 协议或项目业务类型。

### 6.11 UI Camera 3D Validation

UI Camera 3D Validation 用于验证 UI 3D Overlay Camera 路径：主相机作为 URP Base Camera，UI 3D 相机作为 Overlay Camera 加入 camera stack；普通 UI 仍由 UI Toolkit HUD 显示。该入口只验证表现层 camera / layer / HUD 绑定，不承诺玩法闭环，因此交付等级是 `Runtime Slice`。

代码入口：
- Runtime / Composition Root：`Assets/Scripts/WGameFrameworkProject/Demos/CameraUi/UiCamera3DValidationDemo.cs`
- 场景生成：`Assets/Scripts/WGameFrameworkProject/Demos/Editor/CreateUiCamera3DValidationScene.cs`
- UI Toolkit：`Assets/UI/MxFramework/CameraUi3DValidation/UiCamera3DValidation.uxml` / `.uss`
- 场景：`Assets/Scenes/UiCamera3DValidation.unity`
- 测试：`Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Demo/CameraUi/UiCamera3DValidationDemoTests.cs`

生成或重建场景：
```text
MxFramework/Camera UI/Create 3D Validation Scene
```

手动验证：
1. 打开 `Assets/Scenes/UiCamera3DValidation.unity` 并进入 Play Mode。
2. Game 视图应同时显示世界参考 cube、右侧 UI-layer 3D 物体和左上角 UI Toolkit HUD。
3. HUD 的 `URP Overlay Stack Bound`、`Base excludes UI layer`、`Overlay only UI layer`、`3D UI object on UI layer` 均应显示成功状态。
4. 点击 `Rebind` 应保持 stack 绑定成功；点击 `Pause Spin` / `Resume Spin` 应切换 UI 3D 物体旋转状态。
5. Console 不应出现新增 error。

约束：
- 该 Demo 依赖 `MxFramework.Camera.URP`，URP 依赖保持在 Unity-facing Demo / adapter 层，不进入 noEngine camera core。
- UI 3D 物体使用项目已有 `UI` layer 作为最小验证层；主相机排除该 layer，Overlay Camera 只渲染该 layer。
- 不手写 `.unity`、Material 或 PanelSettings YAML；这些序列化资产由 Unity 菜单创建并保存。

### 6.11 Story Runtime Minimal Replay Loop

Story S1 是纯 C# `Runtime Slice`，用于验证剧情 graph/beat/step/choice 能通过 Runtime command、event queue、hash、SaveState 和 replay 路径闭环。它不是可玩 Demo，不包含 Unity 场景、UI Toolkit 对话框、Timeline、Gameplay effect、Resources preload 或 Story authoring import。

最小装配：

```csharp
using MxFramework.Runtime;
using MxFramework.Story;
using MxFramework.Story.Runtime;

var graph = new StoryGraphDefinition(
    graphId: 1001,
    version: 1,
    entryBeatId: 2001,
    beats: new[]
    {
        new StoryBeatDefinition(
            beatId: 2001,
            steps: new[]
            {
                new StoryStepDefinition(stepId: 3001, kind: StoryStepKind.Line)
            },
            branches: new[]
            {
                new StoryBranchDefinition(branchId: 1, targetBeatId: 2002, isFallback: true)
            },
            triggerIds: new[] { 4001 }),
        new StoryBeatDefinition(
            beatId: 2002,
            steps: new[]
            {
                new StoryStepDefinition(
                    stepId: 3002,
                    kind: StoryStepKind.SetFact,
                    factKey: new StoryFactKey(1001, 5001),
                    factValue: StoryValue.FromBool(true))
            })
    });

var director = new StoryDirector();
director.LoadGraph(graph);

var module = new StoryRuntimeModule(director);
module.CommandBuffer.Enqueue(StoryRuntimeCommandFactory.RaiseTrigger(
    RuntimeFrame.Zero,
    StoryRuntimeCommandSources.TestDriver,
    triggerId: 4001));

module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));

long hash = RuntimeHashCombiner.ComputeHash(
    RuntimeFrame.Zero,
    new IRuntimeHashContributor[] { new StoryRuntimeHashContributor(director) });
```

Runtime command ids are fixed by ADR-005:

| Factory | CommandId | Meaning |
| --- | ---: | --- |
| `RaiseTrigger(...)` | `1003001` | External input / adapter raises a stable trigger id. |
| `SelectChoice(...)` | `1003002` | UI or test driver resolves a live `beatInstanceId` by stable `choiceId`. |
| `CompletePresentation(...)` | `1003003` | UI / Timeline / audio / camera adapter acknowledges a waiting presentation step. |
| `RequestEnterBeat(...)` | `1003004` | Debug/system source requests a graph + beat entry. |
| `AbortGraph(...)` | `1003005` | Debug/system source aborts a loaded graph. |

Save / restore uses existing Runtime contracts:

```csharp
var provider = new StoryRuntimeSaveStateProvider(director, () => 0L);
RuntimeSaveState state = provider.CaptureSaveState().Value;
string json = RuntimeSaveStateJson.SaveToJson(state);

RuntimeSaveState loaded = RuntimeSaveStateJson.LoadFromJson(json).Value;
var restoredDirector = new StoryDirector();
new StoryRuntimeSaveStateProvider(restoredDirector).RestoreSaveState(loaded);
```

约束：

- `MxFramework.Story` 是 noEngine core，只依赖 Core + Events，不读取 RuntimeCommand。
- `MxFramework.Story.Runtime` 只依赖 Story + Runtime，拥有独立 Story `RuntimeCommandBuffer` drain owner。
- Story Runtime 不 drain Gameplay command buffer；`Story.GameplayBridge` 只能 enqueue Gameplay commands。
- `WaitWithFrameTimeout` 只保留 DTO 字段，S1 未实现 runtime timeout 行为。
- 当前测试入口：`Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Story/` 和 `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Story.Runtime/`。

### 6.12 Story Config Mapping

`MxFramework.Story.Config` 把项目层导入器产出的配置行映射成 `StoryGraphDefinition`。它只依赖 Story + Config，不读取 RuntimeCommand，不创建 Gameplay / Resources / Unity 侧对象。

```csharp
using MxFramework.Story;
using MxFramework.Story.Config;

var set = new StoryConfigSet(
    graphs: new[]
    {
        new StoryGraphConfig(id: 1001, entryBeatId: 2001)
    },
    beats: new[]
    {
        new StoryBeatConfig(id: 2001, graphId: 1001, sortOrder: 10, triggerIds: new[] { 4001 }),
        new StoryBeatConfig(id: 2002, graphId: 1001, sortOrder: 20)
    },
    steps: new[]
    {
        new StoryStepConfig(
            id: 3001,
            graphId: 1001,
            beatId: 2001,
            kind: StoryStepKind.Line,
            textKey: 9001)
    },
    branches: new[]
    {
        new StoryBranchConfig(id: 1, graphId: 1001, beatId: 2001, targetBeatId: 2002, isFallback: true)
    },
    choices: System.Array.Empty<StoryChoiceConfig>(),
    facts: System.Array.Empty<StoryFactConfig>());

StoryConfigReferenceIndex references = new StoryConfigReferenceIndex()
    .AddTextKey(9001);

StoryGraphConfigMappingResult result = StoryGraphConfigMapper.Map(set, graphId: 1001, references);
if (result.IsValid)
{
    StoryGraphDefinition graph = result.Definition;
}
```

确定性排序规则：

- beat: `SortOrder`, `BeatId`
- step: `SortOrder`, `StepId`
- choice: `SortOrder`, `ChoiceId`
- branch: `Priority`, `BranchId`
- trigger / effect id 数组升序

Validator 会阻止缺失 entry beat、非法 branch / choice target、重复稳定 id、unsupported step kind / wait policy、非法 text key、非法 trigger / effect id、缺失或类型不匹配的 Story fact reference、非法 `SetFact` raw value，以及同一 beat 同时声明 choices / branches 或多个 fallback branches。详见 `Docs/Interfaces/Story.Config.md`。

### 6.13 Story Gameplay / Resources Bridges

Story S3 新增两个 noEngine sibling bridge。它们不改变 Story core / Story.Runtime 依赖方向，也不新增 Gameplay command contract。

Gameplay bridge 把 Story effect intent 转成 Gameplay-owned command：

```csharp
using MxFramework.Gameplay;
using MxFramework.Runtime;
using MxFramework.Story.GameplayBridge;

var componentWorld = new GameplayComponentWorld();
GameplayEntityId entity = componentWorld.CreateEntity();
var gameplayBuffer = new RuntimeCommandBuffer();
var bridge = new StoryGameplayEffectBridge(gameplayBuffer, componentWorld);

StoryGameplayEffectResult effect = bridge.EnqueueGameplayEffect(
    StoryGameplayEffectIntent.AddComponentAttribute(
        StoryGameplayEntityRef.ComponentEntity(entity),
        sourceId: 1003102,
        attributeId: 100,
        delta: -10,
        delayFrames: 0,
        traceId: "story-effect"),
    currentStoryFrame: new RuntimeFrame(42));
```

`targetFrame = currentStoryFrame + max(0, DelayFrames)`。`DelayFrames == 0` 表示同帧 enqueue；组合根若要同帧消费，必须在 `RuntimeTickStage.Simulation` 中先 tick Story，再 tick Gameplay。Bridge 只调用 `RuntimeCommandBuffer.Enqueue`，不调用 `DrainForFrame`。

当前支持的 Gameplay command：`SetComponentAttribute`、`AddComponentAttribute`、`CastComponentAbility`、`ApplyComponentBuff`、`RemoveComponentBuff` 和 legacy `CastAbility`。Story bridge 不直接写 Attributes / Modifiers / component store；Buff 变更必须走 command 或 `GameplayBuffRequestBuffer`。

Resources bridge 只生成 preload plan，不加载资源：

```csharp
using MxFramework.Resources;
using MxFramework.Story.ResourcesBridge;

var metadata = new StoryResourcePreloadMetadata(
    "story.cutscene.1001",
    explicitKeys: new[]
    {
        new StoryResourceKeyMetadata("story.ui.dialogue", ResourceTypeIds.VisualTreeAsset),
        new StoryResourceKeyMetadata("story.audio.line_001", ResourceTypeIds.AudioClip)
    },
    labels: new[] { "story.cutscene.common" },
    maxConcurrentLoads: 4);

StoryResourcePreloadPlanResult planResult = StoryResourcePreloadPlanBuilder.Build(metadata);
ResourcePreloadPlan plan = planResult.Success ? planResult.Plan : null;
```

`StoryResourcePreloadPlanBuilder` 会校验 key / label、去重并稳定排序；它不依赖 Unity provider、不调用 `IResourceManager.Load`，也不释放资源。

### 6.14 Story Unity Adapters / Editor Debug

Story S4 新增 Unity-facing adapter 和 Editor-only debug surface，交付等级是 `Framework Feature`，不是可玩 Demo。Unity 侧只 enqueue Story runtime commands 或 consume `StoryRuntimeEvent`；Editor 侧只读 `StoryDirectorSnapshot` / event queue snapshot。

Unity trigger/input adapter：

```csharp
using MxFramework.Story.Runtime;
using MxFramework.Story.Unity;

StoryRuntimeModule storyModule = CreateStoryRuntimeModule();

var trigger = triggerObject.AddComponent<StoryTriggerZoneAdapter>();
trigger.Bind(storyModule);
trigger.TriggerId = 4001;
trigger.Param0 = 0;
trigger.Param1 = 0;

// OnTriggerEnter 会 enqueue Story.RaiseTrigger；也可由显式 UI/测试调用：
trigger.RaiseTrigger();
```

Presentation completion adapter：

```csharp
var completion = uiObject.AddComponent<StoryPresentationCompletionAdapter>();
completion.Bind(storyModule);
completion.CompletePresentation(beatInstanceId, stepId, graphId);
```

Presentation event router：

```csharp
var router = viewObject.AddComponent<StoryRuntimeEventPresentationRouter>();
router.OnPresentationEvent.AddListener(evt => Debug.Log(evt.Kind));
router.AddRoute(StoryEventKind.StepStarted).Event.AddListener(evt => PlayLine(evt));

// 只有当该 router 是 event queue 的明确 drain owner 时才调用：
router.DrainAndRoute(storyModule.Events, currentFrame);
```

Editor debug：

```csharp
using MxFramework.Story.Editor;

StoryEditorDebugRegistry.Register("Demo Story Runtime", storyModule, () => router.RecentEvents);
```

打开菜单 `MxFramework > Story > Runtime Debug` 查看只读快照。窗口显示 graph、active beat、blackboard、event queue、recent events、last commands 和 command errors；没有注册 target 时只显示空状态。

约束：

- `MxFramework.Story.Unity` 不引用 `UnityEditor`。
- Unity adapter 不直接调用 `StoryDirector` mutation API。
- Editor debug surface 默认只读；若后续需要 debug command，必须显式 command gate 或 enqueue Story `RuntimeCommand`。
- Timeline / Cinemachine package-specific binding 仍 deferred；当前提供通用 `StoryRuntimeEventPresentationRouter`。

### 6.15 Story Runtime AI Planner Bridge / Playable Demo

Story S5 新增 noEngine `MxFramework.Story.RuntimeAiPlannerBridge` 和第一个可玩 Story 垂直切片。

Runtime AI Planner projection 只读 Story facts，并写入调用方持有的 `IAiWorldState`：

```csharp
using MxFramework.AI;
using MxFramework.Story;
using MxFramework.Story.RuntimeAiPlannerBridge;

var profile = new StoryRuntimeAiProjectionProfile(new[]
{
    new StoryRuntimeAiFactMapping(new StoryFactKey(1001, 5001), new AiFactKey("story.signal.seen"))
});

var worldState = new AiWorldState();
var diagnostics = new StoryRuntimeAiProjectionDiagnostics();
StoryRuntimeAiProjectionResult result =
    StoryRuntimeAiWorldStateProjector.Project(storyDirector.CreateSnapshot(), worldState, profile, diagnostics);
```

支持的 Story value：`Bool -> bool`、`Int32 -> int`、`Int64 -> long`、`Fix64 -> long raw`。`StringRef` / `None` 不写入 Runtime AI Planner，会移除 stale AI fact 并记录 diagnostic。profile 外的 Story fact 会被跳过并记录 `SkippedUnlistedStoryFact`；profile 中缺失的 Story fact 会从 `IAiWorldState` 移除。

Playable 入口：

1. 生成或刷新场景：`MxFramework > Story > Create Runtime Vertical Slice Scene`。
2. 打开 `Assets/Scenes/StoryRuntimeVerticalSlice.unity`。
3. Play 后点击 `Trigger`，Story.Unity adapter enqueue `Story.RaiseTrigger`。
4. 点击 `Continue`，presentation adapter enqueue `Story.CompletePresentation`。
5. 点击 `Stabilize signal`，UI enqueue `Story.SelectChoice`；demo bridge 将 choice effect 转成 Gameplay-owned `AddComponentAttribute` command。
6. 点击 `Save` / `Restore` 验证 Story + Gameplay state roundtrip。
7. 点击 `Replay` 验证 recorded Story commands reproduce the same Story + Gameplay result hash。

Demo 代码入口：

- Runtime composition root：`Assets/Scripts/WGameFrameworkProject/Demos/Story/StoryRuntimeVerticalSliceDemo.cs`
- Unity UI Toolkit runner：`Assets/Scripts/WGameFrameworkProject/Demos/Story/StoryRuntimeVerticalSliceRunner.cs`
- UXML / USS：`Assets/UI/MxFramework/Story/StoryRuntimeVerticalSlice.uxml` / `.uss`
- Scene generator：`Assets/Scripts/WGameFrameworkProject/Demos/Editor/CreateStoryRuntimeVerticalSliceScene.cs`
- Tests：`Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Story.RuntimeAiPlannerBridge/` and `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Demo/Story/`

边界：

- Story state authority stays in `StoryDirector` / `StoryRuntimeModule`。
- FairyGUI Story UI 只能作为后续 presentation adapter 接入；迁移前必须
  满足 `Docs/Tasks/FAIRYGUI_STORY_UI_MIGRATION_READINESS_528.md`，并保留
  当前 UI Toolkit vertical slice 作为 diagnostics/fallback surface。
- Gameplay effect uses `StoryGameplayEffectBridge` -> Gameplay `RuntimeCommandBuffer`; no direct buff grant/remove。
- Runtime AI Planner projection is one-way Story -> Runtime AI Planner and never writes Story blackboard。
- Scene / PanelSettings are generated through Unity Editor tooling; `.unity` and `.asset` YAML are not hand-authored。

### 6.16 Story External Authoring CLI

Story S6 新增外部 Python CLI，用 Markdown Story Outline v1 生成可审阅的 `.story.json` draft，再交给项目层导入器映射到 `MxFramework.Story.Config` 行。该工具位于 `Tools/MxFrameworkStoryAuthoring/story_authoring.py`，只使用 Python 标准库，不依赖 Unity、Runtime Story assemblies、Yarn、Ink、Articy 或 Authoring AI Assist 执行路径。

导入和校验：

```bash
python Tools/MxFrameworkStoryAuthoring/story_authoring.py import-markdown \
  Tools/MxFrameworkStoryAuthoring/fixtures/markdown/basic_choice_story.md \
  --out Tools/MxFrameworkStoryAuthoring/fixtures/generated/basic_choice_story.story.json

python Tools/MxFrameworkStoryAuthoring/story_authoring.py validate \
  Tools/MxFrameworkStoryAuthoring/fixtures/generated/basic_choice_story.story.json
```

Markdown Story Outline v1 的首个支持子集：

```markdown
---
graph: 442001
entry: intro
source: basic_choice_story
---

## Beat intro
id: 442101
trigger: 442201
line: 442301 | WaitForCommand | A signal waits at the story boundary.
choice: 442401 | 442302 | end | effect 442501 | Stabilize signal

## Beat end
id: 442102
line: 442303 | NoWait | Signal stabilized through generated Story config.
set-fact: 442601 | Bool | true
```

生成的 `.story.json` draft 使用 `schema=mx.story.config.draft.v1`，包含 `graphs`、`beats`、`steps`、`branches`、`choices`、`facts`、`textKeys` 和 `texts`。行字段名保持和 `StoryGraphConfig` / `StoryBeatConfig` / `StoryStepConfig` / `StoryBranchConfig` / `StoryChoiceConfig` / `StoryFactConfig` 一致；`texts` 只是创作元数据，用于检查 text key 和审阅内容，不进入 Runtime Story.Config 消费路径。

`validate` 会输出结构化 JSON diagnostics 并在 Error 时返回非 0。首批稳定 code 包括 `DuplicateId`、`MissingEntryBeat`、`MissingTextKey`、`InvalidBranchTarget`、`InvalidChoiceTarget`、`InvalidTriggerId`、`InvalidEffectId`、`UnsupportedStepKind`、`UnsupportedWaitPolicy` 和 `UnsupportedDirective`。

CharacterTest demo 已接入这条 handoff 链路。默认 Markdown 源是 `Tools/MxFrameworkStoryAuthoring/fixtures/markdown/character_test_bootstrap.md`，运行时读取 `Assets/StreamingAssets/MxFramework/CharacterTest/character_test_bootstrap.story.json`。修改 Markdown 后重新生成：

```bash
python Tools/MxFrameworkStoryAuthoring/story_authoring.py import-markdown \
  Tools/MxFrameworkStoryAuthoring/fixtures/markdown/character_test_bootstrap.md \
  --out Assets/StreamingAssets/MxFramework/CharacterTest/character_test_bootstrap.story.json
```

场景里只放 `GameManager` 时，`GameManager` 会从 `Application.streamingAssetsPath/MxFramework/CharacterTest/character_test_bootstrap.story.json` 加载外部 draft，经 `CharacterTestStoryDraftJson` 转为 `StoryConfigSet`，再用 `StoryGraphConfigMapper` 生成 `StoryGraphDefinition` 交给 `GameSlice`。Console 会输出 external draft loaded、graph loaded、entry beat enqueued 和 line 文本；文件缺失或校验失败时会 warning 并退回内置 fixture。

CharacterTest 现在有两条 UI surface：UI Toolkit debug 面板只承担 developer/debug 观察；玩家剧情面板由 `CharacterTestFairyGuiStoryDialogHost` 按需打开。Host 在 `GameManager` 创建 `GameSlice` 和 `GlobalResourceRuntimeServices` 后装配局部 `MxFairyGuiNavigator`，仅注册 `MxStoryDialog` 一个 view。Story snapshot 中存在 pending presentation 时显示 Continue，点击后入队 `StoryRuntimeCommandFactory.CompletePresentation(...)`；存在 awaiting choice 时显示 choice 列表，点击后入队 `StoryRuntimeCommandFactory.SelectChoice(...)`；没有 pending presentation/choice 时关闭，避免常驻遮挡 CharacterTest 场景。Host 会缓存当前 Story dialog view-model signature，内容未变化时跳过重复 rebind，避免每帧重置 FairyGUI 焦点；同一 Story UI 状态下的重复 Continue / Choice 点击只接受第一次入队，后续重复点击作为 UI 层重复输入忽略，不向 `StoryRuntimeModule.CommandBuffer` 追加重复命令。资源 runtime 或 `ui.fairygui.storydialog.fui` entry 缺失时，Host 只记录 warning 并保持未配置状态，不阻断 `GameSlice`。

FairyGUI Story dialog 的资源 key 是 `ui.fairygui.storydialog.fui`，typeId 是 `MxFairyGuiPackageBytes`，源资产是 `Assets/Bundles/FGUI/MxStoryDialog/MxStoryDialog_fui.bytes`。该 entry 属于 `ui.fairygui.storydialog` bundle rule，生成 `global.ui.fairygui.storydialog.assetbundle`；它是普通 `assetBundle` provider 资源，不使用 `sceneAssetBundle`。Resource Manager 只负责 profile 编辑、预览和校验；真正写入 `StreamingAssets` 的 runtime catalog 与 AssetBundle 仍由 Unity Global AssetBundle Builder 执行。

边界：

- `.story.json` 是工具层 interchange draft，不是 Unity asset、ScriptableObject、Runtime SaveState 或 replay 文件。
- Story.Config 仍是 C# 运行时配置桥接；外部 CLI 只输出匹配行契约的 JSON draft，不实例化 C# DTO。
- Authoring AI Assist 在本切片中只保留术语和未来规划位置；没有 LLM SDK、API key、prompt runner 或自动生成文本路径。
- Yarn / Ink / Articy 完整导入、GraphView 编辑、Timeline / Cinemachine 专项绑定和未来 Authoring AI Assist 建议流仍 deferred。

## 7. Config 表和校验

基础流程：

1. 定义配置类型，实现 `IConfigData`。
2. 定义 `ConfigSchema<T>`，写清字段、ID 范围、引用规则和多语言要求。
3. 把导入后的数据注册到 `ConfigTable<T>`。
4. 用 `Validate` 或 `ConfigAuthoring.ValidateTable` 做提交前校验。

示例：

```csharp
var modifiers = new ConfigTable<BasicModifierConfig>(BasicModifierConfig.CreateSchema());
modifiers.Add(new BasicModifierConfig(
    200001,
    new LocalizedTextKey("mod.burn.name"),
    new LocalizedTextKey("mod.burn.desc")));

var registry = new ConfigRegistry();
registry.RegisterProvider<BasicModifierConfig>(modifiers);

ConfigTableValidationReport report = modifiers.Validate(registry, localizationProvider);
if (report.HasErrors)
{
    // 提交前阻断，展示 report.Issues。
}
```

### 7.1 Character Application 配置契约

`MxFramework.Character.Application` 提供角色应用层第一批固定配置表和纯解析器。它只描述静态数据、引用关系和解析结果，不生成运行时角色实例。

```csharp
using System.Collections.Generic;
using MxFramework.CharacterApplication;
using MxFramework.Config;

var characters = new ConfigTable<CharacterConfig>(CharacterConfig.CreateSchema());
characters.Add(new CharacterConfig(
    new CharacterConfigId(710001),
    "mx.character.codex_flow_test",
    new LocalizedTextKey("character.codex_flow_test.name"),
    new LocalizedTextKey("character.codex_flow_test.desc"),
    new CharacterAttributeProfileId(720001),
    new CharacterBodyProfileId(730001),
    new EquipmentSchemaId(750001),
    new EquipmentLoadoutId(760001),
    new AbilityLoadoutId(790001),
    new CharacterPresentationProfileId(810001),
    CharacterControllerKind.HumanInput,
    "controller.human.default",
    new[] { "sample", "vanguard" }));

ConfigSchema characterSchema = CharacterConfig.CreateSchema();
IReadOnlyList<ConfigSchema> allCharacterSchemas = CharacterApplicationConfigSchemas.CreateAll();
```

配置包可以先用纯解析器在 Workstation、测试或生成前检查中得到 resolved profile：

```csharp
CharacterPackageResolveResult result = CharacterPackageResolver.Resolve(
    new CharacterPackageResolveRequest(
        character,
        attributeProfile,
        bodyProfile,
        bodyParts,
        equipmentSchema,
        selectedLoadout,
        equipmentStates,
        weapons,
        abilityLoadouts,
        combatActionSets,
        presentationProfile));

if (result.ValidationReport.HasErrors)
{
    // 展示 result.ValidationReport.Issues；例如 CHAR_EQUIPMENT_STATE_TIE。
    return;
}

CharacterResolvedProfile profile = result.ResolvedProfile;
EquipmentStateId activeState = profile.ActiveEquipmentStateId;
CharacterAbilityId[] abilities = profile.EffectiveAbilityIds;
CharacterResourceKeyEntry[] resources = profile.RequiredResources;
```

约定：

- `IConfigData.Id` 保持 `int`，跨表字段使用 `CharacterConfigId`、`EquipmentLoadoutId` 等 typed id。
- `StableId` 用于 SaveState、Mod、调试报告和跨版本迁移。
- `CharacterAttributeProfileConfig` 使用 `BaseValue` / `InitialValue`，运行时当前值由 runtime state 保存。
- `CombatActionSetConfig` 只做动作绑定，不复制 Combat action timeline 权威字段。
- 空手、单武器、多槽位武器都通过 `EquipmentLoadoutConfig` 和 `EquipmentStateResolver` 解释。
- Resolver 不读取 Unity 场景对象、不写运行时 world、不加载资源；失败通过 `CharacterDiagnostic.StableCode` 结构化输出。
- SaveState 恢复时应重新解析装备得到 active state，保存的 active state id 只作为 mismatch 诊断线索。

### 7.2 Mod Package 运行时边界（单包 / LoadPlan / 多包 Merge）

- 单包加载：`RuntimeModPackageLoader.LoadFromDirectory(path)` 只负责读取一个包并返回 `RuntimeConfigPatchBundle`，不处理跨包顺序。
- 包发现与排序：`RuntimeModPackageDiscovery.Discover(containers)` + `RuntimeModPackageLoadPlanBuilder.Build(catalog)` 只产出 `OrderedItems/SkippedItems`，不修改配置表。
- 启用状态持久化：`RuntimeModPackageLoadoutJson.LoadFromFile(path)` 读取 `mod_loadout.json`，再用 `RuntimeModPackageLoadPlanBuilder.Build(catalog, loadout)` 解析启用组合。
- 多包合并：`RuntimeModPackagePatchMerger.Merge(loadPlan, baseRegistry)` 才会按顺序执行 patch，并生成最终 `ConfigRegistry` 与 merge report。

Loadout v0 约定：

- `format` 必须是 `mx.modLoadout.v1`。
- `enabledPackageKeys` 使用 `packageId|containerRelativePath`，例如 `demo.runtime.patch.mod|runtime-patch-mod`。
- `loadout == null` 时保持兼容：启用全部 valid 包。
- `enabledPackageKeys == []` 时启用 0 个包。
- loadout 中缺失 key 不中断流程，写入 `LoadPlan.Warnings`。

### 7.3 Mod Diagnostic CLI（命令行诊断）

`mod diagnose` 在外部 CLI 读取容器、loadout、排序及 runtime-patch 诊断投影，输出 JSON 快照，不打开 Unity。当前 CLI 与 EditorServer 复用 `ModDiagnosticService`；它不调用 Runtime registry/完整 merger，主要诊断 v1 patch 中的 Modifier/Buff覆盖记录，不能据此保证真实WGame全量配置合并或运行时一致性。

```bash
# 基本用法：诊断一个容器目录
dotnet run --project src/MxFramework.Authoring.Cli -- mod diagnose -c /path/to/Mods

# 指定 loadout 文件
dotnet run --project src/MxFramework.Authoring.Cli -- mod diagnose -c /path/to/Mods --loadout /path/to/loadout.json

# 美化 JSON 输出
dotnet run --project src/MxFramework.Authoring.Cli -- mod diagnose -c /path/to/Mods --pretty

# 输出到文件
dotnet run --project src/MxFramework.Authoring.Cli -- mod diagnose -c /path/to/Mods --output snapshot.json

# 遇到 Warning 也返回非零退出码
dotnet run --project src/MxFramework.Authoring.Cli -- mod diagnose -c /path/to/Mods --fail-on-warning
```

退出码：

| 退出码 | 含义 |
|--------|------|
| 0 | success=true；允许Warning，除非传入`--fail-on-warning` |
| 2 | 校验被阻断（存在 Error） |
| 5 | 存在 Warning 但无 Error（仅在 `--fail-on-warning` 时） |
| 1 | 工具内部错误 |

多个容器可重复 `-c`：

```bash
dotnet run --project src/MxFramework.Authoring.Cli -- mod diagnose -c /path/to/Mods1 -c /path/to/Mods2
```

当前目录示例从`Tools/MxFramework.Authoring`执行；从仓库根执行时把project路径写为`Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli`。`--container` / `-c`可重复，`--containers A,B`可合并传入；未指定时容器为相对当前工作目录的`Assets/StreamingAssets/MxFramework/Demo`。未给`--loadout`采用all-valid，不自动寻找loadout文件；绝对路径字段仅显式`--include-absolute-paths`开启。

未给`--output`时stdout为完整snapshot JSON，stderr为人类摘要；指定后文件为完整UTF-8无BOM JSON、自动创建父目录，stdout仅一行success/errors/warnings/output摘要，stderr仍为人类摘要。`--pretty`控制完整JSON缩进。

CLI DTO为Authoring Core的`ModDiagnosticSnapshotDto`，Runtime为Config.Runtime的`RuntimeModDiagnosticSnapshot`；是两份实现，不是shared source/linked compile DTO。两者声明相同`mx.modDiagnosticSnapshot.v1`及对应字段；CLI现有schema测试检查自身格式/初始化，不构成两侧序列化及合并语义的完整验证。需要跨Runtime一致性时应单独执行对照，不引用旧完成任务作保证。本次未执行CLI或测试。

快照 JSON 格式与 `ModDiagnosticSnapshotDto` 一致，包括：

- `summary`：discovered/valid/invalid/enabled/ordered/skipped/overrides/errors/warnings
- `packages`：每包有效性、启用状态、key、相对路径、错误/警告
- `errors` / `warnings`：分别保存 Error和Warning，带 severity/code/source/message；没有顶层`issues`字段
- `success`：整体是否成功（有 Error 则 false）
- `format`：固定为 `mx.modDiagnosticSnapshot.v1`
- `generatedUtc`、`loadout`、`loadPlan`和`overrides`保留同一DTO字段；时间戳不作为确定性内容hash。

### 7.4 Mod Diagnostic Snapshot（运行时诊断统一入口）

- 目的：把 `catalog + loadout + loadPlan + mergeResult` 汇总成单一快照，不改变运行时行为。
- 构建 API：
  - `RuntimeModDiagnosticSnapshotBuilder.Build(catalog, loadout, loadPlan, mergeResult)`
  - `RuntimeModDiagnosticSnapshotJson.SaveToJson(snapshot)`
- 快照格式固定为 `mx.modDiagnosticSnapshot.v1`，包含：
  - `summary`（discovered/valid/invalid/enabled/ordered/skipped/overrides/errors/warnings）
  - `packages`（每包有效性、启用状态、key、相对路径、错误/警告）
  - `loadout`（isDefaultAll/profileId/displayName/enabledPackageKeys；未提供 loadout 时为 default-all）
  - `loadPlan`（Ordered/Skipped + skipReason + orderIndex）
  - `overrides`（配置覆盖链与赢家）
  - `errors` / `warnings`（带 code/source/message）
- 判定规则：
  - `Error` -> `success=false`
  - `Warning` -> `success` 可保持 true
- `RuntimeVerticalSliceRunner` 在 loadout+merge 模式会生成 snapshot 摘要，并可写入 `Application.persistentDataPath/MxFramework/Diagnostics/mod_diagnostic_snapshot.json`。

当前 v0 合并策略：

- deterministic last-write-wins：后加载包覆盖先加载包。
- `Remove` 删除不存在行记为 `Noop`，不会中断。
- skipped（invalid/disabled）包不参与合并，但会记录进报告。
- ordered 包在 merge 阶段加载失败会直接返回 `Success=false`，并包含 `packageId/path/error`。

### 7.5 EditorServer 诊断 API（Authoring Editor）

`editor serve` 提供 Mod 诊断 API：

- `GET /api/mod/diagnose`
- `POST /api/mod/diagnose`

参数：

- `containers`（数组或 CSV）
- `loadout`（可选）
- `includeAbsolutePaths`（可选，默认 false）

返回：`mx.modDiagnosticSnapshot.v1` JSON。
实现上，CLI `mod diagnose` 与 EditorServer API 复用同一 `ModDiagnosticService.BuildSnapshot(...)`，避免双份诊断逻辑漂移。

## 8. 配置编辑和 Authoring AI Assist（AI 辅助）

> **本文的 AI 指 Authoring AI Assist**，即 LLM 辅助编辑配置数据的功能。
> 不包含 Runtime AI Planner（游戏内 NPC 决策引擎）或 Development Agent（Gitea 自动化工作流）。

`ConfigAuthoring` 是后续 Unity ConfigEditor 和 AI 生成配置表时的统一入口。

Unity 内部查看配置源：

1. 打开菜单 `MxFramework > Framework Manager`。
2. 选择 `编辑模式`。
3. 切换到 `配置工作台`。
4. 在左侧 `配置源` 导航中选择 `内置 Demo / BasicModifierConfig`、`内置 Demo / ActionCatalog` 或 `内置 Demo / ActionGraph`。
5. 在主工作区页签查看 `概览 / 字段 / 行视图 / 引用`。
6. 在右侧检查器查看源预览、当前源校验和报告动作。
7. 在 `字段` 页点击 `详情`，右侧检查器会显示字段元数据、enum/flags 候选项和引用目标。
8. 对引用字段点击 `查看目标源`，可跳转到目标配置源。
9. 在底部问题抽屉查看全局问题列表。
10. 点击 `复制模板` 可把模板复制到剪贴板。
11. 页面会自动检测所有配置源健康状态，显示源数量、总行数、问题源、Error、Warning、缺失引用、多语言缺失、ID 问题、Schema 问题、索引源和索引 Key。
12. 在 `问题筛选` 中选择 `全部 / Error / Warning` 查看问题明细。
13. 点击 `AI 上下文` 可复制当前源信息、当前选中字段、健康报告、Schema、TSV 预览和当前源问题列表。
14. 页面会基于上一次健康检测保存轻量基线，显示配置源、行数、Error、Warning 和错误类型统计是否变化。
15. 点击 `变动报告` 可复制配置变动统计。
16. 点击 `重置基线` 可把当前健康状态设为新的比较基线。
17. 点击 `导出报告` 可把当前配置报告包写入 `Temp/MxFrameworkReports/Config/`。
18. 点击 `提交前检查` 会刷新配置源、重新检测、导出报告包并生成 `config_precommit.txt`。
19. 点击 `校验` 可验证当前配置源的引用和多语言规则。
20. 点击顶部 `复制报告` 可复制当前配置源报告。
21. 如果项目层运行时注册了新配置源，点击 `刷新` 重新读取列表并自动检测。

当前 Config Workbench v0 只查看配置源、复制模板和校验当前源，不编辑真实配置资产，也不绑定 Excel/CSV/Json/Luban/ScriptableObject。框架内置 Demo 见 `Docs/Demo/CONFIG_DEMO.md`，它只验证框架能力，不是任何真实项目的数据来源。

旧外部 Buff Authoring Editor、WGame `BuffType` 流程和 `BuffAuthoringWorkflowTemplate` 已随 Buff v2 退役。当前 Buff v2 工具化入口是 `config/buff_authoring.json` + `buff validate/compile/import-unity`，生成物映射到 `GameplayBuffDefinitionRegistry` 和 `CombatGameplayAuraBuffDefinitionRegistry`；不能恢复旧 `DamageByAttr` / `CastOrb*` 编辑路径。

字段显示规则：

- `ConfigField.Name` 是真实字段 key，导入、导出、校验、引用和 AI 修复都使用它。
- `ConfigField.DisplayName` 是中文显示别名，配置工作台优先显示它，并同时保留原字段名。
- 问题报告会输出 `field=` 和可选 `fieldDisplay=`，便于人和 AI 同时理解。

项目层接入真实配置时，优先用 `ExternalConfigEditorSource` 包装已经解析好的 TSV/JSON/Luban 结果：

```csharp
var schema = new ConfigSchema(
        "GameActionCatalog",
        null,
        displayName: "项目动作索引",
        structureKind: ConfigStructureKind.Table)
    .AddField(new ConfigField("Id", ConfigFieldType.Integer, displayName: "编号", required: true))
    .AddField(new ConfigField(
        "GraphId",
        ConfigFieldType.ConfigReference,
        displayName: "动作图",
        referenceRule: new ConfigReferenceRule("GraphId", "GameActionGraph", ConfigStructureKind.Graph)));

MxEditorUtils.RegisterConfigEditorSource(new ExternalConfigEditorSource(
    name: "Game / ActionCatalog",
    sourceType: "TSV",
    schema: schema,
    rowCount: 120,
    keys: new object[] { 1001, 1002 },
    previewText: "Id\tGraphId\n1001\t9001\n",
    sourcePath: "ProjectConfig/Tables/ActionCatalog.tsv",
    contentHash: "sha256:..."));
```

带枚举/flags 候选项：

```csharp
var enums = new ConfigEnumRegistry();
enums.Register(new ConfigEnumDomain("game.ActionTags", isFlags: true)
    .AddValue(new ConfigEnumValue(1, "Area", "范围"))
    .AddValue(new ConfigEnumValue(2, "Projectile", "飞行物")));

MxEditorUtils.RegisterConfigEditorSource(new ExternalConfigEditorSource(
    name: "Game / ActionCatalog",
    sourceType: "TSV",
    schema: schema,
    rowCount: 120,
    keys: new object[] { 1001, 1002 },
    previewText: "Id\tTags\tGraphId\n1001\t3\t9001\n",
    enumRegistry: enums));
```

需要完全自定义展示时，实现 `IConfigEditorSource`：

```csharp
public sealed class GameConfigEditorSource : IConfigEditorSource
{
    private readonly IConfigTable<GameBuffConfig> _table;
    private readonly IConfigProvider _registry;
    private readonly ILocalizationProvider _localization;

    public string Name => "Game / BuffConfig";
    public string SourceType => "Luban";
    public ConfigSchema Schema => _table.Schema;
    public int RowCount => _table.Rows.Count;

    public ConfigAuthoringTemplate CreateTemplate()
    {
        return ConfigAuthoring.CreateTemplate(Schema);
    }

    public ConfigAuthoringReport Validate()
    {
        return ConfigAuthoring.ValidateTable(_table, _registry, _localization);
    }

    public string CreateTsvPreview(int maxRows)
    {
        // 项目层按自己的数据结构导出预览文本。
        return string.Empty;
    }

    public string CreateReport()
    {
        return ConfigAuthoring.ExportReportText(Validate());
    }
}
```

注册到 Framework Manager：

```csharp
MxEditorUtils.RegisterConfigEditorSource(new GameConfigEditorSource());
```

如果该源还需要参与 `ConfigSourceIndex` 和跨源引用校验，同时实现 `IConfigEditorSourceIndexProvider`。

如果该源还需要显示行视图和未来控件映射，同时实现 `IConfigEditorTablePreviewProvider`。`MemoryConfigEditorSource<T>` 和 `ExternalConfigEditorSource` 已经默认支持。当前控件全部只读，保存功能后续开启。

如果该源还需要显示枚举或 flags 候选项，同时实现 `IConfigEditorEnumProvider`，或在创建 `ExternalConfigEditorSource` 时传入 `ConfigEnumRegistry`。

提交前或 AI 辅助修表时，可以直接生成健康报告：

```csharp
IReadOnlyList<IConfigEditorSource> sources = MxEditorUtils.GetConfigEditorSources();
ConfigHealthReport health = MxEditorUtils.AnalyzeConfigHealth(sources);
string report = MxEditorUtils.CreateConfigHealthReport(health);
```

生成 AI 修复上下文：

```csharp
IReadOnlyList<ConfigIssueView> issues = MxEditorUtils.CollectConfigIssues(sources);
string context = MxEditorUtils.CreateConfigAiFixContext(source, health, issues, previewRows: 5);
```

带当前字段摘要：

```csharp
string context = MxEditorUtils.CreateConfigAiFixContext(
    source,
    health,
    issues,
    previewRows: 5,
    selectedFieldName: "GraphId");
```

生成配置变动报告：

```csharp
string baseline = MxEditorUtils.LoadConfigHealthBaseline();
ConfigChangeReport changes = MxEditorUtils.DetectConfigChanges(health, baseline);
string changeReport = MxEditorUtils.CreateConfigChangeReportText(changes);
MxEditorUtils.SaveConfigHealthBaseline(health);
```

导出配置报告包：

```csharp
ConfigReportExportResult result =
    MxEditorUtils.ExportConfigReportBundle(source, health, issues, changes, previewRows: 5);
```

默认导出目录是 `Temp/MxFrameworkReports/Config/`，包含：

- `config_health.txt`：整体健康报告。
- `config_issues.txt`：结构化问题列表。
- `config_changes.txt`：配置统计变动报告。
- `config_ai_context.txt`：当前配置源的 AI 修复上下文。
- `config_precommit.txt`：提交前检查结果。
- `config_report_index.txt`：报告包索引。

`config_precommit.txt` 的规则：

- `result: ready`：没有 Error 或 Warning，可以提交。
- `result: warning`：没有 Error，但存在 Warning，可以提交但必须人工确认。
- `result: blocked`：存在 Error，不应提交。

AI Agent 辅助修配置时，优先读取 `config_precommit.txt` 和 `config_report_index.txt`，再按任务读取具体报告；不要依赖 Unity Console 截图或临时剪贴板内容。

## 9. Runtime Config Patch / Mod v0

运行时动态配置不直接修改编辑源，而是把 Base 表、热更 Patch、Mod 和 Debug 覆盖层合并成新的只读运行时表。

当前 Runtime Config patch 示例只覆盖仍存在的配置类型，例如 `BasicModifierConfig`。Buff v2 definition 不再通过 `BasicBuffConfig` / `ConfigBuffFactory` 路径创建。

```csharp
BasicModifierConfig baseModifier = new BasicModifierConfig(
    200001,
    new LocalizedTextKey("mod.fire.name"),
    new LocalizedTextKey("mod.fire.desc"));

BasicModifierConfig patchedModifier = new BasicModifierConfig(
    200001,
    new LocalizedTextKey("mod.fire.patch.name"),
    new LocalizedTextKey("mod.fire.patch.desc"));

ConfigPatchMergeResult<BasicModifierConfig> result =
    RuntimeConfigPatchMerger.Merge(
        BasicModifierConfig.CreateSchema(),
        new[] { baseModifier },
        new[]
        {
            ConfigPatchEntry<BasicModifierConfig>.Upsert(
                patchedModifier,
                ConfigLayerKind.Patch,
                sourceId: "hotfix")
        });

BasicModifierConfig runtimeModifier = result.Table.GetConfig<BasicModifierConfig>(200001);
string changeReport = result.ChangeSet.ToReportText();
```

Mod 新增配置的推荐流程：

```csharp
ConfigPatchMergeResult<BasicModifierConfig> modifiers =
    RuntimeConfigPatchMerger.Merge(
        BasicModifierConfig.CreateSchema(),
        baseModifiers,
        modModifierPatches);

var registry = new ConfigRegistry();
registry.RegisterProvider<BasicModifierConfig>(modifiers.Table);

ConfigTableValidationReport report = modifiers.Table.Validate(registry, localizationProvider);
```

当前 v0 支持：

- `Upsert`：新增或覆盖同 ID 行。
- `Remove`：从运行时 merged view 删除指定 ID。
- `ConfigLayerKind.Base / Patch / Mod / Debug`：记录变更来源层。
- `ConfigChangeSet`：输出新增、替换、删除、无变化及 sourceId。

当前 v0 不做：

- 不解析 Mod 文件包。
- 不保存回 TSV / JSON。
- 不做字段级 Patch。
- 不直接通知运行时对象或 component store；运行时接入由明确的 reload / command / request 路径处理。

生成表模板：

```csharp
ConfigAuthoringTemplate template =
    ConfigAuthoring.CreateTemplate(BasicModifierConfig.CreateSchema());

string header = template.HeaderLine;
string sample = template.SampleLine;
string aiReadableText = template.Text;
```

生成结构化校验报告：

```csharp
ConfigAuthoringReport report =
    ConfigAuthoring.ValidateTable(modifiers, registry, localizationProvider);

string reportText = ConfigAuthoring.ExportReportText(report);
```

校验表格到 Graph / Localization 的跨源引用：

```csharp
var graphSchema = new ConfigSchema(
    "GameActionGraph",
    null,
    structureKind: ConfigStructureKind.Graph);

var sourceIndex = new ConfigSourceIndex();
sourceIndex.Register(
    new ConfigSourceEntry(graphSchema)
        .AddKey(9001));

ConfigTableValidationReport crossSourceReport =
    sourceIndex.ValidateCrossSourceReferences(aiActionIndexTable);
```

约定：

- `HeaderLine` 和 `SampleLine` 使用 TSV，方便复制到表格工具。
- 业务表里的多语言字段只保存 `LocalizedTextKey`。
- 具体语言文本由 `ILocalizationProvider` 提供。
- `LocalizationTsvLoader.LoadFromText(string)` 可把 `key<TAB><locale...>` 文本源装载到 `MemoryLocalizationProvider`；空 key 是 error，空文本是 warning，重复 key/locale 是 warning 且后值覆盖前值。
- 表格引用 Graph、Localization 或运行时产物时，先把目标源登记到 `ConfigSourceIndex`。
- 具体导入器不进入框架核心，只要最后产出 `ConfigTable<T>`。

CharacterTest 当前使用 `Assets/StreamingAssets/MxFramework/Localization/character_test.tsv` 作为 FairyGUI 玩家界面的权威文本源。UI 层通过 `MxFramework.UI.ConfigBridge.MxConfigUiTextProvider` 把 `MxUiTextKey` / `MxUiLocaleId` 映射到 Config 的 `LocalizedTextKey` / `LocaleId`；`CurrentLocale` 变化会递增 `Revision`，宿主据此刷新已绑定 view model，并在缺 key 时使用 fallback 文本。

## 10. Buff v2 definition registration

旧配置驱动 Buff 路径已经退役：没有 `BasicBuffConfig`、`ConfiguredBuff`、`ConfigBuffFactory` 或 `IBuffFactory`。Buff v2 定义由 Gameplay 组合根显式注册，或通过 Authoring CLI 生成的 JSON 导入 registry；运行时通过 request / command 改 active Buff state。

```csharp
var definitions = new GameplayBuffDefinitionRegistry(new GameplayBuffDefinition(
    buffId: 601008,
    stableId: "buff.character_test.guard_aura.defense",
    defaultDurationFrames: 2,
    maxStackCount: 1,
    GameplayBuffStackPolicy.RefreshDuration,
    new[] { GameplayBuffEffectDefinition.AttributeModifier(1, AttrDefense, 5) }));

var requests = new GameplayBuffRequestBuffer();
requests.EnqueueApply(targetEntityId, 601008, sourceId: 501008);
```

数据化 Buff definitions 使用 `Tools/MxFramework.Authoring/samples/buff-character-test/config/buff_authoring.json` 同形态的 v2 schema，并通过 `buff compile/import-unity` 生成 runtime JSON；不要恢复旧 `ConfigBuffFactory`。

### 10.1 Buff presentation metadata

Buff presentation is a read-only adapter over deterministic Buff state. Authoring JSON may add optional `buffs[].presentation` fields:

- `shortName`, `description`, `iconResourceKey`
- `priority`
- `showOnPlayerHud`, `showOnTargetHud`, `showOnHeadPad`

Buff authoring also requires `buffs[].types`, a multi-select player-facing classification such as `empower`, `weaken`, `damage`, `restore`, `control`, `damageReduction`, or `mechanic`. Types do not encode source/施加方式; Aura, on-hit, command, or environment application remains owned by the applying chain. `buff compile/import-unity` writes `Assets/MxFrameworkGenerated/Gameplay/Buffs/buff_presentation_definitions.json`; its compatibility `polarity` value is inferred from `types` for existing HUD color/sort behavior. Runtime code can import it with `GameplayBuffPresentationDefinitionJsonImporter`, then call `GameplayBuffPresentationSnapshotBuilder.Build(...)` with the target entity, active `GameplayComponentWorld`, Buff definitions, optional presentation definitions, current frame, and the requested surface. The result is a sorted list of `GameplayBuffPresentationItem` rows for UI/head-pad display only; it does not affect Buff application, duration, stacking, hash, SaveState, or projected gameplay effects.

CharacterTest consumes this through the FairyGUI Combat HUD: player and target plates show PoE-like compact Buff/Debuff/Aura icon strips with remaining-time mask, stack count, short-name fallback, and hover summary. Character head pads show only up to four high-priority small status badges, for example `DEF+` / `DEF-`, with polarity color, optional stack count, and a lightweight remaining-time bar; the HeadPad badges are quick combat hints, while the Combat HUD remains the complete Buff readout.

## 11. 配置驱动 Modifier

`ModifierPipeline` 同样通过 factory 接入配置。

```csharp
var modifiers = new ConfigTable<BasicModifierConfig>(BasicModifierConfig.CreateSchema());
modifiers.Add(new BasicModifierConfig(
    200001,
    new LocalizedTextKey("mod.power.name"),
    new LocalizedTextKey("mod.power.desc"),
    paramIndex: 1,
    parameters: new[] { 10, 20 }));

IModifierFactory factory = new ConfigModifierFactory<BasicModifierConfig>(modifiers);
var pipeline = new ModifierPipeline(attributeOwner, factory);

pipeline.TryAddModifier(200001, out IModifier modifier);
```

如果游戏需要条件、效果或属性公式，仍然在游戏层创建具体 `IModifierCondition` / `IModifierEffect`，不要把业务逻辑写进框架配置类。

## 12. Runtime AI Planner（游戏内行为决策）

> **本文的 AI 指 Runtime AI Planner**。不包含 AIAction 配置数据、Authoring AI Assist 或 Development Agent 工作流。

Runtime AI Planner 是内置游戏内行为决策基础设施，不需要第三方插件。

核心概念：

- `AiFactKey`：事实 key，例如 `enemy.visible`、`has.weapon`。
- `IAiWorldState`：事实集合。
- `AiBlackboard` / `SharedAiBlackboard`：分层黑板，可用 parent blackboard 提供共享事实，本地事实覆盖共享事实，并通过 `FactChanged` 暴露变更。
- `IAiGoal`：目标，判断当前世界是否满足。
- `IAiAction`：动作，包含前置条件和效果。
- `SequentialPlanner`：按目标优先级选目标，再搜索可执行动作链。
- `AiUtilityPlanner`：按 `IAiUtilityConsideration` 对可执行动作打分，确定性选择分数最高的单步动作。
- `AiSensorManager`：按优先级和 tick 间隔推进 `IAiSensor`，并记录失败诊断。
- `AiSequenceActionRunner` / `AiAgentBrain`：把 sensors -> planner -> action runner 组合成可 tick 的 agent runtime。
- `AiHfsmStateMachine` / `AiHfsmPlannerAdapter`：代码级 HFSM primitive，可把当前 state action 暴露成 planner 输出。

示例：

```csharp
var hasWeapon = new AiFactKey("has.weapon");
var enemyVisible = new AiFactKey("enemy.visible");
var enemyDefeated = new AiFactKey("enemy.defeated");

var world = new AiWorldState();
world.SetValue(hasWeapon, false);
world.SetValue(enemyVisible, false);
world.SetValue(enemyDefeated, false);

var goals = new IAiGoal[]
{
    new AiFactGoal<bool>(1, priority: 10f, enemyDefeated, true)
};

var actions = new IAiAction[]
{
    new AiAction(
        id: 10,
        cost: 1f,
        effects: new[] { new AiSetFactEffect<bool>(hasWeapon, true) }),

    new AiAction(
        id: 20,
        cost: 1f,
        preconditions: new[] { new AiFactCondition<bool>(hasWeapon, true) },
        effects: new[] { new AiSetFactEffect<bool>(enemyVisible, true) }),

    new AiAction(
        id: 30,
        cost: 1f,
        preconditions: new IAiCondition[]
        {
            new AiFactCondition<bool>(hasWeapon, true),
            new AiFactCondition<bool>(enemyVisible, true)
        },
        effects: new[] { new AiSetFactEffect<bool>(enemyDefeated, true) })
};

var planner = new SequentialPlanner(maxDepth: 4);
if (planner.TryPlan(world, goals, actions, out AiPlan plan))
{
    for (int i = 0; i < plan.Actions.Count; i++)
    {
        IAiAction action = plan.Actions[i];
        // 游戏层决定如何真正执行动作。
    }
}
```

注意：

- Planner 规划时 clone 世界状态，不修改传入的 `world`。
- 框架动作的 `Apply` 只修改模拟状态；真实移动、攻击、放技能由游戏层执行。
- WGame 的 GOAP 经验可以映射到这套接口，但不要把 WGame 的具体枚举、实体、技能逻辑放进框架。
- 当前已有代码级 HFSM 和 utility selector；仍不包含可视化行为树编辑器、生产级 GOAP authoring/editor、Unity NavMesh 集成或 WGame 专用怪物行为包。

Blackboard + sensors + agent brain 最小接入。示例中的 `TargetSensor` 和 `AttackScore` 是游戏层实现的 `IAiSensor` / `IAiUtilityConsideration`：

```csharp
var targetVisible = new AiFactKey("target.visible");
var attackSelected = new AiFactKey("action.attack");

var blackboard = new AiBlackboard();
var sensors = new AiSensorManager();
sensors.Register(new TargetSensor(targetVisible), intervalFrames: 1, priority: 100);

var brain = new AiAgentBrain(
    id: 1,
    worldState: blackboard,
    planner: new AiUtilityPlanner(new[] { new AttackScore() }),
    goals: new[] { new AiFactGoal<bool>(1, 10f, attackSelected, true) },
    actions: new IAiAction[]
    {
        new AiAction(
            1001,
            1f,
            new[] { new AiFactCondition<bool>(targetVisible, true) },
            new[] { new AiSetFactEffect<bool>(attackSelected, true) })
    },
    sensors: sensors,
    blackboard: blackboard);

AiAgentBrainTickResult tick = brain.Tick(frame: 0);
if (tick.SelectedActionId == 1001)
{
    // 游戏层把 action id 映射到移动、攻击、技能或 CharacterCommand。
}
```

CharacterTest 当前用 `CharacterTestRuntimeAiPlannerCommandModule` 作为 Runtime AI Planner 适配层：`AiControlled` 角色在 `PreSimulation` 读取上一帧角色世界，按编译后的 HFSM + Utility profile 选择动作，并提交 `CharacterTestRuntimeCommandSources.RuntimeAiPlanner` 来源的 `SetControlIntent`。`CharacterTestRuntimeCommandModule` 仍是唯一写入 `CharacterTestCharacterInputComponent` 的权威入口，因此本地输入、回放和 Runtime AI Planner 输入都经过同一 command boundary。启用 `CharacterTestCharacterPhysicsRuntime` 时，追击方向由 `CharacterTestCombatPhysicsNavigationService` 通过 `CombatPhysicsWorld` 做局部绕障解析；未启用框架物理时保持直线追击 fallback。

CharacterTest 正式配置入口是 StreamingAssets JSON profile，默认路径为 `Assets/StreamingAssets/MxFramework/CharacterTest/runtime_ai_planner/default.json`。当前 schema 为 `mx.character-test.runtime-ai-planner.profile.v3`，包含感知参数和 movement 默认值：视野距离、目标记忆帧、视线遮挡层、环境采样半径、威胁评分权重、速度倍率、转向响应、接近侧向混合、偏好距离带、到达减速距离、侧移换边帧区间、方向保持帧和反应延迟帧；v1/v2 JSON 仍可加载，会补默认感知/移动参数并给 warning。场景 profile 只保存 JSON 的 StreamingAssets 相对路径，不暴露零散 planner 调参字段。Unity Editor 菜单 `MxFramework/Character/Runtime AI Planner` 顶部使用 `行为 Profile` 库选择器，自动扫描 `Assets/StreamingAssets/MxFramework/CharacterTest/runtime_ai_planner/*.json`，以中文模板名、文件名、profile id 和校验状态显示默认近战、感知近战、远程风筝、危险游斗、技能施法、激进冲锋、守卫防御、伏击者、控制控场和自定义 profile；常用操作是打开、新建模板、复制为、设为场景默认、校验、保存和复制报告，文件选择只在“高级 JSON 路径”里作为临时导入入口。`MxFramework/Level Editor` 的 `CharacterTest Characters` 模块对 `AiControlled` 角色使用同一行为库 picker：空路径表示 `继承场景默认`，非空路径表示角色级 profile override；非 `AiControlled` 角色不会消费 Runtime AI Planner profile。切换场景默认或角色 override 后需要保存/应用并重建 CharacterTest 会话生效。Play Mode 页签只读显示注册的 CharacterTest session 快照，包括 active state path、target visible/blocked/lastSeen、surface、near obstacle/object、utility score/cost、movement speed/side/hold/reaction 和 last command；保存后的配置不热改当前 runtime。旧 `CharacterTestAiBehaviorProfile` 仍作为 fallback 输入生成等价默认 profile，用于兼容既有测试和默认行为。

### CharacterTest Runtime AI Planner Runtime AI Lab

`CharacterTestRuntimeAiLabScene` 是 Runtime AI Planner 的开发者运行时观察和行为草稿入口，用来在真实 CharacterTest session 中暂停、单帧、查看最近帧 Runtime AI Planner snapshot，并编辑独立的 Lab Draft。当前 Behavior Logic schema 为 v3：所有离散角色动作都使用 `CharacterAction` Stage，并直接保存必填的稳定 `CharacterActionId`；移动、等待、纯逻辑和 Ability 继续使用各自 typed Stage。v1/v2 草稿会确定性迁移，无法解析的旧动作显示结构化 Invalid Draft，不模拟玩家输入。进入卡片后，默认玩法模式使用“何时执行 / 执行什么 / 成功后 / 失败后 / 玩法参数”的中文摘要，开发者模式再展示 compiled Stage、canonical condition/success/failure 分支、Ability/CharacterAction 引用、完整 hash，以及所选节点的只读 entry/monitor/window/目标索引 Inspector。两种模式编辑同一份 typed payload；玩法模式不覆盖无法表达的高级字段。行为卡工作台只在草稿或引用变化时低频编译 typed payload：Ready 表示不可变 Lab program 已生成；Failed 只显示完整 `code + path + message` 诊断，不生成假流程，草稿、simulation 和现有 evaluator 均保留。候选 revision 完成预编译与确定性预检后可应用于下一个 RuntimeFrame；应用失败时活动 evaluator 和历史时间线保持不变。草稿持久化和 compiler 都只属于本地开发工作区，不进入 Authority Tick、SaveState、network snapshot 或正式 replay wire。

行为卡与玩家输入是两条不同的上游路径。行为卡已经持有具体动作 ID，直接提交 Runtime CharacterAction 命令并等待真实实例；只有实例成功出现后才提交卡切换。Gateway 拒绝时，committed 行为、原动作实例和本地帧不变，右栏显示候选动作及真实拒绝原因。玩家的轻攻击、闪避按钮仍走 `AttackPrimaryPressed / DodgePressed` 输入请求，由输入层先按当前角色 ActionSet 解析为具体 CharacterAction，再进入同一个 Gateway。

开战前 Setup 使用场景优先的对战配置工作区：右侧依次显示双方启用数量、六个槽位、选中槽位的角色/行为组/出生位置、测试规则和配置校验。只有右侧准备区提供一次 `开始测试`；配置无效时按钮禁用并显示首条中文原因。场景中的角色预览仍可拖动，团队色圆环、朝向提示和双方出生点距离只属于表现层。Setup 不创建或显示时间轴；开始测试捕获首个 RuntimeFrame 后才进入运行/回看界面。

如果仓库中还没有 `Assets/Scenes/CharacterTestRuntimeAiLabScene.unity`，先在 Unity Editor 执行 `MxFramework/CharacterTest/Generate Runtime AI Lab Scene` 生成场景，再执行 `MxFramework/CharacterTest/Validate Runtime AI Lab Scene` 做结构检查。打开生成的场景进入 Play 后，`GameManager` 会检测同 GameObject 上的 `CharacterTestRuntimeAiPlannerDevSceneController`，创建暂停的 Runtime AI Lab setup session，并自动打开 FairyGUI `MxCharacterTestRuntimeAiLabUi/RuntimeAiPlannerDevPanel`。常驻启动器仍来自 `MxCharacterTestHud`。

生成场景包含大型标准测试场地 `Runtime AI Lab Standard Arena`：64m x 44m 地面、弱 1m 网格、4m/8m 强参考线、低视觉权重外墙、LOS 遮挡墙、路径柱体、窄口墙、危险标记区、风筝/追击区和双方出生垫。场地分区用于快速复现常见 Runtime AI Planner 行为：`A Open Engage` 验证普通接战，`B LOS Block` 验证可见性和遮挡，`C Path Obstacles` 验证局部绕障，`D Choke` 验证窄口通过，`E Hazard Marker` 验证危险规避，`F Kite / Pursuit` 验证拉扯和追击。场景生成器同时把这些静态障碍写入 `GameManager.WorldObjectSceneCache`，因此 session build 会通过现有 CharacterTest CombatPhysics bridge 构建静态碰撞体；Runtime Tick 不读取 Unity 场景对象。Runtime AI Lab 的默认双方出生点固定在 x=-24 / x=24 的开阔接战区两侧，主测试距离约 40-50m，便于观察远距离接战、目标线、LOS、移动意图和攻击范围。

Runtime AI Lab 面板是场景优先的透明 HUD，用户可见界面统一使用简体中文；内部 command id、稳定资源标识、hash、诊断 code 和 JSON path 保持原始技术值，便于检索和排错。顶部工具条只保留 `开始测试`、`播放/暂停`、智能体选择、`重置`、`场景标记`、`详情` 和 `隐藏`；历史帧选择统一由底部时间轴、`跳转` 和 `最新帧` 完成，不再在顶部重复提供 `-10 / -1 / Step / +10`。暂停且选中最新帧时，时间轴区域的 `推进 +1` 可精确生成一个新 RuntimeFrame。历史帧默认只读；显式点击 `从此重算` 后，`推进 +1`、播放或向右拖动才会从该选中帧逐帧执行真实 Authority Tick。进入重算态不会删除数据，首个新 Tick 成功捕获时才会不可逆地截断旧未来；失败则保留原时间线。右侧工作区显示测试配置、智能体检查器或行为工作台，中央区域不放全屏诊断遮罩。点击 `隐藏` 后，右上角会保留 `运行时智能实验室 [F9]` 悬浮入口；点击入口或按 `F9` 可重新打开，`F9` 在配置、运行、暂停检查、回看和 seek 期间都可切换主面板。`F1`-`F8` 保留给项目现有 Debug/Audio 输入。隐藏 HUD 不会改变 simulation、timeline、Lab Draft 或当前 selected Agent，场景标记仍可继续绘制。面板只消费 controller 捕获的 typed Runtime AI Planner frame snapshot 和低频 view model 字符串；场景标记只消费 snapshot 中已有 typed geometry/facts，不重新查 Unity 对象、不暴露底层 `CombatQueryResult`。战斗 Tick 不读取 UI 文本或编译行为组。

底部现在使用正式 FairyGUI 多轨时间轴：Live 默认显示 52px 聚合轨，暂停、回看、循环回看和 Completed 默认展开为 ruler + `版本 / 决策 / 动作 / 命中伤害 / 警告` 五轨。版本轨显示 baseline/revision，决策轨显示选择区间，动作轨显示 CharacterAction/Command，命中伤害轨显示 Combat 结果，警告轨显示校验与诊断。1080p 展开高度默认 320px，行为卡工作台默认 280px，720p 默认 240px，可拖动上边缘在 220–480px 内调整。行为卡页采用左侧场景、右侧 720px 玩法卡或开发者编译视图；时间轴使用不透明深色背景并只占左侧场景宽度，不再以灰色遮罩覆盖角色。时间轴按每个 Authority Tick 冻结 RuntimeFrame、commands、typed CharacterAction/Combat/runtime trace，并投影为稳定 marker/span/cluster。历史区拖动走 Warm Replay；从 Latest 拖入未来灰区会在每个 Unity Update 的 `4ms / 16 Tick` 预算内逐帧执行真实 Authority Tick。普通历史帧播放会顺序回放已有记录，到 Latest 后继续实时模拟；普通历史拖动不会越过 Latest 生成数据，必须先选择 `从此重算`。重算态的未来拖动与 `推进 +1` 共用同一个单帧 Authority 原语。未来预览显示目标帧和待生成数量，场景最多 10Hz 刷新。事件单击只高亮并打开时间轴内详情浮层，不移动帧、不改变视口或智能体；普通事件双击或浮层“定位”按钮才导航，span 可显式定位起点/终点，Cluster 单击看成员、双击只缩放并居中。事件、成员和浮层会阻止输入传播，只有空白轨道负责 scrub。时间轴还支持固定级别缩放、平移、跳转、最新帧、A/B 区间；正式 session 按 Setup 冻结的 `1..1800` 帧上限预分配，达到上限进入 Completed。Revision 轨记录候选应用到下一帧的边界；决策详情会附带攻击实例、预计命中剩余帧、是否已响应及闪避冷却状态。完整合同与验收矩阵见 [Runtime AI Lab Timeline Design](Guides/CHARACTER_TEST_RUNTIME_AI_LAB_UX_LAYOUT_DESIGN.md#timeline-design)。

“持续靠近，目标攻击时闪避”的配置步骤：在 Setup 选择要编辑的槽位，打开行为组并点击 `追击+闪避模板`。玩法模式会得到两张可见行为卡：高优先级 `攻击威胁闪避`（默认预计命中前 6–12 帧触发、30 帧冷却、同一次攻击只响应一次）和基础 `持续靠近目标`（默认 1.5m 停止）。创建或安装闪避卡时，Lab 从角色应用配置的 `ability.evade` action role 解析一次具体 evade `CharacterActionId` 并写入卡片；运行时不按玩家输入槽位猜测。开始测试后，角色先持续靠近；选中目标的攻击进入命中预测窗口时，闪避卡直接请求该具体 CharacterAction。动作真正启动后卡才成为当前行为；拒绝时 UI 显示“候选闪避 · 未执行”和真实原因，原行为继续。行为计划中的动作时长直接读取 CharacterAction 权威数据，例如当前 evade 为 1 帧时显示 `1/1`。

### CharacterTest Runtime AI Planner 竞技场

打开 `Assets/Scenes/CharacterTestArenaScene.unity` 并进入 Play，会自动显示 FairyGUI `MxCharacterTestHud/ArenaSetupPanel`。这是 Runtime AI Planner 行为 Loadout 的可玩测试入口，不是行为编辑器。操作流程：

1. 在 Team 1 / Team 2 各自的 1-3 个槽位中启用角色。
2. 每个启用槽位选择角色、策略（Behavior Loadout）和 variant。角色列表来自 `Assets/MxFrameworkGenerated/CharacterPackages`（Editor）或 StreamingAssets generated mirror（Player）中导入成功且 `CanSpawn` 的 package；`packageId` 生成稳定 CharacterId，当前默认条目是数字角色 `000001`。
3. 点击 `开始战斗`。面板会校验双方至少各有一个启用槽位，然后重建干净 offline `GameSlice`，为每个 spawn spec 写入权威 `loadoutId + variantId` 行为选择，锁定 selection phase，并恢复 runtime tick。
4. 战斗开始默认进入 `自动观战` 镜头；战斗中可按 `V` 在 `第三人称` 和 `自动观战` 间切换，也可用 Arena HUD 的镜头按钮直接选择现有镜头模式。角色行为完全由已编译 Runtime AI Planner evaluator 自动执行，UI 不直接修改角色状态。
5. 一方全灭后 runtime 暂停，显示胜方和用时。`再来一局` 使用同配置重建干净会话，`重新配置` 回到配置面板。

Arena 面板不暴露 facts、score/cost、profileSlot、behavior definition id、behavior group JSON 或热更新入口，也不支持战斗中切换策略。镜头模式只是 CharacterTest Arena 的观战/表现状态，使用现有 CharacterTest 第三人称和多目标自动取景模式，不改变 Runtime AI Planner 决策、命中、移动或网络权威规则。配置、catalog 展示和中文文案都在低频 UI/session rebuild 路径中完成；战斗 Tick 不读 JSON、不编译行为组、不构建 UI view model。

```csharp
string json = File.ReadAllText(
    "Assets/StreamingAssets/MxFramework/CharacterTest/runtime_ai_planner/default.json");
CharacterTestRuntimeAiPlannerProfileJson.TryLoad(
    json,
    out CharacterTestRuntimeAiPlannerProfile profile,
    out CharacterTestRuntimeAiPlannerValidationReport report);

var slice = new GameSlice(new CharacterTestSliceOptions
{
    Logger = logger,
    CreateCharacterViews = false,
    RuntimeAiPlannerProfile = profile,
    RuntimeAiPlannerProfileReport = report
});
```

## 13. Resources 资源管理

资源系统用 `ResourceKey` 描述稳定引用，用 `ResourceManager` 统一加载、释放、诊断和 Catalog 合并。

```csharp
var provider = new MemoryResourceProvider()
    .Register("demo/title", "Hello");

var resources = new ResourceManager();
resources.RegisterProvider(provider);
resources.AddCatalog(new ResourceCatalog(
    "demo.catalog",
    "demo.package",
    new[]
    {
        new ResourceCatalogEntry(
            "demo.text.title",
            ResourceTypeIds.String,
            "memory",
            "demo/title")
    }));

ResourceLoadResult<ResourceHandle<string>> result =
    resources.Load<string>(new ResourceKey("demo.text.title", ResourceTypeIds.String));

if (result.Success)
{
    string value = result.Value.Value;
    resources.Release(result.Value);
}
```

变体解析通过显式 profile 控制，Provider 不参与 fallback：

```csharp
resources.SetVariantProfile(new ResourceVariantProfile(
    "pc.high",
    new[] { "pc", "high", "default", string.Empty }));

ResourceLoadResult<ResourceHandle<string>> variantResult =
    resources.Load<string>(new ResourceKey("demo.text.title", ResourceTypeIds.String));
```

RetainPolicy 可以减少短时间重复加载 / 卸载造成的资源抖动：

```csharp
resources.SetRetainPolicy(ResourceRetainPolicy.Timed(durationSeconds: 3f));

ResourceHandle<string> handle =
    resources.Load<string>(new ResourceKey("demo.text.title", ResourceTypeIds.String)).Value;
resources.Release(handle);

// noEngine 层不依赖 Unity 时间，由组合根显式推进。
resources.AdvanceRetainTime(3f);
```

Retain policy 的选择边界：

- `ResourceRetainPolicy.None`：默认策略，handle 引用计数归零后立即释放底层资源。
- `ResourceRetainPolicy.Timed(...)`：适合短时间来回切换 UI、动画或小型场景 warmup，组合根必须显式推进时间或帧数。
- `ResourceRetainPolicy.KeepAlive`：适合明确需要 pinned 的常驻资源，只通过 `EvictRetainedResources()` 显式清理。
- `ResourceRetainPolicy.Budgeted(maxRetainedBytes)`：适合有 catalog size signal 的 Demo / Player 路径；预算只使用 `ResourceCatalogEntry.Size`，可选框架保留 metadata `providerData["retainPriority"]` 决定驱逐优先级，整数越大越晚驱逐。

Budgeted policy 不启动后台卸载线程，也不访问平台内存 API。引用计数归零后，record 会先进入 retained 状态，再按低 priority、较早 retained、`ResourceKey` 字符串顺序驱逐，直到 `ResourceDebugSnapshot.RetainedBytes <= RetainBudgetBytes` 或只剩 pinned `KeepAlive` 记录。`KeepAlive` 记录不会被预算策略自动驱逐；手动调用 `EvictRetainedResources()` 表示显式清理。

Remote Bundle Provider 使用 Catalog 的 `providerData` 指定 source 和 cache key：

```csharp
var remoteProvider = new RemoteBundleProvider(cacheRootPath);
resources.RegisterProvider(remoteProvider);
resources.AddCatalog(new ResourceCatalog(
    "remote.demo",
    "mod.demo",
    new[]
    {
        new ResourceCatalogEntry(
            "demo.text.remote",
            ResourceTypeIds.TextAsset,
            RemoteBundleProvider.Id,
            "demo-bundle|Assets/TestAssets/MxFramework/ResourcesDemo/resource_demo_text.txt",
            hash: "sha256:...",
            providerData: new Dictionary<string, string>
            {
                { "url", "file:///path/to/demo-bundle" },
                { "bundleName", "demo-bundle" },
                { "cacheKey", "demo.bundle.v1" }
            })
    }));
```

Provider 选择建议：

| 使用场景 | 推荐 |
| --- | --- |
| noEngine 测试 | `MemoryResourceProvider` |
| Demo、小样例、少量固定资源 | `ResourcesProvider` |
| 正式本地运行时资源 | `AssetBundleProvider` |
| Mod / DLC / 外部包下载 | `RemoteBundleProvider` |
| 项目已使用 Addressables Groups / Remote Catalog | 独立可选 Addressables Adapter |

Addressables 不是默认依赖。只有项目已经安装并决定使用 Addressables 时，才应新增独立 `MxFramework.Resources.Addressables` 适配层；业务代码仍然只接触 `ResourceKey`、`ResourceHandle<T>` 和 `IResourceManager.Release`。

YooAsset 也不是 MxFramework 默认资源路线。本仓库当前不做 YooAsset adapter；如果具体项目选择 YooAsset，应在项目层把 YooAsset 的定位收敛到 provider / build backend，而不是让框架业务代码直接依赖 YooAsset API。

### 13.1 Authoring Resource Manager / Editor Hub

外部资源管理器是全局 Authoring Resource Manager，不是角色包私有资源库。默认 Codex Flow Test 路径只作为当前工作上下文：用于引用图、Resource Plan 和 consumer 诊断，不代表资源归属于该角色包。

`Tools/MxFramework.ResourceLibrary` 现在是 Resource Mapping Profile 中 `resource-manager.manual` producer snapshot 的编辑入口。点击“加入构建 Profile”只修改浏览器内草稿；点击“保存 Profile”才会通过 Authoring Server 的唯一 Aggregator 校验并以 optimistic revision 替换完整 manual snapshot：

```text
Assets/Config/MxFramework/ResourceProfiles/global_resource_mapping_profile.v2.json
```

保存 Profile 不会构建 AssetBundle，也不会写 `StreamingAssets`。构建 Player 运行时产物仍由 Unity Editor 中的 `GlobalAssetBundleBuilderWorkbench` 或低层构建菜单执行。

从 Editor Hub 启动：

```bash
Tools/MxFramework.EditorHub/start-editor-hub.sh
```

默认打开 `http://127.0.0.1:4873/Tools/MxFramework.EditorHub/web/`，在 Hub 中进入“资源管理器”或“资源管理器 / 资源计划状态”。也可以直接启动资源管理器：

```bash
Tools/MxFramework.ResourceLibrary/start-resource-library.sh
```

常用流程：

1. 打开 Resource Manager，确认 provider 状态、资源列表、运行时计划和诊断。
2. 在 Story 工作区维护世界观记忆、故事大纲和 Story Input Packet。只有由 `canon` facts 和 `approved-for-packet` outlines 支撑、且状态为 `approved-for-art` 的 packet 可以解除 ArtSource story gate；保存只写 `StorySource/Projects/WGame`。
3. 在 Art 工作区查看 ArtSource 流程依赖、预览和任务推进。Art 可以写 `art_tasks.json`，但不能审批 Story、编辑 manifest、生成图片或导入 Unity。
4. 在 Audio 工作区按语义 Cue 审查 FMOD 实现、试听源样本、补充语义和人工审听状态；替换 event 时从 snapshot 选择目标并先通过替换校验。这里保存的是 Authoring 语义，不代表 CharacterTest runtime mapping 已经采用。
5. 在 Profile 工作区查看由 `resource-manager.profile-policy` producer 管理的 Bundle 策略。Resource Manager 只读展示 `bundleRules[]`，不创建、改名或删除 Bundle；策略变更必须由对应 policy producer 发布完整 snapshot。
6. 在 Browse 勾选由 `resource-manager.manual` 拥有的资源，选择已有目标 Bundle 后点击“批量加入 Bundle”；单个 manual 资源也可以点击“加入到 Bundle”。资源归属写入 `entries[].bundleIntent.ruleId`。其他 producer 的条目保持只读。
7. 在 Refs 工作区检查引用关系和 Bundle 分配建议，确认运行时必需资源是否已加入 Profile、是否有缺 Bundle 或同组资源分散问题。
8. 如果需要先理解关系，打开 Graph 工作区查看只读关系图：左列是引用者配置，中列是资源，右列是 Bundle / delivery。Graph 不写草稿，修复动作会回到 Refs。
9. 在 Profile 工作区查看 Bundle 成员表；只对 manual producer 条目执行移出、移动或编辑 `deliveryIntent`、`overrideIntent`、`preloadGroups` 和 `labels`。扩展 `providerData` 和 EditorOnly source locator 在 round-trip 中原样保留，废弃的 authority/proxy 别名不会重新写回。
10. 点击“保存 Profile”。保存会通过 Authoring API 聚合并写入 `Assets/Config/MxFramework/ResourceProfiles/global_resource_mapping_profile.v2.json`；非 manual producer 行保持只读且不会被此次保存覆盖。
11. 在 Build 工作区查看构建状态总览和 Bundle Plan 摘要，预览内部 bundle、成员资源、生成产物、依赖和诊断。

当前 Resource Manager 顶层工作区：

- `Story 故事设定`：以“故事设定编辑器”维护 WGame StorySource 世界观记忆、故事大纲和输入包。顶部显示设定进度、当前阻塞和下一步动作；三栏保持独立选中，packet / outline 选择会高亮引用链；缺字段、派生阻塞和服务端 diagnostics 会按对象分组展示。做故事时同步补充 Packet 的美术参照字段，不等剧情写完后再让 ArtSource 猜测。保存草稿后，在字段完整、引用已批准且服务端校验通过时批准给美术。该页只写 `StorySource`，不写 ArtSource、Unity 资产或 Profile。
- `Art 美术状态`：查看 ArtSource 流程依赖图、已有预览、任务下一动作和 `art_tasks.json` 任务推进。该页不审批 Story、不写 manifest、不导入 Unity。
- `Browse 资源浏览`：发现资源、筛选 runtime-ready 候选、选择当前资源或批量勾选资源。
- `Audio 音频语义`：Cue-first 审查 FMOD / AudioCue Authoring 语义。Error、Warning、Info 和人工试听状态分开显示；未被当前 package 引用的全局 Cue 不算进包缺口。语义状态为 `Missing / Draft / ReviewNeeded / Ready / Rejected`，替换目标从 FMOD snapshot 选择且必须先校验再保存。该页不构建 bank，也不会自动改写 CharacterTest runtime mapping。
- `Profile 构建 Profile`：只读查看 policy producer 的 `bundleRules[]`，编辑 manual producer 的 `entries[]`，并保存完整 manual snapshot。
- `Refs 引用关系`：Bundle 前审查工作台，解释“谁引用当前资源 / 同组资源是否合理 / 哪些建议可安全写入 Profile 草稿”。行级和批量建议只改浏览器内草稿，必须再点“保存 Profile”才落盘。
- `Graph 关系图`：只读 SVG 可视图，用 `引用者配置 -> 资源 -> Bundle / delivery` 帮助理解分包割裂和引用来源。它提供“去 Refs 查看建议”和“在 Browse 中查看资源”，不提供直接应用建议。
- `Build 构建计划`：只读 Build 状态和 saved-profile Bundle Plan 预览，提示是否可去 Unity 构建或 Offline 验证。
- `Debug 详情诊断`：原始 inspect、References、JSON 和工程字段诊断，供 TA / 工程排查。

`preloadGroups` 是加载时机和失败策略，不是物理分包。`bundleGroupHint` 只是 inventory 建议字段，Web Bundle Plan 和 Unity Builder 都不会再用它临时生成隐式 Bundle。内容场景使用 `resourceKey.typeId = "Scene"`、UnityAsset `sourceLocator`，并且必须通过 `bundleIntent.ruleId` 放进 scene-only bundle rule，不能与普通 asset 混包。CharacterTest FairyGUI Story dialog 使用 `resourceKey.typeId = "MxFairyGuiPackageBytes"` 和 `bundleIntent.ruleId = "ui.fairygui.storydialog"`，作为普通 UI asset bundle 构建，不与 scene bundle 或角色 bundle 混包。

Resource Manager 的 Refs、Graph 和 Build 工作区都复用 Authoring Server 已有只读数据：`referenceGraph`、单资源 inspect references、Profile 草稿 / 已保存 Profile、Bundle Plan 和 Build Status。Refs 只允许把建议写入前端 Profile 草稿；Graph 完全只读；Build 是预览和只读状态面。它们都不会构建 AssetBundle，也不会写 `StreamingAssets` 产物。真正生成 Player 运行时 catalog / bundle / preload group 仍走 Unity Editor Global AssetBundle Builder 工作台或低层构建菜单。

保存失败时优先检查 Profile 草稿字段：

- `ResourceKey id contains invalid characters.` 表示 `resourceKey.id` 不是合法运行时 key。应使用类似 `ui.start_screen.button.normal` 的稳定 runtime key，不能使用带 provider 前缀的 `resourceId`、文件路径、包含 `:` / `|` / 空白 / 斜杠的字符串。
- `Internal entry requires one existing Bundle rule id.` 表示 internal 运行时资源的 `bundleIntent.ruleId` 为空或没有命中 policy producer 发布的 Bundle rule。应从现有 Bundle 下拉框选择；Resource Manager 不会临时创建 policy。
- `External entry cannot declare bundleIntent.` / `Non-runtime entry cannot declare bundleIntent.` 表示 external、editor-only 或 excluded 条目仍填写了 `bundleIntent`。这类资源不进入内部 AssetBundle；应清空该 intent，external 条目还必须绑定可解析的 `CatalogMount`。
- `External entry requires one existing Catalog mount id.` 表示 external 条目的 `externalCatalogBinding.catalogMountId` 为空或没有命中一个已声明的 concrete Catalog mount。Resource Manager 不会自动猜测 Catalog 所有权。

### 13.2 Global Resource Mapping Profile

全局 Resource Mapping Profile v2 是正式 Player 资源输出的 authoring authority。唯一编译器同时服务 Resource Manager preview 与 Unity build。路径：

```text
Assets/Config/MxFramework/ResourceProfiles/global_resource_mapping_profile.v2.json
```

推荐 Unity 工作台：

```text
MxFramework/Resources/Open Global AssetBundle Builder
```

工作台会显示当前 Profile 路径、active build target、generated catalog / Catalog mounts / preload groups / bundle dependencies / artifact-set marker / build report / bundle output folder 的状态，并展示 `CreateBuildPlan(...)` 产出的 profileId、catalogId、packageId、entry count、bundle count 和错误/警告数。点击 `Refresh / Validate Profile` 只刷新 build plan 与诊断，不写 Player 产物；点击 `Build Global Player Resources` 才调用正式 builder 生成本地 Player catalog、mount manifest、preload group、bundle dependency manifest、AssetBundle、artifact set 和 build report。`Copy Report` 会复制当前摘要、artifact 状态和诊断，artifact 行可打开或定位已生成文件。

低层 Unity 菜单仍保留给脚本化或快速调用：

```text
MxFramework/Resources/Validate Global Resource Mapping
MxFramework/Resources/Build Global Player Resources
```

`Validate` 只读取并校验 Profile，输出 entries、bundles 和错误诊断。`Build` 会基于 Profile 生成本地 Player 资源产物：

- `Assets/StreamingAssets/MxFramework/Resources/global_runtime_catalog.json`
- `Assets/StreamingAssets/MxFramework/Resources/global_catalog_mounts.json`
- `Assets/StreamingAssets/MxFramework/Resources/global_preload_groups.json`
- `Assets/StreamingAssets/MxFramework/Resources/global_bundle_dependencies.json`
- `Assets/StreamingAssets/MxFramework/Resources/global_resource_artifact_set.json`
- `Assets/StreamingAssets/MxFramework/Resources/Bundles/<BuildTarget>/...`
- `Assets/Config/MxFramework/ResourceBuildReports/global_resource_build_report.json`

这些输出是本机生成产物，包含当前 Unity build target、bundle hash、size 和生成时间，默认不提交到 Git。版本控制只提交 Build Profile、源资源和代码；需要 Player Offline 资源时，先在当前设备用 Global AssetBundle Builder 重新生成。正式发布或回归基线应由固定构建环境产出并走项目 artifact 渠道。

CharacterTest Windows package test 会在完整模式下复用同一个 Global Player Resource Catalog builder，然后再构建并运行 CharacterTest Development Player；它不是新的资源构建管线。

运行时可用 `GlobalResourceRuntimeBootstrap.CreateFromStreamingAssets(...)` 启动 Offline 资源系统。它必须先读取 `global_resource_artifact_set.json`，校验一整代必需产物的 identity 和 SHA-256，再从 marker 指定的路径读取 catalog、Catalog mounts、bundle dependencies 和 preload groups。声明的 concrete Catalog 必须同时通过 expected catalog id/fingerprint、精确 key allowlist、override intent 和 Provider 可用性校验；额外或未挂载 Catalog 会被拒绝。应用或测试自有、且不属于全局 Resource Mapping 的 Catalog，应在全局 bootstrap 完成后显式注册，不能绕过 mount 契约混入 composition root。

内容场景不通过 `IResourceManager.Load<T>` 加载。Profile 中 `Scene` 类型的 internal entry 会生成 provider 为 `sceneAssetBundle` 的 catalog entry；Offline bootstrap 发现该 provider 后会注册 `SceneAssetBundleProvider`，并在 `GlobalResourceRuntimeServices.SceneFlowController` 上暴露资源驱动的场景流。调用方用稳定业务 key 请求：

```csharp
using MxFramework.Resources;
using MxFramework.Resources.Unity;
using MxFramework.Runtime;

using var services = GlobalResourceRuntimeServices.Create(new GlobalResourceRuntimeOptions
{
    Mode = GlobalResourceRuntimeMode.Offline
});

SceneFlowController scenes = services.SceneFlowController;
SceneFlowResult result = scenes.RequestLoad(
    new SceneFlowRequest("scene.character_test", SceneFlowLoadMode.Single));
```

Driver 会按 `new ResourceKey(sceneKey, ResourceTypeIds.Scene)` 查 catalog，先加载 scene AssetBundle，再调用 Unity `SceneManager.LoadSceneAsync`。Player 仍需要一个 Boot/Bootstrap 场景在 Build Settings；被资源管理加载的内容场景不需要加入 Unity scene list。

CharacterTest 已接入这条链路：`GameSlice` 会在 `GlobalResourceRuntimeServices.SceneFlowController` 可用时注册 `CharacterTest.SceneFlow` runtime module，`GameManager.RequestSceneLoad(...)` / `RequestSceneUnload(...)` 可用稳定 scene key 驱动内容场景切换。当前生成的本地内容场景 key 是 `scene.character_test`；`GameManager` 默认启用 `preferResourceManagedSceneFlow`，会把 Inspector 中仍为 `Editor` 的资源模式提升为 `Offline`，以便直接使用 `StreamingAssets/MxFramework/Resources` 的 AssetBundle catalog。关闭该开关后才会保持配置的资源模式。

当前边界：

- 已完成：Resource Manager 加入/移出 Build Profile、保存 Profile、Refs 引用审查和建议草稿、Graph 只读关系图、Bundle Plan 预览、Global AssetBundle Builder 工作台、Unity 低层菜单校验、本地 Player AssetBundle/catalog/preload/dependency/report 生成，以及 internal scene bundle + SceneFlow 加载。
- 未完成：批量构建队列、增量/差分热更、CDN 发布、签名/加密、断点续传和多版本远端 manifest。
- 默认路线：Catalog + AssetBundleProvider + RemoteBundleProvider + Preload + diagnostics。Addressables 仅可选，YooAsset 不作为默认路线且本仓库不做 adapter。

Demo / 测试场景中的 UI、材质、图标、prefab 等运行时资源不应再放入 `Assets/Resources`。Runtime HUD 默认 `PanelSettings`、`GameplayShowcase.uxml`、`GameplayShowcase.uss` 已迁到 `Assets/UI/MxFramework/Showcase`，由 Runner、场景 bootstrap 或测试显式注入；纯调试材质优先运行时生成，不放入 `Resources`。

UI Toolkit 可玩 Demo 必须显式绑定正式 `PanelSettings`、Runtime Theme、UXML 和 USS。验收时不能只检查 visual tree 节点存在，还要在 Play Mode 读取关键 `Label` / `Button` 的 `text` 与 `resolvedStyle.color`，确认文本非空且 alpha > 0。若 Unity Runtime Theme / USS 在当前版本下不稳定，Demo controller 必须在初始化后对关键 `Label` / `Button` 设置 font、font size、color 和 alignment 作为兜底。

预加载 group / scene warmup 可以作为独立策略服务使用，不需要修改 `IResourceManager`：

```csharp
var preload = new ResourcePreloadService(resources);
IResourceOperation<ResourcePreloadResult> preloadOp =
    preload.PreloadAsync(new ResourcePreloadPlan(
        "combat",
        labels: new[] { "warmup.combat" },
        explicitKeys: new[] { new ResourceKey("demo.text.title", ResourceTypeIds.String) },
        maxConcurrentLoads: 2));

while (!preloadOp.IsDone)
{
    float progress = preloadOp.Progress;
    // Update loading UI from the main loop.
}

if (preloadOp.Result.Success)
{
    ResourcePreloadResult preloadResult = preloadOp.Result.Value;
    if (preloadResult.Success)
    {
        // Activate scene or open UI after warmup succeeds.
    }

    preload.ReleaseGroup(preloadResult.Handle);
}
```

`ResourcePreloadPlan.MaxConcurrentLoads` 控制同时启动的 load 数量，完成一个再补一个；默认 collect-all 模式会保留已加载 handle 并收集所有错误，`failFast: true` 会在首个失败后取消仍在途的 load。调用 `preloadOp.Cancel()` 或取消传入的 `CancellationToken` 会释放已经成功加载的 handles，并返回 `ResourceErrorCode.Cancelled`。Unity Provider 的 completion / progress 应在 Unity 主线程驱动，不能从 worker thread 访问 `UnityEngine.Object`、`AssetBundle` 或 UI Toolkit 对象。

Mod Package 可在 `mod.json` 中声明可选资源目录：

```json
{
  "resourceCatalog": "resources/catalog.json"
}
```

加载包后由组合根决定是否挂载：

```csharp
RuntimeModPackageLoadResult package = RuntimeModPackageLoader.LoadFromDirectory(packageRoot);
if (package.HasResourceCatalog)
{
    ResourceCatalog catalog = StreamingResourceCatalogLoader.LoadFromFile(package.ResourceCatalogFilePath);
    resources.AddCatalog(catalog);
}
```

运行时诊断：

```csharp
var debugSource = new ResourceDebugSource(resources);
string report = FrameworkDebugReportExporter.ExportText(debugSource.CreateSnapshot());
```

Editor 校验：

```csharp
ResourceCatalogValidationReport validation =
    ResourceCatalogEditorValidator.ValidateCatalog(catalog, new[] { "resources", "assetBundle" });
```

框架样例 Catalog 由 Editor 生成器维护，菜单路径：

```text
MxFramework/Samples/Generate Resource Catalog
```

生成器会输出 `Assets/Config/MxFramework/ResourceCatalogs/mxframework_samples_resource_catalog.json`，并校验 `memory` provider 的 `providerData.assetPath` 是否指向正式样例资源。该 catalog 仍服务 Editor Play Mode / Demo 组合根；Player 资源路径应由后续 AssetBundle / Streaming catalog 接管。

Player smoke 资源路径由独立生成器维护，菜单路径：

```text
MxFramework/Samples/Build Player Resource Catalog
```

生成器会构建 `Assets/StreamingAssets/MxFramework/Samples/Bundles/mxframework.samples.start_screen.assetbundle`，并输出 `Assets/StreamingAssets/MxFramework/Samples/mxframework_samples_player_catalog.json`。该 catalog 使用 `assetBundle` provider，可通过 `StreamingResourceCatalogLoader.LoadFromStreamingAssets("MxFramework/Samples/mxframework_samples_player_catalog.json")` 挂载到 `ResourceManager`，bundle root 为 `Path.Combine(Application.streamingAssetsPath, "MxFramework/Samples/Bundles")`。

当前 smoke 只验证本地文件系统 / 桌面 Player 兼容路径。Android 和 WebGL 的 StreamingAssets 读取不能依赖同步 `System.IO.File` 或 `AssetBundle.LoadFromFile`，需要后续 provider / loader 使用 `UnityWebRequest` 或平台专用读取策略。

`RuntimeVerticalSliceRunner` 默认会在启动资源 warmup 时执行两条路径：

- Editor Play Mode / Demo serialized reference path：`mxframework.samples` memory catalog + `MemoryResourceProvider`。
- Player smoke path：`StreamingResourceCatalogLoader` + `AssetBundleProvider` + `ResourcePreloadService`，使用上方 Player catalog 和 bundle root。

运行结果会写入 `ResourceWarmupSummary` 和 `ResourceBindingLogLines`，其中包含 warmup、direct load、release 后 `ResourceDebugSnapshot` loaded/ref-count，以及 Player bundle 计数。外层组合根需要显式注入 catalog、provider 和 UI Toolkit 资产；旧 Runtime Preview 不再作为资源持有者或验证入口。

约定：

- 业务配置只保存 `ResourceKey`，不保存 Unity 路径或 `UnityEngine.Object`。
- `ResourcesProvider` 只用于 Demo、小样例和少量常驻资源。
- Catalog JSON 覆盖字段使用 `allowOverride`。
- `PackageId` 非空时精确路由；为空时走全局覆盖结果。
- Variant fallback 必须显式配置；框架不会自动把 `pc.high` 拆成 `pc` / `high`。
- RetainPolicy 不改变外部 handle 语义，`Release(handle)` 后 handle 仍为 released。
- RemoteBundle 第一版只提供 file/local HTTP 最小闭环和 SHA-256 校验；复杂重试、断点续传、签名和 CDN 发布不在框架当前范围内。
- Addressables Adapter 后置为可选项，不进入 `MxFramework.Resources.Unity` 默认依赖。
- Catalog labels 可用于 warmup 分组，例如 `warmup.combat`、`scene.demo`、`ui`。
- Preload group 只持有并释放 asset handles，不负责场景激活、进度 UI 或实例销毁。
- Runtime Showcase 的资源测试直接使用 `mxframework.samples`：`RuntimeVerticalSliceRunner` 会在 Play Mode 预热 package / StartScreen / Combat / StatusEffects / MagicEffects labels，按 `ResourceKeyConfigProfile.CreateSample()` 直接加载 Katana、StatusAura Prefab、StartScreen 贴图和 MagicEffects AudioClip，并输出加载 / 引用计数 / 释放后的诊断结果。MagicEffects label 同时包含 `vfx.magic.fireball`，用于验证 VFX prefab 也能通过 sample catalog 预热和释放。
- `GameObject` 实例由调用方销毁，资源系统只管理 prefab asset handle。

## 13.5 Items 物品、库存和装备投影

Items V0/V0.3 是 noEngine 物品生命周期基础：`MxFramework.Items` 保存定义，`MxFramework.Items.Loot` 解析掉落结果，`MxFramework.Items.World` 保存 active world items 和 pickup，`MxFramework.Items.Runtime` 保存 slot-based inventory runtime，`MxFramework.Items.Acquisition` 提供直接发放 / 购买式 cost+grant 的底层事务，`MxFramework.Items.CharacterApplicationBridge` 把装备容器投影到现有 Character.Application 装备解析器。Unity 3D 掉落表现、完整商店 UI/商品目录、交易、制作、背包 UI、耐久、随机词条或真实消耗品效果仍不属于 Items core。

```csharp
using MxFramework.Config;
using MxFramework.CharacterApplication;
using MxFramework.Items;
using MxFramework.Resources;
using MxFramework.Runtime;

var definitions = new ItemDefinitionRegistry();
definitions.Register(new ItemDefinition(
    new ItemDefinitionId(1001),
    "item.herb",
    new LocalizedTextKey("item.herb.name"),
    new LocalizedTextKey("item.herb.desc"),
    ItemKind.Material,
    new ItemTagSet(new[] { 10 }),
    new ItemStackPolicy(20),
    new[] { new ItemResourceRef(new ResourceKey("item.herb.icon", ResourceTypeIds.Sprite), ItemResourceUsageIds.Icon) },
    new[] { new ItemProfileRef(ItemProfileTypeIds.Generic, "item.herb.profile") }));

var store = new InventoryStore();
var bag = new InventoryContainer(new InventoryContainerId(1), new InventoryOwnerId("owner.hero"), 12, "bag");
store.Register(bag);

var service = new InventoryService(definitions, store);
service.AddStack(bag.ContainerId, 0, new ItemStack(new ItemDefinitionId(1001), 3));
```

自动插入路径用于拾取、奖励和宝箱结算。它先规划容量，容量不足时不产生部分修改：

```csharp
InventoryOperationResult insert =
    service.TryInsertStack(bag.ContainerId, new ItemStack(new ItemDefinitionId(1001), 37));
```

非装备 definition 必须显式提供正数 `ItemStackPolicy`；装备可传默认 policy，并自动归一为单件堆叠。库存合并还要求双方都是非实例 stack，且 custom-state type/schema/payload 内容等价；不同 custom state 会保留在不同 slot，不会被合并覆盖。

直接发放和购买底座走 acquisition 事务；上层先验证商品/奖励/权限，再提交 cost + grant。底层只保证库存变化原子和 request id 幂等：

```csharp
var ledger = new ItemAcquisitionLedger();
var transaction = new InventoryTransactionService(service, ledger);

ItemAcquisitionResult reward = transaction.Execute(new ItemAcquisitionRequest(
    new ItemAcquisitionRequestId(1000001),
    ItemAcquisitionSourceKind.DirectGrant,
    costs: null,
    grants: new[]
    {
        new ItemGrantLine(bag.ContainerId, new ItemDefinitionId(1001), 5)
    }));

ItemAcquisitionResult purchase = transaction.Execute(new ItemAcquisitionRequest(
    new ItemAcquisitionRequestId(2000001),
    ItemAcquisitionSourceKind.ShopPurchase,
    costs: new[]
    {
        new ItemCostLine(bag.ContainerId, new ItemDefinitionId(9001), 30)
    },
    grants: new[]
    {
        new ItemGrantLine(bag.ContainerId, new ItemDefinitionId(1001), 2)
    }));
```

`ShopPurchase` 在这里不是完整 Shop 模块：价格表、折扣、限购、平台 receipt、邮件兜底都由上层处理。Acquisition 只消费库存内普通 stack 作为 cost，并在成本不足、容量不足或重复 `ItemAcquisitionRequestId` 时保持库存不变。

Loot 只解析产物，不改库存、不生成地上物：

```csharp
var lootTable = new LootTableDefinition(
    new LootTableId(1),
    "loot.demo.chest",
    new[]
    {
        new LootGroupDefinition(
            "materials",
            rollCountMin: 1,
            rollCountMax: 1,
            activationChanceBasisPoints: LootContext.BasisPointsScale,
            emptyWeight: 0,
            entries: new[]
            {
                new LootEntryDefinition(new ItemDefinitionId(1001), weight: 100, countMin: 2, countMax: 4)
            })
    });

LootResolveResult loot = new LootResolver(definitions).Resolve(
    lootTable,
    new LootContext("chest.demo", sourceLevel: 1, contextTags: new ItemTagSet(null)),
    new DeterministicRandom(1234u));
```

World item runtime 表达“地上的物品”，pickup 成功后才原子移除 world item：

```csharp
var worldStore = new WorldItemStore();
var worldService = new WorldItemService(worldStore, service);
worldService.Spawn(new WorldItemStack(
    new WorldItemId(1),
    new ItemStack(new ItemDefinitionId(1001), 3),
    new WorldItemPosition(2, 0, 0),
    new InventoryOwnerId("owner.hero"),
    WorldItemAccessPolicy.Public,
    spawnFrame: 0,
    expireFrame: 600));

WorldItemOperationResult pickup = worldService.Pickup(
    new WorldItemId(1),
    new InventoryOwnerId("owner.hero"),
    bag.ContainerId,
    RuntimeFrame.Zero);
```

命令路径使用 request-store 模式，`RuntimeCommand` 只携带 handle：

```csharp
var module = new InventoryRuntimeModule(service);
InventoryRequestHandle handle = module.Requests.Add(new InventoryRequest(
    InventoryRequestKind.RemoveStack,
    bag.ContainerId,
    sourceSlotIndex: 0,
    count: 1));

module.Commands.Enqueue(InventoryRuntimeCommandFactory.Create(
    RuntimeFrame.Zero,
    sourceId: 7,
    ItemRuntimeCommandIds.RemoveStack,
    handle,
    "inventory.remove"));
module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));
```

装备桥接使用一个特殊 inventory container 表达装备栏，slot index 对齐 `EquipmentSchemaConfig.Slots`。投影成功后继续交给现有 `EquipmentStateResolver`，旧 `WeaponConfig` 路径不需要迁移：

```csharp
CharacterItemEquipmentProjectionResult projection =
    CharacterItemEquipmentProjector.Project(
        equipmentContainer,
        equipmentSchema,
        new EquipmentLoadoutId(760101),
        "loadout.item_backed",
        definitions,
        equipmentProfiles);

if (projection.IsSuccess)
{
    EquipmentStateResolveResult resolved = EquipmentStateResolver.Resolve(
        equipmentSchema,
        projection.Loadout,
        projection.Weapons,
        equipmentStates);
}
```

装备槽位和常用武器 tag 不需要手写字符串：

```csharp
string requiredBlade =
    EquipmentMatchRuleToken.Create(CharacterEquipmentSlotIds.MainHand, CharacterWeaponTagIds.Blade);
```

`InventoryHashContributor` / `ItemAcquisitionHashContributor` / `WorldItemHashContributor` 参与 Runtime hash；`InventorySaveStateProvider` 保存 custom state type `ItemRuntimeIds.InventoryModule`（wire string 为 `mxframework.items.inventory`），`ItemAcquisitionSaveStateProvider` 保存 custom state type `ItemRuntimeIds.AcquisitionModule`（wire string 为 `mxframework.items.acquisition`），`WorldItemSaveStateProvider` 保存 custom state type `ItemRuntimeIds.WorldItemsModule`（wire string 为 `mxframework.items.world`）。Inventory / World restore 会比较 definition hash，不匹配则结构化失败；Inventory 还会先在临时 store 验证完整 payload，非法 container/slot/stack 返回 `InvalidDocument` 并保留 live store。Acquisition SaveState 保存已提交 request id ledger，防止恢复后重复发放。库存操作、world item command 和 hash 目标是初始化后 `NoAlloc`；definition 加载、acquisition transaction、request 构造和 SaveState JSON 是低频分配路径。

Item Lifecycle Showcase 是专用物品生命周期验证入口，规则层位于 `Assets/Scripts/WGameFrameworkProject/Demos/Items/`，UI 位于 `Assets/UI/MxFramework/ItemLifecycle/`。详细手测流程见 `Docs/Demo/ITEM_LIFECYCLE_SHOWCASE.md`。打开 `Assets/Scenes/ItemLifecycleShowcase.unity` 后 Play；按钮是权威操作入口，移动、拾取、选择装备槽、装备、卸下、保存和 hash 都通过 Runtime command / Items runtime 路径执行。Iron Sword 额外验证 `ItemDefinition.ResourceRefs -> ResourceKey(showcase.item.iron_sword.icon:Sprite) -> ResourceManager -> Sprite -> UI Toolkit` 图标链路；图标只属于表现资源，不进入 SaveState、hash 或网络 payload。旧输入管理器启用时才额外支持键盘快捷键：

- `Open Loot` / `O`：解析 loot 并生成 world item。
- `-X` / `+X` / `-Z` / `+Z` 或 `WASD` / 方向键：移动逻辑格子位置。
- `Pickup` / `Space`：拾取 1 格内 world item，成功后进入背包。
- `Equip` / `E`：把背包里的测试武器移动到当前选中的 Main Hand / Off Hand equipment slot 并投影为 Character.Application weapon profile。
- `Unequip`：把当前选中的 equipment slot 原子移回第一个空背包槽。
- `Save` / `Load` / `Hash`：验证 inventory + world item SaveState 和 state hash。

联机模式不要把 request handle 或 catalog 数据发过网络。组合根用 `MxFramework.Items.Network` 编码 compact command，让 server 授权、执行，再把接受后的同一条 authoritative command 广播给 clients：

```csharp
using MxFramework.Items.Network;
using MxFramework.Network;

RuntimeCommand move = InventoryNetworkCommandCodec.CreateMoveStack(
    frame,
    sourceId: localPeerNumericId,
    bag.ContainerId,
    sourceSlotIndex: 0,
    equipment.ContainerId,
    destinationSlotIndex: 0,
    count: 1,
    traceId: "inventory.move");

// Client sends move inside NetworkRuntimeCommandPacket.
// Server commandAuthorizer:
var authority = new InventoryOwnerNetworkAuthority(serverInventoryStore);
authority.SetOwnerForPeer("peer.hero", new InventoryOwnerId("owner.hero"));
var inventoryAuthorizer = new InventoryNetworkCommandAuthorizer(authority);

NetworkCommandAuthorizationResult result =
    inventoryAuthorizer.Authorize("peer.hero", move);
```

`AddStack` / `RemoveStack` 默认只给 server 或显式授权来源使用：

```csharp
authority.GrantAllContainerAccess("server");
authority.GrantStackMutationAuthority("server");
```

bootstrap / late join / resync 使用 full snapshot participant；常规同步仍走 authoritative command stream：

```csharp
var inventorySnapshot = new InventoryNetworkSnapshotParticipant(store, definitions);
var inventoryRollback = new InventoryRollbackParticipant(
    store,
    definitions,
    historyCapacity: 128);

// Register inventorySnapshot with NetworkSessionRunner participants.
// Register inventoryRollback with NetworkPredictionRollbackCoordinator.
```

snapshot payload 只包含 `definitionHash`、container/owner/slot/stack/custom state envelope；不包含 localized text、resource key、profile refs、definition JSON 或 SaveState wrapper。Rollback participant 不走 JSON；它用固定容量的 reusable state ring 捕获内存状态，`historyCapacity` 应不小于 rollback coordinator 的 history capacity。participant 构造时缓存 definition hash，因此要先完成 definition registry 加载，再创建 participant；session 期间不得修改 registry。固定库存布局和容量 warmup 后，rollback capture 为 `NoAlloc`。

## 14. Animation 表现层

Animation 模块把业务侧 presentation 意图转换为动画播放请求。noEngine 层只保存 `ResourceKey`、layer id、request DTO 和 diagnostics；Unity 播放由 `MxFramework.Animation.Unity` 的 Playables backend 负责。

Locomotion clip 可以在 `animation_authoring.json` 的 clip `calibration` 中声明 `nativeVelocityX` / `nativeVelocityY`、`playbackSpeed`、`cycleDurationSeconds` 以及左右脚 `footContactWindows`。Authoring Compiler 会把这些数据写入 `animation_set_definition.json`，运行时通过 `MxAnimationSetDefinition.LocomotionClipCalibrations` 或 `TryFindLocomotionClipCalibration` 读取；缺失 locomotion 校准数据会产生 `LOCO_CAL_CLIP_METADATA_MISSING` warning，并且校准数据参与 `DefinitionHash`。

```csharp
using MxFramework.Animation;
using MxFramework.Animation.Unity;
using MxFramework.Resources;
using UnityEngine;

var idle = new ResourceKey("demo.animation.idle", ResourceTypeIds.AnimationClip);
var walk = new ResourceKey("demo.animation.walk", ResourceTypeIds.AnimationClip);
var run = new ResourceKey("demo.animation.run", ResourceTypeIds.AnimationClip);
var left = new ResourceKey("demo.animation.left", ResourceTypeIds.AnimationClip);
var right = new ResourceKey("demo.animation.right", ResourceTypeIds.AnimationClip);
var forward = new ResourceKey("demo.animation.forward", ResourceTypeIds.AnimationClip);
var backward = new ResourceKey("demo.animation.backward", ResourceTypeIds.AnimationClip);
var fallback = new ResourceKey("demo.animation.fallback", ResourceTypeIds.AnimationClip);
var upperBodyMask = new ResourceKey("demo.animation.mask.upper_body", ResourceTypeIds.AvatarMask);
var set = new MxAnimationSetDefinition(
    "demo.actor",
    version: 1,
    defaultClip: idle,
    fallbackClip: fallback,
    layers: new[]
    {
        new MxAnimationLayerDefinition(MxAnimationLayerId.Base),
        new MxAnimationLayerDefinition(
            new MxAnimationLayerId("upper_body"),
            profileId: "combat.upper_body",
            defaultWeight: 0f,
            blendMode: MxAnimationLayerBlendMode.Override,
            avatarMaskKey: upperBodyMask)
    },
    warmup: new MxAnimationWarmupDefinition(
        groupId: "combat.demo.actor",
        labels: new[] { "warmup.combat" }),
    blend1DDefinitions: new[]
    {
        new MxAnimationBlend1DDefinition(
            blendId: "locomotion",
            parameterId: "locomotion.speed",
            layerId: MxAnimationLayerId.Base,
            points: new[]
            {
                new MxAnimationBlend1DPoint(0, idle),
                new MxAnimationBlend1DPoint(500, walk),
                new MxAnimationBlend1DPoint(1000, run)
            },
            parameterScale: 1000)
    },
    blend2DDefinitions: new[]
    {
        new MxAnimationBlend2DDefinition(
            blendId: "locomotion2d",
            parameterXId: "locomotion.x",
            parameterYId: "locomotion.y",
            layerId: MxAnimationLayerId.Base,
            points: new[]
            {
                new MxAnimationBlend2DPoint(-1000, 0, left),
                new MxAnimationBlend2DPoint(1000, 0, right),
                new MxAnimationBlend2DPoint(0, 1000, forward),
                new MxAnimationBlend2DPoint(0, -1000, backward)
            },
            parameterXScale: 1000,
            parameterYScale: 1000)
    },
    compatibilityExpectation: new MxAnimationCompatibilityExpectation(
        skeletonProfileId: "humanoid",
        skeletonProfileHash: "sha256:skeleton-profile",
        requiredBonePaths: new[] { "Hips/Spine" },
        requiredSocketPaths: new[] { "Hips/Spine/WeaponSocket" },
        clipExpectations: new[]
        {
            new MxAnimationClipCompatibilityExpectation(idle, new[] { "Hips/Spine" })
        },
        avatarMaskExpectations: new[]
        {
            new MxAnimationAvatarMaskCompatibilityExpectation(upperBodyMask, new[] { "Hips/Spine" })
        }));

// resources 由组合根注册 provider + catalog；clip / AvatarMask 必须通过 ResourceKey 解析。
var backend = new UnityPlayablesAnimationBackend(animator, resources, set, "actor.demo");
backend.Play(new MxAnimationPlayRequest { ClipKey = idle });
backend.SetBlend1D(new MxAnimationBlend1DRequest
{
    BlendId = "locomotion",
    Parameter = new MxAnimationQuantizedParameter("locomotion.speed", 750)
});
backend.SetBlend2D(new MxAnimationBlend2DRequest
{
    BlendId = "locomotion2d",
    ParameterX = new MxAnimationQuantizedParameter("locomotion.x", 250),
    ParameterY = new MxAnimationQuantizedParameter("locomotion.y", 500)
});
backend.SetLayerWeight(new MxAnimationLayerWeightRequest
{
    LayerId = new MxAnimationLayerId("upper_body"),
    Weight = 1f,
    FadeDurationSeconds = 0.12f
});
backend.Tick(Time.deltaTime);

MxAnimationDiagnosticSnapshot snapshot = backend.CreateSnapshot();
backend.Release();
```

从 catalog 构建 clip registry，并用 mapping provider 交给组合根：

```csharp
ResourceCatalog catalog = LoadCatalog();
MxAnimationClipRegistry registry = MxAnimationClipRegistryBuilder.FromCatalog(
    catalog,
    version: 1,
    catalogHash: "catalog-hash");

var provider = new MxAnimationStaticMappingProvider(new[] { set });
if (provider.TryFindDefinition("demo.actor", out MxAnimationSetDefinition mappedSet))
{
    string mappingHash = mappedSet.DefinitionHash;
}
```

事件时间轴和 dispatch sink 使用同一份 noEngine DTO，不直接保存 Unity object：

```csharp
IReadOnlyList<MxAnimationEventTimelineRow> rows =
    MxAnimationEventTimelineBuilder.BuildRows(mappedSet);

var eventSink = new MxAnimationPresentationEventDispatchSink(mySink, maxDedupeEntries: 128);
var dispatch = new MxAnimationPresentationEventDispatch(
    actorId: "entity:7",
    actionKey: "action:1001",
    bindingId: "attack",
    actionInstanceId: 3,
    worldFrame: 120,
    localFrame: 6,
    sourceOrder: 0,
    presentationEvent: rows[0].Event);

eventSink.TryDispatch(dispatch, payloadResolved: true, out MxAnimationPresentationEventDispatchDiagnostic diagnostic);
```

进入战斗、生成角色或 late join 前可以先做 warmup / version validation：

```csharp
var preloadService = new ResourcePreloadService(resources);
var warmupService = new MxAnimationWarmupService(preloadService);
MxAnimationCompatibilityProfile compatibilityProfile = LoadOrExtractCompatibilityProfile();
MxAnimationWarmupResult warmup = warmupService.Warmup(new MxAnimationWarmupRequest(
    mappedSet,
    registry,
    catalog,
    syncState,
    compatibilityProfile: compatibilityProfile));

if (!warmup.Success)
{
    foreach (MxAnimationWarmupIssue issue in warmup.Issues)
        Log(issue.Code, issue.Key, issue.Field, issue.Expected, issue.Actual, issue.Message);

    // 不要把失败当作空播成功；由组合根选择 fallback、重试或阻断进入表现层。
}

// 战斗结束或 actor 销毁时释放 warmup group。
warmupService.Release(warmup);
```

如果项目层资源 provider 会跨帧完成加载，改用 `warmupService.WarmupAsync(request)`，轮询返回的 `IResourceOperation<MxAnimationWarmupResult>.IsDone` 后再读取 `Result`。同步 `Warmup` 只覆盖立即完成路径；遇到 pending preload 会返回 `PreloadOperationPending` issue，并要求调用方切到异步路径。

如果同一份 mapping 要在 sample memory provider、本地 AssetBundle、remote Bundle 或项目层可选 Addressables adapter 间切换，Animation 层只额外声明 package expectation；provider 差异仍留在 `ResourceCatalogEntry`：

```csharp
var packageExpectation = new MxAnimationPackageExpectation(
    "mx.anim.demo",
    version: 2,
    catalogId: "mx.anim.demo.catalog",
    catalogHash: "sha256:catalog",
    acceptedProviderIds: new[] { "memory", "assetBundle", "remoteBundle", "addressables" },
    resources: new[]
    {
        new MxAnimationPackageResourceExpectation(
            new ResourceKey("demo.animation.attack", ResourceTypeIds.AnimationClip),
            catalogEntryHash: "sha256:clip"),
        new MxAnimationPackageResourceExpectation(
            new ResourceKey("demo.animation.mask.upper_body", ResourceTypeIds.AvatarMask),
            catalogEntryHash: "sha256:mask"),
        new MxAnimationPackageResourceExpectation(
            new ResourceKey("demo.animation.bake.attack", MxAnimationResourceTypeIds.BakeArtifact),
            catalogEntryHash: "sha256:bake"),
        new MxAnimationPackageResourceExpectation(
            new ResourceKey("demo.animation.profile.humanoid", MxAnimationResourceTypeIds.CompatibilityProfile),
            catalogEntryHash: "sha256:profile")
    });

MxAnimationWarmupResult packageWarmup = warmupService.Warmup(new MxAnimationWarmupRequest(
    mappedSet,
    registry,
    catalog,
    syncState,
    null,
    true,
    compatibilityProfile,
    packageExpectation,
    new MxAnimationPackageCatalog(catalog, version: 2, catalogHash: "sha256:catalog")));
```

`MxAnimationPackageCatalogValidator` 会报告 package id/version/catalog hash mismatch、provider 不匹配、entry hash mismatch、missing clip/mask/bake/profile；`RequiredForWarmup=true` 的 package resources 会进入同一个 preload group。Addressables 仍是可选 adapter：项目可把 provider id 写入 `AcceptedProviderIds`，但 MxFramework 默认程序集不引用 Addressables。

Mod animation package 通过 noEngine override DTO 覆盖表现 mapping。合并前必须校验 base hash/version、catalog/package、compatibility；成功后再 warmup：

```csharp
var manifest = new MxAnimationModPackageManifest(
    "mod.anim.demo",
    version: 2,
    displayName: "Demo Animation Mod",
    catalogId: "mod.anim.demo.catalog",
    catalogHash: "sha256:catalog",
    loadOrder: 10);

var modOverride = new MxAnimationModOverrideDefinition(
    targetSetId: mappedSet.SetId,
    manifest: manifest,
    overrideVersion: 1,
    expectedBaseVersion: mappedSet.Version,
    expectedBaseHash: mappedSet.DefinitionHash,
    actionOverrides: new[]
    {
        new MxAnimationActionBindingOverride(new MxAnimationActionBinding(
            "attack",
            "action:attack",
            new ResourceKey("demo.animation.attack.mod", ResourceTypeIds.AnimationClip, packageId: "mod.anim.demo"),
            new MxAnimationLayerId("upper_body")))
    },
    layerOverrides: new[]
    {
        new MxAnimationLayerDefinitionOverride(new MxAnimationLayerDefinition(
            new MxAnimationLayerId("upper_body"),
            avatarMaskKey: new ResourceKey("demo.animation.mask.mod", ResourceTypeIds.AvatarMask, packageId: "mod.anim.demo")))
    },
    packageResources: new[]
    {
        new MxAnimationPackageResourceExpectation(
            new ResourceKey("demo.animation.bake.attack.mod", MxAnimationResourceTypeIds.BakeArtifact, packageId: "mod.anim.demo"),
            catalogEntryHash: "sha256:bake")
    },
    compatibilityExpectation: LoadModCompatibilityExpectation(),
    acceptedProviderIds: new[] { "assetBundle", "remoteBundle" });

MxAnimationModOverrideMergeResult merge = MxAnimationModOverrideMerger.Merge(
    new MxAnimationModOverrideMergeRequest(
        mappedSet,
        modOverride,
        modCatalog,
        compatibilityProfile,
        new MxAnimationPackageCatalog(modCatalog, version: 2, catalogHash: "sha256:catalog"),
        packageExpectation));

if (!merge.Success)
{
    foreach (MxAnimationModOverrideIssue issue in merge.Issues)
        Log(issue.Code, issue.Field, issue.Expected, issue.Actual, issue.Message);

    // 不要强制播放 rejected override；继续使用 base mapping 或阻断加载。
}
```

`merge.MergedDefinition.DefinitionHash` 是合并后稳定 mapping hash；`merge.BaseDefinitionHash` / `merge.OverrideHash` 保留审计所需的 base 与 mod 输入。资源生命周期仍通过 `MxAnimationWarmupService`：把 `merge.MergedDefinition` 和 `merge.MergedPackageExpectation` 传给 warmup，释放时调用 `warmupService.Release(result)`。

Unity Editor 内可以创建 `MxFramework/Animation/Clip Registry` asset，用 Inspector 填写 clip/action/binding 映射，再用 `Validate Mapping Structure` 做无 catalog 的结构校验。Inspector 的 `Event Timeline Preview` 会显示 Seconds / NormalizedTime / CombatFrame / PresentationFrame 事件，并对 CombatFrame / PresentationFrame 输出 deterministic correlation 摘要。正式导出或 CI 校验应调用 exporter 并传入 `ResourceCatalog`，检查 catalog entry、typeId、variant 和 package。该 asset 可以引用 `AnimationClip`，但运行时仍只使用导出的 `ResourceKey` mapping。

只读逐帧检查可以打开 `MxFramework / MxAnimation / Timeline Scrubber Preview MVP`，或在 Clip Registry Inspector 点击 `Open Timeline Scrubber Preview`。窗口选择 action binding 和 frame 后，会显示同帧 presentation event、CombatActionTimeline phase/window/frame event、baked root/socket/weapon trace sample，以及 missing clip、missing bake、hash/source mismatch、event out of range、timeline frame mismatch diagnostics。该工具只做 preview，不编辑 registry、Combat authoring 或 runtime DTO。

Bake MVP 入口用于把选中的 `AnimationClip` 转成 fixed-frame、量化的派生参考数据：

```text
MxFramework / MxAnimation / Bake Selected Animation Clip MVP
```

生成的 `.mxbake.txt` artifact 报告包含 source clip hash、bake profile hash、skeleton/avatar profile hash、artifact hash、root motion reference、weapon trace reference 和 event markers。它只用于 authoring / preview / Combat 参考数据输入；运行时 Combat 不读取 Animator、PlayableGraph 或 Unity bone pose 当前状态。

Skeleton / Avatar / Clip compatibility 可以由 Editor 提取器生成 noEngine profile，再交给 warmup、CI 或 bake 校验：

```csharp
MxAnimationSkeletonCompatibilityProfile skeletonProfile =
    MxAnimationCompatibilityEditorExtractor.CreateSkeletonProfile(
        characterRoot,
        "humanoid",
        socketPaths: new[] { "Hips/Spine/WeaponSocket" });

MxAnimationCompatibilityProfile compatibilityProfile =
    MxAnimationCompatibilityEditorExtractor.CreateProfile(
        skeletonProfile,
        new[] { MxAnimationCompatibilityEditorExtractor.CreateClipProfile(idleClip, idle, skeletonProfile) },
        new[] { MxAnimationCompatibilityEditorExtractor.CreateAvatarMaskProfile(upperBodyAvatarMask, upperBodyMask, skeletonProfile) });
```

约定：

- `MxFramework.Animation` 不引用 Unity，不保存 `AnimationClip`、GUID 或 `Assets/...` path。
- `MxFramework.Animation.Unity` 通过 `IResourceManager.LoadAsync<AnimationClip>` 获取 backend 自己拥有的 handle。
- AvatarMask 与 clip 一样使用 `ResourceKey` 和 `ResourceManager` 加载；runtime definition 不直接保存 `AvatarMask` 引用。
- layer weight 只影响 Unity Playables layer mixer 的表现权重，可用于上半身攻击、下半身移动这类视觉混合；它不改变 Combat authority。
- 1D locomotion blend 使用 `MxAnimationBlend1DDefinition` 和 `MxAnimationBlend1DRequest`；2D locomotion blend 使用 `MxAnimationBlend2DDefinition` 和 `MxAnimationBlend2DRequest`。point clip key 进入 definition hash、mapping validation 和 warmup，实际 clip 仍由 backend 通过 `ResourceManager` 加载。
- `MxAnimationBlend1DRequest` / `MxAnimationBlend2DRequest` 会转换成共享 `MxAnimationBlendRequest`，backend 再走同一条 clip weight -> Playables slot mixer 路径。
- 2D blend 权重计算在 noEngine 层使用量化整数参数完成，Unity backend 只消费 clip weight 列表；它不能把 Unity 当前动画姿态或 Playable time 写回权威逻辑。
- compatibility profile 只保存 skeleton id/hash、bone/socket path、clip binding path、AvatarMask active path 和 `ResourceKey`；不得把 Unity object、GUID 或 asset path 写入 noEngine runtime contract。
- bake artifact 必须作为派生缓存处理。source/profile/skeleton/artifact hash mismatch 必须输出 diagnostics，不能静默使用过期数据。
- bake artifact 可和 `MxAnimationCompatibilityExpectation` 一起校验 skeleton profile id/hash，避免用错误骨架生成的 trace/root motion reference 混入运行时。
- `MxAnimationSetDefinition.DefinitionHash` 是稳定 mapping hash；加载侧可用它和 catalog hash / registry version 检测过期数据。
- warmup 复用 `ResourcePreloadService` 和 catalog labels；hash/version mismatch、missing clip、wrong type、compatibility mismatch 或 partial failure 都会产生结构化 `MxAnimationWarmupIssue`。
- package expectation 复用同一套 `ResourceKey` mapping，允许 memory / AssetBundle / remote Bundle / 可选 Addressables provider 切换；错误包、错误版本、错误 hash、missing bake/profile 不能静默播放。
- Mod animation override 只改表现 mapping 和 package resource expectation；合并结果必须先通过 catalog/package/compatibility validation，不能修改 Combat authority。
- warmup group release 只释放预热持有的 handles，不会误释放其它 consumer 正在持有的同一 clip。
- Editor clip registry 只是 authoring 入口，不允许作为运行时资源加载捷径。
- presentation event 是表现层事件，不驱动 Combat 命中、取消、伤害、无敌、移动或 replay hash。默认 late join 不补播一次性 VFX/SFX；需要补播时必须把事件标记为 `CatchUpSafe` 并由项目网络层显式执行策略。
- runtime presentation event sink 使用 actor/action instance/frame/event/source-order dedupe key；重复 dispatch 会被丢弃并输出 diagnostics。
- default / fallback clip 按 backend 生命周期常驻，并在 diagnostics 中显示 resident 状态。
- crossfade outgoing clip 在权重归零且 playable 从 graph 断开后释放。
- 加载失败会记录 `ResourceError`，先尝试 actor fallback；fallback 也失败时 layer 进入 failed state。
- `Tick(deltaTime)` 由外部传入 presentation delta。这个时间源只用于表现层，不进入 Combat authority、Replay hash 或命中/取消判定。
- `MxFramework.Combat.Animation.Unity` 提供 Combat 到 MxAnimation 的首版 Unity 表现桥；旧 `CombatAnimatorDriver` 仍保留为 opt-in 迁移路径，未被替换。

详细接口见 `Docs/Interfaces/Animation.md`，测试入口为 `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Animation/`。

## 15. Input 输入系统

Input 模块把 Unity Input System 的 Action/Binding 封装成业务意图。角色、相机、UI 和自动化测试依赖 `IInputProvider`，不直接读设备或 `InputAction`。

```csharp
public sealed class CharacterControllerAdapter
{
    private readonly IInputProvider _input;

    public CharacterControllerAdapter(IInputProvider input)
    {
        _input = input;
    }

    public void Tick(float deltaTime)
    {
        Vector2 move = _input.Snapshot.Move;
        bool jump = _input.Snapshot.JumpPressed;
    }
}
```

Unity 场景接入：

1. 确认项目安装 `com.unity.inputsystem`。
2. 创建或复用 `InputActionAsset`，Action Map 默认使用 `Gameplay` / `UI`。
3. 场景中挂 `InputService`，把 asset 指到 `Assets/Input/MxFramework/Config/MxFrameworkInputActions.inputactions` 或项目自己的主输入资产。
4. 打开菜单、背包、弹窗时通过 `PushContext(InputContext.UI)` 临时切换；玩家 modal 使用默认 exclusive 策略来屏蔽 Gameplay / Debug 等非 UI action map。需要释放鼠标的玩家 modal 同时通过 `MxCursorController` push released cursor scope；关闭时释放返回的 `IDisposable`。

```csharp
var cursor = new MxCursorController();
IDisposable uiScope = input.PushContext(InputContext.UI);
IDisposable cursorScope = cursor.Push(MxCursorRequest.Released("Game.UI.Modal"));
cursorScope.Dispose();
uiScope.Dispose();
```

测试和 AI 接管可以直接替换输入源：

```csharp
var fake = new FakeInputProvider();
fake.SetContext(InputContext.Gameplay);
fake.SetSnapshot(new InputSnapshot(
    move: new Vector2(1f, 0f),
    look: Vector2.zero,
    navigate: Vector2.zero,
    point: Vector2.zero,
    scroll: Vector2.zero,
    throttle: 0f,
    jumpPressed: true,
    jumpHeld: true,
    jumpReleased: false,
    attackPrimaryPressed: false,
    attackPrimaryHeld: false,
    attackSecondaryPressed: false,
    interactPressed: false,
    dodgePressed: false,
    sprintHeld: false,
    submitPressed: false,
    cancelPressed: false,
    pausePressed: false,
    debugTogglePressed: false));
```

约定：

- 业务层只消费 `InputSnapshot` 和 `InputCommandQueue`。
- 瞬时输入可由 `InputCommandQueue` 转成游戏自己的 `RuntimeCommand`。
- 本地多人使用 `LocalUserInputAdapter` 读取 `PlayerInput.actions` 的私有副本。
- 重绑定通过 `IInputRebindingService.StartRebind("Gameplay/Jump", bindingIndex)` 触发，结果保存到 `PlayerPrefs`。

## 15.1 Settings 用户偏好底座

Settings v0 是框架能力底座，不是完整玩家设置界面。核心 profile/store 保持 noEngine；输入、音频、画质和本地语言偏好通过外层 adapter 或 UI bridge 复用现有模块；CharacterTest 已有第一版 FairyGUI 玩家入口用于验证这套 binding 和 Apply/Save/Reload 流程。

```csharp
using MxFramework.Settings;
using MxFramework.Settings.Audio;
using MxFramework.Settings.Graphics;
using MxFramework.Settings.Input;

var settings = new MxSettingsService(new MxSettingsJsonFileStore(settingsPath));
MxSettingsLoadResult load = settings.Load();

settings.RegisterApplier(new MxAudioSettingsApplier(audioService));
settings.RegisterApplier(new MxUnityGraphicsSettingsApplier(new MxUnityGraphicsRuntime()));
MxSettingsApplyResult apply = settings.Apply();
MxSettingsSaveResult save = settings.Save();
```

组合根可以按项关闭画质应用。例如 CharacterTest 暂时不让 settings 接管 frame cap：

```csharp
settings.RegisterApplier(new MxUnityGraphicsSettingsApplier(
    new MxUnityGraphicsRuntime(),
    new MxUnityGraphicsSettingsApplyOptions(applyTargetFrameRate: false)));
```

画质设置默认不改变当前运行时状态；只有显式字段会应用。示例：

```csharp
var graphics = new MxGraphicsSettings(
    resolutionWidth: 1920,
    resolutionHeight: 1080,
    applyWindowMode: true,
    windowMode: MxWindowMode.FullscreenWindow,
    vSyncCount: 1,
    targetFrameRate: 120,
    renderScale: 1.0f);

settings.Save(settings.CurrentProfile.WithGraphics(graphics));
```

语言偏好保存到同一个 settings profile，JSON 字段是 `localization.localeId`，旧 JSON 缺字段时默认 `zh-CN`：

```csharp
settings.Save(settings.CurrentProfile.WithLocalization(new MxLocalizationSettings("en-US")));
```

设置 UI 不应直接展示所有底层画质项；默认玩家可见项从 `MxGraphicsSettingsCatalog.PlayerVisibleDescriptors` 读取，开发/内部项由项目自行决定是否开放。玩家可见的整体质量档 `QualityPreset` 会映射到当前 Unity `QualitySettings` level；如果开发项 `UnityQualityLevel` 被显式设置，则优先使用该精确 level。

输入设置入口只包装现有重绑定服务；binding override 仍由 `InputRebindingService` 保存到 `PlayerPrefs`。

```csharp
var inputSettings = new MxInputRebindingSettingsController(
    inputService.Rebinding,
    inputService.BindingDisplay);

MxInputBindingListResult bindings = inputSettings.ListBindings(new[]
{
    new MxInputBindingTarget("Gameplay/Jump", 0)
});

inputSettings.StartRebind("Gameplay/Jump", 0);
inputSettings.Save();
```

CharacterTest 提供代码层 host：`GameManager.SettingsRuntime`。默认 JSON 路径是 `Application.persistentDataPath/MxFramework/CharacterTest/settings.json`，注册 graphics applier，并可通过 `AttachAudioService(IAudioService)` 把当前 session 的 audio service 接入同一套 Apply 流程。attach 或 Session rebuild 替换 Audio Service 时会立即恢复当前已应用的 Audio profile；若设置面板存在尚未 Apply 的 dirty 编辑，则恢复 `SettingsService.CurrentProfile`，不会把未提交编辑暗中应用。CharacterTest 不默认自动应用画质或输入 profile。`GameManager.LocalizationRuntime` 会读取 `StreamingAssets/MxFramework/Localization/character_test.tsv` 并把 settings working profile 中的语言同步到 `MxConfigUiTextProvider`，默认 `zh-CN`。FairyGUI `CharacterTestFairyGuiSettingsPanelHost` 默认隐藏 Composer-owned `MxCharacterTestSettingsUi/SettingsPanel`，由 `MxCharacterTestUi/MainMenuPanel` 中的 Settings 按钮按需打开，并可通过 Close 按钮或 FairyGUI `ui.cancel`/Esc 关闭；它和主菜单、联机大厅共用 player modal input bridge，打开时持有 UI 输入上下文和 released cursor scope。面板通过 `SettingsRuntime.SettingsUi` 修改 working profile，并在点击“应用/保存/重新加载”时调用同一个宿主入口。Language 行会立即切换 MainMenu、NetworkPanel、SettingsPanel、Combat HUD 和 StoryDialog 的玩家可见静态文本；Save 持久化语言，Reload 恢复已保存语言。宿主会委托 `SettingsUi`，因此不会绕过 pending UI edits 或 `Last*Result` 记录：

Settings 命令现在通过 generated v2 policy、modal gate 和
`MxUiFeedbackCommandSink` 进入业务 Sink。普通切换使用 Click，Close/`ui.cancel`
使用 Back，Apply/Save/Reload/Input 结果根据真实业务 outcome 选择 Confirm、
Click 或 Error；业务 Sink 不再提前播放产品 Event。`MxAudioUiFeedbackPlayer`
只消费稳定 Cue key 和完整 Cue 定义，缺 Audio Service 或 Cue 时静音且不阻断
设置命令。当前四个 UI Cue 尚未发布，因此这是一条可验证的 runtime slice，
不是已完成的可听验收；其他 FairyGUI 面板也不会自动继承该组合。

```csharp
MxSettingsApplyResult apply = gameManager.SettingsRuntime.ApplyCurrentProfile();
MxSettingsSaveResult save = gameManager.SettingsRuntime.SaveCurrentProfile();
MxSettingsLoadResult reload = gameManager.SettingsRuntime.ReloadProfile();
```

CharacterTest 的 graphics applier 默认关闭 `TargetFrameRate` 应用，避免和 `CharacterTestSceneProfile.TargetFrameRate` 同时争用 Unity frame cap 和 simulation delta 的所有权。

CharacterTest FairyGUI 设置面板第一版开放 Language、`QualityPreset`、VSync、RenderScale、Resolution/WindowMode 只读显示、CharacterTest 默认 Audio Bus volume/mute，以及 9 行默认输入绑定的 Rebind/Save Input/Reset Input。面板视觉来自 `MxUiFoundation` 语义资源、紧凑按钮配方和 `character_test_settings.ui-screen.json`，并由 UI Composer/helper 物化、发布到 `MxCharacterTestSettingsUi` 的运行时 `.bytes` / atlas。`TargetFrameRate` 不在面板中开放；设置面板本身不接管 CharacterTest pause state，是否暂停仍由打开它的暂停菜单和代码内 UI 默认策略决定，也不会接管 `CharacterTestSceneProfile.TargetFrameRate`。

设置 UI 应优先绑定 `MxSettingsUiPresenter`，它提供 working profile、dirty state、graphics/audio/input sections 和统一命令。示例：

```csharp
MxSettingsUiPresenter settingsUi = gameManager.SettingsRuntime.SettingsUi;
MxSettingsUiViewModel view = settingsUi.BuildViewModel();

settingsUi.SetGraphicsQualityPreset(MxGraphicsQualityPreset.High);
settingsUi.SetRenderScale(1.25f);
MxSettingsApplyResult apply = gameManager.SettingsRuntime.ApplyCurrentProfile();
MxSettingsSaveResult save = gameManager.SettingsRuntime.SaveCurrentProfile();
```

CharacterTest 默认提供一组 player-facing `MxInputBindingTarget` 用于显示输入绑定行；项目可以通过 `CharacterTestSettingsRuntime` 构造参数替换这组列表。重绑定仍委托 `MxInputRebindingSettingsController`，例如：

```csharp
settingsUi.StartInputRebind(new MxInputBindingTarget("Gameplay/Jump", 0));
settingsUi.SaveInputOverrides();
```

约定：

- Settings JSON 保存用户偏好 profile、audio bus 和 graphics 设置，不保存 gameplay 权威状态。
- `localization.localeId` 只保存本地语言偏好，不进入 Runtime SaveState、Replay hash 或网络同步。
- 输入 override 不复制到 settings profile，避免与 Unity Input System 的 override 管线分叉。
- 音频设置只通过 `IAudioService` 应用，不直接访问 FMOD 或 Unity audio API。
- 画质设置只通过 `MxUnityGraphicsSettingsApplier` / `MxVolumetricFogSettingsApplier` 做运行时代码应用，不写 URP、Quality、Scene 或 Prefab YAML；CharacterTest FairyGUI 设置页只作为该 demo 的玩家入口，不是通用项目设置面板。
- CharacterTest 本地化只覆盖 FairyGUI player-facing 静态文本；动态 gameplay 数值、运行时生成名字、Debug UI、Editor 工具和 UI Toolkit diagnostics 不纳入本轮。
- FairyGUI / UI Toolkit binder 只消费 `MxSettingsUiViewModel` 并调用 presenter 命令，不自行拼装 settings service、applier 或 profile JSON。

## 15.5 Character Control 角色控制编排

Character Control 是 Input、Runtime AI Planner、Replay/Test source 与 Combat / Gameplay 之间的 noEngine 编排层。组合根负责把设备输入或规划结果转成 `CharacterCommand`；Character Control 只消费固定点命令和稳定 entity id。

```csharp
using MxFramework.CharacterControl;
using MxFramework.Combat.Core;
using MxFramework.Core;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;

var entity = CharacterControlEntityRef.FromGameplayAndCombat(
    gameplayEntityId: new GameplayEntityId(10, 1),
    combatEntityId: new CombatEntityId(20),
    combatBodyId: new CombatBodyId(30),
    stableId: 1);

var frame = new RuntimeFrame(12);
var command = new CharacterCommand(
    frame: frame,
    sourceId: 0,
    entity: entity,
    moveDirection: new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
    facingBasis: CharacterFacingBasis.Identity,
    jumpPressed: false,
    sprintHeld: true,
    actionButtons: CharacterActionButtons.None,
    actionRequest: default);

var control = new CharacterControlStateMachine(entity);
CharacterControlTransitionResult transition = control.BeginAction(
    frame,
    CharacterControlTransitionReason.ActionStarted);
```

运动桥接必须走 Combat Motion：

```csharp
var resolver = new CharacterMotionResolver(
    combatKinematicMotor,
    CharacterMotionSettings.Default,
    motionModifierProviders);

CharacterMotionResult motion = resolver.Step(
    command: command,
    controlState: control,
    motionState: currentMotionState,
    physicsWorld: combatPhysicsWorld);
```

动作桥接只启动 / 取消 Combat action 或 enqueue Gameplay command，不直接写 Gameplay component store：

```csharp
var actionController = new CharacterActionController(
    stateMachine: control,
    combatActionRunner: combatActionRunner,
    gameplayCommandBuffer: runtimeCommandBuffer,
    abilityRequestStore: gameplayAbilityRequestStore);

CharacterActionResult action = actionController.Submit(
    CharacterActionRequest.CombatAction(
        frame,
        entity,
        CharacterActionKind.Attack,
        combatActionId: 1001,
        sourceId: 0,
        queueIfBusy: true));
```

Gameplay pressure typed events 通过 reaction bridge 接入控制状态，不直接写 Gameplay source of truth：

```csharp
var pressureReaction = new CharacterPressureReactionController(
    control,
    actionController,
    new CharacterPressureReactionPolicy
    {
        GuardBreakReactionFrames = 8,
        GuardBreakLockMask = CharacterControlLockMask.Action
    });

CharacterPressureReactionResult pressureResult = pressureReaction.Apply(
    new CohesionBreakEvent(
        frame,
        entity.GameplayEntityId,
        PressureBand.Critical,
        previousValue: 80,
        currentScatterPressure: 100,
        maxScatterPressure: 100,
        delta: 20,
        traceId: "hit-001"));

if (pressureResult.ReactionStarted)
{
    pressureReaction.TryFinishExpiredReaction(
        pressureResult.ReactionEndFrame,
        out CharacterPressureReactionResult finished);
}
```

如果启用 `BrokenBandChangeStartsReaction`，band-change 只有在 pressure 上升且 `NewBand > PreviousBand` 时才会启动 reaction；recovery、negative delta 和 band 回落不会刷新已有 reaction window。组合根停用 pressure owner 时可调用 `FinishActiveReaction(...)` 主动释放控制锁。

MxAnimation 表现适配放在可选程序集 `MxFramework.CharacterControl.Animation`。它只消费 Character Control 结果 / 事件，把移动、反应转成 animation backend 请求；Combat action lifecycle 仍由 `CombatMxAnimationUnityBridge` 负责：

```csharp
using MxFramework.CharacterControl.Animation;
using MxFramework.Resources;

var animationPresentation = new CharacterAnimationPresentationController(
    mxAnimationBackend,
    new CharacterAnimationPresentationOptions
    {
        TargetActorId = "character:1",
        LocomotionBlendMode = CharacterAnimationLocomotionBlendMode.Blend2D,
        LocomotionBlend2DId = "locomotion2d",
        BlendLocomotionWhenAirborne = false,
        ReactionBindings =
        {
            new CharacterAnimationReactionBinding
            {
                Reason = CharacterControlTransitionReason.GuardBreak,
                BindingId = "reaction.guard_break",
                ClipKey = new ResourceKey("demo.animation.guard_break", ResourceTypeIds.AnimationClip),
                RequestKind = CharacterAnimationReactionRequestKind.CrossFade,
                FadeDurationSeconds = 0.12f
            }
        }
    });

CharacterAnimationPresentationResult locomotionAnimation =
    animationPresentation.ApplyLocomotion(motion);

CharacterAnimationPresentationResult reactionAnimation =
    animationPresentation.ApplyStateChanged(stateChangedEvent);

CharacterAnimationPresentationDiagnosticSnapshot animationDiagnostics =
    animationPresentation.CreateSnapshot();
```

Character Control 可直接注册到 Phase 13 Debug UI registry。Source 会订阅状态、动作和 pressure reaction 的公开事件；command / motion 由组合根在采样和 motion step 后显式记录：

```csharp
using MxFramework.DebugUI;
using MxFramework.DebugUI.Adapters;

var characterDebugSource = new CharacterControlDebugSource(
    control,
    actionController: actionController,
    pressureReactionController: pressureReaction);

characterDebugSource.RecordCommand(command);
characterDebugSource.RecordMotionResult(motion);

var debugRegistry = new FrameworkDebugSourceRegistry();
debugRegistry.Register(characterDebugSource);

// 组合根销毁、场景卸载或角色换绑时：
characterDebugSource.Dispose();
```

本地输入适配放在可选程序集 `MxFramework.CharacterControl.Input`：

```csharp
using MxFramework.CharacterControl.Input;
using MxInput = MxFramework.Input;

var inputSource = new InputCharacterCommandSource(inputProvider, new InputCharacterCommandSourceOptions
{
    SourceId = 0,
    UseLookAsFacing = true,
    ActionBindings = new[]
    {
        CharacterInputActionBinding.CombatAction(
            MxInput.InputIntent.AttackPrimary,
            CharacterActionKind.Attack,
            combatActionId: 1001,
            queueIfBusy: true),
        CharacterInputActionBinding.GameplayAbility(
            MxInput.InputIntent.AttackSecondary,
            gameplayAbilityId: 300001)
    }
});

if (inputSource.TryGetCommand(frame, entity, out CharacterCommand inputCommand))
{
    control.RecordCommandFrame(inputCommand.Frame);
}
```

Runtime AI Planner 适配放在 `MxFramework.CharacterControl.RuntimeAiPlannerBridge`，由项目组合根显式配置 planner action 到角色命令的 profile：

```csharp
using MxFramework.CharacterControl.RuntimeAiPlannerBridge;

var profiles = new RuntimeAiCharacterCommandProfileRegistry();
profiles.Register(new RuntimeAiCharacterCommandProfile(
    actionId: 10,
    moveDirection: FixVector3.Zero,
    facingBasis: CharacterFacingBasis.Identity,
    actionKind: CharacterActionKind.Attack,
    combatActionId: 1001,
    moveSpeedScale: Fix64.Zero,
    traceTag: "stand-cast"));

var plannerSource = new RuntimeAiPlannerCharacterCommandSource(
    aiWorldState,
    planner,
    goals,
    actions,
    profiles,
    new RuntimeAiPlannerCharacterCommandSourceOptions
    {
        SourceId = 10,
        RequireTargetFacts = true,
        ReactionDelayFrames = 2,
        MinDecisionIntervalFrames = 3,
        CommandSmoothingFrames = 2
    });
```

`combatActionId` / `gameplayAbilityId` 这类 action request 只在 action selection edge 或 reaction-delay 生效帧输出一次；后续最小决策间隔或 smoothing 复用同一 profile 时只输出 movement command。`moveSpeedScale` 不传时默认 `1`，显式传 `Fix64.Zero` 可以表达站定施法、原地防御或停步等待。

Character Control Playable vertical slice 把上述模块收进一个 Unity 可操作入口：

- 场景：`Assets/Scenes/CharacterControlPlayable.unity`
- 场景生成菜单：`MxFramework/Character Control/Create Playable Scene`
- Demo runtime：`Assets/Scripts/WGameFrameworkProject/Demos/CharacterControl/CharacterControlPlayableDemo.cs`
- Runtime slice：`Assets/Scripts/WGameFrameworkProject/Demos/CharacterControl/CharacterControlPlayableSlice.cs`
- UI Toolkit：`Assets/UI/MxFramework/CharacterControl/CharacterControlPlayableDemo.uxml` / `.uss`
- 测试：`Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Demo/CharacterControl/CharacterControlPlayableSliceTests.cs`

操作入口同时覆盖本地输入和 Runtime AI Planner command source：WASD / arrow 移动，Space 跳跃，Mouse Left 攻击，Mouse Right 触发 cohesion break reaction，Escape 暂停。Runtime AI Planner step 通过 UI 按钮触发；通用 Debug 键只用于 Debug overlay / probe / cycle / step / restart 语义，不再作为该 Demo 的玩法别名。UI 按钮走同一个 `RuntimeCommandBuffer`，不会直接改 Character Control 状态。

移动 modifier / traction 通过 provider 注入，第一版只影响 `CombatMotionInput.MoveSpeedScale`：

```csharp
public sealed class SlowMotionProvider : ICharacterMotionModifierProvider
{
    public void CollectModifiers(
        CharacterMotionModifierContext context,
        IList<CharacterMotionModifier> destination)
    {
        destination.Add(new CharacterMotionModifier(
            source: "buff.slow",
            moveSpeedScale: Fix64.Half,
            reason: "slow",
            priority: 10));
    }
}
```

约定：

- Input、Runtime AI Planner、UI Toolkit、Replay 和测试只实现 `ICharacterCommandSource` 或提交 `CharacterActionRequest`。
- `CharacterMotionResolver` 通过 `CombatKinematicMotor` 得到权威移动；Unity `CharacterController`、`Rigidbody`、`UnityEngine.Physics` 和表现层 root motion 不能作为权威。
- `CharacterActionController` 通过 Resolver + 有状态 Gateway 执行正式动作；CombatAction 与 Gameplay command 只由显式 track bridge 作为下游结果，不改 Combat timeline、hit window、damage 或 Gameplay HP/Buff/Ability 状态。
- `CharacterPressureReactionController` 只消费 Gameplay pressure typed events，校验 `GameplayEntityId` 映射并维护独立控制状态；正式动作反应由 `CharacterActionReactionBridge` 提交。
- cohesion / guard / armor break 的动作切换必须通过 Gateway 检查 `interruptRules`；拒绝时压力事实仍成立，但活动动作及 ECS Action Component 不会被清除。
- band-change reaction 只响应 pressure 上升导致的 band 升级；生命周期提前结束时用 `FinishActiveReaction(...)` 释放 active reaction。
- cooldown、资源、状态、目标合法性等项目规则通过 `ICharacterActionConstraint` 注入。
- slow、traction、fatigue 等移动影响通过 `ICharacterMotionModifierProvider` 输出 scale，不直接写 Gameplay / Combat 状态。
- Input adapter 在 Gameplay context 关闭时会 drain 并丢弃当前 frame 及以前的 queued commands；Runtime AI Planner profile 的 `ActionRequest` 只在首次选择或 reaction delay 生效帧发出一次。
- MxAnimation presentation adapter 只输出 `MxAnimationBlend1DRequest` / `MxAnimationBlend2DRequest` / `Play` / `CrossFade` 请求和 diagnostics；1D speed 使用水平输入幅度乘移动倍率，默认 airborne 时 locomotion 参数归零，缺失 reaction binding、fallback binding 或 backend reject 会记录 diagnostics 但不影响 Character Control authority。
- Combat action started / finished / canceled 的动画表现仍由 `CombatMxAnimationUnityBridge` 负责；accepted、queued、rejected 和 gameplay-only action events 在 Character Control animation adapter 中只记录 skipped diagnostics。
- Debug UI source 只读公开事件和组合根传入的 last command / motion result；不暴露可写命令，不进入 SaveState、Replay 或 Runtime hash。`FrameworkDebugSourceRegistry` 不拥有 source 生命周期，组合根必须释放 event-backed source。
- Playable vertical slice 中 Unity `MonoBehaviour` 只做输入、UI Toolkit 和 Transform view 适配；权威 loop 仍由 `RuntimeHost`、`RuntimeCommandBuffer`、`CharacterMotionResolver`、`CharacterActionController`、`CharacterPressureReactionController`、`CharacterAnimationPresentationController` 和 `CharacterControlDebugSource` 推进。

详细接口见 `Docs/Interfaces/CharacterControl.md`，测试入口为 `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/CharacterControl/` 和 `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Demo/CharacterControl/`。

### 15.5.1 Character Cloth 可控表现适配

项目安装 Magica Cloth 2 后，可以在角色/服装根添加 `MagicaCharacterClothController`，在角色或场景组合根添加 `MagicaCharacterClothSystem`。两个组件默认自动收集 inactive 子节点，不要求 Gameplay、Combat 或 Character Control 引用 Magica 类型。

```csharp
using MxFramework.CharacterCloth.Magica.Unity;

MagicaCharacterClothSystem clothSystem = characterRoot.GetComponent<MagicaCharacterClothSystem>();

clothSystem.SetPaused(true);                       // 暂停本来正在运行的布料
clothSystem.SetPaused(false);                      // 恢复每个控制器的本地请求
clothSystem.SetEnabled(false, keepPose: false);    // 停止模拟并复位到动画姿势
clothSystem.SetEnabled(false, keepPose: true);     // 停止模拟并保留当前姿势
clothSystem.SetEnabled(true);                      // 清除全局禁用覆盖
```

`SetEnabled` 和 `SetPaused` 只改变表现层 System Override，不覆盖各角色的 `RequestedState`。动态生成且不在系统子层级内的控制器，由角色生成组合根显式 `Register` / `Unregister`。布料求解后的骨骼或网格不得作为战斗碰撞、命中挂点、SaveState、Replay、hash 或网络权威。

完整状态语义与组装边界见 `Docs/Interfaces/CharacterCloth.md`。

#### Cloth Authoring 运动预览

从 Unity 菜单 `MxFramework/Character/Cloth Authoring` 打开窗口。完成 Collider 调整后先点击“保存并重建”；顶部状态显示“Prefab 已同步”时，“运动预览”按钮可用。

“运动预览”会进入隔离的 Editor Play Mode：它从正式 Runtime Resources 加载当前角色 Prefab，从角色生成的 `animation_clip_registry.json` 读取动作，并通过全局资源映射解析独立 `.anim`。当前回归角色为数字角色 `000001`，候选来自其完整已注册动画目录。预览控制条支持：

- 播放/暂停、重播、循环、0.1～2.0 倍速；
- 原地、前后往返、左右横移、启动/急停、90°/180° 转身和 Z 字运动；
- 固定世界/跟随角色两种镜头模式，以及前、后、左、右观察方向；
- 可调位移幅度、运动周期、启停停顿时间和转向速度；
- 地面网格、米制距离刻度、完整运动路径、当前位置与朝向标记；
- 当前世界位置、线速度、加速度和角速度遥测；
- Runtime Collider 绿色线框显隐；
- Cloth Reset 和退出预览。

固定世界镜头保留世界参考系，用于确认角色确实发生位移、急停与转身；跟随角色镜头让角色保持居中，用于近距离观察 Cloth 与身体、Collider 的相对关系。运动预览只用于肉眼观察头发等表现层 Cloth 在动作、整体位移、急停和转向下的摆动与穿模，不产生通过/失败结论，也不进入 Gameplay、Combat、SaveState、Replay、网络或 authority hash。退出 Play Mode 后 Cloth Authoring 会恢复生成 Prefab 的只读 Prefab Stage。原 focused PlayMode 自动测试保留在顶部下拉菜单“运行自动验收”，它负责结构、异步 Build、运动响应和四态往返，不替代肉眼穿模检查。

运动预览期间 Cloth/Collider 列表和属性区保持可编辑。有效草稿变更经过短去抖后重新投影到当前运动角色，支持 Cloth Root Bone、Connection、Preset，以及 Collider 的增加、复制、删除、骨骼、类型、尺寸、方向和局部变换实时生效；角色实例、动作时间、运动轨迹和相机不中断。每次热应用会重新触发 Magica Build 并 Reset Cloth。校验失败时 fail-closed：保留最后一次有效运行投影，在窗口显示阻断诊断。实时编辑只修改内存草稿和 SessionState 恢复副本，不写 Authoring JSON、不修改生成 Prefab；退出预览后仍需“保存并重建”才能持久化。

“Cloth 运动参数”区提供 25 项 typed Override：Gravity/Direction、Damping、Blend Weight；World/Local Inertia、World Inertia Smoothing、Movement/Rotation/Particle Speed Limit、Teleport Distance/Rotation；Distance Stiffness、Angle Restoration Stiffness、Velocity Attenuation、Angle Limit、Max Distance；Collision Friction/Limit Distance、Surface Thickness；Wind Influence/Frequency/Turbulence；Spring Power/Limit Distance。每项独立选择继承 Preset 或 Override，限制类同时控制启用状态；“全部恢复 Preset”只移除 Override。首次启用 Override 会把草稿升级为 schema `1.1`，旧 `1.0` 文档继续只读兼容。

每个参数行同时显示 `Preset` 与运动预览中的实际 `Run` 值，窗口顶部显示运行投影 revision。当前 BoneCloth 不使用的 Spring 与 Collision Limit Distance 会禁用并说明“仅 BoneSpring”；Preset 未启用 Self Collision 时 Surface Thickness 会禁用；Wind 参数在未启用预览风源时显示条件警告。运动预览控制条提供瞬态全局 Magica WindZone（开关、风速、Y 风向、风源湍流）和跟随角色的风向箭头；“压力脉冲”会依次执行急停和 180° 转身，约 4 秒后恢复用户原运动设置，用于比较参数修改前后的可见响应。

左侧对象树的“骨骼”开关会在 Scene View 中显示从当前 `SkinnedMeshRenderer.bones/rootBone` 及其骨架层级收集的真实骨骼，并排除生成的 `MagicaCloth_*` / `MagicaCollider_*` 与 Renderer 节点。Scene 骨骼选择器提供搜索、全部/次级骨/未使用过滤和最多 40 条可点击结果；默认次级骨模式排除 Humanoid 核心链，搜索会跨完整路径匹配。点击结果名称先聚焦骨骼，聚焦点显示黄色圆环/十字准星，再用结果行“添加 Cloth/Root”或直接点击高对比青色骨点完成选择。点击“3D＋”创建的新 Cloth 继承当前 Cloth 的 Preset、参数 Override 和 Collider，只替换 ID 与 Root Bone；`Cloth 结构与 Preset → 3D 添加 Root` 会追加 Root。点选期间暂停 Collider Handles，骨线使用深色描边和 Overlay 深度显示；Esc 或“取消”退出。

左侧对象导航采用稳定的上下分区：固定高度 Cloth 列表及其操作条在上，当前 Cloth 的独立 Collider 列表及其操作条在下；选择 Cloth 只替换 Collider 列表内容，不再把 CRUD、建议卡和 Collider 行内嵌到选中 Cloth 卡片中，因此点击选择不会重排整个导航栏。两个列表都只保留纵向滚动，不显示横向滚动条。Cloth 区显示“＋ 新建 / 复制 / 删除 / 从 3D 骨骼新建 Cloth”；Collider 区显示“＋ Sphere / ＋ Capsule / 复制 / 删除 / ↑ / ↓”。删除仍先弹出对应 Cloth 或 Collider 确认，并进入 Unity Undo；只有后续“保存并重建”才持久化。

## 15.6 Character Action 角色动作层（单一执行权威）

> 当前状态：`MxFramework.CharacterAction` noEngine 核心已合并，CharacterTest Runtime Slice 已通过 JSON-backed Profile / ActionSet / Action Library 接入输入、技能槽、动作执行和轨道分发。
> 本节展示可直接运行的纯 C# 用法，适用于 noEngine 测试和诊断验证。
> `CharacterControl` 已使用 Request / Dispatch Coordinator 运行正式 CharacterAction：请求经 Resolver 与唯一 Gateway 执行，track / presentation adapters 转发 Motion、root motion、Combat、Gameplay、Animation、Presentation 和 Debug。旧 CombatAction 仅可由 `CharacterActionCombatTrackBridge` 作为外层动作的子时间线，Gameplay command 仅由 `CharacterActionGameplayTrackBridge` 提交。两个 Runtime 示例已迁移到同一执行路径。

### 15.6.1 Character Action Authoring 工作台

从 Unity 菜单 `MxFramework/Character/Character Action Authoring` 打开工作台；也可从 CharacterStudio 的动作槽精确打开。CharacterStudio 负责“每个角色槽使用哪个动作”，Unity 工作台负责“该动作具体如何执行”。

正式正文源为 `CharacterActionSource/Libraries/Common/character_actions.json` 与 `<角色包>/config/character_actions.json`，Action Library schema 为 `0.2.0`。`Assets/MxFrameworkGenerated/CharacterActions` 仅是编译产物；Profile `<package>.default`、ActionSet `<package>.<weaponCombination>` 及 schema `0.3.0` 投影不得直接编辑。

CharacterStudio 发来的打开请求包含 Profile、ActionSet、Library、Action ID、读写模式与源 SHA-256。Common 在角色上下文只能只读打开；角色源可保存，但磁盘 hash 与打开时不同时会拒绝覆盖。打开、保存、失败、冲突与超时均写入 CharacterStudio 可查询状态。

界面以 [Character Action Authoring 工作台效果图](Images/CharacterAction/character-action-authoring-workbench-target.png) 为信息层级基准，最小窗口为 `1180×700`，并使用三个一级工作区：

- “动作编排”：左侧聚合当前 Profile 全部 Action Library 的动作，中央显示隔离的 3D 预览和现有帧时间轴，右侧根据选择显示动作或时间轴事件 Inspector。
- “映射与方案”：兼容独立/旧数据的 Profile、ActionSet、指令、能力与受击检查；从 CharacterStudio 打开的编译器拥有 Profile/ActionSet 时不在这里改 ID 或成员组成。`ReactionProfileId + CharacterReactionProfile` 仍是受击解析权威，旧 `reactionBindings[]` 只读。
- “共享资源”：复用 Projectile Manager，并集中 Ability Effect Timeline 与相关 Mechanic Tuning。

Profile、ActionSet、Library、Action 和时间轴选择都按 stable id 保持。CharacterStudio 精确打开时只定位请求中的源库与动作，并服从其读写模式；独立打开仍可加载所选 Profile 引用的全部 Action Library，但不得把生成投影当作正文写回入口。

属性编辑立即进入内存草稿并支持 Undo/Redo，普通选择、搜索、折叠和切换工作区不会产生 dirty。顶部“保存”是本会话的 Save All：先对全部已加载成员执行完整预检；存在 Error 时不写任何来源并展开 `Problems`，Warning 可以保存；预检通过后写入各 dirty 来源，最后只刷新和重新校验一次。单个来源写入失败时，该来源继续保持 dirty，并在 `Problems` 中显示准确路径。需要单独保存或另存一个来源时，使用文件菜单。

底部 `Problems` 抽屉汇总 Import、Local Validation、Cross-module Validation 与 Reference Discovery 的结构化诊断，可按严重度、文本、当前动作或当前来源筛选。点击可定位的问题会打开对应动作、时间轴项或绑定行；无法解释的路径会退回源文件定位。`OpenForAbilityBinding(...)` 仍是原公共入口，并会定位到“映射与方案 → 能力绑定”的对应行。

内嵌 3D 预览通过 `PreviewRenderUtility` 把角色、VFX 和 Projectile 对象隔离在用户活动 Scene 之外。播放、暂停、逐帧与时间轴 scrub 共用一个控制器。内嵌模式用于观察轨迹、碰撞体、根运动、挂点、VFX 和 Projectile 路径；需要拖拽 Projectile Handles 时，点击“场景编辑”切到 SceneView 独占编辑。进入时暂停播放，退出时清理临时 SceneView 对象并回到相同动作和帧，不自动继续播放。

该工作台只写动作正文源，不写 CharacterStudio 的动作槽映射、生成 Profile/ActionSet 或其他模块权威；动作槽变更必须回到 CharacterStudio。

### 15.6.2 Runtime 解析与执行

`MxFramework.CharacterAction` 把角色动作拆为四个阶段：

```text
CharacterActionIntentRequest
  → CharacterActionResolver 解析候选、优先级、tag 需求
    → CharacterActionResolveResult (含 CharacterActionPlan)
      → CharacterActionExecutionGateway 按切换/打断/生命周期策略执行
        → internal Runner 按 frame/phase 推进
        → CharacterActionTrackAdapter 转成 Motion / Combat / Gameplay 请求
        → CharacterActionPresentationTrackAdapter 转成 Animation / Audio / VFX / Camera / UI 请求
```

最小用法（配置 + 解析 + 执行）：

```csharp
using System;
using MxFramework.CharacterAction;
using MxFramework.Gameplay;
using MxFramework.Runtime;

// 1. 定义动作配置（注意：所有类型使用构造函数，不支持 object initializer）
//    DurationFrames 和 Phases 必须覆盖完整动作时长，否则验证器会拒绝
var actionConfig = new CharacterActionConfig(
    id: 1001,
    stableId: "demo.light_attack",
    displayName: "Light Attack",
    category: CharacterActionCategory.BasicAttack,
    timelineAuthority: CharacterActionTimelineAuthority.CharacterAuthored,
    tags: new[] { "attack", "melee" },
    priority: 10,
    durationFrames: 20,
    requirements: Array.Empty<CharacterActionRequirement>(),
    phases: new[]
    {
        new CharacterActionPhase(CharacterActionPhaseKind.Startup, 0, 4),
        new CharacterActionPhase(CharacterActionPhaseKind.Active, 5, 14),
        new CharacterActionPhase(CharacterActionPhaseKind.Recovery, 15, 19),
    },
    cancelRules: Array.Empty<CharacterCancelRule>(),
    interruptRules: Array.Empty<CharacterInterruptRule>());

var actionSet = new CharacterActionSetConfig(
    id: 1,
    stableId: "demo.character.actions",
    displayName: "Demo Character Actions",
    characterStableId: "demo.character",
    equipmentStateStableId: "",
    commandBindings: Array.Empty<CharacterActionBinding>(),
    abilityBindings: Array.Empty<CharacterAbilityActionBinding>(),
    reactionBindings: Array.Empty<CharacterReactionBinding>(),
    movementProfileId: "",
    reactionProfileId: "",
    defaultActionId: "",
    commandSequences: new[]
    {
        new CharacterActionCommandSequence(
            "LightAttack",
            new[] { "demo.light_attack" },
            wrapAfterLast: true,
            priority: 10)
    });

// 2. 准备解析上下文
var resolveContext = new CharacterActionResolverContext(
    actionSet: actionSet,
    actions: new[] { actionConfig });

// 3. 提交意图请求
var intentRequest = new CharacterActionIntentRequest(
    entity: new GameplayEntityId(1, 1),
    intentId: "LightAttack",
    abilityId: null,
    abilityStableId: "",
    requestedActionId: "",
    sourceKind: CharacterActionSourceKind.LocalInput,
    priority: 10,
    frame: new RuntimeFrame(1),
    traceId: "demo-001");

// 4. 解析
var resolver = new CharacterActionResolver();
CharacterActionResolveResult resolveResult = resolver.ResolveCommand(resolveContext, intentRequest);

if (resolveResult.IsSuccess && resolveResult.Plan != null)
{
    // 5. 准备运行定义和调用方复用缓冲
    var runnerDefinition = new CharacterActionRunnerActionDefinition(
        actionConfigId: actionConfig.Id,
        actionId: actionConfig.StableId,
        timelineAuthority: CharacterActionTimelineAuthority.CharacterAuthored,
        cancelRules: Array.Empty<CharacterCancelRule>(),
        interruptRules: Array.Empty<CharacterInterruptRule>(),
        combatTimeline: null,
        trackEvents: Array.Empty<CharacterActionTrackDispatchEvent>());

    var gateway = new CharacterActionExecutionGateway();
    var eventBuffer = new CharacterActionRunnerEventBuffer(capacity: 16);
    CharacterActionExecutionRequest executionRequest = CharacterActionExecutionRequest.StartIntent(
        resolveResult,
        runnerDefinition,
        CharacterActionSourceKind.LocalInput,
        priority: intentRequest.Priority,
        traceId: intentRequest.TraceId);
    CharacterActionExecutionResult runResult = gateway.Execute(in executionRequest, eventBuffer);

    if (runResult.Accepted)
    {
        // 6. 逐帧推进；启动与 Tick 事件均写入复用的 eventBuffer
        CharacterActionExecutionResult tickResult = gateway.Tick(eventBuffer);
    }
}
```

受击反应构建（独立于 Combat 桥接）：

```csharp
// 从 Combat 命中结果构建 reaction 上下文
// 注意：必须提供 impactForce 和 reactionGroupId，否则上下文不完整
var hitSource = new CharacterReactionHitSource(
    frame: new RuntimeFrame(10),
    entityId: new GameplayEntityId(2, 1),
    bodyPartId: "torso",
    hitZoneId: "chest",
    damageTypeId: "physical",
    hitDirection: CharacterHitDirection.Front,
    impactForce: 100,
    reactionGroupId: "medium_hit",
    traceId: "hit-001");

CharacterReactionContextBuildResult buildResult = CharacterReactionContextBuilder.FromHitSource(hitSource);

if (buildResult.Success)
{
    // 从 reaction profile 选择对应动作（注意参数顺序：profile 在前，context 在后）
    CharacterReactionSelectionResult reaction = CharacterReactionSelector.Select(
        reactionProfile,
        buildResult.Context);
}
```

→ 接口：`Docs/Interfaces/CharacterAction.md`
→ 代码：`Packages/com.vincent.wgameframework/CharacterAction/`
→ 测试：`Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/CharacterAction/`（17 个测试文件，覆盖 resolver、runner、adapter、validation、authoring / timeline 等核心行为）
→ CharacterControl：`CharacterActionRequest.ActionIntentId` 为空时使用 `Attack` / `Skill` / `Interact` / `Dodge` / `GameplayAbility` 默认 intent；组合根可显式写入自定义 intent，由 `CharacterActionSetConfig.CommandBindings` 选择动作。每帧调用 `CharacterActionController.TickCharacterAction(frame)` 推进其 Gateway。需要消费轨道时，在 `CharacterActionControllerOptions` 注入复用缓冲的 `CharacterActionTrackAdapter` / `CharacterActionPresentationTrackAdapter` 及对应 sink。Motion/root-motion 可用 `CharacterActionMotionRequestAccumulatorSink`；Combat/Gameplay 分别用显式下游 bridge。
→ **破坏式约束**: 不再提供通用 Cancel、Runner 公共执行入口或旧 Combat/GamePlay 回退。输入侧 Cancel 必须映射到具体目标动作；Replay v1 在播放前明确拒绝，需重新录制为 v2。完整迁移见 `Docs/Decisions/ADR-014-character-action-execution-authority.md`。

## 16. Audio 音频系统

面向策划/音频作者的配套编辑器是 EditorHub 中的 **Audio Workbench**。它负责 Cue 语义、Event Definition 绑定、真实 Unity/FMOD 试听和 Runtime Catalog 发布；Resource Manager Audio 页只读。创建 Cue、替换 FMOD event、试听批准发布的逐步说明见 [`AUDIO_WORKBENCH_GUIDE.md`](AUDIO_WORKBENCH_GUIDE.md)。

需要重制粗糙素材时，使用 Workbench 中央区域的 **Authoring AI Assist**。它把 Cue 语义派生成英文 sound brief，经本地 Authoring server 代理到 4080 SFX Host，并提供候选波形、Peak/RMS、A/B、快捷剪辑和 Hash 校验。“保存并应用替换”会自动更新已有 FMOD Event、构建并同步 Banks、请求 Unity 刷新；用户仍需在 Unity/FMОD 真实试听后批准，再发布到 runtime catalog。版本历史保留原始 FMOD 音效和所有不可变采用 set。

Phase 17 已提供 noEngine `PhysicalAudioInteractionFact`、双方 `AcousticBodyProperties`、稳定 Material/Family/Basis/Response IDs、`PhysicalAudioResolver` 和 `mx.audioPhysicalRuntimeCatalog.v1` 启动 loader。Workbench 可以提升 Basis 草稿、检查覆盖并用 Authoring Core 解析游戏情境；还可以读取 Unity 最近一次成功进入播放队列的真实 fact/result，执行单参数 sweep 和二维覆盖热图。每个 sweep/heatmap 采样都调用 Authoring server 的 Physical resolver，浏览器不维护第二套选择规则；Unity 权威试听会再用 runtime catalog/resolver 核对 Basis/Cue/hash 并播放真实 Event。

受控 managed 创建仅允许 `event:/MxFramework/Generated/Physical/<family>/<basisId>`。它用独立单调 ID 创建或更新 Basis/Cue/Event，继承显式模板的 Bank、Bus、3D 和距离事实，并自动执行 FMOD 脚本、Desktop Build Banks、StreamingAssets mirror、Unity refresh 和 Snapshot 校验；Semantic/Physical manifest 只在全链成功后原子提交，失败会恢复 FMOD 工程、Banks 与 Snapshot。它不会修改未知外部 Event。

CharacterTest 启动时会同时读取 Global Audio Catalog 与 Physical Catalog：两者有效且 `audioCatalogHash` 匹配时，Footstep、Landing、ObjectImpact、WeaponHit/Block 与 AirDisplacement 使用统一 resolver；任一目录缺失、损坏或 hash 不匹配时整份使用 legacy fallback，不混合两套规则。AirDisplacement 由 Light Attack 的 CharacterAction presentation peak 触发，以默认刀具 profile 和稳定 Air/Gas profile 构造事实；它不是 Combat Collider 命中。

当前 checkout 已生成并同步 `Assets/MxFrameworkGenerated/Audio/audio_physical_runtime_catalog.json` 与对应 StreamingAssets mirror，CharacterTest 实际加载 `GeneratedLinked`。Footstep 仍由脚底接触 Started 事实触发，但 Family 选择公共材质 Pair 的 `Impact` Variant；四个已有可听 sole seed 已迁入 ObjectImpact family，稳定 `sfx.body_contact.sole.*` Cue key 仅为兼容保留，不再形成独立 Step 分支。当前 18 个 Active Basis 已绑定 Applied 版本且 coverage 完整；批量生成的 0 ms Impact Basis 保持 Draft，不进入游戏，既有 AirDisplacement seed 的 0 ms 内容仍是正式发布阻断。正式内容发布的主观审听门禁与开发期运行时同步状态分离。

Light Attack 破空使用独立的 `sfx.air_displacement.light_attack`（runtime Cue `500304`）、Event `500314` 和 Basis `530101`，不会播放 `sfx.light_attack` 的命中素材。Cue/Event/Basis lifecycle 已预分配为 Active，但当前 managed Event 只在受控路径中建立 OneShot/3D/Bank/距离骨架，`contentStatus` 仍为 Draft，且没有素材、Applied adoption set 或人工审听；先在 Workbench 生成或导入破空素材并“保存并应用替换”，再完成 Unity/FMOD 真实试听与批准，才能发布。

当前 CharacterTest bridge 已注入 `IAcousticBodyPropertyProvider`：角色 spawn 时从实体物理量缓存 Body/Foot，并缓存默认 katana Weapon profile；Weapon/Air 与接触桥会查询这些整数属性，Combat hit/contact surface 仍是 receiver 材质权威。动态装备切换、身体部位、Collider/地面属性刷新尚未接入，不能把当前缓存描述成完整项目属性链。Physical 发布还要求 Semantic/Generation/Physical 三份 expected hash 当前、Active Basis adoption set 已 Applied，以及人工批准匹配当前 Basis string/runtime ID 与 adoption set；Bank hash 由既有 adoption diagnostics 独立校验。证据中的 Physical manifest hash 只用于审计，不参与审批有效性判断。当前 seed response 只包含 `500251 MxPhysicalVolume` 与 `500252 MxPhysicalPitch`，由 FMOD backend 原生 `setVolume` / `setPitch` 应用；未来添加其他必需参数时，snapshot 必须先证明 Event-local 参数存在。LowPass/Brightness/Scrape 仍需额外 automation evidence，当前不得描述为已生效。Scrape、Roll、持续挤压及 loop 生命周期属于 Phase 18。完整目标见 [`AUDIO_PHYSICAL_EXPRESSION_DESIGN.md`](AUDIO_PHYSICAL_EXPRESSION_DESIGN.md)。

Physical Catalog JSON 只在启动阶段读取；Tick 复用参数缓冲区：

```csharp
using System;
using MxFramework.Audio;
using MxFramework.Audio.Physical;
using MxFramework.Audio.Physical.Config;

if (!PhysicalAudioRuntimeCatalogJson.TryLoad(json, out var physicalCatalog, out string error))
    throw new InvalidOperationException(error);

var resolver = new PhysicalAudioResolver(physicalCatalog);
var parameters = new AudioParameterValue[physicalCatalog.MaxResponseParameterCount];

var weapon = new AcousticBodyProperties(
    new AcousticMaterialId(510004), 0.6f, 0.2f,
    AcousticSurfaceKind.Smooth, AcousticConstructionKind.Solid);
var target = new AcousticBodyProperties(
    new AcousticMaterialId(510005), 0.8f, 0.35f,
    AcousticSurfaceKind.Smooth, AcousticConstructionKind.Solid);
var fact = new PhysicalAudioInteractionFact(
    PhysicalAudioInteractionKind.WeaponHit,
    PhysicalAudioInteractionMode.Hit,
    PhysicalAudioInteractionPhase.Started,
    weapon,
    target,
    relativeSpeed: 12f,
    normalSpeed: 10f,
    tangentialSpeed: 2f,
    pressure: 0.7f,
    impulse: 0.65f,
    angleDegrees: 90f,
    intensity: 0.7f,
    contactNormal: new PhysicalAudioDirection(0f, 1f, 0f),
    transform: AudioTransform.Origin);

if (resolver.TryResolve(fact, parameters, out PhysicalAudioResolvedPlayback resolved))
{
    // resolved.CueId 再由已缓存的 AudioDefinitionCatalog 映射到 EventId。
    // 只使用 parameters[0..resolved.ParameterCount]；不要在 Tick 中复制或扩容。
}
```

接触类事实应显式传入世界空间单位法线；`AudioTransform.Forward` 只表示 emitter 朝向。AirDisplacement 使用同一 `ContactNormal` 字段承载 sweep/displacement direction。

`Mass` 和 `ContactArea` 是项目归一化声学量，尺度由 Catalog ranges 定义，不是物理求解器单位。`CaptureTrace(...)` 会分配候选解释数组，只用于 Workbench、诊断和测试，不在 Tick 调用。

Audio 模块的业务入口是 `IAudioService` / `AudioService`。普通测试、服务器和未安装 FMOD 的环境使用 `NullAudioBackend`，不会依赖 Unity 或 FMOD。

```csharp
using System.Collections.Generic;
using MxFramework.Audio;

const int BusSfx = 10;
const int EventClick = 1001;
const int EventAura = 2001;
const int ParamIntensity = 1;

var definitions = new MemoryAudioDefinitions();
definitions.AddBus(new AudioBusDefinition(BusSfx, "SFX", "bus:/SFX"));
definitions.AddEvent(new AudioEventDefinition(
    EventClick,
    "ui.click",
    "event:/ui/click",
    string.Empty,
    AudioEventKind.Event,
    BusSfx,
    is3D: false,
    isLoop: false,
    maxDistance: 0f));
definitions.AddEvent(new AudioEventDefinition(
    EventAura,
    "combat.aura",
    "event:/combat/aura",
    string.Empty,
    AudioEventKind.Event,
    BusSfx,
    is3D: true,
    isLoop: true,
    maxDistance: 30f,
    parameters: new[]
    {
        new AudioParameterDefinition(ParamIntensity, "Intensity")
    }));

using (var audio = new AudioService(definitions, new NullAudioBackend()))
{
    AudioPlayResult oneShot = audio.PlayOneShot(AudioPlayRequest.Create2D(EventClick));

    AudioPlayResult started = audio.StartEvent(
        AudioPlayRequest.Create2D(EventAura),
        out AudioHandle auraHandle);
    if (started.Success)
    {
        audio.SetParameter(auraHandle, ParamIntensity, 0.75f);
        audio.SetBusVolume(BusSfx, 0.8f);
        audio.Tick(1f / 60f);
        audio.Stop(auraHandle, AudioStopMode.AllowFadeout);
    }

    AudioDebugSnapshot snapshot = audio.CaptureSnapshot();
}

public sealed class MemoryAudioDefinitions : IAudioDefinitionProvider
{
    private readonly Dictionary<int, AudioEventDefinition> _events =
        new Dictionary<int, AudioEventDefinition>();

    private readonly Dictionary<int, AudioBusDefinition> _buses =
        new Dictionary<int, AudioBusDefinition>();

    public void AddEvent(AudioEventDefinition definition)
    {
        _events[definition.Id] = definition;
    }

    public void AddBus(AudioBusDefinition definition)
    {
        _buses[definition.Id] = definition;
    }

    public bool TryGetEvent(int eventId, out AudioEventDefinition definition)
    {
        return _events.TryGetValue(eventId, out definition);
    }

    public bool TryGetBus(int busId, out AudioBusDefinition definition)
    {
        return _buses.TryGetValue(busId, out definition);
    }

    public bool TryGetParameter(int eventId, int parameterId, out AudioParameterDefinition definition)
    {
        definition = default;
        if (!_events.TryGetValue(eventId, out AudioEventDefinition audioEvent))
            return false;

        AudioParameterDefinition[] parameters =
            audioEvent.Parameters ?? AudioEventDefinition.EmptyParameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].Id == parameterId)
            {
                definition = parameters[i];
                return true;
            }
        }

        return false;
    }
}
```

约定：

- One-shot 使用 `PlayOneShot`，循环或需要后续控制的声音使用 `StartEvent` 并保存 `AudioHandle`。
- `SetBusVolume` 使用稳定 bus id，范围由后端 clamp 到 `0..1`。
- `NullAudioBackend` 只验证意图、句柄和诊断，不声明真实出声。

## 17. Combat 战斗模块

Combat 当前落地的是确定性战斗物理查询和 kinematic motion。不要假定存在未实现的 `CombatWorld` 或完整动作系统；组合根应直接装配 `CombatPhysicsWorld`、查询对象和 `CombatKinematicMotor`。

Physics Query 最小示例：

```csharp
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;

var world = new CombatPhysicsWorld();
world.UpsertBody(new CombatPhysicsBody(
    new CombatEntityId(1),
    new CombatBodyId(1),
    FixVector3.Zero));
world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
    new CombatBodyId(1),
    new CombatColliderId(1),
    layer: 1,
    localMin: new FixVector3(-Fix64.Half, -Fix64.Half, -Fix64.Half),
    localMax: new FixVector3(Fix64.Half, Fix64.Half, Fix64.Half)));

var header = new CombatQueryHeader(
    queryId: 1,
    kind: CombatQueryKind.Aabb,
    sourceEntityId: CombatEntityId.None,
    traceId: 0,
    actionId: 0,
    sourceOrder: 0,
    layerMask: CombatPhysicsLayerMask.FromLayer(1));
var query = new CombatAabbQuery(
    header,
    new FixVector3(Fix64.FromInt(-2), Fix64.FromInt(-2), Fix64.FromInt(-2)),
    new FixVector3(Fix64.FromInt(2), Fix64.FromInt(2), Fix64.FromInt(2)));

var hits = new List<CombatQueryResult>();
int hitCount = world.Query(CombatPhysicsQuery.From(query), hits);
```

Baked weapon trace 参考数据转 Combat query 的最小示例：

```csharp
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;

var reference = new CombatBakedWeaponTraceReferenceFrame(
    traceId: 7,
    localFrame: 3,
    socketPrev: FixVector3.Zero,
    socketNow: new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
    tipDirectionPrev: new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One),
    tipDirectionNow: new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero));

var profile = new CombatBakedWeaponRuntimeProfile(
    characterScale: Fix64.One,
    weaponLength: Fix64.FromInt(3),
    weaponRadius: Fix64.Half,
    socketOffset: FixVector3.Zero,
    targetMask: CombatPhysicsLayerMask.FromLayer(1));

CombatCapsuleQuery bakedQuery = CombatBakedWeaponTraceAdapter.BuildCurrentBladeCapsule(
    reference,
    profile,
    new CombatEntityId(1),
    actionId: 1001,
    queryId: 42,
    sourceOrder: 0);
```

Motion 最小示例：

```csharp
using MxFramework.Combat.Core;
using MxFramework.Combat.Motion;
using MxFramework.Core.Math;

var motor = new CombatKinematicMotor(CombatMotionConfig.Default);
var state = new CombatMotionState(
    CombatFrame.Zero,
    FixVector3.Zero,
    FixVector3.Zero,
    grounded: true,
    lastCollisionNormal: FixVector3.Zero,
    collisionFlags: CombatMotionCollisionFlags.None);
var input = new CombatMotionInput(
    new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
    jumpPressed: false);

CombatMotionStepResult step = motor.Step(state, input);
CombatMotionState nextState = step.State;
```

约定：

- `CombatPhysicsWorld` 注册已落地的 body，并支持 AABB / Sphere / Capsule 目标 collider registry；Ray / Sphere / Capsule / AABB / Sector / OBB 查询会命中这些目标并按确定性规则排序。CharacterTest playable scene 通过 `GameManager.WorldObjectSceneCache` 集中承载 AABB / Sphere / Capsule static collider records，并保留 legacy `CharacterTestCombatPhysicsAabbMarker` / `CharacterTestCombatPhysicsSphereMarker` / `CharacterTestCombatPhysicsCapsuleMarker` fallback；core registry 由 focused scene tests 验证。
- `CombatKinematicMotor.Step(world, bodyId, state, input)` 可把移动结果同步回已注册 body。
- Combat 使用 `Fix64` / `FixVector3`，不要在核心战斗逻辑中直接读 Unity Physics。
- 动画驱动命中应走 bake reference + runtime profile -> `WeaponTraceFrame` / `CombatCapsuleQuery`，不能在运行时读 Unity Animator、PlayableGraph 或 bone pose 当前状态。
- 武器 `StartHitTrace` 和 projectile impact 的可命中对象、环境触发、stable event id、命中次数约束和受限伤害属性应走公共 `CombatHitEventConfig`。Authoring 首屏使用目标类型 flags；raw `LayerMask` 只作为旧 JSON 兼容或高级诊断。墙面 / 地面命中默认不扣 HP，只有配置 `environmentHitPolicy=BlockedPresentation` 时才产生 blocked / presentation / 后续效果触发点。公共 damage 包含 `damageTypeId`、`baseDamage`、`impactPower`、`launchPower`、`tags` 和 `analysisCategory`；Damage Profile、命中表现 Profile 和伤害公式不属于公共 hit event 配置。
- `onHitBuffs` 属于同一个公共 `CombatHitEventConfig`，CharacterAction Inspector 的 StartHitTrace、ApplyGameplayEffect、projectile direct 和 projectile area 编辑面共用同一套字段与规范化规则。保存时会过滤 `buffId <= 0`，同一 hit event 内重复 BuffId 只保留第一条并稳定排序；持续时间、叠层、来源和效果本体不在引用面板编辑。

## 18. App / Scene Flow

AppFlow 表达 App 状态流转；SceneFlow 串行编排场景加载。Runtime 层只使用稳定字符串 key，Unity 场景名或 path 由 Unity adapter / 项目组合根映射。

AppFlow 状态注册和切换：

```csharp
using MxFramework.Runtime;

var appFlow = new AppFlowController();
appFlow.RegisterState(new SimpleAppState("Boot", tick =>
{
    tick.RequestTransition("Gameplay", "boot-complete");
}));
appFlow.RegisterState(new SimpleAppState("Gameplay"));

AppFlowTransitionResult start = appFlow.Start("Boot", "launch");
appFlow.Tick(frameIndex: 1, deltaTime: 1d / 60d, elapsedTime: 1d / 60d);
appFlow.Tick(frameIndex: 2, deltaTime: 1d / 60d, elapsedTime: 2d / 60d);

AppFlowSnapshot appSnapshot = appFlow.CaptureSnapshot();

public sealed class SimpleAppState : IAppFlowState
{
    private readonly System.Action<AppFlowTickContext> _onTick;

    public SimpleAppState(string stateId, System.Action<AppFlowTickContext> onTick = null)
    {
        StateId = stateId;
        _onTick = onTick;
    }

    public string StateId { get; }
    public void Enter(AppFlowStateContext context, AppFlowTransition transition) { }
    public void Tick(AppFlowTickContext context) { _onTick?.Invoke(context); }
    public void Exit(AppFlowStateContext context, AppFlowTransition transition) { }
}
```

SceneFlow 加载示例：

```csharp
using MxFramework.Runtime;

var sceneFlow = new SceneFlowController(new ImmediateSceneFlowDriver(), "Boot");
SceneFlowResult accepted = sceneFlow.RequestLoad(new SceneFlowRequest(
    "Gameplay",
    SceneFlowLoadMode.Single,
    unloadPreviousScene: true));
sceneFlow.Tick();
SceneFlowSnapshot sceneSnapshot = sceneFlow.CaptureSnapshot();

public sealed class ImmediateSceneFlowDriver : ISceneFlowDriver
{
    public ISceneFlowOperation LoadScene(SceneFlowRequest request)
    {
        return new DoneSceneFlowOperation(request.SceneKey);
    }

    public ISceneFlowOperation UnloadScene(string sceneKey)
    {
        return new DoneSceneFlowOperation(sceneKey);
    }
}

public sealed class DoneSceneFlowOperation : ISceneFlowOperation
{
    public DoneSceneFlowOperation(string sceneKey)
    {
        SceneKey = sceneKey;
    }

    public string SceneKey { get; }
    public bool IsDone => true;
    public float Progress => 1f;
    public bool Success => true;
    public SceneFlowError Error => SceneFlowError.None;
}
```

约定：

- 状态在自身 `Tick` 中只请求 pending transition，真正 `Exit` / `Enter` 在下一次 `Tick` 开始执行。
- SceneFlow busy 时会拒绝新的 load request；调用方读取 `SceneFlowResult.Error` 做 UI 或日志。
- Unity 项目中使用 `UnitySceneFlowDriver`，但 `MxFramework.Runtime` 本身不引用 `SceneManager`。

## 18.1 Logging / Unity Console Feedback

Use `IRuntimeLogger` when a runtime slice needs development feedback without depending on Unity. The contract lives in `MxFramework.Runtime`; the Unity Console adapter lives in `MxFramework.Runtime.Unity`.

Pure runtime or demo slice:

```csharp
using MxFramework.Runtime;

public sealed class GameSlice
{
    private readonly IRuntimeLogger _logger;

    public GameSlice(IRuntimeLogger logger = null)
    {
        _logger = logger ?? NullRuntimeLogger.Instance;
        _logger.Info("GameSlice", "Construct");
    }
}
```

Unity composition root:

```csharp
using MxFramework.Runtime.Unity;
using UnityEngine;

public sealed class GameManager : MonoBehaviour
{
    [SerializeField] private bool _logConsole = true;

    private UnityRuntimeLogger _logger;
    private GameSlice _slice;

    private void OnEnable()
    {
        _logger = new UnityRuntimeLogger(this, "CharacterTest")
        {
            Enabled = _logConsole
        };
        _logger.SetCategoryHeaderColor("GameManager", "#BB8FCE");
        _logger.SetCategoryHeaderColor("GameSlice", "#58D68D");
        _slice = new GameSlice(_logger);
    }
}
```

Rules:

- Do not call `UnityEngine.Debug` from `MxFramework.Runtime` or pure C# runtime slices.
- Logs are developer feedback, not gameplay authority, Replay data, SaveState, or hash input.
- Recoverable failures should still return structured result types such as `RuntimeCommandValidationResult`; logging is only an observation path.
- For long-lived runtime state views, prefer Diagnostics snapshots or Debug UI.
- `UnityRuntimeLogger` supports level colors by default; use `SetCategoryHeaderColor(category, color)` from a Unity composition root for demo-specific category colors.

## 19. Diagnostics 诊断

Diagnostics 模块用 `IFrameworkDebugSource` 暴露只读快照。Editor、工具和运行时 HUD 读取 snapshot，不直接读模块私有字段。

```csharp
using System.Collections.Generic;
using MxFramework.Diagnostics;

IFrameworkDebugSource source = new RuntimeCounterDebugSource("runtime-counter", 42);
if (source.IsAvailable)
{
    FrameworkDebugSnapshot snapshot = source.CreateSnapshot();
    string report = FrameworkDebugReportExporter.ExportText(snapshot);
}

public sealed class RuntimeCounterDebugSource : IFrameworkDebugSource
{
    private readonly int _value;

    public RuntimeCounterDebugSource(string name, int value)
    {
        Name = name;
        _value = value;
    }

    public string Name { get; }
    public FrameworkDebugMode Mode => FrameworkDebugMode.Runtime;
    public bool IsAvailable => true;

    public FrameworkDebugSnapshot CreateSnapshot()
    {
        return new FrameworkDebugSnapshot(
            Name,
            Mode,
            new List<FrameworkDebugSection>
            {
                new FrameworkDebugSection("Counters", "Value=" + _value)
            });
    }
}
```

约定：

- Snapshot 是只读调试报告，不是 SaveState、网络协议或可写命令入口。
- 可写调试操作要另设 command API，不要塞进 `FrameworkDebugSection.Body`。
- 游戏层在组合根中持有 debug source 列表，框架不提供全局 registry。

## 20. Gameplay Component Runtime

Gameplay Component Runtime 使用 generation-safe `GameplayEntityId`、`GameplayComponentWorld`、component store 和 system pipeline。下面示例用 spawn definition 创建实体，并注册一个自定义 system 读取同一个 `ComponentWorld`。

```csharp
using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Runtime;

const int ActorDefinitionId = 1001;

var world = new GameplayComponentWorld();
GameplayCoreComponentSchemaDescriptors.RegisterDiagnostics(world.Schemas);

var spawnRegistry = new GameplayComponentSpawnRegistry();
spawnRegistry.Register(new GameplayComponentSpawnDefinition(
    ActorDefinitionId,
    "example.actor",
    schemaVersion: 1,
    initializers: new IGameplayComponentSpawnInitializer[]
    {
        new GameplayComponentSpawnInitializer<GameplayIdentityComponent>(
            GameplayCoreComponentSchemaDescriptors.IdentityStableId,
            new GameplayIdentityComponent(ActorDefinitionId)),
        new GameplayComponentSpawnInitializer<GameplayTeamComponent>(
            GameplayCoreComponentSchemaDescriptors.TeamStableId,
            new GameplayTeamComponent(1)),
        new GameplayComponentSpawnInitializer<GameplayLifecycleComponent>(
            GameplayCoreComponentSchemaDescriptors.LifecycleStableId,
            GameplayLifecycleComponent.Alive)
    }));

var commandBuffer = new RuntimeCommandBuffer();
var module = new GameplayRuntimeModule(
    new GameplayWorld(),
    new GameplayAbilityRegistry(),
    commandBuffer,
    tickWorldAutomatically: false,
    configureDefaultPipeline: pipeline =>
    {
        pipeline.Add(new GameplayComponentSpawnCommandSystem(spawnRegistry));
        pipeline.Add(new CountAliveSystem());
    },
    componentWorld: world);

commandBuffer.Enqueue(GameplayRuntimeCommandFactory.SpawnComponentEntity(
    RuntimeFrame.Zero,
    ActorDefinitionId,
    traceId: "spawn-actor"));
module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));

GameplayEntityId entity = world.CreateEntitySnapshot()[0];
GameplayComponentStore<GameplayTeamComponent> teams =
    world.GetOrCreateStore<GameplayTeamComponent>();
bool hasTeam = teams.TryGet(entity, out GameplayTeamComponent team);

public sealed class CountAliveSystem : IGameplaySystem
{
    public string SystemId => "example.count-alive";
    public GameplaySystemPhase Phase => GameplaySystemPhase.Diagnostics;
    public int Priority => 0;
    public bool IsEnabled => true;

    public void Tick(GameplaySystemContext context)
    {
        int alive = context.ComponentWorld.CountAlive;
    }
}
```

约定：

- Component store 只接受 `GameplayEntityId`，不要用裸 int entity id 当 key。
- Spawn definition 是组合根输入，不是 world state；SaveState 保存 spawn 后的实体和组件结果。
- 自定义 system 通过 `GameplaySystemContext.ComponentWorld` 访问 component runtime，不直接 drain `RuntimeCommandBuffer`。

### 20.1 Combat Gameplay Aura Buff

持续光环 v0 的核心仍是 framework-only 组合：Gameplay 保存 caster 身上的 aura source，Combat.GameplayBridge 用 `CombatPhysicsWorld.QuerySphere` 找范围内目标，并刷新目标的 component-native Buff。CharacterTest 额外提供 `Guard Aura` 测试接入，用本地 1~9 热栏触发该框架链路，并通过 CharacterAction `SpawnVisualCue` + VFX runtime 播放跟随施法者的可见光环。CharacterTest `Area Confinement` 的固定 zone 现在是 CharacterTest internal mechanic source：frame 6 pending channel 管 60 frame 预警、打断取消和 telegraph/cancel VFX；frame 66 才创建 fixed source，source 管生命周期、落点和 QuerySphere，effect bundle 负责 tick 刷新 Buff 和 create 时 enqueue `vfx.magic.area_confinement.zone`，CharacterTest status VFX bridge 负责把 `AreaConfined` 投影成随目标角色附着的 `vfx.magic.area_confinement.character` 并在状态消失时播放 release VFX，motion policy 再把 Gameplay status 投影为 CharacterTest 主动移动 root。`Guard Aura` 的 Buff、Buff presentation 和 Aura definition 可以由 `buff_authoring.json` 生成；生成 catalog 存在时 CharacterTest 优先使用生成 JSON，缺失或为空时回退内置定义。

```csharp
using MxFramework.Combat.GameplayBridge;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Gameplay;

var componentWorld = new GameplayComponentWorld();
GameplayComponentAuraSourceSchemaDescriptors.RegisterRuntimeHash(componentWorld.Schemas);
GameplayComponentAuraSourceSchemaDescriptors.RegisterSaveState(componentWorld.Schemas);
GameplayComponentBuffSchemaDescriptors.RegisterRuntimeHash(componentWorld.Schemas);
GameplayComponentBuffSchemaDescriptors.RegisterSaveState(componentWorld.Schemas);

var abilityRegistry = new GameplayComponentAbilityRegistry();
abilityRegistry.Register(new CombatGameplayAuraBuffAbility(
    abilityId: 401001,
    auraId: 501001,
    durationFrames: 180,
    rules: GameplayComponentAbilityRuleSet.Empty));

var auraDefinitions = new CombatGameplayAuraBuffDefinitionRegistry(
    new CombatGameplayAuraBuffDefinition(
        auraId: 501001,
        buffId: 601001,
        radius: Fix64.FromInt(5),
        layerMask: CombatPhysicsLayerMask.FromLayer(0),
        relationFilter: GameplayTargetRelationFilter.SameTeam,
        includeCaster: true));

var pipeline = new GameplaySystemPipeline();
pipeline.Add(new GameplayComponentAbilityCommandSystem(abilityRegistry));
pipeline.Add(new GameplayComponentAuraSourceCleanupSystem()); // Resolution priority 65.
pipeline.Add(new CombatGameplayAuraBuffSystem(
    entityMap,
    componentWorld,
    combatPhysicsWorld,
    auraDefinitions,
    auraAnchorProvider)); // Resolution priority 70.
pipeline.Add(new GameplayComponentBuffCleanupSystem()); // Resolution priority 80.
```

约定：

- `CombatGameplayAuraBuffAbility` 只 upsert caster 的 `GameplayComponentAuraSourceEntry`，仍走现有 component ability command、cost 和 cooldown 规则。
- `ICombatGameplayAuraAnchorProvider` 由组合根提供 caster 当前 combat-space center；框架不假设 `CombatEntityGameplayMap` 能读位置。
- `CombatGameplayAuraBuffSystem` 每 tick 只对范围内目标提交 Buff apply request：目标离开范围或光环结束后停止刷新，再由 `GameplayComponentBuffCleanupSystem` 按 Buff definition duration 清理。
- framework v0 不做周期伤害、DOT、治疗 tick、通用 VFX、UI、固定地面光环或 Unity Physics 查询；CharacterTest 的 `Guard Aura` 可见光环和 `Area Confinement` telegraph channel / fixed mechanic source / fixed landing VFX / attached character status VFX / release VFX 只是测试 slice 的表现层接入，不提升为 framework public mechanic API。

### 20.2 Scatter Pressure

散势压力是 component-native 状态：系统只读写 `GameplayComponentWorld`，Combat / UI / Runtime AI Planner 接入由后续 bridge 负责。

```csharp
var world = new GameplayComponentWorld();
GameplayCoreComponentSchemaDescriptors.RegisterDiagnostics(world.Schemas);
GameplayCoreComponentSchemaDescriptors.RegisterRuntimeHash(world.Schemas);
GameplayCoreComponentSchemaDescriptors.RegisterSaveState(world.Schemas);

GameplayEntityId entity = world.CreateEntity();
world.GetOrCreateStore<GameplayScatterPressureComponent>().Set(
    entity,
    new GameplayScatterPressureComponent(
        maxScatterPressure: 100,
        recoveryRate: 5,
        recoveryDelayFrames: 6));

var pressure = new GameplayScatterPressureSystem();
pressure.BandChangedEvents.Subscribe(evt =>
{
    PressureBand band = evt.NewBand;
});
pressure.CohesionBreakEvents.Subscribe(evt =>
{
    GameplayEntityId brokenEntity = evt.EntityId;
});

pressure.Enqueue(new GameplayScatterPressureRequest(entity, delta: 35, traceId: "hit-001"));
pressure.Tick(new GameplaySystemContext(
    new RuntimeFrame(10),
    0d,
    0d,
    new GameplayWorld(),
    new RuntimeCommand[0],
    world.Events,
    componentWorld: world));
```

约定：

- `GameplayScatterPressureSystem` 默认运行在 Resolution phase、priority 70；它先消费 request，再执行自然恢复。
- 状态段事件和破聚事件是 typed event bus，不会写入 `GameplayRuntimeEvent`。
- Schema / Hash / SaveState 由 `GameplayCoreComponentSchemaDescriptors` 统一注册。

## 21. 推荐组合根

游戏层可以集中装配框架模块：

```csharp
public sealed class GameFrameworkBootstrap
{
    public ConfigRegistry Configs { get; }
    public GameplayBuffDefinitionRegistry BuffDefinitions { get; }
    public IModifierFactory ModifierFactory { get; }
    public IAiPlanner AiPlanner { get; }

    public GameFrameworkBootstrap(
        ConfigRegistry configs,
        ILocalizationProvider localizationProvider)
    {
        Configs = configs;
        BuffDefinitions = new GameplayBuffDefinitionRegistry();
        ModifierFactory = new ConfigModifierFactory<BasicModifierConfig>(configs);
        AiPlanner = new SequentialPlanner(maxDepth: 6);
    }
}
```

组合根可以在 Unity `MonoBehaviour`、服务器入口或测试里创建。框架本身不提供全局单例。

## 22. Development Agent：AI Agent 如何找文档

AI agent 进入项目后应按这个顺序读取：

1. `AGENTS.md`：确认项目边界、红线和文档入口。
2. `Docs/README.md`：确认文档职责。
3. `Docs/USAGE.md`：直接查接入流程和最小示例。
4. `Docs/INTERFACES.md`：确认模块接口文档入口和依赖矩阵。
5. `Docs/Interfaces/<Module>.md`：查看具体模块 API 和约定。
6. 具体测试文件：最小 package contract 位于 `Packages/com.vincent.wgameframework/Tests/`；完整 repository validation tests 位于 `Assets/Scripts/WGameFrameworkProject/Tests/Repository/` 与 CharacterTest 自有测试根。

不要让 AI 先通读 `Packages/com.vincent.wgameframework/**`。只有当 `USAGE.md` 和测试示例不够时，才定位读取具体源码。

可视化编辑器约定：

- Unity Editor 工具的用户可见文案默认使用中文。
- 类名、程序集名、菜单路径、配置 key 和日志中的技术标识保留英文。
- 正式编辑器使用 UI Toolkit；IMGUI 只用于临时调试工具。
- 编辑器分为 `编辑模式` 和 `运行模式`。
- 编辑模式不依赖 Play Mode，用于配置、Schema、依赖和静态校验。
- 运行模式默认只读，通过 `IFrameworkDebugSource` 提供快照。
- 不把运行时对象反向写回配置表。
- 所有验证和沙盒结果都应能复制或导出报告。

## 23. 基础能力验证

每次改框架基础能力后，按实际改动确认：

1. Unity 编译无错误。
2. 相关 EditMode / PlayMode 测试通过，或说明无法运行原因。
3. `git status` 只包含本次相关文件。
4. 使用 `git diff`、`rg`、源码阅读和相关测试做影响面检查。
5. 如果本次涉及配置表，打开 `MxFramework > Framework Manager > 编辑模式 > 配置工作台`，点击 `提交前检查`，优先查看 `Temp/MxFrameworkReports/Config/config_precommit.txt`。

当前已覆盖的关键测试：

- Config 表读取、ID 范围、引用和多语言校验。
- Config authoring 模板和报告。
- Config-backed Buff/Modifier factory。
- Buff/Modifier pipeline 基础行为。
- AI WorldState、目标选择、动作前置条件和规划链。
- Input 上下文栈、命令队列、Fake/Recorded 输入源。


关卡编辑使用 `MxFramework/Level Editor/Open Level Editor` 的 **Mx 关卡编辑器**。顶部选择当前关卡，或新建／复制并关联已保存 Scene；角色列表支持 Ctrl / Shift 多选，Inspector 提供批量队伍／分组。角色组版本、卡摘要与动作槽读取现有发布目录，复制组仍使用原 authoring owner；“打开行为组编辑器”当前只打开 Editor Hub。底层行为卡与上层战术卡都保留；切片 AI 组消费上层精确发布身份及费用／CD／准入／效果的完整链尚待补齐。本批保存/诊断、组选择与复制门控、只读缓存检查已合入并通过 Unity 编译及 46 项 focused EditMode 覆盖；实际新建关卡、添加 AI、选择正式三卡战术组、保存/重载和 Play ID/hash/退出恢复已通过。保存、检查、发布和试玩处理当前文档；切换前选择保存、放弃或取消。具体操作、Scene 共享范围和错误定位边界见[关卡编辑工作流](Guides/CHARACTER_TEST_LEVEL_EDITING_WORKFLOW.md)。


CharacterTest 关卡草稿现支持同一 Editor 会话、同一已加载 Scene 生命周期内的脚本重载恢复：保留完整联合文档、规则与角色组绑定、dirty、源 hash 和登记对象范围基线；源冲突时保留恢复稿并拒绝覆盖。独立原生 19/19 与真实域重载后的保存／丢弃／冲突拒写验证通过，其他 Scene 的未保存修改保持不变；未验证进程重启恢复。专用上层行为组编辑器与全部嵌套字段聚焦仍待补齐，入口说明见[关卡编辑工作流](Guides/CHARACTER_TEST_LEVEL_EDITING_WORKFLOW.md)。
