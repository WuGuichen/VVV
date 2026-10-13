# FairyGUI UI Composer 使用指南

> Status: Guide
>
> 适用版本：UI Composer v5
> 文档版本：2026-10-12.1

本文面向使用 UI Composer 搭建和维护 FairyGUI 界面的设计、开发与验收人员。架构权威、文件职责和同步规则见 `Docs/FAIRYGUI_UI_COMPOSER.md`；本文只说明当前工具怎么用。

## 工作台结构

Composer 使用深色三栏创作工作台：

- 顶部只保留界面选择、保存状态、预检与“应用并发布”；保存当前层、同步、单独发布、命令面板和评审位于右侧更多菜单。
- 左侧窄模式栏在层级、语义资源、按钮配方和组件面板之间切换。
- 中央画布使用浮动工具条、设计标尺、网格、对齐参考线和底部状态栏，不因工具栏换行压缩画布。
- 右侧属性检查器按布局、外观和绑定分区，字段按位置尺寸、锚点约束、自动布局等折叠组织。

默认打开 CharacterTest 主菜单；URL 的 `screen` 参数仍可指定其他 allowlist 蓝图。

## 1. 启动与入口

在仓库根目录启动 Authoring Server：

```powershell
dotnet run --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- editor serve --root . --port 4873
```

打开：

```text
http://127.0.0.1:4873/Tools/MxFramework.FairyGuiUiComposer/web/
```

也可以从 EditorHub 进入 UI Composer。页面默认打开 CharacterTest 主菜单，可在顶部“界面”下拉框切换其他 allowlist 界面。

如果页面提示 API 版本不匹配，先停止旧 Authoring Server，再用当前工作区重新启动。UI Composer v5 只接受 `mx.uiComposer.authoring.v5`，以免旧客户端误读 Blueprint v3 的 List 节点。

## 2. 工作台结构

界面分为三个主要区域：

| 区域 | 职责 |
| --- | --- |
| 左侧 | 在层级、资源、配方、组件之间切换；搜索对象并创建节点。 |
| 中央 | 编辑真实界面布局，执行选择、移动、缩放、批量布局和分辨率预览。 |
| 右侧 | 编辑当前节点、容器、语义资源或按钮配方，并查看绑定和诊断。 |

顶部状态分别显示资源、配方和蓝图是否干净。三层草稿相互独立，但“统一预检”和“应用并发布”会按依赖顺序检查整个工作台。

## 3. 推荐操作流程

```text
选择界面
  -> 编辑资源、配方或节点
  -> 统一预检
  -> 查看诊断和变更评审
  -> 保存当前层或保存蓝图
  -> 应用并发布
  -> 必要时从 FairyGUI 同步受支持的微调
```

- “保存当前层”保存当前存在改动的资源、配方或蓝图草稿。
- “保存蓝图”只保存 screen blueprint，不修改 FairyGUI XML。
- “应用并发布”先保存有效草稿，再由 FairyGUI helper 物化和发布。
- “从 FairyGUI 同步”只回读支持的属性。遇到未知节点、transition、gear 或高级属性会阻断，不会静默删除。

## 4. 编辑语义资源

1. 打开左侧“资源”。
2. 搜索名称、稳定 ID 或分类。
3. 选择资源后，在右侧维护显示名称、分类、用途、标签、设计尺寸和 FairyGUI 使用策略。
4. 点击右侧星标收藏常用资源。收藏项优先显示，最近打开的资源随后排序。
5. 使用“PNG 新建与替换”进入资源文件操作界面。

资源稳定 ID 不允许直接改名。需要迁移 ID 时，应创建新资源并通过批量替换解除旧引用。

### 批量替换资源引用

右侧“使用位置”会展示服务端重新构建的反向引用。点击“批量替换引用”，从兼容资源列表中选择目标。操作会同时进入工作台撤销历史，之后必须重新统一预检。

不要使用浏览器直接修改仓库文件。PNG、Catalog、FairyGUI XML 和发布产物仍由各自受控流程写入。

## 5. 编辑按钮配方

1. 打开左侧“配方”。
2. 选择标准 Button 配方，或复制现有配方作为新配方。
3. 编辑默认尺寸、基础资源、实例标题/图标能力及状态颜色。
4. 从“使用位置”定位引用该配方的界面节点。
5. 删除前检查全部反向引用；仍被引用的配方会被服务端阻断。

FairyGUI 组件名称必须在配方目录中唯一，大小写不同也算重名。名称留空时使用配方 ID 最后一段，因此也可能与其它默认名称或显式名称冲突。保存或统一预检出现 `UICOMP020` 时，按诊断定位两个配方，为其中一个指定独立组件名后重新预检；服务端和 helper 都会阻止重名文件覆盖。

表单编辑只支持受控的标准 FairyGUI Button 配方。系统维护的 Composite
配方会出现在配方列表、引用链和画布预览中，但 Inspector 为只读；当前
Runtime AI Lab 的行为卡流程和五轨时间线使用这一机制。不能在 Composer
中添加任意 controller、transition、脚本或自由组件树。

`MxCharacterTestRuntimeAiLabUi` 与独立的
`MxCharacterTestRuntimeAiLabRecoveryUi` 均通过 Apply/Publish、binding
contract、manifest/binder、Resource Catalog 和 Unity focused 验证。前者的
1920x1080 Screen Blueprint、行为卡、玩法编辑器、开发者流程和五轨时间线是
工作区布局权威；后者只承载 transition/recovery fail-safe surface，并采用独立
资源预检。正式 manifest 不再注册 legacy Launcher，F9 是唯一重开入口；其他
尚未迁移的界面仍留在非 Composer-owned 的 `MxCharacterTestHud` package。

修改配方默认尺寸不会自动缩放已经存在的实例。需要统一实例尺寸时，选择相关节点后使用批量等宽、等高或等尺寸。

## 6. 创建和组织节点

左侧“组件”提供可拖入画布的 Graph、Text、语义 Image、按钮实例和 List。也可以使用“三按钮操作组”模板快速创建常用纵向操作区。

### List 节点

List 是实际发布为 FairyGUI `GList` 的 Screen 节点，不是下面介绍的
authoring 布局容器。创建后在 Inspector 设置：

- 条目组件配方：列表默认条目的结构来源；不能在 List 内直接搭任意子树。
- 排列：首版支持纵向 Column 和横向 Row。
- 行距、列距：非负整数。
- 选择模式：默认不可选，也可设为单选、组合键多选或点击切换多选；原生选择要求条目配方本身具备可选择的 Button 语义，普通 Composite 条目应继续使用不可选并由 Binder 处理点击。
- 溢出：当前固定为滚动；横向列表会同步生成水平滚动配置。
- 运行时虚拟化：默认关闭；勾选只记录运行时意图，不等于在 XML 中自动启用。

新增或粘贴 List 会把当前蓝图升级为
`mx.uiScreenBlueprint.v3`，现有 V1/V2 页面不会批量迁移。Apply 会把条目
配方物化进同一 FairyGUI package，并用 `defaultItem="ui://..."` 连接；删除
仍被 List 引用的配方会被阻断。

勾选“运行时虚拟化”后，业务 Binder 仍需提供 `itemRenderer`、调用
`GList.SetVirtual()` 并设置 `numItems`。Composer 在这一步只建立可发布的
List 结构与运行时契约标记，不生成具体业务数据绑定。

### 选择

- 单击：只选择当前节点。
- `Ctrl + 单击`：增删多选项。
- `Shift + 单击`：在层级树中范围选择。
- 在画布空白处拖动：框选。
- `Ctrl+A`：选择当前容器内全部可见节点。
- `Esc`：清空选择。

主选择节点用于等尺寸等操作的基准。多选后可以整体移动和批量修改宽、高、锚点。

### 字体

Text 节点的“字体”字段写入 `text.font`，Apply/Publish 输出 FairyGUI `font` 属性，Sync 会回读该属性；留空继续使用默认字体。标准按钮配方的 `statePresentation.titleFont` 和 `titleBold` 控制按钮标题字体与伪粗体，省略时保持旧版本行为（默认字体、粗体）。当前项目已绑定精简 MxUI 字体，使用真实字重时设置 `bold=false`／`titleBold=false`。卡名和页面主标题用 `MxUI Serif SC`，正文用 `MxUI Sans SC`，控件与数字用 `MxUI Sans SC`。

Unity 在进入场景前注册随项目打包的字体；Web 画布从仓库字体文件加载。FairyGUI 编辑器需运行 `Tools/MxFramework.FairyGuiUiComposer/scripts/install_ui_fonts.ps1` 安装同一份字体，详见 `Assets/Resources/Fonts/README.md`。

### 网格与吸附

画布工具栏将视觉网格和坐标吸附作为两个独立设置：

- “网格”只控制参考网格是否显示，不改变节点移动结果。
- “吸附”控制新建、拖动和缩放是否按当前步长对齐；关闭后仍保持整数像素坐标。
- “步长”同时决定网格间距和吸附单位，可选择 `4px`、`8px` 或 `16px`。
- 拖动或缩放时按住 `Alt`，可以临时绕过吸附进行精细调整，松开后恢复原设置。

网格显示、吸附状态和步长保存在浏览器本地，不参与 blueprint hash，也不会改变 FairyGUI 发布结构。

### 复制与撤销

- `Ctrl+C`：复制节点或完整容器子树。
- `Ctrl+V`：粘贴，并以 16px 偏移生成稳定的新 ID 和名称。
- `Ctrl+D`：快速复制当前选择。
- `Ctrl+Z`：撤销。
- `Ctrl+Shift+Z` 或 `Ctrl+Y`：重做。

粘贴不会复制 C# binding slot，避免一个必需槽被重复占用。撤销历史覆盖资源、配方和蓝图三层，不会出现只回退一半的批量替换。

### 锁定、隐藏和隔离

层级标题区提供“锁、眼、独”：

- 锁定：节点仍显示，但不能拖动、缩放、方向键移动或删除。
- 隐藏：仅隐藏 Composer 画布显示，不修改蓝图和发布结果。
- 隔离：只显示当前选择或容器子树；再次点击退出。

锁定和隐藏按界面保存在浏览器本地；隔离只属于当前会话。它们都不参与 blueprint hash。

### 层级搜索和筛选

搜索支持名称、节点类型、binding slot、资源 ID 和配方 ID。筛选项包括：

- 全部节点。
- 仅未绑定。
- 仅有诊断。
- 仅当前容器。

容器的子孙节点命中搜索时，父容器仍会保留，避免结果失去上下文。

## 7. 分组、容器和批量布局

- `Ctrl+G`：把选择创建为 Free 分组。
- `Ctrl+Shift+G`：解除分组。
- 层级拖放：调整父容器和同级顺序。

容器模式：

| 模式 | 行为 |
| --- | --- |
| Free | 保留子节点相对位置，移动分组时整体移动。 |
| Horizontal | 按顺序横向排列，容器控制子节点位置。 |
| Vertical | 按顺序纵向排列。 |
| Grid | 按列数、间距和 Padding 排列。 |

自动布局容器中的子节点不能直接拖动位置。调整容器参数、改变层级顺序，或先解组再自由编辑。

批量布局支持左右/水平中心、上下/垂直中心、水平/垂直等间距、等宽、等高和等尺寸。等间距至少需要 3 个节点；等尺寸以主选择节点为基准。

容器只存在于蓝图 authoring 层。Apply 时会递归计算绝对坐标并扁平写入 FairyGUI `displayList`，不生成额外运行时 `GComponent`。

## 8. 画布导航与分辨率检查

- `F`：聚焦当前节点或容器。
- `Home`：适应全部。
- 空格拖动或鼠标中键拖动：平移画布。
- `Ctrl + 滚轮`：缩放。
- 方向键：移动 1px。
- `Shift + 方向键`：移动 10px。

“多分辨率对比”会同时显示：

- 1280x720
- 1920x1080
- 2560x1440
- 3440x1440

该视图用于检查锚点、Stretch、裁切和重叠，不会创建 breakpoint 布局。发现问题时回到主画布调整锚点、尺寸或容器。

## 9. 绑定契约

绑定槽来自 generated C# contract，只读且带 `contractHash`。Composer 只能选择现有槽位，不能填写任意 C# 表达式或命令。

- 必需槽不可删除。
- 同一槽不能重复分配。
- 分配槽位时，节点名称会收敛到契约中的控件名。
- 契约在外部变化后，旧页面的 Save/Apply 会被 hash 校验阻断，应刷新页面重新加载。

## 10. 诊断与安全修复

诊断行提供“定位”，会自动切换到对应节点、资源或配方并聚焦。当前只对无歧义且可撤销的问题提供自动修复：

- 非正尺寸：归一为最小有效尺寸。
- 缺失配方：替换为当前有效配方。
- 非法按钮图标：清空图标引用。
- 缺失图片资源：替换为当前有效资源。

文件漂移、未知 XML 属性、冲突 hash 和不支持的 FairyGUI 结构不会自动修复。按诊断路径处理源文件后刷新并重新统一预检。

## 11. 自动恢复、快照和评审

### 自动恢复

存在未保存改动时，Composer 会在短暂防抖后把资源、配方和蓝图草稿写入浏览器本地恢复区。重新打开同一界面时：

- 四个 authoring hash 全部一致：可选择恢复或丢弃。
- 任一 hash 已变化：旧草稿不会覆盖新源文件，并会被丢弃。

恢复后内容仍是未保存草稿，必须统一预检并正式保存。恢复区不是仓库事实源，也不能替代 Git。

### 快照与评审

点击顶部“评审”可以：

- 查看资源、配方、节点和容器的新增、修改和删除摘要。
- 创建最多 10 个按界面隔离的本地工作台快照。
- 恢复快照为未保存草稿。
- 进入只读评审模式，冻结编辑控件，减少误操作。

快照只保存在当前浏览器，不提交仓库，不适合作为跨机器备份。

## 12. 命令面板与快捷键

按 `Ctrl+K` 打开命令面板，可搜索并执行统一预检、保存、适应全部、聚焦选择、多分辨率对比和常用模板。

| 快捷键 | 功能 |
| --- | --- |
| `Ctrl+K` | 命令面板 |
| `Ctrl+C / Ctrl+V / Ctrl+D` | 复制 / 粘贴 / 快速复制 |
| `Ctrl+Z / Ctrl+Shift+Z / Ctrl+Y` | 撤销 / 重做 |
| `Ctrl+A` | 选择当前容器内全部可见节点 |
| `Ctrl+G / Ctrl+Shift+G` | 分组 / 解组 |
| `F` | 聚焦选择 |
| `Home` | 适应全部 |
| `Esc` | 清空选择 |
| `Delete` | 删除选择；锁定或必需绑定节点会阻断整批删除 |
| 方向键 / `Shift+方向键` | 移动 1px / 10px |
| 空格拖动 / 中键拖动 | 平移画布 |
| `Ctrl+滚轮` | 缩放画布 |

输入框、文本区和下拉框获得焦点时，Composer 不拦截这些画布快捷键。

## 13. 常见问题

### 页面加载不了

1. 确认 Authoring Server 正在监听 4873。
2. 直接请求 `/api/authoring/ui-composer/capabilities`。
3. 确认返回 API version 为 `mx.uiComposer.authoring.v5`。
4. 停止旧服务并从当前工作区重新启动。

### Apply 或 Publish 被阻断

先运行“统一预检”。常见原因是 Catalog、配方、契约、蓝图、mapping 或 FairyGUI XML hash 已变化。刷新页面，不要强行覆盖外部改动。

### 一条命令完成 Apply 和 Publish

开发代理或本地自动化应优先使用正式的 `publish-all`，不再手工拼接启动编辑器、Apply、轮询和 Publish：

```powershell
dotnet run --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- `
  ui-composer publish-all `
  --project-root E:\Unity\WxFramework\WGameFramework `
  --screen ui.screen.character_test.runtime_ai_lab `
  --verify-unity
```

- 默认或 `--fast`：统一预检、helper 就绪、Apply、Publish、XML/state/bytes/atlas/manifest/catalog 核对。
- `--verify-unity`：在产物核对后，通过当前项目 Unity MCP 执行一次 `validate_unity_scripts`。
- `--verify-interaction --interaction-profile runtime-ai-lab`：再追加 Runtime AI Lab Trigger 与真实 Game View 证据；未注册交互网关时明确失败，不使用坐标点击代替。
- `--json` 输出结构化结果；本地完整证据写入 `Logs/UiComposerPublish/<operationId>.json`。
- `Ctrl+C` 会返回稳定的 `Cancelled` 结果，并且只清理由本次命令创建、尚未被 helper 领取的 request；不会中止正在处理的请求，也不会关闭用户原本打开的 FairyGUI。

命令先用只读 `Probe` operation 确认 FairyGUI helper 已经加载，之后才允许 Apply。它只关闭本次命令亲自启动的 FairyGUI；已有编辑器始终复用并保留。
`runtime-ai-lab` 交互配置通过项目内 Trigger 工具打开目标场景、跨域重载进入 Play Mode，并核对唯一 Controller、面板 configured/open、根组件和命令绑定；截图单独写入同一 operation 的 Logs 子目录。

### 自动布局节点不能拖动

该节点属于 Horizontal、Vertical 或 Grid 容器。调整容器、拖动层级顺序，或解除布局后再移动。

### 小屏幕工具栏换行

新版工作台在 `1280×720` 及以上保持三栏、单行顶栏和单行浮动画布工具条，低频命令进入更多菜单，不再通过换行占用画布高度。低于 1280px 时，顶部的导航与属性按钮分别打开左右抽屉；按 `Esc` 可关闭菜单或抽屉。

### FairyGUI 手工改动无法同步

检查是否新增了 Composer 不支持的节点、transition、gear 或属性。Composer 会阻断而不是丢弃这些结构；需要回到 FairyGUI Editor 或扩展受控 schema 后再同步。

## 14. 验证命令

```powershell
node --check Tools/MxFramework.FairyGuiUiComposer/web/app.js
node Tools/MxFramework.FairyGuiUiComposer/scripts/test_layout_tools.js
dotnet run --project Tools/MxFramework.Authoring/tests/MxFramework.Authoring.Tests/MxFramework.Authoring.Tests.csproj -- ui-composer-list
dotnet run --project Tools/MxFramework.Authoring/tests/MxFramework.Authoring.Tests/MxFramework.Authoring.Tests.csproj -- ui-composer-publish-all
dotnet run --project Tools/MxFramework.Authoring/tests/MxFramework.Authoring.Tests/MxFramework.Authoring.Tests.csproj
dotnet run --project Tools/MxFramework.NoEngineTests/FairyGUI.Manifest.Tests/FairyGUI.Manifest.Tests.csproj -- --check-generated
git diff --check
```

真实 Apply/Publish 后还应检查 FairyGUI helper operation status、Unity refresh/compile、Console error 和目标界面的 focused 加载/交互测试。
