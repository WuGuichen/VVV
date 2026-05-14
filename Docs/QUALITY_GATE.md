# MxFramework 质量门禁

> 版本 0.3.2 | 2026-05-13
>
> 本文档定义每批迁移、每个模块和每次发布必须满足的验收条件。

## 1. 批次完成定义

每个迁移批次必须满足：

- 源文件和目标文件在 `MIGRATION.md` 有一一对应记录。
- 每个迁移文件保留来源注释。
- 无 WGame 命名空间引用。
- 无 Entitas、Luban、CrashKonijn 等项目依赖。
- asmdef 依赖符合 `ARCHITECTURE.md`。
- 公共 API 已写入 `INTERFACES.md`。
- 至少有一个覆盖核心行为的测试或 Editor 验证说明。
- 剩余风险明确记录，不用“后续处理”替代具体事项。

## 2. 模块验收

### 2.1 Core

- `MxFramework.Core` 默认无 UnityEngine 依赖。
- Unity 类型扩展放入 `MxFramework.Core.Unity` 或标记为待拆分。
- 集合类边界行为有测试：空、满、重复移除、延迟删除、索引更新。
- 位运算类有正负数和边界值测试。
- 字符串工具有 IL2CPP/AOT 风险说明。

### 2.2 Events

- 订阅、取消订阅、重复订阅、防重入行为明确。
- 发布事件时一个 handler 抛异常的策略明确。
- 高频发布无分配。
- 支持 `IDisposable` 订阅句柄。

### 2.3 Attributes

- 区分 base value、final value、modifier value。
- 修改器排序稳定，排序规则写入文档。
- 属性不存在、重复注册、移除修改器的行为明确。
- 变更事件只在 final value 变化时发送，除非文档另行声明。
- 支持调试快照展示每个属性的计算链。

### 2.4 Modifiers

- 条件、效果、计数器、生命周期彼此解耦。
- `ModifierContext` 支持池化并能安全清理残留字段。
- 工厂创建失败有明确错误。
- 修改器不能直接依赖 Buff 实现类或游戏实体。
- 支持暂停、禁用、移除时的资源释放。

### 2.5 Buffs

- 堆叠策略、刷新策略、覆盖策略可配置。
- 永久 Buff 与限时 Buff 行为明确。
- Tick 顺序稳定。
- 移除 Buff 必须解除属性修改器和事件订阅。
- Buff 子类不能隐藏公共生命周期状态。

### 2.6 Runtime AI Planner（AI 模块）

- 只提供接口和轻量默认实现。
- 不绑定 GOAP 插件。
- Planner 可替换。
- `IAiWorldState` 不暴露游戏实体实现。

### 2.7 Config

- 不绑定 Luban。
- 缺失配置、重复 ID、热重载失败有明确结果。
- 支持至少一个默认内存 Provider，便于单元测试。

### 2.8 Editor

- Editor 程序集不进入 Runtime build。
- 所有验证结果可导出文本或 JSON。
- Debug 面板使用快照接口，不直接依赖实现私有字段。
- Test/Sandbox 不污染项目真实配置和场景对象。

### 2.9 Input

- 业务代码依赖 `IInputProvider`，不直接读取键盘、手柄、触屏或 XR 控件。
- Action 按意图命名，具体设备输入只出现在 `.inputactions` bindings 中。
- `InputService` 初始化后缓存 Action 引用，不在 Update 中字符串查找。
- 上下文切换通过 `InputContextStack`，避免业务到处 Enable/Disable Action Map。
- 重绑定 operation 完成、取消和销毁时必须 Dispose。
- 本地多人必须使用 `PlayerInput.actions` 的私有副本，不读全局 project-wide actions。
- 至少覆盖上下文栈、命令队列和可替换输入源的 EditMode 测试。

## 3. 自动检查建议

可先用脚本实现以下检查；具体日常工作流和提交前步骤见 `WORKFLOW.md`：

```text
check-no-wgame-reference
check-no-forbidden-package-reference
check-asmdef-dag
check-runtime-no-unityeditor
check-core-no-unityengine
check-public-api-documented
check-source-comment-for-migrated-files
```

## 4. Unity 验证清单

每批代码迁移后在 Unity 中验证：

1. 打开项目无编译错误。
2. Console 无新增错误。
3. asmdef 编译顺序符合预期。
4. 运行 EditMode tests。
5. 如涉及 Runtime Tick，运行 PlayMode smoke test。
6. 打开 `MxFramework > Framework Manager`，验证依赖图和模块状态。

### 4.1 UI Toolkit Runtime Demo 验收

新增或改动可玩 Demo / Runtime HUD 时必须额外验证：

- `UIDocument.panelSettings` 必须指向正式 `PanelSettings` 资产，不使用临时运行时 instance 作为最终场景配置。
- `PanelSettings.themeUss` 必须绑定 Runtime Theme，不允许为了消除 warning 把 no-theme warning 关掉后留空。
- 场景中的 `UIDocument.sourceAsset`、Demo 脚本中的 `VisualTreeAsset` 和 `StyleSheet` 必须显式绑定对应 UXML/USS。
- Play Mode 后检查 visual tree 中关键 root、HUD、按钮、状态 Label 存在。
- Play Mode 后至少读取一个标题 Label、一个 Button、一个动态状态 Label 的 `text` 和 `resolvedStyle.color`，确认文本非空且 alpha > 0。
- Console error / warning 为 0；UI Toolkit warning 不可用“看起来能跑”跳过。
- 如果 USS 或 Theme 在当前 Unity 版本下不稳定，Demo controller 必须像 Tetris / Breakout 一样在运行时对关键 `Label` / `Button` 设置 font、font size、color、alignment 作为兜底。

### 4.2 游戏功能 / 小游戏 / Runtime Showcase 验收

新增或改动游戏功能、小游戏、Playable Demo、Runtime Showcase 或场景验证时，必须额外满足 `Docs/AGENT_GAME_CREATION_GUIDE.md`：

- 编码前已有 API 复用计划，说明 Runtime、Input、Command、Replay、SaveState、UI、资源、Gameplay、Combat 等模块是否使用及原因。
- 游戏功能必须先映射到现有框架能力和接口；绕过既有模块时必须记录缺口，不能生成平行系统。
- 核心规则优先保持纯 C#，可由 EditMode 测试直接实例化。
- Unity `MonoBehaviour` 只承担组合根、输入适配、视图同步、场景 bootstrap 或 debug probe 职责，不承载权威玩法规则。
- 输入必须经意图 / command buffer 进入 runtime tick，UI 回调不得直接修改权威状态。
- 固定帧或命令驱动 Demo 必须提供 replay hash、diagnostics 或等价确定性验证。
- 有可保存状态时必须接入 Runtime SaveState；暂不接入时要记录原因和补齐任务。
- 任务最终必须声明交付等级：`Framework Feature` / `Runtime Slice` / `Playable` / `Blocked`。
- 目标是 Playable 时，必须有可打开场景或入口、已绑定 UI/配置/资源，并完成 Play Mode 或 Unity MCP smoke 验证；否则只能标记为 Runtime Slice。
- 新增场景、Prefab、ScriptableObject 等 Unity 序列化资产优先通过 Unity Editor、Unity MCP 或现有 Editor 菜单生成，不手写 YAML。

## 5. 发布清单

发布或提交前必须确认：

- `git status` 中没有意外的 `Library/`、`Temp/`、`Logs/`、`UserSettings/`、`.gitnexus/`、`.codex/`。
- 新增 `.meta` 文件与资产文件同时提交。
- 文档版本号已更新。
- `MIGRATION.md` 的批次状态与代码一致。
- 已记录无法自动验证的事项。
