# GitNexus 工作流

> 当前状态：已接入，作为代码理解、影响面分析和提交前辅助检查入口。

本文是本仓库 GitNexus 规则的唯一权威入口。其他文档只引用本文，不重复维护命令细节。

## 定位

- GitNexus 用于索引 MxFramework 代码库，辅助查询调用链、模块关系、影响面和当前改动风险。
- GitNexus 输出用于辅助判断，不替代编译、测试、Unity Console 检查和人工边界审查。
- 历史任务文档中的 GitNexus 验收记录保留为当时证据，不作为当前流程规范来源。
- GitNexus 自动生成的 `<!-- gitnexus:start -->` 指令块不作为本项目规范来源；如工具写入该块，应删除并回到本文维护规则。

## 工具入口

仓库内保留本地封装脚本：

```bash
Tools/GitNexus/gitnexus.sh
```

索引范围配置在仓库根目录 `.gitnexusignore`。它属于接入配置，应随文档和脚本一起维护；`.gitnexus/` 目录仍是本地索引缓存，不提交。

常用命令：

```bash
# 检查当前工作区改动影响面
Tools/GitNexus/gitnexus.sh detect-changes

# 如果索引过期或不可用，先重新分析。
# 封装脚本会自动追加 --skip-agents-md，避免 GitNexus 改写 AGENTS.md / CLAUDE.md。
Tools/GitNexus/gitnexus.sh analyze
```

如果命令名称随 GitNexus 版本变化，以 `Tools/GitNexus/gitnexus.sh --help` 输出为准，并同步更新本文。

## 使用时机

- 修改核心符号、公共 API、跨模块依赖、Runtime / Gameplay / Config / Combat 主流程前，优先做影响面分析。
- 提交前优先运行 `Tools/GitNexus/gitnexus.sh detect-changes`，确认影响范围和本次任务一致。
- 如果 GitNexus 提示索引过期，先重新分析，再依赖结果。
- 如果 GitNexus 当前不可用，不阻断紧急修复；最终说明原因，并用 `rg`、编译和相关测试补足风险确认。

## Agent 规则

- 代码探索、调试、影响分析、重构任务优先使用对应 GitNexus skill / MCP 查询，再回到源码核对关键证据。
- 不把 GitNexus 结果当作唯一事实；涉及行为正确性时必须读源码、跑测试或检查 Unity Console。
- 不把 `.gitnexus`、本地缓存、临时分析脚本提交到仓库，除非任务明确要求。

## 提交前检查片段

```bash
git status
Tools/GitNexus/gitnexus.sh detect-changes
```

如果 GitNexus 不可用，不能静默跳过；必须说明失败原因，并用 `git diff`、`rg`、源码阅读和相关测试做替代影响面分析。之后按任务范围运行相关编译、EditMode / PlayMode 测试或 Unity MCP / Console 检查。
