# 子需求 02：Buff 外部编辑器 MVP

> **状态**: ✅ 已完成（详情见 `Docs/CAPABILITIES.md`）

## 目标

做出第一个外部主创工具垂直切片：用户能在外部编辑器中按 WGame 逻辑创建 Buff、校验、预览合并并导出 Mod Patch。

## 用户流程

```text
选择项目工作包
  -> 创建 Buff
  -> 选择中文类型
  -> 填公共字段
  -> 填堆叠和持续
  -> 填类型专属字段
  -> 配表现资源
  -> 检查引用
  -> 填多语言
  -> 预览合并
  -> 导出 Mod Patch
```

## 类型入口

UI 显示中文类型，底层映射到 WGame `BuffType`：

| 中文类型 | BuffType |
| --- | --- |
| 属性数值 | `Numerical` |
| 条件触发 | `Condition` |
| 改变属性 | `ChangeAttr` |
| 持续伤害 | `DamageByAttr` |
| 贝塞尔弹道 | `CastOrbBezier` |
| 追踪弹道 | `CastOrbTrack` |
| 直线弹道 | `CastOrbLinear` |
| 被动属性 | `Positive` |
| 状态控制 | `Status` |

## MVP UI

必须包含：

- 项目 / Mod 包选择。（已完成 v0.2，UI 顶部包下拉 + `/api/packages` 列出 samples）
- Buff 列表。
- 创建 Buff 向导。（已完成 v0.3）
- 当前步骤导航。
- 字段表单。（已完成 v0.1）
- 字段中文说明和原始字段名。（已完成 v0.1）
- 实时校验提示。（已完成 v0.2，UI 编辑 debounce 300ms 调用 `/api/validate-draft`）
- 引用选择 / 引用错误提示。
- Mod / Developer 模式切换。（已完成 v0.3，模式只改 UI，Mod 隐藏 `DamageBaseTypeID` 并强制 Layer=Mod）
- 多语言编辑区。（已完成 v0.3 可写：项目内置只读 + 包内追加 Patch，写入 `samples/<package>/patches/localization.patch.json`）
- 合并预览区。（已完成 v0.1）
- 导出按钮。（已完成 v0.1，调用 report bundle 写盘）

不做：

- 完整图编辑器。
- 复杂资源预览。
- 多人协作。
- Steam Workshop 或上传平台。

## Patch 输出

- 导出的 Buff Mod Patch 必须是 JSON Patch 包。
- 临时预览 Patch 和正式 Mod Patch 使用同一目录结构。
- `mod.json` 必须写入 `schemaVersion`、`packageId`、`kind` 和 `gameVersionRange`。
- 多语言修改写入 `localization.patch.json`，不直接覆盖 Base localization。

## 易用性要求

- 默认只显示常用字段。
- 高级字段折叠。
- 单位必须清楚，例如 `Duration` 和 `HitCooldown` 标明毫秒。
- 玩家模式不显示危险字段。
- 错误必须定位到步骤、字段和建议修复动作。
- 每一步都能让 AI 解释或补全。

## 验收标准

- 不打开 Unity 可以创建一个 Buff 草稿。
- 可以导出合法 Mod Patch。
- 常见字段错误能实时提示。
- 缺失引用能显示为阻塞问题。
- UI 文案中文为主，技术 key 保留英文副标。
- 生成的 Patch 可被 Authoring Core 合并预览。
- 单字段编辑到错误高亮目标 <= 200 ms。
- 全量 Buff 校验目标 <= 2 s。

## 依赖

- 子需求 01 Authoring Core / CLI。
- `Docs/WGAME_BUFF_AUTHORING_WORKFLOW.md`。

## 状态

`UI Integrated`

剩余项：AI 建议回写闭环（待 04 子需求）。

已完成 v0：

- 新增 `Tools/MxFramework.Authoring.Editor/`。
- 提供本地 Web UI Skeleton 和启动脚本。
- 读取 Project Authoring Manifest 样例。
- 读取 Buff Preview ModPackage 样例。
- 展示 Workflow 步骤、Buff 列表、Schema 字段、Enum、Reference、Localization、Validation Report 和 Merge Preview。
- 支持复制当前步骤 AI 上下文。
- 通过本地 Authoring API 编辑示例 Buff Patch 字段。
- 保存示例 Buff Patch 到 `samples/buff-preview/patches/buff.patch.json`。
- 调用 Authoring Core 重新生成 report bundle，并刷新校验和合并预览。
- Schema 字段增加分组、单位和 BuffType 可见性元数据。
- UI 按公共字段、目标 / 堆叠 / 持续、类型专属字段和表现资源分组显示。
- UI 根据当前 BuffType 动态显示需要填写的字段。
- Authoring Core Validator 会校验当前 BuffType 下可见的必填字段。

剩余：

- 接入 Runtime Preview。
- 接入 AI 服务。
- 接入 Unity Bridge 导出的真实项目 Manifest。
- 完善 Mod / Developer Mode 权限 UI。
- 将 `Values`、条件列表和弹道参数继续拆成更结构化的子编辑器。
