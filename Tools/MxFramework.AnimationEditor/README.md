# MxFramework Animation Editor

Animation Editor 是独立动画配置编辑器。CharacterStudio 使用的 `globalWeaponLibrary` 模式维护 Global Weapon Library 3.0 中的完整 `GlobalAnimationConfiguration`，提供全局配置列表、创建、复制、受保护删除、引用数、修改状态和统一 revision 冲突处理，并完整复用 Set/Layer/Group/Clip/Blend/Timeline/Profile/Compatibility/Warmup 编辑器。旧 `animation_authoring.json` 数据源保留为 **Legacy / Migration**，不再是 Character Application 5.0 的编译事实源。编辑器消费 Resource Manager 的资源选择链路，不直接管理资源生命周期，也不修改 `.anim`、`.fbx`、`.glb` 源文件。

当前切片提供：

- 动画包列表、加载、保存、校验。
- Set / Group / Clip 树和基础 Inspector。
- 中间工作区按任务拆分为“资源映射”“Locomotion / Blend”“动作时间轴”“预览校验”“运行时高级”，避免把所有底层 DTO 同时摊开。
- Clip mapping 表，支持 `SourceSelection`、`SourceSubClipId`、`SourceClipName`、Loop、Speed、RootMotionPolicy、tags。
- Group 级 1D line / 2D plane Blend 编辑器，Blend point 只引用当前 Group 内的本地 `clipId`，并显示缺失引用、重复坐标、点位不足等本地诊断。
- Group 级 Timeline event 编辑器，支持创建绑定本地 `clipId` 的 `AnimationTimelineAuthoring`，编辑 `eventId`、`clipId`、`timeDomain`、`time`、`eventKind`、`payloadJson`，并用 Seconds / Normalized / PresentationFrame / CombatFrame 轨道显示事件点位。
- `Animation.SourceClip`、`Animation.EventVfx`、`Animation.EventAudioCue` 字段级资源选择器；AudioCue 事件走 AudioCue 选择契约，不按普通 AudioClip 处理。
- Timeline event 本地诊断和 JSON / AI context 复制。
- Preview / Bake / Compatibility 工作流面板，显示 preview target、reference path、bake artifact summary、skeleton/avatar/clip 兼容性诊断；Preview 只作为编辑期辅助，不写 Unity scene/prefab。
- EditorHub 和 CharacterStudio 跳转入口。

真实 3D 播放器、Bake 执行、Compiler 集成和 CharacterStudio 迁移由后续里程碑任务完成。

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
- package context: `Tools/MxFramework.Authoring/samples/character-codex-flow-test`
- URL: `http://127.0.0.1:4873/Tools/MxFramework.AnimationEditor/web/?package=Tools/MxFramework.Authoring/samples/character-codex-flow-test`
- Global Library URL: `http://127.0.0.1:4873/Tools/MxFramework.AnimationEditor/web/?source=globalWeaponLibrary&configurationId=<animationConfigurationId>&package=<character-package>`

Global Library 保存使用 `POST /api/weapon-library/configuration/validate|save` 和统一 `expectedRevision`。`409` 冲突必须刷新后重放修改，不自动覆盖或字段级合并。保存成功后编辑器向打开它的 CharacterStudio 发送 `mx-global-configuration-saved` 消息。

可指定端口和工作上下文：

```bash
Tools/MxFramework.AnimationEditor/start-animation-editor.sh 4885 Tools/MxFramework.Authoring/samples/character-codex-flow-test
```

## 环境变量

- `MXFRAMEWORK_ANIMATION_EDITOR_PORT`: 默认端口。
- `MXFRAMEWORK_ANIMATION_EDITOR_PACKAGE`: 默认工作上下文路径。当前阶段通常指角色包或动画 authoring 包路径。
- `MXFRAMEWORK_ANIMATION_EDITOR_OPEN_BROWSER`: 设为 `0` 时不自动打开浏览器。

## 验证

```bash
node Tools/MxFramework.AnimationEditor/scripts/smoke.mjs
bash -n Tools/MxFramework.AnimationEditor/start-animation-editor.sh
```
