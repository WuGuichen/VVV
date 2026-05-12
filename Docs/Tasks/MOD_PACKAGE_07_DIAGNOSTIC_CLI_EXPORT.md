# Mod Package 07：Diagnostic CLI Export v0

> **状态**: ✅ 已完成（r1182）
> **优先级**：P0
> 前置任务：`MOD_PACKAGE_06_DIAGNOSTIC_SNAPSHOT.md`
> 目标版本：Phase 10.11

## 目标

把 06 中完成的 Mod Diagnostic Snapshot 接到外部 Authoring CLI，让开发者、外部编辑器和 AI Agent 可以在不打开 Unity 场景、不进入 PlayMode 的情况下导出当前 Mod 组合诊断报告。

目标链路：

```text
CLI command
  -> package containers
  -> optional loadout file
  -> catalog
  -> load plan
  -> multi-package merge
  -> diagnostic snapshot
  -> JSON file / stdout
```

这一步完成后，Mod 诊断不再只依赖 `RuntimeVerticalSlice` 的 OnGUI 或 Unity 日志，而是成为一个可自动化执行的工具命令。

## 背景

当前运行时已经具备：

- 包发现：`RuntimeModPackageDiscovery`
- 启用状态：`RuntimeModPackageLoadoutJson`
- 加载计划：`RuntimeModPackageLoadPlanBuilder`
- 多包合并：`RuntimeModPackagePatchMerger`
- 诊断快照：`RuntimeModDiagnosticSnapshotBuilder`

但这些能力主要在 Unity Runtime 侧。外部编辑器和 AI Agent 目前仍缺一个稳定入口来生成同样的诊断报告。

本任务的重点是命令行闭环，而不是新增 UI。

## 范围

### 必须完成

1. 增加 CLI 命令。

命令固定为独立顶级命令组 `mod diagnose`，与现有 `package validate`、`runtime-patch export`、`manifest export` 保持同样的两级命令风格。

示例：

```bash
dotnet run --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli -- \
  mod diagnose \
  --container Assets/StreamingAssets/MxFramework/Demo \
  --loadout Assets/StreamingAssets/MxFramework/Demo/mod_loadout.json \
  --output Library/MxFramework/Diagnostics/mod_diagnostic_snapshot.json
```

2. 支持输入参数。

必须支持：

| 参数 | 规则 |
| --- | --- |
| `--container` | 可重复；每个值是一个包容器目录。 |
| `--containers` | 可选兼容参数；逗号分隔多个包容器目录。 |
| `--loadout` | 可选；不传时使用默认 all-valid 策略。 |
| `--output` | 可选；不传时输出到 stdout。 |
| `--pretty` | 可选；输出缩进 JSON。 |

建议支持：

| 参数 | 规则 |
| --- | --- |
| `--include-absolute-paths` | 本机诊断可开；默认 false 或按当前 snapshot API 默认。 |
| `--fail-on-warning` | CI 可用；有 warning 时返回非 0。 |

默认值：

- 未传 `--container` / `--containers` 时，默认使用 `Assets/StreamingAssets/MxFramework/Demo`。
- 未传 `--loadout` 时，不读取文件，按 all-valid 策略构建 load plan。
- 未传 `--output` 时，完整 snapshot JSON 写入 stdout。

3. 明确 v0 架构策略。

CLI 输出的 JSON 必须与 06 的 `mx.modDiagnosticSnapshot.v1` 格式一致。

v0 采用“共享 DTO + CLI 自有管线”：

- Snapshot 数据模型必须唯一化，避免 Runtime 和 CLI 各自维护一份同名字段。
- CLI 侧实现自己的文件系统发现、loadout 读取、load plan、demo/basic merge、snapshot 构建流程。
- Runtime 侧现有 `RuntimeModDiagnosticSnapshotBuilder` 保持可用，不为了 CLI 大规模搬迁 06 已完成代码。
- Runtime 和 CLI 的 JSON 字段必须通过 schema 一致性测试对齐。

推荐 v0 做法：

- 把 `RuntimeModDiagnosticSnapshot.cs` 中的纯 DTO 拆成共享源文件，例如 `ModDiagnosticSnapshotDto.cs`。
- Unity Runtime 通过 asmdef 正常编译该源文件。
- CLI csproj 通过 linked compile item 引用同一个源文件。
- DTO 不绑定 Newtonsoft.Json 或 System.Text.Json attribute，序列化配置分别由 Runtime / CLI 自己提供。

不要在 v0 尝试让 dotnet CLI 直接引用 Unity asmdef，也不要把整个 `MxFramework.Config.Runtime` 抽成双栖包。

4. 基础配置来源。

多包 merge 需要 base registry。v0 允许使用当前 Demo 基础配置：

```text
RuntimeConfigSliceDemoData 等价数据
```

但必须把限制写清楚：

- CLI v0 只验证 demo/basic config merge。
- 后续真实项目需要通过 Project Manifest 指定 base config provider。
- 不在本任务接入真实 WGame 数据。

5. 输出和退出码。

退出码规则：

| 情况 | 退出码 |
| --- | --- |
| success=true，无 error | `AuthoringExitCodes.Ready` = 0 |
| success=true，但有 warning，且未传 `--fail-on-warning` | `AuthoringExitCodes.Ready` = 0 |
| success=true，但有 warning，且传 `--fail-on-warning` | 新增 `AuthoringExitCodes.Warning` = 5 |
| success=false，诊断流程完成但发现阻断问题 | `AuthoringExitCodes.ValidationBlocked` = 2 |
| 命令参数错误、文件 I/O 异常、工具自身异常 | `AuthoringExitCodes.ToolError` = 1 |

CLI stdout/stderr：

- 未传 `--output`：stdout 只输出完整 snapshot JSON，stderr 输出人类摘要。
- 传入 `--output`：完整 snapshot JSON 写文件，stdout 输出一行机器可读摘要，stderr 输出人类摘要。
- 人类摘要不得写入 stdout，避免污染机器读取。

6. 文档更新。

至少更新：

- `Docs/CAPABILITIES.md`
- `Docs/USAGE.md`
- 必要时更新 `Docs/AUTHORING_EDITOR_USAGE.md`

说明：

- 如何运行命令。
- 如何解释 `success/errors/warnings/summary`。
- 这个 CLI v0 只覆盖 demo/basic config，不是完整游戏 Mod 平台。

### 不做

- 不做 Web UI 按钮。
- 不做玩家导出 UI。
- 不做自动修复。
- 不做真实 WGame 数据接入。
- 不做 zip/签名/发布平台。
- 不做依赖关系和 load priority。
- 不新增 Unity 场景。

## 建议实现

### 1. 共享 DTO，不共享 Builder

Unity asmdef 不能被外部 dotnet SDK 项目直接 `ProjectReference`，而 Runtime Builder 的输入链路依赖 Catalog、Loadout、LoadPlan、MergeResult 等多个 Runtime 类型。v0 不应为了 CLI 抽动整条 Runtime 管线。

正确边界：

- DTO 共享：snapshot 数据模型唯一。
- Builder 分叉：Runtime Builder 消费 Runtime 对象；CLI Builder 消费 CLI 文件系统对象。
- JSON 分叉：Runtime 继续用 Newtonsoft.Json；CLI 继续用 System.Text.Json；通过测试保证 camelCase schema 一致。

### 2. 输出路径

`--output` 的父目录不存在时可以自动创建。

写文件应使用 UTF-8，无 BOM。

### 3. 参数解析

保持现有 CLI 风格，不引入大型命令行框架，除非项目已使用。

支持重复 `--container` 更适合脚本：

```bash
--container A --container B
```

也可兼容：

```bash
--containers A,B
```

### 4. AI Agent 友好

命令成功后建议输出一行机器可读摘要：

```json
{"success":true,"errors":0,"warnings":1,"output":".../mod_diagnostic_snapshot.json"}
```

但不要和完整 snapshot JSON 混在同一个 stdout 流里，除非未传 `--output`。

实际输出规则：

- `--output` 未传：stdout = 完整 snapshot JSON；stderr = 人类摘要。
- `--output` 已传：stdout = 单行机器摘要 JSON；stderr = 人类摘要；文件 = 完整 snapshot JSON。

## 验收标准

1. CLI 能在不打开 PlayMode 的情况下生成 `mx.modDiagnosticSnapshot.v1` JSON。
2. 默认容器目录为 `Assets/StreamingAssets/MxFramework/Demo`。
3. `--container` 重复参数和 `--containers` 逗号参数都支持多个目录。
4. 不传 `--loadout` 时按 all-valid 策略生成诊断。
5. 传入 `--loadout` 时按 loadout 启用状态生成诊断。
6. `--output` 可写入文件，父目录不存在时自动创建。
7. 不传 `--output` 时 stdout 是完整 snapshot JSON，stderr 是人类摘要。
8. 传入 `--output` 时 stdout 是单行机器摘要 JSON，完整 snapshot 写入文件。
9. `--pretty` 输出缩进 JSON。
10. success=false 且诊断流程完成时退出码为 `AuthoringExitCodes.ValidationBlocked`。
11. warning + `--fail-on-warning` 时退出码为新增 `AuthoringExitCodes.Warning`。
12. 命令参数错误或工具异常返回 `AuthoringExitCodes.ToolError`。
13. CLI 输出 schema 与 Runtime Snapshot schema 一致，并有一致性测试。
14. Snapshot DTO 在 Runtime 和 CLI 之间共享同一份源文件或同一纯 DTO 项目。
15. 自动化测试覆盖成功、missing loadout key warning、merge error、stdout 输出、文件输出、schema 一致性。
16. 实现不引用 `UnityEngine` / `UnityEditor`。

## 推荐测试

- `ModDiagnoseCommand_NoLoadout_WritesSuccessSnapshot`
- `ModDiagnoseCommand_WithLoadout_FiltersPackages`
- `ModDiagnoseCommand_OutputFile_CreatesParentDirectory`
- `ModDiagnoseCommand_Stdout_WritesSnapshotJson`
- `ModDiagnoseCommand_OutputFile_WritesSummaryToStdout`
- `ModDiagnoseCommand_FailOnWarning_ReturnsWarningExitCode`
- `ModDiagnoseCommand_MergeError_ReturnsValidationBlocked`
- `ModDiagnoseCommand_SchemaMatchesRuntimeSnapshot`
- `ModDiagnosticSnapshotDto_IsSharedByRuntimeAndCli`

测试数据优先使用临时目录，不依赖用户机器绝对路径。

## 完成定义

- 任务状态改为 `✅ 已完成（rXXXX）`。
- CLI 命令可运行并有测试。
- `Docs/CAPABILITIES.md` 更新外部工具能力。
- `Docs/USAGE.md` 写明命令示例和退出码。
- SVN 提交信息建议：

```text
Add mod diagnostic CLI export
```
