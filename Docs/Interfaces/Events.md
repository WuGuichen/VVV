# Events 接口

## 职责

Events 提供同步、类型安全、可解除订阅的事件总线。

## 公开接口

```csharp
public interface IEventBus<T> where T : struct
{
    IDisposable Subscribe(Action<T> handler);
    bool Unsubscribe(Action<T> handler);
    void Publish(in T args);
}
```

默认实现：`EventBus<T>`。

## 使用约定

- 事件 payload 使用 struct。
- `Subscribe` 返回 `IDisposable`，调用 `Dispose` 解除订阅。
- `Publish` 同步执行，handler 异常会直接向外传播。
- 发布过程中新增订阅从下一次发布开始生效。
- 高频路径不要反复订阅和取消。

## 最小示例

见 `Docs/USAGE.md` 的 Events 章节。

## 测试入口

`Assets/Scripts/MxFramework/Tests/Events/EventBusTests.cs`
