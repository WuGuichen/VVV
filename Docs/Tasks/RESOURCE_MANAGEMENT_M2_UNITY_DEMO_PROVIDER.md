# Resource Management M2：Unity Demo Provider

> 状态：已实现（2026-05-09）
> 优先级：P0
> 前置任务：`RESOURCE_MANAGEMENT_M1_CONTRACT_MEMORY_PROVIDER.md`

## 目标

在 M1 noEngine 契约上新增 Unity `Resources` Provider，验证框架资源管理器可以加载真实 Unity `Resources` 目录下的最小资源，同时保持 Runtime / Editor 依赖边界清晰。

目标链路：

```text
ResourceCatalogEntry(provider="resources")
  -> ResourceManager
  -> MxFramework.Resources.Unity.ResourcesProvider
  -> UnityEngine.Resources.Load
  -> ResourceHandle<TextAsset>
  -> Release / DebugSnapshot
```

## 已实现范围

- 新增 `MxFramework.Resources.Unity` asmdef。
- 新增 `ResourcesProvider`，ProviderId 固定为 `resources`。
- 新增 `UnityResourceTypeResolver`，把框架 `ResourceTypeIds` 映射到 Unity 类型。
- 新增 `ResourcesLoadOperation`，作为 `Resources.LoadAsync` 的轻量 operation 包装。
- 新增最小 Demo 测试资源：`Assets/TestAssets/MxFramework/ResourcesDemo/resource_demo_text.txt`。
- 新增 `ResourcesProviderTests`，覆盖成功加载和缺失资源错误。
- `MxFramework.Tests` 增加 `MxFramework.Resources.Unity` 引用。

## 行为契约

- `ResourcesProvider` 只作为 Demo、小样例和少量常驻资源 Provider。
- `ResourcesProvider.Load` 使用 `UnityEngine.Resources.Load(address, type)`，失败返回 `ResourceErrorCode.NotFound`。
- `ResourcesProvider.Release` 对非 `GameObject` 的 `UnityEngine.Object` 调用 `UnityEngine.Resources.UnloadAsset`。
- `GameObject` prefab 不在 M2 中验证释放，避免误用 `UnloadAsset`。
- `ResourcesLoadOperation` 可表达 Unity async request 状态，但 `ResourceManager.LoadAsync` 的 provider 调度仍留到后续异步切片。

## 测试

新增测试入口：

```text
Assets/Scripts/MxFramework/Tests/Resources/ResourcesProviderTests.cs
```

覆盖：

- 从 Unity `Resources` 目录加载 `TextAsset` 并返回 `ResourceHandle<TextAsset>`。
- `Release` 后 `LoadedCount` 归零。
- 缺失地址返回 `ResourceErrorCode.NotFound`。

## 验证记录

已执行：

```text
Unity refresh + compile
Unity EditMode: MxFramework.Tests.Resources.ResourcesProviderTests
Unity EditMode: MxFramework.Tests.Resources
```

结果：

```text
ResourcesProviderTests: 2/2 passed
Resources test group: 9/9 passed
Unity Console errors: 0
```

## 不做

- 不做 AssetBundle Provider。
- 不做 Addressables Provider。
- 不做 Editor Catalog 构建器。
- 不把 `Resources` Provider 作为大项目主路径。
- 不在 M2 中实现 `ResourceManager.LoadAsync` 到 Provider async operation 的完整调度。

## 下一步

进入 M3：

```text
Resource Management M3：AssetBundle Provider + Catalog File
```

目标是支持从 StreamingAssets 读取 Catalog，并用本地 AssetBundle manifest / dependency table 跑通 bundle、asset 和 dependency 的引用计数及安全卸载。
