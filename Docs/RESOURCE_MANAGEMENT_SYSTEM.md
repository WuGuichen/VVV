# Resource Management System

> 状态：M6C Implemented / M6D Deferred
> 日期：2026-05-10
> 负责人：Framework
> 目标：为 MxFramework 增加可测试、可替换、可诊断的资源管理系统，统一配置、Demo、Runtime Preview、Mod Package 和项目层资源加载入口。

## 1. 结论先行

资源管理系统采用“三层拆分”：

```text
MxFramework.Resources          noEngine contract, key, catalog, handle, provider abstraction
MxFramework.Resources.Unity    UnityEngine adapter, AssetBundle / Resources / StreamingAssets providers
MxFramework.Resources.Editor   catalog build, validation, reports, menu and debug panels
```

核心原则：

- 配置和 Gameplay 只保存 `ResourceKey`，不直接保存 Unity 路径、GUID 或 `UnityEngine.Object`。
- 常用资源类型必须通过 `ResourceTypeIds` 常量或泛型 `Load<T>` 表达，避免裸字符串拼写错误。
- Runtime 通过 `IResourceManager` 加载资源，具体来源由 Provider 决定。
- 资源定位走 Catalog，不允许业务代码拼路径加载。
- 缓存、引用计数、释放、依赖加载和错误报告由框架统一处理。
- Addressables 不作为第一阶段硬依赖；当前 `Packages/manifest.json` 未引入 Addressables，第一阶段以 AssetBundle / Resources / StreamingAssets 适配为主。
- `Resources` Provider 只服务 Demo、小样例和少量常驻资源；运行时主路径优先 AssetBundle Provider。
- 仓库不再保留长期提交的 `Assets/Resources` 目录；Showcase UI、配置资产、Preview profile 和测试资源已迁到 `Assets/UI`、`Assets/Config`、`Assets/Art`、`Assets/TestAssets`。`ResourcesProvider` 仍保留能力，但仅由显式 Catalog 或临时测试 fixture 使用。
- AssetBundle Provider 必须显式加载依赖 bundle，并按 bundle 与 asset 两级引用计数安全卸载。
- Mod Package 可以携带独立资源 Catalog，挂到同一 ResourceManager 中，冲突和覆盖必须显式。

## 2. 要解决的问题

当前框架已经有 Config、Gameplay、Preview 和 Mod Package 的运行时闭环，但资源仍缺少统一契约。后续一旦 Ability、Buff、Combat、UI 或外部编辑器开始引用图标、特效、音频、Prefab、动作片段，就会出现这些问题：

- 配置字段直接写 `Assets/...` 路径，运行时平台不可控。
- Demo、Preview、项目层各自加载资源，行为不一致。
- Mod Package 只能加载配置 patch，无法携带资源和依赖关系。
- 缺少统一释放策略，Prefab、Texture、AudioClip、AssetBundle 生命周期容易泄漏。
- Editor 能看到资源路径，但 Runtime 缺少可诊断的加载状态。

本系统的目标是把“资源引用”和“资源来源”解耦，让框架模块只依赖稳定的 key 和接口。

## 3. 非目标

第一阶段不做：

- 不强制接入 Addressables。
- 不做远程热更新、CDN、版本差分下载。
- 不做加密、签名、权限授权。
- 不做资源打包 UI 的完整产品化。
- 不让 `MxFramework.Gameplay`、`MxFramework.Buffs`、`MxFramework.Combat` 依赖 UnityEngine。
- 不把 WGame 真实业务资源放入框架 Demo。

这些能力应作为后续 Provider 或项目层扩展，而不是污染核心契约。

## 4. 模块边界

| 模块 | 类型 | 依赖 | 职责 |
| --- | --- | --- | --- |
| `MxFramework.Resources` | Runtime, noEngine | Core, Diagnostics 可选 | ResourceKey、Catalog、Provider、Handle、错误码、统计快照 |
| `MxFramework.Resources.Unity` | Runtime | Resources, Core.Unity, UnityEngine | Unity 资源适配、AssetBundle/Resources/StreamingAssets Provider、主线程约束 |
| `MxFramework.Resources.Editor` | Editor only | Resources, Resources.Unity, Config, UnityEditor | Catalog 构建、资源校验、报告导出、调试面板 |
| `MxFramework.Config.Runtime` | Runtime | Config, Resources 可选 | 配置字段到 `ResourceKey` 的桥接，不负责加载 Unity 对象 |
| `MxFramework.Preview.Runtime` | Runtime | Resources.Unity 可选 | Preview 会话中可通过外层组合加载图标、Prefab、VFX 等资源；默认 profile 不再走 `Resources.Load` |
| `MxFramework.Demo` | Runtime | Resources.Unity 可选 | 展示最小可运行资源加载闭环；Showcase UI / config 通过显式资产引用接入 |

依赖方向：

```text
Core
  <- Resources
      <- Resources.Unity
          <- Demo / Preview.Runtime / project composition root

Config
  <- Config.Runtime
      <- project composition root
```

`MxFramework.Resources` 不引用 UnityEngine；`UnityEngine.Object` 只允许出现在 `Resources.Unity` 及外层模块。

## 5. 核心概念

### 5.1 ResourceKey

`ResourceKey` 是配置、创作工具和运行时共同使用的稳定资源 ID。

建议字段：

| 字段 | 说明 |
| --- | --- |
| `Id` | 稳定资源 ID，例如 `demo.icon.fire_burst` |
| `TypeId` | 资源类型字符串，例如 `Texture2D`、`GameObject`、`AudioClip` |
| `Variant` | 可选变体，例如 locale、quality、platform |
| `PackageId` | 可选包 ID，用于 Mod Package 或 DLC |

约定：

- `Id` 必填，不能是空字符串。
- `Id` 使用小写命名空间，推荐 `domain.kind.name`。
- `Id` 只能使用小写字母、数字、`.`、`_` 和 `-`；Editor 构建 Catalog 时必须校验。
- 跨目录、跨包允许同名 `Id`，但全局加载时同层冲突必须失败；需要并存时通过 `PackageId` 精确路由。
- 配置表只能保存 `ResourceKey` 或能转换为 `ResourceKey` 的字段，不直接绑定加载路径。
- `PackageId` 为空时表示从当前 ResourceManager 的全局 Catalog 解析。
- `TypeId` 不允许业务代码散写。框架内置常量类，例如 `ResourceTypeIds.GameObject`、`ResourceTypeIds.Texture2D`、`ResourceTypeIds.AudioClip`；项目层可追加自己的类型常量。
- 调用方优先使用 `Load<T>` / `LoadAsync<T>` 获得编译期类型检查；`TypeId` 主要用于 Catalog、Config 和 Editor 校验。
- `Variant` 采用 `locale_platform_quality` 格式，缺省段可省略，例如 `zh_CN_android_hd`、`en_US_hd`、`android_low`。Catalog 构建器可以从目录结构或 import preset 生成多条 variant entry。

建议内置类型常量：

```csharp
public static class ResourceTypeIds
{
    public const string GameObject = "GameObject";
    public const string Texture2D = "Texture2D";
    public const string Sprite = "Sprite";
    public const string AudioClip = "AudioClip";
    public const string TextAsset = "TextAsset";
}
```

### 5.2 ResourceCatalog

Catalog 是从 key 到实际资源位置的索引。

最小 JSON 结构：

```json
{
  "schemaVersion": 1,
  "catalogId": "mxframework.demo",
  "packageId": "mxframework.demo",
  "entries": [
    {
      "id": "demo.icon.fire_burst",
      "type": "Texture2D",
      "variant": "",
      "packageId": "mxframework.demo",
      "provider": "resources",
      "address": "MxFramework/Demo/Icons/fire_burst",
      "labels": ["demo", "icon"],
      "dependencies": [],
      "hash": "",
      "size": 0,
      "allowOverride": false,
      "providerData": {}
    }
  ]
}
```

Catalog 校验规则：

- `schemaVersion` 必须为 `1`。
- 同一 Catalog 内 `id + type + variant` 必须唯一。
- `provider` 必须在 ResourceManager 注册过。
- `address` 不能是绝对路径，不能包含 `..`。
- Editor 构建和校验阶段必须验证 `address` 对应资源存在；Unity 项目内资源用 `AssetDatabase`，外部包资源用文件系统或 Provider 专用校验器。
- `type` 必须与实际主资源类型一致。Unity Editor 校验优先使用 `AssetDatabase.GetMainAssetTypeAtPath`；AssetBundle 校验使用 bundle manifest / build report。
- `dependencies` 必须记录依赖的 `id`、`type`、`variant` 和 `packageId`，只允许引用同一 Catalog 或显式声明过的外部 package。
- 依赖图不能有循环；未知 Provider、缺失依赖、循环依赖必须在 Catalog 合并或初始化阶段失败，不能延迟到第一次加载。
- `hash` 第一阶段可为空；Bundle / 远程 Provider 接入后必须写入。
- `size` 第一阶段可为 `0`；Editor report 应尽量填充，便于后续统计包体。
- `providerData` 为可选 provider 专用扩展字典，缺省等价于空字典；核心只保存和传递，不解释其业务含义。
- 同层重复 `id + type + variant` 必须失败。
- 高层覆盖低层必须显式声明 `allowOverride: true`，且覆盖前后 `type` 必须一致；被覆盖 entry 仍可通过原 `PackageId` 精确访问。

依赖 entry 建议结构：

```json
{
  "id": "demo.vfx.hit_spark",
  "type": "GameObject",
  "variant": "",
  "packageId": "mxframework.demo"
}
```

### 5.3 ResourceProvider

Provider 负责把 Catalog entry 转成真实资源。

第一阶段内置 Provider：

| Provider | 所在模块 | 用途 |
| --- | --- | --- |
| `memory` | Resources | EditMode 测试和无 Unity 测试 |
| `resources` | Resources.Unity | 框架 Demo 和小型样例，不作为大项目主路径 |
| `assetBundle` | Resources.Unity | 运行时主目标，支持包内资源和依赖 |
| `streamingFile` | Resources.Unity | 读取 catalog、manifest、文本和二进制文件 |

后续可选 Provider：

- `addressables`：项目层或可选程序集实现，不进入第一阶段硬依赖。
- `remoteBundle`：带下载、缓存、hash 校验和版本策略。
- `encryptedBundle`：项目层安全需求。

Provider 实现约束：

- `memory`：用字典模拟 Catalog entry 到对象的映射，测试引用计数、重复加载合并和释放幂等；释放到 0 后移除测试缓存中的 loaded record。
- `resources`：只允许显式 Catalog、临时测试 fixture 或项目层极小样例使用。Unity 官方文档说明，大量内容放入 `Resources` 会增加启动时间和构建长度，也会让细粒度内存管理更困难；框架文档和 Editor 校验必须提示该限制。MxFramework 仓库本身不再提交长期 `Assets/Resources` 资源。
- `assetBundle`：运行时推荐主路径。加载 asset 前必须先根据 `AssetBundleManifest.GetAllDependencies` 或构建期导出的依赖表加载所有依赖 bundle；每个 bundle 和每个 asset 分别维护引用计数；bundle ref-count 归零后由 Provider 调用 `AssetBundle.Unload(true)` 彻底卸载。
- `assetBundle`：禁止卸载仍被已加载 asset 或其他 bundle 引用的依赖 bundle；依赖递减必须晚于依赖者 release。
- `assetBundle`：manifest 依赖查询会分配数组，应在初始化或预热阶段缓存结果，不在高频加载路径反复调用。
- `streamingFile`：所有路径从 `Application.streamingAssetsPath` 解析，不硬编码平台路径；Android 和 WebGL 等平台不能假设可用同步 `System.IO.File`，Provider 需要走 `UnityWebRequest` 或平台专用读取流程。

### 5.4 ResourceHandle

加载结果必须以 handle 表达，禁止散落 `Object` 引用。

建议状态：

```text
None -> Loading -> Loaded
              -> Failed
Loaded -> Released
```

Handle 约定：

- 加载成功后引用计数 +1。
- `Release(handle)` 必须幂等；重复释放记录 warning，不崩溃。
- 资源依赖由 ResourceManager 统一持有，父资源释放后递减依赖计数。
- 失败时 handle 保留 `ResourceError`，调用方可以上报到 Demo UI / Preview log / Diagnostics。
- 同一 key 并发加载应合并为一个底层 operation。
- handle 内部必须绑定 resolved catalog entry、provider、状态和版本号，避免 Catalog 热切换后释放到错误 Provider。
- `GameObject` 实例生命周期不由资源系统接管。调用方负责 `Object.Destroy(instance)`；资源系统只管理 prefab asset 的 handle，销毁实例不等于释放 prefab 资源。

## 6. 运行时流程

### 6.1 初始化

```text
Composition Root
  -> create ResourceManager
  -> register providers
  -> load base catalog
  -> load optional package catalogs
  -> validate merged catalog
  -> expose IResourceManager to Gameplay adapter / Preview / UI
```

Composition Root 可以是：

- Demo runner。
- Preview server bootstrap。
- 游戏项目启动器。
- EditMode 测试 fixture。

框架核心模块不自己创建全局单例。项目层可以选择把 `IResourceManager` 放进自己的服务容器。

初始化必须完成这些失败前置检查：

- 所有 Catalog 的 `schemaVersion` 合法。
- 所有 entry 的 Provider 已注册。
- 同层 key 冲突已报错。
- 高层覆盖都带有 `allowOverride: true` 且类型一致。
- 依赖存在且无循环。
- AssetBundle Provider 的依赖 bundle 已能在 manifest 或构建报告中解析。

### 6.2 加载

```text
ResourceKey
  -> Catalog resolve
  -> Provider select
  -> dependency preload
  -> provider load
  -> cache register
  -> ResourceHandle<T>
```

错误必须在最早阶段暴露：

- Catalog 找不到 key：`NotFound`
- 类型不匹配：`TypeMismatch`
- Provider 未注册：`ProviderMissing`
- Provider 加载失败：`ProviderFailed`
- 依赖缺失或循环：`DependencyInvalid`
- 资源已释放后访问：`HandleReleased`

### 6.3 释放

```text
Release handle
  -> decrement entry ref count
  -> if zero, release asset instance if owned
  -> decrement dependency ref counts
  -> provider unload when bundle ref count reaches zero
  -> update diagnostics
```

释放策略默认保守：

- 非实例化资源使用引用计数。
- Prefab 实例由调用方销毁；资源 handle 只管理 prefab asset。
- AssetBundle 在所有 asset 和依赖 ref count 为 0 后卸载。
- 可配置 `RetainPolicy` 只作为后续性能优化，不进入 M1。

推荐释放顺序：

```text
Release asset handle
  -> if handle already Released, warn and return
  -> decrement asset ref count
  -> if asset ref count > 0, return
  -> provider release asset record
  -> decrement dependency asset refs
  -> decrement owning bundle ref
  -> if bundle ref count == 0, AssetBundle.Unload(true)
  -> update diagnostics snapshot counters
```

## 7. API 草案

### 7.1 noEngine 层

```csharp
public readonly struct ResourceKey
{
    public string Id { get; }
    public string TypeId { get; }
    public string Variant { get; }
    public string PackageId { get; }
}

public interface IResourceManager
{
    bool Contains(ResourceKey key);
    ResourceLoadResult<T> Load<T>(ResourceKey key);
    IResourceOperation<T> LoadAsync<T>(ResourceKey key, CancellationToken cancellationToken = default);
    void Release<T>(ResourceHandle<T> handle);
    ResourceDebugSnapshot CreateDebugSnapshot();
}

public interface IResourceProvider
{
    string ProviderId { get; }
    bool CanLoad(ResourceCatalogEntry entry);
    ResourceLoadResult<object> Load(ResourceLoadContext context);
    IResourceOperation<object> LoadAsync(ResourceLoadContext context, CancellationToken cancellationToken);
    void Release(ResourceReleaseContext context);
}
```

说明：

- `Resources` 层只定义契约和测试 Provider。
- 泛型 `T` 不要求是 Unity 类型；测试中可以加载普通对象。
- Unity Provider 在 `Resources.Unity` 中把 `T` 约束和 `UnityEngine.Object` 适配起来。
- `ResourceLoadResult<T>` 必须带 `ResourceErrorCode`、message、key、providerId 和 resolved catalog entry 摘要。
- `IResourceOperation<T>` 必须暴露状态、错误、进度、取消入口和完成回调；取消后不得再把 Unity 对象交给调用方。

### 7.2 Unity 层

```csharp
public sealed class UnityResourceManagerBuilder
{
    public UnityResourceManagerBuilder AddResourcesProvider();
    public UnityResourceManagerBuilder AddAssetBundleProvider(string bundleRootPath);
    public UnityResourceManagerBuilder AddCatalog(ResourceCatalog catalog);
    public IResourceManager Build();
}
```

Unity 层约定：

- 所有 `UnityEngine.Object` 加载和释放在主线程执行。
- `Resources.LoadAsync`、`AssetBundle.LoadAssetAsync` 通过统一 operation 暴露状态。
- `UnityWebRequest`、`AssetBundleRequest`、`Resources.LoadAsync` 的异常和失败必须转换为 `ResourceError`，不能只写 Console。
- 不把 Unity 异步对象泄漏到 noEngine API。
- 不在 Runtime 程序集中引用 `UnityEditor.AssetDatabase`。
- worker 线程不能访问 `UnityEngine.Object`。后台线程只允许做 JSON 解析、hash 校验、路径计算等纯数据工作；最终 Unity API 调用回到主线程。

## 8. 与现有系统的关系

### 8.1 Config

Config 继续负责数据结构和引用校验，资源系统负责资源定位和加载。

迁移方向：

```text
旧字段：IconPath = "Assets/Demo/Icons/fire_burst.png"
新字段：Icon = ResourceKey("demo.icon.fire_burst", "Texture2D")
```

过渡期允许 Editor 导入器把 legacy path 转成 ResourceKey，但 Runtime 不应直接消费 legacy path。

### 8.2 Gameplay / Buffs / Combat

这些模块保持纯 C#。资源需求只通过外层 adapter 注入：

- Ability 需要 VFX：Ability runtime event 发出 `ResourceKey`，Unity adapter 决定加载和实例化。
- Buff 需要图标：UI 层按 Buff definition 中的 `ResourceKey` 加载 Texture。
- Combat 需要 hit effect：Combat event 输出 key，不直接加载 GameObject。

### 8.3 Preview

Preview 侧使用同一 ResourceManager：

- Patch / Mod Package 提供配置。
- Resource Catalog 提供图标、Prefab、VFX。
- Preview log 展示资源加载错误、耗时、来源包和 provider。

### 8.4 Mod Package

Mod Package 扩展结构：

```text
mod.json
runtime/runtime_config_patch.json
resources/catalog.json
resources/bundles/...
```

`mod.json` 增加可选字段：

```json
{
  "resourceCatalog": "resources/catalog.json"
}
```

加载顺序：

```text
Base Catalog
  -> Patch Catalog
  -> Mod Catalog
  -> Debug Catalog
```

覆盖规则：

- 同层重复 key 默认失败。
- 高层覆盖低层必须显式声明 `allowOverride: true`。
- 覆盖时类型必须一致。
- 被覆盖资源仍可通过 `PackageId` 精确引用。
- `ResourceKey.PackageId` 非空时优先在指定包 Catalog 中解析，不参与全局覆盖结果。
- `ResourceKey.PackageId` 为空时按合并后的全局 Catalog 解析，遵守 Base -> Patch -> Mod -> Debug 的覆盖顺序。
- ResourceManager 必须提供查询 API，能列出当前 Catalog、每个 entry 的来源 package、是否覆盖了其他 entry，以及被覆盖 entry 的数量。

## 9. Diagnostics

资源系统必须输出可观测快照，供 Editor、Preview 和测试读取。

`ResourceDebugSnapshot` 至少包含：

| 字段 | 说明 |
| --- | --- |
| `CatalogCount` | 已加载 Catalog 数量 |
| `EntryCount` | 可解析资源数量 |
| `ProviderCount` | 已注册 Provider 数量 |
| `LoadedCount` | 当前已加载资源数量 |
| `LoadingCount` | 当前异步加载数量 |
| `FailedCount` | 失败次数 |
| `BundleCount` | 已加载 AssetBundle 数量 |
| `TotalRefCount` | 资源引用计数总和 |
| `RecentErrors` | 最近错误列表，包含 key、provider、message |
| `Catalogs` | 当前已挂载 Catalog 摘要，包含 catalogId、packageId、entryCount |
| `EntryOrigins` | 可选 entry 来源摘要，便于定位 key 来自哪个包 |

Diagnostics 不暴露私有缓存对象，只暴露结构化摘要。

`RecentErrors` 必须包含：

- `ResourceKey`
- `ProviderId`
- `ResourceErrorCode`
- `Message`
- `PackageId`
- `Address`

## 10. Editor 工具

Editor 第一阶段只做辅助，不成为 Runtime 必需条件。

必须提供：

- 从指定目录或配置源生成 Catalog。
- 校验 Catalog key 唯一性、路径安全、provider 可用性、类型匹配。
- 报告缺失资源、孤儿资源、未使用资源和重复引用。
- 导出 JSON report，便于 CI 或 Agent 读取。

实现细节：

- Unity 项目资源扫描使用 `AssetDatabase`；类型推断优先 `AssetDatabase.GetMainAssetTypeAtPath`。
- 若项目后续引入 Addressables，可在 Editor 层增加可选 scanner 读取 `AddressableAssetSettings`，但不能让 `MxFramework.Resources` 硬依赖 Addressables。
- AssetBundle Provider 的 Editor 校验必须确认目标 asset、bundle 名、依赖 bundle 都存在于构建结果或 manifest 中。
- 依赖树 report 应记录 packageId、bundle、entry id、type、size、hash 和 labels，便于分包审查。
- 孤儿资源指扫描目录中存在但未进入 Catalog 的资源；未引用 entry 指 Catalog 中存在但配置、Demo 或测试入口未引用的资源。两类问题默认 warning，由项目层决定是否提升为 error。

不在第一阶段做：

- 完整打包 UI。
- 远程资源发布。
- 可视化依赖图编辑器。

## 11. 阶段切片

### M1：Contract + Memory Provider

状态：已实现，详见 `Docs/Tasks/RESOURCE_MANAGEMENT_M1_CONTRACT_MEMORY_PROVIDER.md`。

目标：在 noEngine 层跑通 ResourceKey、Catalog、Manager、Handle、Memory Provider。

完成定义：

- 新增 `MxFramework.Resources` asmdef，保持 `noEngineReferences=true`。
- EditMode 测试覆盖 key resolve、类型不匹配、重复 key、引用计数、释放幂等、依赖缺失。
- 无 UnityEngine 依赖。

### M2：Unity Demo Provider

状态：已实现，详见 `Docs/Tasks/RESOURCE_MANAGEMENT_M2_UNITY_DEMO_PROVIDER.md`。

目标：用 `Resources` provider 跑通 Unity 最小加载。

完成定义：

- 新增 `MxFramework.Resources.Unity`。
- Demo catalog 加载一张 Texture 或一个 Prefab。
- RuntimeVerticalSlice 或独立 Demo runner 显示加载成功和失败日志。
- Unity console 无 error。

### M3：AssetBundle Provider + Catalog 文件

状态：已实现，详见 `Docs/Tasks/RESOURCE_MANAGEMENT_M3_ASSETBUNDLE_PROVIDER_CATALOG_FILE.md`。

目标：建立可部署的 runtime 资源入口。

完成定义：

- 支持从 StreamingAssets 读取 catalog。
- 支持 AssetBundle 加载 asset 和依赖 bundle。
- 支持 hash 字段预留和基础校验。
- 测试覆盖 bundle 缺失、asset 缺失、依赖 bundle 缺失、重复加载合并、依赖者未释放时不卸载依赖 bundle、ref-count 归零后调用 unload。

### M4：Mod Package Resource Catalog

状态：已实现，详见 `Docs/Tasks/RESOURCE_MANAGEMENT_M4_MOD_PACKAGE_RESOURCE_CATALOG.md`。

目标：让 Mod Package 同时携带配置 patch 和资源 catalog。

完成定义：

- `RuntimeModPackageLoader` 读取可选 `resourceCatalog` 字段，并校验包内相对路径。
- 旧包不声明 `resourceCatalog` 仍保持兼容。
- package catalog 可通过 `StreamingResourceCatalogLoader` 挂载到 ResourceManager。
- 支持同层冲突失败、高层显式 `allowOverride` 覆盖、覆盖类型一致性和 `PackageId` 精确路由。
- LoadPlan 禁用包不挂载资源 Catalog。

### M5：Diagnostics + Editor Validation

状态：已实现，详见 `Docs/Tasks/RESOURCE_MANAGEMENT_M5_DIAGNOSTICS_EDITOR_VALIDATION.md`。

目标：资源状态可诊断、可报告。

完成定义：

- `ResourceDebugSource` 将 `ResourceDebugSnapshot` 转成通用 `FrameworkDebugSnapshot`。
- `ResourceCatalogValidator` 校验 key、provider、address、重复项、依赖缺失和依赖环。
- `ResourceCatalogEditorValidator` 使用 `AssetDatabase` 校验 `resources` / `assetBundle` 入口资源存在和类型。
- 文档补齐 `Docs/Interfaces/Resources.md`、`Docs/Interfaces/Diagnostics.md`、`Docs/USAGE.md` 和 `Docs/CAPABILITIES.md`。

### M6：可选 Provider 与策略扩展

状态：M6.0 / M6A / M6B / M6C 已实现，M6D Addressables Adapter 后置为 Deferred / Optional。

M6 不做成“大杂烩阶段”。后续能力按当前框架收益、公共契约稳定性、实现风险和对 M1-M5 的侵入程度排序：

| 阶段 | 任务 | 优先级 | 类型 | 结论 |
| --- | --- | --- | --- | --- |
| M6.0 | `Docs/Tasks/RESOURCE_MANAGEMENT_M6_0_CATALOG_SCHEMA_PREP.md` | P0 | Schema prep | 已补 `variant` 示例和 `providerData` 扩展字典，避免后续 Provider 反复改 Catalog 契约 |
| M6A | `Docs/Tasks/RESOURCE_MANAGEMENT_M6A_PRELOAD_GROUP_WARMUP.md` | P0 | 策略 | 已实现 Preload Group + Scene Warmup，支持 explicit keys、labels、FailFast、取消和幂等 group release |
| M6B | `Docs/Tasks/RESOURCE_MANAGEMENT_M6B_VARIANT_AND_RETAIN_POLICY.md` | P1 | 解析 / 释放策略 | 已实现显式 Variant Profile 和 RetainPolicy，支持 fallback、Timed/KeepAlive retain、手动 eviction 和 retained diagnostics |
| M6C | `Docs/Tasks/RESOURCE_MANAGEMENT_M6C_REMOTE_BUNDLE_PROVIDER.md` | P2 | Provider | 已实现第一段 RemoteBundle Provider，支持 file/local HTTP 下载到 cache、SHA-256 校验、缓存命中和结构化错误 |
| M6D | `Docs/Tasks/RESOURCE_MANAGEMENT_M6D_ADDRESSABLES_PROVIDER.md` | P3 / Optional | Provider | Deferred；仅在项目已安装并决定使用 Addressables 时，以独立 asmdef 接入 |
| Closeout | `Docs/Tasks/RESOURCE_MANAGEMENT_M6_CLOSEOUT.md` | P0 | 决策 | 已收口 M6 默认路线，不引入 Addressables 硬依赖 |

关键决策：

- PreloadGroup 不是 Provider，它是加载调度策略。
- Variant 解析不写进 Provider；Provider 只加载已解析出的 entry。
- RetainPolicy 不改变 handle 语义，只影响底层 loaded record 的卸载时机。
- RemoteBundle 后置，避免过早引入网络、缓存、hash、版本、重试、断点续传和安全问题。
- Addressables Adapter 必须独立为可选程序集，不能让 `MxFramework.Resources.Unity` 硬依赖 Addressables package。
- 当前项目未安装 Addressables；默认路线使用 Catalog / AssetBundle / RemoteBundle / Preload / Variant / Retain / Diagnostics。
- M6 不修改 M1-M5 的 `IResourceManager` / `IResourceProvider` 公共契约。

## 12. 验收标准

资源系统进入主干前，必须满足：

- `MxFramework.Resources` 不依赖 UnityEngine / UnityEditor。
- Runtime 程序集不引用 Editor API。
- 所有 public API 有接口文档或使用文档入口。
- 错误路径不静默 fallback，调用方能拿到明确错误码和 message。
- 重复加载、释放、缺失资源、类型不匹配都有测试。
- Demo 资源为框架级虚构数据，不包含 WGame 真实业务资源。
- Mod Package 资源路径必须做路径穿越防护。
- Diagnostics 能看到已加载资源、引用计数和最近错误。
- `Resources` Provider 的使用范围在文档和 Editor report 中明确标注，不允许被误当作大项目主路径。
- AssetBundle Provider 的依赖加载、bundle 引用计数和卸载行为有单元测试或 PlayMode 测试覆盖。
- 初始化阶段能拦截未知 Provider、重复 key、非法覆盖声明、缺失依赖和循环依赖。

## 13. 风险和决策

| 风险 | 决策 |
| --- | --- |
| 过早绑定 Addressables | 第一阶段不依赖 Addressables，只预留 Provider 扩展点 |
| `ResourceKey` 设计过重 | M1 只保留 Id、TypeId、Variant、PackageId，后续不够再扩展 |
| 资源释放语义不一致 | 所有加载必须返回 handle，释放入口统一 |
| Mod 覆盖资源导致不可追踪 | Catalog entry 保留 packageId，覆盖必须显式 |
| Config 继续散落路径字段 | 新配置使用 ResourceKey，legacy path 只在 Editor/importer 层转换 |
| 异步 API 与 Unity 主线程冲突 | noEngine 只暴露 operation 状态，Unity provider 负责主线程执行 |
| AssetBundle 依赖管理复杂 | M3 先做本地 manifest 依赖表和严格 ref-count，不引入远程更新 |
| 频繁加载卸载造成 asset churn | M1-M5 默认严格引用计数；后续通过 preload group 和 RetainPolicy 减少抖动 |
| Remote bundle 带来网络和缓存复杂度 | 作为 M6 Provider 扩展，单独设计下载、hash、版本、断点续传和加密策略 |

## 14. 下一步

当前 M6A / M6B 已完成并验收：`ResourcePreloadService` 支持 explicit keys、labels、FailFast、取消和幂等 group release；M6B 已补齐 VariantProfile、RetainPolicy，并修正 label warmup 语义，使同一资源的多个 variant 共用 label 时只请求 1 个逻辑 key，实际 entry 由当前 VariantProfile 解析。最新验证：`MxFramework.Tests.Resources.ResourcePreloadServiceTests` 6/6 passed，`MxFramework.Tests.Resources` 41/41 passed，Unity Console error 0。

下一步建议分两条推进：

```text
Docs/Tasks/RESOURCE_MANAGEMENT_RUNTIME_RESOURCE_MIGRATION_01.md
Docs/Tasks/RESOURCE_MANAGEMENT_M7_RUNTIME_ASSET_CATALOG.md
```

- Runtime Resource Migration 01 已把业务/Demo/Preview 侧 `Resources.Load` 迁走；后续可以给 `RuntimeVerticalSlice` 增加一个真实 warmup plan：在场景启动时预加载 HUD、常用图标、调试材质或未来 prefab，并在退出场景时释放 group。
- M7 建议补一个真实运行时资源 catalog 生成 / 绑定任务，把 `Assets/UI`、`Assets/Art`、`Assets/Config` 中适合运行时加载的资源纳入 Catalog，并用 M6A/M6B 的 warmup + variant + retain 策略驱动 Demo 场景。
- M6D Addressables Adapter 保持 Deferred / Optional。只有项目安装 Addressables 并决定使用 Addressables Groups / Remote Catalog / Content Update 时，才新增独立 `MxFramework.Resources.Addressables` 程序集。

## 15. 参考资料

- Unity Manual: [Loading Resources at Runtime](https://docs.unity.cn/2023.2/Documentation/Manual/LoadingResourcesatRuntime.html)
- Unity Manual: [AssetBundle Dependencies](https://docs.unity.cn/Manual/AssetBundles-Dependencies.html)
- Unity Scripting API: [Application.streamingAssetsPath](https://docs.unity3d.com/ScriptReference/Application-streamingAssetsPath.html)
- Unity Addressables Manual: [Memory management overview](https://docs.unity.cn/Packages/com.unity.addressables%402.2/manual/MemoryManagement.html)
- Unity Learn: [Assets, Resources and AssetBundles best practices](https://learn.unity.com/topics/best-practices/guide-assetbundles-and-resources)
