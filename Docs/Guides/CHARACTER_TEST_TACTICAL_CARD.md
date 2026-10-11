# CharacterTest 单段战术行为卡

> Status: Guide
>
> Runtime Slice 已验证，逐项证据见[任务 03 执行记录](../../StorySource/玩法基石/任务/任务03执行记录.md)。
>
> 合同：[任务 03](../../StorySource/玩法基石/任务/任务03_战术行为卡表达模型设计与首卡编译验证.md)、[已确认首卡设计](../../StorySource/玩法基石/任务/任务03_T3-1_战术行为卡表达模型与首卡设计.md)。

> 2026-10-01 术语说明：统一命名与数据关系以[《角色动作与战术行为卡》第零章](../../StorySource/玩法基石/角色动作与战术行为卡.md)为准。本指南保留任务 03 的实际类型名及 version 1 验证合同：留槽位的源是模板语境，按实际装配确定动作后形成可用卡配置，编译时间轴是执行产物；它们不等于持有的卡牌实例。本次术语定稿不改该历史源格式、LightAttack 解析、执行行为或存档，也不宣称已经实现独立模板／卡牌实例／卡组对象迁移。当前制作工具另见[作者指南](CHARACTER_TEST_TACTICAL_CARD_AUTHORING.md)。

## 制作与编译入口

源示例是[任务03首卡源示例.json](../../StorySource/玩法基石/任务/任务03首卡源示例.json)。它属于本任务独立制作输入，尚未加入既有正式行为卡目录或发布到行为组。修改它不会更改当前已发布卡的 revision。

Unity EditMode 菜单：

- `MxFramework → CharacterTest → Tactical Card → Compile Default Loadout`：读取示例源，使用默认验证装配及已保存的任务 01 摘要编译；缺失/失效时明确拒绝，不隐式重测。
- `MxFramework → CharacterTest → Tactical Card → Measure and Compile Default Loadout`：明确重新测量默认验证动作，再读取源并编译。

这些入口使用无视图的正式默认装配验证宿主，不表示编辑器当前选中角色或场景里的任意角色。编译报告写到 `Library/Task03ExecutionEvidence/default-loadout-compilation.txt`，包含固定动作、事件区间、来源映射及相关指纹；编译报告不能替代运行证据。

## 源格式 version 1

源只表达完整行为，不填写具体动作 ID、测量距离或帧偏置。所有字段必需；未知字段、重复字段、类型错误、未知语义值及非标准 JSON 语法明确拒绝。

| 字段 | 支持值 | 含义 |
| --- | --- | --- |
| `version` | 数字 `1` | 源格式版本 |
| `name` | 非空字符串 | 整卡名称；不改变执行身份 |
| `behavior` | `singlePrimaryAttack` | 单次普攻起手机会 |
| `target` | `currentCombatTarget` | 使用层当前战斗目标，不建立独立目标锁 |
| `slot` | `lightAttackOpener` | 当前实际 ActionSet 的 LightAttack 序列在无活动上下文中的正式起手解析 |
| `approach` | `optimalRange` / `none` | 接近动作最佳距离 / 不提供本卡基础接近 |
| `facing` | `entireCard` | 独立转向覆盖整卡，受动作倍率约束 |
| `eligibility` | `gateAndActionEntry` | Gate 与实际起手帧分别查询绑定动作正式机会 |
| `completion` | `fullActionDuration` | 覆盖完整动作时长，不以取消窗口提前结束 |
| `exit` | `none` | 不生成死亡退出或追击超时守卫 |

`approach=none` 不阻止其他控制源或动作 Root Motion；它仍允许转向，仍等待距离、朝向和机会，因此目标不进入合适位置时可以一直等待。源格式首版不是通用单元组合或脚本语言，不支持向其中添加未确认的其他选择。

## 编译和启动有效性

`CharacterTestTacticalCardRuntimeInputs` 每次低频准备从 Runtime 当前实体和 providers 重新取得装配、动作及有效摘要。源实体恢复/重建后不沿用旧 entity 引用。正式 LightAttack 解析失败、缺序列、序列/binding 冲突、摘要缺失或失效均失败，不回退默认动作。

`CharacterTestTacticalCardCompiler` 直接向任务 02 的 `CharacterTestBehaviorExecutionBuilder` 展开，得到只读执行产物和独立来源映射。准备后的原动作内容若被修改，旧 Inputs 在编译时明确拒绝；成功编译不改写作者输入，也不保存旧 Stage/Recipe JSON 作为执行中转。

`CharacterTestTacticalCardBinding` 在编译、启动及恢复边界核对当前相关身份。执行中禁止重编译；B 已生效但编译失败时可以保留 A 产物，不能再用 A 启动。编译失败不修改实际装配，恢复快照也不能凭旧有效标志绕过当前检查。上述文件读取、Hash 和槽解析不进入逐帧执行。

## 正式运行接入

`CharacterTestTacticalCardSession` 是使用层适配，不拥有另一个执行器、时钟或命令队列。它把经过 `Binding.TryAcquire` 验证的产物交给任务 02 的 `CharacterTestBehaviorExecutionHost`，后者使用共用 Evaluator、RuntimeBridge、动作 Gateway 和正式控制意图。

调用方为当前角色构建 `RuntimeInputs → Binding → Session`，将现有 `CharacterTestRuntimeCommandModule.Enqueue` 作为命令提交入口。在配置边界调用 `TryCompile`，使用卡时调用 `TryStart(source,facts,frame)`；在既有固定帧流程中先采样 `Session.Tick`，再由正式命令阶段和角色 Simulation 消费。Session 不直接写 Transform、启动动作或结算伤害。

执行实体必须已由使用层配置为正式命令驱动角色（LocalPlayer、RemotePlayer 或 AiControlled）；Session 不会替使用层抢占控制权。不接受控制命令的惰性测量角色仍可以准备数据/编译，但不能直接作为运行角色启动。

`Stop(facts,frame)` 使用当前帧请求清理。动作终止可能等待命令消费和回执，不能在调用 Stop 后立即认为绑定已释放；以 `IsExecuting=false` 和 Host 终态为准。输入事实暂时缺失时，已有移动/转向输入失败关闭，不隐式新增死亡或超时退卡规则。

`CaptureState` 可保存已编译待用、Gate 阻滞或活动执行的卡状态。未编译前无可保存绑定；已编译但没有 Host 的快照可恢复为待用状态而不发命令。`TryRestore` 必须重新验证当前装配/摘要；若含活动执行状态，调用方先通过既有世界恢复机制回到对应权威帧，再恢复卡状态。Session 不负责替代世界存档/恢复。`AddHash` 只纳入绑定和执行权威，显示说明不参与。

当前混合切片的 Continue 只恢复战斗外 checkpoint，不调用这里的活动执行快照恢复。`CaptureState` / `TryRestore` 是单卡 Session 接口，恢复活动卡仍要求调用方先恢复对应世界权威帧。混合切片外层 Planning 可暂停整个世界，与下文单卡 Gate 只阻滞游标的语义分别成立；操作入口见[当前 Demo 走查](../Walkthroughs/TACTICAL_CARD_PLAYABLE_DEMO_WALKTHROUGH.md#入口与操作)。

## 支持范围与证据口径

首版受支持动作是 BasicAttack/direct-hit：单段从 0 起的 Startup、紧接单段 Active，可选紧接并到时长末尾的 Recovery，总时长有限且与当前固定帧口径一致。命中窗口必须落在 Active 内。空间摘要要求单一封闭连续区间、有效推荐点、中心为 0 且容差为正的扇区；不支持的形状明确拒绝，不能把分段或开放范围伪造为单一区间。

Fallback 朝向容差明确保持 Fallback 身份，不是实测角度覆盖。标准靶摘要只供规划及候选筛选，正式机会使用本次实际目标与绑定动作；传统 Planner 普攻的缓存布尔值不能代替这条查询。

Gate 固定在卡位置 0，动作在位置 1。动作前摇本地 0～17 时，移动事件为 `[0,19)`；总时长 76 时动作为 `[1,77)`、转向为 `[0,77)`，卡在 77 自然结束。Gate 阻滞影响游标，不暂停世界模拟。动作首帧资格失败会消耗本次机会，不补播或回跳；卡完成不等于攻击命中。

双装配测试明确使用巨型偏移参考靶夹具（版本 2、高 5000mm、半径 2200mm、中心 (0,2500,-4000)mm），以同一几何覆盖真实 301 和 940000 的不同攻击轨迹，并验证正下界下的后退。该靶的测量区间不能作为默认标准人形靶射程对外解释。具体测量和运行结果分别记录，不能互相替代。

本机 `Library/` 的摘要和报告是派生缓存，不作为正式作者资产提交。需要重建时从正式动作与本指南指定的入口重新测量/编译。
