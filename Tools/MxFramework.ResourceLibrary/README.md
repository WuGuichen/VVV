# MxFramework Authoring Resource Manager

独立资源管理器用于查看 Authoring 阶段的全局资源视图、provider 状态、运行时资源计划、引用关系和诊断信息。它的目标是服务 CharacterStudio、Animation Editor、Combat/VFX Editor、UI/Config Editor 等所有外部编辑器。

当前目录和启动脚本仍是旧 MVP 实现，默认打开 Iron Vanguard 角色包 scope。后续执行可以直接重命名为 `Tools/MxFramework.ResourceManager` / `start-resource-manager.*`，也可以替换过时样例资源；本阶段不要求兼容旧角色包字段、旧 `/api/character/resources` API 或旧工具命名。

设计目标已经调整为全局 Authoring Resource Manager。角色包资源只是 `characterPackage` provider，Unity AssetDatabase、现有 `MxFramework.Resources.ResourceCatalog`、FMOD snapshot、external import staging 和 generated assets 也应进入统一资源视图。

写入型资源管理入口在 MVP 阶段保持禁用，并通过 Authoring API gate 补齐后再开放。

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
- default package scope: `Tools/MxFramework.Authoring/samples/character-iron-vanguard`
- URL: `http://127.0.0.1:4873/Tools/MxFramework.ResourceLibrary/web/?package=Tools/MxFramework.Authoring/samples/character-iron-vanguard`

可指定端口和角色包：

```bash
Tools/MxFramework.ResourceLibrary/start-resource-library.sh 4884 Tools/MxFramework.Authoring/samples/character-iron-vanguard
```

## 环境变量

- `MXFRAMEWORK_RESOURCE_LIBRARY_PORT`: 默认端口。
- `MXFRAMEWORK_RESOURCE_LIBRARY_PACKAGE`: 默认角色资源包 scope 路径。
- `MXFRAMEWORK_RESOURCE_LIBRARY_OPEN_BROWSER`: 设为 `0` 时不自动打开浏览器。

## Authoring Server

启动脚本会检查：

- 仓库根目录。
- `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj`。
- .NET 9+ SDK。
- 角色包目录和 `manifest.json`。
- 端口占用。
- 已运行且匹配当前旧 MVP API 的 Authoring server。

如果指定端口上已有 Authoring server 且资源列表 API 和 inspect API 都可访问，脚本会直接打开 Resource Manager Editor。

如果页面显示“服务未就绪”，优先重新运行启动脚本。浏览器页面不能直接执行本地 shell/bat 脚本；需要从终端运行 `.sh` / `.bat`，或在 macOS Finder 双击 `.command`。

如果端口已被旧 Authoring server 占用，脚本会提示该进程不匹配当前旧 MVP API。此时可以停止旧进程，或换端口启动：

```bash
Tools/MxFramework.ResourceLibrary/start-resource-library.sh 4884 Tools/MxFramework.Authoring/samples/character-iron-vanguard
```

## 验证

```bash
bash -n Tools/MxFramework.ResourceLibrary/start-resource-library.sh
```
