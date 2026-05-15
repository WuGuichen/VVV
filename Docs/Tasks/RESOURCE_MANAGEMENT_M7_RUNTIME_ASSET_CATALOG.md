# Resource Management M7: Runtime Asset Catalog Binding

> 状态：Implemented / First Slice
> 日期：2026-05-10
> 优先级：P1
> 前置任务：M6A Preload Group + Scene Warmup、M6B Variant Catalog + Retain Policy、Runtime Resource Migration 01

> 2026-05-15 更新：该第一段 Runtime Showcase 专用 catalog 已被 #82 的 `mxframework.samples` 资源链路测试取代。当前 RuntimeVerticalSlice 不再使用 `RuntimeVerticalSliceResourceCatalog`、`warmup.runtime_vertical_slice` 或生成的 `runtime_vertical_slice_resource_catalog.json`。

## 目标

M7 把资源系统从“核心可用”推进到“Demo 场景真实使用”：为运行时 UI、调试材质、配置和未来 prefab 建立可生成、可校验、可预加载的 Catalog，并用 M6A/M6B 的 warmup、variant 和 retain 策略驱动 `RuntimeVerticalSlice`。

## 范围

- 为 `Assets/UI/MxFramework/Showcase`、`Assets/Art/MxFramework/Showcase`、`Assets/Config/MxFramework` 中适合运行时加载的资源定义 catalog entry。
- 保持 `Assets/Resources` 不作为长期资源目录；`ResourcesProvider` 只保留给兼容和测试。
- 增加 Editor 侧 catalog 生成入口，输出 schema v1 JSON，包含 `variant`、`labels`、`providerData`。
- 在 `RuntimeVerticalSlice` 场景启动时创建 warmup plan，按 label 预加载 HUD、常用材质和未来扩展资源。
- 场景退出时释放 group；短时间重复进入场景时通过 `Timed` retain 避免 asset churn。

## 关键规则

- Catalog label 表示逻辑资源分组，同一资源的多个 variant 可共享 label；实际变体由 `ResourceVariantProfile` 解析。
- 真实运行时路径优先走 AssetBundle / StreamingFile / RemoteBundle；不得为了方便重新把 Demo 资产塞回 `Assets/Resources`。
- `RetainPolicy` 只影响底层 loaded record，调用方 release handle 后 handle 仍为 released。
- Addressables 仍保持可选适配器，不作为 M7 默认依赖。

## 验收

- `RuntimeVerticalSlice` 能通过 ResourceManager warmup 所需运行时资源。
- Catalog 校验能发现非法 provider、非法 address、重复 `id + type + variant`、缺失依赖。
- M6A/M6B 资源测试继续通过。
- Unity Console 无编译错误、测试清理错误。

## 实现记录

- 新增 `RuntimeVerticalSliceResourceCatalog`，把 Runtime Showcase 的配置 / HUD 序列化引用映射为 `ResourceCatalogEntry`，并通过 `MemoryResourceProvider`、`ResourcePreloadService`、`ResourceVariantProfile` 和 `ResourceRetainPolicy.Timed` 在 `RuntimeVerticalSliceRunner` 启动时预热，销毁时释放 group。
- 新增 `MxFramework/Runtime Showcase/Generate Resource Catalog` Editor 菜单，确定性生成 `Assets/Config/MxFramework/Demo/runtime_vertical_slice_resource_catalog.json`。Catalog schema v1 entry 显式包含 `variant`、`labels`、`providerData.assetPath`。
- `ResourceCatalogEditorValidator` 现在会校验 `memory` provider entry 的 `providerData.assetPath` 是否存在，并复用 Unity 主资源类型校验。
- 第一段不引入 Addressables，不把 Demo 资产移回 `Assets/Resources`，也不手写 Unity 场景 / prefab / ScriptableObject YAML。

## 剩余增量

- 当前 Runtime warmup 使用 `MemoryResourceProvider` 绑定已序列化或已加载的 Unity 对象，适合 Editor Play Mode 和 Demo 组合根；真正播放器主路径仍应由后续 AssetBundle / Streaming catalog 构建接管。
- `Assets/UI/MxFramework/Showcase/` 当前不存在可扫描资产；生成器会在目录存在后自动纳入 UI catalog entry。

## 建议测试

```text
dotnet build MxFramework.Tests.csproj --no-restore
Unity EditMode: MxFramework.Tests.Resources
Unity EditMode: MxFramework.Tests.Combat.RuntimeCombatShowcaseRunnerTests
```
