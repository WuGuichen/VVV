# WGameFramework / MxFramework 工作流

> Status: Current
>
> 当前状态：以 NAS Gitea `origin` 为主仓库、任务入口和协作源；GitHub `github` 只作为非 LFS Git 镜像；SVN 只作为可选备份。

MxFramework 是 WGameFramework 仓库中的通用框架代号。

本文是项目日常开发、验收、提交和推送的统一入口。专项规则仍由对应文档维护：小游戏 / Demo 见 `AGENT_GAME_CREATION_GUIDE.md`，质量门禁见 `QUALITY_GATE.md`。

Gitea 在本项目中不是传统 Git 托管，而是 Agent Control Plane：

```text
Gitea = Spec Queue + Agent Sandbox + PR Audit Log + Harness Entry + Knowledge Update Trigger
```

核心流程：

```text
Spec Issue -> Agent Queue -> Context Pack -> Agent Branch -> Harness -> Self-review PR -> Human Gate -> Docs / ADR / Memory Update
```

`main` 必须保持稳定。日常开发不直接 push `main`，所有任务通过 Gitea Issue 和 Pull Request 进入主线。

## 1. 任务分流

开始前先判断任务类型，并读取对应文档：

| 任务类型 | 必读文档 | 输出要求 |
| --- | --- | --- |
| 框架能力 / 公共 API | `DESIGN.md`、`ARCHITECTURE.md`、`API_STANDARDS.md`、相关 `Interfaces/*.md` | 说明模块边界、API 影响、测试策略 |
| 小游戏 / Playable Demo / Runtime Showcase | `AGENT_GAME_CREATION_GUIDE.md` | 先给 API 复用计划，最终交付可打开、可操作、可验证入口 |
| 配置 / Authoring / 外部工具 | `AUTHORING_WORKFLOW.md`、`AUTHORING_EDITOR_PROGRAM.md`、相关接口文档 | 保持工具协议独立，不反向绑死 Unity Editor |
| 资源 / 场景 / Unity 资产 | `RESOURCE_MANAGEMENT_SYSTEM.md`、`QUALITY_GATE.md` | Unity 序列化资产优先通过 Editor / Unity MCP / 现有菜单生成 |
| 文档 / 流程 / 规范 | `README.md`、本文、相关专项文档 | 改权威入口，避免多处重复维护 |
| Bug / 回归 / 重构 | 相关模块接口和测试 | 先定位影响面，再做最小修复 |

## 2. 任务等级

不是所有任务都走同样重量的流程。先按风险分级，再选流程。

| 等级 | 适用任务 | 必需流程 | 可省略项 |
| --- | --- | --- | --- |
| S0 | 拼写、格式、纯文档小改、不改变规则 | `docs/*` 分支、`git diff --check`、PR | ADR、Harness、Unity 验证、完整 Agent Session |
| S1 | 单模块小改、局部文档补充、小工具修正 | Issue、Context Pack、分支、PR、相关验证 | ADR，除非公共 API 或规则变化 |
| S2 | 公共 API、跨模块依赖、Runtime / Gameplay / Config / Combat 主流程 | Spec Issue、影响面分析、接口文档同步、PR 审查 | 不可省略 Human Gate |
| S3 | 架构决策、Unity 资产 / 场景、Playable Demo、工作流 / 权限 / 自动化变化 | Spec Issue、ADR 或设计文档、Unity / Harness 验证、Human Gate、知识回写 | 不可省略 Docs / ADR 判断 |

S0 可以轻量执行，但仍不能直接改 `main`。S2 / S3 必须保留完整审计记录。

## 3. 标准开发循环

最小流程：

1. **Spec Issue**：写清目标、范围、不做什么、验收标准和任务等级。
2. **Context Pack**：列出必读文档、允许读取目录、禁止读取或修改范围。
3. **Agent Branch**：从最新 `main` 创建任务分支。
4. **Implement**：按任务边界修改代码或文档。
5. **Validate**：按任务等级运行最小有效验证。
6. **PR**：创建 PR，填写影响范围、验证和风险。
7. **Human Gate**：owner review 后合并。
8. **Docs / Memory 回写**：按需更新 Docs、ADR、Progress 或 memory。

高级流程按任务风险叠加：

1. **创建 Spec Issue**：Issue 不是普通工单，而是 Agent 可执行规格书。
2. **补齐 Context Pack**：Issue 必须列出必读文档、允许读取目录、禁止读取或修改范围。
3. **标记可执行**：只有带 `status/agent-ready` 的 Issue 才允许 Agent 开始。
4. **创建 Agent Session**：记录 Issue、分支名、上下文包、执行者和目标。
5. **创建分支**：从最新 `main` 创建 `feature/*`、`fix/*`、`docs/*` 或 `agent/*` 分支。
6. **读入口**：先读 `AGENTS.md`、`Docs/README.md`、本文件，再读 Issue 指定的上下文。
7. **确认边界**：说明本次会改哪些模块、不会碰哪些模块；涉及游戏功能时先写 API 复用计划。
8. **影响面分析**：使用 `git diff`、`rg`、源码阅读和相关测试确认影响范围。
9. **实现**：优先复用现有 Runtime / Input / Gameplay / Buffs / Attributes / Resources / Combat / UI Toolkit 能力。
10. **验证**：按任务风险选择最小有效测试集；Unity 相关改动至少确认 Console 无 error。
11. **文档同步**：公共 API、能力、工作流、验收口径变化必须同步文档；架构决策写入 `Docs/Decisions/`。
12. **自检和审计**：Agent 必须说明读取了哪些文件、为什么读、改了什么、影响哪些模块、是否改公共 API、是否需要 ADR。
13. **提交并推分支**：每个可验收阶段单独 Git commit，提交范围只包含本任务文件，推到 Gitea 分支。
14. **创建 PR**：按 `.gitea/PULL_REQUEST_TEMPLATE.md` 填写影响范围、测试结果、风险点、Agent Session 和 Docs / ADR 状态。
15. **Harness 检查**：Gitea Actions / Runner 执行轻量规则、测试或 Unity 验证。
16. **审查合并**：Gitea Actions 和人工 review 通过后合并到 `main`；Agent 不允许直接 merge。
17. **知识回写**：PR 合并后更新 `Docs/Progress/`、模块文档、ADR 或 memory，必要时创建后续 Issue。

## 4. Gitea 规则

Gitea 是任务、分支、PR、权限和自动化入口。

`main` 保护建议：

- 禁止直接 push。
- 禁止 force push。
- 禁止删除分支。
- 只能通过 PR 合并。
- Agent 不允许 merge PR。
- 初期可由项目 owner 自审，但仍必须经过 PR。

Issue 是 Agent Spec，必须包含：

- 目标
- 背景
- 修改范围
- 不做什么
- 相关模块
- 必读文档
- 允许读取目录
- 禁止读取或修改目录
- 验收标准
- 测试方式
- 是否允许修改公共 API
- Agent 约束

推荐标签：

```text
type/design
type/implementation
type/refactor
type/bug
type/test
type/docs
module/core
module/resource
module/input
module/audio
module/ui
module/scene
module/editor
status/spec-draft
status/agent-ready
status/agent-running
status/agent-failed
status/in-progress
status/needs-context
status/needs-human
status/needs-review
status/changes-requested
status/ready-to-merge
status/blocked
status/done
priority/high
priority/medium
priority/low
```

队列语义：

| 队列 | 标签 | 含义 |
| --- | --- | --- |
| Spec Queue | `status/spec-draft`、`status/needs-context`、`status/agent-ready` | 准备任务规格和上下文 |
| Execution Queue | `status/agent-running`、`status/agent-failed`、`status/needs-human` | Agent 执行或等待人工输入 |
| Review Queue | `status/needs-review`、`status/changes-requested`、`status/ready-to-merge`、`status/done` | PR 审查、修订和收口 |

`status/in-progress` 保留给人工开发；Agent 执行统一使用 `status/agent-running`。`status/ready-to-merge` 表示 Issue / PR 已准备进入 owner 合并判断，不等同于 Gitea PR review approval。

分支命名：

```text
feature/<issue>-short-topic
fix/<issue>-short-topic
docs/<issue>-short-topic
agent/<issue>-short-topic
```

示例：

```text
feature/23-fmod-audio-service
fix/31-resource-cache-handle
docs/45-gitea-workflow
agent/45-review-resource-provider
```

## 5. Agent 工作流

本节是第 3 节标准开发循环在 AI Agent 场景下的具体执行约束。

Agent 只能在受控分支中执行任务。默认流程：

1. 读取 Gitea Issue。
2. 读取 `AGENTS.md`。
3. 读取 `Docs/README.md` 和本文。
4. 读取 Issue 指定的模块文档和任务文档。
5. 列出计划、改动范围和需要读取的文件。
6. 创建或切换到 Issue 对应分支。
7. 修改代码或文档。
8. 检查 `git diff` 和影响面。
9. 推送分支到 Gitea。
10. 创建或更新 PR，并按模板说明影响范围。

Agent Session 必须留下审计记录：

- 读取了哪些文件。
- 为什么读取这些文件。
- 修改了哪些文件。
- 影响哪些模块。
- 是否修改公共 API。
- 是否需要更新 Docs / ADR。
- 实际运行了哪些检查。
- 未完成风险和后续 Issue 建议。

Agent 不允许：

- 在没有 Issue 或没有明确用户指令时开始大范围改动。
- 处理未标记 `status/agent-ready` 的 Gitea Issue。
- 默认扫描整个仓库。
- 读取或修改 `Library/`、`Temp/`、`Logs/`、`.codex/cache/`。
- 随意打开第三方插件源码；需要时必须说明原因和读取范围。
- 擅自修改公共 API 或跨模块大范围重构。
- 直接 push `main`。
- merge PR。
- 修改 Gitea 仓库设置、Protected Branch、权限或 token。

同一个 Issue 可以有多个 Agent 分支并行产出候选方案，例如：

```text
agent/23-audio-service-codex
agent/23-audio-service-claude
```

最终由 owner 比较 PR 并裁决采用哪个方案。

## 6. Context Pack

Agent 不应默认读全仓库。Issue 必须给出 Context Pack，Agent 按顺序读取：

```text
AGENTS.md
Docs/PROJECT_INDEX.md
Docs/README.md
Docs/Decisions/相关 ADR
Docs/Interfaces/相关模块.md
Issue 明确指定的活跃任务文档
当前模块源码 / 测试
```

`Docs/Tasks/` 是历史任务归档，不进入默认 Context Pack。只有 Issue 明确引用某个任务文档、需要追溯历史设计原因，或需要参考同类任务拆分时才读取。已完成任务文档不代表当前事实；当前能力、接口和流程以 `CAPABILITIES.md`、`INTERFACES.md`、`Interfaces/*.md`、本文、`QUALITY_GATE.md`、源码和测试为准。

`Docs/WORKFLOW.md` 是权威入口，但普通开发任务不要求全文读取；只有流程、权限、提交、PR、Harness 或 Agent 行为不清楚时才读取相关章节。

如果上下文不足，Agent 应先评论 Issue 或在回复中说明缺口，再扩大读取范围。

禁止把以下目录作为默认上下文：

```text
Library/
Temp/
Logs/
UserSettings/
.codex/cache/
Assets/Plugins/
Packages/第三方插件
```

## 7. 验证分层

验证不追求“一次跑全量”，但必须覆盖本次风险：

| 改动范围 | 最低验证 |
| --- | --- |
| 纯文档 / 流程 | `git diff --check`，必要时手动影响面分析 |
| 纯 C# Core / Runtime | 相关 EditMode / dotnet 测试，结合 `git diff` / `rg` 做影响面确认 |
| 公共 API / 跨模块依赖 | 手动影响面分析、相关模块测试，接口文档同步 |
| Unity Editor / Scene / Asset | Unity Console 无 error；资产与 `.meta` 成对；必要时用 Unity MCP / Editor 菜单生成 |
| UI Toolkit Demo | Play Mode 或等价验证关键 Label / Button 可见、文本非空、样式可读 |
| Playable Demo | 可打开场景 + 可操作流程 + 核心规则测试；只有代码和测试不能标记为 Playable |
| 配置 / Authoring | 配置校验报告、引用检查、相关工具或 CLI 验证 |

不能运行某项验证时，最终说明原因、风险和替代验证。

影响面分析默认使用 `git diff`、`rg`、源码阅读和相关测试；不能运行某项验证时，最终说明原因、风险和替代验证。

## 8. Harness / CI

传统 CI 只跑测试；Agent-native Harness 还要检查任务边界和审计信息。

第一阶段 Gitea Actions / Runner 检查：

- 是否提交 `Library/`、`Temp/`、`Logs/`、`UserSettings/`。
- 是否提交 `.csproj` / `.sln`。
- `.gitattributes` 是否存在。
- `Assets/` 下资源是否缺少 `.meta`。
- 是否修改 Issue 禁止目录。
- PR 是否填写 Agent Session、风险、验证结果和 Docs / ADR 状态。
- 公共 API 变化是否同步接口文档。
- 是否需要 ADR，或者明确说明无需 ADR。

后续再接：

- Unity EditMode Tests。
- Unity PlayMode Tests。
- 构建测试。
- 代码格式检查。
- 静态分析。
- Agent self-review / 安全扫描摘要。

Unity Runner 不建议跑在 NAS 上，应跑在装有 Unity Editor 的 Windows / macOS 开发机上。

Harness 检查项后续应落成脚本，并由 Gitea Actions 调用：

```text
Tools/Harness/check_generated_files.sh
Tools/Harness/check_unity_meta.py
Tools/Harness/check_pr_docs.py
Tools/Harness/check_public_api_changes.py
```

这些脚本应作为独立 Gitea Issue 推进，不和普通功能改动混在一个 PR。

## 9. 文档同步规则

只在权威入口维护规则，避免散落：

| 变化 | 更新位置 |
| --- | --- |
| 项目日常流程、提交、推送 | `WORKFLOW.md` |
| 架构、流程或版本控制决策 | `Decisions/*.md` |
| 当前进度和合并后摘要 | `Progress/*.md` |
| 新增可用能力 | `CAPABILITIES.md` |
| 新增接入方式 / 示例 | `USAGE.md` |
| 公共 API 变化 | `INTERFACES.md` 和对应 `Interfaces/*.md` |
| 模块边界 / 架构依赖 | `ARCHITECTURE.md`、必要时 `DESIGN.md` |
| API 命名、兼容、性能标准 | `API_STANDARDS.md` |
| 小游戏 / Demo 交付规则 | `AGENT_GAME_CREATION_GUIDE.md` |
| 验收标准变化 | `QUALITY_GATE.md` |
| 长期路线变化 | `ROADMAP.md` |

历史任务文档保留当时证据，不作为当前流程规范来源。
已完成 `Docs/Tasks/*.md` 不要求持续同步；任务完成时必须把仍然有效的能力、接口、流程或验收口径回写到上表对应的当前权威文档。

PR 合并后，必须判断是否需要回写：

- `Docs/Progress/CurrentStatus.md`
- `Docs/Decisions/*.md`
- `Docs/Interfaces/*.md`
- `Docs/CAPABILITIES.md`
- 后续 Gitea Issue
- agent memory

## 10. 提交前检查

分支提交前至少执行：

```bash
git status
git diff --stat
git diff --check
```

然后按任务类型补充验证：

- Unity 相关改动：确认 Console 无 error；能自动测试时优先跑相关 EditMode / PlayMode 测试。
- 配置相关改动：运行配置校验或导出提交前报告。
- 文档相关改动：运行 `git diff --check`。

确认只提交本任务文件：

```bash
git add <要提交的文件>
git commit -m "提交说明"
```

提交后推送当前分支到 Gitea：

```bash
git push -u origin <branch>
```

然后在 Gitea 创建 PR。不要直接推 `main`。

## 11. 推送和镜像

NAS Gitea `origin` 是主仓库、协作源和默认推送目标，保存完整 Git 内容和 LFS 对象：

```bash
git push -u origin <branch>
```

`main` 只通过 PR 合并更新。只有在仓库尚未启用 Protected Branch、且项目 owner 明确要求时，才允许临时直接 `git push origin main`。

GitHub `github` 不是协作源，不从 GitHub 拉取工作流决策，不在 GitHub 上处理 LFS 资产；它只作为非 LFS Git 镜像 remote：

```bash
git fetch origin
git push github refs/remotes/origin/main:refs/heads/main
```

本地 `.git/hooks/pre-push` 已为 remote 名 `github` 设置 `GIT_LFS_SKIP_PUSH=1`。推送 GitHub 时应看到：

```text
Skipping Git LFS upload for remote 'github'. Git refs will still be pushed.
```

如果 GitHub `main` 与本地分叉，且确认以本地为准：

```bash
git fetch origin
git push --force-with-lease github refs/remotes/origin/main:refs/heads/main
```

不要裸 `--force`。不要把 GitHub 当作 LFS 资产备份。

日常开发默认只要求把任务分支推到 Gitea，并通过 PR 合并。同步 GitHub 是额外镜像动作，不替代 Gitea PR。

### Issue / PR 元数据镜像

Issue 和 PR 不属于 Git 内容，不能靠 `git push` 同步到 GitHub。需要手动触发工具：

```bash
# 预览 Gitea Issue -> GitHub Issue
Tools/GiteaGithubSync/sync_gitea_to_github.py --issues

# 写入 GitHub
Tools/GiteaGithubSync/sync_gitea_to_github.py --issues --apply

# 同时镜像 PR 元数据
Tools/GiteaGithubSync/sync_gitea_to_github.py --issues --prs --apply
```

同步规则：

- 单向同步：Gitea -> GitHub。
- Gitea Issue 镜像为 GitHub Issue。
- Gitea PR 默认镜像为 GitHub Issue，并带 `mirror/pr` 标签。
- GitHub Issue body 内写入 `<!-- gitea-issue-id: N -->` 或 `<!-- gitea-pr-id: N -->`，后续运行按 marker 更新，避免重复创建。
- 不同步评论、review、approval、merge 权限、Protected Branch、自动化状态。
- 不要求 GitHub issue / PR 编号与 Gitea 一致。

真正把 Gitea PR 复刻成 GitHub PR 需要把源分支也推到 GitHub，并持续维护分支状态。当前项目不默认这样做，因为 GitHub 只是非 LFS `main` 镜像，不是协作源。

## 12. Webhook / Hermes

自动化先用于提醒、总结和检查，不用于决策和合并。

推荐接法：

```text
Issue 加 status/agent-ready
-> Webhook 通知 Hermes
-> Hermes 生成任务摘要或 Agent prompt
-> Hermes 生成 Context Pack
-> owner 确认
-> Agent 在分支执行
```

PR 创建后：

```text
PR created
-> Hermes 检查 PR 描述和 AGENTS.md 风险点
-> 评论摘要 / 风险
-> owner 人工 review
```

暂不做：

- Issue 创建后自动开工。
- PR 检查通过后自动 merge。
- Agent 自动删除分支。
- Agent 自动改 Gitea 设置。

## 13. 角色分工

```text
Gitea = Control Plane
Hermes on NAS = Orchestrator
Codex / Cursor / Claude on dev machine = Executor
Source / Tests / Review = Code Intelligence
Unity MCP = Unity Editor Tooling
Gitea Actions / Runner = Harness
Docs / ADR = Knowledge Base
```

原则：Agent 自主执行，人类控制边界。

## 14. 资产和本地文件边界

- 不提交 `Library/`、`Temp/`、`Logs/`、`UserSettings/`、`.codex/`、本地缓存和临时分析脚本。
- 新增 Unity 资产必须和 `.meta` 成对；优先通过 Unity Editor / Unity MCP / 现有菜单生成。
- 不手写 `.unity`、Prefab、ScriptableObject YAML，除非任务明确要求并说明风险。
- 不引入 WGame 真实业务数据、具体角色、元素体系、真实 Buff 配置或关卡逻辑。
- Runtime 代码不得引用 `UnityEditor`；Core / Config / Events / Attributes / Modifiers / Buffs 默认不引用 `UnityEngine`。

## 15. 备份策略

Gitea 成为主仓库后，必须备份：

- Gitea database
- repositories
- LFS objects
- `app.ini`
- SSH keys
- Gitea data 目录

建议节奏：

- 每日 NAS 快照。
- 每周 Gitea dump。
- 每月离线备份。
- 每月至少一次恢复测试。

恢复测试必须包含一次 Git LFS 文件拉取验证，确认大资源不是只有 Git pointer 文件。RAID 不是备份；可靠备份必须测试过恢复。

## 16. 收口标准

一个阶段可以标记为完成，必须同时满足：

- 改动范围和任务目标一致，没有混入无关文件。
- 相关验证已运行，或明确说明无法运行的原因和残余风险。
- 文档、代码、示例场景的描述一致。
- 影响面分析和验证范围符合预期，或已说明无法运行项的原因。
- 可玩任务有真实 Unity 入口；Runtime Slice / Framework Feature 不冒充 Playable。
- PR 包含 Agent Session 审计记录。
- 合并后需要的 Docs / ADR / Progress / memory 回写已完成或创建后续 Issue。
