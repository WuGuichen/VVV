# MxFramework Editor Hub

Editor Hub starts the local Authoring server and provides a compact launcher for the framework web editors.

The home page contains seven tool entries, recent work, one service connection indicator, and a `新建角色` button. It does not show a global work-context selector, resource inventories, Resource Plan previews, delivery-stage cards, or diagnostic records. The home page only checks `/api/character/packages` for service connectivity; tool target lists are loaded when an entry is opened.

Open an entry to select a character, UI screen, material library, or Buff package. The launcher remembers the last target for each kind and the six most recently opened tool/target pairs in this browser's local storage. Recent entries reopen the same target after checking that it still exists. A legacy `?package=...` link preselects that character in the picker without bringing the old dashboard back. Built-in character templates remain accessible to diagnostics through an explicit package URL, while ordinary tool pickers offer actual characters; create a character from the template through `新建角色`.

`更多` contains `诊断` and `重启服务`. Diagnostics opens `web/diagnostics.html` for a selected character. Character delivery checks and Resource Plan previews are also available in CharacterStudio's character details; resource browsing remains in Resource Manager. Browser history and preferences are personal local state, not versioned project content.

## Launch

From the framework repo:

```bash
Tools/MxFramework.EditorHub/start-editor-hub.sh
```

```bat
Tools\MxFramework.EditorHub\start-editor-hub.bat
```

Defaults:

- port: `4873`
- tools root: current `WGameFramework` repo
- project root: current `WGameFramework` repo
- character package: `Tools/MxFramework.Authoring/samples/character-codex-flow-test`
- URL: `http://127.0.0.1:4873/Tools/MxFramework.EditorHub/web/`

On macOS, the launcher first uses an existing `MX_AUDIO_GENERATION_TOKEN`.
When the environment variable is absent, it reads the token from the current
user's Keychain item named `MxFramework.AudioGenerationToken`. The token is
never written to the repository or printed by the launcher. If neither source
is available, Editor Hub still starts, but Audio Workbench generation jobs are
disabled with an explicit warning.

## Editing Another Unity Project

Pass a separate project root as the third argument, or set `MXFRAMEWORK_EDITOR_HUB_PROJECT_ROOT`.

```bat
Tools\MxFramework.EditorHub\start-editor-hub.bat 4973 Tools\MxFramework.Authoring\samples\character-codex-flow-test E:\Unity\WxFramework\WGame_B_Unity
```

```bash
MXFRAMEWORK_EDITOR_HUB_PROJECT_ROOT=E:/Unity/WxFramework/WGame_B_Unity \
Tools/MxFramework.EditorHub/start-editor-hub.sh 4973 Tools/MxFramework.Authoring/samples/character-codex-flow-test
```

Root meanings:

- `tools root`: framework repository containing `Tools/`, web editors, shared samples, and schemas.
- `project root`: Unity project whose `Assets/`, `Temp/`, generated files, config, and runtime catalogs are edited.

The CLI equivalent is:

```bash
dotnet run --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- editor serve \
  --tools-root E:/Unity/WxFramework/WGameFramework \
  --project-root E:/Unity/WxFramework/WGame_B_Unity \
  --port 4973 \
  --package Tools/MxFramework.Authoring/samples/character-codex-flow-test
```

## Entries

- Buff v2 Workbench
- CharacterStudio
- Animation Editor
- UI Art Resource Library
- UI Composer
- Resource Manager
- Audio Workbench：Gameplay Cue、Event Definition、Unity/FMOD 试听证据与 Runtime Catalog 发布
- Diagnostics (under `更多`): character delivery evidence and aggregated diagnostic records

Editor Hub starts and navigates tools only. AssetBundle builds and player runtime artifact builds still run inside Unity.

## UI Art Resource Library

`UI Art Resource Library` opens `Tools/MxFramework.FairyGuiArtSlices/web/?library=MxUiFoundation` and uses the local Authoring server UI art resource APIs. It is the primary editing surface for the hand-curated `ArtSource/UIKits/MxUiFoundation/ui-art-resources.json` catalog, resource semantics, recommended FairyGUI usage, catalog hash guarded saves, draft-resource planning, and guarded same-path PNG replacement planning.

It does not edit FairyGUI `package.xml`, component XML, atlas files, or `*_fui.bytes`. Layout and publishing still belong to FairyGUI Editor. The existing `MxCharacterTestHud` FairyGUI package scanner remains available for usage audits and drift checks; Unity's `MxFramework/FairyGUI/UI Art Slice Catalog` menu is a thin launcher/debug fallback.

## UI Composer

`UI Composer` lists the current `/api/authoring/ui-composer/screens` catalog and opens `Tools/MxFramework.FairyGuiUiComposer/web/?screen=<selected-screen-id>`. It remembers the selected screen instead of always launching the foundation demo. Screen editing, C# binding validation, save, synchronization, materialization, and publishing remain owned by UI Composer.

The tool's current workflow and authority are documented in `Docs/Guides/FAIRYGUI_UI_COMPOSER_USER_GUIDE.md` and `Docs/FAIRYGUI_UI_COMPOSER.md`. Editor Hub only selects the screen and navigates to the existing editor.

## Buff v2 Workbench

`Buff v2 Workbench` opens `Tools/MxFramework.BuffWorkbench/web/` and uses the local Authoring server Buff APIs. It is the current designer-friendly JSON/CLI management surface for existing Buff v2 packages: edit existing Buff/Aura fields with Chinese labels, field help, safe dropdowns, semantic summaries, `types[]` multi-type classification, `瞬时 / 持续 / 永久` time input, simplified `可叠层 / 最大层数` input, localized name key, fallback display name, badge short name/icon key, and Auto/Manual effect description; refresh diagnostics and authoring diff through Validate; save `config/buff_authoring.json` only from a Ready non-stale draft; compile preview runtime JSON with generated semantic diff; and explicitly import generated runtime JSON from the saved Ready package.

It is not the retired external Buff editor. It does not create, copy, or delete Buff/Aura entries and does not restore WGame `BuffType`, `DamageByAttr`, `applyBuff`, or the old Runtime Preview Server.

## Character Delivery Flow

CharacterStudio retains the role's delivery checks. Editor Hub also offers a read-only `交付检查` panel on its separate diagnostics page, backed by:

```text
GET /api/character/readiness?package=Tools/MxFramework.Authoring/samples/character-codex-flow-test
```

The panel aggregates existing CharacterStudio state, Resource Plan, animation authoring, CharacterAction damage analysis, Unity import reports, Runtime validation, and ability runtime binding visibility. It does not write package files. Stages are compact expandable rows; technical metrics and paths are nested under `查看技术证据`. Unity import follow-up links open CharacterStudio, where the import operation is owned, rather than linking back to the launcher with an execution label. Readiness and receipt requirements are unchanged.

Content-author happy path:

1. Start Editor Hub.
2. Fix role package and resource bindings in CharacterStudio / Resource Manager.
3. Fix animation bindings in Animation Editor.
4. Inspect action timeline damage diagnostics on the diagnostics page and edit the corresponding timeline in CharacterAction Inspector.
5. Run Prefab rebuild preflight in CharacterStudio.
6. Run Unity import.
7. Open CharacterTest or calibration scenes for runtime validation.

`AbilityRuntimeBinding: Deferred` is intentional until the role has explicit `CharacterAbilityId -> IGameplayComponentAbility` catalog mappings.

## Action Damage Analysis

Action damage diagnostics are included on the dedicated diagnostics page through `/api/character-actions/damage-analysis`; there is no disabled Action Damage Analysis tool card on the home page.
It reads `Assets/MxFrameworkGenerated/CharacterActions/Libraries/Common/character_actions.json`.
CharacterAction timeline events own the inline damage fields. Diagnostic search, severity filtering, deduplication, and JSON copying remain available. Repetitive unreferenced-resource and optional-audition records are grouped, with every original affected record preserved under `查看完整诊断` and in the copied JSON. Unreferenced resources are explicitly scoped to the selected character, and optional audition reminders remain informational.

## Combat Design card directory

Five fixed interaction preparations use the same shell, quality grading and layout as action cards. Only the icon aperture receives a fine interrupted inner bevel. The shared `utilityFace.cardText` supplies the concise trigger/effect text. Type/subkind and turn/count labels are omitted from the face; complete rules remain in tooltips and details. The optional `generated.utilityFace` is emitted by the same game description owner and consumed as display data; Web does not parse rule text or derive amounts. Unity and Web reuse the action-card shell and utility effect paintings, with silver, gold and animated holographic qualities read from the same `PreparationQualityStyles.json` configuration. See the [functional face contract](../../Docs/Interfaces/TacticalCard.DescriptionFlow.md#准备卡共用行动卡卡面2026-10-10).

On 2026-10-10 the existing authoring owners consolidated two unreferenced duplicate card identities into `tactical.bound-action`: `bc_90tcbvrg31` and `bc_qkjc3ka7za`. Their execution sources, gates, parameters, slots and costs matched; their two equivalent unreferenced drafts were removed as well. The live directory now has **29 current formal cards, 49 formal version records, 32 author drafts and 8 configured test copies (89 source records)**. Retained exact versions and hashes are unchanged; cards with distinct activation conditions remain independent. See the [content consolidation record](../../Docs/Guides/CHARACTER_TEST_TACTICAL_CARD_AUTHORING.md#正式卡内容去重2026-10-10).

`web/combat-design.html#cards` reads formal published cards, indexed author workspaces and configured test copies. The 2026-10-05 browser receipt contains **49 source records: 20 formal cards/current versions, 23 author drafts and 6 configured test copies**. The default filter displays 43 formal/draft records. Same names remain separate by source identity, revision and content hash; unavailable sources remain diagnostic entries. This directory does not read player saves or grant player ownership.

The directory's additive cardType field is projected through the game's validated published-source parser and actual TacticalCardKind / payload Kind enums. TacticalDeployment.CreateDefinition chooses Utility when its validated InteractionPreparation exists; other supported sources are Action. InteractionPreparation is Utility's subkind (current numeric value 3), not a third top-level card kind. The Web face, detail and type/subkind filters consume this field, independently of source/state/mechanism/component/search filters. Titles, tags and intent-component membership never classify card kind.

Drafts and configured copies are explicitly marked source-preview; their type describes the saved source, not a publication or player ownership. Unavailable/unsupported sources or runtime contracts remain Unknown with diagnostics, even when a draft cites a former publication. Type generation is independent of description generation. An older EditorHub response without cardType displays unknown types and an upgrade notice. New CLI fields require the service owner to upgrade the running service. Refresh, URL restoration and Clear filters retain the existing directory behavior and never save authoring drafts.

Card descriptions and parameters use the game's shared C# projection and localized text provider. Fifteen components have independent intent sources with concise text plus retained detailed rules; the component directory has sixteen because functional Step back has no independent intent source. Unsupported sources and missing bindings keep diagnostics or explicit fallback. Web consumes the generated string/optional display rows; it does not implement another gameplay DSL or infer intent from titles. See [description display contract](../../Docs/Interfaces/TacticalCard.DescriptionFlow.md).

`web/combat-design.html#authoring` follows the existing authoring transaction. Main/Experiment roles come from `Authoring/RuntimeAiLab/Drafts/workspace-roles.json`; Experiment may edit/test but cannot publish. Submit/status and Unity EditMode readiness gates are described in the existing [publish task record](../../Docs/Tasks/COMBAT_DESIGN_WEB_AUTHORING_PUBLISH.md). Shared sources remain read-only here. Publishing or refreshing the catalog does not acquire cards, modify player saves, or replace frozen battle deployments. This documentation sync did not run the browser or Unity checks again.

Fixed interaction preparations intentionally have empty DSL event timelines. The CLI display adapter calls the existing game `GenerateInteractionPreparation` method with the validated payload, and consumes its Text, Details, Warnings and empty display arrays. Effect names, amounts and lifetimes are not inferred or rewritten by Web. `generated.descriptionBasis` identifies this fixed game authority; its existing Chinese source text reports `locale=zh-CN`, including when another locale is requested. This display does not mark a card measured or executable. Unknown rulesets/effects and empty effects fail source validation; empty or punctuation-only authority text is an unavailable projection. Web falls back to useful saved text marked as static, or an explicit missing-description placeholder when both sources are empty.

Type and fixed-description regression checks use the compiled game parser without opening Unity. The project root is read-only; build output and test fixtures belong to the test execution directory or temporary folder:

```powershell
dotnet run --project Tools/MxFramework.NoEngineTests/Tactical.Card.Directory.Tests -- tests <project-root>
```

`scripts/tactical-card-classification-smoke.cjs` validates type/subkind, all existing filters, detail/preview, empty results, refresh/URL restoration and narrow-screen card rendering. Set `PLAYWRIGHT_MODULE`, `CHROMIUM_EXECUTABLE_PATH`, `EDITOR_HUB_URL` and `TACTICAL_CARD_EVIDENCE_DIR` as needed. Its explicit fixture stage verifies unknown, unpublished and legacy-service responses separately from real indexed sources. Candidate mode can provide isolated JS/CSS and a freshly generated candidate C# response using `TACTICAL_CANDIDATE_ROOT`, `TACTICAL_CANDIDATE_ASSEMBLY` and `TACTICAL_PROJECT_ROOT`; a candidate receipt is not verification of an upgraded shared service. The test blocks all mutating requests and never uses an existing browser profile.

## Combat Design component catalog

`web/combat-design.html#intent` lists reusable tactical **components**, not input IntentIds or complete tactical templates. `GET /api/combat-design/catalog` and `authoring combat-design catalog --project-root <root>` regenerate the view on every call. The legacy `intent` API/CLI entry is an alias of the complete catalog.

`CombatDesignComponentCatalog.cs` contains the finite source decoders. Each decoder reads actual factory, description-contract, functional-slot or runtime members and checks its required tokens. It does not invoke Unity, execute factories, save authoring data, or maintain a second set of game parameters. Missing or changed contracts become `needs-review`; unrelated prose/document hashes cannot hide components. New component shapes require an explicit decoder and a source-drift regression check. The registry's keys identify Web navigation only.

Current coverage includes approach, keep distance, move away, retreat to distance, orbit, facing, facing action, forward step, functional Step back, wait condition r2, ordinary guard, two-second wait, target-distance wait, selected-point movement, and both behind-target modes. The target-distance factory's fixed/reference forms remain one component; the two behind-target completion modes remain separate. Main-tree implementation is distinct from isolated implementation, a candidate, a saved draft instance, a published card and player ownership. This query reads the current project only; it does not discover or promote isolated checkouts from old reports.

The existing v4 workspace scanner remains the authority for saved shared sources, card expressions, values and Reference edges. Standard event blocks link back to components by structure. Names and whole template IDs do not create components. Unsupported workspace schemas remain visible in diagnostics. Template expressions and unparsed source audit retain separate tabs. Source links open `combat-design-source.html` with the actual file, member line and SHA256; the read-only source API only permits files in the current catalog read set.

Focused checks (installed .NET 9 SDK, no Unity):

```powershell
dotnet run --project Tools/MxFramework.NoEngineTests/Combat.Design.Catalog.Tests -- <project-root>
```

Browser verification is in `scripts/combat-design-smoke.mjs`; set `PLAYWRIGHT_MODULE` to an installed Playwright module and `CHROMIUM_EXECUTABLE_PATH` if needed. It uses GET requests and browser-local navigation only, and writes evidence to `COMBAT_DESIGN_EVIDENCE_DIR`.
