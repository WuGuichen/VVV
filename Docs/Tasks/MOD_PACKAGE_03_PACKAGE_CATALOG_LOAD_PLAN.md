# Mod Package 03：Package Catalog / Load Plan v0

> **状态**: ✅ 已完成（r1173）
> **优先级**：P0
> 前置任务：`MOD_PACKAGE_02_RUNTIME_PACKAGE_LOAD.md`
> 目标版本：Phase 10.7

## 目标

在已经能加载单个 Mod Package 的基础上，补齐“包目录扫描、包索引、加载计划”能力。

目标链路：

```text
Package Roots
  -> Discover packages
  -> Validate manifests
  -> Build package catalog
  -> Build deterministic load plan
  -> Future: apply load plan to RuntimeConfigPatchMerger
```

这一步不直接做完整多 Mod 冲突解决，也不做玩家 UI。它先让框架知道“当前有哪些包、哪些包有效、应该以什么顺序加载、哪些包被禁用或报错”。后续 Mod Mode、Developer Mode、外部编辑器、AI Agent 都应该基于这个 catalog 工作，而不是各自扫描文件夹。

## 背景

当前已经完成：

- `Mod Package v0` manifest 和 CLI 校验。
- 运行时从单个 package root 读取 `mod.json` 并加载 Runtime Patch。
- `RuntimeVerticalSlice` 可以通过包驱动跑通 Attributes + Buffs + Modifiers。

但实际开发和 Mod 使用不会长期只有一个包。至少会出现：

- Base / Demo 包。
- Developer 临时测试包。
- Preview 包。
- 玩家 Mod 包。
- 被禁用或校验失败的包。

如果现在直接做“多包合并”，会过早碰到冲突 UI、依赖图、优先级策略。更稳的下一步是先做 catalog 和 load plan：只负责发现、校验、排序、输出可解释结果。

## 范围

### 必须完成

1. 定义包扫描输入。

建议支持多个 package root 容器目录，例如：

```text
Assets/StreamingAssets/MxFramework/Demo/
  runtime-patch-mod/

<persistentDataPath>/MxFramework/Mods/
  some-player-mod/
```

v0 可以只在测试和 demo 中使用 `StreamingAssets`，但 API 不能写死只有一个目录。`StreamingAssets`、`persistentDataPath` 或后续自定义 Mod 目录都应作为同级容器输入处理，由调用方决定传入哪些路径。

2. 新增 Runtime 侧 catalog 类型。

建议放在 `MxFramework.Config.Runtime`：

```text
RuntimeModPackageCatalog.cs
RuntimeModPackageDiscovery.cs
RuntimeModPackageLoadPlan.cs
```

推荐模型：

```csharp
public sealed class RuntimeModPackageCatalog
{
    public IReadOnlyList<RuntimeModPackageCatalogItem> Items { get; }
}

public sealed class RuntimeModPackageCatalogItem
{
    public string PackageRootPath { get; }
    public RuntimeModPackageManifest Manifest { get; }
    public bool IsValid { get; }
    public bool IsEnabled { get; }
    public IReadOnlyList<string> Errors { get; }
    public IReadOnlyList<string> Warnings { get; }
}

public sealed class RuntimeModPackageLoadPlan
{
    public IReadOnlyList<RuntimeModPackageCatalogItem> OrderedItems { get; }
    public IReadOnlyList<RuntimeModPackageCatalogItem> SkippedItems { get; }
}
```

命名可调整，但职责必须明确：

- Catalog：当前发现了什么。
- CatalogItem：单个包状态。
- LoadPlan：本轮准备加载什么、跳过什么、顺序是什么。

3. 扫描规则。

v0 扫描规则：

- 容器目录不存在时返回空 catalog，不抛异常。
- 只扫描容器目录下一层子目录。
- 子目录含 `mod.json` 才视为候选包。
- 候选包复用 `RuntimeModPackageLoader` 的 manifest/path/kind-layer 校验。
- 扫描多个容器目录时，所有候选包统一进入同一个 catalog，不按容器目录隐式分层。
- 校验失败的包也进入 catalog，但 `IsValid = false`。
- 错误信息必须保留到 item 上，便于 UI、日志、AI Agent 读取。

4. 确定性排序规则。

v0 load plan 排序：

1. `kind = Preview`
2. `kind = Mod`
3. 同 kind 内按 `packageId` 升序。
4. `packageId` 相同时按 normalized package root path 升序。

说明：

- 这不是最终 Mod 加载优先级，只是 v0 稳定排序。
- v0 不要求 `packageId` 全局唯一，但重复 `packageId` 必须产生 warning，并通过 root path 二级排序保证结果确定。
- 后续可以加 `loadPriority`、依赖、启用列表。
- v0 不允许排序结果依赖文件系统返回顺序。

5. 启用状态。

v0 不做复杂配置 UI，但需要预留启用状态输入。

建议 API 支持：

```csharp
BuildLoadPlan(catalog, enabledPackageIds)
```

规则：

- `enabledPackageIds == null` 表示所有 valid 包启用。
- 传入集合时，只启用集合内 packageId。
- invalid 包永远进入 `SkippedItems`。
- disabled 包进入 `SkippedItems`，但不是 error。

6. Demo 输出。

在 `RuntimeVerticalSliceRunner` 或一个轻量 demo 入口中展示 catalog / load plan 摘要。

要求：

- 不新增 Unity 场景。
- 可以复用 `RuntimeVerticalSlice.unity`。
- 至少在日志或 OnGUI 中显示：
  - discovered count
  - valid count
  - skipped count
  - ordered packageId 列表
  - error packageId/path + error message

7. 文档更新。

至少更新：

- `Docs/CAPABILITIES.md`
- `Docs/USAGE.md` 或 `Docs/Interfaces/Config.md`

说明：

- 单包加载用 `RuntimeModPackageLoader`。
- 多包发现和排序用 Catalog / LoadPlan。
- v0 load plan 只负责发现和排序，不代表最终冲突解决策略。

### 不做

- 不做多包 Runtime Patch 合并执行。
- 不做冲突解决 UI。
- 不做依赖关系。
- 不做 zip 解包。
- 不做签名和安全授权。
- 不做平台发布。
- 不做玩家 Mod 管理界面。
- 不引入真实 WGame 数据。
- 不新增 Unity 场景。

## 建议实现

### 1. Discovery API

建议 API：

```csharp
public static RuntimeModPackageCatalog Discover(IEnumerable<string> packageContainerPaths)
```

如果需要保留来源信息，也可以让 container path 进入 item：

```csharp
public string ContainerPath { get; }
public string PackageRootPath { get; }
```

### 2. Load Plan API

建议 API：

```csharp
public static RuntimeModPackageLoadPlan Build(
    RuntimeModPackageCatalog catalog,
    ISet<string> enabledPackageIds = null)
```

不要在 Build 阶段重新读取文件。Build 应只基于 CatalogItem 决策。

### 3. Diagnostics

错误消息要面向工具和人类都可读，例如：

```text
Package 'foo.bar' skipped: runtimePatch escapes package root.
Package at '/path/no-id' skipped: packageId is required.
```

不要只返回 exception type。

### 4. 与单包 Loader 的关系

Discovery 不应复制 `RuntimeModPackageLoader` 的核心校验逻辑。推荐：

```text
Discovery
  -> 找到 candidate package root
  -> 调 RuntimeModPackageLoader.TryLoadManifestOrPackage(...)
  -> 组装 CatalogItem
```

如果现有 `RuntimeModPackageLoader.LoadFromDirectory` 失败时只抛异常，可以补一个非抛异常的 Try API，避免扫描多个包时第一个坏包中断整个 catalog。

推荐新增 API：

```csharp
public static bool TryLoadFromDirectory(
    string packageRootPath,
    out RuntimeModPackageLoadResult result,
    out RuntimeModPackageLoadError error)
```

要求：

- 保留旧的 `LoadFromDirectory` 抛异常 API，避免破坏现有调用方。
- Discovery 必须使用 Try API，不能用 try/catch 包住旧 API 当作主要控制流。
- `RuntimeModPackageLoadError` 至少包含 package root path 和人类可读 message。

## 风险与缓解

| 风险 | 缓解 |
|------|------|
| Discovery 需要扫描多个包，旧 `LoadFromDirectory` 只抛异常，坏包会中断整个流程 | 新增 `TryLoadFromDirectory` 返回 result + error，保留旧 API |
| 容器目录可能来自 `StreamingAssets`、`persistentDataPath` 或自定义目录 | API 接受 `IEnumerable<string>`，所有容器作为同级输入，不写死路径 |
| 排序依赖 `packageId`，但 `packageId` 可能重复 | 重复时产生 warning，同 `packageId` 按 normalized root path 升序，保证确定性 |

## 验收标准

1. 容器目录不存在时返回空 catalog，不报错。
2. 容器目录下没有 `mod.json` 的子目录会被忽略。
3. 有效包进入 catalog，`IsValid = true`。
4. 缺失字段或路径非法的包进入 catalog，`IsValid = false`，并保留错误信息。
5. `BuildLoadPlan(null enabledPackageIds)` 会启用所有 valid 包。
6. `BuildLoadPlan(enabledPackageIds)` 只启用指定 packageId。
7. invalid 包永远进入 skipped。
8. disabled 包进入 skipped，且不作为 error。
9. 排序稳定，不依赖文件系统枚举顺序。
10. 重复 `packageId` 不会导致排序不稳定，catalog item 保留 warning。
11. `RuntimeModPackageLoader.LoadFromDirectory` 旧 API 保持可用。
12. Discovery 使用 `TryLoadFromDirectory`，坏包不会中断其他包扫描。
13. `RuntimeVerticalSlice.unity` 可显示 catalog/load plan 摘要。
14. Unity EditMode 测试覆盖：空目录、有效包、非法包、启用过滤、重复 packageId、稳定排序。
15. 实现不引用 Authoring Core / CLI，不引用 `UnityEditor`。

## 推荐测试

- `RuntimeModPackageDiscovery_MissingContainer_ReturnsEmptyCatalog`
- `RuntimeModPackageDiscovery_IgnoresChildWithoutManifest`
- `RuntimeModPackageDiscovery_ValidPackage_ProducesValidItem`
- `RuntimeModPackageDiscovery_InvalidPackage_ProducesInvalidItemWithErrors`
- `RuntimeModPackageLoadPlan_NullEnabled_UsesAllValidPackages`
- `RuntimeModPackageLoadPlan_EnabledSet_FiltersPackages`
- `RuntimeModPackageLoadPlan_DuplicatePackageId_WarnsAndSortsByRootPath`
- `RuntimeModPackageLoadPlan_SortsDeterministically`

测试数据优先使用临时目录，不依赖用户机器绝对路径。

## 完成定义

- 任务状态改为 `✅ 已完成（rXXXX）`。
- Catalog / LoadPlan API 已有测试。
- `RuntimeVerticalSlice.unity` 不新增场景即可查看摘要。
- `Docs/CAPABILITIES.md` 更新运行时能力。
- `Docs/USAGE.md` 或 `Docs/Interfaces/Config.md` 说明单包与多包 catalog 的使用边界。
- SVN 提交信息建议：

```text
Add runtime mod package catalog load plan
```
