# CharacterTest Runtime AI Lab 行为配方 v7 实施台账

> Status: Archive; superseded by behavior recipe v8 explicit Event timing categories
>
> Implemented: 2026-08-14
>
> Schema: `behavior-card-logic.v7`; runtime contract `4`; snapshot schema `9`

## 已实施

- 保存权威升级到 v7；v6 内容先按旧 schema/hash 认证，再迁移成 v7。
- 原生 v7 JSON 不再输出 `DurationMode` 或 `MaxExecutionsPerEntry`；所有 Event 都使用固定半开区间。
- v6 开放区间迁移后保留原 Event 身份和配置，并设置 `DurationReviewRequired=true`；作者确认固定长度前不能编译、运行或发布。
- Timeline 长度由最右 Event 的右边缘推导；空 Timeline 立即结束，到达末端以结构化原因 `TimelineCompleted` 返回行为决策。
- 编译器校验固定区间、等待点、Loop 重叠、时间控制冲突、组合条件树和资源引用；普通 Event 允许跨越 Loop 边界。
- Evaluator 使用显式 `LocalPlayheadFrame`，不再用 RuntimeFrame 差值推导局部帧。
- `WaitForCondition` 冻结局部游标，但继续刷新真实 RuntimeFrame、事实、动作和冷却。
- `LoopRegion` 按退出条件和重复上限回绕；每次回绕重置区间内的 pass 次数和 Event 结果，同时保留真实帧冷却。
- 跨越 Loop 边界的 Event 保持 Timeline 级执行状态和角色控制权，退出循环后继续到自身右边缘，不再产生 `CARD_V7_EVENT_CROSSES_LOOP_BOUNDARY` 阻断。
- snapshot schema 9 保存局部游标、pass generation、Loop 重复次数、等待点、pending 时间控制、角色控制所有者和前后帧 Event 结果。
- Inspector 移除了开始帧和持续类型；普通 Event 只显示时长和冷却的“帧”单位，并增加“等待条件”“循环区间”操作。
- 时间轴不再绘制 Open Span；等待点只能移动，Loop 可移动和调整宽度；末端线不占 lane。
- 正式“持续接敌”改为 `30f 接近当前目标 + 30f 条件 Loop`，退出条件仅包含“目标距离 ≤ 1.5 米”或“可用移动路径 = 否”。
- 运行覆盖层读取 v7 真实局部游标；等待时停驻，Loop 回绕时直接回到起点，不做伪插值。
- 运行期高频 Console 输出已收敛，不再逐帧重复输出相同覆盖层日志。
- 行为卡草稿改为按 `StableCardId` 共享：多个安装记录、多个行为组和单卡编辑入口均引用同一份定义，不再在编辑时 Copy-on-Write。
- 行为卡修改只提升共享卡片 revision；行为组的安装、删除、启用、职责与顺序修改只提升行为组 revision。两类保存状态和按钮启用条件已经分离。
- workspace schema 升级到 v3；加载旧工作区时会把同一稳定卡身份的历史组内副本合并，并把全部行为组引用重定向到优先保留的共享定义。
- 新实验按稳定卡身份解析最新兼容 Published revision；行为组中保留的旧 revision/hash 只用于来源审计，运行开始后继续冻结不可变 candidate/Overlay 快照。

## Composer 发布证据

- `publish-all` operation: `aae10b5b7858454287e742857f77bdfc`
- Recipe hash: `881c7ee84d62b722fd624ca480083bf822e88fdf49c653e22a85edf367095471`
- Binding contract hash: `91789a5e45e0ebff1e3b5a2171fbd036bf89a61dc517e1430439d7cd5e335ab1`
- Published `_fui.bytes` SHA-256: `2b264097fad6f6d372acd78dbc6c68216a107ce105e6e658687c6bb8888c6175`
- Apply、Publish、source XML、package state、bytes、catalog 和 generated manifest 一致性核对通过。

## 自动测试证据

- v7 schema、迁移、条件树、Compiler、Wait、Loop、末端退出、快照和确定性：30/30 通过。
- 正式项目行为目录与正式“持续接敌”：14/14 通过；与上述核心套件合并执行为 44/44 通过，job `34909a98d39e4c57a774c03912a8e806`。
- 正式行为卡发布、旧哈希认证、v7 迁移阻断、引用与内容哈希：12/12 通过，job `a91ddd85ddc342c1809a01777d80705c`。
- 统一 Event 投影、真实游标、authority 隔离、Timeline 跳转覆盖刷新和历史命令策略：5/5 通过，job `a3cfc9345ece40089105ed3ff42badad`。
- 共享行为卡跨组传播、旧 Copy-on-Write 合并、组 revision 隔离、按当前所选卡判断保存状态、workspace v3 只读门禁：4/4 通过，job `d96989da07304978bb872d217ca6e57b`；跨两个行为组共享并删除单个安装记录不删除共享定义的补充回归 1/1 通过，job `a2d67f2052b942c1b6394ea27f34cb28`。

## Unity 与真实界面证据

- Unity refresh、compile、domain reload、Editor ready、Console error=0 曾完整通过，job `ed7bcf5cc2da4f2fa3dc2277cd858c8e`。
- 共享行为卡改造后的最终 Unity refresh、compile、domain reload、Editor ready 和 Console error=0 全部通过，job `2357958fdd7a4bafb8930dec40bdb30a`。
- Runtime AI Lab 真实 Game View 已确认 RuntimeFrame 从 `3` 前进到 `4` 时，当前 Timeline 的真实局部游标从 `0f` 前进到 `1f`：
  - `Logs/RuntimeAiLabV7/runtime-ai-lab-v7-running-cursor.png`
  - `Logs/RuntimeAiLabV7/runtime-ai-lab-v7-live-latest.png`
- 真实界面验证中，对本机旧 v6 开放 Event 的确认只发生在 Play Mode 内存中，并显式禁止草稿自动保存；本机磁盘草稿没有被验证步骤修改。
- 当前本机旧草稿若仍包含开放区间，会按设计显示“请确认固定长度”并阻止运行；这不是静默迁移或数据丢失。

## 兼容边界

- v6 正式卡和草稿可迁移编辑；未确认开放区间可保存在恢复草稿中，但不能编译、运行或发布。
- v4/v5 继续按 `v4 → v5 → v6 → v7` 顺序认证和迁移，不会自动生成目标消失、路径失败等额外条件。
- runtime contract 3 / snapshot schema 8 活动状态和回放不静默恢复，必须从测试第 0 帧重新模拟。
- v6 实施记录保留为 Archive，不再是当前运行或 UI 权威。
