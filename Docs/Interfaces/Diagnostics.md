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

## 使用约定

- Runtime Debug 默认只读。
- 可写调试命令必须另设命令接口，不能塞进 Snapshot。
- Editor 读取 Debug Source，不直接读运行时对象私有字段。
- 游戏层在自己的组合根中注册 Debug Source，框架不提供全局单例。
- Resources 模块可通过 `new ResourceDebugSource(resourceManager)` 接入同一诊断报告链路。
- Issue #85 `Runtime Debug UI` 设计沿用该只读 Snapshot 契约；通用运行时调试 overlay 只做 source registry / snapshot aggregation，可写命令通过独立 provider 设计。

## 测试入口

`Assets/Scripts/MxFramework/Tests/Diagnostics/`
