# Resource Manager Workflow UI Reorganization 01

> Scope: `Tools/MxFramework.ResourceLibrary` front-end information architecture and minor validation messaging.
> Goal: stop adding resource features until the local Player resource workflow is understandable and reliable.

## Optimization Goal

First make the Resource Manager a clear workflow:

```text
发现资源
-> 加入 / 编辑 Build Profile
-> 保存 Profile
-> 构建 Player 资源
-> Offline Runtime 验证
```

The current UI exposes too many features at the same visual level. Users cannot reliably tell whether they are looking, editing, building, or debugging.

This task should reorganize the existing Resource Manager UI without changing the backend model first. The target is a readable local Player resource flow, not a larger resource platform.

## Proposed Structure

Split the current large screen into four primary workspaces:

```text
Browse   资源浏览
Profile  构建 Profile 编辑
Build    构建计划 / 构建产物状态
Debug    详情 / 引用 / 诊断 / JSON
```

Top-level global actions only:

```text
包上下文选择
刷新
打开 CharacterStudio
保存 Profile
打开 Unity Build Workbench / 构建指引
```

Remove the persistent bottom action bar. Replace it with contextual action areas inside the active workspace.

## Workspace 1: Browse

Purpose: resource discovery and selection only.

Keep:

```text
搜索
provider / kind / runtime-ready / profile 状态筛选
选择可见
清除勾选
资源卡片
```

Resource cards should show only key state:

```text
名称
kind / usage
provider
runtime 状态
profile 状态
诊断数
引用数
```

Do not stack many tags on the card. Detailed tags and metadata belong in the Debug inspector.

Expected contextual actions:

```text
查看详情
加入 Profile
打开 Profile 编辑
```

## Workspace 2: Profile

Purpose: Build Profile editing. This should feel like a guided editor, not a flat dump of all fields.

When a resource is selected, show:

```text
Step 1 资源身份
- ResourceKey.id
- type
- packageId
- source provider
- unityGuid / assetPath

Step 2 交付模式
- internal
- external
- editorOnly
- excluded

Step 3 Bundle 归属
- bundleRule
- bundleGroupHint
- labels

Step 4 Preload
- preloadGroups
- fail policy / 用途提示

Step 5 保存状态
- notInProfile / draftOnly / modified / saved
- 保存 Profile
```

Primary buttons:

```text
加入 Profile
移出 Profile
保存 Profile
还原当前资源草稿
```

Batch editing must not be permanently visible. It appears only when multiple resources are checked:

```text
已选择 N 个资源
批量加入 Profile
批量移出 Profile
批量设置 deliveryMode
批量设置 preloadGroups
批量设置 labels
```

Profile editing rules:

- `加入 Profile` should take the user directly to the Profile workspace.
- `deliveryMode != internal` should hide or clear `bundleRule` and internal bundle hints.
- Invalid `ResourceKey.id` should be shown beside the input before save.
- Each entry should expose one clear state: `will build`, `external`, `editor only`, or `excluded`.

## Workspace 3: Build

Purpose: understand build plans and generated artifacts. This page does not build AssetBundles in the web UI.

Separate Resource Plan from Bundle Plan:

```text
Resource Plan
- 当前角色 / consumer 运行时需要哪些资源
- SpawnCritical / AnimationWarmup / UiDeferred 等

Bundle Plan
- saved Profile 会生成哪些 Bundle
- 每个 bundle 有哪些资源
- external / excluded 数量
- 空 bundle rule
- stale bundle
```

Build page capabilities:

```text
查看 Bundle Plan
查看 generated artifacts 是否存在
复制构建报告
提示去 Unity Workbench 构建
```

Buttons:

```text
刷新 Bundle Plan
复制 Build Report
打开 Unity Workbench 指引
```

Unity remains the build execution surface:

```text
MxFramework/Resources/Open Global AssetBundle Builder
```

The Build workspace should eventually show:

```text
Profile saved
Bundle Plan valid
Catalog exists
Preload groups exists
Bundle exists
Build report no errors
```

## Workspace 4: Debug

Purpose: deep inspection, diagnostics, references, and raw payloads.

The inspector should no longer dominate the default screen. Put this material in Debug:

```text
Overview
Unity
Runtime
Build
References
Diagnostics
Raw JSON
```

`Raw JSON` is collapsed by default.

Copy buttons are Debug-only:

```text
复制详情 JSON
复制诊断 JSON
```

## Button Reorganization

Current bottom actions:

```text
导入资源
导入文件夹
重导
替换源
加入构建 Profile
移出构建 Profile
批量加入
批量移出
保存 Profile
删除资源
编辑标签
复制详情 JSON
复制诊断 JSON
```

Target grouping:

```text
资源操作：
导入资源
导入文件夹
重导
替换源

Profile 操作：
加入 Profile
移出 Profile
保存 Profile

批量操作：
只在勾选资源后出现

危险 / 未完成操作：
删除资源、编辑标签先隐藏，不要禁用常驻

调试操作：
复制详情 JSON、复制诊断 JSON 放 Debug 页
```

## Implementation Phases

### Phase 1: Information Architecture Only

Do not change backend behavior in this phase.

1. Remove the persistent bottom action bar.
2. Add top-level navigation: `Browse / Profile / Build / Debug`.
3. Move the Build Profile panel into the Profile workspace.
4. Move Resource Plan and Bundle Plan into the Build workspace.
5. Move Raw JSON into the Debug workspace and collapse it by default.
6. Show batch actions only after resources are checked.

### Phase 2: Profile Editing Experience

1. After `加入 Profile`, switch to the Profile workspace.
2. On save failure, locate the affected entry and field.
3. When `deliveryMode != internal`, automatically hide or clear `bundleRule`.
4. Show `ResourceKey.id` validation beside the input.
5. Show each entry state as `will build / external / editor only / excluded`.

### Phase 3: Workflow Closure

1. Build workspace shows generated artifact existence.
2. Build workspace shows the active build target bundle files.
3. Build workspace shows CharacterTest Offline mode hints.
4. Add a local-chain checklist:
   - Profile saved
   - Bundle Plan valid
   - Catalog exists
   - Preload groups exists
   - Bundle exists
   - Build report no errors

## Do Not Do Now

Do not add:

```text
删除资源
标签编辑
Online 模式
热更发布
CDN
YooAsset / Addressables
新的资源 provider
复杂 3D 预览
```

These would expand the surface area before the local Player resource workflow is stable.

## Acceptance Criteria

After this UI optimization, a user should be able to complete the flow without reading documentation:

1. Find one resource.
2. Add it to Build Profile.
3. Understand whether it will enter an AssetBundle.
4. Save Profile.
5. Understand why save failed, if it failed.
6. Go to Unity Workbench to build.
7. Return to Play Mode and load through Offline mode.

## Validation

Minimum validation for the first implementation pass:

```text
node Tools/MxFramework.ResourceLibrary/scripts/smoke.mjs
```

Manual browser validation:

1. Start Resource Manager.
2. Confirm each top-level workspace opens.
3. Select one resource in Browse.
4. Add it to Profile and confirm navigation.
5. Save Profile or see field-level validation.
6. Open Build and confirm Bundle Plan/artifact guidance.
7. Open Debug and confirm Raw JSON is not shown by default.

