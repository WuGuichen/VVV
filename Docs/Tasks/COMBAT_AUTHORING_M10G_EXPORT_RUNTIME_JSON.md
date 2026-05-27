# Combat Authoring M10G：Export Runtime Data and JSON v0

> **状态**：已完成
> **优先级**：P0
> 前置任务：`COMBAT_AUTHORING_M10F_PREVIEW_EXPLAIN.md`
> 派发对象：Combat Authoring / Export 子代理

## 目标

让 Combat Authoring 具备第一版可交付导出能力。开发者应能在 Unity Editor 中对当前 Action / Scene Binding 执行 validation gate，通过后生成稳定 JSON authoring package 草案，并得到可复制的 export report、manifest 和 hash。

## 范围

本阶段实现 M10G v0：

- 导出前调用 `CombatAuthoringValidator.Validate(...)`。
- 如果存在 Error，导出失败并返回清晰报告。
- 如果只有 Warning，可以导出，但报告必须包含 warning。
- 生成稳定 JSON package 文本，至少包含：
  - `manifest.json`
  - `schema/combat_authoring.schema.json`
  - `actions/action_<id>.json`
  - `scene_bindings/<binding>.json`
  - `reports/validation_report.txt`
  - `reports/validation_report.json`
- 生成 `CombatAuthoringManifest`，字段包含：
  - packageId
  - version
  - schema
  - schemaVersion
  - createdAt
  - toolVersion
  - sourceAssetGuid
  - contentHash
- 计算并报告：
  - authoringHash
  - runtimeDataHash
  - jsonPackageHash
  - contentHash
- Combat Authoring 窗口增加：
  - `导出 JSON`
  - `复制导出报告`

## 实现原则

- JSON key 必须是稳定英文，不依赖中文显示名。
- 导出逻辑放在 `MxFramework.Combat.Authoring`，便于后续 CLI / 外部 Authoring Editor 复用。
- Editor 层只负责 Unity Asset GUID、保存路径、按钮和展示。
- Runtime Core 不引用 `UnityEditor`。
- v0 可以生成 JSON 文本包和 report DTO；真正写多个文件到磁盘可放 Editor 层做最小实现。
- 输出顺序必须稳定，数组按显式 key 排序。

## 建议产物

可按实际代码风格命名，但建议包括：

```text
CombatAuthoringExportPackage
CombatAuthoringExportFile
CombatAuthoringExportResult
CombatAuthoringJsonExporter
CombatAuthoringExportReport
```

建议 API：

```text
CombatAuthoringExportResult Export(
    CombatActionAuthoringAsset action,
    CombatSceneBindingAsset binding,
    string packageId,
    string sourceAssetGuid,
    string toolVersion)
```

`ExportResult` 至少包含：

```text
bool Success
CombatAuthoringReport ValidationReport
CombatAuthoringManifest Manifest
CombatAuthoringExportContext Context
CombatAuthoringExportFile[] Files
string ReportText
```

## Editor UI 要求

- `Combat Authoring` 窗口新增按钮：
  - `导出 JSON`
  - `复制导出报告`
- 点击导出后：
  - validation 有 error：不写出 package，Report Preview 显示失败原因。
  - validation 无 error：生成 package 文本；可选让用户选择目录保存。
  - v0 如先不落盘，也必须在报告中列出将要生成的 file path 和 hash。
- 不允许只输出 Console。

## 非目标

- 不做 Runtime 加载。
- 不做 Play Mode Showcase 读取导出包。
- 不做外部 CLI。
- 不做完整 binary runtime snapshot。
- 不做真实 Mod 打包平台。

## 需要修改的主要文件

可能涉及：

- `Assets/Scripts/MxFramework/Combat.Authoring/`
- `Assets/Scripts/MxFramework/Combat.Editor/CombatAuthoringWindow.cs`
- `Assets/Scripts/MxFramework/Tests/Combat/Authoring/`

## 验收标准

- 有错误的 Action / Binding 导出失败，报告说明 validation gate。
- 合法 Action / Binding 导出成功。
- 生成文件列表包含 manifest、schema、action、scene binding、report。
- manifest 包含 version、schema、schemaVersion、contentHash。
- hash 稳定：同一输入导出两次，除了 createdAt 外内容 hash 不应漂移。
- JSON key 是英文稳定字段。
- `复制导出报告` 可以复制 report text。
- Unity Console 无 error。
- Authoring EditMode tests 通过。

## 测试要求

子代理完成后不要提交 SVN，由主代理复核和提交。子代理至少自查：

- Unity MCP refresh / Console error 检查。
- 相关 EditMode tests。
- 说明是否做过窗口手动验证。

## 完成记录

- Authoring 层新增 JSON export package / result / report / exporter，导出前强制执行 validation gate。
- Validation error 会阻断导出并返回失败报告；warning 允许导出并进入 report。
- 成功导出会生成内存 package 文件：manifest、schema、action、scene binding、validation txt/json。
- 生成并报告 `authoringHash`、`runtimeDataHash`、`jsonPackageHash`、`contentHash`；稳定内容 hash 不包含会随时间变化的 `createdAt`。
- `Combat Authoring` 窗口新增 `导出 JSON` 和 `复制导出报告`，Editor 层负责 GUID、目录写出、按钮和剪贴板。
- Unity MCP Console error 检查：0 error。
- EditMode tests：`MxFramework.Tests.Combat.Authoring` 11/11 passed。

## 提交边界

本任务只允许修改 Combat Authoring / Editor / Tests 相关文件和本任务文档状态。不要改动或提交以下未跟踪本地文件：

- `Assets/CombatActionAuthoringAsset.asset`
- `Assets/CombatSceneBindingAsset.asset`
- `.claude` / `Tools/*.py`
