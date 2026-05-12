# Buffs 接口

## 职责

Buffs 管理 Buff 生命周期、层数、持续时间、快照和事件，不包含具体业务 Buff。

## 公开接口

| 接口/类型 | 用途 |
|-----------|------|
| `IBuff` | Buff 生命周期和状态 |
| `IBuffTarget` | 可接收 Buff 的目标 |
| `IBuffFactory` | 根据 buffId 创建 Buff |
| `IBuffPipeline` | 管理目标上的 Buff |
| `IBuffStackingPolicy` | 层数、刷新、溢出策略 |
| `BuffBase` | 默认 Buff 基类 |
| `BuffPipeline` | 默认 Pipeline 实现 |
| `BuffSnapshot` | 运行时只读快照 |

## 使用约定

- `TickAll(deltaTime)` 由外部传入时间，不读取 Unity `Time.deltaTime`。
- `OnDetach` 必须清理 Buff 自己注册的属性修改器和事件订阅。
- 同 ID Buff 的重复添加由 `IBuffStackingPolicy` 决定。
- 配置驱动创建通过 `MxFramework.Config.Runtime.ConfigBuffFactory<TConfig>`。

## 最小示例

见 `Docs/USAGE.md` 的 BuffPipeline 和配置驱动 Buff 章节。

## 测试入口

`Assets/Scripts/MxFramework/Tests/Buffs/BuffPipelineTests.cs`
