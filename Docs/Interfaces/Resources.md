# Resources 接口

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
| `IResourceCatalogQuery` | 按 label 查询当前全局 Catalog 中的资源 key，不修改 `IResourceManager` 契约 |
| `IResourceProvider` | Provider 抽象，由 memory、Unity、AssetBundle 等实现 |
| `MemoryResourceProvider` | noEngine 测试 Provider |
| `ResourcesProvider` | Unity `Resources` 目录 Provider，位于 `MxFramework.Resources.Unity` |
| `UnityResourceTypeResolver` | `ResourceTypeIds` 到 Unity 类型的映射 |
| `AssetBundleProvider` | 本地 AssetBundle Provider，地址格式为 `bundleName|assetName` |
| `RemoteBundleProvider` | Remote / file URL AssetBundle Provider，支持下载到本地 cache、SHA-256 校验和缓存命中 |
| `IAssetBundleDependencyProvider` | AssetBundle 依赖解析接口 |
| `AssetBundleManifestDependencyProvider` | 使用 `AssetBundleManifest.GetAllDependencies` 的依赖解析器 |
| `StreamingResourceCatalogLoader` | 从 StreamingAssets 或文件读取 Catalog JSON |
| `ResourceHandle<T>` | 加载成功后的句柄，释放必须走 `IResourceManager.Release` |
| `ResourceLoadResult<T>` / `ResourceError` | 非异常加载结果和错误描述 |
| `IResourceOperation<T>` | 异步加载操作抽象，M1 提供 immediate 实现 |
| `ResourceDebugSnapshot` | Catalog、Provider、加载资源、引用计数和最近错误快照 |
| `ResourceDebugSource` | 将资源快照接入 Diagnostics 的 `IFrameworkDebugSource` |
| `ResourceCatalogValidator` | noEngine Catalog 结构校验 |
| `ResourceCatalogValidationReport` | Catalog 校验问题报告 |
| `ResourceCatalogEditorValidator` | Editor 侧资源存在性和类型校验 |
| `IResourcePreloadService` / `ResourcePreloadService` | 预加载 group / scene warmup 策略服务，批量加载 explicit keys 和 labels |
| `ResourcePreloadPlan` | 预加载计划，包含 groupId、labels、explicit keys、failFast、maxConcurrentLoads |
| `ResourcePreloadResult` | 预加载结果，包含 requested / loaded / failed count 和错误列表 |
| `ResourceGroupHandle` | 预加载 group 持有的 handles；通过 `ReleaseGroup` 统一释放 |
| `ResourceVariantProfile` | 显式资源变体 fallback profile，由 `ResourceManager` 在解析阶段使用 |
| `ResourceRetainMode` / `ResourceRetainPolicy` | 引用计数归零后的底层 record 保留策略 |
| `ResourceEvictionRecord` | retained record 被释放时的诊断记录 |

## 使用约定

- 配置和 Gameplay 只保存 `ResourceKey`，不保存 Unity 路径或 `UnityEngine.Object`。
- 普通 Resource Catalog 覆盖 Unity 资产和 provider 资源，例如 `AudioClip`、`AnimationClip`、`Texture2D`、`GameObject`、AssetBundle 和 RemoteBundle。FMOD bank `.bank` 文件和 FMOD event path/guid 不进入普通 Catalog；它们通过 Audio/FMOD 设置、`AudioEventDefinition` 和后续单独批准的 bank manifest/provider 管理。
- `ResourceKey.Id` 使用小写命名空间，允许小写字母、数字、`.`、`_`、`-`。
- 常用 Unity 资产类型使用 `ResourceTypeIds`，内置常量包括 `GameObject`、`Texture2D`、`Sprite`、`AudioClip`、`AnimationClip`、`AvatarMask`、`TextAsset`、`Material`、UI Toolkit 资源类型和基础 `String` / `Object`；调用方优先通过 `Load<T>` 获得类型检查。
- 同一 Catalog 内 `id + type + variant` 必须唯一。
- 全局冲突默认失败；高层覆盖必须设置 `allowOverride`，并保持类型一致。
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
- M6A 已新增 Preload Group + Scene Warmup，作为独立策略服务，不把 PreloadGroup 做成 Provider，也不修改 `IResourceManager` 签名。
- `ResourcePreloadService` 会先按 `ResourcePreloadPlan.ExplicitKeys` 和 `Labels` 收集 key，去重后调用现有 `LoadAsync<object>`。
- `ResourcePreloadPlan.MaxConcurrentLoads` 第一版保留字段，当前 noEngine immediate async 实现仍按顺序加载。
- `ResourcePreloadPlan.FailFast=true` 时首个失败后停止继续加载；默认会收集所有失败并保留已成功加载的 handles。
- `ResourcePreloadService.ReleaseGroup` 幂等；调用方释放 group 后，底层逐个调用 `IResourceManager.Release`。
- MxAnimation warmup 直接复用 `ResourcePreloadService`。animation set 只声明 `ResourceKey` / label / group id；版本、hash 和 partial failure diagnostics 由 Animation 层包装，不新增 `MxFramework.Animation.Resources`。
- 同一资源被 warmup group 和其它 consumer 同时持有时，释放 warmup group 只减少该 group 的引用；底层卸载仍由 `ResourceManager` ref-count / retain policy 决定。
- M6A 诊断信息先放在 `ResourcePreloadResult` 和 `ResourceGroupHandle`，暂不扩展 `ResourceDebugSnapshot` group 列表。
- M6B 已新增显式 Variant Profile 和 RetainPolicy；Variant 解析不写进 Provider，RetainPolicy 不改变 handle released 语义。
- `ResourceManager.SetVariantProfile` 只影响 key 到 Catalog entry 的解析顺序；`PackageId` 非空时仍只在指定包内解析。
- Variant fallback 必须由 `ResourceVariantProfile.FallbackVariants` 显式声明；框架不会自动猜测 `pc`、`high`、`default` 或空 variant。
- `ResourceManager.SetRetainPolicy(ResourceRetainPolicy.None)` 保持旧行为：引用计数归零立即卸载。
- `ResourceRetainPolicy.Timed` 在 noEngine 层通过 `AdvanceRetainTime` 或 `AdvanceRetainFrames` 手动推进，不依赖 Unity 时间。
- `ResourceRetainPolicy.KeepAlive` 会在 handle release 后保留底层 loaded record，直到 `EvictRetainedResources` 显式清理。
- `ResourceDebugSnapshot` 已包含 `RetainedCount`、`EvictableCount`、`PinnedCount`、`RetainPolicyCount` 和 `RecentEvictions`。
- M6C 已新增 `RemoteBundleProvider`，ProviderId 为 `remoteBundle`，位于 `MxFramework.Resources.Unity`。
- RemoteBundle address 继续使用 `bundleName|assetName`；`providerData.url` 指向 bundle source，`providerData.cacheKey` 控制缓存文件名。
- RemoteBundle hash 使用 `ResourceCatalogEntry.Hash`，支持 `sha256:<hex>` 或裸 hex；hash mismatch 返回 `ProviderFailed`。
- RemoteBundle 当前支持 file URL 和同步 UnityWebRequest 下载；不做重试、断点续传、签名、权限授权或 CDN 发布工具。
- Runtime Showcase 资源测试已改用 `mxframework.samples` catalog：`RuntimeVerticalSliceRunner` 在 Editor Play Mode 通过 `TempImportedResourceCatalogEditorBootstrap` 注册的 `MemoryResourceProvider` 跑 package / StartScreen / Combat / StatusEffects / MagicEffects warmup，再用 `ResourceKeyConfigProfile.CreateSample()` 直接加载 Katana、StatusAura、StartScreen 贴图和 MagicEffects AudioClip，并在同一轮测试中释放 handles / groups。

## 测试入口

`Assets/Scripts/MxFramework/Tests/Resources/`
`Assets/Scripts/MxFramework/Tests/Config/ModPackageCatalogTests.cs`
