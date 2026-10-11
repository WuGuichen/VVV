# CharacterTest 关卡编辑工作流

2026-10-11 保存修复：Genesis／Guidance 草稿记录加载文档路径、关卡／Scene 身份和来源 hash。重新打开窗口或切换文档时，clean 模块重新绑定当前文档并退役旧文档 Undo；脏草稿不能被自动切换或重载到其他关卡。World／Characters 同样拒绝跨文档自动丢弃脏草稿。统一保存准备阶段核对缓存身份与当前场景，并拒绝来源已改动／移除；错误显示模块名与实际文档路径。域重载恢复载体保留原来源 hash，恢复不会把外部修改重新当成保存基线。此修复不跳过创世校验，不清空验证站，也不通过保存旧缓存覆盖当前关卡。

> Status: Current
> 状态：Main implemented and validated on 2026-10-10. Unity refresh/compile/domain reload passed, Console errors 0. 46 distinct focused EditMode tests passed. Actual New → AI group selection → Save/Reload → Play ID/hash → exit restore passed.
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/Editor/Levels/CharacterTestLevelEditor*.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/Editor/Levels/CharacterTestJoinedLevelSaveContext.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/Editor/CharacterTestSceneCharacterLevelEditorModule.LevelBindings.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/Editor/CharacterTestSceneCharacterLevelEditorModule.BehaviorGroups.cs`

Unity 菜单 **MxFramework → Level Editor** 打开 Mx 关卡编辑器。顶部关卡选择器统一各模块的 Authoring 文档；切换前可以保存、丢弃或取消。新建/复制需要已保存的目标 Scene，且目标有唯一 GameManager 和托管根。新建为空角色列表，先添加角色、选择玩家/对手再保存；复制保留正式行为组/卡引用，关卡实例使用新的稳定身份。同 Scene 的关卡共享环境地形，初始角色位置由各关卡 SpawnPose 决定。默认角色创建保留当前新建关卡 ID 和 Scene，不回写示例关卡身份。

在“场景角色”模块用列表或 SceneView 选择角色，设置角色定义、控制 owner、队伍和出生姿态。列表支持多选，批量控制 owner/队伍/启用状态在同一个 Undo 提交。正式组选择器绑定精确版本，并显示卡名、用途和动作槽。选择、复制不会猜测缺失动作槽或自动升级旧版本。关卡规则面板使用现有战术阶段的预算、时间倍率和玩家/对手字段，不另造规则模型。

角色 Inspector 的“准备可试玩战术组”通过现有目录 authoring owner 显式创建一个独立正式组；已有符合预设的组则复用，不在绘制时创建或自动绑定。当前正式组 `user.behavior-group.8df26e3126bec2b @1` 复用单手剑正式组 @2 的条件/顺序及三张卡：`bc_fe8w87d52r @2`、`bc_7hj69v6kd6 @2`、`bc_3205xjgcw8 @1`。仍需明确选择组版本并填写动作槽。已有正式组/卡版本及其他关卡绑定保持不变。

玩家保留旧组兼容入口。新增 AI 组选择与“复制独立组”使用相同消费能力检查：拒绝旧底层 Recipe、当前关卡未接入的准备效果/位置回返/选点卡，以及尚无统一事务支持的非零基础费用。普通物理动作 CD 仍由 ActionUseAuthority 管理。检查和保存最终通过完整运行时预检裁决。底层模型与上层战术卡两层保留；完整共享费用/CD/效果规则尚未实现，统一组合牌方向由玩法/工程文档专责继续定义。本轮没有扩展独立准备卡 AI 执行架构。

“打开行为组编辑器”目前只打开 Editor Hub，尚未跳到当前精确组 ID/revision。Runtime AI Lab → Play → 队伍设置 → 角色 Edit Group 是现有草稿组编辑入口；Unity 菜单 MxFramework → Character → Runtime AI Planner → 行为组编辑是底层 Profile/行为模块入口，不能泛称可直接编辑上层战术组合组。本轮未验收专用上层组编辑器；当前关卡支持选择精确上层组版本、填写动作槽和复制已支持组，编辑上层卡精确发布引用、条件、优先级和顺序的完整入口仍待补齐。

保存收集当前活跃模块的完整联合候选，预检同一个候选后写入；finalizer 根据最终已存载体的相同字节重建缓存，再由现有 coordinator 保存 Scene。只读验证、规则面板读取和缓存检查使用私有投影，保留已存 root 引用和发布 hash 字符串表示。保存/重载后重复读取仍同步，真实编辑仍显示待应用。写入或预检失败保留草稿，并在协调器文件/对象回滚边界内处理。

“检查”验证当前草稿。“试玩”先保存、检查，再用固定 preview token 发布并启动当前 level ID；拒绝失败后偷偷加载旧版本，回到 Edit 时恢复原 InitialLevelId。实际 UI 新建关卡、添加 AI、选择上述真实战术组、保存/重载、Play ID/hash 和退出恢复已验证，发布 hash 为 `b60349dffd4000e675e4639e4723b657072250074d88f72fd553079d400be13f`。测试关卡与运行发布条目均已清理。

诊断可定位已支持的世界对象/角色列表、Scene 和 Inspector。指导 steps[index] 与创世 stations[index] 可定位条目稳定 ID，常用字段及 completion 键支持聚焦；其他嵌套路径明确报告未支持自动字段聚焦并保留原文档入口，不宣称全面覆盖。丢弃仅恢复当前关卡登记的 Transform/登记对象增删；未登记 Scene 草稿不会被覆盖。登记对象下有未登记后代时，丢弃先拒绝并保留全部草稿。文档重绑退役旧 Undo，避免 A 的旧回执写入 B；Editor 域重载恢复保留同一 Editor 会话、同一已加载 Scene 生命周期中的草稿；不承诺进程重启存档。

验收覆盖 46 个不同 focused EditMode 方法：31 个原生场景/保存方法与 15 个纯逻辑方法。证据是初次 46 项运行中其余 37 项通过，加修正界面刷新夹具后的完整 9 项闭环通过，保留两份真实 NUnit XML。图形证据为实际 GameView 截图；本轮 Editor 截图被前台 Codex 遮挡，未算作图形验证。实际控件操作与原生断言验证保存/重载缓存和组选择。Scene、ProjectSettings、水面源码、恢复目录、Lab 草稿与持久文件均按交接基线核查，唯一保留的数据变化为上述新增正式组。


2026-10-10 补充：脚本／程序集重载前，当前完整联合 Authoring 草稿、关卡／Scene 身份、源 hash、各模块 dirty 与保存基线，以及已登记 Scene 对象的范围基线写入 Editor SessionState。重载后恢复这些状态；恢复基线按原生 Scene／对象身份重绑，深层对象树使用扁平 DTO，避免递归序列化截断。源文件已改动或丢失时保留恢复稿并拒绝覆盖；显式丢弃才接受同身份的新源。身份或已加载 Scene 生命周期不匹配时保留记录并拒绝自动恢复。没有保存所有 Scenes，也没有扩展 AI 执行架构。

补充验收是一轮独立的 19/19 原生 EditMode 运行（原有闭环 9、范围恢复 6、新恢复 4），以及三种真实 Editor 域重载后的规则／角色组绑定／dirty／源 hash／范围基线核查：保存、丢弃和源冲突拒写均通过，另一 Scene 的未保存修改及其磁盘文件保持不变。R8 的 46 个不同方法通过记录仍保留；不将这些分批证据宣称为一次 50/50 运行。验证仅覆盖同一 Editor 会话，没有验证进程重启；专用上层组编辑器和全部嵌套字段聚焦仍待完成。
