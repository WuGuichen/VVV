# CharacterTest 关卡编辑工作流

2026-10-13 结算提示同步：TacticalCardSliceCoordinator 在结算成功保存后按实际结果显示“平局”或获胜阵营；保存失败继续显示 ProgressIssue 或保存异常诊断，不把失败写成胜负结果。结算、奖励与保存权威仍归[玩家阶段流程](../Tasks/TACTICAL_SLICE_PLAYER_FLOW.md)，本次仅同步现有提示行为，不追加原生验收声明。

2026-10-11 / Issue #762：对象 Definition 普通 Undo/Redo 恢复可解析且格式/schema 匹配的完整编辑快照，名称为空、stable ID 暂时非法等语义未完成状态仍保留，并重新执行原编译/诊断。语义校验失败继续阻止保存，不再阻止历史恢复；撤销/重做同一草稿中的其它字段也必须同步真实内容、Undo snapshot 与 dirty baseline。损坏 JSON、null 或错误格式/schema 明确拒绝且不覆盖当前草稿。该修复只调整普通快照恢复，不改变既有域重载载体、原来源 hash 或三文件事务回滚。原生回归入口为 `CharacterTestDefinitionDraftUndoNativeTests`；实际候选和运行证据维护在 Issue #762，测试定义不代表已运行通过。

2026-10-11 保存修复：Genesis／Guidance 草稿记录加载文档路径、关卡／Scene 身份和来源 hash。重新打开窗口或切换文档时，clean 模块重新绑定当前文档并退役旧文档 Undo；脏草稿不能被自动切换或重载到其他关卡。World／Characters 同样拒绝跨文档自动丢弃脏草稿。统一保存准备阶段核对缓存身份与当前场景，并拒绝来源已改动／移除；错误显示模块名与实际文档路径。域重载恢复载体保留原来源 hash，恢复不会把外部修改重新当成保存基线。此修复不跳过创世校验，不清空验证站，也不通过保存旧缓存覆盖当前关卡。

2026-10-11 修复已实现（Unity 编译、七项正式原生回归及真实重载/Undo/资产回滚验证通过）：对象定义草稿单独保存于共享定义源。World 参与者在定义 dirty 时登记定义源、generated runtime 与 Player mirror 三个写入路径，使首次 Scene commit 前的写入失败按同一事务恢复。定义恢复载体包含草稿、原来源 hash、dirty 与定义 Undo baseline；可解析但语义未完成的草稿仍保留，来源改动或移除时拒绝覆盖，显式重载才接受新来源。旧恢复记录没有定义字段时沿用磁盘定义。尚未 prepare 的 Guidance 收到其他参与者的失败通知时保留当前 dirty 与 Undo baseline。

> Status: Current
> 文档版本：2026-10-11
> 状态：Main implemented and validated on 2026-10-10. Unity refresh/compile/domain reload passed, Console errors 0. 46 distinct focused EditMode tests passed. Actual New → AI group selection → Save/Reload → Play ID/hash → exit restore passed.
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/Editor/Levels/CharacterTestLevelEditor*.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/Editor/Levels/CharacterTestJoinedLevelSaveContext.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/Editor/CharacterTestSceneCharacterLevelEditorModule.LevelBindings.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/Editor/CharacterTestSceneCharacterLevelEditorModule.BehaviorGroups.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Game/CharacterTest/CharacterTestLevelEditorDocumentFactoryTests.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/Composition/CharacterTestRuntimeSessionBuilder.Levels.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/TacticalCard/TacticalCardSliceCoordinator.Loading.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/TacticalCard/TacticalCardSliceCoordinator.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/PlayMode/CharacterTestLevelStartupNativeTests.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/Editor/CharacterTestWorldObjectLevelEditorModule.Definitions.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Game/CharacterTest/CharacterTestWorldObjectDefinitionAuthoringTests.cs`

复制的重编号按模块拥有的字段执行：角色、World 点／区域／实例、Genesis 组／站点、Guidance 步骤和关卡 Trigger 实例获得新身份；对应战术角色、Genesis bindings 和 Trigger 区域引用同步重映射。正式行为组、卡、entry、action、resource 及未声明的扩展字符串保持精确值，即使与实例 ID 同名也不替换。新增模块实例身份或引用须在 document factory 声明字段路径并加入回归。复制仍深拷贝源文档，不更改已发布目录或正式关卡模板；focused 回归包含真实正式组碰撞的复制／保存／重载／精确组解析。

Unity 菜单 **MxFramework → Level Editor** 打开 Mx 关卡编辑器。顶部关卡选择器统一各模块的 Authoring 文档；切换前可以保存、丢弃或取消。新建/复制需要已保存的目标 Scene，且目标有唯一 GameManager 和托管根。新建为空角色列表，先添加角色、选择玩家/对手再保存；复制保留正式行为组/卡引用，关卡实例使用新的稳定身份。同 Scene 的关卡共享环境地形，初始角色位置由各关卡 SpawnPose 决定。默认角色创建保留当前新建关卡 ID 和 Scene，不回写示例关卡身份。

在“场景角色”模块用列表或 SceneView 选择角色，设置角色定义、控制 owner、队伍和出生姿态。列表支持多选，批量控制 owner/队伍/启用状态在同一个 Undo 提交。正式组选择器绑定精确版本，并显示卡名、用途和动作槽。选择、复制不会猜测缺失动作槽或自动升级旧版本。关卡规则面板使用现有战术阶段的预算、时间倍率和玩家/对手字段，不另造规则模型。

角色 Inspector 的“准备可试玩战术组”通过现有目录 authoring owner 显式创建一个独立正式组；已有符合预设的组则复用，不在绘制时创建或自动绑定。当前正式组 `user.behavior-group.8df26e3126bec2b @1` 复用单手剑正式组 @2 的条件/顺序及三张卡：`bc_fe8w87d52r @2`、`bc_7hj69v6kd6 @2`、`bc_3205xjgcw8 @1`。仍需明确选择组版本并填写动作槽。已有正式组/卡版本及其他关卡绑定保持不变。

玩家保留旧组兼容入口。新增 AI 组选择与“复制独立组”使用相同消费能力检查：拒绝旧底层 Recipe、当前关卡未接入的准备效果/位置回返/选点卡，以及尚无统一事务支持的非零基础费用。普通物理动作 CD 仍由 ActionUseAuthority 管理。检查和保存最终通过完整运行时预检裁决。底层模型与上层战术卡两层保留；完整共享费用/CD/效果规则尚未实现，统一组合牌方向由玩法/工程文档专责继续定义。本轮没有扩展独立准备卡 AI 执行架构。

“打开行为组编辑器”目前只打开 Editor Hub，尚未跳到当前精确组 ID/revision。Runtime AI Lab → Play → 队伍设置 → 角色 Edit Group 是现有草稿组编辑入口；Unity 菜单 MxFramework → Character → Runtime AI Planner → 行为组编辑是底层 Profile/行为模块入口，不能泛称可直接编辑上层战术组合组。本轮未验收专用上层组编辑器；当前关卡支持选择精确上层组版本、填写动作槽和复制已支持组，编辑上层卡精确发布引用、条件、优先级和顺序的完整入口仍待补齐。

保存收集当前活跃模块的完整联合候选，预检同一个候选后写入；finalizer 根据最终已存载体的相同字节重建缓存，再由现有 coordinator 保存 Scene。只读验证、规则面板读取和缓存检查使用私有投影，保留已存 root 引用和发布 hash 字符串表示。保存/重载后重复读取仍同步，真实编辑仍显示待应用。写入或预检失败保留草稿，并在协调器文件/对象回滚边界内处理。

“检查”验证当前草稿。“试玩”先保存、检查，再用固定 preview token 发布并启动当前 level ID；拒绝失败后偷偷加载旧版本，回到 Edit 时恢复原 InitialLevelId。实际 UI 新建关卡、添加 AI、选择上述真实战术组、保存/重载、Play ID/hash 和退出恢复已验证，发布 hash 为 `b60349dffd4000e675e4639e4723b657072250074d88f72fd553079d400be13f`。测试关卡与运行发布条目均已清理。

2026-10-11，Issue #749 已实现并完成本机自验，待原检查 owner 独立验收：成功发布关卡后，SessionBuilder 将同一 Slice、冻结部署和 StageRunId 交给 TacticalCardSliceCoordinator，战术关卡直接进入 Battle。关卡准备阶段已经安装的精确行为组、条件、顺序和动作绑定沿用，Coordinator 不再次执行普通自动装配。首页进度异步加载只拥有开始时的 RuntimeContext、Slice 和 loading attempt；关卡发布退役旧 attempt，迟到的成功、失败或取消均不能发布旧整备或打开首页。新一次进度加载也不能按人数覆盖已发布关卡，三对二和二对二都按关卡来源处理。当前进度加载失败保留关卡并显示加载错误，重试仍保留关卡；所选关卡尚未成功发布时显式报告，不能转入普通整备。

关卡 battle 的 HUD／胜负由现有 mixed runtime 提供，Coordinator 消费其终局并走现有结算保存流程；关卡实例没有玩家物品归属时不生成物品奖励。完成后重赛重新加载该关卡、分配新的 StageRunId，再安装该关卡的精确组；准备失败保留结算会话，已发布后界面失败则走原 presentation fault 暂停恢复，不能误报旧会话仍在。明确返回整备才切回玩家进度。空 InitialLevelId 的首页、Continue、新进度、待领取奖励及保存失败阻断继续沿原流程。

本轮运行基线复现：真实 Level Editor“试玩”按钮将 InitialLevelId 设置为 `formal.sword.crossfire`，两实体隔离进度加载完成后 ActiveLevelId/hash 清空、mixed 模块消失并回到 Home；采样时普通世界已被首页暂停，角色尚未生成（数量 0）。退出恢复原 InitialLevelId 已观察。原生回归入口为 `CharacterTestLevelStartupNativeTests`，使用正式 Scene／关卡、生产编译／发布器和进度服务，存档仅写独立验证目录；夹具显式发布当前动作指纹，结束后恢复原发布目录并移除本轮临时文档。整合至 `d49f915a` 基线后的 PlayMode 作业 `b1fd16c2f36a4df3add5d64bc6acfa9c` 为 6/6，通过两种人数、迟到成功／失败／取消、当前失败重试、普通入口、选关失败、HUD 命令、mixed 终局及关卡重赛；保留实际 NUnit XML。终局以属性权威设置敌方 HP 验证交接，不代表自然击杀视觉验收。既有 `TacticalLoadingFlowTests`／`TacticalBattleRewardTests` 的 EditMode 作业 `d4b2fc46c6f146ca92153b0e03744012` 为 42/42，通过回调失效、待领奖恢复与保存失败阻断等原合同；两种模式分开记录。最终重赛故障处理及固定“主动接敌”入口的补验作业 `c329b7a4454e4654b5a604b9409fb34b` 为 1/1，通过出牌、mixed 终局、结算保存和同关卡新 run；夹具等待现有世界暂停完成，不修改牌的激活规则。

实际修复后“试玩”按钮再次启动 crossfire：两实体隔离进度打开完成后，三名角色、Battle／Planning、三张手牌、Running 健康状态及发布 ID/hash 均保留。通过真实 HUD 指令入口选择并确认“单手剑 · 主动接敌”，完成后回 Planning、TurnIndex 由 1 到 2，同一 Slice／ID/hash 仍在且 ExecutionIssue 为空；已检查实际 GameView PNG。未做物理鼠标拖放验收。退出后原 InitialLevelId、原 Lab Scene、进程存档路径及正式 Scene／关卡／发布目录字节均恢复。整合后编译作业 `eb97ad3ddd494c0da3558a7070b06d99` 的统一工具超时后，分步核对完整证据并另读 Console error 0；最终重赛故障处理候选的作业 `fb127428c8f34dd6b9a7266bf2f95c4b` 经同一工具状态读回明确通过 refresh、编译开始／结束、domain reload、Editor ready 与 Console error 0。未执行 Player 构建、其他平台或全量测试；Issue 尚待独立复核，不以本机自验自动关闭。

诊断可定位已支持的世界对象/角色列表、Scene 和 Inspector。指导 steps[index] 与创世 stations[index] 可定位条目稳定 ID，常用字段及 completion 键支持聚焦；其他嵌套路径明确报告未支持自动字段聚焦并保留原文档入口，不宣称全面覆盖。丢弃仅恢复当前关卡登记的 Transform/登记对象增删；未登记 Scene 草稿不会被覆盖。登记对象下有未登记后代时，丢弃先拒绝并保留全部草稿。文档重绑退役旧 Undo，避免 A 的旧回执写入 B；Editor 域重载恢复保留同一 Editor 会话、同一已加载 Scene 生命周期中的草稿；不承诺进程重启存档。

验收覆盖 46 个不同 focused EditMode 方法：31 个原生场景/保存方法与 15 个纯逻辑方法。证据是初次 46 项运行中其余 37 项通过，加修正界面刷新夹具后的完整 9 项闭环通过，保留两份真实 NUnit XML。图形证据为实际 GameView 截图；本轮 Editor 截图被前台 Codex 遮挡，未算作图形验证。实际控件操作与原生断言验证保存/重载缓存和组选择。Scene、ProjectSettings、水面源码、恢复目录、Lab 草稿与持久文件均按交接基线核查，唯一保留的数据变化为上述新增正式组。


2026-10-10 补充：脚本／程序集重载前，当前完整联合 Authoring 草稿、关卡／Scene 身份、源 hash、各模块 dirty 与保存基线，以及已登记 Scene 对象的范围基线写入 Editor SessionState。重载后恢复这些状态；恢复基线按原生 Scene／对象身份重绑，深层对象树使用扁平 DTO，避免递归序列化截断。源文件已改动或丢失时保留恢复稿并拒绝覆盖；显式丢弃才接受同身份的新源。身份或已加载 Scene 生命周期不匹配时保留记录并拒绝自动恢复。没有保存所有 Scenes，也没有扩展 AI 执行架构。

补充验收是一轮独立的 19/19 原生 EditMode 运行（原有闭环 9、范围恢复 6、新恢复 4），以及三种真实 Editor 域重载后的规则／角色组绑定／dirty／源 hash／范围基线核查：保存、丢弃和源冲突拒写均通过，另一 Scene 的未保存修改及其磁盘文件保持不变。R8 的 46 个不同方法通过记录仍保留；不将这些分批证据宣称为一次 50/50 运行。验证仅覆盖同一 Editor 会话，没有验证进程重启；专用上层组编辑器和全部嵌套字段聚焦仍待完成。
