# Phase 12 M6: UI Toolkit Framework Components

> **状态**: Accepted（2026-05-09）
> **优先级**: P1
> **父任务**: `PHASE12_UI_TOOLKIT_SHOWCASE_GOAL.md`
> **并行批次**: UI Toolkit lane

## Goal

Extract reusable UI Toolkit components and theme tokens from the accepted Runtime Showcase M3/M4/M5 UI, without changing gameplay behavior.

The goal is not to redesign the Showcase. It is to stabilize the component vocabulary so future Config, Mod, AI, Preview, and Showcase panels can share one UI style and binding approach.

## Scope

### Do

- Review current `GameplayShowcase.uxml`, `GameplayShowcase.uss`, `MxRuntimeHudController`, and `MxRuntimeHudViewModel`.
- Define theme token / class naming rules for:
  - status color;
  - command states;
  - panel section rhythm;
  - compact event rows;
  - diagnostic tabs.
- Extract or introduce reusable candidates where the existing code supports it:
  - `MxStatBar`;
  - `MxCommandButton`;
  - `MxStatusBadge`;
  - `MxEventLog`;
  - `MxPanelTabs`;
  - optional `MxTooltip` only if it falls out naturally.
- Keep M3/M4/M5 visual tree names stable:
  - `mini-game-feedback`;
  - `manual-controls`;
  - `config-controls`;
  - `diagnostic-view`.
- Add tests that prove the key visual tree names still exist and component classes / view models can be constructed.
- Add closeout notes to this task document.

### Do Not

- Do not modify Preview Runtime 03.5 files.
- Do not change Gameplay, Config.Runtime, Combat, AIAction, or WGame data.
- Do not remove M3/M4/M5 features.
- Do not introduce third-party UI frameworks.

## Expected Files

Allowed write scope:

```text
Assets/Scripts/MxFramework/UI.Toolkit/
Assets/Scripts/MxFramework/Demo/Ability/
Assets/Scripts/MxFramework/Tests/Ability/
Assets/UI/MxFramework/Showcase/GameplayShowcase.uxml
Assets/UI/MxFramework/Showcase/GameplayShowcase.uss
Docs/Tasks/PHASE12_UI_TOOLKIT_M6_COMPONENTS.md
Docs/Tasks/PHASE12_UI_TOOLKIT_SHOWCASE_GOAL.md
Docs/CAPABILITIES.md
```

Be careful: the UXML / USS files already have local modifications. Read them before editing and preserve unrelated changes.

## Acceptance

- Reusable component / token layer exists and is documented in this task closeout.
- M3/M4/M5 tests remain green.
- `MxFramework.UI.Toolkit` still does not reference Demo, Gameplay, Config.Runtime, Combat, UnityEditor, or WGame business types.
- Visual tree still contains the accepted M3/M4/M5 root names.
- No Runtime Core module gains UI Toolkit or UnityEngine dependencies.

## Suggested Verification

```text
dotnet build WGameFramework.sln --no-restore -v minimal
Unity EditMode: MxFramework.Tests.Ability.RuntimeAbilitySliceConfigPanelTests
Unity EditMode: MxFramework.Tests.Ability.RuntimeAbilitySliceDiagnosticViewModelBuilderTests
Unity EditMode: MxFramework.Tests.Ability.RuntimeAbilitySliceMiniGameFeedbackTests
UI.Toolkit boundary static check
Unity Console: 0 compile error
git diff --stat && git diff --check
```

## Dispatch Notes

You are not alone in the codebase. Do not revert unrelated local UXML / USS changes. Work only in the UI Toolkit lane.

## Closeout Notes

### Extracted Component Layer

M6 introduced the reusable UI Toolkit vocabulary in `MxRuntimeHudComponents`:

- `MxUiThemeTokens` / `MxUiTone`: shared USS class tokens for panel rhythm, command state, status tone, stat bars, compact event rows, and diagnostic tabs.
- `MxStatusBadge`: text badge with exclusive neutral / positive / warning / danger tone classes.
- `MxCommandButton`: command button state helper for enabled / hot / muted and tooltip text.
- `MxStatBar`: fill-based stat bar with normalized value and tone class.
- `MxEventLog`: compact newest-first event row list with stable empty state.
- `MxPanelTabs`: tab strip helper with active tab class and selection callback.

`MxTooltip` was intentionally not added. Existing command tooltips are handled by `Button.tooltip`, and a separate tooltip component would add API surface before there is a second concrete consumer.

### Showcase Migration

`GameplayShowcase.uxml` keeps the accepted M3/M4/M5 visual tree names stable:

- `mini-game-feedback`
- `manual-controls`
- `config-controls`
- `diagnostic-view`

The same tree now carries reusable classes such as `mx-panel`, `mx-command-button`, `mx-status-badge`, `mx-stat-bar__fill`, `mx-event-log`, `mx-panel-tabs`, and `mx-panel-tabs__tab`. `GameplayShowcase.uss` maps those reusable classes onto the already accepted M3/M4/M5 visual styling, preserving the existing Showcase layout instead of redesigning it.

`MxRuntimeHudController` now applies the same token classes at runtime when it creates dynamic status, command, diagnostic tab, and event-row state.

### Verification

- `dotnet build WGameFramework.sln --no-restore -v minimal`
  - Passed, 0 errors.
  - Existing unrelated warnings remain in Demo scene config / Combat editor code.
- Unity MCP EditMode tests:
  - `MxFramework.Tests.Ability.RuntimeAbilitySliceConfigPanelTests`
  - `MxFramework.Tests.Ability.RuntimeAbilitySliceDiagnosticViewModelBuilderTests`
  - `MxFramework.Tests.Ability.RuntimeAbilitySliceMiniGameFeedbackTests`
  - `MxFramework.Tests.Ability.RuntimeAbilitySliceUiToolkitComponentTests`
  - Passed, 11 / 11.
- UI.Toolkit boundary static check:
  - No `MxFramework.Demo`, `MxFramework.Gameplay`, `MxFramework.Config.Runtime`, `MxFramework.Combat`, `UnityEditor`, or `WGame` references under `Assets/Scripts/MxFramework/UI.Toolkit`.
  - No `UnityEngine`, `UnityEngine.UIElements`, or `MxFramework.UI.Toolkit` references found under Runtime Core no-engine modules checked: `Core`, `Events`, `Attributes`, `Modifiers`, `Buffs`, `Config`, `AI`, `Diagnostics`.
- Unity Console:
  - 0 error after the EditMode test run.

### Boundary Conclusion

M6 stayed inside the UI Toolkit lane. It did not touch Preview Runtime 03.5, Combat, AIAction, or WGame data. The reusable component layer depends only on UnityEngine / UI Toolkit and remains outside the pure Runtime Core modules.

### Remaining Risks

1. The reusable components are now constructible and styled, but the Showcase still uses standard UXML elements with token classes rather than custom UXML tags.
2. `MxEventLog` and `MxPanelTabs` have focused construction tests, not full Play Mode interaction tests.
3. Visual validation is covered by UXML/class existence and prior Showcase tests; a screenshot-based regression test is still future work.
