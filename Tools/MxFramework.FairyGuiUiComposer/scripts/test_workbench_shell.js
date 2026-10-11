"use strict";

const assert = require("assert");
const fs = require("fs");
const path = require("path");

const root = path.resolve(__dirname, "..");
const html = fs.readFileSync(path.join(root, "web", "index.html"), "utf8");
const css = fs.readFileSync(path.join(root, "web", "styles.css"), "utf8");
const app = fs.readFileSync(path.join(root, "web", "app.js"), "utf8");

assert(app.includes('const EXPECTED_API_VERSION = "mx.uiComposer.authoring.v5";'), "Workbench must reject pre-List Authoring API versions.");

const ids = [...html.matchAll(/\bid="([^"]+)"/g)].map(match => match[1]);
assert.equal(new Set(ids).size, ids.length, "Workbench DOM ids must remain unique.");

for (const id of ["saveLayerButton", "saveButton", "syncButton", "publishButton", "commandPaletteButton", "reviewChangesButton"]) {
  assert(html.includes(`id="${id}"`), `Overflow action ${id} is missing.`);
}
assert.equal((html.match(/data-rail-tab=/g) || []).length, 4, "Workbench must expose four authoring modes.");
assert.equal((html.match(/data-anchor-h=/g) || []).length, 9, "Anchor matrix must contain nine presets.");
assert(html.includes("class=\"canvas-ruler ruler-top\"") && html.includes("class=\"canvas-ruler ruler-left\""), "Canvas rulers are missing.");
assert(html.includes("<details class=\"inspector-section\""), "Inspector sections must be collapsible.");
assert(css.includes("@media (max-width: 1279px)") && css.includes("body.inspector-open .inspector"), "Responsive drawer behavior is missing.");
assert(css.includes("grid-template-columns: clamp(260px, 18vw, 300px)"), "Desktop three-column workbench layout is missing.");
assert(app.includes("function setCanvasTool") && app.includes("function applyAnchorPreset") && app.includes("function renderRulers"), "Workbench interactions are not wired.");
assert(app.includes("function rulerStepForScale") && app.includes("ruler-tick"), "Adaptive canvas ruler ticks are not wired.");
assert(app.includes('"canvasRulerTop", "canvasRulerLeft", "canvasRulerCorner"'), "Canvas ruler elements must share the cached DOM lookup path.");
assert(css.includes(".ruler-tick.origin") && css.includes(".canvas-ruler-corner::before"), "Canvas ruler origin styling is missing.");
assert(html.includes('id="gridToggle"') && html.includes('id="snapToggle"') && html.includes('id="gridSizeSelect"'), "Grid visibility, snapping, and grid size must be independent controls.");
assert(app.includes("state.gridVisible") && app.includes("state.snapEnabled") && app.includes("event.altKey"), "Independent grid settings and temporary Alt snap bypass are not wired.");
assert(app.includes("mx-ui-composer-canvas-preferences"), "Canvas preferences must persist locally.");
assert(css.includes(".design-canvas.grid::after") && css.includes("pointer-events: none"), "Visual grid must render as a non-interactive overlay above canvas content.");
assert(!css.includes(".canvas-node.selected, .canvas-node.multi-selected { z-index"), "Selection styling must not reorder full-screen canvas nodes above their siblings.");
assert(css.includes("var(--grid-major-step, 40px)") && app.includes('state.gridSize * 5'), "Visual grid must distinguish configured minor and major steps.");
assert(app.includes('state.gridSize * scale >= 7') && css.includes(".grid-minor-visible"), "Minor grid lines must hide automatically when zoom makes them too dense.");
assert(app.includes("border-image-source:url('${url}')") && !app.includes('border-image-source:url("${url}")'), "Semantic resource URLs must not terminate the canvas style attribute.");
assert(app.includes("presentation.titleColor") && css.includes("--component-over-overlay"), "Button preview styling must come from the semantic component recipe.");
assert(app.includes('recipe?.componentType === "Composite"') && app.includes("function renderCompositePreview"), "Controlled Composite recipes must render their typed child template on the canvas.");
assert(html.includes('data-create-type="List"') && html.includes('id="listFields"'), "Controlled List creation and Inspector fields are missing.");
for (const id of ["listItemRecipeInput", "listLayoutSelect", "listSelectionModeSelect", "listLineGapInput", "listColumnGapInput", "listVirtualizedInput", "listItemUsageSummary"]) {
  assert(html.includes(`id="${id}"`) && app.includes(`"${id}"`), `List field ${id} must share the cached DOM lookup path.`);
}
assert(app.includes("function applyListForm") && app.includes('state.blueprint.schemaVersion = "mx.uiScreenBlueprint.v3"'), "List editing must upgrade and write the V3 blueprint shape.");
assert(css.includes(".canvas-node.list") && css.includes(".list-node-preview"), "Controlled List canvas preview styling is missing.");
assert(app.includes("受控复合配方（只读）") && app.includes('recipe.componentType !== "Button"'), "Composite recipe form editing must remain explicitly read-only.");
assert(app.includes("function feedbackSummary") && app.includes("即时 ${escapeHtml(policy.activation") && html.includes("只读反馈策略"), "Binding feedback policy must render read-only in the Inspector.");
assert(app.includes("node.visible !== false") && css.includes(".hierarchy-row.blueprint-hidden"), "Initially hidden authored surfaces must remain discoverable without covering the default canvas state.");
assert(css.includes(".composite-preview") && css.includes(".composite-child"), "Composite canvas preview styling is missing.");

console.log("UI Composer workbench shell tests passed.");
