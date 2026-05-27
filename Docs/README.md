# MxFramework 文档索引

> 版本 0.6.61 | 2026-05-27
>
> 本目录定义框架的长期设计、接口边界、开发流程和验收标准。

---

## 快速入口

| 你需要 | 读这个 |
|--------|--------|
| 想知道框架当前能做什么 | `CAPABILITIES.md` |
| 开始接入和查看最小示例 | `USAGE.md` |
| 让 Agent 读取最小上下文 | `PROJECT_INDEX.md` |
| 查模块边界和公共 API | `INTERFACES.md`、`Interfaces/*.md` |
| 查开发流程、分支、PR 和验证 | `WORKFLOW.md` |
| 查完成定义和质量门禁 | `QUALITY_GATE.md` |
| 制作小游戏 / Demo / Runtime Showcase | `AGENT_GAME_CREATION_GUIDE.md` |
| 创作 Rendering pass / SharedRT / 材质绑定 / Volume 请求 / Demo 验证 | `RENDERING_AUTHORING_GUIDE.md` |
| 排查运行时状态和调试面板 | `Guides/OBSERVABILITY_DEBUGGING_GUIDE.md` |
| 追溯已完成任务的历史证据 | `Tasks/README.md` |

---

## 文档状态

| 状态 | 含义 | 使用规则 |
|------|------|----------|
| Current | 当前事实源 | Agent 和人工默认信任；变更后必须同步维护。 |
| Guide | 当前使用 / 排障指南 | 用于操作流程、接入步骤和手动验证；与源码冲突时回到源码和测试核对。 |
| Design | 设计说明 | 描述长期意图和边界；具体 API 事实以 `INTERFACES.md`、`Interfaces/*.md`、源码和测试为准。 |
| ADR | 已接受决策 | 保存不可轻易绕过的架构或流程决策。 |
| Archive | 历史证据 | 不代表当前事实；只在追溯、复盘或 Issue 明确引用时读取。 |
| Draft | 未接受草案 | 只能作为讨论材料，不能当成实现或验收依据。 |

---

## 当前事实源

| 状态 | 文档 | 用途 |
|------|------|------|
| Current | `PROJECT_INDEX.md` | Agent 最小上下文入口和默认读取边界。 |
| Current | `CAPABILITIES.md` | 当前可用能力清单。 |
| Current | `INTERFACES.md` | 接口索引、依赖矩阵和模块边界入口。 |
| Current | `Interfaces/*.md` | 各模块公共 API 和契约。 |
| Current | `WORKFLOW.md` | 日常开发、验收、提交、推送和 PR 流程。 |
| Current | `QUALITY_GATE.md` | 完成定义、验证要求和质量门禁。 |
| Current | `API_STANDARDS.md` | API 命名、兼容性、GC 和 Unity 依赖标准。 |
| Current | `RESOURCE_SYSTEM_WORKFLOW.md` | 当前资源系统主工作流。 |
| Current | `RENDERING_AUTHORING_GUIDE.md` | Rendering authoring 当前唯一入口。 |

---

## 指南和设计

| 状态 | 文档 | 用途 |
|------|------|------|
| Guide | `USAGE.md` | 最小接入示例和组合方式。 |
| Guide | `AGENT_GAME_CREATION_GUIDE.md` | Agent 制作小游戏 / Demo / Runtime Showcase 的强制规范。 |
| Guide | `Guides/OBSERVABILITY_DEBUGGING_GUIDE.md` | Debug UI、日志、timeline、hot reload、commands 和 Simulation Harness 排障。 |
| Guide | `Demo/CONFIG_DEMO.md` | Config Demo 内置源、字段示例和引用验证说明。 |
| Guide | `Demo/MX_ANIMATION_SYSTEM_SHOWCASE.md` | MxAnimation System Showcase 入口、手测流程和验收清单。 |
| Design | `DESIGN.md` | 项目目标、非目标和总体模块定位。 |
| Design | `ARCHITECTURE.md` | 架构契约、依赖方向、生命周期和错误处理。 |
| Design | `RUNTIME_FOUNDATION_SYSTEM.md` | Runtime Host、Frame/Command/Replay、SaveState 设计。 |
| Design | `RESOURCE_MANAGEMENT_SYSTEM.md` | 资源管理系统设计、加载契约和阶段切片。 |
| Design | `RESOURCE_DIRECTORY_LAYOUT.md` | 资源正式目录、ResourceKey 命名和 Catalog 归档规则。 |
| Design | `RENDERING_PIPELINE.md` | Unity 渲染管线基线和场景/材质验证规则。 |
| Design | `RENDERING_FRAMEWORK_DESIGN.md` | Rendering 总线、URP Feature、Context、SharedRT 和 Bridge 边界。 |
| Design | `COMBAT_ANIMATION_PHYSICS.md` | 动作战斗确定性动画 / 物理协作方案。 |
| Design | `CHARACTER_RESOURCE_PACKAGE_AUTHORING.md` | 角色资源包创作管线。 |
| Design | `CHARACTER_RESOURCE_PACKAGE_IMPLEMENTATION_PLAN.md` | 角色资源包工程落地方案。 |
| Design | `AUTHORING_EDITOR_PROGRAM.md` | 外部主创编辑器总规划。 |
| Guide | `AUTHORING_EDITOR_USAGE.md` | Buff 外部编辑器用法和限制。 |
| Guide | `AUTHORING_WORKFLOW.md` | 创作流程跨 Unity / Mod Editor / Authoring AI Assist / CLI 协作。 |
| Guide | `EDITORS.md` | Unity Editor 工具规范。 |
| Guide | `../Tools/MxFramework.EditorHub/README.md` | 外部编辑器中心入口。 |
| Guide | `../Tools/GiteaGithubSync/README.md` | Gitea Issue / PR 元数据手动镜像到 GitHub。 |

---

## 模块接口

| 文档 | 模块 |
|------|------|
| `Interfaces/Core.md` | Core 工具层 |
| `Interfaces/Events.md` | Events 事件总线 |
| `Interfaces/Attributes.md` | Attributes 属性存储 |
| `Interfaces/Buffs.md` | Buffs 生命周期 |
| `Interfaces/Modifiers.md` | Modifiers 修改器管线 |
| `Interfaces/Config.md` | Config 配置系统 |
| `Interfaces/Resources.md` | Resources 资源管理 |
| `Interfaces/Rendering.md` | Rendering URP 编排、Context、SharedRT、FeaturePipeline 和 Bridge 契约 |
| `Interfaces/AI.md` | Runtime AI Planner 轻量规划 |
| `Interfaces/Diagnostics.md` | Diagnostics 调试接口 |
| `Interfaces/Runtime.md` | Runtime Host / 生命周期调度 |
| `Interfaces/AppFlow.md` | App / Scene Flow 状态和场景切换 |
| `Interfaces/Input.md` | Unity Input System 上层输入意图、上下文和重绑定接口 |
| `Interfaces/CharacterControl.md` | Character Control 角色控制编排接口 |
| `Interfaces/CharacterApplication.md` | Character Application 角色配置聚合、resolver 和 diagnostics 接口 |
| `Interfaces/CharacterAction.md` | Character Action 角色动作层接口 |
| `Interfaces/Combat.md` | Combat 命中、动作、物理和结算接口 |
| `Interfaces/Gameplay.md` | Gameplay 运行时行为核心 |
| `Interfaces/DebugUI.md` | Debug UI source registry、snapshot aggregation 和 Toolkit overlay |
| `Interfaces/Story*.md` | Story core / runtime / bridge / Unity / editor 契约 |
| `Interfaces/Editor.md` | Editor 工具接口 |

---

## 归档和决策

| 状态 | 文档 | 用途 |
|------|------|------|
| ADR | `Decisions/*.md` | 已接受的架构、流程和版本控制决策。 |
| Archive | `Tasks/README.md` | 已完成任务文档的归档定位、读取规则和维护规则。 |
| Archive | `Docs/Tasks/*.md` | 历史任务规格、拆分和验收证据；不作为当前事实源。 |
| Archive | `Progress/CurrentStatus.md` | 当前运营状态快照和下一步；不承载模块事实。 |
| Archive | `MIGRATION.md` | 从 WGame 迁移了哪些代码、如何适配。 |
| Archive | `WGAME_*_AUDIT.md` | WGame 数据审计结果和迁移依据。 |
| Archive | `CONFIG_FORMAT_STRATEGY.md` | 配置格式策略与 Phase 9 契约输入。 |
| Archive | `Interfaces/ConfigSchemaSeeds.md` | Phase 9 首批 Schema 种子清单。 |
| Archive | `Interfaces/ConfigReferenceRulesPhase9.md` | Phase 9 引用规则白名单。 |

---

## 维护规则

- 当前事实只维护在本页“当前事实源”列出的 Current 文档、源码和测试中。
- `Docs/Tasks/` 默认不进入 Agent Context Pack；只有 Issue 明确引用、历史追溯或同类任务复盘时读取。
- 已完成任务文档不持续追更；任务产生的长期知识必须回写到对应 Current / Guide / Design / ADR 文档。
- 当 Design / Guide 与 Current 文档、源码或测试冲突时，以 Current 文档、源码和测试为准。
- Draft 文档必须在文件内标清状态；未接受前不能作为实现或验收依据。

---

## 版本历史（最近）

| 版本 | 日期 | 变更 |
|------|------|------|
| 0.6.61 | 2026-05-27 | 优化 `PROJECT_INDEX.md`：改为 Default Context Pack、Conditional Packs 和 Conflict Rule，明确 Agent 默认读取边界和按任务追加文档。 |
| 0.6.60 | 2026-05-27 | 瘦身 `README.md`：移除长任务清单和长版本表，新增文档状态分层，明确 Current / Guide / Design / ADR / Archive / Draft 的使用规则。 |
| 0.6.59 | 2026-05-27 | 降级 `Docs/Tasks/` 为历史任务归档：新增 `Docs/Tasks/README.md`，明确默认 Context Pack 不读取已完成任务文档，当前事实以能力、接口、流程、质量门禁、源码和测试为准。 |
| 0.6.58 | 2026-05-27 | 移除旧代码索引工具项目接入：删除独立工作流文档、仓库封装脚本和索引配置，当前流程改为 `git diff` / `rg` / 源码阅读 / 相关测试做影响面分析。 |
| 0.6.57 | 2026-05-25 | 文档同步：`INTERFACES.md` 模块表新增 Character Action 和 Character Runtime Spawn；`CAPABILITIES.md` 新增 Character Action 能力清单；`ROADMAP.md` 新增 Phase 16 Character Action Layer。 |

旧版本历史保留在 Git 历史中；当前入口只维护最近变更。
