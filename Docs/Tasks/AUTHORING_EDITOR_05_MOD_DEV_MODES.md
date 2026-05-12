# 子需求 05：Mod Mode / Developer Mode 双模式

> **状态**: 📋 规划中（详情见 `Docs/CAPABILITIES.md`）

## 目标

同一个外部编辑器同时服务玩家 Mod 作者和开发者，但通过模式隔离能力、字段和风险。

用户能力分成两个维度：

| 维度 | 取值 |
| --- | --- |
| 用户权限模式 | Mod Mode / Developer Mode |
| 接入面 | UI / CLI / AI / Unity Bridge |

AI 和 CLI 不是独立权限模式，必须继承当前 Mod Mode 或 Developer Mode 的限制。

## Mod Mode

面向玩家和普通 Mod 作者。

默认能力：

- 使用安全模板。
- 创建 Mod 层内容。
- 编辑常用字段。
- 运行校验。
- 运行时预览。
- 导出 Mod 包。

限制：

- 不能修改 Base。
- 不显示危险字段。
- 不执行项目私有脚本。
- 不要求 Unity 或源码。
- 出错 Mod 可以被禁用，不影响游戏启动。

## Developer Mode

面向开发者和技术策划。

额外能力：

- 查看原始字段名和类型。
- 查看 Schema、Enum、Reference manifest。
- 查看 Base / Patch / Mod / Debug 合并关系。
- 查看 ChangeSet。
- 使用 Unity Bridge 导出项目索引。
- 生成 SVN 提交前报告。
- 接入运行时详细诊断。

限制：

- 修改仍必须经过校验。
- 不能绕过报告和提交流程。
- 不能把游戏真实业务数据提交进框架主干。

## 模式切换

模式切换不能改变数据本身，只改变：

- 可见字段。
- 可用动作。
- 校验严格程度。
- 报告详细程度。
- 导出目标。

## 安全策略

- Mod 包默认只包含数据描述，不包含可执行代码。
- Mod Mode 不允许执行项目私有脚本。
- 条件 DSL 或数值公式必须由 Authoring Core 白名单解释执行。
- 不允许反射、任意文件系统访问、网络访问或任意代码执行。
- Mod 可以引用 Manifest 中公开的 Base 对象，但不能引用未导出的内部对象。
- 资源路径必须通过 Manifest 白名单校验。

## 验收标准

- 同一个 Buff 草稿在 Mod Mode 和 Developer Mode 下可打开。
- Mod Mode 不能选择 Base 层。
- Developer Mode 能看到原始字段和合并结果。
- 模式权限能进入 AI 上下文，AI 不建议越权操作。
- 报告中明确记录当前模式和导出层。
- UI、CLI、AI 三个接入面都不能在 Mod Mode 下生成 Base 写入 Patch。

## 依赖

- 子需求 01 Authoring Core / CLI。
- 子需求 02 Buff 外部编辑器 MVP。

## 状态

`Draft`
