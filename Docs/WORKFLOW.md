# MxFramework 工作流

> 当前状态：以 NAS Gitea `origin` 为主仓库和协作源；GitHub `github` 只作为非 LFS Git 镜像；SVN 只作为可选备份。

本文是项目日常开发、验收、提交和推送的统一入口。专项规则仍由对应文档维护：GitNexus 见 `GITNEXUS.md`，小游戏 / Demo 见 `AGENT_GAME_CREATION_GUIDE.md`，质量门禁见 `QUALITY_GATE.md`。

## 1. 任务分流

开始前先判断任务类型，并读取对应文档：

| 任务类型 | 必读文档 | 输出要求 |
| --- | --- | --- |
| 框架能力 / 公共 API | `DESIGN.md`、`ARCHITECTURE.md`、`API_STANDARDS.md`、相关 `Interfaces/*.md` | 说明模块边界、API 影响、测试策略 |
| 小游戏 / Playable Demo / Runtime Showcase | `AGENT_GAME_CREATION_GUIDE.md` | 先给 API 复用计划，最终交付可打开、可操作、可验证入口 |
| 配置 / Authoring / 外部工具 | `AUTHORING_WORKFLOW.md`、`AUTHORING_EDITOR_PROGRAM.md`、相关接口文档 | 保持工具协议独立，不反向绑死 Unity Editor |
| 资源 / 场景 / Unity 资产 | `RESOURCE_MANAGEMENT_SYSTEM.md`、`QUALITY_GATE.md` | Unity 序列化资产优先通过 Editor / Unity MCP / 现有菜单生成 |
| 文档 / 流程 / 规范 | `README.md`、本文、相关专项文档 | 改权威入口，避免多处重复维护 |
| Bug / 回归 / 重构 | `GITNEXUS.md`、相关模块接口和测试 | 先定位影响面，再做最小修复 |

## 2. 标准开发循环

每个可验收阶段按以下顺序推进：

1. **读入口**：先读 `Docs/README.md`，再读任务类型对应文档。
2. **确认边界**：说明本次会改哪些模块、不会碰哪些模块；涉及游戏功能时先写 API 复用计划。
3. **影响面分析**：按 `Docs/GITNEXUS.md` 使用 GitNexus；如果索引过期，先 `Tools/GitNexus/gitnexus.sh analyze`。
4. **实现**：优先复用现有 Runtime / Input / Gameplay / Buffs / Attributes / Resources / Combat / UI Toolkit 能力。
5. **验证**：按任务风险选择最小有效测试集；Unity 相关改动至少确认 Console 无 error。
6. **文档同步**：公共 API、能力、工作流、验收口径变化必须同步文档。
7. **提交**：每个可验收阶段单独 Git commit，提交范围只包含本任务文件。
8. **推送**：默认推 NAS Gitea `origin`；只有需要公开镜像时才推 GitHub。

## 3. 验证分层

验证不追求“一次跑全量”，但必须覆盖本次风险：

| 改动范围 | 最低验证 |
| --- | --- |
| 纯文档 / 流程 | `git diff --check`，必要时 GitNexus `detect-changes` |
| 纯 C# Core / Runtime | 相关 EditMode / dotnet 测试，GitNexus `detect-changes` |
| 公共 API / 跨模块依赖 | GitNexus 影响面分析，相关模块测试，接口文档同步 |
| Unity Editor / Scene / Asset | Unity Console 无 error；资产与 `.meta` 成对；必要时用 Unity MCP / Editor 菜单生成 |
| UI Toolkit Demo | Play Mode 或等价验证关键 Label / Button 可见、文本非空、样式可读 |
| Playable Demo | 可打开场景 + 可操作流程 + 核心规则测试；只有代码和测试不能标记为 Playable |
| 配置 / Authoring | 配置校验报告、引用检查、相关工具或 CLI 验证 |

不能运行某项验证时，最终说明原因、风险和替代验证。

## 4. 文档同步规则

只在权威入口维护规则，避免散落：

| 变化 | 更新位置 |
| --- | --- |
| 项目日常流程、提交、推送 | `WORKFLOW.md` |
| GitNexus 接入、索引、影响面检查 | `GITNEXUS.md` |
| 新增可用能力 | `CAPABILITIES.md` |
| 新增接入方式 / 示例 | `USAGE.md` |
| 公共 API 变化 | `INTERFACES.md` 和对应 `Interfaces/*.md` |
| 模块边界 / 架构依赖 | `ARCHITECTURE.md`、必要时 `DESIGN.md` |
| API 命名、兼容、性能标准 | `API_STANDARDS.md` |
| 小游戏 / Demo 交付规则 | `AGENT_GAME_CREATION_GUIDE.md` |
| 验收标准变化 | `QUALITY_GATE.md` |
| 长期路线变化 | `ROADMAP.md` |

历史任务文档保留当时证据，不作为当前流程规范来源。

## 5. 提交前检查

提交前至少执行：

```bash
git status
Tools/GitNexus/gitnexus.sh detect-changes
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

## 6. 推送模式

NAS Gitea `origin` 是主仓库、协作源和默认推送目标，保存完整 Git 内容和 LFS 对象：

```bash
git push origin main
```

GitHub `github` 不是协作源，不从 GitHub 拉取工作流决策，不在 GitHub 上处理 LFS 资产；它只作为非 LFS Git 镜像 remote：

```bash
git push github main
```

本地 `.git/hooks/pre-push` 已为 remote 名 `github` 设置 `GIT_LFS_SKIP_PUSH=1`。推送 GitHub 时应看到：

```text
Skipping Git LFS upload for remote 'github'. Git refs will still be pushed.
```

如果 GitHub `main` 与本地分叉，且确认以本地为准：

```bash
git push --force-with-lease github main
```

不要裸 `--force`。不要把 GitHub 当作 LFS 资产备份。

日常开发默认只要求 `git push origin main`。同步 GitHub 是额外镜像动作，不替代 Gitea 推送。

## 7. 资产和本地文件边界

- 不提交 `Library/`、`Temp/`、`Logs/`、`UserSettings/`、`.gitnexus/`、`.codex/`、本地缓存和临时分析脚本。
- 新增 Unity 资产必须和 `.meta` 成对；优先通过 Unity Editor / Unity MCP / 现有菜单生成。
- 不手写 `.unity`、Prefab、ScriptableObject YAML，除非任务明确要求并说明风险。
- 不引入 WGame 真实业务数据、具体角色、元素体系、真实 Buff 配置或关卡逻辑。
- Runtime 代码不得引用 `UnityEditor`；Core / Config / Events / Attributes / Modifiers / Buffs 默认不引用 `UnityEngine`。

## 8. 收口标准

一个阶段可以标记为完成，必须同时满足：

- 改动范围和任务目标一致，没有混入无关文件。
- 相关验证已运行，或明确说明无法运行的原因和残余风险。
- 文档、代码、示例场景的描述一致。
- GitNexus `detect-changes` 影响范围符合预期，或已说明不可用原因。
- 可玩任务有真实 Unity 入口；Runtime Slice / Framework Feature 不冒充 Playable。
