# CharacterTest Runtime AI Lab 行为配方 v8 实施台账

> Status: Current

> Implementation: Implemented; automated Runtime AI Lab interaction passed; final Unity gate is blocked only by unrelated VFX stale evidence
>
> Implemented: 2026-08-16
>
> Schema: `behavior-card-logic.v8`; runtime contract `5`; snapshot schema `10`

## CharacterAction 选择器闭环

- Event Inspector 的 CharacterAction 参数由裸 ID 输入升级为 Composer-owned、可搜索、可滚动的正式动作选择器。
- 选项权威来自 `CharacterActionConfigRegistry.Actions`；选择继续写入 v8 `Operation.CharacterActionId`，不改变 schema、runtime contract 或 snapshot schema。
- 条件树中的 CharacterAction 引用复用同一选择器；时间轴摘要优先显示动作名称并以稳定 ID作为运行身份。

## 已实施

- `BehaviorRecipeEvent` 新增权威 `TimingKind=Instant/Duration`，时间类别进入 semantic hash。
- Instant 固定在单一采样帧，规范化为 `1f / 每 pass 1 次 / 0f 冷却`；Duration 保留固定半开区间、重复和冷却。
- WaitForCondition 固定为 Instant；LoopRegion 固定为 Duration；Hold 已从当前行为配方移除；其他主要 Operation 支持两类时间语义。
- v7 先按旧 schema/hash 认证，再按操作和旧时长迁移到 v8；迁移保留全部 Timeline/Event 稳定身份且不增删 Event。
- Compiler 缓存显式时间类别并阻断非法组合；Evaluator 对 Instant 只在目标帧采样，对 Duration 在区间内逐帧采样。
- Instant 启动的 Move、CharacterAction 和 Ability 按自身生命周期继续运行；Duration 角色控制在右边缘硬取消。
- Timeline 末端遇到仍在运行的 Instant 角色操作时进入结构化等待，操作结束后再返回 Planner。
- snapshot schema 10 保存末端等待状态；runtime contract 4 / snapshot 9 不静默恢复。
- 右键菜单提供“添加单帧事件”和“添加持续事件”；新 Duration 默认 `30f`，两者创建均为单一 Undo 事务。
- Inspector 增加正式时间类别控件；Instant 隐藏时长、次数和冷却，类别切换可原子清理不兼容 Operation。
- 时间轴用精确帧针加浮层卡片显示 Instant，用真实 Span 显示 Duration；`1f Duration` 仍保留区间语义和右边缘。
- 拖动和缩放统一使用 lane 本地坐标与 `XToFrame` 帧吸附；右边缘不再用视觉宽度叠加 delta，鼠标位移与时间位移一比一。
- 正式项目行为卡目录由 Unity 权威生成器升级到 v8/合同 5；恢复命令重绑卡片 hash 时保留项目已有行为组，不再删除“木桩 副本”等作者内容。

## 自动验证证据

- v8 schema、迁移、semantic hash、Instant/Duration 生命周期、末端等待、正式项目目录与发布卡：`ba28259d3da544ceb3fe3b443112c387`，`63/63` 通过。
- Timeline 投影、帧针/Span 类型、lane 本地坐标拖拽/吸附和 TimingKind 原子 mutation：`80cd50dadd464e8e8a9e0fdf95f0505e`，`3/3` 通过。
- 正式 FairyGUI package 的 TimingKind、时长输入和条件树槽位：`a9f5da5351cd494cb783bcfc836879af`，通过。
- 无效 v8 草稿不覆盖最后有效快照：`3df993ec237e4eee9abad649ba0e4177`，通过。
- 发布前 Unity 脚本刷新、编译、domain reload、Editor ready、Console error=0：`6a375cd33260483bb9e504ccd4451a5b`，通过。
- 发布后 Unity 脚本刷新、编译、domain reload 和 Editor ready 成功；最终 Console 检查被无关的 VFX consumer stale evidence `vfx.magic.fireball_burst.charge` 阻断，job `ff77e7ececf94d82a6fee84efdc53297`。本任务未修改 VFX 资产来掩盖该外部错误。

## Composer 与真实界面证据

- Composer operation `471067b4f5b94e768e79c7a2c3365350`：Preflight、HelperReady、Apply `2be7c5f1e42640b79641f59bc3f18e04`、source hash 核对、Publish `f8d14d397aa14d8dac7d4fdfc551c945` 与 artifact 核对全部成功。
- 正式 source XML hash `e62b0ce626521fef8f2633521a9f4d403fc197c1682c300965ec2acbf962bbc7`；published `_fui.bytes` hash `bedf6d1fdfdd79c60a427a5cced8a5d73dea83397fe5244c3661d2758fe896e8`。
- Runtime AI Lab scene、Play Mode、published panel、command binding 与真实 Game View Trigger 通过；运行证据为 `behavior-card-logic.v8 / runtime contract 5 / MxCharacterTestRuntimeAiLabUi`，并验证 Event 选择、Timeline 空白选择和正式 Inspector 路由。
- 两类右键创建入口、类别切换与拖拽吸附已有自动化投影/命令/几何证据；自由鼠标手感仍建议在下一次人工验收中复核，不以 XML 证据冒充实际手感。

## 兼容边界

- v7 正式卡和草稿可迁移编辑，只有显式保存或发布后写回 v8。
- v6 开放区间仍沿既有链迁移并要求作者确认固定长度；v8 不重新引入开放区间。
- 未配置 Operation、非法 TimingKind 或不兼容 Operation 只保留在内存编辑状态，不能保存成可运行草稿、运行或发布。
- CharacterAction 公共编排接口未修改。
## 专用角色动作事件补全

- 角色动作事件继续复用 v8 Event 持久化结构，不升级 schema、Runtime contract 或 snapshot。
- 新建流程为“右键帧 → 添加角色动作事件 → 正式动作选择器 → 原子创建”，不存在未配置的中间 Event。
- 普通 Event 操作菜单不再提供 CharacterAction；动作事件固定为 Duration、无条件、一次执行、零冷却。
- CharacterAction 目录帧长是片段宽度、编译和运行 authority 的唯一时长权威；无正帧长动作不可选择。
- 时间轴动作片段只能水平移动，不能调整右边缘；Inspector 只显示动作引用、只读开始/长度、优先级和运行状态。
- 旧带条件 CharacterAction Event 保留原身份并阻断运行，提供显式“移除条件并转为角色动作事件”修复。
