# MxFramework 文档索引

> Status: Current
>
> 版本 0.7.56 | 2026-08-27
>
> 本目录定义框架的长期设计、接口边界、开发流程和验收标准。

---

## 分类入口

按读取目的选择入口；文件夹名、任务完成标签和历史测试次数不能单独证明当前能力。

| 目的 | 入口与读取边界 |
|------|----------------|
| 当前操作 | [USAGE](USAGE.md)、[AUTHORING_WORKFLOW](AUTHORING_WORKFLOW.md)，再按下方快速入口选专项指南；Combat Editor 操作见 [Character Action Guide §9.1](Guides/CHARACTER_ACTION_AUTHORING_GUIDE.md#91-combat-authoring)。指南内的 v0 / 草案 / Demo 限制继续有效。 |
| 接口合同 | [INTERFACES](INTERFACES.md#模块接口文档) 定位模块公开合同与源码 / 测试；[API_STANDARDS](API_STANDARDS.md) 和 [QUALITY_GATE](QUALITY_GATE.md) 分别维护 API 规范与验证要求。 |
| 设计与决策 | [指南和设计](#指南和设计) 中的 Design / Draft 条目及[归档和决策](#归档和决策)中的 ADR；接受设计不等于全部实施，ADR 以其 Decision Status 为准。 |
| 未完成与活跃工作 | [未完成与活跃工作](#未完成与活跃工作)，再按 [PROJECT_INDEX](PROJECT_INDEX.md) 找到专项台账；Partial / Target / Proposed 及验证缺口必须继续保留。 |
| 证据与历史 | [证据与历史](#证据与历史)；验收记录按当时范围、基线和产物读取，生成清单、schema 和图片按引用消费，不作为新测试结果。 |

Docs 维护工程文档；[StorySource/玩法基石](../StorySource/玩法基石/) 保持独立玩法权威。跨目录链接用于追溯，不表示将玩法权威归入 Docs。

## 快速入口

| 你需要 | 读这个 |
|--------|--------|
| 想知道框架当前能做什么 | `CAPABILITIES.md` |
| 开始接入和查看最小示例 | `USAGE.md` |
| 使用外部 Authoring 工具或 CLI | [AUTHORING_WORKFLOW](AUTHORING_WORKFLOW.md) 的当前管线与边界；命令示例见 [工具 README](../Tools/MxFramework.Authoring/README.md) |
| 检查 Mod Package、Loadout、合并与诊断 | [USAGE](USAGE.md) §7.2–§7.5（包加载、CLI、快照与 EditorServer API）；运行时边界见 [Config 接口](Interfaces/Config.md) |
| 使用现有 UI Toolkit Runtime Showcase 或通用控件 | [运行与场景配置](USAGE.md#64-ui-toolkit-runtime-showcase)；控件合同见 [UI Toolkit 接口](Interfaces/UI.Toolkit.md)；能力状态见 [CAPABILITIES](CAPABILITIES.md) |
| 战术卡短句、详细规则与Web目录 | [描述显示契约](Interfaces/TacticalCard.DescriptionFlow.md)、[Editor Hub](../Tools/MxFramework.EditorHub/README.md#combat-design-card-directory)；49来源记录含20正式卡，16组件含功能Step后撤 |
| 当前混合战术切片、Continue与多进度 | [Demo走查](Walkthroughs/TACTICAL_CARD_PLAYABLE_DEMO_WALKTHROUGH.md#入口与操作)、[战术内容与存档](Guides/CHARACTER_TACTICAL_CONTENT.md#存档)；Continue只恢复战斗外checkpoint |
| 敌方行为卡明牌与人物共享战斗表现设计 | [已确认 UI 方案](../StorySource/玩法基石/半即时动作卡牌UI基石.md#敌方行为卡明牌2026-10-05-已确认待实现核查)、[角色共享契约](Guides/CHARACTER_TACTICAL_CONTENT.md#共享战斗表现设计2026-10-05-已确认待实现核查)；2026-10-08 源码与FairyGUI发布已接入，原生短验1/1通过（展开、换敌不改目标、暂停冻结、重开）；自然hover、死亡、离屏与换卡视觉未纳入；[实现与验证边界](Interfaces/CharacterTest.TacticalCard.md#敌方行为卡明牌设计2026-10-05-已确认待实现核查) |
| 角色头像／立绘制作、继承、发布与整备消费者 | [角色图契约](Guides/CHARACTER_PRESENTATION_IMAGES.md)；原生验证与正式资源导入以该契约的待办状态为准 |
| 按任务查找最直接的专项权威 | `PROJECT_INDEX.md` |
| 查模块边界和公共 API | `INTERFACES.md`、`Interfaces/*.md` |
| 规划或开发 Mx 关卡编辑器、世界对象、角色、物理、Trigger、资源和流程接入 | 先读 `LEVEL_EDITOR_CAPABILITY_REUSE_MATRIX.md`；当前已交付操作读 `Guides/LEVEL_EDITOR_QUICK_PLACEMENT_GUIDE.md`；第二阶段定义库与 Trigger 闭环读 `LEVEL_EDITOR_DEFINITION_DRIVEN_GAMEPLAY_DESIGN.md` 和 `Guides/LEVEL_EDITOR_DEFINITION_DRIVEN_GAMEPLAY_DEVELOPMENT.md`；目录引用控件读 P3.0 `LEVEL_EDITOR_COMMON_AUTHORING_REFERENCES_DESIGN.md`，目标来源/身份/关系/Tag/Status 分层读 P3.1 `LEVEL_EDITOR_TARGET_SEMANTICS_DESIGN.md` 与对应 `Guides/` 开发文档；可编辑 Combat 区域目标筛选读 P3.2 `LEVEL_EDITOR_TARGET_FILTER_AUTHORING_DESIGN.md` 与开发指南；统一事件/条件/动作和顶层 Trigger 工作区读 P3.3 `LEVEL_EDITOR_UNIFIED_TRIGGER_WORKSPACE_DESIGN.md` 与 `Guides/LEVEL_EDITOR_UNIFIED_TRIGGER_WORKSPACE_DEVELOPMENT.md`；再按矩阵进入对应 `Interfaces/*.md` |
| 设计或开发 CharacterTest 创世验证场、验证站、Canon 追溯、场景引用和 Runtime 观察闭环 | [Genesis现行工程合同](CHARACTER_TEST_GENESIS_VALIDATION_SCENE_FRAMEWORK_DESIGN.md#固定输入与探针合同)（区分固定probe、实体状态、配置与未完成P2）、`LEVEL_EDITOR_CAPABILITY_REUSE_MATRIX.md`、`../StorySource/Projects/WGame/genesis_stage_overview.md`、`Interfaces/Editor.md` |
| 设计或实现 Platform 路径/生命周期、SaveGame 存档槽和 Application 启动事务 | `Decisions/ADR-009-platform-contract-authority.md`、`Decisions/ADR-010-savegame-persistence-authority.md`、`Decisions/ADR-011-application-startup-authority.md`，再读三份对应设计；当前 Runtime/Scene/Resource 事实另读 `Interfaces/Runtime.md`、`Interfaces/AppFlow.md`、`Interfaces/Resources.md` |
| 接入或继续设计 Gameplay 区域/阈值/Signal 触发、Enter/Stay/Exit、条件动作、冷却次数和存档回放 | `Interfaces/Gameplay.Triggers.md`、`GAMEPLAY_TRIGGER_RUNTIME_DESIGN.md`、`Decisions/ADR-008-gameplay-trigger-runtime-authority.md`、`Interfaces/Gameplay.md`、`Interfaces/Runtime.md` |
| 设计或开发物理交互驱动音频、Basis Sound、声学材质、运行时选音和 Workbench 覆盖预览 | `AUDIO_PHYSICAL_EXPRESSION_DESIGN.md`、`Decisions/ADR-007-audio-physical-expression-pipeline.md`、`Interfaces/Audio.md`、`Interfaces/Combat.md` |
| 全面评审或优化游戏音频管理、FMOD 生命周期、Bus/Settings、Bank、Listener/Emitter、诊断和发布验收 | `Guides/AUDIO_SYSTEM_OPTIMIZATION_PLAN.md`、`Interfaces/Audio.md`、`AUDIO_SYSTEM_FMOD.md` |
| 使用 UI 命令 invocation/outcome/反馈路由，或设计正式玩家 UI 的自动 Click、Back、Confirm、Error 音效反馈与 binding contract v2 | 当前 Core 读 `Interfaces/UI.md`；目标链路读 `UI_AUDIO_FEEDBACK_AUTOMATION_DESIGN.md`、`FAIRYGUI_UI_COMPOSER.md`、`Interfaces/Audio.md` |
| 查资源系统当前工作流，或规划/实施资源身份、映射和 Build Plan 长期收敛 | 当前事实读 `RESOURCE_SYSTEM_WORKFLOW.md`、`Interfaces/Resources.md`；目标设计读 `RESOURCE_MAPPING_CONVERGENCE_PLAN.md`；实施状态读 `RESOURCE_MAPPING_CONVERGENCE_IMPLEMENTATION.md` |
| 查 UPM 当前状态、阶段任务、外部消费、目录迁移、可选依赖和 release 权威 | 当前事实先读 `UPM_CURRENT_STATUS.md`；8 个阶段边界与状态读 `UPM_PHASE_TASKS.md`；实施顺序读 `UPM_OPTIMIZATION_ROADMAP.md`；日常接入读 `MULTI_PROJECT_WORKFLOW.md`；再按 Current 状态页进入专项政策 |
| 查完成定义和质量门禁 | `QUALITY_GATE.md` |
| 制作小游戏 / Demo / Runtime Showcase | `AGENT_GAME_CREATION_GUIDE.md` |

| 查 CharacterTest 技能实现类型和模块拼装方式 | `Guides/CHARACTER_TEST_SKILL_IMPLEMENTATION_PATTERNS.md` |
| 规划或实施 CharacterTest 大型角色 Body Presence、软接触、动画附肢软接触、伤口/破坏、断尾或确定性地形姿态 | 先读 `Guides/CHARACTER_TEST_LARGE_CHARACTER_CONTACT_DEVELOPMENT.md`，再只打开当前 LCC-M0～LCC-M5 里程碑文件；移动/战斗权威另读 `Guides/CHARACTER_TEST_GAME_DEVELOPMENT_STANDARD.md`、`Interfaces/Combat.md`，持久状态另读 `Interfaces/Network.md` |
| 使用、开发或核查独立 Projectile Authoring、Definition/Request/Presentation、生命周期 VFX 和确定性预览 | 当前入口 `Guides/PROJECTILE_AUTHORING_GUIDE.md`；未完成目标设计 `PROJECTILE_AUTHORING_EDITOR_DESIGN.md`；模块合同 `Interfaces/Combat.md`、`Interfaces/Vfx.md`；CLI 流程 `AUTHORING_WORKFLOW.md` |
| 开发角色创作 SDK、CharacterStudio、Authoring Service 或 Unity Import Bridge | `CHARACTER_SDK_AUTHORING_PIPELINE_PLAN.md`、`Interfaces/CharacterApplication.md` |
| 开发 CharacterStudio 武器库、角色组合武器配置、装备迁移或 Runtime EquipmentState | `CHARACTER_WEAPON_CONFIGURATION.md` |
| 开发 CharacterTest Runtime AI Planner 行为 / 感知层 / 目标选择 / 投射物瞄准共享数据 | `Guides/CHARACTER_TEST_RUNTIME_AI_PLANNER_DEVELOPMENT.md` |
| 设计 Runtime AI Lab 单行为编辑、行为配方 Track/Event 大时间轴、测试组合、双方阵容设置、可拖动预览时间轴和行为卡发布闭环 | 四区产品流程先读 `Guides/CHARACTER_TEST_RUNTIME_AI_LAB_SINGLE_BEHAVIOR_AUTHORING_PREVIEW_DESIGN.md`；Stage 本地时间轴、自动 Track、Event、Inspector 和交互规则读 `Guides/CHARACTER_TEST_RUNTIME_AI_LAB_BEHAVIOR_RECIPE_TIMELINE_DESIGN.md`；底层与历史能力再读 `Guides/CHARACTER_TEST_RUNTIME_AI_LAB_BEHAVIOR_WORKBENCH.md`、`Guides/CHARACTER_TEST_RUNTIME_AI_LAB_BEHAVIOR_TIMELINE_V2_DESIGN.md`、`Guides/CHARACTER_TEST_RUNTIME_AI_LAB_UX_LAYOUT_DESIGN.md`、`Guides/CHARACTER_TEST_RUNTIME_AI_LAB_DRAFT_MODEL_PERSISTENCE.md` |
| 设计玩家行为卡组、6 槽/10 费、整卡解锁、Published Card 生命周期及 Runtime AI Lab 统一关系 | `Guides/CHARACTER_TEST_RUNTIME_AI_PLANNER_BEHAVIOR_CARD_GAMEPLAY.md` |
| 开发 Runtime AI Lab typed 行为卡条件、步骤、分支和校验 | `Guides/CHARACTER_TEST_RUNTIME_AI_LAB_BEHAVIOR_CARD_PAYLOAD.md` |
| 设计或整改 CharacterTest 帧同步边界 / snapshot / rollback / Runtime AI Lab 回放 | `Guides/CHARACTER_TEST_FRAME_SYNC_BOUNDARY_REMEDIATION.md`、`Guides/CHARACTER_TEST_FRAME_SYNC_DEVELOPMENT.md`、`Interfaces/Runtime.md`、`Interfaces/Animation.md` |
| 拆分 CharacterTest Runtime AI Planner 行为组合 / Loadout / 玩家可见策略需求 | `Tasks/CHARACTER_TEST_RUNTIME_AI_PLANNER_BEHAVIOR_COMPOSITION_01.md`、`Tasks/CHARACTER_TEST_RUNTIME_AI_PLANNER_BEHAVIOR_LOADOUT_02.md`、`Tasks/CHARACTER_TEST_RUNTIME_AI_PLANNER_PLAYER_BEHAVIOR_PRESENTATION_03.md`、`Tasks/CHARACTER_TEST_RUNTIME_AI_PLANNER_ACTION_CHAIN_PREVIEW_04.md` |
| 设计 CharacterTest Runtime AI Planner 寻路 / navigation 基础 | `Guides/CHARACTER_TEST_RUNTIME_AI_PLANNER_PATHFINDING_FOUNDATION.md` |
| 开发 CharacterTest Runtime AI Planner Editor 界面 | `Guides/CHARACTER_TEST_RUNTIME_AI_PLANNER_EDITOR_DEVELOPMENT.md` |
| 创作Rendering或核对Pose/VFX预算、阴影、植被 | [Rendering guide](RENDERING_AUTHORING_GUIDE.md)、[Rendering合同](Interfaces/Rendering.md)、[Pose/VFX工程权威](Guides/PRESENTATION_POSE_VFX_LOD.md)、[阴影权威](Guides/DIRECTIONAL_TRIANGLE_SHADOWS.md)、[植被操作](Guides/BASIC_SCENE_VEGETATION.md)；实现与失败边界由各说明维护 |
| 设计或使用 VFX 语义、Piloto 原材料索引/提取、Primitive/Recipe 复用、双向创作飞轮、Candidate 晋升和 Development Agent 管理 | `VFX_ASSET_AUTHORING_DESIGN.md`、`Interfaces/Vfx.md`、`Demo/MX_VFX_SHOWCASE.md`、`RESOURCE_DIRECTORY_LAYOUT.md` |
| 开发 VFX 语义属性、Profile/Snapshot、Composition Draft v3、曲线编辑、精确重定时或烘焙属性驱动 | `VFX_SEMANTIC_PROPERTY_RETIMING_DESIGN.md`、`VFX_ASSET_AUTHORING_DESIGN.md`、`Interfaces/Vfx.md` |
| 新建或迁移 FairyGUI player-facing UI | 先读 `Guides/FAIRYGUI_UI_COMPOSER_USER_GUIDE.md`、`FAIRYGUI_UI_COMPOSER.md`，再读 `Guides/FAIRYGUI_BINDER_GENERATION_GUIDE.md`、`Decisions/ADR-006-fairygui-binder-generation.md` |
| 使用 FairyGUI 切图语义 catalog、扫描使用点或规划 PNG 替换约束 | `FAIRYGUI_UI_ART_SLICE_CATALOG.md` |
| 使用 UI Composer 编辑资源、配方、节点、布局、恢复草稿或评审发布 | `Guides/FAIRYGUI_UI_COMPOSER_USER_GUIDE.md`，架构边界再读 `FAIRYGUI_UI_COMPOSER.md`、`Decisions/ADR-007-fairygui-semantic-ui-composer-authority.md` |
| 设计或接入正式玩家文字提示、Guidance Card、Notice Banner 或 CharacterTest 目标锁定反馈 | `PLAYER_GUIDANCE_PROMPT_UI_DESIGN.md`、`Interfaces/UI.md`、`Interfaces/UI.FairyGUI.md`；涉及动作提示、binding 选择或重绑定刷新时再读 `Interfaces/Input.md` |
| 使用或扩展关卡教学序列、空白步骤、创作预设、可切换 typed 完成条件、Guidance Authoring、语义预览、Scene cache 或 Runtime sequence coordinator | `LEVEL_EDITOR_GUIDANCE_AUTHORING_DESIGN.md`、`Guides/LEVEL_EDITOR_GUIDANCE_AUTHORING_DEVELOPMENT.md`；玩家可见 Guidance Card、Notice Banner 与 FairyGUI Presenter 另读 `PLAYER_GUIDANCE_PROMPT_UI_DESIGN.md`；P3.4 实现状态为 Current |
| 整理 CharacterTest Profiler 性能优化计划 | `Guides/CHARACTER_TEST_PERFORMANCE_OPTIMIZATION_PLAN.md` |
| 排查运行时状态和调试面板 | `Guides/OBSERVABILITY_DEBUGGING_GUIDE.md` |
| 追溯已完成任务的历史证据 | `Tasks/README.md` |

---

## 文档状态

| 状态 | 含义 | 使用规则 |
|------|------|----------|
| Current | 当前事实源 | Agent 和人工默认信任；变更后必须同步维护。 |
| Guide | 当前使用 / 排障指南 | 用于操作流程、接入步骤和手动验证；与源码冲突时回到源码和测试核对。 |
| Design | 设计说明 | 描述长期意图和边界；具体 API 事实以 `INTERFACES.md`、`Interfaces/*.md`、源码和测试为准。 |
| Policy | 当前规则与晋升门禁 | 定义证据、支持等级和发布要求；活动数字仍回到对应 Current 状态页。 |
| ADR | 决策记录 | 保存已接受或已撤销的决策；是否生效以文件内 `Decision Status` 为准。 |
| Archive | 历史证据 | 不代表当前事实；只在追溯、复盘或 Issue 明确引用时读取。 |
| Draft | 未接受草案 | 只能作为讨论材料，不能当成实现或验收依据。 |

---

## 当前事实源

| 状态 | 文档 | 用途 |
|------|------|------|
| Current | `PROJECT_INDEX.md` | 按任务定位最直接专项权威的索引。 |
| Current | `CAPABILITIES.md` | 当前可用能力清单。 |
| Current | `INTERFACES.md` | 接口索引、依赖矩阵和模块边界入口。 |
| Current | `Interfaces/*.md` | 各模块公共 API 和契约。 |
| Current | `QUALITY_GATE.md` | 完成定义、验证要求和质量门禁。 |
| Current | `API_STANDARDS.md` | API 命名、兼容性、GC 和 Unity 依赖标准。 |
| Current | `UPM_CURRENT_STATUS.md` | UPM package identity、surface、E0/E1/E2/E3 evidence ledger、Consumer matrix、readiness gaps 和下一实施顺序的唯一当前入口。 |
| Current | `UPM_PHASE_TASKS.md`、`UPM/Phases/README.md` | UPM-P1 至 P8 的阶段顺序与状态台账，以及每阶段独立的交付物、实施切片、验证和回滚规格。 |
| Current | `UPM_OPTIMIZATION_ROADMAP.md` | Surface 分类、项目内容迁出、Embedded Package cutover、optional integration 和 release train 的当前路线。 |
| Current | `LEVEL_EDITOR_CAPABILITY_REUSE_MATRIX.md` | 关卡编辑器扩展前的模块权威、能力复用矩阵、已确认缺口和新增能力门禁。 |
| Current | `LEVEL_EDITOR_COMMON_AUTHORING_REFERENCES_DESIGN.md` | P3.0 作用目标、触发对象、效果归因、命令来源边界及通用 Editor 引用规则。 |
| Current | `LEVEL_EDITOR_TARGET_SEMANTICS_DESIGN.md` | P3.1 目标来源、实例/定义身份、基础身份/能力、关系、Tag、Status 分层及 Trigger 投影。 |
| Current | `LEVEL_EDITOR_TARGET_FILTER_AUTHORING_DESIGN.md` | P3.2 Combat 区域 Subject 筛选、阵营参照、可编辑 UI、迁移与 Runtime 闭环设计。 |
| In Progress | `LEVEL_EDITOR_UNIFIED_TRIGGER_WORKSPACE_DESIGN.md` | P3.3 顶层玩法触发器工作区、统一事件/条件/动作和数据映射；设计页当前台账为 P3.3a～P3.3c Complete，P3.3d～P3.3e Partial；按对应任务读取未完成范围。 |
| Current | `RESOURCE_SYSTEM_WORKFLOW.md` | 当前资源系统主工作流。 |
| Current | `RESOURCE_MAPPING_CONVERGENCE_IMPLEMENTATION.md` | 资源映射替换的 RM-C1～RM-C6 状态、提交与验证台账。 |
| Current | `STORY_CANON_AUTHORING_DESIGN.md` | WGame StorySource 世界观记忆、故事大纲、输入包审批和 ArtSource handoff 边界。 |
| Current | `RENDERING_PIPELINE.md` | Unity 渲染管线、URP asset 和示例场景/材质基线。 |
| Current | `RENDERING_AUTHORING_GUIDE.md` | Rendering authoring 当前唯一入口。 |

---

## 指南和设计

保留既有专题清单，按状态和正文范围读取：Guide / Current 条目提供维护中的操作或事实；Design / Draft 条目提供目标与讨论；同一页的实施台账、未完成范围和历史证据分别读取，不能仅凭 Status 或文件名推断全部已交付。

| 状态 | 文档 | 用途 |
|------|------|------|
| Guide | `USAGE.md` | 最小接入示例和组合方式。 |
| Guide | `AGENT_GAME_CREATION_GUIDE.md` | Agent 制作小游戏 / Demo / Runtime Showcase 的强制规范。 |
| Guide | `Guides/LEVEL_EDITOR_QUICK_PLACEMENT_GUIDE.md` | 当前关卡编辑器的放置、复制、重新放置、保存、撤销和 Scene 右键操作。 |
| Current | `CHARACTER_TEST_GENESIS_VALIDATION_SCENE_FRAMEWORK_DESIGN.md` | CharacterTest 创世验证场框架 v0.4：P1 已实现 ScenePlan、Zone / Route / Waypoint、Station Spatial Program、5 个模板、owner-backed 一键脚手架、结构 / 几何门禁、量化空间诊断 hash 与 SceneView overlay；P2 executable cache、runner、observer 和 Runtime inspection 仍是 Target。 |
| Current | `LEVEL_EDITOR_DEFINITION_DRIVEN_GAMEPLAY_DESIGN.md` | 关卡编辑器第二阶段的产品、交互、数据权威与运行时闭环；P2.0-P2.5 已实现并完成 CharacterTest offline Playable 验收。 |
| Current | `Guides/LEVEL_EDITOR_DEFINITION_DRIVEN_GAMEPLAY_DEVELOPMENT.md` | 第二阶段 P2.0～P2.5 的开发顺序、文件职责、迁移策略、验证矩阵和完成台账。 |
| Current | `Guides/LEVEL_EDITOR_COMMON_AUTHORING_REFERENCES_DEVELOPMENT.md` | P3.0 通用引用契约、CharacterTest Trigger 适配、兼容策略和验收入口。 |
| Current | `Guides/LEVEL_EDITOR_TARGET_FILTER_AUTHORING_DEVELOPMENT.md` | P3.2 Subject Filter Runtime 契约、CharacterTest v1→v2 迁移、Editor 接入和手工验收。 |
| Current | `Guides/LEVEL_EDITOR_UNIFIED_TRIGGER_WORKSPACE_DEVELOPMENT.md` | P3.3 区域来源、当前玩家 HP Threshold、共享 owner、效果句与 Scene 集成的当前开发入口；§1 列出五项任务状态，P3.3d～P3.3e 仍为 Partial。 |
| Design | `LEVEL_EDITOR_GUIDANCE_AUTHORING_DESIGN.md` | 已接受且实现状态为 Current 的 Level Editor P3.4 产品与架构设计：`characterTest.guidance`、空白步骤、六个创作预设、五类可切换 typed completion condition、三栏编辑器、Scene cache、Coordinator/Observer/Mapper 与 fail-closed 切换均已交付。 |
| Design | `Guides/LEVEL_EDITOR_GUIDANCE_AUTHORING_DEVELOPMENT.md` | 已实现的 P3.4a～P3.4e 开发与回归指南，记录自由步骤 Authoring、条件切换、项目私有类型职责、统一保存、cache freshness、测试矩阵和 2026-08-13 收口证据。 |
| Guide | `Guides/CHARACTER_RUNTIME_MODULE_RELATIONSHIP.md` | Character runtime、Gameplay、Combat、CharacterAction、Character.Application 关系速查。 |
| Design | `Guides/CHARACTER_TEST_FRAME_SYNC_BOUNDARY_REMEDIATION.md` | CharacterTest 帧同步边界整改：区分 Frame Sync、Authority Snapshot、Rollback Checkpoint、Runtime AI Lab Replay 和 Presentation Diagnostics，并定义 BootstrapOnly 目标。 |
| Guide | `Guides/CHARACTER_TEST_FRAME_SYNC_DEVELOPMENT.md` | CharacterTest 现有帧同步与 Lab 排障记录；内容包含 authority 和 presentation diagnostics，新增工作必须先按边界整改文档分类。 |
| Guide | `Guides/CHARACTER_TEST_ATTRIBUTE_DESIGN.md` | CharacterTest 专用属性、伤害、韧性压力 / 姿态债务、UI/Debug/Telemetry 和未来扩展接入规则。 |
| Guide | `Guides/CHARACTER_TEST_SKILL_IMPLEMENTATION_PATTERNS.md` | CharacterTest 技能实现类型资料库：按 projectile、beam、chain、aura、shield、zone、interaction 等模式记录模块链路和权威边界。 |
| Guide | `Guides/CHARACTER_TEST_LARGE_CHARACTER_CONTACT_DEVELOPMENT.md` | CharacterTest 大型角色接触路线总台账：冻结 Hard Root / Body Presence / Animated Hurtbox / Active Trace / Animated Soft Presence 分层；M0～M5 六个里程碑与36/36任务均已 Verified。 |
| Guide | `Guides/CHARACTER_TEST_RUNTIME_AI_PLANNER_DEVELOPMENT.md` | CharacterTest Runtime AI Planner 开发记录：架构边界、JSON profile、结构化编辑器、共享帧感知缓存、感知数据准备层和后续路线。 |
| Guide | `Guides/CHARACTER_TEST_RUNTIME_AI_PLANNER_PATHFINDING_FOUNDATION.md` | CharacterTest Runtime AI Planner 寻路基础：M1 capsule-aware local probe、共享感知缓存复用、CombatPhysics 权威、path graph / A* 路线和验证要求。 |
| Guide | `Guides/CHARACTER_TEST_RUNTIME_AI_PLANNER_EDITOR_DEVELOPMENT.md` | CharacterTest Runtime AI Planner Editor 开发记录：界面结构、代码拆分、交互原则、流程图预览、Play Monitor 和验证入口。 |
| Design | `Guides/CHARACTER_TEST_RUNTIME_AI_LAB_SINGLE_BEHAVIOR_AUTHORING_PREVIEW_DESIGN.md` | Runtime AI Lab 单行为创作当前产品权威：只编辑一张项目行为卡，以本地测试组合、双方阵容设置、不可变 Run Snapshot 和可拖动 RuntimeFrame 预览完成验证；不编辑或发布正式行为组。 |
| Current | `Guides/CHARACTER_TEST_RUNTIME_AI_LAB_BEHAVIOR_RECIPE_TIMELINE_DESIGN.md` | Runtime AI Lab 行为配方大时间轴专项权威：同级 Stage 本地时间轴、自动 Track、复合 Event、右上 Inspector、双轴滚动、原子编辑、Composer 合同和验收边界。 |
| Design | `Guides/CHARACTER_TEST_RUNTIME_AI_LAB_TACTICAL_BEHAVIOR_TIMELINE_OPTIMIZATION.md` | Runtime AI Lab 战术行为时间轴优化设计：以“0 配置数据”为目标的动作元数据级联驱动、复合弹性门禁（距离+朝向）与双重打断边界。 |
| Design | `Guides/CHARACTER_TEST_RUNTIME_AI_LAB_BEHAVIOR_WORKBENCH.md` | Runtime AI Lab 总体运行、数据权威和历史工作台能力；单行为创作界面已退位给上一条专项设计。 |
| Design | `Guides/CHARACTER_TEST_RUNTIME_AI_LAB_BEHAVIOR_TIMELINE_V2_DESIGN.md` | Behavior Plan 内部时间与高级 RuntimeFrame 能力的复用参考，并保留统一三 Tab、完整五轨、A/B 等历史设计；不再定义默认单行为创作表面。 |
| Design | `Guides/CHARACTER_TEST_RUNTIME_AI_LAB_UX_LAYOUT_DESIGN.md` | 已实现 Setup、场景优先 Runtime Test 和历史响应式布局参考；单行为编辑布局以专项权威设计为准。 |
| Design | `Guides/CHARACTER_TEST_RUNTIME_AI_LAB_DRAFT_MODEL_PERSISTENCE.md` | 既有完整实验 workspace/group/card 草稿身份、本地恢复和 copy-on-write 合同，并记录单行为 Test Context 的覆盖关系。 |
| Accepted | `Guides/CHARACTER_TEST_RUNTIME_AI_PLANNER_BEHAVIOR_CARD_GAMEPLAY.md` | Runtime AI Planner 行为卡玩家玩法与 Lab 统一合同：每角色 6 槽/10 费、整卡解锁、推荐预设、发布生命周期、权威校验及当前/目标边界。 |
| Current | `Guides/CHARACTER_TEST_RUNTIME_AI_LAB_BEHAVIOR_CARD_PAYLOAD.md` | Runtime AI Lab typed 行为卡 payload：白名单条件、Ability/CharacterAction 引用、步骤/分支/End、稳定 JSON 和结构校验。 |
| Draft | `Tasks/CHARACTER_TEST_RUNTIME_AI_PLANNER_BEHAVIOR_COMPOSITION_01.md` | CharacterTest Runtime AI Planner 行为组合层需求：模块目录、组合 profile、v3 profile 编译和 debug overlay 边界。 |
| Draft | `Tasks/CHARACTER_TEST_RUNTIME_AI_PLANNER_BEHAVIOR_LOADOUT_02.md` | CharacterTest Runtime AI Planner Behavior Loadout 需求：catalog、selection command、权威状态、snapshot/hash/replay 边界。 |
| Draft | `Tasks/CHARACTER_TEST_RUNTIME_AI_PLANNER_PLAYER_BEHAVIOR_PRESENTATION_03.md` | CharacterTest Runtime AI Planner 玩家可见行为需求：策略卡、战斗短状态、presentation view model 和 debug 字段隔离。 |
| Draft | `Tasks/CHARACTER_TEST_RUNTIME_AI_PLANNER_ACTION_CHAIN_PREVIEW_04.md` | CharacterTest Runtime AI Planner 动作链路预览需求：BehaviorLoadout 到 Ability / CharacterAction / gameplay request 的可读摘要和诊断。 |
| Guide | `Guides/CHARACTER_TEST_PERFORMANCE_OPTIMIZATION_PLAN.md` | CharacterTest Profiler Analyzer 基线、异常尖峰排除口径和分阶段性能优化计划。 |
| Guide | `Guides/AUDIO_SYSTEM_OPTIMIZATION_PLAN.md` | 游戏音频管理全面评审基线、问题台账、Runtime/Physical/Mix/Bank/平台优化阶段和发布验收矩阵。 |
| Guide | `Guides/FAIRYGUI_BINDER_GENERATION_GUIDE.md` | FairyGUI generated manifest/binder 的面板接入、文件职责和验证入口。 |
| Guide | `Guides/UI_ART_RESOURCE_LIBRARY_GUIDE.md` | UI 切图从 PNG 入库、语义/约束维护、替换、FairyGUI 接入配方、人工导入发布和排障的中文操作指南。 |
| Guide | `Guides/FAIRYGUI_UI_COMPOSER_USER_GUIDE.md` | UI Composer v5 中文操作手册：三层工作台、节点、容器与 List、快捷键、自动恢复、诊断、批量替换、快照评审和发布排障。 |
| Guide | `FAIRYGUI_UI_ART_SLICE_CATALOG.md` | UI 切图资源库与 FairyGUI 使用审计：`MxUiFoundation` 手工资源库、推荐 FairyGUI 参数、替换约束、EditorHub 外部主编辑器、Unity fallback、以及 `MxCharacterTestHud` 旧包 scanner/report 审计边界。 |
| Guide | `FAIRYGUI_UI_COMPOSER.md` | 语义 UI Composer 的蓝图、组件配方、受控双向同步、FairyGUI helper、发布和 Unity 真包预览工作流。 |
| Guide | `Guides/OBSERVABILITY_DEBUGGING_GUIDE.md` | Debug UI、日志、timeline、hot reload、commands 和 Simulation Harness 排障。 |
| Guide | `Demo/CONFIG_DEMO.md` | Config Demo 内置源、字段示例和引用验证说明。 |
| Guide | `Demo/ITEM_LIFECYCLE_SHOWCASE.md` | Item Lifecycle Showcase 入口、状态流转、按钮操作和验收说明。 |
| Guide | `Demo/MX_ANIMATION_SYSTEM_SHOWCASE.md` | MxAnimation System Showcase 入口、手测流程和验收清单。 |
| Guide | `Demo/MX_VFX_SHOWCASE.md` | 当前 VFX prefab / RuntimeDefinition 集中预览入口、手测流程和验收清单。 |
| Design | `GAMEPLAY_TRIGGER_RUNTIME_DESIGN.md` | Gameplay Trigger Runtime 分阶段设计：Phase 1、19.2A-H2、19.3A-D、19.4A checkpoint/door/trap Playable与19.4B bounded network/scale均已实现并验收；Phase 19父计划完成。 |
| Design | `DESIGN.md` | 项目目标、非目标和总体模块定位。 |
| Design | `ARCHITECTURE.md` | 架构契约、依赖方向、生命周期和错误处理。 |
| Design | `RUNTIME_FOUNDATION_SYSTEM.md` | Runtime Host、Frame/Command/Replay、SaveState 设计。 |
| Design | `PLATFORM_CONTRACTS_DESIGN.md` | Platform noEngine facts、Unity adapter、路径/location、focus/suspend/quitting notification、权限和非权威 wall clock 设计。 |
| Design | `SAVEGAME_PERSISTENCE_DESIGN.md` | Runtime checkpoint 之上的 profile/slot、envelope、完整性、原子 generation commit、候选 session restore 和 recovery 设计。 |
| Design | `APPLICATION_STARTUP_ORCHESTRATION_DESIGN.md` | AppFlow/SceneFlow/Resources/Persistence 之外层 plan、step、candidate transaction、取消、回滚和 owner handoff 设计。 |
| Archive | `UPM_EXTERNAL_CONSUMPTION_IMPLEMENTATION_PLAN.md` | Phase A-C 建立 preview UPM Consumer 边界的已接受历史执行记录；不代表活动 `main`。 |
| Design | `UPM_PACKAGE_SURFACE_LAYOUT_OPTIMIZATION.md` | Package surface owner、CharacterTest/Tests/Demo 迁出和 Embedded Package 最终拓扑。 |
| Policy | `UPM_CONSUMER_BASELINE_EVIDENCE_OPTIMIZATION.md`、`UPM_OPTIONAL_INTEGRATION_OPTIMIZATION.md`、`UPM_RELEASE_UPGRADE_READINESS_PLAN.md` | Consumer 证据晋升、optional adapter 支持等级和 release/Player/upgrade 门禁。 |
| Design | `RESOURCE_MANAGEMENT_SYSTEM.md` | 资源管理系统设计、加载契约和阶段切片。 |
| Design | `RESOURCE_MAPPING_CONVERGENCE_PLAN.md` | 资源身份、Source Locator、显式 Bundle authority 和共享 Build Plan 编译层的长期收敛计划。 |
| Design | `RENDERING_FRAMEWORK_DESIGN.md` | Rendering 总线、URP Feature、Context、SharedRT 和 Bridge 边界。 |
| Design | `PROJECTILE_AUTHORING_EDITOR_DESIGN.md` | 独立 Projectile Authoring 工作台、Definition/Request/Presentation 权威、生命周期 VFX、无损编译事务、确定性预览、迁移和验收设计。 |
| Design | `VFX_ASSET_AUTHORING_DESIGN.md` | VFX 美术资产语义、Piloto Source Library 原材料提取、Primitive/Recipe 复用、Published Effect、预览、校验和 Development Agent 管理设计。 |
| Design | `VFX_SEMANTIC_PROPERTY_RETIMING_DESIGN.md` | VFX Source Snapshot、Component/Element/Shader Profile、受控语义属性、Draft v3、六种精确重定时、工作台编辑、Candidate 烘焙和 NoAlloc 属性驱动详细设计。 |
| Workflow | `VFX_DEVELOPMENT_AGENT_EXECUTION.md` | 本机 Development Agent 的 Fireball Impact 六方向任务、Unity MCP 创作、回执、A/B 预览与人工晋升流程。 |
| Design | `AUDIO_PHYSICAL_EXPRESSION_DESIGN.md` | 物理交互双方声学属性、Basis Sound、确定性选择、响应曲线、Workbench 预览和发布闭环设计。 |
| Design | `UI_AUDIO_FEEDBACK_AUTOMATION_DESIGN.md` | 正式玩家 FairyGUI 命令的自动 Click/Back、成功/失败反馈、binding contract v2、结果关联和 Audio Cue 边界设计。 |
| Design | `PLAYER_GUIDANCE_PROMPT_UI_DESIGN.md` | 当前采用的正式玩家文字 Guidance 设计：固定 Card、两槽 Notice、noEngine contract、FairyGUI package、CharacterTest 组合和权威边界；实现与验证部分是带日期的快照，当前事实回到 Interfaces、源码和测试核对。 |
| Design | `COMBAT_ANIMATION_PHYSICS.md` | 动作战斗确定性动画 / 物理协作方案。 |
| Design | `AUTHORING_EDITOR_PROGRAM.md` | 外部主创编辑器总规划。 |
| Design | `CHARACTER_SDK_AUTHORING_PIPELINE_PLAN.md` | 已接受的角色创作 SDK 分层、CharacterStudio 客户端边界、版本兼容和端到端就绪门禁。 |
| Design | `CHARACTER_WEAPON_CONFIGURATION.md` | 已接受且已落地的武器配置 4.0 开发基准：全局单一武器/动画/能力/受击配置、角色组合配方及私有动作。 |
| Design | `ROADMAP.md` | 阶段规划和方向，不作为当前能力或 API 事实源。 |
| Draft | `RESOURCE_DIRECTORY_LAYOUT.md` | 资源正式目录、ResourceKey 命名和 Catalog 归档草案。 |
| Draft | `CHARACTER_RESOURCE_PACKAGE_AUTHORING.md` | 角色资源包创作管线草案。 |
| Draft | `CHARACTER_RESOURCE_PACKAGE_IMPLEMENTATION_PLAN.md` | 角色资源包工程落地草案。 |
| Draft | `DESIGN_ATTRIBUTE_AI_SYSTEM.md` | 属性与 Runtime AI Planner 联动讨论草案。 |
| Guide | `AUTHORING_WORKFLOW.md` | 创作流程跨 Unity / Mod Editor / Authoring AI Assist / CLI 协作。 |
| Guide | `Guides/PROJECTILE_AUTHORING_GUIDE.md` | Projectile 组当前入口：工作台、字段与槽位、事务、runtime/preview、consumer 和明确未证明边界。 |
| Guide | `EDITORS.md` | Unity Editor 工具规范。 |
| Guide | `../Tools/MxFramework.EditorHub/README.md` | 外部编辑器中心入口。 |
| Guide | `../Tools/GiteaGithubSync/README.md` | Gitea Issue / PR 元数据手动镜像到 GitHub。 |

---

## 未完成与活跃工作

这里只提供读取入口，不建立第二份完成台账；状态以被链接文档的当前范围为准。

| 工作 | 当前状态入口与边界 |
|------|--------------------|
| Level Editor P3.3 | [统一触发器工作区开发指南](Guides/LEVEL_EDITOR_UNIFIED_TRIGGER_WORKSPACE_DEVELOPMENT.md) §1：a～c Complete，d / e Partial；[d](Tasks/LEVEL_EDITOR_P3_3D_COMPACT_CLAUSE_UI_CLOSEOUT.md) / [e](Tasks/LEVEL_EDITOR_P3_3E_BOOLEAN_CONDITION_AUTHORING.md) 保留未完成规格。 |
| Genesis 后续阶段 | [现行设计](CHARACTER_TEST_GENESIS_VALIDATION_SCENE_FRAMEWORK_DESIGN.md)：P1 与 P2 Target 分开读取；P1 验收不能替代 P2 executable cache / runner / observer。 |
| UPM 阶段与 readiness | [UPM_CURRENT_STATUS](UPM_CURRENT_STATUS.md)、[UPM_PHASE_TASKS](UPM_PHASE_TASKS.md)；本批门禁18/18、Consumer20/20、strict E0零阻断；旧Consumer14项与E0两项失败状态已被本次回执替代，范围见[当前状态](USAGE.md)，不扩成所有未来门禁通过。 |
| 当前确定性修复 | [Runtime](Interfaces/Runtime.md)、[Network](Interfaces/Network.md)：2026-10-08 的24文件修复已完成本地主合，离线357/357、原生关键分组121/121；StageFlow广组42/43中的控制释放合同冲突已由基线单例复现，保留还是取消待用户选择，过程异常与未验范围见[本批状态](USAGE.md)。 |
| 第11项 Docs 整理 | 已完成约定的两份Genesis剩余历史迁移、引用修复与B1旧截图缺链纠正；原缺失截图未伪造。当前入口／任务状态已同步，不扩展历史清理；缺链不等于补获原截图。 |
| AI Lab与Pose/VFX实施边界 | [AI Lab工程权威](Guides/CHARACTER_TEST_RUNTIME_AI_LAB_BEHAVIOR_WORKBENCH.md#真实出牌试验当前实现与边界)、[Pose/VFX工程权威](Guides/PRESENTATION_POSE_VFX_LOD.md)、[拖尾权威](Guides/WEAPON_BLADE_TRAIL.md)；不在索引复制其完成状态和测试数 |
| 战前准备与动作路径修复 | generation空引用修复与右键取消原生通过；四包Combo02＋katana共118条映射、11武器118条接触／actor BakedSocket通过；详细范围见[当前状态](USAGE.md)。 |
| 角色物品基础与C006发布 | [角色物品唯一计划](Plans/CharacterActionInventoryFoundationPlan.md)复核通过，首步6文件47离线候选未主合；角色特殊物品定义已明确。C006本次模型贴图发布completed，之后布料配置hash变化另未验；新长杖不是资产交付。 |
| 阴影与拖尾交接 | [阴影权威](Guides/DIRECTIONAL_TRIANGLE_SHADOWS.md)、[武器拖尾权威](Guides/WEAPON_BLADE_TRAIL.md) |
| Runtime AI Lab 启动优化 | [优化设计](RUNTIME_AI_LAB_STARTUP_OPTIMIZATION_DESIGN.md)：方案中的分配策略与二进制迁移仍为未实施目标；现场问题见其关联报告。 |

阴影修复、普通防御、选点发布及相关行为卡 / 工作流的并行改动属于活跃工作区快照；从 PROJECT_INDEX 命中的当前专项入口核实，暂缓搬动或据旧验收清理。

## 模块接口

| 文档 | 模块 |
|------|------|
| `Interfaces/Core.md` | Core 工具层 |
| `Interfaces/Events.md` | Events 事件总线 |
| `Interfaces/Attributes.md` | Attributes 属性存储 |
| `Interfaces/Buffs.md` | Buffs 生命周期 |
| `Interfaces/Modifiers.md` | Modifiers 修改器管线 |
| `Interfaces/Config.md` | Config 配置系统 |
| `Interfaces/Resources.md` | Resources 资源管理 |
| `Interfaces/Items.md` | Items 物品定义、Loot、world item、库存 runtime、获取事务、网络桥和装备投影桥 |
| `Interfaces/Audio.md` | Audio noEngine 播放契约、FMOD adapter、Runtime Catalog、Physical contact one-shot 和 Authoring 边界 |
| `Interfaces/Rendering.md` | Rendering URP 编排、Context、SharedRT、FeaturePipeline 和 Bridge 契约 |
| `Interfaces/Vfx.md` | VFX tick-driven intent / instance runtime、bridge 和 Unity presenter 契约 |
| `Interfaces/AI.md` | Runtime AI Planner 轻量规划 |
| `Interfaces/Diagnostics.md` | Diagnostics 调试接口 |
| `Interfaces/Runtime.md` | Runtime Host / 生命周期调度 |
| `Interfaces/AppFlow.md` | App / Scene Flow 状态和场景切换 |
| `Interfaces/Input.md` | Unity Input System 上层输入意图、上下文和重绑定接口 |
| `Interfaces/VoiceInput.md` | Runtime 语音输入事实、麦克风 provider 和后续 ASR provider 接入契约 |
| `Interfaces/Settings.md` | 用户偏好 profile、输入设置入口、音频 Bus 和画质设置接口 |
| `Interfaces/CharacterControl.md` | Character Control 角色控制编排接口 |
| `Interfaces/CharacterApplication.md` | Character Application 角色配置聚合、resolver 和 diagnostics 接口 |
| `Interfaces/CharacterAction.md` | Character Action 角色动作层接口 |
| `Interfaces/Combat.md` | Combat 命中、动作、物理和结算接口 |
| `Interfaces/Gameplay.md` | Gameplay 运行时行为核心 |
| `Interfaces/DebugUI.md` | Debug UI source registry、snapshot aggregation 和 Toolkit overlay |
| `Interfaces/Story*.md` | Story core / runtime / bridge / Unity / editor 契约 |
| `Interfaces/Editor.md` | Editor 工具接口 |

---

## 证据与历史

[Evidence](Evidence/)、[Validation](Validation/)、[UPM Evidence](UPM/Evidence/)、[UPM Inventories](UPM/Inventories/) 和 [Images](Images/) 保留各自证据及消费用途。Validation 中也有受测试引用的 schema；UPM manifest / inventory 有生成器固定输出路径，不能仅因目录名称或生成产物标签搬动、删除。

已完成任务的归档读取规则见 [Tasks/README](Tasks/README.md)，决策和迁移历史见下表；Tasks 中仍保留未完成规格与待承接的独有合同，按专项入口核实，不把整个目录等同于可清理历史。新的实现事实仍回到 Current / Guide / Interfaces、源码与测试，完成候选必须先核查合同覆盖和引用。

## 归档和决策

| 状态 | 文档 | 用途 |
|------|------|------|
| ADR | `Decisions/*.md` | 已接受的架构、流程和版本控制决策。 |
| ADR | `Decisions/ADR-006-fairygui-binder-generation.md` | FairyGUI Binder 生成的权威来源、代码拆分和 noEngine 校验决策。 |
| ADR | `Decisions/ADR-007-fairygui-semantic-ui-composer-authority.md` | 语义 UI Composer、FairyGUI XML、helper plugin、generated binding 和运行时预览之间的权威边界。 |
| ADR | `Decisions/ADR-007-audio-physical-expression-pipeline.md` | 物理音频 C#/FMOD 职责、Basis 身份、独立声学材质和确定性边界决策。 |
| ADR | `Decisions/ADR-009-platform-contract-authority.md` | Platform host facts、location、lifecycle、permission 与 UTC wall-clock 的权威边界。 |
| ADR | `Decisions/ADR-010-savegame-persistence-authority.md` | SaveGame v1 envelope、integrity、generation、三种原子性与候选恢复边界。 |
| ADR | `Decisions/ADR-011-application-startup-authority.md` | 外层启动编排、候选 Session、单状态原子发布、F+1 与 Boot/lifecycle authority。 |
| ADR | `Decisions/ADR-012-autonomous-verified-pr-merge.md` | 已撤销的 PR-only 自主合并历史决策。 |
| ADR | `Decisions/ADR-013-unified-main-fast-lane.md` | 已撤销的 main 快车道历史决策；当前仓库不定义特定交付工作流。 |
| ADR | `Decisions/ADR-014-character-action-execution-authority.md` | CharacterAction 唯一有状态 Gateway、生命周期/外部完成语义、CombatAction 子动作边界及命令/Replay v2 破坏式迁移。 |
| ADR | `Decisions/ADR-015-upm-embedded-package-topology.md` | 接受 Embedded Package 作为最终开发拓扑，规定先分类并迁出项目 surface、再独立切换 package root，且不自动拆 Git 仓库或多个 UPM 包。 |
| Archive | `Tasks/README.md` | 已完成任务文档的归档定位、读取规则和维护规则。 |
| 按文件状态与范围 | `Docs/Tasks/*.md` | 历史任务、未完成规格与尚待承接的独有合同混存；按专项入口核实，公共 API 和当前能力仍回到 Interfaces / Current、源码与测试。 |
| Archive | `MIGRATION.md` | 从 WGame 迁移了哪些代码、如何适配。 |

---

## 维护规则

- 当前事实只维护在本页“当前事实源”列出的 Current 文档、源码和测试中。
- 已完成 Task 按历史证据读取；未完成规格和待承接合同按专项路由读取。清理前核对现行合同、实际入口和引用，不凭目录、标题、mtime 或完成标签判定退役。
- 已完成任务文档不持续追更；任务产生的长期知识必须回写到对应 Current / Guide / Design / ADR 文档。
- 当 Design / Guide 与 Current 文档、源码或测试冲突时，以 Current 文档、源码和测试为准。
- Draft 文档必须在文件内标清状态；未接受前不能作为实现或验收依据。

---

## 版本历史（最近）

| 版本 | 日期 | 变更 |
|------|------|------|
| 0.7.56 | 2026-08-27 | 完成 UPM 全面审计修复：移除 project-only friend、公开最小 FairyGUI Editor 合约、以 host provider 注入项目路径、补齐 Magica Cloth O0 authority、抑制 VFX 自动发现误报并修复 Authoring/预构建测试稳定性；候选 `50036e76a` 的 Local/PinnedGit 八行以 base `18/18`、optional `19/19` 全部通过，版本化 P7 bundle 可独立校验 matrix/receipt report 引用，strict E0 仅余 3 项 legal gap。 |
| 0.7.55 | 2026-08-27 | 完成 UPM-P7 Consumer Evidence Gate：profile schema 4、matrix/receipt schema 1、optional O2/O0 权威、artifact/mutation/log fail-closed 与固定八行 runner 落地；候选 `595977713` 的 Local/PinnedGit 八行全部通过，matrix `promotable=true`，E3 candidate 保持 `releaseReady=false`；P8 因 owner 发布决策阻塞。 |
| 0.7.54 | 2026-08-27 | 完成 UPM-P6 Embedded Package Cutover：3,204/3,204 纯移动身份保持，开发工程切换到单一 embedded root，旧 locator fallback 与活动旧路径清零；候选 `c046158f5` 的 PinnedGit base `14/14`、三个 optional profile 各 `15/15` 通过，P6 Complete、P7 Ready。 |
| 0.7.53 | 2026-08-26 | 完成 UPM-P5 Editor Portability：候选 `b2a688ab7` 的 PinnedGit base `14/14`、三个 optional profile 各 `15/15`，resolved SHA/tree 与 cold import、Assets、ProjectSettings、package guards 闭合；P5 Complete、P6 Ready。 |
| 0.7.52 | 2026-08-26 | 落地 UPM-P5 本地候选：冻结 323 条 Editor source inventory，29 个 project-only 文件以 29/29 GUID/hash 保持迁出；修复 VFX absent、locator callers 和 reload no-write，新增两阶段 Consumer ProjectSettings/optional DLL gate。package contracts `5/5`、Repository `162/162 + 21/21`、Local base `14/14` 与三个 optional `15/15` 通过，等待 immutable candidate。 |
| 0.7.51 | 2026-08-26 | 完成 UPM-P4 Demo and Samples Split：候选 `7cd881755` 的 PinnedGit base `11/11` 与显式 RuntimeHost sample Import/Run `1/1` 通过，resolved SHA/package tree 和三项 mutation guard 闭合；P4 Complete、P5 Ready。 |
| 0.7.50 | 2026-08-26 | 落地 UPM-P4 本地候选：113 个 Demo 文件以 `113/113` GUID/hash 保持迁出 package，Unity 生成并登记唯一 RuntimeHost Minimal Lifecycle sample；strict E0、Repository `162/162 + 21/21`、Local base `11/11` 与显式 sample Import/Run `1/1` 通过，等待 immutable candidate gates。 |
| 0.7.49 | 2026-08-26 | 启动 UPM-P4 Demo and Samples Split：冻结 113 个 Demo 文件、110 个 C#、三个 asmdef 的 GUID/owner 清单，选择只依赖 `MxFramework.Runtime` 的 RuntimeHost Minimal Lifecycle 为唯一 sample；RuntimeVerticalSlice 因聚合依赖和项目资源继续作为 repository validation。本切片尚未移动资产。 |
| 0.7.48 | 2026-08-26 | 以 Repository 全绿修复候选 `f8958d7cb` 刷新 UPM-P3 immutable identity：远端完整 SHA 的 base `11/11`、三个 optional profile 各 `12/12`，四档 resolved SHA 与 package tree hash 一致；P3 保持 Complete，P4 保持 Ready。 |
| 0.7.47 | 2026-08-26 | 清零 UPM-P3 Repository 回归失败：Audio Player mirror 验证独立 Event 契约，FMOD smoke 区分受控句柄与合法 one-shot，Offline ResourceSceneFlow 使用可恢复的 prebuild/cleanup fixture；FairyGUI 保留真实 pending key，并在 Preview 前隔离 Stage/Package 污染而不销毁共享 Font。EditMode 最终 `162/162`，PlayMode `21/21`。 |
| 0.7.46 | 2026-08-26 | 完成 UPM-P3 Test Surface Split：10 个 repository test assemblies 迁出 package，保留 4 条 package contract，Test Framework 改由 Consumer/main project 显式拥有；clean Local 与同一远端完整 SHA 四档 PinnedGit matrix 通过，P3 Complete、P4 Ready。 |
| 0.7.45 | 2026-08-26 | 落地 UPM-P3 本地候选：冻结 10 个 test assemblies、31 个 C# 和 183 个测试属性的 owner/coverage，完成 repository tests 迁移、四条 package contract、Consumer testables 以及基线等价验证；保持 Validated，等待 immutable E2。 |
| 0.7.44 | 2026-08-26 | 完成 UPM-P2 CharacterTest Extraction：远端完整 SHA 的四档 PinnedGit E2 通过，P2 Complete、P3 Ready。 |
| 0.7.43 | 2026-08-26 | UPM-P2 本地迁移落地：按 1780 条 GUID/hash 清单把 CharacterTest Runtime、Editor、`MxFramework.Tests` aggregate 和 CharacterTest PlayMode 移到 `Assets/Scripts/WGameFrameworkProject/CharacterTest/`；Unity compile、strict E0、四档 Local Consumer 与 baseline parity 通过。P2 保持 In Progress，等待图形 screenshot、提交与 PinnedGit gates。 |
| 0.7.42 | 2026-08-26 | 启动 UPM-P2 CharacterTest Extraction：冻结 `Assets/Scripts/WGameFrameworkProject/CharacterTest/` project owner，确认 Runtime、Editor、CharacterTest PlayMode 与保持 identity 的 `MxFramework.Tests` aggregate 为移动闭包；P2 进入 In Progress，尚未执行资产移动。 |
| 0.7.41 | 2026-08-26 | 完成 UPM-P1 Surface Manifest：覆盖 133 个 asmdef 和 39 个 initializer，新增 owner dependency/optional gate/repository-validation 反向依赖门禁与 16 个 focused tests，strict E0 0 blocking/7 gaps，Unity 导入生成正式 `.meta`；P1 更新为 Complete，P2 更新为 Ready。 |
| 0.7.40 | 2026-08-26 | 复审并修正 UPM 阶段规格：刷新 Current E0 到 `7af102776`，为 P1 增加 owner 依赖语义门禁并把 Editor 无副作用修复归回 P5，要求 P4 先比较现有 RuntimeVerticalSlice 与 RuntimeHost sample，补充 P6 source-root folder `.meta` 退役特例。 |
| 0.7.39 | 2026-08-26 | 完成 LCC-M5 动画附肢软接触：Body Presence v2/geometry v7、Dragon14头/尾中/尾尖引用、selected Hurtbox same-tick软压力、Severed联动、v7 Network/M1 consumer兼容、正式多帧Playable/rollback和8龙全RuntimeModule 300+1800性能闭合；唯一终审最终P0/P1=0，M0～M5共36/36任务Verified。 |
| 0.7.38 | 2026-08-26 | 复核 Dragon14 头尾可无反馈穿过后扩展 LCC：保留 M0～M4 Verified 事实，新增 LCC-M5 动画附肢软接触里程碑和 T0～T3 四份独立执行文档；冻结 Animated Hurtbox stable reference、非硬阻挡、现有压力复用、Severed 同源、网络/性能/Playable Gate。本轮只补计划，不实现或派发。 |
| 0.7.37 | 2026-08-24 | 新增 CharacterTest 大型角色接触 LCC 总台账与 M0～M4 五个独立实施里程碑，冻结根硬阻挡、稳定 Body Presence、动画 Hurtbox 和动作 Trace 分层，以及伤口/破坏、切断拓扑、确定性地形姿态的 Authoring、网络、Playable、NoAlloc 和状态晋升门禁；当前仅完成计划落盘，全部阶段保持 Planned。 |
| 0.7.36 | 2026-08-23 | 为 UPM-P1 至 P8 新增 8 份独立 Current 任务规格，逐阶段固定进入条件、具体交付物、实施切片、非目标、不变量、验证、回滚、退出证据和下一阶段移交；本轮只落实文档，不启动阶段实现。 |
| 0.7.35 | 2026-08-23 | 新增 UPM 阶段任务台账，把后续实施固定为 P1 Surface Manifest、P2 CharacterTest、P3 Tests、P4 Demo/Samples、P5 Editor、P6 Embedded cutover、P7 Consumer evidence/optional matrix、P8 preview readiness；当前仅 P1 Ready，实施切片按阶段启动时再拆。 |
| 0.7.34 | 2026-08-23 | 重构 UPM 文档权威：新增唯一 Current 状态页，历史 Phase A-C 计划降级为 Archive，刷新 Consumer/Player/optional/release 事实，接受 Embedded Package 最终开发拓扑，并把 README、PROJECT_INDEX 与 Multi-project workflow 路由到新的 Current 入口。 |
| 0.7.33 | 2026-08-13 | 收口 P3.4 自由步骤 Authoring：新建入口拆为`空白步骤 / 从预设新建`，六个玩家意图预设不再暴露五个技术 Type ID；每步可在五类 typed 完成条件间安全切换并支持精确 Undo/Redo；缺失模块改为根关闭零步骤，未完成草稿在任意启停组合下阻止 prepare。正式 `characterTest.guidance` schema、当前六步 payload、golden hash 与 Scene cache 保持不变。 |
| 0.7.32 | 2026-08-13 | 收口 P3.4 教学文案本地化工作流：关卡作者只编辑简中源文案，`stableId` 派生的 Key 由系统维护；正式 TSV 持有简中源快照与 English 翻译，Editor 支持双语语义预览及缺失/过期诊断，Runtime 随 locale revision 原位重投影标题、动作、按键与导航提示。 |
| 0.7.31 | 2026-08-13 | 完成 P3.4 教学引导 Authoring 收口：拆出 Sequence Coordinator、五类 typed Observer 与 Snapshot Mapper，删除旧六步枚举/Mapper/回退；补齐未知 schema 只读保护、共享 envelope 与 JSON/cache 回滚、Play/build freshness preflight、正式六步 cache 消费、发布 FairyGUI package 和实际 Game 帧验收，并将实现状态校准为 Current。 |
| 0.7.30 | 2026-08-13 | 落地 P3.4 教学引导 Authoring 第一版：新增 schema/typed templates/compiler/hash、Level Editor 三栏编辑与语义预览、统一 envelope/cache/Scene 保存、正式六步 payload 与生产 Host cache 消费；保持项目私有和 fail-closed 边界，并明确独立 coordinator/observer/mapper 与完整 Playable 仍待收口。 |
| 0.7.29 | 2026-08-13 | 接受 Level Editor P3.4 教学引导可视化 Authoring 设计：新增 `characterTest.guidance` 单一事实源、五类 typed completion template、独立教学引导模块、Scene cache、Runtime sequence coordinator、统一保存事务和 P3.4a～P3.4e 开发指南；当前仅为 Design / Planned，不代表功能已经实现。 |
| 0.7.28 | 2026-08-10 | 完成 CharacterTest 设置面板按键页：以动态 `GList` 取代固定输入容量，完整投影 21 个默认 binding targets，为 `TargetLock` 提供键盘/鼠标双槽位，并补齐输入草稿的 Apply/Cancel 事务、Composer 发布与回归验证。 |
| 0.7.27 | 2026-08-10 | 校准 Player Guidance 与 CharacterTest 目标锁定文档：区分 Current decision 与带日期的实现证据快照，补充全链路源码入口、显式 binding index 0 / 非 active-device-aware 边界、本地化 key 覆盖缺口及复验触发条件，并把 Input 接口加入动作提示相关文档路由。 |
| 0.7.26 | 2026-07-31 | 创世验证场升级为 v0.4 Current：补齐并实现基础场景设计层，包括 ScenePlan、Zone / Route / Waypoint、Station Spatial Program、5 个模板、world-object owner-backed 三 Zone 脚手架、跨模块单 Undo、Validation Marker / Checkpoint、结构与解析后几何门禁、量化空间诊断 hash 和 SceneView overlay；官方 Scene 不自动修改，P2 Runtime 闭环仍未实现。 |
| 0.7.25 | 2026-07-31 | 创世验证场设计升级为 v0.3 并响应第二轮 Request changes：拆分必需 P1 Authoring descriptor 与可选 P2 executable adapter；v1 静态引用限制为 single static；精确定义 pre-commit / post-commit tail failure 和 `true -> false` participant 状态机；新增 tagged character startup correlation；登记 station-local point 与量化 resolved spatial dependency 两项 P2 门禁。当前等待重新评审，不自行宣告 P1 Ready。 |
| 0.7.24 | 2026-07-31 | 创世验证场设计升级为 v0.2 并响应 Request changes：静态样本采用 typed Body/Collider identity；拆分 Authoring validation、capability diagnostics 与 executable cache；收窄 Scene cache 失败保证；新增 Slice 外 session coordinator、根级 Groups、三维能力状态和 versioned canonical hash。当前等待重新评审，不自行宣告 P1 Ready。 |
| 0.7.23 | 2026-07-31 | 新增 CharacterTest 创世验证场框架设计 v0.1：复用现有 Mx Level Editor，建立验证站 Authoring、场景稳定引用、typed scenario、派生缓存、Runtime inspection、首批验证范围和分阶段门禁；当前仍为 Design only，不代表 Playable 或公共 API 完成。 |
| 0.7.22 | 2026-07-23 | 完成 Runtime AI Planner 行为卡与 Runtime AI Lab 本地闭环：每角色 6 槽/10 费卡组、Published Card/Draft/本地 Publish、行为卡编辑、RuntimeFrame/Facts/Replay 诊断与跨页恢复，并拆分稳定 Runtime Overlay、Timeline Projection 和大型测试夹具。生产账号、catalog 分发审批、云存档和产品多人接入仍不在本次范围。 |
| 0.7.21 | 2026-07-21 | 接受资源映射长期收敛方向：保留 Runtime ResourceManager 核心，将 Authoring-to-Build 层收敛为 `AuthoringStableId`、`ResourceKey`、`SourceLocator` 三个概念，以共享 Build Plan compiler 统一类型、package、Bundle、external 和 artifact provenance 语义，并采用兼容读取、规范写入、分阶段清理策略。 |
| 0.7.20 | 2026-07-20 | Gameplay Trigger Runtime 完成19.4B与父计划：新增protocol-2 bounded binary authority、authenticated transport metadata + ownership授权、Gameplay 100 -> Trigger 200 exact component snapshot、late join F+1、rollback/resync与partial hash；固定seed规模档120+480帧满足4/6/10 ms、memory和512 KiB snapshot预算。legacy protocol 1保持兼容；不含凭证/加密、matchmaking/Relay、durable persistence、CharacterTest protocol迁移或generic Unity action bridge。 |
| 0.7.19 | 2026-07-20 | Gameplay Trigger Runtime 完成19.4A：新增可打开的 `GameplayTriggerPlayable`，串联Combat occupancy、Trigger、Gameplay Buff/Story、SaveState与replay-compatible hash，提供键盘/HUD移动及Capture/Restore/Reset；capture仅为session内存slot，不是durable persistence，19.4B network/scale仍待交付。同时新增 UPM 外部消费与版本权威实施计划：明确当前 `package.json` 的历史 `1.0.0` 不代表稳定发布，冻结 preview/tag/release report 权威关系，定义独立最小 Consumer Smoke、包边界触发条件，以及 FMOD、FairyGUI、Unity Transport、VFX Graph 的可选依赖隔离阶段。 |
| 0.7.18 | 2026-07-20 | Gameplay Trigger Runtime 新增 #732 Unity Physics occupancy adapter：immutable compiled-contract catalog、fixed contact coordinator、持久 `PreSimulation/0` complete Exogenous batch flush、ReplayDisabled 与 Disable/Destroy/scene unload final-empty retry；Runtime gate 同步增加 hard-bounded caller-span atomic admission，不改变 Trigger hash/SaveState/replay schema。 |
| 0.7.17 | 2026-07-20 | Gameplay Trigger Runtime 完成 19.3C2：四种 `ReliableDeferred` action 通过 bounded trusted-derived Runtime command gate 投递 ComponentEntity Destroy、Attribute Set/Add 与 direct Ability Cast，并以 target-owned terminal result relay 回 Trigger `ActionResult`；checkpoint 继续只保存 authority payload，派生 request/result 在 suffix replay 中重建。Story、Unity/network adapter 与 Playable 未交付。 |
| 0.7.16 | 2026-07-20 | Gameplay Trigger Runtime 完成 19.3C1：Gameplay Buff request 增加 fixed reservation/correlation/terminal result seam，独立 noEngine bridge 支持 same-frame Apply/Remove 与 ActionResult relay，并验证 RemoveNoOp、target rejection、checkpoint-suffix derived regeneration 和 NoAlloc boundary；不改变 hash/SaveState/replay schema。 |
| 0.7.15 | 2026-07-16 | 完成游戏音频系统运行时与 FMOD 技术收口：补齐生命周期、混音、Bank manifest、真实 Bank smoke、产品音频和持续物理 Event，并记录人工试听、Physical adoption/evidence 与 Catalog 发布门禁。 |
| 0.8.8 | 2026-07-18 | Projectile Authoring M1-M7 基线收口：独立工作台成为 Definition / Request / Presentation 唯一 Unity writer；CharacterAction 迁移为只读 Request consumer 与 deep link；Generated/Player mirror 由 CLI 双根原子发布并可回滚；20 个 `PROJAUTH013` 明确保留为 v1 Presentation compatibility debt。 |
| 0.8.7 | 2026-07-18 | 新增 Projectile Authoring Editor 完整开发设计与目标效果图：固定 Definition / Request / Presentation 三层身份、独立工作台、生命周期 VFX v2、无损 Core/CLI 保存事务、真实 fixed-frame 预览、CharacterAction 只读引用迁移、特殊 consumer 覆盖矩阵和分阶段验收；所有目标能力保持 Design，尚未写成 Current。 |
| 0.8.6 | 2026-07-16 | VFX 语义属性阶段收口：Trails 增加结构化 Snapshot、原始字段展示、source hash/dieWithParticles 合同校验和 `PreserveNormalizedLifetime` ExtendTail 切片；确认 Unity Trail lifetime 为归一化比例，延长时修改主粒子 Lifetime 并保持 Trail lifetime/width/color 不变。聚焦 EditMode 83/83、Console 0 error。通用 Trail 属性、SubEmitter/Collision、Profile 策展与视觉 Golden 继续保持未完成边界。 |
| 0.8.5 | 2026-07-16 | VFX 语义属性完成文档/实现交叉审计：Registry 可写能力改为 Profile Binding、Adapter 与 UI 的交集；工作台新增逐属性能力矩阵、控件门禁、成员/Element 分级 Profile 确认和稳定缺口报告；当前 focused EditMode 81/81、Console 0 error。Profile Manifest 仍为空，高耦合模块、像素级 Golden 与 VFX Graph 继续如实标为未完成。 |
| 0.8.4 | 2026-07-15 | VFX 语义属性工作台完成 ParticleSystem v3 收口：95 项 Registry、完整 MinMaxCurve/Gradient、成员矩阵、Renderer/Material/Custom Data 首批 Adapter、UberFXSG Profile/派生材质、共享 Profile 批事务、Draft 恢复/三方合并、stale 重映射、Preview/Bake/源完整性 receipt、七类 Fixture、59/59 EditMode、2/2 PlayMode 和 4096 次稳态 0 B managed-allocation 证据；Trails/SubEmitter/Collision、其它 Shader、像素级 Golden 与 VFX Graph 保持 fail-closed。 |
| 0.8.3 | 2026-07-15 | 新增 VFX 语义属性管理与精确重定时完整开发设计及四张预期 UI 图；首个 Current 垂直切片落地 Source Snapshot/Component Profile、Property Registry、Composition Draft/Receipt v3、六种重定时核心、发射曲线/Burst、四页 Inspector、统一 Layer Scheduler、MaterialPropertyBlock 轨道和 v2 事务迁移，并以状态矩阵明确完整 Adapter、曲线编辑和验证证据仍是 Design。 |
| 0.8.2 | 2026-07-14 | Piloto VFX 升级为只读授权原材料库：`mx.vfx.source-library.v1` 按实际 AssetDatabase 内容深度索引 Prefab/ParticleSystem/支持资产，稳定组件 ID 使用 Prefab GUID + local fileID；增加 source search/extraction/composition CLI、通用 Unity 提取器/Recipe assembler、按需材质派生、来源证据 hash guard 与 UI Toolkit 三栏原材料工作台。Runtime 仍只加载烘焙项目 Prefab。 |
| 0.8.1 | 2026-07-14 | VFX 全量替换基线：37/37 semantic 与 0/37 Production 分离，现有资产统一标记 `LegacyPlaceholder`；新增 Piloto Studio ReferenceOnly 本机扫描/hash stale、占位与第三方依赖阻断、Development Agent 原创 Fireball Impact 六方向任务和工作台连接入口。 |
| 0.8.0 | 2026-07-13 | VFX Authoring 升级为 Catalog v2 与 Development Agent 双向创作飞轮：资源库/创作实验室、Provider/Style/认证模板/质量 Profile、受控 Candidate 生成、receipt/evaluation/promotion/registration plan、GUID/hash 事务晋升，以及 Fireball Charge/Flight/Impact 六方向 Draft。自动流程仍不能设置 Ready。 |
| 0.7.14 | 2026-07-16 | 接受 Gameplay Trigger Runtime 分阶段 ADR，交付独立 noEngine Phase 1 kernel、fixed-capacity reducer/policy/arbitration/reservation API、13 个 focused tests 和 Current interface；hash/save/replay/outbox/bridge/Playable 仍明确留在后续 Phase。 |
| 0.7.13 | 2026-07-16 | 新增 Gameplay Trigger Runtime Draft 与 Proposed ADR，定义 occupancy/scalar/signal source reducer、独立 Route、typed action intent、Gameplay pipeline 单 drain、next-frame 跨模块派发及 hash/replay/save/network/NoAlloc 边界；补充独立 reference model、canonical oracle、metamorphic/property、故障注入、Unity PlayMode 和逐 Phase 测试门禁；当前未实现。 |
| 0.7.12 | 2026-07-15 | 接受物理交互驱动音频 Phase 17 设计与 ADR，并同步 Physical Catalog/Resolver、公共材质 Pair/Variant、Workbench 覆盖与真实事实预览、受控 managed Basis/Cue/Event、CharacterTest one-shot bridge 和首批发布门禁。 |
| 0.7.11 | 2026-07-14 | 完成 FairyGUI Semantic UI Composer 的 Development Agent 文档同步：正式 player-facing UI 走语义资源、配方、蓝图、binding contract 和 helper 物化；UI Toolkit 保留 Debug/Showcase/Editor/Validation 职责；同步 Composer-owned/legacy package 边界与 Runtime AI Lab authoring-only 状态。 |
| 0.7.10 | 2026-07-13 | 新增 FairyGUI UI Composer v4 中文使用指南，覆盖资源/配方/节点一站式编辑、层级与容器、完整快捷键、恢复草稿、诊断修复、批量替换、多分辨率预览、收藏模板、命令面板、快照评审和发布排障。 |
| 0.7.9 | 2026-07-12 | VFX Resource Library 升级为 UI Toolkit 三栏工作台：语义分类、虚拟化缩略图卡片、隔离实时预览、Composition/Parameters/Dependencies/Review tabs，以及 authoritative CLI validation + expected-hash guard 的 Catalog-only 草稿写入。 |
| 0.7.8 | 2026-07-12 | VFX Authoring 补齐只读 Resource Library、64-bit dHash 感知相似候选，以及首个 package-template `.vfx` 技术样板；Graph 样板保留 ParticleSystem fallback 和 `ReviewNeeded` 人工美术审批边界。 |
| 0.7.7 | 2026-07-12 | Showcase 接入 Effect preview preset，切换条目时自动应用 target/path distance 与 camera scale；预览状态仍不进入 Runtime authority。 |
| 0.7.6 | 2026-07-12 | 补齐 Showcase 语义预览：显示完整有序 Recipe layer→Primitive 树，并按参数合同为 RuntimeSlot0..3 自动生成 slider；StaticBake/PreviewOnly 参数保持只读，不会误写 Runtime。 |
| 0.7.5 | 2026-07-12 | 补齐 Development Agent VFX 治理入口：`vfx search` 按 role/theme/Anchor/backend/review/text 排序并坚持 Effect→Recipe→Primitive 复用优先级，`vfx impact` 在替换/合并/删除前报告 Recipe、fallback、ResourceKey 和 Definition 影响面。 |
| 0.7.4 | 2026-07-12 | 启用 VFX 成熟治理：37/37 strict semantic coverage、`propose-coverage`、37 张可重建缩略图、preview hash、覆盖/Recipe/Primitive/重复候选指标及静态预算 gate；当前 0 validation/budget finding，自动迁移项仍保持 `ReviewNeeded`。 |
| 0.7.3 | 2026-07-12 | 增加可选 VFX Graph Unity adapter：独立 package-gated asmdef、cached Value0..3 Exposed Property 映射、pool `Stop/Reinit/Play`、compute/platform capability check 与 ParticleSystem fallback；Presenter 保持通用 backend 接口，当前尚无正式 `.vfx` 样板。 |
| 0.7.2 | 2026-07-12 | 落地 ParticleSystem Recipe baker v1 与首个真实复用样板：`fire_core` Primitive 经 large Recipe 将 `Fireball_Burst.prefab` 烘焙为共享 `Fireball.prefab` nested core + 1.15 scale，并生成可被 scanner 核验的 bake evidence；复跑计划为 `VFXBAKE005 UpToDate`。 |
| 0.7.1 | 2026-07-12 | VFX Authoring 增加稳定 Recipe bake input hash、Published Effect bake evidence、`VFXBAKE001`..`VFXBAKE005` dry-run planner 与 `vfx plan-bake`；计划明确区分需 Unity 写入和已同步，但尚不冒充实际 Prefab baker。 |
| 0.7.0 | 2026-07-12 | VFX scanner 增加稳定 Prefab 组件结构 fingerprint 和只读复用候选分组；当前 46 个 Prefab 形成 6 组结构轮廓候选，供 Development Agent 人工复核，不自动合并或删除资产。 |
| 0.6.99 | 2026-07-12 | `MxVfxShowcase` 只读消费 VFX Authoring Catalog：按 role 动态分组、语义搜索并展示 backend/Anchor/review、Recipe 摘要、预算和 fallback；未覆盖资源保留旧分类 fallback。 |
| 0.6.98 | 2026-07-12 | 补齐 VFX Authoring Phase 1 工具链：项目 evidence scanner、稳定 JSON/Markdown report、`vfx validate`、stale-hash guarded `vfx plan-save` 与 focused tests；当前扫描 37 个直接资源、5 个已覆盖、32 个进入迁移 warning 队列。 |
| 0.6.97 | 2026-07-12 | 落地 VFX Authoring Catalog 第一阶段基础：Authoring-only DTO、稳定 JSON、`VFXAUTH001`..`VFXAUTH012` validator、五个 CharacterTest/Samples 语义种子与 focused tests；scanner、Recipe bake、Showcase 和 Graph adapter 保持后续阶段。 |
| 0.6.96 | 2026-07-12 | 新增 VFX Asset Authoring Design，固定 Primitive/Recipe/Published Effect 复用模型、语义 Catalog、Development Agent 管理、Showcase 演进和可选 VFX Graph 第二阶段边界。 |
| 0.6.95 | 2026-07-11 | 武器配置升级到 Character Application 5.0 / Global Library 3.0：角色只拥有语义武器槽位与 Socket 姿态，单一武器拥有模型姿态和完整武器局部 Trace，组合仅保存槽位与武器选择；Compiler 与预览按固定变换顺序投影。 |
| 0.6.94 | 2026-07-10 | 将 CharacterTest Settings、Network 和 Ability Binding 三个玩家模态界面迁移到独立 Composer-owned FairyGUI 包；新增 6 个表单/分区/状态/图标语义资源、3 个紧凑按钮配方、受控文本输入参数和 generated binding contract，同时保留原有命令、Surface Policy、输入、鼠标及返回主菜单行为。 |
| 0.6.93 | 2026-07-10 | 新增语义化 CharacterTest Combat HUD：14 个正式 HUD 切片、178 节点蓝图与 generated binding contract 经 Composer/FairyGUI helper 发布到独立 `MxCharacterTestCombatHud` 包；运行时保留动态血条、架势、技能、Buff、锁定准星和非模态职责，并解除旧玩家面板对 Combat HUD 包常量的隐式耦合。 |
| 0.6.94 | 2026-07-11 | CharacterStudio 单一/组合武器进入全宽专注工作区；组合详情重排为四步导航与用途化行为卡；受击 Slot 按创作意图分组，并修复失效默认 Slot 导致详情空白及语义矩阵新建不预填的问题。 |
| 0.6.93 | 2026-07-10 | CharacterStudio 动画卡片改为结构化摘要并接入 Animation Editor Global Library 模式；受击改为列表、语义矩阵、Slot 详情和只读 JSON 审计工作台；Authoring 增加统一 revision 的单配置列表、校验、保存与受保护删除 API。 |
| 0.6.92 | 2026-07-10 | FairyGUI Semantic UI Composer 扩展为多屏 authoring，并以 10 个正式语义切片、3 个按钮配方和 generated binding contract 迁移 CharacterTest 主菜单到独立 `MxCharacterTestUi` 包；旧 `MxCharacterTestHud` 继续承载设置、网络、能力、HUD 与竞技场面板。 |
| 0.6.92 | 2026-07-10 | 精简 CharacterStudio 一级导航：动画/能力/受击共享配置收进组合武器工作台，解析结果、资源计划与 readiness 合并为“运行验证”二级入口。 |
| 0.6.91 | 2026-07-10 | CharacterStudio 组合武器配置升级为与单一武器一致的列表/详情与独立总览工作台，补齐稳定 ID 创建/复制、草稿比较、搜索筛选、生成诊断和私有动作深复制管理。 |
| 0.6.90 | 2026-07-10 | 武器配置基准升级到 4.0：Global Library 2.0 统一保存单一武器、动画、完整能力和受击配置；角色组合拥有私有动作，并支持共享快捷编辑、复制重绑及 3.0 → 4.0 双 revision 事务迁移。 |
| 0.6.89 | 2026-07-10 | CharacterStudio 能力页改为 compiler-backed 装备方案快照：按授予、槽位输入、包内 CharacterAction 和 Runtime 四段展示真实数据链，移除浏览器端 900xxx 推断，并补充 ability-chain API、包级绑定隔离和回归测试。 |
| 0.6.88 | 2026-07-10 | CharacterStudio 增加角色新建、复制、归档 UI；Authoring Server 补齐 duplicate/archive 事务、内置模板保护、活动目录移出和 Generated 产物保留规则，并增加后端与前端 smoke 回归覆盖。 |
| 0.6.87 | 2026-07-11 | CharacterTest 帧同步边界第一轮整改：schema 20 移除表现/诊断状态，拆分本地 authority checkpoint 与 presentation diagnostics，Runtime AI Lab A-B Loop 改为 authority-only 只读回放；统一 Character Resource Package 的 FBX/GLB 文档契约。 |
| 0.6.91 | 2026-07-10 | FairyGUI Semantic UI Composer v1 落地：以语义资源、组件配方和受约束蓝图生成全新 `MxUiFoundationDemo`，通过 hash 预检与 FairyGUI helper 完成 Apply/Sync/Publish，并新增真实发布包的 Unity 预览和按钮/输入/鼠标验证。 |
| 0.6.90 | 2026-07-10 | 新增 UI Art Resource Library 中文使用指南，覆盖 PNG 入库、语义/替换约束、预检保存、FairyGUI 接入状态与配方、人工导入发布、Development Agent 规则和排障；`USAGE.md` 同步为简短权威入口。 |
| 0.6.89 | 2026-07-10 | UI Art Resource Library 增加 FairyGUI 接入计划层：扫描 package.xml-backed 目标包，报告导入/内容/参数漂移与发布要求，并在外部编辑器中提供稳定 JSON/Markdown 配方的复制和下载；全程不写 FairyGUI XML 或发布产物。 |
| 0.6.88 | 2026-07-10 | UI Art Resource Library 编辑体验收口为固定三栏深色工作台：资源、预览、属性检查器同屏独立滚动；新增与 PNG 替换使用文件优先抽屉，并补充属性标签、元数据复制、折叠诊断和可拖拽 NineSlice 预览。 |
| 0.6.87 | 2026-07-10 | UI Art Resource Library 正式化收口：语义保存改为完整 Catalog 预检并锁定资源身份/路径，资源库根限制在 `ArtSource/UIKits`，新增资源开放 Alpha、尺寸、拉伸和 NineSlice 参数，补充服务版本握手、前端即时校验和新资源库写入回归测试。 |
| 0.6.86 | 2026-07-09 | 补充 CharacterTest Frame Sync Development Guide 的 CharacterAction Runner seek/rollback 恢复链路，并同步 Runtime AI Lab 08B.1 当前基线。 |
| 0.6.85 | 2026-07-09 | 新增 CharacterTest Frame Sync Development Guide，记录角色移动帧同步 authority 链路、表现层边界和 Runtime AI Lab 精准播放排查层级。 |
| 0.6.84 | 2026-07-10 | 修复 Items custom-state stack 合并、Inventory SaveState 原子恢复、默认 stack policy 校验和 rollback capture definition hash 分配边界，并补充 focused regression tests。 |
| 0.6.83 | 2026-07-09 | UI Art Resource Library 外部编辑器补齐新增资源 plan/create 闭环、结构化 FairyGUI 使用参数编辑、预检摘要和 `ArtSource`/`FGUIProject` 只读预览路由；新增资源只写资源库 PNG 与 catalog，不写 FairyGUI XML 或发布产物。 |
| 0.6.82 | 2026-07-09 | FairyGUI Art Slice 主编辑源切换为独立 `MxUiFoundation` UI 切图资源库：新增 1 个通用按钮测试资源、资源库 catalog/schema/API、EditorHub 默认入口和文档口径；旧 `MxCharacterTestHud` scanner 保留为使用点审计。 |
| 0.6.81 | 2026-07-09 | FairyGUI UI Art Slice Catalog 主编辑体验迁移到 EditorHub：Authoring Core/Server 统一 state、hash、plan、save 和 PNG replacement validation；Unity Editor 窗口降级为入口/debug fallback。 |
| 0.6.80 | 2026-07-09 | FairyGUI UI Art Slice Catalog Editor 增加 dry-run operation planner，保存 catalog、替换 PNG、登记 draft slice 前先报告影响范围、阻断项和发布提示；仍不实现 XML writer 或自动发布。 |
| 0.6.79 | 2026-07-09 | 抽出 FairyGUI UI Art Slice Catalog Editor service，集中 catalog load/save、usage 扫描、report 调用和 PNG replacement 校验，为后续完整编辑器窗口和受控 writer 打基础。 |
| 0.6.78 | 2026-07-09 | 为 FairyGUI UI Art Slice Catalog 增加 Phase 2 最小 Editor 入口：浏览 `MxCharacterTestHud` 切图、预览 PNG、编辑 catalog 语义/替换约束、调用 report，并支持受约束同路径 PNG 替换；XML writer 和自动发布仍未实现。 |
| 0.6.77 | 2026-07-09 | 将 FairyGUI UI Art Slice Catalog 从 Draft 晋升为 Guide：Phase 1 scanner/catalog/report 和 review summary 作为当前工具链事实源；Editor 替换流、PNG 替换和 XML writer 仍标记为 planned。 |
| 0.6.76 | 2026-07-08 | 新增 FairyGUI UI Art Slice Catalog Draft，明确切图语义 sidecar、scanner 范围、diagnostics/report 稳定性、Phase 1/2/4 边界和 `MxCharacterTestHud` 样板包验收口径。 |
| 0.6.75 | 2026-07-08 | 同步 Items lifecycle / Loot / WorldItem / Network 能力和 Item Lifecycle Showcase Guide；同步 CharacterTest player-facing FairyGUI Surface Policy：大型玩家面板互斥、StoryDialog suppress、`InputContext.UI` / cursor scope / Cancel 行为和 v1 边界。 |
| 0.6.74 | 2026-07-02 | 将 CharacterTest Runtime AI Planner 行为组合、Behavior Loadout、玩家可见策略和 Ability / CharacterAction 链路预览拆分为独立需求文档。 |
| 0.6.71 | 2026-06-28 | 新增 CharacterTest Runtime AI Planner Editor Development Guide，记录编辑界面结构、代码职责拆分、交互原则、流程图预览、Play Monitor 和验证入口。 |
| 0.6.70 | 2026-06-27 | 新增 CharacterTest Runtime AI Planner Development Guide，记录 JSON profile、HFSM + Utility、Editor 调试、感知数据准备层和后续扩展边界。 |
| 0.6.69 | 2026-06-25 | 新增 CharacterTest 技能实现类型资料库 Guide，按功能类型集中记录技能运行链路、模块权威、数据来源、表现接入和 CharacterTest 特例边界。 |
| 0.6.68 | 2026-06-19 | 新增 CharacterTest 性能优化计划 Guide，记录 Profiler Analyzer 基线、异常尖峰排除口径和 Runtime / Motion / Spawn / FairyGUI 分阶段优化方向。 |
| 0.6.67 | 2026-06-18 | 强化 C# 代码改动的 `API_STANDARDS.md` / `QUALITY_GATE.md` 必读入口，并新增 first-party 代码性能规范扫描的 Harness 入口。 |
| 0.6.66 | 2026-06-18 | 同步 Unity `6000.5.0f1` / URP `17.5.0` 项目基线，更新 Rendering pipeline 文档和 URP RenderGraph 执行路径说明。 |
| 0.6.65 | 2026-06-15 | 新增 FairyGUI Binder 生成 ADR 和 Guide，明确 generated manifest/binder、手写 partial、Host 职责和 noEngine 校验入口。 |
| 0.6.64 | 2026-05-30 | 新增 `Docs/Guides/CHARACTER_RUNTIME_MODULE_RELATIONSHIP.md`，作为角色运行时相关模块依赖和组合关系速查。 |
| 0.6.63 | 2026-05-27 | 新增 `Tools/Harness/check_docs_health.py` 并接入轻量 Harness，检查文档 `Status`、入口文件一致性和旧代码索引词回流。 |
| 0.6.62 | 2026-05-27 | 给核心 Current / Guide / Design / Draft / Archive 文档补显式 `Status` 头，并把 `ROADMAP.md`、迁移和 WGame 审计类文档降为规划或归档视角。 |
| 0.6.61 | 2026-05-27 | 当时为 `PROJECT_INDEX.md` 增加默认读取边界和按任务追加文档；该默认分包机制已于 2026-08-08 撤销。 |
| 0.6.60 | 2026-05-27 | 瘦身 `README.md`：移除长任务清单和长版本表，新增文档状态分层，明确 Current / Guide / Design / ADR / Archive / Draft 的使用规则。 |
| 0.6.59 | 2026-05-27 | 降级 `Docs/Tasks/` 为历史任务归档：新增 `Docs/Tasks/README.md`，明确已完成任务文档不属于当前事实。 |

旧版本历史保留在 Git 历史中；当前入口只维护最近变更。
