# Gameplay Trigger Automation

本目录是测试 Trigger 的持久化管理面，不提供 Scenario CRUD 工具。

人工运行入口是 Unity Editor 菜单
`MxFramework/Testing/Trigger Automation`。它只负责发现、运行、显示逐操作日志和
打开最新报告；Scenario 的创建、修改与删除仍由类型化 C# 和目录管理。

- 当前持久化 Scenario 保留二十六条独立现场操作：
  “打开 Runtime AI Lab 场景”、“进入 Play Mode”、“开始测试”、
  “真实场景 · 推进一帧”、“继续模拟”、“暂停模拟”、“重置测试”、
  “定位到指定历史帧”、“定位到最新帧”、“开启历史帧重算”、
  “确认建立新未来”、“取消历史帧重算”、“放大时间轴”、“缩小时间轴”、
  “适配完整时间轴”、“选择指定时间轴事件”、“定位事件起点”、
  “定位事件终点”、“关闭事件详情”、“切换运行轨迹 / 行为计划”、“选择测试槽位”、
  “选择槽位角色”、“选择槽位行为组”、“启用所选槽位”、“禁用所选槽位”，
  以及“玩家创作与发布主链路”。
- Runner、ActionSink 和报告器的 NoEngine 覆盖使用不可发现的单元测试 fixture，
  不作为 Scenario 注册、索引或保留。
- 前两条是原子 Editor Trigger，不合并成一条：打开场景只负责加载并验证唯一
  Controller；进入 Play Mode 只在目标场景就绪后发起切换，并通过
  `SessionState` 跨 Domain Reload 完成结果校验。
- `PlayMode` Scenario 只有进入 Play 后才可点击，会通过测试 Trigger 的完整
  Signal → Intent → Function 链路调用现有场景命令入口并独立校验结果。
- 首个真实场景用例“真实场景 · 推进一帧”要求 Runtime AI Lab 已开始测试、
  处于暂停状态并已定位最新帧；成功后 Game View、时间轴和游标都会前进一帧。
- “开始测试 / 继续模拟 / 暂停模拟 / 重置测试”使用明确的目标状态，不暴露
  含义随当前状态变化的 Toggle Trigger；每条均重新读取 Controller 快照完成断言。
- “定位到指定历史帧”会显示目标帧整数输入，并校验已提交的确定性恢复目标或
  最终选中帧恰好等于该目标；重算请求、确认和取消
  分别校验 `Pending`、`Armed` 和完全清理后的状态。
- 时间轴观察 Trigger 复用正式缩放、事件选择和端点定位入口；事件选择显示
  “事件序号”输入，选择事件即打开详情，关闭详情则显式清除当前事件选择。
- “切换运行轨迹 / 行为计划”验证显式模式命令和两条旧路由 alias；三次切换均
  保持同一个 Runtime Test Workbench FairyGUI 根、智能体/帧/播放状态和 Game 镜头。
- Setup Trigger 只在配置阶段运行；选择操作校验实际槽位数据，启用和禁用使用
  明确目标状态而不是暴露含义不确定的 Toggle。
- “玩家创作与发布主链路”通过真实 Player Deck、隔离 COW Draft、编辑、
  Undo/Redo、本地 append-only Publish、返回卡组、移除/安装、重开和
  `Apply F+1` 入口完成一条端到端验收；最终重新读取运行帧、事件和 authority
  hash，确认变更只在下一帧边界生效。

- `Scenarios/<module>/<case>/`：一个目录一个可运行 Scenario；删除目录即删除用例。
- `Functions/`：可复用的受控测试函数。
- `Runtime/`：自动发现、bounded function queue、Runner、Probe 和报告。
- `AGENTS.md`：Development Agent 创建、修改和删除用例时的强制规则。

运行索引或 Scenario 后，可重建输出位于：

```text
Temp/MxFramework/TriggerTests/index.json
Temp/MxFramework/TriggerTests/<scenario-id>/latest.json
Temp/MxFramework/TriggerTests/<scenario-id>/latest.md
```

详细用法见 `Docs/Guides/GAMEPLAY_TRIGGER_TEST_AUTOMATION.md`。
