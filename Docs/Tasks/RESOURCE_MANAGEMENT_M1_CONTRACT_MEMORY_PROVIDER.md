# Resource Management M1：Contract + Memory Provider

> 状态：已实现（2026-05-09）
> 优先级：P0
> 前置文档：`Docs/RESOURCE_MANAGEMENT_SYSTEM.md`

## 目标

建立资源管理系统的 noEngine 最小闭环：

```text
ResourceKey
  -> ResourceCatalog
  -> ResourceManager
  -> MemoryResourceProvider
  -> ResourceHandle<T>
  -> Release / DebugSnapshot
```

M1 不接入 UnityEngine、不加载真实 Unity asset、不处理 AssetBundle。它只固定公共契约、错误语义、Catalog 冲突规则、引用计数和释放行为。

## 已实现范围

- 新增 `MxFramework.Resources` asmdef，保持 `noEngineReferences=true`。
- 新增 `ResourceKey`、`ResourceTypeIds`、`ResourceCatalog`、`ResourceCatalogEntry`。
- 新增 `IResourceManager` / `ResourceManager`。
- 新增 `IResourceProvider`、`ResourceLoadContext`、`ResourceReleaseContext`。
- 新增 `MemoryResourceProvider`。
- 新增 `ResourceHandle<T>`、`ResourceLoadResult<T>`、`ResourceError`、`ResourceErrorCode`。
- 新增 `IResourceOperation<T>` 和 immediate operation，用于后续异步接口兼容。
- 新增 `ResourceDebugSnapshot`、Catalog summary 和 entry origin。
- 新增 `Docs/Interfaces/Resources.md` 并接入 `Docs/INTERFACES.md`。

## 行为契约

- `ResourceKey.Id` 必须符合小写命名规范。
- 同一 Catalog 内 `id + type + variant` 重复会抛出 `ResourceCatalogException`。
- 全局 key 冲突默认失败；高层 entry 必须显式 `allowOverride=true`，且类型一致。
- `PackageId` 非空时精确路由到指定包；为空时走合并后的全局 entry。
- `Load<T>` 类型不匹配返回 `ResourceErrorCode.TypeMismatch`，不会加载 Provider。
- 缺失依赖返回 `ResourceErrorCode.DependencyInvalid`。
- 每次成功加载增加引用计数，`Release` 递减引用计数。
- 同一个 handle 重复释放不抛异常，但记录 `HandleReleased` 到最近错误。
- owner 释放到 0 后，依赖资源按反向顺序释放。

## 测试

新增测试入口：

```text
Assets/Scripts/MxFramework/Tests/Resources/ResourceManagerTests.cs
```

覆盖：

- Memory Provider 加载和引用计数。
- 重复释放幂等。
- Catalog 内重复 key 失败。
- 类型不匹配失败。
- 缺失依赖失败。
- owner 释放后依赖释放。
- 显式 override 替换全局 entry，并保留 PackageId 精确路由。

## 验证记录

已执行：

```text
dotnet build <temp ResourcesCompile.csproj>
dotnet build <temp ResourcesTestsCompile.csproj>
dotnet run <temp ManualResourcesCheck.csproj>
```

结果：

```text
ResourcesCompile: 0 warnings, 0 errors
ResourcesTestsCompile: 0 warnings, 0 errors
manual resources check passed
```

说明：临时 csproj 仅用于快速 C# 编译和核心逻辑验证。Unity `.csproj` 和 `.meta` 需要 Unity Editor refresh 后生成。

## 不做

- 不新增 Unity Provider。
- 不加载 `Resources` 文件夹资产。
- 不加载 AssetBundle。
- 不接入 Mod Package loader。
- 不做 Editor Catalog 构建工具。
- 不新增真实 WGame 资源。

## 下一步

进入 M2：

```text
Resource Management M2：Unity Demo Provider
```

目标是新增 `MxFramework.Resources.Unity`，用 Unity `Resources` Provider 跑通一个最小 Texture 或 Prefab Demo，并通过 Unity Editor / PlayMode 验证主线程加载和释放边界。
