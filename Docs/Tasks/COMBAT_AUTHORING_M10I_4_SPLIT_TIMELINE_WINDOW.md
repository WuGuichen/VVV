# Combat Authoring M10I.4：Split Timeline / Inspector Windows

> **状态**：已完成
> **优先级**：P0
> 前置任务：`COMBAT_AUTHORING_M10I_3_SHAPE_DELETE_DUPLICATE.md`
> 设计依据：`COMBAT_AUTHORING_GIZMO_TOOL_DESIGN.md` 的 UI Toolkit 性能、Timeline 编辑、No-Typing Authoring UX 原则
> 派发对象：Editor / Authoring 子代理

## 背景

当前 `Combat Authoring` 把基础字段、工具按钮、timeline、timeline row list、选中项属性、validation report、export / explain preview 都塞进一个 EditorWindow。即使已经加入整体滚动，窗口仍然对宽高要求过高，timeline 的横向空间不足，属性区也难以阅读。

制作侧反馈：希望拆成两个窗口。一个窗口让 timeline 宽度尽量占满，用来拖动和查看帧范围；另一个窗口显示各种属性、校验、导出和 explain。

## 目标

把 Combat Authoring 编辑器从单窗口拥挤布局推进到双窗口布局：

- `Combat Authoring`：作为属性 / 工具窗口，专注 Action、Binding、选中项属性、校验、导出和 Explain。
- `Combat Timeline`：作为专用 timeline 窗口，横向宽度优先，专注 frame scrubber、横向 timeline、timeline row list 和拖拽编辑。

两个窗口必须共享同一份当前 Action / Binding / Frame / Selection 状态。用户在 timeline 窗口选择或拖动 Shape 后，属性窗口能看到对应选中项和最新数据；用户在属性窗口切换 Action 或修改字段后，timeline 窗口能刷新。

## 范围

本阶段做 Split Window v0：

- 新增菜单入口：
  - `MxFramework > Combat > Combat Authoring`
  - `MxFramework > Combat > Combat Timeline`
  - 可选：`MxFramework > Combat > Open Authoring Layout` 同时打开两个窗口。
- `Combat Timeline` 窗口应包含：
  - 当前 Action / Binding 的简洁上下文条。
  - frame slider / 当前帧显示。
  - 横向 timeline strip，占据主窗口空间。
  - timeline row list，作为辅助选择列表。
  - 最少量状态提示，例如 range dragging 状态。
- `Combat Authoring` 属性窗口应包含：
  - Action Asset / Scene Binding 选择。
  - 常用工具按钮：创建 / 生成 Binding、添加 Hitbox / Hurtbox、验证、导出、Explain。
  - 基础字段。
  - 选中项属性。
  - validation issues、report preview、preview explain。
  - 一个明显按钮用于打开 Timeline 窗口。
- 从属性窗口中移除或折叠主 timeline strip，避免两个窗口重复挤占空间。
- 两窗口选中项同步：
  - timeline 窗口选中 row 后，属性窗口详情面板显示该 row。
  - 属性窗口修改当前 Shape 后，timeline 窗口刷新条块。
  - 删除 / 复制 Shape 后，两个窗口同步 selection / rows。
- 两窗口 frame 同步：
  - 任一窗口拖动 frame slider 后，另一个窗口和 Scene View gizmo 同步刷新。
- 两窗口 Action / Binding 同步：
  - 任一窗口通过 asset field 或使用当前选择设置 Action / Binding 后，另一个窗口更新。

## 交互要求

- Timeline 窗口默认 `minSize` 应明显偏宽，例如不低于 `900 x 360`。
- Timeline strip 不能被左右属性栏压缩；横向空间优先。
- 属性窗口默认 `minSize` 可以偏窄高，例如 `420 x 620` 起。
- 属性窗口内容必须整体可滚动。
- 不要求一次做漂亮，但要比当前单窗口更清楚：
  - 属性窗口不应再出现 timeline 挤压属性的布局。
  - Timeline 窗口中 timeline strip 应占据主要可视面积。
- 所有按钮和关键区域保留中文 tooltip。

## 技术建议

- 主要修改 `Assets/Scripts/MxFramework/Combat.Editor/CombatAuthoringWindow.cs`。
- 可以在同文件内新增 `CombatAuthoringTimelineWindow`，也可以把共享 UI 逐步抽出小型 helper；本阶段优先小步改造，不做大规模重写。
- 建议使用已有 `CombatAuthoringSceneState` 作为跨窗口状态源，必要时扩展它：

```text
ActionAsset
SceneBindingAsset
Frame
Selection
Changed
```

- 如果现有 `CombatAuthoringWindow` 私有方法复用困难，可以先引入布局模式：

```text
CombatAuthoringWindowMode.Inspector
CombatAuthoringWindowMode.Timeline
```

同一个类根据 mode 创建不同 UI；也可以创建第二个 EditorWindow 子类调用共享构建函数。

- 避免复制两套 timeline 编辑逻辑。Timeline drag / range edit 仍应只有一套实现。
- 避免两个窗口反复互相触发无限刷新。跨窗口 `Changed` 事件应只在状态实际变化时刷新。
- 不改 Runtime Combat 逻辑。
- 不改 JSON export key。

## 非目标

- 不做完整 docking layout 自动排版。
- 不做 Timeline 多选、批量复制粘贴。
- 不做新的 GraphView timeline。
- 不做外部独立 Authoring Editor。
- 不改 Scene View gizmo 数据模型。

## 验收标准

- 菜单可以分别打开 `Combat Authoring` 和 `Combat Timeline`。
- 属性窗口能选择 / 创建 Action Asset，并能打开 Timeline 窗口。
- Timeline 窗口中横向 timeline strip 明显占据主区域，不再被左右属性栏挤压。
- 在 Timeline 窗口选中 Hitbox / Hurtbox 后，属性窗口显示该 Shape 详情。
- 在 Timeline 窗口拖动 range 后，属性窗口的 Start / End 控件刷新。
- 在属性窗口点击 `添加 Hitbox` 后，Timeline 窗口出现新 row 并选中。
- 在属性窗口点击 `复制 Shape` / `删除 Shape` 后，Timeline 窗口同步刷新。
- 任一窗口调整 frame slider 后，另一个窗口和 Scene View gizmo 同步。
- Unity Console 无 error。
- Authoring EditMode tests 通过。

## 测试要求

子代理完成后不要提交 SVN，由主代理复核和提交。子代理至少自查：

- Unity MCP refresh / Console error 检查。
- 相关 Authoring EditMode tests。
- 手动打开两个窗口，验证 Action / Binding / Frame / Selection 基础同步。
- 手动验证 timeline range drag、添加 Shape、复制 / 删除 Shape 后双窗口刷新。

## 完成记录

- `Combat Authoring` 拆为属性 / 工具窗口，保留 Action / Binding、工具按钮、Frame、基础字段、选中项属性、validation、report preview、preview explain。
- 新增 `Combat Timeline` 专用时间轴窗口，横向 timeline strip 作为主区域，并保留 frame slider 与 timeline row list。
- 新增菜单：
  - `MxFramework > Combat > Combat Authoring`
  - `MxFramework > Combat > Combat Timeline`
  - `MxFramework > Combat > Open Authoring Layout`
- 属性窗口提供 `打开 Timeline` 按钮。
- 同一个 `CombatAuthoringWindow` 通过 `Inspector / Timeline` 两种 mode 复用 timeline 选择、拖拽和 shape 操作逻辑，避免复制第二套 timeline 编辑实现。
- `CombatAuthoringSceneState` 新增 `DataRevision` 和 `NotifyDataChanged()`，用于跨窗口同步 Action / Binding / Frame / Selection 之外的数据刷新。
- Shape 添加、复制、删除、range drag、字段变更、Binding 生成 / 重连后会通知另一窗口刷新。
- Unity MCP 编译 / Console error 检查：0 error。
- Authoring EditMode tests：11/11 passed。
- 不落盘反射烟测：
  - `Open Authoring Layout` 后 Inspector / Timeline 两窗口存在。
  - Inspector 添加 Hitbox 后 Timeline 同步 Action、row 和 selection。
  - Inspector 复制 Shape 后 Timeline row 同步增加，Action hitboxes 为 2。
  - 共享 Frame 设置为 7 后，两窗口 slider 均同步为 7。
- GitNexus detect-changes：low risk，affected processes 0。

## 提交边界

本任务只允许修改 Combat Authoring / Editor / Tests 相关文件和本任务文档状态。不要提交或改动以下未跟踪本地文件：

- `Assets/CombatActionAuthoringAsset.asset`
- `Assets/CombatSceneBindingAsset.asset`
- `.claude` / `.gitnexus` / `Tools/*.py`
