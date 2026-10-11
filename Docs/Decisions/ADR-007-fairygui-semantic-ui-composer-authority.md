# ADR-007: FairyGUI Semantic UI Composer Authority

> Status: ADR

Date: 2026-07-10 | Amended: 2026-07-16

## Context

The independent UI Art Resource Library can describe PNG identity, intended use,
NineSlice parameters, and replacement safety. It does not describe reusable UI
components, screen layout, or runtime bindings. FairyGUI source XML already owns
the actual player-facing layout, while C# generation specs own framework command
and binder contracts.

Adding a visual composer introduces a second editable representation. Without an
explicit authority rule, external edits and FairyGUI edits could silently
overwrite each other or move binding semantics into unreviewed JSON.

## Decision

- FairyGUI `package.xml` and component XML remain the final resource and layout
  facts. Published bytes and atlases remain FairyGUI Editor output.
- UI Composer screen blueprints are Development Agent-readable editable
  projections. They are drafts until an explicit apply operation materializes
  them through the FairyGUI project helper.
- Package-local `ui-composer-state.json` owns only tool ownership, stable semantic
  node-to-FairyGUI id mapping, and synchronization hashes. It never owns layout.
- Apply requires current resource catalog, component recipe, blueprint, mapping,
  and component XML hashes. Drift blocks writes instead of choosing a winner.
- Supported FairyGUI layout changes can be explicitly synchronized back into a
  blueprint. Unknown nodes, properties, gears, controllers, or transitions block
  synchronization; they are never discarded silently.
- The writer is restricted to an explicit Composer-owned allowlist. The ten
  published packages are `MxUiFoundationDemo`, `MxCharacterTestUi`,
  `MxCharacterTestCombatHud`, `MxCharacterTestSettingsUi`,
  `MxCharacterTestNetworkUi`, `MxCharacterTestAbilityUi`,
  `MxCharacterTestRuntimeAiLabUi`, `MxCharacterTestRuntimeAiLabRecoveryUi`,
  `MxCharacterTestBehaviorDeckUi`, and `MxPlayerGuidanceUi`. Legacy packages
  such as `MxCharacterTestHud` remain read-only to the Composer.
- Migration does not grant the Composer permission to patch a legacy package.
  It creates a separately owned package, preserves the runtime binding contract,
  and switches runtime ids only after Apply/Publish, resource registration,
  generated manifest/binder, and Unity validation are complete.
- FairyGUI package, image, and component ids are allocated and persisted by the
  FairyGUI project helper. The Authoring Server only writes blueprints and
  temporary operation requests; it does not write `package.xml`.
- C# FairyGUI manifest/binder generation specs remain the binding authority.
  Blueprints may assign nodes to declared binding slots, but cannot declare
  arbitrary command ids, C# expressions, or generated source.
- The UI Art Resource Library remains the PNG semantic authority. Component
  recipes and screen blueprints reference stable `resourceId` values and never
  copy FairyGUI resource ids into the library catalog.
- Standard Button recipes are editable authoring data. Controlled Composite
  recipes are catalog-owned, read-only in the web form, recursively validated,
  and materialized deterministically; arbitrary controller, transition, script,
  or free component-tree authoring remains outside the contract.

## Consequences

- Human FairyGUI editing and external visual editing can coexist through explicit
  hash-guarded synchronization.
- A screen can be reviewed by Development Agents without making blueprint JSON a
  runtime contract.
- Legacy FairyGUI packages remain protected; a package becomes Composer-owned
  only through an explicit allowlist and migration with a reviewed binding
  contract.
- Advanced FairyGUI features stay Editor-owned until the round-trip parser has an
  explicit contract for them.

## References

- `Docs/Decisions/ADR-006-fairygui-binder-generation.md`
- `Docs/FAIRYGUI_UI_COMPOSER.md`
- `Docs/Guides/FAIRYGUI_UI_COMPOSER_USER_GUIDE.md`
- `Docs/Guides/UI_ART_RESOURCE_LIBRARY_GUIDE.md`
