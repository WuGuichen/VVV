# MxFramework Animation Editor

Animation Editor 是独立动画配置编辑器，用于维护 `AnimationAuthoringPackage`、AnimationSet、AnimationGroup 和 Clip mapping。它消费 Authoring Resource Manager 提供的资源选择器，不直接管理全局资源，也不修改 `.anim`、`.fbx`、`.glb` 源文件。

当前切片提供：

- 动画包列表、加载、保存、校验。
- Set / Group / Clip 树和基础 Inspector。
- Clip mapping 表，支持 `SourceSelection`、`SourceSubClipId`、`SourceClipName`、Loop、Speed、RootMotionPolicy、tags。
- Group 级 1D line / 2D plane Blend 编辑器，Blend point 只引用当前 Group 内的本地 `clipId`，并显示缺失引用、重复坐标、点位不足等本地诊断。
- Group 级 Timeline event 编辑器，支持创建绑定本地 `clipId` 的 `AnimationTimelineAuthoring`，编辑 `eventId`、`clipId`、`timeDomain`、`time`、`eventKind`、`payloadJson`，并用 Seconds / Normalized / PresentationFrame / CombatFrame 轨道显示事件点位。
- `Animation.SourceClip`、`Animation.EventVfx`、`Animation.EventAudioCue` 字段级资源选择器；AudioCue 事件走 AudioCue 选择契约，不按普通 AudioClip 处理。
- Timeline event 本地诊断和 JSON / AI context 复制。
- EditorHub 和 CharacterStudio 跳转入口。

3D 预览、Bake、Compiler 集成和 CharacterStudio 迁移由后续里程碑任务完成。

## 启动

macOS / Linux:

```bash
Tools/MxFramework.AnimationEditor/start-animation-editor.sh
```

Windows:

```bat
Tools\MxFramework.AnimationEditor\start-animation-editor.bat
```

macOS Finder 可双击：

```text
Tools/MxFramework.AnimationEditor/start-animation-editor.command
```

默认：

- port: `4873`
- package context: `Tools/MxFramework.Authoring/samples/character-iron-vanguard`
- URL: `http://127.0.0.1:4873/Tools/MxFramework.AnimationEditor/web/?package=Tools/MxFramework.Authoring/samples/character-iron-vanguard`

可指定端口和包上下文：

```bash
Tools/MxFramework.AnimationEditor/start-animation-editor.sh 4885 Tools/MxFramework.Authoring/samples/character-iron-vanguard
```

## 环境变量

- `MXFRAMEWORK_ANIMATION_EDITOR_PORT`: 默认端口。
- `MXFRAMEWORK_ANIMATION_EDITOR_PACKAGE`: 默认包上下文路径。
- `MXFRAMEWORK_ANIMATION_EDITOR_OPEN_BROWSER`: 设为 `0` 时不自动打开浏览器。

## 验证

```bash
node Tools/MxFramework.AnimationEditor/scripts/smoke.mjs
bash -n Tools/MxFramework.AnimationEditor/start-animation-editor.sh
```
