# 运行时垂直切片 01：Playable Attributes / Buffs / Modifiers

> **状态**: ✅ 已完成（详情见 `Docs/CAPABILITIES.md`）

## 目标

做出框架第一个**最小可运行闭环**：一个 Unity Scene、一个运行脚本，把 `Attributes`、`Modifiers`、`Buffs` 串起来，并能在 Play Mode 中直接看到结果。

这一步的目的不是做完整游戏功能，而是证明框架核心模块不是“一堆类”，而是能被游戏层脚本组合成可运行的东西。

## 核心问题

当前框架已经有：

- `AttributeStore`
- `ModifierPipeline`
- `BuffPipeline`
- `BuffBase`
- `IBuffTarget`
- 基础测试

但缺少一个人能直接打开 Unity、点击 Play、看到结果的垂直切片。

本任务完成后，应能回答：

- 游戏层最小脚本如何创建属性容器？
- Modifier 如何改变属性？
- Buff 如何按 tick 推进生命周期？
- Buff 和 Modifier 能否组合使用？
- 当前 HP、Attack、Buff 层数和持续时间如何被观察？

## 非目标

本任务不做：

- 外部 Authoring Editor。
- Runtime Preview Server。
- Mod / Patch 热加载。
- WGame 真实 Buff 数据。
- 测试角色美术、动画、AI、技能、输入。
- 多角色战斗。
- 完整 UI 系统。

如果本任务开始依赖 Authoring Editor、配置 Patch 或真实 WGame 数据，说明范围失控。

## 最小场景

建议新增：

```text
Assets/Scenes/RuntimeVerticalSlice.unity
```

场景对象：

| 对象 | 作用 |
| --- | --- |
| `RuntimeSliceRunner` | 挂载垂直切片运行脚本 |
| `RuntimeSliceTarget` | 可选，可作为测试目标展示位置 |
| `Main Camera` | 观察输出 |
| `Directional Light` | 基础照明 |

首版可以只使用空对象和 IMGUI / Console 输出，不要求美术资源。

## 最小脚本

建议新增：

```text
Assets/Scripts/MxFramework/Demo/RuntimeVerticalSliceRunner.cs
```

职责：

- 在 `Awake` 或 `Start` 中创建一个测试目标。
- 初始化属性：
  - `Hp = 1000`
  - `Attack = 100`
  - `Defense = 20` 或 `MoveSpeed = 100`
- 创建并应用一个 Modifier。
- 创建并应用一个 Buff。
- 在 `Update` 中 tick Buff。
- 在 Game View 或 Console 显示状态。

## 最小运行模型

### 测试目标

脚本内可定义一个私有 `RuntimeSliceTarget`，实现 `IBuffTarget`：

```text
RuntimeSliceTarget
  - AttributeStore
  - EventBus<BuffEvent>
  - BuffPipeline
```

不要一开始抽成复杂角色系统。这个类只服务垂直切片。

### 属性

建议用常量 ID：

| 常量 | ID | 含义 |
| --- | --- | --- |
| `AttrHp` | `1` | HP |
| `AttrAttack` | `2` | Attack |
| `AttrDefense` | `3` | Defense |

### Modifier 示例

至少演示一种 Modifier：

- `Attack + 50`
- 或 `Attack * 1.5`

验收时必须能看到 Modifier 应用前后属性变化，例如：

```text
Attack: 100 -> 150
```

### Buff 示例

至少演示一种 Buff：

建议 `BurningBuff`：

- BuffId：`100001`
- Duration：`5s`
- Tick：每 `1s`
- 效果：`Hp - 35`
- MaxLayer：`3`

验收时必须能看到：

```text
Buff 100001 added
Hp 1000 -> 965 -> 930 ...
Remaining 5.0s -> 0s
Buff expired
```

### Buff + Modifier 组合

建议再演示一个轻量组合：

- `RageBuff`：添加时给 Attack 一个临时 Modifier，移除时清理。

如果首版只做一个 Buff，必须至少保留代码注释或任务 TODO，说明下一步如何验证 Buff 管理 Modifier 生命周期。

## 可视输出

首版至少一种输出方式：

- `OnGUI` 文本面板。
- Console 日志。
- Scene 中 `TextMesh` / UI Text。

推荐首版用 `OnGUI`，因为不需要引入 UI 资源。

显示内容：

```text
Runtime Vertical Slice
Time: 1.25s
Hp: 965 / 1000
Attack: 150
Defense: 20
Buffs:
  100001 Burning stack=1 remaining=3.75s
Events:
  Attribute Hp 1000 -> 965
  Buff 100001 tick 0 damage=35
```

## 操作方式

1. 打开 `Assets/Scenes/RuntimeVerticalSlice.unity`。
2. 点击 Play。
3. 观察 Game View / Console。
4. 等待 5 秒，确认 Buff 到期。
5. 停止 Play。

## 代码边界

必须遵守：

- Demo 代码放在 `Demo` 或明确的示例目录，不进入核心运行时模块。
- 核心模块不反向依赖 Demo。
- Demo 可以引用 `Attributes`、`Buffs`、`Modifiers`。
- 不引入 WGame 业务类型。
- 不要求配置系统参与。

## 验收标准

- Unity 编译无项目 error。
- 场景可打开。
- 点击 Play 后可见状态输出。
- 初始属性正确。
- Modifier 能改变 Attack。
- Buff 能添加到 `BuffPipeline`。
- Buff 每秒 tick 并修改 HP。
- Buff 到期后从 pipeline 移除。
- 如果 Buff 注册了 Modifier，到期时必须清理 Modifier。
- 运行 10 秒无异常。
- 停止 Play 后无持久化脏数据写入。

## 测试建议

### 手动验证

- 使用 Unity MCP 打开或创建场景。
- Play 运行 10 秒。
- 读取 Console，确认没有 error。
- 截图或记录 Game View 输出。

### 自动化

可新增 PlayMode 测试：

```text
RuntimeVerticalSlice_RunsWithoutErrors
RuntimeVerticalSlice_BurningBuffTicksHp
RuntimeVerticalSlice_AttackModifierAppliesAndCleansUp
```

自动化不要求第一版完成，但代码应便于后续提取测试逻辑。

## 文档更新

完成实现后必须更新：

- `Docs/USAGE.md`
- `Docs/ROADMAP.md`
- 必要时更新 `Docs/Interfaces/Attributes.md`
- 必要时更新 `Docs/Interfaces/Buffs.md`
- 必要时更新 `Docs/Interfaces/Modifiers.md`

文档要明确这是框架运行时最小闭环，不是 WGame 业务 Demo。

## 状态

`Implemented (r1140)`

## 优先级

当前优先级高于：

- Buff Authoring Editor 深化。
- Runtime Preview 场景目标。
- Mod 支持。
- AI 辅助闭环。

原因：它先证明框架核心模块能被游戏层脚本实际组合运行。

## 后续衔接

本任务完成后，再进入：

1. Runtime Config 最小切片。
2. Buff Authoring 垂直切片强化。
3. Runtime Preview Scene Target。

