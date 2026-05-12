# MxFramework Agent Guide

本仓库是从 WGame（万法裂隙）中提取的通用 Unity 游戏框架。Agent 在任何子目录工作时，先遵守本文件；进入更深目录后，再叠加最近一层 `AGENTS.md`。

## 项目定位

- 目标：沉淀可复用的游戏框架能力，不携带具体游戏业务。
- 引擎：Unity 6。
- 版本控制：Git 为主（NAS Gitea remote），SVN 保留备份。
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

1. 先读 `Docs/README.md`，再按任务读对应设计文档。
2. 涉及游戏功能、小游戏 / Demo / Runtime Showcase 时，先读 `Docs/AGENT_GAME_CREATION_GUIDE.md` 并写 API 复用计划。
3. 改代码前查找现有模式，优先用 `rg` / `rg --files`。
4. 涉及 Unity 项目状态、编译、场景、资源时，可以使用 Unity MCP。
5. 涉及代码影响面或提交前，先用本地搜索、编译和测试确认风险；GitNexus 当前不作为默认门禁。
6. 每个可验收阶段单独 Git 提交，标准提交流程：

```bash
git status
git add <要提交的文件>
git commit -m "提交说明"

# 推到 NAS Gitea（含 LFS 对象）
git push origin main

# 推到 GitHub（仅 refs + LFS pointer，pre-push hook 自动跳过 LFS 上传）
git push github main
```

7. 如只需同步 GitHub：

```bash
git push github main
```

8. 如 GitHub 提示非快进，确认本地覆盖：

```bash
git push --force-with-lease github main
```

9. 如需同步 SVN，再单独 `svn commit`。

## Git 推送模式

- `origin` 是 NAS Gitea 主 remote，日常提交后推送到 `origin/main`：`git push origin main`。
- `github` 是 GitHub 镜像 remote，只同步非 LFS 的 Git 内容：`git push github main`。
- 本地 `.git/hooks/pre-push` 已为 remote 名 `github` 设置 `GIT_LFS_SKIP_PUSH=1`，推送 GitHub 时应出现 `Skipping Git LFS upload for remote 'github'. Git refs will still be pushed.`。
- GitHub remote 使用专用 deploy key 和 SSH alias `github-wgameframework-vvv`；不要把 GitHub 当作 LFS 资产备份。
- 如果 GitHub `main` 与本地分叉，且确认以本地为准，使用 `git push --force-with-lease github main`，不要裸 `--force`。
- SVN 仅作为可选备份同步，非默认提交来源。

## GitNexus

本项目当前不视为已接入 GitNexus；`Tools/GitNexus/` 只作为后续重新接入的预留入口。重新接入前，不把 GitNexus 作为提交前强制门禁。

- 重新接入后，可用以下命令作为影响面辅助检查：

```bash
Tools/GitNexus/gitnexus.sh detect-changes
```

- 修改核心符号、公共 API 或跨模块依赖前，当前优先使用 `rg`、编译和相关测试确认影响面。
- GitNexus 重新接入后，其输出只用于辅助判断风险，不替代编译、测试和人工边界检查。
- 如果后续索引提示过期，必须先重新分析再依赖结果。

## 文档入口

| 文档 | 用途 |
| --- | --- |
| `Docs/README.md` | 文档索引和阅读顺序 |
| `Docs/DESIGN.md` | 项目目标、模块边界、非目标 |
| `Docs/ARCHITECTURE.md` | 依赖方向、生命周期、错误处理 |
| `Docs/USAGE.md` | 当前可用功能和接入方式 |
| `Docs/INTERFACES.md` | 接口文档入口和依赖矩阵 |
| `Docs/API_STANDARDS.md` | API 命名、兼容、性能规范 |
| `Docs/AGENT_GAME_CREATION_GUIDE.md` | Agent 基于框架开发游戏功能 / 小游戏 / Demo / Runtime Showcase 的强制规范 |
| `Docs/QUALITY_GATE.md` | 验收标准 |
| `Docs/ROADMAP.md` | 阶段路线 |
| `Docs/Tasks/` | 可执行任务拆分 |

## 提交前检查

- `git status` 确认只提交本任务文件。
- GitNexus 当前不作为默认门禁；重新接入后再运行 `Tools/GitNexus/gitnexus.sh detect-changes` 辅助确认影响范围。
- Unity 相关改动至少确认无 Console error；能自动测试时优先跑测试。
- 文档、代码、示例场景的描述要一致。
- Git 提交推送到 Gitea：`git push origin main`。
- 如需同步 GitHub 非 LFS 镜像：`git push github main`。
- SVN 仍可用作备份同步，非必须。
