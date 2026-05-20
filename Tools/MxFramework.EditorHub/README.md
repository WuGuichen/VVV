# MxFramework Editor Hub

外部编辑器中心用于一键启动本地 Authoring server，并集中打开当前仓库里的外部 Web 编辑器。

## 启动

macOS / Linux:

```bash
Tools/MxFramework.EditorHub/start-editor-hub.sh
```

Windows:

```bat
Tools\MxFramework.EditorHub\start-editor-hub.bat
```

macOS Finder 可双击：

```text
Tools/MxFramework.EditorHub/start-editor-hub.command
```

默认：

- port: `4873`
- character package: `Tools/MxFramework.Authoring/samples/character-iron-vanguard`
- URL: `http://127.0.0.1:4873/Tools/MxFramework.EditorHub/web/`

可指定端口和角色包：

```bash
Tools/MxFramework.EditorHub/start-editor-hub.sh 4883 Tools/MxFramework.Authoring/samples/character-iron-vanguard
```

## 当前入口

- Buff Authoring Editor：Buff / ModPackage 示例编辑器。
- CharacterStudio：角色资源包编辑器。
- 资源库 / 资源计划状态：读取当前 Authoring API，展示资源库、运行时计划和诊断概要。

独立 Resource Library Editor 还未实现。Hub 会保留入口位，但不会把完整资源库编辑功能塞回 CharacterStudio。
