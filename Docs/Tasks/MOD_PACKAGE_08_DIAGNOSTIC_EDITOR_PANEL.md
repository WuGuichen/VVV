# Mod Package 08：Diagnostic Editor Panel v0

> **状态**: ✅ 已完成（r1184）
> **优先级**：P0
> 前置任务：`MOD_PACKAGE_07_DIAGNOSTIC_CLI_EXPORT.md`
> 目标版本：Phase 10.12

## 目标

把 `mod diagnose` 的诊断结果接入外部 Authoring Editor，让开发者、策划、AI Agent 不用打开终端也能看到当前 Mod Package 组合是否健康。

目标链路：

```text
Authoring Editor
  -> EditorServer API
  -> mod diagnose / shared diagnose service
  -> Diagnostic Snapshot JSON
  -> Packages / Loadout / LoadPlan / Overrides / Issues panel
```

这一步不做完整玩家 Mod 管理器，只做诊断可视化和最小刷新入口。完成后，外部编辑器从“能编辑 Buff”推进到“能解释当前 Mod 组合为什么生效或失败”。

## 背景

当前已经完成：

- Runtime 侧 diagnostic snapshot。
- CLI 侧 `mod diagnose`。
- Snapshot JSON 包含 catalog、loadout、loadPlan、merge、errors、warnings、overrides。

但使用者仍要手动运行 CLI 或查看 JSON。对于后续外部编辑器、AI 辅助和 Mod 调试，需要一个直接的诊断面板：

- 当前发现了哪些包。
- 当前启用了哪些包。
- 哪些包被跳过以及原因。
- 哪些配置被覆盖，最终谁生效。
- 是否存在 warning/error。

## 范围

### 必须完成

1. EditorServer 增加诊断 API。

建议 API：

```text
GET  /api/mod/diagnose
POST /api/mod/diagnose
```

v0 可只实现一个，但必须满足：

- 可指定 `containers`。
- 可指定 `loadout`。
- 可指定是否 `includeAbsolutePaths`。
- 返回 `mx.modDiagnosticSnapshot.v1` JSON。

如果当前 EditorServer 更适合直接调用内部服务而不是启动 CLI 子进程，允许复用 CLI 诊断服务逻辑，但不要复制第三套诊断实现。

2. 诊断服务抽取。

07 如果把逻辑写在 `ModDiagnoseCommand` 内，本任务应抽出复用服务：

```text
ModDiagnosticService.BuildSnapshot(...)
```

要求：

- CLI 调用该 service。
- EditorServer API 调用同一个 service。
- CLI 只负责参数解析、stdout/stderr、退出码。
- EditorServer 只负责 HTTP 参数解析和 JSON 返回。

3. Web UI 增加诊断面板。

建议在现有外部编辑器中新增一个 tab 或侧栏区域：

```text
诊断
  Summary
  Packages
  Loadout
  Load Plan
  Overrides
  Issues
```

界面语言使用中文。

必须显示：

- `success`
- discovered / valid / invalid / enabled / ordered / skipped / overrides / errors / warnings
- packageKey / packageId / displayName / version / kind / state
- skipped reason
- loadout profileId / enabledPackageKeys
- override configType / id / packageChain / winner
- errors / warnings 的 code / source / message

4. 刷新入口。

UI 必须有一个明确按钮：

```text
刷新诊断
```

行为：

- 调用 EditorServer 诊断 API。
- 显示 loading 状态。
- 成功后更新面板。
- 失败后显示明确错误，不吞掉 HTTP/JSON 错误。

5. 默认配置。

v0 默认：

- container: `Assets/StreamingAssets/MxFramework/Demo`
- loadout: `Assets/StreamingAssets/MxFramework/Demo/mod_loadout.json`

UI 可暂时不提供复杂目录选择器，但应显示当前使用的 container/loadout。

6. AI 友好输出。

诊断面板应提供一个可复制的 JSON 区域或按钮：

```text
复制诊断 JSON
```

要求：

- 复制完整 snapshot JSON。
- 不要只复制 UI 摘要。
- 复制失败时给出错误提示。

7. 文档更新。

至少更新：

- `Docs/CAPABILITIES.md`
- `Docs/AUTHORING_EDITOR_USAGE.md`
- 必要时更新 `Docs/USAGE.md`

说明：

- 如何打开诊断面板。
- 每个区块怎么看。
- 诊断面板不修改 runtime 行为，只展示 snapshot。

### 不做

- 不做玩家 Mod 管理 UI。
- 不做启用/禁用包编辑。
- 不做自动修复。
- 不做冲突解决 UI。
- 不做真实 WGame 数据接入。
- 不做 zip/签名/发布平台。
- 不新增 Unity 场景。

## 建议实现

### 1. 先抽服务，再接 UI

不要让 Web UI 直接依赖 CLI 命令字符串。推荐分层：

```text
ModDiagnosticService
  <- ModDiagnoseCommand
  <- EditorServer API
  <- Web UI fetch
```

这样后续 AI Agent、自动测试、EditorServer 都能复用同一入口。

### 2. UI 布局

用紧凑工作台式布局，不做营销页：

- 顶部：状态摘要和刷新按钮。
- 左侧或上方：Packages / LoadPlan tabs。
- 右侧或下方：Issues / Overrides。
- JSON 原文放折叠区或独立 tab。

小窗口下不能横向溢出；表格需要滚动容器。

### 3. 颜色语义

- success: 绿色或默认正向状态。
- warnings: 黄色。
- errors: 红色。
- skipped/disabled: 中性灰。

不要只靠颜色表达状态，必须有文字标签。

### 4. 数据稳定性

UI 应直接渲染 snapshot 的稳定字段，不重新推断排序和状态。排序应该来自 snapshot：

- packages 按 snapshot 顺序。
- loadPlan 按 snapshot 顺序。
- overrides 按 snapshot 顺序。

## 验收标准

1. EditorServer 提供诊断 API，返回 `mx.modDiagnosticSnapshot.v1`。
2. CLI 和 EditorServer 复用同一个诊断 service。
3. Web UI 有“诊断”入口。
4. “刷新诊断”能拉取并展示 snapshot。
5. Summary 显示 success/errors/warnings/ordered/skipped/overrides 等统计。
6. Packages 列表显示 packageKey/packageId/kind/version/valid/enabled。
7. LoadPlan 列表显示 ordered/skipped 和 skipReason。
8. Overrides 列表显示 configType/id/packageChain/winner。
9. Issues 列表显示 severity/code/source/message。
10. JSON 区域或复制按钮能提供完整 snapshot JSON。
11. HTTP/JSON 错误会显示在 UI 中。
12. 小窗口下布局不越界。
13. 文档说明如何使用诊断面板。
14. 不引用 `UnityEngine` / `UnityEditor`。

## 推荐测试

- `ModDiagnosticService_BuildSnapshot_ReturnsExpectedFormat`
- `EditorServer_ModDiagnose_ReturnsSnapshotJson`
- `EditorServer_ModDiagnose_InvalidLoadout_ReturnsError`
- Web UI 手工验收：刷新成功、错误显示、复制 JSON、小窗口布局。

如果现有 Web UI 没有自动化测试框架，至少保留手工验收记录和截图。

## 完成定义

- 任务状态改为 `✅ 已完成（rXXXX）`。
- EditorServer API 可用。
- Web UI 诊断面板可用。
- `Docs/CAPABILITIES.md` 更新外部编辑器能力。
- `Docs/AUTHORING_EDITOR_USAGE.md` 说明诊断面板用法。
- SVN 提交信息建议：

```text
Add mod diagnostic editor panel
```
