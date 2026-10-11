# Resources 接口

> Status: Current

## 职责

Resources 提供纯 C# 的资源引用、Catalog、Provider、Handle、引用计数和诊断快照契约。`MxFramework.Resources` 不依赖 `UnityEngine` 或 `UnityEditor`；Unity 资源加载后续放在 `MxFramework.Resources.Unity`。

## 公开接口

| 接口/类型 | 用途 |
|-----------|------|
| `ResourceKey` | 稳定资源引用，包含 `Id`、`TypeId`、`Variant`、`PackageId` |
| `ResourceTypeIds` | 框架内置常用类型 ID 常量 |
| `ResourceCatalog` | 一组资源 entry 的目录 |
| `ResourceCatalogEntry` | 单个资源的 provider、address、依赖和覆盖声明 |
| `IResourceManager` / `ResourceManager` | 资源解析、加载、释放、Catalog 挂载和诊断入口 |
| `IResourceCatalogQuery` | 查询当前全局 Catalog 中的资源 key / entry，不修改 `IResourceManager` 加载契约 |
| `IResourceProvider` | Provider 抽象，由 memory、Unity、AssetBundle 等实现 |
| `MemoryResourceProvider` | noEngine 测试 Provider |
| `ResourcesProvider` | Unity `Resources` 目录 Provider，位于 `MxFramework.Resources.Unity` |
| `UnityResourceTypeResolver` | `ResourceTypeIds` 到 Unity 类型的映射 |
| `AssetBundleProvider` | 本地 AssetBundle Provider，地址格式为 `bundleName|assetName` |
| `SceneAssetBundleProvider` | 本地 Scene AssetBundle Provider，地址格式为 `bundleName|Assets/Scenes/xxx.unity`，只供 `ResourceSceneFlowDriver` 使用 |
| `ResourceSceneFlowDriver` | 从 Catalog 解析 `ResourceTypeIds.Scene` entry，加载 scene bundle 后通过 `SceneManager` 编排场景流 |
| `IGlobalResourceEditorRuntimeFactory` | Runtime 定义的 Editor bootstrap 注入点，避免 Runtime 程序集引用 `UnityEditor` |
| `EditorAssetDatabaseResourceProvider` | Editor-only `editorAssetDatabase` Provider，从 `AssetDatabase` 直读项目内 `Assets/...` 资源 |
| `EditorSceneFlowDriver` | Editor-only SceneFlow driver，通过 `EditorSceneManager.LoadSceneAsyncInPlayMode` 直读 `.unity` 场景 |
| `RemoteBundleProvider` | Remote / file URL AssetBundle Provider，支持下载到本地 cache、SHA-256 校验和缓存命中 |
| `IAssetBundleDependencyProvider` | AssetBundle 依赖解析接口 |
| `AssetBundleManifestDependencyProvider` | 使用 `AssetBundleManifest.GetAllDependencies` 的依赖解析器 |
| `StreamingResourceCatalogLoader` | 从 StreamingAssets 或文件读取 Catalog JSON |
| `GeneratedResourceArtifactSetLoader` / `GeneratedResourceArtifactSet` | Offline 启动前校验一次完整生成的 target、Profile/Catalog identity、mapping/build/source fingerprint、必需 artifact 和逐文件 SHA-256 |
| `GeneratedResourceCatalogMountLoader` / `ResolvedResourceCatalogMountSet` | 按编译后的 CatalogMount 顺序解析 StreamingAssets 或 composition-root Catalog，验证 canonical fingerprint、完整 key allowlist、override intent 和 Provider 可用性 |
| `ResourceCatalogCanonicalFingerprint` | 对 concrete `ResourceCatalog` 计算确定性 canonical SHA-256，供 CatalogMount 生产与启动复验 |
| `GlobalResourceRuntimeOptions` / `GlobalResourceRuntimeBootstrap` | 组合 Editor / Offline 全局资源服务；`CompositionRootCatalogs` 只允许满足已声明的 CatalogMount；实际 Player 的运行平台目标总会与显式 `BuildTargetName`（若有）及 artifact target 核对 |
| `GlobalPlayerResourceBuildProfileBuilder.ValidatePublishedArtifacts` / `DescribeBuildInput` | Editor-only 复用校验与 G 域输入回执；前者逐文件核对 committed marker，并比较 build report 与当前 plan/source 身份 |
| `ResourceHandle<T>` | 加载成功后的句柄，释放必须走 `IResourceManager.Release` |
| `ResourceLoadResult<T>` / `ResourceError` | 非异常加载结果和错误描述 |
| `IResourceOperation<T>` | 异步加载操作抽象，暴露 `IsDone`、`IsCancelled`、`Progress`、`Result` 和 `Cancel()` |
| `ResourceDebugSnapshot` | Catalog、Provider、加载资源、引用计数、retain budget 和最近错误 / eviction 快照 |
| `ResourceDebugSource` | 将资源快照接入 Diagnostics 的 `IFrameworkDebugSource` |
| `ResourceCatalogValidator` | noEngine Catalog 结构校验 |
| `ResourceCatalogValidationReport` | Catalog 校验问题报告 |
| `ResourceCatalogEditorValidator` | Editor 侧资源存在性和类型校验 |
| `SampleResourceCatalogBuilder` | Editor 侧 `mxframework.samples` Catalog 扫描 / 生成入口 |
| `SamplePlayerResourceCatalogBuilder` | Editor 侧 `mxframework.samples` Player AssetBundle / Streaming catalog 生成入口 |
| `IResourcePreloadService` / `ResourcePreloadService` | 预加载 group / scene warmup 策略服务，批量加载 explicit keys 和 labels，并返回可轮询 operation |
| `ResourcePreloadPlan` | 预加载计划，包含 groupId、labels、explicit keys、failFast、maxConcurrentLoads |
| `ResourcePreloadResult` | 预加载结果，包含 requested / loaded / failed count 和错误列表 |
| `ResourceGroupHandle` | 预加载 group 持有的 handles；通过 `ReleaseGroup` 统一释放 |
| `ResourceVariantProfile` | 显式资源变体 fallback profile，由 `ResourceManager` 在解析阶段使用 |
| `ResourceRuntimeConfigurationIdentity` | `ResourceManager` provider/catalog/variant 拓扑的 opaque sealed identity；提供预计算 diagnostic fingerprint 和 NoAlloc 只读摘要 |
| `ResourcePlanPreparation` | 绑定 orchestrator owner、sealed manager identity 与完整 plan fingerprint 的强 preparation lease |
| `ResourcePlanAcquireResult` / `ResourcePlanPreparationError` | strong preparation exact-once acquire 的 typed 成功/失败结果 |
| `ResourceRetainMode` / `ResourceRetainPolicy` | 引用计数归零后的底层 record 保留策略，包含 `None`、`Timed`、`KeepAlive`、`Budgeted` |
| `ResourceEvictionRecord` | retained record 被释放时的诊断记录 |

## 使用约定

- 配置和 Gameplay 只保存 `ResourceKey`，不保存 Unity 路径或 `UnityEngine.Object`。
- 普通 Resource Catalog 覆盖 Unity 资产和 provider 资源，例如 `AudioClip`、`AnimationClip`、`Texture2D`、`GameObject`、AssetBundle 和 RemoteBundle。FMOD bank `.bank` 文件和 FMOD event path/guid 不进入普通 Catalog；它们通过 Audio/FMOD 设置、`AudioEventDefinition` 和后续单独批准的 bank manifest/provider 管理。
- `ResourceKey.Id` 使用小写命名空间，允许小写字母、数字、`.`、`_`、`-`。
- Character Resource Package 的 package-local `ResourceKey` 生成规则为 `char.<packageId>.<typeSegment>.<localId>[.<variant>]`，其中 `animation` 的 key segment 固定为 `anim`；所有段落先归一为小写、`.`、`_`、`-` 和数字。
- Character Resource Package catalog 不等同于 Unity 项目 ResourceCatalog。包内 catalog 只记录包内 relative path、source format、content/import/dependency hash、import hints、dependency graph、conflict policy 和 provenance；C0.6 `CharacterAuthoringCompiler` 先输出 `CharacterPackageResourceMapping` 和 `CharacterUnityImportWritePlan`，Unity Importer Bridge 导入后再把 package-local `ResourceKey` 映射到项目 ResourceCatalog entry。
- Character Resource Package C0.5 不保存 `UnityEngine.Object`、Prefab、`AnimationClip`、Material 或 Unity asset GUID。Unity target path 只能通过 `importHints.targetPathPolicy` + project-relative `targetRelativePath` 表达，绝不能是绝对路径或逃逸路径。
- Character Resource Package v1 模型 entry 支持 `fbx`、`gltf`、`glb`，但 authority 由 usage 决定：Humanoid `characterModel` 使用 FBX 权威源；GLB/glTF 用于非 Humanoid / 静态模型、公共武器或由 FBX 派生的 preview artifact。动画 authoring 选择已导入 clip / `.anim` 资源，不要求维护角色包内 locomotion GLB。C0.6 compiler 只输出 import target 和 hash，不转换格式；C2 Importer Bridge 使用 Unity `ModelImporter` 导入 FBX，GLB/glTF 需要项目提供对应 importer。
- C2 `config/unity_resource_catalog.json` 第一版使用 `memory` provider，并在 `providerData.assetPath` 保存导入后的 Unity project asset path，同时保留 `packageResourceKey`、`stableId`、`sourceRelativePath`、`sourceFormat`、`usage`、`importHash`、`dependencyHash` 和 `sourcePackageHash`，供 Editor Play Mode / Workstation / 后续 Runtime Spawn 组合根挂载。
- 常用 Unity 资产类型使用 `ResourceTypeIds`，内置常量包括 `GameObject`、`Texture2D`、`Sprite`、`AudioClip`、`AnimationClip`、`AvatarMask`、`TextAsset`、`Material`、`Scene`、UI Toolkit 资源类型和基础 `String` / `Object`；调用方优先通过 `Load<T>` 获得类型检查。
- 同一 Catalog 内 `id + type + variant` 必须唯一。
- 全局冲突默认失败；高层覆盖必须设置 `allowOverride`，并保持类型一致。
- CharacterStudio package 写入使用 manifest、resource catalog、application、geometry 和 validation 等同一保存事务所拥有文档的 `sha256` revision 做低频乐观并发控制；保存与 schema migration 都携带 `expectedRevision`，磁盘变化返回 `CHARACTER_PACKAGE_WRITE_CONFLICT` / HTTP 409，只有显式 `force` 才覆盖。独立 endpoint 管理的 import-material state 不混入该 revision。
- `PackageId` 非空时精确路由到指定包；为空时使用合并后的全局 entry。
- 重复释放 handle 不抛异常，但会记录 `HandleReleased` 诊断错误。
- M1 只提供 noEngine 契约和 `MemoryResourceProvider`，不加载 Unity asset。
- M2 提供 Unity `ResourcesProvider`，只用于 Demo、小样例和少量常驻资源。
- `ResourcesProvider` 通过 `UnityEngine.Resources.Load` 加载，缺失资源返回 `ResourceErrorCode.NotFound`。
- `UnityResourceTypeResolver` 会把 `ResourceTypeIds.AvatarMask` 映射到 `UnityEngine.AvatarMask`，供 MxAnimation layer mask 通过正式 `ResourceCatalog` / `ResourceManager` 路径加载。
- `ResourcesProvider.Release` 只对非 `GameObject` asset 调用 `UnityEngine.Resources.UnloadAsset`；Prefab 实例仍由调用方销毁。
- M3 提供 `AssetBundleProvider` 和 `StreamingResourceCatalogLoader`。
- AssetBundle address 使用 `bundleName|assetName`，非法地址不触碰文件系统。
- AssetBundle 依赖由 `IAssetBundleDependencyProvider` 提供，Provider 加载 asset 前先加载依赖 bundle。
- bundle ref-count 归零后调用 `AssetBundle.Unload(true)`。
- Scene 不走普通 `IResourceManager.Load<T>` 对象资源加载。`ResourceTypeIds.Scene` entry 由 `ResourceSceneFlowDriver` 读取，provider 必须是 `sceneAssetBundle`，地址为 `bundleName|Assets/.../*.unity`。
- `GlobalResourceRuntimeMode.Editor` 在 Unity Editor 中通过已注册的 `IGlobalResourceEditorRuntimeFactory` 从 `global_resource_mapping_profile.v2.json` 的唯一 compiled plan 生成内存 catalog / preload groups，并使用 `editorAssetDatabase` 直读项目内最新资源；不要求先重建 AssetBundle。
- Editor SceneFlow 使用 `EditorSceneFlowDriver` 和 `EditorSceneManager.LoadSceneAsyncInPlayMode` 读取 `Scene` entry 的 `Assets/.../*.unity` 路径，可加载未加入 Build Settings 的开发场景。
- `GlobalResourceRuntimeMode.Offline` 是 Player 本地模拟路径。它先读取 `global_resource_artifact_set.json`，逐项校验必需文件和 SHA-256，再从 manifest 指定的相对路径加载 catalog / mounts / preload / dependency；缺文件、混代、target 不符或 identity/fingerprint 不一致均在注册 Provider 前失败。
- Unity Builder 先在 `Temp` 完成 Bundle 和 JSON staging，再逐文件替换，最后写 `global_resource_artifact_set.json` 作为 generation commit marker；中途失败不会让 Runtime 接受混合新旧文件。Build report 在 artifact marker 之后单独原子更新，Resource Manager 只有在 report 与 artifact set 的 Profile、Catalog、target、mapping/build/source fingerprint 全部一致时才显示可 Offline 验证。
- `CatalogMount.expectedCatalogFingerprint` 是必填的 canonical `sha256:<64 hex>`。Streaming mount 必须位于 `StreamingAssets` 根下；composition-root mount 必须由 `GlobalResourceRuntimeOptions.CompositionRootCatalogs` 精确提供一次。未声明 Catalog、重复 mount/order/key、allowlist 缺失、`allowOverride` 不一致或 Provider 未注册都会阻塞启动。
- `CompositionRootCatalogs` 不是任意 Catalog 注入口；它只满足编译 Profile 声明的 mount。应用或测试自身、且不属于全局 mapping 的临时 Catalog，应由该应用组合根在全局服务创建后显式 `ResourceManager.AddCatalog`，不要伪装成 mapping external row。
- Resource mapping 的 concrete build target 必须来自显式调用或最新 Unity build report；Authoring/CLI 不再猜测 `StandaloneOSX` 或当前平台。Bundle policy 可以持久化 `ActiveBuildTarget` 意图，但编译时必须解析为本次 concrete target。Unity 一次 Bundle build 只接受一种 `lz4` / `lzma` / `uncompressed` policy；混合 compression 计划在构建前拒绝。
- Common resource sync 只刷新 `demo.samples.global-profile` 的完整 producer snapshot。尚未生成 Unity GUID 的新复制资源保留 runtime key、Bundle/preload intent，并使用 `GeneratedUnityAsset` locator 让 Unity source resolution 显式报告待导入；它们不会暂时伪装成 `editorOnly`，也不会触发其他 first-party producer 的全量替换。
- FairyGUI package loading exposes `MxFairyGuiResourceBridge.LoadPackageAsync`. The operation preserves pending provider work instead of cancelling it as a synchronous miss, validates package bytes before component creation, owns all package-resource handles until a load scope is produced, and releases partial handles on failure or cancellation. Synchronous `LoadPackage` still maps an unfinished operation to `LoadPending` and cancels that synchronous attempt.
- CharacterTest runtime binary catalogs also use StreamingAssets as a Player mirror root: `Assets/StreamingAssets/MxFramework/CharacterTest/RuntimeCatalogs/...`. This mirror is not a `ResourceCatalog`; it is a Player bootstrap artifact set for Ability/Buff/Combat/VFX generated runtime catalogs. Editor/test loaders still prefer `Assets/MxFrameworkGenerated/...`, while Player loaders prefer `Application.streamingAssetsPath` and then fall back to generated path / embedded compatibility.
- Scene AssetBundle 必须与普通 asset bundle 分开构建；同一个 bundle rule 混合 `Scene` 和非 `Scene` 资源会被 Global Builder 报错。
- Player 仍需要一个最小 Boot/Bootstrap 场景在 Unity Build Settings；内容场景应通过 `global_resource_mapping_profile.v2.json`、generated catalog 和 `SceneFlowController` 加载，不依赖 Build Settings scene list。
- Streaming Catalog JSON 字段名使用 `allowOverride`，不是 `override`。
- Catalog JSON entry 应显式包含 `variant`；无变体时写空字符串。
- M6.0 已补充可选 `providerData` 字典，用于 RemoteBundle、Addressables 或项目层 Provider 的专用字段；核心只保存和传递，不解释其语义。
- M4 支持 Mod Package 在 `mod.json.resourceCatalog` 中声明可选 Catalog 路径。
- `Config.Runtime` 只校验并暴露 `ResourceCatalogFilePath`；实际读取 Catalog 和挂载 `ResourceManager` 由 Unity composition root、Preview、Demo 或测试完成。
- 旧包不声明 `resourceCatalog` 仍有效；声明后必须是包根内相对路径，文件缺失或路径逃逸会导致包加载失败。
- M5 提供 `ResourceDebugSource`，可把 `IResourceManager.CreateDebugSnapshot()` 输出为通用 `FrameworkDebugSnapshot`。
- `ResourceCatalogValidator` 可在无 Unity 环境下校验 key、provider、address、重复项和依赖图。
- `ResourceCatalogEditorValidator` 位于 `MxFramework.Editor`，使用 `AssetDatabase` 校验 `resources` / `assetBundle` 入口资源存在和主资源类型。
- `ResourceCatalogEditorValidator` 会额外校验 `memory` entry 的 `providerData.assetPath`，用于 Demo / Editor Play Mode 将已序列化 Unity 对象纳入 Catalog 和 warmup。
- Editor 校验只报告问题，不自动修改 Catalog 或导入设置。
- `SampleResourceCatalogBuilder` 会扫描 / 校验框架正式样例资源规则，并通过 `MxFramework/Samples/Generate Resource Catalog` 确定性生成 `Assets/Config/MxFramework/ResourceCatalogs/mxframework_samples_resource_catalog.json`；当前输出仍是 Editor Play Mode / Demo 用的 `memory` provider catalog，Player 路径留给 AssetBundle / Streaming catalog 切片。
- VFX application entry 由 `Assets/Config/MxFramework/Vfx/vfx_published_bindings.json` 单向投影：Published Binding Manifest 决定完整稳定 `ResourceKey`、address 和当前正式 Prefab；`SampleResourceCatalogBuilder` 遍历全部 binding 并补充 application catalog 的 provider、labels 与 `providerData.assetPath`。Catalog 是可重建产物，不能反向初始化或刷新 Manifest；新增稳定 VFX 不需要再登记一份硬编码规格。SceneProfile、Candidate 路径和生成后的 Catalog 都不能成为第二绑定事实源。
- `SamplePlayerResourceCatalogBuilder` 会构建最小 Player smoke AssetBundle，并生成 `Assets/StreamingAssets/MxFramework/Samples/mxframework_samples_player_catalog.json`；该 catalog 使用 `assetBundle` provider，bundle root 为 `MxFramework/Samples/Bundles`。
- 当前 Player sample path 覆盖本地文件系统 / 桌面 Player 形态；Android / WebGL 的 StreamingAssets catalog 和 bundle 读取不能假设同步 `System.IO.File` / `AssetBundle.LoadFromFile` 一定可用，后续需要 UnityWebRequest 或平台专用读取分支。
- `RuntimeVerticalSliceRunner` 默认资源绑定会先跑 Editor Play Mode `memory` catalog warmup，再尝试 Player `assetBundle` StreamingAssets smoke；结果通过 `ResourceWarmupSummary` 和 `ResourceBindingLogLines` 暴露给 Demo UI / tests。
- Runtime Preview 当前仍不隐式依赖 `MxFramework.Resources` / `MxFramework.Resources.Unity`。Preview 场景目标、UI Toolkit 资产和 ResourceManager 应由外层组合根显式注入，避免 Preview.Runtime 把资源 Provider 变成隐藏依赖。
- M6A 已新增 Preload Group + Scene Warmup，作为独立策略服务，不把 PreloadGroup 做成 Provider，也不修改 `IResourceManager` 签名。
- `ResourcePreloadService` 会先按 `ResourcePreloadPlan.ExplicitKeys` 和 `Labels` 收集 key，去重后返回 `IResourceOperation<ResourcePreloadResult>`；调用方可以轮询 `IsDone` / `Progress`，完成后读取 `Result`。
- `ResourcePreloadPlan.MaxConcurrentLoads` 控制预加载调度窗口；operation 会在已启动 load 完成后继续补位，不创建后台线程。
- `ResourcePreloadPlan.FailFast=true` 时首个失败后停止继续启动新 load，并取消仍在途的 load；默认会收集所有失败并保留已成功加载的 handles。
- `ResourcePreloadOperation.Cancel()` 或已取消的 `CancellationToken` 会 latch cancellation、停止启动新 load，并对所有已启动 operation 发出一次协作取消；它继续轮询非协作 operation 到真实 terminal，逐个释放 late-success handle，只有全部在途工作达到 safe terminal 后才以 `ResourceErrorCode.Cancelled` 结束。
- `FailFast=true` 的首次失败同样进入 safe-drain：停止补位、协作取消并继续轮询全部已启动 operation；任何失败前已完成或 drain 期间 late-success 的 handle 都 exact-once release，失败结果不携带可 acquire 的 partial group。
- 当前 `ResourceManager.LoadAsync` 仍兼容 noEngine immediate 语义；Unity Provider 的异步能力必须在 Unity 主线程轮询或完成，不能假设 worker thread 可触碰 `UnityEngine.Object` / `AssetBundle` API。
- `ResourcePreloadService.ReleaseGroup` 幂等；调用方释放 group 后，底层逐个调用 `IResourceManager.Release`。
- `ResourceManager.SealRuntimeConfiguration()` 是低频一次性冻结点：identity 绑定 manager instance、provider instance/type/id、完整 catalog entry topology、variant profile 与预计算 diagnostic fingerprint；重复 seal 返回同一 identity。seal 后 `RegisterProvider`、`AddCatalog`、`SetVariantProfile` fail-closed，identity 的公开读取和 `TryGetRuntimeConfigurationIdentity` 为 `NoAlloc`。
- `ResourcePlanOrchestrator.Prepare(identity, plan)` 创建 internal-construction 的 owner-bound `ResourcePlanPreparation`；其 steady `Status` / `Progress` / `Error` 轮询初始化后 `NoAlloc`。`AcquirePrepared` 只接受同 manager、同 orchestrator owner、同完整 `PlanFingerprint` 且 fully successful 的 preparation，并通过 `ResourcePlanAcquireResult` typed error 在 ownership transfer 前拒绝 incomplete、failed、cross-manager、cross-plan、foreign-owner 与 repeated acquire。
- strong acquire 会把成功 group 的 handles exact-once 转移给独立 `ResourcePlanSession`；尚未 acquire 的 candidate preparation 必须在 rollback/放弃时调用幂等、非阻塞的 `Release()` / `Dispose()`，并保留 lease、继续轮询 `Status`，直到从 `Releasing` 到达 `Released` 后 rollback 才算完成。该过程只释放自身 preload ownership，acquire 后再 dispose 不会释放 session。candidate session rollback/release 同样只释放自身 handles，不改变同一 sealed configuration 上已有 live session 的引用或 plan。旧 `Preload` / `Acquire(ResourcePlan, ResourcePreloadResult)` API 保留兼容，但 startup SessionBridge 只能消费 strong preparation lease。
- MxAnimation warmup 直接复用 `ResourcePreloadService`。animation set 只声明 `ResourceKey` / label / group id；版本、hash 和 partial failure diagnostics 由 Animation 层包装，不新增 `MxFramework.Animation.Resources`。
- 同一资源被 warmup group 和其它 consumer 同时持有时，释放 warmup group 只减少该 group 的引用；底层卸载仍由 `ResourceManager` ref-count / retain policy 决定。
- M6A 诊断信息先放在 `ResourcePreloadResult` 和 `ResourceGroupHandle`，暂不扩展 `ResourceDebugSnapshot` group 列表。
- M6B 已新增显式 Variant Profile 和 RetainPolicy；Variant 解析不写进 Provider，RetainPolicy 不改变 handle released 语义。
- `ResourceManager.SetVariantProfile` 只影响 key 到 Catalog entry 的解析顺序；`PackageId` 非空时仍只在指定包内解析。
- Variant fallback 必须由 `ResourceVariantProfile.FallbackVariants` 显式声明；框架不会自动猜测 `pc`、`high`、`default` 或空 variant。
- `ResourceManager.SetRetainPolicy(ResourceRetainPolicy.None)` 保持旧行为：引用计数归零立即卸载。
- `ResourceRetainPolicy.Timed` 在 noEngine 层通过 `AdvanceRetainTime` 或 `AdvanceRetainFrames` 手动推进，不依赖 Unity 时间。
- `ResourceRetainPolicy.KeepAlive` 会在 handle release 后保留底层 loaded record，直到 `EvictRetainedResources` 显式清理。
- `ResourceRetainPolicy.Budgeted(maxRetainedBytes)` 使用 `ResourceCatalogEntry.Size` 作为 retained bytes 估算；`Size=0` 表示当前没有可用 size signal，不会凭空估算 Unity / 平台内存。
- `ResourceCatalogEntry.ProviderData["retainPriority"]` 是框架保留 metadata key，可选声明 retain priority，整数越大越晚被预算策略驱逐；缺省或非法值按 `0` 处理。
- Budgeted retain 的自动驱逐顺序确定为：低 priority 优先、较早进入 retained 状态优先、最后按 `ResourceKey.ToString()` 排序打平。
- `KeepAlive` retained record 视为 pinned，不会被 budgeted policy 自动驱逐；`EvictRetainedResources()` 是显式手动清理，仍可清理所有 retained record。
- `ResourceDebugSnapshot` 已包含 `RetainedCount`、`EvictableCount`、`PinnedCount`、`RetainPolicyCount`、`RetainedBytes`、`RetainBudgetBytes`、`RetainBudgetOverageBytes`、`RetainBudgetExceeded` 和 `RecentEvictions`。
- M6C 已新增 `RemoteBundleProvider`，ProviderId 为 `remoteBundle`，位于 `MxFramework.Resources.Unity`。
- RemoteBundle address 继续使用 `bundleName|assetName`；`providerData.url` 指向 bundle source，`providerData.cacheKey` 控制缓存文件名。
- RemoteBundle hash 使用 `ResourceCatalogEntry.Hash`，支持 `sha256:<hex>` 或裸 hex；hash mismatch 返回 `ProviderFailed`。
- RemoteBundle 当前支持 file URL 和同步 UnityWebRequest 下载；不做重试、断点续传、签名、权限授权或 CDN 发布工具。
- Runtime Showcase 资源测试已改用 `mxframework.samples` catalog：`RuntimeVerticalSliceRunner` 在 Editor Play Mode 通过 `TempImportedResourceCatalogEditorBootstrap` 注册的 `MemoryResourceProvider` 跑 package / StartScreen / Combat / StatusEffects / MagicEffects warmup，再用 `ResourceKeyConfigProfile.CreateSample()` 直接加载 Katana、StatusAura、StartScreen 贴图和 MagicEffects AudioClip，并在同一轮测试中释放 handles / groups。MagicEffects warmup 也包含 `vfx.magic.fireball` prefab，用于验证 VFX `GameObject` 样例能通过同一 catalog 路径预热。

## 测试入口

`Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Resources/`
`Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Config/ModPackageCatalogTests.cs`

- MemoryResourceProvider.Register 保持共享资产语义；RegisterOwned 仅用于私有模板，并要求显式释放回调。Dispose 对所有 owned 资产恰好调用一次释放，即使其 address 已被替换；不会释放 Register 的共享资产。GlobalResourceRuntimeServices.RegisterOwnedProvider 将 provider 的销毁归属交给会话，在 loaded handle 清理后 Dispose，初始化失败也须回收。两者为 LowFreqAlloc composition/cleanup API。
