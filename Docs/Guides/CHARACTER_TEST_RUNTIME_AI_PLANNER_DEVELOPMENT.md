# CharacterTest Runtime AI Planner Development Guide

> Status: Guide
>
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/RuntimeAiPlanner/CharacterTestRuntimeAiPlannerRuntime.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestBehaviorExecutionEvaluator.cs`
>
> Date: 2026-07-17
>
> 本文的 AI 指 **Runtime AI Planner**：`MxFramework.AI` 模块和 CharacterTest 专用运行时行为决策适配层。本文不记录 AIAction Config、Authoring AI Assist 或 Development Agent 工作流。

2026-10-12：BehaviorExecution evaluator 增加 `NaturalCompletionRequested`，仅整卡自然时间线结束、显式 ReturnToPlanner 和无后继的终结动作块设置。外部 `Stop(false)` 即使旧状态为 Completed 也不等于自然成功；黑洞口袋据此冻结终止帧及中心。typed clone/restore 保留该事实；为true时 execution hash 使用v6、字段77，为false时省略并保留旧hash布局。Host在实际终止边界发布来源，恢复不能靠传入JSON伪造可信配件提交。第一批实际ACK通道同时登记所有三卡的共同 `TacticalDirectAttackStamp`，不以是否装破碎幻想为登记条件。原生验证尚待统一协调者，见[实施交接](../Tasks/TACTICAL_DOUBLE_EDGE_ACCESSORY_IMPLEMENTATION.md)。

## 定位

本文记录 CharacterTest Runtime AI Planner 的开发脉络、当前架构、关键边界、已完成切片和后续路线。它是面向主创、程序和 Development Agent 的长期 Guide，不是单次任务归档。

当前事实源仍以源码、测试、`Docs/Interfaces/AI.md`、`Docs/CAPABILITIES.md` 和 `Docs/USAGE.md` 为准。本文用于解释 CharacterTest 如何把 Runtime AI Planner primitive 组合成可编辑、可调试、可验证的垂直切片。编辑界面的持续开发记录见 `Docs/Guides/CHARACTER_TEST_RUNTIME_AI_PLANNER_EDITOR_DEVELOPMENT.md`。

CharacterTest Runtime AI Planner 的范围是游戏内 NPC 行为决策：

- 用 HFSM 管大状态，用 Utility 在当前状态内选择动作。
- 用 CharacterTest 专用 JSON profile 表达状态、切换、感知参数、facts 条件、Utility actions 和 command 输出映射；CharacterTest 内部行为组层可以低频编译成同一份 v3 profile 结构。
- 在运行时输出 deterministic runtime command intent，而不是直接写角色输入或 Unity 对象。
- 在 Editor 内提供结构化编辑和 Play Mode 只读监控。

## 当前状态

当前 CharacterTest Runtime AI Planner 已形成 v3 垂直闭环：

- Profile 真源是 StreamingAssets JSON，默认路径为 `Assets/StreamingAssets/MxFramework/CharacterTest/runtime_ai_planner/default.json`，schema 为 `mx.character-test.runtime-ai-planner.profile.v3`。
- v3 profile 在 `movement` 区新增默认移动风格，并允许 action 级 movement override；运行时在 session build/compile 阶段解析为数值结构，Tick 内只做 deterministic movement intent 计算。
- 场景入口只保存 StreamingAssets 相对路径；代码和测试仍可通过 `CharacterTestSliceOptions.RuntimeAiPlannerProfile` 直接注入 DTO。
- Runtime session build 阶段低频读取 JSON、校验并编译 profile；高频 Tick 路径不读文件、不解析 JSON。
- `CharacterTestRuntimeAiPlannerCommandModule` 在 `PreSimulation` 读取上一帧 CharacterTest world，生成 `RuntimeAiPlanner` source 的 `SetControlIntent` / skill-slot intent，再由现有 command module 应用。
- Editor 菜单为 `MxFramework/Character/Runtime AI Planner`，主入口是行为 Profile 库选择器：自动扫描默认 StreamingAssets 目录、显示中文模板名/文件名/profile id/校验状态，并支持打开、新建模板、复制为、设为场景默认、校验、保存和复制报告；文件路径选择只作为高级入口保留。
- Editor 条件编辑已从 raw enum 升级为 facts 分类下拉、类型化 value 控件、常用条件模板、局部诊断和安全快捷修复；状态页提供只读流程图预览，可点击状态/切换定位 Inspector。
- 当前内置行为 profile 包包括 `default.json`、`perception_melee.json`、`ranged_kiter.json`、`hazard_skirmisher.json`、`skill_caster.json`、`aggressive_berserker.json`、`defensive_guardian.json`、`ambusher.json` 和 `controller_zoner.json`；一次 CharacterTest 会话仍只使用场景绑定的一套 JSON profile。
- Runtime AI Planner 窗口的行为组能力已从“全库模块勾选”改为“只读预览 + 行为组编辑”模型。`行为组预览` 只选择当前草稿、内置默认组或默认 Loadout variant 做低频编译预览；`行为组编辑` 搜索行为定义库并编辑当前组 entries。CharacterTest internal `BehaviorDefinitionCatalog` 提供行为定义库，`BehaviorGroup` 只保存角色/策略实际拥有的 entries，并编译为现有 v3 `CharacterTestRuntimeAiPlannerProfile`；内置定义覆盖 `Targeting.NearestEnemy`、`Movement.ApproachTarget`、`Movement.KeepDistance`、`Movement.StrafeWhenBlocked`、`Survival.AvoidHazard`、`Combat.PrimaryAttack`、`Combat.CastAbilitySlot` 和 `Fallback.WaitOrHold`。
- Play Monitor 只读显示 agent snapshot，包括 active state path、target、facts、utility score/cost、selected action、move style、last command、reject/suppress reason、recent transition 和决策原因摘要。

### 行为卡当前实现基线

Runtime AI Planner 行为卡当前已经有 Editor-only 原型闭环，但它仍不是 gameplay authority。当前基线是：

- `Behavior Card Draft -> Step Template -> Step Exit / Branch -> Flowchart Preview -> BehaviorGroup entry future integration` 的编辑器分层已经建立。
- `行为卡` 页只编辑和预览 Editor 内存草稿。草稿不保存为正式 JSON，不发布到 behavior definition catalog，不进入 BehaviorLoadout，不参与 Arena 开战、SaveState、network snapshot、replay/result hash，也不驱动 runtime Tick 执行。
- 当前首个样例是 `突进追击`：4 个动作步骤，结构化分支，显式判断节点，失败/中断与条件不满足路径汇入 `End / 交回行为组重新决策`。
- 主图使用自动布局分支流程图表达“动作 -> 判断 -> 分支 -> End”。动作节点只显示执行摘要、友好动作名、持续时间/关键窗口和必要 warning；完整条件、raw id、AbilityId、CharacterActionId、窗口编辑字段和诊断放到侧栏 Inspector。
- 行为卡页采用单侧栏模型：未选中流程组件时显示行为卡库；选中动作节点、判断节点或连线标签时，同一侧栏切换为标准化步骤/分支 Inspector；点击主图空白处取消选择并回到行为卡库。
- 标准化 Inspector 以步骤模板、条件预设和出口规则为主，不再要求主创手填 raw `ConditionSummary`、裸持续帧或手写 End reason。当前模板覆盖突进能力、普通能力、近身攻击、连段后续、移动接近、等待和 End 回流的编辑语义。
- 结果预估、时长/窗口预览和断链 warning 都是 Editor 低频解释数据。Ability / CharacterAction 时间摘要只读引用现有动作数据；行为卡不会复制或替代 CharacterAction 时间线。

这段 Editor-only 流程图是历史基线，不再代表当前 Logic v2 完成度。当前已经具备行为卡草稿持久化、schema/migration、validator/compiler/evaluator、typed RuntimeFrame events、Warm Replay、session-local `Apply F+1`，以及已验收的 internal Published Card、Installed Deck、Unlock And Persistence、Session And Authority foundations。阶段 5 已接通 CharacterTest 本地玩家卡库/卡组 FairyGUI、每角色存档、战前 authority 固定、独立 profile slot、Logic v2 Runtime Overlay 和战斗 HUD typed 切换原因；角色观察选择在战斗锁定后仍可切换，但卡组编辑保持锁定。尚未完成的是 production catalog 分发与审批、正式账号/解锁数据接线、面向产品的房间/匹配配置与联网玩家卡组命令。旧 `07-11` 需求切片建立在早期 UI 假设上，只能作为历史输入。

### Runtime AI Lab 独立场景方向

行为卡 Editor-only 原型已经证明静态编辑界面可以表达流程和分支，但下一阶段的主要风险不在于继续增加字段控件，而在于运行时效果是否能被逐帧观察和快速验证。后续 Runtime AI Planner 开发先转向独立运行时开发场景 `CharacterTestRuntimeAiLabScene`：在真实 CharacterTest session 中暂停、单帧、慢速播放，读取真实 debug snapshot 和 `CharacterTestPerceptionFrameCache` 派生数据，场景内绘制目标线、LOS/path、attack range、move intent、hazard/obstacle 摘要，并允许显式 debug gate 下的当前会话临时调参。

Runtime AI Lab 的历史场景入口是 `Docs/Tasks/CHARACTER_TEST_RUNTIME_AI_PLANNER_RUNTIME_AI_LAB_SCENE_08.md`；当前统一产品合同见 `CHARACTER_TEST_RUNTIME_AI_PLANNER_BEHAVIOR_CARD_GAMEPLAY.md`。Lab 界面不是玩家玩法场景，也不是 `MxFramework.AI` 公共编辑器，但它必须设计和验证玩家使用的同一套 Published Behavior Card，并默认先显示玩家装配层。Lab draft 和 current-session evaluator overlay 只用于开发者验证：编译成功后可替换当前测试会话 evaluator，失败则保留旧 evaluator；Apply 会记录不可变的 application RuntimeFrame、profile slot、完整 composition authority hash 和 profile 快照，并作为 Lab seek / A-B / cold verification 的会话回放输入。`Apply F+1` 仍不进入正式 SaveState、network snapshot 或 production catalog；Publish 是独立验证门禁。

默认近战 profile 仍保持“追击 + 轻攻击”兼容行为，但移动输出不再是纯直线：`MoveToTarget` 可轻微弧线接近并在进入距离带时减速，`Strafe` 会按 deterministic seed 和帧区间换侧，`Retreat` 可带侧向偏移。新增远程风筝、危险游斗、技能施法、激进冲锋、守卫防御、伏击者和控制控场 profile 用现有感知 facts、action kind 和 movement settings 演示可切换策略。

## Scene Test Characters

Scene test actor setup is handled by the `CharacterTest Characters` module in `MxFramework/Level Editor`, not by the Runtime AI Planner profile JSON. The module stores `characterTest.characters` inside the existing `mx.level.authoring.v3` level envelope and applies it to `GameManager.CharacterSceneCache`. New scene tests should create characters through the default template, selected transforms, or SceneView placement; legacy world-object point spawns are not a recommended authoring source.

Runtime AI Planner now supports character-level profile overrides for scene tests. A character row still needs `ControlOwner = AiControlled` to participate, but it may also set `runtimeAiPlannerProfilePath`; an empty value inherits the scene-bound `CharacterTestSceneProfile.RuntimeAiPlannerProfilePath`. Non-AI rows preserve a configured path as inactive authoring data without blocking save; the session build path ignores it until control ownership is switched to `AiControlled`. For participating rows, session build loads these JSON files once, assigns deterministic profile slots to startup spawn specs, and the runtime command module selects a precompiled evaluator by slot during `PreSimulation`. This remains a rebuild-to-apply workflow and does not introduce Play Mode hot reload.

Both the Runtime AI Planner window and the `CharacterTest Characters` Inspector use the editor-only behavior library picker for those paths. Known templates use `CharacterTestRuntimeAiPlannerTemplateLibrary` display names; custom JSON uses file name plus profile id. Invalid JSON is still discoverable and can be opened for repair, but the editor blocks binding it as a scene default or character override.

## Arena Test Scene

`Assets/Scenes/CharacterTestArenaScene.unity` is the first playable test harness for Runtime AI Planner behavior loadouts. It is intentionally separate from the Runtime AI Planner editor window: the scene is for choosing published strategies for two sides and then watching a clean offline battle run, not for editing behavior definitions, editing behavior groups, or hot-reloading the current evaluator.

The scene has `CharacterTestArenaSessionController` on the same GameObject as `GameManager`. When the scene enters Play, `GameManager` detects the controller, builds an initial setup session, pauses ticking, and opens the FairyGUI `MxCharacterTestHud/ArenaSetupPanel`. The panel owns only low-frequency match configuration:

- Team 1 and Team 2 each expose three fixed slots.
- Each enabled slot selects a character id, a published Runtime AI Planner behavior loadout/strategy, and a variant.
- The default registry may currently contain only `Codex Flow Test`; the UI still keeps the slot model extensible so later character definitions can appear without changing the session flow.
- The panel never exposes behavior definition ids, facts, score/cost, profile slots, catalog hashes, raw behavior group JSON, or the Runtime AI Planner v3 profile.

`Start Battle` validates that both teams have at least one enabled slot, converts the setup into `CharacterTestCharacterSpawnSpec` values, resolves each selected `loadoutId + variantId` into `CharacterTestRuntimeAiBehaviorSelection`, rebuilds a clean offline `GameSlice`, locks the behavior selection phase, hides the setup UI surface, and resumes ticking. From that point onward combat Tick only reads the already compiled evaluator/profile slot. It does not read behavior JSON, compile behavior groups, build UI strings, or modify selection state.

During battle the Arena panel becomes a compact read-only status surface: alive counts, elapsed time, current result, and reset controls. End-of-match detection is low-frequency and based on runtime character/combat state. `Replay` rebuilds a fresh session from the same config; `Reconfigure` returns to the setup panel and creates a clean paused setup session. This keeps Arena reset semantics deterministic and avoids migrating partial combat state.

The Arena path is CharacterTest internal playable infrastructure. It does not add `MxFramework.AI` public API, does not change Runtime AI Planner JSON schema, and does not introduce combat-time behavior switching. If future gameplay wants progression, equipment unlocks, richer party selection, or battle-time tactical commands, those systems should feed the same low-frequency allowed-list/selection-command foundation instead of directly replacing evaluators or injecting arbitrary JSON.

## 架构边界

Runtime AI Planner core 和 CharacterTest adapter 必须保持清晰分层：

- `MxFramework.AI` 保持 noEngine primitive：facts、blackboard、sensor manager、HFSM、utility planner、action runner 等通用能力。
- CharacterTest adapter 可以复用 `AiFactKey`、HFSM 语义和 Utility tie-break，但不把 CharacterTest JSON schema、Editor 窗口或 profile 字段上推为公共框架 API。
- Profile 不使用 ScriptableObject 作为真源；Unity 场景只保存 JSON 路径，不暴露零散 planner 调参字段。
- 保存后的 JSON 不自动热更新当前 Play Mode runtime；普通 profile 修改仍在重建 CharacterTest 会话后生效。
- `行为组编辑` 页提供显式 debug gate 的会话级 hot update：只允许修改当前行为组实例、实例禁用状态和参数 override，低频重新编译并替换当前 evaluator；失败时保留旧 evaluator。候选 authority hash 必须覆盖 entry identity/order/disabled、所有有效参数、perception/movement/profile 输入和卡片程序；只有卡片与现有 composition evaluator 的规范原子投影完全一致时才允许 Apply。该 overlay 不写回 JSON、不进入正式 SaveState/network snapshot，也不改变 v3 profile schema；但其不可变 revision event 必须进入 Runtime AI Lab 自身的 seek/replay 输入。`行为组预览` 页只读，不触发 hot update。
- Runtime 高频路径遵守初始化后 NoAlloc：不每帧创建 blackboard、sensor manager、planner result、临时集合、字符串报告或 Unity 对象查询。
- Runtime AI Planner 不直接写 entity input；它只提交 runtime command，由 CharacterTest command pipeline 统一校验、应用、回放和诊断。
- Editor 只能通过 JSON draft、validator 和 debug snapshot 工作，不反向修改 Play Mode runtime 对象。

如果未来要把 CharacterTest 的 profile schema 推广成通用 authoring schema，必须另立设计，不应在 CharacterTest adapter 内顺手扩大公共 API。

## 行为组层

Runtime AI Planner 的底层 profile 适合精确排查 HFSM state、transition、Utility action 和条件 fact，但主创直接阅读所有底层字段时不容易理解“这个角色实际拥有哪些行为”。CharacterTest 因此把早期的模块勾选原型收束为内部 **Runtime AI Planner Behavior Group Layer**：行为库只定义可用行为，行为组只保存已经加入到某个角色/策略中的行为实例，再低频编译为现有 v3 profile。

行为组层当前由以下内部类型构成：

| 类型 | 职责 | 分配边界 |
| --- | --- | --- |
| `CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog` | 行为定义库：稳定 behavior id、显示名、分类、tag、required facts、参数、conditions、score/cost、action primitive 和字段映射 | Editor/session build 低频 |
| `CharacterTestRuntimeAiPlannerBehaviorCompositionProfile` | 兼容命名的行为组草稿：`groupId`、`displayName`、`entries[]`、perception/movement/targeting 默认值 | Editor/debug overlay 低频 |
| `CharacterTestRuntimeAiPlannerBehaviorModuleEntry` | 行为组内的单个行为实例：`entryId`、`behaviorId`、`order`、`disabled`、`parameterOverrides` | Editor/session build 低频 |
| `CharacterTestRuntimeAiPlannerBehaviorCompileResult` | 编译结果：生成的 v3 `CharacterTestRuntimeAiPlannerProfile`、validator report、行为实例摘要 | Editor/session build 低频 |
| `CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalogJson` | `mx.character-test.runtime-ai-planner.behavior-definition-catalog.v1` JSON roundtrip | 低频 authoring；不被 Tick 读取 |
| `CharacterTestRuntimeAiPlannerBehaviorGroupJson` | `mx.character-test.runtime-ai-planner.behavior-group.v4` JSON roundtrip；旧 `responsibilityKind/slotKind` 迁移为规范化 `responsibilityKinds[]` | 低频 authoring；不被 Tick 读取 |
| `CharacterTestRuntimeAiPlannerBehaviorCompositionJson` | 旧 `mx.character-test.runtime-ai-planner.composition.v1` 兼容入口，加载后转换为行为组草稿 | 兼容低频入口 |

行为定义不是“当前角色启用了哪些行为”的状态。它只是库条目，数量可以增长到几百或上千；Editor 必须通过搜索、分类、tag 和 action primitive 过滤后展示结果，不能把全库渲染成一组 Toggle。`BehaviorGroup.entries` 保存已安装行为；每条安装记录通过独立 `EntryId` 保持身份，并通过 `responsibilityKind` 归入索敌、接战、输出、兜底或其他职责。职责只用于创作整理和兼容性提示；所有启用且有效的行为进入同一候选、条件、分数与稳定决胜路径。`disabled=true` 只表示这个组内实例当前不可执行，该 entry 仍保留在组内、报告和 Editor 列表中；它不是全局行为库开关，也不是玩法权威状态。

内置行为定义第一批只生成现有 Runtime AI Planner 字段，不新增 `MxFramework.AI` 公共 API，也不新增 action kind：

| 行为定义 | action primitive / 生成影响 | 典型 facts |
| --- | --- | --- |
| `Targeting.NearestEnemy` | 前置目标策略：设置 `NearestOpposingTeam` 并产出目标 facts；不进入行为仲裁 | `target.exists` |
| `Movement.ApproachTarget` | `MoveToTarget`，目标存在、未进攻击距离且路径可用时接近 | `target.exists`、`target.inAttackRange`、`move.pathAvailable` |
| `Movement.KeepDistance` | `Retreat`，目标距离低于阈值时拉开 | `target.distance` |
| `Movement.StrafeWhenBlocked` | `Strafe`，目标遮挡时绕行/侧移 | `target.lineOfSightBlocked` |
| `Survival.AvoidHazard` | 高优先级 `Retreat`，附近危险时脱离 | `environment.nearHazard` |
| `Combat.PrimaryAttack` | `PrimaryAttack` | `target.inAttackRange`、`self.actionRunning` |
| `Combat.CastAbilitySlot` | `CastAbilitySlot` 和技能槽参数 | `target.visible`、`target.inAttackRange` |
| `Fallback.WaitOrHold` | 系统兜底：仅当没有普通行为满足条件时输出 `Wait`，不作为普通计分候选 | none |

### 行为能力分类

行为卡除旧的 `Targeting / Movement / Survival / Combat / Fallback` 粗分类外，还必须声明配置诊断用能力元数据：

- `primaryCapability`：这张卡主要提供的能力。
- `secondaryCapabilities[]`：这张卡附带提供的能力。
- `requiresCapabilities[]`：这张卡要稳定生效时，行为组里应同时具备的能力。
- `countersCapabilities[]`：这张卡适合破解的对手能力。
- `riskTags[]`：这张卡单独使用时的风险，例如 `NoDamageIfAlone`、`NoReengageIfAlone`、`NeedsAbilitySlot`。

能力分类固定为：`Targeting`（索敌）、`Engage`（接战）、`Damage`（输出）、`Pursuit`（追击）、`Control`（控制）、`Kite`（拉扯）、`Defense`（防御保命）、`Navigation`（路径修正）、`Fallback`（兜底）、`Support`（协同预留）。其中 `Targeting`、`Engage/Pursuit/Control`、`Damage` 与 `Fallback` 分别为四个物理基础契约槽提供兼容性依据；其余能力仍用于 Editor、Arena 导航、编译诊断和结果预估。明确不接战或不输出的行为组使用 `Engage.NoOp` / `Damage.NoOp` 占据对应槽，这两个定义提供兼容能力但 `ActionPrimitive=None`。Runtime AI Planner Tick 仍只读 compiled evaluator，不读取槽位或分类元数据，不改变 HFSM + Utility 决策语义，也不进入 SaveState / network snapshot / replay / gameplay state hash。

最小可运行组合校验按能力而不是按具体 behavior id 判断：

- `Melee Basic`：`Targeting + Engage + Damage + Fallback`。
- `Ranged Basic`：`Targeting + Engage 或可持续远程压制 + Damage + Fallback`。
- `Kite`：`Targeting + Kite + Damage + Fallback`，且需要说明如何被 `Pursuit/Control` 反制；只有撤退/保持距离没有输出时判为无效或高风险。
- `Pursuer`：`Targeting + Engage + Pursuit + Damage + Fallback`。
- `Controller`：`Targeting + Control + Damage 或 TeamFollow + Fallback`。
- `Survivor`：`Targeting + Defense + Re-engage 或 Damage + Fallback`；只有逃跑没有回战斗链路时高风险。
- `Arena Valid`：双方至少都要有 `Targeting + Damage + Fallback`；面对对方 `Kite/Defense` 时，本方至少需要 `Pursuit/Control` 或等价远程压制，否则 Arena 配置提示“可能追不上”。

编译语义：

- 行为组只编译 `entries[]` 中的行为实例，不遍历全行为库生成候选。
- Entry 按 `order` 排序后展开到固定 state scaffold：`root / engage / survival / movement / combat / fallback`；v1 不开放 JSON 自定义状态树。
- Definition 提供默认 conditions、score/cost、action primitive、ability slot、attack range、movement override 等映射；entry 的 `parameterOverrides` 只覆盖这些既有字段。
- 编译器负责诊断未知 behavior id、重复 entry id、非法 action primitive、缺失 required fact、缺失 target fact producer、不可达 action 条件，以及 survival/combat 优先级冲突 warning；最终仍调用现有 v3 validator / compiler。
- Tick 高频路径只看到替换后的 compiled evaluator/profile slot；不会读取行为定义 JSON、行为组 JSON、遍历行为库、拼接字符串或构造 UI view model。

Editor 可视预览语义：

- `行为组预览` 是独立只读页，只能选择三类低频候选：`当前行为组草稿`、`内置默认行为组`、`默认 Loadout variant`。选择丢失时回退当前草稿；本轮不扫描行为组 JSON 文件目录，不新增保存/导入规则，也不改变 gameplay `BehaviorSelection`。
- `行为组预览` 首屏以 `行为组决策链` 为主：按组内 `entry.order` 展示行为名称、触发条件摘要、输出动作和状态/诊断，让主创先看懂“这个组会怎么决策”。`组内行为执行结果` 保留为紧凑辅助，raw `entryId/action@state` 只放 tooltip、Raw 或诊断区。
- `底层 Runtime AI Planner 流程` 只读取所选行为组编译出的 v3 profile，放在高级折叠区，复用只读 flow preview 展示 fixed scaffold、transition rule 和 action 所在 state。
- `组内行为执行结果` 只读取 compiler summary，将每个组内 entry 显示为可读的 `行为名称 -> 动作（state）`，例如 `接近目标 -> 移动到目标（movement）`；entry id、module id 和 intent 放在 tooltip 或诊断中。`disabled=true` 的 entry 保留显示但不生成 executable action。预览页中的 chip 只做展示，不触发编辑 Inspector 定位。
- 预览不得把行为定义库画成全量节点图，也不得把未加入行为组的 behavior definition 当作候选 action。
- 点击预览节点或规则只能作为 Editor 解释，不改变 runtime profile schema，不写回 behavior group JSON，也不进入 SaveState、network snapshot、replay/hash。`行为组编辑` 页不渲染流程图预览，只保留行为定义库搜索、组 entries、实例 Inspector、复制 JSON、覆盖底层 profile 草稿和显式 debug hot update。

Agent 使用规则：

- 如果任务是“组合 Runtime AI Planner 行为、让行为更开放/可读/可调试”，优先扩展 CharacterTest behavior definition / behavior group layer，而不是直接新增一批手写 v3 profile 模板。
- 如果需要新增行为，先新增或加载 `BehaviorDefinition`，确认能否用现有 facts/action primitive/movement settings 表达；不能表达时再按“数据准备 -> facts -> action 输出 -> 编辑器解释”的顺序补能力。
- 如果需要给某个角色或策略增加行为，应修改 `BehaviorGroup.entries`，而不是给行为库条目增加全局 enabled/disabled。
- 如果需要更多运行时解释数据，扩展 typed debug snapshot 或编译期行为实例摘要；不要在 Tick 内拼中文文案或 Editor 展示字符串。
- 行为组 JSON 是后续更友好的 authoring 真源方向；编译出的 v3 profile 可以作为 debug/export 产物。旧 v3 profile 继续可加载，旧 composition JSON 只作为兼容入口，不再作为新的主创手工组合入口。

### 行为卡编辑器

行为卡编辑器的目标是编辑一个具体行为能做什么，而不是继续扩展抽象语义系统。首版优先服务 `突进追击` 这类可验证行为：什么时候触发、执行哪个 Ability 或 CharacterAction、执行后如何按有限条件进入下一步。它不替代行为组编辑器；行为组编辑器负责把多张行为卡组成卡组，行为卡编辑器负责维护单张卡的内部流程。

概念边界如下：

| 概念 | 行为卡编辑器含义 | 边界 |
| --- | --- | --- |
| `BehaviorDefinition` | 行为卡模板，保存稳定 id、能力分类、触发条件、步骤链、默认参数和诊断元数据 | authoring/compile 低频数据；不直接进入 Tick |
| `BehaviorGroup entry` | 卡组中的实例卡，引用一个 `BehaviorDefinition` 并覆盖少量参数、顺序或临时禁用状态 | 只在所在行为组内生效；`disabled=true` 不等于全局禁用模板 |
| `BehaviorLoadout` | 面向玩法发布的策略卡，可由多张行为卡组成 | 玩家看到策略，不看到行为卡内部 facts/score/cost |

行为卡内部采用 `steps[]` 表达有限流程。首版只允许这些步骤类型：

- `UseAbility(AbilityId)`：请求释放稳定 Ability，例如追击行为使用 `DashStrikeAbilityId`，而不是临时技能槽。
- `UseAction(CharacterActionId)`：请求执行具体 CharacterAction，例如 `UseAction(light_attack)` 或 `UseAction(light_attack_2)`。
- `MoveToTarget`：请求向当前目标移动，用于追击前置、补位或未到技能距离时的接战。
- `Wait`：短暂保持或等待条件变化。
- `End`：行为流程结束，回到 Runtime AI Planner 下一次选择。

步骤只支持有限分支：成功后、失败后、条件满足后。条件必须来自已有 typed facts 或明确新增的 typed facts，例如目标距离、目标可见、自身是否处于动作硬直、Ability 是否可用、当前 CharacterAction 是否允许切换。首版不支持循环、不支持任意表达式、不支持脚本注入，也不允许在行为卡里直接写 C# 或 DSL。

编辑器呈现上，行为卡不是普通步骤列表，也不是纯全局时间轴。06 的目标是“自动布局分支流程图 + 步骤卡内局部小时间轴”：主图用动作节点、判断节点、条件连线和明确 `End` 终点表达执行分支，默认视口内看全图，不依赖横向滚动理解分支，也不使用自由拖拽节点画布作为主视图。主路径按结构化分支生成，优先走非 `End` 的 `DefaultSuccess`；如果默认直接到 `End`，则走优先级最高的非 `End` 条件分支，直到汇入 `End`。每个动作节点显示自己的预估持续时间、关键帧/命中/切换窗口和条件判断窗口；判断节点表达 `距离 <= 1m?`、`连段窗口允许?` 这类问题；默认、条件、失败分支用带具体语义的连线标签表达，例如 `已进 1m`、`未进 1m`、`失败/中断`。完整条件和 raw id 放 Inspector / 高级诊断。`End` 必须作为明确终点节点显示，语义是“交回行为组重新决策”，成功结束、失败结束和条件不满足结束都只是不同原因的回流。

步骤持续时间优先只读引用 Ability / CharacterAction 摘要：`UseAction(CharacterActionId)` 读取 CharacterAction 的 duration / transition window；`UseAbility(AbilityId)` 先解析 Ability 绑定的 CharacterAction，再展示该动作摘要。无法解析时只显示未知时长 warning。条件判断窗口第一版是 Editor-only 预估/草稿字段，不进入 Runtime Tick；正式持久化、校验和编译规则留给 07 schema 与 08 compiler 切片决定。

不要再把“普通攻击”作为行为概念。正式行为应引用具体动作或能力：近战基础攻击写成 `UseAction(light_attack)`，连段可以追加 `UseAction(light_attack_2)`，但是否允许从第一段切到第二段由 CharacterAction 切换窗口决定。`PrimaryAttack` 和 `CastAbilitySlot` 继续作为旧 profile、测试和兼容路径；新的正式行为卡应逐步迁移到稳定 `UseAction(CharacterActionId)`、`UseAbility(AbilityId)` 或后续 `CastAbilityId` 输出。

Runtime AI Planner 在行为卡流程中的职责仍然是“下一步想做什么”。它可以根据感知 facts、能力冷却、当前动作状态和步骤结果选择 `UseAbility`、`UseAction`、`MoveToTarget` 或 `Wait`；Ability 和 CharacterAction 继续决定动作怎么执行、哪一帧生效、root motion、hit trace、gameplay request、VFX 和伤害事件。行为卡可以预览 Ability / CharacterAction 时间线，但不复制时间线字段，也不成为第二套 CharacterAction 编辑器。

## 行为 Loadout 玩法层

行为组层仍是 authoring/compile 原料。当前代码使用内部 **Runtime AI Planner Behavior Loadout** 作为预制策略和权威选择入口；目标玩家合同则把 Loadout/Variant 保留为推荐预设，应用后展开成每角色可自由换卡的 Installed Deck。完整规则以[行为卡玩家玩法与 Lab 统一设计](CHARACTER_TEST_RUNTIME_AI_PLANNER_BEHAVIOR_CARD_GAMEPLAY.md)为准。

目标首版固定为每个 AI 角色独立 `6` 槽 / `10` 费、同一卡不可重复、战前锁定、整卡解锁和免费寻敌/待机保底。玩家只做单点安装/移除，系统自动排列并诊断；玩家不能编辑卡内 Stage、Detector、Transition 或内部优先级。Lab 默认复现相同构筑，显式越界时持续标记“非玩家合法构筑”。

当前实现的四层概念如下：

| 概念 | 作用 | 权威/持久化边界 |
| --- | --- | --- |
| `BehaviorDefinition` | 行为库条目，例如 `Movement.ApproachTarget`、`Combat.CastAbilitySlot` | 低频 authoring/compile 原料；不直接进入 snapshot/hash |
| `BehaviorGroup` | 角色/策略实际拥有的行为实例集合，包含 `groupId`、`entries[]`、实例顺序、临时禁用和参数 override | 低频 authoring/compile 真源；编译成 v3 profile/evaluator slot |
| `BehaviorLoadout` | 玩法可选预设，包含稳定 `int loadoutId`、`stableId`、显示信息、`behaviorGroupId`、允许参数和 variant | 发布 catalog 的一部分；session build 阶段低频编译 |
| `BehaviorSelection` | 单个角色当前权威选择，只保存 `loadoutId`、`variantId`、`catalogHash/revision` 和派生 `profileSlot` | Gameplay-owned state；进入 network snapshot、rollback clone、state hash、authoritative replay bootstrap/command stream 和未来 SaveState |

当前 catalog schema 为 `mx.character-test.runtime-ai-planner.behavior-loadout-catalog.v1`。Loadout 引用已发布的 `behaviorGroupId + variantId`，而不是保存 composition module list；运行时只接受已发布 catalog 中的 `loadoutId + variantId`，不允许客户端或 Play Mode 注入任意 JSON。Session build 会加载并校验 behavior definition catalog、behavior group 和 loadout catalog，把所有允许的 loadout/variant 编译为现有 v3 `CharacterTestRuntimeAiPlannerProfile`，再生成 evaluator cache / profile slot。战斗 Tick 仍只读取 `RuntimeAiPlannerProfileSlot -> evaluator`，不读 JSON、不编译、不遍历行为库、不拼接字符串。这是兼容基础，不代表玩家自由卡组、解锁或费用规则已经实现。

角色控制状态会同时兼容旧 `runtimeAiPlannerProfilePath/profileSlot` 和新 `BehaviorSelection`。如果实体已有有效 loadout selection，则 `RuntimeAiPlannerProfileSlot` 派生自 selection 的 `profileSlot`；旧 profile path/slot 仍可用于未配置 loadout 的场景。若后续 Editor 同时配置新 loadout 和旧 profile path，loadout 应优先生效并给 warning。

玩法切换通过 runtime command 完成：`SetRuntimeAiBehaviorSelection(targetId=entityId, payload0=loadoutId, payload1=variantId, payload2=schemaVersion)`。命令只在 selection phase 解锁时接受；默认现有战斗场景保持 locked。未知 loadout/variant、catalog hash 不匹配、未授权实体、未允许组合、编译失败或 phase locked 都必须拒绝，并保留旧 evaluator/selection。NPC/敌人选择来自场景、spawn spec 或服务器权威默认值；己方 AI 单位后续由玩法 UI 通过 authority provider 查询 allowed list 后提交同一 command。

正式 Snapshot 和 gameplay replay 只保存稳定选择摘要和 catalog hash，不保存 compiled evaluator、behavior group JSON、Editor 展示文本或 debug overlay。Apply snapshot 必须恢复 `ControlOwner + RuntimeAiPlannerProfileSlot + BehaviorSelection`；catalog hash 不匹配时 fail fast，不能静默换成默认行为。Debug hot update 仍只是当前 Lab 会话 overlay，不能作为玩法选择入口；Lab 自己的确定性 seek/验证会在原 application RuntimeFrame 重放 revision event。

Agent 接入规则：

- 需要让玩家或主创选择策略时，接入 loadout catalog / selection command；不要直接把 behavior group JSON、v3 profile JSON 或 Editor overlay 暴露给 gameplay。
- 需要新增可选策略时，先新增或组合 `BehaviorDefinition` 到 `BehaviorGroup`，再发布为 `BehaviorLoadout` variant，并补 catalog hash、compiler、selection command、snapshot/hash/replay 测试。
- 需要更多 UI 展示信息时，在 Editor 低频 view model 中把 catalog / snapshot typed 字段转成中文标签；不要把显示名、模块命中文案或卡片字符串写入 runtime authority state。

当前运行时选择入口是 CharacterTest FairyGUI 全屏 `RuntimeAiBehaviorLoadoutPanel`。它只列出当前可配置的 AI-controlled 单位和已发布 `BehaviorLoadout` variant，提交时仍走 `SetRuntimeAiBehaviorSelection` command；phase locked 时只能预览，不能改权威选择。该面板不编辑行为组、不保存 JSON、不替换 evaluator overlay、不写 SaveState/network/replay/hash 之外的其它状态，也不在 Tick 中读取 UI 数据或拼接展示字符串。后续玩家卡组界面必须改为稳定卡 ID 集合提交，由 authority 校验解锁、去重、槽位、费用、catalog revision 和角色兼容性，并在 session boundary 固定每张卡的 Published revision/hash。

## 玩家可见行为表达

行为 Loadout 成为玩法后，玩家看到的不是内部模块、facts 或 Utility 数据，而是策略承诺和战斗反馈。CharacterTest 需要同时维护三层语言：

| 语言层 | 面向对象 | 示例 | 不应暴露 |
| --- | --- | --- | --- |
| 内部模块语言 | Runtime / compiler / tests | `Movement.ApproachTarget`、`Combat.CastAbilitySlot`、`target.visible`、`score/cost` | 玩家 UI |
| 主创 authoring 语言 | Runtime AI Planner Editor / Play Monitor | 行为组实例、required facts、生成 actions、Ability / CharacterAction 摘要、诊断 warning | 战斗 HUD 常驻显示 |
| 玩家可见策略语言 | 战前/安全区 UI、战斗短提示 | `突击`、`风筝`、`守卫`、`控场`、`保命`、`协同` | module id、profileSlot、catalog hash、raw facts |

玩家可见策略应表达“这个单位大致会怎么打”。推荐第一批策略卡语义：

- `突击`：主动接近敌人，优先近身攻击。
- `风筝`：保持距离，边退边释放远程技能。
- `守卫`：优先保护位置或目标，敌人靠近后反击。
- `控场`：保持中距离，优先释放控制或区域技能。
- `保命`：危险时撤退，避免硬拼。
- `协同`：跟随玩家目标，优先集火或补位。

策略卡至少应包含：名称、短描述、主要动作倾向、可能使用的技能槽 / Ability / CharacterAction 摘要、适合场景、限制或风险。战斗中只显示短状态标签，例如 `突击中`、`保持距离`、`寻找视线`、`撤离危险`、`准备施法`、`目标丢失`、`路径受阻`。这些标签可以从 typed snapshot / presentation view model 派生，但不写入 Tick 高频字符串、SaveState、network snapshot、replay/hash 或 Runtime AI Planner JSON schema。

当前基础实现由 `CharacterTestRuntimeAiPlannerPlayerBehaviorPresentationCatalog` 提供低频策略卡和战斗短状态派生。Agent 为玩家 HUD、战前/安全区 UI 或调试面板补展示时，应优先复用该 presentation view model；如果需要更多玩家可见字段，应扩 loadout metadata 或 typed debug snapshot，再在低频 view model 中转成文案，不要在 Runtime AI Planner Tick 中拼接字符串。

行为到动作的链路固定为：

```text
BehaviorLoadout
  -> Runtime AI Planner action
  -> Ability slot / Ability
  -> CharacterAction timeline
  -> gameplay request / hit trace / VFX / projectile / damage
```

Runtime AI Planner 决定“什么时候做什么”；Ability 和 CharacterAction 决定“这个动作如何执行、哪一帧生效、播放什么表现”。行为组只引用和预览 Ability / CharacterAction 的结果，不替代角色动作编辑器，也不把动作时间线字段复制到行为定义里。

当前基础实现由 `CharacterTestRuntimeAiPlannerBehaviorActionChainPreviewBuilder` 低频读取 Ability hotbar/catalog 和 CharacterAction runtime/config，生成 `PrimaryAttack`、`CastAbilitySlot(slot)` 等 planner action 的预览摘要和断链 warning。该 builder 只供 authoring、Editor、Play Monitor 或未来玩家 UI 的低频展示使用；不要把 action chain preview 当作 runtime 权威状态，也不要在 Tick 中扫描 Ability / CharacterAction 资源。

## 数据准备层

Runtime AI Planner 行为扩展前，CharacterTest 先补了专用数据准备层。该层在 `PreSimulation` 读取上一帧 CharacterTest runtime world、CombatPhysicsWorld 和角色状态，投影为预分配的 per-agent perception state 与 facts。

`CharacterTestPerceptionFrameCache` 是该数据准备层的共享帧缓存。它由 CharacterTest runtime/gameplay 侧拥有，在 `BeginFrame(frame, world, physicsRuntime)` 后按需缓存 entity sample、target geometry、visibility、navigation 和 environment 派生结果；Runtime AI Planner、目标锁定、投射物瞄准、Debug/HUD 只能只读消费 `TryGet...` / `TryFind...` 输出，不持有底层 `CombatQueryResult` 列表，也不能把缓存结果写回权威状态。

缓存只保存当前帧可重算的派生数据：距离、角度、方向、视线遮挡、直线路径、候选移动、附近障碍/可移动物/危险/可用物等。它不进入 SaveState、network snapshot、replay/result hash，也不改变 Runtime AI Planner JSON schema。`target.lastSeenAgeFrames`、`target.threatScore`、action score/cost、strafe side、reaction delay 等跨帧决策状态仍归 Runtime AI Planner agent state 管理。

正式 Attack Opportunity adapter 提供锁定 Hurtbox 距离与 witness，但不覆盖导航 move。原生活动卡执行器消费 `Facts.MoveDirection` 时保留 waypoint／局部候选方向及 zero stop；即使目标仍在停止距离外，zero 也不能退回直线接近。回归入口为 `CharacterTestRuntimeAiPlannerFormalizationTests.PathfindingPlanner_FormalHurtboxAdapterPreservesNavigationThroughActiveCard`（正式 adapter、Committed Frame Geometry、导航→Facts→活动卡→控制命令）和 `CharacterTestBehaviorExecutionEvaluatorTests.ApproachTarget_PreservesNavigationStopAndArrival`（stop／微小输入／绕行／到达对照）；定义存在不代表已运行通过，实际原生 Runner 与障碍场景回执记录在 Issue #748。

该路径属于 Tick 高频路径，warmed 后按 `NoAlloc after initialization` 约束维护：集合在构造或扩容阶段准备，帧内查询复用小 struct 和预分配 query buffer；不得引入 LINQ、每帧临时集合、字符串拼接、反射、Unity 对象查找或 `unsafe`。

### Agent 使用共享帧缓存的规则

当任务涉及 CharacterTest 的目标选择、目标锁定、投射物瞄准、连锁目标、Runtime AI Planner 感知、Debug/HUD 感知显示，或需要距离、角度、方向、目标中心、视线、路径、附近环境等派生数据时，Agent 应优先复用 `CharacterTestPerceptionFrameCache`，不要在各系统里重复扫描 `world.Entities`、重复计算平方距离/方位角，或直接暴露 CombatPhysics query 结果。

使用入口按数据类型选择：

| 需要的数据 | 使用入口 | 约定 |
| --- | --- | --- |
| source-target 距离、平方距离、dx/dz、归一化方向、目标中心、方位角、朝向差/dot | `TryGetTargetGeometry(frame, source, target, out sample)` | 同帧同 source/target 只构建一次；投射物只复用目标中心、delta、方向和距离，不把友军/死亡/射程/ballistic 校验并入缓存 |
| 最近目标 | `TryFindNearestTarget(frame, source, relationFilter, maxDistance, tiePolicy, out geometry, out target)` | Runtime AI Planner 使用 `OpposingTeam` + `First`，保持旧平方距离最近敌人语义；目标锁定使用 `AnyOther` + `Last`，保持最近非自身目标且同距离后者覆盖的旧语义 |
| 视线遮挡和可见性 | `TryGetVisibility(frame, source, target, perceptionSettings, out sample)` | 只读取 `LineOfSightBlocked`、`TargetVisible`、hit count、nearest id/distance 等派生摘要；不要传出底层 query result list |
| 直线路径、候选移动、attack range 判断 | `TryGetNavigation(source, target, attackRange, navigationProfileKey, probeMinLength, probeMaxLength, candidateAngles, out sample)` | L1 使用一次 CombatPhysics 全高保守 OBB probe（`QueryShape=SweptObb`），cached/fallback 共用构造；读取 `LayerMask`、`ClearanceRadiusMillimeters`、`ClearanceHeightMillimeters`、query/hit count、nearest obstacle 和 selected candidate 摘要；`navigationProfileKey` 随候选角、probe 长度变化，cache key 还覆盖 query shape、layer mask、clearance 半径/高度/实际中心及 grounded 状态，避免同帧姿态或跨台阶资格变化后误复用 |
| 地表、附近可移动物、障碍、危险、可用物 | `TryGetEnvironment(frame, entity, perceptionSettings, out sample)` | 对半径内合法可移动物体按“是否存在”聚合 movable/useful/hazard；普通物体不能截断后续危险物扫描，cached/fallback 仅在三个物体摘要均为 true 时短路。危险沿用任一非零 potency 规则、半径边界包含等距；保留同帧复用、下一帧清空及 warmed NoAlloc（Issue #760）。只保存当前帧摘要，跨帧记忆或威胁评分仍归 agent state |

接入新消费者时：

- 从已有组合根或 `CharacterTestCharacterRuntimeModule.PerceptionFrameCache` 注入缓存；不要在单个系统里 new 第二个帧缓存。
- 确认消费发生在拥有方 `BeginFrame` 之后；如果新增高频 Tick 消费阶段，要在同一 CharacterTest runtime/gameplay 边界显式说明 frame 生命周期。
- 只把 cache sample 当作当前帧只读派生数据；不要存入 SaveState、network snapshot、replay/result hash、Runtime AI Planner JSON schema 或长期 agent memory。
- 如果 cache 缺失或未连接，消费者应保留旧权威路径 fallback，且 fallback 行为必须和原逻辑一致。
- 新增共享数据字段时同时补 focused tests：同帧复用、下一帧刷新、旧行为语义、warmed 0 managed allocation，以及必要的 CombatPhysics query 计数断言。

当前数据准备层覆盖：

- 目标感知：`target.exists`、`target.visible`、`target.lineOfSightBlocked`、`target.lastSeenAgeFrames`、`target.distance`、`target.inAttackRange`、`target.threatScore`。
- 自身状态：`self.actionRunning`、`self.surfaceKind`、`self.onHardSlick`、`self.nearMovableObject`。
- 移动可达：`move.pathAvailable`、`move.directPathClear`、`move.candidateIndex`。
- 环境采样：`environment.nearObstacle`、`environment.nearHazard`、`environment.nearUsefulObject`。

数据来源优先复用现有 CharacterTest / Combat 权威数据：

- 地表材质来自 CharacterTest 角色 motion / surface state。
- 静态障碍和视线遮挡通过 CombatPhysics query。
- 可移动自然物体来自 CharacterTest runtime world 的 movable natural object 状态。
- 目标记忆和威胁评分是 deterministic 规则，不引入听觉、复杂仇恨、GOAP 或 Unity NavMesh。

感知参数保存在 JSON profile 的 `perception` 区：视野距离、目标记忆帧数、视线遮挡层、环境采样半径、威胁评分权重。移动真实感参数保存在 `movement` 区：速度倍率、转向响应、接近侧向混合、偏好距离带、到达减速距离、侧移换边帧区间、方向保持帧和反应延迟帧。v1/v2 JSON 仍可加载，缺失 perception 或 movement 时补默认值并给 validation warning。

## 开发日志

| 阶段 | 结果 | 关键边界 |
| --- | --- | --- |
| Formalization | CharacterTest 专用 Runtime AI Planner profile、DTO/compiler/validator、command source、debug snapshot 初版闭环 | 首版限定 HFSM + Utility，不迁移旧 WGame AIAction Config |
| 结构化编辑器 | Runtime AI Planner 窗口从数组 `PropertyField` 升级为 profile 摘要、状态树、页签、Inspector 和保存状态条 | Edit Mode 编辑保存，Play Mode 只读监控 |
| JSON profile | 移除 profile ScriptableObject authoring，改为 StreamingAssets JSON 真源 | 场景只保存 JSON 相对路径，runtime build 阶段低频加载 |
| 窗口精修 | 默认 JSON 自动加载、空状态主操作、顶部 toolbar 重排、底部状态明确化 | 只改 Editor UX，不改运行时决策路径 |
| 数据准备层 | 增加自然环境采样、角色感知采样、共享帧缓存和 facts 投影 | 新 facts 先进入条件和调试；缓存只保存每帧派生数据，不改 JSON schema / SaveState / network / replay |
| 编辑器好用化 | 条件编辑器增加 facts 元数据、类型化控件、条件模板、局部诊断、快捷修复、感知版近战模板和 Play Monitor 决策解释 | 不改 JSON schema，不做节点图，不做 Play Mode hot reload |
| 状态流程图预览 | 状态页顶部增加只读 HFSM 概览图，默认完整显示状态流；详细查看通过独立放大预览窗口完成 | 图只用于理解和定位，不拖拽、不连线编辑、不持久化布局 |
| 编辑器代码结构收束 | Runtime AI Planner Editor 增加独立 Development Guide，并将窗口、facts catalog、流程图预览按职责拆分记录为长期维护边界 | 只改 Editor 文档和代码结构，不改变 profile schema 或 runtime 决策语义 |
| 多行为 profile | 新增远程风筝、危险游斗和技能施法三套 StreamingAssets JSON，并把模板创建入口收束为 editor-only 模板库 | 后续补充角色级覆盖，空值继承场景默认 |
| 角色级 profile 覆盖 | `CharacterTest Characters` 可为 `AiControlled` 行配置 Runtime AI Planner JSON；session build 分配 profile slot，runtime tick 只按 slot 选择已编译 evaluator | 不做 Play Mode hot reload，不做每帧 JSON 读取 |
| 常见行为 profile 扩展 | 新增激进冲锋、守卫防御、伏击者和控制控场四套 StreamingAssets JSON，并补齐模板、validator、evaluator 和流程图回归 | 只消费已有 facts/action kind；不新增仇恨、友军保护点或同场多 profile 绑定 |
| 移动真实感改造 | JSON schema 升到 v3，profile/action 可配置 movement settings；evaluator 为每个 agent 记录 deterministic strafe side、换侧帧、方向保持和反应延迟，用更连续的 move intent 驱动现有 motion solver | 不改 command schema、不绕过 `SetControlIntent` / motion solver、不在 Tick 读 JSON |
| 行为库选择器 | Runtime AI Planner 窗口和 CharacterTest Characters Inspector 改用内置 profile picker；文件选择降级为高级入口 | 只改 Editor 选择/绑定体验，不改 JSON schema 或 runtime 加载语义 |
| 行为组层 | 新增 CharacterTest internal behavior definition catalog、behavior group JSON、v3 profile 编译器、`行为组预览` 只读决策链、组内行为执行结果和底层 flow 高级预览、`行为组编辑` 行为定义库搜索和组实例 Inspector、显式 debug gate 热更新 | 不新增 `MxFramework.AI` 公共 API；preview 只读且不改变 gameplay selection；hot update 只作用当前会话，不进 SaveState/network/replay/hash |
| 行为 Loadout 玩法层 | 新增 CharacterTest internal behavior loadout catalog、loadout/variant 编译、权威 selection command、snapshot/hash/replay-safe catalog 校验和 Play Monitor 只读摘要 | 不新增 `MxFramework.AI` 公共 API；首版只支持战前/安全区选择，不做战斗中热切换或任意 JSON 注入 |

## 行为卡玩家玩法实施与评审要求

行为卡玩家玩法与 Runtime AI Lab 的实现必须以
`CHARACTER_TEST_RUNTIME_AI_PLANNER_BEHAVIOR_CARD_GAMEPLAY.md` 为产品合同，采用小步、可独立验收的阶段开发。不得把 Published Card、玩家卡组、联网权威、玩家 UI 和 Lab 发布闭环合并成一次巨型改动，也不得仅凭界面可见就把目标设计写入 `CAPABILITIES.md`。

当前基础内容集合只包含 `靠近并普攻`（`bc_3qz33zjzk4`）和 `发呆`
（`bc_g6a5fw319a`）。旧 `Targeting.* / Movement.* / Survival.* / Combat.* /
Fallback.WaitOrHold` 只允许作为历史 JSON 的只读迁移别名解析，不得出现在
`Definitions`、新建默认 Profile、Project Catalog seed、推荐预设或卡库中。
版本化正式目录是恢复真源；恢复流程不得重新构造旧模块派生卡。
两张基础卡的完整 runtime/Overlay 接入仍是未完成能力；默认兼容 Profile 当前只编译
Wait。不要通过恢复旧模块 primitive 或硬编码接近/攻击候选来掩盖这个实施缺口。

### 固定切片产生与执行流程

本节是后续行为卡玩家玩法与 Runtime AI Lab 开发的强制流程。宏观阶段或整改路线只负责确定依赖顺序，不能直接授权编码；每一个进入实现的工作单元都必须先形成独立切片设计，并通过设计评审。

#### 1. 候选切片如何产生

候选切片必须从以下证据共同产生：

- 权威产品合同、数据语义、目标效果图和既有 ADR。
- 当前源码、测试、Composer Blueprint/binding contract 和真实运行状态。
- 真实 Game View 流程走查、Console、typed diagnostic、性能或确定性证据。
- 已接受阶段遗留的明确债务、兼容边界或后续合同。

拆分时先把问题按**共同根因和权威边界**归组，再建立依赖图；不得按按钮数量、文件数量或页面数量机械拆分。例如多个页面同时把 `Wait or hold`、行为卡标题和 Draft 来源混在一起时，应形成一个“身份与投影合同”切片，而不是分别做三个文案补丁。Facts、RuntimeFrame 和 Replay 若共享帧上下文与五轨组件，也应先冻结共享合同，再决定一个组合切片或有顺序的子切片。

候选切片只有同时满足以下条件才可进入设计：

- 只有一个主要根因、一个主要权威边界或一个可独立提交的事务。
- 输入、输出、依赖和非目标可明确列出。
- 能通过一组有限的 build/tests/Unity/画面证据独立验收。
- 失败或回退不会破坏此前 `Accepted`/`Committed` 切片。
- 评审者无需等待未来切片即可判断本切片合同是否成立。

若一个候选同时改变 schema/hash、runtime authority、玩家 UI 和 Replay，或需要用“下一切片会补齐”才能成立，必须继续拆分。切片之间可以有依赖，但单个切片不能依赖尚未设计的补丁来掩盖不完整交付。

#### 2. 每个切片必须先有实施设计

切片设计应写入对应的 `Docs/Tasks/`任务文档；跨阶段的长期约束继续留在 Guide/ADR，不把一次实现细节反写成产品合同。设计文档至少包含：

1. 状态、来源问题、依赖切片和目标。
2. 根因、当前证据和期望行为。
3. 明确范围、非目标和禁止顺带修改项。
4. MxFramework API/模块复用计划，以及绕过既有模块的缺口说明。
5. 数据身份、状态机、线程/帧边界、对象生命周期和失败恢复。
6. public API、schema、semantic/content/deck/catalog/authority hash、SaveState、network、rollback、replay 和 migration 影响。
7. 预计修改的模块/文件边界；Composer-owned UI 还要列出语义资源、组件配方、Screen Blueprint、binding contract 和发布目标。
8. 高频路径判定、分配分类和 `NoAlloc after initialization`方案。
9. 测试矩阵、Unity/Composer 验证、真实 Game View 截图场景和验收标准。
10. 独立提交边界、排除目录和回滚方案。

UI 切片的设计必须同时包含信息架构、主要交互状态、空/错/加载状态、目标效果图和当前真实帧对照。仅有一张效果图、字段清单或“照标准图实现”都不构成实施设计。纯 C# 切片也不能只列类名；必须冻结输入、不可变结果、诊断严重度、hash/排序和失败原子性。

#### 3. 固定状态机

新切片使用以下状态，不得从 `Candidate`直接跳到实现：

```text
Candidate
→ Design In Progress
→ Design Review
→ Design Accepted
→ Implementation In Progress
→ Conformance Review
→ Conformance Accepted
→ Closeout Review
→ Implementation Accepted
→ Committed
```

补充状态：

- `Design Changes Requested`：设计存在可修复缺口；修订后重新进入 `Design Review`。
- `Conformance Changes Requested`：实现偏离已接受设计或出现新的设计缺口；修复后重新进入 `Conformance Review`，需要扩大范围时退回 `Design In Progress`。
- `Closeout Changes Requested`：验证、事实文档、真实画面或提交边界证据不完整；补齐并复测后重新进入 `Closeout Review`。
- `Blocked`：外部依赖或权威合同未解决；不得用兼容 UI、伪数据或临时分支绕过。
- `Abandoned`：候选被更高层设计替代；必须记录替代切片或决策来源。

`Design Accepted`只允许开始实现，不代表代码已经完成；`Conformance Accepted`只证明实现符合设计，不能替代最终证据；`Implementation Accepted`只允许进行收口提交，不代表已经进入主线；只有 `Committed`后，才允许把下一个依赖切片改为 `Design In Progress`。

#### 4. 固定执行步骤

| 顺序 | 主代理动作 | 独立评审动作 | 前进条件 |
| --- | --- | --- | --- |
| 1 | 收集合同、源码、测试和真实运行证据，产生 Candidate | 不评审实现 | 根因与依赖可说明 |
| 2 | 编写完整切片设计，状态 `Design In Progress` | 不提前编码 | 设计模板全部填写 |
| 3 | 状态改为 `Design Review`并停止实现动作 | 只读检查范围、身份、生命周期、兼容、性能和测试合同 | `Design Accepted` |
| 4 | 严格按已接受设计实现，状态 `Implementation In Progress` | 不并行接受未完成实现 | 设计范围内代码/资产完成 |
| 5 | 状态改为 `Conformance Review`并停止扩展 | 只读核对 diff 是否严格符合已接受设计、API/身份/生命周期/性能边界和非目标 | `Conformance Accepted` |
| 6 | 主代理执行完整证据矩阵并更新事实文档 | 不以主代理口述代替 build/tests/Unity/Composer/真实画面 | 设计规定的证据全部完成 |
| 7 | 状态改为 `Closeout Review`并冻结提交范围 | 只读复核验证结果、事实状态、真实表现、排除项和回滚边界 | `Implementation Accepted` |
| 8 | 完成独立提交 | 核对提交内容与已接受范围一致 | `Committed` |
| 9 | 才开始下一个依赖切片的设计 | 不回溯修改已接受合同；若必须改变则新开设计变更 | 上一切片已提交 |

实现中发现设计缺口时，必须停止扩大代码范围，把状态退回 `Design In Progress`，补设计并重新评审。不得以“实现时顺手处理”绕过设计门禁。评审 Agent 只给出结论和问题，不修改、提交或替主代理推进状态；主代理也不能仅凭自测把自己的切片标记为 Accepted。

### 实施阶段

玩家行为卡基础曾按以下六个大阶段建立并验收；它们现在是历史依赖和事实边界，不代表后续整改可以直接追加“阶段 7”后编码。任何新增能力、债务整改或 UI 重构都必须先按上面的固定流程产生 Candidate，并进一步拆成可设计、可评审、可提交的独立切片。

| 阶段 | 交付目标 | 明确不在本阶段完成 | 最低验收证据 |
| --- | --- | --- | --- |
| 1. Published Behavior Card | 发布卡 envelope、stable card id、revision、玩家 metadata（含单卡费用）、catalog、兼容 revision resolver、独立 Publish validation，并复用现有 Logic v2 validator/compiler | 玩家卡组 UI、卡组费用预算/容量规则、解锁、网络 | schema roundtrip、非法发布拒绝、revision 解析、Lab/catalog 编译与 semantic hash 一致 |
| 2. Installed Deck Core | 每角色卡组、`6` 槽/`10` 费、禁止重复、免费寻敌/待机保底、Loadout/Variant 推荐预设展开和卡组诊断 | 玩家画面、账号进度、网络 command | 纯 C# 规则测试、稳定规范化顺序、失败不部分应用 |
| 3. Unlock And Persistence | 整卡解锁、收藏、每角色 Installed Deck 存档、stable id 迁移和缺失卡诊断 | 战斗中换卡、卡内升级 | save/load roundtrip、版本迁移、缺失/不兼容 revision 恢复策略 |
| 4. Session And Authority | 战前锁定、客户端稳定卡 id 请求、authority 合法性校验、会话固定 revision/hash、snapshot/rollback/replay 接入 | 玩家表现优化、任意 JSON 上传 | 未授权/超槽/超费/重复/catalog mismatch 拒绝；相同输入 authority hash 一致 |
| 5. Player UI And Combat Feedback | 玩家卡库、角色卡组、单点安装/移除、预设应用、诊断、战前锁定，以及 HUD 当前卡图标和 typed 切换原因 | Stage/Detector/Transition 玩家编辑 | 当前 Game View 截图或录屏、完整操作路径、错误与锁定状态可读 |
| 6. Runtime AI Lab Unification | Lab 默认玩家装配层、显式“非玩家合法构筑”、COW Logic v2 编辑、Validate、`Apply F+1`、RuntimeFrame/Replay 和 Publish 闭环 | 新公共 `MxFramework.AI` API、第二套行为语义 | 同一 Published revision/卡组在玩家与 Lab 产生相同 compiler 结果、实际路径和 authority hash |

当前阶段状态：

- `1. Published Behavior Card`：`Accepted`，独立提交 `81f4ab7e`。
- `2. Installed Deck Core`：`Accepted`。internal v1 schema、每角色 stable identity、Published compatible revision 解析、`6` 槽/`10` 费、stable card 去重、稳定规范化顺序与 deck hash、原子单卡安装/移除、免费寻敌/待机保底，以及显式 Loadout/Variant 推荐预设到 stable card IDs 的展开已通过复审。卡组诊断区分 `Info/Warning/Error`：合法空卡组保留免费保底并给出低战力 Warning，基础能力覆盖和角色 `RequiredCapabilities` 不匹配作为非阻断 Warning，槽位、费用、重复等硬规则仍以 Error 阻断构建。固定 fallback 合同参与 deck hash，fallback 的 identity、kind、order、cost 或 slot semantics 变化会改变身份。验收证据为 Unity 编译/Console 通过、Installed Deck `17/17` 与 Published Card `11/11` focused EditMode tests 通过、轻量质量门禁通过，以及独立复审的两个 C# 工程构建 `0 warning / 0 error`。
- `3. Unlock And Persistence`：`Accepted`。不可变整卡解锁收藏、已验证 Installed Deck 到每角色 stable-ID 存档的原子更新、规范化 persistence hash、显式 stable-ID migration map、`RuntimeCustomState`/`RuntimeSaveStateJson` roundtrip participant，以及缺失/不兼容 Published revision 的非破坏恢复已通过复审。不可用 ownership 与卡组引用继续保留，但角色卡组不会静默降级或进入 resolved deck 列表；成功恢复继续传播 Installed Deck 的非阻断诊断，框架 JSON 中的空 module entry 会被结构化拒绝。验收证据为两个 C# 工程构建通过、新增 Persistence `16/16` 加阶段 1/2 回归共 `44/44` focused EditMode tests 通过、轻量质量门禁通过。本阶段不包含正式账号/云存储、玩家 UI、session Tick、network command 或战斗中换卡。
- `4. Session And Authority`：`Accepted`。内部正式链路已建立通用 typed application-control request/response envelope，以及 CharacterTest 专用的稳定卡 ID 行为卡组请求；UTP 将首次有效 sender 不可变地绑定到 connection，拒绝重绑定、重复 peer id 和未完成 handshake 的 application-control，请求进入 server 后只使用该连接身份，并按请求者所有权、角色所有权、整卡解锁、Published catalog identity、角色兼容性和 Installed Deck 硬规则裁决。接受后按角色固定 Published revision、semantic/content/deck/catalog hash，形成排序且不可变的多角色 session lock roster；该 roster 已注册到 combined、direct endpoint、host 与 room runner 的 snapshot registry、live rollback coordinator、result-hash 和 replay bootstrap 链路。snapshot 恢复拒绝非规范或规范化后重复的角色 ID，并保持单调锁定；rollback 可通过实际 client coordinator 恢复精确历史 roster；replay 会恢复并验证 authority bootstrap，再从恢复后的 roster 推导 authority hash，bootstrap 篡改会结构化失败。验收证据为阶段 1–4 七个相关测试类（含完整 Unity Transport 类）focused EditMode 回归 `134/134`、三个相关 C# 工程编译通过、Unity Console 无 error，以及 V4 轻量门禁和 code-standards 扫描通过。production authentication token、由正式 catalog/账号解锁/玩家 UI 提供的 composition configuration，以及产品房间/匹配策略不在本阶段范围；因此本阶段只声明内部 authority/network/replay foundation 已验收，不声明玩家多人产品链路已经发布。
- `5. Player UI And Combat Feedback`：`Accepted`。Player Deck Workbench Core、独立 Composer-owned FairyGUI 卡库/卡组页、Runtime SaveState 原子写回、战前 authority lock、Logic v2 Runtime Overlay 安装和 Combat HUD typed 当前卡反馈已形成实际离线流程。卡组页从明确的玩家侧角色 roster 建模：Arena 只收集 Team 1，按 character stable identity 去重并稳定排序，提供角色前后切换；所有角色的卡组共存于同一持久化 snapshot。不同玩家角色在 runtime 构建前获得独立 profile slot，即使来源 profile 相同也不会互相覆盖；CommandModule 批量安装失败会恢复全部旧 evaluator/overlay。玩家可一键应用显式 stable-card 清单定义的推荐预设；不可用/不兼容卡仍可见并可替换，但玩家标题、HUD fallback、持久诊断和命令失败文案均不暴露内部 stable ID。HUD 绑定当前选中/观察角色的 entity identity，忽略敌方和实体列表顺序；Running/Finished Arena 从主菜单继续会重新打开 Arena surface。验收证据包括 UI Composer Apply/Publish、10 个 FairyGUI generated target 一致性检查、Unity Console `0 error`、Installed Deck 与 Formalization focused EditMode tests `185/185`、两个直接 Game framebuffer 的卡组装配/推荐预设画面、轻量门禁中 83 个本次一方代码文件无规范发现，以及独立 Agent 终审 `Accepted`。该切片不冒充生产账号、正式 catalog 分发或联网玩家页面。
- `6. Lab Player Layer And Publish Loop`：`Accepted`。切片 `6.1` 让 Runtime AI Lab 复用阶段 5 的同一 player deck service 与 Composer-owned `BehaviorDeckPanel`，Play 默认进入玩家卡库/卡组页，返回/F9、StartSession 与 Replay rebuild 保持工作区上下文。切片 `6.2a` 将当前兼容 Published revision 以稳定来源身份克隆为隔离 Logic v2 COW Draft，支持持久化、Undo、重开和返回牌库且不修改 Published、Installed Deck 或当前 session；`6.2b` 复用 Installed Deck 和 Player Deck compiler，持续投影 typed 玩家合法/实验越界原因。切片 `6.3a` 建立 append-only 本地 Publish 事务：Logic v2、权威 Ability/CharacterAction 引用、玩家合法性、catalog lineage 和所有 persisted deck 候选重验全部通过且目录原子写盘成功后，才切换本地 authority。切片 `6.3b` 接通 Publish UI 与成功后的 Draft 来源 revision/semantic/content hash 收养；发布前 Undo 历史在不可逆 checkpoint 关闭，Publish 不自动 Apply、不修改当前 evaluator、不追加 Replay application revision，`Apply F+1` 仍是独立 session-local 边界。player legality 与 Publish preflight 按 workspace/group/card、character、reference、catalog/persistence identity 缓存，不进入逐帧分配路径；最近操作状态绑定 stable card ID，切卡不串状态。验收证据包括 UI Composer Apply/Publish、Unity Console `0 error`、Publish `14/14` 及 Publish+Runtime AI Lab Dev `175/175` focused EditMode 回归、直接 Game framebuffer 的玩家牌组/编辑前/语义修改/发布成功画面，以及独立 Agent 终审 `Accepted`。生产 catalog 审批、签名、分发、回滚与联网产品集成仍在 CharacterTest 本地阶段之外。

阶段 2 明确不包含 JSON/save roundtrip、账号解锁、玩家 UI、session Tick、network command 或战斗中换卡。旧 Loadout/Variant 的 BehaviorGroup 条目与 Published Card 不保证一对一，因此预设展开必须使用显式 stable card ID 清单，不得根据显示名或 BehaviorId 猜测映射。

历史宏观阶段状态继续使用 `Not Started`、`In Progress`、`Review`、`Accepted`、`Accepted with debt`、`Changes requested` 或 `Blocked`；新开发切片必须使用“固定状态机”中的设计/实现双门禁状态。`Accepted with debt` 必须记录具体债务、影响和后续归属；不得用“以后优化”替代。完成证据应在对应提交/PR说明中记录，不在本文伪造实时进度。

### 三道强制评审门禁

每个新切片必须经过以下三道门禁；历史阶段文档中的“开工评审”统一解释为第一道完整设计评审，不能再用一句范围说明替代切片设计：

1. **切片设计评审**：冻结根因、依赖、范围、非目标、数据身份、状态/生命周期、迁移和失败恢复、公共 API/schema/hash/network 影响、高频路径、文件边界与测试矩阵。发现切片仍横跨多个不可独立验证的权威边界时，继续拆分；`Design Accepted`前禁止编码。
2. **实现符合性评审**：状态使用 `Conformance Review`。检查实现是否严格遵守已接受设计，是否复用同一 Published Card、validator、compiler 和 runtime；Draft/Published/Installed/Runtime 是否分层；失败是否保留旧 revision/evaluator；Tick 是否避免 JSON、catalog、UI、LINQ、临时集合和字符串拼接。发现必须扩大设计时退回第一道门禁，不能在评审中默许范围漂移；通过后标记 `Conformance Accepted`。
3. **收口验收评审**：状态使用 `Closeout Review`。按切片设计的证据矩阵逐项复核 build、focused tests、Unity Console、迁移/确定性和真实表现。UI 切片必须使用当前 Game View 的直接帧缓冲，不得用目标效果图代替；authority 切片必须提供 snapshot/hash/rollback/replay 证据；迁移切片必须覆盖旧存档、缺失卡和不兼容 revision。通过后才可标记 `Implementation Accepted`并提交。

评审结论语义固定：

- `Design Accepted`：切片设计完整，可以开始本切片实现；不能开始后续切片。
- `Conformance Accepted`：实现符合已接受设计，可以进入完整验证与事实收口；不能跳过收口验收。
- `Implementation Accepted`：实现与证据完整，可以提交；提交前仍需核对范围和事实文档。
- `Accepted`：仅用于历史宏观阶段，表示该阶段合同及其子切片均已完成。
- `Accepted with debt`：仅在核心合同完整且非阻断债务有明确归属时使用。
- `Design/Conformance/Closeout Changes Requested`：存在可修复缺口，修复后重新执行对应门禁；不得推进状态。
- `Blocked`：依赖或权威合同未解决，禁止用临时 UI/兼容分支绕过。

### 提交和事实源约束

- 每个可验收子阶段单独提交；机械拆分、行为变化、schema/migration 和 UI 发布产物应尽量保持可审查边界。
- 设计文档与实现可以位于同一最终提交，但设计必须在编码前独立评审并留下结论；若设计本身是一个需要先冻结的跨切片合同，应先作为独立文档提交。
- 提交必须包含与本阶段匹配的测试和文档；Composer-owned FairyGUI 仍通过语义资源、配方、Screen Blueprint、binding contract 和 helper 物化/发布。
- `CAPABILITIES.md` 只记录已经通过验收的当前能力；设计目标、目标图和未通过的兼容入口必须明确标记为 target/unimplemented。
- `Apply F+1` 只证明当前 Lab session candidate 可用，不能作为 Publish、玩家解锁或多人合法性的验收证据。
- 任一阶段若改变已接受的 `6` 槽/`10` 费、整卡解锁、战前锁定、免费保底或同语义复用原则，必须先修订统一设计并单独完成设计评审。

## 后续路线

后续扩展应按“数据准备 -> 条件/评分 -> 行为策略 -> 调试解释”的顺序推进：

- 行为策略扩展：优先用行为定义和行为组实例表达远程风筝、危险游斗、技能施法、激进冲锋、守卫防御、伏击者和控制控场等 profile 的核心行为，再决定是否保留手写 v3 profile 模板作为 debug/export 产物。
- 行为需求拆分：行为定义/行为组、Behavior Loadout、玩家可见策略、Ability / CharacterAction 链路预览和行为卡编辑器分别以 `Docs/Tasks/CHARACTER_TEST_RUNTIME_AI_PLANNER_BEHAVIOR_COMPOSITION_01.md`、`Docs/Tasks/CHARACTER_TEST_RUNTIME_AI_PLANNER_BEHAVIOR_LOADOUT_02.md`、`Docs/Tasks/CHARACTER_TEST_RUNTIME_AI_PLANNER_PLAYER_BEHAVIOR_PRESENTATION_03.md`、`Docs/Tasks/CHARACTER_TEST_RUNTIME_AI_PLANNER_ACTION_CHAIN_PREVIEW_04.md`、`Docs/Tasks/CHARACTER_TEST_RUNTIME_AI_PLANNER_BEHAVIOR_CARD_EDITOR_05.md` 为历史需求入口；行为卡当前实现基线记录在 `Docs/Tasks/CHARACTER_TEST_RUNTIME_AI_PLANNER_BEHAVIOR_CARD_EDITOR_UI_FOUNDATION_06.md` 和 `Docs/Tasks/CHARACTER_TEST_RUNTIME_AI_PLANNER_BEHAVIOR_CARD_CURRENT_BASELINE_07.md`。旧 `BEHAVIOR_CARD_SCHEMA_DRAFT_07` 到 `BEHAVIOR_CARD_PUBLISH_LOADOUT_11` 已标记为过时，后续 schema、compiler、runtime output、追击 vertical slice 和 publish/loadout 必须先按当前基线重新切片。
- 运行时开发者工具：下一步优先实现 `Docs/Tasks/CHARACTER_TEST_RUNTIME_AI_PLANNER_RUNTIME_AI_LAB_SCENE_08.md` 的独立 Runtime AI Lab 场景，用真实运行时帧数据做暂停、单帧、可视化和 session-only 调参验证，再决定哪些行为卡/schema/compiler 切片值得正式化。
- 寻路基础：Runtime AI Planner 的 navigation / pathfinding 扩展以 `Docs/Guides/CHARACTER_TEST_RUNTIME_AI_PLANNER_PATHFINDING_FOUNDATION.md` 为入口；先稳定 CharacterTest internal direct/probe/path sample，再评估是否沉淀框架级 API。
- Facts 编辑体验：继续补多条件组合模板、profile 差异检查和更明确的非法组合解释。
- 调试可视化：Play Monitor 后续可增加 score breakdown、line-of-sight / environment sample 简图或可复制单 agent report。
- 性能基线：为 sensing pipeline 增加 focused GC / allocation 回归和 CombatPhysics query 成本基线。
- Profile 管理：行为库 picker 已覆盖 profile 列表、搜索、复制和基础绑定状态；后续继续补 profile 差异检查和场景/角色绑定一致性检查。
- 玩法 UI 接入：行为 Loadout 已提供 catalog、selection command 和 snapshot/hash 基础；后续战前/安全区 UI、成长解锁和装备绑定应接同一套 allowed list / command / authority provider，不绕过为直接改 evaluator。
- 可选 hot reload 扩展：现有行为组 debug gate 只支持当前会话行为实例/参数 overlay；它与行为 Loadout 权威选择是两条路径，不能把 debug overlay 结果静默写入 SaveState/network/replay/hash。

暂不做的事项：

- 不做自由节点图；当前状态流程图只是只读预览和定位入口。
- 不做通用框架级 AI authoring schema。
- 不做外部 Workbench。
- 不做旧 WGame AIAction Config 迁移。
- 不引入第三方行为树、GOAP 插件、Unity NavMesh authority 或 Unity PhysX authority。

## 验证入口

涉及 Runtime AI Planner C# 改动时，至少按本次风险读取 `Docs/API_STANDARDS.md`、`Docs/QUALITY_GATE.md`、`Docs/Interfaces/AI.md`，并追加 Runtime / Combat / Gameplay / Editor 相关接口文档。

常用 focused 验证入口：

- JSON / compiler / validator / editor model：`CharacterTestRuntimeAiPlannerFormalizationTests`
- Runtime command source 与默认行为回归：同一 focused tests 内的 command module 和 compatibility tests
- Editor 界面结构、流程图预览、Play Monitor 和交互约束：`Docs/Guides/CHARACTER_TEST_RUNTIME_AI_PLANNER_EDITOR_DEVELOPMENT.md`
- Unity 编译与 Console：按 `AGENTS.md` 的 Unity MCP 验证约定 refresh/compile 后读取 error
- 文档索引：`python Tools/Harness/check_docs_health.py`

保存 profile 后不代表当前 Play Mode runtime 已更新；需要重建 CharacterTest 会话后再观察行为变化。
## Arena Capability-Slot Authoring Boundary

Arena 与 Runtime AI Lab 共用五类行为职责：`Targeting`、`EngageOrPressure`、`Damage`、`Fallback` 与 `Other`。每类职责都允许零张、一张或多张行为；缺失和全部禁用只产生提示，不阻断保存、发布或测试。职责兼容性由行为的主、次能力决定，不兼容的旧归属会阻断运行，但草稿仍可恢复修正。编辑器不会为缺失职责补占位行为。

The runtime path is unchanged. Session build or the Arena selection phase compiles the current draft into the existing Runtime AI Planner profile/evaluator slot. Combat Tick then reads only the compiled evaluator and existing facts/actions. Tick must not read behavior definition JSON, UI search text, capability labels, forecast rows, or card presentation strings.

新建、加载、删除、禁用、职责拖放、撤销、重做、导入、发布与启动预检统一经过 Responsibility Policy。旧 `slotKind` 一对一迁移为 `responsibilityKind`，其中 `Flexible` 迁移为 `Other`；迁移不得新增、复制、删除、替换、启用、禁用或重排行为。职责归属进入行为组内容哈希，但不进入编译运行语义哈希、SaveState、network、snapshot 或 replay。

Outcome forecast rows are low-frequency explanations derived from behavior entries, conditions, score/cost, output actions, and compiler diagnostics. They estimate which card would be selected in fixed typical scenarios, but they do not simulate real navigation, animation hit frames, damage, victory, or arena balance.
