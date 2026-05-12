# Runtime Quality Utilities 01：Cooldown / Operation / Versioning / Command Registry

> 状态：Planned
> 日期：2026-05-11
> 优先级：P1
> 前置：`RUNTIME_FOUNDATION_04_TIMER_SCHEDULER.md`、`RUNTIME_RANDOM_01_DETERMINISTIC_RANDOM.md`

## 目标

在核心工具 Batch A 之后，补齐玩法表达和运行时质量工具：

- `CooldownTracker`
- `DirtyFlag` / `VersionedValue`
- `RuntimeOperation`
- `RateLimiter` / `Debouncer`
- `RuntimeCommandRegistry`

这些工具统一 cooldown、增量刷新、进度/失败/超时、限频和 command 调试信息，减少 UI、Diagnostics、SceneFlow、Resources、Audio、Authoring Preview 中的重复逻辑。

## 工具范围

### CooldownTracker

```csharp
public sealed class CooldownTracker
{
    public bool IsReady(int id, RuntimeFrame frame);
    public void Start(int id, RuntimeFrame frame, long durationFrames);
    public long GetRemainingFrames(int id, RuntimeFrame frame);
    public bool TryConsume(int id, RuntimeFrame frame, long durationFrames);
    public bool Remove(int id);
    public int CleanupExpired(RuntimeFrame frame);
    public CooldownTrackerSnapshot CreateSnapshot(RuntimeFrame frame, bool includeExpired = false);
}
```

用途：Ability cooldown、交互冷却、UI 按钮冷却、音效播放限频、Combat attack interval、AI 决策间隔。

规则：基于 `RuntimeFrame`，不读取 `Time.time`；一次性 id 较多时调用 `CleanupExpired` 显式清理过期条目。

### DirtyFlag / VersionedValue

```csharp
public readonly struct VersionToken
{
    public int Version { get; }
}

public sealed class DirtyFlag
{
    public bool IsDirty { get; }
    public int Version { get; }
    public void MarkDirty();
    public bool Consume();
}

public sealed class VersionedValue<T>
{
    public T Value { get; }
    public int Version { get; }
    public bool Set(T value);
}
```

用途：UI 增量刷新、Diagnostics snapshot 缓存、Resource catalog / config change 检测、GameplayWorld snapshot 缓存、Combat debug view。

### RuntimeOperation

```csharp
public enum RuntimeOperationStatus
{
    Pending,
    Running,
    Succeeded,
    Failed,
    Cancelled,
    TimedOut
}

public interface IRuntimeOperation
{
    string OperationId { get; }
    RuntimeOperationStatus Status { get; }
    float Progress { get; }
    RuntimeOperationError Error { get; }
}
```

用途：SceneFlow、Resource warmup、Remote bundle download、Audio bank load、Authoring preview request、Mod package diagnosis、Editor server request。

资源系统已有 `IResourceOperation<T>`，本任务提供更通用的 operation 状态模型；资源模块可后续适配，不强制替换。

### RateLimiter / Debouncer

用途：输入、日志、按钮、调试刷新限频。应支持 frame-based 和 explicit seconds-based 两种模式，不读取 Unity time；seconds 参数必须 finite 且非负，拒绝 NaN / Infinity / negative。

### RuntimeCommandRegistry

用途：RuntimeCommand ID 注册、名称、payload schema、调试显示、Replay diagnostics。

不改变 `RuntimeCommand` v0 结构；先作为可选 validator / diagnostics registry 接入。

## 不做

- 不做线程 / Task 调度框架。
- 不把 cooldown 替代 TimerScheduler。
- 不把 RuntimeOperation 绑定到资源系统。
- 不要求所有 command 必须注册才能入队；严格模式由 validator 开关控制。

## 测试

新增测试建议：

```text
Assets/Scripts/MxFramework/Tests/Runtime/RuntimeCooldownTrackerTests.cs
Assets/Scripts/MxFramework/Tests/Runtime/RuntimeVersioningTests.cs
Assets/Scripts/MxFramework/Tests/Runtime/RuntimeOperationTests.cs
Assets/Scripts/MxFramework/Tests/Runtime/RuntimeRateLimiterTests.cs
Assets/Scripts/MxFramework/Tests/Runtime/RuntimeCommandRegistryTests.cs
```

覆盖：

- Cooldown start / remaining / consume / ready。
- DirtyFlag version 增长和 consume 语义。
- VersionedValue 相等值不递增，不同值递增。
- Operation 状态转移合法性、错误保留、progress 边界。
- RateLimiter / Debouncer 在固定 frame / delta 下稳定。
- CommandRegistry 注册、重复 id、名称 lookup、payload schema summary。

## 验收

- Batch B 工具有明确模块边界和测试。
- UI / Diagnostics 不再需要每帧无条件 rebuild 的手写 dirty 判断。
- SceneFlow / Resources / Audio 后续能共享 operation 状态模型。
- RuntimeCommand 调试显示可以从 registry 获得名称和 schema。

## 2026-05-11 实现记录

- 已实现 `CooldownTracker`、`CooldownTrackerSnapshot` 和 `CooldownSnapshotEntry`，使用 `RuntimeFrame`，不读取 Unity time，并支持 `Remove`、`CleanupExpired` 和按当前帧过滤 snapshot。
- 已实现 `VersionToken`、`DirtyFlag`、`VersionedValue<T>`。
- 已实现 `RuntimeOperationStatus`、`RuntimeOperationError`、`IRuntimeOperation`、`RuntimeOperation`。
- 已实现 `RuntimeRateLimiter` 和 `RuntimeDebouncer`，支持 frame 和显式 seconds 模式，并拒绝 NaN / Infinity / negative seconds。
- 已实现 `RuntimeCommandDefinition`、`RuntimeCommandPayloadSchema`、`RuntimeCommandRegistry`、`RuntimeCommandRegistrySnapshot`、`RuntimeCommandRegistryValidator`。
- 新增 Batch B 测试：`RuntimeCooldownTrackerTests.cs`、`RuntimeVersioningTests.cs`、`RuntimeOperationTests.cs`、`RuntimeRateLimiterTests.cs`、`RuntimeCommandRegistryTests.cs`。
- 验证：A+B 临时源码级 `dotnet test` 通过，`0` 失败、`109` 通过。Unity EditMode / 生成的 `.csproj` 需要 Unity 刷新新文件后再跑。
