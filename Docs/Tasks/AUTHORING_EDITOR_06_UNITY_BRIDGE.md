# 子需求 06：Unity Bridge

> **状态**: 📋 规划中（详情见 `Docs/CAPABILITIES.md`）

## 目标

Unity Editor 只作为开发者桥接器，不作为主创工具。

它负责把 Unity 项目里的 Schema、Enum、资源索引、多语言索引和运行时验证入口导出给外部编辑器。

## 职责

必须支持：

- 导出 Project Authoring Manifest。
- 导出 Config Schema。
- 导出 Enum / Flags registry。
- 导出资源索引。
- 导出多语言索引。
- 导出引用索引。
- 启动或连接游戏运行时预览。
- 生成开发者提交前报告。

不做：

- 不承担完整 Buff 主编辑器。
- 不要求玩家使用 Unity。
- 不在 Unity 面板中复制外部编辑器全部 UI。

## 导出包

建议结构：

```text
ProjectAuthoringManifest/
  project.json
  schemas/
  enums/
  references/
  assets/
  localization/
  workflows/
```

## Unity 面板范围

Unity 中保留：

- Manifest 导出按钮。
- Schema / Enum / Asset 索引状态。
- 运行时预览连接状态。
- 提交前检查入口。
- Debug 日志和报告导出。

## 验收标准

- 外部编辑器可以只凭导出包启动项目编辑。
- 导出包不包含不该泄露的源码或私有实现。
- 资源索引能被外部编辑器用于引用检查。
- Unity Bridge 的输出能被 CLI 校验。
- 开发者可以在 Unity 中重新导出并刷新外部编辑器。

## 依赖

- 子需求 01 Authoring Core / CLI。
- 子需求 03 Runtime Preview。

## 状态

`Draft`
