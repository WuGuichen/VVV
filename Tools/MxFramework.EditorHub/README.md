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
- Animation Editor：动画 Set / Group / Clip Mapping 独立编辑器。
- 资源管理器：打开独立 Authoring Resource Manager，并携带默认包上下文作为筛选条件。
- 资源管理器 / 资源计划状态：读取当前 Authoring API，展示 provider、资源项、运行时计划和诊断概要。

完整资源浏览和管理入口进入独立资源管理器；CharacterStudio 保持角色字段级资源选择和装配职责。
