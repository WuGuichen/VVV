# Modifiers 接口

## 职责

Modifiers 将条件、效果、Counter 和属性/Buff 访问组合成通用修改器管线。

## 公开接口

| 接口/类型 | 用途 |
|-----------|------|
| `IModifier` | 修改器生命周期 |
| `IModifierCondition` | 条件判断 |
| `IModifierEffect` | 效果执行 |
| `IModifierFactory` | 根据 modifierId 创建 Modifier |
| `IModifierPipeline` | 管理修改器 |
| `ICounterStore` | 通用计数器 |
| `ModifierContext` | 显式上下文 |
| `ModifierBase` | 默认条件/效果组合实现 |
| `ModifierSnapshot` | 运行时只读快照 |

## 使用约定

- `ModifierContext` 必须显式传入，不读取全局单例。
- `ApplyAll` 和 `UpdateAll` 会补齐 Target、Buffs、Counters。
- `CounterStore` 缺失计数默认返回 `0`；需要区分缺失时用 `TryGetCounter`。
- 不包含 WGame 元素、伤害、装备词条文案或具体 Counter ID。
- 配置驱动创建通过 `MxFramework.Config.Runtime.ConfigModifierFactory<TConfig>`。

## 最小示例

见 `Docs/USAGE.md` 的 ModifierPipeline 和配置驱动 Modifier 章节。

## 测试入口

`Assets/Scripts/MxFramework/Tests/Modifiers/`
