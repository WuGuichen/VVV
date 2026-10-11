# Runtime AI Lab 行为职责实施台账

## 当前权威

- 行为组安装记录使用规范化 `ResponsibilityKinds` 集合，可同时属于多个兼容职责。
- 固定顺序为 `Targeting → EngageOrPressure → Damage → Fallback → Other`；重复项和输入顺序会被规范化。
- `Other` 与四种明确职责互斥；没有明确职责时自动回到 `Other`。
- 职责只属于作者组织信息。候选资格、分数、稳定顺序、运行语义哈希、snapshot、network 与 replay 均不读取职责集合。
- 缺少某个职责只表现为数量 `0`，不阻止保存、发布或开始测试；不兼容归属仍阻止运行和发布。
- 加载、筛选、切换 Tab、保存和重载都不得新增、复制、启用、禁用或重排行为记录。

## 数据版本

| 内容 | 当前版本 | 兼容读取 |
| --- | --- | --- |
| 独立行为组 | `behavior-group.v4` | v3 `responsibilityKind` 与 v1/v2 `slotKind` 迁移为单元素集合 |
| Runtime AI Lab 草稿 | `runtime-ai-lab.behavior-group.v4` | `Flexible` 迁移为仅包含 `Other` |
| 项目行为组目录 | `behavior-group-catalog.v3` | 先按旧 schema/hash 验证，再构造当前身份 |

职责集合进入行为组内容哈希，但不进入编译运行语义哈希。集合的序列化顺序和重复项不会改变规范化后的内容哈希。

## 当前 UI

- 左侧 `460×312` 行为区域由一行六个筛选 Tab 和一个共享 `GList` 组成。
- Tab 固定为：全部、索敌、接战、输出、兜底、其他；始终显示名称和安装记录数量。
- “全部”按唯一 `EntryId` 显示每条安装记录一次；职责 Tab 显示包含该职责的记录，并保持行为组原始 `Order`。
- 列表只池化 `RuntimeAiLabBehaviorCard`，不再生成职责标题行、展开箭头、子列表、缩进竖线或职责内添加按钮。
- 卡片内 `inlineResponsibilities` 打开五项多选面板；确定时通过 `TrySetEntryResponsibilities` 一次提交完整集合并形成一个 Undo。
- 不兼容职责不可选择；取消、Esc、无变化、切换 Tab 和筛选均不产生 Dirty、Undo 或 revision。
- 顶部“添加行为卡”是唯一添加入口；行为库会继承左侧当前职责 Tab，但 Tab 只决定初始筛选，不决定保存结果。
- 行为库与已安装列表共享同一套六项职责名称与顺序：`全部 / 索敌 / 接战 / 输出 / 兜底 / 其他`。左侧数量统计安装记录，行为库数量统计当前搜索结果中能够承担职责的正式行为卡。
- 行为库职责从正式能力元数据推导：`Targeting → 索敌`、`Engage/Pursuit/Control → 接战`、`Damage → 输出`、`Fallback → 兜底`；没有命中明确职责的行为只归入“其他”。活动路径不再读取 `IconKey` 分类。
- 同一行为可同时出现在多个兼容职责 Tab，但在任一单独 Tab 和“全部”中都只投影一次。
- 添加任意行为时由 Controller 重新计算全部兼容职责并一次写入；存在明确职责时写入全部明确职责，没有明确职责时只写入 `Other`。从哪个行为库 Tab 添加都得到相同结果。
- 行为库搜索同时匹配名称、用途、标签、能力名称和职责名称；切换职责和搜索只修改本地筛选状态，不产生 Dirty、Undo 或 revision。
- 已有安装记录不会因为打开行为库、升级行为卡、筛选、搜索、保存或重载而自动重分类。

## 防重复不变量

- Tab 投影只读取现有 `EntryId`，不调用任何行为组 mutation。
- `GList.numItems` 必须等于当前筛选后的唯一 `EntryId` 数量。
- 列表重建使用延迟回收与同构行为卡对象池，不再混用标题/卡片原型。
- `EnsureContract` 兼容入口无副作用，不得补卡或生成 `Wait or hold`。
- 旧生成物清理只处理能够证明为历史兼容修复产物的记录；用户安装和正式发布的重复行为全部保留。

## 本轮证据

- UI Composer `publish-all` 已通过 Preflight、Apply、Publish 与 XML/package/state/bytes/catalog/manifest 核对。
- Unity 已完成 refresh、compile、domain reload、Editor ready，Console error=0。
- `CharacterTestRuntimeAiPlannerBehaviorResponsibilityTests`：14/14 通过，覆盖能力到多职责投影和自动全职责添加事务。
- 行为库能力/职责中文搜索专项测试：1/1 通过；正式 Composer 源包六职责 Tab 专项测试：1/1 通过。
- 历史帧职责命令分类专项测试：1/1 通过，职责集合编辑在只读历史帧中被明确归为禁止修改，不再误报未知命令。
- 行为组 v2 迁移、v4 roundtrip 与正式共享列表发布包专项测试：3/3 通过。
- 正式 FairyGUI 包包含六个职责 Tab、共享 `workbenchEntryList` 和卡片 `inlineResponsibilities`；活动 Screen 不再绑定职责标题原型。
- 真实 Game View 已验证：从左侧“输出”打开行为库时右侧默认选中“输出”；“火球压制”和“突进连击”同时显示“可承担：接战、输出”，并分别出现在两个职责筛选中。

## 待真实 Game View 验收

- 给一条行为同时勾选“索敌、接战”，确认在两个职责 Tab 中分别出现一次，在“全部”中只出现一次。
- 反复切换六个 Tab 至少二十轮，确认底层 Entry 数量和实际 UI 子项数不增长。
- 验证职责多选的确定、取消、不兼容禁用、Undo/Redo 与保存重载。
- 验证职责筛选不影响当前运行行为、时间轴播放游标和 Event 高亮。
