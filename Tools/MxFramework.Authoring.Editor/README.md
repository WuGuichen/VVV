# MxFramework Authoring Editor

外部主创编辑器本地 MVP。

当前阶段是本地 Web UI + 本地 API，用于验证 Buff 创建流程、Manifest 读取、Schema / Enum 展示、示例 ModPackage、校验报告和合并预览展示。

## 启动

在仓库根目录运行：

Linux / macOS：

```bash
Tools/MxFramework.Authoring.Editor/start-authoring-editor.sh [port]
```

Windows（cmd / PowerShell 双击或命令行均可）：

```bat
Tools\MxFramework.Authoring.Editor\start-authoring-editor.bat [port]
```

未指定端口时默认 `4873`。脚本依赖 `dotnet` SDK 已加入 `PATH`。

打开：

```text
http://127.0.0.1:4873/Tools/MxFramework.Authoring.Editor/web/
```

## 当前范围

- 读取 `Tools/MxFramework.Authoring/samples/project-manifest/project-authoring-manifest.json`。
- 读取 `Tools/MxFramework.Authoring/samples/buff-preview/mod.json`。
- 读取 `Tools/MxFramework.Authoring/samples/buff-preview/patches/buff.patch.json`。
- 如果已生成报告包，读取 `Tools/MxFramework.Authoring/samples/buff-preview/reports/*`。
- 展示 Buff Workflow、Schema 字段、BuffType、AddType、草稿字段、校验报告和合并预览。
- 按字段分组展示公共字段、目标 / 堆叠 / 持续、类型专属字段和表现资源。
- 根据当前 BuffType 动态显示需要填写的字段。
- 编辑示例 Buff Patch 字段，并即时提示当前类型下缺失的必填字段。
- 保存示例 Buff Patch。
- 重新生成 report bundle。
- 连接本机 Unity Preview Server，触发运行时预览并展示结构化结果 / 错误。

当前运行时预览已接入到 Preview Server 雏形：可以握手和加载 Patch；在未接真实 `IBuffFactory` 前，应用 Buff 会返回结构化 `2003` 错误。当前不接 AI 服务。
