# Runtime Random 01：DeterministicRandom

> 状态：Planned
> 日期：2026-05-11
> 优先级：P0
> 父任务：`CORE_RUNTIME_UTILITIES_01.md`

## 目标

新增 noEngine 确定性随机，作为 Gameplay、Combat、AI、掉落、关卡扰动、Replay 和 SaveState 的权威随机来源。

```csharp
public interface IDeterministicRandom
{
    int NextInt(int minInclusive, int maxExclusive);
    float NextFloat01();
    bool Chance(float probability);
    void Reset(uint seed);
    RuntimeRandomState CaptureState();
    void RestoreState(RuntimeRandomState state);
}
```

UnityEngine.Random 只能用于表现层，不能作为权威 gameplay random。

## 范围

### 做

- 新增 `Assets/Scripts/MxFramework/Runtime/Random/`。
- 提供一个固定算法实现，例如 `XorShift` / `PCG32`，算法必须在文档中固定。
- `RuntimeRandomState` 可序列化，包含 algorithm id、seed/state、draw count。
- 支持 SaveState capture / restore。
- 支持 deterministic tests。
- 更新 `Docs/Interfaces/Runtime.md`。

### 不做

- 不依赖 `System.Random` 作为长期公共契约。
- 不依赖 UnityEngine.Random。
- 不做加密随机。
- 不在全局静态状态里保存默认随机源。

## 规则

- 同一 seed、同一调用序列必须输出相同结果。
- `NextInt(min, max)` 使用 `[minInclusive, maxExclusive)`。
- `Chance(0)` 必须为 false；`Chance(1)` 必须为 true。
- 非法 probability / range 返回明确异常。
- Restore 后后续序列必须与 capture 时继续抽取一致。

## 测试

新增测试建议：

```text
Assets/Scripts/MxFramework/Tests/Runtime/RuntimeDeterministicRandomTests.cs
```

覆盖：

- 相同 seed 序列一致。
- 不同 seed 序列不同。
- `NextInt` 边界和非法参数。
- `NextFloat01` 范围。
- `Chance` 边界。
- Capture / Restore 后序列继续一致。
- SaveState JSON roundtrip 保持 random state。

## 验收

- Runtime 提供 noEngine deterministic random 契约和实现。
- Demo / Gameplay 后续随机逻辑不需要 Unity random。
- Replay / SaveState 可以恢复随机序列。

