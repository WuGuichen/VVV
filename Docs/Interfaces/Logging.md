# Logging 接口

## 职责

Logging 提供 noEngine、可测试、固定容量的框架日志基础设施，用于记录“发生过什么”的时间序列。它服务 Runtime HUD、Preview、Demo、Bridge 和诊断排查，但不替代 Diagnostics 当前状态快照、`RuntimeEventQueue<T>` 可消费事件、异常 / result 控制流错误，也不进入 replay / hash 权威状态。

## 程序集边界

| 程序集 | 路径 | 依赖 | 说明 |
|------|------|------|------|
| `MxFramework.Logging` | `Assets/Scripts/MxFramework/Logging/` | `MxFramework.Core` | noEngine 日志核心，不引用 Diagnostics、Runtime、UnityEngine 或 UnityEditor。 |
| `MxFramework.Logging.Diagnostics` | `Assets/Scripts/MxFramework/Logging.Diagnostics/` | `MxFramework.Logging`、`MxFramework.Diagnostics` | Diagnostics 适配层，只提供 `LogDebugSource`。 |

本模块当前不提供 RuntimeHost 接入、PreviewLogBuffer 替换、Unity sink、文件落盘、远程上传或异步队列。这些能力应拆分到后续 Issue。

## 公开接口

| 类型 | 用途 | 分配行为 |
|------|------|----------|
| `LogLevel` | `Trace` / `Debug` / `Info` / `Warning` / `Error` / `Critical` 严重级别。 | NoAlloc |
| `LogEntry` | 调用方提供的日志 payload，包含 level、category、message、frameValue、traceId、code。 | NoAlloc after initialization |
| `LogRecord` | `LogBuffer` 写入时生成的 buffer-local sequence + entry。 | NoAlloc after initialization |
| `ILogger` | 日志入口，提供 `IsEnabled` 和 `Log(in LogEntry)`。 | `IsEnabled` NoAlloc；`Log` NoAlloc after initialization |
| `ILogSink` | sink fan-out 目标。 | NoAlloc after initialization |
| `LogBuffer` | 固定容量 ring buffer，生成 buffer-local sequence，并记录 dropped count。 | NoAlloc after initialization；`CopyTo(List<LogRecord>)` NoAlloc |
| `DefaultLogger` | `MinLevel + exact category allowlist` 过滤，并 fan-out 到多个 sink。 | NoAlloc after initialization |
| `NullLogger` | 空实现，适合作为默认 logger 避免 null 判断。 | NoAlloc after initialization |
| `BufferedLogSink` | 将日志写入 `LogBuffer`。 | NoAlloc after initialization |
| `LogDebugSource` | 将 `LogBuffer` 最近日志导出为 `FrameworkDebugSnapshot`。 | AllocByDesign |

## 语义约定

- `LogEntry` 不包含 sequence；sequence 由 `LogBuffer` 写入时生成。
- sequence 是 sink-local / buffer-local。多 sink fan-out 时，不同 `LogBuffer` 的 sequence 不需要一致。
- `LogEntry.FrameValue == -1` 表示 unknown / no-frame。调用方无帧上下文时必须填 -1。
- `LogBuffer` 超过容量时丢弃最旧项，并递增 `DroppedCount`。
- `LogBuffer.CopyTo(List<LogRecord>)` 按 sequence 升序追加到调用方提供的列表，不清空列表。
- `DefaultLogger` 的 category allowlist 使用 `StringComparer.Ordinal` 精确匹配。allowlist 为 null 或空集合时允许所有 category。
- 高频路径不做字符串拼接；调用方应使用 `ILogger.IsEnabled` 保护昂贵 message 构造。

## Diagnostics 映射

`LogDebugSource.CreateSnapshot()` 固定映射：

- `SourceName = "Logging"`
- `Mode = FrameworkDebugMode.Runtime`
- `Sections` 只有一个 `FrameworkDebugSection`
- section `Title = "Logs"`
- section `Body` 按 `LogRecord.Sequence` 升序输出最近日志
- 每行基础格式为 `[level] [category] message`
- 当 `FrameValue != -1` 时追加 `frame=<value>`

`CreateSnapshot()` 和文本 export 属于调试路径，允许分配。

## 使用约定

```csharp
var buffer = new LogBuffer(128);
var logger = new DefaultLogger(
    LogLevel.Info,
    new ILogSink[] { new BufferedLogSink(buffer) },
    new[] { "Runtime" });

if (logger.IsEnabled(LogLevel.Info, "Runtime"))
    logger.Log(new LogEntry(LogLevel.Info, "Runtime", "host initialized"));
```

游戏层或组合根负责持有 logger / sink / buffer。框架不提供全局静态 logger。

## 测试入口

`Assets/Scripts/MxFramework/Tests/Logging/`
