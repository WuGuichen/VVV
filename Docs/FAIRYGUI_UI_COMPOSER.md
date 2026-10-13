# FairyGUI Semantic UI Composer

> Status: Guide
> 文档版本：2026-10-12.1

## Purpose

UI Composer turns hand-curated semantic UI resources into real FairyGUI
components and screens without moving layout or binding authority into the web
browser. It supports multiple ownership-constrained screens. The regression
slice remains `MxUiFoundationDemo`; the player-facing slices own the CharacterTest
main menu, Combat HUD, Settings, Network, and Ability Binding screens in separate
Composer-owned packages.

For daily operation, shortcuts, recovery, diagnostics, snapshots, review, and
troubleshooting, read `Docs/Guides/FAIRYGUI_UI_COMPOSER_USER_GUIDE.md` first.

Automatic Click/Back and command-result Confirm/Error feedback is designed in
`Docs/UI_AUDIO_FEEDBACK_AUTOMATION_DESIGN.md`. It preserves the current binding
authority: feedback policy originates in the reviewed C# generation spec and is
only a read-only Composer contract projection. The noEngine command identity,
outcome, policy registry, feedback decorator, v2 contract, generated policy
projection, v1 silent compatibility, and Composer read-only display are current
in `Docs/Interfaces/UI.md`. The optional Audio bridge is also current; published
Cue content remains planned. CharacterTest Settings is the first current
player-facing runtime consumer; other screens still require explicit composition,
so Composer feedback metadata alone does not imply audible behavior.

## Authority

| Concern | Authority |
| --- | --- |
| PNG identity, semantic use, NineSlice, replacement constraints | `ui-art-resources.json` |
| Reusable visual component recipe | `ui-component-recipes.json` |
| External editable screen projection | `screens/*.ui-screen.json` |
| Actual package resources and component layout | FairyGUI package/component XML |
| Semantic node to FairyGUI id mapping and synchronization hashes | package-local `ui-composer-state.json` |
| Controls, commands, manifest and binder source | reviewed C# generation spec |
| Published bytes and atlas | FairyGUI Editor/helper publish |

ADR-007 defines conflict handling. Saving a blueprint does not modify FairyGUI.
Apply and sync are separate hash-guarded operations.

Recipe save, unified preflight and screen/apply validation reject duplicate
effective `Components/<name>.xml` paths across the recipe catalog (`UICOMP020`),
including controlled Composite recipes and recipes not used by the current screen.
The effective name is `fairyGuiComponentName`, or the final segment of `componentId`
when the name is blank. Names use the helper's ASCII letter/digit/underscore
alphabet (`UICOMP019`). Paths compare without case on every host to preserve
Windows compatibility. Diagnostics locate both conflicting recipe IDs; callers
must choose distinct names rather than relying on renaming or overwrite order.
The FairyGUI helper independently rejects duplicate component paths in the entire
payload before accessing the project, pruning components or writing package files.
Existing hash and binding-contract guards still apply.

The Authoring regression entry is `ui-composer-component-paths`; the helper
fixture entry is `node Tools/MxFramework.FairyGuiUiComposer/scripts/test_component_path_guard.js`.
These checks cover planning and write rejection; fixture success alone does not
prove FairyGUI Editor import, publish or runtime visuals.

## Current Implementation

The v3 blueprint and v5 Authoring API flow is current for the tool-owned
`MxUiFoundationDemo`, `MxCharacterTestUi`, `MxCharacterTestCombatHud`,
`MxCharacterTestSettingsUi`, `MxCharacterTestNetworkUi`,
`MxCharacterTestAbilityUi`, `MxCharacterTestRuntimeAiLabUi`,
`MxCharacterTestRuntimeAiLabRecoveryUi`, `MxCharacterTestBehaviorDeckUi`,
`MxCharacterTestTacticalDeckUi`, and `MxPlayerGuidanceUi`
packages:

- `ArtSource/UIKits/MxUiFoundation/ui-component-recipes.json` defines the demo
  button and the primary, neutral, and danger command-button recipes.
- `ArtSource/UIKits/MxUiFoundation/screens/ui_foundation_demo.ui-screen.json`
  contains the seven-node sample blueprint.
- `ArtSource/UIKits/MxUiFoundation/screens/character_test_main_menu.ui-screen.json`
  contains the CharacterTest main-menu blueprint and semantic image references.
- `ArtSource/UIKits/MxUiFoundation/screens/character_test_combat_hud.ui-screen.json`
  contains the 178-node CharacterTest Combat HUD blueprint. Fourteen HUD
  resources cover plates, bars, icons, slots, crosshair, feedback, and network
  presentation.
- `character_test_settings.ui-screen.json`, `character_test_network.ui-screen.json`,
  and `character_test_ability_binding.ui-screen.json` preserve the existing
  player-facing control names and behavior contracts while replacing legacy
  package-local art with semantic resources and compact button recipes.
- `character_test_runtime_ai_lab.ui-screen.json` keeps Setup as the initial
  shell and references controlled behavior-card-flow and five-track-timeline
  Composite recipes. It has passed unified preflight, Apply and Publish, and
  the CharacterTest runtime selects the dedicated package.
- `player_guidance_package.ui-screen.json` is the package-level authority for
  `MxPlayerGuidanceUi`. It references controlled Card and two-slot Notice Stack
  Composite recipes; generated component bindings expose
  `GuidanceCardPanel` and `GuidanceNoticeStackPanel` as the `Hud` and `Toast`
  runtime views without duplicating Screen authority.
- `ArtSource/UIKits/MxUiFoundation/contracts/` contains generated, read-only
  binding contracts. Contract hashes guard save, apply, sync, and publish.
- `Tools/MxFramework.FairyGuiUiComposer/web/` is the external dark Chinese
  workbench linked from EditorHub. It supports tree grouping, Ctrl/Shift
  multi-selection, marquee selection, atomic group movement, batch alignment,
  distribution, size matching, and persistent Free/Horizontal/Vertical/Grid
  layout containers.
- The workbench also provides copy/paste/duplicate, per-screen lock/hide/isolate
  view state, hierarchy search and filters, selection focus, fit-to-canvas,
  Space/middle-button panning, Ctrl+wheel zoom, four-resolution comparison,
  reversible reference replacement, favorites/recent ordering, reusable node
  templates, and a `Ctrl+K` command palette.
- Unsaved resource, recipe, and blueprint drafts are mirrored to browser-local
  recovery storage after a short debounce. Recovery is offered only when all
  four authoring hashes still match; it never bypasses the normal save or Apply
  validation path. Local snapshots and the review dialog provide four-layer
  added/changed/removed summaries and a read-only review mode.
- Diagnostics can locate referenced nodes, recipes, or resources. Only
  unambiguous reversible repairs are offered automatically; all other
  diagnostics remain locate-and-review actions.
- Authoring Core validates, saves, plans, round-trips supported XML, guards all
  writes with hashes, and queues operation ids under `FGUIProject/.agent/`.
- `FGUIProject/plugins/wgameframework-agent-helper` exclusively materializes the
  new package, persists FairyGUI ids, applies NineSlice metadata, and publishes
  with `genCode=false`.
- All ten owned packages have real FairyGUI publish outputs. The generated
  manifest/binder registry keeps the Demo target, routes the CharacterTest
  main menu to `MxCharacterTestUi`, and routes the Combat HUD to
  `MxCharacterTestCombatHud`; Player Guidance routes one shared package to
  `ui.guidance.card` and `ui.guidance.notices`.
- `Assets/Scenes/MxFramework/UiFoundationDemo.unity` loads the published package
  through `MxFramework.Resources`, opens it through the normal FairyGUI layer
  host, and owns modal UI input plus released cursor scopes.

## Development Agent Creation Protocol

For a new or migrated formal player-facing screen, the Development Agent must
start from the Composer authorities rather than from FairyGUI XML:

1. Reuse or add semantic PNG entries in `ui-art-resources.json`.
2. Reuse a standard Button recipe, or add a reviewed controlled recipe in
   `ui-component-recipes.json`; arbitrary component trees are not accepted.
3. Add the screen blueprint and generated read-only binding contract, then add
   the screen to the Authoring allowlist.
4. Use the external workbench to validate the resource, recipe, blueprint, and
   contract together. Resolve diagnostics before creating an Apply request.
5. Let the FairyGUI helper materialize package XML and ids. Publish only after
   the Apply result and hashes match.
6. Register package bytes/atlas in `MxFramework.Resources`, generate the
   manifest/binder, and run real-package Unity loading and interaction tests
   before switching runtime ids.

Debug, Showcase, Editor, and Validation surfaces remain UI Toolkit by default.
Legacy FairyGUI packages are maintained directly only when they are not
Composer-owned; migration should not enlarge the handwritten XML surface.

The current Authoring Server surface is:

- `GET /api/authoring/ui-composer/capabilities`
- `GET /api/authoring/ui-composer/screens`
- `GET /api/authoring/ui-composer/state`
- `GET /api/authoring/ui-composer/workspace`
- `POST /api/authoring/ui-composer/plan-save-recipes`
- `POST /api/authoring/ui-composer/save-recipes`
- `POST /api/authoring/ui-composer/plan-delete-recipe`
- `POST /api/authoring/ui-composer/plan-workspace`
- `POST /api/authoring/ui-composer/apply-workspace`
- `POST /api/authoring/ui-composer/validate`
- `POST /api/authoring/ui-composer/save`
- `POST /api/authoring/ui-composer/plan-apply`
- `POST /api/authoring/ui-composer/apply`
- `POST /api/authoring/ui-composer/plan-sync`
- `POST /api/authoring/ui-composer/sync`
- `GET /api/authoring/ui-composer/operation-status`
- `POST /api/authoring/ui-composer/publish`

## Screen Blueprint V1-V3 Model

Component recipes contain stable component identity and default size. `Button`
recipes own semantic resource slots, fixed button state presentation,
controlled icon slots, and editable instance properties. Controlled
`Composite` recipes own a deterministic child-node template and may reference
semantic resources or other recipes; dependency cycles and unknown references
are rejected before materialization. CharacterTest command buttons share one
semantic NineSlice base while Graph state overlays distinguish primary,
neutral, and danger variants.

Screen blueprints contain:

- screen identity and target package/component;
- 1920x1080 design resolution;
- ordered Graph, Text, Image, ComponentInstance, and (in V3) List nodes;
- position, size, horizontal/vertical anchor, and root relation policy;
- semantic component/resource references;
- assignment to binding slots exposed by the C# contract.
- optional authoring-only layout containers. A container owns stable identity,
  parent/order, mode, design rectangle, padding, gaps, grid columns, and
  main/cross-axis alignment; leaf nodes reference it through
  `parentContainerId`.

Containers are blueprint authority only. Apply recursively resolves nested
container positions and writes the same flat FairyGUI `displayList`; containers
do not receive FairyGUI ids, binding slots, mapping entries, or runtime objects.
`Free` preserves relative child coordinates. `Horizontal`, `Vertical`, and
`Grid` derive child positions from order while preserving child dimensions.
Existing V1/V2 files remain at their declared version. Creating a layout
container upgrades a V1 blueprint to V2; creating or pasting a List upgrades
the blueprint to `mx.uiScreenBlueprint.v3`. Layout operations must not
downgrade an existing V3 blueprint.

Image nodes store only `resourceId` and tint. Pixel path, NineSlice grid,
preserve-aspect behavior, and replacement constraints resolve from the UI Art
Resource Library instead of being copied into screen blueprints.

V3 exposes a controlled Screen-level List node. A List owns a single default
item component recipe, Column/Row layout, line/column gaps, one of FairyGUI's
four selection modes, Scroll overflow, and a runtime virtualization intent.
Apply materializes it as a FairyGUI `<list>` whose `defaultItem` is the mapped
`ui://` component URL; Row lists also materialize horizontal scrolling. The
item recipe participates in dependency collection and reverse-reference
protection. Sync accepts only this controlled XML subset and preserves the
virtualization intent because FairyGUI source XML does not enable runtime
virtual mode. Runtime code must still install `itemRenderer`, call
`GList.SetVirtual()` when requested, and set `numItems`.

`MxCharacterTestRuntimeAiLabUi/RuntimeAiPlannerDevPanel` is the first production
consumer of this node: `workbenchEntryList` owns the behavior-card composition,
uses `ui.component.runtime_ai.behavior_card` as its default item, and leaves
selection commands plus selected-item actions to the runtime binder. The item
recipe, rather than runtime object creation, owns the inline enable/disable and
delete controls.

The V3 List payload is:

```json
{
  "type": "List",
  "list": {
    "itemComponentRecipeId": "ui.component.example.item",
    "layout": "Column",
    "lineGap": 8,
    "columnGap": 0,
    "selectionMode": "None",
    "overflow": "Scroll",
    "virtualized": false
  }
}
```

List does not accept arbitrary inline item children. Its item structure remains
component-recipe authority, and List is not allowed inside Composite recipe
templates in this version. V3 still does not expose arbitrary controllers,
transitions, animation, loaders, localization keys, scripts, or responsive
breakpoints. The generic button has one fixed standard `button` controller
generated from its recipe. Composite templates are controlled catalog content,
flatten to their own FairyGUI component XML, and do not accept screen binding
slots or layout containers.

## Unified Workspace

Composer v5 edits semantic resources, Button recipes, and screen nodes in one three-column workspace while preserving their separate authority files. The left rail switches between resources, recipes, hierarchy, and the creation palette; the canvas remains in the center; the right inspector follows references from a node to its recipe and source resources.

- Resource changes still use the UI Art Resource Library validation and catalog hash guard.
- Form editing is limited to standard FairyGUI Button recipes. Controlled
  Composite recipes are visible in the catalog, canvas, dependency graph, and
  reverse-reference index but remain read-only in the web Inspector. Stable IDs
  are read-only and referenced recipes cannot be deleted.
- Node and container changes remain screen-blueprint authority; changed recipe defaults never silently resize existing instances.
- The server rebuilds reverse references from every allowlisted Composer blueprint instead of trusting browser-provided usage lists.
- Resource, recipe, and blueprint drafts have independent dirty states. Unified preflight validates the proposed state of all three layers against catalog, recipe, contract, blueprint, mapping, and XML authority before any file is replaced.
- Workspace apply stages resource, recipe, and blueprint files as one optimistic-concurrency transaction. A replacement or helper-queue failure rolls every authoritative file back to its prior hash; only a committed transaction may queue the existing FairyGUI helper Apply operation. Apply-and-publish then waits for that result before using the existing publish operation. The browser never writes FairyGUI XML or published assets directly.
- The Authoring CLI exposes `ui-composer publish-all` as the non-browser orchestration authority. It performs the same typed preflight from on-disk authority, acquires a repository/package lock, waits for an explicit read-only helper `Probe`, then serializes Apply, source verification, Publish and artifact verification. It never round-trips the authority through PowerShell JSON.
- `publish-all --verify-unity` invokes the matching Unity MCP endpoint and accepts success only when `validate_unity_scripts` proves refresh, compilation, domain reload, Editor readiness and zero Console errors. Interaction profiles are explicit; they never fall back to coordinate automation.

The v4 API introduced `GET /workspace`, `POST /plan-save-recipes`, `POST /save-recipes`, `POST /plan-delete-recipe`, `POST /plan-workspace`, and `POST /apply-workspace` under `/api/authoring/ui-composer`. The v5 API retains those endpoints and adds the compatibility boundary required by Blueprint v3 List nodes, preventing a pre-List web client from interpreting a List as a component instance. Existing screen, sync, operation-status, and publish endpoints remain compatible within the v5 client/server pair.

## Workflow

```text
Resource Library
  -> Component Recipe
  -> Screen Blueprint Draft
  -> Validate / Plan Apply
  -> FairyGUI helper materializes package
  -> Optional FairyGUI layout micro-adjustment
  -> Plan Sync / Sync Blueprint
  -> Publish
  -> Generated manifest/binder check
  -> Unity preview scene
```

The programmatic equivalent is:

```text
ui-composer publish-all
  -> unified typed preflight
  -> package-scoped mutex
  -> helper Probe (reuse or launch FairyGUI)
  -> Materialize operation + fresh hashes
  -> Publish operation
  -> package/state/XML/bytes/atlas/manifest/catalog verification
  -> optional Unity MCP validation and registered interaction evidence
```

Each run records a parent operation id and child Probe/Apply/Publish ids under the ignored `Logs/UiComposerPublish` evidence directory. A state-file sharing violation may retry one idempotent Apply only after the exact package state file becomes exclusively available; the orchestrator never moves, deletes or recreates ownership state.

The web tool never writes project files directly. Authoring Server saves tracked
blueprints and queues temporary `.agent` operations. The FairyGUI helper owns
package writes, package id allocation, save, and publish.

## Current Screens

`MxUiFoundationDemo/UiFoundationDemoPanel` contains a full-screen dark Graph,
centered panel Graph, title Text, status Text, and three instances of
`ui.component.button.generic`:

| Binding slot | Node name | Runtime behavior |
| --- | --- | --- |
| `status` | `status` | Shows the latest sample action |
| `primary` | `btnPrimary` | Increments the primary action counter |
| `secondary` | `btnSecondary` | Toggles the secondary mode |
| `close` | `btnClose` | Closes the panel |

Bindings are defined in C# and returned to the Composer as read-only slots.

`MxCharacterTestUi/MainMenuPanel` is the first player-facing screen. It uses a
1920x1080 centered modal composition, ten formal semantic resources, three
command-button recipes, six controlled icons, and the existing six CharacterTest
commands. The generated binding contract fixes the required node names, paths,
commands, control types, and focus order. Runtime behavior, pause ownership,
Surface Policy, `InputContext.UI`, and cursor scopes remain in the existing
CharacterTest hosts rather than the Composer.

`MxCharacterTestCombatHud/CombatHudPanel` is the second player-facing screen.
It uses a 1920x1080 HUD composition, fourteen formal semantic resources, and a
178-slot generated binding contract while retaining the existing health,
pressure, target lock, ability cooldown, Buff, feedback, and network-latency
view-model logic. It remains a non-modal `Hud` surface and does not acquire
`InputContext.UI` or cursor ownership. Left, right, center, and bottom relations
keep the layout stable across the four Composer preview resolutions.

`MxCharacterTestSettingsUi/SettingsPanel`,
`MxCharacterTestNetworkUi/NetworkPanel`, and
`MxCharacterTestAbilityUi/AbilityBindingPanel` are the next three player-facing
screens. They retain their existing host, view-model, command, Surface Policy,
input-context, cursor, pause, and return-to-menu behavior. Settings and Network
also use controlled Text input nodes; their restrictions and maximum lengths
are materialized into FairyGUI XML. Compact primary, neutral, and danger button
recipes share the semantic command-button NineSlice while preserving each
instance's authored size.

MainMenu, Combat HUD, Settings, Network, AbilityBinding, Runtime AI Lab, and the
player Behavior Deck now each live in a separate Composer-owned package. The
Behavior Deck loads `MxCharacterTestBehaviorDeckUi/BehaviorDeckPanel` and
projects only the authoritative Player Deck Workbench view; all install,
remove, replacement, cost, slot, unlock, and compatibility decisions remain in
the Workbench and Installed Deck core. The Runtime AI Lab workspace loads
`MxCharacterTestRuntimeAiLabUi/RuntimeAiPlannerDevPanel`. Transition and recovery
states load the independently preflighted
`MxCharacterTestRuntimeAiLabRecoveryUi/RuntimeAiLabRecoverySurface`; workspace
resource failure therefore cannot disable the fail-safe controls. The old
Runtime AI Lab launcher is not part of the official manifest: F9 routes through
the shell coordinator and is the single reopen entry. Other unmigrated
CharacterTest surfaces remain in the legacy `MxCharacterTestHud` package.

`MxCharacterTestRuntimeAiLabUi/RuntimeAiPlannerDevPanel` owns the 1920x1080
developer shell with three mutually exclusive workspaces: Setup, Behavior Card,
and Diagnostics. Setup selects a complete behavior group for each experiment
slot but does not expose behavior-group entry editing. The Behavior Card workspace places its logical
timeline across the lower primary editing area with a node library and
Inspector; Diagnostics owns Facts, RuntimeFrame, and Replay tabs and is the only
workspace that shows the chronological five-track frame timeline. Its behavior
cards, developer flow, logical timeline, and formal five-track timeline are
reusable Composite recipes backed by Runtime AI Lab semantic resources. The
runtime manifest, resource profile, binder, and published package all use the
dedicated package id.

## Safety Rules

- Only `ArtSource/UIKits/*` blueprints and explicitly allowlisted Composer-owned
  targets are accepted. Current targets include the published Runtime AI Lab
  and player Behavior Deck packages; the legacy `MxCharacterTestHud` package
  remains read-only to Composer.
- Apply is blocked by stale catalog, recipe, blueprint, mapping, or source XML.
- Sync is blocked by unsupported XML. It cannot delete unknown content.
- Publish is blocked until materialized source hashes match the latest blueprint.
- Operation requests, progress, results, and screenshots under `.agent` or
  `Temp/` are not committed.
- Runtime code does not read Composer catalogs or blueprints.
- Runtime composition registers generated view fragments into one `MxFairyGuiRuntimeShell` per surface set. The shell owns the aggregate runtime catalog, one navigator, pending opens, cached views, and teardown; individual CharacterTest hosts retain only their view-model and command responsibilities.
- `MxFairyGuiNavigator.OpenAsync` keeps the UI operation pending while `MxFairyGuiResourceBridge.LoadPackageAsync` polls package resources. Cancellation releases in-flight/loaded handles, and component creation starts only after the package scope succeeds.

## Validation

Text typography includes optional `text.font`; standard button recipes include
optional `statePresentation.titleFont` and `titleBold` (default true for existing
recipes). Both materializers preserve font names; supported text XML sync reads
`font` back. These are additive properties of the existing schema. Blank names
retain the previous default-font behavior. Specific font assets, aliases and
runtime initialization belong to the consuming project, not Authoring Core.


- Authoring Core tests cover schema, paths, binding slots, hashes, deterministic
  plans, drift, and supported XML round-trip.
- Web smoke covers editing, undo/redo, resolution preview, dirty state, binding
  restrictions, apply/sync planning, and operation progress.
- FairyGUI smoke materializes and publishes the real package without coordinate
  clicking.
- Unity validates generated manifest/binder output, package loading, command
  delivery, close behavior, UI input scope, cursor state, and representative
  resolutions.

Current automated evidence covers all four preview resolutions, deterministic
Authoring and generated-source checks, real package texture loading, the three
Demo button commands, main-menu panel close, `InputContext.UI`, released/captured
cursor scope restoration, and the Combat HUD's dynamic bars, abilities, Buffs,
target lock, feedback, and non-modal package loading.
