# Resource Management M4: Mod Package Resource Catalog

> 状态：Implemented
> 日期：2026-05-10
> 优先级：P0
> 前置任务：Resource Management M1-M3、Mod Package 02-05

## 目标

让 Mod Package 在已有 runtime config patch 之外，可以声明一个可选资源 Catalog，并由外层组合入口挂到同一个 `ResourceManager`。

M4 只打通包声明、路径安全校验、Catalog 挂载和覆盖规则验证，不实现远程资源、Addressables、Editor 构建器或完整 Demo UI。

## 实现范围

- `RuntimeModPackageManifest` 新增可选 `resourceCatalog` 字段。
- `RuntimeModPackageLoader` 解析并校验 `resourceCatalog`：
  - 空字段保持旧包兼容。
  - 必须是包根内相对路径。
  - 拒绝绝对路径和 `..` 逃逸。
  - 声明了文件但文件不存在时包加载失败。
- `RuntimeModPackageLoadResult` 暴露：
  - `ResourceCatalogFilePath`
  - `HasResourceCatalog`
- `Config.Runtime` 只暴露路径，不依赖 `MxFramework.Resources` 或 `MxFramework.Resources.Unity`，保持 noEngine 边界。
- 资源 Catalog 挂载仍由 composition root、Preview、Demo 或测试通过 `StreamingResourceCatalogLoader.LoadFromFile` 和 `ResourceManager.AddCatalog` 完成。

## Manifest 约定

```json
{
  "schemaVersion": 1,
  "packageId": "pkg.resources",
  "kind": "Preview",
  "runtimePatch": "runtime/patch.json",
  "resourceCatalog": "resources/catalog.json"
}
```

`resourceCatalog` 可省略。省略时 `HasResourceCatalog == false`，旧 Mod Package 不需要迁移。

## Catalog 约定

资源 Catalog 沿用 M3 JSON 格式：

```json
{
  "schemaVersion": 1,
  "catalogId": "pkg.resources.catalog",
  "packageId": "pkg.resources",
  "entries": [
    {
      "id": "demo.text.title",
      "type": "String",
      "variant": "",
      "provider": "memory",
      "address": "mod/title",
      "allowOverride": true,
      "providerData": {}
    }
  ]
}
```

规则：

- 字段名使用 `allowOverride`。
- entry `packageId` 为空时继承 catalog `packageId`。
- 全局 key 冲突默认失败。
- 高层覆盖必须显式设置 `allowOverride: true`。
- 覆盖前后 `type` 必须一致。
- 被覆盖 entry 仍可通过原 `PackageId` 精确访问。

## 推荐加载顺序

```text
Base Catalog
  -> Preview/Patch Package Catalog
  -> Mod Package Catalog
  -> Debug Catalog
```

`RuntimeModPackageLoadPlan` 决定启用包和顺序；只有 `OrderedItems` 中的包应挂载资源 Catalog，`SkippedItems` 不进入 `ResourceManager`。

## 验证

新增测试位于：

- `Assets/Scripts/MxFramework/Tests/Config/ModPackageCatalogTests.cs`

覆盖：

- 旧包不声明 `resourceCatalog` 仍可加载。
- `resourceCatalog` 正常解析为包内绝对路径。
- 路径穿越失败。
- 声明文件缺失失败。
- Mod Catalog 能挂载到 `ResourceManager`，并通过 `PackageId` 精确路由访问被覆盖的 base entry。
- 冲突未声明 `allowOverride` 失败。
- 覆盖类型不一致失败。
- LoadPlan 禁用包不会挂载资源 Catalog。

验证命令：

```bash
# Unity EditMode
MxFramework.Tests.Config.ModPackageCatalogTests
```

## 不做范围

- 不接入 Addressables。
- 不做远程 bundle、CDN、下载缓存、版本差分。
- 不做加密、签名或权限授权。
- 不做 Editor Catalog 构建器或完整 UI。
- 不做平台、语言、质量 variant fallback 策略。
- 不改变 `Config.Runtime` 的 noEngine 边界。
- 不导入真实 WGame 业务资源。
