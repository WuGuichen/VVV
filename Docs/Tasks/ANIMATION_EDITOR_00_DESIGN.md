# Animation Editor 00：Standalone Animation Authoring Workstation

> Issue：#337
>
> 状态：设计草案
>
> 范围：独立 Animation Editor、AnimationGroup / Clip mapping / 1D-2D Blend / timeline event / bake / compatibility authoring、Resource Manager picker 消费、CharacterStudio 迁移边界
>
> 前置：`Docs/Interfaces/Animation.md`、`Docs/Tasks/CHARACTER_RESOURCE_LIBRARY_00_DESIGN.md`、`Docs/Tasks/CHARACTER_RESOURCE_LIBRARY_EDITOR_01_MVP.md`、`Tools/MxFramework.EditorHub`

## Goal

建立一个独立的 Animation Editor，把 `AnimationGroup`、clip 映射、BlendSpace、动画事件时间轴、root motion 策略、bake 和兼容性校验从 CharacterStudio 迁出。

最终边界：

```text
Authoring Resource Manager
  发现 Unity AnimationClip、GLB/GLTF sub-clip、AvatarMask、bake artifact、FMOD/AudioCue 等资源

Animation Editor
  编辑 AnimationSet、AnimationGroup、Clip mapping、1D/2D Blend、Timeline Events、Bake/Compatibility profile

CharacterStudio
  选择角色默认 AnimationProfile / AnimationGroup，预览角色组合结果，不编辑 AnimationGroup 内容

Combat / Weapon / VFX / Audio Editors
  引用 Animation action / event / cue，不拥有动画源资源或 Group 编辑

Authoring Compiler
  把 Animation authoring 配置编译成 MxAnimationSetDefinition、MxAnimationPackageExpectation、runtime resource catalog entries 和 warmup plan

Runtime
  只消费 MxAnimation runtime DTO、ResourceCatalog、ResourcePreloadService 和 AudioCue manifest
```

## Non-Goals

- 不在 Animation Editor 中修改 `.fbx`、`.glb`、`.gltf`、`.anim` 源文件内容。
- 不直接读取 Unity `AnimationClip` 对象作为运行时输入；运行时只用 `ResourceKey` / compiled DTO。
- 不把 Resource Manager 变成 Animation 编辑器。资源库只负责发现、导入、筛选、选择和诊断资源。
- 不让 CharacterStudio 长期承担 AnimationGroup 编辑职责。
- 不在本阶段决定“动画最终归属角色还是武器”。设计必须支持角色、武器、装备状态和 Combat action 后续引用同一套 animation authoring 产物。
- 不新增与 `MxFramework.Resources` 并行的 runtime loader；继续复用 `ResourceCatalogEntry`、`IResourceManager`、`ResourcePreloadService`、`ResourceRetainPolicy`。

## Product Boundary

### Animation Editor 负责

- 创建和维护 `AnimationSet` / `AnimationProfile` / `AnimationGroup`。
- 从 Resource Manager 选择源动画资源和具体 clip / sub-asset，不手填未知 `sourceClipName`。
- 编辑 group 内 clip 映射：
  - `clipId`
  - source resource selection
  - source sub-clip id/name
  - display name
  - loop
  - speed
  - root motion policy
  - tags / purpose
- 可视化编辑 1D / 2D BlendSpace。
- 编辑 Timeline Events：
  - footstep
  - weapon trace on/off
  - hit / active frame marker
  - VFX
  - SFX / AudioCue
  - camera shake / camera cue
  - custom presentation event
- 对齐 Combat frame、presentation frame、seconds / normalized time。
- 预览当前 clip、blend、event timeline 和 root motion reference。
- 运行 bake、compatibility、warmup 和 mapping 校验。
- 输出可复制 diagnostics / AI context。

### Animation Editor 不负责

- 不管理全局资源列表、文件夹导入、FMOD 同步或 orphan cleanup；这些属于 Resource Manager。
- 不编辑角色 body、socket、collider、weapon slot；这些属于 CharacterStudio / Equipment editor。
- 不编辑 Combat authority、damage、cancel、hit resolver；这些属于 Combat authoring。
- 不直接写 Unity prefab 或 scene。

### CharacterStudio 迁移后只负责

- 选择角色默认 animation profile。
- 为角色 / 装备状态选择已存在的 `AnimationGroup` 或 `AnimationSet` 引用。
- 预览角色 body、weapon、socket、collider 和 animation 组合效果。
- 跳转打开 Animation Editor。
- 展示 animation resource plan / diagnostics。

CharacterStudio 中现有 AnimationGroup 编辑能力是过渡桥接。Animation Editor MVP 可用后，CharacterStudio 应降级为只读摘要 + 引用选择。

## Source Clip Model

当前 `sourceClipName` 手填不够健壮。Animation Editor 必须把源 clip 建模为 Resource Manager 可选择项：

```text
Unity .anim file
  -> Resource item: kind=Animation, usage=animationClip
  -> selectable source clip

GLB/GLTF file with multiple animations
  -> Resource item: kind=Animation, usage=animationClipGroup
  -> sub-clips discovered from importer metadata
  -> selectable source clip: resource selection + subClipId/name

Unity model asset with sub AnimationClips
  -> Resource item: kind=Animation, usage=animationClipGroup or animationClip
  -> provider binding carries unityGuid / unityAssetPath / subAsset name

Runtime catalog clip
  -> Resource item: kind=Animation, usage=animationClip
  -> bindingKind=ResourceManagerAsset
  -> runtimeResourceKey available
```

Editing a clip mapping never changes the source animation file. It only changes how authoring config resolves that source into runtime animation bindings.

## Data Model

Names below describe authoring DTOs. Runtime DTOs still map to `MxAnimationSetDefinition`, `MxAnimationActionBinding`, `MxAnimationBlend1DDefinition`, `MxAnimationBlend2DDefinition`, `MxAnimationWarmupDefinition` and package expectation types from `Docs/Interfaces/Animation.md`.

### AnimationAuthoringPackage

```csharp
public sealed class AnimationAuthoringPackage
{
    public string SchemaVersion { get; set; }
    public string PackageId { get; set; }
    public string StableId { get; set; }
    public string DisplayName { get; set; }
    public string SkeletonProfileId { get; set; }
    public string AvatarProfileId { get; set; }
    public List<AnimationAuthoringSet> Sets { get; set; }
    public List<AnimationAuthoringProfile> Profiles { get; set; }
    public List<AnimationAuthoringDiagnostic> Diagnostics { get; set; }
}
```

This package can be project-global or package-local. The important rule is that it is not owned by CharacterStudio. Character packages reference it by stable id.

### AnimationAuthoringSet

```csharp
public sealed class AnimationAuthoringSet
{
    public string SetId { get; set; }
    public string DisplayName { get; set; }
    public string Version { get; set; }
    public string DefaultClipId { get; set; }
    public string FallbackClipId { get; set; }
    public List<AnimationLayerAuthoring> Layers { get; set; }
    public List<AnimationGroupAuthoring> Groups { get; set; }
    public List<AnimationActionBindingAuthoring> ActionBindings { get; set; }
    public AnimationCompatibilityExpectationAuthoring Compatibility { get; set; }
    public AnimationWarmupAuthoring Warmup { get; set; }
}
```

### AnimationGroupAuthoring

```csharp
public sealed class AnimationGroupAuthoring
{
    public string GroupId { get; set; }
    public string DisplayName { get; set; }
    public string Description { get; set; }
    public string Usage { get; set; } // base, locomotion, combat, reaction, weapon, emote, custom
    public List<AnimationClipMappingAuthoring> Clips { get; set; }
    public List<AnimationBlend1DAuthoring> Blend1D { get; set; }
    public List<AnimationBlend2DAuthoring> Blend2D { get; set; }
    public List<AnimationTimelineAuthoring> Timelines { get; set; }
}
```

`AnimationGroup` is not a source file. It is a semantic grouping and mapping layer over one or more source animation resources.

### AnimationClipMappingAuthoring

```csharp
public sealed class AnimationClipMappingAuthoring
{
    public string ClipId { get; set; }
    public string DisplayName { get; set; }
    public ResourceSelectionRef SourceSelection { get; set; }
    public string SourceSubClipId { get; set; }
    public string SourceClipName { get; set; }
    public string RuntimeResourceKey { get; set; } // filled by compiler when resolvable
    public bool Loop { get; set; }
    public float Speed { get; set; }
    public string RootMotionPolicy { get; set; } // Ignore, MotionDelta, ApplyToActorReference
    public List<string> Tags { get; set; }
}
```

`SourceClipName` is display / compatibility metadata, not a free-text primary reference. The primary reference is `SourceSelection + SourceSubClipId`.

### Blend Authoring

```csharp
public sealed class AnimationBlend1DAuthoring
{
    public string BlendId { get; set; }
    public string Parameter { get; set; }
    public string DefaultClipId { get; set; }
    public List<AnimationBlend1DPointAuthoring> Points { get; set; }
}

public sealed class AnimationBlend2DAuthoring
{
    public string BlendId { get; set; }
    public string XParameter { get; set; }
    public string YParameter { get; set; }
    public string DefaultClipId { get; set; }
    public List<AnimationBlend2DPointAuthoring> Points { get; set; }
}
```

Blend points reference local `clipId`, not source file names.

### Timeline Event Authoring

```csharp
public sealed class AnimationTimelineEventAuthoring
{
    public string EventId { get; set; }
    public string ClipId { get; set; }
    public string TimeDomain { get; set; } // Seconds, NormalizedTime, PresentationFrame, CombatFrame
    public float Time { get; set; }
    public string EventKind { get; set; } // Footstep, TraceOn, TraceOff, HitMarker, Vfx, AudioCue, CameraCue, Custom
    public ResourceSelectionRef ResourceSelection { get; set; }
    public string PayloadJson { get; set; }
}
```

FMOD / Audio events must select `AudioCue` / `AudioEventDefinition`, not `AudioClip` unless the project explicitly provides one.

## UI Design

MVP should stay consistent with current external editors: vanilla web app under `Tools/`, launched by EditorHub and Authoring server. Do not introduce React/Tauri for this slice.

```text
Tools/MxFramework.AnimationEditor/
  README.md
  start-animation-editor.sh
  start-animation-editor.bat
  start-animation-editor.command
  scripts/smoke.mjs
  web/index.html
  web/app.js
  web/styles.css
```

### Top Bar

- Scope selector：project / package / animation package。
- Active set/profile selector。
- Preview target：skeleton / character package / optional model resource。
- Refresh resources。
- Save。
- Validate。
- Compile animation package。
- Open Resource Manager。
- Open CharacterStudio for selected preview target。

### Left Panel：Animation Structure

Tree:

```text
Animation Sets
  set.iron_vanguard.base
    Layers
    Groups
      locomotion
        Clips
        1D Blend
        2D Blend
        Timelines
      combat.sword
    Action Bindings
    Compatibility
    Warmup
Profiles
Diagnostics
```

The left panel is not a full resource browser. Resource selection opens a picker only for the active field.

### Center Panel：Preview Workspace

Tabs:

- Clip Preview：single clip playback, loop toggle preview, speed preview, root motion path overlay.
- Blend Editor：1D line or 2D plane; points are draggable and reference local `clipId`.
- Timeline：scrubber with events aligned to seconds / normalized time / presentation frame / combat frame.
- Bake / Compatibility：bake artifact summary, skeleton / avatar path checks, missing bone/socket diagnostics.

### Right Panel：Inspector

Field-driven inspector for:

- Animation set metadata.
- Group metadata.
- Clip mapping.
- Blend point.
- Timeline event.
- Layer / AvatarMask.
- Action binding.
- Warmup and package expectation.

All source resource fields must use `ResourceFieldSpec` picker.

### Bottom Panel：Diagnostics and Resource Plan

- Mapping validation.
- Missing source clip.
- Duplicate `clipId`.
- Blend point references missing clip.
- Runtime resource not resolvable.
- Skeleton / avatar mismatch.
- Timeline event resource missing.
- Warmup required resource missing.
- Bake artifact stale.

Every diagnostic must be copyable and include source field path.

## ResourceFieldSpec Requirements

Animation Editor must define field specs at least for:

| Field | Accepted resources | Output |
| --- | --- | --- |
| `Animation.SourceClip` | `Animation` usage `animationClip` / `animationClipGroup`, bindings `UnityAsset`, `UnityEditorOnlyAsset`, `PackageResource`, `ResourceManagerAsset` | `ResourceSelectionRef + SourceSubClipId` |
| `Animation.AvatarMask` | `AvatarMask`, runtime or Unity asset | `ResourceSelectionRef` |
| `Animation.BakeArtifact` | `Generated` / `Config` usage `animationBakeArtifact` | `ResourceSelectionRef` |
| `Animation.CompatibilityProfile` | `Config` usage `animationCompatibilityProfile` | `ResourceSelectionRef` |
| `Animation.EventVfx` | `Vfx` | `ResourceSelectionRef` |
| `Animation.EventAudioCue` | `Audio` usage `audioCue` / `fmodEvent`, binding `AudioCue` / `AudioEventDefinition` | `AudioCueId` |

The picker must show sub-clips for GLB/Unity model sources. If provider metadata cannot enumerate sub-clips, the item can be selected only with a warning and must ask the user to enter a temporary source clip name until provider support is added.

## Authoring API

Suggested Authoring server endpoints:

```text
GET  /api/authoring/animation/packages
GET  /api/authoring/animation/load?package=<id>
POST /api/authoring/animation/save
POST /api/authoring/animation/validate
POST /api/authoring/animation/compile
POST /api/authoring/animation/bake
GET  /api/authoring/animation/preview-context?set=<id>&clip=<id>
```

Resource selection continues to use:

```text
POST /api/authoring/resources/pick
POST /api/authoring/resources/resolve-selection
GET  /api/authoring/resources
```

No frontend page should parse Unity `.meta`, FMOD banks, runtime catalog files or package resource catalogs directly when an Authoring API exists.

## Compiler Outputs

Animation Compiler should output:

```text
animation_set_definition.json
animation_package_expectation.json
animation_resource_plan.json
animation_clip_registry.json
animation_validation_report.json
```

For character packages, Character Authoring Compiler should consume compiled animation outputs and fold them into:

```text
runtime_resource_catalog.json
character_resource_plan.json
resource_validation_report.json
```

`AnimationWarmup` must include default clip, fallback clip, action clips, blend point clips, AvatarMask, compatibility profile and required bake artifacts.

## CharacterStudio Migration

Phase after Animation Editor MVP:

1. CharacterStudio still reads existing temporary `applicationConfig.animationGroups[]`.
2. Animation Editor can import/migrate those groups into an Animation authoring package.
3. CharacterStudio replaces edit controls with:
   - selected animation profile
   - selected default groups
   - readonly group summary
   - `Open Animation Editor`
   - preview current character animation
4. Character application config should store stable refs:

```json
{
  "animationProfileRef": {
    "animationPackageStableId": "anim.iron_vanguard",
    "profileId": "profile.default"
  }
}
```

5. Temporary slot `resourceKey` / `resourceSelection` fields become compiler-derived or compatibility shims, not primary authoring fields.

## Validation

MVP must validate:

- Duplicate set / group / clip / blend / event IDs.
- Missing source selection.
- Source selection kind/usage mismatch.
- Missing or ambiguous sub-clip.
- Runtime resource key unavailable for required runtime clips.
- Blend point references missing local clip.
- Blend 1D / 2D has too few valid points.
- Loop policy suspicious for one-shot / locomotion tags.
- Root motion policy incompatible with runtime character movement mode.
- Timeline event outside clip duration.
- Audio event selected as regular resource key.
- Skeleton / avatar / clip binding mismatch.
- Warmup required resource missing.
- Bake artifact hash stale.

## Implementation Slice Proposal

### Animation Editor 01：Design Contract and DTOs

- Add this design to docs.
- Add Authoring DTOs for animation set/package/group/clip/blend/timeline event.
- Add JSON roundtrip tests.
- Do not build UI yet.

### Animation Editor 02：Authoring API and Resource Picker Specs

- Add load/save/validate endpoints.
- Add `ResourceFieldSpec` definitions for source clips, AvatarMask, bake artifacts and AudioCue events.
- Resource picker must expose sub-clip metadata when provider supplies it.

### Animation Editor 03：Editor Shell and Group/Clip Mapping

- Add `Tools/MxFramework.AnimationEditor`.
- Add EditorHub entry and launch scripts.
- Implement set/group/clip tree, inspector and save/validate.
- CharacterStudio links to Animation Editor but still keeps temporary bridge.

### Animation Editor 04：Visual Blend Editor

- Add 1D and 2D BlendSpace visual editing.
- Drag points, choose local clips, preview parameter cursor.
- Validate missing clips and degenerate layouts.

### Animation Editor 05：Timeline Events

- Add scrubber timeline.
- Add event rows for footstep, trace, hit marker, VFX, AudioCue, camera and custom events.
- Compile to `MxAnimationPresentationEvent`.

### Animation Editor 06：Preview, Bake and Compatibility

- Add 3D preview using selected skeleton/character target.
- Add root motion path overlay.
- Add bake and compatibility report display.
- Hook to existing Animation bake / compatibility contracts.

### Animation Editor 07：Compiler Integration and CharacterStudio Migration

- Compile authoring package into runtime animation outputs.
- Feed Character resource plan `AnimationWarmup`.
- Migrate CharacterStudio to readonly animation summary + group/profile picker + open Animation Editor.

## Acceptance

- AnimationGroup can be edited without opening CharacterStudio.
- Source clip selection comes from Resource Manager and supports individual `.anim` plus GLB/Unity sub-clips.
- Editing clip mapping, blend points or timeline events does not modify source animation files.
- CharacterStudio no longer owns AnimationGroup authoring once migration is complete.
- Runtime outputs use existing `MxFramework.Animation` and `MxFramework.Resources` contracts.
- Missing resources, mismatched skeleton, invalid blend points and stale bake artifacts are caught before runtime.

## Test Plan

- `git diff --check`
- Authoring DTO JSON roundtrip tests.
- Animation Editor smoke script after UI exists.
- Resource picker smoke: source clip, AvatarMask, AudioCue.
- CharacterStudio smoke: confirms readonly animation summary after migration.
- Animation compiler validation: generated `MxAnimationSetDefinition` warmup includes default/fallback/action/blend/mask resources.
