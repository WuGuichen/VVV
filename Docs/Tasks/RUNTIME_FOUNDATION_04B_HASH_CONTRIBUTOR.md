# Runtime Foundation 04B：Runtime Hash Contributor

> 状态：Completed / Verified 2026-05-10
> 日期：2026-05-10
> 优先级：P0
> 父任务：`RUNTIME_FOUNDATION_04_V1_PARALLEL_CLOSEOUT.md`

## 目标

建立通用 result hash contract，让 Ability、Gameplay、Combat、Preview 或测试模块都能按稳定顺序贡献运行时状态 hash。04B 首轮只负责 Runtime noEngine 层的契约、accumulator 和确定性测试；业务模块 adapter 后续在各模块任务中接入。

## 建议写入范围

- `Assets/Scripts/MxFramework/Runtime/RuntimeHash*.cs`
- `Assets/Scripts/MxFramework/Tests/Runtime/RuntimeHashContributorTests.cs`
- 必要时新增对应 `.meta`

不要修改 Ability、Combat、Preview、Demo 或 UI 文件。

## 建议 API

```csharp
public readonly struct RuntimeHashContext
{
    public RuntimeFrame Frame { get; }
}

public interface IRuntimeHashContributor
{
    string ContributorId { get; }
    void Contribute(RuntimeHashContext context, RuntimeHashAccumulator accumulator);
}

public sealed class RuntimeHashAccumulator
{
    public void AddInt(string key, int value);
    public void AddLong(string key, long value);
    public void AddDoubleQuantized(string key, double value, double scale);
    public void AddStringStable(string key, string value);
    public long ToHash();
}
```

可以按现有代码风格调整命名，但必须保留这些语义：

- contributor 有稳定 ID。
- contributor 先按 ID 排序后执行。
- accumulator 明确写入 key 和 value，避免不同字段碰撞。
- double 必须量化，不能直接依赖平台浮点字符串格式。

## Hash 输入禁区

禁止把以下内容作为默认 hash 输入：

- 对象地址或 `GetHashCode()` 的默认对象实现。
- Dictionary / HashSet 原始迭代顺序。
- 本地化文本。
- Unity 对象实例 ID。
- 当前系统时间、随机种子外的随机值。

## 测试

至少覆盖：

- contributor 注册顺序不同，最终 hash 相同。
- 字段顺序固定时 hash 稳定。
- 不同 frame 或不同值 hash 不同。
- double 量化行为稳定。
- null / empty string 处理稳定。

## 验收

- `MxFramework.Runtime` 仍保持 `noEngineReferences=true`。
- Runtime hash contract 不依赖任何具体玩法模块。
- 后续 04A playback 可以直接使用该 hash 结果比较 expected / actual。

## 2026-05-10 实现记录

- 新增 `RuntimeHash.cs`，包含 `RuntimeHashContext`、`IRuntimeHashContributor`、`RuntimeHashCombiner` 和 `RuntimeHashAccumulator`。
- Contributor 按 `ContributorId` 排序并拒绝重复 ID；accumulator 使用显式 key/value 输入。
- Double hash 输入通过量化处理，避免直接依赖平台浮点字符串格式。
- 新增 `RuntimeHashContributorTests.cs` 覆盖 contributor 顺序稳定、固定字段 golden hash、frame/value 差异、double 量化、null/empty string 和重复 contributor id。
- 验证：Runtime / Tests dotnet build 通过；Unity EditMode `RuntimeHashContributorTests` 6/6 passed；Runtime 相关 EditMode 29/29 completed。
- `MxFramework.Runtime.asmdef` 继续保持 `noEngineReferences=true`。
