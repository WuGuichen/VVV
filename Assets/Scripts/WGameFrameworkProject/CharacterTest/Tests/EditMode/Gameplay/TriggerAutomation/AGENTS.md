# Trigger Automation Agent Guide

本层管理 Development Agent 创建和维护的测试 Trigger。测试资产只属于
`MxFramework.Tests`，不得进入正式 Trigger catalog、Player build、SaveState、
Replay 或 authority hash。

## 必读入口

修改本层前读取：

1. `Docs/Guides/GAMEPLAY_TRIGGER_TEST_AUTOMATION.md`
2. `Docs/Interfaces/Gameplay.Triggers.md`
3. `Docs/API_STANDARDS.md`
4. `Docs/QUALITY_GATE.md`

## 目录权威

- 一个 Scenario 对应一个目录：
  `Scenarios/<module>/<case-slug>/<ScenarioName>.cs`。
- ScenarioId 固定为 `mx.trigger.test.<module>.<case-slug>`。
- 目录是人工索引；运行时索引由程序集自动发现生成，不维护手写 Catalog。
- 删除 Scenario 时只删除确认过的完整 Scenario 目录；不提供 Archive 或软删除，
  恢复依赖 Git。
- `Temp/MxFramework/TriggerTests/` 是可重建报告，不得提交。

## Development Agent 创作规则

- 创建前先搜索已有 ScenarioId、FunctionId 和公共 `Functions/`，优先复用。
- Scenario 必须使用类型化 `ITriggerTestScenario.Build(...)`，不得新增 JSON schema。
- 公共函数放 `Functions/`；场景专用 handler 放自身 Scenario 目录。
- 新函数必须显式注册稳定 FunctionId 和 capability；禁止字符串方法名反射、
  任意 shell、网络或持久化工具命令。
- Trigger function action 只使用
  `TriggerTestActionKindIds.FunctionCall`；`Payload0` 是局部 CallId，
  `Payload1/2` 必须为零。
- Function ActionSink 的 `TryPrepare/Abort/Commit` 是 NoAlloc 热路径；
  `Commit` 只入 bounded queue。自定义 handler 只能在 Host Tick 后执行。
- 断言必须经独立 Probe 读取 Trigger 或目标模块状态；函数返回值不能作为唯一
  oracle。
- 正式 catalog 的集成测试只驱动和观察正式组合，不向正式 catalog 注入测试
  Action。测试专用 catalog 只能存在于测试 fixture。
- Unity、截图或 custom-tool 验证写成 external evidence descriptor，由
  Development Agent 在内部 C# 测试后执行；`PendingExternalEvidence` 不能描述为
  完整通过。

## 最小验证

- Unity refresh/compile 后检查 task-related Console error。
- 运行
  `MxFramework.Tests.Gameplay.TriggerAutomation.TriggerTestAutomationTests`。
- 修改 SaveState/Replay/Hash fixture 时追加对应 Trigger focused 回归。
- 运行 `git diff --check` 和 changed-file standards report。
