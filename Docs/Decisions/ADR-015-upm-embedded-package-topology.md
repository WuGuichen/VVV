# ADR-015: UPM Embedded Package 开发拓扑

> Status: ADR

Date: 2026-08-23

Decision Status: Accepted

## Context

MxFramework 当前把 package root 放在 `Assets/Scripts/MxFramework`，而外部 Consumer 安装后通过 `Packages/com.vincent.wgameframework` 读取同一内容。Editor 资源因此需要同时支持开发路径和 package 路径，Consumer 测试也必须专门捕获两种目录差异。

当前 package root 还同时包含 CharacterTest、仓库聚合测试、Demo 和项目导向 Editor。普通 CharacterTest 开发会改变 package tree、扩大外部安装面并持续使 immutable Consumer evidence 过期。仅增加更多路径 locator 或测试不能消除这两个结构性问题。

## Decision

MxFramework 的最终开发拓扑采用 Unity Embedded Package：

```text
Packages/com.vincent.wgameframework/
```

开发工程与已安装 Consumer 都使用 `Packages/<package-name>/...` 的 canonical asset path。仓库继续保持 monorepo；Embedded Package 不自动要求拆分 Git repository 或拆成多个 UPM package。

迁移必须分阶段完成：

1. 先建立 `upm-surface.json`，分类全部 package assembly 和 Editor initializer；
2. 把 CharacterTest、repository-only tests、主工程 Demo 和 project-only Editor 迁出 package root；
3. 在 package 内建立最小 Tests、Samples、Documentation 和 portable Editor surface；
4. 最后独立执行 `Assets/Scripts/MxFramework` → `Packages/com.vincent.wgameframework` cutover；
5. 更新 local `file:`、Git subpath、Consumer runner、文档和 CI；
6. canonical package asset path 验证通过后，删除开发路径 fallback。

目录 cutover 是 preview breaking change，必须提供 Consumer manifest migration note，并在同一候选上重新运行 Local/PinnedGit matrix。不得把 CharacterTest/Tests 大搬迁与最终 package root cutover 合并成一次不可审计变更。

本 ADR 不冻结长期 package ID、canonical repository URL、许可证或 adapter 拆包策略。这些决策必须在 cutover/release 前由 owner 单独批准；如果 package ID 改变，应以新 ADR 取代本文中的目录名。

## Consequences

- 开发工程和 Consumer 的资源路径最终一致，`MxFrameworkPackageAssetLocator` 的 `Assets/Scripts/MxFramework` 回退成为可删除迁移代码。
- CharacterTest 和仓库测试仍可验证框架，但不再随基础包安装，也不再让每个项目功能提交都扩大 package tree。
- 当前 local/Git subpath 在过渡期继续有效；cutover 后 Consumer 必须显式迁移。
- `.meta`、GUID、asmdef identity 和 public API 必须在每个移动切片中审计。
- 仅为目录美观进行的大规模 Runtime/Editor 二次嵌套不在本决策范围；每次路径变化仍需有独立收益和验证。
- Optional adapter 是否拆包继续由依赖、消费需求、版本节奏和发布责任决定，不由 Embedded Package 形式自动推导。

## Alternatives Considered

- 永久保留 `Assets/Scripts/MxFramework` 并维护双路径 locator：短期风险较低，但永久保留开发/消费形态差异，无法消除路径回退和相关测试成本。
- 立即整体移动当前 package root：会把 CharacterTest、聚合测试和项目工具一起带入 Embedded Package，且一次改变数千文件，风险不可审计。
- 立即拆独立 Git 仓库或多个 adapter package：在 surface owner、版本节奏和 release policy 尚未收敛前增加依赖和发布协调成本。

## References

- `Docs/UPM_CURRENT_STATUS.md`
- `Docs/UPM_OPTIMIZATION_ROADMAP.md`
- `Docs/UPM_PACKAGE_SURFACE_LAYOUT_OPTIMIZATION.md`
- `Docs/UPM_CONSUMER_BASELINE_EVIDENCE_OPTIMIZATION.md`
- `Docs/MULTI_PROJECT_WORKFLOW.md`
