# CharacterTest Runtime AI Planner Behavior Card Gameplay And Runtime AI Lab

> Status: Design
>
> Decision: Accepted
>
> Date: 2026-07-17
>
> Scope: CharacterTest player behavior-card deck building, Runtime AI Lab authoring relationship, published-card lifecycle, session authority, and multiplayer validation contract.
>
> Implementation status: this document freezes the target product contract. The CharacterTest-local offline path now includes behavior-card logic v2, durable Lab Drafts, compilation, session-local `Apply F+1`, RuntimeFrame/Replay diagnostics, accepted Published Card/Installed Deck/Unlock And Persistence/Session Authority foundations, the player FairyGUI deck and Combat HUD flow, player-card-to-isolated-COW-Draft authoring, typed player-legal versus Lab-experiment projection, append-only local Publish, Publish UI, successful source-revision adoption, and explicit Validate/Apply/Replay/Publish separation. Production account storage, catalog distribution/approval/signing/rollback, networked product UI, and product room/matchmaking integration remain unimplemented.

## Product Principle

Runtime AI Lab 与玩家行为卡玩法不是两套系统。玩家使用的行为卡必须来自 Lab 设计、验证并发布的同一个 `Published Behavior Card`；玩家装配、Lab 测试、compiler、validator 和 runtime evaluator 共同使用同一套卡片语义。

两者的差别只在权限和信息密度：

| Surface | 用户目标 | 可见内容 | 可修改内容 |
| --- | --- | --- | --- |
| 玩家行为卡组 | 为每个 AI 角色构筑战斗策略 | 名称、图标、费用、标签、用途、风险、当前执行卡和切换原因 | 安装/移除整卡、应用推荐预设 |
| Runtime AI Lab 装配层 | 用玩家规则复现和验证卡组 | 玩家全部信息，以及构筑诊断、运行覆盖和发布状态 | 玩家操作，加显式实验越界 |
| Runtime AI Lab 编辑层 | 设计单张卡并验证内部逻辑 | 完整 Stage、Detector、Transition、typed facts、诊断和 RuntimeFrame 关联 | 卡内逻辑、测试、候选应用和发布准备 |

Lab UI 本身仍是 CharacterTest internal 开发工具，不是玩家界面。专用 Lab 场景默认进入 `Setup / 实验设置`，玩家装配层作为显式按需工作区和合法性基线；Lab 仍必须成为同一玩家玩法的完整设计、测试与发布超集，而不是维护一套只在 Lab 生效的行为语义。

## Player Loop

玩家围绕“解锁整卡并为每个 AI 角色组卡”形成闭环：

```text
解锁 Published Behavior Card
  -> 选择一个 AI 角色
  -> 单点安装或移除整卡
  -> 系统自动排列并持续诊断
  -> 战前锁定 6 槽 / 10 费卡组
  -> Runtime AI Planner 自动仲裁当前行为卡
  -> 战斗 HUD 显示当前卡图标与切换原因
  -> 战后调整卡组
```

首版固定规则：

- 每个 AI 角色拥有独立卡组；切换角色不会共享安装状态。
- 每个卡组最多 `6` 个槽位、总费用最多 `10`。
- 同一稳定行为卡不得重复安装；不同稳定卡可以具有相似用途。
- 行为卡按完整资产解锁，不升级或拆分卡内 Stage、Detector、Transition。
- 安装与移除对玩家是单点操作；系统依据稳定规则自动排列，不要求玩家决定时间轴坐标、内部优先级或 Stage 位置。
- 进入战斗前锁定卡组；战斗中不允许更换、增删或编辑卡片。
- 基础集合固定为两张正式 Published Behavior Card：`靠近并普攻`
  （`bc_3qz33zjzk4`）与 `发呆`（`bc_g6a5fw319a`）。默认行为组、恢复目录和
  推荐预设只使用这两个 Stable Card。当前只冻结基础内容身份和 Published Logic；
  完整运行接入尚未完成。没有安装有效 Card Overlay 时，兼容 Profile 可以始终 Wait，
  不得用旧 BehaviorDefinition 动作偷偷补齐索敌、接战或输出。旧模块卡不得重新生成
  或重新进入卡库。
- 玩家不能编辑 Stage、Detector、Transition、typed facts、转换策略或内部优先级。

## Card Information And Deck Diagnostics

玩家卡面只展示做出构筑决定所需的信息：

- 稳定名称与图标。
- 费用、用途标签和短描述。
- 适用场景、主要收益、限制或风险。
- 可能发生的行为切换原因，例如“进入攻击距离”“受到高威胁”“生命值过低”。
- 安装状态、当前槽位占用和卡组费用摘要。

Lab 可在同一卡面展开完整 Logic v2，但玩家不应看到 raw fact id、stable transition id、score/cost 明细、profile slot、catalog hash 或 compiler 内部结构。

卡组诊断至少覆盖：

- 超过 6 槽或 10 费。
- 重复稳定卡。
- 缺少目标、接敌、伤害、脱离或生存能力时的提示。
- 相互冲突或可能长期互相打断的卡片组合。
- 卡片与当前角色能力、CharacterAction 或发布目录不兼容。
- 仅依靠免费保底时的低战力提示，而不是把卡组判为不可运行。

诊断解释问题并给出建议，不要求玩家手工调整内部仲裁顺序。行为卡在时间轴或卡组列表中的视觉排列不是 runtime 优先级真源。

## Install, Remove And Recommended Presets

“安装”只把稳定卡引用加入当前角色的 Installed Deck；“移除”只删除这条安装引用，不删除玩家收藏、Published Behavior Card 或 Lab Draft。

现有 `BehaviorLoadout` / `Variant` 保留为推荐预设：

- 预设保存一组已发布卡片引用和可见说明，用于快速形成完整构筑。
- 应用预设后，系统将它展开成当前角色的普通 Installed Deck。
- 玩家可继续单点换卡；预设不会成为不可编辑的黑盒策略。
- UI 可显示“源自某预设”，但 runtime authority 以展开后并锁定的卡组为准。

因此，当前只能选择 `loadoutId + variantId` 的实现仍可作为兼容入口；目标合同是玩家自由卡组，不能把当前预制 Loadout 唯一入口描述成最终玩法。

## Shared Data Lifecycle

权威生命周期固定为：

```text
Behavior Card Draft
  -> Validated Draft
  -> Published Behavior Card
  -> Unlocked Card
  -> Installed Deck
  -> Runtime Instance
```

各层职责如下：

| Layer | Identity / content | Authority boundary |
| --- | --- | --- |
| Draft | `cardDraftId`、可失败的 Logic v2、编辑布局和诊断 | Lab 本地恢复，不进入 gameplay authority |
| Validated Draft | 通过 schema、引用、compiler 和指定测试矩阵的不可变候选 | 仍未对玩家可用 |
| Published Behavior Card | 稳定卡 ID、版本化 Logic v2、玩家 metadata、兼容性与内容 hash | 生产 catalog 真源 |
| Unlocked Card | 玩家账号/进度对稳定卡 ID 的拥有关系 | 不复制卡片内部逻辑 |
| Installed Deck | 角色 ID、稳定卡 ID 列表、槽位/费用和锁定状态 | 玩家构筑 authority input |
| Runtime Instance | 会话固定 revision/hash 的编译结果及运行状态 | Tick 只读不可变 evaluator/runtime state |

`Layout` 继续不进入行为语义 hash。玩家安装顺序、卡组 UI 自动排列和 Lab 时间轴显示位置也不得隐式改变卡内逻辑；只有明确参与 compiler 的卡组仲裁字段才能影响 authority hash。

## Stable Identity, Revision And Save

- 存档、卡组和客户端请求只引用 Published Behavior Card 的稳定卡 ID，不保存 Draft GUID、显示名或整段 Logic v2 JSON。
- 会话开始时，authority 按当前目录解析每个稳定卡 ID 的最新兼容 Published revision；解析完成后固定 revision、content hash 和 catalog revision，战斗中不得静默漂移。
- 同一稳定卡的新 revision 必须声明 schema/runtime/角色能力兼容性。没有兼容 revision 时，卡组校验失败并给出明确替换建议。
- 运行中 snapshot、rollback 和 replay 保存足以重建选择的稳定 ID、固定 revision/hash 和 catalog revision；不得依赖“稍后重新取最新版本”。
- 发布卡的首次语义编辑必须自动创建 copy-on-write Draft，不原地修改共享发布内容，也不改变现有存档或正在运行的 session。

## Two Arbitration Levels

必须区分两个不同层级的跳转：

1. **行为组/卡组仲裁**：Runtime AI Planner 根据当前事实，从已安装且可运行的行为卡中选择唯一当前卡；卡完成、失败、失去资格或更高优先级事件出现时，重新仲裁。
2. **卡内 Stage Transition**：当前卡的 Logic v2 在自己的 Stage、Detector 和 Transition 之间跳转，并继续遵守每帧最多一次跳转、单一主动作所有权和显式转换策略。

卡内 `End` 表示把执行权交回卡组仲裁，不直接指定下一张玩家卡。卡片之间不得用隐藏 Stage 连线形成第二套行为组。战斗 HUD 只需展示当前行为卡图标和结构化切换原因；完整 Stage、Detector、实际路径和 typed RuntimeFrame marker 只在 Lab/诊断表面显示。

## Session Compilation And Multiplayer Authority

客户端只提交目标角色和稳定卡 ID 集合。authority 在 selection phase 内完成：

- 校验客户端是否有权配置该角色。
- 校验卡片是否已解锁、未重复、满足 6 槽/10 费和战前锁定规则。
- 校验 catalog revision、Published revision、角色能力及所有引用兼容。
- 解析推荐预设或自由卡组为规范化 Installed Deck。
- 使用与 Lab 相同的 validator/compiler 生成不可变 evaluator cache/profile slot。
- 将稳定 ID、固定 revision/hash、目录 revision 和规范化卡组摘要写入 gameplay authority、snapshot、rollback 和 replay bootstrap。

客户端不得上传任意 behavior JSON、编译结果、内部优先级或自定义脚本。任一校验/编译失败都拒绝整个候选并保留旧卡组/旧 evaluator；不能部分安装、静默降级或在战斗中改用不同 revision。首版正式实现可继续限制为战前或安全区选择，不支持战斗中热切换。

## Runtime AI Lab Workflow

Lab 默认流程与玩家完全一致：

```text
选择角色和可用卡库
  -> 在玩家装配层单点安装/移除
  -> 以 6 槽 / 10 费规则诊断
  -> 运行同一 compiler/runtime 测试
  -> 需要时进入单卡 Logic v2 编辑
  -> 自动创建 COW Draft
  -> Validate
  -> Apply F+1 到当前 Lab 会话候选
  -> RuntimeFrame / Replay 验证
  -> 通过独立 Publish 门禁发布
```

Lab 可以显式启用实验越界，但必须持续显示“非玩家合法构筑”，并列出超槽、超费、未解锁、未发布、兼容性或重复卡等原因。越界只影响当前 Lab 实验，不生成玩家合法存档、不能通过 Publish，也不得伪装成正常玩家卡组。

`Apply F+1` 与 Publish 必须保持分离：

- `Apply F+1` 只把成功编译的候选 revision 从下一 `RuntimeFrame` 应用于当前 Lab session，并记录重演所需 revision event。
- Publish 需要独立的 schema、引用、玩家合法性、确定性、迁移、测试矩阵、catalog 冲突和审批门禁。
- Applied 不代表 Validated，Validated 不代表 Published，Published 也不自动代表玩家已解锁或已安装。

## Visual Target And Usage Guides

以下图片是目标效果图/使用说明图，不是当前运行截图，也不表示相应玩家卡组、发布 catalog 或联网校验已经实现。

### Unified Player Deck And Lab Authoring Target

![Runtime AI Planner behavior-card gameplay and Runtime AI Lab target](../Images/CharacterTest/runtime-ai-planner-behavior-card-gameplay-lab-target-v1.png)

### One-click Installation And Removal

![Runtime AI Planner behavior-card installation guide](../Images/CharacterTest/runtime-ai-planner-behavior-card-installation-guide-v1.png)

### Stage In-place Editing In Runtime AI Lab

![Runtime AI Planner behavior-card Stage editing guide](../Images/CharacterTest/runtime-ai-planner-behavior-card-stage-editing-guide-v1.png)

### Runtime Reading On The Behavior Timeline

![Runtime AI Planner behavior-card runtime reading guide](../Images/CharacterTest/runtime-ai-planner-behavior-card-runtime-reading-guide-v1.png)

> 行为时间轴的具体执行结构、弹性门禁机制与以 0 配置数据为目标的数据级联驱动见专项设计：[战术行为时间轴优化设计](CHARACTER_TEST_RUNTIME_AI_LAB_TACTICAL_BEHAVIOR_TIMELINE_OPTIMIZATION.md)。


## Current Implementation And Target Gaps

### Current implementation

- CharacterTest 已有 BehaviorDefinition/BehaviorGroup/BehaviorLoadout/BehaviorSelection 基础、预制 loadout/variant 选择和 session 编译入口。
- Runtime AI Lab 已有 workspace/group/card Draft identity、autosave、copy-on-write、Logic v2、validator/compiler/evaluator、typed RuntimeFrame events、Warm Replay 和 session-local `Apply F+1`。
- 阶段 6 已闭合 CharacterTest 本地玩家/Lab 流程：玩家牌库 `Lab 编辑` 解析当前兼容 Published revision，创建或稳定复用隔离 Logic v2 Draft，持久化来源 revision/semantic/content hash，并支持 Undo、重开和返回牌库；typed 玩家合法性投影持续显示槽位、费用、重复、解锁、兼容 revision、能力、禁用、未发布语义及玩家不可表达的手工仲裁原因。Publish UI 只允许隔离 COW 单卡通过 Logic v2、权威引用、玩家合法性与目录 lineage 门禁后追加下一连续 revision；所有角色存档卡组先在候选目录下重验，目录原子写盘成功后才切换本地 authority。成功后 Draft 收养新的来源 revision/hash 并关闭发布前 Undo 历史，但不会隐式执行 `Apply F+1`、修改当前 evaluator 或追加 Replay application revision。生产审批、签名、分发、回滚与旧 revision 生命周期仍未实现。
- 当前实现已有 internal Published Behavior Card v1 基础切片：不可变 envelope、玩家 metadata、stable id/revision、catalog JSON/hash、兼容 revision resolver 和复用 Logic v2 的独立发布校验。该切片已通过阶段 1 验收，但不等同于生产发布系统。
- internal Installed Deck Core v1 已通过阶段 2 验收：每角色 stable identity、compatible Published revision 解析、6 槽/10 费、stable card 去重、稳定自动排序/deck hash、原子安装/移除和显式推荐预设展开。当前基础预设只展开 `靠近并普攻` 与 `发呆`；诊断模型区分 `Info/Warning/Error`，角色 `RequiredCapabilities` 不匹配仍作为非阻断诊断。该内部基础不等同于玩家存档、UI 或联网 authority 已完成。
- internal Unlock And Persistence v1 已通过阶段 3 验收：不可变整卡解锁收藏、从已验证 Installed Deck 原子保存每角色 stable ID 列表、显式 stable ID migration map、内容 hash、`RuntimeCustomState`/`RuntimeSaveState` roundtrip participant，以及缺失或不兼容 Published revision 的保留引用恢复诊断。恢复不会删除所有权或静默启用 fallback；不可用角色卡组保持未解析并要求替换。Session And Authority 内部基础也已通过阶段 4 验收并消费该所有权/卡组合同；CharacterTest 本地玩家 UI 已连接该存档与修复流程，但正式账号/云存储仍未连接。
- 玩家展示已有低频策略卡/战斗短状态 projection 基础。

### Not implemented yet

- Unlock And Persistence 已有内部规则、Runtime SaveState participant 和 CharacterTest 本地玩家 UI；尚未连接正式账号/进度存储或云存档。
- 免费基础保底与卡组能力诊断已连接 CharacterTest 本地玩家 UI；尚未连接正式角色能力事实来源和产品化文案目录。
- Player Deck Workbench Core 的阶段 5 已验收为 `Accepted`：正式 `MxCharacterTestBehaviorDeckUi/BehaviorDeckPanel` 已通过 UI Composer Apply/Publish，玩家可在明确的我方角色列表中切换独立卡组，并对 Published 卡执行单点安装、移除、一键应用推荐预设、失效引用替换和诊断。角色列表只来自 Arena Team 1 或等价的显式玩家侧启动目标，按 stable character identity 去重和稳定排序；不会再依赖实体遍历顺序或把敌方 Runtime AI Planner Agent 当作卡组目标。每个不同角色在 session 构建时预留不与敌方 spawn selection 冲突的独立 Runtime AI Planner profile slot，存档持续保存所有角色卡组；写盘失败保持原 Deck/hash，损坏存档先隔离备份。战前 authority 会为全部我方角色固定 Published revision/content/deck/catalog hash，并以批处理方式原子安装到各自 Logic v2 Runtime Overlay；非空卡组执行真实 Stage，空卡组使用免费寻敌/待机保底。战斗锁定后仍可切换观察角色和对应 session lock，但 Workbench 保持只读。运行时卡组仲裁产生 typed 切换原因，Composer-owned Combat HUD 只查询当前明确观察角色并显示卡图标、名称和原因，不遍历“第一个 Runtime AI Planner Agent”。Arena Running/Finished 从主菜单返回时恢复 Arena surface。不可用/不兼容卡及底层诊断在玩家 UI 中使用友好占位与 typed 文案，不显示内部 stable ID。正式账号、云存档、production catalog 分发和联网玩家页面仍未实现。
- Runtime CommandModule 已提供当前卡 stable ID、semantic/authority hash、Stage、运行状态与 typed 切换原因的内部 NoAlloc 查询；多个角色专属 slot 的 overlay、snapshot 和 presentation 可同时保留。
- 生产 catalog 分发、审批、签名、回滚和旧 revision 生命周期治理；CharacterTest 本地 Publish UI、append-only 事务和 Draft 来源收养已经存在，但不等同于这些生产能力。
- authority 对解锁、费用、目录版本和自由卡组的联网校验。
- 正式账号、产品房间/匹配和联网玩家界面对生产发布目录的完整连接。

上述目标在对应实现落地并通过能力验收前，不得写入 `CAPABILITIES.md` 作为已交付能力。

## Delivery Slices And Acceptance

建议后续按可独立验收的顺序实现：

1. Published Behavior Card envelope、catalog、版本兼容与 Publish 门禁。
2. Installed Deck Core schema、每角色 stable identity、6 槽/10 费、去重、免费保底和推荐预设展开。
3. 整卡解锁收藏、每角色 Installed Deck 存档、stable id 迁移和缺失 revision 恢复。
4. 战前锁定、authority command、session revision/hash 固定、snapshot/rollback/replay 与多人合法性校验。
5. 玩家卡库/卡组 UI、单点安装/移除、预设应用，以及战斗 HUD 当前卡图标和 typed 切换原因。
6. Lab 默认玩家装配层、越界实验、同 catalog 的发布闭环和玩家/Lab 一致性验收。

最终验收必须证明：玩家和 Lab 对同一稳定卡、相同 Published revision、相同卡组与输入会得到相同 compiler 结果、卡组仲裁、Stage 路径、主动作所有权和 authority hash；Lab 额外信息不得形成第二套 runtime 语义。
