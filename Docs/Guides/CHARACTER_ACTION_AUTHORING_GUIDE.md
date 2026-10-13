# Character Action Authoring Guide

> Status: Guide
>
> Version 0.4.0 | 2026-10-12
>
> This guide defines the long-term formal authoring plan for `MxFramework.CharacterAction`.
> It is the development contract for turning the current pure C# action layer into configurable,
> validated, editor-driven framework functionality.

---

## 1. Goal

`CharacterAction` must become the framework-level source of truth for character action selection and action execution orchestration.

It answers:

- Which action should run when input, Runtime AI Planner, story, ability, or reaction requests an intent.
- Whether the action is allowed by grounded state, equipment tags, status, resource, cooldown, target, and current phase.
- Which tracks should be dispatched while the action runs: motion, combat, gameplay, animation, presentation, debug.
- Which animation action key, combat action id, gameplay request, cue, and diagnostic marker are used by that action.

It must not replace:

- `Combat.Authoring` for hitboxes, hurtboxes, weapon traces, combat phases, and combat action runtime data.
- `MxAnimation` for animation clip registry, animation set definition, blend tree, and action binding playback.
- `Resources` / `Config` for resource references, catalog validation, and runtime loading.
- `Character.Application` for character package aggregation and equipment/combat/animation binding resolution.

### 1.1 单一数字动作身份

- 每个正式动作只有一个全局 `CharacterActionId` 正整数；`0` 表示未配置。动作 JSON 只写 `actionId`，不写动作 StableId。
- `CharacterActionSource/action_id_catalog.json` 是版本化分配权威。新建/复制动作必须经过目录事务领取 ID，删除只退休、不复用；角色包创建先领取独立保留号段。
- 编辑器将 ID 显示为只读 `动作 ID`，列表文字使用 `显示名称（#301）`；允许用显示名或 `#301` 搜索。策划可编辑 DisplayName、Category、Tags、Requirements 和轨道，但不能手填身份。
- LightAttack 等语义来自 ActionSet intent/binding/sequence；BasicAttack 等类型来自 Category。Tags、Requirements、轨道和显示名都不能替代身份。
- `CombatActionId`、动画 Action Key、Resource Key、事件 Stable ID 和行为卡 Stable ID 仍是各自领域的字符串，不能批量改成数字动作 ID。

---

## 2. Current State

### 当前控制倍率标记

角色动作时间轴在运动轨道下固定显示“移动控制倍率”和“旋转控制倍率”两行，两行默认全程为 1。双击对应行或使用右键“插入倍率标记”，在该帧插入当前倍率；选中后只编辑帧和值。值可为 0 到 10，阶梯式生效，覆盖至下一标记或动作退出。需要提前恢复时插入值为 1 的标记。拖动到同轴已有标记的帧不会制造重复；删除标记接续前一个值，删除全部标记恢复全程默认 1。操作使用原有 Undo/Redo 事务。

标记控制主动移动速度和转向速率，不缩放 RootMotion、冲量、外力。普通事件菜单不再添加“设置控制倍率”；其他运动事件仍保留。动作结束、打断、取消、切换后旧贡献退出，新动作从自己的标记开始。

时间轴所有单帧标记统一使用直角矩形主体和矩形中心节点，信息提示也使用矩形；主体宽度与游标一致，占当前缩放下的一帧宽。较小缩放保留透明命中区域，选中和拖动行为保持原合同。

移动和旋转控制倍率各占一行 22px 高的紧凑行；16px 数值色带和标记垂直居中，标题与时间轴行同步对齐。

制作数据保存 `motionTrack.movementControlKeys[]` 和 `rotationControlKeys[]`，每项包含 `frame/value/stableEventId`；正式投影另携带准备好的数值排序。共享 noEngine 投影生成只读区间，编辑器不把派生区间当独立事件编辑，恢复 Common 制作源时也不反写这些事件。旧数据按逐帧有效值转换，区间结束时的恢复值会变成标记。打开只转换内存，保存才写回；无法转换时保留原内容并阻止覆盖。动作缩短后保留越界标记，修正前不能保存。

当前 Common 动作库已按这一规则转换；Light Attack 在第 0 帧把移动倍率设为 0，在第 17 帧把旋转倍率设为 0，退出后恢复 1。回归入口为 `CharacterActionControlScaleTimelineTests`，覆盖独立轴、区间终点重投影、旧重叠窗口、删除恢复、往返与 0–10 派发。测试定义本身不代表实际运行结果。

> Code-Doc: `Packages/com.vincent.wgameframework/CharacterAction/CharacterActionControlScaleJsonProjection.cs`
> Code-Doc: `Packages/com.vincent.wgameframework/Editor/CharacterAction/CharacterActionControlScaleAuthoring.cs`
> Code-Doc: `Packages/com.vincent.wgameframework/Editor/CharacterAction/Timeline/*ControlScale*.cs`

The code already has the runtime contracts:

| Capability | Current type |
| --- | --- |
| Action set | `CharacterActionSetConfig` |
| Action config | `CharacterActionConfig` |
| Command binding | `CharacterActionBinding` |
| Ability binding | `CharacterAbilityActionBinding` |
| Reaction binding | `CharacterReactionBinding` |
| Requirements | `CharacterActionRequirement` |
| Phases | `CharacterActionPhase` |
| Transition window / interrupt | `CharacterCancelRule`, `CharacterInterruptRule` |
| Tracks | `MotionTrackConfig`, `CombatTrackConfig`, `GameplayTrackConfig`, `AnimationTrackConfig`, `PresentationTrackConfig`, `DebugTrackConfig` |
| Resolver | `CharacterActionResolver` |
| Execution gateway | `CharacterActionExecutionGateway` |
| Execution authority | `CharacterActionExecutionGateway`（内部独占 Runner） |
| Track adapters | `CharacterActionTrackAdapter`, `CharacterActionPresentationTrackAdapter` |
| Diagnostics | `CharacterActionValidation`, `CharacterActionWorkstation` |

The missing formal pieces are:

- A stable serialized authoring format.
- A Unity authoring asset or JSON-backed config source.
- A dedicated editor to create, edit, validate, and preview action sets and actions.
- Export/import between authoring data and runtime config objects.
- Validation that checks references across CharacterAction, Combat, MxAnimation, Resources, and Character.Application.
- CharacterTest integration that uses the same formal data path as future games.

---

## 3. Source of Truth

The long-term source of truth should be split by responsibility.

| Data | Source of truth | Reason |
| --- | --- | --- |
| Input intent to action mapping | Character Action authoring data | This is character behavior configuration, not input system configuration. |
| Action category, priority, tags, requirements | Character Action authoring data | The resolver needs these fields before runtime execution. |
| Phase ranges for character-authored actions | Character Action authoring data | Jump, dodge, guard, interaction, reaction, and simple skills do not require combat timelines. |
| Action transition windows | Character Action authoring data | Designers tune when the current action can switch into a requested target action; runtime still stores these windows as `cancelRules`. |
| Combat action timing, hitboxes, hurtboxes, weapon traces | Combat Authoring asset / exported combat data | Combat already owns deterministic hit timing and shape authoring. |
| Animation action key to clip binding | MxAnimation animation set definition | Animation clips are shared resources and are not owned by character actions. |
| Character default equipment and selected action set | Character.Application / character package | Character package selects which behavior set applies to a character/equipment state. |
| Resource existence | Resource catalog / animation registry / combat export registry | Action authoring stores keys; resource modules validate and load. |

Rule: `CharacterAction` may reference external ids, but must not duplicate external data.

Terminology rule: authoring UI and design docs call action switch ranges **Transition Windows / 切换窗口**. The serialized JSON field and runtime type remain `cancelRules` / `CharacterCancelRule`. A transition window declares that the current action may switch when local frame、最终动作 Category/request type 与 action slot mask 匹配；它不保存目标动作名称或字符串身份。连段目标由 ActionSet sequence 解析，普通 binding、闪避、技能和防御由对应 intent/slot 解析。

---

## 4. Serialized Data Model

The formal serialized data should mirror runtime contracts without exposing implementation-only constructors.

Recommended root files:

```text
Assets/MxFrameworkGenerated/CharacterActions/Profiles/<profileId>/character_action_profile.json
Assets/MxFrameworkGenerated/CharacterActions/ActionSets/<setId>/character_action_sets.json
```

Profiles are the explicit composition layer. A profile selects one or more ActionSets and one or more Action Libraries for a runtime context. The profile may be selected by a character package, weapon state, demo scene, or composition root, but it does not make actions or action sets character-owned.

Action sets are intent / ability / reaction mapping data. They may include optional character or equipment-state filters, but they are not owned by a character package or weapon. Actions are reusable execution templates and should normally live in one or more action libraries:

```text
Assets/MxFrameworkGenerated/CharacterActions/Libraries/Common/character_actions.json
Assets/MxFrameworkGenerated/CharacterActions/Libraries/<libraryId>/character_actions.json
```

Legacy directories that contain both root files remain valid; authoring treats them as one ActionSet source plus one Action Library source. New data should use the neutral Profile / ActionSet / Library layout above instead of character package paths.

`character_action_profile.json`:

```json
{
  "schemaVersion": "0.4.0",
  "profiles": [
    {
      "id": 1,
      "stableId": "codex_flow_test.default",
      "displayName": "codex flow test Default",
      "actionSetIds": [
        "codex_flow_test.empty_empty",
        "codex_flow_test.empty_sword",
        "codex_flow_test.sword_empty",
        "codex_flow_test.sword_shield",
        "codex_flow_test.sword_sword"
      ],
      "actionLibraryIds": ["Common"]
    }
  ]
}
```

`character_action_sets.json`:

```json
{
  "schemaVersion": "0.4.0",
  "actionSets": [
    {
      "id": 1,
      "stableId": "codex_flow_test.sword_shield",
      "displayName": "codex flow test Sword Shield",
      "characterStableId": "char.codex_flow_test",
      "equipmentStateStableId": "equip_state.codex_flow_test.sword_shield",
      "movementProfileId": "movement.default",
      "reactionProfileId": "reaction.codex_flow_test.default",
      "defaultActionId": 0,
      "commandBindings": [
        {
          "intentId": "LightAttack",
          "actionId": 301,
          "priority": 10,
          "allowQueue": true,
          "queueWindowFrames": 6
        },
        {
          "intentId": "Jump",
          "actionId": 300,
          "priority": 5,
          "allowQueue": false,
          "queueWindowFrames": 0
        }
      ],
      "abilityBindings": [],
      "reactionBindings": []
    }
  ]
}
```

`character_actions.json`:

```json
{
  "schemaVersion": "0.3.0",
  "actions": [
    {
      "actionId": 300,
      "displayName": "Basic Jump",
      "category": "Jump",
      "timelineAuthority": "CharacterAuthored",
      "tags": [],
      "priority": 10,
      "durationFrames": 20,
      "requirements": [
        { "kind": "Grounded", "stableId": "", "value": 0 }
      ],
      "phases": [
        { "kind": "Startup", "startFrame": 0, "endFrame": 3 },
        { "kind": "Active", "startFrame": 4, "endFrame": 15 },
        { "kind": "Recovery", "startFrame": 16, "endFrame": 19 }
      ],
      "cancelRules": [],
      "interruptRules": [],
      "motionTrack": {
        "usesRootMotion": false,
        "events": [
          {
            "frame": 0,
            "kind": "ApplyImpulse",
            "movementMode": "Airborne",
            "x": 0.0,
            "y": 1.0,
            "z": 0.0,
            "stableEventId": "basic_jump.impulse"
          }
        ]
      },
      "animationTrack": {
        "events": [
          {
            "frame": 0,
            "kind": "CrossFadeAnimation",
            "animationActionKey": "anim.basic_jump",
            "transitionSeconds": 0.08,
            "stableEventId": "basic_jump.animation"
          }
        ]
      },
      "combatTrack": null,
      "gameplayTrack": null,
      "presentationTrack": null,
      "debugTrack": null
    }
  ]
}
```

`cancelRules` are authored as transition windows and store request/category plus slot masks, not a target action identity or source string. A missing or non-matching transition window means the running action blocks the requested action until it finishes or is interrupted by a separate interrupt/reaction path.

For combat actions:

- `timelineAuthority` should be `CombatAnchored`.
- `durationFrames` may be omitted when duration is resolved from the bound `CombatActionTimeline`.
- `combatTrack.combatActionId` must reference exported combat runtime data.
- `animationTrack.events[].animationActionKey` must reference `MxAnimationActionBinding.ActionKey`.

---

## 5. Runtime Import Pipeline

The formal import pipeline should be explicit.

```text
JSON / ScriptableObject authoring data
  -> CharacterAction Profile
  -> selected CharacterAction ActionSets + Action Libraries
  -> CharacterActionJsonImporter
  -> CharacterActionAuthoringValidator
  -> CharacterActionConfig[]
  -> CharacterActionSetConfig[]
  -> CharacterActionResolverContext
```

Required runtime-facing services:

| Service | Responsibility |
| --- | --- |
| `ICharacterActionConfigSource` | Loads action sets and actions from package/config sources. |
| `CharacterActionJsonImporter` | Converts serialized DTOs to runtime contracts. |
| `CharacterActionConfigRegistry` | Stores action sets and action configs by stable id. |
| `CharacterActionResolverContextFactory` | Builds resolver context for a character instance. |
| `CharacterActionValidationReport` | Aggregates cross-module validation diagnostics. |

The importer must be deterministic:

- Preserve stable ids.
- Sort diagnostics by stable path.
- Reject duplicate ids and duplicate stable ids.
- Never silently create missing referenced data.

---

## 6. Editor Plan

The dedicated editor should be named:

```text
MxFramework/Character/Character Action Authoring
```

It should not be a fork of Combat Authoring. It should reuse patterns, not copy ownership.

### 6.1 Character Action Authoring 工作台

当前 Editor 入口继续使用 `MxFramework/Character/Character Action Authoring`，但界面不再以多个长表单页签组织。目标布局以 [Character Action Authoring 工作台效果图](../Images/CharacterAction/character-action-authoring-workbench-target.png) 为准：顶部固定当前 Profile、ActionSet、角色和装备上下文；左侧保持动作或映射导航；中央保持预览和时间轴；右侧显示当前动作、时间轴事件或映射的上下文属性；底部 `Problems` 抽屉集中显示可定位诊断。窗口最小尺寸为 `1180×700`。

左上工作区菜单提供四个一级入口：

| 工作区 | 内容 |
| --- | --- |
| 动作编排 | 聚合当前 Profile 引用的全部 Action Library；左侧按动作分类导航，中央编辑内嵌预览和时间轴，右侧编辑动作或所选时间轴项。 |
| 技能编排 | 聚合 `ability_authoring.json` 中的 Ability Effect Timeline；复用动作编排的三栏布局、时间轴、播放控制和隔离预览，右侧编辑技能或所选技能事件。 |
| 映射与方案 | 编辑 Profile 组成、ActionSet 设置、指令绑定、能力绑定、受击规则、引用数据源和只读兼容数据。 |
| 共享资源 | 容纳 consumer-specific Mechanic Tuning 与只读 Projectile 引用摘要；Projectile Definition、Request/Spawn 和 Presentation 的编辑统一深链接到独立 Projectile Authoring。 |

工作区菜单旁保留“动作 / 技能”快捷切换，分别直达动作编排与技能编排。两者共享 `CharacterActionTimelineView`、播放控制器和 `CharacterActionPreviewViewport`；技能预览把 Projectile、VFX、Gameplay 和 Audio 事件投影到只读 CharacterAction 预览模型，继续使用既有隔离预览运行链，不修改 Ability Runtime schema。技能时间轴只保存 Projectile Request Stable ID 和触发帧；Definition、Spawn、碰撞、命中和生命周期 VFX 由独立 Projectile Authoring 维护。

`CharacterActionAuthoringDocumentSession` 是 Editor-only 的完整文档会话。它同时持有 Profile 文件中的全部 `profiles[]`、ActionSet bundle 中的全部 `actionSets[]` 与 `reactionProfiles[]`，以及当前 Profile 引用的全部 Action Library。Profile、ActionSet、Library、Action 和时间轴项均以 stable id 选择；选择动作会切换到它所属的 Library，但不会修改 Profile 的 `actionLibraryIds[]`。Profile 成员关系只能在“映射与方案 → Profile 组成”中显式编辑。

“映射与方案 → Profile 组成”使用任务导向的“角色动作方案”总览，而不是直接展开 Profile JSON 字段。左侧装备方案卡片显示当前 ActionSet 的关联动作、输入触发、技能触发、受击响应和默认动作提醒；其中“关联动作”只统计默认动作、指令绑定、能力绑定和 Reaction Profile 直接引用的去重 action id，不表示 Action Library 中的动作归属于 ActionSet。右侧按“动作内容 → 输入与技能 → 受击响应 → 验证”提供现有工作区的直接入口，并把动作库区分为角色专属与 `Common` 公共库。Id、Stable Id、数据来源和历史未解析引用收进折叠的技术信息区。目标布局见 [角色动作方案总览效果图](../Images/CharacterAction/character-action-profile-overview-target.png)；图中数量是视觉示例，实际界面全部从当前文档会话和校验结果动态投影。

“添加装备方案”只把当前来源中已加载但尚未加入 Profile 的 ActionSet 加入成员关系，不在 CharacterAction 内创建新的角色装备组合。没有候选时，用户应先在 CharacterStudio 的组合武器工作台创建并生成 ActionSet，再回到本页重新加载 Profile 引用。移除 ActionSet 或动作库只修改 Profile 成员关系，不删除来源文件；仍有直接引用的动作库会先阻止无提示移除并展示引用。

每个来源按“文档类型 + 源路径”独立跟踪规范化 JSON baseline、revision 和 dirty 状态。普通选择、搜索、折叠和切工作区不进入 Undo，也不修改文件；字段编辑即时进入内存草稿并参加 Undo/Redo。顶部“保存”执行 Save All：先用当前 Profile 的全部 ActionSet、Reaction Profile 和 Action Library 生成完整校验上下文；Error 阻断所有写入并展开 `Problems`，Warning 允许保存；预检通过后再按稳定路径写入 dirty 来源，最后只执行一次资源刷新和一次重新校验。单文件失败会保留该文档 dirty 状态并报告准确路径，不把多文件写入描述为原子事务。文件菜单仍可按来源执行“保存/另存为”。

动作编排中央预览工具栏的“伤害事件”切换当前动作的攻击窗口对照表与 3D 画面，时间轴及播放位置保留。表格首版仅显示 `HitTraceWindow`，按起始帧排序；基础伤害、攻击系数、冲击、击飞和伤害性质可逐行编辑，标签、分析分类和其它来源属性系数在行内展开。差异数值会提示，表中不显示缺少目标防御的最终伤害试算。点击窗口行会选中对应时间轴事件并跳到起始帧；编辑沿用草稿、Undo/Redo、校验和顶部保存。

Common 生成库在本工作台编辑后，保存计划会按身份目录将改动还原到正式 `CharacterActionSource/Libraries/Common/character_actions.json`，校验正式源与生成投影的加载时哈希，并成对写入。外部文件变化会拒绝保存且保留草稿；写入失败会恢复这组文件。

`Problems` 直接投影 Import、Local Validation、Cross-module Validation 和 Reference Discovery 的结构化 diagnostics，并支持 severity、文本、当前动作和当前来源筛选。可识别的 stable path 会定位到动作、时间轴项、绑定行和对应属性；不能解析的路径回退到源文件。保存失败只展开抽屉，不擅自切换工作区或改变当前动作。

中央预览使用 Editor-only `PreviewRenderUtility` 隔离角色、VFX 与 Projectile 观察对象，不把临时对象放入用户活动 Scene。播放、暂停、逐帧和时间轴 scrub 共用同一个预览控制器。CharacterAction 中的 Projectile 路径与生命周期表现仅用于观察；射程、半径、爆炸范围、弧高等 projectile 参数和 Handles 只在独立 Projectile Authoring 中编辑。CharacterAction 的场景编辑继续服务动作/技能自身拥有的数据。

以上工作台只重组 Editor authoring 与预览体验：不修改 Runtime JSON schema、DTO、Resolver、Reaction 选择规则或公开的 `OpenWindow()` / `OpenForAbilityBinding(...)` 签名。`ReactionProfileId + CharacterReactionProfile` 仍是受击规则权威；旧 `reactionBindings[]` 只在“兼容数据”中只读展示。

The former MVP tabs map into the current workbench as follows; they are no longer top-level navigation:

| Former entry | Current destination |
| --- | --- |
| Profile | 映射与方案 → Profile 组成。 |
| Action Set | 映射与方案 → ActionSet 设置、指令绑定、能力绑定、受击规则。 |
| Action List | 动作编排 → 左侧动作导航。 |
| 时间轴 | Frame-based timeline for phases, transition windows, and motion/animation/combat/presentation events; bottom **属性** inspector; animation events render as clips with cross-fade overlay. |
| Tracks | Legacy tab name merged into 时间轴; gameplay/debug rows remain secondary, while transition windows are a first-class timeline row. |
| Validation | 全局 `Problems` 抽屉。 |
| Preview | 动作编排 → 中央内嵌预览。 |

### 时间轴交互（Unity Authoring）

- **右键菜单**：标尺/轨道空白处可在鼠标帧位置添加阶段或各轨事件；选中条/标记可删除、编辑属性；动画 clip 可「重新计算跨度」「EndFrame 对齐 Clip / 动作最后一帧」。
- **触控板**：除 Alt+滚轮缩放、中键平移外，优先用右键完成添加/删除，避免依赖精确拖拽。
- **动画轨**：`animationTrack.events[]` 支持 `endFrame`、`playbackSpeed`（默认 `1`）；编辑器以 **60fps** 换算 `transitionSeconds` 并绘制左侧过渡条；条宽 ≈ `clipFrames / playbackSpeed`（从 `MxAnimationClipRegistry` 解析 clip，未知时用默认跨度）。
- **切换窗口轨**：显示 `cancelRules[]`，用户看到的是“切换窗口”而不是“CancelRule”。第一版支持在时间轴上显示窗口并拖动 `startFrame/endFrame`；目标动作、来源和 `allow` 由底部属性或同步命令维护。窗口必须落在可切换 phase 内：Active、Recovery、Loop、Channel、Hold、Exit；拖拽范围 clamp 在 `[0, duration-1]`。
- **快捷键**（时间轴聚焦且选中动画事件）：`Cmd/Ctrl+Shift+]` 对齐 Clip 末帧；`Cmd/Ctrl+Shift+[` 对齐动作 `durationFrames - 1`。
- **属性同步**：底部属性修改后时间轴条/标签即时刷新；拖拽帧范围会 clamp 在 `[div, duration-1]`。
- **播放头**：拖红色把手或单击标尺/轨道空白设置当前帧；工具栏 **播放 / 暂停 / 停止 / 速度**；播放时按 ~60fps 推进，末帧后循环；工具栏显示 `帧 n / duration`；「添加事件」落在当前播放头帧。
- **播放高亮**：播放或 scrub 时，当前帧命中的阶段条与事件条高亮，其余条略透明。

The editor must support no-typing workflows where practical:

- Intent ids should be selected from known input/action intent definitions when available.
- Animation action keys should be selected from the active `MxAnimationSetDefinition`.
- Combat action ids should be selected from exported combat action registry.
- Equipment slot ids should be selected from the current character/equipment schema. Timeline hit trace windows choose the equipment slot; the weapon trace profile, locator, radius, and target mask are resolved from the equipped weapon.
- Requirements should use dropdowns for known enums and resource/status ids.
- Phase and animation frame ranges are editable in the bottom inspector and by dragging clips on the timeline.
- RootMotion belongs to `motionTrack.rootMotion`, not to `animation_set_definition.json`. Authors add it from the Motion track with `Add RootMotion`; the RootMotion source is selected from Animation track events and writes `sourceAnimationEventId` / `sourceAnimationClipResourceKey` automatically. The Motion track must show the RootMotion clip from the source animation range before baking, then from baked samples after generation. A single RootMotion channel may be baked from multiple Animation events: `Bake Source Range` replaces only the selected source event's frame range, while `Bake All Animation Events` rebuilds one merged `frames[]` stream from all usable Animation events. Range-level RootMotion adjustments edit baked samples directly through scale/zero operations and are visible in preview/runtime because `frames[]` is the authority.

### 用户可见配置控件准则

- 面向策划和开发者日常使用的配置，不应要求直接手填字符串 ID。Intent、Action、Animation、Combat、Equipment Slot、Resource、Tag、Profile 等引用字段优先使用下拉框、搜索选择器、对象选择器或从上下文自动生成。
- 字段名、按钮、HelpBox、校验提示和 timeline summary 默认使用中文；技术标识只作为括号内补充或兼容提示展示，例如 `主手 (mainHand)`。
- 对非 CharacterAction 领域的未知字符串引用应保留原值并提示；旧字符串 CharacterAction ID 属于已淘汰 schema，必须以稳定 diagnostic 阻断，不能按显示名猜测或静默改写。
- 只有 stable id、事件 id、debug marker 等确实需要精确技术标识的字段才保留文本输入；能通过上下文生成的 stable id 应由编辑器自动填充，人工编辑入口要有说明。
- 不同事件类型只显示当前事件实际使用的参数。武器判定窗口显示 `装备槽位`，不让用户手填判定配置 ID；旧 `traceProfileId` 只能作为兼容说明显示，实际配置由装备槽位解析到当前武器。
- 所有选择控件都应提供合理默认值、空值提示、未知值提示和导出前校验，优先让用户知道“为什么不能运行”和“该去哪里修配置”。

---

## 7. Validation Rules

Validation must be split into local and cross-module gates.

Local CharacterAction validation:

- `actionId` must be globally unique and positive；`0` 只允许作为可空引用，不允许注册为正式动作。
- 全局目录、owner library、保留号段、源路径、active/retired allocation 与源库必须一致。
- `CharacterActionSetConfig.commandBindings[].actionId`、sequence、ability、reaction 与 landing 引用必须解析到 `CharacterActionId`。
- `durationFrames` is required for `CharacterAuthored` actions unless an explicit fallback policy is selected.
- Phase ranges must be ordered, non-negative, and inside duration.
- Track event frames must be inside action duration or resolved combat timeline duration.
- Animation track events: `endFrame >= frame` and `endFrame < durationFrames` when duration is known.
- Track event kind must match track type.
- Interrupt rules with a target must reference a valid `CharacterActionId`。Transition windows (`cancelRules`) only validate request/category and slot masks.

Cross-module validation:

- `CombatTrackConfig.CombatActionId` must resolve to combat runtime data for `CombatAnchored` actions.
- `AnimationTrackEvent.AnimationActionKey` must resolve to an action binding in the active `MxAnimationSetDefinition`.
- Presentation resource keys must resolve through `Resources` catalog.
- Equipment tag requirements must be satisfiable by known equipment state data.
- Character/equipment state references must resolve through `Character.Application`.
- Jump and airborne actions must not require combat-only data unless explicitly marked as hybrid.

Error policy:

- Missing action id: error.
- Missing animation action key: error for playable/runtime entry, warning only for pure data draft.
- Missing combat action id on `CombatAnchored`: error.
- Placeholder animation clips in framework samples: warning with explicit placeholder marker.
- Duplicate stable ids: error.

---

## 8. CharacterTest Integration Standard

`CharacterTest` is framework-level, not a disposable demo. Its action integration must use the formal path.

Required flow:

```text
Framework input snapshot
  -> Character action intent request
  -> CharacterActionResolver
  -> CharacterActionExecutionGateway
  -> CharacterActionTrackAdapter
  -> Motion / Combat / Gameplay / MxAnimation / Presentation sinks
```

CharacterTest must not:

- Hardcode jump or attack animation clip paths.
- Directly play attack/jump animation from input systems.
- Create a separate action enum that competes with `CharacterActionCategory`.
- Store combat hit timing in CharacterTest-only code.
- Treat character package animation clips as owned by the character prefab.

Current bridge:

- `CharacterTestCharacterActionConfigSource` first looks for `character_action_profile.json`.
- When a profile is present, it loads only the ActionSet ids and Action Library ids listed by that profile.
- If no complete external profile/set/library source exists, CharacterTest falls back to embedded sample data and reports the active source in diagnostics.

---

## 9. Relationship with Existing Systems

### 9.1 Combat Authoring

Use Combat Authoring for:

- Attack total frames when combat timeline is authoritative.
- Startup / active / recovery combat phases.
- Hitboxes, hurtboxes, weapon traces.
- Combat preview, explain report, and runtime combat JSON export.

Do not put combat shapes in CharacterAction.

#### 9.1.1 基础 Shape Scene Handles

Unity 菜单为 `MxFramework/Combat/Combat Authoring`。本节记录现有 Sphere / Capsule 基础编辑；数据归 `CombatActionAuthoringAsset`，CharacterAction 不拥有第二份 shape 配置。

- `添加 Hitbox` / `添加 Hurtbox` 在当前帧创建默认 Sphere，并自动选中对应 timeline 行。创建前必须取得可用的 Binding marker：优先首个 Actor 的有效 marker，再取 Binding 中可用的 marker；已有 Binding 可从当前 Scene 候选补齐，仍缺失时显示提示并停止添加。
- 选中行通过 section、trackId 和 SerializedProperty path 同步到 Scene View。选中的 Sphere / Capsule 可移动中心和半径；Capsule 还提供高度端点 Handle，其他行只显示普通 Gizmo。
- `localCenter` 保存 marker-local Unity Vector3；`radiusRaw` 和 `heightRaw` 保存 fixed raw 整数。Handle 写回 Action Asset，记录 Unity Undo 并标记 dirty；它不写 Runtime 状态。
- 旧 asset 未填写 `localCenter` 时保留零中心；`heightRaw = 0` 使用 legacy Capsule 预览高度。Validator 要求半径为正、帧范围合法，并对正高度小于直径的 Capsule 给出 warning。预览与高度 Handle 会将显示 / 写回高度限制到不小于直径。
- 缺失 Binding 或 marker 由现有提示 / Validation 处理；没有可解析的 marker Transform 时不构造可编辑 Handle。

源码入口：项目 Editor 的 `CombatAuthoringWindow.SceneBinding.cs`、`CombatGizmoDrawer.cs`，以及 package 内 `CombatActionAuthoringAsset.cs` / `CombatAuthoringValidator.cs`。以上是当前操作和数据说明，历史 M10E 验收记录不作为新测试结果或提交门禁。

#### 9.1.2 Preview Explain 的当前边界

当前 Combat Authoring 窗口保留 `预览 Explain` / `复制 Explain`，从所选 Action、Binding、frame 和可解析的 marker 构建 `CombatAuthoringPreviewReport`。报告包含 query、candidate、resolve、physics debug、reason 和 stable hash；DTO 按显式 Frame / ID / SourceOrder 等稳定 key 排序，缺 Action、Binding、marker 或 active shape / trace 时提供原因。

Explainer 复用 `CombatPhysicsWorld`、`WeaponTraceQueryBuilder` 和 `HitResolveSystem`。当前报告仍固定为 `Authoring Preview v0`、`HasRuntimePreview=false`：使用 authoring defaults 和当前 blade capsule，缺少完整 Runtime WeaponTrace 历史、真实 damage / target state 和 action instance 数据。报告中的命中解释不能作为这些完整运行合同已经接通的证明。

#### 9.1.3 JSON 草案导出与哈希

当前 `导出 JSON` / `复制导出报告` 调用 `CombatAuthoringJsonExporter.Export(...)`：Validation Error 返回失败且不生成 package files；Warning 可继续并写入报告。英文 JSON key 与数组排序由 exporter 维护。成功 package 含以下六类文件：

- `manifest.json`
- `schema/combat_authoring.schema.json`
- `actions/action_<id>.json`
- `scene_bindings/<规范化 binding 名>.json`
- `reports/validation_report.txt`
- `reports/validation_report.json`

manifest 保存 packageId、version、schema、schemaVersion、createdAt、toolVersion、sourceAssetGuid 和 contentHash。report / context 区分 authoringHash、runtimeDataHash、jsonPackageHash 与 contentHash；当前 contentHash = jsonPackageHash，由按 path 排序的 schema / action / binding 文本计算，不包含 manifest 的 createdAt 或 reports。完整文件字节与每个文件的 hash 不等于这个稳定内容 hash。

exporter 先生成内存文件包；Editor 负责 GUID、目录选择、写盘和复制报告。取消目录选择会保留内存结果，写盘异常显示在 Editor Write 状态。这个入口是 authoring JSON 草案导出，不表示正式 Runtime package loader、binary snapshot、外部 CLI 或 Mod 发布平台已经实现。

#### 9.1.4 Showcase 预览接入边界

当前 `运行 Showcase 预览` / `清除预览会话` 仍由 Editor-only `CombatAuthoringShowcasePlaySession` 提供。启动前 Validation Error 阻断、Warning 显示；Action 必须已保存并有稳定 GUID。SessionState 保存 Action / Binding GUID 和验证摘要，切 Scene 前询问未保存改动，取消则停止启动；目标是仓内仍存在的 `Assets/Scenes/CombatAnimationPhysicsTest.unity`。

进入 Play Mode 后，桥接找到或临时创建 `RuntimeCombatShowcaseRunner`，临时对象使用 DontSaveInEditor；成功应用后清除 pending session，手动清除入口可移除会话。Runtime 配置入口仅接收来源摘要、ActionId、首个 TraceId、Player / Enemy marker、validation 和 marker 摘要，不依赖 UnityEditor。marker 解析先用 Binding actor markerId / targetPath，再以 `Combat_Player_Marker` / `Combat_Enemy_Marker` 回退并显示 fallback / error；解析阶段不会静默创建 marker。

HUD / event log 显示来源、ID、validation 和 marker 摘要；现有鼠标手测属于 Demo。该桥接不加载导出的 JSON，也不把当前 authoring shape、damage 或 action timeline 全量加载到 runner，不能替代完整 Runtime 数据接入或新的 Playable 验收。本节为当前源码和入口核查，未在本轮运行 Unity / PlayMode。

#### 9.1.5 Validation Quick Actions 的当前边界

现行 Validation Report 的 issue row 显示 severity、来源 section / field、message、suggested fix 和中文操作按钮。详情可换行、按钮可折行；Inspector 模式将 Issues 与 Report Preview 上下排列并保留列表高度。这是当前布局实现说明，本轮未重新验证窄窗口表现。

- `适配总帧数`（FitTotalFrames）：按 Startup / Active / Recovery、Hitbox / Hurtbox、WeaponTrace 的当前最大有效结束帧计算至少 1 的总帧数；只扩展不足的 TotalFrames，不缩短已有值。
- `修正帧范围`（ClampFrameRange）：将目标 start/end 限定到 `[0, max(0, TotalFrames - 1)]`，start > end 时令 end = start。Action phase 按 section / field 定位；Shape / Trace 按 TrackId，优先匹配 SourceOrder，找不到该顺序时回退到首个同 ID 元素。
- `定位目标`（SelectIssueTarget）：按 section、Action phase property 或 TrackId 定位 row，同步 Timeline / Inspector 与所选帧；无 row 时回退到 Asset 定位并提示。`定位 Asset` 优先匹配 issue 的 Action / Binding 来源，否则回退到当前 Asset。
- `使用当前选择重连`：要求选中现有场景 Transform / GameObject，拒绝 Project 资产；在 Binding 登记 markerId / hierarchy path，并写回所选 Shape markerId 或 Trace root/tip markerId。它不创建或移动 Scene Transform。`CreatePreviewMarker` 当前仅走定位与处理提示，要求手动重连，不能视为自动创建 marker 已实现。

修改 Action / Binding 时走 Undo、dirty 与现有刷新链路，包括 `CombatAuthoringSceneState.NotifyDataChanged()`、validation、Inspector / Timeline 和 Scene View 重绘。缺资产、目标或不支持的操作有中文提示；按钮 callback 捕获 issue 值，避免绑定过期列表 index。

这些按钮属于 Editor 内的逐项 authoring 操作；不代表批量修复、外部 CLI quick action、Runtime 数据加载或 JSON key 修改能力已经提供。源码入口为项目 Editor 的 `CombatAuthoringWindow.ListsIssues.cs`、`.SerializedFields.cs`、`.DetailShapes.cs`、`.SceneBinding.cs` 和 `.UI.cs`。历史测试数量、手动验收与提交约束不作为当前测试结果或新门禁。

#### 9.1.6 Shape 详情控件

选中 Hitbox / Hurtbox 后，现行 Inspector 提供 Sphere / Capsule / Aabb / Sector 下拉，显示中文名称和英文 key；修改通过 SerializedProperty 写回。Marker 下拉显示 Binding 中的 markerId 和缩略 targetPath，保留高级文本输入、当前值缺失占位及窗口内 HelpBox。占位项不写回；没有 Binding marker 时提示创建或选择 Binding。`使用当前选择重连` 的作用域见 §9.1.5。

Start / End 使用延迟 IntegerField 和 ±1 stepper；控件修改时将两端限制到 `[0, max(0, TotalFrames - 1)]`。新 start 超过 end 时令 end = start，新 end 小于 start 时令 start = end；已有非法范围先提示，不因打开面板而静默改写。radiusRaw / heightRaw 提供 SliderInt、输入和 preset，使用 `1,000,000 raw = 1 Unity unit`；半径最小为 1 raw，高度最小为 0。半径 preset 为 0.25 / 0.5 / 1 unit，高度为默认 0 / 0.5 / 1 / 2 unit。

Capsule 正高度小于直径时显示 warning 和 `高度=直径` 操作；heightRaw=0 的旧资产预览语义仍见 §9.1.1。控件修改支持 Undo / dirty、validation 和 Scene View 刷新；Timeline 刷新保留可重新定位的 selection，编辑字段获得焦点时避免重建详情打断输入。该节不扩展 WeaponTrace root/tip 选择器或外部编辑器。

#### 9.1.7 Timeline 帧范围拖动

当前非空 Startup / Active / Recovery、Hitbox、Hurtbox 条块支持主体 Move、左边 ResizeStart、右边 ResizeEnd；空 range 只显示 empty，不建立拖动条。边缘 handle 和中文状态显示预览范围。坐标按 track 原点及 frameWidth 映射并取整、clamp；Move 保持长度并在合法范围内移动，两种 Resize 均阻止 start > end。

拖动中只更新当前条块、标签和预览帧，不每次 pointer move 重建整棵 Timeline，也不把 UI 状态作为数据权威。释放时有变化才通过 SerializedObject / SerializedProperty 写回 Action、记录 Undo / dirty，刷新 validation、详情、共享 DataRevision 和 Scene View。Escape / pointer cancel 走取消恢复，不提交范围；root pointer-up 和非结束过程中的 capture 丢失按当前实现完成拖动，有变化时提交。该入口不编辑 WeaponTrace range 或 root/tip，不提供空 lane 创建、多选、批量移动、复制粘贴。

#### 9.1.8 Shape 复制与删除

Hitbox / Hurtbox 的 `Shape 操作` 区提供 `复制 Shape` / `删除 Shape`；Delete / Backspace 同样仅处理所选 Shape。事件来源或当前焦点位于文本、数值、slider、vector 等可编辑字段（包括输入子元素）时跳过键盘删除；Action phase 和 WeaponTrace 不是这两个操作的目标。删除不弹确认框，显示中文结果或失败原因，可用 Undo 恢复。

删除按 TrackId 定位数组项，预先选择同组下一个，否则上一个；组内已空时清空选择。复制在当前组数组末尾插入副本，保留 shapeKind、frameRange、markerId、localCenter、radiusRaw、heightRaw，生成当前组新的 TrackId，并将 SourceOrder 设为组内最大值 + 1，随后选中新 Shape。二者均通过 SerializedProperty Apply 写回，支持 Undo / Redo、dirty、Timeline / Inspector / validation / Scene View 和共享 DataRevision 刷新；不扩展批量复制删除、拖拽复制或空白处创建。

#### 9.1.9 双窗口与共享状态

当前项目 Editor 菜单为 `MxFramework > Combat > Combat Authoring`、`Combat Timeline`、`Open Authoring Layout`；最后一项同时打开两窗口，Inspector 中也有 `打开 Timeline`。同一个 CombatAuthoringWindow 的 Inspector / Timeline mode 复用编辑逻辑。Inspector 最小尺寸为 420×620，整体垂直滚动，保留 Action / Binding、工具、Frame、属性、issues / report / Explain；Timeline 最小尺寸为 900×360，以横向 strip、frame scrubber、上下文及 row list 为主，不复制主 Inspector 布局。

`CombatAuthoringSceneState` 共享 Action、Binding、Frame、Selection 与 DataRevision。上下文、帧和选中项实际变化时发 Changed；数据修改显式 NotifyDataChanged。窗口订阅 / 退订 Changed 与 Undo/Redo，并以 SetValueWithoutNotify、修订号及本地刷新保护避免互相反复触发，保留编辑焦点。添加、复制删除、range drag、字段和 Binding 修改通过现有链路通知另一窗口；Frame 同步驱动 playhead 和 Scene gizmo。这不是自动 docking 布局或新的 GraphView / 外部编辑器。

§9.1.6～§9.1.9 为当前源码合同承接，入口来自项目 Editor 的 `CombatAuthoringWindow.cs`、`.UI.cs`、`.DetailShapes.cs`、`.Timeline.cs`、`.NestedTypes.cs`、`.ContextValidation.cs` 和 `CombatAuthoringSceneState.cs`。本轮未运行 Unity、手动窗口或 EditMode 测试；历史 11/11、烟测和 SVN 提交边界不成为新的验收结果或门禁。



### 9.2 MxAnimation

Use MxAnimation for:

- Clip registry.
- Shared animation resources.
- Animation set definition.
- Action binding playback.
- Blend tree locomotion.

CharacterAction stores only `animationActionKey`, not clip resource paths.

### 9.3 Character.Application

Use Character.Application for:

- Selecting active CharacterAction profile from character id, equipment state, weapon state, or other composition context.
- Resolving default equipment.
- Connecting combat action set and animation action set.
- Reporting missing bindings during package load.

CharacterAction should not decide which prefab or weapon asset to spawn.

### 9.4 Input

Input should produce intent ids such as:

- `Move`
- `Jump`
- `LightAttack`
- `HeavyAttack`
- `Guard`
- `Dodge`
- `Interact`

Intent ids are command-level names, not animation names.

---

## 10. Suggested Implementation Milestones

### Milestone 1: Data DTO and JSON importer

Deliverables:

- Serialized DTO types.
- `CharacterActionJsonImporter`.
- Roundtrip tests for action set and action config.
- Diagnostics for missing and duplicate ids.

Acceptance:

- JSON can produce the same `CharacterActionSetConfig` and `CharacterActionConfig[]` currently hand-built in tests.

### Milestone 2: Validation and registry

Deliverables:

- `CharacterActionConfigRegistry`.
- Local validation gate.
- Cross-module validation interfaces.
- Deterministic validation report.

Acceptance:

- Missing action, missing animation action key, missing combat action id, duplicate stable id, and invalid frame ranges produce stable diagnostics.

### Milestone 3: CharacterTest temporary provider replacement

Deliverables:

- Replace C# temporary provider with JSON-backed config source.
- CharacterTest builds resolver context from loaded character package/action config.
- Debug panel shows active action, action phase, local frame, last reject reason, and animation action key.

Acceptance:

- Jump and light attack requests go through `CharacterActionResolver` and the stateful `CharacterActionExecutionGateway`.
- Animation playback uses `MxAnimationActionBinding`.

### Milestone 4: Unity editor MVP

Deliverables:

- `MxFramework/Character/Character Action Authoring` menu.
- Top-level Profile source selector.
- Profile tab ActionSet dropdown, Action Library multi-select list, and active Library dropdown for editing one selected action library.
- Action set and action list editing.
- Phase, transition window, and track event editing.
- Validation panel.
- Save / Save As for profile, action set, and action library JSON.

Acceptance:

- Developer can create jump and light attack action data without writing C#.
- Developer can view and adjust transition window `startFrame/endFrame` on the timeline without editing JSON.
- Editor blocks export on errors.

### Milestone 5: Cross-module authoring polish

Deliverables:

- Animation action key picker.
- Combat action id picker.
- Resource key validation.
- Character package integration.
- Workstation preview embedded in editor.

Acceptance:

- Character action data can be authored, validated, exported, loaded by CharacterTest, and observed in runtime debug UI.

### Milestone 6: Provider-backed reference discovery

Deliverables:

- Editor-only reference discovery service.
- MxAnimation registry and ResourceCatalog provider discovery.
- Combat/Character.Application config object discovery where provider data is available.
- “映射与方案 → 引用数据源” diagnostics and unavailable-provider reporting.
- Manual reference fallback preserved.

Acceptance:

- References can be populated from provider-backed framework data without moving ownership into CharacterAction.
- Cross-module validation still runs through `CharacterActionValidationReport` and the existing provider interfaces.

### Milestone 7: CharacterTest exported JSON load path

Deliverables:

- CharacterTest runtime source resolution for editor-exported `character_action_profile.json`, `character_action_sets.json`, and `character_actions.json`.
- Explicit embedded sample fallback when no external ActionSet + Action Library data exists.
- Runtime/debug diagnostics that identify the active config source and invalid external JSON.

Acceptance:

- CharacterTest consumes editor-exported JSON through `CharacterActionJsonImporter`, `CharacterActionConfigRegistry`, and `CharacterActionValidationReport`.
- Invalid complete external JSON blocks readiness with stable diagnostics instead of being silently replaced by embedded sample data.

### CharacterTest transition-window validation slice

CharacterTest light attack follow-up is the first validation slice for the generic transition-window model, not a separate combo system. `light_attack -> light_attack_2 -> light_attack_3` is expressed by LocalInput transition windows:

- `light_attack` owns a transition window that allows `sourceKind=LocalInput` to switch to `light_attack_2`.
- `light_attack_2` owns a transition window that allows `sourceKind=LocalInput` to switch to `light_attack_3`.
- `light_attack_3` has no fourth follow-up by default; finishing it or starting any non-basic-attack action resets the CharacterTest combo selector.

The CharacterTest combo selector only chooses the requested target action from current action state and buffered input. It must not bypass transition-window checks, and it must not move combo state into `AbilityRuntimeModule`. Ability runtime remains the grant / availability / cooldown authority and commits cooldown or future resource costs only after action start succeeds.

Implementation note: CharacterTest runtime resolves the requested follow-up and submits it through
`CharacterActionExecutionGateway`. Normal intent/slot requests use the current action's authored transition window;
reaction requests use `interruptRules`. Empty `interruptRules` deliberately mean that ordinary Hit、CohesionBreak、
GuardBreak、ArmorBreak or generic Reaction cannot interrupt the action. Only Death/lifecycle teardown may force-end an
active action. Death/Revive replacement must use `ReplaceForLifecycle`: the target is fully resolved and validated before
the old instance changes, then old termination and new start are written to the same event buffer. Landing is not a
forced cancel; it uses `CompleteExternal(expectedActionId, Landed)` and produces a normal finish event. Generating action
records is not sufficient, and first-party runtime code cannot access Runner start/cancel/interrupt/force-end methods.

Input-side “Cancel” is only a device intent name. If the desired gameplay operation is “返回移动”, author it as a concrete
target CharacterAction and add an explicit transition window from each action that may enter it. Do not create a generic
Cancel action, do not bind `payload0=0`, and do not use a force-start flag to implement repeated attacks. Repeated attacks
use explicit self/follow-up transition windows; Gameplay abilities are emitted by Gameplay Track after the outer action
has actually started.

A CharacterTest Unity Editor sync menu may create or refresh the three light attack baseline actions because root motion bake needs Unity animation resources such as `katana_atk1/2/3`. The sync path should generate or repair missing baseline data and refresh derived root motion, but must not overwrite manually tuned transition windows, hit windows, damage payloads, audio/VFX events, or other timeline tuning. CLI support can validate or batch pure JSON later; it is not the first-version root motion authority.

---

## 11. Naming Rules

Use stable ids consistently:

| Concept | Example |
| --- | --- |
| Action profile | `codex_flow_test.default`, `humanoid.default`, `one_hand_sword.default` |
| Action set | `codex_flow_test.sword_shield`, `humanoid.default`, `one_hand_sword.default` |
| Action | `light_attack`, `basic_jump`, `guard_start` |
| Input intent | `LightAttack`, `Jump`, `Guard` |
| Animation action key | `anim.light_attack`, `anim.basic_jump` |
| Combat action id | `combat.light_attack` |
| Trace profile id | `trace.light_attack` |
| Debug event id | `light_attack.start` |

Do not use clip names as action ids.

Correct:

```text
LightAttack -> light_attack -> anim.light_attack -> art.character.skeleton.animation.sword_slash_01
```

Incorrect:

```text
LightAttack -> standing_jump -> standing_jump.anim
```

---

## 12. Documentation Sync Requirements

When this authoring plan is implemented, keep these files synchronized:

- `Docs/CAPABILITIES.md`: update Character Action capability status.
- `Docs/USAGE.md`: add runtime loading and CharacterTest usage.
- `Docs/Interfaces/CharacterAction.md`: document new importer/editor/config source APIs.
- `Docs/Guides/CHARACTER_ACTION_AUTHORING_GUIDE.md`: keep authoring workflow current.
- `Docs/Guides/CHARACTER_TEST_GAME_DEVELOPMENT_STANDARD.md`: keep CharacterTest integration standard aligned.

---

## 13. Open Decisions

These are intentionally unresolved until implementation starts:

- Whether the primary editable asset is JSON-first, ScriptableObject-first, or dual-source with one exported runtime JSON.
- Whether future production profiles should be selected directly by Character.Application or by a separate runtime composition service.
- Whether motion track impulse values should be raw floats or fixed-point runtime values.

Do not resolve these by hidden implementation. Record the decision in an ADR or this guide before coding.
