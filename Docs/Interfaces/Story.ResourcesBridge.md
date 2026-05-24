# Story.ResourcesBridge 接口

> 状态：S3 最小切片已实现（2026-05-24）。本文记录 `MxFramework.Story.ResourcesBridge` 当前可用的 noEngine preload-plan bridge API。

## 职责

`MxFramework.Story.ResourcesBridge` 把 Story 层的资源预加载 metadata 转成 `MxFramework.Resources.ResourcePreloadPlan`。

It may:

- accept Story-owned resource metadata DTOs.
- validate explicit `ResourceKey` fields and labels.
- sort and deduplicate keys / labels deterministically.
- produce `ResourcePreloadPlan` values for an outer composition root.

It must not:

- call `IResourceManager.Load` or `IResourcePreloadService.PreloadAsync`.
- release `ResourceHandle` or `ResourceGroupHandle`.
- choose Unity provider, AssetBundle provider, remote bundle provider, Addressables adapter, or catalog mounting policy.
- depend on UnityEngine or UnityEditor.

## Dependencies

```text
MxFramework.Story.ResourcesBridge
  -> MxFramework.Story
  -> MxFramework.Resources
```

Story core、Story.Runtime 和 Story.Config 不依赖 ResourcesBridge；组合根显式调用 bridge 后，再决定是否把 plan 交给 `ResourcePreloadService`。

## Current API

| 类型 | 用途 |
| --- | --- |
| `StoryResourceKeyMetadata` | Story-facing explicit resource key metadata: id、type、variant、package |
| `StoryResourcePreloadMetadata` | Story-facing preload metadata: group id、explicit keys、labels、failFast、maxConcurrentLoads |
| `StoryResourcePreloadPlanBuilder` | deterministic metadata -> `ResourcePreloadPlan` mapper |
| `StoryResourcePreloadPlanResult` | structured result，成功时带 plan，失败时带 diagnostics |
| `StoryResourcesBridgeDiagnostic` | invalid key / label / missing metadata diagnostics |

## Mapping Rules

- Explicit keys convert to `ResourceKey`.
- Invalid key metadata is rejected when `ResourceKey.IsValid == false` or `TypeId` is empty.
- Labels must be non-empty and non-whitespace.
- Duplicate explicit keys are removed.
- Duplicate labels are removed using ordinal comparison.
- Explicit keys are sorted by `PackageId`, `Id`, `TypeId`, `Variant`.
- Labels are sorted by `StringComparer.Ordinal`.
- `MaxConcurrentLoads < 1` is clamped to `1`, matching `ResourcePreloadPlan`.

The bridge returns `Plan=null` if any diagnostic exists. It does not partially emit a preload plan with invalid metadata.

## Example

```csharp
using MxFramework.Resources;
using MxFramework.Story.ResourcesBridge;

var metadata = new StoryResourcePreloadMetadata(
    "story.cutscene.1001",
    explicitKeys: new[]
    {
        new StoryResourceKeyMetadata("story.audio.line_001", ResourceTypeIds.AudioClip),
        new StoryResourceKeyMetadata("story.ui.dialogue", ResourceTypeIds.VisualTreeAsset)
    },
    labels: new[] { "story.cutscene.common" },
    failFast: true,
    maxConcurrentLoads: 4);

StoryResourcePreloadPlanResult result = StoryResourcePreloadPlanBuilder.Build(metadata);
if (result.Success)
{
    ResourcePreloadPlan plan = result.Plan;
    // Outer composition root may pass plan to ResourcePreloadService.
}
```

## Current Unsupported

- Loading, release, provider selection, catalog mounting, and scene warmup execution.
- Mapping Story `ResourceId` integers to concrete `ResourceKey` values. S3 expects the project/config layer to provide explicit key metadata.
- Unity Timeline / Cinemachine / audio presentation resource binding.

## Test Entry

- `Assets/Scripts/MxFramework/Tests/Story.ResourcesBridge/StoryResourcesBridgeTests.cs`
