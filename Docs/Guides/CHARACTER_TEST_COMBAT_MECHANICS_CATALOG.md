# CharacterTest 战斗机制分类与证据快照（2026-10-01）

> Status: Guide
> 2026-10-08：仅迁移 Genesis 引用；下方机制盘点与原验证范围仍为2026-10-01快照，当前实施状态按对应功能现行契约读取。

> 状态：源码／配置核查快照，不是新机制设计或运行验收报告。
>
> 核查时间：2026-10-01 16:08～16:33 UTC（本机 UTC+8：2026-10-02 00:08～00:33）。
>
> 核查目录：`E:\Unity\WxFramework\WGameFramework`，分支 `main`；核查开始与写文前 HEAD 均为 `992edb4bb288aea046714c47bd6c1bddddcffea7`。
>
> 范围：当前框架 Combat／CharacterAction／CharacterControl／Gameplay、CharacterTest 组合与运行消费者、当前作者源与生成配置。未启动／连接／占用 Editor，未运行编译、构建、测试、Play Mode；未改代码、资产、存档或作者工作副本。

## 先看结论

- **中毒完整接通，并有现有内容入口**：毒瓶 → 毒池 → 刷新 Poisoned Buff → 定时毒伤 → 通用伤害应用 → Buff 到期清理。起始区域另有进入／停留刷新、离开移除中毒配置。
- **“无敌”要分开看**：`Invincible` 有状态写入、合并、命中免疫和退出清理基础，但当前核查动作配置没有启用；垫步／翻滚实际配置的是 `Unhittable` 回避窗口，不应改称已经启用 Invincible。
- **背刺、流血未找到当前实现闭环**。旧枚举审计、世界观描述和 VFX 分类名称不作为机制证据。
- 已有机制还包括接近／守距／后撤／环绕、动作与条件等待、成本／冷却、武器与身体判定、命中次数策略、防御减伤、散势压力／破聚／击退／击飞、物理盾、光环、禁足、凝滞、眩晕，以及弹体、毒池、地雷、炮台和物理场。
- 独立“面向目标”意图在主体核查时仅位于 `face-target-20261002` worktree，按**待合入增量**单列。最终复核观察到该改动及共享源已被其他任务同步到主工作树、尚未提交，见下文并发补记；本文没有把它升格为已核查／验收能力。

## 文档职责与判定口径

现有权威入口适合按原职责维护：

| 入口 | 应维护的内容 |
| --- | --- |
| [角色动作与战术行为卡](../../StorySource/玩法基石/角色动作与战术行为卡.md) | 事件、意图、组合、模板、卡、实例、卡组及动作装配的概念权威；设计契约不自动代表实现完成 |
| [战术行为卡作者源与摘要准备](CHARACTER_TEST_TACTICAL_CARD_AUTHORING.md) | 当前制作、编译、配置、共享意图和验证边界 |
| [技能实现类型指南](CHARACTER_TEST_SKILL_IMPLEMENTATION_PATTERNS.md) | 各类技能生产者、运行路径、编辑入口 |
| [Combat 接口](../Interfaces/Combat.md)、[CharacterAction 接口](../Interfaces/CharacterAction.md)、[Gameplay 接口](../Interfaces/Gameplay.md) | 公开运行合同及模块边界 |

未找到同时覆盖“机制类别、具体规则、当前生产者与使用状态”的统一机制总表，因此本次只新增本文，不修改上述文档。本文是证据索引，不替代接口权威。

“完整接通”限定为**静态证据形成链路**，不表示本次运行通过：

| 标记 | 含义 |
| --- | --- |
| 完整接通 | 找到具体生产者／当前配置、运行状态或输入承载、消费者、结算或结束／退出路径 |
| 部分基础 | 已有字段、接口、消费者或通用通道，但缺具体生产者／启用配置／接入环节；须写明缺什么 |
| 仅设计 | 找到设计表达，未找到对应当前实现证据 |
| 未找到 | 在本文明确范围内没有找到该机制链路；不扩大成“所有历史分支均不存在” |

**实现状态和使用状态分开记录**：

- “默认入口”：默认热栏／当前场景配置有该入口；仍不声称本次实际施放。
- “可选择／可编排”：当前目录和正式运行消费者接通，可在 Showcase、热栏装配或 AI Lab 制作中选择；不等于每个默认角色已经装配。
- “支持未配置”：通用运行能力存在，当前切片未找到具体启用配置。
- “数据已导出”：作者源及生成数据存在；不推断当前场景角色必然加载了它。
- “待合入”：独立 worktree 内容，不计入主目录已有能力。

## 一、空间、移动与朝向

机制回答“距离／位置／朝向如何变化与判定”。这些规则供进攻、撤离、控距等多种意图复用，不按意图目的划分类别。

| 具体机制 | 可观测判定规则 | 使用它的意图／组合 | 状态与使用 | 证据 |
| --- | --- | --- | --- | --- |
| 目标距离与动作距离摘要 | 当前战斗目标采用自身胶囊至目标 Hurtbox 的三维表面净空；指定单位／世界位置采用根节点平面距离。动作有效带、最佳点在执行前绑定／编译；摘要不能替代实际命中 | 接敌出招、守距、环绕；作者选择动作后派生距离 | 完整接通；当前作者与执行路径使用 | E01、E02 |
| 接近目标 | 大于停止距离时向目标移动，到位停止；动作最佳点来源遵循已编译距离规则 | 常规移动接敌出招、贴身逼近 | 完整接通；有共享源与卡草稿 | E01、E02、C01 |
| 保持距离 | 过远靠近、过近后退，带内停步；单值守距采用 ±0.1m 容差，下界不小于 0；移动到位条件消费相同有效带 | 守距观望；可与等待或攻击显式组合 | 完整接通；当前共享源“守距观望” | E01、E02、C01 |
| 远离／过近后撤 | 远离按目标反方向持续移动；过近后撤在距离低于阈值时移动，到阈值停。到位、目标失效或超时结束必须由段完成规则／条件指定 | 脱离目标、残血撤离、退至期望距离 | 完整接通；有源配置；不会仅凭“远离”识别危险 | E01、E02、C01 |
| 环绕目标 | 切向移动与径向修正组合，支持自动／左／右；进入距离带只意味着到位，未配置结束条件时仍可继续环绕 | 绕侧进攻；环绕后显式接敌出招 | 完整接通；有共享源与测试卡 | E01、E02、C01 |
| 面向目标 | `TurnToTarget` 写独立转向控制；面向条件读取实际 yaw 与目标方向。移动目标更换不隐式更换转向目标 | 接敌出招、守距、面向目标向前垫步 | 完整接通；底层事件及组合已有。独立新意图见待合入表 | E01、E03 |
| Root Motion／冲锋位移 | 动作 motionTrack 产出 RootMotionDelta，运动系统和确定性阻挡决定实际位移；冲锋斩用 CasterBody sweep 另行判定伤害。位移与命中是两条规则 | 冲锋斩、垫步、翻滚、Step 后撤 | 完整接通；有动作／Ability 配置 | E04、E07、C02 |
| 横向移动 | 底层切向策略仍可编排；未发现仍保留已删除三张横移卡的当前共享源 | 可手工编排横移；当前推荐内容须按真实目录选取 | 完整接通的底层移动支持；现成“无路侧切／横切脱困／攻后换位”已删除 | E01、E03、C01 |

移动支持平面引导和既有物理阻挡，不据此宣称任意环境自动寻路、危险识别或绕后背刺。横移与环绕的存在也不能证明背刺存在。

## 二、动作时序与控制权限

| 具体机制 | 可观测判定规则 | 使用它的意图／组合 | 状态与使用 | 证据 |
| --- | --- | --- | --- | --- |
| 动作起手、阶段、取消／打断 | 动作配置拥有时长、Startup／Active／Recovery、CancelRules／InterruptRules；正式动作提交经当前 owner 仲裁。控制权和动画表现分开 | 手动攻击、回避、技能；卡内调用具体动作 | 完整接通；当前动作系统使用 | E03、E05、C02 |
| 卡内动作事件与多轨采样 | 起手按进入首帧判定；同帧候选在采样后提交；Gate 保持游标但世界帧和既有动作继续；卡内替换走已有执行提交路径 | 移动＋转向＋攻击；准备后垫步 | 完整接通；执行器已有。概念规则以权威文档为准 | E03、E06 |
| 通用条件等待 | `WaitForCondition` 只控制后续游标，不把同帧事件变成串行依赖；完成规则和独立预算决定成功／失败 | 等距离、等面向、显式条件编排 | 完整接通；可编排 | E03、E06 |
| 等待进入时捕获的攻击窗口退出 | 进入时捕获目标、真实动作实例、已生效窗口 ID；只等这组窗口退出。空集合仍跨至少一个世界帧；目标失效／观察不可用／超时有不同结果。r2 PassiveWait 释放本 owner 移动／转向，不取消独立动作或重置速度 | “等待条件”意图；后续行为由卡显式组织 | 完整接通；主目录已有 r2 共享源。当前运行集成未在本次验证 | E06、C01 |
| 有界段与退出清理 | Arrived／Action／Condition 各有实际帧／动作进度预算；超时或拒绝结束执行并释放控制、清理所拥有动作；清理失败不伪装成功 | 有界接敌、退到距离、等待、动作完成后继续 | 完整接通；主目录使用 | E06 |
| 动作准入、成本与冷却 | ActionUseAuthority 复核 grounded／airborne／target／资源／Ability owner；先暂存动作，成本／冷却 owner 成功提交后才发布。查询可用不等于已经扣费 | 普通技能、正式前垫步 | 完整接通；目录含成本与冷却配置 | E05、C02 |
| 回避资源与恢复 | 体力属性 920002；Step Ability 900001 消耗 15，Roll 900002 有独立成本；上限 100、消耗后延迟 45f、每秒恢复 25 来自 tuning。方向／连携由回避 owner 保存 | 手动垫步／翻滚；面向后前垫步走正式 Step 成本 | 完整接通；当前回避内容配置 | E07、C02、C04 |
| 面向后前垫步 | 转向＋等待朝向＋固定功能动作 950008；默认 ±5°、准备上限 30f。仅此段启用准备超时继续按当前朝向垫步；动作成功由正式回执确认；完成预算按实际动作进度 | `tactical.shared.facing-forward-step` | 完整接通；当前共享源 r2；作者指南明确未完成实际战斗转向／扣费／位移验收 | E03、E05、E07、C01 |
| Step 后撤 | 固定功能槽绑定 950003，Root Motion 后撤；不隐式加转向、Gate 或基础移动 | 后撤、撤步还击 | 完整接通；已有共享源／动作配置；不可仅凭名称推断另加转向 | E03、C02 |

动作槽与装配单列：槽位是流程中的动作占位；装配把具体动作绑定到槽，编译才可派生动作距离／时长。未绑定模板可以结构合法，但不能标成已经可执行的卡。`CharacterActionSlotType` 的输入打断掩码也不是战术模板动作槽。以上不新增卡牌实例或卡组身份保证。

## 三、命中与伤害结算

| 具体机制 | 可观测判定规则 | 使用它的意图／组合 | 状态与使用 | 证据 |
| --- | --- | --- | --- | --- |
| 武器／动作命中窗口 | HitTraceWindow 生命周期与 baked weapon trace、手工体积、CasterBody／baked socket 体积生成候选；窗口结束停止 trace | 近战攻击、冲锋斩等动作 | 完整接通；当前实际 trace consumer | E04、E08 |
| 目标关系、环境与命中可用性 | self／friendly／dead／invincible／parry／block 等进入 HitResolve；配置指定 targetTypes。墙／地面分类缺少正式 subtype 时当前系统使用 Unknown fail-closed，不能冒充精确墙地识别 | 近战和远程选敌；环境阻挡表现 | 完整接通；当前切片使用，精确 WorldWall／WorldGround 分类有明确限制 | E08、E09 |
| 同事件重复命中与穿透预算 | `OncePerEvent`／AllowRepeat／MaxAcceptedHits 由策略事件身份计数，不把每个物理 TraceId 当独立配额；Duplicate 不重复扣血 | 单次打击、多段／穿透弹体 | 完整接通；当前直接命中策略与弹体配置 | E08、C03 |
| 统一伤害与防御减伤 | 当前软防御公式：offense=max(0,base+属性贡献)，mitigation=0.6×defense/(defense+50)，raw=offense×(1-mitigation)×externalMultiplier，再按实现取整。伤害类型／标签作为 payload 存在不证明各自特殊抗性规则 | 武器、范围、弹体、毒伤；光环增防和破防 Buff 改同一防御属性 | 完整接通；当前 DamageResolver／Applier 使用 | E10、E11 |
| 命中附加 Buff | OnHitBuffs 仅携带 BuffId；Damage-kind 命中可施加，即便 HP 伤害为 0；Blocked／Invincible／Parried 等不施加 | 火球破防、Beam Lance 凝滞、Stun Bolt 眩晕 | 完整接通；当前配置与桥接有生产／消费 | E12、C03 |
| 部位判定、伤口开放增伤、结构破坏 | 真实几何证据映射部位；当前命中结算按帧初状态读取增伤；结算 accepted damage 累积 wound／衰减 integrity；达到容量开放，integrity 到 0 Broken，不把开放后的状态回写影响同帧前置判定 | 定点打击、攻击已开放部位；非背刺、非流血 DOT | 完整接通；000003 作者源与生成 geometry_binding 已有数据，未确认当前场景已加载该角色 | E13、C05 |
| 伤口消耗／断肢 | 已有 StageAcceptedFocusConsume／StageAcceptedSever、帧初 Open／Broken、tag 和身份校验，Severed 能被 Hurtbox／动作依赖消费者读取 | 将来对开放部位消耗收益、断尾等明确玩法 producer | 部分基础；有数据／状态／消费者，未找到上述 Stage 入口的非测试调用 | E13、C05 |
| 背刺 | 应能找到“相对目标背后”的生产判定及伤害／动作消费者；本次未找到这种判定或额外结算 | 绕侧／面向事件只提供组合素材，不形成已实现背刺 | 未找到；旧枚举审计仅作历史线索 | E18 |

普通 accepted damage 并不自动意味着打断动作；伤害、散势压力、破聚、特殊击飞和动作反应必须区分。伤口 Open 名称也不代表流血。

## 四、生存与防护

| 具体机制 | 可观测判定规则 | 使用它的意图／组合 | 状态与使用 | 证据 |
| --- | --- | --- | --- | --- |
| HP、死亡与战斗权限 | 伤害输出 Gameplay attribute command；HP 到 0 进入死亡转换；BattleState 决定能否被锁定／受伤／行动，反应经角色动作 owner 处理 | 击杀、失去战斗能力、残血撤离条件 | 完整接通；当前切片 | E11、E14 |
| 复活命令 | 现有 RuntimeCommand consumer 调 TryReviveCharacter，检查状态后恢复 HP 与复活动作／状态；缺省 HP 为上限的 30%（向上取整且至少 1） | 显式命令／验证操作 | 完整接通的命令支持；未据此声称自动复活卡或治疗技能已装配 | E14 |
| 回避窗口 Unhittable | Alive 且 Unhittable 时先生成 Evaded，早于 hit-once 消费；无伤害／硬直／击退；目标仍物理存在并可锁定，同一攻击窗口在回避结束后仍可命中 | 垫步、翻滚及引用这些动作的意图 | 完整接通；16 个公共动作实际 mask=32 | E08、E09、C02 |
| 无敌 Invincible | 状态合并后 HitResolve 返回 Invincible，DamageApplier 不输出 HP 伤害。它属于重叠命中的免疫分类，与不成立命中的 Evaded 不同 | 可供保护动作配置；不声称当前已启用 | 部分基础；状态生产／存储／消费／退出通道已有，当前核查配置未设置 mask=2 | E08、E09、E11 |
| 格挡／招架／霸体标志 | 通用 HitResolve 分别返回 Blocked／Parried，SuperArmor 将 candidate staggerFrames 归零；不自动等于全局免伤或项目所有反应免疫 | 可配置防守／招架动作 | 部分基础；通用分支及 target-state 通道支持，但当前角色动作目录未找到 mask=8／4／16 内容。静态环境 Blocking 不能算角色防守动作启用 | E08、E09 |
| 散势压力、破聚、破绽与恢复 | accepted／blocked 输入产生 ScatterImpact；当前凝聚限来自动作支持；压力达到限制形成 CohesionBreak，owner 提交破聚反应；恢复按现有延迟／破聚窗口规则推进。破聚会撤销角色 Blocking 标志 | 压制、破绽攻击、打断；底层受击反应 | 完整接通；CharacterTest 使用此路径，不接独立 GuardPressure | E11、E15 |
| 击退／击飞及落地恢复 | ImpactPower 经双方质量、部位 impulse scale 等派生冲量；launchPower 有易感性与破聚前提检查，再进入特殊状态／角色运动；不是每个受伤事件都击飞 | 重击、爆发、控制追击 | 完整接通；DamageApplier 与 motion／special-hit 路径 | E11、E14 |
| 物理防护盾 | 朝向／局部偏移绑定独立 Combat collider，遮挡 weapon／projectile，不阻挡角色移动；三势压力跨阈值破盾后移除 collider。默认寿命 300f，恢复延迟 45f，破盾不自然恢复 | Physical Shield，防住近战／弹体 | 完整接通；Ability 910019、动作 320，可选择；不在默认九格热栏 | E16、C02 |
| 通用护甲耐久／GuardPressure | 通用 bridge 支持按 integrity 吸收并衰减护甲，或独立 guard pressure；但 CharacterTest 构造 DamageApplication 时 guardPressureSystem=null，未找到其角色创建 ArmorIntegrity component | 框架其他消费者可接入 | 部分基础（就当前切片而言）；有框架实现／测试，不计入切片已启用机制 | E11、E17 |

## 五、持续状态与属性修饰

以下秒数按切片 60Hz 换算。当前六种 Buff 均为 maxStack=1、applyStack=1、RefreshDuration、非永久；**Buff 刷新保持原 StartFrame，更新 EndFrame**，因此毒伤时钟不会因刷新重置。不同来源刷新同一目标同一 Buff，不按来源保存多份实例。

| 具体机制 | 可观测判定规则／退出 | 使用它的意图／组合 | 状态与使用 | 证据 |
| --- | --- | --- | --- | --- |
| 防御光环 | Ability 建立跟随施法者的球形 aura；范围内刷新 601008，防御属性 3 加 5；4m、source 300f，Buff 2f，离圈无刷新后到期移除 modifier | Guard Aura，靠近队友的保护行为可显式组合 | 完整接通；默认热栏第 8 格 | E12、C02、C06 |
| 命中破防 | 火球／火球连发配置 OnHitBuff=601009；防御 -3，持续 300f；到期清理并撤回修饰。不是破聚／破盾 | 破防后接续伤害 | 完整接通；默认火球入口与具体配置 | E12、C03、C06 |
| 禁足 Rooted | 禁足区域周期刷新 601010／status 901010；2f Buff 投影为主动移动锁定，外力仍允许；区域有 60f 前置、180f 存在、2.5m 半径，取消／退出／到期都有处理 | Area Confinement，范围控制 | 完整接通；Ability 910013、动作 314，可选择；默认热栏未装配 | E12、E19、C02、C04、C06 |
| 凝滞 Stasis | Beam Lance 的 OnHitBuff=601011 → 901011，持续 90f；控制缩放为 0，动作冻结／新动作拒绝；表现可暂停动画；到期投影清除后恢复原控制规则 | 命中后阻止目标行动 | 完整接通；Ability 910014、动作 315，可选择 | E12、E19、C03、C06 |
| 眩晕 Stunned | Stun Bolt 为零 direct HP damage 的 Damage-kind 命中，附 601013 → 901013，持续 90f；控制／动作消费者限制行动；到期解除。与凝滞动画暂停语义区分 | 纯控制命中、为后续动作创造时间 | 完整接通；Ability 910020、动作 321，可选择 | E12、E19、C02、C03、C06 |
| 中毒 Poisoned | 毒池每 30f 刷新 601012，Buff 360f；从原 StartFrame 起每 60f 生成基础 2 点伤害 payload，经软防御／共同应用。EndFrame <= 当前帧时不再 tick；source 到期停止刷新，Buff 到期清理 | 毒瓶区域消耗；区域维持状态 | 完整接通；毒瓶可选，起始区域另有实际配置 | E20、C03、C06、C07 |
| 麻痹 Paralyzed | 901014 已有状态读取、动作冻结拒绝及表现优先级；未找到 Buff 定义／生产技能／施加和到期配置 | 当前不能列成可选完整控制技能 | 部分基础；只有消费与命名基础 | E19、C06 |
| 流血 Bleed | 未找到施加入口、专用 Buff／状态、周期伤害生产或解除规则。开放伤口状态没有定时扣血规则 | 世界观中的伤害描述不算现有 DOT | 未找到当前实现；世界观层仅设计／概念提及 | E18 |

## 六、远程投放、持续区域与物理场

这些是“如何把规则送达目标／留在世界”的机制。火球等招式名是机制消费者，不能把每种 VFX 都当新机制。

| 具体机制 | 可观测判定规则／结束 | 当前消费者／组合 | 状态与使用 | 证据 |
| --- | --- | --- | --- | --- |
| 弹体运动、碰撞与生命周期 | 标准 runtime 保存 active projectile，按 motion／collision／impact 配置推进，输出 hit／expiry；伤害仍走共同管线。现有 23 条定义覆盖 Linear、ArcToTarget、BallisticArcToTarget、BoomerangReturn、SwayForward、HomingToTarget、BezierToTarget | 各类火球、投掷、追踪／回旋、Beam／Channel／Laser | 完整接通；当前目录与消费者；默认热栏含多种火球及 Tornado | E21、C02、C03 |
| 连发／持续发射／延迟范围 | 动作事件与 Ability effect timeline 按明确帧触发多次请求／范围 pulse；每次仍使用标准弹体或范围结算；不是 VFX 自行扣血 | Fireball Burst、Channel Beam、Meteor Barrage、Fireball Explosion | 完整接通；有对应动作／Ability 和 effect timelines；部分可选 | E21、C02 |
| 同帧连锁目标 | Chain Lightning 确定性解析首目标和后续目标，同帧产生命中；不是飞行弹体 | 连锁闪电 | 完整接通；Ability 910017、动作 318，可选择 | E22、C02 |
| 弹体转毒池 | 毒瓶 hostile impact／expiry 由项目桥接计算落点并创建固定区域；池半径 2.5m、寿命 180f、30f 刷 Buff；区域结束与中毒结束独立 | 毒瓶，投掷＋区域持续状态 | 完整接通；Ability 910018、动作 319，可选择 | E20、C02、C03 |
| 钩链牵引 | 首次 accepted hostile projectile hit 创建有界 pull 状态；逐帧向目标提交 ExternalForceRaw，向施法者前方 anchor 拉拽，墙体／运动结算仍有效 | Hook Chain | 完整接通；Ability 910024、动作 326，可选择 | E21、C02 |
| 落地部署地雷、触发与引信 | carrier 命中 hostile 先转 drop；静态 impact／expiry 才部署。1.25m hostile 触发只启动 60f 引信，结束后 2.25m blast，最多 8 目标；存在 180f | Arc Mine，预置区域威胁 | 完整接通；Ability 910025、动作 328，可选择 | E23、C03、C04 |
| 持续炮台自动索敌发射 | 部署后 18f 激活，360f 存在；每 12f 搜索 10m 最近 hostile，45f 发射间隔、最多 8 发；shot 继续走标准 projectile／hit | Rune Turret | 完整接通；Ability 910026、动作 329，可选择 | E23、C03、C04 |
| 吸引、方向推力、爆炸冲量 | projectile physical events 是事实输入；CharacterTest 根据已映射 stableId 生成 AttractionAcceleration／DirectionalImpulse／RadialImpulse，统一物理交互系统处理角色及可动物体。通用开关不自带所有力场语义 | Tornado 吸引、Sword Wave 推力、火球爆炸／陨石冲量 | 完整接通；当前 adapter／consumer 与相应技能 | E24、C03 |

材料燃烧／压碎／开裂另有 `CharacterTestGenesisPotencyMechanicsHarness` 基准：火焰／重锤改变材料三势后跨阈值输出现象；[现行材料转变工程合同](../CHARACTER_TEST_GENESIS_VALIDATION_SCENE_FRAMEWORK_DESIGN.md#材料转变与后续接触)区分固定 harness 样例与实体权威状态。它不证明角色已有 Burning DOT、流血、中毒以外的持续伤害，也不证明当前植被任务已完成战斗接入。本文未审计或修改植被任务。

## 四个指定机制的链路追踪

| 机制 | 生产者 | 状态／承载 | 消费／结算 | 退出／解除 | 判定 |
| --- | --- | --- | --- | --- | --- |
| 背刺 | 当前范围内未找到背后角度、位置或动作规则生产者 | 未找到专用状态；通用 yaw、距离、伤害标签不能代替 | 未找到背刺倍率／特殊命中分支 | 未找到 | 未找到；旧 BackStab enum 审计不算实现 |
| 无敌 Invincible | CharacterAction SetHitTargetState 可生产，但核查动作 JSON 只有 mask=32 的内容，没有 Invincible 内容 | `HitTargetStateContributionStore` 按 entity＋action instance＋stable event 保存并 OR 合并 | EntityLookup 合并后进入 `HitResolveSystem.ResolveSingle`；Invincible 结果不扣普通 HP | Enter／Active upsert；BeforeFrame Exit 与终止后 Exit remove，实体缺失 ClearEntity | 部分基础／支持未配置。不能据此宣称毒伤也免疫 |
| 流血 | 未找到 | 六条现有 Buff 中无 Bleed；BodyPart Open 不是 Bleed | 未找到 Bleed tick | 未找到 | 未找到当前实现；世界观只有概念 |
| 中毒 | 毒瓶 impact／expiry → pool；另有 region.start 的 apply trigger | source runtime 保存区域；BuffSet 保存 601012、StartFrame、EndFrame、stack/source；projection 写 901012 | ProcessPoisonTicks → TryResolveStatusTickDamage → Damage-kind result → DamageApplier／CombatDamageApplication／attribute command | source 到期移除；Buff cleanup＋projection 撤回状态；区域 Exited 显式 remove | 完整接通；已有配置入口 |

中毒的具体边界：

1. 360f 是每次刷新后的寿命；刷新保持原 StartFrame。若仅施加一次，代码在 EndFrame 当帧拒绝 tick，不能按“6 秒”文案机械断言必有 6 次伤害。
2. 2 是基础 damage，不保证最终 HP 恰扣 2；当前软防御公式和其他共同结算仍参与。
3. `AddPoisonTickResult` 当前直接构造 `HitResolveKind.Damage`，没有重新走 `HitResolveSystem` 的 Invincible／Parry／Unhittable 分类；不能宣称这些命中防护必然屏蔽 DOT。
4. tick 当前构造的 source entity 为 0／CombatEntityId.None；Buff／池的来源记录与 tick 攻击者归因不能混为一谈。
5. 毒池消失不会立即 remove 仍有剩余寿命的中毒；起始区域 Exited remove 则是另一种作者配置规则。

## 机制类别与意图类别是两个维度

机制类别按**规则处理的对象**归类：空间、时间／控制、命中／伤害、防护、状态、投放／物理场。意图类别按**行为想达成的目的**归类，可用作阅读标签：接敌／进攻、控距／换位、撤离／保全、等待条件、控制／压制、防守／支援。这些标签不是本次新增代码枚举或新的设计层级。

关联是多对多：

| 意图／组合例子 | 借用的机制 | 当前证据边界 |
| --- | --- | --- |
| 常规移动接敌出招 | 表面净空、移动、面向、条件 Gate、装配动作、真实 trace／伤害 | 当前源与执行路径已有；不保证每次命中 |
| 绕侧进攻 | 环绕／径向修正 → 接敌出招 | 当前共享源已有；没有背刺倍率 |
| 撤步还击 | Step 后撤 → 接敌／攻击 | 当前源已有；后撤不自动代表面向或无敌 |
| 等待条件 → 出招 | 捕获现有攻击窗口退出 → 后续显式动作 | 等待 r2 已有；不推断目标未来动作、不证明安全反击 |
| 面向目标向前垫步 | yaw 条件／准备预算 → 正式 Step 成本 → Root Motion＋回避窗口 | 当前共享源已有；实战验收仍需另取证据 |
| 区域控制／持续消耗 | 禁足／毒池／附加状态／投放 | 技能实现已接通；若要成为战术模板／卡，仍须找到显式作者编排，不能从技能存在反推已有同名意图 |

术语继续遵循：**事件 → 意图 → 意图组合（可选）→ 战术模板 → 战术卡 → 卡牌实例 → 卡组**。动作、动作槽、装配、预设单列。现有 `tactical-shared` 混合承载可复用源，按内容辨认意图／组合；工厂名 Templates、验证 group entry、冻结 execution state 均不能自动视为完整模板／持有实例／卡组新身份已经迁移完成。

## 当前内容启用证据

| 编号 | 具体配置／入口 | 已核查事实与限制 |
| --- | --- | --- |
| C01 | `Authoring/RuntimeAiLab/Drafts/latest-workspace.txt` → `e56af3d4-374b-4ae4-b005-620a5cc8b6dc`；该目录 tactical-shared／cards；`Authoring/RuntimeAiLab/TestCards/` | 主体采集时最新工作区 14 份共享源含守距／绕侧／接敌／撤离／后撤至距离／面向前垫步／等待条件 r2 等；15 份卡草稿。存在源／草稿不等于已经发布、装配或持有实例；旧工作区和旧 published revision 不充当当前启用证明 |
| C02 | `Assets/MxFrameworkGenerated/Abilities/ability_catalog.json`；`Assets/MxFrameworkGenerated/CharacterActions/Libraries/**/character_actions.json`；`CharacterActionSource/Libraries/Common/character_actions.json` | Ability 目录 28 条；公共回避动作 SetHitTargetState 共 16 条，均 mask=32：Step 7..19f，Roll 8..27f（配置闭区间，按动作时间推进；变速／冻结不等于固定墙钟时长） |
| C03 | `Assets/MxFrameworkGenerated/Combat/Projectiles/projectile_definitions.json` | 23 条定义；Fireball／Burst 附 601009，Beam Lance 附 601011，Stun Bolt 附 601013；毒瓶自身不是直接 on-hit Poisoned，后续池负责施加 |
| C04 | `Assets/MxFrameworkGenerated/Abilities/character_test_mechanic_tunings.json` | 回避恢复、禁足前置／范围／持续、地雷引信／blast、炮台 warmup／扫描／发射预算等可见具体值 |
| C05 | `Authoring/CharacterPackages/character-000003/geometry/body_part_damage.json`；`Assets/MxFrameworkGenerated/CharacterPackages/000003/config/geometry_binding.json` | 头／双翼／尾部伤口与 integrity 数据已导出；尾部允许 sever、requiredTag=`action.000003.tail.sever`；仅数据不证明 sever producer 已调用或场景已装配该角色 |
| C06 | `Assets/MxFrameworkGenerated/Gameplay/Buffs/buff_definitions.json`／`.bytes` | 六条：601008 增防、601009 减防、601010 禁足、601011 凝滞、601013 眩晕、601012 中毒。按当前 importer 格式只读解码 bytes：6 条、609 字节全消费，字段与上述 JSON 一致；未以正式 Unity loader 执行 |
| C07 | `Assets/Config/MxFramework/Levels/CharacterTest/character_test_scene.world_objects.json`；`Assets/Scenes/CharacterTestScene.unity` | `region.start`／`trigger.region.start.1`：Entered／Stayed apply 601012，Exited remove，归因 501012。JSON 及 scene 序列化均有配置；未运行区域进出 |

默认热栏以 `CharacterTestAbilityHotbarRuntime.CreateDefaultAbilityIds` 为准：Fireball、Fireball Burst、Flame Lance、Fireball Lob、Fireball Surge、Fireball Return、Tornado、Guard Aura、Fireball Explosion。Poison Bottle、Physical Shield、控制弹、地雷、炮台等在目录和 Showcase 选择入口存在，但不是默认九格内容。保存的热栏可能覆盖默认，本次未读取个人存档，不能断言用户当前按键装配。

已比对下列 generated 与 `Assets/StreamingAssets/MxFramework/CharacterTest/RuntimeCatalogs/` 镜像 SHA-256 相等：Buff JSON／bytes、Projectile JSON／bytes、Ability JSON／bytes、mechanic tuning JSON。未解析 Projectile／Ability bytes 的全部内容；镜像一致不替代正式 loader、源与导出一致性及运行验证。

## 待合入与仅设计边界

| 项目 | 证据 | 本文处理 |
| --- | --- | --- |
| 独立“面向目标”意图 | `E:/Unity/IsolatedWorktrees/face-target-20261002`，HEAD `609044e28b82203f59c2a7b53df13f96a902faca`，未提交 diff。Visual 模板新增 `FacingTargetDefinitionId=tactical.shared.facing-target`／CreateFacingTarget；编译与执行增加 Facing 完成规则，默认阈值 5°／最大 30f | 主体核查时待合入；最终观察主工作树有未提交同步，见并发补记。只读检查 diff，不验证完成度／发布／运行，也不计入本次主表已核查机制 |
| 世界观的流血、烧灼、麻痹等 | `StorySource/Projects/WGame/Canon/living_being_foundation.md` 的伤害描述 | 仅设计／概念语境；麻痹另有当前消费基础，流血无当前闭环 |
| 三势材料现象 harness | 现行 Genesis 材料转变工程合同及 harness | 已有基准实现，不扩大成角色状态或当前植被 Playable |
| 当前植被任务 | 主目录核查开始已有 `Assets/__VegetationFormalAcceptance/**` 删除状态 | 属于其他任务，本次不恢复／编辑，不当作本次机制缺失或新增 |

### 并发补记（2026-10-01 16:33 UTC）

文档写入后的只读 status／diff 复核发现，其他任务已将 face-target 的 10 个源码／测试文件改动同步至 main 工作树，HEAD 仍为上文提交；并新增共享源：

`Authoring/RuntimeAiLab/Drafts/e56af3d4-374b-4ae4-b005-620a5cc8b6dc/tactical-shared/4a76d1fe626452f3412afbbe518e89b06a1584842e0ebfe9d0ac8e9729168373.json`，definitionId=`tactical.shared.facing-target`、revision=1、name=“面向目标”，文件时间为 16:30:58 UTC。workspace.json 亦有同期修改。

因此“主目录没有独立面向意图源码”只适用于主体采集时刻，不能当作最终工作树事实；最终事实是**主工作树出现未提交增量**。该增量保持单列，不冒称已经合入提交、发布或完成本次运行验收；C01 的 14 份共享源计数是采集快照，最终观察另增上述 1 份。本次仅修改本文，没有写入上述并发文件。

## 证据文件与关键符号

以下均相对仓库根。行号只作为本次定位，后续移动以符号为准；P=`Assets/Scripts/WGameFrameworkProject/CharacterTest/`，F=`Packages/com.vincent.wgameframework/`。

| 编号 | 文件与关键符号 |
| --- | --- |
| E01 | P `RuntimeAiPlannerDev/CharacterTestTacticalMovement.cs`：Compile、distanceTolerance（约 42～82）；`CharacterTestBehaviorExecutionEvaluator.MovementConditions.cs`：EvaluateExecutionPredicate；`CharacterTestRuntimeAiLabMovementResolver.cs`：Resolve／ResolveOrbit。新编译规则与兼容移动策略分开阅读 |
| E02 | P `Runtime/Systems/Combat/CharacterActionRangeProber.cs`、`CharacterActionTacticalMetricsStore.cs`；作者指南“基础移动能力”与动作距离修订 |
| E03 | P `RuntimeAiPlannerDev/CharacterTestTacticalAuthoringTemplates.Visual.cs`：CreateFacingForwardStep、CreateRetreatToDistance、CreateVisualAttack、CreateVisualCard；`CharacterTestTacticalAuthoringTemplates.cs`；F `CharacterAction/CharacterActionRunnerTypes.cs`、`CharacterActionTracks.cs` |
| E04 | P `Runtime/Systems/Character/CharacterTestCharacterRootMotionSystem.cs`、`CharacterTestCharacterMotionSystem.cs`；`Runtime/Systems/Combat/CharacterTestCharacterCombatTraceSystem.cs`：ProcessActiveHitVolumes；baked trace provider |
| E05 | P `Runtime/Systems/Character/CharacterTestActionUseAuthority.cs`：Evaluate（55）、TryCommit（127）、TryStageOrchestration；`CharacterTestAbilityCostAuthority.cs`；`Combat/CharacterTestCharacterActionRuntime.OrchestrationCommit.cs` |
| E06 | P `RuntimeAiPlannerDev/CharacterTestBehaviorExecutionEvaluator.cs`：Sample、ReleaseControls；`CharacterTestBehaviorExecutionEvaluator.Blocks.cs`：UpdateExecutionBlocks、CompleteExecutionBlock、FailExecutionBlock、CleanupBudgetExpired；`CharacterTestBehaviorExecutionEvaluator.AttackWindow.cs`：UpdateAttackWaitBlock；`CharacterTestTacticalAuthoringTemplates.WaitOpportunity.cs`：CreateWaitOpportunityDefinition |
| E07 | P `Runtime/Systems/Character/CharacterTestCombatEvadeRuntime.cs`：StaminaAttributeId／StepAbilityId／ForwardStepActionId、CharacterTestCombatEvadeTuning；`CharacterTestCharacterActionSystem.cs` 的正式回避入口；C02／C04 |
| E08 | P `Runtime/Systems/Combat/CharacterTestHitTraceWindowState.cs`：ProcessStartCombatRequests（1126）、ProcessStopCombatRequestsAndClear（1170）、RemoveEndedTraces（1205）、TargetStateContributions；`CharacterTestHitResolutionPolicy.cs`：CharacterTestHitResolutionEvaluator（984）、CharacterTestDirectHitResolutionService；`CharacterTestCombatEntityLookup.cs`：ResolveTargetState（66） |
| E09 | F [HitResolveSystem.cs](../../Packages/com.vincent.wgameframework/Combat/Hit/HitResolveSystem.cs)：Resolve／ResolveSingle；`Combat/Hit/HitTargetStateContributionStore.cs`：Upsert／Remove／ResolveFlags；`HitTargetStateFlags.cs`；旧 CombatActionRunner／ActionStateToHitTargetAdapter 仅作框架支持证据，不冒充当前 CharacterTest 动作生产者 |
| E10 | P `Runtime/Systems/Combat/CharacterTestCombatDamageResolver.cs`：ResolveCommonDamage、ResolveSoftDefenseTrace（1224）、TryResolveStatusTickDamage（788）；P `Combat/CharacterTestAbilityCatalogSource.cs` |
| E11 | P `Runtime/Systems/Combat/CharacterTestCombatDamageApplier.cs`：Apply、构造 CombatDamageApplicationSystem（264）、ApplyDeathTransitions、ApplyCohesionBreakReactions、ApplyLaunchSpecialHitReactions；F `Combat/GameplayBridge/CombatDamageApplicationSystem.cs`；P `CharacterTestCharacterCombatTraceSystem.cs` Tick（946～1092） |
| E12 | P `Combat/CharacterTestGuardAuraRuntime.cs`：CreateBuffDefinitions（317）、CreateAuraDefinitions、ApplyOnHitBuffs、Gameplay 管线（265～301）；F `Combat/GameplayBridge/CombatOnHitBuffApplicationSystem.cs`；`CombatGameplayAuraBuffSystem.cs` |
| E13 | P `Runtime/Systems/Combat/CharacterTestBodyPartDamageAuthorityBridge.cs`：BeginAuthorityFrame、StageAcceptedFocusConsume（595）、StageAcceptedSever（677）；P `Runtime/World/CharacterTestBodyPartStateComponent.cs`：WoundState／StructureState、damage transition（1191 起）、Severed 过滤；`CharacterTestDamageZoneSelector.cs` |
| E14 | P `Runtime/World/CharacterTestComponentWorld.cs`：BattleState／BattleStateSystem；P `Runtime/Systems/Character/CharacterTestCharacterRuntimeModule.cs`：TryReviveCharacter（842）、ResolveReviveHealth（977）；P `Runtime/Commands/CharacterTestRuntimeCommandModule.cs`：复活 consumer（634）；P DamageApplier 特殊击飞／死亡入口 |
| E15 | F `Gameplay/GameplayScatterPressureSystem.cs`：Tick／Recover／CohesionBreakEvents；`GameplayScatterPressureComponent.cs`；F `Combat/GameplayBridge/CombatCohesionLimitAdapter.cs`；P DamageApplier：EnqueueScatterPressureRequests／EnqueueCohesionLimitRequests |
| E16 | P `Combat/CharacterTestPhysicalShieldRuntime.cs`：LifetimeFrames（103）、Tick（387）、RemoveActiveAt（439）；F `Combat/Shields/CombatShieldRuntime.cs`／`CombatShieldOcclusionFilter.cs`；P `CharacterTestCharacterCombatTraceSystem.RuntimeEffects.cs`：ProcessPhysicalShieldRequest |
| E17 | F `Combat/GameplayBridge/CombatDamageApplicationSystem.cs`：ArmorIntegrity 吸收；`CombatGuardArmorApplicationSystem.cs`；`Gameplay/GameplayGuardPressureSystem.cs`。就 CharacterTest 的接入证据见 E11 的 null 参数与未找到 component producer |
| E18 | 搜索范围：当前 Assets/Scripts/WGameFrameworkProject、F 的 AI／Combat／CharacterAction／CharacterControl／Gameplay、Assets/Config、generated actions／Buff／projectile／ability、Authoring、CharacterActionSource、Docs／StorySource；搜索 BackStab／backstab／back_stab／背刺／Bleed／Bleeding／流血，并复核 E08～E13 和 C02／C03／C06。排除 MonoBleedingEdge、颜色 bleeding、VFX 分类等同词异义 |
| E19 | P `Runtime/Systems/Character/CharacterTestMechanicMotionEffectBridge.cs`：CharacterTestControlPresentationResolver、CharacterTestStatusControlEffectBridge、CreateDefault motion policies（401～424）；P `CharacterTestCharacterRuntimeModule.cs`：status bridge Tick（1602）；P `Combat/CharacterTestGuardAuraRuntime.cs`：AreaConfinementRuntime／CancelPendingForDamageEvents |
| E20 | P `Combat/CharacterTestGuardAuraRuntime.cs`：CharacterTestPoisonIds（91～106）、CharacterTestPoisonBottlePoolRuntime（1547）、CreateMechanicSourceDefinitions（1646）；P `Combat/CharacterTestMechanicSourceRuntime.cs`：Tick、Buff cleanup／projection；P `Runtime/Systems/Combat/CharacterTestCharacterCombatTraceSystem.Projectiles.cs`：CreatePoisonBottlePoolFromProjectileImpact（765）／Expiration（802）、ProcessPoisonTicks（1501）、AddPoisonTickResult（1530）、ShouldApplyPoisonTick（1590）；F `Gameplay/GameplayBuffSystems.cs`：RefreshEntry（372）、EffectProjection；`GameplayComponentBuffCleanupSystem.cs` |
| E21 | P `Runtime/Systems/Projectiles/CharacterTestFireballProjectileAdapter.cs`；P CombatTrace `.Projectiles.cs`、`.AbilityTimelines.cs`、`.GameplayRequests.cs`；F `Combat/Projectiles/` 运行与配置；P `Showcase/CharacterTestSkillShowcaseBootstrap.cs`：PriorityAbilityIds、SelectAbility |
| E22 | P `Runtime/Systems/Combat/CharacterTestCharacterCombatTraceSystem.ChainLightning.cs`：ProcessChainLightningRequests；C02 的对应 Ability／动作请求 |
| E23 | P `Runtime/Systems/Combat/CharacterTestArcMineRuntime.cs`／`CharacterTestRuneTurretRuntime.cs` 及 state；CombatTrace `.ArcMine.cs`／`.RuneTurret.cs`；P `Combat/CharacterTestArcMineTuning.cs`／`CharacterTestRuneTurretTuning.cs` |
| E24 | P `Runtime/Systems/Character/CharacterTestInteractionFieldProfiles.cs`；`CharacterTestInteractionPhysicsSystem.cs`；P CombatTrace `.Projectiles.cs` physical-event adapter；`CharacterTestInteractionEvent.cs` |
| E25 | P `UI/Runtime/CharacterTestAbilityHotbarRuntime.cs`：CreateDefaultAbilityIds／AssignSlot／LoadFromFile；P `Combat/CharacterTestGuardAuraRuntime.cs`：LoadBinaryThenJsonOrEmpty（324）；F `Gameplay/GameplayBuffDefinitionBinaryImporter.cs`、`Runtime/RuntimeCatalogBinary.cs`：只读二进制解释依据 |

## 未验证边界与维护约定

- 本次只证明当前源码及配置链路；不宣称编译、Console、实际操作、视觉、命中概率、性能、网络或存档恢复通过。测试源码存在只用于定位，不报作本次已通过测试。
- 当前工作树并发用于其他任务；已记录核查 HEAD 和原有植被删除状态，不作清理。只新增本文，无提交／推送。
- 未读取个人存档、热栏持有记录、Editor 当前运行状态；配置存在不代表用户当前已装配／启用全部技能。
- Main 动作目录、Buff JSON 和 Buff bytes 已核查；其他二进制 catalog 未全面解码，未调用正式 loader／source re-export。非空 generated catalog 优先于 embedded fallback，不能拿 fallback 值覆盖有效导出内容。
- 等待窗口、Root Motion、方向、状态退出、盾遮挡、部位链路均需以后按具体目标单独做运行验证；本次不占用植被任务的 Editor。
- 主目录合入、作者目录变更、动作掩码新增或具体 producer 接入后，应更新相应条目及时间／HEAD；优先改变“状态与使用”列，不能靠改名字把基础能力升格为完整机制。
