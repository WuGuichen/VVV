import assert from "node:assert/strict";
import fs from "node:fs";
import vm from "node:vm";

const elements = new Map();
function element(id) {
  if (!elements.has(id)) {
    const listeners = {};
    elements.set(id, {
      value: "", textContent: "", hidden: false, disabled: false, listeners,
      classList: { add() {} }, focus() {}, contains: () => false,
      addEventListener(name, handler) { listeners[name] = handler; },
      showModal() { this.open = true; }, close() { this.open = false; listeners.close?.(); },
      setAttribute(name, value) { this[name] = value; }, removeAttribute(name) { delete this[name]; },
      querySelector: () => null,
      set innerHTML(value) {
        this.markup = value;
        if (id === "targetSelect") this.value = value.match(/value="([^"]*)"/)?.[1] || "";
      },
      get innerHTML() { return this.markup || ""; }
    });
  }
  return elements.get(id);
}
const requests = [];
const navigation = [];
const storage = new Map();
const catalogs = new Map([
  ["/api/character/packages", [{ relative: "template", isTemplate: true }, { relative: "character-a", packageId: "a" }, { relative: "character-b", packageId: "b" }]],
  ["/api/authoring/ui-composer/screens", [{ screenId: "screen.menu", displayName: "Main Menu" }, { screenId: "screen.deck", displayName: "Deck" }]],
  ["/api/authoring/buffs/packages", [{ relative: "buff/first", packageId: "First" }, { relative: "buff/second", packageId: "Second" }]],
  ["/api/authoring/ui-art/resources/libraries", [{ libraryId: "MxUiFoundation" }]]
]);
let failUrl = "";
let heldRequest = null;
const documentListeners = {};
const context = vm.createContext({
  console, URLSearchParams, AbortSignal, setTimeout,
  document: { getElementById: element, addEventListener: (name, callback) => { documentListeners[name] = callback; } },
  window: { location: { protocol: "http:", search: "?package=character-b", assign: url => navigation.push(url) }, setTimeout },
  localStorage: { getItem: key => storage.get(key) || null, setItem: (key, value) => storage.set(key, value) },
  fetch: async url => {
    requests.push(url);
    if (heldRequest && url === heldRequest.url) return heldRequest.promise;
    if (url === failUrl) return { ok: false, status: 503, statusText: "Unavailable" };
    return { ok: true, json: async () => catalogs.get(url) };
  }
});
const read = name => fs.readFileSync(new URL(`../web/${name}`, import.meta.url), "utf8");
vm.runInContext(read("common.js"), context);
vm.runInContext(read("app.js") + "\nglobalThis.test = { Hub, state, el, TOOLS, refresh, getTargetOptions, openTargetPicker, loadTargetOptions, submitTarget, renderRecent, openRecent };", context);
documentListeners.DOMContentLoaded();
await new Promise(setImmediate);
const { test } = context;
assert.deepEqual(requests, ["/api/character/packages"], "home should only check the service, not load readiness or resource inventories");
assert.equal((test.el.toolGrid.innerHTML.match(/data-tool=/g) || []).length, 7);
assert.equal(test.el.recentSection.hidden, true);

const character = test.TOOLS.find(tool => tool.id === "character");
test.openTargetPicker(character);
await new Promise(setImmediate);
assert.equal(test.el.targetSelect.value, "character-b", "incoming package should preselect the target without adding a global selector");
assert(!test.el.targetSelect.innerHTML.includes('value="template"'), "new-character templates are not editable character targets");
test.submitTarget();
assert.equal(navigation.at(-1), "/Tools/MxFramework.CharacterStudio/web/?package=character-b");
test.renderRecent();
assert.equal(test.el.recentSection.hidden, false);
assert(test.el.recentList.innerHTML.includes("角色编辑器"));

test.openTargetPicker(test.TOOLS.find(tool => tool.id === "composer"));
await new Promise(setImmediate);
test.el.targetSelect.value = "screen.deck";
test.submitTarget();
assert.equal(navigation.at(-1), "/Tools/MxFramework.FairyGuiUiComposer/web/?screen=screen.deck", "Composer must open the selected screen rather than a fixed demo");
test.openTargetPicker(test.TOOLS.find(tool => tool.id === "composer"));
await new Promise(setImmediate);
assert.equal(test.el.targetSelect.value, "screen.deck", "screen target should be remembered independently of the character");

test.openTargetPicker(test.TOOLS.find(tool => tool.id === "buff"));
await new Promise(setImmediate);
test.el.targetSelect.value = "buff/second";
test.submitTarget();
assert.equal(navigation.at(-1), "/Tools/MxFramework.BuffWorkbench/web/?package=buff%2Fsecond");
await test.openRecent(0);
assert.equal(navigation.at(-1), "/Tools/MxFramework.BuffWorkbench/web/?package=buff%2Fsecond", "recent work should reopen the same target");

catalogs.set("/api/authoring/buffs/packages", []);
const beforeMissing = navigation.length;
await test.openRecent(0);
await new Promise(setImmediate);
assert.equal(navigation.length, beforeMissing, "removed targets must not be opened from stale history");
assert.equal(test.el.targetSubmitButton.disabled, true);
assert.equal(test.el.targetError.hidden, false);

failUrl = "/api/authoring/ui-composer/screens";
test.openTargetPicker(test.TOOLS.find(tool => tool.id === "composer"));
await new Promise(setImmediate);
assert.equal(test.el.targetSubmitButton.disabled, true);
assert(test.el.targetError.textContent.includes("503"));
assert(test.el.toolGrid.innerHTML.includes("503"), "entry failures should be explained next to the affected tool");
failUrl = "";

let release;
heldRequest = { url: "/api/authoring/ui-composer/screens", promise: new Promise(resolve => { release = resolve; }) };
test.openTargetPicker(test.TOOLS.find(tool => tool.id === "composer"));
test.el.targetDialog.close();
test.openTargetPicker(character);
await new Promise(setImmediate);
release({ ok: true, json: async () => catalogs.get("/api/authoring/ui-composer/screens") });
await new Promise(setImmediate);
assert.equal(test.el.targetSelect.value, "character-b", "a cancelled request must not overwrite a newer picker");
heldRequest = null;

test.Hub.rememberVisit("composer", "screen.safe", '<img src=x onerror="alert(1)">');
test.renderRecent();
assert(!test.el.recentList.innerHTML.includes("<img"), "recent labels must be escaped");
assert.equal(test.Hub.toolUrl("__proto__", "target"), null);
storage.set("mx.editorHub.preferences.v1", "broken json");
test.renderRecent();
assert.equal(test.el.recentSection.hidden, true, "corrupted browser preferences must not break the launcher");
storage.set("mx.editorHub.preferences.v1", JSON.stringify({ recent: [null, { tool: "external", targetId: "https://example.com" }] }));
test.Hub.rememberVisit("character", "character-a", "A");
assert.equal(test.Hub.readPreferences().recent.length, 1, "invalid history entries should be discarded when recording a visit");
console.log("Editor Hub launcher behavior: PASS");
