# CharacterTest Runtime AI Lab UX And Layout Design

> Status: Current
>
> Date: 2026-07-31
>
> Parent: `CHARACTER_TEST_RUNTIME_AI_LAB_BEHAVIOR_WORKBENCH.md`
>
> Scope: Runtime AI Lab behavior workbench information architecture, layout, interaction states, and responsive acceptance rules. This document does not define draft JSON, compiler schemas, or published behavior formats.
>
> Single-behavior authoring authority: [单行为创作与组合预览设计](CHARACTER_TEST_RUNTIME_AI_LAB_SINGLE_BEHAVIOR_AUTHORING_PREVIEW_DESIGN.md).
>
> Behavior-recipe timeline authority: [行为配方大时间轴设计](CHARACTER_TEST_RUNTIME_AI_LAB_BEHAVIOR_RECIPE_TIMELINE_DESIGN.md).

## Single-Behavior Authoring Override (2026-08-07)

This document remains the reference for the implemented initial Setup surface, scene-first Runtime Test capabilities, full RuntimeFrame diagnostics, and historical responsive layouts. It is no longer the product authority for the default single-behavior authoring surface.

For single-behavior authoring, the linked authority above supersedes every later statement that requires:

- one unified `编辑逻辑 / 行为计划 / 运行轨迹` three-tab Workbench;
- a formal Behavior Group, Player Deck, Facts, A/B, branch-recompute, or Replay surface in the primary authoring flow;
- the full five-track RuntimeFrame projection as the always-visible behavior preview;
- `Apply F+1` as the authoring-preview commit action;
- opening or editing a formal Behavior Group from the behavior editor.

The target authoring shell instead edits exactly one project Behavior Card Draft, keeps a secondary local test composition, opens a Setup-logic variant for both lineups, and uses a compact draggable RuntimeFrame preview timeline. The full capabilities documented below remain reusable advanced or compatibility implementations; they must not be reintroduced as first-version authoring UI.

Implementation note: the phase-driven top experiment bar, scene-first center
area, right-side drill-down workspace, behavior-group selection, retained frame
records, structured five-track RuntimeFrame timeline, scrub/Jump/Latest/Advance/
A-B controls and Warm Replay are implemented. The launcher remains in the
legacy HUD package, while the main Lab panel is an allowlisted Composer-owned
`MxCharacterTestRuntimeAiLabUi` surface that has been Applied, Published,
registered and selected by the runtime. Logic v2 drafts support copy-on-write
editing, structured Inspector mutations, priority and layout dragging,
validation/conflict feedback and Undo/Redo. Immutable candidate revisions apply
at `F+1`; typed behavior events and per-frame logical state snapshots drive
transition-to-frame navigation and historical Warm Replay.

The current build exposes Setup, Behavior Card and Runtime Test routes; the
former Behavior Group editor has been retired. The v2 target deliberately converges the remaining routes to two
full-screen contexts:

```text
Runtime AI Lab Shell
├─ Setup / 实验准备（Unity Play 默认）
└─ Workbench / 工作台
   ├─ 3D scene + Agent + 感知/判断/行为/结果
   ├─ 运行轨迹 / RuntimeFrame timeline
   ├─ 行为计划 / Behavior Timeline v2
   └─ drawers（Player Deck / 卡库 / advanced details）
```

Starting a test enters one persistent Workbench. Selecting `运行轨迹` or
`行为计划` replaces only the lower timeline mode; it preserves scene, camera,
Agent, selected/latest frame, playback and right-column context. Player Deck
and card selection open as drawers rather than pages. Setup selects a complete
behavior group and never opens an entry editor. The current separate Behavior Card route and the former `Facts`,
`RuntimeFrame`, and `Replay` routes remain compatibility implementations until
that migration lands.
CharacterTest-local Behavior Loadout / Player Deck selection and append-only
local Publish are implemented elsewhere in the same Lab flow. Formal
classification, collaborative export/process locking, production catalog
approval/signing/distribution/rollback, and account/cloud/multiplayer
integration remain outside this delivery boundary.

The first-phase runtime-test convergence below is implemented. The unified
Behavior Timeline v2 sections describe the next UI target and must not be read
as already delivered. Here `v2` means Workbench composition and visual
projection: Logic v2 timing, editing gestures, Draft/Undo, runtime identity,
historical evidence, conflict detection and RuntimeFrame navigation are existing
inputs, not a second implementation backlog.

## Runtime Test Single-Surface Convergence (Phase 1 Implemented)

The runtime-test flow is function-oriented rather than workspace-oriented. Its
default surface keeps the 3D scene, current causal chain, and RuntimeFrame
timeline visible together. Facts, arbitration evidence, action details, replay,
and validation are opened from that surface as contextual drill-downs and must
return to the same Agent, frame, viewport, playback state, and timeline
selection.

The following approved mockup remains the phase-1 design reference for the
implemented runtime-test default surface. It preserves the large type,
restrained information density, scene-first layout, right decision summary and
opaque expanded timeline. Behavior Timeline v2 reuses this shell and replaces
only its lower timeline mode.

![Runtime AI Lab runtime-test single-surface phase-1 target](../Images/CharacterTest/runtime-ai-lab-runtime-test-single-surface-phase1-target.png)

### Phase 1 Visible Integration

| Area | Phase-1 behavior |
| --- | --- |
| Agent selector | The top-left `T1-1` badge is a selector, with a visible drop-down cue. Changing Agent refreshes the scene selection, decision cards, and timeline projection together. |
| Frame status | The persistent status uses `状态 · 帧 selected/capturedLimit`, for example `已暂停 · 帧 93/900`. It must not hide whether the user is reviewing history or following Latest. |
| Lifecycle controls | Use one stateful Play/Pause control, one `推进 +1` control, Restart, and Back/Exit. Do not show separate Play and Pause buttons simultaneously. Restart starts a new test transaction; it must not be implemented as Resume. |
| Current causal chain | Keep four large cards: `感知`、`判断`、`行为`、`结果`. A trailing chevron makes every card a drill-down entry without adding another row of buttons. |
| Truthful result | Before an enqueued command has execution evidence, show `已入队 · 尚未执行` with pending/neutral treatment. Green success is reserved for an observed successful outcome. |
| RuntimeFrame timeline | Keep the five semantic tracks and direct zoom/collapse/fit controls. Playback speed is a persistent compact control beside the timeline transport and displays the active multiplier. Low-frequency Latest, Jump, A/B, Loop, replay, and validation commands enter one `…` menu instead of expanding the primary toolbar. |
| Historical branch action | Do not show `从此重算` or equivalent branch affordance while `selectedFrame == latestFrame`. It appears contextually only during historical review, with the existing truncation warning and confirmation semantics. |

### Drill-Down Integration Map

| Default-surface entry | Integrated detail | Return invariant |
| --- | --- | --- |
| `感知 / 最近目标` | Semantic Facts used by the selected decision, including target, availability, distance, visibility, and relevant changes | Return to the same Agent and selected frame |
| `判断 / 距离 14.0m` | Eligible candidates, rejected candidates, winner, priority/score, and the decisive conditions | Return to the same decision card and timeline selection |
| `行为 / 撤退` | Active behavior card, stage, action request, interruption/pending state, and related timeline events | Return without changing simulation state |
| `结果 / 已入队 · 尚未执行` | Command enqueue, execution observation, failure/cancellation, and correlated RuntimeFrame evidence | Never infer success from enqueue alone |
| Timeline event or `…` | Event details, Latest/Jump/A-B/Loop, Warm Replay, validation, and advanced diagnostics | Preserve the viewport and restore the prior default surface on close |

Phase 1 does not place a full Facts table, full Replay page, behavior-card
editor, raw command history, hash/id dump, or advanced validation panel on the
default surface. Those capabilities remain contextual drawers/overlays or
advanced menu destinations. They must not reintroduce permanent side tabs,
multi-column dashboards, or duplicated navigation.

Phase-1 acceptance covers:

- starting a test opens this default runtime-test surface directly;
- the primary path does not require switching among `Facts`, `RuntimeFrame`,
  and `Replay` top-level tabs;
- every causal card is actionable and its detail is sourced from typed runtime
  evidence rather than UI text parsing;
- Play/Pause, `推进 +1`, Restart, Back/Exit, Agent selection, frame progress,
  and timeline overflow remain reachable at 1280x720 and 1920x1080;
- closing any drill-down restores the exact Agent, selected frame, Latest
  relationship, playback state, viewport, and event selection;
- the base screen keeps large readable type and does not grow additional
  persistent panels.

## Historical Four-Workspace Validation Captures

The following captures record the earlier four-workspace implementation at
2560x1440 after Composer Apply/Publish, runtime binding, Unity compilation and
focused workspace navigation validation. They are retained as historical
evidence and do not represent the unified v2 Workbench navigation target.

### Setup / 实验准备

![Current Runtime AI Lab Setup workspace](../Images/CharacterTest/runtime-ai-lab-current-setup-workspace.png)

### Behavior Card / 行为卡时间轴

![Current Runtime AI Lab behavior-card workspace](../Images/CharacterTest/runtime-ai-lab-current-behavior-card-workspace.png)

### Diagnostics / RuntimeFrame

![Current Runtime AI Lab RuntimeFrame diagnostics workspace](../Images/CharacterTest/runtime-ai-lab-current-diagnostics-runtimeframe.png)

## Historical Workbench Reference

The following earlier image remains as historical design input. It is not the
v2 navigation or layout acceptance baseline because it still implies a separate
behavior-card workspace.

![Runtime AI Lab behavior workbench target](../Images/CharacterTest/runtime-ai-lab-behavior-workbench-target-v2.png)

### Unified Behavior Plan Authoring Standard

The authoritative interaction, visual and acceptance contract is
[Unified Behavior Timeline v2 complete design](CHARACTER_TEST_RUNTIME_AI_LAB_BEHAVIOR_TIMELINE_V2_DESIGN.md).
Its two effect images cover normal authoring and blocking conflict/A-B
comparison inside the current Runtime Test shell. They supersede the separate
Behavior Card page, dedicated-editor mockups and earlier v2 compositions
wherever layout or navigation conflicts. They are target mockups rather than
captures of the current FairyGUI implementation.

![Runtime AI Lab unified workbench plan mode v2 target](../Images/CharacterTest/runtime-ai-lab-unified-workbench-plan-mode-v2-target.png)

![Runtime AI Lab unified workbench plan conflict and comparison v2 target](../Images/CharacterTest/runtime-ai-lab-unified-workbench-plan-conflict-compare-v2-target.png)

The earlier v2 composition remains checked in as historical design input:

![Historical Runtime AI Lab behavior timeline v2 composition](../Images/CharacterTest/runtime-ai-lab-behavior-timeline-authoring-standard-v2.png)

The required information hierarchy is:

- the product has only `Setup` and one persistent Workbench as full-screen
  contexts;
- `运行轨迹` and `行为计划` reuse the same lower primary workspace rather than
  stacking timelines or navigating to another page;
- the upper 3D scene, selected Agent, selected/latest frame, camera, playback
  and causal decision cards remain unchanged when the lower mode switches;
- Player Deck and card library are dismissible drawers or
  pickers, not permanent columns;
- the right context column keeps the causal chain above and reuses its lower
  section as the selected plan item/conflict Inspector;
- the plan timeline has seconds/frame rulers, zoom, snapping, a logical-time
  cursor and horizontal scrolling;
- no thumbnail, minimap, overview strip or reserved spacer is present; all lower
  height is assigned to the six primary tracks.

Horizontal length now has explicit authoring meaning. Stage spans, detector
activation windows and primary-action clips show planned logical duration.
Clip bodies move start time, clip edges edit duration, and `Until Event` or
infinite spans use an explicit continuation treatment. Event-driven transitions
may still interrupt, defer or jump to another stage; the cursor is not required
to traverse every planned interval before a transition commits.

The Runtime Overlay track compares planned duration with the most recent actual
execution without changing behavior semantics. Exact chronological evidence,
typed markers and frame-by-frame history remain owned by `运行轨迹` in the same
workbench.

The standard must preserve these editing and reading affordances:

- add, select, multi-select, move, resize, duplicate, delete and reorder;
- detector priority editing and card/stage scope distinction;
- transition creation, readable forward/back/loop routing and policy editing;
- planned versus actual duration, conflict hatching and blocking diagnostics;
- Undo/Redo placement, validation, semantic status and `Apply F+1`;
- track labels that remain visible while horizontally scrolling, plus
  `Fit selection` and `Fit card` for long behavior cards;
- A/B evidence rendered inside existing tracks rather than in a separate bottom
  strip.

The former `v1` images remain checked in as historical design exploration and
usage explanation, but they are no longer visual acceptance baselines:

![Historical Runtime AI Lab logical timeline workbench](../Images/CharacterTest/runtime-ai-lab-behavior-logic-workbench-target-v1.png)

![Historical Runtime AI Lab logical timeline editor](../Images/CharacterTest/runtime-ai-lab-behavior-logic-editor-target-v1.png)

The earlier authoring interaction guide remains useful for selection and
detector-priority examples, but its stage-column positioning rule is historical.
Under the unified v2 contract, vertical detector reordering changes semantic
priority and horizontal Stage movement changes semantic planned start time.
Only transition route geometry, label offset and other explicit Layout fields
remain presentation-only:

![Runtime AI Lab behavior logical timeline editing guide](../Images/CharacterTest/runtime-ai-lab-guide-behavior-logic-editing-v1.png)

The runtime linkage guide fixes how the logical cursor, simultaneous detector
arbitration, pending transition, typed markers, and historical frame selection
must correspond across the two timelines:

![Runtime AI Lab behavior logical timeline RuntimeFrame linkage guide](../Images/CharacterTest/runtime-ai-lab-guide-behavior-logic-runtime-link-v1.png)

Visual semantics for the `v2` standard and retained usage guides are:

- cyan means current stage, selection, or committed logical path;
- amber means detector hit or a semantic-edit warning;
- violet means transition strategy, interruption, or pending state;
- red hatching is reserved for action-ownership conflicts and blocking diagnostics;
- gray means inactive, unarmed, or contextual information.

Generated labels are explanatory artwork. Stable child names, command ids,
schema fields, diagnostic codes, and final localized copy remain authoritative
in source and the contracts in this document.

### Usage Instruction References

The following annotated images explain the intended reading and operation flow.
They are guidance artifacts, not captures of the current implementation.

![Runtime AI Lab interface zones guide](../Images/CharacterTest/runtime-ai-lab-guide-interface-zones.png)

![Runtime AI Lab timeline review guide](../Images/CharacterTest/runtime-ai-lab-guide-timeline-review.png)

![Runtime AI Lab draft apply guide](../Images/CharacterTest/runtime-ai-lab-guide-draft-apply.png)

### Reactive Behavior Group Target

The following `v4` references are authoritative for the first gameplay-level
reactive behavior-group slice. They describe one group named `追击与闪避` with
two cards that edit and execute the same Runtime AI Planner payload:

![Runtime AI Lab reactive dodge target](../Images/CharacterTest/runtime-ai-lab-reactive-dodge-target-v4.png)

![Runtime AI Lab reactive group guide](../Images/CharacterTest/runtime-ai-lab-guide-reactive-group-v4.png)

![Runtime AI Lab threat window guide](../Images/CharacterTest/runtime-ai-lab-guide-threat-window-v4.png)

![Runtime AI Lab arbitration timeline guide](../Images/CharacterTest/runtime-ai-lab-guide-arbitration-timeline-v4.png)

The group contract is fixed:

- `持续靠近目标` is the base card. It remains eligible while a target exists,
  a movement path is available, and the target is outside the preferred stop
  distance.
- `威胁闪避` is a priority-100 reactive card. It becomes eligible only when the
  selected target is running a CharacterAction with a future hit-trace start,
  that impact is predicted to intersect the observing Agent, the impact is
  inside the configured reaction window, and the same attack instance has not
  already consumed a response.
- A successful reaction submits the existing CharacterTest dodge control
  intent and chooses a deterministic safe lateral direction. It does not play
  an animation directly and does not bypass CharacterAction cancel rules.
- Each observed attack instance can trigger at most one dodge submission. A
  cooldown prevents adjacent attacks from producing a per-frame response loop.
- After dodge acceptance, rejection, or action completion, the group returns to
  normal utility arbitration. If the target is still outside the stop distance,
  `持续靠近目标` becomes active again without recreating the behavior group.

Gameplay mode presents these rules as controlled natural-language fields:
`何时执行 / 执行什么 / 成功后 / 失败后 / 玩法参数`. Developer mode exposes
the underlying fact kinds, operators, action id, score/cost, reaction-frame
window, cooldown, attack-instance token, compile diagnostics, revision and
stable hash. Both modes edit one Draft; switching modes must not discard fields.

The timeline must explain arbitration rather than merely list records. The
Decision track shows approach selection, threat preemption, dodge selection and
the return to approach. Selecting a threat/dodge event locates the reactive
card and displays the sampled target action, target local frame, predicted
impact frame, facing result, range result, response-token result and cooldown
result. Action trace continues to use the existing DodgeAccepted/DodgeRejected
events. Single click inspects; double click navigates to the event frame.

Runtime implementation remains inside CharacterTest. It may extend
CharacterTest fact/action/profile/state snapshots and immutable Lab projections,
but it must not add or change a public `MxFramework.AI` API. Threat sampling,
arbitration, dodge submission and trace capture are `NoAlloc after
initialization`; display text is produced only by low-frequency UI projection.

Required visual invariants:

- The 3D simulation remains the largest single region and keeps both agents and
  their spatial relationship readable.
- The right workspace shows one behavior card flowchart and its structured
  Inspector together; it does not repeat the entire behavior-group list.
- The bottom timeline exposes revision, decision, action, hit/damage, and
  warning tracks without covering the agents.
- Runtime lifecycle state, selected frame, selected Agent, active revision,
  draft state, and compile state remain visible in the top bar.
- Raw ids, raw facts and hashes remain internal replay/consistency data. They
  are never rendered in Runtime AI Lab text or tooltips; user-facing
  diagnostics project them into localized names, states and remediation.

### Setup Visual Reference

The following image is the visual acceptance reference for the pre-battle
Setup state. The runtime loads the published Composer-owned
`MxCharacterTestRuntimeAiLabUi/RuntimeAiPlannerDevPanel`; only its persistent
launcher remains in the legacy `MxCharacterTestHud` package.

![Runtime AI Planner Lab setup target](../Images/CharacterTest/runtime-ai-lab-setup-target.png)

Setup uses a 64px experiment bar, an unobstructed scene-first viewport, a
720px opaque configuration workspace, and a 48px scene summary strip. The
workspace order is fixed: team summaries, compact slot selectors, selected
slot editor, test rules, then readiness and the single `开始测试` action.
Timeline controls remain hidden before the first runtime frame exists. The
mockup's projection-mode buttons are illustrative only; the implemented
surface exposes the existing functional `适配视野` command and does not publish
fake controls. The selected-slot badge is team/slot identity, not a fabricated
character portrait.

### Setup Behavior Group Picker Reference

The behavior-group field no longer uses permanent previous/next arrows. It
opens the following anchored side popover immediately to the left of the setup
workspace, temporarily covering only the right edge of the 3D preview.

![Runtime AI Lab compact setup behavior-group picker target](../Images/CharacterTest/runtime-ai-lab-setup-behavior-group-picker-compact-target.png)

The compact popover omits a repeated title and helper footer. Search is
represented by an icon, result count is numeric-only, and each reusable row
separates its monogram, display name, card-count icon, number, and status.
Selected rows use a cyan-green accent and left rail without a redundant check
mark; unavailable rows are subdued and expose the precise reason only through
the custom tooltip/status-feedback path.

The row pool shows at most seven results and the popover contracts to the
visible result count. Larger result sets retain mouse-wheel traversal and add
a narrow draggable position indicator. Selecting an available group
immediately updates the current slot while keeping the popover open for rapid
comparison. Clicking the remaining 3D preview, activating the selector again,
or pressing Back closes it. No Apply/Cancel footer is added because the
selection is a small, immediately reversible setup edit.

## Summary（历史统一 Workbench 目标）

> 本节及其后续统一双上下文、三 Tab、完整五轨、`Apply F+1` 和抽屉结构只保留为历史 Workbench 设计与高级运行测试参考。单行为创作界面以[单行为创作与组合预览设计](CHARACTER_TEST_RUNTIME_AI_LAB_SINGLE_BEHAVIOR_AUTHORING_PREVIEW_DESIGN.md)为准。

Runtime AI Lab 只保留 `Setup / 实验准备` 和 `Workbench / 工作台` 两个全屏上下文。开发者在场景持续可见的前提下选择 Agent、观察运行、编辑行为计划、应用 revision，并在同一底部区域切换运行历史和计划时间轴。

本设计取消固定左栏和独立 Behavior Group / Behavior Card 页面。Setup 只选择完整行为组，不提供行为组条目编辑；Player Deck 与卡库改为按需入口；右侧复用现有因果卡区域并在下部显示 Inspector；底部通过 `运行轨迹 / 行为计划` 切换内容，不同时堆叠两套时间轴。

## Information Architecture

Workbench 只包含四个常驻区域：

| 区域 | 职责 | 不应包含 |
| --- | --- | --- |
| 顶部实验栏 | 实验状态、帧上限、Agent、revision、编译状态和生命周期命令 | 大段诊断、行为参数表、逐帧 raw 数据 |
| 中央 3D 主视图 | 角色选择、出生点布置、实时战斗、Overlay 和空间关系 | 固定背景面板、行为库列表、全屏日志 |
| 右侧上下文工作区 | 运行因果卡、选中计划项 Inspector、冲突修复和按需详情 | 完整卡库、行为组列表、重复场景信息 |
| 底部共享时间线 | `运行轨迹` 或 `行为计划`；同一时刻只显示一种 | 缩略总览、固定属性表、完整状态 dump |

角色和 Agent 以 3D 点击为主入口。顶部提供当前对象和前后切换作为无障碍与遮挡场景下的备用入口。点击 UI 控件不得穿透到 3D 选择或出生点放置。

Player Deck、卡库与高级诊断是覆盖在 Workbench 上的按需抽屉，不构成第五个常驻区域，也不改变模拟状态。

## 1920x1080 Reference Layout

完整设计基线采用 `1920x1080`。以下尺寸为 FairyGUI 设计坐标的目标范围，不要求像素级固定，但必须满足比例和最小场景面积。

```text
┌──────────────────────────────────────────────────────────────────────────────┐
│ Runtime AI Lab | Paused | 900f | Agent 1 | rev 3 | Draft saved | Compiled  │ 64
│                                      [Play] [Restart] [Overlay] [Exit]       │
├───────────────────────────────────────────────────────┬──────────────────────┤
│                                                       │ 感知 / 判断          │
│                                                       │ 行为 / 结果          │
│                    3D SCENE                           ├──────────────────────┤
│                                                       │ selected plan item   │
│        characters / target / range / movement         │ or conflict Inspector│
│                 selected Agent overlay                │                      │
├───────────────────────────────────────────────────────┴──────────────────────┤
│ [运行轨迹] [行为计划]  card breadcrumb  [卡库] [Undo] [Apply F+1]           │
│ 0f ─ 阶段 ─ 卡级检测 ─ 阶段检测 ─ 主动作 ─ 事件跳转 ─ 运行覆盖 ─ 90f       │ 560
└──────────────────────────────────────────────────────────────────────────────┘
```

- 顶栏目标高度 `56-72px`。
- `运行轨迹` 可使用 `44-56px` 紧凑态；`行为计划` 在 1920x1080 默认 `560px`、最小 `520px`，优先把新增空间分配给六条主轨。
- 右侧上下文列在 1920x1080 目标宽度 `520px`，1280x720 紧凑宽度 `360px`，不再为行为卡切换到 `720px` 独立流程档。
- 右侧允许拖动，范围 `320px` 到窗口宽度的 `32%`。
- 右侧完全收起时保留不超过 `44px` 的展开把手。
- 3D 主视图在任何档位都必须可见，不得被模态编辑面板覆盖。
- 时间轴下方不放缩略视图、小地图、总览带或预留空白。

## 1280x720 Compact Layout

`1280x720` 必须功能完整，但允许减少辅助文案和节点摘要。右侧上下文列可临时切换为 Inspector，Player Deck 和卡库使用覆盖式抽屉，不保留 720px 流程档。

```text
┌──────────────────────────────────────────────────────────────────┐
│ AI Lab | Paused | F 0/900 | A1 | rev 3      [Play] [...] [Exit] │ 52
├──────────────────────────────────────┬───────────────────────────┤
│                                      │ causal cards / Inspector  │
│              3D SCENE                │                           │
├──────────────────────────────────────┴───────────────────────────┤
│ [轨迹] [计划] Card > Stage [卡库] [Undo] [Apply]                │
│ 0 ━ 阶段 ━ 检测 ━ 主动作 ━ 事件跳转 ━ 运行覆盖 ━━━━━━━ 90      │ 368
└──────────────────────────────────────────────────────────────────┘
```

紧凑规则：

- 顶栏将低频命令收进 `...` 菜单，只保留状态、播放/暂停和退出。
- 右侧最小宽度 `320px`，打开 Inspector 时可暂时替换因果卡下半部。
- 行为计划自动 `Fit` 到可用宽度；片段只显示标题、动作、时长和 warning，完整条件进入 Inspector。
- 字段标签允许缩写，但按钮命令不得仅依赖颜色。
- `行为计划` 默认约 `368px`、最小 `340px`；轨道不足时允许垂直滚动，但不得增加缩略总览。
- 时间线增高时压缩 3D 主视图高度，但必须保持选中角色可见。

## Right Context Workspace

右侧工作区不再承担 Behavior Group / Behavior Card 页面导航。它保持一列，并按当前选择复用内容：

```text
Current causal chain
  -> Perception / Decision / Action / Result detail
  -> Selected Stage / Detector / Action / Transition Inspector
  -> Conflict diagnosis and repair
```

返回操作只关闭最深的详情或 Inspector，不重置场景选择、时间线模式、时间游标、草稿或滚动位置。切换 Agent 时保留每个 Agent 的独立 Lab Draft 导航状态。

### Width Presets

| 内容 | 默认宽度 | 主要内容 |
| --- | ---: | --- |
| Observe / Agent Inspector | 520px | 当前决策、目标、路径、动作和短诊断 |
| Selected plan item | 520px | typed 属性、时长、策略、目标和校验 |
| Blocking conflict | 520px | 双方 owner、冲突区间和确定性修复 |

自动切换宽度后仍允许拖动。用户手动宽度按内容类型分别保存；重新进入时恢复，但始终 clamp 到当前窗口的 `32%`。

### Player Deck And Card Library Drawers

- Player Deck 和卡库从 Workbench 打开为可关闭抽屉，不是全屏页面。
- 抽屉关闭后恢复同一 Agent、帧、镜头、播放状态、底部时间轴模式和滚动位置。
- 行为组只在 Setup 选择器中作为完整实验方案出现，不提供条目增删、复制、排序、禁用或参数覆盖抽屉。
- 点击具体行为卡可进入卡片逻辑 authoring，并在关闭后返回原来源与滚动位置。

### Behavior Plan Timeline Mode

正式高保真布局以本文件的“Unified Behavior Plan Authoring Standard”为准。上方沿用当前场景、Agent、实时状态和执行摘要；底部共享区域从 `运行轨迹` 原地切换为 `行为计划`。不创建 Behavior Card 全屏页，不增加固定左侧节点库，也不在时间轴内部再放一列 Inspector。

行为卡时间轴有自己的逻辑时间尺度，但不是运行历史的复制品。秒/帧双标尺表达计划时间；阶段、Detector 激活窗口和主动作的区段长度表达计划持续时间。事件可以在计划区段结束前触发 Transition，使逻辑游标跳到目标阶段起点。运行覆盖层显示最近一次实际耗时，精确帧证据仍由运行测试界面及其按需详情负责。

- 轨道自上而下固定为阶段、卡级检测、阶段级检测、主动作、事件跳转和运行覆盖层。
- 拖动区段主体修改计划开始时间；拖动左右手柄修改计划持续时间。所有时间编辑按当前吸附设置对齐帧、事件或阶段边界，并属于语义修改。
- `Until Event`、无限持续和等待事件的区段必须使用明确的延伸箭头、条纹或无限标记，不得伪装成任意固定时长。
- Detector 按优先级分轨；上下拖动可修改优先级，提交前必须显示“此操作会改变同帧裁决结果”的语义变更提示。
- 前跳、回跳与自循环在专用 Transition 轨道中使用带方向连线；长距离连线允许聚合或弱化未选路径，不能覆盖动作和检测文本。
- 当前阶段与逻辑游标保持高亮；活跃且已武装的 Detector 持续点亮。检测命中时闪烁对应区段，获胜 Transition 强调实际路径。
- `Interrupt` 和 `AfterActionEnd` 在动作释放前显示明确的 `Transitioning / Pending` 区段。任何视觉都不得让当前与目标动作同时呈现为 Running。
- Inspector 编辑开始时间、持续方式、激活窗口、typed fact、优先级、转换策略和目标阶段；行为、阶段和条件使用可读名称，raw id 与诊断 path 只保留在内部数据和导出证据中，不进入 Inspector/tooltip。
- 时间轴必须支持缩放、秒/帧切换、吸附、横向滚动、框选/多选、固定轨道标题、适配选择和适配整卡。
- 首个 v2 切片不实现底部小地图、缩略视图或总览带；全部高度分配给六条主轨。
- A/B 作为 `主动作` 与 `运行覆盖` 轨道内的双层 rail 展示，指标进入右侧 Inspector 或 tooltip，不增加独立底栏。
- 选择逻辑事件可切换到同一 Workbench 的 `运行轨迹` 并定位对应 typed marker；历史回看不得依赖 UI 文本或日志反解析。
- Composer-owned FairyGUI 包负责稳定组件合同；运行时可以池化时间区段、手柄、marker 和 Transition 连线，但不得伪造或修补 package XML。

### Plan Mode Draft Actions

`行为计划` 工具栏与右侧 Inspector footer 共同显示：

- 自动保存状态：`Saving / Saved / Save failed`。
- 草稿状态：`Clean / Dirty`。
- 预校验/预编译状态：`Pending / Compiling / Ready / Failed`。
- 当前已应用 revision 和候选 revision。
- `Undo/Redo` 与 `Apply at F+1`。

编辑停止约 `200ms` 后开始低频预校验/预编译。只有 `Ready` 状态允许应用。应用成功后从下一 `RuntimeFrame` 生效；编译失败保留旧 evaluator 和失败草稿，不暂停 Live simulation。

## Top Experiment Bar

顶栏命令随阶段变化，不同时展示所有控制：

| 阶段 | 主命令 | 状态信息 |
| --- | --- | --- |
| Setup | Start, Fit View, Reset, Exit | 帧上限、配置校验、选中槽位 |
| Initialized Paused | Play/Pause, 推进 +1, Restart, Back/Exit | F0/上限、Agent、revision、编译状态 |
| Live Editing | Play/Pause, Restart, Back/Exit | 当前帧/上限、Agent、active/candidate revision |
| Review | Play/Pause, 推进 +1（按历史规则启用）, Restart, Back/Exit | Review F/Latest、该帧 revision；Latest/Jump/A-B/Loop 收进时间线 `…` |
| Completed | Restart, Back/Exit | 结束帧/上限、最终 revision、验证状态；Change Limit 收进重新测试流程 |

帧上限只可在 Setup 或重新运行前修改。提供 `300 / 600 / 900 / 1800` 预设与 `1..1800` 自定义整数；默认 `900`。它表示本次实验允许捕获的 RuntimeFrame 数量，不是绝对 frame id。Start 后冻结该值，并以相同数量预分配 retention，正式实验内不得滚动淘汰；达到上限后进入 Completed，不允许向当前实验追加帧。

## Timeline Design

本节是 Runtime AI Lab 正式时间轴的实现与验收规格。`必须` 表示首个正式切片的验收要求，`后续` 表示已预留但不阻塞首个正式切片。正式五轨、逐 Tick typed capture、不可变 projection、frameLimit/Completed、Warm Replay 恢复和 FairyGUI 发布包已按本节落地；旧蓝色 scrub 与五行 selected-frame 文本仅保留为内部兼容 fallback，不再作为正常用户界面。Revision 轨当前按合同只显示 baseline span，immutable revision apply event 仍属于下一切片。

### Authority And Data Boundary

- 横轴唯一时间单位是 60 Hz `RuntimeFrame`；实时秒数只允许出现在 tooltip，不参与定位、排序或回放。
- 时间轴只读取已经捕获的 Lab session history、typed Runtime AI Planner snapshot、typed CharacterAction/Combat/runtime events、behavior revision event 和验证结果；禁止解析 UI 文本、日志字符串或重新查询 Unity 场景对象来生成 marker。
- 时间轴是低频只读投影，不进入 Runtime Tick、authority hash、SaveState、network 或正式 replay wire。点击、拖动和 marker 导航只能调用现有 Lab seek/selection 命令。
- 现有 `CharacterTestRuntimeAiPlannerDevTimeline` 继续拥有 retained records、selected/latest、A/B loop 和 replay 定位语义；正式 UI 在其上增加不可变 projection，不建立第二套时间线真相源。
- 可以复用 `DebugUiTimelineEntryViewModel` 的 frame/source/category/entity/trace/summary 归一化思路和稳定排序规则，但不能直接把 DebugUI 格式化文本渲染为轨道，也不能让 CharacterTest Runtime 反向依赖 UI Toolkit。

### Frame Domains And Selection

| 名称 | 定义 | 视觉/交互规则 |
| --- | --- | --- |
| `sessionStartFrame` | 本次实验第一个可重演帧，通常为首个捕获帧 | 之前区域显示为不可用，不可选择 |
| `retainedStartFrame` | 当前 records 中最早仍保留的帧 | 早于该帧的历史以斜纹遮罩表示已淘汰 |
| `latestFrame` | 最新捕获帧 | Live follow 和 `最新帧` 的目标 |
| `selectedFrame` | 已完成 seek、与场景/Inspector 同步的帧 | 蓝色实线游标；只有它能设置 A/B |
| `previewFrame` | pointer drag 期间的临时帧 | 蓝色虚线游标和浮动帧号；视觉逐事件更新，场景 seek 最多 10Hz |
| `frameLimit` | Setup 确定的本次实验捕获数量，范围 `1..1800` | 进度分母；不是绝对 frame id |
| `sessionEndFrame` | `sessionStartFrame + frameLimit - 1` | 正式 session 轴终点和 Completed 判定 |
| `viewStartFrame..viewEndFrame` | 当前可视窗口 | 只影响绘制，不改变 records、选择或 replay |

- 完整 session 轴为 `sessionStartFrame..sessionEndFrame`；`latestFrame+1..sessionEndFrame` 是可生成的未来区域。拖入该区域会以真实 Authority Tick 单调追加 RuntimeFrame，超过上限时 clamp。header 进度固定显示 `capturedCount/frameLimit`，刻度显示绝对 RuntimeFrame。
- seek 目标必须是 retained record。输入或 pointer 落在缺失帧时选择距离最近的 retained frame；距离相同时选择较早帧。超出 retained 范围时 clamp 到最近端点并给出中文状态反馈。
- 历史导航时，`selectedFrame` 只有在 Warm Replay 成功后更新。Live follow 的新 record append 则在同一提交中直接同步 `selectedFrame=latestFrame`，不启动 replay。seek 期间保留旧 selected 游标，并在目标 preview 游标上显示“定位中”；失败时回到旧 selectedFrame，不移动场景和 Inspector。
- retained capacity 与 frame limit 不得混用。正式 session Start 时 capacity 必须等于冻结后的 frameLimit，因此 `1..1800` 正常实验不会发生 retention 淘汰；现有固定 1800 rolling capacity 只作为迁移前兼容行为。`retainedStartFrame > sessionStartFrame` 时仍按本规格显示截断遮罩并禁止跨缺口 loop。

### Immutable Timeline Projection

新增 CharacterTest internal 只读投影，类型名固定为 `CharacterTestRuntimeAiLabTimelineDomain`、`CharacterTestRuntimeAiLabTimelineViewport`、`CharacterTestRuntimeAiLabTimelineProjection`、`CharacterTestRuntimeAiLabTimelineTrackProjection`、`CharacterTestRuntimeAiLabTimelineItem`、`CharacterTestRuntimeAiLabTimelineTrackKind`、`CharacterTestRuntimeAiLabTimelineItemKind` 和 `CharacterTestRuntimeAiLabTimelineSeverity`。Timeline 与 typed event index 都提供单调递增的 `ContentRevision`；新增/替换 record、branch truncate、retention 淘汰、diagnostic/revision event 变化都必须递增，不能只用 Count/Latest 判断缓存。Projection 至少包含 domain/viewport/selection、五条固定 track 和以下 item 字段：

```text
stableKey
trackKind
itemKind        Marker | Span | Cluster
startFrame
endFrame        Marker 时等于 startFrame
isOpen          未收到结束事件的 Span 为 true
agentEntityId   无关联时为 0
relatedEntityId 无关联时为 0
category
severity        Info | Success | Warning | Error
label
tooltip
count           非 Cluster 为 1
sourceTraceId
sourceOrder     同帧同源事件的稳定序号
```

- projection 输出使用只读数组；调用方不能修改 records 或 typed event 集合。
- source item 的 stableKey 固定包含 `trackKind/itemKind/startFrame/agentEntityId/relatedEntityId/category/sourceTraceId/sourceOrder`，不包含会随 Live append 变化的 `endFrame/isOpen/label/tooltip/severity`。未结束 span 以当前 `latestFrame` 作为 inclusive endFrame 且 `isOpen=true`，后续延伸或收到结束事件时必须复用同一 stableKey。Cluster 是 viewport projection item，其 stableKey 由 `trackKind/zoomLevel` 与有序 member stableKey 列表计算；成员不变时 pan/repaint 不得改变。item 稳定顺序固定为 `trackKind -> startFrame -> endFrame -> itemKind -> severity(desc) -> agentEntityId -> relatedEntityId -> category -> sourceTraceId -> sourceOrder -> stableKey`。`sourceOrder` 是 capture 时为同帧归一化事件分配的稳定 ordinal；`RuntimeCommand.Sequence` 只可帮助确定原捕获帧内顺序，不能作为跨 Warm Replay 的权威稳定 id。同一 history/replay 输入必须产生相同 stableKey 与顺序。
- Live capture 使用增量 append，不为每个 Tick 全量重建 1800 帧 projection。selected Agent、viewport/zoom、branch truncate、retention 或 source revision 改变时才重建受影响范围；普通 UI repaint 不重建。
- 缓存 key 至少包含 timeline revision、selected Agent entity id、viewport、zoom level 和 diagnostic revision。不得在 `Update/Tick` 中用 LINQ、反射、字符串解析或 Unity object 查找重建投影。

时间线导航新增 CharacterTest internal `CharacterTestRuntimeAiLabSeekRequest`，字段固定为 `TargetFrame / Origin / StayPaused / AgentEntityId / ItemStableKey`。`Origin` 区分 `Scrub / Jump / Marker / Latest / Loop`；scrub、Jump 和 marker 固定 `StayPaused=true`，解决从 Live 发起历史导航后自动恢复播放的问题。Latest 由 Play 命令发起时可以 `StayPaused=false`，单独点击 Latest 时保持当前 pause 状态。完成结果使用 internal `CharacterTestRuntimeAiLabSeekResult` 返回 committed frame、resolved Agent、authority/presentation 状态和 failure code；不新增 `MxFramework.AI` 公共 API。

### Track Contract

轨道顺序固定，不允许用户拖动重排。Agent 专属轨道只显示 selected Agent；全局轨道同时显示双方事件，并用 tooltip 标明 source/target。

| 轨道 | 正式数据源 | item 规则 | 空/未实现状态 |
| --- | --- | --- | --- |
| `Revision` | 初始 baseline 与 immutable behavior revision apply event | revision 从 apply frame 延伸到下一 revision 前一帧；apply frame 有菱形 marker，span 显示“行为配置已更新” | revision runtime 未完成时只显示一个“当前基线”span，不伪造 revision/apply marker |
| `Decision` | selected Agent 相邻 frame snapshot | `(selectedActionId, selectedActionKind, activeStatePath, targetEntityId, behavior profile/loadout/variant)` 任一变化时产生 marker；连续相同选择合并为 span | 没有 Agent 时显示“当前帧无智能体”，不从 Inspector 文本反推 |
| `Action` | `TimelineRecord.DrainedCommands`、typed CharacterAction lifecycle 与当帧 `CharacterTestTraceEvent` | command 是点 marker；action start 到 complete/cancel/reject 为 span；label 与 tooltip 使用友好动作名和可读执行状态 | 尚未捕获 typed action history 时显示“未采集动作事件”，不得用 `LastCommand` 字符串冒充 span |
| `Hit / Damage` | 当帧新增的 typed Combat/`CharacterTestTraceEvent` history | accepted hit、blocked hit、damage 各自为 marker；同一 trace/frame 的 hit+damage 合并为一个 item，tooltip 显示攻击者、目标、伤害和可读结果 | 无事件显示空轨，不显示常驻说明文字 |
| `Warnings` | apply/编译尝试、revision apply、capture gap、seek/Frame Sync、Presentation diagnostics | Warning/Error marker；跨帧故障可用 span；恢复事件结束 span但不删除历史 | 普通 Failed 草稿只留在右侧状态栏；只有用户 Apply 尝试失败才产生 authoring marker；未检查不等于失败 |

Decision change 的第一个 retained frame 必须生成起始 span；retention 淘汰后重新投影时，以 retainedStartFrame 作为截断起点并在 tooltip 标记“历史已截断”。Combat/Action adapter 应在 frame capture 时保存 session-only typed event index，不能在打开时间轴时扫描当前世界倒推历史。`CharacterTestTraceRecorder.RecentEvents` 的 ring 只有短期诊断用途；每帧新增事件必须在被 ring 淘汰前复制到对应 record/index。现有 `FrameSyncRuntimeEventSample` 只有 count/hash/last-event 摘要，不足以生成 marker，不能作为正式事件历史。

### Capture Boundary And History Continuity

- 正式历史必须在每个 Runtime Tick 完成后立即捕获一次，顺序固定为“模块 Tick 完成 → drained commands/runtime/trace/combat events 冻结 → planner/animation presentation input snapshot → authority hash → TimelineRecord”。
- `LateUpdate` 只允许触发低频 UI refresh，不能继续作为 frame history 的唯一采样点。2x/4x 播放或单个 Unity Update 内执行多个 Tick 时，仍必须为每个 RuntimeFrame 生成 record，且 commands/events 归属原始帧。
- 同一 RuntimeFrame 只允许一个 record；重复 callback 以 frame id 去重。捕获缺口不能用前后 snapshot 插值或复制填充，必须形成 `MissingFrameRange` warning 并禁止跨缺口 A/B Loop。
- Warm Replay 重建 history 时，原始外部命令和 revision event 只重放一次；重放产生的派生 marker 必须与原 stableKey 对齐，不得向原 history 重复 append。Review seek 本身不是 timeline event。
- 从历史帧开启 edit branch 并截断 future 时，records、typed event index、projection items、A/B 端点和 viewport selection 必须在同一事务中删除 `branchFrame` 之后的数据；普通 Review seek 不截断 future。
- typed event index 在 Start 时按 `frameLimit × 64` 个 compact sample 预分配，单帧最多保留 64 个事件且不在 Tick 中扩容。归一化保留优先级固定为 revision/action lifecycle → Error/Warning → accepted hit/damage → command/query/info；超出时第 64 项替换为确定性的 overflow warning，记录被聚合数量和分类计数。字符串使用 session string table/stable id 引用，不在 Tick 拼 tooltip。

### Visual Layout And FairyGUI Components

当前运行时正式资源已迁入 Composer-owned `MxCharacterTestRuntimeAiLabUi`。行为工作台、逻辑时间轴和 RuntimeFrame 时间轴均由语义资源、受控 Composite 配方、Screen Blueprint 和 FairyGUI helper 物化并发布；运行时代码只绑定稳定 child contract 和池化动态节点，不临时绘制或修补 package XML。运行时组件职责固定为：

- `RuntimeAiLabTimeline.xml`：外壳、header、viewport、playhead、loop range 和底部命令。
- `Components/RuntimeAiLabTimelineTrack.xml`：左侧固定 track label 与右侧裁剪内容容器。
- `Components/RuntimeAiLabTimelineMarker.xml`：菱形/圆点/警告三角和 cluster badge。
- `Components/RuntimeAiLabTimelineSpan.xml`：revision、decision、action、warning 区间块。
- `Components/RuntimeAiLabTimelineTick.xml`：主/次刻度与帧号。
- `Components/RuntimeAiLabTimelineEventDetails.xml`：时间轴内事件详情、显式定位按钮和 Cluster 成员列表。

稳定 child name 至少包含 `timelineViewport`、`timelineRuler`、`timelinePlayhead`、`timelinePreviewPlayhead`、`timelineLoopRange`、`timelineRetainedMask`、`timelineFutureMask`、`timelineEventDetails`、`trackRevision`、`trackDecision`、`trackAction`、`trackCombat`、`trackWarnings`、`btnTimelineCollapse`、`btnTimelineZoomOut`、`btnTimelineZoomIn`、`btnTimelineFit`、`btnLatestFrame`、`btnStep`、`jumpFrameInput`、`btnJumpFrame`、`btnLoopStart`、`btnLoopEnd` 和 `btnToggleLoop`。`RuntimeAiLabTimelineTrack` 内部固定使用 `trackLabel` 与 `trackContent`；marker/span/cluster 实例只能进入 `trackContent`。现有 `step/latest_frame/jump_frame/loop_start/loop_end/toggle_loop` command id 继续复用；事件交互使用 CharacterTest internal 的 item-select、item-navigate 和 selection-clear 命令，不修改 `MxFramework.AI` 公共 API。

布局基线：

- `运行轨迹` Compact 高度 `52px`；Expanded 在 1920x1080 默认 `320px`，在 1280x720 默认 `240px`。`行为计划` 在 1920x1080 默认 `560px`、最小 `520px`，在 1280x720 默认约 `368px`、最小 `340px`，并把全部新增高度分配给六条主轨。上边缘 `timelineResizeHandle` 在安全范围内调整高度，状态只保留在当前 Lab UI session，Reset/Release 后恢复默认值。时间轴背景必须完全不透明，未来区域使用低对比遮罩/斜纹，禁止以大面积半透明灰层覆盖 3D 场景；`行为计划` 下方不得增加缩略视图、小地图或总览带。
- Expanded 左侧 track header 宽 `132px`（1280 下 `104px`）；顶部 ruler 高 `28px`；五条轨道等高，剩余高度给底部控制行。
- selected playhead 穿过 ruler 和全部轨道，宽 `2px`；preview playhead 为 `1px` 虚线；两者顶部都显示帧号标签，重叠时只显示 selected 标签。首尾游标必须向轴内夹至少 `3px`，帧号标签也必须 clamp 在 ruler 内，不能与边框重合或越界。
- A/B 有效时显示半透明区间带和两端把手；A、B 不完整时只显示已有端点 marker。Loop 播放期间区间带保持可见但不可编辑。
- 未来区域、已淘汰区域和 seek 中区域必须有不同的非颜色提示（斜纹、锁图标或文字），不能只靠颜色区分。
- 用户文案为简体中文；RuntimeFrame 可以作为帧语义使用，hash、revision identity、Ability/CharacterAction id 与 diagnostic code/path 不进入任何界面文本或 Tooltip。
- 主轨道、marker Tooltip、事件集合和详情卡共用同一语义投影。Authority hash、record generation、source trace、行为/阶段 GUID 与 checkpoint identity 只保留在内部选择、重放和一致性校验数据中。Presentation marker 只表示 session-only 表现输入/校验结果，不承诺录像级视觉回放，也不能冒充 authority 失败。

### Ruler, Zoom, Pan And Aggregation

- zoom 级别固定为可视跨度 `120 / 300 / 600 / 900 / 1800` 帧加 `适配`。`适配` 显示完整 session 轴；放大/缩小选择相邻级别，不允许产生任意浮点跨度。
- 默认使用 `适配`。在 Live follow 模式放大后，viewport 右边缘跟随 latest；用户主动 pan、拖 preview 或选择历史 marker 后关闭 follow。点击 `最新帧` 恢复 follow 并把 latest 放在 viewport 的 80% 位置，右侧保留未来空间。
- zoom 以 selectedFrame 为锚；没有 selection 时以 latestFrame 为锚。Ctrl+滚轮与 `+/-` 按钮行为相同；Shift+滚轮和中键拖动按整帧 pan。普通滚轮保留给页面滚动。
- ruler 主刻度目标间距 `80-140px`，次刻度不少于 `8px`；算法只从 `1/2/5 × 10^n` 帧步长中选择，首尾和 selected frame 标签优先保留。
- 同一 track 中屏幕 x 距离小于 `6px` 的 marker 聚合成 Cluster，badge 显示数量；单击 Cluster 展开按 stableKey 稳定排序的成员列表，双击 Cluster 才逐级放大并居中聚合范围。成员单击只查看，双击才定位成员帧。
- span 最小绘制宽度 `3px`。同帧同 trace 的 hit/damage 合并；其余重叠 span 最多使用 3 个垂直 lane，超过后折叠为 Cluster。viewport 内最多实例化 `256` 个 FairyGUI item，超过时必须继续聚合，不能截断最高 severity 事件。
- marker/span 使用池化组件；viewport 外 item 不实例化。Live 可视 projection 最多以 `10 Hz` 增量刷新，暂停、seek 完成、Agent/viewport/zoom 改变时立即刷新。展开时间线或切换 Agent 允许低频分配，但稳定 Live playback 不得每帧创建 FairyGUI 对象或重新格式化全部 tooltip；tooltip 只在 hover/selection 时惰性格式化并按 stableKey 缓存。

### Compact Timeline

Live 时默认显示 compact：一条聚合轨、latest/selected、frame limit、selected playhead、revision 边界以及 Decision/Action/Hit/Warning 聚合 marker。Compact 始终使用 `适配`，不显示 pan/zoom；单击 marker 只选择事件，双击才按 Expanded 的事件导航规则定位。点击空白只移动 preview，松开后提交一次 seek。

Live follow 时 selected 与 latest 同步。pointer down 立即显示本地 previewFrame；pointer move 每次更新视觉游标，但 desiredFrame 最多每 `100ms` 更新一次。历史目标走 Warm Replay；未来目标每个 Unity Update 最多消费 `4ms`、最多执行 `16` 个真实 Authority Tick。seek 与 future advance 始终只消费最新 desiredFrame，不积压中间目标。场景、Inspector 和 Overlay 最多 `10Hz` 刷新，最终目标强制刷新。向左退回已生成范围会立即停止继续生成并回看，已生成记录不删除；松手继续完成最终目标，Cancel 或隐藏只停止后续 Tick。只有 Reset 清空本次实验。

### Expanded Timeline

Initialized Paused、Paused Inspect、Review、Loop Review 和 Completed 默认 Expanded；Live 可由用户手动展开。展开/收起、zoom、viewport 和每个 Agent 的最后 selected item 是 UI session state，不写入 Lab Draft 或 gameplay snapshot。

单击 marker/span/Cluster 只更新临时 UI selection，并在时间轴内打开详情浮层；不得改变 selectedFrame、latest、viewport、暂停状态、selected Agent、simulation 或 seek 状态。selection 保存 stableKey、轨道、类型、起止帧、Agent/目标、category、severity、trace、数值和 Cluster 成员；刷新、pan、zoom 后按 stableKey 恢复描边。Reset、Release、隐藏面板、模式切换或 source event 被截断时清除，不进入 Draft、SaveState、replay 或 gameplay snapshot。

普通事件详情固定宽 `520px`、高 `600px`，正好占满右侧场景区并衔接 `y=600px` 的时间轴。内容顺序固定为标题、观测区间、三行语义详情、涉及对象和底部定位按钮；标题 `26px`、区间 `24px`、详情 `18px`、按钮 `17px`，图标和关闭按钮均为 `52px`。精确帧集合沿用 `600px` 高度并显示最多五条成员；密度聚合保持紧凑，不占用完整详情高度。事件卡、Hover、集合成员和详情卡必须从同一个低频语义 ViewModel 取文案，禁止从技术字符串反向解析。

普通 marker/span 的双击和详情浮层中的“定位此帧/定位起点/定位终点”才执行导航。导航固定先缓存稳定 `agentEntityId`，再以 startFrame 或 endFrame 发起 Warm Replay；成功后按 entity id 解析 Agent 并同步场景、Overlay 与 Inspector。global item 不切换 Agent；entity 缺失与 replay/hash 失败继续使用既有 warning 和恢复机制。Cluster 双击只缩放并居中聚合范围，不执行历史定位；成员双击定位对应帧。

marker/span/Cluster、详情浮层、成员行和浮层按钮的 touch begin/move/end/click 必须全部阻止传播，事件自身的拖动不得触发 scrub。空白轨道仍负责历史回看和未来生成，并在 scrub 开始时关闭详情浮层。marker hover 显示中文摘要、帧、Agent、可读状态和 source→target 关系，不显示稳定 id/trace；右键没有命令，避免形成另一套隐藏操作入口。

### Frame Controls And A/B Loop

#### 选中帧、历史播放与分支重算

- `selectedFrame` 是用户当前操作帧；`latestFrame` 只是当前唯一时间线已经生成的末帧，两者不得再被当作同义词。
- 普通历史帧保持只读。用户在历史帧点击播放时，从 `selectedFrame` 逐帧顺序回放已有记录；到达 `latestFrame` 后无缝切换到实时 Authority Tick，不得静默先跳回 Latest。
- 历史帧只有在用户显式点击 `从此重算` 后才能生成新 Authority 数据。武装态只保存在 Lab UI session，显示 `基于帧 F，首次生成将删除 F+1…L`；切换选中帧、Latest、Reset、隐藏、Release 或取消都会退出武装态。
- 进入重算态不修改 Timeline、FrameBuffer、事件索引、A/B 或 Authority。第一次 `AdvanceOneAuthorityFrame` 成功并捕获 `F+1` 时，才原子截断旧 `F+1…L`，清理越界 A/B 与失效事件选择，再提交唯一的新未来；失败必须保留 F 和完整旧未来。
- `推进 +1`、未来拖动和重算播放共用 internal `AdvanceOneAuthorityFrame(presentationPolicy)`。每次调用只执行一个真实 Authority Tick，并完整经过 RuntimeHost、command drain、typed event capture、authority hash 与 TimelineRecord；不得补帧或复用旧未来记录。
- 普通模式从历史区域直接拖入未生成区域时，只能导航至 Latest，并提示先选择 `从此重算` 或从 Latest 继续。重算态向右拖动则从基准帧逐帧生成；仍遵守每 Update `4ms / 16 Tick` 预算和最多 `10Hz` 表现刷新。
- 本实现保持单一时间线。旧未来一旦因首个成功 Tick 被截断即不可恢复；不提供分支树，Reset 仍是重新开始实验的唯一入口。

- 历史选择统一由 scrub、marker、`跳转` 和 `最新帧` 完成；顶部只保留一个语义明确的 `推进 +1`，不恢复 `-10 / -1 / +10`，底部不恢复 Previous/Next Frame 按钮。`跳转`、`最新帧`、A/B、Loop 和 Replay 作为低频命令进入时间线 `…`。
- `推进 +1` 在 paused Latest 可直接使用；在已武装的历史帧也可使用，并触发上述首次成功后截断。未武装的历史 Review 中禁用。一次命令必须只执行一个 Authority Tick，捕获恰好一个新 RuntimeFrame，结束后仍保持暂停，并同步 latest/selected/follow。
- A/B 只能从已提交 selectedFrame 设置。重新设置 A 且 `A >= B` 时清除 B；B 必须大于 A。retention 淘汰任一端点时关闭 Loop 并显示 warning，不把端点静默 clamp 到新范围。
- Loop 播放只在 retained A..B 内运行；它反复 Warm Replay 已捕获的历史帧，不执行 authority Tick、不新增 record/event/revision，也不改变 latestFrame。暂停后 selectedFrame 停在当前循环帧。点击 Latest、Reset 或开始 seek 会先停止 Loop，再执行对应命令。

### Lifecycle And Failure States

| 状态 | 展示 | 可用交互 |
| --- | --- | --- |
| Setup | 时间线隐藏 | 无 |
| Initialized Paused / Paused Inspect | Expanded，selected=latest | 全部历史选择、A/B、Advance +1、zoom/pan |
| Live | Compact + follow latest | 空白 scrub 会暂停并 seek；事件单击只选择、双击才导航；可手动 Expanded |
| Review | Expanded，selected < latest | 默认只读；可顺序播放已有历史，或显式 `从此重算` 后 Advance/拖动生成；Apply 仍禁用 |
| Seek | 旧 selected 实线 + target preview 虚线 + loading | Reset 可取消；其余 seek/loop 命令禁用 |
| Loop Review | Expanded + A/B range | Pause、zoom/pan；禁止修改 A/B 和 Advance |
| Completed | Expanded | 历史检查可用；生成新帧禁用 |

- 无 frame 时显示一个居中的“尚未捕获运行时帧”，不显示五行伪轨道文本。
- 某 typed source 未接入时只禁用对应轨道并显示一次“未采集…事件”；其他轨道继续工作。
- projection/绑定异常不得影响 simulation、records、selection 或 evaluator；显示单一 timeline warning，并保留现有 scrub + Jump 兼容入口。
- Reset/Release 清空 projection、pool、preview、viewport 和 item selection；不得保留上一次 session 的 marker。

### Implementation Order

1. 先补冻结 frameLimit/Completed、逐 authority Tick capture、history `ContentRevision` 和 seek 失败自动恢复；现有 scrub UI 继续工作。
2. 实现 internal typed event index、immutable projection、增量 append、稳定排序/聚合和单元测试。
3. 接入 CharacterAction 与 Combat typed history；缺失源在接入前必须显示明确 unavailable 状态。
4. 已在 `MxCharacterTestRuntimeAiLabUi` 实现并发布正式 FairyGUI ruler、五轨、marker/span/cluster、zoom/pan、preview/selected 游标、四轨逻辑时间轴、固定 Inspector 和命令绑定；已完成 helper Apply/Publish、Resource Catalog 构建及 Unity focused 验证。
5. immutable revision runtime 落地时只补 Revision adapter 与 apply event，不重写时间轴 ViewModel 或 FairyGUI 合同。
6. 删除五行 selected-frame 文本占位只允许发生在正式五轨通过 PlayMode 验收之后；底层旧 frame navigation 方法继续保留兼容但不重新暴露按钮。

### Timeline Acceptance Matrix

| 场景 | 必须验收的结果 |
| --- | --- |
| Projection determinism | 相同 records/events/Agent/viewport 生成相同 item、stableKey、顺序和 cluster；projection 不修改输入 |
| Tick continuity | 1x/2x/4x 和单 Update 多 Tick 都逐 RuntimeFrame 记录，frame/command/event/hash 不丢失、不重复；LateUpdate 频率不影响 history |
| Decision spans | 只有决策 tuple 改变才分段；首个 retained frame 有起始 span；切 Agent 后只替换 Agent 专属轨道 |
| Open spans | Live append 时未结束 revision/decision/action/warning span 延伸 endFrame 但 stableKey 不变；收到结束事件后 `isOpen=false` 且不再延伸 |
| Revision baseline | revision runtime 未接入时只有“当前基线”；不得显示伪 rev/apply hash |
| Action/Combat history | typed start/end/hit/damage 生成正确 span/marker；同 trace hit+damage 合并；不解析 `LastCommand` 或 UI 文本 |
| Scrub | Live/Paused/Review 的 touch move 即时更新 preview、最多 10Hz coalesced seek；touch end 精确提交最终帧；成功后场景、Inspector、selected playhead 同帧 |
| Event selection | marker/span/Cluster 单击只高亮并打开详情；selectedFrame/latest/viewport/Agent/simulation/seek 均不改变；事件与浮层不会穿透触发 scrub |
| Marker navigation | 普通事件双击或浮层按钮先 replay 再按稳定 entity id 解析 Agent；span 起点/终点准确；global marker 不换 Agent；entity 缺失有明确 warning |
| Seek recovery | authority/hash 失败后自动恢复旧 committed frame；二次恢复失败进入只允许 Reset/退出的不可继续状态 |
| Latest/Advance | Review 中 Advance 禁用；Latest 恢复 follow；Paused Latest 的一次 Advance 只产生一帧、结束仍暂停且 latest/selected 精确 +1；到上限进入 Completed |
| A/B Loop | 区间、端点、播放和 retention 淘汰行为符合本节规则；seek 时不允许并发 loop；循环 3 轮后 history count/latest/event index 均不变 |
| Zoom/Pan | 六种 zoom 状态、锚点、边界 clamp、follow 退出/恢复和 1280/1920 布局稳定 |
| Aggregation | 高密度 fixture 不超过 256 个实例；cluster 数量和展开顺序稳定；Error/Warning 不被截断 |
| Event overflow | 单帧超过 64 项时按固定优先级保留并生成稳定 overflow warning；跨 replay 数量与分类一致 |
| Branch truncate | edit branch 原子删除 future records/events/items/A-B；普通 Review seek 不删除、不重复追加事件 |
| Empty/Unavailable | 无帧、无 Agent、source 未采集、seek 失败各有不同反馈，且 simulation/evaluator 不改变 |
| FairyGUI contract | 正式组件、child name、类型、命令和焦点导航存在；真实发布包可加载，atlas descriptor 仍注册 |
| PlayMode | `CharacterTestRuntimeAiLabScene` 中 compact/expanded、scrub、marker、Latest、Advance、A/B 和隐藏/重开可往返，场景保持可见 |
| Performance | 1800 records 高密度数据下 projection 有界；稳定 Live playback 不逐帧创建 marker、tooltip 字符串或临时集合 |
| Localization | 所有用户文案为简体中文；技术 id/hash/code/path 不进入界面或 Tooltip；1280x720 无重叠和截断 |

验证最低包含 projection/aggregation focused EditMode、FairyGUI source/published-package contract、一个 Runtime AI Lab PlayMode smoke、Unity refresh/compile、Console 0 error、相关工程 build 和 `git diff --check`。

## Workflow State Matrix

| 状态 | 3D 交互 | 右侧内容 | 时间线 | 编辑/应用 |
| --- | --- | --- | --- | --- |
| Setup | 选槽、拖出生点、相机 | 测试配置或行为预编辑 | 隐藏 | 可编辑草稿，不产生 runtime revision |
| Initialized Paused | 选 Agent、Overlay | Agent/行为编辑 | 窄条，可展开 | 可编辑并应用到 F+1 |
| Live Editing | 选 Agent、相机、Overlay | 保持当前编辑层 | 窄条跟随 latest | 可编辑；Ready 后显式应用 |
| Paused Inspect | 选 Agent、Overlay | 编辑或 Inspector | 默认展开 | 可编辑并应用到下一帧 |
| Review | 选 Agent、Overlay | 该帧只读 Inspector | 展开 | 禁止编辑与应用 |
| Completed | 选 Agent、Overlay | 结果/草稿编辑 | 展开 | 可编辑；应用需重新运行实验 |

Start 完成初始化后默认进入 Initialized Paused，不直接播放。实验只因达到帧上限或用户退出而停止，不因胜负或无目标提前结束。

## Draft Save And Publish UX

- Lab Draft 自动保存，保存与应用是两个独立动作。
- 自动保存失败必须持续显示错误，不能通过应用清除。
- 每个选中 Agent 首次编辑时创建独立实验副本，不修改共享行为组或其他 Agent。
- `Validated Draft` 才显示独立 Publish 命令；发布不得与 Apply 共用按钮。
- 发布具体权限、文件格式、版本迁移和 catalog 写入由后续专项设计定义。本设计只预留状态和入口。

## Advanced Diagnostics

Frame Sync probe、手动状态采集、同步记录状态、冷启动校验和完整 command history 放入高级诊断抽屉。该抽屉默认关闭，不占用顶栏、主 Inspector 或时间线常用轨道。Raw facts/hash/id 仅保留在内部记录或显式导出的机器证据中；高级诊断 UI 也只能显示其语义化结果。

高级诊断打开后仍不得改变 simulation、Overlay 或 selected frame；所有检查必须只读，除非用户显式执行带有“同步/恢复”语义的命令。

## Acceptance Criteria

- 进入 `行为计划` 不创建新的全屏页面；1920x1080 下仍保留完整可辨认的 3D 场景和选中角色。
- `运行轨迹 / 行为计划` 切换保持 Agent、selected/latest frame、镜头、播放状态、卡片选择和时间轴 viewport。
- 1280x720 下所有核心命令可达，文本不重叠、不溢出，计划时间轴可 Fit，Inspector 可编辑。
- 任意阶段无固定左栏；Player Deck 与卡库只使用按需抽屉，Behavior Group 只通过 Setup 选择完整方案，右侧工作区不出现三列或卡片嵌套卡片布局。
- 模拟开始、暂停、Review、Completed 不会自动关闭或重置当前编辑上下文。
- Dirty、保存、编译、候选 revision、active revision 和应用帧始终可见。
- 六条计划主轨和共享右侧 Inspector 可同时使用；时间轴下方没有缩略总览、小地图或预留空白。
- A/B 证据只叠加在现有轨道内，不新增底部摘要栏。
- UI 点击不穿透 3D 场景；3D 点击角色可切换 Agent，顶部备用选择可完成同一操作。
- Review 严格只读；返回 Latest 后才能应用新 revision。

## Out Of Scope

- Lab Draft JSON/schema 和迁移。
- 行为卡 runtime compiler 的具体接口。
- behavior revision 的序列化 payload。
- 正式发布权限和 catalog 更新协议。
- FairyGUI 资产和 C# ViewModel 实现。
