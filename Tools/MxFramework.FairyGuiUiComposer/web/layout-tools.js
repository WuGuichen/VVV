(function (root, factory) {
  const api = factory();
  if (typeof module === "object" && module.exports) module.exports = api;
  root.MxComposerLayout = api;
})(typeof globalThis !== "undefined" ? globalThis : this, function () {
  "use strict";

  const MODES = new Set(["Free", "Horizontal", "Vertical", "Grid"]);

  function containers(blueprint) { return blueprint.containers || (blueprint.containers = []); }
  function nodes(blueprint) { return blueprint.nodes || (blueprint.nodes = []); }
  function clone(value) { return JSON.parse(JSON.stringify(value)); }
  function byOrder(left, right) { return (left.order || 0) - (right.order || 0) || String(left.nodeId || left.containerId).localeCompare(String(right.nodeId || right.containerId)); }
  function padding(value) { return { left: value?.left || 0, top: value?.top || 0, right: value?.right || 0, bottom: value?.bottom || 0 }; }
  function alignOffset(value, available, used) { return value === "Center" ? (available - used) / 2 : value === "End" ? available - used : 0; }

  function ensureV2(blueprint) {
    if (!blueprint.containers) blueprint.containers = [];
    blueprint.nodes?.forEach(node => { if (node.parentContainerId == null) node.parentContainerId = ""; });
    if (blueprint.containers.length && blueprint.schemaVersion === "mx.uiScreenBlueprint.v1") blueprint.schemaVersion = "mx.uiScreenBlueprint.v2";
    return blueprint;
  }

  function directChildren(blueprint, containerId) {
    return [
      ...nodes(blueprint).filter(node => (node.parentContainerId || "") === containerId).map(node => ({ kind: "node", value: node, id: node.nodeId, order: node.order || 0, width: node.layout.width, height: node.layout.height, x: node.layout.x, y: node.layout.y })),
      ...containers(blueprint).filter(item => (item.parentContainerId || "") === containerId).map(item => ({ kind: "container", value: item, id: item.containerId, order: item.order || 0, width: item.layout.width, height: item.layout.height, x: item.layout.x, y: item.layout.y }))
    ].sort(byOrder);
  }

  function positionChildren(container, children) {
    const pad = padding(container.padding);
    const availableWidth = Math.max(0, container.layout.width - pad.left - pad.right);
    const availableHeight = Math.max(0, container.layout.height - pad.top - pad.bottom);
    if (container.mode === "Free") return children;
    if (container.mode === "Horizontal") {
      const used = children.reduce((sum, child) => sum + child.width, 0) + Math.max(0, children.length - 1) * (container.gapX || 0);
      let cursor = pad.left + alignOffset(container.mainAxisAlign, availableWidth, used);
      children.forEach(child => { child.x = cursor; child.y = pad.top + alignOffset(container.crossAxisAlign, availableHeight, child.height); cursor += child.width + (container.gapX || 0); });
      return children;
    }
    if (container.mode === "Vertical") {
      const used = children.reduce((sum, child) => sum + child.height, 0) + Math.max(0, children.length - 1) * (container.gapY || 0);
      let cursor = pad.top + alignOffset(container.mainAxisAlign, availableHeight, used);
      children.forEach(child => { child.x = pad.left + alignOffset(container.crossAxisAlign, availableWidth, child.width); child.y = cursor; cursor += child.height + (container.gapY || 0); });
      return children;
    }
    const columns = Math.max(1, container.columns || 1);
    const cellWidth = children.reduce((value, child) => Math.max(value, child.width), 0);
    const cellHeight = children.reduce((value, child) => Math.max(value, child.height), 0);
    const rows = Math.ceil(children.length / columns);
    const visibleColumns = Math.min(columns, children.length);
    const usedWidth = visibleColumns * cellWidth + Math.max(0, visibleColumns - 1) * (container.gapX || 0);
    const usedHeight = rows * cellHeight + Math.max(0, rows - 1) * (container.gapY || 0);
    const startX = pad.left + alignOffset(container.mainAxisAlign, availableWidth, usedWidth);
    const startY = pad.top + alignOffset(container.crossAxisAlign, availableHeight, usedHeight);
    children.forEach((child, index) => { child.x = startX + (index % columns) * (cellWidth + (container.gapX || 0)); child.y = startY + Math.floor(index / columns) * (cellHeight + (container.gapY || 0)); });
    return children;
  }

  function materialize(blueprint) {
    const result = new Map();
    const containerMap = new Map(containers(blueprint).map(item => [item.containerId, item]));
    nodes(blueprint).filter(node => !node.parentContainerId).forEach(node => result.set(node.nodeId, clone(node.layout)));
    function visit(container, parentX, parentY, inheritedH, inheritedV) {
      const originX = parentX + container.layout.x;
      const originY = parentY + container.layout.y;
      const horizontalAnchor = container.parentContainerId ? inheritedH : container.layout.horizontalAnchor;
      const verticalAnchor = container.parentContainerId ? inheritedV : container.layout.verticalAnchor;
      positionChildren(container, directChildren(blueprint, container.containerId)).forEach(child => {
        if (child.kind === "node") result.set(child.id, { ...clone(child.value.layout), x: originX + child.x, y: originY + child.y, horizontalAnchor, verticalAnchor });
        else visit(child.value, originX + child.x - child.value.layout.x, originY + child.y - child.value.layout.y, horizontalAnchor, verticalAnchor);
      });
    }
    containers(blueprint).filter(item => !item.parentContainerId).sort(byOrder).forEach(item => visit(item, 0, 0, item.layout.horizontalAnchor, item.layout.verticalAnchor));
    return result;
  }

  function boundsFor(blueprint, nodeIds) {
    const layouts = materialize(blueprint);
    const selected = nodeIds.map(id => layouts.get(id)).filter(Boolean);
    if (!selected.length) return null;
    const left = Math.min(...selected.map(item => item.x));
    const top = Math.min(...selected.map(item => item.y));
    const right = Math.max(...selected.map(item => item.x + item.width));
    const bottom = Math.max(...selected.map(item => item.y + item.height));
    return { x: left, y: top, width: right - left, height: bottom - top };
  }

  function nextContainerId(blueprint) {
    const used = new Set(containers(blueprint).map(item => item.containerId));
    let index = 1;
    while (used.has(`container_${index}`)) index++;
    return `container_${index}`;
  }

  function createContainer(blueprint, nodeIds, mode) {
    if (!MODES.has(mode)) throw new Error("不支持的布局容器类型。");
    const selected = nodes(blueprint).filter(node => nodeIds.includes(node.nodeId));
    if (selected.length < 2) throw new Error("至少选择两个节点才能创建分组或布局容器。");
    const parents = new Set(selected.map(node => node.parentContainerId || ""));
    if (parents.size !== 1) throw new Error("创建容器前，所选节点必须位于同一层级。");
    const absolute = materialize(blueprint);
    const parentId = selected[0].parentContainerId || "";
    const parentLayout = parentId ? containerAbsoluteOrigin(blueprint, parentId) : { x: 0, y: 0 };
    const box = boundsFor(blueprint, nodeIds);
    const id = nextContainerId(blueprint);
    const item = {
      containerId: id, name: mode === "Free" ? "新建分组" : `${mode} 布局`, parentContainerId: parentId,
      order: Math.min(...selected.map(node => node.order || 0)), mode,
      layout: { x: box.x - parentLayout.x, y: box.y - parentLayout.y, width: Math.max(1, box.width), height: Math.max(1, box.height), horizontalAnchor: selected[0].layout.horizontalAnchor || "Left", verticalAnchor: selected[0].layout.verticalAnchor || "Top" },
      padding: { left: 0, top: 0, right: 0, bottom: 0 }, gapX: 8, gapY: 8, columns: 2, mainAxisAlign: "Start", crossAxisAlign: "Start"
    };
    containers(blueprint).push(item);
    selected.forEach(node => { const layout = absolute.get(node.nodeId); node.parentContainerId = id; node.layout.x = layout.x - box.x; node.layout.y = layout.y - box.y; });
    ensureV2(blueprint);
    return item;
  }

  function containerAbsoluteOrigin(blueprint, containerId) {
    const map = new Map(containers(blueprint).map(item => [item.containerId, item]));
    let x = 0, y = 0, current = map.get(containerId), guard = 0;
    while (current && guard++ < map.size + 1) { x += current.layout.x; y += current.layout.y; current = map.get(current.parentContainerId); }
    return { x, y };
  }

  function ungroup(blueprint, containerId) {
    const container = containers(blueprint).find(item => item.containerId === containerId);
    if (!container) return [];
    const absolute = materialize(blueprint);
    const parentId = container.parentContainerId || "";
    const parentOrigin = parentId ? containerAbsoluteOrigin(blueprint, parentId) : { x: 0, y: 0 };
    const moved = [];
    nodes(blueprint).filter(node => node.parentContainerId === containerId).forEach(node => {
      const layout = absolute.get(node.nodeId); node.parentContainerId = parentId; node.layout.x = layout.x - parentOrigin.x; node.layout.y = layout.y - parentOrigin.y; moved.push(node.nodeId);
    });
    containers(blueprint).filter(item => item.parentContainerId === containerId).forEach(child => { child.parentContainerId = parentId; child.layout.x += container.layout.x; child.layout.y += container.layout.y; });
    blueprint.containers = containers(blueprint).filter(item => item.containerId !== containerId);
    return moved;
  }

  function selectionNodes(blueprint, ids) { return nodes(blueprint).filter(node => ids.includes(node.nodeId)); }
  function requireSameParent(selected) { if (new Set(selected.map(node => node.parentContainerId || "")).size > 1) throw new Error("批量布局要求节点位于同一层级。"); }
  function align(blueprint, ids, action) {
    const selected = selectionNodes(blueprint, ids); if (selected.length < 2) throw new Error("至少选择两个节点。"); requireSameParent(selected);
    const left = Math.min(...selected.map(n => n.layout.x)), top = Math.min(...selected.map(n => n.layout.y));
    const right = Math.max(...selected.map(n => n.layout.x + n.layout.width)), bottom = Math.max(...selected.map(n => n.layout.y + n.layout.height));
    selected.forEach(node => {
      if (action === "left") node.layout.x = left; else if (action === "hcenter") node.layout.x = Math.round((left + right - node.layout.width) / 2); else if (action === "right") node.layout.x = right - node.layout.width;
      else if (action === "top") node.layout.y = top; else if (action === "vcenter") node.layout.y = Math.round((top + bottom - node.layout.height) / 2); else if (action === "bottom") node.layout.y = bottom - node.layout.height;
    });
  }

  function distribute(blueprint, ids, axis) {
    const selected = selectionNodes(blueprint, ids); if (selected.length < 3) throw new Error("等间距分布至少需要三个节点。"); requireSameParent(selected);
    const key = axis === "horizontal" ? "x" : "y", size = axis === "horizontal" ? "width" : "height";
    selected.sort((a, b) => a.layout[key] - b.layout[key]);
    const start = selected[0].layout[key], end = selected[selected.length - 1].layout[key] + selected[selected.length - 1].layout[size];
    const used = selected.reduce((sum, node) => sum + node.layout[size], 0), gap = (end - start - used) / (selected.length - 1);
    let cursor = start; selected.forEach(node => { node.layout[key] = Math.round(cursor); cursor += node.layout[size] + gap; });
  }

  function matchSize(blueprint, ids, primaryId, mode) {
    const selected = selectionNodes(blueprint, ids), primary = selected.find(node => node.nodeId === primaryId); if (!primary || selected.length < 2) throw new Error("至少选择两个节点并指定主选择。");
    selected.forEach(node => { if (mode === "width" || mode === "both") node.layout.width = primary.layout.width; if (mode === "height" || mode === "both") node.layout.height = primary.layout.height; });
  }

  return { ensureV2, directChildren, positionChildren, materialize, boundsFor, createContainer, ungroup, align, distribute, matchSize, containerAbsoluteOrigin };
});
