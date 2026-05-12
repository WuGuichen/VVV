# Resource Management M6C: Remote Bundle Provider

> 状态：Implemented
> 日期：2026-05-10
> 优先级：P2
> 前置任务：Resource Management M6A / M6B

## 结论

Remote Bundle Provider 已按第一段最小切片实现。

它仍不是资源系统主路径；只有项目明确需要热更新、DLC、外部 Mod 资源分发或 UGC 下载时才应接入。

原因是 Remote bundle 会引出网络、缓存、hash、版本、失败重试、断点续传、安全校验和下载进度等问题。M6C 只完成可测试的最小闭环，不做发布管线和复杂网络策略。

## 目标

在已有 AssetBundle Provider 语义基础上，支持从远程或本地 file URL 下载 bundle，并使用现有 `ResourceManager` handle / ref-count 管线管理 asset 生命周期。

## Provider 设计

```csharp
public sealed class RemoteBundleProvider : IResourceProvider
{
    public string ProviderId => "remoteBundle";
}
```

实际构造：

```csharp
public RemoteBundleProvider(
    string cacheRootPath,
    IAssetBundleDependencyProvider dependencyProvider = null)
```

Provider 复用 `AssetBundleProvider` 的 `bundleName|assetName` 地址格式、bundle ref-count 和 `AssetBundle.Unload(true)` 释放语义。

Catalog entry 示例：

```json
{
  "id": "mod.character.prefab",
  "type": "GameObject",
  "variant": "",
  "provider": "remoteBundle",
  "address": "characters|Assets/Mods/Characters/character.prefab",
  "hash": "sha256:...",
  "size": 123456,
  "dependencies": [],
  "providerData": {
    "url": "https://cdn.example.com/mods/characters.bundle",
    "bundleName": "characters",
    "cacheKey": "mod.characters.v1"
  }
}
```

## 必须先决策

- cache root 目录：由 `RemoteBundleProvider(cacheRootPath)` 调用方传入。
- hash 算法：M6C 第一版支持 `sha256:<hex>` 或裸 hex。
- cache key 格式：`providerData.cacheKey`，缺省使用 bundleName，并做文件名安全化。
- 下载失败重试策略：未实现，失败返回结构化 `ResourceError`。
- 离线模式行为：缓存命中可离线加载；缓存缺失且 source 不存在返回 `NotFound`。
- 版本不匹配行为：hash mismatch 会删除缓存并返回 `ProviderFailed`。
- 依赖 bundle 下载顺序：复用 `IAssetBundleDependencyProvider` 顺序；依赖 URL 可通过 `url.<bundleName>`、`dependency.<bundleName>.url` 或 `baseUrl` 提供。
- 下载中取消行为：`LoadAsync` 当前保持 immediate 包装，只在调用前取消；下载中取消留到后续异步 operation。

## 分阶段实现建议

第一段：已实现

- 仅支持 `file://` 或本地 HTTP 测试服务器。
- 支持 hash 校验。
- 支持缓存命中。
- 支持结构化错误。
- 支持 `providerData.url`、`bundleName`、`cacheKey`。
- 支持缓存命中后源文件缺失仍可加载。

第二段：

- 支持 HTTP(S)。
- 支持重试。
- 支持进度。
- 支持缓存清理。

第三段：

- 支持版本策略、断点续传、签名或加密。

## 不做范围

- 不替代本地 AssetBundle Provider。
- 不做 Addressables remote catalog。
- 不做 CDN 发布工具。
- 不做权限授权。
- 不做加密签名，除非项目明确需要。
- 不做断点续传、后台下载队列或复杂进度聚合。

## 测试

已覆盖：

- `file://` URL 复制到 cache 后加载 TextAsset。
- 缓存命中时不再次 fetch，源 bundle 删除后仍可加载。
- SHA-256 mismatch 返回 `ProviderFailed` 并删除无效缓存。
- 源 bundle 缺失返回 `NotFound`。

验证记录：

```text
dotnet build MxFramework.Tests.csproj --no-restore
Unity EditMode: MxFramework.Tests.Resources.RemoteBundleProviderTests, 4/4 passed
Unity EditMode: MxFramework.Tests.Resources, 40/40 passed
```
