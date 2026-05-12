# 子需求 01：Authoring Core / CLI 基础

> **状态**: ✅ 已完成（详情见 `Docs/CAPABILITIES.md`）

## 目标

建立脱离 Unity 的 Authoring Core，让外部编辑器、CLI、AI agent 和 Unity Bridge 共用同一套逻辑。

## 范围

必须实现：

- Workflow 加载和步骤上下文导出。（已完成 v0.1）
- Schema / Field / Enum / Reference 描述。（已完成 v0.2，SchemaField.IsList 支持多值 Reference）
- Draft / Base / Patch / Mod / Debug 层描述。（已完成 v0.1，LayeredMerger 实现 Base→Patch→Mod 合并并标注 OriginLayer）
- Patch 创建、覆盖、删除和合并预览。（已完成 v0.2，merge-preview 缺 base/patch 时统一走 LayeredMerger）
- Validator 规则框架。（已完成 v0.2，AuthoringValidate.Run 统一入口；PackageValidator 解耦内置 schema，必填字段由 manifest 推导）
- Report bundle 输出。（已完成 v0.1）
- CLI 最小命令集。（已完成 v0.2，新增 precommit 命令、editor serve 支持 --package、AI 上下文增加 enumSlice/referenceSummary/allowedActions）

不做：

- 外部 UI。
- 真实游戏运行时预览。
- 完整 Buff 字段编辑界面。
- 真实项目数据导入。

## 物理形态

Authoring Core 首版物理形态：

- 源码目录：`Tools/MxFramework.Authoring/`。
- 目标框架：`.NET Standard 2.1`。
- 产物：dll，供外部编辑器、CLI 和 Unity Bridge 引用。
- Unity 接入：通过 asmdef / asmref 或编译后 dll 引用同一份 Core，不复制实现。
- 禁止引用：`UnityEngine`、`UnityEditor`、WGame 项目程序集、真实项目业务数据。

强制约束：

- Core `.csproj` 中不得出现 Unity 或 WGame 项目程序集引用。
- CI / 提交前检查必须能在不打开 Unity 的情况下 `dotnet build` 通过。
- Unity Bridge 只能引用 Core，Core 不能反向引用 Unity Bridge。

## CLI 最小命令

```text
authoring schema export
authoring workflow list
authoring workflow context --workflow <id> --step <id>
authoring validate --package <path>
authoring merge-preview --package <path>
authoring report --package <path>
```

## 输入

- Workflow 定义。
- Schema / Enum / Reference manifest。
- 草稿或 Mod Patch。
- Localization manifest。

## 输出

- 校验报告。
- 合并预览。
- AI Step Context。
- Report bundle。

## 验收标准

- 不引用 `UnityEngine` 或 `UnityEditor`。
- CLI 能在不打开 Unity 的情况下完成校验和报告导出。
- AI 上下文可裁剪到单个 workflow step。
- Patch 合并结果可被外部编辑器展示。
- 有覆盖核心合并和校验的测试。
- `dotnet build` 能独立构建 Authoring Core。
- Tool Verified 必须同时满足 CLI 命令路径打通和自动化测试覆盖核心行为。

## 依赖

- 已有 `AuthoringWorkflow`。
- 已有 `ConfigSchema` / `ConfigAuthoring` / `RuntimeConfigPatchMerger` 基础能力。

## 状态

`Tool Verified`

已完成 v0：

- 新增 `Tools/MxFramework.Authoring/`。
- `MxFramework.Authoring.Core` 以 `.NET Standard 2.1` 独立构建。
- `MxFramework.Authoring.Cli` 提供 `schema export`、`workflow list`、`workflow context`、`validate`、`merge-preview`、`report`。
- `MxFramework.Authoring.Tests` 覆盖步骤上下文、Base 写入阻断和 Patch 合并。
- `samples/buff-preview` 提供 Buff Preview ModPackage 样例。
- `ProjectAuthoringManifest` 已包含 Schema、Enum、Reference、Workflow 和 Localization 索引。
- CLI 已支持 `manifest export` / `manifest inspect`。
- `samples/project-manifest/project-authoring-manifest.json` 提供外部编辑器可读取的项目工作包样例。
- CLI 已支持 `report --package <path> --out <dir>` 写出稳定报告包。

剩余：

- 与 Unity Bridge 导出的真实 manifest 对接。
