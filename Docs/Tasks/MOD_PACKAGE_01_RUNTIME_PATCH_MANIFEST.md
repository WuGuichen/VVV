# Mod Package 01：Runtime Patch Manifest / Package v0

> **状态**: ✅ 已完成（r1167）
> **优先级**：P0
> 前置任务：`AUTHORING_EDITOR_08_EXPORT_RUNTIME_CONFIG_PATCH.md`、`AUTHORING_EDITOR_09_EXPORT_RUNTIME_PATCH_BUTTON.md`、`AUTHORING_EDITOR_07_SCENE_PREVIEW.md`
> 目标版本：Phase 10.4

## 目标

把当前散落的 `runtime_config_patch.json` 推进为一个最小可用的 Mod 工作包：

```text
Authoring Draft
  -> Runtime Patch v1
  -> Mod Package v0
  -> Validate / Inspect
  -> Runtime Preview 或测试场景手动加载
```

这一步不追求完整 Mod 平台，而是先固定包结构、manifest 字段、路径安全和运行时 Patch 入口。完成后，后续编辑器、CLI、Unity Bridge、AI Agent 都不再直接记忆散文件路径，而是围绕 `mod.json` 识别一个可移植工作包。

## 背景

当前已经完成：

- Buff Authoring Draft 到 Runtime Patch v1 的导出。
- Web Editor 的导出按钮。
- Scene Preview / Runtime Vertical Slice 对 Runtime Patch v1 的加载验证。

但这些能力仍以单个 JSON 文件为中心，缺少 Mod Package 的物理边界。长期看会带来几个问题：

- 无法区分 Preview 包、正式 Mod 包、开发者内部 Patch。
- 无法在包级别声明版本、作者、兼容范围和入口文件。
- 无法对路径穿越、错误格式、包内缺文件做统一校验。
- AI Agent 和外部编辑器仍需要猜测文件位置。

因此下一步先做 `Mod Package v0`，只承载 Runtime Patch，不引入资源包、脚本、发布平台和加载顺序。

## 范围

### 必须完成

1. 定义最小包目录结构：

```text
<PackageRoot>/
  mod.json
  runtime/
    runtime_config_patch.json
  reports/
    validate-report.json      # 可选生成物，不要求纳入版本控制
```

2. 定义 `mod.json` v0 必填字段：

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

字段规则：

| 字段 | 规则 |
| --- | --- |
| `schemaVersion` | 当前只接受 `1`。不匹配必须报错。 |
| `packageId` | 必填，稳定 ID，建议小写点分命名。 |
| `displayName` | 必填，用于 UI 展示。 |
| `author` | 必填。 |
| `version` | 必填，建议 SemVer。 |
| `kind` | 必填，当前只接受 `Preview` 或 `Mod`。 |
| `gameVersionRange` | 必填，v0 可接受 `*`，但正式 `Mod` 包使用 `*` 时应给 warning。 |
| `runtimePatch` | 必填，相对包根目录的路径。 |

3. 固定 Runtime Patch 入口校验：

- `runtimePatch` 不允许绝对路径。
- `runtimePatch` 不允许包含 `..` 路径穿越。
- `runtimePatch` 解析后必须仍位于包根目录内。
- `runtimePatch` 指向的文件必须存在。
- Runtime Patch 文件的 `format` 必须为 `mx.runtimeConfigPatch.v1`。
- `kind = Preview` 时，Runtime Patch 的 `layer` 应为 `Patch`。
- `kind = Mod` 时，Runtime Patch 的 `layer` 应为 `Mod`。
- `kind` 与 `layer` 不匹配时阻断，不允许静默修正。

4. 在 Authoring Core 或 CLI 层补齐包读取能力：

- 读取 `mod.json`。
- 校验 manifest。
- 解析并返回 Runtime Patch 绝对路径或规范化相对路径。
- 输出清晰错误，不抛出难以定位的底层异常。
- 不引用 `UnityEngine` 或 `UnityEditor`。

5. 增加最小样例包：

```text
Tools/MxFramework.Authoring/samples/runtime-patch-mod/
  mod.json
  runtime/runtime_config_patch.json
```

样例包必须能被 CLI 或自动化测试校验通过。

6. 补齐文档：

- 在 Authoring 使用文档或配置文档中说明 `Mod Package v0` 的包结构。
- 明确这不是最终发布包格式，只是后续 Mod/Preview/AI 工作流的最小共同单元。

### 不做

- 不做 zip 打包。
- 不做签名、加密、上传平台、Steam Workshop。
- 不做资源包加载。
- 不做 Mod 依赖关系和加载顺序。
- 不做热更新下载。
- 不执行任何包内脚本。
- 不把真实 WGame 数据导入框架。
- 不新增 Unity 场景。

## 建议实现

### 1. Manifest DTO

在 Authoring Core 中定义纯 C# DTO，例如：

```csharp
public sealed class ModPackageManifest
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
```

如果当前 Authoring Core 已经存在类似类型，应优先扩展现有实现，不新建重复模型。

### 2. Package Reader / Validator

建议形成一个单一入口，例如：

```text
ReadPackage(packageRoot)
  -> manifest
  -> diagnostics
  -> resolvedRuntimePatchPath
```

注意：

- 路径解析必须先做规范化，再判断是否位于包根内。
- manifest 错误和 runtime patch 错误都进入 diagnostics。
- 读取失败不应让 CLI 崩溃，应返回可显示的错误列表。

### 3. CLI 验证入口

CLI 至少需要一个可以独立跑通的入口，命名可按现有命令风格决定，例如：

```bash
dotnet run --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli -- \
  package validate --package Tools/MxFramework.Authoring/samples/runtime-patch-mod
```

输出至少包含：

- packageId
- kind
- version
- runtimePatch resolved path
- diagnostics summary

如果现有 CLI 已有 package validate 命令，优先扩展，不新增平行命令。

## 验收标准

1. `Tools/MxFramework.Authoring/samples/runtime-patch-mod` 能通过 CLI 或自动化测试校验。
2. 缺少 `mod.json` 时返回明确错误。
3. `schemaVersion != 1` 时返回明确错误。
4. `runtimePatch` 为绝对路径或包含 `..` 时返回明确错误。
5. `runtimePatch` 指向不存在文件时返回明确错误。
6. Runtime Patch `format` 不等于 `mx.runtimeConfigPatch.v1` 时返回明确错误。
7. `kind = Mod` 但 Runtime Patch `layer = Patch` 时返回明确错误。
8. `kind = Preview` 但 Runtime Patch `layer = Mod` 时返回明确错误。
9. 校验通过后能输出 Runtime Patch 的解析路径，供 Scene Preview 或测试场景加载。
10. 实现不引用 `UnityEngine` / `UnityEditor`。

## 推荐测试

- `PackageValidator_ValidRuntimePatchPackage_Passes`
- `PackageValidator_MissingManifest_Fails`
- `PackageValidator_InvalidSchemaVersion_Fails`
- `PackageValidator_RuntimePatchPathTraversal_Fails`
- `PackageValidator_MissingRuntimePatch_Fails`
- `PackageValidator_InvalidRuntimePatchFormat_Fails`
- `PackageValidator_ModKindPatchLayerMismatch_Fails`
- `PackageValidator_PreviewKindModLayerMismatch_Fails`

测试应尽量使用临时目录构造包，避免依赖用户机器绝对路径。

## 完成定义

- 任务文档状态改为 `Implemented (rXXXX)`。
- `Docs/README.md` 已加入该任务索引。
- 样例包、校验逻辑、测试均提交。
- SVN 提交信息建议：

```text
Add Mod Package v0 runtime patch manifest
```

