# Resource Management M6B: Variant Catalog + Retain Policy

> 状态：Implemented
> 日期：2026-05-10
> 优先级：P1
> 前置任务：Resource Management M6A Preload Group + Scene Warmup

## 结论

M6B 做两件互相关联但仍要保持分层的事：

- Variant Catalog：解析策略。
- Retain Policy：释放策略。

Variant 不属于 Provider；Provider 只加载已经解析出的 entry。

RetainPolicy 也不属于 Provider；它决定 handle release 后底层 loaded record 是否立即卸载。

## Variant Catalog

`ResourceKey.Variant` 已经存在。M6B 需要补的是显式 fallback profile，而不是把平台、语言、画质写进 `Id`。

不推荐：

```text
demo.icon.fire_burst.pc.high
demo.icon.fire_burst.mobile.low
```

推荐：

```text
id = demo.icon.fire_burst
variant = pc.high
```

已实现 API：

```csharp
public sealed class ResourceVariantProfile
{
    public string ActiveVariant { get; }
    public IReadOnlyList<string> FallbackVariants { get; }
}
```

`ResourceManager.SetVariantProfile(profile)` 设置当前解析 profile，不修改 `IResourceManager` 签名。

示例解析顺序：

```text
pc.high
pc
high
default
empty
```

顺序必须由 profile 显式配置，不由 Provider 猜测。

实际规则：

- `ResourceKey.Variant` 非空时，先尝试请求中的 variant，再按 `FallbackVariants` 走。
- `ResourceKey.Variant` 为空时，先尝试 `ActiveVariant`，再按 `FallbackVariants` 走。
- fallback 列表不自动追加空 variant；如果要回退默认资源，必须显式把 `string.Empty` 放入 fallback。
- `PackageId` 非空时仍只在指定包内解析，不走全局覆盖结果。
- `FindKeysByLabel(label)` 返回去除具体 variant 后的逻辑 key，并按 `id + type` 去重；label warmup 不会把同一资源的所有 variant 一起加载，实际 variant 仍由当前 `ResourceVariantProfile` 决定。

## Retain Policy

RetainPolicy 解决 asset churn：资源引用计数归零后马上又被加载，导致频繁卸载 / 重载。

第一版只做三个模式：

```csharp
public enum ResourceRetainMode
{
    None,
    Timed,
    KeepAlive
}
```

已实现 API：

```csharp
public sealed class ResourceRetainPolicy
{
    public ResourceRetainMode Mode { get; }
    public float DurationSeconds { get; }
    public int FrameCount { get; }

    public static ResourceRetainPolicy None { get; }
    public static ResourceRetainPolicy KeepAlive { get; }
    public static ResourceRetainPolicy Timed(float durationSeconds = 0f, int frameCount = 0);
}
```

`ResourceManager.SetRetainPolicy(policy)` 设置全局 retain 策略。`Timed` 在 noEngine 层不依赖 Unity 时间，需要组合根或测试显式调用：

```csharp
resourceManager.AdvanceRetainTime(deltaSeconds);
resourceManager.AdvanceRetainFrames(frameCount);
resourceManager.EvictRetainedResources();
```

语义：

- `None`：引用计数归零立即释放。
- `Timed`：引用计数归零后保留 N 秒或 N 帧。
- `KeepAlive`：常驻直到显式清理或 ResourceManager shutdown。

后续再扩展：

- `Budgeted`
- `Scene`
- `Package`

## 关键规则

- RetainPolicy 不改变 handle 语义。
- 调用方 `Release(handle)` 后，handle 仍进入 Released 状态。
- 被 retain 的是底层 loaded record，不是外部 handle。
- Diagnostics 必须显示 retained / evictable 状态，避免变成“永不释放”。

## Diagnostics

`ResourceDebugSnapshot` 已新增：

```text
RetainedCount
EvictableCount
PinnedCount
RetainPolicyCount
RecentEvictions
```

这些是新增字段，不破坏 M1-M5 既有消费者。

## 测试

已覆盖 Variant：

- 精确 variant 命中。
- fallback 命中。
- 无 fallback 时 NotFound。
- PackageId 精确路由仍优先。
- Provider 不感知 fallback。

已覆盖 Retain：

- `None` 保持现有 ref-count 归零释放行为。
- `Timed` 在保留窗口内重复加载复用 cached record。
- `KeepAlive` 释放 handle 后仍保留底层记录。
- 显式清理能释放 retained record。
- Diagnostics 能看到 retained / evictable count。

已覆盖 M6A + M6B 组合：

- label warmup 遇到同一资源的多条 variant entry 时只请求 1 个逻辑 key。
- active variant 命中后，group release 进入 timed retain，不立即调用 Provider release。
- retain 窗口内再次 warmup 复用 cached record，不重复加载。
- retain 窗口到期后手动推进 frame 能释放底层 record。

验证记录：

```text
dotnet build MxFramework.Tests.csproj --no-restore
Unity EditMode: MxFramework.Tests.Resources.ResourcePreloadServiceTests, 6/6 passed
Unity EditMode: MxFramework.Tests.Resources, 41/41 passed
Unity Console errors after run: 0
```

## 不做范围

- 不做平台自动检测。
- 不把语言 / 平台 / 画质写死进框架。
- 不做内存预算驱逐。
- 不做 Addressables profile 映射。
- 不做 RemoteBundle 版本选择。
