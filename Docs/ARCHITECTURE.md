# MxFramework 架构契约

> 版本 0.3.0 | 2026-05-05
>
> 本文档定义不可随意破坏的架构规则。实现时若需要违反其中任一条，必须先记录原因并更新设计文档。

## 1. 架构目标

MxFramework 的目标不是复刻 WGame，而是提取 WGame 中已经被验证过的通用机制：

- 属性计算：稳定、可追踪、可组合。
- 修改器系统：数据驱动的条件、效果和生命周期。
- Buff 系统：时间、层数、状态和数值影响的通用管线。
- 事件系统：低分配、类型安全、可解除订阅。
- 配置抽象：不绑定 Luban、Json、ScriptableObject 或任何项目私有格式。
- AI 基础设施：提供事实、目标、动作、效果和轻量规划器，不绑定具体游戏语义或插件。

## 2. 分层规则

### 2.1 依赖方向

```text
Core.Pure
  ^
Core.Unity
  ^
Events
  ^
Attributes
  ^
Modifiers  -> Buffs 仅允许通过 IBuffAccess / IBuffPipeline 接口
  ^             ^
  |             |
AI -------------+

Config 独立依赖 Core.Pure，可被上层以接口方式使用。
Editor 只存在于 Editor 程序集，可引用所有 Runtime 程序集。
Samples 可引用所有 Runtime 程序集，但不能被 Runtime 反向引用。
```

### 2.2 Core 拆分原则

`VectorExtensions`、`RandomTable` 使用 `UnityEngine.Vector2/Vector3/Mathf`，因此必须放在 Unity 适配层。当前拆分如下：

| 层 | 允许依赖 | 内容 |
|----|----------|------|
| `MxFramework.Core` | BCL only | 集合、池、位运算、字符串、非 Unity 数学 |
| `MxFramework.Core.Unity` | `UnityEngine` | Vector 扩展、Unity 随机表适配、Unity 类型转换 |

Core 纯净化是 Batch 1 的完成条件之一；完成后 `MxFramework.Core` 必须保持 `noEngineReferences=true`。

### 2.3 禁止依赖

- Runtime 不引用 `UnityEditor`。
- Framework 不引用 WGame 命名空间。
- Framework 内层模块不引用 Entitas、Luban、CrashKonijn、FairyGUI 或项目私有插件；可选 adapter 程序集可以引用对应第三方运行时，但必须保持依赖单向、边界清晰，并在设计文档中记录用途。
- 下层模块不引用上层模块。
- 模块之间不直接 `new` 对方实现类，跨模块实例由工厂、注册表或组合根创建。

## 3. 组合根

框架不拥有全局单例。游戏项目必须在自己的组合根中装配：

```csharp
public sealed class GameFrameworkBootstrap
{
    public IConfigProvider Configs { get; }
    public ITimeProvider Time { get; }
    public IModifierFactory ModifierFactory { get; }
    public IBuffFactory BuffFactory { get; }
}
```

允许框架提供默认实现，但默认实现不得读取游戏配置、场景对象或静态全局状态。Unity 项目可在 `MonoBehaviour` 中持有组合根，服务器或测试可用纯 C# 组合根。

## 4. 运行时生命周期

所有有生命周期的系统统一使用以下阶段：

1. `Create`：构造对象，只保存依赖，不做昂贵初始化。
2. `Initialize`：注册属性、加载配置、建立事件订阅。
3. `Attach`：绑定到目标或上下文。
4. `Tick`：按外部传入时间推进，不自行读取 `Time.deltaTime`。
5. `Detach`：解除目标关系和事件订阅。
6. `Dispose`：释放池、缓存和非托管资源。

生命周期方法必须满足：

- 可重复调用的行为要明确；不支持重复调用时必须抛出可诊断异常。
- `Detach/Dispose` 必须幂等。
- 高频 `Tick` 路径默认不分配托管对象。

## 5. 数据流

### 5.1 属性数据流

```text
Base Value
  -> AttributeModifier list sorted by phase and priority
  -> Final Value cache
  -> AttrChangedEvent(old, new, delta, source)
```

属性系统只负责计算和通知，不负责解释属性含义。`attrId=1001` 是血量还是韧性，由游戏层配置决定。

### 5.2 修改器数据流

```text
Config row / runtime request
  -> IModifierFactory
  -> IModifier instance
  -> ModifierPipeline
  -> Condition evaluate
  -> Effect execute
  -> Attribute/Buff/Event access through interfaces
```

修改器不直接读取 WGame 实体、背包、元素、装备或关卡。需要上下文时放入 `ModifierContext`，并通过显式接口暴露。

### 5.3 Buff 数据流

```text
Add request
  -> IBuffFactory
  -> stacking policy
  -> OnAttach
  -> Tick
  -> expire / remove
  -> OnDetach
```

Buff 的层数、刷新、覆盖、溢出规则必须由 `IBuffStackingPolicy` 或配置声明，不能散落在 Buff 子类中。

## 6. ID 与注册

框架使用 `int` 作为最低层 ID 类型，但必须区分 ID 空间：

| ID 空间 | 示例 | 所属模块 |
|---------|------|----------|
| AttributeId | HP, Attack, MoveSpeed | Attributes |
| ModifierId | Equipment affix, passive rule | Modifiers |
| BuffId | Poison, Shield, Stun | Buffs |
| CounterId | KillCount, HitCount | Modifiers |
| StatusMask | Stun, Silence, Invincible | Buffs |

规范：

- `0` 默认保留为无效 ID，除非模块文档另行声明。
- `-1` 表示 None，不可作为合法配置 ID。
- 不同 ID 空间可以数值重复，但不能混用。
- Editor 必须提供 ID 冲突和未注册引用检查。

## 7. 错误处理

| 场景 | 行为 |
|------|------|
| 配置缺失 | 返回失败结果或抛 `ConfigNotFoundException`，不得静默创建默认配置 |
| 重复注册 | Debug/Editor 下报错；Release 可选择覆盖或忽略，但必须可配置 |
| 生命周期顺序错误 | 抛 `InvalidOperationException`，错误信息包含对象 ID 和当前状态 |
| 非法数值 | Clamp 必须显式声明；禁止隐式吞掉溢出 |
| 事件处理器异常 | 默认向外抛出；如需隔离必须由调用方提供策略 |

## 8. 性能预算

| 场景 | 目标 |
|------|------|
| 每帧 Tick | 0 GC alloc，除非文档声明 |
| 添加/移除 Buff 或 Modifier | 可有少量分配，但应支持对象池 |
| 属性查询 | O(1) 获取缓存最终值 |
| 属性重算 | O(n log n) 以内，n 为该属性修改器数量 |
| 事件发布 | 不复制完整订阅列表，除非防重入策略要求 |

性能测试不追求绝对数字，但必须覆盖“高频 1000 entity / 100 modifier / 100 buff”的压力场景。

## 9. 可观测性

每个核心模块都必须能导出调试快照：

```csharp
public interface IDebugSnapshotProvider<TSnapshot>
{
    TSnapshot CaptureSnapshot();
}
```

快照只用于 Debug/Editor，不进入高频路径。Editor 面板读取快照，而不是直接窥探私有字段。
