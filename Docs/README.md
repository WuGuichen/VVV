# MxFramework 文档索引

> 版本 0.6.53 | 2026-05-24
>
> 本目录定义框架的长期设计、接口边界、开发流程和验收标准。

---

## 快速入口

| 你需要 | 读这个 |
|--------|--------|
| **想知道框架现在能做什么** | → `CAPABILITIES.md`（按使用场景分类的功能清单） |
| **开始接入：最小代码示例** | → `USAGE.md`（属性/Buff/Modifier/配置 代码模板） |
| **让 Agent 读取最小上下文** | → `PROJECT_INDEX.md`（Context Pack 入口和禁止默认读取范围） |
| **排查运行时状态和调试面板** | → `Guides/OBSERVABILITY_DEBUGGING_GUIDE.md`（Debug UI、日志、timeline、hot reload、commands 和 Simulation Harness） |
| **让 agent 基于框架制作小游戏 / Demo** | → `AGENT_GAME_CREATION_GUIDE.md`（API 复用计划、标准分层、禁用项和验收清单） |
| **选读哪个文档** | → 本文件下方「职责表」 |

---

## 核心文档（必读）

| 文档 | 回答的问题 |
|------|------------|
| `PROJECT_INDEX.md` | Agent 应该按什么顺序读取项目上下文？ |
| `CAPABILITIES.md` | 框架当前能做什么？按场景分类。 |
| `USAGE.md` | 如何接入和组合模块？最小代码示例。 |
| `INTERFACES.md` | 接口索引、依赖矩阵、模块边界。 |
| `ARCHITECTURE.md` | 模块如何协作、依赖规则、运行时生命周期。 |
| `DESIGN.md` | 框架为什么存在、包含什么、不包含什么。 |
| `RUNTIME_FOUNDATION_SYSTEM.md` | Runtime Host、Frame/Command/Replay、SaveState 的运行时底座规划。 |
| `RESOURCE_MANAGEMENT_SYSTEM.md` | 资源管理系统设计、模块边界、加载契约和阶段切片。 |
| `RESOURCE_DIRECTORY_LAYOUT.md` | 资源正式目录、命名、Catalog、临时资产归档和 FMOD 边界规范。 |
| `RENDERING_PIPELINE.md` | 当前 Unity 渲染管线基线、URP 资产位置、材质/场景创作和验证规则。 |
| `COMBAT_ANIMATION_PHYSICS.md` | 动作战斗确定性动画/物理协作方案和落地阶段。 |
| `CHARACTER_RESOURCE_PACKAGE_AUTHORING.md` | 角色资源包、外部 3D 装配编辑器、Unity 导入和 Runtime Spawn 总方案。 |
| `CHARACTER_RESOURCE_PACKAGE_IMPLEMENTATION_PLAN.md` | 角色资源包工程实现方案：模块拆分、目录、CLI、Tauri 编辑器、Unity Importer 和测试矩阵。 |
| `API_STANDARDS.md` | API 命名、兼容性、GC、Unity 依赖标准。 |
| `WORKFLOW.md` | 项目日常开发、验收、提交和推送流程。 |
| `GITNEXUS.md` | GitNexus 接入、影响面分析和提交前辅助检查。 |
| `../Tools/GiteaGithubSync/README.md` | Gitea Issue / PR 元数据手动镜像到 GitHub 的脚本说明。 |
| `Decisions/` | ADR 决策记录，保存架构、流程和版本控制等正式决策。 |
| `Progress/CurrentStatus.md` | 当前项目流程、主仓库和下一步运营状态。 |
| `AGENT_GAME_CREATION_GUIDE.md` | Agent 基于框架制作小游戏 / Demo / Runtime Showcase 的执行规范。 |
| `QUALITY_GATE.md` | 什么算做完、如何验收。 |
| `ROADMAP.md` | 分阶段建设路线和完成定义（按 Phase 组织）。 |
| `MIGRATION.md` | 从 WGame 迁移了哪些代码、怎么改的。 |
| `Guides/OBSERVABILITY_DEBUGGING_GUIDE.md` | 如何接入和排查 Debug UI、日志、timeline、performance counters、Simulation Harness、hot reload、input adapter 和 command gate。 |

---

## 编辑器 / 外部工具

| 文档 | 回答的问题 |
|------|------------|
| `EDITORS.md` | Unity Editor 工具规范。 |
| `../Tools/MxFramework.EditorHub/README.md` | 外部编辑器中心：一键启动 Authoring server，并集中打开 Buff Authoring Editor、CharacterStudio 和全局资源管理器入口。 |
| `AUTHORING_EDITOR_PROGRAM.md` | 外部主创编辑器总规划。 |
| `AUTHORING_EDITOR_USAGE.md` | Buff 外部编辑器怎么开、怎么用、限制在哪。 |
| `AUTHORING_WORKFLOW.md` | 创作流程跨 Unity/Mod Editor/AI/CLI 协作。 |
| `CHARACTER_RESOURCE_PACKAGE_AUTHORING.md` | 角色资源包创作管线：包格式、3D 装配、Compiler、Unity Importer、Runtime Spawn。 |
| `CHARACTER_RESOURCE_PACKAGE_IMPLEMENTATION_PLAN.md` | 角色资源包创作管线的工程落地方案。 |
| `Tasks/CHARACTER_RESOURCE_LIBRARY_00_DESIGN.md` | 全局 Authoring Resource Manager 设计：跨编辑器资源发现/选择、provider catalog、Authoring Compiler 资源计划和运行时资源编排。 |
| `Tasks/CHARACTER_RESOURCE_LIBRARY_EDITOR_01_MVP.md` | 全局 Resource Manager Editor MVP：工具入口、provider 资源列表、inspect API、详情页、引用/诊断只读和 Hub 集成验收。 |
| `Tasks/ANIMATION_EDITOR_00_DESIGN.md` | 独立 Animation Editor 设计：AnimationGroup、Clip mapping、1D/2D Blend、Timeline Events、Bake/Compatibility 和 CharacterStudio 迁移边界。 |
| `Tasks/ANIMATION_EDITOR_01_NATIVE_CLIP_UNITY_CONSUMPTION.md` | Animation Editor 下一阶段：Unity 原生 AnimationClip / model sub-clip 资源选择、权威预览、Compiler warmup 和 Unity runtime 消费闭环。 |
| `WGAME_BUFF_AUTHORING_WORKFLOW.md` | 结合 WGame 类型的 Buff 创作流水线。 |

---

## 模块接口

| 文档 | 对应模块 |
|------|----------|
| `Interfaces/Core.md` | Core 工具层 |
| `Interfaces/Events.md` | Events 事件总线 |
| `Interfaces/Attributes.md` | Attributes 属性存储 |
| `Interfaces/Buffs.md` | Buffs 生命周期 |
| `Interfaces/Modifiers.md` | Modifiers 修改器管线 |
| `Interfaces/Config.md` | Config 配置系统 |
| `Interfaces/Resources.md` | Resources 资源管理 |
| `Interfaces/AI.md` | AI 轻量规划 |
| `Interfaces/Diagnostics.md` | Diagnostics 调试接口 |
| `Interfaces/Runtime.md` | Runtime Host / 生命周期调度 |
| `Interfaces/AppFlow.md` | App / Scene Flow 状态和场景切换 |
| `Interfaces/Input.md` | Unity Input System 上层的输入意图、上下文和重绑定接口 |
| `Interfaces/CharacterControl.md` | Character Control 角色控制编排接口 |
| `Interfaces/CharacterApplication.md` | Character Application 角色配置聚合、纯 resolver 和 diagnostics 接口 |
| `Interfaces/DebugUI.md` | Debug UI source registry、snapshot aggregation、Toolkit overlay 和 source adapter 接入方式 |
| `Interfaces/Gameplay.md` | Gameplay 运行时行为核心 |
| `Interfaces/Story.md` | Story core S0 proposed contract（未实现） |
| `Interfaces/Story.Runtime.md` | Story Runtime S0 proposed contract（未实现） |
| `Interfaces/Story.GameplayBridge.md` | Story 与 Gameplay bridge S0 proposed contract（未实现） |
| `Interfaces/Editor.md` | Editor 工具接口 |

---

## 数据审计（框架设计依据）

| 文档 | 覆盖内容 |
|------|----------|
| `WGAME_DATA_AUDIT.md` | WGame 数据源规模、职责、证据 |
| `WGAME_DATA_RELATION_AUDIT.md` | 跨源引用关系 |
| `WGAME_SPLIT_GRAPH_AUDIT.md` | Split JSON 字段位序 |
| `WGAME_TABLE_FIELD_INDEX.md` | Luban/BaseData 表字段 |
| `WGAME_ENUM_MAPPING_AUDIT.md` | 枚举/Flags 映射 |
| `CONFIG_FORMAT_STRATEGY.md` | 配置格式策略与 Phase 9 契约输入 |
| `Interfaces/ConfigSchemaSeeds.md` | Phase 9 首批 Schema 种子清单 |
| `Interfaces/ConfigReferenceRulesPhase9.md` | Phase 9 引用规则白名单 |
| `Tasks/PHASE9_CLOSEOUT_REPORT.md` | Phase 9 closeout 接受记录、剩余风险和 AIAction 试点建议 |

---

## Demo / Showcase 文档

| 文档 | 覆盖内容 |
|------|----------|
| `Demo/CONFIG_DEMO.md` | Config Demo 内置源、字段示例和引用验证说明。 |
| `Demo/MX_ANIMATION_SYSTEM_SHOWCASE.md` | MxAnimation System Showcase 入口、覆盖能力、手测流程、验收清单和自动验证入口。 |

---

## 当前 P0 Goal

| Goal | 说明 |
|------|------|
| `Tasks/PHASE13_OBSERVABILITY_AND_DEVELOPER_WORKFLOW.md` | Phase 13 Observability and Developer Workflow：Issue #178-#187，已覆盖只读 Debug UI core registry、UI Toolkit overlay shell、RuntimeHost / Logging / Resources / Gameplay / Combat source adapters、event timeline、entity watch、performance counters、Simulation Harness batch reports、Config Runtime patch hot reload、Debug UI input adapter、command gate 和调试指南；明确 Debug UI 状态不进入 Replay / SaveState / Runtime hash。 |
| `Tasks/RUNTIME_DEBUG_UI_FRAMEWORK_01_DESIGN.md` | Runtime Debug UI Framework 01：Issue #85 通用运行时调试界面框架设计，规划 source registry、snapshot aggregation、Hidden / Collapsed / Expanded UX、DebugUI noEngine core / Toolkit / Input 拆分和 M1-M5 实施切片。 |
| `Tasks/MARBLE_MAZE_UNITY_PHYSICS_SHOWCASE_01.md` | Marble Maze Framework Physics Showcase：已重构为框架物理权威，RuntimeHost 负责命令、计时、checkpoint、诊断 hash、Replay 和 SaveState JSON；Unity 场景对象只作为 view / input adapter。 |
| `Tasks/BREAKOUT_RUNTIME_SHOWCASE_01.md` | Breakout Runtime Validation：用打砖块验证连续运动/AABB 碰撞、关卡/砖块类型/多球/道具、预发球滚动/发射方向、AppFlow/SceneFlow、RuntimeCommand、Replay hash、SaveState。当前仓库未提交 Breakout 场景资产，因此不标记为 Playable。 |
| `Tasks/TETRIS_RUNTIME_VALIDATION_01.md` | Tetris Runtime Validation：用纯 C# 经典小游戏验证 RuntimeHost、CommandBuffer、Replay playback hash、SaveState JSON roundtrip，并提供 `Assets/Scenes/TetrisRuntimeValidation.unity` 手动试玩入口。 |
| `Tasks/APP_SCENE_FLOW_01_FOUNDATION.md` | App / Scene Flow Foundation：新增 App 状态流转、SceneFlow 串行加载、Unity SceneManager 适配和 RuntimeHost 模块入口。 |
| `Tasks/PHASE11_RUNTIME_GAMEPLAY_GOAL.md` | Runtime Gameplay Foundation 已 Accepted / Closed；M1-M5 公共 API、配置驱动、诊断快照、配置变更和 Ability authoring contract 已收口。 |
| `Tasks/GAMEPLAY_FOUNDATION_02_RUNTIME_LOOP_AND_COMMANDS.md` | Gameplay Foundation 02：新增 `GameplayRuntimeModule`，将 Gameplay 接入 RuntimeHost / RuntimeCommandBuffer / RuntimeEventQueue 主线。 |
| `Tasks/GAMEPLAY_ECS_STYLE_00_DESIGN_CONTRACT.md` | Gameplay ECS-style 00：定义组件化状态、系统化逻辑、generation entity id、v0 API bridge、source of truth 和 command/system/event 边界。 |
| `Tasks/GAMEPLAY_ECS_STYLE_01_COMPONENT_STORE.md` | Gameplay ECS-style 01：新增 generation entity id、entity lifecycle、component marker 和稳定 component store。 |
| `Tasks/GAMEPLAY_ECS_STYLE_02_SYSTEM_PIPELINE.md` | Gameplay ECS-style 02：新增 system phase/context/pipeline，并保持 RuntimeCommandBuffer 由 GameplayRuntimeModule 单点 drain。 |
| `Tasks/GAMEPLAY_ECS_STYLE_03_V0_API_BRIDGE.md` | Gameplay ECS-style 03：新增 component registry，统一 entity destroy 时 registered component store cleanup，作为 v0 API bridge 的最小边界。 |
| `Tasks/GAMEPLAY_ECS_STYLE_04_CORE_COMPONENTS.md` | Gameplay ECS-style 04：新增 Identity、Team、Lifecycle、Tag、Status 纯数据组件和 registry GetOrCreateStore 入口。 |
| `Tasks/GAMEPLAY_ECS_STYLE_05_COMPONENT_QUERY.md` | Gameplay ECS-style 05：新增稳定 component query helper，支持单组件查询和双组件 join，避免 system 直接依赖 store 内部结构。 |
| `Tasks/GAMEPLAY_ECS_STYLE_06_COMPONENT_WORLD.md` | Gameplay ECS-style 06：新增 GameplayComponentWorld 组合根，聚合 component registry 和 runtime event queue，并接入 GameplaySystemContext。 |
| `Tasks/GAMEPLAY_ECS_STYLE_07_ENTITY_COMMANDS.md` | Gameplay ECS-style 07：新增 component runtime create/destroy entity commands，使用 generation id 防 stale destroy，并输出 component entity runtime events。 |
| `Tasks/GAMEPLAY_ECS_STYLE_08_COMPONENT_WORLD_DIAGNOSTICS.md` | Gameplay ECS-style 08：新增 component world diagnostics snapshot，稳定输出 alive entities、store 摘要和 pending event queue 概要。 |
| `Tasks/GAMEPLAY_ECS_STYLE_09_COMPONENT_SCHEMA_CONTRACT.md` | Gameplay ECS-style 09：定义 component value schema 契约，明确 diagnostics、hash 和 SaveState adapter 边界，避免泛型 store 直接序列化。 |
| `Tasks/GAMEPLAY_ECS_STYLE_10_COMPONENT_SCHEMA_REGISTRY.md` | Gameplay ECS-style 10：实现 component schema registry、capability adapter lookup、core diagnostics descriptors 和 ComponentWorld schema 入口。 |
| `Tasks/GAMEPLAY_ECS_STYLE_11_COMPONENT_RUNTIME_HASH.md` | Gameplay ECS-style 11：实现 ComponentWorld runtime hash contributor、core component hash writers 和 schema-backed hash ordering tests。 |
| `Tasks/GAMEPLAY_ECS_STYLE_12_COMPONENT_SAVE_STATE.md` | Gameplay ECS-style 12：实现 ComponentWorld SaveState provider/restorer、core component save adapters 和 JSON roundtrip hash 恢复测试。 |
| `Tasks/GAMEPLAY_ECS_STYLE_13_COMPONENT_STATE_SYSTEMS.md` | Gameplay ECS-style 13：实现 lifecycle cleanup component state system，验证 Resolution phase、event、hash 和 SaveState restore 闭环。 |
| `Tasks/GAMEPLAY_ECS_STYLE_14_COMPONENT_SPAWN_DEFINITIONS.md` | Gameplay ECS-style 14：实现 component spawn definitions、spawn registry 和 SpawnComponentEntity command system。 |
| `Tasks/GAMEPLAY_ECS_STYLE_15_COMPONENT_ATTRIBUTE_RUNTIME.md` | Gameplay ECS-style 15：实现 component-native attribute set、attribute commands、attribute changed event 和 schema-backed hash / SaveState。 |
| `Tasks/GAMEPLAY_COMPONENT_RUNTIME_SHOWCASE_01.md` | Gameplay Component Runtime Showcase 01：把 v0 component runtime 收口能力组合成可观察 Runtime Slice，提供 Unity runner、UI Toolkit 视图和 Editor 场景生成菜单。 |
| `Tasks/GAMEPLAY_COMPONENT_PLAYABLE_COMBAT_BRIDGE_PLAN_01.md` | Gameplay Component Playable + Combat Bridge Plan：规划将 Gameplay Component Runtime Showcase 升级为提交的 Unity Playable 入口，并固定 component-native Combat bridge 的 source-of-truth 边界和后续实施切片。 |
| `Tasks/CHARACTER_CONTROL_RUNTIME_00_DESIGN_CONTRACT.md` | Character Control Runtime 00：固定 noEngine 角色控制编排边界，并实现 command DTO、控制状态机、Combat Motion resolver 和 Combat / Gameplay action bridge 首批切片。 |
| Gitea #195 / #196 / #201 / #197 / #198 / #199 | Character Control command sources, motion modifiers, pressure reaction, animation presentation and diagnostics：新增 Local Input adapter、Runtime AI Planner command source、motion modifier / traction provider contract、pressure reaction bridge、MxAnimation presentation adapter 和 Debug UI source。 |
| `Tasks/GAMEPLAY_COMPONENT_BUFF_MODIFIER_01.md` | Gameplay Component Buff / Modifier 01：新增 component-native buff / additive modifier state、cleanup system、diagnostics、hash 和 SaveState。 |
| `Tasks/GAMEPLAY_ABILITY_03_COMMAND_SYSTEM.md` | Gameplay Ability 03：将 CastAbility / DespawnEntity 迁入 command systems，让 GameplayRuntimeModule 只负责 drain、pipeline、event queue 和 world tick。 |
| `Tasks/GAMEPLAY_ABILITY_04_COMMAND_HANDLED_STATE.md` | Gameplay Ability 04：新增 command handled 状态，让 unsupported system 基于 handled 判断，支持 default pipeline 上扩展自定义 command system。 |
| `Tasks/RUNTIME_FOUNDATION_01_RUNTIME_HOST.md` | Runtime Foundation P0-1：规划 Runtime Host / Composition Root，统一模块注册、生命周期、Tick 顺序和诊断入口。 |
| `Tasks/RUNTIME_FOUNDATION_02_FRAME_COMMAND_REPLAY.md` | Runtime Foundation P0-2：规划 Frame Clock、Command Buffer、Replay record/playback 和 result hash。 |
| `Tasks/RUNTIME_FOUNDATION_03_SAVE_STATE_SERIALIZATION.md` | Runtime Foundation P0-3：规划 SaveState 契约、版本迁移、Gameplay slice 保存恢复和结构化错误。 |
| `Tasks/CORE_RUNTIME_UTILITIES_01.md` | Core Runtime Utilities P0：统一规划 StableHandleTable、Pooling、RingBuffer、TimerScheduler 和 DeterministicRandom 这批高频 noEngine 小工具。 |
| `Tasks/CORE_HANDLES_01_STABLE_HANDLE_TABLE.md` | Core Handles P0：规划 generation-based stable handle table，防止 Timer、Audio、UI、Operation 等 stale handle 命中新对象。 |
| `Tasks/CORE_COLLECTIONS_01_RING_BUFFER.md` | Core Collections P0：规划固定容量 `RingBuffer<T>`，统一 recent events / errors / commands / diagnostics 缓冲。 |
| `Tasks/RUNTIME_FOUNDATION_04_TIMER_SCHEDULER.md` | Runtime Foundation P0-4：规划 noEngine Runtime Timer Scheduler，支持 frame / seconds delay、repeating、cancel、snapshot、SaveState 和 RuntimeCommand 调度模式。 |
| `Tasks/CORE_POOLING_01_OBJECT_REFERENCE_POOL.md` | Core Pooling P0：规划 `ObjectPool<T>`、`IReference`、`ReferencePool<T>` 和可选 collection pool，沉淀 `ModifierContext` 这类局部池模式。 |
| `Tasks/RUNTIME_RANDOM_01_DETERMINISTIC_RANDOM.md` | Runtime Random P0：规划 noEngine deterministic random，支持 seed、state capture / restore、Replay 和 SaveState。 |
| `Tasks/RUNTIME_EVENTS_01_EVENT_QUEUE.md` | Runtime Events P0：规划按帧缓冲、稳定 drain、可诊断和可保存的 `RuntimeEventQueue<T>`，补足同步 EventBus 之外的事件流。 |
| `Tasks/RUNTIME_QUALITY_UTILITIES_01.md` | Runtime Quality Utilities P1：规划 CooldownTracker、DirtyFlag、VersionedValue、RuntimeOperation、RateLimiter、Debouncer 和 CommandRegistry。 |
| `Tasks/VIEW_AUTHORING_UTILITIES_01.md` | View / Authoring Utilities P2：规划局部 StateMachine、typed ContextMap、View Tween / Interpolator 和 SnapshotDiff / ChangeSet。 |
| `Tasks/RUNTIME_FOUNDATION_05_TIMER_POOL_INTEGRATION.md` | Runtime Foundation P2：规划 Timer / Pool 的真实落点接入，用 Ability、Demo、UI、Combat、SceneFlow、Resources 或 `ModifierContext` 验证工具层。 |
| `Tasks/AUTHORING_CONTRACT_ABILITY_01.md` | Phase 11 M5 已完成；固定 Ability authoring contract、校验错误码、AI context 和 `BasicAbilityConfig` 映射。 |
| `Tasks/PHASE12_UI_TOOLKIT_SHOWCASE_GOAL.md` | M1-M5 已验收，下一步 M6 从 Showcase 沉淀可复用 UI Toolkit 控件和主题 token。 |
| `Tasks/COMBAT_ANIMATION_PHYSICS_EPIC.md` | Combat 确定性动画/物理主线；Motion v0、Physics Debug、World Lifecycle、Motion v1 Capsule Proxy 已验收。 |
| `Tasks/COMBAT_MOTION_V1_CAPSULE_CHARACTER_PROXY.md` | Combat Motion v1 capsule proxy / narrow phase 已验收；角色移动碰撞使用 deterministic capsule proxy，并保持攻击 query 语义不变。 |
| `Tasks/COMBAT_PHYSICS_HIT_QUERY_DEBUG_VISUALIZATION.md` | 命中查询 explain / debug 数据链路、Play Mode hit / miss 摘要和可视化节点已验收。 |
| `Tasks/COMBAT_PHYSICS_RUNTIME_WORLD_LIFECYCLE.md` | `CombatPhysicsWorld` lifecycle API、revision / stats、mutation、copy 和 query regression 已验收。 |
| `Tasks/COMBAT_PHYSICS_M11D_3_OBB_QUERY_V0.md` | Combat Physics 下一阶段：补齐 OBB query v0，用于矩形攻击盒 / 武器盒；本阶段只做 OBB query vs AABB collider，不扩展 OBB collider 或角色运动。 |
| `Tasks/RESOURCE_MANAGEMENT_M1_CONTRACT_MEMORY_PROVIDER.md` | Resource Management M1 已实现 noEngine 契约、Memory Provider、Handle、Catalog 合并和基础测试。 |
| `Tasks/RESOURCE_MANAGEMENT_M2_UNITY_DEMO_PROVIDER.md` | Resource Management M2 已实现 Unity Resources Provider、TextAsset Demo 和 Resources 测试分组。 |
| `Tasks/RESOURCE_MANAGEMENT_M3_ASSETBUNDLE_PROVIDER_CATALOG_FILE.md` | Resource Management M3 已实现 StreamingAssets Catalog loader、本地 AssetBundle Provider、依赖 bundle ref-count 和安全卸载测试。 |
| `Tasks/RESOURCE_MANAGEMENT_M4_MOD_PACKAGE_RESOURCE_CATALOG.md` | Resource Management M4 已实现 Mod Package 可选 resourceCatalog、路径安全校验、Catalog 挂载和覆盖路由测试。 |
| `Tasks/RESOURCE_MANAGEMENT_M5_DIAGNOSTICS_EDITOR_VALIDATION.md` | Resource Management M5 已实现 ResourceDebugSource、Catalog 结构校验和 Editor 资产存在性/类型校验。 |
| `Tasks/RESOURCE_MANAGEMENT_M6_0_CATALOG_SCHEMA_PREP.md` | Resource Management M6.0 已实现：补齐 Catalog `variant` 示例和 `providerData` 扩展字典，稳定后续 Provider schema。 |
| `Tasks/RESOURCE_MANAGEMENT_M6A_PRELOAD_GROUP_WARMUP.md` | Resource Management M6A 已实现：Preload Group + Scene Warmup，独立策略服务，不改 `IResourceManager` 公共契约。 |
| `Tasks/RESOURCE_MANAGEMENT_M6B_VARIANT_AND_RETAIN_POLICY.md` | Resource Management M6B 已实现：显式 Variant Profile + RetainPolicy，解决变体选择和 asset churn。 |
| `Tasks/RESOURCE_MANAGEMENT_M6C_REMOTE_BUNDLE_PROVIDER.md` | Resource Management M6C 已实现第一段：RemoteBundle Provider 支持 file/local HTTP、cache、SHA-256 校验和结构化错误。 |
| `Tasks/RESOURCE_MANAGEMENT_M6D_ADDRESSABLES_PROVIDER.md` | Resource Management M6D 已后置为 Deferred / Optional：仅在项目已安装并决定使用 Addressables 时推进。 |
| `Tasks/RESOURCE_MANAGEMENT_M6_CLOSEOUT.md` | Resource Management M6 已收口：当前默认路线不引入 Addressables 硬依赖，优先使用现有 Catalog / Provider / Warmup / Variant / Retain / RemoteBundle 能力。 |
| `Tasks/AUTHORING_EDITOR_10_RUNTIME_PREVIEW_03_5_REAL_GAMEPLAY_LOOP.md` | Runtime Preview 03.5 已完成 Runtime Adapter 基础，继续收口 Patch resolver / result mapping / UI status。 |

---

## 文档职责表

| 文档 | 回答的问题 | 更新时机 |
|------|------------|----------|
| `PROJECT_INDEX.md` | Agent 最小上下文入口和 Context Pack 读取顺序 | Agent 上下文组织变化 |
| `CAPABILITIES.md` | 当前能用什么 | **每次新功能提交后** |
| `DESIGN.md` | 框架为什么存在、包含什么、不包含什么 | 目标或模块边界变化 |
| `ARCHITECTURE.md` | 模块如何协作、依赖如何约束、生命周期如何运行 | 新增模块或改变依赖方向 |
| `RUNTIME_FOUNDATION_SYSTEM.md` | Runtime Host、Frame/Command/Replay、SaveState 如何协作 | 开发运行时底座、回放、存档前 |
| `RESOURCE_MANAGEMENT_SYSTEM.md` | 资源引用、加载、释放、Catalog、Provider 和 Mod 资源包如何协作 | 开发资源管理模块前 |
| `RESOURCE_DIRECTORY_LAYOUT.md` | 框架样例资源正式目录、ResourceKey 命名、Catalog 归档和 FMOD bank 边界 | 迁移或新增正式样例资源前 |
| `RENDERING_PIPELINE.md` | 当前 Unity 渲染管线基线、URP 资产位置和场景/材质验证规则 | 渲染管线、材质、Shader、Volume、相机或 Demo 表现基线变化 |
| `COMBAT_ANIMATION_PHYSICS.md` | 确定性战斗动画、物理查询、命中结算和工具链如何协作 | 开发 Combat/动作/物理/联网战斗前 |
| `AGENT_GAME_CREATION_GUIDE.md` | Agent 制作小游戏 / Demo 时如何优先复用框架模块 | 开发小游戏、Playable Demo、Runtime Showcase 或场景验证前 |
| `USAGE.md` | 如何直接接入和组合模块 | 新增基础功能或改变推荐用法 |
| `INTERFACES.md` | 如何查找模块接口、跨模块依赖是否合法 | 新增模块或改变接口文档结构 |
| `Interfaces/*.md` | 各模块对外公开哪些类型和方法 | 修改公共 API 前 |
| `API_STANDARDS.md` | API 如何命名、如何演进、如何避免隐藏耦合 | 新增公共类型前 |
| `WORKFLOW.md` | 日常开发、验收、提交和推送如何执行 | 项目工作流变化 |
| `GITNEXUS.md` | GitNexus 如何接入、何时检查、输出如何使用 | GitNexus 工作流变化 |
| `Decisions/*.md` | 已接受的架构、流程和版本控制决策 | 重要决策接受或变更 |
| `Progress/*.md` | 当前进度、流程状态和合并后摘要 | PR 合并后或状态变化 |
| `QUALITY_GATE.md` | 什么算做完、如何验收 | 新增测试或发布要求 |
| `Guides/*.md` | 端到端使用和排错指南 | 新增跨模块工作流或排障路径 |
| `EDITORS.md` | 编辑器工具怎么呈现和验证框架状态 | 开发 Editor 工具前 |
| `AUTHORING_EDITOR_PROGRAM.md` | 外部编辑器总规划 | 外部编辑器范围变化 |
| `AUTHORING_EDITOR_USAGE.md` | 当前编辑器怎么用、限制、排错 | 编辑器功能变化 |
| `AUTHORING_WORKFLOW.md` | 创作流程跨工具协作 | 工作流变化 |
| `MIGRATION.md` | 每批迁移了什么、从哪里来、怎么改过 | 每次迁移后 |
| `ROADMAP.md` | 先做什么、后做什么、每阶段产物是什么 | 阶段计划变化 |
| 审计文档 | WGame 数据分析结果 | 审计完成后 |
| `Tasks/*.md` | 具体任务/子需求的设计稿（已完成的只做历史参考） | 不再实时同步 -> 看 CAPABILITIES.md |

---

## 核心原则

- 框架只提供通用机制，不携带 WGame 业务规则。
- 所有跨模块协作通过接口或数据契约完成。
- Runtime 代码不依赖 Editor，Core 基础层默认不依赖 UnityEngine。
- 配置来源、实体系统、AI 实现、具体 Buff/词条内容由游戏层接入。
- 每批迁移必须留下来源、适配说明、测试证据和剩余风险。

## 版本历史

| 版本 | 日期 | 变更 |
|------|------|------|
| 0.6.53 | 2026-05-24 | 新增 Story 模块 S0 设计契约：ADR-004 / ADR-005、Story core / Runtime / GameplayBridge 接口稿和 `STORY_S1` runtime slice 任务；明确 Story core 仅依赖 Core + Events、RuntimeCommand 边界、独立 command buffer ownership、SaveState provider/restorer 形态和 Gameplay effect bridge 规则 |
| 0.6.52 | 2026-05-23 | 新增 Character Gameplay Runtime foundation：角色包可生成 Gameplay component spawn definition，并通过 noEngine runtime bootstrap / live entity registry 创建真实 `GameplayEntityId` |
| 0.6.51 | 2026-05-23 | 新增 ADR-003 角色 Gameplay Runtime Bootstrap 设计记录，明确 `GameplayComponentWorld` 作为角色权威状态、Unity prefab 作为 view、Combat / Character Control / Animation / Debug UI 的桥接边界 |
| 0.6.50 | 2026-05-18 | 完成 Character Control playable vertical slice：在 `CombatAnimationPhysicsTest` 中串起 Local Input、Runtime AI Planner、state/motion/action、pressure reaction、MxAnimation 和 Debug UI，并补充 Showcase 回归测试 |
| 0.6.49 | 2026-05-18 | 扩展 Character Control v0.5：新增 Phase 13 Debug UI source，输出状态、last command、motion、action、pressure band 和 recent events 只读快照 |
| 0.6.48 | 2026-05-18 | 扩展 Character Control v0.4：新增 noEngine MxAnimation presentation adapter，支持 locomotion 1D/2D blend、reaction Play/CrossFade binding、backend result 和缺失绑定诊断 |
| 0.6.47 | 2026-05-18 | 扩展 Character Control v0.3：新增 Gameplay pressure reaction bridge，支持 posture / guard / armor break typed events 转换为控制 Reaction、动作取消和诊断事件 |
| 0.6.46 | 2026-05-18 | 扩展 Character Control v0.2：新增 Local Input command source、Runtime AI Planner command source、motion modifier / traction adapter contract 和对应测试 |
| 0.6.45 | 2026-05-18 | Phase 13 扩展 #185-#187：新增 Config Runtime patch hot reload、Debug UI input adapter、command gate 和 Observability 调试指南 |
| 0.6.44 | 2026-05-18 | Phase 13 扩展 #182-#184：新增 Debug UI timeline / entity watch、Diagnostics performance counters 和 noEngine Simulation Harness reports |
| 0.6.43 | 2026-05-18 | 新增 Character Control noEngine 编排接口、设计契约和 v0.1 实现入口，串联 Runtime frame、Combat Motion、Combat Action 与 Gameplay command bridge |
| 0.6.42 | 2026-05-18 | 新增 Phase 13 Observability and Developer Workflow 入口，落地 Debug UI core / Toolkit / adapter 首批任务文档 |
| 0.6.41 | 2026-05-17 | 收口 MxAnimation System Showcase 文档索引：能力清单补充 playable validation，详细手测流程归档到 `Docs/Demo/MX_ANIMATION_SYSTEM_SHOWCASE.md` |
| 0.6.40 | 2026-05-17 | 新增 Gitea -> GitHub Issue / PR 元数据手动镜像脚本，明确 PR 默认以 GitHub Issue 形式镜像，不把 GitHub 变成协作源 |
| 0.6.39 | 2026-05-15 | 新增 Runtime Debug UI Framework 01 设计文档，明确通用运行时调试 overlay 与现有 Showcase HUD 边界、DebugUI 程序集拆分、snapshot 聚合、输入/焦点行为和 M1-M5 实施切片 |
| 0.6.38 | 2026-05-14 | 新增资源目录规范文档，明确临时导入资源到正式 Samples 根、ResourceKey/label 命名、direct/dependency-only 划分、MagicEffects 身份和 FMOD bank 边界 |
| 0.6.37 | 2026-05-13 | 新增 Combat Animation RuntimeHost 集成模块，使用预注册服务模式和 Simulation / PostSimulation / Diagnostics 三阶段模块推进动作、武器轨迹和诊断快照 |
| 0.6.36 | 2026-05-13 | 新增 Combat Hit Resolve Bridge v0，补齐 owner 防护、阵营过滤、动态目标状态解析、事件派发和 Action window state adapter |
| 0.6.35 | 2026-05-13 | 新增 Gameplay Component Playable + Combat Bridge 规划文档，明确 Playable 场景升级、Combat bridge source-of-truth 边界和后续 implementation slices |
| 0.6.34 | 2026-05-13 | 新增 Gameplay Component Buff / Modifier v0，支持 component-native buff/modifier state、过期 buff cleanup、schema-backed hash 和 SaveState |
| 0.6.33 | 2026-05-13 | 收口 Agent Control Plane 文档：统一 WGameFramework / MxFramework 命名，明确人工/Agent 状态标签语义，将 `status/approved` 改为 `status/ready-to-merge`，并要求备份恢复测试包含 Git LFS 拉取验证 |
| 0.6.32 | 2026-05-13 | 调整 Agent Control Plane 可执行性：新增 S0-S3 任务等级，补齐 `status/spec-draft`，精简默认 Context Pack，允许 GitNexus 失败时替代影响面分析，并把 Harness 检查脚本化列为后续任务 |
| 0.6.31 | 2026-05-13 | 将 Gitea 流程升级为 Agent Control Plane：新增 `PROJECT_INDEX.md`、`Progress/CurrentStatus.md` 和 ADR-002，Issue/PR 模板加入 Context Pack 与 Agent Session 审计 |
| 0.6.30 | 2026-05-13 | 将开发流程升级为 Gitea Issue / Branch / PR 驱动，新增 `.gitea` Issue/PR 模板和 `Docs/Decisions/ADR-001-Version-Control-Gitea.md` |
| 0.6.29 | 2026-05-13 | 明确以后开发流程以 NAS Gitea `origin` 为主仓库和协作源，GitHub 仅作为非 LFS Git 镜像 |
| 0.6.28 | 2026-05-13 | 新增 `WORKFLOW.md` 作为项目日常开发、验证、提交和推送的统一入口，并让 `AGENTS.md` 等文档引用该入口 |
| 0.6.27 | 2026-05-13 | 新增 `GITNEXUS.md` 作为 GitNexus 唯一工作流入口，收束散落的接入、影响面分析和提交前检查规则 |
| 0.6.26 | 2026-05-13 | 记录 Git 推送模式：`origin` 为 NAS Gitea 主 remote，`github` 为跳过 LFS 上传的 GitHub 镜像 remote，使用 deploy key 和 `pre-push` 防护 |
| 0.6.25 | 2026-05-12 | 新增 Gameplay Component Ability Targeting v0，支持 generation-safe request target、team/lifecycle/tag/status filter 和 request cast |
| 0.6.24 | 2026-05-12 | 新增 Gameplay Component Ability Command Bridge v0，支持 component entity 通过 `CastComponentAbility` 执行 self attribute delta ability |
| 0.6.23 | 2026-05-12 | 新增 Gameplay Component Attribute Runtime v0，支持 component-native attribute set、attribute commands、events、hash 和 SaveState |
| 0.6.22 | 2026-05-12 | 新增 Gameplay Component Spawn Definitions v0，支持按显式 definition 创建带初始 components 的 component entity |
| 0.6.21 | 2026-05-12 | 新增 Gameplay Component State Systems v0，支持 `GameplayLifecycleCleanupSystem` 清理 `PendingDestroy` component entity |
| 0.6.20 | 2026-05-12 | 新增 Gameplay Component SaveState v0，支持 `GameplayComponentWorldSaveStateProvider`、core component save adapters 和 JSON roundtrip restore |
| 0.6.19 | 2026-05-12 | 新增 Gameplay Component Runtime Hash v0，支持 `GameplayComponentWorldHashContributor`、core component hash writers 和 schema-backed hash ordering tests |
| 0.6.18 | 2026-05-12 | 新增 Gameplay Component Schema Registry v0，支持 schema metadata、capability adapter lookup 和 core diagnostics descriptors |
| 0.6.17 | 2026-05-12 | 新增 Gameplay Component Schema 契约文档，固定 component value 注册、诊断、hash 和 SaveState adapter 边界 |
| 0.6.16 | 2026-05-11 | 更新 agent 小游戏规范：移动、碰撞、拾取、出口和物理查询必须优先使用框架物理 / Motion 模块，Unity Rigidbody/Collider/trigger 只能作为非权威 view-only 辅助；Marble Maze 标记为需重构到框架物理权威 |
| 0.6.15 | 2026-05-11 | 新增 Marble Maze Playable Demo，验证 Unity Physics adapter 与 RuntimeHost / CommandBuffer / Replay hash / SaveState JSON 的边界；场景资产由 Unity 生成，支持框架 `DefaultInputService` 输入和 UI Toolkit HUD |
| 0.6.14 | 2026-05-11 | 新增 `AGENT_GAME_CREATION_GUIDE.md`，把 agent 制作小游戏 / Demo 的 API 复用计划、标准分层、禁用项和验收清单设为文档入口 |
| 0.6.13 | 2026-05-11 | 新增 Input v0.1：Unity Input System 采集层上封装 `IInputProvider`、`InputSnapshot`、`InputCommandQueue`、上下文栈、重绑定和本地多人 `LocalUserInputAdapter` |
| 0.6.11 | 2026-05-11 | Breakout Runtime Showcase 升级 v0.3：新增关卡推进、砖块类型/HP/道具砖、多球、Wide/Slow/Multi/ExtraLife/Laser 道具、反馈事件和 UI Toolkit 多球/砖块类型渲染 |
| 0.6.10 | 2026-05-11 | Breakout Runtime Showcase 升级 v0.2：未发球时球在球拍上滚动，发球方向由球相对球拍位置决定，并补充 runtime 测试 |
| 0.6.9 | 2026-05-11 | 新增 Breakout Runtime Showcase v0.1，用纯 C# 打砖块验证连续运动、AABB 碰撞、道具、Replay hash、SaveState JSON roundtrip，并新增 AppFlow / SceneFlow UI Toolkit 试玩层代码；当前仓库未提交 Breakout 场景资产 |
| 0.6.8 | 2026-05-11 | 新增 Tetris Runtime Validation v0.1，用纯 C# Tetris 验证 RuntimeHost、CommandBuffer、Replay playback hash、hash mismatch 和 SaveState JSON roundtrip，并新增 PlayMode 试玩场景 |
| 0.6.7 | 2026-05-11 | 完成 App / Scene Flow v0.1 foundation，新增 noEngine AppFlow / SceneFlow 契约、RuntimeHost 模块和 Unity SceneManager 适配 |
| 0.6.6 | 2026-05-10 | 完成 Runtime Foundation Core v0.1：新增 MxFramework.Runtime Host、Frame/Command/Replay、SaveState 契约、迁移管线、JSON roundtrip 和 Runtime 接口文档 |
| 0.6.5 | 2026-05-10 | 新增 Runtime Foundation System 规划，拆出 Runtime Host、Frame/Command/Replay、SaveState 三个 P0 开发文档 |
| 0.6.4 | 2026-05-10 | 完成 Resource Management M6 closeout，明确 Addressables Adapter 后置为可选独立程序集，默认资源路线不引入 Addressables 硬依赖 |
| 0.6.3 | 2026-05-10 | 完成 Resource Management M6C Remote Bundle Provider 第一段，支持 file/local HTTP、cache hit、SHA-256 校验和 EditMode 覆盖 |
| 0.6.2 | 2026-05-10 | 完成 Resource Management M6B Variant Catalog + Retain Policy，新增 ResourceVariantProfile、ResourceRetainPolicy、retain diagnostics 和 EditMode 覆盖 |
| 0.6.1 | 2026-05-10 | 完成 Resource Management M6A Preload Group + Scene Warmup，新增 ResourcePreloadService、label 查询、group handle 和 EditMode 覆盖 |
| 0.6.0 | 2026-05-10 | 完成 Resource Management M6.0 Catalog Schema Prep，`ResourceCatalogEntry` 新增 `ProviderData`，Streaming Catalog 支持 providerData JSON |
| 0.5.9 | 2026-05-10 | 整理 Resource Management M6 路线，把 M6 拆为 Schema Prep、Preload/Warmup、Variant/Retain、RemoteBundle 和 Addressables 可选任务 |
| 0.5.8 | 2026-05-10 | 完成 Resource Management M5 Diagnostics + Editor Validation，接入 ResourceDebugSource、Catalog validator 和 AssetDatabase 校验 |
| 0.5.7 | 2026-05-10 | 完成 Resource Management M4 Mod Package resourceCatalog、包内路径校验、ResourceManager 挂载和 LoadPlan 禁用包验证 |
| 0.5.6 | 2026-05-10 | 完成 Resource Management M3 StreamingAssets Catalog loader、AssetBundle Provider、dependency ref-count 和 Resources 测试组验证 |
| 0.5.5 | 2026-05-09 | 完成 Resource Management M2 Unity Resources Provider、Resources.Unity asmdef、TextAsset Demo 和 Unity EditMode 验证 |
| 0.5.4 | 2026-05-09 | 完成 Resource Management M1 noEngine 契约、Memory Provider、ResourceManager、Handle、DebugSnapshot 和 Resources 接口文档 |
| 0.5.3 | 2026-05-09 | 新增资源管理系统设计文档入口，明确 ResourceKey、Catalog、Provider、Handle、Mod 资源包和阶段切片 |
| 0.5.2 | 2026-05-09 | Phase 11 Runtime Gameplay Foundation closeout 完成，状态收口为 Accepted / Closed，并同步能力清单、使用手册和 Gameplay 接口状态 |
| 0.5.1 | 2026-05-09 | 新增 Phase 11 M5 Ability Authoring Contract 任务文档，并派发 contract / validator / mapper / schema summary 实现范围 |
| 0.5.0 | 2026-05-09 | 完成 Combat Motion v1 Capsule Character Proxy / Narrow Phase，实现 Motion capsule contract、capsule sweep、skin width / no penetration、Showcase summary 和 Combat regression |
| 0.4.9 | 2026-05-09 | 新增 Combat Motion v1 Capsule Character Proxy / Narrow Phase 任务入口，并把 Combat P0 下一步从泛泛 Motion v1 校准为该 P0 功能包 |
| 0.4.8 | 2026-05-09 | 校准当前 P0 Goal、Phase 11/12 下一步、关闭 Combat Physics Debug / World Lifecycle closeout，并更新 Runtime Preview 03.5 状态 |
| 0.4.7 | 2026-05-09 | 收口 Combat Motion Kinematic Character Movement v0 实现、Showcase 接入和验收记录 |
| 0.4.6 | 2026-05-09 | 新增 Combat Motion Kinematic Character Movement v0 任务入口 |
| 0.4.5 | 2026-05-09 | Phase 9 closeout 接受关闭，补充 Schema seed / reference rules / closeout report 入口 |
| 0.4.4 | 2026-05-08 | 修订 Combat Authoring / Gizmo 工具实现约束和验收口径 |
| 0.4.3 | 2026-05-08 | 新增 Combat Authoring / Gizmo 工具设计入口 |
| 0.4.2 | 2026-05-08 | 新增 Combat Animation Physics Epic 任务派发入口 |
| 0.4.1 | 2026-05-07 | 新增 Combat Animation Physics 开发设计入口 |
| 0.4.0 | 2026-05-07 | 新增 CAPABILITIES.md，裁减 README.md 文件列表，降级 Tasks/ 为历史参考 |
| 0.3.0 | 2026-05-05 | 初始文档索引 |
