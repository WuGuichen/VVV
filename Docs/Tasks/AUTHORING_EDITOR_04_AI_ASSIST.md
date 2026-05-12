# 子需求 04：AI 辅助闭环

> **状态**: 📋 规划中（详情见 `Docs/CAPABILITIES.md`）

## 目标

让 AI 成为每个编辑步骤的辅助者，而不是让 AI 扫完整项目后猜配置。

AI 只读取当前步骤所需上下文：

- Workflow step context。
- Schema 和字段说明。
- 当前草稿。
- 校验报告。
- 引用摘要。
- 运行时预览日志。

## AI 能力

必须支持：

- 解释字段含义。
- 推荐 BuffType。
- 补全文案。
- 检查数值是否明显不合理。
- 根据校验报告生成修复建议。
- 根据运行时预览日志解释失败原因。
- 生成报告摘要。

不允许：

- 隐式修改 Base。
- 跳过校验。
- 执行项目私有脚本。
- 要求完整 Unity 项目源码。
- 输出大段与当前步骤无关的架构分析。

## AI 上下文包

建议包含：

```text
AiStepContext
  workflow
  step
  mode
  target
  schemaSlice
  enumSlice
  draftSlice
  validationIssues
  referenceSummary
  runtimePreviewSummary
  allowedActions
```

## UI 入口

每个步骤提供：

- `解释这个字段`
- `帮我填写`
- `检查当前步骤`
- `修复当前错误`
- `总结预览结果`

AI 输出必须以建议形式进入草稿，由人确认后应用。

## 验收标准

- AI 上下文可稳定导出。
- AI 能围绕单个步骤给建议。
- AI 建议能转成可审查的草稿变更。
- 应用 AI 建议后仍需运行校验。
- AI 报告可以进入导出报告包。

## 依赖

- 子需求 01 Authoring Core / CLI。
- 子需求 02 Buff 外部编辑器 MVP。
- 子需求 03 Runtime Preview 的日志摘要。

## 状态

`Draft`
