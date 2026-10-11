// Resource Library split module.
// Read-only reference graph workspace and graph summary copy.
// Loaded by index.html as a classic script; keep shared bindings global.
function renderReferenceGraphWorkspace() {
  const model = getReferenceGraphModel();

  for (const button of el.graphScopeButtons) {
    button.classList.toggle("active", button.dataset.graphScope === state.graphScope);
  }
  if (el.graphSearchInput && el.graphSearchInput.value !== state.graphSearch) {
    el.graphSearchInput.value = state.graphSearch;
  }
  if (el.graphShowOptional) el.graphShowOptional.checked = state.graphShowOptional;
  if (el.graphColorMode) el.graphColorMode.value = state.graphColorMode;

  el.graphSummary.textContent = `${model.scopeLabel} · ${model.nodes.length} 个节点 · ${model.edges.length} 条连线`;
  el.graphViewportSummary.textContent = `引用者配置 ${model.counts.source} · 资源 ${model.counts.resource} · Bundle / delivery ${model.counts.delivery}`;
  el.graphLegend.innerHTML = renderReferenceGraphLegend(model);
  el.graphNotice.innerHTML = renderReferenceGraphNotice(model);
  el.graphCanvas.innerHTML = renderReferenceGraphSvg(model);
  el.graphInspectorSummary.textContent = model.selected
    ? `${model.selected.kind === "edge" ? "连线" : "节点"} · ${model.selected.label}`
    : "点击节点或连线查看说明";
  el.graphInspectorContent.innerHTML = renderReferenceGraphInspector(model);
}

function getReferenceGraphModel() {
  const scope = getReferenceGraphScopeRows();
  const search = state.graphSearch.trim().toLowerCase();
  let rows = asArray(scope.rows);
  if (!state.graphShowOptional) rows = rows.filter(row => row.required);
  if (search) rows = rows.filter(row => referenceGraphRowMatchesSearch(row, search));

  let graph = buildReferenceGraphFromRows(rows);
  let denoised = false;
  let denoiseMessage = "";
  if (graph.nodes.length > GRAPH_NODE_LIMIT) {
    const limitedRows = denoiseReferenceGraphRows(rows, scope.targets);
    graph = buildReferenceGraphFromRows(limitedRows);
    denoised = true;
    denoiseMessage = graph.nodes.length > GRAPH_NODE_LIMIT
      ? `全部引用图节点过多，已显示前 ${graph.nodes.length} 个高优先节点；可搜索或切换范围继续缩小。`
      : `全部引用图节点过多，已自动只显示问题节点、当前资源邻居和当前 Bundle 邻居。`;
  }

  const layout = getReferenceGraphLayout(graph.nodes, graph.edges);
  const selectedId = state.graphSelectedNodeId || getDefaultReferenceGraphSelectionId(layout.nodes);
  const selected = findReferenceGraphSelection(layout, selectedId);
  const targetItems = uniqueItems(graph.nodes.filter(node => node.type === "resource" && node.item).map(node => node.item));
  const recommendations = getBundleRecommendationRows(targetItems);
  const buckets = getReferenceIssueBuckets(recommendations);
  return {
    ...graph,
    ...layout,
    scope: state.graphScope,
    scopeLabel: formatReferenceGraphScopeLabel(scope),
    scopeHint: scope.hint,
    scopeBundleId: scope.bundleId || "",
    search,
    denoised,
    denoiseMessage,
    recommendations,
    buckets,
    selected,
    selectedId,
    counts: {
      source: graph.nodes.filter(node => node.type === "source").length,
      resource: graph.nodes.filter(node => node.type === "resource").length,
      delivery: graph.nodes.filter(node => node.type === "delivery").length
    }
  };
}

function getReferenceGraphScopeRows() {
  const selected = getSelectedItem();
  const checked = getCheckedItems();
  let targets = [];
  let bundleId = "";
  let hint = "";

  if (state.graphScope === "current") {
    targets = selected ? [selected] : [];
    hint = targets.length > 0 ? "当前资源视角：显示当前资源、引用它的配置，以及同一配置还会加载的资源。" : "请先在 Browse 选择一个资源。";
  } else if (state.graphScope === "checked") {
    targets = checked.length > 0 ? checked : selected ? [selected] : [];
    hint = checked.length > 0 ? "已勾选资源视角：适合批量检查一组资源的引用关系。" : "未勾选资源时暂用当前资源。";
  } else if (state.graphScope === "issues") {
    targets = getBrowserItems().filter(item => getBundleRecommendationRows([item]).some(row => row.severity === "blocking" || row.severity === "suggested"));
    hint = "只看需要处理：聚焦必须处理和建议处理的资源，以及它们的引用者。";
  } else if (state.graphScope === "bundle") {
    bundleId = getReferenceGraphBundleScopeId();
    targets = bundleId ? getBrowserItems().filter(item => pick(findDraftBuildProfileEntryForItem(item), "bundleRule") === bundleId) : [];
    hint = bundleId ? `当前 Bundle 视角：显示 Bundle ${bundleId} 的成员和相关引用者。` : "当前没有可用 Bundle。请先在 Profile 新建或选择 Bundle。";
  } else {
    targets = getBrowserItems();
    hint = "全部引用图会自动降噪，优先显示问题节点、当前资源邻居和当前 Bundle 邻居。";
  }

  const rows = state.graphScope === "all"
    ? getAllReferenceEdges().map(edge => normalizeReferenceRow(edge, null))
    : getReferenceGraphRowsForItems(targets, true);
  return { targets, rows, bundleId, hint };
}

function getReferenceGraphRowsForItems(items, includeCoReferences) {
  const directRows = uniqueReferenceRows(asArray(items).flatMap(item => getReferenceRowsForItem(item)));
  if (!includeCoReferences || directRows.length === 0) return directRows;
  return uniqueReferenceRows([...directRows, ...getCoReferencedResourceRows(directRows)]);
}

function uniqueReferenceRows(rows) {
  const seen = new Set();
  const result = [];
  for (const row of asArray(rows)) {
    const key = getReferenceRowIdentity(row);
    if (seen.has(key)) continue;
    seen.add(key);
    result.push(row);
  }
  return result;
}

function getReferenceGraphBundleScopeId() {
  if (state.selectedBundleRuleId && findBundleRuleById(state.selectedBundleRuleId)) return state.selectedBundleRuleId;
  const selected = getSelectedItem();
  const selectedBundle = selected ? pick(findDraftBuildProfileEntryForItem(selected), "bundleRule") : "";
  if (selectedBundle && findBundleRuleById(selectedBundle)) return selectedBundle;
  return getBuildProfileBundleRules()[0]?.id || "";
}

function formatReferenceGraphScopeLabel(scope) {
  const labels = {
    current: "当前资源",
    checked: "已勾选资源",
    issues: "只看需要处理",
    bundle: scope.bundleId ? `当前 Bundle ${scope.bundleId}` : "当前 Bundle",
    all: "全部引用图"
  };
  const search = state.graphSearch.trim() ? ` · 搜索 "${state.graphSearch.trim()}"` : "";
  return `${labels[state.graphScope] || "当前资源"}${search}`;
}

function referenceGraphRowMatchesSearch(row, query) {
  const item = row.targetItem;
  const delivery = item ? getReferenceGraphDeliveryNode(item) : null;
  return [
    row.sourceLabel,
    row.sourceConfigKind,
    row.sourceStableId,
    row.sourceField,
    row.targetLabel,
    row.targetResourceKey,
    row.targetStableId,
    item ? formatResourceDisplayName(item) : "",
    item?.kind,
    item?.providerId,
    delivery?.label,
    delivery?.detail
  ].some(value => stringValue(value).toLowerCase().includes(query));
}

function denoiseReferenceGraphRows(rows, scopeTargets) {
  const priorityItems = new Set(asArray(scopeTargets).map(item => item?.key).filter(Boolean));
  const selected = getSelectedItem();
  if (selected) priorityItems.add(selected.key);
  for (const item of getBrowserItems()) {
    const recommendations = getBundleRecommendationRows([item]);
    if (recommendations.some(row => row.severity === "blocking" || row.severity === "suggested")) priorityItems.add(item.key);
  }

  const bundleId = getReferenceGraphBundleScopeId();
  if (bundleId) {
    for (const item of getBrowserItems()) {
      if (pick(findDraftBuildProfileEntryForItem(item), "bundleRule") === bundleId) priorityItems.add(item.key);
    }
  }

  const prioritySourceKeys = new Set();
  for (const row of rows) {
    if (row.targetItem && priorityItems.has(row.targetItem.key)) prioritySourceKeys.add(row.sourceKey);
  }

  let filtered = rows.filter(row =>
    (row.targetItem && priorityItems.has(row.targetItem.key)) || prioritySourceKeys.has(row.sourceKey)
  );
  if (filtered.length === 0) filtered = rows.slice(0, GRAPH_NODE_LIMIT);

  let graph = buildReferenceGraphFromRows(filtered);
  while (graph.nodes.length > GRAPH_NODE_LIMIT && filtered.length > 1) {
    filtered = filtered.slice(0, Math.max(1, Math.floor(filtered.length * 0.8)));
    graph = buildReferenceGraphFromRows(filtered);
  }
  return filtered;
}

function buildReferenceGraphFromRows(rows) {
  const nodeMap = new Map();
  const edgeMap = new Map();
  const deliveryResources = new Set();

  for (const row of uniqueReferenceRows(rows)) {
    const sourceNode = getReferenceGraphSourceNode(row);
    const resourceNode = getReferenceGraphResourceNode(row);
    addReferenceGraphNode(nodeMap, sourceNode);
    addReferenceGraphNode(nodeMap, resourceNode);
    addReferenceGraphEdge(edgeMap, {
      id: `edge:ref:${getReferenceRowIdentity(row)}`,
      type: "reference",
      from: sourceNode.id,
      to: resourceNode.id,
      label: row.required ? "必需引用" : "可选引用",
      tone: row.required ? "warn" : "muted",
      row,
      required: row.required,
      preloadPolicy: row.preloadPolicy
    });

    const item = row.targetItem;
    if (!item) continue;
    const deliveryNode = getReferenceGraphDeliveryNode(item);
    addReferenceGraphNode(nodeMap, deliveryNode);
    const resourceKey = getReferenceGraphItemNodeId(item);
    if (!deliveryResources.has(resourceKey)) {
      deliveryResources.add(resourceKey);
      addReferenceGraphEdge(edgeMap, {
        id: `edge:delivery:${resourceKey}:${deliveryNode.id}`,
        type: "delivery",
        from: resourceNode.id,
        to: deliveryNode.id,
        label: "Bundle 归属",
        tone: deliveryNode.tone || "neutral",
        item,
        delivery: deliveryNode
      });
    }
  }

  return {
    nodes: sortReferenceGraphNodes(Array.from(nodeMap.values())),
    edges: Array.from(edgeMap.values())
  };
}

function getReferenceGraphNodes(rows) {
  return buildReferenceGraphFromRows(rows).nodes;
}

function getReferenceGraphEdges(rows) {
  return buildReferenceGraphFromRows(rows).edges;
}

function addReferenceGraphNode(map, node) {
  if (!node || !node.id) return;
  const existing = map.get(node.id);
  if (!existing) {
    map.set(node.id, node);
    return;
  }
  existing.severity = mergeGraphSeverity(existing.severity, node.severity);
  existing.referenceCount = Math.max(existing.referenceCount || 0, node.referenceCount || 0);
  existing.subtitle = existing.subtitle || node.subtitle;
}

function addReferenceGraphEdge(map, edge) {
  if (!edge || !edge.id || map.has(edge.id)) return;
  map.set(edge.id, edge);
}

function getReferenceGraphSourceNode(row) {
  return {
    id: `node:source:${row.sourceKey}`,
    type: "source",
    column: "source",
    label: formatReferenceUserLabel(row.sourceConfigKind, "sourceConfig"),
    subtitle: row.sourceStableId || "未提供配置 ID",
    detail: row.sourceLabel,
    severity: row.required ? "suggested" : "info",
    sourceKey: row.sourceKey,
    row
  };
}

function getReferenceGraphResourceNode(row) {
  const item = row.targetItem;
  if (!item) {
    return {
      id: `node:resource:unmapped:${row.targetStableId || row.targetResourceKey || row.targetResourceId || row.targetLabel}`,
      type: "resource",
      column: "resource",
      label: row.targetLabel,
      subtitle: "无法映射到资源库项",
      detail: row.targetStableId || row.targetResourceKey || "-",
      severity: "info",
      row,
      unmapped: true
    };
  }
  const recommendation = getReferenceGraphItemRecommendation(item);
  const entry = findDraftBuildProfileEntryForItem(item);
  const delivery = getDeliveryEntryState(item, entry);
  return {
    id: getReferenceGraphItemNodeId(item),
    type: "resource",
    column: "resource",
    label: formatResourceDisplayName(item),
    subtitle: `${item.kind || "资源"} · ${formatProfileStatusLabel(getBuildProfileStatus(item))} · ${delivery.label}`,
    detail: item.resourceKey || item.stableId || item.key,
    severity: recommendation.severity,
    item,
    referenceCount: getReferenceRowsForItem(item).length
  };
}

function getReferenceGraphDeliveryNode(item) {
  const assignment = getBundleAssignmentState(item);
  const entry = assignment.entry;
  if (!entry) {
    return {
      id: "node:delivery:not-in-profile",
      type: "delivery",
      column: "delivery",
      label: "未加入 Profile",
      subtitle: "不会进入 Bundle Plan",
      detail: "notInProfile",
      severity: "blocking",
      tone: "warn",
      deliveryMode: "notInProfile"
    };
  }
  if (assignment.missingBundleRule) {
    return {
      id: `node:delivery:missing-bundle:${assignment.bundleRule}`,
      type: "delivery",
      column: "delivery",
      label: `Bundle 缺失：${assignment.bundleRule}`,
      subtitle: "Profile 指向不存在的 Bundle rule",
      detail: assignment.bundleRule,
      severity: "blocking",
      tone: "error",
      bundleRule: assignment.bundleRule,
      deliveryMode: assignment.deliveryMode
    };
  }
  if (assignment.unassignedInternalBundle) {
    return {
      id: "node:delivery:needs-bundle",
      type: "delivery",
      column: "delivery",
      label: "需要 Bundle",
      subtitle: "internal 资源还没有 Bundle",
      detail: "unassigned internal",
      severity: "blocking",
      tone: "warn",
      deliveryMode: assignment.deliveryMode
    };
  }
  if (assignment.bundleRule) {
    const rule = findBundleRuleById(assignment.bundleRule);
    return {
      id: `node:delivery:bundle:${assignment.bundleRule}`,
      type: "delivery",
      column: "delivery",
      label: `Bundle ${assignment.bundleRule}`,
      subtitle: rule?.bundleName || "内部 Player Bundle",
      detail: assignment.bundleRule,
      severity: "accepted",
      tone: "ok",
      bundleRule: assignment.bundleRule,
      deliveryMode: assignment.deliveryMode
    };
  }
  const mode = assignment.deliveryMode || "internal";
  const labels = {
    external: "External 配置引用",
    editorOnly: "仅编辑器",
    excluded: "不构建",
    internal: "Standalone / internal"
  };
  return {
    id: `node:delivery:${mode}`,
    type: "delivery",
    column: "delivery",
    label: labels[mode] || mode,
    subtitle: mode === "external" ? "不进入本地 AssetBundle" : "不使用普通 Bundle 归属",
    detail: mode,
    severity: mode === "excluded" ? "info" : "accepted",
    tone: mode === "excluded" ? "muted" : "info",
    deliveryMode: mode
  };
}

function getReferenceGraphItemNodeId(item) {
  return `node:resource:${item?.key || item?.stableId || item?.resourceKey || "unknown"}`;
}

function getReferenceGraphItemRecommendation(item) {
  const recommendations = getBundleRecommendationRows([item]);
  const severity = recommendations.reduce((current, row) => mergeGraphSeverity(current, row.severity), "info");
  return { recommendations, severity };
}

function mergeGraphSeverity(left, right) {
  const order = { blocking: 4, suggested: 3, accepted: 2, info: 1, neutral: 0 };
  return (order[right] || 0) > (order[left] || 0) ? right : left;
}

function sortReferenceGraphNodes(nodes) {
  const columnOrder = { source: 0, resource: 1, delivery: 2 };
  const severityOrder = { blocking: 0, suggested: 1, accepted: 2, info: 3 };
  return nodes.sort((a, b) =>
    (columnOrder[a.column] ?? 9) - (columnOrder[b.column] ?? 9)
    || (severityOrder[a.severity] ?? 9) - (severityOrder[b.severity] ?? 9)
    || compareText(a.label, b.label)
  );
}

function getReferenceGraphLayout(nodes, edges) {
  const columns = {
    source: nodes.filter(node => node.column === "source"),
    resource: nodes.filter(node => node.column === "resource"),
    delivery: nodes.filter(node => node.column === "delivery")
  };
  const width = 1040;
  const nodeWidth = 240;
  const nodeHeight = 58;
  const rowGap = 24;
  const columnX = { source: 48, resource: 400, delivery: 752 };
  const maxRows = Math.max(columns.source.length, columns.resource.length, columns.delivery.length, 1);
  const height = Math.max(360, 48 + maxRows * (nodeHeight + rowGap));
  const positionedNodes = [];
  for (const [column, list] of Object.entries(columns)) {
    const totalHeight = list.length * nodeHeight + Math.max(0, list.length - 1) * rowGap;
    const startY = Math.max(32, (height - totalHeight) / 2);
    list.forEach((node, index) => {
      positionedNodes.push({
        ...node,
        x: columnX[column],
        y: startY + index * (nodeHeight + rowGap),
        width: nodeWidth,
        height: nodeHeight
      });
    });
  }
  const nodeMap = new Map(positionedNodes.map(node => [node.id, node]));
  const positionedEdges = edges
    .map(edge => ({ ...edge, fromNode: nodeMap.get(edge.from), toNode: nodeMap.get(edge.to) }))
    .filter(edge => edge.fromNode && edge.toNode);
  return { width, height, nodes: positionedNodes, edges: positionedEdges };
}

function renderReferenceGraphLegend(model) {
  return `
    <div class="graph-help-card">
      <strong>怎么读图</strong>
      <span>左边是谁引用，中间是什么资源，右边资源会进入哪个 Bundle 或 delivery 状态。</span>
    </div>
    <div class="graph-legend-row">${smallBadge("必须处理", "error")}<span>构建或运行时可能缺资源</span></div>
    <div class="graph-legend-row">${smallBadge("建议处理", "warn")}<span>Bundle 分散或加载顺序风险</span></div>
    <div class="graph-legend-row">${smallBadge("可接受", "ok")}<span>当前 Profile / Bundle 状态可接受</span></div>
    <div class="graph-legend-row">${smallBadge("信息", "muted")}<span>只读说明或无法自动映射</span></div>
    <p>${escapeHtml(model.scopeHint)}</p>`;
}

function renderReferenceGraphNotice(model) {
  const notices = [];
  if (model.denoised) notices.push(model.denoiseMessage);
  if (model.nodes.length === 0) notices.push("当前范围没有可显示的引用图。请在 Browse 选择资源，或切换到“全部引用图”。");
  if (!state.graphShowOptional) notices.push("已隐藏 optional 引用，只显示运行时必需关系。");
  return notices.length === 0 ? "" : notices.map(text => `<div class="graph-notice-item">${escapeHtml(text)}</div>`).join("");
}

function renderReferenceGraphSvg(model) {
  if (model.nodes.length === 0) {
    return `<div class="empty graph-empty"><strong>没有可显示的关系图</strong><span>当前范围没有匹配的引用边。可以清空搜索、显示 optional 引用，或回 Browse 选择资源。</span></div>`;
  }
  const selectedId = model.selected?.id || "";
  return `
    <svg class="reference-graph-svg" viewBox="0 0 ${model.width} ${model.height}" role="img" aria-label="引用关系图">
      <defs>
        <marker id="graphArrow" markerWidth="8" markerHeight="8" refX="7" refY="4" orient="auto" markerUnits="strokeWidth">
          <path d="M0,0 L8,4 L0,8 Z"></path>
        </marker>
      </defs>
      <g class="graph-edges">
        ${model.edges.map(edge => renderReferenceGraphEdge(edge, edge.id === selectedId)).join("")}
      </g>
      <g class="graph-nodes">
        ${model.nodes.map(node => renderReferenceGraphNode(node, node.id === selectedId)).join("")}
      </g>
    </svg>`;
}

function renderReferenceGraphEdge(edge, selected) {
  const fromX = edge.fromNode.x + edge.fromNode.width;
  const fromY = edge.fromNode.y + edge.fromNode.height / 2;
  const toX = edge.toNode.x;
  const toY = edge.toNode.y + edge.toNode.height / 2;
  const mid = Math.max(40, (toX - fromX) / 2);
  const d = `M ${fromX} ${fromY} C ${fromX + mid} ${fromY}, ${toX - mid} ${toY}, ${toX} ${toY}`;
  return `
    <g class="graph-edge ${edge.type} ${edge.tone || "neutral"}${selected ? " selected" : ""}" data-graph-edge-id="${escapeHtml(edge.id)}">
      <path d="${escapeHtml(d)}"></path>
      <title>${escapeHtml(edge.label)}</title>
    </g>`;
}

function renderReferenceGraphNode(node, selected) {
  const tone = getReferenceGraphNodeTone(node);
  const label = truncateMiddle(node.label, 32);
  const subtitle = truncateMiddle(node.subtitle || node.detail || "", 38);
  return `
    <g class="graph-node ${node.type} ${tone}${selected ? " selected" : ""}" data-graph-node-id="${escapeHtml(node.id)}" transform="translate(${node.x} ${node.y})">
      <rect width="${node.width}" height="${node.height}" rx="8"></rect>
      <text class="graph-node-type" x="12" y="18">${escapeHtml(getReferenceGraphNodeTypeLabel(node))}</text>
      <text class="graph-node-label" x="12" y="36">${escapeHtml(label)}</text>
      <text class="graph-node-subtitle" x="12" y="51">${escapeHtml(subtitle)}</text>
      <title>${escapeHtml(`${getReferenceGraphNodeTypeLabel(node)} · ${node.label}`)}</title>
    </g>`;
}

function getReferenceGraphNodeTone(node) {
  if (state.graphColorMode === "delivery" && node.type === "delivery") return node.tone || "neutral";
  if (node.severity === "blocking") return "error";
  if (node.severity === "suggested") return "warn";
  if (node.severity === "accepted") return "ok";
  return "neutral";
}

function getReferenceGraphNodeTypeLabel(node) {
  if (node.type === "source") return "引用者配置";
  if (node.type === "resource") return "资源";
  if (node.bundleRule) return "Bundle";
  return "Delivery";
}

function findReferenceGraphSelection(graph, selectedId) {
  const node = graph.nodes.find(item => item.id === selectedId);
  if (node) return { kind: "node", id: node.id, label: node.label, node };
  const edge = graph.edges.find(item => item.id === selectedId);
  if (edge) return { kind: "edge", id: edge.id, label: edge.label, edge };
  return null;
}

function getDefaultReferenceGraphSelectionId(nodes) {
  const selected = getSelectedItem();
  if (selected) {
    const resourceId = getReferenceGraphItemNodeId(selected);
    if (nodes.some(node => node.id === resourceId)) return resourceId;
  }
  return nodes.find(node => node.severity === "blocking")?.id || nodes.find(node => node.type === "resource")?.id || nodes[0]?.id || "";
}

function renderReferenceGraphInspector(model) {
  if (!model.selected) {
    return `
      <div class="graph-inspector-empty">
        <strong>点击图中的节点或连线</strong>
        <span>节点会说明“它是什么、为什么重要、当前 Bundle 状态、相关建议”。修复动作会跳回 Refs，不会在图上直接写草稿。</span>
        <button type="button" data-graph-action="open-refs">去 Refs 查看建议</button>
      </div>`;
  }
  if (model.selected.kind === "edge") return renderReferenceGraphEdgeInspector(model.selected.edge);
  return renderReferenceGraphNodeInspector(model.selected.node);
}

function renderReferenceGraphNodeInspector(node) {
  if (node.type === "source") {
    return `
      <div class="graph-detail-card">
        <span>引用者配置</span>
        <strong>${escapeHtml(node.label)}</strong>
        <p>它是引用资源的配置来源。顺着连线可以看到这个配置会加载哪些资源。</p>
        ${renderKeyValueList([
          ["配置 ID", node.subtitle || "-"],
          ["处理状态", formatGraphSeverityLabel(node.severity)]
        ])}
        <div class="graph-inspector-actions">
          <button type="button" data-graph-action="open-refs">去 Refs 查看建议</button>
        </div>
      </div>`;
  }
  if (node.type === "delivery") {
    return `
      <div class="graph-detail-card">
        <span>${escapeHtml(getReferenceGraphNodeTypeLabel(node))}</span>
        <strong>${escapeHtml(node.label)}</strong>
        <p>${escapeHtml(getReferenceGraphDeliveryMeaning(node))}</p>
        ${renderKeyValueList([
          ["状态", formatGraphSeverityLabel(node.severity)],
          ["Bundle", node.bundleRule || "-"],
          ["deliveryMode", node.deliveryMode || "-"]
        ])}
        <div class="graph-inspector-actions">
          <button type="button" data-graph-action="open-refs"${node.bundleRule ? ` data-bundle-rule-id="${escapeHtml(node.bundleRule)}"` : ""}>去 Refs 查看建议</button>
        </div>
      </div>`;
  }
  const item = node.item;
  if (!item) {
    return `
      <div class="graph-detail-card">
        <span>资源</span>
        <strong>${escapeHtml(node.label)}</strong>
        <p>引用图里有这个目标，但当前资源库没有找到对应资源项，因此不能自动跳转或生成 Profile 草稿。</p>
        ${renderKeyValueList([["目标", node.detail || "-"], ["状态", "无法映射"]])}
        <div class="graph-inspector-actions">
          <button type="button" data-graph-action="open-refs">去 Refs 查看建议</button>
        </div>
      </div>`;
  }
  const entry = findDraftBuildProfileEntryForItem(item);
  const delivery = getDeliveryEntryState(item, entry);
  const recommendation = getReferenceGraphItemRecommendation(item);
  const planEntry = findBundlePlanEntryForItem(item);
  return `
    <div class="graph-detail-card">
      <span>资源</span>
      <strong>${escapeHtml(formatResourceDisplayName(item))}</strong>
      <p>${escapeHtml(getReferenceGraphResourceMeaning(item, recommendation.severity, delivery))}</p>
      ${renderKeyValueList([
        ["类型", item.kind || "-"],
        ["Profile", formatProfileStatusLabel(getBuildProfileStatus(item))],
        ["Bundle 状态", delivery.label],
        ["Bundle", pick(entry, "bundleRule") || "-"],
        ["Bundle Plan", planEntry ? "已有预览" : "-"],
        ["引用数", String(getReferenceRowsForItem(item).length)],
        ["建议", formatGraphSeverityLabel(recommendation.severity)]
      ])}
      ${recommendation.recommendations.length > 0 ? `<div class="graph-related-recommendations">${recommendation.recommendations.slice(0, 3).map(row => `<p>${escapeHtml(row.title)}：${escapeHtml(row.message)}</p>`).join("")}</div>` : ""}
      <div class="graph-inspector-actions">
        <button type="button" data-graph-action="open-refs" data-resource-key="${escapeHtml(item.key)}">去 Refs 查看建议</button>
        <button type="button" data-graph-action="open-browse" data-resource-key="${escapeHtml(item.key)}">在 Browse 中查看资源</button>
      </div>
    </div>`;
}

function renderReferenceGraphEdgeInspector(edge) {
  if (edge.type === "delivery") {
    return `
      <div class="graph-detail-card">
        <span>Bundle 归属连线</span>
        <strong>${escapeHtml(edge.fromNode.label)} -> ${escapeHtml(edge.toNode.label)}</strong>
        <p>这条线表示资源当前在 Profile 草稿中的 Bundle / delivery 归属。它只读，不会修改 Profile。</p>
        ${renderKeyValueList([
          ["资源", edge.item ? formatResourceDisplayName(edge.item) : "-"],
          ["目标", edge.delivery?.label || "-"]
        ])}
        <div class="graph-inspector-actions">
          <button type="button" data-graph-action="open-refs"${edge.item ? ` data-resource-key="${escapeHtml(edge.item.key)}"` : ""}>去 Refs 查看建议</button>
        </div>
      </div>`;
  }
  const row = edge.row;
  return `
    <div class="graph-detail-card">
      <span>引用连线</span>
      <strong>${escapeHtml(edge.fromNode.label)} -> ${escapeHtml(edge.toNode.label)}</strong>
      <p>这条线表示配置会使用这个资源。required 资源需要保证 Profile 和 Bundle 归属清晰。</p>
      ${renderKeyValueList([
        ["加载要求", row.required ? "运行时必需" : "可选"],
        ["preload policy", row.preloadPolicy || "-"],
        ["source field", row.sourceField || "-"],
        ["target", row.targetResourceKey || row.targetStableId || "-"]
      ])}
      <div class="graph-inspector-actions">
        <button type="button" data-graph-action="open-refs"${row.targetItem ? ` data-resource-key="${escapeHtml(row.targetItem.key)}"` : ""}>去 Refs 查看建议</button>
      </div>
    </div>`;
}

function getReferenceGraphDeliveryMeaning(node) {
  if (node.bundleRule) return "这是资源会进入的内部 Player Bundle。看这里可以判断同一配置的 required 资源是否被分散。";
  if (node.deliveryMode === "notInProfile") return "资源还没有加入 Profile，因此不会进入 Bundle Plan。";
  if (node.deliveryMode === "external") return "资源作为外部配置引用存在，不进入本地 AssetBundle。";
  if (node.deliveryMode === "editorOnly") return "资源只在编辑器使用，不进入 Player 构建。";
  if (node.deliveryMode === "excluded") return "资源被排除，不进入 Player 构建。";
  return "这是资源当前的 delivery 状态。";
}

function getReferenceGraphResourceMeaning(item, severity, delivery) {
  if (severity === "blocking") return `${formatResourceDisplayName(item)} 有必须处理的 Bundle/Profile 风险：${delivery.label}。`;
  if (severity === "suggested") return `${formatResourceDisplayName(item)} 可以构建，但 Bundle 分配有建议优化项。`;
  if (severity === "accepted") return `${formatResourceDisplayName(item)} 的引用和 Bundle 状态当前可接受。`;
  return `${formatResourceDisplayName(item)} 当前只是信息项或没有可自动处理建议。`;
}

function formatGraphSeverityLabel(severity) {
  const labels = { blocking: "必须处理", suggested: "建议处理", accepted: "可接受", info: "信息", neutral: "信息" };
  return labels[severity] || severity || "信息";
}

function handleReferenceGraphClick(event) {
  const node = event.target.closest("[data-graph-node-id]");
  if (node) {
    state.graphSelectedNodeId = node.dataset.graphNodeId || "";
    renderReferenceGraphWorkspace();
    return;
  }
  const edge = event.target.closest("[data-graph-edge-id]");
  if (edge) {
    state.graphSelectedNodeId = edge.dataset.graphEdgeId || "";
    renderReferenceGraphWorkspace();
  }
}

function handleReferenceGraphActionClick(event) {
  const button = event.target.closest("button[data-graph-action]");
  if (!button || button.disabled) return;
  const action = button.dataset.graphAction;
  const resourceKey = button.dataset.resourceKey || "";
  const bundleRuleId = button.dataset.bundleRuleId || "";
  if (resourceKey) state.selectedResourceKey = resourceKey;
  if (action === "open-refs") {
    state.referencesFilter = resourceKey ? "current" : "issues";
    state.referencesBundleRuleId = resourceKey ? "" : bundleRuleId || "";
    setWorkspace("references");
    render();
    return;
  }
  if (action === "open-browse") {
    setWorkspace("browse");
    render();
    if (resourceKey) selectResource(resourceKey);
  }
}

async function copyReferenceGraphSummary() {
  const model = getReferenceGraphModel();
  const summary = {
    package: state.packageRelative,
    scope: model.scopeLabel,
    denoised: model.denoised,
    nodes: model.nodes.map(node => ({
      id: node.id,
      type: getReferenceGraphNodeTypeLabel(node),
      label: node.label,
      status: formatGraphSeverityLabel(node.severity),
      bundleRule: node.bundleRule || ""
    })),
    edges: model.edges.map(edge => ({
      id: edge.id,
      type: edge.type,
      from: edge.fromNode?.label || edge.from,
      to: edge.toNode?.label || edge.to,
      label: edge.label,
      required: Boolean(edge.required),
      preloadPolicy: edge.preloadPolicy || ""
    })),
    note: "Graph 是只读可视图；修复建议请回到 Refs，保存 Profile 后才落盘。"
  };
  await copyText(JSON.stringify(summary, null, 2), "已复制关系图摘要");
}

function truncateMiddle(value, maxLength) {
  const text = stringValue(value);
  if (text.length <= maxLength) return text;
  const left = Math.ceil((maxLength - 1) / 2);
  const right = Math.floor((maxLength - 1) / 2);
  return `${text.slice(0, left)}…${text.slice(text.length - right)}`;
}
