# Mod Package 02：运行时包驱动加载闭环

> **状态**: ✅ 已完成（r1169）
> **优先级**：P0
> 前置任务：`MOD_PACKAGE_01_RUNTIME_PATCH_MANIFEST.md`
> 目标版本：Phase 10.6

## 目标

让运行时垂直切片不再只依赖单个 `runtime_config_patch.json` 路径，而是可以从一个最小 Mod Package 读取 `mod.json`，解析 `runtimePatch` 入口，再加载 Runtime Patch v1，最终驱动现有的 Attributes + Buffs + Modifiers 闭环。

目标链路：

```text
Mod Package Root
  -> mod.json
  -> runtime/runtime_config_patch.json
  -> RuntimeConfigPatchJsonLoader
  -> RuntimeConfigPatchMerger
  -> ConfigBuffFactory / ConfigModifierFactory
  -> RuntimeVerticalSlice / ScenePreviewWorld
```

这一步完成后，框架运行时就能消费“包”，而不是只消费“散文件”。这也是后续 Mod Mode、Developer Mode、外部编辑器、AI Agent 协作的共同基础。

## 背景

`MOD_PACKAGE_01_RUNTIME_PATCH_MANIFEST.md` 已经完成了 Authoring/CLI 侧的最小包结构和校验：

- `mod.json`
- `runtimePatch`
- `package validate`
- `kind` 与 Runtime Patch `layer` 匹配规则
- 路径穿越和 format 校验

但运行时侧仍主要通过硬编码或序列化字段加载单个 `runtime_config_patch.json`。这会导致：

- 测试场景与主创工具使用的单位不一致。
- 运行时无法知道 patch 来源包、kind、version。
- 后续 Mod 多包加载无法建立在当前垂直切片上。

因此这轮只补“单包运行时加载”，不做多包排序和平台化。

## 范围

### 必须完成

1. 在 Runtime 侧新增最小包 Manifest 读取能力。

建议放在 `MxFramework.Config.Runtime`，例如：

```text
Assets/Scripts/MxFramework/Config.Runtime/
  RuntimeModPackageManifest.cs
  RuntimeModPackageLoader.cs
```

要求：

- 不引用 `UnityEditor`。
- 不引用 Authoring Core / CLI 程序集。
- 可以引用 `Newtonsoft.Json`，保持与 `RuntimeConfigPatchJsonLoader` 一致。
- 只解析运行时必要字段，不复用外部工具 DTO。

2. 支持最小字段：

```json
{
  "schemaVersion": 1,
  "packageId": "sample.buff.preview",
  "displayName": "Buff Preview Sample",
  "author": "MxFramework",
  "version": "0.1.0",
  "kind": "Preview",
  "gameVersionRange": "*",
  "runtimePatch": "runtime/runtime_config_patch.json"
}
```

运行时必须至少保留：

- `PackageId`
- `DisplayName`
- `Version`
- `Kind`
- `RuntimePatch`
- 解析后的 Runtime Patch 路径

3. 固定运行时校验规则：

- `schemaVersion` 必须为 `1`。
- `kind` 必须为 `Preview` 或 `Mod`。
- `runtimePatch` 必填。
- `runtimePatch` 不允许绝对路径。
- `runtimePatch` 不允许包含 `..`。
- `runtimePatch` 解析后必须仍位于 package root 内。
- Runtime Patch 文件必须存在。
- Runtime Patch `format` 必须由现有 `RuntimeConfigPatchJsonLoader` 校验。
- Runtime Patch `layer` 必须与 manifest `kind` 匹配：
  - `Preview` -> `Patch`
  - `Mod` -> `Mod`

4. 复用 `RuntimeVerticalSlice.unity`，不新建场景。

在 `RuntimeVerticalSliceRunner` 上增加包驱动模式，建议字段：

```csharp
[SerializeField] private bool _useModPackage;
[SerializeField] private string _modPackagePath = "MxFramework/Demo/runtime-patch-mod";
```

行为：

- `_useModPackage = true` 时优先走包驱动。
- 包路径从 `Application.streamingAssetsPath` 解析。
- 加载成功后日志显示 `packageId / version / kind / runtimePatch`。
- 加载失败时显示明确错误，不回退到硬编码模式。
- 不删除现有 `_useConfigDriven` 和 `_usePatchFile`，保持旧验收可用。

5. 让 `ScenePreviewWorld` 也能复用包加载逻辑。

当前预览世界仍可保留固定 patch 文件路径，但应抽出共享加载流程，避免：

- RuntimeVerticalSlice 用一套 patch 解析。
- ScenePreviewWorld 再手写一套路径和 merge 逻辑。

建议形成内部辅助函数：

```text
LoadRuntimePatchBundleFromJson(json)
ApplyRuntimePatchBundle(bundle)
```

或等价拆分，只要不重复硬编码 merge 逻辑即可。

6. 增加 StreamingAssets 样例包。

建议路径：

```text
Assets/StreamingAssets/MxFramework/Demo/runtime-patch-mod/
  mod.json
  runtime/
    runtime_config_patch.json
```

内容可以复用当前已验证的 demo patch。注意 `.meta` 由 Unity 生成；如果通过文件系统生成后 Unity 未自动生成 meta，本任务实现者应通过 Unity refresh 验证。

7. 补充接口/使用文档。

至少更新：

- `Docs/CAPABILITIES.md`
- `Docs/USAGE.md` 或 `Docs/AUTHORING_EDITOR_USAGE.md`
- 必要时更新 `Docs/Interfaces/Config.md`

说明：

- 什么时候用 raw patch。
- 什么时候用 Mod Package。
- 包路径如何相对 `StreamingAssets` 解析。

### 不做

- 不做多 Mod 包同时加载。
- 不做 load order。
- 不做冲突解决 UI。
- 不做 zip 解包。
- 不做签名、权限授权、平台发布。
- 不做外部编辑器 UI 改版。
- 不引入真实 WGame 数据。
- 不新增 Unity 场景。

## 建议实现

### 1. Runtime DTO 与结果类型

建议最小类型：

```csharp
public sealed class RuntimeModPackageManifest
{
    public int SchemaVersion { get; set; }
    public string PackageId { get; set; }
    public string DisplayName { get; set; }
    public string Author { get; set; }
    public string Version { get; set; }
    public string Kind { get; set; }
    public string GameVersionRange { get; set; }
    public string RuntimePatch { get; set; }
}

public sealed class RuntimeModPackageLoadResult
{
    public RuntimeModPackageManifest Manifest { get; }
    public string PackageRootPath { get; }
    public string RuntimePatchPath { get; }
    public RuntimeConfigPatchBundle PatchBundle { get; }
}
```

命名可按现有代码风格调整，但职责要清楚：Manifest 是元数据，LoadResult 是解析后的运行时入口。

### 2. Loader API

建议 API：

```csharp
public static RuntimeModPackageLoadResult LoadFromDirectory(string packageRootPath)
```

失败时可以抛出明确异常，例如 `RuntimeModPackageLoadException`。异常消息必须能直接显示在 demo UI 或 preview log 中。

### 3. kind / layer 映射

不要把 manifest `kind` 偷偷改写成 patch `layer`。如果不匹配，应直接失败。

```text
Preview package -> Runtime Patch layer Patch
Mod package     -> Runtime Patch layer Mod
```

这样可以避免把预览包误当正式 Mod 包。

### 4. Demo UI 显示

`RuntimeVerticalSliceRunner.OnGUI` 中应能看到：

- 当前模式：`Mod Package Driven`
- packageId
- version
- kind
- runtimePatch
- merge 变更摘要
- 错误信息

不用做复杂布局，只要可读、可排错。

## 验收标准

1. `RuntimeVerticalSlice.unity` 不新增场景即可切到包驱动模式。
2. 包驱动模式能加载 `Assets/StreamingAssets/MxFramework/Demo/runtime-patch-mod/mod.json`。
3. 成功加载后，Attack 结果与当前 Runtime Patch 文件驱动结果一致。
4. `AttributeChangedEvent` 仍会正确发布，日志能看到配置驱动后的属性变化。
5. 删除 `mod.json` 时，demo 显示明确错误。
6. `runtimePatch` 指向不存在文件时，demo 显示明确错误。
7. `runtimePatch` 包含 `..` 时，loader 拒绝加载。
8. `kind = Mod` 但 Runtime Patch `layer = Patch` 时，loader 拒绝加载。
9. `kind = Preview` 但 Runtime Patch `layer = Mod` 时，loader 拒绝加载。
10. Unity EditMode 测试覆盖 manifest 成功、路径穿越、kind/layer mismatch 至少 3 类用例。
11. 实现不引用 Authoring Core / CLI，不引用 `UnityEditor`。

## 推荐测试

- `RuntimeModPackageLoader_ValidPreviewPackage_LoadsPatch`
- `RuntimeModPackageLoader_MissingManifest_Fails`
- `RuntimeModPackageLoader_PathTraversal_Fails`
- `RuntimeModPackageLoader_KindLayerMismatch_Fails`
- `RuntimeVerticalSlice_ModPackageDriven_PublishesAttributeChangedEvent`

测试数据优先使用临时目录。StreamingAssets 样例只用于人工验证和 demo。

## 完成定义

- 任务状态改为 `✅ 已完成（rXXXX）`。
- `RuntimeVerticalSlice.unity` 仍是唯一垂直切片场景。
- `Docs/CAPABILITIES.md` 更新运行时能力表。
- 测试通过，并在任务文档记录命令或 Unity Test Runner 结果。
- SVN 提交信息建议：

```text
Add runtime mod package loading slice
```

