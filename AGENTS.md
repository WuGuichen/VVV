# MxFramework Agent Guide

本仓库是从 WGame（万法裂隙）中提取的通用 Unity 游戏框架。Agent 在任何子目录工作时，先遵守本文件；进入更深目录后，再叠加最近一层 `AGENTS.md`。

## 项目定位

- 目标：沉淀可复用的游戏框架能力，不携带具体游戏业务。
- 引擎：Unity 6。
- 版本控制：以 NAS Gitea `origin` 为主仓库；GitHub 仅作非 LFS Git 镜像；SVN 保留备份。
- 当前重点：先做可运行的运行时垂直切片，再继续外部编辑器、Mod、AI 辅助和复杂预览。

## 游戏功能开发强制入口

当任务是基于框架开发游戏功能，或制作小游戏、Playable Demo、Runtime Showcase、场景验证，或给现有 Demo 增加输入/UI/场景流/存档/回放能力时，必须先读取 `Docs/AGENT_GAME_CREATION_GUIDE.md`。

执行要求：

- 编码前先输出 API 复用计划，逐项说明会使用哪些 MxFramework 模块；任何绕过既有模块的决定都要说明缺口。
- 默认采用纯 C# 规则层 + `RuntimeHost` / command buffer + Unity 组合根 + UI Toolkit 视图的分层。
- 绕过既有 Runtime / Input / Gameplay / Buffs / Attributes / Resources / Combat / UI Toolkit 能力时，必须先说明缺口和替代方案。
- 如果任务目标是可玩 Demo，必须交付可打开、可操作、可验证的 Unity 入口；只有代码和测试不能标记为 Playable。
- 新增场景、Prefab、ScriptableObject 等 Unity 序列化资产时，优先通过 Unity Editor / Unity MCP / 现有 Editor 菜单生成，不手写 YAML。

## 目录职责

| 路径 | 职责 | 规则 |
| --- | --- | --- |
| `Assets/Scripts/MxFramework/` | 框架运行时、编辑器适配、Demo、测试 | 遵守该目录下的源码层规则 |
| `Assets/Scenes/` | 框架级示例场景和验证场景 | 不放 WGame 真实关卡或业务数据 |
| `Docs/` | 设计、接口、任务、路线图、验收记录 | 重要设计先更新文档，再实现代码 |
| `Tools/` | 外部工具、Authoring Core、CLI、辅助脚本 | 不反向依赖 Unity Editor |
| `Packages/` | 项目固定依赖包 | 避免依赖自动下载才能工作 |

## 不可越过的边界

- 不引入 WGame 游戏特化代码，例如具体角色、元素体系、关卡逻辑、真实 Buff 配置。
- 不依赖 Entitas、Luban 生成代码或 WGame 私有运行时。
- Runtime 代码不引用 `UnityEditor`。
- 纯 C# Core / Config / Events / Attributes / Modifiers / Buffs 默认不引用 `UnityEngine`。
- 外部主创工具、CLI、AI 接入层不能被 Unity Editor 绑死。
- 不提交本地工具缓存、个人插件状态、分析临时脚本，除非用户明确要求。

## 推荐工作流

项目日常开发、验收、提交和推送统一遵守 `Docs/WORKFLOW.md`。

- 先读 `Docs/PROJECT_INDEX.md` 和 `Docs/README.md`，再按任务类型读取 `Docs/WORKFLOW.md` 指定的专项文档。
- 涉及游戏功能、小游戏 / Demo / Runtime Showcase 时，先读 `Docs/AGENT_GAME_CREATION_GUIDE.md` 并写 API 复用计划。
- 涉及代码影响面或提交前，按 `Docs/GITNEXUS.md` 使用 GitNexus 辅助分析。
- 常规开发从 Gitea Issue 开始，只有 `status/agent-ready` 的 Issue 才允许 Agent 执行。
- Issue 是 Agent Spec，分支是 Agent sandbox，PR 是交付物和审计记录。
- 每个可验收阶段单独 Git 提交；分支、PR 和推送模式见 `Docs/WORKFLOW.md`。

## Git 推送模式

统一见 `Docs/WORKFLOW.md`。简述：`origin` 是 NAS Gitea 主仓库和默认推送目标；`github` 是跳过 LFS 上传的非 LFS Git 镜像；SVN 仅作可选备份。

## GitNexus

本项目已接入 GitNexus；具体规则统一维护在 `Docs/GITNEXUS.md`，其他文档不重复定义命令细节。

- 修改核心符号、公共 API 或跨模块依赖前，按 `Docs/GITNEXUS.md` 做影响面分析。
- GitNexus 输出用于辅助判断风险，不替代编译、测试和人工边界检查。
- 如果索引提示过期，必须先重新分析再依赖结果。

## 文档入口

| 文档 | 用途 |
| --- | --- |
| `Docs/README.md` | 文档索引和阅读顺序 |
| `Docs/PROJECT_INDEX.md` | Agent token-budget 入口和 Context Pack 读取顺序 |
| `Docs/DESIGN.md` | 项目目标、模块边界、非目标 |
| `Docs/ARCHITECTURE.md` | 依赖方向、生命周期、错误处理 |
| `Docs/USAGE.md` | 当前可用功能和接入方式 |
| `Docs/INTERFACES.md` | 接口文档入口和依赖矩阵 |
| `Docs/API_STANDARDS.md` | API 命名、兼容、性能规范 |
| `Docs/WORKFLOW.md` | 项目日常开发、验收、提交和推送流程 |
| `Docs/GITNEXUS.md` | GitNexus 接入、影响面分析和提交前辅助检查 |
| `Docs/Decisions/` | ADR 架构和流程决策记录 |
| `Docs/AGENT_GAME_CREATION_GUIDE.md` | Agent 基于框架开发游戏功能 / 小游戏 / Demo / Runtime Showcase 的强制规范 |
| `Docs/QUALITY_GATE.md` | 验收标准 |
| `Docs/ROADMAP.md` | 阶段路线 |
| `Docs/Tasks/` | 可执行任务拆分 |

## 提交前检查

统一见 `Docs/WORKFLOW.md` 的提交前检查和推送模式。
