# MxFramework Source Agent Guide

本层约束 `Assets/Scripts/MxFramework/` 下的框架源码。这里的代码要保持通用、可测试、可被不同游戏项目复用。

## 模块依赖方向

```text
Core
  <- Events
  <- Attributes
  <- Modifiers
  <- Buffs
  <- Config
  <- AI
  <- Diagnostics
  <- Runtime
  <- Gameplay

Core.Unity 只做 Unity 适配
Config.Runtime 连接 Config 与运行时工厂
Runtime 提供 noEngine Host / lifecycle / tick 调度，不反向依赖 Gameplay / Combat / Preview
Gameplay 连接实体、技能、目标选择和效果执行
Preview.Runtime 连接运行时预览协议与框架模块
Editor / Demo / Tests 位于最外层
```

规则：

- 内层模块不能依赖外层模块。
- `Core`、`Events`、`Attributes`、`Modifiers`、`Buffs`、`Config`、`AI`、`Diagnostics`、`Gameplay` 默认保持 `noEngineReferences=true`。
- `Core.Unity` 可以依赖 UnityEngine，但不能把 Unity 类型泄漏回 `Core`。
- `Editor` 可以组合 Runtime 模块，但 Runtime 不能引用 Editor。
- `Demo` 只展示框架用法，不承载框架核心逻辑。

## 模块职责

| 路径 | 职责 |
| --- | --- |
| `Core/` | 通用集合、数学、池化、扩展方法 |
| `Core.Unity/` | Unity 类型适配 |
| `Events/` | 轻量事件总线 |
| `Attributes/` | 属性存储、读取和变更 |
| `Modifiers/` | 修改器计算、生命周期、计数器 |
| `Buffs/` | Buff 添加、层数、tick、到期、事件 |
| `Config/` | 配置 Schema、引用校验、运行时数据契约 |
| `Config.Runtime/` | 配置到运行时对象的桥接 |
| `Diagnostics/` | 运行时可观测快照 |
| `Runtime/` | Runtime Host、模块生命周期、Tick 调度和组合根服务表 |
| `AI/` | 通用 AI 规划抽象 |
| `Gameplay/` | 运行时实体、技能、目标选择和效果执行 |
| `Preview/` | 运行时预览服务和 Unity 菜单 |
| `Demo/` | 可运行垂直切片 |
| `Tests/` | EditMode / PlayMode 测试 |

## 编码规范

- 公共 API 命名稳定、直白，避免业务词：不要在框架公共类型中出现 `Player`、`Monster`、`Element`、具体技能名等。
- 接口前缀 `I`，策略后缀 `Policy`，工厂后缀 `Factory`，描述性数据后缀 `Definition` / `Spec`。
- 高频路径避免 GC 分配；必要分配要限制在构建、编辑器、调试或低频流程。
- 事件 payload 优先使用 `readonly struct`。
- 公共类型变更要同步 `Docs/Interfaces/` 或 `Docs/USAGE.md`。
- 示例代码可以简单，但不能绕过真实 Runtime API。

## 当前运行时切片

框架当前最小可运行闭环是：

- 任务：`Docs/Tasks/RUNTIME_VERTICAL_SLICE_02_ENTITY_ABILITY_EFFECT.md`
- 核心：`Assets/Scripts/MxFramework/Gameplay/`
- 场景：`Assets/Scenes/RuntimeVerticalSlice.unity`
- 脚本：`Assets/Scripts/MxFramework/Demo/RuntimeVerticalSliceRunner.cs`

目标是在 Play Mode 中直接看到 Entity -> Ability -> Target -> Effect -> Attributes -> Buffs -> Events 串起来运行。这个切片优先级高于继续包装 Authoring Editor 或 Runtime Preview 场景目标。

## 修改前检查

- 修改公共类型、核心管线或跨模块依赖前，先查调用点和测试覆盖。
- 修改 Buff / Modifier / Attribute 行为时，至少检查对应 `Tests/` 子目录。
- 改动 Preview 或 Editor 时，确认 Runtime asmdef 没有新增 Editor 依赖。
- 提交前回到仓库根，使用 `git diff`、`rg`、源码阅读和相关测试做影响面检查。
