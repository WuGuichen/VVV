# 子需求 09：Authoring Editor 导出运行时 Patch 按钮

> **状态**: ✅ 已完成（详情见 `Docs/CAPABILITIES.md`）

## 目标

在 `AUTHORING_EDITOR_08_EXPORT_RUNTIME_CONFIG_PATCH.md` 完成后，把外部 Buff Authoring Editor 接到已验证的 CLI / Core 导出能力：用户点击 `导出运行时 Patch`，即可生成 `mx.runtimeConfigPatch.v1` 文件。

本任务只做 UI 和本地 API 调用，不重新实现格式转换逻辑。

## 前置条件

- `AuthoringRuntimePatchExporter` 已完成。
- CLI `runtime-patch export` 已可独立运行。
- sample Authoring package 可导出 Runtime Patch v1。
- Runtime Patch v1 可被 `RuntimeConfigPatchJsonLoader` 读取。

## 范围

必须做：

- 外部编辑器新增 `导出运行时 Patch` 按钮。
- Editor Server 新增或复用 API 调用 CLI / Core 导出能力。
- UI 显示导出路径、sourceId、layer、buff 数、modifier 数。
- 导出失败时显示字段级错误。
- 导出失败时不覆盖上一次有效 Runtime Patch 文件。
- 文档明确 `保存 Patch` 和 `导出运行时 Patch` 的区别。

不做：

- Runtime Patch v1 格式转换逻辑。
- Runtime Preview Scene Target。
- 完整 Mod 包格式。
- AI 自动修复。

## UI 语义

- `保存 Patch`：保存 Authoring 草稿，继续使用现有 `buff.patch.json`。
- `导出运行时 Patch`：生成 Runtime 可加载的 `mx.runtimeConfigPatch.v1`。

两个按钮不得合并，避免用户误以为保存草稿已经等于运行时可加载。

## 验收标准

- 打开外部编辑器后可看到 `导出运行时 Patch` 按钮。
- 点击按钮会生成 Runtime Patch v1 文件。
- 导出成功后 UI 显示输出路径和统计。
- 导出失败后 UI 显示字段级错误。
- 导出失败不覆盖上一次有效文件。
- 生成文件可被 CLI 或 Unity Runtime Patch Slice 加载。

## 状态

`Implemented (r1159)`
