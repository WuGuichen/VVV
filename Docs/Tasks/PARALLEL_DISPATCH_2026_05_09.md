# Parallel Dispatch Plan 2026-05-09

> **状态**: Active dispatch board
> **负责人**: Project lead / Codex
> **目标**: 把当前可并行推进的 P0/P1 工作拆成互不抢文件的任务包，并明确串行依赖。

## Dispatch Principles

- 子代理不共享写入范围，除非任务明确进入合流阶段。
- 已经有前置顺序的任务只先写文档，不提前派发实现。
- 每个子代理必须先读根目录 `AGENTS.md`、`Assets/AGENTS.md`、`Assets/Scripts/MxFramework/AGENTS.md`，再读自己的任务文档。
- 所有子代理都必须遵守：不回退他人改动，不清理无关 Unity / SVN / Git 噪音，不导入 WGame 真实业务数据。
- 验收记录写回各自任务文档；总进度由本 dispatch board 汇总。

## Active Parallel Lanes

| Lane | Task | Status | Primary Scope | Notes |
| --- | --- | --- | --- | --- |
| Preview B | `AUTHORING_EDITOR_10_03_5B_RUNTIME_CONFIG_RESOLVER_CLOSEOUT.md` | Completed | `Preview/Runtime`, `Tests/Preview` | Runtime Patch v1 -> merger -> config factories 已收口，`configMetadata` 可供 03.5C 使用。 |
| Preview C | `AUTHORING_EDITOR_10_03_5C_PREVIEW_RESULT_MAPPING.md` | Completed | `Preview/Runtime`, `Tests/Preview` | Runtime result mapping 已完成，`RuntimePreviewResult` 字段稳定。 |
| Preview E | `AUTHORING_EDITOR_10_03_5E_UI_STATUS_POLISH.md` | Completed | `Tools/MxFramework.Authoring*`, docs | 外部编辑器预览状态已收口，unavailable/success/failed/scene/dummy 可区分。 |
| UI M6 | `PHASE12_UI_TOOLKIT_M6_COMPONENTS.md` | Completed | `UI.Toolkit`, Showcase UXML/USS, Ability UI tests | M6 components / theme token 已完成，11/11 UI tests passed。 |
| AIAction P03 | `AI_ACTION_MIGRATION_PILOT_03_DRY_RUN_FIELD_MAP.md` | Completed | `Config`, `Tests/Config` | dry-run shape 已落地；Unity EditMode 曾被 Preview 编译错误阻塞，Preview B 后应可复跑。 |
| AIAction P04 | `AI_ACTION_MIGRATION_PILOT_04_DRY_RUN_REFERENCE_REPORT.md` | Completed | `Config`, `Tests/Config` | dry-run reference report 已完成；全量 build 曾被 Preview C 中间态阻塞，Preview C 完成后需最终复验。 |
| Phase11 Closeout | `PHASE11_RUNTIME_GAMEPLAY_CLOSEOUT.md` | Completed | Docs only | Phase 11 建议 Accepted / Closed，Ability+Config tests passed。 |

## Blocked / Follow-Up Lanes

| Lane | Task | Blocker | Start Condition |
| --- | --- | --- | --- |
| Preview C | `AUTHORING_EDITOR_10_03_5C_PREVIEW_RESULT_MAPPING.md` | None | Completed after Preview B stabilized `configMetadata`. |
| Preview E | `AUTHORING_EDITOR_10_03_5E_UI_STATUS_POLISH.md` | None | Completed after Preview C stabilized `RuntimePreviewResult`. |
| AIAction P04 | `AI_ACTION_MIGRATION_PILOT_04_DRY_RUN_REFERENCE_REPORT.md` | None | Completed after P03 reported dry-run model / report shape. |

## Merge Order

1. Merge Preview B before Preview C.
2. Merge Preview C before Preview E.
3. Merge AIAction P03 before AIAction P04.
4. UI M6 and Phase11 Closeout can merge independently if tests and docs pass.
5. Final project-state update should synchronize:
   - `Docs/README.md`;
   - `Docs/CAPABILITIES.md`;
   - `Docs/ROADMAP.md`;
   - parent task documents touched by completed lanes.

## Current Risk Register

| Risk | Mitigation |
| --- | --- |
| Preview lanes touching the same files | B, C, and E were run serially through explicit handoff points; no Preview lane remains blocked. |
| UI M6 overwriting existing UXML / USS local changes | Worker must read current files first and preserve unrelated changes. |
| AIAction pilot accidentally importing WGame data | Worker scope forbids real payloads and source-file emission. |
| Phase11 closeout discovering behavior bugs | Worker must stop and open a new implementation task rather than silently patch runtime behavior. |
| Dirty worktree hiding unrelated changes | Workers report changed files explicitly and do not run destructive cleanup. |

## Completion Rule

This dispatch is complete when active lanes either:

- finish with closeout notes and verification evidence; or
- report a blocker with a concrete follow-up task and no half-applied behavior change.

## Final Dispatch Result

Completed lanes:

- Preview 03.5B Runtime Config Resolver Closeout.
- Preview 03.5C Result Mapping.
- Preview 03.5E UI Status Polish.
- Phase 12 M6 UI Toolkit Components.
- AIAction Pilot03 Dry-Run Field Map.
- AIAction Pilot04 Dry-Run Reference Report.
- Phase 11 Runtime Gameplay Closeout.

Remaining follow-up:

- Browser screenshot smoke for external Authoring Editor preview panel.
- Optional final Unity EditMode rerun for AIAction dry-run tests after Preview C/E stabilized the full compile state.
- Decide whether AIAction dry-run fixture types should remain test-only or be promoted before Pilot05 source emission planning.
