(function (root, factory) {
  const api = factory();
  if (typeof module === "object" && module.exports) module.exports = api;
  else root.MxComposerClipboard = api;
})(typeof globalThis !== "undefined" ? globalThis : this, function () {
  "use strict";
  const clone = value => JSON.parse(JSON.stringify(value));

  function capture(blueprint, selectedNodeIds, selectedContainerId) {
    const nodes = blueprint.nodes || [];
    const containers = blueprint.containers || [];
    const nodeIds = new Set(selectedNodeIds || []);
    const containerIds = new Set();
    if (selectedContainerId) collectContainers(containers, selectedContainerId, containerIds);
    if (containerIds.size) nodes.filter(node => containerIds.has(node.parentContainerId)).forEach(node => nodeIds.add(node.nodeId));
    return { nodes: nodes.filter(node => nodeIds.has(node.nodeId)).map(clone), containers: containers.filter(item => containerIds.has(item.containerId)).map(clone), rootContainerId: selectedContainerId || "" };
  }

  function paste(blueprint, payload, offset = 16) {
    if (!payload || (!payload.nodes?.length && !payload.containers?.length)) return { nodeIds: [], containerId: "" };
    const nodeMap = new Map();
    const containerMap = new Map();
    const nodeIds = new Set((blueprint.nodes || []).map(item => item.nodeId));
    const containerIds = new Set((blueprint.containers || []).map(item => item.containerId));
    const nodeNames = new Set((blueprint.nodes || []).map(item => item.name));
    const containerNames = new Set((blueprint.containers || []).map(item => item.name));
    (payload.containers || []).forEach(item => containerMap.set(item.containerId, uniqueId(item.containerId, containerIds)));
    (payload.nodes || []).forEach(item => nodeMap.set(item.nodeId, uniqueId(item.nodeId, nodeIds)));

    const containers = (payload.containers || []).map(source => {
      const value = clone(source);
      value.containerId = containerMap.get(source.containerId);
      value.name = uniqueName(source.name || "分组", containerNames);
      value.parentContainerId = containerMap.get(source.parentContainerId) || source.parentContainerId || "";
      if (!containerMap.has(source.parentContainerId)) { value.layout.x += offset; value.layout.y += offset; }
      return value;
    });
    const nodes = (payload.nodes || []).map(source => {
      const value = clone(source);
      value.nodeId = nodeMap.get(source.nodeId);
      value.name = uniqueName(source.name || source.type || "node", nodeNames);
      value.bindingSlot = "";
      value.parentContainerId = containerMap.get(source.parentContainerId) || source.parentContainerId || "";
      if (!containerMap.has(source.parentContainerId)) { value.layout.x += offset; value.layout.y += offset; }
      return value;
    });
    blueprint.containers = [...(blueprint.containers || []), ...containers];
    blueprint.nodes = [...(blueprint.nodes || []), ...nodes];
    return { nodeIds: nodes.map(item => item.nodeId), containerId: containerMap.get(payload.rootContainerId) || "" };
  }

  function collectContainers(containers, id, output) { if (!id || output.has(id)) return; output.add(id); containers.filter(item => item.parentContainerId === id).forEach(item => collectContainers(containers, item.containerId, output)); }
  function uniqueId(source, existing) { const base = `${source || "item"}_copy`; let value = base; let index = 2; while (existing.has(value)) value = `${base}${index++}`; existing.add(value); return value; }
  function uniqueName(source, existing) { const base = `${source || "item"} 副本`; let value = base; let index = 2; while (existing.has(value)) value = `${base} ${index++}`; existing.add(value); return value; }
  return { capture, paste };
});
