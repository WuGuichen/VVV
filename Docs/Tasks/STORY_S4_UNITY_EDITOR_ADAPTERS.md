# STORY_S4_UNITY_EDITOR_ADAPTERS

## Goal

Implement the Story S4 Unity adapter and Editor debug tooling slice for Issue #440.

This task adds Unity-facing presentation/input adapters and editor inspection surfaces without changing Story authority state. Unity components enqueue Story runtime commands or consume Story runtime events; they do not call `StoryDirector` mutation APIs directly.

## Workflow Level

- Issue: `#440`
- Task level: `S3`
- Delivery level: `Framework Feature`
- Suggested branch: `feature/440-story-unity-adapters`
- Required PR: yes
- Merge policy: implementation agent opens PR and does not merge it.

This is not the final playable demo. Playable Story demo work remains gated to a later issue.

## Required Reading

1. `AGENTS.md`
2. `Assets/AGENTS.md`
3. `Assets/Scripts/MxFramework/AGENTS.md`
4. `Docs/PROJECT_INDEX.md`
5. `Docs/README.md`
6. `Docs/WORKFLOW.md`
7. `Docs/QUALITY_GATE.md`
8. `Docs/AGENT_GAME_CREATION_GUIDE.md`
9. `Docs/Decisions/ADR-004-story-module-scope.md`
10. `Docs/Decisions/ADR-005-story-runtime-command-boundary.md`
11. `Docs/Interfaces/Story.md`
12. `Docs/Interfaces/Story.Runtime.md`
13. `Docs/Interfaces/Story.GameplayBridge.md`
14. `Docs/Interfaces/Story.ResourcesBridge.md`
15. `Docs/Interfaces/DebugUI.md`
16. `Docs/Interfaces/UI.Toolkit.md`
17. `Docs/Interfaces/Editor.md`
18. This task document

## API Reuse Plan

| Need | Existing API / Module | Use in S4 | Notes |
| --- | --- | --- | --- |
| Story command input | `RuntimeCommandBuffer`, `StoryRuntimeCommandFactory`, `StoryRuntimeCommandIds` | Yes | Unity trigger/input adapters enqueue Story commands only. |
| Story runtime events | `RuntimeEventQueue<StoryRuntimeEvent>`, `StoryRuntimeEvent`, `StoryEventKind` | Yes | Timeline/Cinemachine-style adapters consume runtime events as presentation signals. |
| Runtime lifecycle | `RuntimeHost`, `RuntimeTickContext`, frame/stage concepts | Yes | Adapters must not become a second Story runtime loop. |
| Story state snapshot | `StoryDirector.CreateSnapshot()`, `StoryRuntimeModule` public state | Yes | Editor debug reads snapshots, blackboard, active beats, recent events. |
| Debug UI | `IFrameworkDebugSource`, `FrameworkDebugSourceRegistry`, `DebugUiSnapshotAggregator` | Prefer | Runtime debug facts should be exposed as readonly snapshots where possible. |
| UI Toolkit | `MxFramework.UI.Toolkit`, existing EditorWindow UI Toolkit patterns | Yes | Editor debug uses UI Toolkit, Chinese visible labels, stable English element names. |
| Input | Existing Input module / explicit Unity event hooks | Limited | Use existing input bridge if available; otherwise keep adapters as explicit component methods. |
| Unity scene/assets | Unity Editor / Unity MCP generated assets | Avoid | No scene/prefab asset is required for this slice unless generated through approved workflow. |
| Gameplay / Resources bridge | S3 bridge public contracts | Read only if needed | S4 should not change S3 implementation except public integration docs. |

## Implementation Strategy

### Story.Unity

Add `MxFramework.Story.Unity` as a Unity runtime adapter assembly.

Expected minimum public surface:

- trigger-zone or trigger-intent adapter that enqueues `Story.RaiseTrigger`.
- presentation completion adapter that enqueues `Story.CompletePresentation`.
- event presentation router that consumes `StoryRuntimeEvent` and invokes presentation callbacks without mutating Story state.

Rules:

- Runtime assembly may reference UnityEngine, Story, Story.Runtime, and Runtime.
- Runtime assembly must not reference UnityEditor.
- Unity components receive command buffers / runtime module references from a composition root or explicit binding method.
- Unity components must not call `StoryDirector.TryRaiseTrigger`, `TryResolveChoice`, `CompletePresentation`, `AbortGraph`, or `TryEnterBeat` directly.
- Components must expose structured result/status suitable for tests and debug views.

### Story.Editor

Add `MxFramework.Story.Editor` as a Unity Editor tooling assembly.

Expected minimum public surface:

- Story runtime debug source or snapshot adapter for graph / beat / blackboard / recent event inspection.
- UI Toolkit EditorWindow or Framework Manager-compatible panel that displays Story runtime state.
- Read-only by default. Any debug command must go through an explicit command gate or enqueue Story runtime command; no hidden direct mutation.

Rules:

- Editor assembly may reference UnityEditor and UI Toolkit.
- Editor UI visible labels default to Chinese.
- Technical identifiers, element names, menu paths, class names, and logs stay English.
- Prefer existing Framework Manager / DebugUI / UI Toolkit patterns before creating a standalone visual vocabulary.
- Do not add IMGUI `OnGUI` as a formal UI surface unless documented as a temporary fallback.

### Event and Command Policy

Adapters follow ADR-005:

```text
Unity input/presentation adapter
  -> Story RuntimeCommand
  -> StoryRuntimeModule drains Story command buffer
  -> StoryRuntimeEvent
  -> Unity presentation adapter consumes event
```

The Unity side is never authority. It only observes, enqueues intents, and presents snapshots.

## Allowed Changes

- `Assets/Scripts/MxFramework/Story.Unity/**`
- `Assets/Scripts/MxFramework/Story.Editor/**`
- `Assets/Scripts/MxFramework/Tests/Story.Unity/**`
- `Assets/Scripts/MxFramework/Tests/Story.Editor/**`
- UI Toolkit debug code needed for Story runtime inspection.
- `Docs/Interfaces/Story.Unity.md`
- `Docs/Interfaces/Story.Editor.md`
- `Docs/Interfaces/DebugUI.md` if a Story debug source is added.
- `Docs/Interfaces/Editor.md` if a new Story editor menu/window is added.
- `Docs/INTERFACES.md`
- `Docs/CAPABILITIES.md`
- `Docs/USAGE.md`
- This task document.

## Out Of Scope

- Story core/runtime state machine semantics.
- Story.Config / Story.GameplayBridge / Story.ResourcesBridge implementation changes beyond public integration points.
- Final playable demo scene delivery.
- Handwritten `.unity`, `.prefab`, `.asset`, or ScriptableObject YAML.
- Runtime AI Planner bridge or playable demo.
- Timeline / Cinemachine package-specific features beyond a generic event presentation router if packages are not already required.
- WGame-specific story content.

## Suggested Slice Order

1. Create `Story.Unity` and `Story.Editor` asmdefs with correct dependency boundaries.
2. Implement command-enqueue adapters for trigger and presentation completion.
3. Implement event presentation router over `StoryRuntimeEvent`.
4. Implement readonly debug snapshot/source for Story runtime state.
5. Implement UI Toolkit editor panel or Framework Manager-compatible surface.
6. Add focused EditMode tests for command enqueue, direct-mutation guard, event routing, editor snapshot/view model binding.
7. Update interface docs and usage.
8. Use Unity MCP to verify compile/import, targeted tests, and editor window/menu smoke if available.

## Acceptance Criteria

- Unity trigger adapter writes `Story.RaiseTrigger` `RuntimeCommand` into the configured command buffer.
- Presentation completion adapter writes `Story.CompletePresentation` `RuntimeCommand`.
- Event presentation router consumes `StoryRuntimeEvent` and remains presentation-only.
- No Unity adapter directly mutates `StoryDirector` state.
- Runtime `Story.Unity` assembly does not reference `UnityEditor`.
- Editor debug surface uses UI Toolkit or existing editor UI conventions.
- Editor debug surface shows current graph / active beat / blackboard / recent event information from snapshots.
- Editor debug surface is read-only unless an explicit debug command gate is introduced.
- Story.Unity and Story.Editor usage is documented.
- Unity assets, if any, are generated by approved Editor / Unity MCP workflow and include `.meta` files.

## Suggested Tests

- `StoryTriggerZoneAdapterTests.EnqueuesRaiseTriggerCommand`
- `StoryPresentationCompletionAdapterTests.EnqueuesCompletePresentationCommand`
- `StoryUnityAdapterBoundaryTests.DoesNotMutateDirectorDirectly`
- `StoryRuntimeEventPresentationRouterTests.DispatchesPresentationEvents`
- `StoryRuntimeEventPresentationRouterTests.IgnoresUnsupportedEvents`
- `StoryEditorDebugSourceTests.BuildsSnapshotFromStoryRuntime`
- `StoryEditorDebugWindowTests.BuildsReadonlyUiToolkitTree`

## Validation

Required:

```text
git diff --check
Unity MCP refresh/import with Console error count 0
Targeted EditMode tests for Story.Unity and Story.Editor
Unity MCP editor window/menu smoke check if a menu item or EditorWindow is added
```

If Unity MCP cannot inspect a new EditorWindow, the PR must say what was validated instead and why.

## Handoff Notes

The implementation agent must report:

- files read.
- files changed.
- module impact.
- public API impact.
- Docs / ADR status.
- exact validation run.
- whether any Timeline/Cinemachine-specific behavior remains deferred.
