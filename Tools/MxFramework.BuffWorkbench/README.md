# MxFramework Buff v2 Workbench

Local vanilla Web workbench for existing Buff v2 authoring packages. This is the current designer-friendly safe editing surface for Buff v2; it is not a separate designer mode.

## Launch

Start Editor Hub / Authoring server from the framework repo:

```bash
Tools/MxFramework.EditorHub/start-editor-hub.sh
```

Open:

```text
http://127.0.0.1:4873/Tools/MxFramework.BuffWorkbench/web/
```

## Scope

- Loads existing `config/buff_authoring.json` packages.
- Edits existing Buff and Aura values through guarded forms.
- Shows designer-facing Chinese labels, field help, safe dropdowns, semantic list summaries, `瞬时 / 持续 / 永久` time editing, and simplified `可叠层 / 最大层数` editing while saving existing JSON fields.
- Treats `buffs[].types` as first-class player-facing multi-type classification. Types are source authoring data and do not encode source/施加方式.
- Treats badge presentation metadata as first-class editable data: localized display-name key (`presentation.nameKey`), fallback/developer `displayName`, short badge name, icon resource key, visibility, and priority. Generated presentation `polarity` is inferred from `types[]` for runtime HUD compatibility.
- Generates an automatic effect description preview from current effect, time, and stacking config. `descriptionMode=Auto` keeps the generated text out of source authoring JSON; `descriptionMode=Manual` uses `presentation.description` as a handwritten override.
- Warns when `nameKey` or `iconResourceKey` is missing. Empty icon keys are warnings only and do not block Save or Import.
- Keeps identity and structure fields read-only and folded under technical details by default: `buffId`, `auraId`, `effectId`, `stableId`, and `effect.kind`.
- Uses Authoring server options metadata for Buff types, known CharacterTest attributes, stacking modes, description modes, compatibility stack policies, Aura relation, Aura layer presets, `durationFrameRate=60`, and `instantFrames=2`.
- Marks validation and diff stale after each field edit.
- Refreshes field diagnostics and authoring diff through Authoring API validation before Save.
- Supports diff row field focus, single-field revert, and discard-all back to the last load/save baseline.
- Compiles drafts without writing generated runtime JSON and shows generated semantic diff against current generated files.
- Imports only saved Ready packages into generated runtime JSON.

This is a Buff v2 JSON / CLI management surface. It edits only existing fields and does not create, copy, or delete Buff/Aura/effect structure. It does not restore the retired external Buff editor, WGame `BuffType`, `DamageByAttr`, `applyBuff`, or Runtime Preview Server protocol.

The Workbench does not write Localization TSV files. It stores `presentation.nameKey` and keeps `displayName` as fallback text; project localization data is maintained by the existing Localization pipeline.
