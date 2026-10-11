# Mx 关卡编辑器 P3.2：目标筛选创作设计

> Status: Current
>
> Implementation state: Implemented; P3.2a and P3.2b complete
>
> Last reviewed: 2026-07-28
>
> Phase: Level Editor P3.2
>
> Parent: [P3.1 目标语义基础设计](LEVEL_EDITOR_TARGET_SEMANTICS_DESIGN.md)

## 1. 设计结论

P3.2 将 P3.1 的只读目标语义变成第一批可保存、可编译、运行时真实生效的筛选规则，但只开放当前项目能够完整执行的选项。

第一版聚焦 CharacterTest 的 Combat 区域 Trigger：

- `目标来源`继续由 Trigger 模板决定，不开放 Owner、Subject、Instigator 或显式绑定之间的任意切换。
- 区域 Buff 的`作用对象`仍是进入区域的 Subject，但阵营关系可以编辑。
- Story 的动作目标仍是当前区域；谁可以触发 Story 作为`触发对象`规则编辑，不能伪装成 Story 的作用目标。
- 基础身份、必要能力和存活要求进入正式筛选契约；只有出现两个以上真实可用选项时才显示选择器。
- Tag、Status、Self 和世界对象目标等当前没有完整目录或运行时绑定的能力不制造占位控件。

P3.2 不修改 `GameplayTriggerTargetSelector` 的含义，也不把 Buff `效果归因`、Runtime `命令来源`或 Unity GameObject 当作阵营关系参照。

## 2. 当前事实与缺口

| 事实 | 当前证据 | P3.2 结论 |
|---|---|---|
| Combat Sensor 把空间命中映射为 Gameplay Entity 后，将全部有效 Subject 提交给 Trigger | `GameplayTriggerCombatSensorSystem` 当前只检查映射、generation、Component World 存活和容量 | 筛选必须发生在提交完整 occupancy 之前 |
| P3.1 已有身份、能力、生命周期、关系、Tag 和 Status 查询语义 | `GameplayComponentTargetQuery`、`GameplayComponentTargetingService` | 复用语义与拒绝原因，不建立关卡专用目标枚举 |
| 现有 targeting service 返回列表快照，属于 `AllocByDesign` | `GameplayComponentTargetingService.Select` | Sensor Tick 不能直接调用；需要复用同一判定核心的 NoAlloc evaluator |
| CharacterTest 当前只把角色注册到 Combat ↔ Gameplay 映射 | `CharacterTestCharacterRuntimeWorld` 是当前唯一正式注册入口 | 当前基础身份只有 `Character`，不显示无效身份下拉框 |
| 当前区域 Trigger Binding 没有 Gameplay Owner | CharacterTest 固定模板创建的 binding owner 为空 | `Self`没有有效参照；SameTeam/Enemy/Neutral 必须使用显式参照队伍 |
| 场景角色文档已有正整数 TeamId | `CharacterTestSceneCharacterAuthoringDocument` | Editor 直接从当前场景角色文档构造参照队伍候选，不新增全局 Team Registry |
| Trigger 核心 Authoring v1 不拥有 Combat Sensor 参数 | 区域、layer、容量当前保存在 CharacterTest `RegionBinding`，再编译为 Sensor descriptor | 第一版筛选与 RegionBinding 同层持久化，不迁移 Trigger 核心 JSON |

因此，P3.2 的第一批真实变量是“相对哪个队伍，接受什么关系”；身份和最低能力先成为受编译器保护的正式契约，而不是为了可点而解除安全要求。

## 3. 创作者界面

### 3.1 区域 Buff

普通状态保持两行，不增加常驻卡片或编号步骤：

```text
作用对象
进入区域的角色
[可伤害]                         [阵营不限 ▾]
```

选择关系后只增加必要的参照项：

```text
作用对象
进入区域的角色
[可伤害]        [仅敌对 ▾] [相对：队伍 1 ▾]
```

交互规则：

- `可伤害`是动作模板要求的锁定标签，不是可取消的装饰信息。
- 关系选择为`阵营不限 / 仅同队 / 仅敌对 / 仅中立`。
- `阵营不限`时不显示参照队伍。
- 其他关系必须选择参照队伍；候选取自当前场景角色文档中去重、升序排列的正整数 TeamId。
- 当前场景只有一个队伍时仍显示该队伍作为参照；没有有效队伍时只保留`阵营不限`，并给出简短诊断。
- `仅自身`不出现在静态区域 Trigger 中，因为当前没有 generation-safe 的实体参照。

### 3.2 Story Trigger

Story 动作最终作用于当前区域，因此继续显示：

```text
作用对象
当前区域
```

区域里哪类对象能够触发 Story 放到`触发条件`中：

```text
触发对象
进入区域的角色          [阵营不限 ▾]
```

这样不会把“谁触发”与“动作作用于谁”重新混在一起。

### 3.3 渐进显示

- 基础身份：当前只有`角色`，显示只读文字；未来 source/compiler 真正提供两个以上身份时使用 `MxAuthoringReferenceField`。
- 必要能力：模板最低能力锁定显示；额外能力只进入`高级规则`，第一版可选`可移动`。
- 存活要求：区域 Buff 和 Story 默认且固定为只接受 Gameplay lifecycle alive 的对象，不增加低价值开关。
- Tag / Status：当前没有正式创作目录，不显示手动 ID 或空选择器。
- 历史未知值：保留原值、显示兼容警告，用户显式选择前不自动替换。

切换关系或参照队伍后只刷新当前 Trigger 卡片，保持属性区滚动位置与输入焦点。

## 4. Authoring 与编译契约

### 4.1 CharacterTest 首个持久化适配

在 `CharacterTestLevelGameplayTriggerRegionBinding` 上增加一个类型化 Subject Filter：

```text
CharacterTestLevelGameplayTriggerSubjectFilter
  AcceptedIdentityTypes
  RequiredCapabilities
  RequireAlive
  RelationFilter
  ReferenceTeamId
```

该结构是 CharacterTest 对 Gameplay 正式类型的薄适配，不定义第二套身份、能力或关系枚举。

模板最低要求：

| 模板 | 身份 | 最低能力 | 存活 | 默认关系 |
|---|---|---|---|---|
| 进入时施加 Buff | Character | Damageable | 是 | Any |
| 区域内维持 Buff | Character | Damageable | 是 | Any |
| 进入时触发剧情 | Character | None | 是 | Any |

有效规则：

- `AcceptedIdentityTypes`只能包含当前 source/compiler 声明支持的身份。
- `RequiredCapabilities`必须包含模板最低能力；用户只能追加，不能取消。
- `Any`要求 `ReferenceTeamId == 0`。
- `SameTeam`、`Enemy`和`Neutral`要求 `ReferenceTeamId > 0`。
- `Self`在 CharacterTest 静态区域来源中编译失败。
- 未定义 identity/capability/relation 位直接产生 typed diagnostic。

### 4.2 不修改 Trigger 核心 Authoring v1

第一版不向 `GameplayTriggerAuthoringDocument`、Route、Action payload 或 `GameplayTriggerAuthoringJson` 增加字段。

原因：

- 当前筛选发生在 Combat Source Provider 产生完整 occupancy 之前。
- Signal、Threshold 或外部 Unity occupancy provider 未必共享同一种候选目录。
- 把 Combat Sensor 的筛选字段塞进所有 Trigger Binding 会制造尚无消费者的通用协议。

未来第二个 source provider 需要同等筛选时，再依据两个实际适配提炼公共 Trigger Subject Filter Authoring，不提前设计 Provider 注册系统。

### 4.3 Gameplay 公共运行时值

在 Gameplay 层提供不可变的 `GameplayComponentTargetFilter` 与 relation context：

- 接受的基础身份；
- 必需能力；
- 是否要求 lifecycle alive；
- 关系；
- 可选 generation-safe 参照实体；
- 参照 TeamId。

`Self`必须有有效参照实体；SameTeam/Enemy/Neutral 必须有正整数参照 TeamId。旧 `GameplayComponentTargetQuery` 构造方式保持兼容，并投影到同一 filter/evaluator。

公共分配分类：

- filter、relation context、单 candidate 判定：`NoAlloc`；
- query/candidate/result 快照与 Editor 构建：`AllocByDesign`。

## 5. Runtime 数据流

```text
CharacterTest RegionBinding
  -> Subject Filter 编译与模板最低要求合并
  -> CharacterTest Scene Cache Sensor Row
  -> GameplayTriggerCombatSensorDescriptor
  -> CombatPhysicsWorld.Query
  -> CombatEntityGameplayMap
  -> Gameplay Component Target Filter
  -> accepted Subjects
  -> GameplayTriggerRuntime.TrySubmitOccupancy
  -> Entered / Stayed / Exited
  -> Buff 或 Story Action
```

筛选放在 occupancy 提交前有两个必要结果：

1. 被拒绝对象不创建 Trigger relation、route state 或 activation。
2. 某个对象在运行时因 team、classification 或 lifecycle 变化而不再匹配时，下一次完整采样会把它移出 occupancy，并产生正常的 `Exited`；Presence Buff 因此能够走既有 Remove 路由。

### 5.1 NoAlloc evaluator

从现有 `GameplayComponentTargetingService` 提取共享的单 candidate 判定核心。Sensor 直接读取 Component World 中已有的：

- `GameplayTargetClassificationComponent`
- `GameplayTeamComponent`
- `GameplayLifecycleComponent`

并调用 NoAlloc evaluator。不得在 Tick 内：

- 构造 List 或结果快照；
- 复制 Tag/Status 数组；
- 使用 LINQ、反射或字符串诊断；
- 查找 Unity GameObject、Collider 或 MonoBehaviour。

既有低频 targeting service 继续返回 selected/rejected 快照，但必须复用同一 evaluator，避免两份筛选规则漂移。

### 5.2 Sensor 与诊断

`GameplayTriggerCombatSensorDescriptor`携带编译后的 filter，并将全部字段纳入 Sensor Catalog hash 与 combined hash。

每个 binding 的 runtime diagnostics 增加：

- Query hit 数；
- 成功映射 Subject 数；
- 筛选通过数；
- 被筛除总数；
- 第一个或按固定类别聚合的 typed rejection；
- 当前有效筛选摘要。

主界面只显示简短状态；完整计数与 typed rejection 放入既有`运行时检查`页。

## 6. 版本与迁移

P3.2 会改变 CharacterTest Level Trigger module 与派生 Sensor Cache，但不改变 Trigger 核心 JSON：

| 契约 | 处理 |
|---|---|
| CharacterTest gameplay trigger module schema | v1 → v2 |
| CharacterTest trigger document schema | v1 → v2 |
| CharacterTest Gameplay Trigger scene cache | v1 → v2 |
| Combat Sensor catalog hash schema | v1 → v2 |
| `GameplayTriggerAuthoringJson` | 保持 v1 |
| Trigger Runtime SaveState / action payload / network schema | 不变 |

迁移规则：

- v1 文档加载时按模板生成默认 filter，不因为打开窗口而标记未保存。
- 第一次显式修改或保存时写出 v2。
- v1 默认值与当前正式示例一致：区域 Buff 为 Character + Damageable + alive + Any，Story 触发对象为 Character + alive + Any。
- 旧 cache 不猜测新 hash，必须通过统一`保存`事务重建。
- Runtime bootstrap 继续复验 source hash、Trigger catalog hash、Sensor catalog hash 和 combined hash；不一致时整份 fail-closed。
- 未知 v2 关系或 TeamId 保留原文档值并阻止编译，不自动回退为 Any。

当前 CharacterTest Combat ↔ Gameplay 映射只有角色，因此 v1 默认 filter 不改变现有可观察结果；这一点必须由迁移测试和现有 Playable 回归直接证明。

## 7. Undo、保存与关闭

- 关系和参照队伍的每次用户提交分别形成一个文档 Undo step。
- 修改复用现有文档事务、脏状态、保存基线和关闭时保存/放弃/取消流程。
- `Esc`关闭下拉或弹层不修改文档。
- 保存前先执行模板最低要求、队伍引用和 Sensor 编译预检。
- 编译失败不得写 JSON、Scene Cache 或 Unity Scene。
- Undo/Redo 后重建当前卡片、诊断与运行时缓存过期提示，不跳到属性区顶部。

## 8. 开发切片

P3.2 只拆成两个可独立验收的增量。

### P3.2a：筛选权威与 Runtime 闭环

- CharacterTest module v2 与 v1 兼容读取；
- Gameplay NoAlloc filter/evaluator；
- Sensor descriptor、catalog hash、cache 与 runtime filtering；
- typed diagnostics；
- focused noEngine/EditMode 测试与 NoAlloc 证明。

P3.2a 完成后，即使 UI 仍只读，也能通过构造 Authoring 文档验证同队、敌对、中立和非法参照。

### P3.2b：关卡编辑器创作闭环

- 区域 Buff 的关系与参照队伍控件；
- Story 的触发对象规则；
- Undo/Redo、保存、迁移警告、滚动与焦点保持；
- 运行时检查计数；
- CharacterTest Scene 手测和 focused PlayMode。

不再拆更多基础层，避免把一个筛选功能扩成独立工作台。

## 9. 验收计划

### 9.1 数据与编译

- v1 文档读取后得到正确模板默认 filter，打开不产生 dirty。
- v2 roundtrip 保留 identity、capability、alive、relation 与 ReferenceTeamId。
- Any + 非零参照、关系 + 零参照、Self、未知 flags 和未知 relation 均被 typed diagnostic 拒绝。
- 模板最低能力不能被 Authoring 解除。
- source/content/sensor/combined hash 对 filter 变化敏感且稳定。

### 9.2 Runtime

- 同一 AABB 中的同队、敌对和中立对象只提交匹配 Subject。
- 被筛除对象不产生 Entered、route state、activation 或 Buff 请求。
- 已进入对象切换 team、lifecycle 或 classification 后产生一次 Exited。
- Presence Buff 对筛选离开的对象执行既有 Remove。
- Sensor Tick 初始化后零分配。
- checkpoint/restore、replay/result hash 与相同 filter 下保持确定性。

### 9.3 Editor

- Buff 卡片直接编辑关系；Story 明确区分触发对象与作用对象。
- 参照队伍候选来自当前场景角色文档，去重且稳定排序。
- 只有一个身份时不显示身份下拉；最低能力不可取消。
- 未知历史值保持且警告，不自动清零。
- 修改、Undo/Redo、保存、重新加载均保持筛选值。
- 切换选项不跳顶、不抢走正在编辑的其他字段焦点。

### 9.4 实际场景

- 以 Team 1 为参照选择`仅敌对`，验证 Team 2 角色进入区域时触发，Team 1 不触发。
- 改为`仅同队`后结果反转。
- `阵营不限`保持当前全部角色可触发行为。
- 运行时检查显示 Query、Mapped、Accepted 与 Filtered 计数。
- 执行 Unity 编译、Console、focused EditMode、focused PlayMode、轻量检查、代码规范报告和实际窗口视觉检查。

## 10. 非目标

P3.2 不做：

- 任意切换 Owner、Subject、Instigator、ExplicitBindingTarget；
- 静态区域的 Self 筛选；
- 世界对象、空间点或区域作为 Combat occupancy Subject；
- 全局 Team Registry、Player/NPC/Boss/Summon 身份枚举；
- Tag/Status 手动 ID、搜索窗口或布尔表达式编辑器；
- 每个 Action 独立的二次目标查询、目标数量或随机目标；
- Unity Physics、Collider Trigger 或 Scene GameObject 作为玩法筛选权威；
- 活动 Runtime Session 热重载；
- Trigger SaveState、action payload、网络或回放协议迁移。

## 11. 采用前提

- 第一消费者继续是 CharacterTest Combat AABB 区域。
- 当前场景角色 TeamId 是 P3.2 参照队伍候选的正式项目来源。
- 关系筛选针对 Trigger Source Subject；Buff 模板的 Action 也指向 Subject，因此可以在同一个`作用对象`区域编辑。
- 只有出现正式 WorldObject Combat ↔ Gameplay 映射后，CharacterTest 才能把基础身份选择升级为多选项。

## 12. 实现记录

P3.2 已按两个增量完成：

- P3.2a：`GameplayComponentTargetFilter`与共享 NoAlloc evaluator 已接入 component targeting 和 Combat Sensor；Sensor 在提交 complete occupancy 前按身份、能力、存活和关系筛选，并把 filter 纳入 catalog hash、CharacterTest scene cache v2 和运行时诊断。
- P3.2b：区域 Buff 可直接选择阵营关系和参照队伍；Story 保持“当前区域”为作用对象，并单独编辑进入区域的触发对象；`必须可移动`作为高级规则按需显示。
- CharacterTest gameplay trigger module/document/cache 已从 v1 升为 v2。v1 文档加载时按模板补齐默认 filter，只有显式保存才写出 v2；同一次保存会重建旧 cache。
- 关系、参照队伍和额外能力均复用现有文档事务、Undo/Redo、脏状态与保存基线；刷新保持属性区滚动位置。

开发入口与手工验收步骤见 [P3.2 开发指南](Guides/LEVEL_EDITOR_TARGET_FILTER_AUTHORING_DEVELOPMENT.md)。
