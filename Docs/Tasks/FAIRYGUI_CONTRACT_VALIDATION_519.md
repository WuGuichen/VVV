# FairyGUI Contract Validation (#519)

## Goal

Make framework-owned FairyGUI views machine-readable and locally validatable
without opening FairyGUI Editor.

This task builds on the `MxRuntimeHud` vertical slice from #518. The manifest
and validator are tooling contracts: they describe the FairyGUI package,
component, controls, commands, ViewModel, and resources that a view expects.
They do not replace the FairyGUI source package and do not contain gameplay
logic.

## Scope

- Define an adapter-scoped FairyGUI view manifest shape.
- Validate source package XML, component XML, package bytes and resource catalog
  entries against the manifest.
- Produce structured diagnostics with stable codes.
- Keep `MxFramework.UI` core free of FairyGUI, UnityEngine and UnityEditor.
- Document the local validation command and agent-safe editing boundaries.

## Manifest Shape

The checked-in or generated manifest should include:

- schema version
- package id and source package path
- package bytes `ResourceKey` and published bytes path
- one or more view declarations
- component name, source component path, layer and ViewModel type
- required controls with name, kind, optional bind path and optional command id
- required package resources with kind, `ResourceKey`, published path and
  required flag

For `MxRuntimeHud`, the first view contract is:

```text
viewId: ui.runtimehud.main
packageId: MxRuntimeHud
component: RuntimeHudPanel
viewModel: MxFramework.Demo.RuntimeAbilitySliceHudViewModel
packageBytes: ui.fairygui.runtimehud.fui / MxFairyGuiPackageBytes
controls:
  title          Text
  mode           Text
  playerName     Text
  playerHp       Text
  enemyName      Text
  enemyHp        Text
  recentAction   Text
  btnStrike      Button  command runtimeHud.strike
  btnReset       Button  command runtimeHud.reset
```

## Diagnostics

Diagnostics must be structured, not just console text. Required fields:

- stable code
- severity
- view id
- target path or field
- message
- suggested fix

Minimum diagnostic coverage:

- invalid manifest schema
- invalid or missing view id
- package id/name mismatch
- missing package source XML
- missing exported component declaration
- missing component source XML
- missing required control
- control kind mismatch
- missing package bytes file
- invalid package bytes header
- missing package bytes catalog entry
- missing required resource catalog entry
- unsupported FairyGUI resource type
- command declared in contract but not bound to a button control
- duplicate view id
- generated descriptor mismatch

## Agent Workflow

Agents may edit:

- manifest source definitions
- generator or validator code
- handwritten binders and composition code
- tests
- documentation

Agents must not hand-edit:

- `*_fui.bytes`
- atlas textures
- generated `FUI_*` files
- generated descriptor files unless the task explicitly allows a generator
  bootstrap commit

If a FairyGUI package changes, agents should validate source XML and manifest
first. Package bytes should be regenerated through FairyGUI Editor or the
project helper plugin, then validated by header/hash/resource checks.

## Validation

Local validation should not require headless FairyGUI Editor. The expected gate
for this slice is:

```bash
xmllint --noout FGUIProject/assets/MxRuntimeHud/package.xml FGUIProject/assets/MxRuntimeHud/RuntimeHudPanel.xml FGUIProject/assets/MxRuntimeHud/Components/RuntimeHudButton.xml
dotnet build MxFramework.UI.FairyGUI.csproj /nr:false -m:1 -v:minimal
dotnet build MxFramework.Tests.UI.FairyGUI.csproj /nr:false -m:1 -v:minimal
```

Unity Editor refresh may be needed after adding new asmdef files so generated
`.csproj` files include new sources.

## Non-Goals

- Do not build a visual editor replacement.
- Do not migrate Story, Debug UI or all Runtime HUD UI.
- Do not introduce a global UI manager.
- Do not make headless FairyGUI Editor export mandatory.
