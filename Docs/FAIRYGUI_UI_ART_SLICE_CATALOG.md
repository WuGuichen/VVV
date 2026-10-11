# FairyGUI UI Art Slice Catalog

> Status: Guide
>
> Purpose: 当前 FairyGUI 切图语义 sidecar catalog 的工具链说明，用于给 Development Agent 可读的切图语义、校验、report 和受控替换入口提供事实源。本文同时保留 Phase 4 的 planned 边界；未标注为 Current 的 XML writer 和自动发布流程不代表已实现能力。

## Current Implementation Status

Phase 3.0 FairyGUI integration handoff planning is Current for the independent `MxUiFoundation` UI art resource library:
- The primary editing experience is now `Tools/MxFramework.FairyGuiArtSlices/web/?library=MxUiFoundation`, opened through EditorHub or the Unity fallback button.
- The main fact source is `ArtSource/UIKits/MxUiFoundation/ui-art-resources.json`, not an existing FairyGUI package scanner result.
- The library started with the hand-curated test resource `ui.button.generic.base`; it now contains 40 semantic resources covering the generic test button, formal player UI foundations, Combat HUD, and Runtime AI Lab authoring assets.
- Authoring Core owns resource-library catalog loading, catalog hash checks, PNG metadata/replacement validation, recommended FairyGUI usage validation, and operation planning.
- Authoring Server exposes `/api/authoring/ui-art/resources/capabilities`, `/libraries`, `/state`, `/save`, `/plan-replace`, `/replace-png`, `/plan-create`, `/create`, `/plan-draft`, `/fairygui-packages`, and `/plan-fairygui-import`.
- All writes go through dry-run plans and catalog hash checks. Semantic save validates the complete proposed catalog and cannot change resource ids, paths, or inventory; `replace-png` only replaces an existing PNG at the same resource-library path after plan validation; `create` writes one new PNG under the resource library and appends one catalog resource.
- Catalog and resource roots are restricted to repository-relative paths under `ArtSource/UIKits`; unsafe or escaping library roots are not discoverable or writable.
- The web editor uses a dark Simplified Chinese, fixed-height three-column workbench. Resource browsing, PNG preview, and the tabbed semantic inspector remain visible together; each column scrolls independently instead of extending one long page.
- New-resource and same-path PNG replacement flows use focused right-side drawers. New-resource authoring is file-first so PNG metadata can seed dimensions and defaults before semantic fields are completed.
- NineSlice resources show draggable boundaries over the source preview, synchronized with the numeric `scale9Grid` fields. Preflight and diagnostics remain available as collapsed detail surfaces without occupying the main editing viewport.
- The FairyGUI inspector can select any package backed by `FGUIProject/assets/<Package>/package.xml`, generate a read-only integration plan, and copy or download deterministic JSON/Markdown recipes for a Development Agent or FairyGUI author.
- Integration planning reports suggested package folder/name, existing FairyGUI resource id, content-hash match, import state, sidecar usage alignment/drift, and publish requirements. It never assigns a FairyGUI resource id itself; that id remains FairyGUI Editor-owned.
- The page checks the Authoring Server capability version before loading state and reports a stale/missing server explicitly instead of presenting a generic API failure.
- The Authoring static preview route serves project-root `ArtSource/` and `FGUIProject/` PNGs read-only while keeping EditorHub tools under the configured `Tools` root.
- The Unity Editor menu remains as a thin entry/debug fallback. It opens the external resource-library page and can still generate the old `MxCharacterTestHud` audit report.
- The UI Art Resource Library itself still does not write FairyGUI `package.xml`, component XML, atlas files, or `*_fui.bytes`. The separate, hash-guarded UI Composer Authoring API v5 may materialize and publish only explicitly allowlisted Composer-owned packages. Six packages are currently published (`MxUiFoundationDemo`, `MxCharacterTestUi`, `MxCharacterTestCombatHud`, `MxCharacterTestSettingsUi`, `MxCharacterTestNetworkUi`, and `MxCharacterTestAbilityUi`); `MxCharacterTestRuntimeAiLabUi` remains authoring-only. See `Docs/FAIRYGUI_UI_COMPOSER.md` and ADR-007.

The existing `MxCharacterTestHud` FairyGUI package scanner remains available as a usage audit / drift-check path:
- `/api/authoring/fairygui/art-slices/*` endpoints still read the package-local sidecar catalog and scanner-derived usage points.
- `--check-art-slices`, `--write-art-slice-report <path>`, and `--review-report [path]` continue to validate the existing FairyGUI package catalog.
- It is no longer the default editing source for new UI cut-up resources.

## Current Resource Library Shape

The hand-curated library catalog is intentionally independent from FairyGUI package ids:

```json
{
  "schemaVersion": "mx.uiArtResourceLibrary.v1",
  "libraryId": "MxUiFoundation",
  "resourceRoot": "ArtSource/UIKits/MxUiFoundation",
  "pathStyle": "library-relative-posix",
  "designResolution": {
    "width": 1920,
    "height": 1080
  },
  "resources": []
}
```

Each resource owns semantics and replacement constraints:

```json
{
  "resourceId": "ui.button.generic.base",
  "relativePath": "images/buttons/button_generic_240x64.png",
  "category": "ButtonBase",
  "displayName": "通用按钮底图",
  "description": "Test seed resource, not final art.",
  "tags": ["button", "generic", "test"],
  "recommendedFairyGuiUsage": {
    "policy": "NineSlice",
    "designSize": {
      "width": 240,
      "height": 64
    },
    "scale9Grid": {
      "x": 24,
      "y": 18,
      "width": 192,
      "height": 28
    },
    "preserveAspect": false
  },
  "replacement": {
    "requireAlphaChannel": true,
    "requireNonOpaqueAlpha": true,
    "requireSamePixelSize": true,
    "allowStretch": true,
    "minPixelWidth": 240,
    "minPixelHeight": 64,
    "replaceTogether": false
  }
}
```

Authority boundary:

| Source | Authority |
| --- | --- |
| `ui-art-resources.json` | Resource semantics, relative image path, recommended FairyGUI usage, and replacement constraints. |
| PNG under `resourceRoot` | Actual source pixels for the hand-curated library item. |
| FairyGUI package scanner | Optional usage audit after a resource has been imported into a FairyGUI package. |
| FairyGUI Editor | Layout, component XML, `package.xml`, atlas generation, and `*_fui.bytes` publishing. |

`resourceId`, `relativePath`, category enum values, paths, hashes, and diagnostic codes are kept stable and tool-readable. Display names and descriptions may use Simplified Chinese.

Phase 1 已在 noEngine FairyGUI manifest test/tooling 路径落地：

- `FGUIProject/assets/MxCharacterTestHud/ui-art-slices.json` 是首个样板 catalog。
- scanner 只读 `MxCharacterTestHud/package.xml`、当前 package component XML 和 sidecar catalog。
- `--check-art-slices` 校验 catalog 覆盖率、resource id / source path 漂移、缺源 PNG、推荐使用参数、按钮状态组尺寸等规则。
- `--write-art-slice-report <path>` 输出稳定 Markdown / JSON report。
- `--review-report [path]` 在 FairyGUI manifest review report 中追加 art-slice summary。

Phase 2 最小 Editor 入口已在 `MxFramework/FairyGUI/UI Art Slice Catalog` 落地：

- Unity Editor 窗口可浏览 `MxCharacterTestHud` slice、预览源 PNG、查看直接 `<image src>` 使用点、编辑 catalog 语义和替换约束。
- `MxFairyGuiArtSliceCatalogEditorService` 集中负责低频 catalog load/save、package/component XML usage 扫描、report 命令调用、同路径 PNG 替换校验和原子替换，窗口只保留 IMGUI 交互和 preview cache。
- `MxFairyGuiArtSliceCatalogEditorOperationPlanner` 在保存 catalog、替换现有 PNG、登记 draft slice 前生成 dry-run plan，列出会修改的文件类别、阻断 diagnostics 和是否需要 FairyGUI 重新发布。
- 上述 service/planner 及其只读 document/plan/result 类型是正式 public package Editor contract；project-only `MxFramework.Project.FairyGUI.Editor` 只通过该 contract 调用，不再依赖 `InternalsVisibleTo`。构造器、诊断工厂和 package 内路径辅助仍为 internal，避免扩大任意写入面。
- 窗口可调用 noEngine report 命令生成 `Temp/fairygui-art-slices.editor-report.md`。
- 同路径 PNG 替换会校验 package root 路径、旧/新 PNG pixel size、alpha channel、non-opaque alpha 和 catalog replacement 约束，并提示重新通过 FairyGUI Editor 发布。
- 替换流程不写 `package.xml`、component XML、atlas 或 `*_fui.bytes`。

仍未实现：`package.xml` writer、component XML 小修正、跨 package 依赖扫描、runtime dynamic usage 扫描、NineSlice 自动替换和 FairyGUI 发布产物生成。

## Background

FairyGUI 源工程现在已经能通过 `package.xml`、组件 XML、项目插件发布产物和 noEngine manifest / binder generator 支撑 CharacterTest player-facing UI。缺口在于：图片切图本身只有资源 id、文件路径和使用参数，没有稳定表达“这张图是什么、应如何使用、能否替换、替换时要一起替换哪些状态图”。

本方案新增 package-local sidecar catalog，让切图资源拥有可审查的语义契约，但不替代 FairyGUI Editor，不改变运行时加载路径，也不生成 FairyGUI 发布产物。

首个样板包为：

```text
FGUIProject/assets/MxCharacterTestHud
```

推荐 sidecar 文件为：

```text
FGUIProject/assets/MxCharacterTestHud/ui-art-slices.json
```

## Authority Model

Phase 1 必须保持以下权威边界：

| Source | Authority |
| --- | --- |
| `package.xml` | image resource id、实际文件名、实际 package-relative 路径。 |
| component XML | 实际使用点，例如直接 `<image src>`、节点 id、控件名、位置、尺寸、`aspect`、relation、gear display。 |
| `ui-art-slices.json` | 切图语义、推荐使用、替换约束、分组关系、忽略理由和已知外部使用声明。 |
| generated usage index / report | scanner 派生结果；不手写，不作为事实源。 |

Catalog 只能用 `fguiResourceId` 连接 FairyGUI 资源。实际资源路径必须从 `package.xml` 解析；Catalog 中的 `expectedSourcePath` 只是防漂移校验项，不是加载或解析依据。

当 `package.xml` 与 `expectedSourcePath` 冲突时，`package.xml` 作为资源事实源获胜，validator 报告漂移 diagnostic。

## Catalog Shape

顶层结构：

```json
{
  "schemaVersion": "mx.uiArtSliceCatalog.v1",
  "packageId": "MxCharacterTestHud",
  "packageSourcePath": "FGUIProject/assets/MxCharacterTestHud",
  "pathStyle": "package-relative-posix",
  "designResolution": {
    "width": 1920,
    "height": 1080
  },
  "strictCoverage": true,
  "groups": [],
  "slices": []
}
```

`pathStyle=package-relative-posix` 表示路径相对 `packageSourcePath`，并统一使用 `/`。Catalog、report 和 diagnostics 不输出绝对路径。

`strictCoverage=true` 表示该 package 的所有 `package.xml` image resource 都必须被 `slices` 覆盖，或显式声明为 `Ignored`。

## Slice Model

普通 slice：

```json
{
  "sliceId": "main_menu_button_normal",
  "fguiResourceId": "cthud1n",
  "expectedSourcePath": "Images/menu/menu_button_up_420x64.png",

  "category": "ButtonState",
  "displayName": "Main Menu Button Normal",
  "description": "主菜单按钮默认状态底图。",
  "tags": ["main-menu", "button"],

  "recommendedUsage": {
    "policy": "Stretch",
    "logicalSize": {
      "width": 420,
      "height": 64
    },
    "preserveAspect": false,
    "relationHint": "width-driven"
  },

  "replacement": {
    "requireAlphaChannel": true,
    "requireNonOpaqueAlpha": true,
    "requireSamePixelSize": true,
    "allowStretch": true,
    "minPixelWidth": 420,
    "minPixelHeight": 64,
    "replaceTogether": true
  },

  "groupId": "main_menu_button",
  "roleInGroup": "normal"
}
```

Ignored slice 必须写明原因：

```json
{
  "sliceId": "debug_unused_placeholder",
  "fguiResourceId": "cthud99",
  "category": "Ignored",
  "ignoreReason": "Temporary debug placeholder kept for FairyGUI Editor experiments."
}
```

Catalog 描述 intended / recommended 语义；scanner 输出 observed 使用事实；validator 比较两者是否冲突。

## Categories And Policies

v1 categories：

```text
PanelFrame
ButtonState
Icon
BarTrack
BarFill
Divider
Badge
InputBackground
Decoration
Ignored
```

v1 usage policies：

```text
Fixed
PreserveAspect
Stretch
HorizontalFill
VerticalFill
NineSlice
Tile
```

`usagePolicy=NineSlice` 时 `scale9Grid` 不允许为 null，并必须满足：

```text
x >= 0
y >= 0
width > 0
height > 0
x + width <= sourcePixelWidth
y + height <= sourcePixelHeight
```

Phase 1 只读取、校验和报告 NineSlice。Editor 不自动替换 NineSlice 资源。

## Groups

按钮状态、进度条组合和其他成组替换资源必须使用顶层 `groups`，不能只依赖自由字符串 `groupId`。

```json
{
  "groupId": "main_menu_button",
  "kind": "ButtonStateSet",
  "expectedRoles": ["normal", "hover", "pressed"],
  "requireSamePixelSize": true,
  "replaceTogether": true
}
```

每个属于组的 slice 用 `groupId` 和 `roleInGroup` 连接。`ButtonStateGroupIncomplete` 只能基于 `expectedRoles` 判断，不做视觉内容推断。

## Scanner Scope

Phase 1 scanner 必做：

- 读取 package-local `ui-art-slices.json`。
- 校验 JSON schema。
- 解析当前 package `package.xml` 中的 image resource inventory。
- 扫描当前 package component XML 中直接 `<image src>` 使用点。
- 生成 machine-readable JSON report。
- 生成 human-readable Markdown report。

Phase 1 不阻断但必须记录限制：

- loader / image loader 类节点。
- icon 字段。
- controller / gear / transition 中无法稳定归因的引用。
- 跨 package 引用。
- 运行时代码通过 package item id 或 URL 动态加载的资源。

可用 `knownExternalUsages` 声明动态或外部使用，避免 orphan 误报：

```json
{
  "knownExternalUsages": [
    {
      "kind": "RuntimeLoader",
      "owner": "CharacterTest HUD presentation",
      "note": "Loaded by package item id for dynamic status icon."
    }
  ]
}
```

## Usage Index Shape

scanner 输出结构化使用点，不使用 `"120,80"` 这类字符串坐标：

```json
{
  "componentName": "ExamplePanel",
  "componentPath": "FGUIProject/assets/ExamplePackage/ExamplePanel.xml",
  "controlName": "background",
  "nodeId": "n0_ctmenu",
  "resourceId": "cthud1l",
  "resourcePath": "Images/menu/menu_panel_680x620.png",
  "position": {
    "x": 620,
    "y": 230
  },
  "size": {
    "width": 680,
    "height": 620
  },
  "preserveAspect": false,
  "relations": [
    {
      "type": "width"
    }
  ],
  "gearDisplay": {
    "controller": "button",
    "pages": ["0"]
  }
}
```

## Diagnostics

每条 diagnostic 至少包含：

```json
{
  "code": "UIART001",
  "name": "PackageImageUncataloged",
  "severity": "error",
  "packageId": "MxCharacterTestHud",
  "sliceId": null,
  "resourceId": "cthud1n",
  "componentPath": null,
  "nodeId": null,
  "message": "Package image resource is not covered by catalog.",
  "location": {
    "file": "FGUIProject/assets/MxCharacterTestHud/package.xml",
    "resourceId": "cthud1n"
  }
}
```

建议编号：

```text
error:
UIART001 PackageImageUncataloged
UIART002 CatalogExpectedSourcePathMismatch
UIART003 CatalogSourceMissing
UIART004 FguiResourceIdMissing
UIART005 DuplicateSliceId
UIART006 DuplicateFguiResourceId
UIART007 InvalidUsagePolicyForSlice
UIART008 NineSliceGridInvalid
UIART009 ReplacementSizeMismatch

warning:
UIART101 OrphanImageResource
UIART102 UsageAspectMismatch
UIART103 ButtonStateGroupIncomplete
UIART104 ButtonStateGroupSizeMismatch
UIART105 PanelFrameLargeStretchRisk
UIART106 IconUsedAsLargeBackground
UIART107 KnownScannerLimitation

info:
UIART201 ReplaceAsGroupHint
UIART202 CoverageSummary
UIART203 ExternalUsageDeclared
```

Heuristic 规则默认 warning 或 info，不应在早期阻塞 CI。

## Report Stability

report 输出：

```text
ui-art-slices.report.json
ui-art-slices.report.md
```

稳定性约束：

- 不包含时间戳。
- 不包含机器名、Unity 版本、随机 GUID 或扫描耗时。
- 不输出绝对路径。
- 路径统一使用 `/`。
- JSON 字段顺序固定。
- diagnostics 按 `code -> packageId -> resourceId -> componentPath -> nodeId` 稳定排序。
- generated usage index 与手写 catalog 分开存放。

## Phase Boundaries

Phase 3.0: FairyGUI Integration Handoff Plan (Current).
- The external web tool is the primary editing surface for the independent `MxUiFoundation` resource library.
- Resource-library state is loaded from `ArtSource/UIKits/MxUiFoundation/ui-art-resources.json`, not from a FairyGUI package scanner.
- Save, PNG replacement, and new-resource creation all require a dry-run plan plus a current catalog hash.
- Semantic save validates the proposed catalog before writing and only permits semantic/recommended-usage/replacement changes to existing resources.
- All resource-library catalog and PNG roots stay under `ArtSource/UIKits`.
- Project-root static preview is read-only and limited to source PNG paths such as `ArtSource/` and legacy audit previews under `FGUIProject/`.
- New resource creation writes one PNG under the resource library and appends one catalog record; it does not import the image into a FairyGUI package.
- The fixed three-column workbench, tabbed inspector, metadata copy actions, file-first create drawer, replacement drawer, compact summary, collapsible diagnostics, and draggable NineSlice overlay are presentation-only authoring improvements; they do not expand the write boundary.
- Target package discovery reads package.xml-backed directories under `FGUIProject/assets` without requiring an art-slice sidecar.
- The integration plan compares the library source PNG with package image declarations/files and, when available, compares recommended usage with the package-local sidecar.
- JSON and Markdown recipes are stable, contain repository-relative paths only, and can be copied/downloaded from the external editor.
- Integration plans set all `wouldModify*` fields to false. Import, component wiring, resource-id allocation, and publish remain manual FairyGUI Editor operations.

Legacy Phase 2.5: External EditorHub FairyGUI package audit tool (Current only as audit/fallback for `MxCharacterTestHud`).
- Authoring Core exposes the reusable noEngine state/report/planner/replacement validation service.
- Authoring Server is the only write bridge for the external web tool; browser code must not write repository files directly.
- EditorHub hosts the primary slice list, filters, PNG preview, usage list, diagnostics/report view, catalog semantic editor, group/replacement fields, PNG upload plan preview, and draft-slice plan preview.
- Save requires a matching `catalogHash`; stale clients must reload before writing.
- Replacement requires a dry-run plan and is restricted to same-path existing PNG replacement under the FairyGUI package root.
- Unity IMGUI remains a debug fallback and external-editor launcher, not the target for further complex editing UX.

Legacy Phase 1: FairyGUI package scanner + schema + report（Current only as audit/fallback）。

- 只读 FairyGUI XML 和 sidecar catalog。
- 不写 `package.xml`。
- 不写 component XML。
- 不替换 PNG。
- 不生成 FairyGUI 发布产物。

Legacy Phase 2: Unity Editor package-audit browser and low-risk replacement fallback（Current only for `MxCharacterTestHud` debug/fallback）。

- 可以浏览 package、slice、preview 和直接 XML usage，并可调用 report 命令查看 diagnostics。
- 可以编辑现有 catalog slice 的语义和 replacement 约束。
- load/save、usage 扫描、report 调用和 replacement 校验必须集中在 Editor service，避免后续 UI 编辑器重复 XML/PNG 解析逻辑。
- save / replace / draft registration 必须先生成 dry-run operation plan；plan 只能报告目标、阻断项和发布提示，不能直接执行写入。
- 仍不写 `package.xml`。
- 只允许同路径低风险 PNG 替换。
- 替换后必须提示通过 FairyGUI Editor 重新发布。

Phase 3: Manifest / Development Agent integration（Current for Phase 1 summary wiring）。

- 在 FairyGUI manifest review report 中追加 art-slice summary。
- 增加 `--check-art-slices` 入口。
- 不把切图语义塞进运行时 binder。

Phase 4: Controlled XML writer（Planned）。

- 才允许考虑新增 `package.xml` image declaration。
- 才允许考虑小范围修正 `aspect` 或 relation。
- 不自动重排布局，不批量改组件结构。

## Editor Replacement Safety

Phase 2 替换流程必须满足：

- 替换目标路径限制在 package root 内，禁止 `../` 路径逃逸。
- 替换前读取旧 PNG 和新 PNG 的 pixel size、alpha channel、non-opaque alpha。
- 替换前显示 old/new 摘要。
- 替换操作应使用临时文件后原子替换。
- 替换后不自动改 component XML。
- 替换后提示重新通过 FairyGUI Editor 发布。
- NineSlice 和 ButtonStateSet 可在 v1 只提示风险，不自动替换。

## Known Limitations In V1

- 只保证当前 package 内静态 XML 的直接 `<image src>` 使用点扫描。
- 不保证识别所有 runtime loader 使用。
- 不跨 package 构建完整依赖图。
- 不根据视觉内容自动判断图片语义。
- 不自动修复 FairyGUI 布局。
- ButtonState 语义需要 catalog 显式声明 group 和 role，不做 100% 自动推断。

## Phase 1 Acceptance Criteria

- 对 `MxCharacterTestHud` 连续运行两次，report byte-by-byte 一致。
- 所有 `package.xml` image resource 均被 catalog 覆盖或标记 `Ignored`。
- 正常样板包有 0 个 error diagnostic。
- warning 数量可接受，并有明确 owner 或 known limitation。
- 删除任意一个 source PNG 时稳定报出 `UIART003`。
- 删除任意一个 catalog entry 时稳定报出 `UIART001`。
- 修改 `package.xml` 中 resource id 后稳定报出 `UIART004`。
- preserveAspect mismatch fixture 能稳定报出。
- group size mismatch fixture 能稳定报出。

## Phase 2 Acceptance Criteria

- Unity Editor 菜单 `MxFramework/FairyGUI/UI Art Slice Catalog` 可打开窗口。
- 窗口加载 `MxCharacterTestHud` catalog、`package.xml` 和当前 package component XML 后能稳定列出 slice、source PNG preview 和直接 `<image src>` 使用点。
- Editor service 可以被后续 UI 编辑器窗口复用，窗口层不得再复制 package XML scanner、PNG replacement validation 或 report command runner。
- Editor operation planner 覆盖 `SaveCatalogSemantics`、`ReplaceExistingPng` 和 `RegisterDraftSlice`，并能在不写文件的情况下报告 catalog / PNG / FairyGUI XML / 发布产物影响范围。
- `Generate Report` 只调用 noEngine report 命令，不修改 FairyGUI 源 XML、PNG、发布产物或 catalog。
- `Save Catalog Edits` 只写 `ui-art-slices.json` 中的语义和 replacement 约束字段。
- `Replace PNG...` 只允许替换 package root 内已有 source PNG，并在 replacement 约束不满足时阻止替换。
- 替换成功后仍要求人工通过 FairyGUI Editor/helper plugin 重新发布 package。
- NineSlice、跨 package usage、runtime dynamic usage 和自动 `package.xml` 写入仍不作为 Phase 2 当前能力。

## Validation Commands

Phase 3.0 resource-library acceptance criteria:
- EditorHub shows a UI Art Resources card and opens `Tools/MxFramework.FairyGuiArtSlices/web/?library=MxUiFoundation`.
- The web tool can load resource-library state, filter resources, show source PNG preview, diagnostics, catalog hash, and recommended FairyGUI usage.
- The web page requires the expected `/capabilities` API version and gives an actionable stale-server message when it does not match.
- `Save Catalog Edits` calls Authoring Server `save`, requires a non-stale catalog hash, validates the complete proposed catalog, preserves resource identity/path/inventory, and only writes `ui-art-resources.json`.
- `Plan Replacement` calls Authoring Server `plan-replace` and displays blocking diagnostics for path, hash, pixel size, and alpha policy failures.
- `Replace PNG` calls Authoring Server `replace-png` only after an unblocked plan and only changes an existing same-path resource-library PNG; it still requires later FairyGUI import/publish before runtime use.
- `Plan Draft` reports resource-catalog-only intent and warns that real FairyGUI resource/layout work remains in FairyGUI Editor.
- New-resource authoring exposes rather than hard-codes alpha, replacement-size, stretch, minimum-size, and NineSlice parameters; opaque PNG resources are valid when alpha constraints are disabled.
- At desktop viewport sizes, the resource list, preview, and inspector fit in one workbench viewport and scroll independently; create and replace operations open in file-focused drawers.
- NineSlice preview boundaries update the existing numeric `scale9Grid` fields and participate in the same dirty-state and client-side validation flow.
- The editor lists all package.xml-backed FairyGUI packages and generates a resource/package-specific handoff plan without modifying source files.
- The plan distinguishes not imported, exact import, equivalent content, content drift, name conflict, missing file, and orphan target-file states.
- Package-local sidecar usage is reported as aligned, drifted, or untracked; the plan includes deterministic JSON and Markdown recipes with no timestamps or absolute paths.
- Unity Editor menu keeps `Open External Editor` and `Generate Report`; it is not the main editing surface.

Phase 1 / Phase 2 tooling 入口：

```powershell
dotnet run --project .\Tools\MxFramework.NoEngineTests\FairyGUI.Manifest.Tests\FairyGUI.Manifest.Tests.csproj -- --check-art-slices
dotnet run --project .\Tools\MxFramework.NoEngineTests\FairyGUI.Manifest.Tests\FairyGUI.Manifest.Tests.csproj -- --write-art-slice-report .\Temp\ui-art-slices.report.md
dotnet run --project .\Tools\MxFramework.NoEngineTests\FairyGUI.Manifest.Tests\FairyGUI.Manifest.Tests.csproj -- --review-report .\Temp\fairygui-review-report.md
dotnet run --project .\Tools\MxFramework.Authoring\tests\MxFramework.Authoring.Tests\MxFramework.Authoring.Tests.csproj
git diff --check
```

这些命令覆盖 scanner / schema / report。Editor 窗口还需要 Unity refresh/compile + Console error check；PNG 替换流需要人工选择测试 PNG 后确认约束阻止/放行行为。Controlled XML Writer 仍未实现。
