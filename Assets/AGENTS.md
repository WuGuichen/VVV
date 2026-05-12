# Assets Agent Guide

本层约束 Unity `Assets/` 下的资源、场景和程序集布局。更深层的 `Assets/Scripts/MxFramework/AGENTS.md` 会继续约束框架源码。

## Unity 资产规则

- Runtime 资源和 Editor 资源分目录放置，不让 Runtime asmdef 引用 `UnityEditor`。
- 新增场景优先放在 `Assets/Scenes/`，命名要表达验证目标，例如 `RuntimeVerticalSlice.unity`。
- 示例和验证资源必须是框架级 Demo，不包含 WGame 真实业务数据。
- 新增 `.meta` 文件必须随资源一起提交。
- 不提交 Library、Temp、Logs、个人插件缓存或本地生成的工具状态。

## 程序集布局

| asmdef | 类型 | 职责 |
| --- | --- | --- |
| `MxFramework.Core` | Runtime, noEngine | 纯 C# 工具层 |
| `MxFramework.Core.Unity` | Runtime | Unity 适配，例如 Vector、RandomTable |
| `MxFramework.Events` | Runtime, noEngine | 事件总线 |
| `MxFramework.Attributes` | Runtime, noEngine | 属性存储 |
| `MxFramework.Modifiers` | Runtime, noEngine | 修改器管线 |
| `MxFramework.Buffs` | Runtime, noEngine | Buff 生命周期管线 |
| `MxFramework.Config` | Runtime, noEngine | 配置抽象、Schema、表结构 |
| `MxFramework.Config.Runtime` | Runtime | 配置驱动的运行时工厂桥接 |
| `MxFramework.AI` | Runtime, noEngine | 轻量 AI 抽象和 Planner |
| `MxFramework.Diagnostics` | Runtime, noEngine | 运行时调试快照协议 |
| `MxFramework.Runtime` | Runtime, noEngine | Runtime Host、模块生命周期和 Tick 调度 |
| `MxFramework.Editor` | Editor only | Unity 内部管理与配置工具 |
| `MxFramework.Demo` | Runtime | 框架级可运行示例 |
| `MxFramework.Demo.Editor` | Editor only | Demo 场景创建菜单 |
| `MxFramework.Preview.Runtime` | Runtime | Preview Server / Preview World |
| `MxFramework.Preview.Editor` | Editor only | Preview 菜单入口 |

## 新增 Unity 文件时

- 新 Runtime 代码先确认应该属于框架核心、Unity 适配、Preview、Demo 还是游戏层示例。
- 新 Editor 代码必须放入 Editor asmdef 管辖目录。
- 新场景如果用于验收任务，应在 `Docs/USAGE.md` 或对应 `Docs/Tasks/*.md` 写清打开方式。
- Demo 可以依赖 Runtime 模块，但 Runtime 模块不能依赖 Demo。

## 文件生成约束

Agent 生成 Unity 相关文件时，按下表判断是否可以直接写文件。原则是：文本源码可以直接生成；Unity 序列化资产优先让 Unity Editor 或专用编辑器生成，避免 YAML 结构、GUID、fileID 和 importer 状态不一致。

| 文件类型 | 是否建议 Agent 直接生成 | 规则 |
| --- | --- | --- |
| `.cs` | 可以 | 遵守 asmdef 边界和源码层规范 |
| `.asmdef` | 可以，但要谨慎 | 新增或改引用前确认依赖方向，不破坏 Runtime / Editor 隔离 |
| `.shader` | 可以 | 放在明确目录，必要时配套材质由 Unity 生成 |
| `.uxml` / `.uss` | 可以 | 适合文本生成；更新后在 Unity 中验证 UI 是否能加载 |
| `.json` / `.yaml` 自定义配置 | 可以 | 仅限框架自定义格式；必须有 Schema、示例或文档说明 |
| `.mat` | 不默认，简单情况可接受 | 简单材质可由 Unity MCP / Editor API 创建；复杂材质不要手写 YAML |
| `.asset` ScriptableObject | 不默认 | 优先通过 Editor 菜单、Unity MCP 或专用生成器创建 |
| `.prefab` | 不建议 | 优先在 Unity 中创建和保存，Agent 不直接手写 prefab YAML |
| `.unity` | 强烈不建议 | 场景文件应通过 Unity Editor / Unity MCP 创建和修改 |
| `.controller` | 不建议 | Animator Controller 优先用 Unity Editor / API 生成 |
| `.anim` | 不建议 | AnimationClip 优先用 Unity Editor / API 生成 |
| `.inputactions` | 谨慎 | 最好通过 Unity Input System 编辑器或官方格式工具修改 |
| `.meta` | 最好 Unity 生成 | 资源由 Unity 导入后提交生成的 `.meta`；除非修复明确 GUID 问题，否则不手写 |

如果必须直接生成“不建议”类文件，先说明原因，并在生成后用 Unity 打开、刷新或验证资源可用。

## 当前优先级

先保证 `RuntimeVerticalSlice` 这类最小可运行闭环成立，再推进更复杂的场景预览、外部编辑器和 Mod 流程。
