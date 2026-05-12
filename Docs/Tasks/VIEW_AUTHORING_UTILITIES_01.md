# View / Authoring Utilities 01：Local State / Context / Tween / Diff

> 状态：Planned
> 日期：2026-05-11
> 优先级：P2
> 前置：`CORE_RUNTIME_UTILITIES_01.md`、`RUNTIME_QUALITY_UTILITIES_01.md`

## 目标

补充 View、Authoring、Diagnostics 和局部玩法表达工具：

- Local `RuntimeStateMachine<TState>`
- `RuntimeContextMap` / typed blackboard
- UI / View `Tween` / `Interpolator`
- `SnapshotDiff` / `ChangeSet`

这些工具后置实现，避免在 Timer、Pool、Random、EventQueue 等基础设施未稳定前引入过多抽象。

## 工具范围

### Local StateMachine

```csharp
public sealed class RuntimeStateMachine<TState>
{
    public TState Current { get; }
    public bool TryTransition(TState next, string reason);
}
```

用途：Ability cast phase、Combat action phase、AI local behavior state、UI panel state、Tutorial step、Interaction state。

边界：不替代 `AppFlow` / `SceneFlow`；只做局部状态机。

### ContextMap / Blackboard

```csharp
public readonly struct ContextKey<T>
{
    public string Id { get; }
}

public sealed class RuntimeContextMap
{
    public void Set<T>(ContextKey<T> key, T value);
    public bool TryGet<T>(ContextKey<T> key, out T value);
}
```

用途：Ability execution context、AI world blackboard、Combat query temporary context、Authoring preview context。

边界：禁止裸 `Dictionary<string, object>` 到处传；key 必须 typed，可诊断。

### Tween / Interpolator

用途：UI 数字滚动、血条过渡、Toast fade、Combat hit marker 动画、Marble ball view 插值、Scene transition 表现。

边界：只服务 UI / View / 表现层，不参与权威 runtime hash。

推荐位置：

- `MxFramework.UI.Toolkit`
- `MxFramework.Core.Unity`

不要放进 noEngine Gameplay 权威逻辑。

### SnapshotDiff / ChangeSet

用途：Diagnostics、UI、SaveState、Replay 的差异比较和变更显示。

要求：先支持简单 key/value / list summary diff，不做大型对象图 diff。

## 不做

- 不替代 AppFlow / SceneFlow。
- 不做万能黑板。
- 不让 Tween 参与 gameplay 权威状态。
- 不做复杂 JSON patch / CRDT / 资源 merge。

## 测试

覆盖：

- StateMachine 合法 / 非法 transition。
- ContextMap typed key set / get / missing / type mismatch。
- Tween 在固定 delta 下输出稳定，且不接入 runtime hash。
- SnapshotDiff 对新增、删除、修改字段输出稳定 change set。

## 验收

- P2 工具有明确 View / Authoring / Diagnostics 边界。
- 不污染 noEngine Runtime 权威逻辑。
- 至少一个 UI 或 Diagnostics demo 复用其中一个工具后再标记实现完成。

## 2026-05-11 实现记录

- 已实现 `RuntimeStateMachine<TState>` 和 `RuntimeStateTransitionPredicate<TState>`，作为局部状态机，不依赖 AppFlow / SceneFlow。
- 已实现 `ContextKey<T>` 和 `RuntimeContextMap`，以 typed key 访问上下文，并提供 snapshot summary。
- 已实现 `RuntimeEasing`、`RuntimeEasingFunctions`、`RuntimeFloatInterpolator`、`RuntimeTween`，全部使用显式 delta，不引用 UnityEngine；Tween / interpolation 只用于 view、UI、diagnostics presentation，不进入 replay/hash 权威状态，除非调用方把结果记录为确定性输入。
- 已实现 `RuntimeSnapshotValue`、`RuntimeChangeKind`、`RuntimeChange`、`RuntimeChangeSet`、`RuntimeSnapshotDiff`，支持简单 key/value diff。
- 新增 Batch C 测试：`RuntimeStateMachineTests.cs`、`RuntimeContextMapTests.cs`、`RuntimeInterpolationTests.cs`、`RuntimeSnapshotDiffTests.cs`。
- 验证：A+B+C 临时源码级 `dotnet test` 通过，`0` 失败、`139` 通过。Unity EditMode / 生成的 `.csproj` 需要 Unity 刷新新文件后再跑。
