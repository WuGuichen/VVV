# Resource Management M3：AssetBundle Provider + Catalog File

> 状态：已实现（2026-05-10）
> 优先级：P0
> 前置任务：`RESOURCE_MANAGEMENT_M2_UNITY_DEMO_PROVIDER.md`

## 目标

在 M1/M2 的资源管理契约上补齐本地 AssetBundle 主路径和 StreamingAssets Catalog 文件读取：

```text
Assets/StreamingAssets/**/catalog.json
  -> StreamingResourceCatalogLoader
  -> ResourceCatalog
  -> ResourceManager
  -> AssetBundleProvider
  -> AssetBundle.LoadFromFile
  -> ResourceHandle<T>
  -> Release / AssetBundle.Unload(true)
```

M3 固定本地 bundle、asset、dependency bundle 的引用计数和失败回滚语义。远程 bundle、Mod Package resourceCatalog、Addressables 和 Editor Catalog 构建器都不进入本阶段。

## 已实现范围

- 新增 `StreamingResourceCatalogLoader`，支持从 `Application.streamingAssetsPath` 或指定文件读取 JSON Catalog。
- 新增 `AssetBundleProvider`，ProviderId 固定为 `assetBundle`。
- 新增 `IAssetBundleDependencyProvider`。
- 新增 `AssetBundleManifestDependencyProvider`，从 `AssetBundleManifest.GetAllDependencies` 获取依赖并缓存。
- 新增 `DictionaryAssetBundleDependencyProvider`，用于测试依赖 bundle 语义。
- 新增 M3 StreamingAssets fixture：`Assets/StreamingAssets/MxFramework/ResourcesDemo/catalog_m3.json`。
- 新增 `AssetBundleProviderTests` 和 `StreamingResourceCatalogLoaderTests`。

## Catalog JSON 格式

M3 读取 Catalog JSON，字段名必须与 runtime DTO 保持一致：

```json
{
  "schemaVersion": 1,
  "catalogId": "mxframework.resources.m3.demo",
  "packageId": "mxframework.demo",
  "entries": [
    {
      "id": "demo.text.bundle_main",
      "type": "TextAsset",
      "variant": "",
      "packageId": "",
      "provider": "assetBundle",
      "address": "m3-main|Assets/TestAssets/MxFramework/ResourcesDemo/resource_demo_text.txt",
      "labels": ["demo", "m3"],
      "dependencies": [],
      "hash": "",
      "size": 0,
      "allowOverride": false,
      "providerData": {}
    }
  ]
}
```

约定：

- `schemaVersion` 必须为 `1`。
- `entries[].provider` 在 M3 中为 `assetBundle`。
- `entries[].address` 使用 `bundleName|assetName`。
- `entries[].allowOverride` 对应代码里的 `ResourceCatalogEntry.AllowOverride`。
- `packageId` 为空时继承 catalog 的 `packageId`。
- `dependencies` 表达资源级依赖；bundle 依赖由 `IAssetBundleDependencyProvider` / manifest 提供。

## AssetBundleProvider 行为契约

- 加载 asset 前先加载依赖 bundle。
- 每个 bundle 维护 Provider 内部 ref-count。
- `ResourceManager` 维护 asset handle ref-count。
- 同一资源重复加载只增加 `ResourceManager` asset ref-count，不重复调用 Provider 加载。
- release 到 0 时先释放 owner bundle，再按反向顺序释放 dependency bundle。
- bundle ref-count 归零时调用 `AssetBundle.Unload(true)`。
- bundle 缺失返回 `ResourceErrorCode.NotFound`。
- asset 缺失返回 `ResourceErrorCode.NotFound`，并回滚已加载 bundle。
- dependency bundle 缺失返回 `ResourceErrorCode.DependencyInvalid`，并回滚已加载 dependency bundle。
- 非法 address 不能触碰文件系统。

## 测试

新增测试入口：

```text
Assets/Scripts/MxFramework/Tests/Resources/AssetBundleProviderTests.cs
Assets/Scripts/MxFramework/Tests/Resources/StreamingResourceCatalogLoaderTests.cs
```

覆盖：

- 从 bundle 加载 `TextAsset`，释放后 bundle 卸载。
- asset 缺失失败并卸载已加载主 bundle。
- 主 bundle 缺失失败。
- dependency bundle 加载和 owner release 后一起卸载。
- dependency bundle 缺失失败并回滚前置依赖。
- 同一 key 重复加载保持 bundle 到最后一个 handle release。
- asset 缺失且已有 dependency bundle 时，失败路径卸载 dependency bundle。
- 非法 address 解析失败。
- StreamingAssets Catalog happy path。
- JSON 解析 labels、dependencies、hash、size、allowOverride 和 package fallback。
- unsupported schema 抛 `ResourceCatalogException`。

## 验证记录

已执行：

```text
Unity refresh + compile
Unity EditMode: MxFramework.Tests.Resources.AssetBundleProviderTests
Unity EditMode: MxFramework.Tests.Resources.StreamingResourceCatalogLoaderTests
Unity EditMode: MxFramework.Tests.Resources
```

结果：

```text
AssetBundleProviderTests: 8/8 passed
StreamingResourceCatalogLoaderTests: 3/3 passed
Resources test group: 20/20 passed
Unity Console errors: 0
```

## 不做

- 不做 Mod Package 的 `resourceCatalog` 字段接入。
- 不做 Editor Catalog 构建器、扫描器和报告 UI。
- 不做 Addressables Provider。
- 不做远程 bundle、CDN、下载缓存、版本差分或断点续传。
- 不做加密、签名、权限授权。
- 不做 platform / locale / quality variant fallback 策略。
- 不做 RetainPolicy、preload group 和场景切换 warmup。
- 不做完整异步调度器。
- 不导入 WGame 真实业务资源。

## 下一步

进入 M4：

```text
Resource Management M4：Mod Package Resource Catalog
```

目标是让 `RuntimeModPackageLoader` 读取可选 `resourceCatalog` 字段，把 Mod Package 的资源 Catalog 挂入同一个 ResourceManager，并验证 Base / Patch / Mod / Debug 覆盖规则。
