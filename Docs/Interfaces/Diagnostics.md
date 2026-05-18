# Diagnostics 接口

## 职责

Diagnostics 定义运行时调试快照协议，让 Editor 和工具读取运行时状态时不依赖模块私有字段。

## 公开接口

```csharp
public interface IFrameworkDebugSource
{
    string Name { get; }
    FrameworkDebugMode Mode { get; }
    bool IsAvailable { get; }
    FrameworkDebugSnapshot CreateSnapshot();
}
```

| 类型 | 用途 |
|------|------|
| `FrameworkDebugMode` | Authoring / Runtime |
| `FrameworkDebugSection` | 报告片段 |
| `FrameworkDebugSnapshot` | 调试快照 |
| `FrameworkDebugReportExporter` | 文本报告导出 |
| `ResourceDebugSource` | Resources 模块提供的运行时资源诊断源 |
| `FrameworkPerformanceCounterCost` | 计数器采样成本标记，区分未知、无分配和设计性分配 |
| `FrameworkPerformanceCounterSample` | 单个性能计数器样本，包含 id、展示名、分类、数值、单位、成本和可选预算 |
| `FrameworkPerformanceCounterSnapshot` | 一组稳定排序的性能计数器样本 |
| `FrameworkPerformanceCounterRecorder` | 默认关闭的 opt-in counter recorder；关闭时写入为 no-op |
| `FrameworkPerformanceCounterDebugSource` | 将 counter snapshot 暴露为只读 Diagnostics source |
| `FrameworkSimulationMetric` | Simulation Harness 报告中的指标项 |
| `FrameworkSimulationTimelineEvent` | Simulation Harness 报告中的时间线事件项 |
| `FrameworkSimulationFailure` | Simulation Harness 报告中的失败项 |
| `FrameworkSimulationScenarioResult` | 单个 noEngine simulation scenario 的结果 |
| `IFrameworkSimulationScenario` | 可由 batch runner 执行的 simulation scenario 契约 |
| `DelegateFrameworkSimulationScenario` | 用委托包装 scenario 的轻量实现 |
| `FrameworkSimulationBatchRunner` | 批量执行 scenario 并捕获异常为失败结果 |
| `FrameworkSimulationReport` | Simulation Harness 批量报告根对象 |
| `FrameworkSimulationReportDebugSource` | 将 simulation report 暴露为只读 Diagnostics source |
| `FrameworkSimulationReportFormatter` | 输出 Markdown / JSON simulation report |

## 使用约定

- Runtime Debug 默认只读。
- 可写调试命令必须另设命令接口，不能塞进 Snapshot。
- Editor 读取 Debug Source，不直接读运行时对象私有字段。
- 游戏层在自己的组合根中注册 Debug Source，框架不提供全局单例。
- Resources 模块可通过 `new ResourceDebugSource(resourceManager)` 接入同一诊断报告链路。
- Issue #85 `Runtime Debug UI` 设计沿用该只读 Snapshot 契约；通用运行时调试 overlay 只做 source registry / snapshot aggregation，可写命令通过独立 provider 设计。
- Performance counters 默认关闭；模块或组合根需要显式启用 recorder 或提供 snapshot factory，避免默认运行路径承担诊断成本。
- Performance counter 和 Simulation Harness 都是观察 / 报告 API，不写 Replay、SaveState 或 Runtime hash。
- Simulation Harness scenario 必须是普通 noEngine 对象；Unity 场景、Prefab 或 Editor asset 只能作为上层输入，不成为 batch runner 的硬依赖。
- Simulation report 至少保留 metrics、timeline events 和 failures 三类结构化输出，Markdown / JSON 仅是导出格式。

## 测试入口

`Assets/Scripts/MxFramework/Tests/Diagnostics/`
