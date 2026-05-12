# GAMEPLAY_ECS_STYLE_08_COMPONENT_WORLD_DIAGNOSTICS

## 目标

给 `GameplayComponentWorld` 增加第一版 diagnostics snapshot，让新 component runtime 可以被 UI、Editor、测试和 agent 稳定观察。

本批次只做结构摘要，不保存泛型 component value，也不做 replay hash / SaveState。泛型 component value 的权威保存需要后续 registry/schema 明确后再做。

## 新增 API

```csharp
public readonly struct GameplayComponentStoreDiagnosticSnapshot
{
    public string ComponentTypeName { get; }
    public int ComponentCount { get; }
}

public sealed class GameplayComponentWorldDiagnosticSnapshot
{
    public IReadOnlyList<GameplayEntityId> Entities { get; }
    public IReadOnlyList<GameplayComponentStoreDiagnosticSnapshot> Stores { get; }
    public RuntimeEventQueueSnapshot EventQueue { get; }
    public int AliveEntityCount { get; }
    public int ComponentStoreCount { get; }
    public int PendingEventCount { get; }
}

public sealed class GameplayComponentWorldDiagnostics
{
    public GameplayComponentWorldDiagnosticSnapshot BuildSnapshot(GameplayComponentWorld world);
}
```

`GameplayComponentWorld` 新增：

```csharp
public GameplayComponentWorldDiagnosticSnapshot CreateDiagnosticSnapshot();
```

`GameplayComponentRegistry` 新增：

```csharp
public int CopyStoreDiagnostics(List<GameplayComponentStoreDiagnosticSnapshot> output);
```

## 语义

- Entity 列表沿用 `GameplayEntityLifecycle.CreateSnapshot()` 的稳定顺序。
- Store diagnostics 按 component type full name 稳定排序，不依赖注册顺序。
- Event queue 只暴露 `RuntimeEventQueueSnapshot`，不 drain pending events。
- Snapshot 构造时复制 entity / store 列表，后续 world 变化不影响已创建 snapshot。
- 本批次不保存 component value，不定义 component save/hash schema。

## 验收

- Diagnostics snapshot 捕获 alive entity ids、store type/count、pending event queue summary。
- Store diagnostics 输出顺序稳定。
- Diagnostics snapshot 不受后续 world mutation 影响。
- Null world 会抛 `ArgumentNullException`。
