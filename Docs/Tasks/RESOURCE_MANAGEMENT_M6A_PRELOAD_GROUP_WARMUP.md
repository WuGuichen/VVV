# Resource Management M6A: Preload Group + Scene Warmup

> 状态：Implemented
> 日期：2026-05-10
> 优先级：P0
> 前置任务：Resource Management M6.0 Catalog Schema Prep

## 结论

M6A 优先做 Preload Group + Scene Warmup。

这是 M6 中当前收益最高、侵入最低的一项：它不需要新增资源来源，不改变 M1-M5 公共契约，只在 `IResourceManager` 之上提供批量加载、失败收集和统一释放策略。

## 为什么先做

Preload / Warmup 直接解决体验问题：

- 场景切换卡顿。
- UI 第一次打开卡顿。
- 战斗第一次触发 VFX / SFX 卡顿。
- 第一次加载失败只在触发瞬间暴露。

框架已有能力已经足够支撑 M6A：

- `ResourceKey`
- Catalog labels
- `IResourceManager.LoadAsync<T>`
- `ResourceHandle`
- `ResourceDebugSnapshot`
- `ResourceCatalogValidator`

M6A 只做加载调度策略，不做 Provider。

## 设计边界

- PreloadGroup 不是 Provider。
- 不修改 `IResourceManager` 签名。
- 不修改 `IResourceProvider` 签名。
- 不改变 handle release 语义。
- 不引入场景管理硬依赖；Scene Warmup 只提供可由项目层/Unity Demo 调用的 plan。
- 不做 RetainPolicy；预加载 group 只持有 handles，释放时调用现有 `ResourceManager.Release`。

## 已实现 API

```csharp
public interface IResourcePreloadService
{
    IResourceOperation<ResourcePreloadResult> PreloadAsync(
        ResourcePreloadPlan plan,
        CancellationToken cancellationToken = default);

    void ReleaseGroup(ResourceGroupHandle handle);
}
```

```csharp
public sealed class ResourcePreloadPlan
{
    public string GroupId { get; }
    public IReadOnlyList<string> Labels { get; }
    public IReadOnlyList<ResourceKey> ExplicitKeys { get; }
    public bool FailFast { get; }
    public int MaxConcurrentLoads { get; }
}
```

```csharp
public sealed class ResourcePreloadResult
{
    public string GroupId { get; }
    public ResourceGroupHandle Handle { get; }
    public int RequestedCount { get; }
    public int LoadedCount { get; }
    public int FailedCount { get; }
    public IReadOnlyList<ResourceError> Errors { get; }
}
```

`MaxConcurrentLoads` 第一版可先保留字段，底层仍使用当前 immediate async；不要为了并发调度提前复杂化。

M6A 同时新增只读 Catalog 查询接口：

```csharp
public interface IResourceCatalogQuery
{
    IReadOnlyList<ResourceKey> FindKeysByLabel(string label);
}
```

`ResourceManager` 实现该接口，但 `IResourceManager` 公共签名保持不变。`ResourcePreloadService` 通过可选查询接口解析 label，再用现有 `LoadAsync<object>` 执行预加载。

M6B 后的约定：label 查询返回逻辑资源 key，而不是 catalog 中的每一条具体 variant entry。同一资源的多个 variant 可以共用同一个 warmup label，预加载时只请求一次，再由当前 `ResourceVariantProfile` 选择实际变体。

## Catalog 使用方式

Catalog entry 通过 labels 标记 warmup 分组：

```json
{
  "id": "demo.vfx.hit_spark",
  "type": "GameObject",
  "variant": "",
  "provider": "assetBundle",
  "address": "combat|Assets/MxFramework/Demo/Vfx/hit_spark.prefab",
  "labels": ["scene.combat_demo", "warmup.combat", "vfx"],
  "dependencies": [],
  "providerData": {}
}
```

计划可以混用 labels 和 explicit keys：

```text
warmup.combat
scene.combat_demo
explicit: demo.ui.loading_icon
```

## Scene Warmup 推荐流程

```text
SceneTransition Begin
  -> build preload plan for next scene
  -> preload group
  -> report progress / failures
  -> activate scene
  -> release previous scene group
```

项目层决定何时激活场景；框架只提供 preload / release group。

## Diagnostics

M6A 需要在资源诊断中能看到：

- group id
- requested / loaded / failed count
- preload errors
- group handle 是否已释放

第一版已把这些信息放入 `ResourcePreloadResult` 和 `ResourceGroupHandle`。`ResourceDebugSnapshot` 暂不增加 group 列表，后续可在 M6B 或 M7 与 RetainPolicy / group diagnostics 一起扩展。

## 测试

已覆盖：

- explicit keys 预加载成功。
- label 预加载成功。
- 缺失资源收集错误。
- `FailFast=true` 时首个失败后停止。
- `ReleaseGroup` 释放全部成功加载的 handles。
- 重复 `ReleaseGroup` 幂等。
- cancellation 返回 cancelled error，不泄漏已加载 handles。

测试入口：

```text
Assets/Scripts/MxFramework/Tests/Resources/ResourcePreloadServiceTests.cs
```

验证记录：

```text
dotnet build MxFramework.Tests.csproj --no-restore
Unity EditMode: MxFramework.Tests.Resources.ResourcePreloadServiceTests, 6/6 passed
Unity EditMode: MxFramework.Tests.Resources, 41/41 passed
Unity Console errors after run: 0
```

## 不做范围

- 不做 RetainPolicy。
- 不做 memory budget。
- 不做 RemoteBundle。
- 不做 Addressables。
- 不强绑定 Unity SceneManager。
- 不做 UI 进度条，只输出结构化结果。
