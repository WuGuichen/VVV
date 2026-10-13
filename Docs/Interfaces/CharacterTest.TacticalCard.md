# CharacterTest 半即时卡牌核心

> Status: Current
> Version: 0.9 | 当前流程核对：2026-10-12；前三批限定交付／ACK复验已同步，下方历史验收保留原日期
> 2026-10-05 当前源码：混合切片把卡牌费用账本接入角色动势，冷却复用实际动作的跨卡权威；HP归零后 IsAlive 为 false，破聚不等于死亡。玩法权威仍见[角色与卡组底层数据设计](../../StorySource/玩法基石/角色与卡组底层数据设计.md)。历史账本、旧卡定义及验收记录按原日期读取，不构成当前默认玩法或本次新增运行证据。
> 2026-09-11 Task 1–3、T4-A/B/C/D 是历史 Runtime Slice 分阶段记录；场景、HUD、混合切片及战斗外 checkpoint 此后已进入主树。完整自然游玩验收仍需对应实际回执，不能由“已主合”推定。
>
> 最新玩法：所有打出的牌启动行动，原准备等非行动卡改为随主卡统一出牌的配件；固定预装组合牌与随机双牌库装配均须实现。唯一玩法权威是[主卡与配件基石v0.6](../../StorySource/玩法基石/行动卡与交互效果卡设计基石.md)。固定组合已进入下述联合入口；完整工程目标、五卡迁移与最新限定交付归[开发方案3.10](../Plans/TacticalCardAssemblyDevelopmentPlan.md#310-前三批主工程限定交付2026-10-12)。下文旧Action／Utility、独立准备确认、单Deck与旧试用数值按历史范围读取，不构成新玩法。

> 最新主工程交付：前三批Runtime、正式九卡JSON/meta与UI已实装，连续第二回合ACK修复已由原生与真实可见序列复验；准确封存、编译timed_out、暂停／到期／重入边界见[九配件交付说明](../Tasks/TACTICAL_ACCESSORY_NINE_DELIVERY.md#latest-main-accepted-delivery)。Git未提交推送，#792冷准备未解决、第四批暂停；Geometry仅文档与首步准备。旧段落按其历史窗口读取，不把旧待验表述继续当成当前阻断。

候选效果能力的源码与接线边界见[借力、推离、破势说明](../Tasks/TACTICAL_ACCESSORY_COMBAT_CANDIDATES.md)。三候选已进入固定组合联合入口；GameView已有推离和实际扣费证据，借力／破势的图形触发及正式内容发布尚未完成；牵连、留痕仍有明确缺口。

2026-10-12第一批早期冻结记录（最新限定交付见上）：双刃配件（决斗邀请／破碎幻想／黑洞口袋）已接固定组合付款、真实ACK、窗口/弹体出生来源、伤害倍率、实例Exhaust、Motion、阶段snapshot/hash与可信暂停副本，并增加待发布配置及有限旧库存迁移。规则158项和生产核心离线验证见[实施交接](../Tasks/TACTICAL_DOUBLE_EDGE_ACCESSORY_IMPLEMENTATION.md)。协调者已通过隔离编译、实际入场、两种真实停止ACK清理和启动保存/CAS部分原生用例；全新阶段整回合暴露的自然终态接口转发缺口已修复，待原生复验，发布和游玩尚未验收。随机双库和自动AI联合owner未由本批实现。

第一批整审修复：Driver 异常/超时通过既有退出清理通道收到匹配停止ACK后才故障暂停，恰好一次释放预算并兑现Exhaust，不补牌、不加回合、不退费；已放弃执行须阶段重启，不能恢复旧结算收据。启动组合根显式记录库存目录迁移，已有源进度也须先整档CAS保存，再发布服务并启用自动保存；失败或过期写入者不得发布迁移候选。原57项生产测试为新增5项与既有52项；修复回归及待原生执行的真实启动三例另有冻结证据，不覆盖原回执。

清理观测合同与原freeze68／70历史检查点（后续复验见最新交付）：匹配执行的 terminal、owned-intents-released、commands-acknowledged 缺一不可。停止收据已就绪时 `ReleaseControl` 可同步完成全部清理，不能要求外部必先看到 `HasReleasedCleanup=true`。freeze68原生13/14通过，真实同步/异步清理完整终态及旧阶段拒绝再准入均通过，全新阶段整回合仍失败。实际 Driver 收到的是 Stage 包装，Mixed/单卡两层均须实现 `ITacticalFixedTerminalPort` 并转发精确 execution ID 的 adapter 自然收据，不能只有 adapter 实现。当前补齐转发后离线70项通过，原68项及逐项断言数保留，换回旧Runtime恰好两个接口回归失败；真实完整回合、自然收据、ACK、Exhaust与场生成仍由协调者复验。原始失败与各轮冻结均保留。

入场回归修复及当时编译记录（不补签全部入场用例）：清理协调只在 Suspended 且持有 ExecutionFault 或 RecoveryTimeout 原因时同步故障健康状态；新 Stage 未初始化的 Suspended 和入场菜单暂停均不能触发故障。未预先 Initialize 的真实 Story/Spawn/settle 入场两例通过，离线生产回归当前64项；实际 MixedStage 入场原生用例仅完成源码编译，交由统一协调者执行。证据与剩余验收见上方任务文档。

命名空间：`MxFramework.Game.CharacterTest.TacticalCard`。源码属于已有 `MxFramework.Game.CharacterTest` 程序集；测试属于已有 `MxFramework.Tests`，不增加程序集依赖。纯规则不引用 UnityEngine；Unity 适配器允许 UnityEngine，所有 Runtime 源码禁止 UnityEditor。

> 2026-10-11固定组合接线已进入当前工作树：Driver／Deck／Time／detached Host联合准备、实际World账本付款与配件journal、Derived命令身份及既有收据去重、原Ability动作费用／CD、终态清理ACK后释放预算，范围与证据归[开发方案3.8](../Plans/TacticalCardAssemblyDevelopmentPlan.md#38-固定组合真实接线检查点2026-10-11工作树已合入可玩验收未完成)。AI Lab已有显式测试装配配置及现有发布控件绑定；正式配件发布、库存UI、图形可玩、随机双库仍未验收。下文P1及旧prepare说明按其历史范围阅读。

> 2026-10-11历史检查点：实际Unity EditMode Test Runner **16/16**，含只读费用投影、正预算退出／死亡、第二动作取消／中断、真实已付款private owner恢复。费用显示沿同一冻结合同，确认重新证明；执行后读实际journal／预留／消费。退出用独立owner清理接缝，精确ACK后释放预算，不产生正常完成或奖励；动作实例池化重用时凭revision及原action／instance／frame的Timeline终态判定，缺证据拒绝。GameView实际付3、动势50→47并触发推离，Planning往返无重复付款；1280×720全文过小，执行中暂停、借力／破势、图形恢复和自然鼠标输入未验收，布局候选尚未合入。源码／作业／保护边界归[开发方案3.9](../Plans/TacticalCardAssemblyDevelopmentPlan.md#39-固定组合原生修复与图形检查点2026-10-11部分验收)，19项历史fixture直接调用不改称Runner。

> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/TacticalCard/*.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/*FixedAssembly*.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestTacticalCard*.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestBehaviorExecutionHost.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay.RuntimeBridge*.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestRuntimeAiLabCardTrialConfiguration.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/UI/FairyGui/CharacterTestFairyGuiAiLabCardTrialSurface*.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/Character/CharacterTest*Fixed*.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/Character/CharacterTestAbilityCostAuthority.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/Character/CharacterTestActionUseAuthority.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/Character/CharacterTestCharacterRuntimeModule*.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Commands/CharacterTestRuntimeCommandRegistry.cs`

## 主卡／配件持有与装配数据基础（2026-10-10）

本节保留P1当日API与证据范围；2026-10-11奖励owner的后续授予／保存接缝改动不由本节签收，不能把下述历史“无奖励入口”读成更新后全工作树结论。固定组合最新证据及49b79397后的隔离准备归开发方案3.9。

`CharacterTacticalPlayerService`仍是唯一持有owner。[CardAssembly扩展](../../Assets/Scripts/WGameFrameworkProject/CharacterTest/TacticalCard/CharacterTacticalPlayerService.CardAssembly.cs)提供`GrantContentAtTick`四类单项显式授予、`TryReadOwnedContent`detached查询；主卡归真实角色卡实例，动作仍是角色实例学习记录，角色／配件归现有Items allocator和容器owner。没有批量授予、发奖抽选或领取UI接口。各类具体授予复用同一个Items获取账本，重复同载荷返回原身份／AlreadyCommitted，复用请求ID改载荷或类别拒绝；失败候选不发布或消耗计数器。

[装配配置与规则](../../Assets/Scripts/WGameFrameworkProject/CharacterTest/TacticalCard/CharacterTacticalCardAssembly.cs)为显式opt-in：主卡精确发布引用及银／金／彩1／2／3槽；配件精确版本、Items定义ID和作者固定触发／失败／生命周期合同标识。配置hash由定义内容稳定排序计算；玩家选引用，不能覆盖合同。当前行为主卡子集未迁移零动作槽机制卡或五张准备卡，合同标识也尚无效果执行接线。

`SetCharacterCardAssemblyAtTick`修改具体持有牌实例，校验owner、Simulation帧、角色与player revision、精确引用及容量，经原角色候选一次逻辑发布。`TryFreezeCardAssembly`以同一校验支持固定／随机模式的数据快照，不代表已满足动作、目标、费用、随机抽取或合法出牌条件。空槽可存草案，后续批准的固定组合联合入口允许空槽出牌。同名重复、同副本重复与跨牌共享请求因规则未定明确报告当前不支持；并非正式玩法禁令。当前副本API每次创建一份可寻址实例，作者堆叠上限不强制为1；批量堆叠／转移／消耗未开放。

进度显式扩为`tactical-player.v6`：玩家AccessoryItems，角色可选CardAcquisitions／CardAssemblies；旧v5不能携带新记录。结构恢复与精确配置复核分开，缺失／过期定义拒绝且保留存档；目录刷新、读取或配置不补造持有。组合根加载可选正式`tactical_card_assembly.json`，接受v5／v6；需要配件／装配配置而缺文件时不进入Ready。继续经原ProgressCandidate／Store联合保存和SaveGame CAS，逻辑提交不等于durable保存，失败重试Flush不再授予；不新增战斗会话保存。

离线**112 passed / 0 failed**：新增基础19项＋已有回归93项，含实际SaveGame聚合重开、坏引用拒绝、幂等、保存失败重试和旧writer CAS拒绝。随后独占Unity原生验收：编译门禁`bd4254301ce04cfca44a31b8edbe9549`全部证据通过且Console error count=0，基础fixture **19/19**，隔离正式发布链 **2/2**（原角色学习／绑定／解绑／重开／部署回归，及真实组合根主卡授予／Flush／v6重开）。完整接口、schema、配置、低频分配、作业ID及中间失败／场景恢复证据见[开发方案3.1](../Plans/TacticalCardAssemblyDevelopmentPlan.md#31-p1基础实施与当前接口2026-10-10)。原生探针不含正式配件实例或组合部署；新增目录仍受Items全registry hash门禁，显式兼容迁移未实现。正式配件配置与P2–P5玩法验收待完成，不能据字段或这些基础用例宣称组合牌可玩。

依据：[最小 Demo 任务规划](../../StorySource/玩法基石/半即时动作卡牌最小Demo设计与任务规划.md)、[详细技术契约](../../StorySource/玩法基石/卡牌机制具体逻辑与数据细节_GPT6Astra.md)、[角色出战整备与专属卡组设计](../../StorySource/玩法基石/半即时动作卡牌出战整备与角色卡组设计.md)。Task 1的 **3 张手牌** 是历史切片配置；现行设计明确没有三张固定限制，当前关卡补齐目标、Stage等于3的硬检查与固定HUD槽位属于实现差距，不覆盖[行动卡与交互效果卡设计基石](../../StorySource/玩法基石/行动卡与交互效果卡设计基石.md)。

## 武器类型准入反馈

动作/卡片 Authority 的可用性、预留费用及最终提交共用实体装备类型复核。`CharacterTestActionUseStatus` 追加 `WeaponTypeUnavailable=12`、`WeaponTypeUnclassified=13`，对应 `CardPlayFailure` 及 HUD 反馈；状态定义、包 owner、关闭装备来源的拒绝和待核对动作策略统一见 [武器类型与动作准入](../CHARACTER_WEAPON_CONFIGURATION.md#34-实体武器类型与动作准入)。拒绝必须保留原有卡片实例/账本协议，不提前支付、提交 Ability 或发布 Stage；该候选尚未完成真实 Actor、暂停卡片事务和 Player 验收。

## 当前混合切片流程与存档（2026-10-05）

当前正式入口是 `TacticalCardSliceCoordinator`：首页 → 整备 → 校验、冻结并保存部署 → 战斗 → 终局冻结、提交结算 → 重试或返回。整备只允许当前角色持有的卡与动作；刷新正式目录不授予卡。详细操作与实际默认清单见[Demo 走查](../Walkthroughs/TACTICAL_CARD_PLAYABLE_DEMO_WALKTHROUGH.md#入口与操作)。

Planning 暂停世界。玩家存活、处于自由态、无受控原因、无阻塞动作、无待消费命令，且相关装配动作冷却完成后才能选卡；自由攻击收招可继续并随世界暂停冻结，不因进入减速而强行取消。选择预留费用，取消释放，确认后才恢复玩家、双方 AI、世界与动作冷却的共同推进。普通回合只要求行为执行已结束、玩家可以自由行动，即结算并请求暂停；保留未用手牌并补至3张，世界完全停止后进入 Planning。菜单暂停拥有独立原因，关闭菜单不绕过 Ready 暂停。

普通敌目标由混合 runtime 保持当前存活敌人，否则选择最近存活敌人；未发现正式点击敌人选目标的输入链。地点卡单独收集、校验选点输入。2026-10-08 已接入敌方真实当前／已选定待推进卡的只读投影和正式 FairyGUI 明牌组件，源码与发布状态见下节；原生与场景验收仍待回执，真实已选卡不等于未来候选预测。

整份 checkpoint 保存活动 profile 与多个业务 profile 的玩家清单、整备和已提交结算；Continue / 切换进度恢复战斗外状态，新建保留其他进度，不恢复战斗中的 HP、动作、手牌、冷却或世界帧。当前入口不导入退休旧 slot；持久化身份及 schema 见[战术内容指南](../Guides/CHARACTER_TACTICAL_CONTENT.md#存档)。混合终局按全世界存活队伍判定胜负或平局，玩家死亡后其他队伍仍可继续；不能套用下文历史1v1 Story判定。

## 新效果机制与AI Lab接入状态（2026-10-08）

现有共用前置仅包括完整目标尺寸净空、安全停止重定位、导航共用和四项新增测试；原生/离线定向31项通过仅覆盖这些基础及既有回返/牵引/Query-Step，不证明替身受击事务、统一体型owner、尺度消费者或新快照已完成。源码和证据单一入口为[任务09](../../StorySource/玩法基石/任务/任务09_替身准备与体型切换基础.md)。

玩法权威统一见[主基石v0.6](../../StorySource/玩法基石/行动卡与交互效果卡设计基石.md)：主卡与配件仅上节基础已实施，执行迁移待完成；体型到期安全还原及可能延长缩小的后果保留，比例、期限、费用和其他合同未因此全获批准。旧替身预存位置／失败受击方案已否定，新合同仍未定；本轮基础授权不扩大该机制实施范围。

独立AI Lab真实出牌试验的入口、CardTrial／Deck／Stage owner、测试准备事务与验收边界统一见[工作台工程权威](../Guides/CHARACTER_TEST_RUNTIME_AI_LAB_BEHAVIOR_WORKBENCH.md#真实出牌试验当前实现与边界)。本模块仍拥有正式战术费用、卡区与状态合同；测试准备不代表正式替身／体型，本文不复制专项完成状态。

## 敌方行为卡明牌设计（2026-10-05 已确认，待实现核查）

设计权威是[UI 基石](../../StorySource/玩法基石/半即时动作卡牌UI基石.md#敌方行为卡明牌2026-10-05-已确认待实现核查)，人物／角色侧共享表现边界见[角色战术内容指南](../Guides/CHARACTER_TACTICAL_CONTENT.md#共享战斗表现设计2026-10-05-已确认待实现核查)。敌方实际当前卡和已选定待推进卡复用玩家短描述与箭头；完成意图淡化、当前意图高亮，打断／结束／换卡与真实执行状态一致。

常态头顶图标＋当前意图短句，等待可显示真实剩余时间；指向敌人或图标后在敌人旁展开完整卡，鼠标进入浮窗保持，点击固定，换选切换，同一时间仅一张展开。侧边优先、边缘换边、避让角色与地面指示；选中只显示实际存在的目标／方向／落点，不推算攻击范围。查看对象不自动改写战斗目标。本节是待核查／待实现的设计约束，不新增 DTO／API／schema 定义或运行验收结论。

### 2026-10-08 实现与验证边界

混合战斗的 `TacticalCardHud.EnemyCards` → `CharacterTestFairyGuiTacticalBattleView.EnemyCards` 消费 `CharacterTestEnemyCardPresentation`。查询经原 `RuntimeAiPlannerCommandModule` 转发到 AI Overlay，仅读取实际 Active/Pending binding 与执行器的可复用副本；不进行候选选择、条件重求值、命令提交、支付或世界推进。卡面以部署的 `PublishedCardId#DeckEntryInstanceId` 精确绑定，复用玩家 `CardPresentationProjection`、正式卡皮肤、分段描述、tooltip 和参数浮窗。

意图状态通过编译器已有的作者事件位置映射连接 NodeId/BlockId，读取 `BlockResults`，不按描述顺序推测。当前意图着色、完成意图淡化，未开始意图保留未开始；固定等待的剩余时间仅由 `BlockStarts/LastWorldFrame` 推导，重复展示读取不推进。不存在实际选中卡就不显示卡；来源映射缺失、无法确定的进度或特殊自动选点 owner 未提供块事实时，明确显示“意图进度不可用”。

头标与敌人投影区域可悬停展开，进入卡面/参数浮窗保持；点击敌人或头标固定，切换查看对象，卡上可取消固定/关闭，Esc 关闭。同一时间一张展开；这些操作不调用战斗选目标。相机后刷新位置，标记避让、边缘换边、浮窗避让可见角色；离屏、死亡、部署替换、隐藏、重开及 Dispose 清理查看状态。拖牌或地点输入期间关闭查看，避免遮挡其地面操作。本轮未增加目标／方向／落点辅助线；未新增敌方承诺、重选或支付合同。

Composer `TacticalBattleHud` 引用正式 `TacticalEnemyCardMarker` / `TacticalEnemyCardPopup`，已完成受控 Preflight、Apply、Publish、VerifySource 与 VerifyArtifacts；回执 `Logs/UiComposerPublish/c8d507384e77433c8470382438c73608.json`。离线 Roslyn runtime / focused tests 编译通过，`TacticalEnemyCardPresentationTests` 6/6 在离线 Mono 通过，覆盖暂停只读、已选待推进、未知进度、同共享定义多节点、终态和布局。以上不代表原生 Unity 或自然游玩通过；新增脚本 meta、原生编译、资源 Catalog、交互/截图验收交由本批唯一 Unity owner 排队，不能写用户存档或保存 dirty 场景。

## 角色默认内容、玩家清单与行为卡动作装配

2026-09-19：当前数据权威及操作入口见 [角色战术内容](../Guides/CHARACTER_TACTICAL_CONTENT.md)。

- 角色源包只提供初始战术动作、行为卡版本及默认绑定；玩家持有集合由统一服务和单一存档维护。
- 角色名称读取角色包，战术动作引用真实物理动作及作者名称，行为卡名称和编排读取正式发布目录。删除按三个固定 packageId 生成职业和专属招式的注册表；未知角色不自动获得通用卡组。
- 玩家侧行为卡只有“有动作槽 / 无动作槽”；有槽的卡牌实例可明确绑定一个持有的战术动作。内部定位键不具业务含义，也不进入玩家 UI。`PrimaryAttack`、`LightAttack` 及输入取消掩码不负责玩家动作装配。
- 半即时动作卡以 `PublishedCardId / PublishedRevision / PublishedContentHash` 引用正式行为源，准备阶段读取该实例的可选动作装配并编译；不再向玩家暴露 `primary` 或卡内路径。
- 默认卡在开发期以同一 revision 重新发布而导致本地测试存档 hash 过期时，玩家服务只迁移“仍持有、且角色默认配置明确声明同一卡 ID/revision”的引用；不会恢复玩家已移除的卡，也不会替换非默认获取卡。正式部署仍要求精确匹配发布 hash。
- 获取不自动装配动作或重建牌堆；移除引用动作需确认并原子解绑，需要动作但未装配的卡不能启动，正在执行的卡保持冻结快照。
- 玩家卡组以单调 `DeckEntryInstanceId` 区分同定义的重复卡牌；部署冻结卡牌顺序与每个实例的动作装配，不能再以 PublishedCardId 合并绑定。
- 角色属性仍由 `attribute profile` 提供；默认战术配置和初始玩法资源不冒充角色属性。

旧 `ActionId / TacticalActionSlot` 构造接口只供既有调用兼容，不是当前角色内容配置的制作入口。旧实现和执行记录不能证明当前默认数据合法或玩家界面已经验收。

## T4-E HUD 与场景组合（验收中）

### 发布后刷新行为卡库（2026-10-01）

整备面板“预设与开战 → 刷新卡库”重新读取正式 Runtime AI Planner 发布目录，无需重启 Editor 或清档。仅整备阶段可执行；部署中与战斗中拒绝，不修改已确认部署或运行快照。刷新先校验目录、投影、定义和可用性，再在应用线程换入卡定义；读取或校验失败保留旧有效卡库，并显示原因以供重试。

刷新保留玩家库存、动作、卡实例 ID、精确 CardId/revision/contentHash、工作配置、预设和存档修订，不发放卡、不修复默认引用、不升级旧卡、不触发保存。卡库列表显示每个 Stable ID 的最新正式版本；旧版本仍在目录时可继续精确使用，缺失或 hash 不符的引用只报告失效并保留。用户需要升级时明确移除/重新添加并确认动作，再按需更新对应预设。当前仍只支持一个玩家动作槽。

### 2026-10-02：已确认新出牌 UI 与 HeadPad 接入

Battle 阶段使用同一 Composer 包的 `TacticalBattleHud` 和共享 `player_card_face`，依据 [用户确认的 UI 基石](../../StorySource/玩法基石/半即时动作卡牌UI基石.md) 及其唯一当前目标图；保留 `TacticalCardHud` 的场景身份和原命令接口。框架 navigator 注册战斗 HUD、持有包作用域并负责销毁，关闭备战面板不释放战斗纹理，重复备战/战斗切换只保留一个 HUD。选牌小幅上抬，确认与取消独立，卡面绑定原费用、名称、图标、说明和类型，品质保持 Unspecified。

HeadPad 使用原生 FairyGUI 图形：友方绿色/敌方橙色的分段 HP、较细连续蓝色剩余凝聚条、下方 Buff 图标；移除常驻数字、名称和厚底板。保留原显隐、池复用、死亡与破聚短暂反馈。未变更角色、卡牌或战斗规则。

验证：`TacticalUiReadoutBindingTests` 和 `CharacterTestFloatingCombatTextTests` 合计 39 项通过；实际 TacticalCardDemoScene 经原 FGUI 开战进入 Battle，选牌/取消/确认事件链及返回备战后再次开战通过。已检查 Game 截图的真实像素；截图为检查外观暂时唤起原 HeadPad 显示，没有改动战斗状态。物理鼠标命中尚需人工验收（当前 Windows 控制无可调用的 JS 工具）。

### 2026-09-30：正式 UI 前置显示合同

用户已确认本轮聚势、品质与动势采用以下最小方案。项目内部 `TacticalCohesionReadout`、`TacticalCardDisplayData`、`TacticalMotionReadout` 是只读表现投影，不新增游戏资源、稀有度或付款入口。

- 聚势显示剩余承压比例：合法事实且非 Broken 时 `clamp01(1 - ScatterDebt / CurrentCohesionLimit)`；Broken 锁存期间为 0，真实解除后才更新。负压力或当前聚限不大于 0 时明确不可用、填充为 0。分母读取动态聚限，不能用基础聚限替代。Headpad 不用计时充能动画提前补满；原压力刻度不参与剩余聚势条。散势组件及 Combat mirror 仍拥有数值、破聚和恢复。
- 卡品质为 `Unspecified`，采用中性外观；不能将中性色解释为普通品质、从费用推导品质或添加获取规则。费用、名称、描述和类型来自实际卡定义。发布卡的图标唯一作者来源为 `Metadata.IconKey`，通过战术目录、装配和部署携带；图标查找使用精确 CardId/revision/contentHash。缺失 key 保留空值并由正式资源绑定提供诊断和中性占位，不按卡名猜图标。显示投影保留 `DeckEntryInstanceId`，不修改版本或内容哈希。
- 动势的设计已于 2026-10-03 确认为角色可调动储量与卡牌费用使用同一资源。当前实现仍是既有临时账本：初值来自 `tactical_content.json.initialMotion`，经部署进入 Stage 的已接受配置，当前为 50；上限来自 `MomentumLedger.Maximum`，当前为 100。HUD 读取绑定 Stage 的初值和账本 `Current/Reserved/Spendable`，本次不另写常量、反向写角色属性、增加自然恢复或修改底层体力。账本与角色资源尚未合并是实现差距，不再保留为另一种玩法选择；本文不宣称实现已统一，也不据此决定数值换算、恢复速度或费用值。

独立验证入口为 `Tools/MxFramework.NoEngineTests/Tactical.UI.Readout.Tests`，链接真实纯 C# 生产源码。Unity 聚焦入口为 `TacticalUiReadoutBindingTests`，覆盖实际 HUD 的指令预留/取消、UITK 资源加载、Headpad 剩余承压、发布图标经精确目录进入 loadout 与两条部署路线、未配置品质和已接受动势初值；旧 Headpad 恢复计时测试同步为 Broken 不预填充合同。独立检查不替代 Unity 全程序集编译、正式 FairyGUI 发布或实际画面验收。

`TacticalStageFlowCoordinator` 通过只读 `Deck`、`Ledger`、`Time`、`Driver` 属性提供分配为零的协作者读取；`EvaluateHandCard` 由同一权威提供手牌可用性，包括 Pending Action 的原子替换策略。

`MxFramework.Game.CharacterTest.TacticalCardHud` 是内部 repository-validation MonoBehaviour，通过 UI Toolkit 读取上述状态，并向 `TacticalCommandPump` 提交 source 0、单调 sequence、完整 StageRun/Turn/CardVersion/SnapshotRevision 命令。HUD 不推进模拟、不消费权威回执、不计算伤害或卡牌效果；命令消费者写回的 `LastRejection / ExecutionIssue` 会显示为非阻塞操作反馈，保护暂停仍使用独立故障面板。无 Story outcome 的 StageCompleted 不被推断为胜利。

正式 Demo 的 GameSlice 组合接线为 `TacticalCardStageRuntime`，复用原 Host、Story/Spawn、单卡 Session、Combat Physics、BattleVitals、Trigger 与 ReliableOutbox。仅在战术场景配置存在时启用，退出/重试释放旧 StageRun 的时间所有权。完整说明与本次真实结果见 [T4-E 走查](../Walkthroughs/TACTICAL_CARD_PLAYABLE_DEMO_WALKTHROUGH.md)；当前不以编译或纯核心分配测试代替 Playable 验收。

### 2026-10-07：时间轴播放动作事件

普通移动事件段按作者配置的时长结束并进入后续流程，不以到达距离作为隐式完成门槛。到位可停止移动输入，但未到位不会使整卡失败。接近／出招模板不再自动写入 `Completion=Arrived`；旧源和旧执行表中普通移动段的该字段按定时段兼容。显式 `If`、等待条件以及卡编排条件继续决定是否或何时进入出招。需要到位才攻击时，作者应配置独立条件行为；不得把它附加到播放动作事件或普通移动段的隐藏完成字段。专用绕至背后行为的已配置空间目标合同继续保留。

`CharacterAction` 事件表示按时间轴请求播放已配置的动作。播放事件不读取距离、朝向、目标存活或旧的事件条件，不请求攻击机会预览；命令提交也不重复判定这些条件。模板不再给播放事件附加目标存活条件，接近／出招拆分不再把等待条件搬到播放事件上。旧源中的这类事件条件字段保留为历史数据，执行与依赖分析不使用它们。需要等待或分支的行为由显式的等待、移动、转向与流程节点表达。

动作块只观察请求回执和所拥有动作实例的完成，不用目标存活或事件条件阻挡播放入口。事件帧、重复次数、请求身份及实例归属仍由原执行器负责；动作请求继续交由原动作系统，不直接调用 Animator 或重写模拟状态。
### 2026-10-07：正式战斗 HUD 输入

当前 FairyGUI 战斗手牌单击为被动操作，不选牌、不预留费用、不提交执行；三张手牌常态紧凑陈列，悬停放大阅读完整卡面并显示参数浮窗。手牌槽位固定，执行中的空槽不导致其余手牌横移。出牌表现按 Driver 的真实输入需求区分：无地点输入的普通动作／工具卡，向上拖出手牌区后，轻量卡牌图标跟随鼠标，手牌上方保留固定的“松开使用”提示；鼠标图标仅表达正在拖动哪张牌，不表示落点，不拾取地面、不显示落点或地面扩散光圈，也不向执行器传入鼠标位置。地点卡才显示随实际地面点变化的动作图标、标记和原规则范围，合法位置为青玉色，非法位置为红色。拖动期间不选牌、不预留费用；拖回底部手牌区、移出窗口、右键或 Esc 都取消，地点卡的非法落点也取消。合法松手只提交一次，地点卡显示地面扩散反馈，普通卡仅显示 HUD 使用反馈。普通动作卡保持原自动目标或动作移动语义，地点卡才把精确地面点作为执行输入。界面不提供确认执行按钮。内部仍依次提交原选牌、地点输入（需要时）和确认事务，由原 CommandPump、Stage 与 Driver 接纳，不改变费用、准入或结算权威。本文中的选牌／取消／确认记录为历史流程，不能作为当前鼠标手势说明。

## T4-D 历史1v1胜负与 Story 交接（2026-09-11）

`CharacterTestLevelGameplayTriggerAuthoringDocument.AddTacticalBattleOutcomeTemplates()` 成对生成 `tactical.battle.victory.story.v1` / `tactical.battle.defeat.story.v1`。制作期不保存运行实体身份；每个 StageRun 在原 Spawn 回执完成后，使用 `CharacterTestLevelGameplayTriggerAuthoringAdapter.BindBattleRosterOwner(published, player)` 创建运行副本，再经原 `ValidateAndCompile` / `GameplayTriggerCatalogBootstrap` 建立本波独立的 catalog、runtime、binding 与 activation 额度。原发布文档不被修改，不能复用旧波次 runtime 或沿用旧角色 generation。

`CharacterTestGameplayTriggerBattleVitalsCatalog` 验证 provider **3**、Signal schema **1**、`UnorderedMultiset`、每绑定每帧一个 complete sample、双方实体能力与 roster owner。`CharacterTestGameplayTriggerBattleVitalsSystem` 在既有 Gameplay Pipeline 的 Simulation priority **961**（属性/伤害之后、Trigger evaluation 之前）读取 `GameplayAttributeSetComponent.Hp` 的当前值。Signal **450501** 的 `Subject/Instigator` 为本波玩家/对手，`Payload0/1` 为同帧双方 HP，`Payload2` 为 positive Int32 StageDefinitionId，`Sequence=0L`；按 SourceBindingId 固定顺序投影同一份样本。generation 失效、缺实体、缺属性集合或缺 HP 字段均报告完整性故障，绝不默认成 0；已成功接纳的终局则继续使用冻结的 outbox 事实。

Defeat 条件为 `SignalId=450501 && playerHp<=0`；Victory 为 `SignalId=450501 && playerHp>0 && opponentHp<=0`。条件使用现有二元 `All` 编译器，双方同时归零只可能激活 Defeat。两个 route 均为 `PerInstance / MaxActivations=1`。每次激活的三个动作在同一 reservation 事务中提交：可靠 `RaiseTrigger`、关闭自身绑定、关闭另一终局绑定。关闭动作使用项目 action kind **450502** 投影到原 `InternalDeferred.SetEnabled`，下帧 PreCommand 生效；无法预留可靠记录或关闭动作时不消耗激活、不部分提交。原 Authoring adapter 同时校验互斥条件、一次性策略、Story payload 与关闭目标，拒绝弱化合同的模板。

`TacticalStageEndTriggerBridge` 每个 StageRun 一份；组合根把它的 `Vitals` 与 `Evaluation` 加入原唯一 Gameplay Pipeline，将它安装为原 `StoryRuntimeModule` 的 `IStoryRuntimeCommandResultSink`，并通过可选 downstream 保留原 `GameplayTriggerStoryCommandResultRelay`。仍使用同一 hard-bounded Story buffer、同一 validator、Derived `GameplayBridge=1003102`、原 outbox module 与原 Story drain；不使用 observer 作为权威。

Coordinator 在 `OnCompletedSimulationTick` 内先检查 bridge，再调用 Driver。匹配的 intent/outbox 接纳使 Health 进入 `AwaitingStoryTransfer`，释放 UserMenu 之外的暂停，锁定所有卡牌命令并抑制正常 `CompleteTurn`。来源或 evaluation 漏跑、reservation/容量失败会显式故障；正常 `ConditionRejected` 表示没有胜负，不是系统故障。Story 队列满或 transfer 关闭时可靠记录保留；默认 60 个模拟 Tick 的交接预算耗尽后以 `StoryTransferTimeout` 暂停，`RequestRecovery` 可以继续推进原 outbox，未交付前禁止重建下一会话。

原 `TacticalStoryBridge` 在 Director 回调中只复制 `StepStarted`，帧末 Drain。终局 Token 必须匹配本波 StageRun、配置 Graph/Step/Action 及直接 Story 成功回执中的 BeatInstance。只有随后完整模拟边界才进入 `StageCompleted` 并请求 `BattleEnded`。因此致死帧即使正好是第三稳定帧，也保留 Resolving、已付费用和原回合号，不补牌。语义拒绝通过直接 result sink 保存精确 `StoryDirectorResultCode`，进入 `StoryTransferRejected`，不伪装完成；observer 缓冲溢出只损失展示记录。

`CharacterTestStoryFixture.CreateTacticalBattleGraph()` 提供保留原 Bootstrap、增加胜败 Presentation Step 的战斗图变体；普通 `CreateBootstrapGraph()` 行为不变。`CharacterTestStoryFlowBridge` 公开 `LastBattleOutcome`（`None/Victory/Defeat`）、nullable `LastBattleVictory`、`LastBattleOutcomeStep` 与 `BattleOutcomeStarted`。冷路径 `BindTacticalBattle` 将本波身份、旧可靠工作终态检查、原 Story 重试/下一波请求和 `Coordinator.BeginStoryRestart` 时间释放端口绑定一次；`TryRequestRetry()` 可用于故障/结算，`TryRequestNextWave()` 仅在胜利后接纳；请求被原 Story 队列接纳后才放行 Host，同一会话只接纳一次转移。新波次必须由原 Story/Spawn 清理与重建流程提供新 StageRun、runtime、Director/module 和 bridge，不能把旧 envelope 转交新会话。

Driver 默认总预算现在为 **300 Tick**，可由构造配置覆盖；在 60Hz 下是 5 秒模拟时间，0.2 倍速下约 25 秒墙钟。超时保留 Resolving/费用/回合号，`execution:RecoveryTimeout` 可诊断；恢复后重新累计完整三 Tick 停稳窗口，单次执行不重复触发超时。`TacticalStageEndTriggerBridge.Dispose()` 释放本会话 gate，保留 UserMenu；统一时间所有者结束会话时，在释放桥与菜单所有权之后调用 `UnityTacticalTimeAdapter.Dispose()`，恢复进入会话前的有效 timeScale，旧适配器延迟调用不再改时间。

验收入口：`MxFramework.Tests.TacticalStageEndTriggerTests`。使用真实 RuntimeHost、Trigger compiler/runtime、deferred、ReliableOutbox、StoryDirector/module 与 observer router；执行器/物理稳定事实为受控端口。该证据覆盖 T4-D Runtime 合同，T4-E 仍负责真实场景、HUD、下一波 Spawn/清理装配与 Play Mode 视觉验收。

T4-D 最终验证（2026-09-11）：新增 **25/25**、既有 TacticalCard **191/191** 通过；扩展作业 `0fda9266bece4e8e8116262838e2a271` 共 **1121 项，1110 passed / 11 failed / 0 skipped**。移除本工单源码增量后的对照作业 `8d99b4b158904a95a32878d80f1bbf9a` 复现相同 11 项编辑器/UI 基线失败，未发现新增失败；此结果不等于全库全绿。最终脚本作业 `fe830c0051ce4463b3acd835b35eb000` 完整通过（refresh/compile/domain reload/ready/Console 0 error）。原始 XML、基线对照与代码恢复校验位于 `Library/MxFramework/TacticalCardTask4D/`；未执行 Git Commit。

## T4-A 关卡 Authoring 与正式场景

正式入口是 `Assets/Scenes/TacticalCardDemoScene.unity` 与独立的 `Assets/Config/MxFramework/Levels/TacticalCardDemo/tactical_card_demo.level.json`。场景由 Unity Editor 维护，基地布局由 `BaseFacility_Exterior.prefab` 实例提供；场景角色、静态碰撞体和关卡缓存通过 Mx Level Editor 保存。原空场景生成菜单与 `CreateTacticalCardDemoScene.Generate()` 已移除，避免覆盖手工场景。角色 Prefab 仍由运行时动态生成，不静态放入场景。基地资源与操作见 `Docs/Guides/ENVIRONMENT_BASE_SCENE_ASSEMBLY.md`。

角色模块 `characterTest.characters` 的新版本为 schema 2，payload 为 `mx.character-test.scene-characters.v2` / schema 2。`CharacterTestSceneCharacterSpawnPose` 包含 `Position` 与 `YawDegrees`；wire 格式为 `spawnPose: { position: { x, y, z }, yawDegrees }`，只序列化坐标，不包含 Unity Vector3 的计算属性。Normalize 返回副本并将有限 yaw 规范到 `[0, 360)`；非有限姿态、非法角色 ID 与 `TeamId <= 0` 在编译时拒绝，失败不替换已应用缓存。模块与 payload 必须是匹配的 v1 或 v2，未知版本不通过 Normalize 降级。

`CharacterTestSceneCharacterCache.CharacterInstance` 保存 `HasSpawnPose / SpawnPosition / SpawnYawDegrees`。有姿态时 `TryCreateSpawnSpec` 使用编译位置，锚点移动或删除不改变已发布出生坐标；没有姿态时保留既有 Transform 读取行为，支持旧 v1 场景及缓存。v1 读取不改写源文件；编辑锚点时记录 SpawnPose 并在编辑文档中升级为 v2，实际发布仍通过原 SaveCoordinator。当前 SpawnSpec 只有位置接口，yaw 在 Authoring、锚点与 Cache 中保留；后续运行朝向接线属于 Spawn 消费端工作。

角色编辑器文档路径优先使用 `SetDocumentPath(path)` 注入值，其次读取活动 GameManager 的 `CharacterSceneCache.AuthoringJsonPath`，最后使用原默认路径；`SetDocumentPath(null)` 清除注入。切换上下文会重新加载文档。锚点坐标/yaw 的变更加入原 Unity Undo group，与角色文档一起 Undo/Redo；复制与重新放置也捕获新姿态。统一保存保留同一 envelope 中其他模块，默认 CharacterTest 文档不被 Demo 写入。

同一 envelope 的 `characterTest.tacticalStage` / schema 1 由纯配置类型 `CharacterTestTacticalStageAuthoringDocument` 提供：`InitialMomentum=50`、`HandCapacity=3`、`RequiredStableTicks=3`、`NormalScale=1`、`DilatedScale=0.2`、`PreActionBudgetTicks=240`、`ExecutionBudgetTicks=300`、`IngressBudgetTicks=300`、`StoryTransferBudgetTicks=60`、`SimultaneousOutcomePolicy=DefeatWinsSameFrame`、`StageResetPolicy=ResetSessionAndDeck`。`PlayerSpawnStableId=tactical.player.spawn` 对应 Team 1 / LocalPlayer / `(0,0,-2.5)` / yaw 0；`OpponentSpawnStableId=tactical.enemy.spawn` 对应 Team 2 / AiControlled / `(0,0,2.5)` / yaw 180。此处按 T4-A 实施工单修正旧设计示例中的 Team 0/1、敌人 ID 后缀和场景路径。

验收入口为 `TacticalCardDemoAuthoringTests`：生成/重开/统一保存的 hash 与缓存一致性、默认关卡字节保护、空锚点、姿态 Undo/Redo、v1 兼容、已发布位置独立于锚点、路径注入、非法角色/队伍和未知 schema 拒绝。本切片只完成关卡数据与场景骨架，Story 开场、战术时钟、胜负 Trigger、HUD 和三回合 Playable 验收继续由 T4-B～T4-E 承接。

T4-A focused 验证（2026-09-11）：Unity EditMode 作业 `b580209769684ae097b69750a8e1d39d` **96 passed / 0 failed / 0 skipped**，耗时 94.47 秒，包含新增 Authoring 11 项、原角色 Authoring 25 项和 Task 1～3 的 60 项。本地 NUnit 报告：`Library/MxFramework/TacticalCardTask4A/focused-96-passed.xml`。源码编译作业 `6f5b8ffc6573427ba44b30611c5b7410` passed，确认 refresh、编译开始/结束、域重载、Editor ready 与 Console error count=0。

## T4-B 入场生命周期与暂停命令消费

项目内 `ITacticalStageFlowCoordinator`、`TacticalStagePhase`、`TacticalStageHealth`、`TacticalStageCommandKind`、`TacticalStageCommand`、`TacticalStoryStepToken` 和 `TacticalStageRejection` 位于 `TacticalStageContracts.cs`。这些类型为 internal，保持第 7.1 节规定的值类型字段与身份宽度；不增加 package 公共 API。`TacticalStageFlowCoordinator` 接受一个非零 StageRunId、全新的 Time/Deck/Ledger/Driver、复制后的固定 seed 牌库、Story 步骤绑定及 generation 存活谓词；Driver 必须使用同一组协作者，手牌容量必须为 3。

调用顺序：订阅 Story → 消费开场 token → 原 Spawn 消费端提交绑定完成回执 → 每个完整世界帧提交双方物理事实 → 首次 Planning → 暂停命令消费 → Driver 执行与收势。构造与 Story 开场均不调用 `Initialize()`、发牌或重置账本。slot 0 为玩家，slot 1 为对手；`GameplayEntityId.IsValid` 与注入的 `isEntityCurrent` 同时成立才接纳，后续完整帧继续校验 generation。绑定冲突、双方指向同一实体或实体失效进入保护暂停；旧 StageRunId/旧帧回执返回诊断，不替换当前 roster。

入场使用两个独立 `SettleDetector(new SettleDetectorConfig(requiredStableTicks: 3))`，采样必须匹配同一 completedFrame；必须显式 `NoActionStarted=true`、`ActionTimelineCompleted=false`，其它终态、支撑、速度、接触与待运动事实继续由原 detector 检查。重复/倒序帧、错帧或缺少语义事实重启稳定窗口。双方在同一完整帧末满足三 Tick 后，唯一一次初始化牌库、把气机设为 50，再调用 Time.Initialize，进入 Planning/ReadyPaused。预算默认 300 个世界 Tick，最后预算帧仍可正常成功；否则报告 IngressTimeout。开场后仍缺一方时也在预算耗尽后报告 SpawnTimeout，不初始化战斗。

`Apply` 只由战术消费者调用。先校验 StageRunId、TurnIndex、SnapshotRevision，再校验阶段、卡 ID 与版本；取消/确认也携带当前 Pending 卡身份和版本。SelectAction 切换前检查新卡种类与总气机，随后使用原 Cancel/Set；Utility 仍只接受原 AddMomentumPayload。确认只走 Driver.TryCommit，成功后才进入 Executing；准入拒绝保留 Pending、版本、预留和 Ready。Executing/TurnSettling 只向 Driver 提交玩家样本，正常补牌和回合号仍由 Time/Deck 完成。成功控制事务及阶段改变递增 SnapshotRevision；失败通过明确 rejection 和 ExecutionIssue 诊断。

`TacticalCommandPump` 的命令及回执缓冲默认各 64 项，每次 `Drain` 默认最多处理 32 项；Source 是发布时确定的有界槽（默认 0～7），Sequence 从 1 单调增长。`TrySubmit` 将命令登记在最后完整帧 F 的 F+1 边界；`TryEnqueue` 接受保留 ulong/uint 宽度的 typed record，按 WorldBoundary/Source/Sequence 排序。每条已处理命令产生包含原记录、拒绝码和新 SnapshotRevision 的回执。满队列返回 QueueFull 且不消耗 Sequence，满回执缓冲停止消费并保留待处理命令；调用者需通过 TryReadReceipt 取走结果。不能使用“自动套用最新快照”绕过过期命令验证；同一边界的连续 UI 操作应依据已返回版本发出，批处理/回放需携带对应事务版本。

`GameManager.BindTacticalControl(stage, pump)` 注入同一会话；Update 在 UI 输入后、世界步进前消费战术命令，不受 timeScale=0 阻断。恢复当帧丢弃暂停期间的 delta。`CharacterTestFixedTickDriver.CanStepPredicate` 在每个子步之前复查门控，并在最后子步触发暂停时清除剩余小数时间；手动 GameManager 步进入口也检查战术门控。解除绑定/释放运行会话只释放战术门控，保留菜单原因与牌/气机现场。

`TacticalStoryBridge` 使用 `Director.SubscribeEvents`，按明确的 Graph/Step/AuxId 过滤，仅复制 token 到预分配缓冲；主线程在 Director 回调之外 `Drain`，不会重入 Director、Spawn 或世界 Tick。重复排队的同一 token 合并；溢出在 Drain 时报告 StoryBufferOverflow，不静默越过生命周期。终局 token 先锁控制输入，实际 BattleEnded 暂停延迟到完整帧末，Resolving 卡不假装完成或补牌。`CharacterTestSliceOptions.SubscribeBeforeStoryBootstrap` 在 GameSlice 构造的首次 bootstrap 之前建立订阅，并由 GameSlice.Dispose 释放；原 CharacterTestStoryFlowBridge 也按 Graph/BeatInstance/Step 去重 Spawn 请求，部分失败不会重发已接纳的槽位。

RequestRecovery 只允许继续观察 RecoveryTimeout，释放该原因、重置入场窗口并保留菜单；ExecutionFault 不能凭按钮伪装恢复。RequestRestart 必须由注入的正式 Story 请求端口接纳 F+1 请求后才释放旧会话的战术暂停并进入 AwaitingStoryTransfer；拒绝或缺少端口时现场不变。TimeCoordinator 新增 HasPauseReason 查询；未初始化时允许世界运行，显式菜单/保护暂停仍有效。旧会话交接后禁止再次 Commit，新 StageRun 必须使用新协作者。

本切片为可注入 Runtime Slice。真实场景装配仍须从发布配置提供牌库及动作绑定，在 bootstrap 前建立桥接，在原 Spawn/物理消费端提供完成回执与同帧事实，并调用上述控制入口；本次不把 T4-A 场景声明为已自动完成这些装配。敌人入场 AI 控制抑制、玩家自由输入归属、正式会话 Tick 排序、Trigger 可靠外箱屏障分别与 T4-C/D 集成一起验收，HUD/三回合可玩走查属于 T4-E。typed 控制记录可供回放适配，但本切片不实现 RuntimeCommand wire codec、SaveState 持久化或完整暂停存档恢复；保存时必须同时保留已处理/已接纳序号、待处理命令及卡牌会话，不能只保存手牌。

验收入口 `TacticalStageFlowCoordinatorTests` 覆盖 B01～B03、C01～C04，以及错帧/非法物理事实、generation 失效、预算恢复、Story 订阅时序与释放、终局帧末冻结、队列/回执背压和宽身份字段。回归范围包含原 71 项 TacticalCard 测试以及已有 FixedTickDriver/交互节奏测试。

T4-B 最终验证（2026-09-11）：Unity EditMode 作业 `9cf4f439bdae42959a17a26bbce9f966` **134 passed / 0 failed / 0 skipped**，耗时 41.98 秒，包含新增 43 项、原 Task 1～3/T4-A 的 71 项及固定帧/交互节奏 20 项。报告保存在 `Library/MxFramework/TacticalCardTask4B/final-134-passed.xml` 和 `final-editmode-results.json`。编译作业 `ae3a5417e73e47498640eb6e05845ded` passed，确认 refresh completed、compilation started/finished、domain reload observed、Editor ready、Console error count=0；完整回执为同目录 `final-script-validation.json`。编译报告仍有其它既有文件的 22 条 warning，没有本切片新增文件的 warning。首批测试完成后 Console 工具曾将无堆栈的 `Saving results to: ...TestResults.xml` 报为 Exception，与先前 Task 1～3 记录同型。最终测试后的独立 Console error 查询返回 0 条，回执保存在同目录 `post-test-console.json`；两次观测分别保留。临时 MCP 恢复脚本已移除，正式新增脚本均具有 Unity 导入生成的 `.meta`。

## T4-C 单卡真实推演与回合生命周期

本切片继续复用 `TacticalCardSession → RuntimeCommand → CharacterAction Gateway` 与 `TacticalSingleCardDriver → TacticalTimeCoordinator → TacticalDeckManager`。Stage 不创建动作时间轴、不自行扣费或补牌。确认成功进入 Executing；完整帧末只传玩家 sample 给 Driver，行为终态同步 TurnSettling。2026-10-09 起，两处正式组合根向 TimeCoordinator 注入 `TacticalTurnPauseDetector`：仅当 `BehaviorTerminal && PlayerCanActFreely` 时，CompleteTurn 归档并补到三张，TurnIndex 增加并请求 Ready 暂停；世界减速到零后 Stage 回 Planning。普通回合不等待动作时间轴自然 Finished、控制释放/命令清空、落地、稳定支撑、低速度或连续三 Tick；对手 sample 不参与普通回合暂停判定。执行器故障仍走独立保护暂停。原双方入场物理停稳检测保持独立，混合切片仍在 roster 绑定后直接初始化 Planning。

行为终态同时接受成功 `Completed` 和已完成退出清理的 `Failed`（条件拒绝、目标消失、行为时限耗尽、动作请求拒绝），保留具体失败反馈，不继续等待未起手预算。背后保持段到达后仍按作者配置持续保持；段结束成功或失败后均立即进入上述暂停判定。`CleanupTimedOut / CleanupFailed`、正式命令提交失败及权威动作取消／中断仍属于执行故障，不按普通失败结算回合。

2026-10-10 用户修正：末尾攻击段进入可自由行动的 Recovery 即开始减速，但不得强行取消角色攻击后摇。仅 BasicAttack／Skill 且无受击硬直、无受控原因、行动与移动权限均有效时适用。末尾 `Completion=Action` 段经共享 Host／Session 完成卡级调度并释放自身移动／转向控制，解除行为执行器对动作的所有权，不发出物理动作终止命令。角色动作实例仍为 Running，后摇随世界倍率继续播放，并在世界完全暂停时冻结；恢复世界时间后仍由原动作 Gateway 自然推进或处理玩家新指令的正常取消／替换。选牌及普通卡准入允许这种自由收招，仍校验冷却、费用和新卡规则。自然 Finished 在完整帧末直接接纳；运行卡所属动作的外部取消／Interrupted 与清理失败仍进入故障保护。只有动作段结束边界等于当前 Timeline 结束边界时适用，存在后续编排时仍执行原流程，不重复推进行为 Tick。

此前强制退出版本曾测得自由 Recovery 后约2帧开始减速，但会产生 Cancelled；该实现与证据不作为本次保留后摇版本的验收。

保留后摇版本原生回执（2026-10-10）：真实 `TacticalCardDemoScene` 出“常规移动接敌出招”，动作455在世界帧84／动作帧38进入 Recovery，同帧 Session Completed、界面显示“正在减速”，攻击实例仍为 Running。减速期间动作继续推进；世界帧130回 Planning，倍率0，攻击仍为 Running／Recovery／动作帧84，无 Cancelled 或 Interrupted，Health 保持 Running，ExecutionIssue 为空。暂停时原命令待消费数0，手牌“引敌入锋”“就近取背”“选点换位”可用性均为合法，未因残留自由收招被判 ActorBusy。复现沿正式 CommandPump／Session／Gateway／世界帧，不注入 Tick，不保存玩家进度。编译作业 `65389769639749d48729c630e090631a` 确认 refresh／compile／domain reload／ready，Console error 0；仅此单轮与可用性读取，不外推所有动作及后续出牌回归。

提交前验证（2026-10-10）：新增3项核心回归覆盖显式地点提前到达、末尾行为释放保留物理动作、后续编排不能被提前跳过；通过 Unity 已编译程序集在独立 Mono 中调用，3/3通过。最终脚本验证 `1b50106bc5214b2a890084048bf581c3` 确认 refresh／compile／domain reload／ready，Console error 0；定义ID目录校验通过，代码规范扫描6项已有提示。原生战术卡及相关作者入口回归作业 `0c548879b84144eb98fe979045dc69cf`、新增3项精确过滤作业 `beb48f4faa4f49949d2b49019b9c699f` 均在初始化超时、执行数0，未获得原生测试结果；独立 Mono 结果不替代这些原生门禁，也不宣称全部回归通过。

### 目标与权威动作通知

- `CharacterTestTacticalExecutionAdapter` 构造末尾增加可选 `Func<CharacterTestCharacterEntity> targetResolver = null`。外部 facts 有目标时使用该目标；facts 为 null 或 TargetExists=false 时调用 resolver，未注入则在 Runtime 已存在实体中选择存活异队且实体 ID 最小的目标。resolver 明确返回 null/非法目标时拒绝，不擅自替换它指定的选择。
- 自动补齐使用既有 DirectAiNavigationService 和 PerceptionFrameCache 的最近 hurtbox 距离；这是开放场地的默认导航事实，不增加寻路系统，也不将根距离伪装为命中距离。正式测量范围、朝向和起手 Gate 仍归编译的 Session。障碍场景应提供原导航 owner 产出的 facts。
- Prepare/Admit 均复核目标存在、Runtime 中实体引用仍匹配、存活且异队。明确拒绝码为 `execution:TargetMissing / TargetDead / InvalidTarget / TargetChanged`。Prepare 保存目标引用/ID及卡版本；Admit 拒绝目标切换和旧卡版本。执行中缺 facts 时只刷新已锁定目标；目标失效不重新选敌，起手前请求原安全清理，起手后允许原动作收尾。
- `ActionStateChanged` 为 `Action<ulong, CharacterActionInstanceState>` 事件：第一次观察到正式回执实例及之后状态变化时通知，每个已观察状态仅一次。短动作第一次观察即 Finished 时不虚构中间 Running 通知。先更新状态再发布，允许订阅者只读 Observe；订阅者不得抛异常、推进世界或重入准入，退出时解除订阅。
- 只有当前权威实例 `Finished` 才能令 ActionTimelineCompleted=true；Cancelled/Interrupted 显式清除该标志并锁存 `execution:ActionInterrupted`。取消窗口、IsFinished 聚合属性、实例被移除及行为终态均不能代替 Finished。`ExecutionObservation.Issue` 携带故障诊断，Driver.LastIssue 与 Stage.ExecutionIssue 保留相同原因；故障保留 Resolving、已付费用和原 Phase，禁止补牌。

### 命中奖励核销

| 入口 | 合同 |
| --- | --- |
| `Stage.TryApplyHitReward(long hitEventId, int momentumAmount)` | 同步权威命中消费入口，关联 Driver 当前 execution；仅 Running/Executing 且世界运行时允许。 |
| `Stage.TryApplyHitReward(ulong executionId, long hitEventId, int momentumAmount)` | 延迟交付必须使用此重载并保存产生事件时的 executionId；旧执行迟到不能归到新回合。 |
| `Driver.TryApplyHitReward(ulong executionId, long hitEventId, int momentumAmount)` | 复核当前 execution 和时间阶段，通过可选内部 ITacticalHitRewardPort 将自己已有的 ledger 传给执行适配器。 |
| `Adapter.TryApplyHitReward(ulong executionId, long hitEventId, int momentumAmount, IMomentumLedger ledger)` | 同一执行内按 hitEventId 去重，要求卡仍在 Resolving、已真实起手、Session 未结束、角色存活且无故障/中止/暂停；只调用传入账本 Grant。 |

上述方法正常调用 NoAlloc。ID 和奖励量必须为正；每次 Admit 重置预分配的 256 项事件记录，达到容量后拒绝新事件，不淘汰旧记录、不重复付款。重复事件即使改了奖励量也拒绝。气机饱和到 100 的合法事件仍核销，之后不能借重复交付补领。成功奖励增加 Stage.SnapshotRevision。动作最后一帧的命中可以在 Session 进入终态前支付；TurnSettling、Planning、挂起、已释放 Stage、未知/旧执行拒绝。

调用者必须是现有权威 Combat 结果消费者，核对命中归属后在完整模拟帧的奖励阶段提交；入口不把任意 UI 请求或展示事件认证成命中。本切片提供核销端口及真实命中驱动的自动化证据，场景中 Combat 消费者/奖励配置的实际装配仍由后续组合根完成。事件 ID 仅在所属 execution 内唯一；不同执行允许使用同一局部 ID。CompleteTurn 仍使用冻结的 `(instanceId, committedVersion)`，禁止延迟回调重新读取卡的可变 Version。

### 完整采样与验收边界

`SetPhysicsSample` 末尾增加 `bool hasStableSupport = true`。调用表示本帧物理 owner 已提供接触事实；null supportBody 表示静态地面，不能单凭没有 Rigidbody 判无支撑。无有效接触必须显式传 false；接触点非有限、原动态支撑体已销毁、缺本帧采样或 IsGrounded=false 都不得累计停稳。动态支撑继续扣除 GetPointVelocity(contactPoint) 和支撑角速度。Stage 和 Driver 对重复/倒序帧清空回合稳定窗口、不回退世界帧号；漏帧由原 SettleDetector 重新开始窗口，sample 与 completedFrame 不一致则故障挂起。

自动化入口 `TacticalStageFlowTests`：D01 使用当前装配 AttackPrimary、正式 Session/命令/Gateway 连续三回合；D02 覆盖真实取消窗口、实体受击事实、未 Finished 终态与受控 Cancelled/Interrupted 权威实例回执；D03 覆盖腾空顶点、无接触、旋转支撑、缺帧/倒序/重复/错帧；D04 使用受控的敌方前摇/运动 sample 验证非对称停稳；D05 使用真实动作的 Combat 命中触发奖励，验证去重、同卡回洗版本及旧回执，并测量重复核销热路径无分配。EditMode 的隔离 Runtime 证据不替代骨骼渲染、敌人动画暂停恢复或 T4-D/E 的实际 Demo 组合根、Trigger 终局交接与可玩验收。

全量回归同时修复旧 TacticalCard 测试夹具的内容漂移：默认行为改为解析当前装配的正式 opener，显式 actionId 仍固定测量指定动作；动作 ID、完整时长和前摇边界由已解析的正式配置交叉验证，保留原单动作、移动释放、换装失败/修复、存档身份与回放一致性断言。距离事实显式提供当前 hurtbox 表面距离；位置夹具区分表面距离和根坐标，Gate 检查更新到既有编译器的存活/距离/朝向合同，入口重新检查存活，不再断言已移除的聚合命中机会条件。移动滞回与过近后退用接地目标、受控背向姿态隔离朝向 Gate，继续验证真实运动。`ForCurrentOpener` 支持把原可注入 actionRuntime/abilityRuntime 传入同一隔离 Probe。偏移测量靶子的零 yaw 路径直接保留中心坐标，避免 CORDIC 的 cos(0)=1.000002 缩放偏移、破坏精确测量几何合同；非零 yaw 与正式角色几何不变。D01 仅在夹具创建时通过 World 属性权威设置靶子初始 HP=1000，以承受三次真实伤害，回合间不补血或更换目标。

T4-C 最终验证（2026-09-11）：Unity EditMode 作业 `c045473d431c4b6aa6284adcb071a309`，在 `MxFramework.Tests` 中按 `TacticalCard` 分组完整回归，**191 passed / 0 failed / 0 skipped**，耗时 118.29 秒；其中新增 `TacticalStageFlowTests` **24 项**、既有用例 **167 项**。当前默认正式 opener 为 ActionId 455；D01 从 Turn 1 连续推进至 Turn 4 Planning，手牌每回合补至三张，无奖励时气机依次 40/30/20；D05 验证真实匹配动作、攻击者和目标的已接受伤害后去重付款。最终报告为 `Library/MxFramework/TacticalCardTask4C/final-191-passed.xml` 与 `final-editmode-results.json`。

最终脚本验证作业 `9afc4e4181b049f986cc221cf0b5fad7` **passed**：refresh completed、compilation started/finished、domain reload observed、Editor ready、compilation errors 为空、Console error count=0；回执为同目录 `final-script-validation.json`。新增测试 `.meta` 由 Unity 导入生成。验证期间共享工作区另有 Network/Runtime hash 并行改动，未将其纳入本工单实施范围。测试后的 Console 工具查询将两条无堆栈的 `Saving results to: ...TestResults.xml` 保存消息报告为 Exception，与 Task 1～3 已记录现象同型；原始回执保存在 `post-test-console.json`，不将测试后查询描述为 0 条错误。编译通过与全部测试通过分别由上述完整回执及 NUnit XML 证明。

## 数据与账本

- `TacticalCardDefinition` 为不可变配置，包含 `Id / Name / Description / Kind / BaseMomentumCost / DestinationAfterUse`；目的地使用 `CardDestination.Discard / Exhaust`。费用只限制非负，110 费用不能被夹成 100。
- `TacticalActionCardDefinition` 包含 `ActionId` / `ActionSlot` / `CooldownSeconds` / `TargetMode` / `Approach` / `MomentumReward` / `SharedCooldownGroup`。`ActionId` 与 `ActionSlot` 互斥，双配时固定动作显式覆盖插槽并写入 `ConfigurationWarning`（`FixedActionOverridesSlot`）。战术行为卡直接存放或引用具体的动作装配与时间轴，与输入缓存打断掩码（`CharacterActionSlotType`）完全解耦。`Approach.None` 不隐式绕过起手条件。`BoundProfileId` 已在 Phase 5（T-16）清退，仅为兼容保留且恒为空。
- `TacticalUtilityCardDefinition.Payload` 支持 `AddMomentumPayload` 即时加动势。`ApplyStatusPayload` 已有状态 ID、层数、生命周期和持续帧字段；尚无状态权威适配器，因此当前打出返回 `UnsupportedPayload`，不扣费、不移牌。
- `TacticalCardInstance` 保留实例 ID、定义、位置、强化等级和版本；位置变化增加版本，未出牌实例不被重建。本阶段不实现强化费用修饰器。
- `CardPlayEvaluation` 是只读值类型，由 `EvaluateCard` 当场生成，提交重新检查。它只表示本地卡牌/资源合法性，不表示执行器已准入。
- `IMomentumLedger`：`Current / Reserved / Spendable`，`TryReserve(int amount, out ulong reservationId)`、`ReleaseReservation(ulong reservationId)`、`CommitReservation(ulong reservationId)`、`TrySpend(int amount)`、`Grant(int amount)`。
- 账本始终保持 `0 <= Reserved <= Current <= 100`。构造时夹取初值；负数消费/预留失败，负数 Grant 不生效，大额 Grant 饱和到 100 且避免溢出。仅一个预留槽，零费用也领取凭证。凭证只在所属账本内有效，旧凭证不影响新预留。

## 手牌与回合调用

`TacticalDeckManager(int handCapacity = 3)` 是单线程卡牌事务所有者。所有集合通过缓存的只读包装观察；热路径调用方使用索引循环。

| 方法 | 语义 |
|---|---|
| `InitializeDeck(IEnumerable<TacticalCardDefinition> definitions, int seed)` | 每个条目生成独立实例；允许重复定义，拒绝 null 条目；确定性洗牌并发初始手牌。存在 Pending/Resolving 时禁止重初始化。 |
| `ReplenishHand()` | 只消费初始化或正常完成回合开放的补牌窗口；先抽完 DrawPile，再把 DiscardPile 洗回，短牌库尽量补齐。回合内重复调用无效，不能借 Utility/取消刷牌。 |
| `CanPlayCard(card, ledger, hasPendingAction, out failure)` | 检查当前阶段、实例确属本管理器手牌、单 Action 限额、支持的 Payload 和可花费资源；外部传 false 也不能绕过内部待命槽。 |
| `SetPendingAction(card, ledger)` | 预留冻结费用，Hand → PendingAction；已有待命卡则拒绝。切换时先显式 Cancel，再 Set。 |
| `CancelPendingAction(ledger)` | 只释放同一账本的所属凭证；恢复该卡的手牌顺序，不扣费、不补牌。 |
| `PlayUtilityCard(card, ledger)` | 从 Spendable 支付，再应用加动势效果，进入弃牌/移除堆，不补牌。存在待命 Action 时必须使用其账本。 |
| `CommitTurn(ledger, out committedAction)` | 恰好一张 Action；仅扣预留一次并进入 Resolving。重复提交及 Resolving 期间出牌均失败。返回 `CardPlayFailure`。 |
| `CompleteTurn(ulong committedInstanceId, uint committedVersion)` | Task 2/3 在正常停稳后提交匹配身份及提交时版本；移入目的地并补牌。重复、过期回执返回 false，即使同一张牌已被循环再用。 |

`SetPendingAction / CancelPendingAction / PlayUtilityCard` 同样返回 `CardPlayFailure`，成功为 `None`。提交后立即保存 `committedAction.InstanceId` 和 `committedAction.Version` 的值用于完成回执，不能在延迟回调里重新读取可变版本。

Task 3 的 `TacticalSingleCardDriver.TryCommit` 先确认正式执行器准入，再执行本地 Commit；所有协作者必须同步、单线程、不重入，准入本身不得推进世界。若本地 Commit 失败，撤回尚未起手的请求；撤回失败进入 ExecutionFault。Task 2 的异常暂停不能调用 `CompleteTurn`。T4-C 权威命中奖励通过上述去重入口调用账本 Grant；提交和正常结束均不自动发放 `MomentumReward`。SaveState/Replay 快照及完整战斗集成属于后续任务。

## 停稳与时间协调（Task 2）

以下 `SettleDetector` 的物理阈值与连续 Tick 合同保留用于入场及历史隔离组合。当前普通回合使用无累计状态的 `TacticalTurnPauseDetector`，只检查行为结束与玩家自由行动权限；`RequiredStableTicks` 不再限制普通回合暂停。`SettleSample.PlayerCanActFreely` 来自同帧有效玩家实体的战斗状态：存活、无受击硬直、`DominantReason=None`，且 `CanStartNormalAction / CanMoveByInput` 均为 true，不要求静止或落地。

- `SettleSample` 保存完整物理帧事实，速度必须是相对于支撑接触点的非负大小。额外的 `NoActionStarted` 是明确的未起手终态事实，默认 false，不能用缺少起手通知推断，更不能伪造 `ActionTimelineCompleted`。
- `SettleDetectorConfig` 为不可变 class，构造参数依次为 `requiredStableTicks=8 / enterLinearSpeed=0.08 / exitLinearSpeed=0.15 / enterAngularSpeed=0.15 / exitAngularSpeed=0.30`；拒绝非有限、负数及倒置区间。
- `ISettleDetector.Sample(in SettleSample)` / `Reset()`：从第一个稳定帧启用滞回，连续 8 个完整物理 Tick 才返回 true；阈值含等号。语义失败、待落地、待冲量、失去支撑、新破坏接触或非法速度清零。重复/倒序 Tick 返回 false 并清零、不回退最后 Tick；漏帧以当前帧重新开始计数；Reset 开启新序列。
- `ITacticalTimeAdapter.SetSimulationPaused(bool)` / `IsSimulationPaused` 是唯一世界时间端口，必须同步、无分配、不抛异常、不重入。Task 3 提供 Unity 时间适配；组合根须在完整 Tick 边界统一门控 Runtime、物理、动作及世界计时，保留物理状态。
- `TacticalTimeCoordinator(ITacticalTimeAdapter, ISettleDetector = null)` 实现 `ITacticalTimeCoordinator`，公开 `Phase / IsWorldPaused / CurrentTurnIndex`。`Initialize(ulong initialTurnIndex=1)` 只初始化一次，重复调用不抹除现场；起始回合不能为 0。
- `TryCommitTurn(deck, ledger, out committedAction)` 先进入 Committing，失败回 ReadyPaused，成功保存所属 deck、实例 ID 和提交版本，进入 Executing，仅释放 Ready 原因。Task 3 必须在调用前完成正式执行器准入。菜单打开时允许本地提交，但世界继续暂停。
- `NotifyActionStarted()` 记录当前正式执行的真实起手；`NotifyExecutionTerminal()` 进入 AwaitingSettle，不代替时间轴、控制释放或物理事实。调用方必须过滤其他/过期执行的通知；此 API 没有 execution ID 参数。
- `EvaluateSettle(in sample, deck)` 仅在 AwaitingSettle、世界运行且牌组匹配时采样；已真实起手后拒绝 `NoActionStarted`。停稳后通过冻结回执调用 Task 1 `CompleteTurn`，由其补牌，再递增回合并进入 ReadyPaused、请求全局暂停。重复完成无效；回执失效或回合编号溢出进入 ExecutionFault，不完成回合。
- `RequestPause(reason)` / `RequestResume(reason)` 按枚举原因记录幂等所有权，不是引用计数；同一原因只能由一个逻辑所有者管理。ReadyPaused 自身保持暂停，不能通过裸 Resume 绕过 Commit。RecoveryTimeout、ExecutionFault、BattleEnded 的暂停请求进入 Suspended。
- `ForceSuspend(reason)` 保留卡、已付费用、Action 配额和回合号，清除停稳累计，不补牌。多个挂起原因全部由各自所有者释放后恢复原阶段；菜单原因仍可保持世界暂停。故障恢复及战斗结束后的恢复授权属于调用方，协调器不自行判定恢复条件或超时。

上述协调器正常调用的 NoAlloc 断言属于对应聚焦测试，构造允许分配。下述是 Task 3 的历史适配边界；此后命中奖励由 T4-C 扩展，场景组合与战斗外 checkpoint 已接入，但完整战斗世界 SaveState/Replay 恢复仍不属于 Continue。

## HUD 取消输入所有权（Issue #753）

- 战斗 HUD 的原始 Esc／右键拖牌入口、`InputSnapshot.CancelPressed` 选点入口和取消按钮遵守同一 UI 输入所有权。设置或其他顶层 modal 打开时不提交后台 `CancelPending`，不清除待出牌身份、选点或费用预留；点选更新暂停并隐藏战场预览，恢复后重用原 Pending 状态。
- `CharacterTestUiRuntime` 从现有 modal 输入 scope 的进入／释放记录渲染帧，关闭当帧继续阻挡战斗输入，到下一帧自动失效。取消路由在调用顶层 focus bridge 前记录同帧所有权，popup 的 Cancel 优先于 modal，已关闭 popup 的同一次 Cancel 不再进入后台 HUD。popup 的普通 outside-pointer／目标点击仍沿原合同，不把所有 popup 点击变成全局 modal。
- 用户输入门控位于 HUD／BattleView 的入口，不放进通用 stage 或 `TryCancelPending`。内部 `CancelDirectPlay` 和已请求的自动失败回滚仍可清理并释放预留；同一 direct-play 取消已经成功入队后，选点、拖牌和 Tick 重复读取不会再次入队，入队失败仍可重试。
- 2026-10-12 实施方离线证据：基线 `5b3d87c306f092b4627058081b2949e3bbdb976e`，原样抽取生产 HUD／drag／点选／UI Runtime scope 与输入路由，在受控 Unity、UI 和命令端口中回归设置打开、关闭同帧、popup 优先取消、下一帧正常取消、双入口去重及内部清理，49/49 通过。隔离源码编译及 focused 测试源码编译通过，输出与依赖冻结在本地 `.agent/issue-753/`，不写共享 Editor 的 Library 或程序集。
- 原生回归入口为 `TacticalBattleCancelInputTests`、`TacticalDirectPlayTests` 及 `CharacterTestUiRuntime_Configure_EnablesSettingsPanelWhenSettingsRuntimeProvided` 集成夹具。新增输入夹具使用真实 InputSystem 按钮与生产 HUD／command pump，但受控 modal scope 不替代正式设置面板。Editor 仍由其他任务占用：Unity refresh／compile／domain reload／ready／Console、原生 Runner 与正式 `TacticalCardDemoScene` 设置开关／popup 操作均待交接后验证；离线通过不构成 GUI 验收或 Issue 关闭。

## 执行与 Unity 时间适配（Task 3）

- `UnityTacticalTimeAdapter(float simulationTimeScale=1)` 实现 `ITacticalTimeAdapter`，同步设置 `Time.timeScale`，保留可配置的运行流速；`SetSimulationTimeScale` 不会解开已有暂停。`PauseRevision` 提供轮询标记，避免回调重入协调器。不修改固定步长或刚体速度。正常暂停/恢复 NoAlloc；构造及修改流速拒绝非有限、非正值。
- 2026-10-08 世界启停过渡：正式单卡／混合玩家会话显式启用各 1 秒现实时间的加速和减速，以非缩放时间推进三次方曲线（加速 `p³`、减速 `1-p³`），并写入同一个世界倍率。原单参数构造和 `SetSimulationPaused` 保留立即门控语义；菜单、故障、战斗结束、会话交接仍立即处理。固定模拟步长保持不变，动画／位移／物理／弹道与战斗 VFX 继续消费既有世界时间链路，减速期间仍会真实发生移动和命中。
- 停稳回执接纳后，回合结算仍只执行一次；倍率尚未到 0 时界面阶段保持 `TurnSettling`，拒绝选牌／确认。组合根在每次 Update 以 `Time.unscaledDeltaTime` 推进世界倍率并在完全停止后转入 `Planning`，因此不依赖最后一个模拟 Tick 才完成停止。暂停期间不补跑积压时间。重复暂停请求不重启过渡，菜单打断会取消当前曲线，恢复按现有暂停所有权重新确定目标。
- UI 时钟独立：FairyGUI Tween 默认忽略引擎倍率，战术模式的浮字／引导表现使用非缩放帧时间；卡牌材质水纹和拖拽反馈继续使用非缩放时间。世界倍率不会改写 UI 的自身播放倍率。
- 战斗 UI 启停反馈：保留战场原有色彩与饱和度，以持续的粘稠水膜为底层，通过双层缓慢流动折射和沿流向的四次空间采样拖曳表现包裹感，外围强于中央；宽而柔和的同心涟漪只叠加在加减速过程，不去饱和或转为黑白，不叠加高光、色散或对比度闪动。加速时涟漪向外扩散，减速时从外围向内收拢；暂停保留完整粘稠水膜并极缓流动，加速逐渐退去，减速逐渐恢复。采用独立积分的表现时钟，暂停后仍有极缓流动，不通过倍率乘绝对时间造成纹理跳相；水膜与涟漪均只做同通道空间采样，不使用历史帧，不残留旧命中画面。主相机复用现有水影入口，UI 原色与清晰度保持独立，鼠标附近免折射；退出战斗或打开阻塞界面时停用该 pass。
- 鼠标免折射区域采用 Blit 源画面的左上角 UV；渲染前读取最新屏幕鼠标位置并按主相机 `pixelRect` 归一化，UI 稳定区域也使用同一视口原点，不能混用渲染目标像素坐标和 FairyGUI 设计坐标。鼠标移出视口时关闭局部豁免。当前 2560×1440 的四点 GPU 对齐检查与两种视口的八项坐标检查通过，临时测试标记已移除。
- 战斗 HUD 使用 `CharacterTestFairyGuiMeshBatcher` 在 FairyGUI `Stage.afterUpdate` 后合并同材质、同 layer/sorting layer 且 sorting order 连续的 Image/Text 网格；材质属性块、特殊 Shader、模板写入/擦除及不兼容网格保持独立，保留现有遮罩、字体、控件树与命中逻辑。网格与输入缓冲复用，禁用/隐藏/销毁时恢复原 renderer 并释放合并资源。新增或移除该 HUD 内的普通控件自动参与重新分组；当前只接入战斗 HUD，不宣称已优化其它界面。
- 2026-10-08 合批实测：同一暂停战斗帧启用/禁用合批，Stage Camera 的透明 UI 绘制从 56 次降为 26 次（减少 30 次，约 54%），保留三张卡的遮罩与独立卡壳材质。2560×1440 静止/悬停 A/B 截图每通道平均绝对差分别为 0.054/0.052（0–255），非逐像素完全相同；拖拽预览及隐藏取消/恢复检查通过。预热后在 Editor 同线程连续 200 次 Refresh 测得 0 B 托管分配、平均 0.078 ms；这是局部微基准，不代表整帧耗时或目标设备帧率收益。帧调试器与临时开关均已恢复，不修改 URP 资产。
- 手牌在减速中随实际静止程度共同上浮最多 6 个设计像素，并增强已有晶石边缘的明度，不改品质色或卡片横向槽位。完全停住且进入可操作 Planning 后，薄边有一次 280ms 短亮；文字依次表达“正在减速／选择行动／正在加速／执行中”。出牌提示仅在真实可决策状态出现，反馈不会提前放开输入；原拖牌接纳／取消逻辑保持权威。
- 力量感与世界速度采用一致的三次方进度：读取真实过渡的归一化进度，滤镜与方向波前使用 `p³`，前段蓄势、后段持续加快，末段不 ease-out。加速波前从中央向外冲开，减速波前从外围向内收紧，涟漪包络在画面范围外进入和离开，中心收拢时减弱位移；取消放射条纹、120ms 对比度冲击与高速流动，结束不闪屏。世界倍率同步按 1 秒三次方曲线推进，完整加速为 `p³`、完整减速为 `1-p³`，反向过渡从当前倍率接续，出牌门控不变。表现被菜单／故障打断时直接清理，反向过渡从当前滤镜程度接续。
- 首次过渡 Tick 只计算请求发出后的真实时间，避免确认前的同步卡牌准备长帧直接耗尽整条加速曲线。2026-10-08 先前 150ms／250ms 参数下的真实混合战斗出牌记录中，加速采样为 0.100、0.241、0.389、0.539、0.674 后到 1；减速有 14 个中间倍率后到 0。所有倍率大于 0 的样本均不可出牌；最终暂停段世界 Tick 固定为 144、世界时间不变，独立 UI Tween 完成到 1。这是原参数的验证记录，不代表新版 1 秒参数或所有特效／设备的逐项视觉验收。
- 此次相关 EditMode 回归 81 项中 80 通过；新增三项过渡／出牌门控／菜单中断测试全部通过。`ReleasingTacticalControlPreservesTheMenuOwnerAndResources` 仍失败，已在未修改 main 上复现同一失败：现有 `ReleaseControl` 会取消 PendingAction，与该测试保留待出牌的断言冲突，本次未改动该行为。
- 提交验证边界（2026-10-08）：同步到远端 `02d076cb5` 后，23 个本地正式文件无冲突恢复。同步前最终版本已通过 Unity refresh/compile/domain reload/ready 与 Console 0 error，并完成上述 Playable 合批/交互实测；同步后 Unity MCP 持续断开或超时，未取得完整重新编译证据，相关回归启动也未返回可追踪 job，因此不将历史 80/81 或同步前实测表述为合并后回归通过。代码保留既有 ReleaseControl 基线失败，不改玩法断言掩盖它。
- `ITacticalExecutionPort` 提供 `TryPrepare(card, out issue)`、`TryAdmit(card, out executionId, out issue)`、`Observe(executionId)`、`TryAbortPreAction(executionId, reason)`。Prepare/Admit 为 LowFreqAlloc，Observe/Abort 为 NoAlloc。`ExecutionObservation` 包含 ExecutionId、ActionStarted、ActionTimelineCompleted、BehaviorTerminal、OwnedIntentsReleased、CommandsAcknowledged；未知/故障执行返回默认值，不伪造终态。
- 内部 `CharacterTestTacticalExecutionAdapter` 由组合根注入 Runtime、正式命令模块、Session、不可变行为源、注册的 Profile ID、实体及时间端口，可选注入权威 Rigidbody。Profile ID 表示这份明确注册的 Session/source 绑定，不解析成任意文件路径。当前复用的 Session 支持 `AttackPrimary + Enemy` 与其已编译的 `OptimalRange/None` 接近策略；其它槽位/目标模式在扣费前返回 `UnsupportedSessionBinding`。`None` 仍等待正式距离/朝向 Gate，不等于无目标原地攻击。保底架刀等额外绑定需要相应正式行为源，不能由适配器伪造完成。
- Prepare 校验并编译，保存卡引用、版本和目标身份；Admit 复核当前绑定、目标和版本后 `Session.TryStart`，不推进 Session、提交动作命令或步进物理。`SetContext(facts, nextFrame)` 提供正式帧与事实；`Tick()` 在世界运行且帧递增时驱动 Session，起手前目标丢失请求安全清理，起手后不会转向另一个新目标。
- Published 战术卡在暂停的确认边界准备输入；派生动作摘要缺失或任何 `Stale` 过期原因都进入自动重建流程。整备／浏览阶段复用按帧预算推进的测量队列，同一角色、动作、内容、目录与装备版本共用任务；已完成任务的缓存再次过期时重新排队，目录或装备变化则取消旧任务并按最新输入重新准备。战斗中发现过期时，在暂停的确认／准入边界通过现有 reference probe 事务兜底重建并重新校验，重建成功才提交动作和付款；只读预览不做同步测量。正式源动作／烘焙数据缺失、输入身份不匹配或真实测量失败仍保留明确原因，不使用过期摘要强行执行。名义测量的内容指纹包含正式动作、角色体型、烘焙帧与骨架定义，不包含战斗后仍在恢复的临时武器挂点偏移；真实碰撞仍读取当前权威姿态。 多个命中窗口之间的非攻击帧保留完整位移轨迹，但不生成命中采样；预览计划携带明确的必需命中帧清单，真正活动帧的轨迹缺失仍拒绝重建。直接出牌准入拒绝时，HUD 在自动取消与释放预留之前保留可读反馈，避免取消事务清除拒绝状态后原因消失。AI Lab 行为源存在非移动辅助动作槽时，玩家实例可装配一个动作并在部署快照中覆盖制作默认 resolver；无玩家装配时不得伪造绑定。`stepBack` 仍由内部移动逻辑解析，不投影为第二个玩家槽。
- 起手来自正式命令的动作实例回执，回执到达时保留该权威实例的只读引用；即使一帧动作已被 Gateway 移除仍可读取终态。完整时间轴完成只接受该实例的 `CharacterActionInstanceState.Finished`；Cancelled/Interrupted 进入故障，不能把实例消失、取消窗口或行为结束当作自然完成。现有 `TimelineEvents` 在伤害阶段再次绑定世界时会清空，不作为帧末完成依据。Abort 拒绝已起手及尚有待消费命令的执行；使用 `Session.Stop` 请求释放，完整帧结束后产生的清理安排到下一正式 Tick。只有行为终态、队列已消费且实体移动/转向意图已释放，才报告释放完成。
- `SetPhysicsSample(tick, supportBody, contactPoint, hasNewDestabilizingContact, hasPendingImpulse)` 由物理所有者在完整帧结束提供。`BuildSettleSample(tick, entity)` 使用同帧事实、实体 IsGrounded/中立运动语义/受击恢复/待落地/待运动请求，并独立读取战斗状态生成 `PlayerCanActFreely`。物理缺帧保守拒绝物理停稳，但不否定有效实体的自由行动权限；实体不匹配时两种检测均拒绝。可选 Rigidbody 读取线/角速度；无 Rigidbody 使用实体确定性速度与连续 60Hz 权威 yaw 差。移动支撑物使用 `GetPointVelocity(contactPoint)`（包含旋转贡献），角速度同样相对支撑物计算。无刚体首帧/漏帧的角速度不可确认，不累计物理停稳。
- `TacticalSingleCardDriver` 连接端口、牌组、账本和协调器，使用匹配执行 ID 的观测覆盖采样中的执行事实；重复提交/完成无效。`OnCompletedSimulationTick(in sample)` 在命令、动作/运动、物理接触、奖励和执行回执之后调用。默认未起手预算 240 Tick、总执行预算 300 Tick（T4-D 将原 Task 3 的 1800 Tick 默认值收敛为 Demo 预算），按实际世界 Tick 差累计；超时前置清理不截断已起手动作，总预算耗尽挂起并保留 Resolving，不补牌。保护每次执行只触发一次，显式恢复后允许继续累积完整停稳窗口，避免每帧重入挂起形成死锁。菜单暂停独立保留，暂停期间不累计预算。

组合根顺序：初始化时间协调器 → 选择 PendingAction → SetContext/TryCommit → 在时间门控允许时 Tick Session → 消费正式命令并步进 Runtime/物理 → 处理奖励和回执 → SetPhysicsSample/BuildSettleSample → OnCompletedSimulationTick。**自定义 Runtime/手动物理驱动也必须轮询时间端口，暂停期间不得积累待追赶时间；单独设置 Time.timeScale 不代表已有场景的所有自定义时钟均已接线。** 这是 Task 3 交给 Task 4 的历史接线要求；当前混合切片已由组合根接入，仍须遵守同一正式 Tick 与暂停门控顺序。

## 分配与验证证据

Task 3（2026-09-11）：Unity EditMode 作业 `f2c5db6a3ee6467188013ff2d8c10701`，`TacticalExecutionAdapterTests` **14 passed**，Task 1 **11 passed**，Task 2 **35 passed**，合计 **60 passed / 0 failed / 0 skipped**，耗时约 47.69 秒。本地报告：`Library/MxFramework/TacticalCardTask3/2026-09-11-Task3-60-passed.xml`。真实执行用例使用当前装配首段的测量摘要、隔离 Runtime、正式命令队列和动作 Gateway，已验证起手、权威 Finished、控制释放、连续停稳和补牌到 3 张。

Task 3 分配测量：预热后 1,000 次 Unity 暂停/恢复、1,000 次实体采样/观测、1,000 个模拟端口的完整单卡回合分别为 **0 字节**；真实 Session 执行用例中，前 17 次调用后测量 Tick 与完整帧适配/停稳调用同样为 **0 字节**。不包含编译、准入、测试夹具、世界步进及初始化；不外推到 IL2CPP 或尚未接线的场景组合根。测试同时覆盖未起手清理等待命令消费、旋转支撑接触点速度、空中/缺帧/错误实体、异常暂停恢复后可以正常完成停稳窗口。

Task 3 最终源码编译门禁作业 `28cf27769d6c4df780394fa8884df227` **passed**：refresh completed、compilation started/finished、domain reload observed、Editor ready，compilation errors 为空、Console error count=0。域重载造成首次 MCP 响应断连，恢复后通过同一作业 status 取得完整通过回执。新增源码与测试的 `.meta` 均由 Unity 导入生成。未修改场景或执行 Task 4 的 HUD/1v1 三回合视觉验收。

测试结束后 Console 工具再次将 `Saving results to: ...TestResults.xml` 这一条无堆栈的结果保存消息报告为 Exception，与实施前记录的基线同型。保留该工具输出；编译门禁的 0 error 回执与 60 项测试 Passed 报告分别成立，不将测试后查询描述为完全空白。

Task 2（2026-09-11）：Unity EditMode 作业 `e3362c6ccc9a436e8eb296c40927d1f6`，`TacticalTimeCoordinatorTests` **35 passed**，Task 1 回归 **11 passed**，合计 **46 passed / 0 failed / 0 skipped**。预热后 1,000 回合覆盖暂停/恢复、提交、起手/终态、挂起恢复、8 Tick 检测、完成、补牌及回洗，线程托管分配为 **0 字节**。仅证明本次 Editor 环境下纯 C# 核心与测试适配器，不外推真实 Unity 适配器或 IL2CPP。

Task 2 编译验证作业 `f45f82f9c9b4443488e114eb46530bb9`：refresh completed、compilation started/finished、domain reload observed、compilation errors 为空；域重载使 MCP 请求断连，补查作业最终 timed_out，但回执确认 `editor_ready=true`。测试前独立 Console 查询为 0 error；测试后工具报告一条无堆栈的 `Saving results to: ...TestResults.xml` 为 Exception。保留这一工具观测，不将单次作业声明为通过。三个新增脚本的 `.meta` 已由 Unity 导入生成。

构造及初始化是允许分配的冷路径，包括定义、实例、列表、只读包装和输入枚举器。初始化按整副牌规模预留各目的地容量；出牌、取消、提交、完成、回收洗牌和索引查询不使用 LINQ、闭包或新建集合。洗牌使用管理器私有 xorshift32 状态。

2026-09-11 Unity EditMode：`TacticalCardEngineTests` **11 passed / 0 failed / 0 skipped**；测试作业 `9a7ca0931cc64bd4994baa2d92eefad4`。包括初始化、手牌保留、补牌/回收/移除、预留/取消/支付、单 Action、重复提交、过期回执、非法输入及预热后 1,000 回合 **0 字节**线程托管分配。该测量不包含初始化，也不代表 IL2CPP 或未来外部适配器的分配结论。

Unity 验证作业 `57c612f037c349bfbf054abbda23ce24` 已记录 refresh completed、compilation started/finished、domain reload observed；域重载期间 MCP 断连，单次作业最终超时。恢复后状态回执确认 `editor_ready=true`，测试前 Console 错误数为 0。测试后 Console 工具把一条 `Saving results to: ...TestResults.xml` 报为 Exception，无堆栈；该工具输出与测试通过结果分别保留，不将单次验证作业描述为成功。


## 任务05临时几何、预览与移动验证（2026-10-02，结算仍待定）

本节是当时的增量快照；最新主集成与验收状态以文末“任务05主集成”章节为准。当时仅查询与显示基础、一个显式 opt-in WorldPosition 导航分支受控接入主项目，没有接入新的出牌结算、启用或发布选点卡。

- 主项目新增 `TacticalExecutionInput.cs`、独立 `TacticalPointSelectionPort.cs`、`CharacterTestTacticalPointNavigation.cs`、`TacticalPointSelectionPreview.cs`，以及只由显式调用启动的 Editor `CharacterTestSelectedPointValidationFixture.cs`。修改 RuntimeBridge.Execution 的 WorldPosition 分支：仅当注入导航实现该可选接口时调用它；普通路径保留。未覆盖共享 Driver/Deck/Stage/Adapter/CardDefinition/HUD/Session。新增 `.meta` 由本轮 Unity 导入生成。
- 查询复用真实 Combat Physics 与原有有界 A*；可视圈抽成共用渲染器，隔离 HUD 已改用同一渲染器，临时 fixture 不复制算法。真实查询发现胶囊净空与地面相切导致误拒；现按实际 skin、至少1毫米工程净空缩入胶囊上下端，修复已同步隔离源码。不是移动范围或结算默认值。
- 在未保存的空白场景 Play 中，复用原 reference probe 隔离 Runtime、作者编译器、执行 Host、正式命令队列及运动/物理系统；fixture 自行按60Hz推进真实模拟帧，Unity对象仅映射权威姿态用于显示。没有启动原生产场景的玩家 bootstrap，没有连玩家整备或存档服务。
- 18个去重 fixture 检查通过：合法射线落点/平地、拒绝空中和缺失地面/墙面法线、直达、无导航阻挡时fail closed、有界A*绕障查询；5米范围/0.2米到达圈测试参数（非卡默认）、绿/红候选及隐藏；WorldPosition从Z=0移动到2366毫米，40 tick进入距目标2500毫米点134毫米处，Host Completed；完成、主动Stop、预算超时释放，无活动移动且命令已消费，允许既有 `IsRelease` 观测标记；旧owner释放保留新owner移动，匹配owner释放清除。另4项既有 EditMode控制/旧执行回归通过，作业 `dfd86d1c68ff4499aca02d778012fa72`。
- 编译作业 `b1186175d0c04b3887a8394b1dc0e1cc`：refresh/compile completed、domain reload observed、Editor ready、编译错误0；保留既有UAC0005警告被Console工具归为Exception的记录，清空后同作业status确认error_count=0，运行及回归后查询仍为0。不是零警告声明。
- 可复查证据在主项目 `.agent/SelectedPointLive/`：`receipt.json`含本次6份源码、meta、实际DLL及截图SHA256，`integration.diff`为实施前主文件到增量后的diff；`runtime-final.json`、`regression.json`、`verifiedcompile.json`与`pretestconsole.json`保留实际结果。有效截图为 `valid-candidate-camera.png`、`invalid-candidate-camera.png`、`movement-final-camera.png`、`hidden-preview-camera.png`。早期ScreenCapture为蓝色空画面，不能作为验收图。截图是临时相机真实渲染，非生产HUD。
- 已销毁 fixture Runtime/命令/材质/预览，退出Play，重新打开 `TacticalCardDemoScene`；dirty=false、临时对象0，主相机位置(0,56,-80)、FOV60恢复。除本任务两个文档追加记录外，本次实施前已有修改文件、渲染配置及ProjectSettings文件hash未变。persistentDataPath中560份文件只有Unity Test Runner输出 `TestResults.xml`变化，其余559份含玩家/草稿文件hash一致，没有存档写入。
- 限制：未验真实点击、完整生产场景地形/坡面遮挡/动态障碍、新路径的A*实际绕行移动、不可达图、生产扣费/冷却/卡组、自动Provider执行。源库仍有完整隔离增量，当前第一期仅这三项独立临时验证成立。纯移动结算选择保持pending。


## 任务05主集成：地点执行输入与纯移动结算（2026-10-03）

最新合同覆盖此前“仅隔离／结算待定”快照；生产卡发布仍未启用。完整范围与回执见[任务05](../../StorySource/玩法基石/任务/任务05_前往选定点执行输入与手动流程.md)。

- `TacticalExecutionInput`仅属于本次Pending／请求，保留StageRun／Turn／CardInstance／CardVersion和带generation角色Binding，Manual／Automatic来源，整数毫米固定点；不写共享卡、动作槽或存档。共同校验区分缺点、错误类型、陈旧身份、角色失效、OutOfRange、地面及路径不可用。
- 原Stage／CommandPump消费SetExecutionInput。选点不新增预留；取消清空输入并释放同一预留。确认owner捕获当时中心并冻结点，prepare／admit与开始Tick按权威事实复查；不随机、转敌人或用零坐标兜底。
- `TacticalPointMoveRules`需显式提供范围、到达、预算、速度及可达需求。纯移动沿真实CombatPhysics、有界A*、作者compiler、Session及原控制命令执行；`ActionStarted`保持false。
- 2026-10-09 修正地点移动结束边界：`WorldPosition` 的显式 `Arrived` 段保留到达即完成语义，不进入旧普通目标移动的定时兼容。到达圈内即沿 Session 释放控制并结束行为；`MaximumFrames` 仅限制未到达时的执行预算，不要求到位后继续等待该时长。正常回合随后只检查玩家自由行动权限并开始既有减速过渡。
- 点执行确认后延迟支付原Ledger预留：Deck持有Resolving reservation，`ExecutionObservation.ExecutionStartedTick`由消费端匹配owner的首条非零移动接受事实锁存。Driver观察后提交同一预留，卡冷却从接受帧开始；开始前失败释放，开始后失败保留费用／冷却。入队不代表开始，同帧后来owner替换不能抹掉接受事实。旧无输入卡的公开CommitTurn入口语义保留。
- `ITacticalExecutionInputPort`／`ITacticalPointSelectionPort`是可选扩展；HUD Bind由组合根传入原IInputProvider，读Click／Cancel Snapshot。选卡同帧过滤和发布BattleView的实际可见对象几何防止UI点击穿透，不改卡面布局；临时范围／到达圈没有Collider、不写资产，Dispose先隐藏。
- 终结／停止沿原Session／命令消费／Time owner释放，不清后来owner；失败原因进入原Driver／Stage／HUD。显式ReleasePointExecution用于控制清理，目前不另生成主动停止HUD文案；范围／目的地失效与预算超时有具体原因。
- `ITacticalPointProvider`只交付候选点或Unsupported／PointUnavailable。Stage／Driver提供身份、中心、规则并共同校验；构造Automatic输入不确认、扣费或创建冷却。未配置生产Provider／复杂AI。
- 主树已具备显式地点定义／部署的手动运行能力；普通整备尚无批准发布的PointMove卡源与玩家实例。本轮只用临时验收卡，不宣称生产作者发布／安装链或自动／混合模式已完整启用。

验证：原事务及真实世界的到达、开始前失败、开始后停止、超时、动态目的地占据、同帧owner替换各有独立Play回执；最终发布FairyGUI＋原InputService通过Windows注入点击完成选卡／选点／确认／到达，取消回到原余额，14项检查通过。OS注入、Snapshot注入、人手硬件点击分别记录。另52项focused受控检查的17份链接生产源码与主树一致；不做全量Unity重跑。最终编译作业`55764a24c8424e9096af8c85877f18ae`实际错误0，既有UAC警告误分类回执保留，清空后同作业passed／Console0。细节及版本哈希位于`.agent/SelectedPointFormal-20261003/receipt.json`。

## 定点牵引首个试用切片（2026-10-07）

一句话意图：**将自己牵引至本次选定的地面点，遇阻即停。** 这是战术行为模板的机制组件；本次目的地通过执行输入提供，不能把 input intentId 当成战术意图。

作者源 v4 的 `executionInput` 延续 WorldPoint 载体，并显式使用 `mechanism="GroundTraction"`、`startPolicy="OwnedTractionAccepted"`。范围中心 Execution，requireReachable=true；牵引速度/加速度分别由 `tractionSpeedMillimetersPerSecond` / `tractionAccelerationMillimetersPerSecondSquared` 配置，禁止旧 walking `speedMilli`。本次合法试用卡为零动作槽、空事件时间线；它走独立牵引命令，不调用 PointMove 行走。共享定义中任意位置插入牵引尚未接入。

本次参数：范围6000mm、停止距离250mm（整数求解到达容差1mm）、最高速度9000mm/s、加速度54000mm/s²、预算90帧、动势费用3。无物理动作绑定，所以不产生动作冷却；绑定动作的其他卡继续受动作冷却资格约束。手动点选和自动 provider 使用原 WorldPoint 输入绑定与准入；自动 provider 缺失时明确失败，不复用旧手动点。确认和命令消费均复核角色generation、范围、落地点净空与直线扫掠；不绕路或穿墙。

命令 `901025` / envelope `901105` schema1 携带 stageRun、executionId、actor index/generation、冻结锚点和运动预算。角色组件独立保存牵引速度与终止状态。每帧仅叠加自己的程序位移，仍由现有 kinematic motor 处理碰撞和地面；预查询不写 world body，持久外力速度采用无本次牵引的求解结果。首个 started 收据要求牵引相对于该预查询的真实正向位移；排队或提交请求不算开始。

到达、受阻、取消、预算耗尽、死亡/generation丢失、落地支持丢失、控制/动作占用均终止。终止清理自己的运动状态，不清零共享外力；开始前终止释放预留费用，开始后保留已付费用，正常结算并补牌一次，不制造 ActionStarted。当前世界快照schema40及rollback保存/hash包含牵引锚点、身份、速度、started/terminal receipts及独立回返状态；恢复拒绝晚于快照帧的记录。未新增独立通用 RuntimeSaveState provider。

现有 idle/移动表现随程序位移复用，无新动画资源。首版为地面直线牵引，不提供空中抓点、绳索、替身或体型能力。牵引改变实际位移；其他外力的碰撞速度以同起点无牵引查询为基准，不承诺完整运动通道的接触贡献分解。

编辑模式作者入口仍为 `MxFramework/CharacterTest/Tactical/Publish Ground Traction Trial`，复用Main工作区及原preflight→publish→目录重读→草稿checkpoint，不改行为组、默认牌组或绑定。当前D417/40、r1已经实际发布；玩家在混合Demo整备经「更多 → 领取机制试用卡」明确领取，再从当前卡库装配，不依赖旧左右两栏编辑器。发布与领取是两个事务，不会自动授卡。正式发布、机制27项与领取装配3项证据分别见[任务07](../../StorySource/玩法基石/任务/任务07_定点牵引试用切片.md)。


## 位置回返首版（2026-10-07）

新增两个明确零动作槽卡层合同：`positionRecall.operation=Record/Return`，与 executionInput 互斥。记录冻结个人当前整数毫米点，默认600模拟帧、一次成功返回；返回精确原点，当前位置 yaw 保留，成功停止普通移动。两张试用卡各3动势；失败不消费有效记录或费用。运行动作/恢复、空中、活动牵引、待外力/战斗请求拒绝；不取消攻击后摇。固定自动说明避免同一卡依状态变换含义。

正式命令901026/envelope901106绑定stageRun、executionId、actor generation，独立状态由 ComponentWorld保存。Motion owner 查询真实静态支撑与完整AABB净空后同时提交pose/velocity/collider，清除该角色旧Presence/附肢及caster前帧见证；被占据不换邻点。真实recorded/returned收据产生started与terminal，Driver仅实际成功提交原预留，ActionStarted始终false，旧卡策略保留。

快照schema40追加稳定hash字段308—324及完整收据；schema39迁移清空回返，未来帧/伪造收据拒绝。模拟暂停不推进有效期，死亡/复活、战斗结束/场景dispose清理；未加独立RuntimeSaveState provider。首版仅玩家controlled部署，非玩家自动入口明确拒绝。

网页目录提供 `position-recall-record`、`position-recall-return`。独立菜单 `MxFramework/CharacterTest/Tactical/Publish Position Recall Trials` 走原workspace/preflight/publish/目录重读，先prepare草稿及真实allocator身份、生成ID并编译后发布，不改默认牌组/已有装配。当前记录回返点D417/41与返回记录点D417/42均已正式发布r1，原生回返测试及整备三卡领取/保存/装配已有实际回执；完整机制自然试玩仍未据此签署。精确版本及证据归[任务08](../../StorySource/玩法基石/任务/任务08_位置回返试用切片.md)。


### 三张机制卡的显式试用领取

当前整备视图“更多→领取机制试用卡”只列定点牵引D417/40、记录回返点41、返回记录点42的精确r1/hash。它调用现有TryGrantCardForTesting→AddDeckEntry→FlushAsync，不改变默认获取范围或默认牌组。精确版本已持有时不再授予；角色/entity、弹窗序列与view generation复核；保存失败可重试但不重复创建实例。换进度服务时重新绑定，旧回调失效。关闭后在当前卡库通过卡面＋加入玩家控制实体出战；零动作槽不要求额外绑定动作。新入口复用现有已发布popupPanel/popupList，不引入运行时替代面板或旧库存窗口。

原生MechanismTrialAcquisitionUiTests验证正式三卡领取→保存重读→当前卡库装配、旧角色/弹窗点击拒绝、保存失败重试不再授卡；3/3通过。存档验证使用独立MemoryStore和启动隔离目录，不调用用户清档。


## 五张固定交互效果试用卡（2026-10-09）

正式D417/45–49 r1采用v5 exclusive `interactionPreparation`源和固定trial.v1规则，真实Utility基于公共发布ID/revision/hash/entry instance装配，既有compiler/publisher/authoring owner/目录与Lab Freeze入口全部贯通。费用5、追击基础伤害8、奖励3、额外散架压力12、期限600模拟帧均为试用配置。成功支付并Exhaust，不启动动作、不推进回合/世界、不补牌；玩家不修改触发/清理/叠加规则。

权威Combat提交区分合格根命中、普通Blocked和实际HP损失，去重与防衍生递归。Motion回返位于全部producer之后/运动消费之前，等释放并重查完整净空一次。R4联合私有暂停envelope v3覆盖全部actor effects与同一ledger/deck/config/hash，受限两actor临时Lab恢复，不扩大一般世界/Items/磁盘/网络恢复。

Main完整150/150原生（0失败/跳过），原canonical23及断言保留；真实五卡资源经Lab出牌事务验证。菜单为 `MxFramework/CharacterTest/Tactical/Publish Fixed Interaction Trials`，不自动发放库存。正式身份、精确合同、局限及失败修复记录见[五卡R4交付](../Tasks/CHARACTER_TEST_INTERACTION_PREPARATIONS_R4.md)。


## 2026-10-09 动作冷却与费用收尾 C1 源码收尾

本段记录重基 `cbc1409f4e1ce25f54b9aceb4af709c3bd3e14e4` 后合入的源码；离线回归与原生验收分开记录。C1 只修改冷却归属，不改平衡数值、费用提交时点或正式存档/网络 schema。

实际物理动作的冷却由 actor 的既有 Ability owner 查询、提交和恢复。同一角色不同卡引用同一实际动作共用该冷却；物理动作卡的旧 `CooldownSeconds` 和 `SharedCooldownGroup` 继续可读取，但不再创建卡或组使用间隔。Action slot 和精确发布行为同样不得另开物理卡冷却。

纯选点移动没有物理 Action/Ability owner，使用任务05已批准的机制合同：保留精确定义的配置冷却，从正式控制 consumer 首次接受本执行自己的非零移动帧开始；确认和命令入队不触发。开始前失败释放原 Ledger 预留，开始后失败保留卡费及机制冷却。定位回溯、定点牵引和五准备的既有零冷却配置保持原值；不借本次修复为它们添加新的数值。

`TacticalCooldownTracker` 只服务上述 PointMove 机制，暂停快照恢复的允许集合使用同一判断。含物理卡级冷却的旧私有候选输入明确拒绝；不删除或迁移正式用户存档。受限 v3 完整帧、新 world、双存活落地角色及临时 Lab 边界继续有效。

费用仍使用 actor 已绑定的同一 `MomentumLedger`。普通卡确认付款、正常结算时未开始退款；点移动/定位回溯延后付款。实际动作的 Ability 成本在动作提交时重新检查并付款，与动作 CD 一起生效；不把每个 recipe binding 当成必执行一次而全额预扣。耐力费用保留属性语义。内部故障保持现场，不伪造正常结算。

独立验证：1079 Runtime 源码、362 引用编译通过，输入 hash 在编译前后稳定；纯测试 86/86（既有快照69 + 新增冷却/预算17）通过；含修改后的 native 适配测试在六个测试/fixture 源文件中编译通过，原生执行0。新测试证明同 Ledger 的预留保护、混合成本失败不写动势、重复 cost 累计/溢出、耐力独立和旧 actor generation 拒绝。以上为重基前的独立证据，本批已在用户授权窗口合入Main；没有使用Unity。这些结果不签收当前武器门禁、真实场景接受回执或一般恢复。合入后由唯一 Unity owner执行刷新/编译与命中 native tests。


C1重基后离线回执：基线 `cbc1409f4e1ce25f54b9aceb4af709c3bd3e14e4`，当前1079/362 Runtime编译通过，86/86纯回归通过，规范扫描无新增问题。当前七个native测试/fixture源文件及364引用编译通过、原生执行0。使用这批新编译Runtime与测试程序集在独立Mono执行用户出牌修复三项（显式WorldPosition提前到达、末尾卡行为释放保留物理Recovery、后续编排不提前跳过），3/3通过；该离线结果不替代实际场景及后续出牌原生验收。没有Unity操作、提交、数值配置或正式ID变更。自由Recovery可进入Planning，但私有v3恢复仍拒绝活动动作，本次没有扩展恢复范围。原生精确过滤与逐文件hash见本批 `action-cooldown-cost-closeout-main-r1/native-validation-plan.json` 交接回执，由唯一Unity owner统一执行。

## 固定战胜奖励接口（2026-10-11）

奖励r2接口已进入当前工作树；玩法操作及数据owner见[角色战术内容](../Guides/CHARACTER_TACTICAL_CONTENT.md#固定战胜奖励2026-10-11)。不新增框架模块，不改变单项授予的逻辑提交语义。

| 接缝 | 已实现合同 |
| --- | --- |
| `FreezeBattleRewards(recipe, battleId, characterId, owner)` | 低频、detached。读取正式精确引用/获取范围，固定稳定每项请求、内容/请求指纹及显示；不授予、不写盘。输入配方不得带运行/玩家身份 |
| `ClaimBattleRewardsAsync(progress, battleId, RuntimeTickContext)` | 从应用Simulation命令进入。Busy/Rejected/SaveFailed不发布整批；Claimed在同checkpoint持久后发布；AlreadyClaimed不写盘、不新建实例。领取期间阻挡现有player/config mutation，所有observer在提交之后 |
| `ProgressCandidate.ClaimRewardsAsync` | 内部player gate与profile write gate配合；库存/allocator/ledger/preparation/Claimed一起Commit。排队普通Save取得gate后捕获；profile Revision及原SaveGame CAS拒绝过期writer/archive |
| settlement Rewards | `mx.character-test.battle-rewards.v1`固定清单；Pending/Claimed与终态Receipts。BattleId+固定清单指纹不允许重复回调改载荷，旧Pending回调不能降级Claimed |
| progress容器 | 新`mx.character-test.tactical-slice-progress.v2`；原slot/codec及player v5/v6继续使用。v1无奖励记录不追补，v1带Rewards拒绝。收据必须能在真实获取ledger/对应实例记录中复核 |
| 菜单 | 先判SettlementSaved：未保存为“结算尚未保存”/Busy“正在保存结算…”，确认提交后才待领取。显示固定清单及明确角色实例；确认领取、处理中禁用、失败重试、成功返回整备。Pending不能同配置重赛，可保存退出。重开优先恢复原Pending；不恢复战斗现场 |

配方当前只含1张正式`tactical.bound-action` r2/hash `7107346772390999808`，测试角色归属来自deployment唯一玩家控制实例。四类批授予可在明确、已发布且适配的配方中验证；没有配件正式配置就不能发配件，也不扩大角色目录。随机奖励、下一关、奖励经济和全队分配不在本轮范围。

已核验：实际源码离线库存回归132/132（113既有库存回归、19奖励）、Runtime菜单/保存屏障13/13；完整Runtime离线编译通过。主树Unity Editor直接fixture 33/33，原生EditMode Runner 33/33（19事务、13菜单、1正式配方与真实物理存档组合根）通过。Composer publish-all成功，8个新增资产meta由Unity导入。main同步后编译10程序集无错误，refresh完成、domain reload、Editor ready与Console error=0均有证据；MCP跨重载断线，恢复状态为timed_out，保留工具超时限制。Play、GUI故障重试、冷组合根重开和尺寸视觉仍逐项验收，不将Runner或.NET强杀探针计为Unity Player进程重开。

失败回读将磁盘 checkpoint、候选快照与已发布快照统一从序列化 JSON 解析后比较，避免 ISO 时间字符串的 Date/String token 差异误判外部写入。未写入的失败保留原状态，支持同一服务实例直接重试；候选已落盘但完成回执丢失则确认该 checkpoint，真实分歧仍拒绝覆盖。

追加验收（2026-10-11）：保存回读统一解析序列化 JSON，修复 ISO 时间 Date/String token 差异导致的伪写入冲突；同服务失败重试与物理写盘后回执丢失回归覆盖。Unity完整refresh/compile/domain reload/ready与Console0通过，原生EditMode Runner现35/35。受控已持久化Pending夹具通过发布FairyGUI绑定回调验证重复点击仅一次保存、处理中不发布库存、保存失败完整回滚、同服务重试整批Claimed、返回整备，以及全新物理store/服务组合根冷重开后AlreadyClaimed不写盘；角色实例1卡6→7、新卡未绑定、其他角色未变。实际GameView的1920×1080/1280×720待领取、处理中、成功清单与归属完整可读；失败长诊断尾部有裁切。实际自然战斗仍未观察到玩家胜利（剑角色456/457与法师测试装配败北，458开放上界及461缺前摇拒绝原因另记）；不把该受控Pending夹具、Runner或冷组合根作为自然胜利、OS输入或Unity Player进程重开。保存结算重试成功后旧失败文案残留和长失败文案显示修复仍待办。证据级别与回执保留于本地 .agent/tactical-rewards-20261011/native/resume2-acceptance-receipt.json，真实玩家存档未修改。

显示修正（2026-10-11）：结算保存成功后恢复实际胜负/平局提示；发布菜单以短提示呈现未保存结算或未完成领取，完整内部失败原因继续保留于原StatusMessage/ProgressIssue。处理中仍优先显示进度，固定清单与归属不删减。本次不改变保存事务、奖励状态或schema。已验证同服务保存失败→正式菜单重试→成功清除旧提示，以及1920×1080/1280×720领奖失败短提示、奖励清单和角色归属完整可读。Unity默认compile证据完整通过，Console0。

本阶段真实胜利入口已通过确定性弱敌夹具：隔离存档rwd1011-ui03，受控角色实例1/owner1合法学习并绑定正式action:457；调用正式TryDeployPreparedCore，在Deploying且行为未安装时零delta初始化tick生成双方，再以World属性接口预置敌方HP1，玩家HP100。没有战斗中改血、调用CompleteBattle或注入胜负。真实HUD出牌至敌方HP0/玩家HP51，正式判定阵营1获胜并生成原BattleId的Pending固定奖励；领取失败保留全量player，重复回调只产生一笔重试，成功后主卡6→7、Bindings4未变，正式返回整备。故障显示验证在自然胜利已保存的Pending上构建全新带gate的store/服务组合根，再由同服务真实保存失败和重试产生状态，未替换活动readonly store。证据见本地.agent/tactical-rewards-20261011/native/resume3-acceptance-receipt.json；这是Unity Editor内正式绑定回调与渲染验收，未执行OS鼠标输入或Unity Player进程重启。弱敌仅为隔离初始配置，正式平衡和真实玩家存档未改。

## 第二批配件：04／08／18（2026-10-12，生产合同与历史冻结）

v3 执行定义新增 NoTurningBack／NonLethalOath／OnePunch，分别 fee 1／1／2；与 v2 DoubleEdge payload 互斥。精确候选 `tactical_card_assembly.double-edge-six.v2.json` 合并第一批三条。现有 Items／装配 consumer 共用校验与联合付款；有限旧 registry 迁移支持 legacy 和第一批到六条，保持完整 progress save/CAS。自动 AI 无联合 owner 时先拒绝，不接玩家抽弃循环。

Stage 在实际 ACK 上创建来源，生产者冻结 BirthRound／CausalTrigger，完整因果轮汇集排序、全叶预检、共同消费并发布唯一接受子集。04 使用实际完整回合与冻结方向；08 在护甲后逐次限制 HP；18 消费整个 commit 的首个 Damage/Blocked/Parried，后续攻击副作用统一抑制，host 与动画继续。private paused 仅从可信完整帧 witness 恢复；要求 Timeline idle 并保留 allocator，普通网络聚合恢复不放行。

单卡合同、生产者覆盖、离线失败对照与正式入口阻碍见[第二批实施交接](../Tasks/TACTICAL_ACCESSORY_SECOND_BATCH_IMPLEMENTATION.md)。本节保留生产合同与当时冻结边界；现已随前三批实装和限定交付，实际签署范围见[九配件说明](../Tasks/TACTICAL_ACCESSORY_NINE_DELIVERY.md#latest-main-accepted-delivery)，不将旧第一批测试或候选JSON代作第二批全矩阵验收。



### 第二批多弹体 P1：生产身份与已出生效果（已实装；历史补丁证据保留）

公共命中键保留 attacker/target/action instance/trace/frame，并追加真实生产者 Kind/Id/Generation/Occurrence。实际弹体的 native ID/SpawnSequence 贯穿候选、命中结果、伤害 payload/部位缓存索引、配件 decision/receipt、延迟 impact 匹配；缩放与派生 mark 复制时不丢失父身份。Timeline 使用原生 instance ID 和 authored event ID；周期弹体使用原生 ID/SpawnSequence 与帧 occurrence；派生 mark 使用完整 64 位来源节点。

叶子的 SourceNode 绑定真实 ID/出生代次；RootOccurrence 对这些生产者使用 `(Kind << 32) | Occurrence`，使同源 direct/area/periodic 阶段保持独立。当前生产 occurrence 范围为 32 位，超界拒绝，不能截断碰撞。旧无生产者身份路径继续使用其既有 occurrence。精确父命中收据和 causal round 规则沿用 r2，不将普通 Impact 视作 accepted。完整轮收集后稳定排序；逆序收集应保持相同 winner/hash。

延迟出生回调只携带原始 spawn request，因此同一请求不得登记不同父收据；冲突在修改状态前拒绝。同请求、同父收据仍幂等。实际多个子弹体须由原生请求字段（包括 SourceOrder）提供不同请求身份。

交互效果及配件交互消费收据也保存生产者身份，并纳入去重与共同 hash。`TacticalInteractionSnapshot.CurrentVersion` 为 2，receipt DTO 使用可序列化的 Kind/Id/Generation/Occurrence 字段，恢复先验证再构建身份；旧 v1 快照拒绝，不能把旧弹体收据默认为完整的新身份。新的暂停快照仍需通过既有可信 admission/clone 校验。

Stage 绑定期间出生的 Timeline 保存不可变来源 actor generation、team 和属性值；与弹体出生来源一样参与可信 clone、hash 和只读投影。来源实体移除或代次不匹配后，Timeline 战斗事件、周期弹体以及两种 projectile damage resolver 路径使用冻结出生来源，不提前跳过，也不读取同 ID 新实体的属性。Timeline 子代可沿精确来源祖先继承冻结 actor。此处不改变 OnePunch 抑制、死亡目标过滤、主卡执行或 AI 抽弃规则。

验证范围：独立补丁使用四个程序集显式 source-link 离线编译，保留原 263 项与原独审双弹体探针；新增逆序 winner/hash、父收据、延迟请求冲突、相位 occurrence、JSON/hash/clone 边界。原有三个 NoAlloc 失败单独保留。原生补充 fixture 包含双弹体完整汇总/伤害链和 Timeline/周期弹体移除来源前后对照，仅编译；执行由统一 Git/Unity 协调者安排。该离线补丁当时未签原生／发布；后续限定交付的克隆完整Trace/body-part桥2/2另有回执，不冒称上述所有producer路径或旧NoAlloc均已重跑。


## 第三批Stage配件生产合同（已实装；原隔离候选历史保留）

Execution v4只接受玻璃大炮、幸运硬币、铁王座的专属参数，费用及来源共同进入固定装配fingerprint。联合提交沿既有Host/Deck/Time/ledger事务发布Stage coin ledger与效果；公开随机结果不在预览中产生。Runtime AI Planner不接入玩家抽弃循环。

第三批Stage聚合在hash field13 / projection row17使用内部schema1，包含RNG收据、完整ACK与出生、接受根、因果轮、生命周期和身份分配器；使用现有可信暂停聚合恢复，普通网络输入不能自行构造信任。`TacticalAccessoryThirdBatchReadout`是UI只读入口，读投影不抽数。

第三批已随主工程前三批限定交付；初始规则／接缝交接见[已有第三批handoff](../Tasks/TACTICAL_ACCESSORY_THIRD_BATCH_HANDOFF.md)，现行正式发布、原生／UI及未覆盖范围见[九配件说明](../Tasks/TACTICAL_ACCESSORY_NINE_DELIVERY.md#latest-main-accepted-delivery)。初始隔离候选不替代后续字节与回执。


### 第三批 r2：已确认终态后的来源出生资格（隔离修补）

实际接受新的 trace/projectile/derived 来源时，Stage owner 先保留其物理身份，再检查已确认的同 actor generation／stage／execution Host 终态和精确 ACK 终态。终态已成立则不向叶子发布新的 SourceBorn 资格；不能用 SourceBorn 的 phase 排序早于 HostEnded/AttackEnded，倒推出该来源仍可获得玻璃大炮或幸运硬币加成。同帧命中与下一帧延迟命中均适用。

终态之前实际已接受的来源仍保留原资格，包括出生事件尚在当前因果轮排队的情况；该来源精确重放保持幂等。不能在出生或终态回调中提前排空未完成的伤害轮，也不能撤销已出生来源的硬币资格。物理登记、既有终态与 pending 事件继续使用现有共同 hash／可信 clone；此次不新增状态字段或改变 schema。玻璃大炮原本可在后续新物理 ACK 上结算的期限合同不变。

此修补为第三批 r1 独审 P1 的独立 r2 增量；原独立六用例保留原测试体，另有同帧先出生后终态、重放及可信恢复对照。离线结果见 r2 修补交接，不代表 Unity 或正式发布已通过。

ACK eligibility for a direct projectile follows the same loaded request-to-spawn-profile-to-projectile-definition lookup used by the actual projectile spawn path. Generated character-specific request IDs are eligible when their referenced definition has DirectHit policy; a hardcoded legacy request switch must not decide enemy-coin binding. Missing profiles/definitions and non-direct policies remain ineligible.


## Nine-accessory publication and progress compatibility

The explicit nine-accessory publisher, unchanged grant authority, exact prior assembly migration and evidence boundary are maintained in [九配件正式发布与进度兼容](../Tasks/TACTICAL_ACCESSORY_NINE_DELIVERY.md). This isolated source package does not itself establish main-project publication or visible-play acceptance.


### Physical ACK identity across accepted-action cleanup (isolated repair, 2026-10-12)

Natural finish, external completion, cancel-only, orchestration termination and lifecycle release clear the CharacterTest action payload while retaining the actor-local runner NextInstanceId. Removing the gateway at an accepted terminal restarted allocation at 1 while the Stage retained its prior ACK; the next turn then correctly rejected the reused identity as ConflictingPhysicalAck. ReleaseCompleted uses the existing terminal/idle gateway restore contract, which is already represented by runner snapshot/hash. Stage/execution/actor generation/instance/revision matching and frozen source identities are unchanged; no new schema or allocator is introduced.

An exact historical ACK replay remains read-only after its terminal: it returns the original receipt without reopening the attack or publishing another event. A changed accepted frame or eligibility for that physical identity still rejects without mutation. A new physical action receives a new instance; it cannot retag the previous ACK or delayed projectile with the current execution. Existing coin receipts and RNG continuation are preserved.

The original isolated candidate reproduced Stage1/actor(2,1)/F32 accepted/F113 ended/F143 ACK reuse against the pre-repair Runtime; the second eligibility was unknown and both possibilities were tested. Its 117 offline cases remain historical candidate evidence. The independently reviewed repair is now installed in Main: native 13/13 covers gateway cleanup and idle runner/trusted Stage-clone recovery, and real continuous second-turn play, Planning pause/resume, typed expiry and end/reentry have passed within the delivery receipt scope. Old ACK/source identities and RNG receipts remain unchanged; the old F143 error is retained. A full private paused-session owner/world transaction was not newly run. Compile facts are complete while the custom tool returned timed_out; Git remains uncommitted/unpushed. See [the current delivery scope](../Tasks/TACTICAL_ACCESSORY_NINE_DELIVERY.md#latest-main-accepted-delivery).
