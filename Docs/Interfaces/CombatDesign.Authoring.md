# Combat Design Authoring：确定性只读目录

本文中的 Agent 指 Development Agent。概念层级以 [角色动作与战术行为卡第零章](../../StorySource/玩法基石/角色动作与战术行为卡.md) 为准；共享源目录不是“全部都是意图”的分类依据，Sequence 也不是独立组合身份。

## 当前接口与权威源

| 入口 | 返回协议 | 范围 |
| --- | --- | --- |
| `CombatDesignCatalogProjection(projectRoot).Query()`；`authoring combat-design catalog --project-root <project>`；`GET /api/combat-design/catalog` | `mx.combat-design.catalog-projection.v1` | 已索引共享源、已识别卡组合表达、事实、直接引用反查及分类来源 |
| `CombatDesignMechanismProjection(projectRoot).Query()`；`authoring combat-design mechanisms --project-root <project>`；`GET /api/combat-design/mechanisms` | `mx.combat-design.mechanism-projection.v2` | 五个玩法机制的设计注释、有限固定规则提取、实际功能回避关系 |
| `GET /api/combat-design/intent`；`combat-design intent` | 兼容原 `mx.combat-design.intent-projection.v1` | 原单份前垫步样板；新目录 UI 不再依赖此样板接口 |
| 原 `query/plan/submit/status` 与 Editor owner | 既有协议 | 保留原 owner 快照和编辑/发布通道；不作为新目录层级 |

机制接口由 v1 升为 v2：`rules` 是规则提取结果，`annotation`/`response` 是设计注释，`relationships` 是目录关系数组，`catalogProjectionHash` 绑定所用目录。旧 v1 的单样板关系结构不继续冒充统一目录。

读取来自 `Authoring/RuntimeAiLab/Drafts/*/workspace.json` v4 及其真实索引文件、共享 `source`、卡 `payload`、动作目录和编译器/条件词汇源。每份索引校验 identity、revision 和 DraftPersistence 的 LF/旧 Windows 换行文本 hash；`readSet` 保留原始文件字节 SHA256，两种 hash 不能混用。v3 工作区明确未解析，不迁移、不写入。拒绝越界路径、reparse point、重复 JSON 字段和超过 8 MiB 的单文件；JSON 深度上限 128，条件/组合描述递归上限 32。

接口全部只读，不编译、不排队 Editor，不写 authoring、发布数据、玩家存档或战斗参数。CLI 战斗设计 JSON 输出固定 UTF-8，便于 Windows Agent 管道读取。

## 确定性生成的事实

源 v4 的事件 ID、操作参数、条件树、时间起止锚点、优先级、每轮执行上限、冷却、事件块完整参数、动作槽声明、源 revision/hash 均直接读取。事件开始/结束与事件块锚点分别保留；不会按标题补全时间或超时策略。条件数值枚举来自既有 C# 显式枚举；显示名、值种类和单位来自既有 ConditionCatalog descriptor 调用，不复制一套游戏数值或条件标签表。

固定格式化支持已登记编译操作、逻辑 All/Any/Not、SubjectScope、EventSignal，以及当前描述目录支持的布尔/数值/TargetState 条件。Not 保留原逻辑；例如独立 Facing 的 `非(夹角 ≥ 阈值)` 与前垫步的 `夹角 ≤ 阈值` 不因名称相似而合并。TargetState 显示三个真实 mask 字段，不能误解释成 `value=0` 的数值条件。其他事实、枚举、节点、时间锚点或旧 payload 明确 `unparsed`，完整原文始终保留；这是有限解析，不替代正式 schema/编译器语义校验。

识别的 `mx.character-test.tactical-card-composition.v1` 仅按 Sequence/Reference 的真实节点抽取直接引用并反查工作区共享定义；不扫描任意字符串充当业务引用，也不把 Sequence 自动命名为独立意图组合。未知节点不猜测隐含依赖。引用是多对多，缺失目标报 `MISSING_REFERENCE` 并保留目标 ID。当前尚未覆盖跨工作区图、任意嵌套共享 schema 的传递关系、配置后可用卡、持有实例、卡组/整备、角色和场景验收。

功能槽仅对已知 `stepForward/stepBack` 解析既有代码常量及动作目录；普通角色动作槽保留声明并显示“角色绑定未解析”。解析动作身份不证明当前角色装配。机制回避关系进一步要求真实动作目录的 `SetHitTargetState` 事件包含从正式 flags 源解析的 Unhittable bit，不从 Dodge 名称或共享源标题猜关联。未建立的其他机制关系显式保留缺口。

## 设计注释与固定规则模板

`Tools/MxFramework.Authoring/metadata/combat-design-view.json` 是视图设计注释及固定提取模板，包含：

- 五个机制的名称、设计目的、玩家应对；标明 Agent 编写的设计注释，带既有设计文档 path/selector/reviewedHash。
- 已明确复核的共享源角色声明；来源漂移则角色回到待分类。缺少角色声明也显示待分类并保留真实对象。
- 有来源的固定规则格式模板、源 selector、必要 token 和复核源码 hash；没有任何副本战斗参数或手工模板/卡引用台账。

固定模板是有限的已复核代码解释合同，不是任意 C# 自动语义推断。请求时从当前源码确定性提取原文和行号，核对源码指纹及必要 token；不匹配则停止解释、显示当前原文及 `UNPARSED_OR_STALE_FIXED_RULE`。设计目的与应对保持独立来源/陈旧状态，不能冒充机器证明的设计理由。

中毒规则模板展示 `ProcessPoisonTicks` 的可受 HP 伤害检查、`AddPoisonTickResult` 经状态伤害解析后直接构造 Damage，以及 `ShouldApplyPoisonTick` 的 EndFrame/StartFrame 判断；因此明确普通 Invincible/Parry/Unhittable 分类不等于 DOT 免疫。就绪模板保留连续稳定采样与独立暂停 owner 边界；构造器默认不冒充实际场景配置。回避体力 tuning 的身份从正式代码常量读取，数值从已有生成 JSON 读取；未验证当前角色/场景加载。

## 版本、失效与 UI

工作区、源对象和引用按稳定 ID 排序；有执行意义的事件/子节点数组顺序保留。`projectionHash` 对输出 payload 进行递归键排序后 SHA256，包含生成事实、分类/规则状态、readSet 与诊断，排除 `generatedUtc` 和 hash 自身。相同输入产生相同 payload/hash；生成时间只是独立观察时间，不影响确定性身份。机制返回所用目录 hash。结束时复读文件 hash，合并读取发现同源版本不一致也标记；不把混合版本读取当当前证据。

Editor Hub 分为「玩法机制 / 意图目录 / 模板表达 / 源审查 / 原 owner 快照」。意图目录只显示分类来源仍相符的已确认意图，默认和搜索都不混入模板或待分类源；模板制作表达有独立目录，并在意图详情中作为引用者出现。源审查默认列待分类共享源，也可明确选择全量共享源、未解析旧卡草稿、已登记可选组合或全部对象原始源；审查列表不代替意图分类。搜索与详情均受所属目录范围约束，旧混合目录 URL 及机制/引用跳转按真实对象类型进入对应目录，不能把模板详情塞入意图视图。选择、搜索和机制导航写入 URL，刷新和前进后退恢复；原 owner 快照保持兼容。

“重新读取权威源”仅执行 GET。每次请求重建数据与关系；检测 hash 改变则显示先前投影已不能充当当前证据，代码合同和分类来源分别自动失效。没有后台文件监听或轮询，变化在下一次页面加载/主动重读时发现；不得承诺离线页面即时获知所有变更。请求失败显示错误和重试，已有缓存标记为非当前。

**规则归设计者。** 本页没有硬规则、成功/失败/超时、动作准入或玩家覆盖编辑入口；模板保留规则，玩家只可使用既有装配合同明确开放的合法项。设计分类、源码相符、源已索引、启用、运行验证分维度展示。草稿编译 revision/status 仅是原始元数据，不是 content-hash 绑定的运行回执；截图仅验证管理 UI。Unity、Play、发布均不因此宣称完成。

当前真实观测为 15 份共享源：3 份有来源的意图分类、12 份待分类；15 份卡草稿中 13 份为已识别模板制作表达、2 份旧行为 payload 未解析。另有 1 份旧共享 schema 和 5 份动作相关共享源的部分条件描述未覆盖。这是读取时点和有限解析范围，不是永久数量或全项目覆盖承诺。
