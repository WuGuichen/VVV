# Attributes 接口

## 职责

Attributes 管理通用属性 base/final value 和属性修改器链，不定义具体属性含义。

## 公开接口

| 接口/类型 | 用途 |
|-----------|------|
| `IAttributeOwner` | 读取、注册、修改属性 |
| `IAttributeModifierOwner` | 添加、移除、清空属性修改器 |
| `IAttributeModifier` | 修改单个属性值 |
| `AttributeStore` | 默认属性存储和修改器执行实现 |
| `AttributeChangedEvent` | 属性变化事件 |
| `AttributeModifierEvent` | 修改器增删事件 |

## 使用约定

- 游戏层定义属性 ID，框架不解释 ID 含义。
- `GetAttribute` 对不存在属性返回 `0`；需要区分缺失时用 `TryGetAttribute`。
- 修改器按 `Phase`、`Priority`、`Id` 升序执行。
- 相同 modifier id 后添加者替换旧 modifier。
- 修改器不得直接读取 WGame 实体或全局单例。

## 最小示例

见 `Docs/USAGE.md` 的 Attributes 章节。

## 测试入口

`Assets/Scripts/MxFramework/Tests/Attributes/AttributeStoreTests.cs`
