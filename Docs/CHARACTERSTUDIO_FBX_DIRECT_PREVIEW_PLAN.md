# CharacterStudio FBX 直接预览实现说明

> Status: Implemented
>
> Date: 2026-06-25
>
> Scope: 本文只说明 CharacterStudio Web 端直接预览 FBX 的当前实现和边界，不改变 Unity Avatar、Prefab、runtime 加载或角色资源包最终导入契约。

## 结论

CharacterStudio 已支持直接预览 FBX。当前实现仍是原生 HTML/CSS/JavaScript + Three.js，没有引入 React/Vue/Svelte，也没有引入前端打包器。

直接 FBX 预览解决的是编辑器体验问题：`codex_flow_test` 的主体权威源可以是 `ArtSource/Characters/codex_flow_test/character_00.fbx`，用户不再需要手工维护 `character_00_light.glb` 才能在 CharacterStudio 里看模型、调 socket 和碰撞体。

它不是 Unity 导入方案的替代品。Unity Humanoid Avatar、Prefab 构建、CharacterAction 动画预览和 runtime 加载仍然以 Unity 导入链路为准。

## 当前实现

- 3D viewport 位于 `Tools/MxFramework.CharacterStudio/web/src/viewport3d.js`。
- `loadThreeRuntime()` 同时加载 Three.js、`GLTFLoader`、`FBXLoader`、`OrbitControls` 和 `TransformControls`。
- 因为 `viewport3d.js` 位于 `web/src/`，Three.js 动态导入路径必须从 package root 解析：

```js
import("../../node_modules/three/build/three.module.js")
```

- 不要改回 `../node_modules/...`；该路径会指向不存在的 `web/node_modules`，导致浏览器回退到 SVG 占位预览。
- 模型格式通过通用 preview model loader 分派，支持 `.fbx`、`.glb`、`.gltf`。
- FBX 默认不套用 `UNITY_GLTF_PREVIEW_POSE_ADAPTER`，避免把 GLB 专用坐标适配误用到 FBX 上。
- FBX 内部动画当前只作为 loader 元数据保留，不在 CharacterStudio 中作为 locomotion 或 CharacterAction 动画播放来源。

## 边界

- CharacterStudio Web 预览是 best-effort，目标是能看模型并继续编辑挂点、碰撞体、武器挂载和轨迹。
- Unity 侧是否能播放项目 Humanoid 动画，取决于 ModelImporter、当前模型自己的 Avatar、Prefab builder 和 CharacterAction Profile。
- 外部贴图只做允许目录内的 best-effort 加载和诊断，不支持本机绝对路径、`..`、全盘扫描或越过 source root 的文件访问。
- GLB/gltf 预览仍可用。GLB 如果继续存在，应作为工具生成的 preview/import artifact 或缓存，不应作为人形主体权威源文件。

## 与角色制作流程的关系

当前推荐方向：

- 人形主体权威源：FBX，例如 `character_00.fbx`。
- CharacterStudio Web 预览：优先直接显示 FBX；必要时可使用工具生成的 preview artifact。
- Unity Avatar / Prefab：使用当前角色自己的 FBX / ModelImporter 结果，不回退到其他模型 Avatar。
- 武器等静态模型：可继续使用 GLB/FBX，只要它们不需要 Humanoid Avatar。
- Locomotion：是 BlendTree / 动画图配置，不是 `locomotion.glb` 文件。

## 验证

当前相关验证入口：

```powershell
node --check Tools/MxFramework.CharacterStudio/web/app.js
Get-ChildItem Tools/MxFramework.CharacterStudio/web/src -Filter *.js | ForEach-Object { node --check $_.FullName }
node --check Tools/MxFramework.CharacterStudio/scripts/smoke.mjs
npm --prefix Tools/MxFramework.CharacterStudio run smoke
```

Smoke 覆盖点包括：

- CharacterStudio 引入 `FBXLoader`。
- preview loader 支持 FBX/GLB/gltf。
- `codex_flow_test` 的 body source 是 `character_00.fbx`。
- body catalog entry 使用 FBX 作为 `characterModel` 权威资源。
- preview GLB 如存在，必须标记为 `previewMesh` 派生产物。
- 拆分后的 `viewport3d.js` 使用 `../../node_modules/...` 加载 Three.js。
