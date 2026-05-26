# FairyGUI Story UI Migration Readiness (#528)

## Goal

Define the conditions for moving Story dialog, choices and presentation
acknowledgements to FairyGUI without moving Story authority into UI code.

This slice is a readiness contract. It does not implement a FairyGUI Story
package, generated manifest or binder.

## Current Story Surface

Story authority lives in:

- `Assets/Scripts/MxFramework/Story/StoryDirector.cs`
- `Assets/Scripts/MxFramework/Story.Runtime/StoryRuntimeModule.cs`
- `Assets/Scripts/MxFramework/Story.Runtime/StoryRuntimeCommands.cs`

The current Unity-facing presentation bridge lives in:

- `Assets/Scripts/MxFramework/Story.Unity/StoryRuntimeEventPresentationRouter.cs`
- `Assets/Scripts/MxFramework/Story.Unity/StoryPresentationCompletionAdapter.cs`
- `Assets/Scripts/MxFramework/Story.Unity/StoryUnityCommandAdapter.cs`

The current playable vertical slice remains UI Toolkit:

- `Assets/Scripts/MxFramework/Demo/Story/StoryRuntimeVerticalSliceDemo.cs`
- `Assets/Scripts/MxFramework/Demo/Story/StoryRuntimeVerticalSliceRunner.cs`
- `Assets/UI/MxFramework/Story/StoryRuntimeVerticalSlice.uxml`
- `Assets/UI/MxFramework/Story/StoryRuntimeVerticalSlice.uss`
- `Assets/Scenes/StoryRuntimeVerticalSlice.unity`

No `MxFramework.Story*` runtime assembly may reference FairyGUI. The existing
dependency scan is clean:

```bash
rg -n "FairyGUI|Fgui|FairyGui" Assets/Scripts/MxFramework/Story Assets/Scripts/MxFramework/Story.Runtime Assets/Scripts/MxFramework/Story.Unity Assets/Scripts/MxFramework/Story.Config Assets/Scripts/MxFramework/Story.GameplayBridge Assets/Scripts/MxFramework/Story.ResourcesBridge -g '*.cs' -g '*.asmdef'
```

The command should produce no matches.

## Runtime To UI Mapping

Story UI should be a projection over Story runtime state and commands.

| Runtime source | UI ViewModel field | UI command |
| --- | --- | --- |
| `StoryBeatInstanceSnapshot.PendingPresentationStepId` with `StoryPresentationWaitPolicy.WaitForCommand` or `WaitWithFrameTimeout` | `WaitingBeatInstanceId`, `WaitingStepId`, `CanContinue`, `ContinueLabelKey` | `Story.CompletePresentation` |
| `StoryStepDefinition.TextKey`, `SpeakerId`, `ResourceId` | `DialogueTextKey`, `SpeakerId`, `PortraitResourceId`, optional fallback display text | none |
| `StoryChoiceView.ChoiceId`, `LabelTextKey`, `Enabled` from `IStoryChoiceSnapshotReader.GetChoices` | `Choices[]` with `ChoiceId`, `LabelTextKey`, `Enabled`, fallback display text | `Story.SelectChoice` |
| `StoryEventKind.ChoiceOffered` / `ChoiceResolved` | transition state and diagnostics | none |
| `StoryEventKind.GraphCompleted` / `GraphAborted` | terminal presentation state | optional close command handled by UI shell |
| `StoryRuntimeModule.LastCommandErrors` | diagnostics only | none |

Minimum ViewModel shape for later implementation:

- `StoryDialogViewModel`: `GraphId`, `BeatId`, `BeatInstanceId`, `StepId`,
  `StepKind`, `TextKey`, `SpeakerId`, `ResourceId`, `WaitPolicy`,
  `CanContinue`, `IsModal`.
- `StoryChoiceViewModel`: `GraphId`, `BeatInstanceId`, `ChoiceSetId`,
  `Choices[]` with `ChoiceId`, `LabelTextKey`, `Enabled`.

The FairyGUI binder must only emit `MxUiCommand` ids. The composition root maps
those UI commands to `StoryRuntimeCommandFactory` calls and enqueues them into
`StoryRuntimeModule.CommandBuffer`.

Recommended command ids for a later implementation:

- `story.presentation.complete`
- `story.choice.select`
- `story.dialog.close`
- `ui.cancel`

The command payload must carry at least:

- `GraphId`
- `BeatInstanceId`
- `StepId` for presentation completion
- `ChoiceId` for choice selection

## Localization Rules

Story runtime already uses integer text keys in `StoryStepDefinition.TextKey`
and `StoryChoiceDefinition.LabelTextKey`. FairyGUI Story UI must not expose
`Config.LocalizedTextKey` or call `ILocalizationProvider` directly.

The Story FairyGUI composition layer should adapt Story text ids to UI text
requests:

- `StoryStepDefinition.TextKey` -> `MxUiTextKey`
- `StoryChoiceDefinition.LabelTextKey` -> `MxUiTextKey`
- fallback text may come from current demo-only resolver while the real
  localization database is still out of scope

The adapter receives `IMxUiTextProvider` by explicit composition. Locale changes
use `IMxUiTextProvider.Revision` and a view rebind/refresh path.

## Modal And Focus Rules

Story dialog and choices are player-facing modal UI. A later FairyGUI Story
implementation must use the productized rules from #523 and #524:

- Blocking Story dialog/choice views set `MxUiViewDescriptor.Modal = true` and
  use `MxUiLayer.Modal`; explicitly non-blocking presentation may use
  `MxUiLayer.Panel` or `MxUiLayer.Popup`.
- Story dialog opens on a layer owned by `IMxFairyGuiLayerHost`.
- Choice UI blocks lower-layer gameplay commands while a choice is active.
- Back/cancel maps to an explicit `MxUiCommand`; it must not mutate
  `StoryDirector` directly.
- Opening a Story modal pushes `InputContext.UI`; closing or completing it pops
  the context through `IMxFairyGuiInputContextBridge`.
- `CloseOnSceneChange` disposes Story UI package/component state; `KeepAlive`
  is allowed only for non-authoritative cached presentation.

## Validation Before Implementation

A later Story FairyGUI implementation needs three validation layers:

1. NoEngine contract tests

   - Map a waiting presentation snapshot to a ViewModel.
   - Map choices from `IStoryChoiceSnapshotReader` to `Choices[]`.
   - Translate `MxUiCommand story.presentation.complete` to
     `StoryRuntimeCommandFactory.CompletePresentation`.
   - Translate `MxUiCommand story.choice.select` to
     `StoryRuntimeCommandFactory.SelectChoice`.
   - Verify missing localization keys fall back through `IMxUiTextProvider`
     contract rather than Config globals.

2. FairyGUI manifest/generator validation

   - Package bytes and component source XML validate through
     `MxFairyGuiManifestValidator`.
   - Dialog text, speaker label and choices are listed as named controls.
   - Localized text controls use `MxFairyGuiLocalizedTextBinding`.
   - Choice buttons are command controls and validate as button-like controls.
   - Generated manifest output participates in the stale gate.

3. Runtime smoke

   - Open Story dialog after `RaiseTrigger`.
   - Complete a waiting presentation through UI command.
   - Select a choice through UI command.
   - Verify graph completion, gameplay effect, replay/hash and save/restore
     parity against the existing UI Toolkit vertical slice.
   - Verify modal input blocks gameplay controls while choices are active.

## Migration Criteria

Story UI is ready to migrate only when:

- the Story ViewModel and command DTOs are noEngine-testable without FairyGUI
- Story runtime assemblies still have no FairyGUI dependency
- all Story UI user actions flow through `MxUiCommand` and then
  `StoryRuntimeCommandFactory`
- localization keys are visible in manifest/generator output
- modal/focus behavior is covered by a smoke path
- the UI Toolkit Story vertical slice remains available as diagnostics until
  the FairyGUI replacement reaches feature parity

## Follow-Up Implementation Issues

Create implementation issues instead of expanding #528:

- Story FairyGUI package and manifest generation.
- Story ViewModel and UI command adapter.
- Story FairyGUI binder and shell.
- Runtime smoke scene or opt-in Story scene path.
- Real localization provider adapter for Story text ids.
- Feature-parity replacement decision for the UI Toolkit vertical slice.

## Non-Goals

- Do not build the Story FairyGUI package in this issue.
- Do not remove `StoryRuntimeVerticalSliceRunner` or UI Toolkit assets.
- Do not change `StoryDirector`, `StoryRuntimeModule` or Story save/replay
  semantics.
- Do not introduce FairyGUI references into Story runtime, Unity, Config,
  GameplayBridge or ResourcesBridge assemblies.
- Do not implement a full localization database.
