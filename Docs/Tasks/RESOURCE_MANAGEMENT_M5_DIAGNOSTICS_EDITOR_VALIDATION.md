# Resource Management M5: Diagnostics + Editor Validation

> 状态：Implemented
> 日期：2026-05-10
> 优先级：P0
> 前置任务：Resource Management M1-M4

## 目标

让资源系统的运行时状态能进入通用 Diagnostics 报告，并提供 Editor 侧 Catalog 校验入口，用于在接入 Demo、Preview 和项目资源前提前发现配置问题。

M5 不改变资源加载语义，不引入 Addressables、远程 bundle、资源构建 UI 或 retain policy。

## 实现范围

- `MxFramework.Resources` 引用 `MxFramework.Diagnostics`，保持 `noEngineReferences=true`。
- 新增 `ResourceDebugSource`：
  - 实现 `IFrameworkDebugSource`。
  - 从 `IResourceManager.CreateDebugSnapshot()` 生成 `FrameworkDebugSnapshot`。
  - 输出 Summary、Catalogs、Entry Origins、Recent Errors 四段报告。
- 新增 `ResourceDebugSnapshotFormatter`：
  - 将 `ResourceDebugSnapshot` 转成通用诊断快照。
  - 可直接用 `FrameworkDebugReportExporter.ExportText(...)` 导出文本。
- 新增 `ResourceCatalogValidator`：
  - 校验 schemaVersion。
  - 校验 key 合法性。
  - 校验同 catalog 内 `id + type + variant` 唯一。
  - 校验 provider 是否注册。
  - 校验 address 为安全相对地址。
  - 校验依赖存在和依赖环。
- 新增 `ResourceCatalogValidationReport` / `ResourceCatalogValidationIssue`。
- 新增 `ResourceCatalogEditorValidator`：
  - 基于 `ResourceCatalogValidator` 做基础结构校验。
  - 对 `resources` provider 使用 `AssetDatabase` 检查 `Assets/**/Resources/{address}` 是否存在，并校验主资源类型。
  - 对 `assetBundle` provider 校验 `bundle|Assets/...` asset 路径和类型。
  - 对 `streamingFile` provider 校验 `Application.streamingAssetsPath` 下文件存在。
  - 可生成文本报告。
- Framework Manager 模块列表补充 Resources / Resources.Unity。

## 使用方式

运行时诊断：

```csharp
var source = new ResourceDebugSource(resourceManager);
FrameworkDebugSnapshot snapshot = source.CreateSnapshot();
string report = FrameworkDebugReportExporter.ExportText(snapshot);
```

Editor 校验：

```csharp
ResourceCatalogValidationReport report =
    ResourceCatalogEditorValidator.ValidateCatalog(catalog, new[] { "resources", "assetBundle" });

string text = ResourceCatalogEditorValidator.CreateReportText(catalog, report);
```

## 验证

新增或扩展测试：

- `ResourceManagerTests.ResourceDebugSource_ExportsSnapshotAsFrameworkDebugReport`
- `ResourceManagerTests.ResourceCatalogValidator_DetectsProviderUnsafeAddressAndMissingDependency`
- `ResourceManagerTests.ResourceCatalogValidator_DetectsDependencyCycle`
- `ResourceCatalogEditorValidatorTests.ValidateCatalog_ResourcesAddressExistsAndTypeMatches_ReturnsNoErrors`
- `ResourceCatalogEditorValidatorTests.ValidateCatalog_ResourcesAddressMissing_ReturnsAssetMissing`

Unity EditMode 验证：

```text
MxFramework.Tests.Resources.ResourceManagerTests
MxFramework.Tests.Resources.ResourceCatalogEditorValidatorTests
```

结果：12 tests, 12 passed.

## 不做范围

- 不做完整 Catalog 构建器 UI。
- 不扫描孤儿资源和未引用 entry 的全量报告。
- 不接入 Addressables。
- 不做远程 bundle 下载、hash 校验、版本差分。
- 不改变 `ResourceManager` 的引用计数和释放语义。
