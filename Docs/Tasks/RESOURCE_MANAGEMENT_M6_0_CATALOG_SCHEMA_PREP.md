# Resource Management M6.0: Catalog Schema Prep

> 状态：Implemented
> 日期：2026-05-10
> 优先级：P0
> 前置任务：Resource Management M1-M5

## 结论

M6 正式进入可选 Provider 和策略扩展前，先做一张很薄的 schema 准备任务。

目标不是改加载语义，而是把未来一定会用到、且不应反复改公共 Catalog 结构的字段固定下来：

- `variant`
- `providerData`

`variant` 当前在 `ResourceKey` 和 `ResourceCatalogEntry` 中已经存在，但文档示例和测试 fixture 需要统一显式写出。

`providerData` 是给 RemoteBundle、Addressables、EncryptedBundle 或项目层 Provider 使用的可选扩展字典，避免后续 Provider 被迫继续修改 Catalog 主结构。

## 目标

- Catalog JSON entry 统一包含 `"variant": ""`。
- Catalog JSON entry 新增可选 `"providerData": {}`。
- `ResourceCatalogEntry` 增加 `IReadOnlyDictionary<string, string> ProviderData`。
- `StreamingResourceCatalogLoader` 读取 `providerData`，旧 catalog 缺省时返回空字典。
- Editor / Validator 保持兼容：缺少 `providerData` 不报错。
- 文档示例全部使用 `allowOverride`、`variant`、`providerData`，不再出现早期 `override` 字段。

## 非目标

- 不引入 RemoteBundle。
- 不引入 Addressables。
- 不实现 variant fallback。
- 不实现 providerData 的业务语义。
- 不改变 `IResourceManager`、`IResourceProvider` 或 `ResourceHandle` 公共语义。

## ProviderData 约定

`providerData` 是 provider 专用扩展字段，资源系统核心只负责保存和传递，不解释其业务含义。

示例：

```json
{
  "id": "demo.icon.fire_burst",
  "type": "Texture2D",
  "variant": "",
  "provider": "assetBundle",
  "address": "icons|Assets/TestAssets/MxFramework/ResourcesDemo/resource_demo_text.txt",
  "labels": ["demo", "icon"],
  "dependencies": [],
  "hash": "",
  "size": 0,
  "allowOverride": false,
  "providerData": {
    "bundleName": "icons",
    "cacheKey": "demo.icons.v1"
  }
}
```

规则：

- key / value 均为字符串。
- 缺省等价于空字典。
- 核心 Validator 不校验 providerData 内容。
- 具体 Provider 或 Editor Provider Validator 可校验必需字段。
- `providerData` 不得替代 `provider`、`address`、`hash`、`size` 等通用字段。

## 验收

- 旧 Catalog JSON 不带 `providerData` 仍能加载。
- 新 Catalog JSON 带 `providerData` 能 roundtrip 到 `ResourceCatalogEntry.ProviderData`。
- 文档示例里 Catalog entry 都显式包含 `variant`。
- Resources 测试分组通过：26/26 passed。

## 后续

完成后进入：

- `RESOURCE_MANAGEMENT_M6A_PRELOAD_GROUP_WARMUP.md`
  - 使用 labels 和 explicit keys 构建 warmup plan。
- `RESOURCE_MANAGEMENT_M6B_VARIANT_AND_RETAIN_POLICY.md`
  - 在 M6.0 的 `variant` 字段基础上实现显式 fallback。
