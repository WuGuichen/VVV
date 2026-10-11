# 战斗设计管理系统：高自动化落地方案与当前代码对账

> Design / 待实施增量方案，2026-10-02 初稿，2026-10-03 补充只读复核。以当前工作树为核查对象，HEAD 为 `2e9ad0f6e`，并包含其他任务的未提交代码；HEAD 不能单独复现本次来源。
>
> 本文独立交付，保留 [原方案](COMBAT_DESIGN_MANAGEMENT_SYSTEM_PLAN.md) 的历史快照。当前已出现部分工具实现，因此本文修正“尚无 combat-design CLI/HTTP”等早期缺口，不覆盖原文或现有实现。本文所有新增合同和指标均为建议。
>
> 本方案交付仅涉及只读检查与本独立 Markdown；本轮保留已有方案并补充复核，不另建并行方案。未启动、连接 Unity，未运行 CLI/HTTP/MCP、编译、测试、Play、发布或读取玩家存档，未提交推送。源码存在性不等于运行通过。

## 1. 目标与基本决策

系统给 Development Agent 编辑，人可以轻松查阅。机制与意图共同表达战斗设计：机制描述横切规则，意图描述行为目的，战术模板、装配后的卡及使用环境是不可省略的消费者。采用 **既有领域 owner + 设计语义注释 + 可重建事实投影 + 多对多依赖 + 版本化证据**，扩展当前 Editor Hub、AI Lab 和发布链路。

关键引用不让人或 Agent 抄 ID；身份由原 owner 分配，引用通过查询返回的 typed handle 选择。时长、距离、费用、依赖、源与产物关系从权威源生成；不增加第二份可编辑数值表或人工关系台账。Agent 编写目的、理由、期望和取舍；代码提取器只证明有限的实现事实，不能自动推断全部设计语义。

用三种证据口径：**源码事实**表示本次直接阅读支持；**历史记录**表示仓库文档或用户报告，本次未复验；**建议**表示未来功能。解析失败、未接入与未验证必须可见，不用名称、截图或一个“完成”勾选代替证据。

## 2. 真实能力、复用边界与缺口

### 2.1 作者 owner、编译和发布

| 当前源码事实 | 复用方式 | 必须补齐或保留的边界 |
| --- | --- | --- |
| `CharacterActionAuthoringWorkspaceSession.Execute` 在 ExpectedRevision > 0 时检查版本；SaveAll 校验、复核磁盘 canonical SHA-256，才交 DocumentSession 保存并更新 baseline | 动作和轨道的编辑、Undo、保存继续由此 owner 负责 | Agent adapter 强制提供查询获得的正 revision 与 readSet，禁止以 0 跳过 CAS；现有命令不等于所有字段均已开放远程修改；增加 typed 白名单 adapter，禁止任意路径 JSON patch |
| AI Lab EditToken 包含工作区、卡、定义 revision 与编辑上下文；编辑保留 CaptureState/Undo/异常恢复 | Agent 经 owner 提交结构化操作 | 不暴露任意 Action 委托、反射方法名或内部字段路径 |
| `TryReplaceTacticalDefinition` 检查工作区/定义版本；多卡引用要求 confirmedSharedImpact；`TacticalDefinitionImpact` 找当前工作区 composition 消费者，触及卡、组与工作区 revision | 直接消费者反查和共享影响确认复用原合同 | 当前是局部反查，不是所有模板、装配、发布、卡组和场景的完整图 |
| DraftPersistence 自动保存经过数字身份准备、staging、引用 hash、复读，再安装工作区 | 同一保存 owner；保存与发布分开 | 补跨进程租约/CAS，不能把内存 revision 检查宣传为任意多工具同时写的保证 |
| Authoring/DSL compiler 与 Binding 解析槽、条件、锚点，生成既有执行表；执行中输入冻结 | 同一个 prepared result 供校验、说明、预览与执行 | 模板留槽可结构合法，真实装配不足则不能可执行；不新增“设计版执行器” |
| PublishedSource 固定 composition 和所需 shared 定义，拒绝缺失、重复、未使用依赖 | 冻结发布源，不随共享草稿漂移 | 发布作者源不等于冻结全部外部 Runtime 规则、角色动作和装备 |
| 独立 `TryPublishCard` 与组级 Prepare/CommitPublishCurrentChanges 分开；后者绑定 preview token，处理重复确认 | 单卡只发单卡；确需组更新才走组 owner | 不能为了显示一张卡顺便升级行为组 |
| ProjectCatalogEditorBridge Write 经过 staging 校验、持久备份、phase journal、安装复读、Committed 标记；Recovery 核实提交状态 | 原目录事务与恢复结果进入操作回执 | 目录事务不是动作、Buff、战术源和存档的全项目原子事务；提交后收尾失败不可伪称已回滚 |

### 2.2 当前管理工具与接入面

| 当前接口/入口 | 本次读到的能力 | 下一步缺口 |
| --- | --- | --- |
| `CombatDesignGateway` 与 CLI `combat-design query / plan / submit / status`；HTTP `/api/combat-design/*` | 有快照 readSet、Facing 参数计划、planToken、严格字段、幂等 key、队列和 PendingEditor；主项目只读，写入限隔离 fixture | 扩展为多领域操作登记与能力查询；当前不是正式项目任意写 API |
| CLI/HTTP `catalog / intent / mechanisms`，Core 的三个 Projection | 当前 v4 工作区有限解析、事件/条件/槽/组合引用、直接反查、原文和未解析诊断；机制 v2 有五个注释与有限指纹绑定提取器 | 无跨工作区完整图、任意旧 schema、真实装配卡、持有实例、卡组/整备及场景证据覆盖 |
| `CharacterTestCombatDesignOwner` | Editor 消费请求，隔离 fixture 中复用 shared owner、编译、生成说明/卡面文字、原发布 owner、重载重编译；输入为 Unbound(1) | 这不是实际角色装配/战斗验证；禁止把隔离目录发布当正式 Catalog 发布 |
| Editor Hub app.js 已登记“战斗设计”，页面分机制、意图、模板表达、源审查、owner 快照 | 现有导航与只读页面可复用，不重造入口 | 需要卡配置/角色/场景钻取、证据陈旧及恢复操作；真实浏览器交互本次未验 |
| `GET /api/character-actions/catalog`、copy-to-character、action/open/open-status、character/tactical-content | 目录候选、来源 hash/revision、ID lease 与 rollback、精确打开动作 owner；tactical-content 读 published | open 202 是排队状态，不是保存/编译成功；复制到角色库不能冒充改公共动作 |
| Authoring CLI 的 character/buff/projectile、workspace/mutate/apply 与生成器 | 独立于 Unity 的领域工具链 | 每个 adapter 明确哪些操作离线可用、哪些需 Editor；不得绕 owner 直接覆盖生成资产 |
| Unity MCP 配置、TriggerAutomation 的 runtime-ai-lab authoring MCP 测试工具源码 | 仓库存在连接配置与定向测试工具 | 本次未验证连接；测试 start/status 不等于生产战术 CRUD。生产 MCP 应薄封装同一 gateway，不能调用测试工具代替保存 |

当前 Projection 的源码显示请求时重建与结束复读 readSet；页面主动重读/加载时发现变化，**没有后台 watcher 或轮询**。新增自动监听只加速发现，仍需查询、计划、提交前复核及重开对账，避免丢事件。

`metadata/combat-design-view.json` 当前承担设计注释、分类来源和有限固定规则模板：可以复用其职责，但模板中的 reviewedHash 只能由有审查记录的流程更新，禁止 Agent 改规则后顺手抹掉 stale。它不应扩展成手填游戏数值或消费者名单。

历史实现记录见 [第一期交接](Guides/COMBAT_DESIGN_PHASE1_IMPLEMENTATION.md)：记录了窄切片 CLI→Editor→隔离发布验收，同时明确实例、装配、场景、Play、原生 MCP 和跨 owner 事务未完成。本文不重跑、不将其提升为今天所有新投影源码的验证。

### 2.3 Catalog、说明和预览

复用 ActionAuthoring Search/SourceCatalog 与 ActionBrowserCatalog：Common 和角色作者源计数，生成副本不能重复算动作；动作集合映射不代表角色拥有或可选资格。Exporter 的显式重建作为修复入口，增量刷新由 owner receipt 或源变更驱动，避免要求人逐页刷新。

复用 TacticalPreparationCatalog / SummaryPreparation、BlockDescription / DslDescription、CardUiPreview。动作时长、Root Motion、实际动作距离由正式准备链推导；当前距离口径是姿态胶囊到目标 Hurtbox 的三维表面净空，旧根距离摘要失效，摘要仍不等于真实命中。

TacticalPreviewCache 的 key 已含 workspace/card revision、引用 StableHash、角色/队伍/槽/会话、历史冻结、PreparationCatalog revision 与 presentation/actor revision。扩展时复用这些输入与 prepared identity；纯查询、选择、展开、改理由不能重复做战斗准备。卡面文字预览、AI Lab 事件预览和引擎/玩家 UI 截图分别记录，不能互称。

`CharacterTacticalPlayerService.TryRefreshPublishedCards` 暂存定义后替换目录，并报告无效 owned 引用，明确不修复或保存 ownership。发布后自动刷新目录/索引，存档迁移与装配修复仍交正式 owner，不顺带写玩家存档。

特别注意：同一服务的 `OpenAsync` 会载入状态，可能执行旧版本迁移、默认内容初始化、引用修复并 `FlushAsync`。因此管理系统的只读 GET 不能以“打开真实 PlayerService”实现实例目录；新增只读 snapshot adapter，验收使用 `ICharacterTacticalStateStore` 的内存 fixture。不得把实例反查暗中变成个人存档迁移。

当前 CatalogProjection 的功能槽解析读取 `Assets/MxFrameworkGenerated/CharacterActions/Libraries/Common/character_actions.json`，这是已生成目录的观察事实。补接源映射时必须同时呈现 `CharacterActionSource` 作者权威、生成器版本与源/产物指纹；发现两者不一致则显示源已变、产物未更新，禁止将生成副本当可编辑权威，也不能把未生成的新源宣传为已在运行中生效。

## 3. 对象与唯一权威

术语遵循 [角色动作与战术行为卡第零章](../StorySource/玩法基石/角色动作与战术行为卡.md)，制作/发布遵循 [作者指南](Guides/CHARACTER_TEST_TACTICAL_CARD_AUTHORING.md)。共享源目录混合承载用途，Sequence 不是天然独立组合身份。

| 对象 | 权威与身份 | 系统处理 |
| --- | --- | --- |
| 机制：横切规则 | 领域代码、正式配置与接口合同；无统一 Runtime 机制表 | 新设计 identity 只存目的、取舍、验收期待；adapter 自动提供规则事实与证据位置 |
| 时间轴事件 | 动作轨道或战术作者源各自 owner 的事件 identity | 时序、操作、条件、锚点和原文自动投影；两类事件保持命名空间 |
| 角色动作 | CharacterActionSource Common/角色源、action_id_catalog | ID 原 owner 分配；时长、费用路径、距离摘要、资源和需求从源/准备链读取 |
| 战术意图 | 当前 shared 源加有来源的设计分类 | Agent 说明目的；机器抽事件、规则消费与显式引用，不凭标题分类 |
| 意图组合 | 可选复用的真实引用/组合结构 | 复用现格式；没有独立组合对象时只显示组合表达 |
| 战术模板 | 可留动作槽的流程源 | 可保存/结构校验；未装配数据显示占位/Unknown，不制造真实时长距离 |
| 装配后的战术卡 | 模板+参数+当前角色动作装配，经 prepared inputs 解析 | 自动生成可用性与执行输入身份；发布源与 actor-bound 结果分开 |
| 卡牌实例 | PlayerService owned InstanceId / NextCardInstanceId | 与战内 TacticalCardInstance、一次执行 ID 区分；一期仅合成 fixture，禁止读写个人存档 |
| 卡组/整备 | 原 PlayerService/SaveStore 的实例绑定及 preset | 重复持有按实例计算；group entry 不能直接冒充卡组实例 |
| 角色与场景验证 | Character package、场景/关卡 owner、runner | 来源、装配、测量规格和场景条件自动绑定证据，不复制 Transform 或角色数值 |

新增持久数据只允许新职责：设计注释/期待、有来源的分类、adapter 合同、受信 runner 证据。事实、关系边、摘要、影响列表、Catalog 与 readiness 都可删除重建；删除缓存不丢领域数据或设计源。

关系为多对多图：机制可直接作用于事件、动作、意图或场景；一个意图可依赖多个机制，多个模板可共用意图，一个模板可派生多张装配卡，同一配置卡有多份实例。Buff、Ability、弹体、资源、代码符号也可连多对象。遍历 visited 去重，允许关系环；锚点/执行结构非法循环由原 compiler 阻断，不拿“必须是树”简化图。

边自动携带 `kind/from/to/owner/field-or-symbol/source-version/extractor-version/provenance`，读取模式区分 current、pinned、frozen、binding-dependent。Agent 声明的语义关联另标 Declared，不能当自动事实或完整消费者覆盖。跨工作区身份以 owner domain+workspace+stable identity 区分，不能只比较同名字符串。

## 4. ID、revision 与 hash 合同

| 身份/指纹 | 用途与不可混用点 |
| --- | --- |
| stable ID / 数字 ID / instance ID | 由各 owner 分配；查询返回候选 handle，保存时 owner 落具体 ID。handle 是选择/操作定位，不能代替 Runtime 正式身份 |
| workspace/card/shared revision | CAS 与编辑上下文；组/卡触及规则沿原 owner；不同对象的 revision 不做大小比较 |
| 文件字节 SHA-256 | 外部改动、readSet 与恢复冲突；同 HEAD 下脏文件不同仍不同 |
| 动作 canonical SHA-256 | Document baseline；不能等同于文件原始字节 hash |
| AuthoringJson.SemanticFingerprint | canonical JSON 排序，排除 name/title/description；用于行为语义指纹，不证明显示文字相同 |
| PublishedSource.SemanticHash | 既有 RuntimeContentHashAccumulator 对固定源 JSON 的合同；不改成通用 SHA-256 |
| published ContentHash / CatalogHash | 依原 publisher/index 合同绑定发布身份、revision、内容；Catalog 刷新不等于实例绑定已修好 |
| 当前 ProjectionHash | 输出键排序、readSet、状态和诊断参与；generatedUtc 排除；有限投影身份不等于编译或运行证据 |

计划/证据新增 fingerprint 可用 SHA-256，但保存上述字段的类型、算法和 owner，禁止一个 `hash` 字符串混用。发布固定旧 shared 不因草稿变化自动升级；仍读当前角色动作或外部代码的卡可能受影响；历史冻结保持原输入，新的准备结果重新绑定。

## 5. Agent 结构化操作与自动化事务

沿当前 Gateway 扩展操作登记，HTTP、CLI、生产 MCP、UI 调同一 application 层。新接口先返回能力：Available、ReadOnly、RequiresEditor、Busy、Unsupported；服务启动不自动开 Unity。新能力不得以“人工填 ID”作为降级方式。

| 建议操作 | 输入/输出 | 自动执行职责 |
| --- | --- | --- |
| query/resolve/inspect | 类型、词、角色/场景范围、版本模式→候选 handle、来源、歧义、状态 | 候选0诊断、1确定、多个需设计选择，不猜第一项 |
| dependents/impact | handle+snapshot→直接/传递闭包及未覆盖输入 | 自动区分需重编译、重准备、重测、保持旧版本与潜在动态影响 |
| create-design/annotate | 目的、理由、期待+查询 handle→设计源 receipt | 自动分配身份；无代码绑定可保持“仅设计”，不伪造已接通 |
| plan-mutation | 注册操作与设计参数→candidate/Diff/readSet/风险/validation plan | typed 参数、单位/范围、owner 权限、共享影响自动提取；不开放任意 JSON patch |
| validate/preview | candidate handle→schema、语义、prepared result、说明/卡面/可用性 | 机器完成引用、ID、派生、预览与诊断；未装配模板只做结构检查 |
| commit/status/recover | planToken+idempotencyKey→持久 operation receipt | CAS、owner事务、复读、恢复分类和索引失效；排队不等于成功 |
| prepare-publish/publish/refresh-catalog | 原 owner preview token 与所需风险确认→exact revision/hash | 编排独立卡或组事务；刷新投影，不悄悄改实例/存档 |

建议新请求只传 handle、期望 snapshot、注册 operation、设计参数、理由与幂等 key。源码 locator、event ID、动作 ID、引用列表、文件路径、产物路径及 impacted IDs 均由服务派生。未注册操作返回 Unsupported 并列出具体 owner 缺口，不能反射或脚本逃逸。

以未来注册的动作回避窗口操作为例，Agent 先 `resolve` 取得动作与事件 handle，再提交 `plan-mutation(handle, snapshot, operation="set-evade-window", parameters={startFrame:7,endFrame:21}, rationale=...)`。这里的帧数是人/Agent 作出的设计选择；handle、所属库、真实事件身份、mask、读集合、消费者与生成路径全部由查询/owner 解析。返回 `candidateHandle / diff / impact / uncovered / validationPlan / ownerRequirements`；后续 `commit(planToken, idempotencyKey)` 不重传手抄引用。此签名是建议，当前仅有 Facing 阈值/帧预算计划，未实现此操作。

事务顺序：解析查询快照→获取对应 owner 写租约→复核 revision+readSet+Catalog head→在副本应用 typed mutation→全部预检→具体 Diff/风险确认（如确有 gate）→owner commit→复读→receipt→投影失效/刷新。确认必须绑定已完成、可审阅的候选；用户已有适用授权时继续，不能每个保存/字段都再问。

幂等沿当前同 key 同 body 回同 operation 的合同，包括首次修改已使旧 snapshot 陈旧后的重试；同 key 异 body 拒绝。ID 分配、revision、发布只算一次；中断后先检查 durable receipt/journal，不能 blind replay。幂等键作用域包含项目/owner，避免不同项目同 key 相撞。

并发不自动 rebase 游戏语义。Agent A/B 的旧 token 拒绝，返回冲突字段与当前来源；Agent重新读取并给新计划。需要跨 owner 时先做协调计划，各 owner prepare/commit/abort 能力明确；没有全域原子保证则逐步骤记录已提交、待提交、补偿与 RecoveryRequired。补偿要核对目标仍是本操作写的版本，禁止覆盖后来人的修改。发布后回退是明确的新 owner 操作，不删除已发布 revision。

## 6. 自动依赖反查与变更影响

扩展当前真实 Reference parser/owner reader，优先显式引用、功能槽解析和 prepared input；代码 AST/调用图只能辅助发现候选。读取器产生版本化 readSet，结束复读一致性；漏 watcher、外部修改与重开都进行 hash 对账。解析器版本变化也触发重建，不能只监听数据 JSON。

每次变更自动分类：

1. 当前作者 shared/动作源改变：重算引用闭包、使受影响 prepared input/预览陈旧。
2. pinned 发布源：作者快照保持，但外部动作/Runtime规则的依赖仍检查。
3. 装配/装备/角色/测量靶规格改变：生成新的 card input identity，距离与可用性重准备。
4. 历史/执行冻结：保留旧输入，不在执行中替换；历史显示原版、当前对照版及差异。
5. 设计理由/名称：只影响对应设计/文字证据，不自动让无关伤害行为证据失效。

结果必须列“确证引用”“潜在代码影响”“Agent 声明”“未解析范围”，并说明影响理由和最短路径。发现依赖不等于一定破坏；源码变更未接入语义 adapter 时标潜在影响/Unknown，而不是宣称 100% 完整。全量索引重建仅作初始化、迁移或修复，不要求每次字段编辑全项目扫描。

## 7. schema、语义、风险增量验证

schema 复用严格 codec：未知/重复字段、类型、单位、枚举、范围、版本、路径安全与大小/深度边界。语义交原 compiler/owner：引用存在且身份版本一致、条件种类/作用域、事件信号来源、锚点有效与无非法循环、动作槽兼容、功能动作及正式准入/费用路径、共享依赖恰好、执行冻结禁止覆写。投影格式化成功不能替代语义编译。

| 风险类别 | 自动最小验证计划 | 不应触发的额外工作 |
| --- | --- | --- |
| 只读导航、设计理由 | 文档/设计schema、来源绑定 | Unity 编译、全量测试、Play |
| 名称/说明 | 文字投影、缓存失效和卡面文字一致性 | 重新测量动作距离或战斗回归 |
| 朝向阈值、时间预算、条件/锚点 | 受影响源编译、定向边界/退出/超时验证、共用 prepared 预览 | 每个字段或拖动帧都重启 Unity/全套测试 |
| 动作窗口、状态 mask、伤害/费用 | 定向生成/必要编译、相关状态边界/实际结算/清理 fixture；需要行为声明时定向 Play | 拿静态检查宣称真实命中或视觉已验 |
| schema、发布/恢复/身份合同 | 往返兼容、冲突、幂等与必要故障注入 | 无关场景批量 Play |
| 角色/场景/物理/玩家视觉变化 | 对应装配/测量规格与最小图形或 Play fixture | 用控制台文字预览替代图形/点击证据 |

一次候选 batch 合并重复准备与验证；失败或新修改才扩大/重验。Editor 被人或其他任务占用时排队，并显示阻塞 owner，不抢 Editor。发布原 gate 若不要求 Play 不另设强制门禁；但需 Play 的验收断言保持 Pending，不能改写成通过。

## 8. 真实证据、生命周期与失效

证据 receipt 至少由 runner 自动保存：operation/job、runner/tool/adapter version、源 readSet、作者源/编译输入/发布 identity、角色/装备/动作与测量规格、scene/fixture identity、断言及范围、开始结束、结果/错误、日志/截图位置及其 hash、实际 Editor/build module identity（适用时）。签发者和来源明确；Agent 不能自填 Passed。证据文件缺失/哈希不符显示 Missing/Tampered，源或断言/工具变更显示 Stale。

构建时匹配源码与实际加载 module，避免新源码配旧 DLL；HEAD 与时间戳只作辅助。失效按每项断言 readSet 算，而非改一个注释使所有证据报废。暂未声明依赖的 runner 保守陈旧并标明范围，不能假称精确。

生命周期分维度显示：设计声明、代码合同、配置接通、启用范围、结构/编译验证、行为/Play验证、发布版本、证据当前性。允许“设计有理由、源已配置、未启用、编译通过、Play未跑”同时存在，绝不合成单一完成勾选。

真实当前快照：workspace `e56af3d4-374b-4ae4-b005-620a5cc8b6dc` 的 currentRevision=289，cards=15、tacticalDefinitions=15；lastCompiledRevision=0、lastAppliedRevision=248 不能推出 r289 已应用/验收。面向目标 `tactical.shared.facing-target` r1 是 v4、slots=[]；用户报告 main+workspace 已安装、源码编译与8项测试通过，**没有 Play**。本次仅复核文件存在和这些保存字段。

## 9. 人查阅的 UI 与错误恢复

沿 Editor Hub 现有入口，首屏为玩法名称、目的、当前规则、实际消费者、状态与风险；技术 ID/hash/源码作为可展开详情。意图目录、模板表达、待分类源审查继续分开，真实类型导航不能把模板混成意图。人点机制可查看动作、意图、模板、装配卡、实例fixture、卡组/整备和角色/场景；缺接入明确显示，不用假卡填列表。

详情并列“Agent设计期待”“机器实现事实”“验证证据”，差异可见。装配不足的模板显示槽占位；选实际角色后自动推导可用卡与卡面，并链接 AI Lab 原编辑/事件预览 owner。卡面面向玩家用简明真实说明；管理详情可看窗口和规则路径，不把底层术语全部塞进玩家卡面。

操作页展示 before/after Diff、闭包、未覆盖项、原 owner 所需确认与 validation plan；错误返回稳定 code、对象handle/field、阶段、expected/actual version、是否已提交、是否可重试、恢复入口。持久 operation status 使重开可继续：Queued/PendingEditor、Running、Succeeded、FailedBeforeCommit、CommittedNeedsRecovery、Conflict、UnknownOutcome。Unknown 先 recover，禁盲重试；失败保留 candidate/日志与旧可用结果，旧结果标非当前。诊断不能只有“失败，请重试”。

自动观测 scan/changed/unknown counts、闭包大小、cache hit、compile/validation duration、queue wait、owner phase、retry和recovery结果；read-only GET 不改脏状态。外部编辑自动触发“当前证据已陈旧”，由系统安排对应检查，人不用手动修状态。

## 10. 最小端到端第一期与后续

本方案的第一期验收目标比当前隔离 Facing 窄切片更完整：**一个已登记机制族，连接真实事件/动作→意图/组合表达→留槽模板→至少两种实际装配卡→合成重复实例/卡组fixture→一个角色/场景验证范围，Agent操作与人查阅闭环**。不以全面机制覆盖或个人存档迁移为一期前提。

控制一期写入面：选第11节的前垫步普通回避窗口，只增加该动作事件的 typed mutation；毒 tick 是只读规则例外及定向验证对照，面向目标 r1 是既有共享依赖/生命周期对照，均不在一期另开机制写入族。装配、重复实例、卡组和场景从现有正式 owner 与内存 fixture 读取/构造，不开发新的整备编辑器。动作源生成与卡发布按各自 owner 分步收敛；所有节点可查阅、影响可见，不要求一次操作改遍全部对象。这样一期有一个实际编辑入口、一条完整使用链和一个人查阅入口，跨 owner 任意批量修改留待后续。

依赖顺序：

1. 固化权威/版本/能力合同与来源观察：复用现有Gateway、Projection、owner receipt；未知schema保留原文。
2. 补该机制族的 typed reader/规则adapter：功能垫步、普通回避及只读毒tick例外，关联既有朝向依赖，提供实际动作和准备输入；先只读。
3. 接真实角色装配 prepared result：模板占位与两种卡配置分开；合成实例/卡组只通过正式owner建立fixture，不写玩家存档。
4. 接 typed mutation+计划/CAS/幂等/共享影响；扩展单owner事务，具体候选完成后处理必要风险确认。
5. 自动预览、风险增量验证、证据绑定/陈旧：静态/编译与真实场景验证明确范围。
6. 单卡原发布owner适配、复读/刷新、Hub钻取及恢复：完成第11节真实数据演练，保留所有未接入范围。

| 后续阶段 | 内容 | 前置条件 |
| --- | --- | --- |
| P2 | 更多 Buff/弹体/控制/费用规则、角色场景，更多模板、正式整备只读反查、组发布 | 一期来源、装配、证据及恢复稳定；逐adapter计覆盖 |
| P3 | 多Agent并发、跨owner协调事务、批量引用升级与设计期待差异审核 | 原owner提供可核实prepare/commit/abort；不具备时维持可恢复步骤式协调 |
| P4 | 定向Play/Replay/视觉证据队列与长期证据导出 | 真实runner与资源/占用调度；扩规则范围仍显示Unknown |

建议模块落点（均待实施，不是本次创建代码）：

| 目录 | 职责 |
| --- | --- |
| `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/CombatDesign/` | 沿现目录加contracts、adapter registry、graph、plan/validation/evidence应用层，保持noEngine |
| `...Authoring.Cli/CombatDesignCommands.cs`、`EditorServer.CombatDesign*.cs` | 现CLI/HTTP薄封装，不另写保存/发布逻辑 |
| `Tools/MxFramework.EditorHub/web/combat-design*` | 扩现人查阅页面；Hub主页只导航 |
| `Assets/.../CharacterTest/Editor/CombatDesign/` | 项目owner桥、真实角色准备、定向runner及生产MCP薄适配；Runtime不依赖Editor |
| `Assets/.../CharacterTest/RuntimeAiPlannerDev/` | 必需的typed authoring/provenance seam；原workspace/compiler仍为owner |
| `Authoring/CombatDesign/entries/*.design.json` 或现metadata的版本化扩展 | 仅新增设计意图/理由/期待与工具引用；选择一种权威后不双写 |
| `Temp/MxFramework/CombatDesign/` | 可重建快照、索引、计划及本机队列；保留现fixture/journal边界 |
| 现 Authoring/NoEngine 与 CharacterTest test owner | 关键schema/CAS/依赖/stale与定向行为验收；不为单小模块另建asmdef |
| `Docs/Evidence/CombatDesign/<case>/` | 后续按仓库规范保留工具生成的最小证据；个人恢复日志仍在原UserSettings，不入版本化源 |

项目adapter→框架/工具合同单向依赖；framework package 不反向引用 CharacterTest。设计管理服务不进入 Runtime Tick；不改战斗同步协议来服务查阅。

## 11. 真实数据完整演练：前向垫步回避窗口 7..19→7..21

以下为**未来操作演练，本次没有修改**。本次直接读到 Common动作950008，durationFrames=46；combatTrack 的 `combat_girl_sword.evade.step.f.unhittable` 为 SetHitTargetState，frame=7/endFrame=19/mask=32。功能stepForward的身份经正式常量与动作registry解析，shared前垫步有真实引用，不靠Dodge标题判断。

1. 人给设计目标“稍增前垫步躲普通命中的机会”。Agent查询回避机制、动作与准确event handle；系统展示当前窗口、功能动作引用和毒tick例外。人决定延长到21f，Agent保存理由；无人抄ID。
2. Agent提出注册窗口操作。服务生成副本Diff、来源CAS和实际权限：Common源在只读上下文不能写；权限不足止于可审阅proposal，不复制到角色库冒充公共机制变化。存在适用授权/owner能力才执行。
3. 系统从950008功能解析与实际composition自动反查意图、组合表达、模板及卡，输出当前工作区/跨域闭包和未解析部分。仅同属“垫步”的其他方向不会自动改；不存在消费者时显示该范围0，不造名称或数量。
4. 未填槽模板仍可结构校验；真实角色/装备fixture自动准备功能动作，推导装配卡、重复实例与卡组影响。已发布shared固定旧作者源仍保持；若消费当前动作registry，则新的准备输入陈旧。历史执行不替换。
5. 原compiler检查帧范围/状态mask、引用/锚点与准入，生成同prepared identity的说明、卡面和AI Lab事件预览；动作仍46f，窗口延长不能被写成总时长延长。正式费用仍走Ability/属性owner，不复制数值。
6. 系统一次生成定向验证batch：6/7/19/20/21/22边界、状态贡献解除与普通命中；毒tick+回避用例确认分类路径不变。普通回避在HitResolveSystem先判断Alive+Unhittable，且在hit-once消耗前处理；系统显示真实顺序。
7. 毒伤详情显示ProcessPoisonTicks的CanReceiveHpDamage与Buff有效期，ShouldApplyPoisonTick的Start/EndFrame规则，TryResolveStatusTickDamage后AddPoisonTickResult直接构造Damage、CombatEntityId.None。普通Invincible/Parrying/Unhittable分类未进入该路径，**不是毒伤免疫**；状态伤害解析仍有独立规则，不宣称对所有防御无条件穿透。未做Play时保持行为Pending。
8. 保存/生成/刷新走动作owner，自动复读源与产物，然后执行原卡 owner 的发布预检。只改动作事件而作者模板/卡源未变时，明确返回“源未变，无需新卡 revision”，发布阶段仍记录当前 Catalog 与新准备输入的绑定；不能通过空改卡来制造发布完成。若设计确实需要调整模板/卡源，Agent 再用注册操作形成第二个具体候选，独立卡原publisher发布exact依赖并复读，确需组升级才走组事务。已发布作者源不冻结外部动作规则；“永远保留旧回避窗口”需要另有正式版本化动作/运行配置合同，本期不承诺。正式发布提交成功而收尾失败按原journal恢复，不生成重复版本；实例/存档不顺带写。
9. Hub呈现“窗口7..19→7..21、理由、受影响意图/模板/装配卡、角色场景、证据范围/陈旧原因”，点卡到既有预览owner，点证据看版本。机器自动刷新依赖和说明，人不填五张表、改旧报告或重勾完成。

人的介入仅设计取舍和原owner必要公共影响/发布风险确认；简单字段与重复保存/刷新/抄ID由Agent和系统完成。缺adapter、Editor、Common写权限或行为证据时明确阻塞，不以手填绕过。纯完善“面向目标r1”的理由作为轻量对照：不用Unity，编译/8测试历史scope不提升为Play。

## 12. 可量化高自动化验收

分母明确为一期注册规则、正式reader与固定fixture金标准；未知/不支持对象单列，不能从分母删失败以冒充覆盖率。

| 指标 | 一期目标与测量 |
| --- | --- |
| 引用ID手填 | 0次；从空候选到保存/预览/发布全程query handle，新增正式身份由owner分配 |
| 同一数据人工同步 | 0次；修改唯一源、删缓存重建，数值/关联/说明不用编辑第二份台账 |
| 已接入引用反查 | fixture金标准100%，包括多对多、固定旧版本、重复实例、两种装配；未解析范围有计数 |
| 变更发现/证据陈旧 | 规定用例100%：源、绑定、场景、runner/断言、missing report、同HEAD脏文件；外部改动在下次查询/提交复核一定发现，后台延迟另测 |
| 说明/预览一致性 | 支持fixture100%绑定同prepared identity；未装配不显示虚构真实数据；文字/视觉证据区分 |
| 幂等/发布 | 同key重复10次仅一次写/revision/ID分配；同key异body阻断；重复发布收敛不加版本 |
| 并发保护 | 双客户端用例100%拒绝过期revision/readSet/Catalog head，无丢失更新 |
| 恢复 | 登记故障注入点均可分类为未提交、已提交待恢复或未知阻断；补偿不覆盖后续写 |
| 人工操作 | 一次闭环仅设计取舍及适用风险确认；抄ID、维护关系、重标验证、逐页刷新=0 |
| 增量验证 | 纯理由/导航Unity测试触发=0；同风险batch去重；仅变化、失败或未决风险才扩测 |
| 可读查阅 | 机制详情两次导航内到实际消费者/卡预览与陈旧原因；缺接入可见，无假列表 |
| 未解析透明 | 发现的未知schema、操作、动态关联或场景全部显示Unknown/Unsupported，不静默丢弃 |

记录扫描量、闭包规模、缓存命中、任务/编译时长和队列等待，实测后再定延迟指标；不承诺全项目100%语义理解或未经测量的绝对秒数。当前源码数量与已有窄切片记录不能替代上述完整验收。

## 13. 本次阅读证据与未验证项

关键来源（符号优于易漂移行号）：

- [动作WorkspaceSession](../Packages/com.vincent.wgameframework/Editor/CharacterAction/CharacterActionAuthoringWorkspaceSession.cs)：Execute、SaveAll、ValidateDiskHashesBeforeSave。
- [共享Composition owner](../Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestRuntimeAiLabDraftWorkspace.Composition.cs)：TacticalDefinitionImpact、TryReplaceTacticalDefinition、TouchCompositionCards。
- [DraftPersistence](../Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestRuntimeAiLabDraftPersistence.cs)：staging/hash/复读；[作者codec](../Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestTacticalAuthoringJson.cs)：SemanticFingerprint。
- [PublishedSource](../Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestTacticalPublishedSource.cs)；[组级发布](../Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestRuntimeAiPlannerProjectCatalogAuthoringSession.PublishCurrentChanges.cs)；[目录Write](../Assets/Scripts/WGameFrameworkProject/CharacterTest/Editor/RuntimeAiPlanner/CharacterTestRuntimeAiPlannerProjectCatalogEditorBridge.cs)。
- [Gateway](../Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/CombatDesign/CombatDesignGateway.cs)；[CatalogProjection](../Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/CombatDesign/CombatDesignCatalogProjection.cs)；[MechanismProjection](../Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/CombatDesign/CombatDesignMechanismProjection.cs)；[Editor owner](../Assets/Scripts/WGameFrameworkProject/CharacterTest/Editor/CombatDesign/CharacterTestCombatDesignOwner.cs)。
- [CLI](../Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/CombatDesignCommands.cs)；[HTTP](../Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/EditorServer.CombatDesign.cs)；[Hub](../Tools/MxFramework.EditorHub/web/app.js)；[当前接口说明](Interfaces/CombatDesign.Authoring.md)。
- [预览缓存](../Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestRuntimeAiPlannerDevSceneController.TacticalPreviewCache.cs)；[动作输入](../Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestTacticalCardInputs.cs)与Bindings；[PlayerService](../Assets/Scripts/WGameFrameworkProject/CharacterTest/TacticalCard/CharacterTacticalPlayerService.cs)。
- [毒tick](../Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/Combat/CharacterTestCharacterCombatTraceSystem.Projectiles.cs)：ProcessPoisonTicks/AddPoisonTickResult/ShouldApplyPoisonTick；[HitResolveSystem](../Packages/com.vincent.wgameframework/Combat/Hit/HitResolveSystem.cs)；[Common动作源](../CharacterActionSource/Libraries/Common/character_actions.json)。
- [机制目录](Guides/CHARACTER_TEST_COMBAT_MECHANICS_CATALOG.md)、[动作作者指南](Guides/CHARACTER_ACTION_AUTHORING_GUIDE.md)、[QUALITY_GATE](QUALITY_GATE.md)、[PROJECT_INDEX](PROJECT_INDEX.md)及根/Assets/项目/package AGENTS。

已读取Unity项目盘点技能与本地GitNexus exploring/impact-analysis技能；本会话未发现GitNexus工具，未重建或运行索引，直接源码阅读完成核查。依赖关系只是影响候选，不能采用“直接调用必然破坏”的绝对判断。用户本次仅文档授权高于技能的索引更新建议。

未验证：Editor/HTTP/MCP当前连通性、任何新代码实际编译与测试、真实网页点击/视觉、图覆盖完整性、性能、所有故障恢复、角色真实装配与Play。原文与交接记录有历史验收范围，本文保留其来源和局限。本次交付没有权限超范围动作，也没有被审核阻断的剩余写入。

### 关键来源快照（机器生成）

采集于 2026-10-02T19:58:44.2175398Z。以下是本次阅读的定位指纹，不是验证通过记录，也不是业务数值/关联台账。并发工作树后续变化时保留本快照，重新采集并审阅变化，不能只刷新日期冒充已复核。

| 仓库相对路径 | 文件字节 SHA-256 |
| --- | --- |
| Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/CombatDesign/CombatDesignGateway.cs | d790c0f3244f5771c4cc367e0b2b63d2c551940c9c45b1ead54a4914d727b140 |
| Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/CombatDesign/CombatDesignCatalogProjection.cs | 17eaa927546329b04c043593ddfd43da0d3af7347fe1bad88ea22aa0d4b3b042 |
| Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/CombatDesign/CombatDesignMechanismProjection.cs | 73cb06f7e3d2509d6224f5d581fdd666f405f839682c91b98d6fe726a967eaa9 |
| Assets/Scripts/WGameFrameworkProject/CharacterTest/Editor/CombatDesign/CharacterTestCombatDesignOwner.cs | f2d300df63b5cd72c7a70f596053fbfe845d581d965a3e3048426500465f5c21 |
| Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/Combat/CharacterTestCharacterCombatTraceSystem.Projectiles.cs | 0eaff8d3b4a3171b97ccf2f7eb40a38de44760a9f5282ed3a0de6dfe940f403e |
| CharacterActionSource/Libraries/Common/character_actions.json | edc9b600e49f6b71785e86dbbc52472f257b90afff2e318af6f740aa9e026d9b |
| Authoring/RuntimeAiLab/Drafts/e56af3d4-374b-4ae4-b005-620a5cc8b6dc/workspace.json | b2e8656aa8b677ca30aecbb62f901e0339665b2dd5927ee28dc45047dcfa7763 |

### 2026-10-03 只读复核补记

2026-10-03T03:06:44Z 再次读取上述七个关键来源，字节 SHA-256 均与上表相同；HEAD 仍为 `2e9ad0f6e4167552dbede5ac7e4bdb6c9437925d`。当前工作区仍为 r289、15 张卡草稿、15 份共享源，`facing-target` 索引 revision=1。这只证明文件快照相符，不新增编译、8测试或 Play 证据，也不复验历史一期运行记录。

本轮另核实 `CharacterTacticalPlayerService.cs` 的打开/保存副作用，SHA-256 为 `62acec143744cd23a8e56556205813e1a51c0208d39747cf783fce2e3352bffe`，据此增加只读实例 adapter 与内存 store 限制。动作窗口清理可定位到 `CharacterTestHitTraceWindowState.ProcessStartCombatRequests` / `ProcessStopCombatRequestsAndClear`：Enter/Active 添加状态贡献，BeforeFrame Exit 在命中前移除，AfterFrame Exit 在本帧之后移除。方案的边界验证需覆盖实际生命周期边界及提前中断，不能仅检查 JSON 中 endFrame 或按名称判断免疫。

本轮保持同一份独立 Markdown 交付；没有新增第二份数值/关系源，也没有修改其他实现、作者源、生成目录、Unity 资产或存档，未调用 Unity 或启动工具服务。

### 2026-10-03 04:17 UTC 交付复核

本轮再次直接检查动作 WorkspaceSession、共享定义 owner、组级发布与目录恢复 owner、Gateway、三个入口中的 Catalog/机制投影、HTTP 路由、隔离 Editor owner、毒 tick 与普通 HitResolve 路径。上表七个关键来源 SHA-256 均未变化；工作区仍为 r289、15 份卡草稿与15份共享源。Unity版本文件为 `6000.6.3f1`，本轮没有启动 Editor。引用路径核查不构成接口运行、图覆盖或行为验收。

据当前代码，落地应优先补**同一 Gateway 下的能力登记、真实装配只读 adapter、增量依赖与证据失效**，再接唯一一个动作窗口 typed mutation 和原发布 owner。现有隔离接口已能做 Facing 候选编译、说明/卡面文字和逐卡发布复读，但其输入明确是 `Unbound(1)`；不能用该结果替代角色动作装配、持有实例、整备或场景验证。第10节完整一期与历史已完成的隔离窄切片保持分开。

实施后的自动化回执应逐阶段给出 `source-current / prepared-current / catalog-current / evidence-current` 及阻塞原因：保存后由系统安排受影响准备、说明、索引和最小验证，成功后自动推进；失败停在对应 owner 阶段，保留已提交结果与恢复入口。它们是从版本和执行结果推导的状态，不新增四个让人手动勾选的字段。未接入的关联显示 Unknown，不能要求人工填写消费者名单作为“临时自动化”。设计取舍和确有需要的风险确认仍由人负责。

本轮使用 Unity 项目盘点技能及本地 `review-project-evidence` 的证据分级规则。用户开放权限没有改变本任务“仅方案、不实现”的范围；本轮只维护此 Markdown，没有权限阻塞或待批准的写入。
