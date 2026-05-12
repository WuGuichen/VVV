# MxFramework API 规范

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

## 8. 兼容性策略

版本采用语义化规则：

- `0.x`：接口仍可调整，但必须记录迁移说明。
- `1.x`：公共接口稳定，破坏性调整需要适配层。
- 新增 API 优先不破坏旧接口。
- 废弃 API 使用 `[Obsolete("Use X instead.")]`，至少保留一个小版本。

## 9. 文档到代码的同步要求

实现公共类型时必须同步：

- `INTERFACES.md`：接口签名和语义。
- `MIGRATION.md`：如果来自 WGame，记录来源和适配。
- `QUALITY_GATE.md`：新增必要测试项。
- XML 注释：公共接口、事件、异常、分配行为。

