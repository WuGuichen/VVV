// CharacterStudio single-weapon configuration workbench.
// Owns list/detail navigation, structured editing, overview management, and reference visibility.

const SINGLE_WEAPON_LAST_EDITED_STORAGE_KEY = "mx.characterStudio.singleWeapon.lastEditedId";

function readLastEditedSingleWeaponConfigurationId(storage = globalThis.localStorage) {
  try {
    return String(storage?.getItem?.(SINGLE_WEAPON_LAST_EDITED_STORAGE_KEY) || "");
  } catch {
    return "";
  }
}

function rememberLastEditedSingleWeaponConfigurationId(configurationId, storage = globalThis.localStorage) {
  if (!configurationId || configurationId === "weapon.unarmed") return;
  try {
    storage?.setItem?.(SINGLE_WEAPON_LAST_EDITED_STORAGE_KEY, configurationId);
  } catch {
    // Browser privacy modes may reject local storage. The in-memory selection remains authoritative.
  }
}

function selectPreferredSingleWeaponConfigurationId(items = [], requestedId = "", rememberedId = "", activeId = "") {
  const ids = new Set(items.map(item => item?.singleWeaponConfigurationId).filter(Boolean));
  if (requestedId && ids.has(requestedId)) return requestedId;
  if (rememberedId && rememberedId !== "weapon.unarmed" && ids.has(rememberedId)) return rememberedId;
  if (activeId && activeId !== "weapon.unarmed" && ids.has(activeId)) return activeId;
  return items.find(item => item?.singleWeaponConfigurationId !== "weapon.unarmed" && item?.weaponRef)?.singleWeaponConfigurationId
    || items.find(item => item?.singleWeaponConfigurationId !== "weapon.unarmed")?.singleWeaponConfigurationId
    || (ids.has("weapon.unarmed") ? "weapon.unarmed" : items[0]?.singleWeaponConfigurationId || "");
}

function activateSingleWeaponForEditing(configurationId = "", options = {}) {
  const items = getSingleWeaponItems();
  const selectedId = selectPreferredSingleWeaponConfigurationId(
    items,
    configurationId,
    readLastEditedSingleWeaponConfigurationId(),
    state.activeSingleWeaponConfigurationId);
  state.activeSingleWeaponConfigurationId = selectedId;
  state.singleWeaponWorkspaceView = "edit";

  if (!selectedId || selectedId === "weapon.unarmed") {
    if (state.viewportMode === "weapon") globalThis.returnToCharacterViewport?.();
    else if (options.render !== false) render();
    return selectedId;
  }

  rememberLastEditedSingleWeaponConfigurationId(selectedId);
  const focusedId = String(state.focusedWeaponAttachmentId || "");
  if (state.viewportMode === "weapon" && focusedId === `single:${selectedId}`) {
    if (options.render !== false) render();
  } else {
    globalThis.enterSingleWeaponViewport?.(selectedId);
  }
  return selectedId;
}

function canonicalizeSingleWeaponValue(value) {
  if (Array.isArray(value)) return value.map(canonicalizeSingleWeaponValue);
  if (!value || typeof value !== "object") return value;
  const result = {};
  for (const key of Object.keys(value).sort()) result[key] = canonicalizeSingleWeaponValue(value[key]);
  return result;
}

function canonicalizeSingleWeaponConfiguration(value) {
  return JSON.stringify(canonicalizeSingleWeaponValue(value || {}));
}

function getSingleWeaponRecordStatus(item, baselineItems = []) {
  const id = item?.singleWeaponConfigurationId || "";
  const baseline = baselineItems.find(candidate => candidate?.singleWeaponConfigurationId === id);
  if (!baseline) return "new";
  return canonicalizeSingleWeaponConfiguration(item) === canonicalizeSingleWeaponConfiguration(baseline)
    ? "clean"
    : "modified";
}

function getSingleWeaponDraftSummary(items = [], baselineItems = []) {
  const currentIds = new Set(items.map(item => item?.singleWeaponConfigurationId).filter(Boolean));
  let added = 0;
  let modified = 0;
  for (const item of items) {
    const status = getSingleWeaponRecordStatus(item, baselineItems);
    if (status === "new") added++;
    else if (status === "modified") modified++;
  }
  const deleted = baselineItems.filter(item =>
    item?.singleWeaponConfigurationId && !currentIds.has(item.singleWeaponConfigurationId)).length;
  return { added, modified, deleted, changed: added + modified + deleted };
}

function mergeSingleWeaponReferenceIndex(
  serverIndex,
  packageRelative,
  applicationConfig,
  configurationIds = [],
  indexLoaded = true,
  currentPackageDirty = false) {
  const result = {};
  const serverEntries = Array.isArray(serverIndex?.entries) ? serverIndex.entries : [];
  const serverById = new Map(serverEntries.map(entry => [entry?.configurationId || "", entry]));
  for (const configurationId of configurationIds) {
    if (!configurationId) continue;
    const serverEntry = serverById.get(configurationId);
    const references = (serverEntry?.references || []).filter(reference =>
      String(reference?.packageRelative || "") !== String(packageRelative || ""));
    result[configurationId] = {
      configurationId,
      known: Boolean(indexLoaded || serverEntry?.known),
      references
    };
  }

  const combinations = applicationConfig?.combinedWeaponConfigurations || [];
  for (const combination of combinations) {
    for (const slot of combination?.slots || []) {
      const configurationId = slot?.singleWeaponConfigurationId || "";
      if (!result[configurationId]) continue;
      result[configurationId].references.push({
        configurationType: "singleWeapon",
        configurationId,
        packageRelative: packageRelative || "",
        combinedWeaponConfigurationId: combination?.combinedWeaponConfigurationId || "",
        slotId: slot?.slotId || "",
        draft: Boolean(currentPackageDirty)
      });
    }
  }

  for (const entry of Object.values(result)) {
    entry.references.sort((left, right) => {
      const leftKey = `${left?.packageRelative || ""}\n${left?.combinedWeaponConfigurationId || ""}\n${left?.slotId || ""}`;
      const rightKey = `${right?.packageRelative || ""}\n${right?.combinedWeaponConfigurationId || ""}\n${right?.slotId || ""}`;
      return leftKey.localeCompare(rightKey);
    });
    entry.referenceCount = entry.references.length;
    entry.characterCount = new Set(entry.references.map(reference =>
      reference?.packageRelative || "")).size;
    entry.combinationCount = new Set(entry.references.map(reference =>
      `${reference?.packageRelative || ""}\n${reference?.combinedWeaponConfigurationId || ""}`)).size;
  }
  return result;
}

function filterAndSortSingleWeaponConfigurations(
  items = [],
  options = {},
  referencesById = {},
  baselineItems = []) {
  const query = String(options.query || "").trim().toLocaleLowerCase();
  const category = options.category || "all";
  const resource = options.resource || "all";
  const reference = options.reference || "all";
  const filtered = items.filter(item => {
    if (item?.singleWeaponConfigurationId === "weapon.unarmed") return true;
    const searchable = [
      item?.singleWeaponConfigurationId,
      item?.displayName,
      item?.category,
      item?.weaponTypeId,
      getSingleWeaponTypeLabel(item),
      ...(item?.tags || [])
    ].join(" ").toLocaleLowerCase();
    if (query && !searchable.includes(query)) return false;
    if (category.startsWith("type:") ? item?.weaponTypeId !== category.slice(5) : category !== "all" && item?.category !== category) return false;
    if (resource === "bound" && !item?.weaponRef) return false;
    if (resource === "unbound" && item?.weaponRef) return false;
    const referenceEntry = referencesById[item?.singleWeaponConfigurationId || ""];
    if (reference === "referenced" && !(referenceEntry?.referenceCount > 0)) return false;
    if (reference === "unreferenced" && (!referenceEntry?.known || referenceEntry.referenceCount !== 0)) return false;
    return true;
  });

  const statusRank = { new: 0, modified: 1, clean: 2 };
  const sort = options.sort || "id";
  filtered.sort((left, right) => {
    const leftUnarmed = left?.singleWeaponConfigurationId === "weapon.unarmed";
    const rightUnarmed = right?.singleWeaponConfigurationId === "weapon.unarmed";
    if (leftUnarmed !== rightUnarmed) return leftUnarmed ? -1 : 1;
    let comparison = 0;
    if (sort === "name") comparison = String(left?.displayName || "").localeCompare(String(right?.displayName || ""));
    else if (sort === "category") comparison = String(left?.category || "").localeCompare(String(right?.category || ""));
    else if (sort === "references") comparison = (referencesById[right?.singleWeaponConfigurationId]?.referenceCount || 0) -
        (referencesById[left?.singleWeaponConfigurationId]?.referenceCount || 0);
    else if (sort === "status") comparison = (statusRank[getSingleWeaponRecordStatus(left, baselineItems)] ?? 3) -
        (statusRank[getSingleWeaponRecordStatus(right, baselineItems)] ?? 3);
    if (comparison !== 0) return comparison;
    return String(left?.singleWeaponConfigurationId || "").localeCompare(
      String(right?.singleWeaponConfigurationId || ""));
  });
  return filtered;
}

function getSingleWeaponItems() {
  state.weaponLibrary ||= {
    schemaVersion: "2.0",
    libraryId: "mx.global.single-weapons",
    singleWeaponConfigurations: [],
    animationConfigurations: [],
    abilityConfigurations: [],
    reactionConfigurations: []
  };
  state.weaponLibrary.singleWeaponConfigurations = Array.isArray(state.weaponLibrary.singleWeaponConfigurations)
    ? state.weaponLibrary.singleWeaponConfigurations
    : [];
  return state.weaponLibrary.singleWeaponConfigurations;
}

function getSingleWeaponTypes() {
  return Array.isArray(state.weaponLibrary?.weaponTypes) ? state.weaponLibrary.weaponTypes : [];
}

function getSingleWeaponTypeLabel(item) {
  if (item?.singleWeaponConfigurationId === "weapon.unarmed") return "身体 / 空手";
  const type = getSingleWeaponTypes().find(type => type?.typeId === item?.weaponTypeId);
  return type?.displayName || (item?.weaponTypeId ? "未知类型" : "未设置类型");
}

function renderSingleWeaponTypeInput(item, disabled) {
  if (item.singleWeaponConfigurationId === "weapon.unarmed")
    return renderSingleWeaponInput("weaponTypeId", "武器类型", "身体 / 空手", "string", " readonly");
  return `<label><span>武器类型</span><select data-single-weapon-field="weaponTypeId" data-value-type="string"${disabled}><option value="">请选择类型</option>${getSingleWeaponTypes().map(type => `<option value="${escapeHtml(type.typeId)}"${item.weaponTypeId === type.typeId ? " selected" : ""}>${escapeHtml(type.displayName)}</option>`).join("")}</select></label>`;
}

function getActiveSingleWeaponConfiguration() {
  const items = getSingleWeaponItems();
  return items.find(item => item?.singleWeaponConfigurationId === state.activeSingleWeaponConfigurationId)
    || items[0]
    || null;
}

function getEffectiveSingleWeaponReferences() {
  return mergeSingleWeaponReferenceIndex(
    state.singleWeaponReferenceIndex,
    state.packageRelative,
    state.package?.applicationConfig,
    getSingleWeaponItems().map(item => item?.singleWeaponConfigurationId || ""),
    state.singleWeaponReferenceIndexLoaded,
    Boolean(state.dirty));
}

async function loadSingleWeaponReferenceIndex(options = {}) {
  if (state.singleWeaponReferenceIndexLoading && !options.force) return;
  state.singleWeaponReferenceIndexLoading = true;
  state.singleWeaponReferenceIndexError = "";
  const payload = await readJson(
    "/api/weapon-library/reference-index?configurationType=singleWeapon",
    null);
  if (payload && Array.isArray(payload.entries)) {
    state.singleWeaponReferenceIndex = payload;
    state.singleWeaponReferenceIndexLoaded = true;
  } else {
    state.singleWeaponReferenceIndex = null;
    state.singleWeaponReferenceIndexLoaded = false;
    state.singleWeaponReferenceIndexError = "无法读取跨角色引用索引，请重试。";
  }
  state.singleWeaponReferenceIndexLoading = false;
  if (options.renderAfter !== false) render();
}

async function refreshSingleWeaponReference(configurationId) {
  if (!configurationId) return;
  state.singleWeaponReferenceIndexLoading = true;
  state.singleWeaponReferenceIndexError = "";
  const payload = await readJson(
    `/api/weapon-library/references?configurationType=singleWeapon&configurationId=${encodeURIComponent(configurationId)}`,
    null);
  if (payload && Array.isArray(payload.references)) {
    const entries = Array.isArray(state.singleWeaponReferenceIndex?.entries)
      ? state.singleWeaponReferenceIndex.entries.filter(entry => entry?.configurationId !== configurationId)
      : [];
    entries.push({
      configurationId,
      referenceCount: payload.references.length,
      references: payload.references,
      known: true
    });
    state.singleWeaponReferenceIndex = { configurationType: "singleWeapon", entries };
  } else {
    state.singleWeaponReferenceIndexError = `无法刷新 ${configurationId} 的引用，请稍后重试。`;
  }
  state.singleWeaponReferenceIndexLoading = false;
  render();
}

function markSingleWeaponLibraryDirty() {
  state.weaponLibraryDirty = true;
  state.weaponLibraryWriteConflict = null;
}

function setNestedSingleWeaponValue(target, path, value) {
  const parts = String(path || "").split(".").filter(Boolean);
  if (!parts.length) return;
  let cursor = target;
  for (let i = 0; i < parts.length - 1; i++) {
    const part = parts[i];
    if (!cursor[part] || typeof cursor[part] !== "object") cursor[part] = {};
    cursor = cursor[part];
  }
  cursor[parts[parts.length - 1]] = value;
}

function readNestedSingleWeaponValue(target, path, fallback = "") {
  let cursor = target;
  for (const part of String(path || "").split(".").filter(Boolean)) {
    if (cursor == null) return fallback;
    cursor = cursor[part];
  }
  return cursor ?? fallback;
}

function updateSingleWeaponConfigurationField(path, rawValue, valueType = "string") {
  const item = getActiveSingleWeaponConfiguration();
  if (!item || item.singleWeaponConfigurationId === "weapon.unarmed") return;
  let value = rawValue;
  if (valueType === "csv") value = String(rawValue || "").split(",").map(part => part.trim()).filter(Boolean);
  else if (valueType === "number") value = Number.isFinite(Number(rawValue)) ? Number(rawValue) : 0;
  else if (valueType === "boolean") value = Boolean(rawValue);
  if (/traceProfiles\.\d+\.(radius|proximalExtension|proximalRadius)$/.test(path))
    value = Math.max(0, Number(value || 0));
  if (path === "category") return; // Slot compatibility follows the authoritative type mapping.
  if (path === "weaponTypeId") {
    const type = getSingleWeaponTypes().find(type => type?.typeId === value);
    if (!type) return;
    item.category = type.equipmentCategory;
  }
  setNestedSingleWeaponValue(item, path, value);
  if (path.startsWith("modelPose.eulerHint.")) syncSingleWeaponPoseQuaternionFromEuler(item.modelPose);
  markSingleWeaponLibraryDirty();
  render();
}

function syncSingleWeaponPoseQuaternionFromEuler(pose) {
  if (!pose) return;
  const euler = pose.eulerHint || {};
  const x = Number(euler.x || 0) * Math.PI / 180;
  const y = Number(euler.y || 0) * Math.PI / 180;
  const z = Number(euler.z || 0) * Math.PI / 180;
  const c1 = Math.cos(x / 2), c2 = Math.cos(y / 2), c3 = Math.cos(z / 2);
  const s1 = Math.sin(x / 2), s2 = Math.sin(y / 2), s3 = Math.sin(z / 2);
  pose.rotation = {
    x: s1 * c2 * c3 + c1 * s2 * s3,
    y: c1 * s2 * c3 - s1 * c2 * s3,
    z: c1 * c2 * s3 + s1 * s2 * c3,
    w: c1 * c2 * c3 - s1 * s2 * s3
  };
}

function updateSingleWeaponSlotRule(path, slotId, checked) {
  const item = getActiveSingleWeaponConfiguration();
  if (!item || item.singleWeaponConfigurationId === "weapon.unarmed") return;
  const values = new Set(Array.isArray(item[path]) ? item[path] : []);
  if (checked) values.add(slotId);
  else values.delete(slotId);
  item[path] = ["mainHand", "offHand"].filter(slot => values.has(slot));
  markSingleWeaponLibraryDirty();
  render();
}

function updateSingleWeaponModifier(index, field, rawValue) {
  const item = getActiveSingleWeaponConfiguration();
  if (!item || item.singleWeaponConfigurationId === "weapon.unarmed") return;
  item.baseModifiers = Array.isArray(item.baseModifiers) ? item.baseModifiers : [];
  const modifier = item.baseModifiers[index];
  if (!modifier) return;
  modifier[field] = field === "value" ? (Number.isFinite(Number(rawValue)) ? Number(rawValue) : 0) : rawValue;
  markSingleWeaponLibraryDirty();
  render();
}

function createSingleWeaponDefault() {
  return {
    singleWeaponConfigurationId: "",
    displayName: "",
    weaponTypeId: "",
    category: "",
    tags: ["one_hand"],
    allowedSlots: ["mainHand", "offHand"],
    occupiesSlots: [],
    weaponRef: null,
    modelPose: {
      parentKind: "ModelRoot", parentPath: "",
      position: { x: 0, y: 0, z: 0 }, rotation: { x: 0, y: 0, z: 0, w: 1 },
      scale: { x: 1, y: 1, z: 1 }, eulerHint: { x: 0, y: 0, z: 0 }
    },
    presentationConfiguration: { profileId: "", displayMode: "Equipped", modelScale: 1 },
    combatConfiguration: { profileId: "", reachMeters: 1, impactScale: 1, combatTags: [] },
    resourcePolicy: { profileId: "", preloadPolicy: "EquipmentInitial", requiredForSpawn: true },
    traceProfiles: [],
    baseModifiers: []
  };
}

function uniqueSingleWeaponConfigurationId(base) {
  const used = new Set(getSingleWeaponItems().map(item => item?.singleWeaponConfigurationId));
  let candidate = base || "weapon.new";
  let index = 1;
  while (used.has(candidate)) candidate = `${base || "weapon.new"}.${index++}`;
  return candidate;
}

function openSingleWeaponCreationDialog(mode, sourceId = "") {
  const source = getSingleWeaponItems().find(item => item?.singleWeaponConfigurationId === sourceId)
    || getActiveSingleWeaponConfiguration();
  if (mode === "copy" && (!source || source.singleWeaponConfigurationId === "weapon.unarmed")) return;
  state.singleWeaponCreationDialog = {
    mode,
    sourceId: mode === "copy" ? source.singleWeaponConfigurationId : "",
    id: uniqueSingleWeaponConfigurationId(mode === "copy"
      ? `${source.singleWeaponConfigurationId}.copy`
      : "weapon.new"),
    displayName: mode === "copy" ? `${source.displayName || source.singleWeaponConfigurationId} 副本` : "新单一武器",
    error: ""
  };
  render();
}

function submitSingleWeaponCreationDialog() {
  const dialog = state.singleWeaponCreationDialog;
  if (!dialog) return;
  const id = String(dialog.id || "").trim();
  const displayName = String(dialog.displayName || "").trim();
  if (!id) dialog.error = "稳定 ID 不能为空。";
  else if (id === "weapon.unarmed") dialog.error = "weapon.unarmed 是内置保留 ID。";
  else if (getSingleWeaponItems().some(item => item?.singleWeaponConfigurationId === id))
    dialog.error = "稳定 ID 已存在；ID 区分大小写且必须唯一。";
  else if (!displayName) dialog.error = "显示名称不能为空。";
  else dialog.error = "";
  if (dialog.error) {
    render();
    return;
  }

  const source = dialog.mode === "copy"
    ? getSingleWeaponItems().find(item => item?.singleWeaponConfigurationId === dialog.sourceId)
    : null;
  const item = source ? clone(source) : createSingleWeaponDefault();
  item.singleWeaponConfigurationId = id;
  item.displayName = displayName;
  getSingleWeaponItems().push(item);
  state.singleWeaponCreationDialog = null;
  markSingleWeaponLibraryDirty();
  activateSingleWeaponForEditing(id);
}

async function deleteSingleWeaponConfiguration(configurationId) {
  const item = getSingleWeaponItems().find(candidate => candidate?.singleWeaponConfigurationId === configurationId);
  if (!item || item.singleWeaponConfigurationId === "weapon.unarmed") {
    state.message = "weapon.unarmed 是内置只读配置，不能删除。";
    render();
    return;
  }
  const reference = getEffectiveSingleWeaponReferences()[configurationId];
  if (!reference?.known) {
    state.singleWeaponReferenceDetailId = configurationId;
    state.message = "引用状态未知，已阻止删除。请先重试引用扫描。";
    render();
    return;
  }
  if (reference.referenceCount > 0) {
    state.singleWeaponReferenceDetailId = configurationId;
    state.message = `不能删除：该配置仍被 ${reference.characterCount} 个角色、${reference.combinationCount} 个组合引用。`;
    render();
    return;
  }
  if (!window.confirm(`确认从全局库草稿中删除 ${item.displayName || configurationId}（${configurationId}）？`)) return;
  state.weaponLibrary.singleWeaponConfigurations = getSingleWeaponItems().filter(candidate => candidate !== item);
  state.activeSingleWeaponConfigurationId = "";
  if (state.singleWeaponReferenceDetailId === configurationId) state.singleWeaponReferenceDetailId = "";
  markSingleWeaponLibraryDirty();
  activateSingleWeaponForEditing();
}

async function saveSingleWeaponWorkbench() {
  const saved = await saveGlobalWeaponLibrary();
  if (saved) {
    state.message = "全局武器配置库已保存。";
    showToast(state.message, "success");
  }
  render();
}

function clearSingleWeaponResource() {
  const item = getActiveSingleWeaponConfiguration();
  if (!item || item.singleWeaponConfigurationId === "weapon.unarmed" || !item.weaponRef) return;
  if (!window.confirm(`清除 ${item.displayName || item.singleWeaponConfigurationId} 的模型资源绑定？`)) return;
  item.weaponRef = null;
  markSingleWeaponLibraryDirty();
  render();
}

function renderSingleWeaponWorkbench() {
  if (!el.singleWeaponConfigurationPanel) return;
  const items = getSingleWeaponItems();
  if (!items.some(item => item?.singleWeaponConfigurationId === state.activeSingleWeaponConfigurationId))
    state.activeSingleWeaponConfigurationId = selectPreferredSingleWeaponConfigurationId(
      items, "", readLastEditedSingleWeaponConfigurationId(), "");
  const summary = getSingleWeaponDraftSummary(items, state.singleWeaponLibraryBaseline || []);
  const content = state.singleWeaponWorkspaceView === "overview"
    ? renderSingleWeaponOverview(items)
    : renderSingleWeaponEditor(items);
  el.singleWeaponConfigurationPanel.innerHTML = `
    <div class="single-weapon-workbench">
      ${renderSingleWeaponWorkbenchHeader(summary)}
      <div class="single-weapon-subtabs" role="tablist" aria-label="单一武器配置视图">
        <button type="button" data-single-weapon-action="setView" data-single-weapon-view="edit" class="${state.singleWeaponWorkspaceView === "edit" ? "active" : ""}">配置编辑</button>
        <button type="button" data-single-weapon-action="setView" data-single-weapon-view="overview" class="${state.singleWeaponWorkspaceView === "overview" ? "active" : ""}">配置总览</button>
      </div>
      ${content}
      ${renderSingleWeaponReferenceDetail()}
      ${renderSingleWeaponCreationDialog()}
    </div>`;
}

function renderSingleWeaponWorkbenchHeader(summary) {
  const referenceTone = state.singleWeaponReferenceIndexLoading ? "pending"
    : state.singleWeaponReferenceIndexLoaded ? "ok" : state.singleWeaponReferenceIndexError ? "error" : "warn";
  const referenceLabel = state.singleWeaponReferenceIndexLoading ? "扫描引用中"
    : state.singleWeaponReferenceIndexLoaded ? "引用已同步" : state.singleWeaponReferenceIndexError ? "引用未知" : "引用部分同步";
  const dirtyLabel = !state.weaponLibraryDirty ? "已保存"
    : summary.changed ? `未保存 · ${summary.changed} 项单一武器变更` : "全局库有未保存变更";
  return `<div class="single-weapon-workbench-head">
    <div><strong>全局单一武器配置</strong><span>WeaponSource/global_weapon_library.json · revision ${escapeHtml((state.weaponLibraryRevision || "missing").slice(0, 12))}</span></div>
    <div class="single-weapon-head-status">
      <span class="sync-badge ${referenceTone}">${referenceLabel}</span>
      <span class="sync-badge ${state.weaponLibraryDirty ? "warn" : "ok"}">${dirtyLabel}</span>
      <button type="button" data-single-weapon-action="new">新建配置</button>
      <button type="button" data-single-weapon-action="save"${state.weaponLibraryDirty ? "" : " disabled"}>保存全局库</button>
    </div>
  </div>`;
}

function renderSingleWeaponFilters(items) {
  const categories = getSingleWeaponTypes();
  return `<div class="single-weapon-filters">
    <label class="single-weapon-search"><span>搜索</span><input data-single-weapon-search value="${escapeHtml(state.singleWeaponSearchQuery || "")}" placeholder="ID / 名称 / 标签"></label>
    <label><span>类别</span><select data-single-weapon-filter="category"><option value="all">全部类别</option>${categories.map(type => `<option value="type:${escapeHtml(type.typeId)}"${state.singleWeaponCategoryFilter === `type:${type.typeId}` ? " selected" : ""}>${escapeHtml(type.displayName)}</option>`).join("")}</select></label>
    <label><span>资源</span><select data-single-weapon-filter="resource"><option value="all"${state.singleWeaponResourceFilter === "all" ? " selected" : ""}>全部</option><option value="bound"${state.singleWeaponResourceFilter === "bound" ? " selected" : ""}>已绑定</option><option value="unbound"${state.singleWeaponResourceFilter === "unbound" ? " selected" : ""}>未绑定</option></select></label>
    <label><span>引用</span><select data-single-weapon-filter="reference"><option value="all"${state.singleWeaponReferenceFilter === "all" ? " selected" : ""}>全部</option><option value="referenced"${state.singleWeaponReferenceFilter === "referenced" ? " selected" : ""}>已引用</option><option value="unreferenced"${state.singleWeaponReferenceFilter === "unreferenced" ? " selected" : ""}>未引用</option></select></label>
    <label><span>排序</span><select data-single-weapon-filter="sort"><option value="id"${state.singleWeaponSort === "id" ? " selected" : ""}>稳定 ID</option><option value="name"${state.singleWeaponSort === "name" ? " selected" : ""}>名称</option><option value="category"${state.singleWeaponSort === "category" ? " selected" : ""}>类别</option><option value="references"${state.singleWeaponSort === "references" ? " selected" : ""}>引用数</option><option value="status"${state.singleWeaponSort === "status" ? " selected" : ""}>修改状态</option></select></label>
  </div>`;
}

function getFilteredSingleWeaponItems(items, references) {
  return filterAndSortSingleWeaponConfigurations(items, {
    query: state.singleWeaponSearchQuery,
    category: state.singleWeaponCategoryFilter,
    resource: state.singleWeaponResourceFilter,
    reference: state.singleWeaponReferenceFilter,
    sort: state.singleWeaponSort
  }, references, state.singleWeaponLibraryBaseline || []);
}

function renderSingleWeaponEditor(items) {
  if (!items.length) return `<div class="empty">全局单一武器库为空。请新建配置。</div>`;
  const references = getEffectiveSingleWeaponReferences();
  const visibleItems = getFilteredSingleWeaponItems(items, references);
  const active = getActiveSingleWeaponConfiguration();
  const activeVisible = visibleItems.includes(active);
  return `<div class="single-weapon-editor-layout">
    <aside class="single-weapon-master-list">
      ${renderSingleWeaponFilters(items)}
      <div class="single-weapon-list-summary"><strong>${visibleItems.length}</strong><span>/ ${items.length} 条配置</span>${activeVisible ? "" : `<button type="button" data-single-weapon-action="clearFilters">当前配置已被筛选，清除筛选</button>`}</div>
      <div class="single-weapon-list">${visibleItems.map(item => renderSingleWeaponListItem(item, references)).join("") || `<div class="empty">没有匹配配置。</div>`}</div>
    </aside>
    <section class="single-weapon-detail">${active ? renderSingleWeaponDetail(active, references[active.singleWeaponConfigurationId]) : `<div class="empty">请选择或新建一条单一武器配置。</div>`}</section>
  </div>`;
}

function renderSingleWeaponListItem(item, references) {
  const id = item?.singleWeaponConfigurationId || "";
  const immutable = id === "weapon.unarmed";
  const status = getSingleWeaponRecordStatus(item, state.singleWeaponLibraryBaseline || []);
  const reference = references[id];
  return `<button type="button" class="single-weapon-list-item ${id === state.activeSingleWeaponConfigurationId ? "active" : ""}" data-single-weapon-action="select" data-single-weapon-id="${escapeHtml(id)}">
    <span class="single-weapon-list-title"><strong>${escapeHtml(item.displayName || id || "未命名")}</strong>${immutable ? `<span class="mini-chip">内置</span>` : renderSingleWeaponDraftBadge(status)}</span>
    <code>${escapeHtml(id)}</code>
    <span class="single-weapon-list-meta"><span>${escapeHtml(getSingleWeaponTypeLabel(item))}</span><span>${renderSlotSummary(item.allowedSlots)}</span></span>
    <span class="single-weapon-list-meta"><span class="${item.weaponRef || immutable ? "ok-text" : "warn-text"}">${immutable ? "无需模型" : item.weaponRef ? "模型已绑定" : "模型未绑定"}</span><span>${renderReferenceCountLabel(reference, true)}</span></span>
  </button>`;
}

function renderSingleWeaponDetail(item, reference) {
  const immutable = item.singleWeaponConfigurationId === "weapon.unarmed";
  const disabled = immutable ? " disabled" : "";
  return `<div class="single-weapon-detail-head">
      <div><strong>${escapeHtml(item.displayName || item.singleWeaponConfigurationId)}</strong><code>${escapeHtml(item.singleWeaponConfigurationId || "")}</code></div>
      <div class="single-weapon-detail-actions">${immutable ? `<span class="mini-chip">只读配置</span>` : `<span class="mini-chip ok">3D 编辑中</span>`}<button type="button" data-single-weapon-action="showReferences" data-single-weapon-id="${escapeHtml(item.singleWeaponConfigurationId)}">${renderReferenceCountLabel(reference, false)}</button><button type="button" data-single-weapon-action="copy" data-single-weapon-id="${escapeHtml(item.singleWeaponConfigurationId)}"${immutable ? " disabled" : ""}>复制</button><button type="button" class="danger" data-single-weapon-action="delete" data-single-weapon-id="${escapeHtml(item.singleWeaponConfigurationId)}"${immutable ? " disabled" : ""}>删除</button></div>
    </div>
    ${immutable ? `<div class="single-weapon-lock-note"><strong>内置只读配置</strong><span>weapon.unarmed 固定允许主副手使用，不绑定模型、attachment 或占用槽位。</span></div>` : ""}
    <div class="single-weapon-form-sections">
      ${renderSingleWeaponIdentitySection(item, disabled)}
      ${renderSingleWeaponSlotSection(item, disabled)}
      ${renderSingleWeaponResourceSection(item, immutable)}
      ${renderSingleWeaponModelPoseSection(item, disabled)}
      ${renderSingleWeaponPresentationSection(item, disabled)}
      ${renderSingleWeaponCombatSection(item, disabled)}
      ${renderSingleWeaponPolicySection(item, disabled)}
      ${renderSingleWeaponTraceSection(item, disabled)}
      ${renderSingleWeaponModifierSection(item, immutable)}
    </div>
    <p class="field-help">单一武器配置拥有完整的基础表现、战斗、资源、Trace 和 Modifier 规则；动画、能力、受击与 CharacterAction 属于组合武器配置。</p>`;
}

function renderSingleWeaponIdentitySection(item, disabled) {
  return `<article class="single-weapon-form-section"><h4>身份</h4><div class="single-weapon-field-grid">
    ${renderSingleWeaponInput("singleWeaponConfigurationId", "稳定 ID", item.singleWeaponConfigurationId, "string", " readonly")}
    ${renderSingleWeaponInput("displayName", "显示名称", item.displayName, "string", disabled)}
    ${renderSingleWeaponTypeInput(item, disabled)}
    ${renderSingleWeaponInput("category", "装备槽类别", item.category, "string", " readonly")}
    ${renderSingleWeaponInput("tags", "标签（逗号分隔）", (item.tags || []).join(", "), "csv", disabled)}
  </div></article>`;
}

function renderSingleWeaponSlotSection(item, disabled) {
  return `<article class="single-weapon-form-section"><h4>槽位规则</h4><div class="single-weapon-slot-rules">
    ${renderSingleWeaponSlotChecks("allowedSlots", "允许槽位", item.allowedSlots, disabled)}
    ${renderSingleWeaponSlotChecks("occupiesSlots", "占用槽位", item.occupiesSlots, disabled)}
  </div><p class="field-help">普通单手武器不占用额外槽位；双手武器同时占用 mainHand / offHand，且组合中只从主手挂载。</p></article>`;
}

function renderSingleWeaponResourceSection(item, immutable) {
  const resourceKey = immutable ? "内置空手不绑定模型" : getSingleWeaponResourceLabel(item.weaponRef);
  return `<article class="single-weapon-form-section"><h4>模型资源</h4><div class="single-weapon-resource-card"><div><strong>${escapeHtml(resourceKey || "尚未选择 Resource Manager 资源")}</strong><span>${immutable ? "weaponRef 固定为 null" : "资源生命周期由 Resource Manager 管理；这里保存稳定选择引用。"}</span></div><div><button type="button" data-single-weapon-action="pickResource"${immutable ? " disabled" : ""}>${item.weaponRef ? "替换资源" : "选择资源"}</button><button type="button" data-single-weapon-action="clearResource"${immutable || !item.weaponRef ? " disabled" : ""}>清除绑定</button></div></div></article>`;
}

function renderSingleWeaponPresentationSection(item, disabled) {
  return `<article class="single-weapon-form-section"><h4>表现配置</h4><div class="single-weapon-field-grid">
    ${renderSingleWeaponInput("presentationConfiguration.profileId", "Profile ID", readNestedSingleWeaponValue(item, "presentationConfiguration.profileId"), "string", disabled)}
    ${renderSingleWeaponInput("presentationConfiguration.displayMode", "Display Mode", readNestedSingleWeaponValue(item, "presentationConfiguration.displayMode", "Equipped"), "string", disabled)}
    ${renderSingleWeaponInput("presentationConfiguration.modelScale", "Model Scale", readNestedSingleWeaponValue(item, "presentationConfiguration.modelScale", 1), "number", disabled, "0.01")}
    ${renderSingleWeaponBladeTrailStyle(item, disabled)}
    ${renderSingleWeaponBladeTrailType(item, disabled)}
    ${renderSingleWeaponInput("presentationConfiguration.bladeTrailResidueMilliseconds", "拖尾残留（毫秒；0 使用预制体默认）", readNestedSingleWeaponValue(item, "presentationConfiguration.bladeTrailResidueMilliseconds", 100), "number", disabled, "1")}
    <p class="field-help">选择已登记的刀刃拖尾 GameObject 资源键；留空关闭武器默认拖尾。命中窗口中开启拖尾后自动跟随当前槽位武器。</p>
  </div></article>`;
}

function renderSingleWeaponBladeTrailStyle(item, disabled) {
  const current = readNestedSingleWeaponValue(item, "presentationConfiguration.bladeTrailResourceKey", "");
  const builtIn = "vfx.weapon.blade_trail:GameObject@mxframework.samples";
  const options = [["", "无默认拖尾"], [builtIn, "刀刃拖尾"]];
  if (current && current !== builtIn) options.push([current, "当前自定义样式"]);
  return `<label><span>默认拖尾资源</span><select data-single-weapon-field="presentationConfiguration.bladeTrailResourceKey" data-value-type="string"${disabled}>${options.map(([value, label]) => `<option value="${escapeHtml(value)}"${value === current ? " selected" : ""}>${escapeHtml(label)}</option>`).join("")}</select></label>
    <details><summary>自定义拖尾资源</summary>${renderSingleWeaponInput("presentationConfiguration.bladeTrailResourceKey", "已登记的 VFX 资源键", current, "string", disabled)}</details>`;
}

function renderSingleWeaponBladeTrailType(item, disabled) {
  const current = Number(readNestedSingleWeaponValue(item, "presentationConfiguration.bladeTrailStyleId", 0));
  const options = [[0, "使用资源默认类型"], [1, "细线刀光"], [2, "柔光带"], [3, "双线刀光"], [4, "能量拖尾"]];
  if (!options.some(([value]) => value === current)) options.push([current, "未知拖尾类型"]);
  return `<label><span>默认拖尾类型</span><select data-single-weapon-field="presentationConfiguration.bladeTrailStyleId" data-value-type="number"${disabled}>${options.map(([value, label]) => `<option value="${value}"${value === current ? " selected" : ""}>${label}</option>`).join("")}</select></label>
    <p class="field-help">类型控制刀光外观；运行时可临时覆盖，撤销覆盖后恢复武器默认。</p>`;
}

function renderSingleWeaponModelPoseSection(item, disabled) {
  return `<article class="single-weapon-form-section"><h4>武器自身变换</h4><p class="field-help">相对角色武器槽位的武器根变换；不修改角色 Socket 或 Resource Manager 导入修正。</p><div class="single-weapon-field-grid">
    ${renderSingleWeaponInput("modelPose.position.x", "Position X", readNestedSingleWeaponValue(item, "modelPose.position.x", 0), "number", disabled, "0.001")}
    ${renderSingleWeaponInput("modelPose.position.y", "Position Y", readNestedSingleWeaponValue(item, "modelPose.position.y", 0), "number", disabled, "0.001")}
    ${renderSingleWeaponInput("modelPose.position.z", "Position Z", readNestedSingleWeaponValue(item, "modelPose.position.z", 0), "number", disabled, "0.001")}
    ${renderSingleWeaponInput("modelPose.eulerHint.x", "Rotation X", readNestedSingleWeaponValue(item, "modelPose.eulerHint.x", 0), "number", disabled, "0.1")}
    ${renderSingleWeaponInput("modelPose.eulerHint.y", "Rotation Y", readNestedSingleWeaponValue(item, "modelPose.eulerHint.y", 0), "number", disabled, "0.1")}
    ${renderSingleWeaponInput("modelPose.eulerHint.z", "Rotation Z", readNestedSingleWeaponValue(item, "modelPose.eulerHint.z", 0), "number", disabled, "0.1")}
    ${renderSingleWeaponInput("modelPose.scale.x", "Scale X", readNestedSingleWeaponValue(item, "modelPose.scale.x", 1), "number", disabled, "0.01")}
    ${renderSingleWeaponInput("modelPose.scale.y", "Scale Y", readNestedSingleWeaponValue(item, "modelPose.scale.y", 1), "number", disabled, "0.01")}
    ${renderSingleWeaponInput("modelPose.scale.z", "Scale Z", readNestedSingleWeaponValue(item, "modelPose.scale.z", 1), "number", disabled, "0.01")}
  </div></article>`;
}

function renderSingleWeaponCombatSection(item, disabled) {
  return `<article class="single-weapon-form-section"><h4>战斗基础参数</h4><div class="single-weapon-field-grid">
    ${renderSingleWeaponInput("combatConfiguration.profileId", "Profile ID", readNestedSingleWeaponValue(item, "combatConfiguration.profileId"), "string", disabled)}
    ${renderSingleWeaponInput("combatConfiguration.reachMeters", "Reach (m)", readNestedSingleWeaponValue(item, "combatConfiguration.reachMeters", 0), "number", disabled, "0.01")}
    ${renderSingleWeaponInput("combatConfiguration.impactScale", "Impact Scale", readNestedSingleWeaponValue(item, "combatConfiguration.impactScale", 1), "number", disabled, "0.01")}
    ${renderSingleWeaponInput("combatConfiguration.combatTags", "Combat Tags", (readNestedSingleWeaponValue(item, "combatConfiguration.combatTags", []) || []).join(", "), "csv", disabled)}
  </div></article>`;
}

function renderSingleWeaponPolicySection(item, disabled) {
  const required = Boolean(readNestedSingleWeaponValue(item, "resourcePolicy.requiredForSpawn", true));
  return `<article class="single-weapon-form-section"><h4>资源策略</h4><div class="single-weapon-field-grid">
    ${renderSingleWeaponInput("resourcePolicy.profileId", "Profile ID", readNestedSingleWeaponValue(item, "resourcePolicy.profileId"), "string", disabled)}
    ${renderSingleWeaponInput("resourcePolicy.preloadPolicy", "Preload Policy", readNestedSingleWeaponValue(item, "resourcePolicy.preloadPolicy", "EquipmentInitial"), "string", disabled)}
    <label class="single-weapon-check-field"><span>Spawn 要求</span><span><input type="checkbox" data-single-weapon-field="resourcePolicy.requiredForSpawn" data-value-type="boolean"${required ? " checked" : ""}${disabled}> Required For Spawn</span></label>
  </div></article>`;
}

function renderSingleWeaponTraceSection(item, disabled) {
  const traces = Array.isArray(item.traceProfiles) ? item.traceProfiles : [];
  return `<article class="single-weapon-form-section"><div class="single-weapon-section-head"><div><h4>攻击轨迹</h4><span>武器局部坐标；主副手实例复用</span></div><button type="button" data-single-weapon-action="addTrace"${disabled}>新增 Trace</button></div>
    <div class="single-weapon-traces">${traces.length ? traces.map((trace, index) => `<div class="single-weapon-trace-card"><div class="single-weapon-section-head"><strong>${escapeHtml(trace?.traceId || `Trace ${index + 1}`)}</strong><button type="button" class="danger" data-single-weapon-action="removeTrace" data-trace-index="${index}"${disabled}>删除</button></div><div class="single-weapon-field-grid">
      ${renderSingleWeaponInput(`traceProfiles.${index}.traceId`, "Trace ID", trace?.traceId || "", "string", disabled)}
      ${renderSingleWeaponInput(`traceProfiles.${index}.startLocatorPath`, "Start Locator", trace?.startLocatorPath || "", "string", disabled)}
      ${renderSingleWeaponInput(`traceProfiles.${index}.endLocatorPath`, "End Locator", trace?.endLocatorPath || "", "string", disabled)}
      ${renderSingleWeaponInput(`traceProfiles.${index}.startPose.position.x`, "Start X", trace?.startPose?.position?.x ?? 0, "number", disabled, "0.001")}
      ${renderSingleWeaponInput(`traceProfiles.${index}.startPose.position.y`, "Start Y", trace?.startPose?.position?.y ?? 0, "number", disabled, "0.001")}
      ${renderSingleWeaponInput(`traceProfiles.${index}.startPose.position.z`, "Start Z", trace?.startPose?.position?.z ?? 0, "number", disabled, "0.001")}
      ${renderSingleWeaponInput(`traceProfiles.${index}.endPose.position.x`, "End X", trace?.endPose?.position?.x ?? 0, "number", disabled, "0.001")}
      ${renderSingleWeaponInput(`traceProfiles.${index}.endPose.position.y`, "End Y", trace?.endPose?.position?.y ?? 0, "number", disabled, "0.001")}
      ${renderSingleWeaponInput(`traceProfiles.${index}.endPose.position.z`, "End Z", trace?.endPose?.position?.z ?? 0, "number", disabled, "0.001")}
      ${renderSingleWeaponInput(`traceProfiles.${index}.radius`, "Radius", trace?.radius ?? 0.05, "number", disabled, "0.001")}
      ${renderSingleWeaponInput(`traceProfiles.${index}.sampleRule`, "Sample Rule", trace?.sampleRule || "CapsuleSweep", "string", disabled)}
      ${renderSingleWeaponInput(`traceProfiles.${index}.fixedSampleCount`, "Samples", trace?.fixedSampleCount ?? 4, "number", disabled, "1")}
      ${renderSingleWeaponInput(`traceProfiles.${index}.proximalExtension`, "Proximal Extension", trace?.proximalExtension ?? 0, "number", disabled, "0.01")}
      ${renderSingleWeaponInput(`traceProfiles.${index}.proximalRadius`, "Proximal Radius", trace?.proximalRadius ?? 0, "number", disabled, "0.01")}
      ${renderSingleWeaponInput(`traceProfiles.${index}.actionKeys`, "Action Keys", (trace?.actionKeys || []).join(", "), "csv", disabled)}
    </div></div>`).join("") : `<div class="empty compact">该武器没有攻击轨迹。</div>`}</div></article>`;
}

function renderSingleWeaponModifierSection(item, immutable) {
  const modifiers = Array.isArray(item.baseModifiers) ? item.baseModifiers : [];
  return `<article class="single-weapon-form-section"><div class="single-weapon-section-head"><h4>基础 Modifier</h4><button type="button" data-single-weapon-action="addModifier"${immutable ? " disabled" : ""}>新增 Modifier</button></div>
    <div class="single-weapon-modifiers">${modifiers.length ? modifiers.map((modifier, index) => `<div class="single-weapon-modifier-row">
      <label><span>Stable ID</span><input data-single-weapon-modifier-index="${index}" data-single-weapon-modifier-field="modifierStableId" value="${escapeHtml(modifier?.modifierStableId || "")}"${immutable ? " disabled" : ""}></label>
      <label><span>Value</span><input type="number" step="0.01" data-single-weapon-modifier-index="${index}" data-single-weapon-modifier-field="value" value="${escapeHtml(modifier?.value ?? 0)}"${immutable ? " disabled" : ""}></label>
      <label><span>Source</span><input data-single-weapon-modifier-index="${index}" data-single-weapon-modifier-field="source" value="${escapeHtml(modifier?.source || "")}"${immutable ? " disabled" : ""}></label>
      <button type="button" class="danger" data-single-weapon-action="removeModifier" data-modifier-index="${index}"${immutable ? " disabled" : ""}>删除</button>
    </div>`).join("") : `<div class="empty compact">没有基础 Modifier。</div>`}</div>
  </article>`;
}

function renderSingleWeaponInput(path, label, value, valueType, extra = "", step = "") {
  const type = valueType === "number" ? "number" : "text";
  return `<label><span>${escapeHtml(label)}</span><input type="${type}"${step ? ` step="${step}"` : ""} data-single-weapon-field="${escapeHtml(path)}" data-value-type="${escapeHtml(valueType)}" value="${escapeHtml(value ?? "")}"${extra}></label>`;
}

function renderSingleWeaponSlotChecks(path, label, values, disabled) {
  const selected = new Set(Array.isArray(values) ? values : []);
  return `<fieldset><legend>${escapeHtml(label)}</legend>${[["mainHand", "主手"], ["offHand", "副手"]].map(([slotId, slotLabel]) => `<label><input type="checkbox" data-single-weapon-array-field="${path}" value="${slotId}"${selected.has(slotId) ? " checked" : ""}${disabled}> ${slotLabel} <code>${slotId}</code></label>`).join("")}</fieldset>`;
}

function renderSingleWeaponOverview(items) {
  const references = getEffectiveSingleWeaponReferences();
  const visibleItems = getFilteredSingleWeaponItems(items, references);
  const summary = getSingleWeaponDraftSummary(items, state.singleWeaponLibraryBaseline || []);
  const referencedCount = items.filter(item => references[item?.singleWeaponConfigurationId]?.referenceCount > 0).length;
  const unboundCount = items.filter(item => item?.singleWeaponConfigurationId !== "weapon.unarmed" && !item?.weaponRef).length;
  return `<div class="single-weapon-overview">
    <div class="single-weapon-overview-stats">
      ${renderSingleWeaponStat("配置总数", items.length)}${renderSingleWeaponStat("已被引用", referencedCount)}${renderSingleWeaponStat("模型未绑定", unboundCount, unboundCount ? "warn" : "ok")}${renderSingleWeaponStat("草稿变更", summary.changed, summary.changed ? "warn" : "ok")}
    </div>
    ${renderSingleWeaponFilters(items)}
    <div class="single-weapon-overview-table-wrap"><table class="single-weapon-overview-table"><thead><tr><th>配置</th><th>类别 / 标签</th><th>槽位规则</th><th>模型资源</th><th>基础参数摘要</th><th>引用</th><th>状态</th><th>操作</th></tr></thead><tbody>${visibleItems.map(item => renderSingleWeaponOverviewRow(item, references[item.singleWeaponConfigurationId])).join("") || `<tr><td colspan="8"><div class="empty">没有匹配配置。</div></td></tr>`}</tbody></table></div>
  </div>`;
}

function renderSingleWeaponOverviewRow(item, reference) {
  const id = item?.singleWeaponConfigurationId || "";
  const immutable = id === "weapon.unarmed";
  const expanded = state.singleWeaponExpandedOverviewId === id;
  const status = getSingleWeaponRecordStatus(item, state.singleWeaponLibraryBaseline || []);
  return `<tr class="${immutable ? "immutable" : ""}"><td><strong>${escapeHtml(item.displayName || id)}</strong><code>${escapeHtml(id)}</code></td><td><strong>${escapeHtml(getSingleWeaponTypeLabel(item))}</strong><span>${escapeHtml((item.tags || []).join(", ") || "无标签")}</span></td><td><span>允许：${renderSlotSummary(item.allowedSlots)}</span><span>占用：${renderSlotSummary(item.occupiesSlots)}</span></td><td><span>${escapeHtml(immutable ? "无需模型" : getSingleWeaponResourceLabel(item.weaponRef) || "未绑定")}</span></td><td><span>reach ${escapeHtml(readNestedSingleWeaponValue(item, "combatConfiguration.reachMeters", 0))}m / impact ${escapeHtml(readNestedSingleWeaponValue(item, "combatConfiguration.impactScale", 1))}</span><span>${(item.traceProfiles || []).length} Trace / ${(item.baseModifiers || []).length} Modifier</span></td><td><button type="button" class="link-button" data-single-weapon-action="showReferences" data-single-weapon-id="${escapeHtml(id)}">${renderReferenceCountLabel(reference, false)}</button></td><td>${immutable ? `<span class="mini-chip">内置只读</span>` : renderSingleWeaponDraftBadge(status)}</td><td><div class="single-weapon-row-actions"><button type="button" data-single-weapon-action="editOverview" data-single-weapon-id="${escapeHtml(id)}">编辑</button><button type="button" data-single-weapon-action="copy" data-single-weapon-id="${escapeHtml(id)}"${immutable ? " disabled" : ""}>复制</button><button type="button" data-single-weapon-action="toggleOverview" data-single-weapon-id="${escapeHtml(id)}">${expanded ? "收起" : "展开"}</button><button type="button" class="danger" data-single-weapon-action="delete" data-single-weapon-id="${escapeHtml(id)}"${immutable ? " disabled" : ""}>删除</button></div></td></tr>
    ${expanded ? `<tr class="single-weapon-overview-expanded"><td colspan="8">${renderSingleWeaponReadOnlySummary(item)}</td></tr>` : ""}`;
}

function renderSingleWeaponReadOnlySummary(item) {
  const presentation = item.presentationConfiguration || {};
  const combat = item.combatConfiguration || {};
  const policy = item.resourcePolicy || {};
  const traces = item.traceProfiles || [];
  return `<div class="single-weapon-readonly-summary">
    ${renderReadOnlyGroup("身份与资源", [["ID", item.singleWeaponConfigurationId], ["名称", item.displayName], ["武器类型", getSingleWeaponTypeLabel(item)], ["装备槽类别", item.category], ["标签", (item.tags || []).join(", ")], ["Weapon Ref", getSingleWeaponResourceLabel(item.weaponRef) || "无"]])}
    ${renderReadOnlyGroup("槽位", [["允许", (item.allowedSlots || []).join(", ") || "无"], ["占用", (item.occupiesSlots || []).join(", ") || "无"]])}
    ${renderReadOnlyGroup("表现", [["Profile", presentation.profileId], ["Mode", presentation.displayMode], ["Scale", presentation.modelScale]])}
    ${renderReadOnlyGroup("战斗", [["Profile", combat.profileId], ["Reach", combat.reachMeters], ["Impact", combat.impactScale], ["Tags", (combat.combatTags || []).join(", ")]])}
    ${renderReadOnlyGroup("资源与 Trace", [["Policy Profile", policy.profileId], ["Preload", policy.preloadPolicy], ["Required", policy.requiredForSpawn], ["Trace 数量", traces.length], ["Trace IDs", traces.map(trace => trace?.traceId).filter(Boolean).join(", ") || "无"]])}
    ${renderReadOnlyGroup("Modifiers", (item.baseModifiers || []).map(modifier => [modifier.modifierStableId || "未命名", `${modifier.value ?? 0} / ${modifier.source || "无来源"}`]))}
  </div>`;
}

function renderReadOnlyGroup(title, rows) {
  return `<section><h5>${escapeHtml(title)}</h5>${rows?.length ? `<dl>${rows.map(([label, value]) => `<dt>${escapeHtml(label)}</dt><dd>${escapeHtml(value ?? "")}</dd>`).join("")}</dl>` : `<span>无</span>`}</section>`;
}

function renderSingleWeaponStat(label, value, tone = "") {
  return `<article class="single-weapon-stat ${tone}"><strong>${escapeHtml(value)}</strong><span>${escapeHtml(label)}</span></article>`;
}

function renderSingleWeaponReferenceDetail() {
  const id = state.singleWeaponReferenceDetailId;
  if (!id) return "";
  const item = getSingleWeaponItems().find(candidate => candidate?.singleWeaponConfigurationId === id);
  const reference = getEffectiveSingleWeaponReferences()[id];
  if (!item) return "";
  const body = !reference?.known
    ? `<div class="empty">引用状态未知。删除保持阻止状态，直到扫描成功。</div><button type="button" data-single-weapon-action="retryReferences">重试引用扫描</button>`
    : reference.references.length
      ? `<div class="single-weapon-reference-list">${reference.references.map(entry => `<article><strong>${escapeHtml(entry.packageRelative || "角色")}</strong><span>${escapeHtml(entry.combinedWeaponConfigurationId || "组合")} / ${escapeHtml(entry.slotId || "配置")}${entry.draft ? " · 当前草稿" : ""}</span></article>`).join("")}</div>`
      : `<div class="empty compact">当前没有角色组合引用。</div>`;
  return `<aside class="single-weapon-reference-detail"><div class="single-weapon-section-head"><div><strong>引用明细 · ${escapeHtml(item.displayName || id)}</strong><span>${renderReferenceCountLabel(reference, false)}</span></div><button type="button" data-single-weapon-action="closeReferences">关闭</button></div>${body}</aside>`;
}

function renderSingleWeaponCreationDialog() {
  const dialog = state.singleWeaponCreationDialog;
  if (!dialog) return "";
  const copy = dialog.mode === "copy";
  return `<div class="single-weapon-dialog-backdrop"><form class="single-weapon-dialog" data-single-weapon-create-form><div><strong>${copy ? "复制单一武器配置" : "新建单一武器配置"}</strong><span>稳定 ID 在创建后立即锁定；配置仍随全局库统一保存。</span></div><label><span>稳定 ID</span><input required autofocus data-single-weapon-create-field="id" value="${escapeHtml(dialog.id || "")}"></label><label><span>显示名称</span><input required data-single-weapon-create-field="displayName" value="${escapeHtml(dialog.displayName || "")}"></label>${dialog.error ? `<p class="single-weapon-dialog-error">${escapeHtml(dialog.error)}</p>` : ""}<div class="single-weapon-dialog-actions"><button type="button" data-single-weapon-action="closeCreate">取消</button><button type="submit">${copy ? "创建副本" : "创建配置"}</button></div></form></div>`;
}

function renderSingleWeaponDraftBadge(status) {
  if (status === "new") return `<span class="sync-badge pending">新增</span>`;
  if (status === "modified") return `<span class="sync-badge warn">已修改</span>`;
  return `<span class="sync-badge ok">已保存</span>`;
}

function renderReferenceCountLabel(reference, compact) {
  if (!reference?.known) return "引用未知";
  if (compact) return `${reference.combinationCount} 组合`;
  return `${reference.characterCount} 角色 / ${reference.combinationCount} 组合 / ${reference.referenceCount} 槽位`;
}

function renderSlotSummary(slots) {
  const values = Array.isArray(slots) ? slots : [];
  if (!values.length) return "无";
  return values.map(slot => slot === "mainHand" ? "主手" : slot === "offHand" ? "副手" : slot).join(" + ");
}

function getSingleWeaponResourceLabel(reference) {
  if (!reference) return "";
  if (typeof getResourceSelectionKey === "function") {
    const key = getResourceSelectionKey(reference);
    if (key) return key;
  }
  return reference.runtimeResourceKey || reference.providerResourceKey || reference.resourceStableId || "";
}

function clearSingleWeaponFilters() {
  state.singleWeaponSearchQuery = "";
  state.singleWeaponCategoryFilter = "all";
  state.singleWeaponResourceFilter = "all";
  state.singleWeaponReferenceFilter = "all";
  state.singleWeaponSort = "id";
  render();
}

async function handleSingleWeaponWorkbenchClick(event) {
  const button = event.target.closest("button[data-single-weapon-action]");
  if (!button || button.disabled) return;
  const action = button.dataset.singleWeaponAction;
  const id = button.dataset.singleWeaponId || state.activeSingleWeaponConfigurationId;
  if (action === "setView") {
    state.singleWeaponWorkspaceView = button.dataset.singleWeaponView === "overview" ? "overview" : "edit";
    if (state.singleWeaponWorkspaceView === "overview" && state.viewportMode === "weapon")
      globalThis.returnToCharacterViewport?.();
    else if (state.singleWeaponWorkspaceView === "edit") activateSingleWeaponForEditing();
    else render();
  } else if (action === "select") {
    activateSingleWeaponForEditing(id);
  } else if (action === "editOverview") {
    activateSingleWeaponForEditing(id);
  } else if (action === "new") openSingleWeaponCreationDialog("new");
  else if (action === "copy") openSingleWeaponCreationDialog("copy", id);
  else if (action === "delete") await deleteSingleWeaponConfiguration(id);
  else if (action === "save") await saveSingleWeaponWorkbench();
  else if (action === "pickResource") openSingleWeaponResourcePicker(state.activeSingleWeaponConfigurationId);
  else if (action === "clearResource") clearSingleWeaponResource();
  else if (action === "retryReferences") {
    if (state.singleWeaponReferenceDetailId) await refreshSingleWeaponReference(state.singleWeaponReferenceDetailId);
    else await loadSingleWeaponReferenceIndex({ force: true });
  }
  else if (action === "showReferences") {
    state.singleWeaponReferenceDetailId = id;
    render();
  } else if (action === "closeReferences") {
    state.singleWeaponReferenceDetailId = "";
    render();
  } else if (action === "toggleOverview") {
    state.singleWeaponExpandedOverviewId = state.singleWeaponExpandedOverviewId === id ? "" : id;
    render();
  } else if (action === "clearFilters") clearSingleWeaponFilters();
  else if (action === "addModifier") {
    const item = getActiveSingleWeaponConfiguration();
    if (item && item.singleWeaponConfigurationId !== "weapon.unarmed") {
      item.baseModifiers = Array.isArray(item.baseModifiers) ? item.baseModifiers : [];
      item.baseModifiers.push({ modifierStableId: "", value: 0, source: "" });
      markSingleWeaponLibraryDirty();
      render();
    }
  } else if (action === "addTrace") {
    const item = getActiveSingleWeaponConfiguration();
    if (item && item.singleWeaponConfigurationId !== "weapon.unarmed") {
      const pose = createSingleWeaponDefault().modelPose;
      item.traceProfiles = Array.isArray(item.traceProfiles) ? item.traceProfiles : [];
      item.traceProfiles.push({
        traceId: `trace.${item.singleWeaponConfigurationId}.${item.traceProfiles.length + 1}`,
        startLocatorPath: "", endLocatorPath: "", startPose: clone(pose),
        endPose: { ...clone(pose), position: { x: 0, y: 1, z: 0 } },
        radius: 0.05, proximalExtension: 0, proximalRadius: 0,
        sampleRule: "CapsuleSweep", fixedSampleCount: 4, actionKeys: ["primary"]
      });
      markSingleWeaponLibraryDirty();
      render();
    }
  } else if (action === "removeTrace") {
    const item = getActiveSingleWeaponConfiguration();
    const index = Number(button.dataset.traceIndex);
    if (item && item.singleWeaponConfigurationId !== "weapon.unarmed" && Number.isInteger(index)) {
      item.traceProfiles.splice(index, 1);
      markSingleWeaponLibraryDirty();
      render();
    }
  } else if (action === "removeModifier") {
    const item = getActiveSingleWeaponConfiguration();
    const index = Number(button.dataset.modifierIndex);
    if (item && item.singleWeaponConfigurationId !== "weapon.unarmed" && Number.isInteger(index)) {
      item.baseModifiers.splice(index, 1);
      markSingleWeaponLibraryDirty();
      render();
    }
  } else if (action === "closeCreate") {
    state.singleWeaponCreationDialog = null;
    render();
  }
}

function handleSingleWeaponWorkbenchChange(event) {
  const filter = event.target.closest("[data-single-weapon-filter]");
  if (filter) {
    const key = filter.dataset.singleWeaponFilter;
    if (key === "category") state.singleWeaponCategoryFilter = filter.value || "all";
    else if (key === "resource") state.singleWeaponResourceFilter = filter.value || "all";
    else if (key === "reference") state.singleWeaponReferenceFilter = filter.value || "all";
    else if (key === "sort") state.singleWeaponSort = filter.value || "id";
    render();
    return;
  }
  const arrayField = event.target.closest("[data-single-weapon-array-field]");
  if (arrayField) {
    updateSingleWeaponSlotRule(arrayField.dataset.singleWeaponArrayField, arrayField.value, arrayField.checked);
    return;
  }
  const modifier = event.target.closest("[data-single-weapon-modifier-field]");
  if (modifier) {
    updateSingleWeaponModifier(
      Number(modifier.dataset.singleWeaponModifierIndex),
      modifier.dataset.singleWeaponModifierField,
      modifier.value);
    return;
  }
  const field = event.target.closest("[data-single-weapon-field]");
  if (!field || field.readOnly || field.disabled) return;
  const valueType = field.dataset.valueType || "string";
  updateSingleWeaponConfigurationField(
    field.dataset.singleWeaponField,
    valueType === "boolean" ? field.checked : field.value,
    valueType);
}

function handleSingleWeaponWorkbenchInput(event) {
  const creationField = event.target.closest("[data-single-weapon-create-field]");
  if (creationField && state.singleWeaponCreationDialog) {
    state.singleWeaponCreationDialog[creationField.dataset.singleWeaponCreateField] = creationField.value;
    state.singleWeaponCreationDialog.error = "";
    return;
  }
  const search = event.target.closest("[data-single-weapon-search]");
  if (!search) return;
  const start = search.selectionStart;
  state.singleWeaponSearchQuery = search.value || "";
  renderSingleWeaponWorkbench();
  const next = el.singleWeaponConfigurationPanel?.querySelector("[data-single-weapon-search]");
  next?.focus();
  if (next && Number.isInteger(start)) next.setSelectionRange(start, start);
}

function handleSingleWeaponWorkbenchSubmit(event) {
  if (!event.target.closest("[data-single-weapon-create-form]")) return;
  event.preventDefault();
  submitSingleWeaponCreationDialog();
}

function wireSingleWeaponWorkbench() {
  const panel = el.singleWeaponConfigurationPanel;
  if (!panel || panel.dataset.singleWeaponWorkbenchWired === "1") return;
  panel.dataset.singleWeaponWorkbenchWired = "1";
  panel.addEventListener("click", handleSingleWeaponWorkbenchClick);
  panel.addEventListener("change", handleSingleWeaponWorkbenchChange);
  panel.addEventListener("input", handleSingleWeaponWorkbenchInput);
  panel.addEventListener("submit", handleSingleWeaponWorkbenchSubmit);
}

Object.assign(globalThis, {
  loadSingleWeaponReferenceIndex,
  renderSingleWeaponWorkbench,
  wireSingleWeaponWorkbench,
  getActiveSingleWeaponConfiguration,
  openSingleWeaponCreationDialog,
  activateSingleWeaponForEditing
});

globalThis.SingleWeaponWorkbenchTest = {
  canonicalizeSingleWeaponConfiguration,
  getSingleWeaponRecordStatus,
  getSingleWeaponDraftSummary,
  mergeSingleWeaponReferenceIndex,
  filterAndSortSingleWeaponConfigurations,
  selectPreferredSingleWeaponConfigurationId,
  readLastEditedSingleWeaponConfigurationId,
  rememberLastEditedSingleWeaponConfigurationId
};

export {
  canonicalizeSingleWeaponConfiguration,
  getSingleWeaponRecordStatus,
  getSingleWeaponDraftSummary,
  mergeSingleWeaponReferenceIndex,
  filterAndSortSingleWeaponConfigurations,
  selectPreferredSingleWeaponConfigurationId
};
