# MxFramework Resource Library Editor

独立资源库编辑器用于查看角色资源包中的资源库、运行时资源计划、引用关系和诊断信息。写入型资源管理入口在 MVP 阶段保持禁用，并通过 Authoring API gate 补齐后再开放。

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
- character package: `Tools/MxFramework.Authoring/samples/character-iron-vanguard`
- URL: `http://127.0.0.1:4873/Tools/MxFramework.ResourceLibrary/web/?package=Tools/MxFramework.Authoring/samples/character-iron-vanguard`

可指定端口和角色包：

```bash
Tools/MxFramework.ResourceLibrary/start-resource-library.sh 4884 Tools/MxFramework.Authoring/samples/character-iron-vanguard
```

## 环境变量

- `MXFRAMEWORK_RESOURCE_LIBRARY_PORT`: 默认端口。
- `MXFRAMEWORK_RESOURCE_LIBRARY_PACKAGE`: 默认角色资源包路径。
- `MXFRAMEWORK_RESOURCE_LIBRARY_OPEN_BROWSER`: 设为 `0` 时不自动打开浏览器。

## Authoring Server

启动脚本会检查：

- 仓库根目录。
- `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj`。
- .NET 9+ SDK。
- 角色包目录和 `manifest.json`。
- 端口占用。
- 已运行的兼容 Authoring server。

如果指定端口上已有 Authoring server 且资源库列表 API 和 inspect API 都可访问，脚本会直接打开 Resource Library Editor。

如果页面显示“服务未就绪”，优先重新运行启动脚本。浏览器页面不能直接执行本地 shell/bat 脚本；需要从终端运行 `.sh` / `.bat`，或在 macOS Finder 双击 `.command`。

如果端口已被旧 Authoring server 占用，脚本会提示该进程不兼容。此时可以停止旧进程，或换端口启动：

```bash
Tools/MxFramework.ResourceLibrary/start-resource-library.sh 4884 Tools/MxFramework.Authoring/samples/character-iron-vanguard
```

## 验证

```bash
bash -n Tools/MxFramework.ResourceLibrary/start-resource-library.sh
```
