# MxFramework API 规范

> Status: Current
>
> 版本 0.3.0 | 2026-05-05
>
> 本文档约束公共 API 的形态，避免框架在迁移过程中变成 WGame 私有实现的复制品。

## 1. API 分级

| 等级 | 含义 | 兼容性要求 |
|------|------|------------|
| Public Contract | 游戏层会直接依赖的接口、事件、数据结构 | 不破坏签名；破坏时升级主版本 |
| Extension Point | 工厂、策略、Provider、Hook | 保持语义稳定，允许新增默认方法或适配器 |
| Default Implementation | 框架提供的默认实现类 | 可优化内部结构，但行为必须兼容 |
| Internal | 模块内部细节 | 可自由调整，不进入文档示例 |

公共 API 必须写入 `INTERFACES.md`，未写入的类型默认视为内部实现。

## 2. 命名规则

- 接口使用 `I` 前缀：`IAttributeOwner`、`IBuffFactory`。
- 策略使用 `Policy` 后缀：`IBuffStackingPolicy`。
- 工厂使用 `Factory` 后缀：`IModifierFactory`。
- Provider 表示外部数据来源：`IConfigProvider`、`ITimeProvider`。
- Pipeline 表示拥有生命周期并管理多个对象：`ModifierPipeline`、`BuffPipeline`。
- Event 使用过去式或名词：`AttributeChangedEvent`、`BuffLayerChangedEvent`。
- Snapshot 只用于调试：`AttributeDebugSnapshot`。

禁止把游戏概念写进框架公共类型名，例如 `Player`、`Monster`、`Element`、`WeaponEntry`、`Roguelike`。

## 3. 参数和返回值

### 3.1 高频路径

高频路径优先使用：

- `int`、`float`、`bool`、`struct`。
- `ReadOnlySpan<T>` 或预分配集合，前提是 Unity/.NET 版本支持。
- `IReadOnlyList<T>` 用于只读结果。
- `TryGet` 模式避免异常作为流程控制。

示例：

```csharp
bool TryGetAttribute(int attrId, out int finalValue);
bool TryGetBuff(int buffId, out IBuff buff);
```

### 3.2 低频路径

状态哈希新接口必须使用预定义的整数 Key、schema／contributor ID 和固定状态枚举。编号在源码或制作期目录显式固定，禁止按运行时名称哈希、注册顺序或字符串解析临时得到定义 ID；正常 Tick 不执行字符串到身份的转换。内容目录只能在制作阶段分配整数，Runtime 只验证并读取。显示文字与自由文本内容不作为定义身份。当前整数哈希迁移范围与尚未迁移的旧接口见 `Docs/Interfaces/Runtime.md`，不能以新接口已存在代替调用链迁移验收。

配置加载、Editor、调试导出可以使用：

- `string`
- `Dictionary<TKey, TValue>`
- `IEnumerable<T>`
- 异常和详细错误对象

低频 API 不能被 Tick 路径调用，除非文档显式说明会分配。

## 4. 结果类型

对可恢复失败，优先使用结果类型而非布尔值加日志：

```csharp
public readonly struct MxResult
{
    public bool Success { get; }
    public int ErrorCode { get; }
    public string Message { get; }
}
```

`Message` 允许在失败时分配；成功路径不应创建新字符串。

## 5. 事件规范

- 事件 payload 默认使用 `readonly struct`。
- payload 字段名必须可诊断：包含 ID、旧值、新值、来源或原因。
- 订阅返回 `IDisposable` 优先于单独 `Unsubscribe`，便于生命周期管理。
- 支持显式 `Unsubscribe` 作为兼容 API。
- 发布事件时不允许修改订阅集合导致枚举崩溃；实现必须定义防重入策略。

推荐接口：

```csharp
public interface IEventBus<T> where T : struct
{
    IDisposable Subscribe(Action<T> handler);
    bool Unsubscribe(Action<T> handler);
    void Publish(in T args);
}
```

## 6. Unity 依赖规则

| 类型 | Runtime 是否允许 | 说明 |
|------|------------------|------|
| `UnityEngine.Vector2/Vector3` | 仅 `Core.Unity` 及上层允许 | 不能污染纯 Core |
| `MonoBehaviour` | 默认实现中谨慎允许 | 接口不得要求继承它 |
| `ScriptableObject` | Config 适配层允许 | 不作为唯一配置形态 |
| `UnityEditor` | 只允许 Editor 程序集 | Runtime 禁止 |
| `Time.deltaTime` | 禁止在框架内部读取 | 时间由调用方传入 |

## 7. GC 规则

每个公共方法必须能被归类为：

- `NoAlloc`：正常执行不产生托管分配。
- `LowFreqAlloc`：低频路径允许分配。
- `AllocByDesign`：明确返回新集合、字符串或快照。

文档和 XML 注释中应标记高频 API：

```csharp
/// <remarks>NoAlloc after initialization.</remarks>
void Tick(float deltaTime);
```

`Tools/Harness/check_code_standards.py` provides a report-only scanner for likely violations in first-party C# code. It does not replace review or profiling, but C# changes should run it or explain why it was not run. Existing findings are treated as baseline debt until explicitly cleaned up; new hot-path code should not add findings without documenting the allocation or lookup boundary.

## 8. Unsafe / pointer policy

`unsafe` is not the default answer to performance problems. Prefer this order:

1. Better algorithms and data layout.
2. `NoAlloc` APIs and caller-owned buffers.
3. Preallocated collections, `Span<T>` / `ReadOnlySpan<T>`, and small `struct` values.
4. Burst / Jobs / `NativeArray<T>` in Unity-facing adapter layers when that model fits.
5. Narrow `unsafe` kernels only after the previous options are insufficient.

Allowed `unsafe` use is limited to internal hot kernels with measured or reviewable evidence, such as byte copy, fixed-format serialization, hashing/checksums, low-level string utilities, or very small math / collision inner loops. Gameplay rules, Buff / Attribute / Events business semantics, UI, Editor tooling, and Config parsing must stay in safe C# unless an approved module document records the exception.

Public contracts must not expose pointer types, `unsafe` methods, or caller-owned raw memory. Wrap any unsafe implementation behind safe APIs such as `Span<T>`, `ReadOnlySpan<T>`, arrays, stable struct ids, or explicit buffer writer abstractions, and document the allocation behavior in XML comments and the relevant interface document.

Adding `unsafe` requires documenting the bottleneck evidence, rejected safe alternatives, asmdef `allowUnsafeCode` scope, IL2CPP / platform risks, and test coverage. Existing low-level unsafe code is treated as baseline debt until a task explicitly cleans it up.

`Tools/Harness/check_code_standards.py` reports unsafe constructs and first-party `allowUnsafeCode` changes. The report is advisory unless `--fail-on-findings` or `CODE_STANDARDS_FAIL_ON_FINDINGS=1` is used.

## 9. 兼容性策略

版本采用语义化规则：

- `0.x`：接口仍可调整，但必须记录迁移说明。
- `1.x`：公共接口稳定，破坏性调整需要适配层。
- 新增 API 优先不破坏旧接口。
- 废弃 API 使用 `[Obsolete("Use X instead.")]`，至少保留一个小版本。

## 10. 文档到代码的同步要求

实现公共类型时必须同步：

- `INTERFACES.md`：接口签名和语义。
- `MIGRATION.md`：如果来自 WGame，记录来源和适配。
- `QUALITY_GATE.md`：新增必要测试项。
- XML 注释：公共接口、事件、异常、分配行为。
