# 角色头像与立绘契约

角色数字包的正式游戏展示图配置位于 `Authoring/CharacterPackages/character-<id>/config/character_application.json` 的 `presentationImages`。这是现有 applicationConfig 的可选增量字段；不另建角色身份表、配置库或 UI 框架。

```json
{
  "presentationImages": {
    "schemaVersion": "1.0",
    "portraitResourceKey": "char.000001.texture.ui_portrait_placeholder",
    "standingResourceKey": "char.000001.texture.ui_standing_placeholder",
    "variants": [
      {
        "variantId": "appearance.winter",
        "portraitResourceKey": "",
        "standingResourceKey": ""
      }
    ]
  }
}
```

## 资源与身份

- 头像与立绘独立选择。Web 头像框保持 1:1，立绘框保持 2:3、完整适配；图片不要求裁成框比例，不拉伸。
- key 必须命中本包 `resource_catalog.json` 已登记的 `texture`，对应 usage 为 `characterPortrait` 或 `characterStanding`，sourceFormat 为 png/jpg/jpeg，资源 `variant` 显式为空字符串。发布为 `Texture2D`，runtime 用同一稳定 ID、typeId=`Texture2D`、variant=`""` 加载。路径只由资源目录与正式发布器解析。
- `variantId` 是明确的外观身份，大小写敏感且唯一，使用稳定 key 语法。逐槽空覆盖继承基础；未知外观退回基础；基础空槽显示可见占位。当前整备列表使用基础外观，未从战术变体、角色名字推断外观 ID。
- `CharacterPrepare_Characters` 的 UUID 注册、current/candidate 概念图不构成正式游戏图授权，也不构成数字包映射。没有确证映射时，数字包使用占位图；未来真实图需要明确身份对应、资源登记及人工选择。未改角色库背景、真实图片或战术数值。
- 000001/000002/000003/000004/000008/000009 提供独立 256×256 头像与 256×384 立绘源占位。资源 provenance 标注 `game-display-placeholder`，preview.isPlaceholder=true。准备脚本 `Tools/MxFramework.CharacterStudio/scripts/prepare-presentation-placeholders.mjs` 只填空槽、不覆盖已有图片与明确选择。

## 配置、发布与消费者

CharacterStudio 左侧“头像与立绘”直接编辑该字段，通过现有保存事务、撤销、版本冲突与完整包校验保存。选项限制为槽用途正确的正式目录资源；失效 key 保留并标示错误；缺图和 HTTP 加载失败显示可见占位。外观覆盖可新增、编辑、移除。

Authoring 编译器将字段投影到现有 `CharacterPresentationProfileConfig.PresentationImages`，同时补入现有 `ResourceKeys`，即使 applicationConfig.ResourceKeys 只限制了模型。基础与覆盖槽参与 source/generated hash；资源计划包含所有图键并使用 `UiDeferred` 可选组与占位失败策略。schema 不兼容、重复/非法外观 ID、缺 key、用途/类型/格式/资源变体错误阻止保存/发布。旧包无此字段保持兼容。

实际游戏调用链为 `TacticalCardDemoScene` → tactical slice coordinator/profile → `CharacterTestUiRuntime.Configure` → `CharacterTestFairyGuiTacticalDeckPanelHost` → `CharacterTestFairyGuiTacticalPreparationView.RenderDrawer(Characters)` → `RenderCharacters`。目录定义从 imported presentation profile 携带数据，列表沿用已发布的 `TacticalPreparationChoice` 的 `icon` loader。按稳定 key 异步加载，加载中/空配置/失败显示本地头像轮廓；行复用立即清除上一张图，完成回调校验当前 key；同 key 共用句柄，view dispose 时释放句柄及 FairyGUI 包装，不销毁资源系统拥有的图片。

该组件已经有 icon 和 title，因此不需要重新 Apply/Publish FairyGUI 包。本任务不增加战术卡图片、不恢复退役角色选择窗口。现行游戏没有立绘展示位：立绘配置、Web 预览、发布及资源目录可用，但未声称游戏全屏立绘已展示。

## 正式导入交接（仅 Editor 唯一操作方）

任务实施期间另一个线程独占 Unity Editor；下面命令没有由本线程正式执行。需要在其确认安全 EditMode 后执行。

项目根 PowerShell：

```powershell
dotnet build Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj --no-restore -o .agent/character-images/publish-cli -v:q
foreach ($id in @('000001','000002','000003','000004','000008','000009')) {
    dotnet .agent/character-images/publish-cli/authoring.dll character import-unity --package "Authoring/CharacterPackages/character-$id" --project-root . --check-files --report-out ".agent/character-images/import-published-$id"
    if ($LASTEXITCODE -ne 0) { throw "Import failed: $id" }
}
```

也可使用已验证的 `.agent/character-images/verified/authoring.dll`，无需再次构建。现有 import-unity bridge 是生成资产写入权威，生成 `Assets/MxFrameworkGenerated/CharacterPackages/<id>/config/character_config_patch.json`、`resource_catalog_mapping.json`、`character_resource_plan.json`、`runtime_resource_catalog.json` 等相关投影/收据，新增该包 `resources/ui/portrait_placeholder.png` 与 `standing_placeholder.png`，并通过现有 ResourceMappingCharacterCoordinator 合并全局资源映射 profile。完整 import 可能更新其它投影的源 hash 与收据；不手工编辑 Generated 内容、不覆盖当前并行 profile 改动。

随后由 Editor 唯一操作方执行 refresh/compile（优先 `validate_unity_scripts` 完整回执），生成新增 Assets/Packages C#、Generated PNG/目录对应 `.meta`，等待 ready 和 Console error=0；不要自行写 GUID。确认六包配置字段、12 个目录条目的 stable key / Texture2D / 空 variant 和源文件 hash 一致。

Player 正式资源发布使用 Editor 菜单 **MxFramework/Resources/Validate Global Resource Mapping**，验证后 **MxFramework/Resources/Build Global Player Resources**。准确代码入口为 `MxFramework.Editor.GlobalPlayerResourceBuildProfileBuilder.ValidateGlobalProfile()` 与 `BuildGlobalPlayerResourceCatalog()`。输出包括 `Assets/StreamingAssets/MxFramework/Resources/global_runtime_catalog.json`、`global_catalog_mounts.json`、`global_preload_groups.json`、`global_bundle_dependencies.json`、`global_resource_artifact_set.json` 与 `Bundles/`，以及 `Assets/Config/MxFramework/ResourceBuildReports/global_resource_build_report.json`。以当前全局映射生成，不能复制旧缓存混用；需要确认12个图 key进入产物。

原生 EditMode fixture：`MxFramework.Tests.UI.CharacterPresentationImageTests`，3项为 `AppearanceOverride_InheritsEachSlotIndependently`、`MissingKey_LeavesVisibleFallback`、`ReusedRow_ResetsImageAndReleasesSharedHandleOnce`。现有 MxFramework.Tests asmdef，无新增程序集。

用户视觉验收：打开现有整备的“角色库”抽屉，检查角色名旁头像；换行/重开抽屉不串图，缺资源仍可看到轮廓。游戏原生 compile、3项测试、正式资源导入与Player发布在本线程离线阶段未执行，须由父线程/Editor操作方补齐回执。

## 离线验证证据

- 独立测试入口 `Tools/MxFramework.Authoring/tests/CharacterPresentation.Tests`：24项契约断言（独立槽、继承、校验、DTO往返、实际Web保存处理器隔离包持久化/拒绝无效引用、hash、发布字段、受限 ResourceKeys、正式runtime目录与空资源variant、数值不变），离线构建0警告0错误。
- 六包 `import-unity --dry-run --check-files`：全部 canWrite/canSpawn=true、conflicts/errors=0；000003 WarningOnly 是既有胶囊XZ与 locomotion 校准诊断。报告 `.agent/character-images/import-final-<id>/import_report.json`，汇总 `publication-preflight.json`。
- 本机 Edge+Playwright 实际 Web 预览：独立槽比例、图片加载、变体继承、请求失败可见占位、无页面异常；截图 `.agent/character-images/web-preview.png`、`web-fallback.png`，回执 `web-preview-report.json`。这些为本地工作证据，不版本化、不网上传。

## 已运行 Web 服务升级

父线程授权后只读核实：原4873服务 PID72988 为本仓库 `bin/Debug/net9.0/authoring.exe editor serve`，同项目/tools根目录、默认包 `Tools\MxFramework.Authoring\samples\character-codex-flow-test`；HTTP角色state响应确实丢弃新字段。Http.sys绑定核实了该PID与端口，并确认活动/排队请求均0、无子进程，才短暂重启该明确服务。

现服务使用独立最终输出 `.agent/character-images/publish-cli/authoring.exe`，保留原全部参数和4873端口（升级当时PID148236）。10个既有角色/动画/Buff/UI Composer/战术卡/战斗设计/动作伤害与静态入口回归全部HTTP200并保持schema；真实HTTP隔离包保存往返保留基础图/外观字段，无效图引用返回验证阻断且不改变已保存文件。未保存用户源包、现有浏览器草稿，也未触碰Unity。回执：`.agent/character-images/service-upgrade.json`、`service-regression.json`，升级前队列快照 `service-idle-before-upgrade.txt`。原exe保留用于故障回滚，正常启动脚本未来重新构建当前源码即可带入新字段。
