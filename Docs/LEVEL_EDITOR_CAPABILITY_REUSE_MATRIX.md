# Mx 关卡编辑器框架：能力复用矩阵

> Status: Current
>
> 用途：关卡编辑器新增功能前的模块复用门禁与当前接入盘点
>
> Last verified: 2026-07-28

## 结论

Mx 关卡编辑器不是新的玩法运行时。它负责把项目已有模块的 Authoring、引用选择、场景可视化、校验、编译、应用和调试入口组织到同一个简洁工作流中。

以后新增关卡编辑功能时，默认遵守以下顺序：

1. 先确定玩法数据和运行时行为由哪个 MxFramework 模块负责。
2. 复用该模块已有的 Definition、Authoring、Compiler、Catalog、Runtime、SaveState、Replay 和 Diagnostics 契约。
3. 只在关卡编辑器中补薄的模块适配层，不复制领域规则。
4. 只有在源码、接口文档和测试中都无法找到等价能力后，才把它登记为缺口。
5. 缺口属于物理、Trigger、Story、Resources 等领域时，优先扩展对应领域模块；只有通用的窗口编排能力才进入关卡编辑器 Core。

这意味着：

- 碰撞、范围查询、角色运动和可达性判定优先使用 `CombatPhysicsWorld`、`CombatKinematicMotor` 及其查询契约，不建立第二套关卡物理。
- 区域、阈值、Signal、Enter/Stay/Exit、条件、冷却、次数与动作路由使用 `Gameplay.Triggers`，不在编辑器里再写一个 Trigger Runtime。
- 角色、技能、属性、Buff、物品、Story、Runtime AI Planner、资源、VFX、Audio、Animation、Camera 等内容只编辑或绑定各自模块拥有的配置。
- Unity Scene、Transform、Renderer、Terrain、灯光和 SceneView 继续承担场景创作与可视化；它们不能偷偷成为玩法运行时的第二份权威。

## 权威分层

| 层 | 负责什么 | 不负责什么 |
|---|---|---|
| Unity Scene Authoring | Transform、层级、Renderer、Terrain、灯光、Prefab 实例、SceneView 选取和预览 | 确定性碰撞、Trigger 状态、玩法条件、SaveState 或 Replay 权威 |
| Mx Level Editor Shell | 模块切换、列表/属性、放置、Undo/Redo、统一保存、诊断汇总、场景定位 | 各领域的运行时规则和重复配置模型 |
| Domain Authoring / Compiler | 领域 Definition、结构化 Authoring、引用校验、编译和稳定 hash | 窗口布局及通用 SceneView 交互 |
| Mx Runtime Authority | RuntimeHost 中的 Physics、Gameplay、Trigger、Story、Runtime AI Planner 等确定性状态与行为 | 读取编辑器临时状态或把 Unity Inspector 当成运行时协议 |
| Derived Cache / Presentation | 场景缓存、调试快照、Gizmo、标签、预览和运行时投影 | 反向覆盖 Authoring 权威 |

允许使用 `HandleUtility.PlaceObject`、`HandleUtility.PickGameObject`、`Handles`、`Renderer.bounds`、`AssetDatabase` 等 Unity Editor 能力完成选取、落点、预览和资源查找；这些结果必须在保存或应用时转换为对应领域模块的数据。禁止用 `Physics.Raycast`、`OnTriggerEnter`、`NavMeshAgent` 或随意新增的 `MonoBehaviour.Update` 作为新关卡玩法的默认运行时权威。

## 状态说明

| 状态 | 含义 |
|---|---|
| 已接入 | 当前关卡编辑器已经直接使用该能力。 |
| 待接入 | 领域运行时或 Authoring 契约已经存在，关卡编辑器还缺薄适配层。 |
| 项目专用 | CharacterTest 已有实现，但尚不足以提升为框架公共能力。 |
| Unity Authoring | 应继续由 Unity 原生场景工具负责，关卡编辑器只做引用、快捷入口或校验。 |
| 已确认缺口 | 当前源码和接口没有覆盖目标能力；新增前仍需确定缺口应归属哪个领域模块。 |

## 场景、物理与导航

| 使用者要做什么 | 数据 / 运行时权威 | 可复用入口 | 关卡编辑器职责 | 当前状态 | 禁止重复实现 |
|---|---|---|---|---|---|
| 选择、移动、旋转、缩放场景对象 | Unity Scene 的 Transform | Unity Selection、Undo、Scene dirty；`IMxLevelEditorSelectionSyncModule`、`IMxLevelEditorSceneChangeAwareModule` | 同步登记、未保存状态、选择和诊断 | 已接入 | 在 JSON 再保存一份 Transform 并与 Scene 互相覆盖 |
| 新建、复制、重新放置对象 | Unity Scene + 当前模块 Authoring 文档 | `IMxLevelEditorPlacementModule`、SceneView、`HandleUtility.PlaceObject`、统一文档事务 | 提供表面落点、真实预览、稳定 ID、一次 Undo 和取消恢复 | 已接入 | 把编辑器落点查询当成运行时碰撞 |
| 登记世界对象、点位、区域和场景角色 | 当前 `mx.level.authoring.v3` 文档与 CharacterTest 场景缓存 | `CharacterTestWorldObjectLevelEditorModule`、`CharacterTestSceneCharacterLevelEditorModule` | 编辑、校验、保存并生成 CharacterTest 当前消费的缓存 | 已接入，但仍是 CharacterTest 项目专用 |
| 运行时静态/动态碰撞和范围查询 | Combat | `CombatPhysicsWorld`；AABB、Sphere、Capsule collider；Ray、Sphere、Capsule、AABB、Sector、OBB query | 编辑形状与层，编译为 Combat 数据，显示查询/碰撞诊断 | 部分已接入：P2.4 已把 TriggerRegion 编译为 fixed Combat AABB 并显示实际查询；通用形状/碰撞 Authoring 仍待接入 | 新建 LevelEditor Physics 或默认依赖 Unity PhysX |
| 角色和可移动物体运动 | Combat / CharacterControl / CharacterTest Motion | `CombatKinematicMotor`、CharacterControl kinematic adapter、CharacterTest motion systems | 绑定运动/材质配置并提供只读运行时诊断 | 待接入 | 由编辑器脚本或 Transform Update 决定正式运动 |
| 持久化基础碰撞关卡 | Combat Levels | `CombatLevelDefinition`、`CombatLevelWorldBuilder`、`CombatLevelJson`、`CombatLevelHash` | 评估并实现无损投影、校验和 hash 展示 | 待接入 | 另建无法进入 CombatPhysicsWorld 的碰撞格式 |
| 地表材质、摩擦、弹性和固定弹跳 | Combat Surface Material | `CombatSurfaceMaterial`、`CombatLevelSurfaceMaterialDefinition` | 提供材质选择、场景覆盖预览和引用校验 | 待接入 | 只读 Unity PhysicMaterial 并令其成为玩法权威 |
| Terrain、Mesh、灯光和美术布景 | Unity Scene / Rendering / Resources | Unity Terrain、Renderer、Lighting；Mx Rendering 与 Resource catalog | 保留 Unity 原生创作体验，补资源引用和发布校验 | Unity Authoring | 在关卡编辑器重做 Terrain、建模或灯光工具 |
| 局部避障和导航建议 | 当前为 CharacterTest Runtime AI Planner + Combat Physics | `ICharacterTestAiNavigationService`、`CharacterTestCombatPhysicsNavigationService` | 只读显示 CombatPhysics 采样和路径诊断 | 项目专用 | 使用 Unity NavMesh 作为新框架权威，或把 Scene Gizmo 当路径数据 |
| 全局图寻路和 A* | 尚无公共框架 API | CharacterTest 文档只有 L2 目标设计 | 先等待第二消费者和稳定契约，再决定是否提供图 Authoring | 已确认缺口 | 把未来设计写成“已完成”，或先做编辑器图面板再补运行时 |

补充限制：`CombatPhysicsWorld` 已支持 AABB、Sphere 和 Capsule，但当前 `CombatLevelDefinition` schema v1 只持久化 AABB。关卡编辑器不能为了接入该类型而丢失 CharacterTest 已有的 Sphere/Capsule 数据；应先在 Combat Levels 或显式投影层补齐无损边界。

## 玩法、角色与流程

| 使用者要做什么 | 数据 / 运行时权威 | 可复用入口 | 关卡编辑器职责 | 当前状态 | 禁止重复实现 |
|---|---|---|---|---|---|
| 创建区域、接近、阈值和 Signal Trigger | Gameplay.Triggers | `GameplayTriggerAuthoringDocument`、`GameplayTriggerAuthoringJson`、`GameplayTriggerConfigCompiler`、Trigger source/definition/route/binding | 提供结构化编辑、场景范围可视化、编译诊断和引用选择 | 已接入首批闭环：P2.3 完成 Authoring；P2.4 在 CharacterTest offline session 执行三个固定模板并提供 typed 运行诊断；通用阈值/Signal 编辑未接入 | 新建关卡专用 Trigger Runtime 或用 `OnTriggerEnter` 直执行业务 |
| 使用 Combat 形状驱动 Trigger | Gameplay.Triggers + Combat | `GameplayTriggerCombatSensorSystem` | 将关卡区域投影为 Trigger source，并可视化实际 Combat 查询 | P2.4 已接入首版 fixed AABB：无需 Unity Collider，cache/bootstrap 双重 hash 校验；Sphere/Capsule/Sector 尚未开放 | 为新关卡默认接入 PhysX occupancy |
| 配置 Trigger 条件、冷却、次数和动作 | Gameplay.Triggers 及目标领域 Bridge | condition program、activation policy、same-frame/deferred/reliable action sink | 编辑白名单结构、校验 target，并显示投递语义 | 首批固定模板已接入；P3.3e 渐进接入可展平的`All / Any / Not`条件列表，候选只来自当前 Source Adapter 的正式语义；任意混合嵌套和节点图尚未开放 | 在 UI 回调中直接改门、角色、Buff 或 Story 状态 |
| 配置属性、Buff、Modifiers、能力和 Gameplay 组件 | Gameplay / Attributes / Buffs / Modifiers | 对应 Definition、ID、组件与 command 契约 | 使用领域 catalog picker，显示引用和编译错误 | 待接入 | 新建关卡变量表、Buff 表或技能执行器 |
| 放置和生成角色 | Character.Application / RuntimeSpawn；CharacterTest 当前缓存 | Character package/definition、spawn request、现有场景角色模块 | 选择角色资源、队伍、控制来源、出生点和覆盖配置 | 部分已接入 | 复制角色定义或绕过正式 spawn 链 |
| 绑定角色动作、受击、装备和技能 | CharacterAction / CharacterControl / Gameplay / Combat | action library、timeline、reaction、equipment/ability binding | 只做引用选择、组合预览和交叉校验 | 待接入 | 在关卡文档复制动作时间线或战斗规则 |
| 配置 Runtime AI Planner 行为 | MxFramework.AI + CharacterTest Runtime AI Planner | planner facts/profile、CharacterTest behavior profile 与现有诊断 | 选择 profile、覆盖必要参数、显示只读决策/感知结果 | 部分已接入且含项目专用能力 | 在关卡编辑器再做一套行为树运行时 |
| 编排剧情、对话、任务触发和叙事节拍 | Story / Story.Runtime | Story graph/command；现有 `Story.RaiseTrigger` reliable bridge | 绑定 Story ID、选择模板、可视化引用和运行结果 | 首版 `Story.RaiseTrigger` Authoring 与 CharacterTest offline reliable runtime/result 已接入；对话/任务/图编排未接入 | 在关卡编辑器建立第二个故事状态机 |
| 切换场景、加载阶段和应用流程 | AppFlow / SceneFlow / Resources | `AppFlowController`、`SceneFlowController`、`UnitySceneFlowDriver` | 编辑场景键和合法跳转，提供预检及测试入口 | 待接入 | 用按钮回调或场景脚本另写加载 FSM |
| 关卡内交互、掉落、库存和装备结果 | Items / Gameplay / Runtime commands | Item definition、loot/world item/inventory/equipment bridge | 绑定定义、预览结果和验证引用 | 待接入 | 在关卡对象上保存另一份物品/库存逻辑 |
| Playtest 输入和调试操作 | Input / Runtime command buffer | Mx Input、`RuntimeCommandBuffer`、对应 gameplay commands | 提供开始、暂停、重置和受控调试命令入口 | 待接入 | 直接从编辑器修改正式运行时内部字段 |

`Gameplay.Triggers.Unity` 已存在隔离的 Unity occupancy adapter，但它不改变默认方向：由框架关卡创作的正式玩法区域优先走 Combat sensor bridge。Unity occupancy 只适合明确声明的遗留或外部 Unity 组件接入场景，不能因为已有 adapter 就让 PhysX 成为新关卡的默认权威。

## 资源与表现

| 使用者要做什么 | 数据 / 运行时权威 | 可复用入口 | 关卡编辑器职责 | 当前状态 | 禁止重复实现 |
|---|---|---|---|---|---|
| 从素材/定义库添加内容 | Resources / Config / 各领域 Catalog | `ResourceKey`、`ResourceCatalog`、`IResourceManager`、Editor AssetDatabase provider | 构建统一搜索和选择器，保存稳定资源身份并验证可发布性 | 待接入 | 只保存任意 Asset 路径或再建独立素材数据库 |
| 配置特效和表现绑定 | VFX | VFX definition/profile、Gameplay/Combat/Rendering/Unity bridges | 选择语义效果、预览、显示生命周期槽和缺失引用 | 待接入 | 在关卡文档复制 VFX runtime 或直接散落 Prefab 路径 |
| 配置环境音、交互音和声学材质 | Audio | Audio definition/catalog、FMOD adapter、Physical Audio | 选择声音语义、覆盖范围、预览和发布校验 | 待接入 | 新建关卡音频运行时或绕过 Audio 生命周期 |
| 配置动画、动作表现和状态映射 | Animation / CharacterAction / CharacterControl | MxAnimation contract、action/reaction binding | 选择并预览已有配置，显示跨模块引用 | 待接入 | 在关卡编辑器保存另一份 Animator/动作规则 |
| 配置镜头区域和关卡镜头 | Camera | Camera profile/service | 编辑 profile 引用和区域参数，提供 SceneView 预览 | 待接入 | 直接用场景脚本创建第二套 Camera 状态机 |
| 配置场景 GI、雾和共享 Look | Rendering | `MxSceneRenderingConfiguration` / `MxSceneRenderingSession`、现有 Provider 与构建器 | `characterTest.sceneRendering` 编辑 GameManager 场景绑定、Look、构建与诊断；编辑器独立预览 | 当前基地已接入；不写玩法 JSON | 从关卡窗口重写渲染管线或复制太阳参数到另一份权威 |
| 制作和发布玩家可见 Guidance UI | UI / FairyGUI 及对应 gameplay view model | 现有 Guidance Card、Notice Banner、Presenter 与正式 UI binding contract | 不承担 FairyGUI 外观创作；语义预览只检查信息结构 | 正式 Guidance 表现已存在；不属于 P3.4 的新增实现 | 在关卡编辑器制作第二套 player-facing UI、加载 FairyGUI package 充当语义预览，或由 Presenter 推进教学 |
| 创作关卡教学序列 | CharacterTest 项目私有 Guidance plan；移动、Grounded、相机、锁定与伤害事实仍归各现有 Runtime 领域 | `mx.level.authoring.v3`、`MxLevelEditorSaveCoordinator`、现有 Guidance DTO 与正式领域事件 / telemetry | 作为独立模块从空白或六个玩家意图预设编辑任意有序步骤，在五类已注册 typed completion condition 间安全切换，校验并编译 Guidance Scene cache，提供非视觉验收性质的语义预览 | P3.4 Current：自由步骤 Authoring、条件切换、三栏编辑、正式 payload/cache、Coordinator/typed Observer/Snapshot Mapper、生产 Host、freshness 与 Playable/视觉门禁均已接入；当前六步只是正式内容和创作预设 | 第二份关卡主文档、直接暴露技术 type 作为新建意图、任意事件/反射/条件图、从 Editor 路径直接驱动 Player build、复制 Gameplay 完成事实或回退旧硬编码六步 |

## 生命周期、保存与验证

| 使用者要做什么 | 数据 / 运行时权威 | 可复用入口 | 关卡编辑器职责 | 当前状态 | 禁止重复实现 |
|---|---|---|---|---|---|
| 撤销、重做、保存和放弃修改 | Unity Scene + 各模块 Authoring 文档 | Unity Undo group、`MxLevelEditorSaveCoordinator`、模块 save participant | 统一事务、保存基线、关闭提示和恢复 | 已接入 | 每个模块各自悄悄保存，或只保存 Scene 不保存 Authoring |
| 校验结构、ID、引用和编译结果 | Config + 各领域 Validator/Compiler | Config reference/schema validation、领域 compiler/report | 汇总诊断、定位场景与文档路径 | 部分已接入 | 在窗口层复制领域校验规则 |
| 构建运行时 Session 和固定 Tick | Runtime | `RuntimeHost`、module lifecycle、fixed phases、`RuntimeCommandBuffer` | 提供受控 Playtest 启动和状态查看 | P2.4 已接入 CharacterTest offline Trigger composition；专用 Playtest 启动和网络 session 尚未接入 | 编辑器自建 Update 驱动的玩法循环 |
| SaveState、checkpoint、Replay 和 hash | Runtime + 参与的领域模块 | Runtime SaveState/Replay/Hash；Trigger 等模块 participant | 验证关卡配置能进入正式链路，提供测试/差异报告 | P2.4 已接入 level-authored Gameplay/Trigger/Story checkpoint、恢复与组合 hash；通用差异报告和 durable 用户存档 UI 未接入 | 保存 GameObject 快照冒充正式存档或回放 |
| 网络/帧同步验证 | Network + Runtime authority snapshot | 正式 command/snapshot/hash 契约 | 启动验证场景并展示差异，不编辑协议内部状态 | 待接入 | 根据 Scene Transform 直接判定网络一致性 |
| 运行时诊断和可视化 | Diagnostics / DebugUI + 各领域 snapshot | diagnostics source、debug snapshot、query explanation | 将已存在诊断聚合到选中对象和 Scene 标记 | P2.4 已在选中 TriggerRegion 的运行时页显示 typed query/sensor/occurrence/action；通用聚合与 Scene runtime overlay 未接入 | 从 Unity 场景反推一份与 Runtime 不一致的“真相” |

## 已确认缺口与未知项

以下条目是本轮源码和 Current 接口盘点得出的边界。它们不是“重造整个系统”的理由：

| 项目 | 结论 | 正确归属 |
|---|---|---|
| 通用关卡领域适配 | Level Editor Shell 已有模块、放置、SceneView、保存和选择契约，但当前实际模块仍集中在 CharacterTest 世界对象与场景角色 | 先用现有 `IMxLevelEditor*` 契约接入一个新领域；出现重复适配代码后再提炼，不预先设计庞大 SDK |
| Combat 关卡形状持久化 | `CombatPhysicsWorld` 支持 AABB/Sphere/Capsule，`CombatLevelDefinition` v1 只覆盖 AABB | Combat Levels schema / compiler 或显式无损投影层 |
| Unity 美术几何到 Combat 数据的通用 Bake | 当前未发现覆盖 Terrain/Mesh/多形状、稳定 ID、增量重建和无损校验的统一 Current 契约 | Combat Authoring / Levels，而不是 Level Editor Runtime |
| Trigger 关卡 Authoring UI | P2.3 已接入选中 TriggerRegion 的渐进配置卡和三个固定模板，P2.4/P2.5 已补运行时检查与正式 Presence Buff；P3.0～P3.2 已形成引用、目标语义和筛选能力。当前已出现区域以外来源和统一事件/条件/动作的真实产品需求 | 按 P3.3 设计建立顶层`玩法触发器`工作区，但仍只投影现有 Trigger Authoring/Compiler/Runtime；先完成区域功能等价迁移，再按正式 Source Adapter 逐个接入 Threshold 或 Signal，不建设第二套 Trigger Runtime |
| Guidance 教学序列 Authoring | P3.4 已具备空白/六预设自由步骤 Authoring、五类可切换 typed condition、项目私有正式 payload、编译 cache、独立 coordinator、typed observer、snapshot mapper 与 plan 驱动的生产 Host；未知 type 可修复、未知 schema 只读保护和 stale cache preflight 已接入 | 作为 Current 能力维护；保持唯一 `mx.level.authoring.v3` envelope，Level Editor 只创作与编译，正式领域事实保持外部权威，GameManager cache 继续作为 Runtime 不可变启动输入；空白草稿必须补全或删除后才允许正式保存 |
| Trigger 持久化输入流程 | `GameplayTriggerAuthoringJson` 与 `characterTest.gameplayTriggers` payload 已在 P2.3a 交付；P2.4 已交付 GameManager scene cache/compiler/loader 与完整 hash 复验 | Gameplay.Triggers Config canonical boundary + CharacterTest level wrapper |
| 通用全局寻路 | 当前只有 CharacterTest L1 CombatPhysics 局部 probe；公共 graph/A* 仍是未来目标 | 先留在 CharacterTest，直到实现稳定且出现第二消费者 |
| 通用关卡发布产物 | 当前 CharacterTest 仍从 GameManager 场景缓存读取，尚未形成跨游戏统一 Level Package | 需要单独设计 Resources + domain compiler 的发布边界，不能由窗口私自定义 |

“未确认”表示需要在实施对应功能前继续查证，不能直接当作缺失 API；“已确认缺口”也必须先在正确领域补齐，再由关卡编辑器消费。

## 新功能门禁

每个关卡编辑器增量在编码前必须填写下面这份最小复用记录：

```text
用户操作：
玩法 / 数据权威模块：
已存在的 Definition / Authoring / Compiler / Runtime API：
关卡编辑器只需补的适配：
Authoring 单一事实源：
运行时派生产物：
Undo / 保存 / 放弃修改边界：
Runtime / SaveState / Replay / Hash 影响：
缺口证据（源码、接口、测试）：
是否真的需要新公共 API：
```

评审时按以下规则拒绝实现：

- 没写权威模块就开始创建新的 JSON schema、MonoBehaviour 或 manager。
- 已有领域 Definition，却在 Level Editor 模块中复制相同字段和规则。
- 只验证 Unity Scene 表现，没有验证 RuntimeHost 实际消费的派生数据。
- 用 Unity Physics、NavMesh、Animator callback 或 UI callback 绕过框架权威，却没有明确的隔离适配说明。
- 把 CharacterTest 专用实现直接命名成通用框架能力，没有第二消费者和稳定接口证据。
- 把未来设计文档中的目标写成当前已完成能力。

## 建议的渐进接入顺序

这份矩阵只确定复用方向，不代表以下功能已经交付。后续宜按小增量推进：

1. **领域引用基础**：让放置对象通过 Resources/Config/领域 Catalog 选择稳定定义，并统一引用诊断。
2. **Combat Physics Authoring 投影**：先覆盖当前世界对象的 AABB/Sphere/Capsule 与地表材质，显示“场景表示 → Combat 数据 → Runtime 查询”的差异。
3. **Gameplay Trigger Authoring**：直接编辑 Trigger authoring document，编译到现有 Trigger catalog，并使用 Combat sensor bridge。
4. **角色与玩法绑定**：复用 Character.Application、CharacterAction、Gameplay、Items 和 Runtime AI Planner 的定义选择器。
5. **Guidance 教学序列 Authoring**：P3.4 Current 已复用唯一 level envelope、正式领域完成事实和现有 FairyGUI 表现，交付空白/六预设自由步骤、五类可切换 typed condition、Scene cache、Runtime coordinator/observer/mapper 与 Playable/视觉证据；后续扩展不得用任意条件图或旧硬编码回退扩大边界。
6. **Story / AppFlow / 其他表现模块绑定**：只接入稳定 ID、预览、校验和运行结果，不复制各模块工作台。
7. **受控 Playtest 与诊断**：通过 RuntimeHost、正式 command、snapshot、hash 和 Diagnostics 验证整条链路。

每一步继续保持当前简洁五区布局，通过选择对象后的渐进属性和临时弹层暴露能力，不按策划、地编、程序、美术拆岗位模式，也不把所有模块做成常驻面板。

其中第 1～3 步已细化为第二阶段文档：[定义驱动玩法闭环设计](LEVEL_EDITOR_DEFINITION_DRIVEN_GAMEPLAY_DESIGN.md)负责产品、交互和权威边界，[开发指南](Guides/LEVEL_EDITOR_DEFINITION_DRIVEN_GAMEPLAY_DEVELOPMENT.md)负责 P2.0～P2.5 的代码增量、迁移、测试和提交台账。当前 P2.0～P2.5 已实现，CharacterTest offline `Playable` 闭环已经通过正式场景样例、保存重开和实际运行验收；网络 session 注入、活动 session 热重载与通用跨游戏 Level Package 仍是独立后续，不由第二阶段关卡窗口重复实现。

## 当前事实依据

- [游戏功能开发复用规则](AGENT_GAME_CREATION_GUIDE.md)
- [Combat 接口与确定性物理](Interfaces/Combat.md)
- [Gameplay Trigger Runtime 与 Authoring/Compiler](Interfaces/Gameplay.Triggers.md)
- [RuntimeHost、SaveState、Replay 与 Hash](Interfaces/Runtime.md)
- [Gameplay 模块边界](Interfaces/Gameplay.md)
- [Resources 资源身份与加载](Interfaces/Resources.md)
- [AppFlow 与 SceneFlow](Interfaces/AppFlow.md)
- [Runtime AI Planner 接口](Interfaces/AI.md)
- [CharacterTest 导航基础及当前限制](Guides/CHARACTER_TEST_RUNTIME_AI_PLANNER_PATHFINDING_FOUNDATION.md)
- [当前关卡编辑器快速布置能力](Guides/LEVEL_EDITOR_QUICK_PLACEMENT_GUIDE.md)
- [第二阶段定义驱动玩法闭环设计](LEVEL_EDITOR_DEFINITION_DRIVEN_GAMEPLAY_DESIGN.md)
- [第二阶段开发与验收台账](Guides/LEVEL_EDITOR_DEFINITION_DRIVEN_GAMEPLAY_DEVELOPMENT.md)
- [P3.3 统一触发器工作区设计](LEVEL_EDITOR_UNIFIED_TRIGGER_WORKSPACE_DESIGN.md)
- [P3.4 教学引导 Authoring 设计](LEVEL_EDITOR_GUIDANCE_AUTHORING_DESIGN.md)
- [P3.4 教学引导 Authoring 开发指南](Guides/LEVEL_EDITOR_GUIDANCE_AUTHORING_DEVELOPMENT.md)
- `Packages/com.vincent.wgameframework/Editor/Levels/`
- `Assets/Scripts/WGameFrameworkProject/CharacterTest/Editor/`
- `Packages/com.vincent.wgameframework/Combat/`
- `Packages/com.vincent.wgameframework/Gameplay.Triggers.Config/`
- `Packages/com.vincent.wgameframework/Gameplay.Triggers.CombatBridge/`
