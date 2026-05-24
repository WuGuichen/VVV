# STORY_S5_RUNTIME_AI_PLANNER_DEMO

## Goal

Implement Issue #441:

- `MxFramework.Story.RuntimeAiPlannerBridge`
- the first Story playable vertical slice:
  `trigger zone -> dialogue / choice -> Gameplay command effect -> save / replay verification`

The demo must prove that Story state remains authoritative in Runtime, Unity/UI only submits intents, Gameplay effects flow through Gameplay-owned commands, and replay/save can restore Story progress.

## Workflow Level

- Issue: `#441`
- Task level: `S3`
- Delivery level: `Playable`
- Suggested branch: `feature/441-story-runtime-ai-planner-demo`
- Required PR: yes
- Merge policy: implementation agent opens PR and does not merge it.

## Preconditions

- #437, #438, #439, and #440 are merged.
- S3 effect strategy is fixed: direct buff grant/remove remains deferred; this demo must use an existing Gameplay-owned command path such as component attribute set/add or component ability cast.
- This document is the required pre-implementation API reuse plan.

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
13. `Docs/Interfaces/Story.Config.md`
14. `Docs/Interfaces/Story.GameplayBridge.md`
15. `Docs/Interfaces/Story.ResourcesBridge.md`
16. `Docs/Interfaces/Story.Unity.md`
17. `Docs/Interfaces/Story.Editor.md`
18. `Docs/Interfaces/AI.md`
19. `Docs/Interfaces/Gameplay.md`
20. `Docs/Interfaces/UI.Toolkit.md`
21. Existing Runtime Showcase / UI Toolkit demo source patterns.
22. This task document.

## API Reuse Plan

| Need | Framework API / Module | Use in S5 | Notes / intentional gaps |
| --- | --- | --- | --- |
| Runtime loop | `RuntimeHost`, `IRuntimeModule`, `RuntimeTickContext`, `RuntimeFrame` | Yes | Demo composition root must tick Story and Gameplay through RuntimeHost or a documented equivalent host path. |
| Command input | `RuntimeCommandBuffer`, `RuntimeCommand`, Story / Gameplay command factories | Yes | Trigger and UI choice actions enqueue commands; no direct StoryDirector mutation from Unity/UI. |
| Story authority | `StoryRuntimeModule`, `StoryDirector`, `StoryRuntimeHashContributor`, `StoryRuntimeSaveStateProvider` | Yes | Story owns graph, beats, choices, blackboard, save/restore, and replay/hash verification. |
| Story config | `Story.Config` rows, `StoryGraphConfigMapper`, `StoryConfigValidator` | Yes | Demo graph should be config-driven or built from the same config row contracts. Avoid hard-coded graph-only demo unless a clear gap is documented. |
| Gameplay effect bridge | `Story.GameplayBridge`, `StoryGameplayEffectBridge`, `StoryGameplayEffectIntent` | Yes | Use existing Gameplay-owned RuntimeCommand support. Direct buff grant/remove remains deferred and must not be used for demo success. |
| Resources preload | `Story.ResourcesBridge`, `ResourcePreloadPlan`, `ResourceKey` / labels | Yes | Demo should produce a preload plan for its UI/text/sample resources. Actual loading may be memory/no-op if no asset dependency is needed, but the plan path must be exercised. |
| Unity input/presentation | `Story.Unity` trigger / presentation / event router adapters | Yes | Unity layer raises trigger and complete-presentation intents through runtime commands, and consumes StoryRuntimeEvent for presentation updates. |
| Runtime AI Planner projection | `IAiWorldState`, `AiWorldState`, `AiFactKey`, Story.RuntimeAiPlannerBridge | Yes | One-way projection from whitelisted `StoryFactKey` to Runtime AI Planner facts. It never writes back to Story blackboard. |
| UI Toolkit | `MxFramework.UI.Toolkit`, UXML / USS / stable element names | Yes | UI labels/buttons must be visible, non-empty, and bound through a view/controller. No formal OnGUI surface. |
| Save / restore | `RuntimeSaveState`, `StoryRuntimeSaveStateProvider`, JSON roundtrip helpers | Yes | Demo/test must restore at expected beat/step or choice state. |
| Replay / hash | `RuntimeReplayRecorder` / Story runtime hash path | Yes | Command replay must reproduce the same Story runtime hash. |
| Playable scene | Unity Editor / Unity MCP generated scene and assets | Yes | Scene/prefab/asset YAML must not be handwritten. Use Unity MCP, existing Editor menus, or scripts executed in Unity to generate assets. |

## Runtime AI Planner Bridge Strategy

Add `MxFramework.Story.RuntimeAiPlannerBridge` as a noEngine sibling bridge.

Expected minimum public surface:

- stable mapping from `StoryFactKey` to `AiFactKey`.
- whitelist / projection profile so only explicit Story facts enter `IAiWorldState`.
- projection method that reads `IStoryBlackboard` or `StoryDirectorSnapshot` and writes facts to caller-owned `IAiWorldState`.
- diagnostics for missing facts, unsupported `StoryValueKind`, and skipped unlisted facts.

Rules:

- Bridge depends on `MxFramework.Story` and `MxFramework.AI`.
- Bridge must not depend on Unity, Gameplay, Resources, Story.Unity, or Story.Editor.
- Projection is one-way Story -> Runtime AI Planner world state.
- It must not call `IStoryBlackboard.Set`, `StoryDirector` mutation APIs, or any Runtime AI Planner behavior that mutates Story.

## Playable Demo Strategy

Preferred scene:

```text
Assets/Scenes/StoryRuntimeVerticalSlice.unity
```

Equivalent naming is acceptable if documented in `Docs/USAGE.md`.

Minimum playable loop:

1. Player enters or presses a visible trigger control.
2. Unity adapter enqueues `Story.RaiseTrigger`.
3. Story enters a dialogue / presentation beat and offers a choice.
4. UI Toolkit view displays non-empty dialogue and choice button labels.
5. Choice button enqueues `Story.SelectChoice` with `beatInstanceId + choiceId`.
6. Choice effect reaches Gameplay through `StoryGameplayEffectBridge` using an existing Gameplay-owned command.
7. Visible runtime state confirms the Gameplay effect.
8. Save / restore returns to the expected Story beat / step / choice state.
9. Replay of the command sequence produces the same Story runtime hash.
10. Runtime AI Planner projection displays or tests expected projected Story facts.

## Allowed Changes

- `Assets/Scripts/MxFramework/Story.RuntimeAiPlannerBridge/**`
- `Assets/Scripts/MxFramework/Demo/Story/**`
- `Assets/Scripts/MxFramework/Tests/Story.RuntimeAiPlannerBridge/**`
- `Assets/Scripts/MxFramework/Tests/Demo/Story/**`
- `Assets/Scenes/StoryRuntimeVerticalSlice.unity` and `.meta`, generated through Unity Editor / Unity MCP / approved menu.
- `Assets/UI/MxFramework/Story/**` UXML / USS / PanelSettings and `.meta`, generated or imported through approved Unity workflow.
- small demo config assets/files under `Assets/Config/MxFramework/Story/**` when generated through approved workflow.
- `Docs/Interfaces/Story.RuntimeAiPlannerBridge.md`
- `Docs/INTERFACES.md`
- `Docs/CAPABILITIES.md`
- `Docs/USAGE.md`
- this task document.

## Out Of Scope

- New Story core/runtime semantics beyond accepted S1-S4 behavior.
- Third-party narrative DSL importers.
- Real WGame story content, world lore, character names, production Buff ids, or production level data.
- Runtime AI Planner becoming authoritative over Story blackboard.
- Direct buff grant/remove from Story.
- Timeline / Cinemachine package-specific binding beyond existing generic Story event presentation router.
- Handwritten `.unity`, `.prefab`, `.asset`, or ScriptableObject YAML.

## Suggested Slice Order

1. Implement noEngine `Story.RuntimeAiPlannerBridge` and focused projection tests.
2. Build pure C# demo graph/config, command path, save/restore, replay/hash tests.
3. Add Unity runtime demo composition root and UI Toolkit view/controller.
4. Generate scene and UI assets through Unity MCP / approved Editor path.
5. Add PlayMode or EditMode smoke tests for demo command path where feasible.
6. Run a manual playable smoke in Unity: trigger, choose option, observe Gameplay effect, save/restore, replay/hash.
7. Update docs and PR checklist.

## Acceptance Criteria

- API reuse plan exists before coding.
- `Story.RuntimeAiPlannerBridge` projects only whitelisted Story facts to `IAiWorldState`.
- Projection never writes back to Story.
- Playable scene exists and can be opened.
- Demo uses `RuntimeHost`, `RuntimeCommandBuffer`, and `StoryRuntimeModule` as authoritative path.
- Trigger interaction raises `Story.RaiseTrigger`.
- UI choice raises `Story.SelectChoice` with live `beatInstanceId + choiceId`.
- Choice effect reaches Gameplay through S3-approved Gameplay-owned command path.
- Visible UI/runtime state confirms the effect.
- Save/restore resumes Story progress at expected beat/step.
- Replay reproduces the same Story runtime hash.
- UI Toolkit labels/buttons are visible and non-empty.
- Unity Console has no errors after import and playable smoke.

## Validation

Required:

```text
git diff --check
Unity MCP refresh/import with Console error count 0
Targeted EditMode tests for Story.RuntimeAiPlannerBridge
Targeted demo tests for command path, save/restore, and replay/hash
Playable smoke in Unity scene: trigger -> choice -> Gameplay effect -> save/restore -> replay/hash
```

The PR must explicitly state whether validation reached `Playable`. If any playable criterion is not met, the PR must mark the delivery as blocked or runtime slice only, not done.

## Handoff Notes

The implementation agent must report:

- files read.
- files changed.
- module impact.
- public API impact.
- Docs / ADR status.
- Unity asset generation method.
- exact validation run.
- playable smoke result.
- remaining deferred Runtime AI Planner, Timeline/Cinemachine, or authoring-tool gaps.
