"use strict";

const assert = require("assert");
const layout = require("../web/layout-tools.js");

function node(id, x, y, width = 40, height = 20) {
  return { nodeId: id, name: id, order: Number(id.slice(1)), parentContainerId: "", layout: { x, y, width, height, horizontalAnchor: "Left", verticalAnchor: "Top" } };
}

const blueprint = { schemaVersion: "mx.uiScreenBlueprint.v1", containers: [], nodes: [node("n1", 10, 20), node("n2", 70, 40), node("n3", 150, 80)] };
const group = layout.createContainer(blueprint, ["n1", "n2", "n3"], "Free");
assert.equal(blueprint.schemaVersion, "mx.uiScreenBlueprint.v2");
assert.deepEqual(layout.materialize(blueprint).get("n2").x, 70);

group.mode = "Horizontal";
group.layout.width = 240;
group.layout.height = 60;
group.padding = { left: 10, top: 5, right: 10, bottom: 5 };
group.gapX = 8;
group.crossAxisAlign = "Center";
const horizontal = layout.materialize(blueprint);
assert.equal(horizontal.get("n1").x, group.layout.x + 10);
assert.equal(horizontal.get("n2").x, group.layout.x + 58);
assert.equal(horizontal.get("n1").y, group.layout.y + 20);

const moved = layout.ungroup(blueprint, group.containerId);
assert.deepEqual(moved.sort(), ["n1", "n2", "n3"]);
assert.equal(blueprint.containers.length, 0);
assert.equal(blueprint.nodes[1].layout.x, horizontal.get("n2").x);

layout.align(blueprint, ["n1", "n2"], "left");
assert.equal(blueprint.nodes[0].layout.x, blueprint.nodes[1].layout.x);
layout.matchSize(blueprint, ["n1", "n2"], "n1", "both");
assert.equal(blueprint.nodes[1].layout.width, blueprint.nodes[0].layout.width);
assert.equal(blueprint.nodes[1].layout.height, blueprint.nodes[0].layout.height);
layout.distribute(blueprint, ["n1", "n2", "n3"], "vertical");
assert(blueprint.nodes[0].layout.y <= blueprint.nodes[1].layout.y);

const v3Blueprint = { schemaVersion: "mx.uiScreenBlueprint.v3", containers: [], nodes: [node("n1", 10, 20), node("n2", 70, 40)] };
layout.createContainer(v3Blueprint, ["n1", "n2"], "Vertical");
assert.equal(v3Blueprint.schemaVersion, "mx.uiScreenBlueprint.v3", "Layout tools must not downgrade a List-capable V3 blueprint.");

console.log("UI Composer layout tools tests passed.");
