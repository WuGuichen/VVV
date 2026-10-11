# UI Art Resource Library 使用指南

> Status: Guide
>
> 适用范围：从零整理 UI PNG、维护语义与替换约束、供 UI Composer 按稳定 `resourceId` 创作界面，以及为 legacy FairyGUI package 生成接入审计配方。

## 1. 当前边界

UI Art Resource Library 是独立于 FairyGUI package 的手工资源库。它负责让开发者和 Development Agent 明确一张切图是什么、应该怎样使用、怎样安全替换。

| 内容 | 权威来源 |
|------|----------|
| PNG 原图、资源语义、推荐 FairyGUI 参数、替换约束 | `ArtSource/UIKits/<Library>/` 与 `ui-art-resources.json` |
| FairyGUI image id、包内路径和实际声明 | `FGUIProject/assets/<Package>/package.xml` |
| 组件中的实际使用点 | FairyGUI component XML |
| atlas、`*_fui.bytes` 和运行时包 | FairyGUI Editor 发布结果 |
| 接入计划、JSON/Markdown 配方 | 工具生成的派生结果，不是事实源 |

当前工具可以写资源库 Catalog、创建新的资源库 PNG、同路径替换已有资源库 PNG。FairyGUI 接入计划始终只读，不会复制 PNG 到 FairyGUI 包，也不会修改 `package.xml`、component XML、atlas 或 `*_fui.bytes`。

## 2. 启动编辑器

Windows：

```powershell
Tools\MxFramework.EditorHub\start-editor-hub.bat
```

启动后从 EditorHub 打开 **UI Art Resources**，或直接访问：

```text
http://127.0.0.1:4873/Tools/MxFramework.FairyGuiArtSlices/web/?library=MxUiFoundation
```

页面加载成功时，顶部会显示资源库名称、资源数量、错误和警告。若提示 Authoring Server 版本过旧，关闭旧服务后重新运行启动脚本。

## 3. 新增一张资源

### 3.1 选择 PNG

1. 点击资源列表标题右侧的 `+`。
2. 在“新增 UI 切图”抽屉中选择或拖入 PNG。
3. 编辑器读取像素尺寸和 Alpha 信息，并填写初始路径、尺寸与约束。

PNG 是新增流程的第一输入。不要先手写一套尺寸和 NineSlice 参数，再选择尺寸不一致的图片。

### 3.2 填写资源身份

| 字段 | 规则 | 示例 |
|------|------|------|
| `resourceId` | 资源库内稳定且唯一；不含空格和路径分隔符 | `ui.button.generic.base` |
| `relativePath` | 资源库内相对路径，只使用 `/`，扩展名必须是 `.png` | `images/buttons/button_generic_240x64.png` |
| 显示名称 | 面向人的简体中文名称 | `通用按钮底图` |
| 分类 | 用于筛选、校验和推荐策略 | `ButtonBase` |
| 描述 | 写明用途、视觉边界和禁用场景 | `通用按钮的可拉伸底图` |
| 标签 | 使用逗号分隔 | `button, generic` |

`resourceId` 和 `relativePath` 创建后属于资源身份，普通语义保存不能修改。需要迁移路径时，应另做受控迁移，不要通过浏览器开发者工具绕过限制。

### 3.3 填写 FairyGUI 建议

常用策略：

| 策略 | 使用场景 |
|------|----------|
| `Fixed` | 固定像素图标、徽标、小装饰 |
| `PreserveAspect` | 可以缩放但必须保持比例的图片 |
| `Stretch` | 允许整体拉伸的简单底图 |
| `HorizontalFill` / `VerticalFill` | 进度条填充 |
| `NineSlice` | 按钮、面板、输入框等需要保留边角的资源 |
| `Tile` | 可平铺纹理 |

NineSlice 资源必须填写位于 PNG 像素范围内的 `x/y/width/height`。现有资源可在中央预览图上拖动四条边界线，数值会同步到属性检查器。

### 3.4 填写替换约束

| 约束 | 含义 |
|------|------|
| 需要 Alpha 通道 | 候选 PNG 必须包含 Alpha 通道 |
| 需要透明像素 | 候选 PNG 不能全部不透明；开启时也必须开启 Alpha 通道约束 |
| 相同像素尺寸 | 替换时必须保持原始宽高 |
| 允许拉伸 | 该资源允许被组件拉伸 |
| 最小宽度/高度 | 候选 PNG 不得低于该像素尺寸 |
| 整组替换 | 未来按钮状态组等资源需要一起审阅；当前只提示，不执行多文件事务 |

### 3.5 预检并创建

1. 点击“预览计划”。
2. 检查计划是否只声明修改资源库 Catalog 和新的 PNG。
3. 处理所有 error；warning 可以继续，但必须理解风险。
4. 点击“创建资源”。

创建成功不等于已经进入 FairyGUI 包。此时只是完成资源库登记。

## 4. 编辑已有资源

1. 在左侧选择资源。
2. 在“基础信息 / FairyGUI / 替换约束”标签中修改语义字段。
3. 展开“使用与替换预检”，检查推荐参数与 PNG 是否一致。
4. 点击“保存更改”。

保存使用 Catalog Hash 做并发保护。若其他工具或 Development Agent 已修改 Catalog，当前保存会被阻断；刷新后重新应用编辑，不要覆盖新版本。

## 5. 替换 PNG

1. 选择目标资源。
2. 点击预览区右上角“替换 PNG”。
3. 选择候选 PNG。
4. 点击“预览计划”，检查尺寸、Alpha、最小尺寸和路径诊断。
5. 计划无阻断项后点击“替换 PNG”。

替换只覆盖资源库中原路径的 PNG，不修改 Catalog 语义，也不直接修改 FairyGUI 包。Composer-owned package 需要重新统一预检并由 helper Apply/Publish；legacy package 需要在 FairyGUI Editor 中重新导入并发布。

## 6. 生成 legacy FairyGUI 接入审计计划

新建或迁移 Composer-owned 界面时不以本节计划作为主创作入口；应直接在 UI Composer 中通过稳定 `resourceId` 引用资源。本节用于检查 legacy package 的现有导入状态、内容漂移和参数漂移。

### 6.1 生成计划

1. 选择资源并打开“FairyGUI”标签。
2. 若顶部显示“未保存”，先保存语义修改。
3. 在“FairyGUI 接入”区域选择目标包。
4. 点击“生成接入计划”。

目标包列表来自 `FGUIProject/assets/*/package.xml`，不要求目标包已经拥有 `ui-art-slices.json`。

### 6.2 状态说明

| 状态 | 含义 | 后续动作 |
|------|------|----------|
| 未导入 | package.xml 中没有对应图片 | Composer-owned 目标回到 Composer；legacy 目标按建议路径手工导入 |
| 已精确导入 | 建议路径、声明和 PNG 内容一致 | 检查参数/组件使用点 |
| 已有等价资源 | 相同 PNG 内容已在其他包内路径声明 | 优先复用现有资源，避免重复导入 |
| 内容漂移 | 包内 PNG 与资源库源图内容不同 | 判断以资源库还是包内版本为准，然后重新导入 |
| 名称冲突 | 同名图片指向不同内容 | 调整包内名称或路径，不要覆盖未知资源 |
| 导入文件缺失 | package.xml 有声明，但 PNG 不存在 | 修复包内源文件 |
| 存在未登记文件 | 建议目标路径已有 PNG，但 package.xml 未声明 | 在 FairyGUI Editor 中导入或清理孤儿文件 |
| 包内路径不安全 | package.xml 图片路径逃逸目标包 | 修复 XML/包工程后再继续 |

参数状态：

| 状态 | 含义 |
|------|------|
| 一致 | package-local sidecar 与资源库推荐参数一致 |
| 漂移 | Policy、设计尺寸、保持比例或 NineSlice 参数不同 |
| 未跟踪 | 目标包没有 sidecar，或图片尚未登记到 sidecar |
| 未导入 | 图片尚未进入目标包 |

### 6.3 导出配方

- **复制配方**：复制面向人和 Development Agent 的 Markdown。
- **下载 JSON**：保存结构化计划，适合工具链和自动审阅。
- **下载 Markdown**：保存可直接放入 Issue、PR 或交接记录的操作配方。

配方不含时间戳和绝对路径；同一输入应产生稳定结果。JSON 中所有 `wouldModify*` 字段都必须是 `false`。

## 7. 接入真实 FairyGUI

### 7.1 Composer-owned 或新 player-facing 界面

1. 打开 UI Composer，选择或新增 allowlisted Screen Blueprint。
2. 在资源面板按稳定 `resourceId` 选择资源，不填写或猜测 FairyGUI image id。
3. 通过组件配方或 Image 节点使用资源，完成 binding contract 分配和布局。
4. 运行统一预检，检查资源、配方、蓝图、contract、mapping 和 XML hash。
5. 由 FairyGUI helper Apply/Publish；浏览器和 Development Agent 不直接写 package XML 或发布产物。
6. 接入运行时前补齐 Resource Catalog、generated manifest/binder 和 Unity 真实包加载/交互验证。

详细步骤见 `Docs/Guides/FAIRYGUI_UI_COMPOSER_USER_GUIDE.md` 和 `Docs/FAIRYGUI_UI_COMPOSER.md`。

### 7.2 非 Composer-owned legacy package

接入计划生成后，仍需人工执行：

1. 打开配方中的目标 FairyGUI package。
2. 从配方的 `sourcePath` 导入 PNG。
3. 使用 `suggestedPackageFolder` 和 `suggestedResourceName` 作为目标位置与名称。
4. 让 FairyGUI Editor 分配 image resource id；不要在外部工具中猜测或手写 id。
5. 在组件中按 `recommendedFairyGuiUsage` 设置设计尺寸、保持比例和 NineSlice。
6. 将图片连接到真实组件使用点。
7. 使用项目现有 FairyGUI helper plugin 发布目标包。
8. 返回 UI Art Resource Library 重新生成接入计划，确认内容和参数状态。
9. 运行 package audit / generated binder 检查。

真实 FairyGUI 面板和发布还必须遵守 `FGUIProject/AGENTS.md` 与 `Docs/Guides/FAIRYGUI_BINDER_GENERATION_GUIDE.md`。

## 8. Development Agent 使用规则

Development Agent 使用一张切图前应先读取生成的 JSON 或 Markdown 配方，并遵守：

1. `resourceId` 是资源库身份，不等同于 FairyGUI image id。
2. `sourcePath` 是资源库源图；`suggestedTargetPath` 只是建议，不代表文件已存在。
3. `status` 不是 `ImportedExact` 时，不得声称资源已接入目标包。
4. `usageStatus` 是 `Drift` 或 `Untracked` 时，应保留人工 FairyGUI 参数复核。
5. 不手写 `package.xml`、component XML、atlas 或 `*_fui.bytes`。
6. 不修改 generated binder；Composer-owned 结构变化走 UI Composer/helper，legacy 结构变化才走 FairyGUI Editor 和既有生成流程。
7. 配方中的 `requiresFairyGuiPublish=true` 时，未发布不能标记为运行时可用。

## 9. 当前资源库

`MxUiFoundation` 当前包含 40 个语义资源，覆盖基础按钮、面板、分隔线、图标、表单、Combat HUD 和 Runtime AI Lab authoring。完整清单以 `ui-art-resources.json` 为权威，不在本文复制。

最初的流程测试资源继续保留：

| 字段 | 值 |
|------|----|
| `resourceId` | `ui.button.generic.base` |
| 路径 | `images/buttons/button_generic_240x64.png` |
| 尺寸 | `240x64` |
| 策略 | `NineSlice` |
| `scale9Grid` | `24,18,192,28` |

该图片只用于验证资源库和编辑器流程，不用于 CharacterTest 正式菜单。正式按钮、HUD 和 Runtime AI Lab 资源使用各自稳定的语义条目与约束。

## 10. 排障

### 页面无法加载资源

- 确认 Authoring Server 正在运行。
- 从 EditorHub 重新启动当前版本服务。
- 检查 `/api/authoring/ui-art/resources/capabilities` 的版本是否匹配页面。

### 保存按钮不可用

- 没有未保存改动时按钮保持禁用。
- NineSlice 超出 PNG 范围、Alpha 约束矛盾等 error 会阻止保存。

### 接入计划按钮不可用

- 先选择资源和目标包。
- 先保存当前语义改动；计划只读取已保存 Catalog。

### 计划一直显示未导入

- 计划不会自动复制 PNG。
- 确认已通过 FairyGUI Editor 导入并保存 package source。
- 确认 package.xml 中的文件名和路径与配方一致。

### 参数显示未跟踪

- 目标包可能没有 `ui-art-slices.json`。
- 或图片已导入，但 package-local sidecar 尚未登记该 image resource id/source path。
- 这不会自动证明运行时错误，但需要人工检查实际组件参数。

## 11. 验收清单

- [ ] 资源库状态为 0 error。
- [ ] PNG、设计尺寸和推荐策略一致。
- [ ] NineSlice 位于 PNG 范围内。
- [ ] 替换约束与资源用途一致。
- [ ] 接入计划不包含绝对路径，所有 `wouldModify*` 为 false。
- [ ] FairyGUI image id 由 FairyGUI Editor 分配。
- [ ] 组件实际使用参数已复核。
- [ ] 目标包已通过现有 helper plugin 发布。
- [ ] package audit 和相关 binder/generated checks 通过。

相关命令：

```powershell
dotnet run --project .\Tools\MxFramework.Authoring\tests\MxFramework.Authoring.Tests\MxFramework.Authoring.Tests.csproj
dotnet run --project .\Tools\MxFramework.NoEngineTests\FairyGUI.Manifest.Tests\FairyGUI.Manifest.Tests.csproj -- --check-art-slices
dotnet run --project .\Tools\MxFramework.NoEngineTests\FairyGUI.Manifest.Tests\FairyGUI.Manifest.Tests.csproj -- --check-generated
git diff --check
```
