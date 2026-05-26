# MxFramework Authoring Resource Manager

独立资源管理器用于查看 Authoring 阶段的全局资源视图、provider 状态、运行时资源计划、引用关系和诊断信息。它的目标是服务 CharacterStudio、Animation Editor、Combat/VFX Editor、UI/Config Editor 等所有外部编辑器。

当前目录和启动脚本仍沿用旧 `ResourceLibrary` 路径以减少迁移噪音，但界面语义已经改为资源管理器。默认 Iron Vanguard 只是包筛选 / 消费者上下文，不代表资源归属于该角色包。

设计目标已经调整为全局 Authoring Resource Manager。角色包资源只是 `characterPackage` provider，Unity AssetDatabase、现有 `MxFramework.Resources.ResourceCatalog`、FMOD snapshot、external import staging 和 generated assets 也应进入统一资源视图。

导入、重导和替换资源已经通过 Authoring API gate 开放；删除和标签编辑仍等待 reference graph delete guard 后再开放。

## Build Profile / Bundle Plan

Resource Manager 已接入 Global Resource Build Profile 的第一段 authoring 流程：

- “加入构建 Profile”：把当前选中资源加入 `GlobalResourceBuildProfile` 草稿。
- “移出构建 Profile”：从草稿中移除当前资源。
- “保存 Profile”：通过 Authoring API 校验后写入 `Assets/Config/MxFramework/ResourceProfiles/global_resource_build_profile.json`。
- Build Profile 面板：编辑 `delivery mode`、`override mode`、`override value`、`bundle group hint`、`bundle rule`、`preload groups` 和 `labels`。
- Bundle Planner：预览内部 bundle、资源数量、依赖 bundle 和诊断。

典型流程：

1. 启动 Resource Manager。
2. 选择左侧资源项，确认 Overview / Runtime / Diagnostics 没有阻断问题。
3. 点击“加入构建 Profile”。
4. 在 Build Profile 面板补充 bundle 和 preload 意图。
5. 点击“保存 Profile”。
6. 查看 Bundle Planner 摘要，确认预览结果符合预期。

Bundle Planner 是预览面，不写 AssetBundle、不写 `StreamingAssets`。真正生成 Player 产物仍在 Unity 中执行：

```text
MxFramework/Resources/Validate Global Resource Build Profile
MxFramework/Resources/Build Global Player Resource Catalog
```

当前已完成的是 Profile authoring、保存校验、Bundle Plan 预览和 Unity 菜单本地 Player 构建入口。未完成的是通用 AB Builder 工作台、批量/增量构建、远端热更 manifest、CDN 发布、签名、加密、断点续传和 YooAsset adapter。默认路线仍是 MxFramework Catalog + AssetBundleProvider / RemoteBundleProvider；YooAsset 不是默认路线，本仓库不做 adapter。

## 导入规则

资源管理器的导入面板先选择“导入类型”，再选择单个文件或文件夹。文件夹导入会先走 external import staging 预检：

- `.meta`、`.DS_Store`、隐藏目录等编辑器元数据会计入“忽略元数据”，不会成为资源。
- 当前导入类型会作为最终归类依据。例如选择“动画 Clip/Group”后，`.anim`、`.glb`、`.gltf`、`.json` 会导入为 `animation / animationClipGroup`；`.fbx` 只作为模型/外部转换来源，不归类为动画 Clip 或 Clip Group。
- 不匹配当前导入类型、格式不支持、重复 hash 或超过大小限制的文件会计入“跳过非匹配”或诊断，不会自动写入资源目录。
- 资源浏览器只负责准备和提供资源列表；角色、动画、战斗等编辑器通过资源选择器引用这些资源。

## 启动

macOS / Linux:

```bash
Tools/MxFramework.ResourceLibrary/start-resource-library.sh
```

Windows:

```bat
Tools\MxFramework.ResourceLibrary\start-resource-library.bat
```

macOS Finder 可双击：

```text
Tools/MxFramework.ResourceLibrary/start-resource-library.command
```

默认：

- port: `4873`
- default package context: `Tools/MxFramework.Authoring/samples/character-iron-vanguard`
- URL: `http://127.0.0.1:4873/Tools/MxFramework.ResourceLibrary/web/?package=Tools/MxFramework.Authoring/samples/character-iron-vanguard`

可指定端口和角色包：

```bash
Tools/MxFramework.ResourceLibrary/start-resource-library.sh 4884 Tools/MxFramework.Authoring/samples/character-iron-vanguard
```

## 环境变量

- `MXFRAMEWORK_RESOURCE_LIBRARY_PORT`: 默认端口。
- `MXFRAMEWORK_RESOURCE_LIBRARY_PACKAGE`: 默认包筛选 / 消费者上下文路径。
- `MXFRAMEWORK_RESOURCE_LIBRARY_OPEN_BROWSER`: 设为 `0` 时不自动打开浏览器。

## Authoring Server

启动脚本会检查：

- 仓库根目录。
- `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj`。
- .NET 9+ SDK。
- 角色包目录和 `manifest.json`。
- 端口占用。
- 已运行且匹配 Authoring Resource Manager API 的 Authoring server。

如果指定端口上已有 Authoring server 且资源列表 API 和 inspect API 都可访问，脚本会直接打开 Resource Manager Editor。

如果页面显示“服务未就绪”，优先重新运行启动脚本。浏览器页面不能直接执行本地 shell/bat 脚本；需要从终端运行 `.sh` / `.bat`，或在 macOS Finder 双击 `.command`。

如果端口已被旧 Authoring server 占用，脚本会提示该进程不匹配当前 Authoring Resource Manager API。此时可以停止旧进程，或换端口启动：

```bash
Tools/MxFramework.ResourceLibrary/start-resource-library.sh 4884 Tools/MxFramework.Authoring/samples/character-iron-vanguard
```

## 验证

```bash
bash -n Tools/MxFramework.ResourceLibrary/start-resource-library.sh
```
