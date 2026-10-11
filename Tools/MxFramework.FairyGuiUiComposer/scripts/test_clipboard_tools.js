const assert = require("node:assert/strict");
const clipboard = require("../web/clipboard-tools.js");

const blueprint = {
  containers: [{ containerId: "group", name: "Group", parentContainerId: "", layout: { x: 20, y: 30 } }],
  nodes: [
    { nodeId: "title", name: "title", type: "Text", bindingSlot: "title", parentContainerId: "group", layout: { x: 4, y: 5 } },
    { nodeId: "loose", name: "loose", type: "Graph", bindingSlot: "", parentContainerId: "", layout: { x: 100, y: 120 } }
  ]
};
const groupResult = clipboard.paste(blueprint, clipboard.capture(blueprint, [], "group"), 16);
assert.equal(groupResult.containerId, "group_copy");
assert.deepEqual(groupResult.nodeIds, ["title_copy"]);
assert.equal(blueprint.containers[1].layout.x, 36);
assert.equal(blueprint.nodes[2].parentContainerId, "group_copy");
assert.equal(blueprint.nodes[2].bindingSlot, "");
const nodePayload = clipboard.capture(blueprint, ["loose"], "");
assert.deepEqual(clipboard.paste(blueprint, nodePayload, 16).nodeIds, ["loose_copy"]);
assert.deepEqual(clipboard.paste(blueprint, nodePayload, 16).nodeIds, ["loose_copy2"]);
assert.equal(blueprint.nodes.find(item => item.nodeId === "loose_copy").layout.x, 116);
assert.equal(new Set(blueprint.nodes.map(item => item.name)).size, blueprint.nodes.length);
const listNode = { nodeId: "items", name: "items", type: "List", bindingSlot: "", parentContainerId: "", layout: { x: 200, y: 160 }, list: { itemComponentRecipeId: "ui.component.button.generic", layout: "Column", lineGap: 8, columnGap: 0, selectionMode: "Single", overflow: "Scroll", virtualized: true } };
blueprint.nodes.push(listNode);
const listResult = clipboard.paste(blueprint, clipboard.capture(blueprint, ["items"], ""), 16);
const copiedList = blueprint.nodes.find(item => item.nodeId === listResult.nodeIds[0]);
assert.deepEqual(copiedList.list, listNode.list);
copiedList.list.lineGap = 12;
assert.equal(listNode.list.lineGap, 8, "Pasted List styles must be deep-cloned.");
console.log("UI Composer clipboard tools tests passed.");
