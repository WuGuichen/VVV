# 战斗设计管理系统落地方案：Agent 编辑，人轻松查阅

> 状态：Design / 待实施。本文只新增独立方案，不实施业务代码。
>
> 证据仓库：E:\Unity\WxFramework\WGameFramework。2026-10-01 UTC 只读核查，HEAD = 992edb4bb288aea046714c47bd6c1bddddcffea7；包含当前未提交工作树，不能仅凭 HEAD 重现全部证据。
>
> 仓库位置：Docs/COMBAT_DESIGN_MANAGEMENT_SYSTEM_PLAN.md。文档先在授权工作目录完成，再通过受支持的权限审批路径申请仅新增此文件；执行结果以工具回执为准。
>
> 本次未启动、连接或占用 Unity，未编译、测试、Play、发布、提交或推送，未读取个人存档。当前 Unity 版本配置为 6000.6.3f1。

## 1. 决策摘要

采用“既有领域 owner + 设计语义旁注 + 自动事实投影 + 多对多依赖索引 + 版本化证据”的方案。Editor Hub 新增战斗设计查阅入口；Agent 使用同一结构化服务查询、修改和编排既有 owner 事务；AI Lab 继续承担战术作者编辑、当前角色准备、时间轴和真实测试；角色、动作、Buff、弹体、发布目录及存档继续由各自原 owner 负责。

高自动化不是把策划字段搬到新表格：引用候选、ID 分配、源与派生对应、动作时长、距离摘要、费用事实、固定共享依赖、反向影响、校验结果与证据失效均由机器处理。人只决定目的、取舍及必要风险确认。Agent 撰写“为什么如此设计、希望形成怎样的博弈”；系统提取“当前实现实际怎样工作”。二者并列可追溯，但不互相冒充。

第一期必须打通一个机制修改到角色卡预览、发布检查和人查阅的闭环；不能只交一个机制目录，也不能要求人手填一份动作—意图—模板—卡关联台账。第一期只对显式接入的有限规则族承诺可计算覆盖；未知规则显示未解析，不根据名字猜测。

本文使用：**事实**＝当前源码/配置直接支持；**记录**＝用户或既有报告给出的验证，未重跑；**建议**＝待实施合同；**未知**＝还没有足够证据。凡下文新增 API、目录、schema、自动化指标均为建议，不能当作现有功能。

## 2. 已有链路、可复用能力和真实缺口

### 2.1 作者事务与编译发布

| 已核查 owner / 符号 | 当前事实 | 本系统复用与新增边界 |
| --- | --- | --- |
| CharacterActionAuthoringDocumentState、CharacterActionAuthoringWorkspaceSession | 文档按 Kind + SourcePath 定位，包含 canonical baseline SHA-256、revision、dirty；Execute 接受 ExpectedRevision。SaveAll 先 Validate、复核磁盘 hash，再 SaveAllDirty，成功更新基线。文档种类还包括 Projectile、MechanicTuning、AbilityEffectTimeline | 复用文档 owner 与字段描述。已有 Execute 命令集不等于任意轨道字段都可远程改；需补白名单 typed mutation adapter，不能旁路写 JSON |
| CharacterTestRuntimeAiLabDraftWorkspace、CharacterTestTacticalAuthoringEdits | 编辑令牌携带 WorkspaceId/WorkspaceRevision/CardId/CardRevision/DefinitionRevision/上下文身份；clone 后编辑，CaptureState/PushUndo，异常恢复 | 适配 Agent 操作，保留原版本检查。不能直接把任意 Action 委托或反射方法名暴露给 Agent |
| DraftWorkspace.Composition 的 TryReplaceTacticalDefinition、TacticalDefinitionImpact | 共享修改检查工作区及定义版本；多卡受影响需 confirmedSharedImpact；更改后触及引用卡与所在组 revision。现有反查是当前工作区引用级 | 扩展为跨草稿、发布快照、绑定和证据的 typed 反查；现有局部反查不能宣称全项目影响覆盖 |
| CharacterTestRuntimeAiLabDraftPersistence | 0.5 秒延迟自动保存；保存前校验、分配数字身份，写 staging、hash 引用、复读，再安装工作区。草稿保存与发布是两个状态 | 继续使用原保存 owner。外部工具与 Editor 同时写同一工作区所需跨进程 CAS/租约须补，当前 revision 检查不能代替这项保证 |
| CharacterTestTacticalAuthoringCompiler、TacticalCardBinding | 作者端点、条件、槽位与实际动作准备生成现有执行表；作者指南规定执行中拒绝覆盖及恢复依赖核查 | 复用同一个 compiler、binding 与 prepared result，不另做“设计版执行器” |
| CharacterTestTacticalPublishedSource | 正式作者源固定 composition + 使用到的 shared definitions；读取拒绝缺失、重复、未使用依赖；独立于 actor binding | 冻结发布依赖；发布卡不因共享草稿变化而自动改成新版本 |
| ProjectCatalogAuthoringSession.TryPublishCard | 单卡发布用已有 Stable ID、连续 revision、来源 semantic/content hash 与目录文件 hash；提交后复读准确 revision/content hash | 独立卡继续单独发布，不为查阅或发布单卡顺便升行为组 |
| PreparePublishCurrentChanges / CommitPublishCurrentChanges | 组级发布有 preview token、source/candidate identity、主工作区、草稿/卡/组预检及版本冲突；重复确认返回 duplicate 结果 | 复用现有准备/提交/恢复合同；Agent 计划应附具体影响及 token |
| ProjectCatalogEditorBridge.Write / Recovery | 卡文件、卡索引、组目录先 staging 校验，持久备份与 phase journal，安装后复读，Committed 标记；提交前失败恢复，提交后收尾失败不伪报回滚 | 这是目录事务，不是全项目跨作者域分布式事务。新增编排层必须承认边界 |
| PublishRecovery / PublishRecoveryJournal | UserSettings/RuntimeAiLab/PublishRecovery/current.json 记录 before/target/preview；recover 核对已提交、未提交或仍未确认；恢复不新增 revision | 操作回执可复用这些状态，但日志属于本机恢复状态，不等于可提交的验证证据 |

来源详见附录 A 的 E03–E08；只读代码核查没有证明故障恢复在本机本次实际运行通过。

### 2.2 Catalog、自动说明与预览

| 路径 / 服务 | 事实与限制 | 对方案的含义 |
| --- | --- | --- |
| CharacterActionAuthoringSearchCatalog / SourceCatalog；CharacterTestActionBrowserCatalog | 作者指南规定读取 Common 与角色作者源，生成副本不重复计数；选择资格、动作类别和角色范围分别判断 | 搜索引用从真实目录来；ActionSet 基础攻击映射不是持有或战术选择权限 |
| CharacterTestActionBrowserCatalogExporter | 现有 Rebuild Action Browser Catalog 入口；指南说明分类保存更新投影与整理报告 | 新索引订阅 owner receipt，只刷新变化范围；保留显式全量重建作为修复手段 |
| TacticalPreparationCatalog、SummaryPreparation | 当前真实角色、装备、动作和测量规格准备摘要；摘要不是实际命中。旧根距离摘要不能用于新表面净空规则 | 时长和距离不能人工同步；来源改变则自动 stale，必要摘要才重测 |
| TacticalPreviewCache | key 含 workspace/card revision、references StableHash、角色/队伍/槽位/会话、历史冻结、PreparationCatalog.Revision、actor/presentation revision | 复用缓存边界；选择、展开或只改说明不应重新做完整战斗准备 |
| TacticalBlockDescription / DslDescription；CardUiPreview | 说明按真实事件结构、块、条件及准备结果识别，不依赖显示名称；未知复杂结构不能套简单成功文案 | 共用编译/准备结果生成“尝试”“条件”“上限”等说明；机制规则面板补充伤害路径事实 |
| CharacterTacticalPlayerService.TryRefreshPublishedCards | 先暂存定义再替换目录；返回无效 owned 引用数量，不自动修复或保存持有状态 | 发布刷新不能暗中修改卡牌实例或个人装配；系统只报告影响并给正式 owner 操作入口 |

已核实当前 workspace 为 e56af3d4-374b-4ae4-b005-620a5cc8b6dc、currentRevision=289、shared 文件 15 份；lastCompiledRevision=0、lastAppliedRevision=248 是保存字段，不能推出 r289 已运行应用或已验收。

独立“面向目标”共享源为 tactical.shared.facing-target r1，v4 作者源，无动作槽。主工作树有面向完成规则及说明等未提交代码。用户记录为已安装到 r289、源码编译及 8 项测试通过，**未 Play 验收**；本文核实文件安装与工作树状态，没有独立复核那 8 项运行报告。之前机制目录中的“待合入”属于更早采集快照，不能继续当当前状态。

### 2.3 Editor Hub、HTTP、CLI 与 MCP

| 接口现状 | 可复用范围 | 缺口 |
| --- | --- | --- |
| Tools/MxFramework.EditorHub/web/app.js 与 README | 当前七个工具入口、最近工作及服务连通性；主页 GET /api/character/packages；诊断页 GET /api/character/readiness | Hub 只启动/导航。新增“战斗设计”入口与页面，不在主页复制所有编辑器、验证器或发布 owner |
| GET /api/character-actions/catalog | 返回动作目录、Common/角色 sourceHash、轨道存在性等 | 可用作候选入口，不能据目录记录认定已配置/已执行 |
| POST /api/character-actions/copy-to-character | expectedRevision + sourceHash，分配 catalog lease；角色库/应用配置写入失败可回滚 lease | 复用身份及复制事务；Common 写权限遵守当前规则，不用复制操作冒充全局共享动作修改 |
| POST /api/character-action/open；GET /api/character-action/open-status | 精确 Profile/ActionSet/Library/Action/源 hash/模式，请求与状态文件桥接 Unity，202 表示待处理 | 可导航动作 owner，不能把 open=accepted 当 save/compile 完成 |
| GET /api/character/tactical-content | 只读 published 目录；返回卡 revision、contentHash、slots、motionCost | 无完整机制—事件—意图关系，没有战术 structured mutation/publish API |
| Authoring CLI 的 character、buff、projectile validate/compile/import-unity；projectile workspace/mutate/apply | 已有独立于 Unity 的作者 CLI，以及专属领域的事务/生成器 | 当前 Program 未暴露统一 combat-design 命令；不得把建议命令写成已有命令 |
| runtime-ai-lab-character-action-authoring 等 MCP 工具 | 现有工具源码属于 TriggerAutomation 测试 owner；start/status 调度真实 Unity/Play 验收 | 是验证工具，不是通用战术 CRUD API；第一期新增生产 authoring gateway，与测试工具隔离 |
| Unity MCP 工具可见性 | 本会话有相关工具元数据；未连接/调用 | Editor 连通性、运行状态均未验证；规划不可假设一直可用 |

本地已读取 GitNexus exploring / impact-analysis skills。它们的图索引可作为候选线索，但本次没有调用或重建索引；不能照搬“直接调用必然破坏”的推断。正式变更影响由引用合同、数据绑定、代码路径和证据条件给出，代码依赖本身不证明破坏。

## 3. 对象、权威与禁止重复的数据

术语权威是 [角色动作与战术行为卡](../StorySource/玩法基石/角色动作与战术行为卡.md)第零章；制作/验证/发布规则见 [战术作者指南](Guides/CHARACTER_TEST_TACTICAL_CARD_AUTHORING.md)。机制与意图相辅相成：机制解释规则，意图表达目的；一个机制可服务多种意图，一个意图可同时消费多种机制。

| 对象 | 唯一权威 / 当前兼容映射 | 新系统可保存与可派生内容 |
| --- | --- | --- |
| 机制（横切规则） | 实际领域代码、作者配置、接口合同；目前没有统一机制业务表 | 新增设计条目保存目的、取舍、验收意图和自动选中的证据引用。公式、数值、状态 mask、生产者/消费者/退出链从 adapter 派生；代码常量保持代码权威 |
| 时间轴事件 | CharacterAction 轨道事件与 tactical source 事件各归自己的 owner | 事件身份、操作种类、时序/锚点、条件、来源投影；两种事件不可因都叫“事件”合并命名空间 |
| 角色动作 | CharacterActionSource/Libraries/Common 或角色包源，动作 ID catalog 分配 | 时长、Root Motion、命中窗口、requirements、成本路径、动作摘要与资源依赖派生；不复制一份 editable 数值 |
| 战术意图 | tactical-shared 当前混合承载，按内容与明确设计声明辨认 | Agent 给目的和解释；事件及机制关联由 typed operation 与代码 adapter 提取 |
| 意图组合 | composition / shared source 的实际引用结构；组合是可选 | 顺序/并行/条件来自源；未知语义标签允许 Agent 说明，不凭工厂名自动归类 |
| 战术模板 | 留槽的基础卡源，对应模板制作语境；独立模板类型尚不能宣称全部迁移 | 槽位、参数及结构 readiness；未装配时真实时长/距离为 unknown，显示占位且不可执行 |
| 装配后的战术卡 | 模板 + 角色装配/参数 + prepared inputs 的解析结果；发布作者源与 actor-bound execution 分开 | 可用配置、绑定来源和 execution identity 派生；不能把测试单卡直接当正式持有 |
| 卡牌实例 | CharacterTacticalPlayerService 的 owned InstanceId / NextCardInstanceId；TacticalCardInstance 又是 battle-local identity | 使用 typed owner domain 区分持有与战内实例；第一期用合成 fixture 只读投影，不读/写个人存档 |
| 卡组 / 整备 | CharacterTacticalPlayerService / SaveStore 的绑定及 preset；另有既有行为组配置 | owned 重复份数、CardInstanceId 与装配索引需按真实 owner；group entry 不自动等价于持有实例 |
| 角色 / 场景验证 | Character package、world-object/level owner 与具体 validation runner | 声明场景/角色适用条件，生成 fixture 绑定及验证计划；不复制角色数值或场景 Transform |

**可以新增的持久数据仅有新职责**：Agent 设计意图/理由、必要的设计分类与验收期待、适配器代码/规则、受信运行产生的可复核 evidence receipt。机器事实、依赖边、影响列表、UI 摘要、Catalog 和 readiness 均可重建；缓存删除不能丢设计源或领域数据。

设计条目里的引用通过 query/select 返回的 typed handle 绑定，机器填 locator，不要求人或 Agent 从文件抄 ID。尚不存在的机制用系统分配的新 design identity，状态“仅设计”，不得伪造实际代码绑定。机制变为代码后，adapter 产生候选，Agent 选择绑定并解释；没有可靠 adapter 的关联只能标“Agent 声明 / 未核实”，不能升级机器事实。

### 3.1 多对多关系与版本轴

~~~mermaid
graph LR
  M[机制规则] -->|作用于 / 消费规则| A[角色动作]
  M --> E[时间轴事件]
  M --> I[战术意图]
  A -->|槽绑定 / 功能解析| C[装配后的卡]
  E --> I
  I --> O[可选意图组合]
  I --> T[可留槽模板]
  O --> T
  T --> C
  C --> P[持有卡牌实例]
  P --> D[卡组 / 整备]
  R[角色 / 场景] --> C
  R --> D
  V[版本化验证证据] --> M
  V --> A
  V --> C
  V --> R
~~~

这是关系示意，箭头不是继承树，也不是限制只能沿一条路径追踪。Buff、Ability、Projectile、资源、代码符号可直接连多个对象。图中机制—意图/动作关系还要区分自动事实与设计声明。

每条边保存：edgeKind、from/to typed identity、field locator/symbol、owner、source version/hash、读取模式（current/pinned/frozen/binding-dependent）、extractor version、可信类别。管理索引需容许关系环，遍历 visited 去重；只有执行结构禁止的循环，如锚点依赖环，才交 compiler 阻断。

同一变更至少区分四种反查：当前作者草稿需要重准备；固定旧 shared revision 的 published 卡保持旧作者源；仍消费当前动作目录的卡需要检查动作变化；冻结运行/历史快照继续保持原输入。固定 shared 版本不代表全部 Runtime 外部规则也已固定，不能直接显示“不受影响”。

## 4. 自动事实与设计语义

### 4.1 接入方式

第一期写少量显式、可测试的 RuleAdapter：命中目标状态窗口、普通命中分类、DOT 伤害路径、朝向条件/预算、动作槽与固定功能动作。adapter 读取正式格式和明确符号，返回 typed facts + evidence anchors；不要依靠关键词搜索给全项目自动判定语义。源码 AST/调用图可提供候选；事实提升须满足 adapter 合同或正式运行 probe。

单个规则事实结构建议：

~~~json
{
  "factKind": "DamageRoute",
  "authority": "Code",
  "producerSymbol": "AddPoisonTickResult",
  "route": ["TryResolveStatusTickDamage", "HitResolveKind.Damage", "CommonDamageApplication"],
  "ordinaryHitClassifier": "Bypassed",
  "targetDefensePolicy": "ResolvedByStatusDamageResolver",
  "sourceAttribution": "CombatEntityId.None",
  "evidence": "server-generated-evidence-handle",
  "confidence": "StaticVerified",
  "runtimeValidation": "NotRun"
}
~~~

这只是新接口示例，字段由 adapter 产生，不能由 Agent 任填就成为 Verified。UI 展示“来源、作用、结算、解除、例外、当前使用、证据”，展开可看源码符号与版本。

### 4.2 精确规则例子

- 中毒：Buff 601012 / 状态 901012；刷新保留原 StartFrame，EndFrame <= 当前帧不 tick；当前每 60f tick，基础 2 点并经共同防御结算。毒池到期停止刷新，不立即移除尚存 Buff；区域 Exited remove 是独立配置。AddPoisonTickResult 直接构造 Damage，无普通 HitResolveSystem 的 Invincible/Parry/Unhittable 分类。因此“回避窗口延长”不能被自动说明写成“毒伤免疫”；基础 2 不等于最终固定扣 2。
- 普通回避：Common 前垫步动作 950008，DurationFrames=46，SetHitTargetState mask=32，动作帧 7..19 为当前配置闭区间。这是 Unhittable，不是已启用 Invincible；应显示动作进度帧，慢速或冻结时不换算成固定墙钟时间。
- 面向前垫步：tactical.shared.facing-forward-step r2 使用 stepForward 功能槽解析 950008；默认 ±5°、准备预算 30f，显式准备超时继续按当前朝向执行。正式 ActionUseAuthority 提交 Ability 900001、15 体力后才发布动作；30f 准备预算、300f 完成保护与显示动作长度分开。
- 独立面向：tactical.shared.facing-target r1 无动作槽；不能因此要求补主攻击摘要。其源码/配置已出现、用户记录有编译/8 测试，不等于真实战斗朝向和结束已 Play 验收。

依据 [机制证据目录](Guides/CHARACTER_TEST_COMBAT_MECHANICS_CATALOG.md)与本次直接读取毒伤及前垫步代码/配置。机制目录是核查快照，不能作为永不失效的第二权威。

Agent 可写“前垫步用于短窗口脱险”，但系统不应宣称从代码自动推断出了全部战术目的、平衡收益或玩家体验。设计声明与实际事实矛盾时显示“设计期待与实现不同”，保留原话和版本，交人取舍。

## 5. Agent 结构化操作合同

新增一个 CombatDesignApplication 服务；HTTP、CLI、生产 MCP 和 UI 都调用它，不能各自实现保存/编译/发布。所有能力均返回 capability 状态，明确 RequiresEditor、Available、Unsupported、Busy；服务启动不隐式开 Unity。

| 建议操作 | 输入 | 输出与 owner |
| --- | --- | --- |
| query / resolve | 类型、关键词、场景/角色范围、current 或 pinned | 候选 typed handle、可用性、来源、歧义原因；缺失/多个候选不猜第一个 |
| inspect / dependents / impact | handle + source snapshot | 来源、机器事实、设计旁注、直接/传递影响与未解析范围 |
| create-design / amend-rationale | 目的、理由、查询选中的绑定 | 新设计 identity 与版本；自动分配，不复制机制数值 |
| prepare-change | base handles、expected versions、typed operations、理由、风险范围 | candidate、精确 Diff、读写集合、依赖闭包、validation plan、preview、单次 plan token |
| validate / preview | plan token，角色/场景 fixture 句柄 | schema/语义/编译/说明/卡面结果；未知与失败保留源 |
| commit-draft | plan token / idempotency key | 通过领域 owner 保存；结果含每一 owner 的实际提交阶段 |
| prepare-publish / publish | 明确独立卡或组，expected hashes、approval capability（如确需） | 原 Publisher/ProjectCatalog bridge 的 revision/receipt；不隐式升级所有消费者 |
| status / recover / cancel | operationId | 原事务核对/续尾/停止；取消已提交请求不等于撤回已发布版本 |
| export-review | 范围、语言、snapshot | 同一事实投影生成可读 HTML/Markdown；导出不作为编辑权威 |

建议接口根：/api/combat-design/*、CLI combat-design query/plan/validate/status、MCP combat_design_query/plan/status 等，**均未实现**。JSON 只接受 operation kind 白名单与 typed payload；禁止任意脚本、shell、反射调用或客户端指定最终 hash/Verified 状态。测试 MCP 不当生产写入口。

请求形态示例（handle、guard、token 均来自前置查询，非手抄）：

~~~json
{
  "schema": "mx.combat-design.operation.v1",
  "operationId": "allocated-by-service",
  "idempotencyKey": "generated-by-client",
  "baseSnapshot": "server-snapshot-handle",
  "operations": [{
    "kind": "UpdateActionTrackEvent",
    "target": "resolved-action-event-handle",
    "patch": {"endFrame": 21}
  }],
  "rationale": "拟延长前向垫步普通命中回避窗口，保留毒伤规则。",
  "requestedScope": "SelectedOwnerOnly"
}
~~~

### 5.1 幂等、事务、并发与回滚

1. query/prepare 是只读或隔离候选；plan 固定 source/设计文档/依赖 read set、write set、compiler/adapter/schema 版本、preview identity。确认共享影响或发布必须针对这个具体 plan，不能批准模糊的“后面都可以”。
2. expected revision 与文件 hash 双重保护；不以时间戳判冲突。提交前重读所有 authoritative 输入及目录 head；有变化返回 Conflict + Diff，保留候选。不同 owner 改动可并行准备，但共享工作区/目录写入由 owner 串行协调。
3. idempotencyKey + canonical request hash 绑定一次请求；同 key 同请求返回既有 receipt，同 key 不同内容报错。跨重启在本机 durable operation journal 查询阶段；重复调用不多领 ID、不多出 revision。幂等记录保留期内保证，过期 token 报 Expired，不能当新请求再执行。
4. owner 单域使用现有事务。跨域默认拆为显式有依赖的步骤，所有可准备项先完成，再进入写入；新增“协调事务”必须先实现 participant prepare/commit/abort 与 durable journal，不能把多次保存称作全局原子。
5. 提交前失败：discard candidate / 原 owner restore；提交后尾失败：CommittedNeedsRecovery；状态不确定：CommitUnconfirmed，复读核对。只有当前文件仍等于本事务 after hash 才允许补偿恢复 before，不覆盖别的 Agent 后续写入。
6. 发布回滚分语义：事务未完整提交可回滚；已确认 published revision 应追加受控版本/引用调整，不删除已存在历史。运行中 frozen candidate 保持原身份，不自动切换。
7. plan 审批绑定风险、scope、before/after hash；公共动作/多引用共享修改、人请求的发布等需按照实际 owner gate 确认。查询、说明、派生刷新和可逆草稿无需逐字段确认。若当前授权已经覆盖具体风险，使用该授权。
8. UserSettings/Temp journal 属本机；可提交 evidence 摘要与运行报告分开保存，不把个人路径、存档或 session 日志复制到设计源。外部网关默认本机与授权项目范围，拒绝路径逃逸、任意目标根和来源身份伪造。

当前作者 owner 没有统一跨进程事务协议；第一期必须补“单 writer + 文件 CAS + owner 状态核对”的最小保证。不具备安全 mutation adapter 的规则先只读/提案，不给“直接 apply”按钮。

## 6. 自动反查、变更影响和增量刷新

从正式 reader/compiler 捕获引用：shared definition 引用、node/event anchor、slot resolver、具体 action/ability/buff/projectile/resource、published exact revision、角色绑定/scene payload。编译产物应输出 provenance；缺少字段先增补 provenance adapter，禁止让人补完整关联清单。

代码机制关联采用明确符号绑定与 rule adapter；例如 SetHitTargetState→目标状态分类，以及 PoisonTick→直接 Damage 路径。代码符号候选和完整语义覆盖不是一回事。未解析动态分派/外部资源/旧 schema 显示 UnknownDependency，相关高风险发布保守检查。

更新顺序：owner receipt → 对其 read/write set 和 hash 重取事实 → 替换该源边集合 → 求反向影响闭包 → 分类 stale → 按风险重编译/重建需要的预览 → 更新 UI。文件 watcher 仅发 dirty hint，复读 hash 后才接受变化；漏通知通过主动刷新/启动 snapshot 对账恢复。

影响结果必须解释路径，例如：

~~~text
动作 950008 的 combatTrack event endFrame 改变
  → 命中回避规则事实改变
  → stepForward resolver 绑定的卡配置 execution inputs 改变
  → tactical.shared.facing-forward-step 的消费者需重准备
  → 所属测试配置 / 卡面事实 / 场景验证标记 stale
~~~

这里不能无条件把全部消费者源 revision 都提升：共享源没有改就不发 shared r3；只有动作源变化也不凭空发卡作者 rN+1；应生成新的 action/input snapshot、执行准备与 validation identity。反查输出“源需编辑 / 仅派生需重建 / 固定旧依赖 / 待升级 / 未知”。

发布目录刷新、动作目录刷新、摘要刷新、卡面刷新和 owned catalog 刷新分别有自己的 receipt/version；Hub 页只消费更新。局部变更只重建受影响闭包；打开折叠区、换选中项、改纯理由均不触发 Unity 完整启动/测试。

## 7. Schema、语义和按风险验证

### 7.1 分层校验

| 层 | 自动检查 | 成功能证明什么 |
| --- | --- | --- |
| 读取/schema | 大小/路径、duplicate key、非有限数、字段类型、支持版本、身份域、未知格式 | 可解析；未知旧内容保留只读或 repair draft |
| 引用与结构 | 全局/局部身份唯一、active allocation、exact shared revision、槽/锚点/条件对象存在、执行环和资源引用 | 结构可成立；占位模板允许保存，不等于可执行 |
| 领域语义 | 目标规则、动作角色资格、时长/帧空间、完成/超时/失败策略、能力需求、条件值范围、装配一致 | 当前领域合同可通过 |
| 编译/派生 | 既有 compiler/loader、prepared dependency identity、无旧摘要混用、描述与卡面消费同一产物 | 生成配置/预览一致，不等于真实命中/Play |
| 集成证据 | owner 定向生成/导入/目录复读/mirror 对比、必要 Editor compile | 输出安装到指定位置和版本，不等于当前角色启用 |
| 行为验证 | 命中边界、正式扣费、控制清理、场景探针、Play/Replay | 只对具体角色、场景、输入及断言的观察成立 |

新设计条目 schema 建议 mx.combat-design.design-entry.v1；operation、projection、evidence 各自版本。优先在服务代码提供 schema/reader/validator；不让 Markdown 表格当机器字段定义。schema 迁移生成 dry-run Diff，保留原 owner ID/hash 合同；打开旧内容不自动升级。

### 7.2 默认执行策略

| 变更风险 | 自动触发的最小验证 | Unity / Play 策略 |
| --- | --- | --- |
| 纯设计理由/阅读标签/名称 | 文档 schema、绑定存在性、presentation Diff；无需改变战斗事实 | 不运行 Unity 测试 |
| 单引用结构/参数且不改变运行核心 | 本次闭包的引用/语义与 compiler 检查、卡面/时间轴预览 | 无新 C# 时不每步刷新；对本次提交合批处理 |
| 动作窗口、shared 事件、完成预算、成本/伤害 | 源 owner validate、受影响准备/说明、focused 边界验证 | 一次批次 Editor compile（需时）；命中/成本/控制等真实行为在合批验收点做定向 Play，不每次输入重跑 |
| 源码普通改动 | 按仓库根 AGENTS 单次 refresh/compile + ready + Console error；证据不足保留 Pending | 不默认 csproj 构建和全量测试；CharacterTest 模块按其专项规则安排命中 focused 验证 |
| 事务/ID/schema/确定性/Save/Replay/网络核心 | 专项冲突、恢复、迁移及 hash/replay 回归 | 需要相关专项证据；不能以简单预览替代 |

按 batch 生成一次 validation plan；缓存 key 绑定 relevant inputs、tool/规则/断言版本。某步骤已通过且输入未变则复用，不重复运行。简单功能遵守项目默认门禁，不为每一个编辑手势跑全量 Unity。

系统管理数据可以发布为“待 Play 验证”的查阅快照；涉及正式运行发布的门禁依领域规范与风险政策执行。不能为了高自动化把缺失 Play 自动改成 Passed，也不能为了一个文案修改要求重放所有战斗。

## 8. 证据绑定、失效与分维生命周期

证据由真实工具/runner 回执产生，不接受 Agent 自填 Passed。每份 evidence 至少绑定：

- repo HEAD 与相关未提交文件 SHA-256；authoring owner identity/revision/文件 hash；
- published stableId/revision/semanticHash/contentHash、共享 exact dependencies；
- actor、装备/装配、scene fixture hash、prepared inputs/execution identity；
- schema/compiler/adapter/test/断言版本，工具 ID、job/run ID、完整报告文件 hash；
- 验证类型（Static/Compile/Unit/Editor/Play/Replay/Visual）、scope、观察结果、时间及受信来源。

版本快照既要含本次输入，又要保存断言/观察口径。新代码但旧断言、相同 HEAD 但不同未提交源、换装备/靶机、缺失报告、编译器/adapter 升级均可能 stale。为避免全部文案影响所有证据，明确 evidence 的实际依赖集合；无法证明精确性时扩大失效范围并解释。

失效不抹掉历史 Passed：当前显示 Stale（哪项输入改变），历史仍显示“旧版本曾通过”。缺报告为 Missing；不能执行为 Blocked；无可信来源为 Untrusted；未运行为 NotRun；成功指向准确范围。Library/临时报告可丢失，若要长期证明，正式 runner 应按项目 evidence 规范导出 hash 绑定副本，不能只留绝对 Library 路径。

| 生命周期维度 | 状态举例 | 判定来源 |
| --- | --- | --- |
| 设计 | 无 / Agent 草拟 / 已确认 / 设计期待待更新 | 设计条目及确认记录 |
| 代码 | 未找到 / 部分基础 / 静态链路完整 | 当前 symbol/adapter/消费者证据 |
| 配置接通 | 无源 / 源可读 / 已编译 / 已导出 / 已导入并复读 | 正式作者及生成 receipt |
| 启用 | 未配置 / 可选择 / 场景或角色已引用 / 已观察使用 | config/binding 与 runtime probe 分别标注 |
| 验证 | NotRun / Passed(scope) / Failed / Stale / Missing | 相应工具真实 evidence |
| 制作与版本 | DraftSaved / UnpublishedChanges / Published rN / Frozen test vM / RecoveryPending | owner 状态 |

不能一个“完成”勾覆盖六列。静态完整 + 已导出 + 未启用 + NotRun 是合法可见状态；“面向目标 r1”目前也不升格为 Play 已验证。

## 9. 人的查阅 UI 与可观测恢复

Hub 入口打开独立战斗设计页面。默认是可搜索列表和清晰正文，不要求人读依赖图或手填字段：

1. 总览按机制、意图/组合、动作、模板/卡、角色/场景切换；每项显示上述独立状态，过滤“有未发布修改”“验证已陈旧”“当前启用”“仅设计”。
2. 详情首屏给设计目的、真实规则摘要、当前使用与例外；“设计理由”和“实现事实”明确标注来源。ID/hash 默认收起，点击名称可定位 owner。
3. 关联页给“在哪里用”“为什么受影响”“固定哪个版本”；多对多图是辅助视图，表格/句子为主。未装配槽显示“待装配，无真实时长/距离”，不是假数值。
4. 预览复用 AI Lab 的说明、时间轴和卡面。未准备/编译失败隐藏执行标尺；历史 frozen 内容只读，不能显示成当前源。
5. 变更页给前后 Diff、准确影响对象、自动处理项、剩余验证及必要风险确认；不把 commit/publish/activate/test 合成一个按钮。
6. 错误以中文原因、阻断阶段、operationId、受影响对象和恢复动作显示；技术详情保留 code、field path、before/after hash 和日志。复制报告不暴露个人存档。

操作状态建议 Planned → Prepared → DraftCommitted → ValidationPending → PublishPrepared → CatalogCommitted → TailCompleted，失败旁支为 Conflict / FailedBeforeCommit / CommitUnconfirmed / CommittedNeedsRecovery / Cancelled。实际 phase 来自 owner，不能靠前端超时猜测。

“重试”必须说明是否再次写入。状态待核实仅给“核对目录”；确认已提交仅给“继续恢复”；真正未提交才允许重做计划。取消候选和撤回已发布内容是不同动作。UI 刷新失败只标 projection stale，不把正式提交描述成失败或已回滚。

## 10. 最小端到端第一期与阶段顺序

### 第一期：一个闭环，有限但明确的规则覆盖

覆盖：普通目标状态/回避窗口、中毒路径展示、独立面向与面向前垫步预算；Common 和所选角色动作目录；一个主 AI Lab 工作区、引用它们的 composition、一个已配置测试卡 fixture、独立卡发布预检/回执读取、合成 owned instance/deck/preset 只读样例、一组角色/场景验证描述。

第一期可写闭环先选已有共享 owner 的“面向目标”容差/预算修改：通过 TryReplaceTacticalDefinition 形成新共享版本，自动更新实际引用的模板/卡草稿，重准备卡配置，独立卡发布固定依赖并复读。动作是该卡的绑定依赖，不要求每次机制修改都修改动作源。Common 回避窗口第一期至少支持只读分析和完整提案；是否开放公共动作写入，须先解决当前只读模式和正式维护权限，不能把它作为无条件交付指标。第十二章展示该扩展路径的完整风险与阶段边界。

端到端：Agent 查询已注册机制 → 准备一次受支持 owner 修改 → 产生依赖/影响 → 源/编译/说明/卡面检查 → 定向验证或明确 pending → 发布准备/原 owner 提交（需授权时具体确认）→ 目录复读/恢复 → Hub 人查阅。第一期必须显示实例/卡组关系与“不适用/未接入”，不把整链尾部藏掉；不承诺迁移全部旧 group entry 或读写个人存档。

依赖顺序：

1. **权威/身份/provenance adapter**：reader、typed handle、revision/hash、草稿/发布/frozen 区分；不改 runtime 模型。
2. **事实/依赖投影**：上述有限规则、shared/slot/anchor/actor 引用、当前/固定依赖反查；对照真实 reader。
3. **Agent 单 owner 计划事务**：白名单 mutation、candidate、CAS、幂等、共享影响确认与恢复；先只读 fallback 再开放受支持写。
4. **编译/说明/预览和增量验证**：复用 prepared result，生成风险计划与 evidence stale。
5. **独立卡发布适配与只读 Hub**：完整 receipt 展示、目录刷新、frozen/owned 区分；完成一次端到端样例。

### 后续阶段

| 阶段 | 扩展 | 前置条件 / 明确范围 |
| --- | --- | --- |
| P2 | 更多 Buff/Projectile/Ability、条件/控制机制，多个角色与场景，测试库/整备 preset 反查；组级发布适配 | 第一期版本、恢复与 provenance 稳定；逐 adapter 计覆盖，不宣布所有机制自动理解 |
| P3 | 多 Agent 并发、跨域 participant 协调事务、设计期待与实现差异审查、批量引用升级 dry-run | 各 owner 提供可核实 prepare/commit/abort；不足时继续步骤式协调，不伪造原子 |
| P4 | 多场景定向 Play/Replay/视觉证据队列、可读导出与长期 evidence、扩大 adapter | 有真实 runner、资源预算与信任来源；Editor 用户占用时排队，不擅自抢占 |

估算开发时长、实际索引延迟及全项目覆盖率未测，不给“几天全部实现”承诺。

### 10.1 建议目录与模块

以下都是后续实施目录，本次不创建这些代码/源结构：

| 建议位置 | 内容与边界 |
| --- | --- |
| Authoring/CombatDesign/entries/*.design.json | 仅设计目的/理由/验收期待与工具生成引用；无战斗数值、无人工关系表 |
| Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/CombatDesign/ | contracts、query、graph projection、plan、validators、evidence binding；no Unity 依赖 |
| Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/EditorServer.CombatDesign*.cs | 薄 HTTP 适配与 CLI dispatch，统一 application service |
| Tools/MxFramework.CombatDesign/web/ | 人查阅与候选 Diff/恢复页；Hub app.js 只加导航注册 |
| Assets/Scripts/WGameFrameworkProject/CharacterTest/Editor/CombatDesign/ | 项目 rules/compiler/owner bridge/真实验证 adapter 与生产 MCP 注册，保留 Runtime/Editor 边界 |
| Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/ | 仅必要 typed authoring seam/provenance，原 workspace/session 仍是 owner |
| Tools/MxFramework.Authoring/tests/MxFramework.Authoring.Tests/ | schema、CAS、幂等、依赖、stale 的无 Editor 验证 |
| CharacterTest 原 EditMode/PlayMode test owner | 必要场景/窗口/伤害/正式成本/清理的 focused 验收，沿既有程序集 |
| Temp/MxFramework/CombatDesign/ 或现有忽略缓存根 | 可重建 snapshot/index/报告，非版本化事实源 |
| UserSettings/RuntimeAiLab/ 与新服务本机 journal | 原发布恢复照旧；新增操作 journal 不替换现有恢复合同 |
| Docs/Evidence/CombatDesign/<case>/ | 实施后由工具生成、按项目规范保留的最小证据回执/摘要 |

依赖严格为项目 adapter → 框架/外部工具合同；框架 package 不反向引用 CharacterTest。第一期不为每个小子模块新增 asmdef，也不把 EditorHub/设计系统塞进 Runtime Tick。

## 11. 可量化高自动化验收

以下均为待实施的验收指标，分母限定第一期已注册对象/规则与测试 fixture；未知项单列，不靠排除失败偷偷提高覆盖率。

| 指标 | 第一期目标 | 测量方式 |
| --- | --- | --- |
| 引用 ID 人工抄填 | 0 次 | 从空候选到保存/预览/发布样例，事件/槽/动作/shared/card/角色全部 query handle；新 identity owner 分配 |
| 同一领域数值/关联人工同步 | 0 次 | 修改一次源，删除缓存重建事实、依赖、说明；无需编辑另一张配置/台账 |
| 已接入显式引用反查 | fixture gold set 100% | 正式 reader/compiler 真实引用与索引双向比对；含多对多、重复持有、固定旧版本 |
| 已接入输入的变更检测 | fixture 100% | owner receipt、文件外部修改、重开漏 watcher、同 HEAD 未提交不同 hash 均发现 |
| 证据自动 stale | 规定依赖变化用例 100% | 源/绑定/scene/tool/断言变更、missing report；纯理由不失效无关行为证据 |
| 预览一致性 | 所支持 fixture 100% | 描述/卡面/时间轴使用同一 prepared identity；槽缺失不展示真实时长 |
| 幂等 | N=10 同 key 重试仍仅一次 write/revision/ID allocation | 含超时和服务重开；同 key 异内容阻断 |
| 冲突保护 | 双客户端 fixture 100% 拒绝过期写 | revision、文件 hash、目录 head 分别竞争；无丢失更新 |
| 故障恢复 | 注册阶段注入点全部能分类并恢复/保持明确阻断 | 不要求“永不失败”；要求已提交不重复发布、未知状态不报成功、补偿不覆盖新写 |
| 人工操作 | 一次案例仅设计取舍 + 按实际 gate 必需的风险/发布确认 | 记录交互；目录刷新、输入抄写、重复保存/改报告不让人代劳 |
| 增量验证 | 纯理由/只读导航的 Unity 全量测试触发=0；一次风险 batch 不因 N 字段反复重启 | job/phase 计数；高风险专项和实际必要验收保留 |
| 阅读可达 | 从机制详情两次导航内看真实消费者、卡面与陈旧原因 | 固定验收任务；ID/hash 可展开而不是首屏填表 |
| 未解析规则透明 | 所有发现但不支持的输入显示 Unknown/Unsupported | 包括旧 schema、动态路径、未接入场景；不得隐藏或伪造完整覆盖 |

性能先测基线；一期目标是增量只重建依赖闭包、UI 导航不重准备，并记录 scan/compile/cache hit/job duration。没有实测前不承诺绝对秒数或“全项目 100% 自动”。

## 12. 真实案例完整演练：延长前向垫步回避窗口

这是**基于真实数据的未来操作演练**，本次没有执行修改。选动作 950008 的 Unhittable 窗口从 7..19 拟改为 7..21；不改变 DurationFrames=46、Root Motion、体力成本或中毒路径。

1. **Agent 解析意图与现状**：人提出希望前向垫步更容易躲普通命中。Agent 查询回避机制，系统返回 Common 动作 #950008 和准确 event handle，显示 mask=32/Unhittable、闭区间与普通命中消费者，旁边提示“中毒 tick 绕普通命中分类”。人决定延长到 21f 的平衡取舍；Agent 给理由，不手抄 stableEventId。
2. **准备受支持 owner 改动**：服务读取 Common 源、ID catalog、目标 event 和受影响 prepared inputs，生成 before/after Diff。Common 当前存在只读场景及来源写权限限制，adapter 必须显示真实权限：没有维护权限时止于 proposal，不能复制到角色库并伪称改变公共机制。获得适用权限后的操作才走正式动作文档 owner/生成器；本例不是授权绕过只读。
3. **自动展开影响**：直接规则事实是前向 Step 的回避窗口；动作 consumer 包含固定 stepForward→950008；真实 shared 为 tactical.shared.facing-forward-step r2。系统从当前 composition/test 配置找出使用它的模板/卡，不人工列名称或虚构数量。未装配模板结构仍合法；该功能槽的真实动作预览更新。其它方向 Step 与撤步还击不得因同属“垫步”自动改窗口；只有实际引用到 950008 的绑定才算影响。
4. **跨卡、实例、卡组、角色与场景**：对每个被解析出的卡报告 binding/execution input 改变；对 published 卡列固定作者源，但可能消费当前动作目录；对已有 owned fixture、卡组/preset 报告需重准备，不隐式写持有或存档；对场景验证标 stale。当前工作区没有相关 consumer 时显示“该范围 0 引用”，可通过原 owner 建立最小测试 fixture，不能拿示意卡当现成内容。
5. **校验和可读预览**：事件仍在动作合法帧范围，state mask 不变；动作总时长仍 46f，面向准备仍 30f，正式成本仍由 Ability 900001 决定。编译受影响卡配置、预览说明和卡面；如果简单卡面不展示窗口，机制详情应显示变化及对应动作，不硬塞所有技术数据进玩家文案。
6. **按风险一次验证**：静态/编译检查一次；命中边界计划覆盖 6/7/19/20/21/22 的状态贡献及退出清理，真实命中选择最小定向 fixture；Poisoned + 回避窗口用例检查 DOT 仍按原路径结算。修改 mask/endFrame 不凭名称推断毒伤免疫。真实 Play 若暂不可执行，显示 PendingExternalEvidence，不要求每次拖动窗口全量重跑。
7. **保存、安装、发布检查**：动作源通过原 owner 保存与定向生成/刷新，复读源/产物版本。shared r2 未变就不发布 r3；已发布卡作者源未变就不自动加 revision。若同时确实修改了模板/卡作者源，按独立卡 Prepare/Publish 发布 exact shared 依赖；需要行为组升级才走组级事务。目录 commit 已确认而 tail 失败时恢复旧 token，不再造新 revision。当前 frozen 测试保持旧版本，下一次准备才使用新输入。
8. **人轻松查阅**：Hub 显示“回避窗口 7..19→7..21，设计理由、动作 #950008、实际受影响意图/模板/卡、角色/场景、验证 scope 与 stale 原因”；点卡看既有 AI Lab 卡面/事件预览，点证据看绑定版本。不让人手动刷新五份表格、抄动作 ID、改描述数值或把旧 Play 证据勾回完成。

普通字段和派生处理由 Agent/系统完成；人只负责设计取舍及真实 owner 要求的公共影响/发布风险确认。缺 Editor、Common 写权限、adapter 或 Play 证据时是明确 capability/blocker，不能靠更多人工手填“补齐”。

补充轻量案例：将“面向目标 r1”的设计理由完善，只改变设计旁注时应无需 Unity；修改源朝向容差才触发引用闭包编译与定向验证。源码编译/8 测试记录保留原 scope，仍不代表 Play。这可验证系统确实区分设计语义、配置事实和验证证据。

## 13. 本次交付与尚未验证

已完成：只读读取根/Assets/项目/package/相关测试 AGENTS，相关本地 skills、概念权威、当前作者指南、EditorHub/CLI/HTTP、动作与 AI Lab owner、发布/恢复、说明/预览/持有与保存 owner、真实毒伤代码、Common 前向 Step 与当前 workspace。当前工作树其他改动没有处理。

未完成或未知：没有运行现有 Editor/HTTP/MCP/CLI，没有验证服务连接、性能、故障注入、全部旧 schema、所有机制 adapter 覆盖或实际 Play；用户所述 8 测试只标记录。方案全部新模块/接口待实现。

文档写入范围仅限新增本 Markdown；目标已存在时拒绝覆盖。主仓库写入通过受支持的提权审批路径处理，审批失败则保留授权工作目录内的完整交付，不更换工具绕过。系统代码、资产、存档及索引均不属于本次写入范围。

## 附录 A：关键源码与阅读证据

路径以本文预期的仓库 Docs 位置为基准；符号优于易漂移行号。E 前缀是本文证据编号，非新的运行数据 ID。

| 证据 | 当前源码 / 关键入口 |
| --- | --- |
| E01 | [概念权威](../StorySource/玩法基石/角色动作与战术行为卡.md)；[战术作者指南](Guides/CHARACTER_TEST_TACTICAL_CARD_AUTHORING.md)；[动作作者指南](Guides/CHARACTER_ACTION_AUTHORING_GUIDE.md)；[机制证据目录](Guides/CHARACTER_TEST_COMBAT_MECHANICS_CATALOG.md) |
| E02 | [EditorHub README](../Tools/MxFramework.EditorHub/README.md)；[Editor 接口](Interfaces/Editor.md)；[QUALITY_GATE](QUALITY_GATE.md) |
| E03 | [动作 DocumentSession](../Packages/com.vincent.wgameframework/Editor/CharacterAction/CharacterActionAuthoringDocumentSession.cs)：CanonicalizeJson、BaselineSha256、PreparedDocumentWrite；[WorkspaceSession](../Packages/com.vincent.wgameframework/Editor/CharacterAction/CharacterActionAuthoringWorkspaceSession.cs)：Execute、SaveAll |
| E04 | [作者编辑](../Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestTacticalAuthoringEdits.cs)：EditToken、TryEditTacticalSource；[Composition owner](../Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestRuntimeAiLabDraftWorkspace.Composition.cs)：TacticalDefinitionImpact、TryReplaceTacticalDefinition |
| E05 | [DraftPersistence](../Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestRuntimeAiLabDraftPersistence.cs)：Save；[数字身份](../Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestRuntimeAiLabDraftNumericIdentities.cs)：PrepareForSave |
| E06 | [ProjectCatalogAuthoringSession](../Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestRuntimeAiPlannerProjectCatalogAuthoringSession.cs)：TryPublishCard；[PublishCurrentChanges](../Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestRuntimeAiPlannerProjectCatalogAuthoringSession.PublishCurrentChanges.cs)：Prepare / Commit / duplicate |
| E07 | [CatalogEditorBridge](../Assets/Scripts/WGameFrameworkProject/CharacterTest/Editor/RuntimeAiPlanner/CharacterTestRuntimeAiPlannerProjectCatalogEditorBridge.cs)：Write、phase journal、installed validation；[Editor recovery](../Assets/Scripts/WGameFrameworkProject/CharacterTest/Editor/RuntimeAiPlanner/CharacterTestRuntimeAiPlannerProjectCatalogEditorBridge.Recovery.cs) |
| E08 | [PublishRecovery](../Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestRuntimeAiPlannerProjectCatalogAuthoringSession.PublishRecovery.cs)：RecoverPublishCurrentChanges；[RecoveryJournal](../Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestRuntimeAiPlannerProjectCatalogAuthoringSession.PublishRecoveryJournal.cs) |
| E09 | [TacticalPublishedSource](../Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestTacticalPublishedSource.cs)：CaptureDraft、Read、SemanticHash；[PublishedPublisher](../Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestTacticalPublishedPublisher.cs) |
| E10 | [AuthoringJson](../Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestTacticalAuthoringJson.cs)：SemanticFingerprint 排除 name/title/description；[AuthoringSource](../Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestTacticalAuthoringSource.cs)：v2/v3/v4 |
| E11 | [TacticalPreviewCache](../Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestRuntimeAiPlannerDevSceneController.TacticalPreviewCache.cs)；[BlockDescription](../Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestTacticalBlockDescription.cs)：TryParse；[PreparationCatalog](../Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestTacticalPreparationCatalog.cs) |
| E12 | [HTTP ActionLibrary](../Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/EditorServer.CharacterActionLibrary.cs)；[ActionOpen](../Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/EditorServer.CharacterActionOpen.cs)；[TacticalContent GET](../Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/EditorServer.TacticalContent.cs)；[CLI Program](../Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/Program.cs) |
| E13 | [ActionIdCatalog](../Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/EditorServer.CharacterActionIdCatalog.cs)：catalog hash、lease、Rollback；[Editor identity bridge](../Assets/Scripts/WGameFrameworkProject/CharacterTest/Editor/CharacterTestTacticalAuthoringEditorBridgeRegistration.cs)：BatchResolveForAuthoring |
| E14 | [测试 MCP 工具](../Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Gameplay/TriggerAutomation/Editor/TriggerTestRuntimeAiLabCharacterActionAuthoringMcpTool.cs)：start/status；[TriggerAutomation AGENTS](../Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Gameplay/TriggerAutomation/AGENTS.md) |
| E15 | [Poison tick](../Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/Combat/CharacterTestCharacterCombatTraceSystem.Projectiles.cs)：ProcessPoisonTicks、AddPoisonTickResult、ShouldApplyPoisonTick；[普通命中分类](../Packages/com.vincent.wgameframework/Combat/Hit/HitResolveSystem.cs) |
| E16 | [Common 动作源](../CharacterActionSource/Libraries/Common/character_actions.json)：950008 combatTrack；[动作 ID 分配权威](../CharacterActionSource/action_id_catalog.json) |
| E17 | [当前 workspace](../Authoring/RuntimeAiLab/Drafts/e56af3d4-374b-4ae4-b005-620a5cc8b6dc/workspace.json)：r289；[面向目标 r1](../Authoring/RuntimeAiLab/Drafts/e56af3d4-374b-4ae4-b005-620a5cc8b6dc/tactical-shared/4a76d1fe626452f3412afbbe518e89b06a1584842e0ebfe9d0ac8e9729168373.json) |
| E18 | [CharacterTacticalContent](../Assets/Scripts/WGameFrameworkProject/CharacterTest/TacticalCard/CharacterTacticalContent.cs)：PublishedSlots 与非功能槽；[PlayerService](../Assets/Scripts/WGameFrameworkProject/CharacterTest/TacticalCard/CharacterTacticalPlayerService.cs)：InstanceId、TryRefreshPublishedCards；[SaveStore](../Assets/Scripts/WGameFrameworkProject/CharacterTest/TacticalCard/CharacterTacticalSaveStore.cs)：框架 CAS；[战内 Instance](../Assets/Scripts/WGameFrameworkProject/CharacterTest/TacticalCard/TacticalCardInstance.cs) |

### Hash 合同注意

不能把 hash 字段一律当成同一种值：文件 SHA-256 用于字节冲突；动作 canonical SHA-256 表示文档基线；TacticalAuthoringJson.SemanticFingerprint 的 canonical JSON 排除 name/title/description；PublishedSource.SemanticHash 用 RuntimeContentHashAccumulator 累加固定源 JSON；published ContentHash 再绑定 envelope identity/revision/runtime contract/metadata 与 semantic hash。新增证据 fingerprint 可以用 SHA-256，但不得替换既有 runtime/hash 合同。

下方只读采集的文件 SHA-256 用于定位本次文档证据快照，不是第二份业务数值/关联表；未来系统应由工具自动生成和对账。

### 本次关键文件快照（机器生成）

采集时间：2026-10-01T17:02:00.6243466Z。未提交文件必须按各自 hash 对齐，HEAD 相同并不足够。

| 仓库相对路径 | SHA-256 |
| --- | --- |
| Authoring/RuntimeAiLab/Drafts/e56af3d4-374b-4ae4-b005-620a5cc8b6dc/workspace.json | b2e8656aa8b677ca30aecbb62f901e0339665b2dd5927ee28dc45047dcfa7763 |
| Authoring/RuntimeAiLab/Drafts/e56af3d4-374b-4ae4-b005-620a5cc8b6dc/tactical-shared/4a76d1fe626452f3412afbbe518e89b06a1584842e0ebfe9d0ac8e9729168373.json | 5d0c81bd55fefd4c4291ce521b405d702666ef2acc03fd945024f42f79441f34 |
| CharacterActionSource/Libraries/Common/character_actions.json | edc9b600e49f6b71785e86dbbc52472f257b90afff2e318af6f740aa9e026d9b |
| Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/Combat/CharacterTestCharacterCombatTraceSystem.Projectiles.cs | 0eaff8d3b4a3171b97ccf2f7eb40a38de44760a9f5282ed3a0de6dfe940f403e |
| Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestRuntimeAiPlannerProjectCatalogAuthoringSession.PublishCurrentChanges.cs | 030bbee98a3afa8c733fee26573126079bd09f206e270bafb6e927aba85aabca |
| Assets/Scripts/WGameFrameworkProject/CharacterTest/Editor/RuntimeAiPlanner/CharacterTestRuntimeAiPlannerProjectCatalogEditorBridge.cs | 09197e450b526f70e5cd0a056c796d4d463a8ce08b178c9ec6d70322b79e110d |
| Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/EditorServer.TacticalContent.cs | bb103b4c9252a29512368b17e776bef4076c63d7ea9a0a593ebfd3013caef192 |
| Tools/MxFramework.EditorHub/README.md | 2ae7f0766bc8c1d53ab1298625d85b41a60f5eb35ed002b5352c0f7567889338 |
