// Resource Library split module.
// Browse workspace, filters, resource tree/list, import actions, and browse context actions.
// Loaded by index.html as a classic script; keep shared bindings global.
async function importResourceFile(file) {
  if (!file) return;
  const preset = getSelectedImportPreset();
  if (!isFileSupportedByPreset(file, preset)) {
    const formats = preset.extensions.map(extension => `.${extension}`).join(", ");
    state.lastActionMessage = `${preset.label} 只支持 ${formats}。`;
    render();
    return;
  }

  await executeResourceWrite("import", API.importResource, await buildImportRequest(file, preset, false), "导入资源");
}

async function importResourceFolder(files) {
  const candidates = Array.from(files || []);
  if (candidates.length === 0) return;
  const preset = getSelectedImportPreset();

  state.writeState = { status: "running", action: "stage-import", error: "" };
  state.lastActionMessage = `扫描文件夹：0 / ${candidates.length}，类型 ${preset.label}`;
  render();

  const staging = await stageImportFiles(candidates);
  const stagedItems = asArray(pick(staging, "items")).map((raw, index) => normalizeItem(raw, index));
  const diagnostics = asArray(pick(staging, "diagnostics"));
  const ignored = diagnostics.filter(diagnostic => pick(diagnostic, "code") === "AUTH_RES_IMPORT_IGNORED_FILE").length;
  const importable = stagedItems.filter(item => isImportableStagedItem(item) && isStagedItemSupportedByPreset(candidates, item, preset));
  const skipped = Math.max(0, candidates.length - ignored - importable.length);
  const suffix = formatFolderImportCountSuffix(skipped, ignored, preset);
  if (importable.length === 0) {
    state.writeState = { status: "idle", action: "", error: "" };
    state.lastActionMessage = `文件夹中没有可导入资源${suffix}。`;
    if (diagnostics.length > 0) {
      state.errors.push(apiError("导入预检", new Error(diagnostics.slice(0, 3).map(diagnostic => pick(diagnostic, "message") || pick(diagnostic, "code")).join("; "))));
    }
    render();
    return;
  }

  state.writeState = { status: "running", action: "folder-import", error: "" };
  state.lastActionMessage = `导入文件夹：0 / ${importable.length}${suffix}`;
  render();

  const failures = [];
  let selectedId = "";
  for (let i = 0; i < importable.length; i++) {
    const staged = importable[i];
    const file = findStagedSourceFile(candidates, staged);
    if (!file) {
      failures.push(`${staged.sourcePath || staged.displayName}: 找不到源文件`);
      continue;
    }
    state.lastActionMessage = `导入文件夹：${i + 1} / ${importable.length}${suffix}`;
    renderStatus();
    try {
      const payload = await postJson(API.importResource, {
        package: state.packageRelative,
        ...(await buildImportRequestFromStagedItem(file, staged, preset))
      });
      selectedId = stringValue(pick(payload, "selectedResourceKey", "selectedId")) || selectedId;
    } catch (error) {
      failures.push(`${getImportDisplayPath(file)}: ${error.message}`);
    }
  }

  state.inspectCache.clear();
  if (selectedId) {
    state.selectedResourceKey = selectedId;
  }
  state.writeState = failures.length > 0
    ? { status: "error", action: "folder-import", error: `${failures.length} 个文件导入失败` }
    : { status: "idle", action: "", error: "" };
  state.lastActionMessage = failures.length > 0
    ? `文件夹导入完成：成功 ${importable.length - failures.length}，失败 ${failures.length}${suffix}`
    : `文件夹导入完成：成功 ${importable.length}${suffix}`;
  if (failures.length > 0) {
    state.errors.push(apiError("文件夹导入", new Error(failures.slice(0, 3).join("; "))));
  }
  await loadPackageData();
}

async function reimportSelectedResource() {
  const item = getSelectedItem();
  if (!item) return;
  const id = getResourceWriteId(item);
  if (!window.confirm(`重导资源 ${formatResourceDisplayName(item) || id}？`)) return;
  await executeResourceWrite("reimport", API.reimportResource, { id }, "重导资源");
}


async function replaceSelectedResourceFile(file) {
  const item = getSelectedItem();
  if (!item || !file) return;

  const nextFormat = inferFormatFromFileName(file.name);
  const currentFormat = inferFormatFromFileName(item.sourcePath);
  let allowFormatChange = false;
  if (currentFormat && nextFormat && currentFormat !== nextFormat) {
    allowFormatChange = window.confirm(`源文件格式将从 .${currentFormat} 改为 .${nextFormat}，确认替换？`);
    if (!allowFormatChange) return;
  }

  const bytesBase64 = await readFileAsBase64(file);
  await executeResourceWrite("replace-source", API.replaceSource, {
    id: getResourceWriteId(item),
    fileName: file.name,
    role: inferModelRole(item),
    allowFormatChange,
    bytesBase64
  }, "替换源文件");
}

async function buildImportRequest(file, preset, fromFolder) {
  return {
    fileName: file.name,
    kind: preset.kind,
    usage: preset.usage,
    role: preset.role,
    localId: fromFolder ? buildFolderLocalId(file, preset) : "",
    tags: fromFolder ? ["resourcelibrary-folder-import", preset.id] : [preset.id],
    bytesBase64: await readFileAsBase64(file)
  };
}

async function stageImportFiles(files) {
  const stagedFiles = [];
  for (let i = 0; i < files.length; i++) {
    const file = files[i];
    state.lastActionMessage = `扫描文件夹：${i + 1} / ${files.length}`;
    renderStatus();
    const ignored = isIgnoredImportFile(file);
    stagedFiles.push({
      fileName: file.name || "",
      relativePath: getImportDisplayPath(file),
      sizeBytes: file.size || 0,
      bytesBase64: ignored ? "" : await readFileAsBase64(file)
    });
  }

  return postJson(API.stageImport, {
    package: state.packageRelative,
    sourceRootLabel: "browser-folder",
    files: stagedFiles
  });
}

function isImportableStagedItem(item) {
  return item?.sourceProviderId === "externalImportStaging"
    && item?.runtimeAvailability === "NotRuntimeLoadable"
    && item?.importStatus === "New"
    && stringValue(pick(item.metadata, "selectable")) === "true"
    && stringValue(pick(item.metadata, "supported")) === "true";
}

function isStagedItemSupportedByPreset(files, item, preset) {
  const file = findStagedSourceFile(files, item);
  return Boolean(file && isFileSupportedByPreset(file, preset));
}

function findStagedSourceFile(files, staged) {
  const relativePath = stringValue(pick(staged.metadata, "relativePath")) || staged.sourcePath || "";
  return files.find(file => getImportDisplayPath(file) === relativePath || file.name === relativePath || file.name === staged.displayName);
}

async function buildImportRequestFromStagedItem(file, staged, preset) {
  const detectedKind = stringValue(pick(staged.metadata, "detectedKind")) || staged.kind || "";
  const kind = preset?.kind || detectedKind || "config";
  const usage = preset?.usage || stringValue(pick(staged.metadata, "detectedUsage")) || staged.usage || "characterConfig";
  const role = preset?.role || inferStagedRole(kind, usage);
  const tags = ["resourcelibrary-folder-import", preset?.id || `auto-${kind}`];
  if (detectedKind && detectedKind !== kind) {
    tags.push(`detected-${detectedKind}`);
  }

  return {
    fileName: file.name,
    kind,
    usage,
    role,
    localId: buildFolderLocalId(file, preset || { id: kind }),
    tags,
    bytesBase64: await readFileAsBase64(file)
  };
}

function inferStagedRole(kind, usage) {
  if (kind !== "model") return "";
  if (usage === "characterModel") return "body";
  if (usage === "weaponModel") return "preview";
  return "preview";
}


function renderFilters() {
  const items = getBrowserItems();
  setSelectOptions(el.kindFilter, buildOptions(items, "kind"), state.filters.kind);
  setSelectOptions(el.usageFilter, buildOptions(items, "usage"), state.filters.usage);
  setSelectOptions(el.providerFilter, buildOptions(items, "providerId"), state.filters.providerId);
  setSelectOptions(el.sourceFilter, buildOptions(items, "sourceKind"), state.filters.sourceKind);
  setSelectOptions(el.importFilter, buildOptions(items, "importStatus"), state.filters.importStatus);
  setSelectOptions(el.runtimeFilter, buildOptions(items, "runtimeAvailability"), state.filters.runtimeAvailability);
  setSelectOptions(el.tagFilter, buildTagOptions(items), state.filters.tag);
  applyFilterControls();
  renderFilterQuickBar();
  renderActiveFiltersBar();
}

function resetFilters() {
  state.filters = { ...FILTER_DEFAULTS };
  state.quickFilter = "";
  state.selectedTreeNodeId = getTreeRootId();
  applyFilterControls();
}

function applyQuickFilter(preset) {
  if (preset === "all") {
    resetFilters();
    return;
  }
  resetFilters();
  state.quickFilter = preset;
  if (preset === "needsProfile") {
    state.filters.profileMembership = "notInProfile";
    state.filters.runtimeReady = "runtimeReady";
  } else if (preset === "profileDraft") {
    state.filters.profileMembership = "all";
  } else if (preset === "runtimeReady") {
    state.filters.runtimeReady = "runtimeReady";
  } else if (preset === "hasDiagnostics") {
    state.filters.onlyDiagnostics = true;
  }
  applyFilterControls();
}

function clearFilterKey(key) {
  state.quickFilter = "";
  if (key === "search") state.filters.search = "";
  else if (key === "quickFilter") state.quickFilter = "";
  else if (key in FILTER_DEFAULTS) state.filters[key] = FILTER_DEFAULTS[key];
  applyFilterControls();
}

function renderFilterQuickBar() {
  document.querySelectorAll(".filter-quick-bar [data-quick-filter]").forEach(button => {
    button.classList.toggle("active", button.dataset.quickFilter === (state.quickFilter || "all"));
  });
}

function getActiveFilterLabels() {
  const labels = [];
  if (state.quickFilter && state.quickFilter !== "all") {
    const quickLabels = {
      needsProfile: "待加入 Profile",
      profileDraft: "Profile 草稿变更",
      runtimeReady: "运行时候选",
      hasDiagnostics: "有诊断"
    };
    labels.push({ key: "quickFilter", text: quickLabels[state.quickFilter] || state.quickFilter });
  }
  if (state.filters.search.trim()) labels.push({ key: "search", text: `搜索: ${state.filters.search.trim()}` });
  if (state.filters.kind !== "all") labels.push({ key: "kind", text: `类型: ${state.filters.kind}` });
  if (state.filters.usage !== "all") labels.push({ key: "usage", text: `用途: ${state.filters.usage}` });
  if (state.filters.providerId !== "all") labels.push({ key: "providerId", text: `来源: ${state.filters.providerId}` });
  if (state.filters.sourceKind !== "all") labels.push({ key: "sourceKind", text: `源: ${state.filters.sourceKind}` });
  if (state.filters.importStatus !== "all") labels.push({ key: "importStatus", text: `导入: ${state.filters.importStatus}` });
  if (state.filters.runtimeAvailability !== "all") labels.push({ key: "runtimeAvailability", text: `运行时: ${state.filters.runtimeAvailability}` });
  if (state.filters.profileMembership !== "all") labels.push({ key: "profileMembership", text: `Profile: ${formatProfileStatusLabel(state.filters.profileMembership)}` });
  if (state.filters.runtimeReady !== "all") labels.push({ key: "runtimeReady", text: state.filters.runtimeReady === "runtimeReady" ? "运行时候选" : "非运行时候选" });
  if (state.filters.tag !== "all") labels.push({ key: "tag", text: `标签: ${state.filters.tag}` });
  if (state.filters.onlyReferenced) labels.push({ key: "onlyReferenced", text: "已引用" });
  if (state.filters.onlyOrphan) labels.push({ key: "onlyOrphan", text: "orphan" });
  if (state.filters.onlyRuntimeLoadable) labels.push({ key: "onlyRuntimeLoadable", text: "可加载" });
  if (state.filters.onlyDiagnostics) labels.push({ key: "onlyDiagnostics", text: "有诊断" });
  return labels;
}

function renderActiveFiltersBar() {
  const labels = getActiveFilterLabels();
  const advancedCount = countAdvancedFiltersActive();
  if (advancedCount > 0) {
    el.advancedFilterBadge.textContent = String(advancedCount);
    el.advancedFilterBadge.classList.remove("hidden");
    if (el.advancedFilters && !el.advancedFilters.open) {
      el.advancedFilters.open = true;
    }
  } else {
    el.advancedFilterBadge.classList.add("hidden");
  }

  if (labels.length === 0) {
    el.activeFiltersBar.classList.add("hidden");
    el.activeFiltersBar.innerHTML = "";
    return;
  }

  el.activeFiltersBar.classList.remove("hidden");
  el.activeFiltersBar.innerHTML = `
    <span class="active-filters-label">已启用</span>
    ${labels.map(label => `<button type="button" class="filter-chip active removable" data-clear-filter="${escapeHtml(label.key)}">${escapeHtml(label.text)} ×</button>`).join("")}
    <button type="button" class="filter-chip secondary-chip" data-clear-filter="__all__">清除全部</button>`;
}

function countAdvancedFiltersActive() {
  let count = 0;
  if (state.filters.usage !== "all") count++;
  if (state.filters.sourceKind !== "all") count++;
  if (state.filters.importStatus !== "all") count++;
  if (state.filters.runtimeAvailability !== "all") count++;
  if (state.filters.runtimeReady !== "all") count++;
  if (state.filters.tag !== "all") count++;
  if (state.filters.onlyReferenced) count++;
  if (state.filters.onlyOrphan) count++;
  if (state.filters.onlyRuntimeLoadable) count++;
  if (state.filters.onlyDiagnostics) count++;
  return count;
}

function formatProfileStatusLabel(status) {
  const labels = {
    notInProfile: "未入 Profile",
    saved: "已保存",
    draftOnly: "草稿新增",
    removedInDraft: "草稿移除",
    modifiedInDraft: "草稿已改",
    inProfile: "已在 Profile"
  };
  return labels[status] || status || "-";
}

function formatRuntimeLabel(value) {
  const text = String(value || "");
  if (text === "RuntimeLoadable") return "可加载";
  if (text === "NotRuntimeLoadable") return "不可加载";
  if (text === "Unknown") return "未知";
  return text || "-";
}

function sortResourceItems(items) {
  const sorted = [...items];
  if (state.resourceSort === "diagnostics") {
    sorted.sort((a, b) => b.diagnosticCount - a.diagnosticCount || compareText(formatResourceDisplayName(a), formatResourceDisplayName(b)));
    return sorted;
  }
  if (state.resourceSort === "references") {
    sorted.sort((a, b) => b.referenceCount - a.referenceCount || compareText(formatResourceDisplayName(a), formatResourceDisplayName(b)));
    return sorted;
  }
  if (state.resourceSort === "profile") {
    sorted.sort((a, b) => {
      const left = PROFILE_STATUS_ORDER[getBuildProfileStatus(a)] ?? 0;
      const right = PROFILE_STATUS_ORDER[getBuildProfileStatus(b)] ?? 0;
      return left - right || compareText(formatResourceDisplayName(a), formatResourceDisplayName(b));
    });
    return sorted;
  }
  sorted.sort((a, b) => compareText(formatResourceDisplayName(a), formatResourceDisplayName(b)));
  return sorted;
}

function getTreeRootId() {
  return state.treeGroupMode === "taxonomy" ? "tax::__root__" : "path::__root__";
}

function getResourcePathSegments(item) {
  const raw = stringValue(item.sourcePath) || stringValue(item.unityAssetPath) || "";
  const normalized = raw.replace(/\\/g, "/").replace(/^\/+/, "");
  if (!normalized) return ["(未分类路径)"];
  const parts = normalized.split("/").filter(Boolean);
  if (parts.length === 0) return ["(未分类路径)"];
  const last = parts[parts.length - 1];
  if (/\.[a-z0-9]{1,8}$/i.test(last) && parts.length > 1) {
    return parts.slice(0, -1);
  }
  return parts;
}

function createTreeNode(id, label, kind, itemKeys = []) {
  return { id, label, kind, itemKeys: [...itemKeys], children: [] };
}

function buildResourceTree(items) {
  if (state.treeGroupMode === "taxonomy") {
    return buildTaxonomyTree(items);
  }
  return buildPathTree(items);
}

function buildPathTree(items) {
  const root = createTreeNode(getTreeRootId(), "全部资源", "root");
  const folderMap = new Map([[root.id, root]]);

  for (const item of items) {
    const segments = getResourcePathSegments(item);
    let parent = root;
    let pathSoFar = "";
    for (const segment of segments) {
      pathSoFar = pathSoFar ? `${pathSoFar}/${segment}` : segment;
      const id = `path::${pathSoFar}`;
      let node = folderMap.get(id);
      if (!node) {
        node = createTreeNode(id, segment, "folder");
        parent.children.push(node);
        folderMap.set(id, node);
      }
      parent = node;
    }
    parent.itemKeys.push(item.key);
  }

  sortTreeChildren(root);
  return root;
}

function buildTaxonomyTree(items) {
  const root = createTreeNode(getTreeRootId(), "全部资源", "root");
  const providerMap = new Map();

  for (const item of items) {
    const providerId = item.providerId || "unknown";
    const kind = item.kind || "unknown";
    const usage = item.usage || "-";
    let provider = providerMap.get(providerId);
    if (!provider) {
      provider = createTreeNode(`tax::${providerId}`, providerId, "provider");
      root.children.push(provider);
      providerMap.set(providerId, provider);
    }
    let kindNode = provider.children.find(child => child.label === kind);
    if (!kindNode) {
      kindNode = createTreeNode(`tax::${providerId}::${kind}`, kind, "kind");
      provider.children.push(kindNode);
    }
    let usageNode = kindNode.children.find(child => child.label === usage);
    if (!usageNode) {
      usageNode = createTreeNode(`tax::${providerId}::${kind}::${usage}`, usage, "usage");
      kindNode.children.push(usageNode);
    }
    usageNode.itemKeys.push(item.key);
  }

  sortTreeChildren(root);
  return root;
}

function sortTreeChildren(node) {
  node.children.sort((a, b) => compareText(a.label, b.label));
  for (const child of node.children) sortTreeChildren(child);
}

function indexResourceTree(root) {
  const nodeKeys = new Map();
  function walk(node) {
    const keys = new Set(node.itemKeys || []);
    for (const child of node.children) {
      walk(child);
      const childKeys = nodeKeys.get(child.id);
      if (childKeys) {
        for (const key of childKeys) keys.add(key);
      }
    }
    nodeKeys.set(node.id, keys);
    node.count = keys.size;
    return keys;
  }
  walk(root);
  state.treeNodeKeys = nodeKeys;
  return root;
}

function getTreeScopedItems(items) {
  const rootId = getTreeRootId();
  if (!state.selectedTreeNodeId || state.selectedTreeNodeId === rootId) return items;
  const keys = state.treeNodeKeys.get(state.selectedTreeNodeId);
  if (!keys || keys.size === 0) return items;
  return items.filter(item => keys.has(item.key));
}

function findTreeNode(root, nodeId) {
  if (root.id === nodeId) return root;
  for (const child of root.children) {
    const found = findTreeNode(child, nodeId);
    if (found) return found;
  }
  return null;
}

function findTreeNodeIdForItem(root, itemKey) {
  function walk(node) {
    for (const child of node.children) {
      const childHit = walk(child);
      if (childHit) return childHit;
    }
    if ((node.itemKeys || []).includes(itemKey)) return node.id;
    return "";
  }
  return walk(root) || root.id;
}

function ensureTreeExpansionForNode(nodeId, root) {
  if (!nodeId || nodeId === root.id) {
    state.expandedTreeNodes.add(root.id);
    return;
  }
  if (state.treeGroupMode === "path") {
    state.expandedTreeNodes.add("path::__root__");
    if (nodeId === "path::__root__") return;
    const parts = nodeId.replace(/^path::/, "").split("/");
    let pathSoFar = "";
    for (const part of parts) {
      pathSoFar = pathSoFar ? `${pathSoFar}/${part}` : part;
      state.expandedTreeNodes.add(`path::${pathSoFar}`);
    }
    return;
  }
  state.expandedTreeNodes.add("tax::__root__");
  const rest = nodeId.replace(/^tax::/, "");
  const parts = rest.split("::");
  if (parts.length >= 1) state.expandedTreeNodes.add(`tax::${parts[0]}`);
  if (parts.length >= 2) state.expandedTreeNodes.add(`tax::${parts[0]}::${parts[1]}`);
  if (parts.length >= 3) state.expandedTreeNodes.add(nodeId);
}

function ensureDefaultTreeExpansion(root) {
  if (state.expandedTreeNodes.size > 0) return;
  state.expandedTreeNodes.add(root.id);
  for (const child of root.children) {
    state.expandedTreeNodes.add(child.id);
  }
}

function expandAllTreeNodes() {
  for (const nodeId of state.treeNodeKeys.keys()) {
    state.expandedTreeNodes.add(nodeId);
  }
}

function getTreeNodeLabel(node) {
  if (node.kind === "provider") return `来源 · ${node.label}`;
  if (node.kind === "kind") return `类型 · ${node.label}`;
  if (node.kind === "usage") return `用途 · ${node.label}`;
  return node.label;
}

function renderResourceTree(root, filteredCount) {
  ensureDefaultTreeExpansion(root);
  if (state.selectedResourceKey) {
    const nodeId = findTreeNodeIdForItem(root, state.selectedResourceKey);
    state.selectedTreeNodeId = state.selectedTreeNodeId || nodeId;
    ensureTreeExpansionForNode(nodeId, root);
  }
  if (!state.selectedTreeNodeId) {
    state.selectedTreeNodeId = root.id;
  }

  el.resourceTree.innerHTML = renderTreeNodeHtml(root, 0);
  el.treeGroupModeSelect.value = state.treeGroupMode;

  const selectedNode = findTreeNode(root, state.selectedTreeNodeId) || root;
  const scopedCount = getTreeScopedItems(sortResourceItems(getFilteredItems(getNormalizedItems()))).length;
  el.resourceListHeading.textContent = `${getTreeNodeLabel(selectedNode)} · ${scopedCount} 项`;
}

function renderTreeNodeHtml(node, depth) {
  const hasChildren = node.children.length > 0;
  const expanded = state.expandedTreeNodes.has(node.id);
  const selected = state.selectedTreeNodeId === node.id;
  const count = node.count ?? (node.itemKeys?.length || 0);
  const diagCount = countDiagnosticsInTreeNode(node);
  const childrenHtml = hasChildren && expanded
    ? node.children.map(child => renderTreeNodeHtml(child, depth + 1)).join("")
    : "";

  return `
    <div class="tree-node" role="treeitem" aria-expanded="${hasChildren ? expanded : undefined}" data-depth="${depth}">
      <div class="tree-node-row${selected ? " active" : ""}" data-tree-node-id="${escapeHtml(node.id)}">
        <button type="button" class="tree-toggle${hasChildren ? "" : " spacer"}" data-tree-toggle="${escapeHtml(node.id)}"${hasChildren ? ` aria-label="${expanded ? "折叠" : "展开"}"` : " tabindex=\"-1\" aria-hidden=\"true\""}>
          ${hasChildren ? (expanded ? "▾" : "▸") : ""}
        </button>
        <button type="button" class="tree-label" data-tree-select="${escapeHtml(node.id)}">
          <span class="tree-icon" aria-hidden="true">${treeNodeIcon(node)}</span>
          <span class="tree-text" title="${escapeHtml(getTreeNodeLabel(node))}">${escapeHtml(getTreeNodeLabel(node))}</span>
          <span class="tree-count">${count}</span>
          ${diagCount > 0 ? `<span class="tree-diag">${diagCount}</span>` : ""}
        </button>
      </div>
      ${childrenHtml ? `<div class="tree-children" role="group">${childrenHtml}</div>` : ""}
    </div>`;
}

function treeNodeIcon(node) {
  if (node.kind === "root") return "⌂";
  if (node.kind === "provider") return "◎";
  if (node.kind === "kind") return "▣";
  if (node.kind === "usage") return "◦";
  return "▣";
}

function countDiagnosticsInTreeNode(node) {
  const keys = state.treeNodeKeys.get(node.id);
  if (!keys || keys.size === 0) return 0;
  let total = 0;
  for (const key of keys) {
    total += state.browserItemsByKey.get(key)?.diagnosticCount || 0;
  }
  return total;
}

function handleResourceTreeClick(event) {
  const toggle = event.target.closest("[data-tree-toggle]");
  if (toggle) {
    const nodeId = toggle.dataset.treeToggle;
    if (state.expandedTreeNodes.has(nodeId)) state.expandedTreeNodes.delete(nodeId);
    else state.expandedTreeNodes.add(nodeId);
    renderBrowser();
    return;
  }
  const select = event.target.closest("[data-tree-select]");
  if (!select) return;
  state.selectedTreeNodeId = select.dataset.treeSelect;
  renderBrowser();
  renderBrowseContext();
}

function renderBrowser() {
  const sourceItems = getNormalizedItems();
  const allItems = getBrowserItems();
  state.browserItemsByKey = new Map(allItems.map(item => [item.key, item]));
  const filtered = sortResourceItems(getFilteredItems(allItems));
  const treeRoot = indexResourceTree(buildResourceTree(filtered));
  const scoped = getTreeScopedItems(filtered);
  const selectedKey = state.selectedResourceKey;
  const diagnosticsCount = getAllDiagnostics().length;
  pruneCheckedResources(allItems);
  const visibleChecked = scoped.filter(item => state.checkedResourceKeys.has(item.key)).length;
  const foldedCount = Math.max(0, sourceItems.length - allItems.length);

  el.resourceSummary.textContent = allItems.length === 0
    ? "没有读取到 Authoring Resource Manager API 数据。"
    : `${filtered.length} / ${allItems.length} 展示资源 · ${sourceItems.length} 原始项${foldedCount > 0 ? ` · 已折叠 ${foldedCount}` : ""} · ${scoped.length} 当前目录 · ${diagnosticsCount} 诊断`;
  el.checkedSummary.textContent = `已勾选 ${state.checkedResourceKeys.size} · 当前目录 ${visibleChecked} 可见`;
  el.selectVisibleButton.disabled = scoped.length === 0;
  el.clearCheckedButton.disabled = state.checkedResourceKeys.size === 0;

  if (allItems.length === 0) {
    el.resourceTree.innerHTML = emptyBlock("暂无目录");
    el.resourceList.innerHTML = emptyBlock("未读取到资源项。请确认 Authoring 服务已启动，并且至少一个 provider 可用。");
    return;
  }

  renderResourceTree(treeRoot, filtered.length);

  if (filtered.length === 0) {
    el.resourceList.innerHTML = emptyBlock("没有符合当前筛选条件的资源项。");
    return;
  }
  if (scoped.length === 0) {
    el.resourceList.innerHTML = emptyBlock("当前目录下没有资源，请选择其他目录节点。");
    return;
  }

  el.resourceList.innerHTML = scoped.map(item => {
    const active = item.key === selectedKey ? " active" : "";
    const checked = state.checkedResourceKeys.has(item.key);
    const profileStatus = getBuildProfileStatus(item);
    const profileLabel = formatProfileStatusLabel(profileStatus);
    const runtimeLabel = formatRuntimeLabel(item.runtimeAvailability);
    const deliveryState = getDeliveryEntryState(item, findDraftBuildProfileEntryForItem(item));
    const displayName = formatResourceDisplayName(item);
    const identity = formatResourceIdentity(item);
    const typeLine = `${item.kind || "unknown"} · ${item.usage || "-"}${item.foldedCount > 1 ? ` · ${item.foldedCount} 个身份` : ""}`;
    const provider = formatResourceProviderStack(item);
    const runtimeTone = toneForRuntime(item.runtimeAvailability);
    const profileTone = toneForBuildProfileStatus(profileStatus);
    const diagWarn = item.diagnosticCount > 0 ? " has-diagnostics" : "";
    return `
      <article class="resource-row${active}${checked ? " checked" : ""}${diagWarn}" role="option" aria-selected="${item.key === selectedKey}">
        <label class="resource-check" title="勾选用于批量 Profile 操作">
          <input type="checkbox" data-check-resource-key="${escapeHtml(item.key)}"${checked ? " checked" : ""}>
          <span>选择</span>
        </label>
        <button class="resource-row-main" type="button" data-resource-key="${escapeHtml(item.key)}">
          <div class="resource-row-top">
            <strong class="resource-name" title="${escapeHtml(displayName)}">${escapeHtml(displayName)}</strong>
            <span class="resource-inline-metrics">
              ${item.referenceCount > 0 ? `<span class="metric-pill" title="引用">引 ${item.referenceCount}</span>` : ""}
              ${item.diagnosticCount > 0 ? `<span class="metric-pill warn" title="诊断">诊 ${item.diagnosticCount}</span>` : ""}
            </span>
          </div>
          <div class="resource-row-sub">${escapeHtml(typeLine)} · ${escapeHtml(provider)}</div>
          <div class="resource-row-badges">
            <span class="status-pill ${profileTone}" title="Profile 状态">${escapeHtml(profileLabel)}</span>
            <span class="status-pill ${runtimeTone}" title="运行时">${escapeHtml(runtimeLabel)}</span>
            <span class="status-pill neutral" title="交付结果">${escapeHtml(deliveryState.label)}</span>
          </div>
          <div class="resource-row-id" title="${escapeHtml(identity)}">${escapeHtml(identity)}</div>
        </button>
      </article>`;
  }).join("");
}

function selectVisibleResources() {
  const filtered = sortResourceItems(getFilteredItems(getBrowserItems()));
  const visibleItems = getTreeScopedItems(filtered);
  for (const item of visibleItems) {
    state.checkedResourceKeys.add(item.key);
  }
  state.lastActionMessage = `已选择当前目录可见 ${visibleItems.length} 个资源。`;
  render();
}

function toggleCheckedResource(key, checked) {
  if (checked) {
    state.checkedResourceKeys.add(key);
  } else {
    state.checkedResourceKeys.delete(key);
  }
  render();
}

function pruneCheckedResources(items) {
  const valid = new Set(items.map(item => item.key));
  for (const key of Array.from(state.checkedResourceKeys)) {
    if (!valid.has(key)) state.checkedResourceKeys.delete(key);
  }
}

function getCheckedItems() {
  const byKey = new Map(getBrowserItems().map(item => [item.key, item]));
  return Array.from(state.checkedResourceKeys).map(key => byKey.get(key)).filter(Boolean);
}

function renderBrowseContext() {
  renderWorkflowAssistant(el.browseWorkflowAssistant, getBrowseGuideState());
  const item = getSelectedItem();
  const checkedCount = state.checkedResourceKeys.size;

  if (!item) {
    el.browsePanelTitle.textContent = "资源概览";
    el.browsePanelSubtitle.textContent = "点击左侧列表选择资源";
    const items = getBrowserItems();
    const needsProfile = items.filter(entry => getBuildProfileStatus(entry) === "notInProfile" && isRuntimeReadyCandidate(entry)).length;
    el.browseContextBody.innerHTML = `
      <div class="browse-overview-grid">
        ${metric("全部资源", items.length)}
        ${metric("待加入 Profile", needsProfile)}
        ${metric("有诊断", items.filter(entry => entry.diagnosticCount > 0).length)}
        ${metric("Profile", state.buildProfileDirty ? "草稿未保存" : "已同步")}
      </div>
      <p class="profile-hint workflow-hint">推荐流程：筛选候选 → 加入 Profile → 保存 → Unity 构建 → Offline 验证</p>
      <p class="profile-hint">常用筛选：「待加入 Profile」可快速列出 runtime-ready 且未入 Profile 的资源。</p>`;
    el.browseBatchBar.classList.add("hidden");
    el.browseBatchBar.innerHTML = "";
    return;
  }

  const profileStatus = getBuildProfileStatus(item);
  const profileLabel = formatProfileStatusLabel(profileStatus);
  const deliveryState = getDeliveryEntryState(item, findDraftBuildProfileEntryForItem(item));
  const inProfile = Boolean(findDraftBuildProfileEntryForItem(item));
  el.browsePanelTitle.textContent = formatResourceDisplayName(item);
  el.browsePanelSubtitle.textContent = `${item.kind || "unknown"} · ${item.usage || "-"} · ${deliveryState.label}`;

  const nextStep = !inProfile
    ? "下一步：加入 Profile，并在 Profile 工作区配置交付方式。"
    : state.buildProfileDirty
      ? "下一步：保存 Profile，再到 Build 工作区确认 Bundle Plan。"
      : "下一步：到 Build 查看 Bundle Plan，或去 Unity 构建。";

  el.browseContextBody.innerHTML = `
    <div class="browse-status-strip">
      <span class="status-pill ${toneForBuildProfileStatus(profileStatus)}">${escapeHtml(profileLabel)}</span>
      <span class="status-pill ${toneForRuntime(item.runtimeAvailability)}">${escapeHtml(formatRuntimeLabel(item.runtimeAvailability))}</span>
      <span class="status-pill ${deliveryState.tone}">${escapeHtml(deliveryState.label)}</span>
    </div>
    <div class="browse-overview-grid">
      ${metric("引用", item.referenceCount)}
      ${metric("诊断", item.diagnosticCount)}
      ${metric("provider", item.providerId || "-")}
      ${metric("导入", item.importStatus || "-")}
    </div>
    <div class="detail-card compact-card identity-card">
      <h3>资源身份</h3>
      ${renderKeyValueList([
        ["canonicalName", formatResourceDisplayName(item)],
        ["technicalIdentity", formatResourceIdentity(item)],
        ["foldedIdentities", item.foldedCount > 1 ? `${item.foldedCount} 个同源身份` : "1"],
        ["resourceKey", item.resourceKey || "-"],
        ["stableId", item.stableId || "-"],
        ["sourcePath", item.sourcePath || "-"],
        ["unityAssetPath", item.unityAssetPath || "-"]
      ])}
    </div>
    ${renderPublicResourceLifecyclePanel(item)}
    <p class="profile-hint workflow-hint">${escapeHtml(nextStep)}</p>
    <div class="context-actions inline-actions browse-inline-links">
      <button type="button" data-action="open-references-current" class="secondary">查看引用关系</button>
      <button type="button" data-action="view-debug" class="secondary">Debug 原始详情</button>
    </div>`;

  if (checkedCount > 0) {
    el.browseBatchBar.classList.remove("hidden");
    el.browseBatchBar.innerHTML = `
      <span class="batch-bar-label">已选择 ${checkedCount} 个资源</span>
      ${renderBundleActionSelect("checkedBundleTarget")}
      <button type="button" data-action="batch-assign-bundle" class="primary-action">批量加入 Bundle</button>
      <button type="button" data-action="batch-clear-bundle" class="secondary">批量移出 Bundle</button>
      <button type="button" data-action="batch-add-profile" class="secondary">批量加入 Profile</button>
      <button type="button" data-action="batch-remove-profile" class="secondary">批量移出 Profile</button>`;
  } else {
    el.browseBatchBar.classList.add("hidden");
    el.browseBatchBar.innerHTML = "";
  }
}


function renderContextActions() {
  const item = getSelectedItem();
  const preset = getSelectedImportPreset();
  const connected = Boolean(state.resourcesPayload);
  const writeBusy = state.writeState.status === "running";
  const profileReady = Boolean(getBuildProfile());
  const profileEntry = item ? findDraftBuildProfileEntryForItem(item) : null;
  const checkedItems = getCheckedItems();
  const checkedAddable = checkedItems.some(checkedItem => !findDraftBuildProfileEntryForItem(checkedItem));
  const checkedRemovable = checkedItems.some(checkedItem => Boolean(findDraftBuildProfileEntryForItem(checkedItem)));
  const bundleAssignable = item ? canAssignItemToInternalBundle(item) : false;
  const returnAction = renderCharacterStudioReturnAction(item);

  el.importResourceButton.disabled = !connected || writeBusy;
  el.importFolderButton.disabled = !connected || writeBusy;
  el.reimportResourceButton.disabled = !connected || !item || writeBusy;
  el.replaceSourceButton.disabled = !connected || !item || writeBusy;
  el.saveBuildProfileButton.disabled = !profileReady || writeBusy;
  el.saveBuildProfileButton.title = "通过 Authoring API 校验并保存 Global Resource Build Profile";

  el.browseContextActions.innerHTML = item
    ? `${returnAction}${renderBrowsePrimaryActions(item, profileReady, writeBusy, profileEntry, bundleAssignable)}`
    : `${returnAction}
      <button type="button" data-action="show-candidates" class="primary-action">筛选推荐候选</button>
      <span class="action-group-label">选择资源后这里会显示下一步主动作</span>`;

  el.profileContextActions.innerHTML = `
    <span class="action-group-label">Profile 操作</span>
    ${renderBundleActionSelect("profileSelectedBundleTarget")}
    <button type="button" data-action="assign-selected-bundle"${!item || !profileReady || writeBusy || !bundleAssignable ? " disabled" : ""}>当前资源加入 Bundle</button>
    <button type="button" data-action="add-profile"${!item || profileEntry || !profileReady || writeBusy ? " disabled" : ""}>仅加入 Profile</button>
    <button type="button" data-action="remove-profile"${!item || !profileEntry || writeBusy ? " disabled" : ""}>移出 Profile</button>
    <button type="button" data-action="revert-profile" class="secondary"${!item || writeBusy ? " disabled" : ""}>还原当前资源草稿</button>
  `;

  if (state.writeState.status === "running") {
    el.writeActionStatus.textContent = "写入中：正在通过 Authoring API 更新资源库。";
  } else if (state.writeState.status === "error") {
    el.writeActionStatus.textContent = `写入失败：${state.writeState.error}`;
  } else if (item) {
    el.writeActionStatus.textContent = `导入类型：${preset.label}；目标：${formatResourceDisplayName(item) || getResourceWriteId(item)}；Profile：${getBuildProfileStatus(item)}。`;
  } else {
    el.writeActionStatus.textContent = `导入类型：${preset.label}。`;
  }

  el.copyDetailJsonButton.disabled = false;
  el.copyDiagnosticsJsonButton.disabled = false;
  el.copyDetailJsonButton.title = item ? "复制当前资源 inspect/fallback 详情" : "复制当前资源库摘要";
  el.copyDiagnosticsJsonButton.title = item ? "复制当前资源诊断" : "复制当前资源库全部诊断";

  void checkedAddable;
  void checkedRemovable;
}

function renderCharacterStudioReturnAction(item) {
  const fieldKey = state.characterStudioReturn?.fieldKey || "";
  if (!fieldKey) return "";
  const disabled = item ? "" : " disabled";
  const label = item
    ? `Bind to CharacterStudio: ${fieldKey}`
    : `Waiting for Resource Manager selection: ${fieldKey}`;
  return `<button type="button" data-action="return-characterstudio" class="primary-action"${disabled}>${escapeHtml(label)}</button>`;
}

function renderPublicResourceLifecyclePanel(item) {
  if (!item || !isCommonResourceItem(item)) return "";
  const commonItem = findCommonResourceItemForResource(item);
  const summary = state.commonResources?.summary || {};
  const resourceKey = item.resourceKey || commonItem?.resourceKey || "";
  const diagnostic = commonItem?.modelScaleDiagnostic || {};
  const importHints = commonItem?.importHints || {};
  const poseScale = importHints.modelWrapperPose?.scale || {};
  const currentScale = Number(diagnostic.currentScale || poseScale.x || poseScale.y || poseScale.z || 1);
  const recommendedScale = Number(diagnostic.recommendedScale || 0);
  const scaleValue = Number.isFinite(currentScale) && currentScale > 0 ? currentScale : 1;
  const saveBusy = state.commonResourceSaveState?.status === "running";
  const syncBusy = state.commonResourcesSyncing;
  const status = commonItem?.status || item.importStatus || "Unknown";
  const message = commonItem?.message || "Public resource lifecycle is owned by Resource Manager.";
  return `<div class="detail-card compact-card public-resource-card">
    <h3>Public Resource Lifecycle</h3>
    <div class="browse-overview-grid">
      ${metric("status", status)}
      ${metric("ready", summary.ready || summary.synced || 0)}
      ${metric("changed", summary.changed || summary.dirty || 0)}
      ${metric("unity", summary.unityPending || summary.needsUnityImport || 0)}
    </div>
    <p class="profile-hint">${escapeHtml(message)}</p>
    <label class="profile-field-inline">
      <span>Wrapper scale</span>
      <input type="number" min="0.0001" step="0.0001" value="${escapeHtml(String(scaleValue))}" data-common-resource-scale="${escapeHtml(resourceKey)}">
    </label>
    ${recommendedScale > 0 ? `<p class="profile-hint">Recommended scale: ${escapeHtml(String(recommendedScale))}</p>` : ""}
    <div class="context-actions inline-actions">
      <button type="button" data-action="sync-common-resources" class="secondary"${syncBusy ? " disabled" : ""}>Sync public resources</button>
      <button type="button" data-action="save-common-resource-scale" data-resource-key="${escapeHtml(resourceKey)}" class="secondary"${!commonItem || saveBusy ? " disabled" : ""}>Save import hints</button>
    </div>
  </div>`;
}

function renderBrowsePrimaryActions(item, profileReady, writeBusy, profileEntry, bundleAssignable) {
  const primary = getBrowsePrimaryAction(item, profileReady, writeBusy, profileEntry, bundleAssignable);
  const primaryButton = `
    <button type="button" data-action="${escapeHtml(primary.action)}" class="primary-action"${primary.disabled ? " disabled" : ""} title="${escapeHtml(primary.hint)}">
      ${escapeHtml(primary.label)}
    </button>`;
  const bundleSelect = primary.showBundleSelect ? renderBundleActionSelect("selectedBundleTarget") : "";
  return `
    <div class="browse-primary-action-row">
      ${bundleSelect}
      ${primaryButton}
    </div>
    <span class="action-helper ${primary.tone || "neutral"}">${escapeHtml(primary.hint)}</span>
    <details class="browse-more-actions">
      <summary>更多操作</summary>
      <button type="button" data-action="add-profile" class="secondary"${profileEntry || !profileReady || writeBusy ? " disabled" : ""}>仅加入 Profile</button>
      <button type="button" data-action="open-references-current" class="secondary"${!item ? " disabled" : ""}>查看引用关系</button>
      <button type="button" data-action="open-graph-current" class="secondary"${!item ? " disabled" : ""}>查看关系图</button>
      <button type="button" data-action="open-profile" class="secondary"${!profileReady || writeBusy ? " disabled" : ""}>编辑 Profile</button>
      <button type="button" data-action="view-debug" class="secondary">Debug 详情</button>
      <button type="button" data-action="remove-profile" class="secondary"${!profileEntry || writeBusy ? " disabled" : ""}>移出 Profile</button>
    </details>`;
}

function getBrowsePrimaryAction(item, profileReady, writeBusy, profileEntry, bundleAssignable) {
  if (writeBusy) {
    return { action: "open-profile", label: "写入中", hint: "Authoring API 正在处理写入，请稍后。", disabled: true, tone: "warn" };
  }
  if (!profileReady) {
    return { action: "open-profile", label: "等待 Profile", hint: "Authoring Server 尚未返回 Global Resource Build Profile。", disabled: true, tone: "warn" };
  }
  const entryMode = profileEntry ? pick(profileEntry, "deliveryMode") || "internal" : "";
  const needsBundle = bundleAssignable && (!profileEntry || (entryMode === "internal" && !pick(profileEntry, "bundleRule")));
  if (needsBundle) {
    const hasBundle = getBuildProfileBundleRules().length > 0;
    return {
      action: hasBundle ? "assign-selected-bundle" : "open-profile",
      label: hasBundle ? "加入到 Bundle" : "缺少 Bundle policy",
      hint: hasBundle ? "选择目标 Bundle 后加入；这会自动加入 Profile 草稿。" : "还没有 Bundle policy；请先通过所属 policy producer 提交规则。",
      disabled: false,
      showBundleSelect: hasBundle,
      tone: hasBundle ? "ok" : "warn"
    };
  }
  if (!profileEntry) {
    return {
      action: "add-profile",
      label: "加入 Profile",
      hint: bundleAssignable ? "先加入 Profile 草稿；之后仍需分配 Bundle。" : `作为配置引用加入 Profile。${describeBundleAssignBlocker(item)}`,
      disabled: false,
      tone: bundleAssignable ? "warn" : "info"
    };
  }
  if (state.buildProfileDirty) {
    return { action: "save-profile", label: "保存 Profile", hint: "当前 Profile 有未保存草稿，保存后 Build 才会使用。", disabled: false, tone: "warn" };
  }
  return { action: "open-build", label: "查看 Build", hint: "Profile 已保存，去 Build 检查 Bundle Plan 和生成产物。", disabled: false, tone: "ok" };
}

function getDeliveryEntryState(item, entry) {
  const mode = entry ? pick(entry, "deliveryMode") : "notInProfile";
  if (!entry) return { label: "未加入 Profile", tone: "neutral" };
  if (mode === "external") return { label: "外部配置", tone: "warn" };
  if (mode === "editorOnly") return { label: "仅编辑器", tone: "info" };
  if (mode === "excluded") return { label: "不构建", tone: "error" };
  if (mode === "internal") {
    const ruleId = pick(entry, "bundleRule") || "";
    if (!ruleId) return { label: "需要 Bundle", tone: "warn" };
    if (!findBundleRuleById(ruleId)) return { label: "Bundle 缺失", tone: "error" };
  }
  return { label: "将构建", tone: "ok" };
}

function inferProfileValidationField(payload) {
  const issues = asArray(pick(pick(payload, "validation"), "issues"));
  const message = issues.map(issue => String(pick(issue, "message") || pick(issue, "code") || "")).join(" ").toLowerCase();
  if (message.includes("resourcekey") || message.includes("invalid characters")) return "resourceKey";
  if (message.includes("bundle rule")) return "bundleRule";
  if (message.includes("delivery")) return "deliveryMode";
  return "";
}

function renderImportPresetOptions() {
  const selected = IMPORT_PRESETS.some(preset => preset.id === state.selectedImportPreset)
    ? state.selectedImportPreset
    : IMPORT_PRESETS[0].id;
  state.selectedImportPreset = selected;
  el.importPresetSelect.innerHTML = IMPORT_PRESETS
    .map(preset => `<option value="${escapeHtml(preset.id)}"${preset.id === selected ? " selected" : ""}>${escapeHtml(preset.label)}</option>`)
    .join("");
  syncImportAccept();
}

function syncImportAccept() {
  const preset = getSelectedImportPreset();
  const accept = preset.extensions.map(extension => `.${extension}`).join(",");
  el.resourceImportFileInput.accept = accept;
  el.resourceImportFolderInput.accept = accept;
}

function getResourceWriteId(item) {
  return item?.libraryItemId || item?.stableId || item?.resourceKey || item?.key || "";
}

function selectionMatchesItem(item, selection) {
  if (asArray(item?.foldedItems).some(peer => peer !== item && selectionMatchesItem(peer, selection))) return true;
  return Boolean(
    item.key === selection ||
    item.libraryItemId === selection ||
    item.stableId === selection ||
    item.resourceKey === selection
  );
}

function getSelectedImportPreset() {
  return IMPORT_PRESETS.find(preset => preset.id === state.selectedImportPreset) || IMPORT_PRESETS[0];
}

function isFileSupportedByPreset(file, preset) {
  const format = inferFormatFromFileName(file.name);
  return preset.extensions.includes(format);
}

function isIgnoredImportFile(file) {
  const filePath = getImportDisplayPath(file);
  const name = String(file?.name || filePath).toLowerCase();
  if (!name) return true;
  if (name.endsWith(".meta") || name === ".ds_store" || name === "thumbs.db") return true;
  return filePath.split(/[\\/]+/).some(segment => segment.startsWith(".") && segment !== ".");
}

function formatFolderImportCountSuffix(skipped, ignored, preset) {
  const parts = [`类型 ${preset?.label || "自动识别"}`];
  if (skipped > 0) parts.push(`跳过非匹配 ${skipped}`);
  if (ignored > 0) parts.push(`忽略元数据 ${ignored}`);
  return parts.length > 0 ? `，${parts.join("，")}` : "";
}

function inferFormatFromFileName(fileName) {
  const value = String(fileName || "");
  const index = value.lastIndexOf(".");
  return index >= 0 ? value.slice(index + 1).toLowerCase() : "";
}

function inferModelRole(item) {
  if (!item || item.kind !== "model") return "";
  if (item.usage === "characterModel") return "body";
  if (item.tags.includes("mainHand") || item.resourceKey.includes("mainhand")) return "mainHand";
  if (item.tags.includes("offHand") || item.resourceKey.includes("offhand")) return "offHand";
  return "preview";
}

function buildFolderLocalId(file, preset) {
  const path = getImportDisplayPath(file).replace(/\.[^.]+$/, "");
  const segments = path.split(/[\\/]+/).map(normalizeLocalIdSegment).filter(Boolean);
  const suffix = segments.join(".");
  return suffix || "";
}

function normalizeLocalIdSegment(value) {
  return String(value || "")
    .trim()
    .replace(/([a-z0-9])([A-Z])/g, "$1-$2")
    .replace(/[^a-zA-Z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .toLowerCase();
}

function getImportDisplayPath(file) {
  return file.webkitRelativePath || file.name || "";
}

function readFileAsBase64(file) {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
      const result = String(reader.result || "");
      resolve(result.includes(",") ? result.split(",").pop() : result);
    };
    reader.onerror = () => reject(reader.error || new Error("读取文件失败"));
    reader.readAsDataURL(file);
  });
}
